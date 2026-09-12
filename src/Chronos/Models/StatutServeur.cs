namespace Chronos.Models;

/// <summary>
/// Statut déclaré par le SERVEUR pour une fenêtre de limite (HDR-03) — un fait RAPPORTÉ, jamais une
/// déduction locale à partir d'un pourcentage.
///
/// Vocabulaire OUVERT, et c'est le point : <c>allowed</c> / <c>allowed_warning</c> / <c>rejected</c> sont
/// rapportés par le code d'origine et par la communauté, mais la famille d'en-têtes
/// <c>anthropic-ratelimit-unified-*</c> est ABSENTE de la documentation publique Anthropic. Chaque valeur
/// est donc une hypothèse, pas un fait. D'où <see cref="NonReconnu"/> : on dit « statut non reconnu »
/// plutôt que de ranger d'autorité une valeur inconnue dans « autorisé ». Même honnêteté que
/// « null n'est pas 0 » — et la même raison : un overlay qui rassure à tort est pire que muet.
/// </summary>
public enum StatutServeur { Autorise, AutoriseAvertissement, Rejete, NonReconnu }

/// <summary>
/// Lecture TOLÉRANTE d'une valeur d'en-tête de statut. Classe pure : aucune entrée/sortie, aucune horloge.
///
/// Deux inconnus, délibérément distincts et non interchangeables :
/// <list type="bullet">
///   <item><c>null</c> — l'en-tête est ABSENT (ou vide) : le serveur n'a rien dit. Rien à afficher,
///         rien à déduire.</item>
///   <item><see cref="StatutServeur.NonReconnu"/> — l'en-tête est PRÉSENT mais sa valeur n'appartient pas
///         à l'ensemble connu : le serveur a dit quelque chose que nous ne savons pas lire. C'est une
///         information (et le signal que le vocabulaire a bougé côté serveur), pas une absence.</item>
/// </list>
/// Les confondre ferait disparaître exactement le signal dont la phase a besoin pour constater, sur la
/// machine de l'utilisateur, que la famille d'en-têtes a changé.
/// </summary>
public static class StatutServeurTexte
{
    /// <summary>
    /// Valeur textuelle d'en-tête vers statut. Comparaison en <c>OrdinalIgnoreCase</c> après <c>Trim</c> :
    /// ni la casse ni l'enrobage d'espaces d'un en-tête HTTP ne sont des informations.
    /// Aucune correspondance n'entraîne JAMAIS un repli sur <see cref="StatutServeur.Autorise"/>.
    /// </summary>
    public static StatutServeur? DepuisEnTete(string? valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur)) return null;   // rien rapporté

        var v = valeur.Trim();

        if (v.Equals("allowed", StringComparison.OrdinalIgnoreCase)) return StatutServeur.Autorise;
        if (v.Equals("allowed_warning", StringComparison.OrdinalIgnoreCase)) return StatutServeur.AutoriseAvertissement;
        if (v.Equals("rejected", StringComparison.OrdinalIgnoreCase)) return StatutServeur.Rejete;

        return StatutServeur.NonReconnu;                      // rapporté, mais hors de l'ensemble connu
    }
}
