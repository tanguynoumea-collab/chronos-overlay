using Chronos.Services;

namespace Chronos.Tests;

/// <summary>
/// Fake IAutostartService : état bool en mémoire + compteurs Enable/Disable. Permet de vérifier
/// que le VM bascule l'autostart et reflète l'état réel (IsEnabled) sans toucher shell:startup.
/// </summary>
internal sealed class FakeAutostartService : IAutostartService
{
    public bool Enabled { get; set; }
    public int EnableCount { get; private set; }
    public int DisableCount { get; private set; }

    public bool IsEnabled() => Enabled;
    public void Enable() { Enabled = true; EnableCount++; }
    public void Disable() { Enabled = false; DisableCount++; }
}
