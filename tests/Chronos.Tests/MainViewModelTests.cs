using System.Diagnostics;
using System.IO;
using Chronos.Models;
using Chronos.Services;
using Chronos.ViewModels;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la couche présentation temps réel :
/// - RAF-04 : un snapshot poussé HORS thread UI est appliqué via IUiDispatcher.Post EXACTEMENT une fois
///   (frontière de thread unique), et les sous-VM reflètent le snapshot ; DataUnavailable = deux fenêtres Unavailable.
/// - RAF-03 : Interpolate(now) est PUR (recalcule fraction d'arc + compte à rebours) SANS aucun I/O
///   (GetAsync jamais appelé au tick) ; staleness dérivée de SourceCapturedAt.
///
/// Tests en [Fact] SIMPLE (pas [WpfFact]) : preuve que le DispatcherTimer n'est PAS créé dans le ctor
/// (il est créé côté UI via StartClock, Pitfall 4). FakeUiDispatcher + FakeClock + FakeUsageProvider.
/// </summary>
public class MainViewModelTests
{
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

    private static ChronosPaths TempPaths()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ChronosVmTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return new ChronosPaths(Path.Combine(dir, "usage.json"), Path.Combine(dir, "projects"));
    }

    private static WindowState Readable(WindowKind kind, DateTimeOffset now, double util = 0.5, TimeSpan? remaining = null) =>
        new()
        {
            Kind = kind,
            Reliability = SourceReliability.Exact,
            Utilization = util,
            ResetsAt = now + (remaining ?? TimeSpan.FromHours(2)),
        };

    private static readonly DateTimeOffset Now = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    // Orchestrateur non démarré : sert de source d'abonnement pour construire un VM déterministe
    // (les tests d'application/interpolation appellent ApplySnapshot/Interpolate directement).
    private static MainViewModel NewVm(out FakeUiDispatcher ui, out FakeClock clock, out FakeUsageProvider provider, bool onUiThread = true)
    {
        provider = new FakeUsageProvider();
        var options = new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero);
        var orch = new RefreshOrchestrator(provider, TempPaths(), options);
        ui = new FakeUiDispatcher { OnUiThread = onUiThread };
        clock = new FakeClock(Now);
        return new MainViewModel(orch, ui, clock);
    }

    // --- RAF-04 : franchissement de thread unique via IUiDispatcher.Post (exactement une fois) ---

    [Fact]
    public async Task Snapshot_pousse_hors_thread_UI_est_marshale_une_seule_fois()
    {
        var snap = new UsageSnapshot
        {
            FiveHour = Readable(WindowKind.FiveHour, Now, util: 0.5),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now,
        };
        var provider = new FakeUsageProvider { Next = snap };
        var options = new RefreshOptions(TimeSpan.FromMinutes(10), TimeSpan.Zero); // isole la charge initiale
        var orch = new RefreshOrchestrator(provider, TempPaths(), options);
        var ui = new FakeUiDispatcher { OnUiThread = false };  // simule le thread pool de l'orchestrateur
        var clock = new FakeClock(Now);
        var vm = new MainViewModel(orch, ui, clock);
        try
        {
            await orch.StartAsync(CancellationToken.None); // charge initiale → SnapshotChanged (thread pool)
            var applied = await WaitUntilAsync(() => ui.PostCount >= 1, 2000);
            Assert.True(applied, "le snapshot initial doit être marshalé via IUiDispatcher.Post");
        }
        finally { await orch.StopAsync(CancellationToken.None); }

        Assert.Equal(1, ui.PostCount);                 // frontière franchie EXACTEMENT une fois
        Assert.Equal(0.5, vm.FiveHour.Utilization);    // propriétés reflètent le snapshot
        Assert.False(vm.DataUnavailable);              // une fenêtre lisible → données disponibles
    }

    // --- RAF-04 : DataUnavailable vrai SSI les deux fenêtres sont Unavailable ---

    [Fact]
    public void DataUnavailable_vrai_seulement_si_les_deux_fenetres_indisponibles()
    {
        var vm = NewVm(out _, out _, out _);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = WindowState.Unavailable(WindowKind.FiveHour),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
        });
        Assert.True(vm.DataUnavailable);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = Readable(WindowKind.FiveHour, Now),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now,
        });
        Assert.False(vm.DataUnavailable);
    }

    // --- RAF-03 : Interpolate(now) pur → fraction décroît, countdown change, AUCUN I/O au tick ---

    [Fact]
    public void Interpolate_recalcule_sans_aucun_IO_au_tick()
    {
        var vm = NewVm(out _, out var clock, out var provider);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = Readable(WindowKind.FiveHour, Now, remaining: TimeSpan.FromHours(2)),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now,
        });

        var fraction0 = vm.FiveHour.FractionRemaining; // 2 h / 5 h = 0.4
        var texte0 = vm.FiveHour.CountdownText;         // "2 h 00"

        clock.UtcNow = Now + TimeSpan.FromHours(1);     // +1 h
        vm.Interpolate(clock.UtcNow);

        Assert.True(vm.FiveHour.FractionRemaining < fraction0, "la fraction restante doit décroître dans le temps");
        Assert.NotEqual(texte0, vm.FiveHour.CountdownText);
        Assert.Equal(0, provider.GetCount); // Pitfall 1 : aucune relecture disque au tick d'interpolation
    }

    // --- RAF-03 : staleness dérivée de SourceCapturedAt (> 2 min → périmé) ---

    [Fact]
    public void IsStale_vrai_quand_la_capture_depasse_deux_minutes()
    {
        var vm = NewVm(out _, out _, out _);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = Readable(WindowKind.FiveHour, Now),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now - TimeSpan.FromMinutes(3), // capturé il y a 3 min
        });
        Assert.True(vm.IsStale);

        vm.ApplySnapshot(new UsageSnapshot
        {
            FiveHour = Readable(WindowKind.FiveHour, Now),
            SevenDay = WindowState.Unavailable(WindowKind.SevenDay),
            SourceCapturedAt = Now, // frais
        });
        Assert.False(vm.IsStale);
    }
}
