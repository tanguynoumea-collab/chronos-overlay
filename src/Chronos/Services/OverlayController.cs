using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Chronos.Interop;
using Chronos.Placement;

namespace Chronos.Services;

/// <summary>
/// Adaptateur WPF de placement de l'overlay (impl. <see cref="IWindowController"/>). Comme
/// <see cref="TopmostGuard"/>, c'est la SEULE frontière WPF assumée de ce type dans Chronos.Services
/// (ajouté à l'allow-list de <c>ServicesLayerPurityTests</c>) : il détient la <see cref="Window"/> et
/// pilote <see cref="NativeMethods.SetWindowPos"/> en pixels PHYSIQUES.
///
/// Pattern 3 (RESEARCH) : on NE positionne JAMAIS via <c>Window.Left/Top</c> (cassés en PerMonitorV2
/// DPI mixte — bug dotnet/wpf #4127, Pitfall 1). On calcule les coins sur la <c>rcWork</c> physique du
/// moniteur courant (<see cref="NativeMethods.MonitorFromWindow"/> + <see cref="NativeMethods.GetMonitorInfo"/>)
/// et on pose la fenêtre en physique. La logique de coin est pure et neutre (<see cref="CornerSnap"/>).
///
/// Le délégué <see cref="TopmostGuard.SetWindowPosFn"/> est injectable → SendToBackground/Snap sont
/// vérifiables en test (délégué capturant), comme <see cref="TopmostGuard"/>.
/// </summary>
public sealed class OverlayController : IWindowController
{
    // Message Win32 émis lors d'un changement de configuration d'écrans (WM_DISPLAYCHANGE).
    private const int WM_DISPLAYCHANGE = 0x007E;

    // Marge d'accroche en DIU (convertie en pixels physiques via le facteur d'échelle du moniteur).
    private const double Margin = 12;

    private readonly TopmostGuard _guard;
    private readonly SettingsService _settings;
    private readonly TopmostGuard.SetWindowPosFn _setWindowPos;

    private Window? _window;
    private IntPtr _hwnd;

    public OverlayController(TopmostGuard guard, SettingsService settings, TopmostGuard.SetWindowPosFn? setWindowPos = null)
    {
        _guard = guard;
        _settings = settings;
        _setWindowPos = setWindowPos ?? NativeMethods.SetWindowPos;
    }

    /// <summary>
    /// Mémorise la fenêtre, capture son HWND (garanti après SourceInitialized) et branche le hook
    /// WM_DISPLAYCHANGE pour re-clamper la fenêtre à chaud si la configuration d'écrans change
    /// (Pitfall 4 : évite un widget hors-écran). Seule surface WPF publique → allow-list de pureté.
    /// </summary>
    public void Attach(Window window)
    {
        _window = window;
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
    }

    /// <summary>
    /// Accroche au coin le plus proche en pixels PHYSIQUES puis persiste coin + device (vérité de
    /// placement). Appelé au RETOUR de DragMove (pas de handler MouseUp) et sur DpiChanged.
    /// </summary>
    public void SnapToNearestCorner()
    {
        if (_window is null || _hwnd == IntPtr.Zero) return;

        // a. Rectangle physique courant de la fenêtre.
        if (!NativeMethods.GetWindowRect(_hwnd, out var wr)) return;

        // b. Moniteur courant (ou le plus proche) + sa zone de travail physique.
        var hMon = NativeMethods.MonitorFromWindow(_hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var mi = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
        if (!NativeMethods.GetMonitorInfo(hMon, ref mi)) return;

        // c. Facteur d'échelle du moniteur courant → marge en pixels physiques.
        double scale = VisualTreeHelper.GetDpi(_window).DpiScaleX;
        double marginPx = Margin * scale;

        // d. Géométrie neutre (physique) fenêtre + zone de travail.
        var winPhys = new RectD(wr.Left, wr.Top, wr.Right - wr.Left, wr.Bottom - wr.Top);
        var workPhys = ToRectD(mi.rcWork);

        // e. Coin le plus proche + classification pour la persistance.
        var (px, py) = CornerSnap.NearestCorner(winPhys, workPhys, marginPx);
        var corner = CornerSnap.ClassifyCorner(winPhys, workPhys);

        // f. Pose physique (jamais Window.Left/Top). Pas de HWND_TOPMOST ici (le guard réaffirme à part).
        _setWindowPos(_hwnd, IntPtr.Zero, (int)px, (int)py, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        // g. Persiste coin + device = vérité ; X/Y purement indicatifs (diagnostic).
        _settings.Save(_settings.Load() with
        {
            Corner = corner,
            MonitorDeviceName = mi.szDevice,
            X = _window.Left,
            Y = _window.Top,
        });
    }

    /// <summary>
    /// Restauration au lancement (coin + device = vérité). Le moniteur cible est retrouvé par device
    /// name ; repli sur le moniteur primaire + même coin si le device a disparu (FEN-04, Pitfall 4).
    /// Applique enfin le mode arrière-plan persisté.
    /// </summary>
    public void RestorePlacement(ChronosSettings s)
    {
        if (_window is null || _hwnd == IntPtr.Zero) return;

        // a. Retrouver le moniteur cible (device name), sinon repli primaire.
        NativeMethods.MONITORINFOEX? match = null;
        NativeMethods.MONITORINFOEX? primary = null;
        IntPtr matchHmon = IntPtr.Zero, primaryHmon = IntPtr.Zero;

        NativeMethods.MonitorEnumProc cb = (IntPtr hMon, IntPtr hdc, ref NativeMethods.RECT rc, IntPtr data) =>
        {
            var m = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
            if (NativeMethods.GetMonitorInfo(hMon, ref m))
            {
                if (s.MonitorDeviceName is not null && m.szDevice == s.MonitorDeviceName)
                {
                    match = m;
                    matchHmon = hMon;
                }
                if ((m.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0)
                {
                    primary = m;
                    primaryHmon = hMon;
                }
            }
            return true;
        };
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero);

        var target = match ?? primary;
        if (target is null) return;                 // aucun moniteur (cas impossible en pratique)
        var mon = target.Value;
        var targetHmon = match is not null ? matchHmon : primaryHmon;

        // b. Échelle du moniteur cible (repli VisualTreeHelper pour le moniteur courant).
        double scale;
        if (targetHmon != IntPtr.Zero && NativeMethods.GetDpiForMonitor(targetHmon, 0, out uint dpiX, out _) == 0)
            scale = dpiX / 96.0;
        else
            scale = VisualTreeHelper.GetDpi(_window).DpiScaleX;

        // c. Dimensions physiques de la fenêtre + marge.
        double physW = _window.ActualWidth * scale;
        double physH = _window.ActualHeight * scale;
        double marginPx = Margin * scale;

        // d. Top-left physique du coin persisté (offset workArea inclus dans le RectD).
        var workPhys = ToRectD(mon.rcWork);
        var (px, py) = CornerSnap.CornerToTopLeft(s.Corner, new RectD(0, 0, physW, physH), workPhys, marginPx);

        // e. Pose physique avant le premier rendu.
        _setWindowPos(_hwnd, IntPtr.Zero, (int)px, (int)py, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

        // f. Applique le mode arrière-plan persisté.
        if (s.Background) SendToBackground();
        else BringToForeground();
    }

    // Re-snap sur le moniteur courant (WM_DISPLAYCHANGE) pour éviter un widget hors-écran (Pitfall 4).
    private void ReclampToValidMonitor() => SnapToNearestCorner();

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DISPLAYCHANGE) ReclampToValidMonitor();
        return IntPtr.Zero;
    }

    // ---- IWindowController (Pattern 5 : arrière-plan + Suspend/Resume du guard) ----

    public void SendToBackground()
    {
        if (_window is null) return;
        _window.Topmost = false;
        _guard.Suspend();                            // sinon le guard re-force le topmost toutes les 2 s
        _setWindowPos(_hwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        _settings.Save(_settings.Load() with { Background = true });
    }

    public void BringToForeground()
    {
        if (_window is null) return;
        _window.Topmost = true;
        _guard.Resume();                             // repose HWND_TOPMOST immédiatement, sans vol de focus
        _settings.Save(_settings.Load() with { Background = false });
    }

    public void Quit() => System.Windows.Application.Current.Shutdown();

    // Conversion RECT physique (Win32) → RectD neutre.
    private static RectD ToRectD(NativeMethods.RECT r)
        => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
}
