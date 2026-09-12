---
phase: 20-honn-tet-visible-cadran-diagnostic
plan: 02
subsystem: models
tags: [exa-06, source, doctrine, vocabulaire-fr, garde-structurelle, non-retour]

# Dependency graph
requires:
  - phase: 19
    provides: "Motif du champ porté par WindowState et non par UsageSnapshot (Provenance), et la doctrine qui l'hérite par « candidat with »"
  - phase: 18
    provides: "Premier précédent du même motif (StatutServeur) et la sonde d'en-têtes comme primaire de la chaîne"
  - phase: 20
    plan: 01
    provides: "Suite à 2 s : les 9 mutations de falsifiabilité de ce plan ont coûté des secondes, pas des heures"
provides:
  - "SourceUsage : les 5 producteurs de la chaîne DI réelle, nommables"
  - "WindowState.Source : le nom du producteur, survivant aux trois composites imbriqués"
  - "LibelleSource : vocabulaire FR UNIQUE (nom de source, ancienneté, provenance), prêt pour le diagnostic ET l'infobulle"
  - "Garde structurelle de non-retour du champ mort de l'estimation absolue"
affects: [20-03, 20-04, 20-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "3e application du motif « champ porté par WindowState, non par UsageSnapshot » : Best() rend l'instance PAR RÉFÉRENCE, la recomposition du snapshot se fait par « new »"
    - "Garde de non-retour STRUCTURELLE (balayage du texte source) plutôt que COMPORTEMENTALE : un champ absent est une garantie plus forte qu'un champ mort surveillé"
    - "Garde d'exhaustivité d'enum par réflexion : un membre ajouté sans libellé tombe au lieu de retomber en silence sur le repli"

key-files:
  created:
    - src/Chronos/Models/SourceUsage.cs
    - src/Chronos/Text/LibelleSource.cs
    - tests/Chronos.Tests/LibelleSourceTests.cs
  modified:
    - src/Chronos/Models/WindowState.cs
    - src/Chronos/Services/DoctrineFraicheur.cs
    - src/Chronos/Services/RateLimitHeaderUsageProvider.cs
    - src/Chronos/Services/ChronosOAuthUsageProvider.cs
    - src/Chronos/Services/ClaudeOAuthUsageProvider.cs
    - src/Chronos/Services/ClaudeUsageObjectProvider.cs
    - src/Chronos/Services/LastExactStore.cs
    - src/Chronos/ViewModels/WindowGaugeViewModel.cs
    - tests/Chronos.Tests/GardesDoctrineTests.cs
    - tests/Chronos.Tests/CadranBindingTests.cs
    - tests/Chronos.Tests/RateLimitHeaderUsageProviderTests.cs
    - tests/Chronos.Tests/LastExactStoreTests.cs

key-decisions:
  - "Le nom de la source est un axe DISTINCT de la provenance : les fusionner produirait un enum de 15 membres et rendrait le diagnostic incapable de dire lequel des deux points OAuth répond"
  - "Aucun membre fourre-tout dans l'enum : l'absence de source se dit par null, jamais par une valeur qui affirmerait quelque chose"
  - "La forme « dépassement seul » reste sans source : 6 « new WindowState » en production, 5 lignes « Source = » — l'asymétrie est le contenu de la décision"
  - "Rien n'a été ajouté dans Qualifier : l'héritage de la source par un plancher est GRATUIT (candidat with) et c'est un test qui le prouve, pas une supposition"
  - "EstimatedTokens supprimé plutôt que conservé sous garde comportementale : la garde a CHANGÉ DE NIVEAU, elle n'a pas disparu"
  - "TDD RED impossible (ProjectReference), donc falsifiabilité par 9 mutations réelles jouées puis révoquées — précédent explicite des plans 17-04/05, 18-01/02, 20-01"

patterns-established:
  - "Un nouveau champ de WindowState se prouve par une chaîne de TROIS composites imbriqués montée en test, jamais par lecture du code de Best()"
  - "Un commentaire ne reproduit jamais le littéral qu'un critère grep == 0 interdit (2e application consécutive après 20-01)"

requirements-completed: []

# Metrics
duration: 10min
completed: 2026-09-12
---

# Phase 20 Plan 02 : `SourceUsage`, libellés et mort d'`EstimatedTokens` Summary

**Chaque fenêtre d'usage sait désormais NOMMER son producteur parmi cinq, ce nom traverse les trois
composites imbriqués par référence et s'efface dès que personne n'alimente la fenêtre ; le vocabulaire
français de tout cela est calculé en un point unique ; et le champ mort de l'estimation absolue n'est
plus surveillé — il n'existe plus, sous garde structurelle.**

## Performance

- **Duration:** 10 min
- **Started:** 2026-09-12T06:30:11Z
- **Completed:** 2026-09-12T06:39:52Z
- **Tasks:** 3/3
- **Files modified:** 15 (3 créés, 12 modifiés)

## Bilan de la suite : chaque écart nominatif

| | Tests | Durée |
|---|---|---|
| Entrée (commit `4ed9d83`) | 700 | 2 s |
| Sortie (commit `7348f32`) | **724** | **2 s** |

| Écart | Tests | Où |
|---|---|---|
| Gardes de source et de composite | +3 | `GardesDoctrineTests` |
| Nommage par la sonde (nominal + forme « dépassement seul ») | +2 | `RateLimitHeaderUsageProviderTests` |
| Nommage par le magasin au rechargement | +1 | `LastExactStoreTests` |
| Vocabulaire FR | +18 | `LibelleSourceTests` |
| Garde structurelle de non-retour | +1 | `GardesDoctrineTests` |
| Garde de COMPORTEMENT du champ mort, devenue incompilable | **−1** | `CadranBindingTests` |
| **Total** | **+24** | 700 → 724 |

Le plan prévoyait `−1 + ≈20`. Les **+4** supplémentaires sont nommés : **+3** tests de producteurs
(justifiés en Déviations, Rule 2) et **+2** dans `LibelleSourceTests` contre les ≈16 annoncés (la borne
exacte de la 60ᵉ minute, et le `[Theory]` des provenances que xUnit compte en 3 tests), **−1** de
recomptage. Deux passes complètes consécutives : **724/724 les deux fois** (précédent BAML 16-03).

## Accomplishments

### Task 1 — `SourceUsage` posée là où elle survit

- **`SourceUsage` : 5 membres**, calqués un par un sur la chaîne DI réelle d'`App.xaml.cs`. Rien « au
  cas où ».
- **`WindowState.Source`, `init` et nullable, sans `required`.** Le garde-fou réel mesuré :
  `grep -c "public required" WindowState.cs` = **2** (`Kind`, `Reliability`) — inchangé. **Zéro site de
  construction retouché**, ce qui **confirme explicitement la mesure de la recherche** (voir plus bas).
- **5 lignes `Source = SourceUsage.X`** dans `src/Chronos/Services`, pour **6** `new WindowState` en
  production. La 6ᵉ — la forme « dépassement seul » — reste délibérément sans source : ses deux fenêtres
  sont indisponibles, elles TRANSPORTENT un fait de compte mais ne sont alimentées par personne. Un test
  dédié empêche qu'elle en gagne une par mégarde.
- **`DoctrineFraicheur.Indisponible` efface la source** à côté de la provenance. **`Qualifier` n'a pas
  reçu une ligne** : l'héritage par `candidat with` était déjà là, et c'est le test
  `Un_plancher_conserve_la_source_du_releve_memorise` qui le PROUVE au lieu de le supposer.
- **La preuve par référence est montée en vrai** : une chaîne de trois composites imbriqués où les deux
  maillons externes sont muets, et le nom de la source la plus interne arrive intact au snapshot final.

### Task 2 — le vocabulaire français, calculé une fois

- `Chronos.Text.LibelleSource` : pur (aucun WPF, aucun I/O, aucune horloge propre — `now` est toujours
  un paramètre), sur le motif exact de `PercentFormatter`.
- **Un seul libellé de repli** dans tout le fichier, et il ne nomme aucune source. Un repli qui dirait
  « pont statusLine » ferait croire à une source vivante là où il n'y a qu'une absence d'information.
- **L'horloge qui recule est traitée**, pas subie : un relevé horodaté dans le futur rend « à l'instant »
  et jamais une durée négative — les sources horodatent côté serveur, la machine côté client, et rien ne
  garantit leur accord.
- **Garde d'exhaustivité par réflexion** : `Enum.GetValues<SourceUsage>()` + assertion que le libellé
  n'est ni vide ni égal au repli. Un 6ᵉ membre ajouté sans libellé fait tomber le test (mutation jouée).
- `CountdownFormatter` **n'a pas été réutilisé**, et le commentaire dit pourquoi : il formate une durée
  RESTANTE (« encore 3 h 12 »), l'ancienneté regarde vers le passé (« il y a 3 h 12 »). Même
  arithmétique, sens inverse.

### Task 3 — la garde change de niveau

- `EstimatedTokens` **supprimé** de `WindowState` et de `DoctrineFraicheur.Indisponible`.
- **Les trois commentaires qui le nommaient désignent désormais le CONCEPT** (« la somme de l'estimation
  absolue supprimée en phase 16 ») et non le champ. C'est ce qui rend le critère « zéro occurrence dans
  `src/Chronos` » atteignable — doctrine de la phase 19, appliquée pour la 2ᵉ fois consécutive.
- Le test de comportement de `CadranBindingTests` ne compilait plus : il n'a **pas** été retiré sans
  remplaçant, il a **changé de niveau**. Son successeur
  `Aucun_champ_nomme_EstimatedTokens_ne_reapparait_dans_Models_ni_Services` balaie le texte source de
  `Models/` et `Services/` via le chemin injecté par MSBuild, avec la même garde de non-muettitude
  (`fichiers.Count >= 40`) et un message d'échec qui dit POURQUOI.
- Le littéral interdit vit **uniquement** dans ce test, en chaîne de caractères. Périmètre vérifié :
  **0 occurrence dans `src/Chronos`**, **2 dans `GardesDoctrineTests`** (nom du test + constante),
  **0 ailleurs dans `tests/`**.

## Falsifiabilité jouée — 9 mutations, toutes rouges, toutes révoquées

TDD RED strict est impossible dans ce dépôt (`tests/Chronos.Tests` porte un `ProjectReference` vers
`Chronos` : un test référençant un type inexistant empêche TOUTE la solution de compiler, et l'étape RED
serait un commit où `dotnet test` n'est même pas invocable). Doctrine déjà écrite en tête de
`RateLimitHeaderUsageProviderTests`, précédents 17-04, 17-05, 18-01, 18-02, 20-01. La preuve est donc
obtenue par mutation mesurée.

| # | Mutation | Test visé | Résultat |
|---|---|---|---|
| M1 | `Indisponible` n'efface plus la source | `Une_fenetre_indisponible_ne_nomme_AUCUNE_source` | ROUGE |
| M2 | Le magasin ne se nomme plus | `La_fenetre_reconstruite_se_reclame_du_MAGASIN…` + `Un_plancher_conserve_la_source…` | ROUGE |
| M3 | La sonde ne se nomme plus | `Les_fenetres_produites_par_la_sonde_la_NOMMENT…` | ROUGE |
| M4 | La forme « dépassement seul » GAGNE une source | `La_forme_depassement_seul_ne_nomme_AUCUNE_source` | ROUGE |
| M5 | `Best()` recompose la fenêtre par `new` au lieu de rendre l'instance | `La_source_survit_aux_TROIS_composites_imbriques` | ROUGE |
| M6 | 6ᵉ membre d'enum sans libellé | `Aucun_membre_de_l_enum_ne_retombe_sur_le_libelle_de_repli` | ROUGE |
| M7 | L'âge négatif n'est plus rattrapé | `Un_releve_horodate_dans_le_futur…` | ROUGE |
| M8 | Le repli nomme une source par défaut | `Une_source_absente_ne_recoit_aucun_nom_de_producteur` | ROUGE |
| M9 | Réintroduction du champ mort | `Aucun_champ_nomme_EstimatedTokens_ne_reapparait…` | ROUGE |

M5 mérite d'être signalée : c'est la mutation qui casse la PROPRIÉTÉ sur laquelle repose tout le choix de
conception (le champ vit sur `WindowState` parce que `Best()` rend l'instance par référence). Sans elle,
le test de survie aurait pu passer pour une tautologie. Après révocation, `git diff` sur `src/` ne
contient aucune des neuf mutations.

## Mesure de la recherche : CONFIRMÉE

Le plan demandait une confirmation ou une infirmation explicite.

| Affirmation de la recherche | Constat |
|---|---|
| `WindowState` n'a que **2** membres `required` | **Confirmé** : 2, inchangé |
| **48 sites** de construction (6 en `src/`, 42 en `tests/`) | **Confirmé** : 6 en `src/` ; 45 en `tests/` après ce plan = 42 + 4 ajoutés par les nouveaux tests − 1 supprimé avec le test du champ mort |
| Une propriété `init` nullable de plus casse **0 site sur 48** | **Confirmé** : aucun site existant retouché, `dotnet build` vert du premier coup |
| La garde `Toute_propriete_de_UsageSnapshot_…` n'est pas concernée | **Confirmé** : verte, et `CompositeUsageProvider.cs` comme `UsageSnapshot.cs` sont **absents** du diff des trois commits |

## Deviations from Plan

### Ajustements auto-appliqués

**1. [Rule 3 - Blocage] Le commentaire prescrit rendait le critère `grep "Inconnue" == 0` impossible**

- **Found during:** Task 1
- **Issue:** Le plan prescrivait mot pour mot un commentaire contenant « Pas de membre « Inconnue » : … »
  ET le critère `grep -c "Inconnue" SourceUsage.cs` = **0**. Mesuré à **1** après rédaction conforme :
  les deux étaient mutuellement contradictoires — exactement la situation rencontrée en 20-01 (tâche 3)
  avec `UniformGrid`.
- **Fix:** Commentaire reformulé en « AUCUN membre fourre-tout … jamais par une valeur d'enum », et le
  commentaire dit maintenant lui-même pourquoi il ne reproduit pas le littéral. Même correction dans la
  XML-doc de `WindowState.Source`. Doctrine de la phase 19, 2ᵉ application consécutive.
- **Files modified:** `src/Chronos/Models/SourceUsage.cs`, `src/Chronos/Models/WindowState.cs`
- **Commit:** `e9cd825`

**2. [Rule 2 - Fonctionnalité critique] Les 5 lignes `Source =` n'étaient garanties que par un grep**

- **Found during:** Task 1
- **Issue:** Les trois tests prescrits couvrent la survie dans la chaîne, l'effacement et l'héritage —
  mais AUCUN ne vérifie qu'un producteur pose effectivement sa source. Le seul filet était un critère de
  comptage `grep`, qui ne distingue pas une ligne posée au bon endroit d'une ligne posée au mauvais. Or
  la section `<behavior>` du plan nomme explicitement ces deux comportements (« la sonde d'en-têtes a
  produit la fenêtre », « une fenêtre produite par `Reconstruire` »).
- **Fix:** +3 tests de producteur — la sonde en nominal, la sonde en forme « dépassement seul »
  (la seule construction de production SANS source, désormais gardée), et le magasin au rechargement.
  Mutations M2, M3, M4 jouées sur ces trois-là.
- **Files modified:** `tests/Chronos.Tests/RateLimitHeaderUsageProviderTests.cs`,
  `tests/Chronos.Tests/LastExactStoreTests.cs`
- **Commit:** `e9cd825`

### Écarts constatés (aucune action)

- **`CompositeUsageProvider.cs` marqué modifié par `git status` après les mutations**, alors que
  `git diff` était vide : différence de fins de ligne introduite par la réécriture du fichier pendant la
  mutation M5, sur un dépôt en `core.autocrlf=true`. Restauré à l'identique depuis `HEAD`
  (`git checkout --`) AVANT tout commit. Vérification finale : le fichier est **absent** du diff
  `4ed9d83..HEAD`. Le critère du plan est tenu au sens de son intention comme à la lettre.
- **Aucune tâche n'a atteint la limite de 3 auto-corrections.**

## Requirements : EXA-06 laissé Pending

Le frontmatter du plan porte `requirements: [EXA-06]`. Ce plan livre la **matière** d'EXA-06 — le nom de
la source existe, il est posé, il survit, il s'efface, il a un vocabulaire français. Mais EXA-06 est
rédigé côté UTILISATEUR : « **le diagnostic indique** quelle source alimente réellement l'affichage, et
depuis quand ». Aucune ligne de `DiagnosticService` n'a bougé ; rien de tout cela n'est encore lisible à
l'écran. Cocher ici déclarerait satisfait sur la foi d'un plan qui n'a touché aucune surface visible.

**Précédent explicite et répété : 17-01, 18-02, 19-01, 19-02, 20-01.** EXA-06 sera coché par le plan 05
(diagnostic), qui consommera `LibelleSource`.

## Deferred Issues

Aucun nouveau. Le report de 20-01 (`OverlayWindowConfigTests` monte encore sa `MainWindow` sur les
chemins réels du profil utilisateur) reste consigné dans `deferred-items.md` et n'a pas été aggravé par
ce plan — aucun des fichiers touchés ici n'y est lié.

## Known Stubs

Aucun. Les trois fichiers créés sont du code réel : `SourceUsage` est consommé par les 5 producteurs,
`WindowState.Source` porte une valeur en production à chaque tick, et `LibelleSource` est testé sur ses
douze sorties. `LibelleSource` n'a en revanche **pas encore de consommateur en production** — c'est
délibéré et annoncé par le plan (« prêt pour les deux consommateurs : diagnostic 20-05 et infobulle
20-03 »), et non un stub : la classe est complète, testée et falsifiable telle quelle.

## Vérifications de sécurité (contrôle avant / après)

| Contrôle | Attendu | Constaté après |
|---|---|---|
| `%APPDATA%\Chronos\oauth.dat` | 518 o, mtime 1783863147 | **518 1783863147** — identique |
| `~/.claude/settings.json` | 6872 o, mtime 1785403369 | **6872 1785403369** — identique |
| Écriture sous `%APPDATA%\Chronos\` | aucune | aucune (tous les mtimes antérieurs au début de session) |
| Fichiers SOURCE avec `RefreshAsync` | exactement 2 | **2** |
| Requête réseau depuis un test | aucune | aucune (tout passe par `FakeHttpMessageHandler`) |
| Overlay lancé | **NON** | jamais lancé |
| Dépendance NuGet ajoutée | aucune | aucune (aucun `.csproj` touché) |

## Verification

- [x] `dotnet test Chronos.sln -v q --nologo` : **724/724, 0 échec**, deux passes consécutives, 2 s
- [x] 5 gardes permanentes vertes (`ServicesLayerPurityTests`, `CompositionRootTests`,
      `NormalisationUniqueTests`, `GardesDoctrineTests`, `La_sonde_d_en_tetes_est_le_PRIMAIRE…`) — 17 tests
- [x] `grep -rho "Source = SourceUsage\." src/Chronos/Services` = **5**
- [x] `grep -c "SourceUsage? Source" src/Chronos/Models/WindowState.cs` = **1**
- [x] `grep -c "Source = null" src/Chronos/Services/DoctrineFraicheur.cs` = **1**
- [x] `grep -c "Inconnue" src/Chronos/Models/SourceUsage.cs` = **0** (et 0 dans tout `Models/`)
- [x] Membres de l'enum = **5** ; `public required` sur `WindowState` = **2** (inchangé)
- [x] `CompositeUsageProvider.cs` et `UsageSnapshot.cs` **absents** du diff `4ed9d83..HEAD`
- [x] `grep -c "using System.Windows" src/Chronos/Text/LibelleSource.cs` = **0**
- [x] `grep -c "non renseignée" src/Chronos/Text/LibelleSource.cs` = **1**
- [x] `grep -c "de date inconnue" src/Chronos/Text/LibelleSource.cs` = **1**
- [x] `grep -c "Enum.GetValues" tests/Chronos.Tests/LibelleSourceTests.cs` = **1**
- [x] `grep -c "Collection(" tests/Chronos.Tests/LibelleSourceTests.cs` = **0**
- [x] `grep -rn --include=*.cs --include=*.xaml "EstimatedTokens" src/Chronos` = **0 ligne**
- [x] `grep -c "EstimatedTokens" tests/Chronos.Tests/GardesDoctrineTests.cs` = **2** (≥ 1 exigé)
- [x] `grep -c "EstimatedTokens" tests/Chronos.Tests/CadranBindingTests.cs` = **0**
- [x] Aucun `using System.Windows` introduit dans `Services/`, `Models/` ou `Text/` (les 8 occurrences
      sont préexistantes et dans les fichiers allowlistés par `ServicesLayerPurityTests`)
- [x] 9 mutations jouées, 9 rouges, 9 révoquées, `git diff src/` propre après révocation

## Commits

| Task | Commit | Message |
|---|---|---|
| 1 | `e9cd825` | `feat(20-02): SourceUsage posee la ou elle survit - les 5 producteurs (EXA-06)` |
| 2 | `2185d68` | `feat(20-02): le vocabulaire francais du releve, calcule UNE fois (EXA-06)` |
| 3 | `7348f32` | `refactor(20-02): mort du champ mort - la garde passe du comportement a la structure` |

## Ce que la suite de la phase peut tenir pour acquis

1. **Le snapshot qui arrive au ViewModel sait nommer son producteur**, et il ne ment pas : une fenêtre
   indisponible ne nomme personne. Le plan 05 peut donc écrire un diagnostic qui CONSTATE une panne de
   source au lieu d'afficher « exact » sans savoir de qui.
2. **Un seul endroit sait dire « sonde d'en-têtes de rate-limit » et « il y a 3 h 12 ».** Les plans 03
   (infobulle) et 05 (diagnostic) y puisent tous deux — le deuxième consommateur n'aura aucune décision
   de vocabulaire à prendre.
3. **Le champ mort n'est plus une tentation de réemploi** : il est absent, et sa garde est structurelle.
4. **Le motif « champ sur `WindowState`, pas sur `UsageSnapshot` » en est à sa 3ᵉ application.** C'est
   désormais la manière d'ajouter une information par fenêtre dans ce dépôt, et le test de survie aux
   trois composites est le motif de preuve associé.

## Self-Check: PASSED

3 fichiers annoncés comme créés : `src/Chronos/Models/SourceUsage.cs`,
`src/Chronos/Text/LibelleSource.cs`, `tests/Chronos.Tests/LibelleSourceTests.cs` — tous présents sur
disque. 3 commits annoncés (`e9cd825`, `2185d68`, `7348f32`) : tous présents dans l'historique git.
Aucun élément manquant.
