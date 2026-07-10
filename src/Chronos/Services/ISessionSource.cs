using System.Collections.Generic;

namespace Chronos.Services;

/// <summary>
/// Contrat COMMUN d'une source de sessions (transcripts CLI, app bureau UIA, …).
/// <see cref="Read"/> retourne l'instantané courant des sessions connues, de façon NON bloquante ;
/// les sources sont fusionnées dans SessionMonitor (transcripts + hooks + bureau).
/// </summary>
public interface ISessionSource
{
    /// <summary>Instantané courant des sessions à l'instant <paramref name="now"/>. Non bloquant.</summary>
    IReadOnlyList<SessionSnapshot> Read(System.DateTimeOffset now);
}
