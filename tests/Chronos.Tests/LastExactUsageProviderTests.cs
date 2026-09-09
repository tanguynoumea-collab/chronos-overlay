using System.IO;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve EXA-01 côté décorateur : il est l'ÉCRIVAIN UNIQUE du magasin (il voit le snapshot fusionné
/// qui sera affiché) et il ne fait QUE reboucher les trous — une réponse vivante n'est jamais
/// remplacée, une fenêtre estimée n'est ni écrite ni substituée, et un relevé dont le reset est
/// passé n'est jamais ressuscité.
///
/// Isolation stricte : magasin dans un dossier temp unique — jamais %APPDATA%\Chronos.
/// Tests purs (aucun type WPF) → [Fact] classiques, pas de STA.
/// </summary>
public class LastExactUsageProviderTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 09, 09, 12, 0, 0, TimeSpan.Zero);

    private readonly string _dir;
    private readonly string _fichier;
    private readonly LastExactStore _store;
    private readonly FakeClock _clock = new(Now);

    public LastExactUsageProviderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ChronosLastExactDeco_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _fichier = Path.Combine(_dir, "last-exact.json");
        _store = new LastExactStore(_fichier);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private static WindowState Win(WindowKind k, SourceReliability r, double? util = null,
                                   DateTimeOffset? resets = null, DateTimeOffset? captured = null)
        => new()
        {
            Kind = k,
            Utilization = util,
            ResetsAt = resets,
            CapturedAt = captured,
            Reliability = r,
        };

    private static UsageSnapshot Snap(WindowState five, WindowState seven)
        => new() { FiveHour = five, SevenDay = seven };

    private LastExactUsageProvider Deco(FakeUsageProvider inner)
        => new(inner, _store, _clock);

    // --- 1. Écriture : chaque fenêtre exacte vue passer est mémorisée ---

    [Fact]
    public async Task Ecrit_dans_le_magasin_les_deux_fenetres_exactes()
    {
        var inner = new FakeUsageProvider
        {
            Next = Snap(
                Win(WindowKind.FiveHour, SourceReliability.Exact, 0.42, Now.AddHours(3), Now),
                Win(WindowKind.SevenDay, SourceReliability.Exact, 0.63, Now.AddDays(4), Now)),
        };

        await Deco(inner).GetAsync();

        var persiste = _store.Load(Now);
        Assert.NotNull(persiste);
        Assert.Equal(0.42, persiste!.FiveHour!.Utilization);
        Assert.Equal(0.63, persiste.SevenDay!.Utilization);
    }

    // --- 2. Passe-plat : quand tout est vivant, le décorateur est transparent ---

    [Fact]
    public async Task Snapshot_entierement_exact_est_retourne_tel_quel()
    {
        var five = Win(WindowKind.FiveHour, SourceReliability.Exact, 0.42, Now.AddHours(3), Now);
        var seven = Win(WindowKind.SevenDay, SourceReliability.Exact, 0.63, Now.AddDays(4), Now);
        var inner = new FakeUsageProvider { Next = Snap(five, seven) };

        var snap = await Deco(inner).GetAsync();

        Assert.Equal(0.42, snap.FiveHour.Utilization);
        Assert.Equal(Now.AddHours(3), snap.FiveHour.ResetsAt);
        Assert.Equal(0.63, snap.SevenDay.Utilization);
        Assert.Equal(Now.AddDays(4), snap.SevenDay.ResetsAt);
    }

    // --- 3. Rebouchage d'un trou : la fenêtre indisponible est substituée, l'autre reste intacte ---

    [Fact]
    public async Task Rebouche_une_fenetre_indisponible_depuis_le_magasin()
    {
        var capture = Now.AddHours(-2);
        _store.Save(Snap(
            Win(WindowKind.FiveHour, SourceReliability.Exact, 0.42, Now.AddHours(3), capture),
            WindowState.Unavailable(WindowKind.SevenDay)));

        var inner = new FakeUsageProvider { Next = UsageSnapshot.Empty };

        var snap = await Deco(inner).GetAsync();

        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
        Assert.Equal(0.42, snap.FiveHour.Utilization);
        Assert.Equal(capture, snap.FiveHour.CapturedAt);   // horodatage d'ORIGINE conservé
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);  // rien à reboucher
    }

    // --- 4. Une réponse vivante n'est JAMAIS remplacée par le magasin ---

    [Fact]
    public async Task Ne_remplace_jamais_une_reponse_vivante()
    {
        _store.Save(Snap(
            Win(WindowKind.FiveHour, SourceReliability.Exact, 0.80, Now.AddHours(3), Now.AddHours(-2)),
            WindowState.Unavailable(WindowKind.SevenDay)));

        var inner = new FakeUsageProvider
        {
            Next = Snap(
                Win(WindowKind.FiveHour, SourceReliability.Exact, 0.20, Now.AddHours(4), Now),
                WindowState.Unavailable(WindowKind.SevenDay)),
        };

        var snap = await Deco(inner).GetAsync();

        Assert.Equal(0.20, snap.FiveHour.Utilization);   // la valeur vivante, pas le 0,80 du magasin
    }

    // --- 5. Un relevé dont le reset est passé n'est jamais ressuscité ---

    [Fact]
    public async Task Ne_rebouche_pas_depuis_une_fenetre_dont_le_reset_est_passe()
    {
        _store.Save(Snap(
            Win(WindowKind.FiveHour, SourceReliability.Exact, 0.80, Now.AddMinutes(-1), Now.AddHours(-6)),
            WindowState.Unavailable(WindowKind.SevenDay)));

        var inner = new FakeUsageProvider { Next = UsageSnapshot.Empty };

        var snap = await Deco(inner).GetAsync();

        Assert.Equal(SourceReliability.Unavailable, snap.FiveHour.Reliability);
        Assert.Null(snap.FiveHour.Utilization);
    }

    // --- 6. Portée volontairement étroite : l'estimation n'est ni écrite, ni rebouchée ---

    [Fact]
    public async Task Fenetre_estimee_n_est_ni_rebouchee_ni_ecrite()
    {
        _store.Save(Snap(
            Win(WindowKind.FiveHour, SourceReliability.Exact, 0.80, Now.AddHours(3), Now.AddHours(-2)),
            WindowState.Unavailable(WindowKind.SevenDay)));
        var avant = File.ReadAllText(_fichier);

        var inner = new FakeUsageProvider
        {
            Next = Snap(
                Win(WindowKind.FiveHour, SourceReliability.Estimated, 0.30),
                WindowState.Unavailable(WindowKind.SevenDay)),
        };

        var snap = await Deco(inner).GetAsync();

        Assert.Equal(SourceReliability.Estimated, snap.FiveHour.Reliability);  // pas de substitution
        Assert.Equal(0.30, snap.FiveHour.Utilization);
        Assert.Equal(avant, File.ReadAllText(_fichier));                       // pas d'écriture
    }

    // --- 7. Robustesse : un magasin illisible ne prive jamais l'utilisateur de son affichage ---

    [Fact]
    public async Task Magasin_corrompu_ne_leve_pas_et_rend_le_snapshot_de_l_inner()
    {
        File.WriteAllText(_fichier, "{ pas du JSON ]");

        var inner = new FakeUsageProvider
        {
            Next = Snap(
                Win(WindowKind.FiveHour, SourceReliability.Estimated, 0.30),
                WindowState.Unavailable(WindowKind.SevenDay)),
        };

        var snap = await Deco(inner).GetAsync();

        Assert.Equal(SourceReliability.Estimated, snap.FiveHour.Reliability);
        Assert.Equal(0.30, snap.FiveHour.Utilization);
        Assert.Equal(SourceReliability.Unavailable, snap.SevenDay.Reliability);
    }

    // --- 8. Aucune écriture inutile : rien d'exact, rien à mémoriser ---

    [Fact]
    public async Task Deux_passages_sans_rien_d_exact_ne_creent_aucun_fichier()
    {
        var inner = new FakeUsageProvider { Next = UsageSnapshot.Empty };
        var deco = Deco(inner);

        await deco.GetAsync();
        await deco.GetAsync();

        Assert.False(File.Exists(_fichier));
        Assert.Equal(2, inner.GetCount);
    }

    // --- 9. Aucun horodatage inventé au niveau snapshot : l'âge honnête est porté par la fenêtre ---

    [Fact]
    public async Task Rebouchage_n_invente_aucun_SourceCapturedAt()
    {
        var capture = Now.AddHours(-2);
        _store.Save(Snap(
            Win(WindowKind.FiveHour, SourceReliability.Exact, 0.42, Now.AddHours(3), capture),
            WindowState.Unavailable(WindowKind.SevenDay)));

        var inner = new FakeUsageProvider { Next = UsageSnapshot.Empty };

        var snap = await Deco(inner).GetAsync();

        Assert.Null(snap.SourceCapturedAt);              // inconnu reste inconnu
        Assert.Equal(capture, snap.FiveHour.CapturedAt); // l'âge honnête vit sur la fenêtre
    }
}
