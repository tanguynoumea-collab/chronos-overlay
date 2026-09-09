---
phase: 16-fondations-du-delta-persistance-d-molition-des-plafonds
plan: 01
subsystem: services
tags: [wpf, mvvm, dependency-injection, dead-code-removal, budget-calibration]

# Dependency graph
requires:
  - phase: 09-calibration-des-plafonds
    provides: le sous-système CAL-01/CAL-02 (dialogue de plafonds, calibrateur auto, logique pure) — démoli ici
provides:
  - Base de code où plus aucun type ne sait déduire, saisir ou appliquer un plafond de tokens
  - MainViewModel à 11 paramètres de ctor (IBudgetPrompt retiré)
  - Fenêtre de réglages sans entrée « Plafonds… » (UniformGrid RÉGLAGES à 3 boutons)
  - Composition racine sans IBudgetPrompt ni BudgetAutoCalibrator (aucune résolution eager avant StartAsync)
affects: [16-03 renommage JsonlEstimationProvider, 16-04 suppression des 6 champs et de BudgetSource, 19 doctrine du composite]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Démolition par vagues compilables : UI/prompt → calibrateur → logique pure, chaque commit laissant la solution buildable"
    - "Changement d'arité de ctor traité comme geste ATOMIQUE (production + tous les sites de construction en test dans le même commit)"

key-files:
  created: []
  modified:
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/Views/SettingsWindow.xaml
    - src/Chronos/App.xaml.cs
    - src/Chronos/Services/RefreshOrchestrator.cs
    - src/Chronos/Services/GatedOAuthUsageProvider.cs
    - tests/Chronos.Tests/MainViewModelTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - tests/Chronos.Tests/CadranBindingTests.cs
    - tests/Chronos.Tests/OverlayWindowConfigTests.cs
    - tests/Chronos.Tests/ThemingTests.cs

key-decisions:
  - "Le recul de 405 → 384 tests est le LIVRABLE, pas une régression : les 21 tests supprimés couvraient exclusivement du code supprimé, aucun comportement survivant n'a perdu sa couverture."
  - "BudgetSource.cs et les 6 champs de ChronosSettings sont volontairement laissés intacts : les toucher ici casserait la compilation (ChronosSettings les référence encore). Suppression au plan 16-04."
  - "Le commentaire « CAL-02 » résiduel de BudgetSource.cs n'est PAS corrigé : le fichier disparaît au plan 16-04, le retoucher serait de la churn et un risque de conflit avec 16-04."
  - "Purge manuelle des 3 obj/**/Views/BudgetDialog.g.cs : dotnet clean ne nettoie que la configuration courante (Debug), 2 copies Release survivaient."

patterns-established:
  - "Ordre de démolition sûr : le consommateur avant le consommé (BudgetAutoCalibrator avant BudgetCalibration), le type neutre partagé en dernier (BudgetSource)."
  - "Après suppression d'un .xaml : dotnet clean sur TOUTES les configurations + purge explicite des *.g.cs orphelins avant le premier build."

requirements-completed: [DEL-05]

# Metrics
duration: 24min
completed: 2026-09-09
---

# Phase 16 Plan 01 : Démolition du sous-système de calibration des plafonds — Summary

**Les 7 fichiers de production, 3 fichiers de test et 6 points d'accroche du sous-système de plafonds (CAL-01/CAL-02) ont disparu du code, de la DI et de la fenêtre de réglages — le bug des pourcentages faux après Max x5 → Max x20 devient structurellement impossible plutôt que corrigé.**

## Performance

- **Duration:** 24 min
- **Started:** 2026-09-09T11:24:00Z
- **Completed:** 2026-09-09T11:48:00Z
- **Tasks:** 3 / 3
- **Files modified:** 20 (10 modifiés, 10 supprimés)

## Accomplishments

- **L'UI de calibration est inatteignable** : le bouton « Plafonds… », la commande `CalibrateBudgetsCommand`, l'interface `IBudgetPrompt`, son implémentation WPF `BudgetPrompt`, le dialogue `BudgetDialog` et son ViewModel n'existent plus. Le ctor de `MainViewModel` passe de 12 à 11 paramètres.
- **Le calibrateur automatique et sa logique pure sont supprimés** : `BudgetAutoCalibrator` (le service au défaut logique documenté — il ne calibrait que quand une source exacte était présente, c'est-à-dire quand l'estimation ne servait pas) et `BudgetCalibration` (`Deduce` / `ApplyAuto`, la règle qui gelait à vie un plafond marqué `Manual`).
- **La composition racine est nettoyée** : la résolution eager `GetRequiredService<BudgetAutoCalibrator>()` avant `StartAsync` a disparu ; son voisin `GetRequiredService<MainViewModel>()` (Pitfall 3, abonnement du VM avant la charge initiale) est intact.
- **Zéro perte de couverture nette** : 21 tests supprimés, tous nominativement justifiés par du code disparu (détail ci-dessous). 384 tests verts, 0 échec.

## Task Commits

Chaque tâche a été committée atomiquement, la solution compilant et la suite étant à 0 échec après chacune :

1. **Task 1 : Supprimer l'entrée « Plafonds… », la commande et le dialogue** — `fc15400` (refactor) — 405 → 402 tests
2. **Task 2 : Supprimer le calibrateur automatique et la logique pure de déduction** — `062534e` (refactor) — 402 → 384 tests
3. **Task 3 : Reformuler les commentaires orphelins référençant la calibration** — `e9efc47` (docs) — 384 tests, inchangé

## Files Created/Modified

### Supprimés (10)

| Fichier | Rôle disparu |
|---|---|
| `src/Chronos/Services/IBudgetPrompt.cs` | interface `IBudgetPrompt` + record `BudgetSelection` |
| `src/Chronos/Views/BudgetPrompt.cs` | implémentation WPF du prompt |
| `src/Chronos/Views/BudgetDialog.xaml` | dialogue de saisie des deux plafonds |
| `src/Chronos/Views/BudgetDialog.xaml.cs` | code-behind du dialogue |
| `src/Chronos/ViewModels/BudgetDialogViewModel.cs` | ViewModel du dialogue |
| `src/Chronos/Services/BudgetAutoCalibrator.cs` | calibrateur auto opportuniste (CAL-02), `IDisposable` abonné à `SnapshotChanged` |
| `src/Chronos/Services/BudgetCalibration.cs` | logique pure `Deduce` / `ApplyAuto` |
| `tests/Chronos.Tests/Fakes/FakeBudgetPrompt.cs` | fake du prompt |
| `tests/Chronos.Tests/BudgetAutoCalibratorTests.cs` | 3 tests |
| `tests/Chronos.Tests/BudgetCalibrationTests.cs` | 15 tests |

### Modifiés (10)

- `src/Chronos/ViewModels/MainViewModel.cs` — champ `_budgetPrompt`, 7e paramètre du ctor, affectation et commande `CalibrateBudgets` retirés ; commentaire de `_orchestrator` reformulé. Le champ `_orchestrator` survit (4 occurrences : encore utilisé par `ToggleOAuthUsage` et `LoginClaude`).
- `src/Chronos/Views/SettingsWindow.xaml` — bouton « Plafonds… » retiré ; `<Button>` passe de 10 à 9 ; `UniformGrid Columns="2"` conservée avec 3 enfants (case vide en bas à droite, attendue, retouche UI = phase 20).
- `src/Chronos/App.xaml.cs` — 3 points d'accroche retirés : enregistrement `IBudgetPrompt/BudgetPrompt`, résolution eager du calibrateur, enregistrement DI du calibrateur ; commentaire du log de démarrage `(token/OAuth/sources/plafonds)` → `(token/OAuth/sources)`.
- `src/Chronos/Services/RefreshOrchestrator.cs` — XML-doc de `RequestRefresh` recentré (bascule de source / login OAuth). Aucun changement exécutable, la méthode survit.
- `src/Chronos/Services/GatedOAuthUsageProvider.cs` — mention `JsonlEstimationProvider relit ses plafonds` remplacée par `à chaque appel (aucun cache de réglage)` — évite au plan 16-03 de rouvrir ce fichier.
- `tests/Chronos.Tests/MainViewModelTests.cs` — helpers `Build`/`NewVmFull`/`NewVm` désarités, 12 sites de construction ajustés, 3 tests supprimés.
- `tests/Chronos.Tests/CompositionRootTests.cs` — enregistrements `IBudgetPrompt` et `BudgetAutoCalibrator` + assertion de résolution du calibrateur retirés.
- `tests/Chronos.Tests/CadranBindingTests.cs`, `OverlayWindowConfigTests.cs`, `ThemingTests.cs` — argument `new FakeBudgetPrompt()` retiré de l'appel direct au ctor. Aucune assertion changée.

## Tests supprimés — justification nominative

**Critère appliqué : 0 échec + aucune perte de couverture nette.** Chaque test ci-dessous couvrait exclusivement du code qui n'existe plus après ce plan. Total : **405 → 384 (−21)**.

### `tests/Chronos.Tests/MainViewModelTests.cs` — 3 tests (commit `fc15400`)

| Test supprimé | Code disparu qui le justifie |
|---|---|
| `CalibrateBudgets_persiste_le_plafond_saisi_en_Manual_et_None_pour_le_champ_vide` | `MainViewModel.CalibrateBudgets()` + `BudgetSelection` + `IBudgetPrompt.Ask` |
| `CalibrateBudgets_annule_ne_persiste_rien` | idem — branche d'annulation (`sel is null`) de la commande supprimée |
| `CalibrateBudgets_n_ecrase_pas_les_reglages_persistes_par_un_autre_writer` | idem — le motif GAP-1 (`Load()` disque frais avant `Save`) reste couvert par `Recalibrate`, `ToggleOAuthUsage` et `ToggleCadranMode`, dont les tests survivent |

### `tests/Chronos.Tests/BudgetAutoCalibratorTests.cs` — 3 tests, fichier entier (commit `062534e`)

| Test supprimé | Code disparu qui le justifie |
|---|---|
| `Fenetre_5h_exacte_deduit_et_persiste_un_plafond_Auto` | `BudgetAutoCalibrator.OnSnapshot` + `BudgetCalibration.Deduce`/`ApplyAuto` |
| `Plafond_Manual_jamais_ecrase` | la règle de priorité `Manual > Auto` de `BudgetCalibration.ApplyAuto` |
| `Aucune_fenetre_exacte_reste_inerte` | le chemin à coût zéro de `BudgetAutoCalibrator` (abonnement à `SnapshotChanged`) |

### `tests/Chronos.Tests/BudgetCalibrationTests.cs` — 15 tests, fichier entier (commit `062534e`)

Couvrent `BudgetCalibration.Deduce` (7) et `BudgetCalibration.ApplyAuto` (8), la classe statique supprimée :

| Test supprimé | Membre disparu |
|---|---|
| `Deduce_util_moitie_rend_le_double_des_tokens` | `BudgetCalibration.Deduce` |
| `Deduce_util_quart_rend_le_quadruple_des_tokens` | `BudgetCalibration.Deduce` |
| `Deduce_util_nulle_rend_null` | `BudgetCalibration.Deduce` |
| `Deduce_util_negative_rend_null` | `BudgetCalibration.Deduce` |
| `Deduce_tokens_nuls_rend_null` | `BudgetCalibration.Deduce` |
| `Deduce_tokens_negatifs_rend_null` | `BudgetCalibration.Deduce` |
| `Deduce_util_superieure_a_un_est_acceptee` | `BudgetCalibration.Deduce` |
| `ApplyAuto_5h_source_None_ecrit_budget_source_Auto_et_timestamp` | `BudgetCalibration.ApplyAuto` |
| `ApplyAuto_5h_source_Auto_ecrase_budget_et_timestamp_reste_Auto` | `BudgetCalibration.ApplyAuto` |
| `ApplyAuto_5h_source_Manual_ne_change_rien` | `BudgetCalibration.ApplyAuto` |
| `ApplyAuto_5h_ne_contamine_pas_le_plafond_hebdo` | `BudgetCalibration.ApplyAuto` |
| `ApplyAuto_hebdo_source_None_ecrit_budget_source_Auto_et_timestamp` | `BudgetCalibration.ApplyAuto` |
| `ApplyAuto_hebdo_source_Auto_ecrase_budget_et_timestamp` | `BudgetCalibration.ApplyAuto` |
| `ApplyAuto_hebdo_source_Manual_ne_change_rien` | `BudgetCalibration.ApplyAuto` |
| `ApplyAuto_hebdo_ne_contamine_pas_le_plafond_5h` | `BudgetCalibration.ApplyAuto` |

### Assertions retirées (sans suppression de test)

- `CompositionRootTests` : `Assert.NotNull(provider.GetRequiredService<BudgetAutoCalibrator>())` — le service n'existe plus. La preuve de disposition des Singletons `IDisposable` repose sur `MarqueurDisposable`, intacte. **3 tests, 0 échec.**

## Decisions Made

- **Le recul du compteur est le livrable.** 405 → 384 : la suite ne perd aucune couverture d'un comportement survivant. Le critère retenu est « 0 échec + justification nominative de chaque test supprimé », pas « ≥ 405 ».
- **`BudgetSource.cs` et les 6 champs de `ChronosSettings` sont laissés intacts.** `ChronosSettings.FiveHourBudgetSource` / `WeeklyBudgetSource` référencent encore l'enum ; le supprimer ici casserait la compilation. Vérifié : exactement 3 occurrences de `BudgetSource` dans `src/` (la déclaration + les 2 propriétés) — le plan 16-04 a bien encore son objet.
- **`FiveHourWindowInference.cs`, `WeeklyWindow.cs` et `CompositeUsageProvider.cs` non touchés**, conformément aux contraintes : orphelins mais conservés (réutilisables en phase 19), et la refonte de la doctrine du composite est la phase 19.
- **Aucun fichier partagé avec le plan 16-02 n'a été touché** (`ChronosPaths.cs`, `Models/WindowState.cs`, `LastExact*.cs`) — vérifié par `git diff --name-only` : 0 résultat. Les deux plans de la vague 1 restent parallélisables.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] 9 sites d'appel `NewVmFull` supplémentaires à désariter**

- **Found during:** Task 1
- **Issue:** Le plan listait explicitement 6 sites de construction de `MainViewModel` à ajuster (helper `Build`, helper `NewVmFull`, `NewVm`, ligne 111, ligne 244, + les 3 fichiers `CadranBindingTests`/`OverlayWindowConfigTests`/`ThemingTests`). En réalité, **10 appels `var vm = NewVmFull(...)`** passaient 8 arguments `out` dans `MainViewModelTests.cs` (lignes 203, 221, 254, 264, 309, 337, 360, 413, 421, 437) ; les 9 non listés faisaient échouer la compilation après le passage du helper de 8 à 7 paramètres.
- **Fix:** Retrait du 7e argument (`out _` dans les 10 cas, aucun n'observait le prompt de plafonds) par transformation mécanique.
- **Files modified:** `tests/Chronos.Tests/MainViewModelTests.cs`
- **Verification:** `dotnet build Chronos.sln` → 0 erreur ; `dotnet test` → 402 tests, 0 échec.
- **Committed in:** `fc15400` (commit de la Task 1 — geste atomique obligatoire, l'arité du ctor l'imposait)

**2. [Rule 3 - Blocking] `dotnet clean Chronos.sln` ne purge pas les copies Release de `BudgetDialog.g.cs`**

- **Found during:** Task 1 (étape « PIÈGE OBLIGATOIRE », Pitfall 3 de la recherche)
- **Issue:** `dotnet clean Chronos.sln --nologo` ne nettoie que la configuration **courante** (Debug). Après exécution, **2 des 3** copies fantômes subsistaient : `obj/Release/net8.0-windows/Views/BudgetDialog.g.cs` et `obj/Release/net8.0-windows/win-x64/Views/BudgetDialog.g.cs`. Le critère d'acceptation exigeait 0 résultat, et un build Release ultérieur aurait rencontré une `partial class BudgetDialog` orpheline.
- **Fix:** `dotnet clean Chronos.sln -c Release` puis purge explicite `find src/Chronos/obj -name "BudgetDialog.g.cs" -delete`.
- **Files modified:** aucun fichier suivi (`src/Chronos/obj/` est gitignoré — vérifié via `git check-ignore`).
- **Verification:** `find src/Chronos/obj -name "BudgetDialog.g.cs"` → 0 résultat ; `dotnet build` → 0 erreur.
- **Committed in:** aucun (artefacts de build non suivis)

### Écarts assumés (non corrigés)

**Critère d'acceptation de la Task 3 partiellement inatteignable :** `grep -rn "CAL-01\|CAL-02" src/Chronos` renvoie **1** résultat au lieu de 0 — `src/Chronos/Services/BudgetSource.cs:4` (« métadonnée neutre de calibration (CAL-02) »). Ce fichier est **explicitement hors périmètre** par contrainte dure (« NE PAS toucher à `BudgetSource.cs` — plan 16-04 »). Le retoucher pour un commentaire serait de la churn sur un fichier programmé pour la suppression, et un risque de conflit avec 16-04. **L'esprit du critère — aucun commentaire orphelin dans un fichier qui survit à la phase — est satisfait :** les deux seuls fichiers survivants concernés (`RefreshOrchestrator.cs`, `GatedOAuthUsageProvider.cs`) ont été reformulés.

---

**Total deviations:** 2 auto-corrigées (2 blocantes, Rule 3) + 1 écart assumé documenté
**Impact on plan:** Aucun scope creep. Les deux corrections étaient strictement nécessaires pour compiler / satisfaire le piège documenté. L'écart assumé est imposé par une contrainte dure du plan.

## Issues Encountered

- **Fins de ligne LF vs CRLF.** Les fichiers sources sont en LF dans l'arbre de travail (le dépôt applique `autocrlf` au checkout). Une première tentative d'édition sur des motifs contenant `\r\n` a échoué proprement (assertion avant écriture, aucun fichier corrompu) ; les outils d'édition ont été rendus agnostiques aux fins de ligne et au BOM.
- **2 avertissements xUnit2031 pré-existants** dans `tests/Chronos.Tests/DesktopUiaSessionSourceTests.cs` (lignes 345 et 364) — présents avant ce plan, sans lien avec les fichiers touchés. Hors périmètre (SCOPE BOUNDARY), non corrigés.

## User Setup Required

Aucune — aucune configuration de service externe requise.

## Next Phase Readiness

**Prêt pour la suite de la vague 1 et la vague B :**

- **Plan 16-02** (magasin `LastExactStore` / EXA-01) : parallélisable, aucun fichier partagé n'a été touché.
- **Plan 16-03** (`JsonlEstimationProvider` → `TranscriptActivityProvider`) : le calibrateur qui consommait `JsonlEstimationProvider` **concret** en DI a disparu ; le seul enregistrement restant de ce type est celui de la chaîne composite. Le commentaire de `GatedOAuthUsageProvider` a été pré-nettoyé pour éviter de rouvrir ce fichier.
- **Plan 16-04** (suppression des 6 champs + `BudgetSource` + `DiagnosticService` + test DEL-06) : son objet est intact et vérifié — exactement 3 occurrences de `BudgetSource` dans `src/` (déclaration + 2 propriétés de `ChronosSettings`). C'est ce plan qui fera tomber le dernier commentaire `CAL-02` du dépôt.

**Aucun blocage.** Suite complète : 384 tests, 0 échec. Gardes `ServicesLayerPurityTests` (1 test) et `CompositionRootTests` (3 tests) vertes.

---
*Phase: 16-fondations-du-delta-persistance-d-molition-des-plafonds*
*Completed: 2026-09-09*

## Self-Check: PASSED

- 7 fichiers survivants vérifiés présents (dont `BudgetSource.cs`, volontairement conservé pour le plan 16-04).
- 10 fichiers vérifiés absents du disque (les 7 de production + les 3 de test du sous-système de plafonds).
- 3 commits vérifiés présents dans l'historique : `fc15400`, `062534e`, `e9efc47`.
- `dotnet build Chronos.sln` : 0 erreur. `dotnet test Chronos.sln` : 384 réussis, 0 échec.
