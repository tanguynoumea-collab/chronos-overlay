using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Recalibrage hebdomadaire best-effort (ROB-03). Le reset « 7 jours » dérive en pratique
/// (~72 h autour d'un horaire d'ancrage non documenté), donc quand la source fiable n'expose
/// PAS de resets_at hebdo on synthétise un prochain reset à partir d'une ancre utilisateur.
///
/// Classe PURE (aucun type WPF, aucun I/O) : elle consomme/produit un <see cref="WindowState"/>
/// neutre et prend <c>now</c> en paramètre → testable en [Fact] sans horloge.
///
/// Pitfall 7 (honnêteté des chiffres — Core Value) : le recalibrage NE DOIT PAS « mentir ».
/// - Si la fenêtre est déjà <see cref="SourceReliability.Exact"/> AVEC un resets_at, on la laisse
///   strictement inchangée : les chiffres exacts priment toujours.
/// - Sinon (repli / estimation), on synthétise un resets_at mais on CONSERVE
///   <see cref="SourceReliability.Estimated"/> → le badge « estimée » reste affiché.
/// </summary>
public static class WeeklyRecalibration
{
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    /// <summary>
    /// Applique le recalibrage. Renvoie <paramref name="weekly"/> inchangée si elle est déjà
    /// exacte avec un reset, ou si aucune ancre n'est fournie. Sinon, remplace ResetsAt par le
    /// prochain reset strictement futur (ancre + n×7j) EN RESTANT Estimated.
    /// </summary>
    public static WindowState Apply(WindowState weekly, DateTimeOffset? anchor, DateTimeOffset now)
    {
        // Les chiffres exacts priment : on ne recalibre jamais par-dessus un resets_at fiable.
        if (weekly.Reliability == SourceReliability.Exact && weekly.ResetsAt is not null)
            return weekly;

        // Pas d'ancre → rien à synthétiser (on n'invente pas de date).
        if (anchor is null)
            return weekly;

        var next = NextReset(anchor.Value, now);
        return weekly with { ResetsAt = next }; // reste Estimated → badge « estimée » conservé
    }

    /// <summary>
    /// Unique reset aligné sur l'ancre tombant dans l'intervalle ]now ; now+7j] — c.-à-d. le
    /// prochain reset strictement futur, que l'ancre soit passée OU future.
    /// L'utilisateur saisit naturellement la PROCHAINE date de reset (future, vue dans /usage) :
    /// dans ce cas l'ancre elle-même est le bon reset, il ne faut donc PAS forcer « +1 semaine ».
    /// </summary>
    private static DateTimeOffset NextReset(DateTimeOffset anchor, DateTimeOffset now)
    {
        var next = anchor;
        while (next <= now) next += Week;      // ancre passée → avancer jusqu'au futur
        while (next - Week > now) next -= Week; // ancre trop loin dans le futur → ramener au 1er cycle > now
        return next;
    }
}
