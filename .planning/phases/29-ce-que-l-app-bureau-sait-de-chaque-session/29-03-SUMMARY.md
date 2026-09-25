---
phase: 29-ce-que-l-app-bureau-sait-de-chaque-session
plan: 03
subsystem: widget-sessions
tags: [csharp, app-bureau, arbitrage, fus-01, moniteur, titre, degradation-v1-6, gardes, mutation, xunit, tdd]

sha_entree_de_plan: f02add4
one_liner: "La question que Claude pose dans l'app bureau devient une attente OBSERVÉE : SourceSession.AppBureau entre dans l'arbitrage FUS-01 entre le hook et le transcript (rang prouvé sur 720 ordres, fraîcheur toujours première), le moniteur ne la dépose que pour une session déjà connue d'un hook ou d'un transcript, sous 8 h, non archivée, datée par son épisode figé — l'app qualifie une ligne, elle n'en crée pas ; le titre de l'app est posé sur les retenus APRÈS l'arbitrage (SessionSnapshot.Titre, AffichageSessions.Nom) ; sans dossier ou sur fichier illisible, la lecture est exactement celle de la v1.6 ; la production branche le lecteur par argument nommé sous garde ; 1000 → 1039 tests, 0 échec, 5 mutations jouées et révoquées"

requires:
  - phase: 29-01
    provides: "RacinesCandidates.SessionsAppBureau (singleton du conteneur), SessionMonitor.Dossiers, câblage par argument nommé"
  - phase: 29-02
    provides: "LecteurAppBureau (Lire, cache, dernière lecture valide, épisode figé par postTurnSummaryFor), MetadonneesAppBureau, LectureAppBureau, helper de tests RacineAppBureau, six fixtures réelles"
  - phase: 28-04
    provides: "HorizonsSessions.Abandon (borne incluse), AppliquerSilence en un point, RangArbitrage figé"
provides:
  - "SourceSession.AppBureau, déclarée entre Hook et Transcript : l'ordre de déclaration est le rang à âge égal"
  - "DiagnosticService.LibelleSourceSession (internal), libellé « classification de fin de tour de l'app bureau (métadonnées de session) »"
  - "SessionMonitor : paramètre appBureau (dernier, optionnel), propriété internal Lecteur, QuestionPosee, Enrichir"
  - "SessionSnapshot.Titre (string? = null, dernier paramètre) ; LectureSessions.AppBureau (LectureAppBureau? = null)"
  - "AffichageSessions.Nom(s) : le titre sinon le dossier — un producteur pour le widget (29-04) et le rapport (29-05)"
  - "App.xaml.cs : singleton LecteurAppBureau sur RacinesCandidates.SessionsAppBureau, passé au moniteur par argument nommé"
  - "docs/hooks-contract.md §3 : ligne WaitingAttention et paragraphe « Trois sources, un arbitrage », tenus par une garde croisée"
affects: [29-04-l-ecran-du-titre, 29-05-diagnostic-et-constat, 30-la-lecture-fait-disparaitre]

tech-stack:
  added: []
  patterns:
    - "Une source qui QUALIFIE sans créer : signal déposé seulement pour un identifiant déjà déposé ce cycle par une source d'activité, avec l'identifiant et le dossier de cette source"
    - "Un champ d'affichage posé APRÈS l'arbitrage, sur les retenus : jamais un critère de départage caché ; garde de source sur l'ordre textuel"
    - "Dégradation exacte prouvée par comparaison champ par champ de deux moniteurs (sans lecteur / lecteur sans dossier ou sur fichier illisible)"
    - "Garde textuelle falsifiée par l'anti-pattern réel (g2) que les tests de comportement ne voient pas"

key-files:
  created:
    - tests/Chronos.Tests/MoniteurAppBureauTests.cs
  modified:
    - src/Chronos/Services/ArbitrageSessions.cs
    - src/Chronos/Services/DiagnosticService.cs
    - src/Chronos/Services/SessionSnapshot.cs
    - src/Chronos/Services/LectureSessions.cs
    - src/Chronos/Services/AffichageSessions.cs
    - src/Chronos/Services/SessionMonitor.cs
    - src/Chronos/App.xaml.cs
    - docs/hooks-contract.md
    - tests/Chronos.Tests/ArbitrageSessionsTests.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - tests/Chronos.Tests/ContratHooksDocumenteTests.cs
    - tests/Chronos.Tests/AffichageSessionsTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs

key-decisions:
  - "blocked est une TROISIÈME SOURCE DATÉE (SourceSession.AppBureau), pas un enrichissement : rang à âge égal hook, puis app bureau, puis transcript ; la fraîcheur prime toujours (C4 : l'app bat le transcript ; C5 : le hook bat l'app)"
  - "L'app qualifie une ligne, elle n'en crée pas : question déposée seulement pour un identifiant déjà déposé ce cycle par un transcript ou un hook, avec l'identifiant et le dossier de cette source (jamais le dossier de l'app) ; à instant égal entre deux sources connues, le dossier le premier en ordre ordinal"
  - "Conditions de dépôt, toutes nécessaires : Bloquee, motif non blanc, non archivée, InstantClassification (épisode figé par le lecteur) à au plus HorizonsSessions.Abandon, borne incluse"
  - "Piège 8 accepté : C1 produit un désaccord « retenu classification de fin de tour de l'app bureau « En attente » ; écarté transcript « En attente » », lisible par le libellé de source, aucun mot nouveau"
  - "Le titre est posé sur les RETENUS après Trancher et avant les filtres (les masquées le portent) ; le détecteur observe les vainqueurs non enrichis ; un titre blanc n'est pas un titre"

requirements-completed: [APP-01, APP-03]
requirements-advanced: [APP-02]

duration: 19min
completed: 2026-09-25
---

# Phase 29 Plan 03 : La question de l'app bureau dans l'arbitrage, et le titre — Summary

**La question que Claude pose dans l'app bureau devient une attente observée. La session s'affiche « En attente »,
au rang des questions, et porte le motif de l'app. L'app entre dans l'arbitrage FUS-01 comme troisième source,
entre le hook et le transcript : le rang est prouvé sur 720 ordres d'arrivée, et la fraîcheur reste le premier
critère. Le moniteur ne dépose la question que pour une session déjà connue d'un hook ou d'un transcript, sous
8 h, non archivée, et il la date par son épisode figé : l'app qualifie une ligne, elle n'en crée pas. Le titre de
l'app est posé sur les lignes retenues, après l'arbitrage. Sans dossier de l'app, ou avec un fichier illisible, la
lecture est exactement celle de la v1.6. La production branche le lecteur, et une garde le vérifie. La suite passe
de 1000 à 1039 tests, 0 échec ; cinq mutations ont été jouées puis révoquées.**

## Performance

- **Début :** 2026-09-25T21:20:49Z. **Fin :** 2026-09-25T21:39:09Z. **Durée :** environ 19 min.
- **SHA d'entrée :** `f02add4`, avec **1000 verts / 0 échec** mesurés à l'entrée (7 s).
- **Tâches :** 3 sur 3, en TDD, soit 6 commits de tâche.
- **Fichiers :** 15, exactement ceux du plan : 1 classe de tests créée, 7 sources, 1 document et 6 classes de
  tests modifiés.

## Mesures (tests)

| Moment | Filtre de la tâche | Suite complète |
|---|---|---|
| Entrée (`f02add4`) | — | **1000 / 0** |
| Tâche 1, RED (`42ce2e2`) | **5 échecs / 86** | — |
| Tâche 1, GREEN (`0db86f1`) | 86 / 86 | **1009 / 0** = 1000 + 9 |
| Tâche 2, RED (`0e548c2`) | **28 échecs / 151** | — |
| Tâche 2, GREEN (`ee09f2b`) | 151 / 151 | **1037 / 0** = 1009 + 28 |
| Tâche 3, RED (`a129126`) | **1 échec / 20** | — |
| Tâche 3, GREEN (`2753384`) | — | **1039 / 0**, deux exécutions consécutives (8 s et 8 s) |

Le total annoncé par le plan est atteint : 1000 + 9 + 28 + 2 = **1039**.

## Les rouges (TDD)

- **Tâche 1.** Le squelette déclare `AppBureau` en FIN d'énumération, pour compiler. `LibelleSourceSession`
  passe en `internal`, sans libellé pour la nouvelle source. **5 rouges :**
  - `L_ordre_des_sources_est_hook_puis_app_bureau_puis_transcript` ;
  - `A_age_egal_l_app_bureau_bat_le_transcript_dans_les_deux_ordres` ;
  - `Permuter_trois_sources_ne_change_pas_un_seul_etat_ni_un_seul_desaccord` ;
  - `Chaque_source_de_session_a_un_libelle_nomme` ;
  - `Le_paragraphe_3_dit_les_trois_sources_et_que_l_app_qualifie_sans_creer`.

  Les quatre autres cas (C1, C2, C5, C6) sont verts sur ce squelette. Ils se tranchent par la fraîcheur, ou par le
  rang Hook < AppBureau, et aucun des deux ne dépend de la place au milieu.
- **Tâche 2.** Les squelettes posent `SessionSnapshot.Titre` et `LectureSessions.AppBureau`. Le paramètre
  `appBureau` est reçu mais ignoré, et `Nom` lève `NotImplementedException`. **Les 28 nouveaux cas sont rouges**,
  et les 123 autres cas du filtre restent verts. Chaque cas dont l'attendu est « rien ne change » (C7, C8 à
  481 min, C9 ×7, sans titre, sans métadonnées) asserte aussi que le lecteur a LU et joint le fichier (par
  exemple `ParSession` contient l'identifiant). Il est donc rouge sur le squelette et n'est pas vert par vacuité.
- **Tâche 3.** Rouge : `Le_moniteur_de_production_recoit_le_lecteur_de_l_app_bureau`, car App.xaml.cs ne
  construit pas encore le lecteur. La seconde garde, `Le_titre_est_pose_apres_l_arbitrage_et_nulle_part_ailleurs`,
  est **verte dès son arrivée** : elle garde le texte livré en tâche 2. Son rouge vient des mutations (g1) et (g2)
  ci-dessous (écart n° 3).

## Mutations (jouées, constatées, révoquées)

| Mutation | Fichier (sha256 avant = après) | Rouges constatés |
|---|---|---|
| **(d)** `AppBureau` déclarée APRÈS `Transcript` | `ArbitrageSessions.cs` `8d043267…c140` | **4** : `L_ordre_des_sources…`, `A_age_egal_l_app_bureau_bat_le_transcript…`, `Permuter_trois_sources…` (« Not found: "a4=WaitingAttention@" ») et, en plus du plan, la garde croisée `Le_paragraphe_3_dit_les_trois_sources…` |
| **(e)** `m.DerniereActivite` au lieu de `m.InstantClassification` dans `QuestionPosee` | `SessionMonitor.cs` `a709674f…4fa3` | **1 / 24** : `Une_question_marquee_traitee_ne_revient_pas_quand_l_activite_de_fond_avance` |
| **(f)** dépôt pour toute session de l'app (`foreach (var (id, m) in appBureau.ParSession)`) | `SessionMonitor.cs` `a709674f…4fa3` | **1 / 24** : `C7_sans_autre_source_la_question_de_l_app_ne_cree_aucune_ligne`, qui trouve une ligne fabriquée « (session) », `WaitingAttention`, titre « Session E » |
| **(g1)**, en plus du plan : un second porteur `s with { Titre = null }` dans `AffichageSessions.cs` | `AffichageSessions.cs` `c8b82ae3…7e04` | `Le_titre_est_pose_apres…` (« Actual: ["AffichageSessions.cs", "SessionMonitor.cs"] ») |
| **(g2)**, en plus du plan : les signaux enrichis AVANT `Trancher` | `SessionMonitor.cs` `a709674f…4fa3` | `Le_titre_est_pose_apres…` (« Enrichir( à 12319, ArbitrageSessions.Trancher( à 12388 »). **Les 24 tests de comportement restent verts** : c'est cet anti-pattern que seule la garde textuelle attrape |

Chaque mutation a été révoquée par copie de sauvegarde. Le sha256 est vérifié identique après chaque
révocation, et aucune mutation n'a été commitée.

## Task Commits

1. **Tâche 1 : trois sources dans l'arbitrage, un libellé pour chacune, et le contrat.**
   - `42ce2e2` (test, RED 5/86)
   - `0db86f1` (feat, GREEN, suite complète 1009/0)
2. **Tâche 2 : le moniteur dépose les questions de l'app et pose le titre.**
   - `0e548c2` (test, RED 28/151)
   - `ee09f2b` (feat, GREEN, suite complète 1037/0)
3. **Tâche 3 : la production branche le lecteur, sous deux gardes de source.**
   - `a129126` (test, RED 1/20)
   - `2753384` (feat, suite complète 1039/0 deux fois)

**Plan metadata :** commit `docs(29-03)` final (SUMMARY, STATE, ROADMAP, REQUIREMENTS).

## Accomplishments

- **Trois sources, une règle écrite.** `SourceSession.AppBureau` est déclarée au milieu de l'énumération. Sa
  XML-doc dit pourquoi c'est permis ici : `SourceSession` n'est persisté nulle part. La XML-doc de l'arbitrage dit
  « hook, puis app bureau, puis transcript » et exclut le titre des critères, puisqu'il est posé après.
  `Departager` n'a pas changé, et `RangArbitrage` non plus. Les corpus d'origine
  (`Permuter_l_ordre_des_signaux…`, `Remonter_la_deduction…`) sont inchangés et verts.
- **Le moniteur qualifie, il ne crée pas.** L'étape 2.c vient après la collecte des hooks et avant `Trancher` :
  1. un `Lire(now)` par cycle, en best-effort ;
  2. le dossier connu de chaque identifiant (dictionnaire `projetConnu`, sans `Dictionary<string, SessionSnapshot>`) ;
  3. le dépôt d'un signal `AppBureau` uniquement quand `QuestionPosee` rend une question.

  La question passe par `AppliquerSilence`, comme tout signal. Elle ne change rien, puisqu'elle n'est pas du
  travail (C11).
- **Le titre suit la ligne.** `Enrichir` s'applique aux RETENUS après le détecteur, qui observe les vainqueurs
  non enrichis, et avant les filtres : une session archivée porte son titre (test n° 4). Sans dossier de l'app, la
  liste rendue est `arbitrage.Retenus` telle quelle.
- **La lecture de l'app est partagée.** `LectureSessions.AppBureau` est la lecture de CE cycle, celle qui a
  qualifié les lignes, et le rapport de 29-05 la lira là (OBS-01).
- **La dégradation v1.6 est exacte.** Deux moniteurs sont comparés : l'un sans lecteur, l'autre avec un lecteur
  sur un dossier absent ou sur un fichier illisible. Ils ont les mêmes entrées : trois sessions et un fichier de
  hook qui crée un désaccord. La comparaison porte, champ par champ, sur les visibles, les masquées, les
  désaccords et les fichiers écartés.
- **Production.** App.xaml.cs enregistre un singleton `LecteurAppBureau` sur
  `RacinesCandidates.SessionsAppBureau` et le passe au moniteur par `appBureau:`. `DiagnosticService` n'est pas
  touché.

## Critères grep

| Fichier | Critère | Attendu | Mesuré |
|---|---|---|---|
| `DiagnosticService.cs` | `internal static string LibelleSourceSession(SourceSession s)` | 1 | 1 |
| `DiagnosticService.cs` | `SourceSession.AppBureau =>` | 1 | 1 (voir l'écart n° 1) |
| `DiagnosticService.cs` | `new LecteurAppBureau` | 0 | 0 |
| `hooks-contract.md` | `L'app qualifie une ligne, elle n'en crée pas` | 1 | 1 |
| `SessionSnapshot.cs` | `string? Titre = null` | 1 | 1 |
| `LectureSessions.cs` | `LectureAppBureau? AppBureau = null` | 1 | 1 |
| `SessionMonitor.cs` | `new SignalSession(SourceSession.AppBureau` | 1 | 1 |
| `SessionMonitor.cs` | `Dictionary<string, SessionSnapshot>` / `TimeSpan.From` / `byId[` / `Claude_` / `claude-code-sessions` | 0 | 0 / 0 / 0 / 0 / 0 |
| `SessionMonitor.cs` | `Activity = SessionActivity.WaitingDeduced` | 1 | 1 |
| `SessionMonitor.cs` | `with { Titre` | 1 | 1 |
| `App.xaml.cs` | `appBureau: sp.GetRequiredService<LecteurAppBureau>()` | 1 | 1 |
| `App.xaml.cs` | `new LecteurAppBureau(sp.GetRequiredService<RacinesCandidates>().SessionsAppBureau)` | 1 | 1 |

**Vérification du plan :**
- `git diff --stat f02add4..HEAD -- src/Chronos/ViewModels src/Chronos/Resources src/Chronos/Views` est
  **vide** : l'écran relève de 29-04.
- Aucun test ne lit le vrai `%APPDATA%`, `%LOCALAPPDATA%` ou `~/.claude`. Les lecteurs n'ont que des racines
  temporaires, et un `ArchiveStore` temporaire est injecté partout. Les `TreatedStore` sont temporaires, sur
  `FakeClock`. Le moniteur miroir de `CompositionRootTests` n'appelle jamais `Inspecter`.
- `MoniteurAppBureauTests` supprime tout ce qu'il crée (`IDisposable`). Après la suite, aucun dossier
  `chronos-moniteur-app-*` ni `chronos-appbureau-*` ne reste. L'overlay n'a pas été lancé.

**Gardes vertes, sans assouplissement :**

| Classe de tests | Verts |
|---|---|
| `GardesPerimetreTests` | 15/15 (13 + 2) |
| `ServicesLayerPurityTests` | 2/2 |
| `CompositionRootTests` | 5/5 (1 adapté) |
| `NormalisationUniqueTests` | 3/3 |
| `ContratHooksDocumenteTests` | 15/15 (14 + 1) |
| `GardesDoctrineTests` | 8/8 |
| `HorizonsSessionsTests` | 11/11 |
| `LibellesSessionsTests` | 5/5 |
| `ArbitrageSessionsTests` | 22/22 (15 + 7) |
| `DiagnosticServiceTests` | 31/31 (30 + 1) |
| `MoniteurAppBureauTests` (nouvelle) | 24/24 |
| `AffichageSessionsTests` | 35/35 (+4) |
| `InspectionSessionsTests` | 26/26 (inchangés) |
| `TreatedSessionsTests` | 15/15 |
| `GesteTraiteTests` | 5/5 |
| `LecteurAppBureauTests` | 31/31 |
| `LectureSeuleAppBureauTests` | 4/4 |
| `DeuxVuesAppDataTests` | 6/6 |

**Test adapté, sans renommage :** `CompositionRootTests.Le_graphe_DI_resout_la_chaine_de_sessions`. Le miroir
ajoute le singleton `LecteurAppBureau` construit sur les racines candidates temporaires, ainsi que l'argument
nommé `appBureau:`. Il asserte ensuite quatre choses :
- le `Assert.Same` du lecteur sur deux résolutions ;
- des `Candidats` non vides, tous sous `Path.GetTempPath()` ;
- `Assert.Same(lecteur, moniteur.Lecteur)` ;
- l'appel à `Inspecter` n'existe toujours pas.

La garde existante `Le_moniteur_de_production_lit_les_deux_vues_d_AppData` n'a pas changé et reste verte : le
fragment du moniteur contient `appBureau:` mais pas `SessionsAppBureau`, qui n'apparaît que dans
l'enregistrement du lecteur.

## Decisions Made

- **(b) La place de `blocked` dans FUS-01 : c'est une troisième source datée.** Ce n'est pas un enrichissement.
  Le rang à âge égal est hook, puis app bureau, puis transcript. Le hook seul sait dire « permission ». Le
  transcript ne voit pas une question posée en prose.
- **L'app qualifie une ligne, elle n'en crée pas.** Le dépôt est limité aux identifiants déjà déposés ce cycle.
  La ligne garde le dossier de la source d'activité, jamais `ProjectFromCwd` de l'app.
- **Piège 8, accepté.** C1 consigne un désaccord entre la question (app bureau) et le tour fini (transcript),
  lisible par les libellés de source. Aucun mot nouveau n'est inventé.
- **Le titre n'est pas un signal.** Il est posé après `Trancher`, sur les retenus, avant les filtres. Un titre
  blanc n'est pas un titre.

## Deviations from Plan

### Auto-fixed Issues

**1. [Critère grep] `SourceSession.AppBureau =>` écrit avec un seul espace.**
- **Constat :** l'alignement de la table `switch` à deux espaces (`SourceSession.AppBureau  =>`) donnait 0 au
  critère `grep -c "SourceSession.AppBureau =>"`, qui exige 1.
- **Correctif :** la ligne de l'app bureau est écrite avec un seul espace, donc décalée d'un caractère par
  rapport à ses voisines. Les deux autres libellés ne changent pas, mot pour mot et espace pour espace.
- **Commit :** `0db86f1`.

**2. [Rule 2 - Déterminisme] À instant égal, le dossier retenu pour la question est le premier en ordre ordinal.**
- **Constat :** le code du plan retient le dossier du signal connu le plus récent. Si deux sources connues
  datent du même instant avec des dossiers différents, l'ordre de collecte décidait. FUS-01 interdit précisément
  cela.
- **Correctif :** à instant égal, la condition `string.CompareOrdinal(...) < 0` départage. Le cas est
  pratiquement inatteignable, et le coût est nul.
- **Commit :** `ee09f2b`.

**3. [Méthode] La garde n° 2 de la tâche 3 est verte dès son arrivée ; son rouge vient des mutations g1 et g2.**
- **Constat :** le plan attend que « les deux gardes rougissent avant le câblage ». Or la garde du titre lit
  `SessionMonitor.cs` et `Services/`, livrés en tâche 2 ; elle ne dépend pas d'App.xaml.cs. Elle ne pouvait
  donc pas être rouge au commit de test de la tâche 3.
- **Correctif :** la garde a été falsifiée par deux mutations jouées puis révoquées. (g1) ajoute un second
  porteur de `with { Titre`. (g2) enrichit les signaux avant `Trancher`, c'est-à-dire l'anti-pattern réel ; sous
  (g2), les 24 tests de comportement restent verts.

**4. [Rule 2 - Non-vacuité] Assertions supplémentaires dans les cas dont l'attendu est « rien ne change ».**
- C7, C8, C9, C3, « sans métadonnées » et « sans titre » assertent aussi que le lecteur a lu et joint le fichier
  (`ParSession`, `ClassificationFinDeTour`, `RelusSurDisque`).
- Le test d'épisode asserte que `DerniereActivite` a avancé de 190 s pendant que `InstantClassification` restait
  A : la mutation (e) ne peut donc mordre que sur l'épisode.
- Les tests de dégradation ajoutent un fichier de hook qui crée un désaccord, pour que l'égalité des désaccords
  compare quelque chose. Ils comparent aussi les masquées et les fichiers écartés.

**5. [Précision] 1,561 s en millisecondes entières.**
- `TimeSpan.FromMilliseconds(1561)` et `AddMilliseconds` remplacent `AddSeconds(1.561)`, pour éviter tout arrondi
  de `double`. La garde `TimeSpan.From` ne porte que sur `SessionMonitor.cs`.

---

**Total :** 2 écarts auto-corrigés (critère, déterminisme), 1 écart de méthode, 2 renforcements de tests.
**Impact :** aucun élargissement de périmètre, aucune garde assouplie, aucun test existant modifié hors le test
DI adapté que le plan prévoit.

## Issues Encountered

- **Heredoc Bash et long script Python.** Un heredoc contenant un gros script d'édition a échoué (« unexpected
  EOF »). Les scripts d'édition ont été écrits dans le bloc-notes de session, puis exécutés. Aucun fichier du
  dépôt n'a été touché hors de ce qui est commité.
- **Fins de ligne.** Avec `core.autocrlf=true`, les sources sont en CRLF sur disque. L'outil de remplacement
  normalise l'ancien et le nouveau texte vers la fin de ligne du fichier, et les mutations ont été écrites en
  CRLF. Git signale « LF will be replaced by CRLF » pour `LectureSessions.cs`, `SessionSnapshot.cs`,
  `GardesPerimetreTests.cs` et la nouvelle classe de tests. Ces fichiers étaient déjà en LF sur disque, et le
  dépôt normalise.

## Known Limits (écrites, non corrigées ici)

- **Un `Lire` qui lèverait rendrait `LectureSessions.AppBureau` nul**, c'est-à-dire la même valeur qu'un moniteur
  sans lecteur. Le lecteur est écrit pour ne jamais lever. Pour que le rapport de 29-05 distingue « NON
  BRANCHÉE » de « lecture ratée », il peut consulter `SessionMonitor.Lecteur` (internal) en plus de
  `LectureSessions.AppBureau`.
- **Le libellé de source de l'app bureau ne nomme pas de dossier**, contrairement aux deux autres. La racine
  effective relève de la section « Source app-bureau » de 29-05 (APP-04).

## Known Stubs

Aucun. Les squelettes RED (`AppBureau` en fin d'énumération, paramètre ignoré, `Nom` qui lève) ont été remplacés
dans les commits `feat`. `grep SQUELETTE|NotImplementedException|TODO|FIXME` sur les sept sources modifiées
donne 0.

## Exigences

- **APP-03** : livré. C1 à C11 sont en test contre les classes réelles, le rang est écrit et prouvé sur 720
  ordres, et le contrat le dit. **Coché.**
- **APP-01** : la lecture tolérante vient de 29-02 ; la jointure par `cliSessionId`, la dégradation v1.6 exacte
  (dossier absent, fichier illisible) et le câblage de production sous garde viennent d'ici. **Coché.**
- **APP-02** : `SessionSnapshot.Titre` et `AffichageSessions.Nom` sont posés. L'écran (8 styles × 9 thèmes,
  info-bulle, `MaxWidth`) relève de 29-04, donc **APP-02 reste Pending**.

## Next Phase Readiness

- **29-04** : `Project = AffichageSessions.Nom(s)` dans `SessionsViewModel`. `Infobulle` reste à produire à côté
  de `Nom`, dans `AffichageSessions`. La galerie aura besoin d'un échantillon à titre long.
- **29-05** : la section « Source app-bureau » du rapport lit `LectureSessions.AppBureau` (jamais un second
  `Lire`). La garde n° 2 d'APP-05 et le constat hors de l'arbre relèvent aussi de 29-05.
- **30 (LUE-01)** : `ParSession[id].DernierFocus` est disponible, et l'instant d'une question est celui de son
  épisode, ce qui évite qu'une attente se rajeunisse après le focus.

## Self-Check: PASSED

- Fichiers vérifiés présents : `tests/Chronos.Tests/MoniteurAppBureauTests.cs` (497 lignes, au moins 300
  requises) et ce SUMMARY.
- Commits vérifiés dans `git log` : `42ce2e2`, `0db86f1`, `0e548c2`, `ee09f2b`, `a129126`, `2753384`, ainsi que
  la référence d'entrée `f02add4`.
