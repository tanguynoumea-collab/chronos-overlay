---
phase: 29-ce-que-l-app-bureau-sait-de-chaque-session
plan: 05
subsystem: widget-sessions
tags: [csharp, app-bureau, diagnostic, obs-01, lecture-seule, gardes, mutation, msix, sonde-wmi, xunit, tdd]

sha_entree_de_plan: 3c5f395
one_liner: "Le rapport de diagnostic dit enfin ce que l'app bureau sait, et ce qu'elle ne sait pas : une section « Source app-bureau » (NON BRANCHÉE, lecture impossible à ce cycle, absente avec les racines cherchées, ou trouvée avec racine, candidats, compteurs, champs absents et jointures), puis, pour chaque ligne de l'écran, le titre lu, le dernier focus et la classification de fin de tour, le tout lu dans la MÊME lecture que le widget (OBS-01 étendu, AFFICHÉES et MASQUÉES sous le nom du widget) ; le chemin de l'app n'existe plus qu'à un endroit de src/, qui n'écrit rien (garde n° 2 d'APP-05, falsifiée par la mutation (c)) ; et une sonde créée par WMI, hors de l'arbre de l'app comme l'overlay, constate avec la DLL du commit livré que les racines du paquet sont lues et jointes (4 sessions affichées sur 4 portent leur titre), sans rien écrire ; 1039 → 1050 isolé (+11), 1062 / 0 combiné avec 29-04, deux exécutions"

requires:
  - phase: 29-01
    provides: "RacinesEtat (seul porteur du chemin de l'app), SessionMonitor.Dossiers, rapport multi-racines"
  - phase: 29-02
    provides: "LecteurAppBureau, LectureAppBureau (compteurs, ChampsAbsents, RacinesCherchees), liste Interdits des gardes de lecture seule, helper RacineAppBureau"
  - phase: 29-03
    provides: "LectureSessions.AppBureau (la lecture de CE cycle), SessionMonitor.Lecteur, AffichageSessions.Nom, câblage de production"
provides:
  - "DiagnosticService.DecrireSourceAppBureau : section « Source app-bureau » en quatre états, lignes par session affichée"
  - "Lignes AFFICHÉES et MASQUÉES du rapport sous AffichageSessions.Nom (le nom du widget)"
  - "Garde OBS-01 étendue : Le_diagnostic_lit_la_source_app_bureau_dans_la_meme_lecture_que_le_widget"
  - "Garde n° 2 d'APP-05 : Le_chemin_de_l_app_bureau_n_existe_que_dans_le_resolveur_de_racines + Le_resolveur_de_racines_n_ecrit_rien"
  - "29-SONDE-APP06.txt : constat hors de l'arbre de l'app, avec le code livré (racines, compteurs, jointures, coûts, lecture seule)"
affects: [30-la-lecture-fait-disparaitre, 31-crit-publi-constat]

tech-stack:
  added: []
  patterns:
    - "Un rapport qui lit la lecture de l'écran, jamais la sienne : une seule occurrence d'Inspecter( tenue par garde textuelle"
    - "Distinguer « personne n'a cherché » (pas de lecteur) de « la lecture a levé » (lecteur branché, lecture nulle) sans relire"
    - "Garde de chemin : les littéraux d'un emplacement tiers dans UN fichier, qui ne porte aucun jeton d'écriture"
    - "Constat de production par un processus créé par WMI (parent WmiPrvSE), sur une DLL compilée depuis git archive, en lecture seule prouvée par dates avant/après dans trois vues"

key-files:
  created:
    - .planning/phases/29-ce-que-l-app-bureau-sait-de-chaque-session/29-SONDE-APP06.txt
  modified:
    - src/Chronos/Services/DiagnosticService.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs
    - tests/Chronos.Tests/LectureSeuleAppBureauTests.cs

key-decisions:
  - "APP-04 : la section lit lecture.AppBureau du SEUL appel Inspecter ; le rapport ne résout aucune racine (ni RacinesEtat, ni .Lire(, ni new LecteurAppBureau dans le fichier) — il écrit les racines que CETTE lecture a cherchées"
  - "Quatre états, aucun tu : NON BRANCHÉE (moniteur sans lecteur), lecture impossible à ce cycle (lecteur branché, lecture nulle : SessionMonitor.Lecteur consulté, jamais relu), absente (dossier introuvable) avec les racines cherchées, trouvée"
  - "« avec métadonnées » et « relus sur disque à ce cycle » sont deux nombres : avec le cache, 0 relu n'est pas une panne"
  - "Une catégorie de fin de tour inconnue est recopiée brute et dite « non interprétée » ; une question sans motif est dite « aucune attente déposée »"
  - "APP-05 garde n° 2 : les jetons claude-code-sessions et Claude_ n'apparaissent que dans Services/RacinesEtat.cs (122 sources lus), et ce fichier ne porte aucun des jetons Interdits du lecteur"
  - "APP-06 constaté hors de l'arbre (WMI) avec la DLL de dd974f1 ; la confirmation dans l'exe publié reste la vérification manuelle de la phase 31"

requirements-completed: [APP-04, APP-05, APP-06]

duration: 17min
completed: 2026-09-26
---

# Phase 29 Plan 05 : Le diagnostic de la source app-bureau, la dernière garde, le constat hors de l'arbre — Summary

**Le rapport de diagnostic dit maintenant ce que l'app bureau sait, et ce qu'elle ne sait pas. La section
« Source app-bureau » annonce l'un de quatre états : NON BRANCHÉE, lecture impossible à ce cycle, absente (avec les
racines cherchées), ou trouvée. Trouvée, elle donne la racine, les autres candidats, les compteurs, les champs absents
et les jointures. Puis, pour chaque ligne de l'écran, elle donne le titre lu, le dernier focus et la classification
de fin de tour. Tout est lu dans la MÊME lecture que le widget. Le chemin de l'app n'existe plus qu'à un endroit de
`src/`, qui n'écrit rien, et la mutation (c) le prouve. Enfin, une sonde créée par WMI, hors de l'arbre de l'app comme
l'overlay, a constaté avec la DLL du commit livré que les racines du paquet sont lues et jointes : les 4 sessions
affichées portent leur titre, et rien n'a été écrit. La suite passe de 1039 à 1050 tests pour ce plan (+11), et à
1062 / 0 échec avec 29-04, sur deux exécutions.**

## Performance

- **Début :** 2026-09-25T21:43:15Z. **Dernier commit de tâche :** `415cd7d`, vers 21:59Z. Durée d'exécution : environ
  17 min. La clôture a été coupée par la limite d'usage, puis reprise le 2026-09-26 à 07:54Z.
- **SHA d'entrée :** `3c5f395`, à **1039 verts / 0 échec**.
- **Tâches :** 3 sur 3, soit 4 commits de tâche.
- **Fichiers :** 5 : 1 source, 3 classes de tests, 1 constat.

## Mesures (tests)

| Moment | Filtre de la tâche | Suite complète |
|---|---|---|
| Entrée (`3c5f395`) | 55 / 55 (les 5 classes de la tâche 1 et de la tâche 2) | **1039 / 0** |
| Tâche 1, RED (`fc2c834`) | **9 échecs / 60** | — |
| Tâche 1, GREEN (`7d21a11`) | 60 / 60 | **1058 / 0** = 1039 + 9 + 10 (tâche 1 de 29-04, commitée entre-temps) |
| Tâche 2 (`dd974f1`) | `LectureSeuleAppBureauTests` **6 / 6** | — |
| Mutation (c) | **1 échec / 61** | — |
| Fin de plan (`415cd7d`, 29-04 codé) | — | **1062 / 0**, deux exécutions (10 s, 11 s) ; relancé à la reprise : **1062 / 0** deux fois (11 s, 10 s) |

**Par classe, mesuré :**
- `DiagnosticServiceTests` : 31 → **39** (+8) ;
- `GardesPerimetreTests` : 15 → **16** (+1) ;
- `LectureSeuleAppBureauTests` : 4 → **6** (+2).

**Arithmétique :**
- Ce plan seul : 1039 + 11 = **1050**. Le plan annonçait 1049 : l'écart de +1 est expliqué à l'écart n° 1.
- Combiné : 1062 − 1050 = 12 tests viennent de 29-04, qui en annonçait 11 ; son SUMMARY en rend compte.
- Attendu combiné du plan : 1060. Mesuré : **1062**, soit +1 ici et +1 pour 29-04.

## Les rouges (TDD)

**Tâche 1 : 9 rouges sur 60**, contre le code d'entrée (la section n'existe pas).
- Les 7 cas du plan dans `DiagnosticServiceTests` :
  - `La_source_app_bureau_trouvee_dit_sa_racine_ses_compteurs_et_ses_jointures` ;
  - `Chaque_session_affichee_dit_son_titre_son_dernier_focus_et_sa_classification` ;
  - `Une_categorie_inconnue_est_dite_brute_jamais_traduite` ;
  - `La_source_app_bureau_absente_est_annoncee_avec_les_racines_cherchees` ;
  - `Un_moniteur_sans_lecteur_est_annonce_non_branche` ;
  - `Les_champs_absents_sont_nommes_et_comptes` ;
  - `Les_sessions_affichees_du_rapport_portent_le_nom_du_widget` (« Not found: "11456cab Session A — En attente" »).
- Le cas ajouté : `Un_lecteur_branche_dont_la_lecture_leve_n_est_pas_dit_non_branche`.
- La garde `GardesPerimetreTests.Le_diagnostic_lit_la_source_app_bureau_dans_la_meme_lecture_que_le_widget`. Elle
  rougit sur « Not found: "Source app-bureau" » : c'est sa première assertion anti-muette, placée avant celle sur
  `lecture.AppBureau`, et les deux étaient absentes.

**Tâche 2 :** les deux gardes sont **vertes dès leur arrivée**, ce que le plan annonce (29-01 et 29-02 avaient déjà
placé les littéraux dans le seul `RacinesEtat.cs`). Leur rouge vient de la mutation (c).

## Mutation (c) : jouée, constatée, révoquée

| | |
|---|---|
| Mutation | la ligne `// claude-code-sessions` insérée en ligne 2 de `src/Chronos/Services/DiagnosticService.cs` (CRLF conservé) |
| sha256 avant | `3b87c84ebcbf91de9f9a02d54e235c490615b3c967bfe15f4b43a6062f9e69d7` |
| Rouge constaté | **1 / 61** (filtre `LectureSeuleAppBureauTests`, `GardesPerimetreTests` et `DiagnosticServiceTests`) : `Le_chemin_de_l_app_bureau_n_existe_que_dans_le_resolveur_de_racines`, message « APP-05 : le chemin de l'app bureau ne doit exister que dans Services/RacinesEtat.cs, qui n'écrit rien. Fichiers qui le portent : Services/DiagnosticService.cs, Services/RacinesEtat.cs » |
| Révocation | copie de sauvegarde ; sha256 après = `3b87c84e…69d7`, **identique** ; `git diff` du fichier **vide** ; jouée deux fois (la seconde pour capter le décompte), révoquée deux fois, jamais commitée |

**Récapitulatif des mutations de la phase :**

| Plan | Mutations | Résultat |
|---|---|---|
| 29-01 | M1, M2 | voir 29-01-SUMMARY |
| 29-02 | (a), (b) | voir 29-02-SUMMARY |
| 29-03 | (d), (e), (f), et en plus du plan (g1), (g2) | voir 29-03-SUMMARY |
| 29-04 | `MaxWidth` | voir 29-04-SUMMARY |
| 29-05 | **(c)** | ici |

Cela fait **les neuf contrôles prévus**, plus les deux de 29-03 hors plan.

## Le constat hors de l'arbre (APP-06) : extrait de `29-SONDE-APP06.txt`

**Protocole.**
- `git archive dd974f1 src/Chronos` est extrait dans le bloc-notes, puis `dotnet build -c Release`. La compilation
  est isolée, parce que 29-04 écrivait dans l'arbre en parallèle.
- Une console jetable `SondeApp06` référence la `Chronos.dll` Release.
- Un `.ps1` lancé par `powershell.exe -NoProfile -ExecutionPolicy Bypass -File` exécute
  `Invoke-CimMethod Win32_Process Create`, qui rend `ReturnValue=0` (PID 132152). Il attend ensuite la fin du
  processus et le fichier, au plus 90 s.
- Le dossier `sonde-app06` a été supprimé ensuite.

| Grandeur | Attendu (recherche) | **Mesuré (21:57:37Z)** |
|---|---|---|
| Parent du processus | WmiPrvSE | **WmiPrvSE** (PID 93276) |
| `%APPDATA%\Claude existe` vu par la sonde | False | **False** (et `%APPDATA%\Chronos\sessions existe : False`) |
| Racine d'état du paquet | présente | **présente, 5 fichiers `*.json`** ; la vue réelle `…\Roaming\Chronos\sessions` est **absente** |
| Racines de l'app bureau | paquet présent | paquet **présent** ; `…\Roaming\Claude\claude-code-sessions` **absent** |
| `PremiereExistante(SessionsAppBureau)` | `…\Packages\Claude_…\claude-code-sessions` | `%USERPROFILE%\AppData\Local\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude-code-sessions` |
| `DossierTrouve` / énumérés / récents / avec métadonnées | True / ~138-142 / ~18 / ~18 | **True / 138 / 18 / 18** ; 0 illisible, 0 sans cliSessionId, 0 doublon |
| Champs absents (données réelles) | — | **`latestUserFrameAt 5`** |
| Métadonnées | — | 18 sessions : 18 avec titre, 18 avec dernier focus, 0 archivée. Classifications : Inconnue 6, Terminee 6, Bloquee 4 (4 avec motif), PreteARevue 2, 0 catégorie brute |
| `Lire` froid | ~96 ms | **345,81 ms** (voir ci-dessous) |
| `Lire` chaud ×30 : médiane / p90 / max | 0,73 / 0,90 / 1,06 ms (seuil 5 ms) | **0,502 / 0,559 / 0,720 ms** ; 30e lecture : 0 relu sur disque |
| `Inspecter` 1er appel | — | 271,60 ms |
| `Inspecter` chaud ×30 : médiane / p90 / max | ≈ 26-28 ms (seuil 50 ms) | **29,31 / 33,26 / 38,45 ms** |
| Visibles / masquées / désaccords / fichiers de hook écartés | — | **4 / 0 / 2 / 1** |
| Sessions visibles jointes à un titre | ≥ 1 | **4 sur 4** (et 4 sur 4 avec métadonnées) |
| Question de l'app retenue | — | **1** : `c17a1b03`, WaitingAttention, source probable app bureau. Elle bat le hook (WaitingTurn) de 40 ms et le transcript (WaitingTurn) de 2,361 s : ce sont les 2 désaccords, piège 8 accepté en 29-03 |
| Dates d'écriture `archived.json`, `treated.json`, `settings.json` | identiques | **identiques** dans trois vues : ce shell (virtualisé), Python (vue réelle), la sonde elle-même au début et à la fin |

**Ce qui dépasse l'attendu, écrit et non masqué.**
- **`Lire` froid : 345,81 ms au lieu d'environ 96 ms.** Ce premier appel comprend :
  - le chargement et la compilation JIT de `Chronos.dll` et de `System.Text.Json` dans un processus neuf ;
  - la première lecture des 18 fichiers d'environ 275 Ko.

  La mesure de la recherche venait d'une sonde minimale, sans l'assembly de l'application. L'overlay paie ce coût
  une fois, au premier cycle après son lancement. Chaque cycle paie ensuite le coût à chaud : 0,502 ms, sous
  l'attendu.
- **`Inspecter` chaud : 29,31 ms au lieu de 26-28 ms**, mais sous le seuil de 50 ms. Il inclut désormais la lecture
  de la racine d'état du paquet et la jointure de l'app. L'écart d'environ 1 à 3 ms reste dans la variance entre
  mesures de la phase 28 (28,2 puis 25,7 ms).

**Données personnelles.** Aucun titre, aucun motif : `grep "Session A|réponse attendue"` donne 0, et le fichier ne
contient que « titre joint (N car.) » et « motif présent ». Le nom d'utilisateur n'y figure pas (`grep -i tanguy`
donne 0) : `%USERPROFILE%` le remplace partout. Les noms de dossier de projet y figurent, comme le plan le prévoit.

## Task Commits

1. **Tâche 1 : la section « Source app-bureau », dans la même lecture que le widget (APP-04).**
   - `fc2c834` (test, RED 9/60)
   - `7d21a11` (feat, GREEN 60/60, suite 1058/0)
2. **Tâche 2 : le chemin de l'app en un seul endroit, qui n'écrit pas (APP-05).**
   - `dd974f1` (test, 6/6, mutation (c) 1/61 révoquée)
3. **Tâche 3 : le constat hors de l'arbre (APP-06).**
   - `415cd7d` (docs, `29-SONDE-APP06.txt`)

**Plan metadata :** commit `docs(29-05)` final (SUMMARY, STATE, ROADMAP, REQUIREMENTS, VALIDATION).

## Accomplishments

- **Le rapport dit ce que la source sait.** La section est insérée après les désaccords, avant le `catch` du moniteur.
  Voici les fragments que les tests assertent sur le scénario S, à M = 2026-09-25T18:40Z (le rendu complet de la ligne
  « Fichiers » n'a pas été relevé à part) :
  - `Source app-bureau : trouvée — <racine>` ;
  - `4 énumérés`, `3 avec métadonnées`, `1 sans cliSessionId` ;
  - `Champs absents (fichiers lus) : aucun` ;
  - `Jointures : 3 session(s) affichée(s) sur 4 ont des métadonnées` ;
  - la ligne `11456cab` : `« Session A »`, `focus il y a 9 min`, `fin de tour : aucune classification` ;
  - la ligne `adac2711` : `« Session E »`, `focus il y a 33 h`, `fin de tour : question posée (« (anonymisé) réponse attendue »)` ;
  - la ligne `939eb30a` : `aucune métadonnée (comportement v1.6)`.
- **OBS-01 étendu.** La section lit `lecture.AppBureau`, qui vient du SEUL `_moniteurSessions.Inspecter(_clock.UtcNow)`
  (le littéral est inchangé). Les lignes AFFICHÉES et MASQUÉES passent par `AffichageSessions.Nom`. Sans titre,
  `Nom == Project` : les 31 tests existants de `DiagnosticServiceTests` restent verts sans retouche.
- **Le chemin de l'app en un seul lieu.** 122 sources de `src/Chronos` sont lus, récursivement, hors `obj` et `bin`.
  Les porteurs de `claude-code-sessions` ou de `Claude_` forment exactement `[Services/RacinesEtat.cs]`. Ce fichier
  ne porte aucun des 31 jetons `Interdits` du lecteur (la même liste, partagée).
- **Le constat là où il compte.** Le processus a été créé par WMI, sur la DLL du commit livré, et il voit AppData
  comme l'overlay. Il lit les deux racines du paquet et joint les titres : 4 sur 4 à l'écran. Il n'a rien écrit.

## Critères grep

| Critère | Attendu | Mesuré |
|---|---|---|
| `grep -c "Inspecter(" DiagnosticService.cs` | 1 | **1** |
| `grep -c "lecture.AppBureau" DiagnosticService.cs` | ≥ 1 | **1** |
| `grep -cE "new LecteurAppBureau\|\.Lire\(\|RacinesEtat\|claude-code-sessions\|Claude_" DiagnosticService.cs` | 0 | **0** |
| `grep -c "AffichageSessions.Nom(" DiagnosticService.cs` | ≥ 2 | **2** |
| `grep -c "Source app-bureau : " DiagnosticService.cs` | ≥ 3 | **4** (plus « lecture impossible », voir l'écart n° 1) |
| `grep -rlE "claude-code-sessions\|Claude_" src/Chronos --include=*.cs` hors obj/bin | `RacinesEtat.cs` seul | **`src/Chronos/Services/RacinesEtat.cs`** seul |
| `grep -c "Claude existe : False" 29-SONDE-APP06.txt` | ≥ 1 | **1** |
| `git diff --stat 3c5f395..HEAD` sur `ViewModels`, `Resources` et `Views`, pour les commits 29-05 | vide | **vide** (les fichiers d'UI de la vague viennent tous de 29-04) |

**Gardes vertes, sans assouplissement :** `GardesPerimetreTests` 16/16, `ServicesLayerPurityTests` 2/2,
`NormalisationUniqueTests` 3/3, `LectureSeuleAppBureauTests` 6/6, `DiagnosticServiceTests` 39/39. La suite complète,
avec `CompositionRootTests` et `GardesDoctrineTests`, est à 1062 / 0. La liste `Interdits` n'a pas changé, et le
test du lecteur n'a pas été touché.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Honnêteté du rapport] Quatrième état « lecture impossible à ce cycle » et un 9e cas de test.**
- **Constat :** le moniteur rend `AppBureau = null` dans deux situations : il n'a pas de lecteur, ou le `Lire` de son
  lecteur a levé (dans un `try`). C'est la limite écrite par 29-03. Avec la seule règle du plan
  (`AppBureau is null` ⇒ NON BRANCHÉE), un lecteur branché dont la lecture échoue aurait été annoncé « non branché ».
  Le rapport aurait alors menti sur le câblage.
- **Correctif :** `DecrireSourceAppBureau(sb, lecture, lecteurBranche: _moniteurSessions.Lecteur is not null)`. Le
  lecteur est consulté, jamais relu : aucun `.Lire(`, et la garde reste verte. Le texte rendu est « Source app-bureau :
  lecture impossible à ce cycle — le lecteur est branché mais sa lecture a levé ; le widget se comporte comme en
  v1.6 ».
- **Test :** `Un_lecteur_branche_dont_la_lecture_leve_n_est_pas_dit_non_branche`. Des candidats dont l'énumération
  lève forcent l'échec sans toucher au lecteur. C'est la seule façon, parce que le lecteur est écrit pour ne jamais
  lever sur un dossier.
- **Effet :** +1 test, d'où 1050 isolé au lieu de 1049.
- **Commits :** `fc2c834`, `7d21a11`.

**2. [Renforcement] Le test 7 vérifie aussi les MASQUÉES.**
- La vérité du plan porte sur « AFFICHÉES / MASQUÉES » : une session titrée, archivée dans le test, doit donner la
  ligne « c17a1b03 Session B — En attente … masquée par archived.json ».
- Le test ajoute aussi `DoesNotContain("11456cab Projet-A")`. Le nombre de tests ne change pas.

**3. [Renforcement] Les assertions de non-vacuité.**
- Le test 2 exige exactement 4 lignes après « Jointures : ».
- Le test 6 compare la ligne des champs absents EXACTEMENT, sans se contenter de `Contains`.
- Les tests 4 et 5 excluent chacun les deux autres états.
- Les racines de l'app sont supprimées au `Dispose` : `DiagnosticServiceTests` devient `IDisposable`, et aucun test
  existant n'est modifié.

**4. [Sonde] Deux vues de plus que le plan pour prouver la lecture seule.**
- Le plan prévoyait le relevé de ce shell. S'y ajoutent deux vues :
  - Python, lancé par l'alias MSIX, qui tourne hors de l'arbre (29-RESEARCH Q0.a) et voit donc les fichiers RÉELS de
    l'overlay ;
  - la sonde elle-même, au début et à la fin de son exécution.
- La vue de ce shell voit le cache du paquet, pas les fichiers de l'overlay : à elle seule, elle n'aurait rien prouvé.
- Le libellé « après », passé en argument à Python, a été mal encodé par la console. L'en-tête le rétablit, et les
  valeurs ne sont pas touchées.

---

**Total :** 1 écart auto-corrigé (Rule 2), 3 renforcements. **Impact :** aucun élargissement de périmètre, aucune
garde assouplie, aucun test existant modifié.

## Issues Encountered

- **Heredoc Bash.** Un heredoc contenant le bloc de tests a échoué (« unexpected EOF »), comme en 29-03. Les blocs
  et les scripts d'édition ont été écrits dans le bloc-notes, puis appliqués par Python, en conservant les fins de
  ligne : CRLF pour `DiagnosticService.cs` et `DiagnosticServiceTests.cs`, LF pour les deux autres.
- **Bloc-notes partagé.** Le bloc-notes est commun à tous les agents de la session. Mon `t1_green.py` a écrasé un
  fichier du même nom laissé par l'agent de 29-03, déjà terminé. La date le prouve : l'écrasement date de 23:50:26,
  après le dernier commit de 29-04 à 23:50:13. 29-04 n'a pas été touché. La sonde a travaillé dans un sous-dossier
  à nom unique (`sonde-app06`), supprimé ensuite.
- **Overlay en marche.** L'overlay 3.1.0 (PID 40772) écrit lui-même le `treated.json` réel (21:46:49Z, avant la
  sonde). Ni lancé ni arrêté, il n'a rien écrit pendant la fenêtre de la sonde.

## Known Stubs

Aucun. `grep SQUELETTE|NotImplementedException|TODO|FIXME` sur `DiagnosticService.cs` donne 0.

## Exigences

- **APP-04** est livré : section en quatre états, lignes par session, même lecture que le widget sous garde, 8 cas
  plus la garde. **Coché.**
- **APP-05** est livré. Il tient par trois gardes :
  - le texte du lecteur (29-02) ;
  - le comportement, y compris l'empreinte (29-02) ;
  - le chemin en un seul lieu sans écriture (ici).

  Elles sont falsifiées par trois mutations ((a), (b), (c)), et les listes existantes n'ont pas été assouplies.
  **Coché.**
- **APP-06** est livré (29-01) et constaté hors de l'arbre avec le code livré (ici). **Coché.** La confirmation dans
  l'overlay PUBLIÉ reste la vérification manuelle de la phase 31, par le menu « Diagnostic… » de l'exe 3.2.0 (voir
  29-VALIDATION.md, « Manual-Only Verifications »).

## Next Phase Readiness

- **30 (LUE-01) :** `ParSession[id].DernierFocus` est lu sur les données réelles (18 sur 18 avec focus). Le rapport
  affiche désormais le focus de chaque ligne, ce qui sert d'instrument pour régler la règle « lue ».
- **31 :** au lancement normal de l'exe publié, le rapport doit dire « Source app-bureau : trouvée —
  %LOCALAPPDATA%\Packages\Claude_…\claude-code-sessions » et compter la racine d'état du paquet. La sonde en donne
  les valeurs attendues : 138 / 18 / 18, et les jointures.

## Self-Check: PASSED

- Fichiers présents : `src/Chronos/Services/DiagnosticService.cs`, `tests/Chronos.Tests/DiagnosticServiceTests.cs`,
  `tests/Chronos.Tests/GardesPerimetreTests.cs`, `tests/Chronos.Tests/LectureSeuleAppBureauTests.cs`,
  `29-SONDE-APP06.txt` et ce SUMMARY.
- Commits présents dans `git log` : `fc2c834`, `7d21a11`, `dd974f1`, `415cd7d`, ainsi que la référence d'entrée
  `3c5f395`.
