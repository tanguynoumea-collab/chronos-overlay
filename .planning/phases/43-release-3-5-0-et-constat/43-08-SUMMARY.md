---
phase: 43-release-3-5-0-et-constat
plan: 08
subsystem: cadrans / release
tags: [VAL-07, gap_closure, cadrans, braises, release]
requires: [43-07 (Chronos-v3.5.0.exe reconstruit, flèche de Braises retirée)]
provides:
  - anneau 5 h de Braises en 25 braises de 12 min, 5 groupes de 5 (un groupe par heure)
  - preuve au rendu de la VUE que les braises s'allument dans le sens horaire depuis midi (5 h et hebdo)
  - Chronos-v3.5.0.exe reconstruit (même version), empreinte mise à jour
affects: [43-04 (constat utilisateur)]
tech-stack:
  added: []
  patterns: [test de rendu de la vue avec positions attendues calculées sans le code testé (pas de preuve circulaire)]
key-files:
  created: []
  modified:
    - src/Chronos/Views/Cadrans/CadranBraisesView.xaml
    - src/Chronos/Rendering/BraisesGeometrie.cs
    - tests/Chronos.Tests/CadranBraisesTests.cs
    - tests/Chronos.Tests/BraisesGeometrieTests.cs
    - tests/Chronos.Tests/CadransThemeBindingTests.cs
    - tests/Chronos.Tests/RempliConsommeTests.cs
    - README.md
    - .zeus/DESIGN_PLAN_CYCLE2.md
    - docs/publish.md
    - .planning/phases/43-release-3-5-0-et-constat/43-CONSTAT.md
decisions:
  - "Anneau 5 h : Count 25, GroupSize 5, GroupPitch 11° (≈ 12,7 px à R66), vide de 28° entre groupes ; PipRadius 4,0 conservé"
  - "Sens de remplissage : aucun défaut dans le code, rien corrigé — la preuve au rendu passe"
metrics:
  duration: ~25 min
  completed: 2026-10-05
  tasks: 2
  files: 10
---

# Phase 43 Plan 08 : anneau 5 h de Braises en 5 groupes de 5 — Summary

Ce qui a été retenu :
- l'anneau extérieur de Braises compte maintenant **25 braises de 12 min, en 5 groupes de 5** (un groupe par heure) ;
- le pas dans un groupe est de 11°, et le vide entre deux groupes de 28° ;
- midi tombe au milieu d'un vide ;
- les pastilles gardent un rayon de 4,0. Elles ne se touchent pas : 12,65 px de centre à centre, pour un minimum de 10.

Chronos-v3.5.0.exe a été reconstruit.

## Sens de remplissage : pas de défaut, rien corrigé

Le test de rendu **passe**. Le code remplit déjà dans le sens horaire depuis midi, et aucune correction de sens n'a été
faite. Avant la modification, le RED du sens échouait seulement parce que les braises n'étaient pas encore à leur
nouvelle place (anneau en 20/4). Le sens n'était pas en cause. Le rendu hebdo, dont la géométrie ne change pas,
passait déjà avant tout changement.

Ce que vérifient les trois tests `Rendu_vue_*` de `CadranBraisesTests` :
- **Ce qui est rendu.** C'est la vue `CadranBraisesView` complète (pas le contrôle seul), avec un
  `CadranPreviewViewModel`. Le temps consommé passe par `FiveTimePct` / `SevenTimePct`, donc par la vraie chaîne
  `FractionElapsed`. Le rendu est fait par `RenderTargetBitmap` en 170 × 170.
- **Où les braises sont attendues.** Leurs centres sont calculés **sans `BraisesGeometrie`** : groupe centré dans son
  secteur, puis x = 85 + r·sin(θ) et y = 85 − r·cos(θ). Le test affirme aussi que le 1er groupe est en haut à droite
  (x > 85, y < 85) et que le dernier groupe est à gauche de midi (x < 85).
- **Comment un pixel est classé.** On lit sa couleur non prémultipliée (alpha ≥ 100) et on la compare aux pinceaux
  effectifs du contrôle : quota (#E0A43A) ou cendre (#2A2932).
- **Les cas vérifiés :**
  - 5 h à 20 % consommé : les 5 braises du groupe 0-72° (côté droit du haut) sont allumées, les groupes 1 à 4 sont
    éteints, y compris le groupe 288-360° juste à gauche de midi ;
  - 5 h à 60 % : les groupes 0, 1 et 2 sont allumés, les groupes 3 et 4 éteints ;
  - hebdo à 2 jours consommés : les groupes 0 et 1 (à droite de midi) sont allumés, les groupes 2 à 6 éteints.

Aucun `FlowDirection`, `ScaleTransform` ou miroir ne touche la vue : `MainWindow.xaml` l'héberge directement, l.47.
En production, `WindowGaugeViewModel.Interpolate` calcule `FractionElapsed = 1 − FractionRemaining`. Le « grignotage
anti-horaire » observé correspond exactement à l'ancien comportement d'avant 43-06 (liaison `FractionRemaining` : les
braises allumées reculent vers midi). C'est une **hypothèse non vérifiée** : un exe antérieur aurait encore tourné au
moment de l'observation. Je n'ai pas pu reproduire le défaut sur le code actuel.

## Tests (RED e422ab6 : 4 échecs attendus, puis GREEN 3ba2ed3)

- `CadranBraisesTests` :
  - nouvelle garde de vue : Count 25, GroupSize 5, pas > 0, le groupe tient dans 72° ;
  - trois rendus de sens, décrits plus haut ;
  - tests de contrôle passés à 25/5/11 (« première braise à 14° », « à moitié 13 braises »).
- `BraisesGeometrieTests` :
  - angles 14 / 25 / 36 / 47 / 58 / 86 / 346 ;
  - groupes centrés, vide ≥ 2 × pas, symétrie autour de midi ;
  - nouveau test : braises distinctes au rayon 66.
- `CadransThemeBindingTests` : (66, 25, 4.0, 5, 11.0).
- `RempliConsommeTests` : test Braises passé à 25/5/11, avec 20 % consommé.
- `AutomateGesteTests` : non modifié, son 13,2 est un seuil de geste sans rapport avec Braises.
- Échecs du RED, tous attendus :
  - garde de vue 5 h ;
  - `CadransThemeBindingTests` ;
  - deux rendus 5 h.

## Porte

- `dotnet build Chronos.sln -c Debug -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln` (Debug) : 2 255 réussis, 0 échec, 1 ignoré, total 2 256 (+6 par rapport à 43-07).
- `dotnet build Chronos.sln -c Release -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln -c Release` : 2 255 réussis, 0 échec, 1 ignoré, total 2 256. Relancé après l'édition de
  publish.md et 43-CONSTAT.md : même résultat.
- `grep 'Count="25"' CadranBraisesView.xaml` : 1 occurrence. `Count="20"` : aucune.

## L'exe reconstruit

| Champ | Valeur |
|-------|--------|
| Chemin | `C:\Users\Tanguy\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.5.0.exe` |
| Taille | 78 200 214 o (< 120 000 000) |
| FileVersion / ProductVersion / ProductName | 3.5.0.0 / 3.5.0 / Chronos (lus par `VersionInfo`, sans lancer l'exe) |
| SHA-256 | `4bab93a689cef51f6b5a0d2948812dcce3868990f3d19dd4668da4d31c6815e3` |
| Sortie de publish | `Chronos.exe` + `Chronos.pdb`, 0 DLL ; SHA-256 identique à la copie |
| Empreinte précédente | `041f1f07…` (78 200 218 o) : barrée dans 43-CONSTAT.md, remplacée dans publish.md §2 et §4 |
| git | ignoré (`/Chronos-v*.exe`) ; 8 exe à la racine |

Avant la copie, `Get-Process` (0 processus `Chronos*` sur 583) et `tasklist` (aucune tâche) ont été consultés en lecture
seule. `Chronos-v3.5.0` ne tournait pas, donc la copie a été faite. Aucun exe n'a été lancé et aucun processus n'a été
arrêté.

## Écarts par rapport au plan

- `BraisesGeometrie.cs` : seuls les commentaires ont changé (exemple chiffré 20/4/13,2 remplacé par 25/5/11). Aucun
  changement de comportement.
- Aucune correction de sens : la preuve au rendu passe (voir plus haut).

## Known Stubs

Aucun.

## Commits

- e422ab6 — test(43-08) : anneau 5 h de Braises en 5 groupes de 5 et sens horaire prouvé au rendu de la vue (RED)
- 3ba2ed3 — fix(43-08) : anneau 5 h de Braises en 25 braises, 5 groupes de 5
- d1080e1 — release(43-08) : Chronos 3.5.0 reconstruit, anneau 5 h de Braises en 5 groupes de 5

Pas d'étiquette, pas de push. STATE.md, ROADMAP.md et REQUIREMENTS.md n'ont pas été modifiés.

## Self-Check: PASSED

- FOUND: Chronos-v3.5.0.exe (SHA-256 4bab93a6… recalculé)
- FOUND: e422ab6, 3ba2ed3, d1080e1
