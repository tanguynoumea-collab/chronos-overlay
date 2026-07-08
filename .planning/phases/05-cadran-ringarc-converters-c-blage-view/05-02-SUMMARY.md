---
phase: 05-cadran-ringarc-converters-c-blage-view
plan: 02
subsystem: ui
tags: [wpf, shape, dependencyproperty, affectsrender, ivalueconverter, geometrygroup, tdd, xunit, stafact]

# Dependency graph
requires:
  - phase: 05-cadran-ringarc-converters-c-blage-view
    provides: "ArcGeometry.Build/PointAt (géométrie angle→arc pure) + RampColor.Interpolate (rampe couleur) — enveloppés ici par des Shape/Converter minces (05-01)"
provides:
  - "RingArc : Shape réutilisable, DP Fraction/StartAngle/Radius (AffectsRender), DefiningGeometry → ArcGeometry.Build (CAD-07)"
  - "TickRing : Shape de graduations en UN seul GeometryGroup de LineGeometry, DP Count/Radius/TickLength/StartAngle (CAD-01)"
  - "UtilizationToBrushConverter : double? → Brush (rampe [0,1[ / gris épuisé ≥1 / neutre null), honnêteté sur donnée absente (CAD-04/05)"
affects: [05-03, MainWindow.xaml, cablage-vue]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Enveloppes WPF minces : Shape dérivé exposant des DP AffectsRender, DefiningGeometry déléguant à la math pure de Rendering/ (aucune logique en code-behind)"
    - "DP Fraction 0..1 (pas EndAngle en degrés) → binding direct sur FractionRemaining, sweep interne = 360×Fraction"
    - "Graduations en une passe : GeometryGroup de LineGeometry = 1 seul visuel (jamais ItemsControl de 60 items) sous fenêtre layered"
    - "Converter mono-entrée (IValueConverter, pas MultiBinding) ; brushes statiques gelés (Freeze) partageables"

key-files:
  created:
    - src/Chronos/Controls/RingArc.cs
    - src/Chronos/Controls/TickRing.cs
    - src/Chronos/Converters/UtilizationToBrushConverter.cs
    - tests/Chronos.Tests/UtilizationToBrushConverterTests.cs
  modified: []

key-decisions:
  - "RingArc/TickRing dérivent de Shape (pas UserControl) : géométrie = pur produit des DP, redessin auto via AffectsRender au tick 1 s sans animation"
  - "DP Fraction 0..1 plutôt que EndAngle : binding direct sur WindowGaugeViewModel.FractionRemaining, aucun converter d'angle"
  - "TickRing : deux instances XAML prévues (mineurs Count=60 / majeurs Count=12) plutôt qu'une DP MajorEvery branchée"
  - "Converter mono-entrée : la provenance (IsEstimated) ne change PAS la couleur d'arc (badge séparé) ; null → neutre #2A2932 (jamais de couleur inventée)"

patterns-established:
  - "Controls/ = Shape minces paramétrés par DP AffectsRender, déléguant à Rendering/ (math pure testée) via DefiningGeometry"
  - "Converters/ = IValueConverter mono-entrée testé en [WpfFact] (SolidColorBrush = DispatcherObject → STA)"

requirements-completed: [CAD-01, CAD-04, CAD-05, CAD-07]

# Metrics
duration: 2 min
completed: 2026-07-08
---

# Phase 5 Plan 2 : RingArc + TickRing + UtilizationToBrushConverter Summary

**Trois enveloppes WPF minces sur la math pure de 05-01 : `RingArc` (Shape d'arc à DP Fraction 0..1 AffectsRender), `TickRing` (graduations en un seul GeometryGroup) et `UtilizationToBrushConverter` (double? → rampe / gris épuisé #5A5960 / neutre #2A2932 sur donnée absente) — le vocabulaire visuel que le XAML de 05-03 assemblera.**

## Performance

- **Duration:** 2 min
- **Started:** 2026-07-08T16:38:20Z
- **Completed:** 2026-07-08T16:40:53Z
- **Tasks:** 3 (dont 1 TDD, RED→GREEN)
- **Files modified:** 4 créés

## Accomplishments
- `RingArc` : Shape réutilisable exposant `Fraction`/`StartAngle`/`Radius` en DependencyProperty `AffectsRender` ; `DefiningGeometry` délègue à `ArcGeometry.Build` (math testée 05-01). Sert aussi de piste à `Fraction=1`. Aucune logique, aucun EndAngle (CAD-07).
- `TickRing` : Shape rendant toutes les graduations en UNE passe (`GeometryGroup` de `LineGeometry`, un seul visuel — pas 60 éléments) via `ArcGeometry.PointAt` ; 4 DP `AffectsRender` + garde `Count <= 0` (CAD-01).
- `UtilizationToBrushConverter` : `double?` → `Brush` sur 3 branches — rampe `[0,1[` (`RampColor.Interpolate`), gris épuisé `#5A5960` pour `≥1` (CAD-05), neutre `#2A2932` pour `null`/non-double (honnêteté DAT-08 : jamais de couleur inventée). Brushes statiques gelés.
- 7 nouveaux tests `[WpfFact]` couvrant les 3 branches sémantiques + brushes gelés — suite complète à 64 tests verts, aucune régression sur les 57 existants.

## Task Commits

Chaque tâche committée atomiquement (Task 3 en cycle TDD RED→GREEN, aucun refactor nécessaire : code verbatim du RESEARCH) :

1. **Task 1 : RingArc** — `b25bf16` (feat)
2. **Task 2 : TickRing** — `27cac4b` (feat)
3. **Task 3 : UtilizationToBrushConverter** — `8ab8cc9` (test RED) → `88dc3fd` (feat GREEN)

## Files Created/Modified
- `src/Chronos/Controls/RingArc.cs` — Shape d'arc : 3 DP AffectsRender, `DefiningGeometry → ArcGeometry.Build`.
- `src/Chronos/Controls/TickRing.cs` — Shape de graduations : 4 DP AffectsRender, `DefiningGeometry` = GeometryGroup unique de LineGeometry via `ArcGeometry.PointAt`.
- `src/Chronos/Converters/UtilizationToBrushConverter.cs` — `IValueConverter` mono-entrée : rampe / gris épuisé / neutre null, brushes gelés.
- `tests/Chronos.Tests/UtilizationToBrushConverterTests.cs` — 7 `[WpfFact]` (rampe, 0→vert, 1→gris, 1.4→gris, null→neutre, non-double→neutre, IsFrozen).

## Decisions Made
- **Shape (pas UserControl)** pour RingArc/TickRing : la géométrie est le pur produit des DP, redessin auto via `AffectsRender` au tick 1 s (RAF-03) sans animation — décision verrouillée par le plan/RESEARCH.
- **DP `Fraction` 0..1 (pas `EndAngle`)** : binding direct sur `WindowGaugeViewModel.FractionRemaining`, sweep interne `360×Fraction`, aucun converter d'angle.
- **Converter mono-entrée** : la provenance (`IsEstimated`) ne pilote PAS la couleur d'arc (badge texte séparé en 05-03) ; `null` → neutre `#2A2932`, jamais une couleur de rampe (honnêteté).

## Deviations from Plan

None - plan executed exactly as written.

Les trois classes ont été copiées verbatim depuis les Patterns 1, 3 (complété en Shape à 4 DP) et 4 du 05-RESEARCH.md, comme prescrit. Les tests couvrent exactement les cas `<behavior>`.

**Total deviations:** 0
**Impact on plan:** aucun — exécution conforme.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `RingArc`, `TickRing` et `UtilizationToBrushConverter` sont instanciables tels quels dans `MainWindow.xaml` (05-03) : arc extérieur (5 h) + intérieur (hebdo) bindés sur `FiveHour`/`SevenDay`, ticks mineurs/majeurs empilés, couleur d'arc via le converter en StaticResource.
- Aucun blocage. Prêt pour 05-03 (câblage de la vue).

---
*Phase: 05-cadran-ringarc-converters-c-blage-view*
*Completed: 2026-07-08*

## Self-Check: PASSED

- All 4 created files verified on disk.
- All 4 task commits verified in git history (b25bf16, 27cac4b, 8ab8cc9, 88dc3fd).
