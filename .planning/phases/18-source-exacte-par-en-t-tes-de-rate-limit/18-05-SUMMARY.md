---
phase: 18-source-exacte-par-en-t-tes-de-rate-limit
plan: 05
subsystem: composition-root
tags: [di, composition-root, primary-vs-fallback, alias-instance, diagnostic, observabilite, mutation-testing, xunit]

# Dependency graph
requires:
  - phase: 16-fondations-du-delta
    provides: "LastExactUsageProvider + LastExactStore — le décorateur de persistance, que la sonde hérite gratuitement en passant DESSOUS"
  - phase: 17-jeton-toujours-vivant
    provides: "ChronosTokenAuthority (autorité UNIQUE de jeton) et le motif « alias d'instance » IAuthStatus, recopié à l'identique pour IEtatServeur"
  - phase: 18-source-exacte-par-en-t-tes-de-rate-limit
    provides: "18-03 — RateLimitHeaderUsageProvider (la sonde), CadenceNominale, DernierResultat, NomsEnTetesRecus, ChronosSettings.SondeEnTetesActivee"
  - phase: 18-source-exacte-par-en-t-tes-de-rate-limit
    provides: "18-04 — la sonde IMPLÉMENTE IEtatServeur : Depassement, DepassementChange ; WindowState.StatutServeur/Depassement"
provides:
  - "HDR-01/HDR-02 — la sonde est EN PRODUCTION et en PRIMAIRE : ses chiffres, son statut serveur et son dépassement atteignent réellement l'affichage, y compris sous 429"
  - "IEtatServeur résolu comme ALIAS de l'unique instance de sonde — prouvé par Assert.Same, donc jamais 576 requêtes/jour au lieu de 288"
  - "Garde de POSITION par le COMPORTEMENT : deux sources Exact aux chiffres différents, la sonde gagne ; l'inversion primary/fallback fait tomber le test"
  - "HDR-06 — l'interrupteur, la cadence (5 min) et le coût (≈ 288/jour) sont VISIBLES dans le rapport, dérivés de CadenceNominale et jamais recopiés"
  - "Section « sonde d'en-têtes de rate-limit » du diagnostic : issue en 11 libellés français, NOMS d'en-têtes reconnus, dépassement par le canal latéral"
  - "Inventaire des noms d'en-têtes de limite présents sur la réponse de /api/oauth/usage — la question ouverte « la sonde est-elle gratuite ? » sera tranchée au premier rafraîchissement réussi"
  - "Describe(WindowState) étendu : statut serveur et dépassement lus sur le snapshot DÉJÀ obtenu, donc UN SEUL GetAsync dans tout le rapport"
affects: [18-06-ui-et-verification-manuelle, 19-doctrine-du-composite, 20-rendu-visible]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Preuve de POSITION dans une chaîne de composites par le COMPORTEMENT et non par la réflexion : deux sources de MÊME fiabilité produisent des chiffres distincts, et c'est le chiffre sorti qui désigne le primaire"
    - "Composite INSTANCIÉ et non modifié : une chaîne se rallonge en composition racine, sans toucher au fichier dont l'édition est gelée pour une phase ultérieure"
    - "Paramètre optionnel en DERNIÈRE position comme protocole d'extension d'un service à N sites de construction (2e application, après la phase 17)"
    - "Cadence et coût annoncés à l'utilisateur DÉRIVÉS de la constante du provider : un chiffre recopié dans un rapport devient un mensonge poli le jour où la constante change"
    - "Une information coûteuse est lue UNE fois et décorée là où elle est déjà disponible, plutôt que redemandée là où elle serait lisible : ouvrir le diagnostic ne doit pas dépenser de quota"

key-files:
  created:
    - tests/Chronos.Tests/Fakes/FakeEtatServeur.cs
  modified:
    - src/Chronos/App.xaml.cs
    - src/Chronos/Services/DiagnosticService.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs

key-decisions:
  - "La sonde est en PRIMARY, et la raison est mécanique et non esthétique : Best() ne retient le fallback que s'il est STRICTEMENT plus fiable, et les deux sources produisent Exact. Placée en fallback, la sonde n'aurait JAMAIS gagné tant que /api/oauth/usage répond — or son snapshot est le seul porteur du statut serveur et du dépassement, donc HDR-03 et HDR-04 auraient été morts-nés à chaque tick nominal. Le test de position ne le prouve pas par la réflexion (les champs du composite sont privés) mais par le chiffre qui sort."
  - "Le statut serveur et le dépassement du SNAPSHOT sont affichés en étendant Describe(WindowState), dans la section « Ce qui est affiché maintenant », et non dans la section de la sonde. Raison de coût : la section de la sonde est écrite AVANT l'appel au composite, donc y lire le statut aurait exigé un SECOND GetAsync — c'est-à-dire une seconde sonde, donc une dépense de quota DOUBLÉE à chaque ouverture du rapport. Le critère d'acceptation grep GetAsync == 1 grave cette contrainte."
  - "Le dépassement apparaît DEUX fois dans le rapport, par deux chemins différents et c'est volontaire : dans la section de la sonde par le canal LATÉRAL (il survit à un Best() défavorable) et en suffixe de Describe par le champ de WindowState (il accompagne CETTE fenêtre). C'est la traduction fidèle de la doctrine des deux canaux posée au plan 18-04."
  - "La cadence (5 min) et le coût (≈ 288/jour) affichés sont CALCULÉS depuis RateLimitHeaderUsageProvider.CadenceNominale, au lieu d'être recopiés comme le proposait la lettre du plan. Un chiffre recopié survit au changement de la constante et devient un mensonge que rien ne signale — exactement la classe de défaut que v1.5 corrige."
  - "Le préfixe littéral « anthropic-ratelimit » n'est écrit QU'UNE fois dans DiagnosticService.cs (le filtre de l'inventaire). Le commentaire de la section de la sonde, qui le citait initialement, a été reformulé en « la famille d'en-têtes unified » : le critère d'acceptation grep == 1 est un garde-fou contre un préfixe dupliqué qui divergerait, et il méritait d'être respecté à la lettre."
  - "Un quatrième critère d'acceptation de la tâche 2 est littéralement insatisfaisable et a été remplacé par une vérification d'INTENTION (voir Déviations) : le plan exigeait grep -c 'sp.GetRequiredService<IEtatServeur>()' == 2 alors que le code qu'il mandate lui-même pour l'alias s'écrit AddSingleton<IEtatServeur>(sp => sp.GetRequiredService<RateLimitHeaderUsageProvider>()) — qui ne contient pas ce motif."

patterns-established:
  - "Le texte RÉEL du rapport est capturé par une écriture temporaire depuis un test, relu, puis révoquée avec vérification d'identité octet pour octet du fichier de test : la phase 19 part d'un extrait constaté, pas reconstitué de tête."

requirements-completed: [HDR-01, HDR-02, HDR-06]

# Metrics
duration: 24min
completed: 2026-09-12
---

# Phase 18 Plan 05: Câblage DI de la sonde et diagnostic parlant Summary

**La sonde d'en-têtes est branchée dans la chaîne réelle et elle y est le PRIMAIRE — une contrainte mécanique et non un goût, puisque `Best()` ne retient le fallback que s'il est STRICTEMENT plus fiable et que les deux sources produisent `Exact` : en fallback, le seul snapshot porteur du statut serveur et du dépassement aurait été écarté à chaque tick où `/api/oauth/usage` répond. Sa position est prouvée par un test de COMPORTEMENT — deux sources aux chiffres différents, et c'est 0,01 qui sort — dont l'inversion `primary`/`fallback` a été mesurée (0,42 sortait) puis révoquée. `IEtatServeur` est un alias de la MÊME instance, et l'utilisateur peut désormais lire dans son diagnostic l'issue de la sonde, les NOMS d'en-têtes reconnus, le statut serveur et le dépassement — sans qu'aucune valeur d'en-tête brute n'y figure, et sans qu'ouvrir le rapport coûte une sonde de plus.**

## Performance

- **Duration:** 24 min
- **Started:** 2026-09-12T01:43:00Z
- **Completed:** 2026-09-12T02:07:00Z
- **Tasks:** 2
- **Files modified:** 5 (1 créé, 4 modifiés)

## Ce qui a été construit

### Tâche 1 — la sonde en production, à la bonne place (commit `79604d0`)

**La forme EXACTE de la chaîne câblée** (`App.xaml.cs`), que la phase 19 prendra comme point de départ :

```csharp
services.AddSingleton<IUsageProvider>(sp => new LastExactUsageProvider(
    inner: new CompositeUsageProvider(
        primary:  sp.GetRequiredService<RateLimitHeaderUsageProvider>(),
        fallback: new CompositeUsageProvider(
            primary:  sp.GetRequiredService<ChronosOAuthUsageProvider>(),
            fallback: new CompositeUsageProvider(
                primary:  sp.GetRequiredService<GatedOAuthUsageProvider>(),
                fallback: sp.GetRequiredService<ClaudeUsageObjectProvider>()))),
    store: sp.GetRequiredService<LastExactStore>(),
    clock: sp.GetRequiredService<IClock>()));
```

Trois composites imbriqués, le décorateur de persistance EXA-01 toujours en TÊTE, et
`CompositeUsageProvider.cs` **intact** (`git diff` vide) : il est INSTANCIÉ, jamais modifié — la refonte
de sa doctrine reste la phase 19.

Deux enregistrements ajoutés : la sonde (consommateur de `ChronosTokenAuthority`, jamais un troisième
rafraîchisseur) et `IEtatServeur` en **alias de la même instance**, copie exacte du motif
`ChronosTokenAuthority`/`IAuthStatus` de la phase 17.

**La garde de position, et ce qu'elle attrape.** `La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte`
monte la chaîne réelle avec deux sources de **fiabilité identique** (`Exact`) mais aux **chiffres
différents** : la sonde sur le jeu d'en-têtes nominal (5 h = **0,01**, statut `allowed`) et
`/api/oauth/usage` sur un corps JSON (5 h = **0,42**). Seule la POSITION peut décider lequel sort. Le test
asserte `0.01`, et aussi `StatutServeur.Autorise` — preuve que le statut traverse réellement les deux
composites imbriqués **et** le décorateur, ce qui est la condition de vie de HDR-03/HDR-04 en production.

La garde `Assert.Same(sonde, provider.GetRequiredService<IEtatServeur>())` a été ajoutée à la garde DI
préexistante de la phase 17. Ce qu'elle attrape et que `dotnet build` ne voit pas : un
`AddSingleton<IEtatServeur>(_ => new RateLimitHeaderUsageProvider(...))` compilerait, résoudrait, et
dépenserait **576 requêtes par jour au lieu de 288** tout en publiant le dépassement sur une instance que
plus personne n'écoute.

### Tâche 2 — le diagnostic parle (commit `9276416`)

`IEtatServeur?` est devenu le **7e paramètre**, optionnel et en dernière position : les **10 sites de
construction préexistants** (1 en production, 9 en tests) compilent sans une retouche — vérifié par
`git diff --stat` VIDE sur `CadranBindingTests`, `MainViewModelTests`, `OverlayWindowConfigTests` et
`ThemingTests`.

**Extrait RÉEL du rapport** (capturé depuis un test, anonymisé ; point de départ de la phase 19) :

```
[Source exacte — sonde d'en-têtes de rate-limit]
  Interrupteur : ACTIVÉE — une micro-requête sur ton compte toutes les 5 min (≈ 288/jour)
  Dernière sonde : 429 — en-têtes lus quand même, les chiffres restent exacts
  En-têtes « unified » reconnus : anthropic-ratelimit-unified-5h-utilization,
      anthropic-ratelimit-unified-5h-reset, anthropic-ratelimit-unified-7d-utilization,
      anthropic-ratelimit-unified-7d-reset (4)
  Dépassement : aucun dépassement rapporté
```

(La ligne « En-têtes reconnus » est rendue sur une seule ligne dans le fichier ; repliée ici pour la
lisibilité.) La section est insérée **juste après** `[Source exacte — login OAuth Chronos]`, parce que la
sonde est désormais la **première** source de la chaîne.

Autres formes livrées :

- interrupteur à faux → `désactivée (menu clic droit → Réglages) — aucune requête, aucun coût`, et
  **aucune** ligne suivante : couper la sonde doit la faire taire, pas la faire ressasser ;
- liste vide → `AUCUN — la famille « unified » n'est documentée nulle part chez Anthropic et peut avoir
  changé de nom` — le seul signal qui permette de constater un renommage côté serveur ;
- dépassement rapporté → `34 % (reset le 2026-09-14 11:00) · serveur : REJETÉ` ;
- 11 libellés d'issue, **un par membre** de `ResultatSonde`, `JamaisSondee` et « canal non injecté »
  partageant à dessein `pas encore sondé (la première sonde arrive au prochain tick)` — un état initial,
  pas une panne.

**Inventaire des en-têtes de `/api/oauth/usage`** (question ouverte de la recherche) : la section de
l'endpoint OAuth liste désormais les **NOMS** d'en-têtes commençant par `anthropic-ratelimit` présents sur
sa réponse — jamais leurs valeurs, jamais le corps. S'il portait la famille `unified`, HDR-01 à HDR-04
seraient satisfaits **sans dépenser un jeton de quota** et le coût de la sonde disparaîtrait. La question
se tranchera au **premier rafraîchissement réussi de l'utilisateur** ; impossible à trancher en
développement, le 401 de cet endpoint étant rendu en bordure sans aucun en-tête de limite.

## Task Commits

| Tâche | Nom | Commit | Type |
|---|---|---|---|
| 1 | Câblage DI — sonde en primaire, `IEtatServeur` aliasé, garde de position | `79604d0` | feat |
| 2 | Le diagnostic parle — issue, noms d'en-têtes, statut, dépassement | `9276416` | feat |

## Décompte de tests — avant / après

| Moment | Total | Échecs | Filtre concerné |
|---|---|---|---|
| Baseline à l'entrée du plan (rejouée) | **628** | 0 | — |
| Après tâche 1 | **629** | 0 | `CompositionRootTests` : 4 → **5** |
| Après tâche 2 | **633** | 0 | `DiagnosticServiceTests` : 3 → **7** |

**+5 tests, 0 test supprimé, 0 test modifié.** Addition pure mesurée : `git diff | grep -c '^-[^-]'` = **0**
sur `CompositionRootTests.cs` comme sur `DiagnosticServiceTests.cs`.

## Résultat de la mutation d'inversion `primary`/`fallback`

Mutation appliquée **dans le test de position** : le composite externe inversé (`primary` = le composite
`/api/oauth/usage`, `fallback` = la sonde).

```
Assert.Equal() Failure: Values are not within 9 decimal places
Expected: 0,01
Actual:   0,41999999999999998
```

**Exactement la défaillance prédite par le plan** : à fiabilité égale, `Best()` garde le primaire, donc les
chiffres de `/api/oauth/usage` sortent et la sonde est muette — avec elle, le statut serveur et le
dépassement. **Mutation révoquée**, `git diff` conforme à l'intention, 5 tests de
`CompositionRootTests` de nouveau verts.

## Vérifications du plan

| # | Vérification | Résultat |
|---|---|---|
| 1 | `dotnet test Chronos.sln -v q --nologo` | **633 réussis, 0 échec**, 0 test supprimé |
| 2 | `git diff --name-only` sur `CompositeUsageProvider.cs` et `UsageSnapshot.cs` | **VIDE** |
| 3 | Filtre `CompositionRootTests` | **5/5 vert** (position + identité d'instance) |
| 4 | Filtre `NormalisationUniqueTests` | vert — aucune conversion locale réintroduite |
| 5 | Filtre `ServicesLayerPurityTests` | vert — aucun type WPF dans `Services/` |
| 6 | Fichiers SOURCE contenant `RefreshAsync` | **exactement 2** (`ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs`) |
| 7 | `dotnet build Chronos.sln` | **0 erreur, 0 avertissement** — les 10 sites de construction compilent |
| 8 | `%APPDATA%\Chronos\oauth.dat` | **518 o, mtime 1783863147** avant ET après — intact |

Critères `grep` de la tâche 1 : `new RateLimitHeaderUsageProvider(` = 1 · `AddSingleton<IEtatServeur>` = 1 ·
`primary:  sp.GetRequiredService<RateLimitHeaderUsageProvider>()` = 1 · `new CompositeUsageProvider(` = **3** ·
`new LastExactUsageProvider(` = 1 · `Assert.IsType<LastExactUsageProvider>` = 2 · `Path.GetTempPath()` = 15.

Critères `grep` de la tâche 2 : `IEtatServeur? etatServeur = null` = 1 (dernier paramètre) ·
`IAuthStatus? authStatus = null` = 1 · `public DiagnosticService(` = 1 · `LibelleSonde` = 2 ·
`LibelleStatutServeur` = 3 · `anthropic-ratelimit` = **1** · `UsageNormalization.PourcentagePourAffichage` = 6 ·
`* 100`/`FromUnixTime` = **0** · `GetAsync` = **1**.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocage] Critère d'acceptation littéralement insatisfaisable, vérifié par l'intention**

- **Found during:** tâche 2, vérification des critères `grep`.
- **Issue:** le plan exige `grep -c "sp.GetRequiredService<IEtatServeur>()" src/Chronos/App.xaml.cs` **== 2**
  (« l'alias + l'argument de `DiagnosticService` »). Mais le code que le plan mandate lui-même pour l'alias
  s'écrit `AddSingleton<IEtatServeur>(sp => sp.GetRequiredService<RateLimitHeaderUsageProvider>())` — il ne
  contient pas le motif cherché. Le critère ne peut donc jamais valoir 2 sans dupliquer une résolution.
- **Fix:** les deux faits visés sont vérifiés séparément et valent tous deux 1 :
  `grep -c "AddSingleton<IEtatServeur>"` = 1 (l'alias) et `grep -c "sp.GetRequiredService<IEtatServeur>()"` = 1
  (l'argument de `DiagnosticService`). Aucun code de production modifié pour satisfaire une coquille de critère.
- **Files modified:** aucun.

**2. [Rule 2 - Correction] Cadence et coût DÉRIVÉS au lieu d'être recopiés**

- **Found during:** tâche 2, rédaction de la ligne `Interrupteur`.
- **Issue:** le plan dicte le texte littéral « toutes les 5 min (≈ 288/jour) ». Un chiffre recopié survit au
  changement de `CadenceNominale` et devient un mensonge que rien ne signale — la classe exacte de défaut
  que v1.5 corrige.
- **Fix:** les deux nombres sont calculés depuis `RateLimitHeaderUsageProvider.CadenceNominale`. Texte rendu
  identique au plan (vérifié sur le rapport réel capturé), mais il suit désormais la constante.
- **Files modified:** `src/Chronos/Services/DiagnosticService.cs` — commit `9276416`.

**3. [Rule 2 - Correction] Commentaire reformulé pour honorer `anthropic-ratelimit` == 1**

- **Found during:** tâche 2, vérification des critères `grep`.
- **Issue:** le commentaire de tête de la section de la sonde citait « `anthropic-ratelimit-unified-*` »,
  portant le compte à 2 et violant le critère `== 1`.
- **Fix:** reformulé en « la famille d'en-têtes « unified » », avec mention explicite que le préfixe littéral
  n'est écrit qu'une fois dans le fichier. Précédent : plans 18-03 (déviation 3) et 18-04 (déviation 4).
- **Files modified:** `src/Chronos/Services/DiagnosticService.cs` — commit `9276416`.

**4. [Rule 1 - Bug] BOM UTF-8 et fins de ligne LF introduits par accident, puis retirés**

- **Found during:** tâche 1, édition scriptée de `CompositionRootTests.cs`.
- **Issue:** une réécriture en `utf-8-sig` a ajouté un BOM absent du fichier d'origine et converti les fins
  de ligne en LF, transformant une addition pure en `-1/+2` dans `git diff`.
- **Fix:** fins de ligne CRLF restaurées, BOM retiré ; `git diff` revenu à **117 insertions, 0 suppression**
  avant le commit. Aucune autre édition n'a utilisé cette voie.
- **Files modified:** `tests/Chronos.Tests/CompositionRootTests.cs` — commit `79604d0`.

### Écart assumé, non corrigé

**Coût en temps de la suite de tests : 56 s → ~2 min 35.** Cause mesurée : chaque test de
`DiagnosticServiceTests` exécute `BuildReportAsync`, qui balaie `%APPDATA%`/`%LOCALAPPDATA%` à la recherche
des coffres et fait un poll **réel** d'UI Automation — deux découvertes **non injectables**. Les 3 tests
préexistants payaient déjà ~20 s chacun ; les 4 tests **mandatés par ce plan** le paient à leur tour. Rendre
ces découvertes injectables changerait la signature de `DiagnosticService`, ce que ce plan **interdit
explicitement**. Consigné dans
`.planning/phases/18-source-exacte-par-en-t-tes-de-rate-limit/deferred-items.md`, candidat phase 20 (qui
rouvre légitimement `DiagnosticService` pour EXA-03/EXA-06).

## Authentication Gates

Aucun. Aucun test n'émet de requête réseau réelle sur les chemins ajoutés : le transport de la sonde et
celui du point de terminaison de jeton sont des `FakeHttpMessageHandler`, le coffre et le magasin vivent
sous `Path.GetTempPath()` (gardes `Assert.StartsWith`), et les quatre nouveaux tests de diagnostic montent
un `FakeClaudeTokenReader` à `Token = null` — donc la sonde réseau de la section « endpoint OAuth », gardée
par `if (token is not null)`, n'est jamais atteinte.

**Le coffre réel n'a pas été touché** : `%APPDATA%\Chronos\oauth.dat` = 518 octets, mtime 1783863147, avant
comme après.

## Ce qui reste à la phase 18

Le plan **18-06** (remontée au cadran et aux réglages, exécutable en parallèle : aucun fichier partagé)
reste à exécuter. HDR-03 et HDR-04 sont déjà cochés depuis 18-04 ; ce plan vient de les rendre **vivants en
production** en plaçant la sonde là où son snapshot gagne.

## À VÉRIFIER PAR L'UTILISATEUR (hors GSD)

**La ligne la plus informative de tout ce plan n'est observable que sur un compte reconnecté.** Au premier
rafraîchissement réussi, ouvrir le diagnostic (clic droit → « Diagnostic… ») et lire **deux** lignes :

1. `[Source exacte — sonde d'en-têtes de rate-limit]` → `En-têtes « unified » reconnus : …` — si elle dit
   **AUCUN**, la famille `anthropic-ratelimit-unified-*` a changé de nom côté serveur et toute la phase 18
   doit être recalibrée sur les nouveaux noms.
2. `[Source exacte — endpoint OAuth (repli)]` → `→ en-têtes de limite présents : …` — si elle liste la
   famille `unified`, alors `/api/oauth/usage` suffit et **le coût de la sonde peut être supprimé** en
   phase 19+ (≈ 288 micro-requêtes par jour économisées).

Rappel de la phase 17 : sans reconnexion réelle, ni `/api/oauth/usage` ni la sonde ne répondent sur cette
machine (jeton expiré le 2026-07-12).

## Self-Check: PASSED

Les 5 fichiers de code annoncés existent sur disque, les 2 fichiers de planification aussi, et les 2
commits de tâche (`79604d0`, `9276416`) sont présents dans l'historique.
