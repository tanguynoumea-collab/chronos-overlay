using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// LA RÈGLE « LUE » AU DÉTECTEUR (phase 30, LUE-01 à LUE-04) — logique PURE : un <see cref="SessionTreatmentTracker"/>
/// sur un <see cref="TreatedStore"/> TEMPORAIRE, une horloge injectée, un <see cref="ContexteLecture"/> construit à la
/// main. Aucune fenêtre, aucun lecteur de l'app, aucune sonde du premier plan : ce que le moniteur branchera (30-03) est
/// ici posé tel quel.
///
/// <para>Les instants sont ceux du relevé de phase 27 (UTC ; 27-RELEVE.md, 30-RESEARCH.md Q5.a) : la fin de tour de
/// JARVIS à 13:59:05Z et son focus à 14:00:39Z (le relevé de 16 h 08, heure locale) ; le geste B — fin du tour à
/// 18:34:04.328Z pendant que l'utilisateur regardait, focus resté à 18:30:44.976Z, processus <c>claude</c> au premier
/// plan depuis 18:33:27Z.</para>
///
/// <para>La preuve « pas d'écriture à chaque cycle » (critère 2 de la ROADMAP) est une SENTINELLE de date d'écriture :
/// <see cref="TreatedStore"/> remplace le fichier par un déplacement à chaque écriture, donc toute écriture change la
/// date ; une date restée à la sentinelle prouve qu'aucune n'a eu lieu.</para>
/// </summary>
public sealed class DetecteurLectureTests : IDisposable
{
    private const string Jarvis = "c17a1b03-3c8d-4c05-a747-dd77bc702e1b";
    private const string B = "11456cab-d447-42c7-aa85-9920ce64f7ba";
    private const string Autre = "88677186-8690-44dc-ac77-7a319a0aa5cb";

    private static DateTimeOffset Utc(int h, int m, int s, int ms = 0) => new(2026, 9, 25, h, m, s, ms, TimeSpan.Zero);

    // Le relevé de 16 h 08 (heure locale = UTC+2).
    private static readonly DateTimeOffset FinJarvis = Utc(13, 59, 5);       // 1790344745000
    private static readonly DateTimeOffset FocusJarvis = Utc(14, 0, 39);     // 1790344839000
    private static readonly DateTimeOffset Releve = Utc(14, 8, 0);           // 1790345280000

    // Le geste B : le tour qui se termine sous les yeux.
    private static readonly DateTimeOffset FinB = Utc(18, 34, 4, 328);       // 1790361244328
    private static readonly DateTimeOffset FocusB = Utc(18, 30, 44, 976);    // 1790361044976
    private static readonly DateTimeOffset ClaudeDepuisB = Utc(18, 33, 27);  // 1790361207000
    private static readonly DateTimeOffset LueB = Utc(18, 34, 6, 828);       // FinB + 2,5 s

    private static readonly DateTime Sentinelle = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly List<string> _fichiers = new();

    public void Dispose()
    {
        foreach (var f in _fichiers)
            try { if (File.Exists(f)) File.Delete(f); } catch { /* nettoyage best-effort */ }
    }

    private string Chemin()
    {
        var chemin = Path.Combine(Path.GetTempPath(), "chronos-lue-" + Guid.NewGuid().ToString("N") + ".json");
        _fichiers.Add(chemin);
        return chemin;
    }

    /// <summary>Un détecteur NEUF sur un magasin temporaire dont l'horloge (rétention 24 h) est posée près du test.</summary>
    private (TreatedStore Store, SessionTreatmentTracker Detecteur, string Chemin) Neuf(DateTimeOffset horloge)
    {
        var chemin = Chemin();
        var store = new TreatedStore(chemin, new FakeClock(horloge));
        return (store, new SessionTreatmentTracker(store), chemin);
    }

    private static SignalSession Sig(string id, SessionActivity a, DateTimeOffset t, SourceSession src = SourceSession.Hook)
        => new(src, new SessionSnapshot(id, "Proj", a, null, t));

    private static IReadOnlyList<SignalSession> Un(SignalSession s) => new[] { s };

    /// <summary>Le contexte tel que le moniteur le bâtira : dictionnaire du dernier focus INSENSIBLE à la casse.</summary>
    private static ContexteLecture Ctx(string? selection, DateTimeOffset? claudeDepuis,
                                       params (string Id, DateTimeOffset? Focus)[] focus)
    {
        var d = new Dictionary<string, DateTimeOffset?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, f) in focus) d[id] = f;
        return new ContexteLecture(d, selection, claudeDepuis);
    }

    /// <summary>L'épisode inscrit pour la session, ou nul si le magasin ne la porte pas.</summary>
    private static long? Inscrit(TreatedStore store, string id)
        => store.Load().TryGetValue(id, out var ts) ? ts : null;

    // Le geste B, joué à l'instant donné (sélection, premier plan et casse de la clé réglables).
    private static void GesteB(SessionTreatmentTracker detecteur, DateTimeOffset now,
                               string? selection = B, DateTimeOffset? claudeDepuis = null, string cle = B)
        => detecteur.Observe(Un(Sig(B, SessionActivity.WaitingTurn, FinB)), now,
                             Ctx(selection, claudeDepuis ?? ClaudeDepuisB, (cle, FocusB)));

    // ------------------------------------------------------------------ LUE-01 : l'attente antérieure au focus

    [Fact]   // L01 — le relevé de 16 h 08 : JARVIS finie à 15:59:05, ouverte à 16:00:39 ⇒ lue, sans clic.
    public void Une_attente_anterieure_au_focus_est_lue()
    {
        Assert.Equal(1790344745000, FinJarvis.ToUnixTimeMilliseconds());   // garde anti-dérive des constantes
        var (store, detecteur, _) = Neuf(Releve);

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingTurn, FinJarvis)), Releve,
                          Ctx(null, null, (Jarvis, FocusJarvis)));

        Assert.Equal(1790344745000, Inscrit(store, Jarvis));
    }

    [Fact]   // L02 — la 3e session du relevé : finie à 16:05 APRÈS son focus de 16:00 ⇒ elle attend encore.
    public void Une_attente_posterieure_au_focus_reste_en_attente()
    {
        var (store, detecteur, _) = Neuf(Releve);

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingTurn, Utc(14, 5, 0))), Releve,
                          Ctx(null, null, (Jarvis, Utc(14, 0, 0))));

        Assert.Empty(store.Load());
    }

    [Fact]   // L03 — D-30-03 : comparaison STRICTE. Un focus égal à l'attente n'est pas une lecture.
    public void Un_focus_egal_a_l_attente_n_est_pas_une_lecture()
    {
        var (store, detecteur, _) = Neuf(Releve);

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingTurn, FocusJarvis)), Releve,
                          Ctx(null, null, (Jarvis, FocusJarvis)));

        Assert.Empty(store.Load());
    }

    /// <summary>
    /// L04 — LE CRITÈRE 2 : pas de réécriture de treated.json à chaque rafraîchissement. Après le cycle qui inscrit,
    /// la date d'écriture du fichier est posée à une SENTINELLE ; cinq cycles identiques (la cadence du widget, 2 s)
    /// ne doivent pas y toucher. Mutation (m1) — écrire à chaque cycle où la session est lue — et ce test rougit.
    /// </summary>
    [Fact]
    public void La_lecture_n_ecrit_qu_une_fois_par_episode()
    {
        var (store, detecteur, chemin) = Neuf(Releve);
        var signal = Un(Sig(Jarvis, SessionActivity.WaitingTurn, FinJarvis));
        var contexte = Ctx(null, null, (Jarvis, FocusJarvis));

        detecteur.Observe(signal, Releve, contexte);
        Assert.Equal(1790344745000, Inscrit(store, Jarvis));
        Assert.True(File.Exists(chemin), "Le premier cycle devait écrire treated.json.");

        File.SetLastWriteTimeUtc(chemin, Sentinelle);
        for (var i = 1; i <= 5; i++)
            detecteur.Observe(signal, Releve.AddSeconds(2 * i), contexte);   // 14:08:02 … 14:08:10

        Assert.Equal(Sentinelle, File.GetLastWriteTimeUtc(chemin));
        Assert.Equal(1790344745000, Inscrit(store, Jarvis));
    }

    /// <summary>
    /// L05 — cas 5 de la table : un NOUVEL épisode, déjà lu, s'inscrit directement. Deux blocs successifs (LUE puis
    /// NET-03 fondé sur le magasin lu en tête de cycle) écriraient le nouvel épisode puis le purgeraient aussitôt :
    /// mutation (m2), et ce test trouve le magasin vide.
    /// </summary>
    [Fact]
    public void Un_nouvel_episode_deja_lu_s_ecrit_sans_purge()
    {
        var (store, detecteur, _) = Neuf(Utc(14, 2, 0));
        var contexte = Ctx(null, null, (Jarvis, FocusJarvis));

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingTurn, Utc(13, 50, 0))), Utc(14, 1, 0), contexte);
        Assert.Equal(Utc(13, 50, 0).ToUnixTimeMilliseconds(), Inscrit(store, Jarvis));

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingTurn, FinJarvis)), Utc(14, 2, 0), contexte);
        Assert.Equal(1790344745000, Inscrit(store, Jarvis));
    }

    [Fact]   // L06 — cas 3, NET-03 inchangé : un nouveau tour fini APRÈS le focus fait revenir la session lue.
    public void Une_session_lue_revient_sur_un_nouveau_tour()
    {
        var (store, detecteur, _) = Neuf(Utc(14, 6, 0));
        var contexte = Ctx(null, null, (Jarvis, FocusJarvis));

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingTurn, FinJarvis)), Utc(14, 1, 0), contexte);
        Assert.Equal(1790344745000, Inscrit(store, Jarvis));

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingTurn, Utc(14, 5, 0))), Utc(14, 6, 0), contexte);
        Assert.Empty(store.Load());
    }

    [Fact]   // L07 — D-30-07 : une attente DÉDUITE ouverte ensuite est lue au même titre.
    public void La_deduite_ouverte_ensuite_est_lue()
    {
        var (store, detecteur, _) = Neuf(Releve);

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingDeduced, Utc(13, 40, 0))), Releve,
                          Ctx(null, null, (Jarvis, Utc(13, 45, 0))));

        Assert.Equal(Utc(13, 40, 0).ToUnixTimeMilliseconds(), Inscrit(store, Jarvis));
    }

    [Fact]   // L08 — D-30-07, l'autre moitié : redevenue une attente OBSERVÉE plus récente que le focus, elle revient.
    public void La_deduite_redevenue_attente_observee_plus_recente_revient()
    {
        var (store, detecteur, _) = Neuf(Utc(14, 11, 0));
        var contexte = Ctx(null, null, (Jarvis, Utc(13, 45, 0)));

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingDeduced, Utc(13, 40, 0))), Releve, contexte);
        Assert.Equal(Utc(13, 40, 0).ToUnixTimeMilliseconds(), Inscrit(store, Jarvis));

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingTurn, Utc(14, 10, 0))), Utc(14, 11, 0), contexte);
        Assert.Empty(store.Load());
    }

    [Fact]   // L09 — D-30-08 : une demande de permission VUE est lue (la session reste bloquée côté Claude).
    public void Une_permission_vue_est_lue()
    {
        var (store, detecteur, _) = Neuf(Releve);

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingAttention, FinJarvis)), Releve,
                          Ctx(null, null, (Jarvis, FocusJarvis)));

        Assert.Equal(1790344745000, Inscrit(store, Jarvis));
    }

    // ------------------------------------------------------------------ LUE-02 : la session regardée au premier plan

    /// <summary>
    /// L10 — LE GESTE B : le focus (20:30:44.976 locale) reste ANTÉRIEUR à la fin du tour (20:34:04.328) ; seule la
    /// session sélectionnée, fenêtre <c>claude</c> au premier plan, peut dire qu'elle a été lue — 2,5 s après le plus
    /// tardif de la fin du tour et du retour au premier plan, borne INCLUSE. Mutation (m4), comparaison stricte : rouge.
    /// </summary>
    [Fact]
    public void Geste_B_le_tour_fini_sous_les_yeux_est_lu_au_premier_plan()
    {
        Assert.Equal(1790361244328, FinB.ToUnixTimeMilliseconds());
        Assert.Equal(1790361044976, FocusB.ToUnixTimeMilliseconds());
        var (store, detecteur, _) = Neuf(LueB);

        GesteB(detecteur, LueB);

        Assert.Equal(1790361244328, Inscrit(store, B));
    }

    [Fact]   // L11 — une milliseconde avant la grâce : pas encore lue.
    public void Geste_B_la_grace_n_est_pas_ecoulee()
    {
        var (store, detecteur, _) = Neuf(LueB);

        GesteB(detecteur, Utc(18, 34, 6, 827));

        Assert.Empty(store.Load());
    }

    [Fact]   // L12 — le même tour, sans claude au premier plan (explorer, écran verrouillé…) : il attend.
    public void Geste_B_sans_claude_au_premier_plan_reste_en_attente()
    {
        var (store, detecteur, _) = Neuf(LueB);

        detecteur.Observe(Un(Sig(B, SessionActivity.WaitingTurn, FinB)), Utc(18, 34, 10),
                          Ctx(B, null, (B, FocusB)));

        Assert.Empty(store.Load());
    }

    [Fact]   // L13 — une attente ancienne, un retour RÉCENT sur claude : la grâce court depuis le retour.
    public void Un_retour_recent_au_premier_plan_attend_sa_grace()
    {
        var n = Utc(18, 34, 6);
        var depuis = n.AddSeconds(-2);
        var (store, detecteur, _) = Neuf(n);
        var signal = Un(Sig(B, SessionActivity.WaitingTurn, Utc(18, 0, 0)));
        var contexte = Ctx(B, depuis, (B, Utc(17, 50, 0)));

        detecteur.Observe(signal, n, contexte);
        Assert.Empty(store.Load());

        detecteur.Observe(signal, n.AddMilliseconds(500), contexte);
        Assert.Equal(Utc(18, 0, 0).ToUnixTimeMilliseconds(), Inscrit(store, B));
    }

    [Fact]   // L14 — claude au premier plan, mais l'utilisateur regarde une AUTRE session.
    public void Une_session_non_selectionnee_n_est_pas_lue_au_premier_plan()
    {
        var (store, detecteur, _) = Neuf(LueB);

        GesteB(detecteur, LueB, selection: Autre);

        Assert.Empty(store.Load());
    }

    /// <summary>
    /// L15 — LUE-04 : sans focus connu pour la session (pas de fichier de l'app, pas de cliSessionId, champ absent), la
    /// règle ne s'applique PAS — même sélectionnée, même avec claude au premier plan depuis longtemps.
    /// </summary>
    [Theory]
    [InlineData("absente")]
    [InlineData("nulle")]
    public void Sans_focus_connu_la_regle_ne_s_applique_pas(string cas)
    {
        var (store, detecteur, _) = Neuf(LueB);
        var contexte = cas == "absente"
            ? Ctx(B, Utc(18, 0, 0), (Autre, FocusB))
            : Ctx(B, Utc(18, 0, 0), (B, null));

        detecteur.Observe(Un(Sig(B, SessionActivity.WaitingTurn, FinB)), LueB, contexte);

        Assert.Empty(store.Load());
    }

    /// <summary>
    /// L20 — LUE-04 par construction : un contexte nul (tous les appels existants, à deux arguments) et un contexte
    /// VIDE rendent le détecteur de la v1.6, cycle après cycle — NET-01 compris.
    /// </summary>
    [Fact]
    public void Sans_contexte_le_detecteur_est_celui_de_la_v1_6()
    {
        var t = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        var (storeA, a, _) = Neuf(t);
        var (storeB, b, _) = Neuf(t);
        var sequence = new[]
        {
            (Etat: SessionActivity.WaitingTurn, Instant: t),
            (Etat: SessionActivity.Working, Instant: t.AddSeconds(5)),
            (Etat: SessionActivity.WaitingTurn, Instant: t.AddSeconds(10)),
        };

        for (var i = 0; i < sequence.Length; i++)
        {
            var v = Un(Sig(Jarvis, sequence[i].Etat, sequence[i].Instant));
            a.Observe(v, sequence[i].Instant);
            b.Observe(v, sequence[i].Instant, Ctx(null, null));

            Assert.Equal(storeA.Load().OrderBy(kv => kv.Key, StringComparer.Ordinal),
                         storeB.Load().OrderBy(kv => kv.Key, StringComparer.Ordinal));
            if (i == 1)
                Assert.Equal(t.ToUnixTimeMilliseconds(), Inscrit(storeA, Jarvis));   // NET-01 : répondue
        }
    }

    [Fact]   // L21 — la sélection et le dictionnaire se comparent SANS casse (comme ParSession).
    public void La_session_selectionnee_se_compare_sans_casse()
    {
        var (store, detecteur, _) = Neuf(LueB);

        GesteB(detecteur, LueB, selection: B.ToUpperInvariant(), cle: B.ToUpperInvariant());

        Assert.Equal(1790361244328, Inscrit(store, B));
    }

    // ------------------------------------------------------------------ LUE-03 : la cause retenue, jamais inventée

    /// <summary>
    /// L16 — une RÉPONSE est un fait nouveau : lue par focus, puis l'utilisateur répond (attente puis travail sur la
    /// MÊME source) ⇒ la cause devient « répondue », pour le même épisode, et le magasin ne bouge pas.
    /// </summary>
    [Fact]
    public void Repondue_remplace_la_cause_lue()
    {
        var (store, detecteur, _) = Neuf(Releve);
        var contexte = Ctx(null, null, (Jarvis, FocusJarvis));

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingTurn, FinJarvis)), Releve, contexte);
        var lue = detecteur.CauseDe(Jarvis, 1790344745000);
        Assert.NotNull(lue);
        Assert.Equal(MotifMasquage.LueParFocus, lue!.Motif);
        Assert.Equal(FinJarvis, lue.Attente);
        Assert.Equal(FocusJarvis, lue.Focus);

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.Working, Utc(14, 9, 0))), Utc(14, 9, 0), contexte);
        var repondue = detecteur.CauseDe(Jarvis, 1790344745000);
        Assert.NotNull(repondue);
        Assert.Equal(MotifMasquage.Repondue, repondue!.Motif);
        Assert.Equal(FinJarvis, repondue.Attente);
        Assert.Equal(1790344745000, Inscrit(store, Jarvis));
    }

    /// <summary>
    /// L17 — la cause « lue au premier plan » est celle du CONSTAT : l'utilisateur quitte ensuite claude (plus de
    /// premier plan), la session reste lue pour cet épisode et sa cause ne change pas.
    /// </summary>
    [Fact]
    public void La_cause_survit_au_depart_du_premier_plan()
    {
        var (store, detecteur, _) = Neuf(LueB);

        GesteB(detecteur, LueB);
        var attendue = new CauseTraitement(MotifMasquage.LueAuPremierPlan, FinB, FocusB, ClaudeDepuisB, LueB);
        Assert.Equal(attendue, detecteur.CauseDe(B, 1790361244328));

        detecteur.Observe(Un(Sig(B, SessionActivity.WaitingTurn, FinB)), Utc(18, 34, 10), Ctx(B, null, (B, FocusB)));

        Assert.Equal(attendue, detecteur.CauseDe(B, 1790361244328));
        Assert.Equal(1790361244328, Inscrit(store, B));
    }

    /// <summary>
    /// L22 — la cause d'un épisode est FIGÉE à son premier constat : une minute plus tard, claude toujours au premier
    /// plan, le constat reste 18:34:06.828Z — le rapport dira « depuis 39 s », jamais « depuis 99 s ».
    /// </summary>
    [Fact]
    public void La_cause_est_figee_a_son_premier_constat()
    {
        var (_, detecteur, _) = Neuf(LueB);

        GesteB(detecteur, LueB);
        GesteB(detecteur, Utc(18, 35, 6, 828));

        var cause = detecteur.CauseDe(B, 1790361244328);
        Assert.NotNull(cause);
        Assert.Equal(MotifMasquage.LueAuPremierPlan, cause!.Motif);
        Assert.Equal(LueB, cause.Constat);
        Assert.Equal(ClaudeDepuisB, cause.PremierPlanDepuis);
    }

    /// <summary>
    /// L18 — après un redémarrage de l'overlay, le magasin porte déjà l'épisode ; un détecteur NEUF qui voit la même
    /// attente antérieure au focus retrouve la cause (cas 6) SANS écrire — la sentinelle de date d'écriture le prouve.
    /// </summary>
    [Fact]
    public void Apres_redemarrage_la_lecture_par_focus_retrouve_sa_cause_sans_ecrire()
    {
        var (store, detecteur, chemin) = Neuf(Releve);
        store.Set(Jarvis, 1790344745000);                  // écrit par l'overlay d'avant le redémarrage
        File.SetLastWriteTimeUtc(chemin, Sentinelle);

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingTurn, FinJarvis)), Releve,
                          Ctx(null, null, (Jarvis, FocusJarvis)));

        var cause = detecteur.CauseDe(Jarvis, 1790344745000);
        Assert.NotNull(cause);
        Assert.Equal(MotifMasquage.LueParFocus, cause!.Motif);
        Assert.Equal(Sentinelle, File.GetLastWriteTimeUtc(chemin));
    }

    /// <summary>
    /// L19 — le résidu HONNÊTE : après un redémarrage, une inscription que le détecteur neuf ne relit pas comme une
    /// lecture (focus antérieur à l'attente : ni lue, ni répondue sous ses yeux) n'a PAS de cause. Le moniteur
    /// l'annoncera « traitée », sans prétendre savoir pourquoi (cas 2 : magasin inchangé).
    /// </summary>
    [Fact]
    public void Apres_redemarrage_une_cause_non_revue_est_residuelle()
    {
        var (store, detecteur, chemin) = Neuf(Releve);
        store.Set(Jarvis, 1790344745000);
        File.SetLastWriteTimeUtc(chemin, Sentinelle);

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingTurn, FinJarvis)), Releve,
                          Ctx(null, null, (Jarvis, Utc(13, 50, 0))));

        Assert.Null(detecteur.CauseDe(Jarvis, 1790344745000));
        Assert.Equal(1790344745000, Inscrit(store, Jarvis));
        Assert.Equal(Sentinelle, File.GetLastWriteTimeUtc(chemin));
    }

    /// <summary>
    /// L23 — NET-03 purge la cause avec l'inscription : la session revenue (nouveau tour fini après le focus) n'a plus
    /// de cause, ni pour l'ancien épisode ni pour le nouveau.
    /// </summary>
    [Fact]
    public void Une_session_revenue_oublie_sa_cause()
    {
        var (store, detecteur, _) = Neuf(Utc(14, 6, 0));
        var contexte = Ctx(null, null, (Jarvis, FocusJarvis));

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingTurn, FinJarvis)), Utc(14, 1, 0), contexte);
        Assert.NotNull(detecteur.CauseDe(Jarvis, 1790344745000));   // la cause existait : ce qui suit l'oublie

        detecteur.Observe(Un(Sig(Jarvis, SessionActivity.WaitingTurn, Utc(14, 5, 0))), Utc(14, 6, 0), contexte);

        Assert.Empty(store.Load());
        Assert.Null(detecteur.CauseDe(Jarvis, 1790344745000));
        Assert.Null(detecteur.CauseDe(Jarvis, 1790345100000));
    }

    /// <summary>
    /// L24 — un GESTE (« Marquer traitée », écrit par le ViewModel directement dans le magasin) n'a pas de cause au
    /// détecteur : il ne l'a pas constaté, il ne l'invente pas.
    /// </summary>
    [Fact]
    public void Un_geste_n_a_pas_de_cause()
    {
        var (store, detecteur, _) = Neuf(Releve);
        var signal = Un(Sig(Jarvis, SessionActivity.WaitingTurn, FinJarvis));

        detecteur.Observe(signal, Releve);
        store.Set(Jarvis, 1790344745000);                   // le geste du ViewModel
        detecteur.Observe(signal, Releve.AddSeconds(2));

        Assert.Null(detecteur.CauseDe(Jarvis, 1790344745000));
        Assert.Equal(1790344745000, Inscrit(store, Jarvis));
    }
}
