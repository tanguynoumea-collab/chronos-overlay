using System;
using System.Windows.Media;

namespace Chronos.Rendering;

/// <summary>
/// Rampe utilization → couleur. 3 stops verrouillés :
///   0.00 → vert  #7BB13C   0.55 → ambre #EFA23A   1.00 → rouge #D8503A
/// Interpolation LINÉAIRE par canal sur chaque segment. Fonction PURE (testable).
/// </summary>
public static class RampColor
{
    private const double AmberStop = 0.55;
    private static readonly Color Green = Color.FromRgb(0x7B, 0xB1, 0x3C);
    private static readonly Color Amber = Color.FromRgb(0xEF, 0xA2, 0x3A);
    private static readonly Color Red   = Color.FromRgb(0xD8, 0x50, 0x3A);

    public static Color Interpolate(double u)
    {
        u = Math.Clamp(u, 0.0, 1.0);
        return u <= AmberStop
            ? Lerp(Green, Amber, u / AmberStop)
            : Lerp(Amber, Red, (u - AmberStop) / (1.0 - AmberStop));
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
