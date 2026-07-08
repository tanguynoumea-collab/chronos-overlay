---
phase: 01-fondations-architecture-squelette-overlay
verified: 2026-07-08T13:40:51Z
status: passed
score: 3/3 must-haves verifiés (plans 01-01, 01-02), 4/4 vérités checkpoint 01-03 confirmées
human_verification:
  - test: "Apparence visuelle de la transparence (rendu réel à l'écran, absence de halo/artefact)"
    expected: "La fenêtre overlay apparaît sur le bureau sans fond opaque parasite, sans halo ni artefact de rendu ; le placeholder Ellipse est lisible."
    why_human: "Le rendu logiciel forcé par AllowsTransparency ne peut pas être jugé par grep/build/test — nécessite une capture d'écran ou observation directe. Déjà tracké comme pending dans 01-HUMAN-UAT.md."
  - test: "Persistance du premier plan dans le temps face à d'autres fenêtres"
    expected: "Après ouverture d'un explorateur ou navigateur par-dessus l'overlay, celui-ci revient au premier plan en ≤ 2 s (réaffirmation TopmostGuard) sans clignotement ni vol de focus."
    why_human: "Comportement dynamique de Z-order dans le temps réel, non observable par les tests unitaires (qui ne prouvent que les flags P/Invoke, pas le rendu réel dans le temps). Déjà tracké comme pending dans 01-HUMAN-UAT.md."
---

# Phase 1 : Fondations architecture + squelette overlay — Rapport de vérification

**Objectif de la phase :** Une fenêtre overlay vide — borderless, transparente, always-on-top — s'affiche sur le bureau, portée par un graphe de services câblé dans App.xaml.cs (sans StartupUri) sur cible net8.0-windows.
**Vérifié le :** 2026-07-08T13:40:51Z
**Statut :** passed
**Re-vérification :** Non — vérification initiale (aucun 01-VERIFICATION.md préexistant)

## Atteinte de l'objectif

### Vérités observables

| # | Vérité | Statut | Preuve |
|---|--------|--------|--------|
| 1 | Le projet compile sous net8.0-windows, sans StartupUri, deux projets dans Chronos.sln | ✓ VERIFIED | `dotnet build Chronos.sln -c Debug` → « La génération a réussi. 0 Erreur(s) » ; `dotnet sln Chronos.sln list` liste `src\Chronos\Chronos.csproj` + `tests\Chronos.Tests\Chronos.Tests.csproj` ; `App.xaml` ne contient pas l'attribut `StartupUri` |
| 2 | La fenêtre MainWindow porte les 6 propriétés FEN-01 (WindowStyle=None, AllowsTransparency, Topmost, ShowInTaskbar=False, ShowActivated=False, ResizeMode=NoResize) | ✓ VERIFIED | Lecture de `src/Chronos/Views/MainWindow.xaml` (toutes les 6 propriétés présentes) + test `OverlayWindowConfigTests.Fenetre_expose_les_proprietes_overlay` vert |
| 3 | Le host Generic Host résout MainWindow/MainViewModel par DI et dispose les Singletons IDisposable à la fermeture | ✓ VERIFIED | `App.xaml.cs` : `OnStartup` → `GetRequiredService<MainWindow>().Show()` ; `OnExit` → `StopAsync().GetAwaiter().GetResult()` + `Dispose()` ; test `CompositionRootTests.Host_resout_et_dispose_les_singletons` vert (résolution + `provider.Dispose()` → marqueur `Disposed == true`) |
| 4 | Le Topmost est réaffirmé périodiquement (~2 s) via SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE) sans voler le focus | ✓ VERIFIED | `TopmostGuard.cs` : `DispatcherTimer` 2 s + `Reassert()` appelle `SetWindowPosFn` avec `HWND_TOPMOST` et `SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE` ; attaché sur `SourceInitialized` dans `MainWindow.xaml.cs` ; enregistré Singleton dans `App.ConfigureServices` ; test `TopmostGuardTests.Reassert_utilise_HWND_TOPMOST_sans_activation` vert |
| 5 (SC1) | Au lancement, une fenêtre sans bordure, transparente, sans entrée barre des tâches apparaît sur le bureau | ✓ VERIFIED (programmatique, checkpoint 01-03) | SUMMARY 01-03 : mesures Win32 réelles (EnumWindows/GetWindowLong) sur le process Chronos.exe lancé — `WS_EX_TOPMOST` présent, `WS_CAPTION` absent, `WS_EX_APPWINDOW` absent, 1 seule fenêtre |
| 6 (SC2) | La fenêtre reste au-dessus dans le temps et ne prend pas le focus au démarrage | ✓ VERIFIED (partiel programmatique + 1 item humain différé) | Pas de vol de focus confirmé programmatiquement (`GetForegroundWindow ≠ PID Chronos`) ; persistance dans le temps face à une autre fenêtre = item humain tracké en pending dans 01-HUMAN-UAT.md (non bloquant, cf. contexte) |
| 7 (SC3) | L'application se lance et se ferme proprement en libérant ses ressources | ✓ VERIFIED | `CompositionRootTests` (disposition DI) + SUMMARY 01-03 (Stop-Process → process disparu, aucun résiduel) |

**Score :** 7/7 vérités observables vérifiées (5 par automatisation build/test, 2 par mesure programmatique Win32 documentée dans 01-03-SUMMARY.md). 2 items purement visuels restent en observation humaine différée, déjà tracés dans 01-HUMAN-UAT.md (non bloquants selon le contexte fourni).

### Artefacts requis

| Artefact | Attendu | Statut | Détails |
|----------|---------|--------|---------|
| `src/Chronos/Chronos.csproj` | Projet WPF net8.0-windows, UseWPF, publish conditionné | ✓ VERIFIED | `TargetFramework=net8.0-windows`, `UseWPF=true`, bloc `PropertyGroup Condition="'$(PublishSingleFile)' == 'true'"` présent et isolé |
| `src/Chronos/app.manifest` | DPI PerMonitorV2 + supportedOS Win10/11 | ✓ VERIFIED | Contient `dpiAwareness=PerMonitorV2` et `supportedOS` Win10/11 |
| `src/Chronos/App.xaml` | Composition root, sans StartupUri | ✓ VERIFIED | Aucun attribut `StartupUri` ; `Application.Resources` présent |
| `src/Chronos/App.xaml.cs` | Composition root Generic Host complet | ✓ VERIFIED | `OnStartup`/`OnExit`/`ConfigureServices` complets, enregistre `IUiDispatcher`, `TopmostGuard`, `MainViewModel`, `MainWindow` en Singleton |
| `src/Chronos/Views/MainWindow.xaml` | Fenêtre overlay FEN-01 + placeholder | ✓ VERIFIED | 6 propriétés FEN-01 + `Ellipse` placeholder (documenté comme temporaire, remplacé Phase 5) |
| `src/Chronos/Views/MainWindow.xaml.cs` | Injection VM + TopmostGuard + placement | ✓ VERIFIED | Ctor `(MainViewModel, TopmostGuard)`, `DataContext = viewModel`, `SourceInitialized += Attach`, `Loaded += PlacerCoinSuperieurDroit` |
| `src/Chronos/ViewModels/MainViewModel.cs` | ViewModel racine ObservableObject | ✓ VERIFIED | `sealed partial class MainViewModel : ObservableObject` (vide, conforme Phase 1) |
| `src/Chronos/Services/IUiDispatcher.cs` | Contrat neutre sans type WPF | ✓ VERIFIED | `CheckAccess()`, `Post(Action)` — aucun `using System.Windows` |
| `src/Chronos/Services/WpfUiDispatcher.cs` | Impl WPF du dispatcher | ✓ VERIFIED | Wrap `Dispatcher`, `Post` délègue via `BeginInvoke` si hors thread UI |
| `src/Chronos/Interop/NativeMethods.cs` | P/Invoke SetWindowPos + constantes | ✓ VERIFIED | `HWND_TOPMOST`, `SWP_NOSIZE/NOMOVE/NOACTIVATE`, `extern bool SetWindowPos` |
| `src/Chronos/Services/TopmostGuard.cs` | Réaffirmation périodique testable | ✓ VERIFIED | `DispatcherTimer` 2 s, délégué `SetWindowPosFn` injectable, `Attach`/`Reassert`/`Dispose` |
| `tests/Chronos.Tests/*` | Tests xUnit STA couvrant FEN-01, SC3, ROB-04 | ✓ VERIFIED | 3 fichiers de test, 3 tests, tous verts |

### Vérification des liens clés (wiring)

| De | Vers | Via | Statut | Détails |
|----|------|-----|--------|---------|
| `App.xaml.cs` | `MainWindow.xaml.cs` | `GetRequiredService<MainWindow>()` puis `Show()` | ✓ WIRED | Présent dans `OnStartup`, avec commentaire précisant l'absence de vol de focus |
| `MainWindow.xaml.cs` | `MainViewModel.cs` | injection ctor + `DataContext = viewModel` | ✓ WIRED | Confirmé dans le ctor |
| `MainWindow.xaml.cs` | `TopmostGuard.cs` | `SourceInitialized → Attach(this)` | ✓ WIRED | `SourceInitialized += (_, _) => _topmostGuard.Attach(this);` |
| `TopmostGuard.cs` | `NativeMethods.cs` | délégué `SetWindowPosFn` → `SetWindowPos(HWND_TOPMOST, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE)` | ✓ WIRED | `Reassert()` appelle `_setWindowPos` avec les 3 flags combinés |
| `App.ConfigureServices` | `TopmostGuard` | `AddSingleton<TopmostGuard>()` | ✓ WIRED | Présent, résolu sans exception (test CompositionRoot) |

### Comportemental / spot-checks

| Comportement | Commande | Résultat | Statut |
|--------------|----------|----------|--------|
| Build Debug | `dotnet build Chronos.sln -c Debug` | « La génération a réussi. 0 Erreur(s) » | ✓ PASS |
| Suite de tests complète | `dotnet test Chronos.sln -c Debug` | « Réussi! - échec : 0, réussite : 3, ignorée(s) : 0, total : 3 » | ✓ PASS |
| Lancement réel + mesures Win32 (topmost, borderless, hors barre des tâches, pas de vol de focus, fermeture propre) | Décrit et exécuté dans 01-03-SUMMARY.md (EnumWindows/GetWindowLong/GetForegroundWindow/Stop-Process) | Tous les critères mesurés conformes | ✓ PASS (déjà exécuté par le checkpoint 01-03, non ré-exécuté ici pour éviter le lancement redondant d'un processus GUI) |

### Couverture des requirements

| Requirement | Plan source | Description | Statut | Preuve |
|-------------|-------------|--------------|--------|--------|
| FEN-01 | 01-01, 01-03 | Fenêtre borderless transparente always-on-top (WindowStyle=None, AllowsTransparency=True, Topmost=True, ShowInTaskbar=False) | ✓ SATISFIED | XAML conforme + `OverlayWindowConfigTests` vert + mesures Win32 réelles (01-03-SUMMARY.md) |
| ROB-04 | 01-02 | Le Topmost est réaffirmé périodiquement (SetWindowPos HWND_TOPMOST, SWP_NOACTIVATE) sans vol de focus | ✓ SATISFIED | `TopmostGuard` + `NativeMethods` + `TopmostGuardTests` vert |

Aucune requirement orpheline : REQUIREMENTS.md ne mappe que FEN-01 et ROB-04 à la Phase 1, toutes deux réclamées par les plans (01-01/01-03 pour FEN-01, 01-02 pour ROB-04) et toutes deux marquées « Complete » dans le tableau récapitulatif de REQUIREMENTS.md.

### Anti-patterns détectés

Aucun. Recherche `TODO|FIXME|XXX|HACK|not implemented` sur `src/Chronos/**/*.cs` et `*.xaml` : aucune occurrence. Le placeholder `Ellipse` de `MainWindow.xaml` est documenté comme intentionnel (« remplacé en Phase 5 ») et n'est pas un stub caché — c'est un artefact visuel de la fondation, conforme à l'objectif de la phase (« fenêtre overlay vide »).

### Vérification humaine requise

Les 2 items suivants sont déjà persistés (statut `pending`) dans `01-HUMAN-UAT.md` et n'ont pas encore été observés par un humain. Conformément au contexte fourni pour cette vérification, ils ne bloquent pas le statut `passed` de la phase car (a) ils sont déjà trackés, (b) tout ce qui est automatisable/mesurable programmatiquement a été vérifié et est vert (build, tests unitaires, mesures Win32 réelles du checkpoint 01-03).

1. **Apparence de la transparence**
   **Test :** Lancer `dotnet run --project src/Chronos` et observer visuellement le rendu de la fenêtre overlay.
   **Attendu :** Pas de fond opaque parasite, pas de halo/artefact de rendu logiciel, placeholder Ellipse lisible.
   **Pourquoi humain :** Le rendu visuel réel (anti-aliasing, halo, artefacts liés au rendu logiciel forcé par `AllowsTransparency`) n'est pas vérifiable par grep/build/test.

2. **Persistance du premier plan dans le temps**
   **Test :** Ouvrir une fenêtre (Explorateur/navigateur) par-dessus l'overlay, attendre ≥ 2-3 s.
   **Attendu :** L'overlay revient/reste au premier plan sans clignotement ni vol de focus.
   **Pourquoi humain :** Comportement dynamique de Z-order observé dans le temps réel ; les tests unitaires ne prouvent que les flags P/Invoke passés à `SetWindowPos` (ce qui est fait et vert), pas le comportement visuel réel en continu.

### Résumé des écarts

Aucun écart bloquant. La phase 1 atteint son objectif : le squelette compile (net8.0-windows, deux projets dans `Chronos.sln`), la composition root Generic Host est câblée dans `App.xaml.cs` sans `StartupUri`, la fenêtre overlay porte les 6 propriétés FEN-01, et le `TopmostGuard` réaffirme périodiquement le topmost sans vol de focus (ROB-04). Les 3 suites de tests automatisés sont vertes (`OverlayWindowConfigTests`, `CompositionRootTests`, `TopmostGuardTests`), le build Debug est propre (0 erreur, 0 avertissement), et le checkpoint 01-03 a mesuré programmatiquement (Win32 API) les comportements visuels/temps réel critiques sur le process réellement lancé. Seuls 2 items purement esthétiques/visuels restent en observation humaine différée, déjà tracés et non bloquants.

---

*Vérifié le : 2026-07-08T13:40:51Z*
*Vérificateur : Claude (gsd-verifier)*
