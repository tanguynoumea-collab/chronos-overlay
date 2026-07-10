using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Chronos.Services;

/// <summary>
/// Implémentation RÉELLE et OS-dépendante d'<see cref="IForegroundWatch"/> : la fenêtre de l'app bureau
/// Claude est-elle actuellement au premier plan de l'OS ? Alimente la branche NET-02 (acquittement par
/// focus) du <see cref="SessionTreatmentTracker"/>.
///
/// Ce type est MINCE et SANS logique métier (comme <see cref="WindowsUiaTreeProvider"/>) : Win32 P/Invoke
/// pur (<c>user32.dll</c>), AUCUN type WPF → la couche Services reste NEUTRE (garde
/// <c>ServicesLayerPurityTests</c> verte, aucun HWND ne fuit publiquement : seul un <c>bool</c> est exposé).
///
/// Critère : la fenêtre de premier plan de l'OS est « Claude » si son TITRE contient « Claude »
/// (insensible à la casse). C'est le MÊME critère souple que la découverte de fenêtre UIA de la Phase 13
/// (<see cref="WindowsUiaTreeProvider"/> reconnaît la fenêtre Claude par son Name contenant « Claude ») —
/// cohérence et robustesse aux versions de l'app. La lecture d'un titre de fenêtre est rapide (quelques µs)
/// et non bloquante → appelable depuis le chemin SYNCHRONE <c>SessionMonitor.Read</c> (thread UI) sans
/// risque de figer l'overlay (cf. CLAUDE.md, coûts de composition d'une fenêtre layered).
///
/// Best-effort : NE LÈVE JAMAIS. Toute erreur/indisponibilité → <c>false</c> ⇒ la branche NET-02 ne
/// déclenche simplement pas (aucun faux traitement — décision d'honnêteté du CONTEXT.md).
///
/// Il N'EST PAS unit-testé : il dépend d'une vraie fenêtre + de l'OS (comme
/// <see cref="WindowsUiaTreeProvider"/>). Sa correction se juge en EXÉCUTION ; la garde DI (test de
/// résolution dans <c>CompositionRootTests</c>) prouve seulement le CÂBLAGE, pas le comportement OS.
/// </summary>
public sealed class WindowsForegroundWatch : IForegroundWatch
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    /// <summary>La fenêtre de premier plan de l'OS a-t-elle un titre contenant « Claude » ? Best-effort,
    /// jamais d'exception : indisponible/erreur → false (NET-02 dormante ce cycle).</summary>
    public bool IsClaudeForeground()
    {
        try
        {
            var h = GetForegroundWindow();
            if (h == IntPtr.Zero) return false;

            var sb = new StringBuilder(256);
            var len = GetWindowText(h, sb, sb.Capacity);
            if (len <= 0) return false;

            return sb.ToString().IndexOf("Claude", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            // Best-effort : toute erreur d'interop → false (aucun faux traitement).
            return false;
        }
    }
}
