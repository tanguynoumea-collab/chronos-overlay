namespace Chronos.Services;

/// <summary>Réglages des horloges données. Injecté en Singleton (pattern ChronosPaths).
/// La persistance settings.json arrive en Phase 6 ; ici, valeurs par défaut pré-câblées.</summary>
public sealed record RefreshOptions(TimeSpan PeriodicInterval, TimeSpan Debounce)
{
    public static RefreshOptions Default => new(
        PeriodicInterval: TimeSpan.FromSeconds(60),        // filet de sécurité (RAF-02)
        Debounce:         TimeSpan.FromMilliseconds(300)); // coalescence/settle (RAF-01)
}
