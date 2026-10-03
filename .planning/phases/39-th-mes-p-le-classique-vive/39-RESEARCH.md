# Phase 39 : Thèmes Pâle / Classique / Vive - Research

**Researched:** 2026-10-03
**Domain:** Theming WPF (ressources dynamiques, pinceaux dérivés), contraste WCAG, regroupement d'ItemsControl, gardes statiques
**Confidence:** HIGH (tout est vérifié dans le code du dépôt ; les valeurs de gris ont été calculées en reproduisant exactement l'arrondi C#)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Catégories (propriété sur `ChronosTheme`) : **Pâle** = Nord, Forêt, Moka, Roseraie, Sauge, Lavande ; **Classique** = Minuit
  (défaut), Ardoise, Ambre chaud, Graphite, Marine ; **Vive** = Néon, Aurore, Synthwave, Lave. Section Thème des réglages :
  trois groupes titrés (10,5 semi-gras, `Ink2`), vignettes 88 × 76 inchangées.
- Nouvelles palettes (disque · piste · graduation · texte · vert · ambre · rouge), valeurs EXACTES :
  - Sauge `#262B28 #343B37 #AEB8B0 #DCE3DD #8FB996 #D9C27E #D08A7E`
  - Lavande `#22202C #302D3D #C3BCD9 #E6E2F2 #9FCFB0 #E8C88E #E08E9E`
  - Graphite `#121314 #26282B #C4C7CC #F2F3F5 #6DBE45 #F0A830 #E04B3C`
  - Marine `#0F1A2A #1E2D44 #B9C9DE #EAF0F7 #5DBB7A #F2B544 #E25C4F`
  - Synthwave `#140B24 #2A1745 #9AE6FF #F5EEFF #2BFF88 #FFD000 #FF2D55`
  - Lave `#1A0E0A #33190F #FFC9A3 #FFF1E6 #7CFF4F #FFB000 #FF3B1F`
  - Corrections : Néon ambre → `#FFC23D`, rouge → `#FF2E63` ; Aurore ambre → `#F0C36D`, rouge → `#F2577A` (décors inchangés).
- Gris épuisé : dans `From()`, plus petit mélange `Lerp(piste, graduation, t)` (t ≥ 0,18, pas de 0,01) qui atteint un contraste
  WCAG ≥ 3:1 contre le disque. Tests sur tout le catalogue : épuisé ≥ 3:1 ; rouge de rampe en teinte 335°–20° et ≥ 3:1 contre le
  disque ; `Alerte` = ambre ≠ rouge (garde existante). Le test « exactement 9 thèmes » devient un test d'invariants + 15 thèmes.
- `TickReset` entre dans `BrushTokens()` (thémé). `HistoGris` et le gris figé de `UtilizationToBrushConverter` suivent le gris
  épuisé du thème si c'est faisable sans casser l'Historique (sinon le noter dans le SUMMARY).
- Braises / Fusible / Marée / Volets : TOUTES les couleurs fixes de leurs vues (`#A9A8B2`, `#F4F2EC`, `#C7C6D0`, `#211D2A`,
  `#161019`, `#33000000`, `#55000000`, etc.) passent sur des pinceaux du thème (`TextePrincipal`, `TexteSecondaire`, un pinceau
  de tuile dérivé, un texte sur plaque) en `DynamicResource`. ATTENTION conflit de fichiers : la phase 40 redessinera ces vues ;
  ici on ne fait QUE remplacer les couleurs, sans toucher la géométrie.
- Hors périmètre : fonds clairs ; fenêtres Réglages et Historique (restent sombres).

### Claude's Discretion
Forme du regroupement (CollectionViewSource + GroupStyle ou trois ItemsControl) ; nom du pinceau de tuile.

### Deferred Ideas (OUT OF SCOPE)
Café, Tropique (réserve) ; thèmes clairs.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| THM-01 | Section Thème rangée en trois groupes titrés Pâle · Classique · Vive ; chaque thème porte sa catégorie | § Pattern 3 (groupes construits dans le VM, ItemsControl imbriqués), § Ordre du catalogue, tests `ReglagesWindowTests`/`ThemingTests` à adapter |
| THM-02 | Six nouvelles palettes aux valeurs §5.2 ; Néon et Aurore à rampe corrigée | § Catalogue cible (15 lignes `From`), test de palette exacte reconstructible depuis les propriétés de `ChronosTheme` |
| THM-03 | Gris épuisé ≥ 3:1 contre le disque et rouge 335°–20° ≥ 3:1, sur tout le catalogue, par test | § Pattern 1 (algorithme), § Tableau des 15 gris calculés (tous passent), § Validation Architecture |
| THM-04 | Braises, Fusible, Marée, Volets suivent le thème ; `TickReset` thémé ; plus aucune couleur fixe dans les 4 vues et leurs contrôles | § Inventaire exhaustif des couleurs fixes → pinceau du thème, § Pattern 2 (DP de pinceaux sans défaut coloré), § garde statique (regex) |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- C# / .NET 8 (`net8.0-windows`) / WPF / MVVM strict (CommunityToolkit.Mvvm `[ObservableProperty]` / `[RelayCommand]`) / DI.
- Rendu XAML pur, aucune dépendance native ; aucune nouvelle bibliothèque n'est nécessaire dans cette phase.
- UI et commentaires **en français**.
- Ne jamais présenter une estimation comme exacte (non concerné directement ; le canal « plancher » des cadrans ne doit pas
  perdre son grain lors du remplacement des couleurs).
- Toutes les tailles et couleurs passent par `Resources/DesignTokens.xaml` ou par les pinceaux du thème (DESIGN_PLAN_CYCLE2 §9.6).
- Pas d'`Assembly.Location` (les gardes lisent le source via l'attribut `CheminSourcesChronos` déjà injecté par MSBuild).
- Workflow GSD : modifications de code uniquement via `/gsd:execute-phase`.
- Orchestrateur : projet desktop .NET → ZEUS / `daedalus-wpf-design` (pas ATHENA).

## Summary

Tout le theming passe par `Theming/ChronosTheme.cs` : `From(key, name, disc, track, tick, ink, green, amber, red)` dérive
toutes les nuances et `BrushTokens()` produit un dictionnaire clé → pinceau gelé que chaque fenêtre recopie dans ses
`Resources` (`MainWindow`, `ReglagesWindow`, `HistoriqueWindow` et ses trois vues). Les XAML qui veulent suivre le thème
utilisent `{DynamicResource Clé}`. Le plan est donc mécanique : (1) ajouter `Categorie` et le calcul du gris épuisé dans
`From()`, (2) étendre le catalogue à 15 lignes, (3) ajouter `TickReset` et quelques pinceaux dérivés à `BrushTokens()` (plus
leurs replis statiques dans `DesignTokens.xaml`), (4) remplacer les couleurs littérales des 4 vues `Views/Cadrans/*.xaml` et
des 4 contrôles `Controls/Cadrans/*.cs` par des `DynamicResource`, (5) regrouper la grille de thèmes des réglages.

Le calcul du gris épuisé a été reproduit à l'identique (même `Lerp` arrondi en `Math.Round` pair, luminance relative sRGB) :
**les 15 thèmes atteignent ≥ 3:1** avec un t entre 0,32 et 0,55, et les 15 rouges de fin de rampe sont dans la bande
335°–20° avec un contraste ≥ 3,05. Lave est le cas limite (3,0027:1) : le test doit comparer `>= 3.0` sans arrondir avant.
Deux effets de bord à connaître : le nouveau gris épuisé devient **presque identique au gris `Neutre`** (utilization
inconnue) sur la plupart des thèmes (contraste 1,01 à 1,8 entre eux), et l'ancien texte de plaque Volets `#161019` tomberait
à 2,97:1 sur la plaque épuisée de Néon, Synthwave et Lave. Il faut donc un texte de plaque dérivé du disque.

Aucun test ne monte aujourd'hui les 4 cadrans alternatifs ni leurs contrôles : THM-04 exige une nouvelle garde statique
(motif déjà en place dans `GardeTokensHistoriqueTests`) et un test de liaison par thème.

**Primary recommendation:** Calcul `EpuiseLisible()` dans `From()` avec une boucle entière `pas = 18..100` (`t = pas / 100.0`) et
un helper pur `ContrasteWcag`. Ajouter à `BrushTokens()` : `TickReset` (= texte principal), `Epuise`, `CadranTuile`
(= Lerp(disque, piste, 0,5)), `CadranAttente`, `PlaqueTexte` (= disque × 0,5), `PlaqueFilet`, `PlaqueHachure`. Donner aux
contrôles des DP de pinceaux **sans défaut coloré**, liées en `DynamicResource` depuis les vues, et ne **jamais** fusionner
`DesignTokens.xaml` dans les vues de cadran.

## Standard Stack

Rien à installer. Phase 100 % code existant.

| Élément | Version | Rôle | Remarque |
|---|---|---|---|
| WPF `DynamicResource` + `ResourceDictionary` de fenêtre | net8.0-windows | Propagation du thème | Motif déjà utilisé : `MainWindow.ApplyThemeBrushes` |
| CommunityToolkit.Mvvm | 8.4.2 (déjà référencé) | `ThemeChoice`, groupes du VM | `[ObservableProperty]` |
| xUnit 2.9.2 + Xunit.StaFact 1.1.11 | déjà référencés | `[Fact]` purs, `[WpfFact]` STA | Collection `"XAML WPF"` pour tout test qui charge du BAML |

**Alternatives considérées :**
| Au lieu de | Possible | Pourquoi non |
|---|---|---|
| Groupes construits dans le VM | `CollectionViewSource` + `GroupStyle` | Avec le groupement, `ItemContainerGenerator.ContainerFromIndex(i)` rend des `GroupItem` et non les boutons : le test de vignettes actuel casse de façon peu lisible. L'ordre des groupes dépendrait aussi de l'ordre d'apparition ou d'un `SortDescription` en XAML. Des ItemsControl imbriqués sur une collection du VM sont plus simples à tester et à lire. |
| Hachure de plaque en token C# (`DrawingBrush` gelé) | `DrawingBrush` XAML avec `Pen Brush="{DynamicResource …}"` dans `UserControl.Resources` | Une `DynamicResource` dans un Freezable posé en ressource dépend du contexte d'héritage du dictionnaire. Le comportement est incertain (confiance LOW) et pas testé ici. Le token C# est sûr et testable sans UI. |

## Architecture Patterns

### Fichiers touchés
```
src/Chronos/
├── Theming/ChronosTheme.cs          # Categorie, EpuiseLisible, nouveaux tokens, catalogue 15
├── Theming/ContrasteWcag.cs         # NOUVEAU : luminance relative + ratio (pur, testable)
├── Theming/CategorieTheme.cs        # NOUVEAU (ou dans ChronosTheme.cs) : enum Pale/Classique/Vive
├── Resources/DesignTokens.xaml      # replis statiques des nouveaux tokens (valeurs de minuit)
├── ViewModels/ThemeChoice.cs        # (option) expose Categorie
├── ViewModels/GroupeThemes.cs       # NOUVEAU : Titre + IReadOnlyList<ThemeChoice>
├── ViewModels/MainViewModel.cs      # GroupesThemes (mêmes instances ThemeChoice que Themes)
├── Views/Reglages/ReglagesWindow.xaml  # section Thème (aujourd'hui l.738-798, décalée par la phase 38)
├── Views/Cadrans/*.xaml (4)         # littéraux → DynamicResource ; géométrie INCHANGÉE
├── Controls/Cadrans/*.cs (4)        # défauts colorés → null ; WaitFill → DP
├── Views/CadranGalleryWindow.xaml.cs   # applique BrushTokens au changement de thème (sinon la galerie ne suit plus)
└── Converters/UtilizationToBrushConverter.cs  # gris figé → ThemeCatalog.Default.Epuise
tests/Chronos.Tests/
├── ThemingTests.cs                  # invariants + 15 + palettes exactes + contraste + teinte
├── GardeCouleursCadransTests.cs     # NOUVEAU : aucune couleur littérale (4 xaml + 4 cs)
├── CadransThemeBindingTests.cs      # NOUVEAU : chaque vue suit chaque thème
├── ReglagesWindowTests.cs, SessionStylesBindingTests.cs, UtilizationToBrushConverterTests.cs (+ VueJourBindingTests si Historique thémé)
```

### Pattern 1 : gris épuisé calculé par contraste (THM-03)

```csharp
// Theming/ContrasteWcag.cs — WCAG 2.x, luminance relative sRGB.
public static class ContrasteWcag
{
    public static double Luminance(Color c) =>
        0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);

    public static double Ratio(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Lin(byte v)
    {
        double s = v / 255.0;
        return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}

// Dans ChronosTheme : t ENTIER pour éviter la dérive de 0.18 + 0.01 + 0.01…
private static Color EpuiseLisible(Color disque, Color piste, Color graduation)
{
    for (int pas = 18; pas <= 100; pas++)
    {
        var c = Lerp(piste, graduation, pas / 100.0);
        if (ContrasteWcag.Ratio(c, disque) >= 3.0) return c;
    }
    return graduation;   // jamais atteint sur le catalogue ; le test rougit si un thème futur n'y arrive pas
}
// From() : Epuise = EpuiseLisible(d, tr, tk)   — d = Hex(disc), OPAQUE (pas FondCadran, alpha E6)
```

- Le seuil 0,04045 et le 0,03928 de WCAG 2.0 donnent les mêmes résultats sur 8 bits (aucun octet entre les deux).
- Contraste mesuré contre le **disque opaque** `Hex(disc)`, comme le dit le plan. Ce n'est pas `FondCadran`, qui est translucide à E6.
- `Lerp` existant : `Math.Round` (arrondi pair), alpha forcé à FF. Le tableau ci-dessous reproduit exactement ce calcul.

### Tableau des 15 thèmes (calculé, arrondi C# reproduit)

| Thème | Cat. | Disque | Épuisé actuel (t=0,18) | Contraste actuel | t retenu | **Épuisé nouveau** | **Contraste / disque** | Contraste / piste | Rouge | Teinte | Rouge / disque |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Nord | Pâle | #2E3440 | #575E6D | 1,92 | 0,38 | **#777D8B** | **3,028** | 2,44 | #BF616A | 354° | 3,05 |
| Forêt | Pâle | #2D353B | #4D5859 | 1,70 | 0,55 | **#758079** | **3,041** | 2,40 | #E67E80 | 359° | 4,55 |
| Moka | Pâle | #1E1E2E | #4A4C60 | 1,95 | 0,38 | **#65697F** | **3,031** | 2,32 | #F38BA8 | 343° | 7,08 |
| Roseraie | Pâle | #191724 | #3F3C54 | 1,67 | 0,45 | **#65627C** | **3,020** | 2,60 | #EB6F92 | 343° | 6,07 |
| Sauge | Pâle | #262B28 | #4A524D | 1,79 | 0,47 | **#6D7670** | **3,068** | 2,45 | #D08A7E | 9° | 5,22 |
| Lavande | Pâle | #22202C | #4A4759 | 1,78 | 0,42 | **#6E697F** | **3,044** | 2,54 | #E08E9E | 348° | 6,51 |
| Minuit | Classique | #16151B | #47464F | 1,95 | 0,36 | **#63626C** | **3,019** | 2,39 | #D8503A | 8° | 4,43 |
| Ardoise | Classique | #1B2027 | #49505A | 2,01 | 0,35 | **#646B75** | **3,042** | 2,37 | #E06B5A | 8° | 5,01 |
| Ambre chaud | Classique | #1E1712 | #544738 | 1,97 | 0,34 | **#716451** | **3,071** | 2,52 | #D24A3A | 6° | 4,03 |
| Graphite | Classique | #121314 | #424548 | 1,93 | 0,37 | **#606367** | **3,081** | 2,45 | #E04B3C | 5° | 4,64 |
| Marine | Classique | #0F1A2A | #3A4960 | 1,92 | 0,37 | **#57677D** | **3,031** | 2,41 | #E25C4F | 5° | 4,89 |
| Néon | Vive | #0E0A1F | #34435D | 1,95 | 0,33 | **#41647B** | **3,081** | 2,58 | #FF2E63 | 345° | 5,38 |
| Aurore | Vive | #0E1726 | #3A4C66 | 2,06 | 0,32 | **#516680** | **3,047** | 2,40 | #F2577A | 346° | 5,49 |
| Synthwave | Vive | #140B24 | #3E3C66 | 1,85 | 0,35 | **#515F86** | **3,020** | 2,55 | #FF2D55 | 349° | 5,22 |
| Lave | Vive | #1A0E0A | #58392A | 1,83 | 0,36 | **#7C5844** | **3,003** | 2,59 | #FF3B1F | 8° | 5,31 |

Le pas précédent échoue partout (par ex. Lave t=0,35 → #7A5743 à 2,94 ; Forêt t=0,54 → 2,999), ce qui confirme que le t
retenu est bien le plus petit. Teinte = teinte HSV standard. `Alerte` (ambre) ≠ rouge sur les 15 thèmes.
La maquette HTML calcule sans arrondir et avec un `t += .01` qui dérive : elle peut afficher une valeur décalée d'un pas.
**Le C# fait foi.**

### Catalogue cible (ordre recommandé)

Garder `Default => All[0]` = minuit et ranger le catalogue **par catégorie, dans l'ordre du plan à l'intérieur de chaque
groupe**. Le VM groupe ensuite dans l'ordre de l'enum (Pâle, Classique, Vive), ce qui ne dépend pas de l'ordre du catalogue
entre catégories.

```csharp
// Classique (minuit en tête : Default => All[0])
From("minuit",   "Minuit",      Classique, "#16151B","#2A2932","#C9C8D2","#F4F2EC","#7BB13C","#EFA23A","#D8503A"),
From("ardoise",  "Ardoise",     Classique, "#1B2027","#2C333D","#CBD3DE","#EEF2F6","#5FB39A","#E0A94E","#E06B5A"),
From("ambre",    "Ambre chaud", Classique, "#1E1712","#33271C","#EAD9B8","#F6ECD9","#E4B24A","#E07E3C","#D24A3A"),
From("graphite", "Graphite",    Classique, "#121314","#26282B","#C4C7CC","#F2F3F5","#6DBE45","#F0A830","#E04B3C"),
From("marine",   "Marine",      Classique, "#0F1A2A","#1E2D44","#B9C9DE","#EAF0F7","#5DBB7A","#F2B544","#E25C4F"),
// Pâle
From("nord",     "Nord",        Pale, "#2E3440","#3B4252","#D8DEE9","#ECEFF4","#A3BE8C","#EBCB8B","#BF616A"),
From("foret",    "Forêt",       Pale, "#2D353B","#3A454A","#A6B0A0","#D3C6AA","#A7C080","#DBBC7F","#E67E80"),
From("moka",     "Moka",        Pale, "#1E1E2E","#313244","#BAC2DE","#CDD6F4","#A6E3A1","#FAB387","#F38BA8"),
From("roseraie", "Roseraie",    Pale, "#191724","#26233A","#B3AECC","#E0DEF4","#9CCFD8","#F6C177","#EB6F92"),
From("sauge",    "Sauge",       Pale, "#262B28","#343B37","#AEB8B0","#DCE3DD","#8FB996","#D9C27E","#D08A7E"),
From("lavande",  "Lavande",     Pale, "#22202C","#302D3D","#C3BCD9","#E6E2F2","#9FCFB0","#E8C88E","#E08E9E"),
// Vive
From("neon",     "Néon",        Vive, "#0E0A1F","#241B3A","#7DF9FF","#E6E1FF","#38E8C6","#FFC23D","#FF2E63"),
From("aurore",   "Aurore",      Vive, "#0E1726","#1D2B44","#BFE3FF","#EAF2FF","#4FD1C5","#F0C36D","#F2577A"),
From("synthwave","Synthwave",   Vive, "#140B24","#2A1745","#9AE6FF","#F5EEFF","#2BFF88","#FFD000","#FF2D55"),
From("lave",     "Lave",        Vive, "#1A0E0A","#33190F","#FFC9A3","#FFF1E6","#7CFF4F","#FFB000","#FF3B1F"),
```
Les clés existantes ne changent pas, donc un thème déjà choisi reste choisi (`ByKey` est tolérant). Nouvelles clés :
`sauge`, `lavande`, `graphite`, `marine`, `synthwave`, `lave`.

### Pattern 2 : nouveaux tokens du thème et inventaire exhaustif des couleurs fixes (THM-04)

**Tokens à ajouter à `BrushTokens()`** (et en repli statique dans `DesignTokens.xaml`, valeur calculée pour minuit) :

| Clé | Dérivation dans `From()` | Valeur pour minuit | Consommateurs |
|---|---|---|---|
| `TickReset` | `nk` (texte principal) | `#F4F2EC`, **identique au repli statique actuel** | `MainWindow.xaml` l.104 et l.142 ; Historique : `VueJourView` (TraitReset), `VueSemaineView` (TraitReset ×n), via la recopie de `HistoriqueWindow` ; plus tard la flèche Braises (phase 41) |
| `Epuise` | `EpuiseLisible(d, tr, tk)` | `#63626C` | Historique (option, voir Pattern 4) ; utile aussi en test |
| `CadranTuile` (nom à la discrétion) | `Lerp(d, tr, 0.5)` | `#201F26` (avant : `#211D2A`) | Volets tuile 5H/7J ; sillon du FuseBar (avant : `#1D1926`) |
| `CadranAttente` | `Color.FromArgb(0x6E, tk.R, tk.G, tk.B)` | `#6EC9C8D2` (avant : `#6EB0AEBA`) | WaitFill des 4 contrôles (état « en attente ») |
| `PlaqueTexte` | `Scale(d, 0.5)` opaque | `#0B0A0D` (avant : `#161019`) | chiffres sur la plaque Volets |
| `PlaqueFilet` | `WithAlpha(Scale(d,0.5), 0x33)` | `#330B0A0D` (avant : `#33000000`) | filet horizontal de la plaque Volets |
| `PlaqueHachure` | `DrawingBrush` gelé construit en C# (tuile 4×4, ligne (0,4)→(4,0), plume 1, pinceau `WithAlpha(Scale(d,0.5),0x55)`) | — | remplace `GrainHatch` (plancher Volets) |

Pourquoi `PlaqueTexte = disque × 0,5` : contraste minimal mesuré du texte sur la plaque (vert, ambre, rouge, épuisé) de
**3,18** (Lave) à **4,27** (Forêt). Avec le disque brut, on obtient 3,00 à 3,08. Avec l'ancien `#161019`, Néon, Synthwave et
Lave tombent à 2,97 sur la plaque épuisée.

`TickReset = texte principal` reprend la sémantique documentée dans `DesignTokens.xaml` (« même neutre que le texte
principal »). Pour minuit, la valeur ne change pas : `VueJourBindingTests:133` et `VueSemaineBindingTests:346` (qui attendent
`#F4F2EC` avec le thème par défaut ou le repli) restent verts.

**Inventaire EXHAUSTIF des couleurs littérales des cadrans alternatifs → pinceau du thème** :

| Fichier | Ligne(s) | Littéral | Rôle | Remplacer par |
|---|---|---|---|---|
| `Views/Cadrans/CadranBraisesView.xaml` | 27, 35 | `#F4F2EC` | % 5 h / compte à rebours 5 h | `{DynamicResource TextePrincipal}` |
| idem | 29, 37 | `#C7C6D0` | % / compte à rebours hebdo | `{DynamicResource TexteSecondaireClair}` (son repli statique vaut exactement `#C7C6D0`) |
| idem (EmberRingControl ×2) | 13, 18 | (défauts C#) | cendre, attente | ajouter `AshBrush="{DynamicResource TickMajeur}"` `WaitBrush="{DynamicResource CadranAttente}"` |
| `Views/Cadrans/CadranFusibleView.xaml` | 15, 30 | `#A9A8B2` | libellés « 5 H » « 7 J » | `{DynamicResource TexteSecondaire}` |
| idem | 17, 19, 32, 34 | `#F4F2EC` | valeurs | `{DynamicResource TextePrincipal}` |
| idem (FuseBar ×2) | 23, 38 | (défauts C#) | sillon, encoche, attente | `TrackBrush="{DynamicResource CadranTuile}"` `NotchBrush="{DynamicResource TextePrincipal}"` `WaitBrush="{DynamicResource CadranAttente}"` |
| `Views/Cadrans/CadranMareeView.xaml` | 19, 34 | `#A9A8B2` | libellés | `TexteSecondaire` |
| idem | 22, 24, 37, 39 | `#F4F2EC` | valeurs | `TextePrincipal` |
| idem (TideColumn ×2) | 14, 29 | (défauts C#) | canal, ligne d'eau, attente | `TrackBrush="{DynamicResource FondCadran}"` (le plus proche de `#141019`) `WaterlineBrush="{DynamicResource TextePrincipal}"` `WaitBrush="{DynamicResource CadranAttente}"` |
| `Views/Cadrans/CadranVoletsView.xaml` | 17 | `#55000000` | plume de `GrainHatch` | supprimer la ressource locale ; `Fill="{DynamicResource PlaqueHachure}"` (l.39, l.71) |
| idem | 32, 64 | `#211D2A` | fond de tuile | `{DynamicResource CadranTuile}` |
| idem | 33, 65 | `#A9A8B2` | « 5H » « 7J » | `TexteSecondaire` |
| idem | 41, 73 | `#33000000` | filet de plaque | `{DynamicResource PlaqueFilet}` |
| idem | 42, 46, 74, 78 | `#161019` | chiffres sur plaque | `{DynamicResource PlaqueTexte}` |
| idem (FlapRow ×2) | 52, 84 | (défauts C#) | volet allumé/éteint, attente | `OnBrush="{DynamicResource TextePrincipal}"` `OffBrush="{DynamicResource Piste5h}"` (avant : `#2A2634`, minuit `#2A2932`) `WaitBrush="{DynamicResource CadranAttente}"` |
| `Controls/Cadrans/EmberRingControl.cs` | 30, 33, 42, 73, 74 | `Brushes.Gray`, `Frozen(0x46,0x44,0x4F)`, `FrozenA(0x6E,0xB0,0xAE,0xBA)`, `?? Brushes.Gray`, `?? Brushes.DimGray` | défauts / repli | défauts DP à `null` ; WaitFill → DP `WaitBrush` ; replis `??` supprimés (un pinceau null ne dessine rien) |
| `Controls/Cadrans/FuseBar.cs` | 24, 27, 30, 38, 67 | `Brushes.Gray`, `Frozen(0x1D,0x19,0x26)`, `Frozen(0xF4,0xF2,0xEC)`, `FrozenA(0x6E,…)`, `?? Brushes.Gray` | idem | idem |
| `Controls/Cadrans/TideColumn.cs` | 20, 23, 26, 34, 63 | `Brushes.Gray`, `Frozen(0x14,0x10,0x19)`, `Frozen(0xF4,0xF2,0xEC)`, `FrozenA(0x5A,…)`, `?? Brushes.Gray` | idem | idem (même `CadranAttente` ; l'écart d'alpha 0x5A / 0x6E disparaît, ce qui est acceptable) |
| `Controls/Cadrans/FlapRow.cs` | 23, 26, 31 | `Frozen(0xF4,0xF2,0xEC)`, `Frozen(0x2A,0x26,0x34)`, `FrozenA(0x6E,…)` | idem | idem |
| Les 4 contrôles | helpers `Frozen`/`FrozenA` | — | — | **supprimer** les helpers (sinon la garde rougit) ; garder `WithAlpha` (couleur dérivée, pas littérale) |

`QuotaBrush` vient toujours de `{Binding …ValueBrush}` (déjà thémé par `theme.ArcBrush`). Le gris épuisé y arrive donc
automatiquement dans les 5 styles. `Background="Transparent"` à la racine des 4 UserControl est permis : c'est l'absence de
couleur, et il sert au hit-test.

**Ne pas fusionner `DesignTokens.xaml` dans les vues de cadran.** Elles n'ont aujourd'hui que `BoolToVis` en ressources, et
leurs `DynamicResource` remontent donc jusqu'à `MainWindow.Resources`, où `ApplyThemeBrushes` écrit le thème. Un dictionnaire
fusionné local masquerait ces entrées : c'est le piège 34-08 de l'Historique, contourné là-bas en recopiant les tokens dans
chaque vue.

### Pattern 3 : regroupement de la section Thème (THM-01)

Recommandation (discrétion) : **groupes construits dans le VM, deux ItemsControl imbriqués**, sans CollectionViewSource.

```csharp
// Theming
public enum CategorieTheme { Pale, Classique, Vive }   // l'ordre de l'enum = l'ordre d'affichage
// ChronosTheme : public CategorieTheme Categorie { get; init; } ; From(key, name, categorie, disc, …)

// ViewModels/GroupeThemes.cs
public sealed record GroupeThemes(string Titre, IReadOnlyList<ThemeChoice> Themes);

// MainViewModel (là où Themes est peuplé, l.440-442) — MÊMES instances que Themes (la surbrillance reste juste)
foreach (var t in ThemeCatalog.All) Themes.Add(new ThemeChoice(t, t.Key == active.Key));
foreach (var cat in Enum.GetValues<CategorieTheme>())
    GroupesThemes.Add(new GroupeThemes(TitreCategorie(cat), Themes.Where(c => c.Theme.Categorie == cat).ToList()));
```

```xml
<!-- ReglagesWindow.xaml, section Thème : on garde « THÈME » (Etiquette), puis un groupe titré par catégorie -->
<ItemsControl x:Name="GrilleThemes" ItemsSource="{Binding GroupesThemes}" Focusable="False">
  <ItemsControl.ItemTemplate>
    <DataTemplate>
      <StackPanel>
        <TextBlock Text="{Binding Titre}" Style="{StaticResource Etiquette}" Margin="0,4,0,6"/>
        <ItemsControl ItemsSource="{Binding Themes}" Focusable="False">
          <ItemsControl.ItemsPanel><ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate></ItemsControl.ItemsPanel>
          <ItemsControl.ItemTemplate><!-- DataTemplate de vignette ACTUEL, inchangé --></ItemsControl.ItemTemplate>
        </ItemsControl>
      </StackPanel>
    </DataTemplate>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```
- Le style `Etiquette` existant vaut **exactement** 10,5 (`ReglagesCorpsEtiquette`), semi-gras, `Ink2` : c'est celui que demande le plan.
- **Piège de liaison** : la commande de vignette utilise aujourd'hui
  `RelativeSource={RelativeSource AncestorType=ItemsControl}` → `DataContext.SelectThemeCommand`. Dans l'ItemsControl
  interne, l'ancêtre `ItemsControl` le plus proche a pour DataContext un `GroupeThemes`, et la commande devient silencieusement
  nulle. Il faut viser la fenêtre (`AncestorType=Window`) ou l'ItemsControl externe (`AncestorLevel=2`) et le couvrir par un test.
- Titres : la maquette affiche « PÂLE / CLASSIQUE / VIVE » en capitales, comme les étiquettes existantes (« THÈME », « STYLE
  DU CADRAN »). Recommandation : capitales pour les titres de groupe (source unique dans le VM ou un `Textes*`), et l'étiquette
  « THÈME » conservée. Si cela fait deux niveaux de titres jugés redondants en revue visuelle, retirer « THÈME ».
- La largeur 640 donne 3 vignettes par ligne et la largeur 860 en donne 6. Le groupe Pâle compte 6 thèmes : le test
  `Les_vignettes_de_theme_passent_a_la_ligne_entieres` garde donc ses attentes 3 et 6 sur la première ligne. Seul `Assert.Equal(9, …)` passe à 15,
  et la collecte des vignettes doit parcourir l'arbre (boutons de style `VignetteTheme` sous `GrilleThemes`) au lieu de
  `ContainerFromIndex`.

### Pattern 4 : Historique et convertisseur (décision « si faisable »)

- **`HistoGris` a trois rôles** dans `PisteNiveau` : la bande épuisée (l.216/224), le fantôme S-1 (l.120) et les blocs de saut
  (l.152). Dans `VueQuatreSemainesView`, il sert aussi aux fantômes S-1..S-3 et au trait repère (l.193). Il porte aussi la plume de
  `HistoHachure` (StaticResource dans `DesignTokens.xaml`) et `BordTrouArrete`.
- Voie recommandée, faible risque : ne pas changer `HistoGris` (gardé `#5A5960` pour la hachure, les bordures et le repère :
  `GardeTokensHistoriqueTests` et `HonneteteHistoriqueTests:222` restent verts) et lier **seulement** la propriété `Gris` des
  pistes `PisteNiveau` et `PisteQuatreSemaines` à `{DynamicResource Epuise}`. `HistoriqueWindow` recopie déjà tout
  `BrushTokens()` dans la fenêtre et ses trois vues, et le repli statique `Epuise` de `DesignTokens.xaml` sert aux tests qui
  montent une vue seule. Conséquence voulue (D-34-01 : « HistoGris = gris épuisé du cadran ») : le fantôme et les sauts
  prennent aussi le gris du thème. Test à adapter : `VueJourBindingTests:132` (`niveau.Gris` attendu `#5A5960` → valeur
  `Epuise` du repli ou du thème).
- La phase 38 réécrit `VueSemaineView.xaml` juste avant (suppression de Simplifié et Tuiles, `PisteFenetres5h` supprimée) : les
  numéros de ligne cités ici seront décalés. Faire cette tâche **en dernier**, isolée. Si un test d'Historique résiste, la
  laisser de côté et le noter dans le SUMMARY, comme le prévoit la décision.
- `UtilizationToBrushConverter` (`UtilBrush`) est **déclaré dans `DesignTokens.xaml` mais utilisé par aucun XAML** (code mort,
  vérifié par grep). Il ne peut pas suivre le thème actif sans injection. Action minimale cohérente : `Epuise = ThemeCatalog.Default.Epuise`
  (et, si on veut aller au bout, `ThemeCatalog.Default.ArcColor(u)`). Mettre à jour `UtilizationToBrushConverterTests` (l.17 :
  `#5A5960` → `ThemeCatalog.Default.Epuise`). Sa suppression relèverait d'une purge, hors périmètre.

### Anti-Patterns à éviter
- **Modifier la géométrie des cadrans** (tailles, rayons, `Width="210"`, `Count`) : la phase 40 redessine ces fichiers. On ne
  touche qu'aux attributs de couleur et aux DP de pinceaux.
- **`t += 0.01` en double** : une dérive en virgule flottante peut sauter ou doubler un pas. Utiliser un compteur entier.
- **Contraste contre `FondCadran`** (alpha E6) : la luminance d'une couleur translucide n'est pas définie. Utiliser le disque opaque.
- **Arrondir le contraste avant de comparer** (`Math.Round(ratio, 2) >= 3`) : Lave vaut 3,0027 et Forêt t=0,54 vaut 2,9986,
  qui passerait à tort après arrondi.
- **`?? Brushes.Gray` comme repli** dans `OnRender` : c'est une couleur fixe qui masque un oubli de liaison. Un pinceau `null` ne dessine rien,
  et le test de liaison détecte l'oubli.

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser | Pourquoi |
|---|---|---|---|
| Propagation du thème aux cadrans | une liaison au VM par couleur, un convertisseur de thème | `DynamicResource` + `ApplyThemeBrushes` existant | Déjà en place pour Arcs, réglages et Historique ; zéro code nouveau côté fenêtre principale |
| Garde « aucune couleur littérale » | un nouvel analyseur | copier le motif de `GardeTokensHistoriqueTests.Aucune_couleur_ni_taille_en_dur_dans_les_vues_et_les_pistes_de_l_historique` (lecture du source via `GardesPerimetreTests.CheminSources()`) | Infrastructure éprouvée, garde anti-mutisme incluse |
| Interpolation de couleur | un nouveau `Lerp` | `ChronosTheme.Lerp` / `Scale` / `WithAlpha` (privés, dans le même fichier) | Même arrondi que le reste du thème |

## Common Pitfalls

### Pitfall 1 : l'épuisé se confond désormais avec le neutre
**Ce qui se passe :** `Neutre = Scale(tk, 0.5)` (utilization inconnue) et le nouvel `Epuise` sont presque identiques. Contraste
entre eux : Minuit 1,02, Graphite 1,01, Ardoise 1,03, Marine 1,04 ; au mieux 1,78 (Forêt). Avant, l'épuisé était nettement
plus sombre.
**Atténuation existante :** le texte central diffère (« 100 % » contre « — » ou « indisponible »).
**À faire :** ne pas modifier `Neutre` (hors décision verrouillée). Le signaler dans le SUMMARY et comme critère de revue
visuelle (§9.5 du plan). Point positif : la cendre des braises (`TickMajeur`) et l'épuisé, auparavant quasi identiques
(`#46444F` contre `#47464F`), se distinguent maintenant (contraste 1,4 à 1,7).

### Pitfall 2 : la galerie `--cadrans` ne suit plus aucun thème
**Ce qui se passe :** `CadranGalleryWindow` ne recopie pas `BrushTokens()`. Une fois les vues en `DynamicResource`, elles
résolvent vers le repli statique de `App.xaml` (minuit), quel que soit le thème choisi dans sa ComboBox.
**À faire :** dans `CadranGalleryWindow.xaml.cs`, appliquer `SelectedTheme.BrushTokens()` à `Resources` au démarrage et sur
`PropertyChanged(SelectedTheme)`, comme le fait `MainWindow.ApplyThemeBrushes`. La phase 40 réutilisera la galerie pour ses
huit variantes.

### Pitfall 3 : commande de vignette perdue après le regroupement
Voir Pattern 3 : `AncestorType=ItemsControl` change de cible dans un ItemsControl imbriqué. Un clic sur la vignette ne fait
alors plus rien, sans erreur visible (seulement une trace de liaison). Couvrir par un test qui exécute la commande d'une
vignette de chaque groupe.

### Pitfall 4 : la garde attrape les helpers `WithAlpha`
`WithAlpha` contient `Color.FromArgb((byte)…, c.R, c.G, c.B)`. Une regex naïve `Color\.FromArgb\(` le signalerait. Utiliser
`Color\.From(A)?[Rr]gb\(\s*(0x|\d)` (premier argument littéral) et supprimer les helpers `Frozen`/`FrozenA`.

### Pitfall 5 : matrices de tests ×1,67
`SessionStylesBindingTests` monte 8 styles × N thèmes, et passer de 9 à 15 thèmes fait passer de 72 à 120 montages WPF. Il faut
remplacer les littéraux (`9`, `72`, `4 * 9`) par `ThemeCatalog.All.Count` pour que la garde anti-mutisme reste juste. Le temps
d'exécution augmente : à surveiller, mais pas bloquant.

### Pitfall 6 : constructeur de `MainViewModel` modifié par la phase 37
`ThemingTests.ReglagesWindow_se_construit…` construit le VM avec `FakeStatusLineSetup` et d'autres doublures. La purge R5 (phase 37)
peut retirer des dépendances du constructeur : il faut adapter le test à la signature réelle au moment de l'exécution.

### Pitfall 7 : contraste Nord limite
Le rouge de Nord (`#BF616A`) est à 3,05:1 et Lave épuisé à 3,003:1. Ils passent, mais toute retouche future de ces palettes
fera rougir le test. C'est voulu (« un thème ajouté plus tard qui viole la règle fait rougir le test »).

## Code Examples

### Garde statique (THM-04)
```csharp
// tests/Chronos.Tests/GardeCouleursCadransTests.cs — motif GardeTokensHistoriqueTests
private static readonly Regex InterditXaml = new(
    @"#[0-9A-Fa-f]{3,8}\b|\b(Foreground|Background|Fill|Stroke|BorderBrush|Brush|Color)=""(?!\{|Transparent"")",
    RegexOptions.Compiled);
private static readonly Regex InterditCs = new(
    @"Frozen\(0x|FrozenA\(0x|Color\.From(A)?[Rr]gb\(\s*(0x|\d)|Colors\.[A-Z]|Brushes\.(?!Transparent\b)[A-Z]",
    RegexOptions.Compiled);
// Anti-mutisme : Views/Cadrans → exactement 4 *.xaml ; Controls/Cadrans → exactement 4 *.cs.
// Attention : les commentaires XAML contenant un « # » (aucun aujourd'hui) seraient signalés. C'est acceptable : la garde Historique fait pareil.
```

### Liaison par thème (THM-04, STA)
```csharp
[WpfFact] // [Collection("XAML WPF")]
public void Les_quatre_cadrans_suivent_chaque_theme()
{
    foreach (var theme in ThemeCatalog.All)
    foreach (var vue in new FrameworkElement[] { new CadranBraisesView(), new CadranFusibleView(), new CadranMareeView(), new CadranVoletsView() })
    {
        var hote = new Border { Child = vue, DataContext = new CadranPreviewViewModel { SelectedTheme = theme } };
        foreach (var kv in theme.BrushTokens()) hote.Resources[kv.Key] = kv.Value;
        hote.Measure(new Size(400, 400)); hote.Arrange(new Rect(0, 0, 400, 400));
        // Chaque TextBlock visible : Foreground ∈ {TextePrincipal, TexteSecondaire, TexteSecondaireClair, PlaqueTexte} du thème.
        // Chaque contrôle : AshBrush/TrackBrush/NotchBrush/WaterlineBrush/OnBrush/OffBrush/WaitBrush == pinceau du token attendu.
    }
}
```

### Invariants du catalogue (THM-02/03)
```csharp
[Fact] public void Catalogue_quinze_themes_trois_categories()   // remplace Catalogue_a_six_themes_aux_cles_uniques
{
    Assert.Equal(15, ThemeCatalog.All.Count);
    Assert.Equal(15, ThemeCatalog.All.Select(t => t.Key).Distinct().Count());
    Assert.Equal("minuit", ThemeCatalog.Default.Key);
    Assert.Equal(6, ThemeCatalog.All.Count(t => t.Categorie == CategorieTheme.Pale));
    Assert.Equal(5, ThemeCatalog.All.Count(t => t.Categorie == CategorieTheme.Classique));
    Assert.Equal(4, ThemeCatalog.All.Count(t => t.Categorie == CategorieTheme.Vive));
}
// Palette exacte : disque = Color.FromRgb(FondCadran.R,G,B) ; piste = Piste5h ; graduation = TickVisible ; texte = TextePrincipal ;
// RampGreen / RampAmber / RampRed → table de 15 lignes copiée du plan §5.2 et du catalogue actuel.
// Contraste : foreach thème → Ratio(Epuise, disque) >= 3.0 ; Ratio(RampRed, disque) >= 3.0 ; teinte HSV(RampRed) ∈ [335,360) ∪ [0,20].
// Minimalité : si t > 0,18, alors Lerp(piste, graduation, t − 0,01) < 3:1 (protège la règle « plus petit mélange »).
// Ancrage : ThemeCatalog.Default.Epuise == #63626C (valeur calculée, ci-dessus).
// Helper : Ratio(blanc, noir) == 21 ; Ratio(#777777, blanc) ≈ 4,48 (référence WCAG connue).
```

## State of the Art

| Avant | Après | Impact |
|---|---|---|
| `Epuise = Lerp(piste, graduation, 0.18)` : contraste de 1,67 à 2,06 | plus petit t (pas de 0,01) donnant ≥ 3:1 | épuisé lisible ; plus clair partout |
| `TickReset #F4F2EC` statique | `TickReset = texte principal` du thème | même valeur pour minuit |
| 4 cadrans alternatifs codés en dur (minuit) | `DynamicResource` sur les tokens du thème | les 15 thèmes s'appliquent aux 5 styles |
| Grille plate de 9 vignettes | 3 groupes, 15 vignettes | — |

## Open Questions

1. **Épuisé ≈ Neutre** (Pitfall 1). Ce que l'on sait : la confusion est mesurée, et le texte central lève l'ambiguïté. Ce qui reste
   ouvert : faut-il assombrir `Neutre` ou lui donner une teinte ? C'est hors décision verrouillée. Recommandation : ne rien changer,
   le noter au SUMMARY, le juger en revue visuelle (critère 5 du §9).
2. **Titres de groupe en capitales ou non.** La maquette les met en capitales, alors que le plan écrit « Pâle · Classique · Vive ».
   Recommandation : capitales (cohérence avec les étiquettes existantes).
3. **Historique thémé** (Pattern 4) : la voie recommandée est sûre. Elle reste conditionnelle à l'état des fichiers après la
   phase 38.

## Environment Availability

| Dépendance | Requise par | Disponible | Version | Repli |
|---|---|---|---|---|
| .NET SDK | build + tests | ✓ | 10.0.201 (cible net8.0-windows) | — |
| Python | calculs de cette recherche uniquement | ✓ | 3.14.4 | non requis à l'exécution |

Aucune dépendance manquante.

## Validation Architecture

### Test Framework
| Propriété | Valeur |
|---|---|
| Framework | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`, collection `"XAML WPF"` sérialisée) |
| Config file | `tests/Chronos.Tests/Chronos.Tests.csproj` (attribut `CheminSourcesChronos` déjà injecté pour les gardes de source) |
| Quick run command | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~ThemingTests\|FullyQualifiedName~GardeCouleursCadrans\|FullyQualifiedName~CadransThemeBinding"` |
| Full suite command | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` |

### Phase Requirements → Test Map
| Req ID | Comportement | Type | Commande automatisée | Fichier existe ? |
|---|---|---|---|---|
| THM-01 | 15 thèmes, `Categorie` 6/5/4, clés uniques, défaut minuit, clé persistée relue | unit | `--filter FullyQualifiedName~ThemingTests` | ✅ à adapter (`Catalogue_a_six_themes…`, `ReglagesWindow_se_construit…` 9→15) |
| THM-01 | Section Thème : 3 titres dans l'ordre Pâle, Classique, Vive, style Etiquette ; 15 vignettes 88×76 ; retour à la ligne 3 / 6 ; la commande d'une vignette par groupe sélectionne le thème | WPF (STA) | `--filter FullyQualifiedName~ReglagesWindowTests` | ✅ à adapter (`Les_vignettes_de_theme_passent_a_la_ligne_entieres`) + ❌ nouveau test des titres et de la commande |
| THM-02 | Les 6 palettes et Néon/Aurore corrigés ont exactement les 7 couleurs | unit | `--filter FullyQualifiedName~ThemingTests` | ❌ Wave 0 |
| THM-03 | Épuisé ≥ 3:1, minimalité du t, rouge 335–20° et ≥ 3:1, Alerte ≠ rouge, helper WCAG de référence | unit | `--filter FullyQualifiedName~ThemingTests` | ❌ Wave 0 (Alerte : ✅ existant, renommer « neuf » → tous) |
| THM-04 | `TickReset` ∈ `BrushTokens()` et vaut le texte principal ; nouveaux tokens présents sur les 15 thèmes | unit | `--filter FullyQualifiedName~ThemingTests` | ❌ Wave 0 |
| THM-04 | Aucune couleur littérale dans `Views/Cadrans/*.xaml` (4) et `Controls/Cadrans/*.cs` (4) | garde source | `--filter FullyQualifiedName~GardeCouleursCadrans` | ❌ Wave 0 |
| THM-04 | Chaque vue de cadran prend les pinceaux de chaque thème (textes, tuiles, contrôles) | WPF (STA) | `--filter FullyQualifiedName~CadransThemeBinding` | ❌ Wave 0 |
| THM-04 | Historique : TraitReset suit le thème ; `Gris` = Epuise (si la voie est retenue) | WPF | `--filter "FullyQualifiedName~VueJourBindingTests\|FullyQualifiedName~VueSemaineBindingTests\|FullyQualifiedName~HistoriqueBindingTests"` | ✅ à adapter (`VueJourBindingTests:132`) |
| (régression) | Matrice sessions × thèmes | WPF | `--filter FullyQualifiedName~SessionStylesBindingTests` | ✅ à adapter (9/72/4*9 → `ThemeCatalog.All.Count`) |
| (régression) | Convertisseur mort aligné sur le défaut | unit | `--filter FullyQualifiedName~UtilizationToBrushConverterTests` | ✅ à adapter (l.17) |
| (manuel) | Revue visuelle : 15 thèmes × 5 styles, gris épuisé visible, Braises/Fusible/Marée/Volets thémés | manuel | galerie `Chronos.exe --cadrans` + réglages | — (critère §9.5) |

### Sampling Rate
- **Par commit de tâche :** quick run command ci-dessus.
- **Par vague :** suite complète.
- **Phase gate :** suite complète verte avant `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/GardeCouleursCadransTests.cs` : THM-04 (garde source, anti-mutisme 4 + 4)
- [ ] `tests/Chronos.Tests/CadransThemeBindingTests.cs` : THM-04 (liaison des 4 vues × 15 thèmes, collection `"XAML WPF"`)
- [ ] Nouveaux `[Fact]` dans `ThemingTests.cs` : THM-02 (table de palettes), THM-03 (contraste, teinte, minimalité, helper WCAG), THM-04 (tokens)
- [ ] Nouveau test dans `ReglagesWindowTests.cs` : titres de groupes et commande de vignette par groupe
- Aucune installation de framework nécessaire.

### Tests existants à adapter (inventaire)
| Fichier:ligne | Aujourd'hui | Après |
|---|---|---|
| `ThemingTests.cs:26-31` | `Assert.Equal(9, …)` | invariants + 15 + catégories |
| `ThemingTests.cs:51` | nom « neuf thèmes » | « tous les thèmes » (logique inchangée) |
| `ThemingTests.cs:123` | `Assert.Equal(9, vm.Themes.Count)` | 15 (+ `GroupesThemes.Count == 3`) |
| `ReglagesWindowTests.cs:230-236` | `ContainerFromIndex` + `Assert.Equal(9, …)` | collecte par arbre visuel sous `GrilleThemes` ; 15 |
| `SessionStylesBindingTests.cs:151, 181, 317` (+ commentaires 16, 40) | 9 / 72 / `4 * 9` | `ThemeCatalog.All.Count`, `8 * n`, `4 * n` |
| `UtilizationToBrushConverterTests.cs:17` | `Epuise #5A5960` | `ThemeCatalog.Default.Epuise` |
| `VueJourBindingTests.cs:132` | `niveau.Gris == #5A5960` | repli `Epuise` (si l'Historique est thémé) |
| `VueJourBindingTests.cs:133`, `VueSemaineBindingTests.cs:346` | `TraitReset == #F4F2EC` | **inchangé** (minuit = `#F4F2EC`) |
| `ReouvertureHistoriqueTests.cs:94-96` | Alerte aurore ≠ Alerte nord | reste vrai (`#F0C36D` ≠ `#EBCB8B`) |
| `GardeTokensHistoriqueTests` | `HistoGris #5A5960`, plume de hachure | **inchangé** (voie recommandée : `HistoGris` conservé) |

Commentaires et docs à rafraîchir : « 9 thèmes » dans `ChronosTheme.cs` (l.71, l.141), `MainWindow.xaml` (commentaire
M-INDISPO « 9 thèmes », commentaire l.138 « cyan TickReset », obsolète), `README.md:37` « thème (9) » → « thème (15, en 3
groupes) ».

## Sources

### Primary (HIGH confidence)
- Code du dépôt (lu le 2026-10-03) : `Theming/ChronosTheme.cs`, `Views/Cadrans/*.xaml`, `Controls/Cadrans/*.cs`,
  `Views/MainWindow.xaml(.cs)`, `Views/Reglages/ReglagesWindow.xaml(.cs)`, `Views/Historique/HistoriqueWindow.xaml.cs`,
  `Views/CadranGalleryWindow.xaml(.cs)`, `ViewModels/{MainViewModel,ThemeChoice,CadranPreviewViewModel}.cs`,
  `Resources/DesignTokens.xaml`, `Converters/UtilizationToBrushConverter.cs`, `Rendering/RampColor.cs`, tests cités.
- `.zeus/DESIGN_PLAN_CYCLE2.md` §5 et §9 ; `.zeus/maquettes/cycle2-cadrans-themes.html` (§5, algorithme de vignette) ;
  `.zeus/reports/cycle2/inventaire-themes-historique.md` §A.
- Calcul reproduit (script Python, arrondi pair identique à `Math.Round`, luminance relative WCAG 2.x) pour les 15 thèmes.

### Secondary (MEDIUM confidence)
- Définition WCAG 2.x de la luminance relative et du rapport de contraste (connaissance standard, stable depuis 2008).

### Tertiary (LOW confidence)
- Résolution d'une `DynamicResource` dans un Freezable déclaré en ressource de UserControl : non vérifiée. On la contourne par
  le token C# `PlaqueHachure`.

## Metadata

**Confidence breakdown:**
- Standard stack : HIGH, aucun ajout ; mécanismes déjà en service dans le dépôt.
- Architecture : HIGH, motifs copiés de `MainWindow` et `HistoriqueWindow` ; les pièges de liaison sont identifiés dans le code.
- Valeurs de gris et contrastes : HIGH, calcul déterministe reproduit à l'octet près.
- Pitfalls : HIGH pour 1 à 6 (mesurés ou lus dans le code) ; MEDIUM pour le coût en temps de la matrice de sessions.

**Research date:** 2026-10-03
**Valid until:** fin de la phase 40 (les fichiers `Views/Cadrans/*` et `Controls/Cadrans/*` seront redessinés ensuite)
