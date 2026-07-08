---
phase: 05-cadran-ringarc-converters-c-blage-view
plan: 01
subsystem: ui
tags: [wpf, geometry, arcsegment, ellipsegeometry, color-interpolation, tdd, xunit, stafact]

# Dependency graph
requires:
  - phase: 04-orchestration-refresh-viewmodel-temps-r-el
    provides: "WindowGaugeViewModel.FractionRemaining (0..1) et Utilization (double?) — surface de binding consommée par les arcs"
provides:
  - "ArcGeometry.PointAt (repère WPF Y-inversé) + Build (Empty / EllipseGeometry / ArcSegment) — géométrie angle→arc pure"
  - "RampColor.Interpolate(double) → Color — rampe utilization vert→ambre→rouge, 3 stops linéaires"
affects: [05-02, RingArc, TickRing, UtilizationToBrushConverter]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Math pure isolée dans Chronos.Rendering/ (aucun état, aucun I/O) — testable sans logique WPF"
    - "Anneau plein via EllipseGeometry (fraction ≥ 1) plutôt que clamp 359.9° — évite cas dégénéré 360°"
    - "isLargeArc = sweep > 180.0 (condition stricte) — piège #1 WPF résolu"

key-files:
  created:
    - src/Chronos/Rendering/ArcGeometry.cs
    - src/Chronos/Rendering/RampColor.cs
    - tests/Chronos.Tests/ArcGeometryTests.cs
    - tests/Chronos.Tests/RampColorTests.cs
  modified: []

key-decisions:
  - "Stop ambre verrouillé à AmberStop = 0.55 (constante unique, ajustable en UAT)"
  - "fraction ≥ 1 → EllipseGeometry (anneau plein sans micro-fente) au lieu de clamper à 359.9°"
  - "isLargeArc via sweep > 180.0 stricte : fraction 0.5 (sweep 180° pile) → IsLargeArc false"

patterns-established:
  - "Rendering/ = briques de calcul PUR (trigonométrie + interpolation), enveloppées ensuite par des Shape/Converter minces"
  - "Tests géométriques en [WpfFact] (STA) dès qu'une Geometry WPF est construite ; Color (struct) reste en [Fact]"

requirements-completed: [CAD-04, CAD-07]

# Metrics
duration: 3 min
completed: 2026-07-08
---

# Phase 5 Plan 1 : ArcGeometry + RampColor Summary

**Deux fonctions de calcul PURES et testées — géométrie angle→arc WPF (Empty/EllipseGeometry/ArcSegment avec IsLargeArc correct) et interpolation RGB de la rampe utilization vert #7BB13C → ambre #EFA23A → rouge #D8503A.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-07-08T16:32:51Z
- **Completed:** 2026-07-08T16:35:28Z
- **Tasks:** 2 (TDD, RED→GREEN chacune)
- **Files modified:** 4 créés

## Accomplishments
- `ArcGeometry` : `PointAt` (repère Y-inversé, 0° = 12 h, sens horaire) + `Build` gérant les 3 cas verrouillés — sweep 0 (Geometry.Empty, aucune exception), anneau plein (EllipseGeometry, pas de micro-fente 360°), ArcSegment ouvert avec `IsLargeArc = sweep > 180.0`.
- `RampColor.Interpolate` : rampe 3 stops linéaire par canal, passant exactement par les 3 couleurs de la maquette, clampée hors [0,1].
- 16 nouveaux tests (10 ArcGeometry en `[WpfFact]`, 6 RampColor en `[Fact]`) — suite complète à 57 tests verts, aucune régression sur les 41 existants.

## Task Commits

Cycle TDD par tâche (RED puis GREEN — aucun refactor nécessaire, code verbatim du RESEARCH) :

1. **Task 1 : ArcGeometry** — `43176f8` (test RED) → `d4482fb` (feat GREEN)
2. **Task 2 : RampColor** — `7346314` (test RED) → `661bb40` (feat GREEN)

## Files Created/Modified
- `src/Chronos/Rendering/ArcGeometry.cs` — classe statique pure : `PointAt` (angle→point Y-inversé) + `Build` (fraction→Geometry).
- `src/Chronos/Rendering/RampColor.cs` — classe statique pure : `Interpolate` (utilization→Color, 3 stops).
- `tests/Chronos.Tests/ArcGeometryTests.cs` — 10 `[WpfFact]` couvrant PointAt (0°/90°), cas Empty (0/négatif/NaN), plein (1/1.5), IsLargeArc (0.25/0.5/0.75).
- `tests/Chronos.Tests/RampColorTests.cs` — 6 `[Fact]` couvrant 3 stops exacts, 2 clamps, continuité mi-segment.

## Decisions Made
- **AmberStop = 0.55** : position du stop ambre dans la fourchette « ~0.5-0.6 » du CONTEXT, constante unique ajustable en UAT.
- **EllipseGeometry pour fraction ≥ 1** : anneau plein continu, évite le cas dégénéré départ = arrivée d'un ArcSegment 360° (arc invisible pile quand la fenêtre est pleine) et la micro-fente d'un clamp à 359.9°.
- **`isLargeArc: sweep > 180.0` stricte** : `Build(0.5)` (sweep 180° pile) donne `IsLargeArc = false`, comportement testé explicitement.

## Deviations from Plan

None - plan executed exactly as written.

Le code des deux classes a été copié verbatim depuis les Patterns 2 et 5 du 05-RESEARCH.md, comme prescrit par le plan. Les fichiers de test couvrent exactement les cas `<behavior>` listés.

**Total deviations:** 0
**Impact on plan:** aucun — exécution conforme.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `ArcGeometry.Build` et `ArcGeometry.PointAt` sont prêts à être enveloppés par `RingArc` et `TickRing` (05-02) via `DefiningGeometry`.
- `RampColor.Interpolate` est prêt à être appelé par `UtilizationToBrushConverter` (05-02).
- Aucun blocage. Prêt pour 05-02.

---
*Phase: 05-cadran-ringarc-converters-c-blage-view*
*Completed: 2026-07-08*

## Self-Check: PASSED

- All 4 created files verified on disk.
- All 4 task commits verified in git history (43176f8, d4482fb, 7346314, 661bb40).
