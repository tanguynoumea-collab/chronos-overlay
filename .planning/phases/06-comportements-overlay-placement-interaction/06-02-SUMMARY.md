---
phase: 06-comportements-overlay-placement-interaction
plan: 02
subsystem: infra
tags: [win32, pinvoke, interop, dpi, multi-monitor, wpf, com, autostart, topmost]

# Dependency graph
requires:
  - phase: 01-fondations-architecture-squelette-overlay
    provides: NativeMethods.SetWindowPos + TopmostGuard (ROB-04), garde de pureté Services
provides:
  - "NativeMethods étendu : HWND_BOTTOM, MonitorFromWindow, GetMonitorInfo (MONITORINFOEX rcWork/szDevice), GetWindowRect, GetDpiForMonitor, EnumDisplayMonitors"
  - "TopmostGuard.Suspend()/Resume() pour le mode arrière-plan (FEN-05)"
  - "IAutostartService/AutostartService : .lnk shell:startup sans dépendance native (DEP-02)"
affects: [06-03, 06-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "P/Invoke moniteur/DPI en pixels physiques (contourne le bug Window.Left/Top PerMonitorV2)"
    - "COM late-bound via Type.GetTypeFromProgID + dynamic (aucun NuGet d'interop)"
    - "Service neutre à dossier injectable (testable sans polluer le vrai Startup, comme ChronosPaths)"

key-files:
  created:
    - src/Chronos/Services/IAutostartService.cs
    - src/Chronos/Services/AutostartService.cs
    - tests/Chronos.Tests/AutostartServiceTests.cs
  modified:
    - src/Chronos/Interop/NativeMethods.cs
    - src/Chronos/Services/TopmostGuard.cs
    - tests/Chronos.Tests/TopmostGuardTests.cs

key-decisions:
  - "Cible du raccourci autostart = Environment.ProcessPath (single-file-safe), jamais Assembly.Location (vide en mono-fichier)"
  - "Suspend/Resume via _timer.Stop()/Start()+Reassert() — pas de toggle Topmost=false;true (évite scintillement)"
  - "Dossier startup injecté au ctor d'AutostartService → tests en dossier temp, jamais le vrai Startup"

patterns-established:
  - "Placement multi-écrans en pixels physiques : MonitorFromWindow + GetMonitorInfo(rcWork) plutôt que SystemParameters.WorkArea (écran primaire seul)"
  - "Autostart .lnk via WScript.Shell COM late-bound (dynamic), aucune dépendance native"

requirements-completed: [FEN-05, DEP-02]

# Metrics
duration: 4 min
completed: 2026-07-08
---

# Phase 6 Plan 2: Interop Win32 + plomberie placement/arrière-plan/autostart Summary

**P/Invoke moniteur/DPI/fenêtre multi-écrans (HWND_BOTTOM + MonitorFromWindow/GetMonitorInfo/GetDpiForMonitor/EnumDisplayMonitors), TopmostGuard.Suspend/Resume pour le mode arrière-plan, et AutostartService .lnk shell:startup sans dépendance native.**

## Performance

- **Duration:** 4 min
- **Started:** 2026-07-08T19:51:18Z
- **Completed:** 2026-07-08T19:55:02Z
- **Tasks:** 3 (dont 2 en TDD)
- **Files modified:** 6 (3 créés, 3 modifiés)

## Accomplishments
- `NativeMethods` étendu avec l'interop moniteur/DPI/fenêtre/arrière-plan nécessaire au placement physique multi-écrans (consommé par OverlayController en 06-03) : `HWND_BOTTOM`, `MONITOR_DEFAULTTONEAREST`, `MONITORINFOF_PRIMARY`, structs `RECT`/`MONITORINFOEX`, `MonitorFromWindow`, `GetMonitorInfo`, `GetWindowRect`, `GetDpiForMonitor`, `EnumDisplayMonitors`.
- `TopmostGuard.Suspend()`/`Resume()` prouvés par délégué `SetWindowPos` capturé : Suspend ne réaffirme rien, Resume repose immédiatement `HWND_TOPMOST` (FEN-05). Test ROB-04 d'origine toujours vert.
- `IAutostartService`/`AutostartService` : création/suppression d'un `.lnk` dans un dossier startup injectable, ciblant `Environment.ProcessPath`, via COM late-bound `WScript.Shell` — aucun NuGet, aucun droit admin (DEP-02).
- Clic-traversant (WS_EX_TRANSPARENT) explicitement NON implémenté — différé V2 (V2-04), conforme au cadrage du plan.

## Task Commits

Each task was committed atomically:

1. **Task 1: Étendre NativeMethods (moniteur, fenêtre, arrière-plan, DPI)** - `8a4fdfe` (feat)
2. **Task 2 (TDD): TopmostGuard.Suspend/Resume (FEN-05)** - `f8905b4` (test RED) → `f1a5200` (feat GREEN)
3. **Task 3 (TDD): AutostartService .lnk shell:startup (DEP-02)** - `3776464` (test RED) → `a8a9c8e` (feat GREEN)

_TDD : pas de commit refactor (implémentations minimales déjà propres)._

## Files Created/Modified
- `src/Chronos/Interop/NativeMethods.cs` - Interop moniteur/DPI/fenêtre/arrière-plan (P/Invoke user32/Shcore + structs).
- `src/Chronos/Services/TopmostGuard.cs` - Ajout de Suspend()/Resume() pour le mode arrière-plan.
- `src/Chronos/Services/IAutostartService.cs` - Interface neutre IsEnabled/Enable/Disable.
- `src/Chronos/Services/AutostartService.cs` - Implémentation .lnk COM late-bound, dossier startup injectable.
- `tests/Chronos.Tests/TopmostGuardTests.cs` - Deux [WpfFact] Suspend/Resume (délégué capturé).
- `tests/Chronos.Tests/AutostartServiceTests.cs` - Quatre [Fact] (dossier temp injecté), dont un Enable() réel via COM.

## Decisions Made
- **Environment.ProcessPath** comme cible du raccourci (single-file-safe) — conforme à CLAUDE.md qui interdit Assembly.Location.
- **Suspend = _timer.Stop(), Resume = _timer.Start()+Reassert()** — pas de toggle Topmost (anti-pattern scintillement du RESEARCH).
- **Dossier startup injecté au constructeur** — permet les tests d'existence/suppression sans toucher le vrai shell:startup ; le test Enable() COM réel écrit lui aussi dans le dossier temp.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Ajout de `using System.IO;` dans AutostartService.cs**
- **Found during:** Task 3 (implémentation GREEN)
- **Issue:** `ImplicitUsings` du projet n'inclut pas `System.IO` (Path/File/Directory introuvables au build) — même contrainte que `ChronosPaths.cs` qui importe explicitement `System.IO`.
- **Fix:** Ajout de `using System.IO;` en tête du fichier (convention établie du projet).
- **Files modified:** src/Chronos/Services/AutostartService.cs
- **Verification:** `dotnet test --filter AutostartServiceTests` → 4/4 verts.
- **Committed in:** `a8a9c8e` (Task 3 GREEN)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Ajustement de compilation mineur aligné sur la convention d'imports existante. Aucun scope creep.

## Issues Encountered
None - les trois tâches ont suivi le plan (Task 2/3 en RED→GREEN sans refactor nécessaire).

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Interop moniteur/DPI/fenêtre/arrière-plan prêt pour `OverlayController` (placement physique multi-écrans, 06-03).
- `TopmostGuard.Suspend/Resume` prêt pour le toggle arrière-plan du menu contextuel (06-04).
- `IAutostartService` prêt pour l'item de menu « Lancer au démarrage » (06-04) et l'enregistrement DI.
- Exécuté en parallèle de 06-01 (zéro chevauchement de fichiers). Garde de pureté verte. Aucun NuGet ajouté, pas de `<UseWindowsForms>`.
- **Note validation :** suite complète `dotnet test` non exécutée ici (contention avec l'exécuteur parallèle 06-01) — validation finale déléguée à l'orchestrateur. Sous-ensembles TopmostGuardTests (3/3), AutostartServiceTests (4/4) et ServicesLayerPurityTests (1/1) verts.

---
*Phase: 06-comportements-overlay-placement-interaction*
*Completed: 2026-07-08*

## Self-Check: PASSED

- Files created/modified: all 6 present on disk.
- Task commits: 8a4fdfe, f8905b4, f1a5200, 3776464, a8a9c8e all found in git history.
