---
phase: 11-int-gration-composite-r-glage
plan: 02
subsystem: ui
tags: [oauth, usage, wpf, mvvm, menu-contextuel, settings, csharp, dotnet, relaycommand]

# Dependency graph
requires:
  - phase: 11-01
    provides: "ChronosSettings.OAuthUsageEnabled (défaut true), GatedOAuthUsageProvider, chaîne DI composite à 3"
  - phase: 06 (v1.0)
    provides: "SettingsService atomique + menu contextuel (seul point d'accès UI), pattern GAP-1 Load frais → with → Save"
  - phase: 05 (v1.0)
    provides: "WindowGaugeViewModel.Apply (IsEstimated = Reliability==Estimated), badges « estimée » liés"
provides:
  - "MainViewModel.IsOAuthUsageEnabled + ToggleOAuthUsageCommand (persiste GAP-1 + RequestRefresh)"
  - "MenuItem cochable « Usage exact (OAuth) » dans MainWindow.xaml"
  - "Preuve test INT-02 : fenêtre Exact → badge « estimée » masqué + Utilization réelle (WindowGaugeViewModelTests)"
affects: [milestone-v1.2-complete, uat-humain-11]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Toggle menu persisté GAP-1 : Load DISQUE frais → with { flag } → Save → RequestRefresh (calqué sur CalibrateBudgets)"
    - "Caractérisation VM d'un état existant (INT-02) sans nouveau binding : le toggle d'affichage vit déjà côté WindowGaugeViewModel.Apply"

key-files:
  created:
    - tests/Chronos.Tests/WindowGaugeViewModelTests.cs
  modified:
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/Views/MainWindow.xaml
    - tests/Chronos.Tests/MainViewModelTests.cs

key-decisions:
  - "ToggleOAuthUsage réutilise le pattern GAP-1 de CalibrateBudgets (Load frais avant Save) → n'écrase pas un coin/écran persisté par l'OverlayController entre-temps"
  - "INT-02 prouvé au niveau VM (WindowGaugeViewModel), pas par un nouveau binding : Apply() met déjà IsEstimated = false en Exact → aucune modif production nécessaire côté badge"
  - "ctor MainViewModel volontairement inchangé (collaborateurs déjà injectés) → zéro impact CadranBindingTests / OverlayWindowConfigTests / CompositionRootTests (leçon phase 9)"

patterns-established:
  - "Item de menu cochable relié à [ObservableProperty] bool (IsChecked) + [RelayCommand] (Command), état initialisé dans le ctor depuis _settings"

requirements-completed: [INT-02, INT-03]

# Metrics
duration: 5min
completed: 2026-07-09
---

# Phase 11 Plan 02: Réglage menu « Usage exact (OAuth) » + honnêteté bidirectionnelle du cadran Summary

**Toggle contextuel « Usage exact (OAuth) » persisté (GAP-1) qui active/désactive la source exacte à chaud via RequestRefresh, et preuve test INT-02 qu'une fenêtre Exact masque le badge « estimée » en portant l'utilization réelle — l'honnêteté joue dans les deux sens.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-07-09T08:41:49Z
- **Completed:** 2026-07-09T08:46:52Z
- **Tasks:** 3 (2 auto TDD + 1 checkpoint auto-vérifié)
- **Files modified:** 4 (1 créé, 3 modifiés)

## Accomplishments
- `MainViewModel.IsOAuthUsageEnabled` initialisé depuis `_settings.OAuthUsageEnabled` (défaut true) et `ToggleOAuthUsageCommand` : bascule + `Load()` disque frais → `with { OAuthUsageEnabled }` → `Save()` → `RequestRefresh()` (application à chaud, le portillon gated relit le flag frais au prochain `GetAsync`).
- `MenuItem "Usage exact (OAuth)"` cochable ajouté au `<ContextMenu>` de `MainWindow.xaml`, lié à `IsOAuthUsageEnabled` (IsChecked) et `ToggleOAuthUsageCommand` (Command).
- INT-02 prouvé au niveau VM : `WindowGaugeViewModelTests` — fenêtre Exact → `IsEstimated == false` + `Utilization == 0.74` + `HasTokens == false` ; fenêtre Estimated → `IsEstimated == true`.
- Suite complète verte : **188/188** (183 après 11-01 + 5 nouveaux : 3 MainViewModel + 2 WindowGaugeViewModel), `ServicesLayerPurityTests` incluse, ctor VM inchangé → aucune régression DI.

## Task Commits

Each task was committed atomically (Task 1 en TDD RED → GREEN) :

1. **Task 1 (RED): tests ToggleOAuthUsage (init + persist + GAP-1)** - `308cb68` (test)
2. **Task 1 (GREEN): commande VM ToggleOAuthUsage persistée + RequestRefresh** - `508be62` (feat)
3. **Task 2: MenuItem « Usage exact (OAuth) » + test INT-02** - `b3912a8` (feat)
4. **Task 3: checkpoint human-verify — auto-vérifié (voir ci-dessous), aucun commit code**

_Task 2 n'a pas nécessité de commit RED séparé : le test INT-02 caractérise un comportement VM DÉJÀ présent (Apply met IsEstimated selon la fiabilité) — il est vert d'emblée ; la nouveauté livrée est le MenuItem XAML._

## Files Created/Modified
- `src/Chronos/ViewModels/MainViewModel.cs` - Ajout `[ObservableProperty] IsOAuthUsageEnabled`, init ctor depuis `_settings`, `[RelayCommand] ToggleOAuthUsage` (GAP-1 + RequestRefresh).
- `src/Chronos/Views/MainWindow.xaml` - MenuItem cochable « Usage exact (OAuth) » (IsChecked + Command).
- `tests/Chronos.Tests/MainViewModelTests.cs` - 3 tests toggle (init défaut true, bascule + persistance, non-écrasement GAP-1).
- `tests/Chronos.Tests/WindowGaugeViewModelTests.cs` - 2 tests INT-02 (Exact masque le badge + Utilization réelle ; Estimated rallume le badge) — créé.

## Decisions Made
- **Pattern GAP-1 réutilisé** : `ToggleOAuthUsage` relit le disque frais avant `Save` (comme `CalibrateBudgets`) → un coin/écran persisté par l'`OverlayController` entre la construction du VM et le clic n'est pas écrasé (prouvé par le 3ᵉ test).
- **INT-02 au niveau VM plutôt qu'un nouveau binding** : `WindowGaugeViewModel.Apply` positionne déjà `IsEstimated = (Reliability == Estimated)` et les badges XAML y sont liés depuis la Phase 5 ; le test caractérise ce contrat sans code production supplémentaire côté badge.
- **ctor `MainViewModel` inchangé** : les collaborateurs (`_orchestrator`, `_settingsService`, `_settings`) sont déjà des champs → aucun paramètre ajouté → zéro impact sur `CadranBindingTests` / `OverlayWindowConfigTests` / `CompositionRootTests`.

## Deviations from Plan

None - plan executed exactly as written.

_(Le seul obstacle rencontré était environnemental et non lié au code — voir « Issues Encountered ».)_

**Total deviations:** 0
**Impact on plan:** Aucun. Les 3 tâches ont suivi le plan à la lettre ; aucun changement de scope, aucun test d'origine cassé.

## Issues Encountered
- **Publication bloquée par une instance en cours (résolu).** Le premier `dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true` a échoué avec `GenerateBundle` / `UnauthorizedAccessException` sur `Chronos.exe` : une instance de l'exe (PID 48408) issue d'un run antérieur verrouillait le fichier de sortie. Résolu par `taskkill /F /IM Chronos.exe` puis republication (réussie). Blocage purement environnemental (fichier verrouillé), aucune incidence code.

## Checkpoint Task 3 (human-verify) — auto-vérifié en mode autonome

Le checkpoint final `human-verify` a été traité programmatiquement (mode autonome) au lieu de bloquer :

1. **Build + suite complète verte** : `dotnet build Chronos.sln -c Debug` → 0 avertissement / 0 erreur ; `dotnet test Chronos.sln -c Debug` → **188/188**.
2. **Item de menu présent + lié** : `MainWindow.xaml` contient `MenuItem "Usage exact (OAuth)"` (`IsCheckable="True"`) lié à `IsOAuthUsageEnabled` (IsChecked) et `ToggleOAuthUsageCommand` (Command) — vérifié par grep.
3. **Exe republié + lancé** : `dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true` réussi (mono-fichier ~76 Mo) ; processus lancé et **resté vivant 8 s sans crash**.

Les critères **purement visuels** (voir les vrais % ~74/93 sans badge, arcs colorés, bascule visuelle du toggle, persistance au redémarrage) exigent un écran réel + le vrai token/endpoint OAuth (non simulables) → consignés dans
[`11-HUMAN-UAT.md`](./11-HUMAN-UAT.md) (7 critères, statut EN ATTENTE de validation utilisateur). Aucune capture d'écran prise ici (l'orchestrateur s'en charge).

## User Setup Required
None - no external service configuration required. (L'accès au token OAuth est géré par le portillon gated du plan 11-01 ; le réglage se pilote désormais depuis le menu.)

## Next Phase Readiness
- **Milestone v1.2 fonctionnellement complet** : chaîne composite OAuth exact (11-01) + réglage on/off menu persisté + honnêteté bidirectionnelle du cadran (11-02). Dernier plan de la phase 11, dernière phase du milestone.
- **Reste** : validation humaine UAT (`11-HUMAN-UAT.md`) sur le vrai poste (vrais % sans badge, bascule/persistance du toggle) avant clôture du milestone (`/gsd:complete-milestone`).

---
*Phase: 11-int-gration-composite-r-glage*
*Completed: 2026-07-09*

## Self-Check: PASSED

- Fichiers vérifiés présents : `WindowGaugeViewModelTests.cs`, `11-02-SUMMARY.md`, `11-HUMAN-UAT.md`
- Commits vérifiés présents : `308cb68` (test RED), `508be62` (feat T1), `b3912a8` (feat T2)
