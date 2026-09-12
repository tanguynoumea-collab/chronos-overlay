namespace Chronos.Models;

/// <summary>
/// Provenance FINE du chiffre affiché (phase 19). Complète <see cref="SourceReliability"/>, qui répond
/// « peut-on s'y fier », par « d'où vient-il et qu'a-t-on vérifié ». <c>null</c> = la doctrine n'a pas
/// statué : fenêtre indisponible, ou WindowState fabriqué hors doctrine (provider, magasin, test).
///
/// La phase 20 (EXA-03) bindera CES trois valeurs pour la distinction visuelle. Aucune géométrie de
/// cadran n'en dépend en phase 19 : le contrat est gravé avant que le dessin ne bouge.
/// </summary>
public enum ProvenanceReleve
{
    /// <summary>Âge sous la limite : exact, affiché tel quel, aucune E/S consultée pour le certifier.</summary>
    Frais,

    /// <summary>Plus vieux que la limite, MAIS on a prouvé qu'aucune réponse assistant n'est survenue
    /// depuis sa capture : l'utilisation n'a objectivement pas bougé, il est ENCORE exact (DEL-03).</summary>
    EncoreValide,

    /// <summary>De l'activité est survenue depuis la capture : le chiffre devient une BORNE INFÉRIEURE
    /// (« au moins X »), pas une valeur. Jamais gonflé, jamais confondu avec un exact (DEL-04).</summary>
    PlancherAvecActivite,
}
