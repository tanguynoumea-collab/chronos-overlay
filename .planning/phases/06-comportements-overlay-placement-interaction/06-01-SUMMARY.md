---
phase: 06-comportements-overlay-placement-interaction
plan: 01
subsystem: infra
tags: [placement, corner-snap, settings-json, persistence, weekly-recalibration, pure-logic, xunit]

# Dependency graph
requires:
  - phase: 03
    provides: WindowState/SourceReliability immuables neutres (consommés par WeeklyRecalibration)
  - phase: 01
    provides: ChronosPaths injectable, garde de pureté ServicesLayerPurityTests
provides:
  - Chronos.Placement (RectD, OverlayCorner, CornerSnap) — logique pure d'accroche aux coins (FEN-03)
  - ChronosSettings + SettingsService — persistance atomique/tolérante de settings.json (FEN-07)
  - ChronosPaths.SettingsFile — chemin %APPDATA%/Chronos/settings.json dérivé
  - WeeklyRecalibration — recalibrage hebdo pur, badge « estimée » conservé (ROB-03)
affects: [06-03 OverlayController placement physique, 06-04 menu + dialogue recalibrage]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Namespace neutre Chronos.Placement hors gate de pureté, zéro type WPF"
    - "Persistance atomique temp + File.Move(overwrite) miroir du pont Node"
    - "Lecture JSON tolérante (défauts sans exception) alignée sur ClaudeUsageObjectProvider"

key-files:
  created:
    - src/Chronos/Placement/RectD.cs
    - src/Chronos/Placement/OverlayCorner.cs
    - src/Chronos/Placement/CornerSnap.cs
    - src/Chronos/Services/ChronosSettings.cs
    - src/Chronos/Services/SettingsService.cs
    - src/Chronos/Services/WeeklyRecalibration.cs
    - tests/Chronos.Tests/CornerSnapTests.cs
    - tests/Chronos.Tests/SettingsServiceTests.cs
    - tests/Chronos.Tests/WeeklyRecalibrationTests.cs
  modified:
    - src/Chronos/Services/ChronosPaths.cs

key-decisions:
  - "Persistance = coin + device name comme vérité ; X/Y purement indicatifs (Open Question 1)"
  - "RefreshIntervalSeconds persisté sans UI, appliqué au démarrage en 06-03 (Open Question 3)"
  - "Recalibrage hebdo au repli seulement ; reste Estimated (honnêteté Core Value, Pitfall 7)"

patterns-established:
  - "Logique de placement en unités agnostiques (double) via RectD, testable sans écran/STA"
  - "ChronosPaths étendu par propriété calculée (ctor positionnel inchangé, tests temp valides)"
  - "OverlayCorner sérialisé en texte (JsonStringEnumConverter) pour robustesse au réordonnancement"

requirements-completed: [FEN-03, FEN-07, ROB-03]

# Metrics
duration: 4min
completed: 2026-07-08
---

# Phase 6 Plan 01 : Fondations neutres placement + persistance + recalibrage Summary

**Logique pure d'accroche aux coins (RectD/CornerSnap), persistance atomique/tolérante de settings.json (coin+device=vérité), et recalibrage hebdo pur conservant le badge « estimée » — 23 nouveaux tests, zéro type WPF.**

## Performance

- **Duration:** 4 min
- **Started:** 2026-07-08T19:51:28Z
- **Completed:** 2026-07-08T19:55:14Z
- **Tasks:** 3
- **Files modified:** 10 (9 créés, 1 étendu)

## Accomplishments
- **CornerSnap (FEN-03)** : `NearestCorner` (coin le plus proche après drag), `ClassifyCorner` (quadrant), `CornerToTopLeft` (restauration d'un coin imposé) — trois fonctions pures sur `RectD` neutre, marge respectée exactement.
- **SettingsService (FEN-07)** : round-trip settings.json, écriture atomique (temp + `File.Move`), lecture tolérante (fichier absent/corrompu → défauts sans exception), création du dossier ; `ChronosPaths.SettingsFile` dérivé sans casser le ctor positionnel.
- **WeeklyRecalibration (ROB-03)** : garde Exact+ResetsAt (chiffres exacts priment), synthèse d'un ResetsAt strictement futur aligné (ancre + n×7j) sur le repli, provenance restant `Estimated` (badge « estimée » conservé).
- **Garde de pureté** `ServicesLayerPurityTests` restée verte : aucun type WPF dans les signatures Services/Models.

## Task Commits

Each task was committed atomically:

1. **Task 1: CornerSnap — logique pure de placement aux coins (FEN-03)** - `817b04a` (feat)
2. **Task 2: ChronosSettings + SettingsService (persistance atomique/tolérante, FEN-07)** - `2a25124` (feat)
3. **Task 3: WeeklyRecalibration — recalibrage hebdo pur (ROB-03)** - `c7cb0bf` (feat)

**Plan metadata:** _(commit docs final ci-dessous)_

_Note : TDD appliqué de façon pragmatique pour du C# compilé — implémentation (verbatim du plan) + tests écrits ensemble puis vérifiés, commit unique `feat` par tâche pour garder chaque commit compilable._

## Files Created/Modified
- `src/Chronos/Placement/RectD.cs` - Rectangle neutre (double) avec Right/Bottom/CenterX/CenterY
- `src/Chronos/Placement/OverlayCorner.cs` - Enum des 4 coins d'accroche
- `src/Chronos/Placement/CornerSnap.cs` - NearestCorner + ClassifyCorner + CornerToTopLeft purs
- `src/Chronos/Services/ChronosPaths.cs` - Ajout propriété calculée `SettingsFile`
- `src/Chronos/Services/ChronosSettings.cs` - Schéma settings.json (coin+device=vérité, X/Y indicatifs, RefreshIntervalSeconds, WeeklyAnchor)
- `src/Chronos/Services/SettingsService.cs` - Load tolérant + Save atomique
- `src/Chronos/Services/WeeklyRecalibration.cs` - Recalibrage hebdo pur (repli seulement)
- `tests/Chronos.Tests/CornerSnapTests.cs` - 13 tests (4 quadrants × 3 fonctions + marge)
- `tests/Chronos.Tests/SettingsServiceTests.cs` - 5 tests (round-trip, absent, corrompu, atomique, création dossier)
- `tests/Chronos.Tests/WeeklyRecalibrationTests.cs` - 5 tests (garde Exact, repli/ancre, badge estimée, cycle futur)

## Decisions Made
- **Coin + device = vérité de placement** ; X/Y persistés uniquement en indicatif diagnostic (résout Open Question 1).
- **RefreshIntervalSeconds** dans le schéma mais sans UI ; application au démarrage câblée en 06-03 (résout Open Question 3).
- **Recalibrage limité au repli** ; la provenance reste `Estimated` pour ne jamais présenter une estimation comme exacte (Core Value / Pitfall 7).
- **OverlayCorner sérialisé en texte** via `JsonStringEnumConverter` pour rester robuste au réordonnancement de l'enum.

## Deviations from Plan

None - plan executed exactly as written. Une simplification mineure d'assertion de test (une ligne `Assert.Same` tautologique remplacée par `Assert.Same(exact, result)` avant tout commit) — non structurelle, aucun impact sur le périmètre.

## Issues Encountered
- **Course de build transitoire avec l'exécuteur parallèle 06-02** : lors du premier `dotnet test` de la Task 3, `src/Chronos/Services/AutostartService.cs` (fichier de 06-02, hors de mon périmètre) était momentanément incomplet (`using System.IO;` manquant). Conformément à la frontière de périmètre, je n'ai PAS modifié ce fichier ; j'ai attendu et re-tenté le build, que l'exécuteur parallèle avait déjà corrigé. Build ensuite propre.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Briques pures prêtes pour **06-03** (OverlayController : `CornerToTopLeft`/`NearestCorner` pour poser physiquement la fenêtre, `SettingsService` pour restaurer coin+device, `RefreshIntervalSeconds` à appliquer au démarrage).
- Prêtes pour **06-04** (menu + dialogue de recalibrage consommant `WeeklyRecalibration.Apply` + `ChronosSettings.WeeklyAnchor`).
- Suite complète : 97 tests verts (68 existants + 23 nouveaux + tests concurrents 06-02), garde de pureté verte, aucun paquet NuGet ajouté.

## Self-Check: PASSED

- 9 fichiers créés + 1 étendu : tous présents sur disque.
- 3 commits de tâche (`817b04a`, `2a25124`, `c7cb0bf`) présents dans l'historique git.

---
*Phase: 06-comportements-overlay-placement-interaction*
*Completed: 2026-07-08*
