using System.IO;
using System.Windows;
using System.Windows.Media;
using Chronos.Services;
using Chronos.ViewModels;
using Chronos.ViewModels.Historique;
using Chronos.Views.Reglages;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Quick 260927-reglages-v2 — montage commun de la fenêtre de réglages pour les tests STA (collection « XAML WPF »).
///
/// Une Window jamais affichée n'a pas de template appliqué : son <c>Content</c> n'a AUCUN parent visuel, donc le DataContext ne
/// se propage pas et aucun binding ne s'évalue (piège vécu au plan 17-05). D'où : DataContext posé sur la racine du contenu, purge
/// de la file du Dispatcher (la réévaluation déclenchée par un changement de DataContext est DIFFÉRÉE), puis Measure/Arrange de la
/// racine à la taille de la fenêtre (le <c>WindowChrome</c> fait de toute la fenêtre une zone cliente).
/// Chemins sous <c>Path.GetTempPath()</c> : aucun test n'écrit le vrai <c>%APPDATA%\Chronos</c>.
/// </summary>
internal static class MontageReglages
{
    internal static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    internal static readonly Size Minimale = new(640, 440);
    internal static readonly Size ParDefaut = new(860, 580);
    internal static readonly Size Grande = new(1400, 900);

    internal static ChronosPaths TempPaths()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ChronosReglagesTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Assert.StartsWith(Path.GetTempPath(), dir);
        return new ChronosPaths(Path.Combine(dir, "usage.json"), Path.Combine(dir, "projects"));
    }

    /// <summary>Un <see cref="MainViewModel"/> déterministe (orchestrateur jamais démarré, faux partout), sur des chemins temporaires.</summary>
    internal static MainViewModel NouveauVm(
        Func<ChronosSettings, ChronosSettings>? reglagesInitiaux = null,
        FakeEtatJournal? journal = null, FakeClock? clock = null,
        HistoriqueViewModel? historique = null, IOuvreurHistorique? ouvreur = null,
        FakeSessionsController? sessions = null, FakeStatusLineSetup? barreStatut = null,
        FakePressePapiers? pressePapiers = null)
    {
        var paths = TempPaths();
        var settings = new SettingsService(paths);
        if (reglagesInitiaux is not null) settings.Save(reglagesInitiaux(settings.Load()));

        var provider = new FakeUsageProvider();
        var orch = new RefreshOrchestrator(provider, paths, RefreshOptions.Default); // JAMAIS démarré : aucun I/O
        clock ??= new FakeClock(Now);
        return new MainViewModel(orch, new FakeUiDispatcher { OnUiThread = true }, clock,
            new FakeWindowController(), new FakeAutostartService(), new FakeRecalibrationPrompt(),
            settings,
            new DiagnosticService(new FakeClaudeTokenReader(), paths, settings, provider, clock, machine: new FakeInventaireMachine()),
            barreStatut ?? new FakeStatusLineSetup(), new FakeOAuthLogin(), sessions ?? new FakeSessionsController(),
            new FakeAuthStatus(), new FakeEtatServeur(), journal, ouvreurHistorique: ouvreur, historique: historique,
            pressePapiers: pressePapiers);
    }

    /// <summary>Construit la fenêtre sur la section voulue et la met en page à <paramref name="taille"/>.</summary>
    internal static ReglagesWindow Monter(MainViewModel vm, SectionReglages section, Size? taille = null, Visual? cadranReel = null)
    {
        vm.Reglages.AllerACommand.Execute(section);   // persistée : la fenêtre la relit à la construction (Ouvrir)
        var fenetre = new ReglagesWindow(vm, cadranReel);
        var racine = Racine(fenetre);
        racine.DataContext = vm;
        MettreEnPage(fenetre, taille ?? ParDefaut);
        return fenetre;
    }

    internal static FrameworkElement Racine(ReglagesWindow fenetre) => Assert.IsAssignableFrom<FrameworkElement>(fenetre.Content);

    /// <summary>Purge le Dispatcher puis Measure/Arrange la racine à la taille donnée (réutilisable après un changement d'état).</summary>
    internal static void MettreEnPage(ReglagesWindow fenetre, Size taille)
    {
        var racine = Racine(fenetre);
        Purger(racine);
        racine.Measure(taille);
        racine.Arrange(new Rect(taille));
        racine.UpdateLayout();
        Purger(racine);
    }

    internal static void Purger(FrameworkElement e)
        => e.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

    /// <summary>Tous les descendants VISUELS (seul l'arbre visuel est peuplé après Arrange), racine comprise.</summary>
    internal static IEnumerable<DependencyObject> Descendants(DependencyObject racine)
    {
        yield return racine;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(racine); i++)
            foreach (var d in Descendants(VisualTreeHelper.GetChild(racine, i)))
                yield return d;
    }

    /// <summary>Affiché au sens résolu : l'élément ET tous ses ancêtres jusqu'à <paramref name="racine"/> sont <c>Visible</c>.</summary>
    internal static bool EstAffiche(DependencyObject e, DependencyObject racine)
    {
        for (DependencyObject? d = e; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (d is UIElement u && u.Visibility != Visibility.Visible) return false;
            if (ReferenceEquals(d, racine)) return true;
        }
        return false;
    }
}
