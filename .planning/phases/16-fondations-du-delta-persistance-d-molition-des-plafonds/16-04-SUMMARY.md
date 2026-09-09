---
phase: 16-fondations-du-delta-persistance-d-molition-des-plafonds
plan: 04
subsystem: services
tags: [persistence, system-text-json, schema-evolution, backward-compatibility, dead-code-removal, reflection-guard]

# Dependency graph
requires:
  - phase: 16-01
    provides: la démolition du dialogue, de la commande et du calibrateur de plafonds — qui laissait ChronosSettings seul référent de BudgetSource
  - phase: 16-03
    provides: la conversion des transcripts en source de delta, qui rendait factice le titre « Estimation » du diagnostic
provides:
  - ChronosSettings amputé de ses six champs de plafonds — 18 propriétés, plus aucune référence au sous-système démoli
  - Compatibilité ascendante DEL-06 PROUVÉE sur une fixture figée du vrai %APPDATA%\Chronos\settings.json de production
  - TestData/settings-legacy-plafonds.json — état de production v2.8.1 figé comme preuve exécutable
  - Garde réflexive permanente : aucun type Budget* ne peut réapparaître dans l'assembly sans faire échouer la suite
  - DiagnosticService purgé de ses trois dernières accroches de plafonds
affects: [19 doctrine du composite et correction par delta, 20 refonte du diagnostic EXA-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Évolution de schéma par SUPPRESSION prouvée par un test de caractérisation sur fixture de production figée, jamais par un migrateur"
    - "Fixture d'état réel copiée octet pour octet depuis la machine, jamais régénérée par sérialisation — une fixture générée par le code courant ne peut pas contenir ce que ce code ne sait plus produire"
    - "Garde de non-retour par réflexion sur le NOM des types (préfixe), qui résiste au copier-coller depuis l'historique git"
    - "Falsifiabilité d'une garde vérifiée en exécution : type témoin introduit, échec constaté, témoin retiré"

key-files:
  created:
    - tests/Chronos.Tests/TestData/settings-legacy-plafonds.json
  modified:
    - src/Chronos/Services/ChronosSettings.cs
    - src/Chronos/Services/DiagnosticService.cs
    - src/Chronos/ViewModels/MainViewModel.cs
    - tests/Chronos.Tests/SettingsServiceTests.cs
    - tests/Chronos.Tests/ServicesLayerPurityTests.cs
    - .planning/phases/16-fondations-du-delta-persistance-d-molition-des-plafonds/16-VALIDATION.md
  deleted:
    - src/Chronos/Services/BudgetSource.cs

key-decisions:
  - "DEL-06 est livré comme un TEST, pas comme du code : System.Text.Json ignore par défaut les membres non mappés, et le skip a lieu au niveau du LECTEUR, avant toute conversion — d'où l'innocuité d'un type incohérent comme d'une valeur d'enum devenue invalide. Un SettingsMigrator aurait été du code mort à maintenir pour un problème inexistant."
  - "La fixture est une copie octet pour octet du %APPDATA%\\Chronos\\settings.json réel de la machine, pas une reconstruction : le code courant ne sait plus produire les six champs obsolètes, une fixture générée n'aurait rien prouvé."
  - "La garde de non-retour porte sur le NOM des types (préfixe « Budget »), pas sur une liste de types connus : elle attrape aussi une réintroduction sous une forme nouvelle, et sa falsifiabilité a été vérifiée en exécution."
  - "Le trou visuel de la UniformGrid « RÉGLAGES » (3 boutons dans 2 colonnes) est CONSIGNÉ, pas corrigé : la seule correction sans troncature de libellé est un arbitrage de design qui demande un œil sur l'écran, et relève d'EXA-06 (phase 20)."
  - "Les mentions doctrinales du mot « plafond » qui EXPLIQUENT pourquoi le sous-système a été démoli (TranscriptActivityProvider, PercentFormatter, WindowState) sont conservées : le critère structurel réel est l'absence de type Budget*, pas l'absence du mot."

patterns-established:
  - "Prouver une compatibilité ascendante sur l'artefact de production RÉEL de l'utilisateur, figé comme fixture, plutôt que sur un cas d'école reconstruit."
  - "Une garde permanente doit être falsifiée avant d'être committée : sinon rien ne distingue une garde qui protège d'une garde qui ne teste rien."

requirements-completed: [DEL-05, DEL-06]

# Metrics
duration: 24min
completed: 2026-09-09
---

# Phase 16 Plan 04 : Amputation du schéma de réglages et preuve de survie Résumé

**Les six champs de plafonds et l'enum `BudgetSource` retirés de `ChronosSettings`, avec la compatibilité ascendante prouvée — sans une ligne de migration — par une fixture figée du vrai `settings.json` de production, et verrouillée par une garde réflexive qui interdit tout retour d'un type `Budget*`.**

## Performance

- **Duration:** 24 min
- **Started:** 2026-09-09T12:52:00Z
- **Completed:** 2026-09-09T13:16:00Z
- **Tasks:** 3/3
- **Files modified:** 7 (1 créé, 5 modifiés, 1 supprimé)
- **Suite :** 414 → **419 tests, 0 échec**

## Accomplishments

- `ChronosSettings` passe de 24 à **exactement 18 propriétés** — les six champs de plafonds
  (`FiveHourTokenBudget`, `WeeklyTokenBudget`, `FiveHourBudgetSource`, `WeeklyBudgetSource`,
  `FiveHourBudgetCalibratedAt`, `WeeklyBudgetCalibratedAt`) ont disparu, et `BudgetSource.cs` avec eux.
- **DEL-06 prouvé sans aucun code de migration** : le `settings.json` réel de la machine, portant les
  six champs obsolètes dont l'enum supprimé `"FiveHourBudgetSource": "Manual"`, s'ouvre sans exception
  et restitue les 18 préférences à l'identique — offset `+02:00` de `WeeklyAnchor` compris.
- **DEL-05 réellement refermé** : `grep -rn "Budget" src/Chronos` ne renvoie plus rien, et une garde
  réflexive fait désormais échouer la suite si un type `Budget*` réapparaît dans l'assembly.
- Le diagnostic ne mentionne plus ni plafond ni calibration : section « Estimation — transcripts JSONL
  (repli) » renommée « Transcripts JSONL (source de delta) », lignes de plafonds retirées, conseil
  « Calibrer les plafonds… » retiré, libellé « % inconnu (pas de plafond → gris) » ramené à « % inconnu ».

## Task Commits

1. **Task 1 : Retirer les six champs, supprimer BudgetSource et purger le diagnostic** — `d6023d3` (refactor)
2. **Task 2 : Figer la fixture du settings.json réel et prouver DEL-06** — `a94793d` (test)
3. **Task 3 : Poser la garde de non-retour DEL-05 par réflexion** — `2383a98` (test)

## Files Created/Modified

- `src/Chronos/Services/ChronosSettings.cs` — 6 propriétés retirées (24 → 18) ; XML-doc du record
  expliquant pourquoi aucune migration n'est nécessaire et où se trouve la preuve.
- `src/Chronos/Services/BudgetSource.cs` — **supprimé** (`git rm`), après les champs qui le référençaient.
- `src/Chronos/Services/DiagnosticService.cs` — trois accroches de plafonds retirées + en-tête de classe
  corrigé (« sources présentes ? plafonds ? » → « sources présentes ? »).
- `src/Chronos/ViewModels/MainViewModel.cs` — XML-doc de `BuildDiagnosticReportAsync` : mention
  « plafonds » retirée de la liste des rubriques du rapport, devenue fausse.
- `tests/Chronos.Tests/TestData/settings-legacy-plafonds.json` — **créé** : copie octet pour octet du
  `%APPDATA%\Chronos\settings.json` de production au 2026-09-09 (6 obsolètes + 18 survivants).
- `tests/Chronos.Tests/SettingsServiceTests.cs` — 4 lignes visant les champs disparus retirées, **4 tests
  DEL-06 ajoutés** (5 → 9 tests, aucun test supprimé).
- `tests/Chronos.Tests/ServicesLayerPurityTests.cs` — `[Fact]` de non-retour `Budget*` (1 → 2 tests).
- `.planning/.../16-VALIDATION.md` — les 3 tâches passées en ✅ green avec leurs hashes ; résultat de
  l'inspection manuelle consigné.

## Decisions Made

**1. DEL-06 livré comme test, pas comme code — l'interdit du plan tenu.**
Aucun type `SettingsMigrator` / `SettingsUpgrader`, aucune méthode `Migrate`, et `SettingsService.cs`
n'a pas bougé d'un octet (`git diff --name-only HEAD -- SettingsService.cs` vide aux trois commits).
Le mécanisme est `JsonUnmappedMemberHandling.Skip`, comportement PAR DÉFAUT de `System.Text.Json` : le
membre non mappé est sauté au niveau du lecteur, sous-arbre compris, avant toute tentative de
conversion. C'est ce qui rend inoffensifs à la fois un type incohérent (`{"a":[1,2,3]}` là où on
attendait un `long`) et une valeur d'enum devenue invalide — les deux sont testés.

**2. Fixture copiée, jamais reconstruite.**
`cp %APPDATA%\Chronos\settings.json tests/Chronos.Tests/TestData/settings-legacy-plafonds.json`.
Le piège que cela évite : le code courant ne sait plus sérialiser les six champs obsolètes, donc une
fixture produite par `JsonSerializer.Serialize(new ChronosSettings())` ne les contiendrait pas et le
test passerait sans rien prouver.

**3. Garde falsifiée avant d'être committée.**
Un `internal enum BudgetRevenantTemporaire` a été introduit temporairement dans `Chronos.Services` :
la garde a bien échoué (`Aucun_type_de_plafond_ne_subsiste_dans_l_assembly [FAIL]`), le témoin a été
retiré, la garde est redevenue verte. Sans cette étape, rien ne distinguait une garde qui protège
d'une garde qui n'observe rien.

**4. Chemin de fixture par `[CallerFilePath]`, pas par copie MSBuild.**
Motif déjà en place dans `TranscriptActivityProviderTests` et `ClaudeUsageObjectProviderTests` ; le
`.csproj` de test n'a pas été touché — pas de troisième mécanisme inventé.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] XML-doc de `MainViewModel.BuildDiagnosticReportAsync` devenue fausse**
- **Found during:** Task 1
- **Issue:** Le commentaire annonçait un rapport « (token, appel OAuth, sources, **plafonds**, résultat) »
  alors que la rubrique plafonds venait d'être retirée du rapport dans le même commit. Documentation
  contredisant le code au sein d'un même geste.
- **Fix:** Mention « plafonds » retirée de l'énumération. Même traitement pour l'en-tête de classe de
  `DiagnosticService` (« sources présentes ? plafonds ? source active » → « sources présentes ? source active »).
- **Files modified:** `src/Chronos/ViewModels/MainViewModel.cs`, `src/Chronos/Services/DiagnosticService.cs`
- **Verification:** `grep -rn "Budget" src/Chronos` → 0 résultat ; suite complète 414/0 au commit.
- **Committed in:** `d6023d3` (commit de la tâche 1)

**2. [Rule 3 — Blocking] Artefact de build obsolète `BudgetDialog.baml` survivant dans `obj/Release`**
- **Found during:** Task 3
- **Issue:** Le critère d'acceptation `find src/Chronos/obj src/Chronos/bin -iname "*Budget*"` → 0 résultat
  échouait : `src/Chronos/obj/Release/net8.0-windows/win-x64/Views/BudgetDialog.baml` subsistait d'une
  publication Release antérieure au plan 16-01. `dotnet clean` (Debug) puis `dotnet clean -c Release` ne
  l'ont pas emporté — l'intermédiaire est sous un RID (`win-x64`) que la cible Clean invoquée ne visitait pas.
  Un `.baml` d'un XAML supprimé qui traîne dans l'arbre intermédiaire peut être réembarqué par une
  publication ultérieure.
- **Fix:** Suppression de l'arbre intermédiaire obsolète `src/Chronos/obj/Release` (entièrement
  régénérable, ignoré par git). `src/Chronos/bin/Release` — qui contient l'exe publié de l'utilisateur —
  n'a PAS été touché.
- **Files modified:** aucun fichier versionné (arbre `obj/` gitignoré)
- **Verification:** `find src/ tests/ -iname "*Budget*"` → 0 résultat ; rebuild + suite complète 419/0.
- **Committed in:** n/a (artefact non versionné)

### Écarts assumés aux critères d'acceptation (documentés, non corrigés)

**a. `grep -rn "[Pp]lafond" src/Chronos` → 0 résultat : critère NON tenu, volontairement.**
Cinq mentions du mot subsistent, toutes **doctrinales** — elles expliquent précisément pourquoi le
sous-système a été démoli, et les effacer détruirait le raisonnement que les phases 19/20 devront
retrouver :
- `TranscriptActivityProvider.cs:14-15` — « le rapport d'un comptage de tokens à un plafond reste faux
  même avec le bon plafond » (posé par le plan 16-03, cœur de la justification DEL-01).
- `PercentFormatter.cs:6,18` — « on ne présente jamais un plafond inventé » (contrainte d'honnêteté
  des chiffres, CLAUDE.md).
- `WindowState.cs:7` — « null si inconnu (repli sans plafond) ».
- `App.xaml.cs:258` — « Plus aucune dépendance à SettingsService : ni plafond, ni ancre hebdo. »

Le critère STRUCTUREL du plan — `grep -rn "Budget" src/Chronos` → 0 résultat — est lui **tenu**, et
c'est celui que la garde réflexive de la tâche 3 fait respecter mécaniquement. La règle défendable est
« plus aucun TYPE de plafond », pas « plus aucune trace du MOT », qui interdirait d'expliquer une
décision d'architecture.

**b. Vérification manuelle (étape 6 du plan) : partiellement automatisable, reste due.**
Le volet « le diagnostic ne mentionne plus aucun plafond » est **prouvé statiquement** (les trois
accroches sont retirées, aucun type `Budget*` ne subsiste). Le volet visuel a fait l'objet d'une
inspection du XAML, consignée dans `16-VALIDATION.md` :
- Aucun **bouton fantôme** : la `UniformGrid` « RÉGLAGES » porte exactement 3 `Button`, l'entrée
  « Plafonds… » a bien disparu du balisage.
- Un **trou** subsiste néanmoins : la grille est restée en `Columns="2"` avec 3 boutons → cellule
  bas-droite vide. **Non corrigé volontairement** : le panneau fait 330 px (16 px de marge de chaque
  côté), soit ~99 px par colonne en `Columns="3"`, alors que « Recalibrer hebdo… » à `FontSize=12`
  demande ~105 px → libellé tronqué. La correction propre (grille 2×2 avec « Diagnostic… » en
  `ColumnSpan=2`, ou raccourcissement des libellés) est un arbitrage de design qui demande un œil sur
  l'écran, et le plan pose la règle « ici on retire, on n'invente pas ». **À trancher par l'utilisateur.**

---

**Total deviations:** 2 auto-fixed (1 bug de documentation, 1 artefact de build bloquant) + 2 écarts
documentés et assumés.
**Impact on plan:** Aucun élargissement de périmètre. `CompositeUsageProvider.cs`,
`FiveHourWindowInference.cs` et `WeeklyWindow.cs` n'ont pas été touchés, conformément aux contraintes.

## Issues Encountered

**La tâche 2 était marquée `tdd="true"` mais un cycle RED/GREEN y est structurellement impossible.**
Le plan interdit explicitement d'écrire le moindre code de production (aucun migrateur), et le
comportement à prouver est le défaut de `System.Text.Json` : les 4 tests passent au VERT dès leur
première exécution, par construction. Une phase RED artificielle aurait exigé de casser volontairement
`SettingsService` — exactement ce que le plan interdit. Les tests ont donc été livrés en un commit
`test(16-04)` unique, comme **tests de caractérisation** : ils ne pilotent pas une conception, ils
verrouillent un comportement existant contre une régression future (par exemple l'ajout malencontreux
de `JsonUnmappedMemberHandling.Disallow` aux options, qui les ferait tous échouer). Leur valeur de
garde est réelle, leur valeur de pilotage est nulle — et c'est assumé.

**Aucun test n'a écrit dans le vrai `%APPDATA%\Chronos\`** : chaque test isole un dossier temp sous
`Path.GetTempPath()` injecté via `new ChronosPaths(usage, projects)`. Vérifié après exécution :
aucun `.tmp-*` dans le profil réel, et le `settings.json` de production porte toujours ses six champs
obsolètes intacts (il ne sera purgé qu'au prochain `Save()` de l'application elle-même).

## Bilan du compte de tests

| Étape | Total | Écart |
|---|---|---|
| Entrée du plan 16-04 (après 16-03) | 414 | — |
| Tâche 1 (retrait des champs) | 414 | 0 (aucun test supprimé, 4 lignes d'assertions retirées) |
| Tâche 2 (preuve DEL-06) | 418 | +4 |
| Tâche 3 (garde de non-retour) | **419** | +1 |

**0 échec** aux trois commits. La cible du plan était « ~405 ± 10, critère 0 échec » : 419 la dépasse
par le haut, sans aucune perte de couverture nette.

## User Setup Required

None — aucune configuration externe. Note pratique : au prochain lancement de Chronos, le premier
`Save()` (déplacement de l'overlay, changement de thème, n'importe quel toggle) purgera passivement
les six champs obsolètes du `settings.json` de l'utilisateur. Ses 18 préférences sont préservées —
c'est exactement ce que la fixture DEL-06 prouve.

## Next Phase Readiness

- **Phase 16 close.** Les quatre plans sont exécutés : persistance du dernier relevé exact (16-02),
  source de delta (16-03), démolition des plafonds achevée et verrouillée (16-01 + 16-04).
- **Prêt pour la phase 19** (doctrine du composite, DEL-03/DEL-04) : `CompositeUsageProvider.cs` n'a
  été touché par aucun plan de la phase 16 — sa réécriture part d'une base intacte, avec
  `ITranscriptActivitySource` et `LastExactStore` déjà en place et testés.
- **Point ouvert pour la phase 20 (EXA-06)** : la refonte du diagnostic devra donner une rubrique
  « source de delta » digne de ce nom (ici on s'est contenté de retirer), et l'arbitrage de la
  `UniformGrid` « RÉGLAGES » à 3 boutons attend une décision de l'utilisateur.

---
*Phase: 16-fondations-du-delta-persistance-d-molition-des-plafonds*
*Completed: 2026-09-09*

## Self-Check: PASSED

- `tests/Chronos.Tests/TestData/settings-legacy-plafonds.json` — présent
- `.planning/phases/16-.../16-04-SUMMARY.md` — présent
- `src/Chronos/Services/ChronosSettings.cs` — présent (18 propriétés)
- `src/Chronos/Services/BudgetSource.cs` — absent (supprimé comme prévu)
- Commits `d6023d3`, `a94793d`, `2383a98` — présents dans l'historique
- `dotnet test Chronos.sln -v q --nologo` — 419 réussis / 0 échec
