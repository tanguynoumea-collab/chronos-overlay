using System.Windows;
using Chronos.ViewModels;

namespace Chronos.Views;

/// <summary>
/// Dialogue modal MINIMAL de recalibrage hebdo (ROB-03). Code-behind réduit au strict nécessaire :
/// il s'abonne à <see cref="RecalibrationViewModel.CloseRequested"/> et traduit le signal en
/// <see cref="Window.DialogResult"/> puis ferme. Aucune logique métier ici (elle vit dans le VM).
/// </summary>
public partial class RecalibrationDialog : Window
{
    public RecalibrationDialog()
    {
        InitializeComponent();

        // Le DataContext (RecalibrationViewModel) est fourni par le prompt ; on s'abonne dès qu'il arrive.
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is RecalibrationViewModel vm)
                vm.CloseRequested += accepte =>
                {
                    DialogResult = accepte; // true = validé/maintenant, false = annulé
                    Close();
                };
        };
    }
}
