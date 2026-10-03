---
phase: 40-cadrans-l-chelle-1-et-orientations
plan: 06
subsystem: viewmodel / fenêtre overlay
tags: [cadran, orientation, echelle-1, tdd, wpf, mvvm, empreinte]
requires: [40-05]
provides:
  - "MainViewModel : OrientationFusible / OrientationMaree / OrientationVolets (System.Windows.Controls.Orientation), lues des réglages"
  - "MainViewModel : ChoisirOrientationCommand(OrientationCadran), persistée avec relecture disque fraîche (GAP-1), sans effet pour Arcs/Braises"
  - "MainViewModel : EstStyleOrientable, OrientationCourante, EstOrientationHorizontale/Verticale, LargeurCadran/HauteurCadran, MotIndisponibleAuCentre"
  - "MainWindow : plus aucun Viewbox, Width/Height liés à l'empreinte, Orientation des vues liée au VM, mot centré pour les cadrans rectangulaires"
affects: [40-07, 40-08]
tech-stack:
  added: []
  patterns: ["taille de Window liée OneWay à une propriété calculée du VM", "alignements du mot par Style + DataTrigger (aucune valeur locale)"]
key-files:
  created:
    - tests/Chronos.Tests/OrientationCadranTests.cs
  modified:
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/Views/MainWindow.xaml
    - tests/Chronos.Tests/CadranBindingTests.cs
    - tests/Chronos.Tests/EmpreinteCadranTests.cs
decisions:
  - "Alias using Orientation = System.Windows.Controls.Orientation dans MainViewModel : aucun conflit, le type neutre OrientationCadran reste côté réglages."
  - "OrientationCadranTests construit son VM localement (hors STA) au lieu de MontageReglages.NouveauVm, pour maîtriser les chemins et relire les réglages depuis un second VM."
  - "Test ajouté hors plan : La_commande_ne_reecrit_pas_un_reglage_ecrit_ailleurs (GAP-1) ; le cas Arcs/Braises est une Theory sur les deux styles."
metrics:
  duration: "~6 min"
  completed: 2026-10-03
  tasks: 2
  files: 5
---

# Phase 40 Plan 06 : cadrans à l'échelle 1, fenêtre à l'empreinte, orientation par cadran — Summary

La fenêtre overlay prend désormais l'empreinte réelle du cadran visible : 170 × 170 pour Anneaux et Braises, 190 × 92 ou 110 × 190 pour Fusible, 132 × 160 ou 190 × 96 pour Marée, 190 × 66 ou 128 × 190 pour Volets. Les Viewbox ont disparu de `MainWindow.xaml`, et Fusible et Volets gagnent donc vraiment leurs +20 % à l'écran.

`MainViewModel` garde une orientation par cadran rectangulaire, lue des réglages au démarrage. `ChoisirOrientationCommand` ne modifie que celle du cadran courant et la persiste après une relecture fraîche du disque (GAP-1). Elle ne touche jamais `VerticalLayout`, qui reste propre au widget de sessions.

Le mot « indisponible » est centré dans l'empreinte de Fusible, Marée et Volets. Pour Anneaux et Braises, il reste en bas à gauche.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | RED : OrientationCadranTests | a78e467 |
| 1 | GREEN : orientation par cadran, empreinte courante, ChoisirOrientation | da760ca |
| 2 | RED : fenêtre à l'empreinte, mot et pastilles, vues enfants directes, garde texte | 69c9f00 |
| 2 | GREEN : MainWindow à l'échelle 1 | d05a2db |

## Vérification

- RED 1 : le build des tests échouait sur `OrientationFusible` et `ChoisirOrientationCommand` (CS1061). RED 2 : 4 échecs.
- Filtre `OrientationCadranTests|MainViewModelTests` : 60/60.
- Filtre `CadranBindingTests|EmpreinteCadran|GardeGestesCadran|ReglagesWindowTests|OverlayWindowConfig` : 79/79.
- `dotnet build Chronos.sln` : 0 avertissement, 0 erreur.
- `dotnet test` : 1853/1853. Les tests Arcs existants (`fenetre.Width == 170`, distances > 71,5 px) passent sans retouche.
- Critères grep, tous conformes :
  - VM : `ChoisirOrientation` = 1, `_orientationMaree = Orientation.Vertical` = 1, `EmpreinteCadran.Pour(CadranStyle, OrientationCourante)` = 2, `NotifierEmpreinte();` = 4 ;
  - XAML : `<Viewbox` = 0, Width/Height liés = 1, `Orientation="{Binding …}"` = 3, `MotIndisponibleAuCentre` = 1, `CentreHit` 66 = 1 ;
  - tests : les noms de tests attendus sont présents.

## Écarts par rapport au plan

**1. [Rule 1 - Bug de test] Notification d'orientation testée sur un vrai changement**
- **Trouvé pendant :** tâche 1 (GREEN).
- **Problème :** la boucle des 8 variantes laisse Fusible en vertical. Exécuter ensuite `Vertical` ne changeait donc rien, et aucune notification n'était levée, à juste titre.
- **Correctif :** le test vérifie l'état vertical, puis exécute `Horizontal`.
- **Fichier :** tests/Chronos.Tests/OrientationCadranTests.cs (dans da760ca).

**2. Commentaire XAML reformulé**
- Le commentaire EXA-03 citait `MotIndisponibleAuCentre`, ce qui faisait monter le compte grep à 2 alors que le critère exige 1. Il dit maintenant « déclencheur ci-dessous ».

L'affectation du `DataContext` dans `MainWindow.xaml.cs` n'a pas été modifiée (dépendance avec 40-07).

## Connu à ce stade (plan 07)

Quand on change de style ou d'orientation, la fenêtre grandit vers la droite et vers le bas, sans se recaler sur son coin d'accroche. Ce recalage relève de CAD-02.

## Exigences

- CAD-01 cochée : échelle 1, fenêtre à l'empreinte, empreintes en tokens et fonction pure testée.
- CAD-03 cochée : les variantes verticale et horizontale sont affichées à l'échelle 1, avec leurs états.
- CAD-04 non cochée : la carte « Orientation » des réglages et la galerie restent à faire au plan 08.

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND : tests/Chronos.Tests/OrientationCadranTests.cs, src/Chronos/Views/MainWindow.xaml, src/Chronos/ViewModels/MainViewModel.cs
- FOUND : commits a78e467, da760ca, 69c9f00, d05a2db
