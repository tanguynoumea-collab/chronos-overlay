using Chronos.Services;

namespace Chronos.Tests;

/// <summary>Fake d'<see cref="IStatusLineSetup"/> : bascule un état en mémoire, sans toucher au disque.</summary>
public sealed class FakeStatusLineSetup : IStatusLineSetup
{
    public bool Enabled { get; set; }
    public int OfferCalls { get; private set; }

    public bool IsEnabled() => Enabled;
    public void Enable() => Enabled = true;
    public void Disable() => Enabled = false;
    public void OfferOnFirstRun() => OfferCalls++;
}
