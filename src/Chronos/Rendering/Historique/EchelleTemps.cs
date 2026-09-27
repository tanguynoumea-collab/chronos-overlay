using Chronos.Models.Historique;

namespace Chronos.Rendering.Historique;

/// <summary>
/// HIS-02 — l'ÉCHELLE DES TEMPS de la fenêtre Historique : un instant devient une abscisse, une abscisse redevient un
/// instant. Géométrie PURE : aucun état, aucun I/O, aucun type WPF (doubles nus). Le fuseau n'existe pas ici : les
/// instants sont des <see cref="DateTimeOffset"/> et la plage vient de <c>BornesPlage</c> (calendrier local, fuseau
/// injecté). C'est la <b>durée réelle</b> de la plage qui fait l'échelle — 169 h la semaine du passage à l'heure
/// d'hiver, 25 h le jour du 25/10 — jamais un « 7 jours » ni un « 24 h » supposés : le mardi minuit d'une semaine
/// de 169 h est à 73/169 de la largeur, pas à 3/7.
/// </summary>
public static class EchelleTemps
{
    /// <summary>
    /// Position relative de <paramref name="t"/> dans la plage : <c>(t − Debut) / Duree</c>. NON bornée (un instant
    /// avant la plage est négatif, après vaut plus de 1 : la semaine précédente et « maintenant » peuvent déborder,
    /// c'est à la piste de clipper). Une plage de durée nulle ou négative rend 0 — aucune division par zéro.
    /// </summary>
    public static double Fraction(DateTimeOffset t, Plage plage)
    {
        var duree = plage.Duree.Ticks;
        if (duree <= 0) return 0.0;
        return (double)(t - plage.Debut).Ticks / duree;
    }

    /// <summary>Abscisse de <paramref name="t"/> pour une piste de <paramref name="largeur"/> pixels : <c>Fraction × largeur</c>. Non bornée.</summary>
    public static double X(DateTimeOffset t, Plage plage, double largeur)
        => Fraction(t, plage) * largeur;

    /// <summary>Largeur en pixels de l'intervalle <c>[debut, fin]</c> : <c>max(0, X(fin) − X(debut))</c>. Jamais négative.</summary>
    public static double Largeur(DateTimeOffset debut, DateTimeOffset fin, Plage plage, double largeur)
        => Math.Max(0.0, X(fin, plage, largeur) - X(debut, plage, largeur));

    /// <summary>
    /// L'inverse de <see cref="X"/> : l'instant sous l'abscisse <paramref name="x"/>, BORNÉ à <c>[Debut, Fin]</c> (le
    /// réticule ne sort jamais de la plage affichée). Largeur nulle ou négative → <c>Debut</c>.
    /// </summary>
    public static DateTimeOffset Instant(double x, Plage plage, double largeur)
    {
        if (largeur <= 0 || double.IsNaN(x) || plage.Duree.Ticks <= 0) return plage.Debut;
        var fraction = Math.Clamp(x / largeur, 0.0, 1.0);
        var ticks = (long)Math.Round(fraction * plage.Duree.Ticks);
        return plage.Debut + TimeSpan.FromTicks(ticks);
    }
}
