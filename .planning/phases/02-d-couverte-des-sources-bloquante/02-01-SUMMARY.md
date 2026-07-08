---
phase: 02-d-couverte-des-sources-bloquante
plan: 01
subsystem: data-sources
tags: [statusline, rate_limits, jsonl, usage, claude-code, documentation]

# Dependency graph
requires:
  - phase: 01-fondations-architecture-squelette-overlay
    provides: Overlay WPF net8.0-windows + DI, socle sur lequel les providers se brancheront
provides:
  - "docs/data-sources.md : caractérisation empirique de la source d'usage (rate_limits/statusLine) et du repli JSONL"
  - "Correction de champ : used_percentage (0..100) et NON utilization (0..1) ; resets_at en epoch secondes"
  - "Mécanisme d'accès documenté : pont statusLine → fichier (aucune persistance disque de l'objet d'usage)"
  - "Mapping source → UsageSnapshot (/100.0 et DateTimeOffset.FromUnixTimeSeconds)"
  - "Hypothèses/fragilités pour guider IUsageProvider (API privée de facto, staleness, présence conditionnelle)"
affects: [Phase 3 IUsageProvider, Phase 3 CompositeUsageProvider, provider primaire statusLine, provider repli JSONL, V2-01 bande sous-agents]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pont statusLine → fichier watchable (%APPDATA%\\Chronos\\usage.json) consommé par FileSystemWatcher"
    - "Deux formats de temps distincts : primaire epoch s, repli ISO 8601 UTC"

key-files:
  created:
    - docs/data-sources.md
  modified: []

key-decisions:
  - "Source primaire = bloc rate_limits du contrat statusLine (Fiable), consommé via un pont statusLine → fichier ; l'objet d'usage n'est persisté dans aucun fichier disque"
  - "Champ réel used_percentage (0..100), pas utilization (0..1) ; conversion /100.0 côté modèle"
  - "resets_at = Unix epoch secondes → DateTimeOffset.FromUnixTimeSeconds ; ne pas confondre avec le timestamp ISO 8601 du repli JSONL"
  - "Repli JSONL marqué Estimé (plafonds non publiés) ; lecture streaming FileShare.ReadWrite, dernière ligne partielle tolérée"

patterns-established:
  - "Pont statusLine → fichier : documenté en Phase 2, à coder en Phase 3 (non destructif, ré-émet la barre existante sur stdout)"
  - "Dégradation gracieuse : fenêtre/objet absent → indisponible ou repli, jamais de valeur inventée"

requirements-completed: [DAT-01]

# Metrics
duration: 3min
completed: 2026-07-08
---

# Phase 2 Plan 01 : Découverte des sources (bloquante) Summary

**docs/data-sources.md caractérise empiriquement l'objet d'usage Claude Code (bloc `rate_limits` du contrat statusLine, `used_percentage` 0..100 + `resets_at` epoch s), le repli JSONL, et le mapping vers UsageSnapshot — préalable bloquant DAT-01 avant tout code de provider.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-07-08T13:59:02Z
- **Completed:** 2026-07-08T14:01:32Z
- **Tasks:** 2
- **Files modified:** 1 (créé)

## Accomplishments

- **Objet d'usage localisé et corrigé** : ce n'est PAS un format privé non documenté mais le bloc `rate_limits` du contrat statusLine officiel. Correction majeure actée : le champ est `used_percentage` (0..100), pas `utilization` (0..1) ; `resets_at` est en epoch secondes.
- **Mécanisme d'accès documenté** : aucune persistance disque de l'objet d'usage → nécessite un pont statusLine → fichier (non destructif vis-à-vis du `gsd-statusline.js` existant), à coder en Phase 3.
- **Repli JSONL caractérisé** : localisation, `message.usage` (tokens), timestamps ISO 8601 UTC, implications perf (streaming, FileShare.ReadWrite), layout `subagents/` v2.1.202 pour V2-01.
- **Mapping UsageSnapshot explicite** + hypothèses/fragilités consignées pour guider `IUsageProvider` (Phase 3).

## Task Commits

1. **Task 1 : Source primaire, repli JSONL et mapping UsageSnapshot** — `18656cc` (docs)
2. **Task 2 : Hypothèses & fragilités, reproductibilité, anonymisation** — `a9e0727` (docs)

**Plan metadata:** _(commit final ci-dessous)_

## Files Created/Modified

- `docs/data-sources.md` — Documentation empirique complète des sources de données (315 lignes) : source primaire rate_limits/statusLine, repli JSONL, mapping UsageSnapshot, hypothèses/fragilités, reproductibilité.

## Decisions Made

- Source primaire = `rate_limits` du contrat statusLine (Fiable), consommée via un pont statusLine → fichier watchable — l'objet n'est persisté dans aucun fichier disque, il ne transite que par stdin pendant une session active.
- Champ réel `used_percentage` (0..100), pas `utilization` (0..1) → conversion `/100.0` côté modèle ; `resets_at` = epoch s → `DateTimeOffset.FromUnixTimeSeconds`.
- Repli JSONL marqué `Estimé` (plafonds non publiés/mouvants) ; lecture streaming tolérante.
- Sous-agents (V2-01) : lire le dossier `subagents/` (v2.1.202), pas des blocs Task inline.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. Tout le contenu factuel provenait de `02-RESEARCH.md` ; l'exécuteur a structuré et rédigé sans redécouverte. La revue d'anonymisation confirme l'absence de token, UUID réel, e-mail, nom d'utilisateur réel ou contenu de conversation dans le document.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- DAT-01 satisfait : `docs/data-sources.md` livré et vérifié (11 assertions grep + 5 sections + anonymisation).
- La Phase 3 dispose du schéma corrigé, du mapping et des fragilités pour bâtir `IUsageProvider` sur des faits vérifiés, pas sur le champ fantôme `utilization`.
- Fragilité résiduelle à porter en Phase 3 : test de contrat sur échantillon (API privée de facto, runtime 2.1.202 non vérifié champ par champ — confiance MEDIUM sur la stabilité inter-versions).

## Self-Check: PASSED

- FOUND: docs/data-sources.md
- FOUND: 02-01-SUMMARY.md
- FOUND commit: 18656cc (Task 1)
- FOUND commit: a9e0727 (Task 2)

---
*Phase: 02-d-couverte-des-sources-bloquante*
*Completed: 2026-07-08*
