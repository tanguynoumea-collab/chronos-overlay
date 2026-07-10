namespace Chronos.Services;

/// <summary>
/// Seam NEUTRE du focus premier-plan de l'OS : la fenêtre de l'app bureau Claude est-elle actuellement au
/// premier plan ? Sert à la branche NET-02 (acquittement par focus) du <see cref="SessionTreatmentTracker"/>.
///
/// Best-effort, NON bloquant, NE LÈVE JAMAIS : si l'information est indisponible → renvoyer <c>false</c>
/// (la branche NET-02 ne déclenche simplement pas — aucun faux traitement). L'implémentation réelle
/// (WindowsForegroundWatch, dépendante de l'OS) est livrée au plan 02 ; tant qu'elle n'est pas câblée,
/// <c>foreground = null</c> côté <see cref="SessionMonitor"/> laisse la branche NET-02 dormante.
/// </summary>
public interface IForegroundWatch
{
    /// <summary>La fenêtre de l'app bureau Claude est-elle au premier plan de l'OS ? (best-effort, jamais d'exception).</summary>
    bool IsClaudeForeground();
}
