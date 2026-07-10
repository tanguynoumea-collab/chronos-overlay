using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using Chronos.Models;
using Chronos.Services;
using Chronos.Theming;
using Chronos.ViewModels;
using Chronos.Views;
using Xunit;
using WindowState = Chronos.Models.WindowState;

namespace Chronos.Tests;

/// <summary>
/// Prouve le moteur de thèmes : intégrité du catalogue, rampe à stops personnalisés, couleur de l'arc
/// (neutre/épuisé/rampe) selon le thème, réactivité de <see cref="WindowGaugeViewModel.ValueBrush"/>,
/// persistance de la clé de thème, et smoke test XAML de la fenêtre de réglages.
/// </summary>
public class ThemingTests
{
    private static string TempDir() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "chronos-thm-" + Guid.NewGuid().ToString("N"));
    private static ChronosPaths TempPaths() { var d = TempDir(); System.IO.Directory.CreateDirectory(d); return new(System.IO.Path.Combine(d, "usage.json"), System.IO.Path.Combine(d, "projects")); }

    [Fact]
    public void Catalogue_a_six_themes_aux_cles_uniques()
    {
        Assert.Equal(6, ThemeCatalog.All.Count);
        Assert.Equal(ThemeCatalog.All.Count, ThemeCatalog.All.Select(t => t.Key).Distinct().Count());
        Assert.Equal("minuit", ThemeCatalog.Default.Key);
    }

    [Fact]
    public void ByKey_inconnu_retombe_sur_minuit_insensible_a_la_casse()
    {
        Assert.Equal("minuit", ThemeCatalog.ByKey("n'existe pas").Key);
        Assert.Equal("nord", ThemeCatalog.ByKey("NORD").Key);
    }

    [Fact]
    public void Disque_est_translucide_alpha_E6()
    {
        Assert.Equal(0xE6, ThemeCatalog.ByKey("minuit").FondCadran.A);
    }

    [Fact]
    public void ArcColor_gere_neutre_epuise_et_rampe()
    {
        var t = ThemeCatalog.ByKey("nord");
        Assert.Equal(t.Neutre, t.ArcColor(null));       // utilization inconnue → neutre
        Assert.Equal(t.Epuise, t.ArcColor(1.0));         // ≥ 100 % → épuisé
        Assert.Equal(t.RampGreen, t.ArcColor(0.0));      // 0 % → vert de la rampe du thème
        Assert.Equal(t.RampRed, t.ArcColor(0.999999));   // proche de 100 % → ~rouge
    }

    [Fact]
    public void Rampe_a_stops_personnalises_respecte_les_bornes()
    {
        Color g = Color.FromRgb(1, 2, 3), a = Color.FromRgb(10, 20, 30), r = Color.FromRgb(200, 100, 50);
        Assert.Equal(g, Chronos.Rendering.RampColor.Interpolate(0.0, g, a, r));
        Assert.Equal(a, Chronos.Rendering.RampColor.Interpolate(0.55, g, a, r));
        Assert.Equal(r, Chronos.Rendering.RampColor.Interpolate(1.0, g, a, r));
    }

    [Fact]
    public void ValueBrush_suit_le_theme_et_l_utilization()
    {
        var g = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        g.SetTheme(ThemeCatalog.ByKey("nord"));
        g.Apply(new WindowState { Kind = WindowKind.FiveHour, Utilization = 0.0, Reliability = SourceReliability.Exact });
        Assert.Equal(ThemeCatalog.ByKey("nord").RampGreen, ((SolidColorBrush)g.ValueBrush!).Color);

        // Changer de thème recalcule la couleur pour l'utilization courante.
        g.SetTheme(ThemeCatalog.ByKey("ambre"));
        Assert.Equal(ThemeCatalog.ByKey("ambre").RampGreen, ((SolidColorBrush)g.ValueBrush!).Color);
    }

    [Fact]
    public void ThemeKey_persiste_dans_settings()
    {
        var svc = new SettingsService(TempPaths());
        Assert.Equal("minuit", svc.Load().ThemeKey);           // défaut
        svc.Save(svc.Load() with { ThemeKey = "aurore" });
        Assert.Equal("aurore", svc.Load().ThemeKey);
    }

    // --- Smoke test XAML de la fenêtre de réglages (STA) ---

    [WpfFact]
    public void SettingsWindow_se_construit_et_se_met_en_page_sans_crash()
    {
        var settings = new SettingsService(TempPaths());
        var provider = new FakeUsageProvider();
        var orch = new RefreshOrchestrator(provider, TempPaths(), new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero));
        var clock = new FakeClock(new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero));
        var vm = new MainViewModel(orch, new FakeUiDispatcher { OnUiThread = true }, clock,
            new FakeWindowController(), new FakeAutostartService(), new FakeRecalibrationPrompt(),
            new FakeBudgetPrompt(), settings,
            new DiagnosticService(new FakeClaudeTokenReader(), TempPaths(), settings, provider, clock),
            new FakeStatusLineSetup(), new FakeOAuthLogin(), new FakeSessionsController());

        var win = new SettingsWindow(vm);
        win.Measure(new Size(1000, 1000));
        win.Arrange(new Rect(0, 0, 1000, 1000));

        Assert.NotNull(win.Content);
        Assert.Equal(6, vm.Themes.Count);                 // les 6 thèmes alimentent la grille
        Assert.Contains(vm.Themes, t => t.IsSelected);    // un thème est sélectionné
    }
}
