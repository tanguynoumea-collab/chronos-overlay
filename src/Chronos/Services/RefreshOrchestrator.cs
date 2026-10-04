using System.Threading.Channels;
using Chronos.Models;
using Microsoft.Extensions.Hosting;

namespace Chronos.Services;

/// <summary>
/// Horloge DONNÉES (RAF-01 + RAF-02). BackgroundService NEUTRE (aucun type WPF → reste hors
/// allow-list de ServicesLayerPurityTests). Il possède un PeriodicTimer configurable qui se contente
/// d'écrire un déclencheur dans un Channel(1, DropWrite) — comme <see cref="RequestRefresh"/>. Une boucle consommateur UNIQUE lit le channel et appelle
/// IUsageProvider.GetAsync un à la fois (jamais de lecture concurrente), puis émet SnapshotChanged.
/// Le marshaling vers le thread UI est fait côté ViewModel (plan 04-02), pas ici.
///
/// P-03 (42.3, audit externe DS-ARCH-03) : le dernier snapshot publié est exposé (<see cref="DernierSnapshot"/>,
/// <see cref="AttendrePremierAsync"/>). Le diagnostic le LIT via <see cref="DernierSnapshotPublie"/> au lieu de rappeler la
/// chaîne : le consommateur de la chaîne reste UNIQUE (jamais de seconde sonde, jamais de last-exact.json disputé).
/// </summary>
public sealed class RefreshOrchestrator : BackgroundService
{
    private readonly IUsageProvider _provider;
    private readonly RefreshOptions _options;
    private readonly string? _dossierJournal;   // dossier de chronos.log ; null = filet sans journal (tests)

    // Capacité 1 + DropWrite = coalescence naturelle : si un rafraîchissement est déjà en file,
    // les déclencheurs surnuméraires d'une rafale sont abandonnés (un seul rattrapage suffit).
    private readonly Channel<bool> _triggers =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        { FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>Émis (thread pool) après chaque GetAsync avec le snapshot produit. Le VM (04-02)
    /// s'abonne ICI et marshalle via IUiDispatcher — décision verrouillée « le service expose l'event ».</summary>
    public event EventHandler<UsageSnapshot>? SnapshotChanged;

    // P-03 — dernier snapshot publié (écrit par le consommateur unique, lu par le diagnostic depuis n'importe quel thread).
    private UsageSnapshot? _dernier;
    // Résolu au PREMIER snapshot publié ; continuations asynchrones : un lecteur en attente ne s'exécute jamais dans la boucle.
    private readonly TaskCompletionSource<UsageSnapshot> _premier = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Dernier snapshot publié par la boucle (le même objet que celui passé à <see cref="SnapshotChanged"/>),
    /// ou <c>null</c> tant que la charge initiale n'a rien produit. Lecture seule : n'appelle jamais la chaîne.</summary>
    public UsageSnapshot? DernierSnapshot => Volatile.Read(ref _dernier);

    /// <summary>Rend le dernier snapshot publié ; s'il n'y en a pas encore, attend le premier au plus <paramref name="delai"/>.
    /// Délai écoulé = <c>null</c> (« pas encore de relevé publié »), jamais une exception ni un chiffre inventé.
    /// N'appelle JAMAIS la chaîne.</summary>
    public async Task<UsageSnapshot?> AttendrePremierAsync(TimeSpan delai, CancellationToken ct = default)
    {
        if (DernierSnapshot is { } deja) return deja;
        try { return await _premier.Task.WaitAsync(delai, ct).ConfigureAwait(false); }
        catch (TimeoutException) { return null; }
    }

    public RefreshOrchestrator(IUsageProvider provider, RefreshOptions options, string? dossierJournal = null)
        => (_provider, _options, _dossierJournal) = (provider, options, dossierJournal);

    /// <summary>
    /// La boucle part EXPLICITEMENT sur le pool (<see cref="Task.Run(Func{Task})"/>), jamais sur le contexte de
    /// l'appelant. StartAsync est appelé par App.OnStartup SUR LE THREAD UI : l'ancien « Task.Yield » rendait bien
    /// la main, mais sa reprise revenait au Dispatcher, et toute la boucle vivait ensuite sur le thread UI. À la
    /// fermeture, OnExit bloquait ce même thread sur l'arrêt : la boucle ne pouvait plus sortir, la reprise de
    /// StopAsync non plus — processus zombie (quick 260927). Task.Run rend aussi la main à StartAsync
    /// immédiatement : le 1er déclencheur n'est jamais traité INLINE sur le thread appelant.
    /// </summary>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => BoucleAsync(stoppingToken), CancellationToken.None);

    private async Task BoucleAsync(CancellationToken stoppingToken)
    {
        _ = RunPeriodicAsync(stoppingToken);   // RAF-02 : cadence périodique
        _triggers.Writer.TryWrite(true);       // charge initiale immédiate

        try
        {
            // Consommateur UNIQUE : sérialise les GetAsync (jamais de lecture disque concurrente).
            // ConfigureAwait(false) partout : aucune reprise ne doit jamais attendre un contexte capturé.
            await foreach (var _ in _triggers.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                if (_options.Debounce > TimeSpan.Zero)
                    await Task.Delay(_options.Debounce, stoppingToken).ConfigureAwait(false); // regroupe les déclencheurs rapprochés
                var snap = await _provider.GetAsync(stoppingToken).ConfigureAwait(false);
                Volatile.Write(ref _dernier, snap);   // P-03 : publié AVANT l'event — un abonné qui relit DernierSnapshot voit ce snapshot
                _premier.TrySetResult(snap);
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
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                _triggers.Writer.TryWrite(true); // pas de GetAsync ici : seul le consommateur lit
        }
        catch (OperationCanceledException) { /* arrêt normal */ }
    }

    /// <summary>Déclenche un recalcul immédiat — ex. après bascule de la source exacte (portillon OAuth)
    /// ou après un login : le prochain GetAsync relit les réglages frais sans redémarrage. Type neutre
    /// (void) → garde de pureté inchangée.</summary>
    public void RequestRefresh() => _triggers.Writer.TryWrite(true);

    // --- Seams de test internes (visibles via InternalsVisibleTo). Types neutres uniquement
    //     (bool, void) → la garde de pureté WPF reste verte. ---

    /// <summary>Écrit un déclencheur dans le channel (seam déterministe pour les tests, sans dépendre
    /// du timing réel du minuteur). Retourne false si un rafraîchissement est déjà en file (DropWrite).</summary>
    internal bool TryTrigger() => _triggers.Writer.TryWrite(true);

    public override async Task StopAsync(CancellationToken ct)
    {
        // ConfigureAwait(false) : la reprise ne doit JAMAIS attendre le thread appelant — si c'est le thread UI et
        // qu'il est bloqué sur l'arrêt, c'était l'interblocage de « Quitter Chronos » (quick 260927).
        await base.StopAsync(ct).ConfigureAwait(false); // signale stoppingToken → la boucle et le PeriodicTimer sortent
    }
}
