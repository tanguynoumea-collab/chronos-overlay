namespace Chronos.Services;

/// <summary>
/// Canal d'état d'authentification exposé aux ViewModels (TOK-02/TOK-03). Type NEUTRE : aucun type WPF.
///
/// Motif IDENTIQUE à <see cref="RefreshOrchestrator.SnapshotChanged"/> : le service expose l'événement,
/// l'abonné marshalle lui-même via IUiDispatcher (frontière de thread unique, RAF-04). L'état d'auth
/// n'est délibérément PAS un champ d'UsageSnapshot : ce dernier traverse le composite (UN seul en production :
/// sonde → secours OAuth) dont Best() choisit PAR FENÊTRE, et il n'existe aucune règle sensée pour fusionner deux états d'auth issus
/// de branches différentes.
/// </summary>
public interface IAuthStatus
{
    /// <summary>État courant. Lisible à tout instant, y compris avant la première transition.</summary>
    EtatAuthentification Etat { get; }

    /// <summary>Émis sur un thread du POOL à chaque TRANSITION (jamais un événement par tick).
    /// L'abonné marshalle lui-même vers le thread UI.</summary>
    event EventHandler<EtatAuthentification>? EtatChange;

    /// <summary>TOK-03 / P-01 — après un login réussi ET après une déconnexion : relâche le verrou
    /// « Deconnecte » ET le recul, et oublie les jetons mémorisés pour relire le coffre (fraîchement écrit
    /// par le login, ou effacé par la déconnexion). Sans cet appel, le jeton tout neuf ne serait pas
    /// utilisé — ou le rafraîchissement suivant recréerait le coffre avec les jetons de l'ancien compte.</summary>
    void ReinitialiserApresLogin();
}
