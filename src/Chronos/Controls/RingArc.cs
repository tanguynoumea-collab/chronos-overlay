using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Chronos.Rendering;

namespace Chronos.Controls;

/// <summary>
/// Arc d'anneau réutilisable (CAD-07). Dérive de Shape : la géométrie est le pur produit
/// des DP (AffectsRender → redessin auto au changement de binding, pas d'animation).
/// Fraction 0..1 = longueur d'arc (1 = anneau plein). StartAngle = origine (0° = 12 h).
/// L'épaisseur vient de StrokeThickness (hérité), la couleur de Stroke (hérité).
/// Sert AUSSI de « piste » : une instance Fraction=1, Stroke = couleur de piste.
/// </summary>
public sealed class RingArc : Shape
{
    public static readonly DependencyProperty FractionProperty =
        DependencyProperty.Register(nameof(Fraction), typeof(double), typeof(RingArc),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StartAngleProperty =
        DependencyProperty.Register(nameof(StartAngle), typeof(double), typeof(RingArc),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RadiusProperty =
        DependencyProperty.Register(nameof(Radius), typeof(double), typeof(RingArc),
            new FrameworkPropertyMetadata(90d, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Fraction   { get => (double)GetValue(FractionProperty);   set => SetValue(FractionProperty, value); }
    public double StartAngle { get => (double)GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }
    public double Radius     { get => (double)GetValue(RadiusProperty);     set => SetValue(RadiusProperty, value); }

    protected override Geometry DefiningGeometry =>
        ArcGeometry.Build(new Point(ActualWidth / 2, ActualHeight / 2), Radius, StartAngle, Fraction);
}
