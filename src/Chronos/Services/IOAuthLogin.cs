namespace Chronos.Services;

/// <summary>
/// Pilote le login OAuth intégré de Chronos (ouvre le navigateur, récupère le code collé, échange,
/// stocke les jetons chiffrés) et la déconnexion. Abstraction NEUTRE : l'implémentation (dialogues WPF)
/// vit dans la couche Views, hors pureté Services.
/// </summary>
public interface IOAuthLogin
{
    /// <summary>L'utilisateur est-il connecté (un jeton chiffré est présent) ?</summary>
    bool IsLoggedIn { get; }

    /// <summary>Lance le login interactif (navigateur + collage du code). Renvoie true si connecté.</summary>
    Task<bool> LoginAsync();

    /// <summary>Supprime les jetons (déconnexion).</summary>
    void Logout();
}
