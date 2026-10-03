---
phase: 36-socle
plan: 01
subsystem: persistance-reglages
tags: [settings, system-text-json, tolerance, diagnostic, SOC-01]
requires: []
provides:
  - "SettingsService.Load() tolérant valeur par valeur (pré-passe JsonNode)"
  - "LectureReglages / IssueLectureReglages + SettingsService.DerniereLecture / DernieresRetombees"
  - "Ligne « Réglages (settings.json) : » dans le rapport de diagnostic (chronos.log au démarrage)"
affects: [37, 38, 39, 40]
tech-stack:
  added: []
  patterns:
    - "Pré-passe JsonNode : parse du document entier, validation de chaque membre avec les Options réelles, retrait du fautif puis Deserialize → l'initialiseur de la propriété fait le défaut"
    - "Enum.IsDefined pour refuser les entiers hors enum acceptés par STJ"
    - "NullabilityInfoContext pour refuser null sur une référence non-nullable (ThemeKey)"
key-files:
  created:
    - src/Chronos/Services/LectureReglages.cs
    - tests/Chronos.Tests/TestData/settings-valeurs-inconnues.json
  modified:
    - src/Chronos/Services/SettingsService.cs
    - src/Chronos/Services/DiagnosticService.cs
    - tests/Chronos.Tests/SettingsServiceTests.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
decisions:
  - "Pré-passe JsonNode plutôt qu'un JsonConverterFactory (default(T) remettrait Corner à TopLeft)"
  - "Clé de thème inconnue conservée brute dans SettingsService ; retombée à l'affichage par ThemeCatalog.ByKey (normalisation éventuelle en phase 39, côté VM)"
  - "Valeurs non-enum mal typées (double, bool, DateTimeOffset, null sur non-nullable) retombent aussi, une par une"
  - "Aucune écriture disque en lecture : la retombée est journalisée une fois par démarrage via le rapport de diagnostic, hors section [Réglage]"
metrics:
  duration: "~20 min"
  completed: 2026-10-03
  tasks: 3
  files: 6
---

# Phase 36 Plan 01 : Lecture tolérante des réglages (SOC-01) Summary

`SettingsService.Load()` lit maintenant settings.json valeur par valeur. Une pré-passe `JsonNode` valide chaque membre avec les vraies `Options` et retire ceux qui sont fautifs : chaque valeur inconnue ou mal typée reprend le défaut de SA propriété, et un JSON illisible redonne les défauts entiers. Le rapport de diagnostic nomme les valeurs retombées en une ligne.

## Tâches

| # | Tâche | Commit | Fichiers |
|---|-------|--------|----------|
| 1 | RED : fixture témoin, contrat `LectureReglages`, 10 tests SOC-01 | bbe70d2 | settings-valeurs-inconnues.json, LectureReglages.cs, SettingsService.cs (accesseurs), SettingsServiceTests.cs |
| 2 | GREEN : `Load()` par pré-passe JsonNode validée par les Options réelles | 6ac54fe | SettingsService.cs |
| 3 | Ligne « Réglages (settings.json) : » dans le diagnostic (TDD) | 97d179a | DiagnosticService.cs, DiagnosticServiceTests.cs |

## Preuves TDD

- **RED tâche 1** : 16 échecs sur 27 tests SettingsServiceTests (les nouveaux tests SOC-01). L'ancien `Load` renvoyait les défauts entiers (`"minuit"` au lieu de `"nord"`) et laissait `DerniereLecture` à `Absent`. Les tests préexistants restaient verts.
- **GREEN tâche 2** : 41/41 sur SettingsServiceTests, ServicesLayerPurityTests et ThemingTests.
- **RED tâche 3** : 3 échecs (la ligne manquait dans le rapport). **GREEN** : 96/96 sur DiagnosticServiceTests, SettingsServiceTests et GardeDiagnosticHistoriqueTests.
- **Vérification globale** : `dotnet build Chronos.sln` donne 0 avertissement et 0 erreur. `dotnet test Chronos.sln` donne 1756/1756 (le travail du plan 36-02 était déjà intégré).

## Points à retenir

- **Clé de thème inconnue** : `SettingsService` la conserve brute, parce que le catalogue des thèmes est WPF. `ThemeCatalog.ByKey` la fait retomber sur « minuit » à l'affichage. Une normalisation éventuelle se fera en phase 39, côté VM.
- **`"Tuiles"`** reste une valeur valide de `HistoriqueStyleSemaine` jusqu'à la phase 38. Le test `Tuiles_ne_coute_jamais_les_autres_reglages` n'asserte pas cette propriété : il reste vrai après la suppression du membre, qui deviendra alors un membre inconnu ignoré. La fixture utilise `"Mosaique"` pour prouver dès maintenant que la retombée fonctionne.
- **Les valeurs non-enum mal typées retombent aussi** : `RefreshIntervalSeconds: "abc"`, `Background: null`, `WeeklyAnchor: "pas une date"`, `ThemeKey: null`.
- **Le test générique `Chaque_enum_des_reglages_retombe_sur_son_propre_defaut`** passe en revue les enums par réflexion. Il couvre donc d'office tout enum futur, comme l'orientation de la phase 40.
- `Corner` invalide (`"Fantome"`, `99`, `null`) retombe sur `TopRight`, et non sur `TopLeft` (index 0).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocage] `System.IO` absent des usings implicites du projet de test (WPF)**
- **Found during:** tâche 3
- **Issue:** `Directory` / `Path` / `File` ne sont pas résolus dans `DiagnosticServiceTests.cs` (pas de `using System.IO;`).
- **Fix:** noms entièrement qualifiés `System.IO.*` dans le helper `RapportAvecSettings`.
- **Commit:** 97d179a

**2. [Rule 3 - Blocage] Variable locale `lecture` déjà utilisée dans `BuildReportAsync`**
- **Found during:** tâche 3
- **Issue:** CS0136 : une portée imbriquée de `BuildReportAsync` déclare déjà `lecture`.
- **Fix:** variable renommée `lectureReglages`. Le critère `grep "_settings.DerniereLecture" = 1` est toujours respecté.
- **Commit:** 97d179a

Ajout mineur : un helper de test `RapportAvecSettings` factorise la construction du `DiagnosticService` pour les trois tests, selon le motif prévu par le plan.

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND: src/Chronos/Services/LectureReglages.cs
- FOUND: tests/Chronos.Tests/TestData/settings-valeurs-inconnues.json
- FOUND: bbe70d2, 6ac54fe, 97d179a
