---
status: partial
phase: 04-orchestration-refresh-viewmodel-temps-r-el
source: [04-01-SUMMARY.md, 04-02-SUMMARY.md, 04-VERIFICATION.md]
started: 2026-07-08T16:00:08Z
updated: 2026-07-08T16:00:08Z
---

## Current Test

[awaiting human testing — build, suite de tests (41/41) et smoke run automatisé (process
stable ~8s sans crash) sont verts ; reste la validation visuelle en conditions réelles, non
observable par grep/tests car le cadran graphique (arcs, ticks) est le livrable de la Phase 5.
Les items ci-dessous vérifient le COMPORTEMENT temps réel de la Phase 4 (rafraîchissement,
interpolation, absence d'erreur de thread) via les éléments actuellement visibles (placeholder
de cadran, fenêtre overlay).]

## Contexte

Le `RefreshOrchestrator` (horloge données, RAF-01/RAF-02) et le `MainViewModel` temps réel
(marshaling + interpolation, RAF-03/RAF-04) sont vérifiés programmatiquement : build propre,
41/41 tests unitaires verts (coalescence de rafale, Error→recréation du watcher, interpolation
pure sans I/O, marshaling `IUiDispatcher.Post` exactement une fois), et un smoke run de
`Chronos.exe` (~8 s) confirme l'absence de crash au démarrage. Le rendu visuel complet
(arcs à deux anneaux, couleurs, texte centré) arrive en Phase 5 ; le placeholder actuel
(`MainWindow.xaml`) ne permet pas d'observer directement la progression des arcs, mais permet
de vérifier l'absence de crash/erreur de thread sur la durée.

## Tests

### 1. Aucune InvalidOperationException au fil du temps

expected: Lancer `Chronos.exe` (ou `dotnet run --project src/Chronos`) et le laisser tourner
au moins 2 minutes (pour dépasser au moins un cycle `PeriodicTimer` par défaut à 60 s ET le
seuil `IsStale` à 2 min). Aucune boîte de dialogue d'exception, aucun crash, la fenêtre overlay
reste affichée et réactive.
result: [pending]

### 2. Rafraîchissement déclenché par écriture réelle du pont statusLine

expected: Dans une session Claude Code réelle (le pont Phase 3 doit être installé — voir
`03-HUMAN-UAT.md`), après une réponse de l'assistant qui écrit `%APPDATA%\Chronos\usage.json`,
l'overlay doit refléter la mise à jour en quelques centaines de ms (debounce 300 ms) sans
attendre le prochain cycle périodique de 60 s. Vérifiable indirectement via les logs
`Microsoft.Hosting.Lifetime` en console (mode `dotnet run`) ou en ajoutant un log temporaire ;
à défaut, observer que l'état affiché (une fois le cadran Phase 5 livré) change peu après
l'écriture.
result: [pending]

### 3. Filet de sécurité périodique sans écriture

expected: Sans provoquer aucune écriture de `usage.json`, laisser l'overlay tourner > 60 s
(intervalle `PeriodicInterval` par défaut) : le `PeriodicTimer` doit quand même déclencher une
relecture (visible dans les logs si instrumentés, ou par la mise à jour de `CapturedAt`/
`IsStale` une fois le cadran Phase 5 livré).
result: [pending]

## Summary

total: 3
passed: 0
issues: 0
pending: 3
skipped: 0
blocked: 0

## Gaps

- Ces trois tests ne bloquent PAS le statut de vérification de la Phase 4 (goal-backward
  verification programmatique déjà passée : build + 41/41 tests + smoke run). Ils sont
  conservés pour validation humaine ultérieure, idéalement combinés avec la validation visuelle
  du cadran Phase 5 (le rendu complet des arcs/countdown rendra ces vérifications triviales à
  observer directement).
