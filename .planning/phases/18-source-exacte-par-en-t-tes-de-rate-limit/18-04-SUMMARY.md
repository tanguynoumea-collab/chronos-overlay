---
phase: 18-source-exacte-par-en-t-tes-de-rate-limit
plan: 04
subsystem: usage-providers
tags: [http-headers, rate-limit, statut-serveur, overage, canal-lateral, event-on-transition, record-equality, mutation-testing, xunit]

# Dependency graph
requires:
  - phase: 18-source-exacte-par-en-t-tes-de-rate-limit
    provides: "18-01 — UsageNormalization (FractionDepuisTexteFraction / InstantDepuisTexteEpoch) : point unique de conversion, consommé et jamais contourné"
  - phase: 18-source-exacte-par-en-t-tes-de-rate-limit
    provides: "18-02 — StatutServeur + StatutServeurTexte.DepuisEnTete, EtatDepassement, IEtatServeur, WindowState.StatutServeur/Depassement, EnTetesDeReference (8 jeux + 11 constantes de nom)"
  - phase: 18-source-exacte-par-en-t-tes-de-rate-limit
    provides: "18-03 — RateLimitHeaderUsageProvider : aiguillage par code de statut, en-têtes lus AVANT toute décision, DernierResultat / NomsEnTetesRecus / CadenceNominale"
provides:
  - "HDR-03 — lecture du statut déclaré par le serveur sur TROIS noms candidats (-5h-status, -7d-status, puis -status global en repli), posé sur la fenêtre qu'il décrit"
  - "HDR-04 — lecture de la famille de dépassement sur DEUX noms de statut, portée par DEUX canaux : le champ WindowState.Depassement et le canal latéral IEtatServeur"
  - "RateLimitHeaderUsageProvider EST l'implémentation d'IEtatServeur : Depassement, DernierResultat, NomsEnTetesRecus, DepassementChange — la même instance sera réexposée en DI au plan 18-05"
  - "Publication de DepassementChange sur TRANSITION uniquement (égalité structurelle de record), sur un thread du pool — l'abonné marshalle lui-même (frontière RAF-04)"
  - "EnTetesExploitables élargi : la forme « dépassement seul » (aucune fenêtre 5 h ni 7 j) est désormais une réponse exploitable et non un silence"
  - "Invariant de sécurité gravé : aucun nom d'en-tête venant du réseau ne peut remonter au diagnostic (test en casse mélangée)"
affects: [18-05-cablage-di, 18-06-ui-et-verification-manuelle, 19-doctrine-du-composite, 20-rendu-visible]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Plusieurs noms d'en-tête candidats par information, du plus spécifique au plus global, l'absence rendant toujours null — parade au nom d'en-tête pris pour acquis (la famille unified n'est documentée nulle part)"
    - "Statut de COMPTE employé comme REPLI d'un statut de FENÊTRE, jamais comme son remplaçant"
    - "Double canal pour un fait de compte produit par une seule source : champ de WindowState (voyage par référence via Best()) + canal latéral (survit à un Best() défavorable) — motif IAuthStatus"
    - "Émission sur transition garantie par l'égalité STRUCTURELLE d'un record : aucune comparaison champ par champ à maintenir"
    - "Fenêtre Unavailable construite EXPLICITEMENT plutôt que par sa fabrique neutre, pour qu'elle puisse transporter un fait de compte sans élargir la fabrique"
    - "Déduplication à la source dans Lire() : un nom sondé plusieurs fois n'est déclaré qu'une fois — le diagnostic nomme le serveur, pas notre ordre de lecture"

key-files:
  created: []
  modified:
    - src/Chronos/Services/RateLimitHeaderUsageProvider.cs
    - tests/Chronos.Tests/RateLimitHeaderUsageProviderTests.cs

key-decisions:
  - "Les appels à PublierDepassement sont placés EN TÊTE des branches 2xx et 429, avant le test d'exploitabilité, et non à l'intérieur des sous-branches SuccesEnTetesLus / SaturationEnTetesLus. C'est la seule position qui satisfasse à la fois le comportement exigé par le plan (« un quatrième GetAsync sur le jeu Absents porte le compteur à 3 avec un argument null ») et sa propre XML-doc mandatée (« mis à jour UNIQUEMENT quand des en-têtes ont été lus : 2xx ou 429 »). Un 2xx sans en-tête unifié n'est PAS un silence de transport : le serveur a répondu, son silence sur le dépassement est donc une réponse. Le grep d'acceptation (PublierDepassement == 3) est satisfait à l'identique."
  - "LireStatut et LireDepassement sont déclarées static, là où les extraits du plan les écrivaient d'instance : LireFenetre et LireEnTetes sont statiques depuis 18-03, et une méthode d'instance n'y serait pas appelable. Aucun critère d'acceptation ne porte sur ce modificateur."
  - "Lire() déduplique désormais les noms publiés. Le statut GLOBAL est sondé jusqu'à trois fois au cours d'une même lecture (repli de la 5 h, repli de l'hebdo, repli du dépassement) ; le déclarer trois fois dans NomsEnTetesRecus n'aurait rien dit de plus sur le serveur, seulement quelque chose sur notre ordre de lecture. L'ordre de première apparition est conservé, et un test l'exige (Assert.Single sur le nom global)."
  - "Le commentaire mandaté par la tâche 2 a été reformulé pour ne pas contenir le mot-clé de branchement alternatif de Python, que le critère d'acceptation du même plan interdit dans le fichier (grep == 0). Le fait rapporté — le code d'origine traite les deux familles comme mutuellement exclusives — est énoncé en français, sans le nommer."
  - "Le statut serveur n'a PAS de canal latéral, contrairement au dépassement. Raison : un statut DÉCRIT une fenêtre, donc il doit mourir avec elle quand Best() l'écarte ; un dépassement décrit le COMPTE, donc il doit survivre. La différence est écrite dans le commentaire de tête de la sonde."

patterns-established:
  - "Un test de dérivation local (Derive) crée les cas de bord propres à un plan sans polluer le point unique des jeux de référence : les huit formes réelles restent huit."
  - "Une limitation non atteignable sur la machine de développement est GRAVÉE en test avec sa justification, plutôt que livrée au hasard ou repoussée."

requirements-completed: [HDR-03, HDR-04]

# Metrics
duration: 22min
completed: 2026-09-12
---

# Phase 18 Plan 04: Statut serveur par fenêtre et dépassement par deux canaux Summary

**La sonde remonte désormais les deux informations que personne d'autre ne donne : le statut `allowed` / `allowed_warning` / `rejected` déclaré par le serveur — lu sur trois noms candidats, posé sur la fenêtre qu'il décrit, et nommé « non reconnu » plutôt que rangé d'autorité dans « autorisé » quand sa valeur est inédite — et l'usage en dépassement, lu sur les DEUX familles d'en-têtes sans branche exclusive, porté à la fois par le champ de `WindowState` et par le canal latéral `IEtatServeur` qui survit à un `Best()` défavorable, publié sur transition seulement et jamais effacé par une panne de transport.**

## Ce qui a été construit

### HDR-03 — le statut déclaré par le serveur (tâche 1, commit `4689724`)

Trois constantes de nom ajoutées au bloc groupé et daté de la sonde, chacune avec son **niveau de
confiance explicite** :

| Constante | Nom | Confiance |
|---|---|---|
| `H5hStatut` | `anthropic-ratelimit-unified-5h-status` | HAUTE — code d'origine + dump indépendant |
| `H7dStatut` | `anthropic-ratelimit-unified-7d-status` | NULLE — aucune observation, lecture optionnelle |
| `HStatutGlobal` | `anthropic-ratelimit-unified-status` | MOYENNE — code d'origine, **sans** segment de fenêtre |

**Ordre de préférence TEL QU'IMPLÉMENTÉ** (`LireStatut`, une seule méthode, deux appels au mapping unique
`StatutServeurTexte.DepuisEnTete`) :

1. le nom **de la fenêtre** (`-5h-status` pour la 5 h, `-7d-status` pour l'hebdo) ;
2. à défaut, le nom **global** `anthropic-ratelimit-unified-status` — statut de **compte**, donc repli ;
3. à défaut, `null`. Jamais une valeur devinée, jamais `Autorise` par défaut.

Conséquence vérifiée par test : un jeu portant `-5h-status: allowed` **et** le global `rejected` rend
`FiveHour = Autorise` (le spécifique gagne) **et** `SevenDay = Rejete` (le global sert de repli).

Le statut est posé **en un seul endroit**, dans la construction de fenêtre, et **jamais sur une fenêtre
indisponible** : un statut sans chiffre n'a rien à décrire. L'en-tête est néanmoins lu, pour que le
diagnostic puisse dire « le serveur a envoyé un statut mais aucun chiffre » — signal exact d'un renommage
partiel de la famille unifiée.

### HDR-04 — le dépassement, deux canaux (tâche 2, commit `3f49431`)

`RateLimitHeaderUsageProvider` déclare désormais `: IUsageProvider, IEtatServeur`. Les propriétés
`DernierResultat` et `NomsEnTetesRecus` livrées par 18-03 satisfaisaient déjà la moitié du contrat ;
s'ajoutent `Depassement` et l'événement `DepassementChange`.

`LireDepassement` lit les **trois** en-têtes de la famille de dépassement et son statut sur **deux** noms
candidats (`-overage-status`, puis le global). Un `EtatDepassement` entièrement vide n'est **jamais**
publié (`EstRenseigne`) : rien rapporté n'est pas un dépassement de 0 %.

Les **deux familles sont lues ensemble**. Le code d'origine les traite comme mutuellement exclusives, liées
au type de compte ; c'est un choix d'affichage de sa part, pas une contrainte de protocole. Un jeu portant
les deux fenêtres **et** les trois en-têtes de dépassement rend deux fenêtres `Exact` **et** un dépassement
renseigné — test explicite.

Le dépassement est posé sur les **deux** fenêtres, y compris sur une fenêtre **indisponible** :
`WindowState.Unavailable(kind)` reste la fabrique neutre (son test de neutralité l'exige), et la sonde
construit explicitement dans ce cas parce qu'une fenêtre inconnue peut néanmoins **transporter** un fait de
compte. `EnTetesExploitables` a été élargi en conséquence : sans cela, la forme « dépassement seul » aurait
été classée `SuccesSansEnTetes` et jetée.

`PublierDepassement` n'émet que sur **transition**, par égalité structurelle de record — la sonde passe 288
fois par jour, et sans cette garde l'abonné recevrait 288 notifications identiques.

## Décompte de tests — avant / après

| Étape | `RateLimitHeaderUsageProviderTests` | Suite complète |
|---|---|---|
| Avant le plan (baseline 18-03) | 29 | **611** |
| Après la tâche 1 (HDR-03, +9 tests) | 38 (plan : ≥ 37) | 620 |
| Après la tâche 2 (HDR-04, +8 tests) | **46** (plan : ≥ 45) | **628** |

**0 échec, 0 test supprimé, 0 test ignoré.** `git diff | grep '^-'` sur le fichier de test : **0
suppression réelle** — extension pure aux deux commits.

## Falsifiabilité : mutations mesurées PUIS révoquées

TDD strict reste impossible (documenté depuis 18-03 : `tests/Chronos.Tests` porte un `ProjectReference`
vers `Chronos`, donc un test référençant un type inexistant empêche toute la solution de compiler et
l'étape RED serait un commit où `dotnet test` n'est même pas invocable). La falsifiabilité est donc obtenue
par mutation.

| # | Mutation appliquée | Test tombé | Révoquée |
|---|---|---|---|
| 1 | `LireDepassement` abandonne dès que `-5h-utilization` est présent (lecture exclusive des deux familles) | `Les_DEUX_familles_d_en_tetes_sont_lues_ENSEMBLE` **[FAIL]** | oui |
| 2 | Garde `if (nouveau == Depassement) return;` retirée de `PublierDepassement` | `Le_depassement_n_est_publie_que_sur_TRANSITION` **[FAIL]** | oui |

Après révocation : `grep -c "if (nouveau == Depassement) return;"` == 1, et la suite filtrée repasse à
**46/46 verts**. Les deux mutations prouvent que les tests mesurent bien la propriété annoncée et non un
effet de bord.

## Limitation DOCUMENTÉE — la forme « dépassement seul » n'est pas atteignable ici

L'utilisateur de cette machine est sur un abonnement **Max x20**, donc sur la branche `"acct": "pro"` du
code d'origine. La forme « dépassement seul » correspond à `"acct": "ent"` : **aucune** fenêtre 5 h, **aucune**
fenêtre 7 j, le statut sur le nom global. Elle **ne pourra jamais être observée en production sur ce
compte** — et HDR-04 l'exige néanmoins.

Décision : l'implémenter et la **graver en test**
(`La_forme_depassement_seul_reste_exploitable_et_survit_par_le_canal_lateral`), avec la limitation écrite
dans la XML-doc du test lui-même pour qu'elle ne se perde pas. C'est le seul moyen honnête de livrer un
comportement non observable sans le livrer au hasard. Corollaire : le canal latéral `IEtatServeur` n'est pas
une précaution théorique — `CompositeUsageProviderTests.Le_depassement_de_la_fenetre_ecartee_est_JETE_par_le_composite`
(plan 18-02) prouve déjà que `Best()` jette le dépassement porté par une fenêtre perdante.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocage] `LireStatut` et `LireDepassement` déclarées `static`**
- **Found during:** tâche 1, puis tâche 2
- **Issue:** les extraits du plan écrivent `private StatutServeur? LireStatut(...)` et
  `private EtatDepassement? LireDepassement(...)` (méthodes d'instance). Or `LireFenetre` et `LireEnTetes`
  sont `static` depuis 18-03, et une méthode d'instance n'y est pas appelable — la solution ne compile pas.
- **Fix:** les deux méthodes sont `private static`. Aucun critère d'acceptation ne porte sur ce modificateur.
- **Files modified:** `src/Chronos/Services/RateLimitHeaderUsageProvider.cs`
- **Commits:** `4689724`, `3f49431`

**2. [Rule 1 - Contradiction interne du plan] Position des appels à `PublierDepassement`**
- **Found during:** tâche 2
- **Issue:** le `<behavior>` exige qu'un 4ᵉ `GetAsync` sur le jeu `Absents` porte le compteur d'événements
  à 3 avec un argument `null`. Or un 200 + `Absents` produit `SuccesSansEnTetes`, **pas**
  `SuccesEnTetesLus` : en plaçant les appels à l'intérieur des sous-branches nommées par le critère
  d'acceptation, aucun événement n'aurait été émis et le comportement exigé aurait été impossible. La
  XML-doc mandatée par le même plan dit, elle, « mis à jour UNIQUEMENT quand des en-têtes ont été lus
  (**2xx ou 429**) » — sans restriction d'exploitabilité.
- **Fix:** les deux appels sont placés **en tête** des branches `429` et `2xx`, avant le test
  d'exploitabilité. Doctrine appliquée : un 2xx ou un 429 **sans** en-tête de dépassement n'est pas un
  silence de transport — le serveur a répondu, donc son silence est une réponse, et publier `null` est
  honnête. Les branches de refus (401/403, 400/404), la panne réseau, le frein et l'interrupteur ne
  publient rien. Le grep d'acceptation (`PublierDepassement` == 3) est satisfait à l'identique.
- **Files modified:** `src/Chronos/Services/RateLimitHeaderUsageProvider.cs`
- **Commit:** `3f49431`

**3. [Rule 1 - Critère d'acceptation contredit par son propre commentaire mandaté] Reformulation**
- **Found during:** tâche 2
- **Issue:** le commentaire imposé mot pour mot par l'action B contient le mot-clé de branchement
  alternatif de Python, que le critère d'acceptation du même plan interdit dans le fichier (grep == 0).
  Écrire le commentaire tel quel aurait fait échouer le critère.
- **Fix:** le fait rapporté (« le code d'origine traite les deux familles comme mutuellement exclusives,
  en branches alternatives liées au type de compte ») est énoncé en français, sans nommer le mot-clé.
  Précédent explicite : déviation 3 du plan 18-01 et déviation 2 du plan 18-02, où trois noms d'API
  interdits par grep ont été décrits sans être cités.
- **Files modified:** `src/Chronos/Services/RateLimitHeaderUsageProvider.cs`
- **Commit:** `3f49431`

**4. [Rule 2 - Qualité du diagnostic] Déduplication des noms publiés dans `Lire()`**
- **Found during:** tâche 1
- **Issue:** le statut GLOBAL est sondé jusqu'à trois fois dans une même lecture (repli de la 5 h, repli de
  l'hebdo, repli du dépassement). Chaque sondage réussi l'ajoutait à `NomsEnTetesRecus`, qui est une surface
  de **diagnostic** : le rapport aurait répété un nom trois fois, ce qui ne dit rien du serveur et tout de
  notre ordre de lecture interne.
- **Fix:** `Lire()` n'ajoute un nom que s'il n'y est pas déjà. L'ordre de première apparition est conservé,
  et un test l'exige (`Assert.Single` sur le nom global dans le test de repli).
- **Files modified:** `src/Chronos/Services/RateLimitHeaderUsageProvider.cs`
- **Commit:** `4689724`

### Aucun fichier interdit touché

`git diff` **vide** sur : `CompositeUsageProvider.cs`, `UsageSnapshot.cs`, `WindowState.cs`,
`StatutServeur.cs`, `EtatDepassement.cs`, `IEtatServeur.cs`. Aucun bump de
`LastExactStore.SchemaVersion` (`grep -c "SchemaVersion = 1"` == 1) : le statut serveur et le dépassement
sont des assertions **volatiles**, les persister serait un mensonge une heure plus tard.

## Vérification

| Contrôle | Attendu | Obtenu |
|---|---|---|
| `dotnet test Chronos.sln -v q --nologo` | 0 échec | **628 / 628, 0 échec** |
| `grep -c "StatutServeurTexte.DepuisEnTete"` (après tâche 1) | 2 | 2 |
| `grep -c "anthropic-ratelimit-unified-5h-status"` | 1 | 1 |
| `grep -c "anthropic-ratelimit-unified-7d-status"` | 1 | 1 |
| déclaration `HStatutGlobal` = `"anthropic-ratelimit-unified-status"` | 1 | 1 |
| `grep -c "StatutServeur = statut"` | 1 | 1 |
| `grep -c "switch"` | 0 | 0 |
| `grep -c "IUsageProvider, IEtatServeur"` | 1 | 1 |
| `grep -cE "unified-overage-(utilization\|reset\|status)"` | 3 | 3 |
| `grep -c "EstRenseigne"` | 1 | 1 |
| `grep -c "PublierDepassement"` | 3 | 3 |
| `grep -c "DepassementChange?.Invoke"` | 1 | 1 |
| `grep -c "Depassement = depassement"` | 2 | 2 |
| `grep -c "elif"` | 0 | 0 |
| suppressions dans le fichier de test | 0 | 0 |
| `grep -c "SchemaVersion = 1"` dans `LastExactStore.cs` | 1 | 1 |
| fichiers SOURCE contenant `RefreshAsync` | 2 | 2 (`ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs`) |
| `CompositeUsageProviderTests` | vert | vert |
| `ServicesLayerPurityTests` | vert | vert |
| `NormalisationUniqueTests` | vert | vert |
| `%APPDATA%\Chronos\oauth.dat` | 518 o, mtime 1783863147 | **518 o, mtime 1783863147 — intact** |

## Sécurité

Aucune requête réseau réelle : tout passe par `FakeHttpMessageHandler`, qui intercepte avant toute
résolution de nom. Aucun accès au coffre réel — coffre et `settings.json` de test vivent sous
`Path.GetTempPath()`, gardes `Assert.StartsWith` en place. Le point de terminaison de renouvellement n'a
jamais été appelé avec le jeton réel : `oauth.dat` est inchangé (518 octets, mtime 1783863147) avant comme
après. Aucun corps HTTP lu, transporté ni journalisé. Aucune dépendance NuGet ajoutée.

## Ce que ce plan N'A PAS fait (par construction)

- **Aucun câblage DI** : `IEtatServeur` n'est pas encore enregistré — c'est le plan **18-05**.
- **Aucune surface d'UI** : ni `DiagnosticService`, ni `MainViewModel`, ni binding — c'est le plan **18-06**.
- **Aucune persistance** de ces deux informations volatiles, aucun bump de `SchemaVersion`.
- **Aucune géométrie de cadran** : phase 20.
- **Aucune modification du composite** : phase 19.

## Known Stubs

Aucun. Les deux informations sont lues, typées et exposées sur leurs deux canaux ; leur consommation par le
diagnostic et par l'UI est le périmètre explicite des plans 18-05 et 18-06, nommés dans l'objectif du plan.

## Self-Check: PASSED

- `src/Chronos/Services/RateLimitHeaderUsageProvider.cs` — FOUND (517 lignes, artefact `min_lines: 260` satisfait)
- `tests/Chronos.Tests/RateLimitHeaderUsageProviderTests.cs` — FOUND (46 tests)
- `.planning/phases/18-source-exacte-par-en-t-tes-de-rate-limit/18-04-SUMMARY.md` — FOUND
- commit `4689724` (tâche 1, HDR-03) — FOUND
- commit `3f49431` (tâche 2, HDR-04) — FOUND
- sous-suites exigées : `CompositeUsageProviderTests` 14/14, `ServicesLayerPurityTests` 2/2,
  `NormalisationUniqueTests` 3/3 — toutes vertes
- suite complète : 628/628, 0 échec
- coffre `%APPDATA%\Chronos\oauth.dat` : 518 octets, mtime 1783863147 — intact
