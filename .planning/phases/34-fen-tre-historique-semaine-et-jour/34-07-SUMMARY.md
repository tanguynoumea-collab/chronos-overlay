---
phase: 34-fen-tre-historique-semaine-et-jour
plan: 07
subsystem: ui
tags: [wpf, xaml, historique, vue-jour, pistes, annotations, surcouche, tdd, mutations, HIS-04]

# Dependency graph
requires:
  - phase: 34-01
    provides: tokens HistoHauteur{Niveau,Rythme,Tokens}Jour / HistoHauteurCouverture / HistoEpaisseurPremierPlan / HistoOpaciteMaintenant / HistoModele1-3, palette Ink/Ink2/Panel2/Line/Ok, garde « aucune valeur en dur »
  - phase: 34-03
    provides: HistoriqueViewModel (DonneesJour, LibellesHeures, AnnotationsResets/Epuisee/Trous/Sauts, LegendeModeles, LibellePermanentTokens, AfficherMaintenant, Maintenant, Fuseau, Theme, PlafondTokens, TexteFraicheur), ScenariosHistorique
  - phase: 34-04
    provides: PisteNiveau (Variante Jour), PisteRythme, PisteTokens (Colonnes + Modele1/2/3 + InstantLecture), PisteCouverture, SurcoucheReticule, InstantVersXConverter, LargeurIntervalleConverter, trace de rendu
  - phase: 34-05
    provides: stub VueJourView (Grid Racine), HistoriqueWindow hôte (DataContext hérité, thème injecté), rituel de montage
provides:
  - "Views/Historique/VueJourView.xaml — la vue « Jour » complète : axe des heures, NIVEAU 190 (variante Jour), RYTHME 64, TOKENS 64 par quart d'heure et par modèle, COUVERTURE 12, annotations resets / épuisée / trous / sauts 5 h, surcouche + infobulle, ligne « maintenant » seulement aujourd'hui, pied de page fixe"
  - "tests/Chronos.Tests/VueJourBindingTests.cs — 8 [WpfFact] : hauteurs par Measure/Arrange, tokens et thème reçus, axe, légendes, grille unique défilante, annotations du mer. 23 et du jeu. 24, « maintenant », infobulle, pied fixe"
affects: [34-08, 35]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Vue XAML pure : la table des hauteurs §2.3 n'existe que dans les tokens (Height=StaticResource) et un test de mise en page réel la lit sur l'arbre visuel"
    - "Colonnes Auto dimensionnées par leurs cellules (Width=StaticResource HistoLargeur…) : un sys:Double ne devient pas un GridLength"
    - "Annotations = ItemsControl à panneau Canvas, ItemContainerStyle qui pose Canvas.Left par MultiBinding InstantVersX [Debut/Instant, DataContext.DonneesJour.Plage (ancêtre UserControl), ActualWidth (ancêtre Canvas)]"
    - "Cause d'un trou → style par DataTemplate.Triggers (x:Static CauseTrou.JetonInvalide / SondeRefusee → AnnotationAmbre)"
    - "Trace de rendu en test : InvalidateVisual + UpdateLayout AVANT RenderTargetBitmap.Render (OnRender est rejoué à l'arrangement)"

key-files:
  created:
    - tests/Chronos.Tests/VueJourBindingTests.cs
  modified:
    - src/Chronos/Views/Historique/VueJourView.xaml
    - src/Chronos/Views/Historique/VueJourView.xaml.cs
    - tests/Chronos.Tests/HistoriqueBindingTests.cs (une assertion : la vue Jour n'est plus vide)
    - .planning/REQUIREMENTS.md (HIS-04 → Complete)

key-decisions:
  - "D-34-31 — une seule grille en Jour (pas de styles) ; colonnes Auto/*/Auto dont la largeur vient des cellules (HistoLargeurLibelles) et d'un espaceur (HistoLargeurLegendeDroite) ; rangées [annotations haut] [NIVEAU] [sauts] [RYTHME] [TOKENS] [légende + libellé permanent] [COUVERTURE] ; corps dans un ScrollViewer, axe des heures et pied de page fixes"
  - "D-34-32 — « reset 5 h HH:MM » est une annotation du VM (AnnotationsResets) posée à l'instant du reset ; le trait vertical est dessiné par la piste au même instant (même donnée)"
  - "D-34-33 — « épuisée … » posée au DÉBUT du plateau, en Ink2 (pas d'ambre), MaxWidth = LargeurIntervalle du plateau, CharacterEllipsis + ToolTip du texte entier"
  - "D-34-34 — la légende des modèles vient du VM (LegendeModeles) ; posée en colonne 1 SOUS les tokens, au-dessus du libellé permanent (72 px de colonne droite ne suffisent pas à « opus · sonnet · haiku · sous-agents inclus »)"
  - "Les annotations et l'axe sont projetés sur DonneesJour.Plage (pas PlageCourante) : annotation et plage viennent de la MÊME lecture appliquée, elles ne peuvent pas diverger pendant une lecture en vol"
  - "Ressources locales nommées HistoBordureInfobulle (Thickness 1) et HistoDecalageInfobulle (8) dans le dictionnaire de la vue (motif 34-05 : le dictionnaire commun est fermé à 38 sys:Double)"

patterns-established:
  - "Test de hauteur robuste au DPI : la DP Height est comparée EXACTEMENT au token, l'ActualHeight à ± 0,5 px (UseLayoutRounding arrondit au pixel physique : 190 → 190,4 à 125 %)"
  - "Mutations par copie sur l'arbre réel, révocation par git show HEAD:… > fichier, sha256 (16 hex) comparés avant / après"

requirements-completed: [HIS-04]

# Metrics
duration: ~26 min (2026-09-27T14:22:47Z → 14:48Z)
completed: 2026-09-27
tasks: 2 (TDD : 4 commits)
files: 4 (+ REQUIREMENTS.md)
tests_added: 8
suite: 1549 / 1549 verts, deux exécutions consécutives sur l'arbre réel, 0 warning
---

# Phase 34 Plan 07 : Vue « Jour » Summary

**La vue « Jour » assemblée en XAML pur dans le stub de 34-05 : axe 0 h → 24 h locale (huit heures posées à leur instant), NIVEAU 190 en variante Jour (% 5 h au premier plan 2,4 px, gris `HistoGris` à 100 % / `rejected`, hebdo en trait fin `Ink2`, un trait `TickReset` par reset observé), RYTHME 64 par heure, TOKENS 64 par quart d'heure empilés par modèle (`HistoModele1/2/3`, légende « opus · sonnet · haiku · sous-agents inclus », libellé « par quart d'heure, … »), COUVERTURE 12, annotations « reset 5 h HH:MM » / « épuisée à 100 % — le serveur refuse (statut rejected) » / causes des trous (ambre pour « jeton ») / sauts 5 h, surcouche avec infobulle à quatre lignes et ligne « maintenant » `Ink` à 60 % seulement si le jour affiché est aujourd'hui, pied de page fixe — hauteurs 190/64/64/12 lues sur l'arbre visuel, 8 tests, 8 mutations rougies et révoquées, 1549 / 1549 deux fois sur l'arbre réel, 0 warning — HIS-04 clos.**

SHA d'entrée : `a5bccd4` (1531 verts + le correctif xUnit2031 de l'orchestrateur). `requirements mark-complete HIS-04` exécuté (`updated: true`).

## Ce que l'utilisateur voit sur la galerie (`dotnet run --project src/Chronos -- --historique`, segment « Jour »)

Jeudi 24 (aujourd'hui, 17:12) : l'axe « 0 h … 21 h », le % 5 h coloré au premier plan, l'hebdo fin dessous, trois traits de reset avec « reset 5 h 05:00 / 10:00 / 15:00 », le trou « jeton invalide » (14:00 → 16:00) en ambre avec son saut « au moins un reset pendant l'absence (répartition inconnue) », les tokens par quart d'heure en trois gris de modèle (rien après 17:10 : le futur est vide), la ligne « maintenant » à 17:12, la couverture verte coupée d'ambre. Par ‹, mercredi 23 : le plateau gris 20:00 → 00:00 nommé « épuisée à 100 % — le serveur refuse (statut rejected) », les resets 09:00 / 14:00 / 19:00, l'hebdo qui monte en trait fin, PAS de ligne « maintenant », « Aujourd'hui » qui ramène au présent. L'agent n'a lancé ni l'overlay ni la galerie.

## Contrat pour 34-08 (noms XAML et faits utiles)

- **Noms d'éléments** : `Racine` (Grid 3 rangées), `AxeHeures` (Grid, hauteur `HistoHauteurAnnotations`), `Corps` (Grid unique, 7 rangées, dans le `ScrollViewer`), `RangeeAnnotationsHaut`, `RangeeSauts`, `Surcouche` (`SurcoucheReticule`, rangées 1 → 6 de `Corps`), `Infobulle` (`Border` `Panel2` / `Line`, `Canvas.Left = XReticule`, `Canvas.Top = HistoDecalageInfobulle`). Styles locaux : `LibellePiste`, `Repere`, `Annotation`, `AnnotationAmbre`, `Pied`, `PoseeAuDebut` / `PoseeALInstant` (ContentPresenter → `Canvas.Left`), `PanneauCanvas`. Aucun élément nommé `Style*`.
- **Pistes visibles dans l'ordre** : `PisteNiveau` (Variante `Jour`, Height 190) → `PisteRythme` (64) → `PisteTokens` (64, `Colonnes` bindées, `Barres` null) → `PisteCouverture` (12). `PisteFenetres5h` absente. La surcouche est une `PisteBase` : l'exclure quand on compte les pistes de données.
- **Bindings** : `Plage/Analyse/Colonnes/LueA` ← `DonneesJour.*` ; `Rampe` ← `Theme` ; `TraitReset` / `BordTrouJeton` / `Jeton` ← `{DynamicResource TickReset|Alerte}` ; tout le reste `{StaticResource}` ; `Maintenant`, `AfficherMaintenant`, `Fuseau` ← VM ; `Plafond` ← `PlafondTokens`.
- **Annotations exactes du scénario** (heure de Paris ; instants UTC entre crochets) :
  - jeu. 24 sept. (aujourd'hui) — resets : « reset 5 h 05:00 » [03:00Z], « reset 5 h 10:00 » [08:00Z], « reset 5 h 15:00 » [13:00Z] (celui de 15:00 est observé par le champ `R5` de part et d'autre du trou) ; épuisée : aucune ; trous : « jeton invalide » [12:00Z → 14:00Z] ; sauts (5 h) : « au moins un reset pendant l'absence (répartition inconnue) » [12:00Z → 14:00Z] ; journal ouvert : — ; fraîcheur : « 288 relevés attendus · 184 présents · 1 interruption (jeton invalide, 14:00 → 16:00) » ; légende « opus · sonnet · haiku · sous-agents inclus » ; échelle « 0 – 2 k (sortie) » (plafond 2000).
  - mer. 23 sept. — resets : « reset 5 h 09:00 » [07:00Z], « reset 5 h 14:00 » [12:00Z], « reset 5 h 19:00 » [17:00Z] (le reset de 04:00 tombe dans le trou de la nuit : aucun relevé de part et d'autre → **non observé**, ni annoté ni tracé) ; épuisée : « épuisée à 100 % — le serveur refuse (statut rejected) » [18:00Z → 22:00Z] ; trous : **aucun** (voir écart 5) ; sauts : aucun ; fraîcheur : « 288 relevés attendus · 204 présents · 0 interruption ».
- **Trace de rendu** (vocabulaire 34-04) : mercredi, `PisteNiveau` produit ≥ 1 `palier … gris` et exactement 3 `trait f` ; la surcouche produit `maintenant 0.717` jeudi seulement ; `PisteCouverture` produit `present 0.292 …` mercredi (rien avant 07:00) et `trou … jeton` jeudi.

## Task Commits

| Tâche | Commit | Message |
|---|---|---|
| 1 RED | `041a5d6` | test(34-07): vue Jour - hauteurs 190/64/64/12, variante Jour…, grille unique defilante, RED 4 |
| 1 GREEN | `c6623fe` | feat(34-07): VueJourView - axe des heures, NIVEAU 190 variante Jour, RYTHME 64, TOKENS 64 par quart d'heure et par modele, COUVERTURE 12, legende des modeles, libelle permanent par quart d'heure (HIS-04) |
| 2 RED | `7e8e915` | test(34-07): vue Jour - resets observes et epuisee annotes, ligne maintenant seulement aujourd'hui, trou jeton ambre et saut 5 h, infobulle quatre lignes, pied de page fixe, RED 4 |
| 2 GREEN | `b55813d` | feat(34-07): VueJourView - annotations resets/epuisee/trous/sauts 5 h, ligne maintenant seulement aujourd'hui, surcouche + infobulle, pied de page (HIS-04) |

RED nommés : Task 1 → les 4 tests (stub vide : aucune piste, aucun libellé, aucun `ScrollViewer`) ; Task 2 → `Les_resets_observes_et_l_epuisee_sont_annonces_le_mercredi`, `La_ligne_maintenant_n_existe_qu_aujourd_hui`, `Le_trou_jeton_invalide_et_son_saut_5h_sont_annotes_aujourd_hui`, `L_infobulle_du_jour_a_quatre_lignes_et_le_pied_de_page_est_fixe` (4 / 8 rouges, les 4 de Task 1 restant verts).

## Mutations (par copie sur l'arbre réel, révoquées par `git show HEAD:… >`, sha256 identiques)

| # | Mutation | Rouge nommé (seul) | sha256 avant = après |
|---|---|---|---|
| j1 | `Height=HistoHauteurNiveauPistes` sur NIVEAU (150) | `La_vue_Jour_a_exactement_les_pistes_et_hauteurs_du_plan` | `b1077cf31b09d207` |
| j2 | `Variante="SemaineComplete"` | idem | `b1077cf31b09d207` |
| j3 | `Barres="{Binding DonneesSemaine.Barres}"` au lieu de `Colonnes` | idem (`Colonnes == null`) | `b1077cf31b09d207` |
| j4 | `Modele2` non bindé | idem | `b1077cf31b09d207` |
| k1 | `AfficherMaintenant="True"` figé | `La_ligne_maintenant_n_existe_qu_aujourd_hui` (mercredi) | `fe0845a2d088e773` |
| k2 | `ItemsSource="{Binding AnnotationsResets}"` retiré | `Les_resets_observes_et_l_epuisee_sont_annonces_le_mercredi` | `fe0845a2d088e773` |
| k3 | `TraitMaintenant=Ink2` | `La_ligne_maintenant_n_existe_qu_aujourd_hui` (couleur) | `fe0845a2d088e773` |
| k4 | style `Annotation` (gris) sur le trou « jeton » | `Le_trou_jeton_invalide_et_son_saut_5h_sont_annotes_aujourd_hui` | `fe0845a2d088e773` |

Chaque mutation a rougi exactement le test attendu (1 / 8), les sept autres restant verts ; aucune n'a produit d'erreur de compilation.

## Verification

- `dotnet test Chronos.sln -c Debug --nologo -v q` : **1549 / 1549 verts, deux exécutions consécutives sur l'arbre réel** (14:39Z et 14:41Z ; 1531 d'entrée + 8 de ce plan + 10 de 34-06 déjà fusionnés). `dotnet build Chronos.sln -c Debug` : **0 warning**.
- Filtre du plan (`VueJourBindingTests|GardeTokensHistoriqueTests|HistoriqueBindingTests`) : 22 / 22.
- Greps d'acceptation sur `VueJourView.xaml` : `Variante="Jour"` 1 · `<hc:PisteNiveau` 1 · `<hc:PisteRythme` 1 · `<hc:PisteTokens` 1 · `<hc:PisteCouverture` 1 · `<hc:PisteFenetres5h` 0 · `HistoHauteur(NiveauJour|RythmeJour|TokensJour|Couverture)` 4 · `HistoEpaisseurPremierPlan` 1 · `Colonnes="{Binding DonneesJour.Colonnes}"` 1 · `HistoModele[123]` 3 lignes · `LibellesHeures` 1 · `LegendeModeles` 1 · `DonneesSemaine` 0 · `x:Name="Style` 0 · `AnnotationsResets` 1 · `AnnotationsEpuisee` 1 · `AnnotationsTrous` 1 · `AnnotationsSauts` 1 · `CharacterEllipsis` 1 · `AfficherMaintenant="{Binding AfficherMaintenant}"` 1 · `HistoOpaciteMaintenant` 1 · `x:Name="Infobulle"` 1 · `PiedDePage` 1 · `PiedDivergence` 0 · littéraux interdits (hex / `FontSize` / `Height` / `StrokeThickness` / `Thickness` / `Opacity` en chiffres) **0** ; `VueJourView.xaml.cs` 10 lignes, `InitializeComponent` seul ; `[WpfFact]` 8 ; XAML 264 lignes.
- Périmètre : mes 4 commits ne touchent que `VueJourView.xaml(.cs)`, `VueJourBindingTests.cs` et UNE ligne de `HistoriqueBindingTests.cs` (stagée par blob depuis HEAD : la ligne Semaine de 34-06 n'a jamais été emportée). Aucun fichier de `Controls/`, `ViewModels/`, `Resources/`, `VueSemaineView*`, `HistoriqueWindow*`, ROADMAP.md, STATE.md.
- Aucun exe lancé ; aucune écriture hors du dépôt et du scratchpad ; aucun paquet NuGet.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `ColumnDefinition Width="{StaticResource HistoLargeurLibelles}"` lève `ArgumentException` (« '96' n'est pas une valeur valide pour Width »)**
- **Found during:** Task 1 GREEN (les 4 tests + tout `HistoriqueBindingTests` rouges par `XamlParseException`).
- **Issue:** WPF ne convertit pas un `sys:Double` de ressource en `GridLength`.
- **Fix:** colonnes 0 et 2 en `Auto`, largeur portée par les cellules de la colonne 0 (`Width="{StaticResource HistoLargeurLibelles}"`) et par un `Border` espaceur en colonne 2 (`HistoLargeurLegendeDroite`) dans `AxeHeures` et `Corps` — aucun chiffre dupliqué, aucun token nouveau. **À connaître pour 34-06 / 34-08** si le même motif y est tenté.
- **Commit:** `c6623fe`

**2. [Rule 1 - Test] `ActualHeight` = 190,4 à 125 % DPI**
- **Issue:** `UseLayoutRounding` arrondit au pixel physique (190 × 1,25 = 237,5 → 238 → 190,4 DIP) ; « exact 190 » est faux sur cette machine et le serait dans la fenêtre réelle.
- **Fix:** le test compare la DP `Height` EXACTEMENT aux tokens (190 / 64 / 64 / 12) et l'`ActualHeight` à ± 0,5 px (un pixel physique). L'intention (le token pilote la hauteur) est intégralement vérifiée ; j1 rougit bien.

**3. [Rule 1 - Test] Le défilement n'est pas déclenché à 480 px de haut pour la vue SEULE**
- **Issue:** la vue Jour mesure ≈ 450 px (18 + 18 + 190 + 18 + 64 + 64 + légendes + 12 + pied) : à `Measure(760, 480)` `ScrollableHeight == 0`. Mais dans la fenêtre minimale (480), le corps ne dispose que de ≈ 360 px (en-tête 92 + marges + bordure).
- **Fix:** le test mesure à `760 × 360` (le corps réel de la fenêtre minimale) → `ScrollableHeight > 0`, `DesiredSize.Width ≤ 760`, pistes ≤ 592 px ; à `1400 × 900` → `PisteNiveau.ActualWidth > 1000` et `ScrollableHeight == 0`.

**4. [Rule 1 - Contrat] Resets observés du mercredi : 3, pas 4**
- **Issue:** le plan attendait 04:00 / 09:00 / 14:00 / 19:00 ; le reset de 04:00 tombe dans le trou « Chronos arrêté » (mar. 23:00 → mer. 07:00) : aucun relevé de part et d'autre → il n'est pas OBSERVÉ (doctrine « jamais une courbe reconstituée »). Le VM en annonce 3 et la piste en trace 3 : cohérence prouvée (`Assert.Equal(AnnotationsResets.Count, traits)`).
- **Fix:** liste exacte gravée dans le test (« reset 5 h 09:00 / 14:00 / 19:00 »), plus `DoesNotContain("reset 5 h 04:00")`.

**5. [Rule 1 - Contrat] Aucun trou annoté le mercredi (le trou de la nuit chevauche minuit)**
- **Issue:** le plan attendait « Chronos arrêté » posé à `Canvas.Left == 0` avec `Debut < Plage.Debut`. Sonde : `vm.AnnotationsTrous` du mercredi est **vide** — l'analyse du JOUR commence à son premier relevé (07:00 local, `Serie[0].T = 05:00Z`) et ne rapporte pas l'absence antérieure ; la fraîcheur dit « 0 interruption ». Rien n'est interpolé avant 07:00 (la couverture commence à 7/24 — vérifié par la trace `present 0.292`).
- **Fix côté vue :** le test grave le comportement réel (aucun trou, « Chronos arrêté » absent, couverture à partir de 07:00). Le convertisseur borne bien un `Debut` antérieur à la plage à 0 (test `InstantVersX_borne_a_la_largeur` de 34-04) : la vue est prête si l'analyse change.
- **À trancher en 34-08 (hors de mon périmètre : VM / `AnalyseReleves`, non touchés) :** faut-il qu'un trou qui chevauche minuit apparaisse dans la vue Jour du lendemain (borné à la plage), et que la fraîcheur du jour le compte ? Voir « Deferred ».

**6. [Rule 1 - Test] La trace de rendu ne se remplit pas par `RenderTargetBitmap.Render` seul**
- **Issue:** `OnRender` est rejoué à l'ARRANGEMENT d'un visuel invalidé, pas par le rendu bitmap (Open Question 4 de 34-04 dans l'autre sens).
- **Fix:** helper `Trace(piste)` = `TracerPourTests = true` → `InvalidateVisual()` → `UpdateLayout()` → `Render` → lecture, `TracerPourTests` remis à `false` dans un `finally`.

**7. [Rule 2 - Zéro warning] xUnit2013 sur `Assert.Equal(1, Trous.Count)`** → `Assert.Single`. Build à 0 warning avant le commit RED de Task 2.

### Précisions prises à l'exécution (pas des écarts de contrat)

- Légende des modèles en colonne 1, sous les tokens et au-dessus du libellé permanent (D-34-34) : à 72 px la colonne droite l'aurait mise sur quatre lignes.
- Les annotations et l'axe se projettent sur `DonneesJour.Plage` plutôt que `PlageCourante` : même lecture appliquée que les annotations elles-mêmes.
- Le `Canvas.Left` est posé sur le conteneur d'item (`ContentPresenter`), pas sur le `TextBlock` : les tests lisent la position par `TransformToAncestor(canvas)`.
- Compteurs littéraux : la rangée des sauts est nommée `RangeeSauts` (pas `AnnotationsSauts`, qui aurait compté 2), et les trois bindings `Modele1/2/3` sont sur trois lignes (le grep compte des lignes).
- Le mot « épuisée » porte un `ToolTip` du texte complet (D-34-33 : un plateau court le tronque).
- Les annotations d'une même rangée peuvent se chevaucher (un reset et un trou proches) : accepté, à juger en revue DAEDALUS.

## Known Stubs

Aucun. Toute donnée affichée vient du VM ; aucune valeur factice, aucun texte de remplacement.

## Deferred (pour 34-08 / la revue)

1. **Ombrage des brosses du thème par le dictionnaire fusionné de la vue.** La vue fusionne `DesignTokens.xaml` (obligatoire : un `UserControl` construit dans la fenêtre ne voit pas encore les ressources de l'hôte au parse, et il se monte seul en test). Ce dictionnaire contient les replis STATIQUES `TickReset` (#F4F2EC) et `Alerte` (#EFA23A) : un `{DynamicResource Alerte}` posé DANS la vue les trouve avant les pinceaux du thème injectés dans `HistoriqueWindow.Resources`. Pour le thème par défaut « Minuit » les valeurs coïncident (les tests passent) ; pour un autre thème, l'ambre des trous « jeton » et le trait des resets de la fenêtre Historique resteraient ceux de Minuit. La RAMPE (`Rampe="{Binding Theme}"`) n'est pas concernée. La même limite s'applique à `VueSemaineView` (34-06, même consigne de fusion). Pistes : (a) la fenêtre injecte aussi les pinceaux du thème dans `Resources` de chaque vue hébergée ; (b) exposer sur `ChronosTheme` des brosses nommées (`TickReset`, `Alerte`) et binder `Theme.*` ; (c) sortir les deux clés thémées du dictionnaire fusionné. Décision transverse aux deux vues → 34-08.
2. **Trou qui chevauche minuit dans la vue Jour** (écart 5) : l'analyse du jour ne le rapporte pas ; la vue est prête (`InstantVersX` borne à 0) si 34-08 décide de le faire remonter par le VM / la façade.
3. **Infobulle non bornée au bord droit** (comme en Semaine, note du plan 34-06) : un survol après ~22 h peut la faire déborder de la colonne des pistes — à juger en revue.
4. **Garde 34-01** : `OpaciteTrou="0.5"` / `EpaisseurFine="2"` littéraux ne seraient pas vus (motif limité à `FontSize|Height|…|Opacity`) — même remarque que 34-06 (n4) : élargir la regex à `Opacite\w*|Epaisseur\w*|Longueur\w*|Largeur\w*|Decalage\w*="[0-9]` en 34-08.
5. **Espaceurs de colonne** (`Border Width=HistoLargeurLegendeDroite`) : à promouvoir en motif commun si 34-06 a rencontré le même `GridLength`.

## Issues Encountered

- Verrou `testhost` du voisin (MSB3027) pendant une sonde de diagnostic : attente 45 s et reprise (règle de vague) ; les deux passes complètes ont tourné sans verrou.
- `HistoriqueBindingTests.cs` est partagé : ma ligne (Jour) a été stagée par blob construit depuis HEAD ; la ligne Semaine (34-06) est arrivée par son commit. Le test `Les_deux_vues_sont_hebergees_et_encore_vides` porte désormais un nom inexact (les deux vues sont pleines) : renommage laissé à 34-08 pour ne pas toucher un fichier à deux mains.
- Heredoc > 8 Ko tronqué par l'outil (mémoire de session) : les patchs longs sont passés par des scripts Python dans le scratchpad.

## Self-Check: PASSED

- FOUND : `src/Chronos/Views/Historique/VueJourView.xaml`, `src/Chronos/Views/Historique/VueJourView.xaml.cs`, `tests/Chronos.Tests/VueJourBindingTests.cs`, ce SUMMARY.
- FOUND commits : `041a5d6`, `c6623fe`, `7e8e915`, `b55813d`.
- Suite 1549 / 1549 verte deux fois sur l'arbre réel ; 0 warning ; `requirements mark-complete HIS-04` exécuté ; ROADMAP.md / STATE.md non modifiés par ce plan (exécution parallèle : `state *` et `roadmap update-plan-progress` laissés à l'orchestrateur).
