# Phase 29 : Ce que l'app bureau sait de chaque session — Contexte

**Gathered:** 2026-09-25
**Status:** Ready for planning (après la phase 28 : vocabulaire et ordre définitifs)
**Source:** décisions de l'utilisateur (« lance tout ») + relevé de phase 27 (`27-RELEVE.md`) + STATE.md « Contexte technique v1.7 »

<domain>
## Phase Boundary

Une NOUVELLE source de session, en LECTURE SEULE, qui lit les métadonnées par session écrites par l'app bureau
Claude et les joint aux sessions du widget par `cliSessionId`. Elle apporte : le **titre**, le **dernier
focus** (`lastFocusedAt`, consommé par la phase 30), et la **classification de fin de tour** (`blocked` ⇒
question posée). Elle NE décide PAS « lue » (phase 30) et ne change pas les libellés (phase 28).

</domain>

<decisions>
## Implementation Decisions

### Où et comment lire — VERROUILLÉ
- Racine : `Path.Combine(Environment.GetFolderPath(ApplicationData), "Claude", "claude-code-sessions")`, puis
  `<orgId>\<userId>\local_*.json` (énumération récursive à profondeur 2 ; ne PAS coder les identifiants).
  `%APPDATA%\Claude` est une jonction vers le cache du paquet MSIX : `Path.Combine` uniquement, jamais de
  séparateur mixte (un chemin `/`+`\` a échoué en Python via la jonction).
- Fichiers de ~275 Ko réécrits EN ENTIER par l'app : ouvrir en `FileShare.ReadWrite`, parser avec
  `JsonDocument` tolérant, un fichier tronqué ou invalide ⇒ ignoré ce cycle (pas d'état, pas de log bruyant).
- Ne lire que les fichiers dont le `mtime` < 24 h ; relire un fichier seulement si (mtime, taille) a changé
  depuis la dernière lecture (cache par chemin, en mémoire, pas de fichier). Coût à MESURER sur la vraie
  machine : ~20 fichiers < 24 h, 142 au total.
- Champs lus, et seulement eux : `cliSessionId`, `title`, `titleSource`, `cwd`, `createdAt`, `lastFocusedAt`,
  `lastActivityAt`, `latestUserFrameAt`, `completedTurns`, `isArchived`, `postTurnSummary.status_category`,
  `postTurnSummary.needs_action`. Champs inconnus (`_fixture`, `enabledMcpTools`, …) ignorés.
- Un fichier sans `cliSessionId` ⇒ ignoré (aucune jointure possible), compté dans le diagnostic.

### Ce que la source PRODUIT
- Un type NEUTRE `MetadonneesAppBureau` (ou nom équivalent, aucun type WPF) : `CliSessionId`, `Titre`,
  `DernierFocus` (DateTimeOffset?), `DerniereActivite`, `DernierMessageUtilisateur`, `ToursTermines`,
  `Archivee`, `ClassificationFinDeTour` (enum `Inconnue | Terminee | Bloquee | PreteARevue`),
  `MotifBlocage` (texte `needs_action`).
- Elle n'est PAS un `ISessionSource` qui dépose des `SignalSession` d'activité (elle ne sait pas si la session
  travaille) — SAUF pour `blocked` : une classification `blocked` avec `needs_action` non vide est déposée
  comme signal d'ATTENTE (`WaitingAttention`, motif = `needs_action`), datée par `lastActivityAt`, et entre
  dans l'arbitrage FUS-01 comme troisième source (`SourceSession.AppBureau`, rang après Hook et avant
  Transcript à âge égal — décision écrite et testée). `completed` / `review_ready` ne déposent rien.
- `postTurnSummary` est TRANSITOIRE (effacé au redémarrage de l'app, absent sur la plupart des sessions) :
  jamais un état persistant, toujours un indice daté.

### Le titre (APP-02)
- `SessionSnapshot` gagne un champ optionnel `Titre` (string?) — l'enrichissement se fait dans le moniteur
  après l'arbitrage (jointure par id), pas dans les sources d'activité.
- Widget : `Project` affiche le titre s'il existe, sinon le dossier ; l'info-bulle affiche TOUJOURS
  « <titre> — <dossier> » (ou le dossier seul). Titre long : `TextTrimming=CharacterEllipsis`, largeur des
  templates inchangée (compacité).

### Diagnostic (APP-04)
- Nouvelle section « Source app-bureau » : dossier trouvé / absent, nombre de fichiers énumérés, lus, ignorés
  (sans `cliSessionId`, illisibles), jointures réussies avec les sessions affichées ; et par session affichée :
  titre, dernier focus (« il y a N min »), classification.
- Une source absente est écrite « absente (dossier introuvable) », jamais omise.

### Lecture seule (APP-05)
- Garde par test : aucun `File.Write*`, `FileMode.Create`, `Delete`, `Move` dans le fichier de la source ; le
  chemin racine n'apparaît dans aucun code d'écriture. Test falsifié par mutation avant commit.

### Claude's Discretion
- Nom exact des types, découpage en plans, stratégie de cache (tant que la mesure est faite), format des
  lignes du diagnostic.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Vérité-terrain
- `.planning/phases/27-le-relev-avant-la-r-gle/27-RELEVE.md` — champs, jonction, transitoire, gestes A/B
- `tests/Chronos.Tests/TestData/DesktopAppSessions/*.json` + `README.md` — six fixtures RÉELLES à utiliser
  dans les tests (jamais de fixture régénérée par sérialisation)
- `.planning/STATE.md` — « Contexte technique v1.7 »

### Code touché
- `src/Chronos/Services/SessionMonitor.cs` — collecte des signaux, enrichissement par le titre
- `src/Chronos/Services/ArbitrageSessions.cs` — `SourceSession` (nouvelle valeur), `Departager`
- `src/Chronos/Services/SessionSnapshot.cs` — champ `Titre`
- `src/Chronos/Services/DiagnosticService.cs` — nouvelle section
- `src/Chronos/ViewModels/SessionsViewModel.cs`, `src/Chronos/Resources/SessionStyles.xaml` — titre + info-bulle
- `src/Chronos/App.xaml.cs` — câblage DI (`CompositionRootTests`)
- `tests/Chronos.Tests/GardesPerimetreTests.cs` — aucun type `Uia*` / `*ForegroundWatch` : nommer autrement

</canonical_refs>

<specifics>
## Specific Ideas

- Test de jointure : la fixture `session-courante-geste-b.json` (cliSessionId 11456cab…) jointe à un signal
  hook/transcript du même id ⇒ titre « Session A », dernier focus 20:30:44.
- Test `blocked` : `fin-de-tour-blocked.json` ⇒ signal `WaitingAttention` daté `lastActivityAt`, motif
  `needs_action` ; `fin-de-tour-completed.json` et `-review-ready.json` ⇒ aucun signal.
- Test dégradation : dossier absent ⇒ `Inspecter` identique à v1.6 (même liste, mêmes états), diagnostic
  « absente ».

</specifics>

<deferred>
## Deferred Ideas

- Utiliser `lastActivityAt` comme battement côté app (indépendant des hooks) — piste pour après v1.7.
- `SessionStart.source = resume` / `SessionEnd.reason` — phase 28 ou 30 selon le plan (relevé de phase 27, obs. 1).

</deferred>

---

*Phase: 29-ce-que-l-app-bureau-sait-de-chaque-session*
*Context gathered: 2026-09-25 — décisions consignées par l'agent en mode autonome, d'après le relevé de phase 27*
