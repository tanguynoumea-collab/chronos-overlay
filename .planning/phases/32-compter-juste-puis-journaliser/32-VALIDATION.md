---
phase: 32
slug: compter-juste-puis-journaliser
status: planned
nyquist_compliant: false
wave_0_complete: false
created: 2026-09-27
planned: 2026-09-27
---

# Phase 32 — Validation Strategy

> Contrat de validation par phase, échantillonné pendant l'exécution. Rempli à la planification (2026-09-27) depuis la section
> « Validation Architecture » de `32-RESEARCH.md` et les huit PLAN.md ; les colonnes « Status » et les valeurs mesurées sont
> complétées par les SUMMARY et signées en 32-08 (tâche 7).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.2 + Xunit.StaFact 1.1.11 (Microsoft.NET.Test.Sdk 17.11.1), `net8.0-windows` |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` (chemins `CheminSourcesChronos` et `CheminDocsChronos` injectés par MSBuild — les gardes textuelles et documentaires en dépendent) |
| **Quick run command** | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q --filter "FullyQualifiedName~<Classe>"` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` |
| **Estimated runtime** | ~10 s (1200 / 0 à l'entrée de la phase, mesuré le 2026-09-27 ; attendu ≈ 1 300 en sortie) |

Aucun paquet à installer. Les projets de test sont `UseWPF` (`using System.IO;` explicite) ; les tests qui chargent du BAML (`ReglagesBindingTests`) sont en `[WpfFact]`.

---

## Sampling Rate

- **After every task commit:** `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q --filter "FullyQualifiedName~<classe(s) de la tâche>"` (le filtre exact est dans le `<verify>` de chaque tâche)
- **After every plan (fin de plan) :** `dotnet test Chronos.sln -c Debug --nologo -v q` **deux fois de suite**, 0 échec
- **After every plan wave:** suite complète (fusion des plans parallèles de la vague : 01 ∥ 02 ∥ 03 ∥ 04 ; 05 ∥ 06)
- **Before `/gsd:verify-work`:** suite complète verte deux fois + `dotnet build src/Chronos/Chronos.csproj -c Release` sans warning nouveau
- **Max feedback latency:** ~10 s (suite complète) ; < 5 s par filtre
- **Mutations :** chaque plan de code joue au moins une mutation nommée sur sa logique critique, constate le rouge, révoque par copie (sha256 identique) — consignées au SUMMARY

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 32-01-01 | 01 | 1 | CPT-01 | unit (pur) | `dotnet test … --filter "FullyQualifiedName~DedupUsageTests"` | ❌ W0 (`DedupUsageTests.cs`) | ⬜ pending |
| 32-01-02 | 01 | 1 | CPT-01 | unit (E/S temp) + integration (tête + vrai provider) | `… --filter "FullyQualifiedName~TranscriptActivityProviderTests\|…~DedupHeritageDeltaTests\|…~TranscriptActivityLogTests\|…~LastExactUsageProviderTests\|…~DoctrineFraicheurTests"` | ✅ classe / ❌ W0 (fixture `transcript-multi-blocs.jsonl`, `DedupHeritageDeltaTests.cs`) | ⬜ pending |
| 32-01-03 | 01 | 1 | CPT-01 | garde textuelle + réflexive | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ W0 (`GardesDedupUsageTests.cs`) | ⬜ pending |
| 32-02-01 | 02 | 1 | CPT-02 | unit (panne reproduite : dossier parent = fichier) | `… --filter "FullyQualifiedName~LastExactStoreTests\|…~LastExactUsageProviderTests\|…~ServicesLayerPurityTests"` | ✅ classes, ❌ cas | ⬜ pending |
| 32-02-02 | 02 | 1 | CPT-02 | unit (rapport texte) + unit pur (vue AppData) | `… --filter "FullyQualifiedName~DiagnosticServiceTests\|…~VueAppDataTests\|…~GardesPerimetreTests\|…~LectureSeuleAppBureauTests\|…~ServicesLayerPurityTests"` | ✅ classe / ❌ W0 (`VueAppDataTests.cs`) | ⬜ pending |
| 32-02-03 | 02 | 1 | CPT-02 | docs (STATE.md) + suite complète ×2 | `dotnet test Chronos.sln -c Debug --nologo -v q` | ✅ | ⬜ pending |
| 32-03-01 | 03 | 1 | CPT-03 | unit (deux threads, abandon) | `… --filter "FullyQualifiedName~VerrouInstanceUniqueTests\|…~ServicesLayerPurityTests"` | ❌ W0 (`VerrouInstanceUniqueTests.cs`) | ⬜ pending |
| 32-03-02 | 03 | 1 | CPT-03 | unit (pur, listes injectées) | `… --filter "FullyQualifiedName~InventaireProcessusTests\|…~ServicesLayerPurityTests"` | ❌ W0 (`InventaireProcessusTests.cs`) | ⬜ pending |
| 32-03-03 | 03 | 1 | CPT-03 | garde textuelle App.xaml.cs + suite complète ×2 | `dotnet test Chronos.sln -c Debug --nologo -v q` | ✅ (`GardesPerimetreTests.cs`, +2 cas) | ⬜ pending |
| 32-04-01 | 04 | 1 | JRN-03 | unit (sérialisation, parsing tolérant) + gardes élargies | `… --filter "FullyQualifiedName~LigneJournalTests\|…~ServicesLayerPurityTests\|…~GardesDoctrineTests\|…~NormalisationUniqueTests"` | ❌ W0 (`LigneJournalTests.cs`, fixture `journal/tolerance/…`) | ⬜ pending |
| 32-04-02 | 04 | 1 | JRN-01, JRN-03 | unit + concurrence (deux écrivains, 200 écritures) | `… --filter "FullyQualifiedName~JournalRelevesTests\|…~LigneJournalTests\|…~ServicesLayerPurityTests"` | ❌ W0 (`JournalRelevesTests.cs`) | ⬜ pending |
| 32-04-03 | 04 | 1 | JRN-01, JRN-02 | unit (décorateur, événements sur transition) + suite complète ×2 | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ W0 (`JournalisationUsageProviderTests.cs`) | ⬜ pending |
| 32-05-01 | 05 | 2 | JRN-04 (câblage JRN-01/02/03, CPT-02/03) | garde DI + garde textuelle | `… --filter "FullyQualifiedName~CompositionRootTests\|…~GardesPerimetreTests\|…~ServicesLayerPurityTests\|…~JournalisationUsageProviderTests"` | ✅ classes, ❌ cas | ⬜ pending |
| 32-05-02 | 05 | 2 | JRN-04 | unit (rapport texte) | `… --filter "FullyQualifiedName~DiagnosticServiceTests\|…~GardesPerimetreTests\|…~LectureSeuleAppBureauTests"` | ✅ classe, ❌ cas | ⬜ pending |
| 32-05-03 | 05 | 2 | JRN-04 | unit VM (FakeClock) + [WpfFact] binding + suite complète ×2 | `dotnet test Chronos.sln -c Debug --nologo -v q` | ✅ classes / ❌ W0 (`Fakes/FakeEtatJournal.cs`) | ⬜ pending |
| 32-06-01 | 06 | 2 | JRN-05 | unit (E/S temp sur fixtures) | `… --filter "FullyQualifiedName~LecteurJournalTests\|…~LigneJournalTests\|…~ServicesLayerPurityTests"` | ❌ W0 (`LecteurJournalTests.cs`, 8 fixtures, `Fakes/FabriqueJournal.cs`) | ⬜ pending |
| 32-06-02 | 06 | 2 | JRN-05 | unit (pur, TimeZoneInfo injecté, DST) | `… --filter "FullyQualifiedName~BornesPlageTests\|…~ServicesLayerPurityTests"` | ❌ W0 (`BornesPlageTests.cs`) | ⬜ pending |
| 32-06-03 | 06 | 2 | JRN-05 | unit (pur) + suite complète ×2 | `dotnet test Chronos.sln -c Debug --nologo -v q` | ❌ W0 (`AnalyseRelevesTests.cs`) | ⬜ pending |
| 32-07-01 | 07 | 3 | JRN-06 | garde documentaire (doc ↔ types par réflexion) | `… --filter "FullyQualifiedName~ContratJournalDocumenteTests\|…~ContratHooksDocumenteTests"` | ❌ W0 (`ContratJournalDocumenteTests.cs`) | ⬜ pending |
| 32-07-02 | 07 | 3 | JRN-06 | suite complète ×2 + contrôles de publication (grep csproj, VersionInfo, md5, 0 DLL, smoke `--hook`) | `dotnet test Chronos.sln -c Debug --nologo -v q` | ✅ (`VersionPublieeTests`) | ⬜ pending |
| 32-08-01 | 08 | 4 | VAL-04 | script (structure du constat, anonymisation) | `python -c "…"` (voir plan) | — | ⬜ pending |
| 32-08-02 | 08 | 4 | VAL-04 | **manual** (point (a), utilisateur) | — | — | ⬜ pending |
| 32-08-03 | 08 | 4 | VAL-04 | script (réconciliation dans `~/.claude/settings.json`) + sonde WMI | `python -c "…"` (voir plan) | — | ⬜ pending |
| 32-08-04 | 08 | 4 | VAL-04 | **manual** (point (b), utilisateur) | — | — | ⬜ pending |
| 32-08-05 | 08 | 4 | VAL-04 | script (§1 rempli) + sonde WMI T+10 | `python -c "…"` (voir plan) | — | ⬜ pending |
| 32-08-06 | 08 | 4 | VAL-04 | **manual** (point (c), utilisateur) | — | — | ⬜ pending |
| 32-08-07 | 08 | 4 | VAL-04 | script (§2, §4, VALIDATION signée) + sonde WMI T+60 | `python -c "…"` (voir plan) | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

Continuité d'échantillonnage : aucune suite de trois tâches sans commande automatisée (les points de contrôle humains 32-08-02/04/06 sont chacun encadrés par une tâche à script).

---

## Wave 0 Requirements

Rien à installer ; chaque fichier ci-dessous est créé par la tâche qui l'utilise (RED d'abord) :

- [ ] `tests/Chronos.Tests/TestData/transcript-multi-blocs.jsonl` — fixture réelle anonymisée (3 lignes même `message.id`, `output_tokens` 8/8/256, doublon strict, 2 lignes `requestId` seul, 1 sans id) — CPT-01 (32-01-02)
- [ ] `tests/Chronos.Tests/DedupUsageTests.cs`, `DedupHeritageDeltaTests.cs`, `GardesDedupUsageTests.cs` — CPT-01 (32-01)
- [ ] `tests/Chronos.Tests/VueAppDataTests.cs` — CPT-02 (32-02-02)
- [ ] `tests/Chronos.Tests/VerrouInstanceUniqueTests.cs`, `InventaireProcessusTests.cs` — CPT-03 (32-03)
- [ ] `tests/Chronos.Tests/TestData/journal/tolerance/releves-2026-09.jsonl` — JRN-03 (32-04-01)
- [ ] `tests/Chronos.Tests/LigneJournalTests.cs`, `JournalRelevesTests.cs`, `JournalisationUsageProviderTests.cs` — JRN-01/02/03 (32-04)
- [ ] Élargissement de `ServicesLayerPurityTests` (namespace `StartsWith`), `GardesDoctrineTests` ×2 et `NormalisationUniqueTests` (`SearchOption.AllDirectories`) + test « la garde voit `Chronos.Services.Historique` » — JRN-03 (32-04-01)
- [ ] `tests/Chronos.Tests/Fakes/FakeEtatJournal.cs` — JRN-04 (32-05-03)
- [ ] `tests/Chronos.Tests/TestData/journal/{trou-arrete, trou-jeton, reset-5h-milieu-heure, deux-resets-hebdo, jour-dst-2026-10-25, a-cheval}/releves-AAAA-MM.jsonl` (8 fichiers) + `Fakes/FabriqueJournal.cs` (journée nominale 288 relevés) — JRN-05 (32-06-01)
- [ ] `tests/Chronos.Tests/LecteurJournalTests.cs`, `BornesPlageTests.cs`, `AnalyseRelevesTests.cs` — JRN-05 (32-06)
- [ ] `tests/Chronos.Tests/ContratJournalDocumenteTests.cs` — JRN-06 (32-07-01)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Une seule instance en vrai : second double-clic → « Chronos tourne déjà… », un seul cadran ; trois anciennes quittées | CPT-03 / VAL-04 | L'agent ne lance, n'arrête ni ne clique jamais l'overlay ; les exe antérieurs ne connaissent pas le mutex | 32-08 tâche 2 (point (a)), relevé par sonde WMI en tâche 3 |
| Réconciliation hooks/statusLine au premier lancement de la 3.2.2 | JRN-06 / VAL-04 | Se produit au premier lancement en mode overlay, par l'utilisateur | 32-08 tâche 3 : sauvegarde md5 = temps 0, 9 remplacements 3.2.1 → 3.2.2, 8 hooks, statusLine, 3 `gsd-` |
| Le journal s'écrit sur la machine (vue RÉELLE) : âge < 6 min à T+10, ≥ 2 relevés, `demarrage` en tête ; ≈ 12 relevés à T+60 | JRN-01 / JRN-02 / VAL-04 | AppData est virtualisé pour tout processus lancé sous l'app bureau (dont les tests) : seule une sonde WMI hors arbre voit ce que l'overlay écrit | 32-08 tâches 3, 5, 7 (sondes différées `Start-Sleep`, preuve parent `WmiPrvSE` + `%APPDATA%\Claude` absent) |
| Diagnostic en production : « Vue AppData : réelle », « dernière écriture il y a N min », « Processus Chronos : 1 », « Verrou … tenu par ce processus », pas d'ALERTE | CPT-02 / JRN-04 / VAL-04 | Le rapport dépend de la vue réelle et du processus vivant | 32-08 tâches 4 et 6 (rapport collé par l'utilisateur), relevé en 5 et 7 |
| Tableau des gestes L1, L2, L2b, L3, L4, Q et vérifications V01…V12 (dette VAL-03 de v1.7) | VAL-04 | Comportement visuel et interactif du widget avec l'app bureau | 32-08 tâches 4 et 6, protocole de `31-CONSTAT.md` inchangé |
| Hypothèses HYP-1/2/3 de `docs/data-sources.md` §7 (granularité, Δ = consommation, reset hebdo local le 25/10/2026) | JRN-06 | Ne se vérifient qu'avec des semaines de journal réel | Hors phase : relecture du journal (CAD-XX v2 ; échéance HYP-3 le 25/10/2026) |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies (les 3 points de contrôle humains sont manual-only par nature et encadrés)
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15 s
- [ ] `nyquist_compliant: true` set in frontmatter (à poser en 32-08 tâche 7)

**Approval:** pending — signée en 32-08 (tâche 7) avec le verdict du constat.
