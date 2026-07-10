namespace Chronos.Services;

/// <summary>État d'activité d'une session Claude Code, du point de vue « ai-je quelque chose à faire ? ».</summary>
public enum SessionActivity
{
    Working,            // l'assistant travaille (prompt soumis, session démarrée)
    WaitingAttention,   // réclame une intervention MAINTENANT (permission, question, inactif)
    WaitingTurn,        // a fini son tour, attend ton prochain message
    Unknown,            // signal périmé / indéterminé (jamais présenté comme « en attente »)
}

/// <summary>Type de session, du point de vue de la surface Claude qui l'héberge.</summary>
public enum SessionKind
{
    Unknown,   // indéterminé (défaut) — ne jamais inventer un type
    Chat,      // conversation « Mode chat » de l'app bureau
    Code,      // session agentique Code (panneaux Terminal/Diff/Aperçu/Actions de session)
    Cowork,    // session Cowork (pont VM « Contrôle à distance »)
}

/// <summary>Origine de la session : ligne de commande (transcripts/hooks) ou app bureau (UIA).</summary>
public enum SessionOrigin
{
    Cli,       // Claude Code CLI (transcripts JSONL + hooks) — défaut historique
    Desktop,   // app de bureau Claude (lecture UI Automation)
}

/// <summary>Instantané NEUTRE d'une session (pour l'UI). Aucun type WPF.</summary>
// NB : Kind/Origin sont AJOUTÉS EN FIN avec valeurs par défaut (Unknown/Cli) → les usages
// positionnels existants (TranscriptSessionSource, SessionMonitor, hooks, tests) compilent
// inchangés et conservent le comportement CLI d'origine (non-régression garantie).
public sealed record SessionSnapshot(
    string SessionId,
    string Project,
    SessionActivity Activity,
    string? Reason,
    System.DateTimeOffset UpdatedAt,
    SessionKind Kind = SessionKind.Unknown,      // défaut Unknown → usages CLI existants inchangés
    SessionOrigin Origin = SessionOrigin.Cli);   // défaut Cli    → usages CLI existants inchangés
