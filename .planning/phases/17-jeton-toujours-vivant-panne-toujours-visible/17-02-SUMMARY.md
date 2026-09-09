---
phase: 17-jeton-toujours-vivant-panne-toujours-visible
plan: 02
subsystem: auth
tags: [oauth, refresh-token, rotation, rfc6749, rate-limit, enum, event, iclock, xunit]

# Dependency graph
requires:
  - phase: 17-jeton-toujours-vivant-panne-toujours-visible
    provides: "Wave 0 de couverture (17-01) : le [Theory] à 5 cas qui gravait la confusion des causes, et le point de construction unique RefreshAvec(...)"
  - phase: 16-fondations-du-delta
    provides: "IClock/SystemClock injectés partout dans le pipeline, motif d'horloge déterministe sous test"
provides:
  - "EtatAuthentification — vocabulaire d'état à 4 valeurs, neutre, dans Chronos.Services"
  - "IAuthStatus — canal d'état observable (Etat + EtatChange + ReinitialiserApresLogin), motif RefreshOrchestrator.SnapshotChanged"
  - "FakeAuthStatus — faux public prêt pour les 5 sites new MainViewModel du plan 17-05"
  - "ResultatRafraichissement / IssueRafraichissement — la cause d'un échec de rafraîchissement survit désormais à l'échec"
  - "RefreshAsync typé : 400/401 => IdentifiantsRejetes, 429/5xx/réseau/timeout => EchecTemporaire, 200 inexploitable => IdentifiantsRejetes"
  - "ChronosOAuthClient à horloge injectable (IClock? optionnel) — ExpiresAt vérifiable à la seconde"
  - "LireJetonsAsync — parsing unique partagé par l'échange de code et le rafraîchissement"
affects: [17-03, 17-04, 17-05, 18-source-entetes]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Résultat typé sans champ texte : la sécurité d'un type est prouvée par un test réflexif sur GetProperties(), pas par une convention"
    - "Paramètre de constructeur optionnel EN DERNIÈRE POSITION pour rendre une dépendance injectable sans casser un seul site d'appel"
    - "Filtre d'exception nominatif (when ex is …) + inspection de la réponse déjà reçue, à la place d'un catch fourre-tout"

key-files:
  created:
    - src/Chronos/Services/EtatAuthentification.cs
    - src/Chronos/Services/IAuthStatus.cs
    - tests/Chronos.Tests/Fakes/FakeAuthStatus.cs
  modified:
    - src/Chronos/Services/ChronosOAuthClient.cs
    - src/Chronos/Services/ChronosOAuthUsageProvider.cs
    - tests/Chronos.Tests/ChronosOAuthClientTests.cs

key-decisions:
  - "requirements-completed laissé VIDE : ce plan pose le vocabulaire, il ne livre ni le rafraîchissement préventif (TOK-01) ni la pastille visible (TOK-02)"
  - "Le nom du test de la Wave 0 est retiré des commentaires : un grep du nom disparu doit rendre zéro, la traçabilité passe par le hash de commit cef82e1"
  - "Le <see cref> vers ReinitialiserApresLogin retiré du faux : il faisait mentir le critère grep -c → 1"
  - "BOM UTF-8 retiré des 3 fichiers touchés : l'outillage d'édition en avait ajouté un, absent de l'état d'origine"

patterns-established:
  - "Vocabulaire d'état avant mécanique : un plan entier ne livre QUE des types et des contrats, sans câblage DI ni service, pour que les plans suivants n'aient plus de vocabulaire à inventer"
  - "Réécriture d'un test de caractérisation : même fichier, même sujet, nom changé, assertions inversées — jamais une suppression"

requirements-completed: []

# Metrics
duration: 7min
completed: 2026-09-09
---

# Phase 17 Plan 02 : Vocabulaire d'état et cause de l'échec — Summary

**`RefreshAsync` a cessé de détruire l'information : un 429 est désormais « attends », un `invalid_grant` est « reconnecte-toi », et les deux ne peuvent plus se confondre — sans qu'un seul octet du corps HTTP ne puisse sortir du résultat.**

## Performance

- **Duration:** 7 min
- **Started:** 2026-09-09T14:05:30Z
- **Completed:** 2026-09-09T14:12:31Z
- **Tasks:** 2/2
- **Files modified:** 6 (3 créés, 3 modifiés)

## Accomplishments

- **Le `catch { return null; }` unique qui couvrait à la fois l'envoi HTTP et le parsing est scindé.** Le nouveau `RefreshAsync` classe six modes d'échec selon une seule question : « l'utilisateur doit-il faire quelque chose, ou juste attendre ? ». Le cas qui comptait le plus est le **429 `rate_limit_error`** — mode d'échec réel et vérifié du point de terminaison de jeton : il est classé `EchecTemporaire`, jamais « déconnecté ». Crier « reconnecte-toi » sur un rate-limit aurait été une fausse alerte sur un compte parfaitement sain, et aurait détruit la confiance dans le signal que la phase 17 construit.
- **Le cas piégeux est traité : `200` + corps inexploitable ⇒ `IdentifiantsRejetes`.** Le serveur a déjà roulé le refresh token de son côté ; l'ancien est mort, le nouveau est perdu. Réessayer est mécaniquement vain. Le `catch` le prend aussi en compte a posteriori (`resp is { IsSuccessStatusCode: true }`) : une coupure **pendant** la lecture du flux d'une réponse 200 est classée comme un rejet, pas comme un échec temporaire.
- **`ResultatRafraichissement` ne peut transporter aucun texte** — et ce n'est pas une convention, c'est un **test réflexif** : `GetProperties()` ne doit contenir aucun `string` et compter exactement 2 membres. Ajouter un champ `Message` casse la suite. C'est la parade structurelle au risque qu'un corps d'erreur échoïse la requête, donc le refresh token.
- **Le vocabulaire d'état est posé et neutre.** `EtatAuthentification` a **quatre** valeurs, pas trois : `HorsLigne` et `Deconnecte` sont irréductibles l'un à l'autre. `IAuthStatus` copie le motif déjà en production (`RefreshOrchestrator.SnapshotChanged`) : le service expose l'événement, l'abonné marshalle. `ServicesLayerPurityTests` est vert **sans une ligne modifiée dans sa liste d'exceptions**.
- **`ExpiresAt` est devenu vérifiable.** L'horloge est injectée par un paramètre optionnel en dernière position : zéro site d'appel cassé, et l'`Assert.InRange(50 min, 70 min)` approximatif de la Wave 0 est remplacé par une égalité exacte.
- **443 → 445 tests, 0 échec.** Aucun test supprimé : 1 réécrit (+1 cas), 1 ajouté.

## Task Commits

1. **Task 1 : Vocabulaire d'état d'authentification** — `561e869` (feat)
2. **Task 2 : `RefreshAsync` rend sa CAUSE** — `98236fd` (feat, TDD)

**Plan metadata:** voir le commit `docs(17-02)` final.

## Files Created/Modified

- `src/Chronos/Services/EtatAuthentification.cs` *(créé)* — enum à 4 valeurs, XML-doc qui explique **pourquoi** quatre et pas trois.
- `src/Chronos/Services/IAuthStatus.cs` *(créé)* — `Etat` + `event EventHandler<EtatAuthentification>? EtatChange` + `ReinitialiserApresLogin()`. Aucun `using System.Windows.*`.
- `tests/Chronos.Tests/Fakes/FakeAuthStatus.cs` *(créé)* — **public** (comme `FakeOAuthLogin`), `Declencher(...)` pour provoquer une transition, `ReinitCount` pour prouver TOK-03 au plan 17-05.
- `src/Chronos/Services/ChronosOAuthClient.cs` *(+109/−…)* — `IssueRafraichissement`, `ResultatRafraichissement`, ctor à horloge injectable, `RefreshAsync` réécrit, `LireJetonsAsync` extrait, `using System.IO`.
- `src/Chronos/Services/ChronosOAuthUsageProvider.cs` *(4 lignes)* — l'adaptation minimale prévue (`.Jetons`), assortie du commentaire qui annonce sa suppression au plan 17-04.
- `tests/Chronos.Tests/ChronosOAuthClientTests.cs` *(11 → 12 méthodes)* — `Toutes_les_causes_d_echec_se_confondent_en_un_seul_null` **réécrit** en `Chaque_cause_d_echec_est_desormais_distinguee` (6 cas), + `Le_resultat_de_rafraichissement_ne_peut_transporter_aucun_texte`.

## Table de classification livrée

| Signal | Issue | Pourquoi |
|---|---|---|
| `400 {"error":"invalid_grant"}` | `IdentifiantsRejetes` | RFC 6749 §5.2 — seule une reconnexion répare |
| `401 {"error":"invalid_client"}` | `IdentifiantsRejetes` | Identifiants refusés |
| `429 {"error":{"type":"rate_limit_error"}}` | **`EchecTemporaire`** | Mode d'échec réel et vérifié — le classer « déconnecté » serait une fausse alerte |
| `500` | `EchecTemporaire` | Panne serveur, pas panne de compte |
| `200` à corps tronqué | `IdentifiantsRejetes` | Rotation déjà faite côté serveur, nouveau jeton perdu — réessayer est vain |
| `200` sans `refresh_token` | `IdentifiantsRejetes` | Même raisonnement |
| `HttpRequestException` | `EchecTemporaire` | Wifi coupé — le jeton est peut-être parfaitement bon |
| `TaskCanceledException` (timeout 15 s) | `EchecTemporaire` | Idem |

## Decisions Made

- **`requirements-completed` laissé VIDE**, comme au plan 17-01. Le frontmatter du plan déclare `TOK-01, TOK-02`, mais ce plan **ne livre ni l'un ni l'autre** : il ne crée aucune autorité, aucun service de fond, aucune pastille. Il rend le vocabulaire *disponible*. Les cocher ici marquerait la traçabilité « Done » alors que 17-03 → 17-05 doivent encore livrer. `REQUIREMENTS.md` reste à `Pending`.
- **Le nom `Toutes_les_causes_d_echec_se_confondent_en_un_seul_null` est retiré même des commentaires.** La traçabilité avant/après passe par le hash `cef82e1` cité dans la XML-doc du test réécrit — pas par la répétition d'un identifiant qui n'existe plus (voir Déviation 1).
- **Le `catch` conserve un `Exception ex` filtré nominativement** (`HttpRequestException`, `TaskCanceledException`, `OperationCanceledException`, `JsonException`, `IOException`) plutôt qu'un `catch` nu : une exception hors de cette liste (bug de programmation) doit remonter, pas être silencieusement classée « temporaire ».
- **`PostTokenAsync` garde son `catch { return null; }`.** L'échange de code n'a pas de cause à distinguer : `Views/OAuthLogin.cs` affiche une erreur générique. Une seule occurrence subsiste dans le fichier, comme avant.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Critères d'acceptation contradictoires] La XML-doc prescrite par le plan viole le grep prescrit par le plan**

- **Found during:** Task 2
- **Issue:** L'action du plan dictait d'écrire, dans la XML-doc du test réécrit, « remplace `Toutes_les_causes_d_echec_se_confondent_en_un_seul_null` » ; son critère d'acceptation exigeait `grep -n "Toutes_les_causes_d_echec_se_confondent_en_un_seul_null" tests/Chronos.Tests/` → **aucun résultat**. Les deux ne pouvaient pas être vrais ensemble. Même classe de contradiction qu'au plan 17-01 (helper `new(...)` vs `grep "new ChronosOAuthUsageProvider("`).
- **Fix:** Le critère `grep` l'emporte — son intention est qu'un futur `grep` du nom disparu ne laisse croire à personne que le test existe encore. La XML-doc dit désormais « RÉÉCRITURE du `[Theory]` de la Wave 0 (plan 17-01, commit `cef82e1`) », ce qui est une traçabilité *plus* forte : un hash mène à la version exacte du test d'avant, un nom n'y menait pas.
- **Files modified:** `tests/Chronos.Tests/ChronosOAuthClientTests.cs`
- **Committed in:** `98236fd`

**2. [Rule 3 — Critère d'acceptation contradictoire] Le `<see cref>` du faux fait mentir `grep -c → 1`**

- **Found during:** Task 1
- **Issue:** Le code du plan pour `FakeAuthStatus` contenait `ReinitialiserApresLogin` **deux** fois (un `<see cref="…"/>` dans la doc de `ReinitCount`, plus la méthode), alors que le critère exigeait `grep -c "ReinitialiserApresLogin" … → 1` dans chaque fichier.
- **Fix:** La doc de `ReinitCount` dit « Nombre de réarmements demandés après un login réussi » — même information, sans l'identifiant. Le critère et l'intention sont tous deux satisfaits.
- **Files modified:** `tests/Chronos.Tests/Fakes/FakeAuthStatus.cs`
- **Committed in:** `561e869`

**3. [Rule 3 — Blocage de compilation] `IOException` non résolu**

- **Found during:** Task 2
- **Issue:** `error CS0103: Le nom 'IOException' n'existe pas dans le contexte actuel`. `ChronosOAuthClient.cs` déclare ses `using` explicitement en tête et `System.IO` n'y figurait pas.
- **Fix:** `using System.IO;` ajouté — le plan l'avait explicitement anticipé (« Ajouter `using System.IO;` si `IOException` n'est pas encore résolu »).
- **Committed in:** `98236fd`

**4. [Rule 1 — Bug d'outillage] BOM UTF-8 injecté dans 3 fichiers**

- **Found during:** Task 2, à la relecture du `git diff`
- **Issue:** Le script d'édition écrivait en `utf-8-sig`, ajoutant un BOM absent de l'état d'origine. Conséquence visible : `git diff --stat` du provider annonçait **6 lignes changées** au lieu de 4, en violation du critère « ≤ 4 lignes changées » — le diff contenait une modification parasite de la première ligne du fichier.
- **Fix:** BOM retiré des 3 fichiers touchés ; rebuild + suite complète relancés après coup (445/445 verts). Le diff du provider est retombé à **4 lignes (3 insertions, 1 suppression)**, conforme.
- **Files modified:** `ChronosOAuthClient.cs`, `ChronosOAuthUsageProvider.cs`, `ChronosOAuthClientTests.cs`
- **Committed in:** `98236fd`

**5. [Rule 3 — Blocage outillage] Heredoc `bash` impossible sur le script d'édition**

- **Found during:** Task 2
- **Issue:** `cat <<'EOF'` échouait (`unexpected EOF while looking for matching quote`) sur un script Python mêlant chaînes triples et texte français accentué — exactement le même symptôme qu'au plan 17-01.
- **Fix:** Script écrit avec l'outil `Write` puis exécuté. Aucun impact sur le livrable.

---

**Total deviations:** 5 auto-fixed (1× Rule 1, 4× Rule 3). Aucune n'a modifié le périmètre.

## Écarts de critères d'acceptation assumés

- **Verification 6 (« aucun jeton en clair »)** : une occurrence subsiste, `DiagnosticService.cs:220`. Elle écrit le **nom** du champ (`claudeAiOauth.accessToken`) suivi de « PRÉSENT »/« absent », **jamais la valeur**. Pré-existante, non touchée, déjà relevée au plan 17-01. Hors périmètre.
- **`git diff --stat` du fichier de test : 96 lignes changées.** Le critère du plan ne portait que sur le provider (≤ 4). Le volume côté test est la réécriture attendue ; la garantie qui compte — **aucun test supprimé** — est vérifiée nominativement : 11 → 12 méthodes, un seul nom disparu (celui du test réécrit), deux noms apparus.

## Issues Encountered

Aucune surprise de comportement. La phase RED a produit exactement les 8 erreurs de compilation attendues (types inexistants), et les 19 tests du fichier sont passés au premier lancement après implémentation — y compris le cas le plus incertain : `HttpClient` relaie bien la `TaskCanceledException` levée par le faux handler sans la muer en `HttpRequestException`, ce qui laisse le filtre nominatif du `catch` opérant.

## Vérification finale

| Contrôle | Résultat |
|---|---|
| `dotnet build Chronos.sln -v q --nologo` | **0 erreur** (2 avertissements xUnit2031 pré-existants, hors périmètre) |
| `dotnet test Chronos.sln -v q --nologo` | **445 réussis / 0 échec** (baseline 443, cible ≥ 441) |
| `ChronosOAuthClientTests` | 19 tests, 0 échec (cible ≥ 16) |
| `ChronosOAuthUsageProviderTests` | 13 tests, 0 échec — comportement observable inchangé |
| `ServicesLayerPurityTests` + `CompositionRootTests` | 5/5 verts, `git diff` de la garde **vide** |
| `git diff --name-only -- src/Chronos/Views/` | **vide** (login intact) |
| `git diff -- **/*.csproj` | **vide** (aucun paquet ajouté) |
| `grep "Task<OAuthTokens?> ExchangeCodeAsync"` | 1 — signature préservée |
| `grep "catch { return null; }"` | 1 — celui de l'échange de code, comme avant |
| `grep "ex.Message\|ReadAsStringAsync"` dans le client | **aucun résultat** |
| Coffre réel `%APPDATA%\Chronos\oauth.dat` | **518 o, mtime 1783863147 — identique avant/après** |
| Réseau réel joint | aucun — 100 % `FakeHttpMessageHandler` |

## Known Stubs

Aucun stub au sens « donnée factice câblée à un rendu ». En revanche, **deux contrats sont volontairement sans implémentation à ce commit** :

| Contrat | Implémenté par | Pourquoi c'est intentionnel |
|---|---|---|
| `IAuthStatus` | `ChronosTokenAuthority`, plan **17-03** | Le plan 17-02 est explicitement décrit comme « ne crée AUCUNE autorité, AUCUN service de fond, AUCUNE pastille ». Poser le contrat séparément permet à 17-03 (autorité) et 17-05 (ViewModel) d'avancer sans négocier de vocabulaire. |
| `EtatAuthentification` | Consommé par le `MainViewModel`, plan **17-05** | Idem — l'enum n'est encore lu par personne. |

Aucun des deux ne bloque l'objectif de *ce* plan : son objectif était précisément de rendre le vocabulaire disponible.

## Next Phase Readiness

- **17-03 (autorité unique de jeton)** a tout ce qu'il lui faut : l'enum d'état, le contrat `IAuthStatus` à implémenter, et un `RefreshAsync` dont la sortie se mappe **directement** sur la table de décision (`IdentifiantsRejetes` → `Deconnecte`, `EchecTemporaire` → `HorsLigne`, `Succes` → `Connecte`). Aucun parsing à refaire, aucune cause à reconstituer.
- **17-04 (câblage)** trouvera son adaptation minimale déjà signalée en commentaire dans `ChronosOAuthUsageProvider.cs` — le bloc à supprimer est nommé et daté.
- **17-05 (pastille)** dispose de `FakeAuthStatus`, public, prêt pour les 5 sites `new MainViewModel(...)`.

**Rappel du plan 17-01 toujours valable pour 17-03/17-04 :** le recul anti-429 actuel est inopérant sans cache (`_cached` vit en RAM et est vide à chaque démarrage de l'exe). L'autorité doit porter **sa propre** politique de backoff, indépendante de tout cache d'usage — sans quoi le symptôme survivra à la refonte.

**Aucun blocage.** `TOK-01`, `TOK-02`, `TOK-03` restent à `Pending` dans `REQUIREMENTS.md` : c'est l'état honnête.

---
*Phase: 17-jeton-toujours-vivant-panne-toujours-visible*
*Completed: 2026-09-09*

## Self-Check: PASSED

- `src/Chronos/Services/EtatAuthentification.cs` — présent
- `src/Chronos/Services/IAuthStatus.cs` — présent
- `tests/Chronos.Tests/Fakes/FakeAuthStatus.cs` — présent
- `src/Chronos/Services/ChronosOAuthClient.cs` — présent (modifié)
- `src/Chronos/Services/ChronosOAuthUsageProvider.cs` — présent (4 lignes changées)
- `tests/Chronos.Tests/ChronosOAuthClientTests.cs` — présent (11 → 12 méthodes de test, 0 supprimée)
- Commits `561e869` et `98236fd` — présents dans l'historique
