---
phase: 40-cadrans-l-chelle-1-et-orientations
verified: 2026-10-03T00:00:00Z
status: passed
score: 6/6 must-haves verified
---

# Phase 40 : Cadrans à l'échelle 1 et orientations — Rapport de vérification

**Objectif de la phase :** cadrans rendus à l'échelle 1 sans Viewbox, fenêtre à l'empreinte, recalage au coin d'accroche courant sans débordement, trois variantes nouvelles reconnaissables avec leurs états, carte Orientation par cadran, galerie à 8 variantes.
**Vérifié le :** 2026-10-03
**Statut :** passed (vérification initiale)
**Périmètre :** relecture du code réel, sans lancer l'exe. La revue visuelle (8 variantes × 4 coins, 100/150 %, Volets V lisible) relève de la DESIGN-REVIEW après la phase 42 et du constat de la phase 43. Elle n'est pas comptée comme un manque. Porte déclarée passée : build `-warnaserror` à 0 avertissement, 1866/1866 tests (non relancée ici).

## Vérité observables

| # | Vérité | Statut | Preuve |
|---|--------|--------|--------|
| 1 | Plus de Viewbox sur la fenêtre principale ; la fenêtre prend l'empreinte du style et de l'orientation | VERIFIED | `MainWindow.xaml` : `Width/Height` liés à `LargeurCadran/HauteurCadran`, cadrans enfants directs du Grid. `MainViewModel` : `EmpreinteCadran.Pour(CadranStyle, OrientationCourante)`. Le seul `Viewbox` restant est l'aperçu des réglages (`ReglagesWindow.xaml:849`), conforme au plan §1.3. Gardes : `EmpreinteCadranTests.MainWindow_n_a_plus_de_Viewbox…`, `CadranBindingTests` (`Assert.Empty(...OfType<Viewbox>())`). |
| 2 | Empreintes conformes au contrat §1.1, en tokens et en fonction pure | VERIFIED | `Rendering/EmpreinteCadran.cs` : Fusible 190×92 / 110×190, Volets 190×66 / 128×190, Marée 132×160 / 190×96, Arcs et Braises 170×170. `DesignTokens.xaml` l.197-212 : 16 tokens `CadranLargeur*/CadranHauteur*` aux mêmes valeurs. Égalité épinglée par `EmpreinteCadranTests`. |
| 3 | La fenêtre reste collée à son coin courant, sans débordement, après changement d'empreinte | VERIFIED | `MainWindow.xaml.cs` : `SizeChanged` appelle `RecalerSurCoinCourant()`, ignoré pendant un glisser et à la première mise en page, avec un cas de repli pour `RestaurationSansTaille`. `OverlayController.RecalerSurCoinCourant` : coin courant, pixels physiques, moniteur le plus proche, marge à l'échelle du DPI, `SWP_NOSIZE\|NOZORDER`, sans persistance. `CornerSnap.RecalerSurCoin` borne x et y à la zone de travail. `CornerSnapTests` (13 tests). |
| 4 | Fusible vertical, Marée horizontale (ligne d'eau ondulée) et Volets vertical existent avec leur signe propre | VERIFIED | `FuseBar`, `TideColumn` et `FlapRow` portent une DP `Orientation` et s'appuient sur `GeometrieCadrans.Fusible/Maree/Volets`. `LigneDEauOndulee` est utilisée en Marée horizontale, avec une droite en vertical. Étincelle au front du Fusible. Les trois vues exposent `Orientation`. `GeometrieCadransTests` (19 tests). |
| 5 | États en attente, plancher et indisponible gérés dans les deux orientations | VERIFIED | Attente : pinceau `WaitBrush` (`CadranAttente`) dans les trois contrôles. Plancher : grain/pointillé (`FuseBar` l.84-86, `TideColumn` l.79-95, dont ligne d'eau pointillée). Indisponible : `MainWindow.xaml` `MotIndisponible` centré via `MotIndisponibleAuCentre` pour les cadrans rectangulaires. |
| 6 | Carte Orientation par cadran, indépendante de VerticalLayout, et galerie à 8 variantes | VERIFIED | `ReglagesWindow.xaml` l.785 : carte `CarteOrientation`, visible selon `EstStyleOrientable`, puces Horizontal/Vertical liées à `ChoisirOrientationCommand`. `ChronosSettings` : `OrientationFusible` (H), `OrientationMaree` (V), `OrientationVolets` (H), tous trois persistés. `MainViewModel.ChoisirOrientation` ne modifie que le cadran courant, sans lien avec le widget de sessions (`OrientationCadran` est un type neutre distinct de `VerticalLayout`). `CadranGalleryWindow.xaml` : « 8 variantes » (Arcs, Braises, Fusible H/V, Marée V/H, Volets H/V). Tests : `CadransOrientationBindingTests` (15). |

**Score :** 6/6

## Artefacts

| Artefact | Statut | Détail |
|----------|--------|--------|
| `src/Chronos/Rendering/EmpreinteCadran.cs` | VERIFIED | Substantiel et câblé dans `MainViewModel`. |
| `src/Chronos/Rendering/GeometrieCadrans.cs` | VERIFIED | Fonctions pures utilisées par les trois contrôles. |
| `src/Chronos/Controls/Cadrans/{FuseBar,TideColumn,FlapRow,EmberRingControl}.cs` | VERIFIED | Rendu dans les deux sens, états attente/plancher. |
| `src/Chronos/Views/Cadrans/Cadran*View.xaml` (Arcs, Braises, Fusible, Marée, Volets) | VERIFIED | Utilisés par `MainWindow` et la galerie. |
| `src/Chronos/Placement/CornerSnap.cs` | VERIFIED | `RecalerSurCoin` avec bornage. |
| `src/Chronos/Services/OverlayController.cs` | VERIFIED | `RecalerSurCoinCourant`. |
| `src/Chronos/Views/CadranGalleryWindow.xaml` | VERIFIED | Huit variantes. |
| `src/Chronos/Views/Reglages/ReglagesWindow.xaml` | VERIFIED | Carte Orientation et aperçu à l'empreinte. |

## Liens clés

| De | Vers | Via | Statut |
|----|------|-----|--------|
| `MainWindow` Width/Height | `EmpreinteCadran` | `LargeurCadran`/`HauteurCadran`, avec `NotifierEmpreinte` à chaque changement de style ou d'orientation | WIRED |
| `MainWindow.SizeChanged` | `OverlayController.RecalerSurCoinCourant` | `CornerSnap.RecalerSurCoin` | WIRED |
| Carte Orientation | Réglages persistés | `ChoisirOrientationCommand`, `_settings with { OrientationX }` | WIRED |
| Réglages persistés | VM | Lecture dans `MainViewModel` l.471-473 | WIRED |
| `MainWindow` | Vues Fusible/Marée/Volets | `Orientation="{Binding OrientationX}"` | WIRED |

## Flux de données (niveau 4)

Les orientations vont de `settings.json` au VM, puis aux DP des vues, puis à la géométrie. Les fractions viennent des `WindowState` réels du VM. Aucune valeur codée en dur n'a été trouvée. FLOWING.

## Couverture des exigences

| Exigence | Plans | Statut | Preuve |
|----------|-------|--------|--------|
| CAD-01 | 40-01, 04, 05, 06 | SATISFAITE | Vérités 1 et 2. |
| CAD-02 | 40-02, 07, 08 | SATISFAITE | Vérité 3. L'aperçu des réglages à l'empreinte réelle est en place (`ReglagesWindow.xaml` l.849). |
| CAD-03 | 40-02, 03, 04, 05, 06 | SATISFAITE | Vérités 4 et 5. |
| CAD-04 | 40-01, 06, 08 | SATISFAITE | Vérité 6. |

Toutes les exigences listées dans les PLAN figurent dans `REQUIREMENTS.md` (cochées, « Complete »). Aucune exigence orpheline : CAD-01 à CAD-04 sont les seules mappées à la phase 40.

## Anti-patterns

| Fichier | Motif | Gravité | Impact |
|---------|-------|---------|--------|
| `MainWindow.xaml` | `CentreHit` : ellipse de 66 px toujours partagée par tous les styles | Info | Hors périmètre de la phase 40. Le remplacement par la silhouette complète est la phase 42 (R7). |

Aucun TODO, stub ni retour vide dans les fichiers de la phase.

## Contrôles comportementaux

Non lancés : l'exe ne doit pas être démarré. Les contrôles se sont limités à la lecture du code et des tests (EmpreinteCadran 3, CornerSnap 13, GeometrieCadrans 19, CadransOrientationBinding 15, GardeGestes 5). La porte 1866/1866 est reprise de la déclaration de l'orchestrateur.

## Vérification humaine (reportée, pas un manque)

1. **Revue visuelle réelle** : 8 variantes × 4 coins, 100 % et 150 %, lisibilité de Volets vertical. Reportée à la DESIGN-REVIEW après la phase 42 et au constat de la phase 43.

## Synthèse

Aucun manque. L'empreinte, le recalage, les trois variantes nouvelles et leurs états, la carte Orientation par cadran (indépendante de `VerticalLayout`) et la galerie à huit variantes sont présents, substantiels et câblés dans le code réel. Le seul `Viewbox` restant est celui de l'aperçu des réglages, voulu par le contrat.

---

_Vérifié le : 2026-10-03_
_Vérificateur : Claude (gsd-verifier)_
