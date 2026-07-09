using Chronos.Models;
using Chronos.Text;
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

    // --- VIS-05 : PercentFormatter pur (honnêteté : null → rien, « ~ » si estimé, arrondi entier) ---

    [Fact]
    public void UtilizationText_null_rend_vide()
    {
        Assert.Equal("", PercentFormatter.Format(null, false));
        Assert.Equal("", PercentFormatter.Format(null, true));   // null → rien, MÊME estimé
    }

    [Fact]
    public void UtilizationText_exact_rend_80pourcent()
    {
        Assert.Equal("80 %", PercentFormatter.Format(0.80, false)); // espace normal avant %
    }

    [Fact]
    public void UtilizationText_estime_prefixe_tilde()
    {
        Assert.Equal("~80 %", PercentFormatter.Format(0.80, true)); // « ~ » = estimation, pas exact
    }

    [Fact]
    public void UtilizationText_arrondi_entier()
    {
        Assert.Equal("80 %", PercentFormatter.Format(0.804, false)); // arrondi vers le bas
        Assert.Equal("81 %", PercentFormatter.Format(0.806, false)); // arrondi vers le haut
    }

    [Fact]
    public void UtilizationText_plein_100()
    {
        Assert.Equal("100 %", PercentFormatter.Format(1.0, false));
    }

    // --- VIS-01 : FractionElapsed = clamp(1 − FractionRemaining) recalculée à chaque Interpolate ---

    [Fact]
    public void Elapsed_inverse_le_remplissage()
    {
        var now = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        // reste = 1.25 h sur 5 h → FractionRemaining = 0.25 → FractionElapsed = 0.75
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            ResetsAt = now + TimeSpan.FromHours(1.25),
        });
        vm.Interpolate(now);

        Assert.Equal(0.25, vm.FractionRemaining, 9);
        Assert.Equal(0.75, vm.FractionElapsed, 9);
    }

    [Fact]
    public void Elapsed_clampe_0_1()
    {
        var now = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));

        // reset déjà passé → FractionRemaining = 0 → FractionElapsed = 1 (jamais > 1)
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            ResetsAt = now - TimeSpan.FromHours(1),
        });
        vm.Interpolate(now);
        Assert.InRange(vm.FractionElapsed, 0.0, 1.0);
        Assert.Equal(1.0, vm.FractionElapsed, 9);

        // reset très loin → FractionRemaining = 1 → FractionElapsed = 0 (jamais < 0)
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            ResetsAt = now + TimeSpan.FromHours(50),
        });
        vm.Interpolate(now);
        Assert.InRange(vm.FractionElapsed, 0.0, 1.0);
        Assert.Equal(0.0, vm.FractionElapsed, 9);
    }

    // --- VIS-01 (correctif) : reset INCONNU → arc VIDE (0), pas plein — sinon un « — » trompeur affiche un plein ---
    [Fact]
    public void Elapsed_reset_inconnu_est_vide_pas_plein()
    {
        var now = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));

        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Estimated,
            ResetsAt = null, // reset inconnu (repli JSONL sans inférence) → countdown « — »
        });
        vm.Interpolate(now);

        Assert.Equal("—", vm.CountdownText);
        Assert.Equal(0.0, vm.FractionElapsed, 9); // arc VIDE, jamais plein
    }

    // --- VIS-05 : UtilizationText + HasUtilizationText posés par Apply ---

    [Fact]
    public void UtilizationText_pose_par_Apply_exact()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Exact,
            Utilization = 0.8,
        });
        Assert.Equal("80 %", vm.UtilizationText);
        Assert.True(vm.HasUtilizationText);
    }

    [Fact]
    public void UtilizationText_absent_si_null()
    {
        var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5));
        vm.Apply(new WindowState
        {
            Kind = WindowKind.FiveHour,
            Reliability = SourceReliability.Estimated,   // estimé mais utilization null
        });
        Assert.Equal("", vm.UtilizationText);
        Assert.False(vm.HasUtilizationText);
    }
}
