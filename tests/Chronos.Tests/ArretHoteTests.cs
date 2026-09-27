using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Services.Historique.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Quick 260927 — « Quitter Chronos » laissait un processus zombie. Cause : le Host était démarré par
/// <c>await StartAsync()</c> sur le thread UI, <c>RefreshOrchestrator.ExecuteAsync</c> commençait par
/// <c>await Task.Yield()</c> (toute la boucle revenait sur le Dispatcher), puis <c>OnExit</c> bloquait ce même
/// thread sur <c>StopAsync().GetAwaiter().GetResult()</c> : la reprise de <c>await base.StopAsync(ct)</c>
/// attendait le Dispatcher bloqué — interblocage, sans fin.
///
/// Ces tests rejouent EXACTEMENT cette forme : un thread STA qui pompe un Dispatcher WPF avec son
/// <see cref="DispatcherSynchronizationContext"/>, un Host réel (même fabrique que l'app) qui porte les quatre
/// services hébergés de production (providers factices, chemins temporaires), démarré par <c>await</c> sur ce
/// thread. Chaque scénario est BORNÉ : un interblocage se manifeste en échec au bout de la borne, jamais en suite
/// gelée (le thread STA fautif est d'arrière-plan et reste abandonné).
/// </summary>
public class ArretHoteTests
{
    /// <summary>Borne dure d'un scénario complet (démarrage + arrêt). Un arrêt sain prend quelques dizaines de ms.</summary>
    private static readonly TimeSpan Borne = TimeSpan.FromSeconds(10);

    // ------------------------------------------------------------------------------------------
    // Banc : un « thread UI » WPF et un Host miroir de la production
    // ------------------------------------------------------------------------------------------

    /// <summary>Joue <paramref name="scenario"/> sur un thread STA neuf qui pompe un Dispatcher, sous
    /// <see cref="DispatcherSynchronizationContext"/> (comme <c>App.OnStartup</c>). Retourne false si le scénario n'a
    /// pas fini dans la borne (interblocage).</summary>
    private static bool JouerSurThreadUi(Func<int, Task> scenario, out Exception? erreur)
    {
        using var fini = new ManualResetEventSlim();
        Exception? echec = null;
        var thread = new Thread(() =>
        {
            var d = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(d));
            d.InvokeAsync(async () =>
            {
                try { await scenario(Environment.CurrentManagedThreadId); }
                catch (Exception ex) { echec = ex; }
                finally
                {
                    fini.Set();
                    d.InvokeShutdown();
                }
            });
            Dispatcher.Run();
        })
        { IsBackground = true, Name = "ArretHoteTests.UI" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        var termine = fini.Wait(Borne);
        erreur = echec;
        return termine;
    }

    /// <summary>Host construit par la MÊME fabrique que l'app (<c>Host.CreateApplicationBuilder</c>) et portant les
    /// services hébergés de production, dans l'ORDRE d'inscription d'App.xaml.cs. Tout est temporaire : aucun
    /// fichier du vrai profil n'est touché.</summary>
    private static IHost HostMiroir(Action<IServiceCollection>? complement = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ChronosArret_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var paths = new ChronosPaths(Path.Combine(dir, "usage.json"), Path.Combine(dir, "projects"));

        var builder = Host.CreateApplicationBuilder();
        var services = builder.Services;
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(paths);

        var (autorite, _, _, _) = ChronosTokenAuthorityTests.Autorite(ChronosTokenAuthorityTests.Maintenant.AddHours(1));
        services.AddSingleton(_ => new TokenRefreshService(autorite));
        services.AddHostedService(sp => sp.GetRequiredService<TokenRefreshService>());

        services.AddSingleton(sp => new JournalReleves(paths.HistoriqueDir, sp.GetRequiredService<IClock>()));
        services.AddSingleton(sp => new JournalisationUsageProvider(
            inner: new FakeUsageProvider(),
            journal: sp.GetRequiredService<JournalReleves>(),
            etatServeur: new FakeEtatServeur(),
            authStatus: new FakeAuthStatus(),
            clock: sp.GetRequiredService<IClock>()));
        services.AddHostedService(sp => sp.GetRequiredService<JournalisationUsageProvider>());

        services.AddSingleton(sp => new ReconstructionTokens(paths,
            new MagasinAgregats(paths.HistoriqueDir, sp.GetRequiredService<IClock>()),
            new IndexMessages(paths.HistoriqueDir, sp.GetRequiredService<IClock>()),
            sp.GetRequiredService<IClock>()));
        services.AddHostedService(sp => sp.GetRequiredService<ReconstructionTokens>());

        // Périodique court : la boucle de l'orchestrateur tourne pour de vrai pendant le scénario.
        services.AddSingleton(new RefreshOptions(TimeSpan.FromMilliseconds(50), TimeSpan.Zero));
        services.AddSingleton(sp => new RefreshOrchestrator(
            sp.GetRequiredService<JournalisationUsageProvider>(), paths, sp.GetRequiredService<RefreshOptions>()));
        services.AddHostedService(sp => sp.GetRequiredService<RefreshOrchestrator>());

        complement?.Invoke(services);
        return builder.Build();
    }

    // ------------------------------------------------------------------------------------------
    // 1. Le chemin d'OnExit se termine, et proprement
    // ------------------------------------------------------------------------------------------

    /// <summary>La mutation de référence : sur le code 3.3.0, ce scénario ne se termine JAMAIS (le processus
    /// zombie). Corrigé : l'arrêt est propre et bien plus court que le délai accordé.</summary>
    [Fact]
    public void Le_chemin_d_arret_d_OnExit_se_termine_sous_le_contexte_du_Dispatcher()
    {
        bool propre = false;
        string? cause = "non joué";
        var chrono = new Stopwatch();

        var termine = JouerSurThreadUi(async _ =>
        {
            var host = HostMiroir();
            await host.StartAsync();       // comme App.OnStartup : sur le thread UI, contexte capturé
            await Task.Delay(300);         // la boucle de l'orchestrateur tourne (plusieurs ticks de 50 ms)
            chrono.Start();
            propre = ArretHote.Arreter(host, ArretHote.DelaiParDefaut, out cause);   // comme App.OnExit : thread UI BLOQUÉ
            chrono.Stop();
        }, out var erreur);

        Assert.True(termine, $"Arrêt BLOQUÉ au-delà de {Borne.TotalSeconds} s sous le Dispatcher : le processus resterait zombie.");
        Assert.Null(erreur);
        Assert.True(propre, "Arrêt non propre : " + cause);
        Assert.True(chrono.Elapsed < TimeSpan.FromSeconds(2), $"Arrêt trop lent : {chrono.Elapsed.TotalMilliseconds:F0} ms.");
    }

    // ------------------------------------------------------------------------------------------
    // 2. Couche services : aucune reprise n'attend le thread UI
    // ------------------------------------------------------------------------------------------

    /// <summary>Défense en profondeur : même si un appelant bloquait ENCORE le thread UI sur l'arrêt (la forme
    /// 3.3.0 d'OnExit, jouée ici délibérément), les services hébergés ne doivent avoir aucune reprise en attente
    /// du Dispatcher. Rougit si <c>ExecuteAsync</c> revient au contexte (<c>await Task.Yield()</c>) ou si un
    /// <c>await</c> du chemin d'arrêt perd son <c>ConfigureAwait(false)</c>.</summary>
    [Fact]
    public void Les_services_heberges_s_arretent_meme_si_le_thread_UI_est_bloque_sur_StopAsync()
    {
        var termine = JouerSurThreadUi(async _ =>
        {
            var host = HostMiroir();
            await host.StartAsync();
            await Task.Delay(300);
            try
            {
                // Forme 3.3.0 d'OnExit, VOLONTAIREMENT : elle ne doit plus pouvoir interbloquer.
                host.StopAsync().GetAwaiter().GetResult();
            }
            finally { host.Dispose(); }
        }, out var erreur);

        Assert.True(termine, "Un service hébergé attend le thread UI pendant l'arrêt : interblocage.");
        Assert.Null(erreur);
    }

    /// <summary>La boucle de l'orchestrateur ne vit pas sur le thread UI : <c>SnapshotChanged</c> est émis hors UI
    /// (le VM marshalle déjà via IUiDispatcher.Post, RAF-04).</summary>
    [Fact]
    public void SnapshotChanged_est_emis_hors_du_thread_UI()
    {
        int threadUi = -1, threadEmission = -1;

        var termine = JouerSurThreadUi(async idUi =>
        {
            threadUi = idUi;
            var host = HostMiroir();
            var orch = host.Services.GetRequiredService<RefreshOrchestrator>();
            orch.SnapshotChanged += (_, _) => Interlocked.CompareExchange(ref threadEmission, Environment.CurrentManagedThreadId, -1);
            await host.StartAsync();
            var limite = DateTime.UtcNow.AddSeconds(3);
            while (Volatile.Read(ref threadEmission) == -1 && DateTime.UtcNow < limite) await Task.Delay(20);
            ArretHote.Arreter(host, ArretHote.DelaiParDefaut, out _);
        }, out var erreur);

        Assert.True(termine, "Arrêt bloqué sous le Dispatcher.");
        Assert.Null(erreur);
        Assert.NotEqual(-1, threadEmission);
        Assert.NotEqual(threadUi, threadEmission);
    }

    // ------------------------------------------------------------------------------------------
    // 3. L'arrêt est BORNÉ, même face à un service pathologique
    // ------------------------------------------------------------------------------------------

    /// <summary>Service hébergé dont l'arrêt ne finit jamais et ignore l'annulation : le pire cas.</summary>
    private sealed class ServiceQuiNeSArreteJamais : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => new TaskCompletionSource().Task;
    }

    [Fact]
    public void L_arret_rend_la_main_dans_le_delai_meme_si_un_service_ne_s_arrete_jamais()
    {
        bool propre = true;
        string? cause = null;
        var chrono = new Stopwatch();
        var delai = TimeSpan.FromMilliseconds(500);

        var termine = JouerSurThreadUi(async _ =>
        {
            var host = HostMiroir(s => s.AddHostedService<ServiceQuiNeSArreteJamais>());
            await host.StartAsync();
            chrono.Start();
            propre = ArretHote.Arreter(host, delai, out cause);
            chrono.Stop();
        }, out var erreur);

        Assert.True(termine, "L'arrêt n'est pas borné : un seul service récalcitrant suffit à laisser un zombie.");
        Assert.Null(erreur);
        Assert.False(propre, "Un arrêt dépassé ne doit pas se dire propre.");
        Assert.False(string.IsNullOrWhiteSpace(cause), "Un arrêt dépassé doit dire pourquoi (ligne de diagnostic).");
        Assert.True(chrono.Elapsed < TimeSpan.FromSeconds(3), $"Arrêt non borné : {chrono.Elapsed.TotalMilliseconds:F0} ms.");
    }

    // ------------------------------------------------------------------------------------------
    // 4. Gardes textuelles : la forme fautive ne revient pas
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Garde_aucun_ExecuteAsync_ne_commence_par_Task_Yield_et_aucun_arret_bloquant_sur_le_contexte()
    {
        var racine = CheminSources();
        Assert.False(string.IsNullOrWhiteSpace(racine), "L'attribut AssemblyMetadata(\"CheminSourcesChronos\") manque : cette garde ne lirait rien.");
        var fichiers = Directory.GetFiles(racine, "*.cs", SearchOption.AllDirectories)
                                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                                .ToList();
        Assert.Contains(fichiers, f => f.EndsWith("RefreshOrchestrator.cs", StringComparison.Ordinal));   // anti-muet

        var tetes = new Regex(@"ExecuteAsync\s*\(\s*CancellationToken\s+\w+\s*\)\s*\{(?:\s*//[^\n]*\n)*\s*await\s+Task\.Yield\(\)");
        var arretBloquant = new Regex(@"StopAsync\([^;]*?\)\s*\.GetAwaiter\(\)\s*\.GetResult\(\)");
        foreach (var f in fichiers)
        {
            var texte = File.ReadAllText(f);
            Assert.False(tetes.IsMatch(texte), $"{Path.GetFileName(f)} : ExecuteAsync commence par « await Task.Yield() » — la boucle reviendrait au contexte UI.");
            Assert.False(arretBloquant.IsMatch(texte), $"{Path.GetFileName(f)} : StopAsync().GetAwaiter().GetResult() — arrêt bloquant, sans borne, sur le thread appelant.");
        }

        // Les awaits du chemin Execute/Stop de l'orchestrateur ne reviennent jamais au contexte capturé.
        var orch = File.ReadAllText(Path.Combine(racine, "Services", "RefreshOrchestrator.cs"));
        foreach (Match m in Regex.Matches(orch, @"\bawait\b[^;\n]*"))
            Assert.Contains("ConfigureAwait(false)", m.Value, StringComparison.Ordinal);

        // App.OnExit passe par le chemin borné.
        var app = File.ReadAllText(Path.Combine(racine, "App.xaml.cs"));
        Assert.Contains("ArretHote.Arreter(", app, StringComparison.Ordinal);
    }

    private static string CheminSources()
        => typeof(ArretHoteTests).Assembly
               .GetCustomAttributes<AssemblyMetadataAttribute>()
               .FirstOrDefault(a => a.Key == "CheminSourcesChronos")?.Value
           ?? "";
}
