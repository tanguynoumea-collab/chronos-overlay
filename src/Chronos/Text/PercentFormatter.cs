namespace Chronos.Text;

/// <summary>
/// Formatage FR pur du pourcentage d'utilisation d'une fenêtre (aucune dépendance WPF, aucun I/O,
/// aucune CultureInfo). Honnêteté des chiffres (VIS-05) : une utilisation absente (null) ne produit
/// AUCUN texte — on ne présente jamais un plafond inventé.
///
/// DEUX surcharges, et c'est délibéré :
/// - <see cref="Format(double?, bool)"/> — historique, préfixe « ~ » à une estimation. Conservée pour
///   la galerie de styles de cadran (<c>CadranPreviewViewModel</c>), qui pilote un booléen d'aperçu et
///   n'a aucune provenance à exhiber.
/// - <see cref="Format(double?, Chronos.Models.ProvenanceReleve?)"/> — phase 19, préfixe « ≥ » à un
///   PLANCHER. C'est celle que le cadran de production emploie désormais.
///
/// Arrondi à l'entier le plus proche, espace normal avant le %. Type neutre, hautement testable.
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

    /// <summary>
    /// Phase 19 — « 80 % » (exact), « ≥ 80 % » (plancher : borne inférieure), «» (utilisation null).
    /// « ≥ » et non « ~ », et ce n'est pas un choix de style : l'incertitude d'un plancher est
    /// UNILATÉRALE. Un tilde dirait « autour de 80 », ce qui autoriserait la lecture « peut-être 75 » —
    /// or on SAIT qu'on est à 80 au minimum, et c'est la borne SUPÉRIEURE qui est inconnue. Afficher une
    /// incertitude symétrique là où elle ne l'est pas serait un mensonge poli.
    ///
    /// Provenance <c>null</c> (fenêtre née hors doctrine) ⇒ aucun préfixe : on ne salit pas un chiffre
    /// sur lequel la doctrine ne s'est pas prononcée.
    /// </summary>
    public static string Format(double? utilization, Chronos.Models.ProvenanceReleve? provenance)
    {
        if (utilization is null) return ""; // honnêteté : pas d'utilisation → pas de %

        int pct = (int)System.Math.Round(utilization.Value * 100, System.MidpointRounding.AwayFromZero);
        string prefixe = provenance == Chronos.Models.ProvenanceReleve.PlancherAvecActivite ? "≥ " : "";
        return $"{prefixe}{pct} %"; // espace normal avant %
    }
}
