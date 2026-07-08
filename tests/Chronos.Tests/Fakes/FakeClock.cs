using Chronos.Services;

namespace Chronos.Tests;

/// <summary>Horloge déterministe pour tester les calculs de temps (FractionTimeRemaining, fenêtres glissantes).</summary>
internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }
    public FakeClock(DateTimeOffset now) => UtcNow = now;
}
