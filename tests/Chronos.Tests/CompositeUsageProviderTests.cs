using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve DAT-06 : le composite selectionne la MEILLEURE source PAR FENETRE
/// (Exact prioritaire, sinon Estimated, sinon Unavailable). Deux fakes IUsageProvider renvoient
/// des snapshots fabriques. Tests PURS -> [Fact] classiques.
/// </summary>
public class CompositeUsageProviderTests
{
    // Fake provider retournant un snapshot preconfigure.
    private sealed class FakeProvider : IUsageProvider
    {
        private readonly UsageSnapshot _snap;
        public FakeProvider(UsageSnapshot snap) => _snap = snap;
        public Task<UsageSnapshot> GetAsync(CancellationToken ct = default) => Task.FromResult(_snap);
    }

    private static WindowState Win(WindowKind k, SourceReliability r)
        => new() { Kind = k, Reliability = r };

    private static UsageSnapshot Snap(WindowState five, WindowState seven)
        => new() { FiveHour = five, SevenDay = seven };

    // --- Cas 1 : primaire Exact (5 h) + Unavailable (7 j) ; repli Estimated -> 5 h primaire, 7 j repli ---

    [Fact]
    public async Task Prend_primaire_exact_et_repli_estimated_par_fenetre()
    {
        var pFive = Win(WindowKind.FiveHour, SourceReliability.Exact);
        var pSeven = Win(WindowKind.SevenDay, SourceReliability.Unavailable);
        var fFive = Win(WindowKind.FiveHour, SourceReliability.Estimated);
        var fSeven = Win(WindowKind.SevenDay, SourceReliability.Estimated);

        var composite = new CompositeUsageProvider(
            new FakeProvider(Snap(pFive, pSeven)),
            new FakeProvider(Snap(fFive, fSeven)));

        var snap = await composite.GetAsync();

        Assert.Same(pFive, snap.FiveHour);   // primaire Exact conserve
        Assert.Same(fSeven, snap.SevenDay);  // primaire Unavailable -> bascule sur repli Estimated
    }

    // --- Cas 2 : primaire tout Unavailable ; repli tout Estimated -> les deux Estimated (repli) ---

    [Fact]
    public async Task Primaire_indisponible_bascule_entierement_sur_repli()
    {
        var pFive = Win(WindowKind.FiveHour, SourceReliability.Unavailable);
        var pSeven = Win(WindowKind.SevenDay, SourceReliability.Unavailable);
        var fFive = Win(WindowKind.FiveHour, SourceReliability.Estimated);
        var fSeven = Win(WindowKind.SevenDay, SourceReliability.Estimated);

        var composite = new CompositeUsageProvider(
            new FakeProvider(Snap(pFive, pSeven)),
            new FakeProvider(Snap(fFive, fSeven)));

        var snap = await composite.GetAsync();

        Assert.Same(fFive, snap.FiveHour);
        Assert.Same(fSeven, snap.SevenDay);
    }

    // --- Cas 3 : primaire tout Exact ; repli tout Estimated -> primaire prioritaire ---

    [Fact]
    public async Task Primaire_exact_prioritaire_sur_repli()
    {
        var pFive = Win(WindowKind.FiveHour, SourceReliability.Exact);
        var pSeven = Win(WindowKind.SevenDay, SourceReliability.Exact);
        var fFive = Win(WindowKind.FiveHour, SourceReliability.Estimated);
        var fSeven = Win(WindowKind.SevenDay, SourceReliability.Estimated);

        var composite = new CompositeUsageProvider(
            new FakeProvider(Snap(pFive, pSeven)),
            new FakeProvider(Snap(fFive, fSeven)));

        var snap = await composite.GetAsync();

        Assert.Same(pFive, snap.FiveHour);
        Assert.Same(pSeven, snap.SevenDay);
    }

    // --- Cas 4 : primaire ET repli Unavailable pour une fenetre -> Unavailable, pas de crash ---

    [Fact]
    public async Task Deux_sources_indisponibles_reste_unavailable_sans_crash()
    {
        var pFive = Win(WindowKind.FiveHour, SourceReliability.Unavailable);
        var pSeven = Win(WindowKind.SevenDay, SourceReliability.Unavailable);
        var fFive = Win(WindowKind.FiveHour, SourceReliability.Unavailable);
        var fSeven = Win(WindowKind.SevenDay, SourceReliability.Unavailable);

        var composite = new CompositeUsageProvider(
            new FakeProvider(Snap(pFive, pSeven)),
            new FakeProvider(Snap(fFive, fSeven)));

        var snap = await composite.GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
    }
}
