---
phase: 1
slug: fondations-architecture-squelette-overlay
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-08
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (net8.0) — projet tests/Chronos.Tests ; Xunit.StaFact pour les tests nécessitant un thread STA |
| **Config file** | none — Wave 0 installs (création du projet de tests) |
| **Quick run command** | `dotnet build Chronos.sln -c Debug` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug` |
| **Estimated runtime** | ~30 secondes |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build Chronos.sln -c Debug`
- **After every plan wave:** Run `dotnet test Chronos.sln -c Debug`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 1-01-01 | 01 | 1 | FEN-01 | build | `dotnet build Chronos.sln` | ❌ W0 | ⬜ pending |
| 1-01-02 | 01 | 1 | FEN-01 | grep | `grep WindowStyle=\"None\" src/Chronos/Views/MainWindow.xaml` | ❌ W0 | ⬜ pending |
| 1-01-03 | 01 | 1 | ROB-04 | unit | `dotnet test --filter TopmostGuard` | ❌ W0 | ⬜ pending |
| 1-01-04 | 01 | 1 | FEN-01 | smoke manuel | lancement `dotnet run` — fenêtre visible sans bordure ni entrée barre des tâches | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Chronos.Tests/Chronos.Tests.csproj` — projet xUnit référençant src/Chronos
- [ ] `tests/Chronos.Tests/Services/TopmostGuardTests.cs` — stubs pour ROB-04 (flags SetWindowPos vérifiables via délégué injectable)

---

## Human Verification Items

| Criterion | How to verify | Maps to |
|-----------|--------------|---------|
| Fenêtre borderless/transparente visible au bureau | Lancer l'app, observer | FEN-01 / SC1 |
| Pas d'entrée barre des tâches, pas de vol de focus | Observer barre des tâches + focus après lancement | FEN-01 / SC2 |
| Reste au-dessus après ouverture d'autres fenêtres | Ouvrir un explorateur par-dessus, attendre ≥ 2 s | ROB-04 / SC2 |
| Fermeture propre | Fermer l'app (kill process / raccourci), vérifier qu'aucun process ne survit | SC3 |
