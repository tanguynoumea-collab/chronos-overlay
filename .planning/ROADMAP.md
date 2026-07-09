# Roadmap : Chronos

## Milestones

- ✅ **v1.0 — Overlay de quotas Claude complet** (7 phases, 18 plans, SHIPPED 2026-07-08) — [archive](.planning/milestones/v1.0-ROADMAP.md)
- ✅ **v1.1 — Estimation utile en mode app bureau** (2 phases, 5 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.1-ROADMAP.md)
- 🚧 **v1.2 — Usage exact via l'endpoint OAuth** (2 phases, phases 10-11, en cours)

## Prochain milestone

Après v1.2 : révélation au survol (V2-02), tooltip riche (V2-03), bande sous-agents (V2-01),
tray (V2-05), opacité/échelle (V2-06), rafraîchissement du token OAuth (refreshToken — différé v1.3).

---

## Milestone v1.2 : Usage exact via l'endpoint OAuth

### Overview

L'app bureau Claude interroge `GET https://api.anthropic.com/api/oauth/usage` (auth Bearer, token OAuth
local du coffre safeStorage `v10`) pour alimenter sa commande `/usage` — ce sont les **chiffres exacts**
des deux fenêtres. v1.2 rejoue cet appel depuis Chronos : l'overlay devient exact, automatique, sans
terminal ni calibration manuelle. Le provider OAuth s'insère comme source **primaire** du composite déjà
en place (Exact > pont statusLine > repli JSONL estimé), par fenêtre et avec bascule automatique.

Le sujet est **sensible** : lire et déchiffrer un token OAuth. Le découpage isole donc toute la surface à
risque dans une seule phase (Phase 10), testée de bout en bout avant de toucher l'UI (Phase 11). La
**sécurité est transverse et non négociable** : le token en clair ne vit qu'en mémoire, n'est JAMAIS logué,
écrit sur disque, ni transmis ailleurs qu'à `api.anthropic.com` en en-tête `Authorization` ; lecture SEULE
du coffre (jamais de réécriture) ; tolérance totale aux erreurs (jamais de crash, bascule repli). Désactivée
via le menu, la source OAuth n'accède jamais au token — Chronos retombe au comportement v1.1 strict.

### Phases

**Numérotation des phases :**
- Phases entières (…, 10, 11) : travail de milestone planifié — continue après la Phase 9 (v1.1)
- Phases décimales (10.1, 10.2) : insertions urgentes (marquées INSERTED)

- [x] **Phase 10 : Lecture du token + client endpoint** - La partie sensible, isolée et testée à fond : un service produit à la demande un UsageSnapshot Exact depuis `/api/oauth/usage` (ou « indisponible » proprement), sans jamais logger, persister ni exposer le token (completed 2026-07-09)
- [ ] **Phase 11 : Intégration composite + réglage** - Le provider OAuth devient source primaire, le badge « estimée » disparaît sur les fenêtres exactes, et un réglage menu on/off (persisté) gouverne l'accès au token

### Phase Details

### Phase 10 : Lecture du token + client endpoint
**Goal**: Livrer un service isolé et fortement testé qui, à la demande, déchiffre le token OAuth du coffre de l'app bureau, appelle `GET https://api.anthropic.com/api/oauth/usage` et produit un UsageSnapshot **Exact** des deux fenêtres — ou un état « indisponible » propre — sans jamais logger, écrire, ni exposer le token, et sans jamais faire crasher l'overlay.
**Depends on**: Phase 3 (modèles UsageSnapshot immuables + abstraction IUsageProvider existants) — première phase du milestone v1.2
**Requirements**: TOK-01, TOK-02, TOK-03, API-01, API-02, API-03
**Success Criteria** (what must be TRUE):
  1. **Test décisif de bout en bout (première étape de la phase)** : sur le vrai poste, le service déchiffre une fois le token réel, effectue un appel réel à `/api/oauth/usage` et affiche les pourcentages exacts des fenêtres 5 h et 7 j — le token n'apparaissant à aucun moment en clair ailleurs qu'en en-tête `Authorization`. Si ce test échoue, la phase le documente comme **bloquant** (mécanisme de coffre/endpoint invalidé) plutôt que de poursuivre à l'aveugle. *(DÉJÀ RÉUSSI en recherche 2026-07-09 : déchiffrement réel + HTTP 200, cf. 10-RESEARCH.md ; schéma prouvé, re-confirmé en app en Phase 11.)*
  2. Le lecteur déchiffre le token : `config.json['oauth:tokenCache']` (base64 `v10`) + clé AES depuis `Local State['os_crypt']['encrypted_key']` (préfixe DPAPI → CryptUnprotectData) → AES-256-GCM (nonce 12 o + tag 16 o) → champ `token` de l'entrée `claude_code` (MAP), en mémoire ; fichier/clé absents, format inattendu ou déchiffrement échoué ⇒ retourne « pas de token » **sans exception** (TOK-01, TOK-02).
  3. Le client appelle l'endpoint avec `Authorization: Bearer <accessToken>`, `anthropic-beta: oauth-2025-04-20` et timeout court (5 s), puis mappe `five_hour/seven_day` (racine ; `utilization` 0..100 → Utilization 0..1 via `/100`, `resets_at` ISO 8601 → ResetsAt) vers un UsageSnapshot **Exact** ; 401/403, réseau/timeout et réponse malformée dégradent en « indisponible » + bascule repli, **jamais** d'exception non gérée (API-01, API-02).
  4. **Sécurité vérifiée par test** : le token en clair n'apparaît dans **aucun** log ni fichier (un test asserte que le lecteur ne persiste rien) ; le coffre est ouvert en **lecture seule** (aucune réécriture) ; le provider est asynchrone, respecte le CancellationToken, reste inerte hors ligne et n'impacte pas le tick 1 s d'interpolation (TOK-03, API-03).
**Plans**: 2 plans
Plans:
- [x] 10-01-PLAN.md — ClaudeTokenReader : déchiffrement DPAPI + AES-256-GCM v10 → champ `token` claude_code, tolérant, lecture seule prouvée (TOK-01/02/03)
- [x] 10-02-PLAN.md — ClaudeOAuthUsageProvider : GET /api/oauth/usage → UsageSnapshot Exact, tolérance erreurs, inertie token absent/expiré (API-01/02/03)

### Phase 11 : Intégration composite + réglage
**Goal**: Brancher le provider OAuth comme source **primaire** du composite existant, faire disparaître le badge « estimée » sur les fenêtres servies par l'endpoint, et donner à l'utilisateur un réglage menu on/off persisté qui gouverne l'accès au token (désactivé = comportement v1.1 strict, aucune lecture du coffre).
**Depends on**: Phase 10
**Requirements**: INT-01, INT-02, INT-03
**Success Criteria** (what must be TRUE):
  1. `ClaudeOAuthUsageProvider` est la source **primaire** du composite (avant le pont statusLine et le repli JSONL) : par fenêtre, l'Exact est prioritaire et la bascule vers les sources v1.0/v1.1 est automatique quand l'endpoint est indisponible (INT-01).
  2. Quand une fenêtre provient de l'endpoint (Exact), le badge « estimée » **disparaît** et l'arc prend sa vraie couleur (utilization exacte) — l'honnêteté joue dans les deux sens (INT-02).
  3. Un réglage menu « Usage exact (OAuth) » active/désactive la source OAuth, persisté dans settings.json ; désactivé, Chronos se comporte comme en v1.1 strict et **n'accède jamais** au token (INT-03).
**Plans**: 2 plans

Plans:
- [x] 11-01-PLAN.md — Chaîne composite à 3 (OAuth gated → statusLine → JSONL) + réglage OAuthUsageEnabled + portillon gated zéro-accès-token (INT-01/03)
- [ ] 11-02-PLAN.md — Toggle menu « Usage exact (OAuth) » persisté + badge « estimée » masqué en Exact + validation app réelle (INT-02/03)
**UI hint**: yes

### Progress

**Execution Order:**
Les phases s'exécutent dans l'ordre numérique : 10 → 11

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 10. Lecture du token + client endpoint | 2/2 | Complete    | 2026-07-09 |
| 11. Intégration composite + réglage | 1/2 | In Progress|  |
