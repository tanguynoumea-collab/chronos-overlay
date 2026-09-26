---
phase: 30-la-lecture-fait-dispara-tre
plan: 03
subsystem: widget-sessions
tags: [csharp, moniteur, lecture, lue-01, lue-02, lue-04, lue-05, premier-plan, di, garde-de-source, mutation, sentinelle, xunit, tdd]

sha_entree_de_plan: 962e250
one_liner: "Le moniteur branche la lecture, et une session qui travaille ne disparaît plus. Le filtre « traitée » ne masque plus qu'une ATTENTE (LUE-05) : répondue, lue ou marquée traitée, une session qui se remet à travailler s'affiche « Réflexion », et la ligne masquée porte la cause que le détecteur a constatée. À chaque cycle, le moniteur lit le premier plan (une sonde qui lève vaut « indisponible », sans sonde « non branchée »), bâtit le ContexteLecture sur la lecture de l'app de CE cycle (aucun second Lire ; nul sans dossier de l'app) et le passe au détecteur ; la lecture porte l'état du premier plan vu (OBS-01). La production enregistre PremierPlanWin32 en singleton et le passe par l'argument nommé premierPlan:, sous garde de source. Au moniteur réel, avec le lecteur réel et les fixtures du relevé : le relevé de 16 h 08 ne se reproduit plus, les gestes A et B se comportent comme relevé, et sans métadonnées rien ne bouge par rapport à la v1.6. Mutations (m6), (m3 bis) et (m5) jouées puis révoquées. Tests : 1110 → 1132 (+22), 0 échec, deux exécutions ; 3 tests adaptés dont 1 renommé."

requires:
  - phase: 30-01
    provides: "SessionTreatmentTracker.Observe(vainqueurs, now, ContexteLecture?), CauseDe(id, épisode), ContexteLecture, CauseTraitement, motifs LueParFocus / LueAuPremierPlan / Repondue"
  - phase: 30-02
    provides: "IPremierPlan / PremierPlanWin32 / EtatPremierPlan (ClaudeDepuis), LectureAppBureau.Selection, FakePremierPlan"
provides:
  - "SessionMonitor : paramètre final premierPlan (nommé), ContexteLecture bâti sur la lecture de CE cycle, filtre LUE-05, cause posée sur la ligne masquée, internal PremierPlan (miroir DI)"
  - "LectureSessions.PremierPlan (EtatPremierPlan?, dernier paramètre) : ce que la sonde a vu à CE cycle, pour le rapport (OBS-01)"
  - "App.xaml.cs : IPremierPlan → PremierPlanWin32 en singleton, câblé au moniteur du widget par argument nommé"
  - "Garde de source Le_moniteur_de_production_recoit_le_premier_plan ; miroir DI étendu"
affects: [30-04-diagnostic, 31-constat]

tech-stack:
  added: []
  patterns:
    - "Le filtre d'affichage ne purge rien : il lit l'état retenu (EstUneAttente) et la cause du détecteur ; ajout et purge restent au seul détecteur"
    - "Une observation OS lue à chaque cycle, best-effort, et PORTÉE par la lecture : le rapport lit l'état vu, jamais la sonde"
    - "Comparaison champ par champ avec un moniteur témoin « v1.6 » (sans lecteur ni sonde, magasin distinct), cycle après cycle, pour prouver l'absence de faux masquage"
    - "Paramètre optionnel nul par défaut + garde de source sur le câblage de production (Piège 9)"

key-files:
  created:
    - tests/Chronos.Tests/MoniteurLectureTests.cs
    - .planning/phases/30-la-lecture-fait-dispara-tre/deferred-items.md
  modified:
    - src/Chronos/Services/SessionMonitor.cs
    - src/Chronos/Services/LectureSessions.cs
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/TreatedSessionsTests.cs
    - tests/Chronos.Tests/InspectionSessionsTests.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - .planning/phases/30-la-lecture-fait-dispara-tre/30-VALIDATION.md

key-decisions:
  - "D-30-09 (LUE-05) : le filtre « traité » ne masque qu'une session dont l'état RETENU est une attente ; une session traitée qui travaille est visible « Réflexion », son entrée reste dans treated.json jusqu'au prochain épisode (le filtre ne purge rien) ; traitée ET indéterminée ⇒ Indeterminee ; « Marquer traitée » sur une session qui travaille ne la fait plus disparaître"
  - "D-30-10 : le premier plan est lu à CHAQUE cycle, même sans source app-bureau ; une sonde qui lève vaut Indisponible (type de l'exception) ; le détecteur ne reçoit que ClaudeDepuis ; sans dossier de l'app, le contexte est NUL (détecteur v1.6 exact)"
  - "Pas de nouveau type de constat : la sélection est déjà sur LectureSessions.AppBureau.Selection, l'état de la sonde est porté par LectureSessions.PremierPlan"

requirements-completed: [LUE-01, LUE-02, LUE-05]
requirements-advanced: [LUE-04]

duration: 34min
completed: 2026-09-26
---

# Phase 30 Plan 03 : Le moniteur branche la lecture — Summary

**Une session lue quitte maintenant le widget d'elle-même en production, et une session qui travaille n'en disparaît
plus. Le moniteur lit le premier plan à chaque cycle, bâtit le `ContexteLecture` sur la lecture de l'app qu'il fait
déjà, et le passe au détecteur. Il pose la cause sur chaque ligne masquée et ne masque plus qu'une attente (LUE-05). La
production câble la sonde, et une garde de source signale tout oubli. Tests : 1110 → 1132 (+22), 0 échec, sur deux
exécutions consécutives.**

## Performance

- **Début :** 2026-09-26T08:54:44Z. **Fin :** 2026-09-26T09:28:28Z. Durée : environ 34 min.
- **SHA d'entrée :** `962e250`, à **1110 verts / 0 échec**, mesuré à l'entrée (9 s).
- **Tâches :** 3 sur 3, en 6 commits (un RED et un GREEN par tâche).
- **Fichiers :** 8 fichiers de code et de tests, exactement ceux de `files_modified`, dont un nouveau
  (`MoniteurLectureTests.cs`, 684 lignes). S'y ajoutent `30-VALIDATION.md` et `deferred-items.md`.

## Mesures (tests)

| Moment | Filtre | Suite complète |
|---|---|---|
| Entrée (`962e250`) | — | **1110 / 0** |
| RED de la tâche 1 (`744b8f5`) | filtre de la tâche : **6 échecs sur 141** | — |
| GREEN de la tâche 1 (`5db55b0`) | 141 / 141 | **1114 / 0** (1110 + 4) |
| RED de la tâche 2 (`10d83e8`) | `MoniteurLectureTests` : **10 échecs sur 21** | — |
| GREEN de la tâche 2 (`5e85684`) | 158 / 158 | **1131 / 0** (1114 + 17) |
| RED de la tâche 3 (`b22a979`) | — | **1 échec sur 1132** (la garde) |
| GREEN de la tâche 3 (`9ec7233`) | gardes nommées : 75 / 75 | **1132 / 0**, deux exécutions consécutives (11 s, 11 s) |

Le total attendu par le plan (1132) est atteint exactement : +4 (V01 à V04), +17 (M01 à M14, dont M06 compte trois
cas), +1 (la garde). Aucun test n'a été supprimé.

## Les rouges (TDD)

**Tâche 1.** Le RED a été joué contre le filtre v1.6 (`treatedMap.ContainsKey`). **6 rouges :**
`Une_session_traitee_qui_travaille_est_visible_Reflexion` (V01), `Son_prochain_tour_fini_la_ramene_En_attente` (V02),
`Traitee_et_indeterminee_est_masquee_indeterminee` (V03), puis les trois tests adaptés :
`NET01_la_reponse_est_inscrite_et_la_session_montre_son_travail`,
`NET03_le_monitor_la_reaffiche_sur_nouvel_episode` et `Le_cas_e465420e_devient_lisible_en_une_lecture`.
`Traitee_et_deduite_plus_ancienne_que_le_traitement_reste_masquee` (V04) est vert par construction, comme prévu.

**Tâche 2.** Le squelette accepte `premierPlan`, mais il ne lit pas la sonde, passe toujours `Observe(vainqueurs, now)`
et ne remplit pas `PremierPlan`. **10 rouges :** M01 `Le_releve_de_16_h_08_ne_se_reproduit_plus`,
M02 `Geste_A_le_retour_alt_tab_rend_la_session_lue`, M03 `Geste_B_la_session_regardee_est_lue_au_premier_plan`,
M07 `Premier_plan_indisponible_LUE_02_inactive_LUE_01_seule`, M09 `Marquer_traitee_et_archiver_restent_ceux_de_la_v1_6`
(voir écart n° 1), M10 `La_question_de_l_app_lue_est_masquee`, M11 `Lue_puis_repondue_puis_revenue_puis_relue`,
M12 `Treated_json_n_est_pas_reecrit_a_chaque_rafraichissement`,
M13 `La_lecture_porte_l_etat_du_premier_plan_vu_par_le_moniteur` et M14 `Une_sonde_qui_leve_ne_casse_pas_la_lecture`.
Sont verts par construction, comme prévu : M03b, M04, M05, M06 ×3 et M08.

**Tâche 3.** **1 rouge :** `GardesPerimetreTests.Le_moniteur_de_production_recoit_le_premier_plan`, tant
qu'`App.xaml.cs` ne câble pas la sonde. Le miroir DI adapté était déjà vert (voir écart n° 2).

## Tests adaptés : 3, dont 1 renommé (LUE-05)

Ces trois tests figeaient le comportement v1.6 que LUE-05 corrige : une session répondue restait masquée pendant
qu'elle travaillait. Chacun garde sa preuve d'origine et dit désormais « visible, en travail ».

| Test | Changement | Justification |
|---|---|---|
| `TreatedSessionsTests.NET01_le_monitor_masque_apres_reponse` → **renommé** `NET01_la_reponse_est_inscrite_et_la_session_montre_son_travail` | À t1, visible `Working` ET `store.Load()` contient `"s"` | L'ancien nom affirmait le contraire de LUE-05. La réponse est toujours inscrite (NET-01), mais la session n'est plus masquée pendant qu'elle travaille |
| `TreatedSessionsTests.NET03_le_monitor_la_reaffiche_sur_nouvel_episode` (nom inchangé) | À t1, visible `Working` au lieu de `DoesNotContain` ; la suite (t2 : réapparition et purge) est inchangée | Même raison ; NET-03 est toujours prouvé à t2 |
| `InspectionSessionsTests.Le_cas_e465420e_devient_lisible_en_une_lecture` (nom inchangé) | La session est seule visible, en `Working`, `Masquees` est vide, et `FichiersEcartesParAnciennete == 1` est inchangé | Commentaire : « Depuis LUE-05 (phase 30), une session qui travaille n'est jamais masquée par treated.json : le cas fondateur s'affiche « Réflexion » » |

Le miroir DI (`CompositionRootTests.Le_graphe_DI_resout_la_chaine_de_sessions`, nom inchangé) a été étendu en tâche 3
pour inclure la sonde. Ce n'est pas un changement d'assertion LUE-05.

## Mutations : jouées, constatées, révoquées

Chaque mutation a été appliquée sur le fichier après sauvegarde d'une copie dans le bloc-notes de session. Elle a été
révoquée par restauration de cette copie, puis le sha256 a été relu.

| Mutation | Changement | Échecs constatés | sha256 avant = après |
|---|---|---|---|
| (m6) | filtre sans `&& AffichageSessions.EstUneAttente(s.Activity)` | les trois exigés (`Une_session_traitee_qui_travaille_est_visible_Reflexion`, `NET01_la_reponse_est_inscrite_et_la_session_montre_son_travail`, `Le_cas_e465420e_devient_lisible_en_une_lecture`), plus `Son_prochain_tour_fini_la_ramene_En_attente`, `Traitee_et_indeterminee_est_masquee_indeterminee` et `NET03_le_monitor_la_reaffiche_sur_nouvel_episode` (6) | `SessionMonitor.cs` `07c7f5d5bb037b96358b6c4fbb2e5213df0ed13941f8d6fec32be307651db1ae` |
| (m3 bis) | `appBureau.ParSession.Values.OrderByDescending(m => m.DernierFocus).FirstOrDefault()?.CliSessionId` au lieu de `appBureau.Selection?.CliSessionId` | `Le_dernier_focus_sur_un_fichier_sans_cliSessionId_ne_selectionne_personne`, seul | `SessionMonitor.cs` `1386bdc8d36435e098ae106260db822d862c7e9b2a184794eab12cdb8b1bec5c` |
| (m5) | `premierPlan: sp.GetRequiredService<IPremierPlan>()` retiré d'`App.xaml.cs` (virgule ajustée) | `Le_moniteur_de_production_recoit_le_premier_plan`, seul | `App.xaml.cs` `6f887ff8a397b76bfecba8f14714319c477988422ee3fe8fa5750d5fd3710de8` |

Les sha256 finaux sont ceux de la dernière colonne pour `SessionMonitor.cs` (après la tâche 2, inchangé depuis) et
`App.xaml.cs`. Celui de `LectureSessions.cs` est `3996c8c79218cb3502e0565bfca82f906a3fa19bdd752d2e50c76cfb866bfd6f`.

## Task Commits

1. **Tâche 1, RED :** `744b8f5` test(30-03) : une session qui travaille reste visible, V01 à V04, trois tests adaptés (LUE-05), RED 6/141
2. **Tâche 1, GREEN :** `5db55b0` feat(30-03) : le filtre traité ne masque que les attentes, et dit la cause (LUE-05, LUE-03)
3. **Tâche 2, RED :** `10d83e8` test(30-03) : la lecture au moniteur réel, M01 à M14 (17 cas), RED 10/21 sur squelette
4. **Tâche 2, GREEN :** `5e85684` feat(30-03) : le moniteur lit le premier plan et passe la lecture au détecteur (LUE-01, LUE-02, LUE-04)
5. **Tâche 3, RED :** `b22a979` test(30-03) : la production doit câbler la sonde du premier plan, garde de source et miroir DI, RED 1/1132
6. **Tâche 3, GREEN :** `9ec7233` feat(30-03) : la production câble la sonde du premier plan (LUE-02, LUE-04)

## Accomplishments

- **LUE-05 (D-30-09).** `treatedMap.TryGetValue(s.SessionId, out var tts) && AffichageSessions.EstUneAttente(s.Activity)`.
  La cause vient de `_tracker?.CauseDe(s.SessionId, tts)`, avec le motif résiduel `Traitee` quand elle est nulle.
  L'ordre annoncé est : archivée, puis traitée-en-attente, puis indéterminée. Le filtre ne purge rien : seul le détecteur
  ajoute et retire.
- **La lecture branchée (D-30-10).** Le bloc 2.b suit le Pattern 3 de la recherche. `EtatPremierPlan.NonBranche` sert
  par défaut. `_premierPlan.Lire(now)` est appelé sous `try`, et une exception donne `Indisponible` avec
  `e.GetType().Name`. `ContexteLecture` est bâti sur `appBureau.ParSession` (dictionnaire `OrdinalIgnoreCase`), avec
  `appBureau.Selection?.CliSessionId` et `premierPlan.ClaudeDepuis`, ou reste NUL sans `DossierTrouve`. L'appel est
  `_tracker?.Observe(arbitrage.Vainqueurs, now, contexte)`. `treated.json` est relu APRÈS l'observation, donc une
  session lue est masquée dès ce cycle.
- **OBS-01.** `LectureSessions.PremierPlan` est l'état que la sonde a rendu à CE cycle, la même instance (M13 :
  `Assert.Same`, un seul appel). La sonde est lue même sans lecteur, et un moniteur sans sonde rend `NonBranche`.
- **La production.** `services.AddSingleton<IPremierPlan>(_ => new PremierPlanWin32());` est placé juste avant le
  moniteur, suivi de l'argument nommé FINAL `premierPlan: sp.GetRequiredService<IPremierPlan>()`. `DiagnosticService` n'a
  pas été touché (0 occurrence de `IPremierPlan` ou `PremierPlanWin32`).
- **Les critères de la ROADMAP au moniteur réel.**
  - Critère 1 : M01.
  - Critère 2 : M11, M12 (sentinelle intacte sur cinq cycles) et M09.
  - Critère 3 : M02, M03, M03b et M04.
  - Critère 5 : M05, M06 ×3, M07, M08 et M14.
  - Critère 6 : V01 à V04 et M11.

## Critères grep

| Critère | Attendu | Mesuré |
|---|---|---|
| `AffichageSessions.EstUneAttente(s.Activity)` dans `SessionMonitor.cs` | 1 | 1 |
| `_tracker?.CauseDe(s.SessionId, tts)` | 1 | 1 |
| `treatedMap.ContainsKey` | 0 | 0 |
| `_tracker?.Observe(arbitrage.Vainqueurs, now, contexte)` | 1 | 1 |
| `appBureau.Selection?.CliSessionId` | 1 | 1 |
| `premierPlan.ClaudeDepuis` | 1 | 1 |
| `_appBureau.Lire(now)` (aucun second appel au lecteur) | 1 | 1 |
| `TimeSpan.From` / `Dictionary<string, SessionSnapshot>` / `byId[` | 0 | 0 |
| `Claude_` / `claude-code-sessions` dans `SessionMonitor.cs` | 0 | 0 |
| `Activity = SessionActivity.WaitingDeduced` | 1 (pas de seconde) | 1 |
| `with { Titre` ; `Enrichir(` après `ArbitrageSessions.Trancher(` | 1 ; oui | 1 ; l. 216 après l. 188 |
| `EtatPremierPlan? PremierPlan = null` dans `LectureSessions.cs` | 1 | 1 |
| `premierPlan: sp.GetRequiredService<IPremierPlan>()` dans `App.xaml.cs` | 1 | 1 |
| `services.AddSingleton<IPremierPlan>(_ => new PremierPlanWin32());` | 1 | 1 |
| `IPremierPlan` / `PremierPlanWin32` dans `DiagnosticService.cs` | 0 | 0 |
| `git diff --stat 962e250..HEAD -- src/Chronos/ViewModels src/Chronos/Views src/Chronos/Resources` | vide | vide |

## Deviations from Plan

### Écarts mineurs (conception des tests, aucune règle changée)

**1. M09 rougit au RED de la tâche 2 (le plan l'attendait vert par construction)**
- **Raison :** M09 vérifie en plus que JARVIS, lue ET archivée, est bien INSCRITE dans le magasin
  (`treated.Load().ContainsKey(Jarvis)`). Sans cette assertion, « Archiver prime sur une lecture » serait vrai même si
  la lecture n'avait jamais eu lieu. L'échec constaté au RED vient de cette seule assertion (message
  « JARVIS est lue (inscrite) : c'est l'archivage qui prime à l'annonce »).

**2. Au RED de la tâche 3, seule la garde rougit ; le miroir DI est déjà vert**
- **Raison :** le plan disait « la garde et le miroir d'abord (rouges tant que le câblage manque) ». Mais le miroir
  RECOPIE le câblage dans le test, il ne lit pas `App.xaml.cs` : il passe dès que le paramètre `premierPlan` existe
  (tâche 2). La garde de source est le seul test qui peut rougir sur l'absence du câblage de production, et la mutation
  (m5) le confirme.

**3. Ajouts de preuve**
- M02, M03, M03b et M14 vérifient aussi l'état du magasin (M03 : `treated.Load()[B] == 1790361244328`).
- M06 vérifie que ses cycles ne passent pas à vide : la réponse du troisième cycle est inscrite (NET-01).
- M07 vérifie que B est bien la session sélectionnée, donc qu'elle aurait été lue si la sonde savait.
- M08 vérifie que la session est connue de l'app (`ParSession.ContainsKey`), donc que la règle avait de quoi s'appliquer.
- M13 vérifie aussi `Assert.Same(sonde, moniteur.PremierPlan)`.
- Le miroir DI a gagné un paragraphe « Phase 30 » dans son XML-doc.

**Total :** aucune règle automatique (Rules 1-4) déclenchée ; 3 écarts mineurs, tous sur les tests.

## Issues Encountered

- **Un test hors périmètre, instable sous charge.** Pendant un ralentissement de la machine, la suite complète a pris
  1,4 à 3,1 min au lieu de 11 s, et tous les tests ralentissaient, y compris les triviaux. Pendant ce ralentissement,
  `RefreshOrchestratorTests.PeriodicTimer_declenche_GetAsync_sans_evenement_watcher` (phase 04 : un minuteur de 50 ms
  attendu 2 s) a échoué une fois sur trois exécutions. Le même code a ensuite passé cinq fois de suite en 11 à 16 s.
  Ce test est sans lien avec ce plan ; il est consigné dans `deferred-items.md` et n'a pas été modifié.
- **Des commits en parallèle.** Un agent de la phase 31 a commité `d1105fa` et `271f3b9` (documents de la phase 31,
  `.gitignore`, `REQUIREMENTS.md` pour VAL-01) entre mes commits `b22a979` et `9ec7233`. Aucun fichier n'est commun ;
  seuls mes fichiers ont été indexés à chaque commit.
- **Le `sed` de Git Bash retire les CR.** La première tentative de (m5) a produit un fichier non compilable (CS1026) :
  la substitution attendait `\r$`, et seule la suppression de ligne avait porté. Le fichier a été restauré depuis sa
  copie (sha256 identique), puis la mutation a été rejouée avec un motif indifférent au CR. Le résultat consigné vient
  de cette seconde exécution, suivie d'une recompilation et d'une suite complète sur le fichier restauré. Les fichiers
  gardent leur convention (CRLF pour `SessionMonitor.cs`, `App.xaml.cs` et `CompositionRootTests.cs` ; LF pour les
  autres), vérifiée en comptant les octets CR et LF.

## Known Stubs

- **Le libellé des trois nouveaux motifs au rapport.** Depuis ce plan, le moniteur de production PRODUIT
  `LueParFocus` et `LueAuPremierPlan` (et `Repondue` quand la cause l'est). `DiagnosticService.LibelleMotif` n'a pas
  encore de cas pour eux et tombe sur « un filtre non nommé » (`DiagnosticService.cs`, l. 627). C'est volontaire et
  écrit au plan : c'est le périmètre de 30-04 (R01 à R03, R07 : aucun motif sans libellé), qui suit immédiatement.
  Aucun test du plan 30-03 n'exigeait ces libellés, et ils n'ont pas été ajoutés ici.

## Limites connues, écrites et renvoyées au constat de phase 31 (aucune parade inventée)

- **Piège 1.** L'accueil ou une conversation Chat de l'app au premier plan (processus `claude`, dernière session de
  code toujours sélectionnée) : LUE-02 peut déclarer lue une session que l'utilisateur ne voit pas.
- **Piège 11.** La latence d'écriture de `lastFocusedAt` retarde LUE-01. Elle peut aussi faire désigner la session
  précédente pendant un clic dans la barre latérale.
- **Piège 12.** La grâce se voit : une fin de tour sous les yeux reste « En attente » de 2,5 à ~5,5 s (cadence de 2 s
  plus cache de 1 s). C'est voulu.
- **D-30-09, conséquence écrite.** « Marquer traitée » (et « Tout marquer traité ») sur une session qui TRAVAILLE ne la
  fait plus disparaître : elle reste « Réflexion », et le geste ne vaut que pour l'attente qu'il date.

## Exigences

- **LUE-01** : critères 1 et 2 prouvés au moniteur réel. **Coché.**
- **LUE-02** : critère 3, geste B au moniteur, sonde câblée en production, gardes de la phase 21 inchangées. **Coché.**
- **LUE-05** : critère 6. **Coché.**
- **LUE-04** : prouvé au moniteur (critère 5). « Le diagnostic le dit » relève de 30-04 : **reste Pending**.

## Next Phase Readiness

- **30-04 (rapport).**
  - Libellés de `LueParFocus`, `LueAuPremierPlan` et `Repondue` à partir de `SessionMasquee.Cause`, désormais alimentée
    par le moniteur. « Depuis N s » vaut `Constat − PremierPlanDepuis`.
  - « Premier plan : … » à partir de `lecture.PremierPlan` (statut, processus, raison), jamais par un appel à la sonde.
  - « Session sélectionnée : … » à partir de `lecture.AppBureau.Selection`, y compris un `CliSessionId` nul.
  - Réécrire le §3 de `docs/hooks-contract.md` : trois façons de quitter le widget, plus LUE-05.
- **31 (constat).** Les limites écrites ci-dessus, et l'instabilité de `RefreshOrchestratorTests` (`deferred-items.md`)
  si l'on veut une suite qui résiste à la charge.

## Self-Check: PASSED

- FOUND : `tests/Chronos.Tests/MoniteurLectureTests.cs`, `src/Chronos/Services/SessionMonitor.cs`,
  `src/Chronos/Services/LectureSessions.cs`, `src/Chronos/App.xaml.cs`, `tests/Chronos.Tests/TreatedSessionsTests.cs`,
  `tests/Chronos.Tests/InspectionSessionsTests.cs`, `tests/Chronos.Tests/GardesPerimetreTests.cs`,
  `tests/Chronos.Tests/CompositionRootTests.cs`, `.planning/phases/30-la-lecture-fait-dispara-tre/deferred-items.md`
- FOUND : commits `744b8f5`, `5db55b0`, `10d83e8`, `5e85684`, `b22a979`, `9ec7233`
- Suite : 1132 / 0, deux exécutions consécutives (11 s, 11 s) sur le code final.

---
*Phase : 30-la-lecture-fait-dispara-tre*
*Terminé : 2026-09-26*
