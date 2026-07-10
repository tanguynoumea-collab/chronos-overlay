---
phase: 14-auto-disparition-hysteresis
plan: 01
subsystem: sessions
tags: [hysteresis, sessions, treated-store, tracker, mvvm-neutral]
requires:
  - "ArchiveStore (patron du magasin réversible)"
  - "SessionMonitor.Read (point d'insertion du filtre)"
  - "SessionSnapshot (Activity/Origin/UpdatedAt)"
  - "DesktopUiaSessionSource (clés desktop:foreground:… Phase 13)"
provides:
  - "TreatedStore : magasin réversible des sessions traitées (Set/Load/Remove, TTL 6 h)"
  - "SessionTreatmentTracker : détecteur stateful d'hystérésis (NET-01/02/03)"
  - "IForegroundWatch : seam neutre du focus premier-plan (impl. réelle au plan 02)"
  - "SessionMonitor : observation + double filtre archived/treated"
affects:
  - "src/Chronos/Services/SessionMonitor.cs"
  - "src/Chronos/App.xaml.cs"
tech-stack:
  added: []
  patterns:
    - "Magasin JSON atomique tmp+move, lecture tolérante, TTL (calqué ArchiveStore)"
    - "Détecteur stateful pur, horloge injectée par argument (pas d'IClock)"
    - "Seam d'infrastructure OS derrière interface neutre (null = désactivé)"
key-files:
  created:
    - "src/Chronos/Services/TreatedStore.cs"
    - "src/Chronos/Services/IForegroundWatch.cs"
    - "src/Chronos/Services/SessionTreatmentTracker.cs"
    - "tests/Chronos.Tests/TreatedSessionsTests.cs"
  modified:
    - "src/Chronos/Services/SessionMonitor.cs"
    - "src/Chronos/App.xaml.cs"
decisions:
  - "Réversibilité NET-03 portée par le tracker (purge sur nouvel épisode), PAS par une comparaison ts>=UpdatedAt dans le filtre (fausse pour le bureau où UpdatedAt==now à chaque poll)."
  - "L'horodatage d'épisode d'attente est maintenu par le tracker lui-même (stable pendant l'épisode) au lieu de UpdatedAt volatil."
  - "foreground=null en DI → branche NET-02 dormante jusqu'au plan 02, mais sa logique est présente et testée via faux focus."
metrics:
  duration: "~5 min"
  completed: "2026-07-10"
  tasks: 3
  files: 6
  tests_added: 12
  tests_total: 316
---

# Phase 14 Plan 01 : Cœur de l'auto-disparition par hystérésis — Summary

Magasin réversible `TreatedStore` + détecteur stateful `SessionTreatmentTracker` (hystérésis NET-01/02/03) branchés dans `SessionMonitor.Read` via un double filtre archived (permanent) / treated (réversible), le tout prouvé par 12 tests synthétiques (faux focus + horloge injectée) sans aucune fenêtre Claude réelle.

## Ce qui a été livré

- **`TreatedStore`** — calqué exactement sur `ArchiveStore` (TTL 6 h, écriture atomique tmp+move, lecture tolérante `JsonDocument`, chemins `%APPDATA%`) mais RÉVERSIBLE : ajout de `Remove(sessionId)` (point NET-03 absent d'`ArchiveStore`). Map `{ session_id : treatedWaitingTs(ms) }`. `Set` et `Remove` purgent les entrées expirées à chaque écriture.
- **`IForegroundWatch`** — seam neutre `bool IsClaudeForeground()`, best-effort, ne lève jamais. Implémentation OS réelle déférée au plan 02.
- **`SessionTreatmentTracker`** — stateful, pur, aucun type WPF :
  - NET-01 (répondu) : attente → non-attente ce cycle ⇒ `Set`. La disparition seule ne traite pas.
  - NET-02 (focus) : session bureau premier-plan en attente + focus continu ≥ 2,5 s ⇒ `Set` ; toute interruption remet le compteur à zéro (debounce anti-survol).
  - NET-03 (réapparition) : épisode d'attente courant plus récent que `treatedWaitingTs` ⇒ `Remove`.
  - Horodatage d'épisode maintenu en interne (stable), jamais dérivé du `UpdatedAt` volatil des snapshots bureau.
- **`SessionMonitor`** — ctor étendu en fin (3 params optionnels nuls = non-régression). `Read` invoque `tracker.Observe(raw, foreground, now)` en 2.c (best-effort, try/catch), puis double filtre : archived (NET-04, permanent) PUIS treated (réversible).
- **DI** — `TreatedStore` + `SessionTreatmentTracker` enregistrés (Singleton) et injectés dans `SessionMonitor` ; `foreground` non fourni (NET-02 dormant).

## Tests (12 neufs, 316 au total)

Unitaires (tracker direct) : round-trip/TTL du store, NET-01 (répondu / non-déclenchement sur disparition), NET-03 (réapparition + purge), NET-02 (acquittement 2,5 s / reset sur interruption / aucun déclenchement sans focus).
Intégration (via `SessionMonitor` + faux `IForegroundWatch` + `MutableSource`) : NET-01 masquage après réponse, NET-03 réaffichage sur nouvel épisode + purge, NET-04 archivée reste masquée même en attente plus récente (contraste réversible vs permanent), NET-02 masquage après focus 2,5 s, non-régression sans les nouveaux paramètres.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Donnée de test TTL irréaliste**
- **Found during:** Task 2 (test `TreatedStore_set_load_remove_et_purge_TTL`)
- **Issue:** Le plan spécifiait `Set("a", 100)` puis attendait `Load()` contenant `"a"=100`. Or `treatedWaitingTs=100` correspond à 1970 : la purge TTL (`now - ts < 6 h`) le rejette immédiatement → test rouge. La valeur `100` contredit la sémantique du magasin (l'horodatage doit être celui d'un épisode d'attente récent).
- **Fix:** Remplacé `100` par `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` (horodatage récent valide), en conservant l'intention du test (round-trip + purge d'une entrée > 6 h prouvée séparément par l'entrée « vieux »).
- **Files modified:** tests/Chronos.Tests/TreatedSessionsTests.cs
- **Commit:** b1f000f

Aucune autre déviation ; le reste du plan a été exécuté tel qu'écrit.

## Known Stubs

- `IForegroundWatch` n'a PAS d'implémentation réelle dans ce plan — c'est INTENTIONNEL et documenté par le plan : la branche NET-02 est dormante (`foreground=null` en DI) et sera câblée au plan 14-02 (`WindowsForegroundWatch`). Sa logique complète est néanmoins présente dans le tracker et prouvée par un faux focus. Ne bloque pas l'objectif du plan (NET-01/03/04 sont pleinement opérationnels).

## Self-Check: PASSED

- FOUND: src/Chronos/Services/TreatedStore.cs
- FOUND: src/Chronos/Services/IForegroundWatch.cs
- FOUND: src/Chronos/Services/SessionTreatmentTracker.cs
- FOUND: tests/Chronos.Tests/TreatedSessionsTests.cs
- FOUND commit 643c585 (Task 1), b1f000f (Task 2), 491e289 (Task 3)
- Build : 0 avertissement / 0 erreur. Tests : 316/316 verts.
