---
phase: 37-une-cha-ne-de-donn-es-claire
plan: 04
subsystem: viewmodels, réglages, composition
tags: [purge-r5, recalibrage-hebdo, gardes]
requires: ["37-03"]
provides:
  - "MainViewModel sans IRecalibrationPrompt (constructeur réduit d'un paramètre, 6e position)"
  - "ApplySnapshot : SevenDay.Apply(snap.SevenDay), aucun reset hebdo synthétique"
  - "WeeklyAnchor sans écrivain en production, toujours lu en secours par HistoriqueViewModel / BornesPlage"
  - "Garde de non-retour étendue (5 types du recalibrage + RecalibrateCommand)"
affects: [37-05, 37-06]
tech-stack:
  added: []
  patterns: []
key-files:
  created: []
  deleted:
    - src/Chronos/Services/WeeklyRecalibration.cs
    - src/Chronos/Services/IRecalibrationPrompt.cs
    - src/Chronos/ViewModels/RecalibrationViewModel.cs
    - src/Chronos/Views/RecalibrationDialog.xaml
    - src/Chronos/Views/RecalibrationDialog.xaml.cs
    - src/Chronos/Views/RecalibrationPrompt.cs
    - tests/Chronos.Tests/WeeklyRecalibrationTests.cs
    - tests/Chronos.Tests/Fakes/FakeRecalibrationPrompt.cs
  modified:
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/App.xaml.cs
    - src/Chronos/Views/Reglages/ReglagesWindow.xaml
    - src/Chronos/Services/IOuvreurHistorique.cs
    - README.md
    - CLAUDE.md
    - tests/Chronos.Tests/MainViewModelTests.cs
    - tests/Chronos.Tests/ReglagesWindowTests.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - tests/Chronos.Tests/CadranBindingTests.cs
    - tests/Chronos.Tests/MontageReglages.cs
    - tests/Chronos.Tests/OuvreurReglagesTests.cs
    - tests/Chronos.Tests/OverlayWindowConfigTests.cs
    - tests/Chronos.Tests/ThemingTests.cs
decisions:
  - "WeeklyAnchor et ses lecteurs (ChronosSettings, HistoriqueViewModel, BornesPlage, ISourceHistorique) non touchés, conformément à la décision verrouillée ; la doc de ChronosSettings.WeeklyAnchor parle encore d'« ancre du recalibrage » (ancre héritée, le fichier n'était pas dans le périmètre)"
  - "Recalibrer_est_dans_Comportement remplacé par Le_recalibrage_hebdo_a_quitte_Comportement (FindName(\"BoutonRecalibrerHebdo\") == null) en plus du DoesNotContain(\"Recalibrer\") sur les libellés"
  - "NewVmFull perd son out prompt : tous ses appelants passent de 7 à 6 out"
metrics:
  duration: "≈ 4 min"
  completed: 2026-10-03
  tasks: 2
  files: 23
requirements: [DAT-02]
---

# Phase 37 Plan 04 : étape 4 de la purge, recalibrage hebdo retiré — Summary

Le recalibrage manuel du reset hebdomadaire a disparu : fonction pure `WeeklyRecalibration`, dialogue, prompt, VM du
dialogue, carte « Recalibrer le reset hebdomadaire… » des réglages et commande `Recalibrate`. Le cadran affiche toujours le
reset fourni par la source et n'en fabrique jamais. `WeeklyAnchor` n'a plus d'écrivain, mais l'Historique la lit toujours en
secours. Tout tient dans un seul commit.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | Production : 6 fichiers supprimés, constructeur réduit, `ApplySnapshot` simplifié, carte retirée, docs | 902c714 |
| 2 | Tests : 2 fichiers supprimés, 9 sites de construction adaptés, 4 tests `Recalibrate_*` retirés, nouveau test, garde | 902c714 |

Le plan prescrit un seul commit pour l'étape : la tâche 1 n'a pas été commitée séparément.

## Ce qui a changé

- `MainViewModel` : le champ `_prompt`, le paramètre `IRecalibrationPrompt prompt` et la méthode `Recalibrate` avec son
  `[RelayCommand]` sont retirés. `ApplySnapshot` appelle maintenant `SevenDay.Apply(snap.SevenDay)`. `_last` reste, avec un
  commentaire qui précise désormais son usage (reset 5 h local). La doc de classe ne cite plus le recalibrage.
- `App.xaml.cs` : l'enregistrement DI du prompt est retiré. Le commentaire avant `Show` dit maintenant « les dialogues se
  centrent sur l'overlay (Owner) ».
- `ReglagesWindow.xaml` : la carte entière est retirée. La section Comportement garde « Arrière-plan » et « Lancer au
  démarrage ». Le commentaire du style des boutons ne cite plus « Recalibrer ».
- `IOuvreurHistorique` : le renvoi `<see cref="IRecalibrationPrompt"/>` est remplacé par une formulation neutre.
- README (section Réglages) et CLAUDE.md (Conventions) sont mis à jour selon le libellé du plan.

## Vérification

- `dotnet build Chronos.sln -warnaserror` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln` : 1705 / 1705 verts, contre 1717 avant. L'écart vient du fichier `WeeklyRecalibrationTests`,
  des 4 tests `Recalibrate_*` retirés et du nouveau test ajouté ; le test de réglages a été remplacé, sans effet sur le total.
- Le filtre `HistoriqueViewModelTests|BornesPlageTests` donne 32 / 32 verts : l'ancre est toujours lue.
- Greps d'acceptation, tous conformes :
  - les noms de types et de commandes retirés : 0 dans src ;
  - `SevenDay.Apply(snap.SevenDay)` = 1 ;
  - `WeeklyAnchor =` = 0 dans src ;
  - `recalibr` = 0 dans README.md, `recalibrable` = 0 dans CLAUDE.md ;
  - côté tests, il ne reste que la garde ;
  - `"WeeklyRecalibration"` = 1 dans GardesPerimetreTests.
- TDD : `Une_fenetre_hebdo_sans_reset_reste_sans_reset_malgre_une_ancre` est vert après la suppression. Je n'ai pas lancé
  de phase RED séparée sur l'ancien code. Le raisonnement : avec l'ancien `ApplySnapshot`, une fenêtre « estimée » sans
  `ResetsAt` et une ancre recevaient un reset synthétique, donc `HasTime` passait à vrai et le test aurait échoué.
- `GardeDocumentationChaineTests.TermesRetires` contenait déjà `WeeklyRecalibration`. Les 3 documents restent donc
  surveillés.

## Deviations from Plan

Aucune déviation fonctionnelle. Deux retouches de commentaires vont un peu au-delà du libellé du plan :
- dans la doc de classe de `MainViewModel`, « expose les 4 commandes » devient « expose les commandes » ;
- dans `CompositionRootTests`, le commentaire qui citait `IRecalibrationPrompt` a été réécrit.

Aucun stub.

## Self-Check: PASSED

- ABSENT (attendu) : les 6 fichiers source et les 2 fichiers de test supprimés
- FOUND : commit 902c714
