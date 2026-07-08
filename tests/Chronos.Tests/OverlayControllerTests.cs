using System;
using System.IO;
using System.Windows;
using Chronos.Interop;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Preuve automatisée du mode arrière-plan (FEN-05) porté par l'adaptateur WPF
/// <see cref="OverlayController"/> : SendToBackground pose HWND_BOTTOM sans activation et
/// désactive Topmost ; BringToForeground réactive Topmost. Le placement physique réel
/// (SnapToNearestCorner/RestorePlacement) reste couvert par l'UAT 06-04 (nécessite un vrai
/// moniteur) ; ici on prouve le comportement observable via un délégué SetWindowPos capturant,
/// exactement comme <see cref="TopmostGuardTests"/>.
/// </summary>
public class OverlayControllerTests
{
    // SettingsService pointant sur un dossier temp unique (jamais le vrai %APPDATA%).
    private static SettingsService TempSettings()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ChronosTests", Guid.NewGuid().ToString("N"));
        var paths = new ChronosPaths(Path.Combine(dir, "usage.json"), Path.Combine(dir, "projects"));
        return new SettingsService(paths);
    }

    [WpfFact]
    public void SendToBackground_pose_HWND_BOTTOM_sans_activation_et_desactive_topmost()
    {
        // Délégué capturant les arguments passés au P/Invoke SetWindowPos.
        var appele = false;
        var afterCapture = IntPtr.Zero;
        uint flagsCapture = 0;
        TopmostGuard.SetWindowPosFn faux = (hWnd, after, x, y, cx, cy, flags) =>
        {
            appele = true;
            afterCapture = after;
            flagsCapture = flags;
            return true;
        };

        var guard = new TopmostGuard((_, _, _, _, _, _, _) => true);
        var controller = new OverlayController(guard, TempSettings(), faux);
        var fenetre = new Window { Width = 10, Height = 10, ShowActivated = false, Topmost = true };
        try
        {
            controller.Attach(fenetre);     // HWND garanti (EnsureHandle) + hook écran
            controller.SendToBackground();

            Assert.True(appele, "SendToBackground aurait dû appeler le délégué SetWindowPos.");
            Assert.Equal(NativeMethods.HWND_BOTTOM, afterCapture);
            Assert.True((flagsCapture & NativeMethods.SWP_NOACTIVATE) != 0, "SWP_NOACTIVATE attendu (aucun vol de focus).");
            Assert.False(fenetre.Topmost);  // Topmost désactivé en arrière-plan
        }
        finally
        {
            guard.Dispose();
            fenetre.Close();
        }
    }

    [WpfFact]
    public void BringToForeground_reactive_le_topmost()
    {
        var guard = new TopmostGuard((_, _, _, _, _, _, _) => true);
        var controller = new OverlayController(guard, TempSettings(), (_, _, _, _, _, _, _) => true);
        var fenetre = new Window { Width = 10, Height = 10, ShowActivated = false, Topmost = false };
        try
        {
            controller.Attach(fenetre);
            controller.BringToForeground();

            Assert.True(fenetre.Topmost);   // retour premier plan → Topmost réactivé
        }
        finally
        {
            guard.Dispose();
            fenetre.Close();
        }
    }
}
