namespace Chronos.Text;

/// <summary>
/// Formatage FR pur du pourcentage d'utilisation d'une fenêtre (aucune dépendance WPF, aucun I/O,
/// aucune CultureInfo). Honnêteté des chiffres (VIS-05) : une utilisation absente (null) ne produit
/// AUCUN texte — on ne présente jamais un plafond inventé. Une utilisation estimée est préfixée « ~ »
/// (même règle que le badge « estimée »), une utilisation exacte est rendue telle quelle. Arrondi à
/// l'entier le plus proche, espace normal avant le %. Type neutre, hautement testable.
/// </summary>
public static class PercentFormatter
{
    /// <summary>
    /// Rend « 80 % » (exact), « ~80 % » (estimé) ou «» (utilisation null). L'arrondi est à l'entier
    /// le plus proche (0.5 → sup). Le préfixe « ~ » signale une estimation, jamais une valeur exacte.
    /// </summary>
    public static string Format(double? utilization, bool isEstimated)
    {
        if (utilization is null) return ""; // honnêteté : pas de plafond fiable → pas de %

        int pct = (int)System.Math.Round(utilization.Value * 100, System.MidpointRounding.AwayFromZero);
        string prefixe = isEstimated ? "~" : "";
        return $"{prefixe}{pct} %"; // espace normal avant %
    }
}
