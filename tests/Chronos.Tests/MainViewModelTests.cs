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
        FakeRecalibrationPrompt prompt, FakeBudgetPrompt budgetPrompt, SettingsService settings)
    {
        var options = new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero);
        var orch = new RefreshOrchestrator(provider, TempPaths(), options);
        return new MainViewModel(orch, ui, clock, controller, autostart, prompt, budgetPrompt, settings);
    }

    private static MainViewModel NewVmFull(
        out FakeUiDispatcher ui, out FakeClock clock, out FakeUsageProvider provider,
        out FakeWindowController controller, out FakeAutostartService autostart,
        out FakeRecalibrationPrompt prompt, out FakeBudgetPrompt budgetPrompt,
        out SettingsService settings, bool onUiThread = true)
    {
        ui = new FakeUiDispatcher { OnUiThread = onUiThread };
        clock = new FakeClock(Now);
        provider = new FakeUsageProvider();
        controller = new FakeWindowController();
        autostart = new FakeAutostartService();
        prompt = new FakeRecalibrationPrompt();
        budgetPrompt = new FakeBudgetPrompt();
        settings = new SettingsService(TempPaths());
        return Build(ui, clock, provider, controller, autostart, prompt, budgetPrompt, settings);
    }

    // Surcharge minimale conservée pour les tests RAF (fakes par défaut, non observés).
    private static MainViewModel NewVm(out FakeUiDispatcher ui, out FakeClock clock, out FakeUsageProvider provider)
        => NewVmFull(out ui, out clock, out provider, out _, out _, out _, out _, out _);

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
        var vm = new MainViewModel(orch, ui, clock,
            new FakeWindowController(), new FakeAutostartService(),
            new FakeRecalibrationPrompt(), new FakeBudgetPrompt(), new SettingsService(TempPaths()));
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
        var vm = NewVmFull(out _, out _, out _, out var controller, out _, out _, out _, out _);
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
        var vm = NewVmFull(out _, out _, out _, out _, out var autostart, out _, out _, out _);
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
            new FakeRecalibrationPrompt(), new FakeBudgetPrompt(), new SettingsService(TempPaths()));

        Assert.True(vm.IsAutostart);
        Assert.False(vm.IsBackground); // Background par défaut faux (settings absents)
    }

    // --- FEN-06 : Quit ferme l'application via le controller (seul point de sortie) ---

    [Fact]
    public void Quit_appelle_le_controller()
    {
        var vm = NewVmFull(out _, out _, out _, out var controller, out _, out _, out _, out _);
        vm.QuitCommand.Execute(null);
        Assert.Equal(1, controller.QuitCount);
    }

    // --- ROB-03 : Recalibrate recale le repli hebdo, persiste l'ancre ET conserve le badge « estimée » ---

    [Fact]
    public void Recalibrate_recale_le_repli_hebdo_en_conservant_le_badge_estimee()
    {
        var vm = NewVmFull(out _, out _, out _, out _, out _, out var prompt, out _, out var settings);

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

    // --- GAP-1 (audit intégration) : le recalibrage ne doit PAS écraser les réglages écrits sur disque
    // par un autre writer (OverlayController : coin/écran/arrière-plan) après la construction du VM ---

    [Fact]
    public void Recalibrate_n_ecrase_pas_les_reglages_persistes_par_un_autre_writer()
    {
        var vm = NewVmFull(out _, out _, out _, out _, out _, out var prompt, out _, out var settings);

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
        var vm = NewVmFull(out _, out _, out _, out _, out _, out var prompt, out _, out var settings);

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
        var vm = NewVmFull(out _, out _, out _, out _, out _, out var prompt, out _, out _);

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

    // --- CAL-01 : saisie d'un plafond 5 h seul → persisté avec source=Manual ; hebdo laissé vide → None ---

    [Fact]
    public void CalibrateBudgets_persiste_le_plafond_saisi_en_Manual_et_None_pour_le_champ_vide()
    {
        var vm = NewVmFull(out _, out _, out _, out _, out _, out _, out var budgetPrompt, out var settings);

        budgetPrompt.Result = new BudgetSelection(FiveHour: 2_000_000, Weekly: null);
        vm.CalibrateBudgetsCommand.Execute(null);

        Assert.Equal(1, budgetPrompt.AskCount);
        var apres = settings.Load();
        Assert.Equal(2_000_000, apres.FiveHourTokenBudget);
        Assert.Equal(BudgetSource.Manual, apres.FiveHourBudgetSource);
        Assert.NotNull(apres.FiveHourBudgetCalibratedAt);
        Assert.Null(apres.WeeklyTokenBudget);
        Assert.Equal(BudgetSource.None, apres.WeeklyBudgetSource);
        Assert.Null(apres.WeeklyBudgetCalibratedAt);
    }

    // --- CAL-01 : annulation du dialogue → aucune persistance (les plafonds restent null) ---

    [Fact]
    public void CalibrateBudgets_annule_ne_persiste_rien()
    {
        var vm = NewVmFull(out _, out _, out _, out _, out _, out _, out var budgetPrompt, out var settings);

        budgetPrompt.Result = null; // l'utilisateur annule
        vm.CalibrateBudgetsCommand.Execute(null);

        Assert.Equal(1, budgetPrompt.AskCount);
        var apres = settings.Load();
        Assert.Null(apres.FiveHourTokenBudget);
        Assert.Null(apres.WeeklyTokenBudget);
        Assert.Equal(BudgetSource.None, apres.FiveHourBudgetSource);
        Assert.Equal(BudgetSource.None, apres.WeeklyBudgetSource);
    }

    // --- GAP-1 : la calibration ne doit PAS écraser un réglage écrit sur disque par un autre writer
    // (OverlayController : coin/écran) APRÈS la construction du VM ---

    [Fact]
    public void CalibrateBudgets_n_ecrase_pas_les_reglages_persistes_par_un_autre_writer()
    {
        var vm = NewVmFull(out _, out _, out _, out _, out _, out _, out var budgetPrompt, out var settings);

        // Simule l'OverlayController : APRÈS la construction du VM, un drag persiste un nouveau coin.
        var externe = settings.Load() with { Corner = OverlayCorner.BottomLeft };
        settings.Save(externe);

        budgetPrompt.Result = new BudgetSelection(FiveHour: 3_500_000, Weekly: 50_000_000);
        vm.CalibrateBudgetsCommand.Execute(null);

        var apres = settings.Load();
        Assert.Equal(3_500_000, apres.FiveHourTokenBudget);          // plafond bien enregistré…
        Assert.Equal(50_000_000, apres.WeeklyTokenBudget);
        Assert.Equal(OverlayCorner.BottomLeft, apres.Corner);        // …SANS écraser le coin du drag (GAP-1)
    }
}
