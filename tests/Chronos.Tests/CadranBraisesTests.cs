using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Chronos.Controls;
using Chronos.Rendering;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// BRA-01 (plan 41-01) — preuves PAR RENDU de l'anneau 5 h de Braises : 20 braises en 5 groupes de 4 (rien à midi ni à
/// 72°, là où une répartition uniforme en mettrait une), état « en attente » aux MÊMES angles que le nominal, anneau
/// hebdo inchangé. Les couleurs littérales sont permises ICI (tests) : la garde de la phase 39 ne vise que les sources.
/// </summary>
[Collection("XAML WPF")]
public class CadranBraisesTests
{
    private static readonly Color Rouge = Color.FromRgb(0xFF, 0x00, 0x00);
    private static readonly Color Cendre = Color.FromRgb(0x30, 0x30, 0x30);
    private static readonly Color Attente = Color.FromRgb(0x80, 0x80, 0x80);

    private static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    private const int Cote = 170;

    /// <summary>Mesure, arrange et rend hors écran l'élément (170 × 170, 96 DPI) ; renvoie les pixels Pbgra32.</summary>
    private static byte[] Rendre(FrameworkElement c)
    {
        c.Width = Cote;
        c.Height = Cote;
        c.Measure(new Size(Cote, Cote));
        c.Arrange(new Rect(0, 0, Cote, Cote));
        c.UpdateLayout();
        var bmp = new RenderTargetBitmap(Cote, Cote, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(c);
        var px = new byte[Cote * Cote * 4];
        bmp.CopyPixels(px, Cote * 4, 0);
        return px;
    }

    /// <summary>(A, R, G, B) du pixel (x, y) — composantes prémultipliées.</summary>
    private static (byte A, byte R, byte G, byte B) Pixel(byte[] px, (int X, int Y) p)
    {
        int i = (p.Y * Cote + p.X) * 4;
        return (px[i + 3], px[i + 2], px[i + 1], px[i]);
    }

    /// <summary>Pixel qui contient le point de l'anneau à <paramref name="angleDeg"/> (0 = midi, horaire).</summary>
    private static (int X, int Y) PointSur(double angleDeg, double rayon)
    {
        double a = angleDeg * Math.PI / 180.0;
        return ((int)Math.Floor(85 + rayon * Math.Sin(a)), (int)Math.Floor(85 - rayon * Math.Cos(a)));
    }

    private static bool Proche((byte A, byte R, byte G, byte B) p, Color c)
        => p.A == 255 && Math.Abs(p.R - c.R) <= 3 && Math.Abs(p.G - c.G) <= 3 && Math.Abs(p.B - c.B) <= 3;

    private static double AngleCinqHeures(int i) => BraisesGeometrie.Angle(i, 20, 4, 13.2);

    private static EmberRingControl AnneauCinqHeures(double fraction, bool hasData = true) => new()
    {
        Radius = 66, Count = 20, PipRadius = 4, GroupSize = 4, GroupPitch = 13.2,
        Fraction = fraction, HasData = hasData,
        QuotaBrush = B(Rouge), AshBrush = B(Cendre), WaitBrush = B(Attente),
    };

    [WpfFact]
    public void Plein_la_premiere_braise_est_a_16_2_degres_et_rien_a_midi_ni_a_72()
    {
        var px = Rendre(AnneauCinqHeures(1.0));

        Assert.True(Proche(Pixel(px, PointSur(AngleCinqHeures(0), 66)), Rouge), "braise 0 (16,2°) non rouge");
        Assert.Equal(0, Pixel(px, PointSur(0, 66)).A);    // midi : milieu d'un vide
        Assert.Equal(0, Pixel(px, PointSur(72, 66)).A);   // 72° : milieu du vide entre les groupes 1 et 2
    }

    [WpfFact]
    public void A_moitie_dix_braises_allumees_puis_cendres()
    {
        var px = Rendre(AnneauCinqHeures(0.5));

        Assert.True(Proche(Pixel(px, PointSur(AngleCinqHeures(8), 66)), Rouge), "braise 8 non allumée");
        Assert.True(Proche(Pixel(px, PointSur(AngleCinqHeures(12), 66)), Cendre), "braise 12 non cendre");
        Assert.Equal(0, Pixel(px, PointSur(72, 66)).A);
    }

    [WpfFact]
    public void En_attente_les_braises_neutres_sont_aux_memes_angles_que_le_nominal()
    {
        var px = Rendre(AnneauCinqHeures(1.0, hasData: false));

        Assert.True(Proche(Pixel(px, PointSur(AngleCinqHeures(0), 66)), Attente), "braise d'attente absente à 16,2°");
        Assert.Equal(0, Pixel(px, PointSur(0, 66)).A);
        Assert.Equal(0, Pixel(px, PointSur(72, 66)).A);
    }

    [WpfFact]
    public void Anneau_hebdo_sans_groupe_reste_uniforme()
    {
        var hebdo = new EmberRingControl
        {
            Radius = 44, Count = 12, PipRadius = 3.6, Fraction = 1.0,
            QuotaBrush = B(Rouge), AshBrush = B(Cendre), WaitBrush = B(Attente),
        };
        Assert.Equal(1, hebdo.GroupSize);
        Assert.Equal(0.0, hebdo.GroupPitch);

        var px = Rendre(hebdo);
        Assert.True(Pixel(px, PointSur(30, 44)).A > 0, "braise hebdo absente à 30°");
    }
}
