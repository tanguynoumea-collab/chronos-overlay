namespace Chronos.Services;

/// <summary>
/// Borne de début de la fenêtre HEBDO courante. Avec ancre : fenêtre roulante [ancre+k·7j]
/// contenant now, k = floor((now-ancre)/7j). Sans ancre : 7 j glissants (comportement v1.0).
/// Cohérent avec WeeklyRecalibration.NextReset (même ancre) → longueur et couleur de l'arc alignées.
/// Classe NEUTRE (DateTimeOffset/TimeSpan), now en paramètre.
/// </summary>
public static class WeeklyWindow
{
    public static readonly TimeSpan Week = TimeSpan.FromDays(7);

    /// <summary>Début de la fenêtre hebdo courante : ancrée si anchor défini, sinon 7 j glissants.</summary>
    public static DateTimeOffset CurrentStart(DateTimeOffset? anchor, DateTimeOffset now)
        => anchor is { } a
            ? a + TimeSpan.FromTicks(Week.Ticks * (long)Math.Floor((now - a) / Week))
            : now - Week;
}
