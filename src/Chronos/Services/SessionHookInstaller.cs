using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Chronos.Services;

/// <summary>
/// Installe/retire les hooks de suivi de sessions dans le settings.json de Claude Code
/// (%USERPROFILE%\.claude\settings.json). Chaque hook appelle « Chronos.exe --hook &lt;Event&gt; » qui
/// écrit un fichier d'état par session.
///
/// <para><b>REMPLACE inconditionnellement les groupes Chronos</b> (quel que soit le chemin d'exe :
/// PUR-01) et <b>ne touche jamais aux groupes des autres outils</b> — leur ordre, leur « matcher »,
/// leur « timeout » et leurs champs inconnus sont préservés. L'ancien garde d'idempotence comparait
/// au chemin de l'exe COURANT : depuis un exe fraîchement téléchargé, aucune entrée existante ne
/// correspondait, donc un groupe s'ajoutait à chaque version — 5 chemins d'exe = 25 groupes au lieu
/// de 5. Le repérage passe désormais par <see cref="ClaudeSettingsJson.IsChronosGroup"/> (marqueur
/// d'argument + nom de fichier Chronos*.exe), jamais par le chemin.</para>
///
/// <para>RÉVERSIBLE, et — leçon vérifiée sur la vraie machine — le chemin de l'exe est en SLASHES
/// AVANT (des backslashes seraient avalés par le shell).</para>
///
/// <para>Un settings.json INEXPLOITABLE (clés dupliquées, racine non-objet, JSON invalide) fait
/// abandonner l'écriture : les cœurs purs renvoient <c>null</c> et les méthodes d'E/S ne touchent
/// pas au fichier. On ne repart JAMAIS d'un objet vide, ce qui effacerait tout le fichier.</para>
///
/// Rappel prouvé : la config des hooks est lue au DÉMARRAGE d'une session → seules les sessions Claude
/// Code lancées APRÈS l'installation seront suivies (comme pour statusLine).
/// </summary>
/// <summary>Un événement CÂBLÉ par Chronos : son nom, le matcher qui le filtre (<c>null</c> = tout), et ce
/// qu'il produit. Le rôle n'est pas décoratif : c'est lui qu'on recopie dans docs/ (EVT-05).</summary>
/// <param name="Evenement">Le nom exact, validé contre <see cref="CatalogueEvenementsHooks"/> avant écriture.</param>
/// <param name="Matcher">Le filtre PRIMAIRE, ou <c>null</c> quand on veut tout.</param>
/// <param name="Role">Ce que l'entrée produit — recopié tel quel dans docs/hooks-contract.md (EVT-05).</param>
/// <param name="Timeout">
/// Le délai de grâce, en secondes, accordé au processus de hook. Il CESSE d'être uniforme depuis EVT-03,
/// et ce n'est pas un réglage cosmétique : <c>PreToolUse</c> est BLOQUANT — Claude Code attend la fin du
/// hook avant de lancer l'outil. Dix secondes de gel sur CHAQUE appel d'outil coûteraient infiniment plus
/// cher que le battement ne rapporte, alors que l'écriture d'un fichier d'état se compte en
/// millisecondes. Les deux battements portent donc trois secondes, les six entrées de cycle de vie —
/// rares, et jamais dans le chemin critique d'un outil — gardent dix.
/// </param>
public sealed record EvenementCable(string Evenement, string? Matcher, string Role, int Timeout = 10);

public sealed class SessionHookInstaller
{
    /// <summary>
    /// LE CÂBLAGE — source de vérité unique de ce que Chronos installe.
    ///
    /// <para><b>Pourquoi <c>Notification</c> porte un matcher.</b> Ce n'est pas une alerte d'absence mais
    /// un BUS généraliste : le relevé du 2026-09-12 en dénombre douze types (<c>permission_prompt</c>,
    /// <c>idle_prompt</c>, <c>auth_success</c>, quatre <c>elicitation_*</c>, <c>agent_needs_input</c>,
    /// <c>agent_completed</c>, trois <c>quota_auto_resume_*</c>). Chronos les réduisait TOUS à un seul
    /// état : une authentification réussie et une reprise de quota fabriquaient donc une attente. Seuls
    /// les TROIS types qui sont de véritables DEMANDES passent désormais le filtre.</para>
    ///
    /// <para><c>permission_prompt</c> est volontairement ABSENT du matcher : <c>PermissionRequest</c> est
    /// le signal dédié, exact et immédiat, et deux chemins pour un même fait rendraient la source
    /// illisible. <c>idle_prompt</c> est une alerte d'ABSENCE : au mieux un indice sur l'utilisateur,
    /// jamais une vérité sur ce que fait la session.</para>
    ///
    /// <para><b>Pourquoi ce matcher-là est sûr.</b> Il ne contient que des lettres, des tirets bas et des
    /// barres verticales : il est donc évalué comme une LISTE EXACTE, et non comme une expression
    /// régulière NON ANCRÉE. C'est exactement le piège documenté (<c>Edit.*</c> attrape aussi
    /// <c>NotebookEdit</c>) — on n'y entre pas.</para>
    ///
    /// <para><b>Pourquoi les BATTEMENTS DE CŒUR sont ces deux-là (EVT-03).</b> Le catalogue relevé le
    /// 2026-09-12 ne contient AUCUN battement périodique : il n'y a pas de tick horloge à câbler. Ces
    /// deux entrées sont donc une preuve de vie IMPARFAITE, et l'imperfection est assumée — entre le
    /// signal d'entrée d'un build de dix minutes et son signal de sortie, rien n'arrive ; idem pendant
    /// une longue réflexion sans appel d'outil. La limite est écrite dans docs/ (EVT-05), pas tue.</para>
    ///
    /// <para><c>MessageDisplay</c> serait un battement bien plus FIN, mais il se déclenche pendant le
    /// streaming du texte affiché : un processus Chronos par fragment. Il est écarté pour son COÛT, pas
    /// pour sa précision — un battement ne doit pas coûter plus cher que ce qu'il observe.</para>
    ///
    /// <para>Leur <c>matcher</c> est volontairement ABSENT : on veut TOUT outil, et un matcher omis vaut
    /// « tout ». Enfin, ces deux clés hébergent sur cette machine des groupes appartenant à un AUTRE
    /// OUTIL. La purge de Chronos repère les siens par marqueur d'argument et nom de fichier
    /// <c>Chronos*.exe</c>, JAMAIS par la clé : les groupes tiers ne sont ni touchés, ni comptés comme
    /// nôtres, et un test le prouve.</para>
    /// </summary>
    public static readonly EvenementCable[] Cablage =
    {
        new("SessionStart",      null, "la session démarre → en cours"),
        new("UserPromptSubmit",  null, "un message est envoyé → en cours"),
        new("Stop",              null, "le tour se termine → tour fini"),
        new("SessionEnd",        null, "fin de session → le fichier d'état est supprimé"),
        new("PermissionRequest", null, "une permission est DEMANDÉE → à toi (EVT-01)"),
        new("Notification",      "agent_needs_input|elicitation_dialog|elicitation_url_dialog",
                                 "les TROIS types du bus qui sont de vraies demandes → à toi (EVT-02)"),
        new("PreToolUse",        null, "un outil va être appelé → battement de cœur : en cours (EVT-03)", 3),
        new("PostToolUse",       null, "un outil vient de réussir → battement de cœur : en cours (EVT-03)", 3),
    };

    /// <summary>Les noms des événements câblés. CONSERVÉ (le diagnostic et plusieurs tests le lisent)
    /// mais DÉRIVÉ du câblage : une seule source de vérité, jamais deux listes à tenir d'accord.</summary>
    public static readonly string[] Events = Cablage.Select(c => c.Evenement).ToArray();

    private readonly string _settingsPath;

    public SessionHookInstaller(string? settingsPath = null)
        => _settingsPath = settingsPath ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".claude", "settings.json");

    public string SettingsPath => _settingsPath;

    /// <summary>Commande de hook pour un événement (exe en slashes avant, impératif sous shell).</summary>
    public static string HookCommand(string exePath, string ev)
        => "\"" + exePath.Replace('\\', '/') + "\" --hook " + ev;

    /// <summary>
    /// Les hooks pointent-ils sur CET exe ? Question de FRAÎCHEUR, pas d'appartenance : un groupe
    /// Chronos d'une version précédente répond <c>false</c>, ce qui permet à l'appelant de repointer.
    /// </summary>
    public bool IsInstalled(string exePath)
    {
        try
        {
            if (!File.Exists(_settingsPath)) return false;
            var root = ClaudeSettingsJson.ParseOrNull(File.ReadAllText(_settingsPath));
            if (root?["hooks"]?["Notification"] is not JsonArray arr) return false;
            return arr.Any(g => g is JsonObject go && go["hooks"] is JsonArray hs
                && hs.Any(h => ClaudeSettingsJson.CommandOf(h) is { } c
                    && ClaudeSettingsJson.IsChronosCommand(c, ClaudeSettingsJson.HookMarker)
                    && ClaudeSettingsJson.PointsToExe(c, exePath)));
        }
        catch { return false; }
    }

    public void Install(string exePath)
    {
        var current = File.Exists(_settingsPath) ? File.ReadAllText(_settingsPath) : null;
        var updated = TransformForInstall(current, exePath);
        if (updated is null) return;        // fichier inexploitable → on n'écrit RIEN (dégradation silencieuse)
        WriteAtomic(updated);
    }

    public void Uninstall()
    {
        if (!File.Exists(_settingsPath)) return;
        var updated = TransformForUninstall(File.ReadAllText(_settingsPath));
        if (updated is null) return;
        WriteAtomic(updated);
    }

    // --- Cœurs PURS testables ---

    /// <summary>
    /// Amène l'arbre <paramref name="root"/> à l'ÉTAT DÉSIRÉ du <see cref="Cablage"/> de Chronos :
    /// retirer-puis-ajouter INCONDITIONNEL. Idempotent par construction — il ne dépend d'aucun prédicat
    /// de présence exact, donc il ne peut plus empiler (cause du cumul de 25 groupes).
    /// <paramref name="wanted"/> == false ⇒ ZÉRO groupe Chronos (désinstallation / purge).
    /// Les groupes non-Chronos ne sont jamais touchés : leur ordre, leur « matcher », leur « timeout »
    /// et leurs champs inconnus sont préservés (mutation en place, aucun reparentage de JsonNode).
    /// </summary>
    public static void ApplyHooks(JsonObject root, string exePath, bool wanted)
        => ApplyHooks(root, exePath, wanted, Cablage);

    /// <summary>
    /// Surcharge à câblage INJECTÉ : elle existe pour que la validation (liste blanche, support du
    /// matcher) soit prouvable sans jamais écrire un nom douteux dans le câblage réel.
    ///
    /// <para><b>La purge est LARGE, l'installation reste CIBLÉE.</b> Jusqu'ici seuls les événements
    /// câblés étaient parcourus : un groupe Chronos posé sur un événement qu'on cesse de câbler aurait
    /// survécu pour toujours, invisible. On balaie donc TOUTES les clés de <c>hooks</c>. Deux prudences
    /// envers les autres outils : une clé dont la valeur n'est PAS un tableau est ignorée sans être
    /// écrite, et une clé dont le tableau était DÉJÀ vide à l'entrée n'est jamais retirée — seules les
    /// clés dont on a effectivement retiré un groupe à nous peuvent disparaître.</para>
    ///
    /// <para><b>La validation précède toute mutation.</b> Un nom absent de
    /// <see cref="CatalogueEvenementsHooks"/> produirait un hook MORT et MUET (le sort d'un nom inconnu
    /// n'est pas documenté). Un matcher posé sur un événement qui n'en accepte pas est silencieusement
    /// ignoré : le groupe ne ferait alors pas ce qu'il annonce. Dans les deux cas, on n'écrit RIEN pour
    /// cette entrée — une configuration morte ne s'installe pas.</para>
    /// </summary>
    public static void ApplyHooks(JsonObject root, string exePath, bool wanted,
                                  IReadOnlyList<EvenementCable> cablage)
    {
        if (root["hooks"] is not JsonObject hooks)
        {
            if (!wanted) return;                 // rien à purger, et on n'installe pas une clé « hooks » vide
            hooks = new JsonObject();
            root["hooks"] = hooks;
        }

        // 1) PURGE LARGE. On fige d'abord les clés : on ne peut pas retirer une clé d'un JsonObject
        //    pendant qu'on l'énumère.
        var purgees = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cle in hooks.Select(kv => kv.Key).ToList())
        {
            if (hooks[cle] is not JsonArray arr) continue;   // valeur non-tableau (tiers mal formé) : intouchée

            // Parcours DESCENDANT : les indices restants restent valides, aucun reparentage.
            for (int i = arr.Count - 1; i >= 0; i--)
                if (ClaudeSettingsJson.IsChronosGroup(arr[i], ClaudeSettingsJson.HookMarker))
                {
                    arr.RemoveAt(i);
                    purgees.Add(cle);
                }
        }

        // 2) INSTALLATION CIBLÉE, validée AVANT toute mutation.
        if (wanted)
            foreach (var c in cablage)
            {
                if (!CatalogueEvenementsHooks.EstConnu(c.Evenement)) continue;
                if (c.Matcher is not null && !CatalogueEvenementsHooks.AccepteUnMatcher(c.Evenement)) continue;

                if (hooks[c.Evenement] is not JsonArray arr)
                {
                    arr = new JsonArray();
                    hooks[c.Evenement] = arr;    // réassigner la MÊME instance ne lève pas
                }

                var groupe = new JsonObject();
                if (c.Matcher is not null) groupe["matcher"] = c.Matcher;   // clé ABSENTE quand il n'y en a pas
                groupe["hooks"] = new JsonArray(new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = HookCommand(exePath, c.Evenement),   // slashes avant : leçon terrain, conservée
                    ["timeout"] = c.Timeout,   // A4 : court pour les battements, PreToolUse étant BLOQUANT
                });
                arr.Add(groupe);
            }

        // 3) Les clés QU'ON A PURGÉES et devenues vides disparaissent ; celles des autres, jamais.
        foreach (var cle in purgees)
            if (hooks[cle] is JsonArray vide && vide.Count == 0) hooks.Remove(cle);

        if (hooks.Count == 0) root.Remove("hooks");
    }

    /// <summary>
    /// Pose les hooks du <see cref="Cablage"/> pour CET exe. Renvoie <c>null</c> — « NE RIEN ÉCRIRE » —
    /// si le JSON fourni est inexploitable.
    /// </summary>
    public static string? TransformForInstall(string? settingsJson, string exePath)
    {
        var root = ClaudeSettingsJson.ParseOrNull(settingsJson);
        if (root is null) return null;   // JAMAIS new JsonObject() : cela EFFACERAIT tout le fichier
        ApplyHooks(root, exePath, wanted: true);
        return ClaudeSettingsJson.Serialize(root);
    }

    /// <summary>
    /// Retire TOUS les groupes de hooks Chronos. Cette méthode ne prend volontairement PLUS de
    /// <c>exePath</c> : la purge est LARGE (tous les Chronos, quel que soit le chemin), sinon
    /// <c>Disable()</c> depuis une nouvelle version laisserait derrière lui les hooks de toutes les
    /// anciennes. Renvoie <c>null</c> si le JSON fourni est inexploitable.
    /// </summary>
    public static string? TransformForUninstall(string? settingsJson)
    {
        var root = ClaudeSettingsJson.ParseOrNull(settingsJson);
        if (root is null) return null;
        ApplyHooks(root, exePath: "", wanted: false);
        return ClaudeSettingsJson.Serialize(root);
    }

    private void WriteAtomic(string content)
    {
        var dir = Path.GetDirectoryName(_settingsPath)!;
        System.IO.Directory.CreateDirectory(dir);
        var tmp = _settingsPath + ".tmp-" + System.Environment.ProcessId;
        File.WriteAllText(tmp, content);
        File.Move(tmp, _settingsPath, overwrite: true);
    }
}
