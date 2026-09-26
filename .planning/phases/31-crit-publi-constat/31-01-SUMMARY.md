---
phase: 31-crit-publi-constat
plan: 01
subsystem: documentation
tags: [documentation, contrat, app-bureau, garde-croisee, extraction, val-02, readme, mutation, xunit, tdd]

sha_entree_de_plan: f5d68f0
one_liner: "La source app-bureau a son contrat, tenu par une machine. docs/desktop-app-sessions.md dit où Chronos lit (le paquet MSIX d'abord ; %APPDATA%\\Claude n'est pas une jonction), les quatorze champs lus, les trois catégories de fin de tour, ce que Chronos en produit, comment il lit sans gêner l'app, et surtout ce qui n'est PAS garanti, daté du 2026-09-25 (app 2.9939.2.0), avec la procédure de re-relevé. ContratAppBureauDocumenteTests extrait du TEXTE de LecteurAppBureau.cs les champs et les catégories, et exige l'égalité dans les deux sens avec les tables balisées ; chemin, 24 h et partage de lecture sont tenus contre le code. La règle « lue » n'est écrite qu'au §3 du contrat des hooks, et le nouveau document y renvoie. Le §3, data-sources.md et le README renvoient au nouveau document. Le README a enfin une section « Widget de sessions Claude Code », et la garde des libellés lit 11 fichiers. Mutations (d1) à (d4) jouées puis révoquées. Tests : 1143 → 1149 (+6), 0 échec, deux exécutions."

requires:
  - phase: 29-ce-que-l-app-bureau-sait-de-chaque-session
    provides: "LecteurAppBureau (14 champs, 3 catégories, FileShare.ReadWrite | FileShare.Delete), RacinesEtat (seul porteur du chemin), sonde APP-06 (relevé chiffré)"
  - phase: 30-la-lecture-fait-dispara-tre
    provides: "§3 de hooks-contract.md réécrit (règle « lue », D-30-12), sélection LUE-02, section « Règle « lue » » du diagnostic"
  - phase: 27-le-relev-avant-la-r-gle
    provides: "27-RELEVE.md : gestes A et B, réserve 16:23:51 → 19:51, erratum « pas une jonction »"
provides:
  - "docs/desktop-app-sessions.md : le contrat de la source app-bureau (VAL-02), tables balisées CHAMPS-LUS et CATEGORIES-LUES, §5 « CE QUI N'EST PAS GARANTI » daté, §7 procédure de re-relevé"
  - "ContratAppBureauDocumenteTests (D1 à D6) : garde croisée document ↔ texte du lecteur, par extraction"
  - "ContratHooksDocumenteTests.TableEntre et SectionDe en internal static (réutilisables)"
  - "Renvois : hooks-contract.md (introduction, §3, §9), data-sources.md §6, README (section « Widget de sessions Claude Code »)"
  - "LibellesSessionsTests : 11 fichiers surveillés (nouveau document et README ajoutés)"
affects: [31-02-release, 31-03-constat]

tech-stack:
  added: []
  patterns:
    - "Garde croisée par EXTRACTION du texte du code (regex sur les appels qui nomment un champ), égalité d'ensembles dans les deux sens avec une table balisée, écarts nommés, anti-muet qui rougit si la forme des appels change"
    - "Une règle écrite en un seul document ; les autres y renvoient, et une garde rougit si la copie apparaît (HorizonsSessions.GraceLecture absent du nouveau document)"

key-files:
  created:
    - docs/desktop-app-sessions.md
    - tests/Chronos.Tests/ContratAppBureauDocumenteTests.cs
    - .planning/phases/31-crit-publi-constat/deferred-items.md
  modified:
    - tests/Chronos.Tests/ContratHooksDocumenteTests.cs
    - tests/Chronos.Tests/LibellesSessionsTests.cs
    - docs/hooks-contract.md
    - docs/data-sources.md
    - README.md

key-decisions:
  - "D-31-01 : « jonction » est écrit NIÉ (« %APPDATA%\\Claude n'est pas une jonction » : virtualisation d'AppData du paquet MSIX), et D4 tient la phrase"
  - "D-31-02 : la règle « lue » reste écrite au seul §3 de hooks-contract.md ; le contrat de l'app y renvoie, D6 rougit s'il cite HorizonsSessions.GraceLecture"
  - "D-31-03 : garde par extraction du texte de LecteurAppBureau.cs (option C), ni liste recopiée ni seul comportement ; un changement de forme des appels rougit par l'anti-muet"
  - "Le code et les relevés font foi sur le texte attendu du plan : sept formulations corrigées (écarts 1 à 7), dont l'activation du widget, qui installe les hooks tout de suite et sans sauvegarde"

requirements-completed: [VAL-02]

duration: 17min
completed: 2026-09-26
---

# Phase 31 Plan 01 : Le contrat de la source app-bureau, tenu contre le texte du lecteur — Summary

**La source app-bureau a maintenant son contrat, et une machine le tient contre le code. `docs/desktop-app-sessions.md`
dit où Chronos lit : le paquet MSIX d'abord, et « `%APPDATA%\Claude` n'est pas une jonction ». Il donne les quatorze
champs lus, les trois catégories de fin de tour, ce que Chronos en produit et comment il lit sans gêner l'app. Surtout,
il dit ce qui n'est pas garanti, daté du 2026-09-25 (app 2.9939.2.0), avec la procédure de re-relevé. La garde
`ContratAppBureauDocumenteTests` extrait du texte de `LecteurAppBureau.cs` les champs et les catégories. Elle exige
l'égalité, dans les deux sens, avec les tables balisées du document ; le chemin, la fenêtre de 24 h et le partage de
lecture sont tenus contre le code. La règle « lue » reste écrite au seul §3 du contrat des hooks, et le nouveau document
y renvoie. Le §3, `data-sources.md` et le README renvoient au nouveau document. Le README a enfin une section sur le
widget. Tests : 1143 → 1149 (+6), 0 échec, deux exécutions consécutives.**

## Performance

- **Début :** 2026-09-26T10:32:30Z. **Fin :** 2026-09-26T10:49Z environ. **Durée :** environ 17 min.
- **SHA d'entrée :** `f5d68f0`, à **1143 verts / 0 échec**, mesuré à l'entrée (9 s).
- **Exécution :** en séquence, seul sur l'arbre (pas d'arbre de travail isolé ; 31-02 s'exécutera après, dans le même
  arbre). Les totaux ci-dessous sont donc ceux de l'arbre réel.
- **Tâches :** 2 sur 2, en TDD, soit 4 commits de tâche.
- **Fichiers :** les 7 de `files_modified` (579 insertions, 6 suppressions), plus `deferred-items.md`. `src/` n'est pas
  touché : `git diff --stat f5d68f0..HEAD -- src` est vide. `31-VALIDATION.md` n'est pas modifié (31-03 y reportera les
  valeurs de ce SUMMARY).

## Mesures (tests)

| Moment | Filtre de la tâche (7 classes) | Suite complète |
|---|---|---|
| Entrée (`f5d68f0`) | — | **1143 / 0** (9 s) |
| Tâche 1, RED (`40e580e`) | **5 échecs / 103** | — |
| Tâche 1, GREEN (`668a3e4`) | 103 / 103 | **1148 / 0** = 1143 + 5 |
| Tâche 2, RED (`3afb72b`) | **1 échec / 104** | — |
| Tâche 2, GREEN (`41cb79e`) | 104 / 104 | **1149 / 0**, deux exécutions consécutives (9 s, 9 s) |

Le total annoncé par le plan (1149 isolé) est atteint exactement : +5 (D1 à D5), +1 (D6). `LibellesSessionsTests` garde
son nombre de cas. Après 31-02, exécuté ensuite dans le même arbre, 1152 est attendu.

## Les rouges (TDD)

- **Tâche 1 : 5 rouges sur 103, exactement D1 à D5**, tous pour la même raison, l'absence du document (« VAL-02 exige
  docs/desktop-app-sessions.md : introuvable (…) ») :
  - `Le_chemin_du_document_est_injecte_et_le_fichier_existe` ;
  - `La_table_documentee_liste_EXACTEMENT_les_champs_lus_par_le_lecteur` ;
  - `Les_categories_documentees_sont_celles_que_le_lecteur_reconnait` ;
  - `Le_document_dit_ou_il_lit_et_le_resolveur_construit_ce_chemin` ;
  - `Le_document_porte_ce_qui_n_est_pas_garanti_avec_sa_date`.
- **Tâche 2 : 1 rouge sur 104**,
  `Les_documents_voisins_renvoient_au_contrat_de_l_app_et_la_regle_n_est_ecrite_qu_une_fois`, sur « Not found:
  "desktop-app-sessions.md" » dans le §3 du contrat des hooks. `LibellesSessionsTests` est **vert au RED** sur ses
  11 fichiers : le nouveau document et le README ne contiennent aucun ancien libellé, et aucun texte n'a dû être corrigé.

## Mutations : jouées, constatées, révoquées

Chaque fichier a été copié dans le bloc-notes de session avant la mutation, puis restauré par copie.

| Mutation | Fichier (sha256 avant = après) | Rouges constatés (filtre de 103 ou 104) |
|---|---|---|
| **(d1)** `"titleSource"` → `"titleOrigin"` | `LecteurAppBureau.cs` `6b862e90…0a2a11` | **4 / 103** : D2, dont le message nomme « LUS mais NON DOCUMENTÉS : titleOrigin » et « DOCUMENTÉS mais NON LUS : titleSource » ; et trois cas de `LecteurAppBureauTests` : `La_session_du_geste_B_donne_son_titre_son_dossier_et_son_dernier_focus`, `Sans_postTurnSummary_aucune_classification_et_aucun_instant`, `Les_champs_absents_sont_comptes_sur_les_fichiers_lus` |
| **(d2)** ligne `isArchived` retirée de la table CHAMPS-LUS | `desktop-app-sessions.md` `faa6d03c…99ddff` | **1 / 103** : D2 seul, « LUS mais NON DOCUMENTÉS : isArchived », « DOCUMENTÉS mais NON LUS : (aucun) » |
| **(d3)** `"review_ready" =>` → `"ready" =>` | `LecteurAppBureau.cs` `6b862e90…0a2a11` | **2 / 103** : D3, qui nomme « ready » (lu, non documenté) et « review_ready » (documenté, non lu), et `LecteurAppBureauTests.Review_ready_est_une_fin_de_tour_prete_a_revue_sans_motif` |
| **(d4)** paragraphe « **La source elle-même** … » retiré du §3 | `hooks-contract.md` `76deb0e5…ec4cb1`, CR = LF = 508 avant et après | **1 / 104** : D6 seul, « Not found: "desktop-app-sessions.md" » |

sha256 complets :
- `LecteurAppBureau.cs` : `6b862e90be6025a3f7da1e0d67d4287863d46c37dc01e9439dc493e51e0a2a11` ;
- `docs/desktop-app-sessions.md` : `faa6d03c910fdf0ba4e5a3d289e4c4293bbac1957467079a8774035b7199ddff` ;
- `docs/hooks-contract.md` : `76deb0e586cd11ea66356cc8afb5939d4ee673b2761e5db96fbf7d2f1cec4cb1`.

Après chaque révocation, le sha256 est identique et `git diff -- src docs` est vide. Aucune mutation n'a été commitée.

## Task Commits

1. **Tâche 1 : le contrat de la source app-bureau, tenu contre le texte du lecteur.**
   - `40e580e` (test, RED 5/103)
   - `668a3e4` (docs, GREEN 103/103, suite 1148/0)
2. **Tâche 2 : les documents voisins renvoient au contrat de l'app, et le README dit le widget.**
   - `3afb72b` (test, RED 1/104)
   - `41cb79e` (docs, GREEN 104/104, suite 1149/0 deux fois)

**Plan metadata :** commit `docs(31-01)` final (SUMMARY, deferred-items, STATE, ROADMAP, REQUIREMENTS).

## Accomplishments

- **Le document (238 lignes, LF, UTF-8 sans BOM).** Sept sections, sur le modèle de `hooks-contract.md` :
  - §1, où Chronos lit, avec « n'est pas une jonction » ;
  - §2, les champs lus (table `CHAMPS-LUS`, 14 lignes) et les catégories (`CATEGORIES-LUES`, 3 lignes) ;
  - §3, ce que Chronos en produit ;
  - §4, la lecture sans gêner l'écrivain ;
  - §5, « CE QUI N'EST PAS GARANTI », en neuf sous-sections datées ;
  - §6, le relevé daté ;
  - §7, le re-relevé, avec ce qu'une machine garantit et ce qu'aucune ne garantit.

  Il ne contient ni chemin de profil (`C:\Users` : 0), ni nom d'utilisateur (0), ni titre, ni motif.
- **La garde par extraction.** `ChampsLusParLeLecteur()` lit `Texte|Instant|Entier|Booleen(racine|resume, "…")` et
  `TryGetProperty("…")` dans le texte du lecteur, et préfixe `postTurnSummary.` quand l'objet lu est le résumé. Elle
  trouve exactement les 14 noms. Son anti-muet exige `cliSessionId`, `lastFocusedAt` et au moins 12 noms.
  `CategoriesReconnues()` lit les bras du `switch` et trouve `completed`, `blocked` et `review_ready`. Les écarts sont
  nommés dans les deux sens, et les comptes sont égaux, donc aucun doublon n'est possible.
- **D4 tient le document contre trois sièges du code :**
  - le chemin construit par `RacinesEtat.cs` (`"LocalCache", "Roaming", "Claude", "claude-code-sessions"`) ;
  - `FileShare.ReadWrite | FileShare.Delete` dans le lecteur ;
  - `HorizonsSessions.LectureAppBureau` = 24 h.
- **La règle « lue » écrite une fois.** Le nouveau document renvoie au §3 des hooks (« La règle « lue » n'est pas écrite
  ici »). Le §3 renvoie au nouveau document (« La règle « lue » n'est écrite qu'ici, ci-dessous »). D6 tient les deux
  côtés, et la mutation (d4) le prouve.
- **Les renvois.** `hooks-contract.md` gagne trois renvois : l'introduction (le pendant des deux autres documents), le §3
  et le §9 (la procédure de re-relevé propre à l'app). `data-sources.md` gagne un §6 « Les sources du widget de
  sessions » : trois sources, leurs contrats et les deux vues d'AppData. Le README gagne la section « Widget de sessions
  Claude Code », et « 215 tests » y devient « plus de 1 100 tests ».
- **Les gardes existantes sont intactes :**
  - `ContratHooksDocumenteTests` : 15 cas verts ; seuls `TableEntre` et `SectionDe` passent en `internal static`, en
    2 lignes modifiées, CRLF 615/615 ;
  - `LibellesSessionsTests` : étendue de 9 à 11 fichiers, sans rien en retirer ; `fichiers[0]` reste le contrat des
    hooks ;
  - `GardesPerimetreTests`, `LecteurAppBureauTests`, `LectureSeuleAppBureauTests` et `HorizonsSessionsTests` : verts,
    sans retouche.

## Critères grep

| Critère | Attendu | Mesuré |
|---|---|---|
| `wc -l docs/desktop-app-sessions.md` | ≥ 100 | **238** |
| `<!-- CHAMPS-LUS:debut -->` / `<!-- CATEGORIES-LUES:debut -->` | 1 / 1 | **1 / 1** |
| `n'est pas une jonction` | ≥ 1 | **1** |
| `^## 5. CE QUI N'EST PAS GARANTI` | 1 | **1** |
| `HorizonsSessions.GraceLecture` dans le nouveau document | 0 | **0** |
| `grep -ci tanguy` (nouveau document, README, data-sources) | 0 | **0 / 0 / 0** |
| `grep -ciF 'C:\Users'` (nouveau document) | 0 | **0** |
| CR dans le nouveau document | 0 | **0** |
| `internal static IReadOnlyList<string[]> TableEntre` / `internal static string SectionDe` | 1 / 1 | **1 / 1** |
| `ContratHooksDocumenteTests.cs` : CR = LF ; diff de la tâche 1 | égaux ; 2 lignes | **615 = 615 ; 2 lignes** |
| `desktop-app-sessions.md` dans `hooks-contract.md` | 3 | **3** (introduction, §3, §9) |
| `^## 6. Les sources du widget de sessions` (data-sources) | 1 | **1** |
| `^## Widget de sessions Claude Code` / `desktop-app-sessions.md` (README) | 1 / ≥ 1 | **1 / 1** |
| `215 tests` (README) | 0 | **0** |
| `Assert.Equal(11, fichiers.Count)` | 1 | **1** |
| `transition observée sur la MÊME source` / `quitte le widget de trois façons` | ≥ 1 / 1 | **1 / 1** |
| Fins de ligne : `hooks-contract.md` CR = LF ; `data-sources.md`, `README.md` ; `LibellesSessionsTests.cs` | CR = LF ; 0 CR ; CR = LF | **508 = 508 ; 0 et 0 ; 285 = 285** |
| `git diff --stat f5d68f0..HEAD -- src` | vide | **vide** |

## Deviations from Plan

### Le code et les relevés font foi sur le texte attendu (le plan le prévoit : « l'écart est noté au SUMMARY »)

**1. §1 : la méthode `PremiereExistante` n'est pas citée.**
- **Constat :** le lecteur n'appelle pas `RacinesEtat.PremiereExistante`. Il teste l'existence de ses candidats par sa
  propre méthode privée `PremiereRacine()`, qui suit la même logique.
- **Texte écrit :** « à chaque cycle, le lecteur teste leur existence dans l'ordre et lit la première racine qui existe,
  et elle seule ». Le commentaire de `RacinesEtat.cs` qui cite `PremiereExistante` est consigné dans
  `deferred-items.md`, puisque `src/` est hors périmètre.

**2. §4 : les trois gardes de lecture seule sont toutes dans `LectureSeuleAppBureauTests`.**
- **Constat :** les trois gardes sont `Le_lecteur_ne_contient_aucun_appel_d_ecriture…`, le comportement sur une racine
  temporaire, et `Le_chemin_de_l_app_bureau_n_existe_que_dans_le_resolveur_de_racines` avec
  `Le_resolveur_de_racines_n_ecrit_rien`. `GardesPerimetreTests`, que citait le texte attendu, garde le câblage du
  lecteur, pas sa lecture seule.
- **Texte écrit :** le document ne cite que `LectureSeuleAppBureauTests`.

**3. §4 : au-delà de 16 Mo, le fichier « n'est pas copié et compte comme illisible ».**
- **Constat :** `LireOctets` rend nul, ce qui donne `IssueLecture.Illisible`. Le texte attendu disait « ignoré ».

**4. §6 : les fixtures sont « réduites à seize champs au plus et anonymisées ».**
- **Constat :** le texte attendu disait « réduits aux champs lus ». Or les fixtures conservent aussi `sessionId`,
  `originCwd`, `lastAssistantUuid` et `model`, et comptent de 9 à 16 champs selon le fichier (mesuré).

**5. §5.8 : les limites du premier plan sont « à constater », pas « constatées ».**
- **Constat :** le texte attendu disait « constatées en production (phase 31) ». Au moment de l'écriture, 31-03 n'a
  rien constaté, et l'écrire aurait présenté une attente comme un fait.
- **Texte écrit :** « Limites écrites dès la phase 30, jamais « corrigées » sans décision ; le constat en production de
  la phase 31 dira si elles se voient. »

**6. §2 et §3 : trois précisions tirées du code et du relevé.**
- **La ligne `lastFocusedAt`** dit « le dernier focus de la session dans l'app (ce qui le met à jour : §5.5) », au lieu
  de « dernier instant où la session a été sélectionnée ». Le geste A montre que le retour alt-tab le met aussi à jour.
- **La phrase qui suit la table** précise « faux pour `isArchived` » (`Booleen` rend faux) et « avec sa clé
  `postTurnSummaryFor` », lue sans liste d'absents.
- **Le diagnostic est rattaché à (APP-04, LUE-03, LUE-04)**, et plus seulement à (APP-04, LUE-03) : 30-04 a clos
  LUE-04 par la section « Règle « lue » ». La phrase ajoute « ou pourquoi la règle ne voit rien ».

**7. README : l'activation installe les hooks tout de suite, sans sauvegarde.**
- **Constat :** le texte attendu disait « Au lancement suivant de Chronos, 8 hooks sont inscrits …, après une
  sauvegarde ». Or `SessionsController.Enable` appelle `SessionHookInstaller.Install` immédiatement, par une écriture
  atomique sans sauvegarde. Seule la réconciliation au lancement (`ClaudeSettingsReconciler`) sauvegarde, avant toute
  réécriture.
- **Texte écrit :** « Chronos inscrit alors 8 hooks dans `~/.claude/settings.json`, sans toucher à ceux des autres
  outils, et les tient à jour à chaque lancement (le fichier est sauvegardé avant toute réécriture) ».

### Forme

**8. `data-sources.md` : le §6 est précédé d'un `---`,** comme toutes les sections du document, et suivi du `---` final
existant. Le contenu du §6 est celui du plan, mot pour mot.

**9. `hooks-contract.md` : le paragraphe d'introduction est reformaté sur quatre lignes** au lieu de deux, pour garder
la largeur du document. Le texte est celui du plan.

### Méthode

**10. Heredoc Bash.** Le premier heredoc (la classe de tests, environ 230 lignes) a échoué au parsing (« unexpected EOF
while looking for matching `'` »), comme en 29-03, 29-05 et 30-04. Le dépôt n'a pas été touché. Les fichiers et les
blocs ont ensuite été écrits dans le bloc-notes de session avec l'outil d'écriture, puis posés par Python, en
conservant les fins de ligne : CRLF pour les classes de tests et le contrat des hooks, LF pour le reste. Une sortie de
test a été écrite une fois dans `/tmp`, puis supprimée aussitôt ; toutes les suivantes sont allées dans le bloc-notes.

**Total :** aucune règle automatique (Rules 1 à 4) déclenchée. 7 formulations corrigées sur le code ou le relevé, 2
écarts de forme, 1 écart de méthode. Aucune garde n'a été assouplie, aucune assertion existante touchée.

## Observation hors périmètre (demandée par le plan)

La liste du menu dans le README (« Clic droit dessus pour le menu : Arrière-plan, Recalibrer…, Calibrer les plafonds…,
Lancer au démarrage, Usage exact (OAuth), Quitter ») est périmée depuis la fenêtre de réglages : le bouton s'appelle
« Quitter Chronos », et le paragraphe du token renvoie encore au « menu « Usage exact (OAuth) » ». Elle n'a pas été
touchée et figure dans `deferred-items.md`.

## Known Stubs

Aucun. `TODO|FIXME|placeholder|coming soon` ne donne rien dans les fichiers créés ou modifiés, hors une occurrence
préexistante de `data-sources.md` §5 (« placeholders `<slug>` » : une consigne d'anonymisation, pas un bouche-trou).

## Exigences

- **VAL-02 : livré.** Critère 1 de la phase : le document existe, avec le chemin, la virtualisation (« n'est pas une
  jonction »), les champs lus, la date du relevé et ce qui n'est pas garanti. La garde croisée rougit si les champs
  cités cessent d'être ceux que le code lit ((d1), (d2)) ou si les catégories dérivent ((d3)). **Coché.**

## Next Phase Readiness

- **31-02 (release) :** l'arbre est propre en dehors des fichiers de planification, et `src/` n'est pas touché. La suite
  est à **1149 / 0**, et 31-02 y ajoute 3 cas, pour 1152 attendus.
- **31-03 (constat) :**
  - reporter dans `31-VALIDATION.md` les valeurs de ce SUMMARY : RED 5 et 1 ; (d1) 4 rouges, (d2) 1, (d3) 2, (d4) 1 ;
    1148 après la tâche 1, 1149 deux fois après la tâche 2 ;
  - le §5.8 du document attend le constat de l'accueil et de la CLI au premier plan ;
  - le §5.6 attend la réserve 16:23:51 → 19:51 si elle se reproduit.

## Self-Check: PASSED

- FOUND : `docs/desktop-app-sessions.md`, `tests/Chronos.Tests/ContratAppBureauDocumenteTests.cs`,
  `.planning/phases/31-crit-publi-constat/deferred-items.md`, ce SUMMARY
- FOUND : commits `40e580e`, `668a3e4`, `3afb72b`, `41cb79e` (et la référence d'entrée `f5d68f0`)
- Suite : 1149 / 0, deux exécutions consécutives (9 s, 9 s), sur le code final.

---
*Phase : 31-crit-publi-constat*
*Terminé : 2026-09-26*
