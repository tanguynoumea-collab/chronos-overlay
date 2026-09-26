using System.Collections.Generic;
using System.Linq;

namespace Chronos.Services;

/// <summary>
/// SUB-01 (phase 30.1) — UN SOUS-AGENT QUI ÉCRIT EST UN TRAVAIL DE SA SESSION, et il n'efface jamais une attente
/// d'intervention. Écart E2 du constat de phase 31 (2026-09-26, 14:44) : une session dont le tour parent était fini
/// pendant qu'un agent travaillait en arrière-plan s'affichait « En attente ». Deux sources portent désormais ce travail :
/// le battement de hook d'un sous-agent (motif « PreToolUse (sous-agent) » / « PostToolUse (sous-agent) ») et le dernier
/// message d'un transcript subagents/agent-*.jsonl (motif <see cref="MotifTranscript"/>). Ce type les reconnaît à leur
/// motif — un fait écrit par la source, jamais deviné — et tient la règle de non-effacement au point de fusion.
/// Aucun type WPF, aucune E/S, aucune horloge.
/// </summary>
public static class TravailSousAgent
{
    /// <summary>Le suffixe qui marque un signal de sous-agent. Le hook l'écrit (SessionHookProcessor.SuffixeSousAgent,
    /// plan 30.1-01) ; une garde croisée tient l'égalité des deux constantes (plan 30.1-03).</summary>
    public const string Suffixe = " (sous-agent)";

    /// <summary>Le motif d'une session datée par le transcript de l'un de ses sous-agents.</summary>
    public const string MotifTranscript = "transcript" + Suffixe;

    /// <summary>Ce signal vient-il d'un sous-agent ? Lu sur le motif, quel que soit l'état (un travail devenu attente
    /// déduite par le seuil de silence reste un signal de sous-agent).</summary>
    public static bool Est(SessionSnapshot s)
        => s.Reason is { } r && r.EndsWith(Suffixe, System.StringComparison.Ordinal);
}
