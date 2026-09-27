using Microsoft.Extensions.Hosting;

namespace Chronos.Services;

/// <summary>
/// Chemin d'arrêt du Host appelé par <c>App.OnExit</c> (quick 260927 « Quitter Chronos laisse un processus zombie »).
///
/// OnExit s'exécute SUR LE THREAD UI, sous <c>DispatcherSynchronizationContext</c>. L'ancienne
/// attente synchrone de <c>StopAsync</c> (GetResult) y bloquait ce thread sans borne : il suffisait qu'une seule reprise
/// d'un service hébergé attende le Dispatcher pour que le processus reste vivant, sans fenêtre, en tenant le mutex
/// d'instance unique. Ici, l'arrêt part HORS du thread appelant (<see cref="Task.Run(Func{Task})"/> : aucun contexte
/// capturé) et chaque étape est BORNÉE par <paramref name="delai"/> — arrêt, puis libération. Un dépassement ne
/// bloque plus rien : il est dit (<c>cause</c>) et l'appelant garantit la terminaison.
///
/// Type NEUTRE : aucun type WPF.
/// </summary>
public static class ArretHote
{
    /// <summary>Délai accordé à CHAQUE étape (arrêt, puis libération). Un arrêt sain prend quelques dizaines de ms.</summary>
    public static readonly TimeSpan DelaiParDefaut = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Arrête puis libère le Host, hors du thread appelant, en temps borné. Ne lève jamais.
    /// Retourne true si les deux étapes ont fini proprement dans le délai ; sinon false et <paramref name="cause"/>
    /// décrit l'étape fautive (ligne de diagnostic).
    /// </summary>
    public static bool Arreter(IHost host, TimeSpan delai, out string? cause)
    {
        ArgumentNullException.ThrowIfNull(host);
        cause = null;

        // Le jeton est annulé AU MÊME délai : les services qui l'honorent (BackgroundService) rendent la main d'eux-mêmes.
        using var cts = new CancellationTokenSource(delai);
        var arret = Etape(() => host.StopAsync(cts.Token), delai, "arrêt");
        if (arret is not null) cause = arret;

        // Libération des singletons IDisposable (watcher, minuteurs…), elle aussi hors thread appelant et bornée.
        var liberation = Etape(() => { host.Dispose(); return Task.CompletedTask; }, delai, "libération");
        if (liberation is not null) cause = cause is null ? liberation : cause + " ; " + liberation;

        return cause is null;
    }

    /// <summary>Joue une étape sur le pool et l'attend au plus <paramref name="delai"/>. Null = étape propre.</summary>
    private static string? Etape(Func<Task> action, TimeSpan delai, string nom)
    {
        try
        {
            var tache = Task.Run(action);
            if (!tache.Wait(delai))
                return $"{nom} du Host non terminé après {delai.TotalSeconds:0.#} s";
            return null;
        }
        catch (AggregateException ex)
        {
            var e = ex.InnerExceptions.Count == 1 ? ex.InnerException! : ex;
            return $"{nom} du Host en échec : {e.GetType().Name}: {e.Message}";
        }
        catch (Exception ex)
        {
            return $"{nom} du Host en échec : {ex.GetType().Name}: {ex.Message}";
        }
    }
}
