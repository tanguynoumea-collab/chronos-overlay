# Phase 5 : Cadran (RingArc + converters) + câblage View — Recherche

**Recherché :** 2026-07-08
**Domaine :** Rendu vectoriel WPF (Shape/ArcSegment) + converters MVVM + câblage XAML sur un pipeline de données déjà éprouvé (Phase 4)
**Confiance :** HIGH (géométrie WPF, converters, binding = patterns établis, code réel du VM lu ; interpolation couleur = math pure vérifiable)

---

<user_constraints>
## User Constraints (issues de 05-CONTEXT.md)

### Décisions verrouillées (à respecter à l'identique)

**Concept visuel (maquette validée) :**
- Cadran circulaire sombre, semi-transparent. Deux arcs concentriques : EXTÉRIEUR = fenêtre 5 h, INTÉRIEUR = fenêtre hebdo.
- LONGUEUR d'arc = temps restant avant reset (plein en début de fenêtre, se vide à l'approche du reset).
- COULEUR d'arc = utilization : dégradé vert → ambre → rouge ; GRIS quand utilization ≥ 1 (épuisé).
- Compte à rebours texte au CENTRE (5 h + hebdo).

**Tokens de design (valeurs exactes) :**
- Fond cadran `#16151B`, rim `#2C2B34`
- Ticks : mineurs `#34333D`, majeurs `#46454F`
- Arc 5 h (piste) `#2A2932`, arc hebdo (piste) `#26252E`
- Rampe utilization : vert `#7BB13C` (0 %) → ambre `#EFA23A` → rouge `#D8503A` (~100 %) → gris `#5A5960` (épuisé)
- Texte principal `#F4F2EC`, secondaire `#A9A8B2` / `#C7C6D0`

**Rendu (verrouillé) :**
- XAML pur : Path/ArcSegment, AUCUNE dépendance native.
- RingArc : contrôle réutilisable dérivé de `Shape` (PAS UserControl), `DefiningGeometry` surchargée, DP avec `FrameworkPropertyMetadataOptions.AffectsRender`, arc = figure ouverte tracée (StrokeThickness pour l'épaisseur), `StrokeStartLineCap`/`StrokeEndLineCap` = Round.
- Géométrie : repère WPF Y-inversé (`point = centre + R·(sin θ, −cos θ)`), `IsLargeArc = |sweep| > 180°`, cas 360° borné (~359.9°), `SweepDirection` cohérente.
- Pas d'animation continue / blur / shadow (AllowsTransparency = rendu logiciel). Mise à jour par binding sur changements de propriétés uniquement.

**Converters et honnêteté (verrouillé) :**
- `UtilizationToBrushConverter` : interpolation de la rampe (0 → vert, ~0.5-0.6 → ambre, ~1 → rouge), utilization ≥ 1 → gris `#5A5960` + mention « quota épuisé » (CAD-05).
- utilization == null (repli JSONL sans plafond) : l'arc ne doit PAS mentir — couleur neutre (piste ou gris doux) + marquage « estimée » (DAT-08). Ne jamais inventer une couleur d'utilization sur une donnée absente.
- DAT-08 : badge/mention « estimée » visible quand `SourceReliability == Estimated`. Staleness signalé (texte secondaire).
- ROB-01 : état « données indisponibles » (deux fenêtres Unavailable) — cadran visible avec pistes vides + texte « données indisponibles », zéro crash.

**Binding (verrouillé) :**
- Arc extérieur lié à `FiveHour.FractionRemaining`, couleur à `FiveHour.Utilization`.
- Arc intérieur idem pour `SevenDay`.
- Countdown central : textes formatés du VM (CountdownFormatter FR livré Phase 4).
- Aucune logique métier en code-behind ; converters + bindings.

### Discrétion de Claude
Dimensions exactes (fenêtre 220×220 — ajuster si besoin), épaisseurs d'arcs, tailles de police, position exacte du badge « estimée », arc de départ (12 h, sens horaire suggéré), détails des graduations.

### Idées différées (HORS SCOPE)
- Révélation au survol / tooltip détaillé (V2-02, V2-03). **Ne pas implémenter.**
</user_constraints>

---

<phase_requirements>
## Exigences de la phase

| ID | Description | Appui de la recherche |
|----|-------------|------------------------|
| CAD-01 | Cadran sombre gradué (ticks mineurs/majeurs), XAML pur, tokens validés | § Layout du cadran + `TickRing` (Shape unique, une passe géométrique) |
| CAD-02 | Arc extérieur 5 h : longueur = temps restant | § RingArc (DP `Fraction`) ← `FiveHour.FractionRemaining` |
| CAD-03 | Arc intérieur hebdo : longueur = temps restant | Idem ← `SevenDay.FractionRemaining` |
| CAD-04 | Couleur = utilization (vert→ambre→rouge) via converter | § `UtilizationToBrushConverter` + `RampColor.Interpolate` (math pure) |
| CAD-05 | Gris `#5A5960` + « quota épuisé » quand utilization ≥ 1 | Converter (branche `>= 1`) + texte lié à `Exhausted` |
| CAD-06 | Countdown central des deux fenêtres | Binding direct `FiveHour.CountdownText` / `SevenDay.CountdownText` (déjà FR) |
| CAD-07 | RingArc réutilisable (Shape, DefiningGeometry, DP AffectsRender) | § RingArc — code prêt à copier |
| DAT-08 | Badge « estimée » quand `Reliability == Estimated` | Binding `IsEstimated` → visibilité badge ; converter couleur reste neutre si utilization null |
| ROB-01 | Deux fenêtres Unavailable → « données indisponibles », zéro crash | Binding `DataUnavailable` → texte + pistes vides ; converter tolère null |
</phase_requirements>

---

## Résumé

Cette phase est **purement présentation** : tout le pipeline de données (providers, orchestrateur, `MainViewModel`, `WindowGaugeViewModel`) est livré et testé (Phase 4, 41 tests verts). La surface de binding est **figée et connue** — il n'y a plus qu'à câbler des propriétés existantes sur des visuels. Il n'y a **aucun risque de threading** ici (le VM franchit déjà la frontière), **aucun I/O**, **aucune logique métier** à écrire côté vue. Les seuls vrais travaux techniques sont deux briques de calcul PUR (donc testables unitairement) : (1) la **géométrie angle→arc** de `RingArc`, et (2) l'**interpolation RGB** de la rampe de couleur. Le reste est de l'assemblage XAML déclaratif.

Les deux pièges à ne pas rater sont documentés et connus : **`IsLargeArc` oublié** (l'arc bascule du mauvais côté au-delà de 180°) et le **cas dégénéré 360°** (départ = arrivée → arc invisible). La recommandation clé pour ce second point : au lieu de « clamper à 359.9° », **traiter la fraction ≥ 1 comme un anneau plein via `EllipseGeometry`** — plus propre visuellement (pas de micro-fente) et sans cas limite trigonométrique.

**Recommandation principale :** extraire toute la trigonométrie et l'interpolation couleur dans des **fonctions statiques pures** (`ArcGeometry.Build(...)`, `RampColor.Interpolate(double)`), testées en `[WpfFact]`, puis envelopper ces fonctions dans un `Shape` (`RingArc`) et un `IValueConverter` (`UtilizationToBrushConverter`) minces. Le converter reste **mono-entrée** (`utilization : double?` → `Brush`) — **aucun MultiBinding nécessaire** (voir § dédié). Layout du cadran = un `Grid` empilé (fond → rim → `TickRing` → 2 pistes → 2 arcs de valeur → texte central), binding **direct** sur les deux sous-VM nommés (pas de DataTemplate — seulement 2 instances).

---

## Skills UI (windows-wpf / frontend-design)

**Constat vérifié :** ni `windows-wpf` ni `frontend-design` ne sont présents localement. `C:/Users/Tanguy/.claude/skills/` ne contient que `dev-team-council` et `graphify` ; `/mnt/skills/public/` n'existe pas sur cette machine Windows. Les tâches UI de cette phase **ne peuvent pas** charger ces skills — le planner ne doit pas prévoir de step « activer windows-wpf/frontend-design ». Les règles pertinentes (Shape vs UserControl, AffectsRender, rendu logiciel sous AllowsTransparency, tokens en ResourceDictionary, contraste texte) sont couvertes ci-dessous et dans `ARCHITECTURE.md` / `PITFALLS.md`. CLAUDE.md mentionne d'« activer frontend-design + windows-wpf sur les tâches ui » — **directive non applicable faute de skills installés** ; à substituer par les patterns de ce document.

---

## Standard Stack

Aucune nouvelle dépendance. Tout est déjà présent et verrouillé (voir CLAUDE.md § Technology Stack).

### Core (déjà installé)
| Élément | Version | Rôle Phase 5 |
|---------|---------|--------------|
| WPF (`net8.0-windows`, `UseWPF`) | intégré | `Shape`, `PathGeometry`/`ArcSegment`/`EllipseGeometry`, `IValueConverter`, binding |
| CommunityToolkit.Mvvm | 8.4.2 | Déjà utilisé par les VM ; rien à ajouter côté vue |
| System.Windows.Media | intégré | `Color`, `SolidColorBrush`, `Pen`, `GeometryGroup` |

### À NE PAS ajouter
| Éviter | Pourquoi | À la place |
|--------|----------|------------|
| SkiaSharp / Direct2D / D3DImage | Dépendance native interdite (contrainte projet) + pics CPU sur fenêtre layered | `Path`/`ArcSegment`/`EllipseGeometry` XAML pur |
| Bibliothèque de gauges (LiveCharts, SciChart…) | Surdimensionné, dépendances, style non conforme aux tokens | `RingArc` maison (Shape, ~60 lignes) |
| Animations `Storyboard` continues | AllowsTransparency = rendu logiciel → saccades (PITFALLS #2) | Redessin sur binding uniquement (tick 1 s déjà en place) |
| MultiBinding pour la couleur | Inutile : la couleur dérive d'une seule valeur (utilization) | Converter mono-entrée `IValueConverter` |

---

## Architecture Patterns

### Structure de fichiers (dossiers déjà prévus, à créer)
```
src/Chronos/
├── Controls/
│   ├── RingArc.cs          # Shape : arc de valeur ET piste (Fraction=1 → anneau plein)
│   └── TickRing.cs         # Shape : toutes les graduations en une passe géométrique
├── Converters/
│   ├── UtilizationToBrushConverter.cs   # double? → Brush (rampe / gris / neutre)
│   └── (BooleanToVisibilityConverter WPF intégré — badge « estimée »)
├── Rendering/                            # math PURE, testable sans WPF-thread
│   ├── ArcGeometry.cs      # angle→point, sweep, IsLargeArc, cas vide/plein
│   └── RampColor.cs        # interpolation RGB des 3 stops
└── Views/
    └── MainWindow.xaml     # remplace le placeholder par le cadran complet
```
> **Rationale :** isoler la math pure (`Rendering/`) des enveloppes WPF (`Controls/`, `Converters/`) rend les tests unitaires triviaux et sans dépendance de thread. `RingArc` et `TickRing` ne contiennent quasi que des DP + un appel à la fonction pure.

### Pattern 1 : `RingArc` — Shape dérivé, DP `Fraction` (0..1)

**Décision clé vs ARCHITECTURE.md :** ARCHITECTURE.md suggérait de binder `EndAngle` sur `FractionTimeRemaining`. **À ne PAS faire tel quel** : `FractionRemaining` vaut 0..1, pas des degrés — binder directement produirait un arc de 0 à 1°. Exposer plutôt une DP **`Fraction` (0..1)** que le contrôle convertit en balayage (`sweep = 360 × Fraction`). Avantages : binding direct sur `FiveHour.FractionRemaining` (aucun converter d'angle), sémantique claire, testable. `StartAngle` reste une DP (défaut 0° = 12 h) pour la discrétion.

```csharp
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Chronos.Rendering;

namespace Chronos.Controls;

/// <summary>
/// Arc d'anneau réutilisable (CAD-07). Dérive de Shape : la géométrie est le pur produit
/// des DP (AffectsRender → redessin auto au changement de binding, pas d'animation).
/// Fraction 0..1 = longueur d'arc (1 = anneau plein). StartAngle = origine (0° = 12 h).
/// L'épaisseur vient de StrokeThickness (hérité), la couleur de Stroke (hérité).
/// Sert AUSSI de « piste » : une instance Fraction=1, Stroke = couleur de piste.
/// </summary>
public sealed class RingArc : Shape
{
    public static readonly DependencyProperty FractionProperty =
        DependencyProperty.Register(nameof(Fraction), typeof(double), typeof(RingArc),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StartAngleProperty =
        DependencyProperty.Register(nameof(StartAngle), typeof(double), typeof(RingArc),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RadiusProperty =
        DependencyProperty.Register(nameof(Radius), typeof(double), typeof(RingArc),
            new FrameworkPropertyMetadata(90d, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Fraction   { get => (double)GetValue(FractionProperty);   set => SetValue(FractionProperty, value); }
    public double StartAngle { get => (double)GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }
    public double Radius     { get => (double)GetValue(RadiusProperty);     set => SetValue(RadiusProperty, value); }

    protected override Geometry DefiningGeometry =>
        ArcGeometry.Build(new Point(ActualWidth / 2, ActualHeight / 2), Radius, StartAngle, Fraction);
}
```
> XAML : `StrokeThickness`, `Stroke` (via converter), `StrokeStartLineCap="Round"`, `StrokeEndLineCap="Round"` posés directement sur `<ctrl:RingArc>`.

### Pattern 2 : `ArcGeometry.Build` — géométrie PURE, cas limites gérés

**Cœur de la phase.** Résout les trois cas verrouillés : sweep 0 (invisible sans exception), sweep 360 (anneau plein sans dégénérescence), `IsLargeArc`.

```csharp
using System;
using System.Windows;
using System.Windows.Media;

namespace Chronos.Rendering;

/// <summary>
/// Géométrie d'arc PURE (aucun état, aucun I/O, aucun DispatcherObject requis en amont).
/// Repère WPF Y-inversé : 0° = 12 h (haut), sens horaire. point = centre + R·(sin θ, −cos θ).
/// </summary>
public static class ArcGeometry
{
    /// <summary>Point sur le cercle à l'angle <paramref name="deg"/> (0° = 12 h, horaire).</summary>
    public static Point PointAt(Point center, double radius, double deg)
    {
        double r = deg * Math.PI / 180.0;
        return new Point(center.X + radius * Math.Sin(r),
                         center.Y - radius * Math.Cos(r)); // −cos compense Y vers le bas
    }

    /// <summary>
    /// Arc de <paramref name="fraction"/> (0..1) du cercle depuis <paramref name="startAngle"/>, horaire.
    /// fraction ≤ 0 → Geometry.Empty (invisible, AUCUNE exception). fraction ≥ 1 → EllipseGeometry
    /// (anneau plein net, évite le cas dégénéré départ=arrivée). Sinon ArcSegment ouvert.
    /// </summary>
    public static Geometry Build(Point center, double radius, double startAngle, double fraction)
    {
        if (double.IsNaN(fraction) || fraction <= 0.0)
            return Geometry.Empty;                                   // sweep 0 : rien, pas d'exception
        if (fraction >= 1.0)
            return new EllipseGeometry(center, radius, radius);      // anneau plein sans micro-fente

        double sweep = 360.0 * fraction;                            // ∈ ]0, 360[
        var start = PointAt(center, radius, startAngle);
        var end   = PointAt(center, radius, startAngle + sweep);

        var fig = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        fig.Segments.Add(new ArcSegment(
            point:          end,
            size:           new Size(radius, radius),
            rotationAngle:  0,
            isLargeArc:     sweep > 180.0,                           // PIÈGE #1 résolu
            sweepDirection: SweepDirection.Clockwise,               // fraction toujours ≥ 0 ici
            isStroked:      true));
        return new PathGeometry(new[] { fig });
    }
}
```

**Points de vérification :**
- `fraction ≤ 0` → `Geometry.Empty` : l'arc « vide » (reset atteint) ne dessine rien, pas d'`ArgumentException`.
- `fraction ≥ 1` → `EllipseGeometry` : anneau plein continu, pas de fente de 0.1° et pas de départ=arrivée dégénéré. Comme le stroke est un cercle complet, `StrokeThickness` produit un anneau parfait avec les `LineCap` sans effet visible (cohérent).
- `isLargeArc = sweep > 180` : correct pour tout `fraction ∈ ]0.5, 1[`.
- L'anneau est une **figure ouverte tracée** (`IsFilled=false`) : on ne construit pas de donut à double arc — l'épaisseur vient du `StrokeThickness` (décision verrouillée).

### Pattern 3 : `TickRing` — graduations en une seule passe

**Question 3 tranchée :** ne PAS générer 60 éléments (`ItemsControl` de 60 items = 60 visuels → coûteux sur fenêtre layered en rendu logiciel, PITFALLS #2). Ne PAS écrire 60 `<Line>` à la main (immaintenable). Recommandation : un `Shape` `TickRing` qui construit **un `GeometryGroup` de segments** dans `DefiningGeometry` — une seule passe, un seul visuel, cheap. Deux instances (mineurs / majeurs) OU une DP `MajorEvery`. Le plus simple et conforme au pattern RingArc :

```csharp
public sealed class TickRing : Shape   // Controls/TickRing.cs
{
    // DP : Count (ex. 60), Radius, TickLength, StartAngle, AffectsRender sur toutes.
    protected override Geometry DefiningGeometry
    {
        get
        {
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            var group = new GeometryGroup();
            for (int i = 0; i < Count; i++)
            {
                double a = StartAngle + i * 360.0 / Count;
                var outer = ArcGeometry.PointAt(center, Radius, a);
                var inner = ArcGeometry.PointAt(center, Radius - TickLength, a);
                group.Children.Add(new LineGeometry(inner, outer));
            }
            return group;   // Stroke = couleur token, StrokeThickness = épaisseur du tick
        }
    }
}
```
> Deux `TickRing` empilés : l'un `Count=60` court en `#34333D` (mineurs), l'autre `Count=12` plus long/épais en `#46454F` (majeurs). Ou une seule instance avec DP `MajorEvery=5` dessinant les deux longueurs — **recommandation : deux instances** (plus simple, pas de branchement). Réutilise `ArcGeometry.PointAt` → math déjà testée.

### Pattern 4 : `UtilizationToBrushConverter` — mono-entrée, PAS de MultiBinding

**Question 2 tranchée.** La couleur de l'arc dépend d'**une seule valeur** : `Utilization (double?)`. Les trois branches sémantiques en dérivent :
- `null` (repli sans plafond) → **neutre** (piste/gris doux) : ne jamais inventer une couleur de rampe (honnêteté).
- `>= 1` → **gris `#5A5960`** (épuisé). `Exhausted` est *dérivable* de `utilization >= 1`, donc pas besoin de le passer au converter.
- `[0, 1[` → interpolation de la rampe.

→ **Aucun MultiBinding.** La provenance (`Reliability`/`IsEstimated`) ne change PAS la couleur de l'arc, elle pilote seulement le **badge texte « estimée »** (binding séparé sur `IsEstimated`). Garder deux préoccupations séparées = converter simple et testable.

```csharp
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Chronos.Rendering;

namespace Chronos.Converters;

/// <summary>
/// utilization (double?) → Brush. null → neutre (donnée absente, jamais inventée) ;
/// ≥ 1 → gris « épuisé » (CAD-05) ; [0,1[ → rampe vert→ambre→rouge (CAD-04).
/// Brushes gelés (Freeze) = partageables et légers.
/// </summary>
public sealed class UtilizationToBrushConverter : IValueConverter
{
    // Tokens verrouillés
    private static readonly SolidColorBrush Neutre  = Frozen(0x2A, 0x29, 0x32); // teinte piste douce
    private static readonly SolidColorBrush Epuise   = Frozen(0x5A, 0x59, 0x60); // #5A5960

    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        if (value is not double u) return Neutre;      // null / non-double → neutre
        if (u >= 1.0)             return Epuise;        // quota épuisé
        var col = RampColor.Interpolate(u);            // math pure
        var b = new SolidColorBrush(col); b.Freeze();
        return b;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    { var s = new SolidColorBrush(Color.FromRgb(r, g, b)); s.Freeze(); return s; }
}
```

### Pattern 5 : `RampColor.Interpolate` — interpolation RGB PURE (3 stops)

**Question 2 (positions des stops) — proposition :** stop ambre à **0.55** (dans la fourchette « ~0.5-0.6 » du contexte). Deux segments linéaires par canal RGB. Interpolation sRGB simple (suffisante pour une jauge ; le mélange n'est pas perceptuellement uniforme mais l'œil ne compare pas deux teintes côte à côte ici).

```csharp
using System;
using System.Windows.Media;

namespace Chronos.Rendering;

/// <summary>
/// Rampe utilization → couleur. 3 stops verrouillés :
///   0.00 → vert  #7BB13C (123,177,60)
///   0.55 → ambre #EFA23A (239,162, 58)     ← proposé (fourchette contexte ~0.5-0.6)
///   1.00 → rouge #D8503A (216, 80, 58)
/// Interpolation LINÉAIRE par canal sur chaque segment. Fonction PURE (testable).
/// </summary>
public static class RampColor
{
    private const double AmberStop = 0.55;
    private static readonly Color Green = Color.FromRgb(0x7B, 0xB1, 0x3C);
    private static readonly Color Amber = Color.FromRgb(0xEF, 0xA2, 0x3A);
    private static readonly Color Red   = Color.FromRgb(0xD8, 0x50, 0x3A);

    public static Color Interpolate(double u)
    {
        u = Math.Clamp(u, 0.0, 1.0);
        return u <= AmberStop
            ? Lerp(Green, Amber, u / AmberStop)
            : Lerp(Amber, Red, (u - AmberStop) / (1.0 - AmberStop));
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }
}
```
> **Bornes exactes à asserter en test :** `Interpolate(0)` = `#7BB13C` ; `Interpolate(0.55)` = `#EFA23A` ; `Interpolate(1)` = `#D8503A` ; `Interpolate(0.275)` ≈ mi-chemin vert/ambre. Le stop 0.55 est un choix de discrétion ajustable par le planner si besoin d'esthétique.

### Pattern 6 : Layout du cadran (structure XAML)

**Question 3 — structure recommandée** (empilement `Grid`, du fond vers le premier plan) :

```
Grid (220×220)
├── Ellipse           Fill=#16151B (fond, semi-transparent ex. #E616151B), Stroke=#2C2B34 (rim)
├── TickRing Count=60  Stroke=#34333D  (ticks mineurs)
├── TickRing Count=12  Stroke=#46454F  (ticks majeurs, plus longs/épais)
├── RingArc Fraction=1 Radius=RExt Stroke=#2A2932  (piste 5 h)
├── RingArc Fraction=1 Radius=RInt Stroke=#26252E  (piste hebdo)
├── RingArc Radius=RExt Fraction={FiveHour.FractionRemaining}  Stroke={FiveHour.Utilization|conv}
├── RingArc Radius=RInt Fraction={SevenDay.FractionRemaining}  Stroke={SevenDay.Utilization|conv}
└── StackPanel (centre, VerticalAlignment=Center)
    ├── TextBlock  {FiveHour.CountdownText}  Foreground=#F4F2EC  (principal)
    ├── TextBlock  {SevenDay.CountdownText}  Foreground=#A9A8B2  (secondaire)
    ├── TextBlock  "estimée"  Visibility={IsEstimated → converter}          (DAT-08)
    ├── TextBlock  "quota épuisé"  Visibility={Exhausted → converter}       (CAD-05)
    └── TextBlock  "données indisponibles"  Visibility={DataUnavailable}    (ROB-01)
```

**Recommandations de layout :**
- **Binding direct, pas de DataTemplate.** Seulement 2 arcs → deux `<RingArc>` nommés bindés sur `FiveHour`/`SevenDay` est plus lisible qu'un `ItemsControl` + `DataTemplate` (qui n'aurait de sens qu'avec N fenêtres variables).
- **Tokens en `ResourceDictionary`** (`Window.Resources` ou `App.xaml`) : `<SolidColorBrush x:Key="FondCadran">#16151B</SolidColorBrush>`, etc. Une seule source de vérité des couleurs (CLAUDE.md : « tokens en ressources XAML »). Le converter garde ses tokens de rampe en C# (calcul), les tokens statiques (fond/rim/pistes/texte) en ressources.
- **Rayons (discrétion) :** ex. RExt ≈ 90, RInt ≈ 70, StrokeThickness arcs ≈ 10-12, fenêtre 220×220 → centre (110,110). À caler visuellement (Hot Reload XAML).
- **Hit-testing :** le `Grid` racine peut rester `Background="Transparent"` seulement là où le drag sera nécessaire (Phase 6) ; en Phase 5 pas d'interaction, éviter un `Background=null` sur la zone visible.
- **Instancier le converter** comme ressource : `<conv:UtilizationToBrushConverter x:Key="UtilBrush"/>`. Pour la visibilité, `BooleanToVisibilityConverter` de WPF (intégré) suffit — pas de converter maison.

### Anti-patterns à éviter (rappel PITFALLS + spécifiques phase)
- **`IsLargeArc` en dur à `false`** → arc qui bascule au-delà de 180° (PITFALLS anti-pattern #5). Résolu par `sweep > 180`.
- **Binder `FractionRemaining` sur un angle en degrés** → arc microscopique. Résolu par la DP `Fraction` (0..1).
- **60 visuels de ticks** sous fenêtre layered → CPU. Résolu par `TickRing` (1 visuel, GeometryGroup).
- **MultiBinding pour la couleur** → verbeux et inutile. Converter mono-entrée.
- **Logique dans le code-behind** → interdit (décision verrouillée). Tout en converters + bindings ; `MainWindow.xaml.cs` reste tel quel (placement/topmost Phase 1).
- **Inventer une couleur quand `utilization == null`** → viole l'honnêteté. Le converter renvoie neutre.
- **Animation/blur/shadow** → rendu logiciel s'effondre (PITFALLS #2). Redessin sur binding seulement.

---

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser | Pourquoi |
|----------|-------------------|----------|----------|
| Bool → Visibility (badges) | Converter maison | `BooleanToVisibilityConverter` (WPF intégré) | Fourni par le framework |
| Anneau plein (fraction=1) | ArcSegment clampé à 359.9° | `EllipseGeometry` | Évite le cas dégénéré + micro-fente |
| Épaisseur d'anneau | Géométrie donut à double arc | `StrokeThickness` sur figure ouverte | Décision verrouillée, bien plus simple |
| Redessin au changement | Notifier/InvalidateVisual manuel | DP `AffectsRender` | WPF invalide et redessine seul |
| Formatage du countdown | Reformater dans la vue | `CountdownText` (déjà FR, Phase 4) | Livré et testé |
| Couleur partagée | Nouveau Brush par frame | `SolidColorBrush.Freeze()` | Léger, partageable, immuable |

**Insight clé :** presque tout ce dont la vue a besoin existe déjà (fraction, utilization, textes, provenance). La phase se réduit à deux fonctions pures + un assemblage XAML. Toute « logique » écrite dans la vue est un signal d'alarme.

---

## Common Pitfalls

### Pitfall 1 : `IsLargeArc` oublié
**Ce qui casse :** arc correct < 180°, puis bascule du mauvais côté du cercle dès qu'il dépasse le demi-tour — bug **intermittent** selon le temps restant.
**Éviter :** `isLargeArc: sweep > 180.0` (fait dans `ArcGeometry.Build`).
**Signe précoce :** l'arc « saute » quand FractionRemaining passe sous ~0.5.

### Pitfall 2 : cas dégénéré du cercle plein
**Ce qui casse :** un `ArcSegment` de 360° a départ = arrivée → WPF ne dessine rien (arc invisible pile quand la fenêtre est pleine).
**Éviter :** `fraction >= 1 → EllipseGeometry` (anneau plein continu). Ne PAS se contenter de 359.9° (laisse une fente).
**Signe précoce :** l'anneau disparaît en début de fenêtre.

### Pitfall 3 : `ActualWidth/ActualHeight` = 0 au premier rendu
**Ce qui casse :** `DefiningGeometry` appelé avant layout → centre (0,0), géométrie vide/fausse.
**Éviter :** `AffectsRender` force un redessin après mesure ; garder le calcul basé sur `ActualWidth/Height` (recalculé). Si un doute, retourner `Geometry.Empty` quand `ActualWidth <= 0`. La fenêtre a une taille fixe (220×220) donc risque faible, mais le test doit fixer `Width/Height` explicitement.
**Signe précoce :** arc absent tant que la fenêtre n'a pas été mesurée.

### Pitfall 4 : rendu logiciel (AllowsTransparency)
**Ce qui casse :** toute animation continue / blur / shadow s'effondre en CPU (PITFALLS #2).
**Éviter :** aucune `Storyboard`. Le tick 1 s (déjà en place) met à jour `Fraction` via binding → `AffectsRender` redessine. C'est suffisant et léger.

### Pitfall 5 : couleur mensongère sur donnée absente
**Ce qui casse :** afficher du vert (« tout va bien ») alors que `utilization == null` (repli sans plafond) trahit la Core Value.
**Éviter :** converter → neutre si null ; badge « estimée » via `IsEstimated`. Vérifier explicitement le cas `Reliability == Estimated` **avec** `Utilization == null` : couleur neutre + mention.

---

## Runtime State Inventory

*Non applicable — phase greenfield (nouveaux fichiers Controls/Converters/Rendering + réécriture du placeholder XAML). Aucun renommage, aucune migration de données, aucun état runtime externe. Vérifié : aucune donnée stockée, service live, tâche OS, secret ou artefact de build n'est concerné.*

---

## Binding — surface exacte disponible (code réel lu)

`DataContext = MainViewModel`. Propriétés bindables confirmées par lecture du code :

**`MainViewModel`** (`ViewModels/MainViewModel.cs`) :
| Propriété | Type | Usage vue |
|-----------|------|-----------|
| `FiveHour` | `WindowGaugeViewModel` | sous-VM arc extérieur |
| `SevenDay` | `WindowGaugeViewModel` | sous-VM arc intérieur |
| `DataUnavailable` | `bool` | ROB-01 → texte « données indisponibles » |
| `CapturedAt` | `DateTimeOffset?` | staleness (secondaire) |
| `IsStale` | `bool` | signaler donnée périmée (texte secondaire) |

**`WindowGaugeViewModel`** (`ViewModels/WindowGaugeViewModel.cs`) — sur `FiveHour` et `SevenDay` :
| Propriété | Type | Binding cible |
|-----------|------|---------------|
| `FractionRemaining` | `double` (0..1) | `RingArc.Fraction` (longueur, CAD-02/03) |
| `Utilization` | `double?` (0..1 ou null) | `RingArc.Stroke` via `UtilizationToBrushConverter` (CAD-04/05) |
| `CountdownText` | `string` (FR, ex. « 3 h 42 ») | `TextBlock` central (CAD-06) |
| `Exhausted` | `bool` | mention « quota épuisé » (CAD-05) |
| `Reliability` | `SourceReliability` | (info) |
| `IsEstimated` | `bool` | badge « estimée » (DAT-08) |

> Toutes sont des `[ObservableProperty]` (INotifyPropertyChanged généré) → binding réactif immédiat. Le tick 1 s (`MainViewModel.StartClock`, déjà câblé dans `MainWindow.xaml.cs`) rafraîchit `FractionRemaining`/`CountdownText` chaque seconde sans I/O. **Rien à ajouter côté VM.**

**DataTemplate vs binding direct :** binding direct recommandé (2 instances fixes). Ex. :
```xml
<ctrl:RingArc Radius="90" StrokeThickness="12"
              StrokeStartLineCap="Round" StrokeEndLineCap="Round"
              Fraction="{Binding FiveHour.FractionRemaining}"
              Stroke="{Binding FiveHour.Utilization, Converter={StaticResource UtilBrush}}"/>
```

---

## Code Examples

*(Les blocs complets prêts à copier figurent aux Patterns 1-5 : `RingArc`, `ArcGeometry.Build`, `TickRing`, `UtilizationToBrushConverter`, `RampColor.Interpolate`. Le squelette XAML figure au Pattern 6.)*

---

## State of the Art

| Ancienne approche | Approche retenue | Impact |
|-------------------|------------------|--------|
| `EndAngle` bindé sur fraction (ARCHITECTURE.md) | DP `Fraction` (0..1) → sweep interne | Binding direct, pas de converter d'angle, sémantique claire |
| Clamp 360° → 359.9° | `EllipseGeometry` pour anneau plein | Pas de micro-fente, pas de cas dégénéré |
| 60 `<Line>` ou ItemsControl | `TickRing` (Shape, GeometryGroup, 1 visuel) | Léger sous rendu logiciel, maintenable |
| MultiBinding utilization+reliability | Converter mono-entrée + badge séparé | Simplicité, testabilité |

**Rien de déprécié** — WPF `Shape`/`ArcSegment`/`EllipseGeometry`/`IValueConverter` sont stables depuis .NET Framework 3.0, inchangés en .NET 8.

---

## Open Questions

1. **Position exacte du stop ambre (0.55 proposé).**
   - Ce qu'on sait : contexte dit « ~0.5-0.6 » ; 3 couleurs exactes fixées.
   - Incertitude : valeur esthétique précise.
   - Recommandation : partir de **0.55**, ajuster visuellement en UAT (constante unique `AmberStop`).

2. **Opacité du fond du cadran (« semi-transparent »).**
   - Ce qu'on sait : fond `#16151B`, « semi-transparent ».
   - Incertitude : canal alpha exact.
   - Recommandation : ~90 % opaque (`#E616151B`) — lisible sur fond clair comme sombre ; caler en UAT.

3. **Rayons/épaisseurs des deux anneaux (discrétion).**
   - Recommandation : RExt≈90 / RInt≈70 / thickness≈10-12 sur 220×220 ; Hot Reload pour finaliser. Non bloquant.

---

## Environment Availability

*SKIPPED — phase code/XAML uniquement, aucune dépendance externe nouvelle. Le SDK .NET (cible `net8.0-windows`) est installé et fonctionnel (build + 41 tests verts en Phase 4). Aucun outil/service/runtime supplémentaire requis.*

---

## Validation Architecture

*(nyquist_validation = true dans config.json → section incluse.)*

### Test Framework
| Propriété | Valeur |
|-----------|--------|
| Framework | xUnit 2.9.2 + **Xunit.StaFact 1.1.11** (`[WpfFact]` pour thread STA) |
| Config | aucune (config dans `tests/Chronos.Tests/Chronos.Tests.csproj`, `net8.0-windows`, `UseWPF=true`, référence le projet app) |
| Run rapide (filtré) | `dotnet test tests/Chronos.Tests --filter "FullyQualifiedName~ArcGeometry\|FullyQualifiedName~RampColor"` |
| Suite complète | `dotnet test` (racine) |

> `[WpfFact]` (StaFact) exécute sur thread STA — requis pour instancier `SolidColorBrush`/`PathGeometry`/`RingArc`/`MainWindow`. La math sur `Point`/`Color` (structs) peut passer en `[Fact]`, mais **utiliser `[WpfFact]` par sécurité** dès qu'un type WPF est construit. Pattern déjà en place : `OverlayWindowConfigTests` construit `MainWindow` en `[WpfFact]`.

### Phase Requirements → Test Map
| Req | Comportement | Type | Commande automatisée | Fichier existe ? |
|-----|--------------|------|----------------------|------------------|
| CAD-07 | `ArcGeometry.PointAt` : 0°→haut, 90°→droite (Y-inversé) | unit | `dotnet test --filter "FullyQualifiedName~ArcGeometry"` | ❌ Wave 0 |
| CAD-02/03/07 | `Build` : fraction 0→Empty, 1→EllipseGeometry, 0.75→ArcSegment `IsLargeArc=true`, 0.25→`false` | unit | idem | ❌ Wave 0 |
| CAD-07 | `Build` : fraction ≤ 0 et NaN → `Geometry.Empty`, aucune exception | unit | idem | ❌ Wave 0 |
| CAD-04 | `RampColor.Interpolate` : 0→#7BB13C, 0.55→#EFA23A, 1→#D8503A, mi-segment corrects | unit | `dotnet test --filter "FullyQualifiedName~RampColor"` | ❌ Wave 0 |
| CAD-04/05 | `UtilizationToBrushConverter` : 0.3→couleur rampe, ≥1→#5A5960, null→neutre | unit | `dotnet test --filter "FullyQualifiedName~UtilizationToBrush"` | ❌ Wave 0 |
| DAT-08/ROB-01 | Cadran : `MainWindow` construit avec fake VM → aucun crash, RingArc/TextBlocks présents (FindName) | smoke | `dotnet test --filter "FullyQualifiedName~Cadran"` | ❌ Wave 0 |
| CAD-01/06, visuel | Aspect (tokens, ticks, lisibilité, couleurs, badges) | manual (UAT) | — (revue visuelle) | manuel justifié : rendu pixel non automatisable |

### Sampling Rate
- **Par commit de tâche :** `dotnet test --filter` ciblé sur la brique modifiée (math/converter) — < 5 s.
- **Par merge de wave :** `dotnet test` complet (les 41 tests existants + nouveaux).
- **Gate de phase :** suite complète verte avant `/gsd:verify-work` + UAT visuelle du cadran.

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/ArcGeometryTests.cs` — couvre CAD-02/03/07 (PointAt, sweep, IsLargeArc, cas vide/plein)
- [ ] `tests/Chronos.Tests/RampColorTests.cs` — couvre CAD-04 (3 stops exacts + interpolation)
- [ ] `tests/Chronos.Tests/UtilizationToBrushConverterTests.cs` — couvre CAD-04/05 (rampe / gris / neutre null)
- [ ] `tests/Chronos.Tests/CadranBindingTests.cs` — smoke `[WpfFact]` : `MainWindow` + fake VM, aucun crash, éléments présents (DAT-08/ROB-01)
- Framework : **rien à installer** (xUnit + StaFact déjà présents).

---

## Sources

### Primaire (HIGH)
- Code réel du dépôt : `MainViewModel.cs`, `WindowGaugeViewModel.cs`, `WindowState.cs`, `MainWindow.xaml(.cs)`, `App.xaml.cs`, `CountdownFormatter.cs`, `Chronos.Tests.csproj`, `OverlayWindowConfigTests.cs` — surface de binding et framework de test **vérifiés par lecture directe**.
- `.planning/research/ARCHITECTURE.md` § Pattern 2/3 (RingArc Shape, géométrie ArcSegment, IsLargeArc, cas 360°) — HIGH.
- `.planning/research/PITFALLS.md` #2 (rendu logiciel AllowsTransparency) — HIGH.
- `CLAUDE.md` § Technology Stack (versions verrouillées, XAML pur imposé) — HIGH.
- WPF `System.Windows.Media` (`ArcSegment`, `PathGeometry`, `EllipseGeometry`, `IsLargeArc`, `SweepDirection`, `Shape.DefiningGeometry`, `FrameworkPropertyMetadataOptions.AffectsRender`, `IValueConverter`) — API stable .NET 8, connaissance établie cohérente avec le repère WPF Y-down.

### Secondaire (MEDIUM)
- Choix `TickRing` (1 visuel via GeometryGroup) vs ItemsControl — raisonnement perf sous fenêtre layered (dérivé de PITFALLS #2, pratique WPF établie).

### Non trouvé / non applicable
- Skills `windows-wpf` et `frontend-design` : **absents localement** (vérifié : seuls `dev-team-council`, `graphify`). Substitués par les patterns de ce document.

---

## Metadata

**Confiance par domaine :**
- Surface de binding : **HIGH** — code réel lu, propriétés confirmées.
- Géométrie RingArc : **HIGH** — math pure vérifiable, cas limites explicitement traités.
- Interpolation couleur : **HIGH** — fonction pure, bornes assertables ; position du stop ambre = choix esthétique (MEDIUM, ajustable).
- Layout XAML : **HIGH** pour la structure ; dimensions exactes = discrétion (à caler en UAT).
- Testabilité : **HIGH** — framework et pattern `[WpfFact]` déjà en place.

**Date de recherche :** 2026-07-08
**Valide jusqu'à :** ~30 jours (WPF stable ; ne dépend d'aucune source externe mouvante).
