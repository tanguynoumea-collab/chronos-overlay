using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Chronos.Rendering;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la géométrie PURE par axe des cadrans rectangulaires (CAD-03) : Fusible (cordon depuis la gauche
/// en horizontal, depuis le bas en vertical), Marée (lumière depuis la gauche / le bas, ligne d'eau ondulée),
/// Volets (6 volets séparés de 2,5). Rect/Point/Size sont des structures : tests [Fact] sans fenêtre.
/// Seule la ligne d'eau construit une Geometry WPF : [WpfFact] (thread STA) par prudence.
/// </summary>
public class GeometrieCadransTests
{
    private static void EgalRect(Rect attendu, Rect reel)
    {
        Assert.Equal(attendu.X, reel.X, 6);
        Assert.Equal(attendu.Y, reel.Y, 6);
        Assert.Equal(attendu.Width, reel.Width, 6);
        Assert.Equal(attendu.Height, reel.Height, 6);
    }

    private static void EgalPoint(Point attendu, Point reel)
    {
        Assert.Equal(attendu.X, reel.X, 6);
        Assert.Equal(attendu.Y, reel.Y, 6);
    }

    // ---- Fusible ----

    [Fact]
    public void Fusible_horizontal_le_cordon_consomme_part_de_la_gauche()
    {
        // Plan 43-06 : rempli = temps consommé ; le cordon part de la gauche, le front avance vers la droite.
        var g = GeometrieCadrans.Fusible(new Size(178, 20), Orientation.Horizontal, 0.25, 10);
        EgalRect(new Rect(0, 7.3, 178, 5.4), g.Sillon);
        EgalRect(new Rect(0, 5, 44.5, 10), g.Cordon);
        Assert.Equal(0, g.Cordon.Left, 6);
        EgalPoint(new Point(44.5, 10), g.Front);
        Assert.Equal(6.5, g.RayonEtincelle, 6);
        EgalRect(new Rect(0, 7, 178, 6), g.Attente);
        EgalPoint(new Point(0, 10), g.DebutGrain);
        EgalPoint(new Point(44.5, 10), g.FinGrain);
    }

    [Fact]
    public void Fusible_vertical_le_cordon_consomme_monte_du_bas()
    {
        var g = GeometrieCadrans.Fusible(new Size(20, 138), Orientation.Vertical, 0.25, 10);
        EgalRect(new Rect(7.3, 0, 5.4, 138), g.Sillon);
        EgalRect(new Rect(5, 103.5, 10, 34.5), g.Cordon);
        Assert.Equal(138, g.Cordon.Bottom, 6);
        EgalPoint(new Point(10, 103.5), g.Front);
        Assert.Equal(6.5, g.RayonEtincelle, 6);
        EgalRect(new Rect(7, 0, 6, 138), g.Attente);
        EgalPoint(new Point(10, 138), g.DebutGrain);
        EgalPoint(new Point(10, 103.5), g.FinGrain);
    }

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void Fusible_plein_le_front_est_a_l_arrivee_et_le_cordon_pleine_longueur(Orientation axe)
    {
        var taille = axe == Orientation.Horizontal ? new Size(178, 20) : new Size(20, 138);
        var g = GeometrieCadrans.Fusible(taille, axe, 1, 10);
        if (axe == Orientation.Horizontal)
        {
            Assert.Equal(178, g.Front.X, 6);
            Assert.Equal(178, g.Cordon.Width, 6);
        }
        else
        {
            Assert.Equal(0, g.Front.Y, 6);
            Assert.Equal(138, g.Cordon.Height, 6);
        }
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    public void Fusible_vide_ou_NaN_le_cordon_est_de_longueur_nulle(double fraction)
    {
        var h = GeometrieCadrans.Fusible(new Size(178, 20), Orientation.Horizontal, fraction, 10);
        Assert.Equal(0, h.Cordon.Width, 6);
        Assert.Equal(0, h.Front.X, 6);
        var v = GeometrieCadrans.Fusible(new Size(20, 138), Orientation.Vertical, fraction, 10);
        Assert.Equal(0, v.Cordon.Height, 6);
        Assert.Equal(138, v.Front.Y, 6);
    }

    [Fact]
    public void Fusible_fraction_superieure_a_un_est_bornee()
    {
        var a = GeometrieCadrans.Fusible(new Size(178, 20), Orientation.Horizontal, 1.7, 10);
        var b = GeometrieCadrans.Fusible(new Size(178, 20), Orientation.Horizontal, 1, 10);
        Assert.Equal(b, a);
    }

    [Fact]
    public void Fusible_rayon_d_etincelle_suit_l_epaisseur()
    {
        var g = GeometrieCadrans.Fusible(new Size(178, 20), Orientation.Horizontal, 0.5, 8);
        Assert.Equal(5.5, g.RayonEtincelle, 6);
    }

    // ---- Marée ----

    [Fact]
    public void Maree_horizontale_la_lumiere_part_de_la_gauche()
    {
        var g = GeometrieCadrans.Maree(new Size(102, 32), Orientation.Horizontal, 0.5);
        EgalRect(new Rect(0, 0, 102, 32), g.Canal);
        EgalRect(new Rect(0, 0, 51, 32), g.Lumiere);
        Assert.Equal(51, g.PositionLigne, 6);
        Assert.True(g.LigneVisible);
    }

    [Fact]
    public void Maree_verticale_la_lumiere_monte_du_bas()
    {
        var g = GeometrieCadrans.Maree(new Size(42, 118), Orientation.Vertical, 0.5);
        EgalRect(new Rect(0, 0, 42, 118), g.Canal);
        EgalRect(new Rect(0, 59, 42, 59), g.Lumiere);
        Assert.Equal(59, g.PositionLigne, 6);
        Assert.True(g.LigneVisible);
    }

    [Theory]
    [InlineData(0.0005)]
    [InlineData(0.9995)]
    public void Maree_la_ligne_d_eau_est_masquee_aux_extremes(double fraction)
    {
        Assert.False(GeometrieCadrans.Maree(new Size(42, 118), Orientation.Vertical, fraction).LigneVisible);
        Assert.False(GeometrieCadrans.Maree(new Size(102, 32), Orientation.Horizontal, fraction).LigneVisible);
    }

    [WpfFact]
    public void La_ligne_d_eau_horizontale_est_ondulee()
    {
        var geo = GeometrieCadrans.LigneDEauOndulee(51, 32);
        Assert.True(geo.IsFrozen);
        var pg = Assert.IsType<PathGeometry>(geo);
        var figure = Assert.Single(pg.Figures);
        EgalPoint(new Point(51, 0), figure.StartPoint);
        Assert.Equal(2, figure.Segments.Count);
        var s1 = Assert.IsType<QuadraticBezierSegment>(figure.Segments[0]);
        var s2 = Assert.IsType<QuadraticBezierSegment>(figure.Segments[1]);
        EgalPoint(new Point(54, 8), s1.Point1);
        EgalPoint(new Point(51, 16), s1.Point2);
        EgalPoint(new Point(48, 24), s2.Point1);
        EgalPoint(new Point(51, 32), s2.Point2);
        var b = geo.Bounds;
        Assert.True(b.Width > 0, "la ligne d'eau doit onduler (largeur non nulle)");
        Assert.InRange(b.Left, 48, 54);
        Assert.InRange(b.Right, 48, 54);
    }

    [Fact]
    public void Le_grain_de_maree_horizontale_est_en_traits_verticaux()
    {
        var traits = GeometrieCadrans.GrainMaree(new Rect(0, 0, 51, 32), Orientation.Horizontal);
        var attendus = Enumerable.Range(0, 100).Select(i => 3.0 + 4 * i).TakeWhile(x => x < 50).ToList();
        Assert.Equal(attendus.Count, traits.Count);
        for (int i = 0; i < traits.Count; i++)
        {
            Assert.Equal(traits[i].A.X, traits[i].B.X, 6);
            Assert.Equal(attendus[i], traits[i].A.X, 6);
            Assert.Equal(2, traits[i].A.Y, 6);
            Assert.Equal(30, traits[i].B.Y, 6);
        }
    }

    [Fact]
    public void Le_grain_de_maree_verticale_est_en_traits_horizontaux()
    {
        var traits = GeometrieCadrans.GrainMaree(new Rect(0, 0, 42, 59), Orientation.Vertical);
        var attendus = Enumerable.Range(0, 100).Select(i => 3.0 + 4 * i).TakeWhile(y => y < 58).ToList();
        Assert.Equal(attendus.Count, traits.Count);
        for (int i = 0; i < traits.Count; i++)
        {
            Assert.Equal(traits[i].A.Y, traits[i].B.Y, 6);
            Assert.Equal(attendus[i], traits[i].A.Y, 6);
            Assert.Equal(2, traits[i].A.X, 6);
            Assert.Equal(40, traits[i].B.X, 6);
        }
    }

    // ---- Volets ----

    [Fact]
    public void Volets_horizontaux_six_volets_en_ligne()
    {
        var v = GeometrieCadrans.Volets(new Size(62, 14), Orientation.Horizontal, 6);
        Assert.Equal(6, v.Count);
        for (int i = 0; i < 6; i++)
            EgalRect(new Rect(i * 10.75, 0, 8.25, 14), v[i]);
        Assert.Equal(62, v[5].Right, 6);
    }

    [Fact]
    public void Volets_verticaux_six_volets_empiles()
    {
        var v = GeometrieCadrans.Volets(new Size(16, 104), Orientation.Vertical, 6);
        Assert.Equal(6, v.Count);
        for (int i = 0; i < 6; i++)
            EgalRect(new Rect(0, i * 17.75, 16, 15.25), v[i]);
        Assert.Equal(104, v[5].Bottom, 6);
    }

    [Fact]
    public void Volets_zero_est_traite_comme_un()
    {
        var v = GeometrieCadrans.Volets(new Size(62, 14), Orientation.Horizontal, 0);
        var seul = Assert.Single(v);
        EgalRect(new Rect(0, 0, 62, 14), seul);
    }

    [Fact]
    public void Volets_largeur_insuffisante_donne_une_liste_vide()
    {
        Assert.Empty(GeometrieCadrans.Volets(new Size(10, 14), Orientation.Horizontal, 6));
        Assert.Empty(GeometrieCadrans.Volets(new Size(16, 10), Orientation.Vertical, 6));
    }

    [Theory]
    [InlineData(0.5, 6, 3)]
    [InlineData(0.25, 6, 2)]   // 1,5 arrondi loin de zéro
    [InlineData(1.0, 6, 6)]
    [InlineData(double.NaN, 6, 0)]
    [InlineData(-0.2, 6, 0)]
    public void Volets_allumes_arrondi_loin_de_zero(double fraction, int n, int attendu)
    {
        Assert.Equal(attendu, GeometrieCadrans.VoletsAllumes(fraction, n));
    }
}
