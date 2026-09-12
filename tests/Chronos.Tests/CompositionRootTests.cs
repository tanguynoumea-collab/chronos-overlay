using System.Windows.Threading;
using Chronos.Models;
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
[Collection("XAML WPF")]   // charge du BAML : serialise avec les autres classes XAML (voir XamlWpfCollection)
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
        // GARDE ANTI-ACCIDENT : ChronosPaths.Default() pointerait le VRAI %APPDATA%\Chronos, donc
        // le magasin ecrirait le vrai last-exact.json pendant la suite de tests. Un UsageFile en
        // dossier temp isole propage l'isolation a settings.json ET a last-exact.json (proprietes
        // calculees) — meme motif que Le_graphe_DI_resout_le_reconciliateur_de_settings_Claude.
        var tmpUsage = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ChronosDI_" + System.Guid.NewGuid().ToString("N"), "usage.json");
        services.AddSingleton(ChronosPaths.Default() with { UsageFile = tmpUsage });
        services.AddSingleton<ClaudeUsageObjectProvider>();

        // Source de delta (DEL-01/DEL-02) : enregistree HORS de la chaine composite — elle n'est
        // plus un IUsageProvider. Une DI oubliant cet enregistrement compilerait et ne planterait
        // qu'au demarrage de l'app.
        // Phase 19 : le conteneur miroir doit refleter le graphe REEL — la source d'activite est
        // MEMOISEE en production. Un miroir non memoise ne garderait pas ce que la production fait.
        services.AddSingleton<ITranscriptActivitySource>(sp => new SourceActiviteMemoisee(
            new TranscriptActivityProvider(
                sp.GetRequiredService<ChronosPaths>(),
                sp.GetRequiredService<IClock>()),
            sp.GetRequiredService<IClock>()));

        // EXA-01 : le decorateur de persistance coiffe la chaine exacte (ici reduite au pont
        // statusLine, seul maillon reproduit dans ce conteneur miroir).
        services.AddSingleton(sp => new LastExactStore(
            sp.GetRequiredService<ChronosPaths>().LastExactFile));
        services.AddSingleton<IUsageProvider>(sp => new LastExactUsageProvider(
            inner: sp.GetRequiredService<ClaudeUsageObjectProvider>(),
            store: sp.GetRequiredService<LastExactStore>(),
            clock: sp.GetRequiredService<IClock>(),
            activite: sp.GetRequiredService<ITranscriptActivitySource>()));
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
        // Phase 17 : le ctor de MainViewModel dépendra d'IAuthStatus (plan 17-05). L'enregistrer dès
        // maintenant évite une rupture de garde au plan suivant ; le faux suffit ici.
        services.AddSingleton<IAuthStatus>(_ => new FakeAuthStatus());
        // Widget de sessions : le ctor de MainViewModel dépend d'ISessionsController.
        services.AddSingleton<ISessionsController>(_ => new FakeSessionsController());

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MarqueurDisposable>();   // marqueur pour prouver la disposition

        var provider = services.BuildServiceProvider();

        // Résolution sans exception → preuve que le graphe DI est câblé (partie « lance »).
        Assert.NotNull(provider.GetRequiredService<MainWindow>());
        Assert.NotNull(provider.GetRequiredService<MainViewModel>());

        // EXA-01 : le décorateur de persistance est bien en TÊTE de chaîne, pas enterré au milieu.
        Assert.NotNull(provider.GetRequiredService<IUsageProvider>());
        Assert.IsType<LastExactUsageProvider>(provider.GetRequiredService<IUsageProvider>());
        // DEL-01/DEL-02 : la source de delta se résout, hors de la chaîne d'usage.
        Assert.NotNull(provider.GetRequiredService<ITranscriptActivitySource>());
        // Garde anti-accident : aucun test n'écrit dans le vrai %APPDATA%\Chronos.
        Assert.StartsWith(System.IO.Path.GetTempPath(), provider.GetRequiredService<LastExactStore>().Path);

        var marqueur = provider.GetRequiredService<MarqueurDisposable>();
        Assert.False(marqueur.Disposed);

        // Disposition du conteneur → dispose les Singletons IDisposable (partie « ferme proprement »).
        provider.Dispose();
        Assert.True(marqueur.Disposed);
    }

    /// <summary>
    /// GARDE DI RÉELLE (Phase 13, étendue Phase 14, réduite Phase 21) — au-delà de `dotnet build`. Le
    /// conteneur miroir de <see cref="Host_resout_et_dispose_les_singletons"/> n'inclut PAS la chaîne de
    /// sessions : une DI mal ordonnée (<c>SessionMonitor</c> résolu avant ses dépendances) ou un service
    /// manquant COMPILERAIT et ne planterait qu'au DÉMARRAGE de l'app, pas au build. Ce test enregistre
    /// EXACTEMENT la chaîne telle qu'elle est câblée dans App.xaml.cs et prouve qu'elle se résout sans
    /// exception (= graphe câblé dans le bon ordre). [Fact] simple : aucun besoin de STA/WPF.
    ///
    /// Phase 21 : la sous-chaîne app-bureau (phases 13-14) a été retirée du graphe de production ; la garde
    /// couvre désormais ce qui subsiste — <c>ArchiveStore</c>, <c>TreatedStore</c>,
    /// <c>SessionTreatmentTracker</c> et <c>SessionMonitor</c>.
    /// </summary>
    [Fact]
    public void Le_graphe_DI_resout_la_chaine_de_sessions()
    {
        var services = new ServiceCollection();

        // Chaîne de sessions EXACTEMENT comme dans App.xaml.cs (ArchiveStore + hystérésis + moniteur).
        services.AddSingleton(_ => new ArchiveStore(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChronosArch_" + System.Guid.NewGuid().ToString("N") + ".json")));
        services.AddSingleton(_ => new TreatedStore(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChronosTreated_" + System.Guid.NewGuid().ToString("N") + ".json")));
        services.AddSingleton(sp => new SessionTreatmentTracker(sp.GetRequiredService<TreatedStore>()));
        services.AddSingleton(sp => new SessionMonitor(null, null, sp.GetRequiredService<ArchiveStore>(),
            sp.GetRequiredService<TreatedStore>(),
            sp.GetRequiredService<SessionTreatmentTracker>()));

        var provider = services.BuildServiceProvider();

        // Résolution sans exception = graphe câblé dans le BON ORDRE (aucun service manquant/mal ordonné).
        Assert.NotNull(provider.GetRequiredService<SessionMonitor>());

        // OBS-01 — le partage d'instance repose entièrement sur la portée : passer ce moniteur en transient
        // donnerait au diagnostic un exemplaire distinct de celui du widget, avec ses propres magasins
        // rechargés et son propre détecteur sans mémoire. Le rapport redeviendrait faux, sans rien casser
        // au build ni au démarrage.
        Assert.Same(provider.GetRequiredService<SessionMonitor>(), provider.GetRequiredService<SessionMonitor>());

        provider.Dispose();
    }

    /// <summary>
    /// GARDE DI RÉELLE (Phase 15, PUR-03). Attrape un enregistrement manquant de
    /// <see cref="ClaudeSettingsReconciler"/> qui COMPILERAIT — <c>App.OnStartup</c> l'invoque par
    /// <c>GetRequiredService</c> — mais ferait échouer la résolution au DÉMARRAGE de l'app, pas au build.
    ///
    /// <para>Reproduit la sous-chaîne exacte d'<c>App.xaml.cs</c> avec des chemins TEMP : aucune pollution
    /// du vrai profil de l'utilisateur, ni de son <c>~/.claude/settings.json</c>, ni de ses sauvegardes.</para>
    /// </summary>
    [Fact]
    public void Le_graphe_DI_resout_le_reconciliateur_de_settings_Claude()
    {
        var services = new ServiceCollection();
        var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChronosDI_" + System.Guid.NewGuid().ToString("N"));
        services.AddSingleton(_ => new SessionHookInstaller(System.IO.Path.Combine(tmp, "settings.json")));
        services.AddSingleton<StatusLineInstaller>(_ => new StatusLineInstaller(System.IO.Path.Combine(tmp, "settings.json")));
        services.AddSingleton(_ => new ClaudeSettingsReconciler(
            System.IO.Path.Combine(tmp, "settings.json"), System.IO.Path.Combine(tmp, "backups")));

        var provider = services.BuildServiceProvider();
        var recon = provider.GetRequiredService<ClaudeSettingsReconciler>();
        Assert.NotNull(recon);
        Assert.StartsWith(System.IO.Path.GetTempPath(), recon.SettingsPath);   // garde anti-accident
        Assert.False(recon.Reconcile(hooksWanted: true));   // fichier absent → aucune écriture, aucun crash
        provider.Dispose();
    }

    /// <summary>
    /// GARDE DI RÉELLE (phase 17). Reproduit la sous-chaîne OAuth d'App.ConfigureServices : coffre
    /// (sur chemin TEMPORAIRE), client, autorité unique, service de fond hébergé, provider consommateur.
    /// Prouve trois choses qu'un <c>dotnet build</c> ne prouve pas : que le graphe se résout, que
    /// <see cref="ChronosTokenAuthority"/> et <see cref="IAuthStatus"/> sont bien LA MÊME instance (une
    /// seconde autorité rouvrirait la course de rotation du refresh token, donc la fausse déconnexion
    /// du bug claude-code#25609), et que le service de fond est bien enregistré comme
    /// <c>IHostedService</c> — un <see cref="TokenRefreshService"/> que le host ne démarre jamais ne
    /// rafraîchit rien du tout, et TOK-01 resterait lettre morte sans que rien ne le signale.
    ///
    /// SÉCURITÉ : le coffre pointe dans <c>Path.GetTempPath()</c> et ce test n'appelle JAMAIS
    /// <c>GetAccessTokenAsync</c> — aucune requête ne part, le refresh token réel de l'utilisateur
    /// n'est ni lu, ni déchiffré, ni présenté au serveur (la rotation le tuerait définitivement).
    ///
    /// <para>Phase 18 (HDR-01/HDR-04) : la garde attrape EN PLUS une seconde instance de sonde. Un
    /// <c>AddSingleton&lt;IEtatServeur&gt;(_ =&gt; new RateLimitHeaderUsageProvider(...))</c> compilerait,
    /// résoudrait, et dépenserait silencieusement DEUX fois le quota de l'utilisateur — 576 requêtes par
    /// jour au lieu de 288 — tout en publiant un dépassement sur une instance que plus personne n'écoute.
    /// Seul <c>Assert.Same</c> le voit.</para>
    /// </summary>
    [Fact]
    public void Le_graphe_DI_resout_l_autorite_de_jeton_et_son_service_de_fond()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChronosDiTest_" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);

        var services = new ServiceCollection();
        services.AddSingleton<IClock>(_ => new SystemClock());
        services.AddSingleton(_ => new ChronosOAuthStore(System.IO.Path.Combine(dir, "oauth.dat")));
        services.AddSingleton(sp => new ChronosOAuthClient(new System.Net.Http.HttpClient(), sp.GetRequiredService<IClock>()));
        services.AddSingleton(sp => new ChronosTokenAuthority(
            sp.GetRequiredService<ChronosOAuthStore>(),
            sp.GetRequiredService<ChronosOAuthClient>(),
            sp.GetRequiredService<IClock>()));
        services.AddSingleton<IAuthStatus>(sp => sp.GetRequiredService<ChronosTokenAuthority>());
        services.AddSingleton(sp => new TokenRefreshService(sp.GetRequiredService<ChronosTokenAuthority>()));
        services.AddHostedService(sp => sp.GetRequiredService<TokenRefreshService>());
        services.AddSingleton(sp => new ChronosOAuthUsageProvider(
            sp.GetRequiredService<ChronosTokenAuthority>(),
            new System.Net.Http.HttpClient(),
            sp.GetRequiredService<IClock>()));

        // Phase 18 — la sonde d'en-têtes et son canal latéral, EXACTEMENT comme dans App.xaml.cs.
        // SettingsService sur chemin TEMPORAIRE (ChronosPaths.SettingsFile est colocalisé avec UsageFile) :
        // la sonde relit son interrupteur à chaque appel, donc elle connaît le service de réglages.
        services.AddSingleton(_ => new ChronosPaths(
            System.IO.Path.Combine(dir, "usage.json"), System.IO.Path.Combine(dir, "projects")));
        services.AddSingleton<SettingsService>();
        services.AddSingleton(sp => new RateLimitHeaderUsageProvider(
            sp.GetRequiredService<ChronosTokenAuthority>(),
            new System.Net.Http.HttpClient(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<SettingsService>()));
        services.AddSingleton<IEtatServeur>(sp => sp.GetRequiredService<RateLimitHeaderUsageProvider>());

        using var provider = services.BuildServiceProvider();

        // Garde anti-accident : aucun test n'écrit dans le vrai %APPDATA%\Chronos.
        Assert.StartsWith(System.IO.Path.GetTempPath(), provider.GetRequiredService<ChronosOAuthStore>().Path);
        Assert.StartsWith(System.IO.Path.GetTempPath(), provider.GetRequiredService<ChronosPaths>().SettingsFile);

        var autorite = provider.GetRequiredService<ChronosTokenAuthority>();
        Assert.NotNull(autorite);
        Assert.Same(autorite, provider.GetRequiredService<IAuthStatus>());   // UNE seule autorité
        Assert.NotNull(provider.GetRequiredService<ChronosOAuthUsageProvider>());
        Assert.Contains(provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>(), s => s is TokenRefreshService);

        var sonde = provider.GetRequiredService<RateLimitHeaderUsageProvider>();
        Assert.NotNull(sonde);
        Assert.Same(sonde, provider.GetRequiredService<IEtatServeur>());   // UNE seule sonde, jamais deux
    }

    /// <summary>
    /// GARDE DE POSITION (HDR-01/HDR-02/HDR-03/HDR-04). Un `dotnet build` ne voit pas si la sonde est en
    /// primaire ou en fallback, et les champs du composite sont privés : la réflexion ne peut rien prouver.
    /// Ce test le prouve par le RÉSULTAT — deux sources produisent des chiffres DIFFÉRENTS et l'on vérifie
    /// que ce sont ceux de la SONDE qui sortent. Si quelqu'un inversait primary/fallback, Best() (qui ne
    /// retient le fallback que s'il est STRICTEMENT plus fiable, et les deux sont Exact) rendrait les
    /// chiffres de /api/oauth/usage et ce test tomberait — avec lui, le statut serveur et le dépassement.
    ///
    /// Portée ASSUMÉE : ce test prouve la POSITION, pas la résolution du graphe complet (déjà couverte par
    /// Host_resout_et_dispose_les_singletons). L'innermost fallback est donc un FakeUsageProvider.
    ///
    /// SÉCURITÉ : coffre et magasin sous Path.GetTempPath(), tout le trafic par FakeHttpMessageHandler.
    /// Aucune requête réelle, le refresh token de l'utilisateur n'est ni lu, ni déchiffré, ni envoyé.
    /// </summary>
    [Fact]
    public async Task La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChronosDiPos_" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var horloge = new FakeClock(new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero));

        var services = new ServiceCollection();
        services.AddSingleton<IClock>(_ => horloge);
        services.AddSingleton(_ => new ChronosPaths(
            System.IO.Path.Combine(dir, "usage.json"), System.IO.Path.Combine(dir, "projects")));
        services.AddSingleton<SettingsService>();

        // Jeton VALIDE et NON EXPIRÉ : aucun rafraîchissement n'est tenté, donc le point de terminaison
        // de jeton n'est jamais sollicité (son transport reste un faux, par sécurité redondante).
        services.AddSingleton(_ => new ChronosOAuthStore(System.IO.Path.Combine(dir, "oauth.dat")));
        services.AddSingleton(sp => new ChronosOAuthClient(
            new System.Net.Http.HttpClient(FakeHttpMessageHandler.Json(
                System.Net.HttpStatusCode.OK, """{"access_token":"NEUF","refresh_token":"NEUF","expires_in":3600}""")),
            sp.GetRequiredService<IClock>()));
        services.AddSingleton(sp => new ChronosTokenAuthority(
            sp.GetRequiredService<ChronosOAuthStore>(),
            sp.GetRequiredService<ChronosOAuthClient>(),
            sp.GetRequiredService<IClock>()));

        // LA SONDE : jeu nominal → 5 h = 0,01 et statut serveur « allowed ».
        services.AddSingleton(sp => new RateLimitHeaderUsageProvider(
            sp.GetRequiredService<ChronosTokenAuthority>(),
            new System.Net.Http.HttpClient(FakeHttpMessageHandler.AvecEnTetes(
                System.Net.HttpStatusCode.OK, EnTetesDeReference.Nominal)),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<SettingsService>()));
        services.AddSingleton<IEtatServeur>(sp => sp.GetRequiredService<RateLimitHeaderUsageProvider>());

        // /api/oauth/usage : même fiabilité (Exact), chiffres DIFFÉRENTS → 5 h = 0,42.
        services.AddSingleton(sp => new ChronosOAuthUsageProvider(
            sp.GetRequiredService<ChronosTokenAuthority>(),
            new System.Net.Http.HttpClient(FakeHttpMessageHandler.Json(
                System.Net.HttpStatusCode.OK,
                """{"five_hour":{"utilization":42,"resets_at":"2026-09-09T18:30:00+00:00"},"seven_day":{"utilization":63,"resets_at":"2026-09-14T09:00:00+00:00"}}""")),
            sp.GetRequiredService<IClock>()));

        services.AddSingleton(sp => new LastExactStore(sp.GetRequiredService<ChronosPaths>().LastExactFile));
        services.AddSingleton<IUsageProvider>(sp => new LastExactUsageProvider(
            inner: new CompositeUsageProvider(
                primary:  sp.GetRequiredService<RateLimitHeaderUsageProvider>(),
                fallback: new CompositeUsageProvider(
                    primary:  sp.GetRequiredService<ChronosOAuthUsageProvider>(),
                    fallback: new FakeUsageProvider())),
            store: sp.GetRequiredService<LastExactStore>(),
            clock: sp.GetRequiredService<IClock>(),
            // La sonde pose CapturedAt = now et FakeClock est figee : l'age vaut zero, donc la doctrine
            // reste en branche 1 (laissez-passer) et ne consulte JAMAIS cette source. Elle est presente
            // parce que le 4e argument est obligatoire, pas parce que ce test en depend.
            activite: new FakeTranscriptActivitySource()));

        using var provider = services.BuildServiceProvider();

        // Gardes anti-accident : ni le coffre ni le magasin ne touchent le vrai %APPDATA%\Chronos.
        Assert.StartsWith(System.IO.Path.GetTempPath(), provider.GetRequiredService<ChronosOAuthStore>().Path);
        Assert.StartsWith(System.IO.Path.GetTempPath(), provider.GetRequiredService<LastExactStore>().Path);

        // Le coffre reçoit un jeton vivant AVANT toute résolution de la chaîne.
        provider.GetRequiredService<ChronosOAuthStore>()
            .Save(new OAuthTokens("ACC-VALIDE", "REF-VALIDE", horloge.UtcNow.AddHours(2)));
        provider.GetRequiredService<SettingsService>().Save(new ChronosSettings { SondeEnTetesActivee = true });

        // Le décorateur de persistance reste en TÊTE (même exigence qu'à la ligne 108).
        Assert.IsType<LastExactUsageProvider>(provider.GetRequiredService<IUsageProvider>());

        var snap = await provider.GetRequiredService<IUsageProvider>().GetAsync();

        // LA PREUVE : 0,01 vient de la sonde, 0,42 viendrait de /api/oauth/usage. Les deux sont Exact,
        // donc seule la POSITION décide — inverser primary/fallback ferait sortir 0,42 ici.
        Assert.Equal(SourceReliability.Exact, snap.FiveHour.Reliability);
        Assert.Equal(0.01, snap.FiveHour.Utilization!.Value, 9);

        // Et le statut serveur traverse réellement les deux composites imbriqués ET le décorateur :
        // c'est ce voyage par référence qui rend HDR-03/HDR-04 vivants en production.
        Assert.Equal(StatutServeur.Autorise, snap.FiveHour.StatutServeur);
    }
}
