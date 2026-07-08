# Roadmap : Chronos

## Overview

Chronos se construit du bas vers le haut selon un DAG de dépendances strict : d'abord le squelette overlay et son graphe de services (Phase 1), puis une phase de découverte bloquante qui documente la source de données non documentée AVANT tout code (Phase 2). Sur cette base, le pipeline de données neutre est bâti (Phase 3), rafraîchi et marshalé vers un ViewModel temps réel (Phase 4), puis rendu par le cadran à deux anneaux (Phase 5). Les comportements d'overlay (placement, menu, persistance) viennent ensuite (Phase 6), avant l'empaquetage final en exe mono-fichier autonome (Phase 7). Chaque phase délivre une capacité vérifiable et débloque la suivante.

## Phases

**Numérotation des phases :**
- Phases entières (1, 2, 3) : travail de milestone planifié
- Phases décimales (2.1, 2.2) : insertions urgentes (marquées INSERTED)

- [ ] **Phase 1 : Fondations architecture + squelette overlay** - Fenêtre overlay vide, borderless/transparente/always-on-top portée par un graphe DI
- [ ] **Phase 2 : Découverte des sources (bloquante)** - Localisation et documentation empirique de l'objet d'usage Claude AVANT tout code de provider
- [ ] **Phase 3 : Modèles + pipeline de données** - Snapshots neutres et providers primaire/repli/composite isolés du cadran
- [ ] **Phase 4 : Orchestration refresh + ViewModel temps réel** - Rafraîchissement automatique et interpolation à la seconde sans erreur de thread
- [ ] **Phase 5 : Cadran (RingArc + converters) + câblage View** - Cadran à deux anneaux reflétant temps restant et utilization en temps réel
- [ ] **Phase 6 : Comportements overlay (placement + interaction)** - Glisser/accroche, menu contextuel, persistance, autostart et recalibrage
- [ ] **Phase 7 : Packaging + déploiement** - Exe self-contained mono-fichier win-x64 testé sur machine propre

## Phase Details

### Phase 1 : Fondations architecture + squelette overlay
**Goal**: Une fenêtre overlay vide — borderless, transparente, always-on-top — s'affiche sur le bureau, portée par un graphe de services câblé dans App.xaml.cs (sans StartupUri) sur cible net8.0-windows.
**Depends on**: Rien (première phase)
**Requirements**: FEN-01, ROB-04
**Success Criteria** (what must be TRUE):
  1. Au lancement, une fenêtre sans bordure, transparente et sans entrée dans la barre des tâches apparaît sur le bureau.
  2. La fenêtre reste affichée au-dessus des autres fenêtres au fil du temps et ne prend pas le focus au démarrage (ShowActivated=false, Topmost réaffirmé sans vol de focus).
  3. L'application se lance et se ferme proprement en libérant ses ressources (cycle de vie DI possédé et disposé).
**Plans**: 3 plans
Plans:
- [x] 01-01-PLAN.md — Scaffold solution + composition root Generic Host + fenêtre overlay conforme (FEN-01, SC3), wave 1
- [x] 01-02-PLAN.md — P/Invoke + TopmostGuard : réaffirmation périodique du Topmost sans vol de focus (ROB-04), wave 2
- [ ] 01-03-PLAN.md — Smoke test visuel de l'overlay (gate manuel FEN-01/ROB-04/SC3), wave 3
**UI hint**: yes

### Phase 2 : Découverte des sources (bloquante)
**Goal**: La méthode d'obtention de l'objet d'usage Claude Code (five_hour/seven_day : utilization + resets_at) est établie empiriquement et documentée dans docs/data-sources.md, préalable strict à tout code de provider.
**Depends on**: Rien (préalable bloquant pour la Phase 3)
**Requirements**: DAT-01
**Success Criteria** (what must be TRUE):
  1. Le fichier docs/data-sources.md existe et localise précisément l'objet d'usage (five_hour/seven_day) avec un échantillon réel capturé.
  2. Le schéma des champs utilization/resets_at et la structure des transcripts JSONL (~/.claude/projects) y sont documentés.
  3. Les hypothèses et points de fragilité (source non documentée, susceptible de casser) sont consignés pour guider l'abstraction IUsageProvider.
**Plans**: 1 plan
Plans:
- [x] 02-01-PLAN.md — Rédiger docs/data-sources.md : source primaire rate_limits/statusLine + repli JSONL + mapping UsageSnapshot + hypothèses/fragilités (DAT-01), wave 1

### Phase 3 : Modèles + pipeline de données
**Goal**: Un pipeline de données neutre produit des UsageSnapshot immuables — fiables depuis l'objet primaire ou estimés depuis les JSONL — entièrement isolé du cadran, sans aucun type WPF.
**Depends on**: Phase 2
**Requirements**: DAT-02, DAT-03, DAT-04, DAT-05, DAT-06, DAT-07, ROB-02
**Success Criteria** (what must be TRUE):
  1. Un provider composite renvoie un snapshot d'usage : issu de l'objet primaire s'il est lisible, sinon estimé depuis les transcripts JSONL.
  2. Chaque snapshot porte sa provenance (Exact/Estimated), l'utilization, le resets_at et la fraction de temps restante des deux fenêtres.
  3. Le parsing tolère les lignes et champs invalides ainsi que la dernière ligne JSONL partielle, sans jamais échouer ni inventer de valeur.
  4. La couche Services ne référence aucun type WPF (contrat neutre partagé).
**Plans**: 4 plans
Plans:
- [x] 03-01-PLAN.md — Fondations neutres : modèles immuables + IClock + IUsageProvider + ChronosPaths + garde de pureté WPF (DAT-02, DAT-03, DAT-07), wave 1
- [x] 03-02-PLAN.md — Source primaire : pont Node non destructif + ClaudeUsageObjectProvider (usage.json) tolérant (DAT-04, ROB-02), wave 2
- [x] 03-03-PLAN.md — Repli JSONL + composite + enregistrement DI (DAT-05, DAT-06, ROB-02), wave 3
- [x] 03-04-PLAN.md — Installation idempotente du pont dans ~/.claude/settings.json + vérification humaine live (DAT-04), wave 3
**UI hint**: no

### Phase 4 : Orchestration refresh + ViewModel temps réel
**Goal**: Les données se rafraîchissent automatiquement (deux horloges distinctes) et alimentent un ViewModel qui interpole l'affichage à la seconde, tout franchissement de thread passant par un point de marshaling unique.
**Depends on**: Phase 3
**Requirements**: RAF-01, RAF-02, RAF-03, RAF-04
**Success Criteria** (what must be TRUE):
  1. Une écriture sur une source déclenche une relecture débouncée ; un timer périodique garantit la fraîcheur en filet de sécurité (gestion de l'événement Error incluse).
  2. Le compte à rebours et la longueur des arcs progressent chaque seconde par interpolation, sans I/O disque.
  3. Toute mise à jour issue d'un thread de fond atteint l'UI via un point de marshaling unique (IUiDispatcher), sans InvalidOperationException.
**Plans**: 2 plans
Plans:
- [x] 04-01-PLAN.md — RefreshOrchestrator neutre : watcher débouncé + PeriodicTimer + Channel, event SnapshotChanged (RAF-01, RAF-02), wave 1
- [x] 04-02-PLAN.md — MainViewModel temps réel (interpolation + marshaling) + formateur FR + câblage App/MainWindow (RAF-03, RAF-04), wave 2

### Phase 5 : Cadran (RingArc + converters) + câblage View
**Goal**: L'utilisateur voit le cadran complet à deux anneaux refléter en temps réel l'état des quotas, branché sur le flux de données déjà éprouvé.
**Depends on**: Phase 4 (et Phase 1 pour la fenêtre hôte)
**Requirements**: CAD-01, CAD-02, CAD-03, CAD-04, CAD-05, CAD-06, CAD-07, DAT-08, ROB-01
**Success Criteria** (what must be TRUE):
  1. L'utilisateur voit un cadran sombre gradué (ticks mineurs/majeurs) avec deux arcs dont la longueur reflète le temps restant avant reset (5 h à l'extérieur, hebdo à l'intérieur).
  2. La couleur de chaque arc passe du vert à l'ambre au rouge selon l'utilization, et au gris « quota épuisé » quand utilization ≥ 1.
  3. Un compte à rebours texte des deux fenêtres s'affiche au centre du cadran.
  4. Les données issues du repli sont marquées visuellement « estimée » ; un état « données indisponibles » s'affiche sans crash quand aucune source n'est lisible.
**Plans**: 3 plans
Plans:
- [x] 05-01-PLAN.md — Primitives de rendu pures : ArcGeometry (angle→arc, cas limites) + RampColor (rampe 3 stops) + tests (CAD-04, CAD-07), wave 1
- [x] 05-02-PLAN.md — Enveloppes WPF : RingArc (Shape), TickRing (graduations), UtilizationToBrushConverter + tests (CAD-01, CAD-04, CAD-05, CAD-07), wave 2
- [x] 05-03-PLAN.md — Composition MainWindow.xaml (tokens, 2 arcs, countdown, badges) + smoke + checkpoint visuel (CAD-01/02/03/05/06, DAT-08, ROB-01), wave 3
**UI hint**: yes

### Phase 6 : Comportements overlay (placement + interaction)
**Goal**: L'utilisateur peut placer, ranger, régler et faire persister l'overlay entièrement via ses interactions, sur tous ses moniteurs.
**Depends on**: Phase 5
**Requirements**: FEN-02, FEN-03, FEN-04, FEN-05, FEN-06, FEN-07, ROB-03, DEP-02
**Success Criteria** (what must be TRUE):
  1. L'utilisateur déplace l'overlay par glisser ; au relâchement il s'accroche au coin d'écran le plus proche, sur tous les moniteurs (WorkingArea, DPI).
  2. L'utilisateur bascule l'overlay en arrière-plan et le ramène au premier plan.
  3. Un menu contextuel clic droit donne accès à Réglages, Arrière-plan, Recalibrer et Quitter (seul point d'accès et de sortie).
  4. La position, le coin et les réglages sont persistés dans %APPDATA%/Chronos/settings.json et restaurés au lancement suivant.
  5. L'utilisateur active le lancement au démarrage Windows (shell:startup) et recalibre le reset hebdo best-effort.
**Plans**: 4 plans
Plans:
- [x] 06-01-PLAN.md — Fondations neutres : CornerSnap pur + SettingsService atomique + WeeklyRecalibration (FEN-03, FEN-07, ROB-03), wave 1
- [x] 06-02-PLAN.md — Interop moniteur/DPI + TopmostGuard Suspend/Resume + AutostartService .lnk (FEN-05, DEP-02), wave 1
- [x] 06-03-PLAN.md — OverlayController (placement physique) + drag/snap + hook écran + restauration au lancement (FEN-02, FEN-03, FEN-04, FEN-05, FEN-07), wave 2
- [x] 06-04-PLAN.md — Menu contextuel + commandes VM + dialogue recalibrage + autostart + checkpoint UAT (FEN-05, FEN-06, FEN-07, ROB-03, DEP-02), wave 3
**UI hint**: yes

### Phase 7 : Packaging + déploiement
**Goal**: L'application se distribue en un exécutable unique autonome, une fois le comportement stable, et fonctionne sur une machine propre.
**Depends on**: Phase 6
**Requirements**: DEP-01
**Success Criteria** (what must be TRUE):
  1. La publication produit un exe self-contained mono-fichier win-x64 (PublishSingleFile, natives auto-extraites, PublishTrimmed=false).
  2. L'exe se lance et fonctionne sur une machine propre sans runtime .NET préinstallé.
  3. Le lancement au démarrage reste fonctionnel (chemin stable) après déplacement de l'exe.
**Plans**: 1 plan
Plans:
- [ ] 07-01-PLAN.md — Finaliser csproj + profil publish, publier + smoke exe publié + non-régression, doc publish/autostart (DEP-01), wave 1

## Progress

**Execution Order:**
Les phases s'exécutent dans l'ordre numérique : 1 → 2 → 3 → 4 → 5 → 6 → 7

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Fondations architecture + squelette overlay | 0/3 | Planned | - |
| 2. Découverte des sources (bloquante) | 0/1 | Planned | - |
| 3. Modèles + pipeline de données | 0/4 | Planned | - |
| 4. Orchestration refresh + ViewModel temps réel | 0/2 | Planned | - |
| 5. Cadran (RingArc + converters) + câblage View | 0/3 | Planned | - |
| 6. Comportements overlay (placement + interaction) | 0/4 | Planned | - |
| 7. Packaging + déploiement | 0/TBD | Not started | - |
