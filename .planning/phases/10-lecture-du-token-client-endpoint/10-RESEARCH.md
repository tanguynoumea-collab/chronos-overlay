# Phase 10 : Lecture du token + client endpoint — Research

**Researched:** 2026-07-09
**Domain:** Déchiffrement coffre Electron safeStorage (DPAPI + AES-256-GCM) + client HTTP OAuth
**Confidence:** HIGH — test décisif exécuté DE BOUT EN BOUT sur cette machine (HTTP 200, données réelles)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Emplacement du token (RECONNU + VÉRIFIÉ empiriquement le 2026-07-09) :**
- `%APPDATA%/Claude/config.json` → clé `oauth:tokenCache` : chaîne base64 d'un blob **safeStorage `v10`**
  (préfixe décodé « v10 » sur 3 octets, puis nonce 12o + ciphertext + tag GCM 16o), AES-256-GCM.
- Clé AES : `%APPDATA%/Claude/Local State` → `os_crypt.encrypted_key` (base64), préfixe « DPAPI »
  (5 octets) à retirer, puis CryptUnprotectData (DPAPI, portée utilisateur courant) → 32 octets.
- Plaintext déchiffré = JSON `{ ..., token, refreshToken, expiresAt, subscriptionType, rateLimitTier }`.

**Endpoint (RECONNU dans cli.js) :**
- `GET https://api.anthropic.com/api/oauth/usage`.
- Headers : `Content-Type: application/json`, `Authorization: Bearer <accessToken>`,
  `anthropic-beta: oauth-2025-04-20`, User-Agent raisonnable, timeout 5 s.

**Sécurité (NON NÉGOCIABLE) :** token en clair uniquement en variable locale ; JAMAIS logué / écrit /
mis en exception / concaténé dans une URL / exposé. Seul usage : en-tête Authorization. Lecture SEULE de
config.json / Local State. Tolérance totale : toute erreur → « pas de token » / « indisponible », jamais
d'exception non gérée.

**Implémentation (.NET 8) :** `System.Security.Cryptography.AesGcm` (in-box) + DPAPI via
`System.Security.Cryptography.ProtectedData` (NuGet win-only) OU P/Invoke `CryptUnprotectData`.
`HttpClient` in-box (pas de dépendance web). Service NEUTRE (couche Services, aucun type WPF).
Réutiliser UsageSnapshot / WindowState (Reliability = Exact).

### Claude's Discretion
Nom des classes (ex. ClaudeTokenReader, ClaudeOAuthUsageProvider), NuGet ProtectedData vs P/Invoke,
structure des tests (mocker HttpMessageHandler pour tester mapping/erreurs sans réseau réel).

### Deferred Ideas (OUT OF SCOPE)
- Refresh du token via refreshToken (v1.3).
- Intégration composite + toggle menu (Phase 11 : INT-01/02/03).
- Autres endpoints OAuth (profile, organizations).
- Stockage/cache du token par Chronos (explicitement interdit — TOK-03).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TOK-01 | Lecteur déchiffre le token (config.json → clé DPAPI → AES-256-GCM → JSON) | **Schéma prouvé end-to-end.** Voir « Test décisif » + « Code Examples ». Nuance : la valeur `oauth:tokenCache` est une **MAP** (pas un objet plat), et le token est dans le champ `token` (pas `accessToken`). |
| TOK-02 | Lecteur tolérant : fichier/clé absents, format inattendu, déchiffrement échoué → « pas de token » sans exception | Pattern try/catch total démontré ; liste des exceptions à capturer dans « Pitfalls ». |
| TOK-03 | Token jamais logué / écrit / exposé ; test asserte que le lecteur ne persiste rien | Le lecteur retourne un `string` en mémoire ; aucun chemin d'écriture. Test recommandé : purity + « le reader n'a pas de dépendance FileStream en écriture ». |
| API-01 | Client GET /api/oauth/usage → mappe les 2 fenêtres vers UsageSnapshot **Exact** | **Schéma de réponse RÉEL capturé** (diffère de statusLine !). Mapping détaillé fourni. |
| API-02 | Erreurs gérées : 401/403 → indisponible + repli ; réseau/timeout → repli ; malformé → repli. Jamais d'exception non gérée | Pattern de gestion par statut + try/catch réseau documenté. |
| API-03 | Provider inerte hors ligne, asynchrone, respecte CancellationToken, n'impacte pas le tick 1 s | `HttpClient.GetAsync(ct)` + timeout court ; provider indépendant du tick d'interpolation. |
</phase_requirements>

## Summary

Le **test décisif a réussi de bout en bout** sur cette machine réelle : déchiffrement du token +
appel `/api/oauth/usage` → **HTTP 200 avec des chiffres d'usage réels**. Le schéma de coffre est le
schéma **Chromium/Electron safeStorage standard** (DPAPI enveloppant une clé AES-256, blob `v10`
AES-GCM), sans variante exotique. Le prototype jetable a été supprimé ; aucun secret n'a jamais été
écrit ni imprimé.

**Deux découvertes majeures qui corrigent des hypothèses de CONTEXT.md** et doivent piloter le plan :

1. **`oauth:tokenCache` n'est PAS un objet plat `{ accessToken, ... }` mais une MAP** dont les clés sont
   `"<accountUuid>:<orgUuid>:<baseUrl>:<scopes>"`. Le champ d'access token s'appelle **`token`** (pas
   `accessToken`). Le lecteur doit **énumérer la map et choisir l'entrée dont la clé contient le scope
   `claude_code`**.

2. **La réponse de `/api/oauth/usage` a un schéma DIFFÉRENT du bloc `rate_limits` de statusLine.**
   L'endpoint OAuth renvoie `five_hour` / `seven_day` **à la racine** (pas sous `rate_limits`), avec le
   champ **`utilization`** (0..100, un POURCENTAGE malgré son nom) et `resets_at` en **chaîne ISO 8601**
   (pas en epoch secondes). Conséquence directe : le mapping ne peut **pas** être partagé verbatim avec
   `ClaudeUsageObjectProvider` (qui lit `used_percentage` + epoch secondes). Voir « Mapping ».

**Primary recommendation :** Deux classes neutres — `ClaudeTokenReader` (déchiffre → renvoie l'access
token en mémoire, ou null) et `ClaudeOAuthUsageProvider : IUsageProvider` (appelle l'endpoint, parse le
schéma OAuth réel, mappe en UsageSnapshot Exact). Ajouter le NuGet `System.Security.Cryptography.ProtectedData`
8.0.0 (vérifié fonctionnel). Tout échec → `UsageSnapshot.Empty` / token null, jamais d'exception.

## Test décisif — schéma EXACT qui a fonctionné

> Exécuté le 2026-07-09 via un prototype .NET 8 console jetable (scratchpad, supprimé après coup).
> Aucune valeur de token/clé n'a été imprimée — uniquement tailles, préfixes et réponse d'usage.

### Étape 1 — Récupérer la clé AES (Local State)
```
Local State (490 o) → JSON → os_crypt.encrypted_key (base64)
  → décode = 283 octets, préfixe ASCII = "DPAPI" (5 octets)
  → retirer les 5 octets de préfixe → blob DPAPI (278 o)
  → ProtectedData.Unprotect(blob, null, CurrentUser)
  → 32 octets = clé AES-256   ✓
```
Remarque : `Local State` **expose bien `os_crypt.encrypted_key`** de façon standard. La crainte de
CONTEXT.md (« pourrait ne pas exposer os_crypt ») est **levée** : aucune variante Electron directe DPAPI
n'est nécessaire.

### Étape 2 — Déchiffrer le blob v10 (config.json)
```
config.json (2888 o) → JSON → oauth:tokenCache (base64, 2032 car.)
  → décode = 1522 octets, préfixe ASCII = "v10" (3 octets)
  → découpage : nonce = octets [3..15] (12 o)
                tag   = 16 derniers octets
                cipher= octets [15 .. len-16] (1491 o)
  → AesGcm(clé32, tagSize=16).Decrypt(nonce, cipher, tag) → 1491 octets UTF-8
  → JSON du tokenCache   ✓
```

### Étape 3 — Structure du plaintext (MAP, pas objet plat)
```
Racine = Object, 3 entrées. Clés (anonymisées) de forme :
  "<accountUuid>:<orgUuid>:https://api.anthropic.com:<scopes...>:claude_code"
Chaque VALEUR = objet à 5 champs :
  { token, refreshToken, expiresAt, subscriptionType, rateLimitTier }
  → l'access token est le champ **token** (108 caractères ici).
  → expiresAt = date future (non expiré au moment du test).
Sélection : première entrée dont la clé contient "claude_code".
```

### Étape 4 — Appel endpoint → HTTP 200
```
GET https://api.anthropic.com/api/oauth/usage
Headers: Authorization: Bearer <token>, anthropic-beta: oauth-2025-04-20,
         Content-Type: application/json, User-Agent: Chronos/1.2
→ 200 OK
```

### Réponse RÉELLE anonymisée (schéma exact de l'endpoint)
```jsonc
{
  "five_hour":  { "utilization": 42.0, "resets_at": "2026-07-09T09:39:59.692687+00:00",
                  "limit_dollars": null, "used_dollars": null, "remaining_dollars": null },
  "seven_day":  { "utilization": 73.0, "resets_at": "2026-07-10T21:59:59.692707+00:00",
                  "limit_dollars": null, "used_dollars": null, "remaining_dollars": null },
  "seven_day_oauth_apps": null, "seven_day_opus": null, "seven_day_sonnet": null,
  "seven_day_cowork": null, /* ... autres fenêtres nommées, toutes null ici ... */
  "extra_usage": { "is_enabled": false, "monthly_limit": null, /* ... */ },
  "limits": [
    { "kind": "session",      "group": "session", "percent": 42, "severity": "normal",
      "resets_at": "2026-07-09T09:39:59.692687+00:00", "scope": null, "is_active": false },
    { "kind": "weekly_all",   "group": "weekly",  "percent": 73, "severity": "critical",
      "resets_at": "2026-07-10T21:59:59.692707+00:00", "scope": null, "is_active": false },
    { "kind": "weekly_scoped","group": "weekly",  "percent": 100, "severity": "critical",
      "resets_at": "2026-07-10T21:59:59.693030+00:00",
      "scope": { "model": { "id": null, "display_name": "<modele>" } }, "is_active": true }
  ],
  "spend": { "used": { "amount_minor": 0, "currency": "USD", "exponent": 2 }, /* ... */ },
  "member_dashboard_available": false
}
```
> Valeurs `utilization` anonymisées (synthétiques plausibles) ; structure et noms de champs = réels.

## ⚠️ Schéma OAuth vs statusLine — NE PAS confondre

| Aspect | statusLine (`ClaudeUsageObjectProvider`, usage.json) | Endpoint OAuth (`/api/oauth/usage`) — CETTE PHASE |
|--------|------------------------------------------------------|---------------------------------------------------|
| Enveloppe | `rate_limits.five_hour` / `.seven_day` | `five_hour` / `seven_day` **à la racine** |
| Champ % | **`used_percentage`** (0..100) | **`utilization`** (0..100 — un POURCENTAGE) |
| Reset | `resets_at` = **epoch SECONDES** (int) | `resets_at` = **chaîne ISO 8601** (`...+00:00`, microsecondes) |
| Extras | aucun | `limits[]`, `extra_usage`, `spend`, `seven_day_opus`… |

**Piège nommage :** le champ OAuth s'appelle `utilization` mais vaut **0..100**, alors que
`WindowState.Utilization` attend **0..1**. Il faut **diviser par 100**. Ne jamais assigner `utilization`
brut à `WindowState.Utilization`.

## Mapping vers UsageSnapshot

| Source OAuth (réel) | `WindowState` | Conversion |
|---------------------|---------------|------------|
| `five_hour.utilization` (0..100) | `Utilization` (0..1) | `utilization / 100.0` |
| `five_hour.resets_at` (ISO 8601 string) | `ResetsAt` (`DateTimeOffset`) | `DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)` |
| `seven_day.utilization` | idem (arc hebdo) | `/ 100.0` |
| `seven_day.resets_at` | `ResetsAt` | `DateTimeOffset.Parse(...)` |
| fenêtre absente / null | `WindowState.Unavailable(kind)` | jamais de valeur inventée |
| toutes fenêtres | `Reliability` | **`SourceReliability.Exact`** |
| — | `FractionTimeRemaining` | `WindowState.FractionRemaining(reset, clock.UtcNow, len)` (len = 5 h / 7 j) |

**Factoring honnête :** le `/100` + le calcul de `FractionTimeRemaining` sont communs aux deux
providers, MAIS l'extraction de champ et le parsing du temps DIFFÈRENT (nom + format). Recommandation :
extraire un helper neutre `WindowState BuildExact(WindowKind kind, double? utilPercent, DateTimeOffset?
resetsAt, TimeSpan len, IClock clock)` que **chaque** provider alimente après avoir lu SON propre schéma.
Ne PAS tenter de partager le parsing JSON entre statusLine (epoch/used_percentage) et OAuth
(ISO/utilization) — ce sont deux contrats distincts.

## Standard Stack

### Core
| Composant | Version | Rôle | Pourquoi |
|-----------|---------|------|----------|
| `System.Security.Cryptography.AesGcm` | in-box net8.0 | Déchiffrement AES-256-GCM du blob v10 | Aucune dépendance ; testé OK sur cette machine |
| `System.Security.Cryptography.ProtectedData` | **8.0.0** (NuGet) | DPAPI `Unprotect` (CurrentUser) de la clé AES | Vérifié fonctionnel ; aligné ligne 8.0.x (cf. CLAUDE.md) |
| `System.Net.Http.HttpClient` | in-box net8.0 | GET /api/oauth/usage | Pas de dépendance web ; supporte CancellationToken + Timeout |
| `System.Text.Json` | in-box net8.0 | Parse tokenCache + réponse usage | Déjà le standard du repo (parsing tolérant) |

**Installation (projet `src/Chronos/Chronos.csproj`) :**
```bash
dotnet add src/Chronos/Chronos.csproj package System.Security.Cryptography.ProtectedData --version 8.0.0
```

### Alternatives Considered
| Au lieu de | Possible | Tradeoff |
|------------|----------|----------|
| NuGet ProtectedData | P/Invoke `CryptUnprotectData` (crypt32.dll) | Évite 1 dépendance NuGet, mais + code interop/tests. NuGet plus simple et déjà win-x64 ciblé → **recommandé NuGet**. |
| `HttpClient` direct | `IHttpClientFactory` (Extensions.Http) | Overkill pour 1 appel ponctuel non concurrent. Un `HttpClient` statique/injecté suffit. |

**Vérification version :** `System.Security.Cryptography.ProtectedData` 8.0.0 restauré et exécuté avec
succès pendant le test décisif (SDK .NET 10.0.201 ciblant `net8.0-windows`). AesGcm et HttpClient sont
in-box (aucune install).

## Architecture Patterns

### Structure recommandée (couche Services neutre)
```
src/Chronos/Services/
├── ClaudeTokenReader.cs          # déchiffre → string? (access token en mémoire) ou null
├── IClaudeTokenReader.cs         # contrat (testabilité : fake reader)
├── ClaudeOAuthUsageProvider.cs   # IUsageProvider : GET endpoint → UsageSnapshot Exact
└── (helper) BuildExact(...)      # sur WindowState ou util interne partagé
```

### Pattern 1 : ClaudeTokenReader (déchiffrement)
**What :** lit config.json + Local State, déchiffre, énumère la MAP, renvoie le champ `token` de
l'entrée `claude_code`. **Aucun chemin d'écriture**, tout en variables locales.
**When :** appelé par le provider avant chaque requête (ou mis en cache mémoire courte — hors scope ici).
**Signature neutre :** `string? TryReadAccessToken(out DateTimeOffset? expiresAt)` — renvoie `null` sur
tout échec, sans exception.

### Pattern 2 : ClaudeOAuthUsageProvider (client + mapping)
**What :** `GetAsync(ct)` → si token null ou expiré → `UsageSnapshot.Empty` ; sinon GET endpoint, parse
schéma OAuth, mappe 2 fenêtres en Exact.
**Court-circuit expiration (specifics CONTEXT) :** si `expiresAt < now` → ne PAS appeler (évite un 401
inutile) → `UsageSnapshot.Empty`.
**Injection HTTP :** accepter un `HttpClient` (ou `HttpMessageHandler`) injecté pour tester le mapping /
les statuts d'erreur sans réseau réel (pattern établi du repo).

### Anti-Patterns à éviter
- **Réutiliser le parsing de `ClaudeUsageObjectProvider` tel quel** : schéma différent (racine,
  `utilization`, ISO 8601) → mapping cassé. Extraire seulement le helper `BuildExact` neutre.
- **Chercher `accessToken` au lieu de `token`** : le champ réel est `token` → token toujours null.
- **Traiter `oauth:tokenCache` comme un objet plat** : c'est une MAP → énumérer.
- **Logger l'exception brute** d'un échec de déchiffrement : elle pourrait contenir des fragments
  sensibles. Capturer et convertir en « pas de token » **sans** journaliser le détail.
- **`static HttpClient` avec `Timeout` mutable partagé** : préférer un `HttpClient` dédié au provider,
  Timeout 5 s, ou passer le timeout via `CancellationTokenSource`.

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser | Pourquoi |
|----------|-------------------|----------|----------|
| DPAPI unwrap | Interop CryptUnprotectData maison + gestion mémoire | `ProtectedData.Unprotect` (NuGet) | Gère LocalAlloc/Free, entropie, erreurs Win32 |
| AES-256-GCM | Impl AES/GCM maison | `AesGcm` in-box | Tag auth vérifié, timing-safe, testé |
| Parse ISO 8601 microsecondes+offset | Split de string manuel | `DateTimeOffset.Parse(..., RoundtripKind)` | Gère fraction variable + offset |
| Requête HTTP + timeout + annulation | Socket/handler maison | `HttpClient.GetAsync(ct)` + `Timeout` | Redirects, TLS, pool, annulation |
| Base64 | Décodage manuel | `Convert.FromBase64String` | — |

**Key insight :** tout le cœur cryptographique est fourni in-box/NuGet MS et **déjà prouvé fonctionnel
sur cette machine**. Le seul « métier » à écrire est l'orchestration (découpe des offsets, énumération de
la map, mapping, tolérance d'erreur).

## Runtime State Inventory

> Phase de lecture SEULE (aucun rename/migration). Rien n'est écrit ni enregistré.

| Catégorie | Trouvé | Action |
|-----------|--------|--------|
| Stored data | `%APPDATA%/Claude/config.json` (clé `oauth:tokenCache`) + `Local State` (`os_crypt.encrypted_key`) — **lus en lecture seule** | Aucune écriture. Ne jamais réécrire ces fichiers. |
| Live service config | Aucune — Chronos ne configure aucun service externe ici | None — vérifié (provider isolé). |
| OS-registered state | DPAPI CurrentUser : la clé AES est déchiffrable **uniquement par le compte Windows courant** (contrainte, pas un état à modifier) | None — dépendance runtime documentée (voir Pitfalls). |
| Secrets/env vars | Aucun secret en dur, aucune var d'env introduite | None — TOK-03 respecté (token en mémoire seule). |
| Build artifacts | Ajout NuGet `ProtectedData` 8.0.0 au csproj → restauration NuGet | `dotnet restore` (automatique au build). |

**Impératif transverse :** Chronos ne DOIT jamais écrire dans `%APPDATA%/Claude`. L'accès est
strictement `FileAccess.Read`.

## Common Pitfalls

### Pitfall 1 : token cache = MAP, champ `token`
**Ce qui rate :** chercher `root.accessToken` → toujours null → provider silencieusement indisponible.
**Cause :** la valeur est `{ "<uuid>:<uuid>:<baseUrl>:<scopes>": { token, refreshToken, expiresAt, ... } }`.
**Éviter :** énumérer les entrées, filtrer sur clé contenant `claude_code`, lire le champ **`token`**.
**Signe :** déchiffrement OK (JSON valide) mais « pas de token ».

### Pitfall 2 : schéma réponse ≠ statusLine
**Ce qui rate :** parser `rate_limits.five_hour.used_percentage` + epoch → NRE / valeurs nulles.
**Cause :** l'endpoint renvoie `five_hour.utilization` (racine) + `resets_at` ISO 8601.
**Éviter :** parser le schéma OAuth réel (voir « Mapping ») ; utiliser `DateTimeOffset.Parse` pas
`FromUnixTimeSeconds`.

### Pitfall 3 : `utilization` 0..100 vs WindowState 0..1
**Ce qui rate :** arcs à 100 % en permanence (utilization 64.0 assigné brut).
**Éviter :** `Utilization = utilization / 100.0`.

### Pitfall 4 : DPAPI lié au compte + à l'app active
**Ce qui rate :** `ProtectedData.Unprotect` lève `CryptographicException` si exécuté sous un autre compte
Windows, ou token expiré → 401.
**Cause :** DPAPI CurrentUser ; et `expiresAt` fini (ici +10 jours) → si l'app bureau ne rafraîchit pas,
le token périme.
**Éviter :** capturer `CryptographicException` → « pas de token ». Vérifier `expiresAt < now` AVANT
l'appel → indisponible (repli). 401/403 → indisponible (token révoqué/périmé).
**Signe :** 401 alors que `expiresAt` semble futur → token révoqué côté serveur → basculer repli.

### Pitfall 5 : exceptions à capturer (tolérance TOK-02/API-02)
Liste concrète : `FileNotFoundException`, `DirectoryNotFoundException`, `IOException`, `JsonException`,
`FormatException` (base64), `CryptographicException` (DPAPI + tag GCM invalide),
`ArgumentException`/`IndexOutOfRange` (blob trop court), `HttpRequestException`, `TaskCanceledException`
(timeout/annulation), `OperationCanceledException`. Toutes → token null / `UsageSnapshot.Empty`.

### Pitfall 6 : ne rien fuiter
**Éviter :** ne jamais mettre le token dans un message d'exception, une URL, un log. Le token ne quitte
la méthode que via l'en-tête `Authorization`. Un test doit asserter que `ClaudeTokenReader` n'a **aucune
API d'écriture disque** et ne retourne rien d'autre que le token en mémoire.

## Code Examples

### Déchiffrement (schéma vérifié — offsets réels)
```csharp
// Source : test décisif 2026-07-09 (net8.0-windows, ProtectedData 8.0.0). Prototype supprimé.
// Clé AES depuis Local State
byte[] encKey = Convert.FromBase64String(osCrypt.GetProperty("encrypted_key").GetString()!);
byte[] aesKey = ProtectedData.Unprotect(encKey[5..] /* retire "DPAPI" */, null,
                                        DataProtectionScope.CurrentUser); // → 32 octets

// Blob v10 depuis config.json['oauth:tokenCache']
byte[] blob   = Convert.FromBase64String(tokenCacheB64);           // préfixe "v10" (3o)
byte[] nonce  = blob[3..15];                                       // 12 o
byte[] tag    = blob[^16..];                                       // 16 o
byte[] cipher = blob[15..^16];
byte[] plain  = new byte[cipher.Length];
using (var gcm = new AesGcm(aesKey, 16))
    gcm.Decrypt(nonce, cipher, tag, plain);
string tokenJson = Encoding.UTF8.GetString(plain);                 // JSON = MAP
```

### Sélection de l'entrée claude_code + champ `token`
```csharp
using var doc = JsonDocument.Parse(tokenJson);
foreach (var entry in doc.RootElement.EnumerateObject())
{
    if (!entry.Name.Contains("claude_code")) continue;
    var v = entry.Value;
    if (v.TryGetProperty("token", out var t) && t.ValueKind == JsonValueKind.String)
    {
        accessToken = t.GetString();                 // en mémoire seulement
        if (v.TryGetProperty("expiresAt", out var e)) expiresAt = ParseExpiry(e);
        break;
    }
}
```

### Appel + mapping (schéma OAuth réel)
```csharp
using var req = new HttpRequestMessage(HttpMethod.Get,
    "https://api.anthropic.com/api/oauth/usage");
req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
req.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
using var resp = await http.SendAsync(req, ct);      // http.Timeout = 5 s
if (!resp.IsSuccessStatusCode) return UsageSnapshot.Empty;   // 401/403/5xx → indisponible
using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), ct);
var root = doc.RootElement;

WindowState Read(string name, WindowKind kind, TimeSpan len)
{
    if (!root.TryGetProperty(name, out var w) || w.ValueKind != JsonValueKind.Object)
        return WindowState.Unavailable(kind);
    double? util = w.TryGetProperty("utilization", out var u) && u.TryGetDouble(out var p)
        ? p / 100.0 : null;                           // 0..100 → 0..1
    DateTimeOffset? reset = w.TryGetProperty("resets_at", out var r)
        && r.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(r.GetString(), CultureInfo.InvariantCulture,
             DateTimeStyles.RoundtripKind, out var d) ? d : null;  // ISO 8601
    return new WindowState { Kind = kind, Utilization = util, ResetsAt = reset,
        Reliability = SourceReliability.Exact,
        FractionTimeRemaining = WindowState.FractionRemaining(reset, _clock.UtcNow, len) };
}
return new UsageSnapshot {
    FiveHour = Read("five_hour", WindowKind.FiveHour, TimeSpan.FromHours(5)),
    SevenDay = Read("seven_day", WindowKind.SevenDay, TimeSpan.FromDays(7)),
    SourceCapturedAt = _clock.UtcNow };
```

## State of the Art

| Ancienne hypothèse (CONTEXT.md) | Réalité vérifiée 2026-07-09 | Impact |
|--------------------------------|------------------------------|--------|
| `oauth:tokenCache` = objet `{ accessToken, ... }` | MAP par scope ; champ = `token` | Énumérer + filtrer `claude_code` |
| Local State « pourrait ne pas exposer os_crypt » | `os_crypt.encrypted_key` présent, standard | Pas de variante Electron directe nécessaire |
| Réponse = `rate_limits.<w>.used_percentage` + epoch s | `<w>.utilization` racine + ISO 8601 | Mapping OAuth dédié (pas de partage verbatim) |

## Environment Availability

| Dépendance | Requise par | Dispo | Version | Fallback |
|------------|-------------|-------|---------|----------|
| SDK .NET | build/test | ✓ | 10.0.201 (cible net8.0-windows) | — |
| `%APPDATA%/Claude/config.json` | TOK-01 | ✓ | 2888 o, clé `oauth:tokenCache` présente | token null → repli |
| `%APPDATA%/Claude/Local State` | TOK-01 | ✓ | 490 o, `os_crypt.encrypted_key` présent | token null → repli |
| DPAPI (compte Windows courant) | TOK-01 | ✓ | Unprotect → 32 o OK | exception → token null |
| `api.anthropic.com` (réseau) | API-01 | ✓ | HTTP 200 vérifié | timeout/erreur → repli |
| NuGet `ProtectedData` | TOK-01 | ✓ (restauré) | 8.0.0 | P/Invoke crypt32 |

**Aucune dépendance bloquante sans fallback.** Tous les échecs dégradent proprement vers « indisponible »
+ repli (v1.0/v1.1), conformément à TOK-02 / API-02.

## Validation Architecture

### Test Framework
| Propriété | Valeur |
|-----------|--------|
| Framework | xUnit 2.9.2 (+ Xunit.StaFact 1.1.11 pour tests UI STA) |
| Config | `tests/Chronos.Tests/Chronos.Tests.csproj` (net8.0-windows, `IsTestProject`) |
| Quick run | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~OAuth"` |
| Full suite | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj` |

### Phase Requirements → Test Map
| Req | Comportement | Type | Commande | Fichier |
|-----|--------------|------|----------|---------|
| TOK-01 | Découpe blob v10 (offsets nonce/tag) + sélection champ `token` sur MAP | unit | `dotnet test --filter ClaudeTokenReaderTests` | ❌ Wave 0 |
| TOK-02 | Fichier absent / base64 invalide / blob court / GCM tag faux → token null sans exception | unit | idem | ❌ Wave 0 |
| TOK-03 | Le reader n'expose aucune API d'écriture ; purity (aucun WPF) | unit | `--filter ServicesLayerPurityTests` (existant) + nouveau test « no write path » | ⚠️ étendre |
| API-01 | Mapping `utilization`/100 + `resets_at` ISO → UsageSnapshot Exact (HttpMessageHandler mocké) | unit | `--filter ClaudeOAuthUsageProviderTests` | ❌ Wave 0 |
| API-02 | 401/403 → Empty ; timeout (TaskCanceled) → Empty ; JSON malformé → Empty | unit | idem | ❌ Wave 0 |
| API-03 | `GetAsync(ct)` respecte l'annulation ; inerte si token null (aucun appel réseau) | unit | idem | ❌ Wave 0 |

### Sampling Rate
- **Par commit de tâche :** `dotnet test --filter "FullyQualifiedName~OAuth|FullyQualifiedName~Token"`
- **Par merge de wave :** `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj`
- **Phase gate :** suite complète verte (les 154 tests existants + nouveaux) avant `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/ClaudeTokenReaderTests.cs` — TOK-01/02/03 avec blobs synthétiques
      (fabriquer un blob v10 chiffré par une clé de test AES connue — PAS le vrai token).
- [ ] `tests/Chronos.Tests/ClaudeOAuthUsageProviderTests.cs` — API-01/02/03 via
      `FakeHttpMessageHandler` (réponse OAuth réelle anonymisée + statuts d'erreur).
- [ ] `tests/Chronos.Tests/Fakes/FakeHttpMessageHandler.cs` — handler mocké (n'existe pas encore ;
      aucun test réseau actuel).
- [ ] (option) `tests/Chronos.Tests/Fakes/FakeClaudeTokenReader.cs` — injecter un token/null au provider.

## Open Questions

1. **Deux entrées `claude_code` dans la MAP (scopes légèrement différents).**
   - Connu : les deux ont un champ `token` valide ; la 1re a donné 200.
   - Incertain : laquelle est « la bonne » si les `expiresAt` diffèrent.
   - Reco : prendre la 1re entrée `claude_code` avec `token` non vide ET `expiresAt` le plus lointain (ou
     simplement la 1re) ; sur 401, ne pas itérer les autres en v1.2 (repli). Robuste et simple.

2. **Fenêtres `seven_day_opus` / `weekly_scoped` (limits[])**
   - Connu : l'endpoint expose des sous-fenêtres par modèle (ex. `weekly_scoped` à 100 %).
   - Incertain : Chronos v1.2 n'affiche que 2 arcs (5 h + hebdo global).
   - Reco : mapper uniquement `five_hour` + `seven_day` (hebdo global). Ignorer `limits[]`/sous-fenêtres
     (hors scope ; piste v1.3+).

3. **Stabilité du schéma (API privée de facto)**
   - Le contrat `/api/oauth/usage` n'est pas documenté publiquement → susceptible de changer.
   - Reco : parsing tolérant (fenêtre absente → Unavailable), test de contrat sur l'échantillon,
     dégrader plutôt que crasher (aligné data-sources.md §4).

## Sources

### Primary (HIGH confidence)
- **Test décisif empirique 2026-07-09** (cette machine) — déchiffrement + HTTP 200 réels. Schéma coffre,
  structure tokenCache (MAP + champ `token`), schéma réponse `/api/oauth/usage`. Prototype supprimé.
- `%APPDATA%/Claude/config.json` + `Local State` — structure inspectée en lecture seule (clés/tailles).
- Repo Chronos : `IUsageProvider`, `WindowState`, `UsageSnapshot`, `SourceReliability`, `ChronosPaths`,
  `ClaudeUsageObjectProvider`, `ServicesLayerPurityTests` — contrats et patterns réutilisés.
- `docs/data-sources.md` — schéma statusLine (used_percentage/epoch) pour contraste.

### Secondary (MEDIUM confidence)
- CONTEXT.md / REQUIREMENTS.md — décisions verrouillées (endpoint, headers, sécurité).
- Schéma safeStorage Chromium/Electron (DPAPI + AES-256-GCM v10) — confirmé par le test, connu du
  domaine.

## Metadata

**Confidence breakdown:**
- Standard stack : HIGH — toutes dépendances exécutées avec succès sur cette machine.
- Schéma déchiffrement : HIGH — prouvé end-to-end (32 o clé, GCM OK, JSON valide).
- Schéma réponse endpoint : HIGH — HTTP 200 capturé, champs réels.
- Stabilité inter-version du schéma : MEDIUM — API privée de facto (peut changer).
- Sélection multi-entrées de la MAP : MEDIUM — heuristique `claude_code` validée sur 1 cas.

**Research date:** 2026-07-09
**Valid until:** ~7 jours pour le schéma de réponse (API privée) ; ~30 jours pour le schéma de coffre
(safeStorage stable). Revalider à chaque MAJ majeure de l'app Claude bureau.
