using System.ComponentModel;
using System.IO;
using Chronos.Rendering;
using Chronos.Services;
using Chronos.ViewModels;
using Xunit;
using Orientation = System.Windows.Controls.Orientation;

namespace Chronos.Tests;

/// <summary>
/// Phase 40 (CAD-04) — orientation PAR CADRAN dans <see cref="MainViewModel"/> : chaque cadran rectangulaire (Fusible, Marée, Volets)
/// mémorise la sienne, la commande <c>ChoisirOrientation</c> ne change que celle du cadran courant, la persiste (relecture disque
/// fraîche, GAP-1) et ne touche jamais <c>VerticalLayout</c> (widget de sessions). L'empreinte courante (<c>LargeurCadran</c> /
/// <c>HauteurCadran</c>) suit le style et l'orientation. Faits purs : le VM se construit hors STA (motif de MainViewModelTests).
/// Chemins sous <c>Path.GetTempPath()</c> : aucun test n'écrit le vrai <c>%APPDATA%\Chronos</c>.
/// </summary>
public class OrientationCadranTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private static ChronosPaths TempPaths()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ChronosOrientationTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Assert.StartsWith(Path.GetTempPath(), dir);
        return new ChronosPaths(Path.Combine(dir, "usage.json"), Path.Combine(dir, "projects"));
    }

    /// <summary>VM déterministe (orchestrateur jamais démarré, faux partout) sur les chemins donnés.</summary>
    private static MainViewModel NouveauVm(ChronosPaths paths)
    {
        var settings = new SettingsService(paths);
        var provider = new FakeUsageProvider();
        var orch = new RefreshOrchestrator(provider, RefreshOptions.Default);
        var clock = new FakeClock(Now);
        return new MainViewModel(orch, new FakeUiDispatcher { OnUiThread = true }, clock,
            new FakeWindowController(), new FakeAutostartService(), settings,
            new DiagnosticService(paths, settings, provider, clock),
            new FakeOAuthLogin(), new FakeSessionsController(), new FakeAuthStatus());
    }

    [Fact]
    public void Les_orientations_initiales_viennent_des_reglages()
    {
        var paths = TempPaths();
        var settings = new SettingsService(paths);
        settings.Save(settings.Load() with { OrientationFusible = OrientationCadran.Vertical });

        var vm = NouveauVm(paths);

        Assert.Equal(Orientation.Vertical, vm.OrientationFusible);
        Assert.Equal(Orientation.Vertical, vm.OrientationMaree);     // défaut de Marée
        Assert.Equal(Orientation.Horizontal, vm.OrientationVolets);  // défaut de Volets
    }

    [Fact]
    public void Chaque_cadran_memorise_son_orientation()
    {
        var vm = NouveauVm(TempPaths());

        vm.CadranStyle = CadranStyle.Fusible;
        Assert.True(vm.EstOrientationHorizontale);
        vm.ChoisirOrientationCommand.Execute(OrientationCadran.Vertical);
        Assert.Equal(Orientation.Vertical, vm.OrientationFusible);
        Assert.True(vm.EstOrientationVerticale);

        // Marée garde SA propre orientation (verticale par défaut), elle n'hérite pas de celle de Fusible.
        vm.CadranStyle = CadranStyle.Maree;
        Assert.Equal(Orientation.Vertical, vm.OrientationMaree);
        Assert.True(vm.EstOrientationVerticale);

        vm.ChoisirOrientationCommand.Execute(OrientationCadran.Horizontal);
        Assert.Equal(Orientation.Horizontal, vm.OrientationMaree);
        Assert.Equal(Orientation.Vertical, vm.OrientationFusible);   // inchangée
        Assert.True(vm.EstOrientationHorizontale);

        // Retour à Fusible : on la retrouve verticale.
        vm.CadranStyle = CadranStyle.Fusible;
        Assert.True(vm.EstOrientationVerticale);
        Assert.False(vm.EstOrientationHorizontale);
    }

    [Fact]
    public void L_orientation_est_persistee_et_relue()
    {
        var paths = TempPaths();
        var vm = NouveauVm(paths);

        vm.CadranStyle = CadranStyle.Fusible;
        vm.ChoisirOrientationCommand.Execute(OrientationCadran.Vertical);
        vm.CadranStyle = CadranStyle.Maree;
        vm.ChoisirOrientationCommand.Execute(OrientationCadran.Horizontal);

        var lus = new SettingsService(paths).Load();
        Assert.Equal(OrientationCadran.Vertical, lus.OrientationFusible);
        Assert.Equal(OrientationCadran.Horizontal, lus.OrientationMaree);
        Assert.Equal(OrientationCadran.Horizontal, lus.OrientationVolets);

        var relu = NouveauVm(paths);
        Assert.Equal(Orientation.Vertical, relu.OrientationFusible);
        Assert.Equal(Orientation.Horizontal, relu.OrientationMaree);
        Assert.Equal(Orientation.Horizontal, relu.OrientationVolets);
    }

    [Fact]
    public void La_commande_ne_reecrit_pas_un_reglage_ecrit_ailleurs()
    {
        // GAP-1 : un autre writer écrit sur disque APRÈS la construction du VM ; la commande relit le disque avant Save.
        var paths = TempPaths();
        var settings = new SettingsService(paths);
        var vm = NouveauVm(paths);
        settings.Save(settings.Load() with { ThemeKey = "aurore" });

        vm.CadranStyle = CadranStyle.Volets;
        vm.ChoisirOrientationCommand.Execute(OrientationCadran.Vertical);

        var lus = settings.Load();
        Assert.Equal("aurore", lus.ThemeKey);
        Assert.Equal(OrientationCadran.Vertical, lus.OrientationVolets);
    }

    [Theory]
    [InlineData(CadranStyle.Arcs)]
    [InlineData(CadranStyle.Braises)]
    public void La_commande_est_sans_effet_pour_Arcs_et_Braises(CadranStyle style)
    {
        var paths = TempPaths();
        var vm = NouveauVm(paths);
        vm.CadranStyle = style;

        Assert.False(vm.EstStyleOrientable);
        Assert.False(vm.EstOrientationHorizontale);
        Assert.False(vm.EstOrientationVerticale);

        vm.ChoisirOrientationCommand.Execute(OrientationCadran.Vertical);
        vm.ChoisirOrientationCommand.Execute(OrientationCadran.Horizontal);

        Assert.Equal(Orientation.Horizontal, vm.OrientationFusible);
        Assert.Equal(Orientation.Vertical, vm.OrientationMaree);
        Assert.Equal(Orientation.Horizontal, vm.OrientationVolets);

        var lus = new SettingsService(paths).Load();
        Assert.Equal(OrientationCadran.Horizontal, lus.OrientationFusible);
        Assert.Equal(OrientationCadran.Vertical, lus.OrientationMaree);
        Assert.Equal(OrientationCadran.Horizontal, lus.OrientationVolets);
    }

    [Fact]
    public void L_orientation_des_cadrans_est_independante_de_la_disposition_verticale()
    {
        var paths = TempPaths();
        var settings = new SettingsService(paths);
        var vm = NouveauVm(paths);
        Assert.False(vm.VerticalLayout);

        vm.CadranStyle = CadranStyle.Fusible;
        vm.ChoisirOrientationCommand.Execute(OrientationCadran.Vertical);
        Assert.False(vm.VerticalLayout);
        Assert.False(settings.Load().VerticalLayout);

        vm.ToggleVerticalLayoutCommand.Execute(null);
        Assert.True(vm.VerticalLayout);
        Assert.Equal(Orientation.Vertical, vm.OrientationFusible);
        Assert.Equal(Orientation.Vertical, vm.OrientationMaree);
        Assert.Equal(Orientation.Horizontal, vm.OrientationVolets);

        var lus = settings.Load();
        Assert.True(lus.VerticalLayout);
        Assert.Equal(OrientationCadran.Vertical, lus.OrientationFusible);
        Assert.Equal(OrientationCadran.Vertical, lus.OrientationMaree);
        Assert.Equal(OrientationCadran.Horizontal, lus.OrientationVolets);
    }

    /// <summary>Les huit variantes (style, orientation choisie) du contrat d'empreinte.</summary>
    public static readonly (CadranStyle Style, OrientationCadran Orientation)[] Variantes =
    {
        (CadranStyle.Arcs, OrientationCadran.Horizontal),
        (CadranStyle.Braises, OrientationCadran.Horizontal),
        (CadranStyle.Fusible, OrientationCadran.Horizontal),
        (CadranStyle.Fusible, OrientationCadran.Vertical),
        (CadranStyle.Maree, OrientationCadran.Vertical),
        (CadranStyle.Maree, OrientationCadran.Horizontal),
        (CadranStyle.Volets, OrientationCadran.Horizontal),
        (CadranStyle.Volets, OrientationCadran.Vertical),
    };

    [Fact]
    public void L_empreinte_suit_le_style_et_l_orientation()
    {
        var vm = NouveauVm(TempPaths());

        foreach (var (style, orientation) in Variantes)
        {
            vm.CadranStyle = style;
            vm.ChoisirOrientationCommand.Execute(orientation);   // sans effet pour Arcs / Braises
            var attendu = EmpreinteCadran.Pour(style, orientation);
            Assert.True(attendu.Width == vm.LargeurCadran, $"{style}/{orientation} : largeur {vm.LargeurCadran} ≠ {attendu.Width}");
            Assert.True(attendu.Height == vm.HauteurCadran, $"{style}/{orientation} : hauteur {vm.HauteurCadran} ≠ {attendu.Height}");
        }

        // Changer d'orientation notifie l'empreinte…
        vm.CadranStyle = CadranStyle.Fusible;
        var notifiees = new List<string?>();
        PropertyChangedEventHandler h = (_, e) => notifiees.Add(e.PropertyName);
        vm.PropertyChanged += h;
        Assert.Equal(Orientation.Vertical, vm.OrientationFusible);   // laissée verticale par la boucle
        vm.ChoisirOrientationCommand.Execute(OrientationCadran.Horizontal);
        Assert.Contains(nameof(MainViewModel.LargeurCadran), notifiees);
        Assert.Contains(nameof(MainViewModel.HauteurCadran), notifiees);

        // … et changer de style aussi.
        notifiees.Clear();
        vm.CadranStyle = CadranStyle.Volets;
        Assert.Contains(nameof(MainViewModel.LargeurCadran), notifiees);
        Assert.Contains(nameof(MainViewModel.HauteurCadran), notifiees);
        Assert.Contains(nameof(MainViewModel.EstStyleOrientable), notifiees);
        vm.PropertyChanged -= h;
    }

    /// <summary>§11 B1 : la hauteur de fenêtre ajoute la bande de 14 px des pastilles aux trois cadrans rectangulaires
    /// (Arcs et Braises : 0), et la notifie au changement de style comme d'orientation.</summary>
    [Fact]
    public void La_hauteur_de_fenetre_ajoute_la_bande_pour_les_cadrans_rectangulaires()
    {
        var vm = NouveauVm(TempPaths());
        var notifiees = new List<string?>();
        vm.PropertyChanged += (_, e) => notifiees.Add(e.PropertyName);

        foreach (var (style, bande) in new[]
                 {
                     (CadranStyle.Fusible, 14.0), (CadranStyle.Arcs, 0.0), (CadranStyle.Maree, 14.0),
                     (CadranStyle.Braises, 0.0), (CadranStyle.Volets, 14.0),
                 })
        {
            notifiees.Clear();
            vm.CadranStyle = style;
            Assert.Equal(bande, vm.HauteurBandePastilles);
            Assert.Equal(vm.HauteurCadran + vm.HauteurBandePastilles, vm.HauteurFenetre);
            Assert.Contains(nameof(MainViewModel.HauteurFenetre), notifiees);
            Assert.Contains(nameof(MainViewModel.HauteurBandePastilles), notifiees);
        }

        vm.CadranStyle = CadranStyle.Fusible;
        foreach (var orientation in new[] { OrientationCadran.Vertical, OrientationCadran.Horizontal })
        {
            notifiees.Clear();
            vm.ChoisirOrientationCommand.Execute(orientation);
            Assert.Equal(14, vm.HauteurBandePastilles);
            Assert.Equal(EmpreinteCadran.Pour(CadranStyle.Fusible, orientation).Height + 14, vm.HauteurFenetre);
            Assert.Contains(nameof(MainViewModel.HauteurFenetre), notifiees);
            Assert.Contains(nameof(MainViewModel.HauteurBandePastilles), notifiees);
        }
    }
}
