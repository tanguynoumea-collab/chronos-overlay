---
phase: 41-braises
plan: 01
subsystem: cadrans
tags: [wpf, braises, onrender, theme, tdd]
requires:
  - phase: 39 (TickReset thémé, garde des couleurs)
  - phase: 40 (empreinte 170 × 170 sur tokens, 5 vues / 4 contrôles)
provides:
  - Rendering/BraisesGeometrie.Angle (fonction pure, groupes centrés, repli uniforme)
  - EmberRingControl.GroupSize / GroupPitch (DP rétrocompatibles, défauts 1 / 0)
  - CadranBraisesView : anneau 5 h à 20 braises en 5 groupes + FlecheReset (TickReset)
affects: [41-02 (centre de la vue Braises), 42 (zones de geste / silhouette)]
tech-stack:
  added: []
  patterns: [géométrie pure dans Rendering/ testée sans STA, preuve par RenderTargetBitmap]
key-files:
  created:
    - src/Chronos/Rendering/BraisesGeometrie.cs
    - tests/Chronos.Tests/BraisesGeometrieTests.cs
    - tests/Chronos.Tests/CadranBraisesTests.cs
  modified:
    - src/Chronos/Controls/Cadrans/EmberRingControl.cs
    - src/Chronos/Views/Cadrans/CadranBraisesView.xaml
    - tests/Chronos.Tests/CadransThemeBindingTests.cs
decisions:
  - "Braises : angle d'une braise centralisé dans Rendering/BraisesGeometrie.Angle, utilisé par les deux boucles (attente et nominale) d'EmberRingControl"
  - "Flèche de reset Braises dessinée en XAML (Canvas FlecheReset, DynamicResource TickReset), IsHitTestVisible=False ; les zones de geste restent à la phase 42"
metrics:
  duration: ~15 min
  completed: 2026-10-03
  tasks: 2
  files: 6
requirements: [BRA-01]
---

# Phase 41 Plan 01 : Braises groupées et flèche de reset — Summary

L'anneau 5 h de Braises passe à 20 braises de 15 min en 5 groupes d'une heure (16,2° / 29,4° / 42,6° / 55,8°, puis +72° par
groupe ; midi au milieu d'un vide de 32,4°) via une fonction pure `BraisesGeometrie.Angle`, et une flèche fixe à midi en
`TickReset` du thème marque la ligne d'arrivée du reset.

## Tâches

| # | Tâche | Commits |
|---|-------|---------|
| 1 | BraisesGeometrie pure + DP GroupSize/GroupPitch (deux boucles) | `25926a9` (RED), `2ca9fb1` (GREEN) |
| 2 | Vue Braises : 20 braises groupées + FlecheReset TickReset | `a9c8656` (RED), `bf0c80c` (GREEN) |

## Ce qui a été livré

- `Rendering/BraisesGeometrie.cs` : `Angle(i, count, groupSize, pasDansGroupe)`. Groupe centré dans son secteur ; repli
  uniforme si groupSize ≤ 1, count non multiple, pas ≤ 0 ou groupe trop large ; count 0 → n = 1. Placée hors de
  `Controls/Cadrans` (la garde compte toujours 4 contrôles et 5 vues).
- `EmberRingControl` : `GroupSize` (int, 1) et `GroupPitch` (double, 0), `AffectsRender`. Les deux boucles appellent
  `BraisesGeometrie.Angle(i, n, GroupSize, GroupPitch)`, donc l'état « en attente » utilise les mêmes angles que le nominal.
  Rien d'autre n'a changé : `lit`, demi-lueur, pointillé, cendre 0,82, attente 0,7.
- `CadranBraisesView.xaml` : 5 h `Count="20" PipRadius="4.0" GroupSize="4" GroupPitch="13.2"` ; hebdo inchangé (12, R44,
  3,6, sans groupe). Ajout de `Canvas x:Name="FlecheReset"` (triangle 80,5 / 90,5 / 85,12 et filet x=85 de y 13 à 25,
  épaisseur 1,2, opacité 0,55), en `{DynamicResource TickReset}`, sans animation. Le centre n'a pas été modifié (plan 02).
- Tests : 4 cas purs d'angles. 4 preuves par rendu : braise à 16,2°, alpha 0 à 0° et à 72°, 10 braises allumées à 0,5,
  attente aux mêmes angles, hebdo uniforme. Liaison de la flèche et des paramètres d'anneau pour les 15 thèmes. Structure
  de la flèche vérifiée (points, empreinte, non cliquable, présente sans données). Absence de Storyboard/Animation vérifiée.

## Vérification

- Filtres des deux tâches : verts (41, puis 62 tests).
- `dotnet build Chronos.sln` : 0 avertissement, 0 erreur.
- `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` : 1881 / 1881 verts.
- Critères grep d'acceptation : tous conformes (2 appels `Angle(i, n, GroupSize, GroupPitch)`, 0 `i * 360.0 / n`, 4 `.cs`
  dans `Controls/Cadrans`, 2 `DynamicResource TickReset`, aucun `Count="16"`, aucune couleur, aucune animation).

## À signaler pour la phase 42 (Pitfall 8)

**La flèche dépasse du disque r 74.** Le triangle de `FlecheReset` (y 5→12) est presque entièrement hors du disque r 74
centré en (85,85), qui commence à y = 11. Il est en `IsHitTestVisible="False"`, il ne capte donc aucun clic, mais **il peint
des pixels hors de ce disque**. Un test de silhouette par `RenderTargetBitmap` (GST-01) doit en tenir compte : exclure la
flèche ou tolérer cette zone au-dessus de midi.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug, test] `IsVisible` inutilisable hors fenêtre**
- **Trouvé pendant :** Task 2 (GREEN)
- **Problème :** dans le test de la flèche, `Assert.True(fleche.IsVisible)` restait faux. Une vue montée dans un `Border`
  sans `PresentationSource` n'est jamais `IsVisible`, quelle que soit sa structure.
- **Correctif :** le test vérifie `Visibility == Visible` sur toute la chaîne d'ancêtres visuels de la flèche jusqu'à l'hôte.
- **Fichier :** tests/Chronos.Tests/CadranBraisesTests.cs. **Commit :** `bf0c80c`

**2. [Rule 3 - Blocage] Ambiguïté `Path`**
- Avec `System.Windows.Shapes`, `Path` est ambigu dans le test texte (Storyboard/Animation). Il a été qualifié en
  `System.IO.Path` avant le commit RED.

Aucune autre déviation. Le dossier `.claude/skills` est absent du projet, aucun skill local n'a donc été chargé.

## Known Stubs

Aucun.

## Self-Check: PASSED
