using System.Diagnostics;
using System.IO;
using Chronos.Models;
using Chronos.Placement;
using Chronos.Services;
using Chronos.ViewModels;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la couche présentation temps réel + les commandes du menu contextuel (06-04) :
/// - RAF-04 : un snapshot poussé HORS thread UI est appliqué via IUiDispatcher.Post EXACTEMENT une fois
///   (frontière de thread unique), et les sous-VM reflètent le snapshot ; DataUnavailable = deux fenêtres Unavailable.
/// - RAF-03 : Interpolate(now) est PUR (recalcule fraction d'arc + compte à rebours) SANS aucun I/O
///   (GetAsync jamais appelé au tick) ; staleness dérivée de SourceCapturedAt.
/// - FEN-05/06, DEP-02, ROB-03 : ToggleBackground/ToggleAutostart/Recalibrate/Quit pilotent bien les
///   collaborateurs (IWindowController/IAutostartService/IRecalibrationPrompt) et le recalibrage recale
///   le repli hebdo EN CONSERVANT le badge « estimée » (honnêteté des chiffres).
///
/// Tests en [Fact] SIMPLE (pas [WpfFact]) : preuve que le DispatcherTimer n'est PAS créé dans le ctor
/// (il est créé côté UI via StartClock, Pitfall 4). Fakes déterministes (aucun écran/registre réel).
/// </summary>
public class MainViewModelTests
{
    private static async Task<bool> WaitUntilAsync(Func<bool> cond, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (cond()) return true;
            await Task.Delay(15);
        }
        return cond();
    }

    private static ChronosPaths TempPaths()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ChronosVmTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return new ChronosPaths(Path.Combine(dir, "usage.json"), Path.Combine(dir, "projects"));
    }

    private static WindowState Readable(WindowKind kind, DateTimeOffset now, double util = 0.5, TimeSpan? remaining = null) =>
        new()
        {
            Kind = kind,
            Reliability = SourceReliability.Exact,
            Utilization = util,
            ResetsAt = now + (remaining ?? TimeSpan.FromHours(2)),
        };

    // Fenêtre hebdo en REPLI (estimée) sans resets_at : cas où le recalibrage best-effort s'applique (ROB-03).
    private static WindowState EstimatedWeekly() =>
        new() { Kind = WindowKind.SevenDay, Reliability = SourceReliability.Estimated };

    private static readonly DateTimeOffset Now = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    // Cœur de construction : orchestrateur non démarré (source d'abonnement) + ctor complet 06-04/09-02.
    private static MainViewModel Build(
        FakeUiDispatcher ui, FakeClock clock, FakeUsageProvider provider,
        FakeWindowController controller, FakeAutostartService autostart,
        FakeRecalibrationPrompt prompt, SettingsService settings,
        FakeOAuthLogin? login = null, FakeAuthStatus? auth = null,
        RefreshOrchestrator? orchestrator = null, FakeEtatServeur? etatServeur = null)
    {
        var options = new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero);
        // orchestrator injectable : permet d'OBSERVER RequestRefresh en démarrant réellement
        // l'orchestrateur et en comptant les GetAsync. Sans cette prise, « un rafraîchissement a été
        // demandé » serait invérifiable depuis l'extérieur du VM.
        var orch = orchestrator ?? new RefreshOrchestrator(provider, TempPaths(), options);
        var diag = new DiagnosticService(new FakeClaudeTokenReader(), TempPaths(), settings, provider, clock);
        // etatServeur passe par le helper et NON par un nouveau site de construction : le 13e paramètre
        // est optionnel et en dernière position précisément pour que le compte de sites reste à 2.
        return new MainViewModel(orch, ui, clock, controller, autostart, prompt, settings, diag,
            new FakeStatusLineSetup(), login ?? new FakeOAuthLogin(), new FakeSessionsController(),
            auth ?? new FakeAuthStatus(), etatServeur);
    }

    private static MainViewModel NewVmFull(
        out FakeUiDispatcher ui, out FakeClock clock, out FakeUsageProvider provider,
        out FakeWindowController controller, out FakeAutostartService autostart,
        out FakeRecalibrationPrompt prompt,
        out SettingsService settings, bool onUiThread = true)
    {
        ui = new FakeUiDispatcher { OnUiThread = onUiThread };
        clock = new FakeClock(Now);
        provider = new FakeUsageProvider();
        controller = new FakeWindowController();
        autostart = new FakeAutostartService();
        prompt = new FakeRecalibrationPrompt();
        settings = new SettingsService(TempPaths());
        return Build(ui, clock, provider, controller, autostart, prompt, settings);
    }

    // Surcharge minimale conservée pour les tests RAF (fakes par défaut, non observés).
    private static MainViewModel NewVm(out FakeUiDispatcher ui, out FakeClock clock, out FakeUsageProvider provider)
        => NewVmFull(out ui, out clock, out provider, out _, out _, out _, out _);

    // --- RAF-04 : franchissement de thread unique via IUiDispatcher.Post (exactement une fois) ---

    [Fact]
    public async Task Snapshot_pousse_hors_thread_UI_est_marshale_une_seule_fois()
    {
        var snap = new UsageSnapshot
        {
            FiveHour = Readable(WindowKind.FiveHour, Now, util: 0.5),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now,
        };
        var provider = new FakeUsageProvider { Next = snap };
        var options = new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero); // isole la charge initiale
        var orch = new RefreshOrchestrator(provider, TempPaths(), options);
        var ui = new FakeUiDispatcher { OnUiThread = false };  // simule le thread pool de l'orchestrateur
        var clock = new FakeClock(Now);
        var settings = new SettingsService(TempPaths());
        var vm = new MainViewModel(orch, ui, clock,
            new FakeWindowController(), new FakeAutostartService(),
            new FakeRecalibrationPrompt(), settings,
            new DiagnosticService(new FakeClaudeTokenReader(), TempPaths(), settings, provider, clock),
            new FakeStatusLineSetup(), new FakeOAuthLogin(), new FakeSessionsController(),
            new FakeAuthStatus());
        try
        {
            await orch.StartAsync(CancellationToken.None); // charge initiale → SnapshotChanged (thread pool)
            var applied = await WaitUntilAsync(() => ui.PostCount >= 1, 2000);
            Assert.True(applied, "le snapshot initial doit être marshalé via IUiDispatcher.Post");
        }
        finally { await orch.StopAsync(CancellationToken.None); }

        Assert.Equal(1, ui.PostCount);                 // frontière franchie EXACTEMENT une fois
        Assert.Equal(0.5, vm.FiveHour.Utilization);    // propriétés reflètent le snapshot
        Assert.False(vm.DataUnavailable);              // une fenêtre lisible → données disponibles
    }

    // --- RAF-04 : DataUnavailable vrai SSI les deux fenêtres sont Unavailable ---

    [Fact]
    public void DataUnavailable_vrai_seulement_si_les_deux_fenetres_indisponibles()
    {
        var vm = NewVm(out _, out _, out _);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        });
        Assert.True(vm.DataUnavailable);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = Readable(WindowKind.FiveHour, Now),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now,
        });
        Assert.False(vm.DataUnavailable);
    }

    // --- RAF-03 : Interpolate(now) pur → fraction décroît, countdown change, AUCUN I/O au tick ---

    [Fact]
    public void Interpolate_recalcule_sans_aucun_IO_au_tick()
    {
        var vm = NewVm(out _, out var clock, out var provider);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = Readable(WindowKind.FiveHour, Now, remaining: TimeSpan.FromHours(2)),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now,
        });

        var fraction0 = vm.FiveHour.FractionRemaining; // 2 h / 5 h = 0.4
        var texte0 = vm.FiveHour.CountdownText;         // "2 h 00"

        clock.UtcNow = Now + TimeSpan.FromHours(1);     // +1 h
        vm.Interpolate(clock.UtcNow);

        Assert.True(vm.FiveHour.FractionRemaining < fraction0, "la fraction restante doit décroître dans le temps");
        Assert.NotEqual(texte0, vm.FiveHour.CountdownText);
        Assert.Equal(0, provider.GetCount); // Pitfall 1 : aucune relecture disque au tick d'interpolation
    }

    // --- RAF-03 : staleness dérivée de SourceCapturedAt (> 2 min → périmé) ---

    [Fact]
    public void IsStale_vrai_quand_la_capture_depasse_deux_minutes()
    {
        var vm = NewVm(out _, out _, out _);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = Readable(WindowKind.FiveHour, Now),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now - TimeSpan.FromMinutes(3), // capturé il y a 3 min
        });
        Assert.True(vm.IsStale);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = Readable(WindowKind.FiveHour, Now),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now, // frais
        });
        Assert.False(vm.IsStale);
    }

    // --- FEN-05 : ToggleBackground bascule l'état ET pilote le controller (arrière-plan / premier plan) ---

    [Fact]
    public void ToggleBackground_bascule_IsBackground_et_pilote_le_controller()
    {
        var vm = NewVmFull(out _, out _, out _, out var controller, out _, out _, out _);
        Assert.False(vm.IsBackground);

        vm.ToggleBackgroundCommand.Execute(null);
        Assert.True(vm.IsBackground);
        Assert.Equal(1, controller.SendToBackgroundCount);
        Assert.Equal(0, controller.BringToForegroundCount);

        vm.ToggleBackgroundCommand.Execute(null);
        Assert.False(vm.IsBackground);
        Assert.Equal(1, controller.BringToForegroundCount);
    }

    // --- DEP-02 : ToggleAutostart appelle Enable/Disable et reflète l'état réel (IsEnabled) ---

    [Fact]
    public void ToggleAutostart_appelle_Enable_Disable_et_reflete_IsEnabled()
    {
        var vm = NewVmFull(out _, out _, out _, out _, out var autostart, out _, out _);
        Assert.False(vm.IsAutostart);

        vm.ToggleAutostartCommand.Execute(null);
        Assert.True(vm.IsAutostart);
        Assert.Equal(1, autostart.EnableCount);
        Assert.True(autostart.Enabled);

        vm.ToggleAutostartCommand.Execute(null);
        Assert.False(vm.IsAutostart);
        Assert.Equal(1, autostart.DisableCount);
        Assert.False(autostart.Enabled);
    }

    // --- DEP-02 : à l'initialisation, IsAutostart reflète l'état réel du service ---

    [Fact]
    public void Initialisation_reflete_l_etat_reel_de_l_autostart()
    {
        var vm = Build(
            new FakeUiDispatcher { OnUiThread = true }, new FakeClock(Now), new FakeUsageProvider(),
            new FakeWindowController(), new FakeAutostartService { Enabled = true },
            new FakeRecalibrationPrompt(), new SettingsService(TempPaths()));

        Assert.True(vm.IsAutostart);
        Assert.False(vm.IsBackground); // Background par défaut faux (settings absents)
    }

    // --- FEN-06 : Quit ferme l'application via le controller (seul point de sortie) ---

    [Fact]
    public void Quit_appelle_le_controller()
    {
        var vm = NewVmFull(out _, out _, out _, out var controller, out _, out _, out _);
        vm.QuitCommand.Execute(null);
        Assert.Equal(1, controller.QuitCount);
    }

    // --- ROB-03 : Recalibrate recale le repli hebdo, persiste l'ancre ET conserve le badge « estimée » ---

    [Fact]
    public void Recalibrate_recale_le_repli_hebdo_en_conservant_le_badge_estimee()
    {
        var vm = NewVmFull(out _, out _, out _, out _, out _, out var prompt, out var settings);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
            SevenDay = EstimatedWeekly(),           // repli, ResetsAt inconnu → countdown "—"
            SourceCapturedAt = Now,
        });
        var avant = vm.SevenDay.CountdownText;
        Assert.True(vm.SevenDay.IsEstimated);

        var ancre = Now - TimeSpan.FromDays(3);     // prochain reset synthétisé strictement futur
        prompt.Result = ancre;
        vm.RecalibrateCommand.Execute(null);

        Assert.Equal(1, prompt.AskCount);
        Assert.NotEqual(avant, vm.SevenDay.CountdownText);      // arc/compte à rebours recalé
        Assert.True(vm.SevenDay.IsEstimated);                  // badge « estimée » CONSERVÉ (honnêteté)
        Assert.Equal(ancre, settings.Load().WeeklyAnchor);     // ancre persistée dans settings.json
    }

    // --- Clic au centre : bascule pourcentages ↔ temps avant reset (ShowPercent = inverse) ---
    [Fact]
    public void ToggleCenterMode_bascule_pourcentages_et_temps_et_notifie_ShowPercent()
    {
        var vm = NewVm(out _, out _, out _);

        Assert.False(vm.ShowCountdown);   // défaut = pourcentages
        Assert.True(vm.ShowPercent);

        vm.ToggleCenterMode();
        Assert.True(vm.ShowCountdown);    // → temps avant reset
        Assert.False(vm.ShowPercent);

        vm.ToggleCenterMode();
        Assert.False(vm.ShowCountdown);   // → retour aux pourcentages
        Assert.True(vm.ShowPercent);
    }

    // --- GAP-1 (audit intégration) : le recalibrage ne doit PAS écraser les réglages écrits sur disque
    // par un autre writer (OverlayController : coin/écran/arrière-plan) après la construction du VM ---

    [Fact]
    public void Recalibrate_n_ecrase_pas_les_reglages_persistes_par_un_autre_writer()
    {
        var vm = NewVmFull(out _, out _, out _, out _, out _, out var prompt, out var settings);

        // Simule l'OverlayController : APRÈS la construction du VM, un drag persiste un nouveau coin.
        var externe = settings.Load() with { Corner = OverlayCorner.BottomLeft, Background = true };
        settings.Save(externe);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
            SevenDay = EstimatedWeekly(),
            SourceCapturedAt = Now,
        });

        var ancre = Now - TimeSpan.FromDays(3);
        prompt.Result = ancre;
        vm.RecalibrateCommand.Execute(null);

        var apres = settings.Load();
        Assert.Equal(ancre, apres.WeeklyAnchor);                    // l'ancre est bien persistée…
        Assert.Equal(OverlayCorner.BottomLeft, apres.Corner);       // …SANS écraser le coin du drag
        Assert.True(apres.Background);                              // …ni le mode arrière-plan
    }

    // --- ROB-03 : annulation du dialogue → aucun changement, aucune persistance ---

    [Fact]
    public void Recalibrate_annule_ne_change_rien()
    {
        var vm = NewVmFull(out _, out _, out _, out _, out _, out var prompt, out var settings);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
            SevenDay = EstimatedWeekly(),
            SourceCapturedAt = Now,
        });
        var avant = vm.SevenDay.CountdownText;

        prompt.Result = null; // l'utilisateur annule
        vm.RecalibrateCommand.Execute(null);

        Assert.Equal(1, prompt.AskCount);
        Assert.Equal(avant, vm.SevenDay.CountdownText);
        Assert.Null(settings.Load().WeeklyAnchor);
    }

    // --- ROB-03 : le recalibrage NE TOUCHE PAS une source hebdo exacte (les chiffres exacts priment) ---

    [Fact]
    public void Recalibrate_ne_touche_pas_une_source_hebdo_exacte()
    {
        var vm = NewVmFull(out _, out _, out _, out _, out _, out var prompt, out _);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
            SevenDay = Readable(WindowKind.SevenDay, Now, remaining: TimeSpan.FromDays(3)), // Exact + ResetsAt
            SourceCapturedAt = Now,
        });
        var avant = vm.SevenDay.CountdownText;
        Assert.False(vm.SevenDay.IsEstimated);

        prompt.Result = Now - TimeSpan.FromDays(3);
        vm.RecalibrateCommand.Execute(null);

        Assert.Equal(avant, vm.SevenDay.CountdownText); // inchangé : la valeur exacte prime
        Assert.False(vm.SevenDay.IsEstimated);
    }

    // --- JOUR-01/02 : Interpolate pose DayFraction + DayResetAngles (angles vides si ResetsAt 5 h inconnu) ---

    [Fact]
    public void DayFraction_et_angles_poses_par_Interpolate()
    {
        var vm = NewVm(out _, out var clock, out _);

        // FiveHour.ResetsAt connu → angles non vides ; DayFraction câblée sur l'heure locale de now.
        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = Readable(WindowKind.FiveHour, Now, remaining: TimeSpan.FromHours(2)),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now,
        });
        vm.Interpolate(clock.UtcNow);

        var localNow = clock.UtcNow.ToLocalTime();
        Assert.Equal(Chronos.Rendering.DayTimeline.Fraction(localNow), vm.DayFraction, 9); // câblage exact
        Assert.NotEmpty(vm.DayResetAngles);                                                 // resets projetés

        // FiveHour Unavailable (ResetsAt null) → aucun angle (rien à projeter honnêtement).
        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now,
        });
        vm.Interpolate(clock.UtcNow);
        Assert.Empty(vm.DayResetAngles);
    }

    // --- INT-03 : à l'init, IsOAuthUsageEnabled reflète le setting (défaut true) ---
    [Fact]
    public void Initialisation_IsOAuthUsageEnabled_reflete_le_setting_par_defaut_true()
    {
        var vm = NewVmFull(out _, out _, out _, out _, out _, out _, out _);
        Assert.True(vm.IsOAuthUsageEnabled);   // défaut ChronosSettings = true
    }

    // --- INT-03 : le toggle bascule l'état ET persiste le flag dans settings.json ---
    [Fact]
    public void ToggleOAuthUsage_bascule_et_persiste_le_flag()
    {
        var vm = NewVmFull(out _, out _, out _, out _, out _, out _, out var settings);
        Assert.True(vm.IsOAuthUsageEnabled);

        vm.ToggleOAuthUsageCommand.Execute(null);
        Assert.False(vm.IsOAuthUsageEnabled);
        Assert.False(settings.Load().OAuthUsageEnabled);   // persisté off

        vm.ToggleOAuthUsageCommand.Execute(null);
        Assert.True(vm.IsOAuthUsageEnabled);
        Assert.True(settings.Load().OAuthUsageEnabled);     // persisté on
    }

    // --- GAP-1 : le toggle n'écrase pas un réglage écrit sur disque par un autre writer ---
    [Fact]
    public void ToggleOAuthUsage_n_ecrase_pas_les_reglages_persistes_par_un_autre_writer()
    {
        var vm = NewVmFull(out _, out _, out _, out _, out _, out _, out var settings);

        // Simule l'OverlayController : APRÈS construction du VM, un drag persiste un nouveau coin.
        settings.Save(settings.Load() with { Corner = OverlayCorner.BottomLeft });

        vm.ToggleOAuthUsageCommand.Execute(null);   // passe OAuthUsageEnabled à false

        var apres = settings.Load();
        Assert.False(apres.OAuthUsageEnabled);                 // flag bien persisté…
        Assert.Equal(OverlayCorner.BottomLeft, apres.Corner);  // …SANS écraser le coin du drag (GAP-1)
    }

    // ================== TOK-02 / TOK-03 : la panne visible et réparable ==================
    // Le 401 muet de deux mois n'était pas seulement un défaut de service : rien, dans la couche
    // présentation, ne pouvait le DIRE. Ces tests verrouillent les deux moitiés du remède :
    // deux états visuels distincts (ambre actionnable / gris informatif) et une commande de
    // reconnexion qui ne peut pas, par construction, supprimer le coffre de jetons.

    /// <summary>Monte un VM de test avec un FakeAuthStatus (et éventuellement un FakeOAuthLogin
    /// observable). Tous les fakes non observés sont neutres ; l'orchestrateur n'est PAS démarré.</summary>
    private static MainViewModel VmAuth(FakeUiDispatcher ui, FakeAuthStatus auth,
                                        FakeOAuthLogin? login = null)
        => Build(ui, new FakeClock(Now), new FakeUsageProvider(), new FakeWindowController(),
                 new FakeAutostartService(), new FakeRecalibrationPrompt(),
                 new SettingsService(TempPaths()), login: login, auth: auth);

    [Fact]
    public void L_etat_d_authentification_initial_est_applique_DES_le_ctor()
    {
        // L'autorité peut déjà être en échec au démarrage de l'exe (jeton expiré sur disque) : attendre
        // la première TRANSITION laisserait l'overlay muet jusqu'au prochain changement d'état.
        var auth = new FakeAuthStatus { Etat = EtatAuthentification.Deconnecte };

        var vm = VmAuth(new FakeUiDispatcher { OnUiThread = true }, auth);

        Assert.True(vm.AfficherPastilleDeconnexion);
        Assert.False(vm.AfficherPastilleHorsLigne);
    }

    [Fact]
    public void L_etat_Deconnecte_franchit_la_frontiere_de_thread_UNE_fois_et_allume_la_pastille()
    {
        var ui = new FakeUiDispatcher { OnUiThread = false };   // simule le thread pool de l'autorité
        var auth = new FakeAuthStatus();
        var vm = VmAuth(ui, auth);
        var avant = ui.PostCount;

        auth.Declencher(EtatAuthentification.Deconnecte);

        Assert.Equal(avant + 1, ui.PostCount);   // RAF-04 : une seule frontière, un seul Post
        Assert.True(vm.AfficherPastilleDeconnexion);
        Assert.False(vm.AfficherPastilleHorsLigne);
    }

    [Fact]
    public void HorsLigne_allume_la_pastille_INFORMATIVE_et_JAMAIS_l_alerte_de_deconnexion()
    {
        // Le cœur de la distinction à 4 valeurs : crier « reconnecte-toi » à quelqu'un dont le wifi est
        // coupé le ferait relancer un login qui échouerait — et lui ferait perdre confiance dans le signal.
        var auth = new FakeAuthStatus();
        var vm = VmAuth(new FakeUiDispatcher { OnUiThread = false }, auth);

        auth.Declencher(EtatAuthentification.HorsLigne);

        Assert.True(vm.AfficherPastilleHorsLigne);
        Assert.False(vm.AfficherPastilleDeconnexion);
    }

    [Fact]
    public void Connecte_apres_Deconnecte_eteint_les_DEUX_pastilles()
    {
        var auth = new FakeAuthStatus { Etat = EtatAuthentification.Deconnecte };
        var vm = VmAuth(new FakeUiDispatcher { OnUiThread = false }, auth);
        Assert.True(vm.AfficherPastilleDeconnexion);   // état de départ : la panne est visible

        auth.Declencher(EtatAuthentification.Connecte);

        Assert.False(vm.AfficherPastilleDeconnexion);
        Assert.False(vm.AfficherPastilleHorsLigne);
    }

    [Fact]
    public void NonConnecte_n_allume_AUCUNE_pastille()
    {
        // L'invite « jamais connecté » est EXA-05 (phase 19). L'allumer ici créerait un badge PERMANENT
        // pour un utilisateur qui a délibérément choisi de ne pas se connecter.
        var auth = new FakeAuthStatus { Etat = EtatAuthentification.Deconnecte };
        var vm = VmAuth(new FakeUiDispatcher { OnUiThread = false }, auth);

        auth.Declencher(EtatAuthentification.NonConnecte);

        Assert.False(vm.AfficherPastilleDeconnexion);
        Assert.False(vm.AfficherPastilleHorsLigne);
    }

    [Fact]
    public async Task ReconnecterCommand_relance_le_login_et_ne_deconnecte_JAMAIS()
    {
        var login = new FakeOAuthLogin { LoggedIn = true };   // jeton présent mais MORT : le cas réel
        var auth = new FakeAuthStatus { Etat = EtatAuthentification.Deconnecte };
        var vm = VmAuth(new FakeUiDispatcher { OnUiThread = true }, auth, login: login);

        await vm.ReconnecterCommand.ExecuteAsync(null);

        Assert.Equal(1, login.LoginCount);
        Assert.Equal(0, login.LogoutCount);   // <- LE piège : LoginClaudeCommand aurait supprimé le coffre
        Assert.Equal(1, auth.ReinitCount);    // l'autorité est réarmée, la pastille ne survit pas
        Assert.True(vm.IsLoggedIn);
    }

    [Fact]
    public async Task LoginClaudeCommand_DECONNECTE_bel_et_bien_quand_un_jeton_est_present()
    {
        // Contre-épreuve du piège TOK-03, et garde de non-régression sur la commande du MENU : c'est
        // exactement ce comportement de BASCULE (IsLoggedIn == la seule présence du fichier) qui aurait
        // supprimé le coffre si la pastille avait été bindée dessus. La commande reste intacte.
        var login = new FakeOAuthLogin { LoggedIn = true };
        var vm = VmAuth(new FakeUiDispatcher { OnUiThread = true }, new FakeAuthStatus(), login: login);

        await vm.LoginClaudeCommand.ExecuteAsync(null);

        Assert.Equal(1, login.LogoutCount);   // la bascule déconnecte…
        Assert.Equal(0, login.LoginCount);    // …et ne relance AUCUN login
        Assert.False(vm.IsLoggedIn);
    }

    [Fact]
    public async Task ReconnecterCommand_sur_login_reussi_demande_un_rafraichissement_IMMEDIAT()
    {
        // Sans RequestRefresh, il faudrait attendre le tick de 60 s avant de revoir un chiffre exact —
        // la pastille survivrait plus d'une minute à sa propre réparation. Preuve DE BOUT EN BOUT :
        // l'orchestrateur est réellement démarré et un GetAsync SUPPLÉMENTAIRE doit survenir.
        // (Ne pas se fier à TryTrigger : le channel est en DropWrite, où TryWrite renvoie true même plein.)
        //
        // NON couvert ici, faute d'être observable : l'ORDRE réarmement -> rafraîchissement. La source
        // appelle bien ReinitialiserApresLogin() AVANT RequestRefresh() — nécessaire, sinon la boucle
        // consommatrice pourrait lire l'usage alors que l'autorité est encore verrouillée sur
        // « Deconnecte » — mais RequestRefresh ne fait qu'EMPILER un déclencheur : la consommation
        // étant asynchrone, inverser les deux lignes ne change AUCUN résultat de test (vérifié par
        // mutation). Un test d'ordre serait donc une fausse assurance ; l'invariant est tenu par la
        // lecture du code et par la XML-doc de ReconnecterAsync.
        var provider = new FakeUsageProvider();
        var orch = new RefreshOrchestrator(provider, TempPaths(),
                                           new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero));
        var auth = new FakeAuthStatus { Etat = EtatAuthentification.Deconnecte };
        var vm = Build(new FakeUiDispatcher { OnUiThread = true }, new FakeClock(Now), provider,
                       new FakeWindowController(), new FakeAutostartService(), new FakeRecalibrationPrompt(),
                       new SettingsService(TempPaths()),
                       login: new FakeOAuthLogin { LoggedIn = true }, auth: auth, orchestrator: orch);
        try
        {
            await orch.StartAsync(CancellationToken.None);
            Assert.True(await WaitUntilAsync(() => provider.GetCount >= 1, 2000), "charge initiale");
            var avant = provider.GetCount;

            await vm.ReconnecterCommand.ExecuteAsync(null);

            Assert.True(await WaitUntilAsync(() => provider.GetCount > avant, 2000),
                        "un rafraîchissement doit être demandé sans attendre le tick périodique");
        }
        finally { await orch.StopAsync(CancellationToken.None); }

        Assert.Equal(1, auth.ReinitCount);
    }

    [Fact]
    public async Task ReconnecterCommand_sur_login_ECHOUE_ne_rearme_rien_et_laisse_la_panne_visible()
    {
        // Réarmer l'autorité sur un échec relâcherait le verrou « Deconnecte » et le recul sans qu'aucun
        // jeton neuf n'ait été écrit : l'overlay se tairait en prétendant être réparé.
        var login = new FakeOAuthLogin { LoggedIn = true, LoginDoitReussir = false };
        var auth = new FakeAuthStatus { Etat = EtatAuthentification.Deconnecte };
        var vm = VmAuth(new FakeUiDispatcher { OnUiThread = true }, auth, login: login);

        await vm.ReconnecterCommand.ExecuteAsync(null);

        Assert.Equal(1, login.LoginCount);
        Assert.Equal(0, login.LogoutCount);   // jamais de déconnexion, même sur échec
        Assert.Equal(0, auth.ReinitCount);    // l'autorité reste verrouillée sur « Deconnecte »
        Assert.True(vm.AfficherPastilleDeconnexion);   // la panne reste visible
    }

    // --- HDR-03/HDR-04/HDR-06 : interrupteur de la sonde et état déclaré par le serveur ---
    // La sonde est la SEULE source qui dépense du quota pour en mesurer : son interrupteur doit exister,
    // être DISTINCT de celui de l'endpoint OAuth (profil de coût opposé), et ce que le serveur DÉCLARE
    // doit être montré tel quel — jamais complété par une bonne nouvelle inventée.

    /// <summary>Monte un VM avec un canal d'état serveur (et éventuellement un SettingsService
    /// pré-ensemencé). Aucun accès au vrai %APPDATA% : les réglages vivent sous Path.GetTempPath().</summary>
    private static MainViewModel VmSonde(FakeUiDispatcher ui, FakeEtatServeur? etat = null,
                                         SettingsService? settings = null,
                                         RefreshOrchestrator? orchestrator = null,
                                         FakeUsageProvider? provider = null)
        => Build(ui, new FakeClock(Now), provider ?? new FakeUsageProvider(), new FakeWindowController(),
                 new FakeAutostartService(), new FakeRecalibrationPrompt(),
                 settings ?? new SettingsService(TempPaths()),
                 orchestrator: orchestrator, etatServeur: etat);

    [Fact]
    public void L_interrupteur_de_sonde_reflete_l_etat_REEL_persiste()
    {
        // Défaut du champ : true (chiffres exacts dès l'installation). Un settings.json sans le champ
        // doit donc allumer l'interrupteur, et un settings.json qui le porte à false doit l'éteindre :
        // l'interrupteur est un MIROIR, jamais une valeur d'affichage indépendante de l'état réel.
        var vmDefaut = VmSonde(new FakeUiDispatcher { OnUiThread = true });
        Assert.True(vmDefaut.IsSondeEnTetesActivee);

        var settings = new SettingsService(TempPaths());
        settings.Save(settings.Load() with { SondeEnTetesActivee = false });
        var vmCoupee = VmSonde(new FakeUiDispatcher { OnUiThread = true }, settings: settings);
        Assert.False(vmCoupee.IsSondeEnTetesActivee);
    }

    [Fact]
    public void ToggleSondeEnTetes_bascule_persiste_et_n_ecrase_AUCUN_autre_reglage()
    {
        var settings = new SettingsService(TempPaths());
        var vm = VmSonde(new FakeUiDispatcher { OnUiThread = true }, settings: settings);
        Assert.True(vm.IsSondeEnTetesActivee);

        // GAP-1 : un autre writer (sélecteur de thème, OverlayController…) écrit sur disque APRÈS la
        // construction du VM. Sauvegarder la copie du constructeur effacerait son travail.
        settings.Save(settings.Load() with { ThemeKey = "aurore" });

        vm.ToggleSondeEnTetesCommand.Execute(null);

        var apres = settings.Load();
        Assert.False(vm.IsSondeEnTetesActivee);
        Assert.False(apres.SondeEnTetesActivee);      // bascule bien persistée…
        Assert.Equal("aurore", apres.ThemeKey);       // …sans écraser le réglage écrit entre-temps

        vm.ToggleSondeEnTetesCommand.Execute(null);
        Assert.True(vm.IsSondeEnTetesActivee);
        Assert.True(settings.Load().SondeEnTetesActivee);
    }

    [Fact]
    public void Les_DEUX_interrupteurs_sont_INDEPENDANTS_sur_disque()
    {
        // C'est LE test qui documente pourquoi ce sont deux champs et non un seul : leurs profils de coût
        // sont opposés. OAuthUsageEnabled garde le jeton de l'app bureau et ne dépense RIEN ; la sonde
        // dépense une vraie micro-requête par passage. Les fusionner priverait l'utilisateur du seul
        // interrupteur qui gouverne une dépense — ou lui ferait perdre ses chiffres exacts pour l'éteindre.
        var settings = new SettingsService(TempPaths());
        var vm = VmSonde(new FakeUiDispatcher { OnUiThread = true }, settings: settings);

        vm.ToggleSondeEnTetesCommand.Execute(null);
        Assert.False(settings.Load().SondeEnTetesActivee);
        Assert.True(settings.Load().OAuthUsageEnabled);      // l'autre source n'a PAS été coupée
        Assert.True(vm.IsOAuthUsageEnabled);

        vm.ToggleOAuthUsageCommand.Execute(null);
        Assert.False(settings.Load().OAuthUsageEnabled);
        Assert.False(settings.Load().SondeEnTetesActivee);   // …et réciproquement, aucun effet croisé
    }

    [Fact]
    public async Task ToggleSondeEnTetes_demande_un_rafraichissement_IMMEDIAT()
    {
        // Sans RequestRefresh, couper ou rallumer la sonde n'aurait d'effet qu'au prochain tick — ou, pire,
        // l'utilisateur qui vient de couper une dépense verrait une requête partir encore après son clic.
        var provider = new FakeUsageProvider();
        var orch = new RefreshOrchestrator(provider, TempPaths(),
                                           new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero));
        var vm = VmSonde(new FakeUiDispatcher { OnUiThread = true }, orchestrator: orch, provider: provider);
        try
        {
            await orch.StartAsync(CancellationToken.None);
            Assert.True(await WaitUntilAsync(() => provider.GetCount >= 1, 2000), "charge initiale");
            var avant = provider.GetCount;

            vm.ToggleSondeEnTetesCommand.Execute(null);

            Assert.True(await WaitUntilAsync(() => provider.GetCount > avant, 2000),
                        "la bascule doit prendre effet sans attendre le tick périodique");
        }
        finally { await orch.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public void Le_statut_DECLARE_par_le_serveur_est_rendu_pour_les_DEUX_fenetres()
    {
        var vm = VmSonde(new FakeUiDispatcher { OnUiThread = true });

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = new WindowState
            {
                Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact, Utilization = 0.91,
                ResetsAt = Now + TimeSpan.FromHours(1), StatutServeur = StatutServeur.AutoriseAvertissement,
            },
            SevenDay = new WindowState
            {
                Kind = WindowKind.SevenDay, Reliability = SourceReliability.Exact, Utilization = 0.3,
                ResetsAt = Now + TimeSpan.FromDays(3), StatutServeur = StatutServeur.Autorise,
            },
            SourceCapturedAt = Now,
        });

        Assert.True(vm.AfficherEtatSonde);
        Assert.Contains("5 h : AUTORISÉ (avertissement)", vm.TexteEtatSonde);
        Assert.Contains("hebdo : AUTORISÉ", vm.TexteEtatSonde);
    }

    [Fact]
    public void Aucun_statut_rapporte_n_affiche_RIEN_et_JAMAIS_autorise()
    {
        // Le cœur de l'honnêteté de HDR-03 : l'en-tête absent signifie que le serveur n'a rien dit.
        // Afficher « AUTORISÉ » par défaut serait rassurer à tort — pire que se taire.
        var vm = VmSonde(new FakeUiDispatcher { OnUiThread = true }, new FakeEtatServeur());

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = Readable(WindowKind.FiveHour, Now),    // aucun StatutServeur
            SevenDay = Readable(WindowKind.SevenDay, Now),
            SourceCapturedAt = Now,
        });

        Assert.False(vm.AfficherEtatSonde);
        Assert.Equal("", vm.TexteEtatSonde);
        Assert.DoesNotContain("AUTORIS", vm.TexteEtatSonde);
    }

    [Fact]
    public void Le_depassement_est_rendu_en_POURCENTAGE_par_le_canal_lateral()
    {
        var etat = new FakeEtatServeur
        {
            Depassement = new EtatDepassement { Utilization = 0.34, Statut = StatutServeur.Rejete },
        };
        var vm = VmSonde(new FakeUiDispatcher { OnUiThread = true }, etat);

        Assert.True(vm.AfficherEtatSonde);
        Assert.Contains("dépassement 34 %", vm.TexteEtatSonde);
    }

    [Fact]
    public void L_etat_serveur_initial_est_applique_DES_le_ctor()
    {
        // Même raison qu'au plan 17-05 pour la pastille : sur cette machine rien ne transitera avant
        // longtemps (jeton expiré). Un état appliqué seulement sur transition resterait vide pour de bon.
        var etat = new FakeEtatServeur { Depassement = new EtatDepassement { Utilization = 0.12 } };

        var vm = VmSonde(new FakeUiDispatcher { OnUiThread = true }, etat);

        Assert.True(vm.AfficherEtatSonde);
        Assert.Contains("12 %", vm.TexteEtatSonde);
    }

    [Fact]
    public void Le_depassement_franchit_la_frontiere_de_thread_UNE_fois()
    {
        // TROISIÈME frontière de thread du VM (le plan 17-05 annonçait la deuxième comme « dernière » :
        // cet ajout l'amende). Le service émet sur un thread du pool, l'abonné marshalle lui-même.
        var ui = new FakeUiDispatcher { OnUiThread = false };
        var etat = new FakeEtatServeur();
        var vm = VmSonde(ui, etat);
        var avant = ui.PostCount;
        Assert.False(vm.AfficherEtatSonde);

        etat.Declencher(new EtatDepassement { Utilization = 0.5 });

        Assert.Equal(avant + 1, ui.PostCount);   // RAF-04 : un Post par événement, pas deux
        Assert.True(vm.AfficherEtatSonde);
        Assert.Contains("dépassement 50 %", vm.TexteEtatSonde);
    }

    [Fact]
    public void Le_VM_se_construit_SANS_canal_d_etat_serveur_et_reste_muet()
    {
        // Le 13e paramètre est optionnel : les sites de construction préexistants compilent sans retouche,
        // et l'absence de canal ne fabrique aucun état serveur.
        var vm = VmSonde(new FakeUiDispatcher { OnUiThread = true }, etat: null);

        Assert.False(vm.AfficherEtatSonde);
        Assert.Equal("", vm.TexteEtatSonde);
    }

    // ================== EXA-05 : l'invitation à se connecter ==================

    // Snapshot « on n'a JAMAIS rien eu » : deux fenêtres indisponibles ET le bit du magasin à false.
    private static UsageSnapshot JamaisDExact(bool? bit = false) => new()
    {
        FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
        SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        UnExactADejaEteObtenu = bit,
    };

    /// <summary>EXA-05 — aucun exact jamais obtenu ET rien à afficher : l'overlay invite à se connecter
    /// plutôt que de laisser un cadran muet. Il n'affiche AUCUN pourcentage (les deux textes sont vides).</summary>
    [Fact]
    public void Jamais_d_exact_et_rien_a_afficher_allume_l_invitation()
    {
        var vm = NewVm(out _, out _, out _);

        vm.ApplySnapshot(JamaisDExact());

        Assert.True(vm.AfficherInvitationConnexion);
        Assert.True(vm.DataUnavailable);
        Assert.Equal("", vm.FiveHour.UtilizationText);   // EXA-05 : jamais un pourcentage
        Assert.Equal("", vm.SevenDay.UtilizationText);
    }

    /// <summary>EXA-05 — un exact a DÉJÀ été obtenu : sa fenêtre a simplement tourné, ou la source est
    /// momentanément muette. On ne crie pas « connecte-toi » à quelqu'un qui l'est.</summary>
    [Fact]
    public void Un_exact_deja_obtenu_eteint_l_invitation()
    {
        var vm = NewVm(out _, out _, out _);

        vm.ApplySnapshot(JamaisDExact(bit: true));

        Assert.False(vm.AfficherInvitationConnexion);
    }

    /// <summary>EXA-05, le piège : <c>null</c> n'est pas <c>false</c>. Le magasin en panne (ou un snapshot
    /// né hors de la couche de doctrine, dont <c>UsageSnapshot.Empty</c>) ne répond PAS « jamais » — il ne
    /// répond pas du tout. Une absence de réponse ne doit jamais produire une affirmation.</summary>
    [Fact]
    public void Bit_non_evalue_null_n_allume_pas_l_invitation()
    {
        var vm = NewVm(out _, out _, out _);

        vm.ApplySnapshot(JamaisDExact(bit: null));
        Assert.False(vm.AfficherInvitationConnexion);

        vm.ApplySnapshot(UsageSnapshot.Empty);          // le cas réel : Empty porte null
        Assert.False(vm.AfficherInvitationConnexion);
    }

    /// <summary>EXA-05 — le bit dit « jamais d'exact », mais une fenêtre porte tout de même un chiffre
    /// (plancher, repli) : l'overlay affiche déjà quelque chose, l'invitation n'a rien à ajouter.</summary>
    [Fact]
    public void Un_chiffre_disponible_eteint_l_invitation()
    {
        var vm = NewVm(out _, out _, out _);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = Readable(WindowKind.FiveHour, Now, util: 0.42),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            UnExactADejaEteObtenu = false,
        });

        Assert.False(vm.DataUnavailable);
        Assert.False(vm.AfficherInvitationConnexion);
    }

    /// <summary>EXA-05 / TOK-02 — EXCLUSIVITÉ. Les deux pastilles portent le MÊME geste
    /// (ReconnecterCommand) ; les allumer ensemble sur un cadran de 170 px serait une redondance, pas une
    /// information. La déconnexion est le diagnostic le plus précis des deux : elle gagne.</summary>
    [Fact]
    public void L_etat_Deconnecte_eteint_l_invitation()
    {
        var vm = NewVm(out _, out _, out _);

        vm.ApplySnapshot(JamaisDExact());
        vm.AppliquerEtatAuth(EtatAuthentification.Deconnecte);

        Assert.True(vm.AfficherPastilleDeconnexion);
        Assert.False(vm.AfficherInvitationConnexion);
    }

    /// <summary>Les deux entrées arrivent par DEUX canaux et à DEUX instants (un snapshot, un événement
    /// d'authentification). Sans point de recomposition unique, le dernier arrivé écraserait l'autre et
    /// l'invitation resterait périmée. Ce test est la raison d'être de <c>MajPastilles</c>.</summary>
    [Fact]
    public void Un_changement_d_etat_d_auth_apres_le_snapshot_recompose_l_invitation()
    {
        var vm = NewVm(out _, out _, out _);

        vm.ApplySnapshot(JamaisDExact());
        Assert.True(vm.AfficherInvitationConnexion);

        vm.AppliquerEtatAuth(EtatAuthentification.Deconnecte);
        Assert.False(vm.AfficherInvitationConnexion);   // effacée devant le diagnostic plus précis

        vm.AppliquerEtatAuth(EtatAuthentification.Connecte);
        Assert.True(vm.AfficherInvitationConnexion);    // RALLUMÉE sans nouveau snapshot

        vm.AppliquerEtatAuth(EtatAuthentification.HorsLigne);
        Assert.True(vm.AfficherInvitationConnexion);    // hors ligne est informatif : il n'efface rien
        Assert.True(vm.AfficherPastilleHorsLigne);
    }
}
