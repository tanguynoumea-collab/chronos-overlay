---
phase: 40-cadrans-l-chelle-1-et-orientations
plan: 04
subsystem: views / cadrans
tags: [cadran, orientation, echelle-1, tdd, wpf, xaml]
requires: [40-03]
provides:
  - "DP Orientation de VUE sur CadranFusibleView (Horizontal par défaut) et CadranMareeView (Vertical par défaut), rappel AppliquerOrientation"
  - "Deux gabarits par vue (GabaritHorizontal / GabaritVertical) à l'empreinte exacte, en DynamicResource des tokens CadranLargeur*/CadranHauteur*"
  - "Corps de texte en tokens CadranCorpsLibelle (11) / CadranCorpsValeur (12), boîtes de valeur de hauteur 16"
  - "CadransOrientationBindingTests : table de variantes extensible (plans 05 et 08)"
affects: [40-05, 40-06, 40-08]
tech-stack:
  added: []
  patterns: ["DP d'orientation portée par la vue + bascule de Visibility en code-behind", "preuve de mise en page par FormattedText au pire cas + rectangles rendus (TransformToAncestor)"]
key-files:
  created:
    - tests/Chronos.Tests/CadransOrientationBindingTests.cs
  modified:
    - src/Chronos/Views/Cadrans/CadranFusibleView.xaml
    - src/Chronos/Views/Cadrans/CadranFusibleView.xaml.cs
    - src/Chronos/Views/Cadrans/CadranMareeView.xaml
    - src/Chronos/Views/Cadrans/CadranMareeView.xaml.cs
    - tests/Chronos.Tests/CadransThemeBindingTests.cs
decisions:
  - "DP Orientation de type System.Windows.Controls.Orientation (même type que celle des contrôles FuseBar / TideColumn). Les tests convertissent OrientationCadran vers ce type."
  - "Marée H : bande ramenée de 118 à 102 px (x 32..134), comme l'a décidé l'orchestrateur. Le choix est consigné en commentaire XAML."
  - "Le test des textes au pire cas monte chaque variante deux fois (ShowCountdown faux puis vrai), pour mesurer chaque TextBlock de valeur pendant qu'il est visible. Un TextBlock replié a un slot de largeur nulle, ce qui donnerait un faux rouge."
metrics:
  duration: "~12 min"
  completed: 2026-10-03
  tasks: 2
  files: 6
---

# Phase 40 Plan 04 : Fusible et Marée à l'échelle 1, deux orientations — Summary

`CadranFusibleView` et `CadranMareeView` ont chacune deux gabarits : `GabaritHorizontal` et `GabaritVertical`. Une DP `Orientation` portée par la vue choisit le gabarit affiché ; ce n'est pas le DataContext qui décide, pour que la galerie puisse montrer H et V côte à côte sur un même VM.
Les quatre gabarits ont exactement leur empreinte à l'échelle 1 : Fusible 190 × 92 et 110 × 190, Marée 132 × 160 et 190 × 96. Les positions viennent de la maquette (fusibleH/V, mareeV/H). Les libellés restent en corps 11 et les valeurs en corps 12. Toutes les liaisons et tous les pinceaux thémés de la phase 39 sont conservés.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | RED : CadransOrientationBindingTests (7 faits, 4 variantes) + comptes doublés du test de thème | 39e77a3 |
| 2 | GREEN : deux gabarits par vue, DP Orientation + AppliquerOrientation | 8ccc799 |

## Vérification

- RED : le build du projet de test échouait sur `Orientation` (CS1061) pour les deux vues.
- Filtre `CadransOrientationBinding|CadransThemeBinding|GardeCouleursCadrans|CadranBindingTests` : 44/44, dont 7/7 pour le nouveau fichier.
- `dotnet build Chronos.sln` : 0 avertissement, 0 erreur.
- `dotnet test` : 1835/1835.
- Critères grep, tous conformes :
  - 2 gabarits nommés par vue ;
  - `CadranLargeurFusibleV|CadranHauteurFusibleH` = 2 et `CadranLargeurMareeH` = 1 ;
  - `Orientation="Vertical"` (Fusible) = 2 et `Orientation="Horizontal"` (Marée) = 2 ;
  - `Width="102"` = 2 ;
  - `FontSize="1x"`, `MergedDictionaries` et `DesignTokens.xaml` = 0 ;
  - `new FrameworkPropertyMetadata(Orientation.Vertical` = 1 (Marée).
- Les Viewbox de `MainWindow.xaml` sont toujours en place ; elles seront retirées au plan 06.

## Écarts par rapport au plan

**1. [Rule 3 - Bloquant] `using System.Windows.Controls.Primitives` manquant**
- `LayoutInformation` vit dans ce namespace. Je l'ai ajouté au fichier de test pendant la tâche 1 (inclus dans 39e77a3).

**2. Précisions de test, sans changer le contrat**
- Textes au pire cas : seuls les TextBlock de valeur visibles sont mesurés, et chaque variante est montée deux fois (% puis décompte).
- Recouvrement : il n'y a recouvrement que si l'intersection a une aire non nulle. Des bords qui se touchent ne comptent pas.
- `Les_controles_portent_l_orientation_du_gabarit` inspecte les deux gabarits d'une seule instance par style, y compris le gabarit replié.

## Exigences

Aucune case cochée, car CAD-01 et CAD-03 ne sont que partiellement livrées. Restent Volets H/V (plan 05), Braises / Arcs, et le retrait des Viewbox dans MainWindow (plan 06).

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND : tests/Chronos.Tests/CadransOrientationBindingTests.cs, src/Chronos/Views/Cadrans/{CadranFusibleView,CadranMareeView}.xaml(.cs)
- FOUND : commits 39e77a3, 8ccc799
