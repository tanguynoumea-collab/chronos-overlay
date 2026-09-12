---
phase: 20-honn-tet-visible-cadran-diagnostic
plan: 01
subsystem: testing
tags: [xunit, wpf, seam, test-isolation, layout, diagnostic, uia]

# Dependency graph
requires:
  - phase: 17
    provides: "Protocole du paramètre optionnel TERMINAL sur DiagnosticService (authStatus) — zéro site de construction retouché"
  - phase: 18
    provides: "Second emploi du même protocole (etatServeur), qui en fait une convention et non un coup de chance"
  - phase: 16
    provides: "Collection xUnit « XAML WPF » DisableParallelization (course du chargeur BAML) et convention TempPaths()"
provides:
  - "IInventaireMachine : couture neutre des deux sondages d'environnement mesurés chers (coffres OAuth + poll UIA)"
  - "InventaireMachine : implémentation réelle, code DÉPLACÉ depuis DiagnosticService.FindTokenVaults"
  - "FakeInventaireMachine : la suite complète passe de 2 min 12 s à 2 s"
  - "CadranBindingTests entièrement sous Path.GetTempPath() — plus aucune lecture des réglages réels de la machine"
  - "Grille des réglages en Grid 2×2 sans cellule vide, prouvée par une mise en page MESURÉE"
affects: [20-02, 20-03, 20-04, 20-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Couture par paramètre OPTIONNEL TERMINAL + repli « ?? new Impl() » : 3e application, zéro site de construction retouché, zéro inscription DI"
    - "Substitution sous test plutôt que mémoïsation en production pour un coût d'E/S"
    - "Preuve de mise en page par ActualWidth après Measure/Arrange, et non par présence d'attribut XAML"

key-files:
  created:
    - src/Chronos/Services/IInventaireMachine.cs
    - src/Chronos/Services/InventaireMachine.cs
    - tests/Chronos.Tests/Fakes/FakeInventaireMachine.cs
    - .planning/phases/20-honn-tet-visible-cadran-diagnostic/deferred-items.md
  modified:
    - src/Chronos/Services/DiagnosticService.cs
    - src/Chronos/Views/SettingsWindow.xaml
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - tests/Chronos.Tests/CadranBindingTests.cs
    - tests/Chronos.Tests/ReglagesBindingTests.cs

key-decisions:
  - "La couture porte sur les DEUX sondages mesurés chers, et sur rien d'autre : IInventaireMachine n'est PAS un point d'extension"
  - "Substitution sous test, JAMAIS mémoïsation en production : un diagnostic qui met en cache son inventaire d'environnement est un diagnostic qui peut mentir"
  - "Le corps de FindTokenVaults est DÉPLACÉ tel quel, pas réécrit : ses bornes sont le fruit d'un réglage"
  - "Aucune inscription DI : le repli « ?? new InventaireMachine() » suffit et laisse CompositionRootTests immobile"
  - "Le commentaire XAML ne reproduit pas le nom du conteneur fautif, sinon le critère de non-retour par grep serait mort-né (doctrine phase 19)"
  - "Ce n'est pas l'attribut ColumnSpan qui est asserté mais la LARGEUR mesurée : un ColumnSpan ignoré compile et s'affiche sans erreur"

patterns-established:
  - "Paramètre optionnel terminal : 3e application consécutive (authStatus 17, etatServeur 18, machine 20) — c'est désormais LA manière d'ouvrir une couture dans ce dépôt"
  - "TempPaths() avec son Assert.StartsWith intégré : l'assertion fait partie du motif, elle interdit qu'une régression future repointe le profil réel"

requirements-completed: []

# Metrics
duration: 34min
completed: 2026-09-12
---

# Phase 20 Plan 01 : Vague 0 — lever les trois dettes Summary

**La suite complète passe de 2 min 12 s à 2 s (66×) en rendant substituables les deux seuls sondages
d'environnement mesurés chers, sans toucher un seul site de construction ni la production ; les tests de
cadran cessent de lire les réglages réels de la machine ; la grille des réglages n'a plus de cellule vide.**

## Performance

- **Duration:** 34 min
- **Started:** 2026-09-12T06:22:00Z
- **Completed:** 2026-09-12T06:56:00Z
- **Tasks:** 3/3
- **Files modified:** 9 (4 créés, 5 modifiés)

## LE LIVRABLE : durée de la suite, avant / après

Mesures réelles sur la machine cible, `dotnet test Chronos.sln -v q --nologo`, durée rendue par le runner :

| | Tests | Durée runner | Horloge murale (build compris) |
|---|---|---|---|
| **AVANT** (commit `71413c1`) | 699 | **2 min 12 s** | 139 s |
| **APRÈS** (commit `4ed9d83`) | 700 | **2 s** | 6 s |
| Gain | +1 test | **×66** | ×23 |

Détail par classe, mesuré séparément :

| Cible | Avant | Après |
|---|---|---|
| `DiagnosticServiceTests` (7 tests) | 2 min 8 s | inclus dans les 693 ms ci-dessous |
| `DiagnosticServiceTests` + `ServicesLayerPurityTests` + `CompositionRootTests` (14 tests) | — | **693 ms** |
| `CadranBindingTests` (14 tests) | — | 1 s |
| `ReglagesBindingTests` (5 tests) | — | 826 ms |

La cause était concentrée : **17 703 ms par balayage de coffres OAuth (94 %)** et **936 ms par poll UI
Automation (5 %)**, payés 7 fois. Tout le reste du rapport de diagnostic coûte moins de 50 ms cumulés.
Deux passes complètes consécutives ont été jouées (précédent BAML 16-03) : **700/700 les deux fois**.

## Accomplishments

### Task 1 — `IInventaireMachine`, la couture qui rend la suite 66× plus rapide

- `IInventaireMachine` expose exactement **deux membres**, `CoffresOAuth(racine)` et
  `SessionsBureau(now)`, et son commentaire de tête dit explicitement que ce n'est **pas un point
  d'extension** : n'y ajouter un membre que si une mesure montre qu'il coûte des secondes. Les sessions
  et la santé du poll sont rendues **ensemble** parce qu'un second appel re-paierait le poll.
- `InventaireMachine` contient le corps de `FindTokenVaults` **déplacé tel quel**. Ses bornes
  (profondeur 3, 5 résultats, liste noire de dossiers volumineux, fichiers > 2 Mo ignorés) sont le fruit
  d'un réglage : les réécrire aurait fait dériver le coût mesuré sans que rien ne le signale.
- `DiagnosticService` reçoit un **3ᵉ paramètre optionnel en dernière position**, avec le repli
  `?? new InventaireMachine()`. **Les 15 sites de construction recensés compilent sans retouche** ; les
  8 situés hors de `DiagnosticServiceTests` n'ont pas été touchés d'une ligne.
- **Aucune inscription DI** : `grep -c "InventaireMachine" src/Chronos/App.xaml.cs` = 0.
  `CompositionRootTests` n'a pas bougé.

### Task 2 — `CadranBindingTests` cesse de lire les réglages réels de la machine

- `MainViewModel` appelle `settings.Load()` **dans son constructeur**. Avec les chemins du profil
  utilisateur, `CadranStyle`, `CadranMode` et `ThemeKey` venaient de la machine (état constaté :
  `Arcs` / `Normal` / `ardoise`). Tout test de style des plans 03 et 04 y aurait été **vert par accident
  et rouge chez le voisin**.
- `TempPaths()` recopié du motif de `ReglagesBindingTests`, **`Assert.StartsWith` compris** : l'assertion
  fait partie du motif, elle interdit qu'une régression future repointe le profil réel.
- Le piège réel du montage : **deux** `new SettingsService(...)` dans `BuildWindow`. Le second alimente
  `OverlayController` — le rater aurait laissé le contrôleur d'overlay relire le profil réel. Les quatre
  consommateurs partagent désormais **une seule** variable `paths`.
- Neutralisation explicite de `vm.CadranStyle` et `vm.IsModeEtendu` après construction : aucun test de la
  phase ne dépend d'un réglage persisté, fût-il dans un répertoire temporaire préexistant.

### Task 3 — le trou de la grille des réglages

- Le conteneur à cellules uniformes **ignore silencieusement `Grid.ColumnSpan`** : poser l'attribut sur
  l'enfant compile, s'affiche sans erreur et **ne fait rien**. C'est le conteneur qui a été remplacé
  (`Grid` 2 colonnes × 2 lignes), pas seulement l'enfant annoté.
- Passer à 3 colonnes était **exclu par la mesure** : largeur utile 296 px ⇒ cellule de 98,7 px, soit
  74,7 px utiles pour un libellé (« Recalibrer hebdo… ») de ≈ 107 px à `FontSize=12`.
- Les **trois marges étaient fausses** par rapport aux positions réelles des boutons. Corrigées.
- Le nouveau `[WpfFact]` asserte des **largeurs mesurées** après `Measure`/`Arrange`, jamais la présence
  d'un attribut : un `ColumnSpan` ignoré donne un rapport ≈ 1, le test exige > 1,8.

## Falsifiabilité jouée (pas seulement affirmée)

| Mutation | Résultat | Révoquée |
|---|---|---|
| `Grid.ColumnSpan="2"` retiré du bouton « Diagnostic… » | `Le_bouton_Diagnostic_occupe_toute_la_largeur…` **ÉCHOUE** | oui, `grep -c` de contrôle à 1 |

## Key Decisions

1. **Substitution sous test, jamais mémoïsation en production.** Mettre en cache le balayage d'environnement
   aurait donné le même gain de temps ET un diagnostic capable de mentir — l'inverse exact de la raison
   d'être de cette phase. `InventaireMachine` ne garde rien entre deux appels, et son commentaire le dit.
2. **La couture couvre les deux sondages mesurés, et rien d'autre.** Le reste du rapport coûte < 50 ms :
   l'abstraire aurait été de l'architecture sans mesure.
3. **Le commentaire XAML ne reproduit pas le nom du conteneur fautif.** Application directe de la doctrine
   de la phase 19 : un commentaire qui recopie l'expression fautive tue le critère de non-retour par grep.
   Le critère du plan (`grep -c "UniformGrid"` = 3) est ainsi tenu au sens de son intention — les trois
   `ItemsPanelTemplate` des sélecteurs sont intacts.
4. **Aucun requirement coché.** Voir ci-dessous.

## Requirements : EXA-03 et EXA-06 laissés Pending

Le frontmatter du plan porte `requirements: [EXA-03, EXA-06]`, mais **cette vague ne livre aucun des deux**.
Elle lève trois dettes d'outillage : ni la distinction visuelle frais/daté/indisponible (EXA-03), ni le nom
de la source alimentant l'affichage (EXA-06, dont la phase 19 a établi qu'il manque un champ) n'ont reçu une
seule ligne. Les cocher ici les déclarerait satisfaits sur la foi d'un plan qui n'y a pas touché.
**Précédent explicite : 17-01, 18-02, 19-01/19-02**, où la même retenue a été appliquée. Ils seront cochés
par les plans qui livrent le correctif (20-03 / 20-04).

## Deviations from Plan

### Ajustements auto-appliqués

**1. [Rule 3 - Blocage] Le commentaire XAML rendait le critère de comptage impossible à tenir**
- **Found during:** Task 3
- **Issue:** Le commentaire rédigé d'après le plan contenait deux fois le littéral `UniformGrid`, portant
  `grep -c "UniformGrid"` à **4** au lieu des 3 exigés. Le critère et le contenu prescrit du commentaire
  étaient mutuellement contradictoires.
- **Fix:** Commentaire reformulé en « le panneau à cellules uniformes qui occupait cette place », sans le
  littéral. La doctrine de la phase 19 (« le commentaire qui décrit un piège ne reproduit jamais
  l'expression fautive ») tranche dans le même sens.
- **Files modified:** `src/Chronos/Views/SettingsWindow.xaml`
- **Commit:** `4ed9d83`

**2. [Rule 2 - Fonctionnalité critique] Falsifiabilité du nouveau test de mise en page**
- **Found during:** Task 3
- **Issue:** Le plan n'exigeait pas de mutation. Or un test de mise en page qui ne peut pas échouer ne
  garde rien — et c'est précisément la classe d'erreur (`ColumnSpan` silencieusement ignoré) qu'il vise.
- **Fix:** Mutation réelle jouée (retrait de `Grid.ColumnSpan="2"`), échec constaté, mutation révoquée et
  vérifiée par grep. Précédents : 18-01, 19-02.
- **Commit:** `4ed9d83`

### Écarts de comptage constatés (aucune action)

- Le plan annonce **10 sites de construction** de `DiagnosticService` ; le dépôt en compte **15**
  (1 production + 14 tests). Le chiffre 10 datait de la phase 18. **Sans conséquence** : le protocole du
  paramètre optionnel terminal en laisse intacts 8 sur 8 hors `DiagnosticServiceTests`, quel que soit
  leur nombre — c'est exactement la propriété recherchée.
- `CadranBindingTests` compte **14 tests**, conforme au plan.

## Deferred Issues

Consignés dans `.planning/phases/20-honn-tet-visible-cadran-diagnostic/deferred-items.md` :

- **`tests/Chronos.Tests/OverlayWindowConfigTests.cs` monte encore sa `MainWindow` sur les chemins réels du
  profil utilisateur** (4 usages) — exactement le motif retiré de `CadranBindingTests` par la tâche 2.
  Dette **préexistante**, hors du périmètre explicite du plan (qui borne la tâche 2 à `CadranBindingTests`).
  Risque concret : si un plan ultérieur de la phase 20 y ajoute une assertion de style, elle sera verte par
  accident sur cette machine. Correctif connu et chiffré en minutes.

## Known Stubs

Aucun. Les deux fichiers créés dans `src/` sont du code de production réel (`InventaireMachine` exécute
le balayage et le poll véritables) ; le seul faux (`FakeInventaireMachine`) vit sous `tests/`.

## Vérifications de sécurité (contrôle avant / après)

| Contrôle | Attendu | Constaté après |
|---|---|---|
| `%APPDATA%\Chronos\oauth.dat` | 518 o, mtime 1783863147 | **518 1783863147** — identique |
| `~/.claude/settings.json` | 6872 o, mtime 1785403369 | **6872 1785403369** — identique |
| Écriture sous `%APPDATA%\Chronos\` | aucune | aucune (mtimes tous antérieurs de plusieurs semaines) |
| Overlay lancé | **NON** (purgerait les 25 groupes de hooks) | jamais lancé |
| Requête réseau depuis un test | aucune | aucune |

## Verification

- [x] `dotnet test Chronos.sln -v q --nologo` : **700/700, 0 échec**, deux passes consécutives
- [x] Durée de la suite sous 30 s : **2 s** (objectif du plan largement dépassé)
- [x] 5 gardes permanentes vertes (`ServicesLayerPurityTests`, `CompositionRootTests`,
      `NormalisationUniqueTests`, `GardesDoctrineTests`, `La_sonde_d_en_tetes_est_le_PRIMAIRE…`) — 13 tests
- [x] `grep -c "IInventaireMachine? machine = null" DiagnosticService.cs` = **1**
- [x] `grep -c "FindTokenVaults" DiagnosticService.cs` = **0** (déplacée, pas dupliquée)
- [x] `grep "using System.Windows"` sur les deux fichiers créés de `Services/` = **0 ligne**
- [x] `grep -c "InventaireMachine" App.xaml.cs` = **0** (aucune inscription DI)
- [x] `grep -c "new FakeInventaireMachine()" DiagnosticServiceTests.cs` = **7**
- [x] `grep -c "ChronosPaths.Default()" CadranBindingTests.cs` = **0**
- [x] `grep -c "Path.GetTempPath()" CadranBindingTests.cs` = **3** (≥ 2 exigé)
- [x] `grep -c "new SettingsService(paths)" CadranBindingTests.cs` = **2**
- [x] `grep -c 'UniformGrid Columns="2"' SettingsWindow.xaml` = **0**
- [x] `grep -c "UniformGrid" SettingsWindow.xaml` = **3** (les trois `ItemsPanelTemplate` intacts)
- [x] `grep -c 'Grid.ColumnSpan="2"' SettingsWindow.xaml` = **1**
- [x] `grep -c 'Margin="4,0,0,6"' SettingsWindow.xaml` = **1**

## Commits

| Task | Commit | Message |
|---|---|---|
| 1 | `d1fdbb9` | `perf(20-01): IInventaireMachine rend substituables les 2 sondages mesures chers` |
| 2 | `c9171ff` | `test(20-01): CadranBindingTests cesse de lire les reglages reels de la machine` |
| 3 | `4ed9d83` | `fix(20-01): la grille des reglages n'a plus de cellule vide ni de marge fausse` |

## Ce que la suite de la phase peut désormais tenir pour acquis

1. **Itérer sur du dessin coûte 2 secondes de vérification**, pas 2 min 12. Les plans 03 et 04 (les plus
   visuels du milestone) peuvent boucler.
2. **Un test de style de cadran est une preuve**, plus un accident de configuration locale.
3. **Une mise en page WPF peut être prouvée par la mesure** — le motif `FindName` + `ActualWidth` après
   `Measure`/`Arrange` est disponible et démontré falsifiable.
4. **La couture par paramètre optionnel terminal est la convention du dépôt** (3 applications). Toute
   nouvelle couture de la phase devrait la suivre plutôt qu'inventer une inscription DI.

## Self-Check: PASSED

5 fichiers annoncés comme créés : tous présents sur disque. 3 commits annoncés : tous présents dans
l'historique git. Aucun élément manquant.
