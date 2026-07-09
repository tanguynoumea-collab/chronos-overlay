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

        var fiveHour = Best(p.FiveHour, f.FiveHour);
        var sevenDay = Best(p.SevenDay, f.SevenDay);

        // Honnêteté du staleness : SourceCapturedAt doit refléter la source qui ALIMENTE réellement
        // l'affichage. Si au moins une fenêtre vient du primaire (Exact), son horodatage prime ;
        // sinon (tout vient du repli JSONL, calculé à l'instant), c'est celui du repli — un usage.json
        // périmé ne doit pas marquer « données périmées » une estimation fraîche.
        var primaryUsed = ReferenceEquals(fiveHour, p.FiveHour) && fiveHour.Reliability == SourceReliability.Exact
                       || ReferenceEquals(sevenDay, p.SevenDay) && sevenDay.Reliability == SourceReliability.Exact;

        return new UsageSnapshot
        {
            FiveHour = fiveHour,
            SevenDay = sevenDay,
            SourceCapturedAt = primaryUsed ? (p.SourceCapturedAt ?? f.SourceCapturedAt)
                                           : (f.SourceCapturedAt ?? p.SourceCapturedAt),
        };
    }

    // Meilleure source pour UNE fenetre : on classe par fiabilite (Exact > Estimated > Unavailable) et
    // on retient le repli SEULEMENT s'il est STRICTEMENT plus fiable (egalite -> primaire prioritaire).
    // Generalise le choix pour l'IMBRICATION (INT-01) : un repli qui produit lui-meme un Exact (composite
    // interne statusLine) doit primer sur un primaire Unavailable — l'ancien code ne promouvait que
    // l'Estimated du repli et ratait ce cas. Les 6 cas d'origine restent inchanges (primaire prioritaire
    // a fiabilite egale ou superieure).
    private static WindowState Best(WindowState primary, WindowState fallback) =>
        Rank(fallback.Reliability) > Rank(primary.Reliability) ? fallback : primary;

    private static int Rank(SourceReliability r) => r switch
    {
        SourceReliability.Exact => 2,
        SourceReliability.Estimated => 1,
        _ => 0, // Unavailable
    };
}
