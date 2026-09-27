using System;
using System.Runtime.InteropServices;

namespace Chronos.Interop;

internal static class NativeMethods
{
    // hWndInsertAfter : -1 = HWND_TOPMOST (place/maintient dans la bande topmost)
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    // hWndInsertAfter : 1 = HWND_BOTTOM (envoie la fenêtre au fond — mode arrière-plan FEN-05)
    public static readonly IntPtr HWND_BOTTOM = new(1);

    public const uint SWP_NOSIZE     = 0x0001;  // ne pas redimensionner
    public const uint SWP_NOMOVE     = 0x0002;  // ne pas déplacer
    public const uint SWP_NOACTIVATE = 0x0010;  // NE PAS activer → aucun vol de focus

    // Repli au moniteur le plus proche quand la fenêtre n'intersecte aucun moniteur.
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    // Indicateur « moniteur primaire » dans MONITORINFOEX.dwFlags (repli écran primaire FEN-04).
    public const uint MONITORINFOF_PRIMARY = 0x1;

    // Rectangle Win32 (coordonnées PHYSIQUES en pixels).
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    // Infos d'un moniteur : rcMonitor (bornes), rcWork (zone utile hors barre des tâches),
    // szDevice (\\.\DISPLAYn). IMPORTANT : renseigner cbSize AVANT l'appel (sinon échec silencieux).
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    // Moniteur contenant la fenêtre (ou le plus proche avec MONITOR_DEFAULTTONEAREST).
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    // rcWork/szDevice du moniteur courant. Renseigner mi.cbSize = Marshal.SizeOf<MONITORINFOEX>() AVANT l'appel.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    // ACC-02 : délai de double-clic de l'utilisateur (ms), réglage Windows. P/Invoke plutôt que
    // SystemInformation.DoubleClickTime : WinForms n'est pas référencé par le projet.
    [DllImport("user32.dll")]
    internal static extern uint GetDoubleClickTime();

    // Rectangle PHYSIQUE de la fenêtre (centre fenêtre pour le calcul de coin — contourne Window.Left/Top cassés).
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    // DPI d'un moniteur ciblé (restauration vers un moniteur précis). Win 8.1+. dpiType MDT_EFFECTIVE_DPI = 0.
    [DllImport("Shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    // Énumération des moniteurs pour retrouver un moniteur par device name / repli primaire (FEN-04).
    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprc, IntPtr data);
    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    // Coins arrondis DWM de la fenêtre Historique (D-34-22, HIS-01) : attribut DWMWA_WINDOW_CORNER_PREFERENCE (33), valeur
    // DWMWCP_ROUND (2). Honoré par Windows 11 (build ≥ 22000) seulement ; ailleurs DWM rend un HRESULT d'échec (E_INVALIDARG)
    // que l'appelant IGNORE (best-effort : coins droits sur Windows 10, jamais d'exception grâce à PreserveSig = true).
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWCP_ROUND = 2;

    // Réglages v2 (quick 260927) : agrandissement borné à la ZONE DE TRAVAIL du moniteur. Une fenêtre WindowStyle=None s'agrandirait
    // sinon à la taille du moniteur, barre des tâches comprise. ptMaxPosition est relatif au moniteur ; tout est en pixels physiques.
    public const int WM_GETMINMAXINFO = 0x0024;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
