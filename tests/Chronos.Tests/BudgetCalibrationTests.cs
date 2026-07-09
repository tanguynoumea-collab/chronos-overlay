using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la logique PURE de calibration (CAL-02) : déduction d'un plafond (tokens / utilization
/// exacte, arrondi ; null si mesure inexploitable) et la règle de priorité manuel/auto d'ApplyAuto
/// (l'auto n'écrase que None ou Auto, JAMAIS Manual ; ne contamine pas l'autre fenêtre). Tests purs.
/// </summary>
public class BudgetCalibrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 09, 08, 00, 00, TimeSpan.Zero);

    // --- Deduce : nominal ---

    [Fact]
    public void Deduce_util_moitie_rend_le_double_des_tokens()
        => Assert.Equal(2_000_000L, BudgetCalibration.Deduce(0.5, 1_000_000));

    [Fact]
    public void Deduce_util_quart_rend_le_quadruple_des_tokens()
        => Assert.Equal(3_000_000L, BudgetCalibration.Deduce(0.25, 750_000));

    // --- Deduce : bornes ---

    [Fact]
    public void Deduce_util_nulle_rend_null()
        => Assert.Null(BudgetCalibration.Deduce(0.0, 1_000_000));

    [Fact]
    public void Deduce_util_negative_rend_null()
        => Assert.Null(BudgetCalibration.Deduce(-0.5, 1_000_000));

    [Fact]
    public void Deduce_tokens_nuls_rend_null()
        => Assert.Null(BudgetCalibration.Deduce(0.5, 0));

    [Fact]
    public void Deduce_tokens_negatifs_rend_null()
        => Assert.Null(BudgetCalibration.Deduce(0.5, -10));

    [Fact]
    public void Deduce_util_superieure_a_un_est_acceptee()
        => Assert.Equal(500L, BudgetCalibration.Deduce(2.0, 1_000)); // 1000 / 2 = 500

    // --- ApplyAuto : fenêtre 5 h ---

    [Fact]
    public void ApplyAuto_5h_source_None_ecrit_budget_source_Auto_et_timestamp()
    {
        var current = new ChronosSettings(); // FiveHourBudgetSource == None par défaut
        var updated = BudgetCalibration.ApplyAuto(current, WindowKind.FiveHour, 2_000_000, Now);

        Assert.Equal(2_000_000L, updated.FiveHourTokenBudget);
        Assert.Equal(BudgetSource.Auto, updated.FiveHourBudgetSource);
        Assert.Equal(Now, updated.FiveHourBudgetCalibratedAt);
    }

    [Fact]
    public void ApplyAuto_5h_source_Auto_ecrase_budget_et_timestamp_reste_Auto()
    {
        var earlier = Now - TimeSpan.FromHours(3);
        var current = new ChronosSettings
        {
            FiveHourTokenBudget = 1_000_000,
            FiveHourBudgetSource = BudgetSource.Auto,
            FiveHourBudgetCalibratedAt = earlier,
        };
        var updated = BudgetCalibration.ApplyAuto(current, WindowKind.FiveHour, 2_000_000, Now);

        Assert.Equal(2_000_000L, updated.FiveHourTokenBudget);
        Assert.Equal(BudgetSource.Auto, updated.FiveHourBudgetSource);
        Assert.Equal(Now, updated.FiveHourBudgetCalibratedAt); // timestamp mis à jour
    }

    [Fact]
    public void ApplyAuto_5h_source_Manual_ne_change_rien()
    {
        var current = new ChronosSettings
        {
            FiveHourTokenBudget = 999,
            FiveHourBudgetSource = BudgetSource.Manual,
        };
        var updated = BudgetCalibration.ApplyAuto(current, WindowKind.FiveHour, 2_000_000, Now);

        Assert.Same(current, updated);           // référence identique : settings inchangés
        Assert.Equal(999L, updated.FiveHourTokenBudget);
        Assert.Equal(BudgetSource.Manual, updated.FiveHourBudgetSource);
    }

    [Fact]
    public void ApplyAuto_5h_ne_contamine_pas_le_plafond_hebdo()
    {
        var current = new ChronosSettings
        {
            WeeklyTokenBudget = 42_000,
            WeeklyBudgetSource = BudgetSource.Manual,
        };
        var updated = BudgetCalibration.ApplyAuto(current, WindowKind.FiveHour, 2_000_000, Now);

        Assert.Equal(42_000L, updated.WeeklyTokenBudget);
        Assert.Equal(BudgetSource.Manual, updated.WeeklyBudgetSource);
        Assert.Null(updated.WeeklyBudgetCalibratedAt);
    }

    // --- ApplyAuto : fenêtre hebdo ---

    [Fact]
    public void ApplyAuto_hebdo_source_None_ecrit_budget_source_Auto_et_timestamp()
    {
        var current = new ChronosSettings();
        var updated = BudgetCalibration.ApplyAuto(current, WindowKind.SevenDay, 60_000_000, Now);

        Assert.Equal(60_000_000L, updated.WeeklyTokenBudget);
        Assert.Equal(BudgetSource.Auto, updated.WeeklyBudgetSource);
        Assert.Equal(Now, updated.WeeklyBudgetCalibratedAt);
    }

    [Fact]
    public void ApplyAuto_hebdo_source_Auto_ecrase_budget_et_timestamp()
    {
        var current = new ChronosSettings
        {
            WeeklyTokenBudget = 50_000_000,
            WeeklyBudgetSource = BudgetSource.Auto,
            WeeklyBudgetCalibratedAt = Now - TimeSpan.FromDays(1),
        };
        var updated = BudgetCalibration.ApplyAuto(current, WindowKind.SevenDay, 60_000_000, Now);

        Assert.Equal(60_000_000L, updated.WeeklyTokenBudget);
        Assert.Equal(BudgetSource.Auto, updated.WeeklyBudgetSource);
        Assert.Equal(Now, updated.WeeklyBudgetCalibratedAt);
    }

    [Fact]
    public void ApplyAuto_hebdo_source_Manual_ne_change_rien()
    {
        var current = new ChronosSettings
        {
            WeeklyTokenBudget = 777,
            WeeklyBudgetSource = BudgetSource.Manual,
        };
        var updated = BudgetCalibration.ApplyAuto(current, WindowKind.SevenDay, 60_000_000, Now);

        Assert.Same(current, updated);
        Assert.Equal(777L, updated.WeeklyTokenBudget);
        Assert.Equal(BudgetSource.Manual, updated.WeeklyBudgetSource);
    }

    [Fact]
    public void ApplyAuto_hebdo_ne_contamine_pas_le_plafond_5h()
    {
        var current = new ChronosSettings
        {
            FiveHourTokenBudget = 88_000,
            FiveHourBudgetSource = BudgetSource.Manual,
        };
        var updated = BudgetCalibration.ApplyAuto(current, WindowKind.SevenDay, 60_000_000, Now);

        Assert.Equal(88_000L, updated.FiveHourTokenBudget);
        Assert.Equal(BudgetSource.Manual, updated.FiveHourBudgetSource);
        Assert.Null(updated.FiveHourBudgetCalibratedAt);
    }
}
