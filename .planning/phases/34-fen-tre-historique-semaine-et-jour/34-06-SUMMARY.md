---
phase: 34-fen-tre-historique-semaine-et-jour
plan: 06
subsystem: ui
tags: [wpf, xaml, historique, vue-semaine, trois-styles, annotations, surcouche, infobulle, measure-arrange, tdd, mutations, HIS-02, HIS-03]

# Dependency graph
requires:
  - phase: 34-01
    provides: tokens HistoHauteur* / HistoLargeur* / HistoCorps* / HistoEpaisseur* / HistoOpacite*, brosses Histo*, BoolToVis, garde « aucune valeur en dur »
  - phase: 34-02
    provides: EchelleTemps (attendus des tests de position), EchelleValeur.MaxRythme
  - phase: 34-03
    provides: HistoriqueViewModel (DonneesSemaine, LibellesJours, Annotations*, AnnotationJournalOuvert, AfficherPiedDivergence, LibellePermanentTokens, LegendeTokens, EchelleTokens, EchelleRythme, PiedDePage, PlafondTokens, Theme, Fuseau, IsStyleX, ChoisirStyleCommand), TextesHistorique, ScenariosHistorique, SourceHistoriqueDemonstration, FakeSourceHistorique
  - phase: 34-04
    provides: PisteNiveau / PisteRythme / PisteTokens / PisteCouverture / PisteFenetres5h / SurcoucheReticule (table des DP), InstantVersXConverter, LargeurIntervalleConverter, PisteBase.TracerPourTests
  - phase: 34-05
    provides: stub VueSemaineView (Grid Racine), DataContext hérité de HistoriqueWindow, rituel Monter / TousLesTextBlocks
provides:
  - "Views/Historique/VueSemaineView.xaml : la vue « Semaine de forfait » complète — trois grilles de style (StylePistes / StyleSimplifie / StyleTuiles) aux hauteurs des tokens, pistes bindées au VM et aux tokens, axe des jours + « reset hebdo → » + grille verticale par jour, annotations d'honnêteté posées par instant, zone hachurée avant le journal, surcouche réticule + infobulle par grille, libellé permanent des tokens, légende, pied de page fixe et pied « cadre violet »"
  - "tests/Chronos.Tests/VueSemaineBindingTests.cs : 10 [WpfFact] — ordre et ActualHeight exacts par style sur l'arbre visuel réel, bascule à chaud, annotations à leur instant, S-1, pieds, surcouche, tokens/thème, 760 → 1 400 px"
affects: [34-08 (honnêteté, anti-mutisme, garde élargie, revue DAEDALUS), 35 (vue 4 semaines : même motif de grille)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Trois Grid de style superposées dans un même Corps, Visibility par DataTrigger IsStyleX sur un style de base Collapsed ; chaque grille porte SES instances de pistes (une piste appartient à un style)"
    - "Test de mise en page réel : Measure/Arrange de la vue seule (elle fusionne DesignTokens.xaml), parcours de l'arbre visuel qui n'entre pas dans un sous-arbre Collapsed, assertion sur l'ordre des types de pistes et leur ActualHeight EXACT"
    - "Annotations posées par instant sans C# : ItemsControl à panneau Canvas, ItemContainerStyle avec Canvas.Left = MultiBinding(InstantVersX : Debut/Instant, DonneesSemaine.Plage par RelativeSource UserControl, ActualWidth du Canvas par RelativeSource)"
    - "Axe hors défilement aligné sur la colonne des pistes DANS le défilement par un témoin de largeur (Border MesureColonnePistes) : la barre de défilement ne décale pas les libellés des jours"
    - "Pas de UseLayoutRounding sur une vue mesurée seule : à 125 % d'échelle il arrondit 150 DIP → 150,4 ; l'arrondi appartient à la fenêtre hôte"
    - "OnRender d'un visuel invalidé est appelé à la fin de son Arrange (UpdateLayout), pas par RenderTargetBitmap.Render : InvalidateVisual → UpdateLayout → lire la trace"

key-files:
  created:
    - tests/Chronos.Tests/VueSemaineBindingTests.cs
  modified:
    - src/Chronos/Views/Historique/VueSemaineView.xaml
    - src/Chronos/Views/Historique/VueSemaineView.xaml.cs
    - tests/Chronos.Tests/HistoriqueBindingTests.cs (une assertion : la vue Semaine n'est plus vide)
    - .planning/REQUIREMENTS.md (HIS-02, HIS-03 → Complete)

key-decisions:
  - "D-34-27 — trois Grid nommées StylePistes / StyleSimplifie / StyleTuiles, Visibility par DataTrigger IsStyleX (base GrilleStyle Collapsed), instances de pistes distinctes par style ; le test lit l'ordre des pistes VISIBLES et leur ActualHeight — la table §2.2 n'est écrite que dans les tokens"
  - "D-34-28 — le corps défile verticalement (ScrollViewer Auto / Disabled, x:Name Defilement) ; l'axe des jours et les pieds restent hors du défilement ; à la hauteur réellement disponible à la taille minimale de la fenêtre (≈ 360 px) le corps défile, à 900 px non"
  - "D-34-29 — une piste = une rangée Auto / * / Auto (CelluleLibelles 96 px + marge, piste, CelluleDroite 72 px + marge) ; annotations dans des rangées HistoHauteurAnnotations : trous + « journal ouvert le » au-dessus de NIVEAU, sauts au-dessous ; Canvas.Left par InstantVersXConverter sur DonneesSemaine.Plage (même plage que les pistes, pas PlageCourante : cohérence des données immuables)"
  - "D-34-30 — zone antérieure au journal : Rectangle ZoneAvantJournal{Pistes|Simplifie|Tuiles} (HistoHachure à HistoOpaciteTrou) derrière NIVEAU, Width = LargeurIntervalleConverter(Plage.Debut → AnnotationJournalOuvert.Debut, largeur de la cellule), Collapsed quand AnnotationJournalOuvert est null ; le mot est l'annotation elle-même"
  - "Une SurcoucheReticule PAR grille (Surcouche{Pistes|Simplifie|Tuiles}, RowSpan sur toutes les rangées de pistes) et un Border Infobulle* bindé par ElementName à XReticule / TexteInfobulle / InfobulleVisible — non borné au bord droit en phase 34 (à juger en revue DAEDALUS)"
  - "Colonnes Auto + largeurs explicites sur les cellules (pas de GridLength issu d'un sys:Double) ; épaisseurs, marges et côtés locaux en ressources nommées (HistoBordureInfobulle, HistoDecalageInfobulle, HistoCoteCarreDivergence, HistoMarge*) — le dictionnaire commun reste fermé à 38 sys:Double"
  - "Repères « 100 % » / « 0 » de la piste Niveau NON posés : aucun littéral visible hors TextesHistorique (règle de la vague) et TextesHistorique n'a pas ces constantes — à ajouter par 34-08 si la revue les veut"

patterns-established:
  - "Vue par style en XAML statique : la même vue peut recevoir un 4e style (phase 35) en ajoutant une Grid et un IsStyleX, sans toucher au test (il lit l'arbre)"
  - "Mutation d'une vue XAML jouée dans l'instantané snap-34-06 (git archive HEAD + copie des fichiers du plan) pour ne pas exposer un XAML muté aux builds du voisin sur l'arbre partagé"

requirements-completed: [HIS-02, HIS-03]

# Metrics
duration: ~35 min (première lecture ≈ 14:10Z ; RED 1 commité 14:24:47Z ; dernier commit de code 14:40:09Z ; SUMMARY ≈ 14:45Z)
completed: 2026-09-27
tasks: 2
files: 5 (1 créé, 4 modifiés dont REQUIREMENTS.md)
tests_added: 10
suite: 1549 / 1549 verts, deux exécutions consécutives sur l'arbre réel, 0 warning
---

# Phase 34 Plan 06 : Vue « Semaine de forfait » — trois styles, annotations, surcouche Summary

**La vue Semaine assemblée en XAML pur (575 lignes, zéro chiffre, zéro mot hors `TextesHistorique` / VM) : trois grilles de style aux hauteurs des tokens — Pistes 150 / 72 / 72 / 12, Simplifié 200 / 90 / 12, Tuiles 120 (hebdo seul) / 62 / 58 / 58 / 12 — vérifiées sur l'arbre visuel réel par Measure/Arrange (`ActualHeight` EXACT), une seule visible, bascule à chaud par le sélecteur ; axe samedi → samedi avec ses sept libellés, « reset hebdo → » et la grille verticale des jours ; annotations d'honnêteté (« Chronos arrêté » gris, « jeton invalide » ambre, « +2 % pendant l'absence (répartition inconnue) », « journal ouvert le 14 sept. 2026 » avec la zone antérieure hachurée sur S-1) posées à leur instant par les convertisseurs de 34-04 ; réticule + infobulle à quatre lignes par grille ; libellé permanent des tokens, légende, pied de page fixe et pied « cadre violet » sur les trois styles ; 760 → 1 400 px sans débordement — 10 tests, 8 mutations rougies puis révoquées, 1549 / 1549 deux fois, 0 warning. HIS-02 et HIS-03 clos.**

SHA d'entrée : `a5bccd4` (1531 verts + correction xUnit2031 de l'orchestrateur). HEAD de sortie de code : `507c826`.

## Ce que l'utilisateur verra sur la galerie (`dotnet run --project src/Chronos -- --historique`)

Corps de la fenêtre rempli en Semaine : A, B ou C selon la puce « Style : », bascule à chaud ; les deux trous nommés au-dessus de NIVEAU (le second en ambre), le saut « +2 % … » sous NIVEAU, le cadre violet de la divergence du mercredi soir et son pied ; « ‹ » montre S-1 avec la zone hachurée avant le lundi 14 sept. 12:00 et « journal ouvert le 14 sept. 2026 » ; le survol pose le réticule et l'infobulle (heure DU RELEVÉ). L'agent n'a lancé aucun exe.

## Noms XAML utiles à 34-08

| Élément | Nom(s) | Rôle |
|---|---|---|
| Grilles de style | `StylePistes`, `StyleSimplifie`, `StyleTuiles` | une seule `Visible` (DataTrigger `IsStyleX`) |
| Corps / défilement | `Racine`, `Defilement` (ScrollViewer), `Corps` | axe [0] et pieds [2] hors du défilement |
| Axe des jours | `AxeJours` (ItemsControl), `MesureColonnePistes` (Border témoin de largeur) | libellés posés par `PoseParInstant` |
| Zone avant journal | `ZoneAvantJournalPistes` / `…Simplifie` / `…Tuiles` | `Rectangle`, style `ZoneAvantJournal`, `Fill=HistoHachure` |
| Surcouche / infobulle | `SurcouchePistes` / `…Simplifie` / `…Tuiles` ; `InfobullePistes` / `…Simplifie` / `…Tuiles` | `Border` style `Infobulle`, `TextBlock` style `TexteInfobulle` |
| Pieds | `PiedDivergence` (StackPanel) | `Visibility = AfficherPiedDivergence` |

Styles / templates locaux : `CelluleLibelles`, `CelluleDroite`, `LibellePiste`, `Repere`, `Annotation`, `Pied`, `GrilleStyle`, `PoseParInstant`, `PoseParDebut`, `MarqueurJournal`, `ZoneAvantJournal`, `Infobulle`, `TexteInfobulle` ; `PanneauCanvas`, `TplGraduation`, `TplLigneJour`, `TplAnnotation`, `TplTrou`. Ressources locales nommées : `HistoMargePiste` (0,0,0,6), `HistoMargeLibelle`, `HistoMargeLegendeDroite`, `HistoMargeLibellePermanent`, `HistoMargePieds`, `HistoMargePiedDivergence`, `HistoMargeCarreDivergence`, `HistoCoteCarreDivergence` (10), `HistoDecalageInfobulle` (8), `HistoBordureInfobulle` (1) — promotion possible en 34-08.

## Tâches, RED nommés, mutations

| Tâche | RED (commit) | GREEN (commit) | Mutations (rouge attendu → constaté) | sha256 avant = après |
|---|---|---|---|---|
| 1 — squelette : trois grilles, pistes aux hauteurs des tokens, axe des jours | 4 rouges, `078375c` (`Le_style_Pistes…`, `Le_style_Simplifie…`, `Le_style_Tuiles…`, `La_bascule_de_style…`) | `c33b6ed` (18 / 18 sur le filtre VueSemaine + Garde + HistoriqueBinding) | g1 NIVEAU de Pistes sur `HistoHauteurNiveauTuiles` → `Le_style_Pistes…` ; g2 `PisteRythme` retirée de Tuiles → `Le_style_Tuiles…` + `La_bascule…` ; g3 `Variante="SemaineComplete"` en Tuiles → `Le_style_Tuiles…` ; g4 `Height="150"` → `GardeTokensHistoriqueTests.Aucune_couleur_ni_taille_en_dur…` | `6630ea48364f4940` (×4) |
| 2 — annotations, zone avant journal, surcouche + infobulle, pieds, 760 → 1 400 | 5 rouges, `1e6bae8` (`Les_annotations…`, `La_zone_avant_le_journal…`, `Le_libelle_permanent…`, `La_surcouche…`, `A_760_px…`) ; `Les_pistes_recoivent…` déjà vert (voir Deviations) | `507c826` (24 / 24 sur le filtre) | n1 `ItemsSource=AnnotationsTrous` retiré de Tuiles → `Les_annotations…` ; n2 `PiedDivergence` figé `Visible` → `Le_libelle_permanent…` ; n3 `Fuseau` non bindé (×3) → `La_surcouche…` ; n4 `OpaciteTrou="0.5"` → `Les_pistes_recoivent…` SEUL (la garde reste verte : limite notée) | `3bd567007515cb82` (×4) |

Toutes les mutations ont été jouées par copie dans l'instantané `snap-34-06` (`git archive c33b6ed` + mes fichiers copiés depuis l'arbre réel), révoquées par re-copie ; sha256 identiques à chaque fois ; chaque rouge est le test attendu, seul (g2 : les deux tests qui lisent la liste Tuiles).

## Verification

- **Arbre réel** : `dotnet test Chronos.sln -c Debug --nologo -v q` → **1549 / 1549 verts, deux exécutions consécutives** (14:42:15Z et 14:42:58Z ; 1531 d'entrée + 10 de ce plan + 8 de 34-07). `dotnet build Chronos.sln -c Debug` : **0 warning**.
- Greps d'acceptation Task 1 : `x:Name="Style…"` 3 · `<hc:PisteNiveau` 3 · `<hc:PisteRythme` 2 · `<hc:PisteTokens` 3 · `<hc:PisteCouverture` 3 · `<hc:PisteFenetres5h` 1 · `Variante="SemaineHebdoSeul"` 1 · `HistoHauteur(…9 clés…)` 9 · `HistoHauteurCouverture` 3 · `ScrollViewer` ≥ 1 · `TextesHistorique.ResetHebdo` 1 · littéraux (hex / FontSize / Height / StrokeThickness) **0** · `DynamicResource TickReset` 4 · `DynamicResource Alerte` 6 ; code-behind : `InitializeComponent` 1, 10 lignes ; tests : `[WpfFact]` 4 puis 10, `150d` présent.
- Greps Task 2 : `AnnotationsTrous` 3 · `AnnotationsSauts` 3 · `AnnotationJournalOuvert` 8 · `x:Name="ZoneAvantJournal` 3 · `<hc:SurcoucheReticule` 3 · `x:Name="Infobulle` 3 · `LibellePermanentTokens` 3 · `LegendeTokens` 3 · `x:Name="PiedDivergence"` 1 · `PiedDePage` 1 · `HistoHachure` **7** (6 fonctionnels : 3 zones + 3 pistes Tokens, + 1 mention dans le commentaire d'en-tête) · `Fuseau` 3 · littéraux 0 ; tests : `760` et `1400` présents.
- Périmètre : mes quatre commits de code ne touchent que `VueSemaineView.xaml(.cs)`, `VueSemaineBindingTests.cs` et UNE ligne de `HistoriqueBindingTests.cs` ; `git diff --stat a5bccd4 HEAD -- VueJourView.xaml HistoriqueWindow.xaml Controls ViewModels Resources` ne montre que `VueJourView.xaml` (commits du voisin 34-07). ROADMAP.md / STATE.md non modifiés.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `UseLayoutRounding` sur la racine de la vue faussait les hauteurs**
- **Found during:** Task 1 GREEN (`ActualHeight` 150,4 / 89,6 / 62,4 au lieu de 150 / 90 / 62)
- **Issue:** posé sur le `UserControl`, `UseLayoutRounding=True` arrondit au pixel physique à 125 % d'échelle Windows ; la table §2.2 est en DIP.
- **Fix:** retiré de la vue (hérité de la fenêtre hôte, qui le pose déjà) ; commentaire dans le XAML.
- **Commit:** `c33b6ed`

**2. [Rule 1 - Test] `OnRender` n'est pas déclenché par `RenderTargetBitmap.Render`**
- **Found during:** Task 2 RED (`Les_pistes_recoivent…` : trace vide)
- **Issue:** `OnRender` d'un visuel invalidé est appelé à la fin de son `Arrange` ; le test invalidait puis rendait sans remettre en page.
- **Fix:** `InvalidateVisual()` → `vue.UpdateLayout()` → `Render` ; commentaire dans le test. Conséquence : ce test est passé au vert AVANT le GREEN de la Task 2 (tous les bindings de pistes étaient déjà livrés par la Task 1, comme le XAML de la Task 1 du plan le prescrivait) — **RED 5 + 1 déjà vert**, dit dans le message du commit `1e6bae8`.

**3. [Rule 3 - Blocking] Deux collisions de build avec le voisin 34-07**
- Le BAML de `VueSemaineView` n'a pas été régénéré par un premier build (779 octets du stub) : `touch` + rebuild du projet applicatif. Puis les `.g.cs` d'`obj` ont disparu pendant un build croisé (`CS2001`) : attente 45 s, nouvel essai vert (règle des trois essais). Les mutations ont été jouées dans `snap-34-06` pour ne jamais exposer un XAML muté aux builds du voisin.

### Écarts assumés (à dire, pas à masquer)

- **Défilement testé à 360 px de haut, pas 480** : le plan écrit « `ScrollableHeight > 0` à 480 » ; or à 480 la vue seule tient (≈ 440 px en Pistes). La hauteur réellement disponible pour la vue à la taille minimale de la fenêtre est 480 − 92 (en-tête) − marges ≈ 360 px : c'est là que le test exige le défilement (et 0 à 900). D-34-28 reste vraie dans la fenêtre.
- **`Canvas.Left` des annotations lu sur le conteneur** (`ContentPresenter` généré par l'`ItemsControl`), pas sur le `TextBlock` : le test remonte au premier ancêtre posé dans un `Canvas` (`PositionDe`).
- **Plage des annotations = `DonneesSemaine.Plage`**, pas `PlageCourante` (D-34-29 du plan) : les annotations viennent du même record immuable que les pistes ; pendant une navigation ‹ ›, `PlageCourante` change avant `DonneesSemaine` et les annotations de l'ancienne semaine auraient été projetées sur la nouvelle plage.
- **Colonnes `Auto` + largeurs explicites** au lieu de `ColumnDefinition Width="{StaticResource HistoLargeurLibelles}"` : un `sys:Double` n'est pas un `GridLength` ; les cellules (`CelluleLibelles` / `CelluleDroite`) portent la largeur et la marge, identiques dans l'axe et les grilles.
- **Repères « 100 % » / « 0 »** de NIVEAU (D-34-29) non posés : littéraux visibles interdits hors `TextesHistorique`, qui n'a pas ces constantes (fichier de 34-03, hors périmètre). La grille 0 / 50 / 100 % reste dessinée par la piste.
- **`HistoHachure` = 7** occurrences (plan : 6) : la septième est dans le commentaire d'en-tête du XAML.
- **`Alerte` dans la vue** résout la valeur STATIQUE du dictionnaire fusionné dans la vue (`#EFA23A`), pas le pinceau du thème injecté dans les ressources de la fenêtre (la vue fusionne `DesignTokens.xaml` pour se monter seule ; `DynamicResource` s'arrête au premier dictionnaire qui porte la clé). Pour le thème par défaut c'est la même couleur ; pour un autre thème, 34-08 peut injecter `vm.Theme.BrushTokens()` dans `vue.Resources` depuis la fenêtre (une boucle, aucune logique de vue) ou retirer la fusion en montant la vue dans une fenêtre de test. `TickReset` n'est pas dans `BrushTokens()` : toujours le token statique (`#F4F2EC`, attendu par le test).
- **Infobulle non bornée au bord droit** : `Canvas.Left = XReticule` tel quel ; près du samedi, elle dépasse dans la colonne de droite (revue DAEDALUS).
- **`HistoriqueViewModel.PlafondJoli` NON remplacé** par `EchelleValeur.MaxArrondi` : le plan 34-06 ne le prévoit pas et la règle d'exécution parallèle limite mes fichiers à ceux du plan (« si le plan le prévoit »). Le stub reste tel que documenté par 34-03 / 34-04 : `PlafondTokens = EchelleValeur.MaxArrondi(max)` (une ligne + `using Chronos.Rendering.Historique;`, suppression de `PlafondJoli` ; seule différence de valeur : `max ≤ 0` → 1 au lieu de 0, donc « 0 – 1 (sortie) » sur une semaine sans tokens). À faire en 34-08.

## Limites notées pour 34-08

1. **Garde 34-01 à élargir** : `OpaciteTrou="0.5"` (mutation n4) n'est pas vu par `(FontSize|Height|MinHeight|StrokeThickness|Thickness|Opacity)="[0-9]` ; proposer `(Opacite\w*|Epaisseur\w*|Longueur\w*|Plafond|NbBandes|Max)="[0-9]` et `Canvas\.(Left|Top)="[0-9]`. Le `Height="150"` (g4), lui, est bien vu.
2. Anti-mutisme de la garde : `Views/Historique` porte maintenant 3 XAML (fenêtre + 2 vues).
3. Le test `HistoriqueBindingTests.Les_deux_vues_sont_hebergees_et_encore_vides` garde son nom (partagé avec 34-07, qui adapte son assertion) : à renommer en 34-08 (« …sont_hebergees_et_remplies »).
4. Repères « 100 % » / « 0 » : ajouter `TextesHistorique.RepereCent` / `RepereZero` si voulus.

## Known Stubs

- `HistoriqueViewModel.PlafondJoli` (fichier de 34-03) : stub connu, hors de mes fichiers — voir ci-dessus, reporté à 34-08. Il ne bloque rien : la DP `Plafond` et `EchelleTokens` viennent déjà de la même valeur (`PlafondTokens`).
- Aucun stub dans la vue : chaque `TextBlock` est bindé au VM ou à une constante de `TextesHistorique`, chaque piste reçoit ses données, ses brosses et ses tailles (vérifié par `Les_pistes_recoivent_les_tokens_et_le_theme`).

## Commits

- `078375c` test(34-06): vue Semaine — ordre et hauteurs exacts par style, variante Niveau, bascule à chaud, axe des jours, libellés, RED 4
- `c33b6ed` feat(34-06): VueSemaineView — trois grilles de style aux hauteurs des tokens, pistes bindées, axe des jours, reset hebdo (HIS-02, HIS-03)
- `1e6bae8` test(34-06): annotations, zone avant journal, pieds, surcouche + infobulle, tokens et thème, 760 → 1400, RED 5 (+1 déjà vert)
- `507c826` feat(34-06): VueSemaineView — annotations d'honnêteté, zone avant journal hachurée, surcouche + infobulle, libellé permanent, légende, pieds (HIS-02, HIS-06)

## Self-Check: PASSED

- FOUND : `VueSemaineView.xaml`, `VueSemaineView.xaml.cs`, `VueSemaineBindingTests.cs`, `HistoriqueBindingTests.cs`, ce SUMMARY.
- FOUND commits : `078375c`, `c33b6ed`, `1e6bae8`, `507c826` (`git cat-file -e`) ; chacun ne touche que les fichiers du plan (+ une ligne de `HistoriqueBindingTests.cs`).
- Suite 1549 / 1549 verte deux fois sur l'arbre réel, 0 warning ; `requirements mark-complete HIS-02 HIS-03` exécuté (`updated: true`), `REQUIREMENTS.md` stagé depuis un blob HEAD ne portant que ces deux lignes (la ligne HIS-04 de l'arbre appartient à 34-07).
- ROADMAP.md / STATE.md non modifiés ; `state *` et `roadmap update-plan-progress` laissés à l'orchestrateur (exécution parallèle).
