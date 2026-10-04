using System.IO;
using Chronos.Models;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// P-04 (42.3, audit externe DS-ARCH-04) — un diagnostic n'est pas un cycle d'observation. Le rapport lit le moniteur
/// de sessions du widget ; s'il faisait observer le détecteur de traitement, la simple génération d'un rapport pourrait
/// faire passer une session à « répondue » ou « lue » et l'inscrire dans treated.json — le masquage d'une session en
/// attente dépendrait alors de la date d'un diagnostic. Ces tests construisent le scénario où un cycle d'observation
/// ÉCRIRAIT treated.json et vérifient que deux rapports successifs n'y touchent pas.
/// Tous les chemins sont temporaires ; aucun réglage Claude n'est injecté (reglagesClaude: null).
/// </summary>
public class DiagnosticSansEffetDeBordTests
{
    private static readonly DateTimeOffset T = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-diag-passif-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        Assert.StartsWith(Path.GetTempPath(), d);
        return d;
    }

    private static ChronosPaths TempPaths()
    {
        var dir = TempDir();
        return new ChronosPaths(Path.Combine(dir, "usage.json"), Path.Combine(dir, "projects"));
    }

    private sealed class StubProvider : IUsageProvider
    {
        public Task<UsageSnapshot> GetAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(UsageSnapshot.Empty);
    }

    private sealed class MutableSource : ISessionSource
    {
        public List<SessionSnapshot> Snaps = new();
        public IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now) => Snaps;
    }

    [Fact]
    public async Task Le_diagnostic_ne_modifie_pas_treated_json()
    {
        var dossier = TempDir();
        var cheminTraitees = Path.Combine(dossier, "treated.json");
        var store = new TreatedStore(cheminTraitees, new FakeClock(T));
        var tracker = new SessionTreatmentTracker(store);
        var source = new MutableSource();
        var moniteur = new SessionMonitor(TempDir(), source, new ArchiveStore(Path.Combine(dossier, "archived.json")), store, tracker);

        // Cycle 1, observé par le widget : la session attend.
        source.Snaps = new List<SessionSnapshot> { new("s", "Proj", SessionActivity.WaitingTurn, null, T) };
        moniteur.Read(T);
        Assert.False(File.Exists(cheminTraitees));

        // La même source dit maintenant un travail : un cycle d'observation inscrirait « répondue » (NET-01).
        var t2 = T.AddSeconds(5);
        source.Snaps = new List<SessionSnapshot> { new("s", "Proj", SessionActivity.Working, null, t2) };

        var paths = TempPaths();
        var diag = new DiagnosticService(paths, new SettingsService(paths), new StubProvider(), new FakeClock(t2),
                                         moniteurSessions: moniteur, reglagesClaude: null);

        var rapport1 = await diag.BuildReportAsync();
        var rapport2 = await diag.BuildReportAsync();

        Assert.False(string.IsNullOrWhiteSpace(rapport1));
        Assert.False(string.IsNullOrWhiteSpace(rapport2));
        Assert.False(File.Exists(cheminTraitees), "le diagnostic ne doit pas créer treated.json");
        Assert.Null(store.DerniereEcriture);
        Assert.Empty(store.Load());
    }

    [Fact]
    public async Task Le_diagnostic_laisse_les_octets_de_treated_json_intacts()
    {
        var dossier = TempDir();
        var cheminTraitees = Path.Combine(dossier, "treated.json");

        // Un magasin déjà peuplé par un autre exemplaire (une session sans rapport avec le scénario) : le fichier existe.
        new TreatedStore(cheminTraitees, new FakeClock(T)).Set("autre", T.AddMinutes(-10).ToUnixTimeMilliseconds());
        var avant = File.ReadAllBytes(cheminTraitees);

        var store = new TreatedStore(cheminTraitees, new FakeClock(T));
        var tracker = new SessionTreatmentTracker(store);
        var source = new MutableSource();
        var moniteur = new SessionMonitor(TempDir(), source, new ArchiveStore(Path.Combine(dossier, "archived.json")), store, tracker);

        source.Snaps = new List<SessionSnapshot> { new("s", "Proj", SessionActivity.WaitingTurn, null, T) };
        moniteur.Read(T);
        Assert.Equal(avant, File.ReadAllBytes(cheminTraitees));
        // Ce magasin n'a rien écrit : DerniereEcriture rend la date du fichier sur le disque — elle doit rester la même.
        var ecritureAvant = store.DerniereEcriture;
        Assert.NotNull(ecritureAvant);

        var t2 = T.AddSeconds(5);
        source.Snaps = new List<SessionSnapshot> { new("s", "Proj", SessionActivity.Working, null, t2) };

        var paths = TempPaths();
        var diag = new DiagnosticService(paths, new SettingsService(paths), new StubProvider(), new FakeClock(t2),
                                         moniteurSessions: moniteur, reglagesClaude: null);
        await diag.BuildReportAsync();
        await diag.BuildReportAsync();

        Assert.Equal(avant, File.ReadAllBytes(cheminTraitees));
        Assert.Equal(ecritureAvant, store.DerniereEcriture);
        Assert.False(store.Load().ContainsKey("s"));
    }
}
