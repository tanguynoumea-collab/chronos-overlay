# REQUIREMENTS.md — Chronos v1.5 « Exactitude permanente »

## Contexte

Depuis le passage du forfait **Max x5 à Max x20**, Chronos affiche des pourcentages faux. Le diagnostic mené
le 2026-09-09 sur la machine réelle montre que ce n'est pas un bug isolé mais une **panne silencieuse des trois
sources exactes**, doublée d'une doctrine de repli défaillante :

- jeton OAuth expiré le 2026-07-12 → `GET /api/oauth/usage` renvoie **HTTP 401**, sans le moindre signal ;
- `%APPDATA%/Chronos/usage.json` figé au 2026-07-10 (`resets_at: 9`, soit epoch 1970) mais toujours servi
  comme `Exact`, car `ClaudeUsageObjectProvider` n'applique **aucune limite d'âge** ;
- le cache du dernier relevé exact vit **en RAM seulement** → perdu à chaque démarrage de l'exe ;
- `CompositeUsageProvider.Best()` classant **uniquement par fiabilité**, une donnée « exacte » de deux mois
  bat une estimation fraîche ;
- le repli calcule `tokens / plafond` avec des plafonds calibrés sous Max x5, dont celui des 5 h est en source
  `Manual` — donc gelé à vie par conception de `BudgetCalibration.ApplyAuto`.

**Décision de fond :** les limites Anthropic **pondèrent par modèle**, donc `tokens / plafond` restera faux même
avec le bon plafond. On cesse de chercher à rendre l'estimation absolue juste : elle est **supprimée**. Les
transcripts JSONL ne savent répondre qu'à deux questions bornées — *y a-t-il eu de l'activité depuis T ?* et
*combien de tokens depuis T ?* — et ne servent donc plus qu'à **corriger un delta** par rapport à un relevé
exact. Doctrine cible : **exact frais → dernier exact persisté (encore exact si rien ne s'est passé) → dernier
exact + delta borné et marqué → indisponible.** Jamais de pourcentage inventé.

S'y ajoute une source exacte supplémentaire reprise de `github.com/juppeee/claude-session-browser`
(`clawdmeter.py:150`) : les en-têtes `anthropic-ratelimit-unified-*` d'une requête jetable, qui répondent
**même sur un 429** et exposent le statut serveur et le dépassement.

## v1.5 Requirements

### Exactitude & doctrine d'affichage (EXA)

- [x] **EXA-01**: Le dernier relevé exact est **persisté sur disque** avec son horodatage et rechargé au
  démarrage, pour que Chronos ne reparte jamais sans chiffre (tue la bascule au redémarrage de l'exe).
- [ ] **EXA-02**: Au-delà d'un **âge maximal**, une source exacte cesse d'être présentée comme exacte —
  fin du « 10 % vieux de deux mois marqué `Exact` ».
- [ ] **EXA-03**: Le cadran **distingue visuellement** trois états : chiffre exact frais, chiffre exact daté,
  état indisponible. `IsStale` (aujourd'hui calculé mais bindé nulle part) devient un signal réel à l'écran.
- [ ] **EXA-04**: Aucune **utilization absolue dérivée d'un comptage de tokens** n'est plus jamais affichée,
  quelle que soit la situation.
- [ ] **EXA-05**: Si **aucun chiffre exact n'a jamais été obtenu**, l'overlay affiche « indisponible » et invite
  à se connecter — jamais un pourcentage.
- [ ] **EXA-06**: Le **diagnostic** indique quelle source alimente réellement l'affichage, et depuis quand.

### Correction par delta (DEL)

- [x] **DEL-01**: Les transcripts JSONL répondent à « y a-t-il eu une **réponse assistant depuis l'instant T** ? »
  sans produire de pourcentage.
- [x] **DEL-02**: Les transcripts JSONL fournissent la **somme de tokens depuis l'instant T**.
- [ ] **DEL-03**: **Sans activité** depuis le dernier relevé exact, ce relevé est présenté comme **encore exact**
  (l'utilisation n'a pas bougé) — et non comme périmé.
- [ ] **DEL-04**: **Avec activité** depuis, l'affichage est « dernier exact **+ delta estimé** », **marqué avec sa
  marge d'incertitude** — jamais confondu avec un relevé exact.
- [x] **DEL-05**: Le **sous-système de plafonds disparaît** du code, des réglages et du menu (`BudgetCalibration`,
  `BudgetAutoCalibrator`, `BudgetSource`, `BudgetDialog` + VM, `IBudgetPrompt`/`BudgetPrompt`, l'entrée
  « Calibrer les plafonds… », les enregistrements DI et les tests associés).
- [x] **DEL-06**: Les réglages existants contenant d'anciens plafonds sont **migrés sans casse** : les champs
  obsolètes sont ignorés et les autres préférences (coin, écran, thème, style de cadran, widget sessions)
  survivent intactes.

### Source exacte par en-têtes de rate-limit (HDR)

- [ ] **HDR-01**: Chronos obtient l'usage exact via les en-têtes `anthropic-ratelimit-unified-*` d'une
  **requête jetable** (`POST /v1/messages`, `max_tokens:1`, modèle le moins cher).
- [ ] **HDR-02**: Les en-têtes sont exploités **même quand la réponse est un 429** — précisément l'instant où
  l'overlay sert le plus.
- [ ] **HDR-03**: Le **statut serveur** (`allowed` / `allowed_warning` / `rejected`) est remonté au cadran, au lieu
  d'être déduit d'un pourcentage.
- [ ] **HDR-04**: L'usage en **dépassement** (`overage`) est lu et affiché quand il est présent.
- [ ] **HDR-05**: Les **unités concurrentes** sont normalisées en un point unique : `utilization` 0..1 pour les
  en-têtes, 0..100 pour `/api/oauth/usage`, `used_percentage` 0..100 pour le pont statusLine ; `resets_at` en
  epoch secondes pour les deux premiers, ISO 8601 pour le troisième.
- [ ] **HDR-06**: La **cadence d'interrogation est bornée** et le **coût de la sonde** (une micro-requête par appel)
  est indiqué honnêtement dans les réglages.

### Cycle de vie du jeton (TOK)

- [x] **TOK-01**: Le jeton OAuth est **rafraîchi préventivement** avant expiration, sans attendre un échec au
  moment du besoin.
- [x] **TOK-02**: Un **échec d'authentification est visible** dans l'overlay — plus jamais un 401 muet pendant
  deux mois.
- [x] **TOK-03**: Le signal de déconnexion permet de **relancer le login en un clic**.

### Idempotence des intégrations (PUR)

- [x] **PUR-01**: L'installation des **hooks remplace** les entrées Chronos existantes au lieu de les cumuler
  (match sur `--hook`, pas sur le chemin d'exe).
- [x] **PUR-02**: L'installation du **pont statusLine remplace** l'entrée Chronos existante (match sur
  `--statusline`).
- [x] **PUR-03**: Les **entrées fantômes** déjà présentes dans `~/.claude/settings.json` sont **purgées**
  (constaté : 25 hooks Chronos au lieu de 5, pointant sur des exes de versions révolues).

## Future Requirements (différés)

- **Préavis avant saturation** (~90 %) et **notification au reset** — repris de `claude-session-browser` ;
  suppose d'ouvrir le canal notification, jusqu'ici hors périmètre.
- **Ventilation par modèle** (opus / sonnet / cowork) — dépend d'une source qui la publie.
- **Survol / tooltip**, tray, taille réglable.

## Out of Scope (v1.5)

- **Estimation absolue par tokens / plafond** — les limites Anthropic pondèrent par modèle : le calcul reste
  faux même avec le bon plafond. Remplacée par la correction par delta.
- **Calibration des plafonds (manuelle ou automatique)** — supprimée avec l'estimation absolue ; c'était la
  cause racine des pourcentages faux après un changement de forfait.
- **Détection ou saisie du forfait (Max x5 / x20)** — inutile dès lors que les chiffres viennent du serveur,
  qui connaît déjà le forfait. Ajouter un sélecteur reviendrait à réintroduire le problème.
- **Réanimation du pont statusLine pour l'app de bureau** — l'app de bureau ne semble pas rendre de statusLine ;
  le pont reste supporté pour le terminal, mais il n'est plus la voie principale.
- **Notifications Windows / toasts** — inchangé depuis v1.4, différé.

## Traceability

| REQ-ID | Phase | Statut |
|--------|-------|--------|
| EXA-01 | Phase 16 | Complete |
| EXA-02 | Phase 19 | Pending |
| EXA-03 | Phase 20 | Pending |
| EXA-04 | Phase 19 | Pending |
| EXA-05 | Phase 19 | Pending |
| EXA-06 | Phase 20 | Pending |
| DEL-01 | Phase 16 | Complete |
| DEL-02 | Phase 16 | Complete |
| DEL-03 | Phase 19 | Pending |
| DEL-04 | Phase 19 | Pending |
| DEL-05 | Phase 16 | Complete |
| DEL-06 | Phase 16 | Complete |
| HDR-01 | Phase 18 | Pending |
| HDR-02 | Phase 18 | Pending |
| HDR-03 | Phase 18 | Pending |
| HDR-04 | Phase 18 | Pending |
| HDR-05 | Phase 18 | Pending |
| HDR-06 | Phase 18 | Pending |
| TOK-01 | Phase 17 | Complete |
| TOK-02 | Phase 17 | Complete |
| TOK-03 | Phase 17 | Complete |
| PUR-01 | Phase 15 | Complete |
| PUR-02 | Phase 15 | Complete |
| PUR-03 | Phase 15 | Complete |

**Couverture :** 24 / 24 requirements mappés — 6 phases (15 → 20), aucun orphelin, aucun doublon.

| Phase | Requirements | Nombre |
|-------|--------------|--------|
| 15 — Idempotence des intégrations | PUR-01, PUR-02, PUR-03 | 3 |
| 16 — Fondations du delta (persistance & démolition des plafonds) | EXA-01, DEL-01, DEL-02, DEL-05, DEL-06 | 5 |
| 17 — Jeton toujours vivant, panne toujours visible | TOK-01, TOK-02, TOK-03 | 3 |
| 18 — Source exacte par en-têtes de rate-limit | HDR-01, HDR-02, HDR-03, HDR-04, HDR-05, HDR-06 | 6 |
| 19 — Nouvelle doctrine du composite | EXA-02, EXA-04, EXA-05, DEL-03, DEL-04 | 5 |
| 20 — Honnêteté visible (cadran & diagnostic) | EXA-03, EXA-06 | 2 |

---
*Last updated: 2026-09-09 — roadmap v1.5 créée : 24/24 requirements mappés sur les phases 15 à 20*
