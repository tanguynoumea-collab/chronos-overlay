---
phase: 04-orchestration-refresh-viewmodel-temps-r-el
plan: 01
subsystem: infra
tags: [background-service, filesystemwatcher, periodictimer, channels, orchestration, dotnet]

# Dependency graph
requires:
  - phase: 03
    provides: IUsageProvider.GetAsync + SnapshotChanged, UsageSnapshot, ChronosPaths, CompositeUsageProvider
  - phase: 01
    provides: IUiDispatcher (point de marshaling), ServicesLayerPurityTests (garde de neutralité)
provides:
  - RefreshOrchestrator neutre (BackgroundService) possédant watcher débouncé + PeriodicTimer + Channel(1, DropWrite)
  - Event SnapshotChanged exposé sur l'orchestrateur (point d'abonnement du VM en 04-02)
  - RefreshOptions (record d'options Singleton, PeriodicInterval 60s / Debounce 300ms)
  - Fakes de test partagés FakeUiDispatcher (RAF-04, partagé 04-02) et FakeUsageProvider (compteur GetAsync + gate)
affects: [04-02 MainViewModel, App.xaml.cs composition root, Phase 5 UI, Phase 6 settings.json]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Horloge DONNÉES : BackgroundService neutre, watcher+timer → Channel(1, DropWrite) → consommateur unique → GetAsync sérialisé"
    - "await Task.Yield() en tête d'ExecuteAsync pour ne pas bloquer StartAsync sur un GetAsync inline"
    - "Seams de test internes (types neutres) via InternalsVisibleTo pour tests déterministes sans dépendre du timing FileSystemWatcher"

key-files:
  created:
    - src/Chronos/Services/RefreshOrchestrator.cs
    - src/Chronos/Services/RefreshOptions.cs
    - tests/Chronos.Tests/Fakes/FakeUiDispatcher.cs
    - tests/Chronos.Tests/Fakes/FakeUsageProvider.cs
    - tests/Chronos.Tests/RefreshOrchestratorTests.cs
  modified: []

key-decisions:
  - "L'orchestrateur EST le point d'exposition de SnapshotChanged (pas le composite) — décision verrouillée"
  - "Channel(1, DropWrite) coalesce les rafales ET sérialise les GetAsync en une brique"
  - "await Task.Yield() au démarrage : sans lui, la boucle traite le 1er déclencheur inline et StartAsync bloque"
  - "Fakes dans le namespace Chronos.Tests (cohérence FakeClock), pas Chronos.Tests.Fakes → référencés sans using superflu"

patterns-established:
  - "Pattern orchestrateur neutre : aucun type WPF dans Chronos.Services hors adaptateurs Phase 1 (garde de pureté)"
  - "Pattern seam de test : membres internal à types neutres (bool/void) exposés via InternalsVisibleTo, invisibles à la garde WPF"

requirements-completed: [RAF-01, RAF-02]

# Metrics
duration: 18min
completed: 2026-07-08
---

# Phase 4 Plan 01 : RefreshOrchestrator (horloge données) Summary

**BackgroundService neutre pilotant un FileSystemWatcher débouncé sur usage.json + un PeriodicTimer de secours vers un Channel(1, DropWrite) à consommateur unique qui sérialise `IUsageProvider.GetAsync` et émet `SnapshotChanged`.**

## Performance

- **Duration:** 18 min
- **Started:** 2026-07-08T15:28:59Z
- **Completed:** 2026-07-08T15:47:39Z
- **Tasks:** 2 (1 auto + 1 TDD)
- **Files modified:** 5 créés

## Accomplishments
- RAF-01 : FileSystemWatcher débouncé (Changed/Created/Renamed + Error→recréation) déclenche la relecture ; rafales coalescées via `Channel(1, DropWrite)`.
- RAF-02 : `PeriodicTimer` à intervalle configurable (défaut 60 s) relit en filet de sécurité sans aucun événement watcher.
- Consommateur unique : jamais de `GetAsync` concurrents (boucle `ReadAllAsync` sérialisée).
- Couche 100 % neutre : `RefreshOrchestrator` reste hors allow-list, `ServicesLayerPurityTests` vert.
- Fakes partagés livrés pour 04-02 (`FakeUiDispatcher`) et l'orchestrateur (`FakeUsageProvider` avec gate de coalescence).

## Task Commits

1. **Task 1: RefreshOptions + fakes de test partagés** - `a42cd2d` (feat)
2. **Task 2 (RED): tests RefreshOrchestrator RAF-01/RAF-02** - `0b0481b` (test)
3. **Task 2 (GREEN): implémentation RefreshOrchestrator** - `3988f94` (feat)

_Task 2 suit le cycle TDD RED→GREEN ; aucun refactor nécessaire (implémentation directe de Pattern 1)._

## Files Created/Modified
- `src/Chronos/Services/RefreshOrchestrator.cs` - BackgroundService neutre : watcher + PeriodicTimer + Channel + event SnapshotChanged, seams internes TryTrigger/RecreateWatcher.
- `src/Chronos/Services/RefreshOptions.cs` - Record d'options (PeriodicInterval 60 s, Debounce 300 ms) + Default.
- `tests/Chronos.Tests/Fakes/FakeUiDispatcher.cs` - Fake IUiDispatcher comptant les Post (partagé 04-02).
- `tests/Chronos.Tests/Fakes/FakeUsageProvider.cs` - Fake IUsageProvider comptant GetAsync (thread-safe) + gate de coalescence.
- `tests/Chronos.Tests/RefreshOrchestratorTests.cs` - 5 tests : périodique (RAF-02), écriture usage.json (RAF-01), coalescence de rafale, Error→recréation, émission SnapshotChanged.

## Decisions Made
- **SnapshotChanged porté par l'orchestrateur** (pas par le composite) : décision verrouillée du CONTEXT — clarifie la propriété de l'horloge données et découple le VM du composite.
- **`Channel(1, DropWrite)`** : coalescence des rafales + sérialisation producteur→consommateur en une seule brique, plus propre qu'un `SemaphoreSlim`.
- **Namespace des fakes = `Chronos.Tests`** (et non `Chronos.Tests.Fakes` comme dans l'esquisse) pour rester cohérent avec `FakeClock` existant et éviter un `using` superflu dans les tests.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Ajout de `await Task.Yield()` en tête d'`ExecuteAsync`**
- **Found during:** Task 2 (GREEN — le test de coalescence ne rendait jamais la main)
- **Issue:** L'esquisse Pattern 1 du RESEARCH n'incluait pas de yield initial. Sans lui, la boucle consommateur lit le déclencheur de charge initiale de façon SYNCHRONE (l'item est déjà dans le channel), exécute `GetAsync` inline ; quand `GetAsync` bloque (gate de test / I/O lent), `StartAsync` bloque sur le thread appelant → le test `await orch.StartAsync()` se fige (deadlock de démarrage).
- **Fix:** `await Task.Yield();` en première ligne d'`ExecuteAsync` force le retour immédiat à `StartAsync` (le reste s'exécute sur le pool). Pattern recommandé .NET pour un BackgroundService long.
- **Files modified:** src/Chronos/Services/RefreshOrchestrator.cs
- **Verification:** Les 6 tests (5 orchestrateur + purity) passent en 460 ms ; suite complète 32/32.
- **Committed in:** `3988f94` (commit GREEN de la Task 2)

**2. [Rule 3 - Blocking] Namespace des fakes ajusté à `Chronos.Tests`**
- **Found during:** Task 1
- **Issue:** L'esquisse RESEARCH mettait les fakes en `namespace Chronos.Tests.Fakes`, mais `FakeClock` (fake existant, même dossier `Fakes/`) utilise `Chronos.Tests`. Diverger aurait imposé des `using Chronos.Tests.Fakes;` dans chaque test et cassé la cohérence.
- **Fix:** Fakes déclarés en `namespace Chronos.Tests` (la note de la Task 1 demandait explicitement de vérifier et rester cohérent).
- **Files modified:** tests/Chronos.Tests/Fakes/FakeUiDispatcher.cs, tests/Chronos.Tests/Fakes/FakeUsageProvider.cs
- **Verification:** Les tests référencent les fakes sans using supplémentaire ; build + suite verts.
- **Committed in:** `a42cd2d` (commit Task 1)

---

**Total deviations:** 2 auto-fixed (1 bug de démarrage, 1 blocage de cohérence de namespace)
**Impact on plan:** Les deux corrections sont nécessaires à la correction/testabilité. Aucun élargissement de périmètre ; l'architecture verrouillée (Pattern 1) est respectée à l'identique.

## Issues Encountered
- Un premier lancement de `dotnet test` s'est figé sur le test de coalescence : cause identifiée = `StartAsync` bloqué faute de yield initial (voir Déviation 1). Résolu par `await Task.Yield()`.

## User Setup Required
None - aucune configuration de service externe requise (tout se teste via fakes + fichiers temporaires, sans le pont réel).

## Next Phase Readiness
- Horloge DONNÉES prête. Le plan **04-02** peut brancher `MainViewModel` sur `RefreshOrchestrator.SnapshotChanged` (marshaling via `IUiDispatcher`, interpolation à la seconde via `DispatcherTimer`), et `App.xaml.cs` enregistrer l'orchestrateur comme Singleton + HostedService (même instance), en résolvant le VM AVANT `StartAsync` (Pitfall 3 du RESEARCH).
- `FakeUiDispatcher` déjà livré pour les tests de marshaling de 04-02.
- Note perf reportée (RESEARCH Open Question 3) : court-circuit paresseux du composite (ne scanner le JSONL que si une fenêtre primaire est Unavailable) — optionnel, non bloquant.

---
*Phase: 04-orchestration-refresh-viewmodel-temps-r-el*
*Completed: 2026-07-08*

## Self-Check: PASSED

- All 6 created files verified on disk.
- All 3 task commits verified in git history (a42cd2d, 0b0481b, 3988f94).
