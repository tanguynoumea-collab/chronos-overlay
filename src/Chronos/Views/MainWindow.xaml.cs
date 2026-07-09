using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Chronos.Services;
using Chronos.Theming;
using Chronos.ViewModels;

namespace Chronos.Views;

public partial class MainWindow : Window
{
    private readonly TopmostGuard _topmostGuard;
    private readonly OverlayController _controller;
    private readonly MainViewModel _vm;

    // État persisté à restaurer AVANT le premier rendu (fourni par App avant Show).
    private ChronosSettings? _restored;

    public MainWindow(MainViewModel viewModel, TopmostGuard topmostGuard, OverlayController controller)
    {
        InitializeComponent();
        DataContext = viewModel;          // MVVM : la vue reçoit son VM par injection
        _vm = viewModel;
        _topmostGuard = topmostGuard;
        _controller = controller;

        // Thèmes : appliquer les pinceaux du thème persisté (ressources dynamiques) AVANT le 1er rendu,
        // puis suivre chaque changement émis par le VM (sélection dans la fenêtre de réglages).
        ApplyThemeBrushes(ThemeCatalog.ByKey(viewModel.SelectedThemeKey));
        viewModel.ThemeChanged += ApplyThemeBrushes;

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

    // Clic DROIT : ouvre la fenêtre de réglages sombre (remplace le menu contextuel gris). Positionnée
    // près du curseur, elle partage le VM du cadran et se referme quand elle perd le focus.
    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        var dlg = new SettingsWindow(_vm) { Owner = this };
        var p = PointToScreen(e.GetPosition(this));
        var src = PresentationSource.FromVisual(this);
        var m = src?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var dip = m.Transform(new System.Windows.Point(p.X, p.Y));
        dlg.Left = dip.X - 20;
        dlg.Top = dip.Y - 20;
        dlg.Show();
        dlg.Activate();
        e.Handled = true;
    }

    // Applique les pinceaux d'un thème dans les ressources de la fenêtre → les DynamicResource du cadran
    // (disque, pistes, graduations, textes) se mettent à jour instantanément.
    private void ApplyThemeBrushes(ChronosTheme theme)
    {
        foreach (var kv in theme.BrushTokens())
            Resources[kv.Key] = kv.Value;
    }
}
