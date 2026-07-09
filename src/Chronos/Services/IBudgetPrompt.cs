namespace Chronos.Services;

/// <summary>
/// Sélection de plafonds retournée par le dialogue de calibration manuelle (CAL-01).
/// Chaque champ null signifie « pas de plafond » (l'arc correspondant ne sera pas coloré).
/// </summary>
public sealed record BudgetSelection(long? FiveHour, long? Weekly);

/// <summary>
/// Contrat NEUTRE (aucun type WPF en signature) permettant au ViewModel de demander les deux
/// plafonds de tokens (fenêtre 5 h + fenêtre hebdo) sans dépendre du dialogue WPF concret.
/// L'implémentation WPF <c>BudgetPrompt</c> (namespace Chronos.Views) vit hors de la garde de
/// pureté Services : garder cette interface exempte de type WPF laisse le VM testable.
/// </summary>
public interface IBudgetPrompt
{
    /// <summary>Demande les deux plafonds (valeurs courantes pré-remplies). Retourne null si l'utilisateur annule.</summary>
    BudgetSelection? Ask(long? currentFiveHour, long? currentWeekly);
}
