---
phase: 20
slug: honn-tet-visible-cadran-diagnostic
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-09-12
---

# Phase 20 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (net8.0-windows) — `tests/Chronos.Tests` |
| **Quick run command** | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~Cadran\|FullyQualifiedName~WindowGauge\|FullyQualifiedName~Theming"` |
| **Full suite command** | `dotnet test Chronos.sln -v q --nologo` |
| **Estimated runtime** | **~2 min 10** aujourd'hui · baseline : **699 tests / 0 échec** |

### La dette de durée est mesurée, pas estimée — et elle se lève ici

**692 tests hors `DiagnosticServiceTests` = 2 s.** Les **7** tests de `DiagnosticServiceTests` = **2 min 8 s**,
soit **98,4 % du temps total**. Décomposition d'un `BuildReportAsync` : `FindTokenVaults` **17 703 ms (94 %)**,
poll UIA 936 ms, tout le reste < 50 ms.

Le blocage invoqué par les phases 18 et 19 (« 10 sites de construction à retoucher ») est **faux** : le
protocole du **paramètre optionnel terminal**, établi par les phases 17 et 18, coûte **zéro site**.
Lever cette dette rend la boucle de rétroaction de cette phase — la plus visuelle du milestone — utilisable.

## Sampling Rate

- Après chaque commit de tâche : commande rapide
- Après chaque vague : suite complète
- Avant vérification de phase : suite complète verte, **deux exécutions** (BAML)

## Deux bugs découverts par la recherche, à corriger dans cette phase

1. **`PastilleHorsLigne` et `PastilleInvitationConnexion` peuvent être visibles SIMULTANÉMENT et se
   superposent** (prouvé par lecture de `MajPastilles`). Les pastilles doivent être mutuellement exclusives.
2. **`CadranBindingTests` lit le VRAI `%APPDATA%\Chronos\settings.json`** via `ChronosPaths.Default()` :
   tout test de style y serait vert **par accident** (`CadranStyle: "Arcs"` sur cette machine).
   **À trancher en vague 0** — c'est structurant pour tous les tests de style de cette phase.

## Sémiologie — la réponse retenue

**Trois marques, quatre apparences, parce que deux marques se composent.**
`plancher ⊂ daté` est **prouvé par le code** : `DoctrineFraicheur.Statuer` ne peut atteindre la branche 3
qu'après avoir échoué `if (age <= LimiteAge) return Frais`. Un plancher n'est donc pas *au lieu de* daté :
il est **aussi** daté, et *incomplet vers le haut*.

**Le canal texture existe déjà.** `EmberRingControl`, `FuseBar`, `TideColumn` et `CadranVoletsView` portent
une DP `Estimated` bindée sur `IsEstimated`, qui rend un grain/pointillé — et `SourceReliability.Estimated`
n'est plus affecté qu'à **un seul endroit** en production (branche 3 de la doctrine). **`IsEstimated` signifie
déjà « plancher ».** Seul le style **Arcs** manque : un `StrokeDashArray` sur `RingArc : Shape`.

**Contrainte dure héritée de la phase 19 : pas d'arc de delta, pas de barre d'erreur.** Un delta chiffré
serait une fiction (280 % d'erreur mesurée).

## Matrice réelle à couvrir

**5 styles × 9 thèmes × 2 modes = 90 combinaisons** — et non 5 × 3 × 2. `ThemeCatalog` compte **9** thèmes,
pas 3 comme le disait le contexte. Les tests doivent balayer le catalogue, pas une liste en dur.

## Per-Task Verification Map

*Rempli par le planner.*

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| — | — | — | — | — | — | — | ⬜ pending |

## Gardes permanentes

| Garde | Commande | Attendu |
|-------|----------|---------|
| Pureté Services | `--filter "FullyQualifiedName~ServicesLayerPurityTests"` | vert — le signal visuel passe par les ViewModels |
| Composition DI | `--filter "FullyQualifiedName~CompositionRootTests"` | vert |
| Position de la sonde | `--filter "La_sonde_d_en_tetes_est_le_PRIMAIRE"` | vert (phase 18) |
| Normalisation unique | `--filter "FullyQualifiedName~NormalisationUniqueTests"` | vert (phase 18) |
| Gardes de doctrine | `--filter "FullyQualifiedName~GardesDoctrineTests"` | vert (phase 19) — `tokens / plafond` reste mort |
| Coffre intact | `stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"` | `518 1783863147` |
| Suite complète | `dotnet test Chronos.sln -v q --nologo` | 0 échec, **deux exécutions** |

Toutes les commandes `--filter` se préfixent de `dotnet test Chronos.sln -v q --nologo`.

## Wave 0 Requirements

- [ ] **Trancher `CadranBindingTests` → chemins temporaires** (il lit le vrai `settings.json` aujourd'hui).
      Structurant : sans cela, aucun test de style de cette phase ne prouve quoi que ce soit.
- [ ] **Lever la dette de durée** (paramètre optionnel terminal sur `DiagnosticService`, zéro site cassé) :
      la boucle de rétroaction passe de 2 min 10 à quelques secondes.
- [ ] Toute **nouvelle classe de test chargeant du XAML** doit être rattachée à la collection xUnit
      `DisableParallelization` posée en phase 16 (flakiness du chargeur BAML de WPF).

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Rendu réel du pointillé sur l'arc | EXA-03 | Non exécutable : l'overlay ne doit pas être lancé sans supervision (la purge des 25 hooks se déclencherait) | **Premier point à constater** au lancement : le grain doit être lisible sans être bruyant |
| Lisibilité dans les 90 combinaisons | EXA-03 | Jugement visuel sur fond d'écran réel | Balayer quelques styles × thèmes représentatifs |

## Validation Sign-Off

- [ ] Les trois marques sont distinguables, et leur composition (daté + plancher) reste lisible
- [ ] Le style Arcs a enfin sa texture de plancher
- [ ] Les pastilles sont mutuellement exclusives (bug corrigé)
- [ ] Une seule notion de « périmé » subsiste (l'incohérence `IsStale` 2 min vs doctrine 6 min est tranchée)
- [ ] Le diagnostic nomme la source ET son ancienneté
- [ ] Aucun code calculé et testé ne reste bindé nulle part sans décision explicite
