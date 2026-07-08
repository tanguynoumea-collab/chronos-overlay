using System.Windows;
using System.Windows.Media;
using Chronos.Rendering;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve les cas limites géométriques de <see cref="ArcGeometry"/> (CAD-07).
/// Repère WPF Y-inversé : 0° = 12 h (haut), sens horaire. point = centre + R·(sin θ, −cos θ).
/// [WpfFact] (thread STA) requis dès que Build construit des Geometry WPF.
/// </summary>
public class ArcGeometryTests
{
    private static readonly Point Center = new(0, 0);

    // ---- PointAt : repère Y-inversé, 0° = 12 h, sens horaire ----

    [WpfFact]
    public void PointAt_0deg_pointe_vers_le_haut_12h()
    {
        var p = ArcGeometry.PointAt(Center, 10, 0);
        Assert.Equal(0.0, p.X, 6);
        Assert.Equal(-10.0, p.Y, 6); // haut = Y négatif (Y-inversé)
    }

    [WpfFact]
    public void PointAt_90deg_pointe_vers_la_droite_sens_horaire()
    {
        var p = ArcGeometry.PointAt(Center, 10, 90);
        Assert.Equal(10.0, p.X, 6);
        Assert.Equal(0.0, p.Y, 6);
    }

    // ---- Build : cas dégénérés → Geometry.Empty, aucune exception ----

    [WpfFact]
    public void Build_fraction_zero_est_Geometry_Empty()
    {
        var g = ArcGeometry.Build(Center, 10, 0, 0.0);
        Assert.Same(Geometry.Empty, g);
    }

    [WpfFact]
    public void Build_fraction_negative_est_Geometry_Empty()
    {
        var g = ArcGeometry.Build(Center, 10, 0, -0.5);
        Assert.Same(Geometry.Empty, g);
    }

    [WpfFact]
    public void Build_fraction_NaN_est_Geometry_Empty()
    {
        var g = ArcGeometry.Build(Center, 10, 0, double.NaN);
        Assert.Same(Geometry.Empty, g);
    }

    // ---- Build : anneau plein (fraction ≥ 1) → EllipseGeometry, pas de cas dégénéré 360° ----

    [WpfFact]
    public void Build_fraction_1_est_un_EllipseGeometry_anneau_plein()
    {
        var g = ArcGeometry.Build(Center, 10, 0, 1.0);
        Assert.IsType<EllipseGeometry>(g);
    }

    [WpfFact]
    public void Build_fraction_superieure_a_1_est_un_EllipseGeometry_clamp_plein()
    {
        var g = ArcGeometry.Build(Center, 10, 0, 1.5);
        Assert.IsType<EllipseGeometry>(g);
    }

    // ---- Build : ArcSegment ouvert avec IsLargeArc correct ----

    [WpfFact]
    public void Build_fraction_0_25_est_un_ArcSegment_IsLargeArc_false()
    {
        var arc = ArcSegmentOf(ArcGeometry.Build(Center, 10, 0, 0.25));
        Assert.False(arc.IsLargeArc);
    }

    [WpfFact]
    public void Build_fraction_0_75_est_un_ArcSegment_IsLargeArc_true()
    {
        var arc = ArcSegmentOf(ArcGeometry.Build(Center, 10, 0, 0.75));
        Assert.True(arc.IsLargeArc);
    }

    [WpfFact]
    public void Build_fraction_0_5_donne_IsLargeArc_false_condition_stricte_180()
    {
        // sweep = 180° pile : la condition étant > 180 (stricte), IsLargeArc reste false.
        var arc = ArcSegmentOf(ArcGeometry.Build(Center, 10, 0, 0.5));
        Assert.False(arc.IsLargeArc);
    }

    /// <summary>Extrait l'unique ArcSegment d'une géométrie d'arc ouverte.</summary>
    private static ArcSegment ArcSegmentOf(Geometry g)
    {
        var path = Assert.IsType<PathGeometry>(g);
        return Assert.IsType<ArcSegment>(path.Figures[0].Segments[0]);
    }
}
