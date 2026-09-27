using Microsoft.Extensions.Hosting;

namespace Chronos.Services;

/// <summary>
/// Chemin d'arrêt du Host appelé par <c>App.OnExit</c>, extrait pour être testé sous un vrai contexte
/// <c>DispatcherSynchronizationContext</c> (quick 260927 « Quitter Chronos laisse un processus zombie »).
/// Type NEUTRE : aucun type WPF.
/// </summary>
public static class ArretHote
{
    /// <summary>Délai d'arrêt accordé au Host par l'overlay.</summary>
    public static readonly TimeSpan DelaiParDefaut = TimeSpan.FromSeconds(5);

    /// <summary>Arrête puis libère le Host. Retourne true si l'arrêt a été propre ; sinon <paramref name="cause"/> le dit.</summary>
    public static bool Arreter(IHost host, TimeSpan delai, out string? cause)
    {
        cause = null;
        host.StopAsync().GetAwaiter().GetResult();
        host.Dispose();
        return true;
    }
}
