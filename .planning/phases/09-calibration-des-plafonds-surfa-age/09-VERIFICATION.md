---
phase: 09-calibration-des-plafonds-surfa-age
verified: 2026-07-09T06:21:44Z
status: passed
score: 15/15 must-haves verified (09-01: 6, 09-02: 5, 09-03: 4 dont 1 partagée)
---

# Phase 9 : Calibration des plafonds + surfaçage — Rapport de vérification

**Objectif de phase :** L'utilisateur calibre les plafonds de tokens (manuel + auto opportuniste) et
voit les tokens estimés, sans qu'une estimation ne soit jamais présentée comme exacte.
**Vérifié :** 2026-07-09T06:21:44Z
**Statut :** passed
**Re-vérification :** Non — vérification initiale

## Goal Achievement

### Observable Truths

| # | Vérité | Statut | Preuve |
|---|--------|--------|--------|
| 1 | `BudgetCalibration.Deduce` rend `tokens/util` arrondi, `null` si `util<=0` ou `tokens<=0` | ✓ VERIFIED | `src/Chronos/Services/BudgetCalibration.cs:12-15` ; 15 `[Fact]` dans `BudgetCalibrationTests.cs`, tous verts |
| 2 | `BudgetCalibration.ApplyAuto` n'écrit un plafond auto que si la source n'est pas Manual | ✓ VERIFIED | `BudgetCalibration.cs:20-27` (`ApplyAuto`) ; test `BudgetAutoCalibratorTests.Plafond_Manual_jamais_ecrase` vert |
| 3 | `TokenFormatter.Format` abrège en français (M/k, virgule, `,0` supprimé) | ✓ VERIFIED | `src/Chronos/Text/TokenFormatter.cs` ; 6 `[Fact]` dans `TokenFormatterTests.cs`, égalité de chaîne exacte, tous verts |
| 4 | `BudgetAutoCalibrator` inerte (aucun I/O) sans fenêtre Exact avec `Utilization>0` | ✓ VERIFIED | `BudgetAutoCalibrator.CalibrateAsync` (garde `if (!fiveExact && !weekExact) return;` avant tout accès) ; test `Aucune_fenetre_exacte_reste_inerte` utilise un `ThrowingProvider` qui lèverait si `GetAsync` était appelé — vert |
| 5 | Un plafond calibré ne change pas la Reliability (badge « estimée » conservé) — CAL-03 | ✓ VERIFIED | Régression dans `JsonlEstimationProviderTests.cs` (`SourceReliability.Estimated` malgré `FiveHourTokenBudget` défini) ; `BudgetCalibration`/`BudgetAutoCalibrator` ne touchent jamais `WindowState.Reliability` (seulement `ChronosSettings`) |
| 6 | `BudgetAutoCalibrator`/`BudgetCalibration` neutres (aucun type WPF) | ✓ VERIFIED | `ServicesLayerPurityTests.La_couche_Services_ne_reference_aucun_assembly_WPF` verte, aucune entrée d'allow-list requise pour ces deux types |
| 7 | Menu contextuel « Calibrer les plafonds… » ouvre un dialogue à 2 champs pré-remplis (vide = null) | ✓ VERIFIED | `MainWindow.xaml:28-29` (`MenuItem` lié à `CalibrateBudgetsCommand`) ; `BudgetDialog.xaml` (2 `TextBox` liés à `FiveHourText`/`WeeklyText`) ; `BudgetDialogViewModel` ctor pré-remplit depuis les valeurs courantes |
| 8 | `CalibrateBudgetsCommand` persiste via Load frais → with → Save (GAP-1), source=Manual pour les champs saisis | ✓ VERIFIED | `MainViewModel.CalibrateBudgets()` (2 `Load()` : un pour pré-remplir, un juste avant `Save` — GAP-1) ; test `CalibrateBudgets_n_ecrase_pas_les_reglages_persistes_par_un_autre_writer` prouve la non-régression GAP-1 |
| 9 | Après enregistrement, l'orchestrateur est re-déclenché (`RequestRefresh`) | ✓ VERIFIED | `RefreshOrchestrator.RequestRefresh()` (`RefreshOrchestrator.cs:96`) appelé en fin de `CalibrateBudgets()` |
| 10 | L'annulation du dialogue ne persiste rien | ✓ VERIFIED | `CalibrateBudgets()` : `if (sel is null) return;` avant toute écriture ; test `CalibrateBudgets_annule_ne_persiste_rien` vert |
| 11 | Le conteneur DI résout `IBudgetPrompt` et instancie `BudgetAutoCalibrator` (abonné) au démarrage | ✓ VERIFIED | `App.xaml.cs:72,99-103,29` (résolution eager avant `StartAsync`) ; `CompositionRootTests.Host_resout_et_dispose_les_singletons` vert |
| 12 | En Estimated avec tokens>0, `WindowGaugeViewModel` expose `TokensText`/`HasTokens=true` | ✓ VERIFIED | `WindowGaugeViewModel.Apply` (`WindowGaugeViewModel.cs:42-45`) ; test `Estimated_avec_tokens_expose_HasTokens_et_TokensText_abrege` vert (« ≈ 62,5 M tokens ») |
| 13 | En Exact ou sans tokens, `HasTokens=false` et aucun texte | ✓ VERIFIED | Tests `Exact_sans_tokens_n_affiche_aucun_texte_de_tokens` et `Estimated_avec_zero_token_ne_surface_rien`, tous verts |
| 14 | Le cadran affiche les tokens en texte secondaire discret (#A9A8B2), visible seulement si HasTokens | ✓ VERIFIED | `MainWindow.xaml:84-88,104-108` (2 `TextBlock` `TokensCinqHeures`/`TokensHebdo`, `FontSize=9`, `Foreground=TexteSecondaire`, `Visibility` via `BoolToVis` sur `HasTokens`) |
| 15 | Suite complète verte (150 tests attendus) + build 0 erreur | ✓ VERIFIED | `dotnet build` : 0 avertissement/0 erreur ; `dotnet test` : **150/150 réussis**, 0 échec |

**Score : 15/15 vérités vérifiées**

### Required Artifacts

| Artefact | Attendu | Statut | Détails |
|----------|---------|--------|---------|
| `src/Chronos/Services/BudgetSource.cs` | Enum neutre `BudgetSource {None,Manual,Auto}` | ✓ VERIFIED | Présent, documenté, neutre |
| `src/Chronos/Services/BudgetCalibration.cs` | `Deduce` + `ApplyAuto` purs | ✓ VERIFIED | Présent, aucune dépendance I/O/WPF |
| `src/Chronos/Services/BudgetAutoCalibrator.cs` | Service neutre écoutant `SnapshotChanged` | ✓ VERIFIED | Présent, `IDisposable`, abonnement ctor / désabonnement `Dispose` |
| `src/Chronos/Text/TokenFormatter.cs` | Formateur pur fr abrégé | ✓ VERIFIED | Présent, déterministe (InvariantCulture) |
| `tests/Chronos.Tests/BudgetCalibrationTests.cs` | Couverture Deduce + priorité | ✓ VERIFIED | 15 `[Fact]`, tous verts |
| `src/Chronos/Services/IBudgetPrompt.cs` | Contrat neutre + `BudgetSelection` | ✓ VERIFIED | Présent, neutre |
| `src/Chronos/Views/BudgetDialog.xaml`/`.cs` + `BudgetPrompt.cs` | Dialogue WPF miroir de `RecalibrationPrompt` | ✓ VERIFIED | Présents, pattern respecté (DialogResult piloté par `CloseRequested`) |
| `src/Chronos/ViewModels/BudgetDialogViewModel.cs` | VM du dialogue (parse texte→long?) | ✓ VERIFIED | Présent, `Parse` pur, `CloseRequested` |
| `src/Chronos/ViewModels/MainViewModel.cs` | `CalibrateBudgetsCommand` | ✓ VERIFIED | Présent, GAP-1 respecté, `RequestRefresh` appelé |
| `src/Chronos/Views/MainWindow.xaml` | `MenuItem` + `TextBlock`s tokens | ✓ VERIFIED | `MenuItem` (CAL-01) + 2 `TextBlock` tokens (NET-02) présents |
| `src/Chronos/ViewModels/WindowGaugeViewModel.cs` | `TokensText`/`HasTokens` | ✓ VERIFIED | Dérivés dans `Apply`, testés |

### Key Link Verification

| From | To | Via | Statut | Détails |
|------|-----|-----|--------|---------|
| `BudgetAutoCalibrator.cs` | `RefreshOrchestrator.SnapshotChanged` | `orchestrator.SnapshotChanged += OnSnapshot` (ctor) / `-= OnSnapshot` (Dispose) | ✓ WIRED | Ligne 28 et 60 |
| `BudgetAutoCalibrator.cs` | `BudgetCalibration.Deduce`/`ApplyAuto` | Appel après `settings.Load()` frais | ✓ WIRED | Lignes 49-55 |
| `MainWindow.xaml` | `MainViewModel.CalibrateBudgetsCommand` | Command binding du `MenuItem` | ✓ WIRED | Ligne 29 |
| `MainViewModel.cs` | `SettingsService.Load/Save` + `RefreshOrchestrator.RequestRefresh` | Load frais → with → Save → RequestRefresh | ✓ WIRED | `CalibrateBudgets()` lignes 143-162 |
| `App.xaml.cs` | `BudgetAutoCalibrator` | `AddSingleton` + résolution eager avant `StartAsync` | ✓ WIRED | Lignes 29, 99-103 |
| `MainWindow.xaml` | `WindowGaugeViewModel.TokensText`/`HasTokens` | Text binding + Visibility via `BoolToVis` | ✓ WIRED | Lignes 84-88, 104-108 |
| `WindowGaugeViewModel.cs` | `TokenFormatter.Format` | Appel dans `Apply` quand Estimated + tokens>0 | ✓ WIRED | Ligne 45 |

### Data-Flow Trace (Level 4)

| Artefact | Variable | Source | Données réelles | Statut |
|----------|----------|--------|------------------|--------|
| `WindowGaugeViewModel.TokensText` | `TokensText`/`HasTokens` | `WindowState.EstimatedTokens` (issu du pipeline JSONL réel via `JsonlEstimationProvider`) | Oui — somme réelle des tokens JSONL, pas de valeur statique | ✓ FLOWING |
| `MainWindow.xaml` badges tokens | `FiveHour`/`SevenDay` sous-VM | `MainViewModel.ApplySnapshot` → `FiveHour.Apply(snap.FiveHour)` | Oui — alimenté par le vrai `UsageSnapshot` de l'orchestrateur | ✓ FLOWING |
| `BudgetDialog` valeurs pré-remplies | `courant.FiveHourTokenBudget`/`WeeklyTokenBudget` | `_settingsService.Load()` (disque réel) | Oui | ✓ FLOWING |

### Behavioral Spot-Checks

| Comportement | Commande | Résultat | Statut |
|--------------|----------|----------|--------|
| Build complet | `dotnet build Chronos.sln -c Debug` | 0 avertissement, 0 erreur | ✓ PASS |
| Suite de tests complète | `dotnet test Chronos.sln -c Debug` | 150/150 réussis, 0 échec | ✓ PASS |
| Tests ciblés Phase 9 | `dotnet test --filter "BudgetCalibration\|TokenFormatter\|BudgetAutoCalibrator\|CalibrateBudgets\|CadranBinding\|CompositionRoot\|ServicesLayerPurity"` | 36/36 réussis | ✓ PASS |
| Lancement de l'app (smoke UI) | non exécuté (nécessite un écran réel, cf. 09-HUMAN-UAT.md) | — | ? SKIP (routé vers UAT humain) |

### Requirements Coverage

| Requirement | Plan source | Description | Statut | Preuve |
|-------------|-------------|--------------|--------|--------|
| CAL-01 | 09-02 | Plafonds persistés + réglables via menu contextuel | ✓ SATISFIED (code) — ⚠️ REQUIREMENTS.md non coché | `MenuItem` + dialogue + `CalibrateBudgetsCommand` + tests, tous vérifiés ; voir note ci-dessous |
| CAL-02 | 09-01, 09-02 | Calibration auto opportuniste, jamais écrasée par un manuel plus récent | ✓ SATISFIED | `BudgetAutoCalibrator` + `BudgetCalibration.ApplyAuto` + câblage DI, tous vérifiés |
| CAL-03 | 09-01 | Plafond calibré ne supprime jamais le badge « estimée » | ✓ SATISFIED | Régression `JsonlEstimationProviderTests`, `BudgetCalibration`/`BudgetAutoCalibrator` ne touchent jamais `Reliability` |
| NET-02 | 09-01, 09-03 | Tokens estimés surfacés en texte secondaire discret | ✓ SATISFIED | `TokenFormatter` + `WindowGaugeViewModel.TokensText`/`HasTokens` + `TextBlock`s XAML, tous vérifiés |

**Note documentaire (non-bloquante) :** `.planning/REQUIREMENTS.md` a la case CAL-01 non cochée
(`- [ ] **CAL-01**`) et la ligne de traçabilité indique « Pending » alors que le code l'implémente
intégralement (menu, dialogue, persistance GAP-1, RequestRefresh — tous vérifiés ci-dessus) et que
`09-02-SUMMARY.md` déclare `requirements-completed: [CAL-01, CAL-02]`. Il s'agit d'un écart de mise à
jour de REQUIREMENTS.md (probablement omis lors de la finalisation par l'orchestrateur après
l'interruption API de l'exécuteur sur 09-02), PAS d'un écart de code. À corriger par une simple mise à
jour du document (case à cocher + statut "Complete") — ne bloque pas le statut de la phase.

### Anti-Patterns Found

Aucun `TODO`/`FIXME`/`PLACEHOLDER`/implémentation vide détecté dans les fichiers de la Phase 9
(`BudgetCalibration.cs`, `BudgetAutoCalibrator.cs`, `BudgetSource.cs`, `TokenFormatter.cs`,
`IBudgetPrompt.cs`, `BudgetPrompt.cs`, `BudgetDialog.xaml.cs`, `BudgetDialogViewModel.cs`,
`MainViewModel.cs`, `WindowGaugeViewModel.cs`). Aucune valeur codée en dur masquant une absence de
données (les TextBlocks tokens sont bindés à des sous-VM réels alimentés par le pipeline JSONL).

| Fichier | Ligne | Pattern | Sévérité | Impact |
|---------|-------|---------|----------|--------|
| `.planning/REQUIREMENTS.md` | 28, 60 | Case CAL-01 non cochée malgré implémentation complète | ℹ️ Info | Documentaire seulement, cf. note ci-dessus |

### Human Verification Required

Voir `.planning/phases/09-calibration-des-plafonds-surfa-age/09-HUMAN-UAT.md` (9 critères : ouverture/
pré-remplissage/persistance/annulation du dialogue, coloration immédiate de l'arc, conservation du
badge « estimée », lisibilité et discrétion du texte de tokens). Ces items sont trackés dans le fichier
UAT dédié et ne bloquent pas le statut `passed` de cette vérification (aucun automatisme WPF ne peut
remplacer l'observation d'un écran réel).

### Gaps Summary

Aucun gap de code détecté. Les 15 vérités observables dérivées des `must_haves` des 3 plans (09-01,
09-02, 09-03) sont toutes vérifiées avec preuve directe dans le code et les tests. Build 0 erreur,
suite complète 150/150 verte (conforme à l'attendu), `ServicesLayerPurityTests` verte. Les 3 commits
du plan 09-02 (`9dc439c`, `71bcf64`, `b493d3d`) sont tous présents dans l'historique git et leur
contenu correspond exactement à ce que déclare le SUMMARY finalisé par l'orchestrateur — aucune
divergence trouvée malgré l'interruption API de l'exécuteur.

Seul écart relevé : `.planning/REQUIREMENTS.md` n'a pas coché CAL-01 (documentaire, non-bloquant, à
corriger lors de la clôture de milestone).

---

*Vérifié : 2026-07-09T06:21:44Z*
*Vérificateur : Claude (gsd-verifier)*
