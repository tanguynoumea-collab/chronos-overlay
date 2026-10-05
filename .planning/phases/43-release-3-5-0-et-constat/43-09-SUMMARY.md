---
phase: 43-release-3-5-0-et-constat
plan: 09
subsystem: cadrans / release
tags: [VAL-07, gap_closure, cadrans, braises, release]
requires: [43-08 (Chronos-v3.5.0.exe reconstruit, anneau 5 h en 25 braises)]
provides:
  - anneau extérieur de Braises = la journée locale, 24 braises groupées par tranches de 5 h (calcul pur BraisesJournee)
  - preuve au rendu de la VUE à 11:35 avec un reset à 14:50 (positions calculées sans le code testé)
  - Chronos-v3.5.0.exe reconstruit (même version), empreinte mise à jour
affects: [43-04 (constat utilisateur)]
tech-stack:
  added: []
  patterns: [angles et états explicites fournis par le VM à un contrôle de rendu générique ; fuseau injecté optionnel dans MainViewModel]
key-files:
  created:
    - src/Chronos/Rendering/BraisesJournee.cs
    - tests/Chronos.Tests/BraisesJourneeTests.cs
  modified:
    - src/Chronos/Controls/Cadrans/EmberRingControl.cs
    - src/Chronos/Views/Cadrans/CadranBraisesView.xaml
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/ViewModels/CadranPreviewViewModel.cs
    - tests/Chronos.Tests/CadranBraisesTests.cs
    - tests/Chronos.Tests/CadransThemeBindingTests.cs
    - tests/Chronos.Tests/RempliConsommeTests.cs
    - tests/Chronos.Tests/MainViewModelTests.cs
    - README.md
    - docs/data-sources.md
    - docs/publish.md
    - .zeus/DESIGN_PLAN_CYCLE2.md
    - .planning/phases/43-release-3-5-0-et-constat/43-CONSTAT.md
decisions:
  - "Facteur 0,8 appliqué tel quel ; la règle « vide ≥ 2 × écart intra » n'est pas tenable pour 60 % des ancres (voir « Point impossible »)"
  - "EmberRingControl étendu (Angles / Etats explicites) plutôt qu'un contrôle dédié : le dessin des braises reste unique"
  - "Anneau journée sans état « en attente » : l'heure locale est toujours connue ; reset inconnu = 24 braises régulières"
  - "MainViewModel reçoit un fuseau optionnel (défaut Local), utilisé aussi par la timeline 24 h"
metrics:
  duration: ~45 min
  completed: 2026-10-05
  tasks: 2
  files: 15
---

# Phase 43 Plan 09 : l'anneau extérieur de Braises devient la journée — Summary

L'anneau extérieur de Braises montre maintenant la **journée locale**. Il compte 24 braises, une par heure, avec
minuit en haut et un sens horaire. Les braises sont groupées par tranches de la grille des resets 5 h. Cette grille
part du `resets_at` courant, et ses autres limites sont projetées. Les heures passées sont pleines, l'heure en cours
est en demi-lueur et les heures à venir sont en cendre. La couleur est celle du quota 5 h, et un relevé estimé
(« ≥ ») s'affiche en pointillé. L'anneau hebdo et le centre n'ont pas changé. Chronos-v3.5.0.exe a été reconstruit.

## Point impossible de la spécification : signalé, rien n'a été interprété

Avec un rapprochement de 0,8, l'écart entre deux braises d'un même groupe vaut toujours 12°. Le vide entre deux
groupes de tailles a et b vaut 12 + 1,5·(a + b) degrés.
- **Grilles 5/5/5/5/4** (ancre 14:50, 05:00) : vides de 27° et 25,5°, soit au moins 2 × 12°. La spécification est
  tenue.
- **Grilles avec un petit groupe près de minuit** (ancre 00:50 → 1/5/5/5/5/3, ancre 23:10 → 3/5/5/5/5/1) : les vides
  font 18°, 21° ou 22,5°, sous 24°.
  - Ce cas couvre toutes les ancres dont la première limite du jour tombe entre 00:30 et 03:30, modulo 5 h. Cela
    représente **60 % des ancres**.
  - Il n'a pas de solution. Un groupe de 5 avec un écart uniforme et au plus 6° de décalage par braise impose un écart
    de 12° exactement. Une braise isolée, coincée entre ses voisines (elles-mêmes à au plus 6° de leur heure), dispose
    d'au plus 42° alors qu'il en faudrait 48.
- **Ce qui est fait** : le facteur 0,8 de la spécification est appliqué tel quel. Le vide reste visible (≥ 18°, soit
  1,5 × l'écart intra) et les braises ne se touchent jamais (corde minimale de 13,8 px à R66).
- **À trancher par l'utilisateur** s'il veut autre chose. Par exemple, accepter cette limite, ou relâcher la contrainte
  « ≤ 6° » pour les petits groupes.
- La limite est écrite dans `BraisesJournee.cs` et dans l'amendement de `.zeus/DESIGN_PLAN_CYCLE2.md`. Un test la
  vérifie : `Grille_avec_groupe_d_une_braise_le_vide_reste_plus_large_que_l_ecart_intra`.

## Choix d'implémentation

- **`Rendering/BraisesJournee.cs`** (pur, sans WPF, sans horloge, sans fuseau) :
  - `Tranches` : `floor((i:30 du jour − ancre) / 5 h)`, calculé en heure d'horloge, avec passage de minuit ;
  - `TaillesGroupes` ;
  - `Angles` : `c + 0,8 × (nominal − c)`, où c est le milieu des braises extrêmes du groupe ;
  - `Etats` ;
  - avec un reset inconnu, les angles sont uniformes à (i + 0,5) × 15°.
  - Un groupe ne traverse jamais minuit : 23:30 et 00:30 du même jour sont toujours dans deux tranches différentes.
- **`EmberRingControl`** : deux propriétés de dépendance ont été ajoutées, `Angles` et `Etats`. Elles ne s'appliquent
  que si elles contiennent une valeur par braise. Le dessin des braises est le même qu'avant (pleine, demi-lueur,
  pointillé si estimé, cendre). Sans elles, rien ne change (repli 43-08, utilisé par l'hebdo).
- **VM** :
  - `MainViewModel.JourneeAngles` et `JourneeEtats` sont calculés dans `Interpolate` (tick de 1 s), au même endroit
    que `DayTimeline` ;
  - un fuseau optionnel `TimeZoneInfo? fuseau = null` est ajouté en dernière position du constructeur, avec
    `TimeZoneInfo.Local` par défaut. La timeline 24 h utilise maintenant ce même fuseau (comportement identique en
    production) ;
  - `CadranPreviewViewModel` expose les mêmes propriétés. Il utilise un `MaintenantEchantillon` interne, qui vaut
    14:00 par défaut et que les tests peuvent régler. Le reset reste déduit du curseur.
- **Vue** : l'anneau extérieur est maintenant `Count="24"`, lié à `Angles={Binding JourneeAngles}` et
  `Etats={Binding JourneeEtats}`. La couleur et l'estimation viennent toujours de `FiveHour`. Il n'y a plus de
  `Fraction` ni de `HasData`, donc pas d'état d'attente : l'heure locale est toujours connue. Les commentaires d'en-tête
  sont à jour.
- **Docs** :
  - README : l'anneau extérieur est ta journée, les tranches sont projetées ;
  - `docs/data-sources.md` §3 : nouvelle limite « les tranches de 5 h sont projetées » (Anneaux DS-ARCH-05 + Braises) ;
  - amendement daté du 2026-10-05 dans `.zeus/DESIGN_PLAN_CYCLE2.md` §3.

## Tests (RED c25d6f7 : 35 échecs attendus, puis GREEN b01c896)

- **`BraisesJourneeTests`** (nouveau) :
  - tailles de groupes [5,5,5,5,4] pour 14:50, et pour les ancres 00:50, 05:00 et 23:10 ;
  - invariance de la grille quand l'ancre est décalée de k × 5 h, y compris sur la veille ou le lendemain ;
  - angles exacts pour 14:50 (13,5 / 100,5 / 187,5 / 274,5 / 348) ;
  - position à au plus 6° de l'heure et écart intra de 12°, pour les 4 ancres ;
  - vide ≥ 24° (grilles 5/5/5/5/4) ou ≥ 18° (petits groupes) ;
  - pas de contact au rayon 66 ;
  - allumage à 11:35, 00:10, 23:59 et minuit ;
  - tranche 09:50 → 14:50 : une braise pleine et une en demi-lueur ;
  - pureté (aucun assembly WPF, pas de `UtcNow`, pas de `TimeZoneInfo.Local`) ;
  - garde de doc (README, data-sources).
- **`CadranBraisesTests`** :
  - garde de vue : 24 braises, Angles et Etats non nuls, aucune liaison `Fraction` ;
  - rendu de la vue à 11:35 (+02:00), avec un reset à 14:50 (65 % de 5 h) :
    - braises 0 à 10 pleines (alpha ≥ 250, teinte quota), braise 11 en demi-lueur, braises 12 à 23 en cendre ;
    - braise 6 à droite, braise 12 en bas, braise 18 à gauche ;
    - le pixel au milieu du vide 14|15 est vide ;
    - les positions sont calculées sans `BraisesJournee`, avec des groupes écrits à la main ;
  - contrôle avec états explicites ;
  - l'hebdo est toujours lié à `SevenDay.FractionElapsed`.
  - Les deux anciens rendus 5 h (20 % et 60 %) ont été retirés : ce que montre l'anneau a changé. Le rendu hebdo et
    les tests génériques du contrôle (25/5/11) sont conservés.
- **`RempliConsommeTests`** :
  - Braises compte maintenant 1 liaison `Fraction` (l'hebdo) ;
  - nouveau cas documenté : « rempli = heures passées » pour l'anneau journée ;
  - le rendu de contrôle passe sur la configuration hebdo.
- **`CadransThemeBindingTests`** : (66, 24, 4.0), liaisons `JourneeAngles` et `JourneeEtats` vérifiées par
  `BindingOperations`.
- **`MainViewModelTests`** : un fuseau fixe +02:00 est injecté, avec 09:35Z et un reset à 12:50Z. On vérifie
  `JourneeAngles` et `JourneeEtats` (11:35 / 14:50 locales), puis le cas du reset inconnu (angles uniformes).
- **Corrections de mes propres tests pendant le GREEN :**
  - la tolérance verticale de la braise 6 est passée de 10 à 15 px (100,5° donne y = 97) ;
  - le test de thème vérifie la liaison et non la valeur : le montage sans `UpdateLayout` ne propage pas encore la
    valeur.

## Porte

- `dotnet build Chronos.sln -c Debug -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln` (Debug) : 2 287 réussis, 0 échec, 1 ignoré, total 2 288 (+32 par rapport à 43-08).
- `dotnet build Chronos.sln -c Release -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln -c Release` : 2 287 réussis, 0 échec, 1 ignoré, total 2 288. Relancé après l'édition de
  publish.md et 43-CONSTAT.md : même résultat.

## L'exe reconstruit

| Champ | Valeur |
|-------|--------|
| Chemin | `C:\Users\Tanguy\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.5.0.exe` |
| Taille | 78 200 548 o (< 120 000 000) |
| FileVersion / ProductVersion / ProductName | 3.5.0.0 / 3.5.0 / Chronos (lus par `VersionInfo`, sans lancer l'exe) |
| SHA-256 | `e9884209a6bf61625aa574ada9dcca27b123b2282d692ef562e63fed99a220b3` |
| Sortie de publish | `Chronos.exe` + `Chronos.pdb`, 0 DLL ; SHA-256 identique à la copie |
| Empreinte précédente | `4bab93a6…` (78 200 214 o) : barrée dans 43-CONSTAT.md, remplacée dans publish.md §2 et §4 |
| git | ignoré (`/Chronos-v*.exe`) ; 8 exe à la racine |

Avant la copie, j'ai consulté `Get-Process` (0 processus `Chronos*` sur 583) et `tasklist` (aucune tâche), en lecture
seule. `Chronos-v3.5.0` ne tournait pas à ce moment, donc la copie a été faite. Aucun exe n'a été lancé et aucun
processus n'a été arrêté.

## Écarts par rapport au plan

- Le RED contient des squelettes d'API (méthodes qui lèvent `NotImplementedException`, propriétés vides). Ils
  permettent d'obtenir des échecs à l'exécution plutôt qu'une erreur de compilation.
- Le fuseau injecté dans `MainViewModel` est un nouveau paramètre optionnel. Il fallait l'ajouter : le VM convertissait
  avec `ToLocalTime()`, qui suit le fuseau de la machine.
- Le point impossible est décrit plus haut.

## Known Stubs

Aucun.

## Commits

- c25d6f7 — test(43-09) : anneau extérieur de Braises = journée en tranches de 5 h (RED)
- b01c896 — feat(43-09) : anneau extérieur de Braises = journée en tranches de 5 h
- baccbb8 — release(43-09) : Chronos 3.5.0 reconstruit, anneau journée de Braises

Pas d'étiquette, pas de push. STATE.md, ROADMAP.md et REQUIREMENTS.md n'ont pas été modifiés.

## Self-Check: PASSED

- FOUND: src/Chronos/Rendering/BraisesJournee.cs, tests/Chronos.Tests/BraisesJourneeTests.cs
- FOUND: Chronos-v3.5.0.exe (SHA-256 e9884209… recalculé, identique à la sortie de publish)
- FOUND: c25d6f7, b01c896, baccbb8
