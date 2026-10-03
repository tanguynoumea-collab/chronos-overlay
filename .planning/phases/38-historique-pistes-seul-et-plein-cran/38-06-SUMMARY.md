---
phase: 38-historique-pistes-seul-et-plein-cran
plan: 06
subsystem: historique-ui
tags: [plein-ecran, wpf, dwm, multi-moniteur, honnetete]
requires:
  - phase: 38-05
    provides: PleinEcranHistorique (Echap, BornesDip), HistoriqueViewModel.EstPleinEcran / BasculerPleinEcranCommand / EchapCommand, libellés du bouton
  - phase: 38-04
    provides: les trois vues basculables par fusion du dictionnaire PleinEcran
provides:
  - fenêtre Historique câblée au plein écran (bouton, F11, Échap à deux niveaux, bornes rcMonitor en DIP, géométrie restaurée)
  - seam de test HistoriqueWindow.FournisseurBornesMoniteur, EstEnPleinEcran, EntrerPleinEcran / QuitterPleinEcran, PreferenceCoinsDwm
  - NativeMethods.DWMWCP_DONOTROUND
  - preuve d'honnêteté identique en mode normal et en plein écran (trois vues)
affects: [43]
tech-stack:
  added: []
  patterns:
    - "Dictionnaires construits AVANT toute fusion : un dictionnaire PleinEcran fusionné dans la fenêtre masque les tokens normaux pour l'indexeur"
key-files:
  created: []
  modified:
    - src/Chronos/Views/Historique/HistoriqueWindow.xaml
    - src/Chronos/Views/Historique/HistoriqueWindow.xaml.cs
    - src/Chronos/Interop/NativeMethods.cs
    - tests/Chronos.Tests/HistoriqueBindingTests.cs
    - tests/Chronos.Tests/PleinEcranVuesTests.cs
    - tests/Chronos.Tests/HonneteteHistoriqueTests.cs
    - tests/Chronos.Tests/GardeDocumentationHistoriqueTests.cs
    - README.md
key-decisions:
  - "En-tête plein écran à 124 DIP (contrat : 92 inchangé) — écart décidé par l'orchestrateur et consigné au plan 03, appliqué et testé ici"
  - "Barre des tâches couverte par la seule détection de Windows (fenêtre active au rectangle exact du moniteur) : pas de repli ITaskbarList2.MarkFullscreenWindow, constat manuel en phase 43"
  - "Pas de recalcul des bornes sur DpiChanged pendant le plein écran"
  - "Pas de Topmost (D-34-23), pas d'état maximisé : géométrie posée à la main"
requirements-completed: [HIS-10, HIS-11]
duration: 25min
completed: 2026-10-03
---

# Phase 38 Plan 06 : Fenêtre Historique au plein écran Summary

**La fenêtre Historique applique le plein écran décidé par le VM. Elle se pose sur rcMonitor converti en DIP (barre des tâches couverte, jamais maximisée) et mémorise sa géométrie d'avant pour la restaurer exactement. Redimensionnement et déplacement sont bloqués, les coins DWM deviennent carrés, et le dictionnaire PleinEcran est fusionné en dernier dans la fenêtre et dans les trois vues. Bouton « ⛶ Plein écran » visible partout, F11, Échap à deux niveaux. Un test prouve que les textes et les trous sont identiques à 920 × 610 et à 1920 × 1080.**

## Performance

- **Durée :** ~25 min
- **Terminé le :** 2026-10-03
- **Tâches :** 3
- **Fichiers modifiés :** 8

## Accomplishments

- `HistoriqueWindow.xaml` : Échap lié à `EchapCommand` (la liaison à `FermerCommand` est **remplacée**, pas doublée), F11 lié à `BasculerPleinEcranCommand`, et ✕ reste sur `FermerCommand`.
  La ligne 3 de l'en-tête passe en deux colonnes : `WrapPanel` (fraîcheur en `Wrap` + pastille) à gauche, `BoutonPleinEcran` à droite. Son libellé change par DataTrigger sur `EstPleinEcran`.
  Les 12 références `HistoCorps*` / `HistoHauteurEnTete` sont en `DynamicResource`.
- `HistoriqueWindow.xaml.cs` :
  - le constructeur remet `EstPleinEcran` à faux, puis s'abonne avec le handler nommé `SurVmPropertyChanged`. Au `Closed`, il se désabonne puis remet à faux ;
  - `EntrerPleinEcran` est idempotent et annule la bascule si aucun moniteur n'est lisible. Il passe par `RestoreBounds` quand la fenêtre n'est pas en état normal, pose la position puis la taille, met `NoResize` et `DWMWCP_DONOTROUND` ;
  - `QuitterPleinEcran` restaure taille puis position, remet `CanResize` et `DWMWCP_ROUND` ;
  - `EnregistrerGeometrie` écrit la géométrie d'avant pendant le plein écran, et `EnTete_Drag` est inerte en plein écran ;
  - `PreferenceCoinsDwm` est extraite, et `ArrondirCoinsDwm` garde sa signature.
- `NativeMethods.DWMWCP_DONOTROUND = 1`.
- Tests : 7 tests de fenêtre et un test d'honnêteté sur les trois vues. La garde « aucune StaticResource échelonnée » couvre maintenant `HistoriqueWindow.xaml`.
  Le README a une section « Plein écran », et la garde de documentation vérifie « Plein écran », « F11 » et « Échap ».

## Task Commits

1. **Task 1 :** `b34e1d7` feat(38-06): fenêtre Historique — bouton plein écran, F11 / Échap, bornes du moniteur, dictionnaire dans 4 propriétaires
2. **Task 2 (correctif) :** `c9b3f60` fix(38-06): construire les quatre dictionnaires plein écran avant toute fusion
3. **Task 2 :** `825e6eb` test(38-06): liaisons F11 / Échap / bouton, géométrie du moniteur, propagation du dictionnaire
4. **Task 3 :** `852a85c` test(38-06): honnêteté identique en plein écran sur les trois vues, README « Plein écran »

## Decisions Made

- **En-tête plein écran à 124 DIP, hors contrat** (le contrat § 4.2 garde l'en-tête à 92). C'est une décision de l'orchestrateur, consignée au plan 03.
  Elle est appliquée par `DictionnairePleinEcran` (clé `HistoHauteurEnTete`) et vérifiée par `Le_dictionnaire_plein_ecran_atteint_la_fenetre_et_les_trois_vues` (124 en plein écran, 92 après la sortie).
- **Pas de repli `ITaskbarList2.MarkFullscreenWindow`** : la barre des tâches passe sous la fenêtre grâce à la détection automatique de Windows (fenêtre active au
  rectangle exact du moniteur). Le constat est **manuel, en phase 43** : `Chronos.exe --historique`, F11 sur le primaire puis sur un secondaire, barre des
  tâches couverte, coins carrés, Échap ×2, géométrie retrouvée.
- **Pas de recalcul sur `DpiChanged`** pendant le plein écran : les bornes sont lues une fois, à l'entrée. Si l'échelle du moniteur change pendant le plein écran, il faut en sortir puis y revenir.
- Pas de `Topmost`, pas de `WindowState.Maximized`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Dictionnaires construits après la première fusion : GridLength NaN**
- **Trouvé pendant :** Task 2 (premier passage des tests)
- **Problème :** `AjouterDictionnaires` construisait chaque instance depuis `Resources` de la fenêtre, puis la fusionnait. Dès que la première instance
  était fusionnée dans la fenêtre, l'indexeur de `Resources` trouvait ses `HistoHauteur*` = NaN (le dernier fusionné gagne) au lieu des tokens normaux.
  La construction suivante levait alors `ArgumentException` (« GridLength NaN ») au premier F11.
- **Correction :** les quatre instances sont construites avant toute fusion, et un commentaire explique pourquoi.
- **Fichiers :** src/Chronos/Views/Historique/HistoriqueWindow.xaml.cs
- **Commit :** c9b3f60

**2. [Ajustement mineur] Doc XML de la classe**
- La phrase existante « pas de `<c>Topmost</c>` » a été reformulée en « jamais « toujours au premier plan » ». Le critère grep
  `WindowState.Maximized|Topmost|ITaskbarList` = 0 porte sur tout le fichier, commentaires compris. Le sens ne change pas.

Aucune exclusion dans le test d'honnêteté : l'égalité stricte des textes visibles passe sur les trois vues, sans aucun texte retiré.

## Verification

- `dotnet test Chronos.sln -c Debug`, deux fois : 1733 / 1733, 0 échec (7 de plus qu'au 38-05 : 6 tests de fenêtre ajoutés, 1 renommé et réécrit, 1 test d'honnêteté).
- `dotnet build Chronos.sln -c Release` : 0 avertissement, 0 erreur.
- Critères grep de la tâche 1 : 1 / 0 / 1 / 1 / 1 / 1 / 0. Dans le code-behind : rcMonitor 3, rcWork 0, Maximized|Topmost|ITaskbarList 0,
  `VueQuatreSemaines.Resources.MergedDictionaries` 1, `SurVmPropertyChanged` 3. `DWMWCP_DONOTROUND = 1` : 1.
- Tâche 2 : les 7 noms sont présents (1 chacun), l'ancien nom 0 fois, `FournisseurBornesMoniteur` 6 fois, `"HistoriqueWindow.xaml"` présent dans la garde.
- Tâche 3 : `### Plein écran` 1 fois, F11 et Échap présents dans le README, `"Plein écran"` 1 fois dans la garde.
- Gardes de doctrine, `ServicesLayerPurityTests` et `CompositionRootTests` sont verts sans modification. Aucun fichier de `Services/` n'a été touché.
- L'exe n'a pas été lancé (règle d'exécution). Le comportement réel sur moniteur (barre des tâches, coins, secondaire) reste à constater en phase 43.

## Known Stubs

Aucun.

## Next Phase Readiness

- HIS-10 et HIS-11 sont livrés et cochés. La phase 38 est complète côté code. Reste le constat manuel du plein écran réel en phase 43.

## Self-Check: PASSED

- FOUND : src/Chronos/Views/Historique/HistoriqueWindow.xaml, src/Chronos/Views/Historique/HistoriqueWindow.xaml.cs, src/Chronos/Interop/NativeMethods.cs, README.md
- FOUND : b34e1d7, c9b3f60, 825e6eb, 852a85c
