using System;
using System.Windows.Media;

namespace Chronos.Rendering;

/// <summary>
/// Rampe utilization → couleur. 3 stops : 0.00 → vert, 0.55 → ambre, 1.00 → rouge, couleurs fournies par le thème
/// actif (<c>ChronosTheme.ArcColor</c>, seule rampe vivante — aucune rampe figée sur le thème par défaut).
/// Interpolation LINÉAIRE par canal sur chaque segment. Fonction PURE (testable).
/// </summary>
public static class RampColor
{
    private const double AmberStop = 0.55;

    /// <summary>Rampe à stops fournis par le thème : vert → ambre (0,55) → rouge.</summary>
    public static Color Interpolate(double u, Color green, Color amber, Color red)
    {
        u = Math.Clamp(u, 0.0, 1.0);
        return u <= AmberStop
            ? Lerp(green, amber, u / AmberStop)
            : Lerp(amber, red, (u - AmberStop) / (1.0 - AmberStop));
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }
}
