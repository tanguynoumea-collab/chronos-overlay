using System.Diagnostics;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve l'horloge DONNÉES (RAF-01 + RAF-02) : le RefreshOrchestrator possède un PeriodicTimer qui alimente un
/// Channel(1, DropWrite) ; une boucle consommateur UNIQUE le lit → GetAsync sérialisé (jamais concurrent) → SnapshotChanged.
/// Plus aucune surveillance de fichier depuis la 3.5 : le minuteur est la seule horloge.
///
/// Le seam interne TryTrigger (visible via InternalsVisibleTo) rend la coalescence testable sans dépendre du minuteur réel.
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

    // --- RAF-02 : le PeriodicTimer déclenche GetAsync seul (aucune autre horloge) ---

    [Fact]
    public async Task PeriodicTimer_declenche_GetAsync_sans_autre_declencheur()
    {
        var provider = new FakeUsageProvider();
        var options = new RefreshOptions(TimeSpan.FromMilliseconds(50), TimeSpan.Zero);
        var orch = new RefreshOrchestrator(provider, options);
        try
        {
            await orch.StartAsync(CancellationToken.None);
            // Charge initiale (1) + ticks périodiques 50 ms → GetCount croît vite au-delà de 1.
            var ok = await WaitUntilAsync(() => provider.GetCount >= 2, 2000);
            Assert.True(ok, $"GetCount attendu >= 2 via PeriodicTimer, obtenu {provider.GetCount}");
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
        var orch = new RefreshOrchestrator(provider, options);
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

    // --- SnapshotChanged : chaque GetAsync fait remonter un UsageSnapshot via l'event de l'orchestrateur ---

    [Fact]
    public async Task SnapshotChanged_est_emis_apres_GetAsync()
    {
        var provider = new FakeUsageProvider();
        var options = new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero);
        var orch = new RefreshOrchestrator(provider, options);
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

    // --- P-03 (42.3) : l'orchestrateur expose son DERNIER snapshot publié — le diagnostic le lit, il ne relance pas la chaîne ---

    [Fact]
    public async Task DernierSnapshot_est_le_snapshot_emis_par_SnapshotChanged()
    {
        var provider = new FakeUsageProvider();
        var options = new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero);
        var orch = new RefreshOrchestrator(provider, options);
        Chronos.Models.UsageSnapshot? emis = null;
        orch.SnapshotChanged += (_, s) => Volatile.Write(ref emis, s);
        Assert.Null(orch.DernierSnapshot);   // avant toute charge : rien de publié
        try
        {
            await orch.StartAsync(CancellationToken.None);
            var ok = await WaitUntilAsync(() => Volatile.Read(ref emis) is not null, 2000);
            Assert.True(ok, "la charge initiale doit émettre un snapshot");
            Assert.Same(Volatile.Read(ref emis), orch.DernierSnapshot);

            var attendu = await orch.AttendrePremierAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
            Assert.Same(orch.DernierSnapshot, attendu);
        }
        finally { await orch.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task AttendrePremierAsync_rend_null_sans_lever_si_rien_n_est_publie()
    {
        var provider = new FakeUsageProvider();
        var orch = new RefreshOrchestrator(provider, new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero));
        // Orchestrateur NON démarré : aucun relevé ne sera jamais publié.
        var s = await orch.AttendrePremierAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        Assert.Null(s);
        Assert.Equal(0, provider.GetCount);
    }
}
