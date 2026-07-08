# Phase 6 : Comportements overlay (placement + interaction) — Research

**Researched:** 2026-07-08
**Domain:** Placement/interaction d'un overlay WPF borderless multi-écrans (drag, snap, DPI PerMonitorV2, persistance, autostart shell:startup)
**Confidence:** HIGH (mécaniques WPF/Win32 vérifiées, dont bugs officiels dotnet/wpf ; conventions overlay établies)

## Summary

Cette phase est presque entièrement de la **plomberie WPF/Win32 + persistance**, aucune nouvelle dépendance NuGet. Le pipeline données (Phases 3-4) et le cadran (Phase 5) sont figés ; on ajoute une couche de placement/interaction autour de la fenêtre existante. Les décisions structurantes sont déjà verrouillées dans CONTEXT.md (DragMove, snap WorkingArea, menu clic droit seul point d'accès, toggle Topmost + suspend TopmostGuard, settings.json atomique, autostart .lnk, recalibrage hebdo best-effort).

Le seul vrai piège technique est le **multi-écrans en DPI mixte** : `Window.Left`/`Window.Top` de WPF sont **officiellement cassés** en PerMonitorV2 quand des moniteurs ont des facteurs DPI différents (dotnet/wpf #4127 et #3105 — WPF calcule la même position DIU pour des positions physiques différentes). La parade fiable et éprouvée est de **positionner la fenêtre en pixels PHYSIQUES via `SetWindowPos`** (interop déjà présent dans le projet) plutôt que via les propriétés WPF, et de calculer les coins sur la `rcWork` du moniteur courant obtenue par `MonitorFromWindow` + `GetMonitorInfo`. Cela contourne entièrement la confusion DIU inter-moniteurs.

**Primary recommendation :** Garder la logique « coin le plus proche » en fonction **PURE neutre** (structs de doubles, testable en `[Fact]` sans écran), et déléguer TOUT le placement réel à un **adaptateur WPF `IWindowController`** (même pattern d'allow-list que `TopmostGuard`) qui pilote `SetWindowPos` en coordonnées physiques. Persister le **coin + device name** (pas des X/Y bruts fragiles) pour une restauration robuste au rebranchement d'écran.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions (verrouillées)

**Placement**
- FEN-02 : déplacement par glisser via `DragMove` (`MouseLeftButtonDown` sur le cadran).
- FEN-03 : au relâchement, accroche au coin d'écran LE PLUS PROCHE, cible la **WorkingArea** (pas Bounds, pour éviter la barre des tâches), marge ~12 px.
- FEN-04 : multi-écrans — coordonnées Per-Monitor V2 (manifest déjà en place), coins calculés sur le moniteur contenant la fenêtre ; **repli sur écran primaire** si l'écran persisté a disparu.
- Chaîne stricte : drag → snap → persistance.

**Menu contextuel** (table stake critique)
- FEN-06 : menu clic droit = **SEUL** point d'accès (pas de barre de titre, pas de barre des tâches, pas d'Alt-Tab). Items : « Arrière-plan » (toggle), « Recalibrer le reset hebdo… », « Lancer au démarrage » (toggle DEP-02), « Quitter ».
- Sans « Quitter » l'utilisateur doit tuer le process — garde-fou n°1.
- `ContextMenu` WPF standard, commandes `[RelayCommand]` dans le ViewModel.

**Arrière-plan (FEN-05)**
- Toggle Topmost : `Topmost=false` + renvoi au fond (`SetWindowPos HWND_BOTTOM`) ; retour premier plan = `Topmost=true` + réactivation TopmostGuard.
- Le TopmostGuard (Phase 1) doit être **suspendu** en mode arrière-plan (sinon il re-force le topmost toutes les 2 s).
- Pas de re-assertion agressive : pas de clignotement, pas de vol de focus.

**Persistance (FEN-07)**
- `%APPDATA%\Chronos\settings.json` (ChronosPaths existant) : position (X,Y), coin d'accroche, écran (device name), mode arrière-plan, intervalle de refresh, offset de recalibrage hebdo.
- Écriture **atomique** (temp + rename, pattern du pont Node), lecture **tolérante** (fichier corrompu → défauts).
- Service `SettingsService` neutre (couche Services sans WPF), restauration au lancement **AVANT** affichage.

**Recalibrage hebdo (ROB-03)**
- Dialogue/menu permettant de définir un OFFSET/date d'ancrage appliqué au `resets_at` hebdo estimé **quand la source est le repli JSONL**. Quand la source primaire fournit `resets_at` exact, le recalibrage **ne s'applique PAS** (les chiffres exacts priment). Persisté dans settings.json.

**Autostart (DEP-02)**
- Toggle menu « Lancer au démarrage » : crée/supprime un raccourci `.lnk` dans `shell:startup` pointant vers l'exe courant. **Aucun droit admin.** WSH COM (`IWshRuntimeLibrary`) ou écriture .lnk — voie la plus simple sans dépendance native.

### Claude's Discretion
Marge de snap exacte, animation de snap (**aucune** si coûteuse — rendu logiciel forcé), structure du dialogue de recalibrage (minimal), organisation des services.

### Deferred Ideas (OUT OF SCOPE)
- Tray icon (V2-05), clic-traversant (V2-04), opacité/échelle (V2-06). **Ne rien construire ici.**
- Note STATE.md : la décision clic-traversant v1-vs-v2 doit être **tranchée explicitement** au plan comme « différée v2 » (elle conflictue avec le drag). Pas d'implémentation.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FEN-02 | Déplacer l'overlay par glisser (DragMove) | Pattern DragMove bloquant (§ Pattern 1), guard bouton gauche |
| FEN-03 | Snap au coin le plus proche au relâchement (WorkingArea) | Fonction pure `NearestCorner` (§ Pattern 2) + snap-après-DragMove |
| FEN-04 | Snap multi-écrans Per-Monitor, repli si écran disparu | Win32 `MonitorFromWindow`/`GetMonitorInfo` + placement physique (§ Pattern 3, Pitfall 1) |
| FEN-05 | Toggle arrière-plan (Topmost off + fond) + retour | `SetWindowPos HWND_BOTTOM` + Suspend/Resume TopmostGuard (§ Pattern 5, Code Examples) |
| FEN-06 | Menu contextuel clic droit (4 items) | `ContextMenu` XAML + `[RelayCommand]` VM (§ Pattern 4) |
| FEN-07 | Persistance settings.json atomique restaurée au lancement | `SettingsService` neutre + écriture atomique + restore en SourceInitialized (§ Pattern 6) |
| ROB-03 | Recalibrage hebdo best-effort, ne s'applique qu'au repli | Fonction pure `WeeklyRecalibration` appliquée au VM (§ Pattern 7) |
| DEP-02 | Autostart shell:startup sans admin | `.lnk` via WScript.Shell COM `dynamic` + `Environment.ProcessPath` (§ Pattern 8, Code Examples) |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Directives actionnables extraites de `CLAUDE.md` (autorité = décisions verrouillées) :

- **MVVM strict** : `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers `Models/Views/ViewModels/Services`. Le code-behind fenêtre reste limité au strict nécessaire d'API fenêtre (DragMove, hooks HWND — acceptable, cf. CONTEXT).
- **Aucune dépendance native**, pas de nouveau NuGet (System.Text.Json intégré suffit pour settings.json ; interop Win32 via `NativeMethods` existant).
- **Chemins sous profil utilisateur uniquement, aucun droit admin** → `shell:startup` (jamais HKLM), `%APPDATA%\Chronos`.
- **Ne jamais présenter une estimation comme exacte** → le countdown hebdo recalibré doit **rester badgé « estimée »** (le badge `IsEstimated` existe déjà, ne pas le retirer).
- **Reset hebdo best-effort et recalibrable** (ROB-03).
- **UI et commentaires en français.** Activer skills `frontend-design` + `windows-wpf` sur les tâches UI (dialogue de recalibrage, menu).
- **Pas de `Assembly.Location`** (vide en mono-fichier) → utiliser `Environment.ProcessPath` / `Environment.GetFolderPath`.
- **Purge de la couche Services** : `ServicesLayerPurityTests` interdit tout type WPF (`PresentationCore`/`PresentationFramework`/`WindowsBase`) dans les signatures publiques de `Chronos.Services` et `Chronos.Models`, **sauf** l'allow-list nominative (`WpfUiDispatcher`, `TopmostGuard`). Tout nouvel adaptateur fenêtre doit être **ajouté à cette allow-list** ; toute logique pure doit éviter `System.Windows.Rect`/`Point` (types WindowsBase) dans ces namespaces.

## Standard Stack

**Aucune nouvelle dépendance.** Tout est déjà présent ou dans le framework.

### Core (déjà en place)
| Composant | Version | Rôle Phase 6 | Notes |
|-----------|---------|--------------|-------|
| WPF (`net8.0-windows`) | intégré | `DragMove`, `ContextMenu`, `HwndSource` hooks, `VisualTreeHelper.GetDpi` | Manifest PerMonitorV2 **déjà** en place (`app.manifest`) |
| CommunityToolkit.Mvvm | 8.4.2 | `[RelayCommand]` pour les 4 items de menu | Générateurs de source, déjà utilisé |
| System.Text.Json | intégré | (dé)sérialisation `settings.json` tolérante | `JsonSerializerOptions` tolérant, comme `ClaudeUsageObjectProvider` |
| `Chronos.Interop.NativeMethods` | interne | `SetWindowPos` (déjà) + à étendre (`HWND_BOTTOM`, `MonitorFromWindow`, `GetMonitorInfo`, `GetDpiForMonitor`) | InternalsVisibleTo Chronos.Tests déjà configuré |

### Supporting (framework, à activer)
| API | Namespace/DLL | Usage | Quand |
|-----|---------------|-------|-------|
| `WScript.Shell` (COM late-bound) | via `Type.GetTypeFromProgID` + `dynamic` | Création du `.lnk` autostart | DEP-02. Aucun NuGet (pas d'IWshRuntimeLibrary interop) |
| `Environment.ProcessPath` | System (net6+) | Cible du raccourci = exe courant | **Single-file-safe** (contrairement à `Assembly.Location`) |
| `Environment.GetFolderPath(SpecialFolder.Startup)` | System | Dossier `shell:startup` (`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`) | Per-user, aucun admin |
| `HwndSource.AddHook` / `WM_DISPLAYCHANGE` | PresentationCore | Détecter débranchement/réagencement d'écran à chaud | FEN-04 repli à chaud (préférable à `SystemEvents`) |
| `GetDpiForMonitor` (Shcore.dll) | P/Invoke | Facteur DPI d'un moniteur ciblé (restauration) | Win 8.1+ ; sinon `VisualTreeHelper.GetDpi(window)` pour le moniteur courant |

**Installation :** aucune. Ne rien ajouter au `.csproj` (surtout **pas** `<UseWindowsForms>` — voir « Don't Hand-Roll »).

**Vérification versions :** N/A (aucun package ajouté). Les versions du stack existant restent celles validées dans STACK.md (CLAUDE.md).

## Architecture Patterns

### Structure recommandée (extension de l'existant)
```
src/Chronos/
├── Interop/
│   └── NativeMethods.cs        # ÉTENDRE : HWND_BOTTOM, MonitorFromWindow, GetMonitorInfo,
│                               #           MONITORINFOEX, GetDpiForMonitor, RECT/POINT
├── Placement/                  # NOUVEAU namespace NEUTRE (hors purity-gate Services/Models)
│   ├── RectD.cs                # readonly record struct RectD(double X,Y,Width,Height) — neutre
│   ├── OverlayCorner.cs        # enum TopLeft/TopRight/BottomLeft/BottomRight
│   └── CornerSnap.cs           # fonction PURE NearestCorner(...) — testable [Fact] sans écran
├── Services/
│   ├── ChronosSettings.cs      # record neutre (schéma settings.json)
│   ├── SettingsService.cs      # NEUTRE : load tolérant + save atomique (temp+rename)
│   ├── IWindowController.cs    # NEUTRE : interface (aucun type WPF en signature)
│   ├── IAutostartService.cs    # NEUTRE : Enable/Disable/IsEnabled
│   ├── AutostartService.cs     # NEUTRE : .lnk via COM dynamic (System.Object, pas de WPF)
│   ├── WeeklyRecalibration.cs  # fonction PURE (WindowState → WindowState), neutre
│   └── ChronosPaths.cs         # ÉTENDRE : + SettingsFile
├── ViewModels/
│   ├── MainViewModel.cs        # + [RelayCommand] ToggleBackground/Recalibrate/ToggleAutostart/Quit
│   │                           #   applique WeeklyRecalibration dans ApplySnapshot
│   └── RecalibrationViewModel.cs  # NOUVEAU (dialogue minimal)
└── Views/
    ├── MainWindow.xaml(.cs)    # + ContextMenu, + MouseLeftButtonDown→DragMove→snap,
    │                           #   + WM_DISPLAYCHANGE hook, restore en SourceInitialized
    ├── OverlayController.cs     # NOUVEAU adaptateur WPF (impl. IWindowController) → allow-list purity
    └── RecalibrationDialog.xaml(.cs)  # NOUVEAU dialogue WPF minimal
```

### Pattern 1 : Drag via `DragMove` (bloquant) puis snap au retour — FEN-02/FEN-03
**What :** `DragMove()` est **synchrone/bloquant** : il ne rend la main qu'au relâchement du bouton gauche. `MouseLeftButtonUp` **ne se déclenche pas** après (DragMove consomme le up). Le snap se fait donc **juste après le retour de `DragMove()`**, pas dans un handler MouseUp.
**When :** `MouseLeftButtonDown` sur le cadran (zone hit-testable — `Background` non-null requis, déjà `Transparent` sur la Grid mais le fond Ellipse capte).
**Gotcha :** `DragMove()` lève `InvalidOperationException` si le bouton gauche n'est pas pressé → garder `if (e.ButtonState != MouseButtonState.Pressed) return;` ou try/catch. Fenêtre borderless taille fixe jamais maximisée → les cas « snap Windows / restore maximisé » (sources web) **ne s'appliquent pas** ici.
```csharp
// Views/MainWindow.xaml.cs (code-behind fenêtre = acceptable, API fenêtre — cf. CONTEXT)
private void Cadran_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (e.ButtonState != MouseButtonState.Pressed) return;
    DragMove();                 // BLOQUE jusqu'au relâchement
    _controller.SnapToNearestCorner();  // s'exécute au relâchement (pas de MouseUp)
}
```

### Pattern 2 : « Coin le plus proche » = fonction PURE neutre — FEN-03
**What :** signature testable sans écran, en unités agnostiques (physiques OU DIU, on lui passe des doubles). **Ne pas** utiliser `System.Windows.Rect`/`Point` (types WindowsBase → casserait `ServicesLayerPurityTests` si placé en Services/Models). Placer dans le namespace neutre `Chronos.Placement`.
```csharp
// Placement/CornerSnap.cs — PURE, [Fact] sans STA
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
}

public static class CornerSnap
{
    /// <summary>Coin de workArea le plus proche du centre de window. Retourne le top-left cible (mêmes unités).</summary>
    public static (double X, double Y) NearestCorner(RectD window, RectD workArea, double margin)
    {
        bool left = window.CenterX < workArea.CenterX;
        bool top  = window.CenterY < workArea.CenterY;
        double x = left ? workArea.X + margin
                        : workArea.Right - window.Width - margin;
        double y = top  ? workArea.Y + margin
                        : workArea.Bottom - window.Height - margin;
        return (x, y);
    }
}
```
Tests : 4 quadrants → 4 coins attendus ; marge respectée ; fenêtre plus grande que la zone → clamp (optionnel).

### Pattern 3 : Placement multi-écrans en pixels PHYSIQUES (contourne le bug WPF) — FEN-04
**What :** NE PAS positionner via `Window.Left`/`Top` (cassés en DPI mixte — cf. Pitfall 1). Obtenir la `rcWork` (physique) du moniteur courant, calculer le coin en physique, poser via `SetWindowPos` en physique.
**Chaîne :**
1. `hwnd = new WindowInteropHelper(window).Handle` (HWND garanti après `SourceInitialized`).
2. `hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST)`.
3. `GetMonitorInfo(hMon, ref mi)` → `mi.rcWork` (physique), `mi.szDevice` (`\\.\DISPLAY1`).
4. scale = `VisualTreeHelper.GetDpi(window).DpiScaleX` (moniteur courant ; ou `GetDpiForMonitor` pour un moniteur ciblé au restore).
5. `physW = window.ActualWidth * scale`, `physH = window.ActualHeight * scale`, `marginPx = margin * scale`.
6. `(px, py) = CornerSnap.NearestCorner(fenêtrePhys, rcWorkPhys, marginPx)`.
7. `SetWindowPos(hwnd, IntPtr.Zero, px, py, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE)` (pas de `HWND_TOPMOST` ici pour ne pas interférer avec le mode arrière-plan ; le TopmostGuard réaffirme séparément).

**Note DPI-change :** après un déplacement vers un moniteur de DPI différent, WPF PerMonitorV2 redimensionne la fenêtre et lève `Window.DpiChanged`. Re-déclencher **un** snap sur `DpiChanged` pour re-caler le coin (la taille physique a changé). La fonction pure étant idempotente, c'est sûr.

### Pattern 4 : Menu contextuel = seul point d'accès — FEN-06
**What :** `ContextMenu` WPF sur la Grid racine ; `MenuItem.Command` bindés à des `[RelayCommand]` du `MainViewModel`. Clic gauche = drag, clic droit = menu → **aucun conflit**.
```xml
<Grid.ContextMenu>
  <ContextMenu>
    <MenuItem Header="Arrière-plan" IsCheckable="True"
              IsChecked="{Binding IsBackground}" Command="{Binding ToggleBackgroundCommand}"/>
    <MenuItem Header="Recalibrer le reset hebdo…" Command="{Binding RecalibrateCommand}"/>
    <MenuItem Header="Lancer au démarrage" IsCheckable="True"
              IsChecked="{Binding IsAutostart}" Command="{Binding ToggleAutostartCommand}"/>
    <Separator/>
    <MenuItem Header="Quitter" Command="{Binding QuitCommand}"/>
  </ContextMenu>
</Grid.ContextMenu>
```
**Gotcha :** le `DataContext` d'un `ContextMenu` n'hérite pas toujours automatiquement (il est hors de l'arbre visuel). En pratique, sur un `ContextMenu` défini inline dans `Grid.ContextMenu`, le `PlacementTarget.DataContext` est accessible ; le plus simple/robuste est que le `ContextMenu` hérite du DataContext de la fenêtre (fonctionne pour un menu attaché à un élément de la fenêtre dont le DataContext est le VM). Prévoir un test/UAT que les 4 commandes se déclenchent. `QuitCommand` → `Application.Current.Shutdown()` (via l'adaptateur ou directement, le VM peut appeler `IWindowController.Quit()` pour rester neutre).

### Pattern 5 : Toggle arrière-plan + Suspend/Resume TopmostGuard — FEN-05
Voir Code Examples pour le code concret. Principe : passer par l'adaptateur `IWindowController` (VM neutre). Aller au fond = `Topmost=false` + `guard.Suspend()` + `SetWindowPos(hwnd, HWND_BOTTOM, …, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE)`. Revenir = `Topmost=true` + `guard.Resume()` (Resume réaffirme `HWND_TOPMOST`). Persister l'état dans settings (`Background`).

### Pattern 6 : Persistance neutre + restauration avant affichage — FEN-07
**Schéma settings.json** (record neutre) :
```csharp
public sealed record ChronosSettings
{
    public OverlayCorner Corner { get; init; } = OverlayCorner.TopRight;
    public string? MonitorDeviceName { get; init; }        // \\.\DISPLAY1 (repli si absent)
    public double? X { get; init; }                         // DIU indicatif (optionnel, non fiable seul)
    public double? Y { get; init; }
    public bool Background { get; init; }
    public double RefreshIntervalSeconds { get; init; } = 60;
    public DateTimeOffset? WeeklyAnchor { get; init; }      // recalibrage ROB-03
}
```
**Recommandation clé :** persister **Corner + MonitorDeviceName** comme source de vérité (X/Y = simple indice). Au restore : trouver le moniteur par device name → calculer le coin → placer. Si device name introuvable → **moniteur primaire + même coin** (repli FEN-04, jamais hors-écran). Beaucoup plus robuste que des X/Y bruts (survit aux changements de résolution).
**Écriture atomique** (miroir du pont Node `writeSettingsAtomic`) :
```csharp
var tmp = _paths.SettingsFile + $".tmp-{Environment.ProcessId}";
File.WriteAllText(tmp, JsonSerializer.Serialize(settings, _opts));
File.Move(tmp, _paths.SettingsFile, overwrite: true);   // remplacement atomique (même volume)
```
**Lecture tolérante** : try/catch (`IOException`/`JsonException`) → `new ChronosSettings()` (défauts). Créer le dossier `%APPDATA%\Chronos` si absent (`Directory.CreateDirectory`).
**Restauration AVANT affichage** : appliquer dans `SourceInitialized` (HWND dispo, **avant** le premier rendu → pas de saut visible), pas dans `Loaded` (post-rendu = flash). Remplacer l'actuel `PlacerCoinSuperieurDroit` (sur `Loaded`) par la restauration en `SourceInitialized`.

### Pattern 7 : Recalibrage hebdo = fonction PURE appliquée au VM — ROB-03
**What :** fonction pure neutre, appliquée dans `MainViewModel.ApplySnapshot` **avant** `SevenDay.Apply(...)`. Ne touche PAS les providers (restent neutres/purs).
**Règle métier :** si la fenêtre hebdo est **Exact avec `ResetsAt` non-null → inchangée** (les chiffres exacts priment) ; sinon (repli Estimated / ResetsAt null) et `WeeklyAnchor` défini → **synthétiser** `ResetsAt` depuis l'ancre. Le résultat reste `Reliability = Estimated` → `IsEstimated` reste vrai → **badge « estimée » conservé** (honnêteté).
```csharp
public static class WeeklyRecalibration
{
    static readonly TimeSpan Week = TimeSpan.FromDays(7);
    public static WindowState Apply(WindowState weekly, DateTimeOffset? anchor, DateTimeOffset now)
    {
        if (weekly.Reliability == SourceReliability.Exact && weekly.ResetsAt is not null) return weekly;
        if (anchor is null) return weekly;
        var next = NextReset(anchor.Value, now);
        return weekly with { ResetsAt = next };   // reste Estimated → badge « estimée »
    }
    static DateTimeOffset NextReset(DateTimeOffset anchor, DateTimeOffset now)
    {
        var cycles = Math.Ceiling((now - anchor) / Week);   // 1er reset STRICTEMENT futur
        if (cycles < 1) cycles = 1;
        return anchor + cycles * Week;
    }
}
```
**Dialogue minimal :** petite fenêtre WPF modale (`RecalibrationDialog`) possédée par MainWindow, un `DatePicker` (+ heure optionnelle) → l'utilisateur pointe la date d'un reset hebdo connu ; `Owner` = MainWindow, `WindowStartupLocation=CenterOwner`. Écrit `WeeklyAnchor` dans settings. Rester léger (pas de shell d'options). Alternative acceptable si le DatePicker est jugé lourd : « recaler sur maintenant » via item de menu — mais le DatePicker est plus utile (dérive ~72 h).

### Pattern 8 : Autostart .lnk sans dépendance — DEP-02
Voir Code Examples. `Type.GetTypeFromProgID("WScript.Shell")` + `Activator.CreateInstance` + `dynamic` (COM late-bound, **aucun NuGet**). Cible = `Environment.ProcessPath` (single-file-safe). Dossier = `SpecialFolder.Startup`. `IsEnabled` = `File.Exists(lnk)`. Désactiver = `File.Delete`.

### Anti-Patterns à éviter
- **Positionner via `Window.Left`/`Top` en multi-écrans DPI mixte** → décalages (bug WPF officiel). Utiliser SetWindowPos physique.
- **Toggle `Topmost=false;Topmost=true`** pour réaffirmer → scintillement + réactivation. `TopmostGuard` utilise déjà `SetWindowPos+SWP_NOACTIVATE` : ne pas régresser.
- **Snap dans un handler `MouseLeftButtonUp`** → ne se déclenche pas après DragMove. Snapper au retour de `DragMove()`.
- **Persister X/Y bruts comme unique source** → widget hors-écran au rebranchement. Persister coin+device.
- **Retirer le badge « estimée » après recalibrage** → viole la Core Value. Le recalibrage garde `Estimated`.
- **Mettre `RectD`/logique de snap dans `Chronos.Services`/`Chronos.Models`** avec `System.Windows.Rect` → casse `ServicesLayerPurityTests`. Neutre + namespace `Placement`.
- **`<UseWindowsForms>true</UseWindowsForms>`** pour `Screen` → dépendance et surface évitables ; Win32 P/Invoke suffit.

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser | Pourquoi |
|----------|-------------------|----------|----------|
| Écriture d'un `.lnk` | Parser/écrire le format binaire Shell Link | `WScript.Shell` COM (`dynamic`) | Format .lnk complexe (IShellLink) ; COM late-bound = 6 lignes, zéro NuGet |
| Chemin de l'exe | Concaténer/`Assembly.Location` | `Environment.ProcessPath` | `Assembly.Location` **vide** en single-file ; ProcessPath pointe l'apphost |
| Dossier startup | Chemin en dur `%APPDATA%\...\Startup` | `Environment.GetFolderPath(SpecialFolder.Startup)` | Robuste à la localisation/redirection de profil |
| Zone de travail multi-écrans | `SystemParameters.WorkArea` | `MonitorFromWindow`+`GetMonitorInfo` (rcWork) | `SystemParameters.WorkArea` = **écran PRIMAIRE seulement** (piège explicite CONTEXT) |
| Énumération moniteurs | `System.Windows.Forms.Screen` (+UseWindowsForms) | Win32 `MonitorFromWindow`/`EnumDisplayMonitors` | Évite une dépendance assembly ; cohérent avec `NativeMethods` existant |
| Positionnement DPI-correct | Convertir DIU↔pixels à la main via matrices inter-moniteurs | `SetWindowPos` en **pixels physiques** | Contourne le bug `Window.Left/Top` PerMonitorV2 (dotnet/wpf #4127) |
| Écriture atomique | `File.WriteAllText` direct | temp + `File.Move(overwrite:true)` | Miroir exact du pont Node ; évite un settings.json à moitié écrit |

**Key insight :** presque tout le « code natif » de cette phase se réduit à ~5 P/Invoke user32/shcore et un objet COM late-bound. La complexité réelle est dans le **DPI multi-écrans** et le **cycle de vie du placement** (restore avant Show, repli écran disparu), pas dans les APIs elles-mêmes.

## Common Pitfalls

### Pitfall 1 : `Window.Left`/`Top` cassés en PerMonitorV2 DPI mixte
**What goes wrong :** WPF calcule la **même** position DIU pour des positions physiques différentes quand les moniteurs ont des DPI différents ; poser `Window.Left/Top` place la fenêtre au mauvais endroit (souvent partiellement hors-écran ou sur le mauvais moniteur).
**Why :** conversion DIU↔physique avec le mauvais facteur d'échelle lors du franchissement de moniteur (bugs **officiels** dotnet/wpf #4127 et #3105, non corrigés).
**How to avoid :** positionner via `SetWindowPos` en **pixels physiques** ; calculer les coins sur `rcWork` (physique) du moniteur retourné par `MonitorFromWindow`. Re-snapper sur `Window.DpiChanged`.
**Warning signs :** overlay qui atterrit au centre/hors-écran/sur le mauvais moniteur au restore ou après déplacement inter-moniteurs de DPI différent.

### Pitfall 2 : `SystemParameters.WorkArea` = écran primaire uniquement
**What goes wrong :** utiliser `SystemParameters.WorkArea` pour le snap donne toujours la zone du moniteur **primaire**, donc snap incorrect sur les écrans secondaires.
**How to avoid :** `GetMonitorInfo(...).rcWork` du moniteur courant (déjà exclut la barre des tâches, par moniteur).
**Warning signs :** snap correct sur l'écran principal, faux sur le second (piège explicitement cité dans CONTEXT/PITFALLS).

### Pitfall 3 : snap attendu dans `MouseLeftButtonUp`
**What goes wrong :** aucun snap ne se produit ; le handler MouseUp n'est jamais appelé.
**Why :** `DragMove()` est bloquant et consomme le relâchement.
**How to avoid :** snapper **juste après** le retour de `DragMove()`.

### Pitfall 4 : écran persisté disparu → overlay invisible
**What goes wrong :** au rebranchement, le device name persisté n'existe plus → placement sur des coordonnées hors bureau virtuel → widget introuvable, l'utilisateur doit tuer le process.
**How to avoid :** repli **moniteur primaire + même coin** si device name absent ; hook `WM_DISPLAYCHANGE` (via `HwndSource.AddHook`) pour re-clamper à chaud si le moniteur courant disparaît en cours de session.
**Warning signs :** overlay absent après changement de configuration d'écrans / dock/undock.

### Pitfall 5 : `ContextMenu` et héritage du DataContext
**What goes wrong :** les `Command` du menu ne se déclenchent pas car le `ContextMenu` (hors arbre visuel) n'a pas le VM en DataContext.
**How to avoid :** attacher le `ContextMenu` à un élément dont le DataContext est le VM (héritage) ou binder via `PlacementTarget.DataContext`. Vérifier par UAT que les 4 items agissent (surtout « Quitter »).

### Pitfall 6 : hit-testing sur zones transparentes
**What goes wrong :** le clic droit/gauche « traverse » les zones transparentes du cadran et n'ouvre ni menu ni drag.
**How to avoid :** la zone interactive doit avoir un `Background` non-null (l'Ellipse de fond capte déjà ; sinon `#01000000`). Déjà partiellement géré par le fond du cadran (CLAUDE.md piège hit-testing).

### Pitfall 7 : recalibrage qui « ment »
**What goes wrong :** appliquer l'ancre hebdo alors que la source primaire fournit un `resets_at` exact → on écrase une donnée fiable par une estimation.
**How to avoid :** garde `if (Exact && ResetsAt != null) return unchanged;` (Pattern 7). Le recalibrage **ne s'applique qu'au repli**, badge « estimée » conservé.

## Code Examples

### Étendre `NativeMethods` (interop)
```csharp
// Interop/NativeMethods.cs — additions
public static readonly IntPtr HWND_BOTTOM = new(1);   // envoyer au fond (mode arrière-plan)

public const uint MONITOR_DEFAULTTONEAREST = 2;

[StructLayout(LayoutKind.Sequential)]
public struct RECT { public int Left, Top, Right, Bottom; }

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct MONITORINFOEX
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
}

[DllImport("user32.dll")]
public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);
// cbSize = Marshal.SizeOf<MONITORINFOEX>() AVANT l'appel, sinon échec silencieux.

// DPI d'un moniteur ciblé (restauration vers un moniteur précis). Win 8.1+.
[DllImport("Shcore.dll")]
public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
// dpiType MDT_EFFECTIVE_DPI = 0 ; scale = dpiX / 96.0
```

### Toggle arrière-plan + Suspend/Resume (FEN-05)
```csharp
// Services/TopmostGuard.cs — additions (reste dans l'allow-list purity)
public void Suspend() => _timer.Stop();            // arrête la réaffirmation topmost
public void Resume() { _timer.Start(); Reassert(); } // reprend + réaffirme immédiatement

// Views/OverlayController.cs — adaptateur WPF, impl. IWindowController (À AJOUTER à l'allow-list)
public void SendToBackground()
{
    _window.Topmost = false;
    _guard.Suspend();
    NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0,
        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
}
public void BringToForeground()
{
    _window.Topmost = true;
    _guard.Resume();   // Reassert() repose HWND_TOPMOST sans voler le focus
}
```

### Autostart .lnk sans dépendance (DEP-02)
```csharp
// Services/AutostartService.cs — NEUTRE (aucun type WPF ; System.Object/dynamic)
public sealed class AutostartService : IAutostartService
{
    private static string LinkPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Chronos.lnk");

    public bool IsEnabled() => File.Exists(LinkPath);

    public void Enable()
    {
        var exe = Environment.ProcessPath!;                 // single-file-safe (PAS Assembly.Location)
        var t = Type.GetTypeFromProgID("WScript.Shell")!;   // COM late-bound, aucun NuGet
        dynamic shell = Activator.CreateInstance(t)!;
        var lnk = shell.CreateShortcut(LinkPath);
        lnk.TargetPath = exe;
        lnk.WorkingDirectory = Path.GetDirectoryName(exe);
        lnk.Description = "Chronos — overlay de quotas Claude";
        lnk.Save();
    }

    public void Disable() { if (File.Exists(LinkPath)) File.Delete(LinkPath); }
}
```
> Compatibilité single-file : `dynamic` requiert `Microsoft.CSharp` (présent dans le runtime partagé) → OK en self-contained avec `PublishTrimmed=false` (notre cas). **Incompatible AOT** — mais on n'utilise pas l'AOT (R2R seulement, cf. STACK.md).

### Restauration au lancement (FEN-07) — avant `Show()`
```csharp
// App.xaml.cs — résoudre SettingsService, charger, injecter dans la fenêtre AVANT Show.
// La fenêtre applique le placement en SourceInitialized (HWND dispo, avant 1er rendu → pas de flash).
var settings = _host.Services.GetRequiredService<SettingsService>().Load();
var window = _host.Services.GetRequiredService<MainWindow>();
window.ApplyRestoredState(settings);   // stocke ; SourceInitialized appellera le controller
window.Show();
```

## State of the Art

| Ancienne approche | Approche actuelle | Impact |
|-------------------|-------------------|--------|
| `Window.Left/Top` pour placer | `SetWindowPos` en pixels physiques (multi-écrans DPI mixte) | Fiabilité multi-moniteurs (bug WPF non résolu) |
| `System.Windows.Forms.Screen` | Win32 P/Invoke `MonitorFromWindow`/`GetMonitorInfo` | Pas de dépendance WinForms |
| `IWshRuntimeLibrary` (interop COM référencé) | `dynamic` + `Type.GetTypeFromProgID` | Aucun assembly d'interop à générer/référencer |
| `Assembly.GetExecutingAssembly().Location` | `Environment.ProcessPath` | Correct en single-file (Location vide) |

## Open Questions

1. **Persistance X/Y indicatif vs coin+device seul**
   - Ce qu'on sait : coin+device est robuste ; X/Y bruts fragiles.
   - Incertain : faut-il honorer un X/Y « libre » si l'utilisateur déplace sans snapper ? (Ici snap systématique au relâchement → position toujours un coin → X/Y redondant.)
   - Recommandation : persister coin+device comme vérité, X/Y purement indicatif/diagnostic. Le planner peut ignorer X/Y si simplicité préférée.

2. **Dialogue de recalibrage : DatePicker vs « caler sur maintenant »**
   - Ce qu'on sait : décision = « minimal », discrétion de Claude.
   - Recommandation : `DatePicker` (dérive ~72 h justifie de pointer une date). Repli acceptable : item « recaler maintenant » si le dialogue est jugé hors-budget.

3. **Intervalle de refresh dans settings.json (FEN-07 le liste)**
   - `RefreshOptions.Default` est un Singleton figé (Phase 4). Rendre l'intervalle configurable depuis settings.json exige de câbler `RefreshIntervalSeconds` → `RefreshOptions` au démarrage.
   - Recommandation : persister le champ (schéma prêt) mais n'exposer aucune UI de réglage en Phase 6 (pas d'item menu prévu) ; appliquer la valeur au démarrage seulement. À confirmer au plan (peut être laissé au défaut 60 s sans régresser).

## Environment Availability

| Dépendance | Requise par | Disponible | Version | Repli |
|------------|-------------|------------|---------|-------|
| user32.dll (SetWindowPos/MonitorFromWindow/GetMonitorInfo) | FEN-03/04/05 | ✓ (Windows) | OS | — |
| Shcore.dll (GetDpiForMonitor) | FEN-04 DPI moniteur ciblé | ✓ (Win 8.1+) | OS | `VisualTreeHelper.GetDpi(window)` pour moniteur courant |
| WScript.Shell (COM) | DEP-02 | ✓ (Windows Script Host, présent par défaut) | OS | Écriture .lnk manuelle (non recommandé) |
| `Environment.ProcessPath` | DEP-02 | ✓ | net6+ (on cible net8) | — |
| Manifest PerMonitorV2 | FEN-04 | ✓ **déjà en place** | `app.manifest` | — |
| Second moniteur physique | UAT FEN-04 | ⚠ à confirmer sur la machine de dev | — | Tester DPI mixte si possible ; sinon UAT documenté |

**Manque bloquant :** aucun (toutes les APIs sont natives Windows).
**Manque avec repli :** `GetDpiForMonitor` (Win8.1+) → repli `VisualTreeHelper.GetDpi` pour le moniteur courant ; un **2e écran DPI mixte** pour l'UAT peut manquer sur la machine de dev → prévoir un test unitaire pur (fonction snap) + UAT manuel documenté.

## Validation Architecture

`nyquist_validation` est **activé** (config.json `workflow.nyquist_validation: true`).

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 (+ Xunit.StaFact 1.1.11 pour `[WpfFact]` STA) |
| Config file | `tests/Chronos.Tests/Chronos.Tests.csproj` (`IsTestProject=true`, net8.0-windows, UseWPF) |
| Quick run command | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~<Classe>"` |
| Full suite command | `dotnet test` (68 tests existants — ne rien casser) |

### Phase Requirements → Test Map
| Req | Comportement | Type | Commande automatisée | Fichier ? |
|-----|--------------|------|----------------------|-----------|
| FEN-03 | Coin le plus proche par quadrant + marge | unit pur `[Fact]` | `dotnet test --filter FullyQualifiedName~CornerSnapTests` | ❌ Wave 0 (`CornerSnapTests.cs`) |
| ROB-03 | Recalibrage : Exact→inchangé ; repli+ancre→ResetsAt futur ; reste Estimated | unit pur `[Fact]` | `dotnet test --filter FullyQualifiedName~WeeklyRecalibrationTests` | ❌ Wave 0 |
| FEN-07 | Save atomique + load tolérant (corrompu→défauts, round-trip) | unit `[Fact]` (tmp dir) | `dotnet test --filter FullyQualifiedName~SettingsServiceTests` | ❌ Wave 0 |
| FEN-05 | Suspend arrête le timer / Resume réaffirme HWND_TOPMOST | unit `[WpfFact]` (délégué SetWindowPos capturé, comme TopmostGuardTests) | `dotnet test --filter FullyQualifiedName~TopmostGuardTests` | ⚠ étendre l'existant |
| FEN-05 | SendToBackground pose HWND_BOTTOM + SWP_NOACTIVATE, Suspend appelé | unit `[WpfFact]` (fakes) | `dotnet test --filter FullyQualifiedName~OverlayControllerTests` | ❌ Wave 0 |
| DEP-02 | Enable crée .lnk ciblant ProcessPath ; Disable supprime ; IsEnabled | unit `[Fact]` (Startup redirigé/temp) | `dotnet test --filter FullyQualifiedName~AutostartServiceTests` | ❌ Wave 0 (voir note) |
| Purity | Nouveaux adaptateurs WPF (`OverlayController`) ajoutés à l'allow-list ; types neutres sans WPF | `[Fact]` existant | `dotnet test --filter FullyQualifiedName~ServicesLayerPurityTests` | ⚠ mettre à jour allow-list |
| FEN-02/04/06 | DragMove, placement physique multi-écrans, menu | **manual UAT** | — (nécessite écran réel / 2e moniteur DPI mixte) | Documenté (pas d'auto) |

**Note testabilité DEP-02 :** `AutostartService` écrit dans `SpecialFolder.Startup`. Pour tester sans polluer le vrai dossier, rendre `LinkPath`/le dossier **injectable** (paramètre de ctor, comme `ChronosPaths`) → test dans un répertoire temporaire. La création COM WScript.Shell peut rester couverte par un test d'intégration léger ou un test qui vérifie le chemin/logique (mock du facteur COM). Recommander l'injection du dossier startup pour rester pur.

### Sampling Rate
- **Per task commit :** `dotnet test --filter` de la classe touchée (fonctions pures = <1 s).
- **Per wave merge :** `dotnet test` complet (68 + nouveaux, tout vert).
- **Phase gate :** suite complète verte avant `/gsd:verify-work` ; UAT manuel FEN-02/04/06 documenté (drag, 2 écrans, menu 4 items dont Quitter).

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/CornerSnapTests.cs` — FEN-03 (4 quadrants, marge, clamp)
- [ ] `tests/Chronos.Tests/WeeklyRecalibrationTests.cs` — ROB-03 (garde Exact, calcul NextReset, reste Estimated)
- [ ] `tests/Chronos.Tests/SettingsServiceTests.cs` — FEN-07 (atomique, tolérant, round-trip) + `ChronosPaths.SettingsFile`
- [ ] `tests/Chronos.Tests/OverlayControllerTests.cs` — FEN-05 (HWND_BOTTOM, SWP_NOACTIVATE, Suspend/Resume) via fakes
- [ ] `tests/Chronos.Tests/AutostartServiceTests.cs` — DEP-02 (dossier startup injecté en temp)
- [ ] Étendre `TopmostGuardTests.cs` — Suspend/Resume
- [ ] Mettre à jour `ServicesLayerPurityTests` allow-list — ajouter `OverlayController` (nouvel adaptateur WPF sanctionné)
- Framework install : aucun (xUnit + StaFact déjà présents).

## Sources

### Primary (HIGH confidence)
- Code source du projet (lu intégralement) : `TopmostGuard.cs`, `NativeMethods.cs`, `MainWindow.xaml(.cs)`, `App.xaml.cs`, `MainViewModel.cs`, `WindowGaugeViewModel.cs`, `ClaudeUsageObjectProvider.cs`, `JsonlEstimationProvider.cs`, `ChronosPaths.cs`, `app.manifest`, `ServicesLayerPurityTests.cs`, `TopmostGuardTests.cs`, `install-bridge.mjs` (pattern atomique).
- dotnet/wpf #4127 « Window.Left and Window.Top are broken when using PerMonitorV2 » — https://github.com/dotnet/wpf/issues/4127 — bug de positionnement DPI mixte.
- dotnet/wpf #3105 « Positioning a window in WPF (Top/Left) in PerMonitorV2 » — https://github.com/dotnet/wpf/issues/3105.
- CLAUDE.md / STACK.md / PITFALLS.md / FEATURES.md (recherche projet validée).

### Secondary (MEDIUM confidence)
- SciChart « Window Positioning and DPI Handling in WPF Applications » — https://www.scichart.com/blog/window-positioning-in-wpf-applications/ — confirme SetWindowPlacement/GetMonitorInfo/GetDpiForMonitor comme parade.
- Microsoft Learn « Developing a Per-Monitor DPI-Aware WPF Application » — https://learn.microsoft.com/en-us/windows/win32/hidpi/declaring-managed-apps-dpi-aware.
- Dragablz « Getting Windows Snap to Play with WPF Borderless Windows » — https://dragablz.net/2014/12/16/getting-windows-snap-to-play-with-wpf-borderless-windows/ — comportement DragMove/borderless.

### Tertiary (LOW confidence — à valider en UAT)
- Comportement exact du re-snap sur `Window.DpiChanged` après déplacement inter-moniteurs DPI mixte : à confirmer par UAT sur 2 écrans.

## Metadata

**Confidence breakdown :**
- Standard stack : HIGH — aucune nouvelle dépendance, tout vérifié dans le code existant.
- Architecture / placement physique : HIGH — bug WPF confirmé par issues officielles ; parade éprouvée.
- Autostart COM/ProcessPath : HIGH — APIs .NET documentées, compatibles single-file (trim off).
- Recalibrage hebdo : HIGH (design) — fonction pure alignée sur le pipeline existant ; MEDIUM sur l'ergonomie du dialogue (discrétion Claude).
- Re-snap DPI-change inter-moniteurs : MEDIUM — logique saine, à confirmer par UAT.

**Research date :** 2026-07-08
**Valid until :** ~2026-08-07 (stable ; le bug WPF #4127/#3105 n'est pas près d'être corrigé).
