---
phase: 04-orchestration-refresh-viewmodel-temps-r-el
plan: 02
subsystem: ui
tags: [mvvm, communitytoolkit, viewmodel, dispatchertimer, marshaling, interpolation, wpf, dotnet]

# Dependency graph
requires:
  - phase: 04-01
    provides: RefreshOrchestrator (BackgroundService neutre) + event SnapshotChanged, RefreshOptions, FakeUiDispatcher/FakeUsageProvider
  - phase: 03
    provides: UsageSnapshot, WindowState.FractionRemaining (pure, prend now), SourceReliability, CompositeUsageProvider
  - phase: 01
    provides: IUiDispatcher (point de marshaling unique), IClock/FakeClock, App.xaml.cs (Host + DI)
provides:
  - CountdownFormatter (formatage FR pur d'un TimeSpan, neutre, sans I/O)
  - WindowGaugeViewModel (sous-VM par fenêtre : fraction d'arc, utilization, countdown FR, provenance, épuisement)
  - MainViewModel temps réel (abonnement orchestrateur + marshaling unique IUiDispatcher.Post + Interpolate pur + StartClock)
  - Câblage App.xaml.cs (orchestrateur Singleton + IHostedService même instance, VM pré-résolu avant StartAsync)
  - MainWindow démarre l'horloge UI 1 s au chargement (StartClock via Loaded)
affects: [Phase 5 UI/cadran XAML (bind FiveHour/SevenDay/RingArc), Phase 6 settings.json, DAT-08 marquage estimé]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Marshaling unique : abonnement thread pool → IUiDispatcher.Post (RAF-04), aucune mutation d'ObservableProperty hors du Post"
    - "Interpolation pure : Interpolate(now) recalcule fraction+countdown depuis le dernier WindowState mémorisé, jamais de GetAsync au tick (RAF-03)"
    - "DispatcherTimer créé côté UI (StartClock) et non dans le ctor du VM → tests en [Fact] simple (Pitfall 4)"
    - "Formateur FR pur (littéraux fixes, aucune CultureInfo) hautement testable"
    - "Ordre de démarrage : pré-résolution du VM Singleton avant StartAsync pour préserver le snapshot initial (Pitfall 3)"

key-files:
  created:
    - src/Chronos/Text/CountdownFormatter.cs
    - src/Chronos/ViewModels/WindowGaugeViewModel.cs
    - tests/Chronos.Tests/CountdownFormatterTests.cs
    - tests/Chronos.Tests/MainViewModelTests.cs
  modified:
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/App.xaml.cs
    - src/Chronos/Views/MainWindow.xaml.cs
    - tests/Chronos.Tests/OverlayWindowConfigTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs

key-decisions:
  - "Test de marshaling piloté par le vrai RefreshOrchestrator + FakeUsageProvider (PeriodicInterval 10 min pour isoler la charge initiale) → PostCount==1 déterministe, sans seam supplémentaire dans la prod"
  - "Tests d'application/interpolation appellent ApplySnapshot/Interpolate directement (membres internal via InternalsVisibleTo) → déterministes, sans Dispatcher"
  - "IsStale seuil 2 min (discrétion RESEARCH, à confirmer Phase 5)"

patterns-established:
  - "Sous-VM par fenêtre (WindowGaugeViewModel) réutilisable : le cadran Phase 5 bindera deux instances (FiveHour/SevenDay)"
  - "Deux handlers Loaded coexistent sur MainWindow (placement + StartClock)"

requirements-completed: [RAF-03, RAF-04]

# Metrics
duration: 5min
completed: 2026-07-08
---

# Phase 4 Plan 02 : ViewModel temps réel (marshaling + interpolation) Summary

**MainViewModel qui s'abonne à RefreshOrchestrator.SnapshotChanged, marshalle en un point unique via IUiDispatcher.Post (RAF-04), et interpole fraction d'arc + compte à rebours FR chaque seconde via un DispatcherTimer côté UI sans aucun I/O disque (RAF-03).**

## Performance

- **Duration:** 5 min
- **Started:** 2026-07-08T15:51:56Z
- **Completed:** 2026-07-08T15:56:37Z
- **Tasks:** 3 (2 TDD + 1 câblage)
- **Files modified:** 9 (4 créés, 5 modifiés)

## Accomplishments
- RAF-04 : franchissement de thread unique — le snapshot poussé hors thread UI est appliqué via `IUiDispatcher.Post` exactement une fois (`FakeUiDispatcher.PostCount == 1`), les sous-VM reflètent le snapshot ; `DataUnavailable` vrai ssi les deux fenêtres Unavailable.
- RAF-03 : `Interpolate(now)` pur recalcule fraction d'arc décroissante + compte à rebours FR sans jamais appeler `GetAsync` (`FakeUsageProvider.GetCount == 0` au tick) ; staleness dérivée de `SourceCapturedAt` (> 2 min → `IsStale`).
- `CountdownFormatter` FR pur : `3 j 14 h` / `2 h 05` / `45 min` / `0 min` (garde du temps écoulé).
- Câblage complet : orchestrateur Singleton + `IHostedService` (même instance), VM pré-résolu AVANT `StartAsync` (snapshot initial préservé, Pitfall 3), `DispatcherTimer` 1 s démarré au `Loaded` de la fenêtre.
- Suite complète verte : 41/41 tests, 0 avertissement.

## Task Commits

1. **Task 1 (RED): tests CountdownFormatter FR** - `a3030b4` (test)
2. **Task 1 (GREEN): implémentation CountdownFormatter** - `e5e74ae` (feat)
3. **Task 2 (RED): tests MainViewModel RAF-03/RAF-04** - `a26366b` (test)
4. **Task 2 (GREEN): MainViewModel + WindowGaugeViewModel + fix tests amont** - `4560882` (feat)
5. **Task 3: câblage App.xaml.cs + StartClock MainWindow** - `d20e868` (feat)

_Tasks 1 et 2 suivent le cycle TDD RED→GREEN ; aucun refactor nécessaire (Patterns 4/5 du RESEARCH implémentés directement)._

## Files Created/Modified
- `src/Chronos/Text/CountdownFormatter.cs` - Formatage FR pur d'un `TimeSpan` (neutre, aucune dépendance WPF, aucune CultureInfo).
- `src/Chronos/ViewModels/WindowGaugeViewModel.cs` - Sous-VM par fenêtre : `FractionRemaining`, `Utilization`, `CountdownText`, `Exhausted`, `Reliability`, `IsEstimated` ; `Apply(state)` + `Interpolate(now)` pur.
- `src/Chronos/ViewModels/MainViewModel.cs` - Abonnement `SnapshotChanged` + `OnSnapshotChanged` → `_ui.Post` (frontière unique), `ApplySnapshot`/`Interpolate` internes, `StartClock` (DispatcherTimer hors ctor).
- `src/Chronos/App.xaml.cs` - `RefreshOptions` + `RefreshOrchestrator` Singleton + `AddHostedService` (même instance) ; pré-résolution du VM avant `StartAsync`.
- `src/Chronos/Views/MainWindow.xaml.cs` - Second handler `Loaded` appelant `viewModel.StartClock()`.
- `tests/Chronos.Tests/CountdownFormatterTests.cs` - 4 cas de formatage FR (5 assertions via Theory).
- `tests/Chronos.Tests/MainViewModelTests.cs` - RAF-04 (marshaling PostCount==1, DataUnavailable), RAF-03 (interpolation sans I/O, IsStale), tous en `[Fact]` simple.
- `tests/Chronos.Tests/OverlayWindowConfigTests.cs` - Mise à jour du ctor `MainViewModel` (nouvelles dépendances).
- `tests/Chronos.Tests/CompositionRootTests.cs` - Enregistrements DI miroir de `App.xaml.cs` (orchestrateur + pipeline) pour résoudre le nouveau VM.

## Decisions Made
- **Marshaling testé via le vrai orchestrateur + FakeUsageProvider** (PeriodicInterval 10 min pour isoler la seule charge initiale) : prouve `PostCount == 1` de façon déterministe sans ajouter de seam de test à la production (l'orchestrateur 04-01 reste inchangé).
- **Application/interpolation testées en appelant `ApplySnapshot`/`Interpolate` directement** (internal via `InternalsVisibleTo`) : évite tout `DispatcherTimer`/contexte STA → tests `[Fact]` simples, preuve concrète du Pitfall 4.
- **Seuil `IsStale` = 2 min** conservé (discrétion du RESEARCH, à confirmer au design UI Phase 5).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Mise à jour de deux tests amont cassés par le nouveau ctor de `MainViewModel`**
- **Found during:** Task 2 (GREEN — le build échouait sur des tests existants)
- **Issue:** Le plan change la signature de `MainViewModel` (ctor `()` → `(RefreshOrchestrator, IUiDispatcher, IClock)`). Deux tests Phase 1 le construisaient/résolvaient avec l'ancienne signature : `OverlayWindowConfigTests` (`new MainViewModel()`) et `CompositionRootTests` (résolution DI sans les nouvelles dépendances enregistrées) → build/résolution en erreur.
- **Fix:** `OverlayWindowConfigTests` construit désormais le VM avec un `RefreshOrchestrator` non démarré (aucun I/O) + fakes ; `CompositionRootTests` enregistre le pipeline données + orchestrateur en miroir de `App.xaml.cs` pour que le graphe résolve `MainWindow`/`MainViewModel`.
- **Files modified:** tests/Chronos.Tests/OverlayWindowConfigTests.cs, tests/Chronos.Tests/CompositionRootTests.cs
- **Verification:** Suite complète 41/41 verte (dont les deux tests corrigés).
- **Committed in:** `4560882` (commit GREEN de la Task 2)

---

**Total deviations:** 1 auto-fixed (1 blocage de compilation dû au changement de contrat mandaté par le plan)
**Impact on plan:** Correction strictement nécessaire à la compilation ; aucun élargissement de périmètre. Les deux tests restent fidèles à leur intention (config overlay FEN-01 ; résolution + disposition du graphe DI).

## Issues Encountered
None - les 3 tâches se sont déroulées comme prévu (hors la mise à jour de compatibilité ci-dessus).

## User Setup Required
None - aucune configuration de service externe requise.

## Next Phase Readiness
- Couche présentation temps réel prête : Phase 5 peut binder le cadran XAML (Path/ArcSegment) sur `MainViewModel.FiveHour`/`SevenDay` (FractionRemaining → longueur d'arc, Utilization → couleur, CountdownText → texte central), `DataUnavailable` → état « données indisponibles », `IsEstimated` → marquage estimé (DAT-08).
- Vérification humaine (VALIDATION.md) reportée au design UI : lancer l'app pour observer le compte à rebours décroître à la seconde et la réaction à une écriture usage.json (bout-en-bout RAF-01→RAF-04). Non bloquant pour la planification Phase 5.
- Amélioration optionnelle notée (RESEARCH Open Question 3) : court-circuit paresseux du composite (ne scanner le JSONL que si une fenêtre primaire est Unavailable) — non bloquant.

---
*Phase: 04-orchestration-refresh-viewmodel-temps-r-el*
*Completed: 2026-07-08*

## Self-Check: PASSED

- Les 4 fichiers créés vérifiés sur disque.
- Les 5 commits de tâches vérifiés dans l'historique git (a3030b4, e5e74ae, a26366b, 4560882, d20e868).
