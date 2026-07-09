namespace Chronos.Services;

/// <summary>
/// Provenance d'un plafond de tokens (5 h ou hebdo) — métadonnée neutre de calibration (CAL-02).
/// <list type="bullet">
/// <item><see cref="None"/> : jamais calibré (aucun plafond fixé, ou valeur héritée sans source connue).</item>
/// <item><see cref="Manual"/> : saisie explicite de l'utilisateur — JAMAIS écrasée par la calibration auto.</item>
/// <item><see cref="Auto"/> : déduit opportunément (tokens / utilization exacte) par le calibrateur.</item>
/// </list>
/// </summary>
public enum BudgetSource { None, Manual, Auto }
