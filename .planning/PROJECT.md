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

### Active

<!-- Current scope. Building toward these. -->

Milestone v1.4 — Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code) — **✓ LIVRÉ & VALIDÉ en UAT app-réelle (2026-07-11)** :
- Source UIA « app bureau » : détecter l'état des sessions de l'app de bureau Claude (bosse / attend / attend-permission / indéterminé) via UI Automation, distinguer Chat/Code/Cowork, énumérer les sessions actives de la barre latérale (SESS-01..05).
- Auto-disparition hystérésis : une session traitée sort de la liste (répondu → en cours OU acquittée par focus ≥ ~2-3 s), réversible sur nouvel événement d'attente ; archivage manuel conservé (SESS-06..09).
- Robustesse & honnêteté : matching souple par libellé (fr/en), test de santé au démarrage, dégradation vers « indéterminé », lecture non bloquante pour le thread UI (SESS-10..11).


### Out of Scope

<!-- Explicit boundaries. Includes reasoning to prevent re-adding. -->

- Source de données Cowork séparée — le pool est partagé au niveau du compte, donc l'objet d'usage de Code inclut déjà Cowork
- Notifications Windows / toasts — l'alerte est purement visuelle (couleur + grisé)
- Dépendances de rendu natives (SkiaSharp, etc.) — arcs en XAML pur (Path/ArcSegment)
- Droits administrateur / modifications système — chemins sous profil utilisateur uniquement
- Comptage de tokens présenté comme exact contre des plafonds publiés — plafonds non documentés et mouvants
- ClickOnce / SharePoint — déploiement exe mono-fichier uniquement
- Bande d'activité des sous-agents (blocs Task JSONL) — optionnelle, différée après le cœur fonctionnel

## Current State (v1.4 — VALIDÉ en UAT app-réelle 2026-07-11)

Chronos affiche les quotas Claude (exact via OAuth, sinon estimé JSONL) dans un cadran compact 170 px à
fond transparent. Le widget **sessions** couvre désormais l'app de bureau Claude (Chat / Code / Cowork) via
UI Automation, avec noms, énumération individuelle, auto-disparition hystérésis et pastille ORANGE pour les
deux états d'attente. Le **cadran a deux modes** (Normal épuré par défaut / Étendu 3 anneaux, bascule dans les
réglages) : en normal, la timeline 24 h portée en anneau principal (couleur = usage 5 h) + sous-tirets
horaires alignés sur la grille des resets 5 h + marques de reset en blanc primaire. **327 tests verts.**
5 milestones (v1.0→v1.4), exe mono-fichier. Reste (au feu vert utilisateur) : versionner l'exe + push/ship ;
suivi mineur (durcir NET-02 contre le flicker du Kind au premier plan). Le cadran deux-modes a été livré en
direct (hors phases GSD), comme le widget sessions historique.

## Current Milestone: v1.4 — Intégration des sessions de l'app bureau Claude

**Goal :** Étendre le widget sessions (déjà livré hors GSD, « v2.5 » : détection des sessions Claude Code
par transcripts JSONL + hooks) pour couvrir aussi les sessions de l'**application de bureau** Claude —
Chat, Code et Cowork — via **UI Automation**, et faire **disparaître automatiquement** les sessions traitées.

**Target features :**
- Source UIA « app bureau » — états bosse / attend / attend-permission / indéterminé, distinction
  Chat/Code/Cowork, énumération des sessions actives de la barre latérale (spike prouvé le 2026-07-10 :
  voir mémoire `chronos-desktop-uia.md`).
- Auto-disparition hystérésis des sessions traitées (répondu **ou** acquittée par focus), réversible.
- Robustesse inter-versions (matching souple fr/en, test de santé, dégradation) + lecture non bloquante.

**Note de versioning :** le tracking GSD (milestones v1.0→v1.3) a divergé du versioning de l'app (« v2.5 »).
Le widget sessions a été construit hors GSD ; v1.4 le reprend sous tracking GSD.

## Next Milestone Goals (après v1.4)

Refresh du token OAuth, sous-fenêtres opus/sonnet/cowork, survol/tooltip, tray, taille réglable,
notification Windows en bonus du signal UIA (`UserNotificationListener`).

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
*Last updated: 2026-07-11 — v1.4 validé en UAT app-réelle + cadran deux modes (Normal/Étendu)*
