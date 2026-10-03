using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Chronos.Views;

/// <summary>
/// R7 (phase 42) — déclare la silhouette de geste d'un cadran : la forme dans laquelle un appui gauche compte comme un
/// geste sur le cadran (clic, double-clic, glisser). Chaque vue de cadran pose UNE forme marquée
/// <c>vues:ZoneGeste.Silhouette="True"</c>, peinte avec le token <c>ZoneSilhouette</c> (alpha 1) : sur une fenêtre
/// layered, c'est l'alpha du pixel qui décide si Windows livre le clic à Chronos ou au bureau. Une seule silhouette est
/// visible à la fois (les gabarits masqués sont <c>Collapsed</c>).
/// </summary>
public static class ZoneGeste
{
    /// <summary>Marque la forme qui porte la silhouette de geste de son cadran.</summary>
    public static readonly DependencyProperty SilhouetteProperty = DependencyProperty.RegisterAttached(
        "Silhouette", typeof(bool), typeof(ZoneGeste), new PropertyMetadata(false));

    public static bool GetSilhouette(DependencyObject o) => (bool)o.GetValue(SilhouetteProperty);

    public static void SetSilhouette(DependencyObject o, bool v) => o.SetValue(SilhouetteProperty, v);

    /// <summary>
    /// Toutes les silhouettes VISIBLES sous <paramref name="racine"/>. Parcours en profondeur de l'arbre visuel qui SAUTE
    /// tout sous-arbre dont un élément a <c>Visibility != Visible</c> (cadran d'un autre style, gabarit d'une autre
    /// orientation). On teste <c>Visibility</c> et non la propriété calculée de visibilité effective : celle-ci vaut faux
    /// pour toute fenêtre jamais affichée (tests hors écran), elle rendrait la recherche toujours vide.
    /// On parcourt l'arbre visuel plutôt que de chercher par nom : chaque UserControl de cadran a sa propre portée de
    /// noms, un nom posé dans une vue est invisible depuis la fenêtre (Pitfall 7).
    /// </summary>
    public static IReadOnlyList<Shape> TrouverToutes(DependencyObject racine)
    {
        var trouvees = new List<Shape>();
        Parcourir(racine, trouvees);
        return trouvees;
    }

    /// <summary>La silhouette visible (la première de <see cref="TrouverToutes"/>), ou <c>null</c>.</summary>
    public static Shape? Trouver(DependencyObject racine)
    {
        var toutes = TrouverToutes(racine);
        return toutes.Count > 0 ? toutes[0] : null;
    }

    /// <summary>Le point, exprimé dans le repère de la forme, est-il dans la silhouette ? Faux avant la mise en page
    /// (géométrie rendue encore vide).</summary>
    public static bool Contient(Shape silhouette, Point p) => silhouette.RenderedGeometry.FillContains(p);

    private static void Parcourir(DependencyObject noeud, List<Shape> trouvees)
    {
        if (noeud is UIElement element && element.Visibility != Visibility.Visible)
            return;
        if (noeud is Shape forme && GetSilhouette(forme))
            trouvees.Add(forme);

        var n = VisualTreeHelper.GetChildrenCount(noeud);
        for (var i = 0; i < n; i++)
            Parcourir(VisualTreeHelper.GetChild(noeud, i), trouvees);
    }
}
