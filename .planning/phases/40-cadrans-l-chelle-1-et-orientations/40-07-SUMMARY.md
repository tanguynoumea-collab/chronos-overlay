---
phase: 40-cadrans-l-chelle-1-et-orientations
plan: 07
subsystem: placement overlay / fenêtre
tags: [cadran, placement, coin-accroche, dpi, tdd, wpf, cad-02]
requires: [40-06, 40-02]
provides:
  - "OverlayController.CoinCourant : coin d'accroche mémorisé par RestorePlacement et SnapToNearestCorner"
  - "OverlayController.RecalerSurCoinCourant() : pose sur le coin courant (repli : coin persisté), pixels physiques, bornée par CornerSnap.RecalerSurCoin, SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER, sans persistance"
  - "OverlayController.DerniereEmpreinteRestauree / RestaurationSansTaille : la taille réellement utilisée au démarrage et un filet de sécurité"
  - "NativeMethods.SWP_NOZORDER"
  - "MainWindow : SizeChanged -> recalage, ignoré à la première mise en page (sauf restauration sans taille) et pendant DragMove"
affects: [40-08]
tech-stack:
  added: []
  patterns: ["recalage sur coin imposé après redimensionnement, sans z-order ni persistance", "DataContext posé avant InitializeComponent pour que les liaisons de la Window soient résolues avant SourceInitialized"]
key-files:
  created: []
  modified:
    - src/Chronos/Interop/NativeMethods.cs
    - src/Chronos/Services/OverlayController.cs
    - src/Chronos/Views/MainWindow.xaml.cs
    - tests/Chronos.Tests/OverlayControllerTests.cs
    - tests/Chronos.Tests/GardeGestesCadranTests.cs
    - tests/Chronos.Tests/CadranBindingTests.cs
decisions:
  - "DataContext est posé AVANT InitializeComponent dans MainWindow. Posé après, la liaison Width/Height n'était pas encore résolue à SourceInitialized, et RestorePlacement voyait (0, 0)."
  - "Au démarrage, le test de l'empreinte accepte ±1 DIP : WPF recale Width sur la taille entière du HWND (à 125 %, 190 DIP = 237,5 px -> 238 px -> 190,4 DIP)."
  - "Les drapeaux des deux SetWindowPos existants (Snap et Restore) sont inchangés, comme le plan l'impose."
metrics:
  duration: "~10 min"
  completed: 2026-10-03
  tasks: 2
  files: 6
---

# Phase 40 Plan 07 : la fenêtre reste collée à son coin quand l'empreinte change — Summary

Quand on change de style ou d'orientation, WPF redimensionne le HWND en gardant fixe son coin haut-gauche. `MainWindow` capte ce `SizeChanged` et appelle `OverlayController.RecalerSurCoinCourant()`, qui repose la fenêtre sur son coin d'accroche courant. Ce coin est celui mémorisé par `RestorePlacement` ou `SnapToNearestCorner`, avec repli sur le coin persisté ; il n'est jamais recalculé comme le coin le plus proche.

Le recalage :
- travaille en pixels physiques ;
- reste borné à la zone de travail du moniteur grâce à `CornerSnap.RecalerSurCoin` ;
- ne touche pas au z-order (`SWP_NOZORDER`) et n'active pas la fenêtre ;
- ne persiste rien.

Il est ignoré à la première mise en page et pendant un `DragMove`, protégé par `_enDeplacement` dans un `try/finally`.

Au démarrage, `RestorePlacement` lit maintenant la largeur liée (`Width`) et non plus `ActualWidth`. Il pose donc Fusible H à 190 × 92 au bon coin.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | RED : tests du contrôleur, test de démarrage, gardes textuelles | aa735a2 |
| 2 | GREEN : SWP_NOZORDER, coin courant, RecalerSurCoinCourant, SizeChanged gardé | 2e72832 |

## Vérification

- RED : le build des tests échouait seulement sur `SWP_NOZORDER`, `CoinCourant`, `RecalerSurCoinCourant`, `DerniereEmpreinteRestauree` et `RestaurationSansTaille`.
- Filtre `OverlayController|GardeGestesCadran|CornerSnap|CadranBindingTests|ServicesLayerPurity|TopmostGuard` : 70/70.
- `dotnet build Chronos.sln` : 0 avertissement, 0 erreur.
- `dotnet test` : 1860/1860.
- Critères grep, tous conformes :
  - `SWP_NOZORDER   = 0x0004` = 1, `public void RecalerSurCoinCourant()` = 1 ;
  - `_coinCourant = corner;|_coinCourant = s.Corner;` = 2, `double.IsNaN(_window.Width)` = 1 ;
  - `DerniereEmpreinteRestauree|RestaurationSansTaille` = 4 dans le contrôleur et 1 dans la vue ;
  - `SizeChanged +=` = 1, `_enDeplacement = true` = 1, `.Left = |.Top = ` dans le contrôleur = 0.

## Écarts par rapport au plan

**1. [Rule 1 - Bug] Liaison Width/Height non résolue à SourceInitialized**
- **Trouvé pendant :** tâche 2 (GREEN).
- **Problème :** le plan supposait que la liaison serait résolue avant `SourceInitialized`, puisque `DataContext` est posé juste après `InitializeComponent()`. Le test de démarrage a montré le contraire : `DerniereEmpreinteRestauree` valait (0, 0). Au démarrage, la fenêtre aurait donc été posée sans taille, et seul le filet `RestaurationSansTaille` l'aurait rattrapée, après un premier placement faux.
- **Correctif :** `DataContext = viewModel;` est déplacé avant `InitializeComponent()`. La liaison se résout dès le chargement du XAML. Le filet de la première mise en page reste en place.
- **Fichier :** src/Chronos/Views/MainWindow.xaml.cs (2e72832).

**2. [Rule 1 - Bug de test] Égalité stricte (190, 92) incompatible avec l'arrondi DPI**
- **Problème :** à 125 % (machine de test), WPF ramène `Width` à la taille entière du HWND, soit 190,4 DIP.
- **Correctif :** le test accepte ±1 DIP. Il prouve toujours que la taille utilisée n'est ni 0 ni 170.
- **Fichier :** tests/Chronos.Tests/CadranBindingTests.cs (2e72832).

## Défaut connu, hors périmètre

`SnapToNearestCorner` et `RestorePlacement` appellent `SetWindowPos` avec `hWndInsertAfter = IntPtr.Zero` (HWND_TOP) sans `SWP_NOZORDER`. Ils peuvent donc remonter brièvement une fenêtre en mode arrière-plan, jusqu'à ce que `SendToBackground` la renvoie au fond. Le plan interdit de modifier ces drapeaux ; seul le nouveau recalage utilise `SWP_NOZORDER`.

## Vérification manuelle restante

Hors agent, l'exe n'a jamais été lancé : tester les 8 variantes × 4 coins, à 100 % et à 150 % (voir 40-VALIDATION.md).

## Exigences

CAD-02 n'est pas cochée. Ce plan en livre la partie « coin d'accroche tenu ». L'aperçu à l'empreinte réelle sera livré par 40-08, qui terminera CAD-02.

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND : src/Chronos/Services/OverlayController.cs, src/Chronos/Views/MainWindow.xaml.cs, src/Chronos/Interop/NativeMethods.cs
- FOUND : commits aa735a2, 2e72832
