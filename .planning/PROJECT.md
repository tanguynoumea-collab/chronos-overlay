# Chronos

## What This Is

Chronos est un overlay Windows always-on-top en forme d'horloge, posé sur le bureau,
qui affiche en temps réel — d'un coup d'œil — l'état des limites d'usage Claude
(fenêtre 5 h glissante + fenêtre hebdomadaire) pour Claude Code et Cowork.
Le cadran encode deux variables par anneau : longueur d'arc = temps restant avant reset,
couleur = pourcentage de quota consommé. Pour un utilisateur intensif de Claude qui veut
savoir sans y penser combien de marge il lui reste avant d'être bloqué.

## Core Value

Voir instantanément, sans ouvrir de terminal ni taper `/usage`, combien de quota et de
temps il reste sur les deux fenêtres — et ne jamais présenter une estimation comme un
chiffre exact.

## Requirements

### Validated

<!-- Shipped and confirmed valuable. -->

- ✓ Fenêtre WPF borderless, transparente, always-on-top, sans barre des tâches (FEN-01) — Phase 1
- ✓ Topmost réaffirmé périodiquement sans vol de focus (ROB-04) — Phase 1
- ✓ Abstraction IUsageProvider + modèles UsageSnapshot immuables neutres (DAT-02, DAT-03) — Phase 3
- ✓ Provider primaire via pont statusLine→usage.json installé (DAT-04) — Phase 3
- ✓ Provider de repli JSONL honnête, utilization=null jamais inventée (DAT-05) — Phase 3
- ✓ Provider composite avec bascule par fenêtre (DAT-06) — Phase 3
- ✓ FractionTimeRemaining clampé depuis ResetsAt (DAT-07) — Phase 3
- ✓ Parsing tolérant (ROB-02) — Phase 3
- ✓ Rafraîchissement watcher débouncé + PeriodicTimer + tick 1 s sans I/O + marshaling unique (RAF-01..04) — Phase 4
- ✓ Cadran complet : graduations, deux arcs temps/couleur, gris épuisé, countdown, badges « estimée » par fenêtre, état indisponible (CAD-01..07, DAT-08, ROB-01) — Phase 5
- ✓ Drag + snap coins multi-écrans, menu contextuel, arrière-plan, persistance settings.json, recalibrage hebdo, autostart (FEN-02..07, ROB-03, DEP-02) — Phase 6
- ✓ Exe self-contained mono-fichier win-x64 74 Mo publié et smoke-testé (DEP-01) — Phase 7
- ✓ Source UIA app bureau : états honnêtes, distinction Chat/Code/Cowork, énumération de la sidebar (SESS-01..05) — Phase 13
- ✓ Auto-disparition hystérésis réversible + archivage manuel conservé (SESS-06..09) — Phase 14
- ✓ Matching souple fr/en, test de santé, dégradation, lecture non bloquante (SESS-10..11) — Phases 13-14

### Active

<!-- Current scope. Building toward these. -->

Milestone v1.5 — Exactitude permanente : éradiquer la bascule silencieuse en estimation pure.
- **Exactitude & doctrine d'affichage (EXA)** : persistance disque du dernier relevé exact, limite d'âge sur
  toute source exacte, distinction visuelle frais / daté / indisponible, plus jamais d'utilization absolue
  dérivée d'un comptage de tokens.
- **Correction par delta (DEL)** : les transcripts JSONL ne produisent plus de pourcentage absolu mais
  répondent à « activité depuis T ? » et « tokens depuis T ? » ; suppression complète du sous-système de plafonds.
- **Source exacte par en-têtes de rate-limit (HDR)** : requête jetable `max_tokens:1` + lecture des en-têtes
  `anthropic-ratelimit-unified-*`, exploitables même sur un 429, avec statut serveur et dépassement.
- **Cycle de vie du jeton (TOK)** : rafraîchissement préventif, panne d'authentification visible et réparable en un clic.
- **Idempotence des intégrations (PUR)** : installateurs de hooks et de statusLine qui remplacent au lieu de cumuler.

### Out of Scope

<!-- Explicit boundaries. Includes reasoning to prevent re-adding. -->

- Source de données Cowork séparée — le pool est partagé au niveau du compte, donc l'objet d'usage de Code inclut déjà Cowork
- Notifications Windows / toasts — l'alerte est purement visuelle (couleur + grisé)
- Dépendances de rendu natives (SkiaSharp, etc.) — arcs en XAML pur (Path/ArcSegment)
- Droits administrateur / modifications système — chemins sous profil utilisateur uniquement
- Comptage de tokens présenté comme exact contre des plafonds publiés — plafonds non documentés et mouvants
- ClickOnce / SharePoint — déploiement exe mono-fichier uniquement
- Bande d'activité des sous-agents (blocs Task JSONL) — optionnelle, différée après le cœur fonctionnel
- **Estimation absolue par tokens / plafond** — retirée en v1.5 : les limites Anthropic pondèrent par modèle, donc `tokens / plafond` reste faux même avec le bon plafond. Les transcripts ne servent plus qu'à corriger un delta borné.
- **Calibration des plafonds (manuelle ou automatique)** — supprimée avec l'estimation absolue ; c'était la cause racine des pourcentages faux après un changement de forfait
- **Détection ou saisie du forfait (Max x5 / x20)** — inutile dès lors que les chiffres viennent du serveur, qui connaît déjà le forfait

## Current State (entrée en v1.5 — 2026-09-09)

Chronos affiche les quotas Claude dans un cadran compact à fond transparent, avec deux modes (Normal épuré /
Étendu 3 anneaux) et 3 thèmes, plus un widget sessions couvrant Claude Code et l'app de bureau (UIA). Exe
mono-fichier v2.8.1. **Mais le pipeline d'usage est en panne silencieuse** : les trois sources exactes sont
tombées simultanément (jeton OAuth expiré le 2026-07-12 → HTTP 401 muet ; `usage.json` figé au 2026-07-10 mais
toujours servi comme `Exact` faute de limite d'âge ; cache exact en RAM seulement, donc perdu à chaque
redémarrage), et le composite classe uniquement par fiabilité — une donnée « exacte » de deux mois bat une
estimation fraîche. Le repli divise une somme de tokens par des plafonds calibrés sous l'ancien forfait Max x5,
d'où des pourcentages faux d'environ 4× depuis le passage en Max x20. `IsStale` est calculé mais bindé nulle
part : rien ne le signale à l'écran. Effet de bord découvert au passage : 25 hooks Chronos cumulés dans
`~/.claude/settings.json` au lieu de 5, pointant sur des exes de versions révolues.

## Current Milestone: v1.5 — Exactitude permanente

**Goal :** Éradiquer la bascule silencieuse en estimation pure — Chronos n'affiche plus jamais qu'un chiffre
exact, éventuellement corrigé d'un delta borné et marqué comme tel, ou rien du tout.

**Target features :**
- **Démolition du sous-système de plafonds** — `BudgetCalibration`, `BudgetAutoCalibrator`, `BudgetSource`, le
  dialogue de calibration, l'entrée de menu et les 6 champs de `settings.json` disparaissent. C'est ce qui rend
  le bug de forfait structurellement impossible.
- **`JsonlEstimationProvider` transformé en source de delta** — plus d'`Utilization` absolue ; il répond à
  « y a-t-il eu une réponse assistant depuis T ? » et « combien de tokens depuis T ? », et sort du composite
  comme source concurrente.
- **Persistance disque du dernier relevé exact** — tue la bascule au redémarrage de l'exe, le cas le plus
  fréquent et jamais identifié jusqu'ici.
- **Nouvelle doctrine du composite** — exact frais → dernier exact persisté *si aucune activité depuis* (auquel
  cas il est encore rigoureusement exact) → dernier exact + delta borné avec sa marge → indisponible.
- **Source exacte par en-têtes de rate-limit** — `POST /v1/messages` (`max_tokens:1`, Haiku) puis lecture des
  en-têtes `anthropic-ratelimit-unified-*`. Technique reprise de `github.com/juppeee/claude-session-browser`
  (`clawdmeter.py:150`). Elle répond **même sur un 429**, et apporte le statut serveur et le dépassement.
- **Jeton toujours vivant, panne toujours visible** — rafraîchissement préventif au lieu du refresh paresseux,
  pastille de déconnexion cliquable. Un 401 ne sera plus muet.
- **Installateurs idempotents** — hooks et statusLine remplacent au lieu de cumuler, et purgent les entrées
  fantômes existantes.

**Note de versioning :** le tracking GSD (v1.0→v1.4) est découplé du versioning de l'exe (2.8.1). Ce milestone
est **v1.5** côté GSD ; l'ampleur du changement de doctrine justifiera vraisemblablement une **v3.0** côté exe.

## Next Milestone Goals (après v1.5)

Sous-fenêtres opus/sonnet/cowork, survol/tooltip, tray, taille réglable, notification Windows en bonus du
signal UIA (`UserNotificationListener`), préavis avant saturation du quota et notification au reset.

## Context

- **Écosystème :** app desktop Windows mono-utilisateur, .NET 8 / WPF / MVVM. SDK .NET 10 installé sur la machine, cible net8.0.
- **Sources non documentées :** il n'existe aucune API publique pour ces données. On lit des sources locales et non documentées, d'où l'abstraction IUsageProvider — si une source casse à une MAJ de l'app Claude, on remplace le provider sans toucher au cadran.
- **Emplacement de l'objet d'usage à découvrir :** les champs five_hour/seven_day (utilization + resets_at) alimentent la commande `/usage`. Leur mécanisme d'obtention exact est une tâche de découverte à faire en tout début de projet, documentée dans docs/data-sources.md, AVANT de coder les providers.
- **Repli JSONL :** %USERPROFILE%\.claude\projects\**\*.jsonl — somme des tokens dans la fenêtre → estimation, toujours marquée comme telle dans l'UI.
- **Sous-agents (secondaire) :** blocs Task (tool_use) parsés dans les JSONL en direct, pour une bande d'activité optionnelle.
- **Reset hebdo dérivant :** le reset « 7 jours » dérive en pratique (~72 h à un horaire d'ancrage fixe non documenté). Afficher resets_at tel que fourni, traiter le compte à rebours hebdo comme best-effort, le rendre facile à recalibrer.
- **Plafonds mouvants :** ×2 le 6 mai, +50 % hebdo jusqu'au 13 juillet 2026 — raison de privilégier utilization/resets_at sur le comptage de tokens.

### Tokens de design (validés sur maquette)

- Fond cadran `#16151B`, rim `#2C2B34`
- Ticks : mineurs `#34333D`, majeurs `#46454F`
- Arc 5 h (piste) `#2A2932`, arc hebdo (piste) `#26252E`
- Rampe utilization : vert `#7BB13C` (0 %) → ambre `#EFA23A` → rouge `#D8503A` (~100 %) → gris `#5A5960` (épuisé)
- Texte principal `#F4F2EC`, secondaire `#A9A8B2` / `#C7C6D0`

## Constraints

- **Tech stack**: C# / .NET 8 / WPF / MVVM (CommunityToolkit.Mvvm) + Microsoft.Extensions.DependencyInjection — imposé.
- **Rendu**: arcs en XAML pur (Path/ArcSegment), aucune dépendance native — portabilité et simplicité de packaging.
- **Fenêtre**: WindowStyle=None, AllowsTransparency=True, Topmost=True, ShowInTaskbar=False — comportement overlay exigé.
- **Chemins**: uniquement sous %USERPROFILE% / %APPDATA%, aucun droit admin — contrainte de sécurité/déploiement.
- **Honnêteté des chiffres**: utilization/resets_at prioritaires ; ne jamais présenter une estimation comme exacte — confiance utilisateur.
- **Robustesse**: aucune source disponible ≠ crash → état « données indisponibles » ; parsing tolérant (lignes/champs invalides ignorés).
- **Langue**: UI et commentaires en français.
- **Déploiement**: exe self-contained mono-fichier win-x64 + autostart shell:startup, sans ClickOnce.

## Key Decisions

<!-- Decisions that constrain future work. Add throughout project lifecycle. -->

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Abstraction IUsageProvider entre sources et cadran | Sources locales non documentées susceptibles de casser ; isoler le point de rupture | ✓ Good — Phase 3 |
| Objet d'usage (utilization/resets_at) en source primaire, JSONL en repli | Chiffres fiables prioritaires sur estimation par tokens | ✓ Good — composite livré ; repli n'invente jamais d'utilization (null) |
| Découverte de source (docs/data-sources.md) avant de coder les providers | Tout le pipeline données en dépend | ✓ Good — source localisée (Phase 2) |
| Source primaire = bloc rate_limits du contrat statusLine (officiel), via pont statusLine→fichier | Rien n'est persisté sur disque ; le champ réel est used_percentage (0-100) et resets_at en epoch secondes | ✓ Good — pont installé avec backup (Phase 3) |
| Rendu des arcs en XAML pur | Éviter dépendance native, simplifier le packaging mono-fichier | ✓ Good — Phase 5 |
| Pas de source Cowork séparée | Pool partagé compte : Cowork déjà inclus dans l'usage de Code | ✓ Good |
| Reset hebdo traité comme best-effort recalibrable | Le reset 7 jours dérive (~72 h, ancrage non documenté) | ✓ Good — recalibrage livré Phase 6 |
| Suppression de l'estimation absolue par tokens/plafond (v1.5) | Les limites Anthropic pondèrent par modèle : `tokens / plafond` reste faux même avec le bon plafond, et les plafonds sont propres au forfait | À valider — remplacée par la correction par delta |
| Doctrine « exact ou rien » : dernier exact persisté + delta borné, jamais d'estimation absolue | Un chiffre exact vieux de 3 min vaut infiniment mieux qu'une estimation fausse de 400 % | À valider — refonte du composite en v1.5 |
| Les transcripts JSONL deviennent un correcteur de delta, plus une source concurrente | Ils savent dire *si* et *combien* depuis un instant T ; ils ne savent pas dire un pourcentage absolu | À valider — v1.5 |
| Source exacte supplémentaire par en-têtes `anthropic-ratelimit-unified-*` | Seule voie qui répond encore quand l'API renvoie 429, et qui expose statut serveur et dépassement | À valider — reprise de claude-session-browser, v1.5 |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd:transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-09-09 — démarrage du milestone v1.5 « Exactitude permanente »*
