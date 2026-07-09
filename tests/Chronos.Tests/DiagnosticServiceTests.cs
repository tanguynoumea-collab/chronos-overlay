using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Le diagnostic explique l'état réel (token, sources, plafonds, résultat affiché) — et n'expose
/// JAMAIS le token en clair dans le rapport (sécurité).
/// </summary>
public class DiagnosticServiceTests
{
    private static ChronosPaths TempPaths()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChronosDiag_" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        return new ChronosPaths(System.IO.Path.Combine(dir, "usage.json"), System.IO.Path.Combine(dir, "projects"));
    }

    private sealed class StubProvider : IUsageProvider
    {
        private readonly UsageSnapshot _snap;
        public StubProvider(UsageSnapshot snap) => _snap = snap;
        public Task<UsageSnapshot> GetAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(_snap);
    }

    [Fact]
    public async Task Rapport_sans_token_conseille_et_n_expose_jamais_le_token()
    {
        var paths = TempPaths();
        var settings = new SettingsService(paths);
        var reader = new FakeClaudeTokenReader { Token = "SECRET-TOKEN-NE-DOIT-PAS-APPARAITRE" };
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState { Kind = WindowKind.FiveHour, Reliability = SourceReliability.Estimated, Utilization = null },
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        };
        var diag = new DiagnosticService(reader, paths, settings, new StubProvider(snap), new FakeClock(DateTimeOffset.UtcNow));

        var report = await diag.BuildReportAsync();

        Assert.Contains("Diagnostic", report);
        Assert.Contains("Usage exact (OAuth)", report);
        Assert.Contains("Token déchiffré : OUI", report);          // présence signalée…
        Assert.DoesNotContain("SECRET-TOKEN", report);              // …mais JAMAIS la valeur
        Assert.Contains("estimé", report);                          // résultat affiché décrit
    }

    [Fact]
    public async Task Rapport_token_absent_le_signale_clairement()
    {
        var paths = TempPaths();
        var diag = new DiagnosticService(new FakeClaudeTokenReader { Token = null }, paths,
            new SettingsService(paths), new StubProvider(UsageSnapshot.Empty), new FakeClock(DateTimeOffset.UtcNow));

        var report = await diag.BuildReportAsync();

        Assert.Contains("Token déchiffré : NON", report);
        Assert.Contains("Conseil", report);
    }
}
