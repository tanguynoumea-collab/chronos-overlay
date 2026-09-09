using System.IO;
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
/// <para><b>MUTATION CIBLÉE, jamais reconstruction</b> (PUR-02) : seule la valeur <c>command</c> est
/// réécrite. Reconstruire l'objet <c>statusLine</c> effaçait sans bruit <c>padding</c> — champ
/// officiel et optionnel — ainsi que toute clé que Claude Code ajouterait à l'avenir.</para>
///
/// <para>Les autres réglages de settings.json sont PRÉSERVÉS (édition par JsonNode, pas de réécriture
/// totale). Un settings.json INEXPLOITABLE fait abandonner l'écriture (les cœurs purs renvoient
/// <c>null</c>) : on ne repart JAMAIS d'un objet vide, ce qui effacerait tout le fichier.</para>
///
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

    /// <summary>
    /// La commande statusLine pointe-t-elle déjà sur CE Chronos.exe ? Le prédicat exige désormais
    /// APPARTENANCE (une commande Chronos) <b>et</b> FRAÎCHEUR (ce chemin d'exe).
    ///
    /// <para>Pourquoi ce durcissement : l'ancien prédicat acceptait n'importe quelle commande
    /// contenant « Chronos », donc <c>IsEnabled()</c> répondait « oui » pour un exe périmé,
    /// <c>OfferOnFirstRun</c> sortait immédiatement et le pont restait branché à vie sur
    /// <c>Chronos-v2.8.1.exe</c> dans <c>Downloads</c> — un exe qui n'existait peut-être même plus.</para>
    ///
    /// <para>Le réconciliateur du plan 03 s'exécute AVANT <c>OfferOnFirstRun()</c> dans
    /// <c>App.OnStartup</c> : après repointage, <c>IsInstalled</c> redevient vrai et aucun dialogue
    /// parasite n'apparaît.</para>
    /// </summary>
    public bool IsInstalled(string exePath)
    {
        try
        {
            if (!File.Exists(_claudeSettingsPath)) return false;
            var root = ClaudeSettingsJson.ParseOrNull(File.ReadAllText(_claudeSettingsPath));
            var cmd = root?["statusLine"] is JsonObject sl && sl["command"] is JsonValue v
                && v.TryGetValue<string>(out var s) ? s : null;
            return ClaudeSettingsJson.IsChronosCommand(cmd, ClaudeSettingsJson.StatusLineMarker)
                && ClaudeSettingsJson.PointsToExe(cmd, exePath);
        }
        catch { return false; }
    }

    /// <summary>
    /// Installe le pont. Renvoie la commande statusLine préexistante à MÉMORISER pour le chaînage
    /// (null si aucune, si déjà Chronos, ou si le fichier est inexploitable). L'appelant persiste
    /// cette valeur dans <see cref="ChronosSettings.InnerStatusLineCommand"/>.
    /// </summary>
    public string? Install(string exePath)
    {
        var current = File.Exists(_claudeSettingsPath) ? File.ReadAllText(_claudeSettingsPath) : null;
        var updated = TransformForInstall(current, exePath, out var capturedInner);
        if (updated is null) return null;   // fichier inexploitable → on n'écrit RIEN
        WriteAtomic(updated);
        return capturedInner;
    }

    /// <summary>
    /// Désinstalle : restaure <paramref name="innerCommand"/> (ou retire statusLine si null). Ne prend
    /// volontairement PLUS de <c>exePath</c> : le retrait est LARGE (toute barre Chronos, quel que soit
    /// le chemin d'exe enregistré), sinon désactiver depuis une nouvelle version laisserait la barre
    /// d'une ancienne en place.
    /// </summary>
    public void Uninstall(string? innerCommand)
    {
        if (!File.Exists(_claudeSettingsPath)) return;
        var updated = TransformForUninstall(File.ReadAllText(_claudeSettingsPath), innerCommand);
        if (updated is null) return;
        WriteAtomic(updated);
    }

    // --- Cœurs PURS testables (aucune E/S) ---

    /// <summary>
    /// REPOINTE la barre statusLine sur <paramref name="exePath"/> — et RIEN d'autre. Ne crée jamais
    /// l'entrée : le consentement d'installation reste porté par le menu / StatusLinePromptDismissed.
    /// Mutation CIBLÉE de « command » : « type », « padding » (champ officiel optionnel) et toute clé
    /// future de l'objet survivent — les reconstruire les effacerait sans bruit.
    /// </summary>
    public static void ApplyStatusLine(JsonObject root, string exePath)
    {
        if (root["statusLine"] is not JsonObject sl) return;
        var cmd = sl["command"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        if (!ClaudeSettingsJson.IsChronosCommand(cmd, ClaudeSettingsJson.StatusLineMarker)) return;  // barre tierce → intouchée
        if (ClaudeSettingsJson.PointsToExe(cmd, exePath)) return;                                    // déjà à jour
        sl["command"] = ChronosCommand(exePath);
    }

    /// <summary>Transforme le JSON de settings pour pointer statusLine sur Chronos, en préservant tout
    /// le reste. <paramref name="capturedInner"/> = commande préexistante à chaîner (null si aucune ou
    /// déjà Chronos). Renvoie <c>null</c> — « NE RIEN ÉCRIRE » — si le JSON est inexploitable.</summary>
    public static string? TransformForInstall(string? settingsJson, string exePath, out string? capturedInner)
    {
        capturedInner = null;
        var root = ClaudeSettingsJson.ParseOrNull(settingsJson);
        if (root is null) return null;   // JAMAIS new JsonObject() : cela EFFACERAIT tout le fichier

        if (root["statusLine"] is JsonObject sl)
        {
            var existing = sl["command"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
            if (!string.IsNullOrWhiteSpace(existing)
                && !ClaudeSettingsJson.IsChronosCommand(existing, ClaudeSettingsJson.StatusLineMarker))
                capturedInner = existing;      // barre tierce → mémorisée pour le chaînage

            sl["command"] = ChronosCommand(exePath);   // MUTATION : type, padding, clés futures intacts
            if (sl["type"] is null) sl["type"] = "command";
        }
        else
        {
            root["statusLine"] = new JsonObject { ["type"] = "command", ["command"] = ChronosCommand(exePath) };
        }
        return ClaudeSettingsJson.Serialize(root);
    }

    /// <summary>Restaure la barre d'origine (ou retire statusLine) SEULEMENT si elle appartient à
    /// Chronos — quel que soit le chemin d'exe enregistré. Renvoie <c>null</c> si le JSON est
    /// inexploitable.</summary>
    public static string? TransformForUninstall(string? settingsJson, string? innerCommand)
    {
        var root = ClaudeSettingsJson.ParseOrNull(settingsJson);
        if (root is null) return null;
        if (root["statusLine"] is JsonObject sl)
        {
            var cmd = sl["command"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
            if (ClaudeSettingsJson.IsChronosCommand(cmd, ClaudeSettingsJson.StatusLineMarker))
            {
                if (!string.IsNullOrWhiteSpace(innerCommand)) sl["command"] = innerCommand;  // mutation, pas reconstruction
                else root.Remove("statusLine");
            }
        }
        return ClaudeSettingsJson.Serialize(root);
    }

    /// <summary>Commande statusLine attendue pour ce Chronos.exe (slashes AVANT, comme HookCommand :
    /// des backslashes seraient avalés par le shell — leçon vérifiée sur la vraie machine).</summary>
    public static string ChronosCommand(string exePath) => "\"" + exePath.Replace('\\', '/') + "\" --statusline";

    private void WriteAtomic(string content)
    {
        var dir = Path.GetDirectoryName(_claudeSettingsPath)!;
        Directory.CreateDirectory(dir);
        var tmp = _claudeSettingsPath + ".tmp-" + Environment.ProcessId;
        File.WriteAllText(tmp, content);
        File.Move(tmp, _claudeSettingsPath, overwrite: true);
    }
}
