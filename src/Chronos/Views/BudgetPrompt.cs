using Chronos.Services;
using Chronos.ViewModels;

namespace Chronos.Views;

/// <summary>
/// Implémentation WPF de <see cref="IBudgetPrompt"/> (CAL-01). Vit dans <c>Chronos.Views</c>
/// (HORS du scan de pureté Services/Models) : afficher un <see cref="BudgetDialog"/> modal ne
/// requiert donc aucune entrée d'allow-list. Le dialogue est centré sur l'overlay (Owner = MainWindow).
/// </summary>
public sealed class BudgetPrompt : IBudgetPrompt
{
    /// <summary>
    /// Ouvre le dialogue modal et retourne les plafonds saisis (null par champ = pas de plafond),
    /// ou null si l'utilisateur annule.
    /// </summary>
    public BudgetSelection? Ask(long? currentFiveHour, long? currentWeekly)
    {
        var vm = new BudgetDialogViewModel(currentFiveHour, currentWeekly);
        var dlg = new BudgetDialog
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow, // centrage sur l'overlay
        };
        return dlg.ShowDialog() == true ? new BudgetSelection(vm.ParsedFiveHour, vm.ParsedWeekly) : null;
    }
}
