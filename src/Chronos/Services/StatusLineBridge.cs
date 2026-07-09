using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Pont statusLine INTÉGRÉ à l'exe (mode <c>--statusline</c>) : remplace l'ancien script Node externe.
/// Claude Code POUSSE sur stdin, à chaque rendu de sa barre de statut, un JSON de session qui contient
/// (abonnés Pro/Max, après le 1er échange) le bloc <c>rate_limits.{five_hour,seven_day}</c> avec
/// <c>used_percentage</c> (0–100) et <c>resets_at</c> (epoch secondes) — EXACTEMENT les chiffres de
/// l'endpoint oauth/usage, fournis par Claude Code lui-même. Aucun token, aucun secret manipulé.
///
/// RÔLE : matérialiser ces chiffres dans %APPDATA%\Chronos\usage.json (schéma lu par
/// <see cref="ClaudeUsageObjectProvider"/>) pour que l'overlay les lise via FileSystemWatcher.
///
/// NON DESTRUCTIF : si une commande statusLine préexistait, l'installateur l'a mémorisée
/// (<see cref="ChronosSettings.InnerStatusLineCommand"/>) ; ce pont la RÉ-EXÉCUTE avec le MÊME stdin et
/// RÉ-ÉMET sa sortie intacte → la barre existante de l'utilisateur n'est jamais remplacée.
///
/// SÉCURITÉ : on ne copie QUE deux nombres par fenêtre (used_percentage, resets_at) — jamais le reste
/// du stdin (contenu de conversation, chemins…). Écriture atomique (temp + File.Move). Tout est
/// enveloppé : une erreur ne doit JAMAIS casser la barre de statut de Claude Code.
/// </summary>
public static class StatusLineBridge
{
    private static readonly JsonSerializerOptions Tolerant = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Cœur PUR et testable : transforme le JSON stdin de Claude Code en contenu usage.json.
    /// N'extrait que rate_limits.{five_hour,seven_day}.{used_percentage,resets_at}. Fenêtre absente
    /// ou champ manquant → la fenêtre est omise (le provider la lira « indisponible »). Tolérance
    /// totale : stdin invalide → objet avec seulement capturedAt.
    /// </summary>
    public static string BuildUsageJson(string? stdinJson, long capturedAtMs)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            try
            {
                if (!string.IsNullOrWhiteSpace(stdinJson))
                {
                    using var doc = JsonDocument.Parse(stdinJson!, new JsonDocumentOptions
                    {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true,
                    });
                    if (doc.RootElement.ValueKind == JsonValueKind.Object
                        && doc.RootElement.TryGetProperty("rate_limits", out var rl)
                        && rl.ValueKind == JsonValueKind.Object)
                    {
                        WriteWindow(w, rl, "five_hour");
                        WriteWindow(w, rl, "seven_day");
                    }
                }
            }
            catch (JsonException)
            {
                // stdin illisible → on écrit quand même capturedAt (fenêtres omises → « indisponible »).
            }
            w.WriteNumber("capturedAt", capturedAtMs);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    // Copie UNE fenêtre en ne retenant que les deux champs numériques attendus (rien d'autre du stdin).
    private static void WriteWindow(Utf8JsonWriter w, JsonElement rateLimits, string name)
    {
        if (!rateLimits.TryGetProperty(name, out var win) || win.ValueKind != JsonValueKind.Object)
            return;

        bool hasPct = win.TryGetProperty("used_percentage", out var pct) && pct.ValueKind == JsonValueKind.Number;
        bool hasReset = win.TryGetProperty("resets_at", out var reset) && reset.ValueKind == JsonValueKind.Number;
        if (!hasPct && !hasReset) return; // fenêtre vide → on l'omet plutôt que d'écrire un objet vide

        w.WriteStartObject(name);
        if (hasPct) w.WriteNumber("used_percentage", pct.GetDouble());
        if (hasReset) w.WriteNumber("resets_at", reset.GetInt64());
        w.WriteEndObject();
    }

    /// <summary>
    /// Point d'entrée du mode <c>--statusline</c>. Lit tout stdin, écrit usage.json ATOMIQUEMENT
    /// (avant tout spawn, pour survivre à l'annulation en vol de Claude Code), puis — si une commande
    /// statusLine préexistait — la ré-exécute avec le même stdin et ré-émet sa sortie. Ne lève jamais.
    /// </summary>
    public static void Run(ChronosPaths paths, string? innerCommand, TextReader stdin, TextWriter stdout, long capturedAtMs)
    {
        string input = "";
        try { input = stdin.ReadToEnd(); } catch { /* stdin illisible : on continue */ }

        // 1) Écrire usage.json ATOMIQUEMENT d'abord (best-effort, jamais fatal).
        try
        {
            var dir = Path.GetDirectoryName(paths.UsageFile)!;
            Directory.CreateDirectory(dir);
            var tmp = paths.UsageFile + ".tmp-" + Environment.ProcessId;
            File.WriteAllText(tmp, BuildUsageJson(input, capturedAtMs));
            File.Move(tmp, paths.UsageFile, overwrite: true); // remplacement atomique
        }
        catch { /* droits/disque/APPDATA absent : ne pas casser la barre */ }

        // 2) Chaîner la barre préexistante (le cas échéant) : même stdin, sortie ré-émise telle quelle.
        if (!string.IsNullOrWhiteSpace(innerCommand))
        {
            try
            {
                var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                var psi = new ProcessStartInfo("cmd.exe", "/c " + innerCommand)
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardInputEncoding = utf8,
                    StandardOutputEncoding = utf8,
                };
                using var child = Process.Start(psi)!;
                child.StandardInput.Write(input);
                child.StandardInput.Close();
                var outText = child.StandardOutput.ReadToEnd();
                child.WaitForExit(4000);
                stdout.Write(outText);
                return;
            }
            catch { /* la barre enfant a échoué : on retombe sur la ligne minimale ci-dessous */ }
        }

        // 3) Aucune barre préexistante : émettre une ligne minimale et honnête (jamais vide).
        stdout.Write(MinimalStatusLine(input));
    }

    // Ligne de statut minimale quand l'utilisateur n'avait pas de statusLine : « Chronos ⧗ 5h 23% · 7j 41% ».
    private static string MinimalStatusLine(string stdinJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(BuildUsageJson(stdinJson, 0));
            var root = doc.RootElement;
            string P(string w) => root.TryGetProperty(w, out var o) && o.TryGetProperty("used_percentage", out var p)
                ? p.GetDouble().ToString("0") + "%" : "–";
            return $"Chronos ⧗ 5h {P("five_hour")} · 7j {P("seven_day")}";
        }
        catch { return "Chronos ⧗"; }
    }
}
