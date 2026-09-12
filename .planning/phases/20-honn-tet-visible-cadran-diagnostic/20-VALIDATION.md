---
phase: 20
slug: honn-tet-visible-cadran-diagnostic
status: validated
nyquist_compliant: true
wave_0_complete: true
created: 2026-09-12
validated: 2026-09-12
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

Carte posée par le planner et RELEVÉE à l'exécution (2026-09-12, plan 20-05 tâche 2). Toutes les
commandes se préfixent de
`dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q`.

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command (`--filter …`) | File Exists | Status |
|---------|------|------|-------------|-----------|----------------------------------|-------------|--------|
| 20-01 T1 — couture `IInventaireMachine` | 01 | 1 | EXA-06 (infra) | unité + perf | `~DiagnosticServiceTests` · `~ServicesLayerPurityTests` · `~CompositionRootTests` | ✅ créé | ✅ 14/14 · 693 ms (2 min 8 s avant) |
| 20-01 T2 — `CadranBindingTests` → `TempPaths()` | 01 | 1 | EXA-03 (infra) | BAML | `~CadranBindingTests` | ✅ | ✅ 14/14 · 1 s · `TempPaths()` |
| 20-01 T3 — `UniformGrid` → `Grid` 2×2 | 01 | 1 | EXA-03 (dette) | BAML `Measure/Arrange` | `~ReglagesBindingTests` | ✅ | ✅ 6/6 · 826 ms · mutation jouée |
| 20-02 T1 — `SourceUsage` + 5 producteurs + doctrine | 02 | 2 | EXA-06 | unité | `~GardesDoctrineTests` · `~DoctrineFraicheurTests` · `~LastExactStoreTests` | ✅ créé | ✅ +6 tests · `SourceUsage` posée |
| 20-02 T2 — `LibelleSource` (vocabulaire FR unique) | 02 | 2 | EXA-06 | unité | `~LibelleSourceTests` | ✅ créé | ✅ 18/18 · vocabulaire FR unique |
| 20-02 T3 — mort d'`EstimatedTokens` + garde structurelle | 02 | 2 | EXA-04 (non-retour) | garde de source | `~GardesDoctrineTests` | ✅ | ✅ garde de STRUCTURE · 0 ligne `EstimatedTokens` en source |
| 20-03 T1 — `IsEstimated` → `EstPlancher` (8 bindings, 11 assertions) | 03 | 3 | EXA-03 | unité + BAML | `~WindowGaugeViewModelTests` · `~MainViewModelTests` · `~CadranBindingTests` | ✅ | ✅ 8 bindings renommés |
| 20-03 T2 — mort d'`IsStale` + `EstDate` + garde du seuil | 03 | 3 | EXA-03 | unité + garde de source | `~GardesDoctrineTests` · `~MainViewModelTests` · `~WindowGaugeViewModelTests` | ✅ | ✅ une seule notion de « périmé » |
| 20-03 T3 — `AfficherReleveDate` + `InfobulleReleve` | 03 | 3 | EXA-03, EXA-06 | unité | `~MainViewModelTests` | ✅ | ✅ 47/47 · infobulle et marque d'âge |
| 20-04 T1 — pointillé de plancher aux Anneaux | 04 | 4 | EXA-03 | BAML `[WpfFact]` | `~CadranBindingTests` | ✅ | ✅ RED 3 → GREEN · pointillé aux Anneaux |
| 20-04 T2 — rangée de pastilles + marque d'âge | 04 | 4 | EXA-03 | BAML `[WpfFact]` (géométrie) | `~CadranBindingTests` | ✅ | ✅ RED 5 → GREEN · recouvrement mesuré puis mort |
| 20-04 T3 — mot « indisponible » | 04 | 4 | EXA-03 | BAML `[WpfFact]` | `~CadranBindingTests` · `~ThemingTests` | ✅ | ✅ RED 4 → GREEN · 24/24 + 9/9 |
| 20-05 T1 — diagnostic : source + ancienneté, « ≥ » et non « ~ » | 05 | 5 | EXA-06 | unité | `~DiagnosticServiceTests` · `~NormalisationUniqueTests` | ✅ | ✅ RED 6 → GREEN · 14/14 · 534 ms |
| 20-05 T2 — porte de phase (suite ×2, 5 gardes, comptage) | 05 | 5 | EXA-03, EXA-06 | suite complète | *(aucun filtre — suite entière, deux fois)* | ✅ | ✅ 748/748 ×2 · 3 s · 5 gardes vertes |
| 20-05 T3 — constat visuel + vérifications héritées 17/18/19 | 05 | 5 | EXA-03 | **manuel** | — (protocole remis à l'utilisateur) | — | ⏳ à vérifier par l'utilisateur |

## Relevé de porte — 2026-09-12

**Suite complète, deux exécutions consécutives** (`dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q`) :

| Passe | Total | Échecs | Durée runner |
|---|---|---|---|
| 1 | **748** | **0** | **3 s** |
| 2 | **748** | **0** | **3 s** |

**Les cinq gardes permanentes, chacune par sa commande :**

| Garde | Tests | Résultat |
|---|---|---|
| `ServicesLayerPurityTests` | 2 | ✅ 12 ms |
| `CompositionRootTests` | 5 | ✅ 349 ms |
| `NormalisationUniqueTests` | 3 | ✅ 13 ms |
| `GardesDoctrineTests` | 8 | ✅ 31 ms |
| `La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte` | 1 | ✅ 97 ms |

**Comptage réconcilié NOMINATIVEMENT depuis la baseline 699 :**

| Plan | Écart | Nom du test ou de la classe | Cumul |
|---|---|---|---|
| — | baseline | fin de phase 19 (`71413c1`) | **699** |
| 20-01 | **+1** | `ReglagesBindingTests.Le_bouton_Diagnostic_occupe_toute_la_largeur_et_aucun_libelle_n_est_tronque` | 700 |
| 20-02 | +3 | `GardesDoctrineTests` — gardes de source et de composite | |
| 20-02 | +2 | `RateLimitHeaderUsageProviderTests` — nommage par la sonde (nominal + « dépassement seul ») | |
| 20-02 | +1 | `LastExactStoreTests` — nommage par le magasin au rechargement | |
| 20-02 | +18 | `LibelleSourceTests` — vocabulaire FR (dont un `[Theory]` compté 3) | |
| 20-02 | +1 | `GardesDoctrineTests.Aucun_champ_nomme_EstimatedTokens_ne_reapparait_dans_Models_ni_Services` | |
| 20-02 | **−1** | `CadranBindingTests.EstimatedTokens_seul_ne_surface_PLUS_rien_au_cadran` — **supprimé**, la garde change de niveau | **724** |
| 20-03 | **−1** | `MainViewModelTests.IsStale_vrai_quand_la_capture_depasse_deux_minutes` — **supprimé**, seconde notion de « périmé » tranchée | |
| 20-03 | +3 | `WindowGaugeViewModelTests` — `EstDate`, composition `plancher ⊂ daté`, couple (qui, depuis quand) | |
| 20-03 | +1 | `GardesDoctrineTests` — « aucun seuil d'ancienneté hors doctrine » | |
| 20-03 | +6 | `MainViewModelTests` — marque d'âge globale (3) + infobulle (3) | **733** |
| 20-04 | +11 | `CadranBindingTests` — pointillé (5), rangée et géométrie (3), mot « indisponible » (3) | **744** |
| 20-05 | +4 | `DiagnosticServiceTests` — source, ancienneté, source absente, plancher « ≥ » | **748** |

699 + 1 + 24 + 9 + 11 + 4 = **748**. Chaque écart porte un nom ; **deux tests ont été supprimés**, tous deux
nommés ci-dessus, tous deux remplacés par une garde de niveau supérieur. Le critère de sortie est
**0 échec + chaque écart justifié**, et jamais « ≥ 699 ».

**Sécurité et intégrité — relevés AVANT et APRÈS le travail de la phase :**

| Contrôle | Attendu | Avant 20-05 | Après 20-05 |
|---|---|---|---|
| `%APPDATA%\Chronos\oauth.dat` | `518 1783863147` | **518 1783863147** | **518 1783863147** |
| `~/.claude/settings.json` | `6872 1785403369` | **6872 1785403369** | **6872 1785403369** |
| Fichiers SOURCE contenant `RefreshAsync` | exactement **2** | 2 | **2** (`ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs`) |
| Écriture dans le vrai `%APPDATA%\Chronos\` | aucune | — | **aucune** (tous les mtimes antérieurs au début du plan) |
| Requête réseau depuis un test | aucune | — | **aucune** (les 11 `DiagnosticServiceTests` montent `Token = null`) |
| Appel de refresh avec le jeton RÉEL | jamais | — | **jamais** |
| Overlay lancé | **NON** | non | **jamais lancé** |

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

- [x] **Trancher `CadranBindingTests` → chemins temporaires** (il lit le vrai `settings.json` aujourd'hui).
      Structurant : sans cela, aucun test de style de cette phase ne prouve quoi que ce soit.
- [x] **Lever la dette de durée** (paramètre optionnel terminal sur `DiagnosticService`, zéro site cassé) :
      la boucle de rétroaction passe de 2 min 10 à quelques secondes.
- [x] Toute **nouvelle classe de test chargeant du XAML** doit être rattachée à la collection xUnit
      `DisableParallelization` posée en phase 16 (flakiness du chargeur BAML de WPF).

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Rendu réel du pointillé sur l'arc | EXA-03 | Non exécutable : l'overlay ne doit pas être lancé sans supervision (la purge des 25 hooks se déclencherait) | **Premier point à constater** au lancement : le grain doit être lisible sans être bruyant |
| Lisibilité dans les 90 combinaisons | EXA-03 | Jugement visuel sur fond d'écran réel | Balayer quelques styles × thèmes représentatifs |


### Vérifications héritées du milestone v1.5, soldées ici

| Behavior | Phase | Requirement | Why Manual | Statut |
|----------|-------|-------------|------------|--------|
| Parcours de reconnexion en un clic, de bout en bout | 17 | TOK-03 | Exige une authentification RÉELLE de l'utilisateur | ⏳ à vérifier par l'utilisateur |
| Contraste des pastilles sur fond d'écran réel | 17 | TOK-02 | Jugement visuel | ⏳ à vérifier par l'utilisateur |
| Un 429 RÉEL porte bien les en-têtes `anthropic-ratelimit-unified-*` | 18 | HDR-02 | Exige une saturation réelle du compte — **non prouvé en production** | ⏳ à vérifier par l'utilisateur |
| Noms des en-têtes de limite présents sur `/api/oauth/usage` | 18 | HDR-06 | Exige un appel réel authentifié | ⏳ à vérifier par l'utilisateur |
| Bascule visuelle réelle (le « 10 % » figé doit avoir cédé) | 19 | EXA-02 | Exige de lancer l'overlay républié | ⏳ à vérifier par l'utilisateur |
| Lisibilité du grain de plancher à distance (9 thèmes × 5 styles × 2 modes) | 20 | EXA-03 | Jugement visuel | ⏳ à vérifier par l'utilisateur |

Le protocole ordonné et actionnable de ces six constats est rédigé dans `20-05-SUMMARY.md`, section
**« À VÉRIFIER PAR L'UTILISATEUR »**, sauvegarde de `~/.claude/settings.json` en tête.

## Validation Sign-Off

- [x] Les trois marques sont distinguables, et leur composition (daté + plancher) reste lisible
      — **au sens du code et des tests de géométrie**. Le jugement « à distance de lecture, sur fond
      d'écran réel » reste ⏳ (point n° 1 et n° 2 du protocole remis à l'utilisateur).
- [x] Le style Arcs a enfin sa texture de plancher
- [x] Les pastilles ne se **recouvrent** plus — corrigé par la STRUCTURE (rangée), et non par une
      exclusion mutuelle : 20-04 a établi qu'elles disent des choses différentes et peuvent légitimement
      coexister. C'est le recouvrement qui était le défaut, pas la simultanéité.
- [x] Une seule notion de « périmé » subsiste (l'incohérence `IsStale` 2 min vs doctrine 6 min est tranchée)
- [x] Le diagnostic nomme la source ET son ancienneté
- [x] Aucun code calculé et testé ne reste bindé nulle part sans décision explicite
