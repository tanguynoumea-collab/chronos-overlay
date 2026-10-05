using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Chronos.Controls;
using Chronos.Rendering;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Plan 43-06 — écart du constat du 2026-10-04, décision utilisateur « Tous les cadrans » : une seule règle,
/// la partie remplie / allumée = temps CONSOMMÉ de la fenêtre (5 h et hebdo, 8 variantes), pleine au reset.
/// Le remplissage progresse dans le sens de lecture : gauche → droite, bas → haut, horaire depuis midi.
/// Garde de vue (aucune liaison Fraction sur FractionRemaining) + preuves par rendu, une par famille de contrôle.
/// Les couleurs littérales sont permises ICI (tests).
/// </summary>
[Collection("XAML WPF")]
public class RempliConsommeTests
{
    // ------------------------------------------------------------------------------------------------
    // Garde de vue
    // ------------------------------------------------------------------------------------------------

    private static readonly Regex LiaisonFraction =
        new("Fraction=\"\\{Binding (FiveHour|SevenDay)\\.(\\w+)\\}\"", RegexOptions.Compiled);

    [Theory]
    [InlineData("CadranArcsView.xaml", 2)]
    [InlineData("CadranBraisesView.xaml", 1)]   // plan 43-09 : l'anneau extérieur est la journée (voir plus bas), seul l'hebdo lie Fraction
    [InlineData("CadranFusibleView.xaml", 4)]
    [InlineData("CadranMareeView.xaml", 4)]
    [InlineData("CadranVoletsView.xaml", 4)]
    public void Chaque_liaison_Fraction_des_cadrans_est_le_temps_consomme(string vue, int attendues)
    {
        var fichier = Path.Combine(GardesPerimetreTests.CheminSources(), "Views", "Cadrans", vue);
        Assert.True(File.Exists(fichier), $"Fichier introuvable : {fichier}");
        var liaisons = LiaisonFraction.Matches(File.ReadAllText(fichier)).Select(m => m.Value).ToList();

        // Une garde qui ne trouverait aucune liaison serait muette : on exige le nombre exact (5 h + hebdo, par gabarit).
        Assert.Equal(attendues, liaisons.Count);
        Assert.All(liaisons, l => Assert.EndsWith(".FractionElapsed}\"", l));
    }

    [Fact]
    public void Braises_l_anneau_journee_est_un_cas_documente_rempli_egale_heures_passees()
    {
        // Plan 43-09 (constat du 2026-10-05) : l'anneau extérieur de Braises n'est plus la fenêtre 5 h mais la JOURNÉE locale.
        // Sa partie allumée = les heures PASSÉES du jour (angles et états calculés par BraisesJournee, liés au VM), jamais
        // FiveHour.FractionElapsed ni FractionRemaining. La règle « rempli = consommé » reste vraie pour l'hebdo.
        var xaml = File.ReadAllText(Path.Combine(GardesPerimetreTests.CheminSources(), "Views", "Cadrans", "CadranBraisesView.xaml"));
        Assert.Contains("Angles=\"{Binding JourneeAngles}\"", xaml);
        Assert.Contains("Etats=\"{Binding JourneeEtats}\"", xaml);
        Assert.DoesNotContain("FiveHour.FractionElapsed", xaml);
        Assert.Contains("Fraction=\"{Binding SevenDay.FractionElapsed}\"", xaml);
    }

    [Fact]
    public void Aucune_vue_de_cadran_ne_lie_FractionRemaining()
    {
        var dossier = Path.Combine(GardesPerimetreTests.CheminSources(), "Views", "Cadrans");
        var vues = Directory.GetFiles(dossier, "*.xaml");
        Assert.NotEmpty(vues);
        foreach (var vue in vues)
            Assert.DoesNotContain("FractionRemaining", File.ReadAllText(vue));
    }

    // ------------------------------------------------------------------------------------------------
    // Géométrie : sens de remplissage
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void Fusible_horizontal_le_cordon_part_de_la_gauche()
    {
        // 25 % consommé : cordon 0..44,5, front à 44,5 ; le sillon creux à droite = temps restant.
        var g = GeometrieCadrans.Fusible(new Size(178, 20), Orientation.Horizontal, 0.25, 10);
        Assert.Equal(0, g.Cordon.X, 6);
        Assert.Equal(44.5, g.Cordon.Width, 6);
        Assert.Equal(44.5, g.Front.X, 6);
    }

    [Fact]
    public void Fusible_vertical_le_cordon_part_du_bas()
    {
        var g = GeometrieCadrans.Fusible(new Size(20, 138), Orientation.Vertical, 0.25, 10);
        Assert.Equal(138, g.Cordon.Bottom, 6);
        Assert.Equal(34.5, g.Cordon.Height, 6);
        Assert.Equal(103.5, g.Front.Y, 6);
    }

    [Fact]
    public void Maree_verticale_la_lumiere_monte_du_bas()
    {
        var g = GeometrieCadrans.Maree(new Size(42, 118), Orientation.Vertical, 0.25);
        Assert.Equal(118, g.Lumiere.Bottom, 6);
        Assert.Equal(29.5, g.Lumiere.Height, 6);
        Assert.Equal(88.5, g.PositionLigne, 6);
    }

    [Fact]
    public void Maree_horizontale_la_lumiere_part_de_la_gauche()
    {
        var g = GeometrieCadrans.Maree(new Size(102, 32), Orientation.Horizontal, 0.25);
        Assert.Equal(0, g.Lumiere.X, 6);
        Assert.Equal(25.5, g.Lumiere.Width, 6);
    }

    [Theory]
    [InlineData(Orientation.Horizontal, new[] { true, true, false, false, false, false })]
    [InlineData(Orientation.Vertical, new[] { false, false, false, false, true, true })]
    public void Volets_allumes_depuis_la_gauche_ou_depuis_le_bas(Orientation axe, bool[] attendus)
    {
        for (int i = 0; i < 6; i++)
            Assert.Equal(attendus[i], GeometrieCadrans.VoletAllume(i, 6, 2, axe));
    }

    // ------------------------------------------------------------------------------------------------
    // Rendu : 25 % consommé remplit 25 %, pas 75 %
    // ------------------------------------------------------------------------------------------------

    private static readonly Color Rouge = Color.FromRgb(0xFF, 0x00, 0x00);
    private static readonly Color Piste = Color.FromRgb(0x20, 0x20, 0x20);
    private static readonly Color Blanc = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private static readonly Color Eteint = Color.FromRgb(0x30, 0x30, 0x30);

    private static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    private static byte[] Rendre(FrameworkElement c, int w, int h)
    {
        c.Width = w; c.Height = h;
        c.Measure(new Size(w, h));
        c.Arrange(new Rect(0, 0, w, h));
        c.UpdateLayout();
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(c);
        var px = new byte[w * h * 4];
        bmp.CopyPixels(px, w * 4, 0);
        return px;
    }

    private static (byte A, byte R, byte G, byte B) Px(byte[] px, int w, int x, int y)
    {
        int i = (y * w + x) * 4;
        return (px[i + 3], px[i + 2], px[i + 1], px[i]);
    }

    private static bool EstRouge((byte A, byte R, byte G, byte B) p) => p.A > 200 && p.R > 200 && p.G < 60 && p.B < 60;
    private static bool EstClair((byte A, byte R, byte G, byte B) p) => p.R > 200 && p.G > 200 && p.B > 200;

    [WpfFact]
    public void Fusible_horizontal_25_pour_cent_consomme_remplit_la_gauche()
    {
        var f = new FuseBar
        {
            Orientation = Orientation.Horizontal, Fraction = 0.25, CordThickness = 10,
            QuotaBrush = B(Rouge), TrackBrush = B(Piste), NotchBrush = B(Blanc),
        };
        var px = Rendre(f, 178, 20);
        Assert.True(EstRouge(Px(px, 178, 20, 10)), "le temps consommé (gauche) doit être rempli");
        Assert.False(EstRouge(Px(px, 178, 150, 10)), "le temps restant (droite) doit rester un sillon creux");
    }

    [WpfFact]
    public void Maree_verticale_25_pour_cent_consomme_remplit_le_bas()
    {
        var m = new TideColumn
        {
            Orientation = Orientation.Vertical, Fraction = 0.25,
            QuotaBrush = B(Rouge), TrackBrush = B(Piste), WaterlineBrush = B(Blanc),
        };
        var px = Rendre(m, 42, 118);
        Assert.True(EstRouge(Px(px, 42, 21, 110)), "le temps consommé (bas) doit être éclairé");
        Assert.False(EstRouge(Px(px, 42, 21, 20)), "le temps restant (haut) doit rester dans l'ombre");
    }

    [WpfFact]
    public void Volets_verticaux_un_tiers_consomme_allume_les_deux_du_bas()
    {
        var v = new FlapRow
        {
            Orientation = Orientation.Vertical, Fraction = 1.0 / 3, Count = 6,
            OnBrush = B(Blanc), OffBrush = B(Eteint),
        };
        var px = Rendre(v, 16, 104);
        Assert.True(EstClair(Px(px, 16, 8, 100)), "volet du bas allumé");
        Assert.True(EstClair(Px(px, 16, 8, 80)), "avant-dernier volet allumé");
        Assert.False(EstClair(Px(px, 16, 8, 7)), "volet du haut éteint");
    }

    [WpfFact]
    public void Braises_hebdo_2_jours_consommes_allume_le_premier_groupe_depuis_midi()
    {
        // Anneau hebdo (14 braises, 7 groupes de 2) : 2/7 consommé → 4 braises, de midi vers la droite (sens horaire).
        // Plan 43-09 : l'anneau extérieur (journée) n'obéit plus à Fraction — voir le cas documenté plus haut.
        var r = new EmberRingControl
        {
            Radius = 44, Count = 14, PipRadius = 3.6, GroupSize = 2, GroupPitch = 15.6, Fraction = 2.0 / 7,
            QuotaBrush = B(Rouge), AshBrush = B(Eteint),
        };
        var px = Rendre(r, 170, 170);
        (int X, int Y) Pos(int i)
        {
            double a = BraisesGeometrie.Angle(i, 14, 2, 15.6) * System.Math.PI / 180.0;
            return ((int)System.Math.Round(85 + 44 * System.Math.Sin(a)), (int)System.Math.Round(85 - 44 * System.Math.Cos(a)));
        }
        var p0 = Pos(0); var p13 = Pos(13);
        Assert.True(EstRouge(Px(px, 170, p0.X, p0.Y)), "la première braise (juste après midi) est allumée");
        Assert.False(EstRouge(Px(px, 170, p13.X, p13.Y)), "la dernière braise (juste avant midi) reste en cendre");
    }
}
