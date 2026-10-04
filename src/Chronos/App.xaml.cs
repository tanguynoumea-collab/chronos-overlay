using System.Net.Http;
using System.Windows;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Services.Historique.Tokens;
using Chronos.ViewModels;
using Chronos.ViewModels.Historique;
using Chronos.Views;
using Chronos.Views.Historique;
using Chronos.Views.Reglages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Chronos;

public partial class App : Application
{
    private IHost? _host;                    // null tant que le Host n'est pas construit (seconde instance retirée avant lui, CPT-03)
    private static ResultatVerrou? _verrou;   // STATIQUE (D-32-13) : le GC ne doit jamais libérer le mutex pendant la vie de l'overlay

    protected override async void OnStartup(StartupEventArgs e)
    {
        // SOC-02 — le mode est décidé AVANT toute initialisation (ni verrou, ni Host, ni hooks), par un tri PUR testé
        // (ArgumentsDemarrage, liste blanche). Préséance et sémantique identiques à la 3.4.0.
        var invocation = ArgumentsDemarrage.Trier(e.Args);
        switch (invocation.Mode)
        {
            case ModeDemarrage.ArgumentInconnu:
                // Argument « --xxx » inconnu ou RETIRÉ (ex. l'ancien mode de barre de statut, que les sessions Claude Code déjà
                // ouvertes continuent d'émettre jusqu'à leur redémarrage) : sortie SILENCIEUSE, code 0 — ni fenêtre, ni verrou (pas de boîte « tourne déjà »),
                // ni réconciliation des hooks, ni stderr (Claude Code afficherait le stderr à l'utilisateur).
                Environment.Exit(0);
                return;

            case ModeDemarrage.Hook:
                // MODE HOOK (--hook <Event>) : Claude Code invoque « Chronos.exe --hook Notification/Stop/… » à
                // chaque événement de session ; on lit le JSON stdin et on écrit l'état de la session sur disque.
                Environment.Exit(RunSessionHook(invocation.EvenementHook));
                return;

            case ModeDemarrage.GalerieCadrans:
            {
                // MODE GALERIE CADRANS (--cadrans) : prototype visuel des 4 pistes de cadran (overlay 1). Court-circuite
                // le pipeline temps réel et le host DI : une simple fenêtre avec des données d'échantillon pilotables,
                // pour juger les concepts au coup d'œil. Sert la refonte visuelle (llm-council), hors app livrée.
                base.OnStartup(e);
                var gallery = new CadranGalleryWindow();
                MainWindow = gallery;
                gallery.Show();
                return;
            }

            case ModeDemarrage.GalerieSessions:
            {
                // MODE GALERIE SESSIONS (--sessions) : prototype visuel des 8 styles du widget de sessions (overlay 2),
                // avec des données d'échantillon. Court-circuite le pipeline/host, comme --cadrans.
                base.OnStartup(e);
                var gallery = new SessionsGalleryWindow();
                MainWindow = gallery;
                gallery.Show();
                return;
            }

            case ModeDemarrage.GalerieHistorique:
            {
                // MODE GALERIE HISTORIQUE (--historique) : la fenêtre Historique RÉELLE sur la semaine de référence des maquettes (phase 34,
                // D-34-26). Court-circuite le pipeline/host et le verrou d'instance unique comme --cadrans / --sessions : aucune réconciliation
                // des hooks, aucun service résolu, aucune écriture dans settings.json (réglages en mémoire). Sert à la revue visuelle DESIGN_PLAN §8.
                base.OnStartup(e);
                var galerie = HistoriqueGalerie.Creer();
                MainWindow = galerie;
                galerie.Show();
                return;
            }

            case ModeDemarrage.Overlay:
                break;   // suite ci-dessous : overlay normal
        }

        base.OnStartup(e);

        // FIAB-1 (42.2) — filet global AVANT tout le reste de l'overlay (verrou, Host, fenêtre) : sous .NET 8, une exception UI
        // non gérée termine le processus sans boîte ni ligne de journal, et le cadran disparaît sans trace. Mode OVERLAY
        // uniquement : le mode --hook et les galeries sont sortis plus haut.
        InstallerFiletGlobal();

        // CPT-03 — UNE SEULE INSTANCE de l'overlay par session Windows. Posé ici, APRÈS les court-circuits du tri des arguments
        // (ArgumentInconnu, --hook, --cadrans, --sessions, --historique ; multi-instances par construction : Claude Code
        // lance jusqu'à 5 hooks en parallèle) et AVANT le Host :
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

        // FIAB-1 (42.2) — la suite du démarrage est PROTÉGÉE : OnStartup est un async void, une exception ici (construction
        // du Host, StartAsync, résolution de la fenêtre, Show/RestorePlacement, widget) tuerait le processus sans explication.
        // Un échec est journalisé (incident reporté au lancement suivant), DIT à l'utilisateur, puis l'application s'arrête
        // proprement : Shutdown → OnExit, qui arrête le Host s'il existe et libère le mutex d'instance unique.
        try
        {
            var builder = Host.CreateApplicationBuilder();
            ConfigureServices(builder.Services);
            _host = builder.Build();

            // PKG-1 (42.2) — les exe publiés sont versionnés et coexistent : un raccourci shell:startup créé par une version
            // précédente vise l'ANCIEN exe, qui reprendrait la main au prochain redémarrage (verrou d'instance, hooks repointés).
            // Un raccourci EXISTANT est repointé vers l'exe courant (jamais créé s'il est absent). AVANT la résolution du
            // MainViewModel, qui lit IsEnabled : la case « Lancer au démarrage » reflète l'état corrigé.
            try
            {
                var bilanAutostart = _host.Services.GetRequiredService<IAutostartService>().ConvergerVersExeCourant();
                if (bilanAutostart == BilanAutostart.Repointe)
                    JournalIncidents.Signaler(DossierJournal, "autostart : raccourci shell:startup repointé vers l'exe courant (" + Environment.ProcessPath + ")");
                else if (bilanAutostart == BilanAutostart.Ignore)   // PKG-R1 (42.2-11) : plus de « dernier lancé gagne »
                    JournalIncidents.Signaler(DossierJournal, "autostart : raccourci conservé : l'exe courant (" + Environment.ProcessPath + ") est un build de développement, une copie temporaire ou n'est pas plus récent que la cible");
                else if (bilanAutostart == BilanAutostart.Echec)
                    JournalIncidents.Signaler(DossierJournal, "autostart : raccourci shell:startup non repointé — l'ancienne version pourrait démarrer au prochain redémarrage");
            }
            catch { /* l'autostart ne doit jamais empêcher le démarrage */ }

            // DAT-03 — l'ancienne barre de l'utilisateur (clé héritée des réglages ≤ 3.4), lue BRUTE avant tout Save : la clé, devenue
            // inconnue en 3.5, disparaîtrait au premier Save(Load() with …) déclenché par le placement/DPI. Elle sert à restaurer
            // la barre d'origine à la place de la barre Chronos retirée plus bas. Toute panne ⇒ null (simple retrait).
            var commandeHeritee = ClaudeSettingsReconciler.LireCommandeInterneHeritee(
                _host.Services.GetRequiredService<ChronosPaths>().SettingsFile);

            // Ordre de démarrage (Pitfall 3) : résoudre le VM AVANT StartAsync pour forcer son abonnement
            // à RefreshOrchestrator.SnapshotChanged. Sinon la charge initiale (émise pendant StartAsync)
            // partirait avant tout abonné → overlay vide jusqu'au prochain tick périodique (~60 s).
            _ = _host.Services.GetRequiredService<MainViewModel>();

            await _host.StartAsync();                    // charge initiale → atteint le VM (Post mis en file via BeginInvoke)

            // Restauration AVANT Show (FEN-07) : on fournit l'état persisté à la fenêtre ; SourceInitialized
            // appliquera RestorePlacement (coin + device = vérité) avant le premier rendu → pas de flash.
            var settings = _host.Services.GetRequiredService<ChronosSettings>();
            var window = _host.Services.GetRequiredService<MainWindow>();
            window.ApplyRestoredState(settings);
            MainWindow = window;                         // Application.MainWindow AVANT Show → les dialogues
                                                         // se centrent sur l'overlay (Owner), FEN-07
            window.Show();                               // ShowActivated=False (XAML) → pas de vol de focus

            // PUR-01/02/03, DAT-03 — réconcilier ~/.claude/settings.json : la barre de statut Chronos est RETIRÉE (sauvegarde
            // d'abord ; la barre d'origine est restaurée si elle est connue ; une barre tierce reste intacte), et les hooks sont
            // repointés vers l'exe courant dans la même écriture. Le bilan du passage est écrit dans chronos.log par le journal de
            // démarrage, lancé JUSTE APRÈS. Mode OVERLAY UNIQUEMENT : le mode --hook sort bien plus haut (en tête d'OnStartup, via
            // ArgumentsDemarrage.Trier) et ne doit JAMAIS atteindre ce point — 5 processus --hook concurrents en lire-modifier-écrire
            // perdraient les purges. Best-effort et silencieux : ne peut pas empêcher le démarrage.
            try
            {
                // MAT-5 — une ignorance sur un fichier Chronos ne devient pas une écriture dans la configuration de Claude Code :
                // si les réglages Chronos n'ont pas été lus de façon fiable au démarrage, SessionsWidgetEnabled est un DÉFAUT,
                // pas la volonté de l'utilisateur ; null laisse alors les hooks tels quels.
                var lectureDemarrage = _host.Services.GetRequiredService<SettingsService>().LectureDuDemarrage;
                bool? hooksVoulus = lectureDemarrage is { EstFiable: false } ? null : settings.SessionsWidgetEnabled;
                // Arbitrage ZEUS — après une quarantaine des réglages, le widget est revenu à « désactivé » par défaut : tant que
                // le marqueur est posé (effacé au prochain choix explicite sur le widget), aucun hook Chronos n'est retiré.
                _host.Services.GetRequiredService<ClaudeSettingsReconciler>()
                     .Reconcile(hooksVoulus, commandeHeritee, conserverHooks: settings.QuarantaineReglagesDepuis is not null, quarantaineDepuis: settings.QuarantaineReglagesDepuis);
            }
            catch { }

            // Log automatique au démarrage (observabilité) : écrit %APPDATA%/Chronos/chronos.log avec l'état réel
            // (token/OAuth/sources, bilan de la réconciliation). APRÈS la réconciliation, pour que le bilan y figure.
            // Fire-and-forget, ne bloque pas et ne peut pas casser le lancement. DS-PERF-03 (42.3) : lancé sur le POOL
            // (Task.Run), jamais sur le thread UI — processus, lectures disque et écriture du journal ne touchent pas le Dispatcher.
            var diag = _host.Services.GetRequiredService<DiagnosticService>();
            _ = Task.Run(() => diag.LogStartupAsync());

            // CYC-01 — le magasin d'états de session est balayé une fois par lancement. Même régime que la réconciliation
            // ci-dessus : mode OVERLAY uniquement (le mode --hook sort bien plus haut),
            // best-effort et silencieux, il ne peut pas empêcher le démarrage. Mesuré le 2026-09-12 : 54 états,
            // dont 48 de plus de sept jours, plus 12 fichiers temporaires abandonnés.
            //
            // Expirer, c'est ne plus savoir : ce qui est balayé disparaît, rien n'est déclaré terminé ni traité.
            try { _host.Services.GetRequiredService<BalayageMagasinSessions>().Balayer(); }
            catch { }

            // Widget de sessions : réafficher le panneau s'il était activé.
            _host.Services.GetRequiredService<ISessionsController>().ShowIfEnabled();
        }
        catch (Exception ex)
        {
            JournalIncidents.Signaler(DossierJournal, FiletExceptions.Decrire("démarrage", ex));
            MessageBox.Show("Chronos n'a pas pu démarrer : " + ex.Message + "\n\nDétail dans %APPDATA%\\Chronos\\chronos.log.",
                            "Chronos", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// FIAB-1 (42.2) — dossier du journal d'incidents (%APPDATA%\Chronos, via ChronosPaths) pour les filets, qui ne peuvent pas
    /// compter sur le Host (pas encore construit, ou déjà libéré). Null si indisponible : Signaler rend alors false sans lever.
    /// </summary>
    private static string? DossierJournal
    {
        get
        {
            try { return System.IO.Path.GetDirectoryName(ChronosPaths.Default().SettingsFile); }
            catch { return null; }
        }
    }

    /// <summary>
    /// FIAB-1 (42.2) — les trois filets globaux, tous vers le journal d'incidents de chronos.log :
    /// Dispatcher (thread UI : handlers, timers, commandes, WndProc) — marqué traité sauf exception FATALE, pour que le cadran
    /// survive à un défaut ponctuel au lieu de disparaître ; domaine (tout autre thread — on ne peut que journaliser, le
    /// processus meurt) ; tâches non observées (marquées observées : simple trace, jamais une terminaison).
    /// </summary>
    private void InstallerFiletGlobal()
    {
        DispatcherUnhandledException += (_, a) =>
        {
            // FIAB-R3 (42.2-11) : clé (origine, type, message, premier cadre) — une exception récurrente du tick n'inonde plus le journal.
            JournalIncidents.Signaler(DossierJournal, FiletExceptions.Decrire("UI", a.Exception), cle: FiletExceptions.Cle("UI", a.Exception));
            a.Handled = !FiletExceptions.EstFatale(a.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
            JournalIncidents.Signaler(DossierJournal, FiletExceptions.Decrire(a.IsTerminating ? "domaine, fatale" : "domaine",
                a.ExceptionObject as Exception ?? new Exception(a.ExceptionObject?.ToString())));
        TaskScheduler.UnobservedTaskException += (_, a) =>
        {
            JournalIncidents.Signaler(DossierJournal, FiletExceptions.Decrire("tâche non observée", a.Exception),
                                      cle: FiletExceptions.Cle("tâche non observée", a.Exception));
            a.SetObserved();
        };
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
    // encodage OEM par défaut mutilerait les accents (leçon tirée de l'ancienne barre de statut, qui
    // écrivait elle aussi en UTF-8 strict). Ne lève jamais : signaler un échec ne doit pas en produire un second.
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

    protected override void OnExit(ExitEventArgs e)
    {
        // Arrêt ATTENDU (dispose déterministe des Singletons IDisposable ; évite le piège async-void qui n'attend pas StopAsync),
        // mais HORS du thread UI et BORNÉ (quick 260927) : l'ancienne attente synchrone de l'arrêt (GetResult) bloquait ce thread
        // sans limite, une reprise de service hébergé attendait le Dispatcher → « Quitter Chronos » laissait un processus
        // zombie, sans fenêtre, qui gardait le mutex d'instance unique.
        // CPT-03 : le Host peut n'avoir JAMAIS été construit (seconde instance retirée avant lui) — _host est alors null.
        string? cause = null;
        if (_host is not null)
        {
            var dossierLog = DossierLog(_host);   // lu AVANT l'arrêt : le conteneur est libéré ensuite
            if (!ArretHote.Arreter(_host, ArretHote.DelaiParDefaut, out cause))
                ArretHote.SignalerDepassement(dossierLog, cause);
        }
        _verrou?.Liberer();   // sur le thread UI, celui qui a acquis (ReleaseMutex l'exige) ; l'OS le ferait à la mort du processus, on le fait proprement
        base.OnExit(e);
        // Dernier recours, APRÈS la libération du mutex : un arrêt dépassé ne doit jamais laisser le processus vivant.
        if (cause is not null) Environment.Exit(e.ApplicationExitCode);
    }

    /// <summary>Dossier de chronos.log (%APPDATA%\Chronos, via ChronosPaths) — null si indisponible.</summary>
    private static string? DossierLog(IHost host)
    {
        try { return System.IO.Path.GetDirectoryName(host.Services.GetService<ChronosPaths>()?.SettingsFile); }
        catch { return null; }
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
        // Phase 35 (ACC-02) : un seul ouvreur, une fenêtre recréée après fermeture, le VM singleton partagé avec la carte des réglages.
        services.AddSingleton(TimeZoneInfo.Local);
        services.AddSingleton<ISourceHistorique>(sp => new SourceHistoriqueDisque(sp.GetRequiredService<ChronosPaths>(), sp.GetRequiredService<TimeZoneInfo>()));
        services.AddSingleton<IReglagesHistorique>(sp => new ReglagesHistoriqueSurDisque(sp.GetRequiredService<SettingsService>()));
        services.AddSingleton<HistoriqueViewModel>();
        services.AddSingleton<IOuvreurHistorique>(sp => new OuvreurHistorique(() => new HistoriqueWindow(sp.GetRequiredService<HistoriqueViewModel>())));
        // Réglages v2 (quick 260927) : un seul ouvreur pour le clic droit du cadran — la fenêtre est ramenée si elle est ouverte,
        // recréée après fermeture ; le VM partagé reste le MainViewModel ; l'aperçu d'Apparence peint le contenu RÉEL du cadran.
        // Fabrique paresseuse : MainWindow (qui reçoit l'ouvreur) est déjà construite quand le premier clic droit arrive.
        services.AddSingleton<IOuvreurReglages>(sp => new OuvreurReglages(() => new ReglagesWindow(sp.GetRequiredService<MainViewModel>(), sp.GetRequiredService<MainWindow>().Content as System.Windows.Media.Visual)));
        services.AddSingleton<IPressePapiers, PressePapiersWpf>();

        // Placement/persistance Phase 6 (FEN-03/04/05/07) : settings.json chargé UNE fois au démarrage
        // (coin + device = vérité), adaptateur de placement, contrat neutre pour le VM (menu 06-04).
        services.AddSingleton<SettingsService>();
        services.AddSingleton(sp => sp.GetRequiredService<SettingsService>().ChargerPourDemarrage());   // ChronosSettings : LA lecture de démarrage (MAT-4, MAT-5)
        services.AddSingleton<OverlayController>();
        services.AddSingleton<IWindowController>(sp => sp.GetRequiredService<OverlayController>());

        // Menu contextuel 06-04 (FEN-06) : autostart shell:startup (DEP-02, service neutre de 06-02)
        services.AddSingleton<IAutostartService>(_ => new AutostartService());

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

        // MAT-1 — UNE seule passerelle vers ~/.claude/settings.json, partagée par l'installateur de hooks et le réconciliateur :
        // une seule voie d'écriture (lecture tri-état, sauvegarde du texte lu, contrôle « inchangé », refus sur lien symbolique).
        services.AddSingleton(_ => PasserelleReglagesClaude.ParDefaut());

        services.AddSingleton(sp => new SessionHookInstaller(sp.GetRequiredService<PasserelleReglagesClaude>()));

        // PUR-03, DAT-03 : réconciliation de ~/.claude/settings.json au démarrage (purge des entrées fantômes des
        // versions révolues, hooks repointés, retrait de la barre de statut Chronos). Chemins par défaut = profil utilisateur ; les tests
        // injectent systématiquement des chemins temp.
        services.AddSingleton(sp => new ClaudeSettingsReconciler(sp.GetRequiredService<PasserelleReglagesClaude>()));

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

        // DEL-01/DEL-02 : les transcripts ne repondent plus qu'a deux questions bornees (activite
        // depuis T ? tokens depuis T ?) — ils ne sont PLUS un IUsageProvider et sont donc HORS de la
        // chaine composite. Plus aucune dependance a SettingsService : ni plafond, ni ancre hebdo.
        // Phase 19 — MEMOISATION : une passe complete lit 727 fichiers / 882 Mo sur cette machine (mesure
        // 2026-10-04). Depuis 42.3 seule la premiere passe est complete : les suivantes ne relisent que les
        // fichiers modifies (cache par fichier du provider). Le journal rendu est PUR et interrogeable N fois, donc le reutiliser pendant sa duree de
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

        // DS2-03 / D-03 : « Se déconnecter » efface le dernier relevé exact. MÊME instance que la tête : un second
        // magasin n'aurait pas l'instant d'oubli, et la tête réécrirait le relevé de l'ancien compte au tick suivant.
        // AddSingleton<MainViewModel>() reste automatique : le paramètre optionnel d'un type enregistré est injecté.
        services.AddSingleton<IOubliDernierReleve>(sp => sp.GetRequiredService<LastExactStore>());

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
        // relu FRAIS à chaque appel via SettingsService.
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
        //   sonde d'en-têtes → login OAuth Chronos (UN SEUL composite).
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
                primary:  sp.GetRequiredService<RateLimitHeaderUsageProvider>(),   // sonde : seule porteuse du statut serveur
                fallback: sp.GetRequiredService<ChronosOAuthUsageProvider>()),     // secours exact
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
        // DS2-01 (42.4) : le filet par tick de l'orchestrateur consigne dans chronos.log — même dossier que DossierJournal,
        // mais issu du ChronosPaths du conteneur.
        services.AddSingleton(sp => new RefreshOrchestrator(sp.GetRequiredService<IUsageProvider>(), sp.GetRequiredService<RefreshOptions>(),
            dossierJournal: System.IO.Path.GetDirectoryName(sp.GetRequiredService<ChronosPaths>().SettingsFile)));
        services.AddHostedService(sp => sp.GetRequiredService<RefreshOrchestrator>());

        // Diagnostic auto-explicatif (menu « Diagnostic… ») : lit le snapshot PUBLIÉ par l'orchestrateur (P-03, 42.3),
        // jamais la tête de chaîne — aucune sonde supplémentaire, aucun last-exact.json disputé.
        services.AddSingleton(sp => new DiagnosticService(
            sp.GetRequiredService<ChronosPaths>(),
            sp.GetRequiredService<SettingsService>(),
            new DernierSnapshotPublie(sp.GetRequiredService<RefreshOrchestrator>()),   // P-03 : jamais un second consommateur de la chaîne
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<IAuthStatus>(),     // TOK-02 : le rapport dit l'état RÉEL
            sp.GetRequiredService<IEtatServeur>(),    // HDR-03/HDR-04 : le rapport nomme ce que la sonde reçoit
            // OBS-01 — LE moniteur du widget, pas un second exemplaire. PARTAGE D'INSTANCE : le singleton
            // que Views.SessionsController consomme (l. 240) est exactement celui que le rapport interroge.
            // C'est ce qui rend le rapport vrai à chaque commit ultérieur PAR CONSTRUCTION : les phases 23
            // à 26 changeront le câblage du widget sans qu'une ligne du diagnostic ne change. Une copie du
            // comportement du widget aurait rouvert l'écart dès la phase suivante.
            moniteurSessions: sp.GetRequiredService<SessionMonitor>(),
            // CPT-02 — les TROIS magasins persistants RÉELS (mêmes instances que la chaîne) : âge de la dernière écriture,
            // dernière erreur, « journal muet depuis N min » dans [Magasins persistants] ; le troisième est celui des agrégats (TOK-01).
            // MAT-3 / MAT-4 (42.2-03) — plus les deux magasins du widget de sessions (mêmes instances que le widget) : lecture non
            // aboutie et quarantaine de l'original illisible visibles au diagnostic.
            magasins: new IEtatMagasin[] { sp.GetRequiredService<LastExactStore>(), sp.GetRequiredService<JournalReleves>(), sp.GetRequiredService<MagasinAgregats>(),
                                           sp.GetRequiredService<ArchiveStore>(), sp.GetRequiredService<TreatedStore>() },
            // TOK-02 — l'état de la reconstruction (même instance que le service hébergé) : N / M fichiers, phase, dernier fichier, périmètre.
            reconstruction: sp.GetRequiredService<IEtatReconstruction>(),
            // Décision 5 (phase 35) : heures de la section « Journal d'historique » dans le fuseau injecté (celui de la fenêtre
            // Historique), jamais le fuseau local deviné dans le code neutre.
            fuseau: sp.GetRequiredService<TimeZoneInfo>(),
            // DAT-03 — la MÊME instance que celle appelée au démarrage : section « [Réglages de Claude Code] », bilan du retrait.
            reglagesClaude: sp.GetRequiredService<ClaudeSettingsReconciler>()));
    }
}
