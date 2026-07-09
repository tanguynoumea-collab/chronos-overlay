using System.Net.Http;
using System.Windows;
using Chronos.Services;
using Chronos.ViewModels;
using Chronos.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Chronos;

public partial class App : Application
{
    private IHost _host = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();
        ConfigureServices(builder.Services);
        _host = builder.Build();

        // Ordre de démarrage (Pitfall 3) : résoudre le VM AVANT StartAsync pour forcer son abonnement
        // à RefreshOrchestrator.SnapshotChanged. Sinon la charge initiale (émise pendant StartAsync)
        // partirait avant tout abonné → overlay vide jusqu'au prochain tick périodique (~60 s).
        _ = _host.Services.GetRequiredService<MainViewModel>();

        // CAL-02 : même raison — résoudre le calibrateur AVANT StartAsync pour forcer son abonnement
        // à SnapshotChanged avant la charge initiale (sinon il raterait le premier snapshot Exact).
        _ = _host.Services.GetRequiredService<BudgetAutoCalibrator>();

        await _host.StartAsync();                    // charge initiale → atteint le VM (Post mis en file via BeginInvoke)

        // Restauration AVANT Show (FEN-07) : on fournit l'état persisté à la fenêtre ; SourceInitialized
        // appliquera RestorePlacement (coin + device = vérité) avant le premier rendu → pas de flash.
        var settings = _host.Services.GetRequiredService<ChronosSettings>();
        var window = _host.Services.GetRequiredService<MainWindow>();
        window.ApplyRestoredState(settings);
        MainWindow = window;                         // Application.MainWindow AVANT Show → le dialogue de
                                                     // recalibrage se centre sur l'overlay (Owner), ROB-03/FEN-07
        window.Show();                               // ShowActivated=False (XAML) → pas de vol de focus
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Blocage volontaire : dispose déterministe des Singletons IDisposable
        // (évite le piège async-void qui n'attend pas StopAsync).
        _host.StopAsync().GetAwaiter().GetResult();
        _host.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IUiDispatcher>(_ => new WpfUiDispatcher(Current.Dispatcher));
        services.AddSingleton<TopmostGuard>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        // Placement/persistance Phase 6 (FEN-03/04/05/07) : settings.json chargé UNE fois au démarrage
        // (coin + device = vérité), adaptateur de placement, contrat neutre pour le VM (menu 06-04).
        services.AddSingleton<SettingsService>();
        services.AddSingleton(sp => sp.GetRequiredService<SettingsService>().Load());   // ChronosSettings (une lecture)
        services.AddSingleton<OverlayController>();
        services.AddSingleton<IWindowController>(sp => sp.GetRequiredService<OverlayController>());

        // Menu contextuel 06-04 (FEN-06) : autostart shell:startup (DEP-02, service neutre de 06-02)
        // + dialogue de recalibrage hebdo (ROB-03) via le prompt WPF (namespace Views, hors pureté Services).
        services.AddSingleton<IAutostartService>(_ => new AutostartService());
        services.AddSingleton<IRecalibrationPrompt, RecalibrationPrompt>();

        // CAL-01 : dialogue de calibration manuelle des plafonds (namespace Views, hors pureté Services).
        services.AddSingleton<IBudgetPrompt, BudgetPrompt>();

        // Pipeline de donnees Phase 3 : primaire (pont usage.json) -> repli (JSONL), composite
        // expose comme IUsageProvider. Chemins via Environment (jamais Assembly.Location, mono-fichier).
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(ChronosPaths.Default());
        services.AddSingleton<ClaudeUsageObjectProvider>();
        services.AddSingleton<JsonlEstimationProvider>();

        // v1.2 (INT-01/03) : source EXACTE OAuth en tête de chaîne. Le reader cible le coffre de l'app
        // bureau (%APPDATA%/Claude) ; il n'est JAMAIS sollicité tant que le portillon gated est fermé.
        services.AddSingleton<IClaudeTokenReader>(_ => ClaudeTokenReader.Default());
        services.AddSingleton(sp => new ClaudeOAuthUsageProvider(
            sp.GetRequiredService<IClaudeTokenReader>(),
            new HttpClient(),                                    // long-lived, une seule destination (constante)
            sp.GetRequiredService<IClock>()));
        // Portillon gated : OAuthUsageEnabled==false → Empty sans toucher au token (INT-03).
        services.AddSingleton(sp => new GatedOAuthUsageProvider(
            sp.GetRequiredService<ClaudeOAuthUsageProvider>(),
            sp.GetRequiredService<SettingsService>()));

        // Chaîne à 3 par imbrication (INT-01) : OAuth (exact, gated) → pont statusLine (exact) → JSONL (estimé).
        // Le CompositeUsageProvider gère déjà « meilleure source PAR FENÊTRE » + staleness → aucune réécriture.
        services.AddSingleton<IUsageProvider>(sp => new CompositeUsageProvider(
            primary:  sp.GetRequiredService<GatedOAuthUsageProvider>(),
            fallback: new CompositeUsageProvider(
                primary:  sp.GetRequiredService<ClaudeUsageObjectProvider>(),
                fallback: sp.GetRequiredService<JsonlEstimationProvider>())));

        // Horloge DONNÉES Phase 4 : l'orchestrateur est enregistré UNE fois (Singleton, pour l'abonnement
        // du VM) et réexposé comme IHostedService via la MÊME instance (cycle de vie Start/Stop du host).
        // FEN-07 : l'intervalle de rafraîchissement est dérivé de settings.json (persisté sans UI de réglage).
        services.AddSingleton(sp =>
        {
            var s = sp.GetRequiredService<ChronosSettings>();
            var secs = s.RefreshIntervalSeconds > 0 ? s.RefreshIntervalSeconds : 60;
            return new RefreshOptions(TimeSpan.FromSeconds(secs), TimeSpan.FromMilliseconds(300));
        });
        services.AddSingleton<RefreshOrchestrator>();
        services.AddHostedService(sp => sp.GetRequiredService<RefreshOrchestrator>());

        // CAL-02 : calibrateur auto opportuniste. tokenSource = JsonlEstimationProvider CONCRET
        // (porte toujours EstimatedTokens ; le composite les perd sur une fenêtre Exact). Singleton
        // IDisposable → disposé par le host à l'arrêt (aucune disposition manuelle requise).
        services.AddSingleton(sp => new BudgetAutoCalibrator(
            sp.GetRequiredService<RefreshOrchestrator>(),
            sp.GetRequiredService<JsonlEstimationProvider>(),
            sp.GetRequiredService<SettingsService>(),
            sp.GetRequiredService<IClock>()));
    }
}
