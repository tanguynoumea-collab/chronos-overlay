---
phase: 20-honn-tet-visible-cadran-diagnostic
plan: 03
subsystem: viewmodels
tags: [exa-03, exa-06, del-04, doctrine, garde-structurelle, non-retour, vocabulaire-fr]

# Dependency graph
requires:
  - phase: 19
    provides: "ProvenanceReleve (Frais / EncoreValide / PlancherAvecActivite) et la doctrine qui statue par fenêtre"
  - phase: 20
    plan: 02
    provides: "SourceUsage, WindowState.Source et LibelleSource — le vocabulaire FR calculé en un point unique"
provides:
  - "WindowGaugeViewModel.EstPlancher (ex-marque d'estimation) : le nom dit enfin le fait"
  - "WindowGaugeViewModel.EstDate : « ce chiffre est daté », RAPPORTÉ par la doctrine, jamais recalculé"
  - "WindowGaugeViewModel.SourceDuReleve / InstantDuReleve / ProvenanceDuReleve : le couple (qui, depuis quand, quoi vérifié)"
  - "MainViewModel.AfficherReleveDate : marque d'âge globale et conservatrice"
  - "MainViewModel.InfobulleReleve : le texte qui nomme la source et l'ancienneté de CHAQUE fenêtre"
  - "Garde de source : aucun seuil d'ancienneté ne peut repousser dans les deux ViewModels de la doctrine"
affects: [20-04, 20-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Garde de source SCOPÉE nominativement à deux fichiers plutôt qu'à un dossier : une garde rouge sur du code correct est une garde qu'on apprend à ignorer"
    - "Une propriété de présentation non bindée n'est PAS [ObservableProperty] : SourceDuReleve / InstantDuReleve / ProvenanceDuReleve sont de simples { get; private set; }"
    - "Recomposition d'un texte coûteux à la cadence des DONNÉES (60 s) et non à celle de l'AFFICHAGE (1 s), avec le retard qui en découle écrit en commentaire plutôt que laissé à deviner"

key-files:
  created: []
  modified:
    - src/Chronos/ViewModels/WindowGaugeViewModel.cs
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/ViewModels/CadranPreviewViewModel.cs
    - src/Chronos/Models/UsageSnapshot.cs
    - src/Chronos/Views/Cadrans/CadranBraisesView.xaml
    - src/Chronos/Views/Cadrans/CadranFusibleView.xaml
    - src/Chronos/Views/Cadrans/CadranMareeView.xaml
    - src/Chronos/Views/Cadrans/CadranVoletsView.xaml
    - tests/Chronos.Tests/WindowGaugeViewModelTests.cs
    - tests/Chronos.Tests/MainViewModelTests.cs
    - tests/Chronos.Tests/CadranBindingTests.cs
    - tests/Chronos.Tests/GardesDoctrineTests.cs

key-decisions:
  - "La doctrine a l'AUTORITÉ : le seuil concurrent de 2 min du ViewModel n'a pas été aligné sur les 6 min de LimiteAge, il a été SUPPRIMÉ — aligner deux autorités les laisse diverger au refactor suivant"
  - "La garde de non-retour balaie DEUX fichiers nommés et non le dossier ViewModels : SessionsViewModel compare légitimement des durées, une garde rouge sur du code correct serait inexploitable"
  - "UsageSnapshot.SourceCapturedAt CONSERVÉ et son commentaire amendé : ce champ dit quelque chose de vrai (ancienneté de SOURCE), ce qui est mort c'est la seconde notion de « périmé » que le ViewModel en dérivait"
  - "EstDate et EstPlancher se COMPOSENT (plancher ⊂ daté) : un EncoreValide est daté SANS être un plancher, le griser serait mentir sur une preuve positive"
  - "AfficherReleveDate est GLOBALE et conservatrice (le plus vieux des deux) : l'âge est une propriété du PIPELINE, pas de la fenêtre ; le détail par fenêtre passe par l'infobulle"
  - "La matière brute de DEL-04 devient visible À LA DEMANDE (infobulle) et non en permanence : une ligne de tokens au centre annulerait la décision de design « centre épuré » de la v1.3 pour une exigence qui ne la demande pas"
  - "MajInfobulleReleve n'est PAS appelée depuis Interpolate : recomposer une chaîne chaque seconde pour un texte visible au survol serait du travail permanent pour un affichage occasionnel"

patterns-established:
  - "Le renommage d'une propriété bindée se fait dans UN SEUL commit avec ses 8 bindings XAML et ses 11 assertions : tests/Chronos.Tests porte un ProjectReference vers Chronos, une erreur de compilation rend dotnet test non invocable"
  - "Un commentaire ne reproduit jamais le littéral qu'un critère grep == 0 interdit (3e application consécutive après 20-01 et 20-02) — ici « l'ancienne marque d'estimation »"

requirements-completed: []

# Metrics
duration: 16min
completed: 2026-09-12
---

# Phase 20 Plan 03 : `EstPlancher`, mort d'`IsStale`, `EstDate` et infobulle Summary

**Il ne reste qu'UNE notion de « périmé » dans Chronos — celle de la doctrine, dérivée et non réglable —
et une garde de source empêche la seconde de repousser ; la propriété qui pilote la texture des quatre
styles de cadran porte enfin le nom du fait qu'elle décrit ; et le ViewModel expose désormais « ce chiffre
est daté » et « voici qui l'alimente, depuis quand » comme des faits RAPPORTÉS, jamais recalculés.**

## Performance

- **Duration:** 16 min
- **Started:** 2026-09-12T06:40:00Z
- **Completed:** 2026-09-12T06:56:00Z
- **Tasks:** 3/3
- **Files modified:** 12 (0 créé, 12 modifiés)

## Bilan de la suite : chaque écart nominatif

| | Tests | Durée |
|---|---|---|
| Entrée (commit `7af27be`) | 724 | 2 s |
| Sortie (commit `2aeca15`) | **733** | **2 s** |

| Écart | Tests | Où |
|---|---|---|
| `IsStale_vrai_quand_la_capture_depasse_deux_minutes` | **−1** | `MainViewModelTests` |
| `EstDate` rapporté (les 4 cas) + composition `plancher ⊂ daté` + le couple (qui, depuis quand) | +3 | `WindowGaugeViewModelTests` |
| Garde de source « aucun seuil d'ancienneté » | +1 | `GardesDoctrineTests` |
| Marque d'âge globale (nominal, une seule datée, deux indisponibles) | +3 | `MainViewModelTests` |
| Infobulle (les deux fenêtres nommées, tokens bruts, source absente) | +3 | `MainViewModelTests` |
| **Total** | **+9** | 724 → 733 |

Comptage par classe, réconcilié à l'unité : `MainViewModelTests` **47** (42 − 1 + 6),
`WindowGaugeViewModelTests` **22** (19 + 3), `GardesDoctrineTests` **8** (7 + 1),
`CadranBindingTests` **13** (inchangé — seul un nom de propriété y a bougé).

**Écart au plan, justifié :** le plan annonçait `−1 + ≈ 12`, dont « 4 sur `EstDate` ». Il y en a **3**, et
c'est délibéré : les **quatre** cas de `<behavior>` (Frais, EncoreValide, PlancherAvecActivite, provenance
`null`) sont prouvés **dans un seul `[Fact]`**, par une fonction locale appelée quatre fois. Les scinder en
quatre `[Fact]` aurait quadruplé le montage pour la même couverture — et surtout dissous la propriété que
le test veut établir : c'est la **comparaison** des quatre sorties, sur un horodatage identique et
volontairement très ancien, qui prouve que le ViewModel **rapporte** au lieu de juger. Aucune assertion du
plan n'a été abandonnée ; les 4 cas sont assertés nominativement.

**Deux passes complètes consécutives : 733/733 les deux fois** (précédent BAML 16-03 ; `CadranBindingTests`
charge du XAML).

## Accomplishments

### Task 1 — le nom ment depuis la phase 19, il ne ment plus

- `IsEstimated` → **`EstPlancher`**. Depuis la phase 19, `SourceReliability.Estimated` n'est produit qu'en
  **un seul endroit** de la production — la branche 3 de la doctrine. La propriété signifiait déjà
  « plancher » ; dans une phase intitulée « honnêteté visible », garder le nom d'un concept supprimé était
  une contradiction interne.
- **8 bindings XAML** renommés (2 par style : braises, fusible, marée, volets). Le nom de la **DP** des
  contrôles (`Estimated`) n'a **pas** bougé : seule la source du binding change. Le renommage des DP reste
  explicitement différé.
- **11 assertions** réécrites, **2 tests renommés** (`Fenetre_exacte_ne_porte_PAS_la_marque_de_plancher…`,
  `Fenetre_plancher_rallume_la_marque`) et le commentaire de classe amendé : le « badge estimée » n'existe
  plus depuis le centre épuré de la v1.3.
- **`PercentFormatter.Format(double?, bool)` CONSERVÉE** : la galerie `--cadrans` pilote des booléens
  d'**aperçu** sans provenance. La supprimer aurait cassé la galerie pour rien.
- **Tout en un seul commit** : `tests/Chronos.Tests` porte un `ProjectReference` vers `Chronos`. Un
  renommage scindé aurait produit un commit où `dotnet test` n'est même pas invocable.
- **Consigne de rédaction tenue** : le littéral interdit n'apparaît dans aucun commentaire de `src/` ni de
  `tests/` — on y lit « l'ancienne marque d'estimation ». 3ᵉ application consécutive de la doctrine de la
  phase 19, après 20-01 (`UniformGrid`) et 20-02 (`Inconnue`).

### Task 2 — une seule notion de « périmé », et une garde pour qu'elle le reste

- **Le point n'était pas « 2 min ou 6 min », c'était QUI a l'autorité.** `MainViewModel` perd
  `CapturedAt`, `IsStale`, et les deux lignes qui les alimentaient. Aucune des quatre n'avait d'autre
  consommateur que le test supprimé.
- `WindowGaugeViewModel.EstDate` est une **affectation directe** depuis `s.Provenance`. Aucune soustraction
  d'horodatage, aucune constante de durée : c'est la garantie **structurelle** qu'un second seuil ne peut
  pas réapparaître.
- **`UsageSnapshot.SourceCapturedAt` CONSERVÉ** (encore asserté par des tests de providers), commentaire
  amendé : il n'est plus « en attente de retrait », il est l'ancienneté de **source**, distincte de la
  fraîcheur **par fenêtre**. Ce qui est mort, c'est la seconde notion de « périmé » qu'on en dérivait.
- **Le test supprimé a TROIS remplaçants**, et le commentaire laissé à sa place les nomme un par un — un
  lecteur futur n'a pas à faire confiance à un trou.
- **La garde balaie exactement DEUX fichiers**, nommés : `MainViewModel.cs` et `WindowGaugeViewModel.cs`.
  Elle exige que les deux existent **et** dépassent 1 000 octets (une garde qui ne lit rien est muette).
  Son commentaire **dit pourquoi** elle s'arrête là : `SessionsViewModel` compare légitimement des durées
  (l. 190-191) pour formater l'ancienneté d'une session du widget. Une garde à l'échelle du dossier aurait
  été rouge sur du code correct, donc inexploitable.
- Les **deux sens** de la comparaison sont couverts par le motif (`âge > seuil` et `seuil < âge`). Les
  3 occurrences restantes de `TimeSpan.From` dans `MainViewModel` sont deux constructions de fenêtre et un
  `Interval =` — aucune n'est adjacente à un opérateur de comparaison.

### Task 3 — la marque d'âge et l'infobulle qui nomme la source

- **`AfficherReleveDate` posée dans `MajPastilles`**, le point de recomposition **UNIQUE** — `grep` le
  confirme : une seule affectation dans tout le fichier. La méthode recompose désormais **quatre** marques
  et son `<summary>` le dit.
- **Règle conservatrice** : la marque s'allume dès que l'**une** des deux fenêtres est datée. Le plus vieux
  des deux, jamais le plus jeune. Une synthèse conservatrice n'est pas un mensonge ; une synthèse optimiste
  en serait un.
- **Marque ADDITIVE, jamais soustractive** : un `EncoreValide` est un chiffre **prouvé** juste (la doctrine
  est allée vérifier qu'aucune réponse assistant n'était survenue depuis la capture). Le voiler serait une
  régression d'honnêteté — et c'est asserté (`EstDate && !EstPlancher`).
- **`InfobulleReleve`** : une ligne par fenêtre — source, ancienneté, ce que la doctrine a vérifié, et la
  matière brute de DEL-04 quand elle existe. **Aucun mapping FR local** : les trois libellés viennent de
  `LibelleSource`, qui trouve ici son **premier consommateur en production**.
- **`TokensText` / `HasTokens` cessent d'être du code calculé pour personne** — dette n° 1 léguée par la
  phase 19, levée par une décision écrite plutôt que par un héritage silencieux : la **marque** (`≥` +
  texture) reste permanente, la **matière brute** devient visible **à la demande**.
- **Jamais convertie en points de pourcentage** (EXA-04), et c'est asserté :
  `Assert.DoesNotContain("%", vm.InfobulleReleve)` sur un cas plancher à 643 649 933 tokens.
- **Appelée une fois par rafraîchissement (≈ 60 s), jamais depuis `Interpolate` (1 s)** : `grep` le
  confirme, le corps d'`Interpolate` n'en contient aucune occurrence. Le retard d'un tick qui en découle
  est **écrit** dans la XML-doc plutôt que laissé à deviner.
- **Aucune arithmétique d'horodatage** dans `MajInfobulleReleve` : `LibelleSource.Anciennete` fait tout le
  travail, et la garde de la tâche 2 reste verte par construction.

## Falsifiabilité jouée — 3 mutations, 3 rouges, 3 révoquées

TDD RED strict reste impossible dans ce dépôt (`ProjectReference` : un test référençant un type inexistant
empêche TOUTE la solution de compiler, l'étape RED serait un commit où `dotnet test` n'est pas invocable).
Doctrine déjà écrite, précédents 17-04, 17-05, 18-01, 18-02, 20-01, 20-02. La preuve passe par mutation.

| # | Mutation | Test visé | Résultat |
|---|---|---|---|
| M10 | La garde de source cesse de mordre : un seuil d'ancienneté réintroduit dans `MainViewModel.Interpolate` | `Aucun_seuil_d_anciennete_n_est_calcule_dans_les_ViewModels_de_la_doctrine` | **ROUGE** |
| M11 | Synthèse OPTIMISTE : `\|\|` devient `&&` (« le plus jeune des deux ») | `Une_seule_fenetre_datee_suffit_a_allumer_la_marque` + `Une_fenetre_plancher_joint_son_compte_de_tokens_BRUT…` | **ROUGE** (2) |
| M12 | `MajInfobulleReleve()` n'est plus appelée depuis `ApplySnapshot` | les 3 tests d'infobulle | **ROUGE** (3) |

**M10 mérite un mot** : le plan prescrivait de réintroduire *littéralement*
`IsStale = CapturedAt is { } c && (now - c) > TimeSpan.FromMinutes(2);`. Cette ligne ne compile plus — les
deux propriétés n'existent plus — et la garde lit le **texte source**, donc la solution doit compiler pour
que le test s'exécute. La mutation jouée est l'**équivalent compilable** :
`_ = _last?.SourceCapturedAt is { } c && (now - c) > TimeSpan.FromMinutes(2);` — même forme syntaxique,
même verdict attendu, et c'est bien cette forme-là que la garde doit attraper. Après révocation,
`grep -c "TimeSpan.FromMinutes" src/Chronos/ViewModels/MainViewModel.cs` rend **0** et le diff des trois
commits ne contient aucune des trois mutations.

## Deviations from Plan

### Ajustements auto-appliqués

**1. [Rule 3 - Blocage] La mutation prescrite par le plan ne compilait pas**

- **Found during:** Task 2
- **Issue:** Le plan demandait de réintroduire textuellement la ligne `IsStale = …` pour vérifier que la
  garde mord. Or la tâche 2 venait de supprimer `IsStale` **et** `CapturedAt` : la ligne ne compile plus,
  et une solution qui ne compile pas ne permet pas d'exécuter la garde.
- **Fix:** Mutation équivalente et compilable, portant exactement le motif que la garde traque
  (`> TimeSpan.FromMinutes(2)` adjacent à une comparaison d'horodatage). Rouge constaté, révoquée,
  absence vérifiée par `grep`.
- **Files modified:** `src/Chronos/ViewModels/MainViewModel.cs` (temporairement)
- **Commit:** aucun — la mutation n'a jamais été committée.

**2. [Rule 2 - Fonctionnalité critique] `ProvenanceDuReleve` livrée en tâche 3 et non en tâche 2**

- **Found during:** Task 3
- **Issue:** Le plan pose `SourceDuReleve` / `InstantDuReleve` en tâche 2 et mentionne
  `ProvenanceDuReleve` en tâche 3, sans l'inscrire dans la liste des propriétés du VM de la tâche 2. Sans
  elle, `MajInfobulleReleve` ne peut pas appeler `LibelleSource.Provenance`, donc le critère
  `grep -c "LibelleSource" >= 3` est inatteignable et l'infobulle perd l'axe « ce qui a été vérifié ».
- **Fix:** `public ProvenanceReleve? ProvenanceDuReleve { get; private set; }` ajoutée en tâche 3, sur le
  même motif que les deux autres (non observable, alimentée dans `Apply`).
- **Files modified:** `src/Chronos/ViewModels/WindowGaugeViewModel.cs`
- **Commit:** `2aeca15`

### Écarts constatés (aucune action)

- **Les numéros de ligne de `CadranBindingTests` donnés par le plan (81, 118, 119) étaient périmés** :
  les trois assertions vivent aux lignes 112, 149 et 150. Les trois ont été trouvées et réécrites ; le
  critère réel (`grep IsEstimated == 0`) est tenu.
- **Aucune tâche n'a atteint la limite de 3 auto-corrections.**

## Requirements : EXA-03 laissé Pending

Le frontmatter du plan porte `requirements: [EXA-03]`. Ce plan livre le **contrat de présentation** complet
d'EXA-03 — `EstDate` par fenêtre, `AfficherReleveDate` en synthèse, `InfobulleReleve` en détail — mais
EXA-03 est rédigé côté UTILISATEUR : « **distinction visuelle** frais / daté / indisponible ». **Aucun
pixel n'a bougé** : `AfficherReleveDate` et `InfobulleReleve` ne sont bindés dans aucun XAML à ce commit,
et le plan lui-même le dit (« Rien n'est encore à l'écran — c'est le plan 04 »).

Cocher ici déclarerait satisfait sur la foi d'un plan qui n'a touché aucune surface visible.
**Précédent explicite et répété : 17-01, 18-02, 19-01, 19-02, 20-01, 20-02.** EXA-03 sera coché par le
plan 04 (dessin), EXA-06 par le plan 05 (diagnostic).

## Known Stubs

**Deux propriétés de présentation sont calculées, testées et bindées nulle part à ce commit :**

| Propriété | Fichier | Ligne | Raison |
|---|---|---|---|
| `AfficherReleveDate` | `src/Chronos/ViewModels/MainViewModel.cs` | 44-55 | Le dessin de la marque est **le plan 20-04**, nommément. |
| `InfobulleReleve` | `src/Chronos/ViewModels/MainViewModel.cs` | 57-65 | Le `ToolTip` du cadran est **le plan 20-04**, nommément. |

Ce n'est pas un stub au sens d'un code mort hérité en silence : c'est la **frontière explicite** de ce plan
(`<done>` de la tâche 3), la dette est nominative et son échéance est le plan suivant de la même phase. La
différence avec la dette n° 1 de la phase 19 — que ce plan vient précisément de lever — est que celle-ci
était **non datée et non attribuée**.

Aucune valeur codée en dur, aucun texte « à venir », aucune donnée factice : les deux propriétés sont
alimentées par le pipeline réel à chaque rafraîchissement.

## Vérifications de sécurité (contrôle avant / après)

| Contrôle | Attendu | Constaté après |
|---|---|---|
| `%APPDATA%\Chronos\oauth.dat` | 518 o, mtime 1783863147 | **518 1783863147** — identique |
| `~/.claude/settings.json` | 6872 o, mtime 1785403369 | **6872 1785403369** — identique |
| Écriture sous `%APPDATA%\Chronos\` | aucune | aucune (tous les mtimes antérieurs au début de session) |
| Fichiers SOURCE avec `RefreshAsync` | exactement 2 | **2** (`ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs`) |
| Requête réseau depuis un test | aucune | aucune (aucun test de ce plan ne touche au transport) |
| Appel de refresh avec le jeton RÉEL | jamais | jamais |
| Overlay lancé | **NON** | jamais lancé |
| `LoginClaudeCommand` bindée | jamais | intouchée (aucun XAML de commande modifié) |
| Dépendance NuGet ajoutée | aucune | aucune (aucun `.csproj` touché) |

## Verification

- [x] `dotnet test Chronos.sln -v q --nologo` : **733/733, 0 échec**, **deux passes consécutives**, 2 s
- [x] 5 gardes permanentes vertes (`ServicesLayerPurityTests`, `CompositionRootTests`,
      `NormalisationUniqueTests`, `GardesDoctrineTests`, `La_sonde_d_en_tetes_est_le_PRIMAIRE…`) — **18 tests**
      (17 auparavant + la nouvelle garde de source)
- [x] `grep -rn --include=*.cs --include=*.xaml "IsEstimated" src/Chronos tests/Chronos.Tests` = **0 ligne**
- [x] `grep -rn --include=*.cs --include=*.xaml "IsStale" src/Chronos tests/Chronos.Tests` = **0 ligne**
- [x] `EstPlancher` dans `src/Chronos/Views/Cadrans/*.xaml` = **8**
- [x] `grep -c "EstPlancher" src/Chronos/ViewModels/CadranPreviewViewModel.cs` = **1**
- [x] `PercentFormatter.Format(double?, bool)` **CONSERVÉE** (surcharge historique de la galerie)
- [x] `Etat_fiabilite_mixte_la_marque_du_pourcentage_est_par_fenetre` passe **sans modification de sa logique**
- [x] `grep -c "CapturedAt" src/Chronos/ViewModels/MainViewModel.cs` = **0**
- [x] `grep -c "EstDate = s.Provenance is" src/Chronos/ViewModels/WindowGaugeViewModel.cs` = **1**
- [x] `grep -c "SourceCapturedAt" src/Chronos/Models/UsageSnapshot.cs` = **1** (champ CONSERVÉ)
- [x] Les 3 `TimeSpan.From` restants de `MainViewModel` (l. 40, 41, 409) : **aucun** opérateur `<`/`>`/`<=`/`>=` adjacent
- [x] `grep -c "AfficherReleveDate =" src/Chronos/ViewModels/MainViewModel.cs` = **1**
- [x] `grep -c "MajInfobulleReleve()" src/Chronos/ViewModels/MainViewModel.cs` = **2** (déclaration + unique appel)
- [x] Aucune occurrence de `MajInfobulleReleve` dans le corps d'`Interpolate` (vérifié par lecture du corps)
- [x] `grep -c "LibelleSource" src/Chronos/ViewModels/MainViewModel.cs` = **6** (≥ 3 exigé)
- [x] `grep -rn --include=*.cs "TokensDepuisReleve.*100\|TokensText.*%" src/Chronos/ViewModels` = **0 ligne**
- [x] Aucun converter enum→Brush, aucune couleur en dur, aucun arc de delta, aucune barre d'erreur
- [x] `dotnet build src/Chronos` : **0 avertissement, 0 erreur**
- [x] 3 mutations jouées, 3 rouges, 3 révoquées, `git diff` propre après révocation

## Commits

| Task | Commit | Message |
|---|---|---|
| 1 | `e9c4f20` | `refactor(20-03): IsEstimated devient EstPlancher - la propriete porte le nom du fait` |
| 2 | `8b9dce6` | `feat(20-03): une seule notion de perime, et une garde pour qu'elle le reste (EXA-03)` |
| 3 | `2aeca15` | `feat(20-03): marque du releve date et infobulle qui nomme la source (EXA-03, EXA-06)` |

## Ce que le plan 04 peut tenir pour acquis

1. **Le contrat de présentation est complet et stable.** Quatre signaux à binder, tous posés dans des
   points de recomposition uniques : `EstPlancher` (par fenêtre, déjà bindé), `EstDate` (par fenêtre,
   à binder), `AfficherReleveDate` (global, à binder), `InfobulleReleve` (texte, à binder en `ToolTip`).
2. **`EstPlancher` et `EstDate` sont indépendants et composables.** Le dessin doit pouvoir les superposer :
   un plancher est aussi daté, un `EncoreValide` est daté sans être un plancher. Les deux cas sont testés.
3. **Aucune décision de vocabulaire ne reste à prendre.** `LibelleSource` dit « sonde d'en-têtes de
   rate-limit », « relevé il y a 3 h 12 » et « borne inférieure (activité depuis) » — le plan 04 affiche,
   il ne formule pas.
4. **Aucun jugement d'ancienneté ne peut plus entrer dans ces deux ViewModels**, et un test le fait
   tomber. Si le dessin a besoin d'un seuil, c'est que la doctrine doit le fournir.
5. **La contrainte dure de la phase 19 tient toujours** : `Utilization` n'est jamais gonflée, il n'existe
   aucune borne supérieure à représenter, donc **pas d'arc de delta et pas de barre d'erreur**.

## À VÉRIFIER PAR L'UTILISATEUR

Rien de nouveau pour ce plan : aucune surface visible n'a bougé. Les points hors GSD des phases 17 et 19
restent ouverts et sont rappelés dans `STATE.md`.

## Self-Check: PASSED

12 fichiers annoncés comme modifiés : tous présents sur disque et tous présents dans le diff
`7af27be..2aeca15`. 3 commits annoncés (`e9c4f20`, `8b9dce6`, `2aeca15`) : tous présents dans l'historique
git. Aucun fichier créé n'est annoncé. Aucun élément manquant.
