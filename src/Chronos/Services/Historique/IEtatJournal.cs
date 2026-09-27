namespace Chronos.Services.Historique;

/// <summary>
/// JRN-04 — ce que le journal des relevés DIT de lui-même, en contrat NEUTRE (aucun WPF) pour le ViewModel
/// des réglages et le rapport de diagnostic (câblés en 32-05).
///
/// <para>L'âge de la dernière écriture est un chiffre de PREMIÈRE CLASSE : la phase 32 est née d'un magasin
/// qu'on croyait figé depuis deux semaines sans qu'aucun canal ne le dise (CPT-02). Un journal qui se tait
/// doit être vu se taire — « journal muet depuis N min » — et une écriture ratée doit porter sa cause.</para>
/// </summary>
public interface IEtatJournal
{
    /// <summary>Le dossier des fichiers mensuels (<c>releves-AAAA-MM.jsonl</c>). Chemin injecté, jamais construit ici.</summary>
    string Dossier { get; }

    /// <summary>Instant (horloge injectée) de la dernière écriture RÉUSSIE de ce processus. <c>null</c> = aucune depuis le démarrage.</summary>
    DateTimeOffset? DerniereEcriture { get; }

    /// <summary>« Type : message » de la dernière exception d'écriture ; effacée au succès suivant. <c>null</c> = pas de panne connue.</summary>
    string? DerniereErreur { get; }

    /// <summary>Nombre de relevés réellement ÉCRITS par ce processus (les doublons refusés ne comptent pas).</summary>
    int RelevesEcrits { get; }

    /// <summary>ACC-01 (35-02) — « journal ouvert le … » : le t de la première ligne valide du plus ancien fichier mensuel, amorcé
    /// HORS du thread UI au démarrage, posé à la première écriture si le dossier était vide ; le plus ancien des deux gagne.
    /// <c>null</c> = inconnu : le segment « journal du … » est alors omis, jamais inventé.</summary>
    DateTimeOffset? JournalOuvertLe { get; }
}
