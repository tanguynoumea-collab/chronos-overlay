using System;
using System.Windows;
using System.Windows.Media;

namespace Chronos.Controls;

/// <summary>
/// CADRAN « anneau de braises » (piste 1). Une couronne de N pastilles discrètes sur un cercle.
/// Le NOMBRE de braises allumées = temps restant (Fraction 0..1) ; la couleur = quota (QuotaBrush,
/// thémé). La dernière braise allumée est à demi-lueur (incertitude native ±1 braise) ; Estimated
/// (repli JSONL) rend les braises allumées en CONTOUR pointillé (grain) au lieu du plein.
/// FrameworkElement + OnRender (per-pip) car un Shape ne porte qu'un Stroke/Fill unique.
/// </summary>
public sealed class EmberRingControl : FrameworkElement
{
    public static readonly DependencyProperty FractionProperty =
        DependencyProperty.Register(nameof(Fraction), typeof(double), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CountProperty =
        DependencyProperty.Register(nameof(Count), typeof(int), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(16, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty RadiusProperty =
        DependencyProperty.Register(nameof(Radius), typeof(double), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(60d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PipRadiusProperty =
        DependencyProperty.Register(nameof(PipRadius), typeof(double), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(4d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty QuotaBrushProperty =
        DependencyProperty.Register(nameof(QuotaBrush), typeof(Brush), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty AshBrushProperty =
        DependencyProperty.Register(nameof(AshBrush), typeof(Brush), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(Frozen(0x46, 0x44, 0x4F), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty EstimatedProperty =
        DependencyProperty.Register(nameof(Estimated), typeof(bool), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Fraction  { get => (double)GetValue(FractionProperty);  set => SetValue(FractionProperty, value); }
    public int    Count     { get => (int)GetValue(CountProperty);        set => SetValue(CountProperty, value); }
    public double Radius    { get => (double)GetValue(RadiusProperty);    set => SetValue(RadiusProperty, value); }
    public double PipRadius { get => (double)GetValue(PipRadiusProperty); set => SetValue(PipRadiusProperty, value); }
    public Brush  QuotaBrush { get => (Brush)GetValue(QuotaBrushProperty); set => SetValue(QuotaBrushProperty, value); }
    public Brush  AshBrush   { get => (Brush)GetValue(AshBrushProperty);   set => SetValue(AshBrushProperty, value); }
    public bool   Estimated  { get => (bool)GetValue(EstimatedProperty);   set => SetValue(EstimatedProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        int n = Math.Max(1, Count);
        double frac = double.IsNaN(Fraction) ? 0.0 : Math.Clamp(Fraction, 0.0, 1.0);
        int lit = (int)Math.Round(frac * n, MidpointRounding.AwayFromZero);

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var quota = QuotaBrush ?? Brushes.Gray;
        var ash = AshBrush ?? Brushes.DimGray;

        Pen? estPen = null;
        if (Estimated) { estPen = new Pen(quota, 1.4) { DashStyle = new DashStyle(new double[] { 1.4, 1.4 }, 0) }; estPen.Freeze(); }
        var halfQuota = WithAlpha(quota, 0.45);

        for (int i = 0; i < n; i++)
        {
            double a = i * 360.0 / n * Math.PI / 180.0;                 // 0 = 12 h, horaire
            var p = new Point(center.X + Radius * Math.Sin(a), center.Y - Radius * Math.Cos(a));

            if (i < lit)
            {
                bool last = i == lit - 1;
                if (Estimated)
                    dc.DrawEllipse(null, estPen, p, PipRadius, PipRadius);           // grain = braise en contour pointillé
                else
                    dc.DrawEllipse(last ? halfQuota : quota, null, p, PipRadius, PipRadius); // dernière = demi-lueur
            }
            else
            {
                dc.DrawEllipse(ash, null, p, PipRadius * 0.82, PipRadius * 0.82);    // cendre
            }
        }
    }

    private static Brush WithAlpha(Brush b, double f)
    {
        if (b is SolidColorBrush s)
        {
            var c = s.Color;
            var nb = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(c.A * f, 0, 255), c.R, c.G, c.B));
            nb.Freeze();
            return nb;
        }
        return b;
    }

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    { var s = new SolidColorBrush(Color.FromRgb(r, g, b)); s.Freeze(); return s; }
}
