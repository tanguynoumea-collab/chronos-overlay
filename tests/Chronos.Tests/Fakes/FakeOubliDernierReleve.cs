using Chronos.Services;

namespace Chronos.Tests;

/// <summary>Faux d'<see cref="IOubliDernierReleve"/> (DS2-03 / D-03) : compte les oublis, retient le dernier instant
/// reçu et appelle <see cref="QuandAppele"/> AU MOMENT de chaque oubli — c'est ce rappel qui prouve l'ORDRE des appels
/// de LoginClaude (oubli avant Logout / LoginAsync, puis de nouveau après).</summary>
public sealed class FakeOubliDernierReleve : IOubliDernierReleve
{
    public int Appels { get; private set; }
    public DateTimeOffset? Dernier { get; private set; }
    public Action? QuandAppele { get; set; }

    public void OublierDernierReleve(DateTimeOffset instant)
    {
        Appels++;
        Dernier = instant;
        QuandAppele?.Invoke();
    }
}
