---
phase: 42-un-geste-sur-toute-la-silhouette
plan: 01
subsystem: viewmodels/gestes
tags: [tdd, gst-01, automate-pur, drag-move, double-clic]
requires: []
provides:
  - "AutomateGeste (Appui / Deplacement / Relache / PerteCapture), ActionGeste, TypeActionGeste, EtatGeste — contrat utilisé tel quel par le répartiteur de MainWindow (plan 42-03)"
affects:
  - src/Chronos/Views/MainWindow.xaml.cs (plan 42-03, câblage)
tech-stack:
  added: []
  patterns:
    - "Automate pur sans System.Windows ni horloge, sur le modèle d'ArbitreClicCentre, avec garde textuelle de pureté"
key-files:
  created:
    - src/Chronos/ViewModels/AutomateGeste.cs
    - tests/Chronos.Tests/AutomateGesteTests.cs
  modified: []
decisions:
  - "Double-clic décidé à l'appui (Clic(n) immédiat, état Consomme) ; simple clic armé au relâchement (Clic(1)) ; ArbitreClicCentre inchangé"
  - "Seuil strict par axe : Math.Abs(x - _x0) > seuilX || Math.Abs(y - _y0) > seuilY, positions et seuils en DIP sans conversion"
  - "L'état passe à Glisse AVANT de rendre CommencerGlisser : le WM_LBUTTONUP synthétique de DragMove et la perte de capture ne produisent rien"
  - "Drapeau dernierEtaitGlisser lu puis remis à zéro à chaque Appui : un appui compté double juste après un glisser est traité comme simple"
metrics:
  duration: "~3 min"
  completed: 2026-10-03
  tasks: 2
  files: 2
---

# Phase 42 Plan 01 : AutomateGeste — clic, double-clic et glisser sur une même surface — Summary

Automate pur `AutomateGeste` (sans WPF, sans horloge) qui décide clic simple au relâchement, double-clic à l'appui et
glisser au franchissement strict du seuil Windows sur un seul axe. Il absorbe la réentrance de `DragMove` et le faux
double-clic qui suit un glisser. 13 tests purs `[Fact]`.

## Tâches

| # | Nom | Commit | Fichiers |
|---|-----|--------|----------|
| 1 | RED : AutomateGesteTests (13 cas purs) | 4174cc0 | tests/Chronos.Tests/AutomateGesteTests.cs |
| 2 | GREEN : AutomateGeste.cs | 08f0361 | src/Chronos/ViewModels/AutomateGeste.cs, tests/Chronos.Tests/AutomateGesteTests.cs (using) |

## Vérification

- Filtre `AutomateGesteTests|ArbitreClicCentreTests|GardeGestesCadranTests` : 23/23 verts.
- `dotnet build Chronos.sln` : 0 avertissement, 0 erreur.
- Suite complète `dotnet test tests/Chronos.Tests` : 1906/1906 verts.
- `ArbitreClicCentre.cs` et `MainViewModel.cs` non modifiés. Garde de pureté : 0 occurrence de `System.Windows` / `DateTime` / `Stopwatch` dans `AutomateGeste.cs`.

## Écarts au plan

### Corrections automatiques

**1. [Rule 3 - Blocage] `using System.IO;` manquant dans le fichier de tests**
- **Trouvé pendant :** tâche 2 (premier build GREEN)
- **Problème :** le projet de tests n'a pas d'usings implicites pour `System.IO` (`GardeGestesCadranTests` l'importe explicitement). Le test 13 (`Path` / `File`) ne compilait donc pas. Au RED, cette erreur était masquée par les symboles manquants, attendus.
- **Correction :** ajout de `using System.IO;` en tête de `AutomateGesteTests.cs`, commité avec la tâche 2.
- **Fichiers modifiés :** tests/Chronos.Tests/AutomateGesteTests.cs
- **Commit :** 08f0361

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND : src/Chronos/ViewModels/AutomateGeste.cs
- FOUND : tests/Chronos.Tests/AutomateGesteTests.cs
- FOUND : 4174cc0, 08f0361
