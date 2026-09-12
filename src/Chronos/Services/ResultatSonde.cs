namespace Chronos.Services;

/// <summary>
/// Issue du DERNIER passage de la sonde d'en-têtes de limite (observabilité HDR-01 / HDR-02 / HDR-06).
///
/// AUCUN membre porteur de chaîne, par construction : un corps de réponse d'erreur ne doit JAMAIS
/// pouvoir remonter jusqu'au rapport de diagnostic. Précédent verrouillé en phase 17 par
/// <c>ResultatRafraichissement</c>, dont un test réflexif interdit toute propriété <c>string</c> — la
/// même discipline s'applique ici : un vocabulaire fermé de onze faits, et rien d'autre.
///
/// Le grain est volontairement FIN : l'échec silencieux que v1.5 corrige venait précisément de
/// l'écrasement de plusieurs causes distinctes en un seul « pas de données ». « 429 porteur des
/// chiffres » et « 429 muet » ne sont pas la même information ; « modèle refusé » et « panne réseau »
/// n'appellent ni le même recul ni le même message.
/// </summary>
public enum ResultatSonde
{
    /// <summary>Aucun passage depuis le démarrage de l'exe. État initial, pas une panne.</summary>
    JamaisSondee,

    /// <summary>Interrupteur de réglage à faux : zéro accès réseau, par choix de l'utilisateur.</summary>
    Desactivee,

    /// <summary>Coffre vide : rien à présenter. Ce n'est pas une panne, c'est une absence de login.</summary>
    PasDeJeton,

    /// <summary>Cadence bornée (HDR-06) : refus LOCAL, aucune requête émise. Le quota n'est pas dépensé.</summary>
    FreinActif,

    /// <summary>2xx et famille d'en-têtes unifiée reconnue : le cas nominal.</summary>
    SuccesEnTetesLus,

    /// <summary>2xx SANS la famille unifiée : signal que les noms d'en-têtes ont changé côté serveur.
    /// À dire explicitement dans le diagnostic, jamais à confondre avec « pas de données ».</summary>
    SuccesSansEnTetes,

    /// <summary>429 PORTEUR des chiffres : la raison d'être de la phase (HDR-02). Refusé ne veut pas
    /// dire aveugle — les en-têtes survivent au refus.</summary>
    SaturationEnTetesLus,

    /// <summary>429 muet (voie de facturation, plafond de dépense) : ne rien inventer.</summary>
    SaturationSansEnTetes,

    /// <summary>401 après rejeu unique, ou 403 : le serveur refuse les identifiants. Dégrader ;
    /// aucun en-tête de limite n'est à espérer.</summary>
    RefusServeur,

    /// <summary>400 / 404 : panne de CONFIGURATION (identifiant de modèle périmé), pas de quota.
    /// La distinguer évite de rejouer la panne silencieuse sous un autre nom.</summary>
    ModeleRefuse,

    /// <summary>Réseau, délai dépassé, ou 5xx : indisponibilité transitoire.</summary>
    PanneReseau,
}
