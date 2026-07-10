---
phase: 13-source-uia-app-bureau
plan: 02
subsystem: sessions
tags: [uia, accessibility, windows-automation, hosted-service, poll, rob-07, os-adapter]

# Dependency graph
requires:
  - phase: 13 (plan 01)
    provides: "UiaNode (DTO neutre) / IUiaTreeProvider / DesktopUiaSessionSource.Poll+Read+MapTree / DesktopHealth"
provides:
  - "WindowsUiaTreeProvider : implémentation RÉELLE d'IUiaTreeProvider (AutomationElement/TreeWalker → UiaNode, AutomationId inclus dont l'ancre RootWebArea), racine cachée entre polls"
  - "DesktopUiaPollService : IHostedService pilotant DesktopUiaSessionSource.Poll hors thread UI (~1,5 s), avec PollOnce testable"
  - "csproj : accès aux assemblies UIAutomationClient/UIAutomationTypes documenté (fournies implicitement par le pack WindowsDesktop via UseWPF)"
affects: [phase-13-plan-03 (câblage DI + SessionMonitor + affichage type)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Adaptateur OS mince isolé derrière un seam (IUiaTreeProvider) : System.Windows.Automation confiné à un seul type, surface publique neutre (UiaNode?)"
    - "Racine (fenêtre Claude) cachée entre polls, réacquise si périmée (test de vivacité via accès à une propriété)"
    - "Walk borné en profondeur (12) et largeur (200) pour rester léger sur fenêtre layered (coûts de composition)"
    - "Poll de fond via Timer .NET (thread du pool) → lecture UIA jamais sur le thread UI (ROB-07)"
    - "PollOnce public : point de test déterministe d'un service de fond sans attendre le timer réel"

key-files:
  created:
    - src/Chronos/Services/WindowsUiaTreeProvider.cs
    - src/Chronos/Services/DesktopUiaPollService.cs
    - tests/Chronos.Tests/DesktopUiaPollServiceTests.cs
  modified:
    - src/Chronos/Chronos.csproj

key-decisions:
  - "Chaque UiaNode émis renseigne AutomationId=Current.AutomationId → l'ancre RootWebArea est réellement produite en PROD (évite le piège « tests verts / prod vide »)"
  - "Aucun <Reference> explicite UIAutomation : les assemblies sont fournies implicitement par Microsoft.WindowsDesktop.App (UseWPF) ; les ajouter déclenche MSB3243/MSB3245 — documenté par commentaire (build 0 warning)"
  - "Racine cachée + réacquisition si périmée ; toute erreur UIA = dégradation silencieuse (TryGetTree → null, ne lève jamais)"
  - "Timer .NET (thread du pool) plutôt qu'une boucle async : rappel garanti hors thread UI, cœur de ROB-07"

patterns-established:
  - "Adaptateur OS non unit-testé (dépend de l'OS) mais mince et sans logique métier ; la logique en aval (MapTree) reste couverte par faux arbre"
  - "Test de la mécanique de poll via PollOnce + faux IUiaTreeProvider (cache vide→peuplé, provider null ne lève pas, horloge injectée)"

requirements-completed: [BUR-01, ROB-07]

# Metrics
duration: 4min
completed: 2026-07-10
---

# Phase 13 Plan 02 : Implémentation réelle UIA + moteur de poll non bloquant — Summary

**La seule couche OS-dépendante de la source bureau : `WindowsUiaTreeProvider` lit l'arbre d'accessibilité de la fenêtre Claude via `System.Windows.Automation` (racine cachée, AutomationId propagé dont l'ancre RootWebArea), et `DesktopUiaPollService` (IHostedService) pilote son poll à ~1,5 s sur un thread du pool — jamais le thread UI (ROB-07).**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-07-10T12:31:23Z
- **Completed:** 2026-07-10T12:35:16Z
- **Tasks:** 2/2
- **Files créés/modifiés:** 4 (3 neufs, 1 modifié)

## Accomplishments
- `WindowsUiaTreeProvider` : implémentation réelle d'`IUiaTreeProvider`. Recherche la fenêtre Claude (ControlType Window, Name contenant « Claude », `TreeScope.Children`), la met en **cache** (`_root`) et la réacquiert si périmée. Walk récursif `TreeWalker.ControlViewWalker` borné (profondeur 12 / largeur 200) projetant chaque `AutomationElement` vers un `UiaNode` neutre.
- **Contrat d'ancre respecté** : chaque `UiaNode` renseigne `AutomationId = Current.AutomationId` (+ ControlType via `ProgrammaticName`, Name, `IsEnabled`). L'ancre `RootWebArea` est donc réellement émise en PROD → `MapTree` reconnaît le foreground (piège « tests verts / prod vide » évité).
- **Dégradation silencieuse** : tout est en try/catch, `TryGetTree` retourne `null` (fenêtre fermée / UIA indisponible / élément périmé), ne lève jamais ; la racine cachée est invalidée en cas d'erreur pour forcer une réacquisition propre.
- **Surface publique neutre** : seul `UiaNode? TryGetTree()` est public — aucun `AutomationElement` en signature publique → `ServicesLayerPurityTests` reste vert.
- `DesktopUiaPollService` : `IHostedService` + `IDisposable`. `Timer` .NET (~1,5 s, thread du pool → **jamais le thread UI**, ROB-07) appelant `PollOnce()` qui pilote `DesktopUiaSessionSource.Poll(clock.UtcNow)`. `PollOnce` public et à l'épreuve des exceptions. Start/Stop/Dispose idempotents.
- csproj : accès aux assemblies UIA (`UIAutomationClient`/`UIAutomationTypes`) documenté ; elles sont fournies **implicitement** par le pack WindowsDesktop (UseWPF) — build **0 avertissement**.
- 4 tests neufs sur la mécanique de poll ; suite complète **298 verts** (294 + 4), aucune régression.

## Task Commits

1. **Task 1: Références UIAutomation + WindowsUiaTreeProvider réel (AutomationElement → UiaNode, AutomationId, racine cachée)** — `8408595` (feat)
2. **Task 2: DesktopUiaPollService — poll de fond non bloquant + PollOnce testable** — `59031e0` (feat)

## Files Created/Modified
- `src/Chronos/Services/WindowsUiaTreeProvider.cs` — adaptateur OS réel (`System.Windows.Automation`), racine cachée, walk borné, AutomationId propagé, dégradation silencieuse. NON unit-testé (dépend de l'OS), sans logique métier.
- `src/Chronos/Services/DesktopUiaPollService.cs` — `IHostedService` de poll de fond (Timer 1,5 s hors thread UI) + `PollOnce` public testable ; type neutre.
- `tests/Chronos.Tests/DesktopUiaPollServiceTests.cs` — 4 tests (cache vide→peuplé, provider null ne lève pas, horloge injectée, cycle Start/Stop idempotent).
- `src/Chronos/Chronos.csproj` — commentaire documentant l'accès UIA via le pack WindowsDesktop (UIAutomationClient/UIAutomationTypes).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking config] Références UIAutomation explicites remplacées par une documentation (0 warning)**
- **Found during:** Task 1
- **Issue:** L'ajout de `<Reference Include="UIAutomationClient" />` / `<Reference Include="UIAutomationTypes" />` comme le suggérait le plan déclenche 4 avertissements (MSB3245 « impossible de trouver l'assembly » + MSB3243 « conflit »), car ces assemblies sont **déjà fournies** par le framework `Microsoft.WindowsDesktop.App` tiré par `UseWPF` (les ref-assemblies net8.0 contiennent `UIAutomationClient.dll`/`UIAutomationTypes.dll`, vérifié sur disque). Le `<Reference>` bare entre alors en conflit avec l'assembly du framework.
- **Fix:** Aucun `<Reference>` explicite ; un commentaire csproj documente que ces assemblies proviennent du pack WindowsDesktop (satisfait le critère « csproj contient UIAutomationClient/UIAutomationTypes »). Le code `using System.Windows.Automation;` compile via la référence implicite du framework.
- **Résultat:** build **0 avertissement / 0 erreur** (au lieu de 4 avertissements). Le plan anticipait d'ailleurs ce cas (« FrameworkReference n'est PAS requis, déjà tiré par UseWPF »).
- **Files modified:** src/Chronos/Chronos.csproj
- **Commit:** 8408595

## Known Stubs

Aucun stub. `WindowsUiaTreeProvider` et `DesktopUiaPollService` sont complets et fonctionnels. Le **câblage DI** (enregistrement de `IUiaTreeProvider`→`WindowsUiaTreeProvider`, `DesktopUiaSessionSource`, `DesktopUiaPollService` en `AddHostedService`, injection dans `SessionMonitor`) et l'**affichage du type** sont HORS périmètre de ce plan (Plan 03, par conception) — pas des stubs mais les artefacts de la vague suivante. Tant que le Plan 03 n'a pas câblé la DI, l'app ne consomme pas encore ces deux types : c'est attendu et documenté (le plan interdit explicitement tout câblage DI ici).

## Verification
- `dotnet build src/Chronos/Chronos.csproj -c Debug` : réussi, **0 avertissement / 0 erreur** (UIA résolue via le framework).
- `dotnet test tests/Chronos.Tests` : **298 réussis / 0 échec** (294 + 4 nouveaux DesktopUiaPollServiceTests).
- `ServicesLayerPurityTests` : vert (WindowsUiaTreeProvider n'expose pas `AutomationElement` publiquement ; DesktopUiaPollService sans `System.Windows`).
- Aucun câblage DI ni modification de SessionMonitor/SessionsViewModel/App.xaml.cs (réservé au Plan 03).

## Self-Check: PASSED
