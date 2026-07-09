using System.Windows;
using Chronos.ViewModels;

namespace Chronos.Views;

/// <summary>
/// Dialogue modal MINIMAL de calibration manuelle des plafonds (CAL-01). Code-behind réduit au
/// strict nécessaire : il s'abonne à <see cref="BudgetDialogViewModel.CloseRequested"/> et traduit
/// le signal en <see cref="Window.DialogResult"/> puis ferme. Aucune logique métier ici (elle vit dans le VM).
/// </summary>
public partial class BudgetDialog : Window
{
    public BudgetDialog()
    {
        InitializeComponent();

        // Le DataContext (BudgetDialogViewModel) est fourni par le prompt ; on s'abonne dès qu'il arrive.
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is BudgetDialogViewModel vm)
                vm.CloseRequested += accepte =>
                {
                    DialogResult = accepte; // true = validé, false = annulé
                    Close();
                };
        };
    }
}
