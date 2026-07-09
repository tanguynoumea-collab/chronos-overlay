---
gsd_state_version: 1.0
milestone: v1.2
milestone_name: — Usage exact via l'endpoint OAuth
status: planning
stopped_at: Roadmap v1.2 créée (phases 10-11)
last_updated: "2026-07-09T00:00:00.000Z"
last_activity: 2026-07-09
progress:
  total_phases: 2
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-08)

**Core value:** Voir instantanément, sans terminal ni `/usage`, combien de quota et de temps il reste sur les deux fenêtres — sans jamais présenter une estimation comme un chiffre exact.
**Current focus:** Phase 10 — Lecture du token + client endpoint (la partie sensible, isolée et testée à fond)

## Current Position

Milestone: v1.2 — Usage exact via l'endpoint OAuth
Phase: 10
Plan: Not started
Status: Ready to plan
Last activity: 2026-07-09

Progress: [░░░░░░░░░░] 0% (0/2 phases)

## Performance Metrics

**Velocity:**

- Total plans completed (v1.2): 0
- Average duration: —
- Total execution time: 0 h

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 10 | 0/TBD | - | - |
| 11 | 0/TBD | - | - |

*Updated after each plan completion*

## Accumulated Context

### Decisions

Contexte technique v1.0/v1.1 conditionnant v1.2 (v1.2 enrichit le composite, ne réécrit pas) :

- [v1.0/Phase 3]: Abstraction IUsageProvider + UsageSnapshot immuables neutres en place ; le nouveau ClaudeOAuthUsageProvider s'y conforme (Exact) sans toucher au cadran.
- [v1.0/Phase 3]: CompositeUsageProvider bascule PAR FENÊTRE (Exact > Estimated > Unavailable) — v1.2 insère l'OAuth en tête de priorité Exact, devant le pont statusLine et le repli JSONL.
- [v1.0/Phase 6]: SettingsService atomique + menu contextuel = seul point d'accès UI — v1.2 y ajoute le toggle « Usage exact (OAuth) ».
- [source docs/data-sources.md]: le champ réel est `used_percentage` (0..100) et `resets_at` en epoch SECONDES ; normalisation `Utilization = used_percentage / 100` côté modèle. L'endpoint OAuth `/api/oauth/usage` renvoie la même structure `rate_limits.five_hour/seven_day` — mapping réutilisable.

### Décisions v1.2 (roadmap)

- Découpage 2 phases avec la **sécurité isolée** : Phase 10 concentre toute la surface sensible (lecture/déchiffrement du token, appel réseau) et la teste de bout en bout AVANT de brancher l'UI en Phase 11.
- Phase 10 démarre par un **test décisif E2E** (déchiffrer le vrai token une fois + appel réel + afficher les % exacts) : s'il échoue, la phase le documente comme bloquant plutôt que de coder à l'aveugle.
- Contrainte sécurité transverse à honorer dans les critères Phase 10 : token jamais logué/écrit, transmis uniquement à api.anthropic.com, lecture seule du coffre, tolérance totale aux erreurs (jamais de crash, bascule repli).

### Pending Todos

None yet.

### Blockers/Concerns

- Faisabilité du mécanisme coffre → endpoint : à valider par le test décisif E2E en tout début de Phase 10 (safeStorage v10, clé DPAPI via Local State, AES-256-GCM). Si le déchiffrement ou l'appel échoue sur le vrai poste, la phase remonte le blocage.
- Token potentiellement expiré (refreshToken hors scope v1.2) : comportement attendu = 401 → « indisponible » + bascule repli, pas un bug.

## Session Continuity

Last session: 2026-07-09
Stopped at: Roadmap v1.2 créée (phases 10-11)
Resume file: None
