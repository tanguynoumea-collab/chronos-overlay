---
phase: 28-deux-mots-une-question-les-m-mes-horizons
plan: 02
subsystem: widget-sessions
tags: [csharp, wpf, mvvm, arbitrage, ordre-ecran, xunit, mutation]
one_liner: "L'ordre d'écran dit l'urgence (attente déduite devant la réflexion) sans toucher l'arbitrage — RangArbitrage privé figé, prouvé sur 720 permutations d'un corpus qui descend au rang 3 et par deux mutations ; l'indéterminé quitte le widget par un motif nommé au moniteur ; un seul prédicat d'attente, égal à celui du détecteur"

requires:
  - phase: 24-fus
    provides: "ArbitrageSessions (FUS-01), ordre total de départage, corpus de 720 permutations"
  - phase: 22-obs
    provides: "LectureSessions / MotifMasquage, Visibles = écran (OBS-01), rapport partagé"
  - phase: 26-trt
    provides: "SessionTreatmentTracker.EstAttente et sa garde documentaire croisée"
provides:
  - "AffichageSessions.Urgence = ordre d'ÉCRAN seul (Attention 0, Turn 1, Deduced 2, Working 3, Unknown 4)"
  - "ArbitrageSessions.RangArbitrage privé, figé aux valeurs de la phase 24 (réserve R4 fermée)"
  - "AffichageSessions.EstUneAttente — LE prédicat « est une attente », point d'entrée de la phase 30"
  - "AffichageSessions.AUneLigne + MotifMasquage.Indeterminee, masquage au moniteur après archivée et traitée"
  - "§3 de docs/hooks-contract.md : les deux ordres (écran / RangArbitrage) + ligne Unknown sans ligne"
affects: [28-03, 28-04, 30-lue, 31]

tech-stack:
  added: []
  patterns:
    - "Deux ordres découplés : un classement d'affichage ne peut plus réécrire une règle d'arbitrage"
    - "Corpus de permutation qui DESCEND au rang testé + assertion de non-vacuité (un corpus muet ne prouve rien)"
    - "Masquage au moniteur avec motif nommé, jamais au ViewModel (Visibles reste mot pour mot l'écran)"

key-files:
  created: []
  modified:
    - src/Chronos/Services/AffichageSessions.cs
    - src/Chronos/Services/ArbitrageSessions.cs
    - src/Chronos/Services/DiagnosticService.cs
    - src/Chronos/Services/LectureSessions.cs
    - src/Chronos/Services/SessionMonitor.cs
    - src/Chronos/Services/SessionTreatmentTracker.cs
    - docs/hooks-contract.md
    - tests/Chronos.Tests/AffichageSessionsTests.cs
    - tests/Chronos.Tests/ArbitrageSessionsTests.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs
    - tests/Chronos.Tests/ContratHooksDocumenteTests.cs
    - tests/Chronos.Tests/InspectionSessionsTests.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - tests/Chronos.Tests/SessionStylesBindingTests.cs

key-decisions:
  - "R4 fermée par DÉCOUPLAGE : l'arbitrage a son propre RangArbitrage privé, figé aux valeurs de la phase 24 ; l'ordre d'écran peut changer sans toucher FUS-01"
  - "L'exception héritée (WaitingDeduced et Unknown ex aequo au rang 3 de l'arbitrage) est ÉCRITE et NON corrigée : pas de rang 6, la phase s'interdit de changer un départage"
  - "L'indéterminé est masqué au MONITEUR (MotifMasquage.Indeterminee), après archivée et traitée : le geste de l'utilisateur prime, et le rapport la liste parmi les MASQUÉES"
  - "SessionTreatmentTracker.EstAttente garde son corps (garde documentaire au texte) ; seule sa visibilité passe internal, et un test tient son égalité avec AffichageSessions.EstUneAttente sur les cinq états"
  - "LIB-04 coché ; LIB-01 et LIB-03 restent Pending (moitié couche neutre livrée, les mots et les huit gabarits sont le plan 28-03)"

patterns-established:
  - "Contrôle de mutation joué dans les deux sens (ordre d'écran muté sans découplage ; arbitrage recâblé sur l'ordre d'écran), révoqué par checksum"

requirements-completed: [LIB-04]

duration: 11min
completed: 2026-09-25
---

# Phase 28 Plan 02 : L'ordre dit l'urgence, et ne touche plus l'arbitrage — Summary

**L'ordre d'écran dit l'urgence (attente déduite devant la réflexion) sans toucher l'arbitrage — `RangArbitrage`
privé figé, prouvé sur 720 permutations d'un corpus qui descend au rang 3 et par deux mutations ; l'indéterminé
quitte le widget par un motif nommé au moniteur ; un seul prédicat d'attente, égal à celui du détecteur.**

## Performance

- **Duration:** ~11 min
- **Started:** 2026-09-25T19:23:10Z
- **Completed:** 2026-09-25T19:34:32Z
- **Tasks:** 2 / 2
- **Files modified:** 14 (6 sources, 1 document, 7 fichiers de tests)
- **SHA d'entrée du plan :** `5f5c3cb`

## Accomplishments

- **LIB-04 livré.** `AffichageSessions.Urgence` est désormais l'ordre d'ÉCRAN, et lui seul : permission ou question
  (0), tour fini (1), attente déduite (2), travail (3), indéterminé (4) ; puis la fraîcheur. Le widget et le rapport
  le lisent ; le repli codé `3` du rapport (qui aurait valu `Working` avec le nouvel ordre) est remplacé par
  `dernierRang = Urgence(Unknown)`.
- **Réserve R4 fermée, et prouvée.** `ArbitrageSessions.Departager` lit un `RangArbitrage` privé figé aux valeurs de
  la phase 24 ; plus aucun appel `AffichageSessions.Urgence(` dans l'arbitrage (garde textuelle). Le corpus de
  remplacement `CorpusRang3` descend au rang 3 (même session, même instant, même source), 720 permutations, un seul
  résultat, `r1=Working@` — avec une assertion de non-vacuité en tête.
- **Un seul prédicat d'attente.** `AffichageSessions.EstUneAttente` (tour fini, permission/question, déduite) — le
  point d'entrée de la règle « lue » (phase 30) ; égal au prédicat du détecteur sur les cinq états, par test.
- **L'indéterminé n'a plus de ligne.** `AUneLigne(Unknown) == false` ; le moniteur le masque avec
  `MotifMasquage.Indeterminee` APRÈS archivée et traitée ; le rapport le liste « masquée par état indéterminé
  (signal illisible) — aucune ligne dans le widget ». `TotalCount`, `WaitingCount` et « Tout marquer traité (N) »
  l'excluent sans une ligne de code de plus.
- **Le §3 du contrat dit les deux ordres** (écran / `RangArbitrage`) et ne contient plus « derrière `Working` (2) » ;
  la ligne `Unknown` de la table dit « (aucune ligne) … listée parmi les MASQUÉES ». Garde croisée document ↔ code.
- **Aucun mot affiché n'a changé** (`"à toi"` toujours présent une fois dans `AffichageSessions.cs`) ; aucune UI
  touchée (`git diff --stat 5f5c3cb..HEAD -- src/Chronos/ViewModels src/Chronos/Resources src/Chronos/Views` vide).

## Task Commits

1. **Tâche 1 : l'ordre d'écran dit l'urgence, l'arbitrage a son propre rang figé (LIB-04, R4)** — `147f41a` (feat)
2. **Tâche 2 : le prédicat unique, l'indéterminé masqué au moniteur avec son motif (LIB-01, LIB-03)** — `24a0957` (feat)

**Plan metadata :** commit `docs(28-02)` final (SUMMARY, STATE, ROADMAP, REQUIREMENTS).

## Preuves — rouges, mutations, totaux

### Tâche 1 — RED (tests écrits avant le code), 4 classes : 8 échecs / 56 réussites sur 64

- `ContratHooksDocumenteTests.Le_paragraphe_3_dit_l_ordre_d_ecran_et_le_rang_d_arbitrage_fige`
- `GardesPerimetreTests.L_arbitrage_ne_lit_pas_l_ordre_d_ecran`
- `AffichageSessionsTests.L_ordre_d_ecran_dit_l_urgence_puis_la_fraicheur`
- `AffichageSessionsTests.Chaque_etat_a_son_rang_d_urgence` × 3 : (`Working, 3`), (`WaitingDeduced, 2`), (`Unknown, 4`)
- `AffichageSessionsTests.A_l_ecran_une_attente_deduite_passe_devant_la_reflexion`
- `ArbitrageSessionsTests.Remonter_la_deduction_a_l_ecran_ne_change_aucun_arbitrage` — par sa NON-VACUITÉ
  (« L'ordre d'écran ne place plus la déduction devant le travail : ce test ne prouverait plus rien. »)
- VERTE comme prévu : `Le_rang_d_arbitrage_reste_celui_de_la_phase_24` × 4 (l'arbitrage d'alors lisait des rangs
  égaux à ceux de la phase 24). Les 4 classes passent de 56 à 64 cas : **+8 exactement**.

### Tâche 1 — Mutation n° 1 (étape 2 du plan) : `Urgence` au nouvel ordre, arbitrage ENCORE couplé

- `ArbitrageSessionsTests.Remonter_la_deduction_a_l_ecran_ne_change_aucun_arbitrage` — **rouge sur `r1=Working@`** :
  `Assert.Contains() Failure … String: "r1=WaitingDeduced@2026-09-12T13:28:00.000"··· Not found: "r1=Working@"`
- `ArbitrageSessionsTests.Le_rang_d_arbitrage_reste_celui_de_la_phase_24(a: Working, b: WaitingDeduced, gagnant: Working)`
  — `Expected: Working / Actual: WaitingDeduced`
- (plus les deux gardes textuelle et documentaire, pas encore satisfaites à ce stade)
- **Constat qui confirme la recherche (Q2) :** sous cette mutation, le test historique
  `Permuter_l_ordre_des_signaux_ne_change_pas_un_seul_etat_ni_un_seul_desaccord` est resté VERT — le corpus d'origine
  était bien muet sur R4.
- Révoquée par l'étape 3 (découplage) : le couplage n'a jamais été commité.

### Tâche 1 — Mutation n° 2 (contrôle du vérificateur) : après le GREEN, `Departager` relit `AffichageSessions.Urgence`

- 3 échecs nommés : `Le_rang_d_arbitrage_reste_celui_de_la_phase_24(a: Working, b: WaitingDeduced, gagnant: Working)`,
  `Remonter_la_deduction_a_l_ecran_ne_change_aucun_arbitrage` (`Not found: "r1=Working@"`),
  `GardesPerimetreTests.L_arbitrage_ne_lit_pas_l_ordre_d_ecran`.
- Révoquée : sha256 de `ArbitrageSessions.cs` identique avant/après
  (`690fce2ffef57fd338df878d9465a49fe8698fa13cfb8812668e906a8bbc9e2e`), diff de mutation vide.

### Tâche 2 — RED contre des squelettes compilables (précédent 18-01), 5 classes : 9 échecs / 88 réussites sur 97

- `AffichageSessionsTests.Seul_l_etat_indetermine_n_a_pas_de_ligne` (`NotImplementedException`)
- `AffichageSessionsTests.EstUneAttente_dit_exactement_ce_que_dit_le_detecteur` (`NotImplementedException`)
- `AffichageSessionsTests.Le_compteur_d_attente_inclut_la_deduction` (`TotalCount` 4 ≠ 3)
- `InspectionSessionsTests.Une_activite_illisible_reste_inconnue_et_non_deduite` (l'indéterminé encore visible)
- `DiagnosticServiceTests.Une_session_indeterminee_est_annoncee_masquee_avec_son_motif`
- `SessionStylesBindingTests` × 4 via `Vm()` (5 lignes ≠ 4) : `Le_style_Pastilles_n_a_plus_qu_un_seul_separateur_visible_par_session`,
  `Dans_les_huit_menus_le_reversible_precede_le_destructif_et_les_libelles_le_disent`,
  `Les_8_styles_et_les_9_themes_se_chargent_se_mesurent_et_se_disposent`, `Les_trois_gestes_sont_offerts_sur_les_8_styles_et_les_9_themes`
- VERTS par construction : `Le_geste_de_l_utilisateur_prime_sur_l_etat_indetermine` (le filtre d'archive passait
  déjà en premier — il fige l'ordre), `Une_session_deduite_est_visible_ambre_et_dit_sa_deduction` (adapté),
  `Un_hook_deduit_et_un_transcript_en_tour_fini_du_MEME_instant_sont_departages_par_la_source` (reformulé),
  `ContratHooksDocumenteTests.Le_document_dit_la_regle_de_traitement_reellement_cablee` (le prédicat `EstAttente`
  est toujours trouvé et contient `WaitingDeduced`).

### Totaux mesurés (`dotnet test Chronos.sln -c Debug --nologo -v q`)

| Instant | Total | Échecs | Composition |
|---|---|---|---|
| Entrée du plan (`5f5c3cb`, 28-01 pas encore commité) | **896** | 0 | baseline |
| Après la tâche 1 | 913 | 0 | 896 + 8 (28-02) + 9 (28-01, travail en cours non commité) |
| Après la tâche 2 — deux exécutions consécutives | **924** | 0 | 896 + **12** (28-02) + 16 (28-01, `TranscriptQuestionTests` + `TranscriptInstantSignalTests`, mesurés à 16) |

Part propre de ce plan : **896 + 12 = 908**, conforme au plan (+8 puis +4). Aucun test supprimé, aucun ignoré.

**Deux renommages annoncés :**
1. `AffichageSessionsTests.Une_deduction_ne_passe_jamais_devant_une_observation` → `A_l_ecran_une_attente_deduite_passe_devant_la_reflexion`
2. `InspectionSessionsTests.Un_hook_deduit_et_un_transcript_travaillant_du_MEME_instant_sont_departages_par_la_source`
   → `Un_hook_deduit_et_un_transcript_en_tour_fini_du_MEME_instant_sont_departages_par_la_source` (dette n° 6 / R5
   reformulée, pas supprimée : elle survivra au silence étendu de 28-04)

**Gardes vertes, sans assouplissement :** `GardesPerimetreTests` 12/12 (dont `L_arbitrage_ne_connait_ni_horloge_ni_magasin_ni_chemin`),
`ServicesLayerPurityTests` 2/2, `ContratHooksDocumenteTests` 11/11, `GardesDoctrineTests` 8/8,
`NormalisationUniqueTests` 3/3, `CompositionRootTests` 5/5, `TreatedSessionsTests` 15/15, `GesteTraiteTests` 5/5,
`SessionStylesBindingTests` 4/4 (72 combinaisons), `ArbitrageSessionsTests` 15/15 (corpus d'origine INCHANGÉ).

### Critères grep

`AffichageSessions.Urgence(` dans `ArbitrageSessions.cs` = 0 ; `RangArbitrage(` = 3 lignes ;
`SessionActivity.WaitingDeduced => 2` = 1 ; `_ => 4` = 1 ; `dernierRang` = 3 ; `: 3;` = 0 ;
« derrière `Working` (2) » dans le contrat = 0 ; `RangArbitrage` dans le contrat = 1 ;
`AffichageSessions.AUneLigne(` dans le moniteur = 1 ; `MotifMasquage.Indeterminee` moniteur = 1, rapport = 1 ;
`internal static bool EstAttente(SessionActivity` = 1 ; `"à toi"` = 1.

## Files Created/Modified

- `src/Chronos/Services/AffichageSessions.cs` — ordre d'écran, `EstUneAttente`, `AUneLigne`
- `src/Chronos/Services/ArbitrageSessions.cs` — `RangArbitrage` privé figé, XML-doc (item 3 + exception héritée)
- `src/Chronos/Services/DiagnosticService.cs` — `dernierRang`, libellé du motif `Indeterminee`
- `src/Chronos/Services/LectureSessions.cs` — `MotifMasquage.Indeterminee`
- `src/Chronos/Services/SessionMonitor.cs` — masquage de l'indéterminé après archivée et traitée
- `src/Chronos/Services/SessionTreatmentTracker.cs` — `EstAttente` `internal`, corps intact
- `docs/hooks-contract.md` — §3 : les deux ordres ; ligne `Unknown`
- `tests/Chronos.Tests/*` — 7 fichiers (voir preuves ci-dessus)

## Decisions Made

Voir `key-decisions` en tête. En bref : découpler plutôt qu'aligner (deux ordres, deux usages) ; ne pas corriger
l'ex aequo hérité déduit/indéterminé (écrit, pas de rang 6) ; masquer au moniteur, jamais au ViewModel ; garder le
corps du prédicat du détecteur et tenir l'égalité par test.

## Deviations from Plan

### Ajustements mineurs (aucun changement de comportement)

**1. [Rule 3 - Blocage de critère] Appel de `RangArbitrage` réparti sur deux lignes**
- **Found during:** Tâche 1 (critères grep)
- **Issue:** la forme d'une ligne donnée par le plan faisait `grep -c "RangArbitrage("` = 2 lignes, sous le seuil
  « ≥ 3 (déclaration + deux appels) » qui compte des lignes.
- **Fix:** reprise de la mise en forme d'origine de `Departager` (appel sur deux lignes) ; même code, même sens.
- **Commit:** `147f41a`

**2. [Rule 1 - Test exact] Le test du rapport repère la ligne par son projet**
- **Found during:** Tâche 2
- **Issue:** le rapport abrège l'identifiant à 8 caractères (`Court`), donc `inde-0001-xx` s'y lit `inde-000` : une
  recherche sur `inde-0001` ne trouverait jamais la ligne.
- **Fix:** `Single(l => l.Contains("projet-illisible"))`, puis `Contains("inde-000")` et `Contains("masquée par état indéterminé")`.
- **Commit:** `24a0957`

**3. [Rule 2 - Preuve] Seconde mutation jouée (sens inverse)**
- Au-delà de l'étape 2 du plan, le contrôle que la recherche confie au vérificateur a été joué ici (arbitrage recâblé
  sur l'ordre d'écran après le GREEN) : 3 rouges nommés, révocation prouvée par checksum.

**4. [Rule 1 - Exactitude documentaire] Deux commentaires mis à jour**
- XML-doc du test d'égalité à la milliseconde (`InspectionSessionsTests`) : « rang urgence (3) » → « rang d'état
  (3, `RangArbitrage`) » ; commentaire de `Departager` : renvoi à l'exception héritée écrite dans la doc du type.

**5. [Exécution parallèle] RED et GREEN dans un même commit par tâche**
- Les rouges ont été exécutés et consignés nominativement ci-dessus, mais non commités seuls : 28-01 partageait le
  MÊME arbre de travail et lançait la suite complète ; un commit rouge aurait faussé ses propres mesures.

**Total deviations:** 5 mineures, aucune architecturale. **Impact:** aucun changement de périmètre.

## Issues Encountered

Aucun. Exécution en parallèle de 28-01 sur des fichiers disjoints sans conflit de verrou (`index.lock`) ni de build.

## Known Stubs

Aucun. `AffichageSessions.Etat(Unknown)` vaut encore « inconnu » : il n'est plus lu que par le rapport (masquées,
désaccords) — les mots sont le plan 28-03, qui le renomme.

## Next Phase Readiness

- 28-03 (les mots, les huit gabarits) : `EstUneAttente` est prêt à remplacer les deux copies du ViewModel (`IsWaiting`,
  `WaitingCount`) ; `IsGhost` n'a plus aucun cas vrai à l'écran (l'`Unknown` est masqué en amont).
- 28-04 (silence étendu aux transcripts) : la dette n° 6 est déjà reformulée avec un transcript en tour fini.
- Phase 30 (règle « lue ») : point d'entrée `AffichageSessions.EstUneAttente`.

## Self-Check: PASSED

Fichiers annoncés présents ; commits `147f41a`, `24a0957` et SHA d'entrée `5f5c3cb` trouvés ; diff UI (ViewModels, Resources, Views) depuis `5f5c3cb` vide.
