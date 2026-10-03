# Chronos

## What This Is

Chronos est un overlay Windows always-on-top en forme d'horloge, posé sur le bureau,
qui affiche en temps réel — d'un coup d'œil — l'état des limites d'usage Claude
(fenêtre 5 h glissante + fenêtre hebdomadaire) pour Claude Code et Cowork.
Le cadran encode deux variables par anneau : longueur d'arc = temps restant avant reset,
couleur = pourcentage de quota consommé. Pour un utilisateur intensif de Claude qui veut
savoir sans y penser combien de marge il lui reste avant d'être bloqué.
Un widget dit quelle session Claude Code attend l'utilisateur, et depuis v1.8 une fenêtre « Historique » séparée montre
comment le forfait a été consommé au fil du temps (semaine de forfait, jour, quatre semaines), sans un chiffre inventé.

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
- ✓ Deux mots à l'écran (« Réflexion », « En attente », « En attente ? »), question `AskUserQuestion` classée attente, ordre découplé de l'arbitrage, horizons uniques 20 min / 8 h (LIB-01..04, SIL-01) — Phase 28
- ✓ Source app-bureau par fichiers en lecture seule : titre, dernier focus, question `blocked` ; deux vues d'AppData (cache MSIX + réelle) lues par l'overlay, constaté hors de l'arbre (APP-01..06) — Phase 29
- ✓ La lecture fait disparaître : attente antérieure au dernier focus ou session sélectionnée avec la fenêtre Claude au premier plan (grâce 2,5 s) ⇒ masquée, avec sa cause au diagnostic ; une session qui travaille reste visible (LUE-01..05) — Phase 30
- ✓ Un sous-agent qui écrit est un travail de sa session : battements et transcripts des sous-agents ⇒ « Réflexion » du parent, sans jamais effacer une attente d'intervention ; contrat sous garde ; exe 3.2.1 (SUB-01, SUB-02) — Phase 30.1 (insérée, écart E2 du constat)
- ✓ Relevé in vivo AVANT la règle : le retour alt-tab met `lastFocusedAt` à jour, le tour fini sous les yeux non (VAL-01) — Phase 27
- ✓ Contrat de la source app-bureau sous garde croisée (14 champs) ; exe 3.2.0 puis 3.2.1 publiés, version embarquée, réconciliation constatée dans le fichier (VAL-02 ; VAL-03 partielle : constat en production reporté en phase 32) — Phase 31 — v1.7
- ✓ Compter juste et journaliser : dédup des usages par `message.id` (max par champ, dictionnaire global), magasins observables + vue AppData au diagnostic, mutex mono-instance, journal des relevés exacts (1 / 5 min, événements de couverture, append sûr), alerte « journal muet », lecture par plage (169 h / 167 h aux changements d'heure), exe 3.2.2 publié (CPT-01..03, JRN-01..06 ; VAL-04 NON jouée : constat reporté au milestone suivant) — Phase 32 — v1.8
- ✓ Agrégats de tokens : tranches 15 min UTC × modèle × sous-agent, quatre compteurs, index d'ids append-only + projection mensuelle réécrite atomiquement, reconstruction de fond (thread BelowNormal, 3,2 s à chaud, 47 ms en incrémental), curseurs, rendu local DST (25 barres le 25/10/2026, 23 le 28/03/2027), couverture « hors couverture / transcripts absents / couverte », garde « aucun pourcentage dérivé de tokens » (TOK-01..05) — Phase 33 — v1.8
- ✓ Fenêtre Historique : fenêtre de consultation opaque (WindowChrome, géométrie persistée), vue Semaine de forfait en trois styles sélectionnables (Pistes / Simplifié / Tuiles, hauteurs du plan de design vérifiées par Measure/Arrange), vue Jour au grain 5 min, pistes à OnRender + StreamGeometry gelée jamais redessinées au tick, palette promue dans DesignTokens.xaml sans changer une valeur, honnêteté testée mot pour mot (trous, « répartition inconnue », « consommé ailleurs »), bandeau F2, galerie `--historique` (HIS-01..04, HIS-06..08) — Phase 34 — v1.8
- ✓ 4 semaines, accès, release : vue 4 semaines (semaines antérieures au journal dites telles, semaine épuisée annotée), carte « Historique d'utilisation » des réglages + double-clic au centre du cadran sans double bascule, section `[Journal d'historique]` du diagnostic, README et `docs/data-sources.md` §9 sous garde de vocabulaire, exe 3.3.0 (HIS-05, ACC-01..04 ; VAL-05 NON jouée : constat reporté au milestone suivant) — Phase 35 — v1.8
- ✓ « Quitter Chronos » sans processus zombie (arrêt du Host borné hors thread UI), exe 3.3.1 ; fenêtre de réglages classique redimensionnable (six sections, aperçu vivant, diagnostic intégré), exe 3.4.0 — quick 260927 et 260927-reglages-v2 — v1.8 (hors phases)

### Active

<!-- Current scope. Building toward these. -->

Milestone v1.9 « Lisible partout » (exe 3.5.0) — 25 exigences dans `.planning/REQUIREMENTS.md` : SOC-01..02, DAT-01..05,
HIS-09..11, THM-01..04, CAD-01..04, BRA-01..02, GST-01..03, VAL-06..07. VAL-07 reprend les constats reportés de v1.8
(VAL-04, VAL-05) ; HIS-09 absorbe la suppression des styles Simplifié et Tuiles.

Validées en cours de milestone : SOC-01, SOC-02 (phase 36 Socle, 2026-10-03 — lecture des réglages valeur par valeur, garde d'arguments inconnus) ; DAT-01..05 (phase 37, 2026-10-03 — chaîne réduite à sonde d'en-têtes + secours OAuth Chronos, barre statusLine retirée avec sauvegarde, diagnostic « Chaîne de données », méthodologie unique) ; HIS-09..11 (phase 38, 2026-10-03 — Pistes seul, plein écran général, Échap à deux niveaux ; revue visuelle humaine en attente) ; THM-01..04 (phase 39, 2026-10-03 — 15 thèmes en Pâle / Classique / Vive, gris épuisé ≥ 3:1, cadrans alternatifs et TickReset thémés) ; CAD-01..04 (phase 40, 2026-10-03 — cadrans à l'échelle 1, +20 % Fusible/Volets, orientations H/V, recalage au coin, carte Orientation, galerie à 8 variantes).

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
- **SQLite pour l'historique** — `e_sqlite3.dll` est une dépendance native (casse le mono-fichier) ; JSONL mensuel tolérant retenu en v1.8.
- **Pourcentage dérivé de tokens, double axe Y, reconstitution des pourcentages avant l'ouverture du journal** — v1.8 : deux séries de nature différente ne se fusionnent jamais ; une garde structurelle l'interdit.
- **Hystérésis « traité » par focus de fenêtre** — supprimée avec l'UIA : elle exigeait `Origin == Desktop` et n'atteignait donc JAMAIS une session Claude Code. Remplacée par une transition observée et un geste explicite.

## Current State (v1.8 clos — 2026-10-03)

**v1.8 « Historique d'utilisation » est livré (exe 3.3.0, puis 3.3.1 et 3.4.0 hors phases) et clos avec écarts connus.**
Phases 32 à 35, 26 plans sur 28 ; **1707 tests verts, 0 warning** (Debug et Release) à la 3.4.0 ; ≈ 60 800 lignes C#/XAML
sous `src/`, ≈ 40 700 lignes de tests ; toujours zéro dépendance native. Chronos compte juste (dédup `message.id`), ne se tait
plus (âge de dernière écriture de chaque magasin au diagnostic, vue AppData réelle ou virtualisée affichée), refuse de tourner en
double, journalise un relevé exact toutes les 5 min et agrège les tokens des transcripts ; la fenêtre Historique (Semaine en
trois styles, Jour, 4 semaines) s'ouvre par la carte des réglages ou par un double-clic au centre du cadran.

**Écarts connus** (voir `.planning/MILESTONES.md` › v1.8 › Known Gaps) : les constats avec l'utilisateur VAL-04 (32-08) et
VAL-05 (35-07) ne sont pas joués et passent dans la phase de constat du milestone suivant ; la DESIGN-REVIEW de la galerie, le
DEV-COUNCIL et le DEV-SENIOR du cycle ZEUS n°1 sont absorbés par le cycle n°2. Les exécutables 3.2.2, 3.3.0, 3.3.1 et 3.4.0 sont
publiés localement, sans tag de release ni push.

<details><summary>État pendant v1.8 (2026-09-27)</summary>

**v1.7 clos le 2026-09-27** (exe 3.2.1, 1200 tests) avec un écart connu : le constat en production est PARTIEL (trois exécutables
en marche, tableau des gestes non joué). **Deux défauts découverts à la clôture** par le conseil LLM du 26/09 : le parser de
tokens compte 2 à 2,75× trop (corrigé en phase 32) ; le « gel » de `last-exact.json` (établi en phase 32 comme un artefact de la
vue AppData virtualisée par MSIX, pas une panne d'écriture). Sonde d'en-têtes : 1 relevé / 5 min. Transcripts : 1 565 fichiers,
2,2 Go, depuis juin (juillet purgé). Reset hebdo : samedi 00:00 heure locale (confirmé par `resets_at`).

</details>

<details><summary>État à l'entrée en v1.7 (2026-09-25)</summary>


**Avancement v1.7 (2026-09-25, soir) :** phase 27 close (relevé in vivo : alt-tab met `lastFocusedAt` à jour ;
tour fini sous les yeux le laisse antérieur ⇒ LUE-02 nécessaire) ; **phase 28 close** (947 tests, vérification
5/5 : les trois mots, la question en suspens, `HorizonsSessions`). **Découverte majeure** (sonde hors de l'arbre
de l'app, `29-SONDE-HORS-ARBRE.txt`) : AppData est VIRTUALISÉ par MSIX pour tout ce qui tourne sous l'app
bureau — l'overlay n'a jamais vu un fichier d'état de hook (`%APPDATA%\Chronos\sessions` réel vide) ; APP-06
(phase 29) lit les deux vues.
**Phase 29 close** (1062 tests, vérification 6/6) : l'overlay lit enfin les fichiers d'état des hooks (dossier du paquet), affiche le titre de session et reconnaît une question posée par l'app. **Phase 30 close** (1143 tests, vérification 6/6) : une session lue disparaît sans clic, revient si elle redemande, et le diagnostic dit pourquoi. **Phase 30.1 close** (insérée le 2026-09-26 sur l'écart E2 signalé en direct par l'utilisateur : une session dont un agent tourne en arrière-plan s'affichait « En attente ») : 1200 tests, exe **3.2.1** publié. Reste : le constat de la phase 31 avec l'utilisateur (points a, b, c).

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

</details>

## Current Milestone: v1.9 — Lisible partout

**Goal:** Chaque cadran lisible et saisissable dans toutes ses formes, sur une chaîne de données réduite à ce qui marche
(roadmap utilisateur R1-R7 du 2026-10-01, cycle ZEUS n°2).

**Target features:**
- Socle : réglages tolérants valeur par valeur, garde d'arguments inconnus
- Purge de la récupération des données + méthodologie unique ; retrait de la barre statusLine (décision utilisateur)
- Historique : Pistes seul + plein écran général
- Thèmes Pâle / Classique / Vive, 6 palettes nouvelles, contrastes garantis, cadrans alternatifs thémés
- Cadrans à l'échelle 1 (+20 % Fusible/Volets), orientations horizontale et verticale pour Fusible / Marée / Volets
- Braises : 5 groupes d'une heure, flèche de reset, heure du reset
- Geste unique sur la silhouette de chaque cadran (clic, double-clic, glisser, clic droit)
- Release Chronos-v3.5.0.exe + constat utilisateur (reprend VAL-04 / VAL-05 de v1.8)

**Cadre :** plan de design validé `.zeus/DESIGN_PLAN_CYCLE2.md` ; approche `.zeus/reports/llm-council-2026-10-03.md` ;
liste de purge validée `.zeus/reports/cycle2/liste-purge.md` (2026-10-03).

## Next Milestone Goals (après v1.8)

À reprendre en priorité : les constats VAL-04 et VAL-05 (gestes de l'utilisateur) ; suppression des styles Simplifié et Tuiles.
Heatmap jour × heure des rythmes ; export CSV de la plage affichée ; compaction du journal au-delà de 8 semaines ; dimension
projet dans les agrégats ; projection conditionnelle « à ce rythme » (seulement si visuellement distincte d'un relevé) ;
`DayTimeline` sur les resets observés plutôt qu'une grille théorique de 5 h ; dérive d'une heure de `WeeklyWindow` au
changement d'heure (test d'acceptation le 25/10/2026). Reliquat : sous-fenêtres opus/sonnet/cowork, tray, taille réglable,
préavis avant saturation, notification au reset ; économie de la sonde si `/api/oauth/usage` sert un jour les en-têtes unifiés.

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
| « Lue » lu dans les métadonnées par session de l'app bureau (v1.7) | `lastFocusedAt` est le seul signal de lecture qui existe ; format interne non documenté → tolérance + dégradation | ✓ Good — v1.7, 1200 tests ; constat geste par geste reporté en phase 32 |
| Deux journaux de nature différente, jamais fusionnés : relevés exacts (magnitude, compte entier) et agrégats de tokens (attribution, Claude Code seul) (v1.8) | Les tokens ne sont pas convertibles en % (plafonds pondérés, non publiés, périmètre partiel) ; le Δ entre deux relevés exacts est la seule mesure de consommation vraie | ✓ Good — v1.8 : deux axes, deux palettes, garde structurelle TOK-05 |
| JSONL mensuel tolérant, pas SQLite (v1.8) | `e_sqlite3.dll` est une dépendance native, exclue ; 25 Mo/an tiennent en mémoire | ✓ Good — v1.8 : journal et agrégats en JSONL, append exclusif idempotent, réécriture atomique |
| Fenêtre Historique séparée, opaque, non topmost (v1.8) | Le cadran est un mode coup d'œil (layered, coûteux) ; l'historique un mode consultation | ✓ Good — v1.8 livré ; constat utilisateur (VAL-05) reporté |
| Trois styles sélectionnables pour la vue Semaine (Pistes, Simplifié, Tuiles) (v1.8) | L'utilisateur voulait comparer avant de choisir | ⚠️ Revisit — décision utilisateur : Simplifié et Tuiles supprimés au milestone suivant, seul Pistes reste |
| Verrou mono-instance (mutex `Local\Chronos-overlay`), hooks et CLI exemptés (v1.8) | Trois exécutables tournaient ensemble au constat v1.7 ; un journal à plusieurs écrivains doit être idempotent | ✓ Good — v1.8 (constat en vrai reporté avec VAL-04) |
| Ne jamais juger les fichiers de l'overlay depuis une session Claude Code (v1.8) | AppData est virtualisé par MSIX sous l'app bureau : la « panne » de `last-exact.json` était la copie du paquet | ✓ Good — vue AppData affichée au diagnostic, sondes WMI hors arbre |
| Arrêt du Host hors thread UI, borné (quick 260927, 3.3.1) | `await` + `GetResult` dans `OnExit` interbloquait Host et Dispatcher : processus zombie, mutex tenu | ✓ Good — 6 tests Host réel sous Dispatcher |
| Dédup des tokens par `message.id` avant toute somme (v1.8) | Une ligne `assistant` par bloc de contenu, même `usage` recopié : facteur 2 à 2,75 mesuré | ✓ Good — v1.8, publié en 3.2.2 (max par champ, dictionnaire global, garde « aucun lecteur hors du helper ») |

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
*Last updated: 2026-10-03 — phase 40 (cadrans et orientations) terminée*
