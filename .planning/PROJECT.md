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

### Active

<!-- Current scope. Building toward these. -->

Milestone v1.6 — Observer au lieu de déduire : le widget de sessions.
- **Périmètre (SRC)** : le widget ne couvre plus que les sessions **Claude Code** ; la source app-bureau
  (UI Automation) est retirée, et la source transcripts cesse de s'aveugler pendant les vagues de sous-agents.
- **Contrat d'événements (EVT)** : `PermissionRequest` au lieu du proxy `Notification`, battements de cœur
  qui font **observer** « réfléchit » au lieu de le déduire par expiration, cas de l'interruption utilisateur
  couvert, et contrat des hooks enfin documenté.
- **Fusion (FUS)** : arbitrage par **fraîcheur**, jamais par ordre d'insertion ; désaccords traçables.
- **« Traité » (TRT)** : déduit d'une transition observée sur la MÊME source, jamais d'une expiration ;
  survit au redémarrage ; geste explicite pour l'utilisateur ; contrat d'archivage unique.
- **Cycle de vie (CYC)** : balayage des états expirés et des `.tmp` orphelins ; plus d'écriture perdue en silence.
- **Observabilité (OBS)** : le diagnostic dit **exactement** ce que le widget affiche.

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

## Current State (entrée en v1.6 — 2026-09-12)

**Le cadran est réglé.** Le milestone v1.5 « Exactitude permanente » est livré : 6 phases, 24 exigences,
328 → 752 tests verts. L'estimation absolue `tokens / plafond` est supprimée et gardée morte ; le dernier
relevé exact est persisté ; une sonde d'en-têtes `anthropic-ratelimit-unified-*` alimente le cadran et répond
même en saturation ; le jeton est rafraîchi préventivement et sa panne est visible. Constaté en production le
2026-09-12 après reconnexion : `5 h : EXACT — 23 % · source : sonde d'en-têtes · frais · serveur : AUTORISÉ`.
Exe publié en **3.0.2**.

**Le widget de sessions, lui, ne l'est pas.** Investigation menée le même jour
(`.planning/debug/widget-sessions-statuts.md`) : trois causes racines distinctes, prouvées contre les classes
réelles et contre la documentation officielle des hooks. Le widget **déduit** des états au lieu de les
**observer** — exactement le défaut que v1.5 vient de corriger sur le cadran. « Réfléchit » est deviné par
expiration et se fait écraser par des signaux de 7 h ; « attend » repose sur une sémantique d'événements qui a
dérivé (`Stop` ne se déclenche pas sur interruption, `Notification` est une alerte d'absence) ; « traité » ne
peut structurellement pas fonctionner pour une session de terminal. Et l'instrument de mesure lui-même était
faussé : le diagnostic construisait son propre moniteur nu, sans les filtres du widget — ce qui explique
probablement pourquoi le problème n'a jamais été élucidé.

## Current Milestone: v1.6 — Observer au lieu de déduire

**Goal :** Le widget de sessions répond enfin, de façon fiable, à « quelle session m'attend ? » — en
**observant** les trois états au lieu de les déduire, et sur le seul périmètre des sessions Claude Code.

**Target features :**
- **Périmètre réduit à Claude Code** — retrait de la source app-bureau (UI Automation, ~690 lignes) et de
  l'hystérésis par focus qui en dépendait. Les entrées fantômes `desktop:foreground:*`, qui ne vieillissaient
  jamais, disparaissent avec elle.
- **Un contrat d'événements refondé** — `PermissionRequest` (signal exact, aujourd'hui inutilisé) au lieu du
  proxy `Notification` ; des battements de cœur pour que « réfléchit » soit **observé** ; le cas de
  l'interruption utilisateur enfin couvert ; le contrat documenté dans `docs/`, comme les autres sources.
- **Une fusion qui arbitre par fraîcheur** — aujourd'hui un hook de 7 h écrase un transcript de 10 secondes,
  par simple ordre d'insertion dans un dictionnaire.
- **Un « traité » qui veut dire quelque chose** — déduit d'une transition **observée sur la même source**,
  jamais d'une expiration de source ; persistant au redémarrage ; et assorti d'un geste explicite, puisque le
  focus ne peut pas le fournir en terminal.
- **Un magasin qui ne croît plus indéfiniment** — balayage des états expirés et des `.tmp` orphelins, puisque
  `SessionEnd` ne couvre ni terminal tué, ni crash, ni redémarrage machine.
- **Un instrument de mesure honnête** — le diagnostic dit exactement ce que le widget affiche.

**Doctrine héritée de v1.5, appliquée au widget :** ne jamais présenter comme un fait ce qui n'a pas été
observé. Un état dont la source a expiré n'est pas « terminé » : il est **inconnu**.

## Next Milestone Goals (après v1.6)

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
| Suppression de l'estimation absolue par tokens/plafond (v1.5) | Les limites Anthropic pondèrent par modèle : `tokens / plafond` reste faux même avec le bon plafond, et les plafonds sont propres au forfait | À valider — remplacée par la correction par delta |
| Doctrine « exact ou rien » : dernier exact persisté + delta borné, jamais d'estimation absolue | Un chiffre exact vieux de 3 min vaut infiniment mieux qu'une estimation fausse de 400 % | À valider — refonte du composite en v1.5 |
| Les transcripts JSONL deviennent un correcteur de delta, plus une source concurrente | Ils savent dire *si* et *combien* depuis un instant T ; ils ne savent pas dire un pourcentage absolu | À valider — v1.5 |
| Source exacte supplémentaire par en-têtes `anthropic-ratelimit-unified-*` | Seule voie qui répond encore quand l'API renvoie 429, et qui expose statut serveur et dépassement | À valider — reprise de claude-session-browser, v1.5 |
| Le widget de sessions se limite aux sessions Claude Code (v1.6) | La source app-bureau produisait des entrées qui ne vieillissaient jamais, et l'hystérésis par focus qu'elle imposait n'atteignait structurellement jamais une session de terminal | À valider — retrait de ~690 lignes |
| Les états de session sont OBSERVÉS, jamais déduits par expiration (v1.6) | Un état dont la source a expiré n'est pas « terminé », il est INCONNU. Même doctrine que « exact ou rien » appliquée au cadran en v1.5 | À valider |
| La fusion des sources arbitre par FRAÎCHEUR (v1.6) | L'ordre d'insertion laissait un signal de 7 h écraser un signal de 10 s — mesuré contre les classes réelles | À valider |

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
*Last updated: 2026-09-12 — démarrage du milestone v1.6 « Observer au lieu de déduire »*
