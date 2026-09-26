---
phase: 30-la-lecture-fait-dispara-tre
verified: 2026-09-26T00:00:00Z
status: passed
score: 6/6 critères ROADMAP vérifiés, 5/5 exigences LUE mérité(e)s
human_verification:
  - test: "Une session finie, ouverte dans l'app puis quittée, disparaît du widget SANS clic ; noter le délai"
    expected: "Le widget la masque « lue » peu après le focus, sans clic ; le rapport dit « lue : focus à … > attente à … »"
    why_human: "Exige l'app bureau réelle et un vrai changement de focus ; latence d'écriture de lastFocusedAt non bornée (Piège 11)"
    deferred_to: "Phase 31 (constat en production, 30-VALIDATION.md § Manual-Only Verifications)"
  - test: "Un tour qui finit SOUS LES YEUX, fenêtre Claude au premier plan : « En attente » 2,5 à ~5,5 s puis rien ; avec l'Explorateur au premier plan : reste « En attente »"
    expected: "La grâce se voit (voulu) ; sans claude au premier plan, LUE-02 reste inactive"
    why_human: "Premier plan réel + cadence réelle du widget (2 s) + cache (1 s), Piège 12"
    deferred_to: "Phase 31"
  - test: "Accueil ou conversation Chat de l'app au premier plan pendant la fin d'un tour de la dernière session de code sélectionnée"
    expected: "Comportement à observer et consigner, pas à corriger sans décision"
    why_human: "Limite connue et écrite (Piège 1) : processus claude au premier plan, sans UIA rien ne distingue les vues ; non vérifiée par le relevé de phase 27"
    deferred_to: "Phase 31"
  - test: "Une session répondue qui travaille s'affiche « Réflexion », puis « En attente » à son tour suivant ; une permission vue puis laissée quitte le widget"
    expected: "LUE-05 et D-30-08 se comportent comme décidé, jugés à l'usage"
    why_human: "Enchaînement réel réponse → travail → fin de tour"
    deferred_to: "Phase 31"
  - test: "Le rapport « Diagnostic… » : pour chaque masquée, le motif et ses instants ; la section « Règle « lue » » dit la sélection et le premier plan"
    expected: "Aucune ligne ne dit « un filtre non nommé »"
    why_human: "Rapport produit par l'exe réel sur les données réelles"
    deferred_to: "Phase 31"
  - test: "Réserve de la phase 27 : retour dans l'app SANS mise à jour de lastFocusedAt (16:23:51 → 19:51 inchangé)"
    expected: "À observer en production, sans en faire une règle"
    why_human: "Cas non reproduit par le geste A"
    deferred_to: "Phase 31"
---

# Phase 30 : La lecture fait disparaître — Rapport de vérification

**Objectif de phase (ROADMAP.md) :** Une session que l'utilisateur a **lue** quitte le widget d'elle-même, sans
clic — et revient si elle lui redemande quelque chose ; une session dont on ne sait rien de la lecture garde
exactement le comportement v1.6 ; et chaque disparition a une cause écrite dans le diagnostic.
**Vérifié :** 2026-09-26
**Statut :** **passed**
**Mode :** vérification initiale (aucun `30-VERIFICATION.md` antérieur)

## Suite de tests — mesure de l'orchestrateur

```
dotnet test Chronos.sln -c Debug --nologo -v q
Réussi! - échec : 0, réussite : 1143, ignorée(s) : 0, total : 1143, durée : 11 s
```

Rejoué **trois fois** pendant cette vérification (deux avant mutation, une après révocation) : **1143 / 0** à
chaque fois. Conforme à la cible de fin de phase de `30-VALIDATION.md` (« Cible de fin de phase : 0 échec, 1143 »)
et aux quatre SUMMARY (1062 → 1088 → 1110 → 1132 → 1143).

## Mutation rejouée en direct par ce vérificateur (au-delà des neuf consignées aux SUMMARY)

Pour ne pas se fier seulement à la narration des SUMMARY, la mutation **(m6)** a été **rejouée ici, indépendamment** :

1. `sha256sum src/Chronos/Services/SessionMonitor.cs` avant mutation : `1386bdc8d36435e098ae106260db822d862c7e9b2a184794eab12cdb8b1bec5c`
   — **identique** à la valeur consignée dans `30-03-SUMMARY.md` pour ce fichier après révocation de (m3 bis).
2. Retrait de `&& AffichageSessions.EstUneAttente(s.Activity)` dans le filtre « traité » de `SessionMonitor.Inspecter`.
3. `dotnet test … --filter "MoniteurLectureTests|TreatedSessionsTests|InspectionSessionsTests"` → **7 échecs** :
   `Une_session_traitee_qui_travaille_est_visible_Reflexion`, `NET01_la_reponse_est_inscrite_et_la_session_montre_son_travail`,
   `NET03_le_monitor_la_reaffiche_sur_nouvel_episode`, `Lue_puis_repondue_puis_revenue_puis_relue`,
   `Le_cas_e465420e_devient_lisible_en_une_lecture`, `Traitee_et_indeterminee_est_masquee_indeterminee`,
   `Son_prochain_tour_fini_la_ramene_En_attente` — un sur-ensemble des trois exigés par le plan (`Une_session_traitee_…`,
   `NET01_…`, `Le_cas_e465420e_…`), cohérent avec le fait que retirer le garde-fou LUE-05 casse plus de scénarios que le
   minimum exigé.
4. Révocation par `git checkout -- src/Chronos/Services/SessionMonitor.cs` : `git status --short` **vide**, sha256
   **identique** à l'étape 1, suite complète relancée → **1143 / 0**.

Ce test vivant confirme que les neuf mutations consignées aux SUMMARY 30-01 à 30-04 (m1, m2, m4, m7, m3, m6,
m3 bis, m5, m8) ne sont pas une narration : le code réel rougit quand on le mute, et redevient bit-à-bit identique
après révocation.

## Les 6 critères de succès de la ROADMAP, un par un

### Critère 1 — le relevé du 16 h 08 ne se reproduit plus

**Preuve :** `MoniteurLectureTests.Le_releve_de_16_h_08_ne_se_reproduit_plus` (M01), lu intégralement (ligne 232).
Rejoue le scénario EXACT avec les fixtures dérivées de `fin-de-tour-review-ready.json` / `sans-postTurnSummary.json`
(les fixtures réelles de phase 27), un moniteur RÉEL et un lecteur RÉEL — pas de mock du pipeline. Jarvis (fini
13:59:05Z, focus 14:00:39Z) et ADVANCED SHEET (fini 13:56:10Z, focus 14:00:59Z) sont masquées `LueParFocus` avec
leurs deux instants exacts ; une 3ᵉ session finie après son focus (ProjetC, 14:05Z vs focus 14:00Z) reste visible
« En attente » ; une 4ᵉ qui travaille (B) reste visible « Réflexion ». `treated.json` ne porte que les deux épisodes
lus, rien d'autre.
**Statut :** ✓ VERIFIED.

### Critère 2 — réversible (NET-03), magasin existant, pas de réécriture à chaque rafraîchissement

**Preuve :** `SessionTreatmentTracker.Observe` (`src/Chronos/Services/SessionTreatmentTracker.cs` l. 153-184) —
table à six cas lisible en commentaire, une seule écriture par épisode. `DetecteurLectureTests` L04/L05 (sentinelle
de date d'écriture intacte sur cinq cycles) et `MoniteurLectureTests.Treated_json_n_est_pas_reecrit_a_chaque_rafraichissement`
(M12) le prouvent au moniteur réel. `Marquer_traitee_et_archiver_restent_ceux_de_la_v1_6` (M09) prouve qu'Archiver
prime sur une lecture, comme en v1.6.
**Statut :** ✓ VERIFIED.

### Critère 3 — le tour qui finit sous les yeux n'est pas annoncé « En attente », Win32 sans UIA, gardes phase 21 inchangées

**Preuve :** `PremierPlanWin32.cs` — deux `DllImport("user32.dll")` uniquement (`GetForegroundWindow`,
`GetWindowThreadProcessId`), aucune UI Automation. `GardesPerimetreTests` interdit toujours tout type contenant
`Uia` ou finissant par `ForegroundWatch` (lu au texte, l. 31-32) — **non assoupli**. `MoniteurLectureTests.Geste_B_la_session_regardee_est_lue_au_premier_plan`
(M03) et `_avec_explorer_au_premier_plan_reste_en_attente` (M03b) rejouent le geste B exact du relevé de phase 27
(fin de tour 20:34:04.328 / 18:34:04.328 UTC, focus 20:30:44.976, `claude` au premier plan depuis 20:33:27) : lue
à 18:34:06.828Z, visible à 18:34:06.827Z (mutation m4 confirmée : `>=` strict sur la borne).
**Statut :** ✓ VERIFIED.

### Critère 4 — aucun masquage sans cause

**Preuve :** `DiagnosticService.LibelleMasquage` (l. 635-650) couvre les six valeurs de `MotifMasquage` ; `R08`
(`Aucun_motif_de_masquage_n_est_sans_libelle`) parcourt `Enum.GetValues<MotifMasquage>()` et vérifie qu'aucune ne
rend « un filtre non nommé ». Extrait de rapport réel capturé (30-04-SUMMARY) : « masquée par treated.json — lue :
focus à 16:00:39 > attente à 15:59:05 » et « sélectionnée au premier plan depuis 39 s (claude) — attente à
20:34:04 ». `docs/hooks-contract.md` §3 dit désormais « quitte le widget de trois façons » (vérifié par grep,
0 occurrence de « deux raisons seulement »), sous garde croisée code ↔ document (`ContratHooksDocumenteTests.Le_paragraphe_3_dit_les_trois_facons_de_quitter_le_widget_et_la_regle_lue`, lue intégralement).
**Statut :** ✓ VERIFIED.

### Critère 5 — pas de faux masquage sans métadonnées

**Preuve :** `MoniteurLectureTests.Sans_metadonnees_lisibles_la_lecture_est_celle_de_la_v1_6` (M06, théorie sur
dossier-absent / fichier-illisible / focus-illisible) compare, cycle par cycle, un moniteur « avec la règle » à un
moniteur « v1.6 » sans lecteur ni sonde : projections égales aux deux. `Sans_cliSessionId_la_regle_ne_s_applique_pas`
(M05) et `Le_scenario_des_478_minutes_reste_visible_avec_la_regle_lue` (M08, le scénario fondateur de
`TreatedSessionsTests` rejoué avec la règle branchée) confirment. Le rapport dit l'inactivité (R05, R07 :
« indisponible (…) — LUE-02 inactive, LUE-01 seule » ; « inconnue — source app-bureau absente : règle « lue »
inactive (comportement v1.6) »).
**Statut :** ✓ VERIFIED.

### Critère 6 — une session qui travaille est toujours visible (LUE-05)

**Preuve :** filtre `SessionMonitor.cs` l. 242 : `AffichageSessions.EstUneAttente(s.Activity)` ajouté à la
condition de masquage — confirmé par la mutation (m6) rejouée en direct ci-dessus (7 échecs à son retrait, 0 après
révocation). `MoniteurLectureTests` V01-V04 et M11 (`Lue_puis_repondue_puis_revenue_puis_relue`, cycle complet
lue → répondue-visible → revenue → re-lue) le prouvent de bout en bout. Trois tests v1.6 adaptés et un renommé
(`NET01_le_monitor_masque_apres_reponse` → `NET01_la_reponse_est_inscrite_et_la_session_montre_son_travail`),
tenus vivants (aucun supprimé) : lus intégralement dans `TreatedSessionsTests.cs` et `InspectionSessionsTests.cs`,
conformes à la narration du SUMMARY.
**Statut :** ✓ VERIFIED.

## Exigences LUE-01 à LUE-05 — chaque coche méritée

| REQ | REQUIREMENTS.md | Preuve code | Preuve test | Verdict |
|---|---|---|---|---|
| LUE-01 | Une attente antérieure au focus est traitée automatiquement, réversible (NET-03) | `SessionTreatmentTracker.Lue` l. 200-201 (`attente < focus`, strict — D-30-03) ; table à six cas l. 153-184 | L01-L08, L20-L24 ; M01, M02, M08, M11, M12 | ✓ mérité |
| LUE-02 | Session sélectionnée + claude au premier plan ≥ grâce (2,5 s) ⇒ lue ; Win32 sans UIA | `PremierPlanWin32.cs` (deux P/Invoke, `Classer` par égalité ordinale) ; `Lue` l. 202-205 (`>=`, D-30-02) | L09-L15, L21 ; P01-P09 ; M02-M04, M07 ; mutation (m4), (m7) | ✓ mérité |
| LUE-03 | Le diagnostic distingue « lue » de « répondue », nomme la cause | `DiagnosticService.LibelleMasquage` (six motifs nommés, aucun replié) ; §3 du contrat réécrit | R01-R04, R08 ; garde croisée du §3 ; mutation (m8) | ✓ mérité |
| LUE-04 | Sans métadonnées de l'app, comportement v1.6 strict | `Lue` l. 198 (`l is null \|\| !DernierFocus.TryGetValue(...)` ⇒ `return null`) ; `ContexteLecture` nul sans `DossierTrouve` (`SessionMonitor.cs` l. 204) | M05, M06×3, M07, M14 ; R05-R07 | ✓ mérité |
| LUE-05 | Le masquage « traité » ne s'applique qu'à une attente | `SessionMonitor.cs` l. 242 (`EstUneAttente`) | V01-V04, M11 ; mutation (m6, rejouée en direct) | ✓ mérité |

**Aucun orphelin** : les cinq requirements LUE-01..05 sont déclarés dans les frontmatters des quatre plans
(30-01 à 30-04) et couvrent exactement les cinq entrées de `REQUIREMENTS.md` mappées à la Phase 30.

## Artifacts (niveaux 1-3 : existe, substantiel, câblé)

| Artifact | Existe | Substantiel | Câblé | Statut |
|---|---|---|---|---|
| `src/Chronos/Services/SessionTreatmentTracker.cs` | ✓ (208 lignes) | ✓ (`Observe` à 3 paramètres, `CauseDe`, table à six cas) | ✓ (appelé par `SessionMonitor.Inspecter` l. 210) | ✓ VERIFIED |
| `src/Chronos/Services/PremierPlanWin32.cs` | ✓ (162 lignes, nouveau) | ✓ (P/Invoke réels, cache, trou, verrou) | ✓ (`App.xaml.cs` l. 267 singleton, `SessionMonitor` l. 199-203) | ✓ VERIFIED |
| `src/Chronos/Services/LecteurAppBureau.cs` (Selection) | ✓ | ✓ (`SessionSelectionnee`, calcul sur `focusServis` avant regroupement) | ✓ (`SessionMonitor.cs` l. 207 `appBureau.Selection?.CliSessionId`) | ✓ VERIFIED |
| `src/Chronos/Services/LectureSessions.cs` (motifs, `PremierPlan`) | ✓ | ✓ (3 motifs ajoutés en fin, `CauseTraitement`, `ContexteLecture`) | ✓ (posé par `SessionMonitor`, lu par `DiagnosticService`) | ✓ VERIFIED |
| `src/Chronos/Services/DiagnosticService.cs` (`LibelleMasquage`, `DecrireRegleLue`) | ✓ | ✓ (six libellés nommés, section complète) | ✓ (`Inspecter(` unique, `m.Cause`, `lecture.PremierPlan`) | ✓ VERIFIED |
| `src/Chronos/App.xaml.cs` (câblage `IPremierPlan`) | ✓ | ✓ | ✓ (`AddSingleton<IPremierPlan>`, `premierPlan:` nommé) | ✓ VERIFIED |
| `docs/hooks-contract.md` §3 | ✓ | ✓ (« trois façons », `GraceLecture`, LUE-05) | ✓ (garde croisée code ↔ document, verte) | ✓ VERIFIED |

## Data-flow (niveau 4)

La chaîne `SessionMonitor.Inspecter` → `_premierPlan.Lire(now)` → `ContexteLecture` → `SessionTreatmentTracker.Observe`
→ `TreatedStore.Set/Remove` → filtre `EstUneAttente` → `SessionMasquee.Cause` → `DiagnosticService.LibelleMasquage`
est tracée de bout en bout par `MoniteurLectureTests.La_lecture_porte_l_etat_du_premier_plan_vu_par_le_moniteur`
(M13, `Assert.Same(sonde.Etat, lecture.PremierPlan)`, un seul appel) et par les extraits de rapport réellement
rendus (30-04-SUMMARY, capturés par injection temporaire puis révoqués, sha256 vérifié). Aucune donnée statique ou
vide masquée en chemin : `ContexteLecture` est authentiquement nul sans dossier app-bureau (LUE-04), jamais
construit avec des valeurs par défaut qui simuleraient une lecture.
**Statut :** ✓ FLOWING.

## Anti-patterns

Aucun `TODO`/`FIXME`/`PLACEHOLDER` trouvé dans les fichiers de production modifiés par la phase. Aucun
`return null`/`return new()` silencieux masquant un stub — chaque « je ne sais pas » (Indisponible, NonBranche,
inconnue, aucune) est une valeur NOMMÉE et testée (R08 : garde anti-« filtre non nommé »). Les 30-01-SUMMARY et
30-03-SUMMARY documentent honnêtement deux « Known Stubs » temporaires intra-phase (le libellé des 3 nouveaux
motifs entre 30-01/30-03 et 30-04) : ils sont résolus dans le même SUMMARY qui les nomme, avant la fin de la phase
— aucun ne subsiste dans le code final vérifié ici.

## Requirements Coverage (Step 6)

Voir tableau ci-dessus. Traçabilité confirmée dans `.planning/REQUIREMENTS.md` (LUE-01 à LUE-05, ligne
« Traceability », toutes `Complete`, Phase 30) et dans `.planning/ROADMAP.md` (« Couverture des exigences » :
30 → LUE-01..05, 5 requirements, aucun doublon avec une autre phase).

## Gardes existantes — non assouplies

Vérifié au texte et par exécution : `GardesPerimetreTests` (Uia/ForegroundWatch toujours interdits, l. 31-32),
`ServicesLayerPurityTests`, `CompositionRootTests` (miroir DI étendu avec la sonde, jamais appelé),
`NormalisationUniqueTests`, `ContratHooksDocumenteTests` (garde croisée §3), `GardesDoctrineTests`. Toutes vertes
dans la suite complète 1143/0. `git diff --stat` des `ViewModels`/`Views`/`Resources` vide sur les quatre plans
(consigné aux SUMMARY, aucune UI touchée — cohérent avec l'objectif de phase qui n'ajoute ni source ni libellé).

## Human Verification Required

Toutes renvoyées explicitement à la **Phase 31** par `30-VALIDATION.md` § Manual-Only Verifications (voir
frontmatter `human_verification` ci-dessus) : le constat en production d'une disparition sans clic, la latence de
`lastFocusedAt`, la grâce visible de 2,5 à ~5,5 s, et le cas non couvert de l'accueil/Chat au premier plan (Piège 1,
limite connue et écrite, non parée par design). Ces points sont correctement différés — la Phase 30 les documente,
ne les invente pas comme résolus, et le tableau de la ROADMAP place leur constat en Phase 31 ("l'utilisateur
vérifie le tableau en trois lignes sur sa machine").

## Gaps Summary

Aucun gap. Les six critères de succès de la ROADMAP sont vérifiés par du code et des tests réels rejoués (pas
seulement narrés) ; les cinq exigences LUE sont méritées ; la mutation (m6) a été rejouée indépendamment par ce
vérificateur avec succès (rouge attendu, révocation bit-exacte prouvée par sha256 et `git status` vide) ; la suite
complète est verte trois fois de suite à 1143/0. Les seuls éléments non vérifiables automatiquement (constat en
production, sensations de latence, cas limite de l'accueil de l'app) sont correctement déférés à la Phase 31 sans
bloquer cette phase.

---
*Vérifié le 2026-09-26*
*Vérificateur : Claude (gsd-verifier)*
