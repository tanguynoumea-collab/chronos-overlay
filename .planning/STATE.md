---
gsd_state_version: 1.0
milestone: v1.6
milestone_name: — Observer au lieu de déduire (widget de sessions)
status: verifying
stopped_at: Completed 22-03-PLAN.md
last_updated: "2026-09-12T16:19:25.509Z"
last_activity: 2026-09-12
progress:
  total_phases: 6
  completed_phases: 2
  total_plans: 7
  completed_plans: 7
  percent: 33
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-09-12)

**Core value:** Voir instantanément, sans terminal ni /usage, combien de quota et de temps il reste — sans
jamais présenter une estimation comme un chiffre exact. Et savoir quelle session m''attend.
**Current focus:** Phase 22 — Un instrument de mesure qui ne ment plus

## Current Position

Milestone: v1.6 — Observer au lieu de déduire
Phase: 22 (Un instrument de mesure qui ne ment plus) — EXECUTING
Plan: 3 of 3
Status: Phase complete — ready for verification
Last activity: 2026-09-12

Progress: [███░░░░░░░] 33%  (2 phases sur 6 — phases 21 et 22 closes, 7 plans sur 7)

## Performance Metrics

- Total plans completed (v1.6): 7
- Suite de tests : **747 verts / 0 échec** (baseline d'entrée de phase 22 : 719)

## Milestone v1.5 (clos)

Livré le 2026-09-12 : 6 phases (15-20), 24 exigences, 328 → 752 tests verts. Exe 3.0.2 publié et vérifié en
production. Détail dans .planning/v1.5-MILESTONE-AUDIT.md.

## Contexte technique v1.6 (investigation DÉJÀ FAITE — ne pas re-enquêter)

Rapport complet : **.planning/debug/widget-sessions-statuts.md**. Trois causes racines distinctes, prouvées
contre les classes réelles et contre la doc officielle des hooks Claude Code.

1. **« Réfléchit » déduit par expiration, jamais observé.** Working n''est écrit que par UserPromptSubmit et
   SessionStart ; rien ne confirme que le travail continue. Et SessionMonitor.Read arbitre par ORDRE
   D''INSERTION : un hook de 7 h écrase un transcript de 10 s (mesuré).

2. **« Attend » : sémantique fausse à la source.** Stop ne se déclenche PAS sur interruption utilisateur.
   Notification est une alerte « tu sembles absent », couvrant permission ET inactivité ET fin de tâche.
   Un événement PermissionRequest DÉDIÉ existe et n''est pas utilisé (~30 événements au catalogue, 5 utilisés).

3. **« Traité » impossible en terminal.** SessionTreatmentTracker exige Origin == Desktop et un id
   desktop:foreground:* ; une session Claude Code a Origin = Cli et un UUID. Et NET-01 confond « l''utilisateur
   a répondu » avec « ma source a expiré » : preuve arithmétique, 478 min vs DropAfter 480 min → une session
   réellement en attente masquée 6 h. Le tracker est en mémoire : le traité ne survit pas au redémarrage.

**Amplificateurs prouvés :** DiagnosticService construit son propre SessionMonitor NU (sans filtre traité,
sans source bureau) et liste files.Take(8) par ordre alphabétique — l''instrument de mesure était faussé, ce
qui explique probablement que le problème n''ait jamais été élucidé. Take(12) est appliqué AVANT le filtre
isSidechain alors que 94 % des transcripts sont des agent-*.jsonl. Aucun balayage d''expiration nulle part :
54 fichiers d''état dont 48 de plus de 7 jours, + 12 .tmp orphelins. ArchiveStore applique un TTL de 6 h
alors que son contrat annoncé est « permanent ».

**Ce qui N''EST PAS la cause :** le fan-out des 25 hooks concurrents (corrigé en phase 15) n''a produit que
des débris, pas de corruption — poids mesuré ~0,7 événement perdu sur 5 000.

## Accumulated Context

### Contexte technique v1.5 (diagnostic DÉJÀ ÉTABLI — ne pas re-enquêter)

Diagnostic mené le 2026-09-09 sur la machine réelle. Cause racine des pourcentages faux après le passage
Max x5 → Max x20 : **les trois sources exactes sont tombées en même temps et le composite dégrade en silence.**

- **Jeton OAuth Chronos expiré le 2026-07-12.** `GET https://api.anthropic.com/api/oauth/usage` → **HTTP 401**
  (vérifié). `ChronosOAuthUsageProvider` rafraîchit paresseusement, l'échec est totalement muet côté UI.

- **`%APPDATA%/Chronos/usage.json` figé au 2026-07-10**, contenu
  `{"five_hour":{"used_percentage":10,"resets_at":9}}` — `resets_at: 9` = epoch 1970. Le pont statusLine n'est
  visiblement jamais invoqué par l'app de bureau. `ClaudeUsageObjectProvider` n'applique **aucune limite d'âge**
  et renvoie ça marqué `Exact`.

- **`CompositeUsageProvider.Best()` classe uniquement par fiabilité** (`Exact > Estimated > Unavailable`) :
  une donnée « exacte » de deux mois bat donc une estimation fraîche.

- **`ChronosOAuthUsageProvider._cached` est un champ d'instance (RAM)** → à chaque démarrage de l'exe, aucun
  chiffre exact en mémoire, bascule immédiate sur l'estimation. Cas le plus fréquent, jamais identifié.

- **`MainViewModel.cs:269` calcule `IsStale`** (seuil 2 min) mais la propriété n'est **bindée nulle part**.
- **`settings.json`** : `FiveHourTokenBudget=230000000` en source `Manual` (donc `BudgetCalibration.ApplyAuto`
  refuse par conception de l'écraser → gelé à vie), `WeeklyTokenBudget=5817635413` calibré sous Max x5.

- **`BudgetAutoCalibrator` a un défaut logique** : il ne calibre que lorsqu'une source *exacte* est présente,
  c'est-à-dire précisément quand l'estimation ne sert pas.

- **`~/.claude/settings.json` contient 25 hooks Chronos au lieu de 5** (v2.5, v2.5.1, v2.6, v2.8.1 + le build
  Debug), aucun installateur ne retire les précédents. Le `statusLine` pointe un chemin `Downloads` en dur.

- **`.credentials.json` de Claude Code ne contient que `mcpOAuth`** sur cette machine (pas de
  `claudeAiOauth.accessToken`) : la lecture de jeton « à la claude-session-browser » n'y marcherait pas telle
  quelle ; il faut passer par le jeton propre de Chronos.

**Technique reprise de `github.com/juppeee/claude-session-browser`** (`clawdmeter.py:150`, `poll_usage_meta`) :

```
POST https://api.anthropic.com/v1/messages
  Authorization: Bearer <token>
  anthropic-beta: oauth-2025-04-20
  User-Agent: claude-code/<version>
  body {"model":"claude-haiku-4-5-...","max_tokens":1,"messages":[{"role":"user","content":"hi"}]}
→ en-têtes anthropic-ratelimit-unified-{5h,7d}-{utilization,reset}, -5h-status, et variante -overage-*
```

Ils récupèrent les en-têtes **même sur un 429** (`except HTTPError: hdrs = e.headers`) — c'est l'avantage
décisif sur `/api/oauth/usage`.

**PIÈGE D'UNITÉ — trois unités pour la même donnée, à normaliser en un seul point :**

| Source | Champ | Unité |
|---|---|---|
| En-têtes `anthropic-ratelimit-unified-*` | `utilization` | **0..1** |
| `GET /api/oauth/usage` | `utilization` | **0..100** |
| Pont statusLine (`rate_limits`) | `used_percentage` | **0..100** (= `utilization*100`, confirmé dans le binaire `claude-2.1.87`) |

`resets_at` : **epoch secondes** pour les en-têtes et le pont statusLine, **ISO 8601** pour `/api/oauth/usage`.

**Fondement de la décision « plus d'estimation absolue »** : les limites Anthropic pondèrent par modèle (une
heure d'Opus ne pèse pas comme une heure de Haiku), donc `tokens / plafond` restera faux même avec le bon
plafond. Les transcripts ne peuvent répondre qu'à deux questions bornées : *activité depuis T ?* et
*tokens depuis T ?* — d'où la correction par delta.
| Phase 15 P01 | 12min | 3 tasks | 3 files |
| Phase 15 P02 | 7min | 3 tasks | 6 files |
| Phase 15 P03 | 20min | 4 tasks | 4 files |
| Phase 16 P01 | 24min | 3 tasks | 20 files |
| Phase 16 P02 | 9min | 2 tasks | 6 files |
| Phase 16 P03 | 29min | 3 tasks | 12 files |
| Phase 16 P04 | 24 | 3 tasks | 7 files |
| Phase 17 P01 | 18 | 2 tasks | 2 files |
| Phase 17 P02 | 7min | 2 tasks | 6 files |
| Phase 17 P03 | 10min | 2 tasks | 4 files |
| Phase 17 P04 | 26min | 2 tasks | 7 files |
| Phase 17 P05 | 19min | 3 tasks | 9 files |
| Phase 18 P01 | 14min | 3 tasks | 8 files |
| Phase 18 P02 | 18min | 3 tasks | 10 files |
| Phase 18 P03 | 18min | 3 tasks | 3 files |
| Phase 18 P04 | 22min | 2 tasks | 2 files |
| Phase 18 P05 | 24min | 2 tasks | 5 files |
| Phase 18 P06 | 18min | 3 tasks | 6 files |
| Phase 19 P01 | 24min | 3 tasks | 10 files |
| Phase 19 P02 | 15min | 3 tasks | 8 files |
| Phase 19 P03 | 38 min | 3 tasks | 6 files |
| Phase 19 P04 | 19min | 2 tasks | 7 files |
| Phase 19 P05 | 16min | 2 tasks | 2 files |
| Phase 20 P01 | 34min | 3 tasks | 9 files |
| Phase 20 P02 | 10min | 3 tasks | 15 files |
| Phase 20 P03 | 16min | 3 tasks | 12 files |
| Phase 20 P04 | 18min | 3 tasks | 2 files |
| Phase 20 P05 | 14min | 3 tasks | 5 files |
| Phase 21 P01 | 14min | 2 tasks | 3 files |
| Phase 21 P02 | 12min | 2 tasks | 11 files |
| Phase 21 P03 | 8min | 3 tasks | 18 files |
| Phase 21 P04 | 9min | 3 tasks | 5 files |
| Phase 22 P01 | 6 min | 2 tasks | 6 files |
| Phase 22 P02 | 6 min | 2 tasks | 5 files |
| Phase 22 P03 | 9 min | 2 tasks | 3 files |

### Decisions

- [v1.6/roadmap]: **6 phases, numérotées 21 → 26** (continuité après la phase 20 de v1.5). Couverture
  18/18 requirements, aucun orphelin, aucun doublon. Granularité `standard`.

- [v1.6/roadmap]: **SRC-01/02/03 en Phase 21, en tête et sans discussion.** Le retrait de la source
  app-bureau supprime ~690 lignes de `src/` et ~530 de tests, fait tomber `WindowsForegroundWatch` /
  `IForegroundWatch`, retire `SessionKind` / `SessionOrigin` de `SessionSnapshot` et trois arguments de
  construction de `SessionMonitor`. Toute phase antérieure travaillerait sur un terrain démoli juste après.
  SRC-03 (limite de transcripts appliquée APRÈS le filtre sous-agents) y est joint : même fichier de source,
  même question de périmètre.

- [v1.6/roadmap]: **OBS-01 placé TÔT (Phase 22) et non en fin de milestone — choix contraire à l'ordre naïf,
  assumé.** L'argument « il documenterait un état intermédiaire » ne tient que si OBS-01 est livré comme une
  COPIE du comportement du widget. Livré comme **partage d'instance** (le diagnostic interroge le moniteur du
  widget au lieu d'en reconstruire un nu), il reste vrai à chaque commit ultérieur par construction. Le
  placer en fin ferait vérifier les phases 23 à 26 avec l'instrument faussé qui a empêché d'élucider le
  problème pendant des mois. Placé après la Phase 21 seulement pour ne pas le recâbler deux fois.

- [v1.6/roadmap]: **CYC en Phase 23, AVANT FUS (24) — c'est une dépendance, pas un ménage.** L'arbitrage par
  fraîcheur compare des horodatages ; une écriture perdue en silence (mesuré : 200 échecs sur 500 par
  `tmp`+`Move` sous lecteur concurrent) fait paraître un état plus vieux qu'il n'est et empoisonne
  l'arbitrage à sa racine. CYC-01 assainit en outre le terrain de mesure humain (54 fichiers dont 48 > 7 j,

  + 12 `.tmp`).

- [v1.6/roadmap]: **FUS (24) avant EVT (25).** EVT-03 (battements de cœur) n'a aucune valeur sans FUS-01 :
  un battement frais serait écrasé par un hook ancien, exactement le défaut mesuré (hook de 7 h battant un
  transcript de 10 s). Les battements multiplient aussi le volume d'écritures, d'où la Phase 23 en amont.

- [v1.6/roadmap]: **EVT-01..05 gardés dans UNE phase (25).** EVT-05 documente le contrat FINAL : le séparer
  ferait documenter un contrat en cours de refonte. Les cinq touchent le même chemin
  (`App.xaml.cs` mode `--hook`, `SessionHookProcessor`, `SessionHookInstaller`).

- [v1.6/roadmap]: **TRT en Phase 26, dernière.** « Traité » = transition observée sur la MÊME source : la
  notion de « même source » et de fraîcheur (24) et les transitions observables (25) doivent exister avant.

- [v1.6/roadmap]: **Phases 21, 25 et 26 marquées « UI hint: yes »** — 21 retire le libellé de type bindé dans
  les 8 styles, 25 change le vocabulaire d'état affiché, 26 ajoute un geste explicite (TRT-03) dans
  `SessionsWindow.xaml` / `SessionsController`. Toute modification visuelle doit rester cohérente sur les
  **8 styles de session** et les **9 thèmes**. Note : `ui_phase: false` dans config.json — l'indice est
  consigné, aucune phase UI dédiée n'est déclenchée.

- [v1.6/roadmap]: Toute phase doit conserver vertes les **752 tests xUnit** (suite ~4 s) et les gardes
  `ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests`, `GardesDoctrineTests`,
  `La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte`. Exception explicite en Phase 21 : le recul du
  compte de tests y est le LIVRABLE (code supprimé), critère = 0 échec + justification nominative.

- [v1.5/roadmap]: **6 phases, numérotées 15 → 20** (continuité après la phase 14 de v1.4). Couverture
  24/24 requirements, aucun orphelin, aucun doublon.

- [v1.5/roadmap]: **EXA-01 (persistance du dernier relevé exact) est placé en Phase 16, avant la refonte du
  composite (Phase 19)** : la correction par delta (DEL-03/04) est impossible sans un instant T de référence
  persisté.

- [v1.5/roadmap]: **DEL-05 (démolition des plafonds) et DEL-01/02 (JSONL en source de delta) sont dans la même
  phase (16)** : ils touchent le même code et cassent les mêmes tests ; les séparer produirait un état
  intermédiaire non compilable. DEL-06 (migration des réglages) les accompagne pour la même raison.

- [v1.5/roadmap]: **HDR-01..06 en Phase 18, AVANT la refonte du composite (Phase 19)** : la source en-têtes
  est indépendante (un `IUsageProvider` de plus en tête de chaîne), mais la placer avant évite de rétrofitter
  sa place dans la nouvelle doctrine.

- [v1.5/roadmap]: **TOK-01..03 en Phase 17, avant HDR** : sans jeton vivant, ni l'endpoint OAuth existant ni
  la sonde d'en-têtes ne répondent — le jeton conditionne l'utilité réelle de la Phase 18.

- [v1.5/roadmap]: **PUR-01..03 en Phase 15** : totalement indépendant du pipeline d'usage, court, plaçable
  n'importe où ; placé en tête pour assainir le terrain de mesure tout de suite.

- [v1.5/roadmap]: **Phases 17 et 20 marquées « UI hint: yes »** (pastille de déconnexion cliquable TOK-02/03 ;
  distinction visuelle frais / daté / indisponible EXA-03). Les autres phases sont purement services/données.
  Note : `ui_phase: false` dans config.json — l'indice est consigné mais aucune phase UI dédiée n'est déclenchée.

- [v1.5/roadmap]: Toute phase doit conserver vertes les **327 tests xUnit**, la garde de pureté
  `ServicesLayerPurityTests` et la garde de composition `CompositionRootTests`.

- [v1.4/roadmap]: **2 phases à dépendance forte** — la source UIA (Phase 13) avant l'hystérésis (Phase 14).
  La distinction Chat/Cowork/Code et l'acquittement par focus (NET-02) EXIGENT l'arbre UIA ; la branche
  « répondu » (NET-01) roule déjà sur les sources actuelles. On ne peut donc pas inverser l'ordre.

- [v1.4/roadmap]: Phase 13 = BUR-01..05 + ROB-06 + ROB-07 (7 req) ; Phase 14 = NET-01..04 (4 req).
- [v1.4/roadmap]: Les deux phases sont **UI hint: yes** (surface visuelle WPF du widget sessions).
- [Phase 13]: [13-02] WindowsUiaTreeProvider propage AutomationId (dont l'ancre RootWebArea) → foreground reconnu en PROD, pas seulement en test. Assemblies UIA fournies implicitement par UseWPF (aucun <Reference>, 0 warning). Poll de fond via Timer .NET hors thread UI (ROB-07).
- [Phase 13]: 13-03: source bureau fusionnée dans SessionMonitor.Read (ISessionSource? optionnel, 4e arg non cassant), après transcripts+hooks, avant archived, non bloquant (ROB-07)
- [Phase 13]: 13-03: garde DI réelle (CompositionRootTests) reproduisant la sous-chaîne bureau d'App.xaml.cs → attrape un service manquant/mal ordonné qui ne planterait qu'au démarrage
- [Phase 14]: [14-01] Réversibilité NET-03 portée par le tracker (purge sur nouvel épisode d'attente), PAS par une comparaison ts>=UpdatedAt dans le filtre (fausse pour le bureau: UpdatedAt==now à chaque poll). Horodatage d'épisode maintenu par le tracker.
- [Phase 14]: [14-02] Focus premier-plan OS réel (WindowsForegroundWatch, Win32 GetForegroundWindow, titre « Claude ») injecté 7e param de SessionMonitor => branche NET-02 VIVANTE en prod. Best-effort ne lève jamais ; couche neutre (aucun HWND public). NET-01..04 tous couverts, phase 14 complète, milestone v1.4 prêt pour audit.
- [Phase 15]: [15-01] Une entree Chronos s'identifie par le MARQUEUR d'argument (--hook/--statusline) + le NOM de fichier Chronos*.exe, JAMAIS par le chemin : le chemin est la variable qui a produit 25 groupes de hooks au lieu de 5.
- [Phase 15]: [15-01] ParseOrNull renvoie null (= NE RIEN ECRIRE) et jamais un objet vide : le repli 'objet vide' des installateurs actuels ecrase integralement le settings.json de l'utilisateur. Catch LARGE + materialisation forcee (_ = o.Count) car les cles dupliquees levent une ArgumentException, pas une JsonException.
- [Phase 15]: [15-01] Appartenance (IsChronosCommand) et fraicheur (PointsToExe) sont deux predicats distincts ; Serialize impose UnsafeRelaxedJsonEscaping pour ne pas mutiler les accents des valeurs des autres outils.
- [Phase 15]: [15-02] L'installation ne cherche plus si un groupe Chronos existe : elle retire TOUS les groupes Chronos puis ajoute le sien. Idempotent par construction — la robustesse ne depend plus de l'exactitude du predicat mais de la forme de l'algorithme.
- [Phase 15]: [15-02] TransformForUninstall / Uninstall perdent exePath des deux cotes : le retrait est volontairement LARGE (toutes les versions), sinon desactiver depuis une nouvelle version laisserait les hooks et la barre de toutes les anciennes.
- [Phase 15]: [15-02] statusLine est mute sur la seule cle command (padding et cles futures survivent) ; ApplyStatusLine repointe mais n'installe JAMAIS — le consentement reste porte par le menu, pas par la reconciliation.
- [Phase 15]: [15-03] La comparaison avant/apres se fait sur la forme NORMALISEE (serialiser avant/apres mutation), jamais sur le texte lu : une simple reindentation de Claude Code declencherait sinon une ecriture ET une sauvegarde a chaque demarrage, evincant la sauvegarde du premier passage — la seule a contenir l'etat pre-purge complet.
- [Phase 15]: [15-03] Sauvegarde BLOQUANTE (pas d'ecriture sans sauvegarde reussie) mais retention NON bloquante ; le reconciliateur ne cree jamais le fichier absent — purge et repointage seulement, jamais d'installation silencieuse.
- [Phase 15]: [15-03] Point d'appel unique dans App.OnStartup, apres window.Show() et avant OfferOnFirstRun() : atteint en mode overlay UNIQUEMENT, donc jamais 5 processus --hook concurrents en lire-modifier-ecrire.
- [Phase 16]: [16-01] Le recul 405 -> 384 tests est le LIVRABLE : les 21 tests supprimes couvraient exclusivement du code supprime (3 CalibrateBudgets, 3 BudgetAutoCalibrator, 15 BudgetCalibration). Critere = 0 echec + justification nominative, PAS >= 405.
- [Phase 16]: [16-01] BudgetSource.cs et les 6 champs de ChronosSettings laisses INTACTS : ChronosSettings les reference encore, les supprimer ici casserait la compilation. Verifie : exactement 3 occurrences de BudgetSource dans src/ -> le plan 16-04 a bien son objet.
- [Phase 16]: [16-01] dotnet clean ne nettoie QUE la configuration courante (Debug) : 2 des 3 copies obj/**/BudgetDialog.g.cs survivaient. Apres suppression d'un .xaml, nettoyer Debug ET Release puis purger explicitement les *.g.cs orphelins.
- [Phase 16]: [16-02] Une fenetre n'est persistable que si elle est Exact ET porte utilization ET resets_at : sans resets_at, ni detection de la remise a zero ni recalcul de la geometrie -> chiffre invérifiable. Meme regle au chargement.
- [Phase 16]: [16-02] Reliability et FractionTimeRemaining ne sont JAMAIS persistees : la premiere est forcee a Exact a la reconstruction (un fichier edite a la main ne peut pas injecter une fausse exactitude), la seconde est recalculee depuis ResetsAt et l'instant present (la persister ressusciterait une geometrie perimee).
- [Phase 16]: [16-02] La relecture de fusion de Save est BRUTE (sans la garde resets_at <= now) : seule Load invalide une fenetre roulee, la fusion ne doit jamais purger silencieusement.
- [Phase 16]: [16-02] Le decorateur LastExactUsageProvider rebouche UNIQUEMENT les fenetres Unavailable (jamais Estimated) et n'ecrit que de l'Exact : portee volontairement la plus etroite possible, la doctrine de choix de source reste la phase 19.
- [Phase 16]: [16-03] La source de delta n'implemente PAS IUsageProvider et un test reflexif l'interdit : EXA-04 devient structurel des la phase 16, pas seulement documente. Rebrancher les transcripts dans la chaine casse le test.
- [Phase 16]: [16-03] Le contrat de delta separe l'E/S (ReadAsync, une seule passe disque) de la requete (Since(t), PURE, appelable N fois) : les deux fenetres 5 h et hebdo ont des bornes basses differentes mais repondent depuis un SEUL instantane disque, donc coherentes par construction.
- [Phase 16]: [16-03] Horizon/Covers font partie du contrat : une interrogation anterieure au filtre mtime de 8 j est declaree NON COUVERTE plutot que servie sous-evaluee. La constante 8 j est declaree UNE fois (cutoff du scan = horizon annonce, ils ne peuvent pas diverger).
- [Phase 16]: [16-03] Borne basse STRICTEMENT exclusive, borne haute inclusive : le message pose exactement sur l'instant du releve exact a deja ete compte par le serveur, le recompter le compterait deux fois.
- [Phase 16]: [16-03] L'isolation des chemins n'a PAS stabilise Host_resout_et_dispose_les_singletons : la cause reelle est une course du chargeur BAML de WPF (WpfXamlType.FindKnownMember) sous parallelisme xUnit. Correctif : collection 'XAML WPF' DisableParallelization sur les 4 classes qui chargent du XAML.
- [Phase 16]: DEL-06 livré comme TEST, pas comme code : System.Text.Json ignore par défaut les membres non mappés (skip au niveau du LECTEUR, avant conversion) — un migrateur aurait été du code mort pour un problème inexistant.
- [Phase 16]: La fixture DEL-06 est une copie octet pour octet du settings.json RÉEL de production : une fixture régénérée par sérialisation ne contiendrait pas les champs obsolètes et ne prouverait rien.
- [Phase 16]: La garde de non-retour Budget* porte sur le NOM des types et sa falsifiabilité a été vérifiée en exécution (type témoin introduit, échec constaté, témoin retiré).
- [Phase 17]: [17-01] Wave 0 : 3 defauts d'avant-phase graves en tests VERTS (401 muet, refresh paresseux, recul 429 inoperant sans cache). Ils seront REECRITS par 17-02/17-04, jamais supprimes — trace executable du bug avant/apres.
- [Phase 17]: [17-01] DEFAUT NON PREVU AU PLAN : le garde-fou anti-429 (ChronosOAuthUsageProvider.cs:53) exige 'now < _nextAllowedCall ET _cached is not null'. _cached vivant en RAM, il est vide a CHAQUE demarrage de l'exe : le recul 429 ne freine alors rien du tout. 17-04 doit porter le backoff dans l'autorite de jeton, independamment de tout cache d'usage.
- [Phase 17]: [17-01] TOK-01/TOK-02 laisses Pending malgre le frontmatter du plan : la Wave 0 ne livre que la couverture et prouve au contraire que les requirements ne sont PAS satisfaits. Ils seront coches par les plans qui livrent le correctif.
- [Phase 17]: [17-02] La cause d'un echec de refresh est desormais typee : 429 rate_limit_error = EchecTemporaire (jamais 'deconnecte' — ce serait une fausse alerte sur un compte sain), 200 a corps inexploitable = IdentifiantsRejetes (la rotation a deja eu lieu cote serveur, reessayer est vain).
- [Phase 17]: [17-02] ResultatRafraichissement n'expose AUCUNE propriete string, et un test reflexif (GetProperties) l'impose : un corps d'erreur peut echoiser la requete, donc le refresh token. La securite est structurelle, pas conventionnelle.
- [Phase 17]: [17-02] EtatAuthentification a 4 valeurs et pas 3 : HorsLigne et Deconnecte sont irreductibles. IAuthStatus copie le motif RefreshOrchestrator.SnapshotChanged (le service expose l'event, l'abonne marshalle) — ServicesLayerPurityTests reste vert sans toucher son allow-list.
- [Phase 17]: [17-02] TOK-01/TOK-02 laisses Pending : ce plan ne cree ni autorite, ni service de fond, ni pastille. IAuthStatus et EtatAuthentification sont des contrats sans implementation a ce commit — intentionnel, 17-03 implemente, 17-05 consomme.
- [Phase 17]: [17-03] Autorite UNIQUE de jeton : SemaphoreSlim(1,1) + double-verification => N demandes concurrentes produisent UNE rotation, jamais N. Parade structurelle a la course de rotation du refresh token (bug claude-code#25609) qui fabriquerait une FAUSSE deconnexion sur un compte sain.
- [Phase 17]: [17-03] Le recul (backoff 2->30 min) vit DANS l'autorite et ne consulte AUCUN cache d'usage : c'est la reponse directe au defaut n.3 decouvert par 17-01 (le garde-fou du provider exige un _cached en RAM, donc vide a chaque demarrage de l'exe). Un recul adosse a un cache disparait precisement au redemarrage, c'est-a-dire quand le martelement se produit.
- [Phase 17]: [17-03] Aucun .Clear() dans l'autorite, et c'est teste : apres un 400 invalid_grant, oauth.dat existe toujours ET contient encore l'ancien refresh token. Parade au faux positif serveur documente (claude-code#54443). Un Save en echec ne fait pas croire a un echec de refresh : les jetons neufs restent en memoire.
- [Phase 17]: [17-03] TokenRefreshService : tick FIXE 60 s + predicat pur d'horloge murale, dueTime ZERO (premier tick immediat), TickAsync public et non-levant. Pas de reveil calcule sur ExpiresAt (casse a la mise en veille) ; RefreshOrchestrator (horloge DONNEES) laisse intact.
- [Phase 17]: [17-03] TOK-01/TOK-02 laisses Pending : les deux types existent et sont prouves (39 tests) mais AUCUN n'est enregistre dans le graphe DI. Un TokenRefreshService que le host ne demarre jamais ne rafraichit rien — le cablage est le plan 17-04, en un seul commit avec le rebranchement du provider.
- [Phase 17]: [17-04] UN SEUL rafraichisseur : le provider d'usage perd _store/_client et son refresh paresseux dans le MEME commit ou l'autorite est cablee. Une demi-mesure aurait fait coexister deux rotations du refresh token, donc un invalid_grant sur la seconde, donc une FAUSSE deconnexion sur un compte sain (claude-code#25609).
- [Phase 17]: [17-04] Le rejeu sur 401 n'est pas un raffinement mais une obligation : le serveur peut revoquer un jeton AVANT son ExpiresAt local (claude-code#54443), donc le preventif seul ne suffit jamais. UN SEUL rejeu (garde locale non rearmable) ; 403 sans rejeu ; 429/5xx/reseau ne declarent JAMAIS Deconnecte.
- [Phase 17]: [17-04] Le garde-fou de debit du provider ne consulte PLUS le cache : 'ai-je le droit d'appeler' est decouple de 'ai-je un cache a servir'. Le cache vivant en RAM, le frein disparaissait a chaque demarrage de l'exe — precisement quand le martelement se produit (defaut n.3 de 17-01, dernier segment non corrige).
- [Phase 17]: [17-04] IAuthStatus est un ALIAS de l'instance d'autorite (jamais une seconde) et TokenRefreshService est enregistre ET heberge : la garde DI le prouve par Assert.Same + Assert.Contains sur IHostedService, ce qu'un dotnet build ne voit pas. TOK-01 fonctionnellement acquis, seule la visibilite manque (17-05).
- [Phase 17]: [17-04] DiagnosticService : parametre IAuthStatus OPTIONNEL en DERNIERE position => 9 sites de construction preexistants compilent sans retouche. 'Le fichier oauth.dat existe' n'a jamais voulu dire 'authentifie' : le rapport nomme desormais l'etat reel, HORS LIGNE (informatif) distingue de DECONNECTE (actionnable).
- [Phase 17]: [17-05] La pastille porte une commande DEDIEE (ReconnecterCommand) et JAMAIS LoginClaudeCommand : cette derniere bascule sur IsLoggedIn == _store.Exists, vrai meme avec un jeton expire -> un clic aurait SUPPRIME le coffre de jetons. Verrouille a deux niveaux : LogoutCount == 0 cote VM, et un [WpfFact] qui compare l'instance de commande reellement bindee dans le XAML.
- [Phase 17]: [17-05] Deux booleens de pastille et non un enum binde : Deconnecte est ACTIONNABLE (ambre, cliquable), HorsLigne est INFORMATIF (gris, inerte). Etat applique DES le ctor : sur cette machine le jeton est mort depuis le 2026-07-12, l'autorite est deja en echec au demarrage et n'emettra aucune transition. NonConnecte n'allume rien (EXA-05, phase 19).
- [Phase 17]: [17-05] ReinitialiserApresLogin a enfin un appelant (aucun depuis 17-02), suivi de RequestRefresh : sans quoi le verrou Deconnecte et le recul restaient poses et la pastille survivait a sa propre reparation. L'ORDRE des deux est reel en production mais INOBSERVABLE en test (RequestRefresh ne fait qu'empiler, consommation asynchrone) : declare non couvert plutot que teste faussement.
- [Phase 17]: [17-05] DEUX gardes de test MUETTES attrapees par mutation : (1) RefreshOrchestrator.TryTrigger ne renvoie PAS false quand le channel est plein (DropWrite renvoie true) ; (2) une fenetre WPF jamais affichee n'a pas de parent visuel pour son Content, donc le DataContext ne se propage pas et AUCUN binding ne s'evalue (Command null, Visibility=Visible par defaut). Parade : DataContext sur la grille racine + purge du Dispatcher.
- [Phase 18]: [18-01] Le plancher d'epoch 2020-01-01 vit DANS le point unique UsageNormalization, pas dans le provider fautif : il corrige le bug reel resets_at: 9 pour TOUTES les sources d'un coup, y compris celles qui n'existent pas encore (la sonde du plan 18-03 lit un epoch en TEXTE et en herite gratuitement).
- [Phase 18]: [18-01] InstantDepuisEpochMillisecondes n'est pas un confort mais une CONDITION de la garde : sans elle, les 3 horodatages en ms du rapport de diagnostic auraient force l'exemption de DiagnosticService.cs — et une garde qui exempte le plus gros fichier de la couche ne garde rien.
- [Phase 18]: [18-01] Aucun clamp dans la porte de validation, ni haut ni bas : Exhausted teste >= 1.0 donc un depassement reel doit rester visible, et une valeur negative est un symptome de source incoherente, pas un zero (null != 0).
- [Phase 18]: [18-01] Etape RED jouee contre un SQUELETTE compilable (NotImplementedException) et non contre une classe absente : tests/Chronos.Tests reference Chronos, donc un commit non compilable rendrait dotnet test non invocable. RED = 25 echecs / 1 succes, donc comportemental et non de compilation.
- [Phase 18]: [18-01] Garde de non-retour HDR-05 prouvee FALSIFIABLE par mutation reelle (p / 100.0 reintroduit -> echec nommant ChronosOAuthUsageProvider.cs:174) puis revoquee (git diff vide). Exemptions NOMINATIVES : point unique + ClaudeTokenReader (expiration de jeton) + SessionMonitor + TranscriptActivityProvider (dates qui ne sont pas des quotas).
- [Phase 18]: 18-02 : le statut serveur et le depassement vont sur WindowState (transmis PAR REFERENCE par Best()) et non sur UsageSnapshot, que le composite reconstruit -- contrainte mecanique, pas preference
- [Phase 18]: 18-02 : vocabulaire de statut OUVERT (NonReconnu) et null distinct de NonReconnu -- un statut inconnu n'est jamais range d'autorite dans « autorise »
- [Phase 18]: 18-02 : HDR-03/HDR-04 restent Pending -- ce plan livre leurs contrats, c'est au plan 18-06 de les cocher quand la remontee au cadran sera livree
- [Phase 18]: Sonde : une fenetre dont SEULE l'utilization est illisible reste Exact avec Utilization null — jeter un reset reellement obtenu serait une perte d'information
- [Phase 18]: Un 429 PORTEUR d'en-tetes appelle SignalerSucces() : il prouve que le jeton est valide, donc la pastille ne mentira pas pendant la saturation
- [Phase 18]: Les criteres grep du plan sont devenus un test permanent balayant le texte source de la sonde (horloge systeme, controle de succes par exception, lecture du corps, rafraichisseur)
- [Phase 18]: 18-04 : PublierDepassement en tete des branches 2xx et 429 (avant le test d'exploitabilite) — un serveur qui repond sans en-tete de depassement DIT quelque chose ; seules les pannes de transport et le frein preservent l'etat
- [Phase 18]: 18-04 : le statut serveur n'a PAS de canal lateral, le depassement si — un statut DECRIT une fenetre et meurt avec elle, un depassement decrit le COMPTE et doit survivre a un Best() defavorable
- [Phase 18]: [18-05] La sonde est en PRIMARY et c'est MECANIQUE : Best() ne retient le fallback que s'il est STRICTEMENT plus fiable et les deux sources produisent Exact. En fallback, le seul snapshot porteur du statut serveur et du depassement etait ecarte a chaque tick ou /api/oauth/usage repond — HDR-03/HDR-04 morts-nes.
- [Phase 18]: [18-05] Position prouvee par le COMPORTEMENT, pas par la reflexion (champs du composite prives) : deux sources Exact aux chiffres differents (0,01 contre 0,42), l'inversion primary/fallback mesuree (0,42 sortait) puis revoquee.
- [Phase 18]: [18-05] Statut serveur et depassement du snapshot affiches en etendant Describe(), PAS dans la section de la sonde : celle-ci est ecrite avant l'appel au composite, donc un second GetAsync aurait DOUBLE la depense de quota a chaque ouverture du diagnostic (grep GetAsync == 1 grave la contrainte).
- [Phase 18]: [18-05] Cadence et cout annonces a l'utilisateur DERIVES de CadenceNominale et non recopies ; l'inventaire des noms d'en-tetes de /api/oauth/usage tranchera si la sonde est gratuite (suppression possible du cout en phase 19+).
- [Phase 18]: HDR-02 reste « implémenté et testé sur faux transport, NON prouvé en production » : qu'un 429 RÉEL d'Anthropic porte la famille d'en-têtes unified exige un jeton valide et un compte saturé — le point de vérification humaine du plan 18-06 est consigné, non simulé.
- [Phase 18]: L'interrupteur de la sonde est un réglage DISTINCT de la source OAuth, et un test le documente : leurs profils de coût sont opposés (une micro-requête par passage contre rien), donc les fusionner priverait l'utilisateur du seul interrupteur qui gouverne une dépense.
- [Phase 18]: Le ViewModel franchit TROIS frontières de thread et non deux : la formule « seconde et dernière » du plan 17-05 est explicitement amendée plutôt que laissée en contradiction dans l'historique.
- [Phase 19]: [19-01] INVENTAIRE REEL MESURE : ZERO test casse par l'ajout de CapturedAt (652 -> 664, +12 nouveaux uniquement). Aucun des ~120 tests des quatre providers ne comparait un WindowState entier ; ils assertent champ par champ. La crainte du plan etait une hypothese, 2 min 10 de mesure l'ont revoquee.
- [Phase 19]: [19-01] L'horodatage appartient a la SOURCE, pas au lecteur : meme parametre pour les trois providers mais PAS la meme valeur — now pour les deux OAuth (la lecture EST la capture), capturedAt du FICHIER pour usage.json. Un fichier ecrit il y a deux mois porte deux mois d'age, verrouille par fixture.
- [Phase 19]: [19-01] Le commentaire qui decrit un piege ne REPRODUIT jamais l'expression fautive : sans quoi le critere de non-retour par grep serait mort-ne, et une garde qui ne peut pas echouer ne garde rien (precedent 18-01).
- [Phase 19]: [19-01] Le memoiseur n'invente JAMAIS un journal vide : une panne des la premiere lecture laisse l'exception remonter. Un journal vide se lirait « aucune activite » — une AFFIRMATION, pas une absence de reponse. La conversion en branche indisponible appartient a l'appelant (19-03).
- [Phase 19]: [19-01] SourceActiviteMemoisee livree PROUVEE (6 tests) mais NON cablee : l'enregistrement DI est le plan 19-03, en un seul commit avec son consommateur (precedent 17-03, « un service que le host ne demarre jamais ne fait rien »).
- [Phase 19]: [19-01] EXA-02 et EXA-05 laisses Pending : ce plan livre les FAITS (horodatage par fenetre, UnExactADejaEteObtenu) et non la doctrine qui les applique. Aucun appelant ne consulte encore le bit ; aucune limite d'age ne rejette encore rien. Coches par 19-03 et 19-04.
- [Phase 19]: [19-02] SourceReliability.Estimated REAFFECTE au plancher DEL-04 plutot qu'augmente d'un membre : un nouveau membre aurait traverse silencieusement WindowGaugeViewModel.cs:69/84 (IsEstimated=false, HasTokens=false) et fait afficher le plancher COMME UN EXACT. La 4e distinction passe par un champ nullable Provenance, additif : zero des 32 sites new WindowState retouche, required reste a 2.
- [Phase 19]: [19-02] La limite d'age est un LAISSEZ-PASSER, pas un couperet : DEL-03 RE-HABILITE au-dela. Un releve de 3 h sans reponse assistant depuis est EXACT — c'est une deduction (utilization est fonction de la consommation), pas une indulgence. Le plafond absolu n'est pas la limite d'age mais Covers(T), l'horizon de 8 jours deja dans le code : c'est par la que meurt le 10 % de deux mois.
- [Phase 19]: [19-02] DEL-04 n'a pas de reponse numerique honnete et n'en a pas besoin : Utilization reste RIGOUREUSEMENT inchangee, seule sa NATURE change (« au moins X »), incertitude UNILATERALE, tokens bruts en accompagnement non convertis. Mesure a l'appui : 643 649 933 tokens sur 5 h contre l'ancien plafond de 230 000 000 = 280 %.
- [Phase 19]: [19-02] 0L et null ne disent pas la meme chose : branche 2 rend TokensDepuisReleve == 0 (MESURE a zero), branche 1 rend null (NON MESURE). Les confondre ferait d'une absence de mesure une affirmation.
- [Phase 19]: [19-02] Falsifiabilite JOUEE et non affirmee : 4 mutations reelles de DoctrineFraicheur (branche 1 supprimee -> 5 echecs ; HasActivity inverse -> 2 ; Covers neutralise -> 1 ; Utilization conservee a la demotion -> 3) plus la mutation EXA-04 (echec nommant DoctrineFraicheur.cs:85), toutes revoquees, git diff vide.
- [Phase 19]: [19-02] EXA-02/EXA-04/DEL-03/DEL-04 laisses Pending : la doctrine existe, pure et prouvee, mais AUCUN appelant ne l'invoque a ce commit. Le cablage est 19-03. CompositeUsageProvider n'a recu aucune injection — modification strictement documentaire (ctor et champs intacts, new UsageSnapshot conserve).
- [Phase 19]: La doctrine de fraicheur est branchee en TETE de chaine (LastExactUsageProvider), pas dans Best() — Seule cette couche detient simultanement horloge, magasin et source d activite, et elle est au-dessus de tout composite : elle statue UNE fois sur le snapshot final au lieu de statuer a chacun des trois niveaux imbriques. CompositeUsageProvider reste intouche, ses 14 tests verts.
- [Phase 19]: La passe de transcripts est PARESSEUSE et memoisee : zero lecture en regime nominal, une seule pour deux fenetres degradees — Mesure reelle du 2026-09-12 : une passe coute 2,7 a 3,2 s et lit 536 Mo. Au tick de 60 s, une lecture inconditionnelle serait une E/S permanente. Trois tests gardent la propriete, deux mutations en prouvent la falsifiabilite.
- [Phase 19]: La garde de WeeklyRecalibration porte desormais sur le RESET SEUL, plus sur la fiabilite — Un resets_at connu est un FAIT. La garde precedente (exacte ET datee) aurait fait tomber les fenetres hebdo passees en plancher dans le chemin de synthese, remplacant le reset reel du serveur par une supposition « ancre + n semaines ».
- [Phase 19]: [19-04] « >= » et non « ~ » : l'incertitude d'un plancher est UNILATERALE. Un tilde dirait « autour de 80 » et autoriserait la lecture « peut-etre 75 » — or on SAIT qu'on est a 80 au minimum, c'est la borne SUPERIEURE qui est inconnue. Consequence imposee au dessin de la phase 20 : PAS d'arc de delta, PAS de barre d'erreur, il n'existe aucune borne superieure a representer.
- [Phase 19]: [19-04] Le prefixe derive de la PROVENANCE et non de la fiabilite : un exact encore valide (DEL-03) est un chiffre juste et ne porte aucune marque. Decider par SourceReliability aurait sali precisement le cas que 19-02 a passe sa demonstration a rehabiliter.
- [Phase 19]: [19-04] Surcharge ADDITIVE de PercentFormatter (bool pour la galerie d'apercu, ProvenanceReleve? pour la production) : aucune ambiguite de resolution car bool n'accepte pas null. 3 sites d'appel, ZERO retouche, et les 4 assertions litterales du tilde restent intactes.
- [Phase 19]: [19-04] MajPastilles, point de recomposition UNIQUE des trois pastilles : les deux entrees arrivent par deux canaux (snapshot, evenement d'auth) et a deux instants. Sans ce point, un changement d'etat d'auth posterieur au dernier snapshot laisserait l'invitation perimee. Mutation jouee : retrait de l'exclusivite -> 2 echecs.
- [Phase 19]: [19-04] L'invitation s'efface devant la pastille de deconnexion : les deux portent le MEME geste (ReconnecterCommand), les afficher ensemble sur 170 px serait une redondance et non une information ; la deconnexion est le diagnostic le plus precis des deux.
- [Phase 19]: [19-04] « == false » et non « != true » : null = non evalue (magasin en panne, ou UsageSnapshot.Empty). Une absence de reponse ne produit jamais une affirmation — c'est ce qui garde les 4 tests de pastille de la phase 17 verts sans retouche.
- [Phase 19]: [19-04] TokensText/HasTokens et DataUnavailable sont calcules mais bindes NULLE PART (zero occurrence dans les XAML) : la matiere brute de DEL-04 est correcte, testee et invisible. Dette leguee a la phase 20, qui doit trancher explicitement plutot que d'en heriter en silence.
- [Phase 19]: [19-05] Porte de phase franchie sur preuves : 699/699 en DEUX executions consecutives (les [WpfFact] de la phase justifient la double passe, precedent BAML 16-03), 5 gardes permanentes vertes, comptage reconcilie a l'unite (652+12+16+8+11=699), zero test supprime, zero test ignore.
- [Phase 19]: [19-05] Le constat humain de la bascule est SUBSTITUE et non simule : lancer l'overlay declencherait la purge des 25 groupes de hooks de ~/.claude/settings.json (phase 15, App.OnStartup) hors supervision. La ligne 19-05 T2 de la carte porte un statut DISTINCT, jamais un vert usurpe.
- [Phase 19]: [19-05] DEL-04 : la moitie invisible est DITE plutot que masquee. Le « + delta estime » de la lettre a ete amende pour cause (280 % mesure), et la matiere brute de substitution (TokensText/HasTokens) est calculee, testee et bindee NULLE PART — 0 occurrence dans les XAML. Dette n.1 de la phase 20.
- [Phase 19]: [19-05] EXA-06 n'est couverte qu'a moitie et c'est verifie : ProvenanceReleve ne compte que 3 membres, tous des ETATS ; le NOM de la source alimentant l'affichage n'est porte par aucun champ. Il faut un champ de plus en phase 20.
- [Phase 20]: [20-01] Substitution sous test, JAMAIS memoisation en production : cacher le balayage d'environnement aurait donne le meme gain ET un diagnostic capable de mentir. InventaireMachine ne garde rien entre deux appels. Suite complete 2 min 12 s -> 2 s (700 tests).
- [Phase 20]: [20-01] 3e application du parametre optionnel TERMINAL (authStatus 17, etatServeur 18, machine 20) : les 15 sites de construction de DiagnosticService compilent sans retouche, zero inscription DI, CompositionRootTests immobile. C'est desormais LA maniere d'ouvrir une couture dans ce depot.
- [Phase 20]: [20-01] CadranBindingTests lisait les reglages REELS de la machine (MainViewModel appelle settings.Load() dans son ctor) : tout test de style des plans 03/04 y aurait ete vert PAR ACCIDENT. Piege du montage : DEUX SettingsService dans BuildWindow, le second alimentant OverlayController.
- [Phase 20]: [20-01] Le conteneur a cellules uniformes ignore SILENCIEUSEMENT Grid.ColumnSpan : c'est le CONTENEUR qui change, pas l'enfant annote. Preuve par LARGEUR MESUREE (ActualWidth apres Measure/Arrange), falsifiabilite jouee puis revoquee. 3 colonnes exclu par la mesure (98,7 px de cellule pour 107 px de libelle).
- [Phase 20]: [20-01] EXA-03/EXA-06 laisses Pending malgre le frontmatter du plan : cette vague ne leve que des dettes d'outillage et n'a touche ni la distinction visuelle frais/date/indisponible, ni le nom de la source. Precedents 17-01, 18-02, 19-01/19-02.
- [Phase 20]: Le nom de la source est un axe DISTINCT de la provenance : les fusionner produirait un enum de 15 membres et rendrait le diagnostic incapable de dire lequel des deux points OAuth repond
- [Phase 20]: EstimatedTokens supprime plutot que conserve sous garde comportementale : la garde a CHANGE DE NIVEAU (structurelle), elle n'a pas disparu
- [Phase 20]: Aucun membre fourre-tout dans SourceUsage : l'absence de source se dit par null, jamais par une valeur d'enum qui affirmerait quelque chose
- [Phase 20]: [20-03] La doctrine a l'AUTORITE : le seuil concurrent de 2 min du ViewModel n'a pas ete aligne sur les 6 min de LimiteAge, il a ete SUPPRIME. Aligner deux autorites les laisse diverger au refactor suivant ; il ne doit en rester qu'une.
- [Phase 20]: [20-03] La garde de non-retour balaie DEUX fichiers NOMMES (MainViewModel, WindowGaugeViewModel) et non le dossier ViewModels : SessionsViewModel compare legitimement des durees (l. 190-191). Une garde rouge sur du code correct est une garde qu'on apprend a ignorer.
- [Phase 20]: [20-03] EstDate et EstPlancher se COMPOSENT (plancher inclus dans date) : un EncoreValide est date SANS etre un plancher — la doctrine est allee VERIFIER qu'aucune reponse assistant n'est survenue depuis la capture. Le griser serait mentir sur une preuve positive.
- [Phase 20]: [20-03] AfficherReleveDate est GLOBALE et conservatrice (le plus vieux des deux, jamais le plus jeune) : l'age est une propriete du PIPELINE, pas de la fenetre. Le detail par fenetre passe par l'infobulle.
- [Phase 20]: [20-03] La matiere brute de DEL-04 (TokensText/HasTokens) devient visible A LA DEMANDE en infobulle, pas en permanence : une ligne de tokens au centre annulerait la decision de design 'centre epure' de la v1.3 pour une exigence qui ne la demande pas. Dette n.1 de la phase 19 levee.
- [Phase 20]: [20-03] UsageSnapshot.SourceCapturedAt CONSERVE et son commentaire amende : ce champ dit quelque chose de vrai (anciennete de SOURCE). Ce qui est mort, c'est la seconde notion de perime que le ViewModel en derivait.
- [Phase 20]: [20-03] EXA-03 laisse Pending malgre le frontmatter : ce plan livre le CONTRAT de presentation complet, aucun pixel n'a bouge (AfficherReleveDate et InfobulleReleve bindes dans aucun XAML). Precedents 17-01, 18-02, 19-01, 19-02, 20-01, 20-02. Coche par le plan 20-04.
- [Phase 20]: 20-04 : l'anneau 24 h fin (4 px) n'est DELIBEREMENT pas marque par le pointillé de plancher - le 5 h a déjà son arc épais marqué juste en dessous ; décision verrouillée par un Assert.Empty, pas par un commentaire
- [Phase 20]: 20-04 : les deux pastilles qui se recouvraient ne sont PAS rendues mutuellement exclusives mais mises en RANGEE - elles disent des choses différentes et peuvent coexister ; c'est le recouvrement qui était le défaut
- [Phase 20]: 20-04 : les commentaires de sécurité qui NOMMENT LoginClaudeCommand pour l'interdire sont conservés - un critère grep littéral ne justifie pas de supprimer l'avertissement qui protège le coffre de jetons
- [Phase 20]: 20-04 : TDD RED/GREEN RÉEL pour la première fois du milestone - le dessin ne crée aucun symbole de production, donc l'étape rouge compile et la falsifiabilité est portée par l'historique git
- [Phase 20]: Le tilde du diagnostic etait un OUBLI de la phase 19 : « >= » partout, un seul vocabulaire pour dire borne inferieure
- [Phase 20]: Les deux segments d'EXA-06 (source, anciennete) sont INCONDITIONNELS : une fenetre sans source dit « non renseignee » plutot que de se taire
- [Phase 20]: HDR-02 reste NON prouvee en production : le 429 reel exige une saturation du compte, la nuance est ecrite plutot que cochee en silence
- [Phase 21]: [21-01] La limite de douze porte sur les sessions RETENUES (break apres Classify), plus sur les fichiers examines (Take avant filtre) : un sous-agent, un transcript sans message exploitable ou un fichier illisible ne consomme plus aucun emplacement. MaxSessions reste a 12 — le defaut n'etait pas la valeur mais son point d'application.
- [Phase 21]: [21-01] Le pre-filtre de chemin EstSousAgent (dossier subagents/ ou nom agent-*) est une ECONOMIE d'I/O (94 % des 870 transcripts), JAMAIS l'autorite : le champ isSidechain lu ligne a ligne dans Classify reste le juge, et un test le verrouille sur un fichier mal range au nom banal.
- [Phase 21]: [21-01] Le catch de Read ne protegeait rien : l'enumeration LINQ etait paresseuse, donc l'erreur disque survenait dans le foreach, HORS du try. ToList() a l'interieur du try. Non couvert par un test — injecter une panne d'enumeration exigerait une abstraction de systeme de fichiers absente du depot.
- [Phase 21]: [21-01] ISessionSource porte par TranscriptSessionSource en vague 1, avant toute demolition : aucun corps de methode touche (la signature de Read satisfaisait deja le contrat), et le plan 21-02 peut faire prendre a SessionMonitor sa source de base PAR LE CONTRAT sans commit non compilable.
- [Phase 21]: [21-01] L'invariant de securite 'oauth.dat mtime 1783863147' du 21-VALIDATION.md est FAUX (mesure : 1789211525 ; 1783867137 est celui d'archived.json — transposition). L'overlay en cours fait tourner le refresh token toutes les 60 s : un mtime fige ne peut pas etre un invariant. Invariant de remplacement pour le plan 21-04 : 518 OCTETS.
- [Phase 21]: MutableSource survit en passant du 4e au 2e argument de SessionMonitor : le contrat ISessionSource porte par TranscriptSessionSource evite la reecriture des tests d'hysteresis
- [Phase 21]: Le champ _machine de DiagnosticService reste : CoffresOAuth vaut 94 % du cout du rapport et sa couture sous test est la raison d'etre de IInventaireMachine
- [Phase 21]: NET-02 documente par une epitaphe (raison mecanique de sa mort) plutot que supprime en silence
- [Phase 21]: Le montage BAML d'une fenetre jamais affichee se fait sur sa GRILLE RACINE (DataContext pose dessus + purge du Dispatcher), jamais sur la fenetre : sinon DesiredSize rend 0x0 et aucun binding ne s'evalue.
- [Phase 21]: Une garde de non-retour se prouve par MUTATION avant d'etre committee : 3 mutations injectees puis revoquees (type Uia*, binding KindLabel, separateur orphelin).
- [Phase 21]: [21-04] Ecarter a la lecture n'est PAS retirer : ArchiveStore.Load() ecartait deja les deux fantomes desktop:foreground:* par TTL, s'en contenter aurait laisse le contournement MANUEL de l'utilisateur grave dans ses donnees. PurgerPrefixe retire DU FICHIER ; l'agent n'a pas touche au vrai archived.json (84 o, inchange) — c'est le code livre qui le purge au prochain lancement.
- [Phase 21]: [21-04] Purger un prefixe et expirer une entree sont deux gestes DISTINCTS : PurgerPrefixe n'applique AUCUN filtre de TTL, et un test verrouille qu'une entree de 7 h non prefixee SURVIT a la purge (Load continue de ne pas la rendre). Les confondre ferait disparaitre, a l'occasion d'un nettoyage, des archives que l'utilisateur n'a jamais demande de retirer. Le TTL de 6 h reste : c'est TRT-04, phase 26.
- [Phase 21]: [21-04] Le nombre rendu est une OBSERVATION et non une intention (return ecrit ? retirees : 0) : une ecriture en echec rend 0, parce que rien n'a ete retire. Et rien a retirer => le fichier n'est PAS reecrit, date de derniere ecriture comprise.
- [Phase 21]: [21-04] Porte de phase franchie : 719/719 en DEUX executions consecutives, 5 gardes nommees vertes, recul 752 -> 719 reconcilie a l'unite apres CHAQUE tache. Precision mesuree : le bilan compte des CAS (49) et non des methodes (41) — DesktopUiaSessionSourceTests portait 28 methodes pour 36 cas (3 [Theory], 11 [InlineData]).
- [Phase 22]: SessionMonitor.Read devient une pure projection d'Inspecter — Une seule implementation des filtres (archivage, traite) subsiste : aucun consommateur ne peut decrire un systeme different de celui qui tourne. C'est la condition structurelle des phases 23 a 26.
- [Phase 22]: Un fichier de hook ecarte pour anciennete est compte a part, il n'est pas masque — Une session dont le signal a expire n'est pas cachee, elle est inconnue. Confondre les deux effacait le fait qui explique l'ecart entre 54 fichiers sur disque et 1 ligne a l'ecran.
- [Phase 22]: OBS-01 reste Pending apres 22-01 — L'exigence est partagee avec le plan 22-02, qui porte le cablage du diagnostic. La cocher ici affirmerait une capacite qui n'existe pas encore.
- [Phase 22]: OBS-01 livré en PARTAGE D'INSTANCE : le diagnostic interroge le SessionMonitor du conteneur DI, sans aucun repli — sans moniteur il dit « MONITEUR NON INJECTÉ » au lieu d'en fabriquer un
- [Phase 22]: Les gardes de non-retour sont falsifiées avant commit : trois mutations appliquées, rouge observé, révocation vérifiée par checksum
- [Phase 22]: Le rapport de diagnostic choisit ses exemples de fichiers d'etat par PERTINENCE (attente d'abord, puis fraicheur, bareme partage avec le widget) et annonce combien il n'en montre pas — fin du tirage alphabetique des UUID (OBS-02)

### Contexte technique (déjà établi — ne pas re-rechercher)

- **Spike UIA PROUVÉ (2026-07-10)** sur la vraie machine : l'app Electron/Chromium expose son arbre
  d'accessibilité complet. Matcher par **ControlType + Name** (PAS par `AutomationId` volatils) ; Names
  **localisés fr**. Signaux vérifiés :

  - Text « Claude répond. » / Button « Arrêter » = **Working**.
  - Button « Ignorer les permissions » = **attend permission**.
  - Text « Mode chat » + placeholder « Tapez / pour les commandes » = **repos** (attend ton message).
  - Onglets Home/Code + panneaux Terminal/Diff/Aperçu/Actions de session/Contrôle à distance.
  - Sidebar : Button « En cours d'exécution <nom> » par session active.
  - Cowork-VM = état distant **non observable** → indéterminé.
- **MANQUE** : un snapshot en état **REPOS** (le spike a été pris pendant une génération). À capturer en
  tout début de Phase 13 pour figer la représentation exacte du « m'attend » premier plan.

- **Code existant** :
  - `src/Chronos/Services/SessionSnapshot.cs` — record neutre + enum `SessionActivity`
    (Working / WaitingAttention / WaitingTurn / Unknown).

  - `src/Chronos/Services/SessionMonitor.cs` — `Read(now)` fusionne transcripts + hooks, applique la
    staleness, filtre `archived` via `ArchiveStore`. **Le filtre « traitées » (Phase 14) se branche ici,
    au même endroit que le filtre archived.**

  - `src/Chronos/Services/TranscriptSessionSource.cs`, `ArchiveStore`.
  - `src/Chronos/ViewModels/SessionsViewModel.cs` — `Refresh`, timer 2 s.
  - `tests/Chronos.Tests/SessionsTests.cs`.
- **Nouveau (Phase 13)** : `DesktopUiaSessionSource` (nouvelle `ISessionSource`), lecture hors thread UI
  puis marshalling, élément racine mis en cache, cadence ~1-2 s.

- **Contraintes** : `System.Windows.Automation` (interop COM managé, aucune dépendance native de rendu,
  pas d'admin) ; `%USERPROFILE%`/`%APPDATA%` uniquement ; honnêteté (indéterminé, jamais une inférence
  présentée comme exacte) ; lecture tolérante (aucune source ≠ crash) ; UI/commentaires en français.

### Pending Todos

- **Prochaine action v1.6 : `/gsd:plan-phase 22`** (OBS — l'instrument de mesure : le diagnostic partage
  l'instance du moniteur du widget au lieu d'en reconstruire un nu).

- ~~Prochaine action v1.6 : `/gsd:plan-phase 21`~~ (fait, phase 21 close : SRC-01, SRC-02, SRC-03).
- **A faire par l'utilisateur, hors GSD — NOUVEAU (phase 21)** : au prochain lancement VOLONTAIRE de
  l'overlay, constater que `%APPDATA%\Chronos\archived.json` passe de **84 octets** a `{}` (2 octets).
  C'est la preuve in vivo de SRC-02, non substituable. Plus deux points visuels (galerie `--sessions` :
  8 styles x 9 themes, style Pastilles sans separateur orphelin ; et apres quelques heures d'usage, plus
  aucune ligne prefixee `desktop:`). Protocole complet sous « A VERIFIER PAR L'UTILISATEUR » dans
  `21-04-SUMMARY.md` et `21-VALIDATION.md`. Ne bloque pas la phase 22.

- ~~Prochaine action v1.5 : `/gsd:plan-phase 20`~~ (fait, milestone v1.5 clos).
- **A faire par l'utilisateur, hors GSD — NOUVEAU** : constater la bascule « 10 % » -> « indisponible +
  invitation » en lancant la version du DEPOT. Protocole complet sous « A VERIFIER PAR L'UTILISATEUR »
  dans `19-05-SUMMARY.md`. **Attention** : lancer l'overlay purge les 25 groupes de hooks de
  `~/.claude/settings.json` (reconciliation de la phase 15) — c'est voulu, mais a faire en connaissance
  de cause. Ne bloque pas la phase 20.

- **À faire par l'utilisateur, hors GSD** : les deux vérifications manuelles de la phase 17, listées sous
  « À VÉRIFIER PAR L'UTILISATEUR » dans `17-05-SUMMARY.md` — (1) lisibilité de la pastille de
  déconnexion dans les 3 thèmes × 5 styles × 2 modes, (2) parcours de reconnexion en un clic de bout en
  bout (login navigateur réel). Ne bloquent aucune phase suivante, **mais** sans reconnexion réelle, ni
  `/api/oauth/usage` ni la sonde d'en-têtes de la phase 18 ne répondront sur cette machine.

- ~~Prochaine action v1.5 : `/gsd:plan-phase 15`~~ (fait, phases 15/16/17 closes).
- ~~Phase 13 (tout début) : capturer le snapshot UIA en état **repos**~~ (fait, v1.4 clos).

### Blockers/Concerns

- **NEUF constats humains en attente, seul reste du milestone v1.5.** Protocole consolide (phases 17 a 20)
  dans `20-05-SUMMARY.md`. Deux d'entre eux ne peuvent PAS etre refermes par un agent :
  **HDR-02** (un 429 REEL porte-t-il bien les en-tetes `anthropic-ratelimit-unified-*` ?) et le **parcours
  de reconnexion** de bout en bout. Ne pas les cocher sur la foi d'un test a reponse fabriquee.

- **L'exe deploye (`~/Downloads/Chronos-v2.8.1.exe`) est anterieur a TOUT le milestone v1.5** : tant qu'il
  n'est pas republie, rien des phases 15 a 20 n'est visible. La version du `.csproj` est encore 2.8.1 —
  une release v1.5 doit la monter.

- **Le premier lancement purgera les 25 groupes de hooks** de `~/.claude/settings.json` (attendu, PUR-01 /
  PUR-03, avec sauvegarde horodatee). L'agent ne l'a pas declenche : le fichier est intact
  (6872 o, mtime 1785403369).

- **Economie possible, a trancher apres constat** : si `/api/oauth/usage` sert deja la famille
  `anthropic-ratelimit-unified-*`, les ~288 micro-requetes/jour de la sonde deviennent redondantes.

- Le mapping d'états UIA dépend de Names **localisés** ; prévoir la table fr/en (ROB-06) dès la Phase 13
  pour ne pas coder en dur des libellés qui changent à une MAJ de l'app.

- Débounce du focus (NET-02, Phase 14) : caler ~2-3 s pour distinguer un vrai acquittement d'un survol.

## Session Continuity

Last session: 2026-09-12T16:19:25.407Z
Stopped at: Completed 22-03-PLAN.md
Resume file: None
Next: /gsd:verify-phase 21, puis /gsd:plan-phase 22 — OBS : l'instrument de mesure (OBS-01, OBS-02)
