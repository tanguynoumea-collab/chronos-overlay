namespace Chronos.Services;

/// <summary>Horloge réelle branchée sur DateTimeOffset.UtcNow.</summary>
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
