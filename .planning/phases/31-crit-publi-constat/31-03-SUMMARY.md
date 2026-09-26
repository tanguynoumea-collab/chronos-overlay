---
phase: 31-crit-publi-constat
plan: 03
subsystem: validation
tags: [constat, production, val-03, partiel]
requires: ["31-01", "31-02"]
provides: ["31-CONSTAT.md avec protocole, relevés, écarts et verdict PARTIEL"]
affects: [32]
tech-stack:
  added: []
  patterns: ["constat en production : protocole écrit avant d'être joué, relevés hors de l'arbre de l'app"]
key-files:
  created: []
  modified: [".planning/phases/31-crit-publi-constat/31-CONSTAT.md"]
key-decisions:
  - "Milestone v1.7 clos avec un écart connu : le point (a) (une seule instance) et le tableau des gestes ne sont pas constatés ; reportés en tête de la phase 32 (v1.8), sur la 3.2.2"
patterns-established: []
requirements-completed: []
duration: "2026-09-26 13:03 → 2026-09-27 (constat interrompu)"
completed: 2026-09-27
one_liner: "Constat en production PARTIEL : réconciliation 3.1.0→3.2.0→3.2.1 conforme dans le fichier, version 3.2.1 au diagnostic, E2 corrigé par 30.1 ; point (a) jamais atteint (trois exécutables en marche), tableau des gestes et 12 vérifications reportés en phase 32."
---

# Phase 31 — Plan 03 : constat en production (PARTIEL)

## Ce qui a été constaté
- Réconciliation de `~/.claude/settings.json` vers la 3.2.0 puis la 3.2.1 : conforme, prouvée par md5 de sauvegarde et égalité structurelle (critère 3, VAL-03).
- « Version : 3.2.1 » au diagnostic, exe publié à la racine (critère 2).
- E2 (sous-agent en arrière-plan) : diagnostiqué en production, corrigé par la phase insérée 30.1, republié 3.2.1.

## Ce qui ne l'a pas été
- Point (a) : la 3.2.1 n'a jamais tourné seule (3.1.0 et 3.2.0 jamais quittées).
- Point (b) : les lignes L1, L2, L2b, L3, L4, Q du tableau et les 12 vérifications déférées.

## Décision
Verdict PARTIEL écrit dans `31-CONSTAT.md` §4 ; écart consigné dans `MILESTONES.md` (Known Gaps) ; reprise en tête de la phase 32 du milestone v1.8 (« une seule instance », sur la 3.2.2). Aucun overlay lancé, arrêté ni cliqué par l'agent.
