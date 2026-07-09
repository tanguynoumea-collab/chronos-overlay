using Chronos.Services;

namespace Chronos.Tests;

/// <summary>
/// Fake IBudgetPrompt : retourne une <see cref="BudgetSelection"/>? programmée (null = annulation).
/// Mémorise les dernières valeurs courantes reçues et compte les appels pour les assertions.
/// </summary>
internal sealed class FakeBudgetPrompt : IBudgetPrompt
{
    public BudgetSelection? Result { get; set; }
    public int AskCount { get; private set; }
    public long? LastFiveHour { get; private set; }
    public long? LastWeekly { get; private set; }

    public BudgetSelection? Ask(long? currentFiveHour, long? currentWeekly)
    {
        AskCount++;
        LastFiveHour = currentFiveHour;
        LastWeekly = currentWeekly;
        return Result;
    }
}
