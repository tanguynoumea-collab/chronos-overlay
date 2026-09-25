---
phase: 28-deux-mots-une-question-les-m-mes-horizons
plan: 01
subsystem: sessions
tags: [transcripts, askuserquestion, instant-du-signal, fixture-reelle, anonymisation, tdd, xunit]

sha_entree_de_plan: 5f5c3cb
one_liner: "Une question AskUserQuestion en suspens est une attente (motif « AskUserQuestion ») et un transcript est daté par le timestamp de son dernier message, borné par l'écriture — 896 → 912 tests (isolé), 924 avec le plan 28-02"

requires:
  - phase: 21-le-widget-ne-parle-que-de-claude-code
    provides: "SRC-03 — la limite de douze porte sur les sessions RETENUES (reformulée ici en Take après tri)"
  - phase: 18-la-source-exacte-par-en-tetes
    provides: "UsageNormalization.InstantDepuisIso — le point unique de conversion ISO (garde HDR-05)"
  - phase: 26-traite-veut-enfin-dire-quelque-chose
    provides: "La doctrine TRT-02 (l'instant que le SIGNAL porte), désormais appliquée à la source transcripts"
provides:
  - "TranscriptSessionSource.Classify : trois issues — WaitingTurn / WaitingAttention (dernier tool_use nommé EXACTEMENT « AskUserQuestion », motif « AskUserQuestion ») / Working"
  - "SessionSnapshot.UpdatedAt d'un transcript = timestamp de la dernière ligne user/assistant, borné par l'écriture, repli sur l'écriture (décision D-28-01)"
  - "Read : date d'écriture réduite au pré-filtre ; tri par UpdatedAt puis SessionId ordinal ; Take(12) après tri"
  - "Deux fixtures RÉELLES anonymisées (tests/Chronos.Tests/TestData/TranscriptQuestion/) : question en suspens, question répondue"
  - "Les deux cas d'arbitrage du Piège 3 écrits en test (PreToolUse seul ⇒ Working ; PermissionRequest ⇒ WaitingAttention)"
affects: [28-03-le-producteur-des-mots, 28-04-les-horizons, 30-la-lecture-fait-disparaitre, 31-constat]

tech-stack:
  added: []
  patterns:
    - "Une fixture réelle se repère par ses TIMESTAMPS, pas par ses numéros de ligne, et la date d'écriture de la source se relève avant et après l'extraction"
    - "Réduction de fixture : on RETIRE et on ANONYMISE, on n'invente rien — une chaîne vide reste vide"
    - "Date d'écriture = pré-filtre d'économie (exact parce que l'horodatage d'un message ne la dépasse jamais) ; horodatage = autorité"
    - "Un test de limite dont l'ordre alphabétique est l'INVERSE de la fraîcheur ne peut pas passer par un ordre d'énumération favorable ; l'appartenance est assertée AVANT les instants pour que le rouge dise lequel des deux casse"

key-files:
  created:
    - tests/Chronos.Tests/TranscriptQuestionTests.cs
    - tests/Chronos.Tests/TranscriptInstantSignalTests.cs
    - tests/Chronos.Tests/TestData/TranscriptQuestion/question-en-suspens.jsonl
    - tests/Chronos.Tests/TestData/TranscriptQuestion/question-repondue.jsonl
    - tests/Chronos.Tests/TestData/TranscriptQuestion/README.md
  modified:
    - src/Chronos/Services/TranscriptSessionSource.cs

key-decisions:
  - "D-28-01 : un transcript est daté par le timestamp de sa dernière ligne significative (user/assistant), borné par la date d'écriture, repli sur celle-ci si absent ou illisible ; la date d'écriture n'est plus qu'un pré-filtre d'énumération (fait du 2026-09-25 16:58:20 : douze transcripts rajeunis par des métadonnées sans horodatage)"
  - "LIB-02 : AskUserQuestion comparé ordinalement et exactement (switch sur string), aucune liste extensible ; l'appel parallèle (1 sur 222 mesuré) se dégrade vers v1.6 (Working), écrit et testé"
  - "Piège 3 non tranché : les deux issues d'arbitrage sont écrites en test ; la vérification in vivo (PermissionRequest part-il pour AskUserQuestion ?) reste à la phase 31"
  - "Le motif du snapshot transcript est un fait observé : « AskUserQuestion » pour la seule attente qu'un transcript peut voir, null sinon"

requirements-completed: [LIB-02]

duration: 11min
completed: 2026-09-25
---

# Phase 28 Plan 01 : Une question, et l'instant du signal — Summary

**Une question `AskUserQuestion` en suspens se lit désormais « attente, au rang des questions » (motif
`AskUserQuestion`) sur des lignes réelles, et un transcript est daté par le `timestamp` de son dernier message,
borné par la date d'écriture de son fichier. Des métadonnées sans horodatage ne rajeunissent donc plus une
session (décision D-28-01).**

## Performance

- **Duration:** ~11 min (19:23:06Z → 19:34:01Z)
- **Started:** 2026-09-25T19:23:06Z
- **Completed:** 2026-09-25T19:34:01Z
- **Tasks:** 2 / 2
- **Files:** 6 (1 source modifiée, 2 classes de tests, 2 fixtures, 1 README)

## Mesures

| Point | Valeur |
|---|---|
| SHA d'entrée du plan | `5f5c3cb` |
| Baseline mesurée à l'entrée | **896 verts / 0 échec** (8 s) |
| RED tâche 1 | **1 échec** / 9 : `Une_question_sans_reponse_est_une_attente_au_rang_des_questions` (lu `Working`) |
| RED tâche 2 | **4 échecs** / 16 : `Un_transcript_est_date_par_le_timestamp_de_son_dernier_message` (15:59 au lieu de 15:55), `Un_transcript_rajeuni_par_des_metadonnees_n_est_pas_lu` (collection non vide), `Les_metadonnees_ecrites_pendant_la_question_ne_la_rajeunissent_pas` (06:59:03.967 au lieu de 06:55:33.967), et **le cas 6 `La_limite_de_douze_garde_les_plus_recents_par_leur_dernier_message` ROUGE sur l'APPARTENANCE** (retenus `s-00`…`s-11` au lieu de `s-03`…`s-14` : l'ordre d'énumération n'était PAS favorable) |
| Après tâche 1 | 913 / 0 sur l'arbre partagé = 896 + **9** (TranscriptQuestionTests, compté isolément) + 8 tests alors non commités du plan 28-02 |
| Après tâche 2, **copie isolée** (`git archive 8542274` + mes seuls fichiers) | **912 / 0**, deux exécutions consécutives (8 s chacune) = 896 + 16 |
| Après tâche 2, arbre partagé, les deux plans commités | **924 / 0** = 896 + 16 + 12 (l'attendu combiné du plan) |
| Date d'écriture du transcript source (`stat -c %Y`) | **1790348300** avant l'extraction, après l'extraction, et en fin de plan (taille 5 708 243 octets inchangée) |
| `grep -c ActiveWindow` dans la source | **3** à l'entrée, **3** après la tâche 1, **4** après la tâche 2 : la 4e occurrence est le post-filtre sur l'horodatage prescrit par le plan ; la valeur reste `FromMinutes(15)` (1 occurrence) |

## Accomplishments

- **LIB-02 livré (classification).** `HasToolUse` remplacé par `DernierOutil` (nom du DERNIER bloc `tool_use`,
  `ValueKind` vérifiés à chaque niveau). `Classify` a trois issues ; `"AskUserQuestion" => WaitingAttention`
  par `switch` sur chaîne (ordinal, exact). Le motif `AskUserQuestion` est posé sur le snapshot.
- **D-28-01 livré.** `Horodatage(o)` passe par `UsageNormalization.InstantDepuisIso` (seule occurrence, garde
  HDR-05 intacte) ; il est relu à chaque ligne user/assistant, donc la dernière l'emporte (un `null` sur la
  dernière fait retomber sur l'écriture). `updatedAt = instant ≤ écriture ? instant : écriture`.
- **`Read` refondu.** La date d'écriture sert de pré-filtre, puis on classe tout, on filtre sur `UpdatedAt`, on
  trie par `UpdatedAt` décroissant puis `SessionId` ordinal, et on applique `Take(12)`. Plus aucun `break`.
- **Fixtures réelles.** Lignes 154-163 et 154-164 de la session `939eb30a` (repérées par timestamp), réduites et
  anonymisées (0 « tanguy », 0 « Users » ; 10 et 11 lignes ; 5 `timestamp` dans la fixture en suspens ; UTF-8
  sans BOM, fins de ligne `\n`, 0 CR dans le blob).
- **Piège 3 documenté.** Deux tests d'arbitrage purs, avec leur XML-doc : fait externe MEDIUM, non tranché, in
  vivo reporté à la phase 31, les hooks ne lisent pas `tool_name`.

## Task Commits

1. **Task 1 : fixtures réelles + la question classée attente (LIB-02)** — `8542274` (feat ; RED constaté avant, 1 échec)
2. **Task 2 : l'instant du signal = dernier message (D-28-01)** — `243a535` (feat ; RED constaté avant, 4 échecs)

Commits `src/` de ce plan : `git show --stat 8542274 -- src/` et `git show --stat 243a535 -- src/` ne montrent
QUE `src/Chronos/Services/TranscriptSessionSource.cs`. (`git diff --stat 5f5c3cb..HEAD -- src/` montre aussi
`AffichageSessions.cs`, `ArbitrageSessions.cs`, `DiagnosticService.cs`, etc. : ce sont les commits `147f41a` et
`24a0957` du plan 28-02, intercalés dans la même vague.)

## Files Created/Modified

- `src/Chronos/Services/TranscriptSessionSource.cs` : `DernierOutil`, `Horodatage`, `Classify` à trois issues et
  instant du signal, `Read` en pré-filtre puis tri et limite, XML-doc (LIB-02 et D-28-01).
- `tests/Chronos.Tests/TranscriptQuestionTests.cs` : 10 cas (9 de la tâche 1 et 1 de la tâche 2).
- `tests/Chronos.Tests/TranscriptInstantSignalTests.cs` : 6 cas (instant, métadonnées, repli, borne, illisible, limite de douze).
- `tests/Chronos.Tests/TestData/TranscriptQuestion/{question-en-suspens,question-repondue}.jsonl` + `README.md`
  (origine, retraits, anonymisation, pourquoi les métadonnées gardent leur forme).

## Decisions Made

- **Chaîne vide gardée vide** dans les fixtures (`thinking` l. 156, `atis` l. 162). Elle ne porte aucune
  information, et la remplacer par « (anonymisé) » aurait inventé un contenu. C'est écrit dans le README.
- **`else continue;` pour les lignes non significatives** dans `Classify`, puis `instant = Horodatage(o)` une
  seule fois après les deux branches. Le comportement est celui que prescrit le plan (poser l'instant dans la
  branche assistant ET dans la branche user), écrit sans duplication.
- **Tests isolés par `IDisposable`** : chaque racine temporaire est supprimée après le test (0 résidu constaté),
  avec le même `Assert.StartsWith(Path.GetTempPath())` que `TranscriptSousAgentsTests`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocage] Variable de motif `t` renommée `h` dans `Classify`**
- **Found during:** Task 2 (GREEN)
- **Issue :** le code du plan (`instant is { } t && t <= ecriture ? t : ecriture`) déclare `t` dans le bloc
  `try`, où la boucle déclare déjà `out var t` (lecture du champ `type`) dans une portée imbriquée : CS0136.
- **Fix :** `instant is { } h && h <= ecriture ? h : ecriture`. La sémantique ne change pas, et aucun critère
  textuel ne porte sur ce nom.
- **Files modified :** `src/Chronos/Services/TranscriptSessionSource.cs`
- **Commit :** `243a535`

**2. [Rule 3 - Blocage] Chaînes brutes interpolées en `$$$` et non `$$`**
- **Found during:** Task 1 (RED)
- **Issue :** le JSON des lignes en ligne se termine par `}]}}`. Sous `$$"""`, deux accolades fermantes
  consécutives sont un délimiteur d'interpolation, ce qui donne CS9007.
- **Fix :** `$$$"""…{{{Iso(t)}}}…"""`. Le contenu JSON reste identique.
- **Files modified :** `tests/Chronos.Tests/TranscriptQuestionTests.cs`, `tests/Chronos.Tests/TranscriptInstantSignalTests.cs`
- **Commit :** `8542274`, `243a535`

**3. [Mesure] Totaux mesurés sur une copie isolée en plus de l'arbre partagé**
- **Pourquoi :** le plan 28-02 écrivait dans le même arbre en même temps. Sur l'arbre partagé, la suite
  complète a d'abord donné 922 tests dont 3 échecs, puis 4 avec le filtre élargi, parce que `~SessionsTests`
  attrape aussi `AffichageSessionsTests` et `InspectionSessionsTests`. Ces échecs venaient tous des squelettes
  RED du plan 28-02 (`NotImplementedException` dans `AffichageSessions.cs`, `Une_activite_illisible_reste_inconnue_et_non_deduite`).
  Je n'ai touché à aucun de ces fichiers.
- **Mesure retenue :** copie isolée (`git archive 8542274` avec mes seuls fichiers, dans le bloc-notes de
  session, supprimée ensuite) : 912 / 0 sur deux exécutions. Sur l'arbre partagé, une fois les deux plans
  commités : 924 / 0.

## Issues Encountered

- Le heredoc de l'outil Bash échoue sur certains contenus (guillemets triples C#, apostrophes françaises). J'ai
  écrit les fichiers concernés avec l'outil d'écriture de fichiers. Le code produit n'en dépend pas.

## Known Stubs

Aucun. Les valeurs `"(anonymisé)"` des fixtures sont de l'anonymisation voulue et ne passent par aucune
interface utilisateur.

## Next Phase Readiness

- **28-03** : la classification est livrée, mais le mot « En attente » affiché pour la question viendra du
  producteur (`AffichageSessions`).
- **28-04** : `ActiveWindow` est toujours à 15 min. Le post-filtre sur `UpdatedAt` et le tri avant la limite
  sont déjà en place, donc l'élargissement à 8 h ne touchera qu'une constante et la règle de silence.
  `Un_transcript_rajeuni_par_des_metadonnees_n_est_pas_lu` utilise un écart de deux jours et restera vrai
  après ce changement. **SIL-01 reste Pending.**
- **Phase 31** : vérifier in vivo, en lecture seule, si `PermissionRequest` part pour `AskUserQuestion`. Le test
  consiste à lire le fichier d'état pendant qu'une vraie question est affichée.

## Self-Check: PASSED

- FOUND: src/Chronos/Services/TranscriptSessionSource.cs
- FOUND: tests/Chronos.Tests/TranscriptQuestionTests.cs
- FOUND: tests/Chronos.Tests/TranscriptInstantSignalTests.cs
- FOUND: tests/Chronos.Tests/TestData/TranscriptQuestion/question-en-suspens.jsonl
- FOUND: tests/Chronos.Tests/TestData/TranscriptQuestion/question-repondue.jsonl
- FOUND: tests/Chronos.Tests/TestData/TranscriptQuestion/README.md
- FOUND: 8542274
- FOUND: 243a535

---
*Phase: 28-deux-mots-une-question-les-m-mes-horizons*
*Completed: 2026-09-25*
