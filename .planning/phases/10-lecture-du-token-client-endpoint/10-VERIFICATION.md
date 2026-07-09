---
phase: 10-lecture-du-token-client-endpoint
verified: 2026-07-09T00:00:00Z
status: passed
score: 8/8 must-haves verified
---

# Phase 10 : Lecture du token client endpoint — Rapport de vérification

**Objectif de phase :** Service isolé produisant un `UsageSnapshot` Exact depuis `/api/oauth/usage` ou « indisponible » proprement, sans jamais logger/écrire/exposer le token.
**Vérifié :** 2026-07-09
**Statut :** passed
**Re-vérification :** Non — vérification initiale

## Goal Achievement

### Observable Truths

| # | Truth | Statut | Preuve |
|---|-------|--------|--------|
| 1 | Le lecteur déchiffre un blob v10 synthétique et retourne le champ `token` de l'entrée `claude_code` | ✓ VERIFIED | `ClaudeTokenReader.DecryptAndSelectToken` (lignes 92-132) ; tests `TOK01_Nominal_dechiffre_et_retourne_token_et_expiresAt`, `TOK01_Selection_ignore_les_entrees_sans_claude_code` verts |
| 2 | Fichier/clé absents, base64 invalide, blob court, tag GCM faux, map sans `claude_code`, entrée sans `token` → `null` SANS exception | ✓ VERIFIED | 7 tests `TOK02_*` verts, tous passent par le `catch (Exception)` global sans journalisation |
| 3 | Le lecteur ouvre les fichiers coffre en lecture seule et n'écrit aucun fichier | ✓ VERIFIED | `FileAccess.Read` explicite (lignes 52, 67) ; test `TOK03_TryReadAccessToken_n_ecrit_aucun_fichier` (snapshot répertoire avant/après identique) ; grep source vide |
| 4 | Le token en clair ne quitte la méthode que comme valeur de retour en mémoire | ✓ VERIFIED | Aucune trace de log/écriture/concat ; seule sortie = `return t.GetString()` |
| 5 | Réponse OAuth valide → `UsageSnapshot` Exact (utilization/100, resets_at ISO, Reliability=Exact, FractionTimeRemaining) | ✓ VERIFIED | `ClaudeOAuthUsageProvider.Read` (lignes 89-109) ; test `Reponse_valide_mappe_les_deux_fenetres_en_Exact` vert ; **confirmé en direct sur cette machine** (sonde E2E, voir Spot-Checks) : `FiveHour.Reliability=Exact 74%`, `SevenDay.Reliability=Exact 93%` |
| 6 | 401/403, timeout, erreur réseau, JSON malformé → `UsageSnapshot.Empty`, jamais d'exception | ✓ VERIFIED | 6 tests `API-02` verts (401/403/500, `HttpRequestException`, `TaskCanceledException`, JSON malformé) |
| 7 | Token null OU `expiresAt < now` → aucun appel HTTP émis | ✓ VERIFIED | Tests `Token_null_est_inerte_aucun_appel_reseau` et `Token_expire_court_circuite_aucun_appel_reseau` assertent `SendCount == 0` ; garde-fou `Token_non_expire_declenche_bien_l_appel` prouve que le court-circuit ne bloque pas les cas valides |
| 8 | Appel asynchrone, respecte le `CancellationToken`, timeout court (5 s) sans bloquer | ✓ VERIFIED | `CancellationTokenSource.CreateLinkedTokenSource(ct)` + `CancelAfter(TimeSpan.FromSeconds(5))` ; test `Annulation_ne_crashe_pas_et_renvoie_Empty` vert |

**Score :** 8/8 truths vérifiées

### Required Artifacts

| Artifact | Attendu | Statut | Détails |
|----------|---------|--------|---------|
| `src/Chronos/Services/IClaudeTokenReader.cs` | Contrat neutre `TryReadAccessToken` | ✓ VERIFIED | Interface présente, commentée, neutre (aucun type WPF) |
| `src/Chronos/Services/ClaudeTokenReader.cs` | Déchiffrement DPAPI + AES-256-GCM v10, lecture seule | ✓ VERIFIED | 133 lignes, `DecryptAndSelectToken` présent, `FileAccess.Read` strict |
| `src/Chronos/Chronos.csproj` | Dépendance `System.Security.Cryptography.ProtectedData` 8.0.0 | ✓ VERIFIED | `<PackageReference Include="System.Security.Cryptography.ProtectedData" Version="8.0.0" />` ligne 31 |
| `tests/Chronos.Tests/Fakes/V10TestVault.cs` | Fabrique blob v10 chiffré par clé de test connue | ✓ VERIFIED | `TestKey` = octets 0..31 (aucun secret réel), `MakeTokenCacheB64` conforme au schéma RESEARCH |
| `tests/Chronos.Tests/ClaudeTokenReaderTests.cs` | Tests TOK-01/02/03 | ✓ VERIFIED | 11 tests, tous verts |
| `src/Chronos/Services/ClaudeOAuthUsageProvider.cs` | Client GET `/api/oauth/usage` → mapping Exact | ✓ VERIFIED | 111 lignes, `class ClaudeOAuthUsageProvider : IUsageProvider` |
| `tests/Chronos.Tests/Fakes/FakeHttpMessageHandler.cs` | Handler HTTP scripté + compteur | ✓ VERIFIED | `SendCount`, `LastRequest`, `Json`, `Throws` présents |
| `tests/Chronos.Tests/Fakes/FakeClaudeTokenReader.cs` | `IClaudeTokenReader` factice injectable | ✓ VERIFIED | Implémente le contrat, `ReadCount` présent |
| `tests/Chronos.Tests/ClaudeOAuthUsageProviderTests.cs` | Tests API-01/02/03 | ✓ VERIFIED | 13 tests, tous verts |

### Key Link Verification

| From | To | Via | Statut | Détails |
|------|-----|-----|--------|---------|
| `ClaudeTokenReader.cs` | `ProtectedData.Unprotect` | `encrypted_key[5..]`, `DataProtectionScope.CurrentUser` | ✓ WIRED | ligne 63 |
| `ClaudeTokenReader.cs` | `AesGcm.Decrypt` | nonce/tag/cipher découpés du blob v10 | ✓ WIRED | ligne 103-104 |
| `ClaudeTokenReader.cs` | entrée `claude_code`, champ `token` | `EnumerateObject` + `entry.Name.Contains("claude_code")` | ✓ WIRED | lignes 110-122 |
| `ClaudeOAuthUsageProvider.cs` | `IClaudeTokenReader.TryReadAccessToken` | token placé uniquement dans l'en-tête `Authorization` | ✓ WIRED | ligne 46, 58 |
| `ClaudeOAuthUsageProvider.cs` | `https://api.anthropic.com/api/oauth/usage` | `HttpClient.SendAsync` + `anthropic-beta: oauth-2025-04-20` | ✓ WIRED | lignes 29, 57-63 |
| `ClaudeOAuthUsageProvider.cs` | `WindowState` (Exact) | `utilization/100` → `Utilization`, `resets_at` ISO → `ResetsAt`, `Reliability=Exact` | ✓ WIRED | lignes 94-107 |

### Data-Flow Trace (Level 4)

| Artifact | Variable de donnée | Source | Données réelles | Statut |
|----------|--------------------|--------|------------------|--------|
| `ClaudeOAuthUsageProvider.GetAsync` | `snap.FiveHour` / `snap.SevenDay` | Réponse HTTP réelle `GET /api/oauth/usage` parsée via `JsonDocument` | Oui — sonde E2E réelle sur cette machine (voir Spot-Checks) : `74%` / `93%`, `Reliability=Exact`, `ResetsAtPresent=True` | ✓ FLOWING |
| `ClaudeTokenReader.TryReadAccessToken` | token retourné | Déchiffrement réel du coffre `%APPDATA%/Claude` (Local State + config.json) | Oui — le token réel a permis l'appel HTTP à obtenir une réponse `200 Exact` dans la sonde E2E (sans jamais être imprimé) | ✓ FLOWING |

### Behavioral Spot-Checks

| Comportement | Commande | Résultat | Statut |
|--------------|----------|----------|--------|
| `dotnet build` compile | `dotnet build Chronos.sln -c Debug` | 0 avertissement, 0 erreur | ✓ PASS |
| Suite de tests complète | `dotnet test Chronos.sln -c Debug` | 178/178 réussis (0 échec) | ✓ PASS |
| Pureté couche Services | `dotnet test --filter ServicesLayerPurityTests` | 1/1 réussi | ✓ PASS |
| **E2E réel** : `ClaudeTokenReader.Default()` + `ClaudeOAuthUsageProvider` réels sur cette machine (sonde temporaire hors dépôt, token jamais imprimé) | Programme console éphémère référençant `Chronos.csproj`, appel `GetAsync()` réel | `FiveHour.Reliability=Exact Utilization=74% ResetsAtPresent=True` / `SevenDay.Reliability=Exact Utilization=93% ResetsAtPresent=True` | ✓ PASS |

Note sur la sonde E2E : exécutée dans un projet temporaire du répertoire scratchpad (hors dépôt, non commité), qui référence uniquement `Chronos.csproj` et n'imprime que `Reliability`/`Utilization` arrondi/`ResetsAtPresent` (booléen) — jamais le token. Les pourcentages diffèrent légèrement de ceux documentés dans le RESEARCH (65 %/92 %) car l'usage réel évolue avec le temps ; cela confirme que le pipeline complet (déchiffrement réel → appel réseau réel → mapping) fonctionne de bout en bout, avec des valeurs plausibles et `Reliability=Exact` sur les deux fenêtres.

### Requirements Coverage

| Requirement | Plan source | Description | Statut | Preuve |
|-------------|-------------|-------------|--------|--------|
| TOK-01 | 10-01 | Déchiffrement config.json/Local State → DPAPI → AES-256-GCM → champ token | ✓ SATISFIED | `ClaudeTokenReader.cs` + 2 tests nominal/sélection verts + sonde E2E réelle |
| TOK-02 | 10-01 | Tolérance totale → null sans exception | ✓ SATISFIED | 7 tests `TOK02_*` verts |
| TOK-03 | 10-01 | Token jamais logué/écrit/exposé, preuve lecture seule | ✓ SATISFIED | grep vide + test snapshot répertoire |
| API-01 | 10-02 | Client GET /api/oauth/usage → mapping Exact | ✓ SATISFIED | `ClaudeOAuthUsageProvider.cs` + tests mapping/en-têtes + sonde E2E réelle |
| API-02 | 10-02 | Erreurs (401/403/réseau/timeout/malformé) → indisponible sans crash | ✓ SATISFIED | 6 tests `API-02` verts |
| API-03 | 10-02 | Inerte hors ligne/token expiré, asynchrone, respecte CancellationToken | ✓ SATISFIED | tests inertie (SendCount==0) + annulation verts |

Aucune exigence orpheline détectée : les 6 IDs déclarés dans les frontmatters des plans (10-01, 10-02) correspondent exactement aux 6 IDs marqués `Phase 10 | Complete` dans `.planning/REQUIREMENTS.md`.

**Note documentaire (non bloquante) :** le texte descriptif de TOK-01/API-01 dans `.planning/REQUIREMENTS.md` (rédigé avant la recherche empirique) mentionne encore le champ `accessToken` et le schéma `rate_limits.*.used_percentage`/epoch secondes — ce schéma présumé s'est révélé FAUX lors du test décisif (10-RESEARCH.md) : le vrai endpoint renvoie `token` (pas `accessToken`) et `five_hour`/`seven_day` à la racine avec `utilization`/ISO 8601. L'implémentation suit correctement le schéma RÉEL prouvé, pas le texte obsolète de REQUIREMENTS.md. Aucun impact sur le code ; suggestion de mise à jour cosmétique de REQUIREMENTS.md pour une phase ultérieure.

### Anti-Patterns Found

Aucun. Grep `TODO|FIXME|XXX|HACK|PLACEHOLDER|placeholder|coming soon|not yet implemented` sur les 3 fichiers source de la phase : aucune correspondance. Les seuls `return null;` trouvés relèvent explicitement de la tolérance totale voulue (TOK-02), pas de stubs.

### Audit Sécurité (critique)

| Vérification | Commande | Résultat |
|---------------|----------|----------|
| Aucun log/écriture/console dans les 2 fichiers sensibles | `grep -rn "Console\|File.Write\|File.Append\|StreamWriter\|Log\|Debug.Write" src/Chronos/Services/ClaudeTokenReader.cs src/Chronos/Services/ClaudeOAuthUsageProvider.cs` | **Vide** — aucune correspondance |
| Lecture seule des fichiers coffre | `grep -n "FileAccess.Read" src/Chronos/Services/ClaudeTokenReader.cs` | 2 occurrences, `FileMode.Open` + `FileAccess.Read` + `FileShare.Read` strict, aucun `FileMode.Create/Append` |
| Token uniquement en en-tête Authorization | Inspection `ClaudeOAuthUsageProvider.cs` | `req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token)` ligne 58 ; `UsageUrl` est une constante (le token n'y apparaît jamais) ; aucune concaténation du token ailleurs |
| Aucun vrai secret commité | `grep -rn "sk-ant-oat"` / `grep -rn "Bearer <valeur 10+ car>"` / `grep -rn "accessToken.*=.*valeur 20+ car"` sur `src/` et `tests/` | **Vide** dans les trois cas — `V10TestVault.TestKey` est une clé de test déterministe (octets 0..31), aucune valeur ressemblant à un vrai token |
| Aucune exception journalisée avec détail | Inspection des `catch (Exception)` dans `ClaudeTokenReader.cs` et `catch (Exception ex) when (...)` dans `ClaudeOAuthUsageProvider.cs` | Aucun accès à `ex.Message`/`ex.ToString()` dans les deux fichiers ; retour direct `null`/`UsageSnapshot.Empty` |

**Conclusion audit sécurité : conforme.** Le token/clé n'est jamais logué, écrit sur disque, ni exposé en dehors de l'en-tête Authorization et de la valeur de retour mémoire.

### Human Verification Required

Aucune. Tous les critères de succès (déchiffrement tolérant, mapping Exact, tolérance totale aux erreurs, audit sécurité) sont vérifiables et vérifiés par grep/tests/inspection de code, complétés par une sonde E2E réelle exécutée sur cette machine (résultat plausible, `Reliability=Exact` confirmé, token jamais imprimé).

### Gaps Summary

Aucun gap. Les 8 vérités observables dérivées des `must_haves` des deux plans sont vérifiées ; les 9 artefacts existent, sont substantiels et câblés ; les 6 liens clés sont vérifiés ; l'audit sécurité est conforme ; `dotnet build` et `dotnet test` (178/178) passent ; la sonde E2E réelle confirme le fonctionnement de bout en bout avec des données plausibles (`74 %` 5h / `93 %` hebdo, `Reliability=Exact`).

---

*Vérifié le : 2026-07-09*
*Vérificateur : Claude (gsd-verifier)*
