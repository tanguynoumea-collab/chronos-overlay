---
phase: 03-mod-les-pipeline-de-donn-es
plan: 03
subsystem: data-pipeline
tags: [dotnet, system-text-json, jsonl, streaming, tolerant-parsing, composite, dependency-injection, tdd]

# Dependency graph
requires:
  - phase: 03-mod-les-pipeline-de-donn-es (plan 01)
    provides: "Records neutres UsageSnapshot/WindowState, enums SourceReliability/WindowKind, contrat IUsageProvider, IClock/FakeClock, ChronosPaths, garde de purete WPF"
  - phase: 03-mod-les-pipeline-de-donn-es (plan 02)
    provides: "Source primaire ClaudeUsageObjectProvider (Exact) + pont statusLine usage.json"
provides:
  - "JsonlEstimationProvider : repli honnete par somme de tokens JSONL en streaming tolerant (input+output+cache_creation+cache_read), fenetre glissante 5 h / 7 j, toujours Estimated, Utilization/ResetsAt/Fraction null (DAT-05, ROB-02)"
  - "Inclusion INTENTIONNELLE du sous-dossier subagents/ dans la somme (scan recursif AllDirectories, meme pool de quota, aucun filtre d'exclusion) — prouvee par test SubagentsRoot"
  - "CompositeUsageProvider : bascule PAR FENETRE Exact>Estimated>Unavailable, emet SnapshotChanged (DAT-06)"
  - "Enregistrement DI Singleton du pipeline complet dans App.xaml.cs : IUsageProvider resout CompositeUsageProvider(primaire, repli)"
affects: [phase-04-orchestration, phase-05-cadran-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Streaming JSONL ligne par ligne (StreamReader.ReadLineAsync) + JsonDocument.Parse par ligne + try/catch (JsonException) -> tolerance ROB-02 sans charger le fichier entier"
    - "FileShare.ReadWrite sur les transcripts (Claude Code ecrit en parallele) ; IOException par fichier -> continue"
    - "Filtre assistant structure (type==assistant ET message.role==assistant) -> jamais de faux positif sur la prose five_hour/seven_day"
    - "Composite sans etat : selection par fenetre via helper Best pur (comparaison de Reliability)"
    - "DI Singleton : deux providers concrets + factory IUsageProvider=CompositeUsageProvider, chemins via ChronosPaths.Default() (Environment, jamais Assembly.Location)"

key-files:
  created:
    - src/Chronos/Services/JsonlEstimationProvider.cs
    - src/Chronos/Services/CompositeUsageProvider.cs
    - tests/Chronos.Tests/JsonlEstimationProviderTests.cs
    - tests/Chronos.Tests/CompositeUsageProviderTests.cs
    - tests/Chronos.Tests/TestData/sample-valid.jsonl
    - tests/Chronos.Tests/TestData/sample-tolerant.jsonl
    - tests/Chronos.Tests/TestData/SubagentsRoot/session.jsonl
    - tests/Chronos.Tests/TestData/SubagentsRoot/subagents/agent-abc.jsonl
  modified:
    - src/Chronos/App.xaml.cs

key-decisions:
  - "Scan recursif AllDirectories inclut subagents/ dans la SOMME de tokens (meme pool de quota) — arbitrage phase 3, aucun filtre ; exploitation structuree differee V2-01"
  - "Estimation honnete : somme exacte mais % quota inconnu -> toujours Estimated, Utilization/ResetsAt/Fraction null (jamais invente)"
  - "Composite appelle les deux GetAsync (pas de court-circuit paresseux) : suffisant et testable en Phase 3, court-circuit = raffinement Phase 4"
  - "Isolation des fixtures a fichier unique : copie dans un dossier temp dedie au moment du test, pour que le scan recursif ne voie QUE le fichier vise (structure de test, discretion Claude)"

patterns-established:
  - "Provider de repli tolerant : jamais d'exception, jamais de valeur inventee, degradation vers 0 token / Estimated"
  - "Composite par fenetre : la granularite de bascule est la fenetre, pas le snapshot entier"

requirements-completed: [DAT-05, DAT-06, ROB-02]

# Metrics
duration: 6min
completed: 2026-07-08
---

# Phase 3 Plan 03: JsonlEstimationProvider + CompositeUsageProvider + cablage DI Summary

**Repli JSONL honnete (somme de tokens streaming tolerant, session principale + subagents/ = meme pool, toujours Estimated), composite qui prend la meilleure source par fenetre (Exact>Estimated>Unavailable), et graphe DI Singleton ou IUsageProvider resout un CompositeUsageProvider operationnel.**

## Performance

- **Duration:** ~6 min
- **Started:** 2026-07-08T14:55:14Z
- **Completed:** 2026-07-08T15:01:08Z
- **Tasks:** 3 (Task 1 & 2 en TDD, Task 3 auto)
- **Files modified:** 9 (2 providers, 2 tests, 4 fixtures JSONL, 1 App.xaml.cs)

## Accomplishments
- **`JsonlEstimationProvider`** (DAT-05 + ROB-02) : somme `input+output+cache_creation+cache_read` des lignes `assistant` dont le `timestamp` ISO 8601 tombe dans la fenetre glissante (5 h / 7 j depuis `now`), en streaming `FileShare.ReadWrite`. Parsing tolerant par ligne (`try/catch (JsonException)`) : ligne corrompue, derniere ligne tronquee, champ manquant, ligne `user` et prose `five_hour` ignores ; dossier absent -> 0 token. Toujours `Estimated`, `Utilization`/`ResetsAt`/`FractionTimeRemaining` = null (jamais invente).
- **Inclusion intentionnelle des subagents/** : scan `Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories)` — sans aucun filtre d'exclusion — additionne les tokens du sous-dossier `subagents/` (meme pool de quota compte). Prouve par le test `SubagentsRoot` (session 500 + agent 300 = 800).
- **`CompositeUsageProvider`** (DAT-06) : selection PAR FENETRE via `Best(primary, fallback)` = `Exact ? primary : Estimated ? fallback : primary`. Emet `SnapshotChanged` une fois avec le snapshot final. Ne crashe pas quand les deux sources sont Unavailable.
- **Cablage DI complet** (`App.xaml.cs`) : `IClock`/`SystemClock`, `ChronosPaths.Default()`, les deux providers concrets et `IUsageProvider = CompositeUsageProvider(primaire, repli)` en Singleton, sans casser les 4 enregistrements Phase 1.
- **Suite complete verte : 27 tests** (18 anterieurs + 4 Jsonl + 5 Composite) ; garde de purete WPF toujours verte (les deux providers sont neutres). Build solution 0 avertissement / 0 erreur.

## Task Commits

Each task was committed atomically:

1. **Task 1: JsonlEstimationProvider + fixtures (TDD)** - `e7644e1` (test RED) → `7f930f4` (feat GREEN)
2. **Task 2: CompositeUsageProvider — bascule par fenetre (TDD)** - `3f0690e` (test RED) → `2e9d9d5` (feat GREEN)
3. **Task 3: Enregistrement DI du pipeline dans App.xaml.cs** - `ed709d2` (feat)

**Plan metadata:** _(commit docs de fin de plan)_

_Note: Tasks 1 & 2 en TDD (RED → GREEN, pas de refactor necessaire — code provider verbatim du RESEARCH)._

## Files Created/Modified
- `src/Chronos/Services/JsonlEstimationProvider.cs` - Repli estime : somme de tokens JSONL streaming tolerant, inclut subagents/, toujours Estimated
- `src/Chronos/Services/CompositeUsageProvider.cs` - Bascule primaire->repli par fenetre (Best) + SnapshotChanged
- `src/Chronos/App.xaml.cs` - ConfigureServices etendu : pipeline de donnees Singleton, IUsageProvider = Composite
- `tests/Chronos.Tests/JsonlEstimationProviderTests.cs` - 4 tests (somme par fenetre, tolerance, subagents inclus, dossier absent)
- `tests/Chronos.Tests/CompositeUsageProviderTests.cs` - 5 tests (bascule par fenetre + SnapshotChanged), fakes IUsageProvider
- `tests/Chronos.Tests/TestData/sample-valid.jsonl` - 3 lignes assistant (recente <5h, mid <7d, ancienne >7d exclue)
- `tests/Chronos.Tests/TestData/sample-tolerant.jsonl` - valide + corrompue + user(prose) + derniere ligne tronquee
- `tests/Chronos.Tests/TestData/SubagentsRoot/session.jsonl` - ligne assistant session principale (500 tokens)
- `tests/Chronos.Tests/TestData/SubagentsRoot/subagents/agent-abc.jsonl` - ligne assistant sous-agent (300 tokens)

## Decisions Made
- **Subagents inclus dans la somme** : `SearchOption.AllDirectories` sans filtre — les sous-agents consomment le meme pool de quota compte, donc leurs tokens comptent pour l'estimation d'usage (arbitrage phase 3). L'exploitation STRUCTUREE (bande d'activite) reste differee V2-01.
- **Estimation honnete** : la somme de tokens est exacte, mais sa traduction en % de quota est inconnue (plafonds non publies/mouvants) -> `SourceReliability.Estimated` toujours, `Utilization`/`ResetsAt`/`FractionTimeRemaining` = null. Le modele nullable-safe (03-01) accueillera un plafond calibre (b/c) sans refonte.
- **Composite sans court-circuit** : les deux `GetAsync` sont appeles ; la paresse (ne scanner le JSONL que si une fenetre primaire manque) est un raffinement perf de Phase 4, note dans le code.
- **Isolation des fixtures a fichier unique** : `sample-valid.jsonl` / `sample-tolerant.jsonl` sont copies dans un dossier temp dedie au moment du test, car le scan recursif verrait sinon plusieurs `*.jsonl` de `TestData/` en meme temps. `SubagentsRoot/` est auto-suffisant et pointe directement (prouve la recursion). Detail de structure de test (discretion Claude), sans effet sur le comportement du provider.

## Deviations from Plan

None - plan executed exactly as written.

Les fixtures ont ete placees aux chemins listes par le plan ; l'isolation par copie temp est un choix de structure de test (dans la discretion « structure exacte des tests » du RESEARCH), pas un ecart de comportement.

**Total deviations:** 0.
**Impact on plan:** Plan livre verbatim ; providers et cablage conformes au RESEARCH.

## Issues Encountered
None — les 3 taches ont ete executees dans l'ordre prevu. Seuls des avertissements git cosmetiques de fin de ligne (LF -> CRLF) sont apparus, sans effet sur le parsing (StreamReader gere LF/CRLF, derniere ligne tronquee sans newline preservee).

## User Setup Required
None - aucune configuration de service externe requise dans ce plan. L'installation du pont statusLine (`~/.claude/settings.json`) est le perimetre du plan 03-04.

## Next Phase Readiness
- **Success criterion 1 de la phase VRAI** : `IUsageProvider` resout un `CompositeUsageProvider` operationnel qui renvoie un snapshot (primaire si `usage.json` lisible, sinon estime JSONL).
- Pipeline de donnees complet et cable en DI Singleton : pret pour l'orchestration Phase 4 (watcher/timer branchant `SnapshotChanged`) et le cadran UI Phase 5.
- Garde de purete WPF toujours verte : les deux nouveaux providers sont neutres (System.Text.Json / System.IO / System.Globalization uniquement).

## Self-Check: PASSED

- 9/9 fichiers crees/modifies presents sur disque.
- 5/5 commits de tache presents (e7644e1, 7f930f4, 3f0690e, 2e9d9d5, ed709d2).
- Build solution : 0 erreur / 0 avertissement. Suite complete : 27 tests verts (dont ServicesLayerPurityTests).

---
*Phase: 03-mod-les-pipeline-de-donn-es*
*Completed: 2026-07-08*
