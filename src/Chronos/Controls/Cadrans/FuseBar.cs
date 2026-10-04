using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Chronos.Rendering;

namespace Chronos.Controls;

/// <summary>
/// CADRAN « fusible » (piste 2), dans les DEUX sens (Orientation). Plan 43-06 : rempli = temps CONSOMMÉ.
/// Horizontal (défaut historique) — le cordon part de la GAUCHE et le front avance vers la droite ; vertical — le
/// cordon monte du BAS. La LONGUEUR du cordon = temps consommé (Fraction 0..1), plein au reset ;
/// l'ÉPAISSEUR (CordThickness) + la couleur (QuotaBrush) = quota. Le temps restant reste un sillon creux
/// (piste sombre), donc le gris reste réservé au quota épuisé. Une étincelle (NotchBrush) marque le front,
/// le « maintenant ». Estimated (plancher « ≥ ») : cordon MUET + trait pointillé (grain), jamais le mark
/// du temps. Toute la géométrie vient de <see cref="GeometrieCadrans"/> (Fusible). Une instance par fenêtre (5 h / 7 j).
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
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(FuseBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty NotchBrushProperty =
        DependencyProperty.Register(nameof(NotchBrush), typeof(Brush), typeof(FuseBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty EstimatedProperty =
        DependencyProperty.Register(nameof(Estimated), typeof(bool), typeof(FuseBar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HasDataProperty =
        DependencyProperty.Register(nameof(HasData), typeof(bool), typeof(FuseBar),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    // Pinceau de l'état « en attente » (temps de reset inconnu) — fourni par le thème (CadranAttente).
    public static readonly DependencyProperty WaitBrushProperty =
        DependencyProperty.Register(nameof(WaitBrush), typeof(Brush), typeof(FuseBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    // Sens de la mèche (CAD-03) : horizontal par défaut (sens historique), vertical dans le gabarit en colonne.
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(FuseBar),
            new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Fraction      { get => (double)GetValue(FractionProperty);      set => SetValue(FractionProperty, value); }
    public double CordThickness { get => (double)GetValue(CordThicknessProperty); set => SetValue(CordThicknessProperty, value); }
    public Brush? QuotaBrush    { get => (Brush?)GetValue(QuotaBrushProperty);     set => SetValue(QuotaBrushProperty, value); }
    public Brush? TrackBrush    { get => (Brush?)GetValue(TrackBrushProperty);     set => SetValue(TrackBrushProperty, value); }
    public Brush? NotchBrush    { get => (Brush?)GetValue(NotchBrushProperty);     set => SetValue(NotchBrushProperty, value); }
    public bool   Estimated     { get => (bool)GetValue(EstimatedProperty);       set => SetValue(EstimatedProperty, value); }
    public bool   HasData       { get => (bool)GetValue(HasDataProperty);         set => SetValue(HasDataProperty, value); }
    public Brush? WaitBrush { get => (Brush?)GetValue(WaitBrushProperty); set => SetValue(WaitBrushProperty, value); }
    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Aucun rembourrage interne : la position et la taille viennent du gabarit (plans 04-05).
        double th = CordThickness;
        var g = GeometrieCadrans.Fusible(new Size(w, h), Orientation, Fraction, th);

        // Sillon creux (piste sombre) sur toute la longueur : le temps restant reste vide, PAS cendre.
        dc.DrawRoundedRectangle(TrackBrush, null, g.Sillon, 2.7, 2.7);

        // EN ATTENTE : pas de temps de reset → cordon neutre pleine longueur (fin), jamais un sillon vide.
        if (!HasData)
        {
            dc.DrawRoundedRectangle(WaitBrush, null, g.Attente, 0.3 * th, 0.3 * th);
            return;
        }

        if (Estimated)
        {
            dc.DrawRoundedRectangle(WithAlpha(QuotaBrush, 0.5), null, g.Cordon, th / 2, th / 2);
            var grain = new Pen(TrackBrush, 1.6) { DashStyle = new DashStyle(new double[] { 1.5, 1.6 }, 0) };
            grain.Freeze();
            dc.DrawLine(grain, g.DebutGrain, g.FinGrain);               // grain = trait brisé sur le cordon
        }
        else
        {
            dc.DrawRoundedRectangle(QuotaBrush, null, g.Cordon, th / 2, th / 2);
        }

        // Front de combustion : étincelle (pinceau thémé, TextePrincipal), le « maintenant ».
        double longueur = Orientation == Orientation.Horizontal ? g.Cordon.Width : g.Cordon.Height;
        if (longueur > 0)
        {
            dc.PushOpacity(0.9);
            dc.DrawEllipse(NotchBrush, null, g.Front, g.RayonEtincelle, g.RayonEtincelle);
            dc.Pop();
        }
    }

    private static Brush? WithAlpha(Brush? b, double f)
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
}
