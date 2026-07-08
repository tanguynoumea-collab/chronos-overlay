# CLAUDE.md — Chronos

## Contexte
Overlay WPF always-on-top en forme d'horloge affichant en temps réel l'état des limites
d'usage Claude (fenêtre 5 h + fenêtre hebdomadaire) pour Claude Code et Cowork.
Deux arcs concentriques : longueur = temps avant reset, couleur = % de quota consommé,
gris = quota épuisé. Extérieur = 5 h, intérieur = hebdo.

## Stack
C# / .NET 8 / WPF / MVVM (CommunityToolkit.Mvvm) / Microsoft.Extensions.DependencyInjection.
Rendu des arcs en XAML pur (Path/ArcSegment), aucune dépendance native (pas de SkiaSharp).

## Sources de données
- Primaire : objet d'usage Claude Code (five_hour/seven_day : utilization + resets_at).
- Repli : transcripts JSONL (~/.claude/projects), estimation marquée comme telle.
- Abstraction IUsageProvider : sources interchangeables.
- Pool partagé compte : Cowork déjà inclus dans l'usage de Code.

## Conventions
- MVVM strict, [ObservableProperty] / [RelayCommand], DI, dossiers Models/Views/ViewModels/Services.
- Chemins sous profil utilisateur uniquement, aucun droit admin.
- utilization/resets_at prioritaires sur le comptage de tokens ; ne jamais présenter une estimation comme exacte.
- Reset hebdo best-effort et recalibrable.
- UI et commentaires en français. Activer frontend-design + windows-wpf sur les tâches ui.

## Statut GSD
[Sera rempli après export en fin de session]

<!-- GSD:project-start source:PROJECT.md -->
## Project

**Chronos**

Chronos est un overlay Windows always-on-top en forme d'horloge, posé sur le bureau,
qui affiche en temps réel — d'un coup d'œil — l'état des limites d'usage Claude
(fenêtre 5 h glissante + fenêtre hebdomadaire) pour Claude Code et Cowork.
Le cadran encode deux variables par anneau : longueur d'arc = temps restant avant reset,
couleur = pourcentage de quota consommé. Pour un utilisateur intensif de Claude qui veut
savoir sans y penser combien de marge il lui reste avant d'être bloqué.

**Core Value:** Voir instantanément, sans ouvrir de terminal ni taper `/usage`, combien de quota et de
temps il reste sur les deux fenêtres — et ne jamais présenter une estimation comme un
chiffre exact.

### Constraints

- **Tech stack**: C# / .NET 8 / WPF / MVVM (CommunityToolkit.Mvvm) + Microsoft.Extensions.DependencyInjection — imposé.
- **Rendu**: arcs en XAML pur (Path/ArcSegment), aucune dépendance native — portabilité et simplicité de packaging.
- **Fenêtre**: WindowStyle=None, AllowsTransparency=True, Topmost=True, ShowInTaskbar=False — comportement overlay exigé.
- **Chemins**: uniquement sous %USERPROFILE% / %APPDATA%, aucun droit admin — contrainte de sécurité/déploiement.
- **Honnêteté des chiffres**: utilization/resets_at prioritaires ; ne jamais présenter une estimation comme exacte — confiance utilisateur.
- **Robustesse**: aucune source disponible ≠ crash → état « données indisponibles » ; parsing tolérant (lignes/champs invalides ignorés).
- **Langue**: UI et commentaires en français.
- **Déploiement**: exe self-contained mono-fichier win-x64 + autostart shell:startup, sans ClickOnce.
<!-- GSD:project-end -->

<!-- GSD:stack-start source:research/STACK.md -->
## Technology Stack

## Recommended Stack
### Core Technologies
| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET SDK | 10.x (installé) ciblant `net8.0-windows` | Toolchain de build/publish | Le SDK .NET 10 compile sans problème une cible `net8.0-windows` (compatibilité descendante du SDK). La cible reste net8.0 : LTS, support jusqu'en nov. 2026, runtime pack restauré automatiquement pour le self-contained. |
| WPF (`UseWPF`) | intégré à net8.0-windows | UI overlay, rendu vectoriel XAML | Framework retenu. Le rendu `Path`/`ArcSegment` est natif WPF, accéléré matériellement, sans dépendance externe — cohérent avec la contrainte « aucune dépendance native ». |
| CommunityToolkit.Mvvm | **8.4.2** | MVVM (ObservableObject, `[ObservableProperty]`, `[RelayCommand]`) via générateurs de source | Dernière version stable (publiée le 25/03/2026), maintenue par Microsoft / .NET Foundation. Générateurs de source = zéro réflexion à l'exécution → **compatible mono-fichier et trim-friendly** (ce qui compte vu le packaging). Aucune dépendance UI, purement `netstandard2.0`. |
| Microsoft.Extensions.DependencyInjection | **8.0.x** (dernier patch de la ligne 8.0) | Conteneur IoC (enregistrement providers, VMs, services) | Aligné sur la cible `net8.0` → graphe de dépendances minimal, pas de remontée forcée d'assemblies `System.*` plus récentes. Conteneur MS standard, suffisant pour une app desktop mono-utilisateur. |
| Microsoft.Extensions.Hosting | **8.0.x** (dernier patch de la ligne 8.0) | Generic Host : composition racine, cycle de vie, `IHostedService` pour les timers/watchers, config `settings.json` | Fournit `HostApplicationBuilder` pour câbler DI + configuration + services d'arrière-plan proprement dans une app WPF. Aligné net8.0. |
### Supporting Libraries
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Text.Json | intégré (net8.0) | Parsing tolérant des JSONL + lecture/écriture `settings.json` | Déjà dans le framework — **ne rien ajouter**. Utiliser `JsonSerializerOptions` avec lecture tolérante (ignorer champs/lignes invalides) pour le repli JSONL. |
| Microsoft.Extensions.Configuration + .Json | 8.0.x | Charger `%APPDATA%/Chronos/settings.json` | Optionnel : si on veut binder la config via le Host plutôt que sérialiser à la main. Sinon System.Text.Json seul suffit. |
| Microsoft.Extensions.Logging.Debug | 8.0.x | Traces de dev (découverte des sources, parsing) | Optionnel, utile en phase découverte `docs/data-sources.md`. Retirer/mettre en niveau minimal en release. |
### Development Tools
| Tool | Purpose | Notes |
|------|---------|-------|
| `dotnet publish` (CLI) | Génération de l'exe mono-fichier | Utiliser un **PublishProfile** ou passer les propriétés en ligne de commande, PAS dans le `<PropertyGroup>` inconditionnel (sinon `dotnet build`/debug devient self-contained et lent). |
| Visual Studio 2022 / Rider | IDE WPF (designer XAML, hot reload) | Hot Reload XAML précieux pour caler le cadran (ticks, arcs, tokens de design). |
## Installation
# Depuis le dossier du projet (.csproj déjà en net8.0-windows / UseWPF)
# MVVM (générateurs de source)
# DI + Host (aligner sur la ligne 8.0.x)
# (optionnel) config bindée via le Host
## csproj recommandé (concret)
# → bin/Release/net8.0-windows/win-x64/publish/Chronos.exe  (mono-fichier)
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
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->
## Conventions

Conventions not yet established. Will populate as patterns emerge during development.
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->
## Architecture

Architecture not yet mapped. Follow existing patterns found in the codebase.
<!-- GSD:architecture-end -->

<!-- GSD:workflow-start source:GSD defaults -->
## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd:quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd:debug` for investigation and bug fixing
- `/gsd:execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->
## Developer Profile

> Profile not yet configured. Run `/gsd:profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
