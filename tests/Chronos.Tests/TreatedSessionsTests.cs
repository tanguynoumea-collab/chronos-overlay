using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve le cœur de l'auto-disparition par hystérésis (Phase 14) : le magasin RÉVERSIBLE
/// <see cref="TreatedStore"/>, le détecteur STATEFUL <see cref="SessionTreatmentTracker"/>
/// (NET-01 répondu, NET-02 acquitté par focus, NET-03 réapparition/purge) et le branchement dans
/// <see cref="SessionMonitor"/>. Tout se teste avec des séquences de snapshots synthétiques + un faux
/// focus + une horloge injectée — aucune fenêtre Claude ni UIA réels.
/// </summary>
public class TreatedSessionsTests
{
    private static string TempFile() => Path.Combine(Path.GetTempPath(), "chronos-treated-" + Guid.NewGuid().ToString("N") + ".json");
    private static SessionSnapshot Cli(string id, SessionActivity a, DateTimeOffset t) => new(id, "Proj", a, null, t);
    private static SessionSnapshot Fg(SessionActivity a, DateTimeOffset t) => new("desktop:foreground:chat", "Claude (bureau)", a, null, t, SessionKind.Chat, SessionOrigin.Desktop);

    // --- TreatedStore : round-trip + réversibilité + TTL ---

    [Fact]
    public void TreatedStore_set_load_remove_et_purge_TTL()
    {
        var store = new TreatedStore(TempFile());

        // treatedWaitingTs = horodatage d'un épisode d'attente RÉCENT (sinon la purge TTL le rejette aussitôt).
        var recent = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        store.Set("a", recent);
        Assert.True(store.Load().TryGetValue("a", out var ts) && ts == recent);

        store.Remove("a");
        Assert.False(store.Load().ContainsKey("a"));

        // Entrée vieille de > 6 h → jamais rendue par Load() (purge TTL).
        var old = DateTimeOffset.UtcNow.AddHours(-7).ToUnixTimeMilliseconds();
        store.Set("vieux", old);
        Assert.False(store.Load().ContainsKey("vieux"));
    }

    // --- SessionTreatmentTracker ---

    [Fact]
    public void NET01_repondu_marque_traitee()
    {
        var store = new TreatedStore(TempFile());
        var tracker = new SessionTreatmentTracker(store);
        var t0 = DateTimeOffset.UtcNow;

        tracker.Observe(new[] { Cli("s", SessionActivity.WaitingTurn, t0) }, claudeForeground: false, t0);
        Assert.Empty(store.Load()); // en attente → rien encore

        var t1 = t0.AddSeconds(5);
        tracker.Observe(new[] { Cli("s", SessionActivity.Working, t1) }, claudeForeground: false, t1);
        Assert.True(store.Load().ContainsKey("s")); // attente → Working = répondu = traité
    }

    [Fact]
    public void NET01_disparition_seule_ne_traite_pas()
    {
        var store = new TreatedStore(TempFile());
        var tracker = new SessionTreatmentTracker(store);
        var t0 = DateTimeOffset.UtcNow;

        tracker.Observe(new[] { Cli("s", SessionActivity.WaitingTurn, t0) }, claudeForeground: false, t0);
        var t1 = t0.AddSeconds(5);
        tracker.Observe(System.Array.Empty<SessionSnapshot>(), claudeForeground: false, t1); // disparue

        Assert.Empty(store.Load()); // la disparition n'est pas un « répondu »
    }

    [Fact]
    public void NET03_reapparition_purge_l_entree()
    {
        var store = new TreatedStore(TempFile());
        var tracker = new SessionTreatmentTracker(store);
        var t0 = DateTimeOffset.UtcNow;
        var t1 = t0.AddSeconds(5);

        // NET-01 : la session est traitée (store contient "s").
        tracker.Observe(new[] { Cli("s", SessionActivity.WaitingTurn, t0) }, false, t0);
        tracker.Observe(new[] { Cli("s", SessionActivity.Working, t1) }, false, t1);
        Assert.True(store.Load().ContainsKey("s"));

        // Nouvel épisode d'attente PLUS RÉCENT → purge (réapparition).
        var t2 = t1.AddSeconds(5);
        tracker.Observe(new[] { Cli("s", SessionActivity.WaitingTurn, t2) }, false, t2);
        Assert.False(store.Load().ContainsKey("s"));
    }

    [Fact]
    public void NET02_focus_continu_2_5s_acquitte()
    {
        var store = new TreatedStore(TempFile());
        var tracker = new SessionTreatmentTracker(store);
        var t0 = DateTimeOffset.UtcNow;

        tracker.Observe(new[] { Fg(SessionActivity.WaitingTurn, t0) }, claudeForeground: true, t0);
        tracker.Observe(new[] { Fg(SessionActivity.WaitingTurn, t0.AddSeconds(1.5)) }, claudeForeground: true, t0.AddSeconds(1.5));
        Assert.False(store.Load().ContainsKey("desktop:foreground:chat")); // < 2,5 s → pas encore acquitté

        tracker.Observe(new[] { Fg(SessionActivity.WaitingTurn, t0.AddSeconds(3)) }, claudeForeground: true, t0.AddSeconds(3));
        Assert.True(store.Load().ContainsKey("desktop:foreground:chat")); // >= 2,5 s → acquitté
    }

    [Fact]
    public void NET02_interruption_focus_remet_a_zero()
    {
        var store = new TreatedStore(TempFile());
        var tracker = new SessionTreatmentTracker(store);
        var t0 = DateTimeOffset.UtcNow;

        tracker.Observe(new[] { Fg(SessionActivity.WaitingTurn, t0) }, claudeForeground: true, t0);
        tracker.Observe(new[] { Fg(SessionActivity.WaitingTurn, t0.AddSeconds(1)) }, claudeForeground: false, t0.AddSeconds(1)); // interruption
        tracker.Observe(new[] { Fg(SessionActivity.WaitingTurn, t0.AddSeconds(2)) }, claudeForeground: true, t0.AddSeconds(2)); // repart
        tracker.Observe(new[] { Fg(SessionActivity.WaitingTurn, t0.AddSeconds(3)) }, claudeForeground: true, t0.AddSeconds(3));

        // Compteur reparti à t0+2s → seulement 1 s de tenue continue < 2,5 s → PAS acquitté.
        Assert.False(store.Load().ContainsKey("desktop:foreground:chat"));
    }

    [Fact]
    public void NET02_ne_declenche_pas_sans_focus()
    {
        var store = new TreatedStore(TempFile());
        var tracker = new SessionTreatmentTracker(store);
        var t0 = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++) // > 2,5 s couverts, mais focus=false partout
            tracker.Observe(new[] { Fg(SessionActivity.WaitingTurn, t0.AddSeconds(i)) }, claudeForeground: false, t0.AddSeconds(i));

        Assert.Empty(store.Load());
    }

    // --- Intégration bout-en-bout via SessionMonitor (faux focus + faux ISessionSource) ---

    private static string TempDir() { var d = Path.Combine(Path.GetTempPath(), "chronos-treated-mon-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    // Faux focus premier-plan : valeur pilotée par le test.
    private sealed class FakeForegroundWatch : IForegroundWatch
    {
        public bool Value;
        public bool IsClaudeForeground() => Value;
    }

    // Faux ISessionSource MUTABLE : la liste rendue peut changer entre deux Read pour simuler l'évolution
    // des cycles (attente → répondu → nouvel épisode).
    private sealed class MutableSource : ISessionSource
    {
        public List<SessionSnapshot> Snaps = new();
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => Snaps;
    }

    private static SessionMonitor BuildMonitor(MutableSource source, TreatedStore treated, SessionTreatmentTracker tracker,
        FakeForegroundWatch focus, ArchiveStore? archive = null)
        => new SessionMonitor(TempDir(), new TranscriptSessionSource(TempDir()),
            archive ?? new ArchiveStore(TempFile()), source, treated, tracker, focus);

    [Fact]
    public void NET01_le_monitor_masque_apres_reponse()
    {
        var store = new TreatedStore(TempFile());
        var tracker = new SessionTreatmentTracker(store);
        var source = new MutableSource();
        var monitor = BuildMonitor(source, store, tracker, new FakeForegroundWatch());
        var t0 = DateTimeOffset.UtcNow;

        source.Snaps = new List<SessionSnapshot> { Cli("s", SessionActivity.WaitingTurn, t0) };
        Assert.Contains(monitor.Read(t0), s => s.SessionId == "s"); // en attente → visible

        var t1 = t0.AddSeconds(5);
        source.Snaps = new List<SessionSnapshot> { Cli("s", SessionActivity.Working, t1) };
        Assert.DoesNotContain(monitor.Read(t1), s => s.SessionId == "s"); // répondu → masquée
    }

    [Fact]
    public void NET03_le_monitor_la_reaffiche_sur_nouvel_episode()
    {
        var store = new TreatedStore(TempFile());
        var tracker = new SessionTreatmentTracker(store);
        var source = new MutableSource();
        var monitor = BuildMonitor(source, store, tracker, new FakeForegroundWatch());
        var t0 = DateTimeOffset.UtcNow;
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
        var store = new TreatedStore(TempFile());
        var tracker = new SessionTreatmentTracker(store);
        var source = new MutableSource();
        var archive = new ArchiveStore(TempFile());
        var monitor = BuildMonitor(source, store, tracker, new FakeForegroundWatch(), archive);
        var t0 = DateTimeOffset.UtcNow;

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
    public void NET02_le_monitor_masque_apres_focus_2_5s()
    {
        var store = new TreatedStore(TempFile());
        var tracker = new SessionTreatmentTracker(store);
        var source = new MutableSource();
        var focus = new FakeForegroundWatch { Value = true };
        var monitor = BuildMonitor(source, store, tracker, focus);
        var t0 = DateTimeOffset.UtcNow;

        source.Snaps = new List<SessionSnapshot> { Fg(SessionActivity.WaitingTurn, t0) };
        Assert.Contains(monitor.Read(t0), s => s.SessionId == "desktop:foreground:chat");

        source.Snaps = new List<SessionSnapshot> { Fg(SessionActivity.WaitingTurn, t0.AddSeconds(1.5)) };
        Assert.Contains(monitor.Read(t0.AddSeconds(1.5)), s => s.SessionId == "desktop:foreground:chat"); // < 2,5 s

        source.Snaps = new List<SessionSnapshot> { Fg(SessionActivity.WaitingTurn, t0.AddSeconds(3)) };
        Assert.DoesNotContain(monitor.Read(t0.AddSeconds(3)), s => s.SessionId == "desktop:foreground:chat"); // acquittée
    }

    [Fact]
    public void Monitor_sans_tracker_ni_treated_ne_regresse_pas()
    {
        // SessionMonitor construit SANS les nouveaux paramètres (comme aujourd'hui) → aucune exception,
        // la session bureau reste visible (hystérésis désactivée = null).
        var now = DateTimeOffset.UtcNow;
        var source = new MutableSource { Snaps = new List<SessionSnapshot> { Cli("s", SessionActivity.WaitingTurn, now) } };
        var monitor = new SessionMonitor(TempDir(), new TranscriptSessionSource(TempDir()),
            new ArchiveStore(TempFile()), source);

        Assert.Contains(monitor.Read(now), s => s.SessionId == "s");
    }
}
