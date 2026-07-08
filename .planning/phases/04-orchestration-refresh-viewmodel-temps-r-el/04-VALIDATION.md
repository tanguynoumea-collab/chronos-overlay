---
phase: 4
slug: orchestration-refresh-viewmodel-temps-r-el
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-08
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (tests/Chronos.Tests, 27 tests verts) + Xunit.StaFact pour ce qui touche le Dispatcher |
| **Config file** | tests/Chronos.Tests/Chronos.Tests.csproj |
| **Quick run command** | `dotnet build Chronos.sln -c Debug` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug` |
| **Estimated runtime** | ~60 secondes |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build Chronos.sln -c Debug`
- **After every plan wave:** Run `dotnet test Chronos.sln -c Debug`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 90 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 4-xx | — | — | RAF-01 | unit | `dotnet test --filter RefreshOrchestrator` (debounce/coalescence, Error→recréation, Renamed traité) | ❌ W0 | ⬜ pending |
| 4-xx | — | — | RAF-02 | unit | `dotnet test --filter PeriodicRefresh` (tick périodique déclenche GetAsync) | ❌ W0 | ⬜ pending |
| 4-xx | — | — | RAF-03 | unit | `dotnet test --filter Interpolate` (Interpolate(now) pur, aucun I/O — FakeClock) | ❌ W0 | ⬜ pending |
| 4-xx | — | — | RAF-04 | unit | `dotnet test --filter Marshaling` (FakeUiDispatcher.PostCount, aucune exception cross-thread) | ❌ W0 | ⬜ pending |
| 4-xx | — | — | pureté | unit | `dotnet test --filter ServicesLayerPurity` (orchestrateur neutre) | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Chronos.Tests/FakeUiDispatcher.cs` — dispatcher factice comptant les Post
- [ ] Fixtures fichier temporaire pour le watcher (répertoire temp, écriture atomique simulée)

---

## Human Verification Items

| Criterion | How to verify | Maps to |
|-----------|--------------|---------|
| Countdown visible progresse à la seconde dans l'app réelle | Lancer l'app, observer 5 s | RAF-03 |
| Écriture usage.json (session Claude réelle) → maj de l'affichage | Session Claude Code active + overlay ouvert | RAF-01 |
