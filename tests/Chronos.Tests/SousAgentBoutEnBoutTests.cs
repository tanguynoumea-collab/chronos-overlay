using System.IO;
using System.Text.Json;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// SUB-01 (phase 30.1) — LES DEUX MOITIÉS TENUES ENSEMBLE. La vague 1 a livré le côté hooks (plan 30.1-01 : le battement
/// d'un sous-agent écrit « Réflexion » pour sa session, avec un motif suffixé) et le côté fusion (plan 30.1-02 : le
/// moniteur reconnaît un signal de sous-agent à ce suffixe et ne le laisse jamais effacer une attente d'intervention), à
/// fichiers disjoints. La constante du suffixe est donc déclarée DEUX fois : <see cref="SessionHookProcessor.SuffixeSousAgent"/>
/// et <see cref="TravailSousAgent.Suffixe"/>.
///
/// <para><b>Pourquoi cette garde.</b> Si les deux constantes divergeaient, le moniteur prendrait un battement de sous-agent
/// pour un travail PROPRE du parent ; ce travail, plus récent, gagnerait l'arbitrage et effacerait une question encore
/// ouverte — la brèche R3 rouverte, sans qu'aucun test d'une seule moitié ne rougisse : chacune resterait cohérente avec
/// elle-même.</para>
///
/// <para><b>Classes RÉELLES, de bout en bout.</b> Le vrai hook (<see cref="SessionHookProcessor.Process"/> puis
/// <see cref="EcritureEtatSession.Appliquer"/>) écrit ; le vrai <see cref="SessionMonitor"/> relit, avec la vraie question
/// <c>blocked</c> de l'app (fixture réelle de la phase 27, lue par un vrai <see cref="LecteurAppBureau"/>). Seule la source
/// de transcripts est substituée (vide) : elle lirait sinon le vrai <c>~/.claude/projects</c>. Dossiers TEMPORAIRES
/// seulement, <see cref="ArchiveStore"/> temporaire toujours injecté (le défaut lirait le vrai %APPDATA%), aucune horloge
/// réelle.</para>
/// </summary>
public sealed class SousAgentBoutEnBoutTests : IDisposable
{
    // La session blocked de la phase 27 (fixture fin-de-tour-blocked.json, MoniteurAppBureauTests).
    private const string IdE = "adac2711-86a4-4b4d-b71c-9a594c9d959e";
    private const string MotifApp = "(anonymisé) réponse attendue";
    private const string StdinE = """{"session_id":"adac2711-86a4-4b4d-b71c-9a594c9d959e","cwd":"C:/Projets/Projet-E"}""";
    private const string StdinESousAgent = """{"session_id":"adac2711-86a4-4b4d-b71c-9a594c9d959e","cwd":"C:/Projets/Projet-E","agent_id":"a-1"}""";

    /// <summary>E — la fin du tour, vue par le transcript (relevé réel de la phase 29).</summary>
    private static readonly DateTimeOffset E = new(2026, 9, 24, 9, 0, 4, 991, TimeSpan.Zero);

    /// <summary>A — l'instant d'activité lu à la première apparition du résumé <c>blocked</c> : l'épisode de la question.</summary>
    private static readonly DateTimeOffset A = new(2026, 9, 24, 9, 0, 6, 552, TimeSpan.Zero);

    private readonly List<string> _aSupprimer = new();

    public void Dispose()
    {
        foreach (var d in _aSupprimer)
            try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private string Dossier()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-sous-agent-bout-en-bout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // aucun test n'écrit hors du dossier temporaire
        _aSupprimer.Add(d);
        return d;
    }

    private static long Ms(DateTimeOffset t) => t.ToUnixTimeMilliseconds();

    /// <summary>Le motif (<c>reason</c>) que le hook s'apprête à écrire. Nul s'il n'en porte pas.</summary>
    private static string? MotifEcrit(string? stateJson)
    {
        Assert.False(string.IsNullOrEmpty(stateJson), "Le hook n'a rendu aucun état à écrire.");
        using var doc = JsonDocument.Parse(stateJson!);
        return doc.RootElement.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String
            ? r.GetString()
            : null;
    }

    /// <summary>La source de transcripts SUBSTITUÉE : aucune session. La session n'est connue que par son fichier de hook.</summary>
    private sealed class SourceVide : ISessionSource
    {
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => Array.Empty<SessionSnapshot>();
    }

    /// <summary>
    /// Les deux constantes sont égales, et ce que le hook ÉCRIT pour un battement de sous-agent est ce que le moniteur
    /// RECONNAÎT comme tel. Une DEMANDE de sous-agent (permission) garde son motif ordinaire : le moniteur ne la prend pas
    /// pour un travail — une attente de sous-agent n'est pas un travail.
    /// </summary>
    [Theory]
    [InlineData("PreToolUse")]
    [InlineData("PostToolUse")]
    public void Le_motif_d_un_battement_de_sous_agent_est_reconnu_par_le_moniteur(string ev)
    {
        Assert.Equal(TravailSousAgent.Suffixe, SessionHookProcessor.SuffixeSousAgent);

        const string stdin = """{"session_id":"s","cwd":"C:/dev/P","agent_id":"a-1"}""";
        var r = SessionHookProcessor.Process(ev, stdin, 1000);
        Assert.False(r.Ignore, $"Le battement {ev} d'un sous-agent doit passer le processeur (SUB-01).");

        var motif = MotifEcrit(r.StateJson);
        Assert.Equal(ev + TravailSousAgent.Suffixe, motif);
        Assert.True(TravailSousAgent.Est(new SessionSnapshot("s", "P", SessionActivity.Working, motif, DateTimeOffset.UnixEpoch)),
            $"Le moniteur ne reconnaît pas « {motif} » comme un signal de sous-agent : il le prendrait pour un travail propre du parent.");

        var demande = SessionHookProcessor.Process("PermissionRequest", stdin, 1000);
        Assert.False(demande.Ignore, "Une demande de permission d'un sous-agent réclame une intervention : elle passe.");
        var motifDemande = MotifEcrit(demande.StateJson);
        Assert.False(TravailSousAgent.Est(new SessionSnapshot("s", "P", SessionActivity.WaitingAttention, motifDemande, DateTimeOffset.UnixEpoch)),
            $"« {motifDemande} » est une DEMANDE de sous-agent, pas un travail : le moniteur ne doit pas l'écarter comme tel.");
    }

    /// <summary>
    /// La chaîne complète, sur la question réelle de l'app. Le tour du parent se termine (<c>Stop</c>, 1,5 s après la fin du
    /// tour vue par le transcript) ; l'app classe cette fin de tour <c>blocked</c> (épisode A) ; dix minutes plus tard, un
    /// agent de fond bat. Le hook ÉCRIT ce battement — le tour est fini, donc réaffirmable — et c'est au point de fusion que
    /// la question est protégée : la ligne reste « En attente », avec le motif de l'app, datée de la question.
    /// </summary>
    [Fact]
    public void Bout_en_bout_un_battement_de_sous_agent_ecrit_par_le_hook_n_efface_pas_la_question_de_l_app()
    {
        var maintenant = A.AddMinutes(11);
        var racineApp = RacineAppBureau.Creer(maintenant, "fin-de-tour-blocked.json");
        _aSupprimer.Add(racineApp);
        var hooks = Dossier();

        // 1. Le tour du parent se termine : le vrai hook écrit WaitingTurn.
        var stop = EcritureEtatSession.Appliquer(hooks, SessionHookProcessor.Process("Stop", StdinE, Ms(E.AddMilliseconds(1500))));
        Assert.True(stop.Reussi, stop.Cause);
        Assert.False(stop.Ignoree, stop.Cause);

        // 2. Un agent de fond bat, dix minutes après la question : sur un tour fini, le battement est ÉCRIT (réaffirmable).
        var battement = EcritureEtatSession.Appliquer(hooks,
            SessionHookProcessor.Process("PostToolUse", StdinESousAgent, Ms(A.AddMinutes(10))));
        Assert.True(battement.Reussi, battement.Cause);
        Assert.False(battement.Ignoree, battement.Cause);

        // 3. Le verdict d'abord — c'est ce que l'utilisateur lit : le vrai moniteur, avec la vraie question de l'app.
        var archive = new ArchiveStore(Path.Combine(Dossier(), "archived.json"), new FakeClock(maintenant));
        var lecture = new SessionMonitor(hooks, new SourceVide(), archive, appBureau: new LecteurAppBureau(new[] { racineApp }))
            .Inspecter(maintenant);

        var s = Assert.Single(lecture.Visibles);
        Assert.Equal(IdE, s.SessionId);
        Assert.Equal(SessionActivity.WaitingAttention, s.Activity);
        Assert.Equal(MotifApp, s.Reason);
        Assert.Equal(A, s.UpdatedAt);

        // 4. La cause, sur le disque : le battement est bien là, avec le motif que le §4 du contrat documente.
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(hooks, IdE + ".json")));
        Assert.Equal("Working", doc.RootElement.GetProperty("activity").GetString());
        Assert.Equal("PostToolUse (sous-agent)", doc.RootElement.GetProperty("reason").GetString());
    }
}
