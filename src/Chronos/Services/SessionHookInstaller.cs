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
public sealed class SessionHookInstaller
{
    // Les 5 événements qui portent l'état « attente vs travail » (SubagentStop/PostToolUse exclus).
    public static readonly string[] Events = { "Notification", "Stop", "UserPromptSubmit", "SessionStart", "SessionEnd" };

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
    /// Amène l'arbre <paramref name="root"/> à l'ÉTAT DÉSIRÉ pour les 5 événements de Chronos :
    /// retirer-puis-ajouter INCONDITIONNEL. Idempotent par construction — il ne dépend d'aucun prédicat
    /// de présence exact, donc il ne peut plus empiler (cause du cumul de 25 groupes).
    /// <paramref name="wanted"/> == false ⇒ ZÉRO groupe Chronos (désinstallation / purge).
    /// Les groupes non-Chronos ne sont jamais touchés : leur ordre, leur « matcher », leur « timeout »
    /// et leurs champs inconnus sont préservés (mutation en place, aucun reparentage de JsonNode).
    /// </summary>
    public static void ApplyHooks(JsonObject root, string exePath, bool wanted)
    {
        if (root["hooks"] is not JsonObject hooks)
        {
            if (!wanted) return;                 // rien à purger, et on n'installe pas une clé « hooks » vide
            hooks = new JsonObject();
            root["hooks"] = hooks;
        }

        foreach (var ev in Events)
        {
            if (hooks[ev] is not JsonArray arr)
            {
                if (!wanted) continue;
                arr = new JsonArray();
                hooks[ev] = arr;                 // réassigner la MÊME instance ne lève pas
            }

            // Parcours DESCENDANT : les indices restants restent valides, aucun reparentage.
            for (int i = arr.Count - 1; i >= 0; i--)
                if (ClaudeSettingsJson.IsChronosGroup(arr[i], ClaudeSettingsJson.HookMarker))
                    arr.RemoveAt(i);

            if (wanted)
                arr.Add(new JsonObject
                {
                    ["hooks"] = new JsonArray(new JsonObject
                    {
                        ["type"] = "command",
                        ["command"] = HookCommand(exePath, ev),   // slashes avant : leçon terrain, conservée
                        ["timeout"] = 10,
                    }),
                });

            if (arr.Count == 0) hooks.Remove(ev);   // événement devenu vide → retirer la clé
        }

        if (hooks.Count == 0) root.Remove("hooks");
    }

    /// <summary>
    /// Pose les 5 hooks de CET exe. Renvoie <c>null</c> — « NE RIEN ÉCRIRE » — si le JSON fourni est
    /// inexploitable.
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
