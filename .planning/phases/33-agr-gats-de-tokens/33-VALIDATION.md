---
phase: 33
slug: agr-gats-de-tokens
status: planned
nyquist_compliant: true
wave_0_complete: false
created: 2026-09-27
updated: 2026-09-27
---

# Phase 33 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.2 + Xunit.StaFact 1.1.11 (Microsoft.NET.Test.Sdk 17.11.1), `net8.0-windows`, `UseWPF` |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` (`CheminSourcesChronos`, `CheminDocsChronos` injectés par MSBuild ; fixtures localisées par `[CallerFilePath]`, rien n'est copié en sortie) |
| **Quick run command** | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q --filter "FullyQualifiedName~<Classe>"` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` |
| **Estimated runtime** | ≈ 10–12 s (1313 / 0 à l'entrée, HEAD `5a154a5`) ; ≈ 15 s attendus en fin de phase (≈ +95 tests, dont les passes de reconstruction sur racines temporaires) |

---

## Sampling Rate

- **After every task commit:** Run the filtre de la classe du plan (< 5 s) — commande `<automated>` de la tâche.
- **After every plan wave:** Run `dotnet test Chronos.sln -c Debug --nologo -v q` DEUX fois de suite (règle du dépôt), 0 échec, 0 warning (`dotnet build Chronos.sln -c Debug --nologo 2>&1 | grep -c " warning "` = 0).
- **Before `/gsd:verify-work`:** Full suite must be green twice ; build Release 0 warning ; `git status --short -- src tests docs` vide.
- **Max feedback latency:** 15 s (suite complète) ; < 5 s par filtre.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 33-01-01 | 01 | 1 | TOK-01 | unit (pur + fixture) | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q --filter "FullyQualifiedName~LigneAgregatTests"` | ❌ W0 (créé par la tâche : `LigneAgregatTests.cs`, fixture `tokens/tolerance`) | ⬜ pending |
| 33-01-02 | 01 | 1 | TOK-01 | unit (E/S temp) | `… --filter "FullyQualifiedName~MagasinAgregatsTests\|FullyQualifiedName~LigneAgregatTests"` | ❌ W0 (`MagasinAgregatsTests.cs`) | ⬜ pending |
| 33-01-03 | 01 | 1 | TOK-01 (prépare TOK-04/05) | unit + garde réflexive/textuelle | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ W0 (`CouvertureTokensTests.cs`, `GardeTokensSansPourcentageTests.cs`) / ✅ `ServicesLayerPurityTests.cs` (+1) | ⬜ pending |
| 33-02-01 | 02 | 1 | TOK-03 (lecture TOK-02) | unit (12 fixtures réelles anonymisées) | `… --filter "FullyQualifiedName~LecteurTranscriptTests\|FullyQualifiedName~GardesDedupUsageTests"` | ❌ W0 (`LecteurTranscriptTests.cs`, `TestData/transcripts/**`) | ⬜ pending |
| 33-02-02 | 02 | 1 | TOK-03 | unit (E/S temp) | `… --filter "FullyQualifiedName~IndexMessagesTests\|FullyQualifiedName~DedupUsageTests\|FullyQualifiedName~GardesDedupUsageTests\|FullyQualifiedName~DedupHeritageDeltaTests\|FullyQualifiedName~TranscriptActivityProviderTests"` | ❌ W0 (`IndexMessagesTests.cs`) / ✅ `DedupUsageTests.cs` (+1) | ⬜ pending |
| 33-02-03 | 02 | 1 | TOK-03 | unit (E/S temp) | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ W0 (`CurseursTests.cs`) | ⬜ pending |
| 33-03-01 | 03 | 2 | TOK-02 | unit (pur) | `… --filter "FullyQualifiedName~ProjectionAgregatsTests\|FullyQualifiedName~GardeTokensSansPourcentageTests\|FullyQualifiedName~ServicesLayerPurityTests"` | ❌ W0 (`ProjectionAgregatsTests.cs`, `Fakes/FakeEtatReconstruction.cs`) | ⬜ pending |
| 33-03-02 | 03 | 2 | TOK-02, TOK-03 | integration (racine temp, callbacks) | `… --filter "FullyQualifiedName~ReconstructionTokensTests\|FullyQualifiedName~GardeTokensSansPourcentageTests"` | ❌ W0 (`ReconstructionTokensTests.cs`) | ⬜ pending |
| 33-03-03 | 03 | 2 | TOK-02, TOK-03 (idempotence octets) | integration (thread, annulation, sha256) | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ W0 (même classe, +6) | ⬜ pending |
| 33-04-01 | 04 | 2 | TOK-04 | unit (fixtures + E/S temp) | `… --filter "FullyQualifiedName~LecteurAgregatsTests\|FullyQualifiedName~GardeTokensSansPourcentageTests"` | ❌ W0 (`LecteurAgregatsTests.cs`, fixtures `tokens/dst-*`, `tokens/a-cheval-mois`) | ⬜ pending |
| 33-04-02 | 04 | 2 | TOK-04 | unit (pur, tz `FuseauParisPourTests`) | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ W0 (`RenduLocalTokensTests.cs`) | ⬜ pending |
| 33-05-01 | 05 | 3 | TOK-02 câblé, CPT-02 | garde DI + unit | `… --filter "FullyQualifiedName~CompositionRootTests\|FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~LectureSeuleAppBureauTests"` | ✅ classes (+2, +2, 1 assertion modifiée) | ⬜ pending |
| 33-05-02 | 05 | 3 | TOK-02 (VM) | unit | `… --filter "FullyQualifiedName~MainViewModelTests\|FullyQualifiedName~ReglagesBindingTests\|FullyQualifiedName~CompositionRootTests"` | ✅ classe (+2) | ⬜ pending |
| 33-05-03 | 05 | 3 | TOK-05 | garde documentaire + suite complète | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ W0 (`ContratAgregatsDocumenteTests.cs`) / ✅ `ContratJournalDocumenteTests` intact | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

Sampling continuity : chaque tâche porte un `<automated>` ; aucune suite de 3 tâches sans vérification automatisée ; chaque fin de plan = suite complète ×2.

---

## Wave 0 Requirements

Toutes les classes et fixtures ci-dessous sont CRÉÉES par la tâche qui les consomme (RED nommé → GREEN), dans la vague indiquée — aucune tâche « Wave 0 » séparée n'est nécessaire, le framework est en place.

- [ ] `tests/Chronos.Tests/TestData/tokens/tolerance/tokens-2026-09.jsonl` — 33-01-01 (TOK-01)
- [ ] `tests/Chronos.Tests/LigneAgregatTests.cs`, `MagasinAgregatsTests.cs`, `CouvertureTokensTests.cs`, `GardeTokensSansPourcentageTests.cs` — 33-01
- [ ] `tests/Chronos.Tests/TestData/transcripts/{multi-blocs, sous-agent, tronque, reduit, futur, fork-copie, a-cheval-tranche, prefiltre-piege, sans-message-id, synthetic}/…` — 12 fixtures réelles anonymisées (gabarit `transcript-multi-blocs.jsonl`, clés 2.1.281) — 33-02-01 (TOK-03, TOK-02)
- [ ] `tests/Chronos.Tests/LecteurTranscriptTests.cs`, `IndexMessagesTests.cs`, `CurseursTests.cs` — 33-02
- [ ] `tests/Chronos.Tests/Fakes/FakeEtatReconstruction.cs` — 33-03-01 (consommé par 33-05)
- [ ] `tests/Chronos.Tests/ProjectionAgregatsTests.cs`, `ReconstructionTokensTests.cs` — 33-03
- [ ] `tests/Chronos.Tests/TestData/tokens/{dst-2026-10-25, dst-2027-03-28, a-cheval-mois}/` (100 / 92 / 1 + 4 lignes) — 33-04-01 (TOK-04)
- [ ] `tests/Chronos.Tests/LecteurAgregatsTests.cs`, `RenduLocalTokensTests.cs` — 33-04
- [ ] `tests/Chronos.Tests/ContratAgregatsDocumenteTests.cs` — 33-05-03 (TOK-05)
- [ ] Extensions de classes existantes (insertions seules sauf mention) : `ServicesLayerPurityTests` (+1, 33-01), `DedupUsageTests` (+1, 33-02), `CompositionRootTests` (+2), `DiagnosticServiceTests` (+2, **1 assertion modifiée** l. ~1354 « aucun (phase 33) » — écart assumé), `MainViewModelTests` (+2) — 33-05
- Framework : rien à installer.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| — | — | — | — |

*All phase behaviors have automated verification.* Le coût sur la vraie machine (TOK-02 « mesuré et consigné ») est une MESURE (prototype de la recherche sur 2,08 Go, 2026-09-27 ; harnais réel optionnel en 33-05-03, lecture seule, sortie sous le scratchpad), pas une vérification manuelle ; l'overlay n'est jamais lancé par l'agent — la présence réelle des quatre fichiers sous `%APPDATA%\Chronos\historique\` se constate en phase 35 par sonde hors arbre.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (chaque référence ❌ est créée par sa propre tâche RED)
- [x] No watch-mode flags
- [x] Feedback latency < 15 s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending (à signer par l'orchestrateur après `/gsd:execute-phase 33` : totaux réels des deux exécutions finales, mutations jouées, 0 warning)
