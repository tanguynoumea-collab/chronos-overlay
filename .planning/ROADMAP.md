# Roadmap : Chronos

## Milestones

- ✅ **v1.0 — Overlay de quotas Claude complet** (7 phases, 18 plans, SHIPPED 2026-07-08) — [archive](.planning/milestones/v1.0-ROADMAP.md)
- ✅ **v1.1 — Estimation utile en mode app bureau** (2 phases, 5 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.1-ROADMAP.md)
- ✅ **v1.2 — Usage exact via l'endpoint OAuth** (2 phases, 4 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.2-ROADMAP.md)
- ✅ **v1.3 — Refonte du cadran (3 anneaux, remplissage, compacité)** (1 phase, phase 12, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.3-ROADMAP.md)
- 🚧 **v1.4 — Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code)** (2 phases, phases 13-14, en cours)

## Prochain milestone

Après v1.4 : refresh du token OAuth, sous-fenêtres opus/sonnet/cowork, survol/tooltip, tray,
taille réglable, notification Windows en bonus du signal UIA (`UserNotificationListener`).

---

## Milestone v1.4 : Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code)

### Overview

Le widget sessions existant (livré hors GSD, « v2.5 ») détecte les sessions **Claude Code** via les
transcripts JSONL (`~/.claude/projects`) et les hooks. v1.4 l'étend à l'**application de bureau** Claude
(Chat / Code / Cowork) via **UI Automation** — spike prouvé sur la machine le 2026-07-10 — et fait
**disparaître automatiquement** les sessions traitées selon une règle d'hystérésis décidée avec l'utilisateur.

Le milestone se lit en **deux gestes cohérents à dépendance technique forte** :

1. **La source de vérité d'abord** (Phase 13). La distinction Chat/Cowork/Code et la branche « acquittée
   par focus » (NET-02) EXIGENT l'arbre UI Automation de la fenêtre Claude. Rien de l'auto-disparition
   focus-based ne peut fonctionner sans cette source. On construit donc `DesktopUiaSessionSource` :
   lecture de l'arbre UIA, états honnêtes (bosse / attend / attend-permission / indéterminé), distinction
   des types de session, énumération de la sidebar, matching souple fr/en, test de santé, dégradation,
   lecture non bloquante pour le thread UI.

2. **L'hystérésis ensuite** (Phase 14), qui roule sur la source précédente. Un magasin « traitées »
   auto-géré et réversible retire une session sur « répondu » (retour en cours — déjà observable avec les
   sources actuelles, NET-01) **ou** sur « acquittée par focus » (premier plan ≥ ~2-3 s — nécessite la
   source UIA de la Phase 13, NET-02), et la fait réapparaître sur un événement d'attente plus récent.
   L'archivage manuel par clic droit reste conservé, distinct et permanent.

**Contraintes portées :** `System.Windows.Automation` (interop COM managé, aucune dépendance native de
rendu, aucun droit admin), chemins sous `%USERPROFILE%`/`%APPDATA%` uniquement, honnêteté (état
« indéterminé » quand la vérité-terrain n'est pas observable localement — ex. Cowork en VM distante),
lecture tolérante (aucune source indisponible ne provoque de crash), UI et commentaires en français.

**Briques existantes réutilisées :** `SessionSnapshot` (record neutre + enum `SessionActivity`
Working/WaitingAttention/WaitingTurn/Unknown), `SessionMonitor.Read(now)` (fusionne transcripts + hooks,
staleness, filtre `archived` via `ArchiveStore`), `TranscriptSessionSource`, `SessionsViewModel`
(Refresh, timer 2 s), tests dans `SessionsTests.cs`.

### Phases

**Numérotation des phases :**
- Phases entières (13, 14) : travail de milestone planifié — continue après la Phase 12 (v1.3)
- Phases décimales (13.1, 13.2) : insertions urgentes (marquées INSERTED)

- [ ] **Phase 13 : Source UIA app bureau** - Un `DesktopUiaSessionSource` lit l'arbre UI Automation de la fenêtre Claude : états honnêtes (bosse / attend / attend-permission / indéterminé), distinction Chat/Code/Cowork, énumération des sessions actives de la sidebar, matching souple fr/en, test de santé + dégradation, lecture non bloquante pour le thread UI
- [ ] **Phase 14 : Auto-disparition hystérésis des sessions traitées** - Un magasin « traitées » auto-géré et réversible retire une session sur « répondu » OU « acquittée par focus ≥ ~2-3 s », la fait réapparaître sur un événement d'attente plus récent, et conserve l'archivage manuel clic-droit distinct et permanent

### Phase Details

### Phase 13 : Source UIA app bureau
**Goal**: Livrer un `DesktopUiaSessionSource` qui, en lisant l'arbre UI Automation de la fenêtre de
l'application de bureau Claude (`System.Windows.Automation`), fait apparaître dans le widget les sessions
Chat / Code / Cowork de l'app bureau — en plus des sessions Claude Code CLI existantes — chacune avec un
état honnête (bosse / attend / attend-permission / indéterminé), son type identifié, et l'ensemble des
sessions agentiques actives de la sidebar énumérées ; le tout robuste aux changements de version (matching
souple fr/en, test de santé, dégradation vers « indéterminé ») et sans jamais bloquer le thread UI.
**Depends on**: v1.3 (Phase 12 close) — première phase du milestone v1.4. S'appuie sur les briques
sessions existantes (`SessionSnapshot`, `SessionMonitor`, `SessionsViewModel`) livrées hors GSD.
**Requirements**: BUR-01, BUR-02, BUR-03, BUR-04, BUR-05, ROB-06, ROB-07
**Success Criteria** (what must be TRUE):
  1. **Sessions bureau visibles** : l'utilisateur voit dans le widget les sessions de l'application de
     bureau Claude (en plus des sessions Claude Code CLI), détectées via l'arbre UI Automation de la
     fenêtre Claude — sans dépendance native de rendu ni droit admin (BUR-01).
  2. **États honnêtes** : chaque session bureau affiche « en cours » (bosse), « attend ton message »
     (tour fini), « attend une permission », ou « indéterminé » — et une session Cowork en VM distante,
     structurellement non observable localement, est marquée « indéterminé », jamais présentée comme un
     état d'exécution certain (BUR-02, BUR-05).
  3. **Type distingué** : le widget indique le type de chaque session bureau — Chat, Code ou Cowork —
     dérivé des libellés/affordances de l'arbre (« Mode chat », onglets Home/Code, panneaux
     Terminal/Diff/Cowork) (BUR-03).
  4. **Sidebar énumérée** : les sessions agentiques actives listées dans la barre latérale de l'app
     (marqueur « En cours d'exécution ») sont toutes énumérées, pas seulement la conversation au premier
     plan (BUR-04).
  5. **Robuste & non bloquant** : la détection résiste aux changements de version (matching souple par
     libellé fr/en et non par `AutomationId` volatils, test de santé au démarrage, dégradation vers
     « indéterminé » plutôt que d'inventer un état, aucune source indisponible ne crashe), et la lecture
     UIA se fait hors thread UI puis marshalée, à la cadence du tick existant (~1-2 s), sans figer
     l'overlay (ROB-06, ROB-07).
**Plans**: TBD
**UI hint**: yes

**Note de démarrage** : le spike UIA a été capturé pendant une génération ; il MANQUE un snapshot en
état **repos** (premier plan qui « m'attend »). Le capturer en tout début de Phase 13 pour figer la
représentation exacte avant de coder le mapping d'états.

### Phase 14 : Auto-disparition hystérésis des sessions traitées
**Goal**: Faire disparaître automatiquement de la liste les sessions traitées, selon une règle
d'hystérésis réversible : un magasin « traitées » auto-géré (clé `SessionId` + horodatage de l'état
d'attente déclencheur), filtré dans `SessionMonitor.Read` au même endroit que le filtre `archived`, retire
une session dès qu'elle est « répondue » (retour en Working) OU « acquittée » (focus premier plan ≥ ~2-3 s
avec debounce anti-survol), et la fait réapparaître si un événement d'attente plus récent survient —
tandis que l'archivage manuel par clic droit reste disponible, distinct et permanent.
**Depends on**: Phase 13 (la branche « acquittée par focus », NET-02, exige la source UIA premier-plan ;
la branche « répondu », NET-01, fonctionne déjà avec les sources actuelles).
**Requirements**: NET-01, NET-02, NET-03, NET-04
**Success Criteria** (what must be TRUE):
  1. **Disparition sur réponse** : une session en attente disparaît automatiquement de la liste dès que
     l'utilisateur y répond (elle repasse en « en cours », transition observable via transcript ou UIA)
     (NET-01).
  2. **Disparition sur acquittement focus** : une session en attente disparaît dès que l'utilisateur la
     garde au premier plan de l'app ≥ ~2-3 s (debounce anti-survol) (NET-02).
  3. **Réapparition réversible** : une session « traitée » qui repart en attente (événement d'attente plus
     récent que le traitement) réapparaît dans la liste (NET-03).
  4. **Archivage manuel préservé** : l'archivage par clic droit reste disponible et permanent — distinct
     et complémentaire de l'auto-disparition, ne réapparaît jamais contrairement au « traité » (NET-04).
**Plans**: TBD
**UI hint**: yes

### Progress

**Execution Order:**
Phase 13 → Phase 14 (dépendance forte : la source UIA avant l'hystérésis focus-based).

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 13. Source UIA app bureau | 0/TBD | Not started | - |
| 14. Auto-disparition hystérésis des sessions traitées | 0/TBD | Not started | - |
