namespace Chronos.Services;

/// <summary>Issue de la dernière lecture de settings.json (SOC-01).</summary>
public enum IssueLectureReglages
{
    /// <summary>Fichier absent : défauts.</summary>
    Absent,

    /// <summary>Lu sans aucune retombée.</summary>
    Lu,

    /// <summary>Lu, mais au moins une valeur inconnue ou mal typée est retombée sur le défaut de SA propriété.</summary>
    LuAvecRetombees,

    /// <summary>JSON illisible (syntaxe, racine non objet, E/S) : défauts ENTIERS, jamais « à moitié lu ».</summary>
    Illisible,
}

/// <summary>Rapport de la dernière lecture : issue + noms (tels qu'écrits dans le JSON) des valeurs retombées sur leur défaut.
/// Type NEUTRE (aucun type WPF) : la garde de pureté Services/Models reste verte.</summary>
public sealed record LectureReglages(IssueLectureReglages Issue, IReadOnlyList<string> Retombees)
{
    /// <summary>État avant toute lecture.</summary>
    public static readonly LectureReglages Aucune = new(IssueLectureReglages.Absent, Array.Empty<string>());
}
