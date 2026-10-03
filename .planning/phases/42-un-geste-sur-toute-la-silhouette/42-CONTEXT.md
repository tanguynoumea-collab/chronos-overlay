# Phase 42: Un geste sur toute la silhouette - Context

**Gathered:** 2026-10-03 · **Status:** Ready for planning · **Source:** plan de design VALIDÉ `.zeus/DESIGN_PLAN_CYCLE2.md` § 2

<decisions>
## Implementation Decisions
- Remplacer le `CentreHit` (ellipse 66 × 66 partagée, `MainWindow.xaml:191-193`) par **un geste unique sur toute la silhouette**
  de chaque variante : clic sans déplacement → bascule % ↔ temps ; double-clic → Historique sans bascule (`ArbitreClicCentre`
  inchangé, `GetDoubleClickTime`) ; appui puis déplacement au-delà de `SystemParameters.MinimumHorizontalDragDistance` /
  `MinimumVerticalDragDistance` → `DragMove()` puis `SnapToNearestCorner()` ; clic droit → réglages ; hors silhouette → le clic
  traverse vers le bureau.
- Silhouettes : disque de rayon 74 (centre de l'empreinte 170) pour Arcs et Braises ; rectangle arrondi (rayon 10) englobant le
  cadran avec 6 px de marge pour Fusible, Marée, Volets dans les deux orientations. Peintes avec le token `ZoneSilhouette`
  = `#01000000` (fenêtre layered : un pixel alpha 0 laisse passer le clic). Chaque vue de cadran déclare sa silhouette ; un seul
  répartiteur de gestes dans `MainWindow`. Les pastilles d'Arcs gardent leurs propres clics (Handled).
- Tests : (1) rendu `RenderTargetBitmap` par cadran × orientation : alpha > 0 aux points témoins dans la silhouette, = 0 hors ;
  (2) `VisualTreeHelper.HitTest` de routage aux mêmes points ; (3) garde statique : aucun élément portant un geste n'a de
  pinceau nul ou `Transparent` ; (4) fonction pure de décision clic / glisser (seuil) testée. `GardeGestesCadranTests` (chaînes
  exactes) est réécrit pour le nouveau répartiteur. NE PAS se fier à `HitTest` seul (Transparent y est « touché »).
- Corriger les commentaires faux « Transparent suffit » (`MainWindow.xaml:249, 269-270, 295-296`).
- Carte Historique des réglages : « Aussi : double-clic sur le cadran » (au lieu de « au centre du cadran ») ; README / docs
  des gestes mis à jour.

### Claude's Discretion
Forme du répartiteur (propriété attachée vs nom d'élément) ; points témoins.
</decisions>

<canonical_refs>
- `.zeus/DESIGN_PLAN_CYCLE2.md` § 2 ; maquette § 1-2 (bouton « Montrer les zones de clic ») ;
  `.zeus/reports/cycle2/inventaire-cadrans-gestes.md` § 3, § 6 ; `.zeus/reports/llm-council-2026-10-03.md` § 3
</canonical_refs>

<deferred>None</deferred>
