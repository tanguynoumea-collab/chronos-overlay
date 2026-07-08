using Chronos.Services;

namespace Chronos.Tests;

/// <summary>
/// Fake IWindowController : compte les appels des trois commandes de pilotage de fenêtre
/// (arrière-plan / premier plan / quitter) pour prouver que le VM les déclenche sans écran réel.
/// </summary>
internal sealed class FakeWindowController : IWindowController
{
    public int SendToBackgroundCount { get; private set; }
    public int BringToForegroundCount { get; private set; }
    public int QuitCount { get; private set; }

    public void SendToBackground() => SendToBackgroundCount++;
    public void BringToForeground() => BringToForegroundCount++;
    public void Quit() => QuitCount++;
}
