using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Chronos.Services;

/// <summary>
/// Horloge de fond du JETON (TOK-01) : elle sollicite à cadence fixe
/// <see cref="ChronosTokenAuthority.GetAccessTokenAsync"/> ; c'est l'autorité — et elle seule — qui
/// décide s'il faut réellement rafraîchir.
/// Le rafraîchissement cesse ainsi d'être PARESSEUX : un jeton expiré est renouvelé sans qu'aucun
/// utilisateur n'ait eu besoin de regarder le cadran.
///
/// Tick FIXE + prédicat d'horloge murale plutôt que réveil calculé : un portable qui dort huit heures
/// ferait mentir tout minuteur fondé sur le temps écoulé, alors qu'une comparaison
/// « expiration − marge ≤ maintenant » rattrape naturellement la veille, les changements d'heure et
/// une expiration vieille de deux mois.
///
/// Ordonnancement avec <see cref="RefreshOrchestrator"/> (l'horloge des DONNÉES) : les deux services
/// hébergés démarrent quasi simultanément. C'est sans danger précisément grâce à l'autorité unique —
/// le premier arrivé prend le sémaphore et rafraîchit, le second passe la double-vérification et
/// réutilise le jeton frais. Aucune coordination explicite n'est nécessaire.
///
/// Type NEUTRE : aucun type WPF. Le <see cref="Timer"/> .NET exécute son rappel sur un thread du POOL,
/// donc par construction hors thread UI.
/// </summary>
public sealed class TokenRefreshService : IHostedService, IDisposable
{
    /// <summary>Période du tick. Doit rester STRICTEMENT inférieure à la marge de l'autorité (12 min),
    /// sans quoi le rafraîchissement cesserait d'être préventif.</summary>
    private static readonly TimeSpan Periode = TimeSpan.FromSeconds(60);

    private readonly ChronosTokenAuthority _autorite;
    private readonly object _gate = new();

    private Timer? _timer;
    private bool _disposed;

    public TokenRefreshService(ChronosTokenAuthority autorite)
        => _autorite = autorite ?? throw new ArgumentNullException(nameof(autorite));

    /// <summary>Démarre l'horloge de fond. Idempotent (le host appelle Start/Stop à l'ouverture et à
    /// la fermeture de l'application).</summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_disposed) return Task.CompletedTask;
            // dueTime ZÉRO : premier tick IMMÉDIAT. Le cas réel de l'utilisateur est un jeton déjà
            // expiré (2026-07-12) ; attendre 60 s afficherait une pastille de déconnexion transitoire
            // au lancement — exactement le faux signal que TOK-02 doit éviter.
            _timer ??= new Timer(_ => _ = TickAsync(), null, TimeSpan.Zero, Periode);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Un tick. PUBLIC pour permettre un test déterministe sans attendre le minuteur.
    /// Ne LÈVE JAMAIS : une exception non gérée sur un thread du pool tuerait la boucle de fond,
    /// donc le rafraîchissement préventif, donc TOK-01.
    /// </summary>
    public async Task TickAsync()
    {
        try
        {
            await _autorite.GetAccessTokenAsync();   // l'autorité décide seule s'il faut rafraîchir
        }
        catch
        {
            // Dégradation silencieuse : un tick raté ne doit pas interrompre la boucle.
        }
    }

    /// <summary>Arrête le minuteur proprement. Idempotent.</summary>
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
