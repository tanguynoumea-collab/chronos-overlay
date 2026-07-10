---
phase: 13-source-uia-app-bureau
plan: 03
subsystem: sessions
tags: [uia, di, hosted-service, mvvm, session-type, integration, fr-labels]

# Dependency graph
requires:
  - phase: 13 (plan 01)
    provides: "ISessionSource / SessionSnapshot(Kind,Origin) / DesktopUiaSessionSource (MapTree + cache)"
  - phase: 13 (plan 02)
    provides: "WindowsUiaTreeProvider (IUiaTreeProvider réel) / DesktopUiaPollService (IHostedService poll ~1,5 s)"
provides:
  - "SessionMonitor.Read fusionne la source bureau (ISessionSource? optionnelle) APRÈS transcripts + hooks, avant le filtre archived — non bloquant, non cassant"
  - "Câblage DI complet de la chaîne bureau : IUiaTreeProvider→WindowsUiaTreeProvider, DesktopUiaSessionSource, SessionMonitor(desktop), DesktopUiaPollService en AddHostedService"
  - "Garde DI réelle (CompositionRootTests) : résolution prouvée de SessionMonitor + DesktopUiaPollService (attrape une DI mal ordonnée/non enregistrée qui ne planterait qu'au démarrage)"
  - "Widget : SessionItemVm.KindLabel (Chat/Code/Cowork ; vide pour CLI) + badge XAML masqué quand vide"
affects: [phase-14 (hystérésis / sessions traitées — consomme les clés desktop:... et le type)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Paramètre optionnel EN FIN de ctor (ISessionSource? desktop = null) → extension non cassante d'une composition existante"
    - "Fusion additive tolérante (try/catch) d'une source de fond dans un pipeline synchrone non bloquant (ROB-07)"
    - "Test de résolution DI RÉEL reproduisant la sous-chaîne d'App.xaml.cs → garde au-delà de `dotnet build` (attrape le crash-au-démarrage, pas seulement le crash-au-build)"
    - "Badge XAML conditionnel via DataTrigger sur chaîne vide (séparateur + libellé masqués ensemble) — pas de bruit pour les sessions sans type"

key-files:
  created: []
  modified:
    - src/Chronos/Services/SessionMonitor.cs
    - src/Chronos/App.xaml.cs
    - src/Chronos/ViewModels/SessionsViewModel.cs
    - src/Chronos/Views/SessionsWindow.xaml
    - tests/Chronos.Tests/SessionsTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs

key-decisions:
  - "desktop ajouté en 4e paramètre optionnel du ctor SessionMonitor → App.xaml.cs et tous les tests existants compilent inchangés (non-régression garantie)"
  - "Fusion bureau APRÈS transcripts + hooks et AVANT le filtre archived : les clés desktop:... sont disjointes des session_id JSONL (fusion additive) ET restent archivables au clic droit"
  - "Lecture bureau entourée d'un try/catch dans Read : une source bureau ne casse JAMAIS le pipeline des sessions CLI"
  - "Ordre d'enregistrement DI explicite (IUiaTreeProvider + DesktopUiaSessionSource AVANT SessionMonitor) documenté ; garde par test de résolution réel plutôt que par simple build"
  - "KindLabel vide pour Kind=Unknown (sessions CLI) → aucune pollution visuelle ; Chat/Code/Cowork conservés tels quels (noms propres de modes)"

patterns-established:
  - "Faux ISessionSource (liste fixe) pour prouver la fusion et l'affichage du type sans fenêtre Claude ni UIA"

requirements-completed: [BUR-01, BUR-03]

# Metrics
duration: 4min
completed: 2026-07-10
---

# Phase 13 Plan 03 : Intégration finale de la source bureau (fusion + DI + affichage du type) — Summary

**La source bureau UIA est branchée de bout en bout dans le pipeline vivant : fusion non bloquante dans `SessionMonitor.Read` (après transcripts + hooks, avant archived), câblage DI complet (provider réel → source → poll de fond IHostedService → SessionMonitor → widget), et affichage du type Chat/Code/Cowork dans la pastille — prouvé par une garde DI réelle (résolution de `SessionMonitor` + `DesktopUiaPollService`) et 6 tests neufs, suite verte 304.**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-07-10T12:38:17Z
- **Completed:** 2026-07-10T12:42:20Z
- **Tasks:** 3/3
- **Files modifiés:** 6 (4 src, 2 tests)

## Accomplishments
- `SessionMonitor` : ctor étendu d'un `ISessionSource? desktop = null` (4e paramètre optionnel, non cassant). `Read` fusionne le cache bureau APRÈS transcripts + hooks et AVANT le filtre archived, sous try/catch tolérant → lecture non bloquante (ROB-07), sessions CLI jamais cassées.
- `App.xaml.cs` : chaîne bureau câblée en Singletons dans le bon ordre — `IUiaTreeProvider`→`WindowsUiaTreeProvider`, `DesktopUiaSessionSource`, `SessionMonitor(null,null,archive,desktop)`, `DesktopUiaPollService` + `AddHostedService` (démarré/arrêté par le host comme `RefreshOrchestrator`).
- **Garde DI réelle** : nouveau test `Le_graphe_DI_resout_les_services_bureau_UIA` (CompositionRootTests) qui reproduit EXACTEMENT la sous-chaîne bureau d'App.xaml.cs et asserte la résolution sans exception de `SessionMonitor` ET `DesktopUiaPollService` — attrape une DI mal ordonnée / un service manquant qui compilerait mais planterait au démarrage.
- Widget : `SessionItemVm.KindLabel` (`[ObservableProperty]`) alimenté par un helper `KindText(SessionKind)` (Chat/Code/Cowork, vide pour Unknown) ; badge XAML dans la pastille masqué (avec son séparateur, via `DataTrigger` sur chaîne vide) quand le type est absent → aucun bruit pour les sessions CLI.
- 6 tests neufs (fusion bureau+CLI, non-régression `desktop=null`, archivage d'une session bureau, garde DI réelle, KindLabel Code/vide, mappage Chat/Cowork). Suite complète **304 verte** (298 + 6), MVVM strict, pureté Services préservée.

## Task Commits

1. **Task 1: Fusionner la source bureau (ISessionSource) dans SessionMonitor.Read** — `f39ebd6` (feat)
2. **Task 2: Câbler la source bureau UIA dans la DI + garde DI réelle** — `f812585` (feat)
3. **Task 3: Afficher le type (Chat/Code/Cowork) dans le widget (BUR-03)** — `4ae8f38` (feat)

## Files Modified
- `src/Chronos/Services/SessionMonitor.cs` — ctor + `_desktop` ; étape de fusion bureau (2.b) dans `Read`, try/catch tolérant, avant le filtre archived.
- `src/Chronos/App.xaml.cs` — bloc « Widget de sessions » : enregistrement des 3 services UIA + injection dans `SessionMonitor` + poll en `AddHostedService`.
- `src/Chronos/ViewModels/SessionsViewModel.cs` — `SessionItemVm.KindLabel` + helper `KindText` + affectation dans `Refresh`.
- `src/Chronos/Views/SessionsWindow.xaml` — badge de type (KindLabel) dans la pastille, masqué quand vide.
- `tests/Chronos.Tests/SessionsTests.cs` — `FakeSessionSource` + 5 tests (fusion, non-régression, archivage bureau, affichage/mappage du type).
- `tests/Chronos.Tests/CompositionRootTests.cs` — test de résolution DI réel des services bureau.

## Deviations from Plan

Aucune déviation. Le plan a été exécuté exactement comme écrit.

Note de contexte (non-déviation) : `IClock` est enregistré dans `ConfigureServices` APRÈS le bloc « Widget de sessions », mais la résolution DI étant paresseuse, `DesktopUiaPollService` (qui dépend d'`IClock`) se résout sans problème — exactement comme le `SessionsController` préexistant qui consomme déjà `IClock` depuis le même bloc. Le test de résolution réel confirme le câblage.

## Known Stubs

Aucun stub. La chaîne bureau est complète et vivante : provider réel → cache → poll de fond → fusion → widget typé. Quand l'app bureau Claude est fermée, la source rend un cache vide (dégradation tracée `WindowMissing`) → aucune pastille bureau, sessions CLI intactes, aucun crash.

## État final de la phase 13 (dernière wave)

Phase 13 « Source UIA app bureau » **terminée** (3/3 plans). Couverture des requirements de la phase :

| Req | Couvert par | Objet |
|-----|-------------|-------|
| BUR-01 | Plan 02 + Plan 03 | Session de l'app bureau visible dans le widget (poll réel + fusion) |
| BUR-02 | Plan 01 | États honnêtes (Working/WaitingAttention/WaitingTurn/Unknown) |
| BUR-03 | Plan 01 + Plan 03 | Type Chat/Code/Cowork identifié ET affiché dans la pastille |
| BUR-04 | Plan 01 | Sessions actives de la sidebar énumérées (préfixe « En cours d'exécution ») |
| BUR-05 | Plan 01 | Cowork VM (Contrôle à distance) : présence signalée, état forcé Unknown |
| ROB-06 | Plan 01 | Table de libellés fr/en extensible + matching tolérant, dégradation tracée |
| ROB-07 | Plan 02 + Plan 03 | Lecture UIA hors thread UI (poll de fond) ; `Read` non bloquant (cache) |

Tous les BUR (01–05) et ROB (06–07) de la phase sont couverts. Suite de tests **304 verte** (28 + 4 + 6 neufs sur la phase). Prochaine étape : Phase 14 (hystérésis / auto-disparition des sessions traitées), qui consommera les clés `desktop:...` et le type produits ici.

## Verification
- `dotnet build src/Chronos/Chronos.csproj -c Debug` : réussi, **0 avertissement / 0 erreur**.
- `dotnet test tests/Chronos.Tests` : **304 réussis / 0 échec** (298 + 6 neufs).
- `CompositionRootTests.Le_graphe_DI_resout_les_services_bureau_UIA` : vert (SessionMonitor + DesktopUiaPollService résolus).
- `ServicesLayerPurityTests` : vert (aucun type WPF introduit côté Services ; l'ajout dans SessionMonitor reste neutre).

## Self-Check: PASSED

- 7/7 fichiers présents sur disque.
- 3/3 commits de tâche présents (f39ebd6, f812585, 4ae8f38).
