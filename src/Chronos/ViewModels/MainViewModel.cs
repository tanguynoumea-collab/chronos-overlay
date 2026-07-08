using Chronos.Models;
using Chronos.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronos.ViewModels;

/// <summary>
/// ViewModel racine (temps réel). S'abonne à l'horloge DONNÉES (<see cref="RefreshOrchestrator.SnapshotChanged"/>,
/// émis sur le thread pool) et franchit la frontière de thread EN UN SEUL POINT via <see cref="IUiDispatcher.Post"/>
/// (RAF-04). L'affichage vit grâce à <see cref="Interpolate"/> (PUR, aucun I/O — RAF-03), piloté par un
/// DispatcherTimer 1 s créé côté UI (<see cref="StartClock"/>) — jamais dans le ctor (Pitfall 4 : tests en [Fact] simple).
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IUiDispatcher _ui;
    private readonly IClock _clock;

    public WindowGaugeViewModel FiveHour { get; } = new(TimeSpan.FromHours(5));
    public WindowGaugeViewModel SevenDay { get; } = new(TimeSpan.FromDays(7));

    [ObservableProperty] private bool _dataUnavailable;
    [ObservableProperty] private DateTimeOffset? _capturedAt; // staleness pour l'UI Phase 5
    [ObservableProperty] private bool _isStale;

    public MainViewModel(RefreshOrchestrator orchestrator, IUiDispatcher ui, IClock clock)
    {
        _ui = ui;
        _clock = clock;
        orchestrator.SnapshotChanged += OnSnapshotChanged; // callback thread pool (horloge données)
    }

    // FRONTIÈRE DE THREAD — franchie UNE seule fois (RAF-04). Aucune mutation d'ObservableProperty hors d'ici.
    private void OnSnapshotChanged(object? sender, UsageSnapshot snap) => _ui.Post(() => ApplySnapshot(snap));

    /// <summary>Applique un snapshot (thread UI) : pousse chaque fenêtre, l'état global, puis rend immédiatement.</summary>
    internal void ApplySnapshot(UsageSnapshot snap)
    {
        FiveHour.Apply(snap.FiveHour);
        SevenDay.Apply(snap.SevenDay);
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
}
