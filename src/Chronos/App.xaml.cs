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

        await _host.StartAsync();                    // charge initiale → atteint le VM (Post mis en file via BeginInvoke)

        var window = _host.Services.GetRequiredService<MainWindow>();
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

        // Pipeline de donnees Phase 3 : primaire (pont usage.json) -> repli (JSONL), composite
        // expose comme IUsageProvider. Chemins via Environment (jamais Assembly.Location, mono-fichier).
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(ChronosPaths.Default());
        services.AddSingleton<ClaudeUsageObjectProvider>();
        services.AddSingleton<JsonlEstimationProvider>();
        services.AddSingleton<IUsageProvider>(sp => new CompositeUsageProvider(
            primary:  sp.GetRequiredService<ClaudeUsageObjectProvider>(),
            fallback: sp.GetRequiredService<JsonlEstimationProvider>()));

        // Horloge DONNÉES Phase 4 : l'orchestrateur est enregistré UNE fois (Singleton, pour l'abonnement
        // du VM) et réexposé comme IHostedService via la MÊME instance (cycle de vie Start/Stop du host).
        services.AddSingleton(RefreshOptions.Default);
        services.AddSingleton<RefreshOrchestrator>();
        services.AddHostedService(sp => sp.GetRequiredService<RefreshOrchestrator>());
    }
}
