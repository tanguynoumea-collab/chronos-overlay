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
}
