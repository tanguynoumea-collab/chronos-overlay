---
phase: 9
slug: calibration-des-plafonds-surfa-age
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-09
---

# Phase 9 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + Xunit.StaFact (tests/Chronos.Tests, 119 tests verts) |
| **Config file** | tests/Chronos.Tests/Chronos.Tests.csproj |
| **Quick run command** | `dotnet build Chronos.sln -c Debug` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug` |
| **Estimated runtime** | ~75 secondes |

## Sampling Rate

- **After every task commit:** `dotnet build Chronos.sln -c Debug`
- **After every plan wave:** `dotnet test Chronos.sln -c Debug`
- **Max feedback latency:** 90 seconds

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 9-xx | — | — | CAL-01 | unit+grep | `dotnet test --filter Calibrate` (commande VM Load frais→Save, null accepté) + MenuItem « Calibrer » dans XAML | ❌ W0 | ⬜ pending |
| 9-xx | — | — | CAL-02 | unit | `dotnet test --filter AutoCalibr` (plafond déduit=tokens/util, manuel récent jamais écrasé, inerte sans Exact) | ❌ W0 | ⬜ pending |
| 9-xx | — | — | CAL-03 | unit | Reliability reste Estimated avec plafond calibré (badge conservé) | ✅ (étendre) | ⬜ pending |
| 9-xx | — | — | NET-02 | unit+grep | formatage tokens fr abrégé (fonction pure) + TextBlock lié visible seulement si Estimated | ❌ W0 | ⬜ pending |
| 9-xx | — | — | pureté | unit | ServicesLayerPurityTests verte (BudgetAutoCalibrator neutre) | ✅ | ⬜ pending |

## Wave 0 Requirements

- [ ] Tests de la règle de priorité manuel/auto (fonction pure)
- [ ] Tests du formateur de tokens

## Human Verification Items

| Criterion | How to verify | Maps to |
|-----------|--------------|---------|
| Dialogue plafonds utilisable, arc 5 h se colore après saisie | Menu → Calibrer, saisir un plafond, observer | CAL-01/03 |
| Tokens estimés lisibles et discrets | Observer le cadran en mode repli | NET-02 |
