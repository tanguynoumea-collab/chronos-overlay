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
- ✓ Installateurs de hooks et statusLine idempotents + purge des entrées fantômes (PUR-01..03) — Phase 15
- ✓ Dernier relevé exact persisté ; transcripts devenus source de DELTA ; sous-système de plafonds démoli (EXA-01, DEL-01..06) — Phase 16
- ✓ Autorité unique du jeton, rafraîchissement préventif, panne d'authentification visible et réparable (TOK-01..03) — Phase 17
- ✓ Source exacte par en-têtes `anthropic-ratelimit-unified-*`, exploitables même sur 429 (HDR-01..06) — Phase 18
- ✓ Doctrine « exact ou rien » : exact frais → encore exact → plancher « ≥ N % » → indisponible (EXA-02/04/05, DEL-03/04) — Phase 19
- ✓ Quatre états lisibles à l'œil + diagnostic nommant la source et son âge (EXA-03, EXA-06) — Phase 20
- ✓ Widget limité à Claude Code, source UI Automation retirée, transcripts robustes aux vagues de sous-agents (SRC-01..03) — Phase 21
- ✓ Diagnostic = même moniteur et mêmes filtres que le widget, sessions pertinentes d'abord (OBS-01, OBS-02) — Phase 22
- ✓ Écriture d'état sans perte silencieuse + balayage du magasin au démarrage (CYC-01, CYC-02) — Phase 23
- ✓ Arbitrage des sources par FRAÎCHEUR, désaccords tracés dans le diagnostic (FUS-01, FUS-02) — Phase 24
- ✓ Contrat d'événements : `PermissionRequest`, veto `Notification`, battements de cœur, attente déduite, `docs/hooks-contract.md` (EVT-01..05) — Phase 25
- ✓ « Traité » = transition observée sur la même source, persistant, geste explicite, archivage permanent (TRT-01..04) — Phase 26

### Active

<!-- Current scope. Building toward these. -->

Milestone v1.7 — « Lue ou non lue » : le widget de sessions ne montre que ce qui mérite un regard.
- **Source app-bureau par fichiers (APP)** : lire les métadonnées par session que l'app bureau Claude écrit
  (`%APPDATA%\Claude\claude-code-sessions\<org>\<user>\local_*.json` : `cliSessionId`, `title`,
  `lastFocusedAt`, `postTurnSummary`), joindre par UUID, dégrader vers le comportement v1.6 si absentes.
- **« Lue » (LUE)** : une session en attente dont la fin de tour est antérieure au dernier focus disparaît ;
  la session sélectionnée pendant que la fenêtre Claude est au premier plan compte comme lue.
- **Deux libellés (LIB)** : « Réflexion » et « En attente » (+ « En attente ? » pour la déduction) ; le titre de
  session remplace le nom de dossier ; une question `AskUserQuestion` en suspens est une attente, pas un travail.
- **Trou §9.1 (SIL)** : une session sans fichier de hook ne disparaît plus à 15 min sans un mot.

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
- **Source app-bureau via UI Automation pour le widget de sessions** — retirée en v1.6 : le widget ne couvre que Claude Code. Ses entrées `desktop:foreground:*` ne vieillissaient jamais et ne pouvaient donc jamais expirer ; l'utilisateur a dû les archiver à la main.
- **Hystérésis « traité » par focus de fenêtre** — supprimée avec l'UIA : elle exigeait `Origin == Desktop` et n'atteignait donc JAMAIS une session Claude Code. Remplacée par une transition observée et un geste explicite.

## Current State (entrée en v1.7 — 2026-09-25)

**Le cadran est réglé, et le widget observe.** v1.6 « Observer au lieu de déduire » est livré (exe **3.1.0**,
6 phases, 18 exigences, 752 → 889 tests) et **vérifié en production le 2026-09-25** : les 8 groupes de hooks
3.1.0 sont installés et se déclenchent depuis l'app bureau (fichier d'état écrit moins d'une seconde après un
appel d'outil), l'arbitrage par fraîcheur et le geste explicite sont en place.

**Mais le widget n'aide toujours pas**, et l'utilisateur le dit : « les widgets de sessions ne servent au final à
rien ». Relevé du 2026-09-25 à 16 h 08 : 4 sessions actives, 2 en cours, 2 « tour fini » en orange — les deux
avaient déjà été ouvertes et lues par l'utilisateur à 16 h 00 et 16 h 01. **Rien ne sait si une session a été
LUE** : toute session terminée reste en attente jusqu'au prochain prompt, à un clic droit, ou à 8 h.

**La source qui manquait existe.** L'app bureau Claude (paquet MSIX `Claude_pzs8sxrjxfjjc`) écrit un fichier
JSON par session sous `%APPDATA%\Claude\claude-code-sessions\<org>\<user>\local_<id>.json` (142 fichiers,
~275 Ko, réécrits au changement de session et en fin de tour). `%APPDATA%\Claude` est une jonction vers le
cache du paquet, lisible par un processus non packagé (vérifié). Champs utiles : `cliSessionId` (= l'UUID des
hooks et des transcripts), `title`, `lastFocusedAt`, `lastActivityAt`, `latestUserFrameAt`, `completedTurns`,
`postTurnSummary { status_category ∈ completed | blocked | review_ready, needs_action, status_detail }`.
Le titre `ai-title` des transcripts CLI n'existe pas dans ceux de l'app bureau (vérifié sur 4 sessions).
Format interne NON documenté : lecture tolérante, dégradation, jamais d'invention.

## Current Milestone: v1.7 — Lue ou non lue

**Goal :** le widget ne montre que les sessions qui méritent un regard — « Réflexion » ou « En attente » — et
une session lue disparaît d'elle-même, sans clic.

**Target features :**
- **Source app-bureau par fichiers** — un `ISessionSource` qui lit les métadonnées par session, joint par
  `cliSessionId`, expose titre, dernier focus et classification de fin de tour ; tolérant et dégradable.
- **Règle « lue »** — attente antérieure au dernier focus ⇒ traitée (via le magasin réversible existant) ;
  session sélectionnée + fenêtre Claude au premier plan ⇒ lue. Deux points à valider in vivo : le retour
  alt-tab sur la même session met-il `lastFocusedAt` à jour ? et le tour qui finit pendant qu'on regarde.
- **Deux libellés et le titre** — « Réflexion » / « En attente » (+ « En attente ? » pour la déduction),
  titre de session à la place du dossier, `AskUserQuestion` en suspens classée attente, `blocked` de l'app
  reconnu comme question posée.
- **Trou §9.1 refermé** — une session sans fichier de hook ne disparaît plus en silence à 15 min.

**Doctrine inchangée :** observé, jamais déduit ; une déduction porte son point d'interrogation.

## Next Milestone Goals (après v1.7)

Sous-fenêtres opus/sonnet/cowork, survol/tooltip, tray, taille réglable, préavis avant saturation du quota et
notification au reset. Piste d'économie à trancher : si `/api/oauth/usage` sert un jour la famille
`anthropic-ratelimit-unified-*`, les ≈ 288 micro-requêtes/jour de la sonde deviennent supprimables.

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
| Suppression de l'estimation absolue par tokens/plafond (v1.5) | Les limites Anthropic pondèrent par modèle : `tokens / plafond` reste faux même avec le bon plafond, et les plafonds sont propres au forfait | ✓ Good — v1.5 livré ; constaté en production le 2026-09-12 (5 h EXACT 23 %, sonde d'en-têtes) |
| Doctrine « exact ou rien » : dernier exact persisté + delta borné, jamais d'estimation absolue | Un chiffre exact vieux de 3 min vaut infiniment mieux qu'une estimation fausse de 400 % | ✓ Good — v1.5, 752 tests ; quatre états lisibles |
| Les transcripts JSONL deviennent un correcteur de delta, plus une source concurrente | Ils savent dire *si* et *combien* depuis un instant T ; ils ne savent pas dire un pourcentage absolu | ✓ Good — v1.5 |
| Source exacte supplémentaire par en-têtes `anthropic-ratelimit-unified-*` | Seule voie qui répond encore quand l'API renvoie 429, et qui expose statut serveur et dépassement | ✓ Good — v1.5 ; HDR-02 (429 réel) reste à constater |
| Le widget de sessions se limite aux sessions Claude Code (v1.6) | La source app-bureau produisait des entrées qui ne vieillissaient jamais, et l'hystérésis par focus qu'elle imposait n'atteignait structurellement jamais une session de terminal | ✓ Good — 1 294 lignes retirées, gardes anti-retour ; **v1.7 relit l'app bureau par FICHIERS, pas par UIA** |
| Les états de session sont OBSERVÉS, jamais déduits par expiration (v1.6) | Un état dont la source a expiré n'est pas « terminé », il est INCONNU. Même doctrine que « exact ou rien » appliquée au cadran en v1.5 | ✓ Good — v1.6, 889 tests, hooks vérifiés actifs en app bureau le 2026-09-25 |
| La fusion des sources arbitre par FRAÎCHEUR (v1.6) | L'ordre d'insertion laissait un signal de 7 h écraser un signal de 10 s — mesuré contre les classes réelles | ✓ Good — v1.6 |
| « Traité » = transition observée sur la MÊME source + geste explicite (v1.6) | Une expiration de source n'est pas une réponse ; le focus n'existe pas en terminal | ⚠️ Revisit — insuffisant seul : rien ne dit si l'utilisateur a LU (v1.7) |
| « Lue » lu dans les métadonnées par session de l'app bureau (v1.7) | `lastFocusedAt` est le seul signal de lecture qui existe ; format interne non documenté → tolérance + dégradation | — Pending |

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
*Last updated: 2026-09-25 — clôture du milestone v1.6, entrée en v1.7 « Lue ou non lue »*
