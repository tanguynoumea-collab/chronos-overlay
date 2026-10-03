using System;
using System.IO;
using System.Windows;
using System.Runtime.InteropServices;
using Chronos.Interop;
using Chronos.Placement;
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

    // ---- CAD-02 (phase 40) : recalage sur le coin COURANT quand l'empreinte change ----

    // Réglages temp + chemin du fichier (pour prouver qu'aucune persistance n'a lieu).
    private static (SettingsService Settings, string Fichier) TempSettingsAvecFichier()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ChronosTests", Guid.NewGuid().ToString("N"));
        var paths = new ChronosPaths(Path.Combine(dir, "usage.json"), Path.Combine(dir, "projects"));
        return (new SettingsService(paths), paths.SettingsFile);
    }

    // Lit, sur le HWND réel, la taille physique de la fenêtre et la zone de travail de son moniteur.
    private static (double Largeur, double Hauteur, RectD Travail) GeometrieReelle(Window fenetre)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(fenetre).Handle;
        Assert.True(NativeMethods.GetWindowRect(hwnd, out var wr));
        var hMon = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var mi = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
        Assert.True(NativeMethods.GetMonitorInfo(hMon, ref mi));
        var travail = new RectD(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right - mi.rcWork.Left, mi.rcWork.Bottom - mi.rcWork.Top);
        return (wr.Right - wr.Left, wr.Bottom - wr.Top, travail);
    }

    private sealed record Appel(IntPtr Apres, int X, int Y, uint Flags);

    [WpfFact]
    public void Le_recalage_pose_le_coin_courant_sans_z_order_ni_activation_ni_persistance()
    {
        var (settings, fichier) = TempSettingsAvecFichier();
        settings.Save(settings.Load() with { Corner = OverlayCorner.BottomLeft });
        var appels = new System.Collections.Generic.List<Appel>();
        TopmostGuard.SetWindowPosFn faux = (_, after, x, y, _, _, flags) => { appels.Add(new Appel(after, x, y, flags)); return true; };

        var guard = new TopmostGuard((_, _, _, _, _, _, _) => true);
        var controller = new OverlayController(guard, settings, faux);
        var fenetre = new Window { Width = 190, Height = 66, ShowActivated = false, WindowStyle = WindowStyle.None };
        try
        {
            controller.Attach(fenetre);
            controller.RestorePlacement(settings.Load());
            Assert.Equal(OverlayCorner.BottomLeft, controller.CoinCourant);

            var avant = File.ReadAllText(fichier);
            appels.Clear();

            controller.RecalerSurCoinCourant();

            var appel = Assert.Single(appels);
            Assert.Equal(IntPtr.Zero, appel.Apres);
            Assert.Equal(NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER, appel.Flags);

            var (l, h, travail) = GeometrieReelle(fenetre);
            Assert.Equal(OverlayCorner.BottomLeft, CornerSnap.ClassifyCorner(new RectD(appel.X, appel.Y, l, h), travail));
            Assert.InRange(appel.X, travail.X - 1, travail.Right - l + 1);   // bornée à la zone de travail (arrondi inclus)
            Assert.InRange(appel.Y, travail.Y - 1, travail.Bottom - h + 1);

            Assert.Equal(avant, File.ReadAllText(fichier));                  // AUCUNE persistance
        }
        finally
        {
            guard.Dispose();
            fenetre.Close();
        }
    }

    [WpfFact]
    public void Sans_coin_en_memoire_le_recalage_reprend_le_coin_persiste()
    {
        var (settings, _) = TempSettingsAvecFichier();
        settings.Save(settings.Load() with { Corner = OverlayCorner.TopLeft });
        var appels = new System.Collections.Generic.List<Appel>();
        TopmostGuard.SetWindowPosFn faux = (_, after, x, y, _, _, flags) => { appels.Add(new Appel(after, x, y, flags)); return true; };

        var guard = new TopmostGuard((_, _, _, _, _, _, _) => true);
        var controller = new OverlayController(guard, settings, faux);
        var fenetre = new Window { Width = 190, Height = 66, ShowActivated = false, WindowStyle = WindowStyle.None };
        try
        {
            controller.Attach(fenetre);
            Assert.Null(controller.CoinCourant);           // aucune pose encore

            controller.RecalerSurCoinCourant();

            var appel = Assert.Single(appels);
            var (l, h, travail) = GeometrieReelle(fenetre);
            Assert.Equal(OverlayCorner.TopLeft, CornerSnap.ClassifyCorner(new RectD(appel.X, appel.Y, l, h), travail));
        }
        finally
        {
            guard.Dispose();
            fenetre.Close();
        }
    }

    [WpfFact]
    public void Le_recalage_avant_Attach_ne_fait_rien()
    {
        var appele = false;
        var guard = new TopmostGuard((_, _, _, _, _, _, _) => true);
        var controller = new OverlayController(guard, TempSettings(), (_, _, _, _, _, _, _) => { appele = true; return true; });
        try
        {
            controller.RecalerSurCoinCourant();            // ni exception ni appel natif
            Assert.False(appele);
        }
        finally
        {
            guard.Dispose();
        }
    }

    [WpfFact]
    public void Le_snap_memorise_le_coin_qu_il_persiste()
    {
        var (settings, _) = TempSettingsAvecFichier();
        var guard = new TopmostGuard((_, _, _, _, _, _, _) => true);
        var controller = new OverlayController(guard, settings, (_, _, _, _, _, _, _) => true);
        var fenetre = new Window { Width = 190, Height = 66, ShowActivated = false, WindowStyle = WindowStyle.None };
        try
        {
            controller.Attach(fenetre);
            controller.SnapToNearestCorner();

            Assert.NotNull(controller.CoinCourant);
            Assert.Equal(settings.Load().Corner, controller.CoinCourant);
        }
        finally
        {
            guard.Dispose();
            fenetre.Close();
        }
    }
}
