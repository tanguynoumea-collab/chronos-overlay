using System.Windows;
using System.Windows.Input;
using Chronos.Services;
using Chronos.ViewModels;

namespace Chronos.Views;

public partial class MainWindow : Window
{
    private readonly TopmostGuard _topmostGuard;
    private readonly OverlayController _controller;

    // État persisté à restaurer AVANT le premier rendu (fourni par App avant Show).
    private ChronosSettings? _restored;

    public MainWindow(MainViewModel viewModel, TopmostGuard topmostGuard, OverlayController controller)
    {
        InitializeComponent();
        DataContext = viewModel;          // MVVM : la vue reçoit son VM par injection
        _topmostGuard = topmostGuard;
        _controller = controller;

        // HWND garanti ici : on attache le guard puis le controller, et on restaure le placement
        // AVANT le premier rendu (pas de flash), remplaçant l'ancien PlacerCoinSuperieurDroit (Loaded).
        SourceInitialized += (_, _) =>
        {
            _topmostGuard.Attach(this);
            _controller.Attach(this);
            if (_restored is not null) _controller.RestorePlacement(_restored);
        };

        // FEN-02 : glisser via DragMove (bloquant), snap au RETOUR (Pattern 1 / Pitfall 3).
        MouseLeftButtonDown += Cadran_MouseLeftButtonDown;

        // Pattern 3 : re-caler le coin après un franchissement de moniteur DPI mixte (taille physique change).
        DpiChanged += (_, _) => _controller.SnapToNearestCorner();

        // Démarre l'horloge UI 1 s côté vue (RAF-03) : le DispatcherTimer est créé sur le thread UI,
        // jamais dans le ctor du VM (Pitfall 4).
        Loaded += (_, _) => viewModel.StartClock();
    }

    /// <summary>
    /// Fournit l'état persisté à restaurer. Appelé par App AVANT <see cref="Window.Show"/> :
    /// SourceInitialized appliquera <see cref="OverlayController.RestorePlacement"/> avant le 1er rendu.
    /// </summary>
    public void ApplyRestoredState(ChronosSettings settings) => _restored = settings;

    private void Cadran_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        DragMove();                          // BLOQUE jusqu'au relâchement (consomme le MouseUp)
        _controller.SnapToNearestCorner();   // snap AU RETOUR de DragMove (pas de handler MouseUp — Pitfall 3)
    }

    // Clic au CENTRE : bascule pourcentages ↔ temps avant reset. On marque l'événement Handled pour
    // qu'il ne remonte PAS au handler de fenêtre (Cadran_MouseLeftButtonDown) → pas de DragMove parasite.
    private void CentreHit_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        (DataContext as MainViewModel)?.ToggleCenterMode();
        e.Handled = true;
    }

    // Menu « Diagnostic… » : affiche le rapport dans une POP-UP (facile à screenshoter), pas un fichier.
    private async void OnDiagnosticClick(object sender, RoutedEventArgs e)
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
