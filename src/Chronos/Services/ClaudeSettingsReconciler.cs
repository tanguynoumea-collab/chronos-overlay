using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Chronos.Services;

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
/// <para><b>(b) Appelé UNE SEULE FOIS par démarrage, en mode OVERLAY UNIQUEMENT.</b> Jamais en mode
/// <c>--hook</c> : Claude Code lance jusqu'à 5 processus de hook en parallèle par événement, et 5 cycles
/// lire-modifier-écrire concurrents perdraient les purges les uns des autres. Jamais non plus en mode
/// <c>--statusline</c>, invoqué à CHAQUE rendu de la barre. Les deux modes court-circuitent bien plus haut
/// dans <c>App.OnStartup</c> ; le point d'appel se situe après <c>window.Show()</c> et avant
/// <c>OfferOnFirstRun()</c>, de sorte que l'offre de source exacte porte sur un état déjà propre.</para>
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
    {
        var root = ClaudeSettingsJson.ParseOrNull(settingsJson);
        if (root is null) return null;                       // JAMAIS un objet vierge : cela effacerait tout

        var avant = ClaudeSettingsJson.Serialize(root);      // forme normalisée AVANT mutation
        SessionHookInstaller.ApplyHooks(root, exePath, hooksWanted);
        StatusLineInstaller.ApplyStatusLine(root, exePath);  // repointe seulement ; n'installe jamais
        var apres = ClaudeSettingsJson.Serialize(root);

        return string.Equals(avant, apres, StringComparison.Ordinal) ? null : apres;
    }

    // --- Couche E/S ---

    /// <summary>
    /// Lit, réconcilie, sauvegarde puis écrit. Renvoie <c>true</c> si une écriture a EU LIEU. Ne lève
    /// jamais : une panne de lecture, de sauvegarde ou d'écriture dégrade silencieusement (le settings.json
    /// de l'utilisateur vaut plus que la fonctionnalité).
    /// </summary>
    public bool Reconcile(bool hooksWanted)
    {
        try
        {
            if (!File.Exists(_settingsPath)) return false;   // on ne CRÉE jamais le fichier : purge seulement

            // Mono-fichier : Environment.ProcessPath, JAMAIS l'emplacement de l'assembly (vide en single-file).
            var exePath = _exePath ?? Environment.ProcessPath ?? "Chronos.exe";
            var actuel = File.ReadAllText(_settingsPath);
            var reconcilie = ReconcileJson(actuel, exePath, hooksWanted);
            if (reconcilie is null) return false;            // inexploitable OU déjà conforme → rien à faire
            if (!Sauvegarder()) return false;                // échec de sauvegarde ⇒ on N'ÉCRIT PAS
            WriteAtomic(reconcilie);
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Copie l'original dans le dossier de sauvegarde AVANT toute écriture, puis ne conserve que les
    /// <paramref name="garder"/> plus récentes. Renvoie <c>false</c> si la copie a échoué → l'appelant
    /// ABANDONNE l'écriture. La purge de rétention, elle, n'est jamais bloquante.
    /// </summary>
    private bool Sauvegarder(int garder = SauvegardesConservees)
    {
        try
        {
            Directory.CreateDirectory(_backupDir);

            var cible = CibleDeSauvegarde();
            if (cible is null) return false;   // 100 collisions d'affilée : on renonce plutôt qu'on écrase
            File.Copy(_settingsPath, cible, overwrite: true);

            foreach (var vieux in new DirectoryInfo(_backupDir)
                         .GetFiles("claude-settings-*.json")
                         .OrderByDescending(f => f.Name)
                         .Skip(garder))
                try { vieux.Delete(); } catch { }   // rétention best-effort : un échec ici n'est pas bloquant

            return true;
        }
        catch { return false; }
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
    /// Écriture atomique temp → rename, motif identique à celui des deux installateurs : on n'observe
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
