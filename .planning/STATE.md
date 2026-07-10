---
gsd_state_version: 1.0
milestone: v1.4
milestone_name: "— Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code)"
status: defining_requirements
stopped_at: Milestone v1.4 started
last_updated: "2026-07-10"
last_activity: 2026-07-10
progress:
  total_phases: 1
  completed_phases: 1
  total_plans: 2
  completed_plans: 2
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-08)

**Core value:** Voir instantanément, sans terminal ni `/usage`, combien de quota et de temps il reste sur les deux fenêtres — sans jamais présenter une estimation comme un chiffre exact.
**Current focus:** Phase 12 — refonte-du-cadran-3-anneaux-remplissage-compacit

## Current Position

Milestone: v1.4 — Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code)
Phase: Not started (defining requirements)
Plan: —
Status: Defining requirements
Last activity: 2026-07-10 — Milestone v1.4 started

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
| Phase 12 P01 | 6 min | 3 tasks | 9 files |
| Phase 12 P02 | 3 min | 3 tasks | 3 files |

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
- [Phase 12]: TickRing.Angles = angles explicites (resets 5 h projetés), pas grille régulière : 24h/5h=4,8 non entier
- [Phase 12]: Grille resets 5 h dérive de +4h/jour (honnête) ; DayTimeline normalise puis rembobine au 1er reset du jour
- [Phase 12]: 12-02 XAML : graduations décoratives (Count 60/12) supprimées à 170 px ; seuls les ticks resets 5 h (DayResetAngles) conservés (sens).
- [Phase 12]: 12-02 : géométrie 170 px = rayons 44/58/72, épaisseur 9, ellipse 156 — ajustable en UAT sans toucher la logique.

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

Last session: 2026-07-09T09:37:25.949Z
Stopped at: Completed 12-02-PLAN.md
Resume file: None
