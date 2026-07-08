using System.Windows.Media;
using Chronos.Rendering;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve les 3 stops exacts et l'interpolation continue de <see cref="RampColor"/> (CAD-04).
/// Color est un struct → [Fact] suffit (aucun thread WPF requis).
/// Stops verrouillés : 0.00 → #7BB13C, 0.55 → #EFA23A, 1.00 → #D8503A.
/// </summary>
public class RampColorTests
{
    private static readonly Color Green = Color.FromRgb(0x7B, 0xB1, 0x3C);
    private static readonly Color Amber = Color.FromRgb(0xEF, 0xA2, 0x3A);
    private static readonly Color Red   = Color.FromRgb(0xD8, 0x50, 0x3A);

    // ---- 3 stops exacts ----

    [Fact]
    public void Interpolate_0_est_vert_exact()
    {
        Assert.Equal(Green, RampColor.Interpolate(0.0));
    }

    [Fact]
    public void Interpolate_stop_median_est_ambre_exact()
    {
        Assert.Equal(Amber, RampColor.Interpolate(0.55));
    }

    [Fact]
    public void Interpolate_1_est_rouge_exact()
    {
        Assert.Equal(Red, RampColor.Interpolate(1.0));
    }

    // ---- Clamps hors [0, 1] ----

    [Fact]
    public void Interpolate_sous_zero_clampe_au_vert()
    {
        Assert.Equal(Green, RampColor.Interpolate(-0.2));
    }

    [Fact]
    public void Interpolate_au_dessus_de_un_clampe_au_rouge()
    {
        Assert.Equal(Red, RampColor.Interpolate(1.4));
    }

    // ---- Interpolation continue : mi-segment vert→ambre ----

    [Fact]
    public void Interpolate_mi_segment_vert_ambre_est_strictement_entre_les_bornes()
    {
        // 0.275 = mi-chemin entre 0 (vert) et 0.55 (ambre).
        var c = RampColor.Interpolate(0.275);
        Assert.True(c.R > Green.R && c.R < Amber.R,
            $"R={c.R} doit être strictement entre 0x7B={Green.R} et 0xEF={Amber.R}");
        Assert.NotEqual(Green, c);
        Assert.NotEqual(Amber, c);
    }
}
