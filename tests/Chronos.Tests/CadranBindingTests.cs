using System.Windows;
using System.Windows.Controls;
using Chronos.Controls;
using Chronos.Models;
using Chronos.Services;
using Chronos.ViewModels;
using Chronos.Views;
using Xunit;
using WindowState = Chronos.Models.WindowState; // lève l'ambiguïté avec System.Windows.WindowState

namespace Chronos.Tests;

/// <summary>
/// Smoke test du cadran assemblé (05-03) : MainWindow se construit et se bind sans crash dans
/// QUATRE états (exact / estimé / indisponible / fiabilité mixte). Verrouille DAT-08 (badges
/// « estimée » PAR FENÊTRE + converter tolérant à Utilization null) et ROB-01 (deux fenêtres
/// Unavailable → cadran + texte « données indisponibles », zéro crash).
///
/// [WpfFact] (thread STA) : la construction de MainWindow + les SolidColorBrush exigent STA.
/// L'orchestrateur n'est PAS démarré (aucun I/O) ; le VM reçoit le snapshot via ApplySnapshot.
/// </summary>
public class CadranBindingTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    // Construit un MainViewModel déterministe (orchestrateur non démarré, aucun I/O) et lui applique
    // le snapshot voulu, puis construit + met en page la fenêtre (Measure/Arrange déclenche les bindings).
    private static MainWindow BuildWindow(UsageSnapshot snap, out MainViewModel vm)
    {
        var prov = new FakeUsageProvider();
        var orch = new RefreshOrchestrator(prov, ChronosPaths.Default(), RefreshOptions.Default);
        var settings = new SettingsService(ChronosPaths.Default());
        vm = new MainViewModel(orch, new FakeUiDispatcher { OnUiThread = true }, new FakeClock(Now),
            new FakeWindowController(), new FakeAutostartService(), new FakeRecalibrationPrompt(),
            new FakeBudgetPrompt(), settings,
            new DiagnosticService(new FakeClaudeTokenReader(), ChronosPaths.Default(), settings, prov, new FakeClock(Now)));
        vm.ApplySnapshot(snap);

        var guard = new TopmostGuard();
        var controller = new OverlayController(guard, new SettingsService(ChronosPaths.Default()));
        var fenetre = new MainWindow(vm, guard, controller);
        fenetre.Measure(new Size(220, 220));
        fenetre.Arrange(new Rect(0, 0, 220, 220));
        return fenetre;
    }

    [WpfFact]
    public void Etat_exact_deux_fenetres_lisibles_construit_sans_crash()
    {
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState { Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact, Utilization = 0.3, ResetsAt = Now + TimeSpan.FromHours(2) },
            SevenDay = new WindowState { Kind = WindowKind.SevenDay, Reliability = SourceReliability.Exact, Utilization = 0.6, ResetsAt = Now + TimeSpan.FromDays(3) },
            SourceCapturedAt = Now,
        };

        var fenetre = BuildWindow(snap, out var vm);

        Assert.Same(vm, fenetre.DataContext);
        Assert.NotNull(fenetre.FindName("ArcCinqHeures") as RingArc);
        Assert.NotNull(fenetre.FindName("ArcHebdo") as RingArc);
        Assert.False(vm.DataUnavailable);
    }

    [WpfFact]
    public void Etat_estime_utilization_null_ne_crashe_pas_le_converter()
    {
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState { Kind = WindowKind.FiveHour, Reliability = SourceReliability.Estimated, Utilization = null, ResetsAt = null },
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now,
        };

        var fenetre = BuildWindow(snap, out var vm);

        // Centre épuré (v1.3) : plus de badge « estimée » — l'honnêteté passe par le « ~ » du %.
        // Utilization null → PAS de % (texte vide), et le converter sur null n'a pas levé (fenêtre construite).
        Assert.True(vm.FiveHour.IsEstimated);
        Assert.Equal("", vm.FiveHour.UtilizationText);           // aucune valeur inventée
        Assert.NotNull(fenetre.FindName("ArcCinqHeures") as RingArc); // la fenêtre s'est construite sans crash
    }

    [WpfFact]
    public void Etat_indisponible_deux_fenetres_Unavailable_ne_crashe_pas_et_centre_vide()
    {
        var fenetre = BuildWindow(UsageSnapshot.Empty, out var vm);

        Assert.True(vm.DataUnavailable);                          // ROB-01
        // Centre épuré : aucune donnée → aucun % affiché (textes vides), fenêtre construite sans crash.
        Assert.Equal("", vm.FiveHour.UtilizationText);
        Assert.Equal("", vm.SevenDay.UtilizationText);
        Assert.NotNull(fenetre.FindName("ArcHebdo") as RingArc);
    }

    [WpfFact]
    public void Etat_fiabilite_mixte_le_tilde_du_pourcentage_est_par_fenetre()
    {
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState { Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact, Utilization = 0.4, ResetsAt = Now + TimeSpan.FromHours(2) },
            SevenDay = new WindowState { Kind = WindowKind.SevenDay, Reliability = SourceReliability.Estimated, Utilization = 0.9, ResetsAt = Now + TimeSpan.FromDays(3) },
            SourceCapturedAt = Now,
        };

        var fenetre = BuildWindow(snap, out var vm);

        // Honnêteté INDÉPENDANTE par fenêtre, désormais portée par le « ~ » du pourcentage central :
        // 5 h exacte → « 40 % » SANS tilde ; hebdo estimée → « ~90 % » AVEC tilde.
        Assert.False(vm.FiveHour.IsEstimated);
        Assert.True(vm.SevenDay.IsEstimated);
        Assert.DoesNotContain("~", vm.FiveHour.UtilizationText);
        Assert.Contains("40", vm.FiveHour.UtilizationText);
        Assert.StartsWith("~", vm.SevenDay.UtilizationText);
        Assert.Contains("90", vm.SevenDay.UtilizationText);
        Assert.NotNull(fenetre.FindName("ArcCinqHeures") as RingArc);
    }

    // NET-02 : tokens estimés surfacés en texte secondaire discret, dérivés dans WindowGaugeViewModel.Apply.
    // Ces [Fact] testent directement le sous-VM (pas de STA requis : pur, aucun WPF).

    [Fact]
    public void Estimated_avec_tokens_expose_HasTokens_et_TokensText_abrege()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));

        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Estimated,
            EstimatedTokens = 62_484_658,
        });

        Assert.True(vm.HasTokens);
        Assert.Equal("≈ 62,5 M tokens", vm.TokensText);
    }

    [Fact]
    public void Exact_sans_tokens_n_affiche_aucun_texte_de_tokens()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));

        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = 0.3,
            EstimatedTokens = null, // honnêteté : jamais de tokens affichés en source Exact
        });

        Assert.False(vm.HasTokens);
        Assert.Equal("", vm.TokensText);
    }

    [Fact]
    public void Estimated_avec_zero_token_ne_surface_rien()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));

        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Estimated,
            EstimatedTokens = 0,
        });

        Assert.False(vm.HasTokens);
        Assert.Equal("", vm.TokensText);
    }
}
