using System.IO;
using System.Windows;
using System.Windows.Input;
using Chronos.Models;
using Chronos.Services;
using Chronos.ViewModels;
using Chronos.Views;
using Chronos.Views.Reglages;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Quick 260927-reglages-v2 — l'ouvreur de la fenêtre de réglages est un SINGLETON (même principe qu'<c>OuvreurHistorique</c>,
/// 35-02) : deux clics droits = une fenêtre, ramenée au premier plan ; une fenêtre fermée est recréée ; réduite, elle revient en
/// Normal. Et le clic droit du cadran passe PAR lui : plus de <c>new SettingsWindow(_vm) { Owner = this }</c> dans la vue.
/// Fenêtres de test hors écran, non activées, hors barre des tâches, toujours fermées en fin de test.
/// </summary>
[Collection("XAML WPF")]
public class OuvreurReglagesTests
{
    private readonly List<Window> _creees = new();

    private Func<Window> Fabrique() => () =>
    {
        var w = new Window
        {
            Left = -20000, Top = -20000, Width = 40, Height = 40,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
        };
        _creees.Add(w);
        return w;
    };

    private void FermerTout()
    {
        foreach (var w in _creees)
            if (w.IsLoaded) w.Close();
    }

    [WpfFact]
    public void Deux_ouvertures_une_seule_fenetre()
    {
        try
        {
            var ouvreur = new OuvreurReglages(Fabrique());

            ouvreur.Ouvrir();
            ouvreur.Ouvrir();

            Assert.Single(_creees);
            Assert.True(_creees[0].IsVisible);
        }
        finally { FermerTout(); }
    }

    [WpfFact]
    public void Apres_fermeture_une_nouvelle_fenetre_est_creee()
    {
        try
        {
            var ouvreur = new OuvreurReglages(Fabrique());

            ouvreur.Ouvrir();
            var premiere = _creees[0];
            premiere.Close();
            ouvreur.Ouvrir();

            Assert.Equal(2, _creees.Count);
            Assert.NotSame(premiere, _creees[1]);
            Assert.True(_creees[1].IsVisible);
            Assert.False(premiere.IsVisible);
        }
        finally { FermerTout(); }
    }

    [WpfFact]
    public void Une_fenetre_reduite_revient_en_normal()
    {
        try
        {
            var ouvreur = new OuvreurReglages(Fabrique());

            ouvreur.Ouvrir();
            _creees[0].WindowState = System.Windows.WindowState.Minimized;
            ouvreur.Ouvrir();

            Assert.Single(_creees);
            Assert.Equal(System.Windows.WindowState.Normal, _creees[0].WindowState);
        }
        finally { FermerTout(); }
    }

    private static ChronosPaths TempPaths()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ChronosOuvreurReglages_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Assert.StartsWith(Path.GetTempPath(), dir);
        return new ChronosPaths(Path.Combine(dir, "usage.json"), Path.Combine(dir, "projects"));
    }

    /// <summary>Le clic droit sur le cadran demande l'ouverture à l'ouvreur injecté — une fois par clic, et rien d'autre : aucune
    /// fenêtre n'est fabriquée par la vue (le faux ne crée rien ; l'ancien code aurait levé en posant un <c>Owner</c> sur un cadran
    /// jamais affiché).</summary>
    [WpfFact]
    public void Le_clic_droit_du_cadran_passe_par_l_ouvreur()
    {
        var paths = TempPaths();
        var prov = new FakeUsageProvider();
        var orch = new RefreshOrchestrator(prov, paths, RefreshOptions.Default);
        var settings = new SettingsService(paths);
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));
        var vm = new MainViewModel(orch, new FakeUiDispatcher { OnUiThread = true }, clock,
            new FakeWindowController(), new FakeAutostartService(), new FakeRecalibrationPrompt(), settings,
            new DiagnosticService(new FakeClaudeTokenReader(), paths, settings, prov, clock, machine: new FakeInventaireMachine()),
            new FakeStatusLineSetup(), new FakeOAuthLogin(), new FakeSessionsController(), new FakeAuthStatus());
        var guard = new TopmostGuard();
        var ouvreur = new FakeOuvreurReglages();
        var cadran = new MainWindow(vm, guard, new OverlayController(guard, new SettingsService(paths)), ouvreur);
        var racine = Assert.IsAssignableFrom<UIElement>(cadran.Content);

        foreach (var _ in Enumerable.Range(0, 2))
            racine.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right)
            {
                RoutedEvent = UIElement.MouseRightButtonUpEvent,
            });

        Assert.Equal(2, ouvreur.Ouvertures);
    }
}
