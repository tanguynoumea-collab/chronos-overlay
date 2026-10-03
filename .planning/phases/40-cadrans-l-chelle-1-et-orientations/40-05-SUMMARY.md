---
phase: 40-cadrans-l-chelle-1-et-orientations
plan: 05
subsystem: views / cadrans
tags: [cadran, orientation, echelle-1, tdd, wpf, xaml, volets, arcs]
requires: [40-04]
provides:
  - "CadranVoletsView : DP Orientation (Horizontal par défaut), gabarits H 190 × 66 et V 128 × 190"
  - "CadranBraisesView : empreinte lue dans les tokens CadranLargeurBraises / CadranHauteurBraises"
  - "Views/Cadrans/CadranArcsView.xaml : Anneaux extrait de MainWindow (VueArcs), utilisable par la galerie"
  - "Assistant de test ArcNomme (FindName via VueArcs)"
affects: [40-06, 40-08]
tech-stack:
  added: []
  patterns: ["gabarits à positions absolues (Left/Top + Margin) tirés de la maquette", "vue de cadran extraite avec un BoolToVis local"]
key-files:
  created:
    - src/Chronos/Views/Cadrans/CadranArcsView.xaml
    - src/Chronos/Views/Cadrans/CadranArcsView.xaml.cs
  modified:
    - src/Chronos/Views/Cadrans/CadranVoletsView.xaml
    - src/Chronos/Views/Cadrans/CadranVoletsView.xaml.cs
    - src/Chronos/Views/Cadrans/CadranBraisesView.xaml
    - src/Chronos/Views/MainWindow.xaml
    - tests/Chronos.Tests/CadransOrientationBindingTests.cs
    - tests/Chronos.Tests/CadransThemeBindingTests.cs
    - tests/Chronos.Tests/CadranBindingTests.cs
    - tests/Chronos.Tests/GardeCouleursCadransTests.cs
decisions:
  - "Volets a sa propre table VariantesVolets. Les tests génériques (gabarit visible, empreinte, orientation des contrôles) parcourent ToutesVariantes = Variantes + VariantesVolets. Les tests qui dépendent de la structure (corps, pire cas, empilement, hachure/filet) sont dédiés à Volets, parce que la structure diffère de celle de Fusible et Marée : tuile « 5H », plaque Border, pas de boîte de valeur de 16."
  - "Pour les textes au pire cas, la boîte est l'ActualWidth de la Border plaque (90 en H, 56 en V). Chaque variante est montée deux fois (% puis décompte), comme au plan 04."
  - "MainWindow : xmlns:ctrl est retiré, car plus aucun ctrl: n'y reste après l'extraction."
metrics:
  duration: "~15 min"
  completed: 2026-10-03
  tasks: 2
  files: 10
---

# Phase 40 Plan 05 : Volets à deux gabarits, Braises tokenisé, Arcs extrait — Summary

`CadranVoletsView` a maintenant deux gabarits à l'échelle 1, sélectionnés par une DP `Orientation` portée par la vue, sur le même motif que Fusible et Marée au plan 04 :
- **H, 190 × 66** : deux lignes, chacune avec une tuile 26, une plaque 90 × 26 en corps 13 et 6 volets en ligne 62 × 14.
- **V, 128 × 190** : deux colonnes, chacune avec une tuile 26, une plaque 56 × 40 en corps 14 et 6 volets empilés 16 × 104.

Les positions viennent de la maquette (`voletsH` / `voletsV`). Liaisons et pinceaux thémés sont conservés, y compris la hachure « plancher » et le filet dans les deux sens.

Braises lit son empreinte 170 × 170 dans ses tokens. Le style Anneaux devient une vue autonome, `CadranArcsView` : MainWindow l'utilise sous `x:Name="VueArcs"`, et les 11 accès aux arcs dans les tests passent par `ArcNomme`. Les huit variantes existent donc désormais sous forme de vues.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | RED : variantes Volets et Braises dans CadransOrientationBindingTests, comptes Volets doublés dans le test de thème | f031433 |
| 1 | GREEN : Volets H/V, DP Orientation, Braises sur tokens | 2df6c27 |
| 2 | Arcs extrait en CadranArcsView, ArcNomme, garde à 5 vues | c65a796 |

## Vérification

- RED : le build des tests échouait sur `CadranVoletsView.Orientation` (CS1061).
- Filtre `CadransOrientationBinding|CadransThemeBinding|GardeCouleursCadrans` : 25/25.
- Filtre `CadranBindingTests|GardeCouleursCadrans|GardeGestesCadran|ReglagesWindowTests` : 74/74.
- `dotnet build Chronos.sln` : 0 avertissement, 0 erreur.
- `dotnet test` : 1840/1840.
- Critères grep, tous conformes :
  - Volets : 2 gabarits, `CadranCorpsPlaqueH` = 4, `CadranCorpsPlaqueV` = 4, `56 × 40` = 2, `16 × 104` = 2, `PlaqueHachure` = 4 ;
  - aucune couleur et aucun `FontSize` littéral dans Volets ;
  - Braises : `Width="170"` = 0 ;
  - Arcs : 4 arcs nommés dans la vue, 0 dans MainWindow, `VueArcs` = 1, `ArcNomme` = 11, `fenetre.FindName("Arc` = 0, aucune couleur hexadécimale ;
  - garde : `xamls.Count == 5`.

## Écarts par rapport au plan

**1. Tests Volets dédiés au lieu d'une simple extension de la table `Variantes`**
- Les tests du plan 04 sur les corps, le pire cas et le recouvrement supposent les libellés « 5 H », une boîte Grid de 16 et FuseBar/TideColumn. Volets a une autre structure.
- J'ai donc gardé la table `Variantes` pour ces tests, ajouté `VariantesVolets` avec ses tests dédiés, et fait parcourir `ToutesVariantes` aux tests génériques.
- Le contrat du plan est inchangé : corps 11/13/14, pire cas ≤ plaque, volets 62 × 14 et 16 × 104, défaut Horizontal, `FlapRow.Orientation` du gabarit, Braises 170 × 170.
- J'ai aussi ajouté un test qui vérifie que hachure et filet restent présents dans les deux sens.

## Exigences

Aucune case cochée : CAD-01 et CAD-03 ne seront finalisées qu'au plan 06, qui retire les Viewbox de MainWindow.

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND : src/Chronos/Views/Cadrans/CadranArcsView.xaml(.cs), CadranVoletsView.xaml(.cs)
- FOUND : commits f031433, 2df6c27, c65a796
