using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve le cœur de l'auto-disparition par hystérésis (phase 14, réduite en phase 21) : le magasin
/// RÉVERSIBLE <see cref="TreatedStore"/>, le détecteur STATEFUL <see cref="SessionTreatmentTracker"/>
/// (NET-01 répondu, NET-03 réapparition/purge), l'archivage PERMANENT (NET-04) et le branchement dans
/// <see cref="SessionMonitor"/>. Tout se teste avec des séquences de signaux synthétiques et une horloge
/// INJECTÉE — aucune fenêtre réelle, et aucun instant emprunté à la machine. NET-02 (acquittement par
/// focus) a disparu avec la source app-bureau : la branche n'atteignait structurellement jamais une
/// session Claude Code.
/// </summary>
public class TreatedSessionsTests
{
    /// <summary>L'instant de référence. Fixe : ce fichier ne demande jamais l'heure à personne — un
    /// filtre de durée adossé à l'heure de la machine est vert à l'écriture et rouge quelques heures
    /// plus tard sans qu'une ligne de code ait bougé (trois plans de la phase 22).</summary>
    private static readonly DateTimeOffset T = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static string TempFile() => Path.Combine(Path.GetTempPath(), "chronos-treated-" + Guid.NewGuid().ToString("N") + ".json");
    private static SessionSnapshot Cli(string id, SessionActivity a, DateTimeOffset t) => new(id, "Proj", a, null, t);

    // Un SIGNAL dit QUI a parlé. Tous les cas du détecteur ci-dessous gardent une source UNIQUE d'un cycle
    // à l'autre, sauf ceux qui testent nommément la bascule de source.
    private static SignalSession Sig(string id, SessionActivity a, DateTimeOffset t,
                                     SourceSession src = SourceSession.Hook)
        => new(src, new SessionSnapshot(id, "Proj", a, null, t));

    // --- TreatedStore : round-trip + réversibilité + rétention de fichier ---

    [Fact]
    public void TreatedStore_set_load_remove_et_purge_TTL()
    {
        // L'horloge du magasin est INJECTÉE : aucune de ces assertions ne dépend de l'heure de la journée.
        var store = new TreatedStore(TempFile(), new FakeClock(T));

        // treatedWaitingTs = horodatage d'un épisode d'attente, ici contemporain de l'horloge injectée.
        var recent = T.ToUnixTimeMilliseconds();
        store.Set("a", recent);
        Assert.True(store.Load().TryGetValue("a", out var ts) && ts == recent);

        store.Remove("a");
        Assert.False(store.Load().ContainsKey("a"));

        // La borne est une RÉTENTION DE FICHIER, pas un délai d'affichage : au-delà, l'entrée n'est plus
        // rendue — la session n'est de toute façon plus lue par le moniteur depuis longtemps.
        var old = T.AddDays(-2).ToUnixTimeMilliseconds();
        store.Set("vieux", old);
        Assert.False(store.Load().ContainsKey("vieux"));

        // ET L'INVERSE, qui accuse l'ancienne borne de six heures : une session qui attend depuis SEPT
        // heures est encore pleinement affichée (le moniteur la lit jusqu'à huit heures). Sous l'ancienne
        // durée de vie, l'entrée disparaissait en silence et « marquer traitée » devenait un no-op — sur
        // le cas même dont ce milestone est parti.
        var septHeures = T.AddHours(-7).ToUnixTimeMilliseconds();
        store.Set("sept-heures", septHeures);
        Assert.True(store.Load().TryGetValue("sept-heures", out var ts7) && ts7 == septHeures);
    }

    // --- SessionTreatmentTracker ---

    [Fact]
    public void NET01_repondu_marque_traitee()
    {
        var store = new TreatedStore(TempFile(), new FakeClock(T));
        var tracker = new SessionTreatmentTracker(store);
        var t0 = T;

        tracker.Observe(new[] { Sig("s", SessionActivity.WaitingTurn, t0) }, t0);
        Assert.Empty(store.Load()); // en attente → rien encore

        var t1 = t0.AddSeconds(5);
        tracker.Observe(new[] { Sig("s", SessionActivity.Working, t1) }, t1);
        Assert.True(store.Load().ContainsKey("s")); // attente → Working = répondu = traité
    }

    [Fact]
    public void NET01_disparition_seule_ne_traite_pas()
    {
        var store = new TreatedStore(TempFile(), new FakeClock(T));
        var tracker = new SessionTreatmentTracker(store);
        var t0 = T;

        tracker.Observe(new[] { Sig("s", SessionActivity.WaitingTurn, t0) }, t0);
        var t1 = t0.AddSeconds(5);
        tracker.Observe(System.Array.Empty<SignalSession>(), t1); // disparue

        Assert.Empty(store.Load()); // la disparition n'est pas un « répondu »
    }

    [Fact]
    public void NET03_reapparition_purge_l_entree()
    {
        var store = new TreatedStore(TempFile(), new FakeClock(T));
        var tracker = new SessionTreatmentTracker(store);
        var t0 = T;
        var t1 = t0.AddSeconds(5);

        // NET-01 : la session est traitée (store contient "s").
        tracker.Observe(new[] { Sig("s", SessionActivity.WaitingTurn, t0) }, t0);
        tracker.Observe(new[] { Sig("s", SessionActivity.Working, t1) }, t1);
        Assert.True(store.Load().ContainsKey("s"));

        // Nouvel épisode d'attente PLUS RÉCENT → purge (réapparition).
        var t2 = t1.AddSeconds(5);
        tracker.Observe(new[] { Sig("s", SessionActivity.WaitingTurn, t2) }, t2);
        Assert.False(store.Load().ContainsKey("s"));
    }

    // --- Intégration bout-en-bout via SessionMonitor (source de base substituée par le contrat) ---

    private static string TempDir() { var d = Path.Combine(Path.GetTempPath(), "chronos-treated-mon-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    // Faux ISessionSource MUTABLE : la liste rendue peut changer entre deux Read pour simuler l'évolution
    // des cycles (attente → répondu → nouvel épisode).
    private sealed class MutableSource : ISessionSource
    {
        public List<SessionSnapshot> Snaps = new();
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => Snaps;
    }

    // MutableSource alimente désormais la source de BASE du moniteur (2e argument) : c'est le point de
    // substitution que le contrat ISessionSource existe pour offrir.
    private static SessionMonitor BuildMonitor(MutableSource source, TreatedStore treated,
        SessionTreatmentTracker tracker, ArchiveStore? archive = null)
        => new SessionMonitor(TempDir(), source, archive ?? new ArchiveStore(TempFile()), treated, tracker);

    [Fact]
    public void NET01_le_monitor_masque_apres_reponse()
    {
        var store = new TreatedStore(TempFile(), new FakeClock(T));
        var tracker = new SessionTreatmentTracker(store);
        var source = new MutableSource();
        var monitor = BuildMonitor(source, store, tracker);
        var t0 = T;

        source.Snaps = new List<SessionSnapshot> { Cli("s", SessionActivity.WaitingTurn, t0) };
        Assert.Contains(monitor.Read(t0), s => s.SessionId == "s"); // en attente → visible

        var t1 = t0.AddSeconds(5);
        source.Snaps = new List<SessionSnapshot> { Cli("s", SessionActivity.Working, t1) };
        Assert.DoesNotContain(monitor.Read(t1), s => s.SessionId == "s"); // répondu → masquée
    }

    [Fact]
    public void NET03_le_monitor_la_reaffiche_sur_nouvel_episode()
    {
        var store = new TreatedStore(TempFile(), new FakeClock(T));
        var tracker = new SessionTreatmentTracker(store);
        var source = new MutableSource();
        var monitor = BuildMonitor(source, store, tracker);
        var t0 = T;
        var t1 = t0.AddSeconds(5);

        // Attente puis répondu → masquée.
        source.Snaps = new List<SessionSnapshot> { Cli("s", SessionActivity.WaitingTurn, t0) };
        monitor.Read(t0);
        source.Snaps = new List<SessionSnapshot> { Cli("s", SessionActivity.Working, t1) };
        Assert.DoesNotContain(monitor.Read(t1), s => s.SessionId == "s");

        // Nouvel épisode d'attente PLUS RÉCENT → réapparaît + entrée purgée.
        var t2 = t1.AddSeconds(5);
        source.Snaps = new List<SessionSnapshot> { Cli("s", SessionActivity.WaitingTurn, t2) };
        Assert.Contains(monitor.Read(t2), s => s.SessionId == "s");
        Assert.False(store.Load().ContainsKey("s"));
    }

    [Fact]
    public void NET04_archivee_reste_masquee_meme_en_attente()
    {
        var store = new TreatedStore(TempFile(), new FakeClock(T));
        var tracker = new SessionTreatmentTracker(store);
        var source = new MutableSource();
        var archive = new ArchiveStore(TempFile());
        var monitor = BuildMonitor(source, store, tracker, archive);
        var t0 = T;

        // Archivée = permanent (NET-04), contraste direct avec traité (réversible NET-03).
        archive.Add("s");
        source.Snaps = new List<SessionSnapshot> { Cli("s", SessionActivity.WaitingTurn, t0) };
        Assert.DoesNotContain(monitor.Read(t0), s => s.SessionId == "s");

        // Repart en attente avec un épisode PLUS RÉCENT → RESTE masquée (l'archivage ne se purge jamais).
        var t1 = t0.AddSeconds(10);
        source.Snaps = new List<SessionSnapshot> { Cli("s", SessionActivity.WaitingTurn, t1) };
        Assert.DoesNotContain(monitor.Read(t1), s => s.SessionId == "s");
    }

    [Fact]
    public void Monitor_sans_tracker_ni_treated_ne_regresse_pas()
    {
        // SessionMonitor construit SANS les paramètres d'hystérésis → aucune exception, la session en
        // attente reste visible (hystérésis désactivée = null).
        var now = T;
        var source = new MutableSource { Snaps = new List<SessionSnapshot> { Cli("s", SessionActivity.WaitingTurn, now) } };
        var monitor = new SessionMonitor(TempDir(), source, new ArchiveStore(TempFile()));

        Assert.Contains(monitor.Read(now), s => s.SessionId == "s");
    }

    // --- TRT-01 / TRT-02 : ce que « traité » doit vouloir dire ---

    /// <summary>
    /// TRT-01 — une bascule de source n'est PAS une transition. Le fichier de hook se tait, le transcript
    /// reprend la main : personne n'a répondu, une source s'est simplement relayée. Conclure ici, c'est
    /// affirmer un geste de l'utilisateur qui n'a jamais eu lieu.
    /// </summary>
    [Fact]
    public void Une_bascule_de_source_ne_marque_rien()
    {
        var store = new TreatedStore(TempFile(), new FakeClock(T));
        var tracker = new SessionTreatmentTracker(store);
        var t0 = T;
        var t1 = t0.AddSeconds(5);

        tracker.Observe(new[] { Sig("s", SessionActivity.WaitingAttention, t0, SourceSession.Hook) }, t0);
        tracker.Observe(new[] { Sig("s", SessionActivity.Working, t1, SourceSession.Transcript) }, t1);

        Assert.Empty(store.Load());
    }

    /// <summary>
    /// TRT-01 — une attente DÉDUITE est une attente. La phase 25 l'a livrée, le bandeau la compte et le
    /// cadran la colore ; le détecteur doit donc lui ouvrir un épisode, sans quoi la réponse qui la suit
    /// ne serait jamais reconnue.
    /// </summary>
    [Fact]
    public void Une_attente_deduite_ouvre_un_episode()
    {
        var store = new TreatedStore(TempFile(), new FakeClock(T));
        var tracker = new SessionTreatmentTracker(store);
        var t0 = T;
        var t1 = t0.AddSeconds(5);

        tracker.Observe(new[] { Sig("s", SessionActivity.WaitingDeduced, t0) }, t0);
        tracker.Observe(new[] { Sig("s", SessionActivity.Working, t1) }, t1);

        Assert.True(store.Load().ContainsKey("s"));
    }

    /// <summary>
    /// TRT-01 — l'autre face de la même règle, et le trou ouvert par la phase 25 : une attente OBSERVÉE
    /// dont la source se tait devient une attente DÉDUITE. Si le détecteur y voit une non-attente, il
    /// marque traitée une session qui attend toujours — c'est le masquage de six heures, par la porte
    /// d'à côté.
    /// </summary>
    [Fact]
    public void Une_attente_qui_devient_deduite_n_est_pas_traitee()
    {
        var store = new TreatedStore(TempFile(), new FakeClock(T));
        var tracker = new SessionTreatmentTracker(store);
        var t0 = T;
        var t1 = t0.AddSeconds(5);

        tracker.Observe(new[] { Sig("s", SessionActivity.WaitingTurn, t0) }, t0);
        tracker.Observe(new[] { Sig("s", SessionActivity.WaitingDeduced, t1) }, t1);

        Assert.Empty(store.Load());
    }

    /// <summary>
    /// TRT-01 — un état INCONNU n'affirme rien. Le lire comme une réponse, c'est conclure d'une absence de
    /// lecture. Sonde S2 du 2026-09-12 : un seul cycle de ce genre suffisait à masquer une session.
    /// </summary>
    [Fact]
    public void Un_etat_inconnu_n_affirme_pas_qu_on_a_repondu()
    {
        var store = new TreatedStore(TempFile(), new FakeClock(T));
        var tracker = new SessionTreatmentTracker(store);
        var t0 = T;
        var t1 = t0.AddSeconds(5);

        tracker.Observe(new[] { Sig("s", SessionActivity.WaitingTurn, t0) }, t0);
        tracker.Observe(new[] { Sig("s", SessionActivity.Unknown, t1) }, t1);

        Assert.Empty(store.Load());
    }

    /// <summary>
    /// TRT-02 — les DEUX temps, dans un seul test, parce que la persistance sans la réversibilité serait
    /// un masquage de plus. Un tracker NEUF, c'est un redémarrage de l'overlay : s'il redate l'épisode à
    /// « maintenant », NET-03 purge le magasin qu'il vient de lire et toutes les traitées ressortent.
    /// Mais si l'épisode se figeait à jamais, une session ne pourrait plus jamais revenir me demander
    /// quelque chose.
    /// </summary>
    [Fact]
    public void Le_traite_survit_au_redemarrage_mais_pas_a_une_nouvelle_demande()
    {
        var store = new TreatedStore(TempFile(), new FakeClock(T));
        store.Set("s", T.AddHours(-2).ToUnixTimeMilliseconds());

        var tracker = new SessionTreatmentTracker(store);   // NEUF = l'overlay vient de redémarrer

        // 1er temps — la session attend TOUJOURS, et son signal est ANTÉRIEUR au traitement mémorisé :
        // rien de neuf n'a été demandé, l'entrée reste.
        tracker.Observe(new[] { Sig("s", SessionActivity.WaitingAttention, T.AddHours(-5)) }, T);
        Assert.True(store.Load().ContainsKey("s"));

        // 2e temps — la même session réaffirme son attente avec un signal PLUS RÉCENT que le traitement :
        // elle me redemande quelque chose, elle revient.
        tracker.Observe(new[] { Sig("s", SessionActivity.WaitingAttention, T.AddHours(-1)) }, T);
        Assert.False(store.Load().ContainsKey("s"));
    }

    /// <summary>
    /// NON-RÉGRESSION de NET-03, VERTE avant comme après le correctif : la réversibilité ne doit pas être
    /// sacrifiée à la persistance. Un épisode d'attente réellement plus récent que le traitement mémorisé
    /// purge l'entrée, même après un redémarrage.
    /// </summary>
    [Fact]
    public void Un_episode_reellement_plus_recent_purge_toujours()
    {
        var store = new TreatedStore(TempFile(), new FakeClock(T));
        store.Set("s", T.AddHours(-2).ToUnixTimeMilliseconds());

        var tracker = new SessionTreatmentTracker(store);   // NEUF
        tracker.Observe(new[] { Sig("s", SessionActivity.WaitingAttention, T.AddHours(-1)) }, T);

        Assert.False(store.Load().ContainsKey("s"));
    }

    /// <summary>
    /// CRITÈRE 2 DU ROADMAP — non-régression sur une panne RÉELLEMENT SURVENUE chez l'utilisateur, avec
    /// ses chiffres. Relevé du 2026-09-12 : e465420e (PROJET ADVANCED SHEET) était vivante et en attente
    /// de permission ; le widget l'a cachée six heures.
    ///
    /// La chaîne mesurée, rejouée ici à l'identique :
    ///   • le fichier de hook porte updated_at = 1789181509266 (« une permission est demandée ») ;
    ///   • l'overlay redémarre 478 minutes plus tard — un tracker NEUF ouvre donc un épisode d'attente ;
    ///     c'est l'instant 1789210240523 relevé dans treated.json, à 69 s du seuil de huit heures ;
    ///   • deux minutes après, le fichier de hook franchit DropAfter et cesse d'être lu, tandis que le
    ///     transcript (Working) reprend la main. Le détecteur voit « attente → travail » et conclut.
    ///
    /// Ce n'est pas une réponse de l'utilisateur : c'est une source qui se tait pendant qu'une autre parle.
    /// </summary>
    [Fact]
    public void Le_scenario_mesure_des_478_minutes_laisse_la_session_visible()
    {
        const string Id = "e465420e-83e0-428f-97f1-f0174c0848fc";
        var tHook = DateTimeOffset.FromUnixTimeMilliseconds(1789181509266);   // updated_at du fichier de hook
        var tEpisode = DateTimeOffset.FromUnixTimeMilliseconds(1789210240523); // treatedWaitingTs relevé

        // Garde anti-dérive : si ces deux constantes cessent d'encadrer le seuil de huit heures, le test
        // ne rejoue plus le scénario mesuré et il faut le dire, pas l'ajuster.
        Assert.Equal(478, (int)(tEpisode - tHook).TotalMinutes);

        var dossier = TempDir();
        File.WriteAllText(Path.Combine(dossier, Id + ".json"),
            $$"""{"session_id":"{{Id}}","project":"PROJET ADVANCED SHEET","activity":"WaitingAttention","reason":"permission_prompt","updated_at":{{tHook.ToUnixTimeMilliseconds()}}}""");

        var transcripts = new MutableSource();                 // muette : la session est bloquée, elle n'écrit rien
        var horloge = new FakeClock(tEpisode);
        var store = new TreatedStore(TempFile(), horloge);
        var monitor = new SessionMonitor(dossier, transcripts, new ArchiveStore(TempFile()),
                                         store, new SessionTreatmentTracker(store));

        // Cycle 1 — redémarrage : le fichier de hook a 478 min (< 8 h), il est lu et il gagne seul.
        Assert.Contains(monitor.Read(tEpisode), s => s.SessionId == Id);

        // Cycle 2 — trois minutes plus tard : le fichier de hook a franchi les huit heures et n'est plus
        // lu ; le transcript, lui, vient d'écrire. LA BASCULE DE SOURCE.
        var apres = tEpisode.AddMinutes(3);
        horloge.UtcNow = apres;
        transcripts.Snaps = new List<SessionSnapshot> { new(Id, "PROJET ADVANCED SHEET", SessionActivity.Working, null, apres) };

        var lecture = monitor.Inspecter(apres);

        Assert.Contains(lecture.Visibles, s => s.SessionId == Id);
        Assert.DoesNotContain(lecture.Masquees, m => m.Session.SessionId == Id && m.Motif == MotifMasquage.Traitee);
        Assert.False(store.Load().ContainsKey(Id), "expirer n'est pas répondre : rien ne doit être marqué traité");
    }
}
