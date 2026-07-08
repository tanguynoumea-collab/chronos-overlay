using System.Globalization;
using System.IO;
using System.Text.Json;
using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Provider de REPLI (source Estimee) : estime l'usage par SOMME de tokens des transcripts JSONL
/// (%USERPROFILE%\.claude\projects\**\*.jsonl), en streaming tolerant, marque
/// <see cref="SourceReliability.Estimated"/> (DAT-05).
///
/// Estimation HONNETE : la somme de tokens est EXACTE (comptage reel), mais le % de quota est
/// INCONNU (plafonds non publies, mouvants) -> toujours Estimated, Utilization/ResetsAt/
/// FractionTimeRemaining = null (jamais de valeur inventee — Core Value).
///
/// Parsing TOLERANT (ROB-02) : ligne corrompue, derniere ligne partielle, champ manquant, ligne
/// non-assistant et prose "five_hour" sont ignores ; dossier absent -> sequence vide. Jamais
/// d'exception qui remonte.
/// </summary>
public sealed class JsonlEstimationProvider : IUsageProvider
{
    private readonly ChronosPaths _paths;
    private readonly IClock _clock;

    public JsonlEstimationProvider(ChronosPaths paths, IClock clock)
    {
        _paths = paths;
        _clock = clock;
    }

    /// <summary>Emis en fin de GetAsync (le declenchement par watcher arrive en Phase 4).</summary>
    public event EventHandler<UsageSnapshot>? SnapshotChanged;

    public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        long five = 0, week = 0;

        foreach (var file in EnumerateJsonl(_paths.ProjectsRoot))
        {
            FileStream? fs = null;
            // FileShare.ReadWrite : Claude Code ecrit le transcript en parallele.
            try { fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); }
            catch (IOException) { continue; }
            await using (fs)
            using (var reader = new StreamReader(fs))
            {
                string? line;
                while ((line = await reader.ReadLineAsync(ct)) is not null)
                {
                    if (line.Length == 0) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);        // ligne partielle/corrompue -> JsonException
                        var o = doc.RootElement;
                        if (!IsAssistant(o)) continue;                    // type==assistant ET message.role==assistant
                        if (!o.TryGetProperty("timestamp", out var ts)) continue;
                        if (!DateTimeOffset.TryParse(ts.GetString(), CultureInfo.InvariantCulture,
                                DateTimeStyles.RoundtripKind, out var when)) continue;

                        long tokens = SumUsageTokens(o);                  // input+output+cache_creation+cache_read
                        if (when >= now - TimeSpan.FromHours(5)) five += tokens;
                        if (when >= now - TimeSpan.FromDays(7)) week += tokens;
                    }
                    catch (JsonException) { /* ligne invalide ignoree (ROB-02) */ }
                }
            }
        }

        var snap = new UsageSnapshot
        {
            FiveHour = EstimatedWindow(WindowKind.FiveHour, five),
            SevenDay = EstimatedWindow(WindowKind.SevenDay, week),
            SourceCapturedAt = now,
            Age = TimeSpan.Zero,
        };
        SnapshotChanged?.Invoke(this, snap);
        return snap;
    }

    // Enumere les *.jsonl sous root. Dossier absent / inaccessible -> sequence vide (jamais d'exception).
    private static IEnumerable<string> EnumerateJsonl(string root)
    {
        if (!Directory.Exists(root)) return Array.Empty<string>();
        try
        {
            // Recursif total : inclut INTENTIONNELLEMENT le sous-dossier subagents/ — les sous-agents
            // consomment le MEME pool de quota de compte, donc leurs tokens comptent dans la somme
            // (arbitrage phase 3). Exploitation STRUCTUREE des sous-agents = differee V2-01. AUCUN
            // filtre d'exclusion n'est pose ici.
            return Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories);
        }
        catch (IOException) { return Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    // Objet structure uniquement : type=="assistant" ET message.role=="assistant". Ne matche JAMAIS
    // une chaine "five_hour"/"seven_day" dans un champ content (faux positifs de prose evites).
    private static bool IsAssistant(JsonElement o)
    {
        if (!o.TryGetProperty("type", out var t) || t.ValueKind != JsonValueKind.String
            || t.GetString() != "assistant")
            return false;
        if (!o.TryGetProperty("message", out var m) || m.ValueKind != JsonValueKind.Object)
            return false;
        return m.TryGetProperty("role", out var r) && r.ValueKind == JsonValueKind.String
            && r.GetString() == "assistant";
    }

    // Somme message.usage : input + output + cache_creation + cache_read. Chaque champ optionnel (defaut 0).
    private static long SumUsageTokens(JsonElement o)
    {
        if (!o.TryGetProperty("message", out var m) || m.ValueKind != JsonValueKind.Object
            || !m.TryGetProperty("usage", out var u) || u.ValueKind != JsonValueKind.Object)
            return 0;

        return Field(u, "input_tokens") + Field(u, "output_tokens")
             + Field(u, "cache_creation_input_tokens") + Field(u, "cache_read_input_tokens");
    }

    private static long Field(JsonElement usage, string name)
        => usage.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
           && v.TryGetInt64(out var n) ? n : 0;

    // Fenetre estimee : somme brute exacte, mais % de quota inconnu -> Utilization/ResetsAt/Fraction null.
    private static WindowState EstimatedWindow(WindowKind kind, long total) => new()
    {
        Kind = kind,
        EstimatedTokens = total,
        Utilization = null,
        ResetsAt = null,
        FractionTimeRemaining = null,
        Reliability = SourceReliability.Estimated,
    };
}
