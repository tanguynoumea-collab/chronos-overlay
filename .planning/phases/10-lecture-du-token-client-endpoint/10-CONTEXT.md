# Phase 10: Lecture du token + client endpoint - Context

**Gathered:** 2026-07-09
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Service isolé et testé produisant, à la demande, un UsageSnapshot **Exact** depuis
`GET https://api.anthropic.com/api/oauth/usage`, ou « indisponible » proprement — sans jamais
logger, écrire ni exposer le token OAuth.

Requirements couverts : TOK-01, TOK-02, TOK-03, API-01, API-02, API-03.
</domain>

<decisions>
## Implementation Decisions

### Emplacement du token (RECONNU empiriquement le 2026-07-09 — verrouillé)
- `%APPDATA%/Claude/config.json` → clé `oauth:tokenCache` : chaîne base64 d'un blob **safeStorage `v10`**
  (préfixe décodé = « v10 » sur 3 octets, puis nonce 12o + ciphertext + tag GCM 16o), AES-256-GCM.
- La clé AES : `%APPDATA%/Claude/Local State` → `os_crypt.encrypted_key` (base64), préfixe « DPAPI »
  (5 octets) à retirer, puis CryptUnprotectData (DPAPI, portée utilisateur courant) → 32 octets de clé.
  ATTENTION : la reconnaissance a montré que `Local State` (490 o) pourrait ne pas exposer os_crypt de
  façon standard — le chercheur DOIT vérifier le schéma réel et, si besoin, tester la variante Electron
  safeStorage Windows (certaines versions chiffrent le blob directement via DPAPI sans clé AES intermédiaire).
  Le TEST DÉCISIF tranche empiriquement.
- Plaintext déchiffré = JSON `{ accessToken, refreshToken, expiresAt, scopes, subscriptionType, ... }`.

### Endpoint (RECONNU dans cli.js — verrouillé)
- `GET {BASE_API_URL}/api/oauth/usage` avec BASE_API_URL = `https://api.anthropic.com`.
- Headers : `Content-Type: application/json`, `Authorization: Bearer <accessToken>`,
  `anthropic-beta: oauth-2025-04-20`, User-Agent quelconque raisonnable, timeout 5 s.
- Réponse : `{ rate_limits: { five_hour: {used_percentage, resets_at}, seven_day: {...} } }`
  (mêmes champs que le pont statusLine : used_percentage 0..100, resets_at epoch SECONDES).

### TEST DÉCISIF (première étape de recherche — OBLIGATOIRE)
Le chercheur écrit un petit prototype JETABLE (scratchpad, hors du repo/solution) qui :
1. déchiffre réellement le token de CETTE machine,
2. appelle réellement /api/oauth/usage,
3. imprime UNIQUEMENT la réponse d'usage (les %/resets), JAMAIS le token ni la clé.
Puis SUPPRIME le prototype. Consigne dans RESEARCH.md : le schéma de déchiffrement exact qui a marché,
un échantillon anonymisé de la réponse, et le mapping. Si l'appel échoue (401, schéma coffre différent),
documente-le comme bloquant AVANT de planifier le provider.

### Sécurité (transverse — NON NÉGOCIABLE)
- Le token en clair ne vit QU'en mémoire (variable locale), n'est JAMAIS : logué, écrit sur disque,
  mis en exception, concaténé dans une URL, ni exposé dans un message. Seul usage : en-tête Authorization.
- Lecture SEULE de config.json / Local State (jamais de réécriture).
- Un test doit prouver qu'aucune trace du token n'apparaît (ex. le lecteur n'a pas de chemin d'écriture).
- Tolérance totale : toute erreur (fichier absent, DPAPI échoue, GCM échoue, 401, réseau, JSON malformé)
  → « pas de token » / « indisponible », JAMAIS d'exception non gérée, JAMAIS de crash.

### Implémentation (.NET 8 — verrouillé)
- Déchiffrement : `System.Security.Cryptography.AesGcm` (dispo net8), DPAPI via
  `System.Security.Cryptography.ProtectedData` (paquet `System.Security.Cryptography.ProtectedData`,
  win-only, OU P/Invoke CryptUnprotectData — choisir ; NuGet ProtectedData acceptable car win-x64 déjà ciblé).
- Client HTTP : `HttpClient` (System.Net.Http, in-box). Pas de nouvelle dépendance web.
- Le service reste NEUTRE (couche Services, aucun type WPF) → garde de pureté verte.
- Modèle : réutiliser UsageSnapshot/WindowState existants (Reliability = Exact).

### Claude's Discretion
Nom des classes (ex. ClaudeTokenReader, ClaudeOAuthUsageProvider), NuGet ProtectedData vs P/Invoke,
structure des tests (mocker HttpMessageHandler pour tester le mapping/erreurs sans réseau réel).
</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- UsageSnapshot/WindowState (Phase 3), SourceReliability.Exact.
- ChronosPaths / AppContext pour les chemins %APPDATA%.
- Pattern de parsing tolérant (ClaudeUsageObjectProvider lit déjà rate_limits depuis usage.json —
  MÊME schéma de champs : réutiliser la logique de mapping used_percentage/resets_at).
- IUsageProvider (contrat GetAsync). 154 tests verts — ne rien casser.
- ServicesLayerPurityTests : le nouveau provider doit rester neutre.

### Established Patterns
- TDD, HttpMessageHandler mocké pour tests réseau déterministes.
- Commentaires français.

### Integration Points
- Composite (Phase 11) consommera ce provider — ici on livre juste le service isolé + tests.
</code_context>

<specifics>
## Specific Ideas

- Le mapping used_percentage/resets_at est DÉJÀ implémenté dans ClaudeUsageObjectProvider (lecture de
  usage.json) : factoriser une fonction de mapping partagée (rate_limits JSON → WindowState Exact) plutôt
  que dupliquer, pour que pont statusLine et endpoint OAuth restent cohérents.
- Prévoir la gestion de l'expiration : si expiresAt < now, ne pas appeler (token périmé → indisponible),
  éviter un 401 inutile.
</specifics>

<deferred>
## Deferred Ideas

- Refresh du token via refreshToken (v1.3). Intégration composite + toggle (Phase 11).
</deferred>
