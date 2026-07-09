---
phase: 10-lecture-du-token-client-endpoint
plan: 02
subsystem: api
tags: [oauth, httpclient, usagesnapshot, tolerant-parsing, cancellation, xunit, tdd]

# Dependency graph
requires:
  - phase: 10-01
    provides: IClaudeTokenReader (access token en mémoire + expiresAt)
  - phase: 03-providers (v1.0)
    provides: IUsageProvider + UsageSnapshot/WindowState neutres, mapping tolérant, FractionRemaining
provides:
  - ClaudeOAuthUsageProvider (IUsageProvider) — GET /api/oauth/usage → UsageSnapshot Exact, tolérant, inerte hors ligne/token expiré
  - FakeHttpMessageHandler (infra de test réseau : réponse scriptée ou exception + compteur d'envois)
  - FakeClaudeTokenReader (IClaudeTokenReader factice : token/expiration injectables + compteur)
affects: [11 (intégration composite : insertion OAuth en tête de priorité Exact + toggle UI)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "HttpClient injecté au constructeur (testable via HttpMessageHandler mocké, sans réseau réel)"
    - "Timeout court indépendant via CancellationTokenSource.CreateLinkedTokenSource(ct) + CancelAfter(5s)"
    - "Token en variable locale uniquement, placé exclusivement dans l'en-tête Authorization: Bearer"
    - "Court-circuit expiration (expiresAt < now → 0 appel) prouvé par SendCount == 0"

key-files:
  created:
    - src/Chronos/Services/ClaudeOAuthUsageProvider.cs
    - tests/Chronos.Tests/Fakes/FakeHttpMessageHandler.cs
    - tests/Chronos.Tests/Fakes/FakeClaudeTokenReader.cs
    - tests/Chronos.Tests/ClaudeOAuthUsageProviderTests.cs
  modified: []

key-decisions:
  - "Mapping OAuth dédié (utilization/100 + resets_at ISO 8601 via DateTimeOffset.Parse RoundtripKind) — PAS de partage avec le schéma statusLine (used_percentage/epoch)"
  - "HttpClient injecté plutôt qu'IHttpClientFactory : 1 appel ponctuel non concurrent, testabilité maximale"
  - "Timeout 5 s appliqué via CancellationTokenSource lié (indépendant du tick 1 s), pas via HttpClient.Timeout mutable partagé"

patterns-established:
  - "FakeHttpMessageHandler : handler scripté (statut+JSON ou exception) + SendCount → prouve mapping, erreurs ET inertie (0 appel) sans réseau"
  - "Sécurité token : seule sortie = en-tête Authorization ; aucun Console/Log/File.Write (prouvé par grep)"

requirements-completed: [API-01, API-02, API-03]

# Metrics
duration: 3min
completed: 2026-07-09
---

# Phase 10 Plan 02: Client endpoint OAuth Summary

**ClaudeOAuthUsageProvider appelle GET /api/oauth/usage avec le token déchiffré (Plan 01), mappe le schéma OAuth réel (five_hour/seven_day, utilization/100, resets_at ISO 8601) en UsageSnapshot Exact — tolérant à toute erreur (→ Empty), inerte si token null/expiré (0 appel réseau), prouvé par 13 tests via HttpMessageHandler mocké.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-07-09T08:13:35Z
- **Completed:** 2026-07-09T08:17:02Z
- **Tasks:** 2 (dont 1 TDD)
- **Files modified:** 4 créés

## Accomplishments
- `ClaudeOAuthUsageProvider : IUsageProvider` — client HTTP + mapping du schéma OAuth RÉEL prouvé en RESEARCH : `five_hour`/`seven_day` à la racine, `utilization` 0..100 → `/100`, `resets_at` ISO 8601 → `DateTimeOffset.Parse(RoundtripKind)`, `Reliability = Exact`, `FractionTimeRemaining` calculé. Fenêtre absente → `Unavailable` (jamais de valeur inventée).
- Tolérance TOTALE (API-02) : 401/403/500, réseau (`HttpRequestException`), timeout/annulation (`TaskCanceledException`/`OperationCanceledException`), JSON malformé (`JsonException`) → `UsageSnapshot.Empty`, jamais d'exception non gérée.
- Inertie (API-03) prouvée : token null OU `expiresAt < now` → `SendCount == 0` (aucun appel réseau) ; annulation (`CancellationToken(canceled)`) → pas de crash ; timeout court 5 s via `CancellationTokenSource` lié (indépendant du tick 1 s).
- Sécurité honorée : le token ne vit qu'en variable locale, placé UNIQUEMENT dans l'en-tête `Authorization: Bearer` ; URL constante ; aucun `Console`/`Log`/`File.Write` (grep vide). En-têtes `anthropic-beta: oauth-2025-04-20` émis et asserté par test.
- Infra de test réseau réutilisable : `FakeHttpMessageHandler` (statut+JSON ou exception + `SendCount`/`LastRequest`) et `FakeClaudeTokenReader` (token/expiration injectables).
- 13 tests API-01/02/03 verts ; suite complète 178/178 verte (165 existants + 13), pureté Services conservée.

## Task Commits

1. **Task 1: infra de test réseau (FakeHttpMessageHandler + FakeClaudeTokenReader)** - `d13caa2` (feat)
2. **Task 2 (RED): tests API-01/02/03 en échec** - `d5decc4` (test)
3. **Task 2 (GREEN): implémenter ClaudeOAuthUsageProvider** - `21a9e67` (feat)

**Plan metadata:** (docs commit final)

_TDD : Task 2 = RED (test, ne compile pas) → GREEN (feat). Pas de refactor nécessaire._

## Files Created/Modified
- `src/Chronos/Services/ClaudeOAuthUsageProvider.cs` - Client GET /api/oauth/usage + mapping OAuth → UsageSnapshot Exact, tolérant, inerte hors ligne/token expiré
- `tests/Chronos.Tests/Fakes/FakeHttpMessageHandler.cs` - Handler HTTP scripté (statut+JSON ou exception) + compteur d'envois
- `tests/Chronos.Tests/Fakes/FakeClaudeTokenReader.cs` - IClaudeTokenReader factice (token/expiration injectables + compteur)
- `tests/Chronos.Tests/ClaudeOAuthUsageProviderTests.cs` - 13 tests API-01/02/03 (mapping, en-têtes, erreurs, inertie, annulation)

## Decisions Made
- **Mapping OAuth dédié (pas de partage avec statusLine)** : le schéma OAuth (`utilization` 0..100 racine + `resets_at` ISO 8601) diffère de `ClaudeUsageObjectProvider` (`used_percentage` + epoch secondes) → helper `Read` propre à ce provider, `DateTimeOffset.Parse(RoundtripKind)` (pas `FromUnixTimeSeconds`).
- **HttpClient injecté au constructeur** : 1 appel ponctuel non concurrent → un `HttpClient` injecté suffit (testable via `FakeHttpMessageHandler`), pas d'`IHttpClientFactory` (overkill). La composition Phase 11 fournira un HttpClient réel.
- **Timeout via CancellationTokenSource lié** : `CreateLinkedTokenSource(ct)` + `CancelAfter(5s)` → timeout court indépendant, respecte l'annulation de l'appelant sans muter un `HttpClient.Timeout` partagé.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Ajout de `using System.IO;` pour `IOException`**
- **Found during:** Task 2 (GREEN)
- **Issue:** La clause `catch ... when (... or IOException)` ne compilait pas (CS0103 : `IOException` introuvable) — directive `using` manquante.
- **Fix:** Ajout de `using System.IO;` en tête de `ClaudeOAuthUsageProvider.cs`.
- **Files modified:** src/Chronos/Services/ClaudeOAuthUsageProvider.cs
- **Verification:** `dotnet test --filter OAuth` → 13/13 verts après le fix.
- **Committed in:** `21a9e67` (commit GREEN de la Tâche 2)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Correction triviale de directive `using`, indispensable à la compilation. Aucun écart de comportement ni de périmètre.

## Issues Encountered
None. Le schéma OAuth étant prouvé end-to-end en RESEARCH, l'implémentation verbatim a produit des tests verts au premier run après RED (hors l'ajout de `using System.IO`).

## Known Stubs
None. Le provider est pleinement câblé sur le schéma prouvé. Une remarque de fidélité : l'en-tête `Content-Type: application/json` est ajouté via `TryAddWithoutValidation` sur les en-têtes de requête ; comme la requête GET n'a pas de corps, .NET le traite comme en-tête de contenu et ne l'attache pas physiquement (no-op silencieux). Sans impact : le test décisif RESEARCH a renvoyé 200 avec `Authorization` + `anthropic-beta`, qui sont bien émis et assertés. Non bloquant pour Phase 11.

## User Setup Required
None - no external service configuration required. (Le token est lu localement via le Plan 01 ; seul appel réseau = api.anthropic.com, déclenché à la demande.)

## Next Phase Readiness
- Phase 11 (intégration composite) peut insérer `ClaudeOAuthUsageProvider` en tête de priorité `Exact` du `CompositeUsageProvider` (devant le pont statusLine et le repli JSONL) et ajouter le toggle « Usage exact (OAuth) » au menu contextuel.
- Composition Phase 11 : fournir un `HttpClient` réel + `ClaudeTokenReader.Default()` (Plan 01) + `IClock` existant.
- Aucun blocage : tout échec (401/réseau/timeout/malformé/token absent ou expiré) dégrade proprement vers `UsageSnapshot.Empty` → bascule repli, jamais de crash ni de vol du tick 1 s.

## Self-Check: PASSED

- 4 fichiers créés vérifiés présents sur disque.
- 3 commits de tâche vérifiés présents (`d13caa2`, `d5decc4`, `21a9e67`).

---
*Phase: 10-lecture-du-token-client-endpoint*
*Completed: 2026-07-09*
