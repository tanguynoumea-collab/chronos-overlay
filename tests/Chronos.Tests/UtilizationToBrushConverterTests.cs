using System.Globalization;
using System.Windows.Media;
using Chronos.Converters;
using Chronos.Rendering;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve les 3 branches sémantiques de <see cref="UtilizationToBrushConverter"/> (CAD-04/05)
/// + l'honnêteté sur donnée absente (Pitfall 5 : jamais de couleur inventée sur null).
/// [WpfFact] (thread STA) : SolidColorBrush est un DispatcherObject.
/// </summary>
public class UtilizationToBrushConverterTests
{
    private static readonly Color Vert    = Color.FromRgb(0x7B, 0xB1, 0x3C); // borne rampe 0
    private static readonly Color Epuise  = Color.FromRgb(0x5A, 0x59, 0x60); // gris épuisé (CAD-05)
    private static readonly Color Neutre  = Color.FromRgb(0x2A, 0x29, 0x32); // piste douce (donnée absente)

    private static SolidColorBrush Convert(object? value)
    {
        var sut = new UtilizationToBrushConverter();
        var result = sut.Convert(value, typeof(Brush), null, CultureInfo.InvariantCulture);
        return Assert.IsType<SolidColorBrush>(result);
    }

    // ---- Branche rampe [0,1[ (CAD-04) ----

    [WpfFact]
    public void Convert_valeur_intermediaire_rend_la_rampe()
    {
        // 0.3 ∈ [0,1[ → exactement la couleur de la rampe pure.
        var brush = Convert(0.3);
        Assert.Equal(RampColor.Interpolate(0.3), brush.Color);
    }

    [WpfFact]
    public void Convert_zero_est_le_vert_borne_rampe()
    {
        var brush = Convert(0.0);
        Assert.Equal(Vert, brush.Color);
    }

    // ---- Branche épuisé ≥ 1 (CAD-05) ----

    [WpfFact]
    public void Convert_un_est_gris_epuise_pas_rouge()
    {
        // 1.0 → gris épuisé, PAS la couleur rouge de la rampe.
        var brush = Convert(1.0);
        Assert.Equal(Epuise, brush.Color);
        Assert.NotEqual(RampColor.Interpolate(1.0), brush.Color);
    }

    [WpfFact]
    public void Convert_au_dessus_de_un_reste_gris_epuise()
    {
        var brush = Convert(1.4);
        Assert.Equal(Epuise, brush.Color);
    }

    // ---- Branche neutre : donnée absente (honnêteté, Pitfall 5) ----

    [WpfFact]
    public void Convert_null_est_neutre_aucune_couleur_inventee()
    {
        var brush = Convert(null);
        Assert.Equal(Neutre, brush.Color);
    }

    [WpfFact]
    public void Convert_non_double_est_neutre_sans_exception()
    {
        var brush = Convert("abc");
        Assert.Equal(Neutre, brush.Color);
    }

    // ---- Brushes statiques gelés (partageables, légers) ----

    [WpfFact]
    public void Convert_couleurs_statiques_sont_gelees()
    {
        Assert.True(Convert(null).IsFrozen, "le brush neutre doit être gelé");
        Assert.True(Convert(1.0).IsFrozen,  "le brush épuisé doit être gelé");
    }
}
