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
        // MODE PONT statusLine (--statusline) : court-circuit AVANT toute initialisation WPF/host.
        // Claude Code invoque « Chronos.exe --statusline » à chaque rendu de sa barre : on lit stdin,
        // on matérialise usage.json, on chaîne l'éventuelle barre préexistante, puis on sort tout de suite.
        if (e.Args.Any(a => string.Equals(a, "--statusline", StringComparison.OrdinalIgnoreCase)))
        {
            RunStatusLineBridge();
            Environment.Exit(0);   // sortie immédiate : ne charge jamais l'overlay (rapidité de la barre)
            return;
        }

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

        // Log automatique au démarrage (observabilité) : écrit %APPDATA%/Chronos/chronos.log avec l'état réel
        // (token/OAuth/sources/plafonds). Fire-and-forget, ne bloque pas et ne peut pas casser le lancement.
        _ = _host.Services.GetRequiredService<DiagnosticService>().LogStartupAsync();

        // Restauration AVANT Show (FEN-07) : on fournit l'état persisté à la fenêtre ; SourceInitialized
        // appliquera RestorePlacement (coin + device = vérité) avant le premier rendu → pas de flash.
        var settings = _host.Services.GetRequiredService<ChronosSettings>();
        var window = _host.Services.GetRequiredService<MainWindow>();
        window.ApplyRestoredState(settings);
        MainWindow = window;                         // Application.MainWindow AVANT Show → le dialogue de
                                                     // recalibrage se centre sur l'overlay (Owner), ROB-03/FEN-07
        window.Show();                               // ShowActivated=False (XAML) → pas de vol de focus

        // Première exécution : proposer d'activer la SOURCE EXACTE (pont statusLine Claude Code).
        // Une seule fois (StatusLinePromptDismissed), non bloquant pour le rendu de l'overlay.
        _host.Services.GetRequiredService<IStatusLineSetup>().OfferOnFirstRun();
    }

    // Exécuté en mode --statusline : neutre, sans WPF ni DI. Ne lève jamais (ne doit pas casser la barre).
    // Lecture stdin / écriture stdout en UTF-8 STRICT (Claude Code parle UTF-8) — pas via Console.In/Out,
    // dont l'encodage OEM par défaut mutilerait les caractères non-ASCII de la barre.
    private static void RunStatusLineBridge()
    {
        try
        {
            var utf8 = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var paths = ChronosPaths.Default();
            var settings = new SettingsService(paths).Load();

            string input;
            using (var sr = new System.IO.StreamReader(Console.OpenStandardInput(), utf8))
                input = sr.ReadToEnd();

            var sb = new System.Text.StringBuilder();
            using (var sw = new System.IO.StringWriter(sb))
                StatusLineBridge.Run(paths, settings.InnerStatusLineCommand,
                    new System.IO.StringReader(input), sw, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            var bytes = utf8.GetBytes(sb.ToString());
            using var stdout = Console.OpenStandardOutput();
            stdout.Write(bytes, 0, bytes.Length);
            stdout.Flush();
        }
        catch { /* jamais casser la barre de statut de Claude Code */ }
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

        // Source EXACTE via pont statusLine Claude Code : installateur (édite ~/.claude/settings.json)
        // + setup WPF (menu + proposition au 1er lancement). C'est la voie universelle recommandée.
        services.AddSingleton<StatusLineInstaller>(_ => new StatusLineInstaller());
        services.AddSingleton<IStatusLineSetup>(sp => new Views.StatusLineSetup(
            sp.GetRequiredService<StatusLineInstaller>(),
            sp.GetRequiredService<SettingsService>()));

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

        // v2.1 : SOURCE EXACTE PRIMAIRE = login OAuth propre à Chronos (jeton obtenu par login navigateur,
        // rafraîchi tout seul, stocké chiffré DPAPI). Marche que l'utilisateur soit en app bureau OU terminal.
        services.AddSingleton<ChronosOAuthStore>(_ => new ChronosOAuthStore());
        services.AddSingleton(_ => new ChronosOAuthClient(new HttpClient()));
        services.AddSingleton(sp => new ChronosOAuthUsageProvider(
            sp.GetRequiredService<ChronosOAuthStore>(),
            sp.GetRequiredService<ChronosOAuthClient>(),
            new HttpClient(),
            sp.GetRequiredService<IClock>()));
        services.AddSingleton<IOAuthLogin>(sp => new Views.OAuthLogin(
            sp.GetRequiredService<ChronosOAuthClient>(),
            sp.GetRequiredService<ChronosOAuthStore>()));

        // Chaîne exacte→estimée par imbrication, MEILLEURE source PAR FENÊTRE (composite) :
        //   login OAuth Chronos (exact) → OAuth coffre app (exact, gated) → pont statusLine (exact) → JSONL (estimé).
        services.AddSingleton<IUsageProvider>(sp => new CompositeUsageProvider(
            primary:  sp.GetRequiredService<ChronosOAuthUsageProvider>(),
            fallback: new CompositeUsageProvider(
                primary:  sp.GetRequiredService<GatedOAuthUsageProvider>(),
                fallback: new CompositeUsageProvider(
                    primary:  sp.GetRequiredService<ClaudeUsageObjectProvider>(),
                    fallback: sp.GetRequiredService<JsonlEstimationProvider>()))));

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

        // Diagnostic auto-explicatif (menu « Diagnostic… ») : consomme le lecteur de token + le composite réel.
        services.AddSingleton(sp => new DiagnosticService(
            sp.GetRequiredService<IClaudeTokenReader>(),
            sp.GetRequiredService<ChronosPaths>(),
            sp.GetRequiredService<SettingsService>(),
            sp.GetRequiredService<IUsageProvider>(),
            sp.GetRequiredService<IClock>()));

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
