---
plan: 09-02
phase: 09-calibration-des-plafonds-surfa-age
status: complete
requirements-completed: [CAL-01, CAL-02]
completed: 2026-07-09
note: exécuteur interrompu (erreur API) après son dernier commit — vérification finale et SUMMARY finalisés par l'orchestrateur
---

# SUMMARY — Plan 09-02 : Calibration manuelle + câblage

## Ce qui a été livré

- **Contrat + dialogue plafonds** (`9dc439c`) : `IBudgetPrompt` (Services, neutre) + `BudgetDialog`
  WPF (Views, hors gate de pureté) + `BudgetPrompt` adaptateur — deux champs (5 h / hebdo),
  valeurs pré-remplies, vide = null (pas de plafond).
- **CalibrateBudgetsCommand** (`71bcf64`) : commande `[RelayCommand]` dans MainViewModel — leçon
  GAP-1 appliquée (Load disque frais avant `with`/Save), source=Manual + timestamp posés pour les
  champs saisis, `RefreshOrchestrator.RequestRefresh()` public ajouté → recalcul immédiat des arcs.
- **Câblage DI + menu** (`b493d3d`) : `IBudgetPrompt`/`BudgetAutoCalibrator` enregistrés dans
  App.xaml.cs, `MenuItem` « Calibrer les plafonds… » ajouté au menu contextuel, calibrateur abonné
  à `RefreshOrchestrator.SnapshotChanged`. Le ctor MainViewModel (nouveau paramètre IBudgetPrompt)
  répercuté dans TOUTES les constructions (helpers + CadranBindingTests + OverlayWindowConfigTests
  — fix du blocker plan-checker appliqué).

## Vérification (orchestrateur)

- `dotnet test Chronos.sln -c Debug` : **147/147 verts** (144 après 09-01 + 3 nouveaux), 0 échec.
- Arbre de travail propre — les 3 tâches du plan sont intégralement commitées.
- ServicesLayerPurityTests verte (dialogue en Views, contrat neutre en Services).

## Déviations

- Exécuteur interrompu par une erreur de connexion API après son dernier commit de tâche : la
  vérification finale (suite complète) et ce SUMMARY ont été réalisés par l'orchestrateur —
  aucune tâche de code manquante.
