using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve le cœur de l'auto-disparition par hystérésis (phase 14, réduite en phase 21) : le magasin
/// RÉVERSIBLE <see cref="TreatedStore"/>, le détecteur STATEFUL <see cref="SessionTreatmentTracker"/>
/// (NET-01 répondu, NET-03 réapparition/purge), l'archivage PERMANENT (NET-04) et le branchement dans
/// <see cref="SessionMonitor"/>. Tout se teste avec des séquences de snapshots synthétiques et une horloge
/// injectée — aucune fenêtre réelle. NET-02 (acquittement par focus) a disparu avec la source app-bureau :
/// la branche n'atteignait structurellement jamais une session Claude Code.
/// </summary>
public class TreatedSessionsTests
{
    private static string TempFile() => Path.Combine(Path.GetTempPath(), "chronos-treated-" + Guid.NewGuid().ToString("N") + ".json");
    private static SessionSnapshot Cli(string id, SessionActivity a, DateTimeOffset t) => new(id, "Proj", a, null, t);

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

        tracker.Observe(new[] { Cli("s", SessionActivity.WaitingTurn, t0) }, t0);
        Assert.Empty(store.Load()); // en attente → rien encore

        var t1 = t0.AddSeconds(5);
        tracker.Observe(new[] { Cli("s", SessionActivity.Working, t1) }, t1);
        Assert.True(store.Load().ContainsKey("s")); // attente → Working = répondu = traité
    }

    [Fact]
    public void NET01_disparition_seule_ne_traite_pas()
    {
        var store = new TreatedStore(TempFile());
        var tracker = new SessionTreatmentTracker(store);
        var t0 = DateTimeOffset.UtcNow;

        tracker.Observe(new[] { Cli("s", SessionActivity.WaitingTurn, t0) }, t0);
        var t1 = t0.AddSeconds(5);
        tracker.Observe(System.Array.Empty<SessionSnapshot>(), t1); // disparue

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
        tracker.Observe(new[] { Cli("s", SessionActivity.WaitingTurn, t0) }, t0);
        tracker.Observe(new[] { Cli("s", SessionActivity.Working, t1) }, t1);
        Assert.True(store.Load().ContainsKey("s"));

        // Nouvel épisode d'attente PLUS RÉCENT → purge (réapparition).
        var t2 = t1.AddSeconds(5);
        tracker.Observe(new[] { Cli("s", SessionActivity.WaitingTurn, t2) }, t2);
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
        var store = new TreatedStore(TempFile());
        var tracker = new SessionTreatmentTracker(store);
        var source = new MutableSource();
        var monitor = BuildMonitor(source, store, tracker);
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
        var monitor = BuildMonitor(source, store, tracker);
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
        var monitor = BuildMonitor(source, store, tracker, archive);
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
    public void Monitor_sans_tracker_ni_treated_ne_regresse_pas()
    {
        // SessionMonitor construit SANS les paramètres d'hystérésis → aucune exception, la session en
        // attente reste visible (hystérésis désactivée = null).
        var now = DateTimeOffset.UtcNow;
        var source = new MutableSource { Snaps = new List<SessionSnapshot> { Cli("s", SessionActivity.WaitingTurn, now) } };
        var monitor = new SessionMonitor(TempDir(), source, new ArchiveStore(TempFile()));

        Assert.Contains(monitor.Read(now), s => s.SessionId == "s");
    }
}
