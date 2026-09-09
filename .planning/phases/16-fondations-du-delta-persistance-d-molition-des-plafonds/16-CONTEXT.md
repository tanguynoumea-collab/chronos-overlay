# Phase 16: Fondations du delta — persistance & démolition des plafonds - Context

**Gathered:** 2026-09-09
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Chronos dispose d'un **instant T de référence persistant** (le dernier relevé exact, sur disque avec son
horodatage, rechargé au démarrage) et d'une **source de delta bornée** (les transcripts JSONL répondent à
« activité depuis T ? » et « tokens depuis T ? »), le sous-système de plafonds ayant entièrement disparu du
code, des réglages et du menu.

Exigences couvertes : EXA-01 (persistance du dernier relevé exact), DEL-01 (activité depuis T),
DEL-02 (tokens depuis T), DEL-05 (démolition des plafonds), DEL-06 (migration des réglages sans casse).

**Hors périmètre de CETTE phase** (elle pose les fondations, elle ne les consomme pas) :
la refonte de la doctrine du composite (EXA-02/04/05, DEL-03/04) est la **phase 19**. Ici on livre les
briques ; on ne change pas encore la règle de choix de source ni l'affichage.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
Tous les choix d'implémentation sont à la discrétion de Claude — la phase de discussion est désactivée
(`workflow.skip_discuss=true`). S'appuyer sur le goal de la ROADMAP, les critères de succès et les
conventions de la base de code.

### Contraintes non négociables
- **Ne jamais présenter une estimation comme un chiffre exact** — Core Value du projet.
- MVVM strict, `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers Models/Views/ViewModels/Services.
- Aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`).
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin, aucune dépendance native.
- Lecture tolérante : fichier absent / corrompu → dégradation, jamais de crash, jamais de valeur inventée.
- `AppContext.BaseDirectory` ou `Environment.GetFolderPath`, **jamais** `Assembly.Location` (mono-fichier).
- UI et commentaires en **français**.
- **Baseline à l'entrée de la phase : 405 tests xUnit verts.** Ils doivent le rester.

### Migration des réglages (DEL-06) — exigence de non-régression
`%APPDATA%\Chronos\settings.json` en production contient aujourd'hui les six champs à supprimer :
`FiveHourTokenBudget`, `WeeklyTokenBudget`, `FiveHourBudgetSource`, `WeeklyBudgetSource`,
`FiveHourBudgetCalibratedAt`, `WeeklyBudgetCalibratedAt`. Le fichier réel contient AUSSI, et doit les
conserver intacts : `Corner`, `MonitorDeviceName`, `X`, `Y`, `Background`, `RefreshIntervalSeconds`,
`WeeklyAnchor`, `OAuthUsageEnabled`, `InnerStatusLineCommand`, `StatusLinePromptDismissed`, `ThemeKey`,
`SessionsWidgetEnabled`, `SessionsX`, `SessionsY`, `CadranMode`, `CadranStyle`, `SessionStyle`,
`VerticalLayout`. Un fichier portant les anciens champs doit s'ouvrir sans erreur : les champs obsolètes
sont ignorés, pas fatals.

</decisions>

<code_context>
## Existing Code Insights

### À supprimer (DEL-05)
- `src/Chronos/Services/BudgetCalibration.cs` — logique pure de déduction de plafond.
- `src/Chronos/Services/BudgetAutoCalibrator.cs` — service `IDisposable` abonné à
  `RefreshOrchestrator.SnapshotChanged`.
- `src/Chronos/Services/BudgetSource.cs` — enum `None/Manual/Auto`.
- `src/Chronos/Services/IBudgetPrompt.cs`, `src/Chronos/Views/BudgetPrompt.cs`.
- `src/Chronos/Views/BudgetDialog.xaml` + `.xaml.cs`, `src/Chronos/ViewModels/BudgetDialogViewModel.cs`.
- Les 6 champs de plafonds de `src/Chronos/Services/ChronosSettings.cs`.
- L'entrée de menu « Calibrer les plafonds… » et la `[RelayCommand]` associée dans `MainViewModel`.
- Les enregistrements DI dans `src/Chronos/App.xaml.cs` (`IBudgetPrompt`, `BudgetAutoCalibrator`, et la
  résolution forcée `_ = _host.Services.GetRequiredService<BudgetAutoCalibrator>()` avant `StartAsync`).
- `tests/Chronos.Tests/BudgetCalibrationTests.cs`, `tests/Chronos.Tests/BudgetAutoCalibratorTests.cs`,
  `tests/Chronos.Tests/Fakes/FakeBudgetPrompt.cs`, et toute assertion liée ailleurs
  (`SettingsServiceTests`, `MainViewModelTests`, `CompositionRootTests`).

### À transformer (DEL-01 / DEL-02)
`src/Chronos/Services/JsonlEstimationProvider.cs` — aujourd'hui un `IUsageProvider` qui calcule
`Utilization = tokens / budget` (lignes ~95 et ~120). Il doit cesser de produire une utilization absolue et
n'exposer que deux réponses bornées : « y a-t-il eu une ligne `assistant` depuis l'instant T ? » et
« combien de tokens depuis l'instant T ? ». Son parcours disque existant est bon et doit être conservé :
streaming tolérant, `FileShare.ReadWrite`, ligne partielle ignorée, filtre `mtime >= now - 8 j`,
`IsAssistant` (objet structuré `type=="assistant"` ET `message.role=="assistant"`, jamais une chaîne de
prose), `SumUsageTokens` (input + output + cache_creation + cache_read), filtrage des timestamps futurs.

**Attention aux consommateurs actuels** : `JsonlEstimationProvider` est injecté comme `IUsageProvider` dans
la chaîne composite d'`App.xaml.cs` ET comme source de tokens concrète du `BudgetAutoCalibrator`. Le second
disparaît ; le premier doit être traité sans casser le composite avant la phase 19. Une décision de
conception est nécessaire ici (le planner tranchera) : garder un `IUsageProvider` dégradé provisoire, ou
sortir le provider de la chaîne dès cette phase en laissant le composite se terminer sur « indisponible ».

### À créer (EXA-01)
Un magasin persistant du dernier `UsageSnapshot` exact, sous `%APPDATA%\Chronos\`. Motifs existants à
réutiliser : `ChronosPaths` (résolution de chemins), écriture atomique temp + `File.Move` (voir
`ArchiveStore`, `TreatedStore`, `StatusLineBridge`), `System.Text.Json` tolérant.
Aujourd'hui `ChronosOAuthUsageProvider._cached` et `ClaudeOAuthUsageProvider._cached` sont des champs
d'instance **en RAM seulement** — c'est la cause de la bascule en estimation à chaque redémarrage de l'exe.

### Modèles concernés
`src/Chronos/Models/UsageSnapshot.cs`, `WindowState.cs`, `SourceReliability.cs` (`Exact` / `Estimated` /
`Unavailable`), `WindowKind.cs`.

</code_context>

<specifics>
## Specific Ideas

Aucune exigence particulière — se référer à la description de la phase dans la ROADMAP et à ses 5 critères
de succès.

</specifics>

<deferred>
## Deferred Ideas

Aucune — la phase de discussion a été sautée.

</deferred>
