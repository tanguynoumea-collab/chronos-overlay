using System.Windows;
using System.Windows.Input;
using Chronos.ViewModels;

namespace Chronos.Views;

/// <summary>
/// Fenêtre de réglages sombre (ouverte au clic droit sur le cadran, remplace le menu contextuel gris).
/// Partage le <see cref="MainViewModel"/> du cadran (DataContext fourni par l'ouvreur) → toutes les
/// commandes (connexion, thème, arrière-plan, démarrage, diagnostic, quitter) pilotent le même état.
/// Se ferme quand elle perd le focus (comportement « popover »).
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Deactivated += (_, _) => Close();   // clic ailleurs → referme (comme un menu)
    }

    private void Header_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // Diagnostic : réutilise le rapport du VM dans une pop-up (comme l'ancien menu).
    private async void Diagnostic_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var report = await ((MainViewModel)DataContext).BuildDiagnosticReportAsync();
            MessageBox.Show(this, report, "Chronos — Diagnostic", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(this, "Le diagnostic a échoué : " + ex.Message, "Chronos — Diagnostic");
        }
    }
}
