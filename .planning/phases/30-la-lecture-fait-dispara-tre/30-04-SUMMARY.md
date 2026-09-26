---
phase: 30-la-lecture-fait-dispara-tre
plan: 04
subsystem: widget-sessions
tags: [csharp, diagnostic, rapport, contrat-des-hooks, lue-03, lue-04, lue-05, obs-01, garde-croisee, mutation, xunit, tdd]

sha_entree_de_plan: 4c27afb
one_liner: "Aucun masquage sans cause. Le rapport « Diagnostic… » ne dit plus « treated.json (hystérésis OU geste) » : chaque ligne masquée nomme la cause que le détecteur a constatée, avec ses instants à la seconde (« lue : focus à 16:00:39 > attente à 15:59:05 », « sélectionnée au premier plan depuis 39 s (claude) — attente à 20:34:04 », « répondue : attente à …, puis travail observé sur la même source »), et ce qu'il n'a pas vu est dit tel quel (« marquée à la main, ou traitée avant le démarrage de l'overlay »). Une section « Règle « lue » » dit la session sélectionnée dans l'app et le premier plan, et quand la règle ne peut rien voir, elle le dit (« inconnue — … comportement v1.6 », « indisponible (…) — LUE-01 seule », « NON BRANCHÉ »). Tout est lu dans la lecture du widget : la sonde est appelée une fois par rapport (OBS-01, gardé au texte et en dynamique). Le §3 du contrat des hooks dit maintenant trois façons de quitter le widget, la règle « lue » et LUE-05, sous une garde croisée document ↔ code. Mutation (m8) jouée puis révoquée. Tests : 1132 → 1143 (+11), 0 échec, deux exécutions : fin de phase 30."

requires:
  - phase: 30-01
    provides: "CauseTraitement (Motif, Attente, Focus, PremierPlanDepuis, Constat) figée au constat ; motifs LueParFocus / LueAuPremierPlan / Repondue ; HorizonsSessions.GraceLecture"
  - phase: 30-02
    provides: "EtatPremierPlan / StatutPremierPlan, LectureAppBureau.Selection (SessionSelectionnee avec CliSessionId nul possible), FakePremierPlan (compteur d'appels)"
  - phase: 30-03
    provides: "SessionMasquee.Cause posée par le moniteur, LectureSessions.PremierPlan (ce que la sonde a vu à CE cycle), filtre LUE-05 sur EstUneAttente"
provides:
  - "DiagnosticService.LibelleMasquage(MotifMasquage, CauseTraitement?) interne : un libellé NOMMÉ par motif, avec la cause et ses heures locales à la seconde"
  - "Section « Règle « lue » (LUE-01, LUE-02) » du rapport : sélection de l'app et premier plan, lus dans la MÊME lecture que le widget"
  - "docs/hooks-contract.md §3 : trois façons de quitter le widget, la lecture (LUE-01 à LUE-04), LUE-05"
  - "Gardes : Le_diagnostic_dit_la_regle_lue_dans_la_meme_lecture_que_le_widget (texte) et Le_rapport_ne_relit_pas_la_sonde (dynamique) ; Le_paragraphe_3_dit_les_trois_facons_de_quitter_le_widget_et_la_regle_lue (document ↔ code)"
affects: [31-constat]

tech-stack:
  added: []
  patterns:
    - "Un libellé par valeur d'énumération, interne et parcouru par un test (Enum.GetValues, garde anti-muette sur le nombre de valeurs) : le repli « non nommé » ne peut plus masquer un oubli"
    - "Une heure attendue calculée dans le test par ToLocalTime(), jamais écrite en dur (Piège 10)"
    - "OBS-01 gardé deux fois : au texte (un seul Inspecter(, ni l'interface ni l'implémentation de la sonde) et en dynamique (compteur d'appels du faux)"
    - "Extrait de rapport capturé par injection TEMPORAIRE d'un Assert.Fail(report), révoquée par copie et sha256"

key-files:
  created: []
  modified:
    - src/Chronos/Services/DiagnosticService.cs
    - docs/hooks-contract.md
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs
    - tests/Chronos.Tests/ContratHooksDocumenteTests.cs
    - .planning/phases/30-la-lecture-fait-dispara-tre/30-VALIDATION.md

key-decisions:
  - "D-30-11 : heures du rapport LOCALES à la SECONDE (ToLocalTime().ToString(\"HH:mm:ss\")) ; les mots du verrou sont conservés (« lue : focus à … > attente à … », « sélectionnée au premier plan », « répondue »)"
  - "D-30-12 : le §3 du contrat des hooks est réécrit en phase 30 ; le document de la source (docs/desktop-app-sessions.md, VAL-02) reste en phase 31"
  - "Tout libellé issu de treated.json commence par « treated.json » (le fichier à ouvrir reste nommé) ; sans cause connue, le résidu dit « marquée à la main, ou traitée avant le démarrage de l'overlay », jamais « lue » ni « répondue »"
  - "Le rapport dit ce que le moniteur voit À L'INSTANT DU RAPPORT : demandé depuis le menu, le premier plan est Chronos ou l'éditeur, et la ligne le dit ; « depuis N s » des lignes masquées reste figé au constat"
  - "Le premier plan non branché s'écrit « NON BRANCHÉ » (masculin), jamais « NON BRANCHÉE » (réservé à la source app-bureau)"

requirements-completed: [LUE-03, LUE-04]
requirements-advanced: []

duration: 17min
completed: 2026-09-26
---

# Phase 30 Plan 04 : Aucun masquage sans cause — Summary

**Chaque disparition a maintenant une cause écrite. Le rapport « Diagnostic… » nomme, pour chaque session masquée, ce que
le détecteur a constaté, avec ses instants à la seconde ; ce qu'il n'a pas vu est dit tel quel. Une section « Règle « lue » »
dit ce que la règle voit à l'instant du rapport, et dit aussi quand elle ne peut rien voir. Tout est lu dans la lecture du
widget, sans second appel. Le §3 du contrat des hooks dit la règle qui tourne, et une garde le tient contre le code. Tests :
1132 → 1143 (+11), 0 échec, deux exécutions consécutives. C'est la cible de fin de phase, atteinte exactement.**

## Performance

- **Début :** 2026-09-26T09:32:30Z. **Fin :** 2026-09-26T09:49Z environ. Durée : environ 17 min.
- **SHA d'entrée :** `4c27afb`, à **1132 verts / 0 échec**, mesuré à l'entrée (10 s).
- **Tâches :** 2 sur 2, en 4 commits (un RED et un GREEN par tâche).
- **Fichiers :** exactement les 5 de `files_modified` (348 insertions, 9 suppressions), plus `30-VALIDATION.md`. Aucun
  test existant n'a été retouché : les trois classes de tests ne gagnent que des lignes (263 insertions, 0 suppression).

## Mesures (tests)

| Moment | Filtre | Suite complète |
|---|---|---|
| Entrée (`4c27afb`) | — | **1132 / 0** (10 s) |
| RED de la tâche 1 (`8342bcc`) | filtre de la tâche : **10 échecs sur 89** | — |
| GREEN de la tâche 1 (`aa3d722`) | 89 / 89 | **1142 / 0** (1132 + 10) |
| RED de la tâche 2 (`104cb12`) | `ContratHooksDocumenteTests` + `LibellesSessionsTests` : **1 échec sur 23** | — |
| GREEN de la tâche 2 (`5995fa5`) | 23 / 23 | **1143 / 0**, deux exécutions consécutives (10 s, 10 s) |

Le total attendu (1143) est atteint exactement : +9 (R01 à R09), +1 (garde OBS-01 au texte), +1 (garde du §3). Aucun test
n'a été supprimé, renommé ni adapté.

## Les rouges (TDD)

**Tâche 1.** Squelette compilable : `LibelleMotif` devient `internal static string LibelleMasquage(MotifMasquage motif,
CauseTraitement? cause)`, qui rend encore l'ancien libellé, et la ligne des masquées l'appelle avec `m.Cause`. La section
« Règle « lue » » est absente. **10 rouges, chacun pour la bonne raison :**

- R01 `La_session_lue_par_focus_dit_ses_deux_instants` : la ligne masquée de JARVIS existe déjà, mais elle dit « un filtre
  non nommé ». Le moniteur de 30-03 produit donc bien `LueParFocus` dès le premier cycle.
- R02 `La_session_lue_au_premier_plan_dit_depuis_combien_de_secondes` : même constat pour `LueAuPremierPlan`.
- R03 `Le_libelle_de_repondue_dit_l_attente_et_le_travail_observe` et R08 `Aucun_motif_de_masquage_n_est_sans_libelle` :
  « un filtre non nommé ».
- R04 `Un_masquage_sans_cause_connue_est_dit_marque_a_la_main_ou_avant_le_demarrage` : l'ancien libellé « hystérésis OU
  geste ».
- R05 `Premier_plan_indisponible_le_rapport_dit_LUE_01_seule`, R06
  `Le_dernier_focus_sans_cliSessionId_ne_selectionne_personne_au_rapport`, R07
  `Sans_source_app_bureau_la_regle_lue_est_dite_inactive` : la section est absente.
- R09 `Le_rapport_ne_relit_pas_la_sonde` : `Assert.Single(sonde.Appels)` passe déjà (le moniteur est câblé). Le test rougit
  sur son assertion anti-muette, « Premier plan : claude depuis » (voir l'écart n° 1).
- La garde `GardesPerimetreTests.Le_diagnostic_dit_la_regle_lue_dans_la_meme_lecture_que_le_widget` rougit sur son
  anti-muet : « Règle « lue » » est introuvable.

**Tâche 2.** **1 rouge :** `ContratHooksDocumenteTests.Le_paragraphe_3_dit_les_trois_facons_de_quitter_le_widget_et_la_regle_lue`,
sur « deux raisons seulement » (`Assert.DoesNotContain` sur le document entier).

## Mutation (m8) : jouée, constatée, révoquée

Le fichier a été copié dans le bloc-notes de session avant la mutation. Son sha256 de référence après le GREEN est
`77f2ff90267bf086633a7fe5035200e5dc4d68d3dd6631da8073c011c1f0b40a`.

| Mutation | Changement | Échecs constatés | sha256 après révocation |
|---|---|---|---|
| (m8) | `masquée par {LibelleMasquage(m.Motif, null)}` au lieu de `LibelleMasquage(m.Motif, m.Cause)` | les deux exigés, `La_session_lue_par_focus_dit_ses_deux_instants` et `La_session_lue_au_premier_plan_dit_depuis_combien_de_secondes`, plus la garde `Le_diagnostic_dit_la_regle_lue_dans_la_meme_lecture_que_le_widget` (son anti-muet exige l'appel avec `m.Cause`) : **3** | `77f2ff90…1c1f0b40a`, identique ; 66 / 66 sur les deux classes après restauration ; `git diff` vide avant le commit |

sha256 finaux : `DiagnosticService.cs` `77f2ff90267bf086633a7fe5035200e5dc4d68d3dd6631da8073c011c1f0b40a`,
`docs/hooks-contract.md` `37bf50aa52b2b4ec85c1203f81bbe1028cfe3519a74a717effbf6d4af3d2c51b`.

## Extrait du rapport rendu (machine de test, fuseau `Romance Standard Time`, UTC+2 le 2026-09-25)

Ces extraits ont été capturés en injectant temporairement un `Assert.Fail(report)` à la fin de R01 et de R02. L'injection
a été révoquée par copie, avec un sha256 identique (`d146b938…23f426` pour `DiagnosticServiceTests.cs`) et un arbre propre
ensuite. Les heures sont telles que la machine les imprime.

R01 (relevé de 16 h 08, `explorer` au premier plan) :

```
  Sessions MASQUÉES par un filtre : 1
    · c17a1b03 Session B — En attente (il y a 8 min) — masquée par treated.json — lue : focus à 16:00:39 > attente à 15:59:05 (réversible : revient sur une nouvelle demande)
  …
  Règle « lue » (LUE-01, LUE-02) :
    Session sélectionnée dans l'app : c17a1b03 (focus 16:00:39)
    Premier plan : explorer — LUE-02 inactive
```

R02 (geste B, `claude` au premier plan depuis 20:33:27, rapport à 20:34:06.828) :

```
  Sessions MASQUÉES par un filtre : 1
    · 11456cab Session A — En attente (à l'instant) — masquée par treated.json — sélectionnée au premier plan depuis 39 s (claude) — attente à 20:34:04 (réversible : revient sur une nouvelle demande)
  …
  Règle « lue » (LUE-01, LUE-02) :
    Session sélectionnée dans l'app : 11456cab (focus 20:30:44)
    Premier plan : claude depuis 39 s — LUE-02 active
```

## Task Commits

1. **Tâche 1, RED :** `8342bcc` test(30-04) : le rapport doit nommer la cause de chaque masquage et dire la règle lue, R01 à R09 et garde OBS-01, RED 10/89
2. **Tâche 1, GREEN :** `aa3d722` feat(30-04) : le rapport nomme la cause de chaque masquage et dit ce que la règle lue voit (LUE-03, LUE-04)
3. **Tâche 2, RED :** `104cb12` test(30-04) : le paragraphe 3 du contrat doit dire les trois façons de quitter le widget et la règle lue, RED 1
4. **Tâche 2, GREEN :** `5995fa5` docs(30-04) : le paragraphe 3 du contrat dit les trois façons de quitter le widget, la règle lue et LUE-05 (LUE-03)

## Accomplishments

- **La ligne masquée dit sa cause (LUE-03).** `— masquée par {LibelleMasquage(m.Motif, m.Cause)}`. Les motifs se lisent ainsi :
  - `LueParFocus` : « treated.json — lue : focus à HH:mm:ss > attente à HH:mm:ss ».
  - `LueAuPremierPlan` : « treated.json — sélectionnée au premier plan depuis N s (claude) — attente à HH:mm:ss ». N vaut
    `Constat − PremierPlanDepuis` et reste figé au constat.
  - `Repondue` : « treated.json — répondue : attente à HH:mm:ss, puis travail observé sur la même source ».
  - `Traitee`, le résidu honnête : « treated.json (« traité » — marquée à la main, ou traitée avant le démarrage de
    l'overlay ; réversible) ».
  - Chaque cause a aussi un libellé sans instants, pour le cas où la cause manque.
  - `Archivee` et `Indeterminee` sont inchangés.
  - Le repli « un filtre non nommé » ne sert plus à aucune valeur, et R08 le garantit pour les six.
- **La règle « lue » dite telle qu'elle voit (LUE-04).** La section `DecrireRegleLue` est une méthode d'instance :
  l'ancienneté du premier plan se mesure à l'horloge injectée.
  - La sélection se lit sur `lecture.AppBureau.Selection`. Le rapport écrit l'identifiant court et l'heure du focus,
    « aucune — le dernier focus (…) est une session sans cliSessionId », « aucune — aucun dernier focus lisible », ou
    « inconnue — source app-bureau absente : règle « lue » inactive (comportement v1.6) ». Dans ce dernier cas, aucun
    identifiant n'est écrit.
  - Le premier plan se lit sur `lecture.PremierPlan`. Le rapport écrit « claude depuis N s — LUE-02 active » (ou
    « inactive » sans source), « <processus> — LUE-02 inactive », « aucune fenêtre au premier plan », « indisponible
    (<raison>) — LUE-02 inactive, LUE-01 seule », ou « NON BRANCHÉ — LUE-02 inactive, LUE-01 seule ».
  - Aucune ligne de la section ne commence par « · ».
- **OBS-01 tenu.** `DiagnosticService.cs` ne contient qu'un seul `Inspecter(` et aucune occurrence de `IPremierPlan`,
  `PremierPlanWin32`, `.Lire(`, `new LecteurAppBureau`, `RacinesEtat`, `Claude_`, `claude-code-sessions`,
  `HorizonsSessions.Silence` ni `FromUnixTimeMilliseconds`. R09 prouve l'appel unique à la sonde.
- **Le §3 du contrat dit vrai (D-30-12).** Il annonce trois façons de quitter le widget : un geste, une réponse sur la même
  source, ou une lecture. Il ajoute un paragraphe sur la lecture (LUE-01 à LUE-04, `HorizonsSessions.GraceLecture`, jamais
  d'UI Automation ni de titre) et un paragraphe LUE-05. La garde croisée vérifie `GraceLecture = 2,5 s`, le filtre
  `AffichageSessions.EstUneAttente(s.Activity)` du moniteur et la lecture de `GraceLecture` par le détecteur. Les phrases
  gardées par les tests existants restent présentes, et les fins de ligne CRLF sont conservées (501 CR pour 501 LF).
- **Les tests existants passent sans retouche.** Sont concernés `Le_cas_e465420e_se_lit_en_une_ligne_du_rapport`
  (« masquée par treated.json »), `Une_session_archivee_est_annoncee_masquee_par_le_magasin_d_archives`,
  `Une_session_indeterminee_est_annoncee_masquee_avec_son_motif`,
  `Le_rapport_ne_tronque_plus_la_liste_des_sessions_affichees`, les tests 29-05 (`LignesAppBureau`, `DoesNotContain("NON
  BRANCHÉE")`) et `LibellesSessionsTests`.

## Critères grep

| Critère | Attendu | Mesuré |
|---|---|---|
| `internal static string LibelleMasquage(MotifMasquage motif, CauseTraitement? cause)` | 1 | 1 |
| `LibelleMasquage(m.Motif, m.Cause)` | 1 | 1 |
| `LibelleMotif` | 0 | 0 |
| `Inspecter(` dans `DiagnosticService.cs` | 1 | 1 |
| `IPremierPlan\|PremierPlanWin32\|\.Lire\(\|new LecteurAppBureau\|HorizonsSessions\.Silence\|Claude_` | 0 | 0 |
| `hystérésis automatique OU geste explicite` | 0 | 0 |
| `FromUnixTimeMilliseconds` / `RacinesEtat` / `claude-code-sessions` dans `DiagnosticService.cs` | 0 | 0 / 0 / 0 |
| `deux raisons seulement` dans le contrat | 0 | 0 |
| `quitte le widget de trois façons` | 1 | 1 |
| `HorizonsSessions.GraceLecture` dans le contrat | 1 | 1 |
| `ne s'applique qu'à une session en attente` | 1 | 1 |
| `transition observée sur la MÊME source` | ≥ 1 | 1 |
| `git diff --stat 4c27afb..HEAD -- src/Chronos/ViewModels src/Chronos/Views src/Chronos/Resources` | vide | vide |

## Deviations from Plan

### Écarts mineurs (conception des tests, aucune règle changée)

**1. R09 rougit au RED (le plan l'attendait vert par construction)**
- **Raison :** R09 assert aussi, en plus de `Assert.Single(sonde.Appels)`, que le rapport contient « Premier plan : claude
  depuis ». Sans cette assertion, un rapport qui n'écrirait aucune ligne de premier plan appellerait lui aussi la sonde une
  seule fois, et le test passerait à vide.
- **Constat au RED :** l'appel unique tenait déjà (le moniteur est câblé), et le test ne rougissait que sur la ligne absente.

**2. (m8) rougit trois tests au lieu de deux**
- **Raison :** la garde n° 10 exige, par anti-muet, le texte `LibelleMasquage(m.Motif, m.Cause)`. La mutation le retire,
  donc la garde rougit aussi. C'est voulu : la garde tient l'appel avec la cause au texte, R01 et R02 le tiennent au
  comportement.

**3. Formes de test**
- La garde n° 10 compte `Inspecter(` avec `Assert.Single(Regex.Matches(…))`, ce qui évite un nouvel avertissement
  xUnit2013. La garde de 29-05 garde son `Assert.Equal(1, …Count)`.
- R07 ne teste pas l'absence de « NON BRANCHÉE » dans tout le rapport, qui dit légitimement « Source app-bureau : NON
  BRANCHÉE ». « Premier plan : NON BRANCHÉ — » suffit à exclure la forme féminine sur cette ligne.

**4. `30-VALIDATION.md` complété au-delà des lignes 30-04**
- Les cases laissées vides par 30-01 et 30-02 ont été remplies avec les valeurs mesurées de leurs SUMMARY, chacune marquée
  « repris du … SUMMARY ». Il s'agit des totaux après 30-01, après 30-02 et après la vague 1, des statuts 30-01-01 et
  30-01-02, des rouges de (m1), (m2) et (m4), et des cases de vague 0.
- La carte est ainsi complète pour `/gsd:verify-work`. Aucune valeur nouvelle n'y a été inventée.

**Total :** aucune règle automatique (Rules 1-4) déclenchée ; 4 écarts mineurs, sur les tests et la carte de validation.

## Issues Encountered

- **Un heredoc Bash trop long a échoué au parsing** (« unexpected EOF »). Aucun fichier du dépôt n'a été touché. Les
  fragments longs ont ensuite été écrits dans le bloc-notes de session, puis appliqués par un utilitaire Node de
  remplacement exact. Cet utilitaire exige une occurrence unique et conserve les CRLF et le BOM. Les conventions de fin de
  ligne sont intactes : CRLF pour `DiagnosticService.cs`, `DiagnosticServiceTests.cs`, `ContratHooksDocumenteTests.cs`
  et `hooks-contract.md` ; LF pour `GardesPerimetreTests.cs`.
- **Première tentative de capture de l'extrait :** une erreur de guillemets dans l'injection a produit un fichier non
  compilable. Rien n'a été commité, le fichier a été restauré depuis sa copie (sha256 `d146b938…` identique), et la capture
  a été rejouée avec un script écrit dans le bloc-notes.
- `RefreshOrchestratorTests` (instable sous charge, `deferred-items.md`) n'a pas échoué sur les exécutions de ce plan.

## Known Stubs

Aucun. Les trois libellés « instants non retenus » ne sont pas des bouche-trous. Ils couvrent une cause dont le motif est
connu mais qui n'a pas d'instants, ce que le moniteur ne produit pas aujourd'hui. Ils existent pour que R08 n'ait aucune
valeur sans libellé.

## Limites connues, écrites et renvoyées au constat de phase 31

- **Le rapport voit Chronos au premier plan.** Demandé depuis le menu, il écrit « Premier plan : <Chronos ou l'éditeur> —
  LUE-02 inactive ». C'est vrai à cet instant. Les causes déjà constatées restent lisibles sur les lignes masquées. Un
  rapport lu longtemps après peut donc écrire « claude depuis … » ailleurs que la ligne masquée : les deux instants sont
  différents, et chacun est vrai.
- Les pièges 1, 11 et 12 et la conséquence de D-30-09 : voir 30-03-SUMMARY, inchangés.

## Exigences

- **LUE-03** : critère 4. Au rapport : R01 à R04 et R08, (m8). Au contrat : la garde du §3. **Coché.**
- **LUE-04** : critère 5, prouvé au moniteur en 30-03. « Le diagnostic le dit » : R05, R06 et R07. **Coché.**
- **Fin de phase 30 :** LUE-01 à LUE-05 sont cochés. Les constats humains sont renvoyés à la phase 31 (`30-VALIDATION.md`,
  « Manual-Only Verifications »).

## Next Phase Readiness

- **31 (constat).**
  - Au constat, ouvrir « Diagnostic… » sur l'exe réel. Aucune ligne ne doit dire « un filtre non nommé ». Chaque masquée
    doit porter sa cause. La section « Règle « lue » » doit dire la sélection et le premier plan (Chronos ou l'éditeur, au
    moment du rapport).
  - Le document de la source (`docs/desktop-app-sessions.md`, VAL-02) reste à écrire en phase 31. Le §3 du contrat des
    hooks est déjà à jour.

## Self-Check: PASSED

- FOUND : `src/Chronos/Services/DiagnosticService.cs`, `docs/hooks-contract.md`,
  `tests/Chronos.Tests/DiagnosticServiceTests.cs`, `tests/Chronos.Tests/GardesPerimetreTests.cs`,
  `tests/Chronos.Tests/ContratHooksDocumenteTests.cs`, `.planning/phases/30-la-lecture-fait-dispara-tre/30-VALIDATION.md`
- FOUND : commits `8342bcc`, `aa3d722`, `104cb12`, `5995fa5`
- Suite : 1143 / 0, deux exécutions consécutives (10 s, 10 s) sur le code final.

---
*Phase : 30-la-lecture-fait-dispara-tre*
*Terminé : 2026-09-26*
