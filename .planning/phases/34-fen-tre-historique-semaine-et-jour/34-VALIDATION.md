---
phase: 34
slug: fen-tre-historique-semaine-et-jour
status: planned
nyquist_compliant: true
wave_0_complete: false
created: 2026-09-27
---

# Phase 34 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`, `[Collection("XAML WPF")]` pour tout chargement de BAML), Microsoft.NET.Test.Sdk 17.11.1 |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` (AssemblyMetadata `CheminSourcesChronos` / `CheminDocsChronos`) ; `tests/Chronos.Tests/XamlWpfCollection.cs` |
| **Quick run command** | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q --filter "FullyQualifiedName~<ClasseDeTests>"` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` (1415 verts à l'entrée, ≈ 10 s) + `dotnet build Chronos.sln -c Release --nologo` (0 warning) |
| **Estimated runtime** | ~10 s (suite complète) ; < 5 s par filtre |

---

## Sampling Rate

- **After every task commit:** Run the plan's filter (`--filter "FullyQualifiedName~<ClasseDeTests>"`) + gardes transverses `--filter "FullyQualifiedName~ServicesLayerPurityTests|FullyQualifiedName~GardeTokensSansPourcentageTests|FullyQualifiedName~GardesDoctrineTests|FullyQualifiedName~GardeTokensHistoriqueTests"`
- **After every plan wave:** Run `dotnet test Chronos.sln -c Debug --nologo -v q` DEUX fois (0 échec) + `dotnet build Chronos.sln -c Release --nologo` (0 warning)
- **Before `/gsd:verify-work`:** Full suite must be green twice ; mutations h1–h5 (34-08) rouges nommées puis révoquées par copie (sha256)
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 34-01-01 | 01 | 1 | HIS-07 | garde XML + smoke | `dotnet test … --filter "FullyQualifiedName~GardeTokensHistoriqueTests\|FullyQualifiedName~ReglagesBindingTests\|FullyQualifiedName~ThemingTests\|FullyQualifiedName~CadranBindingTests"` | ❌ W0 (`GardeTokensHistoriqueTests.cs` créé par la tâche) | ⬜ pending |
| 34-01-02 | 01 | 1 | HIS-07 | garde textuelle + `[WpfFact]` | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ W0 (même fichier) | ⬜ pending |
| 34-02-01 | 02 | 1 | HIS-02 | `[Fact]` pur | `… --filter "FullyQualifiedName~EchellesHistoriqueTests\|FullyQualifiedName~ReticuleTests"` | ❌ W0 | ⬜ pending |
| 34-02-02 | 02 | 1 | HIS-02, HIS-06 (géométrie) | `[Fact]` sur fixture `trou-arrete` | `… --filter "FullyQualifiedName~EscalierTests"` | ❌ W0 (fixtures 32-06 ✅) | ⬜ pending |
| 34-02-03 | 02 | 1 | HIS-02, HIS-03 (géométrie) | `[Fact]` sur fixtures + 4 001 points | `… --filter "FullyQualifiedName~BinningTests\|FullyQualifiedName~Tuiles5hTests"` puis suite complète | ❌ W0 | ⬜ pending |
| 34-03-01 | 03 | 1 | HIS-01, HIS-06 (socle) | `[Fact]` services + purety + settings | `… --filter "FullyQualifiedName~DivergencesTests\|FullyQualifiedName~GraduationsCalendrierTests\|FullyQualifiedName~SourceHistoriqueDisqueTests\|FullyQualifiedName~SettingsServiceTests\|FullyQualifiedName~ServicesLayerPurityTests"` | ❌ W0 (SettingsServiceTests ✅ étendu) | ⬜ pending |
| 34-03-02 | 03 | 1 | HIS-06, HIS-08 (textes) | `[Fact]` textes mot pour mot + scénario | `… --filter "FullyQualifiedName~TextesHistoriqueTests\|FullyQualifiedName~ScenariosHistoriqueTests\|FullyQualifiedName~DivergencesTests"` | ❌ W0 | ⬜ pending |
| 34-03-03 | 03 | 1 | HIS-08, HIS-01, HIS-07 (VM) | `[Fact]` VM (tick, F2 coalescé, navigation) | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ W0 (`FakeSourceHistorique`, `FakeUiDispatcherDiffere`) | ⬜ pending |
| 34-04-01 | 04 | 2 | HIS-07, HIS-02 | `[WpfFact]` contrôles (compteur de rendus, trace) | `… --filter "FullyQualifiedName~PistesHistoriqueTests\|FullyQualifiedName~GardeTokensHistoriqueTests"` | ❌ W0 (helper `Rendre`) | ⬜ pending |
| 34-04-02 | 04 | 2 | HIS-07, HIS-02, HIS-03 | `[WpfFact]` contrôles + `[Fact]` converters | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ W0 | ⬜ pending |
| 34-05-01 | 05 | 2 | HIS-01 | smoke XAML (collection) + `[Fact]` placement | `… --filter "FullyQualifiedName~HistoriqueBindingTests\|FullyQualifiedName~PlacementHistoriqueTests\|FullyQualifiedName~GardeTokensHistoriqueTests\|FullyQualifiedName~ReglagesBindingTests"` | ❌ W0 | ⬜ pending |
| 34-05-02 | 05 | 2 | HIS-01 | miroir DI + garde textuelle | `dotnet test Chronos.sln -c Debug --nologo -v q` | ✅ (`CompositionRootTests`, `GardesPerimetreTests` étendus) | ⬜ pending |
| 34-06-01 | 06 | 3 | HIS-03 | smoke vue (Measure/Arrange, ActualHeight) | `… --filter "FullyQualifiedName~VueSemaineBindingTests\|FullyQualifiedName~GardeTokensHistoriqueTests\|FullyQualifiedName~HistoriqueBindingTests"` | ❌ W0 | ⬜ pending |
| 34-06-02 | 06 | 3 | HIS-02, HIS-06 | smoke vue (annotations, surcouche, 760/1400) | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ W0 | ⬜ pending |
| 34-07-01 | 07 | 3 | HIS-04 | smoke vue Jour (hauteurs, heures, modèles) | `… --filter "FullyQualifiedName~VueJourBindingTests\|FullyQualifiedName~GardeTokensHistoriqueTests\|FullyQualifiedName~HistoriqueBindingTests"` | ❌ W0 | ⬜ pending |
| 34-07-02 | 07 | 3 | HIS-04 | smoke vue Jour (annotations, maintenant, infobulle) | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ W0 | ⬜ pending |
| 34-08-01 | 08 | 4 | HIS-06 | honnêteté bout en bout (mots + trace) | `… --filter "FullyQualifiedName~HonneteteHistoriqueTests\|FullyQualifiedName~VueSemaineBindingTests\|FullyQualifiedName~VueJourBindingTests\|FullyQualifiedName~HistoriqueBindingTests"` | ❌ W0 | ⬜ pending |
| 34-08-02 | 08 | 4 | HIS-06, HIS-07 | gardes textuelles + GATE (suite × 2, Release) | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ W0 (`GardeVocabulaireHistoriqueTests`) | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

Chaque « ❌ W0 » désigne une classe de tests créée par la tâche elle-même en RED (TDD par plan) : aucune dépendance à une vague 0 séparée. Les fixtures et fakes existants (32-06 journal, 33-04 tokens, `FakeUiDispatcher`, `FakeClock`, `FakeEtatJournal`, `FakeEtatReconstruction`, `FabriqueJournal`) sont ✅.

---

## Wave 0 Requirements

- [ ] `tests/Chronos.Tests/Fakes/FakeSourceHistorique.cs` — source en mémoire (enveloppe `SourceHistoriqueDemonstration`), compte les lectures, `TransformerSemaine`, `Barriere` — créé en 34-03 Task 3
- [ ] `tests/Chronos.Tests/Fakes/FakeUiDispatcherDiffere.cs` — file de `Post` + `Vider()` pour prouver la coalescence F2 — créé en 34-03 Task 3
- [ ] `src/Chronos/ViewModels/Historique/ScenariosHistorique.cs` + `SourceHistoriqueDemonstration.cs` — semaine de référence en mémoire partagée galerie / tests — créés en 34-03 Task 2
- [ ] helper `Rendre(FrameworkElement, w, h)` (`RenderTargetBitmap`) + `PisteBase.TracerPourTests` / `TraceRendu` — créés en 34-04 Task 1
- [ ] helpers de montage `Monter` / `PistesVisibles` / `TousLesTextBlocks` — 34-05 / 34-06 / 34-07 (recopiés ou partagés `internal static`)
- Framework : aucun à installer (xUnit + StaFact présents).

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Revue visuelle DAEDALUS (DESIGN_PLAN §8) : trois styles A/B/C sur les mêmes données, Jour avec reset + plateau épuisé + infobulle, aucune valeur hors tokens, aucun texte tronqué à 100 % et 150 % d'échelle Windows, redimensionnement 760 → 1 400 px sans chevauchement, libellés d'honnêteté présents mot pour mot | HIS-01…HIS-08 (rendu perçu) | Le rendu perçu (lisibilité, densité, coins DWM ≈ 8 px vs 16, infobulle au bord droit) n'est pas décidable par un test ; l'agent ne lance jamais l'overlay ni la galerie | APRÈS le GATE TESTS (34-08) : l'UTILISATEUR lance `Chronos.exe --historique` (ou `dotnet run --project src/Chronos -- --historique`), capture Semaine en Pistes / Simplifié / Tuiles (sélecteur), Jour jeu. 24 (« Aujourd'hui ») et mer. 23 (‹), redimensionne 760 → 1 400, teste 100 % et 150 % ; l'orchestrateur soumet les captures à la revue DAEDALUS (ZEUS phase 6) |
| Coins arrondis et ombre DWM sur Windows 11 (best-effort), coins droits sur Windows 10 | HIS-01 | API DWM non observable hors écran | Même session de captures : constater les coins ; noter l'écart 8 px vs 16 px du plan |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (chaque classe de tests est créée en RED par sa tâche)
- [x] No watch-mode flags
- [x] Feedback latency < 15s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending (revue DAEDALUS sur la galerie après le GATE TESTS)
