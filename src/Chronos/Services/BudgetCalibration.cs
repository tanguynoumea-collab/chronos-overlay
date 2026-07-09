using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Logique PURE de calibration des plafonds (aucun I/O, aucun type WPF). Déduit un plafond de
/// tokens à partir d'une utilization exacte et applique la règle de priorité manuel/auto (CAL-02).
/// </summary>
public static class BudgetCalibration
{
    /// <summary>Plafond déduit = tokens / utilization_exacte, arrondi. null si mesure inexploitable.</summary>
    public static long? Deduce(double exactUtilization, long jsonlTokens)
        => exactUtilization > 0 && jsonlTokens > 0
            ? (long)Math.Round(jsonlTokens / exactUtilization, MidpointRounding.AwayFromZero)
            : null;

    /// <summary>Applique un plafond AUTO à la fenêtre <paramref name="kind"/> SEULEMENT si sa source
    /// n'est pas Manual (l'auto n'écrase que l'auto ou l'absence de valeur — CAL-02). Sinon renvoie
    /// les settings inchangés (référence identique).</summary>
    public static ChronosSettings ApplyAuto(ChronosSettings current, WindowKind kind, long deducedBudget, DateTimeOffset now)
    {
        if (kind == WindowKind.FiveHour)
            return current.FiveHourBudgetSource == BudgetSource.Manual ? current
                : current with { FiveHourTokenBudget = deducedBudget, FiveHourBudgetSource = BudgetSource.Auto, FiveHourBudgetCalibratedAt = now };
        return current.WeeklyBudgetSource == BudgetSource.Manual ? current
            : current with { WeeklyTokenBudget = deducedBudget, WeeklyBudgetSource = BudgetSource.Auto, WeeklyBudgetCalibratedAt = now };
    }
}
