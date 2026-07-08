---
phase: 6
slug: comportements-overlay-placement-interaction
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-08
---

# Phase 6 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + Xunit.StaFact (tests/Chronos.Tests, 68 tests verts) |
| **Config file** | tests/Chronos.Tests/Chronos.Tests.csproj |
| **Quick run command** | `dotnet build Chronos.sln -c Debug` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug` |
| **Estimated runtime** | ~70 secondes |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build Chronos.sln -c Debug`
- **After every plan wave:** Run `dotnet test Chronos.sln -c Debug`
- **Before `/gsd:verify-work`:** Full suite green + smoke run
- **Max feedback latency:** 90 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 6-xx | — | — | FEN-03/04 | unit | `dotnet test --filter CornerSnap` (fonction pure coin le plus proche, marge, multi-rects) | ❌ W0 | ⬜ pending |
| 6-xx | — | — | FEN-07 | unit | `dotnet test --filter SettingsService` (round-trip, corrompu→défauts, écriture atomique) | ❌ W0 | ⬜ pending |
| 6-xx | — | — | ROB-03 | unit | `dotnet test --filter Recalibr` (offset appliqué au repli seulement, Exact intact, badge estimée conservé) | ❌ W0 | ⬜ pending |
| 6-xx | — | — | FEN-05 | unit | `dotnet test --filter TopmostGuard` (Suspend/Resume, pas de réaffirmation en mode arrière-plan) | ✅ (à étendre) | ⬜ pending |
| 6-xx | — | — | FEN-06 | grep+build | ContextMenu avec items Arrière-plan/Recalibrer/démarrage/Quitter + RelayCommand dans le VM | ❌ W0 | ⬜ pending |
| 6-xx | — | — | DEP-02 | unit/manual | création/suppression .lnk dans un dossier temp injecté (pas le vrai Startup en test) | ❌ W0 | ⬜ pending |
| 6-xx | — | — | FEN-02 | grep+smoke | `DragMove()` dans MainWindow.xaml.cs + snap au retour | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Tests purs CornerSnap (rects doubles neutres)
- [ ] Tests SettingsService avec répertoire temp injecté
- [ ] Mise à jour allow-list ServicesLayerPurityTests (OverlayController)

---

## Human Verification Items

| Criterion | How to verify | Maps to |
|-----------|--------------|---------|
| Drag fluide + snap au coin le plus proche | Glisser l'overlay, relâcher | FEN-02/03 |
| Multi-écrans + DPI mixte | Glisser vers 2e écran, snap correct | FEN-04 |
| Menu clic droit complet, Quitter fonctionne | Clic droit | FEN-06 |
| Position restaurée après relance | Fermer/relancer | FEN-07 |
| Autostart réel | Toggle + reboot (optionnel) | DEP-02 |
