using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Chronos.Rendering;

namespace Chronos.Controls;

/// <summary>
/// Anneau de graduations (CAD-01) rendu en UNE passe géométrique (GeometryGroup) → un seul visuel,
/// léger sous fenêtre layered (rendu logiciel, AllowsTransparency). Dérive de Shape comme RingArc.
/// Deux instances empilées en XAML : mineurs (Count=60, court) et majeurs (Count=12, long/épais).
/// Couleur = Stroke, épaisseur = StrokeThickness (posés en XAML avec les tokens).
/// </summary>
public sealed class TickRing : Shape
{
    public static readonly DependencyProperty CountProperty =
        DependencyProperty.Register(nameof(Count), typeof(int), typeof(TickRing),
            new FrameworkPropertyMetadata(60, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RadiusProperty =
        DependencyProperty.Register(nameof(Radius), typeof(double), typeof(TickRing),
            new FrameworkPropertyMetadata(96d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TickLengthProperty =
        DependencyProperty.Register(nameof(TickLength), typeof(double), typeof(TickRing),
            new FrameworkPropertyMetadata(6d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StartAngleProperty =
        DependencyProperty.Register(nameof(StartAngle), typeof(double), typeof(TickRing),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public int    Count      { get => (int)GetValue(CountProperty);        set => SetValue(CountProperty, value); }
    public double Radius     { get => (double)GetValue(RadiusProperty);     set => SetValue(RadiusProperty, value); }
    public double TickLength { get => (double)GetValue(TickLengthProperty); set => SetValue(TickLengthProperty, value); }
    public double StartAngle { get => (double)GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }

    protected override Geometry DefiningGeometry
    {
        get
        {
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            var group = new GeometryGroup();
            if (Count <= 0) return group;
            for (int i = 0; i < Count; i++)
            {
                double a = StartAngle + i * 360.0 / Count;
                var outer = ArcGeometry.PointAt(center, Radius, a);
                var inner = ArcGeometry.PointAt(center, Radius - TickLength, a);
                group.Children.Add(new LineGeometry(inner, outer));
            }
            return group;
        }
    }
}
