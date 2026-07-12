using System;
using System.Windows;
using System.Windows.Media;

namespace Chronos.Controls;

/// <summary>
/// CADRAN « marée » (piste 3). Une colonne verticale : la HAUTEUR de lumière restante (par le haut)
/// = temps restant (Fraction 0..1) — l'ombre monte par le bas et « referme » la fenêtre vers le reset.
/// La LUMINANCE de la partie éclairée (QuotaBrush) = quota. Estimated (repli JSONL) : waterline
/// frangée + grain sur la partie éclairée, jamais sur la hauteur (le temps). Une instance par fenêtre.
/// </summary>
public sealed class TideColumn : FrameworkElement
{
    public static readonly DependencyProperty FractionProperty =
        DependencyProperty.Register(nameof(Fraction), typeof(double), typeof(TideColumn),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty QuotaBrushProperty =
        DependencyProperty.Register(nameof(QuotaBrush), typeof(Brush), typeof(TideColumn),
            new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(TideColumn),
            new FrameworkPropertyMetadata(Frozen(0x14, 0x10, 0x19), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty WaterlineBrushProperty =
        DependencyProperty.Register(nameof(WaterlineBrush), typeof(Brush), typeof(TideColumn),
            new FrameworkPropertyMetadata(Frozen(0xF4, 0xF2, 0xEC), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty EstimatedProperty =
        DependencyProperty.Register(nameof(Estimated), typeof(bool), typeof(TideColumn),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HasDataProperty =
        DependencyProperty.Register(nameof(HasData), typeof(bool), typeof(TideColumn),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush WaitFill = FrozenA(0x5A, 0xB0, 0xAE, 0xBA);

    public double Fraction       { get => (double)GetValue(FractionProperty);       set => SetValue(FractionProperty, value); }
    public Brush  QuotaBrush     { get => (Brush)GetValue(QuotaBrushProperty);      set => SetValue(QuotaBrushProperty, value); }
    public Brush  TrackBrush     { get => (Brush)GetValue(TrackBrushProperty);      set => SetValue(TrackBrushProperty, value); }
    public Brush  WaterlineBrush { get => (Brush)GetValue(WaterlineBrushProperty);  set => SetValue(WaterlineBrushProperty, value); }
    public bool   Estimated      { get => (bool)GetValue(EstimatedProperty);        set => SetValue(EstimatedProperty, value); }
    public bool   HasData        { get => (bool)GetValue(HasDataProperty);          set => SetValue(HasDataProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        double pad = 2, x = pad, colW = w - 2 * pad, top = 3, bot = h - 3, colH = bot - top;
        var channel = new Rect(x, top, colW, colH);
        dc.DrawRoundedRectangle(TrackBrush, null, channel, 5, 5);

        // EN ATTENTE : pas de temps de reset → colonne voilée d'un neutre translucide, jamais un vide.
        if (!HasData)
        {
            dc.PushClip(new RectangleGeometry(channel, 5, 5));
            dc.DrawRectangle(WaitFill, null, channel);
            dc.Pop();
            return;
        }

        double frac = double.IsNaN(Fraction) ? 0.0 : Math.Clamp(Fraction, 0.0, 1.0);
        double litH = colH * frac;
        var quota = QuotaBrush ?? Brushes.Gray;

        dc.PushClip(new RectangleGeometry(channel, 5, 5));
        dc.DrawRectangle(quota, null, new Rect(x, top, colW, litH));    // lumière par le HAUT = temps
        if (Estimated)
        {
            var grain = new Pen(WithAlpha(TrackBrush, 0.7), 1.2); grain.Freeze();
            for (double gy = top + 3; gy < top + litH - 1; gy += 4)
                dc.DrawLine(grain, new Point(x + 2, gy), new Point(x + colW - 2, gy));
        }
        dc.Pop();

        double wy = top + litH;                                         // waterline = front de l'ombre
        var wl = Estimated
            ? new Pen(WithAlpha(WaterlineBrush, 0.5), 1) { DashStyle = new DashStyle(new double[] { 3, 2 }, 0) }
            : new Pen(WithAlpha(WaterlineBrush, 0.85), 1.6);
        wl.Freeze();
        if (frac > 0.001 && frac < 0.999)
            dc.DrawLine(wl, new Point(x, wy), new Point(x + colW, wy));
    }

    private static Brush WithAlpha(Brush b, double f)
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

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    { var s = new SolidColorBrush(Color.FromRgb(r, g, b)); s.Freeze(); return s; }

    private static SolidColorBrush FrozenA(byte a, byte r, byte g, byte b)
    { var s = new SolidColorBrush(Color.FromArgb(a, r, g, b)); s.Freeze(); return s; }
}
