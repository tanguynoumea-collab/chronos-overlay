# REQUIREMENTS.md — Chronos v1.6 « Observer au lieu de déduire »

## Contexte

Le widget de sessions doit répondre à une seule question : **quelle session m'attend ?** Trois états comptent
— « en train de réfléchir », « en attente d'une réponse », « traité ». Les trois sont mal transmis.

L'investigation du 2026-09-12 (`.planning/debug/widget-sessions-statuts.md`) établit **trois causes racines
distinctes**, prouvées contre les classes réelles et contre la documentation officielle des hooks Claude Code :

1. **« Réfléchit » est déduit par expiration, jamais observé.** `Working` n'est écrit que par
   `UserPromptSubmit` et `SessionStart` ; aucun événement ne confirme jamais que le travail continue. Et
   `SessionMonitor.Read` arbitre par **ordre d'insertion**, pas par fraîcheur : un fichier de hook de 7 h
   écrase un transcript de 10 secondes.
2. **« Attend » repose sur une sémantique fausse à la source.** `Stop` **ne se déclenche pas** sur
   interruption utilisateur — la session ne sera donc jamais annoncée en attente dans le cas où elle attend
   le plus. `Notification` est une alerte « tu sembles absent du terminal » qui couvre permission **et**
   inactivité **et** fin de tâche. Un événement `PermissionRequest` **dédié** existe et n'est pas utilisé.
3. **« Traité » ne peut structurellement pas fonctionner en terminal.** Le tracker exige `Origin == Desktop`
   et un identifiant `desktop:foreground:*` ; une session Claude Code a `Origin = Cli` et un UUID. Et la règle
   confond « l'utilisateur a répondu » avec « ma source a expiré » : preuve arithmétique, une attente
   enregistrée 478 min avant le seuil de 480 min a masqué une session **réellement en attente** pendant 6 h.

**Doctrine du milestone**, héritée de v1.5 et appliquée au widget : **ne jamais présenter comme un fait ce
qui n'a pas été observé.** Un état dont la source a expiré n'est pas « terminé » — il est **inconnu**.

## v1.6 Requirements

### Périmètre — widget Claude Code uniquement (SRC)

- [x] **SRC-01**: Le widget ne montre QUE des sessions **Claude Code**. La source app-bureau par UI Automation
  est retirée (`DesktopUiaSessionSource`, `DesktopUiaPollService`, `WindowsUiaTreeProvider`,
  `IUiaTreeProvider`, `UiaLabels`, `UiaNode` — ~690 lignes — plus `WindowsForegroundWatch` / `IForegroundWatch`
  devenus morts avec l'hystérésis par focus).
- [x] **SRC-02**: Les entrées fantômes `desktop:foreground:*` disparaissent, y compris celles déjà présentes
  dans `archived.json` — l'utilisateur avait dû les archiver à la main parce qu'elles ne vieillissaient jamais.
- [x] **SRC-03**: La limite de fichiers de transcripts est appliquée **après** le filtre des sous-agents, et
  non avant : 94 % des transcripts sont des `agent-*.jsonl`, et une vague d'agents parallèles aveuglait la
  source jusqu'à faire disparaître la vraie session.

### Contrat d'événements (EVT)

- [x] **EVT-01**: L'événement `PermissionRequest` alimente « en attente » — signal **exact et immédiat**, au
  lieu du proxy `Notification`.
- [x] **EVT-02**: `Notification` cesse d'être traité comme un **état**. C'est une alerte d'absence : au mieux
  un indice, jamais une vérité sur ce que fait la session.
- [x] **EVT-03**: Des **battements de cœur** rafraîchissent « réfléchit » : l'état est **observé** tant que le
  travail continue, et cesse de dépendre d'un seuil d'expiration deviné.
- [x] **EVT-04**: Une **interruption utilisateur** (Échap — aucun `Stop` n'est émis) ne laisse plus la session
  dans un état faux ni invisible.
- [ ] **EVT-05**: Le contrat des hooks est **documenté** dans `docs/`, au même titre que les autres sources.
  Son absence est ce qui a laissé la dérive du contrat externe passer inaperçue.

### Fusion des sources (FUS)

- [x] **FUS-01**: Un signal ne peut en écraser un autre que s'il est **plus récent**. Jamais par ordre
  d'insertion dans un dictionnaire.
- [x] **FUS-02**: Les **désaccords** entre sources sont traçables dans le diagnostic — aujourd'hui ils sont
  silencieux.

### « Traité » (TRT)

- [ ] **TRT-01**: « Traité » n'est déduit que d'une **transition observée sur la même source**. Jamais d'une
  expiration de source, jamais d'une bascule transcript ↔ hook.
- [ ] **TRT-02**: Le « traité » **survit à un redémarrage** de l'overlay : une session traitée ne ressort pas
  toute seule.
- [ ] **TRT-03**: L'utilisateur dispose d'un **geste explicite** pour marquer une session traitée — le focus
  de fenêtre ne peut pas le fournir pour une session de terminal.
- [ ] **TRT-04**: L'archivage respecte un **contrat unique** : permanent OU temporaire, pas les deux. Le clic
  droit « Archiver » est aujourd'hui annoncé permanent mais expire au bout de 6 h.

### Cycle de vie du magasin (CYC)

- [x] **CYC-01**: Les fichiers d'état expirés et les `.tmp` orphelins sont **balayés** — `SessionEnd` ne peut
  pas être garanti (il ne couvre ni terminal tué, ni crash, ni redémarrage machine), donc le magasin ne peut
  aujourd'hui que croître : 54 fichiers dont 48 de plus de 7 jours, plus 12 `.tmp`.
- [x] **CYC-02**: L'écriture d'un état de hook ne peut plus être **perdue en silence**.

### Observabilité (OBS)

- [x] **OBS-01**: Le diagnostic dit **exactement** ce que le widget affiche — même moniteur, mêmes filtres.
  Il construit aujourd'hui son propre moniteur nu, ce qui a très probablement empêché d'élucider le problème.
- [x] **OBS-02**: Le diagnostic liste les sessions **pertinentes**, et non les 8 premières par ordre
  alphabétique (toutes vieilles de plusieurs semaines).

## Future Requirements (différés)

- Ventilation par modèle, survol/tooltip, tray, taille réglable.
- Préavis avant saturation (~90 %) et notification au reset.

## Out of Scope (v1.6)

- **Source app-bureau par UI Automation** — retirée : elle produisait des entrées qui ne vieillissaient jamais
  et imposait une hystérésis par focus inatteignable depuis une session de terminal.
- **Hystérésis « traité » par focus de fenêtre** — supprimée avec elle.
- **Correction fine des écritures concurrentes** — le mécanisme est réel (`File.Move` échoue si un lecteur
  tient le fichier, perte silencieuse) mais son poids mesuré est de ~0,7 événement perdu sur 5 000. CYC-02
  traite le silence ; l'optimisation fine n'est pas prioritaire.
- **Notifications Windows / toasts** — inchangé.

## Traceability

| REQ-ID | Phase | Statut |
|--------|-------|--------|
| SRC-01 | Phase 21 | Complete |
| SRC-02 | Phase 21 | Complete |
| SRC-03 | Phase 21 | Complete |
| EVT-01 | Phase 25 | Complete |
| EVT-02 | Phase 25 | Complete |
| EVT-03 | Phase 25 | Complete |
| EVT-04 | Phase 25 | Complete |
| EVT-05 | Phase 25 | Pending |
| FUS-01 | Phase 24 | Complete |
| FUS-02 | Phase 24 | Complete |
| TRT-01 | Phase 26 | Pending |
| TRT-02 | Phase 26 | Pending |
| TRT-03 | Phase 26 | Pending |
| TRT-04 | Phase 26 | Pending |
| CYC-01 | Phase 23 | Complete |
| CYC-02 | Phase 23 | Complete |
| OBS-01 | Phase 22 | Complete |
| OBS-02 | Phase 22 | Complete |

**Couverture :** 18 / 18 requirements mappés — aucun orphelin, aucun doublon.

**Regroupement par phase :**

| Phase | Intitulé | Requirements |
|-------|----------|--------------|
| 21 | Périmètre — le widget ne parle que de Claude Code | SRC-01, SRC-02, SRC-03 |
| 22 | Un instrument de mesure qui ne ment plus | OBS-01, OBS-02 |
| 23 | Un magasin qui ne croît plus et n'oublie plus | CYC-01, CYC-02 |
| 24 | L'arbitrage par fraîcheur | FUS-01, FUS-02 |
| 25 | Le contrat d'événements refondé | EVT-01, EVT-02, EVT-03, EVT-04, EVT-05 |
| 26 | « Traité » veut enfin dire quelque chose | TRT-01, TRT-02, TRT-03, TRT-04 |

---
*Last updated: 2026-09-12 — roadmap v1.6 créée : 6 phases (21-26), traçabilité complète 18/18*
