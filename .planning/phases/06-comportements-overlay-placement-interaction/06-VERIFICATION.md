---
phase: 06-comportements-overlay-placement-interaction
verified: 2026-07-08T00:00:00Z
status: passed
score: 19/19 must-haves verified (4 plans, 106/106 tests verts)
---

# Phase 6 : Comportements overlay (placement + interaction) — Rapport de vérification

**Objectif de phase :** L'utilisateur peut placer, ranger, régler et faire persister l'overlay entièrement via ses interactions, sur tous ses moniteurs.
**Vérifié le :** 2026-07-08
**Statut :** passed
**Re-vérification :** Non — vérification initiale (aucun `06-VERIFICATION.md` préexistant)

## Vérifications automatisées globales

| Commande | Résultat |
|---|---|
| `dotnet build Chronos.sln -c Debug` | 0 erreur, 0 avertissement |
| `dotnet test Chronos.sln -c Debug` | **106/106 tests verts** (conforme à la valeur attendue) |
| Sous-ensemble ciblé (CornerSnap/SettingsService/WeeklyRecalibration/TopmostGuard/AutostartService/OverlayController/ServicesLayerPurityTests/CompositionRootTests/MainViewModelTests) | 45/45 verts |
| Historique git | Tous les commits cités dans les 4 SUMMARY présents (`817b04a` … `3f56839`) |

## Goal Achievement

### Observable Truths

| # | Truth | Statut | Preuve |
|---|---|---|---|
| 1 | `CornerSnap.NearestCorner` renvoie le coin le plus proche (4 quadrants) en respectant la marge | ✓ VERIFIED | `src/Chronos/Placement/CornerSnap.cs:19-26` — logique conforme au plan, 13 tests `CornerSnapTests` verts |
| 2 | `CornerSnap.CornerToTopLeft` place la fenêtre à un coin IMPOSÉ (restauration) | ✓ VERIFIED | `CornerSnap.cs:50-57`, consommé par `OverlayController.RestorePlacement` |
| 3 | `SettingsService` écrit `settings.json` atomiquement et le relit en round-trip | ✓ VERIFIED | `SettingsService.cs:59-70` (temp + `File.Move(overwrite:true)`), `Load()` round-trip testé |
| 4 | `settings.json` corrompu/absent → défauts sans exception | ✓ VERIFIED | `SettingsService.cs:40-53` — try/catch `IOException/JsonException/ArgumentException` → `new ChronosSettings()` |
| 5 | `WeeklyRecalibration` laisse Exact+ResetsAt inchangé, ne synthétise qu'au repli avec ancre | ✓ VERIFIED | `WeeklyRecalibration.cs:28-40` — garde `Reliability==Exact` respectée exactement comme au plan |
| 6 | Fenêtre recalibrée reste `Estimated` (badge « estimée » conservé) | ✓ VERIFIED | `WeeklyRecalibration.cs:39` — `weekly with { ResetsAt = next }` (Reliability non touchée) |
| 7 | `TopmostGuard.Suspend()` arrête la réaffirmation ; `Resume()` la reprend + réaffirme immédiatement | ✓ VERIFIED | `TopmostGuard.cs:45-48` |
| 8 | `NativeMethods` expose HWND_BOTTOM/GetWindowRect/MonitorFromWindow/GetMonitorInfo(rcWork+szDevice)/GetDpiForMonitor/EnumDisplayMonitors | ✓ VERIFIED | `NativeMethods.cs` — tous les symboles présents et utilisés par `OverlayController` |
| 9 | `AutostartService.Enable()` crée un `.lnk` shell:startup ciblant `Environment.ProcessPath` ; `Disable()`/`IsEnabled()` cohérents | ✓ VERIFIED | `AutostartService.cs:25-47` — `Environment.ProcessPath` (jamais `Assembly.Location`), dossier injectable |
| 10 | Glisser (clic gauche) déplace la fenêtre ; au relâchement, accroche au coin le plus proche de la WorkingArea du moniteur courant | ✓ VERIFIED (code) / tracké UAT | `MainWindow.xaml.cs:49-54` — `DragMove()` puis `_controller.SnapToNearestCorner()` au retour ; interaction visuelle réelle trackée dans `06-HUMAN-UAT.md` #1 |
| 11 | Placement en pixels PHYSIQUES via `SetWindowPos` (jamais `Window.Left/Top`) | ✓ VERIFIED | `OverlayController.cs` — aucune écriture sur `Window.Left`/`Window.Top` pour le positionnement ; tout passe par `_setWindowPos(...)` sur `GetWindowRect`/`rcWork` physiques |
| 12 | `SendToBackground` pose HWND_BOTTOM+SWP_NOACTIVATE et suspend le guard ; `BringToForeground` remet Topmost + Resume | ✓ VERIFIED | `OverlayController.cs:176-192` |
| 13 | Au lancement, restauration AVANT le premier rendu au coin+device persistés, repli primaire si device disparu | ✓ VERIFIED | `App.xaml.cs:31-36` (`ApplyRestoredState` avant `Show()`) + `MainWindow.xaml.cs:25-30` (`SourceInitialized` → `RestorePlacement`) + `OverlayController.cs:106-163` (repli primaire) |
| 14 | Changement de config d'écrans (WM_DISPLAYCHANGE) re-clampe la fenêtre | ✓ VERIFIED | `OverlayController.cs:168-172` (`WndProc` → `ReclampToValidMonitor` → `SnapToNearestCorner`) |
| 15 | Clic droit ouvre un menu à 4 items (Arrière-plan/Recalibrer/Lancer au démarrage/Quitter) | ✓ VERIFIED | `MainWindow.xaml:21-34` — 4 `MenuItem` bindés aux 4 `[RelayCommand]` |
| 16 | Quitter ferme l'application | ✓ VERIFIED | `MainViewModel.cs:134-135` → `IWindowController.Quit()` → `Application.Current.Shutdown()` |
| 17 | `ToggleBackground` bascule via `IWindowController` et persiste l'état ; retour premier plan réaffirme le topmost | ✓ VERIFIED | `MainViewModel.cs:100-107` + `OverlayController.SendToBackground/BringToForeground` persistant `Background` |
| 18 | Lancer au démarrage crée/supprime le `.lnk` et l'item reflète l'état réel | ✓ VERIFIED | `MainViewModel.cs:109-116` (`ToggleAutostart` → `IsAutostart = _autostart.IsEnabled()`) |
| 19 | Recalibrer applique une ancre hebdo au repli uniquement ; l'ancre est persistée ; l'arc reste « estimée » | ✓ VERIFIED | `MainViewModel.cs:118-131` + `ApplySnapshot` (ligne 72) appliquant `WeeklyRecalibration.Apply` avant `SevenDay.Apply` |

**Score :** 19/19 truths vérifiées par le code (10 des 19 s'appuient en plus sur une confirmation visuelle réelle déjà trackée dans `06-HUMAN-UAT.md`, non re-déclenchée ici).

### Required Artifacts

| Artifact | Attendu | Statut | Détails |
|---|---|---|---|
| `src/Chronos/Placement/CornerSnap.cs` | Fonctions pures NearestCorner/ClassifyCorner/CornerToTopLeft | ✓ VERIFIED | Présent, aucun `using System.Windows`, 3 fonctions conformes au plan |
| `src/Chronos/Placement/RectD.cs`, `OverlayCorner.cs` | Types neutres support | ✓ VERIFIED | Présents |
| `src/Chronos/Services/SettingsService.cs` | Load tolérant + Save atomique | ✓ VERIFIED | `Load()`/`Save()` conformes, type neutre |
| `src/Chronos/Services/ChronosSettings.cs` | Schéma settings.json (coin+device=vérité) | ✓ VERIFIED | Record avec Corner/MonitorDeviceName/X/Y/Background/RefreshIntervalSeconds/WeeklyAnchor |
| `src/Chronos/Services/WeeklyRecalibration.cs` | Recalibrage hebdo pur | ✓ VERIFIED | Classe statique pure, garde Exact respectée |
| `src/Chronos/Interop/NativeMethods.cs` | P/Invoke moniteur/DPI/fenêtre/arrière-plan | ✓ VERIFIED | HWND_BOTTOM, MonitorFromWindow, GetMonitorInfo(MONITORINFOEX rcWork/szDevice), GetWindowRect, GetDpiForMonitor, EnumDisplayMonitors tous présents |
| `src/Chronos/Services/TopmostGuard.cs` | Suspend/Resume | ✓ VERIFIED | Exporte `Suspend()`/`Resume()` |
| `src/Chronos/Services/AutostartService.cs` | .lnk shell:startup sans dépendance native | ✓ VERIFIED | `WScript.Shell` COM late-bound, dossier injectable |
| `src/Chronos/Services/OverlayController.cs` | Adaptateur WPF placement physique + arrière-plan + restauration | ✓ VERIFIED | `MonitorFromWindow` utilisé, implémente `IWindowController`, seul point d'allow-list de pureté ajouté |
| `src/Chronos/Services/IWindowController.cs` | Contrat neutre | ✓ VERIFIED | Aucun type WPF en signature |
| `src/Chronos/Views/MainWindow.xaml.cs` | DragMove + snap + hook + restauration | ✓ VERIFIED | `DragMove()`, `SnapToNearestCorner()`, `DpiChanged`, `SourceInitialized` |
| `src/Chronos/ViewModels/MainViewModel.cs` | 4 [RelayCommand] + recalibrage dans ApplySnapshot | ✓ VERIFIED | `ToggleBackground`/`ToggleAutostart`/`Recalibrate`/`Quit` + `WeeklyRecalibration.Apply` dans `ApplySnapshot` |
| `src/Chronos/Views/MainWindow.xaml` | ContextMenu à 4 items | ✓ VERIFIED | `Grid.ContextMenu` avec 4 `MenuItem` bindés |
| `src/Chronos/Views/RecalibrationDialog.xaml` | DatePicker + « caler sur maintenant » | ✓ VERIFIED | `DatePicker` + bouton « Caler sur maintenant » présents |

Tous les artefacts requis par les 4 plans (`06-01` à `06-04`) existent, sont substantiels (implémentation complète, pas de stub) et sont câblés (wiring niveau 3 confirmé par lecture directe du code + tests).

### Key Link Verification

| From | To | Via | Statut | Détails |
|---|---|---|---|---|
| `SettingsService` | `ChronosPaths.SettingsFile` | chemin injecté `%APPDATA%/Chronos/settings.json` | ✓ WIRED | `SettingsService.cs` utilise `_paths.SettingsFile` partout |
| `WeeklyRecalibration.Apply` | `WindowState.Reliability` | garde Exact → inchangé, sinon Estimated conservé | ✓ WIRED | `WeeklyRecalibration.cs:31-39` |
| `TopmostGuard.Suspend` | `DispatcherTimer.Stop` | arrêt réaffirmation arrière-plan | ✓ WIRED | `TopmostGuard.cs:45` |
| `AutostartService.Enable` | `Environment.ProcessPath` | cible du raccourci (single-file-safe) | ✓ WIRED | `AutostartService.cs:37` |
| `MainWindow.xaml.cs (MouseLeftButtonDown)` | `OverlayController.SnapToNearestCorner` | snap juste après retour de DragMove | ✓ WIRED | `MainWindow.xaml.cs:49-54` — aucun handler MouseUp, exactement au retour de `DragMove()` |
| `App.xaml.cs OnStartup` | `MainWindow.ApplyRestoredState` | chargement settings avant Show | ✓ WIRED | `App.xaml.cs:31-36` |
| `OverlayController` | `TopmostGuard.Suspend/Resume` | mode arrière-plan | ✓ WIRED | `OverlayController.cs:180,190` |
| `MainWindow.xaml ContextMenu` | `MainViewModel [RelayCommand]` | `MenuItem.Command` bindés | ✓ WIRED | `MainWindow.xaml:23-32` — `Command="{Binding ...Command}"` × 4 |
| `MainViewModel.ApplySnapshot` | `WeeklyRecalibration.Apply` | recalibrage avant SevenDay.Apply | ✓ WIRED | `MainViewModel.cs:72-75` |
| `MainViewModel.ToggleAutostartCommand` | `IAutostartService` | Enable/Disable + IsEnabled reflété | ✓ WIRED | `MainViewModel.cs:111-116` |

Tous les key links déclarés dans les 4 PLAN sont câblés — aucune fuite « composant existe mais pas connecté » détectée.

### Data-Flow Trace (Level 4)

| Artifact | Variable | Source | Données réelles | Statut |
|---|---|---|---|---|
| `MainWindow.xaml ContextMenu.IsChecked` (Arrière-plan) | `IsBackground` | `MainViewModel` initialisé depuis `settings.Background` (fichier réel), mis à jour par `ToggleBackground` | Oui | ✓ FLOWING |
| `MainWindow.xaml ContextMenu.IsChecked` (Lancer au démarrage) | `IsAutostart` | `_autostart.IsEnabled()` — interroge réellement le `.lnk` sur disque via `AutostartService` (enregistré en Singleton réel dans `App.xaml.cs`, pas un fake) | Oui | ✓ FLOWING |
| Restauration au lancement | `ChronosSettings` | `SettingsService.Load()` (lecture réelle de `%APPDATA%/Chronos/settings.json` via DI singleton) | Oui | ✓ FLOWING |
| Recalibrage hebdo | `SevenDay.ResetsAt` | `WeeklyRecalibration.Apply(snap.SevenDay, _settings.WeeklyAnchor, _clock.UtcNow)` — dépend du snapshot réel du pipeline (Phase 3/4), pas de valeur statique | Oui | ✓ FLOWING |

Aucune donnée figée/hardcodée détectée sur le chemin menu → services → persistance.

### Behavioral Spot-Checks

| Comportement | Commande | Résultat | Statut |
|---|---|---|---|
| Build complet | `dotnet build Chronos.sln -c Debug` | 0 erreur | ✓ PASS |
| Suite de tests complète | `dotnet test Chronos.sln -c Debug` | 106/106 verts | ✓ PASS |
| Sous-ensemble ciblé Phase 6 | `dotnet test --filter "CornerSnap\|SettingsService\|WeeklyRecalibration\|TopmostGuard\|AutostartService\|OverlayController\|ServicesLayerPurityTests\|CompositionRootTests\|MainViewModelTests"` | 45/45 verts | ✓ PASS |
| Interactions physiques réelles (drag, multi-écrans, arrière-plan visuel, reboot autostart) | — | — | ? SKIP — nécessite écran/interaction réelle, déjà tracké dans `06-HUMAN-UAT.md` (10 items), non ré-exécuté ici |

### Requirements Coverage

| Requirement | Plan source | Description | Statut | Preuve |
|---|---|---|---|---|
| FEN-02 | 06-03 | Déplacement par glisser (DragMove) | ✓ SATISFIED | `MainWindow.xaml.cs:49-54` |
| FEN-03 | 06-01, 06-03 | Accroche au coin d'écran le plus proche (WorkingArea, pas Bounds) | ✓ SATISFIED | `CornerSnap.cs` + `OverlayController.SnapToNearestCorner` (utilise `rcWork`) |
| FEN-04 | 06-03 | Multi-écrans (Per-Monitor, repli si écran disparu) | ✓ SATISFIED | `OverlayController.RestorePlacement` (EnumDisplayMonitors + repli primaire) |
| FEN-05 | 06-02, 06-03, 06-04 | Toggle arrière-plan / premier plan | ✓ SATISFIED | `TopmostGuard.Suspend/Resume` + `OverlayController.SendToBackground/BringToForeground` + `MainViewModel.ToggleBackground` |
| FEN-06 | 06-04 | Menu contextuel clic droit, seul point d'accès/sortie | ✓ SATISFIED | `MainWindow.xaml` ContextMenu 4 items + `Quit()` |
| FEN-07 | 06-01, 06-03, 06-04 | Persistance settings.json + restauration au lancement | ✓ SATISFIED | `SettingsService` + `App.xaml.cs` restauration avant Show |
| ROB-03 | 06-01, 06-04 | Recalibrage hebdo best-effort, recalibrable | ✓ SATISFIED | `WeeklyRecalibration` + `MainViewModel.Recalibrate` + dialogue |
| DEP-02 | 06-02, 06-04 | Autostart shell:startup | ✓ SATISFIED | `AutostartService` + `MainViewModel.ToggleAutostart` |

Aucune exigence orpheline : les 8 IDs déclarés dans les frontmatters des 4 PLAN correspondent exactement aux 8 IDs mappés à la Phase 6 dans `REQUIREMENTS.md` (toutes déjà marquées `[x]`/`Complete`).

### Anti-Patterns Found

Aucun `TODO`/`FIXME`/`PLACEHOLDER`/`not yet implemented` détecté dans `src/Chronos`. Aucune implémentation vide (`return null`/`=> {}`) sur les chemins de menu/placement/persistance. Aucune valeur hardcodée vide flottant jusqu'au rendu.

Point de vigilance mineur (non bloquant, non-gap) : le libellé de Success Criterion #3 dans `ROADMAP.md` mentionne encore « Réglages, Arrière-plan, Recalibrer et Quitter », phrasé antérieur au verrouillage de décision du `06-CONTEXT.md`/`06-04-PLAN.md` qui a remplacé « Réglages » par « Lancer au démarrage » (DEP-02). L'implémentation suit fidèlement la décision verrouillée (4 items : Arrière-plan / Recalibrer / Lancer au démarrage / Quitter), cohérente avec `REQUIREMENTS.md` FEN-06 et le prompt de vérification fourni. Documentation à rafraîchir dans le ROADMAP si souhaité, sans impact sur le goal de phase.

### Human Verification Required

Aucun nouvel item déclenché : les 10 critères d'interaction réelle (drag/snap 4 coins, multi-écrans DPI mixte, menu visuel, arrière-plan visuel, round-trip persistance, recalibrage via dialogue, autostart .lnk + reboot optionnel) sont déjà trackés dans `06-HUMAN-UAT.md` et restent en attente de validation utilisateur sur écran réel — non re-déclenchés par cette vérification automatisée.

### Gaps Summary

Aucun gap détecté. Tous les must-haves des 4 plans (06-01 à 06-04) sont vérifiés au niveau code (existence, substance, câblage, flux de données réel) ; build et suite de tests complets verts (106/106) ; couverture des 8 exigences confirmée sans orpheline ; placement physique via `SetWindowPos` confirmé (aucun usage de `Window.Left/Top` pour le positionnement) ; garde Exact du recalibrage confirmée ; Suspend/Resume du TopmostGuard confirmés ; restauration en `SourceInitialized` confirmée. Le seul reliquat est la validation humaine sur écran réel des 10 interactions physiques, déjà tracké séparément dans `06-HUMAN-UAT.md` et non bloquant pour ce rapport.

---

*Vérifié le : 2026-07-08*
*Vérificateur : Claude (gsd-verifier)*
