---
phase: 34-fen-tre-historique-semaine-et-jour
plan: 08
subsystem: ui
tags: [wpf, historique, honnetete, gardes, vocabulaire, tokens, mutations, gate-tests, HIS-06, HIS-07]

# Dependency graph
requires:
  - phase: 34-03
    provides: ScenariosHistorique, SourceHistoriqueDemonstration, FakeSourceHistorique, HistoriqueViewModel, TextesHistorique
  - phase: 34-04
    provides: PisteBase.TracerPourTests + TraceRendu (vocabulaire de trace), pistes Niveau / Rythme / Tokens / Couverture / Fenetres5h
  - phase: 34-06
    provides: VueSemaineView (trois grilles de style, annotations, zone avant journal, pieds)
  - phase: 34-07
    provides: VueJourView (annotations resets / épuisée / trous / sauts, fraîcheur du jour), écart 5 (brosses ombrées), écart « trou ouvert à minuit »
provides:
  - "tests/Chronos.Tests/HonneteteHistoriqueTests.cs — 11 [WpfFact] : chaque promesse d'honnêteté du plan de design prouvée sur les objets de la galerie, mots (TextBlock visibles) + traits (TraceRendu), 2 vues × 3 styles"
  - "tests/Chronos.Tests/GardeVocabulaireHistoriqueTests.cs — 3 [Fact] : aucune projection dans les 34 fichiers de la fenêtre, mots du plan présents, anti-mutisme nommé"
  - "GardeTokensHistoriqueTests durcie : Opacite*/Epaisseur*/Longueur*/Plafond/NbBandes, anti-mutisme 3 xaml / 7 cs, + garde « aucun texte visible écrit en dur »"
  - "HistoriqueWindow : pinceaux du thème injectés aussi dans les vues hébergées (l'ambre « jeton » suit les neuf thèmes)"
  - "TextesHistorique.RepereCent / RepereZero : repères de l'axe NIVEAU sur les trois styles de la Semaine et en Jour"
  - "HistoriqueViewModel : PlafondJoli supprimé, EchelleValeur.MaxArrondi source unique du plafond des tokens"
affects: [35 (vue 4 semaines, release 3.3.0, revue DAEDALUS)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Test d'honnêteté = objets de la galerie (SourceHistoriqueDemonstration) + mots lus sur l'arbre visuel VISIBLE (parcours qui n'entre pas dans un sous-arbre caché) + traits lus dans TraceRendu"
    - "Injection des pinceaux du thème dans Resources de CHAQUE UserControl hébergé qui fusionne DesignTokens.xaml : une entrée locale prime sur le dictionnaire fusionné"
    - "Garde de texte visible : Text/Content/ToolTip littéraux interdits dans les XAML de la fenêtre, liste de tolérance nommée (marque « Chronos », glyphes ✕ ‹ ›)"
    - "Mutation d'une garde de fichiers par témoin non compilé (_mut.xaml + dotnet test --no-build), jamais commité"

key-files:
  created:
    - tests/Chronos.Tests/HonneteteHistoriqueTests.cs
    - tests/Chronos.Tests/GardeVocabulaireHistoriqueTests.cs
  modified:
    - tests/Chronos.Tests/GardeTokensHistoriqueTests.cs
    - tests/Chronos.Tests/HistoriqueBindingTests.cs
    - src/Chronos/Views/Historique/HistoriqueWindow.xaml
    - src/Chronos/Views/Historique/HistoriqueWindow.xaml.cs
    - src/Chronos/Views/Historique/VueSemaineView.xaml
    - src/Chronos/Views/Historique/VueJourView.xaml
    - src/Chronos/Text/TextesHistorique.cs
    - src/Chronos/ViewModels/Historique/HistoriqueViewModel.cs
    - src/Chronos/Models/Historique/LectureJournal.cs (un commentaire)
    - .planning/REQUIREMENTS.md (HIS-06 → Complete)

key-decisions:
  - "D-34-35 — ce que l'utilisateur voit est ce que les tests prouvent : aucun test d'honnêteté ne fabrique ses données, tous lisent ScenariosHistorique par SourceHistoriqueDemonstration (la semaine vide dérive des mêmes objets par FakeSourceHistorique.TransformerSemaine)"
  - "D-34-36 — la garde de vocabulaire couvre tout .cs / .xaml de src/Chronos dont le chemin contient « Historique », hors Services/Historique/** et obj/bin (34 fichiers vus) ; motif « épuisé[e]? vers | à ce rythme | projection | prévision | estim(é|ation|er) | tendance | dans N h »"
  - "D-34-37 — corrections limitées aux libellés et au plafond du VM : aucune piste, géométrie, service ou token modifié (git diff Controls/Rendering/Services vide) ; les deux corrections hors libellés (injection des pinceaux, commentaire) sont des points différés nommés par l'orchestrateur"
  - "Brosses thémées (point différé 1, piste (a)) : la fenêtre injecte BrushTokens() dans Resources de VueSemaine et VueJour (nommées) — aucune vue ni token ne change, les vues continuent de se monter seules en test"
  - "Tirets / traits de reset 5 h permis dans un trou : ils sont datés par le resets_at ANNONCÉ par le serveur (02:00Z et 13:00Z tombent dans les deux trous du scénario), un instant connu et pas une valeur interpolée — seules les primitives qui portent une valeur relevée (palier, barre, tuile) sont interdites dans ]début, fin[ d'un trou"
  - "Trou ouvert à minuit (point différé 5) NON corrigé : le VM ne reçoit que la lecture du jour ; nommer l'absence de la nuit exige de lire [Début − cadence, Fin[ dans les deux façades (Services / ViewModels source) — hors D-34-37, consigné pour la phase 35 ; le test épingle le comportement réel (« 0 interruption » le mercredi, premier relevé à 07:00)"

patterns-established:
  - "Chaque test d'honnêteté boucle sur les trois styles DANS un [WpfFact] avec le style dans le message (lisible, un seul montage par style)"
  - "Une promesse de ligne interrompue se teste par RECOUVREMENT (un palier qui enjambe un trou commence au bord du trou : tester le seul début ne voit pas l'interpolation)"

requirements-completed: [HIS-06]

# Metrics
duration: ~23 min (2026-09-27T15:24Z → 15:47Z)
completed: 2026-09-27
tasks: 2 (+ 3 points différés en commits nommés)
files: 12 (2 créés, 10 modifiés dont REQUIREMENTS.md)
tests_added: 16
suite: 1565 / 1565 verts, deux exécutions consécutives sur le HEAD final, 0 warning Debug et Release
---

# Phase 34 Plan 08 : Honnêteté de bout en bout et GATE TESTS Summary

**Onze tests d'honnêteté sur les objets exacts de la galerie `--historique` (trous nommés en rectangle `Line` 35 % pointillé gris / ambre, saut en bloc plat sur toute l'absence, cadre `Accent` de la divergence et son pied « consommé ailleurs », zone avant journal hachurée et datée, libellé permanent des tokens et pied de page sur chaque vue, plateau épuisé gris et nommé, rien de relevé dessiné dans un trou, semaine vide qui le dit, mots du plan, fraîcheur du jour, repères de NIVEAU), une garde anti-projection sur les 34 fichiers de la fenêtre, une garde « aucune valeur ni texte en dur » durcie, l'ambre des trous « jeton » qui suit enfin les neuf thèmes, la dette `PlafondJoli` soldée ; cinq mutations d'honnêteté rougies puis révoquées ; 1565 / 1565 deux fois, 0 warning Debug et Release — HIS-06 clos, la phase 34 attend la revue visuelle de l'utilisateur.**

SHA d'entrée : `0babc97` (arbre propre ; première tentative coupée avant tout commit). HEAD de code : `e816e0e`. `requirements mark-complete HIS-06` exécuté (`updated: true`, seule la ligne HIS-06 change). ROADMAP.md / STATE.md non modifiés (consigne de l'orchestrateur).

## Promesse du plan de design → test qui la tient

| Promesse (DESIGN_PLAN) | Test(s) | Mutation qui le prouve |
|---|---|---|
| Trou > 2 cadences : ligne interrompue, rectangle `Line` 35 %, bordure pointillée grise « Chronos arrêté » / ambre « jeton invalide » (§2.2) | `HonneteteHistoriqueTests.Un_trou_interrompt_l_escalier_et_se_dessine_en_rectangle_pointille_nomme` (× 3 styles) | h1 |
| Rien n'est dessiné entre deux relevés absents | `Rien_n_est_dessine_entre_deux_relevés_absents_de_plus_de_deux_cadences` (Pistes + Tuiles) | h1 |
| Saut non localisé : bloc plat gris « +N % pendant l'absence (répartition inconnue) », jamais une barre au réveil | `Le_saut_d_une_absence_est_un_bloc_plat_jamais_une_barre_au_reveil` (× 3) | h2 |
| Divergence : cadre pointillé `Accent` + « cadre violet : … consommé ailleurs (Cowork, claude.ai) », seulement sur une heure couverte | `Une_marche_sans_tokens_Code_est_encadree_et_le_pied_le_dit` (× 3), `DivergencesTests.Une_heure_hors_couverture…` | h3 |
| Avant le journal : zone vide + « journal ouvert le 14 sept. 2026 » | `La_zone_anterieure_au_journal_est_vide_et_datee` | (n2 de 34-06, zone) |
| Libellé permanent des tokens et pied de page fixe sur chaque vue | `Le_libelle_permanent_des_tokens_et_le_pied_de_page_sont_sur_chaque_vue` (Semaine × 3, Jour, fenêtre) | (n2 de 34-06) |
| Épuisée : gris à 100 % + « épuisée à 100 % — le serveur refuse (statut rejected) » ; tuile grise | `L_epuisee_est_grise_et_nommee_le_mercredi_et_la_tuile_l_est_aussi` | (g3 de 34-06) |
| Aucune projection (§4 : relevé, jamais estimation) | `GardeVocabulaireHistoriqueTests.Aucune_projection…`, `Les_mots_de_la_fenetre_sont_ceux_du_plan_de_design`, `TextesHistoriqueTests.Aucune_projection_dans_les_textes` | h4 |
| Semaine sans relevé : dite, rien de dessiné, jamais « 0 % » | `La_semaine_vide_dit_aucun_releve_et_ne_dessine_rien` | — (garde de sens) |
| Fraîcheur du jour « 288 relevés attendus · N présents · 1 interruption (jeton invalide, 14:00 → 16:00) » | `La_fraicheur_du_jour_compte_les_attendus_et_nomme_les_interruptions` | — |
| Grille 0 / 50 / 100 % : repères « 100 % » / « 0 » (D-34-29) | `Les_reperes_de_l_axe_niveau_sont_poses_sur_chaque_vue`, `GardeTokensHistoriqueTests.Aucun_texte_visible_ecrit_en_dur…` | RED de c1c7a22 |
| Hauteurs §2.2 / §2.3 depuis les tokens | `VueSemaineBindingTests.Le_style_Pistes…`, `GardeTokensHistoriqueTests.Les_tokens_de_taille…` | h5 |
| Aucune valeur en dur (couleur, taille, DP numérique nommée) | `GardeTokensHistoriqueTests.Aucune_couleur_ni_taille_en_dur…` (durcie) | témoin `_mut.xaml` |
| Ambre « jeton » = `Alerte` du thème ACTIF | `HistoriqueBindingTests.Les_vues_hebergees_suivent_les_pinceaux_du_theme_actif` (thème Nord) | RED de a8742ab |
| Jamais au tick, tokens promus, F2 mot pour mot | tenus par 34-03 / 34-04 / 34-05 (`HistoriqueViewModelTests`, `PistesHistoriqueTests`, `HistoriqueBindingTests.Le_bandeau_F2…`) — verts dans les deux passes | (mutations de ces plans) |

## Task Commits

| # | Commit | Message (abrégé) | RED / GREEN |
|---|---|---|---|
| 1 | `d6fd5e7` | test(34-08): honnêteté de bout en bout sur les objets de la galerie … RED 0 (10 verts à l'entrée) | 10 verts à l'entrée (voir ci-dessous) |
| diff. 1+4 | `a8742ab` | test(34-08): les vues hébergées suivent les pinceaux du thème actif ; `Les_deux_vues_sont_hebergees_et_remplies` (renommé), RED 1 | rouge : #EFA23A au lieu de #EBCB8B |
| diff. 1 | `c19ce39` | fix(34-08): pinceaux du thème injectés aussi dans les vues hébergées | GREEN |
| diff. 6 | `c1c7a22` | test(34-08): aucun texte visible écrit en dur ; repères « 100 % » / « 0 » sur chaque vue, RED 2 | rouges : `VueJourView.xaml:186-187` ; aucun repère en Semaine |
| diff. 6 | `0de0ad8` | feat(34-08): repères « 100 % » / « 0 » de l'axe NIVEAU depuis TextesHistorique | GREEN |
| 2 | `8e7fc91` | test(34-08): garde de vocabulaire … et garde tokens durcie, RED 1 | rouge réel : `Models/Historique/LectureJournal.cs:12` « aucune projection » (commentaire) ; témoin `_mut.xaml` rouge |
| 2 | `779ac7d` | fix(34-08): commentaire de doctrine de LectureJournal sans mot de projection | GREEN |
| 2 / diff. 2 | `91ae5fb` | refactor(34-08): PlafondTokens par EchelleValeur.MaxArrondi (dette 34-03 soldée) | vert (HistoriqueViewModelTests, VueSemaine/Jour inchangés) |
| 1 | `e816e0e` | test(34-08): helper de trace nommé TraceRendu, aucun changement d'assertion | — |

**Verts à l'entrée (Task 1, 10 / 10)** : attendu — les vagues 1 à 3 ont livré l'honnêteté ; ce plan la VERROUILLE. Chaque test est rougi par au moins une mutation : h1 (trou, rien entre deux relevés), h2 (saut), h3 (divergence), h4 (mots), et les mutations déjà jouées sur les mêmes éléments (34-06 n1 annotations de trous, n2 pied divergence ; 34-07 k4 ambre ; g3 variante Tuiles). `La_semaine_vide…` et `La_fraicheur_du_jour…` sont des gardes de sens (données limites / chaîne exacte) : la chaîne exacte rougit dès qu'un mot change dans `TextesHistorique.LigneFraicheurJour`.

## Mutations d'honnêteté (par copie sur l'arbre réel, révoquées par re-copie, suite complète à chaque fois)

| # | Mutation | Rouges constatés (suite complète, 1565) | sha256 (16 hex) avant = après |
|---|---|---|---|
| h1 | `PisteNiveau` : escalier hebdo `Escalier.Segments(…, Array.Empty<Trou>(), …)` | **`Un_trou_interrompt…`**, **`Rien_n_est_dessine…`**, `PistesHistoriqueTests.L_escalier_de_niveau_ne_traverse_pas_les_trous…` (3) | `fcbb9ded344df1c5` |
| h2 | `PisteNiveau` : bloc de saut remplacé par une barre d'1 h à `Fin` (« barre au réveil »), trace comprise | **`Le_saut_d_une_absence_est_un_bloc_plat_jamais_une_barre_au_reveil`**, `PistesHistoriqueTests.L_escalier…` (2) | `fcbb9ded344df1c5` |
| h3 | `Divergences.Detecter` sans `Etat == Couverte` + heure mer. 17:00 (15:00Z) « transcripts absents » sans tokens | **`Une_marche_sans_tokens_Code_est_encadree_et_le_pied_le_dit`**, **`DivergencesTests.Une_heure_hors_couverture_ou_transcripts_absents_ne_peut_pas_accuser`**, `ScenariosHistoriqueTests.La_divergence_est_le_mercredi_de_21_h_a_23_h…`, `ScenariosHistoriqueTests.Les_tokens_couvrent_la_semaine…`, `HistoriqueViewModelTests.Les_annotations_disent…`, `PistesHistoriqueTests.Les_tokens_empilent…`, `VueSemaineBindingTests.Les_pistes_recoivent…` (7) | `e936ac5158e4da15` (Divergences) / `a4c1e51603338b53` (source) |
| h4 | `TextesHistorique.PiedDePage` += « (estimé) » | **`GardeVocabulaireHistoriqueTests.Aucune_projection…`**, **`TextesHistoriqueTests.Aucune_projection_dans_les_textes`**, **`HonneteteHistoriqueTests.Les_mots_de_la_fenetre…`**, `TextesHistoriqueTests.Les_constantes_du_vocabulaire…` (4) | `70fef5034c36c8fe` |
| h5 | `DesignTokens.xaml` : `HistoHauteurNiveauPistes` 150 → 148 | **`VueSemaineBindingTests.Le_style_Pistes…`**, **`GardeTokensHistoriqueTests.Les_tokens_de_taille…`**, `GardeTokensHistoriqueXamlTests.Le_dictionnaire_de_tokens…` (3) | `0c5812fed8ea8991` |
| témoin | `Views/Historique/_mut.xaml` avec `OpaciteTrou="0.5"` (lu par `dotnet test --no-build`, jamais compilé ni commité) | `GardeTokensHistoriqueTests.Aucune_couleur_ni_taille_en_dur…` (la limite n4 de 34-06 est levée) | fichier supprimé, `ls` = 0 |

`git status --short` vide après chaque révocation. Chaque rouge exigé par le plan (en gras) est présent ; aucune mutation n'a produit d'erreur de compilation.

## GATE TESTS (fin de phase 34)

- `dotnet test Chronos.sln -c Debug --nologo -v q` sur le HEAD final `e816e0e` : **1565 / 1565, deux exécutions consécutives** (15:45:37Z et 15:46:02Z) ; déjà 1565 / 1565 deux fois sur `91ae5fb` (15:44:11Z, 15:44:35Z). 1549 d'entrée + 16 de ce plan (11 honnêteté + 3 vocabulaire + 1 texte en dur + 1 thème).
- `dotnet build Chronos.sln -c Release --nologo` : **0 Avertissement(s), 0 Erreur(s)** ; `-c Debug` : **0 / 0**.
- `ServicesLayerPurityTests`, `GardeTokensSansPourcentageTests`, `GardesDoctrineTests`, `GardesPerimetreTests` : verts (suite complète).
- Commits de la phase : `git log --oneline | grep -c "(34-0"` = **52** (dont 9 de ce plan : `git log 0babc97..HEAD`).
- Greps d'acceptation : `[WpfFact]` 11 · `SourceHistoriqueDemonstration` 2 · `TraceRendu` 18 · « répartition inconnue » 1 · « journal ouvert le 14 sept. 2026 » 1 · « ce n'est PAS un % du forfait » 2 · « consommé ailleurs » 2 ; vocabulaire : `[Fact]` 3 · `>= 12` 1 · « à ce rythme » 1 ; tokens : « Anti-mutisme relevé en 34-08 » 0 · `Opacite` 6 · `>= 7` 1 ; VM : `PlafondJoli` 0 · `EchelleValeur.MaxArrondi(` 1 · chaînes ≥ 12 lettres 0 ; `git diff --stat 0babc97 HEAD -- src/Chronos/Controls src/Chronos/Rendering src/Chronos/Services` **vide**.
- Aucun exe lancé (ni overlay ni galerie), aucun `dotnet run`, aucune écriture sous `~/.claude`, `%APPDATA%\Claude`, `%APPDATA%\Chronos` ; aucun paquet NuGet ; release 3.3.0 non publiée (phase 35).

## Points différés des vagues 1 à 3

| # | Point | Traitement |
|---|---|---|
| 1 | Brosses `Alerte` ombrées par le `DesignTokens.xaml` fusionné dans les vues (bug réel pour 8 thèmes sur 9) | **Corrigé** (`a8742ab` RED → `c19ce39`) : piste (a), injection dans les vues nommées. `TickReset` n'est pas une brosse de thème (absent de `BrushTokens()`) : non concerné. |
| 2 | `PlafondJoli` → `EchelleValeur.MaxArrondi` | **Soldé** (`91ae5fb`). Différence assumée : période sans tokens → « 0 – 1 (sortie) » au lieu de « 0 – 0 ». |
| 3 | Garde 34-01 aveugle aux DP numériques nommées | **Durcie** (`8e7fc91`) : `Opacite\w*|Epaisseur\w*|Longueur\w*|Plafond|NbBandes`, anti-mutisme 3 / 7 ; les ressources locales nommées (`<sys:Double x:Key=…>8</sys:Double>`) restent permises. |
| 4 | `Les_deux_vues_sont_hebergees_et_encore_vides` | **Renommé** `Les_deux_vues_sont_hebergees_et_remplies` (`a8742ab`). |
| 5 | Trou qui chevauche minuit absent de la vue Jour du mercredi | **Consigné pour la phase 35** (voir Deferred) ; comportement réel épinglé par `La_fraicheur_du_jour…` (« 0 interruption », premier relevé 07:00). |
| 6 | Repères « 100 % » / « 0 » de NIVEAU | **Fait** (`c1c7a22` → `0de0ad8`) : `TextesHistorique.RepereCent` / `RepereZero` ; les deux littéraux de `VueJourView.xaml` supprimés, repères posés sur les trois styles de la Semaine ; une garde interdit désormais tout texte visible littéral dans les XAML de la fenêtre. |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Ambre des trous « jeton » figé sur Minuit pour les huit autres thèmes**
- **Found during:** point différé 1 (test RED au thème Nord : `#FFEFA23A` au lieu de `#FFEBCB8B`)
- **Fix:** `HistoriqueWindow` nomme `VueSemaine` / `VueJour` et leur injecte `BrushTokens()` comme à elle-même.
- **Files modified:** `HistoriqueWindow.xaml`, `HistoriqueWindow.xaml.cs`
- **Commit:** `c19ce39`

**2. [Rule 2 - Honnêteté des mots] Deux textes visibles écrits en dur dans `VueJourView.xaml`** (`Text="100 %"`, `Text="0"`)
- **Fix:** constantes `TextesHistorique.RepereCent` / `RepereZero` + `{x:Static}` ; nouvelle garde `Aucun_texte_visible_ecrit_en_dur_dans_les_xaml_de_l_historique` (tolérance nommée : « Chronos », ✕ ‹ ›).
- **Commit:** `c1c7a22`, `0de0ad8`

**3. [Rule 1 - Garde] Commentaire « aucune projection » dans `Models/Historique/LectureJournal.cs`** — seul rouge réel de la garde de vocabulaire ; commentaire reformulé (« rien n'annonce l'avenir »), aucun code. Fichier hors de la liste du plan (Models), modification d'une ligne de commentaire. **Commit:** `779ac7d`

**4. [Rule 1 - Test du plan trop large] Tirets / traits de reset permis dans un trou**
- **Issue:** le plan interdisait le début d'un `tiret` dans `]f0, f1[` d'un trou. Sonde : l'analyse observe les resets 5 h de 2026-09-23 02:00Z et 2026-09-24 13:00Z, tous deux DANS les trous (le `resets_at` annoncé avant le trou change après lui). Ce sont des instants datés par le serveur, pas des valeurs interpolées ; 34-07 annote déjà « reset 5 h 15:00 » dans le trou « jeton » (D-34-32).
- **Fix:** le test interdit les primitives qui portent une VALEUR relevée (palier, barre, tuile) ; le commentaire du test dit pourquoi le tiret peut y vivre. Ce n'est pas un assouplissement d'un test vert : la règle a été écrite ainsi avant la première exécution.

**5. [Rule 1 - Test du plan qui ne rougirait pas h1] Recouvrement des paliers**
- **Issue:** un palier qui enjambe un trou (mutation h1) COMMENCE au bord du trou : tester le seul début ne le voit pas, alors que le plan exige que h1 rougisse `Rien_n_est_dessine…`.
- **Fix:** les paliers sont testés par recouvrement de `]f0, f1[` (durcissement) ; h1 rougit bien les deux tests.

**6. [Rule 1 - Test du plan faux sur les données] « Barre au réveil » en Rythme**
- **Issue:** le plan interdisait toute barre de Δ ≥ 0,02 contenant 07:00 ; or l'heure 07:00–08:00 consomme réellement ≈ 0,055 de 5 h (activité 0,5), relevé après relevé.
- **Fix:** le test exige que la barre de l'heure du réveil n'excède pas ce qui a été relevé APRÈS le réveil (dernier U5 de l'heure − U5 de 07:00), qu'aucun Δ (5 h ou hebdo) ne relie le dernier relevé avant le trou au premier après, et qu'il y ait exactement une barre du réveil (anti-mutisme).

**7. [Rule 3 - Mutation h2, moitié Rythme non jouable]** Le plan voulait aussi « `DeltasParHeure` alimenté par les Sauts ». `PisteRythme` ne reçoit que `Deltas5h` (aucune DP de sauts) et les deux trous du scénario contiennent chacun un reset 5 h : aucun Δ 5 h ne peut les traverser dans cette fixture. Seule la moitié Niveau de h2 a été jouée (rouge nommé obtenu) ; la protection Rythme est tenue par l'assertion « aucun Δ ne traverse le trou » et par `BinningTests` (34-02).

**8. [Rule 3 - Mutation h3, fixture]** `CouvertureTokens` ne sait pas exprimer un trou d'une heure (une passe garantit au moins 30 jours) : l'heure « transcripts absents » a été injectée dans `SourceHistoriqueDemonstration.LireSemaine` (même chemin que la galerie), avec la mutation de `Divergences.Detecter`. Les deux rouges exigés sont obtenus.

**9. [Rule 1 - Test] `L_epuisee…` : fin du gris à ± 2 × tolérance** — le dernier relevé du mercredi est à 21:55Z (le palier finit au dernier relevé, pas à minuit pile) ; le test demande le début à 20:00 ± 0,002 et la fin à 00:00 ± 0,004 (≈ 6 min sur 24 h), et aucun palier rampe dans le plateau.

### Précisions (pas des écarts de contrat)
- Les « × 3 styles » sont des boucles dans un `[WpfFact]` (message préfixé du style) : `WpfTheory` n'est utilisé nulle part dans le dépôt.
- Test 11 ajouté (`Les_reperes_de_l_axe_niveau…`) : 11 `[WpfFact]` au lieu de 10.
- `Les_mots_du_plan_sont_presents_dans_les_textes` interdit « mesure » dans les CHAÎNES de `TextesHistorique` (les commentaires de doctrine citent le mot banni pour l'interdire) ; au moins 30 chaînes lues (anti-mutisme).

## Known Stubs

Aucun. `PlafondJoli` (stub connu depuis 34-03) est soldé ; toute donnée affichée vient du VM ; aucun texte de remplacement.

## Deferred (phase 35)

1. **Trou ouvert à minuit** : la vue Jour du mercredi ne nomme pas l'absence mar. 23:00 → mer. 07:00 (l'analyse du jour commence à son premier relevé ; la fraîcheur dit « 0 interruption »). Correction : lire `[Début − cadence, Fin[` dans `SourceHistoriqueDisque` et `SourceHistoriqueDemonstration` (ou un relevé « veille ») pour que `AnalyseReleves` voie le trou, borné à la plage par `InstantVersX` (déjà prêt). Hors D-34-37 (services / façades).
2. **Infobulle non bornée au bord droit** (Semaine et Jour).
3. **Rythme** : une moitié de la mutation h2 n'est pas exprimable avec la fixture actuelle ; si la phase 35 ajoute un trou SANS reset 5 h, rejouer « Δ 5 h à travers le trou ».

## Revue visuelle DAEDALUS (à faire par l'utilisateur — l'agent n'a rien lancé)

**Commande** (depuis la racine du dépôt) :

```
dotnet run --project src/Chronos -- --historique
```

La galerie ouvre la VRAIE `HistoriqueWindow` sur la semaine de référence (jeu. 24 sept. 2026 17:12, réglages en mémoire donc thème **Minuit**, aucune écriture de fichier). Critères DESIGN_PLAN §8 :

1. **Semaine, style A « Pistes »** (défaut) : trou « Chronos arrêté » (mar. 23:00 → mer. 07:00) en rectangle gris 35 % à bordure pointillée grise, nommé au-dessus de NIVEAU ; trou « jeton invalide » (jeu. 14:00 → 16:00) en ambre ; bloc plat gris « +2 % pendant l'absence (répartition inconnue) » sous NIVEAU ; cadre violet pointillé mer. 21:00 → 23:00 et le pied « cadre violet : … consommé ailleurs (Cowork, claude.ai) » ; repères « 100 % » / « 0 » à droite de « NIVEAU » ; pied de page fixe. Vérifier que « 100 % » ne touche pas « NIVEAU » (96 px) et que « 0 » est aligné sur le bas de la piste (≈ 4 px d'écart possible, marge de piste).
2. **Styles B « Simplifié » et C « Tuiles »** (puces « Style : ») avec les MÊMES données : mêmes trous, saut, divergence ; en C, la tuile grise de la fenêtre épuisée (mer. 20:00 → jeu. 00:00) et l'hebdo seul dans NIVEAU.
3. **‹ (semaine précédente)** : zone hachurée avant le lun. 14 sept. 12:00 et « journal ouvert le 14 sept. 2026 ».
4. **Jour, jeu. 24** : « reset 5 h 05:00 / 10:00 / 15:00 », trou « jeton invalide » ambre et « au moins un reset pendant l'absence (répartition inconnue) », ligne « maintenant » à 17:12, infobulle à quatre lignes au survol (« … · relevé exact »). **‹ mer. 23** : plateau gris 20:00 → 00:00 nommé « épuisée à 100 % — le serveur refuse (statut rejected) », pas de ligne « maintenant ». Noter que la nuit mar. → mer. n'est pas nommée en vue Jour (Deferred 1).
5. **Redimensionnement 760 → 1 400 px** : aucun chevauchement, libellés de pistes dans leur colonne, le corps défile à la hauteur minimale.
6. **Échelle Windows 100 % et 150 %** : traits 1 px nets, aucun texte tronqué (sauf « épuisée … » volontairement tronqué avec infobulle quand le plateau est court).
7. **Thème** : NON vérifiable dans la galerie (réglages en mémoire, toujours Minuit, dont l'ambre coïncide avec le repli statique). Le suivi des neuf thèmes est prouvé par `HistoriqueBindingTests.Les_vues_hebergees_suivent_les_pinceaux_du_theme_actif` (thème Nord) ; contrôle visuel possible quand la fenêtre sera ouverte depuis l'overlay (gestes de la phase 35).

**Écarts connus par rapport aux maquettes** (à juger, pas des régressions) :
- Coins arrondis DWM ≈ 8 px (rayon système Windows 11) au lieu de 16 ; coins droits sous Windows 10 (D-34-22).
- Badge de version `[v3.3.0]` omis de l'en-tête (release de la phase 35).
- Infobulle du réticule non bornée au bord droit : près du samedi / après ~22 h, elle déborde dans la colonne de droite.
- Annotations d'une même rangée qui peuvent se chevaucher (un reset et un trou proches, Jour).
- Police Segoe UI au lieu d'Inter (mêmes corps).
- « 4 semaines » présent mais désactivé (« bientôt (phase 35) »).
- Tirets de reset 5 h dessinés dans les trous quand le serveur avait annoncé l'instant (voir écart 4) : à valider visuellement.

## Issues Encountered

- Échappements Python / heredoc sur une regex C# (`\b`, `\{`) : repris par l'outil Edit ; aucun commit intermédiaire cassé.
- Aucun verrou `testhost` (seul sur l'arbre en vague 4).

## Self-Check: PASSED

- FOUND : `tests/Chronos.Tests/HonneteteHistoriqueTests.cs`, `tests/Chronos.Tests/GardeVocabulaireHistoriqueTests.cs`, ce SUMMARY ; `src/Chronos/Views/Historique/_mut.xaml` ABSENT.
- FOUND commits : `d6fd5e7`, `a8742ab`, `c19ce39`, `c1c7a22`, `0de0ad8`, `8e7fc91`, `779ac7d`, `91ae5fb`, `e816e0e`.
- Suite 1565 / 1565 deux fois sur le HEAD final, 0 warning Debug et Release ; `git status --short` vide avant le commit de documentation (hors REQUIREMENTS.md et ce SUMMARY).
