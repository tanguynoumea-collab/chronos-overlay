using System.Diagnostics;
using System.IO;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve l'horloge DONNÉES (RAF-01 + RAF-02) : le RefreshOrchestrator possède un FileSystemWatcher
/// débouncé sur usage.json ET un PeriodicTimer de secours ; les deux alimentent un Channel(1, DropWrite)
/// qu'une boucle consommateur UNIQUE lit → GetAsync sérialisé (jamais concurrent) → SnapshotChanged.
///
/// Les seams internes (TryTrigger / RecreateWatcher, visibles via InternalsVisibleTo) rendent les tests
/// déterministes sans dépendre du timing réel du FileSystemWatcher (best-effort).
/// </summary>
public class RefreshOrchestratorTests
{
    // Attend qu'une condition devienne vraie (poll), jusqu'à timeoutMs. Retourne l'état final.
    private static async Task<bool> WaitUntilAsync(Func<bool> cond, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (cond()) return true;
            await Task.Delay(15);
        }
        return cond();
    }

    // ChronosPaths pointant sur un répertoire temporaire unique (isole le vrai profil utilisateur).
    private static ChronosPaths TempPaths(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "ChronosTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return new ChronosPaths(Path.Combine(dir, "usage.json"), Path.Combine(dir, "projects"));
    }

    private static ChronosPaths TempPaths() => TempPaths(out _);

    // --- RAF-02 : le PeriodicTimer déclenche GetAsync SANS aucun événement watcher (filet de sécurité) ---

    [Fact]
    public async Task PeriodicTimer_declenche_GetAsync_sans_evenement_watcher()
    {
        var provider = new FakeUsageProvider();
        var options = new RefreshOptions(TimeSpan.FromMilliseconds(50), TimeSpan.Zero);
        var orch = new RefreshOrchestrator(provider, TempPaths(), options);
        try
        {
            await orch.StartAsync(CancellationToken.None);
            // Charge initiale (1) + ticks périodiques 50 ms → GetCount croît vite au-delà de 1.
            var ok = await WaitUntilAsync(() => provider.GetCount >= 2, 2000);
            Assert.True(ok, $"GetCount attendu >= 2 via PeriodicTimer, obtenu {provider.GetCount}");
        }
        finally { await orch.StopAsync(CancellationToken.None); }
    }

    // --- RAF-01 : une écriture sur usage.json déclenche un GetAsync (watcher débouncé) ---

    [Fact]
    public async Task Ecriture_usage_json_declenche_GetAsync()
    {
        var provider = new FakeUsageProvider();
        var paths = TempPaths(out _);
        // Périodique très long pour ISOLER le watcher ; debounce court pour un test rapide.
        var options = new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.FromMilliseconds(20));
        var orch = new RefreshOrchestrator(provider, paths, options);
        try
        {
            await orch.StartAsync(CancellationToken.None);
            await WaitUntilAsync(() => provider.GetCount >= 1, 2000); // charge initiale
            var baseline = provider.GetCount;

            await File.WriteAllTextAsync(paths.UsageFile, "{}"); // le pont écrit ce fichier en prod

            var ok = await WaitUntilAsync(() => provider.GetCount > baseline, 3000);
            Assert.True(ok, $"GetCount attendu > {baseline} après écriture usage.json, obtenu {provider.GetCount}");
        }
        finally { await orch.StopAsync(CancellationToken.None); }
    }

    // --- RAF-01 : une rafale de déclencheurs est coalescée (Channel(1, DropWrite) + consommateur unique) ---

    [Fact]
    public async Task Rafale_de_declencheurs_est_coalescee()
    {
        using var gate = new ManualResetEventSlim(false);
        var provider = new FakeUsageProvider { Gate = gate };
        var options = new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero);
        var orch = new RefreshOrchestrator(provider, TempPaths(), options);
        try
        {
            await orch.StartAsync(CancellationToken.None);
            // La charge initiale déclenche GetAsync #1 qui BLOQUE sur le gate.
            var blocked = await WaitUntilAsync(() => provider.GetCount == 1, 2000);
            Assert.True(blocked, "le 1er GetAsync doit être en vol (bloqué sur le gate)");

            // Empiler une rafale PENDANT le blocage : le Channel(1) n'en garde qu'un, le reste est DropWrite.
            for (int i = 0; i < 20; i++) orch.TryTrigger();

            gate.Set(); // libère → le consommateur unique traite au plus UN rattrapage coalescé
            await WaitUntilAsync(() => provider.GetCount >= 2, 2000);
            await Task.Delay(200); // fenêtre pour d'éventuels GetAsync surnuméraires (ne doivent PAS survenir)

            Assert.True(provider.GetCount <= 2,
                $"coalescence attendue (<= 2 malgré 20 déclencheurs), obtenu {provider.GetCount}");
        }
        finally { gate.Set(); await orch.StopAsync(CancellationToken.None); }
    }

    // --- RAF-01 : l'événement Error entraîne la recréation du watcher sans perdre la capacité de rafraîchir ---

    [Fact]
    public async Task Error_du_watcher_entraine_recreation_sans_perdre_le_refresh()
    {
        var provider = new FakeUsageProvider();
        var options = new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero);
        var orch = new RefreshOrchestrator(provider, TempPaths(), options);
        try
        {
            await orch.StartAsync(CancellationToken.None);
            await WaitUntilAsync(() => provider.GetCount >= 1, 2000);
            var baseline = provider.GetCount;

            orch.RecreateWatcher(); // = corps du handler Error (dispose + recrée + rescan)

            var rescanned = await WaitUntilAsync(() => provider.GetCount > baseline, 2000);
            Assert.True(rescanned, "après recréation (Error), un rescan doit relancer GetAsync");

            // Le watcher recréé doit toujours pouvoir rafraîchir sur un déclencheur ultérieur.
            var after = provider.GetCount;
            orch.TryTrigger();
            var stillWorks = await WaitUntilAsync(() => provider.GetCount > after, 2000);
            Assert.True(stillWorks, "le watcher recréé doit conserver la capacité de rafraîchir");
        }
        finally { await orch.StopAsync(CancellationToken.None); }
    }

    // --- SnapshotChanged : chaque GetAsync fait remonter un UsageSnapshot via l'event de l'orchestrateur ---

    [Fact]
    public async Task SnapshotChanged_est_emis_apres_GetAsync()
    {
        var provider = new FakeUsageProvider();
        var options = new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero);
        var orch = new RefreshOrchestrator(provider, TempPaths(), options);
        int emissions = 0;
        orch.SnapshotChanged += (_, _) => Interlocked.Increment(ref emissions);
        try
        {
            await orch.StartAsync(CancellationToken.None);
            var ok = await WaitUntilAsync(() => Volatile.Read(ref emissions) >= 1, 2000);
            Assert.True(ok, "au moins une émission SnapshotChanged attendue après la charge initiale");
        }
        finally { await orch.StopAsync(CancellationToken.None); }
    }
}
