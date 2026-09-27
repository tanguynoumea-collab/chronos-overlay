---
phase: 34-fen-tre-historique-semaine-et-jour
plan: 05
subsystem: ui
tags: [wpf, xaml, window-chrome, dwm, historique, galerie, di, tdd, HIS-01]

# Dependency graph
requires:
  - 34-01 (tokens Histo*, palette promue, BoolToVis dans DesignTokens.xaml, garde « aucune valeur en dur »)
  - 34-03 (HistoriqueViewModel, ISourceHistorique, IReglagesHistorique, ScenariosHistorique, SourceHistoriqueDemonstration, TextesHistorique)
  - 32-03 (VerrouInstanceUnique : le mode --historique se place avant lui)
provides:
  - "Views/Historique/HistoriqueWindow.xaml(.cs) : coquille opaque redimensionnable (WindowChrome), tokens, en-tête §2.1, bandeau F2, Échap, géométrie bornée et persistée, coins DWM best-effort"
  - "Views/Historique/VueSemaineView.xaml(.cs) et VueJourView.xaml(.cs) : stubs vides nommés (Grid Racine), DataContext hérité — propriété de 34-06 / 34-07"
  - "Views/Historique/HistoriqueGalerie.cs : composition statique de la galerie --historique (D-34-26)"
  - "Placement/PlacementHistorique.cs : Borner(géométrie, écran virtuel, minima) pur + RectangleEcran"
  - "Interop/NativeMethods.cs : DwmSetWindowAttribute, DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2"
  - "App.xaml.cs : mode --historique avant le verrou ; DI TimeZoneInfo / ISourceHistorique / IReglagesHistorique / HistoriqueViewModel"
affects: [34-06, 34-07, 34-08, 35]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Fenêtre de consultation : WindowStyle=None + AllowsTransparency=False + WindowChrome (caption 0, bords 6) + DWMWCP_ROUND en SourceInitialized sous try/catch"
    - "Tailles de fenêtre en éléments-propriétés StaticResource APRÈS Window.Resources (D-34-25)"
    - "Épaisseurs Thickness / diamètres locaux nommés dans le dictionnaire de la fenêtre quand le dictionnaire commun est fermé"
    - "Bornage pur de la géométrie persistée (PlacementHistorique.Borner) appliqué avant Show ; persistance en WindowState.Normal seulement"
    - "Galerie CLI = composition statique avant le verrou mono-instance, réglages en mémoire, sans conteneur (motif --sessions)"
    - "Barre de progression sans converter : ProgressBar Minimum 0 / Maximum 1, template PART_Track + PART_Indicator"

key-files:
  created:
    - src/Chronos/Views/Historique/HistoriqueWindow.xaml
    - src/Chronos/Views/Historique/HistoriqueWindow.xaml.cs
    - src/Chronos/Views/Historique/VueSemaineView.xaml
    - src/Chronos/Views/Historique/VueSemaineView.xaml.cs
    - src/Chronos/Views/Historique/VueJourView.xaml
    - src/Chronos/Views/Historique/VueJourView.xaml.cs
    - src/Chronos/Views/Historique/HistoriqueGalerie.cs
    - src/Chronos/Placement/PlacementHistorique.cs
    - tests/Chronos.Tests/HistoriqueBindingTests.cs
    - tests/Chronos.Tests/PlacementHistoriqueTests.cs
  modified:
    - src/Chronos/Interop/NativeMethods.cs
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/CompositionRootTests.cs (+2)
    - tests/Chronos.Tests/GardesPerimetreTests.cs (+1)

key-decisions:
  - "D-34-22 — chrome sans transparence : WindowChrome (CaptionHeight 0 via ressource, ResizeBorderThickness = HistoBordRedimensionnement, GlassFrameThickness 0, UseAeroCaptionButtons False) ; coins arrondis + ombre par DwmSetWindowAttribute(33, ROUND) dans SourceInitialized, try/catch, échec silencieux testé (handle nul → false) ; Windows 10 = coins droits ; rayon DWM ≈ 8 px ≠ 16 (écart assumé pour la revue DAEDALUS)"
  - "D-34-23 — pas de fenêtre propriétaire, pas de Deactivated += Close, pas de Topmost ; drag sur l'en-tête (DragMove sous try/catch InvalidOperationException) ; seul code-behind : DataContext, thème, géométrie, DWM, fermeture"
  - "D-34-24 — segment « 4 semaines » présent, IsEnabled=False, ToolTip = TextesHistorique.InfobulleBientot, ToolTipService.ShowOnDisabled=True"
  - "D-34-25 — MinWidth / MinHeight / Width / Height / Background en éléments-propriétés StaticResource après Window.Resources ; épaisseurs Thickness (1 ; 1,5 ; 0,1,0,1 ; 0), diamètres (8 ; 7) et hauteur de caption (0) en ressources LOCALES nommées de la fenêtre — le dictionnaire commun est fermé à 38 sys:Double et hors périmètre de ce plan"
  - "D-34-26 — galerie --historique = HistoriqueGalerie.Creer() : composition statique branchée dans App.OnStartup APRÈS --sessions et AVANT base.OnStartup / VerrouInstanceUnique.Acquerir / Host ; SourceHistoriqueDemonstration + HorlogeFigee + ReglagesHistoriqueMemoire + ReconstructionEnCours (886 / 1603) ; badge de version [v3.3.0] omis en phase 34"
  - "Ouvrir() + DemarrerHorloge() dans Loaded (jamais dans le ctor) ; Closing → EnregistrerGeometrie (Normal seulement, ActualWidth/Height si chargée) ; Closed → ArreterHorloge"

patterns-established:
  - "La garde 34-01 interdit aussi Thickness=\"n\" et Height=\"n\" (donc CaptionHeight=\"0\", BorderThickness=\"1\", GlassFrameThickness=\"0\") : toute épaisseur passe par une ressource"
  - "Test d'alerte « journal muet » sur une fenêtre : horloge démarrée 30 min avant maintenant (l'alerte compte depuis max(démarrage, dernière écriture), D-32-21)"

requirements-completed: [HIS-01]

# Metrics
duration: ~30 min (15:43 → 16:12, 2026-09-27)
completed: 2026-09-27
tasks: 2
files: 14
tests_added: 15 (12 + 3)
suite: 1531 / 1531 verts deux fois sur l'arbre réel (1514 / 1514 deux fois dans snap-34-05) ; 0 warning sur mes fichiers
---

# Phase 34 Plan 05: Fenêtre Historique — coquille, chrome, galerie `--historique` et DI Summary

**Une vraie fenêtre Windows de consultation — `WindowStyle=None` sans transparence, redimensionnable par `WindowChrome`, coins DWM best-effort, tailles lues dans les tokens, position/taille bornées à l'écran virtuel et persistées en `Normal` seulement, Échap et ✕ → `FermerCommand`, en-tête §2.1 mot pour mot (« 4 semaines » désactivé avec « bientôt (phase 35) »), bandeau F2 qui vit et disparaît, pastille « journal muet » — ouvrable par `Chronos.exe --historique` sur la semaine des maquettes AVANT le verrou mono-instance et sans résoudre un service ; deux stubs vides attendent 34-06 / 34-07.**

SHA d'entrée : `56772cf` (1499 verts). HIS-01 **clos** (`requirements mark-complete HIS-01` exécuté).

## Ce que l'utilisateur lance pour la revue visuelle (DESIGN_PLAN §8)

```
dotnet run --project src/Chronos -- --historique
```
(ou, une fois l'exe publié en fin de phase : `Chronos-vX.Y.exe --historique`). L'agent n'a lancé aucun exe. La galerie ouvre la
fenêtre réelle sur la semaine de référence (jeu. 24 sept. 2026 17:12 Paris figé), bandeau F2 à 886 / 1603, corps VIDE (les vues
arrivent en vague 3) ; elle n'écrit ni ne lit `settings.json` (réglages en mémoire), coexiste avec l'overlay (avant le verrou).
Attendu à l'écran : bordure `Line` 1 px, fond `Panel`, coins arrondis ≈ 8 px et ombre sur Windows 11, redimensionnement par les
bords (6 px, invisibles), drag par l'en-tête, Échap ferme.

## Contrat pour 34-06 / 34-07 / 34-08

- **Stubs** : `Views/Historique/VueSemaineView.xaml(.cs)` et `VueJourView.xaml(.cs)` sont des `UserControl` dont le contenu est
  `<Grid x:Name="Racine"/>` VIDE et le code-behind `InitializeComponent()` seul. **Ils appartiennent désormais à 34-06 (Semaine) et
  34-07 (Jour)** : chacun remplit SON fichier, sans toucher `HistoriqueWindow.xaml`. Le test `Les_deux_vues_sont_hebergees_et_encore_vides`
  asserte `Children.Count == 0` : **34-06 / 34-07 doivent l'adapter** (le remplacer par une assertion sur leur propre contenu) — il est
  dans `HistoriqueBindingTests.cs`, fichier de ce plan, mais c'est la seule ligne qui leur appartient.
- **DataContext** : `HistoriqueViewModel` hérité de la fenêtre (posé dans le ctor de `HistoriqueWindow`) ; en test, poser aussi
  `racine.DataContext = vm` sur le `Content` de la fenêtre (rituel `Monter`).
- **Ressources** : `DesignTokens.xaml` est fusionné au niveau de la fenêtre hôte, et les pinceaux `vm.Theme.BrushTokens()` (dont
  `Alerte`, `TickReset`…) sont injectés dans `fenetre.Resources` → les vues emploient `{StaticResource Histo…}` pour le fixe et
  `{DynamicResource …}` pour les 12 clés du thème. Un `UserControl` construit SEUL (hors fenêtre) ne voit pas ces ressources.
- **Ressources locales de la fenêtre** (non exportées) : `HistoBordureFenetre` (1), `HistoBordureActive` (1,5), `HistoBordureBandeau`
  (0,1,0,1), `HistoBordureNulle` (0), `HistoDiametrePointLogo` (8), `HistoDiametrePastille` (7), `HistoHauteurCaption` (0). Promotion
  possible en 34-08 si les vues en ont besoin.
- **Noms d'éléments XAML** (pour 34-08) : `EnTete`, `BandeauF2`, `BarreF2` (`ProgressBar` 0..1), `PastilleAlerte`, `SelecteurStyle`
  (`Collapsed` en vue Jour). Styles locaux : `Plat`, `Segment` / `SegmentJour` / `SegmentSemaine`, `Puce` / `PucePistes` /
  `PuceSimplifie` / `PuceTuiles`.
- **Membres `internal` de la fenêtre** : `DemandesDeFermeture` (compteur), `EnregistrerGeometrie()`, `static ArrondirCoinsDwm(IntPtr)`.
- **Corps** : `Grid` (`Margin 16,6,16,12`) contenant les deux vues superposées, `Visibility` bindées sur `IsVueSemaine` / `IsVueJour`.

## Tâches, RED nommés, mutations

| Tâche | RED (commit) | GREEN (commit) | Mutations (rouge attendu → constaté) | sha256 avant = après |
|---|---|---|---|---|
| 1 — placement pur, dwmapi, fenêtre, stubs | 12 tests, `74886db` (compilation : `Chronos.Views.Historique`, `HistoriqueWindow`, `RectangleEcran` introuvables) | `f53a1f8` (24 / 24 sur le filtre du plan, 0 warning) | w1 `AllowsTransparency="True"` → `La_fenetre_est_une_fenetre_de_consultation…` ; w2 `Topmost="True"` → idem ; w3 sans test de `WindowState` → `La_geometrie_persistee…` ; w4 `IsEnabled="False"` retiré → `L_en_tete_commun…` ; w5 `FontSize="11"` → `GardeTokensHistoriqueTests.Aucune_couleur_ni_taille_en_dur…` | xaml `0e17eaf3fcf2c14e`, cs `f862a622978f4ca4` |
| 2 — galerie, `--historique`, DI, miroir, garde | 3 tests, `ef8ca1a` (compilation : `HistoriqueGalerie` introuvable ; puis, galerie créée seule : `Le_mode_historique_precede_le_verrou…` et `Le_conteneur_resout_la_fenetre_historique` rouges nommés, galerie verte) | `e3b398a` (50 / 50 sur le filtre élargi) | a1 branche déplacée après `Acquerir` (l. 85 > 71) → garde ; a2 `GetRequiredService<SettingsService>()` dans le bloc → garde ; a3 `AddSingleton<HistoriqueViewModel>()` retiré du miroir → miroir | App `21141a492488e201`, CompositionRootTests `daa5bdbf110c0d23` |

Toutes les mutations ont été appliquées par copie et révoquées par re-copie ; sha256 identiques à chaque fois. Première passe des
mutations a1–a3 restée verte parce que les regex perl ignoraient le CRLF d'`App.xaml.cs` (fichier non muté, sha inchangé) : rejouées
avec `\r?\n` en vérifiant que le sha du fichier muté DIFFÈRE avant chaque exécution.

**Où la suite a tourné** : Task 1 (RED, GREEN, w1–w5) sur l'arbre réel ; puis le RED de 34-04 (`3abc5ca`) a cassé la compilation du
projet de tests → Task 2 (GREEN, a1–a3, suite complète ×2 = **1514 / 1514**, Release 0 warning) dans l'instantané `snap-34-05`
(`git archive 56772cf` + mes 14 fichiers) ; dès le GREEN du voisin (`c58c3b6`), **suite complète deux fois sur l'arbre réel :
1531 / 1531, 0 échec** (1499 + 15 de ce plan + 17 de 34-04).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `CaptionHeight="0"` et les `Thickness` littéraux sont refusés par la garde 34-01**
- **Found during:** Task 1 (GREEN : `Aucune_couleur_ni_taille_en_dur…` nommait `HistoriqueWindow.xaml:175`)
- **Issue:** le motif de la garde `(FontSize|Height|MinHeight|StrokeThickness|Thickness|Opacity)="[0-9]` couvre `CaptionHeight="0"`,
  `GlassFrameThickness="0"`, `BorderThickness="1"`, `BorderThickness="0,1,0,1"` ; le critère grep du plan (`CaptionHeight="0"` = 1) est
  donc incompatible avec la garde, et `DesignTokens.xaml` (vague 1) n'expose que des `sys:Double`.
- **Fix:** ressources LOCALES nommées dans le dictionnaire de la fenêtre (`HistoHauteurCaption`, `HistoBordure*`, `HistoDiametre*`) ;
  `CaptionHeight="{StaticResource HistoHauteurCaption}"`. Le test asserte toujours `chrome.CaptionHeight == 0`. Critère grep
  `CaptionHeight="0"` = **0** (assumé).
- **Files modified:** `HistoriqueWindow.xaml` — **Commit:** `f53a1f8`

**2. [Rule 1 - Test incohérent] Alerte « journal muet » impossible avec l'horloge figée du scénario**
- **Found during:** Task 1 (conception du test)
- **Issue:** le plan monte le VM sur `ScenariosHistorique.HorlogeFigee(tz)` puis `vm.Tick(now)` avec `DerniereEcriture = now − 16 min` ;
  or l'alerte compte depuis `max(démarrage, dernière écriture)` (D-32-21) et `démarrage == now` → âge 0, jamais d'alerte.
- **Fix:** `Monter(..., clock: new FakeClock(Now − 30 min))` puis `Tick(Now)` — même procédé que `HistoriqueViewModelTests`.
- **Files modified:** `HistoriqueBindingTests.cs` — **Commit:** `74886db`

**3. [Rule 2 - Robustesse] Bornage tolérant et valeurs non finies**
- `PlacementHistorique.Borner` rend `null` si une valeur persistée n'est pas un nombre fini (settings.json corrompu → défaut centré),
  et le clamp ne lève jamais quand l'écran est plus petit que le minimum (le minimum l'emporte). `EnregistrerGeometrie` lit
  `ActualWidth/Height` quand la fenêtre est chargée (les DP `Width/Height` ne suivent pas toujours un redimensionnement utilisateur).

**4. [Rule 2 - Test] Échec DWM silencieux prouvé**
- `ArrondirCoinsDwm` est `internal static bool` ; le test de chrome asserte `ArrondirCoinsDwm(IntPtr.Zero) == false` sans exception
  (demande de la vague : « test que l'échec est silencieux »). Compte de `[WpfFact]` inchangé (9).

**5. Critères grep de commentaires**
- `Owner`, `WindowState.Normal` et `SourceHistoriqueDemonstration` étaient cités dans la doc XML : reformulés (« fenêtre
  propriétaire », « état normal », « source de démonstration ») pour respecter `Owner` = 0, `WindowState.Normal` = 1,
  `SourceHistoriqueDemonstration` = 1 (`e3b398a`). Le miroir `Le_conteneur_resout_la_fenetre_historique` ajoute une garde textuelle
  sur `App.xaml.cs` (les 4 lignes) pour qu'il soit RED tant que la production ne les porte pas.

### Écarts assumés (à dire, pas à masquer)

- **Rayon des coins** : DWM impose ≈ 8 px ; la maquette dit 16. Windows 10 : coins droits, sans ombre DWM (la bordure `Line` 1 px reste).
- **Badge de version `[v3.3.0]`** de la maquette §2.1 : omis en phase 34 (affaire de la release 35).
- **`ProgressBar` du bandeau F2 haute de 1,5 px** (`HistoEpaisseurBordureActive`, comme prescrit) : filet, pas barre — à juger en revue.

## Issues Encountered

- **Warning hors périmètre** : `tests/Chronos.Tests/PistesHistoriqueTests.cs(566,21): warning xUnit2031` (34-04) apparaît au build
  Release de l'arbre réel ; le build Release de `snap-34-05` (mes fichiers seuls sur `56772cf`) est à **0 warning**. Non touché
  (fichier de 34-04) — à relever par 34-04 ou 34-08.
- Fichiers non suivis / commits du voisin visibles pendant le plan (`Controls/Historique/*`, `Converters/HistoriqueConverters.cs`) :
  jamais stagés ; mes quatre commits ne touchent aucun fichier de `Controls/`, `ViewModels/`, `Services/`, `Rendering/`, `Resources/`
  (vérifié par `git show --name-only`).

## Known Stubs

- `VueSemaineView` / `VueJourView` : `Grid Racine` VIDE — **intentionnel**, remplis par 34-06 / 34-07 (contrat ci-dessus). Le corps de
  la fenêtre est donc vide en galerie tant que la vague 3 n'est pas passée : c'est le but de ce plan (coquille d'abord).
- `PuceSimplifie` / `PuceTuiles` : les commandes sont câblées et persistées ; le rendu des trois styles est l'affaire de 34-06.

## Verification

- `dotnet test Chronos.sln -c Debug --nologo -v q` : **1531 / 1531**, deux exécutions consécutives sur l'arbre réel (HEAD `e3b398a`
  + `c58c3b6` du voisin) ; **1514 / 1514** deux fois dans `snap-34-05`.
- `dotnet build Chronos.sln -c Release` : 0 warning dans `snap-34-05` ; 1 warning (×2 projets) sur l'arbre réel, dans un fichier de 34-04.
- Critères grep : xaml `AllowsTransparency="False"` ×1, `Topmost="False"` ×1, `ShowInTaskbar="True"` ×1, `shell:WindowChrome ` ×1,
  `Key="Escape"` ×1, `HistoLargeurMin` ×1, `BandeauF2` / `SelecteurStyle` / `PastilleAlerte` ×1, `ShowOnDisabled` ×1,
  `TextesHistorique.` ×9, littéraux (hex / FontSize / Height / StrokeThickness) **0**, `DesignTokens.xaml` ×1 ; cs `Deactivated` 0,
  `Owner` 0, `DwmSetWindowAttribute` 1, `WindowState.Normal` 1, `PlacementHistorique.Borner(` 1, `DemarrerHorloge()` 1 ; dwmapi 1,
  `= 33` 1, `public static class PlacementHistorique` 1, `<Grid x:Name="Racine"/>` 2 ; `[WpfFact]` 9, `[Collection("XAML WPF")]` 1,
  `[Fact]` 3 ; App `"--historique"` 1, `HistoriqueGalerie.Creer()` 1, les 4 `AddSingleton` ×1, `--historique` (l. 67) < `Acquerir`
  (l. 83) ; galerie : classe 1, `ReglagesHistoriqueMemoire` 1, `SourceHistoriqueDemonstration` 1, `SettingsService|ChronosPaths|GetRequiredService` 0.
- `git status --short` vide après le dernier commit de code ; ROADMAP.md / STATE.md non modifiés par ce plan ; aucun exe lancé ;
  aucune écriture sous `~/.claude`, `%APPDATA%\Claude`, `%APPDATA%\Chronos` (réglages en mémoire, dossiers temporaires).

## Commits

- `74886db` test(34-05): RED 12 — chrome, tokens, Échap, en-tête, segment/style, F2, pastille, géométrie, vues ; placement pur
- `f53a1f8` feat(34-05): HistoriqueWindow, stubs, PlacementHistorique, dwmapi (HIS-01)
- `ef8ca1a` test(34-05): RED 3 — miroir DI, garde --historique, galerie sans écriture
- `e3b398a` feat(34-05): mode --historique, DI, miroir et garde (HIS-01)

## Self-Check: PASSED

- 10 fichiers créés vérifiés présents + SUMMARY ; 4 commits vérifiés (`git cat-file -e`) : 74886db, f53a1f8, ef8ca1a, e3b398a.
- Suite 1531 / 1531 verte deux fois sur l'arbre réel ; `requirements mark-complete HIS-01` exécuté.
