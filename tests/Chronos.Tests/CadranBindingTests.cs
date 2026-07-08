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
        var orch = new RefreshOrchestrator(new FakeUsageProvider(), ChronosPaths.Default(), RefreshOptions.Default);
        vm = new MainViewModel(orch, new FakeUiDispatcher { OnUiThread = true }, new FakeClock(Now));
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

        Assert.True(vm.FiveHour.IsEstimated);                     // badge « estimée » 5 h attendu
        Assert.NotNull(fenetre.FindName("BadgeEstimeeCinqHeures")); // le converter sur Utilization null n'a pas levé
    }

    [WpfFact]
    public void Etat_indisponible_deux_fenetres_Unavailable_affiche_texte_sans_crash()
    {
        var fenetre = BuildWindow(UsageSnapshot.Empty, out var vm);

        Assert.True(vm.DataUnavailable);                          // ROB-01
        Assert.NotNull(fenetre.FindName("TexteIndisponible") as TextBlock);
    }

    [WpfFact]
    public void Etat_fiabilite_mixte_badges_estimee_sont_par_fenetre()
    {
        var snap = new UsageSnapshot
        {
            FiveHour = new WindowState { Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact, Utilization = 0.4, ResetsAt = Now + TimeSpan.FromHours(2) },
            SevenDay = new WindowState { Kind = WindowKind.SevenDay, Reliability = SourceReliability.Estimated, Utilization = null, ResetsAt = Now + TimeSpan.FromDays(3) },
            SourceCapturedAt = Now,
        };

        var fenetre = BuildWindow(snap, out var vm);

        // Preuve que le signal « estimée » est INDÉPENDANT par fenêtre (le composite choisit la
        // meilleure source par fenêtre) : 5 h exacte, hebdo estimée.
        Assert.False(vm.FiveHour.IsEstimated);
        Assert.True(vm.SevenDay.IsEstimated);
        Assert.NotNull(fenetre.FindName("BadgeEstimeeCinqHeures") as TextBlock);
        Assert.NotNull(fenetre.FindName("BadgeEstimeeHebdo") as TextBlock);
    }
}
