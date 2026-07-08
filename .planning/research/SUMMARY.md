# Project Research Summary

**Project:** Chronos
**Domain:** Overlay de bureau Windows WPF/MVVM always-on-top — moniteur temps réel de quotas Claude (rendu XAML pur, exe mono-fichier)
**Researched:** 2026-07-08
**Confidence:** HIGH (technique WPF/.NET/MVVM vérifiée sur sources officielles) — une seule zone MEDIUM structurante : le format des sources Claude, non documenté par nature.

## Executive Summary

Chronos est un widget de bureau mono-fonction (catégorie HUD/OSD, famille Rainmeter / MSI Afterburner) qui affiche l'état des quotas Claude sous forme d'un cadran à deux anneaux. Les experts construisent ce type d'outil comme une **application desktop mono-processus en couches strictes**, où la seule complexité réelle n'est pas l'UI mais **l'isolation d'une source de données instable** et le **respect de l'affinité de thread WPF**. Le stack imposé (C# / .NET 8 / WPF / MVVM CommunityToolkit + Microsoft.Extensions.DI) est **validé sans réserve** par la recherche ; deux ajustements de forme sont non négociables : cible `net8.0-windows` (le suffixe `-windows` est obligatoire pour WPF) et `PublishTrimmed=false` (WPF n'est pas trim-safe).

L'approche recommandée est une **architecture en couches avec inversion de dépendance sur la source** : la couche Services ne référence aucun type WPF et produit des `UsageSnapshot` immuables ; le ViewModel — et lui seul — franchit la frontière de thread via un `IUiDispatcher` pour traduire ces snapshots en état lié. Le point d'entrée `IUsageProvider` (composite primaire→repli) absorbe la fragilité de la source. Le cadran se rend en `Shape`-derived (`RingArc`) avec géométrie `ArcSegment` calculée par trigonométrie. Deux horloges distinctes cohabitent : une horloge « données » (FileSystemWatcher debounce + PeriodicTimer, thread pool) qui relit le disque, et une horloge « UI » (DispatcherTimer 1 s) qui interpole le temps restant sans aucun I/O.

Les risques majeurs sont concentrés et bien cartographiés : (1) **source non documentée** — parade : tâche de découverte `docs/data-sources.md` AVANT tout code de provider + abstraction stricte + parsing défensif ; (2) **threading WPF** — parade : marshaling centralisé en un point unique, DTO immuables à la frontière ; (3) **honnêteté des chiffres** (Core Value) — parade : provenance Exact/Estimated portée dans le snapshot et reflétée visuellement, reset hebdo best-effort recalibrable ; (4) **overlay transparent** — `AllowsTransparency` force le rendu logiciel (pas d'animation continue), `Topmost` non fiable dans le temps (réaffirmation périodique) ; (5) **packaging mono-fichier** — natives à auto-extraire, faux positifs Defender, test sur machine propre.

## Key Findings

### Recommended Stack

Le stack est **imposé et confirmé** (voir STACK.md). La recherche fige les versions exactes et prescrit une configuration de publication conditionnelle pour ne pas ralentir build/debug. Aucune dépendance native (contrainte respectée par le rendu XAML pur). Le SDK .NET 10 installé compile sans problème une cible `net8.0-windows` (rétro-compatibilité), et le runtime pack .NET 8 est restauré pour le self-contained.

**Core technologies:**
- **.NET 8 (`net8.0-windows`) + WPF** : UI overlay, rendu vectoriel `Path`/`ArcSegment` natif accéléré — LTS, aucune dépendance externe. Le suffixe `-windows` est obligatoire.
- **CommunityToolkit.Mvvm 8.4.2** : MVVM par générateurs de source (`[ObservableProperty]`, `[RelayCommand]`) — zéro réflexion runtime, compatible mono-fichier.
- **Microsoft.Extensions.DependencyInjection + Hosting 8.0.x** : composition racine, cycle de vie, `IHostedService` pour timers/watchers — aligné sur le shared framework .NET 8 (graphe cohérent au packaging).
- **System.Text.Json (intégré)** : parsing tolérant des JSONL + `settings.json` — rien à ajouter.
- **Packaging** : `PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract=true` + `PublishTrimmed=false` + `EnableCompressionInSingleFile=true`.

### Expected Features

Périmètre étudié : l'UX d'interaction de l'overlay (la maquette visuelle est validée, hors périmètre). Chronos reprend les *table stakes* de placement de Rainmeter mais **coupe volontairement** toute configurabilité (skins, z-order multi-niveaux, notifications) pour rester un widget mono-fonction.

**Must have (table stakes) — v1:**
- **Menu contextuel clic droit** (Réglages / Arrière-plan / Recalibrer / Quitter) — *seul* point d'accès et de sortie (`ShowInTaskbar=False`). Fondation, pas confort.
- **Déplacement par glisser** (`DragMove`) — sans lui le widget est piégé.
- **Accroche au coin le plus proche multi-écrans** (WorkingArea) — convention HUD, évite la barre des tâches.
- **Persistance position/coin/écran** (`settings.json`) — indispensable pour un outil lancé au démarrage.
- **Bascule mode arrière-plan (Topmost on/off)** — l'always-on-top permanent est vite gênant.
- **État « données indisponibles »** sans crash — robustesse, confiance.
- **Lancement au démarrage** (`shell:startup`) et **tooltip détaillé au survol** (chiffres exacts + mention « estimée »).

**Should have (competitive) — v1.x:**
- **Opacité réglable + révélation au survol** — discrétion ambiante (fort ratio valeur/coût).
- **Recalibrage fin du reset hebdo** — crédibilise le compte à rebours qui dérive.
- **Paliers d'échelle S/M/L** — adaptation densité d'écran (paliers, pas de resize libre).

**Defer (v2+):**
- **Clic-traversant togglable** — décision d'interaction lourde : `WS_EX_TRANSPARENT` **conflit majeur avec le drag/hover/tooltip** ; exige un chemin de repositionnement hors-fenêtre (tray/hotkey). À trancher tôt même si différé.
- **Bande d'activité des sous-agents** (blocs Task JSONL) — déjà marqué différé dans PROJECT.md.
- **Anti-features explicites à NE PAS construire** : notifications/toasts, historique/graphes, skins configurables, multi-comptes, sons, mode wallpaper (WorkerW), resize libre.

### Architecture Approach

Architecture en **couches strictes avec inversion de dépendance sur la source**. La couche Services ne référence aucun type WPF et produit des `UsageSnapshot` neutres ; le ViewModel franchit seul la frontière de thread. Composition root explicite dans `App.xaml.cs` (pas de `StartupUri`). Voir ARCHITECTURE.md pour le build order détaillé (10 blocs) et les 5 patterns.

**Major components:**
1. **App (Composition Root)** — câble le graphe DI, possède le cycle de vie et le dispose (`OnStartup`/`OnExit`).
2. **IUsageProvider / CompositeUsageProvider** — contrat abstrait `GetAsync()` + `event SnapshotChanged` ; orchestre primaire (`ClaudeUsageObjectProvider`, Exact) → repli (`JsonlEstimationProvider`, Estimated). Point de rupture isolé.
3. **RefreshOrchestrator** — pilote *quand* relire : FileSystemWatcher debounce + PeriodicTimer (filet de sécurité).
4. **MainViewModel** — s'abonne à `SnapshotChanged`, marshale via `IUiDispatcher`, tick 1 s (interpolation), commandes.
5. **RingArc (Control `Shape`-derived)** — géométrie `ArcSegment` paramétrée par DependencyProperties (`AffectsRender`), couleur via `UtilizationToBrushConverter`.
6. **ISettingsStore** — persistance atomique (temp + rename) sous `%APPDATA%/Chronos`.

### Critical Pitfalls

1. **Source non documentée câblée en dur** — parade : `docs/data-sources.md` (découverte) AVANT le code des providers ; `IUsageProvider` strict ; parsing défensif (champ absent → « indisponible », jamais de crash ni de valeur inventée).
2. **Accès UI hors thread Dispatcher** (watcher/timers/async) — crash `InvalidOperationException` intermittent. Parade : DTO immuables à la frontière, marshaling centralisé en un seul point via `IUiDispatcher.Post`, `CheckAccess()` pour éviter le re-post.
3. **Présenter une estimation/un compte à rebours dérivant comme exact** (viole la Core Value) — parade : provenance Exact/Estimated dans le snapshot, marquage visuel distinct, reset hebdo best-effort recalibrable, pas de fausse précision.
4. **Overlay transparent** — `AllowsTransparency=True` force le **rendu logiciel** (pas d'animation continue, pas de blur/shadow) ; `Topmost=True` **n'est pas fiable dans le temps** (réaffirmer via `SetWindowPos HWND_TOPMOST` + `SWP_NOACTIVATE`, accepter la défaite face au plein écran exclusif).
5. **JSONL en cours d'écriture + FileSystemWatcher best-effort** — parade : `FileShare.ReadWrite`, streaming ligne par ligne avec try/catch, ignorer la dernière ligne partielle ; watcher = déclencheur débouncé, PeriodicTimer = vraie garantie de fraîcheur, gérer l'événement `Error` (overflow).
6. **Packaging mono-fichier WPF** — parade : `IncludeNativeLibrariesForSelfExtract=true`, **jamais `PublishTrimmed=true`** (casse WPF + Defender), autostart pointant un chemin stable, test sur **machine propre**.

## Implications for Roadmap

Basé sur la recherche, structure de phases suggérée (aligne le *build order* d'ARCHITECTURE.md et le *pitfall-to-phase mapping* de PITFALLS.md).

### Phase 1: Fondations architecture + squelette overlay
**Rationale:** Sans composition root ni arborescence, rien ne se câble (bloc 1 du build order). L'overlay borderless/transparent/topmost pose aussi les pièges 4 (rendu logiciel, topmost) qu'il faut cadrer tôt.
**Delivers:** Projet `net8.0-windows`/UseWPF, arborescence Models/Views/ViewModels/Services/Controls, DI dans `App.xaml.cs` (pas de StartupUri), `app.manifest` PerMonitorV2, MainWindow overlay vide (borderless, `AllowsTransparency`, `ShowActivated=false`).
**Addresses:** base des features de placement.
**Avoids:** Pitfall 2 (rendu léger/statique), Pitfall 3-topmost (réaffirmation non-activante), config csproj de STACK.md.
**Uses:** CommunityToolkit.Mvvm, Microsoft.Extensions.DI.

### Phase 2: Découverte des sources (bloquante)
**Rationale:** Tout le pipeline données en dépend ; coder un provider avant de connaître la source, c'est deviner (bloc 2, Pitfall 1). Phase courte mais préalable strict.
**Delivers:** `docs/data-sources.md` — localisation précise de l'objet d'usage (five_hour/seven_day), schéma, échantillon réel capturé, hypothèses, structure des JSONL.
**Avoids:** Pitfall 1 (fragilité source non documentée).

### Phase 3: Modèles + pipeline de données (providers)
**Rationale:** Contrat neutre partagé avant producteurs/consommateurs (blocs 3-4). Construire le repli JSONL d'abord donne un chemin vérifiable de bout en bout tôt.
**Delivers:** `UsageSnapshot`/`WindowState`/`SourceReliability` (records immuables) ; `IUsageProvider` ; `JsonlEstimationProvider` (repli) ; `ClaudeUsageObjectProvider` (primaire) ; `CompositeUsageProvider`.
**Implements:** abstraction source + provenance Exact/Estimated.
**Avoids:** Pitfall 1 (parsing défensif), Pitfall 5 (JSONL en écriture : FileShare.ReadWrite, streaming, try/catch par ligne), Pitfall 7 (provenance dans le snapshot).

### Phase 4: Orchestration refresh + ViewModel temps réel
**Rationale:** Fournit le flux d'événements que le VM consomme, puis le VM qui le marshale (blocs 5-6). Threading = risque transverse n°1, à traiter ici.
**Delivers:** `RefreshOrchestrator` (FileSystemWatcher debounce + PeriodicTimer + gestion `Error`), `IUiDispatcher`, `MainViewModel` (abonnement, marshaling, tick 1 s d'interpolation).
**Implements:** les deux horloges distinctes.
**Avoids:** Pitfall 2 (threading Dispatcher centralisé), Pitfall 5/6 (watcher best-effort + PeriodicTimer de secours).

### Phase 5: Cadran (RingArc + converters) + câblage View
**Rationale:** Présentation pure branchée sur un flux déjà éprouvé (blocs 7-8).
**Delivers:** `RingArc` (Shape-derived, DPs), `UtilizationToBrushConverter`, bindings XAML MainWindow ↔ VM ↔ arcs, cadran/ticks, marquage visuel « estimé », état « données indisponibles ».
**Avoids:** Anti-Pattern 5 (`IsLargeArc` + cas 360°), Pitfall 7 (marquage estimé côté UI).

### Phase 6: Comportements overlay (placement + interaction)
**Rationale:** Features périphériques indépendantes du cœur données (bloc 9).
**Delivers:** Drag + snap coin multi-écrans (WorkingArea, DPI), bascule Topmost/arrière-plan, menu contextuel (Réglages/Arrière-plan/Recalibrer/Quitter), persistance settings, tooltip, autostart shell:startup, recalibrage hebdo.
**Addresses:** table stakes v1 de FEATURES.md.
**Avoids:** UX pitfalls (vol de focus, hit-testing, écran débranché).

### Phase 7: Packaging + déploiement
**Rationale:** En dernier, une fois le comportement stable (bloc 10).
**Delivers:** `PublishSingleFile` win-x64 self-contained, test sur machine propre, autostart robuste au déplacement.
**Avoids:** Pitfall 8 (natives auto-extraites, PublishTrimmed=false, Defender/SmartScreen).

### Phase Ordering Rationale

- **Dépendances strictes** : le build order d'ARCHITECTURE.md est un DAG — découverte source → modèles → providers → orchestration → VM → rendu → overlay → packaging. On ne peut pas inverser sans deviner.
- **Découverte avant code** : la Phase 2 est un préalable bloquant imposé par Pitfall 1 ; elle ne produit que de la doc mais conditionne tout le pipeline.
- **Pipeline validable sans UI** : les modèles neutres + `IUiDispatcher` fake permettent de valider les phases 3-4 sans rendu final — le cadran (5) vient se brancher sur un flux éprouvé.
- **Overlay séparé du cœur données** : le placement/interaction (6) est indépendant et peut se dérouler en parallèle du polissage données.

### Research Flags

Phases nécessitant probablement `/gsd:research-phase` en planification :
- **Phase 2 (Découverte des sources)** : **critique** — source Claude non documentée, MEDIUM confidence. Localisation exacte de l'objet d'usage à établir empiriquement (reconnaissance sur la machine réelle), c'est le seul vrai inconnu du projet.
- **Phase 6 (interaction) — volet clic-traversant si remonté en scope** : `WS_EX_TRANSPARENT` conflit drag/hover, mécanisme de sortie hors-fenêtre à concevoir. Non requis si le clic-traversant reste en v2.

Phases à patterns standards (skip research-phase) :
- **Phase 1, 3, 4, 5** : patterns WPF/MVVM/DI + géométrie ArcSegment bien documentés (ARCHITECTURE.md fournit déjà le code de référence).
- **Phase 7** : packaging documenté par STACK.md/PITFALLS.md (config csproj concrète fournie).

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Versions vérifiées sur NuGet/docs officielles ; pièges PublishSingleFile+WPF documentés par issues officielles dotnet/wpf et dotnet/sdk. |
| Features | HIGH | Conventions overlays desktop établies (Rainmeter, MSI Afterburner) ; mécaniques WPF vérifiées. |
| Architecture | HIGH | Patterns WPF/MVVM/DI standards vérifiés Context7 (CommunityToolkit.Mvvm + dotnet/wpf). |
| Pitfalls | HIGH (technique) / MEDIUM (source) | Technique WPF/.NET confirmée par docs officielles ; format des sources Claude incertain par nature. |

**Overall confidence:** HIGH

### Gaps to Address

- **Emplacement/format exact de l'objet d'usage Claude Code** (five_hour/seven_day) : non documenté par Anthropic, API privée de facto. → Traiter en Phase 2 (découverte empirique + `docs/data-sources.md` + échantillon capturé). C'est le principal inconnu ; l'abstraction `IUsageProvider` le rend récupérable à bas coût si la source casse.
- **Ancrage exact du reset hebdomadaire** (~72 h, horaire non documenté) : impossible à figer. → Afficher `resets_at` tel que fourni, traiter le compte à rebours hebdo comme best-effort, prévoir le recalibrage utilisateur (Phase 6).
- **Plafonds de tokens mouvants** (×2 le 6 mai, +50 % hebdo jusqu'au 13/07/2026) : rendent l'estimation JSONL structurellement approximative. → Prioriser toujours utilization/resets_at ; marquer l'estimation comme telle.
- **Numéro de patch exact de la ligne Microsoft.Extensions.* 8.0.x** : prendre le dernier 8.0.x au moment du `restore` (MEDIUM sur le patch, HIGH sur le principe).
- **Décision clic-traversant v1 vs v2** : arbitrage d'interaction structurant (conflit drag) à trancher explicitement en planification, même si l'implémentation est différée.

## Sources

### Primary (HIGH confidence)
- Context7 `/websites/learn_microsoft_en-us_dotnet_communitytoolkit_mvvm` — ObservableProperty/RelayCommand/générateurs de source.
- Context7 `/dotnet/wpf` — DependencyProperty.Register, Dispatcher.BeginInvoke + CheckAccess, priorités Dispatcher.
- NuGet Gallery — CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting/DI 8.0.x.
- Microsoft Learn — single-file deployment (IncludeNativeLibrariesForSelfExtract), breaking change runtime-specific apps .NET 8, AllowsTransparency (WindowStyle=None requis), Graphics Rendering Tiers (AllowsTransparency → rendu logiciel).
- GitHub dotnet/wpf #3386 & #4216 (WPF + trim/single-file crash), dotnet/runtime #33745 (PublishTrimmed → faux positif Defender), dotnet/sdk #24181 (natives extraites).
- Rainmeter Documentation — Arranging Skins (snap, keep-on-screen, transparency, z-order, positionnement multi-écrans).

### Secondary (MEDIUM confidence)
- Microsoft Q&A / CodeProject — WPF/Win32 layered windows (WS_EX_LAYERED + WS_EX_TRANSPARENT, hit-test).
- Comportement Topmost non exclusif + réaffirmation SetWindowPos(HWND_TOPMOST) — pratique Win32 établie, discussions communautaires convergentes.
- DisplayFusion — snap multi-moniteurs. Connaissance de domaine HUD/OSD desktop.

### Tertiary (LOW confidence)
- Format/emplacement des sources Claude Code (objet d'usage, JSONL) — non documenté, à valider empiriquement en Phase 2.

---
*Research completed: 2026-07-08*
*Ready for roadmap: yes*
