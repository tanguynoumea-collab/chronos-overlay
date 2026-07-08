---
phase: 01-fondations-architecture-squelette-overlay
plan: 02
subsystem: infra
tags: [wpf, dotnet8, pinvoke, setwindowpos, topmost, overlay, dispatchertimer, dependency-injection, xunit, stafact, rob-04]

# Dependency graph
requires:
  - phase: 01-01
    provides: "Squelette WPF câblé par Generic Host, MainWindow overlay (FEN-01), couche Services (IUiDispatcher), tests STA"
provides:
  - "Interop/NativeMethods.cs : P/Invoke SetWindowPos + constantes HWND_TOPMOST / SWP_NOMOVE / SWP_NOSIZE / SWP_NOACTIVATE"
  - "Services/TopmostGuard.cs : réaffirmation périodique du topmost (DispatcherTimer 2 s dédié) sans vol de focus, délégué SetWindowPosFn injectable"
  - "MainWindow attache le guard sur SourceInitialized (HWND garanti) ; TopmostGuard enregistré en Singleton dans la composition root"
  - "TopmostGuardTests : preuve automatisée de ROB-04 par capture des flags P/Invoke"
  - "InternalsVisibleTo Chronos.Tests : accès du projet de tests aux types internes (Interop.NativeMethods)"
affects: [03-lancement-visuel, cadran, providers, persistance, comportements-fenetre]

# Tech tracking
tech-stack:
  added:
    - "P/Invoke user32!SetWindowPos (DllImport, aucune dépendance native ajoutée)"
  patterns:
    - "Réaffirmation Topmost via SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE) plutôt que toggle Topmost=false/true (pas de scintillement ni réactivation)"
    - "Délégué injectable (SetWindowPosFn) devant le P/Invoke → flags P/Invoke vérifiables en test unitaire"
    - "Attache HWND-dépendante sur SourceInitialized (jamais dans le ctor)"
    - "Timer dédié au guard, distinct du futur tick d'interpolation UI (séparation des responsabilités)"
    - "InternalsVisibleTo pour exposer les types internes au seul projet de tests"

key-files:
  created:
    - "src/Chronos/Interop/NativeMethods.cs"
    - "src/Chronos/Services/TopmostGuard.cs"
    - "tests/Chronos.Tests/TopmostGuardTests.cs"
  modified:
    - "src/Chronos/Views/MainWindow.xaml.cs"
    - "src/Chronos/App.xaml.cs"
    - "src/Chronos/Chronos.csproj"
    - "tests/Chronos.Tests/CompositionRootTests.cs"
    - "tests/Chronos.Tests/OverlayWindowConfigTests.cs"

key-decisions:
  - "Intervalle de réaffirmation fixé à 2 s (compromis réactivité/coût, recommandation RESEARCH § question ouverte n°3)"
  - "InternalsVisibleTo Chronos.Tests plutôt que rendre NativeMethods public — préserve l'encapsulation Interop tout en autorisant la vérification des flags en test"
  - "Constructeur TopmostGuard à paramètre optionnel (SetWindowPosFn? = null) : DI résout le défaut NativeMethods.SetWindowPos, le test injecte un faux"

patterns-established:
  - "Pattern P/Invoke testable : fonction native derrière un délégué injectable, défaut = méthode réelle"
  - "Réaffirmation périodique d'état fenêtre sur DispatcherTimer dédié attaché à SourceInitialized"

requirements-completed: [ROB-04]

# Metrics
duration: 3min
completed: 2026-07-08
---

# Phase 1 Plan 02 : Plomberie Topmost Guard Summary

**Réaffirmation périodique du Topmost de l'overlay via `SetWindowPos(HWND_TOPMOST, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE)` sur un DispatcherTimer 2 s dédié — sans vol de focus (ROB-04) — attachée sur `SourceInitialized` et prouvée par la capture des flags P/Invoke en test.**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-07-08T13:33:01Z
- **Completed:** 2026-07-08T13:35:35Z
- **Tasks:** 2
- **Files modified:** 8 (3 créés, 5 modifiés)

## Accomplishments
- `Interop/NativeMethods.cs` : P/Invoke `SetWindowPos` + constantes `HWND_TOPMOST` (-1), `SWP_NOSIZE` (0x1), `SWP_NOMOVE` (0x2), `SWP_NOACTIVATE` (0x10).
- `Services/TopmostGuard.cs` : `IDisposable` sur `DispatcherTimer` dédié (2 s), délégué `SetWindowPosFn` injectable, `Attach()` sur HWND garanti + `Reassert()` immédiat, `Dispose()` arrête le timer.
- `MainWindow` étendue : ctor prend `TopmostGuard`, attache le guard sur `SourceInitialized` (HWND présent), placement de départ (Loaded) conservé.
- `App.ConfigureServices` : `AddSingleton<TopmostGuard>()` en composition root Singleton.
- `TopmostGuardTests` (`[WpfFact]` STA) : injecte un faux `SetWindowPosFn` capturant `(after, flags)`, prouve `after == HWND_TOPMOST` et `flags == SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE` (= 0x13). Suite complète verte (3/3).

## Task Commits

Chaque tâche a été committée atomiquement :

1. **Task 1: P/Invoke NativeMethods + service TopmostGuard (ROB-04)** - `e5aa1b8` (feat)
2. **Task 2: Câblage du guard dans la fenêtre + DI + test des flags (ROB-04)** - `fe4491b` (feat)

_Note : le métacommit de plan (docs) suit ce SUMMARY._

## Files Created/Modified
- `src/Chronos/Interop/NativeMethods.cs` - P/Invoke SetWindowPos + constantes topmost/SWP (créé)
- `src/Chronos/Services/TopmostGuard.cs` - Réaffirmation périodique du topmost, délégué injectable (créé)
- `tests/Chronos.Tests/TopmostGuardTests.cs` - Preuve ROB-04 par capture des flags P/Invoke (créé)
- `src/Chronos/Views/MainWindow.xaml.cs` - ctor + TopmostGuard, attache sur SourceInitialized (modifié)
- `src/Chronos/App.xaml.cs` - AddSingleton<TopmostGuard> (modifié)
- `src/Chronos/Chronos.csproj` - InternalsVisibleTo Chronos.Tests (modifié)
- `tests/Chronos.Tests/CompositionRootTests.cs` - enregistre TopmostGuard pour le nouveau ctor (modifié)
- `tests/Chronos.Tests/OverlayWindowConfigTests.cs` - construit MainWindow avec TopmostGuard (modifié)

## Decisions Made
- **Intervalle 2 s** : conforme à la recommandation RESEARCH (question ouverte n°3) — compromis réactivité/coût, timer dédié distinct du futur tick d'interpolation UI (Phases 4-5).
- **`InternalsVisibleTo Chronos.Tests`** : `NativeMethods` reste `internal` (encapsulation Interop) ; le test y accède pour asserter les valeurs `HWND_TOPMOST`/`SWP_*` réelles plutôt que des littéraux magiques.
- **Ctor à paramètre optionnel** : `TopmostGuard(SetWindowPosFn? = null)` — DI utilise la valeur par défaut (`NativeMethods.SetWindowPos`), le test substitue un faux capturant.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `InternalsVisibleTo` requis pour référencer `NativeMethods` (internal) en test**
- **Found during:** Task 2 (rédaction de TopmostGuardTests)
- **Issue:** Le plan exige que TopmostGuardTests asserte sur `NativeMethods.HWND_TOPMOST` et `NativeMethods.SWP_*`, mais `NativeMethods` est `internal static` (comme au RESEARCH) et le projet de tests n'y avait pas accès → non-compilation.
- **Fix:** Ajout de `<InternalsVisibleTo Include="Chronos.Tests" />` au `Chronos.csproj`. `NativeMethods` reste `internal` (encapsulation préservée).
- **Files modified:** src/Chronos/Chronos.csproj
- **Verification:** `dotnet build Chronos.sln -c Debug` réussit ; TopmostGuardTests compile et passe.
- **Committed in:** fe4491b (Task 2 commit)

**2. [Rule 3 - Blocking] `OverlayWindowConfigTests` cassé par le nouveau ctor de MainWindow**
- **Found during:** Task 2 (extension du ctor MainWindow)
- **Issue:** L'ajout du paramètre `TopmostGuard` au ctor de `MainWindow` rend `new MainWindow(new MainViewModel())` (dans OverlayWindowConfigTests, non listé dans les fichiers du plan) non-compilable → build cassé.
- **Fix:** Mise à jour de l'appel en `new MainWindow(new MainViewModel(), new TopmostGuard())` (guard non attaché — le test couvre FEN-01). Ajout du `using Chronos.Services;`.
- **Files modified:** tests/Chronos.Tests/OverlayWindowConfigTests.cs
- **Verification:** Suite complète verte (3/3) via `dotnet test Chronos.sln -c Debug`.
- **Committed in:** fe4491b (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (2 blocking)
**Impact on plan:** Les deux corrections sont nécessaires pour que le build/tests passent après le changement de ctor exigé par le plan. Aucun changement de périmètre ni d'architecture ; aucun scope creep.

## Issues Encountered
None - travaux planifiés exécutés sans obstacle hors les deux corrections de compilation ci-dessus.

## User Setup Required
None - aucune configuration de service externe requise.

## Next Phase Readiness
- ROB-04 automatiquement prouvé par les flags P/Invoke (TopmostGuardTests) ; guard disposé proprement (timer arrêté) à la fermeture du host.
- Prêt pour le Plan 03 (lancement visuel / smoke manuel) : validation visuelle que la fenêtre reste au premier plan dans le temps sans voler le focus.
- Limite Windows connue et documentée (RESEARCH Pitfall 6) : le plein écran exclusif tiers peut passer devant malgré la réaffirmation — comportement attendu, non traité.
- Aucun blocker introduit.

## Self-Check: PASSED

Tous les fichiers déclarés existent (3 créés + 5 modifiés + SUMMARY) et les 2 commits de tâche (e5aa1b8, fe4491b) sont présents dans l'historique git.

---
*Phase: 01-fondations-architecture-squelette-overlay*
*Completed: 2026-07-08*
