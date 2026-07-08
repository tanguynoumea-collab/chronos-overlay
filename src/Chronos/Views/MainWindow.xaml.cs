using System.Windows;
using Chronos.ViewModels;

namespace Chronos.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;          // MVVM : la vue reçoit son VM par injection
        Loaded += PlacerCoinSuperieurDroit;
    }

    // Placement de départ observable, non persisté (la persistance/snap = Phase 6).
    private void PlacerCoinSuperieurDroit(object sender, RoutedEventArgs e)
    {
        var zone = SystemParameters.WorkArea;
        Left = zone.Right - Width - 24;
        Top = zone.Top + 24;
    }
}
