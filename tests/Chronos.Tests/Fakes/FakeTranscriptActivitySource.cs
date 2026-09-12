using Chronos.Services;

namespace Chronos.Tests;

/// <summary>
/// Faux <see cref="ITranscriptActivitySource"/> : journal PROGRAMMABLE + compteur de passes + mode panne.
/// Le compteur est l'instrument de la contrainte de coût de la phase 19 : une passe RÉELLE coûte 2,7 à
/// 3,2 s et lit 536 Mo (mesuré le 2026-09-12 sur la machine cible : 474 fichiers, 155 171 lignes).
/// Compter les passes est donc une assertion de CONCEPTION, pas une micro-optimisation.
/// </summary>
internal sealed class FakeTranscriptActivitySource : ITranscriptActivitySource
{
    private int _lectures;

    /// <summary>Nombre de délégations réellement effectuées (thread-safe).</summary>
    public int Lectures => Volatile.Read(ref _lectures);

    /// <summary>Journal rendu. null = la source ÉCHOUE (disque absent, E/S) — la doctrine doit dégrader.</summary>
    public TranscriptActivityLog? Journal;

    public Task<TranscriptActivityLog> ReadAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref _lectures);
        return Journal is null
            ? Task.FromException<TranscriptActivityLog>(new System.IO.IOException("source indisponible"))
            : Task.FromResult(Journal);
    }
}
