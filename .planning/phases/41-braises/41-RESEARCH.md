# Phase 41: Braises - Research

**Researched:** 2026-10-03
**Domain:** WPF (FrameworkElement.OnRender, XAML Canvas/Polygon, DynamicResource), MVVM CommunityToolkit, fuseau/format d'heure
**Confidence:** HIGH (tout est dans le code du dépôt et la maquette validée ; aucune bibliothèque nouvelle)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- L'anneau 5 h de Braises **est** la fenêtre de 5 h (pas de grille 24 h). Allumées = temps restant, départ midi, sens horaire
  (`EmberRingControl.cs:72-85`) : la dernière braise allumée recule vers midi ; le reset tombe toujours à midi.
- **20 braises de 15 min en 5 groupes de 4** : chaque groupe couvre 72°, pas de 13,2° entre braises d'un groupe (groupe centré
  dans son secteur), pastilles de rayon 4,0 à R66 ; le vide entre groupes est la délimitation (pas de tiret). Paramètres
  ajoutés à `EmberRingControl` (ex. `GroupSize`), l'anneau hebdo (12 braises, R44) reste inchangé.
- **Flèche fixe à midi**, extérieure : triangle 10 × 7 pointe vers l'anneau, filet de 12 px, couleur `TickReset` du thème (phase
  39), sans animation. Elle reste dans l'empreinte 170 × 170.
- Mode temps : 3e ligne au centre « ↻ HH:MM » (10,5, `TexteSecondaire`), heure LOCALE du reset 5 h issue de `resets_at`
  (exact) ; **absente si `resets_at` est inconnu** (exact ou rien). Mode pourcentages inchangé. Exposer l'heure du reset
  depuis `WindowGaugeViewModel` (aujourd'hui `ResetsAt` est privé) — texte formaté fr-FR, horloge injectée pour les tests.
- États : « en attente » garde 20 braises neutres ; « plancher » garde le contour pointillé.

### Claude's Discretion
Nom des propriétés ; formatage exact de l'heure (HH:mm).

### Deferred Ideas (OUT OF SCOPE)
Anneau hebdo 12 braises / 7 jours ; « ↻ HH:MM » sur les autres cadrans.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| BRA-01 | Anneau 5 h : 20 braises de 15 min en 5 groupes d'une heure séparés par un vide + flèche fixe à midi couleur `TickReset` | § Pattern 1 (fonction pure d'angle `BraisesGeometrie.Angle`), § Pattern 2 (DP `GroupSize`/`GroupPitch` rétrocompatibles), § Pattern 3 (flèche en XAML `Fill="{DynamicResource TickReset}"`) |
| BRA-02 | Mode temps : « ↻ HH:MM » (depuis `resets_at`) sous les deux comptes à rebours ; rien ne change en mode % | § Pattern 4 (`HeureResetTexte`/`HasHeureReset` dans `WindowGaugeViewModel`, fuseau injecté, calcul dans `Interpolate(now)`), § Pattern 5 (3e TextBlock), § galerie |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- C# / .NET 8 (`net8.0-windows`) / WPF / MVVM CommunityToolkit (`[ObservableProperty]`) / DI. Rendu XAML pur ou `OnRender`, **aucune dépendance native**.
- MVVM strict ; UI et commentaires **en français**.
- Honnêteté des chiffres : `resets_at` prioritaire ; ne jamais présenter une estimation comme exacte (ici : « exact ou rien » pour l'heure).
- Robustesse : donnée absente ≠ crash (heure absente → ligne masquée).
- Chemins sous profil utilisateur uniquement (tests : `Path.GetTempPath()`, motif `TempPaths()`).
- Travail via GSD (`/gsd:execute-phase`). Projet desktop .NET → orchestrateur ZEUS (pas ATHENA).
- Critère de revue § 9.6 du plan de design : aucune couleur hors `DesignTokens.xaml` / pinceaux du thème.

## Summary

La phase est entièrement interne : un contrôle existant (`Controls/Cadrans/EmberRingControl.cs`, `FrameworkElement` +
`OnRender` par pastille), une vue (`Views/Cadrans/CadranBraisesView.xaml`, grille 170 × 170 sans code-behind), une sous-VM
(`ViewModels/WindowGaugeViewModel.cs`) et la galerie (`ViewModels/CadranPreviewViewModel.cs`). Aucun paquet à ajouter.

La géométrie de référence est la fonction `braises(after,mode)` de la maquette : angle = `g·72 + (72 − 3·13,2)/2 + k·13,2`
avec `g = i / 4`, `k = i % 4`, soit un décalage de **16,2°** : les braises sont à 16,2 / 29,4 / 42,6 / 55,8°, puis +72° par
groupe, la dernière à 343,8°. Midi tombe donc **au milieu d'un vide de 32,4°** (contre 13,2° à l'intérieur d'un groupe) ;
c'est là que se loge le filet de la flèche. La flèche : triangle `(80,5) (90,5) (85,12)` et filet `x=85, y 13→25`, épaisseur
1,2, opacité 0,55, dans le repère 170 × 170 (centre 85,85). Elle tient dans l'empreinte.

L'heure du reset : `WindowState.ResetsAt` (5 h) vient toujours du serveur (OAuth, objet d'usage, en-têtes, ou dernier exact
mémorisé) ; aucune inférence ne la produit en production (`FiveHourWindowInference` est orphelin, `WeeklyRecalibration` ne
touche que l'hebdo). L'exposer comme texte calculé dans `Interpolate(now)` avec un `TimeZoneInfo` injecté dans le constructeur
(défaut `TimeZoneInfo.Local`) et le helper existant `TextesHistorique.HeureMinute(t, tz)`.

**Primary recommendation:** extraire la position angulaire dans une fonction pure `Rendering/BraisesGeometrie.cs` (testée
sans STA), ajouter deux DP rétrocompatibles `GroupSize` (défaut 1) et `GroupPitch` (défaut 0) à `EmberRingControl`, dessiner la
flèche en XAML dans `CadranBraisesView` avec `{DynamicResource TickReset}`, et exposer `HeureResetTexte` + `HasHeureReset` sur
`WindowGaugeViewModel`.

## Standard Stack

Aucun ajout. Tout existe dans le dépôt.

| Élément | Où | Rôle dans la phase |
|---------|----|--------------------|
| `EmberRingControl` | `src/Chronos/Controls/Cadrans/EmberRingControl.cs` | Rendu des pastilles, à étendre (groupes) |
| `CadranBraisesView.xaml` | `src/Chronos/Views/Cadrans/` | Flèche + 3e ligne de centre |
| `WindowGaugeViewModel` | `src/Chronos/ViewModels/` | Exposer l'heure du reset |
| `TextesHistorique.HeureMinute(t, tz)` | `src/Chronos/Text/TextesHistorique.cs:117` | Format « HH:mm » local, `InvariantCulture` (identique à fr-FR pour ce motif : séparateur `:`) |
| `BornesPlage.FuseauParisPourTests()` | `src/Chronos/Services/Historique/BornesPlage.cs:77` | Fuseau déterministe en test (jamais `Local` en test pur) |
| `TimeZoneInfo` singleton DI | `App.xaml.cs:310` | Fuseau de production (déjà enregistré) |
| xUnit 2.9.2 + `[WpfFact]` | `tests/Chronos.Tests` | Tests purs et STA |
| .NET SDK 10.0.201 | machine | build/test cible net8.0-windows |

## Architecture Patterns

### Fichiers touchés
```
src/Chronos/
├── Rendering/BraisesGeometrie.cs            # NOUVEAU — angle pur d'une braise (pas dans Controls/Cadrans, voir Pitfall 1)
├── Controls/Cadrans/EmberRingControl.cs     # + DP GroupSize, GroupPitch ; angles via BraisesGeometrie (rendu ET attente)
├── Views/Cadrans/CadranBraisesView.xaml     # 5 h : Count=20 PipRadius=4.0 GroupSize=4 GroupPitch=13.2 ; flèche ; 3e ligne
├── ViewModels/WindowGaugeViewModel.cs       # + ctor (TimeSpan, TimeZoneInfo? fuseau = null) ; HeureResetTexte / HasHeureReset
└── ViewModels/CadranPreviewViewModel.cs     # Push pose HeureResetTexte/HasHeureReset (échantillon)
tests/Chronos.Tests/
├── BraisesGeometrieTests.cs                 # NOUVEAU — pur [Fact]
├── WindowGaugeViewModelTests.cs             # + heure du reset (fuseau Paris, absente si inconnue/passée)
└── CadranBraisesBindingTests.cs (ou ajout dans CadranBindingTests) # [WpfFact] [Collection("XAML WPF")]
```

### Pattern 1 : fonction pure d'angle (BRA-01)
```csharp
// src/Chronos/Rendering/BraisesGeometrie.cs — motif ArcGeometry / DayTimeline (0° = midi, sens horaire)
namespace Chronos.Rendering;

/// <summary>Position angulaire PURE d'une braise sur l'anneau (degrés, 0 = midi, sens horaire).
/// groupSize ≤ 1, ou count non multiple de groupSize, ou pas trop large → répartition uniforme i·360/count
/// (comportement historique, anneau hebdo inchangé).</summary>
public static class BraisesGeometrie
{
    public static double Angle(int i, int count, int groupSize, double pasDansGroupe)
    {
        int n = Math.Max(1, count);
        if (groupSize <= 1 || n % groupSize != 0) return i * 360.0 / n;
        double secteur = 360.0 * groupSize / n;                       // 20/4 → 72°
        double largeur = (groupSize - 1) * pasDansGroupe;             // 3 × 13,2 = 39,6°
        if (pasDansGroupe <= 0 || largeur >= secteur) return i * 360.0 / n;
        int g = i / groupSize, k = i % groupSize;
        return g * secteur + (secteur - largeur) / 2 + k * pasDansGroupe; // groupe centré : 16,2 + k·13,2
    }
}
```
Valeurs attendues (20, 4, 13,2) : i=0 → 16,2 ; 3 → 55,8 ; 4 → 88,2 ; 19 → 343,8. Vide inter-groupes = 32,4° (et autour de midi :
343,8 → 376,2, centré sur 360). (12, 1, 0) → i·30 (hebdo inchangé).

### Pattern 2 : DP rétrocompatibles dans EmberRingControl
- `GroupSize` (int, défaut **1**) et `GroupPitch` (double degrés, défaut **0**), tous deux `AffectsRender`.
- Remplacer **les deux** calculs `i * 360.0 / n` (boucle « en attente » l.65 ET boucle nominale l.84) par
  `BraisesGeometrie.Angle(i, n, GroupSize, GroupPitch)`. Sinon l'état en attente garderait la répartition uniforme
  (décision : « en attente garde 20 braises neutres » — aux mêmes places).
- Ne rien changer à `lit = round(frac × n)`, à la demi-lueur de la dernière, au contour pointillé `Estimated`, à la cendre.
- Construire sur l'état laissé par la phase 39 : défauts de pinceaux à `null`, `WaitBrush` en DP (39-RESEARCH l.263). Aucune
  couleur littérale nouvelle (`Brushes.X`, `Frozen(0x…)` interdits par la garde `GardeCouleursCadrans`).

### Pattern 3 : flèche fixe en XAML (BRA-01)
Dans la `Grid Width="170" Height="170"` de `CadranBraisesView`, **avant** le texte du centre :
```xml
<!-- Flèche de braise : ligne d'arrivée du reset 5 h, fixe à midi, hors anneau (triangle 10 × 7 + filet 12 px).
     Structure, pas donnée : visible dans tous les états. Pas d'animation (le mouvement est réservé aux sessions). -->
<Canvas x:Name="FlecheReset" Width="170" Height="170" IsHitTestVisible="False">
    <Polygon Points="80,5 90,5 85,12" Fill="{DynamicResource TickReset}"/>
    <Line X1="85" Y1="13" X2="85" Y2="25" StrokeThickness="1.2" Opacity="0.55"
          Stroke="{DynamicResource TickReset}"/>
</Canvas>
```
Pourquoi XAML plutôt qu'un DP dans le contrôle : la couleur suit le thème par `DynamicResource` sans DP de pinceau
supplémentaire, l'anneau hebdo n'en hérite pas, et le test de liaison peut la trouver par nom. `IsHitTestVisible="False"` : la
phase 42 décidera des zones (le disque r 74 commence à y = 11 ; le triangle y 5→12 en sort presque entièrement — à signaler).

### Pattern 4 : heure du reset dans WindowGaugeViewModel (BRA-02)
```csharp
private readonly TimeZoneInfo _fuseau;
[ObservableProperty] private string _heureResetTexte = "";   // « ↻ 14:20 » ; vide si inconnue
[ObservableProperty] private bool _hasHeureReset;             // pilote la visibilité (motif HasTokens)

public WindowGaugeViewModel(TimeSpan windowLength, TimeZoneInfo? fuseau = null)
{ _windowLength = windowLength; _fuseau = fuseau ?? TimeZoneInfo.Local; ... }

// dans Interpolate(DateTimeOffset now), après CountdownText :
// exact ou rien : ResetsAt vient du serveur ; inconnu → rien ; déjà passé → rien (le compte à rebours dit « 0 min »).
HasHeureReset = _state.ResetsAt is { } rr && rr > now;
HeureResetTexte = HasHeureReset ? "↻ " + TextesHistorique.HeureMinute(_state.ResetsAt!.Value, _fuseau) : "";
```
- « Horloge injectée » = le `now` déjà passé à `Interpolate` (vient de `IClock` dans `MainViewModel.Interpolate`, l.697) +
  le fuseau injecté. Aucun `DateTime.Now` dans la VM.
- Les ~25 `new WindowGaugeViewModel(TimeSpan)` existants (tests, `MainViewModel` l.50-51, `CadranPreviewViewModel` l.17-18)
  compilent tels quels grâce au paramètre optionnel.
- Ne **pas** rendre `ResetsAt` public : exposer le texte calculé (une seule mise en mots, testée).
- Placer le calcul dans `Interpolate` (pas `Apply`) : `Apply` est suivi d'`Interpolate` dans `ApplySnapshot` → premier rendu
  immédiat ; et la condition « déjà passé » dépend de `now`.

### Pattern 5 : 3e ligne du centre (mode temps)
```xml
<TextBlock x:Name="HeureResetBraises" Text="{Binding FiveHour.HeureResetTexte}" HorizontalAlignment="Center"
           FontSize="10.5" Margin="0,2,0,0" Foreground="{DynamicResource TexteSecondaire}"
           Visibility="{Binding FiveHour.HasHeureReset, Converter={StaticResource BoolToVis}}"/>
```
Dans le `StackPanel` « mode TEMPS » seulement (sous `SevenDay.CountdownText`). Le panneau % n'est pas touché. Le StackPanel
reste centré : quand la ligne apparaît, les deux premières remontent d'environ 7 px — c'est aussi le cas de la maquette
(c−1 / c+14 / c+28 en temps contre c+2 / c+17 en %). Hauteur ≈ 50 px < diamètre utile de l'anneau hebdo (≈ 80 px).
Taille 10,5 : suivre la convention de la phase 40 (si elle crée des tokens de corps de cadran, en ajouter un ; sinon littéral
comme les 20/12/16/11 voisins). Le glyphe « ↻ » est déjà rendu dans Réglages (`ReglagesWindow.xaml:1013`).

### Galerie `--cadrans`
`CadranPreviewViewModel.Push` pose déjà les propriétés à la main (pas d'`Apply`/`Interpolate`). Ajouter pour la 5 h :
`g.HasHeureReset = true; g.HeureResetTexte = "↻ " + TextesHistorique.HeureMinute(baseLocale + reste, TimeZoneInfo.Local)`,
avec une base fixe ou un `IClock` optionnel (`SystemClock` par défaut ; le ctor sans paramètre reste pour
`CadranGalleryWindow.xaml.cs:16`). Hebdo : `HasHeureReset = false` (différé). La bascule `ShowCountdown` de la galerie montre
la ligne.

### Anti-patterns
- **Dupliquer la formule d'angle** dans la boucle d'attente et la boucle nominale : une seule fonction pure.
- **Tirets entre groupes** (copie d'Arcs) : rejeté par le plan de design § 8 ; la délimitation est le vide.
- **Halo/animation sur la flèche** : rejeté (§ 8, « Chanel »).
- **Afficher l'heure quand `ResetsAt` est null** ou une heure dérivée d'une estimation : contraire à « exact ou rien ».
- **`ToLocalTime()` dans la VM** : non testable ; utiliser le fuseau injecté.

## Don't Hand-Roll

| Problème | Ne pas écrire | Utiliser | Pourquoi |
|----------|---------------|----------|----------|
| Heure locale « HH:mm » | `ConvertTime` + `ToString` ad hoc | `TextesHistorique.HeureMinute(t, tz)` | Déjà testé, invariant, même fuseau injecté que l'Historique |
| Fuseau de test | `TimeZoneInfo.Local` | `BornesPlage.FuseauParisPourTests()` | Tests déterministes quelle que soit la machine |
| Visibilité bool | Converter maison | `BooleanToVisibilityConverter` déjà en ressource (`BoolToVis`) | Présent dans la vue |
| Couleur de la flèche | DP de pinceau + défaut coloré | `{DynamicResource TickReset}` | Thémé en phase 39, interdit de littéral par la garde |

## Common Pitfalls

### Pitfall 1 : garde « anti-mutisme » de la phase 39
**Ce qui casse :** la garde `GardeCouleursCadrans` prévue en 39 exige **exactement 4** `*.cs` dans `Controls/Cadrans` et 4
`*.xaml` dans `Views/Cadrans`. Un nouveau fichier de géométrie posé dans `Controls/Cadrans` la ferait échouer.
**Éviter :** mettre `BraisesGeometrie` dans `src/Chronos/Rendering/` (à côté d'`ArcGeometry`, `DayTimeline`). Vérifier le
compte réel dans la garde livrée par la phase 39 avant d'ajouter quoi que ce soit.

### Pitfall 2 : regex de couleurs en XAML
La garde interdit `Fill="…"`/`Stroke="…"` ne commençant pas par `{` (sauf `Transparent`). La flèche doit écrire
`Fill="{DynamicResource TickReset}"` et `Stroke="{DynamicResource TickReset}"`. `Opacity="0.55"` n'est pas visé.

### Pitfall 3 : `FindName` ne traverse pas le UserControl
`MainWindow.FindName("HeureResetBraises")` renvoie `null` : les noms de `CadranBraisesView` vivent dans le namescope du
UserControl. En test, appeler `vue.FindName(...)` sur l'instance de `CadranBraisesView` (trouvée par parcours visuel ou montée
seule), ou monter la vue seule dans un `Border` avec `DataContext` (motif 39-RESEARCH l.424).

### Pitfall 4 : DynamicResource non résolue en test isolé
Une vue montée hors `MainWindow` ne voit ni `DesignTokens.xaml` ni les pinceaux du thème : `Fill` vaut `null` (pas d'erreur,
mais rien de visible). Copier `theme.BrushTokens()` dans `hote.Resources` avant `Measure/Arrange` (motif de la phase 39), puis
comparer `Polygon.Fill` au pinceau `TickReset` du thème.

### Pitfall 5 : l'état « en attente » oublié
`OnRender` a deux boucles ; si seule la nominale passe par `BraisesGeometrie`, l'attente reste uniforme (20 pastilles à 18°)
et « saute » à l'arrivée des données. Test : angles de l'attente = angles nominaux.

### Pitfall 6 : MainViewModel et le fuseau
`FiveHour`/`SevenDay` sont des initialiseurs de propriété (`= new(TimeSpan.FromHours(5))`) : ils prendront
`TimeZoneInfo.Local` par défaut, ce qui est le comportement de production voulu. Les tests de liaison passant par
`MainViewModel` doivent donc calculer l'attendu avec `TimeZoneInfo.Local` (ou ne vérifier que préfixe/visibilité) ; les tests
purs passent `FuseauParisPourTests()` au constructeur.

### Pitfall 7 : arrondi des braises allumées
Avec 20 braises, `round(frac × 20)` éteint la dernière sous 7,5 min restantes alors que le compte à rebours affiche encore
« 7 min ». C'est le comportement existant (16 braises : sous 9,4 min), non modifié par la décision ; ne pas « corriger » ici.

### Pitfall 8 : zone de geste (phase 42)
Le triangle (y 5→12) est en grande partie hors du disque r 74 (y ≥ 11) prévu par GST-01. `IsHitTestVisible="False"` évite
qu'il capte des clics ; un test de silhouette par `RenderTargetBitmap` en phase 42 doit en tenir compte (pixels peints hors
silhouette). À reporter dans le SUMMARY pour la phase 42.

## Code Examples

### Test pur des angles
```csharp
public class BraisesGeometrieTests
{
    [Theory]
    [InlineData(0, 16.2)] [InlineData(3, 55.8)] [InlineData(4, 88.2)] [InlineData(19, 343.8)]
    public void Vingt_braises_en_cinq_groupes(int i, double attendu)
        => Assert.Equal(attendu, BraisesGeometrie.Angle(i, 20, 4, 13.2), 6);

    [Fact]
    public void Vide_entre_groupes_plus_large_que_le_pas_et_midi_au_milieu_du_vide()
    {
        double Vide(int a, int b) => BraisesGeometrie.Angle(b, 20, 4, 13.2) - BraisesGeometrie.Angle(a, 20, 4, 13.2);
        Assert.Equal(13.2, Vide(0, 1), 6);
        Assert.Equal(32.4, Vide(3, 4), 6);
        Assert.Equal(360 - BraisesGeometrie.Angle(19, 20, 4, 13.2), BraisesGeometrie.Angle(0, 20, 4, 13.2), 6); // symétrie midi
    }

    [Fact]
    public void Anneau_hebdo_inchange()
    { for (int i = 0; i < 12; i++) Assert.Equal(i * 30.0, BraisesGeometrie.Angle(i, 12, 1, 0), 6); }

    [Fact]
    public void Pas_trop_large_ou_compte_non_multiple_retombe_en_uniforme()
    {
        Assert.Equal(18.0, BraisesGeometrie.Angle(1, 20, 4, 30), 6);   // 3 × 30 ≥ 72
        Assert.Equal(360.0 / 18, BraisesGeometrie.Angle(1, 18, 4, 13.2), 6);
    }
}
```

### Test VM de l'heure
```csharp
[Fact]
public void Heure_du_reset_locale_quand_resets_at_connu()
{
    var tz = BornesPlage.FuseauParisPourTests();
    var vm = new WindowGaugeViewModel(TimeSpan.FromHours(5), tz);
    var now = new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero);          // 12:00 à Paris (UTC+2)
    vm.Apply(new WindowState { Kind = WindowKind.FiveHour, Reliability = SourceReliability.Exact,
                               Utilization = 0.4, ResetsAt = now.AddMinutes(140) });
    vm.Interpolate(now);
    Assert.True(vm.HasHeureReset);
    Assert.Equal("↻ 14:20", vm.HeureResetTexte);
}
// + ResetsAt null → HasHeureReset false, texte "" ; ResetsAt ≤ now → absent ;
// + plancher (Estimated) avec ResetsAt → présent (l'heure vient du serveur, seul le % est une borne) ;
// + passage heure d'hiver (25/10/2026) : reset 01:30Z → « ↻ 02:30 » (UTC+1).
```

### Test de liaison (STA)
```csharp
[WpfFact] // [Collection("XAML WPF")]
public void Braises_mode_temps_montre_la_fleche_et_l_heure()
{
    var theme = ThemeCatalog.Default;
    var vm = new CadranPreviewViewModel { SelectedTheme = theme, ShowCountdown = true };
    var vue = new CadranBraisesView();
    var hote = new Border { Child = vue, DataContext = vm };
    foreach (var kv in theme.BrushTokens()) hote.Resources[kv.Key] = kv.Value;
    hote.Measure(new Size(170, 170)); hote.Arrange(new Rect(0, 0, 170, 170));

    var heure = Assert.IsType<TextBlock>(vue.FindName("HeureResetBraises"));
    Assert.Equal(Visibility.Visible, heure.Visibility);
    Assert.StartsWith("↻ ", heure.Text);
    var fleche = Assert.IsType<Canvas>(vue.FindName("FlecheReset"));
    Assert.Same(theme.BrushTokens()["TickReset"], ((Polygon)fleche.Children[0]).Fill);   // ou comparer la couleur
    vm.ShowCountdown = false;                                                              // mode % : ligne cachée
    // le StackPanel temps est Collapsed → la ligne n'est pas rendue
}
// + via MainWindow (CadranBindingTests.BuildWindow, CadranStyle=Braises) : snapshot avec ResetsAt null → HasHeureReset faux.
```

## State of the Art

| Avant | Après (phase 41) | Impact |
|-------|------------------|--------|
| 16 braises uniformes (18,75 min), aucun repère | 20 braises (15 min) en 5 groupes d'une heure, vide = délimitation | Lecture « combien d'heures » d'un coup d'œil |
| Aucune marque de reset | Flèche fixe à midi `TickReset` | Ligne d'arrivée visible |
| Centre temps : 2 lignes | 3e ligne « ↻ HH:MM » (exact ou rien) | Heure absolue du reset |

## Open Questions

1. **Heure déjà passée** : la décision dit « absente si inconnu ». Recommandation : masquer aussi quand `ResetsAt ≤ now`
   (fenêtre entre le reset et le prochain relevé ; le compte à rebours affiche « 0 min »). Si le planificateur préfère la
   lettre stricte, retirer la clause `rr > now` (un seul endroit).
2. **Tokens de taille** : la phase 40 n'a pas encore livré ; si elle introduit des tokens de corps pour les cadrans, utiliser un
   token pour 10,5 au lieu du littéral. À vérifier au début de l'exécution.
3. **État exact de `EmberRingControl` après la phase 39** (DP `WaitBrush`, défauts `null`) : à relire avant d'éditer ; le plan
   doit s'appuyer sur le fichier livré, pas sur celui d'aujourd'hui.

## Environment Availability

| Dépendance | Requis par | Disponible | Version | Repli |
|------------|-----------|------------|---------|-------|
| .NET SDK | build/test | ✓ | 10.0.201 (cible net8.0-windows) | — |
| xUnit + `[WpfFact]` | tests | ✓ | xunit 2.9.2 | — |

Aucune dépendance externe nouvelle.

## Validation Architecture

### Test Framework
| Propriété | Valeur |
|-----------|--------|
| Framework | xUnit 2.9.2 + `[WpfFact]` (STA), collection `"XAML WPF"` pour le BAML |
| Config | `tests/Chronos.Tests/Chronos.Tests.csproj` |
| Quick run | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~BraisesGeometrie\|FullyQualifiedName~WindowGaugeViewModelTests\|FullyQualifiedName~CadranBraises\|FullyQualifiedName~GardeCouleursCadrans"` |
| Full suite | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` |

### Phase Requirements → Test Map
| Req | Comportement | Type | Commande | Fichier ? |
|-----|--------------|------|----------|-----------|
| BRA-01 | Angles 20/4/13,2 (16,2…343,8), vide 32,4°, midi centré dans un vide | unit pur | `--filter FullyQualifiedName~BraisesGeometrie` | ❌ Wave 0 |
| BRA-01 | Hebdo 12/1/0 = i·30 (rétrocompat) ; repli uniforme si pas trop large | unit pur | idem | ❌ Wave 0 |
| BRA-01 | Vue : 5 h `Count=20 PipRadius=4 GroupSize=4 GroupPitch=13.2` ; hebdo `Count=12` sans groupe | WPF liaison | `--filter FullyQualifiedName~CadranBraises` | ❌ Wave 0 |
| BRA-01 | Flèche `FlecheReset` : `Fill` = `TickReset` du thème pour chaque thème ; dans 0..170 | WPF liaison | idem | ❌ Wave 0 |
| BRA-01 | (option) rendu `RenderTargetBitmap` : alpha > 0 à 16,2° R66, alpha = 0 au milieu d'un vide (36°) sur l'anneau | WPF rendu | idem | ❌ Wave 0 |
| BRA-02 | `HeureResetTexte` = « ↻ HH:mm » local (Paris, DST) ; vide + `HasHeureReset=false` si `ResetsAt` null (et passé) | unit | `--filter FullyQualifiedName~WindowGaugeViewModelTests` | ✅ fichier existe, tests à ajouter |
| BRA-02 | Mode temps : ligne visible ; mode % : panneau % inchangé (2 TextBlock), ligne non rendue | WPF liaison | `--filter FullyQualifiedName~CadranBraises` | ❌ Wave 0 |
| BRA-01/02 | Aucune couleur littérale ajoutée (garde de la phase 39 verte) | statique | `--filter FullyQualifiedName~GardeCouleursCadrans` | ✅ (livré par 39) |
| manuel | Revue § 9.3 : flèche à midi, 5 groupes lisibles, « ↻ HH:MM » en mode temps, 100 % et 150 % | manuel | `Chronos.exe --cadrans` + overlay réel | — |

### Sampling Rate
- **Par commit de tâche :** quick run ci-dessus.
- **Par vague :** suite complète.
- **Porte de phase :** suite complète verte + capture galerie (modes % et temps) avant `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/BraisesGeometrieTests.cs` — BRA-01 (pur)
- [ ] `tests/Chronos.Tests/CadranBraisesBindingTests.cs` — BRA-01 flèche/paramètres, BRA-02 ligne (STA, `[Collection("XAML WPF")]`)
- [ ] Ajouts dans `tests/Chronos.Tests/WindowGaugeViewModelTests.cs` — BRA-02

## Sources

### Primary (HIGH)
- `src/Chronos/Controls/Cadrans/EmberRingControl.cs` (rendu actuel, deux boucles d'angle l.65 et l.84)
- `src/Chronos/Views/Cadrans/CadranBraisesView.xaml`, `src/Chronos/ViewModels/WindowGaugeViewModel.cs`, `CadranPreviewViewModel.cs`, `MainViewModel.cs:50-51, 697-711`
- `src/Chronos/Text/TextesHistorique.cs:117`, `src/Chronos/Services/Historique/BornesPlage.cs:77`, `App.xaml.cs:310`
- `.zeus/maquettes/cycle2-cadrans-themes.html` l.157-171 (`braises(after,mode)` : formule d'angle, flèche, 3e ligne)
- `.zeus/DESIGN_PLAN_CYCLE2.md` § 3, § 7, § 8, § 9 ; `.planning/phases/39-…/39-RESEARCH.md` (garde couleurs, `TickReset` thémé, motif de test par thème)
- `.zeus/reports/cycle2/inventaire-cadrans-gestes.md` (aucun test sur Braises aujourd'hui)

### Secondary / Tertiary
- Aucune : pas de recherche web nécessaire (pas de bibliothèque nouvelle ; API WPF `OnRender`/`Polygon`/`DynamicResource` déjà employées dans le dépôt).

## Metadata

**Confidence breakdown :**
- Stack : HIGH — aucun ajout, tout lu dans le dépôt.
- Architecture : HIGH — formule tirée de la maquette validée, points d'insertion lus.
- Pitfalls : MEDIUM-HIGH — dépendent de l'état exact livré par les phases 39 et 40 (non encore exécutées).

**Research date :** 2026-10-03
**Valid until :** fin de la phase 40 (relire `EmberRingControl` et la garde de la phase 39 avant de planifier les tâches)
