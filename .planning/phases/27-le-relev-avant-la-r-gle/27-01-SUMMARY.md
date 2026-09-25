---
phase: 27-le-relev-avant-la-r-gle
plan: 01
subsystem: sessions
tags: [releve-in-vivo, app-bureau, lastFocusedAt, lue, fixtures, checkpoint-humain]
requirements: [VAL-01]
one_liner: "Les deux inconnues de la règle « lue » sont tranchées sur la vraie machine avec l'utilisateur — le retour alt-tab met `lastFocusedAt` à jour (16:23:51 → 20:30:44), mais un tour terminé sous ses yeux laisse `lastFocusedAt` antérieur à la fin du tour (20:30:44 < 20:34:04) : LUE-02 est confirmée nécessaire, LUE-01 est datée par le dernier message du transcript, et six fixtures réelles anonymisées attendent la phase 29."
provides:
  - "27-RELEVE.md — protocole, valeurs relevées (Task 1 passif, gestes A et B), conclusion, LUE-02 réécrite"
  - "REQUIREMENTS.md — LUE-01 précisée (instant = timestamp du dernier end_turn du transcript), LUE-02 confirmée et précisée (processus `claude`, délai 2,5 s)"
  - "tests/Chronos.Tests/TestData/DesktopAppSessions/*.json — 6 fixtures réelles réduites et anonymisées (completed, blocked, review_ready, sans postTurnSummary, sans cliSessionId, session du geste B)"
affects: [28-deux-mots-une-question-les-m-mes-horizons, 29-ce-que-l-app-bureau-sait, 30-la-lecture-fait-disparaitre, 31-ecrit-publie-constate]
tests: "896 verts / 0 échec (7 s) — aucun test ajouté, fixtures seulement ; la baseline réelle est 896, pas 889 (audit v1.6 antérieur aux derniers commits)"
---

# Phase 27 — Plan 01 : le relevé avant la règle

## Ce qui a été fait

Relevé en lecture seule sur la machine de l'utilisateur, le 2026-09-25, avec deux gestes joués par lui
(« A fait » à 20:31, « B fait » à 20:34). Aucun overlay de développement lancé ; `~/.claude/settings.json`
(mtime 11:07:56) et `%APPDATA%\Chronos\settings.json` inchangés ; `%APPDATA%\Claude` jamais écrit.

| Inconnue | Réponse | Valeurs |
|---|---|---|
| 1 — le retour alt-tab met-il `lastFocusedAt` à jour ? | **Oui** | 16:23:51 → **20:30:44** sans changer de session |
| 2 — tour terminé sous les yeux : `lastFocusedAt` précède-t-il la fin du tour ? | **Oui** | `lastFocusedAt` 20:30:44 < fin de tour **20:34:04** (dernier `end_turn` du transcript), fenêtre `claude` au premier plan |
| Prémisse de LUE-02 | **Tenue** | la session au `lastFocusedAt` max est celle où l'utilisateur écrit ; processus au premier plan = `claude` (PID 88220), titre « Claude » |
| Champs de la phase 29 sur la version du jour (app 2.9939.2.0) | **Présents** | `cliSessionId`, `title`, `lastFocusedAt`, `lastActivityAt`, `latestUserFrameAt`, `completedTurns`, `postTurnSummary` |

## Ce qui a été découvert en plus (à porter dans les phases 28-30)

1. **Le fichier d'état des hooks disparaît à la frontière des tours** (absent au 1er appel d'outil de deux tours
   consécutifs, présent au 2e). Seul `SessionEnd` le supprime : l'app bureau relance le processus CLI entre
   les tours. Un `Stop` → `WaitingTurn` peut donc être perdu ; Chronos ne lit ni `SessionStart.source`
   (`resume` ≠ travail) ni `SessionEnd.reason`.
2. **La date d'une attente = `timestamp` du dernier message significatif du transcript**, pas le `mtime` du
   fichier (rafraîchi par des lignes de métadonnées sans message : `custom-title`, `agent-name`, `mode`,
   `atis-latch`, `bridge-session`, `last-prompt`, `queue-operation`). La recherche de la phase 28 le mesure
   indépendamment : 12 « En attente » fantômes évités, 5 sessions visibles au lieu de 13.
3. **`postTurnSummary` est transitoire** (effacé au redémarrage de l'app, absent après 5 tours sur la session
   courante) : indice, jamais état.

## Décisions

- **LUE-01 précisée** : instant d'attente = `timestamp` du dernier `end_turn` (ou de la question) du transcript.
- **LUE-02 conservée et précisée** : session sélectionnée (max `lastFocusedAt`) + premier plan appartenant au
  processus `claude`, délai de grâce 2,5 s ; dès que le premier plan quitte `claude`, LUE-01 seule.
- **Réserve pour la phase 31** : entre 16:23:51 et 19:51, `lastFocusedAt` n'a pas bougé malgré un retour de
  l'utilisateur (après limite d'usage) — cas non reproduit par le geste A, à observer en production.

## Fixtures livrées (phase 29)

`tests/Chronos.Tests/TestData/DesktopAppSessions/` : `fin-de-tour-completed`, `fin-de-tour-blocked`,
`fin-de-tour-review-ready`, `sans-postTurnSummary`, `sans-cliSessionId`, `session-courante-geste-b` — réduits
aux champs lus, titres/chemins anonymisés, origine et date consignées dans `_fixture`.

## Vérification

- `dotnet test Chronos.sln --nologo` : **896 / 896**, 0 échec, 7 s.
- `git status` : modifications limitées à `.planning/` et `tests/Chronos.Tests/TestData/DesktopAppSessions/`.
