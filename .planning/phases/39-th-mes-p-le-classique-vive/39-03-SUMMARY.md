---
phase: 39-th-mes-p-le-classique-vive
plan: 03
subsystem: theming
tags: [wpf, theming, cadrans, dynamicresource, garde]
requires:
  - "39-01 : tokens CadranTuile, CadranAttente, PlaqueTexte, PlaqueFilet, PlaqueHachure dans BrushTokens()"
provides:
  - "Garde source GardeCouleursCadransTests : aucune couleur littérale dans Views/Cadrans (4 xaml) et Controls/Cadrans (4 cs)"
  - "Test de liaison CadransThemeBindingTests : 4 vues x 15 thèmes, pinceaux == tokens du thème (Assert.Same)"
  - "DP WaitBrush sur EmberRingControl, FuseBar, TideColumn, FlapRow ; DP de pinceaux sans défaut coloré"
  - "Galerie --cadrans thémée (recopie BrushTokens() au changement de SelectedTheme)"
affects: [39-04, 40]
tech-stack:
  added: []
  patterns:
    - "Contrôle de cadran sans défaut coloré : la vue fournit chaque pinceau en DynamicResource, un oubli ne dessine rien et le test de liaison le détecte"
    - "Garde source par ligne avec anti-mutisme (exactement 4 + 4 fichiers) et auto-test [Theory] des regex"
key-files:
  created:
    - tests/Chronos.Tests/GardeCouleursCadransTests.cs
    - tests/Chronos.Tests/CadransThemeBindingTests.cs
  modified:
    - src/Chronos/Controls/Cadrans/EmberRingControl.cs
    - src/Chronos/Controls/Cadrans/FuseBar.cs
    - src/Chronos/Controls/Cadrans/TideColumn.cs
    - src/Chronos/Controls/Cadrans/FlapRow.cs
    - src/Chronos/Views/Cadrans/CadranBraisesView.xaml
    - src/Chronos/Views/Cadrans/CadranFusibleView.xaml
    - src/Chronos/Views/Cadrans/CadranMareeView.xaml
    - src/Chronos/Views/Cadrans/CadranVoletsView.xaml
    - src/Chronos/Views/CadranGalleryWindow.xaml.cs
decisions:
  - "Alpha de l'état « en attente » de Marée passé de 0x5A à 0x6E : les quatre contrôles partagent CadranAttente"
  - "Sillon du Fusible et tuiles 5H/7J de Volets sur CadranTuile ; canal de Marée sur FondCadran ; volets éteints sur Piste5h ; cendres de Braises sur TickMajeur"
  - "Test de liaison par arbre LOGIQUE (les TextBlock masqués par Visibility sont aussi vérifiés)"
metrics:
  duration: "~10 min"
  completed: 2026-10-03
  tasks: 3
  files: 11
---

# Phase 39 Plan 03 : les quatre cadrans alternatifs suivent le thème

Braises, Fusible, Marée et Volets prennent maintenant les couleurs du thème actif au lieu de rester en minuit. Leurs vues lisent tous leurs pinceaux en `DynamicResource` : textes, sillon, canal, volets, tuiles, plaque, hachure du plancher et état « en attente ». Les quatre contrôles n'ont plus aucune couleur par défaut. Une garde statique fait échouer les tests si une couleur fixe revient. Un test de liaison vérifie les 4 vues sur les 15 thèmes. La galerie `--cadrans` applique le thème choisi dans sa liste. La géométrie n'a pas changé.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | RED : garde « aucune couleur littérale » (auto-test des regex, anti-mutisme 4 + 4) et liaison 4 vues × 15 thèmes, plus le rafraîchissement au changement de thème | b8635df |
| 2 | Contrôles : DP de pinceaux à `null`, `WaitBrush` en DP à la place de `WaitFill`, replis gris et helpers `Frozen`/`FrozenA` supprimés (`WithAlpha` conservé) | 6c60177 |
| 3 | Vues en `DynamicResource` (couleurs seulement), `GrainHatch` remplacé par `PlaqueHachure`, galerie thémée | 537e263 |

## Vérification

- RED confirmé : le projet de test ne compilait pas, faute de `WaitBrush` sur les 4 contrôles.
- `dotnet build src/Chronos/Chronos.csproj` en Debug et en Release : 0 avertissement, 0 erreur.
- Tests filtrés (garde, liaison, CadranBindingTests, GardeGestesCadranTests) : 40 sur 40 réussis.
- `dotnet test` (suite complète) : 1759 sur 1759 réussis.
- `git diff -U0` sur les vues : les lignes qui portent Width, Height, Margin, FontSize ou CornerRadius ne changent que par la couleur. Aucune fusion de `DesignTokens.xaml` dans les vues de cadran.

## Changements de rendu à surveiller

- **Marée, état « en attente » :** l'opacité passe de `0x5A` à `0x6E`, via `CadranAttente` qui est maintenant commun aux quatre cadrans.
- **Fusible :** le sillon passe sur `CadranTuile`.
- **Volets :** les tuiles 5H/7J passent sur `CadranTuile` et les volets éteints sur `Piste5h`. Les chiffres de la plaque passent sur `PlaqueTexte` (disque × 0,5) au lieu de `#161019`. La hachure du plancher passe sur `PlaqueHachure`, et le grain reste visible.
- **Marée :** le canal passe sur `FondCadran`, qui est translucide (E6).
- **Braises :** les cendres passent sur `TickMajeur`.

Ces valeurs sont à confirmer à la revue visuelle. La phase 40 redessinera la géométrie sur ces pinceaux.

## Écarts au plan

Aucun : le plan a été exécuté tel qu'écrit.

Remarques :
- Le test de liaison parcourt l'arbre logique et non l'arbre visuel. Les TextBlock masqués par `Visibility` sont donc vérifiés aussi.
- THM-04 reste ouverte : le plan 04 la complète (Historique).

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND: tests/Chronos.Tests/GardeCouleursCadransTests.cs, tests/Chronos.Tests/CadransThemeBindingTests.cs
- FOUND: b8635df, 6c60177, 537e263
