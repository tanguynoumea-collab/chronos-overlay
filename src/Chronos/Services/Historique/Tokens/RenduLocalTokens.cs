using System.Globalization;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;

namespace Chronos.Services.Historique.Tokens;

/// <summary>
/// TOK-04 — les agrégats rendus en heure LOCALE, justes aux changements d'heure.
///
/// <para><b>POURQUOI grouper sur l'heure UTC et non sur l'heure d'horloge locale</b> (D-33-18) : le 25/10/2026, 02:00–03:00
/// local existe deux fois (+02:00 puis +01:00) ; le 28/03/2027 (dimanche — le 29 est un lundi ordinaire), 02:00–03:00
/// n'existe pas. Une barre = un DÉBUT D'HEURE UTC de la plage, libellé en local : 25 barres le 25/10, 23 le 28/03, 24 sinon.
/// Un regroupement sur l'heure locale fusionnerait deux heures en octobre et laisserait un trou en mars. La plage vient de
/// <see cref="BornesPlage.Jour"/> / <see cref="BornesPlage.SemaineDeForfait"/> (25 h / 23 h, 169 h / 167 h prouvés en 32-06) :
/// ce type ne calcule AUCUNE borne. <c>plage.Debut</c> est supposée alignée sur l'heure (c'est le cas de ces bornes) ; sinon
/// la première barre commence à <c>plage.Debut</c> tel quel, sans recalage.</para>
///
/// <para>Chaque barre, chaque colonne porte son état de couverture (D-33-19) : une absence de tranche n'est « zéro » que si
/// elle est couverte. Entiers seulement (garde TOK-05) : la part sous-agents est un couple de totaux (D-33-20), jamais un
/// rapport ; les libellés d'axe sont l'affaire de la phase 34. Pur, sans E/S, fuseau injecté (D-33-21 : production
/// le fuseau local du système au site d'appel, tests <c>BornesPlage.FuseauParisPourTests()</c>). Type NEUTRE.</para>
/// </summary>
public static class RenduLocalTokens
{
    private static readonly TimeSpan Heure = TimeSpan.FromHours(1);

    /// <summary>
    /// Une barre par DÉBUT D'HEURE UTC de <c>[Debut, Fin[</c>, libellée en heure locale ; principal et sous-agents sommés
    /// séparément ; état de couverture au début de l'heure. Les tranches hors de la plage sont ignorées.
    /// </summary>
    public static IReadOnlyList<BarreHeure> ParHeure(IReadOnlyList<TrancheTokens> tranches, Plage plage, TimeZoneInfo tz, CouvertureTokens couverture)
    {
        ArgumentNullException.ThrowIfNull(tranches);
        ArgumentNullException.ThrowIfNull(plage);
        ArgumentNullException.ThrowIfNull(tz);
        ArgumentNullException.ThrowIfNull(couverture);

        // Indexation en O(n) : une semaine = 168 h × quelques milliers de tranches, pas un Where par barre.
        var parBarre = ParIndex(tranches, plage, Heure);
        var barres = new List<BarreHeure>();
        var index = 0;
        for (var h = plage.Debut; h < plage.Fin; h = h + Heure, index++)
        {
            var dansHeure = parBarre.TryGetValue(index, out var liste) ? liste : (IReadOnlyList<TrancheTokens>)Array.Empty<TrancheTokens>();
            barres.Add(new BarreHeure(
                h,
                Libelle(h, tz),
                TotauxTokens.Somme(dansHeure.Where(t => !t.Sub)),
                TotauxTokens.Somme(dansHeure.Where(t => t.Sub)),
                couverture.Classer(h)));
        }
        return barres;
    }

    /// <summary>
    /// Une colonne par tranche de 15 min UTC de <c>[Debut, Fin[</c> (vue Jour), libellée en heure locale, empilée par modèle
    /// — <c>sub</c> FUSIONNÉ dans le modèle (« sous-agents inclus », plan de design §2.3) —, parts triées par sortie
    /// décroissante puis modèle ordinal ; état de couverture au début de la tranche.
    /// </summary>
    public static IReadOnlyList<ColonneQuartDHeure> ParQuartDHeure(IReadOnlyList<TrancheTokens> tranches, Plage plage, TimeZoneInfo tz, CouvertureTokens couverture)
    {
        ArgumentNullException.ThrowIfNull(tranches);
        ArgumentNullException.ThrowIfNull(plage);
        ArgumentNullException.ThrowIfNull(tz);
        ArgumentNullException.ThrowIfNull(couverture);

        var parColonne = ParIndex(tranches, plage, TrancheTokens.Tranche);
        var colonnes = new List<ColonneQuartDHeure>();
        var index = 0;
        for (var s = plage.Debut; s < plage.Fin; s = s + TrancheTokens.Tranche, index++)
        {
            IReadOnlyList<PartModele> parModele = parColonne.TryGetValue(index, out var liste)
                ? liste.GroupBy(t => t.Model, StringComparer.Ordinal)
                    .Select(g => new PartModele(g.Key, TotauxTokens.Somme(g)))
                    .OrderByDescending(p => p.Totaux.Out)
                    .ThenBy(p => p.Model, StringComparer.Ordinal)
                    .ToList()
                : Array.Empty<PartModele>();
            colonnes.Add(new ColonneQuartDHeure(s, Libelle(s, tz), parModele, couverture.Classer(s)));
        }
        return colonnes;
    }

    /// <summary>La part sous-agents : DEUX totaux entiers (<c>sub == true</c>, <c>sub == false</c>) — jamais un rapport entre eux.</summary>
    public static (TotauxTokens SousAgents, TotauxTokens Principal) PartSousAgents(IEnumerable<TrancheTokens> tranches)
    {
        ArgumentNullException.ThrowIfNull(tranches);
        var sousAgents = TotauxTokens.Vide;
        var principal = TotauxTokens.Vide;
        foreach (var t in tranches)
        {
            if (t.Sub) sousAgents = sousAgents.Plus(t);
            else principal = principal.Plus(t);
        }
        return (sousAgents, principal);
    }

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

    // --- Internes ---

    // « HH:mm » en heure locale du fuseau injecté, culture invariante (le libellé est une clé de grille, pas un texte de culture).
    private static string Libelle(DateTimeOffset debutUtc, TimeZoneInfo tz)
        => TimeZoneInfo.ConvertTime(debutUtc, tz).ToString("HH:mm", CultureInfo.InvariantCulture);

    // Les tranches de la plage, rangées par indice de case (case k = [Debut + k × pas, Debut + (k+1) × pas[), calculé sur
    // l'axe UTC depuis plage.Debut : exact même si Debut n'est pas aligné, O(n). Hors plage → ignorée.
    private static Dictionary<long, List<TrancheTokens>> ParIndex(IReadOnlyList<TrancheTokens> tranches, Plage plage, TimeSpan pas)
    {
        var index = new Dictionary<long, List<TrancheTokens>>();
        foreach (var t in tranches)
        {
            if (!plage.Contient(t.Slot)) continue;
            var k = (t.Slot - plage.Debut).Ticks / pas.Ticks;
            if (!index.TryGetValue(k, out var liste)) index[k] = liste = new List<TrancheTokens>();
            liste.Add(t);
        }
        return index;
    }
}
