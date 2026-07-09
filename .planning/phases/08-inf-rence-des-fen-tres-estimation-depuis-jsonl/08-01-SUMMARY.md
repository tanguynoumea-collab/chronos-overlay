---
phase: 08-inf-rence-des-fen-tres-estimation-depuis-jsonl
plan: 01
subsystem: services
tags: [dotnet, csharp, pure-functions, time-windows, settings, tdd, xunit]

# Dependency graph
requires:
  - phase: 03-providers
    provides: JsonlEstimationProvider (scanne JSONL, somme tokens) qui consommera l'inférence en 08-02
  - phase: 06-overlay-behaviors
    provides: ChronosSettings + SettingsService (persistance atomique/tolérante), WeeklyAnchor, WeeklyRecalibration
provides:
  - FiveHourWindowInference.InferWindowStart — début pur de la fenêtre 5 h (algorithme « A » verrouillé, EST-01/EST-02)
  - WeeklyWindow.CurrentStart — borne pure de la fenêtre hebdo ancrée ou 7 j glissants (EST-04)
  - ChronosSettings.FiveHourTokenBudget / WeeklyTokenBudget (long?, null par défaut, round-trip prouvé, EST-03/EST-04)
affects: [08-02-enrichissement-provider, 09-calibration-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Classe statique pure NEUTRE (DateTimeOffset/TimeSpan, now en paramètre) — miroir de WeeklyRecalibration, gardée par ServicesLayerPurityTests"
    - "Fenêtre roulante ancrée via floor((now-anchor)/7j) — robuste à la frontière exacte"

key-files:
  created:
    - src/Chronos/Services/FiveHourWindowInference.cs
    - src/Chronos/Services/WeeklyWindow.cs
    - tests/Chronos.Tests/FiveHourWindowInferenceTests.cs
    - tests/Chronos.Tests/WeeklyWindowTests.cs
  modified:
    - src/Chronos/Services/ChronosSettings.cs
    - tests/Chronos.Tests/SettingsServiceTests.cs

key-decisions:
  - "Algorithme « A » verrouillé : activité continue > 5 h ⇒ fenêtre inactive (null), assumé imparfait (raffinement B différé v1.2)"
  - "Trou d'exactement 5 h traité comme borne stricte (>=)"
  - "windowStart hebdo calculé directement par floor (pas NextReset - 7j) pour rester robuste à la frontière exacte"

patterns-established:
  - "Logique pure séparée de l'I/O du provider : testable RED→GREEN sans disque ni horloge réelle"
  - "long? nullable persisté nativement par System.Text.Json (aucune option de sérialisation à changer)"

requirements-completed: [EST-01, EST-02, EST-03, EST-04]

# Metrics
duration: 4 min
completed: 2026-07-09
---

# Phase 8 Plan 01 : Logique pure d'inférence des fenêtres + plafonds de tokens Summary

**Deux classes pures — inférence de la fenêtre 5 h (algorithme « A ») et borne de fenêtre hebdo ancrée — plus deux plafonds de tokens `long?` persistés dans ChronosSettings, le tout prouvé sur les cas limites en TDD.**

## Performance

- **Duration:** 4 min
- **Started:** 2026-07-09T05:18:40Z
- **Completed:** 2026-07-09T05:22:26Z
- **Tasks:** 3 (TDD RED→GREEN chacune)
- **Files modified:** 6 (2 modifiés, 4 créés)

## Accomplishments
- `FiveHourWindowInference.InferWindowStart(tsAsc, now)` : remonte depuis le message le plus récent tant qu'aucun trou ≥ 5 h n'apparaît ; renvoie le début courant ou `null` si expirée/inactive (EST-01/EST-02).
- `WeeklyWindow.CurrentStart(anchor, now)` : fenêtre roulante `[ancre + k·7j]` (k = floor) si ancre, sinon 7 j glissants (EST-04).
- `ChronosSettings.FiveHourTokenBudget` / `WeeklyTokenBudget` (`long?`, null par défaut) persistés et relus sans perte (EST-03/EST-04).
- Suite complète verte : 118 tests (107 baseline + 11 nouveaux), ServicesLayerPurityTests incluse.

## Task Commits

Each task was committed atomically (TDD: test → feat) :

1. **Task 1 : Plafonds de tokens + round-trip** — `6d44c57` (test) → `ae12600` (feat)
2. **Task 2 : FiveHourWindowInference (algorithme « A »)** — `19afd3d` (test) → `0e0cdec` (feat)
3. **Task 3 : WeeklyWindow.CurrentStart** — `4610100` (test) → `fabe8ce` (feat)

**Plan metadata:** _(commit final ci-dessous)_

_Aucune étape REFACTOR nécessaire : le code des classes pures est verbatim de la décision verrouillée (RESEARCH § Pattern 1 / Code Example 3)._

## Files Created/Modified
- `src/Chronos/Services/FiveHourWindowInference.cs` — Classe statique pure : `InferWindowStart` + constante `Window = 5 h`.
- `src/Chronos/Services/WeeklyWindow.cs` — Classe statique pure : `CurrentStart` + constante `Week = 7 j`.
- `src/Chronos/Services/ChronosSettings.cs` — Ajout de `FiveHourTokenBudget` / `WeeklyTokenBudget` (`long?`).
- `tests/Chronos.Tests/FiveHourWindowInferenceTests.cs` — 7 `[Fact]` : vide, unique <5h/≥5h, trou exact 5 h, rafale contiguë, activité continue >5h, contrat de tri.
- `tests/Chronos.Tests/WeeklyWindowTests.cs` — 4 `[Fact]` : sans ancre, ancre 10 j, ancre = now, frontière exacte 7 j.
- `tests/Chronos.Tests/SettingsServiceTests.cs` — Round-trip étendu aux deux plafonds + assertions null par défaut.

## Decisions Made
- **Algorithme « A » verrouillé conservé** : une activité continue > 5 h rend la fenêtre `null` (inactive, fraction = 1 en aval). Sémantiquement imparfait (l'utilisateur est actif mais l'arc s'affiche « plein ») — raffinement « B » (bloc de 5 h depuis le 1er message) consigné comme candidat v1.2, à valider empiriquement (cf. Blocker STATE.md sur la fiabilité de l'inférence).
- **Trou d'exactement 5 h = borne stricte** (`>=`) : un message isolé ≥ 5 h avant le suivant coupe la fenêtre.
- **windowStart hebdo par `floor` direct** (pas `NextReset - 7j`) : robuste à la frontière exacte (ancre = now - 7 j ⇒ start = now).

## Deviations from Plan

None - plan executed exactly as written.

**Total deviations:** 0
**Impact on plan:** Plan exécuté verbatim (code des classes pures fourni dans le RESEARCH, cas limites couverts tels que spécifiés).

## Issues Encountered
None. Les avertissements Git « LF will be replaced by CRLF » sont cosmétiques (fins de ligne Windows) et sans effet sur le build/tests.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Briques pures prêtes pour **08-02** : le provider triera les `(ts, tokens)` collectés, appellera `FiveHourWindowInference.InferWindowStart` puis `WeeklyWindow.CurrentStart`, sommera les tokens des fenêtres bornées, et calculera l'utilization via les deux plafonds (`Math.Max(0, tokens/budget)` si `budget > 0`, sinon `null`).
- Aucun changement provider/UI dans ce plan (séparation logique pure / I/O respectée).
- Blocker à porter en 08-02+ : fiabilité empirique de l'inférence 5 h (Open Question 1, définition A vs B).

## Self-Check: PASSED

All 6 key files present on disk; all 6 task commits (3 TDD pairs) found in git history.

---
*Phase: 08-inf-rence-des-fen-tres-estimation-depuis-jsonl*
*Completed: 2026-07-09*
