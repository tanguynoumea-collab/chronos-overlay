using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Provider COMPOSITE (DAT-06) : le composite retient, PAR FENÊTRE, la source la plus fiable entre un
/// primaire et un repli exacts (sonde d'en-têtes, secours OAuth du login Chronos). Chaque fenêtre
/// (5 h / 7 j) pouvant être indépendamment absente du primaire, le composite prend la MEILLEURE source
/// par fenêtre, primaire prioritaire à fiabilité égale, sinon Unavailable (ROB-01 en aval, pas de crash).
/// Un non-exact n'entre jamais ici : le seul chiffre non exact, le plancher « ≥ », est fabriqué par la
/// tête (LastExactUsageProvider / DoctrineFraicheur).
///
/// <para><b>Phase 19 — où vit la doctrine, et pourquoi pas ici.</b> Ce composite ne juge PAS la
/// fraîcheur : il classe par fiabilité et rien d'autre, délibérément. La doctrine (DoctrineFraicheur)
/// vit dans la couche de tête, LastExactUsageProvider, pour trois raisons mécaniques : Best() ne reçoit
/// que deux WindowState nus (ni horloge, ni magasin, ni journal d'activité) ; la chaîne réelle est faite
/// de TROIS composites imbriqués, donc une doctrine placée ici s'exécuterait trois fois par tick et
/// statuerait sur une information partielle ; et une source à ancienneté non bornée, placée en repli,
/// ne peut gagner que lorsque tout ce qui est au-dessus est indisponible — appliquer la porte d'âge
/// AU-DESSUS du composite y est donc strictement équivalent.</para>
/// <para><b>Corollaire à ne jamais enfreindre :</b> Best() ne doit JAMAIS arbitrer par récence entre deux
/// sources exactes. La porte d'âge est BINAIRE (certifiable ou non), pas un classement. Un arbitrage par
/// récence ferait gagner l'endpoint OAuth contre le cache légitime de la sonde, et emporterait avec lui
/// le statut serveur et le dépassement, dont la sonde est l'unique porteuse.</para>
/// </summary>
public sealed class CompositeUsageProvider : IUsageProvider
{
    private readonly IUsageProvider _primary;   // source exacte prioritaire (ex. sonde d'en-têtes)
    private readonly IUsageProvider _fallback;  // source exacte de secours (ex. login OAuth Chronos)

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
        // sinon (tout vient du repli), c'est celui du repli — l'horodatage d'une source écartée ne doit
        // pas qualifier un chiffre qu'elle n'a pas fourni.
        //
        // Phase 19 : UnExactADejaEteObtenu n'est PAS recomposé ici, et c'est correct — ce champ est posé
        // par la couche de doctrine, qui est AU-DESSUS de tous les composites. Aucun composite ne le voit
        // jamais. Toute AUTRE propriété ajoutée à UsageSnapshot devra en revanche être nommée dans ce
        // « new », sans quoi elle sera détruite en silence à chaque passage : une garde l'impose.
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
    // Generalise le choix pour l'IMBRICATION (INT-01) : un repli qui produit lui-meme un Exact (le secours
    // OAuth Chronos, ou un composite imbrique) doit primer sur un primaire Unavailable — l'ancien code ne promouvait que
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
