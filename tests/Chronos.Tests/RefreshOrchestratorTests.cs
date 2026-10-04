using System.Diagnostics;
using System.IO;
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

    // --- DS2-01 (42.4) : une exception d'un provider ne tue plus la boucle ; elle est consignée dans chronos.log ---

    /// <summary>Dossier de journal TEMPORAIRE (jamais %APPDATA%\Chronos), garde anti-accident.</summary>
    private static string DossierJournalTemp()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "ChronosOrchFilet_" + Guid.NewGuid().ToString("N"));
        Assert.StartsWith(Path.GetTempPath(), dossier);
        return dossier;
    }

    [Fact]
    public async Task Une_exception_du_provider_ne_tue_pas_la_boucle()
    {
        var dossier = DossierJournalTemp();
        var provider = new FakeUsageProvider { LeverAuProchain = new NullReferenceException("simulée") };
        var orch = new RefreshOrchestrator(provider, new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero), dossierJournal: dossier);
        int emissions = 0;
        orch.SnapshotChanged += (_, _) => Interlocked.Increment(ref emissions);
        try
        {
            await orch.StartAsync(CancellationToken.None);
            Assert.True(await WaitUntilAsync(() => provider.GetCount == 1, 2000), "la charge initiale doit appeler le provider");

            // Le 1er GetAsync a levé : un nouveau déclencheur doit encore être consommé et publié.
            orch.TryTrigger();
            Assert.True(await WaitUntilAsync(() => Volatile.Read(ref emissions) >= 1, 2000), "la boucle doit publier au tick suivant");
            Assert.NotNull(orch.DernierSnapshot);

            var journal = File.ReadAllText(Path.Combine(dossier, JournalIncidents.NomFichier));
            Assert.Contains(JournalIncidents.Marqueur, journal);
            Assert.Contains("rafraîchissement", journal);
            Assert.Contains("NullReferenceException", journal);
        }
        finally
        {
            await orch.StopAsync(CancellationToken.None);
            try { Directory.Delete(dossier, true); } catch { /* nettoyage best-effort */ }
        }
    }

    [Fact]
    public async Task Une_annulation_qui_ne_vient_pas_de_l_arret_ne_tue_pas_la_boucle()
    {
        var dossier = DossierJournalTemp();
        var provider = new FakeUsageProvider { LeverAuProchain = new TaskCanceledException("délai HTTP") };
        var orch = new RefreshOrchestrator(provider, new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero), dossierJournal: dossier);
        int emissions = 0;
        orch.SnapshotChanged += (_, _) => Interlocked.Increment(ref emissions);
        try
        {
            await orch.StartAsync(CancellationToken.None);
            Assert.True(await WaitUntilAsync(() => provider.GetCount == 1, 2000));

            orch.TryTrigger();
            Assert.True(await WaitUntilAsync(() => Volatile.Read(ref emissions) >= 1, 2000), "un délai HTTP ne doit pas arrêter le rafraîchissement");
        }
        finally
        {
            await orch.StopAsync(CancellationToken.None);
            try { Directory.Delete(dossier, true); } catch { /* nettoyage best-effort */ }
        }
    }

    [Fact]
    public async Task L_arret_normal_n_est_jamais_journalise_comme_incident()
    {
        var dossier = DossierJournalTemp();
        using var gate = new ManualResetEventSlim(false);   // jamais libéré : seul l'arrêt débloque le provider
        var provider = new FakeUsageProvider { Gate = gate };
        var orch = new RefreshOrchestrator(provider, new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero), dossierJournal: dossier);

        await orch.StartAsync(CancellationToken.None);
        Assert.True(await WaitUntilAsync(() => provider.GetCount == 1, 2000), "le provider doit être en vol");

        var sw = Stopwatch.StartNew();
        await orch.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5));

        Assert.False(File.Exists(Path.Combine(dossier, JournalIncidents.NomFichier)), "l'arrêt normal ne doit rien journaliser");
    }

    /// <summary>Garde textuelle : la composition racine passe le dossier du journal à l'orchestrateur — sinon le filet
    /// rattraperait les exceptions sans jamais les consigner.</summary>
    [Fact]
    public void App_construit_l_orchestrateur_avec_le_dossier_du_journal()
    {
        var app = File.ReadAllText(Path.Combine(GardesPerimetreTests.CheminSources(), "App.xaml.cs"));
        Assert.Contains("new RefreshOrchestrator(", app, StringComparison.Ordinal);
        Assert.Contains("dossierJournal:", app, StringComparison.Ordinal);
    }
}
