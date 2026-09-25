using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// LIB-02 — une question n'est pas une réflexion.
///
/// <para>Avant la phase 28, un message assistant qui portait un <c>tool_use</c> était lu « travail en cours »,
/// quel que soit l'outil. Or <c>AskUserQuestion</c> est un outil qui ATTEND l'utilisateur : la session
/// s'affichait « en cours » pendant qu'elle lui posait une question. Règle verrouillée (28-CONTEXT.md) : le
/// dernier <c>tool_use</c> de la dernière ligne assistant s'appelle EXACTEMENT <c>AskUserQuestion</c>, et
/// aucune ligne user ne suit ⇒ attente, au rang des questions. Mesurée sur 222 questions réelles répondues
/// (28-RESEARCH.md, Q3) : 221 reconnues, aucune après la réponse.</para>
///
/// <para>Les deux premiers cas tournent sur des lignes RÉELLES (<c>TestData/TranscriptQuestion/</c>, relevé
/// du 2026-09-25, session du 2026-09-24), réduites et anonymisées, copiées dans une racine TEMPORAIRE : aucun
/// test ne lit le vrai <c>~/.claude/projects</c>. Aucun ne demande l'heure : <c>now</c> est injecté.</para>
/// </summary>
public class TranscriptQuestionTests : IDisposable
{
    /// <summary>L'instant de référence des cas écrits en ligne. Fixe : jamais <c>DateTimeOffset.UtcNow</c>.</summary>
    private static readonly DateTimeOffset T = new(2026, 9, 25, 15, 40, 0, TimeSpan.Zero);

    // Les instants RÉELS de la fixture (Parse est permis dans tests/, pas dans Services/).
    private static readonly DateTimeOffset tQuestion = DateTimeOffset.Parse("2026-09-24T06:55:33.967Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset tReponse = DateTimeOffset.Parse("2026-09-24T06:59:44.051Z", CultureInfo.InvariantCulture);

    private const string SessionReelle = "939eb30a-8200-4c6e-b89f-49ef26260e92";

    private readonly List<string> _racines = new();

    public void Dispose()
    {
        foreach (var r in _racines)
            try { Directory.Delete(r, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private string TempRoot()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-question-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // aucun test n'écrit hors du dossier temporaire
        _racines.Add(d);
        return d;
    }

    private static string TestDataPath(string file, [CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "TestData", "TranscriptQuestion", file);

    // Copie la fixture réelle dans une racine temporaire, là où la source cherche les transcripts, et pose la
    // date d'écriture voulue (c'est elle que le pré-filtre de la source regarde).
    private string CopierFixture(string nom, DateTimeOffset ecriture)
    {
        var racine = TempRoot();
        var dossier = Path.Combine(racine, "C--Projets-Projet-J");
        Directory.CreateDirectory(dossier);
        var f = Path.Combine(dossier, SessionReelle + ".jsonl");
        File.Copy(TestDataPath(nom), f);
        File.SetLastWriteTimeUtc(f, ecriture.UtcDateTime);
        return racine;
    }

    private static void Ecrire(string racine, string id, DateTimeOffset ecriture, params string[] lignes)
    {
        var dossier = Path.Combine(racine, "C--Projets-Projet-Q");
        Directory.CreateDirectory(dossier);
        var f = Path.Combine(dossier, id + ".jsonl");
        File.WriteAllText(f, string.Join("\n", lignes) + "\n");
        File.SetLastWriteTimeUtc(f, ecriture.UtcDateTime);
    }

    private static string Iso(DateTimeOffset t)
        => t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    // Une invite utilisateur (ligne user, bloc texte).
    private static string Invite(DateTimeOffset t) => $$$"""
        {"type":"user","isSidechain":false,"timestamp":"{{{Iso(t)}}}","cwd":"C:\\Projets\\Projet-Q","message":{"role":"user","content":[{"type":"text","text":"une invite"}]}}
        """;

    // Une ligne assistant dont l'UNIQUE bloc est un tool_use portant ce nom (Claude Code écrit un bloc par ligne).
    private static string Outil(string nom, DateTimeOffset t) => $$$"""
        {"type":"assistant","isSidechain":false,"timestamp":"{{{Iso(t)}}}","cwd":"C:\\Projets\\Projet-Q","message":{"role":"assistant","stop_reason":"tool_use","content":[{"type":"tool_use","id":"toolu_x","name":"{{{nom}}}","input":{}}]}}
        """;

    // --- LIB-02 sur les lignes réelles -------------------------------------------------------------------

    [Fact]
    public void Une_question_sans_reponse_est_une_attente_au_rang_des_questions()
    {
        var racine = CopierFixture("question-en-suspens.jsonl", ecriture: tQuestion.AddSeconds(90));

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(tQuestion.AddMinutes(2)));

        Assert.Equal(SessionReelle, s.SessionId);
        Assert.Equal("Projet-J", s.Project);
        Assert.Equal(SessionActivity.WaitingAttention, s.Activity);   // et non Working : elle attend l'utilisateur
        Assert.Equal("AskUserQuestion", s.Reason);                    // le motif est un FAIT observé
    }

    [Fact]
    public void La_reponse_ecrite_rend_la_reflexion()
    {
        var racine = CopierFixture("question-repondue.jsonl", ecriture: tReponse.AddSeconds(10));

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(tReponse.AddMinutes(1)));

        Assert.Equal(SessionActivity.Working, s.Activity);
        Assert.Null(s.Reason);
    }

    /// <summary>D-28-01 sur les lignes réelles : les cinq lignes de métadonnées SANS horodatage écrites pendant
    /// que la question attend font avancer la date d'écriture du fichier, pas l'instant du signal. Daté par
    /// l'écriture, l'épisode d'attente avancerait à chaque ligne et une question marquée traitée ressortirait
    /// sans rien avoir redemandé (NET-03 dévoyé).</summary>
    [Fact]
    public void Les_metadonnees_ecrites_pendant_la_question_ne_la_rajeunissent_pas()
    {
        var racine = CopierFixture("question-en-suspens.jsonl", ecriture: tQuestion.AddSeconds(210));

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(tQuestion.AddMinutes(4)));

        Assert.Equal(SessionActivity.WaitingAttention, s.Activity);
        Assert.Equal(tQuestion, s.UpdatedAt);   // l'instant de la QUESTION, et non l'écriture (question + 3 min 30 s)
    }

    // --- La règle, et ses limites écrites -----------------------------------------------------------------

    /// <summary>Comparaison ORDINALE et EXACTE : ni la casse, ni un préfixe, ni un suffixe ne font une question.
    /// Aucune liste extensible d'outils « au cas où » (décision verrouillée).</summary>
    [Theory]
    [InlineData("askuserquestion")]
    [InlineData("AskUserQuestion2")]
    [InlineData("AskUser")]
    public void Le_nom_d_outil_se_compare_exactement(string nom)
    {
        var racine = TempRoot();
        Ecrire(racine, "s-nom", T.AddSeconds(-30), Invite(T.AddMinutes(-2)), Outil(nom, T.AddMinutes(-1)));

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(T));

        Assert.Equal(SessionActivity.Working, s.Activity);
        Assert.Null(s.Reason);
    }

    /// <summary>Le cas MESURÉ 1 sur 222 : un appel d'outils parallèle dont la question n'est pas le dernier bloc.
    /// Claude Code écrit un bloc par ligne, la dernière ligne porte l'autre outil. La règle verrouillée ne le
    /// reconnaît pas et c'est VOULU : il se dégrade vers le comportement v1.6 (« Réflexion »), jamais vers une
    /// attente inventée. Écrit et testé, pas corrigé par un suivi d'identifiants.</summary>
    [Fact]
    public void Un_appel_parallele_retombe_sur_le_comportement_v16()
    {
        var racine = TempRoot();
        Ecrire(racine, "s-parallele", T.AddSeconds(-30),
               Invite(T.AddMinutes(-2)),
               Outil("AskUserQuestion", T.AddMinutes(-1)),
               Outil("Bash", T.AddMinutes(-1).AddMilliseconds(5)));

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(T));

        Assert.Equal(SessionActivity.Working, s.Activity);
        Assert.Null(s.Reason);
    }

    [Fact]
    public void Un_message_utilisateur_apres_la_question_rend_la_reflexion()
    {
        var racine = TempRoot();
        Ecrire(racine, "s-reprise", T.AddSeconds(-30),
               Invite(T.AddMinutes(-3)),
               Outil("AskUserQuestion", T.AddMinutes(-2)),
               Invite(T.AddMinutes(-1)));

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(T));

        Assert.Equal(SessionActivity.Working, s.Activity);
        Assert.Null(s.Reason);
    }

    // --- Piège 3 : le battement PreToolUse d'une question (sessions à hooks) --------------------------------

    private static SignalSession Signal(SourceSession source, SessionActivity a, string? motif, DateTimeOffset maj)
        => new(source, new SessionSnapshot("s-q", "Projet-Q", a, motif, maj));

    /// <summary>
    /// LIMITE du Piège 3, écrite et NON corrigée. <c>AskUserQuestion</c> étant un outil, <c>PreToolUse</c> part
    /// et le fichier d'état porte un battement <c>Working</c> PLUS RÉCENT que la ligne de question : si rien
    /// d'autre ne suit, l'arbitrage par fraîcheur retient « Réflexion ».
    /// <para>Fait externe MEDIUM (source tierce du 2026-09-23) : <c>PermissionRequest</c> partirait aussi pour
    /// <c>AskUserQuestion</c> — non tranché dans cette phase ; la vérification in vivo, en lecture seule, est
    /// reportée à la phase 31. Les hooks ne lisent pas <c>tool_name</c> (§2 du contrat, « Jamais lus,
    /// délibérément ») : ce test ne propose pas de le faire.</para>
    /// </summary>
    [Fact]
    public void Sans_PermissionRequest_le_battement_d_une_question_gagne_par_fraicheur()
    {
        var t0 = T.AddMinutes(-1);

        var r = ArbitrageSessions.Trancher(new[]
        {
            Signal(SourceSession.Transcript, SessionActivity.WaitingAttention, "AskUserQuestion", t0),
            Signal(SourceSession.Hook, SessionActivity.Working, "PreToolUse", t0.AddMilliseconds(400)),
        });

        Assert.Equal(SessionActivity.Working, Assert.Single(r.Retenus).Activity);
    }

    /// <summary>
    /// Si <c>PermissionRequest</c> part pour <c>AskUserQuestion</c> (fait externe MEDIUM, source tierce du
    /// 2026-09-23, non tranché dans cette phase), le fichier d'état porte une attente plus récente que le
    /// battement : la question reste une attente. Vérification in vivo, en lecture seule, reportée à la
    /// phase 31. Les hooks ne lisent pas <c>tool_name</c> (§2 du contrat).
    /// </summary>
    [Fact]
    public void Avec_PermissionRequest_la_question_reste_une_attente()
    {
        var t0 = T.AddMinutes(-1);

        var r = ArbitrageSessions.Trancher(new[]
        {
            Signal(SourceSession.Transcript, SessionActivity.WaitingAttention, "AskUserQuestion", t0),
            Signal(SourceSession.Hook, SessionActivity.WaitingAttention, "PermissionRequest", t0.AddMilliseconds(900)),
        });

        Assert.Equal(SessionActivity.WaitingAttention, Assert.Single(r.Retenus).Activity);
    }
}
