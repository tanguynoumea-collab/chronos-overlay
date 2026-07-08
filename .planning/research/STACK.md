# Stack Research

**Domain:** Overlay desktop Windows (WPF) always-on-top, temps réel, rendu XAML pur, exe mono-fichier
**Researched:** 2026-07-08
**Confidence:** HIGH (versions de packages vérifiées sur NuGet/docs officielles ; pièges PublishSingleFile+WPF documentés par les issues officielles dotnet/wpf et dotnet/sdk)

> Note de cadrage : le stack est **imposé** par le porteur (C# / .NET 8 / WPF / MVVM). Ce document **valide** ces choix, **fige les versions exactes** et **signale les pièges** avec la parade. Aucun choix imposé n'est remis en cause — deux ajustements de forme sont prescrits (TFM `net8.0-windows` obligatoire pour WPF, `PublishTrimmed=false` obligatoire pour WPF).

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET SDK | 10.x (installé) ciblant `net8.0-windows` | Toolchain de build/publish | Le SDK .NET 10 compile sans problème une cible `net8.0-windows` (compatibilité descendante du SDK). La cible reste net8.0 : LTS, support jusqu'en nov. 2026, runtime pack restauré automatiquement pour le self-contained. |
| WPF (`UseWPF`) | intégré à net8.0-windows | UI overlay, rendu vectoriel XAML | Framework retenu. Le rendu `Path`/`ArcSegment` est natif WPF, accéléré matériellement, sans dépendance externe — cohérent avec la contrainte « aucune dépendance native ». |
| CommunityToolkit.Mvvm | **8.4.2** | MVVM (ObservableObject, `[ObservableProperty]`, `[RelayCommand]`) via générateurs de source | Dernière version stable (publiée le 25/03/2026), maintenue par Microsoft / .NET Foundation. Générateurs de source = zéro réflexion à l'exécution → **compatible mono-fichier et trim-friendly** (ce qui compte vu le packaging). Aucune dépendance UI, purement `netstandard2.0`. |
| Microsoft.Extensions.DependencyInjection | **8.0.x** (dernier patch de la ligne 8.0) | Conteneur IoC (enregistrement providers, VMs, services) | Aligné sur la cible `net8.0` → graphe de dépendances minimal, pas de remontée forcée d'assemblies `System.*` plus récentes. Conteneur MS standard, suffisant pour une app desktop mono-utilisateur. |
| Microsoft.Extensions.Hosting | **8.0.x** (dernier patch de la ligne 8.0) | Generic Host : composition racine, cycle de vie, `IHostedService` pour les timers/watchers, config `settings.json` | Fournit `HostApplicationBuilder` pour câbler DI + configuration + services d'arrière-plan proprement dans une app WPF. Aligné net8.0. |

> **Alignement de versions Microsoft.Extensions.\*** : les lignes 9.0.x et 10.0.9 existent (dernière = 10.0.9, 09/06/2026) et **fonctionnent** dans une app net8.0, mais elles tirent des assemblies `System.*` plus récentes en transitif. Pour une cible `net8.0`, **rester sur la ligne 8.0.x** = graphe cohérent avec le shared framework .NET 8, moins de surprises au packaging mono-fichier. Passer à 9.0.x/10.0.x seulement si un package tiers l'exige. (Confiance : HIGH sur le principe, MEDIUM sur le numéro de patch exact — prendre le dernier 8.0.x au moment du `restore`.)

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Text.Json | intégré (net8.0) | Parsing tolérant des JSONL + lecture/écriture `settings.json` | Déjà dans le framework — **ne rien ajouter**. Utiliser `JsonSerializerOptions` avec lecture tolérante (ignorer champs/lignes invalides) pour le repli JSONL. |
| Microsoft.Extensions.Configuration + .Json | 8.0.x | Charger `%APPDATA%/Chronos/settings.json` | Optionnel : si on veut binder la config via le Host plutôt que sérialiser à la main. Sinon System.Text.Json seul suffit. |
| Microsoft.Extensions.Logging.Debug | 8.0.x | Traces de dev (découverte des sources, parsing) | Optionnel, utile en phase découverte `docs/data-sources.md`. Retirer/mettre en niveau minimal en release. |

> `FileSystemWatcher`, `PeriodicTimer`, `DispatcherTimer` sont tous **dans le framework** (System.IO / System.Threading / WPF) — aucun package à ajouter. C'est exactement l'outillage prescrit par PROJECT.md.

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| `dotnet publish` (CLI) | Génération de l'exe mono-fichier | Utiliser un **PublishProfile** ou passer les propriétés en ligne de commande, PAS dans le `<PropertyGroup>` inconditionnel (sinon `dotnet build`/debug devient self-contained et lent). |
| Visual Studio 2022 / Rider | IDE WPF (designer XAML, hot reload) | Hot Reload XAML précieux pour caler le cadran (ticks, arcs, tokens de design). |

## Installation

```bash
# Depuis le dossier du projet (.csproj déjà en net8.0-windows / UseWPF)

# MVVM (générateurs de source)
dotnet add package CommunityToolkit.Mvvm --version 8.4.2

# DI + Host (aligner sur la ligne 8.0.x)
dotnet add package Microsoft.Extensions.Hosting --version 8.0.1
dotnet add package Microsoft.Extensions.DependencyInjection --version 8.0.1

# (optionnel) config bindée via le Host
dotnet add package Microsoft.Extensions.Configuration.Json --version 8.0.1
```

> Prendre le **dernier patch disponible** de la ligne 8.0.x au moment du restore (8.0.1 confirmé publié ; des patchs de sécurité ultérieurs 8.0.x sont préférables). `CommunityToolkit.Mvvm` : **8.4.2 fixe** (dernière stable 2026).

## csproj recommandé (concret)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <!-- OBLIGATOIRE pour WPF : le suffixe -windows. "net8.0" seul NE compile PAS WPF. -->
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>

    <!-- Identité de l'app -->
    <AssemblyName>Chronos</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>

    <!-- Cible de publication (peut aussi vivre dans un PublishProfile) -->
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>

    <!-- DPI / rendu : app.manifest recommandé pour PerMonitorV2 (overlay net sur multi-écrans) -->
  </PropertyGroup>

  <!-- Propriétés de PUBLICATION uniquement : conditionnées pour ne pas ralentir build/debug -->
  <PropertyGroup Condition="'$(PublishSingleFile)' == 'true'">
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>

    <!-- WPF n'est PAS trim-safe : garder false (voir What NOT to Use) -->
    <PublishTrimmed>false</PublishTrimmed>

    <!-- Optionnel : démarrage plus rapide contre taille + gros exe -->
    <PublishReadyToRun>true</PublishReadyToRun>

    <!-- NE PAS activer InvariantGlobalization si l'UI formate dates/nombres en fr-FR -->
    <InvariantGlobalization>false</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.1" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
  </ItemGroup>

</Project>
```

**Commande de publication :**

```bash
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
# → bin/Release/net8.0-windows/win-x64/publish/Chronos.exe  (mono-fichier)
```

## Décisions de configuration — le POURQUOI

| Propriété | Valeur | Rationale |
|-----------|--------|-----------|
| `TargetFramework` | `net8.0-windows` | **WPF exige le TFM `-windows`.** `net8.0` seul ne connaît pas `UseWPF` et échoue au build. C'est l'ajustement de forme n°1 vs le libellé « net8.0 » de PROJECT.md. |
| `SelfContained` | `true` (explicite) | Depuis .NET 8, un `RuntimeIdentifier` **n'implique plus** self-contained (breaking change officiel). `PublishSingleFile` l'implique au `publish` seulement — le mettre explicite lève toute ambiguïté (build vs publish). |
| `IncludeNativeLibrariesForSelfExtract` | `true` | WPF embarque des **DLL natives** (`PresentationNative`, `wpfgfx`, `vcruntime`…). Sans ce flag, elles restent à côté de l'exe → pas de vrai mono-fichier. À `true`, elles sont extraites dans un dossier temp au 1er lancement. |
| `EnableCompressionInSingleFile` | `true` | Réduit l'exe (typiquement ~140 Mo → ~60-70 Mo pour du WPF self-contained). Coût : léger surcoût de décompression au démarrage. Acceptable pour un overlay lancé une fois au boot. |
| `PublishTrimmed` | **`false`** | **WPF n'est pas compatible avec le trimming** (voir pièges). Activer le trim casse le rendu XAML/réflexion → crash au lancement. Non négociable. |
| `PublishReadyToRun` | `true` (optionnel) | Pré-compile en code natif → démarrage plus rapide. Contre : +taille. Bénéfique pour une app d'autostart. |
| `InvariantGlobalization` | `false` | Réduirait la taille mais **casse le formatage fr-FR** (dates de reset, comptes à rebours). L'UI est en français → garder la globalization. |

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| CommunityToolkit.Mvvm 8.4.2 | Prism / MVVMLight / manuel `INotifyPropertyChanged` | Prism si on avait besoin de modules/régions/navigation complexe — ici overkill pour une fenêtre unique. MVVMLight est déprécié. `INPC` manuel = boilerplate évitable grâce aux générateurs. |
| Généric Host (Extensions.Hosting) | `ServiceCollection` nu sans Host | Le Host nu (juste `new ServiceCollection()`) suffit si on ne veut PAS `IHostedService`/config/logging intégrés. Ici les timers/watchers en `IHostedService` justifient le Host complet. |
| Extensions.* 8.0.x | 9.0.x / 10.0.9 | Uniquement si un package tiers force une version ≥ 9. Sinon rester aligné net8.0. |
| Rendu XAML `Path`/`ArcSegment` | SkiaSharp / Direct2D | Jamais ici : contrainte explicite « aucune dépendance native ». SkiaSharp ré-ajoute des DLL natives et complique le mono-fichier — exactement ce qu'on évite. |
| Mono-fichier self-contained | Framework-dependent + runtime installé | FD réduit l'exe à quelques Mo MAIS exige .NET 8 Desktop Runtime sur la machine. Contrainte = mono-fichier autonome → self-contained obligatoire. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `PublishTrimmed=true` avec WPF | WPF **n'est pas trim-safe** : le trimmer supprime des types résolus par réflexion (XAML, styles, converters) → crash `Unhandled Exception` au lancement (issues officielles dotnet/wpf #3386 et #4216). | `PublishTrimmed=false`. Compresser via `EnableCompressionInSingleFile` à la place. |
| `TargetFramework=net8.0` (sans `-windows`) | `UseWPF` inconnu → erreur de build. | `net8.0-windows`. |
| `PublishAot=true` | AOT incompatible WPF (réflexion, COM, XAML). | R2R (`PublishReadyToRun`) si on veut accélérer le démarrage. |
| `InvariantGlobalization=true` (UI fr-FR) | Casse le formatage local des dates/nombres des comptes à rebours. | Laisser `false` (défaut). |
| `Assembly.Location` / `GetExecutingAssembly().Location` pour trouver des fichiers | **Vide en mono-fichier** → chemins cassés. | `AppContext.BaseDirectory` ou chemins `%USERPROFILE%`/`%APPDATA%` construits via `Environment.GetFolderPath`. |
| Propriétés `PublishSingleFile`/`SelfContained` en `<PropertyGroup>` inconditionnel | Rend `dotnet build`/debug self-contained → builds lents, F5 alourdi. | Conditionner sur `'$(PublishSingleFile)'=='true'` ou utiliser un PublishProfile. |
| MVVMLight, Microsoft.Toolkit.Mvvm (ancien nom) | Dépréciés / renommés. | CommunityToolkit.Mvvm 8.4.2. |
| SkiaSharp / D3DImage pour les arcs | Ré-introduit des dépendances natives ; D3DImage + `AllowsTransparency` = pics CPU sur fenêtre layered. | `Path` + `ArcSegment` XAML pur. |

## Pièges spécifiques overlay transparent (à intégrer au XAML/Window)

| Piège | Symptôme | Parade |
|-------|----------|--------|
| `AllowsTransparency=true` exige `WindowStyle=None` | Exception au chargement sinon | Toujours `WindowStyle=None` + `AllowsTransparency=true` + `Background=Transparent` ensemble (déjà prévu dans PROJECT.md). |
| Fenêtre layered (transparence) = coût de composition | CPU si zones transparentes énormes ou animations lourdes | Garder le cadran compact, éviter animations continues inutiles ; le tick 1 s ne redessine que les arcs/texte (léger). |
| Hit-testing sur zones transparentes | Clics « traversent » ou capturent mal | Zones cliquables (drag, bouton arrière-plan) doivent avoir un `Background` non-null (même quasi-transparent `#01000000`) pour rester hit-testables. |
| Multi-écrans + DPI mixte | Accroche au coin décalée, flou | `app.manifest` en **PerMonitorV2** ; calculer les coins via `System.Windows.Forms.Screen` ou les API WPF de `PresentationSource`/`Screen` en coordonnées device-independent. |
| Mono-fichier + extraction native au 1er lancement | Léger délai/AV smartscreen au tout premier run | Normal ; `IncludeNativeLibrariesForSelfExtract` extrait une fois. Signer l'exe (optionnel) réduit les alertes SmartScreen. |

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| CommunityToolkit.Mvvm 8.4.2 | net8.0-windows, .NET 8 runtime | Cible `netstandard2.0` → compatible net8.0 ; générateurs de source OK avec Roslyn du SDK .NET 10. |
| Microsoft.Extensions.Hosting 8.0.x | Microsoft.Extensions.DependencyInjection 8.0.x | Toujours **aligner les patchs entre eux** (même ligne 8.0.x) pour éviter les conflits transitifs. |
| SDK .NET 10 | cible net8.0-windows | SDK rétro-compatible : compile/publie une cible net8. Le runtime pack .NET 8 win-x64 est restauré depuis NuGet pour le self-contained. |
| PublishSingleFile + WPF | net8.0-windows self-contained | OK **si** `IncludeNativeLibrariesForSelfExtract=true` et `PublishTrimmed=false`. |

## Sources

- NuGet Gallery — CommunityToolkit.Mvvm **8.4.2** (25/03/2026) — https://www.nuget.org/packages/CommunityToolkit.Mvvm — HIGH
- NuGet Gallery — Microsoft.Extensions.DependencyInjection / Hosting (lignes 8.0.x, 9.0.x, 10.0.9) — https://www.nuget.org/packages/microsoft.extensions.hosting/ — HIGH
- Context7 `/websites/learn_microsoft_en-us_dotnet_communitytoolkit_mvvm` — setup ObservableProperty / RelayCommand / générateurs — HIGH
- Microsoft Learn — « Create a single file for application deployment » (IncludeNativeLibrariesForSelfExtract, EnableCompressionInSingleFile) — https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview — HIGH
- Microsoft Learn — « Breaking change: Runtime-specific apps no longer self-contained » (.NET 8) — https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/8.0/runtimespecific-app-default — HIGH
- GitHub dotnet/wpf #3386 & #4216 — WPF + PublishTrimmed/SingleFile crash — https://github.com/dotnet/wpf/issues/3386 — HIGH (piège trimming)
- Microsoft Learn — Window.AllowsTransparency (WindowStyle=None requis) — https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency — HIGH
- « Transparent windows in WPF » (Dwayne Need) + wpf-disciples (hit-test layered) — MEDIUM (patterns overlay)

---
*Stack research for: overlay desktop Windows WPF always-on-top, rendu XAML pur, exe mono-fichier self-contained*
*Researched: 2026-07-08*
