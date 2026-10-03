# Phase 42 : Un geste sur toute la silhouette — Research

**Researched:** 2026-10-03
**Domain:** WPF / fenêtre layered (`AllowsTransparency`) — hit-test OS par alpha, routage des événements souris, `DragMove`,
tests de rendu `RenderTargetBitmap` hors écran
**Confidence:** HIGH (comportements critiques vérifiés par prototype exécuté sur la machine et dans le source WPF ;
aucune bibliothèque nouvelle)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Remplacer le `CentreHit` (ellipse 66 × 66 partagée, `MainWindow.xaml:191-193`) par **un geste unique sur toute la silhouette**
  de chaque variante : clic sans déplacement → bascule % ↔ temps ; double-clic → Historique sans bascule (`ArbitreClicCentre`
  inchangé, `GetDoubleClickTime`) ; appui puis déplacement au-delà de `SystemParameters.MinimumHorizontalDragDistance` /
  `MinimumVerticalDragDistance` → `DragMove()` puis `SnapToNearestCorner()` ; clic droit → réglages ; hors silhouette → le clic
  traverse vers le bureau.
- Silhouettes : disque de rayon 74 (centre de l'empreinte 170) pour Arcs et Braises ; rectangle arrondi (rayon 10) englobant le
  cadran avec 6 px de marge pour Fusible, Marée, Volets dans les deux orientations. Peintes avec le token `ZoneSilhouette`
  = `#01000000` (fenêtre layered : un pixel alpha 0 laisse passer le clic). Chaque vue de cadran déclare sa silhouette ; un seul
  répartiteur de gestes dans `MainWindow`. Les pastilles d'Arcs gardent leurs propres clics (Handled).
- Tests : (1) rendu `RenderTargetBitmap` par cadran × orientation : alpha > 0 aux points témoins dans la silhouette, = 0 hors ;
  (2) `VisualTreeHelper.HitTest` de routage aux mêmes points ; (3) garde statique : aucun élément portant un geste n'a de
  pinceau nul ou `Transparent` ; (4) fonction pure de décision clic / glisser (seuil) testée. `GardeGestesCadranTests` (chaînes
  exactes) est réécrit pour le nouveau répartiteur. NE PAS se fier à `HitTest` seul (Transparent y est « touché »).
- Corriger les commentaires faux « Transparent suffit » (`MainWindow.xaml:249, 269-270, 295-296`).
- Carte Historique des réglages : « Aussi : double-clic sur le cadran » (au lieu de « au centre du cadran ») ; README / docs
  des gestes mis à jour.

### Claude's Discretion
Forme du répartiteur (propriété attachée vs nom d'élément) ; points témoins.

### Deferred Ideas (OUT OF SCOPE)
None
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| GST-01 | Sur toute la silhouette (disque r 74 Arcs/Braises, rectangle arrondi pour les autres) : clic sans déplacement = bascule, double-clic = Historique sans bascule, appui-glisser au-delà du seuil Windows = déplacer puis accrocher, clic droit = réglages | Automate pur `AutomateGeste` (§ Pattern 3), séquence `DragMove` vérifiée dans le source WPF (§ Pattern 4), `ClickCount` lu au MouseDown seulement (vérifié source), répartiteur unique sur la grille racine (§ Pattern 2), pastilles = `ButtonBase` qui marque Handled |
| GST-02 | Hors silhouette le clic traverse ; silhouette peinte en `ZoneSilhouette` (`#01000000`) ; garde statique : aucun élément à geste avec pinceau nul/`Transparent` | Règle Win32 officielle des fenêtres layered (alpha 0 = clic traversant), prototype : `#01000000` rend A = 1, `Transparent` rend A = 0 ; garde par parcours d'arbre (§ Pattern 5) |
| GST-03 | Zones prouvées par test par cadran × orientation : `RenderTargetBitmap` (alpha > 0 dedans, = 0 dehors) + `HitTest` de routage aux points témoins ; commentaires « Transparent suffit » corrigés | Recette de rendu hors écran **validée par prototype** (il faut mesurer/arranger le `Content`, pas la `Window`), points témoins calculés (§ Code Examples), pièges DPI/thread/brosse partagée |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- C# / .NET 8 (`net8.0-windows`) / WPF / MVVM strict (CommunityToolkit.Mvvm `[ObservableProperty]`/`[RelayCommand]`) / DI ;
  dossiers Models/Views/ViewModels/Services. Le code-behind d'une vue se limite à transmettre (modèle actuel :
  `CentreHit_MouseLeftButtonDown` → `ClicCentre(e.ClickCount)`) ; la décision vit dans un type pur testable.
- Rendu XAML pur (`Path`/`Shape`), **aucune dépendance native** ; pas de SkiaSharp.
- Fenêtre : `WindowStyle=None`, `AllowsTransparency=True`, `Topmost=True`, `ShowInTaskbar=False` — inchangés.
- Toutes les tailles et couleurs passent par `Resources/DesignTokens.xaml` ou les pinceaux du thème (plan de design § 9.6) :
  `#01000000` n'apparaît **que** dans `DesignTokens.xaml` sous la clé `ZoneSilhouette`.
- UI et commentaires **en français**.
- `Assembly.Location` interdit (vide en mono-fichier) — les gardes textuelles utilisent `GardesPerimetreTests.CheminSources()`.
- Workflow GSD obligatoire ; l'agent **ne lance, n'arrête ni ne clique jamais l'overlay** (STATE.md) : la vérification en clics
  réels est un critère de revue visuelle humaine (plan § 9.2), pas une tâche d'agent.
- Les tests ne doivent jamais lire/écrire le vrai `%APPDATA%\Chronos` (`TempPaths()` de `CadranBindingTests`).

## Summary

Le mécanisme qui décide qu'un clic « traverse » n'est pas WPF mais Windows : sur une fenêtre layered, « les zones dont la
valeur alpha est zéro laissent passer les messages souris » (doc Win32 officielle). WPF ne fait son propre hit-test
qu'**après** que l'OS lui a livré le clic. C'est pourquoi `Fill="Transparent"` est un piège à double face : WPF le
considère « touché » (`VisualTreeHelper.HitTest` le renvoie — vérifié par prototype), mais il rend A = 0, donc l'OS ne livre
jamais le clic. Les commentaires actuels « Transparent suffit, CentreHit le prouve » sont faux : `CentreHit` ne marche que
parce qu'il est posé sur le disque opaque `FondCadran` d'Arcs. `#01000000` rend A = 1 (vérifié) : c'est le plus petit alpha
qui capte la souris, invisible à l'œil.

Le geste unique impose de remplacer le `DragMove()` bloquant lancé dès le `MouseLeftButtonDown` de la fenêtre par une
**décision différée** : appui → capture souris → au premier `MouseMove` qui dépasse le seuil Windows (en DIP : 3,2 × 3,2 sur
cette machine à 125 %), `DragMove()` puis `SnapToNearestCorner()` ; au relâchement sans dépassement, le clic est transmis à
l'arbitre existant. Deux faits vérifiés dans le source WPF cadrent l'implémentation : `ClickCount` n'est renseigné que sur
le MouseDown (le MouseUp porte 0), et `DragMove()` envoie lui-même un `WM_LBUTTONUP` synthétique **pendant** son appel
(réentrance). La logique doit donc vivre dans un automate pur, sans type WPF, comme `ArbitreClicCentre`.

Pour les tests, le prototype a montré que `Measure/Arrange` sur une `Window` jamais affichée **ne met pas en page son
contenu** (contenu 0 × 0, rendu entièrement vide — un test « alpha = 0 hors silhouette » serait vert par accident). Il faut
mesurer/arranger le `Content` (grille racine) directement à la taille de l'empreinte, puis rendre ce `Content` avec
`RenderTargetBitmap(w, h, 96, 96, Pbgra32)`. Ainsi rendu, `#01000000` donne A = 1 à l'intérieur, A = 0 au-delà du bord.

**Primary recommendation:** silhouette déclarée par une propriété attachée `ZoneGeste.Silhouette="True"` sur une `Ellipse`/
`Rectangle` placée en **premier enfant** (sous le contenu) de chaque cadran ; un répartiteur unique sur la grille racine de
`MainWindow` piloté par un automate pur `AutomateGeste` ; tests par rendu du `Content` mis en page + `HitTest` de routage +
garde par parcours d'arbre.

## Standard Stack

Aucun paquet nouveau. Tout est dans le framework et dans le projet de tests existant.

### Core
| Élément | Version | Rôle | Pourquoi |
|---------|---------|------|----------|
| WPF (`net8.0-windows`) | runtime 8.0.25 (vu au test) | `Shape`, routage des événements, `Window.DragMove`, `Mouse.Capture` | Déjà la pile imposée |
| `System.Windows.SystemParameters.MinimumHorizontalDragDistance` / `MinimumVerticalDragDistance` | framework | Seuil clic/glisser de l'utilisateur, **en DIP** | = `SM_CXDRAG`/`SM_CYDRAG` convertis ; mesuré 3,2 × 3,2 DIP à 125 % (4 px physiques) |
| `Interop.NativeMethods.GetDoubleClickTime` | existant | Délai de l'arbitre | Inchangé |
| `ArbitreClicCentre` | existant | Simple clic différé / double-clic | **Inchangé** (décision verrouillée) |

### Supporting (tests)
| Élément | Version | Rôle | Quand |
|---------|---------|------|-------|
| xunit | 2.9.2 | Tests | Existant |
| Xunit.StaFact (`[WpfFact]`) | 1.1.11 | Thread STA pour WPF | Tout test qui construit une vue |
| `System.Windows.Media.Imaging.RenderTargetBitmap` | framework | Lecture de l'alpha rendu | Preuve GST-03 |
| `[Collection("XAML WPF")]` | existant (`XamlWpfCollection.cs`) | Sérialise les classes qui chargent du BAML | Obligatoire pour la nouvelle classe de tests de zones |

### Alternatives Considered
| Au lieu de | On pourrait | Pourquoi non |
|------------|-------------|--------------|
| Propriété attachée `ZoneGeste.Silhouette` | Élément nommé `x:Name="Silhouette"` dans chaque vue | `FindName` ne traverse pas les NameScope des `UserControl` (chaque vue XAML a le sien) : `MainWindow.FindName("Silhouette")` ne trouve pas celles de Braises/Fusible/Marée/Volets. Il faudrait une interface par vue en plus. La propriété attachée se retrouve par simple parcours d'arbre, sert au répartiteur ET aux gardes |
| Décision clic/glisser maison | `DragDrop.DoDragDrop` | Hors sujet (transfert de données, pas déplacement de fenêtre) |
| `WS_EX_TRANSPARENT` / `WM_NCHITTEST` sur mesure | — | `WS_EX_TRANSPARENT` rendrait **toute** la fenêtre traversante (doc Win32) ; `WM_NCHITTEST` dupliquerait ce que l'alpha fait déjà. Aucun des deux n'est présent dans le code (vérifié) |
| Bouton « Montrer les zones de clic » | — | Il n'existe que dans la maquette HTML (aide à la revue), pas dans l'app |

**Installation :** rien.

## Architecture Patterns

### Fichiers touchés (recommandé)
```
src/Chronos/
├── Resources/DesignTokens.xaml          # + <SolidColorBrush x:Key="ZoneSilhouette" Color="#01000000"/>
├── Views/ZoneGeste.cs                   # NOUVEAU : propriété attachée Silhouette (bool) + Trouver(racine) + Contient(...)
├── ViewModels/AutomateGeste.cs          # NOUVEAU : automate PUR appui/déplacement/relâchement (aucun System.Windows)
├── Views/MainWindow.xaml                # CentreHit supprimé ; silhouette d'Arcs ; handlers sur la grille racine ;
│                                        #   pastilles en ZoneSilhouette ; commentaires corrigés
├── Views/MainWindow.xaml.cs             # Répartiteur unique (remplace Cadran_MouseLeftButtonDown + CentreHit_…)
├── Views/Cadrans/Cadran{Braises,Fusible,Maree,Volets}View.xaml   # silhouette en 1er enfant ; Background="Transparent" retiré
├── Views/Reglages/ReglagesWindow.xaml   # l.731 : « Aussi : double-clic sur le cadran »
README.md, docs/publish.md (l.156), docs/data-sources.md (l.620)   # vocabulaire des gestes
tests/Chronos.Tests/
├── AutomateGesteTests.cs                # NOUVEAU (pur, parallélisable)
├── ZonesGesteRenduTests.cs              # NOUVEAU [Collection("XAML WPF")] : RTB + HitTest + garde de pinceaux, 8 variantes
└── GardeGestesCadranTests.cs            # RÉÉCRIT
```

### Pattern 1 : silhouette = forme peinte en `#01`, premier enfant, déclarée par propriété attachée
**Quoi :** chaque cadran porte UNE forme qui couvre exactement sa silhouette, peinte `ZoneSilhouette`, **sous** le contenu.
- Arcs (inline dans `MainWindow.xaml`, grille `IsStyleArcs`) : `<Ellipse Width="148" Height="148" …/>` en premier enfant
  (centrée par la grille 170 → centre 85,85, rayon 74). Les anneaux s'arrêtent à r 71,5 : dedans.
- Braises (`CadranBraisesView`, 170 × 170 à l'échelle 1 après phase 40) : même ellipse 148 en premier enfant de la grille racine.
  La flèche de la phase 41 (triangle à y = 85-80 = 5..12) est à r ≈ 73-80 : **vérifier** qu'aucun pixel peint ne dépasse r 74
  de plus que la bande de tolérance (la pointe du triangle est à r 80 dans la maquette : `M c-5 c-80 … L c c-73`). Voir
  Pitfall 8 — c'est un conflit potentiel entre phase 41 et le disque r 74.
- Fusible / Marée / Volets : `<Rectangle RadiusX="10" RadiusY="10" …/>` **sans taille explicite**, étiré sur toute la vue, la
  vue elle-même faisant l'empreinte (`EmpreinteCadran`, phase 40). La maquette (`zoneRect(w,h)` = `rect x=0 y=0 width=w
  height=h rx=10`) confirme : **le rectangle = l'empreinte entière**, le cadran étant dessiné avec ~6 px de marge à
  l'intérieur. Une seule forme par vue, valable pour les deux orientations (elle suit la taille de la vue).
- Les vues dont la racine est un `StackPanel` sont enveloppées dans une `Grid` : `[Rectangle silhouette] + [contenu]`.

```xml
<!-- Vue de cadran rectangulaire (Fusible / Marée / Volets) -->
<Grid>
    <!-- Silhouette de geste (R7) : TOUTE l'empreinte capte la souris. #01000000 et non Transparent : sur une fenêtre
         layered, Windows laisse passer le clic là où l'alpha vaut 0 ; Transparent rend un alpha 0. -->
    <Rectangle RadiusX="10" RadiusY="10" vues:ZoneGeste.Silhouette="True"
               Fill="{DynamicResource ZoneSilhouette}"/>
    <!-- … contenu existant (StackPanel, gabarits d'orientation) … -->
</Grid>
```

**Brosse dans les `UserControl` :** utiliser `{DynamicResource ZoneSilhouette}` (ou fusionner `DesignTokens.xaml` dans les
ressources de la vue). Un `{StaticResource}` dans une vue compilée séparément se résout au chargement du BAML, avant que la
vue ait un parent : il ne voit ni les ressources de `MainWindow` ni (en test) celles d'une `Application` absente →
`XamlParseException`. C'est la raison même pour laquelle `MainWindow` fusionne `DesignTokens.xaml` localement (commentaire
l.17-18). La garde runtime (Pattern 5) vérifie que la brosse **résolue** vaut bien `#01000000`. Dans `MainWindow.xaml`
(Arcs, pastilles), `{StaticResource ZoneSilhouette}` fonctionne (dictionnaire fusionné au niveau fenêtre).

### Pattern 2 : un seul répartiteur, sur la grille racine de `MainWindow`
**Quoi :** les handlers `MouseLeftButtonDown`, `MouseMove`, `MouseLeftButtonUp`, `LostMouseCapture`, `MouseRightButtonUp`
sont posés sur la grille racine (`<Grid x:Name="Racine" …>`, elle porte déjà `MouseRightButtonUp="OnRightClick"`) et **plus
sur la `Window`**.

Pourquoi la grille racine et un filtre géométrique :
- La silhouette est un **frère** du contenu, pas son ancêtre : un clic sur un chiffre ou un anneau bulle
  TextBlock → grille de la vue → … → grille racine, sans jamais passer par la forme silhouette. Le seul ancêtre commun
  de tout ce qui est peint est la grille racine.
- Des pixels peints existent **hors** silhouette (Arcs : pastilles en bas à droite, à ≈ 111 px du centre > 74). Les
  pastilles d'action sont des `Button` (`ButtonBase.OnMouseLeftButtonDown` marque Handled → la bulle s'arrête, le
  répartiteur ne voit rien, à condition de **ne pas** utiliser `AddHandler(…, handledEventsToo: true)`). Mais
  `PastilleReleveDate` et `PastilleHorsLigne` sont des `Ellipse` inertes : un clic gauche sur elles bullerait jusqu'au
  répartiteur. D'où le filtre : au `MouseLeftButtonDown`, le répartiteur n'arme le geste que si
  `ZoneGeste.Contient(silhouetteVisible, e.GetPosition(silhouetteVisible))` (via `RenderedGeometry.FillContains`).
- Le gestionnaire de fenêtre `MouseLeftButtonDown += Cadran_MouseLeftButtonDown` **doit disparaître** : la `Window` a un
  `Background="Transparent"`, donc côté WPF elle « attrape » tout ce que l'OS lui livre ; le garder relancerait un
  `DragMove` bloquant sur n'importe quel pixel peint.
- Clic droit : recommandation = **non filtré** (tout pixel du cadran qui reçoit le clic droit ouvre les réglages,
  pastilles comprises — c'est le comportement actuel sur les enfants touchés, et `ButtonBase` ne consomme pas le bouton
  droit). Laissé à l'arbitrage du planificateur ; le filtrer est aussi valide.

**Silhouette visible :** `ZoneGeste.Trouver(racine)` parcourt l'arbre visuel et renvoie le seul élément
`Silhouette == true` dont `IsVisible` est vrai (un seul style visible à la fois via `IsStyleX`). Le mettre en cache est
inutile (≤ quelques centaines de visuels, appelé une fois par appui).

### Pattern 3 : automate pur `AutomateGeste` (la décision, sans WPF)
**Quoi :** comme `ArbitreClicCentre`, un type sans `System.Windows`, sans horloge, entièrement testable.

États : `Repos` → `Appuye(depart, clickCount)` → `Glisse` | `Consomme`.

| Entrée | État | Sortie | Nouvel état |
|--------|------|--------|-------------|
| `Appui(x, y, clickCount ≥ 2)` | Repos | `Clic(clickCount)` **immédiat** (→ `ClicCentre(2)` → Historique) | Consomme |
| `Appui(x, y, 1)` | Repos | Rien | Appuye |
| `Deplacement(x, y)` avec `|dx| > seuilX` ou `|dy| > seuilY` | Appuye | `CommencerGlisser` | Glisse |
| `Deplacement` sous le seuil | Appuye | Rien | Appuye |
| `Relache` | Appuye | `Clic(1)` (→ `ClicCentre(1)` → bascule différée par l'arbitre) | Repos |
| `Relache` | Glisse / Consomme | Rien | Repos |
| `PerteCapture` | Appuye | Rien (pas de clic : Alt+Tab, fenêtre système…) | Repos |
| `PerteCapture` | Glisse | Rien | Glisse (le `DragMove` en cours la provoque lui-même) |

Choix justifiés :
- **Le double-clic se décide à l'appui**, pas au relâchement. L'arbitre arme la bascule au premier relâchement (t_up1) ;
  WPF compte le second appui comme double si `t_down2 − t_down1 < délai` (source `MouseDevice.CalculateClickCount`), donc
  `t_down2 − t_up1 < délai` : le second appui arrive **toujours** avant l'échéance → zéro bascule parasite. Décider au
  second relâchement ouvrirait une course (si le second appui est tenu plus longtemps que le premier).
- **Le simple clic s'arme au relâchement** (et non à l'appui comme aujourd'hui) : c'est le seul moment où l'on sait qu'il
  n'y a pas eu de glisser. L'arbitre n'a pas d'« annuler » et doit rester inchangé — armer à l'appui obligerait à le
  modifier. Coût : la bascule part `délai` après le relâchement au lieu de l'appui (imperceptible).
- **Seuil strict par axe** : `Math.Abs(dx) > seuilX || Math.Abs(dy) > seuilY`. `SM_CXDRAG` est défini comme « le nombre
  de pixels **de chaque côté** du point d'appui que le pointeur peut parcourir avant qu'un glisser commence » (doc Win32).
  Positions et seuils sont tous deux en DIP (`e.GetPosition` et `SystemParameters`) : cohérent sans conversion.
- **Après un glisser, ignorer un `clickCount ≥ 2` au prochain appui** (le traiter comme 1). `IsSameSpot` compare des
  coordonnées **client** : après un `DragMove` la fenêtre a bougé avec le curseur, donc un clic rapide juste après un
  glisser bref tombe au « même endroit client » et WPF le compterait double → Historique intempestif. Un drapeau
  `dernierEtaitGlisser` suffit.
- Le type reçoit des `double` (ou un petit `readonly record struct`) et non `System.Windows.Point`, pour rester pur et
  passer la garde textuelle « aucun `System.Windows` » déjà appliquée à l'arbitre.

### Pattern 4 : séquence de glisser (code-behind)
```csharp
// Répartiteur (MainWindow.xaml.cs) — la vue ne fait que traduire les événements WPF pour l'automate.
private void Racine_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    var sil = ZoneGeste.Trouver(Racine);
    if (sil is null || !ZoneGeste.Contient(sil, e.GetPosition(sil))) return;   // hors silhouette : rien
    var p = e.GetPosition(Racine);
    var action = _geste.Appui(p.X, p.Y, e.ClickCount);                          // ClickCount : SEUL le MouseDown le porte
    if (action.EstClic) _vm.ClicCentre(action.ClickCount);                       // double-clic → Historique, tout de suite
    else Racine.CaptureMouse();                                                  // on suit le MouseMove même hors fenêtre
    e.Handled = true;
}

private void Racine_MouseMove(object sender, MouseEventArgs e)
{
    var p = e.GetPosition(Racine);
    if (_geste.Deplacement(p.X, p.Y, SystemParameters.MinimumHorizontalDragDistance,
                           SystemParameters.MinimumVerticalDragDistance) != ActionGeste.CommencerGlisser) return;
    // L'automate est DÉJÀ en « Glisse » : le WM_LBUTTONUP synthétique que DragMove envoie pendant son appel (réentrance)
    // et la perte de capture qu'il provoque tombent sur un état qui les ignore.
    Racine.ReleaseMouseCapture();
    try { DragMove(); }                                   // boucle modale système (SC_MOUSEMOVE) jusqu'au relâchement
    catch (InvalidOperationException) { }                 // bouton déjà relâché : DragMove lève (précédent : HistoriqueWindow)
    finally { _geste.Relache(); }
    _controller.SnapToNearestCorner();                    // AU RETOUR de DragMove, comme aujourd'hui
}

private void Racine_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
{
    var action = _geste.Relache();                        // e.ClickCount vaut 0 ici : ne jamais le lire
    Racine.ReleaseMouseCapture();
    if (action.EstClic) _vm.ClicCentre(1);
}

private void Racine_LostMouseCapture(object sender, MouseEventArgs e) => _geste.PerteCapture();
```
Vérifié dans le source WPF (`Window.DragMove`) : il lève `InvalidOperationException` si `Mouse.LeftButton` n'est pas
`Pressed`, puis envoie `WM_SYSCOMMAND/SC_MOUSEMOVE` (boucle modale) **et ensuite `WM_LBUTTONUP`** de façon synchrone.
Libérer la capture WPF avant l'appel évite un état de capture périmé (la boucle système prend sa propre capture).
Petite conséquence assumée : les ~3 DIP parcourus avant le dépassement du seuil ne sont pas répercutés (le curseur
« glisse » de ≤ 3 DIP par rapport au point saisi).

### Pattern 5 : garde « aucun élément à geste n'a de pinceau nul ou Transparent »
Recommandation : garde **runtime par parcours d'arbre** (plus robuste qu'une regex : elle voit les styles, les gabarits et
les `DynamicResource` résolus), complétée par une garde textuelle légère.
- Éléments « à geste » = (a) tout élément `ZoneGeste.Silhouette == true` ; (b) tout `ButtonBase` (pastilles) et la racine
  de son gabarit ; (c) tout élément portant une `ToolTip` (le survol est une interaction ; `PastilleReleveDate` porte
  EXA-06 par son infobulle).
- Pinceau inspecté : `Shape.Fill` (pour les formes), `Panel.Background` / `Border.Background` / `Control.Background`.
  Assertion : non nul, `SolidColorBrush`, `Color.A ≥ 1` ; pour (a) : exactement `#01000000`.
- Exemptions explicites : la `Window` elle-même (`Background="Transparent"` **doit** rester alpha 0).
- Pour (c), une `Ellipse` à contour (`PastilleReleveDate`) : `Fill` = `ZoneSilhouette` pour que l'infobulle s'ouvre sur
  tout le disque de 12 px (aujourd'hui seul le trait de 2 px la déclenche, contrairement à ce que dit le commentaire l.249).
- Garde textuelle complémentaire : `Fill="Transparent"` / `Background="Transparent"` interdits dans `MainWindow.xaml` hors
  balise `<Window …>`, et dans `Views/Cadrans/*.xaml` ; `#01000000` interdit hors `DesignTokens.xaml`.
- Conséquence : retirer `Background="Transparent"` des quatre `UserControl` (`Cadran*View.xaml:5`). Sans effet côté OS
  (alpha 0 dans les deux cas) mais ils faussent `VisualTreeHelper.HitTest` (la vue entière serait « touchée » même hors
  silhouette) — c'est précisément le faux vert que la décision interdit.

### Anti-Patterns to Avoid
- **`Fill="Transparent"` pour capter la souris sur une fenêtre layered** : alpha 0 → l'OS ne livre pas le clic.
- **Tester les zones avec `HitTest` seul** : il renvoie la `Border` du gabarit de `Window` (fond Transparent) et toute
  forme `Transparent` — prototype : `HitTest(root, coin)` → `Border` transparente.
- **`Measure/Arrange` sur la `Window` non affichée pour la rendre** : le contenu reste 0 × 0, rendu vide (prototype).
- **Lire `e.ClickCount` dans `MouseLeftButtonUp`** : toujours 0 (source WPF : le MouseUp est recréé sans ClickCount).
- **Gestionnaire posé sur la silhouette elle-même** : le contenu (frère) ne bulle pas vers elle ; clics sur les chiffres perdus.
- **`AddHandler(…, true)`** sur le répartiteur : réveillerait le geste sous les pastilles qui ont marqué Handled.
- **Silhouette au-dessus du contenu** : techniquement capte tout, mais assombrit le rendu de 1/255 et masque les
  `ToolTip`/curseurs des enfants ; la placer en premier enfant.
- **Brosse figée partagée non gelée entre threads de test** (voir Pitfall 5).

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser | Pourquoi |
|----------|-------------------|----------|----------|
| Rendre le clic traversant hors silhouette | `WM_NCHITTEST`, région de fenêtre (`SetWindowRgn`), `WS_EX_TRANSPARENT` | L'alpha par pixel de la fenêtre layered (déjà en place) + `#01000000` | Règle native de Windows ; `WS_EX_TRANSPARENT` rendrait tout traversant |
| Déplacer la fenêtre | Suivi manuel de `Left/Top` au `MouseMove` | `Window.DragMove()` puis `OverlayController.SnapToNearestCorner()` | Boucle système (DPI mixte, multi-écrans) déjà éprouvée ; `SnapToNearestCorner` lit `GetWindowRect` |
| Délai et rectangle de double-clic | Horloge maison | `e.ClickCount` (WPF applique déjà `GetDoubleClickTime` et `SM_CXDOUBLECLK`) + `ArbitreClicCentre` | Réglages utilisateur respectés, déjà testé |
| Seuil de glisser | Constante « 4 px » | `SystemParameters.MinimumHorizontal/VerticalDragDistance` | Réglage utilisateur, déjà en DIP |
| Test « point dans la silhouette » côté app | Formules de disque/rectangle dupliquées | `Shape.RenderedGeometry.FillContains(point)` | Une seule source de vérité : la forme déclarée par la vue |

**Key insight :** le seul code vraiment nouveau est l'automate (≈ 60 lignes pures) et le câblage ; tout le reste est un
choix de pinceau et de placement. Le risque n'est pas technique, il est de **croire** que ça marche : d'où les tests de
rendu qui lisent l'alpha, seul critère qu'utilise Windows.

## Common Pitfalls

### Pitfall 1 : `Transparent` est « touché » par WPF mais traversant pour Windows
**Ce qui casse :** une zone `Transparent` semble marcher en test `HitTest` et ne reçoit jamais le clic en vrai.
**Cause :** deux hit-tests successifs — l'OS (alpha) puis WPF (géométrie). **Évitement :** `ZoneSilhouette` partout où un
geste est attendu ; tests par alpha rendu. **Signe :** un commentaire qui dit « Transparent suffit ».

### Pitfall 2 : `Window` jamais affichée = contenu jamais mis en page (VÉRIFIÉ)
**Ce qui casse :** `fenetre.Measure/Arrange` (le motif de `CadranBindingTests.BuildWindow`) laisse le `Content` à 0 × 0 ; un
`RenderTargetBitmap` de la fenêtre ou du contenu est entièrement à alpha 0 → les assertions « alpha = 0 hors silhouette »
passent par accident, et « alpha > 0 dedans » échoue sans explication claire.
**Évitement :** `var racine = (FrameworkElement)fenetre.Content; racine.Measure(empreinte); racine.Arrange(new Rect(empreinte));
racine.UpdateLayout();` puis rendre `racine`. (Prototype : rendu correct, ressources et liaisons résolues car le parent
logique reste la fenêtre.) Alternative vérifiée : `Show()` hors écran (`Left = Top = -32000`, `ShowActivated = false`) puis
`Dispatcher.Invoke(…, ApplicationIdle)` — fonctionne aussi, mais affiche une vraie fenêtre ; à éviter (règle « l'agent ne
manipule pas l'overlay » et coût). **Toujours** ajouter une assertion de non-vacuité : `ActualWidth > 0` et au moins un
pixel A > 0 au centre.

### Pitfall 3 : `ClickCount` absent du MouseUp ; double-clic au relâchement = course (VÉRIFIÉ source)
Voir Pattern 3. **Signe :** une bascule visible puis l'Historique qui s'ouvre sur un double-clic un peu lent.

### Pitfall 4 : réentrance de `DragMove` (VÉRIFIÉ source)
`DragMove` envoie `WM_LBUTTONUP` pendant son exécution → `MouseLeftButtonUp` est traité **avant** le retour de `DragMove`.
Si l'automate n'est pas déjà en `Glisse`, ce relâchement serait pris pour un clic → bascule après chaque déplacement.
**Évitement :** passer en `Glisse` avant l'appel (l'automate renvoie `CommencerGlisser` en changeant d'état).

### Pitfall 5 : brosse statique non gelée partagée entre tests STA (VÉRIFIÉ)
Chaque `[WpfFact]` tourne sur son propre thread STA. Une `static readonly SolidColorBrush` non gelée créée par un test et
utilisée par un autre lève « le thread appelant ne peut pas accéder à cet objet » — le prototype passait test par test et
échouait en lot. **Évitement :** `Freeze()` toute brosse partagée de test, ou la créer par test ; la fenêtre charge
`DesignTokens.xaml` par instance (pas de partage).

### Pitfall 6 : DPI — rendre à 96 DPI, choisir des témoins à ≥ 2 DIP du bord
Cette machine est à **125 %** (`PixelsPerDip = 1,25`, mesuré). `RenderTargetBitmap(w, h, 96, 96, …)` produit 1 pixel = 1 DIP
quelle que soit l'échelle système (prototype : bord du disque net entre r 73 → A = 1 et r 74 → A = 0). Le lissage rend
A ∈ {0, 1} dans la frange (1 × couverture arrondi) : aucun témoin à moins de ~1,5 DIP de la frontière. La fenêtre réelle à
125 % fait 213 px physiques pour 170 DIP (`ActualWidth = 170,4`, mesuré) : sans effet sur les tests à 96 DPI. Rendu à 120 DPI
vérifié aussi (A = 1 à r 72 × 1,25, A = 0 à r 76 × 1,25) — inutile en CI.

### Pitfall 7 : `FindName` ne traverse pas les `UserControl`
Les noms déclarés dans `CadranBraisesView.xaml` etc. vivent dans le NameScope de la vue. **Évitement :** propriété attachée +
parcours d'arbre (ou `vue.FindName`). Utile aussi pour les tests.

### Pitfall 8 : pixels peints hors silhouette (Arcs : pastilles ; Braises : flèche de la phase 41 ; mot « indisponible »)
- Pastilles d'Arcs en bas à droite : hors du disque r 74 **par construction** (bord gauche de la rangée à 72,8 px du centre en
  x seul, coin à ≈ 111 px). Elles captent leurs propres clics (alpha > 0) : c'est voulu. Les témoins « hors silhouette »
  doivent être pris dans l'état nominal (pastilles masquées), et un scénario séparé vérifie qu'une pastille visible route
  vers son `ButtonBase`.
- Flèche de Braises (phase 41) : la maquette place la pointe à r 73-80 (`M c-5 c-80 … L c c-73`), donc **au-delà de r 74**.
  Si la phase 41 la dessine ainsi, des pixels peints sortiront du disque. Sans conséquence fonctionnelle (le filtre
  géométrique du répartiteur ignore le clic ; l'OS le livre à la fenêtre, qui ne fait rien), mais un test « tout pixel
  peint est dans la silhouette » rougirait. À trancher au plan : soit bande d'exemption documentée pour la flèche, soit
  (préférable) accepter que le contrat soit « alpha > 0 dedans, = 0 aux témoins dehors » et choisir les témoins hors flèche.
- Mot « indisponible » : aujourd'hui en bas à gauche (hors disque, `IsHitTestVisible=False`) ; la phase 40 doit le recentrer
  (plan § 7). Vérifier après la phase 40 et ajouter un scénario `DataUnavailable = true`.

### Pitfall 9 : `StaticResource` dans les vues sans `Application`
Voir Pattern 1. **Signe :** `XamlParseException` dans les tests de la galerie ou des vues isolées.

### Pitfall 10 : ordre de dépendance — la géométrie n'existe pas encore
Les empreintes (`EmpreinteCadran`), l'enum d'orientation, le retrait des `Viewbox` et Braises à l'échelle 1 arrivent en
phases 40-41. Les noms exacts (propriété d'orientation du VM, tokens `CadranLargeur*`) ne sont **pas encore fixés** : le plan
42 doit les lire dans le code livré par 40-41, pas dans cette recherche. Aujourd'hui (avant 40), les vues sont sous
`Viewbox` : une silhouette posée dedans serait mise à l'échelle et ne correspondrait plus au contrat.

## Code Examples

### Propriété attachée et test de contenance
```csharp
// Source : motif standard WPF (DependencyProperty.RegisterAttached) ; FillContains vérifié par prototype (après mise en page).
namespace Chronos.Views;

/// <summary>R7 — déclare la silhouette de geste d'un cadran. Une seule silhouette visible à la fois.</summary>
public static class ZoneGeste
{
    public static readonly DependencyProperty SilhouetteProperty = DependencyProperty.RegisterAttached(
        "Silhouette", typeof(bool), typeof(ZoneGeste), new PropertyMetadata(false));
    public static bool GetSilhouette(DependencyObject o) => (bool)o.GetValue(SilhouetteProperty);
    public static void SetSilhouette(DependencyObject o, bool v) => o.SetValue(SilhouetteProperty, v);

    /// <summary>La silhouette VISIBLE sous <paramref name="racine"/> (parcours de l'arbre visuel), ou null.</summary>
    public static Shape? Trouver(DependencyObject racine) { /* DFS VisualTreeHelper, saute les sous-arbres non visibles */ }

    /// <summary>Le point (coordonnées de la forme) est-il dans la silhouette ? Faux tant que la forme n'est pas mise en page.</summary>
    public static bool Contient(Shape silhouette, Point p) => silhouette.RenderedGeometry.FillContains(p);
}
```
Attention : `RenderedGeometry` est vide tant que la forme n'a pas été arrangée (prototype : `Bounds = Empty` dans une fenêtre
non mise en page). En production c'est toujours le cas au moment d'un clic.

### Lecture de l'alpha rendu (motif de test validé)
```csharp
// Source : prototype exécuté (scratchpad proto42), cohérent avec HonneteteHistoriqueTests.TraceRendu.
private static byte[] Rendre(FrameworkElement racine, int w, int h)
{
    var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
    bmp.Render(racine);
    var px = new byte[w * h * 4];
    bmp.CopyPixels(px, w * 4, 0);
    return px;
}
private static byte Alpha(byte[] px, int w, int x, int y) => px[(y * w + x) * 4 + 3];   // Pbgra32 : B, G, R, A

// Construction : MainWindow comme CadranBindingTests.BuildWindow (TempPaths, VM déterministe), PUIS :
var racine = (FrameworkElement)fenetre.Content;
var taille = EmpreinteCadran(style, orientation);        // phase 40
racine.Measure(taille); racine.Arrange(new Rect(taille)); racine.UpdateLayout();
Assert.True(racine.ActualWidth > 0);                     // non-vacuité (Pitfall 2)
```
Mesures du prototype (disque 148 en `#01000000`, disque rouge opaque de 60 au centre, à 96 DPI) : centre A = 255 ; r 60, 72,
73 → A = 1 ; r 74, 75, 78 → A = 0 ; coins → A = 0 ; `Border` `Transparent` au coin → A = 0 mais **touchée** par `HitTest`.

### Points témoins recommandés (coordonnées pixel, centre du pixel = +0,5)
- **Disque (Arcs, Braises — 170 × 170, centre 85,85, r 74)**
  - Dedans : (85,85) ; (15,85) (155,85) (85,15) (85,155) [r ≈ 70] ; diagonales (35,35) (134,35) (35,134) (134,134) [r ≈ 70] ;
    zone vide entre anneaux d'Arcs, ex. (85,40) [r 45, entre R38 et R52 — prouve que « l'entre-anneaux » capte désormais].
  - Dehors : (2,2) (167,2) (2,167) [coins ; (167,167) seulement si pastilles masquées] ; (85,1) si la flèche de Braises
    n'y est pas, sinon (40,3) ; axes à r ≈ 79 : (6,85) (164,85).
- **Rectangle arrondi (empreinte W × H, r 10)**
  - Dedans : (W/2,H/2) ; (3,H/2) (W−4,H/2) (W/2,3) (W/2,H−4) ; quasi-coins (5,5) (W−6,5) (5,H−6) (W−6,H−6)
    [distance au centre du congé ≈ 6,4 < 10] ; **interstice entre deux barres** (prouve le « point faible assumé » du plan § 8).
  - Dehors : (0,0) (W−1,0) (0,H−1) (W−1,H−1) [pixel entièrement hors du congé : distance ≥ 11,3 > 10].
- Les générer depuis le contrat (r 74, r 10, empreinte) dans le test, **pas** depuis la forme XAML : c'est ce qui épingle la
  déclaration de la vue au plan de design.

### `HitTest` de routage (complément, jamais seul)
```csharp
// Dedans : le visuel touché doit avoir la grille racine (porteuse du répartiteur) pour ancêtre, sans ButtonBase sur le
// chemin, ET le filtre du répartiteur doit accepter le point. Dehors : avec les fonds Transparent retirés des vues,
// HitTest(racine, p) doit renvoyer null (on teste la racine, PAS la Window dont la Border de gabarit est Transparent).
var touche = VisualTreeHelper.HitTest(racine, p)?.VisualHit;
Assert.NotNull(touche);
Assert.Contains(racine, Ancetres(touche));
Assert.DoesNotContain(Ancetres(touche), a => a is ButtonBase);
var sil = ZoneGeste.Trouver(racine)!;
Assert.True(ZoneGeste.Contient(sil, racine.TranslatePoint(p, sil)));
```
Option de bout en bout pour le clic droit (si non filtré) : lever `UIElement.MouseRightButtonUpEvent` sur `touche` avec un
`IOuvreurReglages` factice et vérifier `Ouvrir()` appelé une fois. Le clic gauche ne se simule pas proprement
(`ClickCount` a un setter interne, `GetPosition` lit la vraie souris) : c'est l'automate pur qui porte ces cas.

## State of the Art

| Ancien | Nouveau | Quand | Impact |
|--------|---------|-------|--------|
| `DragMove()` bloquant dès `MouseLeftButtonDown` de la fenêtre | Décision au seuil (capture → `MouseMove` → `DragMove`) | cette phase | Le clic et le glisser coexistent sur la même surface |
| Disque `CentreHit` 66 px `Transparent` posé sur `FondCadran` | Silhouette entière `#01000000` par cadran | cette phase | Cible Fitts × ~5 ; marche pour les 5 styles, pas seulement Arcs |
| Simple clic armé à l'appui | Armé au relâchement sans glisser | cette phase | Bascule ≈ délai après relâchement ; arbitre inchangé |

**Deprecated/outdated :** commentaires `MainWindow.xaml` l.188-190 (CentreHit), l.206 (« le clic continue d'atteindre
CentreHit »), l.249-250, l.268-270, l.292-296 ; `MainWindow.xaml.cs` l.49, l.66-80 ; textes « au centre du cadran »
(`ReglagesWindow.xaml:731`, `README.md:19, 100, 104`, `docs/publish.md:156`, `docs/data-sources.md:620`). Les commentaires
d'en-tête des vues (« clic au centre ») aussi.

## Open Questions

1. **Flèche de Braises au-delà de r 74 (phase 41)**
   - Connu : maquette = pointe à r 80, base à r 73. Contrat R7 = disque r 74.
   - Flou : la phase 41 garde-t-elle exactement ces cotes ?
   - Recommandation : lire le code livré ; si la flèche dépasse, témoins « dehors » hors de son secteur et note dans le test.
     Ne pas élargir le disque sans accord (r 74 est verrouillé).
2. **Clic droit filtré par la silhouette ou non**
   - Recommandation : non filtré (comportement actuel conservé sur les pastilles). Les deux respectent GST-01.
3. **Infobulle et curseur de l'ancien `CentreHit`** (« Cliquer : basculer… », `Cursor="Hand"`)
   - Le plan de design est muet. Recommandation : retirer le curseur main (la surface sert aussi à glisser) ; infobulle
     facultative sur la grille racine avec le texte des quatre gestes, à faire valider en revue visuelle. Une infobulle sur la
     silhouette seule ne s'afficherait pas au-dessus du contenu (frère, pas ancêtre).
4. **Noms livrés par la phase 40** (propriété d'orientation du VM, `EmpreinteCadran`, structure des gabarits)
   - Recommandation : le plan 42 commence par une tâche de lecture/alignement ; les tests de zones itèrent sur
     `(style, orientation)` à partir de l'API réelle.

## Environment Availability

| Dépendance | Requise par | Disponible | Version | Repli |
|------------|-------------|-----------|---------|-------|
| .NET SDK | build/tests | ✓ | 10.0.201 (cible net8.0-windows ; runtime de test 8.0.25) | — |
| Paquets de test (xunit, Xunit.StaFact, Test.Sdk) | tests WPF | ✓ (cache NuGet) | 2.9.2 / 1.1.11 / 17.11.1 | — |
| Session de bureau interactive (rendu WPF logiciel) | `RenderTargetBitmap` | ✓ (prototype exécuté) | échelle 125 % | — |
| Clics réels sur l'overlay | revue visuelle § 9.2 | — humain uniquement | — | Constat utilisateur (phase 43) |

**Missing dependencies with no fallback :** aucune.

## Validation Architecture

### Test Framework
| Propriété | Valeur |
|-----------|--------|
| Framework | xunit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`) |
| Config file | `tests/Chronos.Tests/Chronos.Tests.csproj` (chemin des sources injecté : `CheminSourcesChronos`) |
| Quick run command | `dotnet test tests/Chronos.Tests --filter "FullyQualifiedName~AutomateGeste|FullyQualifiedName~ZonesGeste|FullyQualifiedName~GardeGestesCadran|FullyQualifiedName~ArbitreClicCentre"` |
| Full suite command | `dotnet test tests/Chronos.Tests` |

### Phase Requirements → Test Map
| Req ID | Comportement | Type | Commande automatisée | Existe ? |
|--------|--------------|------|----------------------|----------|
| GST-01 | Appui + relâchement sous le seuil → `Clic(1)` ; dépassement d'un seul axe (> seuil, pas ≥) → `CommencerGlisser` ; `clickCount 2` → `Clic(2)` immédiat puis relâchement → Rien ; relâchement en `Glisse` (réentrance) → Rien ; perte de capture en `Appuye` → pas de clic ; `clickCount 2` juste après un glisser → traité comme 1 | unit (pur) | `dotnet test tests/Chronos.Tests --filter FullyQualifiedName~AutomateGesteTests` | ❌ Wave 0 |
| GST-01 | Arbitre inchangé (double-clic = Historique sans bascule) | unit | `--filter FullyQualifiedName~ArbitreClicCentreTests` | ✅ |
| GST-01 | Câblage : plus de `CentreHit`, plus de `MouseLeftButtonDown +=` sur la `Window` ; répartiteur appelle `ClicCentre(`, `DragMove()` suivi de `SnapToNearestCorner()`, lit `MinimumHorizontalDragDistance` et `MinimumVerticalDragDistance`, `CaptureMouse`/`ReleaseMouseCapture` ; `MouseRightButtonUp` câblé ; `AutomateGeste.cs` sans `System.Windows` ni `DateTime.Now` ; `e.ClickCount` absent du gestionnaire de relâchement | statique (texte) | `--filter FullyQualifiedName~GardeGestesCadranTests` | ✅ à réécrire |
| GST-01 | Une silhouette exactement, visible, par variante (8 : Arcs, Braises, Fusible H/V, Marée V/H, Volets H/V) ; ellipse 148 centrée pour Arcs/Braises ; rectangle `RadiusX=RadiusY=10` à la taille de l'empreinte pour les autres | intégration WPF | `--filter FullyQualifiedName~ZonesGesteRenduTests` | ❌ Wave 0 |
| GST-02 | Garde de pinceaux : tout élément à geste (silhouette, `ButtonBase` + racine de gabarit, porteur de `ToolTip`) a une brosse non nulle d'alpha ≥ 1 ; silhouettes = `#01000000` ; aucun `Transparent`/`#01000000` littéral hors `DesignTokens.xaml` dans `MainWindow.xaml` et `Views/Cadrans/*.xaml` (sauf `<Window Background>`) | intégration + statique | `--filter FullyQualifiedName~ZonesGeste` | ❌ Wave 0 |
| GST-02/03 | `RenderTargetBitmap` par variante : A > 0 à chaque témoin dedans, A = 0 à chaque témoin dehors ; assertion de non-vacuité ; état nominal + `DataUnavailable` + pastilles visibles (Arcs) | intégration WPF | `--filter FullyQualifiedName~ZonesGesteRenduTests` | ❌ Wave 0 |
| GST-03 | `HitTest` de routage aux mêmes témoins : dedans → ancêtre = grille racine, pas de `ButtonBase`, filtre accepté ; dehors → `null` (sur la racine) ; pastille visible → `ButtonBase` sur le chemin | intégration WPF | idem | ❌ Wave 0 |
| GST-03 | Commentaires « Transparent suffit » / « CentreHit le prouve » absents | statique | `--filter FullyQualifiedName~GardeGestesCadranTests` | ✅ à étendre |
| GST-01 | Textes : « double-clic sur le cadran » dans `ReglagesWindow.xaml` ; « au centre du cadran » absent de README/docs | statique | idem (ou garde de doc existante) | ✅ à étendre |
| GST-01 | Clics réels (bascule, double-clic, glisser + accroche, clic droit, traversée) sur les 8 variantes à 100 % et 150 % | manuel | — (revue visuelle § 9.2, l'agent ne clique pas l'overlay) | manuel |

### Sampling Rate
- **Par commit de tâche :** quick run command ci-dessus (< 30 s).
- **Par fin de vague :** `dotnet test tests/Chronos.Tests` (suite complète, inclut `CadranBindingTests`, `ReglagesWindowTests`).
- **Porte de phase :** suite complète verte avant `/gsd:verify-work` ; la vérification en clics réels reste au constat humain.

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/AutomateGesteTests.cs` — GST-01 (pur, sans `[Collection]`)
- [ ] `tests/Chronos.Tests/ZonesGesteRenduTests.cs` — GST-02/03, `[Collection("XAML WPF")]`, fabrique de fenêtre reprise de
      `CadranBindingTests.BuildWindow` (TempPaths, VM déterministe) + mise en page du `Content` (Pitfall 2)
- [ ] Réécriture de `tests/Chronos.Tests/GardeGestesCadranTests.cs` (ses trois tests actuels cassent dès la suppression de
      `CentreHit` et de `Cadran_MouseLeftButtonDown`)
- Aucun paquet à installer.

## Sources

### Primary (HIGH confidence)
- Prototype exécuté sur la machine (scratchpad `proto42`, 2026-10-03) : rendu `#01000000` → A = 1 ; `Transparent` → A = 0 mais
  touché par `HitTest` ; `Window` non affichée → contenu 0 × 0 ; mise en page du `Content` → rendu correct ; brosse statique
  non gelée → échec inter-threads ; `PixelsPerDip` 1,25 ; seuil de glisser 3,2 × 3,2 DIP ; `FillContains` vide avant mise en page.
- Microsoft Learn — *Window Features / Layered Windows* : « Hit testing of a layered window is based on the shape and
  transparency of the window… areas… whose alpha value is zero will let the mouse messages through. However, if the layered
  window has the WS_EX_TRANSPARENT extended window style, the shape of the layered window will be ignored… » —
  https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features
- Source WPF `Window.DragMove` (dotnet/wpf, main) — exception si bouton non pressé, `WM_SYSCOMMAND/SC_MOUSEMOVE` puis
  `WM_LBUTTONUP` — https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Window.cs
- Source WPF `MouseDevice` (dotnet/wpf, main) — `ClickCount` calculé et posé sur `PreviewMouseDown` seulement ; `MouseUp` recréé
  sans `ClickCount` ; `CalculateClickCount` (temps depuis le 1er appui, même endroit en coordonnées client, même bouton) —
  https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Input/MouseDevice.cs
- Microsoft Learn — `GetSystemMetrics` : `SM_CXDRAG` / `SM_CYDRAG` « de chaque côté du point d'appui » —
  https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getsystemmetrics
- Code du dépôt : `MainWindow.xaml(.cs)`, `ArbitreClicCentre.cs`, `MainViewModel.ClicCentre`, `OverlayController.SnapToNearestCorner`,
  `GardeGestesCadranTests.cs`, `CadranBindingTests.cs`, `HonneteteHistoriqueTests.cs` (motif RTB), `Cadran*View.xaml`,
  `SessionsWindow.xaml` / `SessionStyles.xaml` (précédent `#01000000`), `HistoriqueWindow.xaml.cs:116-120` (précédent
  try/catch `DragMove`), maquette `.zeus/maquettes/cycle2-cadrans-themes.html` (`zoneRect(w,h)` = empreinte entière, rx 10).

### Secondary (MEDIUM confidence)
- Appeler `DragMove` depuis un gestionnaire `MouseMove` (bouton pressé) : usage courant documenté par la communauté et
  cohérent avec le source (seule condition : `Mouse.LeftButton == Pressed`) ; non exécuté en clics réels ici (interdit).
- Le décalage de ≤ 3 DIP entre point saisi et curseur après dépassement du seuil : déduit du fonctionnement de
  `SC_MOUSEMOVE` (la boucle part de la position courante), non mesuré.

### Tertiary (LOW confidence)
- Aucune.

## Metadata

**Confidence breakdown :**
- Standard stack : HIGH — aucune dépendance nouvelle, versions lues dans les csproj.
- Architecture : HIGH pour le mécanisme alpha / routage / `ClickCount` / `DragMove` (doc officielle + source + prototype) ;
  MEDIUM pour l'ergonomie du glisser au seuil (non cliquable par l'agent).
- Pitfalls : HIGH — les pièges 2, 3, 4, 5, 6 sont vérifiés par prototype ou source ; 8 et 10 dépendent du code des phases 40-41.

**Research date :** 2026-10-03
**Valid until :** 2026-11-02 (pile stable) — mais à relire contre le code effectivement livré par les phases 40 et 41.
