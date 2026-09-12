using System.Collections.Generic;

namespace Chronos.Services;

/// <summary>
/// Contrat COMMUN d'une source de sessions. <see cref="Read"/> retourne l'instantané courant des sessions
/// connues, de façon NON bloquante ; les sources sont fusionnées dans <c>SessionMonitor</c>.
///
/// <para>Phase 21 — le périmètre du widget est Claude Code, et lui seul. La seule implémentation de
/// production est <see cref="TranscriptSessionSource"/> (lecture des transcripts JSONL). L'interface est
/// conservée parce qu'elle est le point de substitution des tests du moniteur, pas parce qu'on prévoit
/// d'autres sources.</para>
/// </summary>
public interface ISessionSource
{
    /// <summary>Instantané courant des sessions à l'instant <paramref name="now"/>. Non bloquant.</summary>
    IReadOnlyList<SessionSnapshot> Read(System.DateTimeOffset now);
}
