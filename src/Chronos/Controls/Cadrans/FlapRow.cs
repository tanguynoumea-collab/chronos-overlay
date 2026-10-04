using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Chronos.Rendering;

namespace Chronos.Controls;

/// <summary>
/// CADRAN « afficheur à volets » (piste 4) — piste de volets (repère périphérique du temps), dans les DEUX sens
/// (Orientation) : horizontal (défaut historique) — volets en ligne, allumés depuis la gauche ; vertical — volets
/// empilés, allumés depuis le BAS. Le NOMBRE de volets allumés = fraction de fenêtre CONSOMMÉE (Fraction 0..1) :
/// tout éteint en début de fenêtre, tout allumé au reset (plan 43-06).
/// Le chiffre EXACT du compte à rebours et la luminance de la plaque (quota) sont portés par le XAML autour ;
/// ce contrôle ne dessine que la piste de volets. Volet allumé = OnBrush, éteint = OffBrush.
/// Géométrie : <see cref="GeometrieCadrans"/> (Volets) (écart 2,5, maquette).
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
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty OffBrushProperty =
        DependencyProperty.Register(nameof(OffBrush), typeof(Brush), typeof(FlapRow),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HasDataProperty =
        DependencyProperty.Register(nameof(HasData), typeof(bool), typeof(FlapRow),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    // Pinceau de l'état « en attente » (temps de reset inconnu) — fourni par le thème (CadranAttente).
    public static readonly DependencyProperty WaitBrushProperty =
        DependencyProperty.Register(nameof(WaitBrush), typeof(Brush), typeof(FlapRow),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    // Sens de la piste (CAD-03) : horizontal par défaut (sens historique), vertical dans le gabarit en colonne.
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(FlapRow),
            new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Fraction { get => (double)GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public int    Count    { get => (int)GetValue(CountProperty);       set => SetValue(CountProperty, value); }
    public Brush? OnBrush  { get => (Brush?)GetValue(OnBrushProperty);    set => SetValue(OnBrushProperty, value); }
    public Brush? OffBrush { get => (Brush?)GetValue(OffBrushProperty);   set => SetValue(OffBrushProperty, value); }
    public bool   HasData  { get => (bool)GetValue(HasDataProperty);     set => SetValue(HasDataProperty, value); }
    public Brush? WaitBrush { get => (Brush?)GetValue(WaitBrushProperty); set => SetValue(WaitBrushProperty, value); }
    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var volets = GeometrieCadrans.Volets(new Size(w, h), Orientation, Count);
        if (volets.Count == 0) return;
        int lit = GeometrieCadrans.VoletsAllumes(Fraction, Count);
        int n = volets.Count;

        for (int i = 0; i < volets.Count; i++)
        {
            // EN ATTENTE : volets neutres (ni allumés ni éteints) → jamais une piste vide.
            var brush = !HasData ? WaitBrush : (GeometrieCadrans.VoletAllume(i, n, lit, Orientation) ? OnBrush : OffBrush);
            dc.DrawRoundedRectangle(brush, null, volets[i], 2, 2);
        }
    }
}
