namespace Chronos.Placement;

/// <summary>
/// Logique PURE d'accroche aux coins (FEN-03). Trois fonctions sans état ni I/O, sans aucun
/// type WPF : elles ne manipulent que des <see cref="RectD"/> et des <see cref="OverlayCorner"/>.
/// Consommées par l'OverlayController (06-03) pour poser physiquement la fenêtre ; ici tout est
/// vérifiable en [Fact] sans moniteur réel ni contexte STA.
///
/// Convention : la marge est retranchée/ajoutée exactement une fois par axe, de sorte que la
/// fenêtre reste entièrement dans la zone de travail avec un liseré constant.
/// </summary>
public static class CornerSnap
{
    /// <summary>
    /// Renvoie le top-left du coin le PLUS PROCHE du centre de la fenêtre (accroche automatique
    /// après un drag). Le quadrant est déterminé par la position du centre fenêtre vis-à-vis du
    /// centre de la zone de travail.
    /// </summary>
    public static (double X, double Y) NearestCorner(RectD window, RectD workArea, double margin)
    {
        bool left = window.CenterX < workArea.CenterX;
        bool top = window.CenterY < workArea.CenterY;
        double x = left ? workArea.X + margin : workArea.Right - window.Width - margin;
        double y = top ? workArea.Y + margin : workArea.Bottom - window.Height - margin;
        return (x, y);
    }

    /// <summary>
    /// Classe la fenêtre dans un des quatre quadrants (coin le plus proche) sans calculer de
    /// position — sert à persister le coin choisi (coin + device = vérité).
    /// </summary>
    public static OverlayCorner ClassifyCorner(RectD window, RectD workArea)
    {
        bool left = window.CenterX < workArea.CenterX;
        bool top = window.CenterY < workArea.CenterY;
        return (left, top) switch
        {
            (true, true) => OverlayCorner.TopLeft,
            (false, true) => OverlayCorner.TopRight,
            (true, false) => OverlayCorner.BottomLeft,
            _ => OverlayCorner.BottomRight,
        };
    }

    /// <summary>
    /// Restauration d'un coin PERSISTÉ : pour un <see cref="OverlayCorner"/> imposé, renvoie le
    /// top-left cible dans la zone de travail (marge respectée). Consommé par l'OverlayController
    /// (06-03) au démarrage, où le coin persisté prime sur les X/Y indicatifs.
    /// </summary>
    public static (double X, double Y) CornerToTopLeft(OverlayCorner corner, RectD window, RectD workArea, double margin)
    {
        bool left = corner is OverlayCorner.TopLeft or OverlayCorner.BottomLeft;
        bool top = corner is OverlayCorner.TopLeft or OverlayCorner.TopRight;
        double x = left ? workArea.X + margin : workArea.Right - window.Width - margin;
        double y = top ? workArea.Y + margin : workArea.Bottom - window.Height - margin;
        return (x, y);
    }
}
