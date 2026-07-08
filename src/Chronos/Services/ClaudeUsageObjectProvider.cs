using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Provider PRIMAIRE (source Fiable) : lit %APPDATA%\Chronos\usage.json — le fichier materialise par
/// le pont statusLine (chronos-statusline-bridge.js) — et le mappe en <see cref="UsageSnapshot"/>
/// marque <see cref="SourceReliability.Exact"/> (DAT-04).
///
/// Lecture TOLERANTE (ROB-02) : fichier absent/corrompu -> <see cref="UsageSnapshot.Empty"/> ;
/// fenetre absente -> <see cref="WindowState.Unavailable"/> ; champ manquant -> null. Jamais
/// d'exception qui remonte, jamais de valeur inventee.
/// </summary>
public sealed class ClaudeUsageObjectProvider : IUsageProvider
{
    private readonly ChronosPaths _paths;
    private readonly IClock _clock;

    public ClaudeUsageObjectProvider(ChronosPaths paths, IClock clock)
    {
        _paths = paths;
        _clock = clock;
    }

    /// <summary>Emis en fin de GetAsync reussi (le declenchement par watcher arrive en Phase 4).</summary>
    public event EventHandler<UsageSnapshot>? SnapshotChanged;

    // Options tolerantes : casse insensible, commentaires ignores, virgules trainantes, nombres en chaine.
    private static readonly JsonSerializerOptions Tolerant = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        try
        {
            // FileShare.ReadWrite : le pont Node peut reecrire le fichier en parallele.
            await using var fs = new FileStream(_paths.UsageFile, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite);
            using var doc = await JsonDocument.ParseAsync(fs, cancellationToken: ct);
            var root = doc.RootElement;

            // capturedAt = epoch MILLISECONDES (ecrit par le pont via Date.now()).
            DateTimeOffset? capturedAt = root.TryGetProperty("capturedAt", out var c) && c.TryGetInt64(out var ms)
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;

            var five = ReadWindow(root, "five_hour", WindowKind.FiveHour, TimeSpan.FromHours(5));
            var week = ReadWindow(root, "seven_day", WindowKind.SevenDay, TimeSpan.FromDays(7));

            var snap = new UsageSnapshot
            {
                FiveHour = five,
                SevenDay = week,
                SourceCapturedAt = capturedAt,
                Age = capturedAt is null ? null : _clock.UtcNow - capturedAt,
            };
            SnapshotChanged?.Invoke(this, snap);
            return snap;
        }
        catch (Exception ex) when (ex is IOException or JsonException or FileNotFoundException or DirectoryNotFoundException)
        {
            // Fichier absent / corrompu / verrouille -> indisponible, jamais de crash (ROB-01/ROB-02).
            return UsageSnapshot.Empty;
        }
    }

    // Lit UNE fenetre de facon tolerante : fenetre absente/non-objet -> Unavailable ;
    // used_percentage -> /100 ; resets_at epoch SECONDES -> DateTimeOffset ; champ manquant -> null.
    private WindowState ReadWindow(JsonElement root, string name, WindowKind kind, TimeSpan len)
    {
        if (!root.TryGetProperty(name, out var w) || w.ValueKind != JsonValueKind.Object)
            return WindowState.Unavailable(kind);

        double? util = w.TryGetProperty("used_percentage", out var up) && up.TryGetDouble(out var pct)
            ? pct / 100.0 : null;
        DateTimeOffset? reset = w.TryGetProperty("resets_at", out var ra) && ra.TryGetInt64(out var epoch)
            ? DateTimeOffset.FromUnixTimeSeconds(epoch) : null;

        return new WindowState
        {
            Kind = kind,
            Utilization = util,
            ResetsAt = reset,
            Reliability = SourceReliability.Exact,
            FractionTimeRemaining = WindowState.FractionRemaining(reset, _clock.UtcNow, len),
        };
    }
}
