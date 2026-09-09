using Chronos.Services;

namespace Chronos.Tests;

/// <summary>Faux d'<see cref="IAuthStatus"/> : état en mémoire, déclencheur manuel de transition,
/// et comptage des réarmements après login (preuve TOK-03).</summary>
public sealed class FakeAuthStatus : IAuthStatus
{
    /// <summary>Réglable AVANT construction du VM pour tester l'application de l'état initial.</summary>
    public EtatAuthentification Etat { get; set; } = EtatAuthentification.NonConnecte;

    /// <summary>Nombre de réarmements demandés après un login réussi.</summary>
    public int ReinitCount { get; private set; }

    public event EventHandler<EtatAuthentification>? EtatChange;

    public void ReinitialiserApresLogin() => ReinitCount++;

    /// <summary>Provoque une transition observable (comme le ferait l'autorité de jeton).</summary>
    public void Declencher(EtatAuthentification e)
    {
        Etat = e;
        EtatChange?.Invoke(this, e);
    }
}
