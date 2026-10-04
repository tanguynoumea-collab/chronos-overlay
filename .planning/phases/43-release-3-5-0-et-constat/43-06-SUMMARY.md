---
phase: 43-release-3-5-0-et-constat
plan: 06
subsystem: cadrans / release
tags: [VAL-07, gap_closure, cadrans, release]
requires: [43-05 (Chronos-v3.5.0.exe reconstruit, anneau hebdo de Braises en 7 groupes de 2)]
provides:
  - une seule règle sur les 5 cadrans et les 8 variantes : partie remplie / allumée = temps CONSOMMÉ, pleine au reset
  - Chronos-v3.5.0.exe reconstruit (même version), empreinte mise à jour
affects: [43-04 (constat utilisateur)]
tech-stack:
  added: []
  patterns: [garde de vue sur les liaisons Fraction (nombre exact attendu par vue, jamais FractionRemaining)]
key-files:
  created:
    - tests/Chronos.Tests/RempliConsommeTests.cs
  modified:
    - src/Chronos/Views/Cadrans/CadranBraisesView.xaml
    - src/Chronos/Views/Cadrans/CadranFusibleView.xaml
    - src/Chronos/Views/Cadrans/CadranMareeView.xaml
    - src/Chronos/Views/Cadrans/CadranVoletsView.xaml
    - src/Chronos/Rendering/GeometrieCadrans.cs
    - src/Chronos/Controls/Cadrans/FuseBar.cs
    - src/Chronos/Controls/Cadrans/TideColumn.cs
    - src/Chronos/Controls/Cadrans/FlapRow.cs
    - src/Chronos/Controls/Cadrans/EmberRingControl.cs
    - tests/Chronos.Tests/GeometrieCadransTests.cs
    - tests/Chronos.Tests/ControlesCadransOrientationTests.cs
    - README.md
    - .zeus/DESIGN_PLAN_CYCLE2.md
    - docs/publish.md
    - .planning/phases/43-release-3-5-0-et-constat/43-CONSTAT.md
decisions:
  - "Sens de remplissage unique : gauche → droite, bas → haut, horaire depuis midi ; plein au reset"
  - "Fusible horizontal inversé (cordon depuis la gauche) ; Marée et Volets verticaux remplis depuis le bas"
metrics:
  duration: ~30 min
  completed: 2026-10-04
  tasks: 2
  files: 16
---

# Phase 43 Plan 06 : rempli = temps consommé sur tous les cadrans — Summary

Une seule règle désormais : la partie remplie ou allumée = temps consommé de la fenêtre (5 h et hebdo), vide au début,
pleine au reset, vide si le reset est inconnu. Braises, Fusible, Marée et Volets lient `FractionElapsed`, comme les
Anneaux depuis VIS-01. Couleur = quota et état « en attente » inchangés. Chronos-v3.5.0.exe reconstruit.

## Sens retenu par contrôle

| Contrôle | Horizontal | Vertical | Changement de géométrie | Pourquoi |
|----------|-----------|----------|-------------------------|----------|
| Braises (`EmberRingControl`) | — | — | aucun | Les braises s'allumaient déjà depuis l'indice 0 (midi), sens horaire : avec `FractionElapsed`, elles progressent de midi vers la flèche et l'anneau est plein au reset. La dernière allumée garde la demi-lueur (braise en cours). |
| Fusible (`FuseBar`) | cordon depuis la **gauche** (avant : à droite du front) | cordon depuis le **bas** | horizontal seulement | Lié tel quel, le cordon consommé aurait grandi de droite à gauche, à rebours de la lecture. Le vertical montait déjà du bas : seule la sémantique change (le front s'élève vers le haut au reset). Le trait « plancher » va maintenant du départ au front. |
| Marée (`TideColumn`) | lumière depuis la gauche | lumière depuis le **bas** (avant : depuis le haut) | vertical seulement | Lié tel quel, la marée aurait « descendu » du haut. Elle monte maintenant du bas (marée montante), la ligne d'eau à h − h·f. |
| Volets (`FlapRow`) | allumés depuis la gauche | allumés depuis le **bas** (avant : depuis le haut) | vertical seulement | Cohérent avec Fusible et Marée verticaux. Nouveau point de test pur `GeometrieCadrans.VoletAllume(i, n, lit, axe)`. |

`GeometrieCadrans.Volets` garde son ordre (haut → bas). C'est le choix du volet allumé qui dépend de l'axe.

## Tests

- RED (2964068) : 21 échecs, tous attendus.
  - Nouveau `RempliConsommeTests`, avec deux gardes de vue.
    - Nombre exact de liaisons `Fraction` par vue (Arcs 2, Braises 2, Fusible 4, Marée 4, Volets 4), toutes sur
      `FractionElapsed`.
    - Aucun `FractionRemaining` dans `Views/Cadrans/*.xaml`.
  - Toujours dans `RempliConsommeTests` : la géométrie des sens, et un rendu par famille à 25 % (ou un tiers)
    consommé, où la part remplie vaut 25 % et non 75 %. Braises et Anneaux passaient déjà.
  - `VoletAllume` a été introduit dans le commit RED avec le comportement d'origine, pour que le RED échoue sur
    l'assertion et non à la compilation.
- Tests existants adaptés (ils figeaient l'ancien sens) :
  - `GeometrieCadransTests` :
    - `Fusible_horizontal_le_cordon_restant_est_a_droite` devient `…_consomme_part_de_la_gauche`.
    - `Fusible_vertical_brule_de_haut_en_bas` devient `…_consomme_monte_du_bas` (ordre du grain inversé).
    - `Fusible_plein_…` : front à l'arrivée.
    - `Fusible_vide_ou_NaN_…` : front horizontal à 0.
    - `Maree_verticale_la_lumiere_part_du_haut` devient `…_monte_du_bas`.
  - `ControlesCadransOrientationTests` :
    - `Fusible_horizontal_le_cordon_reste_a_droite` devient `…_part_de_la_gauche`.
    - `Fusible_vertical_…` : libellés seulement.
    - `Maree_verticale_inchangee_lumiere_par_le_haut` devient `…_lumiere_par_le_bas`.
    - `Volets_verticaux_allumes_en_haut` devient `…_depuis_le_bas`.
    - `Le_plancher_reste_visible_…` : pixels de mesure déplacés dans la nouvelle zone remplie (Fusible H (28, 13),
      Marée V (21, 90)).
- Aucun test de vue ne figeait `FractionRemaining` dans le XAML.
- GREEN (7bca918) : suite verte. Les gardes de thème, de tokens et de silhouettes sont intactes.

## Porte

- `dotnet build Chronos.sln -c Debug -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln -c Debug` : 2 249 réussis, 0 échec, 1 ignoré, total 2 250 (+16 par rapport à 43-05).
- `dotnet build Chronos.sln -c Release -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln -c Release` : 2 249 réussis, 0 échec, 1 ignoré, total 2 250.
- Gardes documentaires relancées après l'édition de publish.md : 20 / 20.
- `grep FractionRemaining src/Chronos/Views/Cadrans/` : aucune occurrence.

## L'exe reconstruit

| Champ | Valeur |
|-------|--------|
| Chemin | `C:\Users\Tanguy\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.5.0.exe` |
| Taille | 78 200 158 o (< 120 000 000) |
| FileVersion / ProductVersion / ProductName | 3.5.0.0 / 3.5.0 / Chronos (lus par `VersionInfo`, sans lancer) |
| SHA-256 | `adea832ff2dcf63244db893ce4526e2a0c4c26e5e1ed9976716cc1ede21cb521` |
| Sortie de publish | `Chronos.exe` + `Chronos.pdb`, 0 DLL ; SHA-256 identique à la copie |
| Empreinte précédente | `cca57384…` (78 200 845 o), barrée dans 43-CONSTAT.md, remplacée dans publish.md §2/§4 |
| git | ignoré (`/Chronos-v*.exe`) ; 8 exe à la racine |

Avant la copie, `Get-Process` (0 processus `Chronos*` sur 577) et `tasklist` ont été consultés en lecture seule :
`Chronos-v3.5.0` ne tournait pas, donc la copie a été faite. Aucun exe n'a été lancé et aucun processus n'a été arrêté.

## Écarts par rapport au plan

Aucun sur le fond. Le plan prévoyait d'ajuster le sens « si un contrôle devient incohérent » : c'est ce qui a été
fait, pour trois cas (Fusible horizontal, Marée verticale, Volets verticaux), comme le détaille le tableau ci-dessus.

## Known Stubs

Aucun.

## Commits

- 2964068 — test(43-06) : rempli = temps consommé sur les 8 variantes (RED)
- 7bca918 — fix(43-06) : rempli = temps consommé sur tous les cadrans
- df5b54c — release(43-06) : Chronos 3.5.0 reconstruit — rempli = temps consommé sur tous les cadrans

Pas d'étiquette, pas de push. STATE.md, ROADMAP.md et REQUIREMENTS.md n'ont pas été modifiés.

## Self-Check: PASSED

- FOUND: Chronos-v3.5.0.exe (SHA-256 adea832f… recalculé)
- FOUND: tests/Chronos.Tests/RempliConsommeTests.cs
- FOUND: 2964068, 7bca918, df5b54c
