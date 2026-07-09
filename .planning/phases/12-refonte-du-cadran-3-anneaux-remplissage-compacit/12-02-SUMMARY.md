---
phase: 12-refonte-du-cadran-3-anneaux-remplissage-compacit
plan: 02
subsystem: ui
tags: [wpf, xaml, mvvm, binding, arc-geometry, overlay, design-tokens]

# Dependency graph
requires:
  - phase: 12-01-logique-pure
    provides: "FractionElapsed, UtilizationText/HasUtilizationText, DayFraction, DayResetAngles, TickRing.Angles"
  - phase: 05-cadran
    provides: "RingArc (Fraction/Radius), TickRing, UtilizationToBrushConverter (UtilBrush)"
provides:
  - "MainWindow.xaml recomposé : overlay 170×170, ellipse de fond 156, centre 85,85"
  - "3 anneaux réordonnés centre → extérieur : hebdo R44 → 5 h R58 → 24 h R72 (VIS-02)"
  - "Arcs hebdo/5 h bindés sur FractionElapsed (remplissage inversé VIS-01)"
  - "Anneau timeline 24 h : Fraction=DayFraction, Stroke=FiveHour.Utilization, ticks=DayResetAngles (JOUR-01/02/03)"
  - "% d'utilization « countdown · % » à côté de chaque countdown, séparateur lié à HasUtilizationText (VIS-05)"
  - "Token de piste Piste24h (#242330) dans DesignTokens.xaml"
affects: [uat-humain-visuel, future-tuning-visuel]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Binding-only XAML : recomposition visuelle qui ne consomme QUE des propriétés de VM/contrôles déjà testées (aucune logique dans la vue)"
    - "Séparateur conditionnel piloté par un bool VM (HasUtilizationText) — même pattern que HasTokens"

key-files:
  created:
    - .planning/phases/12-refonte-du-cadran-3-anneaux-remplissage-compacit/12-HUMAN-UAT.md
  modified:
    - src/Chronos/Views/MainWindow.xaml
    - src/Chronos/Resources/DesignTokens.xaml

key-decisions:
  - "Suppression des graduations décoratives (TickRing Count=60/12) : à 170 px elles surchargeaient ; on ne garde que les ticks porteurs de sens (resets 5 h projetés)."
  - "StrokeThickness 9 + gap ~5 px et rayons 44/58/72 : compromis compacité/lisibilité concret pour 170 px (ajustable en UAT)."
  - "Polices réduites (5 h : 18, hebdo : 12, badges/mentions : 8-9) pour tenir sans chevauchement dans la zone hebdo interne."

patterns-established:
  - "Recomposition XAML pure binding après logique testée : la vue ne fait que câbler des propriétés déjà couvertes par les tests du plan 12-01."

requirements-completed: [VIS-01, VIS-02, VIS-05, JOUR-01, JOUR-02, JOUR-03, TAILLE-01]

# Metrics
duration: 3min
completed: 2026-07-09
---

# Phase 12 Plan 02 : Recomposition XAML du cadran (3 anneaux, remplissage inversé, 24 h, %, 170 px) Summary

**MainWindow.xaml recomposé en binding pur : overlay 170 px, 3 anneaux réordonnés (hebdo R44 → 5 h R58 → 24 h R72) au remplissage inversé (FractionElapsed), anneau timeline 24 h coloré/gradué aux resets, et « countdown · % » honnête à côté de chaque compteur.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-07-09T09:32:47Z
- **Completed:** 2026-07-09T09:36:22Z
- **Tasks:** 3 (2 auto + 1 checkpoint visuel auto-vérifié)
- **Files created/modified:** 3 (1 créé, 2 modifiés)

## Accomplishments
- **VIS-01/VIS-02** : 3 anneaux réordonnés du centre vers l'extérieur (hebdo 44 → 5 h 58 → 24 h 72), arcs 5 h/hebdo bindés sur `FractionElapsed` → ils se remplissent vers le reset.
- **JOUR-01/02/03** : anneau timeline 24 h — `Fraction=DayFraction`, `Stroke=FiveHour.Utilization` (via `UtilBrush`), ticks aux `DayResetAngles`.
- **VIS-05** : « countdown · % » sur une ligne pour chaque fenêtre, séparateur « · » et % visibles seulement si `HasUtilizationText` (honnêteté : rien si utilization null, « ~ » si estimé).
- **TAILLE-01** : overlay resizé à 170×170 (ellipse 156, centre 85,85), polices et badges réduits, graduations décoratives supprimées pour la lisibilité.
- **DesignTokens** : token `Piste24h` (#242330) pour la piste de l'anneau 24 h.
- **Non-régression** : 209/209 tests verts, build 0 avertissement/0 erreur, exe self-contained republié.

## Task Commits

Chaque tâche committée atomiquement :

1. **Task 1 : réordonner 3 anneaux + anneau 24 h + resize 170** - `d895ed9` (feat)
2. **Task 2 : % d'utilization à côté de chaque countdown + polices réduites** - `4bde22a` (feat)
3. **Task 3 : checkpoint visuel** - auto-vérifié (aucun commit code — voir ci-dessous)

## Files Created/Modified
- `src/Chronos/Views/MainWindow.xaml` (modifié) - Fenêtre 170×170, ellipse 156, 3 anneaux réordonnés + anneau 24 h + ticks resets, bloc central « countdown · % », polices réduites, graduations décoratives supprimées.
- `src/Chronos/Resources/DesignTokens.xaml` (modifié) - Token `Piste24h` pour la piste de l'anneau 24 h.
- `.planning/phases/12-.../12-HUMAN-UAT.md` (créé) - Critères purement visuels persistés pour validation humaine (remplissage, ordre, 24 h, %, compacité, drag/snap).

## Decisions Made
- **Graduations décoratives supprimées** (Count=60/12) : à 170 px elles créaient du bruit visuel ; seuls les ticks porteurs de sens (resets 5 h projetés à `DayResetAngles`) sont conservés.
- **Géométrie concrète 170 px** : rayons 44/58/72, `StrokeThickness=9`, gap ~5 px, ellipse 156 — départ de checkpoint, ajustable en UAT sans toucher à la logique.
- **Polices réduites** (5 h : 18, hebdo : 12, badges : 8) pour tenir dans la zone hebdo interne sans chevauchement.

## Deviations from Plan

None - plan executed exactly as written. Les deux tâches auto ont été exécutées telles que spécifiées (binding sur les propriétés livrées par 12-01, valeurs géométriques concrètes du plan). Aucune règle de déviation 1-4 déclenchée.

## Checkpoint (Task 3) — auto-vérifié (mode autonome)

Le checkpoint visuel a été traité en mode autonome : PAS d'arrêt, vérification programmatique complète.
- **Build** : `dotnet build Chronos.sln -c Debug` → 0 avertissement, 0 erreur.
- **Tests** : `dotnet test Chronos.sln -c Debug` → 209/209 verts.
- **Grep structure** : Width/Height 170 ; ordre RingArc `ArcHebdo`(44) → `ArcCinqHeures`(58) → `ArcVingtQuatreHeures`(72) ; hebdo/5 h ← `FractionElapsed` ; 24 h ← `DayFraction` + `FiveHour.Utilization` (UtilBrush) ; `TickRing Angles=DayResetAngles` ; % au centre ← `UtilizationText` + séparateur lié à `HasUtilizationText` ; token `Piste24h` présent.
- **Publish** : instance `Chronos.exe` arrêtée (`taskkill /F`) puis `dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true` → exe mono-fichier ~76,8 Mo republié. Exe NON lancé / NON capturé (délégué à l'orchestrateur).
- **Critères purement visuels** (remplissage inversé, ordre sans chevauchement, exactitude anneau 24 h, format %, compacité 170 px, drag/snap) persistés dans `12-HUMAN-UAT.md` pour validation humaine.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- La refonte du cadran est complète et livrée à l'écran (dernier plan v1.3). Reste la **validation humaine visuelle** (`12-HUMAN-UAT.md`) : remplissage, ordre des anneaux, anneau 24 h, %, compacité 170 px, drag/snap.
- Ajustements visuels éventuels (rayons/épaisseurs/FontSize) à faire en UAT sans replanification lourde ; toute anomalie fonctionnelle → `--gaps`.

---
*Phase: 12-refonte-du-cadran-3-anneaux-remplissage-compacit*
*Completed: 2026-07-09*

## Self-Check: PASSED

Tous les fichiers créés/modifiés existent sur le disque (MainWindow.xaml, DesignTokens.xaml, 12-HUMAN-UAT.md, 12-02-SUMMARY.md) ; les 2 commits de tâche (`d895ed9`, `4bde22a`) sont présents dans l'historique git.
