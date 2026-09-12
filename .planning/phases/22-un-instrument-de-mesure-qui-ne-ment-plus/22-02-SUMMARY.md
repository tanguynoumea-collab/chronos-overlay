---
phase: 22-un-instrument-de-mesure-qui-ne-ment-plus
plan: 02
subsystem: observabilite
tags: [diagnostic, sessions, di, partage-instance, gardes, tdd]

# Dependency graph
requires:
  - phase: 22-01
    provides: "SessionMonitor.Inspecter (visibles + masquées + motif), AffichageSessions (ordre/état/âge)"
  - phase: 20-le-diagnostic-ne-coute-plus-deux-minutes
    provides: "Protocole du paramètre optionnel en dernière position + repli machine ?? new InventaireMachine()"
  - phase: 21-p-rim-tre-le-widget-ne-parle-que-de-claude-code
    provides: "GardesPerimetreTests.CheminSources() — chemin des sources injecté par MSBuild"
provides:
  - "PARTAGE D'INSTANCE : le DiagnosticService interroge LE SessionMonitor du conteneur DI, celui que lit le widget"
  - "Section « widget » du rapport : visibles sans troncature, masquées nommées avec leur filtre ET son fichier"
  - "« MONITEUR NON INJECTÉ » — sans moniteur, le rapport n'invente aucune lecture"
  - "Deux gardes de SOURCE prouvées falsifiables : aucune fabrication locale, et l'injection depuis le conteneur"
  - "CompositionRootTests prouve la PORTÉE singleton, condition matérielle du partage"
affects: [22-03 sélection des fichiers d'état, 23 balayage, 24 fusion par fraîcheur, 25 contrat d'événements, 26 TTL ArchiveStore]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Partage d'instance plutôt que copie de comportement : le consommateur secondaire reçoit l'objet du conteneur, jamais un jumeau"
    - "Absence de repli comme choix documenté : là où `machine` se rabat, `moniteurSessions` se tait et le dit"
    - "Garde de SOURCE portée sur un FRAGMENT de fichier (l'enregistrement DI) et non sur le fichier entier"
    - "Falsification par mutation : chaque garde est muée, observée rouge, puis révoquée par checksum"

key-files:
  created: []
  modified:
    - src/Chronos/Services/DiagnosticService.cs
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs

key-decisions:
  - "Le paramètre moniteurSessions n'a AUCUN repli : sans lui le rapport dit qu'il n'a rien observé, au lieu de fabriquer un moniteur nu — c'est exactement le défaut corrigé"
  - "L'argument de composition est NOMMÉ pour sauter `machine`, dont la production garde volontairement son repli réel (acquis de la phase 20, suite en 5 s)"
  - "La garde d'injection lit le FRAGMENT d'enregistrement du DiagnosticService : une recherche globale de GetRequiredService<SessionMonitor>() resterait verte alors que le diagnostic serait débranché"
  - "Le commit ROUGE porte le paramètre de constructeur : la compilabilité à chaque commit est une contrainte de phase, elle prime sur la pureté du cycle TDD"
  - "OBS-02 n'est PAS marqué complet : la sélection des fichiers d'état appartient au plan 22-03"

patterns-established:
  - "Partage d'instance : un rapport qui décrit un système reconstruit à la volée décrit un autre système"
  - "Nommer le filtre AVEC son fichier : « écarté par treated.json » dit quoi ouvrir, « absent » n'apprend rien"
  - "Falsification obligatoire des gardes de non-retour, consignée nominativement"

requirements-completed: [OBS-01]

# Metrics
duration: 6 min
completed: 2026-09-12
---

# Phase 22 Plan 02 : Partage d'instance du moniteur de sessions Summary

**Le rapport de diagnostic interroge désormais LE `SessionMonitor` du conteneur DI — la même instance que lit
le widget — liste sans troncature ce qui est à l'écran, et nomme pour chaque session écartée le filtre et le
fichier qui la masquent ; le cas `e465420e` tient sur une ligne.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-09-12T16:01:52Z
- **Completed:** 2026-09-12T16:07:40Z
- **Tasks:** 2 (la première en TDD : rouge puis vert)
- **Files modified:** 5 (0 créé, 5 modifiés)

## Accomplishments

- **Les trois mécanismes du partage d'instance sont en place**, et aucun n'est une copie de comportement :
  - (a) `App.xaml.cs` passe `moniteurSessions: sp.GetRequiredService<SessionMonitor>()` en argument **nommé** ;
  - (b) `DiagnosticService` n'a **aucun repli** — sans moniteur, le rapport dit « MONITEUR NON INJECTÉ » ;
  - (c) `SessionMonitor.Read(now) => Inspecter(now).Visibles` (plan 22-01) reste l'unique implémentation des filtres.
- **`new SessionMonitor` a disparu de `DiagnosticService.cs`** — 0 occurrence, ni en code ni en commentaire.
  Le moniteur qu'il fabriquait était nu : sans magasin d'archives, sans filtre « traité », sans détecteur.
- **Le cas `e465420e` se lit en une ligne.** Format livré (chaque fragment asserté par
  `Le_cas_e465420e_se_lit_en_une_ligne_du_rapport`) :
  `· e465420e PROJET ADVANCED SHEET — à toi (il y a 10 min) — masquée par treated.json (hystérésis « traité » — posée automatiquement)`
- **La troncature à huit de la liste des sessions a disparu** (OBS-02, part widget) : neuf sessions → neuf lignes,
  l'attente en tête, dans l'ordre d'`AffichageSessions.Ordonner` — celui du widget.
- **Le rapport compte les fichiers de hook écartés pour ancienneté** : c'est le fait qui explique l'écart entre
  « 54 fichiers sur disque » et « 1 ligne à l'écran », et que rien ne disait.
- **Deux gardes de non-retour, prouvées falsifiables** (voir la section dédiée), plus la preuve de la PORTÉE
  singleton dans `CompositionRootTests` — sans laquelle le partage n'existerait pas.
- **Aucun comportement du widget n'a changé** : `SessionsTests.cs`, `TreatedSessionsTests.cs` et
  `SessionStylesBindingTests.cs` sont **absents du diff du plan**.

## Task Commits

1. **Task 1 (ROUGE) : le rapport doit décrire LE moniteur du widget** — `e40a3c6` (test)
2. **Task 1 (VERT) : le rapport interroge le moniteur injecté et nomme chaque filtre** — `33abea9` (feat)
3. **Task 2 : partage d'instance câblé + deux gardes de non-retour** — `2a3ce4d` (feat)

Aucune étape REFACTOR : l'implémentation est sortie propre du vert, et toute reformulation aurait risqué de
gonfler le compte de `masquée par`, qui doit rester à 1.

**Plan metadata:** voir le commit `docs(22-02)` qui suit.

## Falsification des gardes — trois mutations, observées ROUGES, révoquées

Le critère n°4 de la phase exige des gardes *falsifiables*, pas seulement *vertes*. Les trois mutations ont
été appliquées une à une, l'échec observé, puis révoquées par restauration d'une copie de sauvegarde
(checksums MD5 comparés avant/après — identiques dans les trois cas).

| # | Mutation appliquée | Test attendu rouge | Observé |
| --- | --- | --- | --- |
| **a** | `App.xaml.cs` : ligne `moniteurSessions: …` supprimée, parenthèse refermée sur `IEtatServeur` | `Le_diagnostic_recoit_le_moniteur_du_conteneur` | **FAIL** — 1 échec / 6, et lui seul |
| **b** | `DiagnosticService.cs` : `_moniteurSessions.Inspecter(…)` → `new SessionMonitor().Inspecter(…)` | `Le_diagnostic_ne_fabrique_aucun_moniteur_de_sessions` | **FAIL** — 1 échec / 6, et lui seul |
| **c** | `CompositionRootTests.cs` : `AddSingleton` → `AddTransient` pour le `SessionMonitor` | `Le_graphe_DI_resout_la_chaine_de_sessions` (nouvel `Assert.Same`) | **FAIL** — 1 échec / 5, et lui seul |

Chaque mutation n'a fait tomber **que** la garde visée : aucune n'est un détecteur de bruit. Après révocation,
`grep -rn "MUTATION (" src/ tests/` rend **0** et `git status --short` est vide.

La mutation (b) mérite une note : elle **compile et démarre parfaitement**. Un rapport ainsi muté redeviendrait
faux en silence, exactement comme avant cette phase — c'est la raison d'être de la garde.

## Files Created/Modified

- `src/Chronos/Services/DiagnosticService.cs` *(modifié)* — champ `_moniteurSessions`, paramètre optionnel
  `SessionMonitor? moniteurSessions = null` en dernière position (+ XML-doc expliquant l'absence de repli),
  section « widget » réécrite autour d'`Inspecter`, aides privées `LibelleMotif` et `Court`.
- `src/Chronos/App.xaml.cs` *(modifié)* — une ligne d'argument nommé dans l'enregistrement du `DiagnosticService`.
  Aucun autre changement : ni l'ordre des enregistrements, ni `OnStartup`.
- `tests/Chronos.Tests/DiagnosticServiceTests.cs` *(modifié)* — 5 tests ajoutés en fin de classe, 0 ligne
  préexistante supprimée.
- `tests/Chronos.Tests/GardesPerimetreTests.cs` *(modifié)* — les deux gardes de non-retour.
- `tests/Chronos.Tests/CompositionRootTests.cs` *(modifié)* — un `Assert.Same` sur la portée du moniteur.

## Decisions Made

- **Aucun repli pour `moniteurSessions`**, contrairement à `machine`. Un repli `?? new SessionMonitor()` aurait
  rendu le câblage facultatif et le défaut réversible sans que rien ne le signale. Vérifié par grep :
  `?? new ` reste à **2** — la mention en XML-doc et le repli réel de `machine`, inchangés.
- **Argument NOMMÉ dans la composition** pour sauter `machine` : la production garde son repli réel, acquis de
  la phase 20 (suite en 5 s au lieu de 2 min 12). Effet de bord : `GetRequiredService<SessionMonitor>()`
  apparaît **2 fois** dans `App.xaml.cs` — une pour le contrôleur du widget, une pour le diagnostic. C'est le
  partage d'instance, lisible à l'œil nu.
- **La garde d'injection lit un FRAGMENT**, borné par `new DiagnosticService(` … `));`. Une recherche globale de
  `GetRequiredService<SessionMonitor>()` dans `App.xaml.cs` resterait verte grâce au contrôleur du widget, alors
  même que le diagnostic aurait été débranché — une garde muette.
- **Le commit ROUGE porte déjà le paramètre de constructeur.** Sans lui, les tests ne compileraient pas, et
  `tests/Chronos.Tests` ayant un `ProjectReference` vers `Chronos`, **toute** l'invocation `dotnet test`
  échouerait. La contrainte de phase « compilabilité à chaque commit, aucun échec de build attendu » prime sur
  la pureté du cycle. Le rouge observé porte donc bien sur le CONTENU du rapport (5 échecs / 20, les 15 tests
  préexistants verts).
- **Effet de bord du partage assumé, non contourné** : `Inspecter` invoque `_tracker?.Observe`, donc
  `LogStartupAsync` observe désormais le détecteur quelques dizaines de millisecondes avant le widget. Le
  détecteur n'agit que sur des TRANSITIONS entre deux cycles ; une observation supplémentaire au même instant
  n'en produit aucune. Le diagnostic paie exactement ce que le widget paie — c'est le sens du partage.
- **OBS-02 laissé « Pending »** — voir « Deviations » ci-dessous.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Horodatage de magasin figé dans le passé = test à retardement**

- **Found during:** Task 1 (rédaction des tests, phase ROUGE)
- **Issue:** Le plan écrivait `treated.Set(Id, T22.ToUnixTimeMilliseconds())` avec `T22` = 2026-09-12T13:28:00Z,
  une constante figée. Or `TreatedStore.Load()` purge au-delà d'un TTL de **6 h mesuré sur l'horloge réelle**
  (`DateTimeOffset.UtcNow`, aucune horloge injectable). `Le_cas_e465420e_se_lit_en_une_ligne_du_rapport` serait
  donc devenu rouge à partir du 2026-09-12T19:28Z, sans qu'aucun code de production n'ait changé. C'est
  exactement l'écart déjà corrigé au plan 22-01 (déviation n°1 de son SUMMARY), et le même remède s'applique.
- **Fix:** Un helper nommé `EcritMaintenant22()` rend `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` pour
  l'écriture dans le magasin, avec le commentaire qui explique pourquoi. `T22` reste l'instant des snapshots :
  c'est l'arithmétique du relevé réel, et elle doit rester littérale. La valeur écrite n'est jamais assertée —
  le filtre ne regarde que la présence de la clé.
- **Files modified:** `tests/Chronos.Tests/DiagnosticServiceTests.cs`
- **Verification:** le test passe et ne dépend plus de l'heure d'exécution.
- **Committed in:** `e40a3c6` (commit ROUGE de la Task 1)

**2. [Rule 3 - Blocking] OBS-02 non marqué complet dans REQUIREMENTS.md**

- **Found during:** mise à jour des métadonnées, après la Task 2
- **Issue:** Le frontmatter du plan porte `requirements: [OBS-01, OBS-02]`. OBS-02 (« le diagnostic liste les
  sessions **pertinentes**, et non les 8 premières par ordre alphabétique ») est **partagé avec le plan 22-03**,
  dont le frontmatter porte `requirements: [OBS-02]` et qui livre la sélection par pertinence des fichiers
  d'état. Ce plan-ci n'a retiré qu'un `Take(8)` sur trois — les deux autres subsistent, dont celui des fichiers
  d'état, précisément visé par OBS-02. Le cocher ici affirmerait une capacité qui n'existe pas encore.
- **Fix:** `requirements mark-complete OBS-01` seul. OBS-02 sera coché par 22-03.
- **Files modified:** `.planning/REQUIREMENTS.md` (OBS-01 uniquement)
- **Verification:** OBS-02 toujours `Pending` dans la table de traçabilité.
- **Committed in:** commit `docs(22-02)` final

---

**Total deviations:** 2 auto-fixed (1 bug, 1 blocking)
**Impact on plan:** Aucun élargissement de portée. Le premier écart supprime un test à retardement ; le second
empêche une fausse déclaration de complétude — les deux servent la doctrine d'honnêteté du milestone.

## Issues Encountered

None.

## Verification

| Contrôle | Attendu | Obtenu |
| --- | --- | --- |
| `dotnet test Chronos.sln -v q --nologo` | 0 échec, ≥ 719 | **0 échec, 742 réussis** (735 + 7) |
| `dotnet build Chronos.sln -c Debug` | succès | **0 avertissement, 0 erreur** |
| `grep -cF "SessionMonitor? moniteurSessions = null"` (DiagnosticService.cs) | 1 | **1** |
| `grep -cF "new SessionMonitor"` (DiagnosticService.cs) | 0 | **0** |
| `grep -cF "_moniteurSessions.Inspecter(_clock.UtcNow)"` (DiagnosticService.cs) | 1 | **1** |
| `grep -cF "?? new "` (DiagnosticService.cs) | 2 (inchangé) | **2** |
| `grep -cF "Take(8)"` (DiagnosticService.cs) | 2 (il en restait 3) | **2** |
| `grep -cF "masquée par"` (DiagnosticService.cs) | 1 (la seule ligne de rapport) | **1** |
| `grep -cF "private static string LibelleMotif"` (DiagnosticService.cs) | 1 | **1** |
| `grep -c "UsageNormalization.InstantDepuisEpochMillisecondes"` (DiagnosticService.cs) | 2 | **2** |
| `grep -cF "GetRequiredService<SessionMonitor>()"` (App.xaml.cs) | 2 | **2** |
| `grep -cF "moniteurSessions: sp.GetRequiredService<SessionMonitor>()"` (App.xaml.cs) | 1 | **1** |
| `grep -cF "AddSingleton(sp => new SessionMonitor("` (App.xaml.cs) | 1 | **1** |
| Lignes préexistantes supprimées de `DiagnosticServiceTests.cs` | 0 | **0** |
| `SessionsTests.cs` / `TreatedSessionsTests.cs` / `SessionStylesBindingTests.cs` dans le diff | absents | **absents** |
| Fichiers du diff du plan | les 5 déclarés | **les 5, exactement** |
| Résidus de mutation (`grep -rn "MUTATION ("`) | 0 | **0** |
| `git status --short` après falsification | vide | **vide** |
| `%APPDATA%\Chronos\sessions` | 66 entrées | **66** |
| `archived.json` | 84 octets | **84** |
| `oauth.dat` (taille seule, jamais le mtime) | 518 octets | **518** |
| Overlay `Chronos-v3.0.2.exe` (pid 119412) | vivant, ni lancé ni tué | **vivant** |
| Dépendances NuGet ajoutées | 0 | **0** |

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- **Prêt pour 22-03.** Le constructeur du `DiagnosticService` porte son nouveau paramètre en dernière position,
  la section « widget » est stabilisée, et il reste exactement **2** `Take(8)` — dont celui des fichiers d'état
  que 22-03 remplacera par une sélection par pertinence.
- **L'instrument ne ment plus, et il ne peut plus recommencer.** Les phases 23 à 26 modifieront le câblage du
  widget ; le rapport les suivra sans qu'une ligne de `DiagnosticService.cs` ne change, et deux gardes prouvées
  falsifiables signalent tout retour à l'autonomie.
- **Rien n'a été anticipé** : la sélection des fichiers d'état (22-03), le balayage (23), `tmp`+`Move`, la fusion
  par ordre d'insertion (24), le contrat d'événements (25) et le TTL d'`ArchiveStore` (26) restent intacts.
- **Aucun stub** : chaque ligne ajoutée au rapport est alimentée par une lecture réelle du moniteur injecté, et
  le cas « non injecté » est un état nommé, pas un placeholder.

---
*Phase: 22-un-instrument-de-mesure-qui-ne-ment-plus*
*Completed: 2026-09-12*

## Self-Check: PASSED

Les 5 fichiers modifiés et le SUMMARY existent sur disque ; les 3 commits de tâche
(`e40a3c6`, `33abea9`, `2a3ce4d`) sont présents dans l'historique.
