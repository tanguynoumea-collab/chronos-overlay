---
phase: 03-mod-les-pipeline-de-donn-es
plan: 04
subsystem: data-pipeline
tags: [node, esm, statusline, installer, idempotent, non-destructive, deployment, dat-04]

# Dependency graph
requires:
  - phase: 03-mod-les-pipeline-de-donn-es (plan 02)
    provides: "Pont Node non destructif chronos-statusline-bridge.js (materialise rate_limits dans %APPDATA%\\Chronos\\usage.json, re-emet gsd-statusline.js intact) + ClaudeUsageObjectProvider"
  - phase: 02-d-couverte-des-sources-bloquante
    provides: "docs/data-sources.md — contrainte non destructive (une seule commande statusLine), schema rate_limits"
provides:
  - "Installeur idempotent non destructif scripts/install-bridge.mjs : backup non ecrasant de ~/.claude/settings.json, detection/chainage de la commande existante, ecriture atomique de la SEULE cle statusLine, --uninstall reversible (DAT-04 deploiement)"
  - "Pont DEPLOYE dans ~/.claude/settings.json (statusLine.command -> chronos-statusline-bridge.js), verifie programmatiquement : usage.json alimente + statusLine gsd re-emise intacte"
  - "scripts/README.md : procedure install auto/manuelle + desinstall + securite (rate_limits seulement)"
  - "03-HUMAN-UAT.md : 3 tests de validation en session Claude Code reelle (rate_limits reels)"
affects: [phase-04-orchestration, phase-05-cadran-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Installeur ESM idempotent : no-op si deja ponte (marqueur chronos-statusline-bridge dans la commande), abandon si settings.json illisible (jamais d'ecriture destructrice)"
    - "Backup non ecrasant : .chronos.bak au 1er passage, suffixe timestamp si un backup existe deja"
    - "Ecriture atomique settings.json (temp + renameSync) touchant UNIQUEMENT la cle statusLine, preservant hooks et autres cles"
    - "Chainage non destructif verifie : le pont chaine gsd-statusline.js (CHILD_STATUSLINE) ; si la commande existante differe, avertir + abandonner (ou --force), jamais perdre la barre en place"
    - "Reversibilite : --uninstall restaure statusLine depuis le backup (repli sur CHILD_STATUSLINE si backup illisible)"

key-files:
  created:
    - scripts/install-bridge.mjs
    - scripts/README.md
    - .planning/phases/03-mod-les-pipeline-de-donn-es/03-HUMAN-UAT.md
  modified:
    - "~/.claude/settings.json (HORS repo : statusLine.command -> pont ; backup .chronos.bak cree)"

key-decisions:
  - "Auto-mode : checkpoint human-verify (Task 2) verifie PROGRAMMATIQUEMENT (install + rendu simule) au lieu d'attendre l'utilisateur ; les items exigeant une session reelle sont deportes dans 03-HUMAN-UAT.md"
  - "Le pont chaine gsd-statusline.js en dur (CHILD_STATUSLINE, plan 03-02) : l'installeur VERIFIE que la commande existante correspond a cet enfant avant d'installer, sinon avertit et abandonne (garde --force pour forcer)"
  - "Backup non ecrasant (timestamp) : garantit qu'on ne perd jamais l'etat d'origine meme apres plusieurs cycles install/uninstall"

patterns-established:
  - "Deploiement d'un pont hors-repo : backup obligatoire + ecriture atomique de la seule cle concernee + reversibilite par --uninstall + verification programmatique du rendu avant de dependre d'une session reelle"

requirements-completed: [DAT-04]

# Metrics
duration: 5min
completed: 2026-07-08
---

# Phase 3 Plan 04: Deploiement du pont statusLine (installeur idempotent) Summary

**Installeur ESM idempotent et non destructif qui branche le pont `chronos-statusline-bridge.js` dans `~/.claude/settings.json` (backup non ecrasant, chainage verifie de `gsd-statusline.js`, ecriture atomique de la seule cle `statusLine`, `--uninstall` reversible), puis deploiement effectif verifie programmatiquement : `usage.json` s'alimente et la statusLine existante est re-emise intacte.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-07-08T14:55:25Z
- **Completed:** 2026-07-08T15:00:01Z
- **Tasks:** 2 (Task 1 auto, Task 2 checkpoint auto-verifie)
- **Files:** 3 crees (installeur, README, UAT) + 1 modifie hors-repo (~/.claude/settings.json)

## Accomplishments

- **`scripts/install-bridge.mjs`** (Node ESM, idempotent, non destructif) : resout le pont via `import.meta.url` (independant du cwd, slashes avant), lit `~/.claude/settings.json` de facon tolerante (abandon propre si illisible, aucune ecriture), **sauvegarde** en `.chronos.bak` **sans jamais ecraser** un backup existant (suffixe timestamp), detecte les 3 cas de commande statusLine (deja pontee -> no-op ; autre commande -> chainage verifie ou avertissement/`--force` ; aucune -> pont seul), ecrit **atomiquement** (temp + `renameSync`) la **seule** cle `statusLine`, et fournit `--uninstall` qui restaure l'original depuis le backup.
- **`scripts/README.md`** (FR) : role du pont, install auto (`node scripts/install-bridge.mjs`) et manuelle, desinstall, verification, avertissement securite (le pont ne lit QUE `rate_limits`).
- **Deploiement effectif verifie** : le pont est installe dans `settings.json` (backup cree, `hooks` et autres cles preservees), l'idempotence, la reversibilite et le chainage non destructif sont prouves ; un rendu simule (JSON de test avec `rate_limits`) confirme que la sortie de `gsd-statusline.js` est **re-emise byte-for-byte** et que `usage.json` recoit les bonnes valeurs.
- **`03-HUMAN-UAT.md`** : 3 tests reportes vers une session Claude Code reelle (donnees `rate_limits` reelles, rendu visuel).

## Checkpoint auto-verifie (Task 2 — human-verify en mode autonome)

Mode autonome : au lieu d'attendre l'utilisateur, le checkpoint a ete verifie **programmatiquement**. Resultats mesures :

| Verification | Methode | Resultat |
|---|---|---|
| Sauvegarde prealable | `node scripts/install-bridge.mjs` | `~/.claude/settings.json.chronos.bak` cree (924 o), contenant la statusLine d'origine `gsd-statusline.js` |
| statusLine pointe sur le pont | lecture `settings.json` | `command = node "…/scripts/chronos-statusline-bridge.js"` |
| Autres cles preservees | lecture `settings.json` | `hooks` (SessionStart/PostToolUse/PreToolUse) intacts |
| Idempotence | relance de l'installeur | « deja installe » — aucune re-ecriture, exit 0 |
| (a) statusLine re-emise intacte | `diff` baseline `gsd-statusline.js` vs sortie du wrapper (meme JSON test) | **IDENTIQUE** (byte-for-byte, ANSI preserves) |
| (b) usage.json ecrit | rendu simule via le wrapper (JSON test `five_hour=67.8`, `seven_day=34.1`) | `usage.json` = valeurs exactes propagees + `capturedAt` rafraichi |
| Robustesse « avant 1re reponse » | stdin SANS `rate_limits` | statusLine re-emise + `five_hour/seven_day=null` (aucune invention) |
| Reversibilite | `--uninstall` puis re-`install` | statusLine restauree depuis backup, `hooks` preserves ; re-install cree un backup timestampe (non ecrasant) |

**Deporte en `03-HUMAN-UAT.md`** (non simulable hors-ligne) : remplissage de `usage.json` avec des `rate_limits` **reels** apres la 1re reponse API en session Pro/Max, rendu visuel de la barre dans le terminal, rafraichissement continu de `capturedAt`.

**Note importante** : la config `~/.claude/settings.json` **avait deja** une statusLine (`gsd-statusline.js`), correspondant exactement au `CHILD_STATUSLINE` code en dur dans le pont -> chainage direct, aucune adaptation « pont sans chainage » necessaire. L'etat final laisse le pont **deploye** (objectif DAT-04).

## Files Created/Modified

- `scripts/install-bridge.mjs` - Installeur idempotent non destructif (backup, detection, chainage verifie, ecriture atomique, --uninstall/--force)
- `scripts/README.md` - Procedure install auto/manuelle + desinstall + securite
- `.planning/phases/03-mod-les-pipeline-de-donn-es/03-HUMAN-UAT.md` - 3 tests de validation en session reelle
- `~/.claude/settings.json` (HORS repo) - `statusLine.command` -> pont ; backup `.chronos.bak` cree

## Decisions Made

- **Checkpoint verifie programmatiquement** (mode autonome) : install + rendu simule avec un JSON de test contenant `rate_limits`, plutot que d'attendre une confirmation interactive. Seuls les items exigeant une vraie session Claude Code sont deportes en UAT.
- **Chainage verifie plutot qu'aveugle** : l'installeur compare la commande statusLine existante au `CHILD_STATUSLINE` du pont ; il n'ecrase jamais silencieusement une commande differente (avertit + abandonne, ou `--force`).
- **Backup non ecrasant** (suffixe timestamp) : plusieurs cycles install/uninstall ne detruisent jamais le premier backup d'origine.

## Deviations from Plan

### Auto-fixed Issues

**Aucune deviation fonctionnelle.** Le plan a ete execute tel qu'ecrit. Deux precisions d'execution liees au mode autonome (prevues par les instructions de lancement, non des ecarts au plan) :

**1. [Mode autonome] Task 2 (checkpoint human-verify) verifiee programmatiquement**
- **Found during:** Task 2
- **Contexte:** Les instructions de lancement imposent de NE PAS bloquer sur le checkpoint mais de verifier programmatiquement (install reelle + rendu simule) et de deporter en UAT les items necessitant une session interactive.
- **Action:** Installation reelle effectuee (avec backup prealable), rendu simule verifie (statusLine intacte + usage.json ecrit), `03-HUMAN-UAT.md` cree.

**2. [Ajout scope planifie] Ajout de `--force` a l'installeur**
- **Found during:** Task 1
- **Issue:** Le plan decrit le cas « commande existante differente » comme devant afficher un avertissement et demander une edition manuelle. Pour rester utilisable sans edition du pont, un flag `--force` a ete ajoute (opt-in explicite) permettant d'ecraser en connaissance de cause.
- **Fix:** Flag `--force` documente (installeur + README), sans changer le comportement par defaut (avertir + abandonner).
- **Files modified:** scripts/install-bridge.mjs, scripts/README.md
- **Committed in:** `1e8e0f3`

---

**Total deviations:** 0 ecart fonctionnel au plan (1 verification en mode autonome, 1 ajout mineur opt-in `--force`).
**Impact on plan:** Nul sur les livrables ; DAT-04 (deploiement non destructif + idempotent + reversible) satisfait et verifie.

## Issues Encountered

None. La config disposait deja de la statusLine `gsd-statusline.js` (exactement l'enfant chaine par le pont), rendant le chainage direct. Le rendu simule a valide de bout en bout la re-emission intacte + l'ecriture de `usage.json`.

## User Setup Required

Effectue automatiquement (mode autonome) : le pont est **installe** dans `~/.claude/settings.json` (backup `.chronos.bak` present). Reste a la charge de l'utilisateur : **valider en session Claude Code reelle** (voir `03-HUMAN-UAT.md`) que `usage.json` se remplit avec des `rate_limits` reels apres la 1re reponse. Reversible a tout moment : `node scripts/install-bridge.mjs --uninstall`.

## Next Phase Readiness

- Source primaire **deployee** de bout en bout : `rate_limits` -> pont -> `%APPDATA%\Chronos\usage.json` -> `ClaudeUsageObjectProvider` (Exact). Prete pour la Phase 4 (orchestration/composition) et la Phase 5 (cadran UI).
- Reversibilite garantie (backup + `--uninstall`), aucune modification du repo hors `scripts/` et `.planning/`.

## Self-Check: PASSED

- Fichiers crees presents : scripts/install-bridge.mjs, scripts/README.md, 03-HUMAN-UAT.md (verifies en fin de plan).
- Commits presents : 1e8e0f3 (Task 1), 534c958 (Task 2 UAT).
- Deploiement verifie : settings.json pointe sur le pont, backup present, rendu simule vert (statusLine intacte + usage.json alimente).

---
*Phase: 03-mod-les-pipeline-de-donn-es*
*Completed: 2026-07-08*
