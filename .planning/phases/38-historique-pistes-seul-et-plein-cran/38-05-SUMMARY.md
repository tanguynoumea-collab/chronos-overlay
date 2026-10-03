---
phase: 38-historique-pistes-seul-et-plein-cran
plan: 05
subsystem: historique-ui
tags: [plein-ecran, mvvm, fonctions-pures, dpi]
requires:
  - phase: 38-04
    provides: les trois vues basculables par fusion du dictionnaire PleinEcran
provides:
  - PleinEcranHistorique.Echap(bool) → ActionEchap (QuitterPleinEcran / Fermer), pure
  - PleinEcranHistorique.BornesDip(gauche, haut, droite, bas, m11, m22) → RectangleEcran en DIP, pure
  - HistoriqueViewModel.EstPleinEcran (observable, jamais persisté), BasculerPleinEcranCommand, EchapCommand
  - TextesHistorique.BoutonPleinEcran / BoutonQuitterPleinEcran
affects: [38-06]
tech-stack:
  added: []
  patterns:
    - "Décision de fenêtre en fonction pure dans Placement/ (motif PlacementHistorique), le VM l'appelle, la fenêtre n'observe que l'état"
key-files:
  created:
    - src/Chronos/Placement/PleinEcranHistorique.cs
    - tests/Chronos.Tests/PleinEcranHistoriqueTests.cs
  modified:
    - src/Chronos/ViewModels/Historique/HistoriqueViewModel.cs
    - src/Chronos/Text/TextesHistorique.cs
    - tests/Chronos.Tests/HistoriqueViewModelTests.cs
    - tests/Chronos.Tests/TextesHistoriqueTests.cs
key-decisions:
  - "FermerCommand inchangée : le bouton ✕ ferme toujours directement, seul Échap passe par la décision à deux niveaux"
requirements-completed: []
duration: 10min
completed: 2026-10-03
---

# Phase 38 Plan 05 : Logique du plein écran (Échap à deux niveaux, bornes en DIP, état du VM) Summary

**Échap à deux niveaux et conversion des bornes physiques de moniteur en DIP en fonctions pures sans WPF (`Placement/PleinEcranHistorique.cs`). Le VM expose `EstPleinEcran`, jamais persisté, avec `BasculerPleinEcranCommand` (F11 / bouton) et `EchapCommand`. Les deux libellés du bouton sont dans `TextesHistorique`.**

## Performance

- **Durée :** ~10 min
- **Terminé le :** 2026-10-03
- **Tâches :** 2 (TDD : RED puis GREEN pour chacune)
- **Fichiers :** 2 créés, 4 modifiés

## Accomplishments

- `PleinEcranHistorique` : `Echap(true)` = `QuitterPleinEcran`, `Echap(false)` = `Fermer`. `BornesDip` applique m11 / m22 à rcMonitor. Testé à 100 %,
  125 %, 150 %, avec une origine négative et avec un secondaire à droite à 125 %. Une garde vérifie qu'aucun « System.Windows » n'apparaît dans le fichier.
- `HistoriqueViewModel` : nouvelle région « Plein écran ». Échap en plein écran remet `EstPleinEcran` à faux sans lever `FermetureDemandee` ;
  hors plein écran, il appelle `Fermer()`. Basculer ne touche pas `Reglages.Courant`.
- `TextesHistorique` : `BoutonPleinEcran = "⛶ Plein écran"`, `BoutonQuitterPleinEcran = "⤢ Quitter le plein écran · Échap"`. Ils passent
  la garde existante « aucune projection ».

## Task Commits

1. **Task 1 RED :** `672c41f` test(38-05): tests purs de l'Échap à deux niveaux et des bornes de moniteur en DIP
2. **Task 1 GREEN :** `636c305` feat(38-05): décisions pures du plein écran — Échap à deux niveaux et bornes en DIP
3. **Task 2 RED :** `c52da91` test(38-05): tests de l'état plein écran du VM, de l'Échap à deux niveaux et des libellés
4. **Task 2 GREEN :** `fd8c3e0` feat(38-05): état plein écran du VM, F11 et Échap à deux niveaux, libellés du bouton

## Deviations from Plan

Aucune. Le plan a été exécuté tel qu'écrit.

## Verification

- `dotnet test Chronos.sln -c Debug`, deux fois : 1726 / 1726, 0 échec (11 tests de plus qu'au 38-04).
- `dotnet build Chronos.sln -c Release` : 0 avertissement, 0 erreur.
- Critères grep : 1 / 1 / 1 / 1 / 1 pour le VM et les textes. `EstPleinEcran` apparaît 0 fois dans `ChronosSettings.cs`, et 0 fois « System.Windows » dans
  `PleinEcranHistorique.cs`. Aucun fichier de `Services/` n'a été touché.

## Known Stubs

Aucun. Rien n'est encore branché dans la fenêtre : F11, le bouton, Échap, l'application de `BornesDip` à la géométrie et la fusion du dictionnaire
seront faits en 38-06.

## Next Phase Readiness

- 38-06 : la fenêtre observe `EstPleinEcran`. Elle lit rcMonitor (MonitorFromWindow / GetMonitorInfo) et `TransformFromDevice`, passe le tout à
  `BornesDip`, puis fusionne `DictionnairePleinEcran` en dernier dans chaque vue. Les touches F11 et Échap sont liées à `BasculerPleinEcranCommand` /
  `EchapCommand`. HIS-10 et HIS-11 seront cochés en 38-06.

## Self-Check: PASSED

- FOUND : src/Chronos/Placement/PleinEcranHistorique.cs, tests/Chronos.Tests/PleinEcranHistoriqueTests.cs
- FOUND : 672c41f, 636c305, c52da91, fd8c3e0
