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
}
