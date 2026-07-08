---
phase: 03-mod-les-pipeline-de-donn-es
plan: 01
subsystem: data-pipeline
tags: [dotnet, records, immutable, iusageprovider, iclock, xunit, tdd, wpf-purity]

# Dependency graph
requires:
  - phase: 01-fondations-architecture-squelette-overlay
    provides: "Projet Chronos (net8.0-windows), couche Services amorcée (IUiDispatcher/WpfUiDispatcher/TopmostGuard), suite xUnit verte + InternalsVisibleTo"
  - phase: 02-d-couverte-des-sources-bloquante
    provides: "docs/data-sources.md — contrat rate_limits (used_percentage 0..100, resets_at epoch s), repli JSONL, staleness"
provides:
  - "Records immuables neutres UsageSnapshot / WindowState + enums SourceReliability / WindowKind (DAT-03)"
  - "WindowState.FractionRemaining : fraction de temps restante clampée [0..1], null si inconnu (DAT-07)"
  - "Contrat neutre IUsageProvider (GetAsync + SnapshotChanged) figé pour les plans 02/03/04 (DAT-02)"
  - "Horloge injectable IClock/SystemClock + FakeClock (tests déterministes)"
  - "Chemins injectables ChronosPaths (usage.json, projectsRoot) résolus via Environment"
  - "Garde réflexive de pureté WPF (ServicesLayerPurityTests) — filet permanent DAT-02"
affects: [03-02-claude-usage-object-provider, 03-03-jsonl-estimation-provider, 03-04-composite-provider, phase-04-orchestration, phase-05-cadran-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Records sealed immuables à init + nullabilité honnête (null = inconnu, jamais de valeur sentinelle inventée)"
    - "Static pure FractionRemaining(resetsAt, now, windowLength) — testable sans horloge, clamp Math.Clamp"
    - "Contrats neutres dans Chronos.Services (aucun type WPF) ; adaptateurs WPF isolés et allow-listés"
    - "Injection de l'horloge (IClock) et des chemins (ChronosPaths) pour tester sans toucher le vrai profil"
    - "Garde de pureté par réflexion sur les signatures publiques des types Services/Models"

key-files:
  created:
    - src/Chronos/Models/SourceReliability.cs
    - src/Chronos/Models/WindowKind.cs
    - src/Chronos/Models/WindowState.cs
    - src/Chronos/Models/UsageSnapshot.cs
    - src/Chronos/Services/IClock.cs
    - src/Chronos/Services/SystemClock.cs
    - src/Chronos/Services/ChronosPaths.cs
    - src/Chronos/Services/IUsageProvider.cs
    - tests/Chronos.Tests/Fakes/FakeClock.cs
    - tests/Chronos.Tests/WindowStateTests.cs
    - tests/Chronos.Tests/ServicesLayerPurityTests.cs
  modified: []

key-decisions:
  - "Nullabilité honnête : Utilization/ResetsAt/FractionTimeRemaining nullables ; null = inconnu, jamais inventé (Core Value)"
  - "Exhausted dérivé (Utilization is >= 1.0), non stocké — évite un champ contradictoire"
  - "FractionRemaining prend `now` en paramètre direct (pas d'IClock) → modèles purs testables sans horloge"
  - "Garde de pureté WPF avec allow-list nominative des adaptateurs Phase 1 (WpfUiDispatcher, TopmostGuard) plutôt que de déplacer des fichiers Phase 1"

patterns-established:
  - "Modèles immuables nullable-safe (record sealed { init } + factories Unavailable/Empty)"
  - "Frontière Services neutre gardée automatiquement par test de réflexion (DAT-02)"

requirements-completed: [DAT-02, DAT-03, DAT-07]

# Metrics
duration: 5min
completed: 2026-07-08
---

# Phase 3 Plan 01: Fondation neutre du pipeline de données Summary

**Records immuables nullable-safe (UsageSnapshot/WindowState) + FractionRemaining clampée, contrat neutre IUsageProvider, horloge/chemins injectables, et garde réflexive prouvant zéro type WPF dans Services/Models.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-07-08T14:38:53Z
- **Completed:** 2026-07-08T14:43:33Z
- **Tasks:** 3
- **Files modified:** 11 créés (8 source, 3 tests)

## Accomplishments
- Modèles immuables neutres (`WindowState`, `UsageSnapshot`, enums `SourceReliability`/`WindowKind`) avec nullabilité honnête — DAT-03 prouvé par 10 tests.
- `WindowState.FractionRemaining` : fraction de temps restante clampée [0..1], null si reset inconnu ou fenêtre non positive — DAT-07 prouvé par Theory (milieu, >fenêtre, dépassé, null, len=0).
- Contrat neutre `IUsageProvider` (GetAsync + SnapshotChanged) figé + `IClock`/`SystemClock` + `ChronosPaths` injectables + `FakeClock` — DAT-02.
- Garde réflexive `ServicesLayerPurityTests` : filet permanent prouvant qu'aucun assembly WPF n'apparaît dans les signatures des types neutres Services/Models.
- Suite complète verte : 14 tests (3 Phase 1 + 10 WindowStateTests + 1 purity).

## Task Commits

Each task was committed atomically:

1. **Task 1: Modèles immuables + FractionRemaining (TDD)** - `ce67a80` (test RED) → `17332a2` (feat GREEN)
2. **Task 2: Contrats neutres + horloge + chemins injectables** - `80e97b9` (feat)
3. **Task 3: Garde réflexive de pureté WPF** - `35cdf63` (test)

**Plan metadata:** _(commit docs de fin de plan)_

_Note: Task 1 en TDD (RED → GREEN, pas de refactor nécessaire)._

## Files Created/Modified
- `src/Chronos/Models/SourceReliability.cs` - Enum provenance (Exact/Estimated/Unavailable)
- `src/Chronos/Models/WindowKind.cs` - Enum fenêtres (FiveHour/SevenDay)
- `src/Chronos/Models/WindowState.cs` - Record immuable d'une fenêtre + Exhausted + Unavailable + static FractionRemaining
- `src/Chronos/Models/UsageSnapshot.cs` - Record immuable des deux fenêtres + SourceCapturedAt/Age + Empty
- `src/Chronos/Services/IClock.cs` - Horloge injectable (UtcNow)
- `src/Chronos/Services/SystemClock.cs` - Horloge réelle (DateTimeOffset.UtcNow)
- `src/Chronos/Services/ChronosPaths.cs` - Chemins injectables (usage.json, projectsRoot) via Environment
- `src/Chronos/Services/IUsageProvider.cs` - Contrat neutre GetAsync + SnapshotChanged (DAT-02)
- `tests/Chronos.Tests/Fakes/FakeClock.cs` - Horloge déterministe pour tests
- `tests/Chronos.Tests/WindowStateTests.cs` - 10 tests (Exhausted, Unavailable, Empty, FractionRemaining)
- `tests/Chronos.Tests/ServicesLayerPurityTests.cs` - Garde réflexive pureté WPF

## Decisions Made
- **Nullabilité honnête** : `Utilization`/`ResetsAt`/`FractionTimeRemaining` nullables ; null encode l'inconnu, jamais de valeur sentinelle inventée (aligné Core Value « ne jamais présenter une estimation comme exacte »).
- **Exhausted dérivé** (`Utilization is >= 1.0`) plutôt que champ stocké → pas de contradiction possible.
- **FractionRemaining reçoit `now` en paramètre** (pas d'IClock dans le modèle) → modèles auto-suffisants, testables sans horloge et sans dépendance vers Services.
- **Garde de pureté avec allow-list** des deux adaptateurs WPF de Phase 1, au lieu de déplacer/refactorer des fichiers Phase 1 (choix non destructif, voir Déviations).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Garde de pureté : faux échec sur les adaptateurs WPF de Phase 1**
- **Found during:** Task 3 (garde réflexive de pureté)
- **Issue:** Le snippet de test verbatim du plan itère TOUS les types de `Chronos.Services`. Or ce namespace contient déjà, depuis Phase 1, deux adaptateurs volontairement WPF (`WpfUiDispatcher` encapsulant `Dispatcher`, `TopmostGuard` utilisant `Window`/`DispatcherTimer`). Le test échouait donc sur `PresentationFramework`. Un contrôle au niveau assembly est impossible : Models/Services vivent dans le MÊME assembly que l'app WPF (Chronos.dll = WinExe WPF), qui référence forcément WPF — seule l'inspection par signature de type est pertinente.
- **Fix:** Ajout d'une allow-list nominative documentée (`WpfUiDispatcher`, `TopmostGuard`) excluant uniquement ces deux adaptateurs-frontière ; tous les autres types Services/Models (contrats + pipeline de données) restent gardés. Toute NOUVELLE fuite WPF (dans un modèle ou un provider) échoue toujours. Aucun fichier Phase 1 déplacé (ce serait un changement architectural).
- **Files modified:** tests/Chronos.Tests/ServicesLayerPurityTests.cs
- **Verification:** `dotnet test --filter "FullyQualifiedName~ServicesLayerPurity"` vert ; suite complète verte (14 tests).
- **Committed in:** `35cdf63` (Task 3 commit)

**2. [Rule 3 - Blocking] `using System.IO;` manquant dans ChronosPaths.cs**
- **Found during:** Task 2 (chemins injectables)
- **Issue:** Le snippet verbatim du plan utilise `Path.Combine` mais `System.IO` n'est pas dans les ImplicitUsings de ce projet WPF → erreur de build CS0103 (`Path` introuvable).
- **Fix:** Ajout de `using System.IO;` en tête de ChronosPaths.cs.
- **Files modified:** src/Chronos/Services/ChronosPaths.cs
- **Verification:** `dotnet build Chronos.sln -c Debug` réussit (0 erreur, 0 avertissement).
- **Committed in:** `80e97b9` (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 bug de test faux-positif, 1 blocage de build)
**Impact on plan:** Les deux correctifs sont nécessaires à la correction/compilation. Aucun scope creep ; la garde de pureté reste stricte sur tout le pipeline de données. Contrats et modèles conformes au plan verbatim.

## Issues Encountered
None — les 3 tâches ont été exécutées dans l'ordre prévu (modèles avant contrats pour un build mécaniquement compilable).

## User Setup Required
None - aucune configuration de service externe requise.

## Next Phase Readiness
- `UsageSnapshot`, `WindowState`, `IUsageProvider`, `IClock`/`SystemClock`, `ChronosPaths`, `FakeClock` disponibles et figés pour le plan 03-02 (`ClaudeUsageObjectProvider`, DAT-04).
- Garde de pureté WPF en place : toute implémentation de provider ajoutée aux plans suivants sera automatiquement vérifiée neutre.
- Prêt pour 03-02.

## Self-Check: PASSED

- 11/11 fichiers créés présents sur disque.
- 4/4 commits de tâche présents (ce67a80, 17332a2, 80e97b9, 35cdf63).
- Build solution : 0 erreur / 0 avertissement. Suite complète : 14 tests verts.
