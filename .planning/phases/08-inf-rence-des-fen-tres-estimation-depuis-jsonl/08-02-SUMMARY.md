---
phase: 08-inf-rence-des-fen-tres-estimation-depuis-jsonl
plan: 02
subsystem: services
tags: [dotnet, csharp, jsonl, time-windows, settings, utilization, tdd, xunit, cleanup]

# Dependency graph
requires:
  - phase: 08-01
    provides: FiveHourWindowInference.InferWindowStart, WeeklyWindow.CurrentStart, ChronosSettings.FiveHourTokenBudget/WeeklyTokenBudget
  - phase: 03-providers
    provides: JsonlEstimationProvider (scan JSONL, somme tokens), IUsageProvider, CompositeUsageProvider, UsageSnapshot
  - phase: 06-overlay-behaviors
    provides: SettingsService (Load tolérant), WeeklyAnchor
provides:
  - "JsonlEstimationProvider enrichi : fenêtre 5 h inférée [start, now], ResetsAt/Fraction peuplés (EST-01/02), utilization estimée par plafonds (EST-03/04)"
  - "Somme hebdo bornée à la fenêtre ancrée (WeeklyWindow.CurrentStart) ou 7 j glissants"
  - "SettingsService injecté dans le provider (Load() frais à chaque GetAsync — calibration Phase 9 sans redémarrage)"
  - "Contrat IUsageProvider épuré (SnapshotChanged retiré) + UsageSnapshot sans Age (NET-01)"
affects: [09-calibration-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Passe disque unique matérialisant (timestamp, tokens) puis calcul en mémoire (tri + inférence + sommes bornées)"
    - "Lecture fraîche des settings à chaque GetAsync via SettingsService injecté (plafonds/ancre non figés)"
    - "Filtre mtime < 8 j sur les JSONL (append-only) pour éviter de scanner tout l'historique"
    - "Staleness dérivée de SourceCapturedAt côté VM — aucun champ Age matérialisé"

key-files:
  created: []
  modified:
    - src/Chronos/Services/JsonlEstimationProvider.cs
    - src/Chronos/Services/IUsageProvider.cs
    - src/Chronos/Services/ClaudeUsageObjectProvider.cs
    - src/Chronos/Services/CompositeUsageProvider.cs
    - src/Chronos/Models/UsageSnapshot.cs
    - tests/Chronos.Tests/JsonlEstimationProviderTests.cs
    - tests/Chronos.Tests/ClaudeUsageObjectProviderTests.cs
    - tests/Chronos.Tests/CompositeUsageProviderTests.cs
    - tests/Chronos.Tests/Fakes/FakeUsageProvider.cs
    - tests/Chronos.Tests/TestData/sample-inactive.jsonl

key-decisions:
  - "Somme 5 h bornée à la fenêtre inférée [start, now] (pas 5 h glissantes brutes) — ancien filtre now-5h retiré"
  - "Utilization jamais clampée à 1 (>= 1 = gris épuisé déjà géré par WindowState.Exhausted) ; budget <= 0 → null"
  - "SevenDay.ResetsAt laissé null par le provider — rempli par WeeklyRecalibration côté VM (non-régression EST-05)"
  - "Filtre mtime < 8 j sûr pour les fixtures (test now figé au 2026-07-08 < mtime disque réel)"

patterns-established:
  - "Provider = seul lecteur des timestamps → inférence côté provider, jamais dupliquée dans le VM"
  - "Helper de test ProviderFor injecte un SettingsService sur UsageFile temp isolé (pas de settings.json parasite du CWD)"

requirements-completed: [EST-01, EST-02, EST-03, EST-04, EST-05, NET-01]

# Metrics
duration: 6 min
completed: 2026-07-09
---

# Phase 8 Plan 02 : Enrichissement du repli JSONL + nettoyage du contrat Summary

**Le repli JSONL infère désormais la fenêtre 5 h courante (arc retrouve longueur ET couleur si plafond, toujours « estimée »), borne ses sommes aux fenêtres inférée/ancrée, et le contrat IUsageProvider est débarrassé de SnapshotChanged/Age morts — suite complète à 119 tests verte.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-07-09T05:25:00Z
- **Completed:** 2026-07-09T05:31:52Z
- **Tasks:** 2 (Task 1 en TDD RED→GREEN, Task 2 refactor)
- **Files modified:** 10 (9 modifiés, 1 fixture créée)

## Accomplishments
- `JsonlEstimationProvider.GetAsync` refondu : une seule passe disque matérialise `(timestamp, tokens)`, tri global, `InferWindowStart` pilote la fenêtre 5 h `[start, now]` — `ResetsAt = start + 5 h`, `FractionTimeRemaining` calculé (EST-01), fenêtre inactive → arc plein `Fraction = 1`, `tokens = 0` (EST-02).
- Utilization estimée = `tokens / plafond` si budget défini, `null` sinon, sans clamp haut (EST-03/04) ; `SettingsService` injecté et `Load()` frais à chaque refresh (calibration Phase 9 sans redémarrage).
- Somme hebdo bornée à `WeeklyWindow.CurrentStart` (ancrée si `WeeklyAnchor`, sinon 7 j glissants) ; `SevenDay.ResetsAt` reste `null` (rempli par `WeeklyRecalibration` côté VM — non-régression EST-05).
- NET-01 : `IUsageProvider.SnapshotChanged` retiré du contrat et des 3 providers + fakes ; `UsageSnapshot.Age` retiré du modèle et des 3 providers ; `RefreshOrchestrator.SnapshotChanged` (event distinct, vivant) intact.
- Filtre perf : les JSONL dont `mtime < now - 8 j` sont ignorés (append-only → aucun message des fenêtres 5 h/7 j).

## Task Commits

Chaque tâche commitée atomiquement :

1. **Task 1 : Enrichir JsonlEstimationProvider (inférence + sommes bornées + utilization)** — `d03d97c` (test) → `c9baea2` (feat)
2. **Task 2 : NET-01 — retirer SnapshotChanged + UsageSnapshot.Age** — `60e19f1` (refactor)

**Plan metadata:** _(commit final ci-dessous)_

_Aucune étape REFACTOR distincte en Task 1 : le cœur de `GetAsync` et les helpers `BuildFiveHour`/`BuildSevenDay` sont verbatim du RESEARCH § Code Example 1/2/3._

## Files Created/Modified
- `src/Chronos/Services/JsonlEstimationProvider.cs` — Passe unique + inférence 5 h + sommes bornées + utilization par plafonds ; `SettingsService` injecté ; filtre mtime ; `SnapshotChanged`/`Age` retirés ; helper `EstimatedWindow` supprimé.
- `src/Chronos/Services/IUsageProvider.cs` — `SnapshotChanged` retiré du contrat.
- `src/Chronos/Services/ClaudeUsageObjectProvider.cs` — `SnapshotChanged` + affectation `Age` retirés.
- `src/Chronos/Services/CompositeUsageProvider.cs` — `SnapshotChanged` + `Age = p.Age ?? f.Age` retirés.
- `src/Chronos/Models/UsageSnapshot.cs` — Propriété `Age` retirée (staleness dérivée de `SourceCapturedAt`).
- `tests/Chronos.Tests/JsonlEstimationProviderTests.cs` — Assertions 5 h adaptées (ResetsAt=16:30, Fraction=0.9) ; 2 nouveaux `[Fact]` (utilization avec plafond 3100→0.5 ; fenêtre inactive→arc plein) ; `ProviderFor` injecte un `SettingsService` sur UsageFile temp isolé.
- `tests/Chronos.Tests/ClaudeUsageObjectProviderTests.cs` — Assertion `Age` retirée, test renommé `_et_capturedAt`.
- `tests/Chronos.Tests/CompositeUsageProviderTests.cs` — Test `SnapshotChanged_emis...` supprimé + event/pragma du fake interne.
- `tests/Chronos.Tests/Fakes/FakeUsageProvider.cs` — Event `SnapshotChanged` + son `Invoke` retirés.
- `tests/Chronos.Tests/TestData/sample-inactive.jsonl` — Fixture : message unique à 06:00 (6 h avant now) → fenêtre inactive.

## Decisions Made
- **Somme 5 h = fenêtre inférée `[start, now]`** (l'ancien `if (when >= now-5h)` glissant brut est retiré) : cohérence longueur/couleur de l'arc avec le reset inféré.
- **Pas de clamp haut sur l'utilization** : `WindowState.Exhausted (>= 1.0)` gère déjà le gris épuisé ; `budget <= 0` → `null` (garde anti-division).
- **`SevenDay.ResetsAt` laissé `null` par le provider** : le VM le remplit via `WeeklyRecalibration` (ordre inchangé, EST-05 non régressé).
- **Filtre mtime < 8 j** adopté (recommandation RESEARCH) : sûr ici car le `now` de test est figé au 2026-07-08, antérieur au mtime disque réel des fixtures → seuil `now-8j` toujours franchi.

## Deviations from Plan

None - plan executed exactly as written.

**Total deviations:** 0
**Impact on plan:** Plan exécuté verbatim (structure `GetAsync` et helpers fournis dans le RESEARCH ; warnings du plan-checker appliqués : `ProviderFor` sur UsageFile temp isolé, feedback filtré `JsonlEstimation` avant la suite complète).

## Issues Encountered
None. Les avertissements Git « LF will be replaced by CRLF » sont cosmétiques (fins de ligne Windows), sans effet sur build/tests.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Le repli JSONL est désormais réellement utile en app bureau pure : les arcs 5 h/hebdo retrouvent longueur et (si plafond) couleur, tout en restant `Estimated`. **Prêt pour la Phase 9** (UI de calibration CAL-01..03 qui peuplera `FiveHourTokenBudget`/`WeeklyTokenBudget`, + NET-02).
- Contrat `IUsageProvider` épuré : dette DT-1 (SnapshotChanged) et DT-2 (Age) soldées.
- Blocker à porter en Phase 9+ : fiabilité empirique de l'inférence 5 h (algorithme « A » verrouillé ; raffinement « B » candidat v1.2 si l'affichage « plein pendant le travail » gêne à l'usage).

## Self-Check: PASSED

Tous les fichiers clés présents sur disque ; les 3 commits de tâche (d03d97c/c9baea2/60e19f1) trouvés dans l'historique git. Acceptance NET-01 vérifiée : `SnapshotChanged` absent du contrat `IUsageProvider`, propriété `Age` absente de `UsageSnapshot` (seule mention résiduelle = commentaire). Suite complète : 119 tests verts.

---
*Phase: 08-inf-rence-des-fen-tres-estimation-depuis-jsonl*
*Completed: 2026-07-09*
