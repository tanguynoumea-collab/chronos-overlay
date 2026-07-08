using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Chronos.Interop;

namespace Chronos.Services;

/// <summary>Réaffirme périodiquement la bande topmost sans voler le focus (ROB-04).</summary>
public sealed class TopmostGuard : IDisposable
{
    // Délégué injectable → rend le comportement (flags) vérifiable en test.
    public delegate bool SetWindowPosFn(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    private readonly SetWindowPosFn _setWindowPos;
    private readonly DispatcherTimer _timer;
    private IntPtr _hwnd;

    public TopmostGuard(SetWindowPosFn? setWindowPos = null)
    {
        _setWindowPos = setWindowPos ?? NativeMethods.SetWindowPos;
        // Timer dédié 2 s, distinct du futur tick d'interpolation UI (Phases 4-5).
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Reassert();
    }

    /// <summary>À appeler quand la fenêtre a un HWND (SourceInitialized ou après Show).</summary>
    public void Attach(Window window)
    {
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        Reassert();          // une première réaffirmation immédiate
        _timer.Start();
    }

    public void Reassert()
    {
        if (_hwnd == IntPtr.Zero) return;
        // SetWindowPos + SWP_NOACTIVATE : réaffirme le topmost sans activer (aucun vol de focus).
        // NE PAS utiliser le toggle Topmost=false;Topmost=true (scintillement + réactivation).
        _setWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    public void Dispose() => _timer.Stop();
}
