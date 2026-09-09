---
phase: 16-fondations-du-delta-persistance-d-molition-des-plafonds
verified: 2026-09-09T00:00:00Z
status: passed
score: 5/5 must-haves verified
---

# Phase 16 : Fondations du delta — persistance & démolition des plafonds — Verification Report

**Phase Goal:** Chronos dispose d'un instant T de référence persistant (le dernier relevé exact, sur disque
avec son horodatage, rechargé au démarrage) et d'une source de delta bornée (les transcripts JSONL
répondent à « activité depuis T ? » et « tokens depuis T ? »), le sous-système de plafonds ayant
entièrement disparu du code, des réglages et du menu.
**Verified:** 2026-09-09
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Le chiffre survit au redémarrage (EXA-01) | ✓ VERIFIED | `LastExactStore.Save`/`Load` (atomic write, tolerant read), `LastExactUsageProvider` decorator wired in head of `IUsageProvider` chain in `App.xaml.cs`. `Reliability` forced to `Exact` on reconstruction (never read from disk), `FractionTimeRemaining` recalculated via `WindowState.FractionRemaining`, never deserialized. `captured_at` stored per-window (`five_hour`/`seven_day` keys). 13+9=22 dedicated tests green. |
| 2 | Deux questions bornées, aucun pourcentage (DEL-01, DEL-02) | ✓ VERIFIED | `ITranscriptActivitySource.ReadAsync()` does one disk pass; `TranscriptActivityLog.Since(t)` is pure (no I/O), callable N times, exposes `HasActivity`/`Tokens`/`LastActivityAt`. `Horizon = Now - 8j` and `Covers(since)` exposed and tested (`Horizon_borne_a_huit_jours`). `TranscriptActivityProvider` does NOT implement `IUsageProvider` — enforced by reflexive test `Le_provider_de_transcripts_n_est_pas_un_IUsageProvider`. |
| 3 | Plus une trace de plafond (DEL-05) | ✓ VERIFIED | `grep -rn "Budget" src/Chronos --include=*.cs --include=*.xaml` → 0 results (re-verified live). No `Plafonds…` button in `SettingsWindow.xaml`. `MainViewModel` ctor has 11 params (no `IBudgetPrompt`). No DI registration of any Budget* type in `App.xaml.cs`. Reflexive guard `Aucun_type_de_plafond_ne_subsiste_dans_l_assembly` present in `ServicesLayerPurityTests.cs`, verified green (2/2). |
| 4 | Réglages migrés sans casse (DEL-06) | ✓ VERIFIED | `ChronosSettings` has exactly 18 properties (counted). Fixture `tests/Chronos.Tests/TestData/settings-legacy-plafonds.json` is a literal copy of real production `settings.json` (6 obsolete fields + 18 survivors). 4 dedicated tests (`Reglages_avec_anciens_plafonds_s_ouvrent_sans_erreur_et_conservent_les_18_preferences`, `Les_six_champs_obsoletes_disparaissent_au_premier_Save`, `Champ_obsolete_de_type_incoherent_est_ignore`, `Valeur_d_enum_supprimee_invalide_est_ignoree`) all green. No `SettingsMigrator`/`SettingsUpgrader`/`Migrate` exists anywhere (`grep` confirms 0 results); `SettingsService.cs` untouched by this phase. |
| 5 | Aucune régression de garde | ✓ VERIFIED | Live re-run: `ServicesLayerPurityTests` 2/2 green, `CompositionRootTests` 3/3 green, full suite 419/419 green on 3 consecutive runs (including one cold run after `dotnet clean`) — no flakiness reproduced. |

**Score:** 5/5 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Chronos/Services/LastExactStore.cs` | Atomic persistence, per-window, versioned schema | ✓ VERIFIED | 178 lines, `Save`/`Load`/`Path` exported, `File.Move(overwrite:true)` atomic write, `Reliability`/`FractionTimeRemaining` never read from disk (forced/recalculated), reset-passed windows rejected in `Reconstruire`. |
| `src/Chronos/Services/LastExactUsageProvider.cs` | Decorator: sole writer + hole-patching | ✓ VERIFIED | 84 lines, delegates to inner, writes only `Exact` windows, patches only `Unavailable` windows, never overwrites a live response, `try/catch` around Save/Load never surfaces exceptions. |
| `src/Chronos/Services/ChronosPaths.cs` | Computed `LastExactFile` property | ✓ VERIFIED | Present, positional ctor unchanged (`(UsageFile, ProjectsRoot)`), colocated with `usage.json`. |
| `src/Chronos/Models/WindowState.cs` | Per-window `CapturedAt` | ✓ VERIFIED | Additive nullable field present. |
| `src/Chronos/Services/ITranscriptActivitySource.cs` | Delta contract, no `IUsageProvider` inheritance | ✓ VERIFIED | `TranscriptActivity`, `TranscriptActivityLog` (pure, `Now`/`Horizon`/`Covers`/`Since`), `ITranscriptActivitySource` interface — none inherit `IUsageProvider`. |
| `src/Chronos/Services/TranscriptActivityProvider.cs` | Disk scan, no utilization/budget production | ✓ VERIFIED | Renamed via `git mv` from `JsonlEstimationProvider.cs` (history preserved, 4 commits via `--follow`). No `IUsageProvider`, `UsageSnapshot`, `Utilization`, `TokenBudget`, `SettingsService` references. Disk-scan guards (FileShare.ReadWrite, structured assistant detection, cache token sum, future-timestamp filter, subagents recursion, 8-day mtime filter) preserved verbatim. |
| `src/Chronos/Services/ChronosSettings.cs` | 18 properties, no Budget* fields | ✓ VERIFIED | Counted 18 properties exactly matching the expected list; XML-doc documents the DEL-05/DEL-06 rationale. |
| `src/Chronos/Services/BudgetSource.cs` | Deleted | ✓ VERIFIED | File absent (`ls` fails). |
| `tests/Chronos.Tests/TestData/settings-legacy-plafonds.json` | Frozen real production fixture | ✓ VERIFIED | Present, contains all 6 obsolete fields + 18 survivors, literal (not code-generated). |
| `src/Chronos/Services/CompositeUsageProvider.cs` | INTACT (untouched by phase 16) | ✓ VERIFIED | `git log --oneline` shows last touching commit is `efff91f` (pre-phase-16, phase 11); none of the phase 16 commits appear. |
| `src/Chronos/Services/FiveHourWindowInference.cs`, `WeeklyWindow.cs` | Conserved as documented orphans | ✓ VERIFIED | Both files present, both carry `<remarks>ORPHELIN ASSUMÉ depuis la phase 16` marker. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `App.xaml.cs` (ConfigureServices) | `LastExactUsageProvider` | `AddSingleton<IUsageProvider>(sp => new LastExactUsageProvider(...))` head of chain | ✓ WIRED | Confirmed present at line ~298, wraps a 2-level `CompositeUsageProvider` nesting (OAuth Chronos → Gated OAuth → statusLine bridge). |
| `App.xaml.cs` | `ITranscriptActivitySource` | `AddSingleton<ITranscriptActivitySource>(sp => new TranscriptActivityProvider(...))`, outside the usage chain | ✓ WIRED | Confirmed present, registered separately from `IUsageProvider`. |
| `tests/Chronos.Tests/CompositionRootTests.cs` | `LastExactUsageProvider` | Guard reproduces new wiring, asserts `IsType<LastExactUsageProvider>` | ✓ WIRED | Live-verified: assertion present, test green. Anti-accident guard confirms `LastExactStore.Path` starts with temp dir (never real `%APPDATA%`). |
| `LastExactUsageProvider` | `LastExactStore` | `_store.Save`/`_store.Load` | ✓ WIRED | Confirmed in source, wrapped in try/catch. |
| `LastExactStore` | `WindowState.FractionRemaining` | Recalculation at load, never persisted | ✓ WIRED | Confirmed: `WindowState.FractionRemaining(resets, now, longueur)` called in `Reconstruire`, JSON never carries a fraction field. |

### Data-Flow Trace (Level 4)

Not applicable in the strict WPF-rendering sense — Phase 16 delivers backend services with no new UI bindings (the visible-ring consequences — grey weekly ring, disappearance of "≈ N tokens" — are documented and expected side effects of removing the estimation provider, not new UI work of this phase). The wiring trace above (App.xaml.cs → LastExactUsageProvider → LastExactStore → disk) constitutes the relevant data-flow for this phase: confirmed non-hollow — `Save`/`Load` perform real atomic file I/O against `ChronosPaths.LastExactFile`, not static/empty stubs.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full build compiles | `dotnet build Chronos.sln -v q --nologo` | 0 errors, 0 warnings | ✓ PASS |
| Full test suite green (run 1, warm) | `dotnet test Chronos.sln -v q --nologo` | 419/419, 0 failures | ✓ PASS |
| Full test suite green (run 2, warm) | `dotnet test Chronos.sln -v q --nologo` | 419/419, 0 failures | ✓ PASS |
| Full test suite green (run 3, cold after `dotnet clean`) | `dotnet test Chronos.sln -v q --nologo` | 419/419, 0 failures (2 pre-existing unrelated xUnit2031 warnings) | ✓ PASS |
| `SettingsServiceTests` (DEL-06 proof) | `--filter "FullyQualifiedName~SettingsServiceTests"` | 9/9 | ✓ PASS |
| `ServicesLayerPurityTests` (DEL-05 non-return guard) | `--filter "FullyQualifiedName~ServicesLayerPurityTests"` | 2/2 | ✓ PASS |
| `CompositionRootTests` (DI graph + anti-accident guard) | `--filter "FullyQualifiedName~CompositionRootTests"` | 3/3 | ✓ PASS |
| No test pollutes real `%APPDATA%\Chronos\last-exact.json` | `ls "$APPDATA/Chronos/last-exact.json"` | File absent | ✓ PASS |
| `CompositeUsageProvider.cs` untouched by phase 16 | `git log --oneline -- .../CompositeUsageProvider.cs` | Last commit `efff91f` (phase 11, pre-dates phase 16) | ✓ PASS |
| Zero `Budget` references in `src/Chronos` | `grep -rn "Budget" src/Chronos --include=*.cs --include=*.xaml` | 0 results | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|--------------|--------|----------|
| EXA-01 | 16-02, 16-03 | Dernier relevé exact persisté sur disque avec horodatage, rechargé au démarrage | ✓ SATISFIED | `LastExactStore` + `LastExactUsageProvider` created and wired into DI head of chain. |
| DEL-01 | 16-03 | Transcripts répondent à « activité depuis T ? » sans pourcentage | ✓ SATISFIED | `TranscriptActivityLog.Since(t).HasActivity`, tested. |
| DEL-02 | 16-03 | Transcripts fournissent la somme de tokens depuis T | ✓ SATISFIED | `TranscriptActivityLog.Since(t).Tokens`, tested. |
| DEL-05 | 16-01, 16-04 | Sous-système de plafonds disparu du code/réglages/menu | ✓ SATISFIED | Confirmed by live grep (0 Budget occurrences), reflexive guard test, and UI inspection (no "Plafonds…" button). Correctly noted by 16-01 as NOT yet complete (BudgetSource.cs survived) until 16-04 truly closed it — consistent with REQUIREMENTS.md now marking it Complete under Phase 16. |
| DEL-06 | 16-04 | Réglages existants migrés sans casse | ✓ SATISFIED | Frozen real-production fixture test proves 18 preferences survive; no migrator code written (confirmed absent). |

No orphaned requirements found: REQUIREMENTS.md maps exactly EXA-01, DEL-01, DEL-02, DEL-05, DEL-06 to Phase 16, and all five appear in the plans' `requirements:` frontmatter (16-01: DEL-05; 16-02: EXA-01; 16-03: DEL-01, DEL-02, EXA-01; 16-04: DEL-05, DEL-06).

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | No TODO/FIXME/placeholder/stub patterns found in any file created or modified by this phase | — | None |

No blockers found. Two pre-existing, unrelated `xUnit2031` warnings persist in `DesktopUiaSessionSourceTests.cs` (lines 345, 364) — documented as out-of-scope in `deferred-items.md`, confirmed still present and harmless (analyzer warning only, not a test failure).

### Human Verification Required

### 1. Visual check of the "RÉGLAGES" settings panel gap

**Test:** Open Chronos settings window (right-click tray/overlay → réglages), inspect the `UniformGrid` row with "Recalibrer hebdo…", "Source terminal", "Diagnostic…" buttons.
**Expected:** Confirm there is a visually empty bottom-right cell (3 buttons in a 2-column grid) and decide whether to accept this until Phase 20 or request an earlier fix.
**Why human:** This is a deliberately deferred design decision (documented in `16-VALIDATION.md` and `16-04-SUMMARY.md`), not a functional gap — visual judgment call, not programmatically verifiable, and explicitly punted to Phase 20 (EXA-06) by the phase's own scope decision.

### 2. Visual confirmation of expected ring changes

**Test:** Launch Chronos and observe the overlay.
**Expected:** Weekly ring is now grey (no more colored `Utilization` from token/budget division), "≈ N tokens" text has disappeared, five-hour ring is unchanged (still `Exact`), no "données indisponibles" banner appears.
**Why human:** Visual/runtime behavior on a live overlay; documented as an expected, non-regression consequence of DEL-01/DEL-02/DEL-05 in the SUMMARY and VALIDATION files, but worth a human glance to confirm it matches reality on this machine.

### Gaps Summary

No gaps found. All 5 ROADMAP success criteria are verified against actual code (not just SUMMARY claims):

1. Persistence layer (`LastExactStore`/`LastExactUsageProvider`) exists, is substantive, is wired at the head of the `IUsageProvider` chain, and correctly avoids persisting derived/recalculable fields (`Reliability`, `FractionTimeRemaining`).
2. The delta contract (`ITranscriptActivitySource`/`TranscriptActivityLog`/`TranscriptActivityProvider`) is pure where it should be pure, bounded by an exposed `Horizon`, and structurally prevented (by type design + reflexive test) from producing a percentage again.
3. The budget subsystem is completely gone — verified live via `grep`, DI inspection, ctor arity, and a permanent reflexive guard test that would fail on any reintroduction of a `Budget*` type.
4. Settings migration works with zero migration code, proven against a byte-for-byte copy of the real production `settings.json`, not a synthetic fixture.
5. All permanent guard tests (`ServicesLayerPurityTests`, `CompositionRootTests`) and the full suite (419 tests) are green across 3 consecutive runs, including one cold run, with no flakiness reproduced — the previously diagnosed BAML race condition fix (`XamlWpfCollection` with `DisableParallelization`) held.

The two known deferred items (settings-grid visual gap, expected ring-color changes) are correctly scoped as non-gaps per the phase's own explicit decisions and are routed to human verification / Phase 20, not flagged as blocking.

---

*Verified: 2026-09-09*
*Verifier: Claude (gsd-verifier)*
