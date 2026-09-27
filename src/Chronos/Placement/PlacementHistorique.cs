namespace Chronos.Placement;

/// <summary>Écran virtuel en unités agnostiques (DIU côté WPF) : origine possiblement NÉGATIVE (moniteur à gauche du primaire).</summary>
public readonly record struct RectangleEcran(double Gauche, double Haut, double Largeur, double Hauteur);

/// <summary>
/// HIS-01 — bornage PUR de la géométrie persistée de la fenêtre Historique, sans aucun type WPF (motif <see cref="RectD"/> /
/// <c>CornerSnap</c>). Pitfall 2 de la recherche 34 : un moniteur débranché ne doit pas cacher la fenêtre — la position et la taille
/// relues dans <c>settings.json</c> sont ramenées dans l'écran virtuel courant et au-dessus des minima AVANT <c>Show</c>.
/// La fenêtre ne fait qu'appliquer le résultat ; la galerie <c>--historique</c> passe des réglages en mémoire.
/// </summary>
public static class PlacementHistorique
{
    /// <summary>
    /// Rend la géométrie bornée, ou <c>null</c> si un des quatre champs manque (ou n'est pas un nombre fini) : la fenêtre garde alors
    /// son défaut (920 × 610, centrée). Ordre : la taille d'abord (minima, puis l'écran), la position ensuite (dans l'écran, compte tenu
    /// de la taille déjà réduite).
    /// </summary>
    public static (double X, double Y, double Largeur, double Hauteur)? Borner(
        (double? X, double? Y, double? Largeur, double? Hauteur) g, RectangleEcran ecran, double largeurMin, double hauteurMin)
    {
        if (g.X is not { } x || g.Y is not { } y || g.Largeur is not { } l || g.Hauteur is not { } h) return null;
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(l) || !double.IsFinite(h)) return null;

        l = Serrer(l, largeurMin, ecran.Largeur);
        h = Serrer(h, hauteurMin, ecran.Hauteur);
        x = Serrer(x, ecran.Gauche, ecran.Gauche + ecran.Largeur - l);
        y = Serrer(y, ecran.Haut, ecran.Haut + ecran.Hauteur - h);
        return (x, y, l, h);
    }

    /// <summary>Clamp tolérant : si le minimum dépasse le maximum (écran plus petit que le minimum), le minimum l'emporte.</summary>
    private static double Serrer(double v, double min, double max)
        => v < min ? min : (v > max ? System.Math.Max(min, max) : v);
}
