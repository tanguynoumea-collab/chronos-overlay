using Chronos.Models.Historique;

namespace Chronos.Rendering.Historique;

/// <summary>
/// HIS-02 — le RÉTICULE : quel relevé montrer quand le pointeur survole un instant. Géométrie PURE : aucun état, aucun
/// I/O, aucun type WPF ; la surcouche convertit l'abscisse en instant par <see cref="EchelleTemps.Instant"/>, puis
/// demande ici le relevé le plus proche et l'écrit tel quel (« HH:MM · relevé exact / 5 h : N % … », §2.3).
///
/// <para>Le plus proche, pas « le dernier avant » : au milieu d'un trou, montrer le relevé qui SUIT quand il est plus
/// près est plus honnête que d'étirer le précédent — et l'infobulle donne l'heure du relevé, jamais celle du pointeur,
/// pour qu'on voie l'écart.</para>
/// </summary>
public static class Reticule
{
    /// <summary>
    /// Le relevé de <paramref name="serie"/> (triée par <c>T</c>, comme <c>AnalyseJournal.Serie</c>) dont <c>|T − t|</c> est
    /// minimal ; à égalité, le plus ANCIEN ; série vide → <c>null</c>. Recherche binaire puis comparaison des deux voisins.
    /// </summary>
    public static ReleveJournal? PlusProche(IReadOnlyList<ReleveJournal> serie, DateTimeOffset t)
    {
        if (serie.Count == 0) return null;

        // Premier index dont T ≥ t (borne inférieure).
        int bas = 0, haut = serie.Count;
        while (bas < haut)
        {
            var milieu = bas + (haut - bas) / 2;
            if (serie[milieu].T < t) bas = milieu + 1;
            else haut = milieu;
        }

        if (bas == 0) return serie[0];                       // t avant (ou sur) le premier relevé
        if (bas == serie.Count) return serie[^1];            // t après le dernier relevé

        var avant = serie[bas - 1];
        var apres = serie[bas];
        var dAvant = (t - avant.T).Ticks;
        var dApres = (apres.T - t).Ticks;
        return dAvant <= dApres ? avant : apres;             // égalité → le plus ancien
    }
}
