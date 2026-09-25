using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// APP-03, APP-01 et APP-02, versant MONITEUR (phase 29, plan 03) : la question que l'app bureau a vue à la fin d'un
/// tour entre dans l'arbitrage exactement quand elle le doit, et jamais autrement ; le titre suit la ligne ; sans
/// fichiers de l'app, rien ne bouge.
///
/// <para><b>Classes RÉELLES.</b> Un vrai <see cref="SessionMonitor"/> et un vrai <see cref="LecteurAppBureau"/> sur une
/// racine temporaire à la forme exacte de l'app, remplie avec les fixtures RÉELLES de la phase 27 (octets exacts, ou
/// dérivées par remplacement TEXTUEL asserté). Seule la source de transcripts est substituée : elle lirait sinon le
/// vrai <c>~/.claude/projects</c>.</para>
///
/// <para><b>Aucune écriture hors du dossier temporaire.</b> Dossier de hooks temporaire, <see cref="ArchiveStore"/>
/// temporaire TOUJOURS injecté (le défaut lirait le vrai %APPDATA%), <see cref="TreatedStore"/> temporaire et daté par
/// une horloge FIXE : sa rétention de 24 h se mesure à SON horloge, et l'horloge réelle purgerait une entrée datée du
/// 2026-09-24. Tout ce qui est créé est supprimé à la fin de chaque test.</para>
///
/// <para><b>Instants du relevé réel</b> (29-RESEARCH Q3.c) : fin de tour E (<c>end_turn</c> du transcript)
/// = 2026-09-24T09:00:04.991Z ; <c>lastActivityAt</c> de la fixture <c>blocked</c> A = 2026-09-24T09:00:06.552Z ;
/// A − E = 1,561 s. Les cas C1 à C11 portent les numéros du tableau de 29-RESEARCH Q3.d.</para>
/// </summary>
public sealed class MoniteurAppBureauTests : IDisposable
{
    // ── Le relevé réel ─────────────────────────────────────────────────────────────────────────────────────────
    private const string Bloquee = "fin-de-tour-blocked.json";
    private const string Terminee = "fin-de-tour-completed.json";
    private const string PreteARevue = "fin-de-tour-review-ready.json";
    private const string SansResume = "sans-postTurnSummary.json";
    private const string GesteB = "session-courante-geste-b.json";

    private const string IdE = "adac2711-86a4-4b4d-b71c-9a594c9d959e";   // blocked, « Session E », Projet-E
    private const string IdB = "11456cab-d447-42c7-aa85-9920ce64f7ba";   // geste B, « Session A », Projet-A
    private const string IdC = "7bc776ff-8019-4f2e-a942-83d2ac6490e6";   // completed, « Session C »
    private const string IdR = "c17a1b03-3c8d-4c05-a747-dd77bc702e1b";   // review_ready, « Session B »
    private const string IdD = "dae57d29-d277-4e4c-8cad-b4d2b97701e5";   // sans postTurnSummary, « Session D »
    private const string Motif = "(anonymisé) réponse attendue";

    /// <summary>E — la fin du tour, vue par le transcript.</summary>
    private static readonly DateTimeOffset E = new(2026, 9, 24, 9, 0, 4, 991, TimeSpan.Zero);

    /// <summary>A — l'instant d'activité lu à la première apparition du résumé <c>blocked</c> (1790240406552).</summary>
    private static readonly DateTimeOffset A = new(2026, 9, 24, 9, 0, 6, 552, TimeSpan.Zero);

    /// <summary>Le « maintenant » par défaut : cinq minutes après la question.</summary>
    private static readonly DateTimeOffset M = A.AddMinutes(5);

    // ── Ce que chaque test crée, et qu'il supprime ─────────────────────────────────────────────────────────────
    private readonly List<string> _aSupprimer = new();

    public void Dispose()
    {
        foreach (var d in _aSupprimer)
            try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { }
    }

    private string Dossier()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-moniteur-app-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // garde anti-accident
        _aSupprimer.Add(d);
        return d;
    }

    /// <summary>Une racine de l'app où chaque fixture est copiée octet pour octet, écrite une minute avant
    /// <paramref name="maintenant"/>.</summary>
    private string Racine(DateTimeOffset maintenant, params string[] fixtures)
    {
        var r = RacineAppBureau.Creer(maintenant, fixtures);
        _aSupprimer.Add(r);
        return r;
    }

    /// <summary>Une racine de l'app qui ne contient qu'un texte, sous le nom de fichier d'une fixture.</summary>
    private string RacineAvecTexte(string nomFichier, string contenu, DateTimeOffset dateEcriture)
    {
        var r = RacineAppBureau.NouvelleRacine();
        _aSupprimer.Add(r);
        RacineAppBureau.Ecrire(r, nomFichier, contenu, dateEcriture);
        return r;
    }

    private static LecteurAppBureau Lecteur(string racine) => new(new[] { racine });

    private ArchiveStore Archive() => new(Path.Combine(Dossier(), "archived.json"), new FakeClock(M));

    /// <summary>Le moniteur de production, aux racines près : lecteur de l'app par argument nommé, détecteur de
    /// traitement branché dès qu'un magasin « traité » est donné.</summary>
    private SessionMonitor Moniteur(ISessionSource transcripts, LecteurAppBureau? lecteur, string? hooks = null,
        ArchiveStore? archive = null, TreatedStore? treated = null)
        => new(hooks ?? Dossier(), transcripts, archive ?? Archive(), treated,
               treated is null ? null : new SessionTreatmentTracker(treated), appBureau: lecteur);

    /// <summary>La source de transcripts SUBSTITUÉE, réaffectable entre deux cycles.</summary>
    private sealed class SourceModifiable : ISessionSource
    {
        public IReadOnlyList<SessionSnapshot> Sessions { get; set; }
        public SourceModifiable(params SessionSnapshot[] sessions) => Sessions = sessions;
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => Sessions;
    }

    /// <summary>Le transcript de la session E : tour fini, à l'instant E.</summary>
    private static SessionSnapshot TranscriptE() => new(IdE, "Projet-E", SessionActivity.WaitingTurn, null, E);

    /// <summary>Un fichier d'état de hook au format RÉEL, dans le dossier de hooks donné.</summary>
    private static void EcrireHook(string hooks, string id, string projet, SessionActivity a, DateTimeOffset maj)
        => File.WriteAllText(Path.Combine(hooks, id + ".json"),
               SessionHookProcessor.BuildStateJson(id, projet, a, null, maj.ToUnixTimeMilliseconds()));

    /// <summary>Une chaîne UNIQUE par lecture, champ par champ : ce qui permet de dire « exactement la même ».</summary>
    private static string Sequence(IEnumerable<SessionSnapshot> sessions)
        => string.Join(" | ", sessions.Select(s => $"{s.SessionId}={s.Activity}@{s.UpdatedAt:O}|{s.Reason}|{s.Project}|{s.Titre}"));

    private static string Desaccords(LectureSessions l)
        => string.Join(" | ", l.Desaccords.Select(d =>
               $"{d.SessionId}:{d.SourceRetenue}/{d.EtatRetenu}>{d.SourceEcartee}/{d.EtatEcarte}+{d.EcartAge}"));

    // ═══ La jointure par cliSessionId, et le titre (APP-01, APP-02) ═══════════════════════════════════════════

    /// <summary>Le geste B de la phase 27 : la session reçoit le titre de l'app, et RIEN d'autre ne change — ni son
    /// dossier (la ligne garde le nom que les sources d'activité lui donnent), ni son état, ni son instant. Le dernier
    /// focus est lu en UTC exact (l'heure de Paris affichait 20:30:44).</summary>
    [Fact]
    public void La_session_du_geste_B_recoit_son_titre_et_garde_son_dossier()
    {
        var maintenant = new DateTimeOffset(2026, 9, 25, 18, 40, 0, TimeSpan.Zero);
        var finDeTour = new DateTimeOffset(2026, 9, 25, 18, 34, 4, 328, TimeSpan.Zero);
        var transcript = new SessionSnapshot(IdB, "Projet-A", SessionActivity.WaitingTurn, null, finDeTour);

        var lecture = Moniteur(new SourceModifiable(transcript), Lecteur(Racine(maintenant, GesteB))).Inspecter(maintenant);

        var s = Assert.Single(lecture.Visibles);
        Assert.Equal("Session A", s.Titre);
        Assert.Equal("Projet-A", s.Project);
        Assert.Equal(SessionActivity.WaitingTurn, s.Activity);
        Assert.Equal(finDeTour, s.UpdatedAt);
        Assert.Equal(transcript with { Titre = "Session A" }, s);   // le titre, et rien d'autre
        Assert.Empty(lecture.Desaccords);
        Assert.Equal(new DateTimeOffset(2026, 9, 25, 18, 30, 44, 976, TimeSpan.Zero),
                     lecture.AppBureau!.ParSession[IdB].DernierFocus);
    }

    /// <summary>Une session que l'app ne connaît pas garde un titre NUL — pas un titre vide, pas celui d'une autre.</summary>
    [Fact]
    public void Une_session_sans_metadonnees_n_a_pas_de_titre()
    {
        var lecture = Moniteur(new SourceModifiable(TranscriptE()), Lecteur(Racine(M, GesteB))).Inspecter(M);

        Assert.Null(Assert.Single(lecture.Visibles).Titre);
        Assert.True(lecture.AppBureau!.DossierTrouve);                // l'app a bien été lue…
        Assert.True(lecture.AppBureau.ParSession.ContainsKey(IdB));   // …elle connaît une AUTRE session
        Assert.False(lecture.AppBureau.ParSession.ContainsKey(IdE));
    }

    /// <summary>Un fichier de l'app sans <c>title</c> ne donne pas un titre vide : la ligne garde son dossier.</summary>
    [Fact]
    public void Un_fichier_sans_titre_ne_donne_pas_de_titre_vide()
    {
        var activite = new DateTimeOffset(2026, 9, 25, 18, 30, 28, 351, TimeSpan.Zero);
        var maintenant = activite.AddMinutes(5);
        var sansTitre = RacineAppBureau.Deriver(SansResume, ("\"title\": \"Session D\",", ""));
        var racine = RacineAvecTexte(RacineAppBureau.NomFichier(SansResume), sansTitre, maintenant.AddMinutes(-1));
        var transcript = new SessionSnapshot(IdD, "Projet-D", SessionActivity.WaitingTurn, null, activite);

        var lecture = Moniteur(new SourceModifiable(transcript), Lecteur(racine)).Inspecter(maintenant);

        var s = Assert.Single(lecture.Visibles);
        Assert.Null(s.Titre);
        Assert.Equal("Projet-D", AffichageSessions.Nom(s));
        Assert.True(lecture.AppBureau!.ParSession.ContainsKey(IdD));   // lu et joint — simplement sans titre
    }

    /// <summary>Le titre est posé AVANT les filtres : une session masquée le porte aussi, et le rapport, qui liste les
    /// masquées, peut la nommer.</summary>
    [Fact]
    public void Une_session_masquee_porte_aussi_son_titre()
    {
        var maintenant = new DateTimeOffset(2026, 9, 25, 18, 40, 0, TimeSpan.Zero);
        var archive = Archive();
        archive.Add(IdB);
        var transcript = new SessionSnapshot(IdB, "Projet-A", SessionActivity.WaitingTurn, null, maintenant.AddMinutes(-6));

        var lecture = Moniteur(new SourceModifiable(transcript), Lecteur(Racine(maintenant, GesteB)), archive: archive)
            .Inspecter(maintenant);

        Assert.Empty(lecture.Visibles);
        var masquee = Assert.Single(lecture.Masquees);
        Assert.Equal(MotifMasquage.Archivee, masquee.Motif);
        Assert.Equal("Session A", masquee.Session.Titre);
    }

    // ═══ C1 à C11 : la question de l'app dans l'arbitrage (APP-03) ═════════════════════════════════════════════

    /// <summary>C1 — le cas nominal. Le résumé <c>blocked</c> est écrit 1,561 s après la fin du tour : l'app gagne par
    /// FRAÎCHEUR. La ligne dit « En attente » au rang des questions, porte le motif de l'app, garde le dossier du
    /// transcript et prend le titre de l'app. Le désaccord (question contre tour fini) est dit, nommé par ses sources.</summary>
    [Fact]
    public void C1_une_question_de_l_app_bat_le_tour_fini_du_transcript_et_porte_son_motif()
    {
        var lecture = Moniteur(new SourceModifiable(TranscriptE()), Lecteur(Racine(M, Bloquee))).Inspecter(M);

        var s = Assert.Single(lecture.Visibles);
        Assert.Equal(SessionActivity.WaitingAttention, s.Activity);
        Assert.Equal(Motif, s.Reason);
        Assert.Equal(A, s.UpdatedAt);
        Assert.Equal("Projet-E", s.Project);
        Assert.Equal("Session E", s.Titre);
        Assert.Equal(AffichageSessions.EnAttente, AffichageSessions.Etat(s.Activity));

        var d = Assert.Single(lecture.Desaccords);
        Assert.Equal(SourceSession.AppBureau, d.SourceRetenue);
        Assert.Equal(SessionActivity.WaitingAttention, d.EtatRetenu);
        Assert.Equal(SourceSession.Transcript, d.SourceEcartee);
        Assert.Equal(SessionActivity.WaitingTurn, d.EtatEcarte);
        Assert.Equal(TimeSpan.FromMilliseconds(1561), d.EcartAge);
    }

    /// <summary>C2 — l'utilisateur répond : le transcript, plus récent, repasse la ligne en « Réflexion » avant même
    /// que l'app efface son résumé (le fichier est inchangé).</summary>
    [Fact]
    public void C2_la_reponse_repasse_en_Reflexion_avant_meme_l_effacement()
    {
        var reponse = new SessionSnapshot(IdE, "Projet-E", SessionActivity.Working, null, A.AddSeconds(30));

        var lecture = Moniteur(new SourceModifiable(reponse), Lecteur(Racine(M, Bloquee))).Inspecter(M);

        Assert.Equal(SessionActivity.Working, Assert.Single(lecture.Visibles).Activity);
        Assert.Equal(ClassificationFinDeTour.Bloquee, lecture.AppBureau!.ParSession[IdE].ClassificationFinDeTour);
        var d = Assert.Single(lecture.Desaccords);
        Assert.Equal(SourceSession.Transcript, d.SourceRetenue);
        Assert.Equal(SourceSession.AppBureau, d.SourceEcartee);
    }

    /// <summary>C3 — au cycle suivant l'app a effacé son résumé : plus aucun signal de l'app, la ligne revient au tour
    /// fini du transcript, sans désaccord. L'effacement est dérivé TEXTUELLEMENT, sur une seule ligne : la clé de
    /// l'objet est renommée, et le lecteur ignore une clé inconnue comme tout champ inconnu.</summary>
    [Fact]
    public void C3_le_resume_efface_ne_laisse_aucun_signal_de_l_app()
    {
        var racine = Racine(M, Bloquee);
        var moniteur = Moniteur(new SourceModifiable(TranscriptE()), Lecteur(racine));
        Assert.Equal(SessionActivity.WaitingAttention, Assert.Single(moniteur.Inspecter(M).Visibles).Activity);

        var efface = RacineAppBureau.Deriver(Bloquee, ("\"postTurnSummary\": {", "\"postTurnSummaryEfface\": {"));
        RacineAppBureau.Ecrire(racine, RacineAppBureau.NomFichier(Bloquee), efface, M);   // date + 1 min
        var lecture = moniteur.Inspecter(M.AddMinutes(1));

        var s = Assert.Single(lecture.Visibles);
        Assert.Equal(SessionActivity.WaitingTurn, s.Activity);
        Assert.Equal(E, s.UpdatedAt);
        Assert.Null(s.Reason);
        Assert.Empty(lecture.Desaccords);
        Assert.Equal(1, lecture.AppBureau!.RelusSurDisque);   // le lecteur a bien vu l'effacement
        Assert.Equal(ClassificationFinDeTour.Inconnue, lecture.AppBureau.ParSession[IdE].ClassificationFinDeTour);
    }

    /// <summary>C5 — un hook du MÊME instant garde la main : lui seul sait dire « permission », il passe avant l'app à
    /// âge égal.</summary>
    [Fact]
    public void C5_un_hook_du_meme_instant_garde_la_main()
    {
        var hooks = Dossier();
        EcrireHook(hooks, IdE, "Projet-E", SessionActivity.WaitingTurn, A);

        var lecture = Moniteur(new SourceModifiable(TranscriptE()), Lecteur(Racine(M, Bloquee)), hooks).Inspecter(M);

        var s = Assert.Single(lecture.Visibles);
        Assert.Equal(SessionActivity.WaitingTurn, s.Activity);
        Assert.Equal(A, s.UpdatedAt);
        var d = Assert.Single(lecture.Desaccords);   // le transcript, d'accord avec le hook, n'est pas un désaccord
        Assert.Equal(SourceSession.Hook, d.SourceRetenue);
        Assert.Equal(SourceSession.AppBureau, d.SourceEcartee);
        Assert.Equal(TimeSpan.Zero, d.EcartAge);
    }

    /// <summary>C6 — le rang du hook ne sert qu'à âge égal : un Stop plus ancien cède à la question de l'app.</summary>
    [Fact]
    public void C6_un_Stop_de_hook_plus_ancien_cede_a_la_question_de_l_app()
    {
        var hooks = Dossier();
        EcrireHook(hooks, IdE, "Projet-E", SessionActivity.WaitingTurn, E.AddMilliseconds(300));

        var lecture = Moniteur(new SourceModifiable(TranscriptE()), Lecteur(Racine(M, Bloquee)), hooks).Inspecter(M);

        var s = Assert.Single(lecture.Visibles);
        Assert.Equal(SessionActivity.WaitingAttention, s.Activity);
        Assert.Equal(Motif, s.Reason);
        Assert.Equal(2, lecture.Desaccords.Count);
        Assert.All(lecture.Desaccords, d => Assert.Equal(SourceSession.AppBureau, d.SourceRetenue));
    }

    /// <summary>C7 — L'APP QUALIFIE UNE LIGNE, ELLE N'EN CRÉE PAS (décision verrouillée). Sans hook ni transcript pour
    /// la session ce cycle, la question de l'app ne fabrique aucune ligne : le périmètre reste celui des sources
    /// d'activité, et une question de 36 h ne ressuscite pas une session. Mutation (f) jouée : déposer pour toute
    /// session de l'app fait rougir ce test.</summary>
    [Fact]
    public void C7_sans_autre_source_la_question_de_l_app_ne_cree_aucune_ligne()
    {
        var lecture = Moniteur(new SourceModifiable(), Lecteur(Racine(M, Bloquee))).Inspecter(M);

        Assert.Empty(lecture.Visibles);
        Assert.Empty(lecture.Masquees);
        Assert.Empty(lecture.Desaccords);
        Assert.Equal(ClassificationFinDeTour.Bloquee, lecture.AppBureau!.ParSession[IdE].ClassificationFinDeTour);
    }

    /// <summary>C8 — l'horizon d'abandon (8 h, <see cref="HorizonsSessions.Abandon"/>) vaut pour la question de l'app
    /// comme pour les hooks et les transcripts, avec la MÊME borne incluse : à 480 min elle compte, à 481 elle se tait.</summary>
    [Theory]
    [InlineData(480, SessionActivity.WaitingAttention)]
    [InlineData(481, SessionActivity.WaitingTurn)]
    public void C8_une_question_de_plus_de_huit_heures_ne_depose_rien(int minutes, SessionActivity attendu)
    {
        var maintenant = A.AddMinutes(minutes);

        var lecture = Moniteur(new SourceModifiable(TranscriptE()), Lecteur(Racine(maintenant, Bloquee))).Inspecter(maintenant);

        Assert.Equal(attendu, Assert.Single(lecture.Visibles).Activity);
        Assert.Equal(ClassificationFinDeTour.Bloquee, lecture.AppBureau!.ParSession[IdE].ClassificationFinDeTour);
    }

    /// <summary>C9 — rien d'autre que <c>blocked</c> avec un motif, sur une session non archivée, ne change un état :
    /// ni <c>completed</c>, ni <c>review_ready</c>, ni l'absence de résumé (fixtures réelles), ni les catégories que
    /// l'app connaît et que le disque n'a jamais montrées (<c>need_input</c>, <c>failed</c>), ni un motif blanc, ni une
    /// session archivée dans l'app. Chaque fichier est pourtant lu et joint (le titre le prouve).</summary>
    [Theory]
    [InlineData("completed")]
    [InlineData("review_ready")]
    [InlineData("sans-resume")]
    [InlineData("need_input")]
    [InlineData("failed")]
    [InlineData("motif-vide")]
    [InlineData("archivee")]
    public void C9_rien_d_autre_que_blocked_avec_motif_ne_change_un_etat(string cas)
    {
        var (fixture, contenu, id, instantRef, titre) = cas switch
        {
            "completed" => (Terminee, RacineAppBureau.Texte(Terminee), IdC,
                            new DateTimeOffset(2026, 9, 25, 7, 24, 15, 962, TimeSpan.Zero), "Session C"),
            "review_ready" => (PreteARevue, RacineAppBureau.Texte(PreteARevue), IdR,
                            new DateTimeOffset(2026, 9, 25, 18, 34, 6, 752, TimeSpan.Zero), "Session B"),
            "sans-resume" => (SansResume, RacineAppBureau.Texte(SansResume), IdD,
                            new DateTimeOffset(2026, 9, 25, 18, 30, 28, 351, TimeSpan.Zero), "Session D"),
            "need_input" => (Bloquee, RacineAppBureau.Deriver(Bloquee,
                                ("\"status_category\": \"blocked\"", "\"status_category\": \"need_input\"")), IdE, A, "Session E"),
            "failed" => (Bloquee, RacineAppBureau.Deriver(Bloquee,
                                ("\"status_category\": \"blocked\"", "\"status_category\": \"failed\"")), IdE, A, "Session E"),
            "motif-vide" => (Bloquee, RacineAppBureau.Deriver(Bloquee,
                                ($"\"needs_action\": \"{Motif}\"", "\"needs_action\": \"   \"")), IdE, A, "Session E"),
            "archivee" => (Bloquee, RacineAppBureau.Deriver(Bloquee,
                                ("\"isArchived\": false", "\"isArchived\": true")), IdE, A, "Session E"),
            _ => throw new ArgumentOutOfRangeException(nameof(cas), cas, "cas inconnu"),
        };
        var maintenant = instantRef.AddMinutes(5);
        var racine = RacineAvecTexte(RacineAppBureau.NomFichier(fixture), contenu, maintenant.AddMinutes(-1));
        var transcript = new SessionSnapshot(id, "Projet", SessionActivity.WaitingTurn, null,
                                             instantRef - TimeSpan.FromMilliseconds(1561));

        var lecture = Moniteur(new SourceModifiable(transcript), Lecteur(racine)).Inspecter(maintenant);

        var s = Assert.Single(lecture.Visibles);
        Assert.Equal(SessionActivity.WaitingTurn, s.Activity);
        Assert.Null(s.Reason);
        Assert.Empty(lecture.Desaccords);
        Assert.Equal(titre, s.Titre);                                   // lu et joint : le titre le prouve
        Assert.True(lecture.AppBureau!.ParSession.ContainsKey(id));
    }

    /// <summary>C10 — TRT-01 : un relais de l'app au transcript n'est PAS une réponse. La question de l'app puis le
    /// travail du transcript viennent de deux sources différentes : le détecteur ne conclut pas « répondu » et ne
    /// marque rien traité (inchangé par construction : NET-01 exige la même source aux deux cycles).</summary>
    [Fact]
    public void C10_un_relais_de_l_app_au_transcript_n_est_pas_une_reponse()
    {
        var treated = new TreatedStore(Path.Combine(Dossier(), "treated.json"), new FakeClock(A.AddMinutes(2)));
        var source = new SourceModifiable(TranscriptE());
        var moniteur = Moniteur(source, Lecteur(Racine(A.AddMinutes(1), Bloquee)), treated: treated);

        Assert.Equal(SessionActivity.WaitingAttention, Assert.Single(moniteur.Inspecter(A.AddMinutes(1)).Visibles).Activity);

        source.Sessions = new[] { new SessionSnapshot(IdE, "Projet-E", SessionActivity.Working, null, A.AddSeconds(90)) };
        var lecture = moniteur.Inspecter(A.AddMinutes(2));

        Assert.Equal(SessionActivity.Working, Assert.Single(lecture.Visibles).Activity);
        Assert.False(treated.Load().ContainsKey(IdE));
    }

    /// <summary>C11 — la règle de silence ne touche que le travail : une question de l'app vieille de 25 min reste
    /// « En attente », jamais « En attente ? ».</summary>
    [Fact]
    public void C11_le_silence_ne_touche_pas_une_question_de_l_app()
    {
        var maintenant = A.AddMinutes(25);

        var lecture = Moniteur(new SourceModifiable(TranscriptE()), Lecteur(Racine(maintenant, Bloquee))).Inspecter(maintenant);

        var s = Assert.Single(lecture.Visibles);
        Assert.Equal(SessionActivity.WaitingAttention, s.Activity);
        Assert.NotEqual(SessionActivity.WaitingDeduced, s.Activity);
    }

    // ═══ L'épisode, et le « traité » (TRT-02, NET-03) ══════════════════════════════════════════════════════════

    /// <summary>
    /// Piège 6 de la recherche : après la fin du tour, l'activité de fond fait avancer <c>lastActivityAt</c> (+3 min 10 s
    /// mesurés). Si la question était datée par cette valeur COURANTE, l'épisode se rajeunirait, NET-03 purgerait le
    /// « traité », et une session marquée traitée reviendrait sans avoir rien redemandé. Elle est datée par l'instant lu
    /// à la PREMIÈRE apparition du résumé : elle ne revient pas. Mutation (e) jouée : dater par la dernière activité
    /// fait rougir ce test.
    /// </summary>
    [Fact]
    public void Une_question_marquee_traitee_ne_revient_pas_quand_l_activite_de_fond_avance()
    {
        var treated = new TreatedStore(Path.Combine(Dossier(), "treated.json"), new FakeClock(A.AddMinutes(5)));
        var racine = Racine(A.AddMinutes(1), Bloquee);
        var moniteur = Moniteur(new SourceModifiable(TranscriptE()), Lecteur(racine), treated: treated);

        var avant = Assert.Single(moniteur.Inspecter(A.AddMinutes(1)).Visibles);
        Assert.Equal(SessionActivity.WaitingAttention, avant.Activity);
        Assert.Equal(A, avant.UpdatedAt);

        treated.Set(IdE, A.ToUnixTimeMilliseconds());   // le geste « marquer traitée » du ViewModel

        var fond = RacineAppBureau.Deriver(Bloquee, ("1790240406552", "1790240596552"));   // lastActivityAt + 3 min 10 s
        RacineAppBureau.Ecrire(racine, RacineAppBureau.NomFichier(Bloquee), fond, A.AddMinutes(3));
        var lecture = moniteur.Inspecter(A.AddMinutes(5));

        // L'activité de fond a bien été lue — l'épisode, lui, n'a pas bougé.
        Assert.Equal(A.AddSeconds(190), lecture.AppBureau!.ParSession[IdE].DerniereActivite);
        Assert.Equal(A, lecture.AppBureau.ParSession[IdE].InstantClassification);

        Assert.Empty(lecture.Visibles);
        var masquee = Assert.Single(lecture.Masquees);
        Assert.Equal(IdE, masquee.Session.SessionId);
        Assert.Equal(MotifMasquage.Traitee, masquee.Motif);
    }

    // ═══ La dégradation v1.6 EXACTE (APP-01) ══════════════════════════════════════════════════════════════════

    /// <summary>La même source de transcripts, le même fichier de hook, pour les deux moniteurs comparés.</summary>
    private (ISessionSource Source, string Hooks) Monde()
    {
        var source = new SourceModifiable(
            TranscriptE(),
            new SessionSnapshot("b-deux", "Proj-b", SessionActivity.Working, null, M.AddMinutes(-2)),
            new SessionSnapshot("c-trois", "Proj-c", SessionActivity.WaitingAttention, "permission_prompt", M.AddMinutes(-3)));
        var hooks = Dossier();
        EcrireHook(hooks, "b-deux", "Proj-b", SessionActivity.WaitingTurn, M.AddMinutes(-4));   // un désaccord à comparer
        return (source, hooks);
    }

    private void AssertMemeLecture(LectureSessions v16, LectureSessions autre)
    {
        Assert.Equal(3, v16.Visibles.Count);          // garde anti-muette : il y a quelque chose à comparer
        Assert.NotEmpty(v16.Desaccords);
        Assert.Equal(Sequence(v16.Visibles), Sequence(autre.Visibles));
        Assert.Equal(Sequence(v16.Masquees.Select(m => m.Session)), Sequence(autre.Masquees.Select(m => m.Session)));
        Assert.Equal(Desaccords(v16), Desaccords(autre));
        Assert.Equal(v16.FichiersEcartesParAnciennete, autre.FichiersEcartesParAnciennete);
    }

    /// <summary>Dossier de l'app absent : <c>Inspecter</c> rend EXACTEMENT la lecture v1.6 — mêmes identifiants, états,
    /// instants, motifs, dossiers, aucun titre, mêmes désaccords — et dit que le dossier n'a pas été trouvé.</summary>
    [Fact]
    public void Dossier_absent_rend_exactement_la_lecture_v1_6()
    {
        var (source, hooks) = Monde();
        var absent = Path.Combine(Dossier(), "rien");
        Assert.False(Directory.Exists(absent));

        var sans = Moniteur(source, null, hooks).Inspecter(M);
        var avecAbsent = Moniteur(source, new LecteurAppBureau(new[] { absent }), hooks).Inspecter(M);

        AssertMemeLecture(sans, avecAbsent);
        Assert.Null(sans.AppBureau);
        Assert.False(avecAbsent.AppBureau!.DossierTrouve);
    }

    /// <summary>Un fichier de l'app illisible ne change RIEN à la lecture v1.6 : il est compté, jamais une exception.</summary>
    [Fact]
    public void Un_fichier_illisible_rend_exactement_la_lecture_v1_6()
    {
        var (source, hooks) = Monde();
        var racine = RacineAvecTexte("local_x.json", "pas du JSON {{{", M.AddMinutes(-1));

        var sans = Moniteur(source, null, hooks).Inspecter(M);
        var illisible = Moniteur(source, Lecteur(racine), hooks).Inspecter(M);

        AssertMemeLecture(sans, illisible);
        Assert.True(illisible.AppBureau!.DossierTrouve);
        Assert.Equal(1, illisible.AppBureau.Illisibles);
    }
}
