# REQUIREMENTS.md — Chronos v1.4 « Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code) »

## Contexte

Le widget sessions existant (livré hors GSD, « v2.5 ») détecte les sessions **Claude Code** via les
transcripts JSONL (`~/.claude/projects`) et les hooks. v1.4 l'étend à l'**application de bureau** Claude
(Chat / Code / Cowork) via **UI Automation** — spike prouvé sur la machine le 2026-07-10 (voir mémoire
`chronos-desktop-uia.md`) — et ajoute la **disparition automatique** des sessions traitées (règle
d'hystérésis décidée avec l'utilisateur). Aucune notification Windows (le signal UIA suffit) ; honnêteté
préservée (état « indéterminé » quand la vérité-terrain n'est pas observable localement).

## v1.4 Requirements

### App bureau via UI Automation (BUR)

- [x] **BUR-01**: L'utilisateur voit dans le widget les sessions de l'**application de bureau** Claude
  (en plus des sessions Claude Code CLI), détectées en lisant l'arbre UI Automation de la fenêtre Claude
  (`System.Windows.Automation` — interop COM managé, pas de dépendance native de rendu, pas d'admin).
- [x] **BUR-02**: Chaque session bureau affiche un **état honnête** : en cours (bosse), tour fini
  (attend ton message), attend une permission, ou indéterminé — jamais un état certain quand il est inféré.
- [x] **BUR-03**: Le widget **distingue le type** de session bureau : Chat, Code, Cowork
  (via les libellés/affordances de l'arbre : « Mode chat », onglets Home/Code, panneaux Terminal/Diff/Cowork).
- [x] **BUR-04**: Les **sessions agentiques actives** listées dans la barre latérale de l'app (marqueur
  « En cours d'exécution ») sont énumérées, pas seulement la conversation au premier plan.
- [x] **BUR-05**: Une session **Cowork en VM distante** est marquée « indéterminé » et jamais présentée
  comme un état d'exécution certain — son exécution n'est pas observable localement (honnêteté).

### Auto-disparition des sessions traitées (NET)

- [ ] **NET-01**: Une session en attente **disparaît automatiquement** de la liste dès que l'utilisateur
  **y répond** (elle repasse en « en cours » — transition observable via transcript ou via UIA).
- [ ] **NET-02**: Une session en attente **disparaît automatiquement** dès que l'utilisateur la garde
  **au premier plan** de l'app ≥ ~2-3 s (acquittement, avec debounce anti-survol).
- [ ] **NET-03**: Une session « traitée » qui **repart en attente** (événement d'attente plus récent que
  le traitement) **réapparaît** dans la liste.
- [ ] **NET-04**: L'**archivage manuel** par clic droit reste disponible et **permanent**, distinct et
  complémentaire de l'auto-disparition (ne réapparaît jamais, contrairement au « traité »).

### Robustesse & threading (ROB)

- [x] **ROB-06**: La détection UIA **résiste aux changements de version** de l'app : matching souple par
  libellé (table fr/en, pas par `AutomationId` volatils), **test de santé au démarrage**, dégradation vers
  « indéterminé » plutôt que d'inventer un état ; aucune source indisponible ne provoque de crash.
- [x] **ROB-07**: La lecture UIA **ne bloque pas le thread UI** (lecture hors thread UI puis marshalling),
  cadence alignée sur le tick existant (~1-2 s), élément racine mis en cache.

## Future Requirements (différés)

- Notification Windows en bonus du signal UIA (`UserNotificationListener`) — front « vient de finir ».
- Décompte d'usage par session / ventilation Chat vs Code vs Cowork (bonus lisible dans l'arbre : `Usage: …`).

## Out of Scope (v1.4)

- **Notifications Windows / toasts** — le signal UIA suffit pour v1.4 ; le canal notification est un bonus différé.
- **État d'exécution des sessions Cowork en VM distante** — structurellement non observable localement (marqué « indéterminé »).
- **Détection des conversations Chat en arrière-plan** (hors premier plan et hors sidebar) — l'app n'expose que
  le premier plan pour le Chat pur ; le « m'attend » par session Chat n'a de sens qu'au premier plan.
- **Historique / réouverture / navigation** des sessions depuis l'overlay — visualisation seule.
- **Décompte d'usage par session dans le cadran** — l'usage reste agrégé (cadran v1.x inchangé).

## Traceability

| REQ-ID | Phase | Statut |
|--------|-------|--------|
| BUR-01 | Phase 13 | Complete |
| BUR-02 | Phase 13 | Complete |
| BUR-03 | Phase 13 | Complete |
| BUR-04 | Phase 13 | Complete |
| BUR-05 | Phase 13 | Complete |
| NET-01 | Phase 14 | Pending |
| NET-02 | Phase 14 | Pending |
| NET-03 | Phase 14 | Pending |
| NET-04 | Phase 14 | Pending |
| ROB-06 | Phase 13 | Complete |
| ROB-07 | Phase 13 | Complete |

**Couverture :** 11/11 requirements mappés · Phase 13 (7) + Phase 14 (4) · aucun orphelin, aucun doublon.

---
*Last updated: 2026-07-10 — roadmap v1.4 créé (phases 13-14, 11 requirements mappés)*
