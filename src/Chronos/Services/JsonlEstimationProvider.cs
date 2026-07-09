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
    private readonly SettingsService _settings;

    public JsonlEstimationProvider(ChronosPaths paths, IClock clock, SettingsService settings)
    {
        _paths = paths;
        _clock = clock;
        _settings = settings;
    }

    /// <summary>Emis en fin de GetAsync (le declenchement par watcher arrive en Phase 4).</summary>
    public event EventHandler<UsageSnapshot>? SnapshotChanged;

    public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var settings = _settings.Load();                          // Q4 : plafonds/ancre frais a chaque refresh (calibration Phase 9 sans redemarrage)

        // Une SEULE passe disque : on materialise (timestamp, tokens) par message ; les fenetres
        // (inference 5 h, somme hebdo ancree) sont ensuite calculees EN MEMOIRE (Pattern 2).
        var entries = new List<(DateTimeOffset Ts, long Tokens)>();

        foreach (var file in EnumerateJsonl(_paths.ProjectsRoot, now))
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
                        if (when <= now) entries.Add((when, tokens));     // Pitfall 3 : filtrer les timestamps futurs (horloge decalee)
                    }
                    catch (JsonException) { /* ligne invalide ignoree (ROB-02) */ }
                }
            }
        }

        entries.Sort((a, b) => a.Ts.CompareTo(b.Ts));                     // tri global (fichiers non tries entre eux)
        var tsAsc = entries.Select(e => e.Ts).ToList();
        var start = FiveHourWindowInference.InferWindowStart(tsAsc, now); // debut de la fenetre 5 h courante, ou null si inactive

        var snap = new UsageSnapshot
        {
            FiveHour = BuildFiveHour(entries, start, now, settings.FiveHourTokenBudget),
            SevenDay = BuildSevenDay(entries, settings.WeeklyAnchor, now, settings.WeeklyTokenBudget),
            SourceCapturedAt = now,
            Age = TimeSpan.Zero,
        };
        SnapshotChanged?.Invoke(this, snap);
        return snap;
    }

    // Fenetre 5 h INFEREE (EST-01/02/03) : somme bornee a [start, now], reset = start + 5 h, utilization
    // par plafond (jamais clampee a 1 : >=1 = gris epuise, gere par WindowState.Exhausted). Toujours Estimated.
    private static WindowState BuildFiveHour(
        List<(DateTimeOffset Ts, long Tokens)> entries, DateTimeOffset? start, DateTimeOffset now, long? budget)
    {
        if (start is null)                                        // EST-02 : inactive -> arc plein, rien d'entame
            return new WindowState
            {
                Kind = WindowKind.FiveHour,
                EstimatedTokens = 0,
                Utilization = budget is > 0 ? 0.0 : null,
                ResetsAt = null,
                FractionTimeRemaining = 1.0,
                Reliability = SourceReliability.Estimated,
            };

        var reset = start.Value + FiveHourWindowInference.Window;
        long tokens = entries.Where(e => e.Ts >= start.Value && e.Ts <= now).Sum(e => e.Tokens);
        return new WindowState
        {
            Kind = WindowKind.FiveHour,
            EstimatedTokens = tokens,
            Utilization = budget is > 0 ? Math.Max(0.0, (double)tokens / budget.Value) : null,   // EST-03, pas de clamp haut
            ResetsAt = reset,                                                                     // EST-01
            FractionTimeRemaining = WindowState.FractionRemaining(reset, now, FiveHourWindowInference.Window),
            Reliability = SourceReliability.Estimated,
        };
    }

    // Fenetre hebdo (EST-04) : somme bornee a la fenetre ancree (WeeklyAnchor) sinon 7 j glissants.
    // ResetsAt/Fraction laisses null cote provider : remplis par WeeklyRecalibration cote VM (EST-05).
    private static WindowState BuildSevenDay(
        List<(DateTimeOffset Ts, long Tokens)> entries, DateTimeOffset? anchor, DateTimeOffset now, long? budget)
    {
        var start = WeeklyWindow.CurrentStart(anchor, now);
        long tokens = entries.Where(e => e.Ts >= start && e.Ts <= now).Sum(e => e.Tokens);
        return new WindowState
        {
            Kind = WindowKind.SevenDay,
            EstimatedTokens = tokens,
            Utilization = budget is > 0 ? Math.Max(0.0, (double)tokens / budget.Value) : null,   // EST-04
            ResetsAt = null,                          // EST-05 : rempli par WeeklyRecalibration cote VM
            FractionTimeRemaining = null,             // idem : derive du reset hebdo cote VM
            Reliability = SourceReliability.Estimated,
        };
    }

    // Enumere les *.jsonl sous root. Dossier absent / inaccessible -> sequence vide (jamais d'exception).
    private static IEnumerable<string> EnumerateJsonl(string root, DateTimeOffset now)
    {
        if (!Directory.Exists(root)) return Array.Empty<string>();
        try
        {
            // Recursif total : inclut INTENTIONNELLEMENT le sous-dossier subagents/ — les sous-agents
            // consomment le MEME pool de quota de compte, donc leurs tokens comptent dans la somme
            // (arbitrage phase 3). Exploitation STRUCTUREE des sous-agents = differee V2-01. AUCUN
            // filtre d'exclusion n'est pose ici.
            //
            // Perf : un JSONL est append-only -> son message le plus recent >= LastWriteTime. Un fichier
            // non ecrit depuis > 8 j (fenetre hebdo 7 j + marge) ne peut contenir de message < 7 j, donc
            // ne contribue a aucune fenetre : on l'ignore pour eviter de scanner tout l'historique.
            var cutoff = now - TimeSpan.FromDays(8);
            return Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories)
                .Where(f => RecentEnough(f, cutoff));
        }
        catch (IOException) { return Array.Empty<string>(); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    // mtime tolerant : un fichier illisible/disparu est conserve (on le tentera puis on l'ignorera en lecture).
    private static bool RecentEnough(string file, DateTimeOffset cutoff)
    {
        try { return File.GetLastWriteTimeUtc(file) >= cutoff.UtcDateTime; }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
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
}
