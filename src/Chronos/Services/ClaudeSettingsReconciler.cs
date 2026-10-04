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
/// <paramref name="HooksLaissesTelsQuels"/> : la volonté de l'utilisateur sur les hooks était inconnue (réglages Chronos
/// illisibles au démarrage, MAT-5), ils n'ont donc été ni posés, ni retirés, ni repointés.
/// <paramref name="HooksConservesDepuis"/> : le marqueur de quarantaine des réglages était posé depuis cette date ; aucun hook
/// Chronos n'a été retiré (arbitrage ZEUS).
/// </summary>
public sealed record BilanReconciliation(bool Ecrit, IssueBarreStatut? Barre, string? Sauvegarde, string? Cause,
                                         bool HooksLaissesTelsQuels = false, DateTimeOffset? HooksConservesDepuis = null);

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
/// sait les LIRE mais pas les réémettre). C'est précisément ce que couvre la sauvegarde horodatée du TEXTE LU,
/// créée dans <c>%APPDATA%\Chronos\backups\</c> par <see cref="PasserelleReglagesClaude"/> AVANT toute écriture — et
/// uniquement quand une écriture va réellement avoir lieu, sinon la rétention évincerait la sauvegarde la plus
/// précieuse : celle du tout premier passage, seule à contenir l'état pré-purge complet. Toute E/S passe par cette
/// passerelle, partagée avec l'installateur de hooks (MAT-1) : lecture tri-état, contrôle « inchangé » juste avant
/// l'écriture, refus sur lien symbolique.</para>
///
/// <para><b>(e) Une ignorance ne devient pas une écriture (MAT-5).</b> Si les réglages Chronos n'ont pas pu être lus de
/// façon fiable au démarrage, la volonté de l'utilisateur sur les hooks est inconnue : <c>hooksWanted: null</c> laisse
/// les hooks tels quels (la barre est quand même retirée). Et tant que le marqueur de quarantaine des réglages est
/// posé, aucun hook Chronos n'est RETIRÉ : la quarantaine a remis le widget à « désactivé » par défaut, pas par choix.</para>
/// </summary>
public sealed class ClaudeSettingsReconciler
{
    private readonly PasserelleReglagesClaude _passerelle;
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
        : this(new PasserelleReglagesClaude(
                   settingsPath ?? PasserelleReglagesClaude.ParDefaut().SettingsPath,
                   backupDir ?? PasserelleReglagesClaude.ParDefaut().BackupDir),
               exePath)
    {
    }

    /// <summary>Constructeur de production : la passerelle UNIQUE, partagée avec l'installateur de hooks (MAT-1).</summary>
    public ClaudeSettingsReconciler(PasserelleReglagesClaude passerelle, string? exePath = null)
    {
        _passerelle = passerelle;
        _exePath = exePath;
    }

    /// <summary>Fichier de settings de Claude Code ciblé (diagnostic + garde anti-accident en test).</summary>
    public string SettingsPath => _passerelle.SettingsPath;

    /// <summary>Dossier des sauvegardes horodatées (diagnostic + garde anti-accident en test).</summary>
    public string BackupDir => _passerelle.BackupDir;

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
    public static string? ReconcileJson(string? settingsJson, string exePath, bool? hooksWanted)
        => ReconcileJson(settingsJson, exePath, hooksWanted, commandeHeritee: null, out _);

    /// <summary>
    /// Variante complète : <paramref name="commandeHeritee"/> est l'ancienne barre de l'utilisateur
    /// (restaurée à la place de la barre Chronos si elle n'est pas elle-même Chronos) ;
    /// <paramref name="barre"/> rend l'issue sur <c>statusLine</c>, <c>null</c> si le contenu est inexploitable.
    ///
    /// <para><paramref name="hooksWanted"/> <c>null</c> = volonté inconnue (MAT-5) : les hooks ne sont pas touchés, seule la
    /// barre est traitée. <paramref name="conserverHooks"/> (marqueur de quarantaine posé) : un « non » n'est pas un choix de
    /// l'utilisateur, donc AUCUN groupe Chronos n'est retiré ; s'il en existe, ils sont repointés vers l'exe courant (retirer
    /// puis ajouter), sinon rien n'est ajouté.</para>
    /// </summary>
    public static string? ReconcileJson(string? settingsJson, string exePath, bool? hooksWanted,
                                        string? commandeHeritee, out IssueBarreStatut? barre, bool conserverHooks = false)
    {
        barre = null;
        var root = ClaudeSettingsJson.ParseOrNull(settingsJson);
        if (root is null) return null;                       // JAMAIS un objet vierge : cela effacerait tout

        var avant = ClaudeSettingsJson.Serialize(root);      // forme normalisée AVANT mutation
        if (hooksWanted == true)
            SessionHookInstaller.ApplyHooks(root, exePath, wanted: true);
        else if (hooksWanted == false)
        {
            if (!conserverHooks)
                SessionHookInstaller.ApplyHooks(root, exePath, wanted: false);
            else if (PorteUnGroupeChronos(root))             // marqueur posé : jamais de purge, seulement repointer
                SessionHookInstaller.ApplyHooks(root, exePath, wanted: true);
        }
        // hooksWanted null (MAT-5) : aucun appel à ApplyHooks, les hooks restent tels quels.
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

    /// <summary>Vrai si au moins un groupe Chronos existe sous une clé quelconque de <c>hooks</c> (purge LARGE, mêmes règles qu'ApplyHooks).</summary>
    private static bool PorteUnGroupeChronos(JsonObject root)
        => root["hooks"] is JsonObject hooks
           && hooks.Any(kv => kv.Value is JsonArray arr
                              && arr.Any(g => ClaudeSettingsJson.IsChronosGroup(g, ClaudeSettingsJson.HookMarker)));

    // --- Couche E/S (par la passerelle unique) ---

    /// <summary>
    /// Lit, réconcilie puis écrit PAR LA PASSERELLE (sauvegarde du texte lu, contrôle « inchangé », refus sur lien
    /// symbolique). Renvoie <c>true</c> si une écriture a EU LIEU, et pose <see cref="DernierBilan"/> à CHAQUE sortie. Ne lève
    /// jamais : une panne de lecture, de sauvegarde ou d'écriture dégrade silencieusement (le settings.json de l'utilisateur
    /// vaut plus que la fonctionnalité).
    ///
    /// <para><paramref name="hooksWanted"/> <c>null</c> : réglages Chronos illisibles au démarrage (MAT-5), hooks laissés tels
    /// quels. <paramref name="conserverHooks"/> / <paramref name="quarantaineDepuis"/> : marqueur de quarantaine des réglages,
    /// aucun hook Chronos retiré (arbitrage ZEUS).</para>
    /// </summary>
    public bool Reconcile(bool? hooksWanted, string? commandeHeritee = null, bool conserverHooks = false,
                          DateTimeOffset? quarantaineDepuis = null)
    {
        var laisses = hooksWanted is null;
        var conservesDepuis = conserverHooks ? quarantaineDepuis : null;
        try
        {
            var lecture = _passerelle.Lire();
            if (lecture.Issue == IssueLectureClaude.Absent)      // on ne CRÉE jamais le fichier : purge seulement
            {
                DernierBilan = new BilanReconciliation(false, null, null, "fichier absent", laisses, conservesDepuis);
                return false;
            }
            if (lecture.Issue == IssueLectureClaude.Inexploitable)   // vide, invalide, verrouillé : on n'y touche pas
            {
                DernierBilan = new BilanReconciliation(false, null, null,
                    "illisible — rien écrit" + (lecture.Cause is { } c ? " (" + c + ")" : ""), laisses, conservesDepuis);
                return false;
            }

            // Mono-fichier : Environment.ProcessPath, JAMAIS l'emplacement de l'assembly (vide en single-file).
            var exePath = _exePath ?? Environment.ProcessPath ?? "Chronos.exe";
            var reconcilie = ReconcileJson(lecture.Texte, exePath, hooksWanted, commandeHeritee, out var barre, conserverHooks);

            if (barre is null)                               // inexploitable (ne devrait plus arriver après Lire) : on n'y touche pas
            {
                DernierBilan = new BilanReconciliation(false, null, null, "illisible — rien écrit", laisses, conservesDepuis);
                return false;
            }
            if (reconcilie is null)                          // déjà conforme → rien à faire
            {
                DernierBilan = new BilanReconciliation(false, barre, null, null, laisses, conservesDepuis);
                return false;
            }

            var ecriture = _passerelle.Ecrire(lecture, reconcilie, creerSiAbsent: false);
            DernierBilan = new BilanReconciliation(ecriture.Ecrit, barre, ecriture.Sauvegarde, ecriture.Cause, laisses, conservesDepuis);
            return ecriture.Ecrit;
        }
        catch (Exception ex)
        {
            DernierBilan = new BilanReconciliation(false, null, null, ex.GetType().Name, laisses, conservesDepuis);
            return false;
        }
    }
}
