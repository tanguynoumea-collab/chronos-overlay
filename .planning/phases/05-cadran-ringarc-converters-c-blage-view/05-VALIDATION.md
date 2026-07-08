---
phase: 5
slug: cadran-ringarc-converters-c-blage-view
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-08
---

# Phase 5 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + Xunit.StaFact (tests/Chronos.Tests, 41 tests verts) |
| **Config file** | tests/Chronos.Tests/Chronos.Tests.csproj |
| **Quick run command** | `dotnet build Chronos.sln -c Debug` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug` |
| **Estimated runtime** | ~60 secondes |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build Chronos.sln -c Debug`
- **After every plan wave:** Run `dotnet test Chronos.sln -c Debug`
- **Before `/gsd:verify-work`:** Full suite green + smoke run de l'exe
- **Max feedback latency:** 90 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 5-xx | — | — | CAD-07 | unit | `dotnet test --filter ArcGeometry` (points, IsLargeArc, fraction 0/1, EllipseGeometry plein) | ❌ W0 | ⬜ pending |
| 5-xx | — | — | CAD-04 | unit | `dotnet test --filter RampColor` (stops exacts #7BB13C/#EFA23A/#D8503A, interpolation) | ❌ W0 | ⬜ pending |
| 5-xx | — | — | CAD-05 | unit | `dotnet test --filter UtilizationToBrush` (≥1 → #5A5960, null → neutre) | ❌ W0 | ⬜ pending |
| 5-xx | — | — | CAD-01 | grep+build | tokens exacts présents dans le XAML (`#16151B`, `#2C2B34`, `#34333D`, `#46454F`) | ❌ W0 | ⬜ pending |
| 5-xx | — | — | CAD-02/03 | grep | bindings `FiveHour.FractionRemaining` (extérieur) / `SevenDay.FractionRemaining` (intérieur) dans MainWindow.xaml | ❌ W0 | ⬜ pending |
| 5-xx | — | — | CAD-06 | grep | bindings `FiveHour.CountdownText` / `SevenDay.CountdownText` au centre | ❌ W0 | ⬜ pending |
| 5-xx | — | — | DAT-08 | grep+unit | badge lié à `IsEstimated`, visible seulement si Estimated | ❌ W0 | ⬜ pending |
| 5-xx | — | — | ROB-01 | unit+smoke | `DataUnavailable` → texte « données indisponibles », exe ne crashe pas sans sources | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Chronos.Tests/ArcGeometryTests.cs`
- [ ] `tests/Chronos.Tests/RampColorTests.cs`
- [ ] `tests/Chronos.Tests/UtilizationToBrushConverterTests.cs`

---

## Human Verification Items

| Criterion | How to verify | Maps to |
|-----------|--------------|---------|
| Fidélité visuelle à la maquette (couleurs, proportions) | Lancer l'app, comparer | CAD-01..06 |
| Arcs se vident dans le bon sens et progressent à la seconde | Observer 10 s | CAD-02/03, RAF-03 |
