---
phase: 30-la-lecture-fait-dispara-tre
plan: 01
subsystem: widget-sessions
tags: [csharp, detecteur, treated-store, lue-01, lue-02, lue-03, lue-04, net-03, hysteresis, mutation, sentinelle, xunit, tdd]

sha_entree_de_plan: af60f59
one_liner: "Le détecteur sait maintenant LIRE. SessionTreatmentTracker.Observe reçoit un ContexteLecture optionnel (le dernier focus par session, la session sélectionnée, l'instant depuis lequel claude est au premier plan). Il décide NET-03 et la règle « lue » en UN bloc, selon la table à six cas : une attente strictement antérieure au focus (LUE-01), ou la session sélectionnée regardée au premier plan depuis HorizonsSessions.GraceLecture = 2,5 s, borne incluse (LUE-02), est inscrite à l'instant de son épisode, au plus une fois par épisode (la sentinelle de date d'écriture reste intacte sur cinq cycles). Sans contexte ou sans focus connu, rien ne change par rapport à la v1.6 (LUE-04). Le détecteur retient en mémoire la cause de chaque inscription (lue par focus, lue au premier plan, répondue), figée à son premier constat ; il n'en invente aucune pour un geste ou pour une entrée antérieure à son démarrage (LUE-03). Mutations (m1), (m2) et (m4) jouées puis révoquées. Tests : 1062 → 1088 pour ce plan (+26), et 1110 / 0 échec avec 30-02, sur deux exécutions."

requires:
  - phase: 28
    provides: "HorizonsSessions (siège unique des seuils, gardes de chaîne et de câblage), datation des attentes par le dernier message (D-28-01)"
  - phase: 29
    provides: "lastFocusedAt lu par le lecteur de l'app (source du DernierFocus que 30-03 construira), LectureSessions.AppBureau"
provides:
  - "SessionTreatmentTracker.Observe(vainqueurs, now, ContexteLecture? lecture = null) : décision unique NET-03 / LUE-01 / LUE-02 (table à six cas)"
  - "SessionTreatmentTracker.CauseDe(string sessionId, long episodeTraite) : la cause constatée, ou nul (résidu honnête)"
  - "ContexteLecture(DernierFocus, Selectionnee, ClaudeAuPremierPlanDepuis) et CauseTraitement(Motif, Attente, Focus, PremierPlanDepuis, Constat)"
  - "MotifMasquage.LueParFocus / LueAuPremierPlan / Repondue (en fin d'enum) ; SessionMasquee.Cause optionnelle"
  - "HorizonsSessions.GraceLecture = 2,5 s, sous garde (G04)"
affects: [30-03-moniteur-et-cablage, 30-04-diagnostic, 31-constat]

tech-stack:
  added: []
  patterns:
    - "Une décision, pas deux blocs : l'ajout et la purge d'un magasin réversible, décidés sur UNE lecture de tête de cycle, pour qu'il n'y ait jamais plus d'une écriture par épisode"
    - "Preuve de non-écriture par sentinelle de date d'écriture (File.SetLastWriteTimeUtc 2000-01-01), sans compteur ni seam dans le magasin"
    - "Une cause en mémoire, clé (session, épisode), figée au premier constat ; ce qui n'a pas été constaté n'a pas de cause"
    - "Un paramètre optionnel nul par défaut pour que les appels existants gardent le comportement v1.6 : LUE-04 est vrai par construction"

key-files:
  created:
    - tests/Chronos.Tests/DetecteurLectureTests.cs
  modified:
    - src/Chronos/Services/SessionTreatmentTracker.cs
    - src/Chronos/Services/LectureSessions.cs
    - src/Chronos/Services/HorizonsSessions.cs
    - tests/Chronos.Tests/HorizonsSessionsTests.cs

key-decisions:
  - "D-30-01 : l'instant comparé au focus ET inscrit est l'épisode du détecteur (_attenteDepuis), égal à UpdatedAt du signal retenu en régime permanent"
  - "D-30-02 : LUE-02 = focus connu ET session sélectionnée (OrdinalIgnoreCase) ET claude au premier plan depuis d ET now − max(épisode, d) ≥ GraceLecture (2,5 s, borne incluse)"
  - "D-30-03 : LUE-01 en comparaison stricte, un focus égal à l'attente n'est pas une lecture"
  - "D-30-05 : treated.json garde le format id → ms ; la cause vit en mémoire dans le détecteur, et ce qu'il n'a pas constaté n'a pas de cause"
  - "D-30-06 : NET-03 et LUE forment un seul bloc (table à six cas) ; Set seulement si le magasin porte un épisode antérieur, Remove seulement si la session n'est pas lue"
  - "D-30-07 : une attente DÉDUITE ouverte ensuite est lue au même titre ; redevenue une attente observée plus récente que le focus, elle revient (NET-03)"
  - "D-30-08 : une demande de permission vue est lue (elle quitte le widget pendant que la session reste bloquée), à constater en phase 31"
  - "La cause d'un épisode est figée à son premier constat ; seule une réponse (NET-01) la remplace, parce qu'une réponse est un fait nouveau"

requirements-completed: []
requirements-advanced: [LUE-01, LUE-02, LUE-03, LUE-04]

duration: 15min
completed: 2026-09-26
---

# Phase 30 Plan 01 : Le détecteur lit — Summary

**Le détecteur sait maintenant LIRE. `SessionTreatmentTracker.Observe` reçoit en option un `ContexteLecture` : le
dernier focus de chaque session, la session sélectionnée dans l'app, et l'instant depuis lequel `claude` est au premier
plan. Il décide NET-03 et la règle « lue » en UN seul bloc. Une attente strictement antérieure au focus est inscrite à
l'instant de son épisode, sans clic (LUE-01) : JARVIS, finie à 13:59:05Z et ouverte à 14:00:39Z, donne 1790344745000.
La session sélectionnée, regardée au premier plan, est inscrite 2,5 s après le plus tardif de la fin du tour et du
retour au premier plan (LUE-02) : pour le geste B, elle est lue à 18:34:06.828Z, pas à 18:34:06.827Z. Chaque épisode
n'est écrit qu'une fois, et la sentinelle de date d'écriture reste intacte sur cinq cycles. Sans contexte, rien ne change
par rapport à la v1.6 (LUE-04). Le détecteur retient aussi la cause de chaque inscription, figée à son premier constat,
et n'en invente aucune (LUE-03). Tests : 1062 → 1088 pour ce plan (+26), et 1110 / 0 échec avec 30-02.**

## Performance

- **Début :** 2026-09-26T08:37:35Z. **Dernier commit de tâche :** `f0ab69a`, vers 08:49Z. Durée : environ 15 min.
- **SHA d'entrée :** `af60f59`, à **1062 verts / 0 échec** (mesure de l'orchestrateur à l'entrée de phase).
- **Tâches :** 2 sur 2, en 4 commits (un RED et un GREEN par tâche).
- **Fichiers :** 5 (3 sources, 2 classes de tests dont une nouvelle, de 483 lignes).

## Mesures (tests)

| Moment | Filtre | Suite complète |
|---|---|---|
| Entrée (`af60f59`) | — | **1062 / 0** |
| RED de la tâche 1 (squelettes) | `DetecteurLectureTests` + `HorizonsSessionsTests` : 11 échecs sur 30 | — |
| GREEN de la tâche 1 | les 9 classes du `<verify>` : **120 / 120** | 1103 dont 6 rouges, tous des RED volontaires de 30-02 (`LecteurAppBureauTests`, sélection) ; **1081** pour ce plan seul (1062 + 19) |
| RED de la tâche 2 (`CauseDe` rend nul) | `DetecteurLectureTests` : 5 échecs sur 25 | — |
| GREEN de la tâche 2 | gardes nommées + `DetecteurLectureTests` : 82 / 82 | **1110 / 0**, deux exécutions consécutives (8 s, 9 s) |

**Décompte isolé.** La suite sans les deux classes de 30-02 compte 1057, dont 31 cas de `LecteurAppBureauTests`
antérieurs à la phase qui en sont exclus : 1057 + 31 = **1088 = 1062 + 26**. Les deux classes de 30-02 comptent
53 cas (31 préexistants, 8 S et 14 P), et 1088 + 22 = **1110**, le total attendu en fin de vague 1.

## Les rouges (TDD)

**Tâche 1.** Squelettes compilables : `GraceLecture = TimeSpan.Zero`, `Observe` à trois paramètres qui ignore la
lecture, et les types `ContexteLecture` et `CauseTraitement` déclarés. **11 rouges sur 19 :** L01
`Une_attente_anterieure_au_focus_est_lue`, L04 `La_lecture_n_ecrit_qu_une_fois_par_episode`, L05
`Un_nouvel_episode_deja_lu_s_ecrit_sans_purge`, L06 `Une_session_lue_revient_sur_un_nouveau_tour`, L07
`La_deduite_ouverte_ensuite_est_lue`, L08 `La_deduite_redevenue_attente_observee_plus_recente_revient`, L09
`Une_permission_vue_est_lue`, L10 `Geste_B_le_tour_fini_sous_les_yeux_est_lu_au_premier_plan`, L13
`Un_retour_recent_au_premier_plan_attend_sa_grace`, L21 `La_session_selectionnee_se_compare_sans_casse`, G04
`La_grace_de_lecture_tient_sous_le_silence`. Le plan attendait 9 rouges. L06 et L08 rougissent en plus : ils vérifient
l'inscription intermédiaire avant d'en constater la purge, pour ne pas passer à vide. Les cas « magasin vide » (L02,
L03, L11, L12, L14, L15 ×2) et L20 sont verts par construction.

**Tâche 2.** Squelette : `CauseDe` rend toujours nul. **5 rouges sur 7 :** L16 `Repondue_remplace_la_cause_lue`, L17
`La_cause_survit_au_depart_du_premier_plan`, L22 `La_cause_est_figee_a_son_premier_constat`, L18
`Apres_redemarrage_la_lecture_par_focus_retrouve_sa_cause_sans_ecrire`, L23 `Une_session_revenue_oublie_sa_cause`.
Le plan attendait L23 vert par construction ; il rougit parce qu'il vérifie d'abord que la cause existait avant NET-03.
L19 et L24 sont verts par construction, comme prévu.

## Mutations : jouées, constatées, révoquées

sha256 de référence de `SessionTreatmentTracker.cs` après la tâche 1 :
`44264408eaac9be084886dfe2b1c4b225f4c58e3fcf2c625b28dcab3a5952193`. Il est identique après chaque révocation (restauration
depuis une copie, sha256 relu à chaque fois). `git diff` est vide avant le commit `3b7ce99`.

| Mutation | Changement | Échec constaté |
|---|---|---|
| (m1) | `if (!traitee \|\| tts < cur) _store.Set(id, cur);` remplacé par `_store.Set(id, cur);` | `La_lecture_n_ecrit_qu_une_fois_par_episode` (la sentinelle a bougé), seul |
| (m2) | deux blocs successifs : `if (lue is not null && (!traitee \|\| tts < cur)) _store.Set(id, cur);` PUIS `if (traitees.TryGetValue(id, out var t2) && cur > t2) _store.Remove(id);` | `Un_nouvel_episode_deja_lu_s_ecrit_sans_purge` (magasin vide), seul |
| (m4) | `>= HorizonsSessions.GraceLecture` remplacé par `> HorizonsSessions.GraceLecture` | `Geste_B_le_tour_fini_sous_les_yeux_est_lu_au_premier_plan`, plus `Un_retour_recent_au_premier_plan_attend_sa_grace` et `La_session_selectionnee_se_compare_sans_casse` (les trois tests posés exactement sur la borne) |

sha256 final de `SessionTreatmentTracker.cs` (après la tâche 2) :
`b4120d09f93cc1c44006dd295de5c9b2b8020b9aab9dfdd79fda24c357094018`.

## Task Commits

1. **Tâche 1, RED :** `7aad169` test(30-01) : la règle « lue » au détecteur, L01 à L15, L20, L21 et G04, RED 11/19 sur squelette
2. **Tâche 1, GREEN :** `3b7ce99` feat(30-01) : le détecteur lit, NET-03 et la règle « lue » en une décision (LUE-01, LUE-02, LUE-04)
3. **Tâche 2, RED :** `26e93b8` test(30-01) : la cause retenue par le détecteur, L16 à L19, L22 à L24, RED 5/7 sur squelette
4. **Tâche 2, GREEN :** `f0ab69a` feat(30-01) : la cause retenue, figée à son premier constat, jamais inventée (LUE-03)

## Accomplishments

- **La décision unique (D-30-06).** Elle remplace l'ancien bloc NET-03. Pour une session en attente d'épisode `cur` :
  si elle est lue, `Set(id, cur)` seulement si le magasin ne la porte pas ou porte un épisode antérieur (cas 4 et 5,
  sans `Remove` préalable) ; sinon, zéro écriture (cas 6). Si elle n'est pas lue, `Remove` seulement si l'épisode est
  plus récent que l'inscription (cas 3, NET-03 inchangé). La table à six cas est recopiée en commentaire au-dessus du bloc.
- **`Lue(...)` (D-30-01, D-30-02, D-30-03, LUE-04).** Pas de contexte, ou pas de focus connu pour CETTE session :
  aucune lecture. L'épisode est converti en instant par `UsageNormalization.InstantDepuisEpochMillisecondes`, jamais
  localement. LUE-01 est testée en premier (comparaison stricte), puis LUE-02 (sélection insensible à la casse, `claude`
  depuis `d`, `now − max(attente, d) ≥ GraceLecture`).
- **La cause (LUE-03, D-30-05).** Elle est gardée dans `_causes : id → (épisode, CauseTraitement)`. NET-01 la remplace
  toujours par « répondue ». LUE la pose à l'écriture (cas 4 et 5) ou au premier constat sans écriture (cas 6), puis
  elle reste figée. NET-03 la retire avec l'inscription. `CauseDe(id, tts)` ne rend la cause que si l'épisode demandé
  est celui qui a été constaté.
- **Les contrats (`LectureSessions.cs`).** Trois motifs ajoutés EN FIN de l'énumération (rien ne se réordonne).
  L'XML-doc de `Traitee` est réécrite : c'est le résidu honnête (« marquée à la main, ou traitée avant le démarrage de
  l'overlay et non relue depuis »). `CauseTraitement`, `ContexteLecture`, et `SessionMasquee.Cause` optionnelle : les
  trois constructions de `SessionMonitor.cs` compilent sans retouche.
- **Le seuil (`HorizonsSessions.GraceLecture = 2,5 s`).** Il est dans la liste des inégalités de la classe
  (`GraceLecture < Silence`). La garde G04 vérifie la valeur, les deux inégalités, sa lecture par le détecteur, et
  l'absence de `TimeSpan.From` dans ce fichier.
- **Ce qui n'a pas bougé.** Les 18 appels existants à deux arguments, `EstAttente`, `EstTravailObserve`,
  `InstantDuSignal`, et la condition NET-01 `prec.Source == v.Source`. Le scénario des 478 minutes
  (`TreatedSessionsTests`) et `ContratHooksDocumenteTests` restent verts sans retouche.

## Critères grep

| Critère | Attendu | Mesuré |
|---|---|---|
| `ContexteLecture? lecture = null` dans le détecteur | 1 | 1 |
| `HorizonsSessions.GraceLecture` dans le détecteur | ≥ 1 | 2 (XML-doc et prédicat) |
| `TimeSpan.From` / `FromUnixTimeMilliseconds` / `HorizonsSessions.Silence` dans le détecteur | 0 | 0 |
| `prec.Source == v.Source` | 1 | 1 |
| `GraceLecture = System.TimeSpan.FromMilliseconds(2500)` dans `HorizonsSessions.cs` | 1 | 1 |
| `MotifMasquage Motif, CauseTraitement? Cause = null` dans `LectureSessions.cs` | 1 | 1 |
| `public CauseTraitement? CauseDe(string sessionId, long episodeTraite)` | 1 | 1 |
| `_causes.Remove(id)` | ≥ 1 | 2 (NET-01 sans instant valide, NET-03) |
| `MotifMasquage.Repondue` dans le détecteur | 1 | 1 |
| Première occurrence de `EstAttente(SessionActivity` | la définition | ligne 73, la définition (Piège 8 respecté) |
| Fichiers touchés par les 4 commits | les 5 de `files_modified` | les 5, rien d'autre (ni `SessionMonitor.cs`, ni `LecteurAppBureau.cs`, ni `TreatedSessionsTests.cs`) |

## Deviations from Plan

### Écarts mineurs (conception des tests, aucun changement de règle)

**1. Trois tests rougissent en plus au RED (L06, L08, L23)**
- **Constat :** le plan les attendait verts par construction.
- **Raison :** chacun vérifie l'état intermédiaire (inscription, ou cause présente) avant d'en constater la purge.
  Sans cela, un détecteur qui ne fait rien les ferait passer.
- **Effet :** ces tests sont plus exigeants ; les mêmes exigences finales sont assertées.

**2. L06 et L23 font leur premier cycle à 14:01Z au lieu de 14:08Z**
- **Raison :** le plan enchaîne « L01 (14:08Z) puis un cycle à 14:06Z », ce qui ferait reculer l'horloge entre deux
  cycles. À 14:01Z, l'attente (13:59:05Z) reste antérieure au focus (14:00:39Z) : la sémantique de L01 est inchangée.

**3. Ajouts de preuve**
- L19 vérifie aussi que la sentinelle de date d'écriture est intacte (cas 2 : zéro écriture).
- Le cas « absente » de L15 garde un dictionnaire NON vide (le focus d'une autre session), pour que « clé absente » ne
  se confonde pas avec « dictionnaire vide ».

**4. Commit RED de la tâche 1 reconstitué**
- Le GREEN de la tâche 1 a été écrit avant le commit RED. Pour garder l'historique RED puis GREEN, les squelettes ont
  été reposés pour le commit `7aad169` : le détecteur à `HEAD` avec la seule signature, et `GraceLecture = Zero`.
  Le GREEN a ensuite été restauré depuis sa copie, avec le même sha256 (`44264408…`, `0b5687d8…` pour
  `HorizonsSessions.cs`). Les tests ont été relancés après restauration : 120 / 120 sur le filtre de la tâche 1.

**Total :** aucune règle automatique (Rules 1-4) déclenchée ; 4 écarts mineurs, tous sur les tests ou l'historique.

## Issues Encountered

- **La compilation cassée par le RED en cours de 30-02.** Les premières exécutions des mutations (m1) et (m2) sont
  tombées pendant que `LecteurAppBureauTests` appelait `LectureAppBureau.Selection`, qui n'existait pas encore. Elles ont
  été rejouées dès que 30-02 a compilé. Les résultats consignés plus haut viennent de ces secondes exécutions. Le fichier
  muté a été restauré après chaque tentative, avec le sha256 vérifié.
- **Des fins de ligne mixtes.** `SessionTreatmentTracker.cs` est en CRLF dans l'arbre de travail, les autres fichiers
  touchés sont en LF. Les éditions ont conservé la convention de chaque fichier.
- **Des heredocs Bash qui cassaient sur le C#** (apostrophes et accents graves). Le fichier de tests et les scripts
  d'édition ont été écrits avec l'outil d'écriture, puis appliqués par Python depuis le bloc-notes de session.

## Known Stubs

Aucun. Deux points sont volontairement inertes jusqu'aux plans suivants :

- `SessionMasquee.Cause` est déclarée, mais aucun code de production ne l'alimente encore. Le moniteur la posera en
  30-03 (`_tracker?.CauseDe(s.SessionId, tts)`).
- `DiagnosticService.LibelleMotif` n'a pas encore de cas pour `LueParFocus`, `LueAuPremierPlan` et `Repondue`, qui
  tomberaient sur le libellé par défaut « un filtre non nommé ». Aucun code de production ne produit ces motifs avant
  30-03 ; 30-04 écrit leurs libellés (R07 : aucun motif sans libellé).

## Exigences

LUE-01 à LUE-04 restent **Pending**, comme le prévoit le plan : la règle, le prédicat de grâce, la cause et
l'inactivité sans contexte existent et sont prouvés dans le détecteur, mais le moniteur ne les branche qu'en 30-03, et
le rapport ne les dit qu'en 30-04. Aucune exigence n'est cochée dans REQUIREMENTS.md.

## Next Phase Readiness

- **30-03 (moniteur et câblage).**
  - Construire `ContexteLecture` depuis `appBureau.ParSession` : le dictionnaire du dernier focus doit être en
    `StringComparer.OrdinalIgnoreCase`, et la sélection et le premier plan viennent de 30-02.
  - Appeler `_tracker?.Observe(arbitrage.Vainqueurs, now, contexte)`.
  - Poser `CauseDe` sur `SessionMasquee`.
  - Le contexte doit être NUL si la source app-bureau est absente (LUE-04, D1-D3).
  - Aucun seuil à déclarer dans le moniteur : `GraceLecture` est lu par le détecteur seul.
- **30-04 (rapport).** Les libellés des trois nouveaux motifs. `CauseTraitement.Constat` est figé au premier constat :
  « depuis N s » = `Constat − PremierPlanDepuis`, jamais `now − PremierPlanDepuis`.
- **Limite connue, écrite :** une demande de permission vue quitte le widget (D-30-08). Une déduite regardée après son
  dernier signal n'est jamais annoncée « En attente ? » (D-30-07). Les deux sont à constater en phase 31.

## Self-Check: PASSED

- Fichiers : les 5 de `files_modified` et ce SUMMARY existent.
- Commits : `7aad169`, `3b7ce99`, `26e93b8`, `f0ab69a` présents dans l'historique.
- Suite : 1110 / 0 combiné (deux exécutions), 1088 isolé.
