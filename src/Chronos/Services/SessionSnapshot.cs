namespace Chronos.Services;

/// <summary>État d'activité d'une session Claude Code, du point de vue « ai-je quelque chose à faire ? ».</summary>
public enum SessionActivity
{
    Working,            // l'assistant travaille (prompt soumis, session démarrée)
    WaitingAttention,   // réclame une intervention MAINTENANT (permission, question, inactif)
    WaitingTurn,        // a fini son tour, attend ton prochain message
    Unknown,            // signal périmé / indéterminé (jamais présenté comme « en attente »)
}

/// <summary>Instantané NEUTRE d'une session Claude Code (pour l'UI). Aucun type WPF.
/// <para>Phase 21 (SRC-01) : le record est revenu à cinq champs. Les deux champs de typologie ajoutés en
/// phase 13 (quel mode de l'app bureau héberge la session, de quelle surface elle vient) n'avaient de
/// producteur que la source app-bureau ; sans elle, ils ne pouvaient plus valoir que leur défaut. Un champ
/// qui ne peut plus varier est un champ qui ment par omission.</para></summary>
public sealed record SessionSnapshot(
    string SessionId,
    string Project,
    SessionActivity Activity,
    string? Reason,
    System.DateTimeOffset UpdatedAt);
