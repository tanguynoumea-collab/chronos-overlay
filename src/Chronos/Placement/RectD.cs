namespace Chronos.Placement;

/// <summary>
/// Rectangle NEUTRE en unités agnostiques (double). Volontairement sans aucun type WPF
/// (pas de <c>System.Windows.Rect</c>) : la logique de placement reste pure et testable
/// en [Fact] sans écran ni contexte STA. Les coordonnées sont interprétées par l'appelant
/// (DIU côté WPF, pixels physiques côté interop) — ici on ne fait que de la géométrie.
/// </summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    /// <summary>Bord droit = X + largeur.</summary>
    public double Right => X + Width;

    /// <summary>Bord bas = Y + hauteur.</summary>
    public double Bottom => Y + Height;

    /// <summary>Centre horizontal du rectangle.</summary>
    public double CenterX => X + Width / 2;

    /// <summary>Centre vertical du rectangle.</summary>
    public double CenterY => Y + Height / 2;
}
