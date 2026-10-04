using System.Diagnostics;
using System.IO;
using Chronos.Models;
using Chronos.Models.Historique.Tokens;
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
///   (GetAsync jamais appelé au tick) et sans AUCUN jugement d'ancienneté : depuis la phase 20, juger
///   l'âge d'un relevé appartient à la doctrine seule, le ViewModel se borne à le rapporter.
/// - FEN-05/06, DEP-02 : ToggleBackground/ToggleAutostart/Quit pilotent bien les collaborateurs
///   (IWindowController/IAutostartService) ; le reset hebdo affiché n'est jamais synthétique (DAT-02).
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

    // Fenêtre EXACTE et lisible dont la doctrine a statué la PROVENANCE : matière des tests EXA-03.
    // Un ResetsAt futur est fourni pour que le recalibrage hebdo best-effort ne s'en mêle pas.
    private static WindowState Provenue(WindowKind kind, ProvenanceReleve p) =>
        new()
        {
            Kind = kind,
            Reliability = SourceReliability.Exact,
            Utilization = 0.5,
            ResetsAt = Now + TimeSpan.FromHours(2),
            Provenance = p,
            Source = SourceUsage.SondeEnTetes,
            CapturedAt = Now - TimeSpan.FromMinutes(12),
        };

    // Fenêtre hebdo en REPLI (estimée) sans resets_at : cas où le recalibrage best-effort s'applique (ROB-03).
    private static WindowState EstimatedWeekly() =>
        new() { Kind = WindowKind.SevenDay, Reliability = SourceReliability.Estimated };

    private static readonly DateTimeOffset Now = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    // Cœur de construction : orchestrateur non démarré (source d'abonnement) + ctor complet 06-04/09-02.
    private static MainViewModel Build(
        FakeUiDispatcher ui, FakeClock clock, FakeUsageProvider provider,
        FakeWindowController controller, FakeAutostartService autostart,
        SettingsService settings,
        FakeOAuthLogin? login = null, FakeAuthStatus? auth = null,
        RefreshOrchestrator? orchestrator = null, FakeEtatServeur? etatServeur = null,
        FakeEtatJournal? journal = null, FakeEtatReconstruction? reconstruction = null,
        FakeOuvreurHistorique? ouvreur = null, FakeOubliDernierReleve? oubli = null)
    {
        var options = new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero);
        // orchestrator injectable : permet d'OBSERVER RequestRefresh en démarrant réellement
        // l'orchestrateur et en comptant les GetAsync. Sans cette prise, « un rafraîchissement a été
        // demandé » serait invérifiable depuis l'extérieur du VM.
        var orch = orchestrator ?? new RefreshOrchestrator(provider, options);
        var diag = new DiagnosticService(TempPaths(), settings, provider, clock);
        // etatServeur passe par le helper et NON par un nouveau site de construction : le 13e paramètre
        // est optionnel et en dernière position précisément pour que le compte de sites reste à 2.
        return new MainViewModel(orch, ui, clock, controller, autostart, settings, diag,
            login ?? new FakeOAuthLogin(), new FakeSessionsController(),
            auth ?? new FakeAuthStatus(), etatServeur, journal, reconstruction: reconstruction, ouvreurHistorique: ouvreur,
            oubliReleve: oubli);
    }

    private static MainViewModel NewVmFull(
        out FakeUiDispatcher ui, out FakeClock clock, out FakeUsageProvider provider,
        out FakeWindowController controller, out FakeAutostartService autostart,
        out SettingsService settings, bool onUiThread = true)
    {
        ui = new FakeUiDispatcher { OnUiThread = onUiThread };
        clock = new FakeClock(Now);
        provider = new FakeUsageProvider();
        controller = new FakeWindowController();
        autostart = new FakeAutostartService();
        settings = new SettingsService(TempPaths());
        return Build(ui, clock, provider, controller, autostart, settings);
    }

    // Surcharge minimale conservée pour les tests RAF (fakes par défaut, non observés).
    private static MainViewModel NewVm(out FakeUiDispatcher ui, out FakeClock clock, out FakeUsageProvider provider)
        => NewVmFull(out ui, out clock, out provider, out _, out _, out _);

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
        var orch = new RefreshOrchestrator(provider, options);
        var ui = new FakeUiDispatcher { OnUiThread = false };  // simule le thread pool de l'orchestrateur
        var clock = new FakeClock(Now);
        var settings = new SettingsService(TempPaths());
        var vm = new MainViewModel(orch, ui, clock,
            new FakeWindowController(), new FakeAutostartService(),
            settings,
            new DiagnosticService(TempPaths(), settings, provider, clock),
            new FakeOAuthLogin(), new FakeSessionsController(),
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

    // --- EXA-03 : la seconde notion de « périmé » est MORTE ---
    //
    // Le test qui vivait ici vérifiait qu'un seuil de DEUX minutes, calculé par ce ViewModel et bindé
    // nulle part, déclarait périmée une capture de trois minutes. Il ne décrivait plus une exigence mais
    // une incohérence : la limite d'âge du projet vit dans DoctrineFraicheur, dérivée de la cadence de
    // la sonde. Il n'a pas été retiré sans remplaçant — il en a TROIS :
    //   . GardesDoctrineTests.Aucun_seuil_d_anciennete_n_est_calcule_dans_les_ViewModels_de_la_doctrine
    //   . WindowGaugeViewModelTests.EstDate_est_RAPPORTE_par_la_doctrine_et_jamais_recalcule
    //   . WindowGaugeViewModelTests.Un_plancher_est_AUSSI_date_et_un_encore_valide_est_date_SANS_etre_plancher

    // --- EXA-03 : la marque « relevé daté », globale et CONSERVATRICE (le plus vieux des deux) ---

    /// <summary>Le cas nominal ne porte AUCUNE marque : deux fenêtres fraîches, rien à signaler. Sans ce
    /// test, une marque allumée en permanence passerait pour un succès.</summary>
    [Fact]
    public void Deux_fenetres_fraiches_ne_portent_AUCUNE_marque_d_age()
    {
        var vm = NewVm(out _, out _, out _);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = Provenue(WindowKind.FiveHour, ProvenanceReleve.Frais),
            SevenDay = Provenue(WindowKind.SevenDay, ProvenanceReleve.Frais),
            SourceCapturedAt = Now,
        });

        Assert.False(vm.AfficherReleveDate);
    }

    /// <summary>« Le plus vieux des deux, jamais le plus jeune » : une SEULE fenêtre datée suffit. Une
    /// synthèse conservatrice n'est pas un mensonge ; une synthèse optimiste en serait un. Et l'hebdo est
    /// ici EncoreValide : datée, mais PROUVÉE juste — la marque est informative, elle ne dégrade rien.</summary>
    [Fact]
    public void Une_seule_fenetre_datee_suffit_a_allumer_la_marque()
    {
        var vm = NewVm(out _, out _, out _);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = Provenue(WindowKind.FiveHour, ProvenanceReleve.Frais),
            SevenDay = Provenue(WindowKind.SevenDay, ProvenanceReleve.EncoreValide),
            SourceCapturedAt = Now,
        });

        Assert.True(vm.AfficherReleveDate);
        Assert.False(vm.SevenDay.EstPlancher);   // datée ET juste : aucune dégradation du chiffre
    }

    /// <summary>Rien à dater quand il n'y a pas de chiffre : deux fenêtres indisponibles n'allument pas
    /// la marque. Dater le vide reviendrait à affirmer qu'un chiffre existe, et qu'il est vieux.</summary>
    [Fact]
    public void Deux_fenetres_indisponibles_n_ont_rien_a_dater()
    {
        var vm = NewVm(out _, out _, out _);
        vm.ApplySnapshot(UsageSnapshot.Empty);

        Assert.True(vm.DataUnavailable);
        Assert.False(vm.AfficherReleveDate);
    }

    // --- EXA-06 au cadran : l'infobulle nomme QUI alimente chaque fenêtre, et DEPUIS QUAND ---

    /// <summary>Les DEUX fenêtres sont nommées, chacune avec son libellé de source et son ancienneté.
    /// Assertions par PRÉSENCE et jamais par égalité stricte : une égalité sur un texte composé se casse
    /// au premier ajout de séparateur et n'apprend rien de plus. Le vocabulaire asserté est celui de
    /// LibelleSource — si quelqu'un remappait les libellés en local, ces assertions tomberaient.</summary>
    [Fact]
    public void L_infobulle_nomme_les_DEUX_fenetres_avec_leur_source_et_leur_anciennete()
    {
        var vm = NewVm(out _, out _, out _);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = new WindowState
            {
                Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact, Utilization = 0.4,
                ResetsAt = Now + TimeSpan.FromHours(2), Provenance = ProvenanceReleve.Frais,
                Source = SourceUsage.SondeEnTetes, CapturedAt = Now - TimeSpan.FromMinutes(12),
            },
            SevenDay = new WindowState
            {
                Kind = WindowKind.SevenDay, Reliability = SourceReliability.Exact, Utilization = 0.7,
                ResetsAt = Now + TimeSpan.FromDays(3), Provenance = ProvenanceReleve.EncoreValide,
                Source = SourceUsage.EndpointOAuthChronos, CapturedAt = Now - TimeSpan.FromHours(3),
            },
            SourceCapturedAt = Now,
        });

        Assert.Contains("5 h :", vm.InfobulleReleve);
        Assert.Contains("hebdo :", vm.InfobulleReleve);
        Assert.Contains("sonde d'en-têtes de rate-limit", vm.InfobulleReleve);
        Assert.Contains("endpoint OAuth (login Chronos)", vm.InfobulleReleve);
        Assert.Contains("relevé il y a 12 min", vm.InfobulleReleve);
        Assert.Contains("relevé il y a 3 h 00", vm.InfobulleReleve);
        Assert.Contains("encore valide", vm.InfobulleReleve);   // ce que la doctrine a VÉRIFIÉ
    }

    /// <summary>DEL-04 + EXA-04 — la matière brute d'un plancher devient enfin visible, à la demande, et
    /// TELLE QUELLE. Elle n'est jamais convertie en points de pourcentage : les limites Anthropic
    /// pondèrent par modèle, donc « tokens / plafond » restera faux quel que soit le plafond. C'est aussi
    /// la levée de la dette n° 1 de la phase 19 — TokensText cessait d'être calculé pour personne.</summary>
    [Fact]
    public void Une_fenetre_plancher_joint_son_compte_de_tokens_BRUT_jamais_un_pourcentage()
    {
        var vm = NewVm(out _, out _, out _);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = new WindowState
            {
                Kind = WindowKind.FiveHour, Reliability = SourceReliability.Estimated, Utilization = 0.8,
                ResetsAt = Now + TimeSpan.FromHours(2), Provenance = ProvenanceReleve.PlancherAvecActivite,
                Source = SourceUsage.MagasinDernierExact, CapturedAt = Now - TimeSpan.FromHours(3),
                TokensDepuisReleve = 643_649_933,
            },
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now,
        });

        Assert.True(vm.AfficherReleveDate);
        Assert.True(vm.FiveHour.EstPlancher);
        Assert.Contains("tokens", vm.InfobulleReleve);                 // la matière brute est LÀ
        Assert.Contains(vm.FiveHour.TokensText, vm.InfobulleReleve);   // et c'est bien celle de la jauge
        Assert.Contains("borne inférieure", vm.InfobulleReleve);       // ce que la doctrine a statué
        Assert.DoesNotContain("%", vm.InfobulleReleve);                // JAMAIS un point de pourcentage
    }

    /// <summary>Une fenêtre que personne n'alimente ne reçoit pas un nom par défaut : l'infobulle dit
    /// « non renseignée ». Nommer une source qu'on ignore serait exactement la panne silencieuse que ce
    /// milestone éradique — et le relevé sans horodatage se dit, lui aussi, plutôt que de se taire.</summary>
    [Fact]
    public void Une_fenetre_sans_source_rend_non_renseignee_et_jamais_un_nom_par_defaut()
    {
        var vm = NewVm(out _, out _, out _);
        vm.ApplySnapshot(UsageSnapshot.Empty);

        Assert.Contains("non renseignée", vm.InfobulleReleve);
        Assert.Contains("de date inconnue", vm.InfobulleReleve);
        Assert.Contains("5 h :", vm.InfobulleReleve);      // les deux lignes existent même vides de source
        Assert.Contains("hebdo :", vm.InfobulleReleve);
    }

    // --- FEN-05 : ToggleBackground bascule l'état ET pilote le controller (arrière-plan / premier plan) ---

    [Fact]
    public void ToggleBackground_bascule_IsBackground_et_pilote_le_controller()
    {
        var vm = NewVmFull(out _, out _, out _, out var controller, out _, out _);
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
        var vm = NewVmFull(out _, out _, out _, out _, out var autostart, out _);
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
            new SettingsService(TempPaths()));

        Assert.True(vm.IsAutostart);
        Assert.False(vm.IsBackground); // Background par défaut faux (settings absents)
    }

    // --- FEN-06 : Quit ferme l'application via le controller (seul point de sortie) ---

    [Fact]
    public void Quit_appelle_le_controller()
    {
        var vm = NewVmFull(out _, out _, out _, out var controller, out _, out _);
        vm.QuitCommand.Execute(null);
        Assert.Equal(1, controller.QuitCount);
    }

    // --- DAT-02 (37-04) : plus de recalibrage — une ancre enregistrée ne fabrique JAMAIS de reset hebdo ---

    [Fact]
    public void Une_fenetre_hebdo_sans_reset_reste_sans_reset_malgre_une_ancre()
    {
        var settings = new SettingsService(TempPaths());
        var ancre = new DateTimeOffset(2026, 07, 11, 0, 0, 0, TimeSpan.FromHours(2));
        settings.Save(settings.Load() with { WeeklyAnchor = ancre });   // ancre héritée d'un ancien recalibrage
        var vm = Build(new FakeUiDispatcher { OnUiThread = true }, new FakeClock(Now), new FakeUsageProvider(),
                       new FakeWindowController(), new FakeAutostartService(), settings);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
            SevenDay = EstimatedWeekly(),           // aucune date de reset fournie par la source
            SourceCapturedAt = Now,
        });

        Assert.False(vm.SevenDay.HasTime);                       // aucun reset synthétique
        Assert.Equal(ancre, settings.Load().WeeklyAnchor);       // l'ancre n'est ni effacée ni réécrite
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

    // --- ACC-02 / D-35-06 : clic au centre arbitré — simple = bascule à l'échéance, double = Historique sans bascule ---

    private static MainViewModel VmAvecOuvreur(FakeOuvreurHistorique? ouvreur, out FakeClock clock)
    {
        clock = new FakeClock(Now);
        return Build(new FakeUiDispatcher { OnUiThread = true }, clock, new FakeUsageProvider(), new FakeWindowController(),
                     new FakeAutostartService(), new SettingsService(TempPaths()), ouvreur: ouvreur);
    }

    [Fact]
    public void Un_simple_clic_au_centre_bascule_une_fois_a_l_echeance()
    {
        var ouvreur = new FakeOuvreurHistorique();
        var vm = VmAvecOuvreur(ouvreur, out var clock);
        vm.DefinirDelaiDoubleClic(TimeSpan.FromMilliseconds(500));

        vm.ClicCentre(1);
        Assert.False(vm.ShowCountdown);                    // armée, pas encore décidée

        clock.UtcNow = Now + TimeSpan.FromMilliseconds(500);
        vm.EcheanceClicCentre();
        Assert.True(vm.ShowCountdown);                     // basculé…

        clock.UtcNow = Now + TimeSpan.FromSeconds(2);
        vm.EcheanceClicCentre();
        Assert.True(vm.ShowCountdown);                     // … une seule fois
        Assert.Equal(0, ouvreur.Ouvertures);
    }

    [Fact]
    public void Un_double_clic_au_centre_ouvre_l_Historique_sans_bascule()
    {
        var ouvreur = new FakeOuvreurHistorique();
        var vm = VmAvecOuvreur(ouvreur, out var clock);
        vm.DefinirDelaiDoubleClic(TimeSpan.FromMilliseconds(500));

        vm.ClicCentre(1);
        clock.UtcNow = Now + TimeSpan.FromMilliseconds(200);
        vm.ClicCentre(2);
        clock.UtcNow = Now + TimeSpan.FromSeconds(1);
        vm.EcheanceClicCentre();

        Assert.Equal(1, ouvreur.Ouvertures);
        Assert.False(vm.ShowCountdown);                    // INCHANGÉ : aucune bascule

        vm.OuvrirHistoriqueCommand.Execute(null);          // le bouton « Ouvrir » des réglages passe par le même ouvreur
        Assert.Equal(2, ouvreur.Ouvertures);
    }

    [Fact]
    public void Sans_ouvreur_un_double_clic_ne_leve_pas_et_ne_bascule_pas()
    {
        var vm = VmAvecOuvreur(ouvreur: null, out var clock);

        vm.ClicCentre(1);
        vm.ClicCentre(2);
        clock.UtcNow = Now + TimeSpan.FromSeconds(1);
        vm.EcheanceClicCentre();
        vm.OuvrirHistoriqueCommand.Execute(null);

        Assert.False(vm.ShowCountdown);
    }

    [Fact]
    public void Le_bouton_Ouvrir_ouvre_l_Historique_une_fois()
    {
        var ouvreur = new FakeOuvreurHistorique();
        var vm = VmAvecOuvreur(ouvreur, out _);

        vm.OuvrirHistoriqueCommand.Execute(null);

        Assert.Equal(1, ouvreur.Ouvertures);
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

    // ================== TOK-02 / TOK-03 : la panne visible et réparable ==================
    // Le 401 muet de deux mois n'était pas seulement un défaut de service : rien, dans la couche
    // présentation, ne pouvait le DIRE. Ces tests verrouillent les deux moitiés du remède :
    // deux états visuels distincts (ambre actionnable / gris informatif) et une commande de
    // reconnexion qui ne peut pas, par construction, supprimer le coffre de jetons.

    /// <summary>Monte un VM de test avec un FakeAuthStatus (et éventuellement un FakeOAuthLogin
    /// observable). Tous les fakes non observés sont neutres ; l'orchestrateur n'est PAS démarré.</summary>
    private static MainViewModel VmAuth(FakeUiDispatcher ui, FakeAuthStatus auth,
                                        FakeOAuthLogin? login = null, FakeOubliDernierReleve? oubli = null)
        => Build(ui, new FakeClock(Now), new FakeUsageProvider(), new FakeWindowController(),
                 new FakeAutostartService(), new SettingsService(TempPaths()), login: login, auth: auth, oubli: oubli);

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

    // --- P-01 (audit externe, DS-ARCH-01) : la commande du menu réarme l'autorité de jeton ---
    // Sans réarmement, la copie mémoire de l'autorité survivait à « Se déconnecter » et le
    // rafraîchissement suivant recréait oauth.dat avec les jetons de l'ancien compte.

    [Fact]
    public async Task LoginClaudeCommand_en_etat_connecte_deconnecte_ET_rearme_l_autorite()
    {
        var login = new FakeOAuthLogin { LoggedIn = true };
        var auth = new FakeAuthStatus();
        var vm = VmAuth(new FakeUiDispatcher { OnUiThread = true }, auth, login: login);

        await vm.LoginClaudeCommand.ExecuteAsync(null);

        Assert.Equal(1, login.LogoutCount);
        // Séquence D-03 (amendement 42.4-03) : réarmement AVANT le Logout puis de nouveau APRÈS.
        Assert.Equal(2, auth.ReinitCount);    // l'autorité oublie ses jetons : le coffre effacé n'est pas recréé
        Assert.False(vm.IsLoggedIn);
    }

    [Fact]
    public async Task LoginClaudeCommand_login_reussi_rearme_l_autorite()
    {
        var login = new FakeOAuthLogin { LoggedIn = false };
        var auth = new FakeAuthStatus();
        var vm = VmAuth(new FakeUiDispatcher { OnUiThread = true }, auth, login: login);

        await vm.LoginClaudeCommand.ExecuteAsync(null);

        Assert.Equal(1, login.LoginCount);
        // Séquence D-03 (amendement 42.4-03) : réarmement AVANT le login puis de nouveau APRÈS.
        Assert.Equal(2, auth.ReinitCount);    // le coffre du nouveau login est relu, jamais écrasé par l'ancien
        Assert.True(vm.IsLoggedIn);
    }

    [Fact]
    public async Task LoginClaudeCommand_login_ECHOUE_ne_fait_que_le_rearmement_d_avant_le_login()
    {
        var login = new FakeOAuthLogin { LoggedIn = false, LoginDoitReussir = false };
        var auth = new FakeAuthStatus();
        var vm = VmAuth(new FakeUiDispatcher { OnUiThread = true }, auth, login: login);

        await vm.LoginClaudeCommand.ExecuteAsync(null);

        Assert.Equal(1, login.LoginCount);
        // D-03 (amendement 42.4-03) : la PREMIÈRE paire (oubli + réarmement) précède le login, donc reste faite ;
        // la seconde, comme en 42.3, n'a lieu que sur un login réussi (aucun jeton neuf : rien à relire).
        Assert.Equal(1, auth.ReinitCount);
        Assert.False(vm.IsLoggedIn);
    }

    // --- DS2-03 / D-03 (décision VERROUILLÉE « Les effacer ») : la commande du menu oublie le dernier relevé exact ---
    // Séquence (amendement orchestrateur) : oubli ; réarmement ; Logout()|LoginAsync() ; réarmement ; oubli.
    // Le premier oubli ferme la porte AVANT que le coffre ne change ; le second rattrape un relevé de l'ancien
    // compte arrivé pendant le login (navigateur ouvert plusieurs secondes).

    [Fact]
    public async Task Se_deconnecter_oublie_le_dernier_releve_AVANT_le_logout()
    {
        var login = new FakeOAuthLogin { LoggedIn = true };
        var auth = new FakeAuthStatus();
        var oubli = new FakeOubliDernierReleve();
        var moments = new List<(int Logouts, int Reinits)>();
        oubli.QuandAppele = () => moments.Add((login.LogoutCount, auth.ReinitCount));
        var vm = VmAuth(new FakeUiDispatcher { OnUiThread = true }, auth, login: login, oubli: oubli);

        await vm.LoginClaudeCommand.ExecuteAsync(null);

        Assert.Equal(2, oubli.Appels);
        Assert.Equal(Now, oubli.Dernier);              // horloge du VM
        Assert.Equal((0, 0), moments[0]);              // premier oubli : avant le réarmement et avant le Logout
        Assert.Equal((1, 2), moments[1]);              // second oubli : après le Logout et le second réarmement
        Assert.Equal(1, login.LogoutCount);
    }

    [Fact]
    public async Task Changer_de_compte_oublie_le_dernier_releve_AVANT_le_login()
    {
        var login = new FakeOAuthLogin { LoggedIn = false };
        var auth = new FakeAuthStatus();
        var oubli = new FakeOubliDernierReleve();
        var moments = new List<(int Logins, int Reinits)>();
        oubli.QuandAppele = () => moments.Add((login.LoginCount, auth.ReinitCount));
        var vm = VmAuth(new FakeUiDispatcher { OnUiThread = true }, auth, login: login, oubli: oubli);

        await vm.LoginClaudeCommand.ExecuteAsync(null);

        Assert.Equal(2, oubli.Appels);
        Assert.Equal((0, 0), moments[0]);              // effacement AVANT le login du nouveau compte
        Assert.Equal((1, 2), moments[1]);
    }

    [Fact]
    public async Task Changer_de_compte_avec_login_ECHOUE_oublie_quand_meme_avant_le_login()
    {
        var login = new FakeOAuthLogin { LoggedIn = false, LoginDoitReussir = false };
        var oubli = new FakeOubliDernierReleve();
        var loginsAuMoment = -1;
        oubli.QuandAppele = () => loginsAuMoment = login.LoginCount;
        var vm = VmAuth(new FakeUiDispatcher { OnUiThread = true }, new FakeAuthStatus(), login: login, oubli: oubli);

        await vm.LoginClaudeCommand.ExecuteAsync(null);

        Assert.Equal(1, oubli.Appels);                 // la première paire seulement (sortie anticipée de 42.3)
        Assert.Equal(0, loginsAuMoment);
    }

    [Fact]
    public async Task Reconnecter_depuis_la_pastille_n_oublie_rien()
    {
        // « Répare-moi » : même compte dans la quasi-totalité des cas — effacer masquerait un chiffre vrai.
        var login = new FakeOAuthLogin { LoggedIn = true };
        var oubli = new FakeOubliDernierReleve();
        var vm = VmAuth(new FakeUiDispatcher { OnUiThread = true }, new FakeAuthStatus(), login: login, oubli: oubli);

        await vm.ReconnecterCommand.ExecuteAsync(null);

        Assert.Equal(0, oubli.Appels);
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
        var orch = new RefreshOrchestrator(provider,
                                           new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero));
        var auth = new FakeAuthStatus { Etat = EtatAuthentification.Deconnecte };
        var vm = Build(new FakeUiDispatcher { OnUiThread = true }, new FakeClock(Now), provider,
                       new FakeWindowController(), new FakeAutostartService(), new SettingsService(TempPaths()),
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
                 new FakeAutostartService(), settings ?? new SettingsService(TempPaths()),
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
    public void L_interrupteur_de_la_sonde_persiste_sans_toucher_aux_autres_reglages()
    {
        // La sonde dépense une vraie micro-requête par passage : son interrupteur ne gouverne QU'ELLE. Le
        // couper ne doit déplacer aucun autre réglage persisté (coin, arrière-plan, mode du cadran).
        var settings = new SettingsService(TempPaths());
        settings.Save(settings.Load() with
        {
            Corner = OverlayCorner.BottomLeft,
            Background = true,
            CadranMode = CadranDisplayMode.Etendu,
        });
        var vm = VmSonde(new FakeUiDispatcher { OnUiThread = true }, settings: settings);

        vm.ToggleSondeEnTetesCommand.Execute(null);

        var apres = settings.Load();
        Assert.False(apres.SondeEnTetesActivee);
        Assert.Equal(OverlayCorner.BottomLeft, apres.Corner);
        Assert.True(apres.Background);
        Assert.Equal(CadranDisplayMode.Etendu, apres.CadranMode);
    }

    [Fact]
    public async Task ToggleSondeEnTetes_demande_un_rafraichissement_IMMEDIAT()
    {
        // Sans RequestRefresh, couper ou rallumer la sonde n'aurait d'effet qu'au prochain tick — ou, pire,
        // l'utilisateur qui vient de couper une dépense verrait une requête partir encore après son clic.
        var provider = new FakeUsageProvider();
        var orch = new RefreshOrchestrator(provider,
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
    // --- JRN-04 (phase 32, 32-05) : l'âge de la dernière écriture du journal, en première classe ---
    // D-32-21 : « muet » se mesure depuis max(démarrage, dernière écriture) — sinon l'alerte s'allumerait à chaque lancement
    // sur l'écriture de la veille, ce qui n'est pas « muet alors que Chronos tourne ». D-32-22 : mêmes mots que le diagnostic.

    private static UsageSnapshot SnapSimple() => new()
    {
        FiveHour = Readable(WindowKind.FiveHour, Now, util: 0.5),
        SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        SourceCapturedAt = Now,
    };

    /// <summary>VM construit à <see cref="Now"/> (= instant de démarrage retenu par le VM), horloge rendue pour avancer le temps.</summary>
    private static MainViewModel VmAvecJournal(FakeEtatJournal? journal, out FakeClock clock)
    {
        clock = new FakeClock(Now);
        return Build(new FakeUiDispatcher { OnUiThread = true }, clock, new FakeUsageProvider(), new FakeWindowController(),
                     new FakeAutostartService(), new SettingsService(TempPaths()), journal: journal);
    }

    [Fact]
    public void Sans_journal_injecte_la_carte_est_masquee()
    {
        var vm = VmAvecJournal(journal: null, out _);

        vm.ApplySnapshot(SnapSimple());

        Assert.False(vm.AfficherEtatJournal);
        Assert.Equal("", vm.TexteEtatJournal);
        Assert.False(vm.AlerteJournal);
    }

    [Fact]
    public void Une_ecriture_recente_donne_l_age_et_le_compte_sans_alerte()
    {
        // Démarrage à Now ; une écriture 6 min plus tard ; on regarde 10 min après le démarrage → « il y a 4 min ».
        var journal = new FakeEtatJournal { RelevesEcrits = 3 };
        var vm = VmAvecJournal(journal, out var clock);
        journal.DerniereEcriture = Now + TimeSpan.FromMinutes(6);
        clock.UtcNow = Now + TimeSpan.FromMinutes(10);

        vm.ApplySnapshot(SnapSimple());

        Assert.Equal("dernière écriture il y a 4 min · 3 relevés depuis le démarrage", vm.TexteEtatJournal);
        Assert.False(vm.AlerteJournal);
        Assert.True(vm.AfficherEtatJournal);
    }

    [Fact]
    public void Seize_minutes_sans_ecriture_alors_que_Chronos_tourne_allument_l_alerte()
    {
        var journal = new FakeEtatJournal { DerniereEcriture = Now, RelevesEcrits = 1 };
        var vm = VmAvecJournal(journal, out var clock);
        clock.UtcNow = Now + TimeSpan.FromMinutes(16);

        vm.ApplySnapshot(SnapSimple());

        Assert.True(vm.AlerteJournal);
        Assert.StartsWith("journal muet depuis 16 min", vm.TexteEtatJournal);
        Assert.Contains("dernière écriture il y a 16 min", vm.TexteEtatJournal);
        Assert.Equal("journal muet depuis 16 min · dernière écriture il y a 16 min · 1 relevé depuis le démarrage", vm.TexteEtatJournal);
    }

    [Fact]
    public void L_alerte_ne_s_allume_pas_sur_l_ecriture_de_la_veille_juste_apres_le_demarrage()
    {
        var journal = new FakeEtatJournal { DerniereEcriture = Now - TimeSpan.FromDays(1) };
        var vm = VmAvecJournal(journal, out var clock);

        // 5 min après le démarrage : la référence est le démarrage, pas l'écriture d'hier — aucune alerte.
        clock.UtcNow = Now + TimeSpan.FromMinutes(5);
        vm.ApplySnapshot(SnapSimple());
        Assert.False(vm.AlerteJournal);
        Assert.Equal("aucune écriture depuis le démarrage · dernière écriture il y a 1 j", vm.TexteEtatJournal);

        // Une panne d'écriture se dit avec sa cause, à la place de l'âge.
        journal.DerniereErreur = "IOException : x";
        vm.ApplySnapshot(SnapSimple());
        Assert.StartsWith("dernière écriture : ÉCHEC — IOException : x", vm.TexteEtatJournal);
        journal.DerniereErreur = null;

        // 16 min après le DÉMARRAGE sans écriture : là, Chronos tourne et se tait.
        clock.UtcNow = Now + TimeSpan.FromMinutes(16);
        vm.ApplySnapshot(SnapSimple());
        Assert.True(vm.AlerteJournal);
        Assert.StartsWith("journal muet depuis 16 min", vm.TexteEtatJournal);
    }

    // ------------------------------------------------------------------ TOK-02 (phase 33, 33-05) : la progression de la reconstruction exposée au VM
    // Deux propriétés observables, relues au tick comme l'état du journal (D-32-21 / D-33-22) : le VM du cadran formate des ENTIERS en
    // texte, jamais une fraction ni un pourcentage ; la fenêtre Historique (phase 34) consommera IEtatReconstruction directement.

    /// <summary>VM construit à <see cref="Now"/>, état de reconstruction injecté par l'argument nommé <c>reconstruction:</c>.</summary>
    private static MainViewModel VmAvecReconstruction(FakeEtatReconstruction? reconstruction, out FakeClock clock)
    {
        clock = new FakeClock(Now);
        return Build(new FakeUiDispatcher { OnUiThread = true }, clock, new FakeUsageProvider(), new FakeWindowController(),
                     new FakeAutostartService(), new SettingsService(TempPaths()), reconstruction: reconstruction);
    }

    [Fact]
    public void Le_texte_de_reconstruction_suit_la_progression_au_tick_et_disparait_en_incremental()
    {
        var etat = new FakeEtatReconstruction { Phase = PhaseReconstruction.Reconstruction, FichiersTraites = 0, FichiersTotal = 1603 };
        var vm = VmAvecReconstruction(etat, out _);

        // Dès la construction : la passe a commencé, rien n'est encore disponible.
        Assert.True(vm.AfficherReconstruction);
        Assert.Equal("reconstruction des tokens — 0 / 1603 fichiers", vm.TexteReconstruction);

        // Un tick plus tard : la semaine courante est sur le disque, la progression a avancé (relue, pas poussée).
        etat.FichiersTraites = 886;
        etat.SemaineCouranteDisponible = true;
        vm.ApplySnapshot(SnapSimple());
        Assert.Equal("reconstruction des tokens — 886 / 1603 fichiers · la semaine courante est déjà complète", vm.TexteReconstruction);
        Assert.True(vm.AfficherReconstruction);

        // Incrémental : tout est à jour, le VM se tait.
        etat.Phase = PhaseReconstruction.Incremental;
        etat.FichiersTraites = 1603;
        vm.ApplySnapshot(SnapSimple());
        Assert.False(vm.AfficherReconstruction);
        Assert.Equal("", vm.TexteReconstruction);

        // Échec : la cause est dite, sans mourir.
        etat.Phase = PhaseReconstruction.EnEchec;
        etat.DerniereErreur = "IOException : x";
        vm.ApplySnapshot(SnapSimple());
        Assert.True(vm.AfficherReconstruction);
        Assert.Equal("agrégats de tokens : ÉCHEC — IOException : x", vm.TexteReconstruction);

        // Jamais un pourcentage, dans aucun des états traversés.
        Assert.DoesNotContain("%", vm.TexteReconstruction);
    }

    [Fact]
    public void Sans_etat_de_reconstruction_rien_n_est_affiche()
    {
        var vm = VmAvecReconstruction(reconstruction: null, out _);

        Assert.False(vm.AfficherReconstruction);
        Assert.Equal("", vm.TexteReconstruction);

        vm.ApplySnapshot(SnapSimple());
        Assert.False(vm.AfficherReconstruction);
        Assert.Equal("", vm.TexteReconstruction);
    }
}
