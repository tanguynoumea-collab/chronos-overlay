using Chronos.Services;

namespace Chronos.Tests;

/// <summary>Fake d'<see cref="ISessionsController"/> : bascule un état en mémoire, sans hooks ni fenêtre.</summary>
public sealed class FakeSessionsController : ISessionsController
{
    public bool Enabled { get; set; }
    public bool IsEnabled => Enabled;
    public void Enable() => Enabled = true;
    public void Disable() => Enabled = false;
    public void ShowIfEnabled() { }
}
