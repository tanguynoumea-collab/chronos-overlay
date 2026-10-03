# Inventaire cycle 2 — cadrans, gestes, fenêtre principale (R1, R2, R3, R7)

Agent Explore, lecture seule, 2026-10-03. Chemins relatifs à `src/Chronos/`.

## Constats structurants
- Fenêtre principale **170 × 170 en dur** (`Views/MainWindow.xaml:8-9`, `SizeToContent=Manual`), testée à 170 (`CadranBindingTests.cs:291-292, 675-676`, `ReglagesWindowTests.cs:430`).
- Les 4 styles alternatifs sont dans un **`Viewbox Stretch=Uniform`** (marge 4 Braises, 6 sinon ; `MainWindow.xaml:171-186`) → **agrandir le contenu de 20 % ne change rien à l'écran**. Rendus effectifs : Fusible ≈ 158 × 77 px (facteur 0,752), Volets ≈ 158 × 54 px (0,675), Marée ≈ 131 × 158 (0,99), Braises facteur 0,953.
- **Normal/Étendu n'existe que pour Arcs** (`MainWindow.xaml:68,110` ; carte masquée hors Arcs `ReglagesWindow.xaml:814-816`). La mention R2 « modes Normal et Étendu » pour Fusible/Volets ne correspond à rien.
- Tailles et couleurs des 4 vues alternatives **en dur** (aucun token de taille de cadran dans `DesignTokens.xaml`).
- Orientation **codée en dur** dans `FuseBar` (horizontal), `TideColumn` (vertical), `FlapRow` (horizontal).

## Cadrans
- **Arcs** (`MainWindow.xaml:25-166`) : `RingArc`, `TickRing`, `Rendering/ArcGeometry.cs`. Disque `FondCadran` opaque 64 px. Hebdo R38 ép. 8. Étendu : 5 h R54, 5 ticks R59, 24 h R64, resets R68. Normal : 24 h R52 coloré par l'usage 5 h, sous-tirets `DaySubTickAngles` R55,5, resets `DayResetAngles` R57. Rayon max 71,5.
- **Braises** (`Views/Cadrans/CadranBraisesView.xaml`, `Controls/Cadrans/EmberRingControl.cs`) : 5 h R66 **16 pastilles** (`FiveHour.FractionRemaining`), hebdo R44 12 pastilles ; allumées = round(fraction × N), départ midi sens horaire. L'anneau 5 h représente **la fenêtre de 5 h elle-même**, pas la journée (1 pastille = 18,75 min). Aucune marque de reset.
- **Fusible** (`CadranFusibleView.xaml`, `FuseBar.cs:48-88`) : StackPanel 210 de large, barres 26/24 de haut, ≈ 102 de haut.
- **Marée** (`CadranMareeView.xaml`, `TideColumn.cs:43-82`) : 2 colonnes 42 × 120, ≈ 132 × 159.
- **Volets** (`CadranVoletsView.xaml`, `FlapRow.cs:39-59`) : StackPanel 234, 2 lignes de 30, `FlapRow` 84 × 16 × 6 volets, ≈ 80 de haut.

## Grille des resets 5 h
- `Rendering/DayTimeline.cs` (pur) : `ResetAngles` (l.29-53), `SubTickAngles` (l.61-86), angles sur un tour de **24 h**. VM : `MainViewModel.cs:79-82`, recalcul chaque seconde `Interpolate` l.705-711. `WindowGaugeViewModel` n'expose pas `ResetsAt`.
- Pour Braises : `TickRing` superposable tel quel (centre 85,85). Deux options : divisions horaires internes à la fenêtre 5 h (5 × 72°, pastilles multiples de 5 : 15 ou 20), ou grille 24 h (n'a de sens que si Braises représente la journée).
- Galerie `CadranPreviewViewModel` ne fournit ni angles ni `IsModeEtendu`.

## Gestes (R7)
- Drag : `MouseLeftButtonDown` sur la fenêtre → `DragMove()` + `SnapToNearestCorner()` (`MainWindow.xaml.cs:50,66-71`).
- Clic centre : `Ellipse CentreHit 66 × 66 Fill=Transparent` (`MainWindow.xaml:191-193`) **identique pour les 5 styles, hors Viewbox** ; arbitre pur `ViewModels/ArbitreClicCentre.cs:48-71` (1 clic = bascule différée, ≥ 2 = Historique) ; `GetDoubleClickTime`.
- Clic droit : `Grid MouseRightButtonUp=OnRightClick` (`MainWindow.xaml:21`) — grille sans fond → ne marche que sur un enfant touché.
- **Fenêtre layered** : un pixel alpha 0 laisse passer le clic vers le bureau. Aucun `#01000000` dans MainWindow (seulement SessionsWindow / SessionStyles). Les commentaires « Transparent suffit, CentreHit le prouve » sont faux : CentreHit est posé sur le disque opaque FondCadran.
- Effets par style : Arcs — clic centre seulement r ≤ 32, entre-anneaux traversent ; Braises — pas de fond au centre, clic seulement sur les chiffres ; Fusible / Volets / Marée — le disque central ne recouvre pas les valeurs/étiquettes (qui déplacent au lieu de basculer), les interlignes traversent.

## Fenêtre, accroche, widget
- `OverlayController.SnapToNearestCorner` lit le rectangle réel (taille quelconque OK) ; **pas de recalage au changement de style**. `RestorePlacement` utilise ActualWidth/Height.
- Aperçu des réglages = copie du contenu réel de la fenêtre dans 144 × 144 (`ReglagesWindow.xaml.cs:83-116`).
- Widget sessions : fenêtre indépendante, `VerticalLayout` ne concerne que les rangées de pastilles (Sonar, Jetons, Veilleurs) — sans lien avec les cadrans.

## Réglages
- `CadranStyle`/`CadranMode` (`ChronosSettings.cs:105,109`), catalogue `MainViewModel.cs:417-424`, Apparence `ReglagesWindow.xaml:738-831`. Brancher l'orientation : propriété persistée + commande sur le modèle de `ToggleCadranMode`, booléen « style orientable » notifié dans `OnCadranStyleChanged`, carte après l.830. `ReglagesWindowTests.cs:303-345` liste les commandes.
- Galerie `--cadrans` (`CadranGalleryWindow`) : 4 tuiles, sans Arcs, sans Étendu, sans orientation.

## Tests
- `GardeGestesCadranTests.cs` (chaînes exactes des gestes — à adapter), `ArbitreClicCentreTests.cs`, `MainViewModelTests.cs`, `CadranBindingTests.cs` (Arcs uniquement, fenêtre réelle), `TickRingTests`, `DayTimelineTests`, `CornerSnapTests`.
- **Aucun test** sur Braises/Fusible/Marée/Volets ni sur leurs contrôles.
