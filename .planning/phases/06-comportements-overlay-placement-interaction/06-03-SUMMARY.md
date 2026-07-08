---
phase: 06-comportements-overlay-placement-interaction
plan: 03
subsystem: ui
tags: [wpf, win32, placement, dragmove, multi-monitor, dpi, setwindowpos, di, persistence, topmost]

# Dependency graph
requires:
  - phase: 06-comportements-overlay-placement-interaction
    provides: "06-01 CornerSnap (NearestCorner/ClassifyCorner/CornerToTopLeft) + SettingsService/ChronosSettings"
  - phase: 06-comportements-overlay-placement-interaction
    provides: "06-02 NativeMethods étendu (MonitorFromWindow/GetMonitorInfo/GetWindowRect/GetDpiForMonitor/EnumDisplayMonitors/HWND_BOTTOM) + TopmostGuard.Suspend/Resume"
provides:
  - "IWindowController — contrat neutre (SendToBackground/BringToForeground/Quit) consommé par le VM (menu 06-04)"
  - "OverlayController — adaptateur WPF de placement physique (SetWindowPos), arrière-plan et restauration ; ajouté à l'allow-list de pureté"
  - "MainWindow : drag (DragMove) + snap au coin le plus proche au relâchement + hook WM_DISPLAYCHANGE + restauration en SourceInitialized"
  - "App : DI du controller + settings + restauration AVANT premier rendu + RefreshOptions dérivé de settings"
affects: [06-04 menu contextuel + dialogue recalibrage (consomme IWindowController)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Placement en pixels PHYSIQUES via SetWindowPos sur rcWork du moniteur courant (contourne bug WPF Window.Left/Top #4127 en PerMonitorV2 DPI mixte)"
    - "Snap au RETOUR de DragMove (bloquant) — jamais dans un handler MouseUp (DragMove consomme le relâchement)"
    - "Restauration coin+device en SourceInitialized (HWND dispo, avant 1er rendu → pas de flash) ; repli moniteur primaire si device disparu"
    - "Adaptateur WPF à délégué SetWindowPos injectable (testable via délégué capturant, comme TopmostGuard)"

key-files:
  created:
    - src/Chronos/Services/IWindowController.cs
    - src/Chronos/Services/OverlayController.cs
    - tests/Chronos.Tests/OverlayControllerTests.cs
  modified:
    - src/Chronos/Views/MainWindow.xaml.cs
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/ServicesLayerPurityTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - tests/Chronos.Tests/CadranBindingTests.cs
    - tests/Chronos.Tests/OverlayWindowConfigTests.cs

key-decisions:
  - "OverlayController vit dans Chronos.Services (pas Views) et est ajouté à l'allow-list de pureté : seule sa méthode Attach(Window) expose PresentationFramework"
  - "SetWindowPos routé via le délégué injectable pour SendToBackground ET SnapToNearestCorner → arrière-plan prouvé en test sans écran réel"
  - "ChronosSettings enregistré en Singleton (SettingsService.Load() une fois) et réutilisé par RefreshOptions + restauration"
  - "RefreshOptions dérivé de RefreshIntervalSeconds (défaut 60 si ≤ 0), sans UI de réglage (Open Question 3 résolue)"

patterns-established:
  - "Restauration avant Show : App.ApplyRestoredState(settings) → RestorePlacement en SourceInitialized"
  - "Hook WM_DISPLAYCHANGE (0x007E) via HwndSource.AddHook → re-snap sur le moniteur courant (anti widget hors-écran)"

requirements-completed: [FEN-02, FEN-03, FEN-04, FEN-05, FEN-07]

# Metrics
duration: 4min
completed: 2026-07-08
---

# Phase 6 Plan 03 : Câblage du placement réel de l'overlay Summary

**OverlayController (adaptateur WPF) pilote SetWindowPos en pixels physiques pour le snap multi-écrans et le mode arrière-plan, MainWindow gère drag+snap au relâchement de DragMove + hook WM_DISPLAYCHANGE, et App restaure le coin+device persistés AVANT le premier rendu — 99 tests verts, zéro NuGet.**

## Performance

- **Duration:** 4 min
- **Started:** 2026-07-08T20:00:22Z
- **Completed:** 2026-07-08T20:04:18Z
- **Tasks:** 3 (Task 1 en TDD)
- **Files modified:** 9 (3 créés, 6 modifiés)

## Accomplishments
- **OverlayController (FEN-03/04/05)** : adaptateur WPF qui pose la fenêtre en pixels PHYSIQUES via `SetWindowPos` sur la `rcWork` du moniteur courant (`MonitorFromWindow`+`GetMonitorInfo`), contournant le bug WPF `Window.Left/Top` PerMonitorV2 DPI mixte (#4127). `SnapToNearestCorner` (coin le plus proche + persistance coin+device), `RestorePlacement` (retrouve le moniteur par device name, repli primaire+même coin si disparu), hook `WM_DISPLAYCHANGE` pour re-clamper à chaud.
- **Mode arrière-plan (FEN-05)** : `SendToBackground` = `Topmost=false` + `TopmostGuard.Suspend()` + `HWND_BOTTOM` (SWP_NOACTIVATE) ; `BringToForeground` = `Topmost=true` + `Resume()`. État `Background` persisté. Prouvé par délégué `SetWindowPos` capturé (pas d'écran réel requis).
- **IWindowController** : contrat NEUTRE (aucun type WPF en signature) prêt pour le VM/menu de 06-04.
- **MainWindow (FEN-02/03/04)** : `DragMove()` bloquant + `SnapToNearestCorner()` au RETOUR (pas de handler MouseUp), attach guard+controller et restauration en `SourceInitialized` (avant 1er rendu), re-snap sur `DpiChanged`. `PlacerCoinSuperieurDroit` (Loaded) supprimé.
- **App (FEN-07)** : DI de SettingsService/ChronosSettings/OverlayController/IWindowController ; `RefreshOptions` dérivé de `RefreshIntervalSeconds` (sans UI) ; `ApplyRestoredState(settings)` appelé AVANT `Show()`.
- **Garde de pureté** étendue avec `OverlayController` et restée verte (aucune autre fuite WPF dans Services/Models).

## Task Commits

Each task was committed atomically :

1. **Task 1 (TDD): IWindowController + OverlayController (FEN-03/04/05)** - `5ed9097` (test RED) → `82db431` (feat GREEN)
2. **Task 2: MainWindow DragMove+snap+hook+attach (FEN-02/03/04)** - `31fccb5` (feat)
3. **Task 3: App DI + restauration + intervalle depuis settings (FEN-07)** - `4fceca2` (feat)

_TDD Task 1 : pas de commit refactor (implémentation minimale déjà propre)._

## Files Created/Modified
- `src/Chronos/Services/IWindowController.cs` - Contrat neutre SendToBackground/BringToForeground/Quit.
- `src/Chronos/Services/OverlayController.cs` - Adaptateur WPF : placement physique, snap, restauration, arrière-plan, hook écran.
- `src/Chronos/Views/MainWindow.xaml.cs` - DragMove+snap, attach controller, restauration SourceInitialized, hook DpiChanged ; suppression PlacerCoinSuperieurDroit.
- `src/Chronos/App.xaml.cs` - DI controller/settings, restauration avant Show, RefreshOptions dérivé de settings.
- `tests/Chronos.Tests/OverlayControllerTests.cs` - 2 [WpfFact] (SendToBackground HWND_BOTTOM/SWP_NOACTIVATE/Topmost, BringToForeground Topmost).
- `tests/Chronos.Tests/ServicesLayerPurityTests.cs` - Allow-list étendue avec OverlayController.
- `tests/Chronos.Tests/CompositionRootTests.cs` - Enregistre SettingsService + OverlayController (nouveau ctor MainWindow).
- `tests/Chronos.Tests/CadranBindingTests.cs` - Construction MainWindow adaptée au nouveau ctor.
- `tests/Chronos.Tests/OverlayWindowConfigTests.cs` - Construction MainWindow adaptée au nouveau ctor.

## Decisions Made
- **OverlayController en Chronos.Services** (pas Views comme suggéré dans RESEARCH) : conforme au frontmatter du plan et à l'allow-list de pureté ; seule `Attach(Window)` expose PresentationFramework.
- **SetWindowPos routé via le délégué injectable** pour tout le controller → arrière-plan et snap vérifiables sans moniteur réel.
- **ChronosSettings en Singleton** (une seule lecture au démarrage), consommé par RefreshOptions et la restauration.
- **RefreshOptions dérivé de settings** avec garde `> 0 ? … : 60` (résout Open Question 3, aucune UI).

## Deviations from Plan

None - plan executed exactly as written.

Note : deux fichiers de test existants hors périmètre nominal (`CadranBindingTests.cs`, `OverlayWindowConfigTests.cs`) construisaient `MainWindow` avec l'ancien ctor à 2 paramètres. Leur mise à jour pour le nouveau ctor (3 paramètres) était une conséquence directe et attendue du changement de signature de Task 2 (le plan cite explicitement la mise à jour de `CompositionRootTests` pour le nouveau ctor) ; ces deux fichiers relèvent du même changement mécanique, sans modification de leur intention de test.

## Issues Encountered
None - les trois tâches ont suivi le plan ; Task 1 en RED→GREEN, aucun refactor nécessaire.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `IWindowController` prêt pour le menu contextuel (FEN-06) de **06-04** : items « Arrière-plan » (ToggleBackground via SendToBackground/BringToForeground), « Quitter » (Quit).
- Placement physique, snap, restauration coin+device et repli multi-écrans opérationnels ; UAT manuel (drag réel, 2 écrans DPI mixte, franchissement de moniteur) documenté pour la vérification de phase.
- `WeeklyRecalibration` (06-01) + `WeeklyAnchor` + `IAutostartService` (06-02) restent à câbler dans le menu/dialogue de 06-04.
- Suite complète : **99 tests verts** (97 précédents + 2 OverlayController), garde de pureté verte, aucun NuGet ajouté.

## Self-Check: PASSED

- Fichiers créés (`IWindowController.cs`, `OverlayController.cs`, `OverlayControllerTests.cs`) : présents sur disque.
- Commits `5ed9097`, `82db431`, `31fccb5`, `4fceca2` présents dans l'historique git.

---
*Phase: 06-comportements-overlay-placement-interaction*
*Completed: 2026-07-08*
