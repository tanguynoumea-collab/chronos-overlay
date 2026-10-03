---
phase: 40-cadrans-l-chelle-1-et-orientations
plan: 03
subsystem: controls / cadrans
tags: [cadran, orientation, rendu, tdd, wpf]
requires: [40-02]
provides:
  - "DP Orientation (AffectsRender) sur FuseBar (Horizontal), TideColumn (Vertical), FlapRow (Horizontal)"
  - "OnRender des trois contrôles dessiné par GeometrieCadrans, sans rembourrage interne"
  - "Fusible : étincelle au front (NotchBrush, opacité 0,9) à la place de l'encoche"
  - "Marée horizontale : ligne d'eau ondulée (LigneDEauOndulee), pointillée en plancher"
  - "Volets : empilés en vertical, écart 2,5"
affects: [40-04, 40-05]
tech-stack:
  added: []
  patterns: ["contrôle OnRender = consommateur de géométrie pure par axe", "preuve par rendu RenderTargetBitmap + lecture de pixels"]
key-files:
  created:
    - tests/Chronos.Tests/ControlesCadransOrientationTests.cs
  modified:
    - src/Chronos/Controls/Cadrans/FuseBar.cs
    - src/Chronos/Controls/Cadrans/TideColumn.cs
    - src/Chronos/Controls/Cadrans/FlapRow.cs
decisions:
  - "L'étincelle utilise NotchBrush (thémé, TextePrincipal) et non le #FFF3D6 de la maquette, interdit par la garde de la phase 39"
  - "Le test « plancher » ne vise que le Fusible et la Marée : FlapRow n'a pas d'état Estimated (la vue porte le plancher)"
  - "La ligne d'eau se reconnaît à G et B > 120 (trait de 1,6 px à 85 % anticrénelé sur deux pixels), pas à un seuil > 200"
metrics:
  duration: "~10 min"
  completed: 2026-10-03
  tasks: 2
  files: 4
---

# Phase 40 Plan 03 : contrôles de cadran à deux sens — Summary

`FuseBar`, `TideColumn` et `FlapRow` ont maintenant une DP `Orientation`. Leurs défauts reprennent le sens historique : Horizontal, Vertical, Horizontal.
Leur `OnRender` ne fait plus que consommer `GeometrieCadrans` (plan 02), sans rembourrage interne. En vertical, le Fusible brûle de haut en bas
et une étincelle marque le front. En horizontal, la Marée part de la gauche avec une ligne d'eau ondulée. En vertical, les Volets s'empilent
et s'allument depuis le haut. Les états « en attente » et « plancher » marchent dans les deux sens, toujours sans couleur littérale.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | RED : 14 cas de rendu (8 faits + 6 théories attente + 4 théories plancher) | 3a82ba5 |
| 2 | GREEN : DP Orientation + OnRender par GeometrieCadrans dans les trois contrôles | 493094a |

## Vérification

- RED : le build du projet de test échouait sur `Orientation` (CS0117 / CS1061) pour les trois contrôles.
- Filtre `ControlesCadransOrientation|GardeCouleursCadrans|CadransThemeBinding|CadranBindingTests` : 54/54.
- `dotnet build Chronos.sln` : 0 avertissement, 0 erreur.
- `dotnet test` : 1828/1828.
- Critères grep : `OrientationProperty` ≥ 2 par fichier, `new FrameworkPropertyMetadata(Orientation.Vertical` = 1 (TideColumn),
  `GeometrieCadrans.Fusible` = 1, `DrawEllipse` = 1, `LigneDEauOndulee` = 1, `GeometrieCadrans.Volets|VoletsAllumes` = 2,
  `pad = |double gap = 3` = 0, 4 fichiers `.cs` dans `Controls/Cadrans`.

## Écarts par rapport au plan

**1. [Rule 1 - Bug de test] Seuil de la ligne d'eau trop strict**
- **Trouvé pendant :** tâche 2 (GREEN)
- **Problème :** le trait de 1,6 px à alpha 0,85 est anticrénelé sur deux pixels. Aucun pixel ne dépasse donc R, G, B > 200 (environ 175-185).
- **Correction :** prédicat `EstLigneDEau` (G et B > 120). Il distingue toujours la ligne de la lumière rouge (G = 0) et de la piste (0x20).
  Le test d'ondulation (ligne décalée vers la droite au quart de la hauteur) reste discriminant : une ligne droite à x = 51 ne couvrirait pas x ≥ 52.
- **Fichier :** tests/Chronos.Tests/ControlesCadransOrientationTests.cs (inclus dans 493094a)

**2. Ajouts aux tests, sans changer le contrat**
- Étincelle aussi vérifiée en horizontal, et ondulation vérifiée à y = h/4.
- La théorie « plancher » est limitée au Fusible et à la Marée, car FlapRow n'a pas de DP `Estimated`.

**3. Doc des classes**
- Les `<see cref>` pointent vers `GeometrieCadrans` (classe) et non vers ses méthodes. Les critères grep comptent ainsi exactement un site d'appel.

## Exigences

CAD-03 n'est pas cochée : ce plan livre les contrôles, mais il reste à poser `Orientation="Vertical"` dans les vues à deux gabarits (plans 40-04 et 40-05).

## Known Stubs

Aucun. En attendant les plans 04-05, les vues actuelles gardent leur ancien gabarit. Seul leur rembourrage visuel change (pad 4 / 2 / top 3 → 0), comme prévu par le plan.

## Self-Check: PASSED

- FOUND : tests/Chronos.Tests/ControlesCadransOrientationTests.cs, src/Chronos/Controls/Cadrans/{FuseBar,TideColumn,FlapRow}.cs
- FOUND : commits 3a82ba5, 493094a
