using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Chronos.Models;
using Chronos.Models.Historique;

namespace Chronos.Services.Historique;

/// <summary>
/// Ce qu'une purge a RÉELLEMENT fait (motif <c>BilanBalayage</c>) : fichiers supprimés, suppressions en échec
/// (comptées, jamais relancées), noms ignorés parce que non conformes à <c>releves-AAAA-MM.jsonl</c>.
/// </summary>
public sealed record BilanRetention(int FichiersSupprimes, int Echecs, int Ignores);

/// <summary>
/// JRN-01 / JRN-03 — l'écrivain du journal des relevés exacts.
///
/// <para><b>POURQUOI pas le mode d'ouverture « ajout » de <c>FileStream</c></b> : sous Windows il fait un <c>Seek(End)</c> à l'OUVERTURE, pas un
/// <c>O_APPEND</c> ; deux appenders concurrents ont produit une seule ligne (mesuré le 2026-09-27, .NET 8.0.25).
/// <b>POURQUOI pas temp + <c>Move</c></b> : c'est un fichier à AJOUT, pas à réécriture, et le <c>Move</c> perd face à un
/// lecteur (290 pertes sur 500, phase 23). Donc : partage EXCLUSIF + reprises bornées (motif
/// <c>EcritureEtatSession.EcrireAvecReprise</c>) + relecture de la QUEUE sous le MÊME verrou → la séquence
/// lire-comparer-écrire est atomique entre processus ET entre threads. Une ligne &lt; 4 Ko, UTF-8 sans BOM, « \n » seul.</para>
///
/// <para><b>Idempotence</b> : clé (<c>t</c>, <c>source</c>). Un relevé n'est écrit que si son <c>t</c> est STRICTEMENT
/// postérieur au dernier <c>t</c> de la même source relu dans la queue du fichier du mois. C'est ce qui élimine le
/// rejeu du cache de la sonde (même instance 5 fois sur 6) même sans mémoire, et ce qui rend deux processus
/// inoffensifs l'un pour l'autre. Les ÉVÉNEMENTS ne sont pas dédoublonnés : deux arrêts sont deux faits.</para>
///
/// <para><b>Fichiers mensuels</b> par mois UTC de <c>t</c> (D-32-16) ; <b>rétention</b> <see cref="RetentionMois"/> mois,
/// aucune compaction. <b>Seuils</b> dérivés de la cadence nominale de la sonde (D-32-17), jamais en dur.</para>
///
/// <para>Une écriture qui échoue pose <see cref="DerniereErreur"/> et n'est PAS relancée : le prochain relevé arrive
/// dans cinq minutes, et le canal d'observabilité (JRN-04) est là pour le dire. Ne lève jamais vers l'appelant.
/// Type NEUTRE ; horloge injectée ; chemin injecté (<c>ChronosPaths.HistoriqueDir</c> en production, 32-05).</para>
///
/// <para><see cref="IEtatMagasin"/> (CPT-02, câblé en 32-05) : le journal est le deuxième des trois magasins persistants de la
/// section « [Magasins persistants] » du diagnostic — <see cref="Nom"/> l'apparie, <see cref="Chemin"/> est le DOSSIER
/// (il n'a pas de fichier unique : la règle « dernière écriture = mtime » du dernier exact ne s'applique pas, l'horloge injectée date).</para>
/// </summary>
public sealed class JournalReleves : IEtatJournal, IEtatMagasin
{
    public string Nom => NomsMagasins.JournalReleves;
    public string Chemin => Dossier;

    /// <summary>Au-delà, un fichier mensuel est supprimé au démarrage. 24 mois : deux fois la plage la plus longue affichée (4 semaines) fois douze.</summary>
    public const int RetentionMois = 24;

    /// <summary>Trou de couverture (JRN-05) : deux cadences sans relevé = 10 min. Dérivé, jamais 600 s en dur (D-32-17).</summary>
    public static readonly TimeSpan SeuilReprise = 2 * RateLimitHeaderUsageProvider.CadenceNominale;

    /// <summary>Alerte « journal muet » (JRN-04) : trois cadences sans écriture = 15 min. Dérivé, jamais 900 s en dur (D-32-17).</summary>
    public static readonly TimeSpan SeuilMuet = 3 * RateLimitHeaderUsageProvider.CadenceNominale;

    // Reprises : les douze premières cèdent simplement la main (une écriture concurrente dure des microsecondes),
    // les suivantes dorment 1 ms. Borne plus haute que celle des hooks (60) parce que deux écrivains du journal
    // peuvent boucler SERRÉ (test à deux écrivains ; deux overlays qui rattrapent un retard) : avec 60 essais, un
    // écrivain qui tient le fichier 95 % du temps ferait échouer l'autre une fois sur vingt. ~0,6 s au pire.
    private const int EssaisMax = 600;

    // Queue relue pour l'idempotence : 16 Ko ≈ 70 lignes ≈ 6 h de relevés. Assez pour retrouver le dernier t de
    // chaque source active ; une source muette depuis plus longtemps que la queue est traitée comme « jamais vue ».
    private const int QueueRelue = 16 * 1024;

    private static readonly Regex NomMensuel = new(@"^releves-(\d{4})-(\d{2})\.jsonl$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly UTF8Encoding Utf8SansBom = new(encoderShouldEmitUTF8Identifier: false);

    // Entre threads du MÊME processus (le consommateur de l'orchestrateur et les événements d'authentification
    // arrivent sur des threads différents) : sérialiser ici évite de payer les reprises du verrou de fichier.
    private readonly object _verrou = new();
    private readonly IClock _clock;

    public JournalReleves(string dossier, IClock clock)
    {
        Dossier = dossier ?? throw new ArgumentNullException(nameof(dossier));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public string Dossier { get; }
    public DateTimeOffset? DerniereEcriture { get; private set; }
    public string? DerniereErreur { get; private set; }
    public int RelevesEcrits { get; private set; }
    public int EvenementsEcrits { get; private set; }

    /// <summary>Nom du fichier mensuel : mois UTC de <c>t</c> (D-32-16), chiffres invariants.</summary>
    public static string NomFichier(DateTimeOffset t)
        => "releves-" + t.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".jsonl";

    /// <summary>Chemin complet du fichier mensuel qui reçoit un instant <c>t</c>.</summary>
    public string CheminDuMois(DateTimeOffset t) => Path.Combine(Dossier, NomFichier(t));

    /// <summary>
    /// Ajoute un relevé s'il est NOUVEAU pour sa source (clé (t, source), t strictement croissant).
    /// <c>false</c> = déjà présent (ce n'est pas une erreur : <see cref="DerniereErreur"/> reste nulle) OU échec
    /// d'écriture (<see cref="DerniereErreur"/> posée). Ne lève jamais.
    /// </summary>
    public bool AjouterReleve(ReleveJournal r)
    {
        var ligne = Octets(LigneJournal.Serialiser(r));
        lock (_verrou)
        {
            var ecrit = EcrireSousVerrou(CheminDuMois(r.T), ligne,
                                         fs => DernierTDansFlux(fs, r.Source) is { } dernier && r.T <= dernier);
            if (ecrit) RelevesEcrits++;
            return ecrit;
        }
    }

    /// <summary>Ajoute un événement, toujours (pas d'idempotence : deux arrêts sont deux faits). <c>false</c> = échec.</summary>
    public bool AjouterEvenement(EvenementJournal e)
    {
        var ligne = Octets(LigneJournal.Serialiser(e));
        lock (_verrou)
        {
            var ecrit = EcrireSousVerrou(CheminDuMois(e.T), ligne, _ => false);
            if (ecrit) EvenementsEcrits++;
            return ecrit;
        }
    }

    /// <summary>
    /// Dernier <c>t</c> écrit pour une source, relu dans la queue du fichier du mois COURANT (horloge injectée),
    /// à défaut du mois précédent. <c>null</c> = rien de lisible. Sert à AMORCER la mémoire du décorateur au
    /// démarrage ; la vérité de l'idempotence reste la relecture sous verrou de <see cref="AjouterReleve"/>.
    /// </summary>
    public DateTimeOffset? DernierT(SourceUsage source)
    {
        var maintenant = _clock.UtcNow;
        lock (_verrou)
        {
            return LireDernierT(CheminDuMois(maintenant), source)
                   ?? LireDernierT(CheminDuMois(maintenant.AddMonths(-1)), source);
        }
    }

    /// <summary>
    /// Rétention : supprime les fichiers mensuels dont le mois est antérieur à (mois courant − <see cref="RetentionMois"/>).
    /// Un nom non conforme est ignoré et compté ; une suppression en échec est comptée, jamais relancée ; un dossier
    /// absent rend un bilan à zéro. Motif <c>BalayageMagasinSessions.Balayer</c>. Ne lève jamais.
    /// </summary>
    public BilanRetention Purger()
    {
        if (!Directory.Exists(Dossier)) return new BilanRetention(0, 0, 0);

        string[] fichiers;
        try { fichiers = Directory.GetFiles(Dossier); }
        catch { return new BilanRetention(0, 0, 0); }

        var maintenant = _clock.UtcNow;
        var limite = maintenant.Year * 12 + maintenant.Month - RetentionMois;   // index de mois : tout ce qui est STRICTEMENT avant part

        int supprimes = 0, echecs = 0, ignores = 0;
        foreach (var fichier in fichiers)
        {
            var m = NomMensuel.Match(Path.GetFileName(fichier));
            if (!m.Success
                || !int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var annee)
                || !int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var mois)
                || mois is < 1 or > 12)
            {
                ignores++;
                continue;
            }

            if (annee * 12 + mois >= limite) continue;   // dans la rétention : gardé, et pas compté

            try { File.Delete(fichier); supprimes++; }
            catch { echecs++; }   // pas supprimé = pas compté comme supprimé : le bilan reste une observation
        }

        return new BilanRetention(supprimes, echecs, ignores);
    }

    // --- Cœur : append exclusif avec relecture sous le même verrou ---

    private static byte[] Octets(string ligneJson) => Utf8SansBom.GetBytes(ligneJson + "\n");

    // Le dossier se crée HORS de la boucle de reprises : un dossier qu'on ne peut pas créer (un FICHIER porte son
    // nom, droits refusés) n'est pas un verrou transitoire — on le dit tout de suite au lieu d'insister 600 fois.
    private bool EcrireSousVerrou(string chemin, byte[] ligne, Func<FileStream, bool> dejaPresent)
    {
        try { Directory.CreateDirectory(Dossier); }
        catch (Exception ex) { DerniereErreur = Decrire(ex); return false; }

        for (var essai = 1; ; essai++)
        {
            try
            {
                using var fs = new FileStream(chemin, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                if (dejaPresent(fs)) return false;   // relu sous le MÊME verrou que l'écriture qui suit
                fs.Seek(0, SeekOrigin.End);
                fs.Write(ligne, 0, ligne.Length);   // une seule écriture, < 4 Ko, terminée par '\n'
                fs.Flush(flushToDisk: false);
                DerniereEcriture = _clock.UtcNow;
                DerniereErreur = null;
                return true;
            }
            catch (IOException) when (essai < EssaisMax)
            {
                if (essai <= 12) Thread.Yield(); else Thread.Sleep(1);   // l'autre écrivain tient le fichier : céder la main
            }
            catch (Exception ex)
            {
                // UnauthorizedAccessException (pas une IOException) sort ici sans reprise ; l'IOException du dernier essai aussi.
                DerniereErreur = Decrire(ex);
                return false;
            }
        }
    }

    private static string Decrire(Exception ex) => ex.GetType().Name + " : " + ex.Message;

    // Lecture seule de la queue d'un fichier (partage large : un écrivain peut le tenir un instant → quelques reprises courtes).
    private static DateTimeOffset? LireDernierT(string chemin, SourceUsage source)
    {
        if (!File.Exists(chemin)) return null;
        for (var essai = 1; ; essai++)
        {
            try
            {
                using var fs = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return DernierTDansFlux(fs, source);
            }
            catch (IOException) when (essai < 20) { if (essai <= 12) Thread.Yield(); else Thread.Sleep(1); }
            catch (Exception) { return null; }   // illisible = « jamais vu » : l'idempotence sous verrou reste la vérité
        }
    }

    // Les derniers min(QueueRelue, longueur) octets, découpés sur '\n', parcourus de la FIN vers le début : le premier
    // relevé de la source cherchée donne son t. Si la queue est tronquée en tête, le premier fragment est écarté.
    private static DateTimeOffset? DernierTDansFlux(FileStream fs, SourceUsage source)
    {
        var longueur = fs.Length;
        if (longueur == 0) return null;

        var taille = (int)Math.Min(QueueRelue, longueur);
        fs.Seek(-taille, SeekOrigin.End);
        var tampon = new byte[taille];
        fs.ReadExactly(tampon, 0, taille);

        var lignes = Utf8SansBom.GetString(tampon).Split('\n');
        var premiere = taille < longueur ? 1 : 0;   // fragment de tête : pas une ligne

        for (var i = lignes.Length - 1; i >= premiere; i--)
        {
            var ligne = lignes[i].TrimEnd('\r');
            if (ligne.Length == 0) continue;
            if (LigneJournal.Parser(ligne, out var releve, out _) && releve is { } r && r.Source == source)
                return r.T;
        }
        return null;
    }
}
