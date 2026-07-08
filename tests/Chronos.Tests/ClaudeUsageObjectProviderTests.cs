using System.Runtime.CompilerServices;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve DAT-04 (lecture de l'objet d'usage primaire, mapping used_percentage/100 -> Utilization,
/// resets_at epoch SECONDES -> DateTimeOffset, capturedAt epoch MILLISECONDES -> Age via IClock,
/// Reliability = Exact) et ROB-02 (parsing tolerant : fenetre/champ absent, fichier corrompu/absent
/// -> jamais d'exception, jamais de valeur inventee).
///
/// Les fixtures vivent dans TestData/ a cote de ce fichier ; le chemin est resolu via
/// [CallerFilePath] (compile-time) plutot que par copie MSBuild -> aucun couplage au csproj.
/// Tests PURS (pas de Dispatcher) -> [Fact] classiques, chemins et horloge injectes.
/// </summary>
public class ClaudeUsageObjectProviderTests
{
    // Instant de capture fige des fixtures valides (epoch MILLISECONDES) = 2025-07-08T12:00:00Z.
    private const long CapturedAtMs = 1751976000000L;

    private static string TestDataPath(string file, [CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "TestData", file);

    private static ClaudeUsageObjectProvider ProviderFor(string usageFile, IClock clock)
        => new(new ChronosPaths(UsageFile: usageFile, ProjectsRoot: "N/A"), clock);

    // --- DAT-04 : fichier valide -> mapping complet (utilization, reset epoch s, age) ---

    [Fact]
    public async Task Valide_mappe_utilization_reset_et_age()
    {
        // Horloge = capturedAt + 30 s -> Age attendu = 30 s.
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(CapturedAtMs) + TimeSpan.FromSeconds(30));
        var provider = ProviderFor(TestDataPath("usage-valid.json"), clock);

        var snap = await provider.GetAsync();

        // used_percentage 23.5 -> Utilization 0.235 ; provenance Exact.
        Assert.NotNull(snap.FiveHour.Utilization);
        Assert.Equal(0.235, snap.FiveHour.Utilization!.Value, 9);
        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);

        // resets_at 1738425600 (SECONDES, Pitfall 1) -> 2025-02-01T16:00:00Z.
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1738425600), snap.FiveHour.ResetsAt);
        Assert.Equal(new DateTimeOffset(2025, 02, 01, 16, 00, 00, TimeSpan.Zero), snap.FiveHour.ResetsAt);

        // seven_day : 41.2 -> 0.412.
        Assert.NotNull(snap.SevenDay.Utilization);
        Assert.Equal(0.412, snap.SevenDay.Utilization!.Value, 9);
        Assert.Equal(SourceReliability.Exact, snap.SevenDay.Reliability);

        // Staleness : Age = now - capturedAt = 30 s.
        Assert.Equal(TimeSpan.FromSeconds(30), snap.Age);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(CapturedAtMs), snap.SourceCapturedAt);

        // FractionTimeRemaining calcule (fenetre 5 h) -> non null meme si reset passe (clamp 0).
        Assert.NotNull(snap.FiveHour.FractionTimeRemaining);
    }

    // --- ROB-02 / Pitfall 4 : fenetre presente sans resets_at, autre fenetre absente ---

    [Fact]
    public async Task Partiel_fenetre_sans_reset_et_seven_day_absente()
    {
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(CapturedAtMs));
        var provider = ProviderFor(TestDataPath("usage-partial.json"), clock);

        var snap = await provider.GetAsync();

        // five_hour : utilization connue (0.88), mais resets_at absent -> ResetsAt null, pas de NRE.
        Assert.NotNull(snap.FiveHour.Utilization);
        Assert.Equal(0.88, snap.FiveHour.Utilization!.Value, 9);
        Assert.Null(snap.FiveHour.ResetsAt);
        Assert.Null(snap.FiveHour.FractionTimeRemaining); // reset inconnu -> fraction inconnue.
        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);

        // seven_day absente -> Unavailable.
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
        Assert.Null(snap.SevenDay.Utilization);
    }

    // --- ROB-02 : fichier JSON corrompu -> Empty, aucune exception ---

    [Fact]
    public async Task Corrompu_renvoie_Empty_sans_exception()
    {
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(CapturedAtMs));
        var provider = ProviderFor(TestDataPath("usage-corrupt.json"), clock);

        var snap = await provider.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
    }

    // --- ROB-02 : fichier absent -> Empty, aucune exception ---

    [Fact]
    public async Task Absent_renvoie_Empty_sans_exception()
    {
        var clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(CapturedAtMs));
        var provider = ProviderFor(TestDataPath("fichier-inexistant.json"), clock);

        var snap = await provider.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
        Assert.Null(snap.SourceCapturedAt);
    }
}
