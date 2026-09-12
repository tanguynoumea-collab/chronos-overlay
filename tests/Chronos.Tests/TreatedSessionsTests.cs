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
}
