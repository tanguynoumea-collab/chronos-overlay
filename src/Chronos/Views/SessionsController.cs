using System.Diagnostics;
using System.Windows;
using Chronos.Services;
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

    private SessionsWindow? _window;

    public SessionsController(SessionHookInstaller installer, SettingsService settings, SessionMonitor monitor, IClock clock)
    {
        _installer = installer;
        _settings = settings;
        _monitor = monitor;
        _clock = clock;
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

    private void ShowWindow()
    {
        if (_window is null)
        {
            var vm = new SessionsViewModel(_monitor, _clock);
            _window = new SessionsWindow(vm);
            var s = _settings.Load();
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
