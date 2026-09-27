# Phase 34 : Fenêtre Historique : Semaine et Jour — Research

**Researched:** 2026-09-27
**Domain:** WPF (.NET 8, `net8.0-windows`) — fenêtre secondaire opaque redimensionnable, rendu `OnRender` par piste, MVVM CommunityToolkit, consommation des services de lecture par plage des phases 32/33
**Confidence:** HIGH sur le code réel (tout lu dans le dépôt), HIGH sur les API WPF citées (docs officielles), MEDIUM sur deux points de rendu (coins arrondis DWM, stratégie du compteur de rendus)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### Décisions de l'utilisateur (verrouillées, 2026-09-27)
- Forme A (Pistes) par défaut ; **B (Simplifié) et C (Tuiles) codées aussi et sélectionnables** (`HistoriqueStyleSemaine` dans
  `settings.json`, sélecteur dans la fenêtre à droite de la ligne de fraîcheur ; la carte des réglages arrive en phase 35).
- Vues Jour (cette phase) et 4 semaines (phase 35) conservées.
- Deux séries de nature différente, jamais fusionnées : % du compte (relevés exacts) et tokens Claude Code ; jamais d'axe ni de
  palette communs ; aucun trou interpolé ; aucune projection.

#### Contrat du plan de design (`.zeus/DESIGN_PLAN.md`)
- **§2 fenêtre** : `Views/HistoriqueWindow.xaml` + `ViewModels/HistoriqueViewModel` ; `WindowStyle=None`, `AllowsTransparency=False`,
  `Topmost=False`, `ShowInTaskbar=True`, coins 16, ombre, redimensionnable (min 760 × 480, défaut 920 × 610), position/taille
  mémorisées (`HistoriqueX/Y/Width/Height`) ; palette `Panel #151322 / Panel2 #1E1B30 / Line #2C2942 / Ink #F2F0FB / Ink2 #A9A6C4 /
  Accent #8B7BF0 / Ok #4EC98A` **promue de `SettingsWindow` vers `Resources/DesignTokens.xaml` sans changer une valeur** ; rampe du
  thème actif via `UtilizationToBrushConverter` ; nouveaux tokens `HistoTokens #8C89A8`, `HistoSousAgent #5A5776`,
  `HistoModele1/2/3 #9D9AB8 / #6E6B8C / #4A4762`, `HistoGris` ; Segoe UI ; corps 16 / 14 / 11,5 / 11 / 10,5 / 9,5 / 9 / 8,5 ; ✕ et Échap.
- **§2.1 en-tête commun** : segment Jour / Semaine / 4 semaines (le bouton « 4 semaines » existe mais renvoie « bientôt » ou est
  désactivé jusqu'à la phase 35 — à trancher au plan, sans tromper), ‹ ›, « Cette semaine » / « Aujourd'hui », ligne de fraîcheur
  « Dernier relevé il y a N min · source · N relevés · N interruptions · journal ouvert le … », pastille `Alerte` + « journal muet
  depuis N min » au-delà de 15 min (source : `IEtatJournal` / `JournalReleves.SeuilMuet`).
- **§2.2 Semaine** : trois styles avec EXACTEMENT les pistes et hauteurs du tableau (Pistes : NIVEAU 150 / RYTHME 72 / TOKENS 72 /
  COUVERTURE 12 ; Simplifié : NIVEAU 200 / TOKENS 90 / COUVERTURE 12 ; Tuiles : NIVEAU 120 / FENÊTRES 5 H 62 / RYTHME 58 / TOKENS 58 /
  COUVERTURE 12) ; X = samedi 00:00 → samedi 00:00 local (`BornesPlage`), grille et libellés des jours, « reset hebdo → » ; réticule
  vertical commun + infobulle (heure, valeur, source, âge) ; éléments d'honnêteté : trou (> 2 cadences) = rectangle `Line` 35 % +
  bordure pointillée (grise « Chronos arrêté », ambre « jeton invalide ») ; saut non localisé = bloc plat gris « +N % pendant
  l'absence (répartition inconnue) » ; marche de % sans tokens Code = cadre pointillé `Accent` + pied « consommé ailleurs (Cowork,
  claude.ai) » ; « journal ouvert le <date> » et zone antérieure vide ; libellé permanent des tokens et pied de page fixe.
- **§2.3 Jour** : 0 h → 24 h locale, grille 3 h, grain 5 min ; NIVEAU 190 (% 5 h au premier plan 2,4 px, gris à 100 % « épuisée à
  100 % — le serveur refuse (statut rejected) », % hebdo trait fin `Ink2`, traits `TickReset` « reset 5 h HH:MM ») ; RYTHME 64 ;
  TOKENS 64 par quart d'heure empilés par modèle (`HistoModele1/2/3`, légende « opus · sonnet · haiku · sous-agents inclus ») ;
  COUVERTURE 12 ; ligne « maintenant » `Ink` 60 % ; ligne de fraîcheur « 288 relevés attendus · N présents · N interruption(s)
  (cause, HH:MM → HH:MM) » ; « Aujourd'hui ».
- **§3 données** : la fenêtre ne reçoit rien du cadran ; elle lit par les services : `LecteurJournal.Lire` + `AnalyseReleves` +
  `BornesPlage` (32-06), `LecteurAgregats.Lire` + `RenduLocalTokens` (33-04), `IEtatJournal` (32-04/05), `IEtatReconstruction`
  (33-03/05 : `Changement` levé sur le thread de fond → `Post` + coalescence côté VM), `IEtatMagasin` des trois magasins.
- **§4 vocabulaire** : Historique · Semaine de forfait · Jour · 4 semaines · Niveau · Rythme · Tokens Claude Code · Couverture ·
  Fenêtres 5 h · relevé · reset 5 h / reset hebdo · trou · « Chronos arrêté » · « jeton invalide » · « sonde refusée » · « épuisée » ·
  « répartition inconnue » · « consommé ailleurs » · « journal ouvert le … » · « dernière écriture » · style : Pistes / Simplifié /
  Tuiles. Une garde sur le vocabulaire interdit toute projection (« épuisé vers », « à ce rythme »).
- **Rendu (HIS-07)** : un `FrameworkElement` par piste, `OnRender` + `StreamGeometry` gelée, réduction min/max par colonne de pixels
  au-delà de 4 000 points, redessin sur changement de données ou de plage — JAMAIS sur le tick 1 s (test) ; géométrie (temps → x,
  valeur → y, binning, trous, tuiles, segments par bande de rampe) en classes PURES de `Rendering/` testées comme `ArcGeometry`.
- **Bandeau F2 (HIS-08)** : « Reconstruction des tokens depuis vos transcripts Claude Code — N / M fichiers · la semaine courante
  est déjà complète », barre `Accent`, sous-texte « en arrière-plan, priorité basse · du plus récent au plus ancien · les
  pourcentages du forfait ne se reconstruisent pas : ils commencent au <date> » ; disparaît à la fin.

### Claude's Discretion
Découpage des contrôles (une piste = un `FrameworkElement` ; conteneur de vue), structure du ViewModel (un VM de fenêtre + un VM
par vue ou un seul), format du réticule/infobulle, mécanique de la galerie `--historique` (fixtures = celles de 32-06/33-04 +
scénarios du DESIGN_PLAN §8 : trous, saut, épuisée, divergence, semaine précédente), stratégie de test du « jamais au tick 1 s »
(compteur de rendus), tout dans les conventions du dépôt.

### Deferred Ideas (OUT OF SCOPE)
- Vue 4 semaines (HIS-05), carte des réglages et double-clic (ACC-01/02), diagnostic (ACC-03), release 3.3.0 et docs (ACC-04),
  constat (VAL-05) → phase 35.
- Heatmap, export CSV, projection conditionnelle, dimension projet → v1.9.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description (résumé de REQUIREMENTS.md) | Research Support |
|----|-------------|------------------|
| HIS-01 | `HistoriqueWindow` opaque non topmost, redimensionnable 760 × 480 → 920 × 610, coins 16, position/taille dans `settings.json`, en-tête commun (segment, ‹ ›, retour au présent, ligne de fraîcheur, pastille `Alerte`), Échap | §Code réel 1 (chrome de `SettingsWindow`, persistance `ChronosSettings`/`SettingsService`, motif GAP-1), Pattern 1 (WindowChrome + DWM), Pattern 6 (ligne de fraîcheur depuis `AnalyseJournal` + `IEtatJournal`), Pitfalls 1–3 |
| HIS-02 | Vue Semaine style Pistes : bornes `BornesPlage`, NIVEAU 150 (escalier hebdo rampe, dents de scie 5 h `TickReset`, tirets de reset, S-1 fantôme), RYTHME 72, TOKENS 72, COUVERTURE 12, réticule + infobulle | §Code réel 4 (signatures réelles de `AnalyseReleves`, `BornesPlage`, `RenduLocalTokens`), Pattern 3 (géométrie pure `Rendering/Historique`), Pattern 4 (pistes `OnRender`), Pattern 7 (réticule en surcouche) |
| HIS-03 | Styles Simplifié et Tuiles, réglage `HistoriqueStyleSemaine`, sélecteur, pistes et hauteurs exactes §2.2 testées | §Code réel 1 (enum de style dans `ChronosSettings`, motif `CadranStyle`), Pattern 5 (hauteurs en tokens `sys:Double` + test de mise en page), Découpage 34-06 |
| HIS-04 | Vue Jour : grain 5 min, % 5 h 2,4 px gris à 100 % (`Statut5 == Rejete`), hebdo `Ink2`, resets observés, RYTHME 64, TOKENS 64 par quart d'heure et par modèle, COUVERTURE 12, ligne « maintenant », fraîcheur « 288 relevés attendus … » | §Code réel 4 (`ParQuartDHeure`, `ResetObserve.Instant`, `StatutServeur.Rejete`), Pattern 7 (« maintenant » dans la surcouche, pas dans les pistes), Pitfall 9 (288 = plage / cadence, 300 le 25/10) |
| HIS-06 | Honnêteté testée mot pour mot : trous, saut non localisé, divergence « consommé ailleurs », « journal ouvert le », libellé permanent des tokens, pied de page fixe, aucune projection | Pattern 8 (`TextesHistorique` producteur unique + textes en `TextBlock` découvrables), Pattern 9 (garde de vocabulaire textuelle), Pitfall 7 (divergence hors des namespaces `Tokens`), fixtures de scénarios |
| HIS-07 | Un `FrameworkElement` par piste, `OnRender` + `StreamGeometry` gelée, réduction min/max > 4 000 points, jamais au tick 1 s (test), géométrie pure testée, couleurs/tailles via `DesignTokens.xaml`, palette promue sans changer une valeur, nouveaux tokens, `ServicesLayerPurityTests` vert | §Code réel 2–3 (contrôles existants, tokens, gardes), Pattern 4, Pattern 5, Validation Architecture (compteur de rendus), Pitfall 4 (`SettingsWindow` doit fusionner `DesignTokens.xaml` après la promotion) |
| HIS-08 | Bandeau F2 pendant la reconstruction, barre `Accent`, sous-texte, disparaît à la fin | §Code réel 4–5 (`IEtatReconstruction` réel, `Changement` sur le thread de fond, D-33-22), Pattern 6 bis (Post + coalescence), `FakeEtatReconstruction.Declencher()` |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Directives extraites de `./CLAUDE.md` (même autorité que les décisions verrouillées) :

- **Stack imposée** : C# / .NET 8 (`net8.0-windows`) / WPF / MVVM CommunityToolkit.Mvvm 8.4.2 / Microsoft.Extensions.DependencyInjection + Hosting 8.0.1. **Aucune dépendance NuGet nouvelle** (vérifié : `Chronos.csproj` n'en a que trois). Rendu XAML pur (`Path`/`ArcSegment`, `OnRender`), pas de SkiaSharp ni de bibliothèque de graphiques.
- **MVVM strict** : `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers `Models/Views/ViewModels/Services` ; code-behind limité au drag / fermeture ; marshaling UI par `IUiDispatcher`.
- **Chemins** sous `%USERPROFILE%` / `%APPDATA%` uniquement, jamais `Assembly.Location` ; **lecture seule stricte** de `~/.claude` et `%APPDATA%\Claude`.
- **Honnêteté** : `utilization`/`resets_at` prioritaires ; jamais une estimation présentée comme exacte ; jamais un % dérivé de tokens.
- **Robustesse** : aucune source ≠ crash → état « données indisponibles » ; parsing tolérant.
- **Langue** : UI et commentaires en français ; activer `frontend-design` + `windows-wpf` sur les tâches UI (skills utilisateur, pas de `.claude/skills/` dans le dépôt — vérifié : le dossier n'existe pas).
- **Pièges overlay/tokens du CLAUDE.md** : `PublishTrimmed=false`, `InvariantGlobalization=false` (le formatage fr-FR des dates compte ici), `app.manifest` PerMonitorV2 (présent), `DynamicResource` pour tout ce que le thème peut écraser.
- **Workflow GSD** : pas d'édition hors `/gsd:execute-phase` ; l'agent **ne lance jamais l'overlay ni la galerie** ; TDD par plan, mutations révoquées par copie, suite complète deux fois, zéro warning (DoD `.zeus/DOD.md`).

## Summary

La phase 34 n'a **rien à inventer côté données** : `LecteurJournal.Lire(dossier, de, a)`, `AnalyseReleves.Analyser(lecture, now, cadence, source?)`, `BornesPlage.{SemaineDeForfait, Jour}`, `LecteurAgregats.Lire(dossier, de, a)`, `RenduLocalTokens.{ParHeure, ParQuartDHeure, PartSousAgents}`, `IEtatJournal`, `IEtatReconstruction` existent, sont purs, neutres et testés sur 7 + 4 fixtures. Ce qui manque est une **façade de lecture** (une interface neutre `ISourceHistorique` qui enchaîne bornes → journal → analyse → agrégats → rendu local, hors thread UI) et deux calculs croisés absents des phases 32/33 : la **semaine précédente** (une seconde lecture décalée d'une semaine locale) et la **divergence** « marche de % sans tokens Code » (croisement `DeltasHebdo` par heure × `BarreHeure.Principal.N + SousAgents.N == 0` avec état `Couverte`) — ce dernier doit vivre **hors** des namespaces `*.Historique.Tokens` (la garde TOK-05 y interdit tout `double`).

Côté rendu, le dépôt a déjà le moule : `Controls/Cadrans/{TideColumn, FuseBar, EmberRingControl}` sont des `FrameworkElement` à `OnRender` avec DP `AffectsRender`, pinceaux gelés et `Pen.DashStyle` ; `CadranVoletsView.xaml` a une hachure `DrawingBrush` 4 × 4. La règle « jamais au tick 1 s » se tient structurellement : la fenêtre **n'a aucun tick 1 s** (elle ne reçoit rien du cadran) ; un `DispatcherTimer` 60 s ne rafraîchit que les textes de fraîcheur et la **surcouche** réticule/« maintenant » ; les pistes ne changent de DP que sur nouvelle lecture (référence de record) ou changement de plage/style. Trois pièges de chrome sont décisifs : (1) `AllowsTransparency=False` + `WindowStyle=None` ne donne ni coins ronds ni ombre par lui-même → `WindowChrome` (PresentationFramework, `System.Windows.Shell`) pour les bords de redimensionnement, et `DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2)` en best-effort sur Windows 11 ; (2) après promotion de la palette, **`SettingsWindow` doit fusionner `DesignTokens.xaml` au niveau fenêtre** (pack URI, comme `MainWindow`), sinon ses `StaticResource` échouent dans les smoke tests qui la construisent sans `Application` ; (3) `UtilizationToBrushConverter` utilise la rampe **par défaut** (`RampColor.Interpolate(u)` = stops Minuit), pas le thème actif — pour « même loi que le cadran », la couleur d'un niveau doit venir de `ChronosTheme.ArcColor(u)` du thème persisté.

**Primary recommendation:** construire dans l'ordre tokens ∥ géométrie pure ∥ (façade neutre + VM) → pistes ∥ coquille de fenêtre + galerie `--historique` sur scénarios → vue Semaine ∥ vue Jour → honnêteté de bout en bout ; une seule source des mots (`TextesHistorique`) et des couleurs (`DesignTokens.xaml` + `ChronosTheme`), les hauteurs §2.2 en tokens `sys:Double` vérifiées par un test de mise en page réel.

## Standard Stack

### Core (déjà dans le dépôt — rien à installer)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK / TFM | SDK 10.0.201 installé, cible `net8.0-windows` | build/tests | vérifié `dotnet --list-sdks` ; le csproj cible net8.0-windows |
| WPF (`UseWPF`) | intégré | fenêtre, `OnRender`, `WindowChrome` (`System.Windows.Shell`, PresentationFramework.dll) | aucune dépendance ; `WindowChrome` est dans PresentationFramework — pas de NuGet |
| CommunityToolkit.Mvvm | 8.4.2 | `[ObservableProperty]`, `[RelayCommand]`, `ObservableObject` | convention du dépôt (`MainViewModel`, `SessionsViewModel`, `CadranPreviewViewModel`) |
| Microsoft.Extensions.Hosting / DI | 8.0.1 | enregistrement `HistoriqueViewModel`, `ISourceHistorique` | `App.ConfigureServices` |
| System.Text.Json | intégré | déjà utilisé par `SettingsService` (`JsonStringEnumConverter`) | l'enum `HistoriqueStyleSemaine` se sérialise en texte gratuitement |
| xunit 2.9.2 + Xunit.StaFact 1.1.11 | tests | `[Fact]` pour le pur, `[WpfFact]` (70 usages) pour ce qui touche WPF, `[Collection("XAML WPF")]` pour tout chargement de BAML | `XamlWpfCollection.cs` (course du chargeur BAML) |

### Supporting (API WPF à employer)
| API | Purpose | When to Use |
|-----|---------|-------------|
| `System.Windows.Shell.WindowChrome` (`CaptionHeight=0`, `ResizeBorderThickness=6`, `GlassFrameThickness=0`, `UseAeroCaptionButtons=False`) | bords de redimensionnement invisibles sur une fenêtre `WindowStyle=None` opaque | `HistoriqueWindow` (HIS-01) |
| `DwmSetWindowAttribute` (dwmapi.dll, attribut 33 `DWMWA_WINDOW_CORNER_PREFERENCE`, valeur 2 `DWMWCP_ROUND` ; optionnel 34 `DWMWA_BORDER_COLOR`) | coins arrondis + ombre système, Windows 11 build ≥ 22000 | `SourceInitialized`, best-effort (try/catch, no-op ailleurs) |
| `StreamGeometry` + `StreamGeometryContext` + `Freeze()` | géométrie légère des escaliers / barres / tuiles, construite une fois par changement de données | dans `OnRender` des pistes (ou mise en cache par DP) |
| `DrawingContext.{DrawGeometry, DrawRectangle, DrawLine, PushGuidelineSet, PushClip, PushOpacity}` | dessin des pistes | `OnRender` |
| `GuidelineSet` + `RenderOptions.EdgeMode=Aliased` (sur les lignes de grille) | netteté 1 px à 100 % et 150 % | grille, couverture, tirets de reset |
| `Pen.DashStyle` (`new DashStyle(new[]{3,2},0)`) gelé | bordures pointillées (trous, cadre `Accent` de divergence, S-1 fantôme) | pistes |
| `DrawingBrush` tuilé (`Viewport 0,0,4,4 Absolute`, `LineGeometry 0,4→4,0`) | hachure des trous (précédent `CadranVoletsView.GrainHatch`) | trous, zone « avant le journal » |
| `RenderTargetBitmap.Render(visual)` | forcer un rendu hors écran en test (bypass de `IsRenderable`, source WPF) | tests de pistes |
| `UseLayoutRounding=True`, `SnapsToDevicePixels=True` sur la fenêtre | alignement des `TextBlock`/bordures XAML au pixel | racine de `HistoriqueWindow` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `WindowChrome` | `ResizeMode=CanResizeWithGrip` seul | grip seulement (pas de bords), et `WindowStyle=None` + `CanResize` laisse parfois un bandeau non-client résiduel ; `WindowChrome` est le mécanisme documenté pour « conserver les comportements système sans le chrome » |
| DWM coins ronds | `AllowsTransparency=True` + `Border CornerRadius=16` + `DropShadowEffect` (comme `SettingsWindow`) | **interdit par le contrat** (`AllowsTransparency=False` : pas de fenêtre layered, pas de rendu logiciel) |
| Textes dessinés en `FormattedText` dans `OnRender` | `TextBlock` XAML bindés à `TextesHistorique` | les `TextBlock` sont découvrables par les smoke tests (`TousLesTextBlocks`) et prennent les tokens de police ; garder `OnRender` pour la géométrie seule |
| `Popup` pour l'infobulle | `Border` positionné dans la surcouche | un `Popup` est un HWND séparé (passe au-dessus d'autres fenêtres, focus) ; le `Border` dans le `Canvas` de la surcouche suffit et se teste en arbre visuel |
| Un VM par vue | un `HistoriqueViewModel` unique + records immuables `DonneesSemaine` / `DonneesJour` | un seul point de marshaling, une seule persistance ; les vues sont des `UserControl` sans logique |

**Installation:** aucune. `dotnet build Chronos.sln -c Debug` doit rester à 0 warning.

## Code réel — ce que le planner doit savoir (lecture seule du dépôt, HIGH)

### 1. Fenêtres et chrome existants

- **`Views/SettingsWindow.xaml`** : `WindowStyle=None AllowsTransparency=True Background=Transparent ResizeMode=NoResize SizeToContent=WidthAndHeight ShowInTaskbar=False Topmost=True`. Ressources **locales** : 7 `SolidColorBrush` (`Panel #151322`, `Panel2 #1E1B30`, `Line #2C2942`, `Ink #F2F0FB`, `Ink2 #A9A6C4`, `Accent #8B7BF0`, `Ok #4EC98A`) + styles `Section` (TextBlock 10,5 SemiBold `Ink2`), `Flat` (Button, `Panel2`, CornerRadius 9, survol `#26223A`), `Switch` (ToggleButton, piste `#2C2942`, bouton `#6B6890`), `ThemeCardBtn` et `StyleChip` (bordure survol `#3B3860`, sélection `Accent` + fond `#241F3C`), `BoolToVis`. Racine : `Border Background=Panel CornerRadius=16 BorderBrush=Line BorderThickness=1 Width=330` + `DropShadowEffect`. Le fichier compte **19 littéraux hexadécimaux** (`#151322, #1E1B30, #221C33, #241F3C ×2, #26223A, #2C2942 ×2, #38C08A, #3B3860 ×2, #4EC98A, #6B6890, #8B7BF0, #A9A6C4, #E8907F, #ECA23C, #F2F0FB, #F4F2EC`) — D-32-23 le notait comme critère d'acceptation (`grep -c`), **aucun test ne le compte** (vérifié par grep de `tests/`). Après promotion des 7 brosses, il en restera 12 : à écrire dans le SUMMARY, pas à préserver.
- **`SettingsWindow.xaml.cs`** : `DataContext = MainViewModel` ; `Deactivated += Close` (popover) ; `Header_Drag` → `DragMove()` ; `Close_Click`. **Pas de gestion d'Échap** ; l'`HistoriqueWindow` ne doit PAS se fermer sur `Deactivated`.
- **`MainWindow.xaml`** fusionne `DesignTokens.xaml` **au niveau fenêtre** (`<ResourceDictionary Source="pack://application:,,,/Chronos;component/Resources/DesignTokens.xaml"/>`, commentaire : « les StaticResource résolvent sans dépendre d'une Application démarrée ») et applique le thème par `Resources[kv.Key] = kv.Value` sur `theme.BrushTokens()` (`ApplyThemeBrushes`, aussi à `viewModel.ThemeChanged`). `SessionsWindow.ApplyThemeBrushes` fait pareil avec `SessionBrushTokens()`.
- **`SessionsWindow`** : construite par `Views/SessionsController` (`new SessionsViewModel(_monitor, _clock, _archive, _treated)` puis `new SessionsWindow(vm)`), position restaurée depuis `SessionsX/Y` (`double?`), sinon 80/80 ; `LocationChanged += PersistPosition` ; persistance **GAP-1** : `_settings.Save(mutate(_settings.Load()))`. C'est le modèle d'un contrôleur de fenêtre secondaire (phase 35) et de la persistance.
- **Galeries** : `SessionsGalleryWindow()` fait `DataContext = new SessionsPreviewViewModel()` et injecte `ThemeCatalog.Default.SessionBrushTokens()` dans `Resources` ; `CadranGalleryWindow()` idem avec `CadranPreviewViewModel`. **`App.OnStartup`** : les modes CLI sont testés **avant le mutex mono-instance et avant le Host** ; branche `--sessions` = `base.OnStartup(e); var gallery = new SessionsGalleryWindow(); MainWindow = gallery; gallery.Show(); return;` — aucune réconciliation de `~/.claude/settings.json`, aucun service hébergé, multi-instances par construction. `--historique` copie cette branche.
- **Persistance** : `ChronosSettings` est un `record` à propriétés `init` (`SessionsX/Y : double?`, enums `CadranStyle`, `SessionStyle` sérialisés en texte par `JsonStringEnumConverter`) ; `SettingsService.Load()` tolérant, `Save()` atomique (temp + `File.Move`). Un champ absent d'un ancien `settings.json` prend sa valeur par défaut → ajouter `HistoriqueStyleSemaine` (enum `{ Pistes, Simplifie, Tuiles }`, défaut `Pistes`) et `HistoriqueX/Y/Width/Height : double?` **sans migration**. `SettingsServiceTests` + fixture `settings-legacy-plafonds.json` est le précédent.
- **DI** : `AddSingleton<MainViewModel>()` sans fabrique ; le conteneur injecte les paramètres optionnels (`IEtatJournal? journal = null`, `IEtatReconstruction? reconstruction = null`, en dernière position). `CompositionRootTests` est un **miroir** des lignes d'`App.xaml.cs` (à étendre quand on enregistre `HistoriqueViewModel` / `ISourceHistorique`), et `GardesPerimetreTests` porte des gardes textuelles sur `App.xaml.cs` (`CheminSources()` = chemin injecté par `AssemblyMetadata CheminSourcesChronos`).
- **DPI** : `app.manifest` PerMonitorV2 ; `MainWindow` écoute `DpiChanged` et lit `VisualTreeHelper.GetDpi(this)`. `Interop/NativeMethods.cs` : user32 (`SetWindowPos`, `MonitorFromWindow`, `GetMonitorInfo`, `GetWindowRect`, `EnumDisplayMonitors`) + Shcore `GetDpiForMonitor` ; **pas encore de dwmapi**.

### 2. Rendu existant

- **`Controls/RingArc.cs`, `TickRing.cs`** : `Shape` + `DefiningGeometry` (pas `OnRender`) ; DP avec `FrameworkPropertyMetadataOptions.AffectsRender` ; `TickRingTests` lit `DefiningGeometry` par réflexion en `[WpfFact]`.
- **`Controls/Cadrans/{TideColumn, FuseBar, EmberRingControl, FlapRow}.cs`** : le vrai moule des pistes — `sealed class X : FrameworkElement`, DP `AffectsRender` (`Fraction`, `QuotaBrush`, `TrackBrush`…), `OnRender(DrawingContext dc)` avec `dc.DrawRoundedRectangle`, `PushClip`, `DrawEllipse`, `Pen { DashStyle = new DashStyle(new double[]{3,2},0) }` gelé, brosses gelées (`Frozen(r,g,b)`), garde `if (w <= 0 || h <= 0) return;`. **Défaut à ne pas reproduire** : ils portent des couleurs par défaut en dur (`Frozen(0x14,0x10,0x19)`) — les pistes de l'historique prennent TOUTES leurs brosses par DP bindées aux tokens (défaut `Brushes.Transparent` ou `null` = ne rien dessiner).
- **`Rendering/`** : `ArcGeometry` (statique, `Point`/`Geometry` WPF, Y inversé), `RampColor` (`Color` WPF, 3 stops, `Interpolate(u, g, a, r)`), `DayTimeline` (pur, sans WPF, `DateTimeOffset` local fourni par l'appelant). Le namespace `Chronos.Rendering` **n'est pas** sous `ServicesLayerPurityTests` (qui ne vise que `Chronos.Services*` et `Chronos.Models*`) : les classes pures de géométrie peuvent rendre des `Point`/`Rect` WPF ou des doubles nus — préférer des **doubles nus / records neutres** (testables en `[Fact]`, pas de STA).
- **Tick 1 s du cadran** : `MainWindow.Loaded → viewModel.StartClock()` crée un `DispatcherTimer` 1 s côté UI → `Interpolate(now)` pur ; les données arrivent par `RefreshOrchestrator.SnapshotChanged` (thread pool) → `_ui.Post(ApplySnapshot)` toutes les 60 s. **La fenêtre Historique n'est pas abonnée à l'orchestrateur** (§3 : elle ne reçoit rien du cadran) — elle n'a donc **aucun tick 1 s** ; le test « jamais au tick » porte sur son propre `Tick(now)` (60 s) et sur les DP des pistes.
- **Rampe** : `UtilizationToBrushConverter.Convert` → `Neutre #6E6D7A` (null), `Epuise #5A5960` (≥ 1), sinon `RampColor.Interpolate(u)` **avec les stops par défaut** (`#7BB13C → #EFA23A → #D8503A`, thème Minuit). Le cadran, lui, colore par `WindowGaugeViewModel.SetTheme(theme)` → `theme.ArcBrush(u)` (`ChronosTheme.ArcColor` : `Neutre`, `Epuise`, `RampColor.Interpolate(u, RampGreen, RampAmber, RampRed)`). **Pour « même loi que le cadran », utiliser `ChronosTheme.ArcColor` du thème persisté (`ThemeCatalog.ByKey(settings.ThemeKey)`)**, pas le converter (voir Open Question 1).
- **`ChronosTheme.BrushTokens()`** rend 12 clés (`FondCadran, Rim, TickMineur, TickMajeur, TickVisible, Piste5h, PisteHebdo, Piste24h, TextePrincipal, TexteSecondaireClair, TexteSecondaire, Alerte`) ; `ThemingTests` vérifie seulement que `Alerte` existe dans les 9 thèmes et vaut `RampAmber`. **Aucun test n'exige la parité entre les clés de `DesignTokens.xaml` et `BrushTokens()`** : ajouter des tokens au XAML ne casse rien ; ne pas ajouter les tokens `Histo*`/`Panel*` à `BrushTokens()` (le chrome est « toujours sombre, indépendant du thème », commentaire de `SettingsWindow`).

### 3. Tokens

- **`Resources/DesignTokens.xaml`** : 14 `SolidColorBrush` (`FondCadran, Rim, TickMineur, TickMajeur, TickVisible, TickReset #F4F2EC, Piste5h, PisteHebdo, Piste24h, TextePrincipal, TexteSecondaireClair, TexteSecondaire #A9A8B2, Alerte #EFA23A`) + `UtilBrush` (converter) + `BoolToVis`. En-tête : « dictionnaire autonome pour être chargeable à la fois par App.xaml (MergedDictionaries) et par les smoke tests (pack URI) ». Fusionné par `App.xaml` ET par `MainWindow.xaml`. **Aucun test ne lit `DesignTokens.xaml` directement** (grep `DesignTokens` dans `tests/` : vide) ; il n'existe **aucun token de taille** (les `FontSize` sont littéraux dans les vues).
- **Gardes textuelles existantes à ne pas rougir** : `GardeTokensSansPourcentageTests` (réflexion + texte sur `Services/Historique/Tokens` et `Models/Historique/Tokens` : aucun `double|float|decimal`, `/ 100`, `* 100`, `Utilization`, même en commentaire) ; `GardesDoctrineTests.Aucun_rapport_entre_un_comptage_de_tokens_et_un_plafond_dans_Services_et_Models` (regex `Tokens?\w*\s*[/*]\s*\w*(Plafond|Budget|Limite|Capacite)` récursif sur `Services/` et `Models/`) ; `ServicesLayerPurityTests` (aucun assembly WPF dans les signatures publiques sous `Chronos.Services*`/`Chronos.Models*`) ; `GardesPerimetreTests` (textuelles sur `App.xaml.cs`, `SessionStyles.xaml`…) ; `LibellesSessionsTests` (balaie `Views/SessionsWindow.xaml`, `SessionsGalleryWindow.xaml`, `SessionsController.cs`).
- **Promotion** : déplacer les 7 brosses dans `DesignTokens.xaml` (mêmes clés, mêmes valeurs), retirer les 7 lignes locales de `SettingsWindow.xaml` et **ajouter dans `SettingsWindow.xaml` la fusion `pack://application:,,,/Chronos;component/Resources/DesignTokens.xaml`** (voir Pitfall 4). Nouveaux tokens couleur : `HistoTokens #8C89A8`, `HistoSousAgent #5A5776`, `HistoModele1 #9D9AB8`, `HistoModele2 #6E6B8C`, `HistoModele3 #4A4762`, `HistoGris` (valeur à trancher — Open Question 2). Nouveaux tokens de taille (`xmlns:sys="clr-namespace:System;assembly=mscorlib"`, `<sys:Double x:Key="…">`) : corps de police (16 / 14 / 11,5 / 11 / 10,5 / 9,5 / 9 / 8,5), hauteurs de pistes §2.2 (150, 200, 120, 62, 72, 58, 90, 12, 190, 64), épaisseurs (2,2 ; 2,4 ; 1 ; 1,5 ; 8), opacités (0,35 ; 0,55 ; 0,60), rayon 16, tailles min/défaut de fenêtre (760, 480, 920, 610).

### 4. Données — signatures RÉELLES

```csharp
// Services/Historique/LecteurJournal.cs (statique, neutre, ne lève jamais)
LectureJournal LecteurJournal.Lire(string dossier, DateTimeOffset de, DateTimeOffset a);
// → Releves (toutes sources, triés, [de,a[), Evenements, LignesIgnorees, JournalOuvertLe (première ligne valide du plus ancien fichier), Plage
// Coût : ouvre les seuls mois UTC chevauchants + le(s) plus ancien(s) fichier(s) jusqu'au premier lisible (JournalOuvertLe).

// Services/Historique/AnalyseReleves.cs (pur)
AnalyseJournal AnalyseReleves.Analyser(LectureJournal lecture, DateTimeOffset now, TimeSpan cadence, SourceUsage? source = null);
// → Source, Serie (UNE source), Trous(Debut, Fin?, Cause), Resets5h/ResetsHebdo(Fenetre, Instant = ancienne borne, ObserveA),
//   Deltas5h/DeltasHebdo(Fenetre, De, A, Delta, ResetsAt, Anormal), Sauts(Fenetre, Trou, Avant?, Apres?, Delta?), JournalOuvertLe, Plage
// cadence au site d'appel = RateLimitHeaderUsageProvider.CadenceNominale (300 s) → seuil de trou = JournalReleves.SeuilReprise (10 min)

// Services/Historique/BornesPlage.cs (pur, fuseau injecté)
Plage BornesPlage.SemaineDeForfait(DateTimeOffset instant, DateTimeOffset? resetHebdoObserve, DateTimeOffset? ancre, TimeZoneInfo tz);
Plage BornesPlage.Jour(DateTimeOffset instant, TimeZoneInfo tz);
IReadOnlyList<Plage> BornesPlage.QuatreSemaines(DateTimeOffset instant, DateTimeOffset? resetHebdoObserve, DateTimeOffset? ancre, TimeZoneInfo tz);
TimeZoneInfo BornesPlage.FuseauParisPourTests();
// Plage(Debut, Fin) : [Debut, Fin[, Duree, Contient(t). 169 h / 167 h aux changements d'heure, jour de 25 h le 25/10/2026.

// Services/Historique/Tokens/LecteurAgregats.cs (statique, neutre, entiers seulement)
LectureAgregats LecteurAgregats.Lire(string dossier, DateTimeOffset de, DateTimeOffset a);
// → Tranches (TrancheTokens: Slot UTC, Model, Sub, In, Out, CacheW, CacheR, N), LignesIgnorees, Plage,
//   Couverture (sous-plages SousPlageCouverture(Debut, Fin, Etat)), PlusAncienneLigneVue ; EtatA(instant)
// ⚠ ne rend PAS l'instance CouvertureTokens ; ParHeure/ParQuartDHeure en ont besoin → CouvertureTokens.Charger(Path.Combine(dossier, CouvertureTokens.NomFichier))

// Services/Historique/Tokens/RenduLocalTokens.cs (pur, fuseau injecté)
IReadOnlyList<BarreHeure> ParHeure(IReadOnlyList<TrancheTokens> tranches, Plage plage, TimeZoneInfo tz, CouvertureTokens couverture);
// BarreHeure(DebutUtc, Libelle "HH:mm" local, Principal: TotauxTokens, SousAgents: TotauxTokens, Etat: EtatCouverture) — 168/169/167 barres pour une semaine
IReadOnlyList<ColonneQuartDHeure> ParQuartDHeure(...);   // ColonneQuartDHeure(Slot, Libelle, ParModele: PartModele(Model, Totaux) triées Out desc, Etat) — 96 colonnes (100 le 25/10)
(TotauxTokens SousAgents, TotauxTokens Principal) PartSousAgents(IEnumerable<TrancheTokens>);
// TotauxTokens(In, Out, CacheW, CacheR, N) — jamais additionnés entre eux ; EtatCouverture { HorsCouverture, TranscriptsAbsents, Couverte }

// États
interface IEtatJournal { string Dossier; DateTimeOffset? DerniereEcriture; string? DerniereErreur; int RelevesEcrits; }
// JournalReleves.SeuilMuet = 3 × CadenceNominale = 15 min ; règle D-32-21 : alerte si now − max(démarrage, DerniereEcriture ?? démarrage) > SeuilMuet
interface IEtatReconstruction { PhaseReconstruction Phase; int FichiersTraites; int FichiersTotal; int FichiersOuvertsDernierePasse;
  bool SemaineCouranteDisponible; string? DernierFichier; string? DerniereErreur; int FichiersDisparus; int LignesIgnorees; int IdsConnus;
  TimeSpan? DureeMurDernierePasse; TimeSpan? DureeCpuProcessusDernierePasse; DateTimeOffset? DerniereReconstructionTerminee;
  event EventHandler? Changement; }   // levé SUR LE THREAD DE FOND, ≈ une fois par fichier
// PhaseReconstruction { JamaisLancee, Reconstruction, Incremental, Arretee, EnEchec }
interface IEtatMagasin { string Nom; string Chemin; DateTimeOffset? DerniereEcriture; string? DerniereErreur; }  // LastExactStore, JournalReleves, MagasinAgregats
// ReleveJournal(T, Source, U5?, R5?, Statut5?, U7?, R7?, Statut7?, Overage?, OverageStatut?) ; StatutServeur { Autorise, AutoriseAvertissement, Rejete, NonReconnu }
// Textes existants : CauseTrouTexte.Libelle(cause) → « Chronos arrêté » / « jeton invalide » / « sonde refusée » / « cause inconnue » ;
//   LibelleSource.Anciennete(t, now) → « à l'instant » / « il y a N min » / « il y a N h MM » / « il y a N j » ; LibelleSource.Format(source) ;
//   TokenFormatter.Format(long) → « ≈ 1,2 M tokens » ; LigneAgregat.Perimetre = « Claude Code seulement — hors Cowork et claude.ai ; bruts, non pondérés »
// Chemins : ChronosPaths.HistoriqueDir (= %APPDATA%\Chronos\historique), ChronosPaths.ProjectsRoot ; JournalReleves.NomFichier(t), MagasinAgregats.NomFichier(mois)
```

**Ce qui manque pour les pistes (à construire en phase 34) :**
1. **Repère de la semaine** : `resetHebdoObserve` = `R7` du **dernier relevé** — lire d'abord `[now − 7 j, now]` (ou la semaine provisoire par l'ancre) et prendre `Serie[^1].R7`, puis calculer la semaine ; repli `ChronosSettings.WeeklyAnchor`.
2. **Semaine précédente** : seconde lecture de `SemaineDeForfait(courante.Debut − 1 tick, repère, ancre, tz)` puis `Analyser` ; superposition par fraction locale de la plage (voir Pitfall 10).
3. **Divergence** « marche de % sans tokens Code » : par heure de la plage, `Σ DeltasHebdo (ou Deltas5h) dont A ∈ heure` ≥ seuil (une granularité, 0,01) ET `BarreHeure.Principal.N + BarreHeure.SousAgents.N == 0` ET `BarreHeure.Etat == Couverte` (une heure « hors couverture » ou « transcripts absents » ne peut pas accuser) → cadre `Accent` pointillé. À écrire dans `Rendering/Historique/Divergences.cs` (hors `*.Tokens`, hors regex doctrine).
4. **Fraîcheur** : « Dernier relevé il y a N min » = `LibelleSource.Anciennete(analyse.Serie[^1].T, now)` ; « source » = `LibelleSource.Format(analyse.Source)` ; « N relevés » = `Serie.Count` ; « N interruptions » = `Trous.Count` ; « journal ouvert le » = `JournalOuvertLe` en fr-FR ; « journal muet depuis N min » = règle D-32-21 sur `IEtatJournal` (copier la logique de `MainViewModel.MajTexteEtatJournal`, l. 469-505).
5. **Épuisée** (Jour) : `ReleveJournal.Statut5 == StatutServeur.Rejete` ou `U5 >= 1.0` → segment gris `HistoGris` + libellé.
6. **Tuiles (C)** : une tuile par valeur distincte de `R5` dans la série : `x0 = T du premier relevé portant ce R5`, `x1 = min(R5, Plage.Fin)`, hauteur = `max U5`, grise si `max U5 ≥ 1` ou `Statut5 == Rejete`, tiret de reset à `R5` s'il est dans la plage (`ResetObserve.Instant` = la même borne).
7. **Coût mesuré/estimé** : une semaine ≈ 2 016 relevés × 216 o ≈ 435 Ko, un fichier mensuel ≈ 2,3 Mo (D-32-20) ; agrégats de tokens : quelques milliers de lignes par mois ; lecture + analyse + rendu local < 200 ms attendus, **toujours hors UI** (`Task.Run` puis `IUiDispatcher.Post`). `JournalOuvertLe` relit le plus ancien fichier à chaque `Lire` : le mémoriser dans le VM pour la durée d'ouverture de la fenêtre.

### 5. MVVM — conventions

- `sealed partial class X : ObservableObject`, `[ObservableProperty] private T _x;`, `partial void OnXChanged`, `[RelayCommand]` ; horloge `IClock` et `IUiDispatcher` injectés ; **jamais de `DispatcherTimer` dans le ctor** (Pitfall 4 historique : les tests de VM sont en `[Fact]` simple) → `StartClock()` appelé par la vue dans `Loaded`. Les VM **peuvent** référencer `System.Windows.Media.Brush` (`SessionsViewModel`, `WindowGaugeViewModel`) — ce ne sont pas des types neutres au sens de la garde, mais rester neutre là où c'est gratuit (préférer exposer des `double`/records et laisser les pistes choisir la brosse par token).
- Frontière de thread unique : `orchestrator.SnapshotChanged += (s, snap) => _ui.Post(() => Apply(snap))` ; `FakeUiDispatcher { OnUiThread, PostCount }` exécute inline.
- VM de fenêtre secondaire : **construit par `new` dans un contrôleur `Views/*Controller`** à partir de services résolus (modèle `SessionsController`), ou **singleton DI** (modèle `MainViewModel`). Recommandation phase 34 : `AddSingleton<ISourceHistorique>` + `AddSingleton<HistoriqueViewModel>` (un seul abonnement à `Changement`, état de navigation conservé entre deux ouvertures) ; la fenêtre reste construite par `new HistoriqueWindow(vm)` (galerie en 34, contrôleur en 35).
- `IEtatReconstruction.Changement` : `MainViewModel` **ne s'y abonne pas** (D-33-22, relecture au tick) ; le contrat de la phase 34 demande `Post` + coalescence → un `Interlocked` drapeau (Pattern 6 bis) ; `FakeEtatReconstruction.Declencher()` lève l'événement sur le thread appelant.

### 6. Tests UI possibles sans lancer l'app

- **Smoke XAML** : `[Collection("XAML WPF")]` obligatoire (course `WpfXamlType.FindKnownMember`) + `[WpfFact]` ; `ReglagesBindingTests.MonterReglages` montre le rituel : `TempPaths()` (jamais le vrai `%APPDATA%`), construire le VM sur fakes, `new SettingsWindow(vm)`, `racine = (FrameworkElement)fenetre.Content; racine.DataContext = vm; racine.Dispatcher.Invoke(() => {}, DispatcherPriority.ApplicationIdle); racine.Measure(...); racine.Arrange(...)`, puis parcours de l'arbre **visuel** (`TousLesTextBlocks`) pour vérifier textes/visibilités/commandes. Une fenêtre jamais affichée n'applique pas son template : poser le DataContext sur la racine du contenu, sinon les bindings ne s'évaluent pas et les tests sont verts pour de mauvaises raisons.
- **Géométrie pure** : `ArcGeometryTests`, `DayTimelineTests`, `RampColorTests` en `[Fact]`/`[WpfFact]` selon les types ; `TickRingTests` lit la géométrie par réflexion.
- **Rendu forcé** : `RenderTargetBitmap.Render(visual)` après `Measure`/`Arrange` rend un visuel hors écran (le code WPF documente que `RenderTargetBitmap.Render` contourne la garde `IsRenderable`). Un compteur `internal int RendusPourTests` incrémenté dans `OnRender` (`InternalsVisibleTo Chronos.Tests` existe) est observable ainsi.
- **Gardes textuelles** : `GardesPerimetreTests.CheminSources()` (chemin `src/Chronos` injecté par MSBuild) ; motif « anti-mutisme » (asserter que ≥ N fichiers sont vus avant d'asserter l'absence d'un motif). Le chemin `.zeus/` n'est **pas** injecté (seulement `CheminSourcesChronos` et `CheminDocsChronos`) : une garde qui lirait `DESIGN_PLAN.md` exigerait un nouvel `AssemblyMetadata` dans `Chronos.Tests.csproj`.
- **Fixtures disponibles** : journal (`TestData/journal/`) `trou-arrete` (10 l. : trou 10:10Z→11:03Z « Chronos arrêté », saut 5 h +0,06, hebdo +0,02), `trou-jeton` (8 l.), `reset-5h-milieu-heure` (12 l.), `deux-resets-hebdo` (17 + 6 l.), `jour-dst-2026-10-25` (26 l.), `a-cheval` (1 + 1), `tolerance` (9 l.) ; `FabriqueJournal.JourneeNominale(minuitUtc, u5Depart, pas)` fabrique 288 relevés par le vrai écrivain dans un dossier temp ; tokens (`TestData/tokens/`) `dst-2026-10-25` (100 l.), `dst-2027-03-28` (92 l.), `a-cheval-mois` (1 + 4), `tolerance` (8 l.). Ces fixtures sont **petites et locales aux cas** : la galerie et l'honnêteté de bout en bout ont besoin d'un **générateur de scénarios en mémoire** (une semaine entière, voir Pattern 10), qui doit vivre dans `src/` (la galerie tourne dans l'exe) et être réutilisé par les tests.
- **Suite** : 1 415 tests, ≈ 10 s, `dotnet test Chronos.sln -c Debug --nologo -v q` ; 0 warning en Release ; les exécuteurs parallèles utilisent un instantané `git archive` nommé par plan (précédent 32-06 / 33-04) parce que les RED voisins cassent la compilation du projet de tests partagé.

### 7. Environnement CLI, fuseau, culture

- `TimeZoneInfo` injecté (D-32-29 / D-33-21) : production `TimeZoneInfo.Local` **au site de composition** (App.xaml.cs), tests `BornesPlage.FuseauParisPourTests()`. Aucune classe pure ne lit le fuseau système.
- Culture : les clés (« HH:mm ») sont invariantes ; les libellés visibles (« sam. 19 sept. », « jeudi 24 sept. 2026 ») doivent utiliser explicitement `CultureInfo.GetCultureInfo("fr-FR")` dans `TextesHistorique` (déterministe en test, `InvariantGlobalization=false` en publication). Aucun usage de `CurrentCulture` dans `src/` aujourd'hui (grep) — garder cette discipline.

## Architecture Patterns

### Recommended Project Structure (fichiers NOUVEAUX, disjoints par plan)
```
src/Chronos/
├── Resources/DesignTokens.xaml                 # + palette promue, tokens Histo*, tailles sys:Double        (34-01)
├── Views/SettingsWindow.xaml                   # − 7 brosses locales, + fusion DesignTokens (pack URI)      (34-01)
├── Rendering/Historique/
│   ├── EchelleTemps.cs                         # Plage × largeur → x ; x → instant ; grille jours / 3 h      (34-02)
│   ├── EchelleValeur.cs                        # fraction 0..1 → y ; échelles « 0 – 25 % », « 0 – 1,2 M »     (34-02)
│   ├── Escalier.cs                             # série → segments (coupés aux trous), bandes de rampe        (34-02)
│   ├── Binning.cs                              # Δ par heure ; réduction min/max par colonne (> 4 000 pts)   (34-02)
│   ├── Tuiles5h.cs                             # tuiles bornées par R5 (style C)                             (34-02)
│   ├── Divergences.cs                          # marche de % sans tokens Code (croisement, HORS Tokens)      (34-02)
│   └── Reticule.cs                             # relevé le plus proche d'un x ; contenu neutre de l'infobulle (34-02)
├── Models/Historique/DonneesHistorique.cs      # records neutres DonneesSemaine / DonneesJour / Fraicheur    (34-03)
├── Services/Historique/ISourceHistorique.cs    # interface neutre (LireSemaine / LireJour / RepereHebdo)     (34-03)
├── Services/Historique/SourceHistoriqueDisque.cs # façade : bornes → journal → analyse → agrégats → rendu    (34-03)
├── Services/ChronosSettings.cs                 # enum HistoriqueStyleSemaine + HistoriqueX/Y/Width/Height    (34-03)
├── ViewModels/Historique/HistoriqueViewModel.cs      # vue active, période, ‹ ›, fraîcheur, style, F2, Tick  (34-03)
├── ViewModels/Historique/TextesHistorique.cs         # PRODUCTEUR UNIQUE des mots (§4), fr-FR                (34-03)
├── ViewModels/Historique/ScenariosHistorique.cs      # scénarios en mémoire (galerie + tests), neutre        (34-03)
├── Controls/Historique/PisteBase.cs            # FrameworkElement, DP Donnees/Plage/brosses, compteur       (34-04)
├── Controls/Historique/{PisteNiveau,PisteRythme,PisteTokens,PisteCouverture,PisteFenetres5h,SurcoucheReticule}.cs (34-04)
├── Views/Historique/HistoriqueWindow.xaml(.cs) # coquille : chrome, en-tête, F2, Échap, persistance         (34-05)
├── Views/Historique/VueSemaineView.xaml(.cs)   # stub en 34-05, rempli en 34-06 (3 grilles par style)
├── Views/Historique/VueJourView.xaml(.cs)      # stub en 34-05, rempli en 34-07
├── Interop/NativeMethods.cs                    # + DwmSetWindowAttribute (dwmapi)                            (34-05)
└── App.xaml.cs                                 # + mode --historique, + DI HistoriqueViewModel/ISourceHistorique (34-05)
tests/Chronos.Tests/
├── Rendering/…Tests.cs, HistoriqueViewModelTests.cs, TextesHistoriqueTests.cs, PistesHistoriqueTests.cs,
├── HistoriqueBindingTests.cs (XAML WPF), GardeTokensHistoriqueTests.cs, GardeVocabulaireHistoriqueTests.cs,
└── Fakes/FakeSourceHistorique.cs
```

### Pattern 1 : fenêtre opaque sans chrome, redimensionnable, coins ronds système
**What :** `WindowStyle=None` + `AllowsTransparency=False` + `WindowChrome` (bords de redimensionnement invisibles, caption 0 → drag par `DragMove` sur l'en-tête comme `SettingsWindow`) + coins/ombre par DWM sur Windows 11.
**When :** `HistoriqueWindow` uniquement (HIS-01).
**Example :**
```xml
<!-- Views/Historique/HistoriqueWindow.xaml — Source : docs WindowChrome (learn.microsoft.com/dotnet/api/system.windows.shell.windowchrome) -->
<Window x:Class="Chronos.Views.HistoriqueWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"
        WindowStyle="None" AllowsTransparency="False" Topmost="False" ShowInTaskbar="True"
        ResizeMode="CanResize" UseLayoutRounding="True" SnapsToDevicePixels="True"
        MinWidth="{StaticResource HistoLargeurMin}" MinHeight="{StaticResource HistoHauteurMin}"
        Width="{StaticResource HistoLargeurDefaut}" Height="{StaticResource HistoHauteurDefaut}"
        Background="{StaticResource Panel}" FontFamily="Segoe UI" Title="Chronos — Historique">
    <Window.Resources>
        <ResourceDictionary Source="pack://application:,,,/Chronos;component/Resources/DesignTokens.xaml"/>
    </Window.Resources>
    <shell:WindowChrome.WindowChrome>
        <shell:WindowChrome CaptionHeight="0" ResizeBorderThickness="6" GlassFrameThickness="0"
                            CornerRadius="0" UseAeroCaptionButtons="False"/>
    </shell:WindowChrome.WindowChrome>
    <Window.InputBindings>
        <KeyBinding Key="Escape" Command="{Binding FermerCommand}"/>
    </Window.InputBindings>
    …
</Window>
```
```csharp
// Views/Historique/HistoriqueWindow.xaml.cs — coins ronds + ombre DWM (Windows 11 build ≥ 22000), best-effort
// Source : learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute (DWMWA_WINDOW_CORNER_PREFERENCE = 33)
//          learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwm_window_corner_preference (DWMWCP_ROUND = 2)
SourceInitialized += (_, _) =>
{
    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
    try { int pref = 2; _ = NativeMethods.DwmSetWindowAttribute(hwnd, 33, ref pref, sizeof(int)); }
    catch { /* Windows 10 : coins droits, sans ombre — dit dans le SUMMARY, pas masqué */ }
};
// Interop/NativeMethods.cs
[DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
```
Persistance (motif GAP-1 de `SessionsController`) dans `Closing` : `if (WindowState == WindowState.Normal) Persist(s => s with { HistoriqueX = Left, HistoriqueY = Top, HistoriqueWidth = Width, HistoriqueHeight = Height })` ; restauration avant `Show` avec clamp à `SystemParameters.VirtualScreen{Left,Top,Width,Height}` (un moniteur débranché ne doit pas cacher la fenêtre) ; `WindowStartupLocation=Manual` si persisté, sinon `CenterScreen`. La galerie passe une persistance **no-op** (jamais le vrai `settings.json` depuis `--historique`).

### Pattern 2 : façade de lecture neutre, hors UI, résultat immuable
**What :** `ISourceHistorique` (Services/Historique, neutre) rend des records `DonneesSemaine` / `DonneesJour` (Models/Historique) déjà **prêts à dessiner** ; le VM lance `Task.Run(() => source.LireSemaine(...))`, numérote la requête, et n'applique que la plus récente par `_ui.Post`.
**Example :**
```csharp
// Services/Historique/ISourceHistorique.cs (neutre : aucun type WPF ; garde de pureté verte)
public interface ISourceHistorique
{
    /// Repère hebdo = R7 du dernier relevé connu (null si journal vide) — sert à BornesPlage.SemaineDeForfait.
    DateTimeOffset? RepereHebdo(DateTimeOffset now);
    DonneesSemaine LireSemaine(Plage semaine, Plage semainePrecedente, DateTimeOffset now);
    DonneesJour LireJour(Plage jour, DateTimeOffset now);
}

// Services/Historique/SourceHistoriqueDisque.cs — enchaînement réel (chemins ChronosPaths, jamais en dur)
public DonneesSemaine LireSemaine(Plage s, Plage s1, DateTimeOffset now)
{
    var cadence = RateLimitHeaderUsageProvider.CadenceNominale;
    var journal = LecteurJournal.Lire(_paths.HistoriqueDir, s.Debut, s.Fin);
    var analyse = AnalyseReleves.Analyser(journal, now, cadence);
    var precedente = AnalyseReleves.Analyser(LecteurJournal.Lire(_paths.HistoriqueDir, s1.Debut, s1.Fin), now, cadence);
    var agregats = LecteurAgregats.Lire(_paths.HistoriqueDir, s.Debut, s.Fin);
    var couverture = CouvertureTokens.Charger(Path.Combine(_paths.HistoriqueDir, CouvertureTokens.NomFichier)); // ⚠ non exposée par Lire
    var barres = RenduLocalTokens.ParHeure(agregats.Tranches, s, _tz, couverture);
    return new DonneesSemaine(s, analyse, precedente, barres, agregats.Couverture, journal.JournalOuvertLe, now);
}
```
`DonneesSemaine`/`DonneesJour` sont des `record` **neutres** (ils portent `AnalyseJournal` — des `double` — et `BarreHeure` — des `long`) : placer dans `Chronos.Models.Historique` (pas `.Tokens`), ne jamais y écrire de rapport tokens/plafond (regex doctrine), ne jamais y calculer un `%` depuis les tokens.

### Pattern 3 : géométrie pure dans `Rendering/Historique`, testée en `[Fact]`
**What :** fonctions statiques sur doubles nus / records, sans WPF : `EchelleTemps.X(t, plage, largeur)`, `Escalier.Segments(serie, trous, selecteur u, plage)` → segments `(x0, x1, u)` coupés aux trous, `Escalier.ParBandes(segments, nbBandes)` → un `StreamGeometry` par bande de rampe côté contrôle, `Binning.DeltasParHeure(deltas, plage)`, `Binning.ReductionMinMax(points, colonnes)` (appliquée si `points.Count > 4000`, constante `SeuilReduction = 4000`), `Tuiles5h.Depuis(serie, plage)`, `Divergences.Detecter(deltasHebdo, barres, seuil)`, `Reticule.PlusProche(serie, t)`.
**Why :** même statut que `ArcGeometry`/`DayTimeline` (« géométrie en classes PURES testées comme ArcGeometry ») ; les fixtures 32-06/33-04 et le générateur de scénarios alimentent ces tests sans STA.

### Pattern 4 : une piste = un `FrameworkElement` à `OnRender`, DP immuables, brosses par tokens
**Example :**
```csharp
// Controls/Historique/PisteNiveau.cs — moule TideColumn/FuseBar, sans couleur par défaut en dur
public sealed class PisteNiveau : FrameworkElement
{
    public static readonly DependencyProperty DonneesProperty = DependencyProperty.Register(
        nameof(Donnees), typeof(DonneesSemaine), typeof(PisteNiveau),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty RampeProperty = DependencyProperty.Register(          // ChronosTheme du thème actif
        nameof(Rampe), typeof(ChronosTheme), typeof(PisteNiveau),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TraitResetProperty = DependencyProperty.Register(     // {StaticResource TickReset}
        nameof(TraitReset), typeof(Brush), typeof(PisteNiveau),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    // … EpaisseurEscalier (sys:Double 2,2), FondTrou (Line), OpaciteTrou (0,35), FantomeProperty (HistoGris) …

    internal int RendusPourTests;   // InternalsVisibleTo Chronos.Tests : « jamais au tick »

    protected override void OnRender(DrawingContext dc)
    {
        RendusPourTests++;
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0 || Donnees is null || Rampe is null) return;

        var gs = new GuidelineSet(); gs.GuidelinesX.Add(0.5); gs.GuidelinesY.Add(0.5); gs.Freeze();
        dc.PushGuidelineSet(gs);                                    // 1 px nets à 100 % et 150 %

        foreach (var bande in Escalier.ParBandes(Escalier.Segments(Donnees.Analyse.Serie, Donnees.Analyse.Trous, r => r.U7, Donnees.Plage), 12))
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
                foreach (var s in bande.Segments)
                {
                    ctx.BeginFigure(new Point(EchelleTemps.X(s.T0, Donnees.Plage, w), EchelleValeur.Y(s.U, h)), false, false);
                    ctx.LineTo(new Point(EchelleTemps.X(s.T1, Donnees.Plage, w), EchelleValeur.Y(s.U, h)), true, false);
                }
            geo.Freeze();                                           // gelée : partageable, plus rapide (docs perf 2D)
            var pen = new Pen(Rampe.ArcBrush(bande.Niveau), EpaisseurEscalier); pen.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }
        dc.Pop();
    }
}
```
Règles : tout ce qui varie avec les données/la plage/le style est une DP `AffectsRender` ; **aucune** DP n'est bindée à une propriété qui change au tick ; `Pen`/`Brush`/`Geometry` gelés ; pas de `DropShadowEffect`/`BitmapEffect` ; clip par `PushClip(new RectangleGeometry(new Rect(0,0,w,h)))` avant de dessiner les segments (les valeurs > 1 ou les S-1 peuvent déborder).

### Pattern 5 : hauteurs §2.2 et tailles en tokens `sys:Double`, vue par style en XAML statique
**What :** `DesignTokens.xaml` porte `HistoHauteurNiveauPistes=150`, `HistoHauteurRythmePistes=72`, `…TokensPistes=72`, `HistoHauteurCouverture=12`, `HistoHauteurNiveauSimplifie=200`, `…TokensSimplifie=90`, `HistoHauteurNiveauTuiles=120`, `HistoHauteurFenetres5hTuiles=62`, `…RythmeTuiles=58`, `…TokensTuiles=58`, `HistoHauteurNiveauJour=190`, `…RythmeJour=64`, `…TokensJour=64` ; `VueSemaineView.xaml` contient **trois `Grid` nommées** (`StylePistes`, `StyleSimplifie`, `StyleTuiles`) dont une seule est visible (`DataTrigger` sur `IsStylePistes/IsStyleSimplifie/IsStyleTuiles`, motif `SessionsWindow`/`MainWindow`), chaque piste avec `Height="{StaticResource HistoHauteurNiveauPistes}"`.
**Test (HIS-03)** : `[WpfFact]` monte la vue pour chaque style, `Measure/Arrange`, parcourt l'arbre visuel et asserte **l'ordre des types de pistes et leur `ActualHeight`** exacts (150/72/72/12 ; 200/90/12 ; 120/62/58/58/12) — la table du plan est ainsi vérifiée sur l'assembly réel, sans dupliquer les chiffres dans du C#.

### Pattern 6 : ligne de fraîcheur et alerte « journal muet » (mêmes mots que 32-05)
```csharp
// ViewModels/Historique/TextesHistorique.cs — producteur unique, culture fr-FR explicite, AUCUNE projection
public static string LigneFraicheurSemaine(AnalyseJournal a, DateTimeOffset now) =>
    a.Serie.Count == 0
        ? "aucun relevé sur cette semaine de forfait"                                   // jamais un « 0 % »
        : $"Dernier relevé {LibelleSource.Anciennete(a.Serie[^1].T, now)} · {LibelleSource.Format(a.Source)} · "
          + $"{a.Serie.Count} relevé{(a.Serie.Count > 1 ? "s" : "")} · {a.Trous.Count} interruption{(a.Trous.Count > 1 ? "s" : "")}"
          + (a.JournalOuvertLe is { } j ? $" · journal ouvert le {DateLongue(j)}" : "");
public static string LigneFraicheurJour(Plage jour, AnalyseJournal a, TimeSpan cadence) =>
    $"{(int)(jour.Duree / cadence)} relevés attendus · {a.Serie.Count} présents · " + Interruptions(a.Trous, tz);   // 288 ; 300 le 25/10 ; 276 le 28/03
// Alerte D-32-21 : now − max(_demarrage, journal.DerniereEcriture ?? _demarrage) > JournalReleves.SeuilMuet → « journal muet depuis N min »
```

### Pattern 6 bis : bandeau F2 — `Changement` sur thread de fond → `Post` coalescé
```csharp
// HistoriqueViewModel — la frontière de thread est franchie UNE fois, les rafales (≈ 1 événement par fichier) sont coalescées
private int _f2EnAttente;   // 0 = rien en file
private void SurChangementReconstruction(object? s, EventArgs e)
{
    if (Interlocked.Exchange(ref _f2EnAttente, 1) == 1) return;   // déjà un Post en vol
    _ui.Post(() => { Volatile.Write(ref _f2EnAttente, 0); MajBandeauF2(); });
}
private void MajBandeauF2()
{
    if (_reconstruction is null) { AfficherBandeauF2 = false; return; }
    var r = _reconstruction;
    AfficherBandeauF2 = r.Phase == PhaseReconstruction.Reconstruction;
    TexteBandeauF2 = TextesHistorique.BandeauF2(r.FichiersTraites, r.FichiersTotal, r.SemaineCouranteDisponible);
    FractionBandeauF2 = r.FichiersTotal > 0 ? (double)r.FichiersTraites / r.FichiersTotal : 0;   // barre : côté VM/UI, jamais dans le namespace Tokens
    SousTexteBandeauF2 = TextesHistorique.SousTexteF2(_journalOuvertLe);   // « … ils commencent au <date> »
}
```
Test : `FakeEtatReconstruction { Phase = Reconstruction, FichiersTraites = 886, FichiersTotal = 1603, SemaineCouranteDisponible = true }` ; `Declencher()` × 50 → `FakeUiDispatcher.PostCount == 1` si le dispatcher n'exécute pas inline (variante du fake avec file différée), texte exact « Reconstruction des tokens depuis vos transcripts Claude Code — 886 / 1603 fichiers · la semaine courante est déjà complète » ; `Phase = Incremental` + `Declencher()` → `AfficherBandeauF2 == false`.

### Pattern 7 : réticule, infobulle et « maintenant » dans UNE surcouche, pas dans les pistes
**What :** `SurcoucheReticule : FrameworkElement` posée en `Grid.RowSpan` sur toutes les pistes, `Background=#01000000`-équivalent (dessiner un rectangle transparent pour être hit-testable), `MouseMove` → `vm.Survoler(fractionX)` → le VM calcule (pur, `Reticule.PlusProche`) et expose `XReticule`/`TexteInfobulle`/`XMaintenant` ; seule la surcouche a des DP `AffectsRender` bindées à ces propriétés. La ligne « maintenant » (Jour, `Ink` 60 %) et le réticule bougent sans jamais invalider une piste. L'infobulle est un `Border Background=Panel2` dans un `Canvas` de la même surcouche (`Canvas.Left` bindé à `XReticule` via un converter qui borne au bord droit), `IsHitTestVisible=False`.

### Pattern 8 : tous les mots visibles viennent de `TextesHistorique` et vivent dans des `TextBlock`
Les libellés d'honnêteté (« Chronos arrêté », « +N % pendant l'absence (répartition inconnue) », « consommé ailleurs (Cowork, claude.ai) », « journal ouvert le … », « épuisée à 100 % — le serveur refuse (statut rejected) », libellé permanent des tokens, pied de page fixe) sont produits par `TextesHistorique` (testé mot pour mot en `[Fact]`, en reprenant `CauseTrouTexte.Libelle` et `LigneAgregat.Perimetre` plutôt qu'en recopiant) et affichés par des `TextBlock` (annotations positionnées dans un `Canvas` au-dessus des pistes par des `x` calculés dans le VM en **fraction 0..1** + `MultiBinding(fraction, ActualWidth)` → converter `FractionVersX`). Les smoke tests les retrouvent par `TousLesTextBlocks` ; les pistes ne dessinent **aucun texte** (pas de `FormattedText`) — ce qui les garde indépendantes des polices et des cultures.

### Pattern 9 : garde de vocabulaire (aucune projection) et garde « aucune valeur en dur »
```csharp
// tests/GardeVocabulaireHistoriqueTests.cs — motif GardesPerimetreTests : anti-mutisme puis absence
var fichiers = Directory.EnumerateFiles(CheminSources(), "*.*", SearchOption.AllDirectories)
    .Where(f => f.Contains("Historique", StringComparison.Ordinal) && (f.EndsWith(".cs") || f.EndsWith(".xaml")))
    .Where(f => !f.Contains(Path.Combine("Services","Historique")) || f.EndsWith("TextesHistorique.cs")).ToList();
Assert.True(fichiers.Count >= 12, "la garde ne voit pas la fenêtre Historique");
var interdits = new Regex(@"épuisé[e]? vers|à ce rythme|projection|prévision|estim(é|ation)|tendance|dans \d+ h", RegexOptions.IgnoreCase);
// + « aucune valeur en dur » : Regex(@"#[0-9A-Fa-f]{6,8}|FontSize=""\d|Height=""\d|StrokeThickness=""\d") sur Views/Historique/*.xaml et Controls/Historique/*.cs
```
Les mots « estimation »/« estimé » sont interdits dans la fenêtre (§4 : « relevé (jamais « mesure », jamais « estimation ») »). L'anti-mutisme (`≥ 12 fichiers`) ne peut être vert qu'en vague 4 : créer la garde en 34-01 avec un seuil bas (`≥ 1`, sur `DesignTokens.xaml` + `SettingsWindow.xaml`) et **relever le seuil en 34-08**.

### Pattern 10 : scénarios de démonstration en mémoire (galerie `--historique` + honnêteté de bout en bout)
`ScenariosHistorique.SemaineDeReference(TimeZoneInfo tz)` fabrique **en mémoire** (aucun fichier) : `LectureJournal` de la semaine sam. 19 → sam. 26 sept. 2026 (relevés 5 min, `u7` croissant par paliers, `u5` en dents de scie avec `r5` toutes les 5 h, trou « Chronos arrêté » mar. 22 sept. 23:00 → mer. 23 sept. 07:00 local avec `arret`/`demarrage`, trou « jeton invalide » jeu. 24 sept. 14:00 → 16:00 avec `jeton_invalide`, plateau `u5 = 1.0`/`Statut5 = Rejete` mer. soir 20:00 → 22:30 (« épuisée »), marche hebdo +0,04 mer. 21:00 → 23:00 **sans** tranche de tokens (divergence), `JournalOuvertLe = 14 sept.`), `LectureJournal` de la semaine précédente (série plus basse, complète), `LectureAgregats` + `CouvertureTokens` (couverte du 14 sept. à now, opus/sonnet/haiku, sous-agents ≈ 30 %), `now = jeu. 24 sept. 2026 15:12 local`. `SourceHistoriqueDemonstration : ISourceHistorique` sert ces objets ; la galerie = `HistoriqueWindow` réelle sur cette source + horloge figée + persistance no-op (l'utilisateur bascule Pistes/Simplifié/Tuiles et Jour pour les captures DESIGN_PLAN §8). Les tests d'honnêteté de 34-08 consomment **les mêmes scénarios** que la galerie : ce que l'utilisateur voit est ce que les tests prouvent.

### Anti-Patterns to Avoid
- **Un `DispatcherTimer` 1 s dans la fenêtre Historique** : rien ne l'exige ; 60 s suffisent à « il y a N min », et la surcouche absorbe « maintenant ».
- **Binder une piste à une propriété recalculée au tick** (texte, âge) : c'est exactement ce que le test HIS-07 doit rougir.
- **Recalculer la géométrie dans le VM avec `ActualWidth`** : la géométrie dépend de la taille → elle se calcule dans `OnRender` à partir de records immuables + classes pures ; le VM ne connaît pas les pixels (sauf fractions 0..1 pour les annotations XAML).
- **Réutiliser `WeeklyWindow`/`WeeklyRecalibration`/`DayTimeline` pour les bornes** : ils avancent par 7 × 24 h et supposent une grille 5 h théorique (dérive d'une heure le 25/10/2026 mesurée en 32-06) ; les bornes viennent de `BornesPlage`, les resets sont **observés** (`ResetObserve`).
- **`Deactivated += Close`** (popover des réglages) sur une fenêtre de consultation.
- **Écrire dans `settings.json` depuis la galerie** ou lire `~/.claude` / `%APPDATA%\Claude` depuis la fenêtre : la fenêtre ne lit que `%APPDATA%\Chronos\historique` (déjà journalisé par les phases 32/33).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Bornes de semaine/jour locales, DST | arithmétique `AddDays(7)` sur UTC | `BornesPlage.SemaineDeForfait/Jour` (tz injecté) | 169 h / 167 h / 25 h prouvés par `BornesPlageTests` |
| Trous, resets, Δ, sauts | boucles ad hoc sur la série | `AnalyseReleves.Analyser` | D-32-25…28 déjà tranchées et mutées |
| Barres horaires / quarts d'heure en heure locale | `GroupBy(t.Hour)` local | `RenduLocalTokens.ParHeure/ParQuartDHeure` | 25 barres le 25/10, 23 le 28/03 (`RenduLocalTokensTests`) |
| Cause d'un trou, mots | littéraux dans le XAML | `CauseTrouTexte.Libelle`, `LigneAgregat.Perimetre`, `LibelleSource.*`, `TokenFormatter` | « mêmes mots partout » (§4, D-32-22) |
| Ancienneté « il y a N min » | formatage maison | `LibelleSource.Anciennete(t, now)` | paliers et âge négatif déjà gérés |
| Alerte « journal muet » | seuil 900 s en dur | `JournalReleves.SeuilMuet` + règle D-32-21 | seuils dérivés de `CadenceNominale`, jamais en dur |
| Persistance des réglages | fichier propre à la fenêtre | `ChronosSettings` (+ champs) + `SettingsService` + motif GAP-1 | atomique, tolérant, testé |
| Redimensionnement d'une fenêtre sans chrome | `WM_NCHITTEST` maison | `System.Windows.Shell.WindowChrome` | mécanisme WPF documenté, aucune dépendance |
| Coins ronds + ombre sur fenêtre opaque | `AllowsTransparency` + effet | `DwmSetWindowAttribute(33, 2)` best-effort | contrat `AllowsTransparency=False` |
| Rampe de couleur | interpolation maison | `ChronosTheme.ArcColor/ArcBrush` (`RampColor` dessous) | une seule loi de couleur pour tout Chronos |
| Marshaling UI | `Dispatcher.Invoke` direct dans le VM | `IUiDispatcher.Post` (+ coalescence) | frontière unique, `FakeUiDispatcher` compte |
| Journée nominale de test | 288 lignes de fixture | `FabriqueJournal.JourneeNominale` | D-32-30 |

**Key insight :** la phase 34 est une phase d'**assemblage et de rendu** ; chaque fois qu'une donnée ou un mot semble manquer, il existe déjà dans 32-06 / 33-04 / `Text/` — le chercher avant de l'écrire.

## Runtime State Inventory

Step 2.5 : **non applicable** (aucun renommage, refactor ou migration ; la phase n'écrit que deux clés nouvelles dans `settings.json`, absentes → défauts, sans migration — précédent DEL-06). Aucun état d'exécution à migrer.

## Common Pitfalls

### Pitfall 1 : `WindowStyle=None` + `AllowsTransparency=False` = rectangle opaque
**What goes wrong :** ni coins ronds ni ombre ; un `Border CornerRadius=16` laisse des coins de la couleur de `Background` ; `DropShadowEffect` est coupé au bord de la fenêtre.
**How to avoid :** `Background` de la fenêtre = `Panel` (les coins « carrés » sont de la même couleur), `WindowChrome` pour les bords, DWM `DWMWCP_ROUND` sur Windows 11 (rayon système ≈ 8 px, pas 16 — Open Question 3), `BorderBrush=Line 1 px` dessiné dans le contenu.
**Warning signs :** bandeau blanc en haut de fenêtre (frame non-client résiduel) → le `WindowChrome` n'est pas attaché.

### Pitfall 2 : fenêtre restaurée hors écran
**How to avoid :** clamp de `Left/Top/Width/Height` persistés à `SystemParameters.VirtualScreen*` et aux minima 760 × 480 avant `Show` ; ne persister qu'en `WindowState.Normal`.

### Pitfall 3 : `ShowInTaskbar=True` + `Owner`
**What goes wrong :** une fenêtre avec `Owner = MainWindow` (cadran topmost) hérite d'un comportement de dialogue (reste au-dessus de l'owner, minimisée avec lui). **Ne pas poser `Owner`** sur `HistoriqueWindow` (§7 du plan : fenêtre indépendante, rappelée par les gestes de la phase 35).

### Pitfall 4 : promotion de la palette → `SettingsWindow` casse dans les tests
**What goes wrong :** `ReglagesBindingTests`, `ThemingTests`, `SessionStylesBindingTests` construisent `SettingsWindow` sans `Application.Current` ; ses `{StaticResource Panel}` ne résolvent plus → `XamlParseException`.
**How to avoid :** dans `SettingsWindow.xaml`, `<Window.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceDictionary Source="pack://application:,,,/Chronos;component/Resources/DesignTokens.xaml"/></ResourceDictionary.MergedDictionaries> …styles locaux… </ResourceDictionary></Window.Resources>` (les styles `Section/Flat/Switch/…` restent locaux, ils référencent les tokens promus par `StaticResource` — l'ordre de fusion les rend disponibles). Vérifier la non-régression visuelle : **aucune valeur ne change**, 12 littéraux restants.

### Pitfall 5 : `StaticResource` vs `DynamicResource`
**Rule :** `DynamicResource` pour tout token que `ApplyThemeBrushes` peut écraser (`Alerte`, `TickReset`, `TexteSecondaire`… — les 12 clés de `BrushTokens()`) ; `StaticResource` pour le chrome fixe (`Panel*`, `Ink*`, `Line`, `Accent`, `Ok`, `Histo*`, tailles). Une piste reçoit ses brosses par binding sur la ressource de la fenêtre : `TraitReset="{DynamicResource TickReset}"` fonctionne sur une DP de `FrameworkElement`.

### Pitfall 6 : `sys:Double` en ressource
`xmlns:sys="clr-namespace:System;assembly=mscorlib"` fonctionne sous .NET 8 (façade + type forwarding) ; `{StaticResource X}` sur `Height`/`FontSize`/`StrokeThickness` attend un `double` boxé — c'est le cas. Ne pas écrire `"12"` (string) dans une ressource typée. Les clés ne doivent pas contenir de virgule : `HistoCorpsMini` plutôt que `HistoCorps8,5`.

### Pitfall 7 : la divergence et les records mixtes vs les gardes
**What goes wrong :** un `double` (Δ %) dans `Chronos.Services.Historique.Tokens` ou `Chronos.Models.Historique.Tokens` rougit `GardeTokensSansPourcentageTests` (réflexion ET texte, même en commentaire) ; le mot `Utilization` aussi.
**How to avoid :** `Divergences.cs` dans `Rendering/Historique`, `DonneesSemaine` dans `Chronos.Models.Historique` ; ne jamais nommer un membre `Ratio/Pct/Fraction/Quota` près de tokens ; ne jamais écrire `tokens / plafond` (regex doctrine sur `Services/` et `Models/`). La barre F2 (`FichiersTraites / FichiersTotal`) est un `double` **côté VM**, jamais dans le namespace Tokens (le contrat dit « se formate côté UI »).

### Pitfall 8 : `OnRender` déclenché par ce qu'on croit inerte
`ActualWidth/ActualHeight` changent au redimensionnement (légitime : « changement de plage » au sens pixel), mais aussi `Visibility` bascule des trois grilles de style, `Opacity`, `Margin`. Un binding de `Donnees` à une **nouvelle instance** de record à chaque `Tick` (même contenu) redessine : garder la **même référence** tant que la lecture n'a pas changé (comparer `DerniereEcriture`/`Phase`/`SemaineCouranteDisponible` avant de relire).

### Pitfall 9 : « 288 relevés attendus » et « samedi → samedi » ne sont pas des constantes
Le 25/10/2026 le jour fait 25 h (300 relevés attendus), la semaine 169 h ; `plage.Duree / cadence` et `Plage` de `BornesPlage` sont la vérité. La grille des jours en Semaine : sept lignes à `Debut + k jours LOCAUX` (`MinuitLocal` par `tz`), pas `k × 24 h`.

### Pitfall 10 : superposition de la semaine précédente
S-1 peut durer 168 h quand S en dure 169 : projeter S-1 par **fraction de sa propre plage** (`(t − S1.Debut) / S1.Duree`) sur la largeur de S — décalage d'au plus une heure sur deux semaines par an, documenté dans le SUMMARY. Le fantôme est **gris pointillé** et jamais coloré par la rampe.

### Pitfall 11 : `LecteurAgregats.Lire` ne rend pas la `CouvertureTokens`
Charger `couverture.json` séparément (`CouvertureTokens.Charger`, tolérante, jamais d'exception) pour `ParHeure/ParQuartDHeure` ; un dossier absent → `new CouvertureTokens()` → tout « hors couverture » (c'est vrai et c'est dit).

### Pitfall 12 : course du chargeur BAML dans les tests
Toute classe de test qui construit `HistoriqueWindow`, `VueSemaineView`, `VueJourView` ou `SettingsWindow` porte `[Collection("XAML WPF")]` et `[WpfFact]`. Les tests de pistes seules (pas de BAML) peuvent rester en `[WpfFact]` hors collection ; les tests de géométrie et de VM en `[Fact]`.

### Pitfall 13 : exécution parallèle sur un arbre partagé
Les fenêtres RED des voisins cassent la compilation de `Chronos.Tests` : instantané `git archive` au SHA d'entrée dans un dossier **nommé par plan** (`snap-34-0N`), suite complète finale sur l'arbre réel, deux fois (précédents 32-06, 33-04, 33-05).

## Code Examples

### Escalier coupé aux trous + réduction min/max (pur, `[Fact]`)
```csharp
// Rendering/Historique/Escalier.cs
public readonly record struct SegmentPalier(DateTimeOffset T0, DateTimeOffset T1, double U);
public static IReadOnlyList<SegmentPalier> Segments(IReadOnlyList<ReleveJournal> serie, IReadOnlyList<Trou> trous,
                                                    Func<ReleveJournal, double?> u, Plage plage)
{
    var debuts = trous.Select(t => t.Debut).ToHashSet();   // un palier s'arrête au dernier relevé avant un trou
    var res = new List<SegmentPalier>();
    for (var i = 0; i < serie.Count; i++)
    {
        if (u(serie[i]) is not { } v) continue;
        var fin = i + 1 < serie.Count && !debuts.Contains(serie[i].T) ? serie[i + 1].T : serie[i].T;   // palier jusqu'au relevé suivant, sauf trou
        if (fin > plage.Fin) fin = plage.Fin;
        res.Add(new SegmentPalier(serie[i].T, fin, v));
    }
    return res;
}
// Rendering/Historique/Binning.cs — réduction min/max par colonne de pixels (appliquée au-delà de SeuilReduction = 4000)
public static IReadOnlyList<(double X, double Min, double Max)> ReductionMinMax(IReadOnlyList<(double X, double Y)> pts, int colonnes) { … }
```

### Tick 60 s sans redessin des pistes (VM)
```csharp
/// Appelé toutes les 60 s par le DispatcherTimer créé dans HistoriqueWindow.Loaded (jamais dans le ctor).
/// Ne touche QUE les textes de fraîcheur et la surcouche ; relit les données seulement si un magasin a bougé.
internal void Tick(DateTimeOffset now)
{
    TexteFraicheur = TextesHistorique.LigneFraicheur(_donnees, now);           // TextBlock, pas une piste
    XMaintenant = EchelleTemps.Fraction(now, PlageCourante);                   // surcouche
    MajAlerteJournal(now);
    if (_journal?.DerniereEcriture != _derniereEcritureVue || _reconstruction?.SemaineCouranteDisponible != _semaineVue)
        DemanderLecture();                                                     // → nouvelle référence de Donnees, redessin légitime
}
```

### Test « jamais au tick » (deux niveaux)
```csharp
[Fact] // niveau VM : aucune notification sur les propriétés de données pendant 60 ticks
public void Soixante_ticks_ne_changent_pas_les_donnees_des_pistes()
{
    var vm = Construire(out var clock, out var source);           // FakeSourceHistorique, FakeUiDispatcher inline
    vm.ChargerSynchrone();                                        // helper de test : lecture + Post
    var donnees = vm.DonneesSemaine; var notifs = 0;
    vm.PropertyChanged += (_, e) => { if (e.PropertyName is nameof(vm.DonneesSemaine) or nameof(vm.DonneesJour)) notifs++; };
    for (var i = 0; i < 60; i++) { clock.UtcNow += TimeSpan.FromMinutes(1); vm.Tick(clock.UtcNow); }
    Assert.Same(donnees, vm.DonneesSemaine); Assert.Equal(0, notifs); Assert.Equal(1, source.Lectures);
}
[WpfFact] // niveau contrôle : le compteur d'OnRender ne bouge pas quand seules les propriétés de tick changent
public void La_piste_ne_se_redessine_pas_quand_la_surcouche_bouge()
{
    var piste = new PisteNiveau { Width = 800, Height = 150, Donnees = Scenario(), Rampe = ThemeCatalog.Default, TraitReset = Brushes.White };
    piste.Measure(new Size(800, 150)); piste.Arrange(new Rect(0, 0, 800, 150));
    Rendre(piste); var avant = piste.RendusPourTests;             // RenderTargetBitmap.Render force un premier rendu
    var surcouche = new SurcoucheReticule { Width = 800, Height = 150 };
    for (var i = 0; i < 60; i++) surcouche.XMaintenant = i / 60.0;   // DP AffectsRender de la surcouche seulement
    Rendre(piste); Assert.Equal(avant, piste.RendusPourTests);   // voir Open Question 4 sur le comportement exact de RTB
    piste.Donnees = Scenario();                                    // nouvelle référence = changement de données
    Rendre(piste); Assert.Equal(avant + 1, piste.RendusPourTests);
}
```

### Mode CLI `--historique` (App.xaml.cs, copie de `--sessions`)
```csharp
// AVANT le mutex mono-instance et AVANT le Host, comme --cadrans / --sessions : aucune réconciliation, aucun service, multi-instances.
if (e.Args.Any(a => string.Equals(a, "--historique", StringComparison.OrdinalIgnoreCase)))
{
    base.OnStartup(e);
    var tz = TimeZoneInfo.Local;
    var vm = new HistoriqueViewModel(new SourceHistoriqueDemonstration(tz), new WpfUiDispatcher(Current.Dispatcher),
                                     new HorlogeFigee(ScenariosHistorique.Maintenant(tz)), tz,
                                     new PersistanceHistoriqueMemoire(), journal: null, reconstruction: ScenariosHistorique.ReconstructionEnCours());
    var fenetre = new HistoriqueWindow(vm);
    MainWindow = fenetre;
    fenetre.Show();
    return;
}
```
(Une garde textuelle sur `App.xaml.cs` — motif `GardesPerimetreTests` — épingle que `--historique` précède `VerrouInstanceUnique.Acquerir` et ne résout aucun service.)

## Découpage recommandé : 8 plans / 4 vagues (fichiers disjoints)

| Vague | Plan | Exigences | Fichiers créés / modifiés (disjoints dans la vague) | Livrable testable |
|---|---|---|---|---|
| 1 | **34-01 tokens + palette** | HIS-07 | `Resources/DesignTokens.xaml` (+7 promus, +6 Histo couleur, tailles/hauteurs/épaisseurs `sys:Double`), `Views/SettingsWindow.xaml` (−7 locales, +fusion pack URI), `tests/GardeTokensHistoriqueTests.cs` (valeurs exactes des 7 promues et des Histo, anti-mutisme bas, « aucune valeur en dur » sur `Views/Historique` et `Controls/Historique` quand ils existent) | `ReglagesBindingTests`/`ThemingTests` verts ; `SettingsWindow.xaml` = 12 littéraux, aucune valeur changée |
| 1 | **34-02 géométrie pure** | HIS-07, HIS-06 (géométrie) | `Rendering/Historique/{EchelleTemps, EchelleValeur, Escalier, Binning, Tuiles5h, Divergences, Reticule}.cs` + tests `[Fact]` sur fixtures 32-06/33-04 (`trou-arrete` → segments coupés, `reset-5h-milieu-heure` → tuiles, DST → grille 3 h/jours locaux, 4 001 points → réduction) | classes pures, aucun WPF requis |
| 1 | **34-03 façade + VM + textes + scénarios** | HIS-01, HIS-08 (VM), HIS-04 (textes) | `Services/ChronosSettings.cs` (enum + 4 champs), `Services/Historique/{ISourceHistorique, SourceHistoriqueDisque}.cs`, `Models/Historique/DonneesHistorique.cs`, `ViewModels/Historique/{HistoriqueViewModel, TextesHistorique, ScenariosHistorique, SourceHistoriqueDemonstration}.cs`, `tests/Fakes/FakeSourceHistorique.cs`, `HistoriqueViewModelTests`, `TextesHistoriqueTests`, `SettingsServiceTests` (+2), `ServicesLayerPurityTests` (+1 : la garde voit `ISourceHistorique`) | navigation ‹ ›, retour au présent, libellés de période fr-FR, fraîcheur, alerte D-32-21, style, F2 coalescé, tick sans relecture, lecture hors UI (Post compté) — sans un pixel de XAML |
| 2 | **34-04 pistes** | HIS-07, HIS-02 (rendu) | `Controls/Historique/{PisteBase, PisteNiveau, PisteRythme, PisteTokens, PisteCouverture, PisteFenetres5h, SurcoucheReticule}.cs`, `PistesHistoriqueTests` (`[WpfFact]`, hors collection BAML) | compteur de rendus, brosses null → rien dessiné (jamais d'exception), géométrie gelée, clip |
| 2 | **34-05 coquille + galerie + DI** | HIS-01, HIS-08 (vue) | `Views/Historique/HistoriqueWindow.xaml(.cs)`, stubs `VueSemaineView.xaml(.cs)` / `VueJourView.xaml(.cs)` (vides, nommés), `Interop/NativeMethods.cs` (+dwmapi), `App.xaml.cs` (`--historique`, `AddSingleton<ISourceHistorique>`, `AddSingleton<HistoriqueViewModel>`), `CompositionRootTests` (+1 miroir), `GardesPerimetreTests` (+1 : `--historique` avant le verrou), `HistoriqueBindingTests` (XAML WPF : chrome, min/défaut, Échap → `FermerCommand`, en-tête, bandeau F2 visible/masqué, persistance no-op en galerie) | fenêtre ouvrable par `--historique` (par l'utilisateur), corps vide |
| 3 | **34-06 vue Semaine (3 styles)** | HIS-02, HIS-03 | `Views/Historique/VueSemaineView.xaml(.cs)` (3 grilles, hauteurs par tokens, annotations `TextBlock`, sélecteur de style), `VueSemaineBindingTests` (ordre + `ActualHeight` exacts par style, fantôme S-1 présent, « reset hebdo → », libellés des jours) | HIS-03 « exactement les pistes et hauteurs §2.2 » |
| 3 | **34-07 vue Jour** | HIS-04 | `Views/Historique/VueJourView.xaml(.cs)`, `VueJourBindingTests` (grille 3 h, 190/64/64/12, « reset 5 h HH:MM », « épuisée à 100 % — … », légende modèles, ligne « maintenant » seulement si aujourd'hui, « N relevés attendus » = 288/300/276) | |
| 4 | **34-08 honnêteté de bout en bout** | HIS-06 | `tests/HonneteteHistoriqueTests.cs` (scénarios de 34-03 → deux vues × trois styles : mots exacts, trou = rectangle pointillé gris/ambre, saut = bloc plat gris « +N % … », divergence encadrée `Accent`, « journal ouvert le », libellé permanent et pied de page), `GardeVocabulaireHistoriqueTests` (seuil relevé), éventuels correctifs de libellés dans `TextesHistorique`/vues ; **mutations** : (h1) trou interpolé (segment traverse le trou) → rouge ; (h2) saut dessiné en barre au réveil → rouge ; (h3) divergence détectée sur une heure « transcripts absents » → rouge ; (h4) mot « estimé » ajouté → garde rouge ; (h5) hauteur 150 → 148 → test de mise en page rouge | GATE TESTS avant la revue DAEDALUS sur captures de `--historique` |

Points de vigilance de découpage : 34-01 est le seul à toucher `DesignTokens.xaml` et `SettingsWindow.xaml` ; 34-03 est le seul à toucher `ChronosSettings.cs` ; 34-05 est le seul à toucher `App.xaml.cs`, `CompositionRootTests`, `GardesPerimetreTests` ; 34-06 et 34-07 ne touchent que leur `UserControl` (créés vides par 34-05) ; les stubs de 34-05 doivent compiler sans les vues (un `Grid` vide).

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `AllowsTransparency=True` + `Border CornerRadius` + `DropShadowEffect` (SettingsWindow, SessionsWindow) | fenêtre opaque + `WindowChrome` + coins DWM | contrat DESIGN_PLAN §2 (2026-09-27) | pas de fenêtre layered, pas de rendu logiciel, composition DWM normale |
| Couleurs par défaut en dur dans les contrôles (`Controls/Cadrans/*`) | toutes les brosses par DP bindées aux tokens, défaut null | phase 34 | DoD « aucune valeur magique UI » |
| `UtilizationToBrushConverter` (rampe Minuit fixe) | `ChronosTheme.ArcColor` du thème persisté | à trancher (Open Question 1) | cohérence avec le cadran themé |
| Grille de resets théorique 5 h (`DayTimeline`) | resets **observés** (`AnalyseJournal.Resets5h`) | 32-06 | la vue Jour ne projette rien |
| `WeeklyWindow` 7 × 24 h | `BornesPlage` calendrier local | 32-06 | 169 h / 167 h justes |

**Deprecated/outdated :** `MainViewModel.TexteReconstruction` reste pour les réglages (deux propriétés, D-33-22) — la fenêtre Historique ne le réutilise pas, elle consomme `IEtatReconstruction` directement.

## Open Questions

1. **Rampe : `UtilizationToBrushConverter` (DESIGN_PLAN) ou `ChronosTheme.ArcColor` (thème actif) ?**
   - What we know : le converter interpole avec les stops **par défaut** (Minuit) et ignore le thème ; le cadran colore via `theme.ArcBrush` ; le plan écrit « rampe du thème actif via UtilizationToBrushConverter » — les deux moitiés de la phrase sont incompatibles dans le code réel.
   - Recommendation : tenir l'intention (« même loi que le cadran », thème actif) → DP `Rampe : ChronosTheme` sur les pistes, alimentée par `ThemeCatalog.ByKey(settings.ThemeKey)` ; le dire dans le SUMMARY de 34-04. Alternative sans écart au texte : faire évoluer le converter pour accepter un thème via `ConverterParameter` — plus lourd, sans gain.
2. **Valeur de `HistoGris`** : le plan ne la donne pas.
   - Recommendation : `#5A5960` (le gris « épuisé » déjà utilisé par `UtilizationToBrushConverter.Epuise`) — aucune couleur nouvelle dans le projet ; à confirmer au plan 34-01 (ou `ChronosTheme.Epuise` du thème si l'on veut qu'il suive le thème — mais le plan le classe parmi les tokens neutres fixes).
3. **Coins 16 et ombre sur fenêtre opaque** : DWM arrondit avec le rayon système (≈ 8 px DIU à 100 %) et dessine l'ombre ; sur Windows 10, coins droits et pas d'ombre. Le rayon 16 exact exigerait `AllowsTransparency=True`, interdit. Recommendation : DWM `DWMWCP_ROUND` + bordure `Line` 1 px ; noter l'écart (8 vs 16) pour la revue DAEDALUS §8 ; option `DWMWA_BORDER_COLOR = Line` pour une bordure système cohérente.
4. **Comportement exact de `RenderTargetBitmap.Render` vis-à-vis d'un `OnRender` non invalidé** (test niveau contrôle, MEDIUM) : le contenu de rendu d'un `UIElement` est retenu et `OnRender` n'est ré-exécuté que sur `InvalidateVisual` (DP `AffectsRender`) — c'est le modèle de rendu retenu de WPF, mais je n'ai pas exécuté le test. Recommendation : écrire d'abord le test niveau VM (déterministe), puis le test niveau contrôle ; si RTB ré-invoque `OnRender` à chaque appel, remplacer le compteur d'`OnRender` par un compteur dans le `PropertyChangedCallback` des DP `AffectsRender` (« aucune DP de piste invalidée pendant 60 ticks »), qui prouve la même chose.
5. **Bouton « 4 semaines » (phase 35)** : désactivé (`IsEnabled=False`, infobulle « bientôt (phase suivante) ») plutôt qu'un clic qui « renvoie bientôt » — ne trompe pas, ne fait rien, et le test HIS-01 peut l'asserter ; la phase 35 l'active.
6. **Seuil de divergence** : Δ hebdo ≥ 0,01 sur une heure (granularité constatée des en-têtes) sans tranche de tokens **couverte**. À poser en constante nommée dans `Divergences.cs` avec son pourquoi ; l'utilisateur pourra recaler après constat (phase 35).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build/tests | ✓ | 10.0.201 (cible net8.0-windows) | — |
| Windows 11 (DWM coins ronds, build ≥ 22000) | HIS-01 (coins/ombre) | ✓ | 10.0.26200 | Windows 10 : coins droits (best-effort, sans exception) |
| CommunityToolkit.Mvvm / Extensions.Hosting | VM / DI | ✓ | 8.4.2 / 8.0.1 | — |
| xunit + Xunit.StaFact | tests STA | ✓ | 2.9.2 / 1.1.11 | — |
| `%APPDATA%\Chronos\historique` (journal, agrégats, couverture.json) | données réelles | ✓ (écrit par 3.2.2 / phase 33 en production) | — | dossier absent → lectures vides, « hors couverture », fenêtre sans crash ; la galerie ne lit rien |
| Figma (maquettes A/B/C/D/F2) | référence visuelle | ✓ (URL dans CONTEXT) | — | captures PNG déjà transmises |

**Missing dependencies with no fallback :** aucune.
**Missing dependencies with fallback :** DWM rounded corners (Windows 10) → coins droits.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`), Microsoft.NET.Test.Sdk 17.11.1 |
| Config file | `tests/Chronos.Tests/Chronos.Tests.csproj` (AssemblyMetadata `CheminSourcesChronos`, `CheminDocsChronos`) ; `XamlWpfCollection.cs` |
| Quick run command | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~Historique" --nologo -v q` |
| Full suite command | `dotnet test Chronos.sln -c Debug --nologo -v q` (1 415 tests, ≈ 10 s, à passer deux fois) + `dotnet build Chronos.sln -c Release` (0 warning) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| HIS-01 | chrome (`WindowStyle`, `AllowsTransparency=False`, `Topmost=False`, `ShowInTaskbar`, min/défaut), Échap → fermeture, en-tête, persistance X/Y/W/H relue, clamp | smoke XAML (`[WpfFact]`, collection) + `[Fact]` settings | `--filter FullyQualifiedName~HistoriqueBindingTests` ; `~SettingsServiceTests` | ❌ Wave 2 (34-05) / ❌ Wave 1 (34-03 pour settings) |
| HIS-01 | ligne de fraîcheur, alerte « journal muet » (D-32-21), libellés de période fr-FR, ‹ › / retour au présent | `[Fact]` VM + textes | `~HistoriqueViewModelTests`, `~TextesHistoriqueTests` | ❌ Wave 1 (34-03) |
| HIS-02 | bornes, escalier coupé, dents de scie, tirets, S-1, Δ par heure, tokens empilés, couverture | `[Fact]` géométrie + `[WpfFact]` pistes + smoke vue | `~Rendering`, `~PistesHistoriqueTests`, `~VueSemaineBindingTests` | ❌ 34-02 / 34-04 / 34-06 |
| HIS-03 | trois styles, réglage persisté, sélecteur, ordre et hauteurs exacts | smoke vue (Measure/Arrange, `ActualHeight`) + `[Fact]` settings/VM | `~VueSemaineBindingTests`, `~HistoriqueViewModelTests` | ❌ 34-06 / 34-03 |
| HIS-04 | Jour : grille 3 h, % 5 h gris à 100 % (`Rejete`), resets observés, quarts d'heure par modèle, « maintenant », « N relevés attendus » | `[Fact]` géométrie/textes + smoke vue | `~VueJourBindingTests`, `~TextesHistoriqueTests` | ❌ 34-07 / 34-03 |
| HIS-06 | mots exacts, trou/saut/divergence/journal ouvert/pied de page sur 2 vues × 3 styles ; garde vocabulaire | smoke sur scénarios + garde textuelle | `~HonneteteHistoriqueTests`, `~GardeVocabulaireHistoriqueTests` | ❌ 34-08 |
| HIS-07 | jamais au tick (VM + contrôle), géométrie gelée, réduction > 4 000, tokens promus sans changement, aucune valeur en dur, pureté | `[Fact]` + `[WpfFact]` + gardes | `~HistoriqueViewModelTests`, `~PistesHistoriqueTests`, `~GardeTokensHistoriqueTests`, `ServicesLayerPurityTests`, `GardeTokensSansPourcentageTests`, `GardesDoctrineTests` | ❌ 34-01/02/03/04 (gardes existantes ✅) |
| HIS-08 | bandeau F2 : texte exact, barre, visible en `Reconstruction`, masqué en `Incremental`, `Changement` coalescé (`PostCount`) | `[Fact]` VM + smoke | `~HistoriqueViewModelTests`, `~HistoriqueBindingTests` | ❌ 34-03 / 34-05 |

### Sampling Rate
- **Per task commit :** filtre du plan (`--filter FullyQualifiedName~<ClasseDeTests>`) + gardes transverses (`~ServicesLayerPurityTests|~GardeTokensSansPourcentageTests|~GardesDoctrineTests`).
- **Per wave merge :** `dotnet test Chronos.sln -c Debug --nologo -v q` deux fois, 0 échec ; `dotnet build Chronos.sln -c Release` 0 warning.
- **Phase gate :** suite complète verte deux fois + mutations (h1–h5) rouges puis révoquées par copie (sha256) avant `/gsd:verify-work` ; ensuite seulement la revue DAEDALUS sur captures de `--historique` lancée par l'utilisateur.

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/Fakes/FakeSourceHistorique.cs` — source en mémoire réglable, compte les lectures (34-03)
- [ ] `tests/Chronos.Tests/Fakes/FakeUiDispatcher` : variante **différée** (file de `Action`, `Vider()`) pour prouver la coalescence de F2 (`PostCount == 1` pour 50 `Declencher()`) — le fake actuel exécute inline (34-03)
- [ ] `ScenariosHistorique` dans `src/` (34-03) : indispensable à la galerie ET aux tests de 34-04/06/07/08 ; les fixtures de 32-06/33-04 restent pour la géométrie
- [ ] helper `Rendre(FrameworkElement)` (`RenderTargetBitmap`) partagé par `PistesHistoriqueTests` (34-04)
- Framework : aucun à installer.

## Sources

### Primary (HIGH confidence)
- Code du dépôt (lu intégralement) : `Views/SettingsWindow.xaml(.cs)`, `MainWindow.xaml(.cs)`, `SessionsWindow.xaml(.cs)`, `SessionsController.cs`, `SessionsGalleryWindow.xaml(.cs)`, `CadranGalleryWindow.xaml(.cs)`, `App.xaml(.cs)`, `Resources/DesignTokens.xaml`, `Theming/ChronosTheme.cs`, `Converters/UtilizationToBrushConverter.cs`, `Controls/{RingArc,TickRing}.cs`, `Controls/Cadrans/{TideColumn,FuseBar,EmberRingControl}.cs`, `Views/Cadrans/CadranVoletsView.xaml`, `Rendering/{ArcGeometry,RampColor,DayTimeline}.cs`, `Services/Historique/{LecteurJournal,AnalyseReleves,BornesPlage,IEtatJournal,JournalReleves}.cs`, `Models/Historique/{LectureJournal,ReleveJournal,EvenementJournal}.cs`, `Services/Historique/Tokens/{LecteurAgregats,RenduLocalTokens,IEtatReconstruction,CouvertureTokens,MagasinAgregats,LigneAgregat}.cs`, `Models/Historique/Tokens/{LectureAgregats,TrancheTokens,Couverture,PhaseReconstruction}.cs`, `Services/{ChronosSettings,SettingsService,IUiDispatcher,WpfUiDispatcher,IClock,IEtatMagasin,ChronosPaths}.cs`, `ViewModels/{MainViewModel,SessionsPreviewViewModel,CadranPreviewViewModel,WindowGaugeViewModel}.cs`, `Text/{LibelleSource,TokenFormatter}.cs`, `Models/StatutServeur.cs`, `Interop/NativeMethods.cs`, `app.manifest`, `Chronos.csproj`, `Chronos.Tests.csproj`
- Tests du dépôt : `XamlWpfCollection`, `CadranBindingTests`, `ReglagesBindingTests`, `ThemingTests`, `TickRingTests`, `MainViewModelTests`, `ServicesLayerPurityTests`, `GardeTokensSansPourcentageTests`, `GardesDoctrineTests`, `GardesPerimetreTests`, `CompositionRootTests`, `AnalyseRelevesTests`, `BornesPlageTests`, `RenduLocalTokensTests`, `LecteurAgregatsTests`, `Fakes/{FakeUiDispatcher,FakeClock,FakeEtatJournal,FakeEtatReconstruction,FabriqueJournal}`, `TestData/journal/*`, `TestData/tokens/*`
- SUMMARY 32-04, 32-05 (D-32-21/22/23), 32-06 (D-32-25…30), 33-03 (API `IEtatReconstruction`), 33-04 (D-33-18…21), 33-05 (D-33-22, câblage DI réel) ; `.zeus/DESIGN_PLAN.md`, `.zeus/DOD.md`, ROADMAP/REQUIREMENTS/STATE
- Microsoft Learn — `System.Windows.Shell.WindowChrome` (PresentationFramework ; `CaptionHeight`, `ResizeBorderThickness`, `GlassFrameThickness`, `CornerRadius` sans effet si glass activé) — https://learn.microsoft.com/en-us/dotnet/api/system.windows.shell.windowchrome
- Microsoft Learn — `DWMWINDOWATTRIBUTE` (`DWMWA_WINDOW_CORNER_PREFERENCE = 33`, `DWMWA_BORDER_COLOR = 34`, Windows 11 build 22000) — https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute ; `DWM_WINDOW_CORNER_PREFERENCE` (`DWMWCP_ROUND = 2`) — https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_window_corner_preference
- Microsoft Learn — Optimizing Performance: 2D Graphics and Imaging (`StreamGeometry` plus léger que `PathGeometry`, `Drawing` vs `Shape`, `DrawingVisual`, `CachingHint` des `DrawingBrush`) — https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-2d-graphics-and-imaging
- Context7 `/dotnet/wpf` — source `UIElement.IsRenderable` (rendu refusé avant Measure/Arrange ; `RenderTargetBitmap.Render` contourne la garde), `RenderTargetBitmap.Render` (rend sans Measure/Arrange), `VisualBrush.DoLayout` (Measure/Arrange explicites hors arbre), `RibbonWindow.xaml` (usage de `WindowChrome`)

### Secondary (MEDIUM confidence)
- Retenue du contenu d'`OnRender` tant que la DP `AffectsRender` n'est pas invalidée (modèle de rendu retenu WPF) — cohérent avec la source lue, non exécuté ici (Open Question 4)
- `xmlns:sys="clr-namespace:System;assembly=mscorlib"` pour `sys:Double` sous .NET 8 (façade + type forwarding) — usage courant, à vérifier au premier build de 34-01

### Tertiary (LOW confidence)
- Rayon des coins DWM ≈ 8 px DIU à 100 % (observation courante, non documenté numériquement)

## Metadata

**Confidence breakdown :**
- Standard stack : HIGH — rien de nouveau, tout vérifié dans le csproj et l'environnement
- Architecture : HIGH sur les données (signatures lues), HIGH sur le moule des contrôles, MEDIUM sur le chrome (DWM/WindowChrome vérifiés en docs, pas exécutés)
- Pitfalls : HIGH pour les gardes et la promotion de palette (lus dans les tests), MEDIUM pour le test de rendu (Open Question 4)

**Research date :** 2026-09-27
**Valid until :** 2026-10-27 (30 jours — WPF stable ; à réviser si le csproj change de TFM ou si la phase 35 remanie `App.xaml.cs`)
