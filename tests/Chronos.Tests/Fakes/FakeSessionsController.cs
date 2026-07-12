using Chronos.Services;
using Chronos.Theming;

namespace Chronos.Tests;

/// <summary>Fake d'<see cref="ISessionsController"/> : bascule un état en mémoire, sans hooks ni fenêtre.</summary>
public sealed class FakeSessionsController : ISessionsController
{
    public bool Enabled { get; set; }
    public SessionStyle Style { get; private set; }
    public bool Vertical { get; private set; }
    public bool IsEnabled => Enabled;
    public void Enable() => Enabled = true;
    public void Disable() => Enabled = false;
    public void ShowIfEnabled() { }
    public void SetStyle(SessionStyle style) => Style = style;
    public void SetVerticalLayout(bool vertical) => Vertical = vertical;
    public ChronosTheme? Theme { get; private set; }
    public void SetTheme(ChronosTheme theme) => Theme = theme;
}
