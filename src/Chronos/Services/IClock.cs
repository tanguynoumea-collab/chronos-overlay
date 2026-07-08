namespace Chronos.Services;

/// <summary>Horloge injectable pour des calculs de temps déterministes et testables.</summary>
public interface IClock { DateTimeOffset UtcNow { get; } }
