using System;
using System.Windows;
using Chronos.Interop;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Preuve automatisée de ROB-04 : Reassert emploie HWND_TOPMOST avec
/// SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE (= 0x13) — donc sans jamais voler le focus.
/// </summary>
public class TopmostGuardTests
{
    [WpfFact]
    public void Reassert_utilise_HWND_TOPMOST_sans_activation()
    {
        // Faux délégué capturant les arguments passés au P/Invoke.
        var appele = false;
        var afterCapture = IntPtr.Zero;
        uint flagsCapture = 0;

        TopmostGuard.SetWindowPosFn faux = (hWnd, after, x, y, cx, cy, flags) =>
        {
            appele = true;
            afterCapture = after;
            flagsCapture = flags;
            return true;   // simule un SetWindowPos réussi
        };

        var guard = new TopmostGuard(faux);
        // Une vraie fenêtre STA fournit un HWND non nul ; Attach déclenche un Reassert immédiat.
        var fenetre = new Window { Width = 10, Height = 10, ShowActivated = false };
        try
        {
            guard.Attach(fenetre);

            Assert.True(appele, "Reassert aurait dû appeler le délégué SetWindowPos.");
            Assert.Equal(NativeMethods.HWND_TOPMOST, afterCapture);
            Assert.Equal(
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE,
                flagsCapture);
        }
        finally
        {
            guard.Dispose();
            fenetre.Close();
        }
    }
}
