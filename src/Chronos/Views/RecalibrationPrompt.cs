using Chronos.Services;
using Chronos.ViewModels;

namespace Chronos.Views;

/// <summary>
/// Implémentation WPF de <see cref="IRecalibrationPrompt"/> (ROB-03). Vit dans <c>Chronos.Views</c>
/// (HORS du scan de pureté Services/Models) : afficher un <see cref="RecalibrationDialog"/> modal ne
/// requiert donc aucune entrée d'allow-list. Le dialogue est centré sur l'overlay (Owner = MainWindow).
/// </summary>
public sealed class RecalibrationPrompt : IRecalibrationPrompt
{
    /// <summary>
    /// Ouvre le dialogue modal et retourne la date d'ancrage choisie (DatePicker ou « maintenant »),
    /// ou null si l'utilisateur annule.
    /// </summary>
    public DateTimeOffset? Ask(DateTimeOffset? current)
    {
        var vm = new RecalibrationViewModel(current ?? DateTimeOffset.Now);
        var dlg = new RecalibrationDialog
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow, // centrage sur l'overlay
        };
        return dlg.ShowDialog() == true ? new DateTimeOffset(vm.SelectedDate) : null;
    }
}
