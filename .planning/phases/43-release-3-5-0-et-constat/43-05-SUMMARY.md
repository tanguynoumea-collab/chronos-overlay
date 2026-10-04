---
phase: 43-release-3-5-0-et-constat
plan: 05
subsystem: cadran Braises / release
tags: [VAL-07, gap_closure, braises, release]
requires: [43-03 (Chronos-v3.5.0.exe publié)]
provides:
  - anneau hebdo de Braises en 14 braises, 7 groupes de 2 (un jour par groupe, une braise par demi-journée)
  - Chronos-v3.5.0.exe reconstruit (même version), empreinte mise à jour
affects: [43-04 (constat utilisateur, point (d) Braises)]
tech-stack:
  added: []
  patterns: [garde de vue sur les paramètres de groupe (évite le repli uniforme silencieux de BraisesGeometrie)]
key-files:
  modified:
    - src/Chronos/Views/Cadrans/CadranBraisesView.xaml
    - tests/Chronos.Tests/BraisesGeometrieTests.cs
    - tests/Chronos.Tests/CadranBraisesTests.cs
    - tests/Chronos.Tests/CadransThemeBindingTests.cs
    - README.md
    - .zeus/DESIGN_PLAN_CYCLE2.md
    - docs/publish.md
    - .planning/phases/43-release-3-5-0-et-constat/43-CONSTAT.md
decisions:
  - "Pas angulaire hebdo 15,6° : ≈ 12 px centre à centre à R44, cohérent avec l'anneau 5 h"
metrics:
  duration: ~25 min
  completed: 2026-10-04
  tasks: 2
  files: 8
---

# Phase 43 Plan 05 : anneau hebdo de Braises en 7 groupes de 2 — Summary

L'anneau hebdo de Braises passe de 12 braises uniformes (1 braise = 14 h) à 14 braises en 7 groupes de 2 : un groupe par
jour, une braise par demi-journée, et le vide entre groupes marque les jours. Chronos-v3.5.0.exe a été reconstruit et
l'empreinte a été mise à jour.

## Pas angulaire retenu : 15,6°

- 12 px centre à centre au rayon 44 donnent 12/44 rad ≈ 15,6°. C'est l'écart que demandait le plan, et il est cohérent
  avec l'anneau 5 h.
- Secteur d'un jour : 360/7 ≈ 51,43°. Le vide entre deux jours fait 51,43 − 15,6 ≈ 35,8°, soit plus de 2 × 15,6° :
  la délimitation se lit sans effort.
- Le groupe (15,6°) tient dans son secteur, donc pas de repli uniforme. Midi tombe au milieu d'un vide (braises 0 et 13
  à ±17,9°).
- PipRadius 3,6 reste inchangé : avec un diamètre de 7,2 px pour 12 px entre centres, il reste 4,8 px de jour, donc les
  braises ne se touchent pas.
- Lecture du cas constaté : 5 j 8 h restants → round(128/168 × 14) = 11 braises allumées, soit 5 jours pleins et une
  demi-journée (contre 9/12 avant).

## Tests

- RED (8be6e4a). Une seule assertion échouait, et c'était celle attendue : la garde de vue lisait `Count` 12 au lieu
  de 14.
  - `BraisesGeometrieTests` : 7 groupes centrés, vide ≥ 2 × pas, symétrie autour de midi, lecture 128 h → 11,
    24 h → 2, 0 → 0, 168 h → 14.
  - `CadranBraisesTests` : vérifications par rendu. Rien à midi ni à la frontière 360/7. Une braise allumée par
    demi-journée, la dernière en demi-lueur. Garde de vue : second `EmberRingControl` à R44, Count 14, GroupSize 2,
    GroupPitch > 0, groupe tenant dans son secteur.
- GREEN (810d0ad). Le verrou de géométrie de `CadransThemeBindingTests` (hebdo 12 / 1 / 0) a été mis à jour à
  14 / 2 / 15,6. C'est une conséquence directe de la décision.

## Porte

- `dotnet build Chronos.sln -c Debug -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln -c Debug` : 2 233 réussis, 0 échec, 1 ignoré, total 2 234 (+10 par rapport à 43-03).
- `dotnet build Chronos.sln -c Release -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln -c Release` : 2 233 réussis, 0 échec, 1 ignoré, total 2 234.
- Gardes documentaires relancées après l'édition de publish.md : 20 / 20.

## L'exe reconstruit

| Champ | Valeur |
|-------|--------|
| Chemin | `C:\Users\Tanguy\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.5.0.exe` |
| Taille | 78 200 845 o (< 120 000 000) |
| FileVersion / ProductVersion / ProductName | 3.5.0.0 / 3.5.0 / Chronos (lus par `VersionInfo`, sans lancer) |
| SHA-256 | `cca57384af57dcd66575a7cad571c8974faecb7e56b592210c8c62c98d36f6f3` |
| Sortie de publish | `Chronos.exe` + `Chronos.pdb`, 0 DLL ; SHA-256 identique à la copie |
| Empreinte précédente | `3577e72a…` (78 199 851 o), barrée dans 43-CONSTAT.md, remplacée dans publish.md §2/§4 |
| git | ignoré (`/Chronos-v*.exe`) ; 8 exe à la racine |

Avant la copie, `Get-Process` et `tasklist` ont été consultés en lecture seule : aucun processus `Chronos*` ne tournait.
La copie a donc été faite. Aucun exe n'a été lancé et aucun processus n'a été arrêté.

## Écarts par rapport au plan

**1. [Rule 1 - Bug] Un verrou de test figeait l'anneau hebdo à 12**
- **Constaté pendant :** tâche 1 (GREEN).
- **Problème :** `CadransThemeBindingTests.Les_quatre_cadrans_suivent_chaque_theme` imposait `(44, 12, 3.6, 1, 0)`.
- **Correction :** valeur passée à `(44, 14, 3.6, 2, 15.6)`, commentaire mis à jour.
- **Commit :** 810d0ad.

Le test de rendu `Anneau_hebdo_sans_groupe_reste_uniforme` est remplacé par les trois tests hebdo groupés. Le test de
géométrie uniforme est conservé sous le nom `Sans_groupe_repartition_uniforme`, car il couvre le repli.

## Known Stubs

Aucun.

## Commits

- 8be6e4a — test(43-05) : anneau hebdo de Braises en 14 braises, 7 groupes de 2 (RED)
- 810d0ad — fix(43-05) : anneau hebdo de Braises en 14 braises, 7 groupes de 2 (un jour par groupe)
- 56e053f — release(43-05) : Chronos 3.5.0 reconstruit — anneau hebdo de Braises en 7 groupes de 2

Pas d'étiquette, pas de push. STATE.md, ROADMAP.md et REQUIREMENTS.md n'ont pas été modifiés.

## Self-Check: PASSED

- FOUND: Chronos-v3.5.0.exe (SHA-256 cca57384… recalculé)
- FOUND: 8be6e4a, 810d0ad, 56e053f
