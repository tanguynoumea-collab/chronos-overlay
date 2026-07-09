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
        => null; // STUB — implémenté en phase GREEN

    /// <summary>Applique un plafond AUTO à la fenêtre visée SEULEMENT si sa source n'est pas Manual.</summary>
    public static ChronosSettings ApplyAuto(ChronosSettings current, WindowKind kind, long deducedBudget, DateTimeOffset now)
        => current; // STUB — implémenté en phase GREEN
}
