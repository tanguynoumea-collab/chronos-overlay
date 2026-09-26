using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// LA LECTURE, versant MONITEUR (phase 30, plan 03) : une session LUE quitte le widget d'elle-même, et une session qui
/// TRAVAILLE n'en disparaît plus jamais.
///
/// <para><b>LUE-05 (V01 à V04).</b> Le filtre « traitée » du moniteur ne masque qu'une session dont l'état RETENU est une
/// attente (<see cref="AffichageSessions.EstUneAttente"/>). Répondue, lue ou marquée traitée, une session qui se remet à
/// travailler s'affiche « Réflexion » ; son prochain épisode d'attente plus récent la ramène (NET-03, détecteur) ; le
/// filtre ne purge rien.</para>
///
/// <para><b>Classes RÉELLES.</b> Un vrai <see cref="SessionMonitor"/>, un vrai <see cref="SessionTreatmentTracker"/> sur
/// un vrai <see cref="TreatedStore"/> et, quand le cas le demande, un vrai <see cref="LecteurAppBureau"/> sur une racine
/// temporaire à la forme exacte de l'app. Seules la source de transcripts (elle lirait le vrai <c>~/.claude/projects</c>)
/// et la sonde du premier plan (elle lirait le vrai premier plan de la machine) sont substituées.</para>
///
/// <para><b>Aucune écriture hors du dossier temporaire.</b> <see cref="ArchiveStore"/> et <see cref="TreatedStore"/>
/// TOUJOURS temporaires, datés par une horloge FIXE proche de l'instant du cas : la rétention de 24 h du magasin se
/// mesure à SON horloge. Tout ce qui est créé est supprimé à la fin de chaque test.</para>
/// </summary>
public sealed class MoniteurLectureTests : IDisposable
{
    /// <summary>L'instant de référence des cas LUE-05.</summary>
    private static readonly DateTimeOffset T = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    // ── Ce que chaque test crée, et qu'il supprime ─────────────────────────────────────────────────────────────
    private readonly List<string> _aSupprimer = new();

    public void Dispose()
    {
        foreach (var d in _aSupprimer)
            try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { }
    }

    private string Dossier()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-moniteur-lecture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);   // garde anti-accident
        _aSupprimer.Add(d);
        return d;
    }

    /// <summary>Un magasin « traité » temporaire, daté par une horloge fixe.</summary>
    private TreatedStore Traitees(DateTimeOffset horloge) => new(Path.Combine(Dossier(), "treated.json"), new FakeClock(horloge));

    /// <summary>Un magasin d'archives temporaire (le défaut lirait le vrai %APPDATA%).</summary>
    private ArchiveStore Archive(DateTimeOffset horloge) => new(Path.Combine(Dossier(), "archived.json"), new FakeClock(horloge));

    /// <summary>Le moniteur de production, aux racines près : le détecteur est TOUJOURS construit sur
    /// <paramref name="treated"/> ; le lecteur de l'app et la sonde du premier plan sont passés par argument nommé,
    /// comme en production.</summary>
    private SessionMonitor Moniteur(ISessionSource transcripts, TreatedStore treated, LecteurAppBureau? lecteur = null,
        ArchiveStore? archive = null, string? hooks = null, IPremierPlan? premierPlan = null)
        => new(hooks ?? Dossier(), transcripts, archive ?? Archive(T), treated, new SessionTreatmentTracker(treated),
               appBureau: lecteur, premierPlan: premierPlan);

    /// <summary>Une racine de l'app NEUVE et vide, sous le dossier temporaire, supprimée à la fin du test.</summary>
    private string Racine()
    {
        var r = RacineAppBureau.NouvelleRacine();
        _aSupprimer.Add(r);
        return r;
    }

    private static LecteurAppBureau Lecteur(string racine) => new(new[] { racine });

    /// <summary>Un magasin « traité » temporaire dont le chemin est rendu (pour la sentinelle de date d'écriture).</summary>
    private (TreatedStore Store, string Chemin) TraiteesAvecChemin(DateTimeOffset horloge)
    {
        var chemin = Path.Combine(Dossier(), "treated.json");
        return (new TreatedStore(chemin, new FakeClock(horloge)), chemin);
    }

    /// <summary>Un instant du 2026-09-25, en UTC (heure de Paris = UTC+2 ce jour-là : 16:08 local = 14:08Z).</summary>
    private static DateTimeOffset U(int h, int m, int s = 0, int ms = 0) => new(2026, 9, 25, h, m, s, ms, TimeSpan.Zero);

    /// <summary>Les visibles, champ par champ SAUF le titre (APP-02 : un titre n'est pas un masquage), triées.</summary>
    private static string Projection(IEnumerable<SessionSnapshot> sessions)
        => string.Join(" | ", sessions.Select(s => $"{s.SessionId}={s.Activity}@{s.UpdatedAt:O}|{s.Reason}|{s.Project}")
                                      .OrderBy(x => x, StringComparer.Ordinal));

    /// <summary>Les masquées : identifiant et motif, triés.</summary>
    private static string Motifs(LectureSessions l)
        => string.Join(" | ", l.Masquees.Select(m => $"{m.Session.SessionId}|{m.Motif}").OrderBy(x => x, StringComparer.Ordinal));

    /// <summary>Le contenu du magasin « traité », trié.</summary>
    private static string Magasin(TreatedStore t)
        => string.Join(" | ", t.Load().OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"));

    // ── Le relevé réel (27-RELEVE, 30-RESEARCH Q5.a) ───────────────────────────────────────────────────────────
    private const string PreteARevue = "fin-de-tour-review-ready.json";
    private const string SansResume = "sans-postTurnSummary.json";
    private const string Terminee = "fin-de-tour-completed.json";
    private const string GesteB = "session-courante-geste-b.json";
    private const string Bloquee = "fin-de-tour-blocked.json";
    private const string SansId = "sans-cliSessionId.json";

    private const string Jarvis = "c17a1b03-3c8d-4c05-a747-dd77bc702e1b";    // review_ready (ne dépose rien), « JARVIS »
    private const string Adv = "88677186-8690-44dc-ac77-7a319a0aa5cb";       // « ADVANCED SHEET »
    private const string ProjetC = "7bc776ff-8019-4f2e-a942-83d2ac6490e6";   // completed, la 3e session du relevé
    private const string B = "11456cab-d447-42c7-aa85-9920ce64f7ba";         // gestes A et B
    private const string IdE = "adac2711-86a4-4b4d-b71c-9a594c9d959e";       // blocked, la question de l'app
    private const string IdD = "dae57d29-d277-4e4c-8cad-b4d2b97701e5";       // l'identifiant de sans-postTurnSummary.json

    /// <summary>La source de transcripts SUBSTITUÉE, réaffectable entre deux cycles.</summary>
    private sealed class SourceModifiable : ISessionSource
    {
        public IReadOnlyList<SessionSnapshot> Sessions { get; set; }
        public SourceModifiable(params SessionSnapshot[] sessions) => Sessions = sessions;
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => Sessions;
    }

    private static SessionSnapshot Snap(string id, string projet, SessionActivity a, DateTimeOffset maj)
        => new(id, projet, a, null, maj);

    // ═══ LUE-05 — le filtre ne masque que les attentes ═══════════════════════════════════════════════════════

    /// <summary>V01 — une session marquée traitée qui se remet à travailler s'affiche « Réflexion » ; son entrée reste
    /// dans le magasin (le filtre ne purge rien : seul le détecteur ajoute et retire).</summary>
    [Fact]
    public void Une_session_traitee_qui_travaille_est_visible_Reflexion()
    {
        var treated = Traitees(T);
        treated.Set("s", T.ToUnixTimeMilliseconds());   // le geste « Marquer traitée », pour l'attente de T
        var source = new SourceModifiable(Snap("s", "Proj", SessionActivity.Working, T.AddMinutes(1)));
        var moniteur = Moniteur(source, treated);

        var lecture = moniteur.Inspecter(T.AddMinutes(1));

        var visible = Assert.Single(lecture.Visibles);
        Assert.Equal("s", visible.SessionId);
        Assert.Equal(SessionActivity.Working, visible.Activity);
        Assert.Empty(lecture.Masquees);
        Assert.True(treated.Load().ContainsKey("s"), "le filtre ne purge rien : l'entrée reste jusqu'au prochain épisode");
    }

    /// <summary>V02 — suite de V01 : le prochain tour fini, plus récent que l'épisode traité, la ramène « En attente »
    /// et le détecteur purge son entrée (NET-03, inchangé).</summary>
    [Fact]
    public void Son_prochain_tour_fini_la_ramene_En_attente()
    {
        var treated = Traitees(T);
        treated.Set("s", T.ToUnixTimeMilliseconds());
        var source = new SourceModifiable(Snap("s", "Proj", SessionActivity.Working, T.AddMinutes(1)));
        var moniteur = Moniteur(source, treated);

        var enTravail = moniteur.Inspecter(T.AddMinutes(1));
        Assert.Contains(enTravail.Visibles, s => s.SessionId == "s" && s.Activity == SessionActivity.Working);

        source.Sessions = new[] { Snap("s", "Proj", SessionActivity.WaitingTurn, T.AddMinutes(5)) };
        var lecture = moniteur.Inspecter(T.AddMinutes(5));

        var visible = Assert.Single(lecture.Visibles);
        Assert.Equal("s", visible.SessionId);
        Assert.Equal(SessionActivity.WaitingTurn, visible.Activity);
        Assert.Empty(lecture.Masquees);
        Assert.False(treated.Load().ContainsKey("s"), "un épisode plus récent que le traitement purge l'entrée (NET-03)");
    }

    /// <summary>V03 — traitée ET indéterminée : l'état retenu n'est pas une attente, le filtre « traitée » ne la prend
    /// pas ; elle est masquée parce qu'elle n'a pas de ligne (LIB-01), avec ce motif-là.</summary>
    [Fact]
    public void Traitee_et_indeterminee_est_masquee_indeterminee()
    {
        var treated = Traitees(T);
        treated.Set("u", T.ToUnixTimeMilliseconds());
        var moniteur = Moniteur(new SourceModifiable(Snap("u", "Proj", SessionActivity.Unknown, T)), treated);

        var lecture = moniteur.Inspecter(T);

        Assert.Empty(lecture.Visibles);
        var masquee = Assert.Single(lecture.Masquees);
        Assert.Equal("u", masquee.Session.SessionId);
        Assert.Equal(MotifMasquage.Indeterminee, masquee.Motif);
    }

    /// <summary>V04 — une attente DÉDUITE (silence de 25 min) plus ancienne que le traitement est une attente : elle
    /// reste masquée, motif résiduel <see cref="MotifMasquage.Traitee"/>, sans cause (un geste que le détecteur n'a pas
    /// constaté).</summary>
    [Fact]
    public void Traitee_et_deduite_plus_ancienne_que_le_traitement_reste_masquee()
    {
        var treated = Traitees(T);
        treated.Set("d", T.ToUnixTimeMilliseconds());
        var moniteur = Moniteur(new SourceModifiable(Snap("d", "Proj", SessionActivity.Working, T.AddMinutes(-25))), treated);

        var lecture = moniteur.Inspecter(T);

        Assert.Empty(lecture.Visibles);
        var masquee = Assert.Single(lecture.Masquees);
        Assert.Equal("d", masquee.Session.SessionId);
        Assert.Equal(SessionActivity.WaitingDeduced, masquee.Session.Activity);   // déduite par le moniteur (SIL-01)
        Assert.Equal(MotifMasquage.Traitee, masquee.Motif);
        Assert.Null(masquee.Cause);
        Assert.True(treated.Load().ContainsKey("d"));
    }

    // ═══ LUE-01 et LUE-02 au moniteur réel, avec le lecteur réel et les fixtures du relevé ═══════════════════════

    /// <summary>Le montage du relevé du 2026-09-25 à 16 h 08 (heure de Paris), en UTC : quatre fichiers de l'app dérivés
    /// TEXTUELLEMENT sur leur <c>lastFocusedAt</c>, quatre transcripts, <c>explorer</c> au premier plan (LUE-01 seule).</summary>
    private (SessionMonitor Moniteur, TreatedStore Traitees, string Chemin) MontageReleve(DateTimeOffset maintenant)
    {
        var racine = Racine();
        RacineAppBureau.Ecrire(racine, "local_jarvis.json",
            RacineAppBureau.Deriver(PreteARevue, ("1790361032950", "1790344839000")), U(14, 1));             // focus 14:00:39Z
        RacineAppBureau.Ecrire(racine, "local_adv.json",
            RacineAppBureau.Deriver(SansResume, (IdD, Adv), ("1790361025541", "1790344859000")), U(14, 1)); // focus 14:00:59Z
        RacineAppBureau.Ecrire(racine, "local_c.json",
            RacineAppBureau.Deriver(Terminee, ("1790321046116", "1790344800000")), U(14, 5));                // focus 14:00Z
        RacineAppBureau.Ecrire(racine, "local_b.json",
            RacineAppBureau.Deriver(GesteB, ("1790361044976", "1790344800000")), U(14, 5));                  // focus 14:00Z
        var transcripts = new SourceModifiable(
            Snap(Jarvis, "JARVIS", SessionActivity.WaitingTurn, U(13, 59, 5)),
            Snap(Adv, "ADVANCED SHEET", SessionActivity.WaitingTurn, U(13, 56, 10)),
            Snap(ProjetC, "Projet-C", SessionActivity.WaitingTurn, U(14, 5)),
            Snap(B, "Projet-A", SessionActivity.Working, U(14, 7, 30)));
        var (treated, chemin) = TraiteesAvecChemin(maintenant);
        var moniteur = Moniteur(transcripts, treated, Lecteur(racine), premierPlan: FakePremierPlan.Autre("explorer"));
        return (moniteur, treated, chemin);
    }

    /// <summary>M01 — critère 1. JARVIS (finie 15:59:05, ouverte 16:00:39) et ADVANCED SHEET (finie 15:56:10, ouverte
    /// 16:00:59) sont masquées « lue » avec leurs deux instants ; la session finie à 16:05 après un focus à 16:00 reste
    /// « En attente » ; celle qui travaille reste « Réflexion ». Le magasin porte exactement les deux épisodes lus.</summary>
    [Fact]
    public void Le_releve_de_16_h_08_ne_se_reproduit_plus()
    {
        var maintenant = U(14, 8);
        var (moniteur, treated, _) = MontageReleve(maintenant);

        var lecture = moniteur.Inspecter(maintenant);

        Assert.Equal(new[] { $"{B}={SessionActivity.Working}", $"{ProjetC}={SessionActivity.WaitingTurn}" },
            lecture.Visibles.Select(s => $"{s.SessionId}={s.Activity}").OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(2, lecture.Masquees.Count);
        Assert.All(lecture.Masquees, m => Assert.Equal(MotifMasquage.LueParFocus, m.Motif));

        var jarvis = Assert.Single(lecture.Masquees, m => m.Session.SessionId == Jarvis).Cause;
        Assert.NotNull(jarvis);
        Assert.Equal(U(14, 0, 39), jarvis!.Focus);
        Assert.Equal(U(13, 59, 5), jarvis.Attente);

        var adv = Assert.Single(lecture.Masquees, m => m.Session.SessionId == Adv).Cause;
        Assert.NotNull(adv);
        Assert.Equal(U(14, 0, 59), adv!.Focus);
        Assert.Equal(U(13, 56, 10), adv.Attente);

        Assert.Equal($"{Adv}=1790344570000 | {Jarvis}=1790344745000", Magasin(treated));   // les deux épisodes lus, rien d'autre
    }

    /// <summary>M02 — geste A : le retour par alt-tab fait passer le focus de 16:23:51 à 20:30:44 (heure de Paris) ;
    /// l'attente de 20:06 devient lue par LUE-01, <c>explorer</c> au premier plan.</summary>
    [Fact]
    public void Geste_A_le_retour_alt_tab_rend_la_session_lue()
    {
        var racine = Racine();
        RacineAppBureau.Ecrire(racine, "local_b.json",
            RacineAppBureau.Deriver(GesteB, ("1790361044976", "1790346231000")), U(18, 9));                  // focus 14:23:51Z
        var transcripts = new SourceModifiable(Snap(B, "Projet-A", SessionActivity.WaitingTurn, U(18, 6)));
        var treated = Traitees(U(18, 10));
        var moniteur = Moniteur(transcripts, treated, Lecteur(racine), premierPlan: FakePremierPlan.Autre("explorer"));

        var avant = moniteur.Inspecter(U(18, 10));
        Assert.Contains(avant.Visibles, s => s.SessionId == B && s.Activity == SessionActivity.WaitingTurn);
        Assert.Empty(avant.Masquees);
        Assert.Empty(treated.Load());

        // Le retour : l'app réécrit le fichier, focus 18:30:44.976Z (la fixture réelle du relevé, octets exacts).
        RacineAppBureau.EcrireOctets(racine, "local_b.json", RacineAppBureau.Fixture(GesteB), U(18, 31, 9));
        var apres = moniteur.Inspecter(U(18, 31, 30));

        Assert.Empty(apres.Visibles);
        var masquee = Assert.Single(apres.Masquees);
        Assert.Equal(B, masquee.Session.SessionId);
        Assert.Equal(MotifMasquage.LueParFocus, masquee.Motif);
        Assert.Equal(U(18, 30, 44, 976), masquee.Cause!.Focus);
        Assert.Equal(U(18, 6), masquee.Cause.Attente);
        Assert.Equal(1790359560000, treated.Load()[B]);
    }

    /// <summary>M03 — geste B : claude au premier plan depuis 20:33:27 (Paris), la fin de tour de 20:34:04.328 est lue
    /// 2,5 s plus tard, borne incluse — visible à 18:34:06.827Z, lue à 18:34:06.828Z. La sélection est celle du lecteur.</summary>
    [Fact]
    public void Geste_B_la_session_regardee_est_lue_au_premier_plan()
    {
        var racine = Racine();
        RacineAppBureau.EcrireOctets(racine, "local_b.json", RacineAppBureau.Fixture(GesteB), U(18, 34));
        var fin = U(18, 34, 4, 328);
        var transcripts = new SourceModifiable(Snap(B, "Projet-A", SessionActivity.WaitingTurn, fin));
        var treated = Traitees(U(18, 34));
        var moniteur = Moniteur(transcripts, treated, Lecteur(racine), premierPlan: FakePremierPlan.Claude(U(18, 33, 27)));

        var avantGrace = moniteur.Inspecter(U(18, 34, 6, 827));
        Assert.Contains(avantGrace.Visibles, s => s.SessionId == B && s.Activity == SessionActivity.WaitingTurn);
        Assert.Empty(avantGrace.Masquees);
        Assert.Empty(treated.Load());

        var lecture = moniteur.Inspecter(U(18, 34, 6, 828));

        Assert.Empty(lecture.Visibles);
        var masquee = Assert.Single(lecture.Masquees);
        Assert.Equal(B, masquee.Session.SessionId);
        Assert.Equal(MotifMasquage.LueAuPremierPlan, masquee.Motif);
        Assert.Equal(U(18, 33, 27), masquee.Cause!.PremierPlanDepuis);
        Assert.Equal(U(18, 34, 6, 828), masquee.Cause.Constat);
        Assert.Equal(fin, masquee.Cause.Attente);
        Assert.Equal(B, lecture.AppBureau!.Selection!.CliSessionId);
        Assert.Equal(fin.ToUnixTimeMilliseconds(), treated.Load()[B]);
    }

    /// <summary>M03b — le même tour fini, <c>explorer</c> au premier plan : personne ne regarde, la session reste
    /// « En attente » (le focus, antérieur à la fin du tour, ne suffit pas).</summary>
    [Fact]
    public void Geste_B_avec_explorer_au_premier_plan_reste_en_attente()
    {
        var racine = Racine();
        RacineAppBureau.EcrireOctets(racine, "local_b.json", RacineAppBureau.Fixture(GesteB), U(18, 34));
        var transcripts = new SourceModifiable(Snap(B, "Projet-A", SessionActivity.WaitingTurn, U(18, 34, 4, 328)));
        var treated = Traitees(U(18, 34));
        var moniteur = Moniteur(transcripts, treated, Lecteur(racine), premierPlan: FakePremierPlan.Autre("explorer"));

        var lecture = moniteur.Inspecter(U(18, 34, 10));

        var visible = Assert.Single(lecture.Visibles);
        Assert.Equal(B, visible.SessionId);
        Assert.Equal(SessionActivity.WaitingTurn, visible.Activity);
        Assert.Empty(lecture.Masquees);
        Assert.Empty(treated.Load());
    }

    /// <summary>M04 — le dernier focus appartient à une session NEUVE, sans <c>cliSessionId</c> : la sélection ne désigne
    /// personne, et la session finie reste visible malgré claude au premier plan (le moniteur prend la sélection du
    /// lecteur, jamais le maximum des seules sessions jointes).</summary>
    [Fact]
    public void Le_dernier_focus_sur_un_fichier_sans_cliSessionId_ne_selectionne_personne()
    {
        var racine = Racine();
        RacineAppBureau.EcrireOctets(racine, "local_b.json", RacineAppBureau.Fixture(GesteB), U(18, 34));
        RacineAppBureau.Ecrire(racine, "local_neuve.json",
            RacineAppBureau.Deriver(SansId, ("1781190743865", "1790361100000")), U(18, 34));                    // focus 18:31:40Z
        var transcripts = new SourceModifiable(Snap(B, "Projet-A", SessionActivity.WaitingTurn, U(18, 34, 4, 328)));
        var treated = Traitees(U(18, 35));
        var moniteur = Moniteur(transcripts, treated, Lecteur(racine), premierPlan: FakePremierPlan.Claude(U(18, 33, 27)));

        var lecture = moniteur.Inspecter(U(18, 35));

        var selection = lecture.AppBureau!.Selection;
        Assert.NotNull(selection);
        Assert.Null(selection!.CliSessionId);
        Assert.Equal(U(18, 31, 40), selection.DernierFocus);
        Assert.Contains(lecture.Visibles, s => s.SessionId == B && s.Activity == SessionActivity.WaitingTurn);
        Assert.Empty(lecture.Masquees);
        Assert.Empty(treated.Load());
    }

    /// <summary>M05 — exigé par le verrou (LUE-04) : un fichier de l'app SANS <c>cliSessionId</c> ne joint personne ; la
    /// règle ne s'applique pas, même avec claude au premier plan et un focus plus récent que l'attente. La lecture est
    /// celle d'un moniteur sans lecteur ni sonde.</summary>
    [Fact]
    public void Sans_cliSessionId_la_regle_ne_s_applique_pas()
    {
        var racine = Racine();
        RacineAppBureau.Ecrire(racine, "local_neuve.json",
            RacineAppBureau.Deriver(SansId, ("1781190743865", "1790361600000")), U(18, 44));                    // focus 18:40Z
        var transcripts = new SourceModifiable(Snap(ProjetC, "Projet-C", SessionActivity.WaitingTurn, U(18, 30)));
        var treated = Traitees(U(18, 45));
        var moniteur = Moniteur(transcripts, treated, Lecteur(racine), premierPlan: FakePremierPlan.Claude(U(18, 0)));
        var temoin = Moniteur(transcripts, Traitees(U(18, 45)));

        var lecture = moniteur.Inspecter(U(18, 45));
        var reference = temoin.Inspecter(U(18, 45));

        Assert.Contains(lecture.Visibles, s => s.SessionId == ProjetC && s.Activity == SessionActivity.WaitingTurn);
        Assert.Empty(lecture.Masquees);
        Assert.Empty(treated.Load());
        Assert.True(lecture.AppBureau!.DossierTrouve);
        Assert.Empty(lecture.AppBureau.ParSession);
        Assert.Equal(Projection(reference.Visibles), Projection(lecture.Visibles));
    }

    /// <summary>M06 — LUE-04 : sans métadonnée lisible (dossier de l'app absent, fichier illisible, focus illisible), la
    /// lecture est, cycle après cycle, celle de la v1.6 : mêmes visibles, mêmes masquées, même magasin — y compris une
    /// réponse (NET-01) au troisième cycle. Claude au premier plan n'y change rien.</summary>
    [Theory]
    [InlineData("dossier-absent")]
    [InlineData("fichier-illisible")]
    [InlineData("focus-illisible")]
    public void Sans_metadonnees_lisibles_la_lecture_est_celle_de_la_v1_6(string cas)
    {
        string racine;
        switch (cas)
        {
            case "dossier-absent":
                racine = Path.Combine(Path.GetTempPath(), "chronos-appbureau-absent-" + Guid.NewGuid().ToString("N"));
                Assert.False(Directory.Exists(racine));
                break;
            case "fichier-illisible":
                racine = Racine();
                RacineAppBureau.Ecrire(racine, "local_x.json", "pas du JSON {{{", U(18, 39));
                break;
            case "focus-illisible":
                racine = Racine();
                RacineAppBureau.Ecrire(racine, "local_b.json",
                    RacineAppBureau.Deriver(GesteB, ("\"lastFocusedAt\": 1790361044976", "\"lastFocusedAt\": \"illisible\"")), U(18, 39));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(cas), cas, "cas inconnu");
        }

        var transcripts = new SourceModifiable();
        var treated = Traitees(U(18, 40));
        var moniteur = Moniteur(transcripts, treated, Lecteur(racine), premierPlan: FakePremierPlan.Claude(U(18, 0)));
        var treatedV16 = Traitees(U(18, 40));
        var v16 = Moniteur(transcripts, treatedV16);

        var cycles = new (DateTimeOffset Maintenant, SessionSnapshot[] Sessions)[]
        {
            (U(18, 40), new[] { Snap(B, "Projet-A", SessionActivity.WaitingTurn, U(18, 20)),
                                Snap(ProjetC, "Projet-C", SessionActivity.Working, U(18, 39)) }),
            (U(18, 41), new[] { Snap(B, "Projet-A", SessionActivity.WaitingTurn, U(18, 20)),
                                Snap(ProjetC, "Projet-C", SessionActivity.WaitingTurn, U(18, 40, 30)) }),
            (U(18, 42), new[] { Snap(B, "Projet-A", SessionActivity.Working, U(18, 41, 30)),
                                Snap(ProjetC, "Projet-C", SessionActivity.WaitingTurn, U(18, 40, 30)) }),
        };
        foreach (var (maintenant, sessions) in cycles)
        {
            transcripts.Sessions = sessions;
            var lecture = moniteur.Inspecter(maintenant);
            var reference = v16.Inspecter(maintenant);

            Assert.Equal(Projection(reference.Visibles), Projection(lecture.Visibles));
            Assert.Equal(Motifs(reference), Motifs(lecture));
            Assert.Equal(Magasin(treatedV16), Magasin(treated));
        }

        // Les cycles ne sont pas passés à vide : la réponse du troisième cycle est inscrite des deux côtés (NET-01), et
        // la session qui travaille est visible (LUE-05).
        Assert.True(treated.Load().ContainsKey(B));
    }

    /// <summary>M07 — LUE-04 : la sonde du premier plan ne sait pas ; LUE-02 s'éteint (la session sélectionnée, finie
    /// sous les yeux, reste visible), LUE-01 reste seule (JARVIS, ouverte après sa fin, est lue). Le constat de la sonde
    /// est porté par la lecture.</summary>
    [Fact]
    public void Premier_plan_indisponible_LUE_02_inactive_LUE_01_seule()
    {
        var racine = Racine();
        RacineAppBureau.EcrireOctets(racine, "local_b.json", RacineAppBureau.Fixture(GesteB), U(18, 34));           // focus 18:30:44.976Z
        RacineAppBureau.EcrireOctets(racine, "local_jarvis.json", RacineAppBureau.Fixture(PreteARevue), U(18, 34)); // focus 18:30:32.950Z
        var transcripts = new SourceModifiable(
            Snap(B, "Projet-A", SessionActivity.WaitingTurn, U(18, 34, 4, 328)),
            Snap(Jarvis, "JARVIS", SessionActivity.WaitingTurn, U(18, 20)));
        var treated = Traitees(U(18, 35));
        var moniteur = Moniteur(transcripts, treated, Lecteur(racine),
            premierPlan: FakePremierPlan.Indisponible("GetWindowThreadProcessId a échoué"));

        var lecture = moniteur.Inspecter(U(18, 35));

        Assert.Equal(B, lecture.AppBureau!.Selection!.CliSessionId);   // B serait lue au premier plan si la sonde savait
        Assert.Contains(lecture.Visibles, s => s.SessionId == B && s.Activity == SessionActivity.WaitingTurn);
        var masquee = Assert.Single(lecture.Masquees);
        Assert.Equal(Jarvis, masquee.Session.SessionId);
        Assert.Equal(MotifMasquage.LueParFocus, masquee.Motif);
        Assert.Equal(StatutPremierPlan.Indisponible, lecture.PremierPlan!.Statut);
        Assert.Equal("GetWindowThreadProcessId a échoué", lecture.PremierPlan.Raison);
    }

    /// <summary>M08 — le scénario mesuré des 478 minutes (<c>TreatedSessionsTests</c>) rejoué AVEC la règle branchée : la
    /// session est connue de l'app, son focus précède son attente, <c>explorer</c> est au premier plan. Expirer n'est
    /// toujours pas répondre, et un focus ancien n'est pas une lecture : visible aux deux cycles, magasin vide.</summary>
    [Fact]
    public void Le_scenario_des_478_minutes_reste_visible_avec_la_regle_lue()
    {
        const string Id = "e465420e-83e0-428f-97f1-f0174c0848fc";
        var tHook = DateTimeOffset.FromUnixTimeMilliseconds(1789181509266);
        var tEpisode = DateTimeOffset.FromUnixTimeMilliseconds(1789210240523);
        Assert.Equal(478, (int)(tEpisode - tHook).TotalMinutes);

        var hooks = Dossier();
        File.WriteAllText(Path.Combine(hooks, Id + ".json"),
            $$"""{"session_id":"{{Id}}","project":"PROJET ADVANCED SHEET","activity":"WaitingAttention","reason":"permission_prompt","updated_at":{{tHook.ToUnixTimeMilliseconds()}}}""");
        var racine = Racine();
        RacineAppBureau.Ecrire(racine, "local_e465.json",
            RacineAppBureau.Deriver(SansResume, (IdD, Id), ("1790361025541", "1789181000000")), tEpisode.AddMinutes(-1));

        var transcripts = new SourceModifiable();
        var horloge = new FakeClock(tEpisode);
        var treated = new TreatedStore(Path.Combine(Dossier(), "treated.json"), horloge);
        var moniteur = Moniteur(transcripts, treated, Lecteur(racine), hooks: hooks,
            premierPlan: FakePremierPlan.Autre("explorer"));

        var redemarrage = moniteur.Inspecter(tEpisode);
        Assert.True(redemarrage.AppBureau!.ParSession.ContainsKey(Id));   // la règle a de quoi s'appliquer
        Assert.Contains(redemarrage.Visibles, s => s.SessionId == Id);
        Assert.DoesNotContain(redemarrage.Masquees, m => m.Session.SessionId == Id);
        Assert.Empty(treated.Load());

        var apres = tEpisode.AddMinutes(3);
        horloge.UtcNow = apres;
        transcripts.Sessions = new[] { new SessionSnapshot(Id, "PROJET ADVANCED SHEET", SessionActivity.Working, null, apres) };
        var lecture = moniteur.Inspecter(apres);

        Assert.Contains(lecture.Visibles, s => s.SessionId == Id);
        Assert.DoesNotContain(lecture.Masquees, m => m.Session.SessionId == Id);
        Assert.False(treated.Load().ContainsKey(Id), "expirer n'est pas répondre, un focus ancien n'est pas une lecture");
    }

    /// <summary>M09 — les gestes de la v1.6 restent ce qu'ils étaient : « Archiver » prime sur une lecture (JARVIS, lue ET
    /// archivée, n'est annoncée qu'une fois, archivée) ; « Marquer traitée » donne le motif Traitee, sans cause.</summary>
    [Fact]
    public void Marquer_traitee_et_archiver_restent_ceux_de_la_v1_6()
    {
        var racine = Racine();
        RacineAppBureau.EcrireOctets(racine, "local_b.json", RacineAppBureau.Fixture(GesteB), U(18, 39));
        RacineAppBureau.EcrireOctets(racine, "local_jarvis.json", RacineAppBureau.Fixture(PreteARevue), U(18, 39));
        var transcripts = new SourceModifiable(
            Snap(B, "Projet-A", SessionActivity.WaitingTurn, U(18, 35)),      // après son focus (18:30:44.976Z) : non lue
            Snap(Jarvis, "JARVIS", SessionActivity.WaitingTurn, U(18, 20)));  // avant son focus (18:30:32.950Z) : lue
        var archive = Archive(U(18, 40));
        archive.Add(Jarvis);
        var treated = Traitees(U(18, 40));
        var moniteur = Moniteur(transcripts, treated, Lecteur(racine), archive: archive,
            premierPlan: FakePremierPlan.Autre("explorer"));

        var lecture = moniteur.Inspecter(U(18, 40));

        var jarvis = Assert.Single(lecture.Masquees);
        Assert.Equal(Jarvis, jarvis.Session.SessionId);
        Assert.Equal(MotifMasquage.Archivee, jarvis.Motif);
        Assert.True(treated.Load().ContainsKey(Jarvis), "JARVIS est lue (inscrite) : c'est l'archivage qui prime à l'annonce");
        Assert.Contains(lecture.Visibles, s => s.SessionId == B && s.Activity == SessionActivity.WaitingTurn);

        treated.Set(B, U(18, 35).ToUnixTimeMilliseconds());                   // le geste « Marquer traitée »
        var apresGeste = moniteur.Inspecter(U(18, 40, 2));

        Assert.DoesNotContain(apresGeste.Visibles, s => s.SessionId == B);
        var b = Assert.Single(apresGeste.Masquees, m => m.Session.SessionId == B);
        Assert.Equal(MotifMasquage.Traitee, b.Motif);
        Assert.Null(b.Cause);
    }

    /// <summary>M10 — la question posée par l'app (<c>blocked</c>, APP-03) est une attente comme une autre : ouverte
    /// après son épisode, elle est lue. L'attente comparée est l'épisode de la QUESTION (09:00:06.552Z), pas la fin du
    /// tour du transcript.</summary>
    [Fact]
    public void La_question_de_l_app_lue_est_masquee()
    {
        var m = new DateTimeOffset(2026, 9, 24, 9, 5, 6, 552, TimeSpan.Zero);
        var racine = Racine();
        RacineAppBureau.Ecrire(racine, "local_e.json",
            RacineAppBureau.Deriver(Bloquee, ("1790240046504", "1790240500000")), m.AddMinutes(-1));   // focus 09:01:40Z
        var transcripts = new SourceModifiable(
            Snap(IdE, "Projet-E", SessionActivity.WaitingTurn, new DateTimeOffset(2026, 9, 24, 9, 0, 4, 991, TimeSpan.Zero)));
        var treated = Traitees(m);
        var moniteur = Moniteur(transcripts, treated, Lecteur(racine), premierPlan: FakePremierPlan.Autre("explorer"));

        var lecture = moniteur.Inspecter(m);

        Assert.Empty(lecture.Visibles);
        var masquee = Assert.Single(lecture.Masquees);
        Assert.Equal(IdE, masquee.Session.SessionId);
        Assert.Equal(SessionActivity.WaitingAttention, masquee.Session.Activity);
        Assert.Equal(MotifMasquage.LueParFocus, masquee.Motif);
        Assert.Equal(new DateTimeOffset(2026, 9, 24, 9, 0, 6, 552, TimeSpan.Zero), masquee.Cause!.Attente);
        Assert.Equal(new DateTimeOffset(2026, 9, 24, 9, 1, 40, TimeSpan.Zero), masquee.Cause.Focus);
    }

    /// <summary>M11 — LUE-05 de bout en bout : lue, puis répondue (visible « Réflexion » pendant son travail, entrée
    /// gardée), puis revenue sur un nouveau tour fini (NET-03), puis relue sur un nouveau focus.</summary>
    [Fact]
    public void Lue_puis_repondue_puis_revenue_puis_relue()
    {
        var racine = Racine();
        RacineAppBureau.EcrireOctets(racine, "local_b.json", RacineAppBureau.Fixture(GesteB), U(18, 31, 9));  // focus 18:30:44.976Z
        var transcripts = new SourceModifiable(Snap(B, "Projet-A", SessionActivity.WaitingTurn, U(18, 20)));
        var treated = Traitees(U(18, 32));
        var moniteur = Moniteur(transcripts, treated, Lecteur(racine), premierPlan: FakePremierPlan.Autre("explorer"));

        var lue = moniteur.Inspecter(U(18, 32));
        Assert.Empty(lue.Visibles);
        Assert.Equal(MotifMasquage.LueParFocus, Assert.Single(lue.Masquees).Motif);

        transcripts.Sessions = new[] { Snap(B, "Projet-A", SessionActivity.Working, U(18, 32, 30)) };
        var repondue = moniteur.Inspecter(U(18, 33));
        Assert.Contains(repondue.Visibles, s => s.SessionId == B && s.Activity == SessionActivity.Working);
        Assert.Empty(repondue.Masquees);
        Assert.Equal(U(18, 20).ToUnixTimeMilliseconds(), treated.Load()[B]);

        transcripts.Sessions = new[] { Snap(B, "Projet-A", SessionActivity.WaitingTurn, U(18, 35)) };
        var revenue = moniteur.Inspecter(U(18, 36));
        Assert.Contains(revenue.Visibles, s => s.SessionId == B && s.Activity == SessionActivity.WaitingTurn);
        Assert.Empty(revenue.Masquees);
        Assert.False(treated.Load().ContainsKey(B));

        RacineAppBureau.Ecrire(racine, "local_b.json",
            RacineAppBureau.Deriver(GesteB, ("1790361044976", "1790361420000")), U(18, 37, 5));                // focus 18:37Z
        var relue = moniteur.Inspecter(U(18, 38));
        Assert.Empty(relue.Visibles);
        var masquee = Assert.Single(relue.Masquees);
        Assert.Equal(MotifMasquage.LueParFocus, masquee.Motif);
        Assert.Equal(U(18, 35), masquee.Cause!.Attente);
        Assert.Equal(U(18, 37), masquee.Cause.Focus);
    }

    /// <summary>M12 — critère 2 : treated.json n'est pas réécrit à chaque rafraîchissement. Après le premier cycle, la date
    /// d'écriture du fichier est posée à une SENTINELLE ; cinq cycles plus tard elle est intacte, et les deux sessions
    /// lues sont toujours masquées.</summary>
    [Fact]
    public void Treated_json_n_est_pas_reecrit_a_chaque_rafraichissement()
    {
        var maintenant = U(14, 8);
        var (moniteur, _, chemin) = MontageReleve(maintenant);
        var sentinelle = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var premiere = moniteur.Inspecter(maintenant);
        Assert.Equal(2, premiere.Masquees.Count);
        Assert.True(File.Exists(chemin));
        File.SetLastWriteTimeUtc(chemin, sentinelle);

        for (var i = 1; i <= 5; i++)
        {
            var lecture = moniteur.Inspecter(maintenant.AddSeconds(2 * i));
            Assert.Equal(2, lecture.Masquees.Count);
            Assert.All(lecture.Masquees, m => Assert.Equal(MotifMasquage.LueParFocus, m.Motif));
            Assert.Equal(sentinelle, File.GetLastWriteTimeUtc(chemin));
        }
    }

    /// <summary>M13 — OBS-01 : la lecture porte l'état du premier plan que le moniteur a vu, par UN appel à la sonde ; la
    /// sonde est lue même sans source app-bureau (le rapport doit pouvoir dire ce qu'il voit) ; sans sonde, « non
    /// branchée ».</summary>
    [Fact]
    public void La_lecture_porte_l_etat_du_premier_plan_vu_par_le_moniteur()
    {
        var racine = Racine();
        RacineAppBureau.EcrireOctets(racine, "local_b.json", RacineAppBureau.Fixture(GesteB), U(18, 34));
        var transcripts = new SourceModifiable(Snap(B, "Projet-A", SessionActivity.WaitingTurn, U(18, 34, 4, 328)));
        var sonde = FakePremierPlan.Claude(U(18, 33, 27));

        var avecLecteur = Moniteur(transcripts, Traitees(U(18, 34)), Lecteur(racine), premierPlan: sonde);
        var lecture = avecLecteur.Inspecter(U(18, 34));
        Assert.Same(sonde.Etat, lecture.PremierPlan);
        Assert.Equal(new[] { U(18, 34) }, sonde.Appels);
        Assert.Same(sonde, avecLecteur.PremierPlan);

        var sansLecteur = Moniteur(transcripts, Traitees(U(18, 34)), premierPlan: sonde);
        var sansSource = sansLecteur.Inspecter(U(18, 34, 2));
        Assert.Null(sansSource.AppBureau);
        Assert.Same(sonde.Etat, sansSource.PremierPlan);
        Assert.Equal(new[] { U(18, 34), U(18, 34, 2) }, sonde.Appels);

        var sansSonde = Moniteur(transcripts, Traitees(U(18, 34)), Lecteur(racine));
        Assert.Equal(StatutPremierPlan.NonBranche, sansSonde.Inspecter(U(18, 34, 4)).PremierPlan!.Statut);
    }

    /// <summary>M14 — LUE-04 : une sonde qui LÈVE ne casse pas la lecture ; elle vaut « indisponible » avec le type de
    /// l'exception, et LUE-01 reste entière.</summary>
    [Fact]
    public void Une_sonde_qui_leve_ne_casse_pas_la_lecture()
    {
        var racine = Racine();
        RacineAppBureau.EcrireOctets(racine, "local_jarvis.json", RacineAppBureau.Fixture(PreteARevue), U(18, 34));
        var transcripts = new SourceModifiable(Snap(Jarvis, "JARVIS", SessionActivity.WaitingTurn, U(18, 20)));
        var sonde = new FakePremierPlan { Leve = true };
        var treated = Traitees(U(18, 35));
        var moniteur = Moniteur(transcripts, treated, Lecteur(racine), premierPlan: sonde);

        var lecture = moniteur.Inspecter(U(18, 35));

        Assert.Single(sonde.Appels);
        Assert.Equal(StatutPremierPlan.Indisponible, lecture.PremierPlan!.Statut);
        Assert.Equal("InvalidOperationException", lecture.PremierPlan.Raison);
        Assert.Null(lecture.PremierPlan.ClaudeDepuis);
        var masquee = Assert.Single(lecture.Masquees);
        Assert.Equal(Jarvis, masquee.Session.SessionId);
        Assert.Equal(MotifMasquage.LueParFocus, masquee.Motif);
    }
}
