# Phase 40: Cadrans à l'échelle 1 et orientations - Context

**Gathered:** 2026-10-03 · **Status:** Ready for planning · **Source:** plan de design VALIDÉ `.zeus/DESIGN_PLAN_CYCLE2.md` § 1

<decisions>
## Implementation Decisions
- **Plus de Viewbox** autour des cadrans dans `MainWindow.xaml` (l.171-186). Empreintes contractuelles (DIP) :
  Arcs 170 × 170 ; Braises 170 × 170 ; Fusible H 190 × 92 / V 110 × 190 ; Marée V 132 × 160 / H 190 × 96 ;
  Volets H 190 × 66 / V 128 × 190. Tokens `CadranLargeur*` / `CadranHauteur*` dans `DesignTokens.xaml` ; fonction pure
  `EmpreinteCadran(style, orientation) → Size` testée ; la fenêtre lie Width/Height à l'empreinte (plus de 170 × 170 en dur).
- Corps de texte non réduits : libellés 11, valeurs 12, plaque Volets 13 (H) / 14 (V) ; la géométrie suit (Fusible H : cordons
  10 / 8 dans un sillon de 5,4, front avec étincelle ; Volets H : tuile 26, plaque 90 × 26, 6 volets 62 × 14).
- **Ancrage** : à tout changement d'empreinte (style, orientation), la fenêtre reste collée à son **coin d'accroche courant**
  (pas au coin le plus proche recalculé) et ne déborde jamais (DPI mixte, multi-écrans) — `OverlayController` / `CornerSnap`.
- Orientations (plan § 1.2) : DP `Orientation` sur `FuseBar`, `TideColumn`, `FlapRow` + géométrie pure par axe ; deux gabarits
  par vue de cadran. Fusible V brûle **de haut en bas** (cordon restant en bas, étincelle au front) ; Marée H : lumière depuis la
  gauche, ligne d'eau verticale **légèrement ondulée** ; Volets V : deux colonnes (tuile 26, plaque 56 × 40, 6 volets empilés
  16 × 104). États « en attente » (HasData=false), « plancher » (Estimated) et « indisponible » repris dans les deux sens.
- Réglage : enum d'orientation persisté **par cadran** (Fusible, Marée, Volets ; défauts = H, V, H), lu de façon tolérante (phase
  36). Carte « Orientation » (puces Horizontal · Vertical) dans Apparence sous les styles, visible seulement pour ces trois
  styles (modèle : carte « Mode étendu » visible seulement pour Arcs) ; indépendante de `VerticalLayout` (widget de sessions).
- Aperçu des réglages : montre l'empreinte réelle réduite dans 144 × 144 (seul Viewbox conservé) ; le test qui vérifie
  `Rect(0,0,170,170)` s'adapte. Galerie `--cadrans` : huit variantes (Arcs compris).
- Les couleurs ont été thémées en phase 39 : conserver ces pinceaux en redessinant.
- Vérifier que les pastilles d'Arcs et le mot « indisponible » restent centrés dans l'empreinte courante.

### Claude's Discretion
Gabarits (DataTemplate/trigger vs deux sous-vues) ; nom de l'enum d'orientation ; structure de la fonction d'empreinte.
</decisions>

<canonical_refs>
- `.zeus/DESIGN_PLAN_CYCLE2.md` § 1 ; maquette `.zeus/maquettes/cycle2-cadrans-themes.html` § 1 (dessins de référence en SVG)
- `.zeus/reports/cycle2/inventaire-cadrans-gestes.md` ; `.zeus/reports/llm-council-2026-10-03.md` § 1-2
</canonical_refs>

<deferred>None</deferred>
