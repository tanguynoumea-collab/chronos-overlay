# Phase 4 : Orchestration refresh + ViewModel temps réel — Research

**Researched:** 2026-07-08
**Domain:** Orchestration de rafraîchissement WPF/.NET (FileSystemWatcher débouncé + PeriodicTimer), interpolation ViewModel à la seconde, marshaling de thread unique
**Confidence:** HIGH (APIs .NET 8 stables vérifiées Context7 `/dotnet/docs` ; patterns confirmés par le code Phases 1-3 déjà en place)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Architecture des deux horloges (verrouillé — ARCHITECTURE.md)**
- Horloge DONNÉES (thread pool) : FileSystemWatcher débouncé sur les sources (usage.json du pont ; éventuellement dossiers JSONL) + PeriodicTimer configurable en filet de sécurité → relit via `IUsageProvider.GetAsync` → publie un `UsageSnapshot`.
- Horloge UI (DispatcherTimer 1 s) : INTERPOLE le temps restant à partir du dernier snapshot, AUCUN I/O disque à ce tick. Anti-pattern à éviter absolument : relire le fichier à chaque tick UI.
- Frontière de thread : tout passage horloge données → ViewModel passe par `IUiDispatcher` (déjà posé Phase 1). Aucune `InvalidOperationException` possible.

**FileSystemWatcher (verrouillé — PITFALLS.md)**
- Best-effort : buffer overflow silencieux, doublons, fichiers verrouillés → debounce (~300-500 ms) + PeriodicTimer de secours OBLIGATOIRE.
- Gérer l'événement `Error` du watcher (re-création du watcher).
- Surveiller `%APPDATA%\Chronos` (usage.json). La surveillance des JSONL (`~/.claude/projects` récursif) est plus coûteuse : à faire seulement si peu onéreuse, sinon le PeriodicTimer suffit pour le repli.

**ViewModel (verrouillé — conventions)**
- `MainViewModel` : `[ObservableProperty]` pour l'état des deux fenêtres (utilization, provenance, resets_at, fraction de temps restante interpolée, compte à rebours formaté), état DonnéesIndisponibles.
- CommunityToolkit.Mvvm, aucun code-behind métier.
- L'interpolation à la seconde recalcule `FractionRemaining(now)` à partir du snapshot (les modèles Phase 3 prennent `now` en paramètre — conçu pour ça).
- Formatage des comptes à rebours en français (ex. « 2 h 05 » / « 3 j 14 h »), hebdo best-effort.

**Services d'orchestration**
- Un service hôte (IHostedService ou service démarré explicitement) possède watcher + PeriodicTimer, expose l'événement `SnapshotChanged` — c'est lui qui appelle `GetAsync`, PAS le ViewModel.
- Intervalle du PeriodicTimer configurable (défaut raisonnable : 60 s), lu depuis la config (pré-câbler une valeur, la persistance settings.json arrive en Phase 6).
- Disposal propre (CancellationToken, await du timer) à l'arrêt du host — pattern OnExit Phase 1.

### Claude's Discretion
Détails du debounce, structure interne du service d'orchestration, format exact des chaînes de compte à rebours, tests.

### Deferred Ideas (OUT OF SCOPE)
None — discuss phase skipped.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| RAF-01 | Un FileSystemWatcher débouncé déclenche la relecture sur écriture des sources | Pattern 1 (orchestrateur BackgroundService), Pattern 3 (debounce/coalescence par Channel), Pitfall 2 (watcher best-effort + événement `Error` + écriture atomique `Renamed`) |
| RAF-02 | Un PeriodicTimer relit les données à intervalle configurable (filet de sécurité) | Pattern 1 (boucle `PeriodicTimer.WaitForNextTickAsync`), config `RefreshOptions` injectée (§ Config d'intervalle) |
| RAF-03 | Un DispatcherTimer 1 s interpole arcs et compte à rebours à partir du dernier snapshot, sans I/O | Pattern 4 (séparation interpolation pure `Interpolate(now)` / DispatcherTimer), Pattern 5 (formatage FR), Pitfall 1 (jamais d'I/O au tick) |
| RAF-04 | Tout franchissement thread pool → UI passe par un point de marshaling unique (IUiDispatcher) | Pattern 4 (abonnement unique → `IUiDispatcher.Post`), `WpfUiDispatcher` déjà en place, FakeUiDispatcher pour test |
</phase_requirements>

## Summary

Cette phase transforme un pipeline de données *tiré à la demande* (Phases 3, `IUsageProvider.GetAsync`) en un flux *poussé en continu*, sans jamais franchir un thread WPF de façon non contrôlée. Tout le code neuf tient dans **trois briques** : (1) un `RefreshOrchestrator` neutre (couche Services, zéro WPF) qui possède le `FileSystemWatcher` + le `PeriodicTimer` et appelle `GetAsync` ; (2) un `MainViewModel` enrichi qui s'abonne à l'événement de l'orchestrateur, marshalle via `IUiDispatcher`, et expose deux sous-VM de fenêtre (5 h / hebdo) ; (3) une logique d'**interpolation pure** `Interpolate(now)` que pilote un `DispatcherTimer` 1 s sans aucun I/O.

Les fondations sont déjà là et testées : `IUiDispatcher`/`WpfUiDispatcher` (point de marshaling unique — RAF-04 déjà « câblable »), `IClock`/`FakeClock`, `CompositeUsageProvider` (fire déjà `SnapshotChanged` en fin de `GetAsync`), `WindowState.FractionRemaining(resetsAt, now, len)` (pure, prend `now` — conçue pour l'interpolation). Le seul vrai travail d'architecture est la **conception de l'orchestrateur** (ordonnancement, debounce, gestion `Error`, disposal) et l'**ordre de démarrage** du host (piège n°1 : le host démarre avant que le VM soit abonné).

**Primary recommendation :** `RefreshOrchestrator` en `BackgroundService` (neutre, couche Services), consommateur unique alimenté par un `Channel<bool>` (capacité 1, DropWrite) que nourrissent le watcher débouncé ET le `PeriodicTimer` ; le VM s'abonne à l'événement `SnapshotChanged` de l'orchestrateur et est **résolu AVANT `_host.StartAsync()`** pour ne pas rater le snapshot initial ; interpolation via `Interpolate(now)` pur piloté par un `DispatcherTimer` créé côté UI (jamais dans le ctor du VM, pour garder les tests en `[Fact]` simple).

## Standard Stack

Aucune nouvelle dépendance NuGet n'est nécessaire. Tout est dans le BCL .NET 8 + les paquets déjà référencés.

### Core (déjà référencé)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| CommunityToolkit.Mvvm | 8.4.2 (pinné) | `[ObservableProperty]`/`ObservableObject` sur `MainViewModel` et sous-VM de fenêtre | Convention projet imposée (CLAUDE.md) ; génère `INotifyPropertyChanged` |
| Microsoft.Extensions.Hosting | 8.0.1 (pinné) | `BackgroundService`/`IHostedService`, cycle de vie Start/Stop du host | Déjà utilisé dans `App.xaml.cs` ; gère Start→Stop→Dispose automatiquement |
| System.IO.FileSystemWatcher | BCL net8.0 | Horloge données événementielle (RAF-01) | API standard de surveillance de fichiers |
| System.Threading.PeriodicTimer | BCL net8.0 | Filet de sécurité périodique (RAF-02) | Timer async moderne, `WaitForNextTickAsync(ct)` — pas de callback re-entrant |
| System.Threading.Channels | BCL net8.0 | Coalescence des déclencheurs + consommateur unique (sérialise les `GetAsync`) | Idiomatique pour producteur(s)→consommateur unique |
| System.Windows.Threading.DispatcherTimer | WPF net8.0 | Horloge UI 1 s (RAF-03) — tick sur le thread UI, pas de marshaling | Idiomatique WPF ; **déjà utilisé Phase 1** (`TopmostGuard`) |

### Supporting (déjà en place — à réutiliser, ne pas recréer)
| Asset | Purpose | When to Use |
|-------|---------|-------------|
| `IUiDispatcher` / `WpfUiDispatcher` | Point de marshaling unique thread pool → UI (RAF-04) | À l'unique abonnement `SnapshotChanged` du VM |
| `IClock` / `SystemClock` / `FakeClock` | Horloge injectable | Interpolation (`Interpolate(clock.UtcNow)`) + tests déterministes |
| `IUsageProvider` (→ `CompositeUsageProvider`) | `GetAsync()` + `SnapshotChanged` | Appelé par l'orchestrateur, jamais par le VM |
| `WindowState.FractionRemaining(resetsAt, now, len)` | Fraction [0..1] pure prenant `now` | Cœur de l'interpolation à la seconde |
| `ChronosPaths` | `UsageFile` (%APPDATA%\Chronos\usage.json), `ProjectsRoot` | Chemin surveillé par le watcher |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `Channel<bool>` (coalescence) | `SemaphoreSlim(1,1)` autour de `RefreshAsync` | Sémaphore sérialise mais ne coalesce pas les rafales aussi proprement ; Channel DropWrite = coalescence + sérialisation en une brique |
| `BackgroundService` (IHostedService) | Singleton neutre + `.Start()` manuel après `window.Show()` | Le `.Start()` manuel évite le piège d'ordre de démarrage, mais perd le Stop/CancellationToken automatique du host. Voir Open Question 1 |
| Record `RefreshOptions` injecté | `IOptions<RefreshOptions>` | `IOptions` impose l'infra `Microsoft.Extensions.Configuration` (absente) ; le record injecté colle au pattern existant `ChronosPaths.Default()` |
| Debounce par `System.Threading.Timer` reset | Delay dans la boucle consommateur | Les deux marchent ; le delay dans la boucle est plus testable (délai injectable) et évite un timer/callback re-entrant supplémentaire |

**Installation :** aucune. Vérifié : `Chronos.csproj` référence déjà `CommunityToolkit.Mvvm 8.4.2` et `Microsoft.Extensions.Hosting 8.0.1` ; le reste est BCL/WPF net8.0-windows.

## Architecture Patterns

### Structure de fichiers (nouveaux fichiers en gras)
```
src/Chronos/
├── Services/
│   ├── RefreshOrchestrator.cs      ← NOUVEAU : BackgroundService neutre (watcher + PeriodicTimer + Channel)
│   ├── RefreshOptions.cs           ← NOUVEAU : record d'options (intervalle, debounce), neutre
│   └── (IUiDispatcher, IClock, CompositeUsageProvider… existants)
├── ViewModels/
│   ├── MainViewModel.cs            ← ENRICHI : abonnement + marshaling + tick + état global
│   └── WindowGaugeViewModel.cs     ← NOUVEAU : sous-VM par fenêtre (fraction, util, countdown, provenance)
└── Text/  (ou ViewModels/)
    └── CountdownFormatter.cs       ← NOUVEAU : formatage FR pur d'un TimeSpan (testable, neutre)
tests/Chronos.Tests/
├── RefreshOrchestratorTests.cs     ← NOUVEAU
├── MainViewModelTests.cs           ← NOUVEAU
├── CountdownFormatterTests.cs      ← NOUVEAU
└── Fakes/
    ├── FakeUiDispatcher.cs         ← NOUVEAU
    └── FakeUsageProvider.cs        ← NOUVEAU (records GetAsync + permet de fire SnapshotChanged)
```

### Pattern 1 : `RefreshOrchestrator` en `BackgroundService`, consommateur unique via Channel

**What :** Un `BackgroundService` (donc `IHostedService`) neutre (aucun type WPF → passe `ServicesLayerPurityTests` sans allow-list). Il possède le `FileSystemWatcher`, le `PeriodicTimer`, et un `Channel<bool>` capacité 1 `DropWrite`. Watcher (débouncé) et PeriodicTimer se contentent d'écrire un déclencheur dans le channel ; une **boucle consommateur unique** lit le channel et appelle `GetAsync` un à la fois → aucune lecture disque concurrente, aucun état déchiré. Il expose `event EventHandler<UsageSnapshot>? SnapshotChanged` (décision verrouillée : « le service hôte expose l'événement »).

**When to use :** C'est le cœur de la phase. Le `Channel(1, DropWrite)` coalesce naturellement les rafales : si un rafraîchissement est déjà en file, les déclencheurs surnuméraires sont abandonnés (un seul rattrapage suffit).

**Trade-offs :** `BackgroundService` donne Start/Stop/CancellationToken gratuits via le host, mais introduit le **piège d'ordre de démarrage** (voir Pitfall 3 + Open Question 1) — résolu en résolvant le VM avant `StartAsync`.

**Example (esquisse — l'implémentation exacte est à la discrétion) :**
```csharp
// Couche Services — AUCUN type WPF (reste neutre, garde de pureté).
public sealed class RefreshOrchestrator : BackgroundService
{
    private readonly IUsageProvider _provider;
    private readonly ChronosPaths _paths;
    private readonly RefreshOptions _options;
    private readonly Channel<bool> _triggers =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
            { FullMode = BoundedChannelFullMode.DropWrite });
    private FileSystemWatcher? _watcher;

    /// <summary>Horloge DONNÉES : émis (thread pool) après chaque GetAsync. Marshaling côté VM.</summary>
    public event EventHandler<UsageSnapshot>? SnapshotChanged;

    public RefreshOrchestrator(IUsageProvider provider, ChronosPaths paths, RefreshOptions options)
        => (_provider, _paths, _options) = (provider, paths, options);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        CreateWatcher();                          // RAF-01
        _ = RunPeriodicAsync(stoppingToken);      // RAF-02 (filet de sécurité)
        _triggers.Writer.TryWrite(true);          // charge initiale immédiate

        try
        {
            await foreach (var _ in _triggers.Reader.ReadAllAsync(stoppingToken))
            {
                if (_options.Debounce > TimeSpan.Zero)
                    await Task.Delay(_options.Debounce, stoppingToken); // settle + coalescence
                var snap = await _provider.GetAsync(stoppingToken);
                SnapshotChanged?.Invoke(this, snap);                    // thread pool → VM marshalle
            }
        }
        catch (OperationCanceledException) { /* arrêt normal */ }
    }

    private async Task RunPeriodicAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_options.PeriodicInterval);
        try { while (await timer.WaitForNextTickAsync(ct)) _triggers.Writer.TryWrite(true); }
        catch (OperationCanceledException) { }
    }

    private void CreateWatcher()
    {
        var dir = Path.GetDirectoryName(_paths.UsageFile)!;
        Directory.CreateDirectory(dir);           // le pont crée usage.json ; le dossier doit exister pour watcher
        var w = new FileSystemWatcher(dir, "usage.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        // Écriture atomique du pont (renameSync) ⇒ traiter Renamed AUSSI (sinon écritures ratées).
        w.Changed += OnChanged; w.Created += OnChanged; w.Renamed += OnChanged;
        w.Error   += OnError;
        _watcher = w;
    }

    private void OnChanged(object? s, FileSystemEventArgs e) => _triggers.Writer.TryWrite(true);

    private void OnError(object? s, ErrorEventArgs e)         // buffer overflow → recréer + rescanner
    {
        _watcher?.Dispose();
        CreateWatcher();
        _triggers.Writer.TryWrite(true);
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        _watcher?.Dispose();                       // disposal propre (décision verrouillée)
        await base.StopAsync(ct);                  // signale stoppingToken → boucle + PeriodicTimer sortent
    }
}
```
> Source pattern `BackgroundService`/`PeriodicTimer` : Context7 `/dotnet/docs` (workers.md, generic-host.md). Le host appelle `ExecuteAsync` au `StartAsync` et `StopAsync` à l'arrêt.

### Pattern 2 : DI + événement — le VM s'abonne à l'ORCHESTRATEUR

**What :** Enregistrer l'orchestrateur comme **une seule instance** servant à la fois de Singleton (pour l'abonnement du VM) et de `IHostedService` (pour le cycle de vie). Le VM prend `RefreshOrchestrator` en dépendance et s'abonne à *son* `SnapshotChanged` (décision verrouillée). Le `SnapshotChanged` interne du `CompositeUsageProvider` devient un détail privé sans abonné côté UI.

```csharp
// App.xaml.cs — ConfigureServices (ajouts)
services.AddSingleton(RefreshOptions.Default);
services.AddSingleton<RefreshOrchestrator>();
services.AddHostedService(sp => sp.GetRequiredService<RefreshOrchestrator>()); // MÊME instance
// MainViewModel prend RefreshOrchestrator + IUiDispatcher + IClock par ctor (déjà Singleton).
```

**Trade-offs :** Alternative (Option A) = VM s'abonne directement à `IUsageProvider.SnapshotChanged` (déjà émis par le composite). Rejeté car la décision verrouillée dit explicitement « le service hôte expose l'événement » ; centraliser l'événement sur l'orchestrateur clarifie la propriété de l'horloge données et découple le VM du composite.

### Pattern 3 : Debounce / coalescence

**What :** Deux mécanismes complémentaires, tous deux configurables via `RefreshOptions` :
1. **Coalescence** (rafales/doublons) : le `Channel(1, DropWrite)` absorbe les événements `Changed`+`Renamed` multiples d'une seule écriture — pas de recalcul redondant.
2. **Settle delay** (`Task.Delay(Debounce)` avant `GetAsync`, ~300 ms) : laisse l'écrivain finir. **Note :** le pont écrit `usage.json` de façon *atomique* (`renameSync`, cf. STATE.md), donc les lectures partielles sont déjà impossibles pour la source primaire ; le debounce sert surtout à regrouper les doublons. Le garder modeste.

**When to use :** Toujours avec FileSystemWatcher (best-effort). Combiner OBLIGATOIREMENT avec le PeriodicTimer (décision verrouillée) — c'est lui la vraie garantie de fraîcheur si le watcher perd un événement.

### Pattern 4 : Marshaling unique + interpolation pure (RAF-03 + RAF-04)

**What :** Le VM franchit la frontière de thread **en un seul point** (l'abonnement) via `IUiDispatcher.Post`. La logique d'interpolation est une **méthode pure `Interpolate(DateTimeOffset now)`** qui ne lit que le dernier `WindowState` mémorisé + l'horloge — **jamais le disque**. Le `DispatcherTimer` 1 s ne fait qu'appeler `Interpolate(_clock.UtcNow)`.

**Décision clé de testabilité :** ne PAS créer le `DispatcherTimer` dans le ctor du VM (sinon les tests exigent un contexte STA/Dispatcher). Le VM expose `Interpolate(now)` (pur, testable en `[Fact]` simple avec `FakeClock`) et une méthode `StartClock()` qui crée le `DispatcherTimer` — appelée seulement depuis l'UI (p. ex. `MainWindow.Loaded` ou App après `Show()`). On teste l'interpolation, pas le `DispatcherTimer` (code du framework).

**Example :**
```csharp
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IUiDispatcher _ui;
    private readonly IClock _clock;

    public WindowGaugeViewModel FiveHour { get; } = new(TimeSpan.FromHours(5));
    public WindowGaugeViewModel SevenDay { get; } = new(TimeSpan.FromDays(7));

    [ObservableProperty] private bool _dataUnavailable;
    [ObservableProperty] private DateTimeOffset? _capturedAt;   // staleness pour l'UI Phase 5
    [ObservableProperty] private bool _isStale;

    public MainViewModel(RefreshOrchestrator orchestrator, IUiDispatcher ui, IClock clock)
    {
        _ui = ui; _clock = clock;
        orchestrator.SnapshotChanged += OnSnapshotChanged;      // callback thread pool
    }

    // FRONTIÈRE DE THREAD — franchie UNE seule fois (RAF-04).
    private void OnSnapshotChanged(object? s, UsageSnapshot snap) => _ui.Post(() => ApplySnapshot(snap));

    internal void ApplySnapshot(UsageSnapshot snap)             // exécuté sur thread UI
    {
        FiveHour.Apply(snap.FiveHour);
        SevenDay.Apply(snap.SevenDay);
        CapturedAt = snap.SourceCapturedAt;
        DataUnavailable = snap.FiveHour.Reliability == SourceReliability.Unavailable
                       && snap.SevenDay.Reliability == SourceReliability.Unavailable;
        Interpolate(_clock.UtcNow);                            // premier rendu immédiat
    }

    // PUR, aucun I/O (RAF-03) — appelé chaque seconde par le DispatcherTimer.
    internal void Interpolate(DateTimeOffset now)
    {
        FiveHour.Interpolate(now);
        SevenDay.Interpolate(now);
        IsStale = CapturedAt is { } c && (now - c) > TimeSpan.FromMinutes(2);
    }

    // Créé côté UI uniquement (jamais dans le ctor → tests en [Fact] simple).
    public void StartClock()
    {
        var timer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Interpolate(_clock.UtcNow);
        timer.Start();
    }
}
```
> `IUiDispatcher.Post` fait `if (CheckAccess()) action(); else BeginInvoke(action);` (déjà implémenté, `WpfUiDispatcher`). Aucun `Dispatcher` en dur dans le VM.

### Pattern 5 : sous-VM de fenêtre + formatage FR

**What :** Un `WindowGaugeViewModel` par fenêtre (le XAML Phase 5 bindera deux `RingArc` sur `FiveHour`/`SevenDay`). Il mémorise le `WindowState` immuable et recalcule fraction + texte à chaque `Interpolate`.

```csharp
public sealed partial class WindowGaugeViewModel : ObservableObject
{
    private readonly TimeSpan _windowLength;
    private WindowState _state;                 // dernier snapshot de cette fenêtre

    [ObservableProperty] private double _fractionRemaining;   // 0..1 → longueur d'arc
    [ObservableProperty] private double? _utilization;        // 0..1 ou null → couleur (Phase 5)
    [ObservableProperty] private string _countdownText = "—";
    [ObservableProperty] private bool _exhausted;
    [ObservableProperty] private SourceReliability _reliability = SourceReliability.Unavailable;
    [ObservableProperty] private bool _isEstimated;           // provenance → marquage « estimé » (DAT-08 Phase 5)

    public WindowGaugeViewModel(TimeSpan windowLength) { _windowLength = windowLength; _state = WindowState.Unavailable(default); }

    public void Apply(WindowState s)
    {
        _state = s;
        Utilization = s.Utilization; Exhausted = s.Exhausted; Reliability = s.Reliability;
        IsEstimated = s.Reliability == SourceReliability.Estimated;
    }

    public void Interpolate(DateTimeOffset now)               // PUR, aucun I/O
    {
        FractionRemaining = WindowState.FractionRemaining(_state.ResetsAt, now, _windowLength) ?? 0.0;
        CountdownText = _state.ResetsAt is { } r
            ? CountdownFormatter.Format(r - now)
            : "—";
    }
}
```

**Formatage FR (`CountdownFormatter`, pur & neutre) — exemples attendus :**
| Reste | Sortie | Règle |
|-------|--------|-------|
| 3 j 14 h | `3 j 14 h` | ≥ 1 jour : `{j} j {h} h` |
| 2 h 05 | `2 h 05` | ≥ 1 h : `{h} h {mm}` (minutes sur 2 chiffres) |
| 45 min | `45 min` | < 1 h : `{m} min` |
| ≤ 0 | `0 min` (ou état « épuisé »/reset géré en amont) | temps écoulé |

Le formateur est une fonction pure `string Format(TimeSpan)` → hautement testable, aucune dépendance WPF, pas de `CultureInfo` (littéraux FR fixes). L'hebdo est best-effort (la fenêtre 7 j « dérive » ~72 h — cf. PITFALLS 7) : `resets_at` affiché tel quel s'il existe, sinon `—`.

### Anti-Patterns à éviter (rappel des recherches Phase globale)
- **Relire le fichier au tick 1 s** (Pitfall 3 ARCHITECTURE) : le tick n'appelle QUE `Interpolate(now)`, aucun `GetAsync`.
- **Muter une `[ObservableProperty]` depuis le callback watcher/timer** (Anti-Pattern 1 ARCHITECTURE) : toujours passer par `IUiDispatcher.Post`.
- **Type WPF dans `RefreshOrchestrator`** (Anti-Pattern 4) : l'orchestrateur reste dans `Chronos.Services` SANS être ajouté à l'allow-list WPF ; s'il compile en présence de `ServicesLayerPurityTests`, la pureté est prouvée.
- **`DispatcherTimer` en agression** (specifics CONTEXT) : `AllowsTransparency=True` force le rendu logiciel ; le tick met à jour texte + valeurs (fraction), jamais une animation continue.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Boucle périodique async | `while(true){ await Task.Delay }` + gestion manuelle d'arrêt | `PeriodicTimer.WaitForNextTickAsync(ct)` | Pas de dérive cumulée, annulation propre par token, pas de callback re-entrant |
| Coalescer des rafales d'événements | File + verrous + drapeaux « en cours » maison | `Channel.CreateBounded(1, DropWrite)` | Coalescence + sérialisation producteur→consommateur en une brique testée |
| Marshaling vers l'UI | `Application.Current.Dispatcher.Invoke` dispersé | `IUiDispatcher.Post` (déjà en place) | Point unique (RAF-04), testable via `FakeUiDispatcher`, VM sans dépendance WPF |
| Notifier l'UI d'un changement | `INotifyPropertyChanged` à la main | `[ObservableProperty]` (CommunityToolkit) | Convention projet, source-generator, zéro boilerplate |
| Cycle de vie start/stop du service | Threads + `ManualResetEvent` maison | `BackgroundService` (host) | `ExecuteAsync`/`StopAsync` + `stoppingToken` fournis, disposal géré par le host |
| Calcul de fraction de temps restante | Nouvelle formule dans le VM | `WindowState.FractionRemaining(resetsAt, now, len)` (Phase 3) | Déjà pure, clampée [0..1], testée — conçue pour prendre `now` |

**Key insight :** presque tout le « plomberie » de cette phase existe déjà en BCL ou dans le code Phases 1-3. Le seul code réellement neuf et spécifique est l'**assemblage** (orchestrateur) et l'**adaptation présentation** (sous-VM + formateur FR). Résister à réimplémenter timers, debounce ou marshaling à la main.

## Common Pitfalls

### Pitfall 1 : Relire le disque au tick UI (RAF-03 violé)
**What goes wrong :** tenter d'« avoir des chiffres à jour » en appelant `GetAsync` dans le `DispatcherTimer.Tick`.
**Why :** confusion entre « rafraîchir la donnée » (horloge données) et « animer l'affichage » (horloge UI). I/O sur le thread UI chaque seconde → saccades sous `AllowsTransparency` (rendu logiciel).
**How to avoid :** le tick n'appelle QUE `Interpolate(now)` (pur). Vérification : test asserte que `GetAsync` n'est PAS appelé pendant N ticks (compteur du fake provider = 0).
**Warning signs :** `IUsageProvider` injecté dans le VM ; appel `await` dans un handler `Tick`.

### Pitfall 2 : Watcher qui rate l'écriture atomique du pont (Renamed) ou déborde (Error)
**What goes wrong :** (a) n'écouter que `Changed` → le pont écrit via `renameSync` (temp → usage.json), donc l'événement final est `Renamed`, raté ; (b) rafale JSONL/écritures → buffer overflow silencieux (`Error`), événements perdus.
**Why :** FileSystemWatcher est best-effort (PITFALLS 6). `NotifyFilters` incomplet ou handler `Error` absent.
**How to avoid :** `NotifyFilter = FileName | LastWrite`, s'abonner à `Changed` + `Created` + `Renamed` (tous → même déclencheur) ; handler `Error` → dispose + recrée le watcher + force un rescan ; **PeriodicTimer de secours obligatoire** comme garantie de fraîcheur.
**Warning signs :** l'UI ne bouge pas quand `usage.json` est réécrit ; aucun handler `Error`.

### Pitfall 3 : Le host démarre avant que le VM soit abonné → snapshot initial perdu
**What goes wrong :** `await _host.StartAsync()` lance `ExecuteAsync` (charge initiale → `SnapshotChanged`) AVANT que `MainViewModel` (résolu avec `MainWindow`, plus tard) ne se soit abonné. Le premier snapshot part dans le vide → overlay vide au lancement jusqu'au prochain tick périodique (jusqu'à 60 s).
**Why :** ordre de résolution DI : le VM est construit à la résolution de `MainWindow`, actuellement APRÈS `StartAsync` dans `App.xaml.cs`.
**How to avoid :** résoudre `MainViewModel` (Singleton) AVANT `_host.StartAsync()` pour forcer l'abonnement :
```csharp
_host = builder.Build();
_ = _host.Services.GetRequiredService<MainViewModel>();  // force l'abonnement à SnapshotChanged
await _host.StartAsync();                                // charge initiale → atteint le VM (marshalé)
var window = _host.Services.GetRequiredService<MainWindow>();
window.Show();
```
Le `Post` du snapshot initial est mis en file via `BeginInvoke` et s'appliquera dès que le Dispatcher tourne (après `OnStartup`) — aucune `InvalidOperationException`.
**Warning signs :** overlay vide au démarrage qui « se remplit » seulement après une écriture ou ~1 min.

### Pitfall 4 : `DispatcherTimer` dans le ctor du VM → tests exigent STA
**What goes wrong :** `new DispatcherTimer()` dans le constructeur oblige tous les tests du VM à `[WpfFact]` (STA + Dispatcher), et crée un Dispatcher parasite sur le thread de test.
**How to avoid :** créer le timer dans `StartClock()` appelé côté UI ; exposer `Interpolate(now)` pour les tests. Les tests du VM (marshaling + interpolation) restent en `[Fact]` simple avec `FakeUiDispatcher` + `FakeClock`.
**Warning signs :** `MainViewModelTests` en `[WpfFact]` ; échec « thread must be STA ».

### Pitfall 5 : `GetAsync` concurrents (watcher + PeriodicTimer simultanés)
**What goes wrong :** un événement watcher et un tick périodique déclenchent deux `GetAsync` en parallèle → deux lectures disque concurrentes, événements `SnapshotChanged` entrelacés, ordre indéterminé.
**How to avoid :** consommateur unique (la boucle `ReadAllAsync` du Channel sérialise) — un seul `GetAsync` à la fois. Ne PAS appeler `GetAsync` directement dans les handlers.
**Warning signs :** deux snapshots quasi simultanés ; état qui « clignote ».

## Code Examples

### `RefreshOptions` (config d'intervalle — pré-câblée, settings.json en Phase 6)
```csharp
namespace Chronos.Services;

/// <summary>Réglages des horloges données. Injecté en Singleton (pattern ChronosPaths).
/// La persistance settings.json arrive en Phase 6 ; ici, valeurs par défaut pré-câblées.</summary>
public sealed record RefreshOptions(TimeSpan PeriodicInterval, TimeSpan Debounce)
{
    public static RefreshOptions Default => new(
        PeriodicInterval: TimeSpan.FromSeconds(60),   // filet de sécurité (RAF-02)
        Debounce:         TimeSpan.FromMilliseconds(300)); // coalescence/settle (RAF-01)
}
```

### `FakeUiDispatcher` (test du marshaling — RAF-04)
```csharp
internal sealed class FakeUiDispatcher : IUiDispatcher
{
    public int PostCount { get; private set; }
    public bool OnUiThread { get; set; }               // simule le thread courant
    public bool CheckAccess() => OnUiThread;
    public void Post(Action a) { PostCount++; a(); }    // exécute inline pour l'assertion
}
```

### `FakeUsageProvider` (test orchestrateur + « pas d'I/O au tick »)
```csharp
internal sealed class FakeUsageProvider : IUsageProvider
{
    public int GetCount;
    public UsageSnapshot Next = UsageSnapshot.Empty;
    public event EventHandler<UsageSnapshot>? SnapshotChanged;
    public Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref GetCount);
        SnapshotChanged?.Invoke(this, Next);
        return Task.FromResult(Next);
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `System.Timers.Timer` / `System.Threading.Timer` + callback | `PeriodicTimer.WaitForNextTickAsync(ct)` | .NET 6+ | Async natif, pas de re-entrance, annulation par token — préféré pour une boucle |
| `BlockingCollection` / `Queue` + `lock` | `System.Threading.Channels` | .NET Core 3+ | Producteur(s)→consommateur async, coalescence via bornage |
| `INotifyPropertyChanged` manuel | `[ObservableProperty]` (CommunityToolkit source-gen) | Toolkit 8.x | Zéro boilerplate (déjà adopté projet) |

**Rien de déprécié à signaler** dans le périmètre : `FileSystemWatcher`, `DispatcherTimer`, `BackgroundService` restent les APIs standard net8.0-windows.

## Open Questions

1. **`BackgroundService` (IHostedService) vs Singleton démarré manuellement ?**
   - Ce qu'on sait : le host démarre avant `MainWindow` (Pitfall 3). `BackgroundService` donne Start/Stop/token gratuits ; le Singleton `.Start()` après `window.Show()` (comme l'esquisse ARCHITECTURE Pattern 1) évite l'ordre de démarrage mais gère l'arrêt/token à la main.
   - Ce qui est flou : préférence entre « idiomatique host » vs « ordre trivial ».
   - Recommandation : **`BackgroundService` + pré-résolution du VM avant `StartAsync`** (Pitfall 3). Bénéfice : `StopAsync`/`stoppingToken`/disposal automatiques collent à la décision verrouillée « Disposal propre (CancellationToken, await du timer) ». Coût : une ligne de pré-résolution dans `OnStartup`.

2. **Surveiller aussi `~/.claude/projects` (JSONL récursif) ?**
   - Ce qu'on sait : la source primaire (`usage.json`, Exact) est ce qui s'affiche quand elle est disponible (cas courant) ; le JSONL n'est que le repli Estimé. Le watch récursif sur `.claude/projects` est coûteux (gros volume, forte churn, risque d'overflow — PITFALLS 6 & Performance Traps).
   - Recommandation : **NON — surveiller uniquement `%APPDATA%\Chronos` (usage.json)**. Le `PeriodicTimer` (60 s) suffit largement pour rafraîchir le repli JSONL : une estimation n'a pas besoin de fraîcheur sub-seconde, et elle n'est même pas affichée quand le primaire répond. Cela évite tout le coût du watch récursif. (Aligné sur la décision verrouillée « sinon le PeriodicTimer suffit pour le repli ».)

3. **Court-circuit paresseux du composite ?**
   - Ce qu'on sait : `CompositeUsageProvider.GetAsync` appelle actuellement TOUJOURS primaire ET repli (JSONL) même si le primaire est complet — un commentaire du code note ce raffinement possible.
   - Recommandation : **hors périmètre Phase 4**, mais à surveiller : avec la surveillance périodique, le JSONL est scanné toutes les 60 s même quand `usage.json` est fiable. Si le coût devient sensible (gros transcripts), ajouter le court-circuit paresseux (n'appeler le repli que si une fenêtre primaire est Unavailable). À noter dans le plan comme amélioration optionnelle, pas un blocage RAF-01..04.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build/test net8.0-windows | ✓ | 10.0.201 (SDK ; cible net8.0-windows) | — |
| xUnit + Xunit.StaFact | Tests (`[Fact]`/`[WpfFact]`) | ✓ | xunit 2.9.2 / StaFact 1.1.11 | — |
| CommunityToolkit.Mvvm | ViewModels | ✓ | 8.4.2 (pinné csproj) | — |
| Microsoft.Extensions.Hosting | BackgroundService | ✓ | 8.0.1 (pinné csproj) | — |
| Pont statusLine (`usage.json`) | Source réelle du watcher | ✓ (déployé Phase 3, `install-bridge.mjs`) | — | Tests via fichiers temp + `FakeUsageProvider` (pas de dépendance au pont) |

Aucune dépendance manquante. Tout le travail de phase se teste sans le pont réel (fakes + fichiers temporaires).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]` pour code STA/Dispatcher) |
| Config file | none (SDK-style, aucun `xunit.runner.json` requis) |
| Quick run command | `dotnet test tests/Chronos.Tests --filter "FullyQualifiedName~RefreshOrchestrator|FullyQualifiedName~MainViewModel|FullyQualifiedName~CountdownFormatter"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| RAF-01 | Rafale de signaux `Changed`/`Renamed` → un seul `GetAsync` (coalescence) | unit | `dotnet test --filter "FullyQualifiedName~RefreshOrchestratorTests"` | ❌ Wave 0 |
| RAF-01 | Événement `Error` → watcher recréé + rescan (compteur `GetAsync` incrémenté) | unit | idem | ❌ Wave 0 |
| RAF-02 | `PeriodicTimer` déclenche `GetAsync` sans aucun événement watcher (filet) | unit | idem (intervalle court injecté via `RefreshOptions`) | ❌ Wave 0 |
| RAF-03 | `Interpolate(now)` avançant `FakeClock` fait décroître `FractionRemaining`/`CountdownText` SANS appeler `GetAsync` (`FakeUsageProvider.GetCount == 0` pendant les ticks) | unit `[Fact]` | `dotnet test --filter "FullyQualifiedName~MainViewModelTests"` | ❌ Wave 0 |
| RAF-04 | `SnapshotChanged` émis hors UI → appliqué via `IUiDispatcher.Post` exactement une fois (`FakeUiDispatcher.PostCount == 1`), propriétés à jour | unit `[Fact]` | idem | ❌ Wave 0 |
| RAF-03 | Formatage FR : `2 h 05`, `3 j 14 h`, `45 min`, `0 min` | unit `[Fact]` | `dotnet test --filter "FullyQualifiedName~CountdownFormatterTests"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit :** `dotnet test tests/Chronos.Tests --filter "FullyQualifiedName~RefreshOrchestrator|FullyQualifiedName~MainViewModel|FullyQualifiedName~CountdownFormatter"`
- **Per wave merge :** `dotnet test` (suite complète — les 27+ tests Phases 1-3 doivent rester verts, dont `ServicesLayerPurityTests` qui garde la neutralité de `RefreshOrchestrator`)
- **Phase gate :** suite complète verte avant `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/Fakes/FakeUiDispatcher.cs` — marshaling (RAF-04)
- [ ] `tests/Chronos.Tests/Fakes/FakeUsageProvider.cs` — compteur `GetAsync` + émission `SnapshotChanged` (RAF-01/02/03)
- [ ] `tests/Chronos.Tests/RefreshOrchestratorTests.cs` — RAF-01, RAF-02
- [ ] `tests/Chronos.Tests/MainViewModelTests.cs` — RAF-03, RAF-04
- [ ] `tests/Chronos.Tests/CountdownFormatterTests.cs` — formatage FR
- Framework install : aucun (xUnit + StaFact déjà présents)
- Note : `FakeClock` existe déjà (`tests/Chronos.Tests/Fakes/FakeClock.cs`).

## Sources

### Primary (HIGH confidence)
- Context7 `/dotnet/docs` — `workers.md`, `generic-host.md`, `timer-service.md`, `queue-service.md` : `BackgroundService.ExecuteAsync`/`StopAsync`, ordre `StartAsync`→`ExecuteAsync`, `PeriodicTimer`, disposal. Le host appelle `ExecuteAsync` au `StartAsync`.
- Code source du dépôt (lu directement) : `App.xaml.cs` (host + ordre de démarrage), `IUiDispatcher`/`WpfUiDispatcher`, `IClock`/`FakeClock`, `CompositeUsageProvider` (émet `SnapshotChanged` dans `GetAsync`), `WindowState.FractionRemaining` (pure, prend `now`), `ChronosPaths`, `ServicesLayerPurityTests`, `Chronos.Tests.csproj` (xUnit + StaFact).
- `.planning/research/ARCHITECTURE.md` (Patterns 1, 4, 5 ; deux horloges ; Anti-Patterns 1, 3, 4) — **HIGH**.
- `.planning/research/PITFALLS.md` (Pitfalls 4 threading, 6 FileSystemWatcher, 7 honnêteté hebdo) — **HIGH**.

### Secondary (MEDIUM confidence)
- STATE.md — écriture atomique `usage.json` par le pont (`renameSync`), `capturedAt` en epoch ms : justifie le traitement de `Renamed` et l'absence de lectures partielles pour le primaire.

## Metadata

**Confidence breakdown :**
- Standard stack : HIGH — aucune dépendance neuve, APIs BCL/WPF stables + paquets déjà pinnés vérifiés dans le csproj.
- Architecture (orchestrateur, marshaling, interpolation) : HIGH — vérifiée Context7 `/dotnet/docs` + cohérente avec le code Phases 1-3 réellement lu.
- Pitfalls : HIGH — issues des recherches transverses (PITFALLS/ARCHITECTURE) et confirmées par l'ordre de démarrage réel d'`App.xaml.cs`.
- Formatage FR / seuils staleness : MEDIUM — format exact et seuil `IsStale` (2 min) à la discrétion, à confirmer au plan/UI Phase 5.

**Research date :** 2026-07-08
**Valid until :** ~2026-08-07 (stack stable ; ré-évaluer si migration de cible .NET ou ajout d'infra de configuration en Phase 6).
