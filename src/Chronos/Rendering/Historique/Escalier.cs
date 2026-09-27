using Chronos.Models.Historique;

namespace Chronos.Rendering.Historique;

/// <summary>Un palier de l'escalier : la valeur <paramref name="U"/> tenue de <paramref name="T0"/> à <paramref name="T1"/> (T1 == T0 : un point, la ligne s'interrompt).</summary>
public readonly record struct SegmentPalier(DateTimeOffset T0, DateTimeOffset T1, double U);

/// <summary>Les paliers d'une même bande de rampe : la piste gèle UN pinceau par bande (<c>ChronosTheme.ArcColor(Niveau)</c>) et un seul <c>StreamGeometry</c>.</summary>
public sealed record BandeRampe(double Niveau, IReadOnlyList<SegmentPalier> Segments);

/// <summary>Le rectangle d'un trou, borné à la plage ; <paramref name="Trou"/> porte la cause pour la bordure et le libellé.</summary>
public readonly record struct RectangleTrou(DateTimeOffset Debut, DateTimeOffset Fin, Trou Trou);

/// <summary>Le bloc plat d'un saut non localisé : de <paramref name="Bas"/> à <paramref name="Haut"/> sur toute la durée du trou (« +N % pendant l'absence (répartition inconnue) »).</summary>
public readonly record struct BlocSaut(DateTimeOffset Debut, DateTimeOffset Fin, double Bas, double Haut, SautNonLocalise Saut);

/// <summary>
/// HIS-02 / HIS-06 — l'ESCALIER d'une série de relevés exacts, et les formes de l'absence. Géométrie PURE : aucun état,
/// aucun I/O, aucun type WPF ; consomme <c>AnalyseJournal</c> (série, trous, sauts) et ne recalcule rien.
///
/// <para><b>D-34-08</b> — un palier va de <c>T[i]</c> à <c>T[i+1]</c>, SAUF si <c>T[i]</c> est le <c>Debut</c> d'un trou : le
/// palier s'arrête à <c>T[i]</c> (longueur nulle, la ligne s'interrompt), et le suivant repart à <c>T[i+1]</c>, premier
/// relevé après l'absence. Le dernier relevé fait un point ; tout est borné à <c>Plage.Fin</c>. <b>Un trou est VIDE de
/// géométrie</b> : la piste ne peut pas y dessiner une ligne même par erreur — c'est le contrat HIS-06 tenu par la
/// structure des données, pas par une vérification au dessin. L'absence n'a d'autre forme qu'un
/// <see cref="RectangleTrou"/> (fond hachuré + cause) et, si le Δ est connu, un <see cref="BlocSaut"/> plat de la
/// hauteur du Δ — jamais une barre au réveil, jamais une pente interpolée.</para>
/// <para><b>D-34-09</b> — <see cref="ParBandes"/> découpe [0, 1[ en <c>nbBandes</c> bandes égales plus une bande « épuisé »
/// pour <c>U ≥ 1</c> ; le <c>Niveau</c> d'une bande est son centre (<c>(k + 0,5) / nb</c>) ou 1,0 : c'est lui que la piste
/// passe à la rampe du thème actif, un pinceau gelé par bande et non par segment.</para>
/// </summary>
public static class Escalier
{
    /// <summary>
    /// D-34-08. Un palier par relevé de <paramref name="serie"/> (triée par <c>T</c>) pour lequel <paramref name="u"/> rend une
    /// valeur ; la LONGUEUR d'un palier suit la série, pas le sélecteur (un relevé sans valeur pour ce sélecteur ne fait pas de
    /// palier, mais borne quand même le précédent). Un relevé à <c>T ≥ Plage.Fin</c> est hors plage : omis.
    /// </summary>
    public static IReadOnlyList<SegmentPalier> Segments(IReadOnlyList<ReleveJournal> serie, IReadOnlyList<Trou> trous,
                                                        Func<ReleveJournal, double?> u, Plage plage)
    {
        var debuts = new HashSet<DateTimeOffset>(trous.Select(t => t.Debut));   // un palier s'arrête au dernier relevé AVANT un trou
        var paliers = new List<SegmentPalier>(serie.Count);
        for (var i = 0; i < serie.Count; i++)
        {
            var releve = serie[i];
            if (releve.T >= plage.Fin) continue;
            if (u(releve) is not { } valeur || double.IsNaN(valeur)) continue;

            var fin = i + 1 < serie.Count && !debuts.Contains(releve.T) ? serie[i + 1].T : releve.T;
            if (fin > plage.Fin) fin = plage.Fin;
            paliers.Add(new SegmentPalier(releve.T, fin, valeur));
        }
        return paliers;
    }

    /// <summary>
    /// D-34-09. Regroupe les paliers par bande de rampe, bandes ordonnées par <c>Niveau</c> croissant, la bande « épuisé »
    /// (<c>U ≥ 1</c>, Niveau 1,0) toujours séparée de la dernière bande de [0, 1[. <paramref name="nbBandes"/> ≤ 0 → une
    /// seule bande de Niveau 0,5 contenant tout.
    /// </summary>
    public static IReadOnlyList<BandeRampe> ParBandes(IReadOnlyList<SegmentPalier> segments, int nbBandes)
    {
        if (nbBandes <= 0) return new[] { new BandeRampe(0.5, segments) };

        return segments
            .GroupBy(s => IndexBande(s.U, nbBandes))
            .OrderBy(g => g.Key)
            .Select(g => new BandeRampe(g.Key == nbBandes ? 1.0 : (g.Key + 0.5) / nbBandes, g.ToList()))
            .ToList();
    }

    // k ∈ [0, nb] : nb = épuisé (U ≥ 1) ; sinon la bande de largeur 1/nb qui contient U, bornée (U négatif ou NaN → 0).
    private static int IndexBande(double u, int nb)
    {
        if (u >= 1.0) return nb;
        if (double.IsNaN(u) || u <= 0.0) return 0;
        return Math.Clamp((int)Math.Floor(u * nb), 0, nb - 1);
    }

    /// <summary>
    /// Un rectangle par trou, borné à <c>[Plage.Debut, Plage.Fin]</c> ; un trou encore OUVERT (<c>Fin</c> null) finit à
    /// <paramref name="finSiOuvert"/> (le <c>now</c> de l'analyse). Les intervalles vides (trou entièrement hors plage) sont omis.
    /// </summary>
    public static IReadOnlyList<RectangleTrou> RectanglesTrous(IReadOnlyList<Trou> trous, Plage plage, DateTimeOffset finSiOuvert)
    {
        var rectangles = new List<RectangleTrou>(trous.Count);
        foreach (var trou in trous)
        {
            var debut = Max(trou.Debut, plage.Debut);
            var fin = Min(trou.Fin ?? finSiOuvert, plage.Fin);
            if (fin <= debut) continue;
            rectangles.Add(new RectangleTrou(debut, fin, trou));
        }
        return rectangles;
    }

    /// <summary>
    /// Un bloc plat par saut dont le Δ est CONNU (même <c>resets_at</c> des deux côtés, valeurs présentes) : de
    /// <c>min(Avant, Apres)</c> à <c>max(Avant, Apres)</c>, sur la durée du trou bornée à la plage. Un saut indéterminable
    /// (<c>Delta</c>, <c>Avant</c> ou <c>Apres</c> null) n'a AUCUNE forme — surtout pas une barre au réveil.
    /// </summary>
    public static IReadOnlyList<BlocSaut> BlocsSauts(IReadOnlyList<SautNonLocalise> sauts, Plage plage)
    {
        var blocs = new List<BlocSaut>(sauts.Count);
        foreach (var saut in sauts)
        {
            if (saut.Delta is null || saut.Avant is not { } avant || saut.Apres is not { } apres) continue;
            if (saut.Trou.Fin is not { } finTrou) continue;   // un trou ouvert n'a pas d'« après » : rien à dessiner

            var debut = Max(saut.Trou.Debut, plage.Debut);
            var fin = Min(finTrou, plage.Fin);
            if (fin <= debut) continue;
            blocs.Add(new BlocSaut(debut, fin, Math.Min(avant, apres), Math.Max(avant, apres), saut));
        }
        return blocs;
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a >= b ? a : b;
    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a <= b ? a : b;
}
