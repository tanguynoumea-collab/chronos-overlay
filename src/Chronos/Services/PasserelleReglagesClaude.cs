using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Chronos.Services;

/// <summary>Issue d'une lecture de <c>~/.claude/settings.json</c> par la passerelle.</summary>
public enum IssueLectureClaude
{
    /// <summary>Le fichier n'existe pas : seul cas où « objet vide » est un point de départ légitime.</summary>
    Absent,
    /// <summary>Le fichier existe et son contenu est un objet JSON exploitable.</summary>
    Lu,
    /// <summary>Le fichier existe mais n'a pas pu être lu, ou son contenu est vide, blanc ou invalide : on n'écrit RIEN.</summary>
    Inexploitable,
}

/// <summary>
/// Résultat de <see cref="PasserelleReglagesClaude.Lire"/>. <paramref name="MtimeUtc"/> et <paramref name="Taille"/> sont
/// relevés AVANT la lecture du texte : ils servent de témoin « inchangé » juste avant l'écriture.
/// </summary>
public sealed record LectureReglagesClaude(IssueLectureClaude Issue, string? Texte, DateTime? MtimeUtc, long? Taille, string? Cause);

/// <summary>Résultat de <see cref="PasserelleReglagesClaude.Ecrire"/> : écrit ou non, chemin de la sauvegarde, cause d'un refus.</summary>
public sealed record EcritureReglagesClaude(bool Ecrit, string? Sauvegarde, string? Cause);

/// <summary>
/// SEULE voie de lecture et d'écriture de <c>%USERPROFILE%\.claude\settings.json</c> (MAT-1, DATA-4, FIAB-8).
/// L'installateur de hooks et le réconciliateur de démarrage passent tous deux par elle : deux écrivains avec deux
/// prudences différentes, c'était la garantie que l'un des deux finirait par écrire sans sauvegarde.
///
/// <para><b>Pourquoi tant de gardes.</b> Ce fichier est la configuration EXÉCUTABLE de Claude Code : il porte les règles
/// <c>permissions</c> (dont les <c>deny</c> qui protègent l'utilisateur), <c>env</c>, <c>model</c> et les hooks des
/// autres outils. Le réécrire à partir d'une ignorance — fichier vide pendant une réécriture tronquante, lecture ratée,
/// contenu changé par Claude Code entre-temps — effacerait ces règles en silence. Chaque garde ci-dessous transforme une
/// ignorance en « rien écrit, et voici pourquoi ».</para>
///
/// <para>Type NEUTRE : aucune dépendance WPF, ne lève jamais (toute panne devient un refus « Type : message »).</para>
/// </summary>
public sealed class PasserelleReglagesClaude
{
    /// <summary>Nombre de sauvegardes horodatées conservées dans le dossier de sauvegarde.</summary>
    private const int SauvegardesConservees = 5;

    /// <summary>FIAB-R1 (42.2-11) : nom de la sauvegarde ÉPINGLÉE (état d'avant Chronos), jamais évincée ni réécrite.</summary>
    public const string NomSauvegardeInitiale = "claude-settings-initial.json";

    /// <summary>Essais de lecture sur une erreur d'E/S passagère (Claude Code peut tenir le fichier un instant).</summary>
    private const int EssaisLecture = 3;

    private readonly string _settingsPath;
    private readonly string _backupDir;

    /// <summary>Les TESTS passent toujours deux chemins sous <c>Path.GetTempPath()</c> ; seule la production appelle <see cref="ParDefaut"/>.</summary>
    public PasserelleReglagesClaude(string settingsPath, string backupDir)
    {
        _settingsPath = settingsPath;
        _backupDir = backupDir;
    }

    /// <summary>Vrais chemins du profil (aucun droit admin) : <c>%USERPROFILE%\.claude\settings.json</c> et <c>%APPDATA%\Chronos\backups</c>.</summary>
    public static PasserelleReglagesClaude ParDefaut() => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chronos", "backups"));

    /// <summary>Fichier de réglages de Claude Code ciblé (diagnostic + garde anti-accident en test).</summary>
    public string SettingsPath => _settingsPath;

    /// <summary>Dossier des sauvegardes horodatées (diagnostic + garde anti-accident en test).</summary>
    public string BackupDir => _backupDir;

    /// <summary>
    /// Lecture tri-état. Absent ⇒ <see cref="IssueLectureClaude.Absent"/>. Erreur d'E/S persistante après
    /// <see cref="EssaisLecture"/> essais, contenu vide/blanc ou JSON inexploitable ⇒ <see cref="IssueLectureClaude.Inexploitable"/>.
    /// Ne lève jamais.
    /// </summary>
    public LectureReglagesClaude Lire()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new LectureReglagesClaude(IssueLectureClaude.Absent, null, null, null, null);

            Exception? derniere = null;
            for (int essai = 1; essai <= EssaisLecture; essai++)
            {
                try
                {
                    // Témoins relevés AVANT le texte : si un autre écrivain passe entre les deux, le texte lu est plus
                    // récent que le témoin et le contrôle d'avant écriture refusera — jamais l'inverse.
                    var info = new FileInfo(_settingsPath);
                    var mtime = info.LastWriteTimeUtc;
                    var taille = info.Length;
                    var texte = File.ReadAllText(_settingsPath);

                    if (ClaudeSettingsJson.ParseOrNull(texte) is null)
                        return new LectureReglagesClaude(IssueLectureClaude.Inexploitable, null, mtime, taille,
                            string.IsNullOrWhiteSpace(texte) ? "fichier vide" : "JSON inexploitable");

                    return new LectureReglagesClaude(IssueLectureClaude.Lu, texte, mtime, taille, null);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    derniere = ex;
                    if (essai < EssaisLecture) Thread.Sleep(20);
                }
            }
            return new LectureReglagesClaude(IssueLectureClaude.Inexploitable, null, null, null, Decrire(derniere!));
        }
        catch (Exception ex)
        {
            return new LectureReglagesClaude(IssueLectureClaude.Inexploitable, null, null, null, Decrire(ex));
        }
    }

    /// <summary>
    /// Écrit <paramref name="nouveauTexte"/> à la place du contenu décrit par <paramref name="lecture"/>, ou refuse.
    /// Refus : lecture inexploitable ; fichier absent sans <paramref name="creerSiAbsent"/> ; lien symbolique ;
    /// sauvegarde impossible ; fichier modifié (ou apparu) entre la lecture et l'écriture. Une écriture sur un fichier
    /// LU est précédée d'une sauvegarde du TEXTE LU. Ne lève jamais.
    /// </summary>
    public EcritureReglagesClaude Ecrire(LectureReglagesClaude lecture, string nouveauTexte, bool creerSiAbsent)
    {
        string? tmp = null;
        try
        {
            // Une ignorance ne devient jamais une écriture : on ne sait pas ce que contient le fichier.
            if (lecture.Issue == IssueLectureClaude.Inexploitable)
                return new EcritureReglagesClaude(false, null, "illisible — " + (lecture.Cause ?? "cause inconnue") + " — rien écrit");
            if (lecture.Issue == IssueLectureClaude.Absent && !creerSiAbsent)
                return new EcritureReglagesClaude(false, null, "fichier absent");

            // FIAB-8 c : File.Move remplacerait le LIEN par un fichier ordinaire ; la cible (dotfiles synchronisés, par
            // exemple) ne verrait jamais l'écriture et le lien de l'utilisateur serait détruit.
            if (File.Exists(_settingsPath) && (File.GetAttributes(_settingsPath) & FileAttributes.ReparsePoint) != 0)
                return new EcritureReglagesClaude(false, null, "lien symbolique — Chronos n'écrit pas au travers");

            // Sauvegarde du TEXTE LU, pas une copie disque faite maintenant : celle-ci pourrait déjà porter l'écriture
            // d'un autre (Claude Code écrit aussi ce fichier), et ce serait l'état d'origine qu'on perdrait.
            string? sauvegarde = null;
            if (lecture.Issue == IssueLectureClaude.Lu)
            {
                sauvegarde = Sauvegarder(lecture.Texte ?? "");
                if (sauvegarde is null)
                    return new EcritureReglagesClaude(false, null, "sauvegarde impossible — rien écrit");
            }

            var dossier = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(dossier);
            tmp = Path.Combine(dossier, Path.GetFileName(_settingsPath) + ".tmp-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(tmp, nouveauTexte);

            // Deux écrivains (Claude Code et Chronos) : JUSTE AVANT le Move, le fichier doit être celui qu'on a lu. Sinon
            // on écraserait une écriture de Claude Code faite entre-temps avec une version calculée sur l'ancienne.
            if (!Inchange(lecture))
            {
                SupprimerSansLever(tmp);
                return new EcritureReglagesClaude(false, null, "modifié entre lecture et écriture — rien écrit");
            }

            File.Move(tmp, _settingsPath, overwrite: true);   // atomique : jamais de settings.json à moitié écrit
            tmp = null;
            return new EcritureReglagesClaude(true, sauvegarde, null);
        }
        catch (Exception ex)
        {
            if (tmp is not null) SupprimerSansLever(tmp);
            return new EcritureReglagesClaude(false, null, Decrire(ex));
        }
    }

    /// <summary>Le fichier est-il encore dans l'état décrit par la lecture ? Absent ⇒ il doit l'être encore ; Lu ⇒ mêmes mtime et taille.</summary>
    private bool Inchange(LectureReglagesClaude lecture)
    {
        if (lecture.Issue == IssueLectureClaude.Absent) return !File.Exists(_settingsPath);
        if (!File.Exists(_settingsPath)) return false;
        var info = new FileInfo(_settingsPath);
        return info.LastWriteTimeUtc == lecture.MtimeUtc && info.Length == lecture.Taille;
    }

    /// <summary>
    /// Écrit le texte lu dans le dossier de sauvegarde, puis ne conserve que les <see cref="SauvegardesConservees"/>
    /// plus récentes (best-effort). Rend le CHEMIN, ou <c>null</c> si la sauvegarde a échoué → l'appelant N'ÉCRIT PAS.
    /// </summary>
    private string? Sauvegarder(string texte)
    {
        try
        {
            Directory.CreateDirectory(_backupDir);
            Epingler(texte);
            var cible = CibleDeSauvegarde();
            if (cible is null) return null;   // 100 collisions d'affilée : on renonce plutôt qu'on écrase
            File.WriteAllText(cible, texte);

            // SEC-R4 (42.2-11) : tri sur (horodatage, numéro de collision) — le nom brut faisait passer « -10 » avant « -9 » et
            // la base avant ses doublons. L'épingle n'a pas d'horodatage : elle n'entre jamais dans le tri. La sauvegarde du
            // jour n'est jamais évincée (transition des anciens noms en heure locale vers l'UTC).
            foreach (var vieux in new DirectoryInfo(_backupDir)
                         .GetFiles("claude-settings-*.json")
                         .Select(f => (Fichier: f, Cle: CleDeSauvegarde(f.Name)))
                         .Where(x => x.Cle is not null)
                         .OrderByDescending(x => x.Cle!.Value.Horodatage, StringComparer.Ordinal)
                         .ThenByDescending(x => x.Cle!.Value.Collision)
                         .Skip(SauvegardesConservees)
                         .Select(x => x.Fichier))
            {
                if (string.Equals(vieux.FullName, Path.GetFullPath(cible), StringComparison.OrdinalIgnoreCase)) continue;
                try { vieux.Delete(); } catch { }   // rétention best-effort : un échec ici n'est pas bloquant
            }

            return cible;
        }
        catch { return null; }
    }

    /// <summary>
    /// Nom horodaté à la seconde ; deux écritures dans la même seconde s'écraseraient, d'où le suffixe <c>-1</c>,
    /// <c>-2</c>, … Rend <c>null</c> après 100 tentatives.
    /// SEC-R4 (42.2-11) : le numéro part TOUJOURS au-delà du plus grand déjà présent pour cette seconde — réutiliser un trou
    /// laissé par la rétention (la base évincée) rangeait la sauvegarde du jour parmi les plus anciennes.
    /// </summary>
    private string? CibleDeSauvegarde()
    {
        // SEC-R4 (42.2-11) : UTC et chiffres invariants — ni le fuseau ni la culture (calendrier non grégorien) ne déplacent le tri.
        var horodatage = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var racine = Path.Combine(_backupDir, "claude-settings-" + horodatage);

        var max = -1;   // -1 : aucune sauvegarde de cette seconde ; 0 : la base ; n : « -n »
        foreach (var f in Directory.GetFiles(_backupDir, "claude-settings-" + horodatage + "*.json"))
            if (CleDeSauvegarde(Path.GetFileName(f)) is { } c && c.Horodatage == horodatage && c.Collision > max) max = c.Collision;

        if (max < 0 && !File.Exists(racine + ".json")) return racine + ".json";
        for (int i = Math.Max(max + 1, 1); i <= Math.Max(max + 1, 1) + 100; i++)
        {
            var candidat = racine + "-" + i.ToString(CultureInfo.InvariantCulture) + ".json";
            if (!File.Exists(candidat)) return candidat;
        }
        return null;
    }

    /// <summary>
    /// FIAB-R1 (42.2-11) — épingle UNE FOIS POUR TOUTES l'état d'avant Chronos sous <see cref="NomSauvegardeInitiale"/> : chaque
    /// bascule du widget consomme une des <see cref="SauvegardesConservees"/> sauvegardes, et l'original était évincé en trois
    /// allers-retours. Contenu : la plus ANCIENNE sauvegarde horodatée déjà présente (versions précédentes), sinon le texte lu.
    /// Jamais réécrite (création exclusive) ; un échec n'empêche pas la sauvegarde horodatée.
    /// </summary>
    private void Epingler(string texte)
    {
        var epingle = Path.Combine(_backupDir, NomSauvegardeInitiale);
        if (File.Exists(epingle)) return;
        try
        {
            var plusAncienne = new DirectoryInfo(_backupDir)
                .GetFiles("claude-settings-*.json")
                .Select(f => (Fichier: f, Cle: CleDeSauvegarde(f.Name)))
                .Where(x => x.Cle is not null)
                .OrderBy(x => x.Cle!.Value.Horodatage, StringComparer.Ordinal)
                .ThenBy(x => x.Cle!.Value.Collision)
                .Select(x => x.Fichier)
                .FirstOrDefault();
            var contenu = plusAncienne is not null ? File.ReadAllBytes(plusAncienne.FullName) : Encoding.UTF8.GetBytes(texte);
            using var flux = new FileStream(epingle, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            flux.Write(contenu, 0, contenu.Length);
        }
        catch { /* déjà créée par un autre processus, ou disque : l'épingle est best-effort */ }
    }

    /// <summary>(horodatage « yyyyMMdd-HHmmss », numéro de collision) d'une sauvegarde horodatée ; null pour tout autre nom.</summary>
    private static (string Horodatage, int Collision)? CleDeSauvegarde(string nom)
    {
        var m = NomHorodate.Match(nom);
        if (!m.Success) return null;
        var collision = m.Groups[2].Success && int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
        return (m.Groups[1].Value, collision);
    }

    private static readonly Regex NomHorodate =
        new(@"^claude-settings-(\d{8}-\d{6})(?:-(\d{1,6}))?\.json\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static void SupprimerSansLever(string chemin)
    {
        try { File.Delete(chemin); } catch { }
    }

    private static string Decrire(Exception ex) => ex.GetType().Name + " : " + ex.Message;
}
