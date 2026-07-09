---
phase: 12
slug: refonte-du-cadran-3-anneaux-remplissage-compacit
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-09
---

# Phase 12 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + Xunit.StaFact (tests/Chronos.Tests, 188 tests verts) |
| **Config file** | tests/Chronos.Tests/Chronos.Tests.csproj |
| **Quick run command** | `dotnet build Chronos.sln -c Debug` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug` |
| **Estimated runtime** | ~80 secondes |

## Sampling Rate

- **After every task commit:** `dotnet build Chronos.sln -c Debug`
- **After every plan wave:** `dotnet test Chronos.sln -c Debug`
- **Max feedback latency:** 90 seconds

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 12-xx | — | — | VIS-01 | unit | `dotnet test --filter Elapsed` (FractionElapsed = 1−FractionRemaining, clamp 0..1) | ❌ W0 | ⬜ pending |
| 12-xx | — | — | JOUR-01 | unit | `dotnet test --filter DayTimeline` (fraction jour = minutes depuis minuit/1440 ; 18h→0.75, minuit→0) | ❌ W0 | ⬜ pending |
| 12-xx | — | — | JOUR-02 | unit | `dotnet test --filter DayTicks` (angles des resets 5h projetés sur 24h, tous les 75° + offset de phase) | ❌ W0 | ⬜ pending |
| 12-xx | — | — | VIS-05 | unit | `dotnet test --filter UtilizationText` (exact « 80 % », estimé « ~80 % », null → vide) | ❌ W0 | ⬜ pending |
| 12-xx | — | — | VIS-02 | grep | ordre des RingArc dans MainWindow.xaml : hebdo rayon min, 5h milieu, 24h rayon max | ❌ W0 | ⬜ pending |
| 12-xx | — | — | JOUR-03 | grep | anneau 24h lié à FiveHour.Utilization via UtilizationToBrushConverter | ❌ W0 | ⬜ pending |
| 12-xx | — | — | TAILLE-01 | grep+smoke | `grep 'Width="170"\|Height="170"' MainWindow.xaml` + exe lancé sans crash, fenêtre 170px | ❌ W0 | ⬜ pending |

## Wave 0 Requirements

- [ ] Tests de la math pure (FractionElapsed, fraction jour, angles ticks 24h)

## Human Verification Items

| Criterion | How to verify | Maps to |
|-----------|--------------|---------|
| Arcs se remplissent (pas se vident) vers le reset | Observer, comparer début/fin de fenêtre | VIS-01 |
| 3 anneaux lisibles, ordre correct, pas de chevauchement à 170px | Observer l'overlay | VIS-02, TAILLE-01 |
| Anneau 24h : remplissage jour + graduations 5h correctes | Observer + comparer à l'heure réelle | JOUR-01/02 |
| % lisibles à côté des countdowns | Observer le centre | VIS-05 |
