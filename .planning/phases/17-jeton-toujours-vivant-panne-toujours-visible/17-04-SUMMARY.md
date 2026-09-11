---
phase: 17-jeton-toujours-vivant-panne-toujours-visible
plan: 04
subsystem: auth
tags: [oauth, refresh-token, rotation, 401-replay, backoff, ihostedservice, dependency-injection, diagnostic, xunit]

# Dependency graph
requires:
  - phase: 17-jeton-toujours-vivant-panne-toujours-visible
    provides: "17-03 : ChronosTokenAuthority (autorité unique) et TokenRefreshService (IHostedService), tous deux prouvés mais AUCUN enregistré dans le graphe DI"
  - phase: 17-jeton-toujours-vivant-panne-toujours-visible
    provides: "17-02 : EtatAuthentification, IAuthStatus, ChronosOAuthClient à horloge injectable"
  - phase: 17-jeton-toujours-vivant-panne-toujours-visible
    provides: "17-01 : couverture de la Wave 0 + la DÉCOUVERTE que le recul anti-429 du provider est inopérant sans cache"
  - phase: 16-fondations-du-delta
    provides: "IClock/FakeClock, isolation des chemins sous Path.GetTempPath(), collection xUnit « XAML WPF »"
provides:
  - "UN SEUL rafraîchisseur dans l'application : ChronosOAuthUsageProvider est devenu un simple consommateur de l'autorité (plus de _store, plus de _client, plus de refresh paresseux)"
  - "Chemin « 401 → invalider → UN SEUL rejeu → sinon Deconnecte » : le 401 muet de deux mois est mort"
  - "Un 2xx exploité déverrouille l'état d'authentification (SignalerSucces) — la pastille de 17-05 pourra disparaître d'elle-même"
  - "Recul anti-429 du provider DÉCOUPLÉ de l'existence d'un cache : le frein survit au redémarrage de l'exe"
  - "Câblage DI réel : autorité singleton + alias IAuthStatus sur la MÊME instance + TokenRefreshService hébergé — TOK-01 est fonctionnellement acquis"
  - "Garde DI réelle prouvant l'unicité de l'instance (Assert.Same) et l'enregistrement en IHostedService"
  - "DiagnosticService nomme l'état d'authentification RÉEL via un paramètre optionnel (0 site de construction cassé)"
affects: [17-05, 18-source-entetes, 19-doctrine-composite, 20-honnetete-visible]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Ctor + unique site de construction changés dans le MÊME commit (invariant « solution compilable à chaque commit »)"
    - "Rejeu borné par une garde locale non réarmable plutôt que par une boucle"
    - "Classification des codes HTTP par ACTIONNABILITÉ : 401/403 = refus de compte, 429/5xx/réseau = temporaire, jamais une déconnexion"
    - "Découplage « ai-je le droit d'appeler » / « ai-je un cache à servir » dans un garde-fou de débit"
    - "Instance unique réexposée sous une interface (AddSingleton<I>(sp => sp.GetRequiredService<T>())) + AddHostedService sur la même instance"
    - "Paramètre ctor OPTIONNEL en DERNIÈRE position pour ramener un rayon de souffle de N sites à 0"
    - "Propriété Path exposée sur un magasin pour permettre une garde anti-accident Assert.StartsWith en test (motif LastExactStore.Path)"
    - "Tests de défaut RÉÉCRITS en tests du comportement corrigé, chacun citant en doc le test qu'il remplace"

key-files:
  created: []
  modified:
    - src/Chronos/Services/ChronosOAuthUsageProvider.cs
    - src/Chronos/Services/ChronosOAuthStore.cs
    - src/Chronos/Services/DiagnosticService.cs
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/ChronosOAuthUsageProviderTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs

key-decisions:
  - "Le provider perd le DROIT de rafraîchir dans le même geste où l'autorité est câblée : une demi-mesure aurait fait coexister deux rotations concurrentes du refresh token, donc un invalid_grant sur la seconde, donc une FAUSSE déconnexion sur un compte sain (claude-code#25609)."
  - "Le rejeu sur 401 n'est pas un raffinement mais une obligation : le serveur peut révoquer un jeton AVANT son ExpiresAt local (claude-code#54443), donc le rafraîchissement préventif seul ne suffit jamais."
  - "Le garde-fou de débit du provider ne consulte plus le cache : « now < _nextAllowedCall » suffit désormais à freiner. Le cache étant un champ d'instance en RAM, le frein disparaissait à chaque démarrage de l'exe — précisément quand le martèlement se produit."
  - "403 ne déclenche AUCUN rejeu (scope/abonnement : un jeton frais n'y changerait rien) ; 429/5xx/réseau ne déclarent JAMAIS Deconnecte."
  - "IAuthStatus est un ALIAS de l'instance d'autorité, jamais une seconde autorité, et la garde DI le prouve par Assert.Same plutôt que par lecture de code."
  - "Le nouveau paramètre de DiagnosticService est optionnel et en dernière position : 9 sites de construction préexistants compilent sans une seule retouche."

patterns-established:
  - "Autorité unique consommée : tout appelant de l'usage demande son jeton, ne le stocke pas, et signale au retour (succès / refus serveur)"
  - "Garde DI réelle par sous-chaîne : reproduire littéralement un bloc d'App.ConfigureServices dans un test pour attraper ce que dotnet build ne voit pas"
  - "Garde anti-accident systématique sur les magasins de test : Assert.StartsWith(Path.GetTempPath(), store.Path)"

requirements-completed: [TOK-01, TOK-02]

# Metrics
duration: 26min
completed: 2026-09-12
---

# Phase 17 Plan 04: Autorité de jeton câblée, second rafraîchisseur supprimé Summary

**`ChronosOAuthUsageProvider` perd son refresh paresseux et devient consommateur de l'autorité unique (rejeu unique sur 401 → sinon « Deconnecte »), `TokenRefreshService` est enfin hébergé par le host, et le diagnostic nomme l'état d'authentification réel — TOK-01 est fonctionnellement acquis.**

## Performance

- **Duration:** 26 min
- **Started:** 2026-09-11T22:24:00Z
- **Completed:** 2026-09-11T22:50:00Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments

- **Il n'existe plus qu'UN SEUL rafraîchisseur dans tout le dépôt.** `grep -rln "RefreshAsync" src/Chronos` → exactement 2 fichiers : `ChronosOAuthClient.cs` (qui la définit) et `ChronosTokenAuthority.cs` (qui l'appelle). Le provider d'usage ne connaît plus ni le coffre, ni le refresh token, ni le client OAuth.
- **Le 401 muet de deux mois est mort.** 401 → `InvaliderAccessToken` → **un seul** rejeu avec un jeton frais → si le refus persiste, `SignalerRefusServeur` et état `Deconnecte` observable. 401 puis 200 répare tout seul sans rien signaler.
- **TOK-01 est fonctionnellement acquis** : `TokenRefreshService` est enregistré comme `IHostedService` et réellement démarré par le host (premier tick immédiat). Un exe laissé tourner voit désormais son jeton renouvelé sans que personne ne regarde le cadran.
- **Le recul anti-429 du provider s'applique enfin sans cache** — le défaut n° 3 découvert par 17-01, qui subsistait sur le chemin de l'usage après avoir été éradiqué sur celui du jeton, est corrigé et testé (`Le_recul_freine_des_le_PREMIER_429_sans_aucun_cache_prealable`).
- **Le diagnostic ne confond plus « le fichier existe » et « authentifié »** : une ligne « État d'authentification » distingue HORS LIGNE (informatif) de DÉCONNECTÉ (actionnable), sans casser un seul site de construction ni une seule chaîne existante du rapport.
- **491 tests verts** (484 avant le plan, +7 nets), aucun test supprimé, les 3 tests de la Wave 0 qui nommaient les défauts ont été **réécrits** en tests du comportement corrigé.

## Task Commits

1. **Task 1: Le provider perd son refresh, gagne le rejeu unique sur 401, et App.xaml.cs suit dans le MÊME commit** — `299d6ba` (feat)
2. **Task 2: Le diagnostic dit l'état d'authentification RÉEL — paramètre optionnel, 0 site cassé** — `1f0e68a` (feat)

## Files Created/Modified

- `src/Chronos/Services/ChronosOAuthUsageProvider.cs` — réécrit : ctor `(ChronosTokenAuthority, HttpClient, IClock)`, plus aucun refresh, `EnvoyerAsync` extrait, rejeu unique sur 401, `SignalerSucces`/`SignalerRefusServeur`, garde-fou de débit découplé du cache.
- `src/Chronos/Services/ChronosOAuthStore.cs` — additif : `public string Path => _path;` (chemin seul, jamais le contenu) + qualification des appels en `System.IO.Path.*` pour lever la collision de nom. `Save`/`Load`/`Clear` intacts mot pour mot ; 8 lignes changées.
- `src/Chronos/App.xaml.cs` — `ChronosOAuthClient` gagne son `IClock` ; autorité de jeton enregistrée **une** fois ; `IAuthStatus` aliasé sur la même instance ; `TokenRefreshService` en singleton + `AddHostedService` ; provider construit à 3 arguments ; `DiagnosticService` gagne son 6ᵉ argument.
- `src/Chronos/Services/DiagnosticService.cs` — `IAuthStatus? authStatus = null` en dernière position, ligne « État d'authentification », helper `LibelleAuth`.
- `tests/Chronos.Tests/ChronosOAuthUsageProviderTests.cs` — helper `Provider` réécrit (rend le tuple `(provider, autorite)`), 3 tests de défaut réécrits, 5 tests ajoutés → 18 tests.
- `tests/Chronos.Tests/CompositionRootTests.cs` — `IAuthStatus` ajouté au conteneur miroir (anticipe le ctor de `MainViewModel` du plan 17-05) + garde DI réelle `Le_graphe_DI_resout_l_autorite_de_jeton_et_son_service_de_fond`.
- `tests/Chronos.Tests/DiagnosticServiceTests.cs` — 1 test ajouté, les 2 existants intouchés.

## Decisions Made

Voir `key-decisions` en frontmatter. Les deux qui comptent pour la suite :

1. **Le découplage du garde-fou de débit** (exigence supplémentaire du prompt, hors plan écrit) : le provider ne retourne plus `_cached` directement dans la fenêtre de recul mais passe par `ServeCachedOr`. Effet de bord voulu : un cache de plus de 15 min (`CacheUsable`) n'est plus resservi pendant un recul, alors que l'ancien code le servait indéfiniment. C'est plus honnête et cohérent avec la doctrine de la phase ; aucun test existant ne couvrait ce cas.
2. **La garde DI ne touche jamais le réseau** : elle résout le graphe et vérifie l'identité des instances, mais n'appelle **jamais** `GetAccessTokenAsync`. Le coffre de test vit sous `Path.GetTempPath()`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Encodage et fins de ligne accidentellement altérés sur `ChronosOAuthStore.cs`**
- **Found during:** Task 1 (ajout de la propriété `Path`)
- **Issue:** le script d'édition a réécrit le fichier en LF + BOM UTF-8, alors que l'original était CRLF sans BOM. Le diff gonflait à 10 lignes changées (critère d'acceptation : ≤ 8) et un BOM introduit dans un fichier qui n'en avait pas est une modification silencieuse non désirée.
- **Fix:** normalisation CRLF puis suppression du BOM ; diff ramené à 6 insertions / 2 suppressions = 8 lignes.
- **Files modified:** `src/Chronos/Services/ChronosOAuthStore.cs`
- **Verification:** `git diff --stat` → `6 insertions(+), 2 deletions(-)` ; `git diff | grep -c "Clear"` → 0.
- **Committed in:** `299d6ba`

**2. [Rule 3 - Blocking] Constantes de corps de réponse manquantes dans les nouveaux tests**
- **Found during:** Task 1 (écriture des tests 429/401/403)
- **Issue:** les nouveaux tests référençaient `CorpsRateLimit` et `CorpsRefus`, qui n'existaient pas — la compilation du projet de test aurait échoué, donc `dotnet test` n'aurait pas pu s'exécuter du tout.
- **Fix:** déclaration des deux constantes à côté de `CorpsNominal`, avec commentaire sur leur rôle (429 = temporaire, 401/403 = refus).
- **Files modified:** `tests/Chronos.Tests/ChronosOAuthUsageProviderTests.cs`
- **Verification:** `dotnet build` → 0 erreur ; les 18 tests du fichier passent.
- **Committed in:** `299d6ba`

**3. [Rule 1 - Bug] Deux critères d'acceptation littéralement infalsifiables en l'état**
- **Found during:** Task 1 (vérification des greps)
- **Issue:** le critère `grep -cE "…|ChronosOAuthStore|ChronosOAuthClient|…" ChronosOAuthUsageProvider.cs → 0` était mis en échec par un `<see cref="ChronosOAuthStore"/>` dans la XML-doc qui expliquait justement que le provider **ne** connaît **plus** ce type. Idem pour la vérification 3 du plan (`RefreshAsync` dans exactement 2 fichiers), mise en échec par une occurrence dans un **commentaire** d'`App.xaml.cs`.
- **Fix:** reformulation des deux commentaires (« ni le coffre chiffré », « le droit de rafraîchir ») sans perdre un mot de leur POURQUOI. Les greps sont désormais de vraies gardes : toute réintroduction de ces types dans le code sera détectée.
- **Files modified:** `src/Chronos/Services/ChronosOAuthUsageProvider.cs`, `src/Chronos/App.xaml.cs`
- **Verification:** grep pureté → `0` ; `grep -rln "RefreshAsync" src/Chronos` → exactement 2 fichiers.
- **Committed in:** `299d6ba`

### Écarts de forme constatés, non corrigés (documentés)

- **Le plan comptait 8 sites de construction de `DiagnosticService`, il y en avait 9** (1 en production + 8 en tests sur 6 fichiers, désormais 10 avec le nouveau test). Le critère « `grep -rc "new DiagnosticService("` → 9 » lit donc **10**. Aucune conséquence : le paramètre étant optionnel et en dernière position, **0** site a été cassé — c'est l'invariant réel que le critère cherchait à protéger.
- **Deux critères de grep sont satisfaits en substance mais pas au chiffre exact**, parce que le plan prescrit lui-même le texte de documentation qui les met en échec :
  - `grep -c "État d'authentification" DiagnosticService.cs` → **2** au lieu de 1 : une occurrence est la ligne du rapport, l'autre est le `<param>` de XML-doc que le plan dicte mot pour mot.
  - `grep -n "Un_401_est_aujourd_hui_totalement_muet\|Le_rafraichissement_est_aujourd_hui_paresseux" tests/` → 4 résultats au lieu d'aucun, tous en **XML-doc** (« remplace `<c>…</c>` »), exactement comme le plan le demande dans son propre exemple de docstring. Vérification de substance : `grep "public async Task Un_401_est_aujourd_hui_totalement_muet"` → aucun résultat. Aucune **méthode de test** ne porte plus ces noms, et `TokenRefreshServiceTests.cs` (plan 17-03) contenait déjà une telle référence documentaire.
- **Critère « aucun jeton en clair » (vérification 7)** : le grep remonte `DiagnosticService.cs:220`, ligne **préexistante** qui affiche le NOM de clé `claudeAiOauth.accessToken` suivi de « PRÉSENT »/« absent », jamais une valeur. Hors périmètre de ce plan (scope boundary), et le test anti-fuite `Assert.DoesNotContain("SECRET-TOKEN", report)` reste vert.

---

**Total deviations:** 3 auto-fixed (2 bugs, 1 blocking) + 3 écarts de forme documentés.
**Impact on plan:** aucune dérive de périmètre. Les trois corrections étaient nécessaires à la compilation ou à la falsifiabilité des gardes ; les écarts de forme sont des imprécisions du plan, pas des manquements du livrable.

## Issues Encountered

- **Le plan ne pouvait pas être exécuté en TDD strict** (`tdd="true"` sur la tâche 1) : le ctor du provider change de forme, donc écrire les tests d'abord aurait produit un état où le projet de test ne compile pas — et `tests/Chronos.Tests` ayant un `ProjectReference` vers `Chronos`, **aucun** test de la solution n'aurait pu s'exécuter. Le plan lui-même tranche ce conflit (« Il ne doit plus exister d'échec de build attendu et localisé ») : source et tests livrés dans un commit unique et compilable. La falsifiabilité est conservée autrement — chaque test réécrit cite dans sa doc le test d'avant-phase qu'il remplace, et ces tests-là étaient verts sur le code fautif.
- **Aucun autre obstacle.** Le coffre réel `%APPDATA%\Chronos\oauth.dat` a été mesuré avant (518 o, mtime 1783863147) et après la suite complète (518 o, mtime 1783863147) : **inchangé**. Aucun appel de refresh n'a été émis avec le refresh token réel.

## User Setup Required

None — aucun service externe à configurer, aucune dépendance NuGet ajoutée (`git diff` sur les deux `.csproj` → vide).

## Next Phase Readiness

**Prêt pour le plan 17-05 (la visibilité).** Ce qui l'attend est déjà en place :

- `IAuthStatus` est enregistré dans le graphe DI réel **et** dans le conteneur miroir de `CompositionRootTests` — le ctor de `MainViewModel` peut en dépendre sans casser la garde de composition.
- L'autorité publie ses transitions via `EtatChange` sur un thread du pool ; l'abonné devra marshaller lui-même (motif `RefreshOrchestrator.SnapshotChanged`).
- `SignalerSucces` est déjà appelé sur chaque 2xx exploité : la pastille pourra disparaître d'elle-même sans code supplémentaire côté provider.
- `ReinitialiserApresLogin` (TOK-03) existe sur l'interface et n'a **aucun appelant** : c'est au plan 17-05 de le brancher sur le retour du login, sans quoi la pastille survivra à sa propre réparation.

**Points de vigilance :**

- `ChronosOAuthUsageProvider` n'expose toujours aucune trace de l'état vers le `UsageSnapshot` — c'est voulu (17-02 l'a tranché : l'état d'auth ne traverse pas deux composites dont `Best()` choisit par fenêtre). La pastille doit donc lire `IAuthStatus`, pas le snapshot.
- La distinction visuelle frais / daté / indisponible du cadran reste la **phase 20**, et la source par en-têtes de rate-limit la **phase 18** : rien n'a été anticipé sur ces deux fronts.

---
*Phase: 17-jeton-toujours-vivant-panne-toujours-visible*
*Completed: 2026-09-12*

## Self-Check: PASSED

- 7 fichiers modifiés + le SUMMARY : tous présents sur disque.
- Commits `299d6ba` et `1f0e68a` : présents dans l'historique.
- 18 tests dans `ChronosOAuthUsageProviderTests.cs`, 491 au total, 0 échec.
- Aucun stub, aucun TODO/FIXME/placeholder dans les fichiers livrés.
- `%APPDATA%\Chronos\oauth.dat` : 518 o, mtime 1783863147 — identique avant et après.
