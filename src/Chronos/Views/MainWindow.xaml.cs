using System.Windows;
using Chronos.Services;
using Chronos.ViewModels;

namespace Chronos.Views;

public partial class MainWindow : Window
{
    private readonly TopmostGuard _topmostGuard;

    public MainWindow(MainViewModel viewModel, TopmostGuard topmostGuard)
    {
        InitializeComponent();
        DataContext = viewModel;          // MVVM : la vue reçoit son VM par injection
        _topmostGuard = topmostGuard;
        SourceInitialized += (_, _) => _topmostGuard.Attach(this);  // HWND garanti ici
        Loaded += PlacerCoinSuperieurDroit;
        // Démarre l'horloge UI 1 s côté vue (RAF-03) : le DispatcherTimer est créé sur le thread UI,
        // jamais dans le ctor du VM (Pitfall 4). Les deux handlers Loaded coexistent.
        Loaded += (_, _) => viewModel.StartClock();
    }

    // Placement de départ observable, non persisté (la persistance/snap = Phase 6).
    private void PlacerCoinSuperieurDroit(object sender, RoutedEventArgs e)
    {
        var zone = SystemParameters.WorkArea;
        Left = zone.Right - Width - 24;
        Top = zone.Top + 24;
    }
}
