using System;
using System.Windows;
using System.Windows.Media;

namespace Chronos.Controls;

/// <summary>
/// CADRAN « afficheur à volets » (piste 4) — rangée de volets (repère périphérique du temps). Le NOMBRE
/// de volets allumés = fraction de fenêtre restante (Fraction 0..1). Le chiffre EXACT du compte à rebours
/// et la luminance de la plaque (quota) sont portés par le XAML autour ; ce contrôle ne dessine que la
/// piste de volets. Volet allumé = OnBrush (blanc chaud), éteint = OffBrush (sombre).
/// </summary>
public sealed class FlapRow : FrameworkElement
{
    public static readonly DependencyProperty FractionProperty =
        DependencyProperty.Register(nameof(Fraction), typeof(double), typeof(FlapRow),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CountProperty =
        DependencyProperty.Register(nameof(Count), typeof(int), typeof(FlapRow),
            new FrameworkPropertyMetadata(6, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty OnBrushProperty =
        DependencyProperty.Register(nameof(OnBrush), typeof(Brush), typeof(FlapRow),
            new FrameworkPropertyMetadata(Frozen(0xF4, 0xF2, 0xEC), FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty OffBrushProperty =
        DependencyProperty.Register(nameof(OffBrush), typeof(Brush), typeof(FlapRow),
            new FrameworkPropertyMetadata(Frozen(0x2A, 0x26, 0x34), FrameworkPropertyMetadataOptions.AffectsRender));

    public double Fraction { get => (double)GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public int    Count    { get => (int)GetValue(CountProperty);       set => SetValue(CountProperty, value); }
    public Brush  OnBrush  { get => (Brush)GetValue(OnBrushProperty);    set => SetValue(OnBrushProperty, value); }
    public Brush  OffBrush { get => (Brush)GetValue(OffBrushProperty);   set => SetValue(OffBrushProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        int n = Math.Max(1, Count);
        double frac = double.IsNaN(Fraction) ? 0.0 : Math.Clamp(Fraction, 0.0, 1.0);
        int lit = (int)Math.Round(frac * n, MidpointRounding.AwayFromZero);

        double gap = 3;
        double fw = (w - (n - 1) * gap) / n;
        if (fw <= 0) return;

        for (int i = 0; i < n; i++)
        {
            double x = i * (fw + gap);
            dc.DrawRoundedRectangle(i < lit ? OnBrush : OffBrush, null, new Rect(x, 0, fw, h), 2, 2);
        }
    }

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    { var s = new SolidColorBrush(Color.FromRgb(r, g, b)); s.Freeze(); return s; }
}
