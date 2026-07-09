---
phase: 8
slug: inf-rence-des-fen-tres-estimation-depuis-jsonl
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-09
---

# Phase 8 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (tests/Chronos.Tests, 107 tests verts) |
| **Config file** | tests/Chronos.Tests/Chronos.Tests.csproj |
| **Quick run command** | `dotnet build Chronos.sln -c Debug` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug` |
| **Estimated runtime** | ~70 secondes |

## Sampling Rate

- **After every task commit:** `dotnet build Chronos.sln -c Debug`
- **After every plan wave:** `dotnet test Chronos.sln -c Debug`
- **Max feedback latency:** 90 seconds

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 8-xx | — | — | EST-01 | unit | `dotnet test --filter FiveHourWindowInference` (trou ≥5h, remontée, reset=début+5h, cas limites) | ❌ W0 | ⬜ pending |
| 8-xx | — | — | EST-02 | unit | `dotnet test --filter Inference` (fenêtre expirée/inactive → fraction=1, tokens=0) | ❌ W0 | ⬜ pending |
| 8-xx | — | — | EST-03 | unit | `dotnet test --filter JsonlEstimation` (utilization=tokens/plafond ; null sans plafond) | ✅ (étendre) | ⬜ pending |
| 8-xx | — | — | EST-04 | unit | `dotnet test --filter Weekly` (fenêtre ancrée [ancre+k·7j], 7j glissants sans ancre) | ✅ (étendre) | ⬜ pending |
| 8-xx | — | — | EST-05 | unit | non-régression WeeklyRecalibration (suite existante) | ✅ | ⬜ pending |
| 8-xx | — | — | NET-01 | grep+test | `grep -L "SnapshotChanged" src/Chronos/Services/IUsageProvider.cs` + `grep -L "Age" src/Chronos/Models/UsageSnapshot.cs` + suite verte | ✅ | ⬜ pending |

## Wave 0 Requirements

- [ ] `src/Chronos/Services/FiveHourWindowInference.cs` (fonction pure) + tests
- [ ] Fixtures de trous d'activité (timestamps contrôlés, générées en mémoire ou TestData)

## Human Verification Items

| Criterion | How to verify | Maps to |
|-----------|--------------|---------|
| Arc 5 h se remplit avec l'usage réel de l'app bureau | Lancer l'overlay après une session desktop | EST-01/02 |
