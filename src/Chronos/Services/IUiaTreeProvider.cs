namespace Chronos.Services;

/// <summary>
/// Seam INJECTABLE isolant l'accès réel à UI Automation. Retourne un arbre NEUTRE
/// (<see cref="UiaNode"/>) de la fenêtre de l'app bureau Claude, ou <c>null</c> si la fenêtre
/// est absente / indisponible. NE LÈVE JAMAIS : toute erreur d'accès à l'arbre est une
/// dégradation (retourne null), pas une exception qui remonte.
/// </summary>
public interface IUiaTreeProvider
{
    /// <summary>Arbre neutre de la fenêtre Claude, ou null si absente/indisponible. NE LÈVE JAMAIS.</summary>
    UiaNode? TryGetTree();
}
