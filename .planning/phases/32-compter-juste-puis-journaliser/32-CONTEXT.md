# Phase 32: Compter juste, puis journaliser - Context

**Gathered:** 2026-09-27
**Status:** Ready for planning
**Mode:** Auto-généré (workflow.skip_discuss=true) — enrichi des décisions déjà prises par l'utilisateur (checkpoint ZEUS du
2026-09-27) et du conseil LLM du 2026-09-26. Ces décisions sont VERROUILLÉES ; le reste est à la discrétion de Claude.

<domain>
## Phase Boundary

Avant toute ligne d'historique, Chronos compte juste (dédup `message.id`), ne se tait plus (cause du gel de `last-exact.json`
comprise et corrigée ; âge de chaque écriture exposé), ne tourne plus en double (mutex nommé), puis écrit un journal des
relevés exacts (1 / 5 min, dédup `CapturedAt`, événements de couverture, append idempotent multi-instances, lecture tolérante,
lecture par plage en classes pures). Publié SEUL en 3.2.2, sans interface. Se termine par le constat en production repris de
v1.7 (VAL-04), point de contrôle humain (`autonomous: false`). Exigences : CPT-01..03, JRN-01..06, VAL-04. Voir ROADMAP.md.

</domain>

<decisions>
## Implementation Decisions

### Décisions de l'utilisateur (verrouillées)
- La phase se publie seule en **3.2.2** avant toute UI : chaque jour sans journal de pourcentages est perdu à jamais.
- Toutes les variantes de la fenêtre (A/B/C) seront codées plus tard (phase 34) : cette phase ne fait AUCUNE UI, mais la
  lecture par plage (JRN-05) doit servir les trois vues (Semaine de forfait, Jour, 4 semaines) sans retouche.
- Le constat reporté de v1.7 (une seule instance, tableau des gestes, 12 vérifications) est le dernier plan de la phase, joué
  avec l'utilisateur sur la 3.2.2 ; l'agent ne lance, n'arrête ni ne clique jamais l'overlay.

### Approche technique arrêtée par le conseil (`.zeus/reports/llm-council-2026-09-26.md`)
- Deux journaux de nature différente, jamais fusionnés ; cette phase ne livre que celui des RELEVÉS EXACTS.
- Format : JSONL mensuel tolérant (`%APPDATA%\Chronos\historique\releves-AAAA-MM.jsonl`), dialecte maison de `LastExactStore`
  (SchemaVersion, ligne invalide ignorée, `v` inconnu sauté) ; **pas de SQLite** (dépendance native), pas de CSV.
- Ligne de relevé `{v, t, u5, r5, u7, r7, statut5, statut7, overage, source}` ; lignes d'événement
  `{v, t, ev: demarrage|arret|jeton_invalide|sonde_refusee|reprise}`.
- Dédup : `CapturedAt` strictement croissant (l'orchestrateur ressert le même relevé 5 fois sur 6 : sonde 300 s, timer 60 s) ;
  clé d'idempotence `CapturedAt` + source pour survivre à plusieurs écrivains ; append atomique (une ligne < 4 Ko).
- Point d'accroche : décorateur/abonné posé au même niveau que `LastExactUsageProvider` (ou sur `RefreshOrchestrator.SnapshotChanged`),
  n'écrivant que des fenêtres `Reliability == Exact` produites par l'inner, jamais un plancher ni une valeur rejouée du magasin.
- L'âge de la dernière écriture de chaque magasin persistant est un chiffre de première classe (diagnostic + réglages) ;
  alerte au-delà de 15 min alors que Chronos tourne (« journal muet depuis N min », pastille `Alerte`).
- Mono-instance : mutex nommé ; le second exe se retire en le disant (message) sans jamais tuer l'autre ; `--hook` et le mode
  CLI restent multi-instances.
- Lecture par plage en classes NEUTRES (`Services/` ou `Rendering/` sans WPF, `ServicesLayerPurityTests` reste vert) : série,
  trous (> 2 cadences = 10 min) avec leur cause, resets 5 h et hebdo observés, Δ entre relevés consécutifs de même `resets_at`
  (jamais à travers un reset), saut non localisé de part et d'autre d'un trou. Bornes hebdo : `resets_at` 7 j du journal, repli
  `WeeklyAnchor` ; semaine de forfait = samedi 00:00 heure locale (2026-09-18T22:00Z constaté).
- Hypothèses à écrire dans `docs/data-sources.md` § « Journal d'historique », à vérifier AVEC le journal : granularité des
  en-têtes `utilization` (0,01 constaté) ; Δ d'utilization = consommation (pas de recalcul rétroactif hors reset) ; reset hebdo à
  l'heure locale au changement d'heure du 25/10/2026 (`WeeklyWindow` avance par 7 × 24 h fixes et dériverait d'une heure).

### Claude's Discretion
Nommage des classes, découpage des fichiers, forme exacte du mutex, stratégie de test des écrivains concurrents, format des
lignes de diagnostic — dans le respect des conventions du dépôt (MVVM, DI, français, commentaires qui disent POURQUOI).

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Services/LastExactStore.cs` : écriture atomique temp + `File.Move`, lecture tolérante, `SchemaVersion` — le modèle du journal.
- `Services/LastExactUsageProvider.cs` (App.xaml.cs:400 : il enveloppe `CompositeUsageProvider(sonde → OAuth → gated → objet
  d'usage)`) : point d'accroche ; CORRIGÉ par 32-RESEARCH : il n'y a PAS de gel — le fichier réel est réécrit chaque minute (sonde WMI hors
  arbre, 27/09 01:56) ; la copie datée du 13/09 12:44 est la vue virtualisée du paquet MSIX que toute session lit. CPT-02 =
  observabilité (âge de dernière écriture, `ecriture_ratee`, « Vue AppData : réelle | virtualisée ») + test du `try/catch` muet.
- `Services/RateLimitHeaderUsageProvider.cs` : `CadenceNominale` 300 s, `CapturedAt = now` (l. 431), statuts serveur
  (`IEtatServeur`) — les sources de `statut5/statut7/overage`.
- `Services/RefreshOrchestrator.cs` (`SnapshotChanged`, `PeriodicTimer` 60 s), `Services/RefreshOptions.cs`.
- `Services/TranscriptActivityProvider.cs` : `SumUsageTokens` (l. 136-143) somme les 4 champs LIGNE PAR LIGNE — c'est le bug
  CPT-01 ; `DoctrineFraicheur.TokensDepuisReleve` (l. ~113) en hérite. `TranscriptSessionSource` fait un `Seek` de queue, pas un
  curseur : aucune lecture par offsets n'existe.
- `Services/WeeklyWindow.cs` (orphelin assumé, logique pure), `Rendering/DayTimeline.cs` (suppose un pas de 5 h — à ne pas copier).
- `Services/DiagnosticService.cs` : le rapport « Diagnostic… » (sections par source, `[Ce qui est affiché maintenant]`).
- `Services/ChronosPaths.cs` : chemins injectés sous `%APPDATA%\Chronos` (ne jamais construire un chemin en dur).
- Tests : `tests/Chronos.Tests` (1200 verts), `ServicesLayerPurityTests`, gardes structurelles des phases 20/29/30.1.

### Established Patterns
- MVVM strict, DI par `HostApplicationBuilder` (App.xaml.cs), `IClock` injecté (aucune horloge système directe), types neutres
  sous `Services/`, commentaires en français qui disent le POURQUOI, une garde de test par contrat documentaire.
- Release : bump des quatre propriétés du csproj, `dotnet publish` mono-fichier, exe `Chronos-vX.Y.Z.exe` à la racine, smoke
  `--hook`, commit de release ; procédure de 31-02 (`.planning/milestones/v1.7-ROADMAP.md`, phase 31).
- Fixtures de transcripts RÉELLES à privilégier (une ligne `assistant` par bloc de contenu, même `usage`) : 3 248 lignes pour
  1 183 `message.id` sur le plus gros fichier, `usage` identique entre doublons, 0 divergent.

### Integration Points
- DI : enregistrer le journal et le mutex dans App.xaml.cs près de `LastExactStore`/`LastExactUsageProvider` ; `DiagnosticService`
  reçoit les âges d'écriture ; `SettingsWindow` (section DONNÉES) reçoit la ligne d'état minimale « dernière écriture / alerte »
  (la carte complète F1 arrive en phase 35).
- AppData est VIRTUALISÉ (MSIX) pour tout ce qui tourne sous l'app bureau : les relevés de fichiers du constat se font hors de
  l'arbre de l'app (sonde WMI), jamais depuis une session Claude Code.

</code_context>

<specifics>
## Specific Ideas

- Plan de design validé `.zeus/DESIGN_PLAN.md` §3 (contrat de données) et DoD `.zeus/DOD.md` : mêmes champs, mêmes mots
  (relevé, trou, « Chronos arrêté », « jeton invalide », « sonde refusée », « journal ouvert le … », « dernière écriture »).
- Le constat VAL-04 réutilise mot pour mot le protocole de `.planning/phases/31-crit-publi-constat/31-CONSTAT.md` ; verdicts
  dans `32-CONSTAT.md` ; plus la vérification « le journal s'écrit » (âge < 6 min après 10 min d'overlay).
- Trois exécutables (3.1.0, 3.2.0, 3.2.1) tournent encore : le mutex n'empêchera pas les anciens exe ; le constat commence par
  les quitter (geste utilisateur).

</specifics>

<deferred>
## Deferred Ideas

- Agrégats de tokens, curseurs, reconstruction de fond → phase 33 (TOK).
- Fenêtre Historique, styles A/B/C, vues Jour et 4 semaines → phases 34-35 (HIS, ACC).
- Compaction du journal, heatmap, export CSV, projection conditionnelle → v1.9 (v2 requirements).

</deferred>
