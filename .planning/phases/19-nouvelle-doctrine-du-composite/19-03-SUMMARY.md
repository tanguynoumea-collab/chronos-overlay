---
phase: 19-nouvelle-doctrine-du-composite
plan: 03
subsystem: services
tags: [doctrine, di, memoisation, paresse, cout-disque, recalibrage-hebdo, exa-02, exa-05, del-03, del-04]

# Dependency graph
requires:
  - phase: 19-nouvelle-doctrine-du-composite
    plan: "19-01"
    provides: "WindowState.CapturedAt rempli par les quatre providers exacts, LastExactStore.UnExactADejaEteObtenu, SourceActiviteMemoisee (livree NON cablee), FakeTranscriptActivitySource"
  - phase: 19-nouvelle-doctrine-du-composite
    plan: "19-02"
    provides: "DoctrineFraicheur (Statuer / ABesoinDuJournal / LimiteAge), ProvenanceReleve, WindowState.Provenance + TokensDepuisReleve, UsageSnapshot.UnExactADejaEteObtenu"
  - phase: 18-source-en-tetes
    provides: "Garde de POSITION de la sonde — test de comportement a ne pas casser en silence"
provides:
  - "La doctrine est APPELEE en production : LastExactUsageProvider est devenue la couche de doctrine"
  - "Lecture PARESSEUSE du journal d'activite : zero passe disque en regime nominal"
  - "UNE SEULE passe disque pour les DEUX fenetres degradees, jamais deux"
  - "Conversion de l'IOException du memoiseur en branche « indisponible » : aucune exception ne remonte"
  - "Le bit EXA-05 remonte jusqu'au snapshot, avec null — jamais false — quand le magasin est muet"
  - "SourceActiviteMemoisee enregistree dans le graphe DI (production ET conteneur miroir)"
  - "Un resets_at connu n'est plus ecrase par une synthese d'ancre (bug latent WeeklyRecalibration)"
affects: [19-04, 19-05, 20-honnetete-visible]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "La doctrine vit dans le DECORATEUR DE TETE, seule couche detenant simultanement horloge, magasin et source d'activite"
    - "Asymetrie assumee « with » en tete de chaine / « new » dans le composite, documentee des DEUX cotes"
    - "Predicat de paresse consulte AVANT de payer l'E/S, et un compteur de passes en fait une assertion de conception"
    - "Un 4e argument de constructeur OBLIGATOIRE (sans valeur par defaut) plutot qu'un enregistrement DI optionnel : le compilateur est le seul garde-fou qui ne s'oublie pas"

key-files:
  created: []
  modified:
    - src/Chronos/Services/LastExactUsageProvider.cs
    - src/Chronos/Services/WeeklyRecalibration.cs
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - tests/Chronos.Tests/LastExactUsageProviderTests.cs
    - tests/Chronos.Tests/WeeklyRecalibrationTests.cs

key-decisions:
  - "La panne du magasin du test 7 est une panne DURE (dossier parent qui est un fichier) et non un JSON invalide : LastExactStore est tolerant PAR CONSTRUCTION, donc un fichier corrompu lui fait repondre « false » — une affirmation — au lieu de « je ne sais pas »."
  - "Le reset serveur du test hebdo est pose a +5 j et non a +3 j : l'ancre de la classe synthetise precisement « now + 3 j », donc la valeur du plan rendait le test vert MEME avec l'ancienne garde."
  - "Etape RED jouee contre un squelette compilable (ancien corps + nouvelle signature) : un commit non compilable rendrait dotnet test entierement non invocable (precedent 18-01)."
  - "EXA-04 et EXA-05 restent Pending : leur moitie visible (« au moins X % », invite a se connecter) est le plan 19-04."

patterns-established:
  - "Falsifiabilite JOUEE : trois mutations reelles du code de production, echec constate et NOMME, revocation prouvee par git diff vide."
  - "Un compteur de passes disque dans un faux transforme une contrainte de COUT mesuree en assertion permanente."

requirements-completed: [EXA-02, DEL-03, DEL-04]

# Metrics
duration: 38min
completed: 2026-09-12
---

# Phase 19 Plan 03: La doctrine branchée en tête de chaîne Summary

**La doctrine de fraîcheur, écrite et prouvée au plan 19-02 mais que rien n'appelait, décide désormais réellement de ce que l'utilisateur voit : `LastExactUsageProvider` est devenue la couche de doctrine, elle statue par fenêtre avec un seul journal partagé, ne paie la passe de transcripts (2,7-3,2 s / 536 Mo) que si au moins une fenêtre en a besoin — zéro passe en régime nominal, une seule pour deux fenêtres dégradées — et le mémoïseur du plan 19-01 est enfin enregistré dans le graphe DI ; au passage, un `resets_at` réel porté par une fenêtre hebdo démotée n'est plus écrasé par une synthèse d'ancre.**

## Performance

- **Duration:** 38 min (dont ~11 min de suites complètes : 5 passes × ~2 min 8)
- **Started:** 2026-09-12T07:05:00Z
- **Completed:** 2026-09-12T07:43:00Z
- **Tasks:** 3 / 3 (toutes en TDD)
- **Files modified:** 6 (0 créé, 6 modifiés)
- **Tests:** 680 → **688** (+8), 0 échec

## Task Commits

1. **Task 1 (RED): les quatre tests qui changent de sens, réécrits** — `46e4c61` (test)
2. **Task 1 (GREEN): la doctrine est APPELÉE en production** — `f032ac3` (feat)
3. **Task 2: le coût mesuré de 2,9 s mis sous garde** — `885a05a` (test)
4. **Task 3 (RED): un reset connu est un fait, pas une supposition à écraser** — `c02e027` (test)
5. **Task 3 (GREEN): le correctif d'une ligne** — `fe49375` (fix)

Aucune étape REFACTOR : les deux implémentations sont sorties propres du GREEN.

## Accomplishments

- **La doctrine est vivante.** À ce commit, `DoctrineFraicheur.Statuer` est appelée deux fois par
  rafraîchissement, en production, sur le snapshot final. Un relevé exact trop vieux cesse d'être présenté
  comme exact ; un relevé de trois heures sans activité depuis reste exact et le prouve ; un relevé suivi
  d'activité devient un plancher marqué, sans qu'un seul point de pourcentage soit inventé.
- **Le chemin nominal ne lit RIEN.** `Chemin_nominal_ne_lit_JAMAIS_les_transcripts` asserte
  `_activite.Lectures == 0` quand les deux fenêtres sont fraîches. Sans cette paresse, l'overlay lirait
  536 Mo par minute. La garde est **prouvée falsifiable** (mutation A ci-dessous).
- **Une seule passe sert les deux fenêtres.** `Les_deux_fenetres_degradees_partagent_UNE_SEULE_passe_disque`
  asserte `== 1`, pas `>= 1` : deux passes coûteraient 5,8 s. C'est la raison d'être de la séparation
  lecture/interrogation de la phase 16, désormais gardée par un test (mutation B).
- **Le mémoïseur est enregistré, pas seulement écrit.** `grep -c "new SourceActiviteMemoisee"
  src/Chronos/App.xaml.cs` rend **1**, et le conteneur miroir de `CompositionRootTests` reflète le même
  graphe — sans quoi la garde de composition attesterait d'un câblage que la production n'a pas.
- **Le 4ᵉ argument est obligatoire.** Aucune valeur par défaut : un site de construction qui l'oublierait
  ne compile pas. Les **quatre** sites ont été adaptés dans le **même commit** que le changement de
  signature, `tests/Chronos.Tests` référençant `Chronos`.
- **La garde de position de la sonde (phase 18) est restée verte sans retouche d'assertion**, ainsi que
  les 14 `CompositeUsageProviderTests` — le composite n'a pas été touché d'une ligne dans ce plan.
- **Le bug latent est mort.** Une fenêtre hebdo démotée conserve le `resets_at` que le serveur avait donné.

## Justification NOMINATIVE des quatre tests réécrits

Aucun test n'a été supprimé. Le décompte 680 → 688 (+8) est entièrement nominatif.

| # | Test d'origine | Devenu | Pourquoi il ne pouvait PAS rester tel quel |
|---|---|---|---|
| 3 | `Rebouche_une_fenetre_indisponible_depuis_le_magasin` | **SCINDÉ EN DEUX** : `Un_releve_de_deux_heures_SANS_activite_depuis_reste_EXACT` et `Un_releve_de_deux_heures_AVEC_activite_depuis_devient_un_plancher_marque` | Il affirmait qu'un relevé de deux heures est `Exact` **sans poser aucune question sur l'activité**. La question est désormais posée, et il y a **deux** réponses : la scission est la trace exécutable de DEL-03 et DEL-04. Le supprimer aurait effacé la preuve du changement de doctrine. |
| 6 | `Fenetre_estimee_n_est_ni_rebouchee_ni_ecrite` | `Un_plancher_n_est_JAMAIS_persiste_comme_exact` | Il injectait un `Estimated` **vivant**, impossible en production depuis la phase 16 — et le mot change de sens en phase 19 (`Estimated` = plancher). La propriété qui compte désormais est l'inverse : le plancher que la doctrine **produit** ne doit jamais redescendre dans le magasin, sinon la dégradation deviendrait **permanente**. Assertion : le fichier est **octet pour octet identique**. |
| 7 | `Magasin_corrompu_ne_leve_pas_et_rend_le_snapshot_de_l_inner` | `Un_magasin_en_panne_ne_leve_pas_et_n_affirme_rien_sur_le_passe` | Intention conservée (robustesse), entrée changée en `Exact` **frais** puisqu'un `Estimated` vivant n'existe plus, et assertion EXA-05 ajoutée : `Assert.Null(snap.UnExactADejaEteObtenu)`. |
| 9 | `Rebouchage_n_invente_aucun_SourceCapturedAt` | même nom, journal **sans** activité ajouté | Sans journal, la fenêtre de deux heures serait démotée en branche 4 et l'assertion `Assert.Equal(capture, snap.FiveHour.CapturedAt)` perdrait son objet. Le journal la maintient en branche 2, là où la propriété testée a un sens. |

Les cinq autres tests du fichier (1, 2, 4, 5, 8) n'ont subi **aucune retouche d'assertion** : seul le
helper `Deco()` a reçu le 4ᵉ argument. **Aucun d'eux n'est tombé** — ce qui est le signal attendu : le
chemin nominal (capture = `Now`) et le chemin « rien à servir » sont inchangés par la doctrine.

**Décompte nominatif de la variation 680 → 688 (+8) :**

| Tâche | Fichier | Tests ajoutés (net) | Nombre |
|---|---|---|---|
| 1 | `LastExactUsageProviderTests` | scission du test 3 en deux | **+1** |
| 2 | `LastExactUsageProviderTests` | `Chemin_nominal_ne_lit_JAMAIS_les_transcripts`, `Les_deux_fenetres_degradees_partagent_UNE_SEULE_passe_disque`, `Une_source_d_activite_en_panne_degrade_sans_lever`, `Sans_magasin_le_snapshot_declare_qu_aucun_exact_n_a_JAMAIS_ete_obtenu`, `Un_magasin_portant_un_releve_declare_qu_un_exact_a_deja_ete_obtenu` | **+5** |
| 3 | `WeeklyRecalibrationTests` | `Un_plancher_portant_un_reset_reel_n_est_PAS_recalibre`, `Une_fenetre_indisponible_sans_reset_reste_recalibrable_en_conservant_sa_fiabilite` | **+2** |

680 + 1 + 5 + 2 = **688**. Aucun test supprimé, aucun test ignoré, aucune perte de couverture nette.
`LastExactUsageProviderTests` compte désormais **15** tests (9 d'origine, dont un scindé, + 5).

## LES TROIS MUTATIONS — jouées, constatées, révoquées

Chaque garde de ce plan doit pouvoir **échouer**. Ce n'est pas affirmé, c'est **mesuré** : chaque mutation
a été réellement écrite dans le fichier de production, la suite filtrée exécutée, puis la mutation révoquée.

| # | Fichier muté | Mutation réellement jouée | Échecs | Test tombé (nommément) |
|---|---|---|---|---|
| **A** | `LastExactUsageProvider.cs` | la garde `ABesoinDuJournal(…) \|\| ABesoinDuJournal(…)` remplacée par `if (true)` — lecture inconditionnelle | **1 / 15** | `Chemin_nominal_ne_lit_JAMAIS_les_transcripts` |
| **B** | `LastExactUsageProvider.cs` | un **second** appel à la source ajouté dans le même `try` (deux passes) | **1 / 15** | `Les_deux_fenetres_degradees_partagent_UNE_SEULE_passe_disque` |
| **C** | `WeeklyRecalibration.cs` | l'**ancienne garde** restaurée (fiabilité exacte **ET** reset daté) | **1 / 9** | `Un_plancher_portant_un_reset_reel_n_est_PAS_recalibre` |

**Révocation prouvée :** après chacune des trois mutations, `git diff -- <fichier muté>` rend une chaîne
**vide** (vérifié par `wc -l` = 0). Aucune mutation n'a été committée.

**Ce que la mutation A apprend en plus :** elle ne fait tomber **qu'un** test, et c'est exactement le
signal voulu — la paresse est une propriété de **coût**, pas de **verdict**. Le contrat de cohérence
`ABesoinDuJournal` ⇄ `Statuer`, verrouillé au plan 19-02 sur cinq cas, garantit que lire le journal
inutilement ne change **aucune** réponse. Si la mutation A avait fait tomber un test de verdict, c'est ce
contrat qui aurait été cassé.

## Décisions Made

1. **La panne du magasin du test 7 devait être une panne DURE.** Le plan prescrivait de conserver le
   fichier de JSON invalide et d'asserter `Assert.Null(snap.UnExactADejaEteObtenu)`. Ces deux exigences
   sont **incompatibles** : `LastExactStore` est tolérant par construction (toute défaillance de lecture
   rend `null` sans lever, XML-doc du type), donc un fichier corrompu lui fait répondre **`false`** —
   c'est-à-dire « aucun exact n'a jamais été obtenu », une **affirmation**, exactement ce que l'assertion
   veut proscrire. Pire : avec l'entrée `Exact` fraîche prescrite, `Save` **réussit** et écrase le fichier
   corrompu, si bien que la réponse serait `true`. Le test utilise donc un magasin dont le **dossier
   parent est en réalité un fichier** : `Save` lève, le `catch` de la doctrine est réellement emprunté, et
   `null` est produit pour la bonne raison. Voir « Deviations ».
2. **Le reset serveur du test hebdo est à +5 j, pas à +3 j.** L'ancre de `WeeklyRecalibrationTests`
   synthétise précisément « now + 3 j » : avec la valeur du plan, `Un_plancher_portant_un_reset_reel_n_est_PAS_recalibre`
   passait **déjà** contre l'ancienne garde, par coïncidence numérique. Une garde qui ne peut pas échouer
   ne garde rien (précédent 18-01) — vérifié en exécution avant correction, puis après.
3. **`<see cref="DoctrineFraicheur"/>` et non `DoctrineFraicheur.Statuer` dans la XML-doc.** Le critère
   d'acceptation exige exactement **deux** occurrences de `DoctrineFraicheur.Statuer` dans le fichier (une
   par fenêtre) ; une référence croisée dans la documentation en aurait fait trois et rendu le critère
   faux pour une raison purement rédactionnelle. Même discipline que la consigne de rédaction imposée par
   la garde EXA-04 au plan 19-02.
4. **Un `#pragma warning disable CS0414` transitoire pour l'étape RED.** Le squelette compilable porte le
   champ `_activite` avant que la doctrine ne le lise ; le pragma évite d'introduire un avertissement dans
   un commit, et disparaît au GREEN. Le compte d'avertissements de la solution est resté à **2**, les deux
   `xUnit2031` **préexistants** et hors périmètre.
5. **`WeeklyRecalibration` ne mentionne plus `Estimated` dans sa documentation.** La garde ne teste plus la
   fiabilité, et le chemin de synthèse **conserve** celle qu'il reçoit — y compris `Unavailable`, qui
   recevra plus de trafic après cette phase. Dire « reste Estimated » serait devenu faux.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Le test 7 prescrit ne pouvait pas produire l'assertion prescrite**

- **Found during:** Task 1 (réécriture des quatre tests qui changent de sens)
- **Issue:** Le plan prescrivait `Magasin_corrompu_…` → entrée `Exact` fraîche → `Assert.Null(snap.UnExactADejaEteObtenu)`.
  `LastExactStore` ne lève **jamais** en lecture (`LireBrut` attrape tout et rend `null`), et `Save` sur un
  fichier de JSON invalide **réussit** en l'écrasant. Le `catch` de la doctrine n'aurait donc jamais été
  emprunté : la valeur produite aurait été `true` (relevé fraîchement écrit), et à défaut `false`.
  L'assertion était infalsifiable par construction — elle aurait dû être affaiblie pour passer.
- **Fix:** Le test construit un magasin réellement en panne — son dossier parent est un **fichier** — et
  porte le nom `Un_magasin_en_panne_ne_leve_pas_et_n_affirme_rien_sur_le_passe`. Un surcharge
  `Deco(inner, store)` a été ajoutée au helper. Le commentaire du test explique **pourquoi** un JSON
  invalide ne suffit pas, pour qu'un lecteur futur ne « simplifie » pas le montage.
- **Files modified:** `tests/Chronos.Tests/LastExactUsageProviderTests.cs`
- **Verification:** le test échoue à l'étape RED (parmi les 4), passe au GREEN, et l'assertion `Assert.Null`
  est atteinte par le chemin du `catch` — le seul qui produise `null`.
- **Committed in:** `46e4c61` (RED) / `f032ac3` (GREEN)

**2. [Rule 1 - Bug] Le test hebdo prescrit passait déjà contre le code fautif**

- **Found during:** Task 3 (étape RED)
- **Issue:** Avec `reelDuServeur = Now + 3 j`, `Un_plancher_portant_un_reset_reel_n_est_PAS_recalibre`
  était **vert** contre l'ancienne garde : `NextReset(Anchor = Now − 4 j, Now)` rend précisément
  `Now + 3 j`, donc la synthèse produisait la même valeur que le fait qu'elle écrasait. Mesuré : 9 / 9
  verts avant tout correctif.
- **Fix:** `reelDuServeur = Now + 5 j`, avec la raison écrite dans la XML-doc du test.
- **Files modified:** `tests/Chronos.Tests/WeeklyRecalibrationTests.cs`
- **Verification:** RED redevient 1 échec / 9 ; mutation C confirme que le test retombe si l'ancienne garde
  revient.
- **Committed in:** `c02e027`

---

**Total deviations:** 2 auto-corrigées (2 bugs de falsifiabilité dans les tests prescrits par le plan)
**Impact on plan:** aucun sur la conception. Les deux corrections **renforcent** des gardes qui, écrites
telles que prescrites, n'auraient rien gardé. Aucun fichier hors des 6 listés au frontmatter du plan.
Aucun scope creep : ni ViewModel, ni XAML, ni `CompositeUsageProvider`, ni `DiagnosticService` touchés.

## Issues Encountered

Une seule, résolue : la **révocation de la mutation C** a été faite par `git checkout --` alors que le
correctif de la tâche 3 n'était **pas encore committé** — la révocation a donc emporté le correctif avec
la mutation. Détecté immédiatement par `grep` (l'ancienne garde était de retour), correctif réappliqué,
suite repassée verte, puis **la mutation C a été rejouée après le commit GREEN**, cette fois avec une
révocation valide (`git diff` vide et nouvelle garde présente). La leçon est consignée : une mutation de
falsifiabilité ne se révoque par `git checkout` que si l'état de référence est **committé**.

Les deux étapes RED ont produit des échecs **comportementaux** et non de compilation :

| Étape RED | Échecs / total | Tests tombés |
|---|---|---|
| Tâche 1 | **4 / 10** | exactement les 4 réécrits, nommément identifiés |
| Tâche 3 | **1 / 9** | `Un_plancher_portant_un_reset_reel_n_est_PAS_recalibre` |

## Vérifications de sécurité et de non-régression

| Contrôle | Attendu | Obtenu |
|---|---|---|
| `stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"` (avant ET après) | `518 1783863147` | `518 1783863147` ✅ |
| Fichiers SOURCE contenant `RefreshAsync` | 2 | 2 ✅ |
| Aucune requête réseau depuis un test de ce plan | aucune | aucune ✅ (`api.anthropic.com` : 0 dans les deux fichiers de test touchés ; tout est en mémoire) |
| Écriture de test sous le vrai `%APPDATA%\Chronos\` | aucune | aucune ✅ (tous les chemins dérivent de `Path.GetTempPath()`) |
| `grep -c "class LastExactUsageProvider"` | 1 | 1 ✅ |
| `grep -c "Reboucher"` dans le décorateur | 0 | 0 ✅ |
| `grep -c "DoctrineFraicheur.Statuer"` dans le décorateur | 2 | 2 ✅ |
| `grep -c "ReadAsync"` dans le décorateur | 1 | 1 ✅ (un seul appel, commentaires compris) |
| `grep -cE "ITranscriptActivitySource activite\s*=\s*null"` | 0 | 0 ✅ (4ᵉ paramètre obligatoire) |
| `grep -c "new SourceActiviteMemoisee" src/Chronos/App.xaml.cs` | 1 | 1 ✅ |
| `grep -c "activite:"` App / CompositionRootTests | 1 / 2 | 1 / 2 ✅ |
| `grep -c "_activite.Lectures"` dans les tests | ≥ 3 | 3 ✅ |
| `grep -cE "weekly\.Reliability == SourceReliability\.Exact"` dans `WeeklyRecalibration.cs` | 0 | 0 ✅ |
| `grep -c "weekly.ResetsAt is not null"` | 1 | 1 ✅ |
| `WeeklyRecalibrationTests` | 9 tests, 0 échec | 9 / 9 ✅ |
| `LastExactUsageProviderTests` | ≥ 15 tests, 0 échec | 15 / 15 ✅ |
| `CompositeUsageProviderTests` | exactement 14, sans retouche d'assertion | 14 / 14 ✅ |
| `La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte` (garde phase 18) | vert | 1 / 1 ✅ |
| `Host_resout_et_dispose_les_singletons` | vert | 1 / 1 ✅ |
| `ServicesLayerPurityTests` + `NormalisationUniqueTests` + `GardesDoctrineTests` | 0 échec | 8 / 8 ✅ |
| `dotnet build Chronos.sln -c Release` | 0 erreur | 0 erreur ✅ (2 avertissements `xUnit2031` **préexistants**, hors périmètre) |
| Suite complète `dotnet test Chronos.sln -v q --nologo` | 0 échec | **688 / 688, 0 échec** ✅ |
| `DiagnosticService` | signature inchangée | inchangée ✅ (fichier non touché) |
| `CompositeUsageProvider.cs` | non touché | non touché ✅ (absent de `git diff --name-only`) |

## Statut des requirements

**Cochés à ce commit — EXA-02, DEL-03, DEL-04.** Leur comportement existe réellement dans l'application,
bout en bout :

- **EXA-02** (limite d'âge) : `DoctrineFraicheur.Statuer` est appelée en production sur chaque fenêtre. Un
  relevé exact au-delà de la limite, sans preuve d'absence d'activité, est **démoté** — `Reliability`
  passe à `Unavailable` et `Utilization` est **effacée**. Le « 10 % vieux de deux mois marqué `Exact` »
  ne peut plus s'afficher : il meurt soit par la limite d'âge, soit par `Covers(T)`.
- **DEL-03** (sans activité ⇒ encore exact) : branche 2, `ProvenanceReleve.EncoreValide`, avec
  `TokensDepuisReleve == 0` — **mesuré** à zéro, distinct de « non mesuré ». Testé de bout en bout par
  `Un_releve_de_deux_heures_SANS_activite_depuis_reste_EXACT`.
- **DEL-04** (avec activité ⇒ plancher marqué) : branche 3, `SourceReliability.Estimated` +
  `ProvenanceReleve.PlancherAvecActivite` + `TokensDepuisReleve`. Le marquage est **visible** :
  `WindowGaugeViewModel.IsEstimated` est bindé dans les quatre cadrans
  (`CadranBraisesView`, `CadranFusibleView`, `CadranMareeView`, `CadranVoletsView`) et
  `PercentFormatter.Format(util, IsEstimated)` qualifie déjà le chiffre. Le **raffinement** de ce marquage
  (« au moins N % » plutôt que le badge générique) est le plan 19-04.

**Restent PENDING — EXA-04 et EXA-05, et c'est intentionnel :**

- **EXA-04** : la garde de non-retour est posée, falsifiable et verte, et la branche plancher tourne
  désormais réellement sans jamais gonfler `Utilization`. Le cochage attend l'affichage « ≥ N % » du plan
  **19-04**, qui rend l'interdiction **visible** et non seulement structurelle.
- **EXA-05** : le bit remonte jusqu'au snapshot (`true` / `false` / `null`, chacun sous test), mais
  **aucun ViewModel ne le lit** : l'invite « connecte-toi » n'existe pas encore à l'écran. Plan **19-04**.

Cocher ces deux-là maintenant affirmerait un comportement que l'application n'a pas — l'erreur nommée par
le précédent 17-01 et répétée en 19-01 puis 19-02.

## Next Phase Readiness

Le plan **19-04** peut démarrer sans dépendance non satisfaite. Ce plan n'a touché **aucun** ViewModel et
**aucun** XAML : la surface visible est exactement celle que 19-04 attend.

Points d'attention pour 19-04 :

- **Les trois valeurs du bit EXA-05 appellent trois traitements**, pas deux : `true` (un exact a existé,
  sa fenêtre a roulé → **pas** d'invite à se connecter), `false` (jamais rien → invite), `null` (le magasin
  est muet → ne rien affirmer). Confondre `null` et `false` recrée la fausse alerte sur un compte sain.
- **`ProvenanceReleve` est la variable d'affichage**, pas `SourceReliability` : les deux branches exactes
  (`Frais` et `EncoreValide`) partagent la même fiabilité mais ne disent pas la même chose à l'utilisateur
  — c'est précisément ce que EXA-03 demandera (phase 20).
- **`TokensDepuisReleve` est de la matière première brute**, jamais un pourcentage : il ne doit apparaître
  à l'écran que comme volume d'activité, et jamais mis en rapport avec un plafond (garde EXA-04).
- **La surcharge « ≥ » de `PercentFormatter`** est le cœur du plan 19-04.

Hors périmètre, non anticipé ici : EXA-03 et EXA-06 (phase 20), le trou de la `UniformGrid` des réglages,
la dette de durée des tests (2 min 8 par passe complète).

---
*Phase: 19-nouvelle-doctrine-du-composite*
*Completed: 2026-09-12*

## Self-Check: PASSED

7 / 7 fichiers déclarés présents sur disque. 5 / 5 commits de tâche présents dans l'historique git.
`git diff --name-only` depuis le commit de métadonnées du plan 19-02 rend **exactement** les 6 fichiers du
frontmatter — ni `CompositeUsageProvider.cs`, ni `DiagnosticService.cs`, ni aucun ViewModel ou XAML.
Suite complète : 688 / 688, 0 échec. Coffre `oauth.dat` inchangé (518 o, mtime 1783863147).
Aucune mutation résiduelle : `git diff` vide sur les deux fichiers de production mutés.
