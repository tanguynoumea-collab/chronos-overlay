using Chronos.Placement;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la logique PURE d'accroche aux coins (FEN-03) : NearestCorner sur les 4 quadrants,
/// ClassifyCorner pour les 4 coins, CornerToTopLeft pour les 4 coins imposés — marge respectée
/// exactement. Tests PURS (aucun type WPF, aucun STA) : la géométrie ne dépend d'aucun écran réel.
/// </summary>
public class CornerSnapTests
{
    // Zone de travail de référence : 1000x800 à l'origine (0,0). Centre = (500, 400).
    private static readonly RectD Work = new(0, 0, 1000, 800);
    private const double Window = 200; // fenêtre carrée 200x200
    private const double Margin = 12;

    private static RectD Win(double x, double y) => new(x, y, Window, Window);

    // --- NearestCorner : coin le plus proche + marge exacte, sur les 4 quadrants ---

    [Fact]
    public void NearestCorner_haut_gauche()
    {
        var (x, y) = CornerSnap.NearestCorner(Win(50, 40), Work, Margin);
        Assert.Equal(Work.X + Margin, x, 9);
        Assert.Equal(Work.Y + Margin, y, 9);
    }

    [Fact]
    public void NearestCorner_haut_droite()
    {
        var (x, y) = CornerSnap.NearestCorner(Win(700, 40), Work, Margin);
        Assert.Equal(Work.Right - Window - Margin, x, 9);
        Assert.Equal(Work.Y + Margin, y, 9);
    }

    [Fact]
    public void NearestCorner_bas_gauche()
    {
        var (x, y) = CornerSnap.NearestCorner(Win(50, 600), Work, Margin);
        Assert.Equal(Work.X + Margin, x, 9);
        Assert.Equal(Work.Bottom - Window - Margin, y, 9);
    }

    [Fact]
    public void NearestCorner_bas_droite()
    {
        var (x, y) = CornerSnap.NearestCorner(Win(700, 600), Work, Margin);
        Assert.Equal(Work.Right - Window - Margin, x, 9);
        Assert.Equal(Work.Bottom - Window - Margin, y, 9);
    }

    // --- ClassifyCorner : quadrant du centre de la fenêtre ---

    [Theory]
    [InlineData(50, 40, OverlayCorner.TopLeft)]
    [InlineData(700, 40, OverlayCorner.TopRight)]
    [InlineData(50, 600, OverlayCorner.BottomLeft)]
    [InlineData(700, 600, OverlayCorner.BottomRight)]
    public void ClassifyCorner_renvoie_le_quadrant_du_centre(double x, double y, OverlayCorner attendu)
    {
        Assert.Equal(attendu, CornerSnap.ClassifyCorner(Win(x, y), Work));
    }

    // --- CornerToTopLeft : coin IMPOSÉ (restauration du coin persisté) ---

    [Fact]
    public void CornerToTopLeft_top_left()
    {
        var (x, y) = CornerSnap.CornerToTopLeft(OverlayCorner.TopLeft, Win(0, 0), Work, Margin);
        Assert.Equal(Work.X + Margin, x, 9);
        Assert.Equal(Work.Y + Margin, y, 9);
    }

    [Fact]
    public void CornerToTopLeft_top_right()
    {
        var (x, y) = CornerSnap.CornerToTopLeft(OverlayCorner.TopRight, Win(0, 0), Work, Margin);
        Assert.Equal(Work.Right - Window - Margin, x, 9);
        Assert.Equal(Work.Y + Margin, y, 9);
    }

    [Fact]
    public void CornerToTopLeft_bottom_left()
    {
        var (x, y) = CornerSnap.CornerToTopLeft(OverlayCorner.BottomLeft, Win(0, 0), Work, Margin);
        Assert.Equal(Work.X + Margin, x, 9);
        Assert.Equal(Work.Bottom - Window - Margin, y, 9);
    }

    [Fact]
    public void CornerToTopLeft_bottom_right()
    {
        var (x, y) = CornerSnap.CornerToTopLeft(OverlayCorner.BottomRight, Win(0, 0), Work, Margin);
        Assert.Equal(Work.Right - Window - Margin, x, 9);
        Assert.Equal(Work.Bottom - Window - Margin, y, 9);
    }

    // --- Garantie : la fenêtre reste dans la zone de travail (marge des deux côtés) ---

    [Fact]
    public void CornerToTopLeft_respecte_la_marge_sur_bord_oppose()
    {
        var (x, y) = CornerSnap.CornerToTopLeft(OverlayCorner.BottomRight, Win(0, 0), Work, Margin);
        // Bord droit/bas de la fenêtre = bord de la zone - marge.
        Assert.Equal(Work.Right - Margin, x + Window, 9);
        Assert.Equal(Work.Bottom - Margin, y + Window, 9);
    }

    // --- RecalerSurCoin : recalage après changement d'empreinte (CAD-02) ---

    // Zones de travail : principal, secondaire à gauche (origine négative), secondaire décalé en haut.
    private static readonly RectD[] Zones =
    {
        new(0, 0, 1920, 1040),
        new(-1920, 0, 1920, 1040),
        new(1920, -200, 2560, 1400),
    };

    // Les 7 empreintes de cadran à l'échelle 1 (plan 40-01).
    private static readonly (double W, double H)[] Empreintes =
    {
        (170, 170), (190, 92), (110, 190), (132, 160), (190, 96), (190, 66), (128, 190),
    };

    [Theory]
    [InlineData(OverlayCorner.TopLeft, 1.0)]
    [InlineData(OverlayCorner.TopLeft, 1.5)]
    [InlineData(OverlayCorner.TopRight, 1.0)]
    [InlineData(OverlayCorner.TopRight, 1.5)]
    [InlineData(OverlayCorner.BottomLeft, 1.0)]
    [InlineData(OverlayCorner.BottomLeft, 1.5)]
    [InlineData(OverlayCorner.BottomRight, 1.0)]
    [InlineData(OverlayCorner.BottomRight, 1.5)]
    public void Le_recalage_garde_le_coin_et_reste_dans_la_zone(OverlayCorner coin, double echelle)
    {
        double marge = 12 * echelle;
        bool gauche = coin is OverlayCorner.TopLeft or OverlayCorner.BottomLeft;
        bool haut = coin is OverlayCorner.TopLeft or OverlayCorner.TopRight;
        foreach (var travail in Zones)
        {
            foreach (var (w, h) in Empreintes)
            {
                var fen = new RectD(0, 0, w * echelle, h * echelle);
                var (x, y) = CornerSnap.RecalerSurCoin(coin, fen, travail, marge);

                Assert.Equal(coin, CornerSnap.ClassifyCorner(fen with { X = x, Y = y }, travail));
                Assert.InRange(x, travail.X, travail.Right - fen.Width);
                Assert.InRange(y, travail.Y, travail.Bottom - fen.Height);

                // Marge respectée sur les deux bords du coin imposé.
                if (gauche) Assert.Equal(travail.X + marge, x, 9);
                else Assert.Equal(travail.Right - marge, x + fen.Width, 9);
                if (haut) Assert.Equal(travail.Y + marge, y, 9);
                else Assert.Equal(travail.Bottom - marge, y + fen.Height, 9);
            }
        }
    }

    [Fact]
    public void Le_recalage_ignore_la_position_courante()
    {
        var travail = new RectD(-1920, 0, 1920, 1040);
        var a = CornerSnap.RecalerSurCoin(OverlayCorner.BottomRight, new RectD(0, 0, 190, 96), travail, 12);
        var b = CornerSnap.RecalerSurCoin(OverlayCorner.BottomRight, new RectD(5000, 0, 190, 96), travail, 12);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Une_fenetre_plus_grande_que_la_zone_est_bornee_en_haut_a_gauche()
    {
        var travail = new RectD(0, 0, 200, 200);
        var (x, y) = CornerSnap.RecalerSurCoin(OverlayCorner.BottomRight, new RectD(0, 0, 300, 300), travail, 12);
        Assert.Equal(0, x, 9);
        Assert.Equal(0, y, 9);
    }
}
