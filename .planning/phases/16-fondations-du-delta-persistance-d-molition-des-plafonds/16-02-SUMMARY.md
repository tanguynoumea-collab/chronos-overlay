---
phase: 16-fondations-du-delta-persistance-d-molition-des-plafonds
plan: 02
subsystem: services
tags: [persistence, atomic-write, tolerant-parsing, decorator, tdd, usage-pipeline]

# Dependency graph
requires:
  - phase: 03-pipeline-de-donnees
    provides: IUsageProvider, UsageSnapshot, WindowState, SourceReliability, ChronosPaths, IClock
provides:
  - ChronosPaths.LastExactFile — %APPDATA%\Chronos\last-exact.json résolu par propriété calculée
  - WindowState.CapturedAt — horodatage de capture PAR FENÊTRE (les deux fenêtres peuvent venir de sources différentes)
  - LastExactStore — persistance atomique et tolérante du dernier relevé exact, schéma versionné (v1)
  - LastExactUsageProvider — décorateur IUsageProvider écrivain unique + rebouchage des fenêtres indisponibles
affects: [16-03 câblage DI de la chaîne, 19 doctrine du composite + limite d'âge EXA-02 + correction par delta DEL-03/04, 20 affichage de l'âge du relevé]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Écriture atomique temp-unique-par-process + File.Move(overwrite) — motif du dépôt (SettingsService/ArchiveStore) réutilisé tel quel"
    - "Persistance MINIMALE : tout champ dérivable (fraction, fiabilité) est recalculé au chargement, jamais relu du disque"
    - "Décorateur IUsageProvider en tête de chaîne comme point d'écriture unique, plutôt qu'abonnement à un événement d'orchestrateur"
    - "TDD RED/GREEN avec squelette compilable : le commit RED laisse la solution buildable (stub NotImplementedException), les tests échouent à l'exécution et non au build"

key-files:
  created:
    - src/Chronos/Services/LastExactStore.cs
    - src/Chronos/Services/LastExactUsageProvider.cs
    - tests/Chronos.Tests/LastExactStoreTests.cs
    - tests/Chronos.Tests/LastExactUsageProviderTests.cs
  modified:
    - src/Chronos/Services/ChronosPaths.cs
    - src/Chronos/Models/WindowState.cs

key-decisions:
  - "Une fenêtre n'est persistable que si elle est Exact ET porte à la fois utilization ET resets_at : sans resets_at on ne pourrait ni détecter la remise à zéro ni recalculer la géométrie — on servirait un chiffre invérifiable."
  - "Save ne crée pas de fichier quand il n'y a rien d'exact à mémoriser et rien à préserver : deux passages à vide ne laissent aucune trace sur disque."
  - "La relecture de fusion de Save est BRUTE (sans la garde resets_at <= now) : la fusion ne doit pas purger silencieusement une fenêtre que seul l'instant présent invalide."
  - "Le décorateur rend l'instance de snapshot d'ORIGINE quand aucune substitution n'a lieu (ReferenceEquals) : zéro allocation et zéro effet de bord sur le chemin nominal."
  - "Le try/catch du décorateur enveloppe Save ET Load : un magasin en échec (disque plein, fichier verrouillé) ne prive jamais l'utilisateur de son affichage."

patterns-established:
  - "Dans une classe exposant une propriété Path, qualifier System.IO.Path/File/Directory en entier — la propriété d'instance masque le type statique."
  - "Tests de persistance : dossier temp unique par instance de classe de test + Dispose best-effort ; aucun test ne touche le vrai %APPDATA%\\Chronos."

requirements-completed: [EXA-01]

# Metrics
duration: 9min
completed: 2026-09-09
---

# Phase 16 Plan 02 : Magasin persistant du dernier relevé exact — Summary

**Le dernier relevé exact de chaque fenêtre est désormais persistable sur disque avec son horodatage de capture propre, relisible après redémarrage du processus, et rebouchable dans un snapshot troué par un décorateur `IUsageProvider` qui n'écrase jamais une réponse vivante ni ne ressuscite un pourcentage dont le reset est passé.**

## Performance

- **Duration:** 9 min
- **Started:** 2026-09-09T11:53:16Z
- **Completed:** 2026-09-09T12:02:52Z
- **Tasks:** 2 / 2 (4 commits — cycle TDD RED/GREEN par tâche)
- **Files created:** 4 — **Files modified:** 2
- **Tests:** 384 → **406** (+22 : 13 `LastExactStoreTests`, 9 `LastExactUsageProviderTests`), **0 échec**

## Accomplishments

- **Le chiffre exact survit au redémarrage du processus.** `LastExactStore.Save` écrit
  `%APPDATA%\Chronos\last-exact.json` (schéma versionné `version: 1`, clés `five_hour` / `seven_day`,
  dates ISO 8601 avec offset), de façon atomique — temp unique par process puis `File.Move(overwrite: true)`.
  `Load` relu depuis un **nouvel objet magasin** restitue `utilization`, `resets_at` et `captured_at`.
  C'est l'instant T de référence sans lequel la correction par delta de la phase 19 est impossible.

- **`captured_at` est porté PAR FENÊTRE.** `WindowState.CapturedAt` (champ additif nullable, aucun site de
  construction cassé) : les deux fenêtres peuvent venir de sources différentes à des instants différents,
  un horodatage unique au niveau snapshot aurait menti sur l'âge de l'une des deux. Test dédié : T1 ≠ T2
  restitués séparément.

- **Aucun chiffre n'est inventé au rechargement.** Trois gardes, chacune testée :
  1. `Reliability` **n'est pas persistée** — forcée à `Exact` à la reconstruction, donc un fichier corrompu
     ou édité à la main ne peut pas injecter une fausse exactitude ;
  2. `FractionTimeRemaining` **n'est pas persistée** — recalculée via `WindowState.FractionRemaining(resets, now, longueur)`
     (5 h / 7 j) ; une fraction présente dans le JSON est **ignorée** (test : 0,99 dans le fichier → 0,6 recalculé) ;
  3. `resets_at <= now` → la fenêtre est **rejetée** : un « 80 % » dont le reset est passé ne décrit plus rien.

- **Dégradation silencieuse, jamais de crash.** Fichier absent, JSON corrompu, `version: 999` → `null`,
  aucune exception qui remonte. Le magasin illisible n'est pas une panne de Chronos, c'est l'absence d'un
  relevé de secours.

- **Écriture non destructive par fenêtre.** Un `Save` dont l'hebdo est `Unavailable` laisse **intacte**
  l'hebdo exacte précédemment persistée : on n'efface jamais un chiffre connu avec une absence de donnée —
  c'est précisément l'absence de donnée que ce magasin sert à traverser.

- **Le décorateur est l'écrivain unique et ne fait QUE reboucher.** `LastExactUsageProvider` délègue,
  écrit chaque fenêtre exacte, et substitue **uniquement** les fenêtres `Unavailable` depuis un relevé encore
  valide, en conservant leur `CapturedAt` d'origine. Une réponse vivante (même `Estimated`) n'est jamais
  remplacée ; une fenêtre `Estimated` n'est ni écrite ni substituée ; aucun `SourceCapturedAt` n'est inventé.
  `CompositeUsageProvider` et sa règle `Best()` sont intacts (la doctrine reste la phase 19).

## Task Commits

Cycle TDD par tâche — le commit RED laisse volontairement la solution **buildable** (squelette
`NotImplementedException`), conformément au principe « chaque commit compile » établi au plan 16-01 :

| # | Étape | Commit | Résultat |
|---|---|---|---|
| 1 | **RED** — `ChronosPaths.LastExactFile`, `WindowState.CapturedAt`, squelette `LastExactStore`, 13 tests | `ea99b46` | 12 échecs / 13 (le 13ᵉ, l'isolation du chemin, passe déjà) |
| 1 | **GREEN** — implémentation de `LastExactStore` | `c1b817f` | 13/13 verts, suite 397 |
| 2 | **RED** — squelette `LastExactUsageProvider`, 9 tests | `dcb2067` | 9 échecs / 9 |
| 2 | **GREEN** — implémentation du décorateur | `329b695` | 9/9 verts, suite **406** |

Aucune étape REFACTOR n'a été nécessaire : les deux implémentations sont passées vertes sans dette à nettoyer.

## Files Created/Modified

### Créés (4)

| Fichier | Lignes | Rôle |
|---|---|---|
| `src/Chronos/Services/LastExactStore.cs` | 178 | Magasin persistant, schéma versionné, écriture atomique, lecture tolérante, garde de reset, recalcul de la fraction. Expose `SchemaVersion`, `Path`, `Save`, `Load` + le record `LastExactWindows`. |
| `src/Chronos/Services/LastExactUsageProvider.cs` | 84 | Décorateur `IUsageProvider` : écrivain unique + rebouchage strict des fenêtres indisponibles. |
| `tests/Chronos.Tests/LastExactStoreTests.cs` | 238 | 13 tests — round-trip, deux `captured_at`, fenêtre non-Exact ignorée, fusion non destructive, absent/corrompu/version inconnue, fenêtre roulée (une puis les deux), fraction recalculée / fraction du JSON ignorée, aucun `.tmp-*` résiduel, isolation du chemin. |
| `tests/Chronos.Tests/LastExactUsageProviderTests.cs` | 228 | 9 tests — écriture des deux fenêtres exactes, passe-plat, rebouchage avec `CapturedAt` d'origine, non-écrasement d'une réponse vivante, refus de reboucher depuis une fenêtre roulée, portée étroite (`Estimated` ni écrit ni rebouché), magasin corrompu sans exception, aucune écriture inutile, aucun `SourceCapturedAt` inventé. |

### Modifiés (2)

- `src/Chronos/Services/ChronosPaths.cs` — **propriété calculée** `LastExactFile` colocalisée avec `usage.json`,
  sur le modèle exact de `SettingsFile`. Le ctor positionnel `(UsageFile, ProjectsRoot)` est **inchangé** :
  les dizaines de tests qui construisent un dossier temp obtiennent gratuitement un magasin isolé.
- `src/Chronos/Models/WindowState.cs` — champ additif nullable `CapturedAt`. Aucun site de construction cassé
  (tous utilisent des initialiseurs d'objet).

## Schéma persisté

```json
{
  "version": 1,
  "five_hour": { "utilization": 0.42, "resets_at": "2026-09-09T18:30:00+00:00", "captured_at": "2026-09-09T15:12:03+00:00" },
  "seven_day": { "utilization": 0.63, "resets_at": "2026-09-14T09:00:00+00:00", "captured_at": "2026-09-09T15:12:03+00:00" }
}
```

Ne sont **PAS** persistés, et pourquoi : `Reliability` (dérivée — sécurité par construction),
`FractionTimeRemaining` (fonction pure de `resets_at`/`now` — la persister ressusciterait une géométrie
périmée), `EstimatedTokens` (une fenêtre exacte n'en porte jamais, et c'est le comptage que le milestone
abolit), `Kind` (implicite dans la clé).

## Decisions Made

- **Une fenêtre sans `resets_at` n'est pas persistable.** Décision non prévue explicitement par le plan,
  imposée par la Core Value : sans instant de reset on ne peut ni détecter la remise à zéro (garde
  d'honnêteté) ni recalculer la fraction — on servirait un chiffre invérifiable. Même règle au chargement :
  une entrée sans `resets_at` est rejetée.
- **`Save` ne crée pas de fichier vide.** Si rien n'est exact et que rien n'est à préserver, aucune écriture.
  C'est ce qui rend vrai le comportement « deux `GetAsync` à vide ne créent pas de fichier », sans dépendre
  uniquement de la garde du décorateur.
- **La relecture de fusion de `Save` est brute**, sans la garde `resets_at <= now` : la fusion ne doit pas
  purger silencieusement une fenêtre que seul l'instant présent invalide. Prouvé par le test
  `Fenetre_roulee_rejetee_au_chargement_l_autre_survit` (l'écriture accepte, seul `Load` refuse).
- **Le décorateur rend l'instance de snapshot d'origine** quand aucune substitution n'a eu lieu
  (`ReferenceEquals` sur les deux fenêtres) : chemin nominal sans allocation ni effet de bord.
- **`try/catch` du décorateur autour de `Save` ET de `Load`** : un magasin en échec (disque plein, fichier
  verrouillé) doit dégrader vers « snapshot vivant rendu tel quel », jamais vers une exception qui priverait
  l'utilisateur de son affichage.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `using System.IO;` explicite requis dans les fichiers de test**

- **Found during:** Task 1 (première exécution RED)
- **Issue:** `Path`, `File` et `Directory` étaient inconnus dans `LastExactStoreTests.cs` malgré
  `ImplicitUsings=enable` — le jeu d'usings implicites du projet de test (`UseWPF=true`) ne contient pas
  `System.IO`. 15 erreurs CS0103, le RED n'était pas exécutable.
- **Fix:** ajout de `using System.IO;` en tête des deux fichiers de test, conformément à la convention
  déjà appliquée par 5+ fichiers de test du dépôt (`AutostartServiceTests`, `ChronosOAuthStoreTests`, …).
- **Files modified:** `tests/Chronos.Tests/LastExactStoreTests.cs`, `tests/Chronos.Tests/LastExactUsageProviderTests.cs`
- **Verification:** RED exécutable — 12 échecs / 13 puis 9 échecs / 9.
- **Committed in:** `ea99b46` et `dcb2067` (commits RED des tâches correspondantes)

**2. [Rule 3 - Blocking] `System.IO.Path` masqué par la propriété `Path` de `LastExactStore`**

- **Found during:** Task 1 (GREEN)
- **Issue:** la surface publique imposée par le plan expose une propriété d'instance `Path` ; à l'intérieur
  de la classe, `Path.Combine` / `Path.GetDirectoryName` se seraient résolus sur la propriété `string`
  (membre d'instance prioritaire sur le type importé) → erreur de compilation.
- **Fix:** qualification complète `System.IO.Path` / `System.IO.File` / `System.IO.Directory` dans le corps
  de `LastExactStore`, et suppression du `using System.IO;` devenu inutile dans ce fichier.
- **Files modified:** `src/Chronos/Services/LastExactStore.cs`
- **Verification:** `dotnet build` → 0 erreur ; 13/13 tests verts.
- **Committed in:** `c1b817f`

### Écarts assumés (critères d'acceptation littéralement inatteignables)

**1. `grep -c "LastExactFile" src/Chronos/Services/ChronosPaths.cs` → attendu 1.** Le snippet de commentaire
XML fourni **par le plan lui-même** contient le mot `LastExactFile` (« obtiennent aussi un LastExactFile
isolé »), ce qui portait le compte à 2. Le libellé a été aligné sur celui de `SettingsFile`
(« obtiennent aussi un last-exact.json isolé ») : le critère est désormais satisfait à la lettre **et** à
l'esprit (la propriété n'est déclarée qu'une fois), sans perte d'information dans la doc.

**2. `dotnet test --filter "FullyQualifiedName~CompositeUsageProviderTests"` → attendu « 9 tests verts,
inchangés ».** Le fichier en compte **8**, et en comptait déjà 8 avant ce plan : `git diff --name-only
3559c85..HEAD -- tests/Chronos.Tests/CompositeUsageProviderTests.cs` est **vide**. Inexactitude du plan sur
le nombre, pas régression : l'exigence réelle (fichier intact, tous verts) est satisfaite.

---

**Total deviations:** 2 auto-corrigées (2 blocantes, Rule 3) + 2 écarts de critères documentés
**Impact on plan:** aucun scope creep. Les 6 fichiers touchés sont exactement ceux de `files_modified`.

## Issues Encountered

- **Flakiness observée (hors périmètre) :** lors d'une exécution de la suite complète,
  `CompositionRootTests.Host_resout_et_dispose_les_singletons` a échoué une fois, puis a été verte en
  isolation et sur les **quatre** exécutions complètes suivantes (406/406). Le test construit une fenêtre WPF
  réelle sur STA et enregistre `ChronosPaths.Default()` (chemins réels) ; l'échec est indépendant de ce plan,
  qui ne touche ni la DI, ni `App.xaml.cs`, ni `CompositionRootTests.cs` (`git status --porcelain` sur ces
  deux fichiers : **vide**). Consigné, non corrigé (SCOPE BOUNDARY).
- **2 avertissements xUnit2031 pré-existants** dans `DesktopUiaSessionSourceTests.cs` (lignes 345 et 364),
  déjà signalés au plan 16-01. Hors périmètre.
- **Heredoc bash et apostrophes françaises :** l'écriture des fichiers de test par heredoc a échoué sur les
  apostrophes ; les fichiers ont été créés par l'outil d'écriture direct, en UTF-8, sans altération des
  accents (vérifié).

## Known Stubs

Aucun. Les deux squelettes `NotImplementedException` des commits RED ont été remplacés par leur
implémentation dans les commits GREEN correspondants ; `grep "TODO\|FIXME\|placeholder"` sur les deux
fichiers de production → 0 résultat.

**À noter (différé par conception, pas un stub) :** aucun des deux types n'est enregistré en DI. Le câblage
de `LastExactStore` et l'insertion de `LastExactUsageProvider` en tête de la chaîne `IUsageProvider` dans
`App.ConfigureServices` sont **délibérément** reportés au plan **16-03**, pour rendre 16-01 et 16-02
réellement parallélisables en vague 1 (aucun fichier partagé). Tant que ce câblage n'est pas fait, EXA-01
existe en briques testées mais n'est pas encore actif de bout en bout.

## User Setup Required

Aucune. Le magasin se crée tout seul au premier relevé exact, sous `%APPDATA%\Chronos\`, sans droit admin.

## Verification

| # | Vérification | Résultat |
|---|---|---|
| 1 | `dotnet build Chronos.sln -v q --nologo` | 0 erreur |
| 2 | `dotnet test Chronos.sln -v q --nologo` | **406 réussis, 0 échec** (384 baseline + 22) |
| 3 | `--filter "FullyQualifiedName~LastExactStoreTests"` | 13/13 (≥ 10 exigés) |
| 4 | `--filter "FullyQualifiedName~LastExactUsageProviderTests"` | 9/9 (≥ 8 exigés) |
| 5 | `--filter "FullyQualifiedName~ServicesLayerPurityTests"` | 1/1 vert — aucun type WPF dans les deux nouveaux services |
| 6 | `--filter "FullyQualifiedName~CompositeUsageProviderTests"` | 8/8 verts, fichier intact |
| 7 | `git status --porcelain src/Chronos/App.xaml.cs tests/Chronos.Tests/CompositionRootTests.cs` | **vide** |
| 8 | `git diff --name-only 3559c85..HEAD` | exactement les 6 fichiers de `files_modified` |
| 9 | `grep -c "System.Windows"` sur les 3 fichiers de production | 0 partout |
| 10 | `grep -c "Assembly.Location\|GetExecutingAssembly"` sur `LastExactStore.cs` | 0 |
| 11 | `ls "$APPDATA/Chronos/last-exact.json"` | **absent** — les tests n'ont pas pollué le profil réel |
| 12 | Dossiers temp `ChronosLastExact*` résiduels | 0 |

## Next Phase Readiness

**Prêt pour le plan 16-03** (renommage `JsonlEstimationProvider` → `TranscriptActivityProvider` **et**
câblage DI). Ce qu'il doit brancher, dans `App.ConfigureServices` :

```
services.AddSingleton(sp => new LastExactStore(sp.GetRequiredService<ChronosPaths>().LastExactFile));
services.AddSingleton<IUsageProvider>(sp => new LastExactUsageProvider(
    inner: /* la chaîne composite existante */,
    store: sp.GetRequiredService<LastExactStore>(),
    clock: sp.GetRequiredService<IClock>()));
```

- Aucun risque de boucle de rafraîchissement : le `FileSystemWatcher` de `RefreshOrchestrator` filtre
  nominativement `usage.json` ; écrire `last-exact.json` dans le même dossier ne déclenche aucun événement.
- `RefreshOrchestrator` **et** `DiagnosticService` consomment tous deux `IUsageProvider` et bénéficieront du
  décorateur sans câblage supplémentaire.

**Prêt pour la phase 19** : la limite d'âge (EXA-02) et la correction par delta (DEL-03/04) se greffent dans
`LastExactUsageProvider.GetAsync`, sur un `CapturedAt` déjà disponible par fenêtre. La garde du magasin
(`resets_at <= now`) est une invalidation **logique** et reste indépendante de la future limite temporelle.

**Aucun blocage.**

---
*Phase: 16-fondations-du-delta-persistance-d-molition-des-plafonds*
*Completed: 2026-09-09*

## Self-Check: PASSED

- 6 fichiers de code verifies presents sur disque (4 crees, 2 modifies) + le SUMMARY.
- 4 commits verifies presents dans l historique : ea99b46, c1b817f, dcb2067, 329b695.
- `dotnet build Chronos.sln` : 0 erreur. `dotnet test Chronos.sln` : 406 reussis, 0 echec.
- `git status --porcelain src/Chronos/App.xaml.cs tests/Chronos.Tests/CompositionRootTests.cs` : vide.
