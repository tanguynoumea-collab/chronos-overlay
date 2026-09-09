---
phase: 17-jeton-toujours-vivant-panne-toujours-visible
plan: 01
subsystem: testing
tags: [oauth, refresh-token, rotation, xunit, characterization-tests, dpapi, http-fake]

# Dependency graph
requires:
  - phase: 16-fondations-du-delta
    provides: "collection xUnit « XAML WPF » et motif d'isolation de chemin sous Path.GetTempPath()"
provides:
  - "Première couverture dédiée de ChronosOAuthUsageProvider (13 tests) — la classe n'en avait aucune"
  - "Couverture de ChronosOAuthClient.RefreshAsync (11 cas) — jamais testé auparavant"
  - "Trois défauts d'avant-phase gravés en tests VERTS, à réécrire par 17-02 et 17-04"
  - "Deux points de construction UNIQUES (RefreshAvec, Provider) qui rendent les refontes 17-02/17-04 bon marché"
affects: [17-02, 17-04, 17-05, 18-source-entetes]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Test de caractérisation : graver le comportement ACTUEL, défauts inclus, avant réécriture"
    - "Point de construction unique par classe sous test (une seule ligne à changer à la refonte)"
    - "Coffre DPAPI de test sur chemin injecté sous %TEMP% + garde Assert.StartsWith"

key-files:
  created:
    - tests/Chronos.Tests/ChronosOAuthUsageProviderTests.cs
  modified:
    - tests/Chronos.Tests/ChronosOAuthClientTests.cs

key-decisions:
  - "TOK-01/TOK-02 NON marqués complets : ce plan ne livre que la couverture, pas le correctif"
  - "Le recul 429 sans cache est un TROISIÈME défaut découvert et gravé (le plan n'en prévoyait que deux)"
  - "Corps JSON tronqué écrit en littéral échappé, pas en raw string (ambiguïté de guillemets de fermeture)"

patterns-established:
  - "Défaut documenté : XML-doc qui cite fichier:ligne, explique la conséquence utilisateur, et nomme le plan qui RÉÉCRIRA le test"
  - "Garde de sécurité systématique : aucun secret ne doit apparaître dans RequestUri"

requirements-completed: []

# Metrics
duration: 18min
completed: 2026-09-09
---

# Phase 17 Plan 01 : Wave 0 de couverture — Summary

**Le chemin OAuth le plus sensible du produit (rotation de refresh token) passe de 0 à 24 tests, dont trois qui gravent en vert les défauts exacts que la phase 17 doit éradiquer.**

## Performance

- **Duration:** 18 min
- **Started:** 2026-09-09T14:02:11Z
- **Completed:** 2026-09-09T14:20:37Z
- **Tasks:** 2/2
- **Files modified:** 2 (1 créé, 1 étendu) — **0 fichier de production**

## Accomplishments

- **`ChronosOAuthUsageProvider` — la classe centrale de la phase — avait littéralement zéro test.** Elle en a 13 : normalisation d'unité 0..100 → 0..1, `resets_at` ISO 8601, les trois en-têtes exigés, fenêtre absente → `Unavailable`, 500 / exception réseau → `Empty` sans jamais lever, repli sur cache, recul 429.
- **`ChronosOAuthClient.RefreshAsync` n'était pas couvert non plus** (les 4 tests existants ne touchaient que `CreatePkce` / `BuildAuthorizeUrl` / `SplitCodeState`). 11 cas ajoutés, dont la preuve que le 200 nominal fait bien **tourner** le refresh token.
- **Trois défauts nommés, verts aujourd'hui**, chacun avec une XML-doc qui cite `fichier:ligne`, explique la conséquence vécue par l'utilisateur, et désigne le plan qui réécrira le test.
- **419 → 443 tests, 0 échec.** Aucun octet modifié sous `src/`, aucun appel réseau réel, coffre réel byte-identique.

## Task Commits

1. **Task 1 : Couvrir `RefreshAsync` et nommer la confusion des causes** — `cef82e1` (test)
2. **Task 2 : Créer `ChronosOAuthUsageProviderTests`** — `e3e326c` (test)

**Plan metadata:** voir commit `docs(17-01)` final.

## Files Created/Modified

- `tests/Chronos.Tests/ChronosOAuthUsageProviderTests.cs` *(créé, 315 lignes, 13 tests)* — première couverture du provider ; helper `Provider(...)` = point de construction unique que 17-04 réécrira ; coffre DPAPI isolé sous `%TEMP%`.
- `tests/Chronos.Tests/ChronosOAuthClientTests.cs` *(+121 lignes, −0)* — les 4 tests pré-existants sont **intacts** (le commit ne contient que des insertions) ; helper `RefreshAvec(...)` = point unique pour 17-02.

## Les trois défauts gravés

| Test (vert aujourd'hui) | Ce qu'il prouve | Réécrit par |
|---|---|---|
| `Toutes_les_causes_d_echec_se_confondent_en_un_seul_null` | 400, 401, 429, 500 et 200-tronqué produisent **le même `null`**. `ChronosOAuthClient.cs:106` + `:119` détruisent la cause — donc TOK-02 (rendre la panne visible **et actionnable**) est mécaniquement impossible. | 17-02 |
| `Un_401_est_aujourd_hui_totalement_muet` | Le 401 est traité comme un 500 ou un câble débranché : recul silencieux, `Empty`, `SourceCapturedAt` null, **aucun état exposé, aucun rejeu, aucune invalidation du coffre**. C'est le mécanisme exact des deux mois de chiffres faux. | 17-04 |
| `Le_rafraichissement_est_aujourd_hui_paresseux_declenche_par_le_GetAsync` | Le compteur de refresh reste à 0 jusqu'au `GetAsync`, puis passe à 1 : le jeton n'est renouvelé **que quand quelqu'un regarde le cadran**. Absence de TOK-01. | 17-04 |

## Decisions Made

- **`requirements-completed` laissé VIDE.** Les cinq plans de la phase 17 déclarent tous `TOK-01, TOK-02` en frontmatter, mais ce plan ne livre **aucun** des deux : il prouve au contraire qu'ils ne sont pas satisfaits. Les cocher ici aurait marqué la traçabilité « Done » alors que 17-02 → 17-05 doivent encore livrer. `REQUIREMENTS.md` reste donc à `Pending`.
- **Corps JSON tronqué en littéral échappé** (`"{\"access_token\":\"ACC-2\""`) plutôt qu'en raw string `"""...\""""` comme le suggérait le plan : une séquence de 4 guillemets en fin de raw string est ambiguë au regard du comptage du délimiteur de fermeture. Le littéral échappé est sans équivoque et produit exactement le même JSON invalide.
- **Helper `Provider(...)` écrit avec le nom de type explicite** (`new ChronosOAuthUsageProvider(...)`) et non en `new(...)` ciblé, pour satisfaire simultanément le code d'exemple du plan et son critère d'acceptation par `grep`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug dans le test attendu] Le test 429 du plan asserte un comportement que le code n'a pas**

- **Found during:** Task 2
- **Issue:** Le plan prescrivait `Un_429_recule_et_le_rappel_immediat_n_emet_aucune_requete` avec `Assert.Equal(1, usage.SendCount)` après deux `GetAsync` consécutifs. Le code réel ne fait pas ça : le garde-fou de tête (`ChronosOAuthUsageProvider.cs:53`) exige **deux** conditions, `now < _nextAllowedCall` **ET** `_cached is not null`. Sans appel réussi préalable, `_cached` est null, le garde-fou ne s'applique pas, et la seconde requête part quand même. `SendCount` vaut **2**. Le test du plan aurait échoué.
- **Fix:** Le comportement réel a été gravé, et le fait qu'il soit contre-intuitif a été traité comme ce qu'il est — **un troisième défaut**, pas un détail de test. Deux tests remplacent celui du plan :
  - `Un_429_sans_cache_ne_freine_aujourd_hui_absolument_rien` (DÉFAUT documenté, assert `SendCount == 2`) ;
  - `Apres_un_200_le_recul_sur_429_tient_et_le_rappel_n_emet_aucune_requete` (le recul **fonctionne** dès qu'un cache existe — garde-fou de falsifiabilité : sans lui, le premier test ne prouverait rien).
- **Portée réelle du défaut :** `_cached` est un champ d'instance en RAM (constat déjà au dossier dans `STATE.md`). À **chaque démarrage de l'exe**, il est vide. Un exe qui redémarre avec un jeton mort remartèle donc l'endpoint à chaque tick, sans aucun frein — ce qui entretient le 429 qui l'a causé. C'est le cas nominal, pas un cas limite.
- **Files modified:** `tests/Chronos.Tests/ChronosOAuthUsageProviderTests.cs`
- **Verification:** 13/13 verts ; le comportement était déjà documenté en commentaire dans `ClaudeOAuthUsageProviderTests.cs:303`, ce qui confirme l'analyse.
- **Committed in:** `e3e326c`

**2. [Rule 3 — Critères d'acceptation contradictoires] `grep` du plan vs code d'exemple du plan**

- **Found during:** Task 2
- **Issue:** Le plan fournissait le helper en `=> new(coffre, ...)` (ciblé) mais exigeait `grep -c "new ChronosOAuthUsageProvider(" → exactement 1`. Les deux ne pouvaient pas être vrais ensemble.
- **Fix:** Nom de type explicite dans le helper. L'intention (« point de construction unique ») et le critère littéral sont désormais tous deux satisfaits.
- **Committed in:** `e3e326c`

**3. [Rule 3 — Blocage outillage] Heredoc shell impossible sur le fichier de Task 2**

- **Found during:** Task 2
- **Issue:** L'écriture par `cat <<'EOF'` échouait (`unexpected EOF while looking for matching quote`) sur un contenu C# mêlant raw strings, accents et apostrophes françaises.
- **Fix:** Bascule sur l'outil `Write`. Aucun impact sur le livrable.

---

**Total deviations:** 3 auto-fixed (1× Rule 1, 2× Rule 3).
**Impact on plan:** Aucune dérive de périmètre. La déviation n° 1 **augmente** la valeur du plan : elle ajoute un troisième défaut documenté sur le chemin exact que 17-04 doit refondre, et le fait avec un test-témoin qui garantit sa falsifiabilité.

## Écarts de critères d'acceptation assumés

- **Task 1, critère `grep -niE "oauth\.dat|GetFolderPath|ApplicationData" → aucun résultat` :** une occurrence subsiste, ligne 77 — dans le **commentaire de sécurité que l'action du plan exigeait d'écrire**, et qui dit précisément qu'il ne faut jamais toucher ce fichier. Le plan se contredit ; l'intention du critère (aucun **code** n'accède au coffre réel) est vérifiée : aucun `new ChronosOAuthStore()` sans chemin, aucune lecture de `%APPDATA%`, coffre réel byte-identique après la suite.

## Issues Encountered

Aucune. Les 13 tests de Task 2 sont passés au premier lancement, y compris les trois prédictions de comportement les plus fines (recul 429 sans cache, repli sur cache à 3 min, rotation du jeton consommée par l'appel d'usage suivant).

## Vérification finale

| Contrôle | Résultat |
|---|---|
| `dotnet test Chronos.sln -v q --nologo` | **443 réussis / 0 échec** (baseline 419, cible ≥ 438) |
| `ServicesLayerPurityTests` + `CompositionRootTests` | 5/5 verts |
| `git status --porcelain src/` | **vide** |
| `git diff --name-only HEAD~2 -- src/` | **vide** |
| Coffre réel `%APPDATA%\Chronos\oauth.dat` | **518 o, mtime 1783863147 — identique avant/après** |
| Aucun jeton journalisé dans `src/` | vérifié (seul hit : `DiagnosticService.cs:220`, qui écrit le *nom* du champ et « PRÉSENT »/« absent », jamais la valeur — pré-existant, non touché) |
| Réseau réel joint | aucun — 100 % `FakeHttpMessageHandler` |

## Known Stubs

Aucun. Ce plan ne livre que des tests ; aucun composant d'UI, aucune donnée factice câblée à un rendu.

## Notes d'hygiène (hors périmètre)

Les tests laissent derrière eux un dossier `%TEMP%\ChronosOAuthProviderTest_<guid>\` par appel de `CoffreAvec` (39 après une passe complète). C'est le motif **déjà en vigueur** dans le dépôt (`MainViewModelTests`, `CompositionRootTests`) ; le corriger ici sortirait du périmètre et toucherait des fichiers sans rapport avec ce plan.

## Next Phase Readiness

Le filet est posé pour les deux refontes de la phase :

- **17-02** (typage des causes de `RefreshAsync`) n'a qu'un point à toucher : le corps de `RefreshAvec(...)`. Sa cible est le `[Theory]` à 5 cas, qui devra passer de « toutes égales à `null` » à « chaque cause est distincte ».
- **17-04** (extraction de `ChronosTokenAuthority`) n'a qu'un point à toucher : le corps de `Provider(...)`. Ses cibles sont les trois défauts nommés — 401 muet, refresh paresseux, recul 429 sans cache.

**Point d'attention pour 17-04 :** le troisième défaut (recul 429 inopérant sans cache) n'était pas au plan de phase. Comme `_cached` vit en RAM, il se déclenche à **chaque** démarrage de l'exe, et non dans un cas limite. L'autorité unique de jeton devrait porter sa politique de backoff indépendamment de tout cache d'usage — sans quoi le symptôme survivra à la refonte.

**Aucun blocage.** `TOK-01`, `TOK-02`, `TOK-03` restent à `Pending` dans `REQUIREMENTS.md`, ce qui est l'état honnête.

---
*Phase: 17-jeton-toujours-vivant-panne-toujours-visible*
*Completed: 2026-09-09*

## Self-Check: PASSED

- `tests/Chronos.Tests/ChronosOAuthUsageProviderTests.cs` — présent (315 insertions, création confirmée)
- `tests/Chronos.Tests/ChronosOAuthClientTests.cs` — présent (121 insertions, **0 suppression**)
- `.planning/phases/17-.../17-01-SUMMARY.md` — présent
- Commits `cef82e1` et `e3e326c` — présents dans l'historique
