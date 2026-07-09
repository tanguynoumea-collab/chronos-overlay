# REQUIREMENTS.md — Chronos v1.2 « Usage exact via l'endpoint OAuth »

## Contexte

L'app bureau interroge `GET https://api.anthropic.com/api/oauth/usage` (auth Bearer token OAuth local)
pour son `/usage` — chiffres EXACTS des deux fenêtres. v1.2 rejoue cet appel : Chronos devient exact,
automatique, sans terminal ni calibration. Le token principal est dans le coffre safeStorage `v10`
(AES-256-GCM, clé DPAPI) de l'app bureau (`%APPDATA%/Claude/config.json` → `oauth:tokenCache`).

**Sécurité (non négociable, transverse) :** le token en clair ne vit QU'en mémoire ; JAMAIS logué,
JAMAIS écrit sur disque, JAMAIS transmis ailleurs qu'à `api.anthropic.com` en en-tête Authorization.
Lecture SEULE du coffre (jamais de réécriture). Aucune dépendance à un secret en dur.

## v1.2 Requirements

### Lecture du token (TOK)

- [ ] **TOK-01**: Un lecteur déchiffre le token OAuth : lit `config.json['oauth:tokenCache']` (base64 `v10`),
  récupère la clé AES depuis `Local State['os_crypt']['encrypted_key']` (préfixe DPAPI → CryptUnprotectData),
  puis AES-256-GCM (nonce 12o + tag 16o) → JSON `{accessToken, refreshToken, expiresAt, ...}`.
- [ ] **TOK-02**: Le lecteur est tolérant : fichier/clé absents, format inattendu, déchiffrement échoué →
  retourne « pas de token » SANS exception (Chronos bascule alors sur les sources v1.0/v1.1).
- [ ] **TOK-03**: Le token en clair n'est jamais logué, écrit, ni exposé (vérifiable : aucune trace du token
  dans les logs/fichiers ; test asserte que le lecteur ne persiste rien).

### Client endpoint (API)

- [ ] **API-01**: Un client appelle `GET https://api.anthropic.com/api/oauth/usage` avec
  `Authorization: Bearer <accessToken>`, `anthropic-beta: oauth-2025-04-20`, timeout court (5 s),
  et mappe `rate_limits.five_hour/seven_day` (`used_percentage` 0..100 → Utilization, `resets_at`
  epoch s → ResetsAt) vers un UsageSnapshot **Exact**.
- [ ] **API-02**: Erreurs gérées sans crash : 401/403 (token expiré/refusé) → source indisponible +
  bascule repli ; réseau/timeout → repli ; réponse malformée → repli. Jamais d'exception non gérée.
- [ ] **API-03**: Le provider est **inerte hors ligne** et ne bloque jamais l'UI (appel asynchrone,
  respecte le CancellationToken, n'impacte pas le tick 1 s d'interpolation).

### Intégration (INT)

- [ ] **INT-01**: ClaudeOAuthUsageProvider devient la source PRIMAIRE du composite (avant le pont
  statusLine et le repli JSONL) : Exact prioritaire par fenêtre, bascule automatique si indisponible.
- [ ] **INT-02**: Quand une fenêtre vient de l'endpoint (Exact), le badge « estimée » disparaît et les
  arcs prennent leur vraie couleur (utilization exacte) — l'honnêteté joue dans les deux sens.
- [ ] **INT-03**: Un réglage menu « Usage exact (OAuth) » permet d'activer/désactiver la source OAuth
  (persisté settings.json) ; désactivé → comportement v1.1 strict (aucun accès au token).

## Out of Scope (v1.2)

- Rafraîchissement du token via refreshToken (si expiré, on bascule en repli et l'utilisateur relance
  l'app bureau qui rafraîchit) — réévaluable v1.3.
- Autres endpoints OAuth (profile, organizations) — seul /usage nous intéresse.
- Stockage/cache du token par Chronos — explicitement interdit (TOK-03).

## Traceability

| REQ-ID | Phase | Statut |
|--------|-------|--------|
| TOK-01 | Phase 10 | Pending |
| TOK-02 | Phase 10 | Pending |
| TOK-03 | Phase 10 | Pending |
| API-01 | Phase 10 | Pending |
| API-02 | Phase 10 | Pending |
| API-03 | Phase 10 | Pending |
| INT-01 | Phase 11 | Pending |
| INT-02 | Phase 11 | Pending |
| INT-03 | Phase 11 | Pending |

**Couverture : 9/9 requirements mappés — aucun orphelin, aucun doublon.**

---
*Last updated: 2026-07-09 — roadmap v1.2 créée (phases 10-11)*
