using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;

namespace Chronos.Services.Historique.Tokens;

/// <summary>
/// TOK-04 — les agrégats rendus en heure LOCALE, justes aux changements d'heure. Pur, sans E/S, fuseau injecté (D-33-21 :
/// production <c>TimeZoneInfo.Local</c> au site d'appel, tests <c>BornesPlage.FuseauParisPourTests()</c>).
///
/// <para>Cette tâche ne porte que <see cref="SousPlagesCouverture"/> (Task 1) : le découpage d'une plage en segments
/// contigus d'un seul état de couverture (D-33-19), consommé par <see cref="LecteurAgregats.Lire"/>. Les barres par heure,
/// les colonnes par quart d'heure et la part sous-agents arrivent en Task 2. Entiers seulement (garde TOK-05). Type NEUTRE.</para>
/// </summary>
public static class RenduLocalTokens
{
    /// <summary>
    /// Découpe <paramref name="plage"/> en sous-plages contiguës d'un seul état : les points de coupe sont les bornes de la
    /// plage, la plus ancienne ligne vue et les débuts / fins d'intervalles garantis strictement à l'intérieur ; chaque
    /// segment <c>[p_k, p_k+1[</c> est classé par son DÉBUT (<see cref="CouvertureTokens.Classer"/>) — l'état ne change
    /// qu'aux points de coupe, donc il est constant sur le segment ; les segments adjacents de même état sont fusionnés.
    /// Plage vide (<c>Debut ≥ Fin</c>) → liste vide.
    /// </summary>
    public static IReadOnlyList<SousPlageCouverture> SousPlagesCouverture(Plage plage, CouvertureTokens couverture)
    {
        ArgumentNullException.ThrowIfNull(plage);
        ArgumentNullException.ThrowIfNull(couverture);
        if (plage.Debut >= plage.Fin) return Array.Empty<SousPlageCouverture>();

        var points = new SortedSet<DateTimeOffset> { plage.Debut, plage.Fin };
        void Couper(DateTimeOffset p) { if (plage.Debut < p && p < plage.Fin) points.Add(p); }

        if (couverture.PlusAncienneLigneVue is { } plusAncienne) Couper(plusAncienne);
        foreach (var i in couverture.Intervalles)
        {
            Couper(i.Debut);
            Couper(i.Fin);
        }

        var segments = new List<SousPlageCouverture>();
        DateTimeOffset? precedent = null;
        foreach (var p in points)
        {
            if (precedent is { } debut)
            {
                var etat = couverture.Classer(debut);
                if (segments.Count > 0 && segments[^1].Etat == etat)
                    segments[^1] = segments[^1] with { Fin = p };   // même état : un seul segment
                else
                    segments.Add(new SousPlageCouverture(debut, p, etat));
            }
            precedent = p;
        }
        return segments;
    }
}
