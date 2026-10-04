namespace Chronos.Services;

/// <summary>Réglages des horloges données. Injecté en Singleton (pattern ChronosPaths).
/// Valeurs par défaut ; en production l'intervalle vient de settings.json (RefreshIntervalSeconds),
/// câblé dans App.xaml.cs.</summary>
public sealed record RefreshOptions(TimeSpan PeriodicInterval, TimeSpan Debounce)
{
    public static RefreshOptions Default => new(
        PeriodicInterval: TimeSpan.FromSeconds(60),        // filet de sécurité (RAF-02)
        Debounce:         TimeSpan.FromMilliseconds(300)); // coalescence/settle (RAF-01)
}
