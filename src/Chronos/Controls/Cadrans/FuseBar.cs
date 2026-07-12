using System;
using System.Windows;
using System.Windows.Media;

namespace Chronos.Controls;

/// <summary>
/// CADRAN « fusible » (piste 2). Une mèche horizontale qui se consume. La LONGUEUR du cordon restant
/// (à droite du front) = temps restant (Fraction 0..1) ; l'ÉPAISSEUR (CordThickness) + la couleur
/// (QuotaBrush) = quota. La part écoulée reste un sillon creux (piste sombre), donc le gris reste
/// réservé au quota épuisé. Estimated (repli JSONL) : cordon MUET + trait pointillé (grain), jamais
/// le mark du temps. Une instance par fenêtre (5 h / 7 j).
/// </summary>
public sealed class FuseBar : FrameworkElement
{
    public static readonly DependencyProperty FractionProperty =
        DependencyProperty.Register(nameof(Fraction), typeof(double), typeof(FuseBar),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CordThicknessProperty =
        DependencyProperty.Register(nameof(CordThickness), typeof(double), typeof(FuseBar),
            new FrameworkPropertyMetadata(10d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty QuotaBrushProperty =
        DependencyProperty.Register(nameof(QuotaBrush), typeof(Brush), typeof(FuseBar),
            new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(FuseBar),
            new FrameworkPropertyMetadata(Frozen(0x1D, 0x19, 0x26), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty NotchBrushProperty =
        DependencyProperty.Register(nameof(NotchBrush), typeof(Brush), typeof(FuseBar),
            new FrameworkPropertyMetadata(Frozen(0xF4, 0xF2, 0xEC), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty EstimatedProperty =
        DependencyProperty.Register(nameof(Estimated), typeof(bool), typeof(FuseBar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Fraction      { get => (double)GetValue(FractionProperty);      set => SetValue(FractionProperty, value); }
    public double CordThickness { get => (double)GetValue(CordThicknessProperty); set => SetValue(CordThicknessProperty, value); }
    public Brush  QuotaBrush    { get => (Brush)GetValue(QuotaBrushProperty);     set => SetValue(QuotaBrushProperty, value); }
    public Brush  TrackBrush    { get => (Brush)GetValue(TrackBrushProperty);     set => SetValue(TrackBrushProperty, value); }
    public Brush  NotchBrush    { get => (Brush)GetValue(NotchBrushProperty);     set => SetValue(NotchBrushProperty, value); }
    public bool   Estimated     { get => (bool)GetValue(EstimatedProperty);       set => SetValue(EstimatedProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double pad = 4, x0 = pad, x1 = w - pad, cy = h / 2;
        double frac = double.IsNaN(Fraction) ? 0.0 : Math.Clamp(Fraction, 0.0, 1.0);
        double th = CordThickness;
        var quota = QuotaBrush ?? Brushes.Gray;

        // Sillon creux (piste sombre) sur toute la largeur : la part écoulée reste vide, PAS cendre.
        dc.DrawRoundedRectangle(TrackBrush, null, new Rect(x0, cy - 3, Math.Max(0, x1 - x0), 6), 3, 3);

        double fx = x1 - (x1 - x0) * frac;                              // front de combustion
        var cord = new Rect(fx, cy - th / 2, Math.Max(0, x1 - fx), th); // cordon restant (droite)

        if (Estimated)
        {
            dc.DrawRoundedRectangle(WithAlpha(quota, 0.5), null, cord, th / 2, th / 2);
            var grain = new Pen(TrackBrush, 1.6) { DashStyle = new DashStyle(new double[] { 1.5, 1.6 }, 0) };
            grain.Freeze();
            dc.DrawLine(grain, new Point(fx, cy), new Point(x1, cy));   // grain = trait brisé sur le cordon
        }
        else
        {
            dc.DrawRoundedRectangle(quota, null, cord, th / 2, th / 2);
        }

        // Front de combustion : encoche vive (blanc chaud), le « maintenant ».
        var notch = new Pen(NotchBrush, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        notch.Freeze();
        dc.DrawLine(notch, new Point(fx, cy - th / 2 - 4), new Point(fx, cy + th / 2 + 4));
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
