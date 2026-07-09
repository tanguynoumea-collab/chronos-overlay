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

    // Clic DROIT : ouvre la fenêtre de réglages sombre (remplace le menu contextuel gris), CENTRÉE sur le
    // cadran puis clampée à la zone de travail du moniteur (jamais hors écran). Elle partage le VM du cadran.
    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        var dlg = new SettingsWindow(_vm) { Owner = this };
        // Rendu HORS écran d'abord → SizeToContent calcule la taille sans clignotement visible.
        dlg.Left = -10000; dlg.Top = -10000;
        dlg.Show();
        CenterOnCadran(dlg);
        dlg.Activate();
        e.Handled = true;
    }

    // Centre la fenêtre sur le cadran (pixels physiques) et la clampe à la zone de travail du moniteur.
    private void CenterOnCadran(Window dlg)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == System.IntPtr.Zero || !Interop.NativeMethods.GetWindowRect(hwnd, out var wr)) return;

        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int dlgW = (int)System.Math.Round(dlg.ActualWidth * scale);
        int dlgH = (int)System.Math.Round(dlg.ActualHeight * scale);
        int left = (wr.Left + wr.Right) / 2 - dlgW / 2;
        int top = (wr.Top + wr.Bottom) / 2 - dlgH / 2;

        var hMon = Interop.NativeMethods.MonitorFromWindow(hwnd, Interop.NativeMethods.MONITOR_DEFAULTTONEAREST);
        var mi = new Interop.NativeMethods.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Interop.NativeMethods.MONITORINFOEX>() };
        if (Interop.NativeMethods.GetMonitorInfo(hMon, ref mi))
        {
            left = System.Math.Max(mi.rcWork.Left, System.Math.Min(left, mi.rcWork.Right - dlgW));
            top = System.Math.Max(mi.rcWork.Top, System.Math.Min(top, mi.rcWork.Bottom - dlgH));
        }

        // Physique → DIU pour Window.Left/Top.
        var src = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
        var m = src?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var tl = m.Transform(new System.Windows.Point(left, top));
        dlg.Left = tl.X;
        dlg.Top = tl.Y;
    }

    // Applique les pinceaux d'un thème dans les ressources de la fenêtre → les DynamicResource du cadran
    // (disque, pistes, graduations, textes) se mettent à jour instantanément.
    private void ApplyThemeBrushes(ChronosTheme theme)
    {
        foreach (var kv in theme.BrushTokens())
            Resources[kv.Key] = kv.Value;
    }
}
