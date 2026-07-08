---
phase: 06-comportements-overlay-placement-interaction
plan: 04
subsystem: ui
tags: [wpf, mvvm, contextmenu, relaycommand, recalibration, autostart, persistence, dialog, di]

# Dependency graph
requires:
  - phase: 06-comportements-overlay-placement-interaction
    provides: "06-01 WeeklyRecalibration.Apply (fonction pure repli hebdo) + SettingsService/ChronosSettings.WeeklyAnchor"
  - phase: 06-comportements-overlay-placement-interaction
    provides: "06-02 IAutostartService (Enable/Disable/IsEnabled, .lnk shell:startup)"
  - phase: 06-comportements-overlay-placement-interaction
    provides: "06-03 IWindowController (SendToBackground/BringToForeground/Quit) + OverlayController + restauration"
provides:
  - "MainViewModel : 4 [RelayCommand] du menu (ToggleBackground/Recalibrate/ToggleAutostart/Quit) + recalibrage hebdo dans ApplySnapshot"
  - "ContextMenu WPF à 4 items sur la Grid racine (SEUL point d'accès/sortie de l'overlay, FEN-06)"
  - "IRecalibrationPrompt (contrat neutre) + RecalibrationPrompt/RecalibrationDialog (dialogue minimal DatePicker + « caler sur maintenant »)"
  - "DI final : IAutostartService + IRecalibrationPrompt câblés ; Application.MainWindow défini avant Show (centrage dialogue)"
affects: [publication exe self-contained + autostart (phase suivante), UAT de clôture Phase 6]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ContextMenu attaché à la Grid racine (DataContext = VM hérité) → MenuItem.Command bindés sans PlacementTarget (Pattern 4 / Pitfall 5)"
    - "Recalibrage best-effort dans le pipeline : WeeklyRecalibration.Apply AVANT SevenDay.Apply, ré-application du dernier snapshot mémorisé après saisie de l'ancre"
    - "Dialogue modal neutre : VM expose event CloseRequested(bool), code-behind traduit en DialogResult (aucune logique métier en code-behind)"
    - "Prompt WPF en Chronos.Views (hors scan de pureté Services) → aucune entrée d'allow-list requise pour ouvrir une fenêtre"

key-files:
  created:
    - src/Chronos/Services/IRecalibrationPrompt.cs
    - src/Chronos/ViewModels/RecalibrationViewModel.cs
    - src/Chronos/Views/RecalibrationDialog.xaml
    - src/Chronos/Views/RecalibrationDialog.xaml.cs
    - src/Chronos/Views/RecalibrationPrompt.cs
    - tests/Chronos.Tests/Fakes/FakeWindowController.cs
    - tests/Chronos.Tests/Fakes/FakeAutostartService.cs
    - tests/Chronos.Tests/Fakes/FakeRecalibrationPrompt.cs
    - .planning/phases/06-comportements-overlay-placement-interaction/06-HUMAN-UAT.md
  modified:
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/Views/MainWindow.xaml
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/MainViewModelTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - tests/Chronos.Tests/CadranBindingTests.cs
    - tests/Chronos.Tests/OverlayWindowConfigTests.cs

key-decisions:
  - "SelectedDate du dialogue = DateTime (liaison directe DatePicker), conversion en DateTimeOffset dans le prompt → aucun converter"
  - "MainViewModel mémorise le dernier snapshot (_last) et le ré-applique après recalibrage → arc hebdo recalé immédiatement sans attendre le prochain refresh"
  - "IRecalibrationPrompt reste dans Chronos.Services (contrat neutre DateTimeOffset?) ; l'impl WPF vit dans Chronos.Views → garde de pureté verte sans allow-list"
  - "IAutostartService enregistré en Singleton via new AutostartService() (dossier shell:startup réel par défaut)"

patterns-established:
  - "Menu contextuel = seul point d'accès : 4 [RelayCommand] + ContextMenu sur Grid racine (DataContext hérité)"
  - "Recalibrage honnête : badge « estimée » conservé (Reliability=Estimated inchangé), aucune source exacte écrasée"

requirements-completed: [FEN-05, FEN-06, FEN-07, ROB-03, DEP-02]

# Metrics
duration: 8min
completed: 2026-07-08
---

# Phase 6 Plan 04 : Menu contextuel + recalibrage hebdo + câblage final Summary

**Menu contextuel clic droit à 4 items (Arrière-plan/Recalibrer/Lancer au démarrage/Quitter) bindé à 4 [RelayCommand], recalibrage hebdo best-effort appliqué dans le pipeline temps réel en conservant le badge « estimée », dialogue minimal DatePicker + « caler sur maintenant », DI final — 106 tests verts (99 + 7 nouveaux), zéro NuGet.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-07-08T20:08:19Z
- **Completed:** 2026-07-08T20:15:53Z
- **Tasks:** 3 (Task 1 en TDD, Task 3 = checkpoint UAT auto-vérifié)
- **Files modified:** 16 (9 créés, 7 modifiés)

## Accomplishments

- **Menu contextuel = SEUL point d'accès/sortie (FEN-06)** : `ContextMenu` WPF à 4 items sur la `Grid` racine (DataContext = VM hérité, Pattern 4) ; `Quitter` ferme l'application via `IWindowController.Quit()` (aucune barre de titre/tâches/Alt-Tab). Items « Arrière-plan » et « Lancer au démarrage » cochables reflétant l'état réel.
- **Commandes MVVM (FEN-05/06, DEP-02, ROB-03)** : 4 `[RelayCommand]` dans `MainViewModel` — `ToggleBackground` (SendToBackground/BringToForeground + flip IsBackground), `ToggleAutostart` (Enable/Disable + IsAutostart=IsEnabled), `Recalibrate` (prompt → persist WeeklyAnchor → ré-applique le dernier snapshot), `Quit`.
- **Recalibrage hebdo dans le pipeline (ROB-03)** : `WeeklyRecalibration.Apply(snap.SevenDay, WeeklyAnchor, now)` appelé AVANT `SevenDay.Apply` ; le repli hebdo est recalé MAIS reste `Estimated` → badge « estimée » conservé (honnêteté / Core Value). Une source hebdo exacte n'est jamais écrasée.
- **Dialogue de recalibrage minimal (ROB-03/FEN-07)** : `RecalibrationDialog` (Window modale sombre, `CenterOwner`, `ShowInTaskbar=False`) avec `DatePicker` + « Caler sur maintenant » + Valider/Annuler ; `RecalibrationViewModel` neutre (event `CloseRequested`) ; `RecalibrationPrompt` (Chronos.Views) implémente `IRecalibrationPrompt`.
- **DI final** : `IAutostartService` + `IRecalibrationPrompt` enregistrés ; `Application.MainWindow` défini AVANT `Show()` pour centrer le dialogue sur l'overlay.
- **Checkpoint UAT (Task 3)** : auto-vérifié pour tout l'automatisable (build, 106 tests, présence menu/commandes/DatePicker, smoke run 8 s sans crash, settings.json au bon schéma) ; les critères exigeant un écran réel (drag/snap 4 coins, multi-écrans DPI mixte, menu visuel, round-trip persistance, recalibrage via dialogue, autostart .lnk + reboot) persistés dans `06-HUMAN-UAT.md`.

## Task Commits

Chaque tâche a été committée atomiquement :

1. **Task 1 (TDD): commandes menu + recalibrage + ContextMenu** - `01dfa75` (test RED) → `f0c3057` (feat GREEN)
2. **Task 2: dialogue de recalibrage + prompt WPF + DI final** - `f014e04` (feat)
3. **Task 3: checkpoint UAT auto-vérifié** - `1d7b34a` (docs, 06-HUMAN-UAT.md)

_TDD Task 1 : pas de commit refactor (implémentation minimale déjà propre)._

## Files Created/Modified

- `src/Chronos/Services/IRecalibrationPrompt.cs` - Contrat neutre `Ask(DateTimeOffset?) → DateTimeOffset?` (null = annulé).
- `src/Chronos/ViewModels/MainViewModel.cs` - Nouveau ctor (controller/autostart/prompt/settings), 4 [RelayCommand], recalibrage dans ApplySnapshot, IsBackground/IsAutostart.
- `src/Chronos/ViewModels/RecalibrationViewModel.cs` - VM dialogue : SelectedDate (DateTime), commandes Now/Validate/Cancel, event CloseRequested.
- `src/Chronos/Views/RecalibrationDialog.xaml(.cs)` - Fenêtre modale sombre : DatePicker + « caler sur maintenant » + Valider/Annuler ; code-behind = CloseRequested → DialogResult.
- `src/Chronos/Views/RecalibrationPrompt.cs` - Impl WPF de IRecalibrationPrompt (Chronos.Views), dialogue centré sur MainWindow.
- `src/Chronos/Views/MainWindow.xaml` - ContextMenu à 4 items sur la Grid racine ; badges « estimée » intacts.
- `src/Chronos/App.xaml.cs` - DI IAutostartService + IRecalibrationPrompt ; MainWindow défini avant Show.
- `tests/Chronos.Tests/MainViewModelTests.cs` - Nouveau ctor + 7 tests (toggles, init autostart, quit, recalibrage repli/annulation/source exacte).
- `tests/Chronos.Tests/CompositionRootTests.cs` - Enregistre IWindowController/IAutostartService/IRecalibrationPrompt pour résoudre le nouveau MainViewModel.
- `tests/Chronos.Tests/CadranBindingTests.cs`, `OverlayWindowConfigTests.cs` - Construction VM adaptée au nouveau ctor.
- `tests/Chronos.Tests/Fakes/FakeWindowController.cs`, `FakeAutostartService.cs`, `FakeRecalibrationPrompt.cs` - Fakes déterministes (compteurs/état).
- `.planning/phases/06-.../06-HUMAN-UAT.md` - Critères UAT humains persistés.

## Decisions Made

- **SelectedDate = DateTime** (pas DateTimeOffset) pour se lier directement au `DatePicker` ; conversion `new DateTimeOffset(...)` dans le prompt → aucun converter XAML.
- **_last snapshot mémorisé** dans le VM → le recalibrage recale l'arc hebdo immédiatement (ré-application) sans attendre le prochain refresh.
- **IRecalibrationPrompt neutre en Services, impl en Views** → la garde de pureté reste verte sans nouvelle entrée d'allow-list.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Mise à jour du ctor MainViewModel dans deux tests de vue hors périmètre nominal**
- **Found during:** Task 1 (GREEN)
- **Issue:** `CadranBindingTests.cs` et `OverlayWindowConfigTests.cs` construisaient `MainViewModel` avec l'ancien ctor à 3 paramètres → échec de compilation de tout le projet de test après l'extension du ctor à 7 paramètres.
- **Fix:** Injection des nouveaux fakes (`FakeWindowController`/`FakeAutostartService`/`FakeRecalibrationPrompt`) + `SettingsService(ChronosPaths.Default())` dans les deux constructions ; aucune modification de l'intention de test.
- **Files modified:** tests/Chronos.Tests/CadranBindingTests.cs, tests/Chronos.Tests/OverlayWindowConfigTests.cs
- **Verification:** `dotnet test Chronos.sln -c Debug` → 106 verts.
- **Committed in:** f0c3057 (commit Task 1 GREEN)

---

**Total deviations:** 1 auto-fixed (1 blocking).
**Impact on plan:** Conséquence mécanique directe du changement de signature du ctor (le plan cite explicitement la mise à jour de CompositionRootTests pour le nouveau ctor) ; ces deux fichiers relèvent du même changement. Aucun scope creep.

## Issues Encountered

None - les trois tâches ont suivi le plan (Task 1 en RED→GREEN sans refactor, Task 2 sans accroc, Task 3 checkpoint auto-vérifié en mode autonome).

## User Setup Required

None - aucune configuration de service externe requise.

## Next Phase Readiness

- **Phase 6 fonctionnellement complète** : placement (06-03), CornerSnap/persistance (06-01), autostart (06-02) et couche d'interaction (06-04) livrés et prouvés par 106 tests.
- **UAT humain requis** avant clôture de phase : voir `06-HUMAN-UAT.md` (10 critères sur écran réel : drag/snap 4 coins, multi-écrans DPI mixte, menu visuel, arrière-plan, round-trip persistance, recalibrage via dialogue, autostart .lnk + reboot optionnel). Tout écart → plan de clôture `--gaps`.
- **Reste au périmètre projet** : publication de l'exe self-contained mono-fichier (win-x64, PublishSingleFile) — non couverte par cette phase.

## Self-Check: PASSED

- Fichiers créés vérifiés présents sur disque (IRecalibrationPrompt.cs, RecalibrationViewModel.cs, RecalibrationDialog.xaml/.cs, RecalibrationPrompt.cs, 3 fakes, 06-HUMAN-UAT.md).
- Commits `01dfa75`, `f0c3057`, `f014e04`, `1d7b34a` présents dans l'historique git.

---
*Phase: 06-comportements-overlay-placement-interaction*
*Completed: 2026-07-08*
