using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Chronos.Rendering;
using Chronos.Theming;

namespace Chronos.Converters;

/// <summary>
/// utilization (double?) → Brush. null → neutre (donnée absente, jamais inventée) ;
/// ≥ 1 → gris « épuisé » (CAD-05) ; [0,1[ → rampe vert→ambre→rouge (CAD-04).
/// Brushes gelés (Freeze) = partageables et légers. Aucun MultiBinding (couleur = 1 seule valeur).
/// </summary>
public sealed class UtilizationToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Neutre = Frozen(0x6E, 0x6D, 0x7A); // gris ardoise VISIBLE : la longueur (temps restant) est fiable même sans utilization — l'arc doit se voir sur la piste, sans emprunter ni la rampe ni le gris « épuisé »
    // gris épuisé du thème par défaut ; ce convertisseur n'est utilisé par aucun XAML (le thème actif passe par ChronosTheme.ArcBrush)
    private static readonly SolidColorBrush Epuise = Frozen(ThemeCatalog.Default.Epuise);

    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        if (value is not double u) return Neutre;      // null / non-double → neutre
        if (u >= 1.0)             return Epuise;        // quota épuisé
        var b = new SolidColorBrush(RampColor.Interpolate(u)); b.Freeze();
        return b;
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    => Frozen(Color.FromRgb(r, g, b));

    private static SolidColorBrush Frozen(Color couleur)
    { var s = new SolidColorBrush(couleur); s.Freeze(); return s; }
}
