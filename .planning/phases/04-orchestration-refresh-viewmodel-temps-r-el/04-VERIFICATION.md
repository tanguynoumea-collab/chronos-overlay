---
phase: 04-orchestration-refresh-viewmodel-temps-r-el
verified: 2026-07-08T16:00:08Z
status: passed
score: 3/3 must-haves verified (Success Criteria ROADMAP)
---

# Phase 4 : Orchestration refresh + ViewModel temps réel — Rapport de vérification

**Objectif de phase :** Les données se rafraîchissent automatiquement (deux horloges distinctes) et alimentent un ViewModel qui interpole l'affichage à la seconde, tout franchissement de thread passant par un point de marshaling unique.
**Vérifié :** 2026-07-08T16:00:08Z
**Statut :** passed
**Re-vérification :** Non — vérification initiale

## Goal Achievement

### Observable Truths (Success Criteria ROADMAP)

| # | Vérité | Statut | Preuve |
|---|--------|--------|--------|
| 1 | Une écriture sur une source déclenche une relecture débouncée ; un timer périodique garantit la fraîcheur en filet de sécurité (Error géré) | ✓ VERIFIED | `RefreshOrchestrator.cs` : `FileSystemWatcher` (Changed/Created/Renamed) → `Trigger()` → `Channel<bool>` ; `PeriodicTimer` indépendant (`RunPeriodicAsync`) ; `w.Error += OnError` → `RecreateWatcher()` (dispose + recrée + rescan). Tests `RefreshOrchestratorTests.cs` : 5/5 verts (périodique, écriture réelle usage.json, coalescence de rafale ≤2 malgré 20 déclencheurs, Error→recréation, SnapshotChanged émis) |
| 2 | Countdown/arcs progressent chaque seconde par interpolation, sans I/O | ✓ VERIFIED | `MainViewModel.Interpolate(now)` et `WindowGaugeViewModel.Interpolate(now)` sont purs (aucun appel `GetAsync`/IUsageProvider). `DispatcherTimer` 1 s créé dans `StartClock()` (côté UI, hors ctor). Test `Interpolate_recalcule_sans_aucun_IO_au_tick` : `FractionRemaining` décroît, `CountdownText` change, `provider.GetCount == 0` après interpolation |
| 3 | Updates de fond → UI via IUiDispatcher, sans InvalidOperationException | ✓ VERIFIED | `MainViewModel.OnSnapshotChanged` = unique point d'appel `_ui.Post(...)` (grep confirmé, un seul site dans tout le fichier). Test `Snapshot_pousse_hors_thread_UI_est_marshale_une_seule_fois` : `ui.PostCount == 1` avec `OnUiThread = false`. `App.xaml.cs` résout `MainViewModel` AVANT `_host.StartAsync()` (ordre corrigé, Pitfall 3) |

**Score :** 3/3 vérités vérifiées

### Artefacts requis

| Artefact | Attendu | Statut | Détails |
|----------|---------|--------|---------|
| `src/Chronos/Services/RefreshOrchestrator.cs` | BackgroundService neutre : watcher + PeriodicTimer + Channel, event SnapshotChanged | ✓ VERIFIED | 117 lignes, `class RefreshOrchestrator : BackgroundService`, `Channel.CreateBounded` + `DropWrite` présents, `Renamed +=` et `Error +=` présents, zéro `System.Windows` |
| `src/Chronos/Services/RefreshOptions.cs` | Record d'options (PeriodicInterval, Debounce) + Default | ✓ VERIFIED | Existe, `PeriodicInterval=60s`/`Debounce=300ms` (confirmé via SUMMARY + usage dans DI) |
| `src/Chronos/ViewModels/MainViewModel.cs` | Abonnement orchestrateur + marshaling + Interpolate + StartClock | ✓ VERIFIED | 61 lignes, ctor `(RefreshOrchestrator, IUiDispatcher, IClock)`, `SnapshotChanged +=`, `_ui.Post`, `Interpolate`, `StartClock` (DispatcherTimer hors ctor) |
| `src/Chronos/ViewModels/WindowGaugeViewModel.cs` | Sous-VM par fenêtre : fraction, utilization, countdown, provenance | ✓ VERIFIED | `Apply(WindowState)` + `Interpolate(now)` purs, `IsEstimated` pré-câblé DAT-08 |
| `src/Chronos/Text/CountdownFormatter.cs` | Formatage FR pur d'un TimeSpan | ✓ VERIFIED | 4 cas couverts (`3 j 14 h` / `2 h 05` / `45 min` / `0 min`), aucune CultureInfo |
| `src/Chronos/App.xaml.cs` | DI orchestrateur + hostedservice même instance + pré-résolution VM avant StartAsync | ✓ VERIFIED | `AddSingleton<RefreshOrchestrator>()` + `AddHostedService(sp => sp.GetRequiredService<RefreshOrchestrator>())` (même instance) ; `GetRequiredService<MainViewModel>()` avant `StartAsync` |
| `src/Chronos/Views/MainWindow.xaml.cs` | Démarrage StartClock au chargement | ✓ VERIFIED | `Loaded += (_, _) => viewModel.StartClock();` (second handler, coexiste avec placement) |
| `tests/Chronos.Tests/RefreshOrchestratorTests.cs` | Tests RAF-01/RAF-02 | ✓ VERIFIED | 5 tests, tous verts |
| `tests/Chronos.Tests/MainViewModelTests.cs` | Tests RAF-03/RAF-04 | ✓ VERIFIED | 4 tests, tous verts |
| `tests/Chronos.Tests/CountdownFormatterTests.cs` | Tests formatage FR | ✓ VERIFIED | présent, suite verte |

### Vérification des liens clés (wiring)

| De | Vers | Via | Statut | Détails |
|----|------|-----|--------|---------|
| `RefreshOrchestrator.ExecuteAsync` | `IUsageProvider.GetAsync` | boucle consommateur unique | ✓ WIRED | `var snap = await _provider.GetAsync(stoppingToken);` dans la boucle `ReadAllAsync` uniquement (aucun appel dans un handler watcher/timer) |
| `FileSystemWatcher` + `PeriodicTimer` | boucle consommateur | `Channel<bool>` capacité 1 DropWrite | ✓ WIRED | `Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite })` |
| `MainViewModel` | `RefreshOrchestrator.SnapshotChanged` | abonnement ctor | ✓ WIRED | `orchestrator.SnapshotChanged += OnSnapshotChanged;` |
| `OnSnapshotChanged` (thread pool) | `ApplySnapshot` (thread UI) | `IUiDispatcher.Post` | ✓ WIRED | `_ui.Post(() => ApplySnapshot(snap));` — point unique |
| `App.xaml.cs` | `MainViewModel` (abonnement) | `GetRequiredService` avant `StartAsync` | ✓ WIRED | Ordre confirmé lignes 20-27 d'App.xaml.cs |
| `DispatcherTimer` 1s (`StartClock`) | `Interpolate(now)` | Tick côté UI | ✓ WIRED | `timer.Tick += (_, _) => Interpolate(_clock.UtcNow);` créé dans `StartClock()`, appelé depuis `MainWindow.Loaded` |

### Data-Flow Trace (Level 4)

| Artefact | Variable de données | Source | Données réelles | Statut |
|----------|---------------------|--------|------------------|--------|
| `MainViewModel.FiveHour/SevenDay` | `WindowGaugeViewModel` (FractionRemaining, CountdownText) | `RefreshOrchestrator.SnapshotChanged` → `CompositeUsageProvider.GetAsync` (Phase 3, primaire+repli réels) | Oui — `CompositeUsageProvider` interroge le pont réel `usage.json` puis repli JSONL (pas de retour statique vide) | ✓ FLOWING |
| `MainViewModel.DataUnavailable/IsStale` | `snap.SourceCapturedAt`, `Reliability` | `ApplySnapshot(snap)` alimenté par le même snapshot réel | Oui | ✓ FLOWING |

### Behavioral Spot-Checks

| Comportement | Commande | Résultat | Statut |
|--------------|----------|----------|--------|
| Build solution | `dotnet build Chronos.sln -c Debug` | 0 avertissement, 0 erreur | ✓ PASS |
| Suite de tests complète | `dotnet test Chronos.sln -c Debug` | 41/41 réussis, 0 échec | ✓ PASS |
| Smoke run exécutable | Lancement `Chronos.exe` puis attente ~8s, vérification process vivant, arrêt propre | Process PID 26688 vivant après 8s (mémoire stable ~157-160 Mo), aucun crash, terminé proprement via `taskkill /F` | ✓ PASS |

### Requirements Coverage

| Requirement | Plan source | Description | Statut | Preuve |
|-------------|-------------|-------------|--------|--------|
| RAF-01 | 04-01 | FileSystemWatcher débouncé déclenche la relecture sur écriture des sources | ✓ SATISFIED | Watcher Changed/Created/Renamed + Error→recréation, coalescence Channel(1, DropWrite), tests verts |
| RAF-02 | 04-01 | PeriodicTimer relit les données à intervalle configurable (filet de sécurité) | ✓ SATISFIED | `RunPeriodicAsync` indépendant du watcher, `RefreshOptions.Default.PeriodicInterval = 60s`, test vert |
| RAF-03 | 04-02 | DispatcherTimer 1s interpole arcs et compte à rebours à partir du dernier snapshot, sans I/O | ✓ SATISFIED | `Interpolate(now)` pur (0 GetAsync au tick), `StartClock()` crée le DispatcherTimer côté UI |
| RAF-04 | 04-02 | Tout franchissement thread pool → UI passe par un point de marshaling unique (IUiDispatcher) | ✓ SATISFIED | Unique site `_ui.Post` dans `OnSnapshotChanged`, `PostCount == 1` prouvé par test, ordre DI corrigé (Pitfall 3) |

Aucune requirement orpheline détectée pour la Phase 4 dans REQUIREMENTS.md (RAF-01 à RAF-04 toutes couvertes par les plans 04-01/04-02).

### Anti-Patterns Found

Aucun anti-pattern bloquant détecté dans les fichiers de la phase (RefreshOrchestrator.cs, RefreshOptions.cs, MainViewModel.cs, WindowGaugeViewModel.cs, CountdownFormatter.cs, App.xaml.cs, MainWindow.xaml.cs). Le seul match "Placeholder" (dans `MainWindow.xaml` ligne 8) est un commentaire XAML explicitement documenté comme "empreinte du cadran, remplacé en Phase 5" — hors périmètre de la Phase 4 (le cadran visuel est le livrable de la Phase 5), non un stub des fonctionnalités RAF-01 à RAF-04.

### Human Verification Required

Aucun élément bloquant nécessitant une vérification humaine pour le statut de la phase. Les vérifications visuelles temps réel (compte à rebours qui décroît à l'écran, arcs qui progressent) ne sont pas observables sans le cadran XAML complet (livrable Phase 5) ; elles sont trackées dans `04-HUMAN-UAT.md` conformément à la consigne, sans bloquer le statut `passed`.

### Gaps Summary

Aucun gap. Les trois Success Criteria du ROADMAP sont vérifiés par le code réel (pas seulement par les affirmations du SUMMARY) : build propre, 41/41 tests verts (incluant coalescence de rafale, Error→recréation, interpolation pure sans I/O, marshaling unique), smoke run de l'exécutable stable ~8s sans crash, et wiring de bout en bout tracé (watcher/timer → Channel → GetAsync → SnapshotChanged → IUiDispatcher.Post → ApplySnapshot/Interpolate → DispatcherTimer 1s). Le code correspond fidèlement aux `must_haves` déclarés dans les frontmatters des deux plans (04-01, 04-02).

---

*Vérifié : 2026-07-08T16:00:08Z*
*Vérificateur : Claude (gsd-verifier)*
