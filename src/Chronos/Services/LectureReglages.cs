namespace Chronos.Services;

/// <summary>Issue de la dernière lecture de settings.json (SOC-01, MAT-3).</summary>
public enum IssueLectureReglages
{
    /// <summary>Fichier absent : défauts.</summary>
    Absent,

    /// <summary>Lu sans aucune retombée.</summary>
    Lu,

    /// <summary>Lu, mais au moins une valeur inconnue ou mal typée est retombée sur le défaut de SA propriété.</summary>
    LuAvecRetombees,

    /// <summary>Contenu inexploitable (syntaxe, racine non objet, fichier vide) : défauts ENTIERS, jamais « à moitié lu ».
    /// L'original est mis en QUARANTAINE (renommé, jamais supprimé) avant toute réécriture (42.2-02, MAT-3).</summary>
    Illisible,

    /// <summary>E/S passagère (verrou, droits, disque) après reprises bornées : défauts en mémoire, AUCUNE écriture et
    /// AUCUNE quarantaine — réessai au passage suivant (42.2-02, FIAB-3).</summary>
    Inaccessible,
}

/// <summary>Rapport de la dernière lecture : issue + noms (tels qu'écrits dans le JSON) des valeurs retombées sur leur défaut,
/// et la cause (« Type : message ») d'une lecture non aboutie.
/// Type NEUTRE (aucun type WPF) : la garde de pureté Services/Models reste verte.</summary>
public sealed record LectureReglages(IssueLectureReglages Issue, IReadOnlyList<string> Retombees, string? Cause = null)
{
    /// <summary>État avant toute lecture.</summary>
    public static readonly LectureReglages Aucune = new(IssueLectureReglages.Absent, Array.Empty<string>());

    /// <summary>Vrai si la lecture reflète fidèlement le disque (absent ou lu) : une écriture peut avoir lieu sans rien perdre.</summary>
    public bool EstFiable => Issue is IssueLectureReglages.Absent or IssueLectureReglages.Lu or IssueLectureReglages.LuAvecRetombees;
}
