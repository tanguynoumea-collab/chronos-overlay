using System;
using System.Windows;
using System.Windows.Media;

namespace Chronos.Rendering;

/// <summary>
/// Géométrie d'arc PURE (aucun état, aucun I/O). Repère WPF Y-inversé : 0° = 12 h (haut),
/// sens horaire. point = centre + R·(sin θ, −cos θ).
/// </summary>
public static class ArcGeometry
{
    /// <summary>Point sur le cercle à l'angle <paramref name="deg"/> (0° = 12 h, horaire).</summary>
    public static Point PointAt(Point center, double radius, double deg)
    {
        double r = deg * Math.PI / 180.0;
        return new Point(center.X + radius * Math.Sin(r),
                         center.Y - radius * Math.Cos(r)); // −cos compense Y vers le bas
    }

    /// <summary>
    /// Arc de <paramref name="fraction"/> (0..1) depuis <paramref name="startAngle"/>, horaire.
    /// fraction ≤ 0 ou NaN → Geometry.Empty (invisible, AUCUNE exception).
    /// fraction ≥ 1 → EllipseGeometry (anneau plein sans micro-fente). Sinon ArcSegment ouvert.
    /// </summary>
    public static Geometry Build(Point center, double radius, double startAngle, double fraction)
    {
        if (double.IsNaN(fraction) || fraction <= 0.0)
            return Geometry.Empty;                                   // sweep 0 : rien, pas d'exception
        if (fraction >= 1.0)
            return new EllipseGeometry(center, radius, radius);      // anneau plein sans micro-fente

        double sweep = 360.0 * fraction;                            // ∈ ]0, 360[
        var start = PointAt(center, radius, startAngle);
        var end   = PointAt(center, radius, startAngle + sweep);

        var fig = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        fig.Segments.Add(new ArcSegment(
            point:          end,
            size:           new Size(radius, radius),
            rotationAngle:  0,
            isLargeArc:     sweep > 180.0,                           // PIÈGE #1 résolu
            sweepDirection: SweepDirection.Clockwise,
            isStroked:      true));
        return new PathGeometry(new[] { fig });
    }
}
