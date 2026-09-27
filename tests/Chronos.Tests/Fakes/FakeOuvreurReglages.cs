using Chronos.Services;

namespace Chronos.Tests;

/// <summary>Faux d'<see cref="IOuvreurReglages"/> (réglages v2) : compte les ouvertures demandées, n'ouvre aucune fenêtre.</summary>
public sealed class FakeOuvreurReglages : IOuvreurReglages
{
    public int Ouvertures { get; private set; }

    public void Ouvrir() => Ouvertures++;
}
