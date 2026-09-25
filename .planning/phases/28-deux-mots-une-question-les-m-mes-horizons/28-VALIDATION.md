---
phase: 28
slug: deux-mots-une-question-les-m-mes-horizons
status: complete
nyquist_compliant: true
wave_0_complete: n/a
created: 2026-09-25
validated: 2026-09-25
---

# Phase 28 — Validation Strategy

> Carte **posée à la planification** (2026-09-25), à **remplir de valeurs mesurées** au fil des quatre plans. Les
> colonnes « Attendu » viennent des plans ; les colonnes « Mesuré » restent vides tant qu'aucune exécution ne les a
> produites. **Ne rien y écrire qui n'ait été mesuré** — une case remplie « d'après le plan » est exactement la faute
> que la doctrine de ce projet interdit.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`, `[Collection("XAML WPF")]`), Microsoft.NET.Test.Sdk 17.11.1 — `tests/Chronos.Tests` (`net8.0-windows`) |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` (attributs `CheminSourcesChronos`, `CheminDocsChronos` ; `InternalsVisibleTo Chronos.Tests` côté `src/Chronos/Chronos.csproj`) |
| **Quick run command** | `dotnet test Chronos.sln -c Debug --nologo -v q --filter "FullyQualifiedName~AffichageSessionsTests\|FullyQualifiedName~ArbitrageSessionsTests\|FullyQualifiedName~InspectionSessionsTests\|FullyQualifiedName~TranscriptQuestionTests\|FullyQualifiedName~TranscriptInstantSignalTests\|FullyQualifiedName~TranscriptSousAgentsTests\|FullyQualifiedName~SessionsTests\|FullyQualifiedName~HorizonsSessionsTests\|FullyQualifiedName~LibellesSessionsTests\|FullyQualifiedName~ContratHooksDocumenteTests\|FullyQualifiedName~SessionStylesBindingTests\|FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~GardesPerimetreTests"` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` |
| **Estimated runtime** | ~8 s de tests, ~20 s avec la compilation (mesure de la recherche : 896 tests, 8 s) |
| **Baseline d'entrée de phase** | **896 verts / 0 échec** (mesurée le 2026-09-25 par la recherche et par l'orchestrateur) — et non 889 : les 7 tests du cran 1 de R3 ont été ajoutés après l'audit v1.6 |
| **Cible de fin de phase** | **0 échec, aucun test supprimé.** Égalité, pas plancher : `total = total mesuré du plan précédent + cas ajoutés`, tout écart (même positif) justifié nominativement au SUMMARY. |
| **Attendus indicatifs** | 28-01 : +9 (T1) +7 (T2) = **912** seul ; 28-02 : +8 (T1) +4 (T2) = **908** seul ; **924** après la vague 1 ; 28-03 : +4 (T1) +7 (T2) = **935** ; 28-04 : +8 (T1) +4 (T2) +0 (T3) = **947** |
| **Renommages annoncés** | **2**, tous en 28-02 : `Une_deduction_ne_passe_jamais_devant_une_observation` → `A_l_ecran_une_attente_deduite_passe_devant_la_reflexion` (T1) ; `Un_hook_deduit_et_un_transcript_travaillant_du_MEME_instant_sont_departages_par_la_source` → `Un_hook_deduit_et_un_transcript_en_tour_fini_du_MEME_instant_sont_departages_par_la_source` (T2, Piège 4 : reformulé, pas supprimé) |
| **Tests adaptés sans renommage** | 28-02 : `Chaque_etat_a_son_rang_d_urgence`, `Une_session_deduite_est_visible_ambre_et_dit_sa_deduction`, `Le_compteur_d_attente_inclut_la_deduction`, `Une_activite_illisible_reste_inconnue_et_non_deduite`, `SessionStylesBindingTests.Vm()` et `Le_style_Pastilles_n_a_plus_qu_un_seul_separateur_visible_par_session` ; 28-03 : `Chaque_etat_a_son_libelle`, `Le_widget_affiche_ce_que_la_couche_neutre_produit`, `Une_session_deduite…` (mot puis `IsDeduced`), `Le_silence_apres_travail_produit_une_attente_DEDUITE_jamais_un_tour_fini` (l. 410), `Le_rapport_decrit_les_sessions_du_moniteur_qu_on_lui_donne`, `Le_cas_e465420e_se_lit_en_une_ligne_du_rapport`, `Un_desaccord_nomme_la_source_retenue_la_source_ecartee_et_l_ecart_d_age` ; 28-04 : `Transcript_trop_ancien_est_ignore` (30 min → 8 h 01) |
| **Total mesuré après 28-01** | **912 / 0 échec**, deux exécutions consécutives, sur une copie ISOLÉE (`git archive 8542274` + les seuls fichiers du plan) — relevé de 28-01-SUMMARY.md ; l'arbre partagé avec 28-02 était alors en rouge volontaire |
| **Total mesuré après 28-02** | **924 / 0 échec**, deux exécutions consécutives, arbre partagé, les deux plans de la vague 1 commités (`24a0957`) — relevé de 28-02-SUMMARY.md ; part propre de 28-02 (896 + 12 = 908) calculée, non mesurée isolément |
| **Total mesuré après 28-03** | **935 / 0 échec**, deux exécutions consécutives (924 → 928 après T1 `bf63366` → 935 après T2 `fc9c99d`) |
| **Total mesuré après 28-04 (deux exécutions)** | **947 / 0 échec** et **947 / 0 échec** (935 → 943 après T1 `62d6a0c` → 947 après T2 `331a33e`). RED de T1 : 937 / 943 (6 rouges nommés au SUMMARY) |

---

## Sampling Rate

- **Après chaque tâche :** la commande rapide ci-dessus (ou le filtre du `<verify>` de la tâche).
- **Après chaque vague :** `dotnet test Chronos.sln -c Debug --nologo -v q`.
- **Avant `/gsd:verify-work` :** suite complète verte, **deux exécutions consécutives** (les `[WpfFact]` de la phase le
  justifient — précédent BAML 16-03), et les **deux contrôles de mutation** consignés.
- **Latence maximale de retour :** ~20 s (suite complète avec compilation).

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 28-01-01 | 01 | 1 | LIB-02 | unit sur fixture RÉELLE + arbitrage pur (Piège 3 documenté) | `dotnet test … --filter "FullyQualifiedName~TranscriptQuestionTests"` | ✅ créé (`8542274`, + 2 fixtures, README) | ✅ green (9 cas) |
| 28-01-02 | 01 | 1 | SIL-01 (D-28-01) | unit, racines temporaires, `now` injecté | `dotnet test … --filter "FullyQualifiedName~TranscriptInstantSignalTests\|FullyQualifiedName~TranscriptQuestionTests\|FullyQualifiedName~TranscriptSousAgentsTests"` | ✅ créé (`243a535`) | ✅ green (912 isolé ×2) |
| 28-02-01 | 02 | 1 | LIB-04 (R4) | unit pur (720 permutations, corpus rang 3) + garde texte + garde croisée doc ↔ code + **mutation** | `dotnet test … --filter "FullyQualifiedName~ArbitrageSessionsTests\|FullyQualifiedName~AffichageSessionsTests\|FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~ContratHooksDocumenteTests"` | ✅ classes existantes, étendues (`147f41a`) | ✅ green (913) |
| 28-02-02 | 02 | 1 | LIB-01, LIB-03 | unit + intégration moniteur/rapport + WPF (comptes) | `dotnet test … --filter "FullyQualifiedName~InspectionSessionsTests\|FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~AffichageSessionsTests\|FullyQualifiedName~SessionStylesBindingTests"` | ✅ classes existantes, étendues (`24a0957`) | ✅ green (924 ×2) |
| 28-03-01 | 03 | 2 | LIB-01, LIB-03 | unit + gardes texte (anciens libellés, texte d'activation) + garde croisée §3 ↔ producteur | `dotnet test … --filter "FullyQualifiedName~AffichageSessionsTests\|FullyQualifiedName~LibellesSessionsTests\|FullyQualifiedName~ContratHooksDocumenteTests\|FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~InspectionSessionsTests"` | ✅ `LibellesSessionsTests` créé (`bf63366`) | ✅ green (928) |
| 28-03-02 | 03 | 2 | LIB-01, LIB-03 | WPF 8 styles × 9 thèmes + gardes texte (fantôme, infobulles) + réflexion (bindings des deux ViewModels) | `dotnet test … --filter "FullyQualifiedName~SessionStylesBindingTests\|FullyQualifiedName~LibellesSessionsTests\|FullyQualifiedName~AffichageSessionsTests\|FullyQualifiedName~GardesPerimetreTests"` | ✅ étendus (`fc9c99d`) | ✅ green (935 ×2) |
| 28-04-01 | 04 | 3 | SIL-01 | intégration (vrai moniteur, vraie source, racine temporaire, `now` injecté) | `dotnet test … --filter "FullyQualifiedName~HorizonsSessionsTests\|FullyQualifiedName~SessionsTests\|FullyQualifiedName~InspectionSessionsTests\|FullyQualifiedName~TreatedSessionsTests"` | ✅ `HorizonsSessionsTests` créé (`62d6a0c`) | ✅ green (943 ; RED 6 rouges) |
| 28-04-02 | 04 | 3 | SIL-01 | gardes (chaîne, câblage, silence en un point, doc ↔ code) + **mutation** | `dotnet test … --filter "FullyQualifiedName~HorizonsSessionsTests\|FullyQualifiedName~ContratHooksDocumenteTests"` | ✅ étendus (`331a33e`) | ✅ green (947 ×2 ; mutation jouée) |
| 28-04-03 | 04 | 3 | SIL-01 | mesure réelle, lecture seule (console hors dépôt sur la DLL livrée) | `dotnet build Chronos.sln -c Release --nologo -v q` puis la console de mesure | n/a (aucun fichier du dépôt hors cette carte) | ✅ mesuré (médiane 28,2 / 25,7 ms < 50 ms) |

*Status : ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

**Pas de vague 0 séparée — et c'est voulu.** Chaque tâche de code est TDD (`tdd="true"`) : elle écrit ses tests
d'abord, constate le rouge, puis livre le code, dans la même tâche. Les membres nécessaires à la compilation du rouge
sont posés en SQUELETTES (précédent 18-01 : un rouge de compilation ne dit rien). Les manques relevés par la recherche
sont couverts ainsi :

- [x] `tests/Chronos.Tests/TestData/TranscriptQuestion/question-en-suspens.jsonl`, `question-repondue.jsonl`, `README.md`
      — lignes RÉELLES réduites et anonymisées (28-01, tâche 1)
- [x] `tests/Chronos.Tests/TranscriptQuestionTests.cs` — LIB-02 (28-01, tâches 1 et 2)
- [x] `tests/Chronos.Tests/TranscriptInstantSignalTests.cs` — D-28-01 (28-01, tâche 2)
- [x] extension de `ArbitrageSessionsTests` — corpus rang 3, non-vacuité, rang figé (28-02, tâche 1)
- [x] extension de `ContratHooksDocumenteTests` — deux ordres (28-02), §3 ↔ producteur (28-03), horizons (28-04)
- [x] `tests/Chronos.Tests/LibellesSessionsTests.cs` — anciens libellés, texte d'activation, fantôme, infobulles,
      bindings des deux ViewModels (28-03)
- [x] `tests/Chronos.Tests/HorizonsSessionsTests.cs` — scénarios SIL-01, chaîne, câblage, silence en un point (28-04)

Aucun framework à installer.

---

## Contrôles de mutation (portés par les plans, à consigner ici)

| Mutation | Plan | Attendu | Mesuré |
|---|---|---|---|
| `AffichageSessions.Urgence` au nouvel ordre SANS `RangArbitrage` (l'arbitrage lit encore l'ordre d'écran) | 28-02 T1, étape 2 | `Remonter_la_deduction_a_l_ecran_ne_change_aucun_arbitrage` rouge sur `r1=Working@`, et la ligne (Working, WaitingDeduced) de `Le_rang_d_arbitrage_reste_celui_de_la_phase_24` | **Conforme** (relevé de 28-02-SUMMARY.md) : `Remonter_la_deduction…` rouge (`Not found: "r1=Working@"`, lu `r1=WaitingDeduced@2026-09-12T13:28:00.000`) ; `Le_rang_d_arbitrage_reste_celui_de_la_phase_24(a: Working, b: WaitingDeduced, gagnant: Working)` rouge (`Expected: Working / Actual: WaitingDeduced`). Jamais commitée. Seconde mutation (sens inverse, après le GREEN) : 3 rouges, révoquée par sha256 |
| `HorizonsSessions.Abandon` porté à 30 h | 28-04 T2, étape 3 | `La_chaine_des_horizons_tient` rouge ; révoquée, `git diff` vide | **Conforme** : 2 rouges sur 11 — `La_chaine_des_horizons_tient` (« Abandon (1.06:00:00) < RetentionTraitees (1.00:00:00) défait ») et `Transcript_seul_de_8_h_01_est_absent` (le scénario de borne, par le comportement). Révoquée : sha256 `5273a099…` identique avant/après, `git diff` vide |

---

## Mesures sur la vraie machine (28-04, tâche 3 — lecture seule)

Méthode : console `net8.0-windows` jetable, HORS dépôt (bloc-notes de session, supprimée après), référençant la
`Chronos.dll` Release livrée (`src/Chronos/bin/Release/net8.0-windows/`, construite après `331a33e` ; la DLL a
rendu `Silence=00:20:00, Abandon=08:00:00`). `new TranscriptSessionSource()` sur le vrai `~/.claude/projects`,
1 appel froid puis 30 chauds de `Read(DateTimeOffset.UtcNow)` au `Stopwatch` ; puis `new SessionMonitor(<dossier de
hooks VIDE temporaire>, source, new ArchiveStore(<fichier temporaire>))`. Deux exécutions, le 2026-09-25 à
**22 h 06** et **22 h 08** (heure locale, UTC+2).

| Grandeur | Recherche (copie fidèle, 2026-09-25 ~20 h 10) | Mesuré avec le code livré (22 h 06 ; 22 h 08) |
|---|---|---|
| `.jsonl` énumérés | 1 425 (dont 1 342 sous-agents) | **1 474**, dont **1 369** écartés par le chemin (sous-agents) ; 72 dossiers distincts contenant un `.jsonl` (compte non comparable aux « 201 dossiers » de la recherche, dont la définition n'est pas écrite) |
| Candidats après pré-filtres | 18 à 8 h (13 principaux + 5 `wf_*/journal.jsonl`) ; 1 à 15 min | **18** à 8 h (13 principaux + 5 `subagents/workflows/wf_*/journal.jsonl`, Piège 11) ; **2** à 15 min |
| Énumération + pré-filtres seuls (10 appels) | 12,7 ms (dans le cycle) | médiane **14,8 ms** ; **13,0 ms** |
| Cycle chaud, médiane | 19,6 ms (8 h) — 12,0 ms (15 min) | **28,2 ms** ; **25,7 ms** |
| Cycle chaud, p90 / max | 21,5 / 22,6 ms | **30,4 / 32,5 ms** ; **26,9 / 29,1 ms** (min 25,1 ; 23,9) |
| Premier appel (froid) | 21,0 ms | **63,3 ms** ; **42,4 ms** (premier chargement de la DLL et JIT inclus) |
| `SessionMonitor.Inspecter`, hooks vides (10 appels) | non mesuré | médiane **28,9 ms** (max 31,3) ; **26,0 ms** (max 28,6) |
| Sessions rendues par la source | 5 visibles (datation par le dernier message) | **4** : `88677186` tour fini 4 min, `11456cab` tour fini 11 min, `dae57d29` travail 20 min, `c17a1b03` tour fini 30 min — dont **2** seulement de moins de 15 min |
| Sessions visibles par le moniteur, par mot | non mesuré | **4 visibles, 0 masquée** : « En attente » **3**, « En attente ? » **1** (`dae57d29`, travail muet depuis 20-21 min), « Réflexion » **0** — face à `MaxSessions` = 12 |
| Décision cache | pas de cache (< 50 ms) | **PAS DE CACHE** : médiane mesurée 28,2 ms puis 25,7 ms, **sous le seuil de 50 ms** (1,8 à 1,9 fois dessous) |

**Lecture des chiffres.**
- **Le coût est plus élevé que dans la recherche (+6 à +9 ms de médiane), et reste sous le seuil.** L'écart n'a pas été
  décomposé ; deux causes plausibles, non prouvées : le code livré classe TOUS les candidats avant la limite (18,
  contre 12 lus par la copie de la recherche, qui s'arrêtait à `MaxSessions`), et 49 fichiers de plus sont énumérés.
- **La datation par le dernier message (D-28-01) tient sur les vraies données.** 9 des 13 transcripts principaux
  candidats ont été écrits il y a 308 min, c'est-à-dire à 16 h 58 (la fermeture de l'app bureau du relevé) ; leur
  dernier message date de 8 h 12 à plus de deux jours (vérifié indépendamment, en lecture seule, par `tail -c 65536`
  + `grep` du dernier `timestamp` user/assistant). **Aucun** n'est rendu. Datés par l'écriture, les neuf seraient
  revenus « En attente ».
- **Le trou §9.1 se voit sur la vraie machine.** À la fenêtre de 15 min, `c17a1b03` (tour fini il y a 30 min) et
  `dae57d29` (travail muet depuis 20 min) auraient disparu ; elles sont affichées, « En attente » et « En attente ? ».
- **Population** : 4 lignes, loin des 12 ; ni hauteur maximale ni ascenseur à revoir (Piège 13).

**Lecture seule, vérifiée.** `~/.claude/settings.json` : date d'écriture **1790327276** (taille 3 384) avant (22 h 05)
et après (22 h 08) la mesure. `%APPDATA%\Chronos` (vue de ce processus) : `archived.json` 1789277818, `treated.json`
1789295131, `settings.json` 1789277818, `sessions/` 1790361035 — identiques avant et après. Dossier de mesure et
dossier de hooks temporaire supprimés. L'overlay `Chronos-v3.1.0.exe` n'a été ni lancé ni arrêté.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| La galerie montre les trois mots sur les 8 tuiles, « En attente ? » atténuée mais lisible, plus aucun fantôme, sous-titre à jour | LIB-01, LIB-03 | Rendu visuel ; l'agent ne lance ni l'overlay ni la galerie (une instance 3.1.0 tourne en production). Le test WPF 8 × 9 couvre mots, info-bulles, troncature et tailles, pas l'œil | **À vérifier en phase 31** (28-03 livré, non vu à l'œil). Lancer `Chronos.exe --sessions` (build Debug) ; vérifier chaque tuile ; survoler une session de chaque style : info-bulle « projet — mot » |
| Les 9 thèmes à l'œil | LIB-03 | Contraste et lisibilité de l'atténuation à 0,7 sur fond de thème | Facultatif : galerie, un thème à la fois depuis les réglages. Couvert mécaniquement par `Les_trois_mots_se_lisent_sur_les_8_styles_et_les_9_themes` |
| `PermissionRequest` part-il pour `AskUserQuestion` (fait externe MEDIUM, Piège 3) ? | LIB-02 | Dépend du comportement de Claude Code sur la version de l'utilisateur ; non observable sans une question réelle à l'écran | **Phase 31** (constat en production) : pendant qu'une question `AskUserQuestion` est affichée dans l'app, lire en lecture seule `%APPDATA%\Chronos\sessions\<id>.json` ; attendu `"activity":"WaitingAttention","reason":"PermissionRequest"`. S'il porte `Working` / `PreToolUse`, la session s'affichera « Réflexion » (limite écrite par `Sans_PermissionRequest_le_battement_d_une_question_gagne_par_fraicheur`) — rouvrir alors l'événement porteur, pas le transcript |
| Coût et population à 8 h avec le code livré | SIL-01 | Dépend du disque réel de l'utilisateur | **Fait** par 28-04 tâche 3 (console hors dépôt, lecture seule, deux exécutions) ; valeurs reportées dans le tableau ci-dessus |

---

## Hors périmètre, noté pour la suite

- **`SessionStart` avec `source = resume`** (27-RELEVE.md, conséquence 2 : « une reprise n'est pas un travail ») :
  le relevé la place « phase 28 ou 30 » ; elle touche le câblage des hooks (`SessionHookProcessor`), pas le
  vocabulaire ni les horizons. **Non planifiée ici** ; à porter en phase 30, où la règle « lue » dépend de la fiabilité
  du `Stop`.
- **Piège 11** (`subagents/workflows/wf_*/journal.jsonl` laissés passer par le pré-filtre) : coût seulement
  (~0,6 ms par fichier, aucun emplacement consommé) ; durcissement facultatif non planifié.

---

## Validation Sign-Off

- [x] Toutes les tâches ont un `<automated>` verify
- [x] Continuité d'échantillonnage : aucune suite de 3 tâches sans vérification automatique
- [x] La vague 0 est couverte (tâches TDD qui créent leurs tests et fixtures)
- [x] Aucun mode « watch »
- [x] Latence de retour < 28 s
- [x] `nyquist_compliant: true` posé dans le frontmatter

**Approval:** planifiée le 2026-09-25 ; exécutée le 2026-09-25 — totaux (947 / 0 ×2), deux contrôles de mutation et
mesures réelles consignés ci-dessus. Restent MANUELLES, portées en phase 31 : la galerie à l'œil, les thèmes à l'œil, et
`PermissionRequest` pour `AskUserQuestion`.
