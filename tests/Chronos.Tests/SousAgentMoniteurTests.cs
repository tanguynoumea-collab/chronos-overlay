using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// SUB-01 (phase 30.1), versant MONITEUR — le travail d'un sous-agent tient la ligne « Réflexion », et il n'efface
/// jamais une attente d'intervention.
///
/// <para>Décision D-30.1-06 : l'arbitrage FUS-01 fait gagner le signal le plus récent. Sans parade, un transcript de
/// sous-agent écrit après une demande de permission du PARENT, ou un battement de sous-agent écrit après une question
/// classée <c>blocked</c> par l'app, gagnerait — et « à toi » deviendrait « Réflexion ». La règle
/// <see cref="TravailSousAgent.SansEffacerLesAttentes"/>, appliquée par le moniteur juste avant l'arbitrage, écarte les
/// signaux de sous-agent d'une session dont le verdict PROPRE est une attente d'intervention.</para>
///
/// <para><b>Classes RÉELLES</b> : un vrai <see cref="SessionMonitor"/>, une vraie <see cref="TranscriptSessionSource"/>
/// sur les fixtures réelles de l'écart E2 (<c>TestData/SousAgentsArrierePlan/</c>), de vrais fichiers d'état écrits
/// par <see cref="SessionHookProcessor.Process"/> et <see cref="EcritureEtatSession.Appliquer"/>. Tout vit dans des
/// dossiers TEMPORAIRES, <see cref="ArchiveStore"/> temporaire TOUJOURS injecté (le défaut lirait le vrai %APPDATA%),
/// et aucun test ne demande l'heure.</para>
/// </summary>
public sealed class SousAgentMoniteurTests : IDisposable
{
    private const string Session = "88677186-8690-44dc-ac77-7a319a0aa5cb";
    private const string DossierProjet = "C--Projets-Projet-A";
    private const string StdinParent = """{"session_id":"88677186-8690-44dc-ac77-7a319a0aa5cb","cwd":"C:/Projets/Projet-A"}""";

    // La session blocked de la phase 27 (fixture fin-de-tour-blocked.json, MoniteurAppBureauTests).
    private const string IdE = "adac2711-86a4-4b4d-b71c-9a594c9d959e";
    private const string MotifApp = "(anonymisé) réponse attendue";
    private static readonly DateTimeOffset A = new(2026, 9, 24, 9, 0, 6, 552, TimeSpan.Zero);

    // Les instants RÉELS du relevé de 14:44 (UTC ; Parse est permis dans tests/, pas dans Services/).
    private static DateTimeOffset U(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static readonly DateTimeOffset EcritureParent = U("2026-09-26T12:33:23.541Z");
    private static readonly DateTimeOffset DernierSousAgent = U("2026-09-26T12:45:09.853Z");
    private static readonly DateTimeOffset EcritureSousAgent = U("2026-09-26T12:45:10.000Z");
    private static readonly DateTimeOffset Releve = U("2026-09-26T12:45:30Z");

    private readonly List<string> _aSupprimer = new();

    public void Dispose()
    {
        foreach (var d in _aSupprimer)
            try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private string Dossier()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-sous-agent-moniteur-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // aucun test n'écrit hors du dossier temporaire
        _aSupprimer.Add(d);
        return d;
    }

    private string Racine(DateTimeOffset maintenant, params string[] fixtures)
    {
        var r = RacineAppBureau.Creer(maintenant, fixtures);
        _aSupprimer.Add(r);
        return r;
    }

    private ArchiveStore Archive() => new(Path.Combine(Dossier(), "archived.json"), new FakeClock(Releve));

    private static long Ms(DateTimeOffset t) => t.ToUnixTimeMilliseconds();

    private static string TestDataPath(string file, [CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "TestData", "SousAgentsArrierePlan", file);

    private static void Copier(string racine, string relatif, string fixture, DateTimeOffset ecriture)
    {
        var f = Path.Combine(racine, relatif);
        Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        File.Copy(TestDataPath(fixture), f);
        File.SetLastWriteTimeUtc(f, ecriture.UtcDateTime);
    }

    /// <summary>Les transcripts de l'écart E2 : le parent qui a fini son tour (12:33:21.818Z), et l'agent de fond qui
    /// écrit encore (dernier message 12:45:09.853Z), disposés comme sous ~/.claude/projects.</summary>
    private string TranscriptsE2()
    {
        var racine = Dossier();
        Copier(racine, Path.Combine(DossierProjet, Session + ".jsonl"), "parent-fin-de-tour.jsonl", EcritureParent);
        Copier(racine, Path.Combine(DossierProjet, Session, "subagents", "agent-a7df37762e5ce2df4.jsonl"),
            "sous-agent-en-cours.jsonl", EcritureSousAgent);
        return racine;
    }

    /// <summary>Un événement de hook du PARENT, par le chemin de production : Process puis Appliquer.</summary>
    private static void Hook(string hooks, string evenement, DateTimeOffset t)
        => EcritureEtatSession.Appliquer(hooks, SessionHookProcessor.Process(evenement, StdinParent, Ms(t)));

    /// <summary>La source de transcripts SUBSTITUÉE : un instantané fixe.</summary>
    private sealed class SourceFixe : ISessionSource
    {
        private readonly IReadOnlyList<SessionSnapshot> _sessions;
        public SourceFixe(params SessionSnapshot[] sessions) => _sessions = sessions;
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => _sessions;
    }

    // ═══ Le moniteur réel ═══════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Le_releve_de_14h44_ne_se_reproduit_plus()
    {
        var hooks = Dossier();
        Hook(hooks, "Stop", U("2026-09-26T12:33:23Z"));   // le fichier d'état du relevé : WaitingTurn 14:33:23 locale
        var moniteur = new SessionMonitor(hooks, new TranscriptSessionSource(TranscriptsE2()), Archive());

        var s = Assert.Single(moniteur.Read(Releve));
        Assert.Equal(Session, s.SessionId);
        Assert.Equal(SessionActivity.Working, s.Activity);
        Assert.Equal(DernierSousAgent, s.UpdatedAt);
        Assert.Equal("Réflexion", AffichageSessions.Etat(s.Activity));   // la ligne 1 du tableau de l'utilisateur

        // Le parent finit son tour APRÈS le dernier battement de l'agent : fraîcheur (FUS-01) ⇒ « En attente ».
        Hook(hooks, "Stop", U("2026-09-26T12:50:00Z"));
        var apres = Assert.Single(moniteur.Read(U("2026-09-26T12:50:05Z")));
        Assert.Equal(SessionActivity.WaitingTurn, apres.Activity);
        Assert.Equal(U("2026-09-26T12:50:00Z"), apres.UpdatedAt);
        Assert.Equal("En attente", AffichageSessions.Etat(apres.Activity));
    }

    [Fact]
    public void Un_sous_agent_n_efface_pas_une_permission_demandee_par_le_parent()
    {
        var hooks = Dossier();
        var demande = U("2026-09-26T12:40:00Z");
        Hook(hooks, "PermissionRequest", demande);   // PLUS ANCIENNE que le dernier message de l'agent (12:45:09.853)
        var moniteur = new SessionMonitor(hooks, new TranscriptSessionSource(TranscriptsE2()), Archive());

        var s = Assert.Single(moniteur.Read(Releve));

        Assert.Equal(SessionActivity.WaitingAttention, s.Activity);   // « à toi » reste « à toi »
        Assert.Equal("PermissionRequest", s.Reason);
        Assert.Equal(demande, s.UpdatedAt);
    }

    [Fact]
    public void Un_sous_agent_n_efface_pas_la_question_classee_par_l_app()
    {
        var maintenant = A.AddMinutes(11);
        var racineApp = Racine(maintenant, "fin-de-tour-blocked.json");
        var hooks = Dossier();
        // Un battement de SOUS-AGENT au fichier d'état, et un transcript daté par un sous-agent : tous deux plus
        // récents (A + 10 min) que la question de l'app (épisode A).
        File.WriteAllText(Path.Combine(hooks, IdE + ".json"),
            SessionHookProcessor.BuildStateJson(IdE, "Projet-E", SessionActivity.Working, "PostToolUse (sous-agent)", Ms(A.AddMinutes(10))));
        var source = new SourceFixe(new SessionSnapshot(IdE, "Projet-E", SessionActivity.Working, TravailSousAgent.MotifTranscript, A.AddMinutes(10)));

        var visibles = new SessionMonitor(hooks, source, Archive(), appBureau: new LecteurAppBureau(new[] { racineApp }))
            .Inspecter(maintenant).Visibles;

        var s = Assert.Single(visibles);
        Assert.Equal(SessionActivity.WaitingAttention, s.Activity);
        Assert.Equal(MotifApp, s.Reason);
        Assert.Equal(A, s.UpdatedAt);
    }

    [Fact]
    public void Le_travail_propre_du_parent_plus_recent_que_la_question_rend_la_main_aux_sous_agents()
    {
        var maintenant = A.AddMinutes(11);
        var racineApp = Racine(maintenant, "fin-de-tour-blocked.json");
        var hooks = Dossier();
        // Le PARENT a travaillé après la question (A + 1 min) : son verdict propre n'est plus une attente.
        File.WriteAllText(Path.Combine(hooks, IdE + ".json"),
            SessionHookProcessor.BuildStateJson(IdE, "Projet-E", SessionActivity.Working, "PostToolUse", Ms(A.AddMinutes(1))));
        var source = new SourceFixe(new SessionSnapshot(IdE, "Projet-E", SessionActivity.Working, TravailSousAgent.MotifTranscript, A.AddMinutes(10)));

        var visibles = new SessionMonitor(hooks, source, Archive(), appBureau: new LecteurAppBureau(new[] { racineApp }))
            .Inspecter(maintenant).Visibles;

        var s = Assert.Single(visibles);
        Assert.Equal(SessionActivity.Working, s.Activity);
        Assert.Equal(A.AddMinutes(10), s.UpdatedAt);                   // le sous-agent reprend sa place ordinaire
        Assert.Equal(TravailSousAgent.MotifTranscript, s.Reason);
    }

    [Fact]
    public void Un_sous_agent_muet_depuis_vingt_minutes_rend_En_attente_interrogatif()
    {
        var moniteur = new SessionMonitor(Dossier(), new TranscriptSessionSource(TranscriptsE2()), Archive());

        var s = Assert.Single(moniteur.Read(DernierSousAgent.AddMinutes(21)));

        // Le seuil de silence part du dernier message de l'agent ; l'instant du signal n'est pas réécrit.
        Assert.Equal(SessionActivity.WaitingDeduced, s.Activity);
        Assert.Equal("En attente ?", AffichageSessions.Etat(s.Activity));
        Assert.Equal(DernierSousAgent, s.UpdatedAt);
    }

    [Fact]
    public void Une_session_traitee_dont_le_sous_agent_ecrit_redevient_Reflexion_puis_revient_au_Stop()
    {
        var hooks = Dossier();
        Hook(hooks, "Stop", U("2026-09-26T12:33:23Z"));
        var treated = new TreatedStore(Path.Combine(Dossier(), "treated.json"), new FakeClock(Releve));
        treated.Set(Session, Ms(U("2026-09-26T12:33:23Z")));   // l'attente de 12:33:23 a été marquée traitée
        var moniteur = new SessionMonitor(hooks, new TranscriptSessionSource(TranscriptsE2()), Archive(),
            treated, new SessionTreatmentTracker(treated));

        // LUE-05 : une session traitée qui travaille est visible.
        var s = Assert.Single(moniteur.Read(Releve));
        Assert.Equal(SessionActivity.Working, s.Activity);

        // NET-03 : le Stop suivant du parent est un épisode d'attente PLUS RÉCENT — la session revient.
        Hook(hooks, "Stop", U("2026-09-26T12:50:00Z"));
        var apres = Assert.Single(moniteur.Read(U("2026-09-26T12:50:05Z")));
        Assert.Equal(SessionActivity.WaitingTurn, apres.Activity);
        Assert.False(treated.Load().ContainsKey(Session), "l'épisode plus récent devait purger l'inscription « traitée »");
    }

    // ═══ La règle, pure ═════════════════════════════════════════════════════════════════════════════════════

    private static readonly DateTimeOffset T0 = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddMinutes(5);

    [Fact]
    public void Sans_attente_propre_les_signaux_de_sous_agent_sont_deposes()
    {
        var signaux = new List<SignalSession>
        {
            new(SourceSession.Hook, new SessionSnapshot("X", "p", SessionActivity.WaitingTurn, null, T0)),
            new(SourceSession.Transcript, new SessionSnapshot("X", "p", SessionActivity.Working, TravailSousAgent.MotifTranscript, T1)),
        };

        var deposes = TravailSousAgent.SansEffacerLesAttentes(signaux);

        Assert.Equal(2, deposes.Count);
        var r = Assert.Single(ArbitrageSessions.Trancher(deposes).Retenus);
        Assert.Equal(SessionActivity.Working, r.Activity);
        Assert.Equal(T1, r.UpdatedAt);
    }

    [Fact]
    public void Une_attente_propre_ecarte_les_signaux_de_sous_agent_de_cette_session_seulement()
    {
        var signaux = new List<SignalSession>
        {
            new(SourceSession.Hook, new SessionSnapshot("X", "p", SessionActivity.WaitingAttention, "PermissionRequest", T0)),
            new(SourceSession.Transcript, new SessionSnapshot("X", "p", SessionActivity.Working, TravailSousAgent.MotifTranscript, T1)),
            new(SourceSession.Hook, new SessionSnapshot("X", "p", SessionActivity.Working, "PostToolUse (sous-agent)", T1)),
            new(SourceSession.Hook, new SessionSnapshot("Y", "p", SessionActivity.WaitingTurn, null, T0)),
            new(SourceSession.Transcript, new SessionSnapshot("Y", "p", SessionActivity.Working, TravailSousAgent.MotifTranscript, T1)),
        };

        var deposes = TravailSousAgent.SansEffacerLesAttentes(signaux);

        var x = Assert.Single(deposes, d => d.Session.SessionId == "X");
        Assert.Equal(SessionActivity.WaitingAttention, x.Session.Activity);
        Assert.Equal(2, deposes.Count(d => d.Session.SessionId == "Y"));

        var retenus = ArbitrageSessions.Trancher(deposes).Retenus;
        Assert.Equal(SessionActivity.WaitingAttention, retenus.Single(r => r.SessionId == "X").Activity);
        Assert.Equal(SessionActivity.Working, retenus.Single(r => r.SessionId == "Y").Activity);
    }
}
