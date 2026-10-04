using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Chronos.Rendering;

namespace Chronos.Controls;

/// <summary>
/// CADRAN « marée » (piste 3), dans les DEUX sens (Orientation). Plan 43-06 : rempli = temps CONSOMMÉ.
/// Vertical (défaut historique) — la lumière MONTE DU BAS (marée montante) ; horizontal — la lumière part de la
/// GAUCHE et la ligne d'eau devient une onde verticale. L'étendue de lumière = temps consommé (Fraction 0..1),
/// pleine au reset ;
/// la LUMINANCE de la partie éclairée (QuotaBrush) = quota. Estimated (plancher « ≥ ») : ligne d'eau
/// frangée + grain sur la partie éclairée, jamais sur l'étendue (le temps). Géométrie : <see cref="GeometrieCadrans"/> (Marée).
/// Une instance par fenêtre.
/// </summary>
public sealed class TideColumn : FrameworkElement
{
    public static readonly DependencyProperty FractionProperty =
        DependencyProperty.Register(nameof(Fraction), typeof(double), typeof(TideColumn),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty QuotaBrushProperty =
        DependencyProperty.Register(nameof(QuotaBrush), typeof(Brush), typeof(TideColumn),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(TideColumn),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty WaterlineBrushProperty =
        DependencyProperty.Register(nameof(WaterlineBrush), typeof(Brush), typeof(TideColumn),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty EstimatedProperty =
        DependencyProperty.Register(nameof(Estimated), typeof(bool), typeof(TideColumn),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HasDataProperty =
        DependencyProperty.Register(nameof(HasData), typeof(bool), typeof(TideColumn),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    // Pinceau de l'état « en attente » (temps de reset inconnu) — fourni par le thème (CadranAttente).
    public static readonly DependencyProperty WaitBrushProperty =
        DependencyProperty.Register(nameof(WaitBrush), typeof(Brush), typeof(TideColumn),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    // Sens de la marée (CAD-03) : vertical par défaut (sens historique), horizontal dans le gabarit en bande.
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(TideColumn),
            new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Fraction       { get => (double)GetValue(FractionProperty);       set => SetValue(FractionProperty, value); }
    public Brush? QuotaBrush     { get => (Brush?)GetValue(QuotaBrushProperty);      set => SetValue(QuotaBrushProperty, value); }
    public Brush? TrackBrush     { get => (Brush?)GetValue(TrackBrushProperty);      set => SetValue(TrackBrushProperty, value); }
    public Brush? WaterlineBrush { get => (Brush?)GetValue(WaterlineBrushProperty);  set => SetValue(WaterlineBrushProperty, value); }
    public bool   Estimated      { get => (bool)GetValue(EstimatedProperty);        set => SetValue(EstimatedProperty, value); }
    public bool   HasData        { get => (bool)GetValue(HasDataProperty);          set => SetValue(HasDataProperty, value); }
    public Brush? WaitBrush { get => (Brush?)GetValue(WaitBrushProperty); set => SetValue(WaitBrushProperty, value); }
    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Aucun rembourrage interne : la position et la taille viennent du gabarit (plans 04-05).
        var g = GeometrieCadrans.Maree(new Size(w, h), Orientation, Fraction);
        dc.DrawRoundedRectangle(TrackBrush, null, g.Canal, 5, 5);

        dc.PushClip(new RectangleGeometry(g.Canal, 5, 5));

        // EN ATTENTE : pas de temps de reset → canal voilé d'un neutre translucide, jamais un vide.
        if (!HasData)
        {
            dc.DrawRectangle(WaitBrush, null, g.Canal);
            dc.Pop();
            return;
        }

        dc.DrawRectangle(QuotaBrush, null, g.Lumiere);                  // lumière = temps consommé
        if (Estimated)
        {
            var grain = new Pen(WithAlpha(TrackBrush, 0.7), 1.2); grain.Freeze();
            foreach (var (a, b) in GeometrieCadrans.GrainMaree(g.Lumiere, Orientation))
                dc.DrawLine(grain, a, b);
        }
        dc.Pop();

        if (!g.LigneVisible) return;

        // Ligne d'eau = front de la lumière : droite en vertical, ondulée en horizontal (pointillée si plancher).
        var wl = Estimated
            ? new Pen(WithAlpha(WaterlineBrush, 0.5), 1) { DashStyle = new DashStyle(new double[] { 3, 2 }, 0) }
            : new Pen(WithAlpha(WaterlineBrush, 0.85), 1.6);
        wl.Freeze();
        if (Orientation == Orientation.Vertical)
            dc.DrawLine(wl, new Point(0, g.PositionLigne), new Point(w, g.PositionLigne));
        else
            dc.DrawGeometry(null, wl, GeometrieCadrans.LigneDEauOndulee(g.PositionLigne, h));
    }

    private static Brush? WithAlpha(Brush? b, double f)
    {
        if (b is SolidColorBrush s)
        {
            var c = s.Color;
            var nb = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp((c.A == 0 ? 255 : c.A) * f, 0, 255), c.R, c.G, c.B));
            nb.Freeze();
            return nb;
        }
        return b;
    }
}
