using System.Windows.Threading;

namespace Chronos.Services;

/// <summary>Implémentation WPF de <see cref="IUiDispatcher"/> : encapsule le Dispatcher WPF.</summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;

    public WpfUiDispatcher(Dispatcher dispatcher) => _dispatcher = dispatcher;

    public bool CheckAccess() => _dispatcher.CheckAccess();

    public void Post(Action action)
    {
        if (_dispatcher.CheckAccess()) action();     // déjà sur le thread UI : exécuter directement
        else _dispatcher.BeginInvoke(action);        // sinon : reposter (non bloquant)
    }
}
