# Phase 40 : Cadrans à l'échelle 1 et orientations - Research

**Researched:** 2026-10-03
**Domain:** Mise en page WPF à taille fixe (retrait des Viewbox), dimensionnement d'une fenêtre layered liée à une empreinte, placement Win32 en pixels physiques (coin d'accroche, DPI mixte), contrôles `OnRender` à deux axes, réglage persisté tolérant, galerie
**Confidence:** HIGH pour l'existant (tout lu dans le dépôt le 2026-10-03, mesures de texte faites avec la vraie police) ; MEDIUM pour l'ordre exact des événements WPF au redimensionnement (raisonné, non instrumenté)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **Plus de Viewbox** autour des cadrans dans `MainWindow.xaml` (l.171-186). Empreintes contractuelles (DIP) :
  Arcs 170 × 170 ; Braises 170 × 170 ; Fusible H 190 × 92 / V 110 × 190 ; Marée V 132 × 160 / H 190 × 96 ;
  Volets H 190 × 66 / V 128 × 190. Tokens `CadranLargeur*` / `CadranHauteur*` dans `DesignTokens.xaml` ; fonction pure
  `EmpreinteCadran(style, orientation) → Size` testée ; la fenêtre lie Width/Height à l'empreinte (plus de 170 × 170 en dur).
- Corps de texte non réduits : libellés 11, valeurs 12, plaque Volets 13 (H) / 14 (V) ; la géométrie suit (Fusible H : cordons
  10 / 8 dans un sillon de 5,4, front avec étincelle ; Volets H : tuile 26, plaque 90 × 26, 6 volets 62 × 14).
- **Ancrage** : à tout changement d'empreinte (style, orientation), la fenêtre reste collée à son **coin d'accroche courant**
  (pas au coin le plus proche recalculé) et ne déborde jamais (DPI mixte, multi-écrans) — `OverlayController` / `CornerSnap`.
- Orientations (plan § 1.2) : DP `Orientation` sur `FuseBar`, `TideColumn`, `FlapRow` + géométrie pure par axe ; deux gabarits
  par vue de cadran. Fusible V brûle **de haut en bas** (cordon restant en bas, étincelle au front) ; Marée H : lumière depuis la
  gauche, ligne d'eau verticale **légèrement ondulée** ; Volets V : deux colonnes (tuile 26, plaque 56 × 40, 6 volets empilés
  16 × 104). États « en attente » (HasData=false), « plancher » (Estimated) et « indisponible » repris dans les deux sens.
- Réglage : enum d'orientation persisté **par cadran** (Fusible, Marée, Volets ; défauts = H, V, H), lu de façon tolérante (phase
  36). Carte « Orientation » (puces Horizontal · Vertical) dans Apparence sous les styles, visible seulement pour ces trois
  styles (modèle : carte « Mode étendu » visible seulement pour Arcs) ; indépendante de `VerticalLayout` (widget de sessions).
- Aperçu des réglages : montre l'empreinte réelle réduite dans 144 × 144 (seul Viewbox conservé) ; le test qui vérifie
  `Rect(0,0,170,170)` s'adapte. Galerie `--cadrans` : huit variantes (Arcs compris).
- Les couleurs ont été thémées en phase 39 : conserver ces pinceaux en redessinant.
- Vérifier que les pastilles d'Arcs et le mot « indisponible » restent centrés dans l'empreinte courante.

### Claude's Discretion
Gabarits (DataTemplate/trigger vs deux sous-vues) ; nom de l'enum d'orientation ; structure de la fonction d'empreinte.

### Deferred Ideas (OUT OF SCOPE)
None
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CAD-01 | Cadrans à l'échelle 1 (plus de Viewbox) ; la fenêtre prend l'empreinte du style ; empreintes en tokens, fonction pure testée | § Pattern 1 (EmpreinteCadran miroir de tokens), § Pattern 2 (Width/Height liés), § Gabarits mesurés, Pitfalls 1-3, 8 |
| CAD-02 | Changement d'empreinte → reste collé au coin d'accroche, ne déborde jamais (DPI mixte, multi-écrans) ; l'aperçu montre l'empreinte réelle | § Pattern 3 (recalage sur SizeChanged, coin courant en mémoire, `SWP_NOZORDER`), § Aperçu, Pitfalls 4-7 |
| CAD-03 | Fusible V (110 × 190, brûle de haut en bas), Marée H (190 × 96, ligne d'eau ondulée), Volets V (128 × 190), avec états attente / plancher / indisponible | § Pattern 4 (géométrie pure par axe), § Gabarits mesurés, § Mesure des textes (Marée H déborde avec la maquette), Pitfall 9 |
| CAD-04 | Carte Orientation (visible pour 3 styles), orientation mémorisée par cadran, indépendante de « Disposition verticale » ; galerie 8 variantes | § Pattern 5 (trois propriétés plates tolérantes), § Pattern 6 (carte + puces sur Tag), § Pattern 7 (galerie, extraction d'Arcs), Pitfalls 10-12 |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- C# / .NET 8 (`net8.0-windows`) / WPF / MVVM strict (CommunityToolkit.Mvvm `[ObservableProperty]` / `[RelayCommand]`) / DI. Dossiers Models / Views / ViewModels / Services.
- Rendu en XAML pur ou `OnRender` WPF, **aucune dépendance native** (pas de SkiaSharp).
- Fenêtre overlay : `WindowStyle=None`, `AllowsTransparency=True`, `Topmost=True`, `ShowInTaskbar=False` (ne pas toucher).
- Placement : jamais via `Window.Left/Top` (bug PerMonitorV2, déjà documenté dans `OverlayController`) ; toujours `SetWindowPos` en pixels physiques sur la `rcWork` du moniteur.
- Chemins sous profil utilisateur uniquement ; aucun droit admin.
- Honnêteté des chiffres : états « en attente », « plancher », « indisponible » conservés dans toutes les variantes.
- UI et commentaires **en français**.
- `Chronos.Services` reste pur (garde `ServicesLayerPurityTests`) : un enum d'orientation dans `ChronosSettings` ne peut PAS être `System.Windows.Controls.Orientation`.
- GSD : travail exécuté via `/gsd:execute-phase` ; ordre 36 → 37 → 38 → 39 → 40.
- Toutes les tailles et couleurs passent par `Resources/DesignTokens.xaml` ou par les pinceaux du thème (plan de design, règle directrice et critère de revue n° 6).

## Summary

Le travail est surtout de la **mise en page et du placement**, pas une nouvelle technologie. Trois constats du code changent le plan
par rapport à une lecture naïve du CONTEXT :

1. **Le recalage n'existe pas et doit être déclenché au bon moment.** `OverlayController` sait poser la fenêtre sur un coin
   (`CornerSnap.CornerToTopLeft`, pixels physiques), mais rien ne réagit à un changement de taille. Quand `Window.Width` change,
   WPF redimensionne le HWND en gardant le coin **haut-gauche** fixe : un overlay accroché à droite ou en bas déborde jusqu'au
   recalage. Le bon moment est `SizeChanged` de `MainWindow` (taille DIP réellement appliquée, HWND déjà redimensionné), avec trois
   gardes : ignorer la première mise en page (`PreviousSize` vide), ignorer pendant un `DragMove`, ne rien persister. Le coin à
   utiliser est le **coin courant** tenu en mémoire par le contrôleur (posé par `RestorePlacement` et `SnapToNearestCorner`), pas
   un coin recalculé. Ajouter `SWP_NOZORDER` à ce `SetWindowPos`, sinon `hWndInsertAfter = 0` (HWND_TOP) fait remonter la fenêtre
   et casse le mode arrière-plan.
2. **La persistance doit être en trois propriétés plates.** La lecture tolérante de la phase 36 (`SettingsService.Load`) travaille
   **propriété de premier niveau par propriété** : un dictionnaire `{ "Fusible": "Vertical", ... }` retomberait en bloc à la moindre
   valeur fautive. Trois enums `OrientationFusible` / `OrientationMaree` / `OrientationVolets` de type `OrientationCadran`
   (Services, neutre) profitent gratuitement du test générique `Chaque_enum_des_reglages_retombe_sur_son_propre_defaut`.
3. **La galerie « Arcs compris » impose d'extraire Arcs dans une vue.** Arcs vit en ligne dans `MainWindow.xaml` (l.26-166) et
   dépend de propriétés que `CadranPreviewViewModel` n'a pas (`DayFraction`, `DayResetAngles`, `DaySubTickAngles`,
   `IsModeEtendu/Normal`). L'extraire en `Views/Cadrans/CadranArcsView.xaml` change la portée des noms : 11 appels
   `fenetre.FindName("Arc…")` de `CadranBindingTests` doivent passer par la vue, et la garde de la phase 39 « exactement 4 xaml dans
   Views/Cadrans » passe à 5.

Autres constats : l'aperçu des réglages suit **déjà** la taille réelle (`VisualBrush` + `Viewbox` recalé sur `SizeChanged`,
`ReglagesWindow.xaml.cs:103-115`) — seul le test doit changer, et il doit forcer un `UpdateLayout()` pour voir le recadrage. Les
textes au pire cas (« ≥ 100 % », « 6 j 23 h ») ont été mesurés : la maquette de **Marée H** laisse 32 px à la valeur alors qu'il en
faut 43,2 ; la longueur de bande (non contractuelle) doit raccourcir. Toutes les autres variantes tiennent.

**Primary recommendation:** Construire d'abord le socle pur (tokens + `EmpreinteCadran` + `OrientationCadran` + géométries par axe
+ recalage pur dans `CornerSnap`), puis les vues à deux gabarits pilotées par une DP `Orientation` **sur la vue** (pas lue dans le
DataContext), puis `MainWindow` (Width/Height liés, recalage sur `SizeChanged`), enfin réglages, aperçu et galerie.

## Standard Stack

Aucune bibliothèque nouvelle. Tout est dans le framework et dans le dépôt.

### Core
| Élément | Version | Rôle | Pourquoi |
|---------|---------|------|----------|
| WPF (`FrameworkElement.OnRender`, `DrawingContext`) | net8.0-windows | Dessin de `FuseBar` / `TideColumn` / `FlapRow` | Déjà utilisé ; aucune dépendance native |
| `PathGeometry` + `QuadraticBezierSegment` | WPF | Ligne d'eau ondulée de Marée H | Reproduit exactement le `q3 h/4 0 h/2 q-3 h/4 0 h/2` de la maquette |
| CommunityToolkit.Mvvm | 8.4.2 (dépôt) | `[ObservableProperty]` / `[RelayCommand]` des orientations | Convention du projet |
| Win32 `SetWindowPos` / `GetWindowRect` / `MonitorFromWindow` / `GetMonitorInfo` | `Interop/NativeMethods.cs` | Recalage physique | Déjà en place ; ajouter la constante `SWP_NOZORDER = 0x0004` |
| xUnit 2.9.2 + Xunit.StaFact 1.1.11 | dépôt | `[Fact]` purs, `[WpfFact]` en collection `"XAML WPF"` | Infrastructure existante |

### Alternatives Considered
| Au lieu de | On pourrait | Pourquoi non |
|------------|-------------|--------------|
| Width/Height liés | `SizeToContent=WidthAndHeight` | Verrouillé par le CONTEXT et le conseil (« pas de SizeToContent ») ; la taille dépendrait du contenu (pastilles, texte) au lieu d'un contrat |
| Deux gabarits dans une même vue | Huit UserControl (une par variante) | Casse la garde phase 39 « exactement 4 xaml » de plus de 4 ; double les liaisons de thème ; la galerie et `MainWindow` devraient choisir parmi 8 types |
| Deux gabarits dans une même vue | `ContentControl` + `DataTemplate` choisi par trigger | Gabarit ré-instancié à chaque bascule, noms enfouis dans un template (tests plus durs) ; aucun gain ici |
| Dictionnaire d'orientations persisté | Trois propriétés plates | Voir Summary point 2 : tolérance valeur par valeur perdue |
| `System.Windows.Controls.Orientation` dans `ChronosSettings` | `OrientationCadran` neutre | Garde de pureté de Services |

**Installation :** aucune.

## Architecture Patterns

### Fichiers touchés (recommandé)
```
src/Chronos/
├── Resources/DesignTokens.xaml          # + 10 sys:Double CadranLargeur*/CadranHauteur* (+ corps 11/12/13/14, voir Pattern 1)
├── Rendering/EmpreinteCadran.cs         # NOUVEAU, pur : Size Pour(CadranStyle, OrientationCadran)
├── Rendering/GeometrieCadrans.cs        # NOUVEAU, pur : fusible / marée / volets par axe (Rect, Point, PathGeometry)
├── Placement/CornerSnap.cs              # + recalage pur sur coin imposé, borné à la zone de travail
├── Services/ChronosSettings.cs          # + enum OrientationCadran ; + OrientationFusible/Maree/Volets (défauts H, V, H)
├── Services/OverlayController.cs        # + coin courant en mémoire ; + RecalerSurCoinCourant() ; RestorePlacement lit Width
├── Interop/NativeMethods.cs             # + SWP_NOZORDER
├── Controls/Cadrans/FuseBar.cs          # + DP Orientation ; dessin via GeometrieCadrans ; étincelle
├── Controls/Cadrans/TideColumn.cs       # + DP Orientation ; ligne d'eau ondulée en H
├── Controls/Cadrans/FlapRow.cs          # + DP Orientation ; volets empilés en V ; écart 2,5
├── Views/Cadrans/CadranFusibleView.xaml(.cs)   # + DP Orientation (défaut Horizontal) ; 2 gabarits
├── Views/Cadrans/CadranMareeView.xaml(.cs)     # + DP Orientation (défaut Vertical) ; 2 gabarits
├── Views/Cadrans/CadranVoletsView.xaml(.cs)    # + DP Orientation (défaut Horizontal) ; 2 gabarits
├── Views/Cadrans/CadranArcsView.xaml(.cs)      # NOUVEAU : extrait de MainWindow.xaml l.26-166 (pour la galerie)
├── Views/MainWindow.xaml(.cs)           # Viewbox retirés ; Width/Height liés ; SizeChanged → recalage ; garde de drag
├── ViewModels/MainViewModel.cs          # orientations, LargeurCadran/HauteurCadran, EstStyleOrientable, commande
├── ViewModels/CadranPreviewViewModel.cs # + propriétés Arcs (jour, angles, mode)
├── Views/CadranGalleryWindow.xaml(.cs)  # 8 tuiles
└── Views/Reglages/ReglagesWindow.xaml   # carte « Orientation » après CarteModeEtendu (l.830)
tests/Chronos.Tests/
├── EmpreinteCadranTests.cs              # NOUVEAU (pur + miroir des tokens)
├── GeometrieCadransTests.cs             # NOUVEAU (pur)
├── CornerSnapTests.cs                   # + recalage par coin × empreinte × échelle, moniteur à origine négative
├── OrientationCadranTests.cs (ou MainViewModelTests)  # mémoire par cadran, persistance, indépendance de VerticalLayout
├── CadransOrientationBindingTests.cs    # NOUVEAU [WpfFact] : 8 variantes, gabarit visible, taille, états
├── CadranBindingTests.cs                # adapté (FindName via la vue Arcs ; largeur/hauteur par style)
├── ReglagesWindowTests.cs               # aperçu adapté ; carte Orientation
└── OverlayControllerTests.cs            # + drapeaux du recalage
```

### Pattern 1 : Empreinte = tokens XAML + miroir C# pur prouvé par test
**Quoi :** les dix valeurs vivent dans `DesignTokens.xaml` (source déclarée, motif `GardeTokensReglagesTests`) ; `EmpreinteCadran`
porte les mêmes constantes en C# et un test lit `DesignTokens.xaml` en `XDocument` pour prouver l'égalité clé par clé. C'est le
motif déjà utilisé par `ChronosTheme` (« miroir des clés de DesignTokens.xaml »). Une fonction qui chargerait le dictionnaire à
l'exécution ne serait plus pure (pack URI, STA).

Noms proposés (discrétion) : `CadranLargeurArcs`, `CadranHauteurArcs`, `CadranLargeurBraises`, `CadranHauteurBraises`,
`CadranLargeurFusibleH`, `CadranHauteurFusibleH`, `CadranLargeurFusibleV`, `CadranHauteurFusibleV`, idem `Maree*`, `Volets*`
(soit 14 clés si Arcs et Braises ont les leurs ; 10 si elles partagent `CadranLargeurRond`/`CadranHauteurRond`). Corps de texte
conseillés en tokens aussi : `CadranCorpsLibelle` 11, `CadranCorpsValeur` 12, `CadranCorpsPlaqueH` 13, `CadranCorpsPlaqueV` 14.

```csharp
// Rendering/EmpreinteCadran.cs — PUR (System.Windows.Size est une struct de WindowsBase, testable en [Fact])
public static class EmpreinteCadran
{
    public static Size Pour(CadranStyle style, OrientationCadran orientation) => (style, orientation) switch
    {
        (CadranStyle.Fusible, OrientationCadran.Horizontal) => new(190, 92),
        (CadranStyle.Fusible, OrientationCadran.Vertical)   => new(110, 190),
        (CadranStyle.Maree,   OrientationCadran.Vertical)   => new(132, 160),
        (CadranStyle.Maree,   OrientationCadran.Horizontal) => new(190, 96),
        (CadranStyle.Volets,  OrientationCadran.Horizontal) => new(190, 66),
        (CadranStyle.Volets,  OrientationCadran.Vertical)   => new(128, 190),
        _ => new(170, 170),   // Arcs, Braises : l'orientation est sans objet
    };
}
```

### Pattern 2 : la fenêtre lie Width/Height à l'empreinte
```xml
<!-- MainWindow.xaml : SizeToContent reste Manual -->
<Window ... SizeToContent="Manual"
        Width="{Binding LargeurCadran, Mode=OneWay}" Height="{Binding HauteurCadran, Mode=OneWay}">
```
- `MainViewModel` : `public double LargeurCadran => EmpreinteCadran.Pour(CadranStyle, OrientationCourante).Width;` (idem hauteur),
  notifiées dans `OnCadranStyleChanged` et dans les `On<Orientation…>Changed`.
- `DataContext` est posé dans le constructeur juste après `InitializeComponent()` : la liaison est active **avant** `Show()`, le
  HWND naît à la bonne taille, et `RestorePlacement` (sur `SourceInitialized`) voit la bonne largeur.
- Retirer les quatre `Viewbox` (l.171-186) : chaque vue est un enfant direct du `Grid` racine, qui a la taille de la fenêtre.
- Les tests `Assert.Equal(170d, fenetre.Width)` de `CadranBindingTests` (l.290-291, 346, 415-416, 671-672, 775) restent vrais
  (ils posent `CadranStyle = Arcs`) ; leurs commentaires « empreinte intacte » restent justes pour Arcs.

### Pattern 3 : recalage sur le coin courant, au `SizeChanged`
```csharp
// Placement/CornerSnap.cs — PUR : coin imposé + bornage (ne jamais déborder, même si la fenêtre dépasse la zone)
public static (double X, double Y) RecalerSurCoin(OverlayCorner coin, RectD fenetre, RectD travail, double marge)
{
    var (x, y) = CornerToTopLeft(coin, fenetre, travail, marge);
    x = Math.Max(travail.X, Math.Min(x, travail.Right - fenetre.Width));
    y = Math.Max(travail.Y, Math.Min(y, travail.Bottom - fenetre.Height));
    return (x, y);
}

// Services/OverlayController.cs
private OverlayCorner? _coinCourant;                    // posé par RestorePlacement ET SnapToNearestCorner

public void RecalerSurCoinCourant()
{
    if (_window is null || _hwnd == IntPtr.Zero) return;
    if (!NativeMethods.GetWindowRect(_hwnd, out var wr)) return;      // taille PHYSIQUE déjà appliquée par WPF
    var hMon = NativeMethods.MonitorFromWindow(_hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
    var mi = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
    if (!NativeMethods.GetMonitorInfo(hMon, ref mi)) return;
    double marge = Margin * VisualTreeHelper.GetDpi(_window).DpiScaleX;
    var coin = _coinCourant ?? _settings.Load().Corner;               // jamais NearestCorner ici (CONTEXT)
    var (px, py) = CornerSnap.RecalerSurCoin(coin, new RectD(wr.Left, wr.Top, wr.Right - wr.Left, wr.Bottom - wr.Top),
                                             ToRectD(mi.rcWork), marge);
    _setWindowPos(_hwnd, IntPtr.Zero, (int)Math.Round(px), (int)Math.Round(py), 0, 0,
        NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER);
    // AUCUNE persistance : coin et moniteur n'ont pas changé.
}
```
```csharp
// MainWindow.xaml.cs
private bool _enDeplacement;
SizeChanged += (_, e) =>
{
    if (e.PreviousSize.Width <= 0 || e.PreviousSize.Height <= 0) return;   // 1re mise en page : RestorePlacement l'a déjà fait
    if (_enDeplacement) return;                                            // un DragMove traversant un DPI ne doit pas être volé
    _controller.RecalerSurCoinCourant();
};
private void Cadran_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (e.ButtonState != MouseButtonState.Pressed) return;
    _enDeplacement = true;
    try { DragMove(); } finally { _enDeplacement = false; }
    _controller.SnapToNearestCorner();
}
```
- **Pourquoi `SizeChanged`** : il se lève après la mise en page, quand `ActualWidth/Height` (DIP) valent la nouvelle empreinte ; le
  changement de `Window.Width` a déjà redimensionné le HWND (gauche-haut fixe), donc `GetWindowRect` donne la nouvelle taille
  physique. Un changement de DPI ne change pas la taille DIP : il ne lève normalement pas `SizeChanged` (et `DpiChanged` garde son
  `SnapToNearestCorner` actuel). La garde `_enDeplacement` couvre le doute. Confiance MEDIUM sur l'ordre exact, d'où la vérification
  manuelle prévue (critère de revue § 9.1 : 8 variantes × 4 coins × 100 % / 150 %).
- `RestorePlacement` : remplacer `_window.ActualWidth/ActualHeight` (l.148-149) par `double.IsNaN(_window.Width) ? ActualWidth :
  _window.Width` (idem hauteur). Le code actuel fonctionne en production avec `ActualWidth`, mais la largeur liée explicite supprime
  la dépendance à l'ordre « mise en page avant `SourceInitialized` ». Poser `_coinCourant = s.Corner`.
- `SnapToNearestCorner` : poser `_coinCourant = corner` (déjà calculé l.85).
- Le saut d'une image (fenêtre agrandie vers la droite/bas, puis recalée) est accepté ; le noter pour la revue visuelle.

### Pattern 4 : géométrie pure par axe, contrôles à DP `Orientation`
Les contrôles gardent leurs DP actuelles (dont les pinceaux et `WaitBrush` ajoutés en phase 39) et gagnent
`Orientation` (`System.Windows.Controls.Orientation`, `AffectsRender`). Le calcul sort de `OnRender` dans des fonctions pures :

| Fonction | Horizontal | Vertical |
|---|---|---|
| Fusible (`taille`, `fraction`, `épaisseur`, sillon 5,4) | sillon `Rect(0, cy−2,7, w, 5,4)` ; front `fx = w − w·f` ; cordon `Rect(fx, cy−th/2, w−fx, th)` (reste à **droite**) | sillon `Rect(cx−2,7, 0, 5,4, h)` ; front `fy = h − h·f` ; cordon `Rect(cx−th/2, fy, th, h−fy)` (reste en **bas**) |
| Étincelle | cercle centre front, rayon `th/2 + 1,5`, opacité 0,9 | idem |
| Marée (`taille`, `fraction`) | lumière `Rect(0, 0, w·f, h)` depuis la **gauche** ; ligne d'eau à `x = w·f` : `M x,0 Q x+3,h/4 x,h/2 Q x−3,3h/4 x,h` | lumière `Rect(0, 0, w, h·f)` depuis le **haut** ; ligne d'eau horizontale à `y = h·f` (existant) |
| Volets (`taille`, `n`, écart 2,5) | `fw = (w − (n−1)·2,5)/n`, volet i à `x = i·(fw+2,5)` | `fh = (h − (n−1)·2,5)/n`, volet i à `y = i·(fh+2,5)` |
| Volets allumés | `lit = Round(f·n, AwayFromZero)` (inchangé), indices `< lit` (gauche / haut, comme la maquette) | idem |

- États dans les deux sens : **attente** (`HasData=false`) = cordon neutre pleine longueur à 0,6·th / canal voilé / volets neutres ;
  **plancher** (`Estimated`) = cordon à 50 % + trait brisé le long de l'axe / grain perpendiculaire à l'axe tous les 4 px dans la
  lumière + ligne d'eau pointillée (H : chemin ondulé pointillé) / plaque hachurée (XAML, inchangé) ; jamais sur le temps.
- Ligne d'eau tracée seulement si `0,001 < f < 0,999` (règle actuelle, à garder dans les deux sens).
- L'étincelle remplace l'encoche actuelle (trait vertical) ; sa couleur = `NotchBrush` (thémé en phase 39 sur `TextePrincipal`).
  **Ne pas** reprendre le `#FFF3D6` de la maquette : la garde phase 39 interdit toute couleur littérale dans `Controls/Cadrans/*.cs`.
- Rembourrage interne des contrôles à 0 (aujourd'hui `pad = 4` dans FuseBar, `2/3` dans TideColumn) : la position vient du gabarit.

### Pattern 5 : orientation persistée par cadran
```csharp
// Services/ChronosSettings.cs (neutre)
public enum OrientationCadran { Horizontal, Vertical }
public OrientationCadran OrientationFusible { get; init; } = OrientationCadran.Horizontal;
public OrientationCadran OrientationMaree   { get; init; } = OrientationCadran.Vertical;
public OrientationCadran OrientationVolets  { get; init; } = OrientationCadran.Horizontal;
```
- `MainViewModel` expose trois `[ObservableProperty] Orientation _orientationFusible/_orientationMaree/_orientationVolets`
  (type WPF, comme `ApercuSessions.RowOrientation` déjà converti l.316), initialisées depuis `_settings` au chargement (l.414-437).
- `EstStyleOrientable => CadranStyle is Fusible or Maree or Volets` ; `EstOrientationHorizontale` / `EstOrientationVerticale` pour
  le style courant (sélection des puces) ; tous notifiés dans `OnCadranStyleChanged` et à chaque changement d'orientation.
- Commande : `[RelayCommand] void ChoisirOrientation(OrientationCadran o)` — ignore si le style n'est pas orientable ; met à jour la
  seule propriété du style courant ; persiste avec relecture disque fraîche (`_settingsService.Load() with { … }`, GAP-1, motif
  `ToggleCadranMode`). Paramètre XAML typé : `CommandParameter="{x:Static svc:OrientationCadran.Vertical}"`.
- Aucun lien avec `VerticalLayout` / `ToggleVerticalLayout` (widget de sessions).

### Pattern 6 : carte « Orientation » des réglages
- Juste après `CarteModeEtendu` (`ReglagesWindow.xaml` l.813-830), `Border x:Name="CarteOrientation" Style="{StaticResource Carte}"`,
  `Visibility="{Binding EstStyleOrientable, Converter={StaticResource BoolToVis}}"` : titre « Orientation », aide courte, deux
  boutons « Horizontal » / « Vertical ».
- **Sélection** : le style `Puce` lit `IsSelected` du DataContext (l.204) ; ici le DataContext est le `MainViewModel`. Utiliser le
  motif « Tag » (`PuceStyleHistorique`, l.217-241) : `Tag="{Binding EstOrientationHorizontale}"`. Recommandation : ajouter au style
  `Puce` un second `DataTrigger` sur `Tag` (inoffensif pour les puces existantes, dont `Tag` est nul) plutôt que de dépendre de
  `PuceStyleHistorique`, que la phase 38 peut supprimer avec les puces de style Historique.
- Aucune taille ni couleur en dur (`GardeTokensReglagesTests.Aucune_couleur_ni_taille_en_dur_dans_la_fenetre_de_reglages`) ;
  marges littérales autorisées.

### Pattern 7 : vues à deux gabarits, DP `Orientation` sur la vue, galerie 8 variantes
- Chaque vue orientable porte une DP `Orientation` (défaut = orientation par défaut du cadran) et contient **deux** `Grid` nommés
  `GabaritHorizontal` / `GabaritVertical`, dont la `Visibility` suit la DP (`DataTrigger` sur
  `{Binding Orientation, RelativeSource={RelativeSource AncestorType=UserControl}}`, ou rappel de DP en code-behind de vue).
  Les deux gabarits lisent le même DataContext (`FiveHour`, `SevenDay`, `ShowPercent`…).
- **Pourquoi sur la vue et pas dans le DataContext** : la galerie affiche Fusible H et Fusible V **en même temps** sur un seul
  `CadranPreviewViewModel` ; `MainWindow` lie `Orientation="{Binding OrientationFusible}"` ; la galerie pose
  `Orientation="Vertical"` en littéral ; la phase 42 testera « chaque cadran × orientation » de la même façon.
- `MainWindow` : `<cadv:CadranFusibleView Orientation="{Binding OrientationFusible}" Visibility="{Binding IsStyleFusible, …}"/>`.
- **Galerie** (`CadranGalleryWindow`) : 8 tuiles (Arcs, Braises, Fusible H, Fusible V, Marée V, Marée H, Volets H, Volets V),
  `UniformGrid Columns="4"` dans un `ScrollViewer`, fenêtre élargie ; chaque vue posée dans un `Border` de la taille de son
  empreinte (pour voir l'empreinte réelle). Libellés de tuile : nom + empreinte (« Fusible — vertical · 110 × 190 »).
- **Arcs dans la galerie** : extraire `Views/Cadrans/CadranArcsView.xaml` (contenu de `MainWindow.xaml` l.26-166, centre compris) ;
  `MainWindow` l'utilise avec `x:Name="VueArcs"`. `CadranPreviewViewModel` gagne `IsModeEtendu` (false), `IsModeNormal`,
  `DayFraction`, `DayResetAngles`, `DaySubTickAngles` calculés par `Rendering.DayTimeline` sur un « maintenant » et un reset
  synthétiques dérivés de `FiveTimePct`.

### Gabarits mesurés (positions de la maquette `.zeus/maquettes/cycle2-cadrans-themes.html` § 1, en DIP)
| Variante | Empreinte | Placement |
|---|---|---|
| Fusible H | 190 × 92 | ligne 5 H : libellé gauche x 6 (ligne de base 16), valeur droite x 184 ; mèche x 6..184, axe y 32, cordon 10 ; ligne 7 J : base 62, axe y 77, cordon 8 |
| Fusible V | 110 × 190 | colonnes d'axe x 30 (5 H, cordon 10) et x 80 (7 J, cordon 8) ; libellé centré base 15 ; mèche y 24..162 ; valeur centrée base 182 |
| Marée V | 132 × 160 | colonnes 42 × 118 à x 14 et x 76, y 3 ; libellé centré base 137 ; valeur base 153 (existant) |
| Marée H | 190 × 96 | bandes de 32 de haut à y 10 et y 54 ; libellé gauche x 6 (base y+21) ; bande x 34, **longueur à réduire** (voir mesure) ; valeur alignée à droite x 184 |
| Volets H | 190 × 66 | lignes y 4 et y 36 : tuile 26 à x 0 ; plaque 90 × 26 à x 32 (corps 13) ; volets 62 × 14 à (128, y+6) |
| Volets V | 128 × 190 | colonnes x 4 et x 68 : tuile 26 à (x+15, 0) ; plaque 56 × 40 à (x, 32) (corps 14) ; volets 16 × 104 à (x+20, 80) |
| Braises | 170 × 170 | inchangé, désormais à l'échelle 1 (le Viewbox réduisait à 0,953) |

### Mesure des textes au pire cas (Segoe UI SemiBold, `FormattedText`, mesurée sur la machine le 2026-10-03)
| Corps | « ≥ 100 % » | « 6 j 23 h » |
|---|---|---|
| 12 (valeurs) | 43,2 | 40,0 |
| 13 (plaque H) | 46,8 | 43,4 |
| 14 (plaque V) | 50,4 | 46,7 |

- **Marée H** : la maquette laisse 152..184 = 32 px à la valeur → **débordement d'environ 11 px sur la bande**. La longueur 118 n'est
  pas contractuelle (seuls 190 × 96 et l'épaisseur 32 le sont) : bande ≈ x 32..134 (≈ 102), valeur dans 138..184 (46 px).
- Volets V : plaque 56 pour 50,4 → tient (2,8 px de chaque côté), serré. Fusible V : valeurs centrées à 30 et 80 → 8,4..51,6 et
  58,4..101,6, tient. Marée V, Fusible H, Volets H : larges marges.
- Prévoir un test qui mesure ces deux chaînes avec `FormattedText` dans la boîte de chaque variante (garde contre une future police).

### Anti-Patterns à éviter
- **Fusionner `DesignTokens.xaml` dans une vue de cadran** : ses replis statiques (Minuit) seraient trouvés **avant** les pinceaux du
  thème posés au niveau fenêtre → le cadran ne suivrait plus le thème (même piège documenté dans `HistoriqueWindow.xaml.cs:38`).
  Pour les tailles dans les vues : `{DynamicResource CadranLargeur…}` (résolu par l'hôte), ou pas de taille externe (la vue remplit
  la fenêtre), mais jamais une fusion locale. Les hôtes de test et la galerie fusionnent `DesignTokens.xaml` au niveau hôte.
- **`StaticResource` vers une ressource de la fenêtre depuis une vue** : la vue est construite (BAML) avant d'être parentée → introuvable
  sans `Application`. Les vues déclarent leurs convertisseurs localement (comme aujourd'hui `BoolToVis`).
- **Recalculer le coin le plus proche au changement de style** (interdit par le CONTEXT, et faux pour une fenêtre haute sur petit écran).
- **Persister à chaque recalage** : écriture disque inutile et risque d'écraser un réglage concurrent.
- **Toucher à `CentreHit` ou aux gestes** : la silhouette est la phase 42. Le disque 66 × 66 reste centré dans la fenêtre (il
  dépasse la hauteur de Volets H, 66 = 66 : sans effet).

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser | Pourquoi |
|----------|-------------------|----------|----------|
| Coins et bornage | Calculs de position dans `MainWindow` ou via `Left/Top` | `CornerSnap` pur + `SetWindowPos` physique | Bug PerMonitorV2 de `Window.Left/Top` déjà documenté ; testable sans écran |
| Courbe ondulée | Polyligne échantillonnée | `PathGeometry` + 2 `QuadraticBezierSegment` (gelée) | Identique à la maquette, nette à 150 % |
| Tolérance des réglages | Validation ad hoc de l'orientation | `SettingsService.Load` (phase 36) + enum défini | Retombée par propriété et diagnostic déjà en place |
| Aperçu réduit | Nouveau Viewbox ou capture bitmap | `VisualBrush` existant recadré sur `SizeChanged` | Déjà fait l.103-115 |
| Angles d'Arcs pour la galerie | Valeurs inventées | `Rendering.DayTimeline.ResetAngles/SubTickAngles/Fraction` | Même rendu que le vrai cadran |
| Mesure de texte en test | Estimations par nombre de caractères | `FormattedText` (Segoe UI SemiBold, fr-FR) | Mesure réelle |

## Common Pitfalls

### Pitfall 1 : tests qui mesurent la fenêtre sans template appliqué
**Ce qui se passe :** une fenêtre jamais affichée n'applique pas son template ; son `Content` n'a pas de parent visuel et les liaisons
ne s'évaluent pas (déjà documenté dans `CadranBindingTests.MonterPastille`, l.244-252). `fenetre.Width` lié, lui, s'évalue (la cible
est la fenêtre).
**Éviter :** pour les tests de taille, asserter `fenetre.Width/Height` ; pour les tests de gabarit, poser le DataContext sur la
racine, purger le Dispatcher (`ApplicationIdle`), puis `Measure/Arrange` à l'empreinte de la variante (pas 170 × 170).

### Pitfall 2 : `FindName` après extraction d'Arcs
**Ce qui se passe :** les noms `ArcHebdo`, `ArcCinqHeures`, `ArcVingtQuatreHeures`, `ArcTimelineNormal` passent dans la portée de
noms de `CadranArcsView` ; `fenetre.FindName(...)` renvoie null (11 appels dans `CadranBindingTests`).
**Éviter :** un assistant de test `ArcNomme(fenetre, nom) => ((FrameworkElement)fenetre.FindName("VueArcs")).FindName(nom)` et
remplacer les 11 appels ; `MotIndisponible`, `RangeePastilles`, pastilles et `CentreHit` restent dans `MainWindow`.
**Signe :** `Assert.IsType<RingArc>` échoue sur null.

### Pitfall 3 : garde phase 39 « exactement 4 xaml »
**Ce qui se passe :** `GardeCouleursCadransTests` (phase 39) vérifie 4 `*.xaml` dans `Views/Cadrans` et 4 `*.cs` dans
`Controls/Cadrans`. `CadranArcsView.xaml` fait passer le compte à 5.
**Éviter :** mettre à jour l'anti-mutisme à 5 dans le même commit (Arcs n'a aucune couleur littérale : vérifié, tout est
`DynamicResource` ou liaison). `CadransThemeBindingTests` (phase 39) peut garder ses 4 vues.

### Pitfall 4 : débordement transitoire et z-order
**Ce qui se passe :** le HWND grandit vers la droite/bas avant le recalage ; et `SetWindowPos(hwnd, IntPtr.Zero, …)` sans
`SWP_NOZORDER` place la fenêtre en HWND_TOP — en mode arrière-plan (HWND_BOTTOM), un recalage la ferait remonter.
**Éviter :** `SWP_NOZORDER` sur le recalage (le `TopmostGuard` gère seul le topmost) ; accepter le saut d'une image, le vérifier en
revue. (Les deux appels existants de `SnapToNearestCorner`/`RestorePlacement` ont le même défaut de z-order : le corriger aussi est
peu risqué, mais ce n'est pas exigé.)

### Pitfall 5 : recalage pendant un glisser entre écrans à DPI différents
**Ce qui se passe :** si un changement de DPI levait `SizeChanged` pendant `DragMove`, le recalage arracherait la fenêtre à la souris.
**Éviter :** drapeau `_enDeplacement` autour de `DragMove()` ; ignorer aussi `PreviousSize` vide.

### Pitfall 6 : coin « courant » perdu
**Ce qui se passe :** sans état en mémoire, on relirait `settings.Corner` — correct, mais une écriture concurrente (réglages) pourrait
le remplacer par une valeur ancienne.
**Éviter :** `_coinCourant` posé par `RestorePlacement` et `SnapToNearestCorner` ; `settings.Load().Corner` en repli seulement.

### Pitfall 7 : test de l'aperçu qui ne voit pas le recadrage
**Ce qui se passe :** `SizeChanged` d'un élément mis en page à la main est mis en file par le `LayoutManager` et levé au prochain
`UpdateLayout` ; sans lui, `Viewbox` reste à l'ancienne taille et le test est faux.
**Éviter :** adapter `L_apercu_montre_le_vrai_cadran…` (`ReglagesWindowTests.cs:416-431`) : grille 190 × 66 → `Rect(0,0,190,66)` ;
puis re-`Measure/Arrange` à 110 × 190 + `cadran.UpdateLayout()` → `Rect(0,0,110,190)`. `Stretch=Uniform` centre déjà l'empreinte
dans 144 × 144.

### Pitfall 8 : taille minimale imposée par Windows à une fenêtre étroite
**Ce qui se passe :** Windows impose une taille minimale de suivi aux fenêtres redimensionnables ; 110 DIP de large est plus étroit
que l'actuel 170.
**Éviter :** `ResizeMode=NoResize` + `WindowStyle=None` (déjà) ; le widget de sessions (`SessionsWindow`, même style) affiche déjà
de petites tailles sans problème. Vérifier `ActualWidth == 110` / `ActualHeight == 66` en exécution réelle (revue visuelle). LOW.

### Pitfall 9 : textes qui débordent
Voir la mesure : Marée H débordera si l'on copie la maquette. Test `FormattedText` recommandé.

### Pitfall 10 : test des commandes de réglages
**Ce qui se passe :** `Chaque_commande_de_l_ancienne_fenetre_est_liee_dans_la_nouvelle` (`ReglagesWindowTests.cs:303`) ne compte que
les commandes **affichées** ; avec le style par défaut (Arcs), la carte Orientation est masquée, et avec Fusible c'est
`CarteModeEtendu` qui l'est.
**Éviter :** ne pas ajouter `ChoisirOrientation` à cette liste ; écrire un test dédié avec `CadranStyle = Fusible` (carte visible,
deux boutons liés à `ChoisirOrientationCommand`, paramètres H/V, `Tag` suit l'orientation courante) et un autre avec Arcs (carte
masquée).

### Pitfall 11 : défaut de Marée = Vertical (index 1)
**Ce qui se passe :** un défaut implicite (`default(OrientationCadran)` = Horizontal) retournerait Marée au premier lancement.
**Éviter :** initialiseurs explicites (H, V, H) ; le test générique de phase 36 compare au défaut **de la propriété**, il attrapera
une erreur ; ajouter un test « `"OrientationMaree": 99` → Vertical ».

### Pitfall 12 : pastilles et mot « indisponible » dans les empreintes rectangulaires
**Ce qui se passe :** la rangée de pastilles (bas-droite, marge 6) et le mot (bas-gauche, marge 8,0,0,6) suivent automatiquement les
coins de la fenêtre, donc restent **dans** l'empreinte, mais ils recouvrent du contenu : Volets H (rangée sur les volets 7 J, mot sur
la tuile 7J), Fusible H (rangée sur la fin de la mèche 7 J).
**Éviter :** voir Open Question 1 ; dans tous les cas, test par variante : rangée et mot entièrement dans `Rect(0,0,empreinte)`,
sans intersection mutuelle ; garder les tests Arcs existants (> 71,5 px du centre).

## Code Examples

### Ligne d'eau ondulée (Marée H)
```csharp
// Rendering/GeometrieCadrans.cs — gelée, coordonnées locales du canal (maquette : q3 h/4 0 h/2 q-3 h/4 0 h/2)
public static Geometry LigneDEauOndulee(double x, double h, double amplitude = 3)
{
    var fig = new PathFigure { StartPoint = new Point(x, 0), IsClosed = false, IsFilled = false };
    fig.Segments.Add(new QuadraticBezierSegment(new Point(x + amplitude, h / 4), new Point(x, h / 2), true));
    fig.Segments.Add(new QuadraticBezierSegment(new Point(x - amplitude, 3 * h / 4), new Point(x, h), true));
    var g = new PathGeometry(new[] { fig });
    g.Freeze();
    return g;
}
```

### Fusible par axe
```csharp
public readonly record struct GeometrieFusible(Rect Sillon, Rect Cordon, Point Front, double RayonEtincelle);

public static GeometrieFusible Fusible(Size t, Orientation axe, double fraction, double epaisseur, double sillon = 5.4)
{
    double f = double.IsNaN(fraction) ? 0 : Math.Clamp(fraction, 0, 1);
    if (axe == Orientation.Horizontal)
    {
        double cy = t.Height / 2, fx = t.Width - t.Width * f;
        return new(new Rect(0, cy - sillon / 2, t.Width, sillon),
                   new Rect(fx, cy - epaisseur / 2, t.Width - fx, epaisseur), new Point(fx, cy), epaisseur / 2 + 1.5);
    }
    double cx = t.Width / 2, fy = t.Height - t.Height * f;          // brûle de HAUT en BAS : le cordon reste en bas
    return new(new Rect(cx - sillon / 2, 0, sillon, t.Height),
               new Rect(cx - epaisseur / 2, fy, epaisseur, t.Height - fy), new Point(cx, fy), epaisseur / 2 + 1.5);
}
```

### Test pur du recalage (motif `CornerSnapTests`)
```csharp
[Theory]
[InlineData(OverlayCorner.BottomRight, 1.0)]
[InlineData(OverlayCorner.BottomRight, 1.5)]
[InlineData(OverlayCorner.TopLeft, 1.5)]
public void Le_recalage_garde_le_coin_et_reste_dans_la_zone(OverlayCorner coin, double echelle)
{
    var travail = new RectD(-1920, 0, 1920, 1040);                     // moniteur secondaire à origine négative
    foreach (var (w, h) in new[] { (170d, 170d), (190d, 92d), (110d, 190d), (132d, 160d), (190d, 96d), (190d, 66d), (128d, 190d) })
    {
        var fen = new RectD(0, 0, w * echelle, h * echelle);
        var (x, y) = CornerSnap.RecalerSurCoin(coin, fen, travail, 12 * echelle);
        Assert.Equal(coin, CornerSnap.ClassifyCorner(fen with { X = x, Y = y }, travail));
        Assert.InRange(x, travail.X, travail.Right - fen.Width);
        Assert.InRange(y, travail.Y, travail.Bottom - fen.Height);
    }
}
```

## State of the Art

| Avant | Après | Impact |
|-------|-------|--------|
| Fenêtre 170 × 170 en dur, 4 styles réduits par Viewbox (Fusible 0,752, Volets 0,675) | Fenêtre liée à l'empreinte, rendu à l'échelle 1 | +20 % à l'écran pour Fusible et Volets ; Braises +5 % |
| Orientation codée en dur dans chaque contrôle | DP `Orientation` + géométrie pure | Trois nouvelles variantes |
| Aucun recalage au changement de style | Recalage sur le coin courant au `SizeChanged` | Plus de débordement après bascule |
| Galerie 4 tuiles, sans Arcs | 8 tuiles, empreinte réelle | Revue visuelle complète |

## Open Questions

1. **« Centrés dans l'empreinte » pour les pastilles et le mot « indisponible »**
   - Ce qu'on sait : le plan § 7 dit « le mot reste centré dans l'empreinte du cadran courant » ; mais pour Arcs, un test existant
     (EXA-03) **exige** que le mot soit à plus de 71,5 px du centre (bas-gauche), donc pas centré.
   - Recommandation : Arcs et Braises inchangés (coins, tests existants). Fusible / Marée / Volets : mot centré dans l'empreinte
     (`HorizontalAlignment/VerticalAlignment=Center` par un booléen VM `MotIndisponibleAuCentre`, `IsHitTestVisible=False`), rangée
     de pastilles gardée bas-droite. Test par variante (dans l'empreinte, pas d'intersection) ; recouvrements jugés en revue
     visuelle. À confirmer par l'utilisateur si le planificateur le juge ambigu.
2. **Tokeniser toute la géométrie interne ?** Le CONTEXT n'impose que les empreintes. Recommandation : empreintes + corps de texte
   en tokens ; positions internes littérales dans les gabarits (comme les marges des réglages), parce qu'elles n'ont de sens que
   dans leur gabarit.
3. **Ordre WPF exact au redimensionnement** (HWND redimensionné avant `SizeChanged`, aucun `SizeChanged` sur changement de DPI) :
   raisonné, non instrumenté. La garde de drag et le bornage pur rendent le comportement sûr dans les deux cas ; vérification en
   revue visuelle (§ 9.1).

## Environment Availability

| Dépendance | Requise par | Disponible | Version | Repli |
|------------|------------|-----------|---------|-------|
| .NET SDK | build / tests | ✓ | 10.0.201 (cible net8.0-windows) | — |
| Police Segoe UI | mesure des textes | ✓ | système | — |
| Second écran / DPI 150 % | revue CAD-02 | non vérifiable ici | — | test pur à origine négative + revue manuelle |

## Validation Architecture

### Test Framework
| Propriété | Valeur |
|----------|-------|
| Framework | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`, collection `"XAML WPF"`) |
| Config file | `tests/Chronos.Tests/Chronos.Tests.csproj` |
| Quick run command | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~EmpreinteCadran\|FullyQualifiedName~GeometrieCadrans\|FullyQualifiedName~CornerSnap\|FullyQualifiedName~OrientationCadran\|FullyQualifiedName~CadransOrientationBinding\|FullyQualifiedName~CadranBindingTests\|FullyQualifiedName~ReglagesWindowTests\|FullyQualifiedName~SettingsServiceTests\|FullyQualifiedName~OverlayController"` |
| Full suite command | `dotnet build Chronos.sln` (0 avertissement) puis `dotnet test Chronos.sln` |

### Phase Requirements → Test Map
| Req ID | Comportement | Type | Commande automatisée | Fichier existe ? |
|--------|----------|-----------|-------------------|-------------|
| CAD-01 | `EmpreinteCadran.Pour` rend les 8 empreintes (Arcs/Braises ignorent l'orientation) | unit | `--filter FullyQualifiedName~EmpreinteCadran` | ❌ Wave 0 |
| CAD-01 | Les tokens `CadranLargeur*/Hauteur*` de `DesignTokens.xaml` = `EmpreinteCadran` (XDocument) | unit | idem | ❌ Wave 0 |
| CAD-01 | `MainWindow.xaml` ne contient plus `<Viewbox` ; `Width`/`Height` liés (garde textuelle) | unit | `--filter FullyQualifiedName~EmpreinteCadran` | ❌ Wave 0 |
| CAD-01 | `fenetre.Width/Height` suivent style × orientation (8 cas) | WPF | `--filter FullyQualifiedName~CadranBindingTests` | ✅ à étendre |
| CAD-01 | Gabarit visible mis en page à l'empreinte exacte, corps 11/12/13/14 | WPF | `--filter FullyQualifiedName~CadransOrientationBinding` | ❌ Wave 0 |
| CAD-02 | `CornerSnap.RecalerSurCoin` : coin conservé, bornage, échelles 1 / 1,5, origine négative | unit | `--filter FullyQualifiedName~CornerSnap` | ✅ à étendre |
| CAD-02 | `RecalerSurCoinCourant` : `SWP_NOSIZE\|NOACTIVATE\|NOZORDER`, aucun changement de `Corner` persisté | WPF | `--filter FullyQualifiedName~OverlayController` | ✅ à étendre |
| CAD-02 | `MainWindow.xaml.cs` : `SizeChanged` → recalage, garde de drag (garde textuelle, motif `GardeGestesCadranTests`) | unit | `--filter FullyQualifiedName~GardeGestesCadran` | ✅ à étendre |
| CAD-02 | Aperçu : `Viewbox` = `Rect(0,0,190,66)` puis `Rect(0,0,110,190)` après `UpdateLayout` | WPF | `--filter FullyQualifiedName~ReglagesWindowTests` | ✅ à adapter |
| CAD-03 | Géométries pures par axe (fusible : reste à droite / en bas ; marée : lumière gauche / haut, ondulation ±3 ; volets : 6 rects 8,25 × 14 / 16 × 15,25) | unit | `--filter FullyQualifiedName~GeometrieCadrans` | ❌ Wave 0 |
| CAD-03 | Chaque variante rend sans exception en attente / plancher / exact, et le rendu n'est pas vide (`RenderTargetBitmap`) | WPF | `--filter FullyQualifiedName~CadransOrientationBinding` | ❌ Wave 0 |
| CAD-03 | « ≥ 100 % » et « 6 j 23 h » tiennent dans leur boîte pour chaque variante (`FormattedText`) | WPF | idem | ❌ Wave 0 |
| CAD-03 | Mot « indisponible » et rangée de pastilles dans l'empreinte, sans intersection, par variante | WPF | `--filter FullyQualifiedName~CadranBindingTests` | ✅ à étendre |
| CAD-04 | Orientation mémorisée par cadran (Fusible V puis Marée → Marée reste V par défaut ; retour Fusible → V), persistée, relue | unit | `--filter FullyQualifiedName~OrientationCadran` | ❌ Wave 0 |
| CAD-04 | Lecture tolérante : valeur fautive → défaut de la propriété (générique phase 36 + Marée 99 → Vertical) | unit | `--filter FullyQualifiedName~SettingsServiceTests` | ✅ (générique) + 1 cas |
| CAD-04 | `ChoisirOrientation` ne touche pas `VerticalLayout` ni l'inverse | unit | `--filter FullyQualifiedName~OrientationCadran` | ❌ Wave 0 |
| CAD-04 | Carte Orientation visible pour Fusible/Marée/Volets, masquée pour Arcs/Braises ; puces liées, `Tag` suit | WPF | `--filter FullyQualifiedName~ReglagesWindowTests` | ✅ à étendre |
| CAD-04 | Galerie : 8 variantes instanciées (types + orientations) | WPF | `--filter FullyQualifiedName~CadransOrientationBinding` | ❌ Wave 0 |
| (manuel) | 8 variantes × 4 coins × 100 % / 150 %, sans débordement après changement de style ou d'orientation ; lisibilité Volets V | manuel | `Chronos.exe` + `Chronos.exe --cadrans` | — (critère § 9.1) |

### Sampling Rate
- **Par commit de tâche :** commande rapide filtrée.
- **Par vague :** `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj`.
- **Porte de phase :** build 0 avertissement + suite complète verte avant `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/EmpreinteCadranTests.cs` — CAD-01 (pur, miroir des tokens, garde « pas de Viewbox »)
- [ ] `tests/Chronos.Tests/GeometrieCadransTests.cs` — CAD-03 (pur)
- [ ] `tests/Chronos.Tests/OrientationCadranTests.cs` — CAD-04 (VM + persistance, `TempPaths` sous `Path.GetTempPath()`)
- [ ] `tests/Chronos.Tests/CadransOrientationBindingTests.cs` — CAD-01/03/04 (`[Collection("XAML WPF")]`, hôte qui fusionne `DesignTokens.xaml` et pose les pinceaux du thème)
- [ ] Extensions : `CornerSnapTests`, `OverlayControllerTests`, `CadranBindingTests` (assistant `ArcNomme`), `ReglagesWindowTests`, `GardeGestesCadranTests`, `SettingsServiceTests`, garde phase 39 (4 → 5 xaml)

## Sources

### Primary (HIGH confidence)
- Code du dépôt lu le 2026-10-03 : `Views/MainWindow.xaml(.cs)`, `Services/OverlayController.cs`, `Placement/CornerSnap.cs`,
  `Controls/Cadrans/{FuseBar,TideColumn,FlapRow}.cs`, `Views/Cadrans/*.xaml`, `Views/CadranGalleryWindow.xaml(.cs)`,
  `ViewModels/{MainViewModel,CadranPreviewViewModel}.cs`, `Services/{ChronosSettings,SettingsService}.cs`,
  `Views/Reglages/ReglagesWindow.xaml(.cs)`, `Text/CountdownFormatter.cs`, `Text/PercentFormatter.cs`
- Tests lus : `CadranBindingTests`, `ReglagesWindowTests`, `GardeGestesCadranTests`, `GardeTokensReglagesTests`,
  `SettingsServiceTests` (test générique des enums), `OverlayControllerTests`
- `.zeus/DESIGN_PLAN_CYCLE2.md` § 1, § 7, § 9 ; maquette `.zeus/maquettes/cycle2-cadrans-themes.html` (fonctions `fusibleH/V`,
  `mareeV/H`, `voletsH/V`, `tideH`, `flaps`) ; `.zeus/reports/cycle2/inventaire-cadrans-gestes.md` ;
  `.zeus/reports/llm-council-2026-10-03.md` (synthèse points 1-2)
- `.planning/phases/39-*/39-RESEARCH.md` (pinceaux thémés, gardes `GardeCouleursCadransTests` / `CadransThemeBindingTests`)
- Mesure `FormattedText` (Segoe UI SemiBold, fr-FR) exécutée sur la machine le 2026-10-03

### Secondary (MEDIUM confidence)
- Ordre WPF « changement de `Width` → `SetWindowPos` du HWND → mise en page → `SizeChanged` », et absence de `SizeChanged` sur
  changement de DPI : connaissance du fonctionnement de `Window` / `LayoutManager`, non instrumentée ici.

### Tertiary (LOW confidence)
- Absence de taille minimale imposée à 110 DIP pour une fenêtre `WindowStyle=None` / `NoResize` (indice : `SessionsWindow`).

## Metadata

**Confidence breakdown:**
- Standard stack : HIGH — rien de nouveau, tout est dans le dépôt.
- Architecture : HIGH pour persistance, empreinte, gabarits, galerie (contraintes vérifiées dans le code et les tests) ; MEDIUM pour
  le moment du recalage (gardes prévues).
- Pitfalls : HIGH (tests et gardes existants lus, textes mesurés).

**Research date:** 2026-10-03
**Valid until:** fin de la phase 42 (qui réécrit les gestes de `MainWindow` et s'appuie sur les vues à DP `Orientation`)
