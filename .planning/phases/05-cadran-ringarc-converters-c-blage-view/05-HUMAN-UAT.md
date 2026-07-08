---
status: partial
phase: 05-cadran-ringarc-converters-c-blage-view
source: [05-03-SUMMARY.md, 05-03-PLAN.md]
started: 2026-07-08T16:43:21Z
updated: 2026-07-08T16:43:21Z
---

## Current Test

[awaiting human testing — build vert, suite complète (68/68) et smoke run automatisé du cadran
composé (`Chronos.exe`, process stable ~8 s sans crash, fenêtre affichée) sont verts. Reste la
validation purement VISUELLE en conditions réelles, non observable par grep/tests : fidélité à la
maquette (couleurs, proportions), sens de vidage des arcs, lisibilité des deux nuances secondaires,
et progression temps réel des arcs/countdown à la seconde.]

## Contexte

Le cadran complet (Task 1 : `App.xaml` tokens + `MainWindow.xaml` composition ; Task 2 : smoke
[WpfFact] 4 états) est vérifié programmatiquement :

- **Build** : `dotnet build Chronos.sln -c Debug` vert (XAML compile, aucun binding vers un membre
  inexistant).
- **Tokens verrouillés** présents dans `Resources/DesignTokens.xaml` : `16151B`, `2C2B34`,
  `34333D`, `46454F`, `2A2932`, `26252E`, `F4F2EC`, `A9A8B2`, `C7C6D0` (CAD-01 + les deux nuances
  secondaires).
- **Bindings** présents dans `MainWindow.xaml` : `FiveHour/SevenDay.FractionRemaining` (CAD-02/03),
  `.CountdownText` (CAD-06), `.IsEstimated` / `.Exhausted` PAR FENÊTRE (DAT-08/CAD-05), `IsStale`
  (staleness) + `DataUnavailable` (ROB-01).
- **Smoke 4 états** (`CadranBindingTests`) : exact / estimé (Utilization null → converter ne crashe
  pas) / indisponible (deux Unavailable → `DataUnavailable`, ROB-01) / fiabilité mixte
  (`FiveHour.IsEstimated == false` ET `SevenDay.IsEstimated == true` → badges « estimée » bien
  indépendants par fenêtre).
- **Smoke run** : `Chronos.exe` lancé ~8 s, Host démarré, fenêtre affichée, aucun crash.

Ce qui suit ne peut être vérifié que par un œil humain sur le rendu réel.

## Tests

### 1. Fidélité maquette — couleurs et structure

expected: Lancer `dotnet run --project src/Chronos/Chronos.csproj -c Debug`. Le cadran apparaît en
haut à droite (220×220), sombre et semi-transparent, gradué. Fond sombre `#16151B`, rim discret,
deux anneaux concentriques (extérieur = 5 h, intérieur = hebdo), couleurs conformes à la rampe
(vert `#7BB13C` → ambre `#EFA23A` → rouge `#D8503A` selon la charge, gris `#5A5960` si épuisé).
result: [pending]

### 2. Deux nuances secondaires distinguables

expected: Le countdown hebdo utilise la nuance secondaire CLAIRE (`#C7C6D0`), les badges/mentions
(estimée, quota épuisé, données périmées, données indisponibles) la nuance plus SOURDE (`#A9A8B2`).
Les deux se distinguent à l'œil ; aucun token orphelin.
result: [pending]

### 3. Progression temps réel (arcs + countdown)

expected: Observer ~10 s : le compte à rebours central décrémente à la seconde, et la longueur des
arcs progresse (se vide vers le reset) sans saccade — CAD-02/03 + RAF-03.
result: [pending]

### 4. Sens de vidage et continuité des arcs (piège IsLargeArc)

expected: Aucun arc ne « saute » du mauvais côté quand il dépasse la moitié (piège IsLargeArc), et
un anneau plein en début de fenêtre est continu (pas de micro-fente ni de couture visible).
result: [pending]

### 5. Signaux de fiabilité PAR FENÊTRE

expected: Si une seule source est en repli JSONL (ex. hebdo estimée, 5 h exacte), le badge
« estimée » n'apparaît QUE près du countdown concerné, et l'arc de cette fenêtre n'invente PAS de
couleur d'utilization (couleur neutre `#2A2932`). L'autre anneau garde sa couleur de rampe. De même
« quota épuisé » ne s'affiche que pour la fenêtre réellement épuisée — DAT-08 / CAD-05.
result: [pending]

### 6. Staleness et indisponibilité

expected: Si la donnée est périmée (> 2 min sans capture), la mention « données périmées » apparaît
en texte secondaire. Si aucune source n'est lisible : « données indisponibles » s'affiche, le
cadran reste visible (pistes vides), aucun crash — DAT-08 + ROB-01.
result: [pending]

## Summary

total: 6
passed: 0
issues: 0
pending: 6
skipped: 0
blocked: 0

## Gaps

- Ces six tests VISUELS ne bloquent pas le statut programmatique de la Phase 5 (build + 68/68 tests
  + smoke run du cadran composé sont verts). Ils recueillent l'approbation humaine sur la fidélité
  maquette et le comportement temps réel, non capturables automatiquement. En cas d'écart (couleurs,
  proportions, sens de vidage, badges par fenêtre, staleness), consigner ici pour une passe de
  gap-closure.
