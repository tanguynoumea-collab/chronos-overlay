using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Chronos.Services;

/// <summary>
/// Moteur de POLL de fond de la source bureau (ROB-07). Service hébergé (<see cref="IHostedService"/>)
/// qui appelle périodiquement <see cref="DesktopUiaSessionSource.Poll"/> pour remplir son cache — la
/// lecture UIA (potentiellement coûteuse) se fait ainsi HORS du thread UI, jamais dans le chemin
/// synchrone <c>Read</c> emprunté par le timer 2 s de SessionsViewModel.
///
/// Type NEUTRE : aucun type WPF (<c>System.Windows.*</c>). Le <see cref="Timer"/> .NET exécute son
/// rappel sur un thread du POOL → par construction hors thread UI (cœur de ROB-07).
///
/// <see cref="PollOnce"/> est PUBLIC pour permettre un test déterministe d'un tick, sans attendre le timer.
/// </summary>
public sealed class DesktopUiaPollService : IHostedService, IDisposable
{
    /// <summary>Période du poll de fond (~1,5 s) : assez frais pour l'attente, assez léger pour l'UIA.</summary>
    private static readonly TimeSpan Period = TimeSpan.FromSeconds(1.5);

    private readonly DesktopUiaSessionSource _source;
    private readonly IClock _clock;
    private readonly object _gate = new();

    private Timer? _timer;
    private bool _disposed;

    public DesktopUiaPollService(DesktopUiaSessionSource source, IClock clock)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Démarre le timer de fond. Le rappel court sur un thread du pool → jamais le thread UI (ROB-07).</summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_disposed) return Task.CompletedTask;
            // dueTime 0 : un premier poll immédiat pour peupler le cache dès le démarrage ; puis toutes les ~1,5 s.
            _timer ??= new Timer(_ => PollOnce(), null, TimeSpan.Zero, Period);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Un tick de poll : pilote <see cref="DesktopUiaSessionSource.Poll"/> avec l'heure courante.
    /// PUBLIC pour test déterministe. Ne LÈVE JAMAIS : toute erreur est absorbée (dégradation),
    /// afin de ne pas tuer le thread du pool ni le service de fond.
    /// </summary>
    public void PollOnce()
    {
        try
        {
            _source.Poll(_clock.UtcNow);
        }
        catch
        {
            // Ne jamais remonter : un poll raté ne doit pas interrompre la boucle de fond.
        }
    }

    /// <summary>Arrête le timer proprement. Idempotent (le host appelle StopAsync à l'arrêt, cf. App.OnExit).</summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
