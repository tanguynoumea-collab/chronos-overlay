using Chronos.Models.Historique;

namespace Chronos.Rendering.Historique;

/// <summary>Une barre de la piste Rythme : la somme des Δ 5 h arrivés dans la case <c>[Debut, Fin[</c>, et le niveau 5 h atteint dans l'heure (pour la couleur).</summary>
public readonly record struct BarreDelta(DateTimeOffset Debut, DateTimeOffset Fin, double Delta, double NiveauAtteint);

/// <summary>Une colonne de pixels après réduction : l'abscisse du centre de colonne et les extrêmes des points qu'elle contient.</summary>
public readonly record struct PointReduit(double X, double Min, double Max);

/// <summary>
/// HIS-02 / HIS-07 — le BINNING de la piste Rythme et la réduction min/max par colonne. Géométrie PURE : aucun état,
/// aucun I/O, aucun type WPF ; consomme les <c>DeltaConsommation</c> d'<c>AnalyseReleves</c> (déjà calculés entre relevés
/// consécutifs de même <c>resets_at</c>, jamais à travers un reset ni un trou) et ne recalcule rien.
///
/// <para><b>D-34-10 — le Rythme ne somme que les Δ POSITIFS.</b> Un Δ marqué <c>Anormal</c> (négatif sans changement de
/// <c>resets_at</c>) est une anomalie à compter au diagnostic, pas une consommation à dessiner : le soustraire d'une barre
/// inventerait une « dé-consommation ». Case = heure ENTIÈRE depuis <c>Plage.Debut</c> (comme <c>RenduLocalTokens.ParHeure</c> :
/// 25 cases le 25/10/2026, 23 le 28/03/2027) ; un Δ est rangé dans la case de son instant d'ARRIVÉE <c>A</c> (c'est là que
/// le compteur a monté) ; <c>NiveauAtteint</c> = max du sélecteur (U5) des relevés de la case, pour colorer « au niveau
/// atteint » ; les cases sans Δ sont omises.</para>
/// <para><b>D-34-11 — réduction min/max</b> : au-delà de <see cref="SeuilReduction"/> points il y a plus de points que de
/// pixels ; garder le min et le max de chaque colonne conserve les pics et les creux qu'un sous-échantillonnage
/// perdrait. Points triés par X, <c>colonnes</c> = largeur en pixels entiers, X de sortie = centre de colonne. Une semaine à
/// la cadence de la sonde (2 016 relevés) reste SOUS le seuil : jamais réduite ; le seuil sert aux semaines multi-sources ou
/// à une cadence plus fine. La piste n'appelle la réduction que si <see cref="DoitReduire"/>.</para>
/// </summary>
public static class Binning
{
    /// <summary>Au-delà de ce nombre de points, la piste réduit par min/max de colonne (HIS-07).</summary>
    public const int SeuilReduction = 4000;

    /// <summary>Faut-il réduire ? Strictement au-delà du seuil : 4 000 points se dessinent tels quels.</summary>
    public static bool DoitReduire(int nbPoints) => nbPoints > SeuilReduction;

    /// <summary>
    /// D-34-10. Les barres horaires de la plage : Δ positifs non <c>Anormal</c> sommés par case d'une heure depuis
    /// <c>Plage.Debut</c> (case de l'instant d'arrivée <c>A</c>), <c>NiveauAtteint</c> = max de <paramref name="niveau"/> sur
    /// les relevés dont <c>T</c> tombe dans la case (0 si aucun). Triées par <c>Debut</c> ; la dernière case est coupée à
    /// <c>Plage.Fin</c>.
    /// </summary>
    public static IReadOnlyList<BarreDelta> DeltasParHeure(IReadOnlyList<DeltaConsommation> deltas, IReadOnlyList<ReleveJournal> serie,
                                                          Func<ReleveJournal, double?> niveau, Plage plage)
    {
        var sommes = new Dictionary<long, double>();
        foreach (var d in deltas)
        {
            if (d.Anormal || d.Delta <= 0 || double.IsNaN(d.Delta)) continue;   // une anomalie n'est pas une consommation
            if (!plage.Contient(d.A)) continue;
            var k = Case(d.A, plage);
            sommes[k] = sommes.GetValueOrDefault(k) + d.Delta;
        }
        if (sommes.Count == 0) return Array.Empty<BarreDelta>();

        var niveaux = new Dictionary<long, double>();
        foreach (var r in serie)
        {
            if (!plage.Contient(r.T) || niveau(r) is not { } v || double.IsNaN(v)) continue;
            var k = Case(r.T, plage);
            niveaux[k] = Math.Max(niveaux.GetValueOrDefault(k), v);
        }

        return sommes.Keys.OrderBy(k => k)
            .Select(k =>
            {
                var debut = plage.Debut + TimeSpan.FromHours(k);
                var fin = debut + TimeSpan.FromHours(1);
                if (fin > plage.Fin) fin = plage.Fin;
                return new BarreDelta(debut, fin, sommes[k], niveaux.GetValueOrDefault(k));
            })
            .ToList();
    }

    // Index de la case horaire d'un instant : heures ENTIÈRES écoulées depuis Plage.Debut (pas un calendrier : la plage l'est déjà).
    private static long Case(DateTimeOffset t, Plage plage) => (t - plage.Debut).Ticks / TimeSpan.TicksPerHour;

    /// <summary>
    /// D-34-11. Réduit des <paramref name="points"/> triés par X en au plus <paramref name="colonnes"/> colonnes : pour chaque
    /// colonne non vide, le min et le max des Y qu'elle contient, à l'abscisse du centre de colonne. <paramref name="colonnes"/>
    /// ≤ 0 ou aucun point → vide ; tous les X égaux → une seule colonne.
    /// </summary>
    public static IReadOnlyList<PointReduit> ReductionMinMax(IReadOnlyList<(double X, double Y)> points, int colonnes)
    {
        if (colonnes <= 0 || points.Count == 0) return Array.Empty<PointReduit>();

        var x0 = points[0].X;
        var x1 = points[^1].X;
        var etendue = x1 - x0;
        if (!(etendue > 0))
        {
            var (min, max) = (double.PositiveInfinity, double.NegativeInfinity);
            foreach (var p in points) { min = Math.Min(min, p.Y); max = Math.Max(max, p.Y); }
            return new[] { new PointReduit(x0, min, max) };
        }

        var mins = new double[colonnes];
        var maxs = new double[colonnes];
        var remplies = new bool[colonnes];
        foreach (var p in points)
        {
            var col = Math.Clamp((int)((p.X - x0) / etendue * colonnes), 0, colonnes - 1);
            if (!remplies[col]) { remplies[col] = true; mins[col] = p.Y; maxs[col] = p.Y; }
            else { mins[col] = Math.Min(mins[col], p.Y); maxs[col] = Math.Max(maxs[col], p.Y); }
        }

        var largeurColonne = etendue / colonnes;
        var reduits = new List<PointReduit>(colonnes);
        for (var col = 0; col < colonnes; col++)
            if (remplies[col])
                reduits.Add(new PointReduit(x0 + (col + 0.5) * largeurColonne, mins[col], maxs[col]));
        return reduits;
    }
}
