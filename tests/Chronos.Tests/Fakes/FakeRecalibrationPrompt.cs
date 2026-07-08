using Chronos.Services;

namespace Chronos.Tests;

/// <summary>
/// Fake IRecalibrationPrompt : retourne une DateTimeOffset? programmée (null = annulation).
/// Mémorise la dernière valeur « current » reçue et compte les appels pour les assertions.
/// </summary>
internal sealed class FakeRecalibrationPrompt : IRecalibrationPrompt
{
    public DateTimeOffset? Result { get; set; }
    public int AskCount { get; private set; }
    public DateTimeOffset? LastCurrent { get; private set; }

    public DateTimeOffset? Ask(DateTimeOffset? current)
    {
        AskCount++;
        LastCurrent = current;
        return Result;
    }
}
