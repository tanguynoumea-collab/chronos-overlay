using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Calibration OPPORTUNISTE des plafonds (CAL-02). Service NEUTRE (aucun type WPF) qui écoute
/// <see cref="RefreshOrchestrator.SnapshotChanged"/> : dès qu'une fenêtre porte une utilization
/// EXACTE (source primaire rendue au moins une fois), il déduit le plafond correspondant
/// (tokens JSONL / utilization) et l'écrit en source Auto — sans jamais écraser une saisie Manual.
///
/// INERTE en mode app-bureau pur : tant qu'aucun snapshot Exact avec Utilization&gt;0 n'apparaît,
/// aucun accès disque ni écriture n'a lieu (chemin à coût zéro). Best-effort : une erreur de
/// calibration ne doit jamais faire tomber le pipeline de rafraîchissement.
/// </summary>
public sealed class BudgetAutoCalibrator : IDisposable
{
    private readonly RefreshOrchestrator _orchestrator;
    private readonly IUsageProvider _tokenSource; // décision verrouillée : JsonlEstimationProvider concret (porte toujours EstimatedTokens)
    private readonly SettingsService _settings;
    private readonly IClock _clock;

    public BudgetAutoCalibrator(RefreshOrchestrator orchestrator, IUsageProvider tokenSource, SettingsService settings, IClock clock)
    {
        _orchestrator = orchestrator;
        _tokenSource = tokenSource;
        _settings = settings;
        _clock = clock;
        _orchestrator.SnapshotChanged += OnSnapshot;
    }

    private async void OnSnapshot(object? sender, UsageSnapshot snap)
    {
        try { await CalibrateAsync(snap); }
        catch { /* best-effort : la calibration ne doit jamais faire tomber le pipeline */ }
    }

    // Testable directement. INERTE si aucune fenêtre Exact avec util>0 (aucun GetAsync, aucune écriture).
    internal async Task CalibrateAsync(UsageSnapshot snap, CancellationToken ct = default)
    {
        bool fiveExact = snap.FiveHour.Reliability == SourceReliability.Exact && snap.FiveHour.Utilization is > 0;
        bool weekExact = snap.SevenDay.Reliability == SourceReliability.Exact && snap.SevenDay.Utilization is > 0;
        if (!fiveExact && !weekExact) return; // chemin app-bureau : zéro coût

        var est = await _tokenSource.GetAsync(ct); // tokens JSONL mesurés sur les mêmes fenêtres
        var now = _clock.UtcNow;
        var current = _settings.Load();            // GAP-1 : état DISQUE frais avant écriture
        var updated = current;

        if (fiveExact && est.FiveHour.EstimatedTokens is > 0 &&
            BudgetCalibration.Deduce(snap.FiveHour.Utilization!.Value, est.FiveHour.EstimatedTokens.Value) is { } b5)
            updated = BudgetCalibration.ApplyAuto(updated, WindowKind.FiveHour, b5, now);

        if (weekExact && est.SevenDay.EstimatedTokens is > 0 &&
            BudgetCalibration.Deduce(snap.SevenDay.Utilization!.Value, est.SevenDay.EstimatedTokens.Value) is { } b7)
            updated = BudgetCalibration.ApplyAuto(updated, WindowKind.SevenDay, b7, now);

        if (!ReferenceEquals(updated, current)) _settings.Save(updated); // n'écrit que si un plafond a changé
    }

    public void Dispose() => _orchestrator.SnapshotChanged -= OnSnapshot;
}
