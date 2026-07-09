namespace Chronos.Services;

/// <summary>
/// Inférence PURE du début de la fenêtre 5 h glissante à partir de timestamps triés croissant.
/// Algorithme « A » verrouillé (CONTEXT) : début = le plus ancien message M tel qu'AUCUN trou
/// inter-messages ≥ 5 h n'existe entre M et le message le plus récent, en remontant depuis le
/// plus récent. Classe NEUTRE (DateTimeOffset/TimeSpan uniquement), now en paramètre → testable.
/// </summary>
public static class FiveHourWindowInference
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(5);

    /// <summary>Début de la fenêtre 5 h courante, ou null si inactive/expirée (reset ≤ now).</summary>
    public static DateTimeOffset? InferWindowStart(IReadOnlyList<DateTimeOffset> tsAsc, DateTimeOffset now)
    {
        if (tsAsc.Count == 0) return null;                    // aucune activité → inactive

        var start = tsAsc[^1];                                 // message le plus récent
        for (int i = tsAsc.Count - 2; i >= 0; i--)
        {
            if (start - tsAsc[i] >= Window) break;             // trou ≥ 5 h STRICT → borne atteinte
            start = tsAsc[i];                                  // pas de trou → on recule le début
        }

        var reset = start + Window;
        return reset > now ? start : null;                     // reset ≤ now → fenêtre expirée (EST-02)
    }
}
