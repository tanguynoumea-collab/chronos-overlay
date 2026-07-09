using System.IO;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la calibration opportuniste (CAL-02) du <see cref="BudgetAutoCalibrator"/> :
/// <list type="bullet">
/// <item>une fenêtre Exact (util&gt;0) + tokens JSONL &gt; 0 → plafond Auto déduit et persisté ;</item>
/// <item>une saisie Manual n'est JAMAIS écrasée ;</item>
/// <item>INERTE si aucune fenêtre Exact (mode app-bureau) → settings inchangés.</item>
/// </list>
/// <see cref="SettingsService"/> réel sur un dossier temp isolé, <see cref="FakeClock"/>, et un
/// FakeProvider comme source de tokens. L'orchestrateur n'est PAS démarré : seule la source
/// d'abonnement (event) compte, et <c>CalibrateAsync</c> est appelée directement (seam interne).
/// </summary>
public sealed class BudgetAutoCalibratorTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 07, 09, 09, 00, 00, TimeSpan.Zero);

    private readonly string _dir;
    private readonly ChronosPaths _paths;
    private readonly SettingsService _settings;

    public BudgetAutoCalibratorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "chronos-autocal-tests", Path.GetRandomFileName());
        var usage = Path.Combine(_dir, "Chronos", "usage.json");
        var projects = Path.Combine(_dir, "projects");
        _paths = new ChronosPaths(usage, projects);
        _settings = new SettingsService(_paths);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* nettoyage best-effort */ }
    }

    // Fake source de tokens : renvoie un snapshot Estimated preconfigure (EstimatedTokens portes).
    private sealed class FakeProvider : IUsageProvider
    {
        private readonly UsageSnapshot _snap;
        public FakeProvider(UsageSnapshot snap) => _snap = snap;
        public Task<UsageSnapshot> GetAsync(CancellationToken ct = default) => Task.FromResult(_snap);
    }

    private static WindowState Win(WindowKind k, SourceReliability r, double? util = null, long? tokens = null)
        => new() { Kind = k, Reliability = r, Utilization = util, EstimatedTokens = tokens };

    private static UsageSnapshot Snap(WindowState five, WindowState seven)
        => new() { FiveHour = five, SevenDay = seven };

    // Orchestrateur NON démarré (aucun watcher, aucun GetAsync) — sert uniquement de source d'event.
    private RefreshOrchestrator IdleOrchestrator()
        => new(new FakeProvider(UsageSnapshot.Empty), _paths, RefreshOptions.Default);

    private BudgetAutoCalibrator CalibratorWith(IUsageProvider tokenSource)
        => new(IdleOrchestrator(), tokenSource, _settings, new FakeClock(Now));

    // --- CAL-02 : fenêtre Exact util=0.5 + tokens 1 000 000 → plafond Auto 2 000 000 persiste ---

    [Fact]
    public async Task Fenetre_5h_exacte_deduit_et_persiste_un_plafond_Auto()
    {
        var snapExact = Snap(
            Win(WindowKind.FiveHour, SourceReliability.Exact, util: 0.5),
            Win(WindowKind.SevenDay, SourceReliability.Estimated));
        var tokenSource = new FakeProvider(Snap(
            Win(WindowKind.FiveHour, SourceReliability.Estimated, tokens: 1_000_000),
            Win(WindowKind.SevenDay, SourceReliability.Estimated, tokens: 0)));

        using var calibrator = CalibratorWith(tokenSource);
        await calibrator.CalibrateAsync(snapExact);

        var persisted = _settings.Load();
        Assert.Equal(2_000_000L, persisted.FiveHourTokenBudget); // 1 000 000 / 0.5
        Assert.Equal(BudgetSource.Auto, persisted.FiveHourBudgetSource);
        Assert.Equal(Now, persisted.FiveHourBudgetCalibratedAt);
    }

    // --- CAL-02 : une saisie Manual n'est jamais écrasée ---

    [Fact]
    public async Task Plafond_Manual_jamais_ecrase()
    {
        _settings.Save(new ChronosSettings
        {
            FiveHourTokenBudget = 999,
            FiveHourBudgetSource = BudgetSource.Manual,
        });

        var snapExact = Snap(
            Win(WindowKind.FiveHour, SourceReliability.Exact, util: 0.5),
            Win(WindowKind.SevenDay, SourceReliability.Estimated));
        var tokenSource = new FakeProvider(Snap(
            Win(WindowKind.FiveHour, SourceReliability.Estimated, tokens: 1_000_000),
            Win(WindowKind.SevenDay, SourceReliability.Estimated, tokens: 0)));

        using var calibrator = CalibratorWith(tokenSource);
        await calibrator.CalibrateAsync(snapExact);

        var persisted = _settings.Load();
        Assert.Equal(999L, persisted.FiveHourTokenBudget); // saisie manuelle intacte
        Assert.Equal(BudgetSource.Manual, persisted.FiveHourBudgetSource);
    }

    // --- INERTIE : aucune fenêtre Exact → aucune écriture (mode app-bureau) ---

    [Fact]
    public async Task Aucune_fenetre_exacte_reste_inerte()
    {
        var snapEstimated = Snap(
            Win(WindowKind.FiveHour, SourceReliability.Estimated, util: 0.5),
            Win(WindowKind.SevenDay, SourceReliability.Estimated, util: 0.3));
        // tokenSource qui LÈVERAIT si appelé : prouve qu'aucun GetAsync n'a lieu sur le chemin inerte.
        var tokenSource = new ThrowingProvider();

        using var calibrator = CalibratorWith(tokenSource);
        await calibrator.CalibrateAsync(snapEstimated); // ne doit rien faire, ne pas lever

        var persisted = _settings.Load();
        Assert.Null(persisted.FiveHourTokenBudget);
        Assert.Null(persisted.WeeklyTokenBudget);
        Assert.Equal(BudgetSource.None, persisted.FiveHourBudgetSource);
        Assert.Equal(BudgetSource.None, persisted.WeeklyBudgetSource);
    }

    // Source de tokens qui échoue si consultée : preuve que le chemin inerte ne l'appelle jamais.
    private sealed class ThrowingProvider : IUsageProvider
    {
        public Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("Le chemin inerte ne doit jamais lire les tokens.");
    }
}
