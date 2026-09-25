---
phase: 29-ce-que-l-app-bureau-sait-de-chaque-session
plan: 02
subsystem: widget-sessions
tags: [csharp, app-bureau, lecture-seule, json-tolerant, cache, fixtures-reelles, gardes, mutation, xunit, tdd]

sha_entree_de_plan: 10a821f
one_liner: "Un lecteur PUR des métadonnées par session de l'app bureau (LecteurAppBureau) : il lit sur les six fixtures RÉELLES le titre, le dossier, le dernier focus et la classification de fin de tour en UTC exact, se tait sans lever sur ce qu'il ne sait pas lire, n'ouvre que les local_*.json à profondeur 2 écrits depuis moins de 24 h, ne relit que ce qui a changé, ne fait jamais clignoter un titre, date une question par son épisode — et prouve sa lecture seule par deux gardes falsifiées par trois mutations ; 947 → 982 isolé, 1000 avec 29-01, 0 échec"

requires:
  - phase: 27
    provides: "Les six fixtures RÉELLES (TestData/DesktopAppSessions) et le relevé des champs"
  - phase: 28-04
    provides: "HorizonsSessions, le type unique des horizons (Abandon 8 h), et ses gardes de chaîne et de câblage"
provides:
  - "LecteurAppBureau (Lire, Interpreter, Ouvrir) : la source app-bureau, en lecture seule, racines injectées, non câblée (29-03)"
  - "ClassificationFinDeTour { Inconnue, Terminee, Bloquee, PreteARevue }, IssueLecture, MetadonneesAppBureau, LectureAppBureau : le contrat neutre"
  - "HorizonsSessions.LectureAppBureau = 24 h (fenêtre de LECTURE, ≥ Abandon), puce ajoutée à la chaîne"
  - "RacineAppBureau : helper de tests partagé (fixtures réelles, racine temporaire <org>/<user>, dérivation TEXTUELLE, bruit du dossier) — pour 29-03 et 29-05"
  - "Gardes de lecture seule n° 1 (texte) et n° 3 (comportement) d'APP-05 ; la n° 2 (chemin en un seul fichier) est 29-05"
affects: [29-03-le-moniteur-joint-l-app-bureau, 29-05, 30-la-lecture-fait-disparaitre]

tech-stack:
  added: []
  patterns:
    - "Lecture concurrente d'un fichier réécrit par un tiers : partage lecture-écriture-suppression, poignée tenue le temps de COPIER, analyse après fermeture"
    - "Cache (date d'écriture, taille) dont la clé n'avance PAS sur une relecture ratée : dernière valeur valide servie, fichier retenté au cycle suivant"
    - "Instant d'un indice transitoire figé par épisode (clé fournie par la source), mémoire purgée des épisodes disparus"
    - "Fixture dérivée par remplacement TEXTUEL asserté (jamais une re-sérialisation)"

key-files:
  created:
    - src/Chronos/Services/LecteurAppBureau.cs
    - tests/Chronos.Tests/RacineAppBureau.cs
    - tests/Chronos.Tests/LecteurAppBureauTests.cs
    - tests/Chronos.Tests/LectureSeuleAppBureauTests.cs
  modified:
    - src/Chronos/Services/HorizonsSessions.cs

key-decisions:
  - "Instant d'une classification = lastActivityAt lu à la PREMIÈRE apparition de (cliSessionId, postTurnSummaryFor), mémorisé dans le lecteur (TRT-02) ; limite assumée : un redémarrage de l'overlay pendant une activité de fond re-mémorise une fois une valeur plus récente"
  - "lastAssistantUuid N'EST PAS lu (écart à la recherche, qui proposait une condition « résumé à jour ») : la liste des champs est verrouillée et l'app efface elle-même le résumé au tour suivant"
  - "Epoch hors plancher de sanité = champ absent (compté) ; texte vide = nul mais PAS compté absent (présent, du bon type)"
  - "HorizonsSessions.LectureAppBureau = 24 h : économie de lecture, jamais un horizon d'affichage ; Abandon ≤ LectureAppBureau asserté avant la valeur"

requirements-completed: []
requirements-advanced: [APP-01, APP-05]

duration: 13min
completed: 2026-09-25
---

# Phase 29 Plan 02 : Le lecteur de l'app bureau — Summary

**Une source app-bureau qui lit ce qu'il faut, se tait quand il faut, et ne gêne jamais l'app : `LecteurAppBureau`
interprète les six fixtures réelles champ par champ en UTC exact, n'ouvre que les `local_*.json` à profondeur 2
écrits depuis moins de 24 h, ne relit que ce qui a changé, garde la dernière lecture valide pendant une réécriture,
fige l'instant d'une question par épisode — et sa lecture seule tient sous deux gardes que trois mutations font
rougir.** Le lecteur n'est câblé nulle part (29-03).

## Performance

- **Début :** 2026-09-25T21:04:03Z — **Fin :** 2026-09-25T21:16:46Z — **Durée :** ~13 min
- **SHA d'entrée :** `10a821f` (947 verts / 0 échec)
- **Tâches :** 3/3, en TDD (6 commits de tâche)
- **Fichiers :** 4 créés, 1 modifié

## Mesures (tests)

| Moment | Isolé (29-02 seul) | Suite complète mesurée |
|---|---|---|
| Entrée | — | 947 / 0 échec |
| Tâche 1 verte | `LecteurAppBureauTests` 20/20 | (29-01 en cours : ses 2 rouges volontaires, hors périmètre) |
| Tâche 2 verte | `LecteurAppBureauTests` 31/31 | — |
| Tâche 3 verte | 31 + 4 = **35/35** | **1000 / 0 échec, deux exécutions consécutives** (8 s, 7 s) |

Arithmétique : 947 + 35 (29-02) = **982 isolé** ; + 18 (29-01, commité dans la vague) = **1000 combiné**, conforme au plan.

## Accomplishments

- **Le contrat** (`LecteurAppBureau.cs`) : `ClassificationFinDeTour`, `IssueLecture`, `MetadonneesAppBureau`,
  `LectureAppBureau` (avec `DossierTrouve`), `LecteurAppBureau(IReadOnlyList<string> candidats)` — exactement la
  signature du plan ; aucun type WPF, aucun chemin de l'app.
- **`Interpreter` (pur)** : BOM ignoré ; `JsonDocument` tolérant (virgule finale, commentaires) ; racine non objet ⇒
  illisible ; `cliSessionId` absent ⇒ `SansCliSessionId` ; champs par `TryGetProperty` + `ValueKind` (mauvais type =
  absent, compté) ; epochs par `UsageNormalization.InstantDepuisEpochMillisecondes` ; catégories comparées
  ordinalement, toute autre que `completed`/`blocked`/`review_ready` ⇒ `Inconnue` + `CategorieBrute` ; aucune
  exception ne sort (JSON, UTF-8 invalide).
- **`Lire(now)`** sous `lock (_verrou)` : premier candidat existant ; énumération matérialisée dans le bloc protégé,
  profondeur exacte 2, filtre de nom ordinal ; fenêtre `HorizonsSessions.LectureAppBureau` ; cache (date d'écriture,
  taille) purgé des chemins disparus ; relecture illisible ⇒ dernière lecture valide gardée, clé inchangée ;
  doublons tranchés par la dernière activité ; épisodes figés ; `ChampsAbsents` agrégés, clés triées.
- **`Ouvrir`** : `FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete`, tampon désactivé ; poignée
  fermée avant l'analyse ; au-delà de 16 Mio ⇒ illisible sans copie.
- **`HorizonsSessions.LectureAppBureau`** = 24 h, avec sa raison et une puce dans la chaîne de tête.

## Les rouges (TDD)

- **Tâche 1 — RED** contre squelettes (`NotImplementedException`, horizon provisoire à zéro) : **20/20 rouges** —
  `La_session_du_geste_B_donne_son_titre_son_dossier_et_son_dernier_focus`,
  `Blocked_donne_la_question_son_motif_et_l_instant_de_l_episode`, `Completed_est_une_fin_de_tour_terminee_sans_motif`,
  `Review_ready_est_une_fin_de_tour_prete_a_revue_sans_motif`, `Sans_postTurnSummary_aucune_classification_et_aucun_instant`,
  `Sans_cliSessionId_le_fichier_est_reconnu_et_non_joignable`, `Les_six_fixtures_reelles_se_lisent_malgre_les_champs_inconnus` (×6),
  `Une_categorie_que_le_disque_n_a_jamais_montree_reste_inconnue` (×2), `Un_champ_de_mauvais_type_vaut_absent_et_se_compte`,
  `Un_contenu_illisible_est_illisible_jamais_une_exception` (×3), `Un_BOM_UTF8_n_empeche_pas_la_lecture`,
  `La_fenetre_de_lecture_couvre_l_horizon_d_abandon` (seul rouge d'assertion : `Abandon <= Zero` faux).
- **Tâche 2 — RED** (`Lire` non implémenté) : **11/11 rouges**, les 20 de la tâche 1 restant verts —
  `Seuls_les_local_json_a_profondeur_deux_sont_enumeres`, `Un_fichier_modifie_il_y_a_plus_de_24_h_n_est_pas_ouvert`,
  `Le_cache_ne_relit_que_ce_qui_a_change`, `Un_fichier_tronque_pendant_sa_reecriture_ne_fait_pas_clignoter_le_titre`,
  `Une_premiere_lecture_illisible_ne_donne_rien_et_se_compte`, `Un_fichier_sans_cliSessionId_est_compte_et_ignore`,
  `Deux_fichiers_pour_une_meme_session_gardent_le_plus_recent_et_se_comptent`, `La_racine_est_le_premier_candidat_qui_existe`,
  `Aucune_racine_rend_une_lecture_absente_sans_exception`, `L_instant_d_une_question_est_fige_par_episode`,
  `Les_champs_absents_sont_comptes_sur_les_fichiers_lus`.
- **Tâche 3 — le rouge est celui des MUTATIONS** : `Ouvrir` était déjà implémenté en tâche 2 (étape 5 du plan) ;
  les 4 gardes ont donc été écrites contre le code réel, puis falsifiées :

## Mutations (jouées, constatées, révoquées)

SHA-256 de `src/Chronos/Services/LecteurAppBureau.cs` avant : `f19cf4c328abaaac36d3bd9dea5d0fdca0d85ca91bd5df7dffa1aba39129b082`
(commité en `14c91d2`, sauvegardé dans le bloc-notes de session, restauré par copie après chaque mutation).

| Mutation | Rouges constatés (sur 35) | Révocation |
|---|---|---|
| **(a)** `File.WriteAllText(Path.Combine(racine, "x.json"), "{}");` au début de `Lire` (après la résolution de la racine) | **2** : `Le_lecteur_ne_contient_aucun_appel_d_ecriture_et_ouvre_en_partage_complet` (« 187 — File.Write ») ET `Trois_lectures_laissent_la_racine_identique_octet_pour_octet` | sha256 identique, `git diff` vide |
| **(b)** partage d'`Ouvrir` réduit à `FileShare.Read` | **3** : la garde au texte (« Not found: FileShare.ReadWrite \| FileShare.Delete »), `Un_ecrivain_en_place_ouvert_n_empeche_pas_la_lecture` (ParSession vide), `Notre_poignee_ne_bloque_ni_l_ecriture_en_place_ni_la_suppression` (IOException à l'ouverture de l'écrivain) | sha256 identique, `git diff` vide |
| **(b′)** (en plus du plan) partage `FileShare.ReadWrite` sans `Delete` | **2** : la garde au texte ET `Notre_poignee_ne_bloque_ni_l_ecriture_en_place_ni_la_suppression` — cette fois sur la SUPPRESSION (`Assert.Null` : IOException de `File.Delete`) : la moitié « suppression » du test mord aussi seule | sha256 identique, `git diff` vide |

## Task Commits

1. **Tâche 1 — interprétation pure** : `03e9ec8` (test, RED) → `6a702ee` (feat, GREEN)
2. **Tâche 2 — Lire** : `b2e6fed` (test, RED) → `6aae76f` (feat, GREEN)
3. **Tâche 3 — lecture seule** : `14c91d2` (test : 4 gardes + XML-doc d'`Ouvrir`) ; mutations jouées puis révoquées sans commit

**Plan metadata :** commit `docs(29-02)` final (SUMMARY, STATE, ROADMAP).

## Critères grep

| Critère | Attendu | Mesuré |
|---|---|---|
| `UsageNormalization.InstantDepuisEpochMillisecondes` dans le lecteur | ≥ 1 | 1 |
| `FromUnixTime\|TimeSpan\.From\|Claude_\|claude-code-sessions` dans le lecteur | 0 | 0 |
| `lastAssistantUuid` dans le lecteur | 0 | 0 |
| `public static readonly System.TimeSpan LectureAppBureau` dans HorizonsSessions.cs | 1 | 1 |
| `_fixture` dans RacineAppBureau.cs | 0 | 0 |
| `lock (_verrou)` | 1 | 1 |
| `HorizonsSessions.LectureAppBureau` dans le lecteur | 1 | 1 |
| `_premiereApparition` | ≥ 2 | 6 |
| `EnumerateFiles("local_*.json")` | 1 | 1 |
| `FileShare.ReadWrite \| FileShare.Delete` | ≥ 1 | 1 (unique : c'est ce qui fait mordre la mutation b) |
| `File\.(Write\|ReadAll\|OpenRead\|Delete\|Move\|Copy)` dans le lecteur | 0 | 0 |
| `new LecteurAppBureau` dans `src/` (aucun câblage) | 0 | 0 |
| `git status --porcelain tests/Chronos.Tests/TestData` | vide | vide |

**Gardes vertes, sans assouplissement :** `GardesPerimetreTests`, `ServicesLayerPurityTests`, `NormalisationUniqueTests`
(`LecteurAppBureau.cs` non exempté), `HorizonsSessionsTests` (chaîne, câblage, silence en un point), `GardesDoctrineTests`.
Aucune écriture hors `Path.GetTempPath()` ; aucun dossier `chronos-appbureau-*` laissé derrière ; aucun test ne vise
une racine réelle de l'app (tous les lecteurs reçoivent des racines temporaires explicites). L'overlay n'a pas été lancé.

## Files Created/Modified

- `src/Chronos/Services/LecteurAppBureau.cs` — la source (506 l.) : contrat, `Interpreter`, `Lire`, `Ouvrir`
- `src/Chronos/Services/HorizonsSessions.cs` — `LectureAppBureau` = 24 h, puce de chaîne
- `tests/Chronos.Tests/RacineAppBureau.cs` — helper partagé (135 l.), dont `AjouterBruit` (bruit réel du Piège 3)
- `tests/Chronos.Tests/LecteurAppBureauTests.cs` — 31 cas (508 l.)
- `tests/Chronos.Tests/LectureSeuleAppBureauTests.cs` — 4 cas (201 l.)

## Écart écrit à la recherche

**`lastAssistantUuid` n'est pas lu.** 29-RESEARCH (Pattern 1) proposait un champ `ResumeAJour`
(`postTurnSummaryFor == lastAssistantUuid`). Il est écarté : la liste des champs lus est VERROUILLÉE par le contexte,
et l'app efface elle-même le résumé quand un tour démarre ou qu'un message utilisateur arrive (lu dans son code) —
un résumé présent est donc celui du dernier tour. Le contrat livré n'a pas de `ResumeAJour` ; `ResumePour`
(`postTurnSummaryFor`) sert seulement de clé d'épisode.

## Decisions Made

- Instant de classification figé par (cliSessionId, `postTurnSummaryFor`) à la première apparition ; mémoire purgée
  des épisodes absents du cycle ; racine absente ⇒ cache et mémoire vidés.
- Un epoch présent mais sous le plancher de sanité compte comme ABSENT (inexploitable) ; un texte présent mais vide
  vaut nul sans être compté absent (il est présent et du bon type — ex. `needs_action: ""` est légitime).
- `IssueLecture.Illisible` n'est jamais stocké dans le cache : seules les lectures réussies (valide ou sans
  identifiant) y entrent ; `SansCliSessionId` est donc compté depuis le cache aussi (test 18 le vérifie au 2e cycle).
- Doublons ex aequo départagés par le premier chemin ordinal ; le test 19 vérifie aussi l'ordre de noms inversé
  (l'ordre d'énumération ne décide pas).

## Deviations from Plan

### Auto-fixed Issues

Aucune correction de bogue nécessaire. Trois ajouts mineurs, dans l'esprit du plan :

**1. [Rule 2 - Robustesse] `RacineAppBureau.AjouterBruit`**
- **Constat :** le bruit réel du dossier (test 13) est aussi exigé par le test 26, dans une autre classe.
- **Ajout :** une méthode du helper partagé, au lieu de deux copies — réutilisable par 29-03 et 29-05.
- **Commit :** `03e9ec8`

**2. [Rule 2 - Falsifiabilité] Test 19 renforcé par l'ordre de noms inversé**
- **Constat :** avec `local_a` = le plus récent, « garder le premier énuméré » passerait aussi.
- **Ajout :** une seconde racine aux noms inversés dans le même test ; le cas du plan est inchangé.
- **Commit :** `b2e6fed`

**3. [Rule 2 - Falsifiabilité] Mutation (b′) en plus de (a) et (b)**
- **Constat :** sous (b), le test 25 rougit dès l'ouverture de l'écrivain ; la moitié « suppression » n'était pas
  prouvée seule.
- **Ajout :** (b′) = partage sans `Delete`, qui fait rougir exactement cette moitié ; révoquée (sha256 identique).

**Total deviations :** 3 ajouts de falsifiabilité/réutilisation, aucun changement de contrat ni de périmètre.

## Parallélisme (vague 1 avec 29-01)

Un moment d'attente : pendant la RED de la tâche 2, la solution ne compilait plus à cause d'une édition en cours de
29-01 (`App.xaml.cs` lisait un membre de `SessionMonitor` pas encore écrit). Aucun fichier de 29-01 n'a été touché :
attente jusqu'à son commit `428bf43` (~40 s), puis RED constatée. Les deux rouges de `GardesPerimetreTests` vus à la
tâche 1 étaient les rouges volontaires de 29-01 (`4c806da`), verts depuis.

## Issues Encountered

Aucun, hors l'attente ci-dessus.

## Known Stubs

Aucun. Le lecteur n'est volontairement câblé nulle part : c'est l'objet de 29-03 (le moniteur le consomme).

## Exigences

- **APP-01** : la lecture tolérante est livrée et prouvée sur les fixtures réelles ; la jointure au moniteur et la
  dégradation v1.6 sont 29-03 — **APP-01 reste Pending jusqu'à 29-03**.
- **APP-05** : gardes n° 1 (texte) et n° 3 (comportement) livrées et falsifiées ; la n° 2 (le chemin de l'app n'existe
  que dans la résolution des racines) est 29-05 — **APP-05 reste Pending jusqu'à 29-05**.

## Next Phase Readiness

29-03 peut construire `new LecteurAppBureau(RacinesEtat…)` et consommer `LectureAppBureau.ParSession` (clé
`cliSessionId`, sans casse) : titre, dernier focus, `ClassificationFinDeTour.Bloquee` + `MotifBlocage` +
`InstantClassification` (déjà figé par épisode). Le helper `RacineAppBureau` est prêt pour ses tests.

## Self-Check: PASSED

Fichiers : les 5 fichiers de code/tests et ce SUMMARY existent. Commits : 03e9ec8, 6a702ee, b2e6fed, 6aae76f, 14c91d2 présents (et les références 10a821f, 428bf43, 4c806da).
