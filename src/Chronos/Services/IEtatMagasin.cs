namespace Chronos.Services;

/// <summary>
/// CPT-02 — observabilité d'un magasin persistant. POURQUOI : le projet a payé trois fois la panne silencieuse
/// (jeton expiré, usage.json figé, puis un faux « gel » de last-exact.json qui a fondé une phase — la copie
/// copy-on-write du paquet MSIX lue depuis une session, alors que le fichier réel était réécrit chaque minute).
/// L'âge de la dernière écriture est un chiffre de première classe, montré au diagnostic et aux réglages ; la
/// dernière erreur ne s'avale plus. Contrat NEUTRE (aucun type WPF), commun au dernier exact, au journal des
/// relevés (phase 32) et aux agrégats de tokens (phase 33).
/// </summary>
public interface IEtatMagasin
{
    /// <summary>Une constante de <see cref="NomsMagasins"/> : c'est par ce nom que le diagnostic apparie l'état
    /// injecté et les faits disque d'un même emplacement.</summary>
    string Nom { get; }

    /// <summary>Fichier (last-exact.json) ou dossier (historique\) effectivement piloté — injecté, jamais en dur.</summary>
    string Chemin { get; }

    /// <summary>UTC. null = jamais écrit : ni par ce processus, ni trouvé sur le disque.</summary>
    DateTimeOffset? DerniereEcriture { get; }

    /// <summary>« Type : message » de la dernière écriture ratée ; null après un succès.</summary>
    string? DerniereErreur { get; }

    /// <summary>MAT-4 (phase 42.2) — « Type : message » de la dernière LECTURE non aboutie (illisible ou inaccessible).
    /// Ne s'efface QUE par une lecture réussie, jamais par une écriture : écrire par-dessus un fichier qu'on n'a pas su lire
    /// ne prouve pas qu'on sait désormais le lire. null par défaut (magasin qui ne l'expose pas).</summary>
    string? DerniereErreurLecture => null;

    /// <summary>MAT-3 (phase 42.2) — chemin du dernier original illisible CONSERVÉ (renommé, jamais supprimé) par ce
    /// processus avant réécriture. null : aucune quarantaine, ou magasin sans quarantaine (données dérivées).</summary>
    string? DerniereQuarantaine => null;
}

/// <summary>Les emplacements nommés de la section « [Magasins persistants] » du diagnostic. Des constantes,
/// pas une énumération : le journal (32-05) et les agrégats (phase 33) s'y rangent sans toucher à ce fichier.</summary>
public static class NomsMagasins
{
    public const string DernierExact = "dernier exact";
    public const string JournalReleves = "journal des relevés";
    public const string AgregatsTokens = "agrégats de tokens";
    public const string SessionsArchivees = "sessions archivées";
    public const string SessionsTraitees = "sessions traitées";
}
