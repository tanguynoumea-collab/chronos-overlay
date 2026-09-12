---
phase: 18-source-exacte-par-en-t-tes-de-rate-limit
plan: 03
subsystem: usage-providers
tags: [http-headers, rate-limit, 429, oauth-bearer, auto-throttle, backoff, invariantculture, xunit, mutation-testing]

# Dependency graph
requires:
  - phase: 17-jeton-toujours-vivant
    provides: "ChronosTokenAuthority — AUTORITÉ UNIQUE du jeton. La sonde en est un simple CLIENT ; son commentaire de tête nommait déjà cette phase"
  - phase: 18-source-exacte-par-en-t-tes-de-rate-limit
    provides: "18-01 — UsageNormalization (FractionDepuisTexteFraction / InstantDepuisTexteEpoch) : toute conversion texte->nombre y est déléguée ; 18-02 — ResultatSonde, WindowState étendu, FakeHttpMessageHandler.AvecEnTetes/SequenceAvecEnTetes, EnTetesDeReference (8 jeux)"
provides:
  - "Services/RateLimitHeaderUsageProvider.cs : LA SONDE — deux fenêtres EXACTES lues dans les en-têtes d'un POST /v1/messages jetable, qui répond MÊME en 429"
  - "Tableau d'aiguillage par code de statut, en-têtes lus AVANT toute décision (HDR-02) — contrat dont les plans 18-04 et 18-05 dépendent"
  - "Observabilité publique : DernierResultat (ResultatSonde), NomsEnTetesRecus (constantes locales, jamais l'orthographe du serveur), CadenceNominale"
  - "ChronosSettings.SondeEnTetesActivee : interrupteur DISTINCT d'OAuthUsageEnabled, défaut true"
  - "CapturedAt renseigné PAR FENÊTRE — prérequis de la doctrine de la phase 19 (limite d'âge, correction par delta)"
  - "Garde structurelle permanente sur la sonde : aucun type WPF, aucune horloge système, aucun contrôle de succès par exception, aucune lecture du corps, aucun rafraîchisseur"
affects: [18-04-statut-et-depassement, 18-05-cablage-di, 18-06-ui-et-verification-manuelle, 19-doctrine-du-composite, 20-rendu-visible]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Lecture d'en-têtes AVANT l'aiguillage par code de statut : la source reste informative précisément quand le serveur refuse"
    - "Provider qui est un simple CLIENT d'une autorité de jeton, prouvé par une garde textuelle (aucun appel de renouvellement dans son texte source)"
    - "Frein de cadence inconditionnel DÉCOUPLÉ de l'existence d'un cache, et cache volontairement plus court que celui de la source concurrente"
    - "Falsifiabilité d'un commit source+tests unique obtenue par 3 mutations ciblées, mesurées puis révoquées (git diff vide)"
    - "Interpolation CONSTANTE du modèle dans le corps JSON : le corps ne peut pas diverger de l'identifiant annoncé"

key-files:
  created:
    - src/Chronos/Services/RateLimitHeaderUsageProvider.cs
    - tests/Chronos.Tests/RateLimitHeaderUsageProviderTests.cs
  modified:
    - src/Chronos/Services/ChronosSettings.cs

key-decisions:
  - "Une fenêtre dont SEULE l'utilization est illisible reste Exact avec Utilization null, au lieu de devenir Unavailable : son reset a réellement été obtenu, et le jeter serait une perte d'information — exactement le défaut que la phase corrige. Écart assumé avec la lettre du test n° 11 de la tâche 2, conforme à la règle de construction énoncée par la tâche 1 du même plan."
  - "SignalerSucces() sur un 429 PORTEUR d'en-têtes : un 429 prouve que le jeton est valide. Différence ASSUMÉE avec ChronosOAuthUsageProvider, qui sur 429 se contente de reculer parce qu'il n'apprend rien. Sans cela la pastille de déconnexion mentirait pendant toute la saturation."
  - "L'identifiant de modèle est INTERPOLÉ dans le corps (interpolation constante, résolue à la compilation) plutôt que recopié : la constante Modele n'est pas du code mort et le corps ne peut pas diverger de l'identifiant documenté."
  - "Trois noms d'API interdits par les critères d'acceptation (contrôle de succès par exception, désérialisation, appel de renouvellement) ne sont cités NULLE PART dans le fichier source, commentaires compris — les raisons sont énoncées sans les nommer, précédent des plans 18-01 (déviation 3) et 18-02 (déviation 2). Les noms survivent dans le FICHIER DE TEST, où une garde textuelle permanente les interdit désormais dans la source."
  - "La garde structurelle de la tâche 3 a été élargie au-delà de la lettre du plan (qui ne demandait que DateTimeOffset.UtcNow) : elle interdit aussi, de façon permanente, le contrôle de succès par exception, la lecture du corps et l'appel de renouvellement. Sans cela les sept critères grep du plan n'auraient vécu que le temps d'une exécution d'agent."

patterns-established:
  - "Commit source+tests UNIQUE rendu falsifiable par mutation mesurée : 3 mutations ciblées, décompte d'échecs relevé, révocation vérifiée par git diff vide, puis re-vérification verte."
  - "Critères grep du plan convertis en test permanent : ce qu'un plan mesure une fois, un test le garde toujours."

requirements-completed: [HDR-01, HDR-06]

# Metrics
duration: 18min
completed: 2026-09-12
---

# Phase 18 Plan 03: La sonde d'en-têtes de rate-limit Summary

**La sonde lit les deux fenêtres exactes dans les en-têtes d'un `POST /v1/messages` à `max_tokens:1` — et elle les lit AVANT tout aiguillage sur le code de statut, ce qui lui permet de rendre un chiffre exact et frais au moment même où l'API refuse, là où `/api/oauth/usage` ne rend rien ; un 429 muet, lui, ne produit ni 0 % ni 100 % mais rien du tout, et la cadence est bornée par la sonde elle-même, sans aucun couplage au tick de l'orchestrateur.**

## Performance

- **Duration:** 18 min
- **Started:** 2026-09-12T01:05:50Z
- **Completed:** 2026-09-12T01:23:35Z
- **Tasks:** 3
- **Files modified:** 3 (2 créés, 1 modifié)

## Accomplishments

- **HDR-02 est gravé dans la forme du code, pas dans un commentaire.** Les en-têtes sont lus en première
  instruction du bloc `using (resp)`, et l'aiguillage ne se fait ensuite que sur `(int)resp.StatusCode`.
  Le test `Un_429_livre_quand_meme_les_chiffres` constate un snapshot `Exact`, `Exhausted`, à
  `CapturedAt` = l'instant courant, sur une réponse `429` — l'avantage structurel sur `/api/oauth/usage`,
  dont le 401 est rendu en bordure sans aucun en-tête de limite.
- **Le cas « 429 muet » est traité en première classe, pas supposé impossible.** Aucun `0 %`, aucun
  `100 %`, aucune déconnexion déclarée, et un recul qui honore la valeur du serveur (300 s mesuré) ou son
  plancher (60 s mesuré).
- **La sonde ne peut pas devenir un troisième rafraîchisseur de jeton.** Son texte source ne contient ni
  l'appel de renouvellement, ni le type du coffre, ni celui du client de protocole — et un test permanent
  l'interdit désormais, en plus de la garde de comptage à 2 fichiers source.
- **Le frein est prouvé indépendant de tout cache, deux fois.** Après une panne réseau (tâche 1) et après
  un 401 (tâche 3) : deux situations où *aucun* cache n'existe, et où le second appel n'envoie rien. C'est
  la leçon du défaut n° 3 du plan 17-01, où le frein disparaissait à chaque démarrage de l'exe.
- **Le chiffre annoncé à l'utilisateur est verrouillé par un test** (`288` sondes/jour), avant même que
  l'UI du plan 18-06 ne l'écrive.
- **Les sept critères `grep` du plan sont devenus un test.** Ce qu'un plan mesure une fois, un test le
  garde toujours : `Aucun_type_WPF_ni_aucune_horloge_systeme_dans_la_sonde` balaie le texte source et
  interdit en permanence l'horloge système, le contrôle de succès par exception, la lecture du corps et
  l'appel de renouvellement.

## Task Commits

1. **Task 1 : la sonde, son interrupteur, et les tests d'inertie et de sécurité** — `170cfe4` (feat)
2. **Task 2 : HDR-01/HDR-02 — le 429 livre les chiffres, le muet n'invente rien** — `ce26319` (test)
3. **Task 3 : HDR-06 — cadence bornée, coût chiffré, gardes structurelles** — `fa93863` (test)

**Plan metadata:** commit `docs(18-03)` final.

## Nombre de tests — avant / après

| Moment | Total | Échecs | Dans le filtre `RateLimitHeaderUsageProviderTests` |
|---|---|---|---|
| Baseline à l'entrée du plan (rejouée) | **582** | 0 | — |
| Après tâche 1 | **591** | 0 | 9 |
| Après tâche 2 | **605** | 0 | 23 |
| Après tâche 3 | **611** | 0 | **29** |

**+29 tests, 0 test supprimé, 0 test modifié.** Les deux fichiers de test étendus par **addition pure**,
mesurée par `git diff --numstat` : `432 0` (tâche 2) et `170 0` (tâche 3) ; lignes réellement supprimées
(`grep -c '^-[^-]'`) = **0** dans les deux cas.

Le filtre compte 29 tests pour 28 méthodes : `Un_modele_refuse_est_une_panne_de_configuration` est une
`[Theory]` à deux cas (`404` et `400`).

## Résultat des 3 mutations de falsifiabilité

**TDD strict impossible, et c'est documenté :** `tests/Chronos.Tests` porte un `ProjectReference` vers
`Chronos`, donc un test référençant un type inexistant empêche **toute** la solution de compiler et aucun
test ne s'exécute — l'étape RED serait un commit où `dotnet test` n'est même pas invocable. Source et tests
sont donc livrés dans un commit unique et compilable (précédent des plans 17-04, 17-05, 18-01 et 18-02), et
la falsifiabilité est **mesurée** par mutation. Les trois mutations ont été appliquées **après** le commit
de la tâche 1, ce qui rend la révocation vérifiable par `git diff` et non par une copie de sauvegarde.

| # | Mutation appliquée | Résultat mesuré | Test(s) tombé(s) |
|---|---|---|---|
| 1 | Court-circuit de l'interrupteur **retiré** de `GetAsync` | **1 échec / 8 succès** sur 9 | `L_interrupteur_a_false_rend_la_sonde_totalement_inerte` |
| 2 | Frein de cadence (`if (now < _prochainAppelAutorise)`) **retiré** | **2 échecs / 7 succès** sur 9 | `Le_frein_borne_la_cadence_et_s_ouvre_apres_la_cadence_nominale`, `Le_frein_tient_meme_quand_aucun_cache_n_existe` |
| 3 | En-tête `anthropic-beta` **retiré** d'`EnvoyerAsync` | **1 échec / 8 succès** sur 9 | `La_requete_est_un_POST_sur_l_endpoint_de_messages_avec_les_quatre_en_tetes` |

Chaque échec est **ciblé**, pas un effondrement global — la preuve que les tests mesurent la bonne chose.

**Révocation vérifiée après chacune :** `git checkout --` puis
`git diff -- src/Chronos/Services/RateLimitHeaderUsageProvider.cs` → **vide** les trois fois,
`git status --short` → **vide** à la fin, et 9/9 verts après la dernière révocation.

## Tableau d'aiguillage TEL QU'IMPLÉMENTÉ

**Les plans 18-04 et 18-05 dépendent de ce tableau.** Les en-têtes sont lus, et `NomsEnTetesRecus` est
publié, **avant** toute ligne de ce tableau — y compris sur 401, où la liste se trouve simplement vide.

| Code | Appel à l'autorité | Recul posé | `DernierResultat` | Retour | Cache écrit |
|---|---|---|---|---|---|
| 401 (après rejeu unique) ou 403 | `SignalerRefusServeur()` | `CadenceNominale` (300 s) | `RefusServeur` | `ServirCacheOu(Empty)` | non |
| 429 **avec** en-têtes | `SignalerSucces()` | `max(retry-after, 60 s)` | `SaturationEnTetesLus` | **le snapshot** | **oui** |
| 429 **sans** en-têtes | *rien* | `max(retry-after, 60 s)` | `SaturationSansEnTetes` | `ServirCacheOu(Empty)` | non |
| 400 ou 404 | *rien* | `ReculModeleRefuse` (60 min) | `ModeleRefuse` | `ServirCacheOu(Empty)` | non |
| 2xx **avec** en-têtes | `SignalerSucces()` | `CadenceNominale` | `SuccesEnTetesLus` | **le snapshot** | **oui** |
| 2xx **sans** en-têtes | `SignalerSucces()` | `CadenceNominale` | `SuccesSansEnTetes` | `ServirCacheOu(Empty)` | non |
| tout autre (5xx…) | *rien* | exponentiel 60 s → ×2 → 15 min | `PanneReseau` | `ServirCacheOu(Empty)` | non |
| exception de transport / délai | *rien* | exponentiel 60 s → ×2 → 15 min | `PanneReseau` | `ServirCacheOu(Empty)` | non |

**Trois états atteints AVANT le réseau**, dans cet ordre exact :

| Condition | `DernierResultat` | Envoi HTTP | Jeton demandé | Cache servi |
|---|---|---|---|---|
| `SondeEnTetesActivee == false` (relu frais) | `Desactivee` | **0** | **non** | **non** — couper la sonde doit la faire taire, pas la faire ressasser |
| `now < _prochainAppelAutorise` | `FreinActif` | **0** | non | oui si < 300 s |
| jeton indisponible (coffre vide) | `PasDeJeton` | **0** | oui | oui si < 300 s |
| avant tout appel | `JamaisSondee` | 0 | non | — |

**Le recul réseau est remis à `TimeSpan.Zero`** sur tout succès — 2xx comme 429 authentifié. Mesuré par la
dernière section de `Une_panne_reseau_recule_exponentiellement_et_ne_declare_pas_de_deconnexion` : après un
succès, l'échec suivant recule de 60 s et non de 480 s.

**Exploitable** = au moins une des deux fenêtres porte une `utilization` **ou** un `reset`. La construction
d'une fenêtre rend `Unavailable` **uniquement** si les deux valeurs sont inconnues.

## HDR-02 n'est PAS prouvé en production

**À lire avant de cocher quoi que ce soit.** Ce plan prouve que **le code** exploite un 429 porteur
d'en-têtes. Il ne prouve **pas** qu'un 429 **réel** d'Anthropic porte la famille `anthropic-ratelimit-unified-*` :

- la famille est **absente de la documentation publique** Anthropic (qui documente `-requests-*`,
  `-tokens-*` et `retry-after`, avec `reset` en RFC 3339 et non en epoch) ;
- les trois sources qui l'affirment sont **concordantes mais aucune n'est officielle** ;
- la vérification exige **à la fois** un jeton valide — celui de cette machine est expiré depuis le
  2026-07-12 — **et** un compte réellement saturé.

La vérification manuelle est la **tâche 3 du plan 18-06**. `requirements-completed` de ce plan porte donc
`[HDR-01, HDR-06]` et **pas** HDR-02. La contrainte est inscrite en XML-doc du test central, à l'endroit
exact où une relecture future pourrait se croire rassurée.

## Files Created/Modified

- `src/Chronos/Services/RateLimitHeaderUsageProvider.cs` *(créé, 400 l.)* — la sonde. Constantes de requête
  (URL, `anthropic-beta`, version d'API, agent, modèle, corps), 5 constantes d'en-tête de réponse,
  6 constantes de cadence/recul, 2 propriétés publiques d'observabilité + `CadenceNominale`, `GetAsync` en
  7 étapes ordonnées, et 7 méthodes privées (`Lire`, `LireEnTetes`, `LireFenetre`, `EnTetesExploitables`,
  `Recul429`, `ReculerReseau`, `ServirCacheOu`, `EnvoyerAsync`). Aucune dépendance nouvelle, aucun type WPF.
- `tests/Chronos.Tests/RateLimitHeaderUsageProviderTests.cs` *(créé, 859 l., 29 tests)* — inertie et
  sécurité (9), les treize formes de réponse réelles (14 cas), cadence et gardes structurelles (6).
- `src/Chronos/Services/ChronosSettings.cs` *(+9 l.)* — `SondeEnTetesActivee`, `init`, défaut `true`.

**Mesures d'acceptation de la tâche 1, toutes satisfaites :**

| Mesure | Exigé | Obtenu |
|---|---|---|
| lignes de la sonde | ≥ 200 | **400** |
| contrôle de succès par exception | 0 | **0** |
| lecture du corps (4 motifs) | 0 | **0** |
| rafraîchisseur / coffre / client (4 motifs) | 0 | **0** |
| fichiers source portant l'appel de renouvellement | **2** | `ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs` |
| demandes de jeton | 2 | **2** (appel initial + rejeu) |
| relecture du réglage | 1 | **1** |
| lecture d'en-tête (réponse + repli contenu) | 2 | **2** |
| délégations à `UsageNormalization` | ≥ 2 | **2** |
| en-têtes de requête | 4 | **4** |
| `CapturedAt = now` | ≥ 1 | **2** (par fenêtre + `SourceCapturedAt`) |
| champs superflus du corps | 0 | **0** |
| horloge système | 0 | **0** |

## Decisions Made

1. **Une fenêtre à moitié lisible n'est pas une fenêtre inconnue.** Le test n° 11 de la tâche 2 attendait
   une 5 h `Unavailable` quand seule son `utilization` est illisible. Or son `reset` reste parfaitement
   lisible, et la règle de construction énoncée par la **tâche 1 du même plan** ne rend `Unavailable` que
   si les **deux** valeurs sont inconnues. Jeter un instant de reset réellement obtenu serait une perte
   d'information — le défaut même que la phase corrige. Le test couvre donc **deux volets** : utilization
   seule illisible (fenêtre `Exact`, `Utilization` à `null`, `ResetsAt` conservé) **et** les deux en-têtes
   illisibles (fenêtre réellement `Unavailable`, l'autre fenêtre intacte). Le nom du test et son intention
   — « lecture tolérante, en-tête par en-tête » — sont mieux servis qu'avec un seul volet.
2. **`SignalerSucces()` sur un 429 porteur.** Un 429 **prouve** que le jeton est valide. Ne pas le signaler
   laisserait la pastille de déconnexion mentir pendant toute la saturation, soit exactement quand
   l'utilisateur a le plus besoin de croire son overlay. Différence assumée avec
   `ChronosOAuthUsageProvider`, qui sur 429 se contente de reculer parce qu'il n'apprend rien.
3. **Le modèle est interpolé dans le corps, pas recopié.** Une constante `Modele` recopiée à l'identique
   dans le littéral du corps serait du code mort pouvant diverger du corps réellement émis.
   L'interpolation constante (résolue à la compilation) rend la divergence impossible.
4. **Les critères `grep` du plan sont devenus un test permanent.** Sept des quinze critères d'acceptation de
   la tâche 1 portent sur l'**absence** de motifs dans le texte source. Mesurés une seule fois, ils
   n'auraient rien gardé. `Aucun_type_WPF_ni_aucune_horloge_systeme_dans_la_sonde` les rejoue à chaque
   exécution de la suite, en s'appuyant sur le chemin de sources injecté par MSBuild (motif
   `NormalisationUniqueTests`, plan 18-01).
5. **Le cache est plus court que celui de la source concurrente, et un test le dit.** 300 s contre 15 min,
   parce que `Best()` privilégie le `primary` à fiabilité égale : un cache long de la sonde battrait une
   lecture **fraîche** de `/api/oauth/usage`. Le classement par fraîcheur est la phase 19 ; en attendant on
   ne sert pas de cache vieux.

## Deviations from Plan

Aucune déviation de comportement au regard des `must_haves`. Aucun fichier interdit touché, aucune
dépendance ajoutée, aucune signature existante modifiée, aucun bump de `SchemaVersion`. Trois ajustements,
dont un de fond (le n° 1).

**1. [Fond — honnêteté de la donnée] Le test n° 11 couvre deux volets au lieu d'affirmer un `Unavailable` faux**
- **Found during:** Task 2
- **Issue:** la lettre du test n° 11 (« la 5 h est `Unavailable` » quand seule son `utilization` vaut
  `"xxx"`) contredit la règle de construction explicitement donnée en code par la tâche 1 du même plan
  (`if (util is null && reset is null) return WindowState.Unavailable(kind)`). Avec un `reset` valide, la
  fenêtre est `Exact` avec `Utilization` à `null`.
- **Fix:** la règle de construction a été **conservée** — jeter un reset réellement obtenu serait une perte
  d'information, et `null` signifie déjà « inconnu » sans rien inventer. Le test couvre les deux volets
  (utilization seule illisible, puis les deux en-têtes illisibles), ce qui prouve la tolérance **et** le
  cas de fenêtre réellement inconnue. L'arbitrage est inscrit en XML-doc du test lui-même.
- **Verification:** `Une_fenetre_illisible_n_invalide_pas_l_autre` vert ; source **non modifiée** par la
  tâche 2 (`git diff --name-only -- …/RateLimitHeaderUsageProvider.cs` vide).
- **Committed in:** `ce26319`

**2. [Forme — précédent 18-01 déviation 3 / 18-02 déviation 2] Trois noms d'API interdits par leurs propres critères**
- **Found during:** Task 1
- **Issue:** l'action du plan demande une XML-doc disant que le contrôle de succès par exception n'est
  jamais le chemin de contrôle, que le corps n'est jamais lu et que la sonde n'est jamais un
  rafraîchisseur — tandis que trois critères d'acceptation exigent **0 occurrence** de ces noms d'API dans
  le fichier, commentaires compris. La XML-doc littérale faisait donc échouer ses propres critères.
- **Fix:** les trois raisons sont énoncées **sans nommer** les API (« contrôler le succès par exception
  détruirait l'information », « aucune désérialisation, aucune lecture de flux », « elle ne connaît ni le
  coffre chiffré, ni le jeton de renouvellement, ni le client du protocole »). Les explications sont
  intactes, les mesures satisfaites. Les noms nominatifs survivent dans le **fichier de test**, où la
  garde de la tâche 3 les interdit désormais dans la source — ils sont donc documentés *et* mesurés.
- **Verification:** les trois `grep` rendent 0 ; `Aucun_type_WPF_ni_aucune_horloge_systeme_dans_la_sonde` vert.
- **Committed in:** `170cfe4`, `fa93863`

**3. [Forme] Mesure de l'« addition pure » prise avec `^-[^-]`**
- **Found during:** Tasks 2 et 3
- **Issue:** les critères écrivent `git diff -- fichier | grep -c "^-"` == 0, mesure structurellement
  impossible puisque l'en-tête de tout `git diff` contient `--- a/fichier`. Écart déjà relevé au plan 18-02.
- **Fix:** mesure prise avec `grep -c '^-[^-]'` (lignes réellement supprimées) = **0** aux deux tâches, et
  confirmée indépendamment par `git diff --numstat` : `432 0` puis `170 0`.
- **Verification:** ci-dessus.

---

**Total deviations:** 0 auto-fix de bug (Rule 1), 0 ajout de fonctionnalité critique (Rule 2), 0 déblocage
(Rule 3), 0 question architecturale (Rule 4). 1 arbitrage d'honnêteté de la donnée et 2 ajustements de forme.
**Impact on plan:** nul sur le périmètre livré. Aucun câblage DI (plan 18-05), aucun statut serveur ni
dépassement (plan 18-04), aucune UI (plan 18-06) — les trois frontières du plan sont respectées.

## Issues Encountered

- **Le corps de la requête n'est pas lisible après l'envoi.** `EnvoyerAsync` utilise `using var req`, ce qui
  dispose le contenu : `LastRequest.Content` est mort quand le test l'inspecte (les en-têtes, l'URL et la
  méthode survivent, eux). Parade : un transport de test qui capture le corps **pendant** l'envoi
  (`AvecCaptureDuCorps`), sans toucher au faux partagé `FakeHttpMessageHandler` — que le plan n'autorisait
  pas à modifier dans cette tâche.
- **`Retry-After` posé sans validation reste typé à la lecture.** `resp.Headers.RetryAfter.Delta` vaut bien
  300 s sur un en-tête ajouté via `TryAddWithoutValidation` (l'analyse des en-têtes de .NET est paresseuse).
  Aucun `int.Parse` maison n'a donc été nécessaire.
- **Gardes permanentes re-vérifiées, 31/31 vertes** sur le filtre `ServicesLayerPurityTests` +
  `NormalisationUniqueTests` + `SettingsServiceTests` + `LastExactStoreTests` + `CompositionRootTests` : la
  sonde n'expose aucun type WPF, ne réintroduit **aucune** conversion d'unité locale (ses deux conversions
  passent par le point unique du plan 18-01), le nouveau champ de réglage ne casse aucune migration
  (précédent DEL-06), et `SchemaVersion = 1` est inchangé.
- **Avertissements `xUnit2031` de `DesktopUiaSessionSourceTests`** : préexistants, hors périmètre (aucun
  fichier touché ici), non corrigés — frontière de périmètre des règles de déviation.
- **Sécurité, contrôlée avant et après :** `%APPDATA%\Chronos\oauth.dat` = **518 octets, mtime 1783863147**
  — inchangé. Aucune requête réseau réelle : les 29 tests passent tous par `FakeHttpMessageHandler`, et la
  seule occurrence de `api.anthropic.com` dans le fichier de test est l'assertion sur
  `LastRequest.RequestUri`. Aucune écriture sous `%APPDATA%` : coffre et `settings.json` de test vivent sous
  `Path.GetTempPath()`, sous garde `Assert.StartsWith`.

## Known Stubs

**Aucun stub qui masque un chemin de données.** Deux éléments sont délibérément non branchés, et aucun
n'affiche de valeur fabriquée :

| Élément | Pourquoi ce n'est pas un oubli | Qui le résout |
|---|---|---|
| La sonde n'est **pas** enregistrée en DI | Le plan l'exclut explicitement : `App.xaml.cs` est le plan **18-05**, en un seul commit avec la garde de composition. Aucun consommateur ne peut donc rencontrer un provider à moitié câblé | **18-05** |
| `IEtatServeur` n'est **pas** implémenté par la sonde | Le plan l'interdit ici (« ne pas déclarer l'interface »). Les deux propriétés publiques `DernierResultat` et `NomsEnTetesRecus` en satisfont déjà la surface ; le statut serveur et le dépassement — les deux autres membres — sont le plan **18-04** | **18-04** |

Les deux champs de `WindowState` posés au plan 18-02 (`StatutServeur?`, `Depassement?`) restent `null` dans
tout ce que produit la sonde : c'est exactement la doctrine « `null` = inconnu », et non un stub. La sonde
ne les renseignera qu'au plan 18-04.

## User Setup Required

Aucun. Aucun service externe, aucune variable d'environnement, aucune dépendance NuGet.

**Rappel hérité de la phase 17 :** le jeton OAuth de cette machine est expiré depuis le 2026-07-12. Tant que
l'utilisateur n'a pas refait le parcours de reconnexion, la sonde ne répondra pas **en production** — elle
dégradera proprement en `RefusServeur`, sans rien inventer. Les plans 18-04 et 18-05 restent entièrement
testables. Seule la **vérification manuelle du 429 réel** (plan 18-06, tâche 3) exige une reconnexion *et*
un compte saturé, et HDR-02 ne peut pas être coché « prouvé en production » sans elle.

## Next Phase Readiness

**Prêt pour 18-04** (statut serveur + dépassement) et **18-05** (câblage DI) :

- le **tableau d'aiguillage** ci-dessus est le contrat dont 18-04 doit étendre les branches, sans en
  réordonner la lecture d'en-têtes : toute décision placée avant `LireEnTetes` annulerait HDR-02 ;
- `LireFenetre` est le point d'insertion naturel du `StatutServeur?` (les trois noms à lire par ordre de
  préférence : `-5h-status`, `-7d-status`, puis `-status` global) et `LireEnTetes` celui du
  `EtatDepassement` de la forme « overage seule » ;
- `DernierResultat` et `NomsEnTetesRecus` satisfont déjà deux des quatre membres d'`IEtatServeur` : 18-04
  n'a qu'à ajouter `Depassement` et `DepassementChange`, puis déclarer l'interface ;
- le constructeur est `(ChronosTokenAuthority, HttpClient, IClock, SettingsService)` — 18-05 doit
  enregistrer **la même instance** pour `IUsageProvider` et pour `IEtatServeur` (précédent d'`IAuthStatus`
  au plan 17-05), sans quoi l'observabilité serait publiée par un objet que personne n'appelle ;
- le chiffre de coût à écrire dans les réglages est **288 sondes/jour**, déjà verrouillé par
  `Le_cout_annonce_correspond_a_la_cadence` : le libellé de 18-06 doit dire ce chiffre, et mentionner
  l'**effet d'observation** (les en-têtes incluent la sonde) sans jamais prétendre le compenser.

**Points de vigilance transmis :**

1. **Ne jamais déplacer la lecture d'en-têtes après un test sur le code de statut.** C'est la seule ligne
   du fichier dont l'ordre est la fonctionnalité.
2. **Ne pas ajouter de conversion locale dans 18-04** : la garde `NormalisationUniqueTests` balaie le texte
   source de `Services/` et fera tomber un `FromUnixTimeSeconds` ou un `/ 100`.
3. **Ne pas citer, dans le fichier source de la sonde**, les noms d'API interdits (contrôle de succès par
   exception, désérialisation, appel de renouvellement) : la garde de la tâche 3 les interdit désormais en
   permanence, commentaires compris.
4. **HDR-02, HDR-03 et HDR-04 restent à cocher par 18-06**, pas avant — la remontée au cadran et la
   vérification manuelle en sont les conditions.

---
*Phase: 18-source-exacte-par-en-t-tes-de-rate-limit*
*Completed: 2026-09-12*

## Self-Check: PASSED

**Artefacts exigés par le plan — tous présents, tous au-dessus de leur `min_lines` :**

| Fichier | Lignes | `min_lines` | Verdict |
|---|---|---|---|
| `src/Chronos/Services/RateLimitHeaderUsageProvider.cs` | 400 | 200 | FOUND ✔ |
| `tests/Chronos.Tests/RateLimitHeaderUsageProviderTests.cs` | 859 | 280 | FOUND ✔ |
| `src/Chronos/Services/ChronosSettings.cs` (contient `SondeEnTetesActivee`) | 109 | — | FOUND ✔ |

**Commits — tous retrouvés :** `170cfe4`, `ce26319`, `fa93863`.

**Vérifications du bloc `<verification>` du plan :**

1. `dotnet test Chronos.sln -v q --nologo` → **611 réussis / 0 échec / 0 ignoré**, 0 test supprimé
   (baseline 582 → 611, soit +29).
2. `git diff --name-only -- CompositeUsageProvider.cs UsageSnapshot.cs` → **VIDE** sur les trois commits.
3. `grep -rln "RefreshAsync" src/Chronos` (hors `bin/` et `obj/`) → **exactement 2** :
   `ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs`. La sonde n'est pas un troisième rafraîchisseur.
4. `grep -rn "api.anthropic.com" tests/Chronos.Tests` → la seule occurrence du fichier de ce plan est
   l'assertion sur `LastRequest.RequestUri`. **Aucun test ne joint le réseau.**
5. `ServicesLayerPurityTests` → vert. 6. `NormalisationUniqueTests` → vert (aucune conversion locale).
   7. `SettingsServiceTests` → vert (le nouveau champ ne casse aucune migration). Filtre des 5 classes de
   garde : **31/31**.
8. Coffre intact : `%APPDATA%\Chronos\oauth.dat` = **518 octets, mtime 1783863147** avant **et** après.
   `LastExactStore.SchemaVersion = 1` inchangé ; signature de `DiagnosticService` intacte.

**Critères de succès du plan :**

- **HDR-01** — un 200 porteur rend deux fenêtres `Exact`, `utilization` en 0..1 non divisée (`0.01` / `0.63`),
  `resets_at` en epoch secondes, `CapturedAt` renseigné **par fenêtre** : prouvé par
  `Un_200_porteur_d_en_tetes_rend_deux_fenetres_exactes`, `La_casse_des_noms_d_en_tete_est_indifferente` et
  `Un_en_tete_se_lit_meme_sous_une_culture_a_virgule`.
- **HDR-02** — un 429 porteur rend un snapshot exact et frais et ne déclare jamais de déconnexion ; un 429
  muet ne rend rien : prouvé par `Un_429_livre_quand_meme_les_chiffres`,
  `Un_429_ne_declare_jamais_de_deconnexion`, `Un_429_muet_n_invente_rien`.
  **NON ENCORE PROUVÉ EN PRODUCTION** — vérification manuelle au plan 18-06, tâche 3.
- **HDR-06** — au plus une sonde par 300 s (et refus 4 fois sur 5 aux pas de 60 s du tick réel), frein
  indépendant de tout cache (prouvé deux fois : panne réseau et 401), interrupteur coupant le réseau **et**
  le jeton avec relecture fraîche, chiffre de coût verrouillé (`288`) : prouvé par les 6 tests de la région
  HDR-06.
- Aucune conversion d'unité locale, aucun corps de réponse lu, aucun second rafraîchisseur : **mesuré**, et
  désormais **gardé en permanence** par `Aucun_type_WPF_ni_aucune_horloge_systeme_dans_la_sonde`.
- 0 échec, 0 test supprimé, aucun fichier interdit touché : **vérifié**.
