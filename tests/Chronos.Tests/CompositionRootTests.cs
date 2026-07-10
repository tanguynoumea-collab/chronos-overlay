using System.Windows.Threading;
using Chronos.Services;
using Chronos.ViewModels;
using Chronos.Views;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Preuve du success criterion 3 : le conteneur résout MainWindow/MainViewModel par DI
/// et dispose les Singletons IDisposable à la fermeture (contexte STA pour construire la Window).
/// </summary>
public class CompositionRootTests
{
    /// <summary>Marqueur IDisposable enregistré comme Singleton pour observer la disposition du conteneur.</summary>
    private sealed class MarqueurDisposable : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    [WpfFact]
    public void Host_resout_et_dispose_les_singletons()
    {
        // Reproduit ConfigureServices (App.xaml.cs) dans un conteneur de test.
        // Sur STA, CurrentDispatcher fournit un Dispatcher valide pour WpfUiDispatcher.
        var services = new ServiceCollection();
        services.AddSingleton<IUiDispatcher>(_ => new WpfUiDispatcher(Dispatcher.CurrentDispatcher));
        services.AddSingleton<TopmostGuard>();          // requis par le ctor de MainWindow (ROB-04)

        // Pipeline de données Phase 3 + orchestrateur Phase 4 (miroir de App.xaml.cs) :
        // le MainViewModel dépend désormais de RefreshOrchestrator + IClock (04-02).
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(ChronosPaths.Default());
        services.AddSingleton<ClaudeUsageObjectProvider>();
        services.AddSingleton<JsonlEstimationProvider>();
        services.AddSingleton<IUsageProvider>(sp => new CompositeUsageProvider(
            primary: sp.GetRequiredService<ClaudeUsageObjectProvider>(),
            fallback: sp.GetRequiredService<JsonlEstimationProvider>()));
        services.AddSingleton(RefreshOptions.Default);
        services.AddSingleton<RefreshOrchestrator>();

        // Phase 6 : le ctor de MainWindow dépend désormais de OverlayController (placement),
        // lui-même de SettingsService (ChronosPaths déjà enregistré ci-dessus).
        services.AddSingleton<SettingsService>();
        services.AddSingleton<OverlayController>();

        // 06-04 : le ctor de MainViewModel dépend de IWindowController + IAutostartService +
        // IRecalibrationPrompt (menu contextuel). On câble le controller réel (déjà résolu),
        // un autostart pointant sur un dossier temp (aucune pollution de shell:startup) et un
        // prompt neutre programmé (aucun dialogue WPF ouvert en test).
        services.AddSingleton<IWindowController>(sp => sp.GetRequiredService<OverlayController>());
        services.AddSingleton<IAutostartService>(_ =>
            new AutostartService(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChronosStartup_" + System.Guid.NewGuid().ToString("N"))));
        services.AddSingleton<IRecalibrationPrompt>(_ => new FakeRecalibrationPrompt());

        // 09-02 : le ctor de MainViewModel dépend désormais aussi de IBudgetPrompt (dialogue de
        // calibration manuelle des plafonds, CAL-01). Prompt neutre programmé (aucun dialogue WPF en test).
        services.AddSingleton<IBudgetPrompt>(_ => new FakeBudgetPrompt());

        // 09-02 : le calibrateur auto (CAL-02) est résolu eager au démarrage (abonnement à
        // SnapshotChanged) ; on l'enregistre ici pour prouver que le graphe DI le câble et le dispose.
        services.AddSingleton(sp => new BudgetAutoCalibrator(
            sp.GetRequiredService<RefreshOrchestrator>(),
            sp.GetRequiredService<JsonlEstimationProvider>(),
            sp.GetRequiredService<SettingsService>(),
            sp.GetRequiredService<IClock>()));

        // v1.4 : le ctor de MainViewModel dépend désormais aussi de DiagnosticService (menu « Diagnostic… »).
        services.AddSingleton<IClaudeTokenReader>(_ => new FakeClaudeTokenReader());
        services.AddSingleton(sp => new DiagnosticService(
            sp.GetRequiredService<IClaudeTokenReader>(),
            sp.GetRequiredService<ChronosPaths>(),
            sp.GetRequiredService<SettingsService>(),
            sp.GetRequiredService<IUsageProvider>(),
            sp.GetRequiredService<IClock>()));

        // Source exacte via pont statusLine : le ctor de MainViewModel dépend d'IStatusLineSetup.
        services.AddSingleton<IStatusLineSetup>(_ => new FakeStatusLineSetup());
        // Login OAuth intégré : le ctor de MainViewModel dépend d'IOAuthLogin.
        services.AddSingleton<IOAuthLogin>(_ => new FakeOAuthLogin());
        // Widget de sessions : le ctor de MainViewModel dépend d'ISessionsController.
        services.AddSingleton<ISessionsController>(_ => new FakeSessionsController());

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MarqueurDisposable>();   // marqueur pour prouver la disposition

        var provider = services.BuildServiceProvider();

        // Résolution sans exception → preuve que le graphe DI est câblé (partie « lance »).
        Assert.NotNull(provider.GetRequiredService<MainWindow>());
        Assert.NotNull(provider.GetRequiredService<MainViewModel>());
        Assert.NotNull(provider.GetRequiredService<BudgetAutoCalibrator>());

        var marqueur = provider.GetRequiredService<MarqueurDisposable>();
        Assert.False(marqueur.Disposed);

        // Disposition du conteneur → dispose les Singletons IDisposable (partie « ferme proprement »).
        provider.Dispose();
        Assert.True(marqueur.Disposed);
    }

    /// <summary>
    /// GARDE DI RÉELLE (Phase 13, étendue Phase 14) — au-delà de `dotnet build`. Le conteneur miroir de
    /// <see cref="Host_resout_et_dispose_les_singletons"/> n'inclut PAS la sous-chaîne bureau UIA :
    /// une DI mal ordonnée (SessionMonitor résolu sans DesktopUiaSessionSource enregistré) ou un service
    /// manquant (IUiaTreeProvider) COMPILERAIT et ne planterait qu'au DÉMARRAGE de l'app, pas au build.
    /// Ce test enregistre EXACTEMENT la sous-chaîne bureau telle qu'elle est câblée dans App.xaml.cs et
    /// prouve que <c>SessionMonitor</c> ET <c>DesktopUiaPollService</c> se résolvent sans exception
    /// (= graphe câblé dans le bon ordre). [Fact] simple : aucun besoin de STA/WPF (pas de MainWindow) —
    /// construire WindowsUiaTreeProvider/WindowsForegroundWatch ne touche PAS l'OS tant que rien n'est appelé.
    ///
    /// Phase 14 : la garde couvre désormais aussi <c>TreatedStore</c>, <c>SessionTreatmentTracker</c> et
    /// <c>IForegroundWatch</c> (focus premier-plan RÉEL injecté comme 7e param de <c>SessionMonitor</c>) —
    /// attrape une DI mal ordonnée du câblage NET-02 qui ne planterait qu'au démarrage.
    /// </summary>
    [Fact]
    public void Le_graphe_DI_resout_les_services_bureau_UIA()
    {
        var services = new ServiceCollection();

        // Sous-chaîne bureau EXACTEMENT comme dans App.xaml.cs (bloc « Source BUREAU (UIA) » + hystérésis) :
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(_ => new ArchiveStore(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChronosArch_" + System.Guid.NewGuid().ToString("N") + ".json")));
        services.AddSingleton<IUiaTreeProvider>(_ => new WindowsUiaTreeProvider());
        services.AddSingleton(sp => new DesktopUiaSessionSource(sp.GetRequiredService<IUiaTreeProvider>()));
        services.AddSingleton(_ => new TreatedStore(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChronosTreated_" + System.Guid.NewGuid().ToString("N") + ".json")));
        services.AddSingleton(sp => new SessionTreatmentTracker(sp.GetRequiredService<TreatedStore>()));
        services.AddSingleton<IForegroundWatch>(_ => new WindowsForegroundWatch());
        services.AddSingleton(sp => new SessionMonitor(null, null, sp.GetRequiredService<ArchiveStore>(),
            sp.GetRequiredService<DesktopUiaSessionSource>(),
            sp.GetRequiredService<TreatedStore>(),
            sp.GetRequiredService<SessionTreatmentTracker>(),
            sp.GetRequiredService<IForegroundWatch>()));
        services.AddSingleton(sp => new DesktopUiaPollService(
            sp.GetRequiredService<DesktopUiaSessionSource>(),
            sp.GetRequiredService<IClock>()));
        services.AddHostedService(sp => sp.GetRequiredService<DesktopUiaPollService>());

        var provider = services.BuildServiceProvider();

        // Résolution sans exception = graphe câblé dans le BON ORDRE (aucun service manquant/mal ordonné).
        Assert.NotNull(provider.GetRequiredService<SessionMonitor>());
        Assert.NotNull(provider.GetRequiredService<DesktopUiaPollService>());

        provider.Dispose(); // dispose le poll de fond (IDisposable) sans erreur
    }
}
