---
phase: 28-deux-mots-une-question-les-m-mes-horizons
plan: 04
subsystem: widget-sessions
tags: [csharp, horizons, silence, transcripts, gardes, mutation, mesure-reelle, xunit, tdd]

sha_entree_de_plan: c7db3ce
one_liner: "Une session connue par son seul transcript vit selon les mêmes horizons qu'une session à hook — « En attente ? » après 20 min de silence, « En attente » jusqu'à 8 h, absente au-delà — parce que la règle de silence est UNE fonction du moniteur appliquée à tous les signaux et que les quatre seuils vivent dans HorizonsSessions sous deux gardes ; coût réel mesuré à 28,2 / 25,7 ms de médiane (pas de cache) ; 935 → 947 tests, 0 échec"

requires:
  - phase: 28-01
    provides: "D-28-01 — un transcript est daté par le timestamp de son dernier message (sans quoi l'élargissement à 8 h ramenait douze fantômes) ; tri puis Take(12)"
  - phase: 28-02
    provides: "Test de dette reformulé (hook déduit contre transcript en TOUR FINI) ; EstUneAttente ; masquage de l'indéterminé au moniteur"
  - phase: 28-03
    provides: "Les trois mots du producteur ; §3 du contrat balisé ETATS-AFFICHES"
provides:
  - "HorizonsSessions : Silence 20 min < Abandon 8 h < RetentionTraitees 24 h < ExpirationEtat 72 h, en un seul type"
  - "SessionMonitor.AppliquerSilence : la règle de silence, appliquée à TOUS les signaux avant ArbitrageSessions.Trancher"
  - "TranscriptSessionSource : fenêtre de 15 min remplacée par HorizonsSessions.Abandon (pré-filtre d'écriture ET instant du signal, bornes <=)"
  - "Deux gardes (chaîne ; câblage des quatre consommateurs) + garde « silence en un point » + garde documentaire croisée"
  - "§3 de docs/hooks-contract.md : silence en un point, D-28-01, « les mêmes horizons pour tous » ; §5.2 : réserve R10 corrigée"
  - "Mesure réelle à 8 h avec la DLL livrée, consignée dans 28-VALIDATION.md"
affects: [30-la-lecture-fait-disparaitre, 31-crit-publi-constat]

tech-stack:
  added: []
  patterns:
    - "Une règle de dérivation appliquée au point de COLLECTE, avant l'arbitrage, pour toutes les sources : aucune source ne déduit chez elle"
    - "Seuils dans un type unique + deux gardes complémentaires : la chaîne (valeurs) ET le câblage (personne ne réintroduit un littéral)"
    - "Garde de chaîne : inégalités assertées AVANT les valeurs exactes, pour que le rouge d'une mutation dise laquelle se défait"

key-files:
  created:
    - src/Chronos/Services/HorizonsSessions.cs
    - tests/Chronos.Tests/HorizonsSessionsTests.cs
  modified:
    - src/Chronos/Services/SessionMonitor.cs
    - src/Chronos/Services/TranscriptSessionSource.cs
    - src/Chronos/Services/TreatedStore.cs
    - src/Chronos/Services/BalayageMagasinSessions.cs
    - docs/hooks-contract.md
    - tests/Chronos.Tests/SessionsTests.cs
    - tests/Chronos.Tests/TreatedSessionsTests.cs
    - tests/Chronos.Tests/ContratHooksDocumenteTests.cs
    - .planning/phases/28-deux-mots-une-question-les-m-mes-horizons/28-VALIDATION.md

key-decisions:
  - "SIL-01 : la règle de silence est UNE fonction du moniteur (AppliquerSilence), appliquée à tous les signaux avant l'arbitrage ; la source transcripts ne déduit rien (garde textuelle)"
  - "Les quatre horizons vivent dans HorizonsSessions ; BalayageMagasinSessions.ExpirationEtat reste un alias public, tenu égal par test"
  - "Transcripts et hooks partagent la même borne d'abandon (<= 8 h lu, > 8 h écarté)"
  - "Pas de cache : médiane réelle 28,2 ms puis 25,7 ms par cycle, sous le seuil de 50 ms (plus haute que les 19,6 ms de la recherche, écart non décomposé)"

requirements-completed: [SIL-01]

duration: 15min
completed: 2026-09-25
---

# Phase 28 Plan 04 : Les mêmes horizons pour tous — Summary

**Une session connue par son seul transcript suit désormais les mêmes horizons qu'une session à hook :
« En attente ? » après vingt minutes de silence, « En attente » jusqu'à huit heures, absente au-delà. La règle de
silence est UNE fonction du moniteur appliquée à tous les signaux, et les quatre seuils vivent dans
`HorizonsSessions` sous deux gardes. Le coût réel a été mesuré à 28,2 ms puis 25,7 ms de médiane par cycle, donc
sans cache. La suite passe de 935 à 947 tests, 0 échec.**

## Performance

- **Duration :** ~15 min (19:56:04Z → 20:11Z)
- **Started :** 2026-09-25T19:56:04Z
- **Completed :** 2026-09-25T20:11Z
- **Tasks :** 3 / 3
- **Files :** 11 (1 source créée, 4 sources modifiées, 1 document, 1 classe de tests créée, 3 classes de tests modifiées, 1 carte de validation)

## Mesures

| Point | Valeur |
|---|---|
| SHA d'entrée du plan | `c7db3ce` (vague 3, seul sur l'arbre, arbre propre) |
| Entrée (sortie de 28-03) | 935 / 0 échec, mesuré par 28-03. Non remesuré seul : le RED de la tâche 1 donne 937 verts sur 943, soit 935 + les 2 scénarios verts par construction |
| RED tâche 1 | **6 échecs / 943** : scénarios 1, 2, 3, 4, 5 (`Assert.Single() Failure: The collection was empty`) et 8 (`Expected: 2 / Actual: 1`) ; verts attendus : 6 et 7 |
| Après tâche 1 (`62d6a0c`) | **943 / 0** |
| RED tâche 2 | **1 échec / 24** (`HorizonsSessionsTests` + `ContratHooksDocumenteTests`) : la garde documentaire, `Not found: "HorizonsSessions.Silence"` ; les trois gardes de code sont vertes par construction |
| Mutation `Abandon = 30 h` | **2 échecs / 11** : `La_chaine_des_horizons_tient` (« Abandon (1.06:00:00) < RetentionTraitees (1.00:00:00) défait ») et `Transcript_seul_de_8_h_01_est_absent` ; révoquée, sha256 `5273a0991da3…` identique avant et après, `git diff` vide |
| Après tâche 2 (`331a33e`), deux exécutions consécutives | **947 / 0** et **947 / 0** |
| Mesure réelle, médiane chaude de `TranscriptSessionSource.Read` | **28,2 ms** (22 h 06) et **25,7 ms** (22 h 08), sous le seuil de 50 ms, donc pas de cache |
| `~/.claude/settings.json` (date d'écriture) | **1790327276** avant et après la mesure (taille 3 384) |

## Accomplishments

- **Le trou §9.1 est refermé.** La fenêtre de quinze minutes des transcripts disparaît au profit de
  `HorizonsSessions.Abandon` (8 h), appliqué au pré-filtre d'écriture et à l'instant du signal, avec `<=` comme le
  moniteur. Une session sans fichier de hook ne disparaît plus en silence au bout d'un quart d'heure.
- **Le silence est géré en un seul point.** `SessionMonitor.AppliquerSilence` passe un `Working` de plus de
  vingt minutes en `WaitingDeduced`. Elle s'applique aux deux dépôts (transcripts et hooks), avant
  `ArbitrageSessions.Trancher`. Pour les hooks, c'est la même position qu'avant, donc aucun résultat hook ne
  change : `InspectionSessionsTests` 26/26 et `Monitor_lit_les_sessions_et_applique_la_staleness` verts sans
  retouche. `TryRead` ne garde que l'abandon et le compteur de fichiers périmés.
- **Un seul type pour les horizons.** `HorizonsSessions` porte les quatre seuils. `TreatedStore` (rétention) et
  `BalayageMagasinSessions` (alias public `ExpirationEtat`) le lisent, et plus aucun des quatre fichiers ne
  déclare de durée.
- **Les horizons ne peuvent plus diverger en silence.** Trois gardes de code :
  - la chaîne, avec « neuf fois l'abandon » et l'égalité de l'alias ;
  - le câblage des quatre consommateurs ;
  - la règle de silence en un seul point : seul `SessionMonitor.cs` lit `HorizonsSessions.Silence` parmi 69
    fichiers `Services/`, avec une seule écriture de `WaitingDeduced`.

  S'y ajoute une garde documentaire croisée. La mutation a été jouée.
- **Le contrat dit ce qui est câblé.** Le §3 réécrit contient trois paragraphes :
  - le silence en un point (`AppliquerSilence`, `HorizonsSessions.Silence` et `.Abandon`) ;
  - D-28-01, avec le relevé de 16 h 58 ;
  - « Les mêmes horizons pour tous (SIL-01) ».

  La puce « bascule de source » ne cite plus `DropAfter`. Au §5.2, la réserve R10 est corrigée : le filet est le
  seuil de lecture de 8 h, et le balayage a son propre seuil de 72 h.
- **Le coût et la population à 8 h ont été mesurés sur la vraie machine**, en lecture seule, avec la DLL livrée.

## Mesure réelle (tâche 3) — détail

Une console jetable a été construite hors dépôt sur la `Chronos.dll` Release livrée, puis supprimée. La DLL a
rendu `Silence=00:20:00, Abandon=08:00:00`. Deux exécutions ont eu lieu, à 22 h 06 et 22 h 08 (heure locale).

| Grandeur | Mesuré (22 h 06 ; 22 h 08) | Recherche |
|---|---|---|
| `.jsonl` énumérés | 1 474, dont 1 369 sous-agents écartés par le chemin | 1 425 / 1 342 |
| Candidats après pré-filtres, 8 h (15 min) | 18 : 13 principaux + 5 `wf_*/journal.jsonl` (15 min : 2) | 18 (1) |
| Énumération seule, médiane | 14,8 ; 13,0 ms | 12,7 ms |
| `Read` froid | 63,3 ; 42,4 ms | 21,0 ms |
| `Read` chaud : médiane / p90 / max | 28,2 / 30,4 / 32,5 ; 25,7 / 26,9 / 29,1 ms | 19,6 / 21,5 / 22,6 ms |
| `SessionMonitor.Inspecter`, hooks vides, médiane | 28,9 ; 26,0 ms | non mesuré |
| Sessions rendues par la source | 4 (2 seulement de moins de 15 min) | 5 |
| Visibles par le moniteur, par mot | 4 : « En attente » 3, « En attente ? » 1, « Réflexion » 0 ; 0 masquée | non mesuré |

- **Décision : pas de cache.** La médiane mesurée (28,2 puis 25,7 ms) est sous 50 ms, mais au-dessus des 19,6 ms
  de la recherche, soit +6 à +9 ms. L'écart n'a pas été décomposé. Deux causes sont plausibles, non prouvées :
  - le code livré classe les 18 candidats avant la limite, alors que la copie de la recherche s'arrêtait à 12 ;
  - 49 fichiers de plus sont énumérés.
- **D-28-01 tient sur les vraies données.** 9 des 13 transcripts principaux candidats ont été écrits à 16 h 58, à
  la fermeture de l'app bureau. Leur dernier message date de 8 h 12 à plus de deux jours, et aucun n'est rendu.
  J'ai vérifié ces âges indépendamment, en lecture seule, avec `tail -c 65536` et `grep` du dernier `timestamp`
  user/assistant.
- **Le trou §9.1 est visible sur la vraie machine.** Deux sessions auraient disparu avec la fenêtre de 15 min :
  - `c17a1b03`, tour fini il y a 30 min, affichée « En attente » ;
  - `dae57d29`, travail muet depuis 20-21 min, affichée « En attente ? ».
- **Lecture seule vérifiée.**
  - `~/.claude/settings.json` : 1790327276 avant et après.
  - `%APPDATA%\Chronos` (`archived.json`, `treated.json`, `settings.json`, `sessions/`) : dates identiques.
  - Dossier de mesure et dossier de hooks temporaire supprimés.
  - L'overlay en production n'a été ni lancé ni arrêté.

## Task Commits

1. **Tâche 1 : le trou §9.1 se referme (HorizonsSessions, silence en un point, 8 h)** : `62d6a0c` (feat ; RED constaté avant, 6 échecs)
2. **Tâche 2 : deux gardes, la mutation, le §3 du contrat** : `331a33e` (feat ; RED de la garde documentaire constaté avant ; mutation jouée et révoquée)
3. **Tâche 3 : mesure réelle à 8 h, 28-VALIDATION.md complété** : `88365e2` (docs)

**Plan metadata :** commit `docs(28-04)` final (SUMMARY, STATE, ROADMAP, REQUIREMENTS).

## Critères grep

- Tâche 1 :
  - `AppliquerSilence(` dans `SessionMonitor.cs` : **3** ;
  - `WaitingDeduced` dans `TranscriptSessionSource.cs` : **0** ;
  - `\b(ActiveWindow|DropAfter|SilenceDesBattements|RetentionMax)\b` : **0** dans chacun des quatre fichiers ;
  - `TimeSpan.From` : **0** dans `SessionMonitor.cs`, `TranscriptSessionSource.cs` et `TreatedStore.cs` ;
  - `Activity = SessionActivity.WaitingDeduced` dans le moniteur : **1**.
- Tâche 2 :
  - `` `DropAfter` `` ou `` `SilenceDesBattements` `` dans le contrat : **0** ;
  - `HorizonsSessions\.` : **7** (au moins 5 requis) ;
  - `Un transcript est daté par son dernier MESSAGE` : **1** ;
  - `docs/hooks-contract.md` : **467** lignes (au moins 90 requises).
- Tâche 3 : `git status --porcelain` ne montrait que `28-VALIDATION.md` (plus l'exe non suivi `Chronos-v3.1.0.exe`, déjà présent).

**Gardes vertes, sans assouplissement :**

| Classe de tests | Verts |
|---|---|
| `ContratHooksDocumenteTests` | 13/13 |
| `GardesPerimetreTests` | 12/12 |
| `ServicesLayerPurityTests` | 2/2 |
| `NormalisationUniqueTests` | 3/3 |
| `GardesDoctrineTests` | 8/8 |
| `LibellesSessionsTests` | 5/5 |
| `CompositionRootTests` | 5/5 |
| `HorizonsSessionsTests` | 11/11 |
| `InspectionSessionsTests` | 26/26 |
| `TreatedSessionsTests` | 15/15 |
| `GesteTraiteTests` | 5/5 |
| `BalayageMagasinSessionsTests` | 8/8 |
| `TranscriptQuestionTests` | 10/10 |
| `TranscriptInstantSignalTests` | 6/6 |

Le test de dette `Un_hook_deduit_et_un_transcript_en_tour_fini_du_MEME_instant_sont_departages_par_la_source` reste
vert avec la règle de silence appliquée aux transcripts, comme 28-02 l'avait préparé.

## Files Created/Modified

- `src/Chronos/Services/HorizonsSessions.cs` (créé) : les quatre seuils et la raison de chaque inégalité.
- `src/Chronos/Services/SessionMonitor.cs` : `AppliquerSilence` appliquée aux deux dépôts ; `TryRead` n'a plus
  que l'abandon ; XML-doc de tête (« appliquée à tous les signaux »).
- `src/Chronos/Services/TranscriptSessionSource.cs` : `ActiveWindow` supprimée, `HorizonsSessions.Abandon` en
  pré-filtre et en filtre ; XML-doc (« la règle de silence n'est PAS ici »).
- `src/Chronos/Services/TreatedStore.cs` : `RetentionMax` supprimée, deux filtres sur `HorizonsSessions.RetentionTraitees`.
- `src/Chronos/Services/BalayageMagasinSessions.cs` : `ExpirationEtat = HorizonsSessions.ExpirationEtat` (alias public).
- `docs/hooks-contract.md` : §3 (paragraphe silence réécrit, D-28-01, « les mêmes horizons pour tous », ligne
  `WaitingDeduced` de la table, puce « bascule de source ») et §5.2 (R10).
- `tests/Chronos.Tests/HorizonsSessionsTests.cs` (créé) : 8 scénarios et 3 gardes.
- `tests/Chronos.Tests/ContratHooksDocumenteTests.cs` : 1 garde documentaire croisée.
- `tests/Chronos.Tests/SessionsTests.cs` : `Transcript_trop_ancien_est_ignore`, âge porté de 30 min à 8 h 01.
- `tests/Chronos.Tests/TreatedSessionsTests.cs` : un commentaire (voir écart 2).
- `.planning/phases/28-…/28-VALIDATION.md` : totaux, mutations, statuts, mesures réelles.

## Decisions Made

Voir `key-decisions` en tête. En bref :
- la déduction se fait au point de collecte, pour toutes les sources, et jamais dans une source ;
- l'alias public du balayage est conservé et tenu égal par test ;
- pas de cache, sur la foi de la médiane mesurée et non de celle de la recherche.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Outillage] Fichiers C# écrits avec l'outil d'écriture, pas avec un heredoc**
- **Found during :** tâche 1 (RED)
- **Issue :** vérifié dans le bloc-notes, le heredoc de l'outil Bash réduit `\\` à `\`. Or les lignes JSON des tests
  portent `C:\\Projets\\…` dans des chaînes brutes. C'est la même altération que celle signalée par 28-03.
- **Fix :** les fichiers de code et le document sont écrits et modifiés avec les outils de fichiers. Les heredocs ne
  servent qu'aux messages de commit, qui n'ont pas de double barre oblique inverse. Le code produit n'en dépend pas.

**2. [Rule 1 - Exactitude documentaire] Commentaire de `TreatedSessionsTests` hors de la liste du plan**
- **Issue :** le récit du cas mesuré des 478 minutes disait « franchit DropAfter », un nom qui n'existe plus depuis
  la tâche 1.
- **Fix :** « franchit le seuil d'abandon (HorizonsSessions.Abandon, alors nommé DropAfter) ». C'est un
  commentaire seulement ; aucune assertion n'a changé.
- **Commit :** `62d6a0c`

**3. [Rule 1 - Exactitude documentaire] Ligne `WaitingDeduced` de la table du §3**
- **Issue :** la troisième colonne disait « plus aucun battement n'arrive », ce qui est faux dès que le silence
  vaut aussi pour les transcripts.
- **Fix :** « plus aucun signal n'arrive (battement de hook ou message de transcript) ». Les colonnes 1 et 2,
  comparées au producteur, sont inchangées.
- **Commit :** `331a33e`

### Ajustements mineurs (aucun changement de comportement)

**4. [Rédaction] La justification EVT-03 migre avec la règle.** La XML-doc de l'ancienne `SilenceDesBattements`
expliquait pourquoi le seuil reste large et renvoyait au contrat (EVT-05). Elle devient un `<para>` de la XML-doc
d'`AppliquerSilence`, en plus du commentaire de `TryRead` que le plan faisait migrer, pour ne pas perdre ce
raisonnement.

**5. [Rédaction] Une phrase conservée au §3.** « C'est la doctrine du milestone, appliquée à l'endroit où elle se
vérifie. » termine toujours le paragraphe réécrit. Le texte de remplacement du plan s'arrêtait juste avant ; la
phrase reste vraie.

**6. [Preuve] La mutation fait rougir deux tests, pas un.** En plus de `La_chaine_des_horizons_tient`, le scénario
de borne `Transcript_seul_de_8_h_01_est_absent` rougit par le comportement. Les inégalités sont assertées avant les
valeurs exactes, donc le message de la garde nomme l'inégalité défaite.

**7. [Mesure] Plus que demandé.** Il y a eu deux exécutions au lieu d'une, et j'ai ajouté le chronométrage de
l'énumération seule et de `SessionMonitor.Inspecter`. J'ai aussi vérifié indépendamment les âges des candidats
avant de conclure que la source rendait bien 4 sessions.

**8. [TDD] RED et GREEN dans un même commit par tâche**, comme les plans 28-02 et 28-03. Les rouges ont été
exécutés et sont consignés nominativement ci-dessus ; je ne les ai pas commités seuls, pour ne pas laisser un
commit rouge sur `main`.

**Total deviations :** 3 corrections d'exactitude ou d'outillage, 5 ajustements mineurs. **Impact :** aucun
changement de périmètre. `App.xaml.cs` n'a pas été touché : la source d'attestation du balayage s'élargit d'elle-même
à 8 h, et le balayage supprime donc moins, ce qui va dans le sens sûr.

## Issues Encountered

Aucun blocage. La première compilation de la console de mesure a échoué parce qu'avec `UseWPF`, les usings
implicites excluent `System.IO`. J'ai ajouté la directive dans la console jetable, hors dépôt.

## Known Stubs

Aucun.

## Next Phase Readiness

- **Phase 30 (règle « lue ») : la population est désormais celle de 8 h.**
  - Une session en tour fini reste « En attente » jusqu'à huit heures après son dernier message, y compris après un
    `SessionEnd` qui a supprimé son fichier d'état (Piège 14). C'est écrit au §3.
  - Mesuré à 22 h 06 : 4 lignes, dont 2 qui auraient disparu à 15 min.
  - Le point d'entrée reste `AffichageSessions.EstUneAttente`.
- **Phase 31 (constat) :** trois vérifications restent manuelles (voir 28-VALIDATION.md) :
  - la galerie à l'œil ;
  - les thèmes ;
  - `PermissionRequest` pour `AskUserQuestion`.
- **Coût :** environ 26-28 ms par cycle de 2 s à 8 h. Si le nombre de transcripts continue de croître, il faudra
  surveiller la médiane. Le cache autorisé est indexé par (chemin, date d'écriture, taille), jamais une lecture
  partielle. Écarter les `subagents/workflows/wf_*/journal.jsonl` (Piège 11, 5 candidats sur 18) reste un
  durcissement facultatif.

## Self-Check: PASSED

- FOUND: src/Chronos/Services/HorizonsSessions.cs
- FOUND: tests/Chronos.Tests/HorizonsSessionsTests.cs
- FOUND: .planning/phases/28-deux-mots-une-question-les-m-mes-horizons/28-04-SUMMARY.md
- FOUND: 62d6a0c
- FOUND: 331a33e
- FOUND: 88365e2
- FOUND: c7db3ce (SHA d'entrée)

---
*Phase: 28-deux-mots-une-question-les-m-mes-horizons*
*Completed: 2026-09-25*
