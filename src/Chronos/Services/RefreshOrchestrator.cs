using System.IO;
using System.Threading.Channels;
using Chronos.Models;
using Microsoft.Extensions.Hosting;

namespace Chronos.Services;

/// <summary>
/// Horloge DONNÉES (RAF-01 + RAF-02). BackgroundService NEUTRE (aucun type WPF → reste hors
/// allow-list de ServicesLayerPurityTests). Il possède le FileSystemWatcher débouncé sur usage.json
/// ET un PeriodicTimer de secours configurable ; les deux se contentent d'écrire un déclencheur dans
/// un Channel(1, DropWrite). Une boucle consommateur UNIQUE lit le channel et appelle
/// IUsageProvider.GetAsync un à la fois (jamais de lecture concurrente), puis émet SnapshotChanged.
/// Le marshaling vers le thread UI est fait côté ViewModel (plan 04-02), pas ici.
/// </summary>
public sealed class RefreshOrchestrator : BackgroundService
{
    private readonly IUsageProvider _provider;
    private readonly ChronosPaths _paths;
    private readonly RefreshOptions _options;

    // Capacité 1 + DropWrite = coalescence naturelle : si un rafraîchissement est déjà en file,
    // les déclencheurs surnuméraires d'une rafale sont abandonnés (un seul rattrapage suffit).
    private readonly Channel<bool> _triggers =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        { FullMode = BoundedChannelFullMode.DropWrite });

    private FileSystemWatcher? _watcher;

    /// <summary>Émis (thread pool) après chaque GetAsync avec le snapshot produit. Le VM (04-02)
    /// s'abonne ICI et marshalle via IUiDispatcher — décision verrouillée « le service expose l'event ».</summary>
    public event EventHandler<UsageSnapshot>? SnapshotChanged;

    public RefreshOrchestrator(IUsageProvider provider, ChronosPaths paths, RefreshOptions options)
        => (_provider, _paths, _options) = (provider, paths, options);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Rendre la main à StartAsync immédiatement : sinon la boucle consommateur peut traiter le
        // 1er déclencheur INLINE (GetAsync synchrone, ex. bloqué sur un gate de test) et faire bloquer
        // StartAsync sur le thread appelant. Pattern recommandé pour un BackgroundService long.
        await Task.Yield();

        CreateWatcher();                       // RAF-01 : surveillance événementielle
        _ = RunPeriodicAsync(stoppingToken);   // RAF-02 : filet de sécurité périodique
        _triggers.Writer.TryWrite(true);       // charge initiale immédiate

        try
        {
            // Consommateur UNIQUE : sérialise les GetAsync (jamais de lecture disque concurrente).
            await foreach (var _ in _triggers.Reader.ReadAllAsync(stoppingToken))
            {
                if (_options.Debounce > TimeSpan.Zero)
                    await Task.Delay(_options.Debounce, stoppingToken); // settle + regroupe les doublons
                var snap = await _provider.GetAsync(stoppingToken);
                SnapshotChanged?.Invoke(this, snap);                    // thread pool → VM marshalle
            }
        }
        catch (OperationCanceledException) { /* arrêt normal */ }
    }

    private async Task RunPeriodicAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(_options.PeriodicInterval);
            while (await timer.WaitForNextTickAsync(ct))
                _triggers.Writer.TryWrite(true); // pas de GetAsync ici : seul le consommateur lit
        }
        catch (OperationCanceledException) { /* arrêt normal */ }
    }

    private void CreateWatcher()
    {
        var dir = Path.GetDirectoryName(_paths.UsageFile)!;
        Directory.CreateDirectory(dir); // le pont crée usage.json, mais le dossier doit exister pour le watcher
        var w = new FileSystemWatcher(dir, "usage.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        // Le pont écrit usage.json ATOMIQUEMENT (renameSync) → l'événement final est Renamed :
        // s'abonner à Changed + Created + Renamed (tous → même déclencheur), sinon écritures ratées (Pitfall 2).
        w.Changed += (_, _) => Trigger();
        w.Created += (_, _) => Trigger();
        w.Renamed += (_, _) => Trigger();
        w.Error += OnError; // buffer overflow best-effort → recréer + rescanner
        _watcher = w;
    }

    private void Trigger() => _triggers.Writer.TryWrite(true);

    /// <summary>Déclenche un recalcul immédiat — ex. après calibration manuelle des plafonds (CAL-01) :
    /// le prochain GetAsync relit les settings frais et recolore les arcs sans redémarrage. Type neutre
    /// (void) → garde de pureté inchangée.</summary>
    public void RequestRefresh() => _triggers.Writer.TryWrite(true);

    private void OnError(object? sender, ErrorEventArgs e) => RecreateWatcher();

    // --- Seams de test internes (visibles via InternalsVisibleTo). Types neutres uniquement
    //     (bool, void) → la garde de pureté WPF reste verte. ---

    /// <summary>Écrit un déclencheur dans le channel (seam déterministe pour les tests, sans dépendre
    /// du timing réel du FileSystemWatcher). Retourne false si un rafraîchissement est déjà en file (DropWrite).</summary>
    internal bool TryTrigger() => _triggers.Writer.TryWrite(true);

    /// <summary>Corps du handler Error : dispose l'ancien watcher, en recrée un, et force un rescan.
    /// Exposé aux tests pour prouver la recréation sans provoquer un vrai buffer overflow.</summary>
    internal void RecreateWatcher()
    {
        _watcher?.Dispose();
        CreateWatcher();
        _triggers.Writer.TryWrite(true); // rescan après recréation (événements potentiellement perdus)
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        _watcher?.Dispose();      // disposal propre (décision verrouillée)
        await base.StopAsync(ct); // signale stoppingToken → la boucle et le PeriodicTimer sortent
    }
}
