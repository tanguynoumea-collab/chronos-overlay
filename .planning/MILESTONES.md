# MILESTONES.md — Chronos

Historique des versions livrées. Le détail de chaque milestone (roadmap, exigences, audit) vit dans
`.planning/milestones/` et `.planning/v*-MILESTONE-AUDIT.md`.

---

## v1.7 — Lue ou non lue (widget de sessions) — SHIPPED 2026-09-26 (clos le 2026-09-27)

**Exe :** Chronos-v3.2.1 (release `b981e41`), après 3.2.0 (`f321180`) ; réconciliation des hooks et de la statusLine constatée
dans `~/.claude/settings.json` pour les deux.
**Phases :** 27 → 31 (+ 30.1 insérée) (6) · **Plans :** 20 · **Exigences :** 20 / 21 (VAL-03 partielle) · **Tests :** 896 → **1200** verts, 0 échec.
**Git :** 125 commits (`0195c26` → `10bf4d9`), du 2026-09-25 16:39 au 2026-09-26.

**Livré :** le widget ne montre plus que ce qui mérite un regard — « Réflexion » ou « En attente » —, une session **lue**
disparaît d'elle-même, le titre de session remplace le dossier, et un sous-agent qui écrit est un travail de sa session.

**Accomplissements :**

1. **Le relevé avant la règle** (phase 27) — deux gestes joués par l'utilisateur ont fixé la sémantique de `lastFocusedAt`
   (le retour alt-tab le met à jour ; un tour fini sous les yeux le laisse antérieur ⇒ LUE-02 nécessaire).
2. **Deux mots, une question, les mêmes horizons** (phase 28) — « Réflexion » / « En attente » / « En attente ? » d'un seul
   producteur ; `AskUserQuestion` en suspens classée attente ; horizons uniques 20 min / 8 h pour toutes les sources.
3. **Ce que l'app bureau sait de chaque session** (phase 29) — source par fichiers en lecture seule (titre, dernier focus,
   `blocked`), jointe par UUID ; découverte de la **virtualisation AppData de MSIX** et lecture des deux vues par candidats.
4. **La lecture fait disparaître** (phase 30) — attente antérieure au dernier focus, ou session sélectionnée avec la fenêtre
   Claude au premier plan (grâce 2,5 s) ⇒ masquée, cause écrite au diagnostic ; une session qui travaille reste visible.
5. **Un sous-agent qui écrit est un travail de sa session** (phase 30.1, insérée sur l'écart E2 du constat) — battements
   et transcripts des sous-agents ⇒ « Réflexion » du parent, sans jamais effacer une attente ; contrat sous garde.
6. **Écrit, publié** (phase 31) — `docs/desktop-app-sessions.md` sous garde croisée (14 champs) ; exe 3.2.0 puis 3.2.1
   publiés, version embarquée et réconciliation constatées dans le fichier.

### Known Gaps

- **VAL-03, critère 4 — constat en production PARTIEL.** Le point (a) (une seule instance) n'a jamais été atteint : les
  exécutables 3.1.0, 3.2.0 et 3.2.1 tournaient encore ensemble le 2026-09-27 (aucun verrou mono-instance). Le tableau des
  gestes (L1, L2, L2b, L3, L4, Q) et les 12 vérifications déférées ne sont pas joués. Verdict PARTIEL écrit dans
  `.planning/phases/31-crit-publi-constat/31-CONSTAT.md` §4 ; **reporté en tête de la phase 32 (v1.8)**, sur la 3.2.2.
- Découverts à la clôture (conseil du 2026-09-26), traités en v1.8 : `last-exact.json` non écrit depuis le 2026-09-13 malgré des
  relevés exacts frais ; le parser de tokens compte 2 à 2,75× (lignes `assistant` dupliquées par bloc de contenu).

**Détail :** [roadmap](.planning/milestones/v1.7-ROADMAP.md) · [exigences](.planning/milestones/v1.7-REQUIREMENTS.md) ·
rétrospective dans `.planning/RETROSPECTIVE.md`.

---

## v1.6 — Observer au lieu de déduire (widget de sessions) — SHIPPED 2026-09-13

**Exe :** Chronos-v3.1.0 (commit `cd26b31`, en production chez l'utilisateur depuis le 2026-09-23).
**Phases :** 21 → 26 (6) · **Plans :** 19 · **Exigences :** 18 / 18 · **Tests :** 752 → **889** verts, 0 échec.
**Git :** 110 commits (`3a66b82` → `cd26b31`), 123 fichiers, +30 192 / −2 485 lignes, du 2026-09-12 15:47 au
2026-09-13 02:13.

**Livré :** le widget de sessions **observe** ses trois états au lieu de les déduire, sur le seul périmètre des
sessions Claude Code, et le diagnostic dit exactement ce que le widget affiche.

**Accomplissements :**

1. **Périmètre réduit à Claude Code** (phase 21) — la source app-bureau par UI Automation (1 294 lignes) a quitté
   le dépôt, `SessionSnapshot` est revenu à cinq champs, deux gardes rendent le retour en arrière rouge au build ;
   la source transcripts ne s'aveugle plus pendant les vagues de sous-agents (limite appliquée APRÈS le filtre).

2. **Un instrument de mesure honnête** (phase 22) — le rapport de diagnostic lit LE moniteur du conteneur DI, le
   même que le widget ; chaque session masquée est nommée avec son filtre ; l'ordre du widget vit dans un seul
   type neutre `AffichageSessions`.

3. **Un magasin fiable** (phase 23) — écriture directe sans perte (200 écritures sous lecteur concurrent, 0 perte),
   cause rendue au lieu d'avalée, balayage au démarrage des `.tmp` (> 1 h) et des états morts (> 72 h).

4. **L'arbitrage par fraîcheur** (phase 24) — un transcript de 10 s bat un hook de 7 h : `ArbitrageSessions`
   impose un ordre total, identique pour les 720 permutations de son corpus ; les désaccords sont tracés.

5. **Le contrat d'événements refondé** (phase 25) — `PermissionRequest` câblé, `Notification` filtrée par matcher
   (3 types) et veto (9 types), battements `PreToolUse`/`PostToolUse`, attente DÉDUITE dérivée à la lecture après
   20 min de silence, `docs/hooks-contract.md` tenu par une garde de non-dérive.

6. **« Traité » veut dire quelque chose** (phase 26) — transition observée sur la MÊME source, épisode daté par le
   signal (survit au redémarrage), geste explicite « Marquer traitée » / « Tout marquer traité » sur les 8 styles,
   archivage réellement permanent.

**Dettes et trous assumés (audit du 2026-09-13) :**

- Une session **sans fichier de hook** disparaît 15 min après la dernière écriture de son transcript au lieu
  de devenir « à toi ? déduit » (audit §9.1). Repris en v1.7.

- **R3** : un battement pouvait écraser un « à toi » observé (un seul état par fichier, aucune comparaison
  d'horodatage à l'écriture). Cran 1 livré après l'audit (écriture MONOTONE, MON-01, commit `5523be7`) ; la
  suite du chemin est laissée ouverte (`99ed0e3`).

- Aucune notion de « **lue** » : une session terminée reste en attente jusqu'au prochain prompt, à un clic droit
  ou à 8 h. C'est l'objet du milestone v1.7.

---

## v1.5 — Exactitude permanente — SHIPPED 2026-09-12

6 phases (15-20), 28 plans, 24 / 24 exigences, 328 → 752 tests. Exe 3.0.2. Estimation absolue supprimée,
dernier exact persisté, sonde d'en-têtes `anthropic-ratelimit-unified-*`, jeton rafraîchi préventivement et
panne d'authentification visible. — [roadmap](.planning/milestones/v1.5-ROADMAP.md) ·
[exigences](.planning/milestones/v1.5-REQUIREMENTS.md) · [audit](.planning/v1.5-MILESTONE-AUDIT.md)

## v1.4 — Intégration des sessions de l'app bureau Claude — SHIPPED 2026-07-11

2 phases (13-14), 5 plans. Source UI Automation et hystérésis par focus — **retirées en v1.6**.
— [roadmap](.planning/milestones/v1.4-ROADMAP.md) · [audit](.planning/v1.4-MILESTONE-AUDIT.md)

## v1.3 — Refonte du cadran — SHIPPED 2026-07-09

1 phase (12). — [roadmap](.planning/milestones/v1.3-ROADMAP.md)

## v1.2 — Usage exact via l'endpoint OAuth — SHIPPED 2026-07-09

2 phases, 4 plans. — [roadmap](.planning/milestones/v1.2-ROADMAP.md)

## v1.1 — Estimation utile en mode app bureau — SHIPPED 2026-07-09

2 phases, 5 plans. — [roadmap](.planning/milestones/v1.1-ROADMAP.md)

## v1.0 — Overlay de quotas Claude complet — SHIPPED 2026-07-08

7 phases, 18 plans. — [roadmap](.planning/milestones/v1.0-ROADMAP.md) · [audit](.planning/v1.0-MILESTONE-AUDIT.md)
