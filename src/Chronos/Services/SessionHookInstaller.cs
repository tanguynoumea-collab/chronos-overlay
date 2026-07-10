using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Chronos.Services;

/// <summary>
/// Installe/retire les hooks de suivi de sessions dans le settings.json de Claude Code
/// (%USERPROFILE%\.claude\settings.json). Chaque hook appelle « Chronos.exe --hook &lt;Event&gt; » qui
/// écrit un fichier d'état par session. NON DESTRUCTIF (ajoute à côté des hooks existants — gsd, etc.),
/// RÉVERSIBLE, et — leçon vérifiée sur la vraie machine — le chemin de l'exe est en SLASHES AVANT
/// (des backslashes seraient avalés par le shell).
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

    public bool IsInstalled(string exePath)
    {
        try
        {
            if (!File.Exists(_settingsPath)) return false;
            var root = JsonNode.Parse(File.ReadAllText(_settingsPath)) as JsonObject;
            return root?["hooks"]?["Notification"] is JsonArray arr && arr.Any(e => IsOurEntry(e, exePath));
        }
        catch { return false; }
    }

    public void Install(string exePath)
    {
        var current = File.Exists(_settingsPath) ? File.ReadAllText(_settingsPath) : null;
        WriteAtomic(TransformForInstall(current, exePath));
    }

    public void Uninstall(string exePath)
    {
        if (!File.Exists(_settingsPath)) return;
        WriteAtomic(TransformForUninstall(File.ReadAllText(_settingsPath), exePath));
    }

    // --- Cœurs PURS testables ---

    public static string TransformForInstall(string? settingsJson, string exePath)
    {
        var root = Parse(settingsJson);
        var hooks = root["hooks"] as JsonObject ?? new JsonObject();
        root["hooks"] = hooks;

        foreach (var ev in Events)
        {
            var arr = hooks[ev] as JsonArray ?? new JsonArray();
            hooks[ev] = arr;
            if (arr.Any(e => IsOurEntry(e, exePath))) continue; // déjà présent → idempotent
            arr.Add(new JsonObject
            {
                ["hooks"] = new JsonArray(new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = HookCommand(exePath, ev),
                    ["timeout"] = 10,
                }),
            });
        }
        return root.ToJsonString(Indented);
    }

    public static string TransformForUninstall(string? settingsJson, string exePath)
    {
        var root = Parse(settingsJson);
        if (root["hooks"] is JsonObject hooks)
        {
            foreach (var ev in Events)
            {
                if (hooks[ev] is not JsonArray arr) continue;
                var kept = arr.Where(e => !IsOurEntry(e, exePath)).Select(e => e?.DeepClone()).ToArray();
                if (kept.Length == 0) hooks.Remove(ev);
                else hooks[ev] = new JsonArray(kept);
            }
        }
        return root.ToJsonString(Indented);
    }

    // Une entrée « nôtre » = un groupe dont une commande contient l'exe (slashes avant) ET « --hook ».
    private static bool IsOurEntry(JsonNode? entry, string exePath)
    {
        if (entry is not JsonObject o || o["hooks"] is not JsonArray hs) return false;
        var needle = exePath.Replace('\\', '/');
        return hs.Any(h => h is JsonObject ho && ho["command"]?.GetValue<string>() is { } c
                           && c.Contains("--hook") && c.Contains(needle, System.StringComparison.OrdinalIgnoreCase));
    }

    private static readonly System.Text.Json.JsonSerializerOptions Indented = new() { WriteIndented = true };

    private static JsonObject Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        try { return JsonNode.Parse(json) as JsonObject ?? new JsonObject(); }
        catch { return new JsonObject(); }
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
