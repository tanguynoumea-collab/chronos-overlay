using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chronos.Services;

/// <summary>
/// Installe/désinstalle le pont statusLine dans le settings.json de Claude Code
/// (%USERPROFILE%\.claude\settings.json). Rend l'intégration UNIVERSELLE et RÉVERSIBLE :
///
///   • Install : mémorise toute commande statusLine préexistante (pour le chaînage non destructif),
///     puis pointe statusLine sur « "&lt;Chronos.exe&gt;" --statusline ».
///   • Uninstall : restaure la commande d'origine (ou retire statusLine si l'utilisateur n'en avait pas).
///
/// Les autres réglages de settings.json sont PRÉSERVÉS (édition par JsonNode, pas de réécriture totale).
/// Écriture atomique. Toute erreur d'E/S est remontée à l'appelant (le menu affiche l'échec) mais ne
/// corrompt jamais le fichier (temp + File.Move).
/// </summary>
public sealed class StatusLineInstaller
{
    private readonly string _claudeSettingsPath;

    public StatusLineInstaller(string? claudeSettingsPath = null)
    {
        _claudeSettingsPath = claudeSettingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");
    }

    /// <summary>Chemin du settings.json ciblé (diagnostic).</summary>
    public string SettingsPath => _claudeSettingsPath;

    /// <summary>La commande statusLine pointe-t-elle déjà sur ce Chronos.exe ?</summary>
    public bool IsInstalled(string exePath)
    {
        try
        {
            if (!File.Exists(_claudeSettingsPath)) return false;
            return IsChronosCommand(ReadCommand(File.ReadAllText(_claudeSettingsPath)), exePath);
        }
        catch { return false; }
    }

    /// <summary>
    /// Installe le pont. Renvoie la commande statusLine préexistante à MÉMORISER pour le chaînage
    /// (null si aucune, ou si déjà installé). L'appelant persiste cette valeur dans
    /// <see cref="ChronosSettings.InnerStatusLineCommand"/>.
    /// </summary>
    public string? Install(string exePath)
    {
        var current = File.Exists(_claudeSettingsPath) ? File.ReadAllText(_claudeSettingsPath) : null;
        var updated = TransformForInstall(current, exePath, out var capturedInner);
        WriteAtomic(updated);
        return capturedInner;
    }

    /// <summary>Désinstalle : restaure <paramref name="innerCommand"/> (ou retire statusLine si null).</summary>
    public void Uninstall(string exePath, string? innerCommand)
    {
        if (!File.Exists(_claudeSettingsPath)) return;
        var updated = TransformForUninstall(File.ReadAllText(_claudeSettingsPath), exePath, innerCommand);
        WriteAtomic(updated);
    }

    // --- Cœurs PURS testables (aucune E/S) ---

    /// <summary>Transforme le JSON de settings pour pointer statusLine sur Chronos, en préservant tout
    /// le reste. <paramref name="capturedInner"/> = commande préexistante à chaîner (null si aucune ou déjà Chronos).</summary>
    public static string TransformForInstall(string? settingsJson, string exePath, out string? capturedInner)
    {
        capturedInner = null;
        var root = ParseObject(settingsJson);

        if (root["statusLine"] is JsonObject sl && sl["command"] is JsonValue cv && cv.TryGetValue<string>(out var existing)
            && !string.IsNullOrWhiteSpace(existing) && !IsChronosCommand(existing, exePath))
        {
            capturedInner = existing; // barre préexistante non-Chronos → à mémoriser pour le chaînage
        }

        root["statusLine"] = new JsonObject
        {
            ["type"] = "command",
            ["command"] = ChronosCommand(exePath),
        };
        return root.ToJsonString(Indented);
    }

    /// <summary>Restaure la barre d'origine (ou retire statusLine) SEULEMENT si elle pointe sur Chronos.</summary>
    public static string TransformForUninstall(string? settingsJson, string exePath, string? innerCommand)
    {
        var root = ParseObject(settingsJson);
        if (root["statusLine"] is JsonObject sl && sl["command"] is JsonValue cv && cv.TryGetValue<string>(out var cmd)
            && IsChronosCommand(cmd, exePath))
        {
            if (!string.IsNullOrWhiteSpace(innerCommand))
                root["statusLine"] = new JsonObject { ["type"] = "command", ["command"] = innerCommand };
            else
                root.Remove("statusLine");
        }
        return root.ToJsonString(Indented);
    }

    /// <summary>Commande statusLine attendue pour ce Chronos.exe.</summary>
    public static string ChronosCommand(string exePath) => "\"" + exePath + "\" --statusline";

    // Reconnaît une commande statusLine « Chronos » (contient --statusline ET référence Chronos/l'exe).
    private static bool IsChronosCommand(string? command, string exePath)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        if (command.IndexOf("--statusline", StringComparison.OrdinalIgnoreCase) < 0) return false;
        return command.IndexOf(exePath, StringComparison.OrdinalIgnoreCase) >= 0
            || command.IndexOf("Chronos", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private static JsonObject ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        try { return JsonNode.Parse(json) as JsonObject ?? new JsonObject(); }
        catch { return new JsonObject(); } // settings.json corrompu → on repart d'un objet propre (non destructif : on réécrit valide)
    }

    private static string? ReadCommand(string json)
    {
        try { return (JsonNode.Parse(json) as JsonObject)?["statusLine"]?["command"]?.GetValue<string>(); }
        catch { return null; }
    }

    private void WriteAtomic(string content)
    {
        var dir = Path.GetDirectoryName(_claudeSettingsPath)!;
        Directory.CreateDirectory(dir);
        var tmp = _claudeSettingsPath + ".tmp-" + Environment.ProcessId;
        File.WriteAllText(tmp, content);
        File.Move(tmp, _claudeSettingsPath, overwrite: true);
    }
}
