using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Magasin persistant du DERNIER RELEVÉ EXACT, par fenêtre (EXA-01). Squelette RED : le
/// comportement est spécifié par LastExactStoreTests et implémenté à l'étape GREEN.
/// </summary>
public sealed class LastExactStore
{
    /// <summary>Version du schéma persisté. Une version inconnue est refusée en bloc.</summary>
    public const int SchemaVersion = 1;

    private readonly string _path;

    public LastExactStore(string path) => _path = path;

    /// <summary>Fichier effectivement piloté (injecté, jamais construit en dur).</summary>
    public string Path => _path;

    /// <summary>Persiste les fenêtres exactes du snapshot.</summary>
    public void Save(UsageSnapshot snapshot) => throw new NotImplementedException();

    /// <summary>Relit les fenêtres encore valides à l'instant donné.</summary>
    public LastExactWindows? Load(DateTimeOffset now) => throw new NotImplementedException();
}

/// <summary>Fenêtres exactes rechargées. Chaque membre est null si absent ou invalidé.</summary>
public sealed record LastExactWindows(WindowState? FiveHour, WindowState? SevenDay);
