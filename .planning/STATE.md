---
gsd_state_version: 1.0
milestone: v1.3
milestone_name: — Refonte du cadran : 3 anneaux, remplissage, compacité
status: roadmapped
stopped_at: Roadmap v1.3 créée (Phase 12)
last_updated: "2026-07-09T00:00:00.000Z"
last_activity: 2026-07-09
progress:
  total_phases: 1
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-08)

**Core value:** Voir instantanément, sans terminal ni `/usage`, combien de quota et de temps il reste sur les deux fenêtres — sans jamais présenter une estimation comme un chiffre exact.
**Current focus:** Phase 12 — Refonte du cadran (3 anneaux, remplissage, compacité)

## Current Position

Milestone: v1.3 — Refonte du cadran : 3 anneaux, remplissage, compacité
Phase: 12
Plan: Not started
Status: Roadmap créée — prête pour `/gsd:plan-phase 12`
Last activity: 2026-07-09

Progress: [░░░░░░░░░░] 0% (0/1 phases)

## Performance Metrics

**Velocity:**

- Total plans completed (v1.3): 0
- Average duration: —
- Total execution time: 0 h

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 12 | 0/TBD | - | - |

*Updated after each plan completion*

## Accumulated Context

### Decisions

Milestone v1.3 = **pur rendu / ViewModel**. Aucune source de données, aucun provider, aucun pipeline
n'est modifié (v1.0/1.1/1.2 inchangés). Le code v1.2 est en place et fournit toute la matière.

- [v1.3/roadmap]: Une seule phase (Phase 12) — les 5 ajustements demandés forment un geste de refonte
  cohérent et purement présentation ; pas de découpage artificiel.
- [Contexte technique — briques existantes réutilisées]:
  - `RingArc` : Shape, DP `Fraction` (0..1), `EllipseGeometry` si Fraction ≥ 1, `StartAngle`/`Radius`, AffectsRender.
  - `ArcGeometry` : math pure (PointAt Y-inversé 0°=12 h, Build avec IsLargeArc/cas vide/plein) — testée.
  - `TickRing` : Shape, GeometryGroup de segments en une passe (réutiliser `ArcGeometry.PointAt`).
  - `UtilizationToBrushConverter` : double? → Brush (rampe / gris épuisé / neutre si null) — réutilisable pour l'anneau 24 h.
  - `WindowGaugeViewModel` : `FractionRemaining` (0..1), `Utilization` (double?), `CountdownText` (FR), `IsEstimated`, `Exhausted`.
- [v1.3/Phase 12 — travail à faire]:
  - **Inversion** : longueur d'arc = `1 − FractionRemaining` (arc vide en début de fenêtre, plein au reset).
  - **Anneau 24 h** : nouvelle math pure d'angles — fraction du jour = (now − minuit local) / 24 h ; ticks
    aux resets 5 h projetés toutes les 5 h à partir du `resets_at` 5 h courant, sur l'axe des 24 h ;
    couleur via `UtilizationToBrushConverter` alimenté par l'utilization 5 h.
  - **% au centre** : formatage honnête « countdown · % » (`~` si estimé, rien si `Utilization` null).
  - **Réordonnancement + resize** : dans MainWindow.xaml, hebdo (interne) → 5 h (milieu) → 24 h (externe),
    fenêtre/cadran mis à l'échelle à ~170 px, texte lisible, pas de chevauchement.
  - Décomposition suggérée pour le planner : 2-3 plans (logique pure d'abord, composition XAML + checkpoint visuel ensuite).

### Décisions v1.3 (roadmap)

- Découpage 1 phase : milestone cohérent, purement présentation, aucune surface sensible ni I/O.
- Garder toute nouvelle math (inversion, angles 24 h, projection resets, formatage %) en **fonctions/logique
  pures testables** (cohérent avec le pattern `ArcGeometry`/`RampColor` de Phase 5), avant la composition XAML.
- Honnêteté préservée sur le % : « ~ » si estimé, pas de % si utilization null (VIS-05) — même règle que le badge « estimée ».

### Pending Todos

None yet.

### Blockers/Concerns

- Compacité (TAILLE-01) : à 170 px, valider en checkpoint visuel que les 3 anneaux + les 2 lignes de %
  restent lisibles et sans chevauchement (ajuster rayons/épaisseurs/tailles de police en UAT).
- Projection des resets 5 h sur l'axe 24 h (JOUR-02) : caler le sens (les resets 5 h tombent toutes les 5 h,
  à vérifier que la projection depuis `resets_at` produit des graduations cohérentes avec l'anneau 5 h).

## Session Continuity

Last session: 2026-07-09
Stopped at: Roadmap v1.3 créée (Phase 12)
Resume file: None
