using System.Collections.Generic;
using System.IO;
using System.Linq;
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
///
/// <para><b>Lire peu.</b> Seuls les <c>local_*.json</c> à la profondeur exacte <c>&lt;org&gt;\&lt;user&gt;</c>, écrits
/// dans la fenêtre de lecture de 24 h, sont ouverts ; et un fichier n'est relu que si sa date d'écriture ou sa taille
/// a changé. Mesuré hors de l'arbre de l'app : 24 ms médian, 68 ms au p90 sans cache ; 0,73 ms avec.</para>
///
/// <para><b>Ne jamais gêner l'écrivain.</b> L'app réécrit chaque fichier par un temporaire puis un renommage (trois
/// essais), et se replie sur une écriture EN PLACE si le renommage échoue. Notre poignée partage lecture, écriture
/// et suppression, et n'est tenue que le temps de COPIER les octets ; l'analyse vient après sa fermeture. Les
/// lectures intégrales du framework, qui ouvrent en partage lecture seule, sont proscrites dans ce fichier : elles
/// feraient échouer le repli en place de l'app.</para>
///
/// <para><b>Concurrence.</b> Le minuteur de l'interface et le rapport de diagnostic appellent <see cref="Lire"/> :
/// un verrou protège le cache et la mémoire des épisodes (coût nul, question fermée).</para>
/// </summary>
public sealed class LecteurAppBureau
{
    /// <summary>Options d'analyse tolérantes : virgule finale et commentaires acceptés.</summary>
    private static readonly JsonDocumentOptions OptionsJson = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Taille au-delà de laquelle un fichier est « illisible » sans être copié : ~275 Ko relevés ; au-delà,
    /// ce n'est plus le format relevé, et une allocation démesurée ne vaut pas un titre.</summary>
    private const long TailleMaximale = 16_777_216;

    private readonly object _verrou = new();

    /// <summary>Par chemin : la clé (date d'écriture UTC, taille) de la dernière lecture RÉUSSIE, et ce qu'elle a donné.
    /// Une relecture illisible ne la remplace pas : la dernière métadonnée valide reste servie, et la clé inchangée
    /// fait retenter le fichier au cycle suivant.</summary>
    private readonly Dictionary<string, (System.DateTime Ecriture, long Taille, IssueLecture Issue, MetadonneesAppBureau? Meta)> _cache
        = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>TRT-02 : par (cliSessionId, postTurnSummaryFor), l'instant de l'épisode lu à sa PREMIÈRE apparition.</summary>
    private readonly Dictionary<(string Id, string Resume), System.DateTimeOffset> _premiereApparition = new();

    /// <summary>La dernière lecture COMPLÈTE, rendue telle quelle si une énumération échoue en cours de route.</summary>
    private LectureAppBureau? _derniere;

    /// <summary>Les racines candidates, dans l'ordre d'essai.</summary>
    public LecteurAppBureau(IReadOnlyList<string> candidats) => Candidats = candidats;

    /// <summary>Les racines candidates, dans l'ordre d'essai.</summary>
    public IReadOnlyList<string> Candidats { get; }

    /// <summary>
    /// Un cycle de lecture, à l'instant injecté <paramref name="now"/>. Jamais d'exception, jamais de lecture partielle.
    /// <list type="number">
    ///   <item>La racine est le premier candidat qui existe ; aucun ⇒ une lecture ABSENTE (tout à zéro, candidats
    ///   listés) et les mémoires vidées.</item>
    ///   <item>L'énumération est MATÉRIALISÉE dans un bloc protégé, à la profondeur exacte deux ; un nom doit commencer
    ///   par <c>local_</c> (les temporaires de l'app commencent par <c>.local_</c>) et finir par <c>.json</c>. Si elle
    ///   échoue, la dernière lecture complète de la même racine est rendue.</item>
    ///   <item>Un fichier écrit hors de la fenêtre de lecture n'est pas ouvert ; un fichier dont la clé (date
    ///   d'écriture, taille) n'a pas changé n'est pas relu. Les chemins disparus ou sortis de la fenêtre quittent le
    ///   cache.</item>
    ///   <item>Deux fichiers pour un même identifiant : le plus récent par dernière activité l'emporte (ex aequo :
    ///   le premier chemin dans l'ordre ordinal), l'autre est compté en doublon.</item>
    ///   <item>L'instant d'une classification est FIGÉ par épisode : la dernière activité lue à la première apparition
    ///   de (identifiant, <c>postTurnSummaryFor</c>). L'activité de fond qui la fait avancer après la fin du tour
    ///   (+3 min 10 s mesurés) ne rajeunit donc pas une question — sinon une session marquée traitée reviendrait sans
    ///   rien avoir redemandé (NET-03). Sans <c>postTurnSummaryFor</c>, la valeur courante. Les épisodes qui ne sont
    ///   plus présents sont oubliés (mémoire bornée).</item>
    /// </list>
    /// <para><b>Limite assumée :</b> la mémoire des épisodes vit dans le processus. Un redémarrage de l'overlay pendant
    /// une activité de fond re-mémorise, une fois, une valeur plus récente que la vraie fin du tour.</para>
    /// </summary>
    public LectureAppBureau Lire(System.DateTimeOffset now)
    {
        lock (_verrou)
        {
            var racine = PremiereRacine();
            if (racine is null)
            {
                _cache.Clear();
                _premiereApparition.Clear();
                return _derniere = Vide(null);
            }

            List<(string Chemin, System.DateTime Ecriture, long Taille)> fichiers;
            try
            {
                fichiers = Enumerer(racine);   // MATÉRIALISÉ ici : une énumération paresseuse lèverait hors du bloc
            }
            catch (System.Exception)
            {
                return _derniere is { } derniere && string.Equals(derniere.Racine, racine, System.StringComparison.OrdinalIgnoreCase)
                    ? derniere
                    : Vide(racine);
            }

            var recents = 0;
            var relus = 0;
            var illisibles = 0;
            var sansId = 0;
            var retenus = new List<(string Chemin, MetadonneesAppBureau Meta)>();
            var vus = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var (chemin, ecriture, taille) in fichiers)
            {
                // Économie de lecture, jamais un horizon d'affichage : l'âge d'un signal se juge sur son instant.
                if (now - new System.DateTimeOffset(ecriture, System.TimeSpan.Zero) > HorizonsSessions.LectureAppBureau)
                    continue;
                recents++;
                vus.Add(chemin);

                var connue = _cache.TryGetValue(chemin, out var entree);
                if (!connue || entree.Ecriture != ecriture || entree.Taille != taille)
                {
                    relus++;
                    var issue = LireEtInterpreter(chemin, out var meta);
                    if (issue == IssueLecture.Illisible)
                    {
                        // Réécriture en cours, fichier supprimé entre-temps… : la dernière lecture valide est gardée
                        // et la clé n'avance pas — le fichier sera retenté au cycle suivant. Aucun titre ne clignote.
                        illisibles++;
                    }
                    else
                    {
                        entree = (ecriture, taille, issue, meta);
                        _cache[chemin] = entree;
                        connue = true;
                    }
                }

                if (!connue) continue;   // première lecture ratée : rien à servir pour ce fichier
                if (entree.Issue == IssueLecture.SansCliSessionId) sansId++;
                else if (entree.Meta is { } retenue) retenus.Add((chemin, retenue));
            }

            foreach (var perime in _cache.Keys.Where(c => !vus.Contains(c)).ToList())
                _cache.Remove(perime);

            // Une session, un fichier : le plus récent par dernière activité ; ex aequo, le premier chemin ordinal.
            var parSession = new Dictionary<string, MetadonneesAppBureau>(System.StringComparer.OrdinalIgnoreCase);
            var doublons = 0;
            foreach (var groupe in retenus.GroupBy(r => r.Meta.CliSessionId, System.StringComparer.OrdinalIgnoreCase))
            {
                var elu = groupe
                    .OrderByDescending(r => r.Meta.DerniereActivite ?? System.DateTimeOffset.MinValue)
                    .ThenBy(r => r.Chemin, System.StringComparer.Ordinal)
                    .First();
                doublons += groupe.Count() - 1;
                parSession[elu.Meta.CliSessionId] = elu.Meta;
            }

            FigerLesEpisodes(parSession);

            var champsAbsents = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var meta in parSession.Values)
                foreach (var champ in meta.ChampsAbsents)
                    champsAbsents[champ] = champsAbsents.TryGetValue(champ, out var n) ? n + 1 : 1;

            var lecture = new LectureAppBureau(
                racine, Candidats,
                fichiers.Count, recents, parSession.Count + doublons, relus, illisibles, sansId, doublons,
                champsAbsents, parSession);
            _derniere = lecture;
            return lecture;
        }
    }

    /// <summary>TRT-02 : remplace l'instant de classification COURANT par celui de la première apparition de
    /// l'épisode, et oublie les épisodes qui ne sont plus présents. Appelé sous le verrou.</summary>
    private void FigerLesEpisodes(Dictionary<string, MetadonneesAppBureau> parSession)
    {
        var presents = new HashSet<(string Id, string Resume)>();
        foreach (var (cle, meta) in parSession.ToList())
        {
            if (meta.ResumePour is not { } resume || meta.InstantClassification is not { } instantLu) continue;

            var episode = (meta.CliSessionId, resume);
            presents.Add(episode);
            if (!_premiereApparition.TryGetValue(episode, out var fige))
            {
                fige = instantLu;
                _premiereApparition[episode] = fige;
            }
            if (fige != instantLu)
                parSession[cle] = meta with { InstantClassification = fige };
        }

        foreach (var oublie in _premiereApparition.Keys.Where(k => !presents.Contains(k)).ToList())
            _premiereApparition.Remove(oublie);
    }

    /// <summary>Le premier candidat qui existe, ou nul.</summary>
    private string? PremiereRacine()
    {
        foreach (var candidat in Candidats)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(candidat) && Directory.Exists(candidat)) return candidat;
            }
            catch (System.Exception) { /* chemin invalide : candidat suivant */ }
        }
        return null;
    }

    /// <summary>Les <c>local_*.json</c> à la profondeur EXACTE <c>&lt;org&gt;\&lt;user&gt;</c>, avec leur clé de cache.
    /// Ni l'index des archives, ni les temporaires d'écriture atomique, ni les traces de suppression, ni un
    /// sous-dossier : jamais un motif récursif.</summary>
    private static List<(string Chemin, System.DateTime Ecriture, long Taille)> Enumerer(string racine)
    {
        var fichiers = new List<(string Chemin, System.DateTime Ecriture, long Taille)>();
        foreach (var org in new DirectoryInfo(racine).EnumerateDirectories())
            foreach (var utilisateur in org.EnumerateDirectories())
                foreach (var fichier in utilisateur.EnumerateFiles("local_*.json"))
                {
                    if (!fichier.Name.StartsWith("local_", System.StringComparison.Ordinal)
                        || !fichier.Name.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    fichiers.Add((fichier.FullName, fichier.LastWriteTimeUtc, fichier.Length));
                }
        return fichiers;
    }

    /// <summary>Une lecture vide de la racine donnée (nulle : source absente).</summary>
    private LectureAppBureau Vide(string? racine)
        => new(racine, Candidats, 0, 0, 0, 0, 0, 0, 0,
               new SortedDictionary<string, int>(System.StringComparer.Ordinal),
               new Dictionary<string, MetadonneesAppBureau>(System.StringComparer.OrdinalIgnoreCase));

    /// <summary>Copie puis interprète ; toute erreur d'accès (fichier supprimé ou renommé entre l'énumération et
    /// l'ouverture, accès refusé, erreur d'entrée-sortie) vaut « illisible ».</summary>
    private static IssueLecture LireEtInterpreter(string chemin, out MetadonneesAppBureau? meta)
    {
        meta = null;
        try
        {
            var octets = LireOctets(chemin);
            return octets is null ? IssueLecture.Illisible : Interpreter(octets, out meta);
        }
        catch (System.Exception)
        {
            meta = null;
            return IssueLecture.Illisible;
        }
    }

    /// <summary>Les octets du fichier, copiés poignée ouverte le moins longtemps possible (0,36 ms médian mesuré pour
    /// 289 Ko) ; la poignée est fermée AVANT toute analyse. Nul au-delà de <see cref="TailleMaximale"/>. Un fichier
    /// raccourci pendant la copie (troncature en cours) rend ce qui a été lu : l'analyse JSON dira non.</summary>
    private static byte[]? LireOctets(string chemin)
    {
        using var flux = Ouvrir(chemin);
        var longueur = flux.Length;
        if (longueur > TailleMaximale) return null;

        var tampon = new byte[longueur];
        var lu = 0;
        while (lu < tampon.Length)
        {
            var n = flux.Read(tampon, lu, tampon.Length - lu);
            if (n == 0) break;
            lu += n;
        }
        return lu == tampon.Length ? tampon : tampon[..lu];
    }

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

    /// <summary>
    /// L'UNIQUE ouverture de fichier du lecteur (APP-05) : mode « ouvrir » (jamais créer), accès en lecture seule,
    /// partage lecture, écriture ET suppression — mesuré en 29-RESEARCH (Q1.c) sur un dossier temporaire :
    /// <list type="bullet">
    ///   <item>le partage en ÉCRITURE est obligatoire : sans lui, notre poignée ferait échouer le repli de l'app, qui
    ///   rouvre le fichier pour l'écrire en place quand son renommage a échoué ;</item>
    ///   <item>le partage en SUPPRESSION ne coûte rien et laisse passer un renommage à la sémantique POSIX ou une
    ///   suppression (l'app supprime des sessions) ;</item>
    ///   <item>aucune combinaison de partage n'empêche en revanche notre poignée de faire échouer le renommage de l'app
    ///   (il remplace une cible ouverte) : elle n'est donc tenue que le temps de COPIER les octets, jamais d'analyser.</item>
    /// </list>
    /// Tamponnage désactivé (taille 1) : les octets vont droit dans le tampon de copie.
    /// </summary>
    internal static FileStream Ouvrir(string chemin)
        => new(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.SequentialScan);

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
