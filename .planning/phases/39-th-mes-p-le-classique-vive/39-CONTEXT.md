# Phase 39: Thèmes Pâle / Classique / Vive - Context

**Gathered:** 2026-10-03 · **Status:** Ready for planning · **Source:** plan de design VALIDÉ `.zeus/DESIGN_PLAN_CYCLE2.md` § 5

<decisions>
## Implementation Decisions
- Catégories (propriété sur `ChronosTheme`) : **Pâle** = Nord, Forêt, Moka, Roseraie, Sauge, Lavande ; **Classique** = Minuit
  (défaut), Ardoise, Ambre chaud, Graphite, Marine ; **Vive** = Néon, Aurore, Synthwave, Lave. Section Thème des réglages :
  trois groupes titrés (10,5 semi-gras, `Ink2`), vignettes 88 × 76 inchangées.
- Nouvelles palettes (disque · piste · graduation · texte · vert · ambre · rouge), valeurs EXACTES :
  - Sauge `#262B28 #343B37 #AEB8B0 #DCE3DD #8FB996 #D9C27E #D08A7E`
  - Lavande `#22202C #302D3D #C3BCD9 #E6E2F2 #9FCFB0 #E8C88E #E08E9E`
  - Graphite `#121314 #26282B #C4C7CC #F2F3F5 #6DBE45 #F0A830 #E04B3C`
  - Marine `#0F1A2A #1E2D44 #B9C9DE #EAF0F7 #5DBB7A #F2B544 #E25C4F`
  - Synthwave `#140B24 #2A1745 #9AE6FF #F5EEFF #2BFF88 #FFD000 #FF2D55`
  - Lave `#1A0E0A #33190F #FFC9A3 #FFF1E6 #7CFF4F #FFB000 #FF3B1F`
  - Corrections : Néon ambre → `#FFC23D`, rouge → `#FF2E63` ; Aurore ambre → `#F0C36D`, rouge → `#F2577A` (décors inchangés).
- Gris épuisé : dans `From()`, plus petit mélange `Lerp(piste, graduation, t)` (t ≥ 0,18, pas de 0,01) qui atteint un contraste
  WCAG ≥ 3:1 contre le disque. Tests sur tout le catalogue : épuisé ≥ 3:1 ; rouge de rampe en teinte 335°–20° et ≥ 3:1 contre le
  disque ; `Alerte` = ambre ≠ rouge (garde existante). Le test « exactement 9 thèmes » devient un test d'invariants + 15 thèmes.
- `TickReset` entre dans `BrushTokens()` (thémé). `HistoGris` et le gris figé de `UtilizationToBrushConverter` suivent le gris
  épuisé du thème si c'est faisable sans casser l'Historique (sinon le noter dans le SUMMARY).
- Braises / Fusible / Marée / Volets : TOUTES les couleurs fixes de leurs vues (`#A9A8B2`, `#F4F2EC`, `#C7C6D0`, `#211D2A`,
  `#161019`, `#33000000`, `#55000000`, etc.) passent sur des pinceaux du thème (`TextePrincipal`, `TexteSecondaire`, un pinceau
  de tuile dérivé, un texte sur plaque) en `DynamicResource`. ATTENTION conflit de fichiers : la phase 40 redessinera ces vues ;
  ici on ne fait QUE remplacer les couleurs, sans toucher la géométrie.
- Hors périmètre : fonds clairs ; fenêtres Réglages et Historique (restent sombres).

### Claude's Discretion
Forme du regroupement (CollectionViewSource + GroupStyle ou trois ItemsControl) ; nom du pinceau de tuile.
</decisions>

<canonical_refs>
- `.zeus/DESIGN_PLAN_CYCLE2.md` § 5 ; maquette § 5 ; `.zeus/reports/cycle2/inventaire-themes-historique.md` § A (tests à adapter)
</canonical_refs>

<deferred>Café, Tropique (réserve) ; thèmes clairs.</deferred>
