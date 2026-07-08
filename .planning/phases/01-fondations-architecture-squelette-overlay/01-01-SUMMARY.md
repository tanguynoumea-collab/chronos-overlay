---
phase: 01-fondations-architecture-squelette-overlay
plan: 01
subsystem: infra
tags: [wpf, dotnet8, generic-host, dependency-injection, mvvm, communitytoolkit-mvvm, xunit, stafact, overlay]

# Dependency graph
requires: []
provides:
  - "Solution Chronos.sln (format classique) avec projet applicatif WPF et projet de tests xUnit"
  - "Projet src/Chronos net8.0-windows, UseWPF, propriétés de publish conditionnées (jamais actives en build/debug)"
  - "Composition root Generic Host dans App.xaml.cs (StartAsync → GetRequiredService<MainWindow> → Show ; OnExit dispose déterministe)"
  - "Fenêtre overlay MainWindow conforme FEN-01 (borderless, transparente, topmost, hors barre des tâches, sans vol de focus, carrée + placeholder)"
  - "Abstraction IUiDispatcher (contrat de marshaling UI neutre, sans type WPF) + impl WpfUiDispatcher"
  - "MainViewModel racine (ObservableObject) injecté dans MainWindow"
  - "Tests automatisés : OverlayWindowConfigTests (FEN-01) + CompositionRootTests (résolution/disposition DI = SC3)"
affects: [02-plomberie-topmost-guard, 03-lancement-visuel, cadran, providers, persistance]

# Tech tracking
tech-stack:
  added:
    - "CommunityToolkit.Mvvm 8.4.2"
    - "Microsoft.Extensions.Hosting 8.0.1 (tire DependencyInjection transitivement)"
    - "xunit 2.9.2 + xunit.runner.visualstudio 2.8.2 + Xunit.StaFact 1.1.11 + Microsoft.NET.Test.Sdk 17.11.1"
  patterns:
    - "Composition root = Generic Host dans App (pas de StartupUri)"
    - "MVVM strict : MainViewModel injecté, DataContext posé dans le code-behind"
    - "Frontière de thread UI abstraite par IUiDispatcher (couche Services sans type WPF)"
    - "Tests WPF sous contexte STA via [WpfFact] (Xunit.StaFact)"

key-files:
  created:
    - "Chronos.sln"
    - ".gitignore"
    - "src/Chronos/Chronos.csproj"
    - "src/Chronos/app.manifest"
    - "src/Chronos/App.xaml"
    - "src/Chronos/App.xaml.cs"
    - "src/Chronos/Services/IUiDispatcher.cs"
    - "src/Chronos/Services/WpfUiDispatcher.cs"
    - "src/Chronos/ViewModels/MainViewModel.cs"
    - "src/Chronos/Views/MainWindow.xaml"
    - "src/Chronos/Views/MainWindow.xaml.cs"
    - "tests/Chronos.Tests/Chronos.Tests.csproj"
    - "tests/Chronos.Tests/OverlayWindowConfigTests.cs"
    - "tests/Chronos.Tests/CompositionRootTests.cs"
  modified: []

key-decisions:
  - "Solution en format .sln classique (le SDK .NET 10 génère .slnx par défaut ; forcé via --format sln pour conformité au plan)"
  - "Placement de départ non persisté au coin supérieur droit (marge 24 px, SystemParameters.WorkArea)"
  - "TopmostGuard/NativeMethods volontairement absents (livrés en Plan 02, ROB-04)"

patterns-established:
  - "Generic Host comme unique point de câblage DI + cycle de vie"
  - "Overlay WPF : trio WindowStyle=None + AllowsTransparency=True + Background=Transparent + ShowActivated=False"
  - "Indirection testable : marqueur IDisposable pour prouver la disposition des Singletons"

requirements-completed: [FEN-01]

# Metrics
duration: 4min
completed: 2026-07-08
---

# Phase 1 Plan 01 : Fondations architecture + squelette overlay Summary

**Squelette WPF net8.0-windows câblé par Generic Host (composition root sans StartupUri), fenêtre overlay conforme FEN-01 avec placeholder, et couverture de tests STA (FEN-01 + résolution/disposition DI).**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-07-08T13:26:44Z
- **Completed:** 2026-07-08T13:30:31Z
- **Tasks:** 3
- **Files modified:** 14 créés

## Accomplishments
- Solution `Chronos.sln` + projet applicatif WPF `net8.0-windows` + projet de tests xUnit, tout compile en Debug (0 erreur, 0 warning).
- Composition root Generic Host complet dans `App.xaml.cs` : `StartAsync` → `GetRequiredService<MainWindow>` → `Show()`, et `OnExit` avec dispose déterministe (`StopAsync().GetAwaiter().GetResult()` + `Dispose()`).
- Fenêtre overlay `MainWindow` portant les 6 propriétés FEN-01 (WindowStyle=None, AllowsTransparency, Topmost, ShowInTaskbar=False, ShowActivated=False, ResizeMode=NoResize) + placeholder Ellipse visible.
- Abstraction `IUiDispatcher` neutre (aucun type WPF) + implémentation `WpfUiDispatcher`, préparant la frontière de thread pour les phases données.
- Deux tests STA verts : `OverlayWindowConfigTests` (FEN-01) et `CompositionRootTests` (résolution DI + disposition Singleton = success criterion 3).

## Task Commits

Chaque tâche a été committée atomiquement :

1. **Task 1: Scaffold solution + projet applicatif + projet de tests** - `5ef940b` (feat)
2. **Task 2: Abstractions Services + ViewModel + fenêtre overlay (FEN-01)** - `d96f308` (feat)
3. **Task 3: Câblage composition root Generic Host complet + tests (SC3 + FEN-01)** - `9b909be` (feat)

## Files Created/Modified
- `Chronos.sln` - Solution classique référençant les deux projets
- `.gitignore` - Gabarit VisualStudio/.NET (bin/, obj/, .vs/, *.user)
- `src/Chronos/Chronos.csproj` - Projet WPF net8.0-windows, UseWPF, publish conditionné sur `PublishSingleFile`
- `src/Chronos/app.manifest` - DPI PerMonitorV2 + supportedOS Win10/11
- `src/Chronos/App.xaml` - Ressources globales, sans démarrage automatique par URI
- `src/Chronos/App.xaml.cs` - Composition root Generic Host + cycle de vie
- `src/Chronos/Services/IUiDispatcher.cs` - Contrat de marshaling UI neutre
- `src/Chronos/Services/WpfUiDispatcher.cs` - Impl encapsulant le Dispatcher WPF
- `src/Chronos/ViewModels/MainViewModel.cs` - ViewModel racine ObservableObject (vide)
- `src/Chronos/Views/MainWindow.xaml` - Overlay FEN-01 + placeholder Ellipse
- `src/Chronos/Views/MainWindow.xaml.cs` - Injection VM + placement coin supérieur droit
- `tests/Chronos.Tests/Chronos.Tests.csproj` - Projet de tests net8.0-windows, UseWPF, StaFact
- `tests/Chronos.Tests/OverlayWindowConfigTests.cs` - Assertions FEN-01 sous STA
- `tests/Chronos.Tests/CompositionRootTests.cs` - Résolution + disposition DI (SC3)

## Decisions Made
- **Format de solution `.sln` classique** : le SDK .NET 10 génère par défaut le nouveau format `.slnx` (XML). Le plan exige `Chronos.sln` (fichiers, acceptance criteria, commandes). J'ai forcé le format classique via `dotnet new sln --format sln`.
- **Placement de départ** : coin supérieur droit non persisté (marge 24 px via `SystemParameters.WorkArea`), conformément à la recommandation de la question ouverte n°1 du RESEARCH (choix laissé à discrétion, non bloquant).
- **TopmostGuard / NativeMethods absents** : le RESEARCH.md présente la version complète avec `TopmostGuard`, mais le PLAN 01-01 l'exclut explicitement (ROB-04 = Plan 02). Le code du plan (MainWindow ctor à un seul paramètre, ConfigureServices sans TopmostGuard) a été suivi tel quel.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Format de solution .slnx incompatible avec le plan**
- **Found during:** Task 1 (scaffold de la solution)
- **Issue:** `dotnet new sln` sous SDK .NET 10 génère `Chronos.slnx` (nouveau format XML) au lieu de `Chronos.sln`. Les fichiers attendus, les acceptance criteria (`dotnet sln Chronos.sln list`) et les commandes de build du plan référencent `Chronos.sln`. `dotnet sln Chronos.sln add` échouait (« introuvable »).
- **Fix:** Suppression du `.slnx` et régénération en format classique via `dotnet new sln --format sln`.
- **Files modified:** Chronos.sln
- **Verification:** `dotnet sln Chronos.sln list` liste les deux projets ; `dotnet build Chronos.sln` réussit.
- **Committed in:** 5ef940b (Task 1 commit)

**2. [Rule 1 - Bug] Le commentaire d'App.xaml contenait le token « StartupUri »**
- **Found during:** Task 1 (vérification acceptance criteria)
- **Issue:** Le commentaire verbatim du RESEARCH (« Aucun StartupUri… ») contient le mot littéral `StartupUri`, ce qui faisait échouer l'acceptance criterion `grep -q "StartupUri" src/Chronos/App.xaml` doit retourner FAUX (l'attribut lui-même est bien absent, mais le grep matchait le commentaire).
- **Fix:** Reformulation du commentaire en « Aucun démarrage automatique par URI… » — l'attribut `StartupUri` reste absent, l'intent est préservé, le grep retourne désormais FAUX.
- **Files modified:** src/Chronos/App.xaml
- **Verification:** `grep -q "StartupUri" src/Chronos/App.xaml` → absent (OK).
- **Committed in:** 5ef940b (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug de conformité)
**Impact on plan:** Les deux ajustements sont d'ordre outillage/conformité et n'altèrent pas le périmètre ni l'architecture. Aucun scope creep.

## Issues Encountered
- Avertissement CS0414 (`App._host` assigné mais inutilisé) présent après Task 1/Task 2 — attendu, car le champ n'est réellement consommé qu'après le câblage du host en Task 3. Résolu naturellement à Task 3 (build final : 0 warning).

## User Setup Required
None - aucune configuration de service externe requise.

## Next Phase Readiness
- Squelette buildable et testé, prêt pour le Plan 02 (ROB-04 : `TopmostGuard` + `NativeMethods` P/Invoke SetWindowPos, réaffirmation périodique du topmost).
- Le placeholder de fenêtre matérialise l'empreinte carrée du futur cadran (Phase 5) sans retouche de dimension nécessaire.
- Validation visuelle (`dotnet run --project src/Chronos`) prévue en Plan 03 (smoke manuel) — non exécutée ici (pas de checkpoint dans ce plan).
- Aucun blocker introduit.

## Self-Check: PASSED

Tous les fichiers déclarés existent (14 sources + SUMMARY) et les 3 commits de tâche sont présents dans l'historique git.

---
*Phase: 01-fondations-architecture-squelette-overlay*
*Completed: 2026-07-08*
