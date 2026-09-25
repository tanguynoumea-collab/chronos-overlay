using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>Classification de fin de tour faite PAR L'APP (postTurnSummary.status_category) : un indice transitoire
/// et daté, jamais un état. L'app l'efface elle-même quand un tour démarre ou qu'un message utilisateur arrive.</summary>
public enum ClassificationFinDeTour
{
    /// <summary>Aucun résumé de fin de tour (cas majoritaire), ou une catégorie que le disque n'a jamais montrée.</summary>
    Inconnue,

    /// <summary><c>completed</c> : le tour s'est terminé sans rien demander.</summary>
    Terminee,

    /// <summary><c>blocked</c> : une question est posée à l'utilisateur (son texte est le motif).</summary>
    Bloquee,

    /// <summary><c>review_ready</c> : un travail est prêt à être relu.</summary>
    PreteARevue,
}

/// <summary>Ce qu'a donné la lecture d'UN fichier.</summary>
internal enum IssueLecture
{
    /// <summary>Objet JSON lisible portant un <c>cliSessionId</c> : des métadonnées joignables.</summary>
    Valide,

    /// <summary>Objet JSON lisible, mais sans <c>cliSessionId</c> : aucune jointure possible (23 fichiers sur 138
    /// relevés). Compté au diagnostic, jamais une erreur.</summary>
    SansCliSessionId,

    /// <summary>Contenu invalide, tronqué (réécriture en place en cours) ou d'une autre forme qu'un objet.</summary>
    Illisible,
}

/// <summary>
/// Les métadonnées d'UNE session, telles que l'app bureau les a écrites. Type NEUTRE : aucun type WPF.
///
/// <para>Chaque champ absent ou d'un autre type que celui relevé vaut <c>null</c> (ou faux pour l'archivage) et son
/// nom JSON figure dans <see cref="ChampsAbsents"/>. L'absence de résumé de fin de tour n'y figure PAS : elle est
/// normale (le résumé est transitoire, absent de la plupart des sessions).</para>
/// </summary>
/// <param name="CliSessionId">L'identifiant de session de Claude Code (celui des hooks et des transcripts) : la clé de jointure.</param>
/// <param name="Titre">Le titre lisible de la session (<c>title</c>).</param>
/// <param name="SourceDuTitre">D'où vient le titre (<c>titleSource</c>, « auto » relevé).</param>
/// <param name="Dossier">Le dossier de travail (<c>cwd</c>).</param>
/// <param name="Creation">Création de la session (<c>createdAt</c>).</param>
/// <param name="DernierFocus">Dernier instant où la session a été SÉLECTIONNÉE dans l'app (<c>lastFocusedAt</c>).</param>
/// <param name="DerniereActivite">Dernière activité (<c>lastActivityAt</c>) — avance aussi après la fin du tour.</param>
/// <param name="DernierMessageUtilisateur">Dernier message de l'utilisateur (<c>latestUserFrameAt</c>).</param>
/// <param name="ToursTermines">Nombre de tours terminés (<c>completedTurns</c>).</param>
/// <param name="Archivee">Session archivée dans l'app (<c>isArchived</c>).</param>
/// <param name="ClassificationFinDeTour">La classification de fin de tour, si un résumé est présent.</param>
/// <param name="CategorieBrute">La catégorie telle que l'app l'a écrite, pour le diagnostic.</param>
/// <param name="MotifBlocage">Ce que l'utilisateur doit faire (<c>needs_action</c>), nul s'il est vide.</param>
/// <param name="ResumePour">L'identité de l'épisode résumé (<c>postTurnSummaryFor</c>).</param>
/// <param name="InstantClassification">L'instant de la classification : figé par épisode par <see cref="LecteurAppBureau.Lire"/>.</param>
/// <param name="ChampsAbsents">Les champs lus absents ou d'un autre type, dans l'ordre de la liste lue.</param>
public sealed record MetadonneesAppBureau(
    string CliSessionId, string? Titre, string? SourceDuTitre, string? Dossier,
    System.DateTimeOffset? Creation, System.DateTimeOffset? DernierFocus, System.DateTimeOffset? DerniereActivite,
    System.DateTimeOffset? DernierMessageUtilisateur, int? ToursTermines, bool Archivee,
    ClassificationFinDeTour ClassificationFinDeTour, string? CategorieBrute, string? MotifBlocage,
    string? ResumePour, System.DateTimeOffset? InstantClassification, IReadOnlyList<string> ChampsAbsents);

/// <summary>
/// Le résultat d'UN cycle de lecture : ce qui a été cherché, trouvé, lu, écarté — de quoi écrire le diagnostic
/// sans jamais taire une source absente.
/// </summary>
/// <param name="Racine">Le premier candidat qui existe ; nul si aucun (source ABSENTE, jamais omise).</param>
/// <param name="RacinesCherchees">Tous les candidats, dans l'ordre où ils ont été essayés.</param>
/// <param name="Enumeres">Fichiers <c>local_*.json</c> trouvés à la profondeur exacte de l'app.</param>
/// <param name="Recents">Parmi eux, ceux écrits dans la fenêtre de lecture.</param>
/// <param name="Valides">Fichiers retenus avec un <c>cliSessionId</c> (sessions distinctes plus doublons).</param>
/// <param name="RelusSurDisque">Fichiers réellement relus CE cycle (les autres viennent du cache).</param>
/// <param name="Illisibles">Relectures ratées CE cycle (la dernière lecture valide est gardée).</param>
/// <param name="SansCliSessionId">Fichiers de la fenêtre sans identifiant de jointure.</param>
/// <param name="Doublons">Fichiers écartés parce qu'un autre, plus récent, porte le même identifiant.</param>
/// <param name="ChampsAbsents">Par nom de champ, le nombre de sessions retenues où il manque (clés triées).</param>
/// <param name="ParSession">Les métadonnées retenues, par <c>cliSessionId</c> (comparaison sans casse).</param>
public sealed record LectureAppBureau(
    string? Racine, IReadOnlyList<string> RacinesCherchees,
    int Enumeres, int Recents, int Valides, int RelusSurDisque, int Illisibles, int SansCliSessionId, int Doublons,
    IReadOnlyDictionary<string, int> ChampsAbsents,
    IReadOnlyDictionary<string, MetadonneesAppBureau> ParSession)
{
    /// <summary>Vrai si l'un des candidats existe.</summary>
    public bool DossierTrouve => Racine is not null;
}

/// <summary>
/// La source app-bureau : un LECTEUR PUR, en lecture seule stricte, des métadonnées par session que l'app bureau
/// Claude écrit (APP-01, APP-05). Il ne décide rien : il dit ce que l'app a écrit, et se tait sur ce qu'il ne sait
/// pas lire (« absent », « inconnue », « illisible ») — sans jamais lever.
///
/// <para><b>Format interne non documenté.</b> Seuls sont lus <c>cliSessionId</c>, <c>title</c>, <c>titleSource</c>,
/// <c>cwd</c>, <c>createdAt</c>, <c>lastFocusedAt</c>, <c>lastActivityAt</c>, <c>latestUserFrameAt</c>,
/// <c>completedTurns</c>, <c>isArchived</c>, <c>postTurnSummary.status_category</c>, <c>postTurnSummary.needs_action</c>
/// et <c>postTurnSummaryFor</c> (la clé d'épisode). Tout autre champ est ignoré. Les horodatages sont des epochs en
/// millisecondes, convertis par le point unique <see cref="UsageNormalization"/>.</para>
///
/// <para><b>Les racines sont INJECTÉES.</b> Le lecteur ne connaît aucun chemin de l'app : il reçoit ses racines
/// candidates, dans l'ordre (en production, celles que résout la composition ; en test, des dossiers temporaires).</para>
/// </summary>
public sealed class LecteurAppBureau
{
    /// <summary>Options d'analyse tolérantes : virgule finale et commentaires acceptés.</summary>
    private static readonly JsonDocumentOptions OptionsJson = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Les racines candidates, dans l'ordre d'essai.</summary>
    public LecteurAppBureau(IReadOnlyList<string> candidats) => Candidats = candidats;

    /// <summary>Les racines candidates, dans l'ordre d'essai.</summary>
    public IReadOnlyList<string> Candidats { get; }

    /// <summary>Un cycle de lecture (tâche 2).</summary>
    public LectureAppBureau Lire(System.DateTimeOffset now) => throw new System.NotImplementedException();

    /// <summary>
    /// Interprétation PURE d'un fichier (prouvée sur les six fixtures réelles). Une marque d'ordre d'octets UTF-8 en
    /// tête est ignorée ; un contenu qui n'est pas un objet JSON est <see cref="IssueLecture.Illisible"/> ; un objet
    /// sans <c>cliSessionId</c> texte non vide est <see cref="IssueLecture.SansCliSessionId"/>. Jamais d'exception.
    ///
    /// <para><see cref="MetadonneesAppBureau.InstantClassification"/> vaut ici la dernière activité COURANTE si un
    /// résumé est présent ; son figement par épisode est l'affaire de <see cref="Lire"/>, qui seul a une mémoire.</para>
    /// </summary>
    internal static IssueLecture Interpreter(System.ReadOnlyMemory<byte> json, out MetadonneesAppBureau? meta)
    {
        meta = null;

        var octets = json;
        var debut = octets.Span;
        if (debut.Length >= 3 && debut[0] == 0xEF && debut[1] == 0xBB && debut[2] == 0xBF)
            octets = octets[3..];

        try
        {
            using var document = JsonDocument.Parse(octets, OptionsJson);
            var racine = document.RootElement;
            if (racine.ValueKind != JsonValueKind.Object) return IssueLecture.Illisible;

            var id = Texte(racine, "cliSessionId", absents: null);
            if (id is null) return IssueLecture.SansCliSessionId;

            // L'ordre de lecture est celui de la liste verrouillée : ChampsAbsents le reprend tel quel.
            var absents = new List<string>();
            var titre = Texte(racine, "title", absents);
            var sourceDuTitre = Texte(racine, "titleSource", absents);
            var dossier = Texte(racine, "cwd", absents);
            var creation = Instant(racine, "createdAt", absents);
            var dernierFocus = Instant(racine, "lastFocusedAt", absents);
            var derniereActivite = Instant(racine, "lastActivityAt", absents);
            var dernierMessage = Instant(racine, "latestUserFrameAt", absents);
            var tours = Entier(racine, "completedTurns", absents);
            var archivee = Booleen(racine, "isArchived", absents);

            // Le résumé de fin de tour est TRANSITOIRE : son absence est normale et ne se compte pas.
            var classification = ClassificationFinDeTour.Inconnue;
            string? categorie = null;
            string? motif = null;
            System.DateTimeOffset? instantClassification = null;
            if (racine.TryGetProperty("postTurnSummary", out var resume) && resume.ValueKind == JsonValueKind.Object)
            {
                categorie = Texte(resume, "status_category", absents: null);
                // Comparaison ORDINALE, sur les seules catégories relevées sur le disque. Toute autre (need_input,
                // failed… connues du code de l'app, jamais vues) reste inconnue : aucune question n'est inventée.
                classification = categorie switch
                {
                    "completed" => ClassificationFinDeTour.Terminee,
                    "blocked" => ClassificationFinDeTour.Bloquee,
                    "review_ready" => ClassificationFinDeTour.PreteARevue,
                    _ => ClassificationFinDeTour.Inconnue,
                };
                motif = Texte(resume, "needs_action", absents: null);
                instantClassification = derniereActivite;
            }
            var resumePour = Texte(racine, "postTurnSummaryFor", absents: null);

            meta = new MetadonneesAppBureau(
                id, titre, sourceDuTitre, dossier,
                creation, dernierFocus, derniereActivite, dernierMessage, tours, archivee,
                classification, categorie, motif, resumePour, instantClassification,
                absents.Count == 0 ? System.Array.Empty<string>() : absents.ToArray());
            return IssueLecture.Valide;
        }
        catch (System.Exception e) when (e is JsonException or System.InvalidOperationException or System.ArgumentException)
        {
            // JSON invalide ou tronqué, texte UTF-8 invalide : illisible, jamais une exception.
            meta = null;
            return IssueLecture.Illisible;
        }
    }

    /// <summary>Ouverture en lecture seule (tâche 3).</summary>
    internal static FileStream Ouvrir(string chemin) => throw new System.NotImplementedException();

    // ------------------------------------------------------------------ lecture tolérante d'un champ

    /// <summary>Un texte rogné ; vide ⇒ nul. Absent ou d'un autre type ⇒ nul ET compté absent.</summary>
    private static string? Texte(JsonElement objet, string champ, List<string>? absents)
    {
        if (objet.TryGetProperty(champ, out var v) && v.ValueKind == JsonValueKind.String)
        {
            var texte = v.GetString()?.Trim();
            return string.IsNullOrEmpty(texte) ? null : texte;
        }
        absents?.Add(champ);
        return null;
    }

    /// <summary>Un epoch en millisecondes, converti par le point unique. Absent, d'un autre type ou hors du
    /// plancher de sanité ⇒ nul ET compté absent (une valeur qu'on ne sait pas interpréter n'est pas une valeur).</summary>
    private static System.DateTimeOffset? Instant(JsonElement objet, string champ, List<string> absents)
    {
        if (objet.TryGetProperty(champ, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var ms)
            && UsageNormalization.InstantDepuisEpochMillisecondes(ms) is { } instant)
            return instant;
        absents.Add(champ);
        return null;
    }

    /// <summary>Un entier ; absent ou d'un autre type ⇒ nul ET compté absent.</summary>
    private static int? Entier(JsonElement objet, string champ, List<string> absents)
    {
        if (objet.TryGetProperty(champ, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n))
            return n;
        absents.Add(champ);
        return null;
    }

    /// <summary>Un booléen ; absent ou d'un autre type ⇒ faux ET compté absent.</summary>
    private static bool Booleen(JsonElement objet, string champ, List<string> absents)
    {
        if (objet.TryGetProperty(champ, out var v))
        {
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
        }
        absents.Add(champ);
        return false;
    }
}
