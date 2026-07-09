---
phase: 09-calibration-des-plafonds-surfa-age
plan: 01
subsystem: calibration
tags: [budget, calibration, tokens, formatter, settings, services-purity]

# Dependency graph
requires:
  - phase: 08-estimation-jsonl
    provides: "JsonlEstimationProvider portant toujours EstimatedTokens ; SettingsService injecté (Load frais à chaque GetAsync) ; RefreshOrchestrator.SnapshotChanged conservé (DT-1)"
  - phase: 03-mod-les-pipeline-de-donn-es
    provides: "UsageSnapshot/WindowState immuables, SourceReliability (Exact/Estimated/Unavailable), IUsageProvider, ChronosSettings/SettingsService atomique"
provides:
  - "Enum neutre BudgetSource { None, Manual, Auto } + 4 champs de source/timestamp sur ChronosSettings"
  - "BudgetCalibration pur : Deduce(util, tokens) + ApplyAuto (priorité Manual > Auto/None)"
  - "TokenFormatter fr abrégé (M/k, virgule fr, ,0 supprimé)"
  - "BudgetAutoCalibrator neutre : calibration opportuniste inerte sans fenêtre Exact"
  - "Régression CAL-03 : un plafond défini laisse la fenêtre Estimated"
affects: [09-02, 09-03, ui-calibration, menu-contextuel]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Logique de calibration isolée en couche Services/Text pure (garde ServicesLayerPurity) — les plans UI ne font que du câblage"
    - "Service neutre IDisposable écoutant un event (abonnement ctor / désabonnement Dispose) sans type WPF"
    - "GAP-1 : Load disque frais avant ApplyAuto → Save, écriture seulement si un plafond change (ReferenceEquals)"

key-files:
  created:
    - src/Chronos/Services/BudgetSource.cs
    - src/Chronos/Services/BudgetCalibration.cs
    - src/Chronos/Services/BudgetAutoCalibrator.cs
    - src/Chronos/Text/TokenFormatter.cs
    - tests/Chronos.Tests/BudgetCalibrationTests.cs
    - tests/Chronos.Tests/TokenFormatterTests.cs
    - tests/Chronos.Tests/BudgetAutoCalibratorTests.cs
  modified:
    - src/Chronos/Services/ChronosSettings.cs
    - tests/Chronos.Tests/JsonlEstimationProviderTests.cs

key-decisions:
  - "tokenSource du calibrateur = JsonlEstimationProvider concret (porte toujours EstimatedTokens), PAS le composite (qui perd les tokens sur fenêtre Exact)"
  - "Priorité Manual > Auto/None : une saisie manuelle n'est jamais écrasée par la calibration auto (ApplyAuto renvoie la même référence)"
  - "Calibration best-effort et inerte : aucun accès disque tant qu'aucune fenêtre Exact avec util>0 n'apparaît"

patterns-established:
  - "Formateur pur déterministe (InvariantCulture puis '.'→',') — même contrat neutre que CountdownFormatter"
  - "TDD RED→GREEN sur logique pure : stub → tests → implémentation"

requirements-completed: [CAL-02, CAL-03, NET-02]

# Metrics
duration: 6min
completed: 2026-07-09
---

# Phase 9 Plan 01 : Fondations neutres de calibration Summary

**Logique pure de calibration des plafonds (déduction tokens/util + priorité manuel/auto), formateur fr abrégé des tokens, et service BudgetAutoCalibrator neutre inerte sans source Exact — le tout testé, sans UI ni câblage DI.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-07-09T05:59:46Z
- **Completed:** 2026-07-09T06:05:28Z
- **Tasks:** 3
- **Files modified:** 9 (7 créés, 2 modifiés)

## Accomplishments
- Métadonnée neutre `BudgetSource { None, Manual, Auto }` + 4 champs source/timestamp sur `ChronosSettings` (sérialisés en texte via le JsonStringEnumConverter existant).
- `BudgetCalibration` pur : `Deduce(util, tokens)` (arrondi away-from-zero, null si mesure inexploitable) et `ApplyAuto` appliquant la règle de priorité Manual > Auto/None sans contaminer l'autre fenêtre.
- `TokenFormatter.Format` fr abrégé (M/k, 1 décimale, virgule française, « ,0 » supprimé, cas brut < 1000, négatif borné à 0) — déterministe.
- `BudgetAutoCalibrator` : service NEUTRE écoutant `RefreshOrchestrator.SnapshotChanged`, calibrant opportunément (Load frais → ApplyAuto → Save conditionnel), INERTE tant qu'aucune fenêtre Exact.
- Régression CAL-03 ajoutée : un plafond défini laisse la fenêtre 5 h `Estimated` (jamais `Exact`) tout en peuplant l'utilization.

## Task Commits

1. **Task 1 : Métadonnée source + logique pure de calibration** — `e6746bb` (test RED) → `a5dbe97` (feat GREEN)
2. **Task 2 : Formateur fr abrégé + régression CAL-03** — `6247643` (test RED) → `5c2f7ca` (feat GREEN)
3. **Task 3 : BudgetAutoCalibrator neutre** — `a83b436` (feat + tests)

_Tasks 1 et 2 en TDD (test → feat). Task 3 auto (service + tests dans un même commit)._

## Files Created/Modified
- `src/Chronos/Services/BudgetSource.cs` — enum neutre de provenance des plafonds.
- `src/Chronos/Services/ChronosSettings.cs` — +4 champs (FiveHour/Weekly BudgetSource + CalibratedAt).
- `src/Chronos/Services/BudgetCalibration.cs` — fonctions pures Deduce + ApplyAuto.
- `src/Chronos/Text/TokenFormatter.cs` — formateur fr abrégé.
- `src/Chronos/Services/BudgetAutoCalibrator.cs` — service neutre de calibration opportuniste.
- `tests/Chronos.Tests/BudgetCalibrationTests.cs` — 15 tests (Deduce bornes + priorité manuel/auto + non-contamination).
- `tests/Chronos.Tests/TokenFormatterTests.cs` — 6 tests d'égalité de chaîne exacte.
- `tests/Chronos.Tests/BudgetAutoCalibratorTests.cs` — 3 tests (déduction/persistance Auto, Manual préservé, inertie).
- `tests/Chronos.Tests/JsonlEstimationProviderTests.cs` — +1 régression CAL-03.

## Decisions Made
- **tokenSource = JsonlEstimationProvider concret** : c'est la seule source qui porte toujours `EstimatedTokens` ; le composite les perd sur une fenêtre Exact. Verrouillé dans le plan, appliqué tel quel.
- **Priorité Manual > Auto/None** : `ApplyAuto` renvoie la référence inchangée si la source est Manual — une saisie utilisateur n'est jamais écrasée.
- **Calibration inerte et best-effort** : aucun `GetAsync`/`Save` tant qu'aucune fenêtre Exact avec util>0 ; toute exception du handler est avalée pour ne jamais faire tomber le pipeline de refresh.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Fondations neutres prêtes : les plans 09-02 (UI « Calibrer les plafonds… ») et 09-03 n'ont plus qu'à câbler `BudgetAutoCalibrator` en DI et exposer `TokenFormatter`/`BudgetCalibration` à l'UI.
- `ServicesLayerPurityTests` reste verte : `BudgetCalibration` et `BudgetAutoCalibrator` n'exposent aucun type WPF.
- Suite complète : 144 tests verts (119 existants + 25 nouveaux), build 0 warning/0 erreur.

## Self-Check: PASSED

All 7 created files present on disk; all 5 task commits present in git history.

---
*Phase: 09-calibration-des-plafonds-surfa-age*
*Completed: 2026-07-09*
