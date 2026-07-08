using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Provider COMPOSITE (DAT-06) : tente le primaire (objet d'usage Exact) puis bascule sur le repli
/// (estimation JSONL) — bascule PAR FENETRE. Chaque fenetre (5 h / 7 j) pouvant etre independamment
/// absente du primaire, le composite prend la MEILLEURE source par fenetre :
/// Exact prioritaire, sinon Estimated, sinon Unavailable (ROB-01 en aval, pas de crash).
/// </summary>
public sealed class CompositeUsageProvider : IUsageProvider
{
    private readonly IUsageProvider _primary;   // ClaudeUsageObjectProvider (Exact)
    private readonly IUsageProvider _fallback;  // JsonlEstimationProvider (Estimated)

    /// <summary>Emis en fin de GetAsync avec le snapshot final (branche par l'orchestrateur en Phase 4).</summary>
    public event EventHandler<UsageSnapshot>? SnapshotChanged;

    public CompositeUsageProvider(IUsageProvider primary, IUsageProvider fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        var p = await _primary.GetAsync(ct);
        // Note perf (RESEARCH Open Question 3) : le court-circuit paresseux du repli est un
        // raffinement Phase 4 ; ici, appeler les deux GetAsync suffit et reste teste.
        var f = await _fallback.GetAsync(ct);

        var snap = new UsageSnapshot
        {
            FiveHour = Best(p.FiveHour, f.FiveHour),
            SevenDay = Best(p.SevenDay, f.SevenDay),
            SourceCapturedAt = p.SourceCapturedAt ?? f.SourceCapturedAt,
            Age = p.Age ?? f.Age,
        };
        SnapshotChanged?.Invoke(this, snap);
        return snap;
    }

    // Meilleure source pour UNE fenetre : Exact du primaire, sinon Estimated du repli, sinon Unavailable.
    private static WindowState Best(WindowState primary, WindowState fallback) =>
        primary.Reliability == SourceReliability.Exact ? primary
        : fallback.Reliability == SourceReliability.Estimated ? fallback
        : primary; // reste Unavailable
}
