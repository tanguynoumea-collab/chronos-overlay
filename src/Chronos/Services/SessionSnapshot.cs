namespace Chronos.Services;

/// <summary>État d'activité d'une session Claude Code, du point de vue « ai-je quelque chose à faire ? ».</summary>
public enum SessionActivity
{
    Working,            // l'assistant travaille (prompt soumis, session démarrée)
    WaitingAttention,   // réclame une intervention MAINTENANT (permission, question, inactif)
    WaitingTurn,        // a fini son tour, attend ton prochain message
    Unknown,            // signal périmé / indéterminé (jamais présenté comme « en attente »)
}

/// <summary>Instantané NEUTRE d'une session (pour l'UI). Aucun type WPF.</summary>
public sealed record SessionSnapshot(
    string SessionId,
    string Project,
    SessionActivity Activity,
    string? Reason,
    System.DateTimeOffset UpdatedAt);
