# Phase 4: Orchestration refresh + ViewModel temps réel - Context

**Gathered:** 2026-07-08
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Les données se rafraîchissent automatiquement (deux horloges distinctes) et alimentent un ViewModel
qui interpole l'affichage à la seconde, tout franchissement de thread passant par un point de
marshaling unique.

Requirements couverts : RAF-01, RAF-02, RAF-03, RAF-04.
</domain>

<decisions>
## Implementation Decisions

### Architecture des deux horloges (verrouillé — ARCHITECTURE.md)
- Horloge DONNÉES (thread pool) : FileSystemWatcher débouncé sur les sources (usage.json du pont ; éventuellement dossiers JSONL) + PeriodicTimer configurable en filet de sécurité → relit via IUsageProvider.GetAsync → publie un UsageSnapshot.
- Horloge UI (DispatcherTimer 1 s) : INTERPOLE le temps restant à partir du dernier snapshot, AUCUN I/O disque à ce tick. L'anti-pattern à éviter absolument : relire le fichier à chaque tick UI.
- Frontière de thread : tout passage horloge données → ViewModel passe par IUiDispatcher (déjà posé Phase 1). Aucune InvalidOperationException possible.

### FileSystemWatcher (verrouillé — PITFALLS.md)
- Best-effort : buffer overflow silencieux, doublons, fichiers verrouillés → debounce (~300-500 ms) + PeriodicTimer de secours OBLIGATOIRE.
- Gérer l'événement Error du watcher (re-création du watcher).
- Surveiller %APPDATA%\Chronos (usage.json). La surveillance des JSONL (~/.claude/projects récursif) est plus coûteuse : à faire seulement si peu onéreuse (watcher sur le dossier racine avec IncludeSubdirectories), sinon le PeriodicTimer suffit pour le repli.

### ViewModel (verrouillé — conventions)
- MainViewModel : [ObservableProperty] pour l'état des deux fenêtres (utilization, provenance, resets_at, fraction de temps restante interpolée, compte à rebours formaté), état DonnéesIndisponibles.
- CommunityToolkit.Mvvm, aucun code-behind métier.
- L'interpolation à la seconde recalcule FractionRemaining(now) à partir du snapshot (les modèles Phase 3 prennent `now` en paramètre — conçu pour ça).
- Formatage des comptes à rebours en français (ex. « 2 h 05 » / « 3 j 14 h »), hebdo best-effort.

### Services d'orchestration
- Un service hôte (IHostedService ou service démarré explicitement) possède watcher + PeriodicTimer, expose l'événement SnapshotChanged — c'est lui qui appelle GetAsync, PAS le ViewModel.
- Intervalle du PeriodicTimer configurable (défaut raisonnable : 60 s), lu depuis la config (pré-câbler une valeur, la persistance settings.json arrive en Phase 6).
- Disposal propre (CancellationToken, await du timer) à l'arrêt du host — pattern OnExit Phase 1.

### Claude's Discretion
Détails du debounce, structure interne du service d'orchestration, format exact des chaînes de compte à rebours, tests.
</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- IUsageProvider → CompositeUsageProvider en DI (Singleton) — pipeline complet Phase 3, 27 tests verts.
- IUiDispatcher/WpfUiDispatcher (Phase 1) — le point de marshaling unique exigé par RAF-04.
- IClock/SystemClock/FakeClock — horloge injectable pour tester l'interpolation.
- ChronosPaths — chemins injectables (%APPDATA%\Chronos\usage.json).
- Generic Host dans App.xaml.cs — enregistrer le service d'orchestration ici.
- MainViewModel existe (squelette Phase 1) — à enrichir.

### Established Patterns
- TDD sur la logique pure, tests xUnit, garde de pureté WPF (le service d'orchestration côté données doit rester neutre ; seul le ViewModel/adaptateurs touchent WPF).
- Commentaires français, records immuables.

### Integration Points
- App.xaml.cs : DI du service d'orchestration + démarrage/arrêt avec le host.
- MainViewModel : consommé par MainWindow (DataContext déjà câblé Phase 1).
</code_context>

<specifics>
## Specific Ideas

- Le DispatcherTimer 1 s ne doit PAS tourner en agression : AllowsTransparency force le rendu logiciel — mise à jour de propriétés texte/valeurs uniquement, pas d'animation continue.
- Le watcher sur usage.json doit tolérer l'écriture atomique du pont (rename) — événements Renamed/Created/Changed tous traités.
- staleness : exposer l'âge de la donnée (CapturedAt) dans le ViewModel pour l'UI Phase 5 (« estimée » / « données périmées »).
</specifics>

<deferred>
## Deferred Ideas

None — discuss phase skipped.
</deferred>
