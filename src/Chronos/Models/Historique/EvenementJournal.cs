namespace Chronos.Models.Historique;

/// <summary>
/// JRN-02 — les événements de COUVERTURE du journal : ce qui explique une absence de relevés.
///
/// <para>Les mots que le lecteur (JRN-05) attachera à un trou sont ceux du plan de design (§4) :
/// <see cref="Arret"/> → « Chronos arrêté », <see cref="JetonInvalide"/> → « jeton invalide »,
/// <see cref="SondeRefusee"/> → « sonde refusée ». Un trou sans événement n'est jamais tu : « cause inconnue ».</para>
///
/// <para><see cref="NonReconnu"/> n'est JAMAIS écrit : c'est ce que le lecteur rend quand le fil porte un
/// <c>ev</c> qu'il ne sait pas lire (vocabulaire ouvert — <c>ecriture_ratee</c> est déjà une extension du
/// conseil). Il est conservé, avec le nom brut, pour être compté au diagnostic ; il n'est pas jeté.</para>
/// </summary>
public enum TypeEvenement
{
    /// <summary>Le décorateur de journalisation démarre (porte la <c>version</c> de l'exe : date un changement de comportement).</summary>
    Demarrage,

    /// <summary>Arrêt PROPRE (<c>StopAsync</c>). Absent sur kill ou veille : le lecteur fait alors un trou « Chronos arrêté » sans <c>arret</c>.</summary>
    Arret,

    /// <summary>Transition d'authentification vers <c>Deconnecte</c> : le serveur a refusé les identifiants.</summary>
    JetonInvalide,

    /// <summary>La sonde d'en-têtes a été refusée (saturation ou refus serveur), sur TRANSITION seulement.</summary>
    SondeRefusee,

    /// <summary>Un relevé arrive après un trou de plus de deux cadences : « le trou finit ici ».</summary>
    Reprise,

    /// <summary>Un magasin persistant n'a pas pu écrire (CPT-02) : <c>magasin</c> et <c>cause</c>.</summary>
    EcritureRatee,

    /// <summary>Lu sur le fil sans être reconnu. Conservé avec son nom brut ; jamais écrit.</summary>
    NonReconnu,
}

/// <summary>
/// Un événement du journal, tel qu'écrit et relu. Type NEUTRE (aucun WPF).
/// </summary>
/// <param name="T">Instant de l'événement, UTC. Pour <see cref="TypeEvenement.Reprise"/> : le <c>t</c> du relevé qui met fin au trou.</param>
/// <param name="Type">Nature de l'événement.</param>
/// <param name="Magasin">Pour <see cref="TypeEvenement.EcritureRatee"/> : le magasin en cause (« last-exact », « journal »…).</param>
/// <param name="Cause">Texte libre court : « trou de 25 min », « SaturationEnTetesLus », « IOException : x ».</param>
/// <param name="Version">Pour <see cref="TypeEvenement.Demarrage"/> : version embarquée de l'exe.</param>
/// <param name="EvBrut">Le nom de fil tel qu'il a été LU (renseigné à la relecture ; utile quand <see cref="Type"/> est <see cref="TypeEvenement.NonReconnu"/>).</param>
public sealed record EvenementJournal(
    DateTimeOffset T,
    TypeEvenement Type,
    string? Magasin = null,
    string? Cause = null,
    string? Version = null,
    string? EvBrut = null);

/// <summary>
/// Les noms de fil des événements (valeur du champ <c>ev</c>) : le contrat de données §3, en snake_case,
/// stable indépendamment des noms C#. La garde documentaire de 32-07 les compare à <c>docs/data-sources.md</c>.
/// </summary>
public static class TypeEvenementTexte
{
    /// <summary>Les six noms écrits sur le fil, dans l'ordre du contrat. <see cref="TypeEvenement.NonReconnu"/> n'en a pas.</summary>
    public static readonly string[] NomsDeFil =
        { "demarrage", "arret", "jeton_invalide", "sonde_refusee", "reprise", "ecriture_ratee" };

    /// <summary>Type → nom de fil. <c>null</c> pour <see cref="TypeEvenement.NonReconnu"/> : on n'écrit jamais un inconnu.</summary>
    public static string? Nom(TypeEvenement type) => type switch
    {
        TypeEvenement.Demarrage => "demarrage",
        TypeEvenement.Arret => "arret",
        TypeEvenement.JetonInvalide => "jeton_invalide",
        TypeEvenement.SondeRefusee => "sonde_refusee",
        TypeEvenement.Reprise => "reprise",
        TypeEvenement.EcritureRatee => "ecriture_ratee",
        _ => null,
    };

    /// <summary>Nom de fil → type. Tout ce qui n'est pas reconnu (y compris <c>null</c>) retombe sur
    /// <see cref="TypeEvenement.NonReconnu"/> — conservé, pas jeté.</summary>
    public static TypeEvenement Depuis(string? ev) => ev switch
    {
        "demarrage" => TypeEvenement.Demarrage,
        "arret" => TypeEvenement.Arret,
        "jeton_invalide" => TypeEvenement.JetonInvalide,
        "sonde_refusee" => TypeEvenement.SondeRefusee,
        "reprise" => TypeEvenement.Reprise,
        "ecriture_ratee" => TypeEvenement.EcritureRatee,
        _ => TypeEvenement.NonReconnu,
    };
}
