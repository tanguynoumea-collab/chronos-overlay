using Chronos.Services;

namespace Chronos.Tests;

/// <summary>Faux d'<see cref="IOuvreurHistorique"/> (ACC-02) : compte les ouvertures demandées, n'ouvre aucune fenêtre.</summary>
public sealed class FakeOuvreurHistorique : IOuvreurHistorique
{
    public int Ouvertures { get; private set; }

    public void Ouvrir() => Ouvertures++;
}
