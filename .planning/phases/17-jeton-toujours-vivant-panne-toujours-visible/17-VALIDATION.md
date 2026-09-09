---
phase: 17
slug: jeton-toujours-vivant-panne-toujours-visible
status: planned
nyquist_compliant: false
wave_0_planned: true
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

*Rempli par le planner le 2026-09-09. `F` = fichier de test ; ✅ existe déjà, 🆕 créé par la tâche.*
Commande abrégée `T~X` = `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~X"`.

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 17-01 T1 | 17-01 | 1 | TOK-01, TOK-02 (Wave 0) | unitaire HTTP faux | `T~ChronosOAuthClientTests` | ✅ étendu | ⬜ pending |
| 17-01 T2 | 17-01 | 1 | TOK-01, TOK-02 (Wave 0) | unitaire HTTP faux + coffre temp | `T~ChronosOAuthUsageProviderTests` | 🆕 créé | ⬜ pending |
| 17-02 T1 | 17-02 | 2 | TOK-02 | garde réflexive de pureté | `T~ServicesLayerPurityTests` | ✅ | ⬜ pending |
| 17-02 T2 | 17-02 | 2 | TOK-02 | `[Theory]` de classification | `T~ChronosOAuthClientTests` | ✅ réécrit | ⬜ pending |
| 17-03 T1 | 17-03 | 3 | TOK-01, TOK-02 | unitaire pur + concurrence + coffre temp | `T~ChronosTokenAuthorityTests` | 🆕 créé | ⬜ pending |
| 17-03 T2 | 17-03 | 3 | TOK-01 | `IHostedService` déterministe | `T~TokenRefreshServiceTests` | 🆕 créé | ⬜ pending |
| 17-04 T1 | 17-04 | 4 | TOK-01, TOK-02 | unitaire (rejeu 401, états) | `T~ChronosOAuthUsageProviderTests` | ✅ réécrit | ⬜ pending |
| 17-04 T2 | 17-04 | 4 | TOK-01, TOK-02 | garde DI réelle | `T~CompositionRootTests` | ✅ étendu | ⬜ pending |
| 17-04 T3 | 17-04 | 4 | TOK-02 | unitaire (chaîne de rapport) | `T~DiagnosticServiceTests` | ✅ étendu | ⬜ pending |
| 17-05 T1 | 17-05 | 5 | TOK-02, TOK-03 | unitaire VM (`[Fact]`, pas STA) | `T~MainViewModelTests` | ✅ étendu | ⬜ pending |
| 17-05 T2 | 17-05 | 5 | TOK-02 | smoke `[WpfFact]` + thèmes | `T~CadranBindingTests` / `T~ThemingTests` | ✅ étendu | ⬜ pending |
| 17-05 T3 | 17-05 | 5 | TOK-02, TOK-03 | **MANUEL** (rendu + login réel) | — (prérequis : suite complète verte) | — | ⬜ pending |

### Couverture des exigences par plan

| Requirement | Plans | Preuve décisive |
|---|---|---|
| **TOK-01** | 17-01, 17-02, 17-03, 17-04 | `TokenRefreshServiceTests.Un_jeton_deja_expire_est_rafraichi_des_le_demarrage_sans_attendre_le_tick` + `ChronosTokenAuthorityTests.Dix_demandes_concurrentes_ne_declenchent_QU_UNE_rotation` |
| **TOK-02** | 17-01, 17-02, 17-03, 17-04, 17-05 | `ChronosOAuthClientTests.Chaque_cause_d_echec_est_desormais_distinguee` + `ChronosOAuthUsageProviderTests.Un_401_persistant_declenche_UN_seul_rejeu_puis_declare_Deconnecte` + `MainViewModelTests.L_etat_Deconnecte_franchit_la_frontiere_de_thread_UNE_fois_et_allume_la_pastille` |
| **TOK-03** | 17-05 | `MainViewModelTests.ReconnecterCommand_relance_le_login_et_ne_deconnecte_JAMAIS` (`LogoutCount == 0`, `ReinitCount == 1`) + vérification humaine 17-05 T3 |

### Attendu de couverture par vague

| Après le plan | Total attendu | Delta | Règle |
|---|---|---|---|
| baseline | 419 | — | mesuré le 2026-09-09 |
| 17-01 | ≥ 438 | +19 | Wave 0, aucun test supprimé |
| 17-02 | ≥ 441 | +3 | réécritures ≠ suppressions |
| 17-03 | ≥ 465 | +24 | autorité + service de fond |
| 17-04 | ≥ 473 | +8 | rejeu 401, garde DI, diagnostic |
| 17-05 | ≥ 483 | +10 | VM + pastille |

**Critère de phase : 0 échec ET total ≥ 419.** Contrairement à la phase 16 (où le recul était le
livrable), cette phase ne supprime AUCUN code : toute disparition de test est une régression à
justifier nominativement.

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

- [x] Toutes les tâches ont une commande automatisée ou une dépendance Wave 0
      (seule 17-05 T3 est manuelle, par nature : rendu visuel + authentification navigateur)
- [x] Wave 0 (plan 17-01) posée AVANT toute modification de `ChronosOAuthUsageProvider` (plan 17-04)
      et de `RefreshAsync` (plan 17-02)
- [ ] Aucun appel réseau réel avec le jeton de l'utilisateur, dans aucun test
- [ ] Suite complète verte sur **deux** exécutions (flakiness BAML constatée en phase 16)
- [ ] `%APPDATA%\Chronos\oauth.dat` a la même taille et le même horodatage après la suite qu'avant
