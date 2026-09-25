# Phase 29 : Ce que l'app bureau sait de chaque session — Recherche

**Researched:** 2026-09-25
**Domain:** lecture seule d'un format interne non documenté (Electron/MSIX) depuis un overlay WPF .NET 8,
arbitrage FUS-01 à trois sources, enrichissement d'un widget à 8 gabarits × 9 thèmes, diagnostic OBS-01
**Confidence:** HIGH sur les faits (tout est mesuré sur cette machine, en lecture seule, ou lu dans le dépôt et
dans le code de l'app 2.9939.2.0) ; MEDIUM sur ce qui dépend du comportement interne de l'app (code minifié du
jour, susceptible de changer) — chaque zone MEDIUM est nommée.

> **Découverte bloquante (Q0), à lire avant tout plan :** la racine VERROUILLÉE dans le contexte
> (`%APPDATA%\Claude\claude-code-sessions`) **n'existe pas pour l'overlay**. `%APPDATA%\Claude` n'est pas une
> jonction : c'est une vue **virtualisée MSIX** que seuls voient les processus de l'arbre de l'app bureau
> (dont Claude Code, son bash, son PowerShell, `dotnet test` lancé d'ici… et les hooks). L'overlay, lancé par
> `explorer.exe`, ne la voit pas. Le seul chemin lisible des deux côtés est
> `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude-code-sessions`. Coder la
> racine telle que verrouillée livrerait une phase verte en test et **« absente » en production**.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Phase Boundary (copié tel quel)

Une NOUVELLE source de session, en LECTURE SEULE, qui lit les métadonnées par session écrites par l'app bureau
Claude et les joint aux sessions du widget par `cliSessionId`. Elle apporte : le **titre**, le **dernier
focus** (`lastFocusedAt`, consommé par la phase 30), et la **classification de fin de tour** (`blocked` ⇒
question posée). Elle NE décide PAS « lue » (phase 30) et ne change pas les libellés (phase 28).

### Locked Decisions (copiées telles quelles)

#### Où et comment lire — VERROUILLÉ
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

#### Ce que la source PRODUIT
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

#### Le titre (APP-02)
- `SessionSnapshot` gagne un champ optionnel `Titre` (string?) — l'enrichissement se fait dans le moniteur
  après l'arbitrage (jointure par id), pas dans les sources d'activité.
- Widget : `Project` affiche le titre s'il existe, sinon le dossier ; l'info-bulle affiche TOUJOURS
  « <titre> — <dossier> » (ou le dossier seul). Titre long : `TextTrimming=CharacterEllipsis`, largeur des
  templates inchangée (compacité).

#### Diagnostic (APP-04)
- Nouvelle section « Source app-bureau » : dossier trouvé / absent, nombre de fichiers énumérés, lus, ignorés
  (sans `cliSessionId`, illisibles), jointures réussies avec les sessions affichées ; et par session affichée :
  titre, dernier focus (« il y a N min »), classification.
- Une source absente est écrite « absente (dossier introuvable) », jamais omise.

#### Lecture seule (APP-05)
- Garde par test : aucun `File.Write*`, `FileMode.Create`, `Delete`, `Move` dans le fichier de la source ; le
  chemin racine n'apparaît dans aucun code d'écriture. Test falsifié par mutation avant commit.

### Claude's Discretion (copiée telle quelle)
- Nom exact des types, découpage en plans, stratégie de cache (tant que la mesure est faite), format des
  lignes du diagnostic.

### Deferred Ideas — OUT OF SCOPE (copiées telles quelles)
- Utiliser `lastActivityAt` comme battement côté app (indépendant des hooks) — piste pour après v1.7.
- `SessionStart.source = resume` / `SessionEnd.reason` — phase 28 ou 30 selon le plan (relevé de phase 27, obs. 1).

### Specific Ideas (rappel du contexte)
- Test de jointure : `session-courante-geste-b.json` (cliSessionId 11456cab…) jointe à un signal hook/transcript
  du même id ⇒ titre « Session A », dernier focus 20:30:44 (= **2026-09-25T18:30:44.976Z**).
- Test `blocked` : `fin-de-tour-blocked.json` ⇒ signal `WaitingAttention` daté `lastActivityAt`
  (= **2026-09-24T09:00:06.552Z**), motif `needs_action` ; `-completed` et `-review-ready` ⇒ aucun signal.
- Test dégradation : dossier absent ⇒ `Inspecter` identique à v1.6, diagnostic « absente ».
</user_constraints>

**Deux amendements aux décisions verrouillées sont nécessaires — ils ne sont pas des alternatives, ce sont des
corrections de faits.** Le planificateur doit les faire valider (ou les consigner comme ajustement écrit, sur le
modèle de LUE-02 en phase 27) :
1. **Racine** : « `%APPDATA%\Claude` est une jonction » est faux (Q0). Résoudre la racine parmi des candidats,
   le chemin physique du paquet EN PREMIER, `%APPDATA%\Claude\…` en repli. La phrase « un chemin `/`+`\` a échoué
   en Python via la jonction » est aussi fausse dans sa cause : Python (alias MSIX du Python Manager) tournait
   HORS de l'arbre de l'app ; les séparateurs mixtes marchent (mesuré). `Path.Combine` reste la bonne pratique.
2. **Mode de partage** : `FileShare.ReadWrite` est nécessaire mais pas suffisant ; ouvrir en
   `FileShare.ReadWrite | FileShare.Delete`, tenir la poignée le moins longtemps possible (Q1). C'est un
   sur-ensemble plus permissif pour l'app : aucune décision n'est contredite.

---

<phase_requirements>
## Phase Requirements

| ID | Description (REQUIREMENTS.md) | Ce que la recherche apporte |
|----|-------------------------------|-----------------------------|
| **APP-01** | Lire `…\claude-code-sessions\<org>\<user>\local_*.json`, joindre par `cliSessionId`, `FileShare.ReadWrite`, JSON invalide ignoré, < 24 h, `Path.Combine`, dégradation v1.6 | **Q0** (la racine réelle, mesurée des deux côtés de la virtualisation), **Q1** (mécanisme d'écriture de l'app lu dans son code : temporaire + renommage, 3 essais, **repli en troncature sur place** ; partage `ReadWrite \| Delete` prouvé par test), **Q2** (coût mesuré : 24 ms médian / 71 ms max sans cache, 0,73 ms avec — cache justifié), Pattern 1, Pièges 1-5 |
| **APP-02** | Titre à la place du dossier, dossier en repli et en info-bulle | **Q4** (`Titre` en dernier paramètre optionnel : 0 site de construction à toucher sur ~37 ; 8 info-bulles et 4 textes, fichier:ligne ; mesure de largeur : titres jusqu'à 272 DIP contre 161 pour le plus long dossier ⇒ `MaxWidth` requis dans Pastilles et Marge), Pattern 4 |
| **APP-03** | `blocked` + `needs_action` = attente observée, motif lisible ; `completed`/`review_ready` ne créent rien | **Q3** (rang à âge égal, garde « l'app qualifie, elle ne crée pas », horizon 8 h, `postTurnSummary` effacé par l'app au début du tour suivant — lu dans son code —, datation par `lastActivityAt` et son dérapage mesuré de +3 min 10 s), Pattern 2, Pièges 6-9 |
| **APP-04** | Diagnostic : trouvé/absent, lus, jointures, champs manquants ; par session affichée titre, focus, classification | **Q5** (point d'insertion `DiagnosticService.cs:558-566`, même `lecture` que le widget, format proposé, garde OBS-01 étendue), Pattern 3 |
| **APP-05** | Jamais d'écriture dans le dossier de l'app, garde par test | **Q6** (garde textuelle + garde comportementale « empreinte identique », trois mutations de falsification), Code Examples |

**Critères de succès de la ROADMAP** (ils gouvernent la vérification) : (1) le titre remplace le dossier, dossier
en info-bulle, titres longs coupés proprement sur 8 styles × 9 thèmes ; (2) `blocked` + `needs_action` ⇒ « En
attente » au rang des questions, motif lisible ; `completed`/`review_ready` ne créent aucune ligne ; (3) dossier
absent, JSON invalide, fichier à moitié écrit, champ absent, `cliSessionId` manquant ⇒ comportement v1.6 exact,
aucun clignotement ; < 24 h, `FileShare.ReadWrite`, chemin sans séparateur mixte ; (4) le diagnostic dit ce que
la source sait et ce qu'elle ne sait pas ; (5) lecture seule prouvée, fixtures réelles, gardes intactes.
</phase_requirements>

---

## Project Constraints (from CLAUDE.md)

| Directive | Conséquence pour cette phase |
|---|---|
| C# / .NET 8 (`net8.0-windows`) / WPF / MVVM strict (CommunityToolkit.Mvvm) / DI | Le lecteur est un singleton DI injecté dans `SessionMonitor` ; `Infobulle` est une `[ObservableProperty]` de `SessionItemVm` ; les mots et l'info-bulle naissent dans `Services/AffichageSessions` |
| Aucun type WPF dans `Services/` (`ServicesLayerPurityTests`) | `MetadonneesAppBureau`, `LectureAppBureau`, `ClassificationFinDeTour`, `LecteurAppBureau` sont purs (`System.Text.Json`, `System.IO`) |
| Aucune dépendance native | Aucun P/Invoke dans le lecteur : `FileStream` + `FileShare` suffisent (le P/Invoke de cette recherche est resté dans le bloc-notes) |
| Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin | `%LOCALAPPDATA%\Packages\…\LocalCache` est sous le profil : conforme. Lecture seule stricte de tout ce qui appartient à l'app |
| Ne jamais présenter une estimation comme exacte / « observé, jamais déduit » | `blocked` est une classification **observée** faite par l'app ; `need_input`, `failed` (catégories vues dans le code de l'app, jamais sur disque) ne deviennent PAS des attentes |
| Robustesse : aucune source ≠ crash, parsing tolérant | Tout le lecteur est sous `try/catch` ; un fichier illisible garde la dernière lecture valide (pas de clignotement) |
| UI et commentaires en **français** | Noms proposés : `LecteurAppBureau`, `MetadonneesAppBureau`, `LectureAppBureau`, `ClassificationFinDeTour`, `AffichageSessions.Nom/Infobulle/MotifLisible` |
| « Activer frontend-design + windows-wpf sur les tâches ui » | Aucun de ces deux skills n'est installé (relevé de la phase 28) ; la vérification visuelle passe par la galerie `--sessions` et le test WPF 8 × 9 |
| `Assembly.Location` interdit | Les gardes textuelles lisent `GardesPerimetreTests.CheminSources()` (attribut MSBuild `CheminSourcesChronos`) ; les fixtures se chargent par `[CallerFilePath]` + `TestData/` (motif `ClaudeSettingsReconcilerTests.cs:24-25`) |
| Gardes à garder vertes sans assouplir leurs listes (ROADMAP) | `GardesPerimetreTests` (aucun nom de type contenant `Uia`, finissant par `ForegroundWatch`, ni `DesktopHealth`/`SessionKind`/`SessionOrigin` — les noms proposés passent), `ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests` (**interdit `FromUnixTimeMilliseconds` hors exemptions** : passer par `UsageNormalization.InstantDepuisEpochMillisecondes`), `ContratHooksDocumenteTests`, `GardesDoctrineTests` |
| Ne pas lancer l'overlay ; aucune écriture hors dépôt | Toute mesure de cette recherche : bloc-notes de session, lecture seule. **Et Q0 impose un corollaire** : une vérification faite depuis Claude Code voit la vue virtualisée, pas celle de l'overlay |

---

## Summary

Le format est conforme au relevé : 138 `local_*.json` à profondeur exactement 2 (un seul `<org>\<user>`), tous
analysables, 18 modifiés depuis moins de 24 h (266 à 287 Ko chacun, 4,9 Mo au total), 115/138 avec
`cliSessionId` et `title` (18/18 parmi les récents), 12 avec `postTurnSummary` (6 `completed`, 3 `blocked`,
3 `review_ready`). Les six fixtures de la phase 27 couvrent ces cas. Mais trois faits changent la façon de lire.

**1. Le chemin (Q0).** `%APPDATA%\Claude` n'existe que dans l'arbre de processus de l'app bureau (virtualisation
MSIX du système de fichiers). Une sonde .NET 8 lancée par WMI (hors de l'arbre, comme l'overlay lancé par
`explorer.exe`) : `%APPDATA%\Claude` absent, `%APPDATA%\Chronos\sessions` absent ; le chemin physique
`%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude-code-sessions` présent (140
`.json`). Le journal de démarrage de l'overlay lui-même le confirme (0 fichier d'état vu le 09-23 alors que le
dossier virtualisé en contenait). La même cause explique, hors périmètre, deux défauts plus anciens : **les
fichiers de hook des sessions de l'app bureau n'ont jamais été visibles de l'overlay**, et le `usage.json`
« figé à `resets_at: 9` » du diagnostic v1.5 était la copie virtualisée, pas celle de l'overlay.

**2. L'écriture de l'app (Q1).** Lu dans `app.asar` (2.9939.2.0) et mesuré : `writeFileAtomic` écrit
`.local_<id>.json.<pid>.<rand>.tmp`, `fsync`, puis renomme par-dessus la cible (`MoveFileExW`, libuv) ; sur
`EPERM/EBADF/EACCES/EBUSY` il réessaie deux fois (50 ms, 100 ms) puis **tronque et réécrit la cible sur place**.
Le guet (150 s, 57 816 lectures à ~100 Hz) a vu 2 réécritures, chacune avec un **nouvel index NTFS**, zéro
fichier vide, zéro JSON invalide. Un lecteur qui tient la cible ouverte fait échouer `MoveFileExW` quel que soit
son mode de partage (prouvé) ; seul un repli en place peut alors exposer un fichier tronqué. D'où : partage
`ReadWrite | Delete` (le repli en place ne doit jamais échouer à cause de nous), poignée tenue le temps de copier
les octets (0,36 ms médian pour 289 Ko), analyse après fermeture, relecture seulement sur changement (cache).

**3. La datation de `blocked` (Q3).** Le code de l'app efface `postTurnSummary` au début du tour suivant et à
l'envoi d'un message, et l'enregistre aussitôt (`dropPostTurnSummary` → `saveSessionNow`) : un `blocked` ne
survit pas à la réponse. Mais `lastActivityAt` **bouge après la fin du tour** (mesuré : +3 min 10 s sur
11456cab, activité d'agents en arrière-plan, résumé inchangé) : dater l'épisode par la valeur courante le
« rajeunit » et rejoue le Piège 1 de la phase 28 (NET-03 ramène une session traitée ; la phase 30 verrait une
attente postérieure au focus). Recommandation : dater par `lastActivityAt` **tel que lu à la première
apparition** de ce résumé (identifié par `postTurnSummaryFor`) — c'est encore « daté par `lastActivityAt` »,
mais figé par épisode (TRT-02).

Le reste est mécanique et cartographié : `SourceSession.AppBureau` inséré entre `Hook` et `Transcript`
(l'ordre de déclaration EST le rang, `ArbitrageSessions.cs:6-7`) ; `Titre` en dernier paramètre optionnel de
`SessionSnapshot` (0 site à toucher) ; un `Infobulle` produit par `AffichageSessions` et bindé dans les 8
gabarits ; un `MaxWidth` dans Pastilles et Marge (sinon la fenêtre, en `SizeToContent`, s'élargit : 10 titres
réels sur 18 dépassent le plus long dossier) ; une section de diagnostic lue dans la MÊME `LectureSessions`.

**Primary recommendation :** un `LecteurAppBureau` pur, injecté en dernier paramètre optionnel de
`SessionMonitor` (null ⇒ v1.6 exact), qui résout la racine parmi des candidats (paquet MSIX d'abord), lit
`local_*.json` à profondeur 2 en `ReadWrite | Delete` par copie d'octets, met en cache par (mtime, taille) et
garde la dernière lecture valide ; le moniteur dépose un `WaitingAttention` AppBureau seulement pour une session
déjà connue d'une autre source, puis enrichit les retenus par `Titre` après l'arbitrage.

---

## Q0 — La racine réelle : deux vues de `%APPDATA%` (DÉCOUVERTE BLOQUANTE)

### 0.a Ce qui a été mesuré (2026-09-25, 20 h 45-21 h 00, lecture seule)

Sonde .NET 8 jetable (bloc-notes de session) exécutée deux fois : depuis le bash de Claude Code (« dedans »), puis
créée par `Win32_Process.Create` (WMI, parent `WmiPrvSE`, « dehors ») — l'overlay réel a pour parent
`explorer.exe` (PID 27872), il est dehors lui aussi. Les deux processus : `GetCurrentPackageFullName` ⇒ aucun
paquet (le mécanisme exact qui applique la virtualisation à l'arbre de l'app n'est pas établi — MEDIUM ; les
faits, eux, sont HIGH).

| Chemin testé (`Directory.Exists`, puis `*.json` récursif) | Dedans (arbre `claude.exe`) | Dehors (WMI ≈ overlay) |
|---|---|---|
| `%APPDATA%\Claude\claude-code-sessions` (`Path.Combine`) | **existe, 140** | **absent** |
| même chemin à séparateurs mixtes `Roaming/Claude/claude-code-sessions` | existe, 140 | absent |
| `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude-code-sessions` | existe, 140 | **existe, 140** |
| `%APPDATA%\Chronos\sessions` (dossier des hooks) | existe, 5 | **absent** |
| `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Chronos\sessions` | existe, 5 | existe, 5 |
| `%APPDATA%\Claude` | existe | **absent** |

Contrôles croisés :
- `fsutil reparsepoint query %APPDATA%\Claude` (dedans) ⇒ « n'est pas un point d'analyse » : ce n'est **pas** une
  jonction. PowerShell 5.1 affiche pourtant `Target = …\LocalCache\Roaming\Claude` : c'est ce qui a fait croire
  à une jonction lors du relevé.
- Python lancé par l'alias `python` (paquet MSIX `PythonSoftwareFoundation.PythonManager`) tourne **dehors** :
  `'Claude' in os.listdir(APPDATA)` ⇒ `False`. C'est la vraie cause de l'échec « séparateurs mixtes » du relevé.
- **Le journal de l'overlay lui-même** (`%APPDATA%\Chronos\chronos.log`, réel, 2026-09-23 11:09:34) : « Fichiers
  d'état (C:\Users\Tanguy\AppData\Roaming\Chronos\sessions) : 0 », alors que le dossier virtualisé contenait
  déjà `3498ae3d…json` (mtime 2026-09-13 18:51).
- `usage.json` : copie virtualisée (mtime 2026-07-10 08:55) = `{"five_hour":{"used_percentage":10,"resets_at":9},…}`
  — **exactement** le fichier que le diagnostic v1.5 décrivait comme « figé » ; le `usage.json` réel de
  l'overlay (2026-07-09 22:55) contient d'autres valeurs (75 %, 45 %).
- Documentation Microsoft (MSIX, Windows 10 1903+) : « All newly created files and folders in the user's AppData
  folder … are written to a private per-user, per-app location; but merged at runtime to appear in the real
  AppData location » ; « the OS will open the file from the per-user, per-package location first ». Un dossier
  qui n'existait pas dans le vrai `AppData` (`Chronos\sessions`) est donc créé dans le cache privé du paquet.

### 0.b Conséquence pour la phase 29 — la racine se RÉSOUT, elle ne se suppose pas

```csharp
// Candidats, dans CET ordre. Le premier qui existe gagne ; le diagnostic dit lequel, et liste les autres.
//  1. %LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Claude\claude-code-sessions — install MSIX : le SEUL
//     chemin visible à la fois de l'overlay (lancé par explorer) et de l'arbre de l'app. Le nom de famille du
//     paquet (Claude_pzs8sxrjxfjjc) n'est pas un identifiant d'organisation ni d'utilisateur : c'est celui du
//     produit ; l'énumérer par motif « Claude_* » évite de coder le hachage de l'éditeur (1 seul sur 183 paquets).
//  2. %APPDATA%\Claude\claude-code-sessions — install non-MSIX, ou processus lancé DANS l'arbre de l'app.
public static IReadOnlyList<string> CandidatsParDefaut(string localAppData, string appData)
```

- `%LOCALAPPDATA%\Packages` : 183 entrées, un seul `Claude_*`. Résoudre à chaque cycle coûte une énumération de
  dossiers ; résoudre une fois, puis re-résoudre seulement si la racine retenue disparaît, suffit.
- Les tests n'utilisent JAMAIS les candidats par défaut (racine temporaire injectée) ; un test unitaire de
  `CandidatsParDefaut` sur une arborescence temporaire (`<tmp>\Packages\Claude_xyz\LocalCache\Roaming\Claude\claude-code-sessions`
  + `<tmp>\Roaming\Claude\…`) fixe l'ordre.

### 0.c Conséquences HORS périmètre, à remonter à l'orchestrateur (décision utilisateur)

1. **Les fichiers de hook des sessions de l'app bureau sont invisibles de l'overlay** depuis toujours : le mode
   `--hook` écrit `Path.Combine(ApplicationData, "Chronos", "sessions")` (`App.xaml.cs:149-150`) depuis l'arbre
   de l'app ⇒ cache privé du paquet ; l'overlay lit le vrai `%APPDATA%\Chronos\sessions`, qui n'existe pas. En
   production, pour ces sessions, le widget ne vit QUE des transcripts. Le relevé de la phase 27 (fichiers de
   hook qui « disparaissent à la frontière des tours ») portait sur le dossier virtualisé, que l'overlay ne lit
   pas. Correctif possible, même résolveur : le moniteur lit l'union des deux dossiers d'état. **Ce n'est pas
   dans APP-01..05** : à trancher (phase 29 élargie, ou phase insérée) avant de planifier 28/29/30 — la phase 28
   raisonne sur des hooks (Piège 3 de sa recherche) que l'overlay ne voit pas pour ces sessions.
2. **Le pont statusLine** (`--statusline`, lancé par Claude Code dans l'arbre de l'app) écrit lui aussi dans la
   copie virtualisée : cause racine probable du `usage.json` « figé » de v1.5.
3. **Protocole de vérification** : tout constat « in vivo » fait depuis Claude Code (bash, PowerShell,
   `dotnet test`, Python lancé par un chemin complet) voit la vue virtualisée. Un constat valant pour l'overlay
   se fait **hors de l'arbre** : sonde lancée par WMI (motif de cette recherche) ou overlay publié (phase 31).

---

## Q1 — Lecture concurrente : comment l'app réécrit, comment lire

### 1.a Le code de l'écrivain (lu dans `app.asar`, 2.9939.2.0 — MEDIUM : code minifié du jour)

```js
// writeFileAtomic → #g : fichier temporaire EXCLUSIF dans le MÊME dossier, écrit, fsync, fermé
`.${e.leaf}.${process.pid}.${Math.random().toString(36).slice(2,8)}.tmp`   // O_WRONLY|O_CREAT|O_EXCL
// #_ : renommage par-dessus la cible, 3 essais, attentes 50 puis 100 ms, codes réessayés :
hu = new Set(["EPERM","EBADF","EACCES","EBUSY"])
// … puis REPLI : ouverture O_WRONLY|O_CREAT de la cible, truncate(0), write, sync — ÉCRITURE EN PLACE
```
Constantes de l'app : `wW="claude-code-sessions"`, `TW="local_"`, `b8n="archived-sessions.idx"`. libuv
(`src/win/fs.c`, v1.x) : `fs__rename` = `MoveFileExW(..., MOVEFILE_REPLACE_EXISTING)`, sans sémantique POSIX.

### 1.b Ce qui a été observé (guet .NET 8, 150 s, 6 fichiers < 2 h, ~100 Hz)

| Grandeur | Valeur |
|---|---|
| Lectures | 57 816 (ouverture `ReadWrite \| Delete` + index NTFS + `JsonDocument.Parse`) |
| Réécritures vues | 2 (`local_06b830b5…` à 20:54:47.780, `local_eb880bbb…` à 20:54:50.492) |
| Index NTFS changé à chaque réécriture | **2 / 2** (`46000000201993 → 450000000B41D2`) ⇒ remplacement par renommage |
| Taille avant → après | 276 623 → 276 623 ; 276 770 → 276 770 (inchangée : la taille seule ne détecte pas un changement) |
| Fichier vide / JSON invalide / erreur d'E/S | **0 / 0 / 0** |

Réserve honnête : deux réécritures seulement. L'absence de fichier tronqué vient surtout du mécanisme (le
renommage est atomique : on lit l'ancien ou le nouveau, entier), pas du nombre d'observations.

### 1.c Ce que le mode de partage change (preuve jetable, dossier temporaire, .NET 8)

| Écrivain | Lecteur ouvert en `ReadWrite` | Lecteur ouvert en `ReadWrite \| Delete` | Aucun lecteur (témoin) |
|---|---|---|---|
| `MoveFileEx` par-dessus la cible (libuv / `File.Move`) | ÉCHOUE (accès refusé) | **ÉCHOUE** (accès refusé) | réussi |
| Renommage POSIX (`FileRenameInfoEx`, `POSIX_SEMANTICS`) | ÉCHOUE (err 32) | **réussi** | réussi |

Conséquences, dans l'ordre d'importance :
1. **Aucun mode de partage n'empêche notre poignée de faire échouer `MoveFileExW`.** Si elle était tenue au-delà
   des ~150 ms de réessais, l'app basculerait en écriture EN PLACE — et c'est là, et seulement là, qu'un lecteur
   peut voir 0 octet ou un JSON à moitié écrit. Parade : **tenir la poignée le moins longtemps possible** —
   copier les octets, fermer, analyser ensuite. Mesuré : 0,36 ms médian, 2,2 ms p90 pour 289 Ko (PowerShell,
   surcoût compris). `JsonDocument.Parse(stream)` tiendrait la poignée pendant l'analyse (1,3-1,4 ms) : à éviter.
2. **`FileShare.ReadWrite` est obligatoire** : sans lui, notre lecture ferait échouer le repli en place de l'app
   (ouverture `O_WRONLY` refusée ⇒ exception côté app). `File.ReadAllText/ReadAllBytes/OpenRead` ouvrent en
   `FileShare.Read` : **interdits** dans le lecteur (Piège 2).
3. **`FileShare.Delete`** : sans effet contre `MoveFileExW`, mais laisse passer un renommage POSIX ou une
   suppression (l'app supprime des fichiers : `deleted_<uuid>`). Coût nul, à ajouter.
4. **Relire seulement sur changement** (cache) réduit la tenue moyenne de poignée à ~0 par cycle.
5. Un JSON invalide ou tronqué ⇒ la lecture ne met PAS à jour le cache (elle sera retentée au cycle suivant) et
   la **dernière lecture valide est conservée** : pas de titre qui clignote (critère 3). Première lecture ratée
   ⇒ pas de métadonnées pour ce fichier, aucune exception, compteur « illisibles » + 1.

---

## Q2 — Coût mesuré, et le cache

Méthode : sonde .NET 8 Release (bloc-notes), cycle = énumération `<org>\<user>\local_*.json` + filtre mtime
< 24 h + lecture/analyse des champs verrouillés ; 1 appel froid puis 30 cycles chauds ; 3 exécutions dedans,
1 exécution **dehors** (WMI, chemin physique — c'est celle qui vaut pour l'overlay).

| | Dehors (WMI) | Dedans (3 exécutions) |
|---|---|---|
| Fichiers énumérés / < 24 h / lus | 138 / 18 / 18 | 138 / 18 / 18 |
| Premier appel (JIT + disque) | 95,7 ms | 84-98 ms |
| **Sans cache** : médiane / p90 / max | **24,1 / 68,2 / 71,1 ms** | 17,6-27,9 / 67-71 / 72-74 ms |
| **Avec cache (mtime, taille)** : médiane / p90 / max | **0,73 / 0,90 / 1,06 ms** | 0,41-0,92 / 0,44-1,03 / 0,47-1,08 ms |
| Énumération seule (médiane) | 0,75 ms | 0,41-0,92 ms |
| Lecture + analyse du plus gros fichier (288 Ko) | 1,32 ms | 1,44 ms |
| Octets lus par cycle sans cache | 4,9 Mo (18 × 266-287 Ko) ⇒ ~2,4 Mo/s en continu | idem |

**Verdict : cache par chemin, clé (mtime UTC, taille), en mémoire.** Sans lui, 24 ms médian mais **p90 à 68 ms**
(le pic vient des tampons de 280 Ko : pression GC) toutes les 2 s, et 2,4 Mo/s de lecture continue pour des
fichiers qui ne changent presque jamais (2 réécritures en 150 s). Avec lui, ~0,7 ms. Le seuil de 50 ms fixé par
la phase 28 pour les transcripts est dépassé au p90 sans cache.

Précisions sur la clé : la taille ne change pas à la réécriture (1.b), c'est le mtime qui porte le changement ;
l'index NTFS en serait une troisième composante plus sûre mais demande un P/Invoke (interdit : aucune dépendance
native) — (mtime, taille) suffit. Purger du cache les chemins non énumérés au cycle courant (sessions supprimées)
et ceux qui sortent de la fenêtre des 24 h.

Concurrence : `Inspecter` est appelé par le minuteur UI (2 s) et par `BuildReportAsync` (ses continuations
reviennent sur le contexte UI — aucun `ConfigureAwait(false)` dans `DiagnosticService.cs`). Un `lock` autour du
cache coûte ~0 et ferme la question.

---

## Q3 — L'arbitrage FUS-01 à trois sources

### 3.a Le rang à âge égal

`ArbitrageSessions.cs:6-7` : « L'ORDRE DE DÉCLARATION EST SIGNIFIANT : il sert de départage à ÂGE ÉGAL » ; le
rang est `((int)a.Source).CompareTo((int)b.Source)` (`:122`). **Insérer `AppBureau` entre `Hook` et
`Transcript`** : c'est la forme native du contrat, et `SourceSession` n'est persisté nulle part (seuls usages :
`SessionMonitor.cs:90, 110`, `DiagnosticService.cs:614-615`, `ArbitrageSessions.cs:122`). Tenir l'ordre par
test (`Enum.GetValues<SourceSession>()` = `[Hook, AppBureau, Transcript]`) et par permutation. Le précédent
contraire de `SessionActivity` (« ajoutée en fin ») ne s'applique pas : là, l'ordre n'était pas le contrat.

Mettre à jour dans le même geste : la XML-doc des rangs (`:61-71`, « deux sources »), `LibelleSourceSession`
(`DiagnosticService.cs:612-617`, sinon le repli `_ => "une source non nommée"` masquerait l'oubli — ajouter un test
« chaque valeur de `SourceSession` a un libellé nommé »), et le commentaire FUS-01 de `SessionMonitor.Inspecter` (`SessionMonitor.cs:71-79`, « FUSIONNE deux sources »).

### 3.b Qui a le droit de déposer — « l'app bureau qualifie une ligne, elle n'en crée pas »

Recommandation (Question ouverte 2) : un signal AppBureau n'est déposé que pour un identifiant **déjà présent
parmi les signaux du cycle** (transcript ou hook). Raisons : (1) le périmètre du widget reste défini par les
sources d'activité (SRC-01, phase 21) et `MaxSessions` n'est pas contourné ; (2) la dégradation v1.6 est exacte
par construction (sans AppBureau, même population) ; (3) les 3 `blocked` sur disque aujourd'hui datent de 25 à
36 h — sans garde ni horizon, ils fabriqueraient des lignes.

Conditions de dépôt, toutes nécessaires :

| Condition | Pourquoi | Fait mesuré |
|---|---|---|
| `status_category == "blocked"` (ordinal exact) | verrouillé ; `need_input`, `failed` existent dans le code de l'app (`En(e)`) mais 0/138 sur disque ⇒ `Inconnue`, catégorie brute au diagnostic | 3 `blocked` sur 138 |
| `needs_action` non vide (après `Trim`) | verrouillé | 40, 63, 50 caractères |
| `isArchived != true` | l'app elle-même tait ses notifications pour une session archivée (`o.isArchived … desktop_notification_suppressed`) | 2/138 archivées |
| `postTurnSummaryFor == lastAssistantUuid` quand les deux existent | règle de péremption de l'app elle-même (`markIdleAndNotify`) | 12/12 égaux aujourd'hui |
| âge de l'instant < `HorizonsSessions.Abandon` (8 h, livré par la phase 28) | même horizon que hooks et transcripts ; un fichier < 24 h peut porter une activité de 36 jours (réécriture en masse au redémarrage de 16:58) | `mtime − lastActivityAt` : de 0 s à 36 j sur les 18 récents |
| identifiant connu d'une autre source ce cycle | 3.b | — |

### 3.c La datation — le fait qui compte

Relevé des 12 résumés présents (jointure avec le transcript par `summarizes_uuid`) :
- 12/12 : `summarizes_uuid` = uuid du **dernier message assistant `end_turn`** du transcript.
- `lastActivityAt − end_turn` : +1,4 à +3,5 s pour 11 sessions au repos (le résumé est écrit juste après la fin
  du tour : le signal AppBureau gagne donc par FRAÎCHEUR contre le `WaitingTurn` du transcript, pas par rang).
- **11456cab : +3 min 10 s** (end_turn 18:51:38Z, `lastActivityAt` 18:54:48Z) : activité d'agents en
  arrière-plan après la fin du tour, résumé `review_ready` inchangé. `lastActivityAt` n'est PAS l'instant du
  résumé.

Code de l'app (MEDIUM) : `postTurnSummary` est effacé (`dropPostTurnSummary` → `saveSessionNow`, écriture
immédiate) quand un tour démarre (`isRunning = true`, « label_flipped ») et quand un message utilisateur est
poussé ; il n'est posé que si aucun résumé n'existe et que `lastAssistantUuid` correspond (`TT(e,t,n)`). Donc un
`blocked` ne survit pas à la réponse — mais il **se rajeunit** tant qu'une activité d'arrière-plan fait avancer
`lastActivityAt`. Effets d'un épisode qui se rajeunit (Piège 1 de la phase 28, même mécanique) :
- **NET-03** : `SessionTreatmentTracker` date l'épisode par `UpdatedAt` (`:64-65, 96-100`) et purge le « traité »
  si l'épisode avance (`:104-106`) ⇒ une session « marquée traitée » revient sans avoir rien redemandé ;
- **phase 30 (LUE-01)** : une attente datée APRÈS le dernier focus ⇒ jamais « lue ».

**Recommandation (Question ouverte 1)** : l'instant du signal = `lastActivityAt` **lu à la première apparition
de ce résumé**, mémorisé par (cliSessionId, `postTurnSummaryFor`) dans le lecteur ; il n'avance que si
`postTurnSummaryFor` change (nouveau tour résumé). Sans `postTurnSummaryFor`, la valeur courante. C'est toujours
« daté par `lastActivityAt` » (verrou respecté), figé par épisode (TRT-02). Limite assumée : un redémarrage de
l'overlay pendant une activité d'arrière-plan re-mémorise une valeur plus récente (une fois). Alternative plus
exacte, écartée ici : l'horodatage du message `summarizes_uuid` dans le transcript (jointure inter-sources).

### 3.d Les cas, et le résultat attendu (à écrire en test, instants dérivés de `T`)

Notation : E = fin du tour (`end_turn`), le résumé `blocked` est daté E + 1,5 s. Après la phase 28, le transcript
est daté par sa dernière ligne significative ; les fichiers de hook ne sont visibles que des sessions de terminal.

| # | Signaux | Vainqueur | Écran | Commentaire |
|---|---|---|---|---|
| C1 | AppBureau `WaitingAttention`@E+1,5 s ; Transcript `WaitingTurn`@E | AppBureau (fraîcheur) | « En attente », rang question, motif `needs_action` | le cas nominal ; un désaccord est consigné (Piège 8) |
| C2 | l'utilisateur répond : Transcript `Working`@T_rép (> E+1,5 s) ; AppBureau encore en cache (≤ 2 s) | Transcript | « Réflexion » | la fraîcheur suffit même avant que le lecteur voie l'effacement |
| C3 | cycle suivant : résumé effacé par l'app | Transcript | « Réflexion » | plus de signal AppBureau |
| C4 | AppBureau et Transcript au même instant à la ms | AppBureau (rang 2 < 3) | « En attente » | décision verrouillée |
| C5 | Hook et AppBureau au même instant | Hook | selon le hook | décision verrouillée |
| C6 | Hook `WaitingTurn` (Stop)@E+0,3 s ; AppBureau@E+1,5 s | AppBureau | « En attente », question | cas de terminal : ne se produit pas (pas de `local_*.json`), tenu quand même |
| C7 | AppBureau seul (aucun transcript ni hook ce cycle) | — | **aucune ligne** | garde 3.b |
| C8 | AppBureau daté il y a 8 h 01 | — | inchangé | horizon |
| C9 | `completed`, `review_ready`, sans résumé, `need_input`, `failed` | — | inchangé | aucun signal |
| C10 | AppBureau `WaitingAttention` puis Transcript `Working` au cycle suivant | Transcript | « Réflexion » | TRT-01 : sources différentes ⇒ le détecteur NE conclut PAS « répondu » (relais) — inchangé par construction (`prec.Source == v.Source`, garde `ContratHooksDocumenteTests:330`) |
| C11 | silence : AppBureau `WaitingAttention` vieux de 25 min | AppBureau | « En attente » | la règle de silence ne touche que `Working` |

Corpus de permutation (6 signaux ⇒ 720 ordres, helper `Permutations<T>` existant, `ArbitrageSessionsTests.cs:43-53`) :
C1 (2 signaux), C4 (2 signaux), C5 (2 signaux) — trois sessions, chacune tranchée par une règle différente
(fraîcheur, rang AppBureau/Transcript, rang Hook/AppBureau). Assertion de non-vacuité : au moins un couple est
tranché au rang 2 avec `AppBureau` gagnant et un avec `AppBureau` perdant. Nouveau helper
`AppBureau(id, a, maj, motif)` à côté de `Hook`/`Transcript` (`:24-28`).

---

## Q4 — Le titre : le record, le ViewModel, les 8 gabarits

### 4.a `SessionSnapshot` (`SessionSnapshot.cs:27-32`)

**Paramètre positionnel optionnel en DERNIÈRE position** : `string? Titre = null`. Les ~37 constructions
explicites (`AffichageSessionsTests` 19, `DiagnosticServiceTests` 6, `SessionStylesBindingTests` 5,
`ArbitrageSessionsTests` 2, `GesteTraiteTests` 1, `TreatedSessionsTests` 1+1, `SessionMonitor.cs:181`,
`TranscriptSessionSource.cs:113`) et les `new(...)` à type cible (`InspectionSessionsTests.cs:45-46`,
`TreatedSessionsTests.cs:24`) compilent sans retouche. L'enrichissement se fait par `s with { Titre = … }`.
Mettre à jour la XML-doc (`:24-26`, « revenu à cinq champs… un champ qui ne peut plus varier ment par
omission » : `Titre` a un producteur, la phrase reste vraie).

Égalité de record : `Titre` y entre. L'enrichissement se faisant APRÈS `Trancher`, aucun signal arbitré ne porte
de titre ⇒ l'affirmation de `ArbitrageSessions.cs:68-71` (« ces critères épuisent tous les champs ») reste vraie
si on l'écrit : « `Titre` est posé après l'arbitrage et n'y entre jamais ». Garde textuelle possible : dans
`SessionMonitor.cs`, `with { Titre` n'apparaît qu'après `ArbitrageSessions.Trancher(`. Le détecteur observe
`arbitrage.Vainqueurs` (`:119`), non enrichis : inchangé.

### 4.b Où enrichir (`SessionMonitor.Inspecter`, `:80-138`)

Après `var arbitrage = ArbitrageSessions.Trancher(signaux);` (`:113`), AVANT les filtres (`:127-136`) : les
masquées portent aussi leur titre (le diagnostic les liste). `var raw = arbitrage.Retenus.Select(Enrichir)`.

### 4.c Le ViewModel (`SessionsViewModel.cs:189-207`) et la galerie

- `:194` `Project = s.Project` ⇒ `Project = AffichageSessions.Nom(s)` (titre s'il existe, sinon dossier). Garder
  le NOM de propriété `Project` : il est bindé dans les 8 gabarits.
- Nouvelle `[ObservableProperty] private string _infobulle = "";` ⇒ `Infobulle = AffichageSessions.Infobulle(s)`.
- `SessionsPreviewViewModel.cs:27-37` : la galerie partage les gabarits — elle DOIT poser `Infobulle` (sinon
  info-bulle vide, sans erreur : Piège 7 de la phase 28) et gagner un échantillon à **titre long** (≥ 43
  caractères, la longueur maximale mesurée) pour juger la coupure à l'œil.

### 4.d Les gabarits (`Resources/SessionStyles.xaml`, lignes à HEAD — la phase 28 les décalera)

| Style | Info-bulle (`ToolTip="{Binding Project}"` ⇒ `Infobulle`) | Texte `Project` visible | Largeur du texte |
|---|---|---|---|
| 1 Pastilles | `:50` | `:67` | **aucun `MaxWidth`** (Border `MinWidth="180"`, `:49`) |
| 2 Marge | `:88` | `:105` | **aucun `MaxWidth`** (ItemsControl `MinWidth="190"`, `:84`) |
| 3 Jetons | `:128` | `:153` | `MaxWidth="50"` |
| 4 Sonar | `:171` | — | — |
| 5 Façade | `:225` | — | — |
| 6 Étagère | `:257` | — | — |
| 7 Annonciateur | `:328` | `:345` | `MaxWidth="130"` |
| 8 Veilleurs | `:363` | — | — |

Commentaire du style implicite de `ToolTip` (`:26-28`) à mettre à jour. `SessionsWindow` est en
`SizeToContent="WidthAndHeight"` sans `MaxWidth` (relevé de la phase 28) : **dans Pastilles et Marge, un titre
long élargit la fenêtre**, `TextTrimming` ne coupe rien sans contrainte de largeur.

Mesure (WPF `FormattedText`, Segoe UI SemiBold, textes réels des 18 sessions < 24 h) :

| Police | Titres : min / médiane / max | Dossiers : min / médiane / max | Titres plus larges que le plus large dossier |
|---|---|---|---|
| 12,5 (Pastilles, Marge) | 111 / 169 / **272 DIP** | 85 / 151 / **161 DIP** | **10 / 18** |
| 11 (Annonciateur) | 98 / 149 / 240 DIP | 75 / 133 / 142 DIP | 10 / 18 (déjà coupés à 130 aujourd'hui) |

Recommandation : `MaxWidth="160"` sur le `TextBlock` `Project` de Pastilles (`:67`) et de Marge (`:105`) — la
largeur du plus long dossier réel ; la largeur actuelle des gabarits est conservée pour tous les dossiers vus,
les titres au-delà sont coupés par l'ellipse. Seconde ligne de Pastilles (« En attente ? » 56 DIP +
«  ·  il y a 59 min » 72 DIP = 128 DIP) : sous la contrainte, rien ne bouge.

### 4.e L'info-bulle — un producteur (OBS-01) et le motif (APP-03)

```csharp
// Services/AffichageSessions.cs — couche NEUTRE
public static string Nom(SessionSnapshot s) => string.IsNullOrWhiteSpace(s.Titre) ? s.Project : s.Titre!;

/// « <titre> — <dossier> », ou le dossier seul ; puis, pour une attente, le motif lisible sur une 2e ligne.
public static string Infobulle(SessionSnapshot s)
{
    var entete = string.IsNullOrWhiteSpace(s.Titre) ? s.Project : $"{s.Titre} — {s.Project}";
    return MotifLisible(s) is { } m ? entete + "\n" + m : entete;
}

/// Le motif d'une ATTENTE, en mots. Les codes techniques connus sont traduits ; le texte de l'app
/// (needs_action) passe tel quel — c'est déjà une phrase écrite pour l'utilisateur.
public static string? MotifLisible(SessionSnapshot s)
    => !EstUneAttente(s.Activity) || string.IsNullOrWhiteSpace(s.Reason) ? null : s.Reason switch
    {
        "PermissionRequest" or "permission_prompt" => "permission demandée",
        "AskUserQuestion" => "question posée",
        _ => s.Reason,
    };
```
`EstUneAttente` est livré par la phase 28 (Pattern 1 de sa recherche). Si la phase 28 a ajouté le mot d'état à
l'info-bulle (sa Question ouverte 2), l'ordre des lignes est : nom — dossier, mot d'état, motif. Garde textuelle :
8 `ToolTip="{Binding Infobulle}"`, 0 `ToolTip="{Binding Project}"`.

---

## Q5 — Le diagnostic (APP-04) sans casser OBS-01

**Où.** `DiagnosticService.cs:522-567`, bloc `else { try { var lecture = _moniteurSessions.Inspecter(_clock.UtcNow); … } }`.
Insérer la section **après les désaccords** (`:558-565`) et avant le `catch` (`:566`), en lisant
`lecture.AppBureau` — la MÊME lecture que les lignes « AFFICHÉES » : pas de second appel à `Inspecter`, pas de
second lecteur. `LectureSessions` (`LectureSessions.cs:43-48`) gagne un dernier paramètre optionnel
`LectureAppBureau? AppBureau = null` ; une seule construction (`SessionMonitor.cs:137`).

Les lignes « AFFICHÉES » (`:531-533`) affichent `AffichageSessions.Nom(d)` au lieu de `d.Project` : c'est ce que
le widget montre (comparaison ligne à ligne OBS-01). Sans titre, `Nom = Project` : les assertions existantes
(`DiagnosticServiceTests.cs:555, 580`) ne bougent pas pour cette raison-là.

**Format proposé** (discrétion) :
```
  Source app-bureau : trouvée — C:\Users\…\AppData\Local\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude-code-sessions
    Fichiers : 138 énumérés · 18 modifiés depuis moins de 24 h · 18 lus (0 relus sur disque à ce cycle) · 0 illisible · 0 sans cliSessionId (ignoré)
    Champs absents (fichiers lus) : latestUserFrameAt 5 · aucun autre
    Jointures : 4 session(s) affichée(s) sur 5 ont des métadonnées
    · 11456cab « Overlay session status badges » — focus il y a 2 min — fin de tour : prête à revue
    · adac2711 « … » — focus il y a 34 h — fin de tour : question posée (« … »)
    · 939eb30a — aucune métadonnée (comportement v1.6)
```
Absente : `  Source app-bureau : absente (dossier introuvable) — cherché : <candidat 1> ; <candidat 2>`.
Non branchée (moniteur sans lecteur, oubli de câblage) : `  Source app-bureau : NON BRANCHÉE — comportement v1.6`.
Catégorie non reconnue : `fin de tour : inconnue (« need_input »)` — la catégorie brute est dite, jamais traduite
en état. Focus absent : `focus inconnu` (63/138 fichiers n'ont pas de `lastFocusedAt` ; 0/18 parmi les récents).

Nuance « relus sur disque » : le cache fait que l'appel du rapport relit rarement ; afficher les DEUX nombres
(métadonnées valides disponibles, relectures disque de ce cycle), sinon « 0 lu » passerait pour une panne.

**Gardes OBS-01 à ne pas casser, et à étendre :**
- `GardesPerimetreTests.Le_diagnostic_ne_fabrique_aucun_moniteur_de_sessions` (`:110-122`) exige le littéral
  `_moniteurSessions.Inspecter(_clock.UtcNow)` : le garder tel quel ; **ajouter** `DoesNotContain("new LecteurAppBureau")`
  et « `Inspecter(` apparaît une seule fois » dans `DiagnosticService.cs`.
- `Le_diagnostic_recoit_le_moniteur_du_conteneur` (`:134-149`) : inchangé.
- Nouvelle garde de câblage (même motif que `:134-149`) : le fragment `new SessionMonitor(` d'`App.xaml.cs`
  contient `appBureau: sp.GetRequiredService<LecteurAppBureau>()` — sans elle, un oubli laisserait la production
  en v1.6 **en silence** (le paramètre vaut `null` par défaut).

---

## Q6 — Lecture seule prouvée (APP-05)

Trois gardes, chacune falsifiée par mutation avant commit (précédent phases 26 et 28) :

1. **Textuelle, sur `Services/LecteurAppBureau.cs`** (motif `GardesPerimetreTests.CheminSources()`) :
   - non-vacuité : le fichier existe, contient `FileShare.ReadWrite | FileShare.Delete` et `FileMode.Open` ;
   - interdits : `File.Write`, `File.Append`, `File.Create`, `File.Delete`, `File.Move`, `File.Copy`,
     `File.Replace`, `File.SetAttributes`, `File.SetLastWriteTime`, `File.Open(`, `File.OpenWrite`,
     **`File.OpenRead`, `File.ReadAll`** (partage `Read` : gênerait l'écrivain, 1.c), `FileMode.Create`,
     `FileMode.CreateNew`, `FileMode.OpenOrCreate`, `FileMode.Truncate`, `FileMode.Append`, `FileAccess.Write`,
     `FileAccess.ReadWrite`, `Directory.Create`, `Directory.Delete`, `Directory.Move`, `.Delete(`, `.MoveTo(`,
     `.CopyTo(`, `.Create(`.
2. **Le chemin n'existe qu'à un endroit** : parmi tous les `.cs` de `src/Chronos` (récursif), seul
   `LecteurAppBureau.cs` contient `claude-code-sessions` ou `Claude_` ; aucun code d'écriture ne peut donc
   viser la racine sans passer par ce fichier.
3. **Comportementale** : racine temporaire peuplée des 6 fixtures + bruit (`archived-sessions.idx`,
   `.local_x.json.1234.ab12cd.tmp`, `backlog\tasks.json`, `deleted_<uuid>`), fichiers en `ReadOnly` ; empreinte
   (chemins, tailles, mtimes, SHA-256, attributs) avant/après trois cycles `Lire` ⇒ identique ; et un « écrivain
   en place » ouvert (`FileAccess.Write`, `FileShare.ReadWrite | Delete`, simulant le repli de l'app) n'empêche
   pas la lecture (un lecteur en `FileShare.Read` échouerait : test falsifiable).

Mutations à jouer : (a) ajouter `File.WriteAllText(Path.Combine(racine, "x.json"), "{}")` dans `Lire` ⇒ gardes 1
et 3 rouges ; (b) remplacer le partage par `FileShare.Read` ⇒ garde 1 et test de l'écrivain rouges ; (c) écrire
le littéral `claude-code-sessions` dans `DiagnosticService.cs` ⇒ garde 2 rouge.

---

## Standard Stack

**Aucune dépendance nouvelle.**

| Brique | Version | Usage dans cette phase |
|---|---|---|
| .NET 8 (`net8.0-windows`), SDK 10.0.201 | runtimes 8.0.25 | inchangé |
| `System.Text.Json` (`JsonDocument.Parse(ReadOnlyMemory<byte>)`) | intégré | analyse tolérante APRÈS fermeture de la poignée |
| `System.IO.FileStream` (`FileShare.ReadWrite \| FileShare.Delete`, `FileOptions.SequentialScan`) | intégré | copie des octets |
| `UsageNormalization.InstantDepuisEpochMillisecondes` | dépôt (`UsageNormalization.cs:98-107`) | **seul** point autorisé pour les epochs ms (garde HDR-05 ; plancher 2020 compatible avec 1 790 xxx xxx xxx) |
| `SessionHookProcessor.ProjectFromCwd` | dépôt | dossier d'un signal AppBureau (même règle que transcripts et hooks) |
| `HorizonsSessions.Abandon` | livré par la phase 28 | horizon des signaux `blocked` |
| CommunityToolkit.Mvvm | 8.4.2 | `[ObservableProperty] _infobulle` |
| xUnit 2.9.2, Xunit.StaFact 1.1.11, Microsoft.NET.Test.Sdk 17.11.1 | — | tests |

---

## Architecture Patterns

### Fichiers (tous à plat dans `Services/` : `NormalisationUniqueTests` ne balaie pas les sous-dossiers)
```
src/Chronos/Services/
├── LecteurAppBureau.cs      # NOUVEAU : ClassificationFinDeTour, MetadonneesAppBureau, LectureAppBureau, LecteurAppBureau
├── SessionMonitor.cs        # + paramètre appBureau, dépôt des questions, enrichissement Titre
├── ArbitrageSessions.cs     # + SourceSession.AppBureau (entre Hook et Transcript)
├── SessionSnapshot.cs       # + string? Titre = null
├── LectureSessions.cs       # + LectureAppBureau? AppBureau = null
├── AffichageSessions.cs     # + Nom, Infobulle, MotifLisible
└── DiagnosticService.cs     # + section « Source app-bureau », libellé de source
src/Chronos/ViewModels/      # SessionsViewModel (Project = Nom, Infobulle), SessionsPreviewViewModel
src/Chronos/Resources/SessionStyles.xaml   # 8 ToolTip → Infobulle ; MaxWidth Pastilles/Marge
src/Chronos/App.xaml.cs      # singleton LecteurAppBureau + argument nommé appBureau:
tests/Chronos.Tests/         # LecteurAppBureauTests (nouveau) + extensions
```

### Pattern 1 — Le lecteur : pur, tolérant, mis en cache, sans jamais gêner l'écrivain
```csharp
namespace Chronos.Services;

/// <summary>Classification de fin de tour faite PAR L'APP (postTurnSummary.status_category). Un indice
/// transitoire et daté, jamais un état : l'app l'efface au tour suivant.</summary>
public enum ClassificationFinDeTour { Inconnue, Terminee, Bloquee, PreteARevue }

public sealed record MetadonneesAppBureau(
    string CliSessionId, string? Titre, string? Dossier,
    System.DateTimeOffset? DernierFocus, System.DateTimeOffset? DerniereActivite,
    System.DateTimeOffset? DernierMessageUtilisateur, int? ToursTermines, bool Archivee,
    ClassificationFinDeTour ClassificationFinDeTour, string? CategorieBrute, string? MotifBlocage,
    string? ResumePour,              // postTurnSummaryFor : l'identité de l'épisode résumé
    bool ResumeAJour);               // postTurnSummaryFor == lastAssistantUuid (ou l'un absent)

public sealed record LectureAppBureau(
    string? Racine, IReadOnlyList<string> RacinesCherchees,
    int Enumeres, int Recents, int Valides, int RelusSurDisque, int Illisibles, int SansCliSessionId,
    IReadOnlyDictionary<string, int> ChampsAbsents,
    IReadOnlyDictionary<string, MetadonneesAppBureau> ParSession)   // clé : cliSessionId, OrdinalIgnoreCase
{
    public bool DossierTrouve => Racine is not null;
}

public sealed class LecteurAppBureau
{
    private readonly IReadOnlyList<string> _candidats;
    private readonly object _verrou = new();
    private readonly Dictionary<string, (System.DateTime Mtime, long Taille, MetadonneesAppBureau? Meta)> _cache
        = new(System.StringComparer.OrdinalIgnoreCase);
    // TRT-02 : l'instant d'un épisode « blocked », mémorisé à sa première apparition (Q3.c).
    private readonly Dictionary<(string Id, string Resume), System.DateTimeOffset> _premiereApparition = new();

    public LecteurAppBureau(IReadOnlyList<string>? racines = null)
        => _candidats = racines ?? CandidatsParDefaut(
               System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
               System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData));

    public LectureAppBureau Lire(System.DateTimeOffset now) { lock (_verrou) { /* résoudre, énumérer, lire */ } }

    // Énumération MATÉRIALISÉE dans le try (leçon de TranscriptSessionSource.cs:54-55) ; profondeur 2 exacte ;
    // filtre de nom ordinal en plus du motif : les temporaires de l'app commencent par « .local_ ».
    //   foreach org in racine.EnumerateDirectories()
    //     foreach usr in org.EnumerateDirectories()
    //       foreach fi in usr.EnumerateFiles("local_*.json")
    //         if (!fi.Name.StartsWith("local_", Ordinal) || !fi.Name.EndsWith(".json", OrdinalIgnoreCase)) continue;

    private static byte[] LireOctets(string chemin)
    {
        // ReadWrite : le repli EN PLACE de l'app (troncature + écriture) ne doit jamais échouer à cause de nous.
        // Delete : un renommage POSIX ou une suppression passent malgré notre poignée. Aucun partage n'empêche
        // en revanche notre poignée de faire échouer MoveFileExW : on la tient donc le temps de COPIER, pas
        // d'analyser (0,36 ms médian pour 289 Ko, mesuré).
        using var fs = new FileStream(chemin, FileMode.Open, FileAccess.Read,
                                      FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.SequentialScan);
        var tampon = new byte[fs.Length];
        var lu = 0;
        while (lu < tampon.Length) { var n = fs.Read(tampon, lu, tampon.Length - lu); if (n == 0) break; lu += n; }
        return lu == tampon.Length ? tampon : tampon[..lu];   // raccourci pendant une troncature : le JSON dira non
    }

    /// <summary>Interprétation PURE d'un fichier (testée sur les 6 fixtures réelles). Champs inconnus ignorés ;
    /// un champ de mauvais type vaut « absent » ; null si l'objet racine n'en est pas un.</summary>
    internal static MetadonneesAppBureau? Interpreter(System.ReadOnlyMemory<byte> json) { /* … */ return null; }
}
```
Lecture des champs : `TryGetProperty` + contrôle de `ValueKind` ; epochs par `TryGetInt64` puis
`UsageNormalization.InstantDepuisEpochMillisecondes` ; `title` rogné, vide ⇒ absent ; `status_category`
comparé ordinalement (`"completed"`, `"blocked"`, `"review_ready"`, autre ⇒ `Inconnue` + `CategorieBrute`).
Doublon de `cliSessionId` (0 aujourd'hui) : garder le plus récent par `lastActivityAt`, compter au diagnostic.

### Pattern 2 — Le moniteur : déposer les questions, puis enrichir
```csharp
// SessionMonitor.cs — après la collecte des hooks (:111), avant l'arbitrage (:113)
LectureAppBureau? appBureau = null;
try { appBureau = _appBureau?.Lire(now); } catch { appBureau = null; }   // best-effort : jamais fatal
if (appBureau is { DossierTrouve: true })
{
    // L'app bureau QUALIFIE une ligne, elle n'en crée pas : seuls les identifiants déjà déposés par une
    // source d'activité peuvent recevoir une question (périmètre SRC-01, dégradation v1.6 exacte).
    var connues = new HashSet<string>(signaux.Select(s => s.Session.SessionId), System.StringComparer.Ordinal);
    foreach (var m in appBureau.ParSession.Values)
        if (connues.Contains(m.CliSessionId) && QuestionPosee(m, now) is { } q)
            signaux.Add(new SignalSession(SourceSession.AppBureau, q));
}
var arbitrage = ArbitrageSessions.Trancher(signaux);

// …le détecteur observe arbitrage.Vainqueurs (non enrichis) comme aujourd'hui…

// Le titre n'est pas un signal : posé APRÈS l'arbitrage, il ne départage rien (APP-02).
var raw = arbitrage.Retenus
    .Select(s => appBureau?.ParSession.TryGetValue(s.SessionId, out var m) == true
                 && !string.IsNullOrWhiteSpace(m!.Titre) ? s with { Titre = m.Titre } : s)
    .ToList();
// … filtres inchangés … return new LectureSessions(visibles, masquees, ecartes, arbitrage.Desaccords, appBureau);

// QuestionPosee : Bloquee && MotifBlocage non vide && !Archivee && ResumeAJour && instant d'épisode < Abandon
//   ⇒ new SessionSnapshot(m.CliSessionId, SessionHookProcessor.ProjectFromCwd(m.Dossier),
//                         SessionActivity.WaitingAttention, m.MotifBlocage, instantEpisode)
```

### Pattern 3 — Le câblage DI (`App.xaml.cs:244-256`)
```csharp
// Métadonnées par session de l'app bureau (APP-01) : LECTURE SEULE, singleton (son cache vit avec l'app).
services.AddSingleton(_ => new LecteurAppBureau());
services.AddSingleton(sp => new SessionMonitor(null, null, sp.GetRequiredService<ArchiveStore>(),
    sp.GetRequiredService<TreatedStore>(),
    sp.GetRequiredService<SessionTreatmentTracker>(),
    appBureau: sp.GetRequiredService<LecteurAppBureau>()));
```
Miroir dans `CompositionRootTests.Le_graphe_DI_resout_la_chaine_de_sessions` (`:147-190`) avec
`new LecteurAppBureau(new[] { <racine temporaire> })` et `Assert.Same` sur le lecteur (un seul cache, partagé
par le widget et le rapport). `BalayageMagasinSessions` : inchangé.

### Pattern 4 — Le ViewModel ne fait que poser ce que le producteur rend
```csharp
// SessionsViewModel.Refresh (:189-207)
Project = AffichageSessions.Nom(s),
Infobulle = AffichageSessions.Infobulle(s),
```

### Anti-patterns à éviter
- **Coder `%APPDATA%\Claude` comme racine unique** : vert en test, vert depuis Claude Code, « absente » dans
  l'overlay (Q0).
- **`JsonDocument.Parse(stream)` sur la poignée ouverte**, ou `File.ReadAll*` : poignée tenue pendant l'analyse,
  ou partage `Read` qui fait échouer le repli en place de l'app.
- **Vider les métadonnées d'un fichier au premier JSON invalide** : clignotement du titre (critère 3).
- **Enrichir avant l'arbitrage** : le titre deviendrait un critère de départage caché.
- **Un second appel à `Inspecter` ou un second lecteur dans le diagnostic** : rapport ≠ écran (OBS-01).
- **Déposer un signal pour `need_input`/`failed`** « parce que l'app les traite comme des attentes » : jamais
  observés sur disque, hors verrou.
- **Dater l'épisode par la valeur COURANTE de `lastActivityAt`** sans figer : épisode qui se rajeunit (Q3.c).
- **Créer une ligne à partir de la seule app bureau** : population et `MaxSessions` contournés, `blocked` de
  36 h ressuscités.

---

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser | Pourquoi |
|---|---|---|---|
| Epoch ms → instant | `DateTimeOffset.FromUnixTimeMilliseconds` | `UsageNormalization.InstantDepuisEpochMillisecondes` | garde HDR-05 (`NormalisationUniqueTests:32`) ; `LecteurAppBureau.cs` n'est pas exempté et ne doit pas l'être |
| Dossier depuis `cwd` | `Path.GetFileName(cwd)` local | `SessionHookProcessor.ProjectFromCwd` | même libellé que transcripts et hooks ⇒ l'info-bulle « titre — dossier » concorde |
| Horizon des signaux | un `TimeSpan.FromHours(8)` local | `HorizonsSessions.Abandon` (phase 28) | la garde de chaîne des horizons de la phase 28 |
| Libellés, nom affiché, info-bulle | `switch` dans le VM ou le diagnostic | `AffichageSessions` | un producteur, deux consommateurs (OBS-01) |
| Permutations | nouvelle routine | `Permutations<T>` / `Canonique` (`ArbitrageSessionsTests.cs:43-61`) | éprouvés |
| Lire les sources dans une garde | `Assembly.Location` | `GardesPerimetreTests.CheminSources()` | vide en mono-fichier |
| Racine temporaire + fixtures | fixtures régénérées par sérialisation | copie des 6 fichiers réels (`[CallerFilePath]` + `TestData/DesktopAppSessions`) | précédent DEL-06 ; une troncature se dérive d'une fixture réelle (moitié des octets) |
| Détection de changement | hachage du contenu à chaque cycle | (mtime, taille) | 0,7 ms contre 24 ms ; lire pour hacher annulerait le cache |

---

## Common Pitfalls

### Piège 1 — La racine vue depuis Claude Code n'est pas celle de l'overlay
**Ce qui casse :** section « absente » en production alors que tout est vert en développement.
**Pourquoi :** virtualisation MSIX de l'arbre de l'app (Q0).
**Parade :** candidats, paquet d'abord ; test de `CandidatsParDefaut` ; vérification finale hors de l'arbre.
**Signe :** le diagnostic de l'overlay publié dit « absente » et liste `%APPDATA%\Claude\…` seul.

### Piège 2 — Un lecteur « poli » qui gêne l'écrivain
`File.ReadAllText` (partage `Read`) ferait échouer le repli en place de l'app ; une poignée tenue pendant
l'analyse allonge la fenêtre où `MoveFileExW` échoue. Parade : Q1, Pattern 1, garde Q6.

### Piège 3 — Les temporaires et les autres fichiers du dossier utilisateur
Dans `<org>\<user>` vivent aussi `archived-sessions.idx`, `deleted_<uuid>`, `scheduled-tasks.json`,
`backlog\tasks.json` (profondeur 3) et, pendant une écriture, `.local_<id>.json.<pid>.<rand>.tmp`. Motif
`local_*.json` + filtre de nom ordinal + profondeur exacte 2 ; jamais `*.json` récursif (ce qu'a fait la sonde
« vue » : 140 au lieu de 138).

### Piège 4 — La taille ne change pas à la réécriture
276 623 → 276 623 octets. Une clé de cache sur la taille seule ne verrait jamais le changement : la clé est
(mtime, taille), le mtime porte l'information.

### Piège 5 — Un fichier < 24 h ne dit rien de l'âge de la session
Au redémarrage de l'app (16:58), tous les fichiers sont réécrits : parmi les 18 récents, `lastActivityAt` date
de 0 s à 36 jours avant le mtime. Le filtre de 24 h est une économie de lecture ; l'âge d'un signal se juge sur
son instant (horizon 8 h), et la population sur les autres sources (3.b).

### Piège 6 — L'épisode qui se rajeunit
`lastActivityAt` avance après la fin du tour (+3 min 10 s mesurés) : NET-03 et la future règle « lue » en
souffriraient. Parade : Q3.c. Signe : une session « marquée traitée » revient sans nouvelle question.

### Piège 7 — Les catégories que l'app connaît et que le disque n'a jamais montrées
`need_input` et `failed` (code de l'app) ; `turnWrapUp` peut réécrire `status_category` (`aF(e)`). Tout ce qui
n'est pas `completed`/`blocked`/`review_ready` ⇒ `Inconnue`, catégorie brute au diagnostic, aucun signal.

### Piège 8 — Deux « En attente » en désaccord
Après la phase 28, C1 produit un désaccord « retenu app bureau « En attente » ; écarté transcript « En
attente » » — deux mots identiques pour deux activités différentes (`WaitingAttention` / `WaitingTurn`). Le
désaccord est réel (question contre tour fini) ; le rendre lisible (ajouter le motif ou le nom d'activité à la
ligne) ou l'accepter explicitement — à décider, pas à découvrir.

### Piège 9 — Le paramètre `null` par défaut rend l'oubli silencieux
`SessionMonitor(… appBureau = null)` ⇒ v1.6 exact : excellent pour les ~dizaines de tests existants (aucun ne
lit la vraie machine, précédent Piège 15 de la phase 28), dangereux pour la production. Garde de câblage (Q5) +
ligne « NON BRANCHÉE » au diagnostic.

### Piège 10 — Les dates des fixtures en heure locale
« dernier focus 20:30:44 » est une heure de Paris (UTC+2) : asserter `2026-09-25T18:30:44.976Z`, jamais une
chaîne locale ; injecter `now` (fixtures du 2026-09-24/25) plutôt que l'horloge.

### Piège 11 — Titre long, fenêtre qui s'élargit
Sans `MaxWidth`, `TextTrimming` ne coupe rien dans une fenêtre `SizeToContent`. Parade : 4.d ; test WPF de
largeur sur 9 thèmes.

### Piège 12 — Les gardes de périmètre et le vocabulaire « app bureau »
`Aucun_type_de_la_source_app_bureau_ne_subsiste_dans_l_assembly` vise des NOMS (`Uia`, `ForegroundWatch`,
`DesktopHealth`, `SessionKind`, `SessionOrigin`). `LecteurAppBureau`, `MetadonneesAppBureau`,
`LectureAppBureau`, `ClassificationFinDeTour` passent ; ne pas réintroduire d'identifiant synthétique
`desktop:` (`ArchiveStore.PurgerPrefixe("desktop:")` au démarrage) — la jointure se fait sur l'UUID.

---

## Code Examples

### Interprétation des fixtures réelles (pur)
```csharp
private static ReadOnlyMemory<byte> Fixture(string nom, [CallerFilePath] string ici = "")
    => File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(ici)!, "TestData", "DesktopAppSessions", nom));

[Fact]
public void La_session_du_geste_B_donne_son_titre_et_son_dernier_focus()
{
    var m = LecteurAppBureau.Interpreter(Fixture("session-courante-geste-b.json"))!;
    Assert.Equal("11456cab-d447-42c7-aa85-9920ce64f7ba", m.CliSessionId);
    Assert.Equal("Session A", m.Titre);
    Assert.Equal(new DateTimeOffset(2026, 9, 25, 18, 30, 44, 976, TimeSpan.Zero), m.DernierFocus);
    Assert.Equal(ClassificationFinDeTour.Inconnue, m.ClassificationFinDeTour);   // pas de postTurnSummary
}

[Fact]
public void Blocked_donne_la_question_et_son_motif()
{
    var m = LecteurAppBureau.Interpreter(Fixture("fin-de-tour-blocked.json"))!;
    Assert.Equal(ClassificationFinDeTour.Bloquee, m.ClassificationFinDeTour);
    Assert.Equal("(anonymisé) réponse attendue", m.MotifBlocage);
    Assert.Equal(new DateTimeOffset(2026, 9, 24, 9, 0, 6, 552, TimeSpan.Zero), m.DerniereActivite);
    Assert.True(m.ResumeAJour);   // postTurnSummaryFor == lastAssistantUuid
}

[Fact] public void Sans_cliSessionId_rien_n_est_joignable() { /* Interpreter ⇒ null ou CliSessionId vide ; Lire ⇒ SansCliSessionId == 1 */ }
[Fact] public void Le_bloc_fixture_et_les_champs_inconnus_sont_ignores() { /* _fixture présent dans les 6 : aucune exception */ }
```

### Le cas C1, contre les classes réelles (moniteur + lecteur sur racine temporaire)
```csharp
[Fact]
public void Une_question_de_l_app_bat_le_tour_fini_du_transcript_et_porte_son_motif()
{
    var fin = new DateTimeOffset(2026, 9, 24, 9, 0, 4, 991, TimeSpan.Zero);          // end_turn réel du transcript
    var racine = RacineAvec("fin-de-tour-blocked.json");                               // <tmp>\org\user\local_x.json
    var transcript = new SourceFixe(new SessionSnapshot("adac2711-86a4-4b4d-b71c-9a594c9d959e", "Projet-E",
                                                        SessionActivity.WaitingTurn, null, fin));
    var moniteur = new SessionMonitor(TempDir(), transcript, new ArchiveStore(TempFichier()),
                                      appBureau: new LecteurAppBureau(new[] { racine }));

    var s = Assert.Single(moniteur.Inspecter(fin.AddMinutes(5)).Visibles);
    Assert.Equal(SessionActivity.WaitingAttention, s.Activity);
    Assert.Equal("(anonymisé) réponse attendue", s.Reason);
    Assert.Equal("Session E", s.Titre);
}

[Fact] public void Sans_autre_source_la_question_de_l_app_ne_cree_aucune_ligne() { /* C7 : Visibles vide */ }
[Fact] public void Une_question_de_plus_de_huit_heures_ne_depose_rien() { /* C8 : now = fin + 8 h 01 */ }
[Fact] public void Completed_et_review_ready_ne_changent_aucun_etat() { /* C9 : WaitingTurn reste WaitingTurn */ }
```

### Dégradation v1.6 exacte (critère 3)
```csharp
[Fact]
public void Dossier_absent_rend_exactement_la_lecture_v1_6_et_le_dit()
{
    var source = new SourceFixe(/* trois sessions de InspectionSessionsTests.Trois() */);
    var v16 = new SessionMonitor(TempDir(), source, new ArchiveStore(TempFichier())).Inspecter(Now);
    var absent = new SessionMonitor(TempDir(), source, new ArchiveStore(TempFichier()),
                                    appBureau: new LecteurAppBureau(new[] { Path.Combine(TempDir(), "rien") })).Inspecter(Now);

    Assert.Equal(Sequence(v16.Visibles), Sequence(absent.Visibles));   // mêmes ids, mêmes états, mêmes instants
    Assert.False(absent.AppBureau!.DossierTrouve);
}

[Fact]
public void Un_fichier_tronque_pendant_sa_reecriture_ne_fait_pas_clignoter_le_titre()
{
    // 1er cycle : fichier complet ⇒ « Session A ». 2e cycle : la MOITIÉ des octets (repli en place de l'app),
    // mtime avancé ⇒ le titre reste « Session A », Illisibles == 1, aucune exception.
    // 3e cycle : fichier complet réécrit ⇒ relu (RelusSurDisque == 1).
}
```

---

## Ce qui change par rapport à v1.6

| v1.6 | Phase 29 | Conséquence |
|---|---|---|
| 2 sources (Hook, Transcript) | 3 : Hook, **AppBureau**, Transcript (rang à âge égal) | un corpus de permutation neuf ; un libellé de source neuf |
| Nom de dossier affiché | Titre de l'app s'il existe, dossier en repli | `MaxWidth` dans Pastilles et Marge |
| Info-bulle = dossier | « titre — dossier » + motif d'une attente | `Infobulle` dans le VM, la galerie et les 8 gabarits |
| Une question en prose (fin de tour) = « tour fini » | = « En attente » au rang des questions, motif lisible | seulement si l'app l'a classée `blocked` |
| Rien sur l'app bureau au diagnostic | Section « Source app-bureau » (trouvée / absente / non branchée) | OBS-01 étendu |

---

## Open Questions

1. **Datation de l'épisode `blocked`** (Q3.c).
   - Ce qu'on sait : `lastActivityAt` = fin de tour + 1,4 à 3,5 s au repos, mais +3 min 10 s mesurés sous
     activité d'arrière-plan ; le résumé, lui, ne bouge pas.
   - Recommandation : `lastActivityAt` lu à la première apparition du résumé (clé `postTurnSummaryFor`),
     consigné comme ajustement écrit du verrou « daté par `lastActivityAt` ».
2. **« L'app bureau qualifie, elle ne crée pas »** (3.b).
   - Recommandation : garde de dépôt sur les identifiants déjà connus du cycle ; à écrire dans le plan.
3. **Racine et mode de partage** — amendements de faits aux décisions verrouillées (Q0, Q1). À consigner.
4. **Fichiers de hook et pont statusLine virtualisés** (Q0.c) — HORS périmètre APP-01..05, mais ils
   conditionnent la valeur du milestone (et le raisonnement de la phase 28 sur les hooks). Décision utilisateur :
   élargir la phase 29 (le moniteur lit l'union des deux dossiers d'état), insérer une phase, ou différer.
5. **Désaccord « En attente » contre « En attente »** (Piège 8) — rendre la ligne lisible ou l'accepter.
6. **`isArchived` de l'app** : ce travail n'en fait qu'une condition de dépôt et une donnée de diagnostic ;
   masquer les sessions archivées dans l'app serait un nouveau motif de `MotifMasquage` — pas demandé ici.

---

## Environment Availability

| Dépendance | Requise par | Disponible | Version | Repli |
|---|---|---|---|---|
| .NET SDK | build + tests | ✓ | 10.0.201 | — |
| Runtime .NET 8 (NETCore + WindowsDesktop) | tests `net8.0-windows`, `[WpfFact]` | ✓ | 8.0.25 (et 8.0.0, 8.0.21) | — |
| Suite xUnit | validation | ✓ | **896 verts**, 8 s de tests, 13,7 s de mur (exécutée le 2026-09-25) | — |
| App bureau Claude (MSIX) | données réelles, fixtures | ✓ | `Claude_2.9939.2.0_x64__pzs8sxrjxfjjc` | fixtures de la phase 27 |
| Racine physique `…\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude-code-sessions` | production | ✓ (dedans ET dehors) | 138 `local_*.json`, 1 org, 1 user | — |
| `%APPDATA%\Claude\claude-code-sessions` | — | **✗ pour l'overlay** (✓ seulement dans l'arbre de l'app) | — | racine physique |
| Python 3.14 | mesures de cette recherche | ✓ | 3.14.4 (via alias MSIX : tourne HORS de l'arbre, voit la vue réelle) | non requis par le projet |
| Overlay vivant | — | à ne pas lancer | `Chronos-v3.1.0.exe` PID 40772 | galerie `--sessions` + test WPF 8 × 9 |

Aucune dépendance manquante pour exécuter la phase. **La vérification de la racine en production exige un
processus hors de l'arbre de l'app** (sonde WMI jetable, ou l'overlay publié en phase 31).

---

## Validation Architecture

### Test Framework
| Propriété | Valeur |
|---|---|
| Framework | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`, `[Collection("XAML WPF")]`), Microsoft.NET.Test.Sdk 17.11.1 |
| Config | `tests/Chronos.Tests/Chronos.Tests.csproj` (attributs `CheminSourcesChronos`, `CheminDocsChronos`) |
| Commande rapide | voir ci-dessous (`--no-build` après une compilation) |
| Suite complète | `dotnet test Chronos.sln --nologo` (896 aujourd'hui, ~8 s de tests, ~14 s de mur) |

```bash
dotnet test Chronos.sln --nologo --no-build --filter "FullyQualifiedName~LecteurAppBureauTests|FullyQualifiedName~ArbitrageSessionsTests|FullyQualifiedName~InspectionSessionsTests|FullyQualifiedName~AffichageSessionsTests|FullyQualifiedName~DiagnosticServiceTests|FullyQualifiedName~GardesPerimetreTests|FullyQualifiedName~CompositionRootTests|FullyQualifiedName~NormalisationUniqueTests|FullyQualifiedName~ServicesLayerPurityTests|FullyQualifiedName~SessionStylesBindingTests"
```

### Phase Requirements → Test Map
| Req | Comportement | Type | Commande (`--filter`) | Existe ? |
|---|---|---|---|---|
| APP-01 | 6 fixtures réelles : champs, instants UTC, catégories, `_fixture` ignoré, sans `cliSessionId` ⇒ ignoré et compté | unit pur | `~LecteurAppBureauTests` | ❌ Wave 0 |
| APP-01 | profondeur 2 exacte, bruit ignoré (`.tmp`, `.idx`, `backlog\tasks.json`, `deleted_*`) | intégration (racine temporaire) | `~LecteurAppBureauTests` | ❌ |
| APP-01 | mtime > 24 h ⇒ non ouvert ; cache (mtime, taille) ⇒ 0 relecture ; mtime changé ⇒ 1 relecture | intégration | `~LecteurAppBureauTests` | ❌ |
| APP-01 | tronqué (moitié des octets) ⇒ dernière lecture valide gardée, aucun clignotement, `Illisibles` + 1 | intégration | `~LecteurAppBureauTests` | ❌ |
| APP-01 | racine absente ⇒ `Inspecter` identique à un moniteur sans lecteur ; `DossierTrouve == false` | intégration moniteur | `~InspectionSessionsTests` | ❌ |
| APP-01 | `CandidatsParDefaut` : paquet `Claude_*` d'abord, `%APPDATA%` ensuite | unit (arborescence temporaire) | `~LecteurAppBureauTests` | ❌ |
| APP-01 | un écrivain en place ouvert ne bloque pas la lecture | intégration | `~LecteurAppBureauTests` | ❌ |
| APP-02 | jointure : geste B ⇒ `Titre == "Session A"`, `Project` inchangé ; sans métadonnées ⇒ `Titre == null` | intégration moniteur | `~InspectionSessionsTests` | ❌ |
| APP-02 | `Nom`, `Infobulle` (« titre — dossier », dossier seul, motif en 2e ligne) | unit | `~AffichageSessionsTests` | ❌ |
| APP-02 | VM : `Project` = titre, `Infobulle` posée ; galerie expose `Infobulle` + échantillon long | unit / réflexion | `~AffichageSessionsTests` | ❌ |
| APP-02 | 8 × 9 : titre de 43 caractères ⇒ largeur ≤ celle du dossier « PROJET ADVANCED SHEET » (+1 DIP) | WPF | `~SessionStylesBindingTests` | à étendre (`:115-136`) |
| APP-02 | 8 `ToolTip="{Binding Infobulle}"`, 0 `{Binding Project}` en info-bulle ; `MaxWidth` sur Pastilles/Marge | garde texte | `~GardesPerimetreTests` | ❌ |
| APP-03 | C1-C11 (tableau 3.d) contre les classes réelles | unit pur + intégration | `~ArbitrageSessionsTests`, `~InspectionSessionsTests` | ❌ |
| APP-03 | ordre `[Hook, AppBureau, Transcript]` ; 720 permutations ⇒ 1 résultat, non-vacuité | unit pur | `~ArbitrageSessionsTests` | ❌ |
| APP-03 | datation figée par épisode (même `postTurnSummaryFor`, `lastActivityAt` avancé ⇒ même instant) | intégration | `~LecteurAppBureauTests` | ❌ |
| APP-03 | `Etat(WaitingAttention)` = « En attente » et rang question (`Urgence` 0) pour le signal AppBureau | unit | `~AffichageSessionsTests` | ✅ (phase 28) |
| APP-04 | rapport : trouvée (racine, compteurs, champs absents, jointures, ligne par session), absente (candidats), non branchée | intégration rapport | `~DiagnosticServiceTests` | ❌ |
| APP-04 | chaque `SourceSession` a un libellé nommé | unit | `~DiagnosticServiceTests` | ❌ |
| APP-04 | OBS-01 : pas de `new LecteurAppBureau` dans le diagnostic, un seul `Inspecter(` ; câblage `appBureau:` dans `App.xaml.cs` | garde texte | `~GardesPerimetreTests` | à étendre (`:110-149`) |
| APP-05 | garde textuelle (interdits + partage exigé) ; chemin présent dans un seul fichier | garde texte | `~LecteurAppBureauTests` ou `~GardesPerimetreTests` | ❌ |
| APP-05 | empreinte identique après 3 cycles, fichiers en lecture seule | intégration | `~LecteurAppBureauTests` | ❌ |
| APP-05 | gardes existantes intactes | réflexion / texte | `~GardesPerimetreTests`, `~ServicesLayerPurityTests`, `~CompositionRootTests`, `~NormalisationUniqueTests` | ✅ (miroir DI à étendre) |

### Sampling Rate
- **Par tâche :** commande rapide ci-dessus.
- **Par vague :** `dotnet test Chronos.sln --nologo`.
- **Porte de phase :** suite complète verte (0 échec) avant `/gsd:verify-work`, et les trois mutations de Q6
  jouées puis annulées (a : écriture ajoutée ⇒ rouge ; b : `FileShare.Read` ⇒ rouge ; c : littéral du chemin
  ailleurs ⇒ rouge), plus une quatrième pour l'arbitrage : déclarer `AppBureau` après `Transcript` ⇒ le test de
  rang rougit.

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/LecteurAppBureauTests.cs` — interprétation (6 fixtures), énumération, cache, troncature,
      candidats, écrivain en place, lecture seule (empreinte), garde textuelle
- [ ] helper de racine temporaire `<tmp>\org\user\local_*.json` à partir des fixtures réelles + bruit
- [ ] extension `ArbitrageSessionsTests` (helper `AppBureau`, corpus C1/C4/C5, ordre de l'énumération)
- [ ] extension `InspectionSessionsTests` (jointure, C1, C7-C9, dégradation absente)
- [ ] extension `DiagnosticServiceTests` (section trouvée / absente / non branchée, libellé de source)
- [ ] extension `GardesPerimetreTests` (OBS-01 étendu, câblage `appBureau:`, info-bulles, `MaxWidth`)
- [ ] extension `CompositionRootTests.Le_graphe_DI_resout_la_chaine_de_sessions` (lecteur à racine temporaire)
- [ ] extension `SessionStylesBindingTests` (titre long, 8 × 9)
- Aucun framework à installer ; les 6 fixtures existent (`TestData/DesktopAppSessions`).

### Vérifications manuelles (non automatisables sans lancer l'overlay)
- **Galerie `--sessions`** (contrôle humain) : les 8 tuiles avec l'échantillon à titre long — ellipse propre dans
  Pastilles, Marge, Jetons, Annonciateur ; largeur inchangée ; info-bulle « titre — dossier » et motif sur 9
  thèmes.
- **Racine vue par l'overlay** : à constater HORS de l'arbre de l'app — soit une sonde jetable lancée par
  `Win32_Process.Create` (motif de cette recherche, lecture seule), soit le menu « Diagnostic… » de l'overlay
  publié (phase 31) : « Source app-bureau : trouvée — …\Packages\Claude_pzs8sxrjxfjjc\… ». Un constat fait
  depuis Claude Code ne prouve rien (Q0).

---

## Sources

### Primary (HIGH)
- Dépôt, lu le 2026-09-25 : `SessionMonitor.cs`, `ArbitrageSessions.cs`, `TranscriptSessionSource.cs`,
  `ISessionSource.cs`, `SessionSnapshot.cs`, `LectureSessions.cs`, `AffichageSessions.cs`,
  `DiagnosticService.cs`, `UsageNormalization.cs`, `SessionTreatmentTracker.cs`, `SessionHookProcessor.cs`,
  `App.xaml.cs`, `SessionsViewModel.cs`, `SessionsPreviewViewModel.cs`, `SessionStyles.xaml`, et les tests
  `CompositionRootTests`, `GardesPerimetreTests`, `ServicesLayerPurityTests`, `NormalisationUniqueTests`,
  `InspectionSessionsTests`, `ArbitrageSessionsTests`, `SessionStylesBindingTests`, `DiagnosticServiceTests`.
- Mesures sur la machine (bloc-notes de session, lecture seule) : sonde .NET 8 « vue » dedans / dehors (WMI),
  banc de coût (4 exécutions dont 1 dehors), guet de 150 s (57 816 lectures), preuve des modes de partage
  (dossier temporaire), tenue de poignée, largeurs WPF des 18 titres et dossiers réels, analyse des 138 fichiers,
  jointure des 12 résumés avec leurs transcripts, `chronos.log` et `usage.json` réels (lus via un processus hors
  de l'arbre).
- `app.asar` de `Claude_2.9939.2.0` (lecture seule) : `writeFileAtomic` (temporaire, 3 essais, repli en place),
  codes réessayés, `dropPostTurnSummary` / `saveSessionNow`, `TT`, `aF`, `En`, `NT`, constantes `wW`/`TW`.
- [libuv `src/win/fs.c` (v1.x)](https://raw.githubusercontent.com/libuv/libuv/v1.x/src/win/fs.c) — `fs__rename` = `MoveFileExW(..., MOVEFILE_REPLACE_EXISTING)`.
- [Microsoft Learn — Understanding how packaged desktop apps run on Windows (MSIX)](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes) — redirection des créations sous `AppData` vers un emplacement privé par paquet, ordre d'ouverture.
- Suite exécutée : `dotnet test Chronos.sln --nologo` ⇒ 896 / 896, 8 s.

### Secondary (MEDIUM)
- [rust-lang/rust PR #131072](https://github.com/rust-lang/rust/pull/131072) — `FILE_RENAME_FLAG_POSIX_SEMANTICS` autorise le renommage sur une cible ouverte avec `FILE_SHARE_DELETE` (confirmé par la preuve locale 1.c).
- Comportement interne de l'app (effacement du résumé, catégories `need_input`/`failed`) : code minifié d'une
  version ; à re-constater si l'app change de version majeure.

### Tertiary (LOW)
- Aucune affirmation de ce document ne repose sur une source LOW seule. Le MÉCANISME qui applique la
  virtualisation à l'arbre de l'app malgré l'absence d'identité de paquet n'est pas établi (les effets le sont).

---

## Metadata

**Confidence breakdown :**
- Racine réelle et virtualisation : HIGH sur les faits (4 preuves indépendantes, dont le journal de l'overlay) ;
  MEDIUM sur le mécanisme.
- Mécanisme d'écriture de l'app et modes de partage : HIGH (code lu + guet + preuve locale), MEDIUM sur la
  pérennité (version du jour).
- Coût et cache : HIGH — mesuré dedans et dehors.
- Arbitrage et datation : HIGH sur les données (12 résumés joints) ; MEDIUM sur l'effacement du résumé (code).
- Titre, gabarits, largeurs : HIGH — lus et mesurés ; rendu final à confirmer dans la galerie.
- Diagnostic et gardes : HIGH — lus ligne à ligne.

**Research date :** 2026-09-25
**Valid until :** 2026-10-09 (l'app bureau change souvent de version ; refaire Q0, Q1.a et Q3.c si la version
n'est plus 2.9939.2.0)
