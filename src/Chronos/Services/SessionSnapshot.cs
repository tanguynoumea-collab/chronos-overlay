namespace Chronos.Services;

/// <summary>État d'activité d'une session Claude Code, du point de vue « ai-je quelque chose à faire ? ».</summary>
public enum SessionActivity
{
    Working,            // l'assistant travaille (prompt soumis, session démarrée)
    WaitingAttention,   // réclame une intervention MAINTENANT (permission, question, inactif)
    WaitingTurn,        // a fini son tour, attend ton prochain message
    Unknown,            // signal périmé / indéterminé (jamais présenté comme « en attente »)

    /// <summary>Attente DÉDUITE, jamais observée. Aucun des trente-trois événements du catalogue des hooks
    /// ne couvre l'interruption au clavier (relevé du 2026-09-12) : ce qui est observable, c'est qu'une
    /// session travaillait et que plus aucun battement n'arrive. La même signature vaut pour un terminal
    /// tué ou une mise en veille — d'où l'interrogation dans le libellé. Cette valeur n'est JAMAIS écrite
    /// dans un fichier d'état : elle est dérivée à la lecture, par le moniteur.
    /// <para>AJOUTÉE EN FIN d'énumération, et ce n'est pas un détail de forme : l'insérer au milieu
    /// changerait la valeur entière des membres suivants, et rien ne garantit qu'aucun consommateur ne
    /// s'y adosse.</para></summary>
    WaitingDeduced,
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
