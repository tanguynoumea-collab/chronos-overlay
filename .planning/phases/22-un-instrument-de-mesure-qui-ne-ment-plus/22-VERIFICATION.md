---
phase: 22-un-instrument-de-mesure-qui-ne-ment-plus
verified: 2026-09-12T16:24:08Z
status: human_needed
score: 4/4 must-haves verified (automated) — 1 item requires in-vivo human confirmation
human_verification:
  - test: "Ouvrir le widget de sessions, puis clic droit → Diagnostic… au même instant, et comparer LIGNE À LIGNE la section « Sessions AFFICHÉES par le widget » du rapport avec ce qu'affiche réellement le widget (mêmes projets, mêmes états, mêmes âges, même ordre)."
    expected: "Aucun écart entre les deux : le rapport doit être une transcription textuelle exacte de l'écran, au même instant."
    why_human: "Nécessite un lancement réel de l'overlay (Chronos-v3.0.2.exe, pid 119412, qui ne doit ni être lancé ni tué par cette vérification) et une comparaison visuelle live ; aucun test automatisé ne peut se substituer à l'observation de l'écran réel de l'utilisateur."
  - test: "Sur la même machine, vérifier que la session e465420e (PROJET ADVANCED SHEET) apparaît dans la section « Sessions MASQUÉES par un filtre » du rapport avec la mention « masquée par treated.json »."
    expected: "La ligne existe, nomme l'identifiant court, le projet, l'état « à toi », l'âge, et le fichier masquant."
    why_human: "Dépend de l'état réel du magasin treated.json de la machine de l'utilisateur au moment du test, qui peut avoir changé depuis le relevé du 2026-09-12."
  - test: "Dans la section « Fichiers d'état » du rapport, vérifier que les lignes listées sont des sessions récentes ou en attente (pas des entrées de plusieurs semaines) et que la ligne « … et N autre(s) non listé(s) » apparaît si le dossier contient plus de 8 fichiers."
    expected: "~46 fichiers non listés sur 54 attendus sur cette machine ; aucune entrée ancienne en tête sauf si elle est en attente."
    why_human: "Dépend du contenu réel du dossier %APPDATA%\\Chronos\\sessions au moment du test, qui évolue avec l'usage."
---

# Phase 22 : Un instrument de mesure qui ne ment plus — Verification Report

**Phase Goal:** Ce que le diagnostic rapporte est exactement ce que le widget affiche — même moniteur, mêmes
filtres, même instant — et il liste les sessions qui comptent au lieu des huit premières par ordre
alphabétique.

**Verified:** 2026-09-12T16:24:08Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (4 critères de succès du ROADMAP)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Diagnostic et widget disent la même chose (partage d'instance) | VERIFIED (automated) / pending human line-by-line confirmation | `App.xaml.cs` L226-228 registers `SessionMonitor` as `AddSingleton`; both `Views.SessionsController` (L240) and `DiagnosticService` (L381) resolve `sp.GetRequiredService<SessionMonitor>()` — same singleton. `DiagnosticService.cs` contains **0** occurrences of `new SessionMonitor` (verified by grep). `SessionMonitor.Read(now) => Inspecter(now).Visibles` is a pure one-line delegation (verified by reading `SessionMonitor.cs` L62); only one `archived.Contains` and one `ContainsKey(s.SessionId)` exist in the file. `Assert.Same(provider.GetRequiredService<SessionMonitor>(), …)` in `CompositionRootTests.cs` proves singleton scope. All backed by passing tests (747/747, 2 consecutive runs). In-vivo line-by-line comparison remains a human task (see Human Verification). |
| 2 | Ce qui est masqué est dit, ET pourquoi (cas e465420e lisible en une ligne) | VERIFIED | `LectureSessions.cs` defines `MotifMasquage {Archivee, Traitee}` and `SessionMasquee(Session, Motif)`. `DiagnosticService.cs` renders `· {id} {project} — {état} ({âge}) — masquée par {LibelleMotif}` with `LibelleMotif` naming the actual file (`archived.json` / `treated.json`). Test `Le_cas_e465420e_se_lit_en_une_ligne_du_rapport` reconstructs the exact 2026-09-12 measurement and asserts all fragments (id, project, "à toi", "masquée par treated.json") appear on a single line. Passes. |
| 3 | Des sessions pertinentes, pas les huit premières de l'alphabet (OBS-02) | VERIFIED | `files.Take(8)` (alphabetical) is gone (`grep -cF "files.Take(8)"` → 0). Replaced by `lus.OrderBy(x => x.Urgence).ThenByDescending(x => x.Maj).Take(MaxFichiersEtat)`, where `x.Urgence` is `AffichageSessions.Urgence(a)` — called, not recopied (2 call sites in `DiagnosticService.cs`, one for masked sessions sorting, one for state-file sorting; zero recopy of the urgency scale, confirmed by reading code). Reste non listé annoncé: `"… et {N} autre(s) non listé(s)"`. Widget session list truncation also removed (`AffichageSessions.Ordonner(lecture.Visibles)`, no `.Take()`). Tests `Les_fichiers_listes_sont_ceux_qui_attendent_et_les_plus_recents` and `Le_rapport_ne_tronque_plus_la_liste_des_sessions_affichees` pass. |
| 4 | Non-retour garanti (deux gardes falsifiables) | VERIFIED | `GardesPerimetreTests.Le_diagnostic_ne_fabrique_aucun_moniteur_de_sessions` asserts `DiagnosticService.cs` does NOT contain `new SessionMonitor` and DOES contain `_moniteurSessions.Inspecter(_clock.UtcNow)`. `Le_diagnostic_recoit_le_moniteur_du_conteneur` asserts the `new DiagnosticService(...)` registration fragment in `App.xaml.cs` contains `moniteurSessions: sp.GetRequiredService<SessionMonitor>()`. `CompositionRootTests` adds `Assert.Same(...)` proving singleton scope. Both guards read real source files via `CheminSources()` (MSBuild-injected path), making them genuinely falsifiable by source mutation — verified by inspecting the guard implementation. Three mutations (a/b/c) are documented as played-and-reverted in 22-02-SUMMARY.md and 22-VALIDATION.md, consistent with each other. |

**Score:** 4/4 truths verified by automated evidence; truth #1 additionally requires a live, in-vivo confirmation that no automated check can substitute for.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Chronos/Services/LectureSessions.cs` | `MotifMasquage`, `SessionMasquee`, `LectureSessions` vocabulary | VERIFIED | File exists, matches plan exactly; no forbidden normalization patterns (`FromUnixTime*`, `DateTimeOffset.TryParse`) found. |
| `src/Chronos/Services/SessionMonitor.cs` | `Inspecter` full read; `Read` is a projection | VERIFIED | `Inspecter` present, `Read => Inspecter(now).Visibles`, single filter implementation confirmed by grep counts. |
| `src/Chronos/Services/AffichageSessions.cs` | Neutral producer of order/state-label/age-label | VERIFIED | `public static class AffichageSessions` with `Ordonner`, `Urgence`, `Etat`, `Age` — matches widget's prior inline logic exactly (labels: "à toi"/"tour fini"/"en cours"/"inconnu"). |
| `src/Chronos/Services/DiagnosticService.cs` | Optional `moniteurSessions` param, no fallback, widget section rewired | VERIFIED | Param is last-position, optional, no `?? new SessionMonitor()` fallback (by design, per XML-doc); widget section uses `_moniteurSessions.Inspecter(_clock.UtcNow)`. |
| `src/Chronos/App.xaml.cs` | Shared singleton instance passed to `DiagnosticService` | VERIFIED | `moniteurSessions: sp.GetRequiredService<SessionMonitor>()` in the `DiagnosticService` registration; `SessionMonitor` registered via `AddSingleton`. |
| `tests/Chronos.Tests/InspectionSessionsTests.cs` | e465420e case reproduced, `Read`=projection proof | VERIFIED | 6 `[Fact]` tests present and passing; `e465420e` appears 3 times (class doc, test name, constant). |
| `tests/Chronos.Tests/AffichageSessionsTests.cs` | Widget behavior unchanged after extraction | VERIFIED | 5 tests present and passing. |
| `tests/Chronos.Tests/DiagnosticServiceTests.cs` | Report describes injected monitor, masking, pertinence | VERIFIED | 25 `[Fact]`/`[Theory]` methods total (10 pre-existing + 15 new across plans 22-02/22-03); all pass; pre-existing tests untouched (`git diff -U0 … | grep -c "^-[^-]"` = 0 per SUMMARY, confirmed no regression in diff). |
| `tests/Chronos.Tests/GardesPerimetreTests.cs` | Two non-return guards | VERIFIED | Both guard tests present, read real source files, genuinely falsifiable. |
| `.planning/phases/22-.../22-VALIDATION.md` | Verification card, filled | VERIFIED | Contains `status: validated`, all 4 criteria with named proofs and measured results, falsification table, security invariants, "À VÉRIFIER PAR L'UTILISATEUR" section. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `App.xaml.cs` | `DiagnosticService` | named arg `moniteurSessions: sp.GetRequiredService<SessionMonitor>()` | WIRED | Confirmed by grep and by reading the registration block. |
| `DiagnosticService.cs` | `SessionMonitor.Inspecter` | `_moniteurSessions.Inspecter(_clock.UtcNow)` | WIRED | Confirmed present exactly once. |
| `SessionMonitor.cs` | `SessionMonitor.Inspecter` (internal) | `Read` delegates: `=> Inspecter(now).Visibles;` | WIRED | Confirmed, single implementation of filters. |
| `SessionsViewModel.cs` | `AffichageSessions` | `Ordonner`/`Age`/`Etat` calls | WIRED | 3 call sites confirmed (`Ordonner` in Refresh, `Age` in Refresh, `Etat` in Describe); no local re-implementation (`"à toi"` / `"inconnu"` literal strings absent from ViewModel). |
| `DiagnosticService.cs` | `AffichageSessions.Urgence` | file-list sorting | WIRED | 2 call sites (masked-sessions sort + state-file sort), zero recopy of the urgency scale. |
| `DiagnosticService.cs` | `SessionMonitor.Directory` | `_moniteurSessions?.Directory` | WIRED | Confirmed present exactly once; sessions folder inspected is the widget's own monitor folder. |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full build succeeds | `dotnet build Chronos.sln -c Debug --nologo -v q` | "La génération a réussi. 0 Avertissement(s) 0 Erreur(s)" | PASS |
| Full test suite green, pass 1 | `dotnet test Chronos.sln -c Debug --nologo -v q` | 747 réussite(s), 0 échec, 4s | PASS |
| Full test suite green, pass 2 (stability) | same command, re-run | 747 réussite(s), 0 échec, 4s | PASS |
| Six named phase guards | `--filter "…ServicesLayerPurityTests\|CompositionRootTests\|NormalisationUniqueTests\|GardesDoctrineTests\|GardesPerimetreTests\|La_sonde_d_en_tetes…"` | 24 réussite(s), 0 échec | PASS |
| DiagnosticService-specific tests | `--filter "FullyQualifiedName~DiagnosticServiceTests"` | 25 réussite(s), 0 échec | PASS |
| No stray `new SessionMonitor` in DiagnosticService | `grep -cF "new SessionMonitor" src/Chronos/Services/DiagnosticService.cs` | 0 | PASS |
| Shared singleton wiring visible | `grep -cF "GetRequiredService<SessionMonitor>()" src/Chronos/App.xaml.cs` | 2 | PASS |
| `Read` delegates (pure projection) | `grep -cF "=> Inspecter(now).Visibles;" src/Chronos/Services/SessionMonitor.cs` | 1 | PASS |
| Alphabetical file selection removed | `grep -cF "files.Take(8)" src/Chronos/Services/DiagnosticService.cs` | 0 | PASS |
| Phase-22 diff limited to 11 declared files, widget tests untouched | `git diff --name-only dbd2bb1~1 cb5a598 -- src/ tests/` | Exactly 11 files; `SessionsTests.cs`, `TreatedSessionsTests.cs`, `SessionStylesBindingTests.cs` absent | PASS |
| Working tree clean, no leftover mutation residue | `git status --porcelain` | (empty) | PASS |
| Security invariants (sessions/archived/oauth/overlay process) | `ls sessions \| wc -l`; `wc -c archived.json`; `wc -c oauth.dat`; `Get-Process -Id 119412` | 66; 84; 518; alive since 12.09.2026 14:30:30, not relaunched | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|--------------|--------|----------|
| OBS-01 | 22-01, 22-02 | Le diagnostic dit exactement ce que le widget affiche — même moniteur, mêmes filtres | SATISFIED | Shared-instance wiring confirmed structurally (grep + code read), not just by output comparison; deliberately chosen over "copy of behavior" per phase rationale. Marked Complete in REQUIREMENTS.md. |
| OBS-02 | 22-03 | Le diagnostic liste les sessions pertinentes, non les 8 premières par ordre alphabétique | SATISFIED | `files.Take(8)` replaced by urgency+freshness selection calling the shared `AffichageSessions.Urgence`; remainder count announced. Marked Complete in REQUIREMENTS.md. |

No orphaned requirements: REQUIREMENTS.md maps only OBS-01/OBS-02 to Phase 22, both claimed by plans 22-01/22-02/22-03.

### Anti-Patterns Found

None. Scanned `LectureSessions.cs`, `AffichageSessions.cs`, `SessionMonitor.cs`, `DiagnosticService.cs`, `App.xaml.cs` for TODO/FIXME/HACK/placeholder/"not yet implemented" markers — zero matches. No stub returns, no hardcoded-empty props found in the reviewed sections; the "MONITEUR NON INJECTÉ" no-fallback path is a deliberate, tested, honestly-labeled state, not a stub.

### Known Flags — judged, not gaps

- **`TreatedStore.Load()` TTL relies on `DateTimeOffset.UtcNow` directly (no injectable clock).** This is pre-existing design (not introduced by phase 22). The phase 22 test suite correctly avoids the resulting "time-bomb test" hazard by writing a real `EcritMaintenant22() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` into the treated-store instead of a frozen literal, while keeping snapshot timestamps (`T22`) literal for the arithmetic under test. This is the right workaround given the constraint, but it does not fix the underlying testability gap (`TreatedStore`/`ArchiveStore` are not clock-injectable). Worth flagging as debt for a future phase (candidate: phase 26, which owns `TreatedStore`/`ArchiveStore` TTL semantics) but not a defect of phase 22 itself.
- **`Inspecter` triggers `_tracker.Observe` as a side effect, same as `Read` always did.** Consequence: `LogStartupAsync` (which calls `DiagnosticService.BuildReportAsync` → `Inspecter`) now observes the tracker a few tens of milliseconds before the widget does. This is asserted in code comments and SUMMARY as "quelques dizaines de ms", not measured in production. The team's own reasoning (the tracker only reacts to *transitions* between cycles, and one extra observation at the same instant produces no transition) is sound and consistent with `SessionTreatmentTracker`'s use of `TreatedStore`/`ArchiveStore` maps rather than raw timers. Acceptable to carry forward as documented debt for phase 26 rather than a phase-22 gap.
- **Three self-corrected plan deviations in 22-03** (CS0136 iterator-name collision, tests asserting against the wrong report section, a wrong expected grep count for `AffichageSessions.Urgence`) were reviewed against the actual diff and SUMMARY narrative. All three fixes are legitimate: (1) a pure rename with no logic change, (2) a genuine test-quality fix that would otherwise have left a section untested (verified: the new `LignesFichiersEtat` helper correctly isolates the "Fichiers d'état" block via `SkipWhile`/`TakeWhile`), (3) a documentation-only correction with zero code change. No evidence that production code was bent to satisfy a shortcut or that a test was weakened to pass.

## Human Verification Required

The phase's own closing plan (22-03) explicitly defers one class of verification to a real, live launch of the overlay, which this automated verification cannot substitute for (starting or killing the running overlay process, pid 119412, was itself a constraint the phase had to respect). See frontmatter `human_verification` for the three concrete checks, all rooted in `22-VALIDATION.md`'s own "À VÉRIFIER PAR L'UTILISATEUR" section:

1. Line-by-line comparison between the live widget and the diagnostic report opened at the same instant (critère n°1, structurally guaranteed by shared-instance wiring, but only a live comparison confirms no accidental formatting divergence).
2. Confirmation that `e465420e` (or whatever the current masked-but-alive session is) appears correctly in "Sessions MASQUÉES par un filtre" on the real machine.
3. Confirmation that the "Fichiers d'état" section shows relevant entries plus an accurate "… et N autre(s) non listé(s)" count on the real, current state of `%APPDATA%\Chronos\sessions`.

A fourth item noted by the phase itself — that the diagnostic must keep describing the widget unchanged through the end of phase 26 without a single line of `DiagnosticService.cs` changing — is a milestone-level check that can only be confirmed after phase 26 ships; it is not a gap of phase 22.

### Gaps Summary

No gaps found. All four ROADMAP success criteria for Phase 22 are backed by real, independently-verified code changes (not merely SUMMARY claims): the shared-instance wiring is structural (singleton DI registration, zero local `SessionMonitor` construction in the diagnostic, pure delegation in `Read`), the masking vocabulary is real and tested against the exact historical `e465420e` case, the alphabetical file selection has been replaced by a call to the shared urgency scale, and the two non-return guards are genuinely falsifiable (they parse real source files, not test doubles). The build succeeds, all 747 tests pass on two consecutive runs, the phase's diff is exactly the 11 files it claims, the three widget-behavior test files remain completely untouched, and all disk-based security invariants (sessions count, archived.json size, oauth.dat size, overlay process liveness) hold. The only unresolved item is an in-vivo, human-only comparison that the phase's own validation card correctly routes to manual verification rather than claiming as automatically provable — hence `human_needed` rather than `passed`.

---

*Verified: 2026-09-12T16:24:08Z*
*Verifier: Claude (gsd-verifier)*
