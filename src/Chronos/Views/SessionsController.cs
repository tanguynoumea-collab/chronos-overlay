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

    private SessionsWindow? _window;
    private SessionsViewModel? _vm;

    public SessionsController(SessionHookInstaller installer, SettingsService settings, SessionMonitor monitor, IClock clock, ArchiveStore archive)
    {
        _installer = installer;
        _settings = settings;
        _monitor = monitor;
        _clock = clock;
        _archive = archive;
    }

    private static string ExePath =>
        System.Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "Chronos.exe";

    public bool IsEnabled => _settings.Load().SessionsWidgetEnabled;

    public void Enable()
    {
        try
        {
            _installer.Install(ExePath); // hooks = précision « permission » pour le terminal (bonus)
            Persist(s => s with { SessionsWidgetEnabled = true });
            ShowWindow();
            MessageBox.Show(Owner(),
                "Widget de sessions activé.\n\nIl affiche tes sessions Claude Code ACTIVES (app bureau incluse),\n" +
                "détectées via leurs transcripts. Une session apparaît dès qu'elle a de l'activité récente\n" +
                "et indique si elle a fini son tour (elle t'attend) ou travaille encore.",
                "Chronos", MessageBoxButton.OK, MessageBoxImage.Information);
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
            Persist(s => s with { SessionsWidgetEnabled = false });
            try { _installer.Uninstall(ExePath); } catch { }
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
            var vm = new SessionsViewModel(_monitor, _clock, _archive)
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
            _window.LocationChanged += (_, _) => PersistPosition();
            vm.StartClock();
        }
        _window.Show();
    }

    private void PersistPosition()
    {
        if (_window is null) return;
        Persist(s => s with { SessionsX = _window.Left, SessionsY = _window.Top });
    }

    private void Persist(System.Func<ChronosSettings, ChronosSettings> mutate)
        => _settings.Save(mutate(_settings.Load())); // GAP-1

    private static Window? Owner() => Application.Current?.MainWindow;
}
