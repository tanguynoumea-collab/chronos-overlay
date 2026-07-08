using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Chronos.Rendering;

namespace Chronos.Converters;

/// <summary>
/// utilization (double?) → Brush. null → neutre (donnée absente, jamais inventée) ;
/// ≥ 1 → gris « épuisé » (CAD-05) ; [0,1[ → rampe vert→ambre→rouge (CAD-04).
/// Brushes gelés (Freeze) = partageables et légers. Aucun MultiBinding (couleur = 1 seule valeur).
/// </summary>
public sealed class UtilizationToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Neutre = Frozen(0x2A, 0x29, 0x32); // teinte piste douce
    private static readonly SolidColorBrush Epuise = Frozen(0x5A, 0x59, 0x60); // #5A5960

    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        if (value is not double u) return Neutre;      // null / non-double → neutre
        if (u >= 1.0)             return Epuise;        // quota épuisé
        var b = new SolidColorBrush(RampColor.Interpolate(u)); b.Freeze();
        return b;
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    { var s = new SolidColorBrush(Color.FromRgb(r, g, b)); s.Freeze(); return s; }
}
