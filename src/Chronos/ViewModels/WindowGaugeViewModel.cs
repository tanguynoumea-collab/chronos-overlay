using Chronos.Models;
using Chronos.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronos.ViewModels;

/// <summary>
/// Sous-VM d'UNE fenêtre d'usage (5 h ou hebdo). Mémorise le dernier <see cref="WindowState"/> immuable
/// et recalcule, à chaque interpolation, la fraction d'arc restante + le compte à rebours formaté FR.
/// L'interpolation est PURE (RAF-03) : elle ne lit que l'état mémorisé + l'instant fourni, jamais le disque.
/// Le XAML Phase 5 bindera un RingArc sur FractionRemaining/Utilization/Reliability.
/// </summary>
public sealed partial class WindowGaugeViewModel : ObservableObject
{
    private readonly TimeSpan _windowLength;
    private WindowState _state; // dernier snapshot de cette fenêtre (immuable)

    [ObservableProperty] private double _fractionRemaining;                    // 0..1 → longueur d'arc
    [ObservableProperty] private double? _utilization;                          // 0..1 ou null → couleur (Phase 5)
    [ObservableProperty] private string _countdownText = "—";
    [ObservableProperty] private bool _exhausted;
    [ObservableProperty] private SourceReliability _reliability = SourceReliability.Unavailable;
    [ObservableProperty] private bool _isEstimated;                             // provenance → marquage « estimé » (DAT-08 Phase 5)

    public WindowGaugeViewModel(TimeSpan windowLength)
    {
        _windowLength = windowLength;
        _state = WindowState.Unavailable(default);
    }

    /// <summary>Applique un nouvel état de fenêtre (thread UI). Met à jour provenance/utilization/épuisement.</summary>
    public void Apply(WindowState s)
    {
        _state = s;
        Utilization = s.Utilization;
        Exhausted = s.Exhausted;
        Reliability = s.Reliability;
        IsEstimated = s.Reliability == SourceReliability.Estimated; // pré-câble DAT-08 (Phase 5)
    }

    /// <summary>PUR, aucun I/O (RAF-03) : recalcule fraction d'arc + compte à rebours à l'instant <paramref name="now"/>.</summary>
    public void Interpolate(DateTimeOffset now)
    {
        FractionRemaining = WindowState.FractionRemaining(_state.ResetsAt, now, _windowLength) ?? 0.0;
        CountdownText = _state.ResetsAt is { } r
            ? CountdownFormatter.Format(r - now)
            : "—";
    }
}
