---
gsd_state_version: 1.0
milestone: v1.1
milestone_name: — Estimation utile en mode app bureau
status: executing
stopped_at: Completed 09-01-PLAN.md
last_updated: "2026-07-09T06:06:45.572Z"
last_activity: 2026-07-09
progress:
  total_phases: 2
  completed_phases: 1
  total_plans: 5
  completed_plans: 3
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-08)

**Core value:** Voir instantanément, sans terminal ni `/usage`, combien de quota et de temps il reste sur les deux fenêtres — sans jamais présenter une estimation comme un chiffre exact.
**Current focus:** Phase 09 — calibration-des-plafonds-surfa-age

## Current Position

Milestone: v1.1 — Estimation utile en mode app bureau
Phase: 09 (calibration-des-plafonds-surfa-age) — EXECUTING
Plan: 2 of 3
Status: Ready to execute
Last activity: 2026-07-09

Progress: [░░░░░░░░░░] 0% (0/2 phases)

## Performance Metrics

**Velocity:**

- Total plans completed (v1.1): 0
- Average duration: —
- Total execution time: 0 h

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 8 | 0/TBD | - | - |
| 9 | 0/TBD | - | - |

*Updated after each plan completion*
| Phase 08 P01 | 4 min | 3 tasks | 6 files |
| Phase 08 P02 | 6 min | 2 tasks | 10 files |
| Phase 09 P01 | 6 min | 3 tasks | 9 files |

## Accumulated Context

### Decisions

Contexte technique v1.0 conditionnant v1.1 (v1.1 enrichit, ne réécrit pas) :

- [v1.0/Phase 3]: JsonlEstimationProvider scanne déjà les JSONL (AllDirectories, subagents inclus) et somme les tokens de la fenêtre ; estimation toujours Estimated, Utilization/ResetsAt null (jamais inventé) — v1.1 lève ce null quand un plafond est connu.
- [v1.0/Phase 3]: CompositeUsageProvider bascule PAR FENÊTRE (Exact > Estimated > Unavailable) — v1.1 exploite cette granularité pour la calibration auto (CAL-02).
- [v1.0/Phase 6]: WeeklyRecalibration / WeeklyAnchor existent ; SettingsService atomique (Load disque avant écriture, cf. GAP-1) ; menu contextuel = seul point d'accès — v1.1 y ajoute « Calibrer les plafonds… ».
- [v1.0/audit]: Dette DT-1 (SnapshotChanged mort), DT-2 (UsageSnapshot.Age inerte), DT-3 (EstimatedTokens non surfacé) — adressées par NET-01 (Phase 8) et NET-02 (Phase 9).
- [Phase 08]: Algorithme « A » verrouillé : activité continue > 5 h ⇒ fenêtre 5 h inactive (null). Raffinement « B » différé v1.2, à valider empiriquement.
- [Phase 08]: Repli JSONL enrichi : somme 5 h bornée à la fenêtre inférée [start, now] (ancien glissant brut retiré), utilization = tokens/plafond sans clamp haut (>=1 = gris épuisé déjà géré), SevenDay.ResetsAt laissé null (rempli par WeeklyRecalibration côté VM — EST-05 non régressé).
- [Phase 08]: NET-01 soldé : IUsageProvider.SnapshotChanged et UsageSnapshot.Age retirés (dette DT-1/DT-2) ; RefreshOrchestrator.SnapshotChanged (event distinct) conservé. SettingsService injecté dans le provider (Load frais à chaque GetAsync) pour calibration Phase 9 sans redémarrage.
- [Phase 09]: Calibrateur tokenSource = JsonlEstimationProvider concret (porte toujours EstimatedTokens), pas le composite
- [Phase 09]: Priorité Manual > Auto/None : ApplyAuto n'écrase jamais une saisie manuelle

### Décisions v1.1 (roadmap)

- Phase 8 définit la math d'estimation (utilization = tokens / plafond, ou null si plafond absent = comportement v1.0) ; Phase 9 fournit l'UI qui peuple/calibre ces plafonds. Dépendance : EST-03/04 consomment les settings que CAL-01 permet de régler.
- NET-01 (nettoyage du contrat IUsageProvider) rattaché à la Phase 8 car elle touche déjà les providers.

### Pending Todos

None yet.

### Blockers/Concerns

- Fiabilité de l'inférence de fenêtre 5 h (EST-01) : dépend de la régularité des timestamps JSONL et de la définition d'un « trou d'inactivité » — à valider empiriquement en planification/exécution Phase 8.
- Calibration auto (CAL-02) : ne se déclenche que si un snapshot Exact apparaît un jour (statusline rendue au moins une fois) ; en app bureau pure, le plafond restera manuel — comportement attendu, pas un bug.

## Session Continuity

Last session: 2026-07-09T06:06:35.316Z
Stopped at: Completed 09-01-PLAN.md
Resume file: None
