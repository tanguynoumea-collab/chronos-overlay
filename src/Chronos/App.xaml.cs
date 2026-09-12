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

        // MODE HOOK (--hook <Event>) : Claude Code invoque « Chronos.exe --hook Notification/Stop/… » à
        // chaque événement de session ; on lit le JSON stdin et on écrit l'état de la session sur disque.
        int hookIdx = System.Array.FindIndex(e.Args, a => string.Equals(a, "--hook", StringComparison.OrdinalIgnoreCase));
        if (hookIdx >= 0)
        {
            RunSessionHook(hookIdx + 1 < e.Args.Length ? e.Args[hookIdx + 1] : null);
            Environment.Exit(0);
            return;
        }

        // MODE GALERIE CADRANS (--cadrans) : prototype visuel des 4 pistes de cadran (overlay 1). Court-circuite
        // le pipeline temps réel et le host DI : une simple fenêtre avec des données d'échantillon pilotables,
        // pour juger les concepts au coup d'œil. Sert la refonte visuelle (llm-council), hors app livrée.
        if (e.Args.Any(a => string.Equals(a, "--cadrans", StringComparison.OrdinalIgnoreCase)))
        {
            base.OnStartup(e);
            var gallery = new CadranGalleryWindow();
            MainWindow = gallery;
            gallery.Show();
            return;
        }

        // MODE GALERIE SESSIONS (--sessions) : prototype visuel des 8 styles du widget de sessions (overlay 2),
        // avec des données d'échantillon. Court-circuite le pipeline/host, comme --cadrans.
        if (e.Args.Any(a => string.Equals(a, "--sessions", StringComparison.OrdinalIgnoreCase)))
        {
            base.OnStartup(e);
            var gallery = new SessionsGalleryWindow();
            MainWindow = gallery;
            gallery.Show();
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

        await _host.StartAsync();                    // charge initiale → atteint le VM (Post mis en file via BeginInvoke)

        // Log automatique au démarrage (observabilité) : écrit %APPDATA%/Chronos/chronos.log avec l'état réel
        // (token/OAuth/sources). Fire-and-forget, ne bloque pas et ne peut pas casser le lancement.
        _ = _host.Services.GetRequiredService<DiagnosticService>().LogStartupAsync();

        // Restauration AVANT Show (FEN-07) : on fournit l'état persisté à la fenêtre ; SourceInitialized
        // appliquera RestorePlacement (coin + device = vérité) avant le premier rendu → pas de flash.
        var settings = _host.Services.GetRequiredService<ChronosSettings>();
        var window = _host.Services.GetRequiredService<MainWindow>();
        window.ApplyRestoredState(settings);
        MainWindow = window;                         // Application.MainWindow AVANT Show → le dialogue de
                                                     // recalibrage se centre sur l'overlay (Owner), ROB-03/FEN-07
        window.Show();                               // ShowActivated=False (XAML) → pas de vol de focus

        // PUR-01/02/03 — réconcilier ~/.claude/settings.json AVANT de proposer la source exacte, pour que
        // l'offre porte sur un état déjà propre (une barre Chronos périmée est repointée ici, donc
        // IsEnabled() répond juste juste après). Mode OVERLAY UNIQUEMENT : les modes --statusline et --hook
        // sortent bien plus haut (lignes 20 et 30) et ne doivent JAMAIS atteindre ce point — 5 processus
        // --hook concurrents en lire-modifier-écrire perdraient les purges, et --statusline est invoqué à
        // chaque rendu de la barre. Best-effort et silencieux : ne peut pas empêcher le démarrage.
        try
        {
            _host.Services.GetRequiredService<ClaudeSettingsReconciler>()
                 .Reconcile(settings.SessionsWidgetEnabled);
        }
        catch { }

        // Première exécution : proposer d'activer la SOURCE EXACTE (pont statusLine Claude Code).
        // Une seule fois (StatusLinePromptDismissed), non bloquant pour le rendu de l'overlay.
        _host.Services.GetRequiredService<IStatusLineSetup>().OfferOnFirstRun();

        // Widget de sessions : réafficher le panneau s'il était activé.
        _host.Services.GetRequiredService<ISessionsController>().ShowIfEnabled();
    }

    // Exécuté en mode --hook : lit le JSON stdin de Claude Code, écrit/supprime le fichier d'état de la
    // session dans %APPDATA%\Chronos\sessions\<id>.json. Neutre, ne lève jamais (ne doit pas casser le hook).
    private static void RunSessionHook(string? eventName)
    {
        try
        {
            var utf8 = new System.Text.UTF8Encoding(false);
            string input;
            using (var sr = new System.IO.StreamReader(Console.OpenStandardInput(), utf8))
                input = sr.ReadToEnd();

            var res = SessionHookProcessor.Process(eventName, input, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (res.Ignore || string.IsNullOrEmpty(res.SessionId)) return;

            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chronos", "sessions");
            System.IO.Directory.CreateDirectory(dir);
            var file = System.IO.Path.Combine(dir, res.SessionId + ".json");

            if (res.Delete)
            {
                try { if (System.IO.File.Exists(file)) System.IO.File.Delete(file); } catch { }
            }
            else if (res.StateJson is not null)
            {
                var tmp = file + ".tmp-" + Environment.ProcessId;
                System.IO.File.WriteAllText(tmp, res.StateJson);
                System.IO.File.Move(tmp, file, overwrite: true);
            }
        }
        catch { /* un hook ne doit jamais casser la session Claude Code */ }
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

        // Source EXACTE via pont statusLine Claude Code : installateur (édite ~/.claude/settings.json)
        // + setup WPF (menu + proposition au 1er lancement). C'est la voie universelle recommandée.
        services.AddSingleton<StatusLineInstaller>(_ => new StatusLineInstaller());
        services.AddSingleton<IStatusLineSetup>(sp => new Views.StatusLineSetup(
            sp.GetRequiredService<StatusLineInstaller>(),
            sp.GetRequiredService<SettingsService>()));

        // Widget de sessions Claude Code : moniteur des fichiers d'état (écrits par le mode --hook),
        // installateur des hooks, contrôleur du panneau flottant.
        services.AddSingleton(_ => new ArchiveStore());

        // Hystérésis (phase 14, réduite en phase 21) : magasin RÉVERSIBLE des sessions traitées + détecteur
        // STATEFUL. Déclarés AVANT le SessionMonitor qui les consomme. L'acquittement par focus est tombé
        // avec la source app-bureau : il exigeait une origine qu'aucune session Claude Code ne porte (SRC-01).
        services.AddSingleton(_ => new TreatedStore());
        services.AddSingleton(sp => new SessionTreatmentTracker(sp.GetRequiredService<TreatedStore>()));

        services.AddSingleton(sp => new SessionMonitor(null, null, sp.GetRequiredService<ArchiveStore>(),
            sp.GetRequiredService<TreatedStore>(),
            sp.GetRequiredService<SessionTreatmentTracker>()));

        services.AddSingleton(_ => new SessionHookInstaller());

        // PUR-03 : réconciliation de ~/.claude/settings.json au démarrage (purge des entrées fantômes des
        // versions révolues + repointage de la barre). Chemins par défaut = profil utilisateur ; les tests
        // injectent systématiquement des chemins temp.
        services.AddSingleton(_ => new ClaudeSettingsReconciler());

        services.AddSingleton<ISessionsController>(sp => new Views.SessionsController(
            sp.GetRequiredService<SessionHookInstaller>(),
            sp.GetRequiredService<SettingsService>(),
            sp.GetRequiredService<SessionMonitor>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<ArchiveStore>()));

        // Pipeline de donnees : chaine de sources EXACTES uniquement, exposee comme IUsageProvider
        // et coiffee du decorateur de persistance. Plus aucun repli estime (EXA-04).
        // Chemins via Environment (jamais Assembly.Location, mono-fichier).
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(ChronosPaths.Default());
        services.AddSingleton<ClaudeUsageObjectProvider>();

        // DEL-01/DEL-02 : les transcripts ne repondent plus qu'a deux questions bornees (activite
        // depuis T ? tokens depuis T ?) — ils ne sont PLUS un IUsageProvider et sont donc HORS de la
        // chaine composite. Plus aucune dependance a SettingsService : ni plafond, ni ancre hebdo.
        // Phase 19 — MEMOISATION : une passe reelle coute 2,7 a 3,2 s et lit 536 Mo sur cette machine.
        // Le journal rendu est PUR et interrogeable N fois, donc le reutiliser pendant sa duree de
        // validite est gratuit et sans perte. Le decorateur est ENREGISTRE ici et pas seulement ecrit :
        // un decorateur que le graphe n'instancie jamais ne memoise rien (precedent 17-03).
        services.AddSingleton<ITranscriptActivitySource>(sp => new SourceActiviteMemoisee(
            new TranscriptActivityProvider(
                sp.GetRequiredService<ChronosPaths>(),
                sp.GetRequiredService<IClock>()),
            sp.GetRequiredService<IClock>()));

        // EXA-01 : magasin persistant du dernier releve exact (%APPDATA%\Chronos\last-exact.json).
        services.AddSingleton(sp => new LastExactStore(
            sp.GetRequiredService<ChronosPaths>().LastExactFile));

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

        // v2.1 : SOURCE EXACTE PRIMAIRE = login OAuth propre à Chronos (jeton obtenu par login
        // navigateur, stocké chiffré DPAPI). Marche que l'utilisateur soit en app bureau OU terminal.
        services.AddSingleton<ChronosOAuthStore>(_ => new ChronosOAuthStore());
        services.AddSingleton(sp => new ChronosOAuthClient(new HttpClient(), sp.GetRequiredService<IClock>()));

        // TOK-01/TOK-02 — AUTORITÉ UNIQUE DE JETON. Un seul objet du processus a le droit de
        // rafraîchir et d'écrire dans le coffre. Deux rafraîchisseurs présenteraient le même refresh
        // token et le second récolterait un invalid_grant : une FAUSSE déconnexion sur un compte sain
        // (bug documenté anthropics/claude-code#25609). Enregistrée UNE fois et réexposée comme
        // IAuthStatus sur la MÊME instance — surtout pas une seconde autorité.
        services.AddSingleton(sp => new ChronosTokenAuthority(
            sp.GetRequiredService<ChronosOAuthStore>(),
            sp.GetRequiredService<ChronosOAuthClient>(),
            sp.GetRequiredService<IClock>()));
        services.AddSingleton<IAuthStatus>(sp => sp.GetRequiredService<ChronosTokenAuthority>());

        // TOK-01 — horloge de fond du jeton : tick 60 s, PREMIER TICK IMMÉDIAT. Même motif d'instance
        // unique réexposée en IHostedService que RefreshOrchestrator ci-dessous.
        services.AddSingleton(sp => new TokenRefreshService(sp.GetRequiredService<ChronosTokenAuthority>()));
        services.AddHostedService(sp => sp.GetRequiredService<TokenRefreshService>());

        // Le provider d'usage n'est plus qu'un CONSOMMATEUR de l'autorité : il ne connaît ni le coffre,
        // ni le refresh token, ni le client OAuth.
        services.AddSingleton(sp => new ChronosOAuthUsageProvider(
            sp.GetRequiredService<ChronosTokenAuthority>(),
            new HttpClient(),
            sp.GetRequiredService<IClock>()));

        // HDR-01/HDR-02 — SONDE D'EN-TÊTES : source exacte qui répond MÊME en 429, et seule source du statut
        // serveur et du dépassement. Simple CONSOMMATEUR de l'autorité de jeton, jamais un troisième
        // rafraîchisseur : ce serait la course de rotation du refresh token, donc une FAUSSE déconnexion sur
        // un compte sain (anthropics/claude-code#25609).
        // HDR-06 — la cadence est bornée par le provider lui-même (300 s), pas par sa position dans la chaîne :
        // le composite appelle les deux GetAsync sans court-circuit. L'interrupteur SondeEnTetesActivee est
        // relu FRAIS à chaque appel via SettingsService (motif GatedOAuthUsageProvider).
        services.AddSingleton(sp => new RateLimitHeaderUsageProvider(
            sp.GetRequiredService<ChronosTokenAuthority>(),
            new HttpClient(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<SettingsService>()));

        // HDR-04 — canal LATÉRAL du dépassement : la MÊME instance réexposée, jamais une seconde
        // (motif exact de ChronosTokenAuthority/IAuthStatus, ci-dessus). Une seconde sonde doublerait la
        // dépense de quota sans que rien ne le signale.
        services.AddSingleton<IEtatServeur>(sp => sp.GetRequiredService<RateLimitHeaderUsageProvider>());

        // Le login reste l'écrivain du PREMIER jeton : il parle au coffre et au client directement.
        services.AddSingleton<IOAuthLogin>(sp => new Views.OAuthLogin(
            sp.GetRequiredService<ChronosOAuthClient>(),
            sp.GetRequiredService<ChronosOAuthStore>()));

        // Chaîne exacte par imbrication, MEILLEURE source PAR FENÊTRE (composite) :
        //   sonde d'en-têtes → login OAuth Chronos → OAuth coffre app (gated) → pont statusLine.
        // LA SONDE EST EN PRIMAIRE, et c'est une contrainte mécanique, pas un goût : Best() ne retient le
        // fallback que s'il est STRICTEMENT plus fiable, et les deux produisent Exact. En fallback, la sonde
        // ne gagnerait JAMAIS tant que /api/oauth/usage répond — or son snapshot est le SEUL porteur du statut
        // serveur et du dépassement : HDR-03/HDR-04 seraient morts-nés à chaque tick nominal.
        // CompositeUsageProvider.cs n'est PAS modifié (c'est la phase 19) : on l'INSTANCIE, c'est tout.
        // Le decorateur EXA-01 reste en TETE, et il est desormais LA COUCHE DE DOCTRINE de la chaine
        // (phase 19) : la sonde herite gratuitement de la persistance du dernier releve exact, et c'est
        // cette couche — seule a detenir horloge, magasin ET source d'activite — qui statue sur l'age de
        // chaque fenetre, la re-habilite sans activite (DEL-03) ou la degrade en plancher marque (DEL-04).
        services.AddSingleton<IUsageProvider>(sp => new LastExactUsageProvider(
            inner: new CompositeUsageProvider(
                primary:  sp.GetRequiredService<RateLimitHeaderUsageProvider>(),
                fallback: new CompositeUsageProvider(
                    primary:  sp.GetRequiredService<ChronosOAuthUsageProvider>(),
                    fallback: new CompositeUsageProvider(
                        primary:  sp.GetRequiredService<GatedOAuthUsageProvider>(),
                        fallback: sp.GetRequiredService<ClaudeUsageObjectProvider>()))),
            store: sp.GetRequiredService<LastExactStore>(),
            clock: sp.GetRequiredService<IClock>(),
            activite: sp.GetRequiredService<ITranscriptActivitySource>()));

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
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<IAuthStatus>(),     // TOK-02 : le rapport dit l'état RÉEL
            sp.GetRequiredService<IEtatServeur>()));  // HDR-03/HDR-04 : le rapport nomme ce que la sonde reçoit
    }
}
