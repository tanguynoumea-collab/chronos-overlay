using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Chronos.Controls;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// CAD-03 (plan 40-03) — preuves PAR RENDU des deux sens des trois contrôles rectangulaires (Fusible, Marée, Volets)
/// et de leurs états « en attente » (HasData=false) et « plancher » (Estimated). Chaque contrôle est mesuré, arrangé
/// puis rendu hors écran (RenderTargetBitmap) ; on lit ensuite des pixels précis calculés depuis GeometrieCadrans.
/// Les couleurs littérales sont permises ICI (tests) : la garde de la phase 39 ne vise que les contrôles et les vues.
/// </summary>
[Collection("XAML WPF")]
public class ControlesCadransOrientationTests
{
    // ------------------------------------------------------------------------------------------------
    // Pinceaux de test
    // ------------------------------------------------------------------------------------------------

    private static readonly Color Rouge = Color.FromRgb(0xFF, 0x00, 0x00);
    private static readonly Color Piste = Color.FromRgb(0x20, 0x20, 0x20);
    private static readonly Color Blanc = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private static readonly Color Attente = Color.FromRgb(0x80, 0x80, 0x80);
    private static readonly Color Eteint = Color.FromRgb(0x30, 0x30, 0x30);

    private static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    // ------------------------------------------------------------------------------------------------
    // Rendu et lecture de pixels
    // ------------------------------------------------------------------------------------------------

    /// <summary>Image rendue (Pbgra32, prémultipliée) : accès pixel par pixel.</summary>
    private sealed class Image
    {
        private readonly byte[] _px;
        public int L { get; }
        public int H { get; }

        public Image(byte[] px, int l, int h) { _px = px; L = l; H = h; }

        /// <summary>(A, R, G, B) du pixel (x, y) — composantes prémultipliées par l'alpha.</summary>
        public (byte A, byte R, byte G, byte B) Couleur(int x, int y)
        {
            int i = (y * L + x) * 4;
            return (_px[i + 3], _px[i + 2], _px[i + 1], _px[i]);
        }

        public IEnumerable<(byte A, byte R, byte G, byte B)> Tous()
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < L; x++)
                    yield return Couleur(x, y);
        }
    }

    /// <summary>Mesure, arrange et rend hors écran le contrôle dans un rectangle w × h.</summary>
    private static Image Rendre(FrameworkElement c, int w, int h)
    {
        c.Width = w;
        c.Height = h;
        c.Measure(new Size(w, h));
        c.Arrange(new Rect(0, 0, w, h));
        c.UpdateLayout();
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(c);
        var px = new byte[w * h * 4];
        bmp.CopyPixels(px, w * 4, 0);
        return new Image(px, w, h);
    }

    private static bool EstRouge((byte A, byte R, byte G, byte B) p) => p.A > 200 && p.R > 200 && p.G < 60 && p.B < 60;
    private static bool EstClair((byte A, byte R, byte G, byte B) p) => p.R > 200 && p.G > 200 && p.B > 200;
    /// <summary>Ligne d'eau (blanc à 85 % sur 1,6 px, anticrénelée sur deux pixels) : ni la lumière rouge (G = 0)
    /// ni la piste (0x20) n'atteignent ce niveau de vert et de bleu.</summary>
    private static bool EstLigneDEau((byte A, byte R, byte G, byte B) p) => p.G > 120 && p.B > 120;
    private static bool EstGrisAttente((byte A, byte R, byte G, byte B) p)
        => p.A == 255 && Math.Abs(p.R - 0x80) <= 3 && Math.Abs(p.G - 0x80) <= 3 && Math.Abs(p.B - 0x80) <= 3;
    private static bool EstEteint((byte A, byte R, byte G, byte B) p)
        => p.A == 255 && Math.Abs(p.R - 0x30) <= 3 && Math.Abs(p.G - 0x30) <= 3 && Math.Abs(p.B - 0x30) <= 3;

    // ------------------------------------------------------------------------------------------------
    // Fabriques (pinceaux posés comme le ferait la vue)
    // ------------------------------------------------------------------------------------------------

    private static FuseBar Fusible(Orientation o, double f, bool hasData = true, bool estime = false) => new()
    {
        Orientation = o, Fraction = f, CordThickness = 10, HasData = hasData, Estimated = estime,
        QuotaBrush = B(Rouge), TrackBrush = B(Piste), NotchBrush = B(Blanc), WaitBrush = B(Attente),
    };

    private static TideColumn Maree(Orientation o, double f, bool hasData = true, bool estime = false) => new()
    {
        Orientation = o, Fraction = f, HasData = hasData, Estimated = estime,
        QuotaBrush = B(Rouge), TrackBrush = B(Piste), WaterlineBrush = B(Blanc), WaitBrush = B(Attente),
    };

    private static FlapRow Volets(Orientation o, double f, bool hasData = true) => new()
    {
        Orientation = o, Fraction = f, Count = 6, HasData = hasData,
        OnBrush = B(Blanc), OffBrush = B(Eteint), WaitBrush = B(Attente),
    };

    /// <summary>Taille de référence (maquette cycle 2) d'un contrôle selon son sens.</summary>
    private static (int W, int H) Taille(string controle, Orientation o) => (controle, o) switch
    {
        ("Fusible", Orientation.Horizontal) => (178, 20),
        ("Fusible", _) => (20, 138),
        ("Maree", Orientation.Horizontal) => (102, 32),
        ("Maree", _) => (42, 118),
        ("Volets", Orientation.Horizontal) => (62, 14),
        _ => (16, 104),
    };

    // ------------------------------------------------------------------------------------------------
    // Défauts
    // ------------------------------------------------------------------------------------------------

    [WpfFact]
    public void Les_orientations_par_defaut_sont_les_sens_historiques()
    {
        Assert.Equal(Orientation.Horizontal, new FuseBar().Orientation);
        Assert.Equal(Orientation.Vertical, new TideColumn().Orientation);
        Assert.Equal(Orientation.Horizontal, new FlapRow().Orientation);
    }

    // ------------------------------------------------------------------------------------------------
    // Fusible
    // ------------------------------------------------------------------------------------------------

    [WpfFact]
    public void Fusible_vertical_le_cordon_reste_en_bas_et_l_etincelle_au_front()
    {
        // 20 × 138, f = 0,25 : front à y = 138 − 34,5 = 103,5 ; cordon de 103,5 à 138 (en bas).
        var img = Rendre(Fusible(Orientation.Vertical, 0.25), 20, 138);

        Assert.True(EstRouge(img.Couleur(10, 130)), $"cordon attendu en bas : {img.Couleur(10, 130)}");
        Assert.False(EstRouge(img.Couleur(10, 40)), $"la part brûlée (haut) ne doit pas être rouge : {img.Couleur(10, 40)}");
        Assert.True(EstClair(img.Couleur(10, 104)), $"étincelle claire attendue au front : {img.Couleur(10, 104)}");
    }

    [WpfFact]
    public void Fusible_horizontal_le_cordon_reste_a_droite()
    {
        // 178 × 20, f = 0,25 : front à x = 133,5 ; cordon à droite.
        var img = Rendre(Fusible(Orientation.Horizontal, 0.25), 178, 20);

        Assert.True(EstRouge(img.Couleur(170, 10)), $"cordon attendu à droite : {img.Couleur(170, 10)}");
        Assert.False(EstRouge(img.Couleur(40, 10)), $"la part brûlée (gauche) ne doit pas être rouge : {img.Couleur(40, 10)}");
        Assert.True(EstClair(img.Couleur(134, 10)), $"étincelle claire attendue au front : {img.Couleur(134, 10)}");
    }

    // ------------------------------------------------------------------------------------------------
    // Marée
    // ------------------------------------------------------------------------------------------------

    [WpfFact]
    public void Maree_horizontale_la_lumiere_part_de_la_gauche()
    {
        // 102 × 32, f = 0,5 : lumière de 0 à 51 ; ligne d'eau ondulée passant par x = 51 à mi-hauteur.
        var img = Rendre(Maree(Orientation.Horizontal, 0.5), 102, 32);

        Assert.True(EstRouge(img.Couleur(20, 16)), $"lumière attendue à gauche : {img.Couleur(20, 16)}");
        Assert.False(EstRouge(img.Couleur(85, 16)), $"l'ombre (droite) ne doit pas être rouge : {img.Couleur(85, 16)}");

        bool ligne = false;
        for (int x = 49; x <= 54; x++) ligne |= EstLigneDEau(img.Couleur(x, 16));
        Assert.True(ligne, "une ligne d'eau claire est attendue autour de x = 51 à mi-hauteur");

        // L'ondulation : la ligne s'écarte de x = 51 au quart de la hauteur (sommet de la première Bézier, côté droit).
        bool ecartee = false;
        for (int x = 52; x <= 54; x++) ecartee |= EstLigneDEau(img.Couleur(x, 8));
        Assert.True(ecartee, "la ligne d'eau horizontale doit onduler (décalée vers la droite au quart de la hauteur)");
    }

    [WpfFact]
    public void Maree_verticale_inchangee_lumiere_par_le_haut()
    {
        var img = Rendre(Maree(Orientation.Vertical, 0.5), 42, 118);

        Assert.True(EstRouge(img.Couleur(21, 20)), $"lumière attendue en haut : {img.Couleur(21, 20)}");
        Assert.False(EstRouge(img.Couleur(21, 100)), $"l'ombre (bas) ne doit pas être rouge : {img.Couleur(21, 100)}");
    }

    // ------------------------------------------------------------------------------------------------
    // Volets
    // ------------------------------------------------------------------------------------------------

    [WpfFact]
    public void Volets_verticaux_allumes_en_haut()
    {
        // 16 × 104, 6 volets : hauteur (104 − 5 × 2,5) / 6 = 15,25 ; écart 15,25..17,75 ; 3 allumés.
        var img = Rendre(Volets(Orientation.Vertical, 0.5), 16, 104);

        Assert.True(EstClair(img.Couleur(8, 7)), $"premier volet (haut) allumé : {img.Couleur(8, 7)}");
        Assert.True(EstEteint(img.Couleur(8, 100)), $"dernier volet (bas) éteint : {img.Couleur(8, 100)}");
        Assert.Equal(0, img.Couleur(8, 16).A);
    }

    [WpfFact]
    public void Volets_horizontaux_ecart_de_2_5()
    {
        // 62 × 14 : largeur (62 − 12,5) / 6 = 8,25 ; écart 8,25..10,75.
        var img = Rendre(Volets(Orientation.Horizontal, 0.5), 62, 14);

        Assert.True(EstClair(img.Couleur(4, 7)), $"premier volet (gauche) allumé : {img.Couleur(4, 7)}");
        Assert.True(EstEteint(img.Couleur(58, 7)), $"dernier volet (droite) éteint : {img.Couleur(58, 7)}");
        Assert.Equal(0, img.Couleur(9, 7).A);
    }

    // ------------------------------------------------------------------------------------------------
    // États « en attente » et « plancher » dans les deux sens
    // ------------------------------------------------------------------------------------------------

    private static FrameworkElement Controle(string controle, Orientation o, bool hasData, bool estime) => controle switch
    {
        "Fusible" => Fusible(o, 0.5, hasData, estime),
        "Maree" => Maree(o, 0.5, hasData, estime),
        _ => Volets(o, 0.5, hasData),
    };

    [WpfTheory]
    [InlineData("Fusible", Orientation.Horizontal)]
    [InlineData("Fusible", Orientation.Vertical)]
    [InlineData("Maree", Orientation.Horizontal)]
    [InlineData("Maree", Orientation.Vertical)]
    [InlineData("Volets", Orientation.Horizontal)]
    [InlineData("Volets", Orientation.Vertical)]
    public void L_attente_ne_laisse_jamais_un_vide(string controle, Orientation o)
    {
        var (w, h) = Taille(controle, o);
        var img = Rendre(Controle(controle, o, hasData: false, estime: false), w, h);

        Assert.Contains(img.Tous(), EstGrisAttente);
        Assert.DoesNotContain(img.Tous(), EstRouge);
    }

    /// <summary>
    /// Le plancher (Estimated) concerne le Fusible et la Marée : les Volets n'ont pas d'état « plancher »
    /// (le chiffre et la plaque autour, portés par la vue, le signalent).
    /// </summary>
    [WpfTheory]
    [InlineData("Fusible", Orientation.Horizontal)]
    [InlineData("Fusible", Orientation.Vertical)]
    [InlineData("Maree", Orientation.Horizontal)]
    [InlineData("Maree", Orientation.Vertical)]
    public void Le_plancher_reste_visible_dans_les_deux_sens(string controle, Orientation o)
    {
        var (w, h) = Taille(controle, o);
        // Pixel dans la zone quota à f = 0,5, hors du trait de grain central.
        (int X, int Y) p = (controle, o) switch
        {
            ("Fusible", Orientation.Horizontal) => (150, 13),   // cordon 89..178 × 5..15
            ("Fusible", _) => (13, 115),                        // cordon 5..15 × 69..138
            ("Maree", Orientation.Horizontal) => (25, 16),      // lumière 0..51
            _ => (21, 30),                                      // lumière 0..59
        };

        var plancher = Rendre(Controle(controle, o, hasData: true, estime: true), w, h).Couleur(p.X, p.Y);
        Assert.True(plancher.A > 0, $"le plancher doit rester visible en ({p.X}, {p.Y}) : {plancher}");

        if (controle == "Fusible")
        {
            var exact = Rendre(Controle(controle, o, hasData: true, estime: false), w, h).Couleur(p.X, p.Y);
            Assert.True(EstRouge(exact), $"cordon exact rouge attendu : {exact}");
            Assert.True(plancher.A < exact.A || plancher.R < exact.R,
                $"le cordon plancher doit être atténué : plancher {plancher} vs exact {exact}");
        }
    }
}
