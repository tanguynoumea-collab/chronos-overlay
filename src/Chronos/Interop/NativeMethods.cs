using System;
using System.Runtime.InteropServices;

namespace Chronos.Interop;

internal static class NativeMethods
{
    // hWndInsertAfter : -1 = HWND_TOPMOST (place/maintient dans la bande topmost)
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    public const uint SWP_NOSIZE     = 0x0001;  // ne pas redimensionner
    public const uint SWP_NOMOVE     = 0x0002;  // ne pas déplacer
    public const uint SWP_NOACTIVATE = 0x0010;  // NE PAS activer → aucun vol de focus

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
}
