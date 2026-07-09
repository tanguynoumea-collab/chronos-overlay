using Chronos.Models;
using Chronos.ViewModels;
using Xunit;

namespace Chronos.Tests;

/// <summary>Prouve INT-02 : une fenêtre EXACT (issue de l'OAuth) éteint le badge « estimée »
/// (IsEstimated == false) et porte l'utilisation réelle (couleur de rampe) ; une fenêtre ESTIMÉE
/// rallume le badge. L'honnêteté joue dans les deux sens. Tests PURS ([Fact]).</summary>
public class WindowGaugeViewModelTests
{
    [Fact]
    public void Fenetre_exacte_masque_le_badge_estimee_et_porte_utilisation_reelle()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = 0.74,
            ResetsAt = DateTimeOffset.UtcNow + TimeSpan.FromHours(2),
        });

        Assert.False(vm.IsEstimated);              // badge « estimée » masqué (INT-02)
        Assert.Equal(0.74, vm.Utilization);        // arc en vraie couleur (utilization exacte)
        Assert.False(vm.HasTokens);                // pas de surfaçage tokens estimés en Exact
    }

    [Fact]
    public void Fenetre_estimee_rallume_le_badge()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromDays(7));
        vm.Apply(new WindowState { Kind = WindowKind.SevenDay, Reliability = SourceReliability.Estimated });
        Assert.True(vm.IsEstimated);               // honnêteté dans l'autre sens
    }
}
