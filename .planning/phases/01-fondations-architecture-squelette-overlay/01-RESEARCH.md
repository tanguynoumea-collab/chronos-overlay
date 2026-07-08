# Phase 1 : Fondations architecture + squelette overlay — Research

**Researched:** 2026-07-08
**Domain:** Bootstrap d'app WPF/.NET 8 — solution + csproj, composition root Generic Host, fenêtre overlay borderless/transparente/topmost, réaffirmation Topmost par P/Invoke
**Confidence:** HIGH (pattern Generic Host WPF vérifié doc officielle Microsoft datée 2026-03-30 ; P/Invoke SetWindowPos stable et vérifié ; environnement local sondé)

> **Cadrage.** La recherche projet (`.planning/research/STACK.md`, `ARCHITECTURE.md`, `PITFALLS.md`) couvre déjà le csproj exact, les packages, la composition root de principe et les pièges transparence/topmost. Ce document ne les réécrit pas — il **comble les manques concrets pour planifier la Phase 1** :
> 1. structure de solution physique (sln + arborescence de fichiers à créer) ;
> 2. **séquence exacte** du cycle de vie Generic Host dans `App` (build → StartAsync → resolve MainWindow → Show ; StopAsync → Dispose) ;
> 3. **P/Invoke exact** `SetWindowPos` + constantes + timing (DispatcherTimer dédié) pour ROB-04 ;
> 4. `ShowActivated`/gestion du focus ;
> 5. **architecture de validation** observable de chaque success criterion.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions (déjà verrouillées — PROJECT.md + recherche)
- TFM `net8.0-windows` **obligatoire** (net8.0 seul ne compile pas WPF) ; SDK .NET 10 installé compile cette cible.
- Packages : CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting / DependencyInjection ligne 8.0.x.
- Composition root explicite dans `App.OnStartup` (**retirer `StartupUri`**), providers/services en **Singleton**, disposés dans `OnExit`.
- Fenêtre : `WindowStyle=None`, `AllowsTransparency=True`, `Topmost=True`, `ShowInTaskbar=False`, `Background` transparent.
- ROB-04 : réaffirmation périodique du Topmost via `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE)` — **sans vol de focus**.
- Structure dossiers : Models / Views / ViewModels / Services. MVVM strict, `[ObservableProperty]` / `[RelayCommand]`.
- Commentaires et UI **en français**.
- Pas d'animation continue / blur / shadow (`AllowsTransparency` force le rendu logiciel).

### Claude's Discretion
Tous les autres choix d'implémentation sont à la discrétion de Claude — phase infrastructure, guidée par le goal ROADMAP, les success criteria et les conventions du CLAUDE.md.

### Specific Ideas (à honorer)
- csproj correct **dès le départ** : `net8.0-windows`, `UseWPF`, propriétés de publish **conditionnées** (jamais dans le PropertyGroup inconditionnel).
- La fenêtre vide doit déjà respecter **la taille/forme carrée** prévue du cadran (régions transparentes) pour que la Phase 5 s'y pose sans retouche.
- Prévoir l'abstraction `IUiDispatcher` **dès cette phase** (contrat minimal acceptable) pour que la couche Services ne référence jamais de type WPF.

### Deferred Ideas (OUT OF SCOPE)
None — discuss phase skipped. (Rappel roadmap : drag/snap/menu/persistance = Phase 6 ; cadran/arcs = Phase 5 ; providers = Phases 2-3 ; packaging = Phase 7. **Ne rien anticiper de ces phases.**)
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support (ce qui permet l'implémentation) |
|----|-------------|---------------------------------------------------|
| **FEN-01** | Fenêtre borderless transparente always-on-top (`WindowStyle=None`, `AllowsTransparency=True`, `Topmost=True`, `ShowInTaskbar=False`) | Config XAML complète (§ Pattern 3) + `ShowActivated=False` + `ResizeMode=NoResize` + fenêtre carrée avec placeholder visible ; app.manifest PerMonitorV2 (§ Code Examples) |
| **ROB-04** | Topmost réaffirmé périodiquement (`SetWindowPos HWND_TOPMOST, SWP_NOACTIVATE`) sans vol de focus | P/Invoke exact + constantes + `TopmostGuard` sur `DispatcherTimer` dédié + délégué injectable pour testabilité (§ Pattern 4, § Code Examples) |

**Non couverts par cette phase (ne pas déborder) :** toute la logique données, le cadran, les comportements de fenêtre (drag/snap/menu), le packaging. La Phase 1 livre un **squelette câblé** + **une fenêtre vide conforme**.
</phase_requirements>

---

## Project Constraints (from CLAUDE.md)

Directives actionnables extraites de `./CLAUDE.md` que le planner **doit** faire respecter :

- **MVVM strict** : `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers `Models/Views/ViewModels/Services`.
- **Rendu XAML pur** (Path/ArcSegment), **aucune dépendance native** (pas de SkiaSharp). *(Sans objet Phase 1 — pas de rendu d'arc encore, mais ne rien introduire qui viole ce principe.)*
- **Chemins sous profil utilisateur uniquement**, aucun droit admin. *(Phase 1 n'écrit rien sur disque ; ne pas introduire de chemin absolu machine.)*
- **UI et commentaires en français.**
- **`utilization`/`resets_at` prioritaires ; ne jamais présenter une estimation comme exacte.** *(Sans objet Phase 1.)*
- **Activer les skills `frontend-design` + `windows-wpf` sur les tâches UI.** Le planner doit taguer la tâche « fenêtre overlay » comme tâche UI et référencer ces skills.
- Entrée par workflow GSD (pas d'édit hors `/gsd:execute-phase`).

Aucune de ces directives n'entre en conflit avec les recommandations ci-dessous.

---

## Summary

La Phase 1 est un **bootstrap** : créer la solution, un unique projet WPF `net8.0-windows`, câbler un **Generic Host** comme composition root dans `App`, et afficher une fenêtre overlay vide conforme (borderless, transparente, topmost, hors barre des tâches, sans vol de focus), dont le Topmost est réaffirmé périodiquement.

Le seul vrai travail technique « non trivial » de la phase est **la séquence de cycle de vie du Host** (démarrer/arrêter/disposer proprement autour du thread UI WPF) et **le P/Invoke de réaffirmation Topmost**. Les deux sont désormais tranchés ici avec du code vérifié : le pattern Generic Host + WPF est **documenté officiellement par Microsoft** (article mis à jour le 2026-03-30) — `Host.CreateApplicationBuilder()` → `builder.Build()` → `await _host.StartAsync()` → `GetRequiredService<MainWindow>()` → `Show()`, et à la sortie `using (_host) { await _host.StopAsync(); }`. Le P/Invoke `SetWindowPos` est une API Win32 stable.

L'environnement local est **prêt** : SDK .NET 10.0.201 installé, `Microsoft.WindowsDesktop.App` 8.0.25 présent → `net8.0-windows` compile et `dotnet run` fonctionne sans publish.

**Primary recommendation :** un seul projet WPF sous `src/Chronos/`, composition root = **Generic Host** dans `App` (override `OnStartup`/`OnExit`, `StartupUri` retiré), `MainWindow`+`MainViewModel` en Singleton injectés, fenêtre carrée `ShowActivated=False`, et un service `TopmostGuard` (DispatcherTimer dédié + `SetWindowPos` derrière un délégué injectable pour rendre ROB-04 testable).

---

## Standard Stack

Le stack est **imposé et déjà figé** dans `.planning/research/STACK.md` (autoritatif — ne pas rejouer). Rappel des seuls éléments **installés en Phase 1** :

### Core (projet `Chronos`)
| Package | Version | Rôle en Phase 1 | Vérifié |
|---------|---------|-----------------|---------|
| (SDK) `Microsoft.NET.Sdk` + `UseWPF` | net8.0-windows | Projet WPF | SDK 10.0.201 + WindowsDesktop 8.0.25 présents localement |
| CommunityToolkit.Mvvm | 8.4.2 | `ObservableObject` de base pour `MainViewModel` (même vide) | STACK.md (NuGet 25/03/2026) |
| Microsoft.Extensions.Hosting | 8.0.x (dernier patch) | **Composition root** : Generic Host, DI, cycle de vie | Doc officielle WPF+Host 2026-03-30 |

> `Microsoft.Extensions.Hosting` **tire transitivement** `Microsoft.Extensions.DependencyInjection` — pas besoin de l'ajouter séparément (le faire ne nuit pas, mais rester aligné 8.0.x). `Microsoft.Extensions.Configuration.Json` **non requis** en Phase 1 (aucune config à charger encore).

### Supporting (projet de test — nouveau, voir Validation Architecture)
| Package | Version | Rôle |
|---------|---------|------|
| Microsoft.NET.Test.Sdk | dernier stable | hôte de test |
| xUnit + xunit.runner.visualstudio | dernier stable | framework de test |
| Xunit.StaFact | dernier stable | `[WpfFact]`/`[StaFact]` — indispensable pour instancier un `Window`/`DispatcherTimer` WPF (affinité STA) dans un test |

**Vérification de version au restore (obligatoire) :**
```bash
dotnet add src/Chronos package CommunityToolkit.Mvvm --version 8.4.2
dotnet add src/Chronos package Microsoft.Extensions.Hosting          # prendre le dernier 8.0.x
# tests :
dotnet add tests/Chronos.Tests package Xunit.StaFact                 # confirmer la dernière version au restore
```
> Xunit.StaFact : version exacte à confirmer au `restore` (MEDIUM — package communautaire largement utilisé d'A. Arnott, non vérifié en direct ici).

---

## Architecture Patterns

### Structure de solution physique (à créer)

Décision : **un seul projet applicatif** + un projet de test, sous une solution. La disposition `src/` + `tests/` est retenue parce que la Phase 1 introduit un projet de test (Validation Architecture) ; garder appli et tests séparés est propre et n'impose rien pour la suite. L'arborescence **interne** de `src/Chronos/` reprend exactement celle décrite dans `ARCHITECTURE.md`.

```
PROJET OVERLAY/                      (racine du dépôt = cwd)
├── Chronos.sln
├── .gitignore                       # bin/ obj/ etc. (gabarit VisualStudio)
├── src/
│   └── Chronos/
│       ├── Chronos.csproj           # net8.0-windows, UseWPF, publish conditionné
│       ├── app.manifest             # DPI PerMonitorV2 + supportedOS Win10+
│       ├── App.xaml                 # ressources globales, PAS de StartupUri
│       ├── App.xaml.cs              # COMPOSITION ROOT : Generic Host
│       ├── Models/                  # (vide en Phase 1 — .gitkeep ou 1er record en Phase 3)
│       ├── Services/
│       │   ├── IUiDispatcher.cs     # abstraction marshaling UI (contrat minimal)
│       │   ├── WpfUiDispatcher.cs   # impl WPF (wrap Dispatcher)
│       │   └── TopmostGuard.cs      # ROB-04 : réaffirmation périodique
│       ├── Interop/
│       │   └── NativeMethods.cs     # P/Invoke SetWindowPos + constantes
│       ├── ViewModels/
│       │   └── MainViewModel.cs     # ObservableObject (quasi vide en Phase 1)
│       ├── Views/
│       │   ├── MainWindow.xaml      # overlay borderless/transparent/topmost
│       │   └── MainWindow.xaml.cs   # code-behind minimal (attache TopmostGuard)
│       ├── Controls/                # (vide — Phase 5)
│       └── Converters/              # (vide — Phase 5)
└── tests/
    └── Chronos.Tests/
        ├── Chronos.Tests.csproj     # net8.0-windows, référence src/Chronos
        ├── CompositionRootTests.cs
        ├── OverlayWindowConfigTests.cs
        └── TopmostGuardTests.cs
```

> **Réconciliation avec ARCHITECTURE.md :** l'arbre de ce dernier (`Chronos/App.xaml…`) décrit le **contenu de `src/Chronos/`**. `docs/data-sources.md` y figure comme livrable **Phase 2**, pas Phase 1 — ne pas le créer maintenant.
>
> **Alternative acceptable :** projet unique à la racine sans `src/`/`tests/` (ce que suggère l'arbre brut d'ARCHITECTURE). Le split `src`+`tests` est préféré uniquement parce qu'un projet de test arrive dès cette phase. Si le planner préfère la simplicité maximale, un dossier `Chronos/` + `Chronos.Tests/` à la racine est équivalent.

### Pattern 1 : Composition root = Generic Host dans `App` (séquence exacte)

**What :** Un unique point de câblage. On retire `StartupUri`, on construit un `IHost` (qui **possède** le `IServiceProvider` + le cycle de vie + le dispose), on démarre le host, on résout `MainWindow` par DI, on l'affiche. À la sortie, on arrête et dispose le host.

**Pourquoi le Host complet (et pas un `ServiceCollection` nu) :** décision verrouillée (package `Microsoft.Extensions.Hosting`) + les Phases 4-5 enregistreront `RefreshOrchestrator` en `IHostedService` (watcher/PeriodicTimer pilotés par `StartAsync`/`StopAsync` du host). Poser le Host dès la Phase 1 évite un refactor. En Phase 1 il n'y a **pas encore** de `IHostedService` métier — c'est normal, le host tourne « à vide ».

**Séquence de vie (autoritative, doc Microsoft 2026-03-30) :**

```
OnStartup:
  1. base.OnStartup(e)
  2. builder = Host.CreateApplicationBuilder()
  3. builder.Services.Add… (enregistrer le graphe — voir ci-dessous)
  4. _host = builder.Build()
  5. await _host.StartAsync()                          ← démarre les IHostedService (aucun en P1)
  6. window = _host.Services.GetRequiredService<MainWindow>()
  7. window.Show()                                     ← NE PAS Activate ; ShowActivated=False côté XAML

OnExit:
  1. using (_host) { await _host.StopAsync(); }        ← StopAsync puis Dispose (dispose les Singletons IDisposable)
  2. base.OnExit(e)
```

**Enregistrements DI (Phase 1) :**
```csharp
services.AddSingleton<IUiDispatcher>(_ => new WpfUiDispatcher(Application.Current.Dispatcher));
services.AddSingleton<TopmostGuard>();
services.AddSingleton<MainViewModel>();
services.AddSingleton<MainWindow>();
```
> `MainWindow` et `MainViewModel` en **Singleton** (fenêtre unique). `MainViewModel` est injecté dans le ctor de `MainWindow` qui fixe `DataContext = vm` (MVVM). Voir Code Examples.

**Gotcha `async void` (à documenter dans la tâche) :** `OnStartup`/`OnExit` surchargés sont `void` → on les écrit `async void`. C'est **le pattern officiel**. Conséquence : dans `OnExit`, WPF **n'attend pas** la `Task` retournée — si `StopAsync` faisait un vrai travail asynchrone long, le process pourrait se terminer avant la fin du dispose. **En Phase 1 le dispose est synchrone et instantané → sans risque.** Pour une garantie déterministe (utile pour le success criterion 3), une variante robuste consiste à bloquer : `_host.StopAsync().GetAwaiter().GetResult(); _host.Dispose();` dans `OnExit`. Recommandé ici pour rendre la disposition **observable et déterministe**.

### Pattern 2 : `IUiDispatcher` — abstraction de marshaling (contrat minimal Phase 1)

**What :** Découpler la couche Services de `System.Windows.Threading.Dispatcher`. Posé dès la Phase 1 (contrat minimal), consommé réellement par le ViewModel en Phases 4-6. En Phase 1 il est **enregistré et instanciable** mais peu appelé — c'est voulu (préparer la frontière de thread).

**Contrat minimal :**
```csharp
public interface IUiDispatcher
{
    bool CheckAccess();          // suis-je déjà sur le thread UI ?
    void Post(Action action);    // exécuter sur le thread UI (async, non bloquant)
}
```
> `Post` implémente le pattern `if (CheckAccess()) action(); else BeginInvoke(action);` (voir ARCHITECTURE.md Pattern 4). Suffisant pour la phase. Élargir (`InvokeAsync<T>`) plus tard si besoin — YAGNI ici.

### Pattern 3 : Fenêtre overlay conforme (FEN-01)

**What :** `MainWindow` borderless, transparente, topmost, hors barre des tâches, **non activée au démarrage**, **carrée** (empreinte du futur cadran).

Combinaison **obligatoire** (l'oubli d'un membre casse ou dénature l'overlay) :

| Propriété | Valeur | Raison |
|-----------|--------|--------|
| `WindowStyle` | `None` | Pré-requis de `AllowsTransparency=True` (sinon exception au chargement) ; supprime le chrome |
| `AllowsTransparency` | `True` | Transparence par pixel (fond du bureau visible autour du cadran) |
| `Background` | `Transparent` | Fond transparent — **mais** garde le hit-test (Transparent capte, `null` laisse passer) |
| `Topmost` | `True` | État initial always-on-top (réaffirmé par ROB-04) |
| `ShowInTaskbar` | `False` | Pas d'entrée barre des tâches (success criterion 1) |
| `ShowActivated` | `False` | **Ne prend pas le focus au `Show()`** (success criterion 2) |
| `ResizeMode` | `NoResize` | Overlay non redimensionnable ; pas de poignées |
| `SizeToContent` | `Manual` | Taille fixe carrée |
| `Width` / `Height` | ex. `220` × `220` | Carré (empreinte cadran ; valeur affinée en Phase 5) |
| `WindowStartupLocation` | `Manual` | Positionnement maîtrisé ; snap/persistance = Phase 6 |

**Placeholder visible.** Une fenêtre 100 % transparente serait *invisible* → le success criterion 1 (« une fenêtre apparaît ») ne serait pas observable. Ajouter un **visuel placeholder** : un `Border`/`Ellipse` semi-opaque centré (ex. cercle sombre `#CC1E1E1E`) qui matérialise l'empreinte du cadran. Il sera remplacé par le vrai cadran en Phase 5. **Ne pas** ajouter d'ombre/blur/animation (`AllowsTransparency` force le rendu logiciel).

### Pattern 4 : Réaffirmation Topmost par `SetWindowPos` (ROB-04)

**What :** `Topmost=True` n'est **pas** un état garanti dans le temps (plein écran exclusif, réagencement du Z-order, sessions longues le font retomber — cf. PITFALLS.md #3). On **réaffirme** périodiquement la bande topmost via `SetWindowPos(hwnd, HWND_TOPMOST, 0,0,0,0, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE)` — sans bouger, sans redimensionner, **sans activer** (pas de vol de focus).

**Décisions de conception :**
- **Timer dédié** : un `DispatcherTimer` propre au `TopmostGuard` (thread UI, donc accès HWND sûr), intervalle **~1–3 s**. Volontairement **distinct** du futur `DispatcherTimer` d'interpolation UI 1 s (Phases 4-5) : responsabilités séparées, la Phase 1 ne doit pas préempter le tick de rendu. Recommandation : **2 s** (compromis réactivité / coût négligeable).
- **HWND** obtenu via `new WindowInteropHelper(window).Handle`. Le handle n'existe **qu'après** création de la fenêtre → attacher le guard dans `MainWindow.SourceInitialized` (ou après `Show()`), pas dans le ctor. Utiliser `EnsureHandle()` si on attache avant affichage.
- **Ne pas** utiliser le toggle `Topmost=false; Topmost=true` : il peut provoquer un micro-scintillement et une réactivation ; `SetWindowPos` + `SWP_NOACTIVATE` est plus propre et explicitement « sans focus ».
- **Testabilité (ROB-04) :** injecter la fonction native derrière un délégué (`SetWindowPosFn`) par défaut = `NativeMethods.SetWindowPos`. Un test substitue un faux qui **capture les flags** et vérifie `hWndInsertAfter == HWND_TOPMOST` et `flags == SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE`. Sans cette indirection, ROB-04 n'est vérifiable que visuellement.

### Anti-Patterns à éviter (spécifiques Phase 1)
- **`StartupUri` laissé dans App.xaml** en plus du Host → double instanciation de fenêtre / MainWindow non injectée. Le retirer est une **décision verrouillée**.
- **Instancier `MainWindow` avec `new`** au lieu de `GetRequiredService` → contourne la DI, casse l'injection du ViewModel.
- **Toucher le HWND dans le ctor de la fenêtre** → handle nul. Attacher `TopmostGuard` sur `SourceInitialized`.
- **P/Invoke `SetWindowPos` inlined en `private static extern` dans la fenêtre** → non testable. Le mettre dans `Interop/NativeMethods.cs` et l'injecter via délégué.
- **`Background=null` sur la racine** → clics traversent, la fenêtre paraît absente. Utiliser `Transparent`.
- Réintroduire une propriété de publish (`PublishSingleFile`, `SelfContained`) dans un `PropertyGroup` inconditionnel → build/debug lents. **Conditionner** (cf. STACK.md). Le packaging est **Phase 7** — ne pas l'anticiper au-delà d'un csproj correctement structuré.

---

## Don't Hand-Roll

| Problème | Ne pas coder à la main | Utiliser | Pourquoi |
|----------|------------------------|----------|----------|
| Conteneur DI + cycle de vie | Un mini-container / `Dictionary<Type,object>` | `Microsoft.Extensions.Hosting` (Generic Host) | Décision verrouillée ; gère dispose ordonné, IHostedService (futur), config, logging |
| `INotifyPropertyChanged` | Événements PropertyChanged manuels | CommunityToolkit.Mvvm `ObservableObject` / `[ObservableProperty]` | Générateurs de source, zéro boilerplate ; décision verrouillée |
| Marshaling UI | `Application.Current.Dispatcher` dispersé | `IUiDispatcher` (Pattern 2) | Testabilité + frontière de thread unique (ARCHITECTURE.md) |
| DPI awareness | Code de scaling manuel | `app.manifest` PerMonitorV2 | Standard Windows ; net multi-écrans (cf. STACK.md) |
| Timer UI | `System.Threading.Timer` + marshaling | `DispatcherTimer` | Déjà sur le thread UI → accès HWND/propriétés sûr, pas de marshaling |
| Handle de fenêtre WPF | `Process`/`FindWindow` | `WindowInteropHelper(window).Handle` | API WPF officielle pour le HWND |

**Key insight :** la Phase 1 ne doit **rien** inventer — tout est du câblage de briques standard. Le seul code « bas niveau » est le P/Invoke `SetWindowPos`, qui est un idiome Win32 stable (ci-dessous), pas une invention.

---

## Common Pitfalls

### Pitfall 1 : HWND nul si `TopmostGuard` s'attache trop tôt
**Ce qui se passe :** `WindowInteropHelper(window).Handle` renvoie `IntPtr.Zero` tant que la fenêtre n'a pas de source Win32.
**Éviter :** attacher le guard dans `MainWindow.SourceInitialized`, ou appeler `helper.EnsureHandle()`. Vérifier `handle != IntPtr.Zero` avant le premier `SetWindowPos`.

### Pitfall 2 : `async void OnExit` termine avant le dispose
**Ce qui se passe :** WPF n'attend pas la `Task` d'un handler `async void` → un `StopAsync` réellement asynchrone pourrait ne pas finir. (Success criterion 3 = « libère ses ressources ».)
**Éviter :** en Phase 1, bloquer explicitement : `_host.StopAsync().GetAwaiter().GetResult(); _host.Dispose();`. Rendre la disposition observable (ex. un service `IDisposable` marqueur dont on vérifie le `Dispose` en test).

### Pitfall 3 : `AllowsTransparency=True` sans `WindowStyle=None`
**Ce qui se passe :** exception à l'initialisation de la fenêtre.
**Éviter :** toujours le trio `WindowStyle=None` + `AllowsTransparency=True` + `Background=Transparent` ensemble (cf. STACK.md).

### Pitfall 4 : la fenêtre vole le focus au démarrage
**Ce qui se passe :** `Show()` active la fenêtre → l'overlay prend le focus, apparaît dans le flux d'activation. Viole le success criterion 2.
**Éviter :** `ShowActivated="False"` en XAML **et** `SWP_NOACTIVATE` sur chaque réaffirmation. (Ne PAS utiliser `WS_EX_NOACTIVATE`/click-through en Phase 1 : cela empêcherait l'interaction future menu/drag — c'est du ressort v2, cf. REQUIREMENTS V2-04.)

### Pitfall 5 : overlay invisible (fenêtre 100 % transparente)
**Ce qui se passe :** sans visuel, « la fenêtre apparaît » n'est pas observable → faux échec de validation.
**Éviter :** placeholder semi-opaque centré (Pattern 3), remplacé par le cadran en Phase 5.

### Pitfall 6 : plein écran exclusif tiers gagne toujours
**Ce qui se passe :** un jeu/vidéo plein écran exclusif passera devant malgré la réaffirmation — **limite Windows, pas un bug**.
**Éviter :** documenter cette limite ; ne pas boucler agressivement pour « lutter ». L'intervalle 2 s suffit pour le cas courant (retomber derrière après une session longue / bascule fenêtrée).

---

## Code Examples

Tous vérifiés / dérivés de sources HIGH. Commentaires en français (convention projet).

### `Chronos.csproj` (extrait Phase 1 — publish conditionné, non déclenché en build/debug)
```xml
<!-- Source : STACK.md (autoritatif) — réduit ici au périmètre Phase 1 -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>   <!-- OBLIGATOIRE pour WPF -->
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <RootNamespace>Chronos</RootNamespace>
    <AssemblyName>Chronos</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>

  <!-- Propriétés de PUBLICATION uniquement (Phase 7) — jamais actives en build/debug -->
  <PropertyGroup Condition="'$(PublishSingleFile)' == 'true'">
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
    <PublishTrimmed>false</PublishTrimmed>   <!-- WPF non trim-safe -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.1" /> <!-- dernier 8.0.x au restore -->
  </ItemGroup>
</Project>
```

### `app.manifest` (DPI PerMonitorV2)
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware> <!-- repli anciens OS -->
    </windowsSettings>
  </application>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/> <!-- Windows 10/11 -->
    </application>
  </compatibility>
</assembly>
```

### `App.xaml` (StartupUri retiré)
```xml
<Application x:Class="Chronos.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Aucun StartupUri : la fenêtre est résolue par la composition root. -->
    <Application.Resources/>
</Application>
```

### `App.xaml.cs` (composition root — cycle de vie Generic Host)
```csharp
// Source : doc Microsoft « Use the .NET Generic Host in a WPF app » (2026-03-30),
// adaptée aux overrides OnStartup/OnExit exigés par la décision verrouillée.
using System.Windows;
using Chronos.Services;
using Chronos.ViewModels;
using Chronos.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Chronos;

public partial class App : Application
{
    private IHost _host = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();
        ConfigureServices(builder.Services);
        _host = builder.Build();

        await _host.StartAsync();                    // démarre les IHostedService (aucun en Phase 1)

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();                               // ShowActivated=False (XAML) → pas de vol de focus
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Blocage volontaire : dispose déterministe des Singletons IDisposable
        // (timers, guard). Évite le piège async-void qui n'attend pas StopAsync.
        _host.StopAsync().GetAwaiter().GetResult();
        _host.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IUiDispatcher>(_ => new WpfUiDispatcher(Current.Dispatcher));
        services.AddSingleton<TopmostGuard>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
```

### `Services/IUiDispatcher.cs` + `WpfUiDispatcher.cs`
```csharp
namespace Chronos.Services;

/// <summary>Point unique de franchissement vers le thread UI (testable, sans type WPF côté Services).</summary>
public interface IUiDispatcher
{
    bool CheckAccess();
    void Post(Action action);
}
```
```csharp
using System.Windows.Threading;

namespace Chronos.Services;

public sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;
    public WpfUiDispatcher(Dispatcher dispatcher) => _dispatcher = dispatcher;

    public bool CheckAccess() => _dispatcher.CheckAccess();

    public void Post(Action action)
    {
        if (_dispatcher.CheckAccess()) action();     // déjà sur le thread UI : exécuter directement
        else _dispatcher.BeginInvoke(action);        // sinon : reposter (non bloquant)
    }
}
```

### `Interop/NativeMethods.cs` (P/Invoke exact)
```csharp
using System;
using System.Runtime.InteropServices;

namespace Chronos.Interop;

internal static class NativeMethods
{
    // hWndInsertAfter : -1 = HWND_TOPMOST (place/maintient dans la bande topmost)
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    public const uint SWP_NOSIZE     = 0x0001;  // ne pas redimensionner
    public const uint SWP_NOMOVE     = 0x0002;  // ne pas déplacer
    public const uint SWP_NOACTIVATE = 0x0010;  // NE PAS activer → aucun vol de focus

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);
}
```
> Note : `LibraryImport` (générateur de source) est l'alternative moderne, mais WPF n'étant ni AOT ni trimmé, `DllImport` est parfaitement adapté et plus simple ici.

### `Services/TopmostGuard.cs` (ROB-04 — DispatcherTimer dédié + délégué injectable)
```csharp
using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Chronos.Interop;

namespace Chronos.Services;

/// <summary>Réaffirme périodiquement la bande topmost sans voler le focus (ROB-04).</summary>
public sealed class TopmostGuard : IDisposable
{
    // Délégué injectable → rend le comportement (flags) vérifiable en test.
    public delegate bool SetWindowPosFn(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    private readonly SetWindowPosFn _setWindowPos;
    private readonly DispatcherTimer _timer;
    private IntPtr _hwnd;

    public TopmostGuard(SetWindowPosFn? setWindowPos = null)
    {
        _setWindowPos = setWindowPos ?? NativeMethods.SetWindowPos;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Reassert();
    }

    /// <summary>À appeler quand la fenêtre a un HWND (SourceInitialized ou après Show).</summary>
    public void Attach(Window window)
    {
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        Reassert();          // une première réaffirmation immédiate
        _timer.Start();
    }

    public void Reassert()
    {
        if (_hwnd == IntPtr.Zero) return;
        _setWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    public void Dispose() => _timer.Stop();
}
```

### `Views/MainWindow.xaml` (overlay conforme + placeholder)
```xml
<Window x:Class="Chronos.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        Topmost="True" ShowInTaskbar="False" ShowActivated="False"
        ResizeMode="NoResize" SizeToContent="Manual"
        Width="220" Height="220" WindowStartupLocation="Manual">
    <!-- Placeholder visible de l'empreinte du cadran (remplacé en Phase 5).
         Pas d'ombre/blur/animation : AllowsTransparency force le rendu logiciel. -->
    <Grid>
        <Ellipse Width="200" Height="200" Fill="#CC1E1E1E"/>
    </Grid>
</Window>
```

### `Views/MainWindow.xaml.cs` (attache le guard sur SourceInitialized)
```csharp
using System.Windows;
using Chronos.Services;
using Chronos.ViewModels;

namespace Chronos.Views;

public partial class MainWindow : Window
{
    private readonly TopmostGuard _topmostGuard;

    public MainWindow(MainViewModel viewModel, TopmostGuard topmostGuard)
    {
        InitializeComponent();
        DataContext = viewModel;          // MVVM : la vue reçoit son VM par injection
        _topmostGuard = topmostGuard;
        SourceInitialized += (_, _) => _topmostGuard.Attach(this);  // HWND garanti ici
    }
}
```

### `ViewModels/MainViewModel.cs` (quasi vide en Phase 1)
```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronos.ViewModels;

/// <summary>ViewModel racine. Vide en Phase 1 (le pipeline données arrive en Phases 3-4).</summary>
public sealed partial class MainViewModel : ObservableObject
{
}
```

---

## State of the Art

| Ancienne approche | Approche actuelle (retenue) | Depuis | Impact |
|-------------------|-----------------------------|--------|--------|
| `Host.CreateDefaultBuilder()` + `ConfigureServices` (chaînage) | `Host.CreateApplicationBuilder()` (builder plat, `.Services`) | .NET 8 | API recommandée par la doc WPF officielle 2026 ; plus simple |
| `StartupUri` dans App.xaml | Composition root : résolution manuelle de `MainWindow` par DI | — | Requis pour injecter le ViewModel ; décision verrouillée |
| Toggle `Topmost=false/true` | `SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)` | — | Réaffirmation sans scintillement ni activation |
| `[return: MarshalAs]` `DllImport` | (option) `LibraryImport` source-generated | .NET 7+ | Non nécessaire ici (WPF pas AOT/trim) — `DllImport` reste correct |

**Rien de déprécié à éviter** dans ce périmètre. `Host.CreateDefaultBuilder` reste valide mais `CreateApplicationBuilder` est l'idiome courant .NET 8.

---

## Environment Availability

Sondé localement le 2026-07-08.

| Dépendance | Requise par | Disponible | Version | Repli |
|------------|-------------|------------|---------|-------|
| .NET SDK | build/debug de `net8.0-windows` | ✓ | 10.0.201 | — |
| `Microsoft.WindowsDesktop.App` (runtime WPF) | exécuter WPF en debug (`dotnet run`) | ✓ | 8.0.25 (+ 8.0.0/8.0.21/10.x) | — |
| `Microsoft.NETCore.App` 8.x | runtime cible net8 | ✓ | 8.0.25 | — |
| Session desktop Windows interactive | observer la fenêtre (validation visuelle) | ✓ (Windows 11) | — | — |

**Dépendances manquantes sans repli :** aucune.
**Dépendances manquantes avec repli :** aucune.

> Conséquence : `dotnet build` et `dotnet run` fonctionnent **sans publish/self-contained**. Le packaging mono-fichier (`Microsoft.WindowsDesktop.App` embarqué) est un sujet **Phase 7**, hors périmètre ici.

---

## Validation Architecture

> `workflow.nyquist_validation = true` → section incluse.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit + **Xunit.StaFact** (`[WpfFact]` pour l'affinité STA WPF) |
| Config file | aucun encore — **projet `tests/Chronos.Tests` à créer en Wave 0** |
| Quick run command | `dotnet test tests/Chronos.Tests --filter "Category=fast"` (ou par nom, ci-dessous) |
| Full suite command | `dotnet test Chronos.sln` |
| Build gate (préalable) | `dotnet build Chronos.sln -c Debug` doit réussir |

**Nature de la phase :** trois success criteria dont **deux sont partiellement visuels** (apparence, Z-order dans le temps). On automatise ce qui est déterministe (propriétés de fenêtre, flags P/Invoke, résolution + disposition DI) et on couvre le reste par un **smoke manuel court et scripté**. Honnêteté : l'observation « reste au-dessus après des heures / après une vidéo plein écran » **n'est pas** automatisable de façon fiable → manuel assumé.

### Phase Requirements → Test Map
| Réf | Comportement | Type | Commande automatisée | Fichier existe ? |
|-----|--------------|------|----------------------|------------------|
| FEN-01 | `MainWindow` a `WindowStyle=None`, `AllowsTransparency`, `Topmost`, `ShowInTaskbar=False`, `ShowActivated=False` | unit (WpfFact/STA) | `dotnet test --filter FullyQualifiedName~OverlayWindowConfig` | ❌ Wave 0 |
| FEN-01 | La fenêtre apparaît, borderless, visible (placeholder), sans entrée barre des tâches | manuel | *checklist smoke* | n/a |
| ROB-04 | `TopmostGuard.Reassert()` appelle `SetWindowPos` avec `HWND_TOPMOST` + `SWP_NOMOVE\|SWP_NOSIZE\|SWP_NOACTIVATE` | unit (délégué faux) | `dotnet test --filter FullyQualifiedName~TopmostGuard` | ❌ Wave 0 |
| ROB-04 | La fenêtre reste au-dessus dans le temps, sans voler le focus | manuel | *checklist smoke* | n/a |
| Succès #3 | Le host résout `MainWindow`+`MainViewModel` sans exception ; `Dispose` du host dispose les Singletons `IDisposable` | unit (STA) | `dotnet test --filter FullyQualifiedName~CompositionRoot` | ❌ Wave 0 |
| Succès #3 | L'app se lance et se ferme proprement | manuel | *checklist smoke* | n/a |

**Détails des tests automatisables :**
- **OverlayWindowConfigTests** `[WpfFact]` : `new MainWindow(new MainViewModel(), new TopmostGuard(fake))` sur thread STA, puis `Assert` sur chaque propriété. (Xunit.StaFact fournit le contexte STA + une boucle de dispatcher.)
- **TopmostGuardTests** `[WpfFact]` : injecter un `SetWindowPosFn` faux capturant `(after, flags)`, appeler `Reassert()` avec un HWND non nul simulé (via `Attach` sur une vraie `Window` STA, ou en exposant un setter interne pour le test), vérifier `after == HWND_TOPMOST` et `flags == 0x13` (`NOMOVE|NOSIZE|NOACTIVATE`).
- **CompositionRootTests** : reproduire `ConfigureServices` dans un `ServiceCollection` de test (STA pour construire la `Window`), `BuildServiceProvider()`, `GetRequiredService<MainWindow>()` et `<MainViewModel>()` sans exception ; enregistrer un `IDisposable` marqueur, `provider.Dispose()`, vérifier qu'il est disposé (preuve du criterion 3).

### Sampling Rate
- **Per task commit :** `dotnet build Chronos.sln -c Debug` + `dotnet test tests/Chronos.Tests --filter FullyQualifiedName~<ciblé>`
- **Per wave merge :** `dotnet test Chronos.sln` (suite complète)
- **Phase gate :** suite verte **+ checklist smoke manuelle cochée** avant `/gsd:verify-work`.

### Smoke manuel (success criteria visuels) — checklist
1. `dotnet run --project src/Chronos` → une fenêtre carrée sombre (placeholder) apparaît sur le bureau, **sans bordure**, **sans entrée dans la barre des tâches** (vérifier la barre + Alt+Tab). *(Succès #1)*
2. Cliquer dans une autre application au lancement : l'overlay **ne prend pas le focus** au démarrage ; l'app active reste active. *(Succès #2)*
3. Ouvrir une fenêtre/vidéo par-dessus, attendre, revenir : l'overlay est **toujours au premier plan** (réaffirmation 2 s). *(Succès #2)*
4. Fermer l'app : arrêt propre, aucun processus résiduel (`TopmostGuard`/host disposés). *(Succès #3)*

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/Chronos.Tests.csproj` — projet net8.0-windows, référence `src/Chronos`, packages `Microsoft.NET.Test.Sdk` + `xunit` + `xunit.runner.visualstudio` + `Xunit.StaFact`.
- [ ] `tests/Chronos.Tests/OverlayWindowConfigTests.cs` — couvre FEN-01 (propriétés).
- [ ] `tests/Chronos.Tests/TopmostGuardTests.cs` — couvre ROB-04 (flags P/Invoke).
- [ ] `tests/Chronos.Tests/CompositionRootTests.cs` — couvre le criterion #3 (résolution + disposition).
- [ ] `Chronos.sln` — ajouter les deux projets.
- [ ] Concevoir `TopmostGuard`/composition avec **indirections testables** (délégué `SetWindowPosFn`, `IDisposable` marqueur) — sinon FEN-01/ROB-04/criterion #3 ne sont pas automatisables.

---

## Open Questions

1. **Position initiale de la fenêtre en Phase 1**
   - Ce qu'on sait : snap/persistance = Phase 6 ; il faut néanmoins un placement de départ observable.
   - Ce qui est flou : coin par défaut vs centre.
   - Recommandation : placement fixe simple (ex. coin supérieur droit calculé sur `SystemParameters.WorkArea`, ou centre écran). Ne rien persister. Choix laissé à la discrétion Claude — non bloquant.

2. **`src/`+`tests/` vs projet unique à la racine**
   - Ce qu'on sait : les deux fonctionnent ; ARCHITECTURE.md décrit un arbre sans `src/`.
   - Recommandation : `src/`+`tests/` (retenu) pour isoler le projet de test dès cette phase. Si le planner préfère la simplicité, `Chronos/`+`Chronos.Tests/` à la racine est équivalent — trancher au plan.

3. **Intervalle de réaffirmation Topmost**
   - Ce qu'on sait : « périodique, léger, sans focus ». 1–3 s est un bon ordre de grandeur.
   - Recommandation : **2 s**. Ajustable ; non bloquant. Ne pas descendre trop bas (coût inutile), ni trop haut (retombée visible plus longue).

4. **Xunit.StaFact — version exacte**
   - Ce qu'on sait : package communautaire répandu pour tests WPF/STA.
   - Recommandation : confirmer la dernière version au `restore` ; à défaut, tester manuellement via la checklist smoke (les tests UI automatisés sont un bonus, pas un bloquant de la phase).

---

## Sources

### Primary (HIGH confidence)
- **Microsoft Learn — « Use the .NET Generic Host in a WPF app »** (mis à jour 2026-03-30) — https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/how-to-use-host-builder — séquence exacte `CreateApplicationBuilder`/`StartAsync`/`GetRequiredService<MainWindow>`/`Show` ; `using(_host){ await StopAsync(); }` ; retrait de `StartupUri`.
- **Microsoft Learn — `SetWindowPos` (winuser.h)** — https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos — sémantique `HWND_TOPMOST`, `SWP_NOACTIVATE`.
- **pinvoke.net — SetWindowPos (user32)** — https://pinvoke.net/default.aspx/user32/SetWindowPos.html — signature P/Invoke + valeurs des constantes (`SWP_NOSIZE 0x1`, `SWP_NOMOVE 0x2`, `SWP_NOACTIVATE 0x10`, `HWND_TOPMOST -1`).
- **Sondage environnement local** — `dotnet --list-sdks` (10.0.201) / `--list-runtimes` (`Microsoft.WindowsDesktop.App` 8.0.25).
- **Recherche projet** — `.planning/research/STACK.md`, `ARCHITECTURE.md`, `PITFALLS.md` (csproj, composition root de principe, pièges transparence/topmost) — HIGH.

### Secondary (MEDIUM confidence)
- **badecho.com — « Running WPF Applications with a Generic Host »** (2025-10-25) — https://badecho.com/index.php/2025/10/25/wpf-with-generic-host/ — confirme le pattern (secondaire).
- **Xunit.StaFact** (A. Arnott) — `[WpfFact]`/`[StaFact]` pour tests STA WPF — version à confirmer au restore.

### Tertiary (LOW confidence)
- Discussions communautaires SetWindowPos/topmost (windowsforum, blogs) — convergentes, cohérentes avec la doc officielle ; non déterminantes.

---

## Metadata

**Confidence breakdown :**
- Standard stack : HIGH — déjà figé (STACK.md) + environnement sondé.
- Cycle de vie Generic Host : HIGH — doc Microsoft officielle datée 2026-03-30.
- P/Invoke SetWindowPos / ROB-04 : HIGH — API Win32 stable, constantes vérifiées.
- Config fenêtre / focus : HIGH — propriétés WPF documentées, cohérentes avec PITFALLS.md.
- Validation automatisée WPF (Xunit.StaFact) : MEDIUM — approche établie, version à confirmer ; les criteria visuels restent en smoke manuel assumé.

**Research date :** 2026-07-08
**Valid until :** ~2026-08-07 (stack stable ; re-vérifier la version 8.0.x de `Microsoft.Extensions.Hosting` et Xunit.StaFact au restore).
