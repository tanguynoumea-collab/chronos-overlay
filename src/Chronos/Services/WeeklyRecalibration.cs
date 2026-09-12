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
/// - Si la fenêtre porte déjà un resets_at, quelle que soit sa fiabilité, on la laisse strictement
///   inchangée : un reset connu est un FAIT, et une synthèse ne remplace jamais un fait.
/// - Sinon (aucun reset exposé par la source), on synthétise un resets_at mais on CONSERVE la
///   fiabilité d'origine → le badge « estimée » reste affiché, le recalibrage donne une date et
///   ne prétend pas donner un chiffre.
/// </summary>
public static class WeeklyRecalibration
{
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    /// <summary>
    /// Applique le recalibrage. Renvoie <paramref name="weekly"/> inchangée si elle porte déjà un
    /// reset, ou si aucune ancre n'est fournie. Sinon, remplace ResetsAt par le prochain reset
    /// strictement futur (ancre + n×7j) SANS toucher à la fiabilité.
    /// </summary>
    public static WindowState Apply(WindowState weekly, DateTimeOffset? anchor, DateTimeOffset now)
    {
        // Phase 19 — la garde porte desormais sur le RESET SEUL, plus sur la fiabilite. Un resets_at
        // connu est un FAIT, quelle que soit la fraicheur du pourcentage qui l'accompagne : les fenetres
        // hebdo corrigees par delta en portent un, et la garde precedente (exacte ET datee) les aurait
        // fait tomber dans le chemin de synthese, remplacant le reset reel du serveur par « ancre + n
        // semaines ». Le compte a rebours serait devenu une supposition la ou la reponse existait.
        // Le recalibrage existe pour les sources qui n'exposent PAS de reset hebdo — pas pour corriger
        // celles qui en exposent un.
        if (weekly.ResetsAt is not null)
            return weekly;

        // Pas d'ancre → rien à synthétiser (on n'invente pas de date).
        if (anchor is null)
            return weekly;

        var next = NextReset(anchor.Value, now);
        return weekly with { ResetsAt = next }; // fiabilité inchangée → badge « estimée » conservé
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
