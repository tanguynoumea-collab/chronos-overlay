using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Canal LATÉRAL de ce que SEULE la sonde d'en-têtes sait : le dépassement (HDR-04) et sa propre
/// observabilité (HDR-01 / HDR-02 / HDR-06). Type NEUTRE : aucun type WPF.
///
/// Motif IDENTIQUE à <see cref="IAuthStatus"/> : le service expose l'événement, l'abonné marshalle
/// lui-même via <c>IUiDispatcher</c> (frontière de thread unique, RAF-04).
///
/// POURQUOI un canal latéral et pas seulement un champ de <c>WindowState</c> : quand la forme
/// « dépassement » est SEULE (autre type de compte, aucune fenêtre 5 h ni 7 j), les deux fenêtres de la
/// sonde sont légitimement <c>Unavailable</c> — <c>Best()</c> retient alors l'instance d'une AUTRE source
/// <c>Exact</c> et le dépassement porté par la fenêtre écartée disparaît. Ce n'est pas une hypothèse :
/// <c>CompositeUsageProviderTests.Le_depassement_de_la_fenetre_ecartee_est_JETE_par_le_composite</c> le
/// grave.
///
/// Le dépassement est un fait de COMPTE, produit par UNE seule source, sans règle sensée de fusion par
/// fenêtre : exactement l'argument déjà écrit dans <see cref="IAuthStatus"/> pour l'état
/// d'authentification. Deux canaux, parce qu'aucun des deux ne suffit seul — le champ de
/// <c>WindowState</c> dit « ce dépassement accompagne CETTE fenêtre », le canal latéral dit
/// « ce compte est en dépassement », et le second survit à un <c>Best()</c> défavorable.
///
/// Contrat sans implémentation à ce commit : la sonde l'implémentera (plan 18-04) et le câblage DI est le
/// plan 18-05 — précédent exact d'<see cref="IAuthStatus"/>, déclaré seul au commit 17-02.
/// </summary>
public interface IEtatServeur
{
    /// <summary>Dernier dépassement rapporté. null = rien rapporté (JAMAIS un zéro fabriqué).</summary>
    EtatDepassement? Depassement { get; }

    /// <summary>Issue du dernier passage de la sonde. Aucune chaîne : un corps d'erreur ne peut pas fuir
    /// jusqu'au rapport de diagnostic.</summary>
    ResultatSonde DernierResultat { get; }

    /// <summary>NOMS des en-têtes de limite reconnus au dernier passage — JAMAIS leurs valeurs.
    /// La famille unifiée n'étant pas documentée, c'est le seul moyen honnête de constater sur la machine
    /// de l'utilisateur qu'elle a été renommée : « 200, aucun en-tête unifié reconnu ». Les noms rendus
    /// proviennent TOUJOURS de l'ensemble de constantes de la sonde, jamais du serveur — une valeur venue
    /// du réseau ne doit pas pouvoir se réinjecter dans un affichage.</summary>
    IReadOnlyList<string> NomsEnTetesRecus { get; }

    /// <summary>Émis sur un thread du POOL, sur TRANSITION uniquement (jamais un événement par tick).
    /// L'abonné marshalle lui-même vers le thread UI.</summary>
    event EventHandler<EtatDepassement?>? DepassementChange;
}
