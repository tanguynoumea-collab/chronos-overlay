using Chronos.Models.Historique.Tokens;

namespace Chronos.Services.Historique.Tokens;

/// <summary>
/// TOK-02 — ce que la reconstruction des agrégats DIT d'elle-même, au ViewModel (bandeau F2, phase 34) et au diagnostic (33-05).
///
/// <para>DES ENTIERS ET DES ÉTATS, JAMAIS UN RAPPORT (garde TOK-05) : « N / M fichiers » se formate côté UI, la barre aussi.
/// Les propriétés sont écrites par le thread de fond et lues par le thread UI : valeurs atomiques (int, bool, référence,
/// ou ticks en <c>long</c> derrière les instants), relues à chaque tick ; aucune cohérence croisée n'est promise entre deux
/// compteurs lus l'un après l'autre — c'est de l'affichage, pas une transaction.</para>
///
/// <para><see cref="Changement"/> est levé SUR LE THREAD DE FOND — le consommateur marshalle (<c>IUiDispatcher.Post</c>),
/// jamais l'émetteur, comme pour <c>RefreshOrchestrator.SnapshotChanged</c>. Contrat NEUTRE : aucun type WPF.</para>
/// </summary>
public interface IEtatReconstruction
{
    /// <summary>L'état nommé de la reconstruction (voir <see cref="PhaseReconstruction"/>).</summary>
    PhaseReconstruction Phase { get; }

    /// <summary>Fichiers passés en revue dans la passe en cours (ouverts ou non) — le « N » du bandeau.</summary>
    int FichiersTraites { get; }

    /// <summary>Inventaire de la passe en cours — le « M » du bandeau.</summary>
    int FichiersTotal { get; }

    /// <summary>Fichiers réellement OUVERTS lors de la dernière passe (0 en incrémental quand rien n'a bougé).</summary>
    int FichiersOuvertsDernierePasse { get; }

    /// <summary>Vrai dès que tous les fichiers dont le mtime tombe dans la semaine courante ont été lus et écrits (checkpoint par mtime, Pitfall 7).</summary>
    bool SemaineCouranteDisponible { get; }

    /// <summary>Dernier fichier passé en revue, chemin RELATIF à la racine des projets (diagnostic) — jamais un chemin absolu.</summary>
    string? DernierFichier { get; }

    /// <summary>« Type : message » de la dernière erreur (flush d'une brique, exception capturée) ; <c>null</c> = pas de panne connue.</summary>
    string? DerniereErreur { get; }

    /// <summary>Fichiers présents dans les curseurs mais absents du disque, cumul depuis le démarrage (purge Claude Code, renommage).</summary>
    int FichiersDisparus { get; }

    /// <summary>Lignes ignorées, cumul depuis le démarrage (lecteur de transcripts + index d'ids).</summary>
    int LignesIgnorees { get; }

    /// <summary>Ids de messages connus de l'index (mois ouverts).</summary>
    int IdsConnus { get; }

    /// <summary>Durée murale de la dernière passe (<c>null</c> avant la première).</summary>
    TimeSpan? DureeMurDernierePasse { get; }

    /// <summary>Temps CPU du PROCESSUS consommé pendant la dernière passe (<c>Process.TotalProcessorTime</c>), pas du seul thread — ce que le gestionnaire des tâches montre.</summary>
    TimeSpan? DureeCpuProcessusDernierePasse { get; }

    /// <summary>Instant (horloge injectée) de la fin de la dernière passe COMPLÈTE ; <c>null</c> = aucune encore.</summary>
    DateTimeOffset? DerniereReconstructionTerminee { get; }

    /// <summary>DATA-1 — messages d'un mois GELÉ déjà agrégé (fichier présent au démarrage, index d'ids plus sur disque), ignorés
    /// car déjà comptés — cumul depuis le démarrage. Membre par défaut : 0 pour qui ne reconstruit rien.</summary>
    int MessagesIgnoresMoisGeles => 0;

    /// <summary>Levé sur le thread de fond après chaque fichier et à chaque changement de phase.</summary>
    event EventHandler? Changement;
}
