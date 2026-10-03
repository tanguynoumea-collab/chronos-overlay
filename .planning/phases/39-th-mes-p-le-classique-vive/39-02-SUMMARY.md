---
phase: 39-th-mes-p-le-classique-vive
plan: 02
subsystem: reglages
tags: [wpf, theming, reglages, itemscontrol, mvvm]
requires:
  - "39-01 : CategorieTheme, ChronosTheme.Categorie, catalogue de 15 thèmes"
provides:
  - "record GroupeThemes(Titre, Themes) + GroupeThemes.TitreDe (source unique des titres PÂLE / CLASSIQUE / VIVE)"
  - "MainViewModel.GroupesThemes (mêmes instances ThemeChoice que Themes)"
  - "Section Thème des réglages en trois groupes titrés"
affects: [39-03, 39-04]
tech-stack:
  added: []
  patterns:
    - "ItemsControl imbriqué : la commande remonte à AncestorType=Window (l'ItemsControl le plus proche porte le groupe)"
    - "Regroupement d'affichage par filtrage des MÊMES instances (pas de copie) pour garder la surbrillance"
key-files:
  created:
    - src/Chronos/ViewModels/GroupeThemes.cs
  modified:
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/Views/Reglages/ReglagesWindow.xaml
    - src/Chronos/Views/MainWindow.xaml
    - README.md
    - tests/Chronos.Tests/ThemingTests.cs
    - tests/Chronos.Tests/ReglagesWindowTests.cs
decisions:
  - "Titres de groupe en capitales (PÂLE, CLASSIQUE, VIVE), comme les étiquettes THÈME et STYLE DU CADRAN"
  - "Étiquette THÈME conservée au-dessus des titres de groupe ; à retirer si la revue visuelle la juge redondante"
  - "Marge des titres de groupe = marge par défaut du style Etiquette (0,16,0,8), aucune valeur ajoutée"
metrics:
  duration: "~3 min"
  completed: 2026-10-03
  tasks: 2
  files: 7
---

# Phase 39 Plan 02 : section Thème en trois groupes PÂLE · CLASSIQUE · VIVE

La section Thème des réglages range maintenant ses 15 vignettes en trois groupes titrés : PÂLE (6), CLASSIQUE (5) et VIVE (4). Les titres utilisent le style `Etiquette` (10,5, semi-gras, Ink2). Les vignettes gardent leur taille de 88 × 76 et passent à la ligne sans être coupées. La sélection et la surbrillance fonctionnent dans les trois groupes, parce que `GroupesThemes` reprend les mêmes instances `ThemeChoice` que `Themes` et que la commande de chaque vignette remonte jusqu'à la `Window`.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | `GroupeThemes` + `MainViewModel.GroupesThemes` (ordre de l'enum), test des groupes et du partage d'instances, README et commentaires de `MainWindow.xaml` | 3b45b42 |
| 2 | `GrilleThemes` lié à `GroupesThemes`, ItemsControl interne par groupe, commande via `AncestorType=Window` ; tests des titres, des 15 vignettes et de la sélection depuis chaque groupe | 3c95330 |

## Vérification

- `dotnet build src/Chronos/Chronos.csproj -c Debug` : 0 avertissement.
- `dotnet test` (suite complète) : 1746 réussis sur 1746.
- Tous les critères grep des deux tâches sont respectés.
- Retour à la ligne inchangé : 3 vignettes sur la première ligne à 640 px et 6 à 860 px (PÂLE contient 6 thèmes).

## Écarts au plan

Aucun : le plan a été exécuté tel qu'écrit.

Remarque : les tests TDD de chaque tâche ont été écrits avant le code de production, puis commités avec lui dans le commit unique prévu par le plan. Il n'y a donc pas de commit RED séparé.

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND: src/Chronos/ViewModels/GroupeThemes.cs
- FOUND: 3b45b42, 3c95330
