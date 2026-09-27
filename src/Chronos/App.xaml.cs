using System.Net.Http;
using System.Windows;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Services.Historique.Tokens;
using Chronos.ViewModels;
using Chronos.ViewModels.Historique;
using Chronos.Views;
using Chronos.Views.Historique;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Chronos;

public partial class App : Application
{
    private IHost? _host;                    // null tant que le Host n'est pas construit (seconde instance retirée avant lui, CPT-03)
    private static ResultatVerrou? _verrou;   // STATIQUE (D-32-13) : le GC ne doit jamais libérer le mutex pendant la vie de l'overlay

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
            Environment.Exit(RunSessionHook(hookIdx + 1 < e.Args.Length ? e.Args[hookIdx + 1] : null));
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

        // MODE GALERIE HISTORIQUE (--historique) : la fenêtre Historique RÉELLE sur la semaine de référence des maquettes (phase 34,
        // D-34-26). Court-circuite le pipeline/host et le verrou d'instance unique comme --cadrans / --sessions : aucune réconciliation
        // des hooks, aucun service résolu, aucune écriture dans settings.json (réglages en mémoire). Sert à la revue visuelle DESIGN_PLAN §8.
        if (e.Args.Any(a => string.Equals(a, "--historique", StringComparison.OrdinalIgnoreCase)))
        {
            base.OnStartup(e);
            var galerie = HistoriqueGalerie.Creer();
            MainWindow = galerie;
            galerie.Show();
            return;
        }

        base.OnStartup(e);

        // CPT-03 — UNE SEULE INSTANCE de l'overlay par session Windows. Posé ici, APRÈS les court-circuits --statusline, --hook,
        // --cadrans, --sessions et --historique (multi-instances par construction : Claude Code lance jusqu'à 5 hooks en parallèle) et AVANT le Host :
        // le second exe n'a démarré aucun service, n'a pas réconcilié ~/.claude/settings.json et n'a pas écrasé chronos.log.
        // Il se retire en le DISANT et ne tue jamais l'autre : le 2026-09-27, trois exe tournaient ensemble et écrivaient les mêmes fichiers.
        // Mutex nommé Local\ (pas Global\ : aucun droit, un autre utilisateur a le sien) ; un abandon (instance morte sans libérer) = acquis.
        var verrou = VerrouInstanceUnique.Acquerir(VerrouInstanceUnique.NomOverlay);
        if (!verrou.Obtenu)
        {
            MessageBox.Show("Chronos tourne déjà (une seule instance à la fois). Cette copie se retire ; l'autre continue.\n\n"
                            + "Pour changer de version : clic droit sur le cadran → réglages → « Quitter Chronos », puis relance.",
                            "Chronos", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        _verrou = verrou;

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

        // SRC-02 — les identifiants synthétiques de l'ancienne source app-bureau (préfixe « desktop: »)
        // sont retirés DU FICHIER archived.json, pas seulement ignorés à la lecture. Mesuré le 2026-09-12 :
        // archived.json ne contenait QUE deux de ces entrées, datées de juillet. Elles prouvent que
        // l'utilisateur avait dû les archiver À LA MAIN — leur horodatage était rafraîchi à chaque poll,
        // donc elles ne vieillissaient jamais et ne pouvaient jamais expirer. Les laisser dans le fichier,
        // c'est laisser son contournement gravé dans ses données.
        //
        // Même régime que la réconciliation ci-dessus : mode OVERLAY uniquement (les modes --hook et
        // --statusline sortent bien plus haut, l. 20 et 30), best-effort et silencieux — ne peut pas
        // empêcher le démarrage. Idempotent : une fois le fichier propre, l'appel suivant ne réécrit rien.
        try { _host.Services.GetRequiredService<ArchiveStore>().PurgerPrefixe("desktop:"); }
        catch { }

        // CYC-01 — le magasin d'états de session est balayé une fois par lancement. Même régime que la purge
        // ci-dessus : mode OVERLAY uniquement (les modes --hook et --statusline sortent bien plus haut),
        // best-effort et silencieux, il ne peut pas empêcher le démarrage. Mesuré le 2026-09-12 : 54 états,
        // dont 48 de plus de sept jours, plus 12 fichiers temporaires abandonnés.
        //
        // Expirer, c'est ne plus savoir : ce qui est balayé disparaît, rien n'est déclaré terminé ni traité.
        try { _host.Services.GetRequiredService<BalayageMagasinSessions>().Balayer(); }
        catch { }

        // Première exécution : proposer d'activer la SOURCE EXACTE (pont statusLine Claude Code).
        // Une seule fois (StatusLinePromptDismissed), non bloquant pour le rendu de l'overlay.
        _host.Services.GetRequiredService<IStatusLineSetup>().OfferOnFirstRun();

        // Widget de sessions : réafficher le panneau s'il était activé.
        _host.Services.GetRequiredService<ISessionsController>().ShowIfEnabled();
    }

    // Exécuté en mode --hook : lit le JSON stdin de Claude Code et applique l'ordre au fichier d'état de la
    // session dans %APPDATA%\Chronos\sessions\<id>.json. Rend le CODE DE SORTIE du processus.
    //
    // CYC-02 — deux changements de fond par rapport à la version d'origine.
    // (a) L'écriture est sortie d'ici : elle vit dans Services/EcritureEtatSession, donc elle est testable.
    //     Ce fichier-ci ne l'est pas — il monte WPF — et c'est exactement pourquoi le défaut y a survécu.
    // (b) L'échec n'est plus avalé. Contrat des hooks Claude Code : 0 = succès, 2 = erreur BLOQUANTE
    //     (stderr renvoyé à Claude, action bloquée), tout autre code = erreur NON bloquante (stderr montré
    //     à l'utilisateur, la session continue). On sort donc 1, JAMAIS 2 : un hook ne doit pas casser la
    //     session Claude Code, mais il n'a aucune raison de mentir sur ce qu'il n'a pas pu faire.
    private static int RunSessionHook(string? eventName)
    {
        try
        {
            var utf8 = new System.Text.UTF8Encoding(false);
            string input;
            using (var sr = new System.IO.StreamReader(Console.OpenStandardInput(), utf8))
                input = sr.ReadToEnd();

            var res = SessionHookProcessor.Process(eventName, input, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            // Lancé sous l'app bureau, ce chemin atterrit dans la vue virtualisée du paquet — l'overlay la lit par
            // RacinesEtat (APP-06).
            var dossier = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chronos", "sessions");

            var ecriture = EcritureEtatSession.Appliquer(dossier, res);
            if (ecriture.Reussi) return 0;

            SignalerSurErreurStandard($"Chronos --hook {eventName} : état de session non écrit — {ecriture.Cause}");
            return 1;
        }
        catch (Exception ex)
        {
            SignalerSurErreurStandard($"Chronos --hook {eventName} : {ex.GetType().Name} — {ex.Message}");
            return 1;
        }
    }

    // Écrit une ligne sur le flux d'erreur du processus en UTF-8 STRICT. Pas via Console.Error : son
    // encodage OEM par défaut mutilerait les accents, exactement la leçon déjà tirée pour la barre de
    // statut (RunStatusLineBridge). Ne lève jamais : signaler un échec ne doit pas en produire un second.
    private static void SignalerSurErreurStandard(string message)
    {
        try
        {
            var utf8 = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var octets = utf8.GetBytes(message + Environment.NewLine);
            using var flux = Console.OpenStandardError();
            flux.Write(octets, 0, octets.Length);
            flux.Flush();
        }
        catch { }
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
        // Blocage volontaire : dispose déterministe des Singletons IDisposable (évite le piège async-void qui n'attend pas StopAsync).
        // CPT-03 : le Host peut n'avoir JAMAIS été construit (seconde instance retirée avant lui) — _host est alors null.
        if (_host is not null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }
        _verrou?.Liberer();   // sur le thread UI, celui qui a acquis (ReleaseMutex l'exige) ; l'OS le ferait à la mort du processus, on le fait proprement
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IUiDispatcher>(_ => new WpfUiDispatcher(Current.Dispatcher));
        services.AddSingleton<TopmostGuard>();
        // Le VM est construit par le conteneur : ses paramètres optionnels IEtatJournal (32-05) et IEtatReconstruction (33-05, TOK-02)
        // sont injectés parce qu'ils sont inscrits plus bas — mêmes instances que le journal et que le service hébergé de reconstruction.
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        // Fenêtre Historique (phase 34, HIS-01) : façade de lecture neutre sur %APPDATA%\Chronos\historique (ChronosPaths), réglages via
        // SettingsService (motif GAP-1 : Save(mutation(Load()))), fuseau du système au site de composition (D-32-29 / D-33-21). Le VM est
        // singleton (D-34-05) : un seul abonnement à IEtatReconstruction.Changement, état de navigation conservé entre deux ouvertures ;
        // IEtatJournal / IEtatReconstruction lui sont injectés comme paramètres optionnels (précédent 32-05 / 33-05 : inscrits plus bas).
        // La fenêtre elle-même est construite par la phase 35 (gestes d'ouverture) : new HistoriqueWindow(sp.GetRequiredService<HistoriqueViewModel>()).
        services.AddSingleton(TimeZoneInfo.Local);
        services.AddSingleton<ISourceHistorique>(sp => new SourceHistoriqueDisque(sp.GetRequiredService<ChronosPaths>(), sp.GetRequiredService<TimeZoneInfo>()));
        services.AddSingleton<IReglagesHistorique>(sp => new ReglagesHistoriqueSurDisque(sp.GetRequiredService<SettingsService>()));
        services.AddSingleton<HistoriqueViewModel>();

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
        services.AddSingleton(sp => new ArchiveStore(null, sp.GetRequiredService<IClock>()));

        // Hystérésis (phase 14, réduite en phase 21) : magasin RÉVERSIBLE des sessions traitées + détecteur
        // STATEFUL. Déclarés AVANT le SessionMonitor qui les consomme. L'acquittement par focus est tombé
        // avec la source app-bureau : il exigeait une origine qu'aucune session Claude Code ne porte (SRC-01).
        services.AddSingleton(sp => new TreatedStore(null, sp.GetRequiredService<IClock>()));
        services.AddSingleton(sp => new SessionTreatmentTracker(sp.GetRequiredService<TreatedStore>()));

        // APP-06 — racines des fichiers d'état ET de l'app bureau, résolues UNE fois par candidats (paquet MSIX d'abord).
        // L'overlay, lancé hors de l'arbre de l'app bureau, ne voit pas la vue virtualisée où les hooks lancés sous
        // l'app écrivent : sans ces racines, le widget n'a jamais lu un fichier de hook d'une session de l'app.
        services.AddSingleton(_ => RacinesEtat.ParDefaut());

        // APP-01 — métadonnées par session de l'app bureau : LECTURE SEULE, singleton (son cache vit avec l'app, et le rapport
        // lit la MÊME instance que le widget, par le moniteur — OBS-01).
        services.AddSingleton(sp => new LecteurAppBureau(sp.GetRequiredService<RacinesCandidates>().SessionsAppBureau));

        // LUE-02 — ce que l'OS a au premier plan : le PROCESSUS de la fenêtre (jamais son titre, jamais l'UI Automation retirée en phase 21).
        // Singleton : son état « depuis » vit avec l'app, et le rapport lit ce que le moniteur en a vu (OBS-01).
        services.AddSingleton<IPremierPlan>(_ => new PremierPlanWin32());

        services.AddSingleton(sp => new SessionMonitor(null, null, sp.GetRequiredService<ArchiveStore>(),
            sp.GetRequiredService<TreatedStore>(),
            sp.GetRequiredService<SessionTreatmentTracker>(),
            dossiersEtat: sp.GetRequiredService<RacinesCandidates>().EtatsHooks,
            appBureau: sp.GetRequiredService<LecteurAppBureau>(),
            premierPlan: sp.GetRequiredService<IPremierPlan>()));

        // CYC-01 — balayage du magasin d'états au démarrage. Les racines balayées sont celles DU MONITEUR du
        // widget, toutes (vue du paquet de l'app bureau ET vue réelle), jamais un second chemin déduit : deux
        // listes pour un seul widget rouvriraient l'écart que la phase 22 a fermé, et un balayage qui se
        // tromperait de dossier effacerait les fichiers de quelqu'un d'autre — la racine des métadonnées de l'app
        // bureau n'est jamais une racine d'état. L'attestation de vie vient des transcripts — c'est ce qui permet
        // à une session vivante depuis plusieurs jours de survivre au nettoyage.
        services.AddSingleton(sp => new BalayageMagasinSessions(
            sp.GetRequiredService<SessionMonitor>().Dossiers,
            new TranscriptSessionSource(),
            sp.GetRequiredService<IClock>()));

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
            sp.GetRequiredService<ArchiveStore>(),
            sp.GetRequiredService<TreatedStore>()));

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

        // JRN-01/JRN-03 — le journal des relevés exacts : dossier %APPDATA%\Chronos\historique (ChronosPaths, jamais en dur), horloge injectée.
        services.AddSingleton(sp => new JournalReleves(sp.GetRequiredService<ChronosPaths>().HistoriqueDir, sp.GetRequiredService<IClock>()));
        services.AddSingleton<IEtatJournal>(sp => sp.GetRequiredService<JournalReleves>());   // le VM et les réglages ne voient que l'âge (JRN-04)

        // Chaîne exacte par imbrication, MEILLEURE source PAR FENÊTRE (composite) :
        //   sonde d'en-têtes → login OAuth Chronos → OAuth coffre app (gated) → pont statusLine.
        // LA SONDE EST EN PRIMAIRE, et c'est une contrainte mécanique, pas un goût : Best() ne retient le
        // fallback que s'il est STRICTEMENT plus fiable, et les deux produisent Exact. En fallback, la sonde
        // ne gagnerait JAMAIS tant que /api/oauth/usage répond — or son snapshot est le SEUL porteur du statut
        // serveur et du dépassement : HDR-03/HDR-04 seraient morts-nés à chaque tick nominal.
        // CompositeUsageProvider.cs n'est PAS modifié (c'est la phase 19) : on l'INSTANCIE, c'est tout.
        //
        // JRN-01/JRN-02 — le décorateur de journalisation, ENTRE la tête (LastExactUsageProvider, doctrine) et le composite : il voit l'inner BRUT,
        // donc jamais le magasin ni un plancher. Hosted service inscrit AVANT RefreshOrchestrator : « demarrage » précède le premier relevé,
        // « arret » suit le dernier (ordre d'inscription au Start, inverse au Stop). Aucun appel réseau : il observe ce que la chaîne produit.
        services.AddSingleton(sp => new JournalisationUsageProvider(
            inner: new CompositeUsageProvider(
                primary:  sp.GetRequiredService<RateLimitHeaderUsageProvider>(),
                fallback: new CompositeUsageProvider(
                    primary:  sp.GetRequiredService<ChronosOAuthUsageProvider>(),
                    fallback: new CompositeUsageProvider(
                        primary:  sp.GetRequiredService<GatedOAuthUsageProvider>(),
                        fallback: sp.GetRequiredService<ClaudeUsageObjectProvider>()))),
            journal: sp.GetRequiredService<JournalReleves>(),
            etatServeur: sp.GetRequiredService<IEtatServeur>(),      // sonde_refusee (transition)
            authStatus: sp.GetRequiredService<IAuthStatus>(),        // jeton_invalide (transition vers Deconnecte)
            clock: sp.GetRequiredService<IClock>()));                // version : lue de l'assembly (null ici)
        services.AddHostedService(sp => sp.GetRequiredService<JournalisationUsageProvider>());

        // TOK-01..03 (phase 33) — les agrégats de tokens : magasin mensuel, index d'ids (mémoire d'idempotence) et reconstruction de fond.
        // Hosted service inscrit AVANT RefreshOrchestrator (démarrage dans l'ordre d'inscription, arrêt en ordre inverse) : le thread
        // BelowNormal reçoit l'annulation et fait son dernier flush AVANT que la tête ne s'arrête. Dossier = HistoriqueDir (à côté du
        // journal, jamais en dur) ; racine = ProjectsRoot, en LECTURE SEULE stricte. IndexMessages n'existait pas encore dans le graphe :
        // il est construit ici, explicitement, pour que la reconstruction et les tests partagent le même dossier.
        services.AddSingleton(sp => new MagasinAgregats(sp.GetRequiredService<ChronosPaths>().HistoriqueDir, sp.GetRequiredService<IClock>()));
        services.AddSingleton(sp => new IndexMessages(sp.GetRequiredService<ChronosPaths>().HistoriqueDir, sp.GetRequiredService<IClock>()));
        services.AddSingleton(sp => new ReconstructionTokens(sp.GetRequiredService<ChronosPaths>(), sp.GetRequiredService<MagasinAgregats>(),
                                                             sp.GetRequiredService<IndexMessages>(), sp.GetRequiredService<IClock>()));
        services.AddSingleton<IEtatReconstruction>(sp => sp.GetRequiredService<ReconstructionTokens>());   // le VM et le diagnostic ne voient que l'état (TOK-02)
        services.AddHostedService(sp => sp.GetRequiredService<ReconstructionTokens>());

        // Le decorateur EXA-01 reste en TETE, et il est desormais LA COUCHE DE DOCTRINE de la chaine
        // (phase 19) : la sonde herite gratuitement de la persistance du dernier releve exact, et c'est
        // cette couche — seule a detenir horloge, magasin ET source d'activite — qui statue sur l'age de
        // chaque fenetre, la re-habilite sans activite (DEL-03) ou la degrade en plancher marque (DEL-04).
        // Son inner est désormais le décorateur de journalisation (32-05), qui enveloppe lui-même le composite.
        services.AddSingleton<IUsageProvider>(sp =>
        {
            var store = sp.GetRequiredService<LastExactStore>();
            var journalisation = sp.GetRequiredService<JournalisationUsageProvider>();
            // CPT-02 — une écriture ratée du dernier exact n'est plus muette : elle devient une ligne « ecriture_ratee » du journal.
            store.EcritureRatee += (_, cause) => journalisation.SignalerEcritureRatee("last-exact", cause);
            return new LastExactUsageProvider(
                inner: journalisation,
                store: store,
                clock: sp.GetRequiredService<IClock>(),
                activite: sp.GetRequiredService<ITranscriptActivitySource>());
        });

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
            sp.GetRequiredService<IEtatServeur>(),    // HDR-03/HDR-04 : le rapport nomme ce que la sonde reçoit
            // OBS-01 — LE moniteur du widget, pas un second exemplaire. PARTAGE D'INSTANCE : le singleton
            // que Views.SessionsController consomme (l. 240) est exactement celui que le rapport interroge.
            // C'est ce qui rend le rapport vrai à chaque commit ultérieur PAR CONSTRUCTION : les phases 23
            // à 26 changeront le câblage du widget sans qu'une ligne du diagnostic ne change. Une copie du
            // comportement du widget aurait rouvert l'écart dès la phase suivante.
            // `machine` est sauté par argument NOMMÉ : la production conserve son repli réel (phase 20).
            moniteurSessions: sp.GetRequiredService<SessionMonitor>(),
            // CPT-02 — les TROIS magasins persistants RÉELS (mêmes instances que la chaîne) : âge de la dernière écriture,
            // dernière erreur, « journal muet depuis N min » dans [Magasins persistants] ; le troisième est celui des agrégats (TOK-01).
            magasins: new IEtatMagasin[] { sp.GetRequiredService<LastExactStore>(), sp.GetRequiredService<JournalReleves>(), sp.GetRequiredService<MagasinAgregats>() },
            // TOK-02 — l'état de la reconstruction (même instance que le service hébergé) : N / M fichiers, phase, dernier fichier, périmètre.
            reconstruction: sp.GetRequiredService<IEtatReconstruction>()));
    }
}
