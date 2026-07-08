using Chronos.Models;
using Chronos.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chronos.ViewModels;

/// <summary>
/// ViewModel racine (temps réel). S'abonne à l'horloge DONNÉES (<see cref="RefreshOrchestrator.SnapshotChanged"/>,
/// émis sur le thread pool) et franchit la frontière de thread EN UN SEUL POINT via <see cref="IUiDispatcher.Post"/>
/// (RAF-04). L'affichage vit grâce à <see cref="Interpolate"/> (PUR, aucun I/O — RAF-03), piloté par un
/// DispatcherTimer 1 s créé côté UI (<see cref="StartClock"/>) — jamais dans le ctor (Pitfall 4 : tests en [Fact] simple).
///
/// 06-04 : expose les 4 commandes du menu contextuel (SEUL point d'accès/sortie, FEN-06) —
/// arrière-plan (FEN-05), recalibrage hebdo best-effort (ROB-03), lancer au démarrage (DEP-02), quitter.
/// Le recalibrage est appliqué DANS le pipeline temps réel (ApplySnapshot) via la fonction pure
/// <see cref="WeeklyRecalibration"/> : il ne recale que le repli et CONSERVE le badge « estimée » (honnêteté).
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IUiDispatcher _ui;
    private readonly IClock _clock;
    private readonly IWindowController _controller;
    private readonly IAutostartService _autostart;
    private readonly IRecalibrationPrompt _prompt;
    private readonly SettingsService _settingsService;

    private ChronosSettings _settings;   // état persisté courant (coin/mode/ancre)
    private UsageSnapshot? _last;         // dernier snapshot appliqué (pour ré-appliquer après recalibrage)

    public WindowGaugeViewModel FiveHour { get; } = new(TimeSpan.FromHours(5));
    public WindowGaugeViewModel SevenDay { get; } = new(TimeSpan.FromDays(7));

    [ObservableProperty] private bool _dataUnavailable;
    [ObservableProperty] private DateTimeOffset? _capturedAt; // staleness pour l'UI Phase 5
    [ObservableProperty] private bool _isStale;

    // État reflété dans les items « à cocher » du menu (FEN-05 / DEP-02).
    [ObservableProperty] private bool _isBackground;
    [ObservableProperty] private bool _isAutostart;

    public MainViewModel(
        RefreshOrchestrator orchestrator, IUiDispatcher ui, IClock clock,
        IWindowController controller, IAutostartService autostart,
        IRecalibrationPrompt prompt, SettingsService settings)
    {
        _ui = ui;
        _clock = clock;
        _controller = controller;
        _autostart = autostart;
        _prompt = prompt;
        _settingsService = settings;
        _settings = settings.Load();

        // État initial des toggles du menu : miroir de l'état RÉEL (settings + service autostart).
        IsBackground = _settings.Background;
        IsAutostart = _autostart.IsEnabled();

        orchestrator.SnapshotChanged += OnSnapshotChanged; // callback thread pool (horloge données)
    }

    // FRONTIÈRE DE THREAD — franchie UNE seule fois (RAF-04). Aucune mutation d'ObservableProperty hors d'ici.
    private void OnSnapshotChanged(object? sender, UsageSnapshot snap) => _ui.Post(() => ApplySnapshot(snap));

    /// <summary>Applique un snapshot (thread UI) : recalibre le repli hebdo, pousse chaque fenêtre, l'état global, puis rend.</summary>
    internal void ApplySnapshot(UsageSnapshot snap)
    {
        _last = snap; // mémorisé pour une éventuelle ré-application après recalibrage

        // ROB-03 : recalibrage best-effort AVANT SevenDay.Apply. La fonction pure ne touche PAS une
        // source exacte (les chiffres exacts priment) et conserve Estimated pour le repli → badge « estimée ».
        var weekly = WeeklyRecalibration.Apply(snap.SevenDay, _settings.WeeklyAnchor, _clock.UtcNow);

        FiveHour.Apply(snap.FiveHour);
        SevenDay.Apply(weekly);
        CapturedAt = snap.SourceCapturedAt;
        DataUnavailable = snap.FiveHour.Reliability == SourceReliability.Unavailable
                       && snap.SevenDay.Reliability == SourceReliability.Unavailable;
        Interpolate(_clock.UtcNow); // premier rendu immédiat (pas d'overlay vide entre deux ticks)
    }

    /// <summary>PUR, aucun I/O (RAF-03) — appelé chaque seconde par le DispatcherTimer (StartClock).</summary>
    internal void Interpolate(DateTimeOffset now)
    {
        FiveHour.Interpolate(now);
        SevenDay.Interpolate(now);
        IsStale = CapturedAt is { } c && (now - c) > TimeSpan.FromMinutes(2);
    }

    /// <summary>Démarre l'horloge UI 1 s (RAF-03). Créé côté UI UNIQUEMENT (jamais dans le ctor → Pitfall 4).</summary>
    public void StartClock()
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Interpolate(_clock.UtcNow);
        timer.Start();
    }

    // --- Commandes du menu contextuel (FEN-06 : SEUL point d'accès/sortie) ---

    /// <summary>FEN-05 : bascule le mode arrière-plan et pilote le controller (Topmost/HWND_BOTTOM).</summary>
    [RelayCommand]
    private void ToggleBackground()
    {
        IsBackground = !IsBackground;
        if (IsBackground) _controller.SendToBackground();
        else _controller.BringToForeground();
    }

    /// <summary>DEP-02 : bascule le lancement au démarrage et reflète l'état RÉEL du raccourci (.lnk).</summary>
    [RelayCommand]
    private void ToggleAutostart()
    {
        if (_autostart.IsEnabled()) _autostart.Disable();
        else _autostart.Enable();
        IsAutostart = _autostart.IsEnabled();
    }

    /// <summary>
    /// ROB-03 : demande une ancre de reset hebdo ; si fournie, la persiste et ré-applique le dernier
    /// snapshot → l'arc hebdo se recale MAIS reste « estimée ». Annulation → aucun changement.
    /// </summary>
    [RelayCommand]
    private void Recalibrate()
    {
        var anchor = _prompt.Ask(_settings.WeeklyAnchor);
        if (anchor is null) return;

        // GAP-1 : relire l'état DISQUE avant d'écrire — l'OverlayController persiste coin/écran/arrière-plan
        // indépendamment ; sauvegarder la copie du constructeur écraserait ces réglages plus récents.
        _settings = _settingsService.Load() with { WeeklyAnchor = anchor };
        _settingsService.Save(_settings);
        if (_last is { } s) ApplySnapshot(s); // ré-applique → arc hebdo recalé, badge « estimée » conservé
    }

    /// <summary>FEN-06 : ferme l'application (seul point de sortie d'une fenêtre sans barre de titre ni des tâches).</summary>
    [RelayCommand]
    private void Quit() => _controller.Quit();
}
