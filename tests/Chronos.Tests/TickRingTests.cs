using System.Reflection;
using System.Windows.Media;
using Chronos.Controls;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve l'extension TickRing.Angles (JOUR-02) : la DP Angles permet de dessiner UN trait par angle
/// arbitraire (les resets 5 h projetés), tout en conservant intact le comportement régulier « Count »
/// (non-régression décorative des ticks 60/12). [WpfFact] (thread STA) : on construit des Geometry WPF.
/// La géométrie de définition (protégée) est lue par réflexion — TickRing est sealed.
/// </summary>
public class TickRingTests
{
    // Lit la DefiningGeometry protégée de la Shape (comptage des segments indépendant du layout).
    private static GeometryGroup DefiningOf(TickRing ring)
    {
        var prop = typeof(TickRing).GetProperty("DefiningGeometry",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (GeometryGroup)prop.GetValue(ring)!;
    }

    [WpfFact]
    public void TickRing_Angles_dessine_un_trait_par_angle()
    {
        var ring = new TickRing
        {
            Width = 100, Height = 100, Radius = 40, TickLength = 6,
            Angles = new[] { 45.0, 120.0, 195.0, 270.0, 345.0 },
        };
        var group = DefiningOf(ring);
        Assert.Equal(5, group.Children.Count);                    // un LineGeometry par angle
        Assert.All(group.Children, g => Assert.IsType<LineGeometry>(g));
    }

    [WpfFact]
    public void TickRing_Angles_vide_ou_null_ne_dessine_rien()
    {
        var ring = new TickRing
        {
            Width = 100, Height = 100, Radius = 40, TickLength = 6,
            Angles = System.Array.Empty<double>(),
        };
        Assert.Empty(DefiningOf(ring).Children);                  // liste vide → aucun trait
    }

    [WpfFact]
    public void TickRing_sans_Angles_garde_comportement_regulier()
    {
        var ring = new TickRing
        {
            Width = 100, Height = 100, Radius = 40, TickLength = 6,
            Count = 12, // Angles laissé null (défaut)
        };
        Assert.Equal(12, DefiningOf(ring).Children.Count);        // non-régression : boucle Count intacte
    }
}
