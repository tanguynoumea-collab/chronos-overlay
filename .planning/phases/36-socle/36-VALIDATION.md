---
phase: 36
slug: socle
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-03
---

# Phase 36 — Validation Strategy

> Contrat de validation de la phase. Détail des critères → tests : `36-RESEARCH.md` § « Architecture de validation (Nyquist) ».

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (net8.0-windows) |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~SettingsServiceTests\|FullyQualifiedName~ArgumentsDemarrageTests\|FullyQualifiedName~GardesPerimetreTests"` |
| **Full suite command** | `dotnet build Chronos.sln` (0 avertissement) puis `dotnet test Chronos.sln` |
| **Estimated runtime** | rapide ≈ 15 s ; complète ≈ 2-3 min |

## Sampling Rate

- **After every task commit:** commande rapide filtrée
- **After every plan wave:** suite complète
- **Before `/gsd:verify-work`:** build 0 warning + suite complète verte
- **Max feedback latency:** 15 s

## Per-Task Verification Map

| Critère | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|-------------|-----------|-------------------|-------------|--------|
| C1 valeur fautive ne coûte qu'elle-même (fixture témoin, Save/relecture, Corner défaut ≠ index 0, test générique par réflexion, journal des retombées) | SOC-01 | unit | `--filter "FullyQualifiedName~SettingsServiceTests"` | ❌ W0 | ⬜ pending |
| C2 JSON illisible / tronqué → défauts entiers, jamais à moitié lu ; legacy vert | SOC-01 | unit | idem | partiel | ⬜ pending |
| C3 table de vérité `ArgumentsDemarrage.Trier`, liste blanche, garde textuelle « tri avant verrou » | SOC-02 | unit + garde | `--filter "FullyQualifiedName~ArgumentsDemarrageTests\|FullyQualifiedName~GardesPerimetreTests"` | ❌ W0 | ⬜ pending |
| C4 rien d'autre ne bouge (gardes réécrites, pureté, suite complète) | SOC-02 | garde + suite | `dotnet test Chronos.sln` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

## Wave 0 Requirements

- [ ] `tests/Chronos.Tests/TestData/settings-valeurs-inconnues.json`
- [ ] `tests/Chronos.Tests/ArgumentsDemarrageTests.cs`
- [ ] Réécriture des deux gardes de placement de `GardesPerimetreTests.cs` + nouvelle garde « tri avant verrou »

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Exe réel lancé avec `--zzz` sort en code 0 sans fenêtre | SOC-02 | lancer un exe WPF sous test ouvrirait l'overlay si la garde régressait | joué au constat de la phase 43 |
