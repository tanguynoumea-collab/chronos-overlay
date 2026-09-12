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
                ? UsageNormalization.InstantDepuisEpochMillisecondes(ms) : null;

            var five = ReadWindow(root, "five_hour", WindowKind.FiveHour, TimeSpan.FromHours(5), capturedAt);
            var week = ReadWindow(root, "seven_day", WindowKind.SevenDay, TimeSpan.FromDays(7), capturedAt);

            return new UsageSnapshot
            {
                FiveHour = five,
                SevenDay = week,
                SourceCapturedAt = capturedAt,
            };
        }
        catch (Exception ex) when (ex is IOException or JsonException or FileNotFoundException or DirectoryNotFoundException)
        {
            // Fichier absent / corrompu / verrouille -> indisponible, jamais de crash (ROB-01/ROB-02).
            return UsageSnapshot.Empty;
        }
    }

    // Lit UNE fenetre de facon tolerante : fenetre absente/non-objet -> Unavailable ;
    // used_percentage en 0..100 et resets_at en epoch SECONDES : conversions deleguees a UsageNormalization (HDR-05) ; champ manquant -> null.
    // HDR-05 : plus aucune conversion d'unite locale — tout passe par UsageNormalization.
    // Effet de bord ASSUME : le plancher de sanite du point unique (2020-01-01) transforme le
    // resets_at: 9 REEL de cette machine en inconnu, la ou il produisait une geometrie fausse.
    /// <summary>
    /// EXA-02 — l'horodatage porté par la fenêtre est celui du FICHIER (clé capturedAt, epoch
    /// millisecondes), et JAMAIS l'instant de lecture. Le pont statusLine peut avoir écrit ce fichier
    /// il y a deux mois : lui attribuer l'heure courante le ferait paraître vieux de zéro seconde,
    /// il franchirait la porte de fraîcheur de la doctrine, et le « 10 % » figé de cette machine
    /// reviendrait — avec l'autorité d'une limite d'âge prétendument appliquée. Absent du fichier
    /// ⇒ null (incertifiable), ce qui est strictement plus honnête qu'une date inventée.
    /// </summary>
    private WindowState ReadWindow(JsonElement root, string name, WindowKind kind, TimeSpan len,
                                   DateTimeOffset? capturedAt)
    {
        if (!root.TryGetProperty(name, out var w) || w.ValueKind != JsonValueKind.Object)
            return WindowState.Unavailable(kind);

        double? util = w.TryGetProperty("used_percentage", out var up) && up.TryGetDouble(out var pct)
            ? UsageNormalization.FractionDepuisPourcentage(pct) : null;
        DateTimeOffset? reset = w.TryGetProperty("resets_at", out var ra) && ra.TryGetInt64(out var epoch)
            ? UsageNormalization.InstantDepuisEpochSecondes(epoch) : null;

        return new WindowState
        {
            Kind = kind,
            Utilization = util,
            ResetsAt = reset,
            Reliability = SourceReliability.Exact,
            CapturedAt = capturedAt,
            FractionTimeRemaining = WindowState.FractionRemaining(reset, _clock.UtcNow, len),
            // EXA-06 — le nom du producteur voyage PAR RÉFÉRENCE à travers Best() : posé ici, il arrive
            // intact au ViewModel même à travers les trois composites imbriqués.
            Source = SourceUsage.PontStatusLine,
        };
    }
}
