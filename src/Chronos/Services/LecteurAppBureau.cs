using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>Classification de fin de tour faite PAR L'APP (postTurnSummary.status_category) : un indice transitoire
/// et daté, jamais un état.</summary>
public enum ClassificationFinDeTour { Inconnue, Terminee, Bloquee, PreteARevue }

/// <summary>Ce que la lecture d'UN fichier a donné.</summary>
internal enum IssueLecture { Valide, SansCliSessionId, Illisible }

/// <summary>Les métadonnées d'UNE session, telles que l'app bureau les a écrites.</summary>
public sealed record MetadonneesAppBureau(
    string CliSessionId, string? Titre, string? SourceDuTitre, string? Dossier,
    System.DateTimeOffset? Creation, System.DateTimeOffset? DernierFocus, System.DateTimeOffset? DerniereActivite,
    System.DateTimeOffset? DernierMessageUtilisateur, int? ToursTermines, bool Archivee,
    ClassificationFinDeTour ClassificationFinDeTour, string? CategorieBrute, string? MotifBlocage,
    string? ResumePour, System.DateTimeOffset? InstantClassification, IReadOnlyList<string> ChampsAbsents);

/// <summary>Le résultat d'un cycle de lecture.</summary>
public sealed record LectureAppBureau(
    string? Racine, IReadOnlyList<string> RacinesCherchees,
    int Enumeres, int Recents, int Valides, int RelusSurDisque, int Illisibles, int SansCliSessionId, int Doublons,
    IReadOnlyDictionary<string, int> ChampsAbsents,
    IReadOnlyDictionary<string, MetadonneesAppBureau> ParSession)
{
    public bool DossierTrouve => Racine is not null;
}

/// <summary>SQUELETTE (tâche 1, phase RED).</summary>
public sealed class LecteurAppBureau
{
    public LecteurAppBureau(IReadOnlyList<string> candidats) => Candidats = candidats;

    public IReadOnlyList<string> Candidats { get; }

    public LectureAppBureau Lire(System.DateTimeOffset now) => throw new System.NotImplementedException();

    internal static IssueLecture Interpreter(System.ReadOnlyMemory<byte> json, out MetadonneesAppBureau? meta)
        => throw new System.NotImplementedException();

    internal static FileStream Ouvrir(string chemin) => throw new System.NotImplementedException();
}
