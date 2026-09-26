using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// SUB-01 (phase 30.1), versant TRANSCRIPTS — un sous-agent qui écrit est un travail de sa session.
///
/// <para>Écart E2 du constat de phase 31 (2026-09-26, 14:44 locale) : la session 88677186 avait fini son tour
/// (<c>end_turn</c> 12:33:21.818Z, juste après avoir lancé un agent en arrière-plan) ; l'agent écrivait encore
/// son transcript <c>subagents/agent-a7df37762e5ce2df4.jsonl</c> à 12:45:09.853Z. La source écartait ce
/// fichier par son chemin (SRC-03, phase 21) et ne le lisait jamais : la session restait « En attente ».
/// Décision D-30.1-05 : APRÈS la classification du parent, le dernier message des <c>subagents/agent-*.jsonl</c>
/// de la session, s'il est postérieur à celui du parent, la rend <c>Working</c>, datée de cet instant — sans
/// jamais faire d'un sous-agent une LIGNE.</para>
///
/// <para>Les cas tournent sur des lignes RÉELLES (<c>TestData/SousAgentsArrierePlan/</c>, relevé du
/// 2026-09-26), réduites et anonymisées, copiées dans une racine TEMPORAIRE : aucun test ne lit le vrai
/// <c>~/.claude/projects</c>. Aucun ne demande l'heure : <c>now</c> est injecté.</para>
/// </summary>
public sealed class TranscriptTravailSousAgentTests : IDisposable
{
    private const string Session = "88677186-8690-44dc-ac77-7a319a0aa5cb";
    private const string Orphelin = "ffffffff-0000-0000-0000-000000000000";
    private const string DossierProjet = "C--Projets-Projet-A";
    private const string AgentEnCours = "agent-a7df37762e5ce2df4.jsonl";
    private const string AgentTermine = "agent-af0db524b633162f3.jsonl";

    // Les instants RÉELS du relevé (UTC ; Parse est permis dans tests/, pas dans Services/).
    private static DateTimeOffset U(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static readonly DateTimeOffset FinParent = U("2026-09-26T12:33:21.818Z");
    private static readonly DateTimeOffset EcritureParent = U("2026-09-26T12:33:23.541Z");
    private static readonly DateTimeOffset DernierSousAgent = U("2026-09-26T12:45:09.853Z");
    private static readonly DateTimeOffset EcritureSousAgent = U("2026-09-26T12:45:10.000Z");
    private static readonly DateTimeOffset Releve = U("2026-09-26T12:45:30Z");
    private static readonly DateTimeOffset FinParentAvant = U("2026-09-26T12:32:23.817Z");
    private static readonly DateTimeOffset EcritureParentAvant = U("2026-09-26T12:32:25.700Z");
    private static readonly DateTimeOffset FinSousAgentTermine = U("2026-09-26T12:32:39.611Z");
    private static readonly DateTimeOffset EcritureSousAgentTermine = U("2026-09-26T12:32:39.713Z");

    private readonly List<string> _racines = new();

    public void Dispose()
    {
        foreach (var r in _racines)
            try { Directory.Delete(r, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private string TempRoot()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-travail-sous-agent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // aucun test n'écrit hors du dossier temporaire
        _racines.Add(d);
        return d;
    }

    private static string TestDataPath(string file, [CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "TestData", "SousAgentsArrierePlan", file);

    // Pose un fichier à un chemin relatif sous la racine, avec la date d'écriture voulue (c'est elle que les
    // pré-filtres regardent : celui des candidats, et celui des sous-agents).
    private static string Poser(string racine, string relatif, string contenu, DateTimeOffset ecriture)
    {
        var f = Path.Combine(racine, relatif);
        Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        File.WriteAllText(f, contenu);
        File.SetLastWriteTimeUtc(f, ecriture.UtcDateTime);
        return f;
    }

    private static string Fixture(string nom) => File.ReadAllText(TestDataPath(nom));

    // Le transcript PARENT : C--Projets-Projet-A/<Session>.jsonl.
    private static void PoserParent(string racine, string fixture, DateTimeOffset ecriture)
        => Poser(racine, Path.Combine(DossierProjet, Session + ".jsonl"), Fixture(fixture), ecriture);

    private static void EcrireParent(string racine, DateTimeOffset ecriture, params string[] lignes)
        => Poser(racine, Path.Combine(DossierProjet, Session + ".jsonl"), string.Join("\n", lignes) + "\n", ecriture);

    // Un transcript de SOUS-AGENT : C--Projets-Projet-A/<session>/subagents/<nom>.
    private static string PoserSousAgent(string racine, string fixture, string nom, DateTimeOffset ecriture, string session = Session)
        => Poser(racine, Path.Combine(DossierProjet, session, "subagents", nom), Fixture(fixture), ecriture);

    private static string Iso(DateTimeOffset t)
        => t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    // Une invite utilisateur (ligne user, bloc texte).
    private static string Invite(DateTimeOffset t) => $$$"""
        {"type":"user","isSidechain":false,"timestamp":"{{{Iso(t)}}}","cwd":"C:\\Projets\\Projet-A","message":{"role":"user","content":[{"type":"text","text":"une invite"}]}}
        """;

    // Une ligne assistant dont l'UNIQUE bloc est un tool_use portant ce nom (Claude Code écrit un bloc par ligne).
    private static string Outil(string nom, DateTimeOffset t) => $$$"""
        {"type":"assistant","isSidechain":false,"timestamp":"{{{Iso(t)}}}","cwd":"C:\\Projets\\Projet-A","message":{"role":"assistant","stop_reason":"tool_use","content":[{"type":"tool_use","id":"toolu_x","name":"{{{nom}}}","input":{}}]}}
        """;

    // --- Le relevé de 14:44 ------------------------------------------------------------------------------

    [Fact]
    public void Le_releve_de_14h44_un_sous_agent_qui_ecrit_rend_sa_session_Working()
    {
        var racine = TempRoot();
        PoserParent(racine, "parent-fin-de-tour.jsonl", EcritureParent);
        PoserSousAgent(racine, "sous-agent-en-cours.jsonl", AgentEnCours, EcritureSousAgent);

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(Releve));

        Assert.Equal(Session, s.SessionId);
        Assert.Equal("Projet-A", s.Project);
        Assert.Equal(SessionActivity.Working, s.Activity);            // « Réflexion », et non « En attente »
        Assert.Equal(DernierSousAgent, s.UpdatedAt);                  // daté par le MESSAGE (12:45:09.853), pas par l'écriture
        Assert.Equal(TravailSousAgent.MotifTranscript, s.Reason);     // un fait écrit par la source
    }

    [Fact]
    public void Sans_son_sous_agent_le_meme_parent_est_en_attente()
    {
        var racine = TempRoot();
        PoserParent(racine, "parent-fin-de-tour.jsonl", EcritureParent);

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(Releve));

        Assert.Equal(SessionActivity.WaitingTurn, s.Activity);
        Assert.Equal(FinParent, s.UpdatedAt);   // les métadonnées sans horodatage ne le rajeunissent pas (D-28-01)
        Assert.Null(s.Reason);
    }

    // --- Un sous-agent terminé ---------------------------------------------------------------------------

    [Fact]
    public void Un_sous_agent_termine_apres_le_parent_compte_comme_travail()
    {
        var racine = TempRoot();
        PoserParent(racine, "parent-avant-fin-agent.jsonl", EcritureParentAvant);
        PoserSousAgent(racine, "sous-agent-termine.jsonl", AgentTermine, EcritureSousAgentTermine);

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(U("2026-09-26T12:32:40Z")));

        // end_turn compris : le parent va reprendre (notification de tâche, 43 ms plus tard dans le réel).
        Assert.Equal(SessionActivity.Working, s.Activity);
        Assert.Equal(FinSousAgentTermine, s.UpdatedAt);
        Assert.Equal(TravailSousAgent.MotifTranscript, s.Reason);
    }

    [Fact]
    public void Un_sous_agent_termine_avant_le_dernier_message_du_parent_ne_compte_pas()
    {
        var racine = TempRoot();
        PoserParent(racine, "parent-fin-de-tour.jsonl", EcritureParent);
        PoserSousAgent(racine, "sous-agent-termine.jsonl", AgentTermine, EcritureSousAgentTermine);   // écrit AVANT le parent

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(Releve));

        Assert.Equal(SessionActivity.WaitingTurn, s.Activity);
        Assert.Equal(FinParent, s.UpdatedAt);
        Assert.Null(s.Reason);
    }

    [Fact]
    public void Un_sous_agent_rajeuni_par_sa_date_d_ecriture_ne_compte_pas()
    {
        var racine = TempRoot();
        PoserParent(racine, "parent-fin-de-tour.jsonl", EcritureParent);
        // Écrit APRÈS le parent : il passe le pré-filtre et EST lu. Mais son dernier message (12:32:39.611Z)
        // précède celui du parent : l'autorité est le message, jamais la date d'écriture (D-28-01).
        PoserSousAgent(racine, "sous-agent-termine.jsonl", AgentTermine, U("2026-09-26T12:40:00Z"));

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(Releve));

        Assert.Equal(SessionActivity.WaitingTurn, s.Activity);
        Assert.Equal(FinParent, s.UpdatedAt);
        Assert.Null(s.Reason);
    }

    // --- Ce qu'un sous-agent ne fait jamais --------------------------------------------------------------

    [Fact]
    public void Une_question_du_parent_n_est_pas_effacee_par_un_sous_agent()
    {
        var racine = TempRoot();
        var tQuestion = U("2026-09-26T12:40:05Z");
        EcrireParent(racine, U("2026-09-26T12:40:06Z"), Invite(U("2026-09-26T12:40:00Z")), Outil("AskUserQuestion", tQuestion));
        PoserSousAgent(racine, "sous-agent-en-cours.jsonl", AgentEnCours, EcritureSousAgent);   // plus récent que la question

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(Releve));

        Assert.Equal(SessionActivity.WaitingAttention, s.Activity);   // la question reste une question
        Assert.Equal(tQuestion, s.UpdatedAt);
        Assert.Equal("AskUserQuestion", s.Reason);
    }

    [Fact]
    public void Un_sous_agent_sans_horodatage_lisible_ne_fabrique_aucun_travail()
    {
        var racine = TempRoot();
        PoserParent(racine, "parent-fin-de-tour.jsonl", EcritureParent);
        var ecriture = U("2026-09-26T12:45:00Z");   // postérieure au parent : les deux fichiers SONT lus
        // Messages de sous-agent SANS timestamp (forme de TranscriptSousAgentsTests).
        Poser(racine, Path.Combine(DossierProjet, Session, "subagents", "agent-sans-date.jsonl"),
            """{"type":"user","isSidechain":true,"cwd":"C:\\Projets\\Projet-A","message":{"role":"user","content":[{"type":"text","text":"va"}]}}""" + "\n"
            + """{"type":"assistant","isSidechain":true,"message":{"role":"assistant","stop_reason":"tool_use","content":[{"type":"tool_use","name":"Bash"}]}}""" + "\n",
            ecriture);
        // Un message horodaté lisible (12:44:00Z, postérieur au parent), PUIS le dernier message, illisible : le
        // DERNIER message fait foi, on ne remonte pas chercher un horodatage plus ancien.
        Poser(racine, Path.Combine(DossierProjet, Session, "subagents", "agent-date-illisible.jsonl"),
            """{"type":"assistant","isSidechain":true,"timestamp":"2026-09-26T12:44:00.000Z","message":{"role":"assistant","stop_reason":"tool_use","content":[{"type":"tool_use","name":"Bash"}]}}""" + "\n"
            + """{"type":"user","isSidechain":true,"timestamp":"pas-une-date","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"toolu_x","content":"ok"}]}}""" + "\n",
            ecriture);

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(Releve));

        // Pas de repli sur la date d'écriture : ce signal ne sert qu'à dire « travail » — dans le doute, il se tait.
        Assert.Equal(SessionActivity.WaitingTurn, s.Activity);
        Assert.Equal(FinParent, s.UpdatedAt);
        Assert.Null(s.Reason);
    }

    [Fact]
    public void Seuls_les_transcripts_agent_du_dossier_subagents_sont_lus()
    {
        var racine = TempRoot();
        PoserParent(racine, "parent-fin-de-tour.jsonl", EcritureParent);
        // Le MÊME contenu (un sous-agent qui travaille à 12:45:09.853Z), à quatre places qui ne sont pas
        // « <session>/subagents/agent-*.jsonl » : aucune ne doit dater la session.
        foreach (var relatif in new[]
        {
            Path.Combine(Session, "subagents", "agent-a7df37762e5ce2df4.meta.json"),   // pas un .jsonl
            Path.Combine(Session, "subagents", "workflows", "wf_x", "journal.jsonl"),  // sous-dossier : non récursif
            Path.Combine(Session, "subagents", "autre-a7df.jsonl"),                    // pas « agent- »
            Path.Combine(Session, "agent-a7df.jsonl"),                                 // hors de subagents/
        })
            Poser(racine, Path.Combine(DossierProjet, relatif), Fixture("sous-agent-en-cours.jsonl"), EcritureSousAgent);

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(Releve));

        Assert.Equal(Session, s.SessionId);
        Assert.Equal(SessionActivity.WaitingTurn, s.Activity);
        Assert.Equal(FinParent, s.UpdatedAt);
        Assert.Null(s.Reason);
    }

    [Fact]
    public void Le_plus_recent_des_sous_agents_date_la_session()
    {
        var racine = TempRoot();
        PoserParent(racine, "parent-avant-fin-agent.jsonl", EcritureParentAvant);
        PoserSousAgent(racine, "sous-agent-termine.jsonl", AgentTermine, EcritureSousAgentTermine);
        PoserSousAgent(racine, "sous-agent-en-cours.jsonl", AgentEnCours, EcritureSousAgent);

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(Releve));

        Assert.Equal(SessionActivity.Working, s.Activity);
        Assert.Equal(DernierSousAgent, s.UpdatedAt);   // 12:45:09.853, et non 12:32:39.611
        Assert.Equal(TravailSousAgent.MotifTranscript, s.Reason);
    }

    [Fact]
    public void Un_sous_agent_n_est_jamais_une_ligne()
    {
        var racine = TempRoot();
        PoserParent(racine, "parent-fin-de-tour.jsonl", EcritureParent);
        PoserSousAgent(racine, "sous-agent-en-cours.jsonl", AgentEnCours, EcritureSousAgent);
        PoserSousAgent(racine, "sous-agent-en-cours.jsonl", AgentEnCours, EcritureSousAgent, session: Orphelin);   // aucun parent

        var lignes = new TranscriptSessionSource(racine).Read(Releve);

        var s = Assert.Single(lignes);
        Assert.Equal(Session, s.SessionId);
        Assert.DoesNotContain(lignes, l => l.SessionId.StartsWith("agent-", StringComparison.Ordinal));
        Assert.DoesNotContain(lignes, l => l.SessionId == Orphelin);
    }

    [Fact]
    public void Un_parent_au_travail_est_date_par_son_sous_agent_plus_recent()
    {
        var racine = TempRoot();
        EcrireParent(racine, U("2026-09-26T12:40:06Z"), Invite(U("2026-09-26T12:40:00Z")), Outil("Bash", U("2026-09-26T12:40:05Z")));
        PoserSousAgent(racine, "sous-agent-en-cours.jsonl", AgentEnCours, EcritureSousAgent);

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(Releve));

        // Le seuil de silence du moniteur partira de 12:45:09.853, et non de 12:40:05.
        Assert.Equal(SessionActivity.Working, s.Activity);
        Assert.Equal(DernierSousAgent, s.UpdatedAt);
        Assert.Equal(TravailSousAgent.MotifTranscript, s.Reason);
    }

    [Fact]
    public void Un_gros_transcript_de_sous_agent_est_lu_par_sa_queue()
    {
        var racine = TempRoot();
        PoserParent(racine, "parent-fin-de-tour.jsonl", EcritureParent);
        var tete = string.Concat(Enumerable.Repeat("""{"type":"attachment","isSidechain":true}""" + "\n", 2000));
        var f = Poser(racine, Path.Combine(DossierProjet, Session, "subagents", AgentEnCours),
            tete + Fixture("sous-agent-en-cours.jsonl"), EcritureSousAgent);
        Assert.True(new FileInfo(f).Length > 64 * 1024, "le fichier doit dépasser la queue de 64 Ko lue");

        var s = Assert.Single(new TranscriptSessionSource(racine).Read(Releve));

        Assert.Equal(SessionActivity.Working, s.Activity);
        Assert.Equal(DernierSousAgent, s.UpdatedAt);
    }

    // --- La reconnaissance d'un signal de sous-agent (D-30.1-07) -----------------------------------------

    [Theory]
    [InlineData("PreToolUse (sous-agent)", true)]
    [InlineData("PostToolUse (sous-agent)", true)]
    [InlineData("transcript (sous-agent)", true)]
    [InlineData("PostToolUse", false)]
    [InlineData("PermissionRequest", false)]
    [InlineData(null, false)]
    public void Est_reconnait_le_suffixe_des_sous_agents_et_lui_seul(string? motif, bool attendu)
    {
        Assert.Equal(" (sous-agent)", TravailSousAgent.Suffixe);
        Assert.Equal("transcript (sous-agent)", TravailSousAgent.MotifTranscript);

        Assert.Equal(attendu, TravailSousAgent.Est(new SessionSnapshot("s", "p", SessionActivity.Working, motif, DateTimeOffset.UnixEpoch)));
    }
}
