---
phase: 11
slug: int-gration-composite-r-glage
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-09
---

# Phase 11 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + Xunit.StaFact (tests/Chronos.Tests, 178 tests verts) |
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
| 11-xx | — | — | INT-01 | unit | `dotnet test --filter Composite` (chaîne 3 : OAuth Exact prioritaire par fenêtre, bascule statusLine puis JSONL) | ✅ (étendre) | ⬜ pending |
| 11-xx | — | — | INT-02 | unit | fenêtre Exact OAuth → IsEstimated false → pas de badge, couleur rampe (WindowGaugeViewModel) | ✅ (étendre) | ⬜ pending |
| 11-xx | — | — | INT-03 | unit | `dotnet test --filter Gated` (OAuthUsageEnabled=false → Empty, SendCount==0, token jamais lu) + toggle VM persiste + RequestRefresh | ❌ W0 | ⬜ pending |
| 11-xx | — | — | pureté | unit | ServicesLayerPurityTests verte (wrapper gated neutre) | ✅ | ⬜ pending |
| 11-xx | — | — | non-rég | unit | Assert.Same du composite existant intacts ; 178 tests + nouveaux verts | ✅ | ⬜ pending |

## Wave 0 Requirements

- [ ] Champ OAuthUsageEnabled dans ChronosSettings + round-trip SettingsServiceTests

## Human Verification Items

| Criterion | How to verify | Maps to |
|-----------|--------------|---------|
| Cadran affiche les vrais % (~74/93) SANS badge « estimée », arcs colorés | Republier + lancer l'app | INT-01/02 |
| Toggle « Usage exact (OAuth) » off → repli estimé, aucun accès token | Décocher dans le menu | INT-03 |
