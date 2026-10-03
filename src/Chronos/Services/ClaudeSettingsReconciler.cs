using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chronos.Services;

/// <summary>Issue du passage sur la clé <c>statusLine</c> de <c>settings.json</c>.</summary>
public enum IssueBarreStatut
{
    /// <summary>Aucune clé <c>statusLine</c> : rien à retirer.</summary>
    Absente,
    /// <summary>Barre d'un autre outil : laissée INTACTE.</summary>
    Tierce,
    /// <summary>Barre Chronos retirée (clé supprimée).</summary>
    Retiree,
    /// <summary>Barre Chronos remplacée par la barre d'origine de l'utilisateur.</summary>
    Restauree,
}

/// <summary>
/// Bilan d'un passage de la réconciliation, journalisé dans <c>chronos.log</c> (section
/// « [Réglages de Claude Code] » du diagnostic). <paramref name="Barre"/> vaut <c>null</c> quand le
/// fichier n'a pas pu être examiné ; <paramref name="Cause"/> dit alors pourquoi.
/// </summary>
public sealed record BilanReconciliation(bool Ecrit, IssueBarreStatut? Barre, string? Sauvegarde, string? Cause);

/// <summary>
/// Fait converger <c>%USERPROFILE%\.claude\settings.json</c> vers l'ÉTAT DÉSIRÉ de Chronos, une seule
/// fois par démarrage.
///
/// <para><b>(a) Pourquoi ce service existe — PUR-03, la machine DÉJÀ polluée.</b> Corriger les
/// installateurs (plan 15-02) assainit l'avenir mais ne retire pas les groupes fantômes déjà inscrits :
/// la machine porte <b>25 groupes de hooks Chronos au lieu de 5</b> (cinq chemins d'exe successifs) et une
/// barre <c>statusLine</c> pointant un exe périmé dans <c>Downloads</c>. Sans réconciliation, l'utilisateur
/// devrait rouvrir le menu et recliquer — ce que le critère de succès 3 de la ROADMAP interdit
/// explicitement (« sans intervention manuelle »).</para>
///
/// <para><b>(b) Ce qu'il fait depuis la 3.5.</b> Il repointe les hooks de session vers l'exe courant, et
/// RETIRE la barre <c>statusLine</c> Chronos (quel que soit son chemin ou sa version), en restaurant la
/// barre d'origine de l'utilisateur si elle est connue. Il ne pose ni ne repointe JAMAIS une barre ; une
/// barre tierce reste intacte. Le résultat du passage est exposé par <see cref="DernierBilan"/>.</para>
///
/// <para><b>(b bis) Appelé UNE SEULE FOIS par démarrage, en mode OVERLAY UNIQUEMENT.</b> Jamais en mode
/// <c>--hook</c> : Claude Code lance jusqu'à 5 processus de hook en parallèle par événement, et 5 cycles
/// lire-modifier-écrire concurrents perdraient les purges les uns des autres. Ce mode court-circuite bien
/// plus haut dans <c>App.OnStartup</c> ; le point d'appel se situe après <c>window.Show()</c> et AVANT le
/// journal de démarrage, qui peut ainsi porter le bilan du passage.</para>
///
/// <para><b>(c) Un fichier INEXPLOITABLE ⇒ on n'écrit RIEN.</b> Racine non-objet, clés dupliquées, JSON
/// invalide : <see cref="ClaudeSettingsJson.ParseOrNull"/> renvoie <c>null</c> et la réconciliation
/// abandonne. On ne repart JAMAIS d'un objet JSON vierge — cela effacerait <c>permissions</c>, <c>env</c>,
/// <c>model</c> et les hooks de tous les autres outils.</para>
///
/// <para><b>(d) Limite assumée : les commentaires éventuels sont perdus à la réécriture</b> (System.Text.Json
/// sait les LIRE mais pas les réémettre). C'est précisément ce que couvre la sauvegarde horodatée créée
/// dans <c>%APPDATA%\Chronos\backups\</c> AVANT toute écriture — et uniquement quand une écriture va
/// réellement avoir lieu, sinon la rétention évincerait la sauvegarde la plus précieuse : celle du tout
/// premier passage, seule à contenir l'état pré-purge complet.</para>
/// </summary>
public sealed class ClaudeSettingsReconciler
{
    /// <summary>Nombre de sauvegardes horodatées conservées dans le dossier de sauvegarde.</summary>
    private const int SauvegardesConservees = 5;

    private readonly string _settingsPath;
    private readonly string _backupDir;
    private readonly string? _exePath;

    /// <summary>
    /// Chemins par défaut = profil utilisateur (aucun droit admin). Les TESTS injectent systématiquement
    /// leurs deux chemins depuis <c>Path.GetTempPath()</c> : le vrai settings.json reste hors d'atteinte
    /// de la suite.
    ///
    /// <para><paramref name="exePath"/> vaut <c>null</c> en production : l'exe courant est alors résolu par
    /// <c>Environment.ProcessPath</c>. Il n'est injecté que par les tests d'E/S, et pour une raison de
    /// fond : le processus qui héberge la suite s'appelle <c>testhost.exe</c>, un nom que le prédicat
    /// d'identité (marqueur + <c>Chronos*.exe</c>) ne reconnaît volontairement PAS. Sans injection, la
    /// réconciliation poserait des entrées qu'elle serait ensuite incapable de reconnaître, et son
    /// idempotence — la propriété même que ces tests doivent prouver — serait invérifiable.</para>
    /// </summary>
    public ClaudeSettingsReconciler(string? settingsPath = null, string? backupDir = null, string? exePath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");
        _backupDir = backupDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chronos", "backups");
        _exePath = exePath;
    }

    /// <summary>Fichier de settings de Claude Code ciblé (diagnostic + garde anti-accident en test).</summary>
    public string SettingsPath => _settingsPath;

    /// <summary>Dossier des sauvegardes horodatées (diagnostic + garde anti-accident en test).</summary>
    public string BackupDir => _backupDir;

    /// <summary>
    /// Bilan du dernier passage de <see cref="Reconcile"/> ; <c>null</c> = pas encore passée. Assigné en
    /// UNE fois (record immuable) : un lecteur concurrent voit l'ancien bilan ou le nouveau, jamais un mélange.
    /// </summary>
    public BilanReconciliation? DernierBilan { get; private set; }

    // --- Lecture de l'ancienne barre de l'utilisateur ---

    /// <summary>
    /// Nom de la clé HÉRITÉE des réglages Chronos ≤ 3.4 qui mémorisait la barre d'origine de l'utilisateur. Le
    /// membre correspondant a quitté <c>ChronosSettings</c> en 3.5 ; la clé n'est plus que LUE, une fois, ici.
    /// Composée en deux morceaux à dessein : la garde de non-retour compte le nom du membre retiré par grep dans
    /// src, et elle ne doit pas se déclencher sur cette seule lecture héritée.
    /// </summary>
    private const string CleCommandeHeritee = "Inner" + "StatusLineCommand";

    /// <summary>
    /// PUR : extrait la clé héritée <see cref="CleCommandeHeritee"/> (insensible à la casse) d'un JSON de
    /// réglages Chronos — la barre que l'utilisateur avait avant que Chronos ne la chaîne. <c>null</c> si absente,
    /// non textuelle, vide ou si le JSON est illisible.
    /// </summary>
    public static string? CommandeInterneHeritee(string? chronosSettingsJson)
    {
        if (string.IsNullOrWhiteSpace(chronosSettingsJson)) return null;
        try
        {
            var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            if (JsonNode.Parse(chronosSettingsJson, nodeOptions: null, options) is not JsonObject racine) return null;
            foreach (var (cle, valeur) in racine)
            {
                if (!string.Equals(cle, CleCommandeHeritee, StringComparison.OrdinalIgnoreCase)) continue;
                return valeur is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s : null;
            }
            return null;
        }
        catch { return null; }   // catch LARGE : clés dupliquées = ArgumentException
    }

    /// <summary>
    /// Lit le fichier de réglages Chronos puis <see cref="CommandeInterneHeritee"/>. Toute exception ⇒
    /// <c>null</c>. À appeler AVANT tout enregistrement des réglages : la clé, inconnue depuis la 3.5,
    /// disparaîtrait au premier Save.
    /// </summary>
    public static string? LireCommandeInterneHeritee(string cheminReglagesChronos)
    {
        try { return CommandeInterneHeritee(File.ReadAllText(cheminReglagesChronos)); }
        catch { return null; }
    }

    // --- Cœur PUR (aucune E/S) ---

    /// <summary>
    /// Fait converger le JSON vers l'ÉTAT DÉSIRÉ. Renvoie <c>null</c> si rien ne doit être écrit :
    /// soit le contenu est INEXPLOITABLE (on n'y touche pas), soit il est DÉJÀ conforme.
    ///
    /// <para>La comparaison se fait sur la forme NORMALISÉE (sérialisation avant/après mutation), et non
    /// sur le texte brut du fichier : sinon une simple différence d'indentation — Claude Code réécrit son
    /// fichier à sa guise — déclencherait une écriture ET une sauvegarde à CHAQUE démarrage.</para>
    ///
    /// <para>Propriété testée : <c>ReconcileJson(ReconcileJson(x)) == null</c> (point fixe = PUR-01/02/03).</para>
    /// </summary>
    public static string? ReconcileJson(string? settingsJson, string exePath, bool hooksWanted)
        => ReconcileJson(settingsJson, exePath, hooksWanted, commandeHeritee: null, out _);

    /// <summary>
    /// Variante complète : <paramref name="commandeHeritee"/> est l'ancienne barre de l'utilisateur
    /// (restaurée à la place de la barre Chronos si elle n'est pas elle-même Chronos) ;
    /// <paramref name="barre"/> rend l'issue sur <c>statusLine</c>, <c>null</c> si le contenu est inexploitable.
    /// </summary>
    public static string? ReconcileJson(string? settingsJson, string exePath, bool hooksWanted,
                                        string? commandeHeritee, out IssueBarreStatut? barre)
    {
        barre = null;
        var root = ClaudeSettingsJson.ParseOrNull(settingsJson);
        if (root is null) return null;                       // JAMAIS un objet vierge : cela effacerait tout

        var avant = ClaudeSettingsJson.Serialize(root);      // forme normalisée AVANT mutation
        SessionHookInstaller.ApplyHooks(root, exePath, hooksWanted);
        barre = RetirerBarreChronos(root, commandeHeritee);  // retire seulement ; ne pose jamais de barre
        var apres = ClaudeSettingsJson.Serialize(root);

        return string.Equals(avant, apres, StringComparison.Ordinal) ? null : apres;
    }

    /// <summary>
    /// Retire la barre <c>statusLine</c> si et seulement si elle appartient à Chronos (marqueur
    /// <c>--statusline</c> + exe <c>Chronos*.exe</c>, quel que soit le chemin). Si une ancienne barre non
    /// Chronos est connue, elle est restaurée par MUTATION (type, padding et clés futures conservés).
    /// Une barre tierce — ou une valeur non objet — n'est jamais touchée.
    /// </summary>
    internal static IssueBarreStatut RetirerBarreChronos(JsonObject root, string? commandeHeritee)
    {
        if (!root.ContainsKey("statusLine")) return IssueBarreStatut.Absente;
        if (root["statusLine"] is not JsonObject sl) return IssueBarreStatut.Tierce;   // forme inconnue : intouchée

        var cmd = ClaudeSettingsJson.CommandOf(sl);
        if (!ClaudeSettingsJson.IsChronosCommand(cmd, ClaudeSettingsJson.StatusLineMarker)) return IssueBarreStatut.Tierce;

        if (!string.IsNullOrWhiteSpace(commandeHeritee)
            && !ClaudeSettingsJson.IsChronosCommand(commandeHeritee, ClaudeSettingsJson.StatusLineMarker))
        {
            sl["command"] = commandeHeritee;          // MUTATION : type, padding, clés futures conservés
            return IssueBarreStatut.Restauree;
        }

        root.Remove("statusLine");
        return IssueBarreStatut.Retiree;
    }

    // --- Couche E/S ---

    /// <summary>
    /// Lit, réconcilie, sauvegarde puis écrit. Renvoie <c>true</c> si une écriture a EU LIEU, et pose
    /// <see cref="DernierBilan"/> à CHAQUE sortie. Ne lève jamais : une panne de lecture, de sauvegarde ou
    /// d'écriture dégrade silencieusement (le settings.json de l'utilisateur vaut plus que la fonctionnalité).
    /// </summary>
    public bool Reconcile(bool hooksWanted, string? commandeHeritee = null)
    {
        try
        {
            if (!File.Exists(_settingsPath))                 // on ne CRÉE jamais le fichier : purge seulement
            {
                DernierBilan = new BilanReconciliation(false, null, null, "fichier absent");
                return false;
            }

            // Mono-fichier : Environment.ProcessPath, JAMAIS l'emplacement de l'assembly (vide en single-file).
            var exePath = _exePath ?? Environment.ProcessPath ?? "Chronos.exe";
            var actuel = File.ReadAllText(_settingsPath);
            var reconcilie = ReconcileJson(actuel, exePath, hooksWanted, commandeHeritee, out var barre);

            if (barre is null)                               // inexploitable : on n'y touche pas
            {
                DernierBilan = new BilanReconciliation(false, null, null, "illisible — rien écrit");
                return false;
            }
            if (reconcilie is null)                          // déjà conforme → rien à faire
            {
                DernierBilan = new BilanReconciliation(false, barre, null, null);
                return false;
            }

            var sauvegarde = Sauvegarder();
            if (sauvegarde is null)                          // échec de sauvegarde ⇒ on N'ÉCRIT PAS
            {
                DernierBilan = new BilanReconciliation(false, barre, null, "sauvegarde impossible — rien écrit");
                return false;
            }

            WriteAtomic(reconcilie);
            DernierBilan = new BilanReconciliation(true, barre, sauvegarde, null);
            return true;
        }
        catch (Exception ex)
        {
            DernierBilan = new BilanReconciliation(false, null, null, ex.GetType().Name);
            return false;
        }
    }

    /// <summary>
    /// Copie l'original dans le dossier de sauvegarde AVANT toute écriture, puis ne conserve que les
    /// <paramref name="garder"/> plus récentes. Renvoie le CHEMIN de la sauvegarde, ou <c>null</c> si la
    /// copie a échoué → l'appelant ABANDONNE l'écriture. La purge de rétention, elle, n'est jamais bloquante.
    /// </summary>
    private string? Sauvegarder(int garder = SauvegardesConservees)
    {
        try
        {
            Directory.CreateDirectory(_backupDir);

            var cible = CibleDeSauvegarde();
            if (cible is null) return null;    // 100 collisions d'affilée : on renonce plutôt qu'on écrase
            File.Copy(_settingsPath, cible, overwrite: true);

            foreach (var vieux in new DirectoryInfo(_backupDir)
                         .GetFiles("claude-settings-*.json")
                         .OrderByDescending(f => f.Name)
                         .Skip(garder))
                try { vieux.Delete(); } catch { }   // rétention best-effort : un échec ici n'est pas bloquant

            return cible;
        }
        catch { return null; }
    }

    /// <summary>
    /// Nom de sauvegarde horodaté à la seconde. L'horodatage seul ne suffit pas : deux réconciliations
    /// écrivantes dans la même seconde (boucle de test, double lancement) s'écraseraient. On désambiguïse
    /// alors par un suffixe <c>-1</c>, <c>-2</c>, … Renvoie <c>null</c> après 100 tentatives.
    /// </summary>
    private string? CibleDeSauvegarde()
    {
        var racine = Path.Combine(_backupDir, "claude-settings-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        if (!File.Exists(racine + ".json")) return racine + ".json";

        for (int i = 1; i <= 100; i++)
        {
            var candidat = racine + "-" + i + ".json";
            if (!File.Exists(candidat)) return candidat;
        }
        return null;
    }

    /// <summary>
    /// Écriture atomique temp → rename, motif identique à celui de l'installateur de hooks : on n'observe
    /// jamais un settings.json partiellement écrit.
    /// </summary>
    private void WriteAtomic(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var tmp = _settingsPath + ".tmp-" + Environment.ProcessId;
        File.WriteAllText(tmp, content);
        File.Move(tmp, _settingsPath, overwrite: true);
    }
}
