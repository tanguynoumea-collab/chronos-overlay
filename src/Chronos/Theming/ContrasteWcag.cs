using System;
using System.Windows.Media;

namespace Chronos.Theming;

/// <summary>
/// Luminance relative sRGB et rapport de contraste WCAG 2.x (fonctions pures). Couleurs OPAQUES uniquement :
/// l'alpha est ignoré — mesurer un pinceau translucide exige d'abord de le composer sur son fond.
/// </summary>
public static class ContrasteWcag
{
    /// <summary>Luminance relative WCAG : 0,2126·R + 0,7152·G + 0,0722·B sur les canaux linéarisés.</summary>
    public static double Luminance(Color c) => 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);

    /// <summary>Rapport de contraste (Lmax + 0,05) / (Lmin + 0,05), de 1 à 21, symétrique.</summary>
    public static double Ratio(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary>Linéarisation sRGB d'un canal 8 bits.</summary>
    private static double Lin(byte v)
    {
        double s = v / 255.0;
        return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}
