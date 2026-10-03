---
phase: 40-cadrans-l-chelle-1-et-orientations
plan: 02
subsystem: rendering / placement
tags: [cadran, géométrie, orientation, recalage, tdd]
requires: [40-01]
provides:
  - "GeometrieCadrans.Fusible / Maree / LigneDEauOndulee / GrainMaree / Volets / VoletsAllumes (fonctions pures par axe, Rendering)"
  - "record structs GeometrieFusible, GeometrieMaree ; constantes SillonFusible 5,4, EcartVolets 2,5, AmplitudeOndulation 3, PasGrain 4"
  - "CornerSnap.RecalerSurCoin(coin, fenetre, travail, marge) : coin imposé + bornage à la zone de travail"
affects: [40-03, 40-07]
tech-stack:
  added: []
  patterns: ["géométrie pure par axe consommée par les contrôles OnRender", "recalage borné sur coin imposé"]
key-files:
  created:
    - src/Chronos/Rendering/GeometrieCadrans.cs
    - tests/Chronos.Tests/GeometrieCadransTests.cs
  modified:
    - src/Chronos/Placement/CornerSnap.cs
    - tests/Chronos.Tests/CornerSnapTests.cs
decisions:
  - "La fraction du Fusible est la part restante : front à w − w·f (H) ou h − h·f (V), cordon du front jusqu'au bout (droite / bas)"
  - "Seul le test de la ligne d'eau (Geometry WPF) est en [WpfFact] ; le reste est en [Fact] pur"
  - "RecalerSurCoin ne lit jamais la position courante : le coin vient de l'appelant (coin courant du contrôleur)"
metrics:
  duration: "~8 min"
  completed: 2026-10-03
  tasks: 2
  files: 4
---

# Phase 40 Plan 02 : géométrie pure par axe et recalage sur coin imposé — Summary

Socle pur des cadrans rectangulaires : `GeometrieCadrans` calcule Fusible (cordon à droite en H, brûle de haut en bas en V,
étincelle de rayon th/2 + 1,5), Marée (lumière depuis la gauche / le haut, ligne d'eau ondulée en deux Bézier quadratiques
d'amplitude 3, grain perpendiculaire à l'axe) et Volets (6 volets séparés de 2,5, allumés par Round loin de zéro) ;
`CornerSnap.RecalerSurCoin` garde le coin imposé et borne la fenêtre à la zone de travail.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | RED : 17 méthodes de test GeometrieCadrans + 3 de recalage (8 InlineData × 3 zones × 7 empreintes) | 9e29cbb |
| 2 | GREEN : `Rendering/GeometrieCadrans.cs` + `CornerSnap.RecalerSurCoin` | e9d08ca |

## Vérification

- RED : le build du projet de test échouait sur `GeometrieCadrans` (CS0103) et `RecalerSurCoin` (CS0117).
- Filtre `GeometrieCadrans|CornerSnap` : 47/47.
- `dotnet build Chronos.sln` : 0 avertissement, 0 erreur.
- `dotnet test` : 1811/1811.
- Critères grep : signature `Fusible(Size taille, Orientation axe, double fraction, double epaisseur)`, 2 `QuadraticBezierSegment`,
  `EcartVolets = 2.5`, signature `RecalerSurCoin`, 0 « System.Windows » dans CornerSnap.cs.

## Écarts par rapport au plan

Aucun : le plan a été exécuté tel qu'il était écrit. En plus, le test de recalage vérifie que la marge est exacte sur les deux
bords du coin imposé, comme le demandait la puce de comportement.

## Exigences

CAD-02 et CAD-03 ne sont pas cochées : ce plan n'en fournit que le socle pur. Les contrôles (40-03) et le recalage sur
`SizeChanged` (40-07) les finalisent.

## Known Stubs

Aucun. Les fonctions ne sont pas encore appelées par les contrôles : c'est voulu, les plans 40-03 et 40-07 les branchent.

## Self-Check: PASSED

- FOUND : src/Chronos/Rendering/GeometrieCadrans.cs, tests/Chronos.Tests/GeometrieCadransTests.cs
- FOUND : commits 9e29cbb, e9d08ca
