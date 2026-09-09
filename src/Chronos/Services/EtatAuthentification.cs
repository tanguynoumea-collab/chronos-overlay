namespace Chronos.Services;

/// <summary>
/// État d'authentification de la source exacte OAuth de Chronos, du point de vue de l'overlay (TOK-02).
/// Quatre valeurs et pas trois : confondre « hors ligne » et « déconnecté » ferait crier
/// « reconnecte-toi » à un utilisateur dont le wifi est simplement coupé — il relancerait un login,
/// échouerait, et perdrait confiance dans le signal.
/// </summary>
public enum EtatAuthentification
{
    /// <summary>Aucun jeton dans le coffre : l'utilisateur ne s'est jamais connecté. N'allume RIEN en
    /// phase 17 — l'invite « jamais connecté » est EXA-05, phase 19.</summary>
    NonConnecte,

    /// <summary>Jeton valide, chiffres exacts en circulation.</summary>
    Connecte,

    /// <summary>Réseau ou serveur momentanément indisponible (429, 5xx, timeout, DNS). Le jeton est
    /// peut-être parfaitement bon : état INFORMATIF, jamais actionnable.</summary>
    HorsLigne,

    /// <summary>Le serveur a REFUSÉ les identifiants (invalid_grant, invalid_client, 200 inexploitable
    /// après rotation). Seule une reconnexion répare : état ACTIONNABLE.</summary>
    Deconnecte,
}
