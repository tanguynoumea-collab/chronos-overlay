using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Chronos.Rendering;

namespace Chronos.Controls;

/// <summary>
/// CADRAN « anneau de braises » (piste 1). Une couronne de N pastilles discrètes sur un cercle.
/// Le NOMBRE de braises allumées = temps CONSOMMÉ (Fraction 0..1, plan 43-06) : elles s'allument depuis midi, sens
/// horaire, et l'anneau est plein au reset (retour à midi) ; la couleur = quota (QuotaBrush, thémé). La dernière braise allumée est à demi-lueur (incertitude native ±1 braise) ; Estimated
/// (plancher « ≥ ») rend les braises allumées en CONTOUR pointillé (grain) au lieu du plein.
/// Groupes optionnels (GroupSize / GroupPitch, via BraisesGeometrie) : les braises d'un groupe sont espacées d'un pas
/// fixe et le groupe est centré dans son secteur — la délimitation est le VIDE entre groupes, jamais un tiret.
/// Sans groupe (défauts 1 / 0) : répartition uniforme historique. L'état « en attente » suit les mêmes angles.
/// Plan 43-09 : <see cref="Angles"/> et <see cref="Etats"/> EXPLICITES (anneau journée de Braises, calculés par
/// <see cref="BraisesJournee"/>) remplacent, quand ils sont fournis avec une entrée par braise, la géométrie de groupe et
/// l'allumage par Fraction. Même dessin des braises (pleine, demi-lueur, contour pointillé si Estimated, cendre).
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
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty AshBrushProperty =
        DependencyProperty.Register(nameof(AshBrush), typeof(Brush), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty EstimatedProperty =
        DependencyProperty.Register(nameof(Estimated), typeof(bool), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    // false = pas de temps de reset (chargement / inconnu) → état « en attente » au lieu du vide.
    public static readonly DependencyProperty HasDataProperty =
        DependencyProperty.Register(nameof(HasData), typeof(bool), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    // Pinceau de l'état « en attente » (temps de reset inconnu) — fourni par le thème (CadranAttente).
    public static readonly DependencyProperty WaitBrushProperty =
        DependencyProperty.Register(nameof(WaitBrush), typeof(Brush), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    // Taille d'un groupe de braises ; 1 = aucune délimitation (répartition uniforme).
    public static readonly DependencyProperty GroupSizeProperty =
        DependencyProperty.Register(nameof(GroupSize), typeof(int), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

    // Pas angulaire entre braises d'un même groupe, en degrés ; 0 = aucun groupe.
    public static readonly DependencyProperty GroupPitchProperty =
        DependencyProperty.Register(nameof(GroupPitch), typeof(double), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    // Plan 43-09 — angles EXPLICITES (degrés, 0 = haut, horaire), un par braise ; null = géométrie Count/GroupSize/GroupPitch.
    public static readonly DependencyProperty AnglesProperty =
        DependencyProperty.Register(nameof(Angles), typeof(IReadOnlyList<double>), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    // Plan 43-09 — allumage EXPLICITE, un état par braise ; null = allumage par Fraction (temps consommé).
    public static readonly DependencyProperty EtatsProperty =
        DependencyProperty.Register(nameof(Etats), typeof(IReadOnlyList<EtatBraise>), typeof(EmberRingControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Angles { get => (IReadOnlyList<double>?)GetValue(AnglesProperty); set => SetValue(AnglesProperty, value); }
    public IReadOnlyList<EtatBraise>? Etats { get => (IReadOnlyList<EtatBraise>?)GetValue(EtatsProperty); set => SetValue(EtatsProperty, value); }

    public double Fraction  { get => (double)GetValue(FractionProperty);  set => SetValue(FractionProperty, value); }
    public int    Count     { get => (int)GetValue(CountProperty);        set => SetValue(CountProperty, value); }
    public double Radius    { get => (double)GetValue(RadiusProperty);    set => SetValue(RadiusProperty, value); }
    public double PipRadius { get => (double)GetValue(PipRadiusProperty); set => SetValue(PipRadiusProperty, value); }
    public Brush? QuotaBrush { get => (Brush?)GetValue(QuotaBrushProperty); set => SetValue(QuotaBrushProperty, value); }
    public Brush? AshBrush   { get => (Brush?)GetValue(AshBrushProperty);   set => SetValue(AshBrushProperty, value); }
    public bool   Estimated  { get => (bool)GetValue(EstimatedProperty);   set => SetValue(EstimatedProperty, value); }
    public bool   HasData    { get => (bool)GetValue(HasDataProperty);     set => SetValue(HasDataProperty, value); }
    public Brush? WaitBrush { get => (Brush?)GetValue(WaitBrushProperty); set => SetValue(WaitBrushProperty, value); }
    public int    GroupSize  { get => (int)GetValue(GroupSizeProperty);     set => SetValue(GroupSizeProperty, value); }
    public double GroupPitch { get => (double)GetValue(GroupPitchProperty); set => SetValue(GroupPitchProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        int n = Math.Max(1, Count);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);

        // EN ATTENTE : pas de temps de reset (données pas encore chargées) → couronne neutre visible,
        // jamais un vide (qui laisserait croire à un overlay cassé).
        if (!HasData)
        {
            for (int i = 0; i < n; i++)
            {
                double aw = AngleDe(i, n) * Math.PI / 180.0;   // mêmes angles que le nominal
                var pw = new Point(center.X + Radius * Math.Sin(aw), center.Y - Radius * Math.Cos(aw));
                dc.DrawEllipse(WaitBrush, null, pw, PipRadius * 0.7, PipRadius * 0.7);
            }
            return;
        }

        double frac = double.IsNaN(Fraction) ? 0.0 : Math.Clamp(Fraction, 0.0, 1.0);
        int lit = (int)Math.Round(frac * n, MidpointRounding.AwayFromZero);
        var etats = Etats is { } e && e.Count == n ? e : null;   // allumage explicite (anneau journée), sinon Fraction
        var quota = QuotaBrush;
        var ash = AshBrush;

        Pen? estPen = null;
        if (Estimated) { estPen = new Pen(quota, 1.4) { DashStyle = new DashStyle(new double[] { 1.4, 1.4 }, 0) }; estPen.Freeze(); }
        var halfQuota = WithAlpha(quota, 0.45);

        for (int i = 0; i < n; i++)
        {
            double a = AngleDe(i, n) * Math.PI / 180.0;   // 0 = haut, horaire
            var p = new Point(center.X + Radius * Math.Sin(a), center.Y - Radius * Math.Cos(a));

            var etat = etats is not null ? etats[i]
                     : i < lit - 1 ? EtatBraise.Pleine
                     : i == lit - 1 ? EtatBraise.DemiLueur   // dernière braise allumée = demi-lueur (incertitude ±1 braise)
                     : EtatBraise.Cendre;

            if (etat != EtatBraise.Cendre)
            {
                bool last = etat == EtatBraise.DemiLueur;
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

    /// <summary>Angle (degrés) de la braise i : explicite si <see cref="Angles"/> en fournit un par braise, sinon géométrie de groupe.</summary>
    private double AngleDe(int i, int n)
        => Angles is { } a && a.Count == n ? a[i] : BraisesGeometrie.Angle(i, n, GroupSize, GroupPitch);

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
