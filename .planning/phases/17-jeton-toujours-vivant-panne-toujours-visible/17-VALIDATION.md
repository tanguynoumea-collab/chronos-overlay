---
phase: 17
slug: jeton-toujours-vivant-panne-toujours-visible
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-09-09
---

# Phase 17 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (net8.0-windows) — `tests/Chronos.Tests` |
| **Quick run command** | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~Auth\|FullyQualifiedName~OAuth\|FullyQualifiedName~Token"` |
| **Full suite command** | `dotnet test Chronos.sln -v q --nologo` |
| **Estimated runtime** | ~37 s · baseline mesurée : **419 tests / 0 échec** |

## Sampling Rate

- Après chaque commit de tâche : commande rapide
- Après chaque vague : suite complète
- Avant vérification de phase : suite complète verte

## Contrainte de sécurité qui prime sur toute vérification

**Aucun test, aucune sonde, aucune vérification manuelle ne doit appeler l'endpoint de refresh avec le
refresh token réel de l'utilisateur** (`%APPDATA%\Chronos\oauth.dat`). Le flux OAuth fait tourner le refresh
token : un appel dont le résultat n'est pas re-sauvegardé invaliderait définitivement son login. Tout passe
par `FakeHttpMessageHandler`. Le coffre réel n'est ni lu, ni déchiffré, ni affiché.

Corollaire : **aucun test ne doit écrire dans le vrai `%APPDATA%\Chronos\`** — chemins injectés depuis
`Path.GetTempPath()`.

## Per-Task Verification Map

*Rempli par le planner.*

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| — | — | — | — | — | — | — | ⬜ pending |

## Gardes permanentes

| Garde | Commande | Attendu |
|-------|----------|---------|
| Pureté Services | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~ServicesLayerPurityTests"` | vert (l'état d'auth reste neutre, la pastille vit dans le VM) |
| Composition DI | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~CompositionRootTests"` | vert (autorité de jeton + service de fond résolus) |
| Suite complète | `dotnet test Chronos.sln -v q --nologo` | 0 échec |
| Aucun jeton en clair | `grep -rniE "accessToken|refreshToken" src/Chronos --include=*.cs \| grep -iE "log|Console|WriteLine|Append"` | aucun résultat |

## Wave 0 Requirements

- [ ] `ChronosOAuthUsageProviderTests.cs` — **n'existe pas aujourd'hui** : le provider central de la phase
      n'a aucune couverture. À créer avant de le modifier.
- [ ] Tests de `ChronosOAuthClient.RefreshAsync` — non testé aujourd'hui ; son `catch { return null; }`
      détruit la cause de l'échec et doit devenir un résultat typé.
- [ ] Faux de transport couvrant les modes d'échec réels : 401, 429 `rate_limit_error` (mode d'échec
      PROUVÉ du refresh — ne doit PAS être classé « déconnecté »), timeout, `HttpRequestException` (hors ligne).

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Lisibilité de la pastille | TOK-02 | Rendu visuel sur fenêtre transparente | Lancer l'app dans les 3 thèmes × 2 modes de cadran ; la pastille doit être visible sans masquer les anneaux |
| Réparation en un clic | TOK-03 | Ouvre un navigateur et exige une vraie authentification | À faire par l'utilisateur, en fin de milestone |

## Validation Sign-Off

- [ ] Toutes les tâches ont une commande automatisée ou une dépendance Wave 0
- [ ] Aucun appel réseau réel avec le jeton de l'utilisateur, dans aucun test
