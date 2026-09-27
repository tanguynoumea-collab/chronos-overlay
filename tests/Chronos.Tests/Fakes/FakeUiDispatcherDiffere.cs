using Chronos.Services;

namespace Chronos.Tests;

/// <summary>
/// Fake <see cref="IUiDispatcher"/> à FILE DIFFÉRÉE : <see cref="Post"/> enfile sans exécuter, <see cref="Vider"/> exécute tout
/// dans l'ordre. Complète <see cref="FakeUiDispatcher"/> (inline) pour prouver la COALESCENCE : cinquante <c>Changement</c> levés
/// d'un thread de fond ne doivent produire qu'UN <c>Post</c> tant que le précédent n'a pas été vidé (Pattern 6 bis).
/// Thread-safe : les Post arrivent de threads de fond.
/// </summary>
internal sealed class FakeUiDispatcherDiffere : IUiDispatcher
{
    private readonly Queue<Action> _file = new();
    private readonly object _verrou = new();
    private int _postCount;

    public int PostCount => Volatile.Read(ref _postCount);
    public bool OnUiThread { get; set; }

    public bool CheckAccess() => OnUiThread;

    public void Post(Action action)
    {
        Interlocked.Increment(ref _postCount);
        lock (_verrou) _file.Enqueue(action);
    }

    /// <summary>Exécute toutes les actions en attente (y compris celles enfilées pendant le vidage).</summary>
    public void Vider()
    {
        while (true)
        {
            Action a;
            lock (_verrou)
            {
                if (_file.Count == 0) return;
                a = _file.Dequeue();
            }
            a();
        }
    }
}
