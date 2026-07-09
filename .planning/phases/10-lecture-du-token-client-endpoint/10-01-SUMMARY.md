---
phase: 10-lecture-du-token-client-endpoint
plan: 01
subsystem: auth
tags: [dpapi, aes-gcm, safestorage, oauth, token, tdd, xunit]

# Dependency graph
requires:
  - phase: 03-providers (v1.0)
    provides: IUsageProvider + UsageSnapshot neutres, patterns de lecture tolérante
provides:
  - IClaudeTokenReader (contrat neutre, mémoire seule)
  - ClaudeTokenReader (déchiffrement DPAPI + AES-256-GCM v10 + sélection claude_code, lecture seule, tolérant)
  - V10TestVault (fixture de test : blob v10 chiffré par clé AES de test connue)
  - Dépendance System.Security.Cryptography.ProtectedData 8.0.0
affects: [10-02 (client OAuth qui consomme l'access token), 11 (intégration composite + toggle UI)]

# Tech tracking
tech-stack:
  added: [System.Security.Cryptography.ProtectedData 8.0.0]
  patterns:
    - "Cœur crypto isolé en internal static (DecryptAndSelectToken) testable via InternalsVisibleTo"
    - "Chemins de fichiers injectables au constructeur + fabrique Default() pour %APPDATA%/Claude"
    - "Tolérance totale par try/catch global → null, sans jamais journaliser l'exception (fragments sensibles)"

key-files:
  created:
    - src/Chronos/Services/IClaudeTokenReader.cs
    - src/Chronos/Services/ClaudeTokenReader.cs
    - tests/Chronos.Tests/Fakes/V10TestVault.cs
    - tests/Chronos.Tests/ClaudeTokenReaderTests.cs
  modified:
    - src/Chronos/Chronos.csproj

key-decisions:
  - "NuGet ProtectedData plutôt que P/Invoke CryptUnprotectData (moins de code interop, déjà win-x64)"
  - "Cœur déchiffrement exposé internal static pour tests crypto déterministes sans DPAPI ni vrai token"
  - "Preuve de non-écriture par test snapshot répertoire (liste + taille + timestamp) avant/après appel"

patterns-established:
  - "Fixture V10TestVault : fabrique un blob v10 chiffré par une clé de TEST — jamais le vrai coffre"
  - "Sécurité token : valeur de retour mémoire uniquement, aucun log/write/console (prouvé par grep + test)"

requirements-completed: [TOK-01, TOK-02, TOK-03]

# Metrics
duration: 3min
completed: 2026-07-09
---

# Phase 10 Plan 01: Lecture du token client Summary

**ClaudeTokenReader déchiffre le coffre safeStorage v10 de Claude (DPAPI → AES-256-GCM → MAP) et renvoie l'access token `claude_code` en mémoire seule, tolérant à toute erreur et prouvé en lecture seule.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-07-09T08:07:55Z
- **Completed:** 2026-07-09T08:11:14Z
- **Tasks:** 2
- **Files modified:** 5 (4 créés, 1 modifié)

## Accomplishments
- `IClaudeTokenReader` + `ClaudeTokenReader` : déchiffrement complet du schéma prouvé (Local State → clé DPAPI 32o ; config.json → blob v10 → AES-256-GCM → JSON MAP → 1re entrée `claude_code` → champ `token` + `expiresAt`).
- Tolérance TOTALE (TOK-02) : fichier/clé absents, base64 invalide, blob court, tag GCM faux, mauvaise clé, MAP sans `claude_code`, entrée sans `token`, plaintext non-JSON → tous → null, aucune exception.
- Sécurité (TOK-03) prouvée : aucun chemin d'écriture/log/console (grep vide), `FileAccess.Read` strict, token en valeur de retour mémoire uniquement, test snapshot répertoire avant/après = identique.
- Fixture `V10TestVault` : blobs v10 déterministes chiffrés par une clé AES de test connue — aucun secret réel touché.
- 11 tests TOK-01/02/03 verts ; suite complète 165/165 verte (154 existants + 11), pureté Services conservée.

## Task Commits

1. **Task 1: NuGet DPAPI + contrat IClaudeTokenReader + fixture V10TestVault** - `3b72847` (feat)
2. **Task 2 (RED): tests TOK-01/02/03 en échec + squelette** - `cce1b05` (test)
3. **Task 2 (GREEN): implémenter ClaudeTokenReader** - `2158fce` (feat)

**Plan metadata:** (docs commit final)

_TDD : Task 2 = RED (test) → GREEN (feat). Pas de refactor nécessaire._

## Files Created/Modified
- `src/Chronos/Services/IClaudeTokenReader.cs` - Contrat neutre `TryReadAccessToken(out DateTimeOffset?)` → `string?`
- `src/Chronos/Services/ClaudeTokenReader.cs` - Déchiffrement DPAPI + AES-256-GCM v10, sélection `claude_code`, lecture seule, tolérant
- `tests/Chronos.Tests/Fakes/V10TestVault.cs` - Fabrique de blob v10 chiffré par clé AES de test
- `tests/Chronos.Tests/ClaudeTokenReaderTests.cs` - 11 tests TOK-01/02/03
- `src/Chronos/Chronos.csproj` - Ajout PackageReference ProtectedData 8.0.0

## Decisions Made
- **NuGet ProtectedData vs P/Invoke** : NuGet retenu (moins de code interop, cible win-x64, prouvé fonctionnel en RESEARCH).
- **Cœur crypto internal static** : `DecryptAndSelectToken(byte[] aesKey, string tokenCacheB64, out DateTimeOffset?)` permet de tester la crypto sans DPAPI ni vrai coffre, via `InternalsVisibleTo("Chronos.Tests")` préexistant.
- **Preuve de non-écriture par test** : snapshot (chemin + taille + timestamp UTC) du répertoire coffre avant/après l'appel, en plus du grep source — double garantie TOK-03.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None. Le schéma étant déjà prouvé end-to-end en RESEARCH (10-RESEARCH.md), l'implémentation verbatim a fonctionné du premier coup (GREEN au premier run après RED).

## Known Stubs
None. Aucun stub : le reader est pleinement câblé sur le schéma prouvé. La fabrique `Default()` (chemins %APPDATA%/Claude) sera consommée par le client OAuth du Plan 02.

## User Setup Required
None - no external service configuration required. (Lecture seule d'un coffre déjà présent sur le poste ; DPAPI lié au compte Windows courant.)

## Next Phase Readiness
- Plan 02 (client `/api/oauth/usage`) peut consommer `ClaudeTokenReader.Default()` pour obtenir l'access token + `expiresAt` (court-circuit expiration avant l'appel réseau).
- Contrainte sécurité honorée : seul usage autorisé du token en Plan 02 = en-tête `Authorization: Bearer`.
- Aucun blocage : tous les échecs dégradent proprement vers « pas de token » (repli).

## Self-Check: PASSED

- 4 fichiers créés vérifiés présents sur disque.
- 3 commits de tâche vérifiés présents (`3b72847`, `cce1b05`, `2158fce`).

---
*Phase: 10-lecture-du-token-client-endpoint*
*Completed: 2026-07-09*
