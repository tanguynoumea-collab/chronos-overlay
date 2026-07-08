# Architecture Research

**Domain:** Overlay desktop WPF/MVVM (always-on-top) lisant des sources locales non documentées, rafraîchissement temps réel, rendu d'arcs vectoriels
**Researched:** 2026-07-08
**Confidence:** HIGH (patterns WPF/MVVM/DI standards, vérifiés Context7 : CommunityToolkit.Mvvm + dotnet/wpf)

## Standard Architecture

Chronos est une application desktop mono-processus, mono-utilisateur. L'architecture pertinente n'est pas « n-tiers scalable » mais **une architecture en couches strictes avec inversion de dépendance sur la source de données**, où le point de rupture attendu (sources non documentées) est isolé derrière `IUsageProvider`, et où le **threading est le risque transverse n°1** (données produites en arrière-plan, UI qui doit rester sur le Dispatcher).

### System Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│  COMPOSITION ROOT — App.xaml.cs (OnStartup)                            │
│  ServiceCollection → build → resolve MainWindow → Show                 │
│  Détient : IServiceProvider, cycle de vie, IDisposable global          │
└───────────────┬───────────────────────────────────────────────────────┘
                │ construit et injecte (une seule fois, au démarrage)
                ▼
┌──────────────────────────────────────────────────────────────────────┐
│  COUCHE PRÉSENTATION (UI Thread — Dispatcher uniquement)               │
│  ┌────────────┐   binding    ┌──────────────┐   binding   ┌─────────┐ │
│  │ MainWindow │◄────────────►│ MainViewModel│────────────►│ RingArc │ │
│  │  (View)    │   commands   │ [Observable] │   valeurs   │(Control)│ │
│  └────────────┘              └──────┬───────┘             └─────────┘ │
│         ▲                           │  s'abonne à SnapshotChanged      │
│         │ UtilizationToBrushConverter, converters                     │
└─────────┼───────────────────────────┼─────────────────────────────────┘
          │                           │  ▲ FRONTIÈRE DE THREAD (Dispatcher.Invoke)
──────────┼───────────────────────────┼──────────────────────────────────
          │                           │
┌─────────┼───────────────────────────┼─────────────────────────────────┐
│  COUCHE SERVICES (Thread pool / async — JAMAIS de type WPF ici)        │
│         │                    ┌───────▼────────────────┐                 │
│         │                    │ CompositeUsageProvider │  (IUsageProvider)│
│         │                    └───┬────────────────┬───┘                 │
│         │            primaire ┌──▼──┐        repli┌▼─────────────────┐   │
│         │  ClaudeUsageObjectProvider          JsonlEstimationProvider│   │
│         │            └──┬───────────┘        └────────┬─────────────┘   │
│  RefreshOrchestrator    │ FileSystemWatcher + PeriodicTimer            │
│  (déclenche GetAsync)   │                             │                 │
└─────────────────────────┼─────────────────────────────┼────────────────┘
                          ▼                             ▼
┌──────────────────────────────────────────────────────────────────────┐
│  SOURCES LOCALES (I/O disque — non documentées)                        │
│  Objet d'usage Claude Code (five_hour/seven_day)   %USERPROFILE%/.claude│
│  Transcripts JSONL (**/*.jsonl)                    %APPDATA%/Chronos    │
└──────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Composant | Responsabilité (ce qu'il possède) | Implémentation type |
|-----------|-----------------------------------|---------------------|
| **App (Composition Root)** | Enregistrer et câbler tout le graphe DI ; posséder le cycle de vie ; disposer proprement | `App.xaml.cs`, override `OnStartup`/`OnExit`, `ServiceCollection` |
| **MainViewModel** | État observable pour la vue ; s'abonner à `SnapshotChanged` ; marshaler vers l'UI thread ; tick 1 s ; commandes (drag, arrière-plan) | `ObservableObject` + `[ObservableProperty]`/`[RelayCommand]` |
| **MainWindow (View)** | XAML pur : cadran, ticks, 2× RingArc, texte central ; comportements overlay | `Window` borderless, code-behind minimal |
| **RingArc** | Rendre un arc paramétré (angle début/fin, épaisseur, couleur) géométriquement correct | Custom `Control` dérivé de `Shape` (voir Pattern 2) |
| **Converters** | Transformer valeur métier → ressource UI (utilization → Brush) | `IValueConverter` |
| **IUsageProvider** | Contrat abstrait : `Task<UsageSnapshot> GetAsync()` + `event SnapshotChanged` | Interface, aucune dépendance WPF |
| **CompositeUsageProvider** | Orchestrer primaire → repli ; propager la meilleure source disponible | Décorateur/composite d'`IUsageProvider` |
| **ClaudeUsageObjectProvider** | Lire l'objet d'usage (utilization/resets_at), `SourceReliability=Exact` | Provider primaire, parsing tolérant |
| **JsonlEstimationProvider** | Estimer par somme de tokens JSONL, `SourceReliability=Estimated` | Provider repli |
| **RefreshOrchestrator** | Piloter *quand* rafraîchir : `FileSystemWatcher` (debounce) + `PeriodicTimer` | Service hébergé, boucle async |
| **UsageSnapshot / WindowState** | Modèle immuable transporté source→VM (Utilization, ResetsAt, Exhausted, FractionTimeRemaining, SourceReliability) | `record` immuable |
| **ISettingsStore** | Persistance position/coin/réglages | JSON dans `%APPDATA%/Chronos` |

**Frontière clé :** la couche Services ne référence **aucun type WPF** (pas de `Dispatcher`, pas de `Brush`). Elle produit des `UsageSnapshot` neutres. C'est le ViewModel — et lui seul — qui franchit la frontière de thread et traduit en état UI.

## Recommended Project Structure

```
Chronos/
├── App.xaml                     # resources globales, pas de StartupUri
├── App.xaml.cs                  # COMPOSITION ROOT : DI, resolve MainWindow
├── Models/                      # POCO/records neutres, zéro dépendance WPF
│   ├── UsageSnapshot.cs         # record immuable (Utilization, ResetsAt, Exhausted,
│   │                            #   FractionTimeRemaining, SourceReliability)
│   ├── WindowState.cs           # état d'une fenêtre (5 h / hebdo)
│   └── SourceReliability.cs     # enum Exact | Estimated | Unavailable
├── Services/                    # logique data + I/O, testable hors UI
│   ├── IUsageProvider.cs        # contrat : GetAsync() + event SnapshotChanged
│   ├── ClaudeUsageObjectProvider.cs
│   ├── JsonlEstimationProvider.cs
│   ├── CompositeUsageProvider.cs
│   ├── RefreshOrchestrator.cs   # FileSystemWatcher + PeriodicTimer
│   ├── IUiDispatcher.cs         # abstraction du marshaling UI (testabilité)
│   └── ISettingsStore.cs / SettingsStore.cs
├── ViewModels/
│   └── MainViewModel.cs         # [ObservableProperty]/[RelayCommand], tick 1 s
├── Views/
│   └── MainWindow.xaml(.cs)     # overlay borderless, cadran, arcs
├── Controls/                    # contrôles réutilisables
│   └── RingArc.cs               # Shape-derived, DependencyProperties
├── Converters/
│   └── UtilizationToBrushConverter.cs
└── docs/
    └── data-sources.md          # DÉCOUVERTE DE SOURCE (à écrire AVANT les providers)
```

### Structure Rationale

- **Models/ sans dépendance WPF :** garantit que le contrat de données reste réutilisable et testable ; empêche par construction qu'un `Brush` fuite dans la couche données.
- **Services/ isole le point de rupture :** si une source Claude change, seul un fichier de `Services/` bouge, jamais `Views/` ni `Controls/`. C'est la raison d'être de `IUsageProvider`.
- **Controls/ séparé de Views/ :** RingArc est un composant réutilisable (2 instances) sans logique métier ; le garder hors de `Views/` clarifie que c'est de la présentation pure paramétrable.
- **docs/data-sources.md est un livrable de phase, pas de la doc a posteriori :** tout le pipeline données en dépend, donc il précède le code des providers.

## Architectural Patterns

### Pattern 1: Composition Root explicite dans App.xaml.cs

**What:** Un unique point où tout le graphe d'objets est enregistré et construit. On supprime `StartupUri` du XAML et on résout `MainWindow` manuellement pour qu'elle reçoive son ViewModel par injection.

**When to use:** Toujours, dès qu'il y a DI. C'est le seul endroit qui « connaît » les implémentations concrètes ; le reste du code ne dépend que d'interfaces.

**Trade-offs:** Un peu de plomberie au démarrage vs découplage total et testabilité. Pour cette app, `ServiceCollection` seul suffit — pas besoin du `Generic Host` complet, mais il reste une option si on veut `IHostedService` pour l'orchestrateur.

**Example:**
```csharp
// App.xaml : retirer StartupUri="MainWindow.xaml"
public partial class App : Application
{
    private ServiceProvider _services = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var sc = new ServiceCollection();

        // Providers : concrets nommés, exposés via l'interface pour le composite
        sc.AddSingleton<ClaudeUsageObjectProvider>();
        sc.AddSingleton<JsonlEstimationProvider>();
        sc.AddSingleton<IUsageProvider>(sp => new CompositeUsageProvider(
            primary: sp.GetRequiredService<ClaudeUsageObjectProvider>(),
            fallback: sp.GetRequiredService<JsonlEstimationProvider>()));

        sc.AddSingleton<RefreshOrchestrator>();
        sc.AddSingleton<ISettingsStore, SettingsStore>();

        // Abstraction du Dispatcher (capturée depuis le thread UI courant)
        sc.AddSingleton<IUiDispatcher>(_ => new WpfUiDispatcher(Dispatcher));

        sc.AddSingleton<MainViewModel>();
        sc.AddSingleton<MainWindow>();

        _services = sc.BuildServiceProvider();

        var window = _services.GetRequiredService<MainWindow>();
        window.DataContext = _services.GetRequiredService<MainViewModel>();
        window.Show();

        _services.GetRequiredService<RefreshOrchestrator>().Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services.Dispose(); // dispose watcher, timers, providers IDisposable
        base.OnExit(e);
    }
}
```
> Providers en **Singleton** : ils détiennent des watchers/timers et un état de dernier snapshot ; un seul par source.

### Pattern 2: RingArc en Control dérivé de `Shape` (pas UserControl)

**What:** Pour un visuel purement géométrique paramétré, dériver de `Shape` et surcharger `DefiningGeometry` pour construire le `PathGeometry` de l'arc à la volée. Les paramètres (angle début/fin, rayon, épaisseur) sont des `DependencyProperty` marquées `AffectsRender`/`AffectsMeasure`.

**When to use:** Contrôle réutilisable dont la sortie est *une géométrie calculée*. C'est le choix idiomatique WPF ici — plus propre qu'un `UserControl`.

**Trade-offs:**
- **Shape-derived (recommandé) :** DPs bindables directement (`StartAngle`, `EndAngle`, `Utilization`), géométrie recalculée automatiquement, épaisseur gérée par `StrokeThickness`, couleur par `Stroke`. Pas de MultiBinding acrobatique. Un seul fichier `.cs`, pas de XAML.
- **UserControl + Path + converters :** oblige à un `MultiBinding`/converter pour composer la géométrie à partir de plusieurs paramètres — verbeux et fragile. À éviter pour de la géométrie paramétrique.
- **Astuce clé :** rendre l'arc comme une **figure ouverte tracée** (`IsClosed=false`, `IsFilled=false`) et donner l'épaisseur via `StrokeThickness` + `StrokeStartLineCap/EndLineCap=Round`. On évite ainsi de construire une géométrie d'anneau (donut) à deux arcs — bien plus simple et net.

**Example:**
```csharp
public sealed class RingArc : Shape
{
    public static readonly DependencyProperty StartAngleProperty =
        DependencyProperty.Register(nameof(StartAngle), typeof(double), typeof(RingArc),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EndAngleProperty =
        DependencyProperty.Register(nameof(EndAngle), typeof(double), typeof(RingArc),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RadiusProperty =
        DependencyProperty.Register(nameof(Radius), typeof(double), typeof(RingArc),
            new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender));

    public double StartAngle { get => (double)GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }
    public double EndAngle   { get => (double)GetValue(EndAngleProperty);   set => SetValue(EndAngleProperty, value); }
    public double Radius     { get => (double)GetValue(RadiusProperty);     set => SetValue(RadiusProperty, value); }

    protected override Geometry DefiningGeometry => BuildArc();
    // Stroke + StrokeThickness hérités de Shape → couleur & épaisseur de l'anneau
}
```
> Couleur = binder `Stroke` sur `UtilizationToBrushConverter` ; longueur d'arc = binder `EndAngle` sur `FractionTimeRemaining`.

### Pattern 3: Arc géométriquement correct (ArcSegment)

**What:** `ArcSegment` en WPF ne stocke que le **point d'arrivée** ; il faut calculer les points de départ/arrivée par trigonométrie et régler correctement `IsLargeArc` et `SweepDirection`, sinon l'arc « saute » de l'autre côté du cercle au-delà de 180°.

**Règles :**
- **Repère WPF :** origine en haut-gauche, **Y vers le bas**. Un angle mesuré horaire depuis le haut (12 h) donne :
  `point = center + R·(sin θ, −cos θ)` (θ en radians). Le `−cos` compense le Y inversé pour que 0° soit à 12 h.
- **IsLargeArc :** `true` si `|EndAngle − StartAngle| > 180`. C'est **l'erreur la plus fréquente** : oublier ce flag rend correctement les arcs < 180° puis casse au-delà.
- **SweepDirection :** `Clockwise` si le balayage est positif (horaire), sinon `Counterclockwise`. Le sens de balayage doit être cohérent avec le calcul des points.
- **Size :** `new Size(R, R)` (cercle). `RotationAngle = 0` pour un arc circulaire.
- **Cas limite :** un balayage de 0° (temps écoulé nul) ou de 360° (pile plein) doit être borné — 360° exactement se dégénère (départ = arrivée) ; clamp à ~359.9° ou traiter le cas « anneau plein » séparément.

**Example:**
```csharp
private Geometry BuildArc()
{
    double sweep = EndAngle - StartAngle;                 // en degrés
    var center = new Point(ActualWidth / 2, ActualHeight / 2);
    Point PointAt(double deg) {
        double r = deg * Math.PI / 180.0;
        return new Point(center.X + Radius * Math.Sin(r),
                         center.Y - Radius * Math.Cos(r));   // Y inversé
    }
    var start = PointAt(StartAngle);
    var end   = PointAt(EndAngle);
    var fig = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
    fig.Segments.Add(new ArcSegment(
        point:          end,
        size:           new Size(Radius, Radius),
        rotationAngle:  0,
        isLargeArc:     Math.Abs(sweep) > 180.0,
        sweepDirection: sweep >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
        isStroked:      true));
    return new PathGeometry(new[] { fig });
}
```

### Pattern 4: Marshaling de thread données → UI (LE point critique)

**What:** Les snapshots sont produits en dehors du thread UI (I/O async, callback `FileSystemWatcher`, boucle `PeriodicTimer`). Toute mise à jour de propriété bindée doit être **repoussée sur le Dispatcher WPF**. WPF interdit d'accéder à un `DispatcherObject` depuis un autre thread que celui qui l'a créé (`VerifyAccess` lève sinon).

**When to use:** Systématiquement à la frontière Services→ViewModel. On centralise ce franchissement en un seul endroit (l'abonnement à `SnapshotChanged`) plutôt que de le disperser.

**Trade-offs:** Passer par une abstraction `IUiDispatcher` (plutôt que `Application.Current.Dispatcher` en dur) rend le ViewModel testable et évite un couplage WPF dans la logique VM.

**Example:**
```csharp
public partial class MainViewModel : ObservableObject
{
    private readonly IUiDispatcher _ui;

    public MainViewModel(IUsageProvider provider, IUiDispatcher ui)
    {
        _ui = ui;
        provider.SnapshotChanged += OnSnapshotChanged; // callback sur thread pool
    }

    private void OnSnapshotChanged(object? s, UsageSnapshot snap)
        => _ui.Post(() => ApplySnapshot(snap)); // franchit la frontière UNE fois

    private void ApplySnapshot(UsageSnapshot snap) // exécuté sur UI thread
    {
        Utilization = snap.Utilization;      // [ObservableProperty] → OK ici
        Exhausted   = snap.Exhausted;
        // ...
    }
}

// Implémentation : if (_dispatcher.CheckAccess()) action(); else _dispatcher.BeginInvoke(action);
```
> `CheckAccess()` évite un re-post inutile si on est déjà sur le thread UI (vérifié Context7 : pattern `HwndHost.DestroyWindow`).

### Pattern 5: Debounce du FileSystemWatcher

**What:** `FileSystemWatcher` émet **plusieurs événements pour une seule écriture** (Created + Changed multiples), et le fichier peut être verrouillé/partiel au moment du callback. On coalesce les rafales avec un court délai avant de déclencher `GetAsync`.

**When to use:** Toujours avec FileSystemWatcher sur des fichiers écrits par un autre process (ici Claude Code écrivant ses JSONL/objet d'usage).

**Trade-offs:** ~100-300 ms de latence ajoutée vs suppression des lectures redondantes et des `IOException` sur fichier verrouillé. Combiner avec un retry court sur `IOException`.

## Data Flow

### Flux principal (source → arc)

```
[Claude Code écrit un fichier]
        ↓  (thread OS)
FileSystemWatcher.Changed  ──debounce~200ms──►  RefreshOrchestrator
        ↓                                              (thread pool)
CompositeUsageProvider.GetAsync()
    ├─ ClaudeUsageObjectProvider  → réussit ? → UsageSnapshot{Exact}
    └─ sinon JsonlEstimationProvider → UsageSnapshot{Estimated}
        ↓  provider émet
event SnapshotChanged(UsageSnapshot)             (thread pool)
        ↓
        ═══════════ FRONTIÈRE DE THREAD : IUiDispatcher.Post ═══════════
        ↓
MainViewModel.ApplySnapshot()                    (UI thread / Dispatcher)
    → Utilization, ResetsAt, Exhausted, SourceReliability  ([ObservableProperty])
        ↓  INotifyPropertyChanged
Bindings XAML
    ├─ RingArc.EndAngle   ◄── FractionTimeRemaining
    ├─ RingArc.Stroke     ◄── UtilizationToBrushConverter(Utilization)
    └─ TextBlock          ◄── compte à rebours
        ↓
RingArc.DefiningGeometry recalculé → arc redessiné
```

### Flux du tick UI (fluidité seconde par seconde)

```
DispatcherTimer 1 s (UI thread)
    ↓  ne relit AUCUN fichier — recalcule à partir du dernier snapshot + horloge
MainViewModel.Tick()
    → FractionTimeRemaining = (ResetsAt − now) / windowLength
    → texte compte à rebours
    ↓  binding → RingArc.EndAngle (longueur d'arc décroît en douceur)
```

### Deux horloges distinctes — ne pas confondre

1. **Horloge données** (`FileSystemWatcher` + `PeriodicTimer`, thread pool) : *quand relire les fichiers*. Peu fréquente, coûteuse (I/O), franchit la frontière de thread.
2. **Horloge UI** (`DispatcherTimer` 1 s, UI thread) : *interpoler le temps restant* entre deux snapshots. Fréquente, gratuite, aucun I/O. C'est elle qui rend l'arc « vivant » sans marteler le disque.

## Build Order (dépendances)

L'ordre suit strictement les dépendances : rien ne peut se construire avant ce dont il a besoin.

| # | Bloc | Livrable | Débloque | Pourquoi cet ordre |
|---|------|----------|----------|--------------------|
| 1 | **Fondations arch** | Projet net8, arborescence Models/Views/ViewModels/Services, DI dans App.xaml.cs, MainWindow overlay vide (borderless/topmost) | Tout | Sans composition root ni squelette, rien ne se câble |
| 2 | **Découverte de source** | `docs/data-sources.md` : où/comment lire l'objet d'usage et les JSONL | Providers | **Tout le pipeline données en dépend** ; coder un provider avant de connaître la source, c'est deviner |
| 3 | **Modèles** | `UsageSnapshot`, `WindowState`, `SourceReliability` (records immuables) | Providers + VM | Contrat neutre partagé, doit exister avant producteurs/consommateurs |
| 4 | **Providers** | `IUsageProvider`, puis `JsonlEstimationProvider` (repli, le plus testable), puis `ClaudeUsageObjectProvider`, puis `CompositeUsageProvider` | Orchestrateur + VM | Construire le repli d'abord donne un chemin vérifiable de bout en bout tôt |
| 5 | **Orchestration refresh** | `RefreshOrchestrator` (FileSystemWatcher debounce + PeriodicTimer), `IUiDispatcher` | VM temps réel | Fournit le flux d'événements que le VM consomme |
| 6 | **ViewModel** | `MainViewModel` : abonnement SnapshotChanged, marshaling UI, tick 1 s | UI binding | Nécessite providers + dispatcher ; joignable en isolation via IUiDispatcher fake |
| 7 | **RingArc + converters** | Control `RingArc`, `UtilizationToBrushConverter` | Rendu final | Présentation pure ; dépend seulement des valeurs exposées par le VM |
| 8 | **Câblage View** | Bindings XAML MainWindow ↔ VM ↔ RingArc, cadran/ticks | App visible | Assemble les briques précédentes |
| 9 | **Comportements overlay** | Drag + snap coin, bascule Topmost, persistance settings | — | Fonctionnalités périphériques, indépendantes du cœur données |
| 10 | **Packaging** | PublishSingleFile win-x64 self-contained, autostart shell:startup | Livraison | En dernier, une fois le comportement stable |

**Principe transverse :** on peut valider le pipeline données (blocs 2→6) *sans UI finale* grâce aux modèles neutres et à `IUiDispatcher` — le rendu (7-8) vient se brancher sur un flux déjà éprouvé.

## Responsiveness / Resource Considerations

Application mono-utilisateur : pas de « scaling ». Les seuls enjeux sont la réactivité et l'empreinte.

| Enjeu | Risque | Mitigation |
|-------|--------|------------|
| Réactivité UI | Parsing JSONL volumineux bloque le rendu | Tout le parsing en `async`/thread pool ; l'UI ne touche jamais le disque |
| Rafales FileSystemWatcher | Lectures redondantes, IOException fichier verrouillé | Debounce + retry court sur IOException |
| Fuite mémoire | Abonnement `SnapshotChanged` jamais détaché, timers non disposés | Providers/orchestrateur `IDisposable`, disposés dans `App.OnExit` |
| Charge disque | PeriodicTimer trop agressif | Intervalle périodique large (filet de sécurité) ; le watcher gère l'immédiat |
| Reprise après veille | Timers dérivent au réveil | Recalcul à partir de `ResetsAt` + horloge murale, pas d'accumulation |

## Anti-Patterns

### Anti-Pattern 1: Mettre à jour l'UI depuis un thread d'arrière-plan

**What people do:** Affecter directement une `[ObservableProperty]` du ViewModel dans le callback `FileSystemWatcher` ou après un `await GetAsync()` sans revenir sur le Dispatcher.
**Why it's wrong:** WPF `VerifyAccess` lève `InvalidOperationException` (« The calling thread cannot access this object »), ou pire, corrompt silencieusement le rendu selon la propriété touchée.
**Do this instead:** Centraliser le franchissement via `IUiDispatcher.Post` à l'unique point d'abonnement (Pattern 4). Ne jamais disperser des `Dispatcher.Invoke` dans le code métier.

### Anti-Pattern 2: RingArc en UserControl avec géométrie via converters

**What people do:** UserControl contenant un `Path`, géométrie composée par un `MultiBinding` + converter à partir de 3-4 paramètres.
**Why it's wrong:** Verbeux, difficile à déboguer, recalcul non maîtrisé, réutilisation lourde.
**Do this instead:** `Shape`-derived avec `DefiningGeometry` et DPs `AffectsRender` (Pattern 2). La géométrie est un pur produit des DPs.

### Anti-Pattern 3: Relire le fichier à chaque tick d'UI

**What people do:** Sur le `DispatcherTimer` 1 s, relire l'objet d'usage / les JSONL pour rafraîchir l'affichage.
**Why it's wrong:** I/O disque sur le thread UI chaque seconde → saccades, verrouillages, usure. Confond « rafraîchir la donnée » et « animer l'affichage ».
**Do this instead:** Deux horloges séparées (voir Data Flow). Le tick 1 s *interpole* à partir du dernier snapshot + horloge murale ; seuls le watcher/PeriodicTimer relisent le disque.

### Anti-Pattern 4: Types WPF dans la couche Services

**What people do:** Retourner un `Brush` ou un `SolidColorBrush` depuis un provider, ou injecter le `Dispatcher` dans un provider.
**Why it's wrong:** Couple la source de données à WPF, casse la testabilité, viole la frontière qui justifie `IUsageProvider`.
**Do this instead:** Providers renvoient des `UsageSnapshot` neutres (double/enum/DateTimeOffset). La traduction en `Brush` se fait dans les converters, côté UI.

### Anti-Pattern 5: Oublier IsLargeArc

**What people do:** Construire l'`ArcSegment` avec `isLargeArc: false` en dur.
**Why it's wrong:** Correct visuellement tant que l'arc < 180°, puis l'arc « bascule » du mauvais côté du cercle dès qu'on dépasse le demi-tour — bug intermittent selon le temps restant.
**Do this instead:** `isLargeArc: Math.Abs(sweep) > 180` (Pattern 3), et borner le cas 360°.

## Integration Points

### External Sources (I/O disque, non documenté)

| Source | Pattern d'intégration | Gotchas |
|--------|----------------------|---------|
| Objet d'usage Claude Code (five_hour/seven_day) | Lecture fichier + parsing tolérant, `SourceReliability=Exact` | Mécanisme d'obtention à documenter (bloc 2) ; peut casser à une MAJ Claude → isolé derrière le provider |
| Transcripts JSONL `%USERPROFILE%/.claude/projects/**/*.jsonl` | FileSystemWatcher + somme tokens fenêtre, `Estimated` | Lignes/champs invalides ignorés ; fichier partiel/verrouillé pendant écriture |
| `%APPDATA%/Chronos/settings.json` | Lecture/écriture directe | Écriture atomique (fichier temp + rename) pour éviter corruption |

### Internal Boundaries

| Frontière | Communication | Notes |
|-----------|---------------|-------|
| Providers ↔ ViewModel | `event SnapshotChanged` + `Task<UsageSnapshot> GetAsync()` | **Franchit un thread** → marshaling obligatoire |
| ViewModel ↔ View/RingArc | Data binding (INotifyPropertyChanged) + converters | Unidirectionnel VM→UI pour l'affichage ; commandes UI→VM |
| App ↔ tout | Injection de dépendance (constructeur) | Seul endroit connaissant les types concrets |
| Composite ↔ providers concrets | Composition (primaire, repli) | Bascule automatique sans que l'UI le sache |

## Sources

- CommunityToolkit.Mvvm (MVVM Toolkit) — Context7 `/websites/learn_microsoft_en-us_dotnet_communitytoolkit_mvvm` : `[ObservableProperty]` sur champ ou partial property (C# preview), `[RelayCommand]`, base `ObservableObject`. **HIGH**
- WPF — Context7 `/dotnet/wpf` : pattern `DependencyProperty.Register`, `Dispatcher.BeginInvoke` + `CheckAccess()` (source `HwndHost.DestroyWindow`), modèle de priorités du Dispatcher. **HIGH**
- Géométrie `ArcSegment`/`PathGeometry`/`IsLargeArc`/`SweepDirection` : documentation WPF System.Windows.Media (connaissance établie, cohérente avec le repère WPF Y-down). **HIGH**
- Composition root DI dans `App.OnStartup` avec `Microsoft.Extensions.DependencyInjection` : pattern standard .NET desktop. **HIGH**

---
*Architecture research for: overlay WPF/MVVM temps réel avec abstraction de sources locales et rendu d'arcs vectoriels*
*Researched: 2026-07-08*
