---
phase: 12-refonte-du-cadran-3-anneaux-remplissage-compacit
plan: 01
subsystem: ui
tags: [wpf, mvvm, rendering, tdd, xunit, arc-geometry, timeline]

# Dependency graph
requires:
  - phase: 05-cadran
    provides: ArcGeometry (math pure PointAt/Build), TickRing (GeometryGroup), RingArc, UtilizationToBrushConverter
  - phase: 11-usage-exact-oauth
    provides: WindowGaugeViewModel enrichi (FractionRemaining, Utilization, IsEstimated, CountdownText)
provides:
  - "WindowGaugeViewModel.FractionElapsed = clamp(1 − FractionRemaining) (remplissage inversé VIS-01)"
  - "PercentFormatter pur : % honnête FR (null → «», « ~ » si estimé, arrondi entier) (VIS-05)"
  - "WindowGaugeViewModel.UtilizationText + HasUtilizationText posés par Apply (VIS-05)"
  - "DayTimeline pur : Fraction (jour local) + ResetAngles (resets 5 h projetés sur axe 24 h) (JOUR-01, JOUR-02)"
  - "MainViewModel.DayFraction + DayResetAngles recalculés à chaque Interpolate (JOUR-01, JOUR-02)"
  - "TickRing.Angles : DP pour dessiner des ticks à angles arbitraires (JOUR-02)"
affects: [12-02-recomposition-xaml]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Math pure testable isolée du XAML (pattern ArcGeometry/CountdownFormatter de Phase 5)"
    - "DateTimeOffset offset-local pour une math de temps déterministe en test (indépendante du fuseau CI)"
    - "DP AffectsRender + IReadOnlyList<double> pour piloter un dessin data-driven (TickRing.Angles)"

key-files:
  created:
    - src/Chronos/Text/PercentFormatter.cs
    - src/Chronos/Rendering/DayTimeline.cs
    - tests/Chronos.Tests/DayTimelineTests.cs
    - tests/Chronos.Tests/TickRingTests.cs
  modified:
    - src/Chronos/ViewModels/WindowGaugeViewModel.cs
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/Controls/TickRing.cs
    - tests/Chronos.Tests/WindowGaugeViewModelTests.cs
    - tests/Chronos.Tests/MainViewModelTests.cs

key-decisions:
  - "TickRing.Angles = liste d'angles explicites plutôt que « réguliers + offset » : 24 h / 5 h = 4,8 (non entier) → une grille régulière produirait un dernier écart faux ; on dessine exactement les resets fournis par DayTimeline.ResetAngles."
  - "La grille des resets 5 h DÉRIVE d'un jour à l'autre (+4 h/jour) : c'est la réalité honnête des resets 5 h, pas un bug — ResetAngles normalise puis rembobine au 1er reset du jour de now."
  - "DayTimeline lit le .TimeOfDay/.Date du DateTimeOffset fourni ; l'appelant (MainViewModel) convertit now UTC via ToLocalTime() — math déterministe et testable."

patterns-established:
  - "Logique pure d'abord, XAML ensuite : toute la nouvelle math (inversion, angles 24 h, formatage %) est en fonctions pures testées avant la recomposition visuelle (plan 12-02)."

requirements-completed: [VIS-01, VIS-05, JOUR-01, JOUR-02]

# Metrics
duration: 6min
completed: 2026-07-09
---

# Phase 12 Plan 01 : Logique pure de la refonte du cadran Summary

**Math pure et testée de la refonte : remplissage inversé (FractionElapsed), % honnête (PercentFormatter/UtilizationText), timeline 24 h (DayTimeline fraction + angles des resets 5 h projetés) et TickRing.Angles — 21 nouveaux tests, aucun XAML touché.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-07-09T09:23:45Z
- **Completed:** 2026-07-09T09:29:49Z
- **Tasks:** 3
- **Files created/modified:** 9 (4 créés, 5 modifiés)

## Accomplishments
- **VIS-01** : `WindowGaugeViewModel.FractionElapsed = clamp(1 − FractionRemaining)` recalculée à chaque `Interpolate` — l'arc se remplit vers le reset.
- **VIS-05** : `PercentFormatter` pur (FR, arrondi entier) + `UtilizationText`/`HasUtilizationText` posés par `Apply` — « 80 % », « ~80 % » (estimé), «» (null) : honnêteté préservée.
- **JOUR-01/02** : `DayTimeline` pur (`Fraction` = minutes-jour/1440 ; `ResetAngles` = resets 5 h du jour projetés en angles triés) + `MainViewModel.DayFraction`/`DayResetAngles` rafraîchis chaque seconde.
- **JOUR-02** : `TickRing.Angles` (DP `AffectsRender`) dessine un trait par angle arbitraire, sans casser la boucle régulière `Count`.
- **Non-régression** : les 188 tests existants restent verts (209 au total).

## Task Commits

Chaque tâche committée atomiquement (TDD : RED build échoué → GREEN) :

1. **Task 1 : FractionElapsed + PercentFormatter + UtilizationText** - `3b4351c` (feat)
2. **Task 2 : DayTimeline + MainViewModel DayFraction/DayResetAngles** - `8a97e25` (feat)
3. **Task 3 : TickRing.Angles** - `655fe67` (feat)

## Files Created/Modified
- `src/Chronos/Text/PercentFormatter.cs` (créé) - Formatage honnête du % FR (null → «», « ~ » estimé, arrondi entier).
- `src/Chronos/Rendering/DayTimeline.cs` (créé) - Math pure timeline 24 h : fraction du jour + angles des resets 5 h.
- `src/Chronos/ViewModels/WindowGaugeViewModel.cs` (modifié) - `FractionElapsed`, `UtilizationText`, `HasUtilizationText`.
- `src/Chronos/ViewModels/MainViewModel.cs` (modifié) - `DayFraction` + `DayResetAngles` posés dans `Interpolate` (via `ToLocalTime`).
- `src/Chronos/Controls/TickRing.cs` (modifié) - DP `Angles` (ticks à angles arbitraires) + branche data-driven dans `DefiningGeometry`.
- `tests/Chronos.Tests/WindowGaugeViewModelTests.cs` (modifié) - 9 tests Elapsed/UtilizationText.
- `tests/Chronos.Tests/DayTimelineTests.cs` (créé) - 8 tests DayTimeline/DayTicks.
- `tests/Chronos.Tests/MainViewModelTests.cs` (modifié) - 1 test DayFraction/angles câblés par Interpolate.
- `tests/Chronos.Tests/TickRingTests.cs` (créé) - 3 tests [WpfFact] Angles / non-régression Count.

## Decisions Made
- **TickRing.Angles = angles explicites** (pas « réguliers + offset ») : 24 h / 5 h = 4,8 resets/jour (non entier) → une grille régulière produirait un dernier écart faux ; on dessine exactement les resets fournis par `DayTimeline.ResetAngles`.
- **Dérive assumée de la grille 5 h** (+4 h/jour) : les resets 5 h ne s'alignent pas sur une grille horaire fixe ; `ResetAngles` normalise par pas de 5 h vers le jour de `now` puis rembobine au 1er reset du jour — honnêteté visuelle.
- **`DayTimeline` lit le value fourni** (`.TimeOfDay`/`.Date` du `DateTimeOffset`) ; `MainViewModel` convertit `now` UTC via `ToLocalTime()` — math déterministe et testable indépendamment du fuseau machine.

## Deviations from Plan

None - plan executed exactly as written. Les propriétés/classes/tests demandés ont été créés tels que spécifiés dans le plan, sans écart de code (aucune règle de déviation 1-4 déclenchée).

## Issues Encountered
- **Test d'auteur corrigé (non déviation code)** : un test de robustesse ajouté (`DayTicks_reset_autre_jour_normalise`) portait une attente erronée `[45,120,195,270,345]` supposant une grille 5 h alignée sur une grille horaire fixe. L'implémentation (correcte) a révélé la dérive réelle de +4 h/jour → attente corrigée à `[30,105,180,255,330]` avec commentaire explicatif. L'implémentation `DayTimeline` n'a pas changé ; c'est le test qui encodait une fausse hypothèse.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Toute la logique pure de la refonte est prête et testée. Le plan **12-02** (recomposition XAML) peut se limiter à du binding :
  - remplissage inversé → binder `RingArc.Fraction` sur `FractionElapsed`,
  - anneau 24 h → binder `RingArc.Fraction` sur `DayFraction` + `TickRing.Angles` sur `DayResetAngles` + couleur via `UtilizationToBrushConverter` alimenté par `FiveHour.Utilization` (JOUR-03),
  - % au centre → binder `UtilizationText`/`HasUtilizationText` (séparateur « · »),
  - réordonnancement des 3 anneaux + resize ~170 px + checkpoint visuel (TAILLE-01).
- **Concern reporté au plan 02** : compacité à 170 px (chevauchement des 3 anneaux + 2 lignes de %) à valider en checkpoint visuel.

---
*Phase: 12-refonte-du-cadran-3-anneaux-remplissage-compacit*
*Completed: 2026-07-09*

## Self-Check: PASSED

Tous les fichiers créés existent sur le disque ; les 3 commits de tâche sont présents dans l'historique git.
