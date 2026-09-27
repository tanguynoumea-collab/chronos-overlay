# Phase 33: Agrégats de tokens - Context

**Gathered:** 2026-09-27
**Status:** Ready for planning
**Mode:** Auto-généré (workflow.skip_discuss=true), enrichi des décisions verrouillées (conseil LLM du 2026-09-26, plan de
design validé le 2026-09-27) et des livrables réels de la phase 32.

<domain>
## Phase Boundary

Les tokens des transcripts Claude Code (principal ET `subagents/`) sont agrégés par tranche de 15 min UTC × modèle ×
principal/sous-agent, quatre compteurs séparés + nombre de messages, dans `%APPDATA%\Chronos\historique\tokens-AAAA-MM.jsonl`
(TOK-01) ; la reconstruction initiale parcourt les 2,2 Go existants en arrière-plan sans jamais bloquer l'UI, la semaine
courante d'abord, progression exposée (TOK-02) ; la mise à jour est incrémentale par curseurs `{chemin → offset, taille, mtime}`,
un fichier raccourci/renommé est réingéré et ses tranches RÉÉCRITES, la reprise après arrêt est idempotente (TOK-03) ; les
tranches UTC se rendent en heure locale avec le DST correct, « hors couverture » et « transcripts absents » ne sont jamais
« zéro » (TOK-04) ; aucun pourcentage n'est jamais dérivé de tokens, garde structurelle à l'appui (TOK-05). Aucune UI dans
cette phase : la fenêtre Historique (phase 34) consommera la lecture par plage des agrégats. Exigences : TOK-01..05.

</domain>

<decisions>
## Implementation Decisions

### Verrouillées (utilisateur + conseil + plan de design §3)
- **Deux journaux de nature différente, jamais fusionnés** : les agrégats de tokens ne portent JAMAIS un pourcentage de forfait ;
  périmètre partiel écrit dans le schéma et les docs (« Claude Code seulement — hors Cowork et claude.ai ; bruts, non pondérés »).
- **Ligne d'agrégat** `{v, slot, model, sub, in, out, cache_w, cache_r, n}` — `slot` = début de tranche 15 min en UTC (ISO, `Z`),
  `model` = identifiant tel que lu (`claude-opus-5-5`…), `sub` = booléen (fichier sous `subagents/`), quatre compteurs séparés,
  jamais la somme, jamais le message individuel, jamais le contenu textuel.
- **Dédup** : par `message.id` (repli `requestId`), **max par champ** — le helper `DedupUsage` de la phase 32 (CPT-01) est LA seule
  voie de somme des `usage` (une garde interdit tout autre lecteur). Point ouvert à trancher en recherche/plan : 491 ids vivent
  dans 2 à 3 fichiers (fork/resume) → portée du dictionnaire sous curseurs par fichier (index persistant `id → (slot, max)` borné,
  ou dictionnaire par passe + réécriture des tranches touchées).
- **Format** : JSONL mensuel tolérant (dialecte maison), écriture ATOMIQUE par réécriture du mois (temp + `File.Move`) depuis l'état
  en mémoire — c'est ce qui rend « tranches réécrites, pas ajoutées » trivial ; pas de SQLite ; `curseurs.json` atomique aussi.
- **Reconstruction** : `BackgroundService`/thread `IsBackground`, priorité `BelowNormal`, fichiers par mtime DÉCROISSANT (semaine
  courante disponible en secondes), lecture en flux (`FileShare.ReadWrite | Delete`, `SequentialScan`, tampon 64 Ko), pré-filtre
  texte (`"type":"assistant"`) avant `JsonDocument`, `Task.Yield` entre fichiers, annulable, curseurs persistés par lot ; ensuite
  incrémental : seuls les fichiers dont (taille, mtime) ont changé, relus depuis l'offset de la dernière ligne complète.
- **Fuseaux** : buckets UTC, rendu local via `TimeZoneInfo` injecté (tests « Europe/Paris » explicites) ; 25 h le 25/10/2026, 23 h le
  28/03/2027 ; `BornesPlage` (phase 32) donne les bornes de plage.
- **Couverture** : tranches antérieures au plus vieux transcript = « hors couverture » ; mois purgé par Claude Code = « transcripts
  absents » ; jamais « zéro token ». La lecture par plage des agrégats expose cette couverture comme JRN-05 le fait pour les relevés.
- **Observabilité** : l'agrégateur implémente `IEtatMagasin` (troisième magasin, `NomsMagasins`), la progression N / M et l'état de
  reconstruction sont exposés au ViewModel (bandeau F2 en phase 34) et au diagnostic ; jamais d'appel réseau ; jamais d'écriture
  sous `~/.claude` (lecture seule stricte des transcripts).

### Claude's Discretion
Nommage, découpage des fichiers sous `Services/Historique/` et `Models/Historique/`, structure de l'index de dédup, stratégie de
throttling (budget CPU par tranche), format exact de la progression, forme des fixtures (extraits RÉELS anonymisés multi-blocs,
sous-agents, fichier tronqué, fichier réduit, timestamps futurs) — dans les conventions du dépôt.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets (livrés en phase 32 — lire les SUMMARY 32-01, 32-02, 32-04, 32-06)
- `Services/DedupUsage.cs` : max par champ par `message.id`, `LignesVues / MessagesDistincts` ; garde « aucun lecteur de usage hors
  du helper » (à respecter : le nouvel agrégateur passe par lui ou l'étend).
- `Services/TranscriptActivityProvider.cs` : passe complète bornée à 8 jours, parsing des lignes `assistant` (à imiter, pas à
  réutiliser tel quel : le lecteur d'agrégats est distinct, sans borne de 8 jours) ; `TranscriptSessionSource` : `Seek` de queue.
- `Services/Historique/` : `JournalReleves` (écriture sûre, mois UTC, rétention), `LecteurJournal.Lire` par plage, `BornesPlage`,
  `AnalyseReleves`, `LigneJournal` (lecture tolérante) ; `Models/Historique/LectureJournal.cs`.
- `Services/IEtatMagasin.cs` + `NomsMagasins` (le nom « agrégats de tokens » existe déjà) ; `DiagnosticService` section
  `[Magasins persistants]` prend une liste de magasins ; `ChronosPaths.HistoriqueDir`.
- `IClock`, `ServicesLayerPurityTests` (couvre désormais les sous-dossiers), gardes textuelles de périmètre.
- Transcripts réels : 1 565 fichiers, 2,2 Go, 1 480 de sous-agents, plus gros 53 Mo, juin → aujourd'hui avec juillet purgé ;
  `grep` de 2 Go : 3,8 s cache chaud ; C# en flux avec pré-filtre : 10–20 s CPU estimés.

### Established Patterns
- MVVM strict, DI par `HostApplicationBuilder`, hosted services enregistrés avant `RefreshOrchestrator`, types neutres sous
  `Services/` et `Models/`, `IClock` injecté, commentaires en français qui disent POURQUOI, une garde de test par contrat.
- TDD par plan (RED nommé → GREEN), mutations jouées et révoquées par copie (autocrlf), suite complète deux fois en fin de plan
  (1313 verts à l'entrée de la phase), stage explicite, `--no-verify` en exécution parallèle, jamais ROADMAP/STATE édités par un
  exécuteur.
- Fixtures de transcripts RÉELLES anonymisées (multi-blocs, `output_tokens` partiel croissant) sous `tests/Chronos.Tests/TestData/`.

### Integration Points
- DI dans App.xaml.cs (près du journal des relevés) ; `DiagnosticService` (troisième magasin + section « Agrégats de tokens » :
  progression, curseurs, dernier fichier lu) ; `docs/data-sources.md` §7 à compléter (schéma des agrégats, curseurs, dédup) sous la
  garde documentaire de 32-07 ; le lecteur par plage des agrégats est l'API que la phase 34 consommera (Semaine / Jour / 4 semaines).

</code_context>

<specifics>
## Specific Ideas

- Plan de design `.zeus/DESIGN_PLAN.md` §3 (contrat), §2.2–2.4 (ce que les pistes Tokens attendent : barres par heure en Semaine,
  par quart d'heure et par modèle en Jour, part sous-agents), §4 vocabulaire (« hors couverture », « transcripts absents »).
- Rapport du conseil `.zeus/reports/llm-council-2026-09-26.md` §4 (reconstruction) et erratum (règle max par champ).
- Aucune phase de validation humaine ici ; la 34 consomme, la 35 constate.

</specifics>

<deferred>
## Deferred Ideas

- Dimension projet (dossier) dans les agrégats → v1.9 (TOK-06) ; compaction ; export CSV ; heatmap.
- Toute UI (bandeau F2, pistes) → phase 34.

</deferred>
