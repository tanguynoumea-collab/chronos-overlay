---
phase: 14-auto-disparition-hysteresis
plan: 02
subsystem: sessions
tags: [hysteresis, net-02, focus, win32-interop, mvvm-neutral, di-guard]
requires:
  - "IForegroundWatch (seam neutre du focus, plan 01)"
  - "SessionMonitor.Read (ctor 7e param foreground, plan 01)"
  - "SessionTreatmentTracker (branche NET-02 déjà implémentée + testée par faux focus, plan 01)"
  - "WindowsUiaTreeProvider (patron du service OS-dépendant non unit-testé, Phase 13)"
provides:
  - "WindowsForegroundWatch : implémentation RÉELLE d'IForegroundWatch (Win32 GetForegroundWindow, titre « Claude »)"
  - "Branche NET-02 VIVANTE en production (focus premier-plan OS réel injecté dans SessionMonitor)"
  - "Garde DI réelle étendue : résolution du graphe bureau complet avec focus injecté"
affects:
  - "src/Chronos/App.xaml.cs"
  - "tests/Chronos.Tests/CompositionRootTests.cs"
tech-stack:
  added: []
  patterns:
    - "Service OS-dépendant MINCE, Win32 P/Invoke pur, best-effort ne lève jamais (calqué WindowsUiaTreeProvider)"
    - "Surface publique neutre : bool exposé, aucun HWND — couche Services garde sa pureté (ServicesLayerPurityTests)"
    - "Câblage OS réel prouvé UNIQUEMENT par la garde DI (résolution), pas par unit test (comportement OS non testable)"
key-files:
  created:
    - "src/Chronos/Services/WindowsForegroundWatch.cs"
  modified:
    - "src/Chronos/App.xaml.cs"
    - "tests/Chronos.Tests/CompositionRootTests.cs"
decisions:
  - "Critère de focus = titre de la fenêtre de premier plan contient « Claude » (insensible casse) — MÊME critère souple que la découverte de fenêtre UIA de la Phase 13, pour la cohérence et la robustesse aux versions de l'app."
  - "GetForegroundWindow + GetWindowText (rapides, quelques µs) appelables depuis le chemin synchrone Read (thread UI) sans risque de figer l'overlay — pas de walk UIA coûteux pour le focus."
  - "Aucun HWND public : seul un bool est exposé → la couche Services reste neutre (Win32 n'est pas un assembly WPF interdit)."
metrics:
  duration: "~2 min"
  completed: "2026-07-10"
  tasks: 2
  files: 3
  tests_added: 0
  tests_total: 316
---

# Phase 14 Plan 02 : Focus premier-plan OS réel — branche NET-02 vivante — Summary

Implémentation OS-dépendante `WindowsForegroundWatch` (Win32 `GetForegroundWindow`/`GetWindowText`, titre « Claude ») injectée comme 7e paramètre de `SessionMonitor` : la branche NET-02 (acquittement par focus), prouvée au plan 01 avec un faux focus, devient VIVANTE en production. La garde DI réelle est étendue à la sous-chaîne bureau complète (focus injecté) pour attraper au démarrage un câblage NET-02 mal ordonné.

## Ce qui a été livré

- **`WindowsForegroundWatch`** — implémentation RÉELLE et OS-dépendante d'`IForegroundWatch`, MINCE et sans logique métier (patron `WindowsUiaTreeProvider`). Win32 P/Invoke pur (`user32.dll` : `GetForegroundWindow`, `GetWindowText`), AUCUN type WPF → couche Services neutre préservée. `IsClaudeForeground()` : entièrement sous `try/catch` → `false` (best-effort, ne lève jamais). Critère = titre de la fenêtre de premier plan contient « Claude » (insensible à la casse), le MÊME que la découverte de fenêtre UIA Phase 13. Rapide (quelques µs) et non bloquant → appelable depuis le chemin synchrone `Read` (thread UI) sans figer l'overlay. Type NON unit-testé (dépend d'une vraie fenêtre + de l'OS) : sa correction se juge en exécution, la garde DI ne prouve que le câblage.
- **DI (`App.xaml.cs`)** — `IForegroundWatch` → `WindowsForegroundWatch` enregistré (Singleton) AVANT `SessionMonitor`, puis injecté comme 7e paramètre `foreground`. Le `foreground=null` dormant du plan 01 est remplacé par le focus réel → NET-02 opérationnel.
- **Garde DI réelle (`CompositionRootTests`)** — `Le_graphe_DI_resout_les_services_bureau_UIA` reproduit désormais EXACTEMENT la sous-chaîne bureau d'`App.xaml.cs` après ce plan : `TreatedStore` + `SessionTreatmentTracker` + `IForegroundWatch` + `SessionMonitor` à 7 paramètres. Prouve la résolution sans exception de `SessionMonitor` et `DesktopUiaPollService` (câblage NET-02 dans le bon ordre).

## Comportement NET-02 en production

- Fenêtre Claude au premier plan de l'OS (titre « Claude ») + session bureau en attente + focus continu ≥ ~2,5 s ⇒ acquittement (le tracker `Set` la session, elle disparaît). Logique déjà prouvée au plan 01 ; ce plan branche le signal réel.
- Focus indisponible / erreur d'interop ⇒ `IsClaudeForeground()` renvoie `false` ⇒ NET-02 ne déclenche pas, sans exception ni faux traitement (honnêteté/robustesse du CONTEXT.md). NET-01 (répondu) reste indépendant du focus.

## Tests

Aucun test unitaire ajouté (le comportement OS de `WindowsForegroundWatch` n'est pas testable sans vraie fenêtre — comme `WindowsUiaTreeProvider`). Le câblage est prouvé par la garde DI réelle étendue. Suite complète : 316/316 verte, dont `ServicesLayerPurityTests` (couche neutre préservée : aucun assembly WPF, aucun HWND public).

## Deviations from Plan

None - plan exécuté exactement comme écrit.

## État final du milestone v1.4

Dernière wave de la dernière phase du milestone. Couverture des requirements NET :

- **NET-01 (répondu)** — livré plan 01 (tracker + filtre `treated`), opérationnel sans UIA.
- **NET-02 (acquitté par focus)** — logique plan 01, **focus OS réel branché ce plan** ⇒ VIVANT en production.
- **NET-03 (réapparition)** — livré plan 01 (purge sur nouvel épisode d'attente plus récent).
- **NET-04 (archivage permanent)** — `ArchiveStore` distinct/prioritaire, contraste réversible vs permanent prouvé plan 01.

NET-01..04 tous couverts. Phase 14 complète (2/2 plans). **Milestone v1.4 prêt pour l'audit.**

## Known Stubs

Aucun. Le seul stub connu du plan 01 (`IForegroundWatch` sans implémentation réelle, branche NET-02 dormante) est RÉSOLU par ce plan : `WindowsForegroundWatch` fournit le signal OS réel et est injecté dans `SessionMonitor`.

## Self-Check: PASSED

- FOUND: src/Chronos/Services/WindowsForegroundWatch.cs
- FOUND commit 8f488d8 (Task 1), 386c07e (Task 2)
- Build : 0 avertissement / 0 erreur. Tests : 316/316 verts.
