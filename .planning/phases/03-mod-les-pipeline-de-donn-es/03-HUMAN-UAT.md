---
status: partial
phase: 03-mod-les-pipeline-de-donn-es
source: [03-04-SUMMARY.md]
started: 2026-07-08T00:00:00Z
updated: 2026-07-08T00:00:00Z
---

## Current Test

[awaiting human testing — pont installe et verifie programmatiquement ; reste la validation en session Claude Code REELLE]

## Contexte

Le pont statusLine est **installe** (`node scripts/install-bridge.mjs` execute, backup
`~/.claude/settings.json.chronos.bak` cree, `statusLine.command` pointe sur
`scripts/chronos-statusline-bridge.js`). La verification programmatique (rendu simule avec un
JSON de test contenant `rate_limits`) est **verte** : sortie `gsd-statusline.js` re-emise
intacte + `usage.json` ecrit avec les bonnes valeurs. Les tests ci-dessous exigent une **vraie
session interactive Claude Code** (donnees `rate_limits` reelles apres 1re reponse API,
rendu visuel dans le terminal) — non simulables hors-ligne.

Pour desinstaller en cas de probleme : `node scripts/install-bridge.mjs --uninstall`.

## Tests

### 1. statusLine intacte en session reelle

expected: Dans une session Claude Code reelle, la barre de statut s'affiche TOUJOURS
normalement (model | task | dir | contexte), identique a avant l'installation du pont — le
wrapper ne l'a pas cassee ni ralentie de facon perceptible.
result: [pending]

### 2. usage.json rempli avec des donnees REELLES

expected: Apres au moins une reponse de l'assistant dans une session reelle (abonne Pro/Max),
`%APPDATA%\Chronos\usage.json` contient `five_hour` et/ou `seven_day` avec des
`used_percentage` (0..100) et `resets_at` (epoch s) REELS + un `capturedAt` recent.
Verif : `type "%APPDATA%\Chronos\usage.json"` (cmd) ou `cat "$APPDATA/Chronos/usage.json"` (bash).
result: [pending]

### 3. Rafraichissement au fil de la session

expected: `capturedAt` de `usage.json` progresse a mesure que la session avance (la barre est
re-rendue), confirmant que le pont materialise `rate_limits` en continu et non une seule fois.
result: [pending]

## Summary

total: 3
passed: 0
issues: 0
pending: 3
skipped: 0
blocked: 0

## Gaps

- Les valeurs `rate_limits` reelles n'apparaissent que pour les abonnes Claude.ai (Pro/Max) et
  seulement APRES la 1re reponse API : un compte sans abonnement ne remplira jamais les champs
  (fenetres restant `null`) — comportement attendu, pas un bug (le pont n'invente aucune valeur).
