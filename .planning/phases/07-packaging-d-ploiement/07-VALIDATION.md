---
phase: 7
slug: packaging-d-ploiement
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-08
---

# Phase 7 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | dotnet CLI + vérifications fichiers/process (pas de nouveaux tests unitaires) |
| **Config file** | src/Chronos/Chronos.csproj |
| **Quick run command** | `dotnet build Chronos.sln -c Release` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug` (non-régression) + `dotnet publish` + lancement exe publié |
| **Estimated runtime** | ~3 minutes (publish compris) |

## Sampling Rate

- **After every task commit:** `dotnet build Chronos.sln -c Release`
- **After every plan wave:** publish + lancement exe publié
- **Max feedback latency:** 240 seconds

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 7-xx | — | — | DEP-01 | publish | `dotnet publish src/Chronos -c Release -r win-x64` sort un Chronos.exe unique | ❌ | ⬜ pending |
| 7-xx | — | — | DEP-01 | fichiers | publish/ ne contient que Chronos.exe (+ .pdb) ; taille < 120 Mo | ❌ | ⬜ pending |
| 7-xx | — | — | DEP-01 | smoke | lancement exe publié ~8 s : process vivant, fenêtre visible, kill propre | ❌ | ⬜ pending |
| 7-xx | — | — | non-régression | test | `dotnet test Chronos.sln -c Debug` : 106/106 | ✅ | ⬜ pending |

## Wave 0 Requirements

- (aucun — vérifications par CLI)

## Human Verification Items

| Criterion | How to verify | Maps to |
|-----------|--------------|---------|
| Machine réellement propre sans .NET | Copier l'exe sur une autre machine/VM sans SDK | DEP-01 / SC2 |
| Autostart depuis l'exe publié après reboot | Toggle + redémarrage Windows | SC3 |
