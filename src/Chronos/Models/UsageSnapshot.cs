namespace Chronos.Models;

/// <summary>Snapshot immuable des deux fenêtres + fraîcheur de la donnée sous-jacente.</summary>
public sealed record UsageSnapshot
{
    public required WindowState FiveHour { get; init; }
    public required WindowState SevenDay { get; init; }

    /// <summary>Horodatage de capture de la source (bridge capturedAt / lecture JSONL) ; null si inconnu.</summary>
    public DateTimeOffset? SourceCapturedAt { get; init; }

    /// <summary>Âge de la donnée au moment du snapshot (staleness) ; null si capture inconnue.</summary>
    public TimeSpan? Age { get; init; }

    /// <summary>Snapshot « données indisponibles » : deux fenêtres Unavailable, aucun crash (ROB-01).</summary>
    public static UsageSnapshot Empty => new()
    {
        FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
        SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
    };
}
