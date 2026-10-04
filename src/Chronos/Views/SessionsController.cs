using System.Diagnostics;
using System.Windows;
using Chronos.Services;
using Chronos.Theming;
using Chronos.ViewModels;

namespace Chronos.Views;

/// <summary>
/// Implémentation WPF d'<see cref="ISessionsController"/> : gère le cycle de vie du <see cref="SessionsWindow"/>,
/// l'installation des hooks (<see cref="SessionHookInstaller"/>) et la persistance (activation + position).
/// Vit dans Views (manipule des fenêtres). GAP-1 pour la persistance (relire le disque avant d'écrire).
/// </summary>
public sealed class SessionsController : ISessionsController
{
    private readonly SessionHookInstaller _installer;
    private readonly SettingsService _settings;
    private readonly SessionMonitor _monitor;
    private readonly IClock _clock;
    private readonly ArchiveStore _archive;
    private readonly TreatedStore _treated;

    private SessionsWindow? _window;
    private SessionsViewModel? _vm;

    public SessionsController(SessionHookInstaller installer, SettingsService settings, SessionMonitor monitor, IClock clock, ArchiveStore archive, TreatedStore treated)
    {
        _installer = installer;
        _settings = settings;
        _monitor = monitor;
        _clock = clock;
        _archive = archive;
        _treated = treated;
    }

    private static string ExePath =>
        System.Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "Chronos.exe";

    public bool IsEnabled => _settings.Load().SessionsWidgetEnabled;

    public void Enable()
    {
        try
        {
            // Hooks = précision « permission » pour le terminal (bonus) : le widget s'active TOUJOURS, même si la passerelle
            // refuse d'écrire (fichier vide, illisible, modifié entre-temps, lien symbolique) — elle ne lève jamais.
            var bilan = _installer.Install(ExePath);
            // Choix EXPLICITE de l'utilisateur ⇒ le marqueur de quarantaine des réglages est effacé (arbitrage ZEUS).
            Persist(s => s with { SessionsWidgetEnabled = true, QuarantaineReglagesDepuis = null });
            ShowWindow();
            // Le texte vient du producteur des mots (réserve R9) : il dit ce que l'écran dit, et rien d'autre.
            var texte = AffichageSessions.TexteActivation();
            if (!bilan.Ecrit && bilan.Cause != "déjà conforme")
                texte += "\n\nHooks non installés : " + bilan.Cause + " — ~/.claude/settings.json n'a pas été modifié.";
            MessageBox.Show(Owner(), texte, "Chronos", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(Owner(), "Impossible d'installer les hooks :\n" + ex.Message, "Chronos");
        }
    }

    public void Disable()
    {
        try
        {
            // Choix EXPLICITE de l'utilisateur ⇒ le marqueur de quarantaine des réglages est effacé (arbitrage ZEUS).
            Persist(s => s with { SessionsWidgetEnabled = false, QuarantaineReglagesDepuis = null });
            _installer.Uninstall();   // ne lève jamais ; un refus (fichier illisible…) n'empêche pas la désactivation
            _window?.Hide();
        }
        catch { }
    }

    public void ShowIfEnabled()
    {
        if (IsEnabled) ShowWindow();
    }

    /// <summary>Applique le style à la fenêtre live (si présente). Persistance = côté MainViewModel.</summary>
    public void SetStyle(SessionStyle style)
    {
        if (_vm is not null) _vm.Style = style;
    }

    /// <summary>Bascule la disposition verticale des styles en rangée sur la fenêtre live. Persistance = MainViewModel.</summary>
    public void SetVerticalLayout(bool vertical)
    {
        if (_vm is not null) _vm.Vertical = vertical;
    }

    /// <summary>Applique le thème au widget de sessions (couleurs d'état côté VM + fonds/texte côté fenêtre).</summary>
    public void SetTheme(ChronosTheme theme)
    {
        _vm?.SetTheme(theme);
        _window?.ApplyThemeBrushes(theme);
    }

    private void ShowWindow()
    {
        if (_window is null)
        {
            var s = _settings.Load();
            var vm = new SessionsViewModel(_monitor, _clock, _archive, _treated)
            {
                Style = s.SessionStyle,          // style persisté
                Vertical = s.VerticalLayout,     // disposition (colonne) persistée
            };
            _vm = vm;
            _window = new SessionsWindow(vm);
            var theme = ThemeCatalog.ByKey(s.ThemeKey);   // même thème que le cadran (cohérence)
            vm.SetTheme(theme);
            _window.ApplyThemeBrushes(theme);
            if (s.SessionsX is { } x && s.SessionsY is { } y) { _window.Left = x; _window.Top = y; }
            else { _window.Left = 80; _window.Top = 80; }
            // FIAB-2 : persister à la FIN du glisser, plus à chaque changement de position (des dizaines de cycles temp + Move par
            // seconde pendant un glisser, sur le thread UI).
            _window.DeplacementTermine += (_, _) => PersistPosition();
            vm.StartClock();
        }
        _window.Show();
    }

    private void PersistPosition()
    {
        if (_window is null) return;
        var x = _window.Left;
        var y = _window.Top;
        Persist(s => s with { SessionsX = x, SessionsY = y });
    }

    private void Persist(System.Func<ChronosSettings, ChronosSettings> mutate)
        => _settings.Modifier(mutate); // GAP-1 : lire-modifier-écrire sous verrou, ne lève jamais (42.2-02)

    private static Window? Owner() => Application.Current?.MainWindow;
}
