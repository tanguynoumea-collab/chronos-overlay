---
phase: 40-cadrans-l-chelle-1-et-orientations
plan: 08
subsystem: réglages / galerie des cadrans
tags: [cadran, orientation, reglages, galerie, tdd, wpf, cad-02, cad-04]
requires: [40-06, 40-07]
provides:
  - "ReglagesWindow : carte « Orientation » (CarteOrientation) avec les puces BoutonOrientationHorizontale / BoutonOrientationVerticale liées à ChoisirOrientationCommand, visible pour Fusible, Marée et Volets"
  - "Style Puce : la sélection peut aussi venir de Tag (déclencheur sur Tag, RelativeSource Self)"
  - "Aperçu vivant testé à l'empreinte réelle : 190 × 66 puis 110 × 190"
  - "CadranGalleryWindow : huit tuiles Galerie* (Arcs, Braises, Fusible H/V, Marée V/H, Volets H/V), DesignTokens.xaml fusionné"
  - "CadranPreviewViewModel : IsModeEtendu, IsModeNormal, DayFraction, DayResetAngles, DaySubTickAngles (données d'échantillon déterministes)"
affects: []
tech-stack:
  added: []
  patterns: ["sélection d'une puce portée par Tag quand le DataContext n'est pas un élément de catalogue", "galerie : tokens fusionnés au niveau de la fenêtre, pinceaux du thème recopiés par-dessus"]
key-files:
  created: []
  modified:
    - src/Chronos/Views/Reglages/ReglagesWindow.xaml
    - src/Chronos/ViewModels/CadranPreviewViewModel.cs
    - src/Chronos/Views/CadranGalleryWindow.xaml
    - src/Chronos/Views/CadranGalleryWindow.xaml.cs
    - tests/Chronos.Tests/ReglagesWindowTests.cs
    - tests/Chronos.Tests/CadransOrientationBindingTests.cs
decisions:
  - "Le déclencheur Tag du style Puce utilise RelativeSource Self et non TemplatedParent. Dans ControlTemplate.Triggers, Self désigne déjà le bouton templaté. Avec TemplatedParent, la liaison ne résout rien et la puce ne s'allumait jamais."
  - "Dans le test de l'aperçu, la grille qui joue le cadran a UseLayoutRounding=false. Sinon, à 125 %, WPF arrondit 110 × 190 en 110,4 × 190,4 et le test dépend du DPI de la machine."
  - "Le test de thème de la galerie compare les couleurs, pas les instances, parce que BrushTokens() fabrique des pinceaux neufs à chaque appel. Il vérifie aussi qu'un changement de thème met à jour les pinceaux de la fenêtre."
  - "Dans la galerie, chaque vue est posée dans un pointillé qui épouse son empreinte (Rectangle sans hit-test dans une Grid dimensionnée par la vue). La boîte suit donc les tokens, sans taille codée en dur."
metrics:
  duration: "~20 min"
  completed: 2026-10-03
  tasks: 2
  files: 6
---

# Phase 40 Plan 08 : carte Orientation des réglages, aperçu à l'empreinte réelle, galerie à huit variantes — Summary

La section Apparence a maintenant une carte « Orientation » avec deux puces, Horizontal et Vertical. Elle apparaît juste sous « Mode étendu », seulement pour Fusible, Marée et Volets. Les puces exécutent `ChoisirOrientationCommand`, et celle de l'orientation courante est en surbrillance par son `Tag`.

L'aperçu vivant suivait déjà l'empreinte réelle ; le test le prouve désormais à 190 × 66 puis à 110 × 190.

La galerie `--cadrans` montre les huit variantes à l'échelle 1, Anneaux compris. `CadranPreviewViewModel` fournit l'anneau du jour d'Anneaux à partir d'un « maintenant » fixe et du curseur de temps 5 h.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | RED : carte Orientation, aperçu 190 × 66 puis 110 × 190 | 6c9bb89 |
| 1 | GREEN : carte Orientation + déclencheur Tag du style Puce | 1aa96c8 |
| 2 | RED : galerie à huit variantes, tokens et thème, propriétés d'Arcs | 04cb032 |
| 2 | GREEN : galerie 8 tuiles, DesignTokens fusionné, VM d'aperçu complété | 65ca585 |

## Vérification

- Filtre `ReglagesWindowTests|GardeTokensReglages|ReglagesBindingTests` : 56/56.
- Filtre `CadransOrientationBinding|CadransThemeBinding|ArgumentsDemarrage` : 47/47.
- `dotnet build Chronos.sln` en Debug et en Release : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln` : 1866/1866.
- Critères grep :
  - `CarteOrientation` = 1 ;
  - `x:Static svc:OrientationCadran.*` = 2 ;
  - `new Rect(0, 0, 110, 190)` présent, `new Rect(0, 0, 170, 170), pinceau.Viewbox` = 0 ;
  - `ChoisirOrientation` est absent de la liste `attendu` de `Chaque_commande_de_l_ancienne_fenetre_est_liee_dans_la_nouvelle` (Pitfall 10) ;
  - huit `Galerie*`, sept `Orientation="…"` (six vues plus l'en-tête), `DesignTokens.xaml` = 1, `DayResetAngles = DayTimeline.ResetAngles` = 1 ;
  - un seul critère n'est pas rempli : `TemplatedParent}}" Value="True"` = 0, voir l'écart 1.

## Écarts par rapport au plan

**1. [Rule 1 - Bug] Le déclencheur Tag avec RelativeSource TemplatedParent ne s'allumait jamais**
- **Trouvé pendant :** tâche 1 (GREEN). Le test `Cliquer_vertical_…` donnait une bordure transparente, la police Normal et le premier plan Ink2 alors que `Tag` valait true. Le même résultat est sorti avec une valeur typée `sys:Boolean`.
- **Cause :** dans `ControlTemplate.Triggers`, la source d'un `DataTrigger` est déjà le contrôle templaté. `TemplatedParent` désigne alors le parent templaté du bouton, qui n'existe pas.
- **Correctif :** `{Binding Tag, RelativeSource={RelativeSource Self}}" Value="True"`, avec un commentaire qui l'explique. Le critère grep du plan sur `TemplatedParent` n'est donc pas rempli, mais le comportement attendu l'est et il est testé.
- **Commit :** 1aa96c8.

**2. [Rule 1 - Bug de test] Arrondi DPI dans le test de l'aperçu**
- La grille d'essai passe en `UseLayoutRounding=false` pour garder des dimensions exactes en DIP (commit 6c9bb89).
- Le test de l'aperçu était déjà vert en RED, comme le plan le prévoyait : seul le test changeait.

**3. [Rule 1 - Bug de test] `Assert.Same` sur un pinceau de BrushTokens()**
- `BrushTokens()` renvoie des instances neuves à chaque appel, donc `Assert.Same` ne peut pas passer.
- Le test compare maintenant la couleur et ajoute la vérification du changement de thème (commit 65ca585).

## Known Stubs

Aucun. La galerie utilise des données d'échantillon, ce qui est voulu : c'est un outil de revue visuelle, jamais branché sur une source Claude.

## Reste à faire hors agent

La revue visuelle manuelle reste à faire par l'utilisateur : `Chronos.exe --cadrans` et l'overlay réel (voir 40-VALIDATION.md, § Manual-Only).

## Self-Check: PASSED
- Fichiers modifiés présents ; commits 6c9bb89, 1aa96c8, 04cb032 et 65ca585 présents dans `git log`.
