---
phase: 30
slug: la-lecture-fait-dispara-tre
status: planned
nyquist_compliant: true
wave_0_complete: n/a
created: 2026-09-26
---

# Phase 30 — Validation Strategy

> Carte **posée à la planification** (2026-09-26), à **remplir de valeurs mesurées** au fil des quatre plans. Les colonnes
> « Attendu » viennent des plans ; les colonnes « Mesuré » restent vides tant qu'aucune exécution ne les a produites.
> **Ne rien y écrire qui n'ait été mesuré.**
>
> **Pièges propres à cette phase.** (1) Le premier plan RÉEL de la machine de test est quelconque (session verrouillée ⇒
> `LockApp`, agent ⇒ un terminal) : un seul test touche l'OS (fumée P08) et n'asserte aucun nom de processus ; tout le reste passe
> par une sonde injectée ou `FakePremierPlan`. (2) Les heures du rapport sont LOCALES : chaque attendu horaire est calculé dans le
> test par `ToLocalTime().ToString("HH:mm:ss")`, jamais écrit en dur. (3) `TreatedStore` mesure sa rétention de 24 h à SON
> horloge : toujours `new FakeClock(<instant du test>)`.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + Xunit.StaFact 1.1.11, Microsoft.NET.Test.Sdk 17.11.1 — `tests/Chronos.Tests` (`net8.0-windows`) ; aucun `[WpfFact]` nouveau (aucune UI touchée) |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` (attributs `CheminSourcesChronos`, `CheminDocsChronos` ; `InternalsVisibleTo Chronos.Tests`) ; fixtures RÉELLES `tests/Chronos.Tests/TestData/DesktopAppSessions/*.json` (phase 27), dérivées TEXTUELLEMENT par `RacineAppBureau.Deriver` ; `Fakes/FakeClock.cs`, `Fakes/FakePremierPlan.cs` (créé en 30-02) |
| **Quick run command** | `dotnet test Chronos.sln -c Debug --nologo -v q --filter "FullyQualifiedName~DetecteurLectureTests\|FullyQualifiedName~PremierPlanWin32Tests\|FullyQualifiedName~LecteurAppBureauTests\|FullyQualifiedName~MoniteurLectureTests\|FullyQualifiedName~MoniteurAppBureauTests\|FullyQualifiedName~TreatedSessionsTests\|FullyQualifiedName~InspectionSessionsTests\|FullyQualifiedName~GesteTraiteTests\|FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~CompositionRootTests\|FullyQualifiedName~HorizonsSessionsTests\|FullyQualifiedName~NormalisationUniqueTests\|FullyQualifiedName~ServicesLayerPurityTests\|FullyQualifiedName~ContratHooksDocumenteTests\|FullyQualifiedName~LectureSeuleAppBureauTests\|FullyQualifiedName~LibellesSessionsTests"` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` |
| **Estimated runtime** | ~11 s de tests, ~25 s avec la compilation (baseline 1062 tests, 11 s) |
| **Baseline d'entrée de phase** | **1062 verts / 0 échec** (fin de phase 29, HEAD ≈ `1a25fdf`) |
| **Cible de fin de phase** | **0 échec, 1143.** Égalité, pas plancher : `total = total mesuré du plan précédent + cas ajoutés` ; tout écart (même positif) justifié nominativement au SUMMARY. |
| **Attendus indicatifs** | 30-01 : +19 (T1) +7 (T2) = **1088** seul ; 30-02 : +14 (T1) +8 (T2) = **1084** seul ; **1110** après la vague 1 ; 30-03 : +4 (T1) +17 (T2) +1 (T3) = **1132** ; 30-04 : +10 (T1) +1 (T2) = **1143** |
| **Tests adaptés** | 30-03 T1 (LUE-05 corrige le comportement v1.6 qu'ils figeaient) : `TreatedSessionsTests.NET03_le_monitor_la_reaffiche_sur_nouvel_episode` (assertion à t1), `InspectionSessionsTests.Le_cas_e465420e_devient_lisible_en_une_lecture` (visible « Réflexion ») ; 30-03 T3 : `CompositionRootTests.Le_graphe_DI_resout_la_chaine_de_sessions` (miroir avec la sonde) |
| **Renommages annoncés** | **1** — `TreatedSessionsTests.NET01_le_monitor_masque_apres_reponse` → `NET01_la_reponse_est_inscrite_et_la_session_montre_son_travail` (30-03 T1 : l'ancien nom affirmerait le contraire de LUE-05) |
| **Total mesuré après 30-01** | **1088** isolé (1057 hors classes de 30-02 + 31 cas antérieurs de `LecteurAppBureauTests`) ; 120 / 120 au filtre de T1, 82 / 82 au filtre de T2 à `f0ab69a` (repris du 30-01-SUMMARY) |
| **Total mesuré après 30-02** | **1084 / 0** isolé, deux exécutions consécutives, worktree temporaire à `af60f59` (repris du 30-02-SUMMARY) |
| **Total mesuré après la vague 1** | **1110 / 0**, deux exécutions consécutives (9 s, 8 s) à `284fcc7` (repris des SUMMARY 30-01 et 30-02) |
| **Total mesuré après 30-03** | **1132 / 0**, deux exécutions consécutives (11 s, 11 s) à `9ec7233` ; 1114 / 0 après T1, 1131 / 0 après T2 (entrée : 1110 / 0 à `962e250`) |
| **Total mesuré après 30-04 (deux exécutions)** | **1143 / 0**, deux exécutions consécutives (10 s, 10 s) à `5995fa5` ; 1142 / 0 après T1 (`aa3d722`) (entrée : 1132 / 0 à `4c27afb`, 10 s). **Cible de fin de phase atteinte exactement.** |

---

## Sampling Rate

- **Après chaque tâche :** le filtre du `<verify>` de la tâche (ou la commande rapide ci-dessus).
- **Après chaque vague :** `dotnet test Chronos.sln -c Debug --nologo -v q`, deux exécutions consécutives.
- **Avant `/gsd:verify-work` :** suite complète verte deux fois, les **neuf contrôles de mutation** ci-dessous consignés (rouges
  nommés, sha256 identique après révocation).
- **Latence maximale de retour :** ~25 s (suite complète avec compilation).

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 30-01-01 | 01 | 1 | LUE-01, LUE-02, LUE-04 | unit pur, détecteur + TreatedStore temporaire (L01-L15, L20, L21 ; sentinelle de date ; seuil G04) + **mutations (m1), (m2), (m4)** | `dotnet test … --filter "FullyQualifiedName~DetecteurLectureTests\|FullyQualifiedName~TreatedSessionsTests\|FullyQualifiedName~HorizonsSessionsTests\|FullyQualifiedName~ContratHooksDocumenteTests\|FullyQualifiedName~NormalisationUniqueTests\|FullyQualifiedName~ServicesLayerPurityTests\|FullyQualifiedName~GesteTraiteTests\|FullyQualifiedName~InspectionSessionsTests\|FullyQualifiedName~MoniteurAppBureauTests"` | ✅ créé (`DetecteurLectureTests.cs`) + `HorizonsSessionsTests` étendue | ✅ green (120 / 0 au filtre, `3b7ce99` ; repris du 30-01-SUMMARY) |
| 30-01-02 | 01 | 1 | LUE-03 | unit pur (cause retenue, figée, oubliée, jamais inventée : L16-L19, L22-L24) | `dotnet test Chronos.sln -c Debug --nologo -v q` | ✅ (même classe) | ✅ green (82 / 0 au filtre, 1110 / 0 combiné, `f0ab69a` ; repris du 30-01-SUMMARY) |
| 30-02-01 | 02 | 1 | LUE-02, LUE-04 | unit (sonde injectée : cache, depuis, casse, erreurs, horloge, trou) + fumée Win32 réelle + garde texte + **mutation (m7)** | `dotnet test … --filter "FullyQualifiedName~PremierPlanWin32Tests\|FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~ServicesLayerPurityTests\|FullyQualifiedName~LectureSeuleAppBureauTests\|FullyQualifiedName~HorizonsSessionsTests\|FullyQualifiedName~NormalisationUniqueTests"` | ✅ créé (`PremierPlanWin32Tests.cs` 14 cas, `Fakes/FakePremierPlan.cs`) | ✅ green (52 / 0, `b7b77cb`) |
| 30-02-02 | 02 | 1 | LUE-02, LUE-04 | intégration racine temporaire (sélection sur tous les fichiers, doublon, sans identifiant, ex aequo, cache) + **mutation (m3)** | `dotnet test Chronos.sln -c Debug --nologo -v q` | ✅ classe existante étendue (`LecteurAppBureauTests` 31 → 39) | ✅ green (1084 / 0 isolé à `b10620f` ; 1110 / 0 combiné fin de vague 1 à `284fcc7`) |
| 30-03-01 | 03 | 2 | LUE-05 | intégration moniteur (V01-V04) + 3 tests adaptés + **mutation (m6)** | `dotnet test … --filter "FullyQualifiedName~MoniteurLectureTests\|FullyQualifiedName~TreatedSessionsTests\|FullyQualifiedName~InspectionSessionsTests\|FullyQualifiedName~GesteTraiteTests\|FullyQualifiedName~MoniteurAppBureauTests\|FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~HorizonsSessionsTests"` | ✅ créé (`MoniteurLectureTests.cs`) | ✅ green (141 / 0 au filtre, 1114 / 0 suite, `5db55b0`) |
| 30-03-02 | 03 | 2 | LUE-01, LUE-02, LUE-04 | intégration moniteur RÉEL + lecteur RÉEL + fixtures du relevé + sonde factice (M01-M14 : 16 h 08, gestes A et B, sans identifiant, dégradation ×3, indisponible, 478 min, gestes v1.6, question de l'app, cycle complet, sentinelle, état du premier plan, sonde qui lève) + **mutation (m3 bis)** | `dotnet test … --filter "FullyQualifiedName~MoniteurLectureTests\|FullyQualifiedName~MoniteurAppBureauTests\|FullyQualifiedName~TreatedSessionsTests\|FullyQualifiedName~InspectionSessionsTests\|FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~HorizonsSessionsTests\|FullyQualifiedName~NormalisationUniqueTests\|FullyQualifiedName~ServicesLayerPurityTests"` | ✅ (même classe, 21 cas) | ✅ green (158 / 0 au filtre, 1131 / 0 suite, `5e85684`) |
| 30-03-03 | 03 | 2 | LUE-02, LUE-04 | garde de source (câblage `premierPlan:`) + miroir DI + **mutation (m5)** | `dotnet test Chronos.sln -c Debug --nologo -v q` | ✅ classes existantes étendues | ✅ green (1132 / 0, deux exécutions, `9ec7233`) |
| 30-04-01 | 04 | 3 | LUE-03, LUE-04 | intégration rapport (R01-R09 : lue par focus, au premier plan, répondue, résidu, indisponible, sélection sans identifiant, source absente, exhaustivité, OBS-01 dynamique) + garde OBS-01 texte + **mutation (m8)** | `dotnet test … --filter "FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~NormalisationUniqueTests\|FullyQualifiedName~ServicesLayerPurityTests\|FullyQualifiedName~HorizonsSessionsTests\|FullyQualifiedName~LectureSeuleAppBureauTests"` | ✅ classes existantes étendues (`DiagnosticServiceTests` +9, `GardesPerimetreTests` +1) | ✅ green (RED 10 / 89 à `8342bcc` ; 89 / 0 au filtre, 1142 / 0 suite, `aa3d722`) |
| 30-04-02 | 04 | 3 | LUE-03 | garde croisée document ↔ code (§3 : trois façons, `GraceLecture`, LUE-05) | `dotnet test Chronos.sln -c Debug --nologo -v q` | ✅ classe existante étendue (`ContratHooksDocumenteTests` +1) | ✅ green (RED 1 / 23 à `104cb12` ; 23 / 0 avec `LibellesSessionsTests`, 1143 / 0 deux exécutions, `5995fa5`) |

*Status : ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Contrôles de mutation (joués, rouges constatés, révoqués par sha256)

| Id | Plan / tâche | Mutation | Doit rougir | Rouges constatés |
|----|--------------|----------|-------------|------------------|
| (m1) | 30-01 / T1 | `_store.Set(id, cur)` sans la condition `!traitee \|\| tts < cur` | `La_lecture_n_ecrit_qu_une_fois_par_episode` | l'exigé, et lui seul ; révoquée, sha256 `44264408…5952193` identique (repris du 30-01-SUMMARY) |
| (m2) | 30-01 / T1 | décision unique remplacée par deux blocs (LUE puis l'ancien NET-03 sur la lecture de tête de cycle) | `Un_nouvel_episode_deja_lu_s_ecrit_sans_purge` | l'exigé, et lui seul ; révoquée, sha256 `44264408…5952193` identique (repris du 30-01-SUMMARY) |
| (m4) | 30-01 / T1 | grâce en `>` au lieu de `>=` | `Geste_B_le_tour_fini_sous_les_yeux_est_lu_au_premier_plan` | l'exigé, plus `Un_retour_recent_au_premier_plan_attend_sa_grace` et `La_session_selectionnee_se_compare_sans_casse` (les trois posés sur la borne) ; révoquée, sha256 `44264408…5952193` identique (repris du 30-01-SUMMARY) |
| (m7) | 30-02 / T1 | `Classer` par `StartsWith` au lieu de l'égalité | `Le_nom_du_processus_se_compare_par_egalite_sans_casse` (« claudette »), `La_sonde_ne_lit_ni_UI_Automation_ni_titre_de_fenetre` | les deux exigés, et eux seuls ; révoquée, sha256 `4667de50…767c7` identique |
| (m3) | 30-02 / T2 | sélection calculée sur `ParSession` | `Le_dernier_focus_sur_un_fichier_sans_cliSessionId_ne_selectionne_personne`, `Le_focus_d_un_doublon_ecarte_compte_pour_la_selection` | les deux exigés, plus `A_focus_egal_la_selection_est_le_premier_chemin_ordinal` et `Le_cache_garde_le_focus_pour_la_selection` ; révoquée, sha256 `6b862e90…a2a11` identique |
| (m6) | 30-03 / T1 | filtre sans `&& AffichageSessions.EstUneAttente(s.Activity)` | `Une_session_traitee_qui_travaille_est_visible_Reflexion`, `NET01_la_reponse_est_inscrite_et_la_session_montre_son_travail`, `Le_cas_e465420e_devient_lisible_en_une_lecture` | les trois exigés, plus `Son_prochain_tour_fini_la_ramene_En_attente`, `Traitee_et_indeterminee_est_masquee_indeterminee` et `NET03_le_monitor_la_reaffiche_sur_nouvel_episode` (6) ; révoquée, sha256 `07c7f5d5…651db1ae` identique |
| (m3 bis) | 30-03 / T2 | le moniteur tire la sélection de `ParSession` au lieu de `Selection` | `Le_dernier_focus_sur_un_fichier_sans_cliSessionId_ne_selectionne_personne` (moniteur) | l'exigé, et lui seul ; révoquée, sha256 `1386bdc8…2cdb8b1bec5c` identique |
| (m5) | 30-03 / T3 | `premierPlan:` retiré d'`App.xaml.cs` | `Le_moniteur_de_production_recoit_le_premier_plan` | l'exigé, et lui seul ; révoquée, sha256 `6f887ff8…d5fd3710de8` identique |
| (m8) | 30-04 / T1 | `LibelleMasquage(m.Motif, null)` dans la ligne des masquées | `La_session_lue_par_focus_dit_ses_deux_instants`, `La_session_lue_au_premier_plan_dit_depuis_combien_de_secondes` | les deux exigés, plus `GardesPerimetreTests.Le_diagnostic_dit_la_regle_lue_dans_la_meme_lecture_que_le_widget` (son anti-muet exige l'appel avec `m.Cause`) (3) ; révoquée, sha256 `77f2ff90…1c1f0b40a` identique |

### Correspondance critères de la ROADMAP → preuves

| Critère | Preuve automatisée | Plan |
|---|---|---|
| 1 — le relevé de 16 h 08 ne se reproduit plus ; une attente postérieure au focus reste « En attente » | `Le_releve_de_16_h_08_ne_se_reproduit_plus` (M01) ; L01, L02, L03 | 30-03, 30-01 |
| 2 — réversible (NET-03), magasin existant, pas de réécriture à chaque rafraîchissement, gestes v1.6 | L04, L05, L06, L08 ; M11, M12, M09 | 30-01, 30-03 |
| 3 — le tour fini sous les yeux n'est pas annoncé « En attente » ; Win32 sans UIA ; gardes de la phase 21 inchangées | L10-L15, L21 ; P01-P09 ; M03, M03b, M04 ; `GardesPerimetreTests` sans modification de liste | 30-01, 30-02, 30-03 |
| 4 — aucun masquage sans cause | L16-L19, L22-L24 ; R01-R08 ; garde §3 | 30-01, 30-04 |
| 5 — pas de faux masquage sans métadonnées | L15, L20 ; S03-S05 ; M05, M06×3, M07, M08, M14 ; R07 | 30-01, 30-02, 30-03, 30-04 |
| 6 — une session qui travaille est toujours visible (LUE-05) | V01-V04 ; M11 ; tests adaptés | 30-03 |
| Question ouverte (attente déduite) — décision écrite D-30-07 | L07, L08 | 30-01 |

---

## Wave 0 Requirements

**Pas de vague 0 séparée — et c'est voulu** (précédent des phases 28 et 29). Chaque tâche de code est TDD (`tdd="true"`) : elle
écrit ses tests d'abord, constate le rouge contre des SQUELETTES compilables (un rouge de compilation ne dit rien), puis livre le
code dans la même tâche. Les manques relevés par la recherche (« Wave 0 Gaps ») sont couverts ainsi :

- [x] `tests/Chronos.Tests/DetecteurLectureTests.cs` — L01-L24, contexte construit à la main (30-01, tâches 1 et 2)
- [x] `tests/Chronos.Tests/PremierPlanWin32Tests.cs` — P01-P09, sonde injectée par le constructeur `internal` (30-02, tâche 1)
- [x] `tests/Chronos.Tests/Fakes/FakePremierPlan.cs` — faux `IPremierPlan` réglable, compteur d'appels, mode « lève » (30-02, tâche 1)
- [x] extension `LecteurAppBureauTests` — S01-S08 (30-02, tâche 2)
- [x] `tests/Chronos.Tests/MoniteurLectureTests.cs` — V01-V04, M01-M14 (30-03, tâches 1 et 2)
- [x] extensions `HorizonsSessionsTests` (G04), `GardesPerimetreTests` (G01, G03), `CompositionRootTests` (G02), `DiagnosticServiceTests`
      (R01-R09), `ContratHooksDocumenteTests` (G05)
- Aucun framework à installer ; les six fixtures réelles existent et ne sont jamais réécrites (dérivations textuelles seulement).

---

## Manual-Only Verifications

Toutes renvoyées au **constat de la phase 31** (exe publié, lancé normalement par explorer / démarrage ; l'agent ne lance ni
n'arrête l'overlay).

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Une session finie, ouverte dans l'app puis quittée, disparaît du widget SANS clic ; noter le délai | LUE-01 | Exige l'app bureau réelle et un vrai changement de focus ; la latence d'écriture de `lastFocusedAt` n'a été relevée qu'une fois (focus 20:30:44.976, fichier écrit 20:31:09 — Piège 11) | Laisser une session finir un tour hors de l'app ; vérifier « En attente » ; ouvrir la session dans l'app, revenir ailleurs ; chronométrer la disparition ; ouvrir « Diagnostic… » : la ligne masquée dit « lue : focus à … > attente à … » |
| Un tour qui finit SOUS LES YEUX, fenêtre Claude au premier plan : « En attente » quelques secondes (2,5 à ~5,5 s), puis rien ; la même chose avec l'Explorateur au premier plan : reste « En attente » | LUE-02 | Premier plan réel + cadence réelle du widget (2 s) + cache (1 s) ; la grâce se VOIT, c'est voulu (Piège 12) | Rester sur une session qui travaille, app au premier plan, jusqu'à la fin du tour ; noter la durée de « En attente » ; recommencer en passant sur l'Explorateur avant la fin du tour |
| Accueil ou conversation Chat de l'app au premier plan pendant la fin d'un tour de la dernière session de code sélectionnée | LUE-02 | Limite CONNUE et écrite (Piège 1) : processus `claude` au premier plan, sans UIA rien ne distingue les vues ; le relevé de phase 27 ne l'a pas vérifiée | Ouvrir l'accueil ou Chat pendant qu'une session de code finit ; noter si elle disparaît « sélectionnée au premier plan » ; consigner l'écart, ne rien « corriger » sans décision |
| Une session répondue qui travaille s'affiche « Réflexion », puis « En attente » à son tour suivant ; une permission vue puis laissée quitte le widget | LUE-05, LUE-01 (D-30-08) | Enchaînement réel réponse → travail → fin de tour ; la décision D-30-08 est à juger à l'usage | Répondre à une session en attente : elle doit rester visible « Réflexion » ; à la fin du tour, « En attente » ; ouvrir une demande de permission dans l'app puis la quitter sans répondre : noter qu'elle disparaît et si c'est gênant |
| Le rapport « Diagnostic… » : pour chaque masquée, le motif et ses instants ; la section « Règle « lue » » dit la session sélectionnée et le premier plan (au moment du rapport, le premier plan est Chronos ou l'éditeur — c'est vrai) | LUE-03, LUE-04 | Rapport produit par l'exe réel sur les données réelles | Menu « Diagnostic… » ; lire les lignes « masquée par treated.json — … » et la section « Règle « lue » » ; vérifier qu'aucune ligne ne dit « un filtre non nommé » |
| Réserve de la phase 27 : retour dans l'app SANS mise à jour de `lastFocusedAt` (16:23:51 → 19:51 inchangé) | LUE-01, LUE-02 | Cas non reproduit par le geste A ; LUE-02 le couvre pour la session sélectionnée | Si une session reste « En attente » alors qu'elle a été lue, relever `lastFocusedAt` (rapport : « focus il y a … ») et la ligne « Premier plan » |

---

## Validation Sign-Off

- [x] Toutes les tâches ont une vérification `<automated>`
- [x] Continuité d'échantillonnage : aucune suite de trois tâches sans vérification automatique
- [x] Pas de vague 0 séparée : chaque manque est couvert par une tâche TDD nommée ci-dessus
- [x] Aucun mode « watch »
- [x] Latence de retour < 30 s
- [x] `nyquist_compliant: true` posé dans le frontmatter

**Approval:** planifié le 2026-09-26 — valeurs mesurées remplies à l'exécution ; complétée en fin de 30-04 (2026-09-26) : les neuf
mutations consignées, 1143 / 0 sur deux exécutions consécutives. Les constats humains restent ceux de « Manual-Only
Verifications », renvoyés à la phase 31.
