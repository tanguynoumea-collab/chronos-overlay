namespace Chronos.Placement;

/// <summary>
/// Coin d'écran où l'overlay s'accroche. Persisté dans settings.json (coin + device = vérité,
/// X/Y purement indicatifs) et restauré au démarrage via <see cref="CornerSnap.CornerToTopLeft"/>.
/// </summary>
public enum OverlayCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}
