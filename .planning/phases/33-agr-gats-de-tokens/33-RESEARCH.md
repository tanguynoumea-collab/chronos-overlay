# Phase 33 : Agrégats de tokens — Research

**Researched:** 2026-09-27 (11:00 → 11:50, heure locale ; HEAD `3fb1cad`, 1313 verts à l'entrée)
**Domain:** lecture en flux de transcripts JSONL (.NET 8, BCL seule), dédup par `message.id` sous curseurs, agrégation par tranche UTC, persistance JSONL atomique, service de fond dans le Generic Host d'une app WPF, rendu local aux changements d'heure
**Confidence:** HIGH sur tout ce qui est mesuré (transcripts réels, prototype C# sur la vraie machine, code réel lu) ; MEDIUM sur les choix de forme laissés à discrétion

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### Verrouillées (utilisateur + conseil + plan de design §3)
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
  29/03/2027 ; `BornesPlage` (phase 32) donne les bornes de plage.
- **Couverture** : tranches antérieures au plus vieux transcript = « hors couverture » ; mois purgé par Claude Code = « transcripts
  absents » ; jamais « zéro token ». La lecture par plage des agrégats expose cette couverture comme JRN-05 le fait pour les relevés.
- **Observabilité** : l'agrégateur implémente `IEtatMagasin` (troisième magasin, `NomsMagasins`), la progression N / M et l'état de
  reconstruction sont exposés au ViewModel (bandeau F2 en phase 34) et au diagnostic ; jamais d'appel réseau ; jamais d'écriture
  sous `~/.claude` (lecture seule stricte des transcripts).

### Claude's Discretion
Nommage, découpage des fichiers sous `Services/Historique/` et `Models/Historique/`, structure de l'index de dédup, stratégie de
throttling (budget CPU par tranche), format exact de la progression, forme des fixtures (extraits RÉELS anonymisés multi-blocs,
sous-agents, fichier tronqué, fichier réduit, timestamps futurs) — dans les conventions du dépôt.

### Deferred Ideas (OUT OF SCOPE)
- Dimension projet (dossier) dans les agrégats → v1.9 (TOK-06) ; compaction ; export CSV ; heatmap.
- Toute UI (bandeau F2, pistes) → phase 34.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TOK-01 | Agrégats par tranche 15 min UTC × modèle × principal/sous-agent dans `historique\tokens-AAAA-MM.jsonl`, `{v, slot, model, sub, in, out, cache_w, cache_r, n}`, quatre compteurs séparés, jamais la somme, jamais le message, jamais le contenu | §Mesures (3 886 lignes / 524 Ko pour TOUT l'historique, 135 o/ligne, mois de septembre 304 Ko), §Pattern 2 (état = index d'ids source + agrégats projetés), §Pattern 4 (réécriture atomique du mois, dialecte `LigneJournal`), §Pattern 7 (raccord à `DedupUsage`) |
| TOK-02 | Reconstruction initiale en arrière-plan (thread `IsBackground` `BelowNormal`, mtime décroissant, flux `FileShare.ReadWrite`, pré-filtre texte, dédup par fichier via CPT-01, `Task.Yield`, annulable), progression N / M au ViewModel, UI jamais bloquée, semaine courante d'abord | §Mesures prototype (passe complète **2,4–2,6 s mur / 2,7 s CPU** à chaud, semaine courante à **1,7 s**, 766–800 Mo/s, pic +90 Mo, tas 31 Mo), §Pattern 3 (thread dédié + `BackgroundService`, **pas `await Task.Yield()` sur un thread dédié** : Pitfall 1), §Pattern 1 (lecteur de lignes au niveau octet), §Progression |
| TOK-03 | Incrémental par curseurs `{chemin → offset de la dernière ligne complète, taille, mtime}` ; inchangé = pas ouvert ; grandi = relu depuis l'offset ; raccourci ou renommé = réingéré de zéro et tranches RÉÉCRITES ; reprise idempotente | §Mesures (1 377 ids multi-fichiers, copies indiscernables structurellement, âge max 3,6 j, sur-comptage 2,2 % si dédup par fichier ; contrôle (taille, mtime) de 1 603 fichiers en 63 ms), §Pattern 2 (index d'ids = mémoire d'idempotence, agrégats = projection), §Pattern 1 (offsets), §Curseurs |
| TOK-04 | Rendu local juste aux changements d'heure (25 h le 25/10/2026, 23 h le **28**/03/2027 — voir Pitfall 3), « hors couverture » avant le plus vieux transcript, « transcripts absents » pour un mois purgé, jamais « zéro token » | §Pattern 5 (grouper par heure UTC, libeller en local, `BornesPlage.Jour`), §Pattern 6 (couverture par intervalles garantis ; **juillet 2026 n'est PAS vide** par timestamp : 23 211 lignes de juillet vivent dans des fichiers d'août/septembre) |
| TOK-05 | Aucun pourcentage dérivé de tokens : garde structurelle (aucun type de la couche historique n'expose un `double` de quota calculé à partir de tokens) ; périmètre partiel écrit dans le schéma et les docs | §Garde TOK-05 (sous-namespace `…Historique.Tokens` + garde réflexive « aucun `double`/`float`/`decimal` dans la surface publique » + garde textuelle), §Docs (§8 de `data-sources.md` sous une NOUVELLE classe de garde documentaire, `ContratJournalDocumenteTests` intacte) |
</phase_requirements>

## Summary

Tout ce que la phase suppose a été **mesuré sur la vraie machine, en lecture seule** : 1 603 transcripts (2,08 Go, 1 516 de
sous-agents, plus gros 55,8 Mo), 508 596 lignes dont 238 857 `assistant` (le pré-filtre texte `"type":"assistant"` a une
précision de 100 % : aucune ligne non-assistant ne le contient), 118 401 `message.id` distincts, tous les timestamps en ISO
UTC `Z` à la milliseconde (24 caractères, 0 absent), 0 ligne sans `message.id`, 0 sans `message.model`. **L'agrégat est
minuscule** : 3 886 tuples (tranche × modèle × sub) pour tout l'historique = 524 Ko, 135 o/ligne, 304 Ko pour septembre ;
réécrire le mois entier à chaque lot coûte quelques millisecondes. **Le prototype C# en flux** (thread `BelowNormal`,
lecteur de lignes au niveau octet, pré-filtre, `JsonDocument` sans copie, dédup globale) parcourt les 2,08 Go en
**2,4–2,6 s mur / 2,7 s CPU** à cache chaud (766–800 Mo/s), la semaine courante (886 fichiers) étant disponible à **1,7 s** —
dix fois moins que l'estimation d'entrée « 10–20 s ».

Le point ouvert de la dédup est tranché par les données : **1 377 ids vivent dans 2 à 4 fichiers** (338 dans quatre !), toujours
dans le même projet, jamais dans la même session ; la copie porte le `sessionId` du **nouveau** fichier (17 exceptions sur 1 377)
et garde le timestamp d'origine — **rien ne distingue structurellement une copie d'un original**. Une dédup strictement par
fichier sur-compte de **2,1 % (output) à 2,4 % (cache_read)** sur l'historique. Les copies sont récentes (âge p99 3,6 jours,
max 3,6 j, aucune > 7 j), ce qui borne naturellement la mémoire nécessaire. Recommandation : **l'index d'ids est la mémoire
d'idempotence** (`ids-AAAA-MM.jsonl`, une ligne par message vu — id, tranche, modèle, sub, quatre max — en ajout seul, chargé pour
les mois ouverts), et `tokens-AAAA-MM.jsonl` en est la **projection** réécrite atomiquement. Avec cet invariant, « reprise
idempotente », « fichier raccourci/renommé réingéré », « copie fork/resume » et « bloc final partiel qui grandit » sont un
seul et même cas : re-rencontrer un id applique un delta (max − déjà compté), zéro s'il n'y a rien de neuf.

Trois pièges qu'un plan écrit de mémoire manquerait : (1) `await Task.Yield()` sur le thread dédié `BelowNormal` **renvoie la
suite sur le pool** (la priorité basse est perdue au premier fichier) — céder la main avec `Thread.Yield()`/`Sleep(0)` ou un
scheduler mono-thread ; (2) `StreamReader.ReadLine` ne donne aucun offset d'octet fiable — le lecteur de lignes doit être écrit
au niveau octet (40 lignes, mesuré) ; (3) **le 29/03/2027 est un lundi de 24 h** : le jour de 23 h est le **28/03/2027**
(dernier dimanche de mars, vérifié sur le fuseau réel de la machine et déjà testé ainsi par `BornesPlage` en 32-06) — le
libellé de TOK-04 / ROADMAP porte une coquille à corriger par l'orchestrateur, et le test doit viser le 28.

**Primary recommendation:** cinq plans en trois vagues sur des fichiers disjoints sous `Services/Historique/Tokens/` et
`Models/Historique/Tokens/` (sous-namespace `…Historique.Tokens`, ce qui rend la garde TOK-05 réflexive et nette) ; état = index
d'ids append-only + curseurs, agrégats projetés ; thread dédié `BelowNormal` piloté par un `BackgroundService` sans `Task.Yield` ;
rendu local par regroupement sur l'heure UTC ; couverture par intervalles garantis persistés.

## Project Constraints (from CLAUDE.md)

- Stack imposée : C# / .NET 8 / WPF / MVVM (CommunityToolkit.Mvvm 8.4.2) / Microsoft.Extensions.Hosting 8.0.1 ; **aucune dépendance
  NuGet nouvelle** (`System.IO.Pipelines` n'est PAS dans le framework partagé 8.0 de cette machine — vérifié — donc lecteur de
  lignes maison).
- MVVM strict, `[ObservableProperty]` / `[RelayCommand]`, DI, dossiers `Models/Views/ViewModels/Services` ; types NEUTRES sous
  `Services/` et `Models/` (`ServicesLayerPurityTests`, préfixe de namespace : les sous-namespaces sont vus).
- Chemins sous le profil utilisateur uniquement, aucun droit admin ; ici : lecture seule stricte de `~/.claude/projects`, écriture
  uniquement sous `ChronosPaths.HistoriqueDir` (`%APPDATA%\Chronos\historique`), jamais construit en dur.
- `utilization`/`resets_at` prioritaires sur tout comptage ; ne jamais présenter une estimation comme exacte ; ici : **jamais de
  pourcentage dérivé de tokens** (garde EXA-04 déjà en place : ne jamais accoler « tokens » et « plafond/budget/limite/capacité »
  par un opérateur, même dans un commentaire).
- Robustesse : aucune source ≠ crash ; parsing tolérant (lignes/champs invalides ignorés et COMPTÉS).
- UI et commentaires en français (les commentaires disent POURQUOI).
- Workflow GSD : TDD par plan (RED nommé → GREEN), mutations jouées et révoquées par copie (`core.autocrlf=true` : jamais `git
  checkout` pour révoquer), suite complète deux fois en fin de plan, stage explicite, `--no-verify` en parallèle, ROADMAP/STATE
  jamais édités par un exécuteur, artefacts de scratchpad nommés par plan (scratchpad PARTAGÉ entre agents parallèles).
- `Assembly.Location` interdit (mono-fichier) ; chemins de sources/docs des gardes injectés par MSBuild (`CheminSourcesChronos`,
  `CheminDocsChronos`).
- Point unique de parsing ISO : `UsageNormalization.InstantDepuisIso` (`NormalisationUniqueTests` rougit sur tout
  `DateTimeOffset.TryParse` ailleurs sous `Services/**`).

## Mesures sur les données réelles (lecture seule, 2026-09-27 11:20–11:45 locale)

Scripts et prototype sous le scratchpad de session uniquement (`mesure33*.py`, `proto33/`) ; **aucune écriture sous
`~/.claude`, `%APPDATA%\Claude` ni `%APPDATA%\Chronos`** ; aucun overlay lancé.

### Inventaire des transcripts

| Fait | Valeur |
|---|---|
| Fichiers `*.jsonl` sous `~/.claude/projects` (27 projets) | **1 603** (dont **1 516** sous `subagents/` = 94,6 %, 87 principaux) |
| Octets | **2 077 900 346** (1,94 Gio) ; plus gros `4b4e90cb-….jsonl` 55 826 750 o ; plus longue ligne **1 356 886 o** |
| Par mois de **mtime** (n, Mo) | 2026-06 : 38 (9) · 2026-07 : **1** (3) · 2026-08 : 224 (240) · 2026-09 : 1 340 (1 729) |
| Par mois de **timestamp** des lignes `assistant` (lignes, Mo) | 2026-06 : 2 362 (6,8) · **2026-07 : 23 211 (60,4)** · 2026-08 : 55 244 (153) · 2026-09 : 158 038 (554) |
| Plus vieux mtime / plus vieux timestamp | 2026-06-23 14:56 local / **2026-06-23T12:44:22Z** ; dernier 2026-09-27T09:25Z |
| Lignes totales / pré-filtrées `"type":"assistant"` / vraiment `assistant` | 508 596 / **238 857 / 238 857** (précision du pré-filtre 100 %, 47 % des lignes) |
| `message.id` distincts | **118 401** (2,02 lignes par id) ; longueur 28 (`msg_01…`), 36 pour `<synthetic>` ; 0 ligne sans id, 0 sans `requestId` utile |
| `timestamp` | 100 % présents, 24 caractères, `…Z` ms ; **2 lignes futures** (l'une de +3 s dans un sous-agent de la session courante : latence d'écriture, pas horloge décalée) |
| `message.model` | `claude-opus-5` 137 023 · `claude-opus-5-5` 57 960 · `claude-sonnet-5` 15 440 · `claude-fable-5` 14 693 · `claude-fable-5-1` 8 769 · `claude-opus-4-8` 4 677 · `claude-sonnet-4-6` 239 · **`<synthetic>` 54** (usage à quatre zéros dans 54/54 cas) |
| Fichiers dont les timestamps ne sont pas croissants | **2** (copies fork insérées) ; fichiers sans aucune ligne `assistant` : 22 |
| mtime − dernier timestamp | médiane 0,1 s ; max 85 j (un fichier modifié longtemps après sa dernière réponse) ; jamais négatif → le tri par mtime décroissant est un bon proxy de « le plus récent d'abord », pas une vérité par ligne |

**Conséquence pour TOK-04** : « juillet purgé » (STATE.md) est vrai par mtime (1 fichier) et **faux par contenu** : 60 Mo de lignes de
juillet survivent dans des sessions qui ont continué en août. On ne peut PAS savoir quelles tranches de juillet manquent. La
couverture doit donc être un état persisté et daté (Pattern 6), pas une déduction sur les mtimes.

### Tuples d'agrégation (tranche 15 min UTC × modèle × sub)

| Portée | Tuples | Octets JSONL (prototype, format court) |
|---|---|---|
| Tout l'historique (juin → 27/09) | **3 886** | **523 642** (135 o/ligne) |
| 8 derniers jours | 862 | ≈ 116 Ko |
| Par mois | 06 : 70 · 07 : 468 · 08 : 1 100 · **09 : 2 257** (303 814 o) | — |
| Par origine | sub : 2 172 · principal : 1 723 | — |
| Par modèle | opus-5 2 428 · opus-5-5 501 · fable-5 418 · opus-4-8 195 · sonnet-5 193 · fable-5-1 114 · `<synthetic>` 34 · sonnet-4-6 12 | — |

Avec le format « O » du dialecte `LigneJournal` (`2026-09-01T00:00:00.0000000+00:00` au lieu de `2026-09-01T00:00:00Z`) : +13 o/ligne,
≈ 148 o/ligne, septembre ≈ 335 Ko. Négligeable dans les deux cas : **réécrire le mois entier à chaque lot est le bon choix**.

### Ids multi-fichiers (le point ouvert)

| Fait | Valeur |
|---|---|
| Ids présents dans plusieurs fichiers | **1 377** (1,16 % des ids) : 2 fichiers 1 022 · 3 fichiers 17 · **4 fichiers 338** |
| Même projet / même session | 1 377 / **0** (toujours un AUTRE fichier de session du même projet : fork ou resume) |
| Principal ↔ sous-agent mélangés | 0 |
| `usage` identique entre copies | 1 373 ; **4** divergent (la copie n'a pas de `usage` → zéros ; le max par champ absorbe) |
| Tranche 15 min identique entre copies | oui (les 12 « tranches différentes » du premier passage sont des blocs d'un même message à cheval sur une frontière de quart d'heure, dans les DEUX fichiers → réglé par « premier timestamp » D-32-02) |
| `sessionId` de la ligne copiée | = nom du fichier **copieur** (1 360 / 1 377) ; 17 lignes d'UN fichier gardent l'ancien → **aucun marqueur structurel fiable de copie** |
| Fichiers copieurs | 21 ; 1 905 copies datées ; âge (instant du fork − timestamp copié) **p50 0,17 j · p90 2,35 j · p99 3,56 j · max 3,59 j · 0 au-delà de 7 j** |
| Sur-comptage d'une dédup PAR FICHIER | input +0,35 % · **output +2,15 %** (123 173 178 → 125 814 768) · cache_w +2,22 % · **cache_r +2,42 %** |
| Ids par mois de première apparition | 06 : 905 · 07 : 11 161 · 08 : 29 470 · **09 : 76 872** ; 30 derniers jours : 86 775 ; 7 jours : 32 950 |

### Prototype C# (scratchpad `proto33/`, Release, net8.0, thread `IsBackground` + `BelowNormal`, cache chaud)

| Mesure | Passe 1 (`ToArray` par ligne) | Passes 2–3 (`ReadOnlyMemory`, sans copie) |
|---|---|---|
| Inventaire + tri mtime décroissant (1 603 `FileInfo`) | 129 ms | 93 ms |
| **Semaine courante disponible** (886 fichiers de mtime ≤ 7 j lus) | 1 743 ms | **1 666–1 725 ms** |
| **Passe complète** (2 078 055 682 o, 0 fichier refusé) | 2 587 ms | **2 425–2 478 ms** |
| CPU total | 2 734 ms | **2 625–2 703 ms** |
| Débit | 766 Mo/s | **788–800 Mo/s** |
| Octets alloués (GC) | 1 670 Mo | **890 Mo** |
| Working set : départ / pic / fin ; tas GC final | 167 Mo pic | **28 / 119–124 / 107–115 Mo ; tas 31 Mo** (dictionnaire de 118 407 ids + curseurs) |
| Agrégation 118 407 ids → 3 886 tuples ; écriture 4 mois + curseurs | 43 ms ; 49 ms | idem |
| `curseurs.json` (1 603 entrées, chemins relatifs, mtime « O ») | **351 816 o** ; `offset == taille` pour **1 603 / 1 603** (tous terminés par `\n`) | idem |
| Mode « rien n'a bougé » : contrôle (taille, mtime) de 1 603 fichiers | — | **63 ms** (+ 93 ms d'inventaire) |

**À froid** : non mesurable sans purge du cache (pas de droit admin). Le volume `C:` est un **Samsung 870 QVO SATA** (≈ 500 Mo/s
séquentiel) → borne attendue **≈ 4–5 s de lecture pure, 6–8 s de passe** ; sur NVMe ce serait ≈ 3 s. L'estimation d'entrée « 10–20 s »
est à remplacer par « **≈ 2,5 s chaud / ≈ 7 s froid** » dans le SUMMARY de 33-05.

**Mémoire** : le pic de +90 Mo au-dessus du départ est du churn Gen0 (890 Mo alloués en 2,5 s) et retombe ; l'état résident est le tas
de 31 Mo. Un `Utf8JsonReader` sur le `Span` au lieu de `JsonDocument` réduirait encore l'allocation, mais `JsonDocument.Parse(ReadOnlyMemory<byte>)`
**ne copie pas l'entrée** (elle doit rester stable pendant la vie du document — c'est le cas, on la libère avant la ligne suivante)
et suffit.

## Standard Stack

### Core (déjà dans le dépôt — rien à installer)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 8 BCL (`System.IO`, `System.Text.Json`, `System.Threading`) | runtime 8.0.25, SDK 10.0.201 | flux de fichiers, JSON, thread dédié | Contrainte « aucune dépendance NuGet nouvelle » ; tout ce dont la phase a besoin y est |
| Microsoft.Extensions.Hosting | 8.0.1 (csproj) | `BackgroundService` de reconstruction, ordre Start/Stop | Déjà le Host de l'app ; `RefreshOrchestrator` et `JournalisationUsageProvider` sont des `IHostedService` |
| `System.Text.Json` `JsonDocument` / `JsonSerializer` | intégré | parse tolérant d'une ligne, sérialisation des agrégats et curseurs | Dialecte maison (`LigneJournal`, `LastExactStore`) : DTO privés `[JsonPropertyName]`/`[JsonPropertyOrder]`, `UnsafeRelaxedJsonEscaping`, instants « O » UTC |

### Supporting (à réutiliser tel quel)
| Élément | Où | Pour |
|---|---|---|
| `DedupUsage.LireUsage(JsonElement, out …)` (statique) | `Services/DedupUsage.cs` | **SEUL** lecteur autorisé de `message.usage` (garde textuelle récursive) |
| `UsageNormalization.InstantDepuisIso` | `Services/UsageNormalization.cs` | tout texte ISO → instant (timestamp des lignes, `slot`, `mtime` des curseurs) |
| `LigneJournal` (motif), `LecteurJournal.LireFichier` (motif), `JournalReleves.NomFichier` (motif mois UTC) | `Services/Historique/` | dialecte JSONL mensuel tolérant ; regex de nom `^tokens-(\d{4})-(\d{2})\.jsonl$` |
| `LastExactStore.Save` (motif temp + `File.Move(overwrite:true)`, `DerniereEcriture` = mtime après le Move, `DerniereErreur`, consigner puis relancer) | `Services/LastExactStore.cs` | écriture atomique du mois et de `curseurs.json` |
| `IEtatMagasin` + `NomsMagasins.AgregatsTokens` | `Services/IEtatMagasin.cs` | troisième magasin du diagnostic (le nom existe déjà) |
| `BornesPlage.Jour/SemaineDeForfait/QuatreSemaines(tz)`, `BornesPlage.FuseauParisPourTests()`, `Plage` | `Services/Historique/BornesPlage.cs`, `Models/Historique/LectureJournal.cs` | bornes locales ; fuseau des tests (« Romance Standard Time », repli « Europe/Paris ») |
| `IClock` / `FakeClock`, `FakeEtatJournal` (motif de faux d'état) | `Services/IClock.cs`, `tests/Fakes/` | horloge injectée ; faux d'`IEtatReconstruction` sur le même moule |
| `RefreshOrchestrator` (motif `BackgroundService` neutre, « rendre la main à StartAsync ») | `Services/RefreshOrchestrator.cs` | forme du service hébergé |
| `TranscriptSessionSource.EstSousAgent` (dossier parent nommé `subagents`, ordinal-insensible) | `Services/TranscriptSessionSource.cs` l.108 | **la** convention `sub` du dépôt : `f.Directory?.Name == "subagents"` |
| Fixture réelle `TestData/transcript-multi-blocs.jsonl` (7 lignes, clés 2.1.281) et `TestData/SubagentsRoot/` | `tests/Chronos.Tests/TestData/` | gabarit d'anonymisation ; racine avec `subagents/agent-abc.jsonl` |
| `GardesDedupUsageTests`, `GardesDoctrineTests`, `ContratJournalDocumenteTests`, `CompositionRootTests` | `tests/` | motifs de garde textuelle / réflexive / documentaire / DI |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Lecteur de lignes maison au niveau octet | `System.IO.Pipelines` (`PipeReader`) | absent du framework partagé 8.0 ici → NuGet → interdit. 40 lignes maison suffisent (prototype) |
| `JsonDocument.Parse(ReadOnlyMemory<byte>)` | `Utf8JsonReader` sur `Span` | −allocation, +code ; inutile vu 2,7 s CPU au total |
| Index d'ids append-only + agrégats projetés (recommandé) | dédup par fichier + contribution par fichier dans `curseurs.json` | sur-compte 2,2 % (copies fork/resume) et réécrit plusieurs Mo par lot ; rejeté |
| Timer de polling 60 s pour l'incrémental | `FileSystemWatcher` récursif sur `~/.claude/projects` | FSW sur 1 600 fichiers : débordement de tampon, faux réveils ; le polling coûte **156 ms/min** mesurés, robuste, testable avec `FakeClock` |
| Thread dédié `BelowNormal` | pool + `Task.Yield` | la priorité ne se règle pas sur un thread du pool ; voir Pitfall 1 |

**Installation :** aucune. **Version verification :** `Microsoft.Extensions.Hosting 8.0.1` et `CommunityToolkit.Mvvm 8.4.2` déjà dans
`src/Chronos/Chronos.csproj` ; runtime `8.0.25` présent (`dotnet --list-runtimes`) ; aucun paquet à ajouter.

## Architecture Patterns

### Carte des fichiers proposée (discrétion de Claude, dans les conventions du dépôt)

```
src/Chronos/
├── Models/Historique/Tokens/                 # namespace Chronos.Models.Historique.Tokens (garde TOK-05 par préfixe)
│   ├── TrancheTokens.cs                      # record (Slot UTC, Model, Sub, In, Out, CacheW, CacheR, N) — entiers seulement
│   ├── LectureAgregats.cs                    # Tranches triées, Plage, Couverture (états + intervalles), LignesIgnorees
│   └── EtatReconstruction.cs                 # enum Phase {JamaisLancee, Reconstruction, Incremental, Arretee, EnEchec} + record de progression (N, M, semaine courante prête, dernier fichier, durées)
├── Services/Historique/Tokens/               # namespace Chronos.Services.Historique.Tokens
│   ├── LigneAgregat.cs                       # Serialiser/Parser tolérant, Champs[9], SchemaVersion, Perimetre (const texte)
│   ├── MagasinAgregats.cs                    # état mémoire par mois, réécriture atomique tokens-AAAA-MM.jsonl, IEtatMagasin, rétention
│   ├── LecteurTranscript.cs                  # lecteur octet → lignes complètes + offsets ; pré-filtre ; LireUsage ; MessageLu
│   ├── IndexMessages.cs                      # id → (slot, model, sub, max×4) ; delta ; ids-AAAA-MM.jsonl append-only ; chargement borné
│   ├── Curseurs.cs                           # curseurs.json atomique ; classification inchangé / grandi / raccourci / nouveau / disparu
│   ├── ReconstructionTokens.cs               # BackgroundService + thread dédié BelowNormal ; lots ; incrémental ; IEtatReconstruction
│   ├── LecteurAgregats.cs                    # Lire(dossier, de, a) : mois UTC chevauchants, tri, couverture
│   ├── CouvertureTokens.cs                   # intervalles garantis persistés (couverture.json) ; classification d'une tranche
│   └── RenduLocalTokens.cs                   # pur : ParHeure(tz) (25 / 23 barres), ParQuartDHeure, ParModele, PartSousAgents
tests/Chronos.Tests/
├── TestData/transcripts/<cas>/…              # extraits RÉELS anonymisés (voir §Fixtures)
├── TestData/tokens/<cas>/tokens-AAAA-MM.jsonl # agrégats de test (DST, tolérance, à cheval)
└── Fakes/FakeEtatReconstruction.cs
```

Pourquoi un sous-dossier `Tokens/` : la garde TOK-05 devient une phrase (« aucun type du namespace `*.Historique.Tokens` n'expose
un flottant »), `ServicesLayerPurityTests` le voit déjà (préfixe), `GardesDedupUsageTests`/`GardesDoctrineTests`/`NormalisationUniqueTests`
balaient en `AllDirectories`. Ajouter au test de pureté un cas « la garde voit `Chronos.Services.Historique.Tokens` » (motif de 32-04).

### Pattern 1 : lecteur de lignes au niveau octet, offset de la dernière ligne complète
**What :** `FileStream(chemin, Open, Read, ReadWrite | Delete, bufferSize: 1, FileOptions.SequentialScan)` + tampon de 64 Ko + découpe sur
`\n` ; une ligne qui déborde du tampon s'accumule dans un `MemoryStream` (la plus longue mesurée : 1,36 Mo) ; le fragment final sans
`\n` n'est **pas** traité et le curseur = offset juste après le dernier `\n`. `\r` final retiré par prudence.
**When :** toute lecture de transcript (reconstruction et incrémental). `StreamReader.ReadLine` est exclu : son décodeur lit en avance,
`BaseStream.Position` ne correspond à aucune ligne.
**Example :**
```csharp
// Source : prototype proto33 (mesuré 788–800 Mo/s) ; FileShare.Delete pour qu'une purge de Claude Code n'échoue pas contre nous.
long offsetLigne = depuis; fs.Seek(depuis, SeekOrigin.Begin);
while ((n = fs.Read(tampon, 0, tampon.Length)) > 0)
{
    int debut = 0;
    while (true)
    {
        int idx = Array.IndexOf(tampon, (byte)'\n', debut, n - debut);
        if (idx < 0) { reste.Write(tampon, debut, n - debut); break; }          // fragment à cheval → attendre la suite
        ReadOnlyMemory<byte> ligne = reste.Length == 0
            ? new ReadOnlyMemory<byte>(tampon, debut, idx - debut)
            : Joindre(reste, tampon, debut, idx - debut);                       // ligne > 64 Ko : rare (1 356 886 o max)
        offsetDerniereComplete = offsetLigne + ligne.Length + 1;                 // '\n' compris
        if (ligne.Span.IndexOf("\"type\":\"assistant\""u8) >= 0) Traiter(ligne); // pré-filtre : 47 % des lignes passent, 100 % justes
        offsetLigne = offsetDerniereComplete; debut = idx + 1;
        if (debut >= n) break;
    }
}
// reste non vide = ligne en cours d'écriture : NON comptée, curseur = offsetDerniereComplete (relue au prochain cycle)
```
Dans `Traiter` : `JsonDocument.Parse(ligne)` (pas de copie), `type == "assistant"` ET `message.role == "assistant"` (le pré-filtre est une
économie, pas l'autorité : motif `TranscriptSessionSource`), `timestamp` via `UsageNormalization.InstantDepuisIso`, `message.model`
(`<absent>` si manquant), `sub` par le nom du dossier parent, puis **`DedupUsage.LireUsage`** pour les identifiants et les quatre compteurs.

### Pattern 2 : l'index d'ids est la mémoire d'idempotence ; les agrégats en sont la projection
**What :**
- `ids-AAAA-MM.jsonl` (mois UTC du **slot**) : une ligne par message vu `{v, id, slot, model, sub, in, out, cache_w, cache_r}`, **ajout
  seul** (une seule écrivaine : mutex CPT-03 ; ouverture `OpenOrCreate` + `Seek(End)` + `FileShare.None` avec reprises, motif
  `JournalReleves.EcrireSousVerrou`, jamais le mode « ajout » de `FileStream`). Un id peut y figurer plusieurs fois (blocs partiels,
  copies) : **à la relecture, le max par champ gagne** (le champ n'est jamais soustrait).
- `IndexMessages` en mémoire : `Dictionary<string, Entree>` pour les mois **ouverts** (mois qui chevauchent `[now − HorizonIndex, now]`,
  `HorizonIndex = 45 j`, borne ≥ 12 × l'âge max observé des copies 3,6 j) ; ≈ 87 k ids sur 30 j ≈ 11–15 Mo résidents à ce rythme.
- `Ajouter(MessageLu m)` → si id inconnu : entrée + delta = ses compteurs, `n += 1` ; si connu : delta = max(anciens, nouveaux) − anciens
  (souvent 0), `n` inchangé, **ligne réécrite dans le shard seulement si delta ≠ 0**. Le delta est appliqué à la tranche
  `(slot du PREMIER timestamp vu, model, sub)` du `MagasinAgregats`.
- `tokens-AAAA-MM.jsonl` = projection triée (slot, model, sub) de l'état mémoire, réécrite atomiquement pour chaque mois touché par le lot.
- **Au démarrage** : charger les shards des mois ouverts (dédup id → max), **reprojeter** ces mois (le fichier d'agrégats n'est jamais
  la vérité), les mois plus anciens restent gelés tels quels.
- **Retentions** : agrégats 24 mois (aligné sur `JournalReleves.RetentionMois`, à citer, pas à recopier en dur), shards d'ids
  `RetentionIndexMois = 3` (≈ 11 Mo/mois à ce rythme → ≤ 35 Mo disque). Au-delà, un mois d'agrégats est **gelé** : une copie fork d'un
  message de plus de 3 mois (jamais observé, max 3,6 j) serait comptée deux fois — écrit dans la doc comme limite.
**Why :** un seul invariant couvre TOK-03 en entier : **reprise après arrêt** (curseur non persisté → fichier relu depuis l'ancien curseur →
ids connus → delta 0), **fichier raccourci** (taille < offset → relu de zéro → delta 0 → « tranches réécrites, pas ajoutées »),
**fichier renommé** (nouveau chemin → relu de zéro → delta 0 ; l'ancien chemin disparaît → curseur supprimé, rien à soustraire),
**purge par Claude Code** (rien à soustraire : l'historique déjà journalisé reste — critère DoD), **copie fork/resume** (id connu →
delta 0, y compris les 338 ids présents dans quatre fichiers), **bloc final partiel** (`output_tokens` 8 puis 256 → delta 248). Et
la cohérence multi-fichiers sur panne se règle par l'**ordre d'écriture** : shard (ajout) → agrégats (Move) → `curseurs.json` (Move) ;
un arrêt à n'importe quel point est rattrapé par la reprojection et la relecture depuis le dernier curseur persisté.
**Alternative rejetée :** dédup par fichier sans index (sur-compte 2,1–2,4 % mesuré ; et une reprise après arrêt relit un fichier de
zéro et recompte tout ce qui avait été compté — l'idempotence exigerait de toute façon une mémoire par fichier).

### Pattern 3 : `BackgroundService` qui possède un thread dédié `IsBackground` + `BelowNormal`
**What :** `ExecuteAsync(ct)` crée `new Thread(Boucle) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Chronos.Tokens" }`,
le démarre et **retourne un `TaskCompletionSource.Task`** que le thread complète en sortant → `StartAsync` rend la main immédiatement (le
VM et l'orchestrateur ne sont jamais retardés), `StopAsync` annule `ct`, le thread vérifie `ct.IsCancellationRequested` **entre
fichiers et toutes les 4 096 lignes**, fait un dernier flush (≤ 50 ms) et sort ; `HostOptions.ShutdownTimeout` par défaut (30 s) ne
sera jamais approché. Toute exception dans la boucle est capturée : `Phase = EnEchec`, `DerniereErreur`, jamais de crash.
**Céder la main :** `Thread.Yield()` (ou rien : 2,7 s CPU au total à priorité basse) entre fichiers — **jamais `await Task.Yield()`**
(Pitfall 1). Les deux exigences du texte (« thread BelowNormal » et « Task.Yield entre fichiers ») sont contradictoires prises au pied
de la lettre ; l'intention (« ne jamais monopoliser, rester interruptible ») est tenue par le thread dédié à priorité basse +
vérification d'annulation fréquente + `Thread.Yield()`. À écrire dans le SUMMARY comme décision (D-33-xx).
**Boucle :** (1) inventaire (`EnumerateFiles` récursif sous try/catch, `FileInfo` taille + mtime, 93 ms) trié mtime décroissant ;
(2) classification par `Curseurs` ; (3) lecture des fichiers « nouveau / grandi / raccourci » ; **checkpoint « semaine courante »** dès que
tous les fichiers de mtime ≥ now − 7 j sont lus (flush + `SemaineCouranteDisponible = true`) ; puis flush **tous les 100 fichiers ou 2 s** ;
(4) flush final, `Phase = Incremental` ; (5) attendre `CadenceIncrementale` (60 s, constante nommée) ou l'annulation, puis retour en (1) —
les fichiers inchangés (taille, mtime) ne sont pas ouverts (63 ms pour 1 603).
**Progression :** `IEtatReconstruction` neutre (Services/Historique/Tokens) : `Phase`, `FichiersTraites`, `FichiersTotal`,
`SemaineCouranteDisponible`, `DernierFichier` (nom relatif, pour le diagnostic), `DerniereErreur`, `DureeCpu`/`DureeMur` de la
dernière reconstruction, événement `Changement` (levé sur le thread de fond ; le VM marshalle via `IUiDispatcher` comme pour
`SnapshotChanged`). **Des entiers, jamais une fraction** (TOK-05) : le VM formate « N / M fichiers · la semaine courante est déjà
complète » (vocabulaire F2), la barre est calculée côté UI en phase 34.

### Pattern 4 : écriture atomique d'un mois d'agrégats (dialecte maison)
```csharp
// Source : LastExactStore.Save (temp + Move, mtime après le Move) + LigneJournal (STJ, DTO privé, « O » UTC, WhenWritingNull, encodeur relâché)
var tmp = chemin + ".tmp-" + Environment.ProcessId;
using (var w = new StreamWriter(tmp, false, Utf8SansBom) { NewLine = "\n" })
    foreach (var t in tranches.OrderBy(t => t.Slot).ThenBy(t => t.Model, StringComparer.Ordinal).ThenBy(t => t.Sub))
        w.WriteLine(LigneAgregat.Serialiser(t));
File.Move(tmp, chemin, overwrite: true);
DerniereEcriture = new DateTimeOffset(File.GetLastWriteTimeUtc(chemin), TimeSpan.Zero);   // D-32-05 : mtime, ce qu'une sonde hors arbre lit
```
Ligne : `{"v":1,"slot":"2026-09-01T00:00:00.0000000+00:00","model":"claude-opus-5","sub":true,"in":72,"out":398,"cache_w":48601,"cache_r":20376807,"n":36}`.
DTO privé avec `[JsonPropertyName("in")]`/`("out")` (mots-clés C# : nommer les propriétés `Entree`/`Sortie`). `LigneAgregat.Champs` =
`{ v, slot, model, sub, in, out, cache_w, cache_r, n }` (9), exposé pour la garde documentaire ; `Parser` : `v` lu EN PREMIER, `slot`
via `InstantDepuisIso` et **rejeté s'il n'est pas aligné sur 15 min**, compteurs absents → ligne refusée et comptée (un agrégat sans
compteur n'a pas de sens ; différent du journal des relevés où un champ absent = null), `model` absent → refusée, `sub` absent → `false`.
Ordre déterministe = deux exécutions (interrompue puis reprise / ininterrompue) donnent des fichiers **identiques octet pour octet** :
c'est l'assertion du test d'idempotence.

### Pattern 5 : rendu local — grouper sur l'heure UTC, libeller en local
**What :** une « barre par heure » de la vue Semaine = regroupement des tranches par **début d'heure UTC** (`slot` tronqué à l'heure),
jamais par heure d'horloge locale (le 25/10/2026, 02:00–03:00 local existe deux fois ; le 28/03/2027, 02:00–03:00 n'existe pas). Le
libellé de chaque barre = `TimeZoneInfo.ConvertTime(debutUtc, tz)` ; la plage du jour vient de `BornesPlage.Jour(instant, tz)` (25 h /
23 h déjà testés en 32-06) ; les barres = les heures UTC de `[Debut, Fin[` → **25 barres le 25/10/2026, 23 le 28/03/2027, 24 sinon**.
Par quart d'heure et par modèle (vue Jour) : les tranches elles-mêmes, filtrées sur la plage UTC. Part sous-agents : somme séparée
des tranches `sub == true`. Tout est **pur** (`RenduLocalTokens`, `tz` injecté, `BornesPlage.FuseauParisPourTests()` en test).
```csharp
// 2026-10-24T22:00Z → 2026-10-25T23:00Z : 25 débuts d'heure UTC ; le 2e et le 3e s'affichent tous deux « 02:xx » (+02:00 puis +01:00)
for (var h = plage.Debut; h < plage.Fin; h = h.AddHours(1))
    barres.Add(new BarreHeure(DebutUtc: h, Libelle: TimeZoneInfo.ConvertTime(h, tz).ToString("HH:mm"), tranches.Where(t => h <= t.Slot && t.Slot < h.AddHours(1))…));
```

### Pattern 6 : couverture = intervalles garantis persistés, jamais déduite des mtimes
**What :** `couverture.json` (atomique, à côté des curseurs) : `PlusAncienneLigneVue` (min des timestamps jamais lus, monotone) et la
liste fusionnée d'intervalles `[passe.Debut − HorizonPurge, passe.Fin]` ajoutés à chaque passe **complète** (reconstruction terminée,
puis chaque cycle incrémental complet) ; `HorizonPurge = 30 j` = `cleanupPeriodDays` par défaut de Claude Code (absent de
`~/.claude/settings.json` sur cette machine — vérifié — donc défaut), écrit comme **HYP-4** dans la doc. Classification d'une tranche :
`t < PlusAncienneLigneVue` → **HorsCouverture** ; `t` dans l'union → **Couverte** (absence de tranche = vraiment zéro activité) ; sinon →
**TranscriptsAbsents** (présence partielle possible : juillet 2026 est exactement ce cas, avec ses 468 tuples survivants). Un arrêt de
Chronos de plus de 30 j crée aussi une zone « transcripts absents » entre deux intervalles : c'est voulu, c'est vrai.
`LectureAgregats` porte `Couverture` (les trois états par sous-plage) comme `LectureJournal` porte `JournalOuvertLe`.

### Pattern 7 : raccord à `DedupUsage` sans violer sa garde
- **Lecture** : `DedupUsage.LireUsage(o, out messageId, out requestId, out in, out out, out cw, out cr)` — statique, déjà public, rend
  les quatre champs séparés : c'est exactement ce qu'il faut. Aucun littéral `"usage"`/`"…_tokens"` hors de `DedupUsage.cs`.
- **Règle du max** : ajouter dans `DedupUsage.cs` une méthode statique pure `Fusionner((in,out,cw,cr) connu, (in,out,cw,cr) lu)` → max par
  champ, utilisée par `DedupUsage.Ajouter` (refactor interne : les ≥ 4 `Math.Max(` restent dans ce fichier, garde 3 verte) ET par
  `IndexMessages`. La règle vit à un seul endroit.
- **Ne pas** ajouter de surcharge `Ajouter` : `GardesDedupUsageTests.Le_helper_de_dedup_existe…` fait `GetMethod("Ajouter", …)` sans
  signature → `AmbiguousMatchException`. Ne pas toucher `Entrees()` ni `TranscriptActivityProvider` (garde de position).
- L'accumulation elle-même (`IndexMessages`, id → slot/model/sub/max, deltas, shards) est un type distinct : `DedupUsage` reste la
  passe de 8 jours de la tête, `IndexMessages` la mémoire persistante des agrégats. Le XML-doc de `DedupUsage` (« le helper s'instancie
  de l'extérieur pour que la phase 33 puisse le scoper autrement ») est à compléter d'une phrase qui nomme `IndexMessages`.

### Curseurs (TOK-03) — sémantique précise
`curseurs.json` : `{ "v": 1, "fichiers": { "<chemin relatif à ProjectsRoot>": { "offset": 7653718, "taille": 7653718, "mtime": "2026-09-27T09:28:31.4783445Z" } } }`
(352 Ko mesurés pour 1 603 fichiers, clés **relatives** et comparées `OrdinalIgnoreCase`). Classification à chaque cycle :

| Observé | Action | Pourquoi |
|---|---|---|
| absent des curseurs | **nouveau** → lu de 0 | fichier créé ou renommé |
| `taille == curseur.taille && mtime == curseur.mtime` | **inchangé** → pas ouvert | 63 ms pour 1 603 |
| `taille > curseur.offset` (mtime différent) | **grandi** → lu depuis `offset` | append-only ; l'offset est une fin de ligne |
| `taille < curseur.offset` | **raccourci** → lu de 0 | réécriture ou remplacement |
| `taille == offset` mais mtime différent | traité comme grandi (rien à lire, curseur mis à jour) | touch sans contenu |
| présent dans les curseurs, absent du disque | **disparu** → entrée retirée, compteur `FichiersDisparus` au diagnostic | purge ; rien à soustraire (Pattern 2) |

`offset` = octets jusqu'au dernier `\n` inclus. **Ligne future** (timestamp > `IClock.UtcNow`) : ne PAS avancer le curseur au-delà
d'elle (elle serait perdue à jamais alors que dans le cas mesuré — +3 s — c'est une latence d'écriture) ; le fichier est relu au cycle
suivant depuis cette ligne ; au-delà de `ToleranceFutur = 24 h` d'avance persistante, la ligne est ignorée et comptée (horloge décalée,
Pitfall 3 de la phase 19). Écrit atomiquement (temp + Move) **après** les agrégats de chaque lot.

### Garde TOK-05 (structurelle)
1. **Réflexive** : pour chaque type de l'assembly dont le namespace commence par `Chronos.Models.Historique.Tokens` ou
   `Chronos.Services.Historique.Tokens`, aucune propriété, aucun retour de méthode, aucun paramètre, aucun champ public dont le type
   (ou l'argument générique, ou l'élément de tableau) est `double`, `float`, `decimal` ; aucun membre dont le nom contient
   `Utilization|Utilisation|Pourcent|Quota|Ratio|Fraction|Pct`. Anti-mutisme : ≥ 6 types vus, et **contrôle positif** :
   `Chronos.Models.Historique.DeltaConsommation.Delta` (un `double` légitime, relevés) est HORS filtre — le test prouve que le filtre
   l'aurait attrapé s'il était dans `Tokens`.
2. **Textuelle** (motif `GardesDoctrineTests`) : sous `Services/Historique/Tokens/**` et `Models/Historique/Tokens/**`, aucun jeton
   `double|float|decimal|/ 100|\* 100|Utilization` (commentaires compris — reformuler « part » en entiers : la part sous-agents est une
   paire d'entiers `(SousAgents, Total)`, pas un ratio).
3. `ServicesLayerPurityTests` : ajouter le cas « la garde voit `Chronos.Services.Historique.Tokens` » (motif 32-04).
4. Périmètre écrit : `LigneAgregat.Perimetre = "Claude Code seulement — hors Cowork et claude.ai ; bruts, non pondérés"` comparé au texte
   de `docs/data-sources.md` §8 par la nouvelle garde documentaire, et affiché tel quel par le diagnostic.

### Docs et garde documentaire (étendre, pas contourner)
`ContratJournalDocumenteTests` découpe le §7 jusqu'au dernier `---` et exige **exactement une** ligne `## 7. Journal d'historique` : ajouter
un **§8 « Agrégats de tokens »** après le `---` qui clôt le §7 ne casse rien (vérifié sur le texte : le §7 se termine par `---` avant la
ligne finale du document). Nouvelle classe `ContratAgregatsDocumenteTests` (même moule : `SectionDe`, réflexion sur le DTO privé
`LigneAgregatDto` → `Champs` == `JsonPropertyName` dans l'ordre, chaque champ nommé entre accents graves, `Perimetre` cité mot pour mot,
`HYP-4` présent avec « cleanupPeriodDays » et « 30 »), et **une ligne** ajoutée au §7 « Ce que le journal n'est pas » (déjà là :
« les tokens … arrivent en phase 33 ») → remplacer « arrivent en phase 33 » par un renvoi au §8. Mettre à jour la ligne finale « complété le … ».

### Anti-Patterns to Avoid
- **`await Task.Yield()` dans la boucle du thread dédié** → continuation sur le pool, priorité perdue (Pitfall 1).
- **`StreamReader` pour les curseurs** → offsets faux (Pitfall 2).
- **Grouper les barres par heure d'horloge locale** → 24 barres le 25/10 (deux heures fusionnées) et une barre vide le 28/03.
- **Déduire la couverture des mtimes** → juillet « purgé » alors que 60 Mo de juillet existent.
- **Sauter une ligne future en avançant le curseur** → message perdu à jamais.
- **Dédup par fichier « parce que c'est plus simple »** → +2,2 % mesurés, et une reprise après arrêt recompte le fichier en cours.
- **`Directory.EnumerateFiles` sans try/catch autour de l'énumération paresseuse** → une `IOException` au milieu tue la passe.
- **Chemins absolus dans `curseurs.json`** → 2× plus gros et cassés si le profil change de lettre.
- **Accoler « tokens » et « plafond/budget/limite/capacité » par `/` ou `*`** dans un commentaire → `GardesDoctrineTests` rougit.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Lecture des quatre compteurs de `usage` | un second parseur de `message.usage` | `DedupUsage.LireUsage` | garde CPT-01 textuelle récursive ; un seul lecteur d'un champ piégeux |
| Règle « max par champ » | une deuxième implémentation dans l'index | `DedupUsage.Fusionner` (à extraire) | la règle mesurée vit à un endroit ; ≥ 4 `Math.Max(` gardés dans `DedupUsage.cs` |
| Texte ISO → instant | `DateTimeOffset.TryParse` | `UsageNormalization.InstantDepuisIso` | `NormalisationUniqueTests` |
| Bornes locales (jour, semaine, 4 semaines) | arithmétique de `TimeSpan` | `BornesPlage` | 25 h / 23 h / 169 h / 167 h déjà prouvés |
| Fuseau de test | `TimeZoneInfo.Local` | `BornesPlage.FuseauParisPourTests()` | déterministe sur toute machine |
| Écriture atomique | append en place | temp + `File.Move(overwrite:true)` (motif `LastExactStore`) ; pour les shards, `EcrireSousVerrou` (motif `JournalReleves`) | rien de partiel n'est jamais observable |
| Nom de mois UTC | formatage local | motif `JournalReleves.NomFichier` (culture invariante) | `tokens-2026-09.jsonl` quel que soit le fuseau |
| Détection `sub` | regex sur le chemin | `f.Directory?.Name == "subagents"` (motif `TranscriptSessionSource`) | convention unique du dépôt |
| Faux d'état pour le VM | mocks | `FakeEtatReconstruction` sur le moule de `FakeEtatJournal` | réglable après construction, relu à chaque tick |

**Key insight :** tout ce qui est délicat ici (usage, max, ISO, DST, atomicité) a déjà été payé une fois dans le dépôt et gardé par un
test ; la phase 33 **compose** ces briques et n'en réécrit aucune.

## Runtime State Inventory

Phase de création (aucun renommage/refactor) — étape 2.5 sans objet. Le seul état d'exécution touché est **créé** par la phase :
`%APPDATA%\Chronos\historique\{tokens-AAAA-MM.jsonl, ids-AAAA-MM.jsonl, curseurs.json, couverture.json}` (vue RÉELLE quand l'overlay est
lancé par l'Explorateur ; vue virtualisée MSIX pour tout processus lancé sous l'app bureau — règle de 32-02, à rappeler dans la doc).

## Common Pitfalls

### Pitfall 1 : `await Task.Yield()` quitte le thread dédié
**What goes wrong :** la première itération tourne sur le thread `BelowNormal`, toutes les suivantes sur des threads du pool à priorité
normale ; la décision « priorité basse » est vidée en silence.
**Why :** sans `SynchronizationContext` ni `TaskScheduler` personnalisé, la continuation d'un `await` est planifiée sur le pool (doc
officielle « SynchronizationContext and console apps » : le thread principal n'apparaît qu'une fois, le reste sur le pool). Le thread
UI WPF a un contexte ; un `new Thread` n'en a pas.
**How to avoid :** boucle **synchrone** sur le thread dédié ; `Thread.Yield()` / `Thread.Sleep(0)` entre fichiers ; `ct.IsCancellationRequested`
souvent ; `BackgroundService.ExecuteAsync` ne fait que démarrer le thread et rendre un `TaskCompletionSource.Task`.
**Warning signs :** un test qui capture `Thread.CurrentThread.Priority` dans le callback « après fichier » et le trouve `Normal` — à écrire.

### Pitfall 2 : offsets d'octets avec `StreamReader`
**What goes wrong :** `BaseStream.Position` est en avance de tout le tampon du décodeur ; un curseur posé là coupe une ligne.
**How to avoid :** Pattern 1 (découpe au niveau octet). Test : fixture dont la dernière ligne est tronquée (sans `\n`) → curseur = offset
de la fin de l'avant-dernière ligne ; puis la fixture est complétée (ligne finie + une nouvelle) → relecture depuis l'offset donne
exactement les deux messages, ni un de moins ni un de plus.

### Pitfall 3 : la date DST de mars 2027 dans l'énoncé
**What goes wrong :** TOK-04 et le ROADMAP écrivent « 23 h le 29/03/2027 » ; le 29/03/2027 est un **lundi ordinaire** (vérifié :
`Romance Standard Time`, offset +02:00 stable) ; le jour de 23 h est le **dimanche 28/03/2027** (01:30 → 03:30 local à 01:00Z). Un test
qui affirmerait 23 barres le 29 serait rouge — ou pire, faux s'il était écrit pour passer.
**How to avoid :** tester `BornesPlage.Jour(2027-03-28…)` = 23 h (déjà vrai en 32-06 : « 28/03/2027 = 23 h ») et 23 barres ; **signaler à
l'orchestrateur** la coquille de REQUIREMENTS/ROADMAP (un exécuteur ne les édite pas) ; le SUMMARY de 33-04 la consigne.

### Pitfall 4 : une assertion existante casse forcément
**What goes wrong :** `DiagnosticServiceTests.La_section_Magasins_persistants_dit_la_vue_et_les_trois_magasins` (l. 1354) épingle le texte
`"agrégats de tokens : aucun (phase 33)"` ; le câblage de 33-05 le remplace.
**How to avoid :** 33-05 modifie cette assertion (écart assumé à la règle « insertions seules », nommé dans le plan) et garde l'ordre
« lignes de processus avant la ligne des agrégats » (l. 1460–1463). Nouvelle sous-section `[Agrégats de tokens]` après la ligne de magasin :
phase, N / M, semaine courante, dernier fichier, fichiers disparus, `LignesIgnorees`, `IdsConnus`, `Perimetre`.

### Pitfall 5 : les gardes textuelles mordent sur les nouveaux fichiers
**What goes wrong :** un littéral `"output_tokens"` dans un commentaire de `LecteurTranscript.cs` → `GardesDedupUsageTests` ; un
`DateTimeOffset.TryParse` → `NormalisationUniqueTests` ; « tokens / plafond » dans un XML-doc → `GardesDoctrineTests` ; un `double` dans
`Tokens/` → la nouvelle garde TOK-05 ; `FileMode.Append` même en commentaire → critère de doctrine (32-04 l'a payé).
**How to avoid :** relire chaque fichier neuf contre les cinq motifs avant le commit GREEN ; formuler les commentaires sans reproduire les
jetons (« les quatre compteurs de usage » sans guillemets JSON).

### Pitfall 6 : `JsonDocument.Parse(ReadOnlyMemory<byte>)` et la durée de vie du tampon
**What goes wrong :** le document référence le tampon ; réutiliser le tampon de 64 Ko pendant que le document vit corrompt la lecture.
**How to avoid :** `using var doc` consommé entièrement dans `Traiter` avant de passer à la ligne suivante (le prototype fait ainsi : 0 ligne
ignorée, mêmes totaux que Python). Ne jamais stocker un `JsonElement` au-delà de la ligne.

### Pitfall 7 : « semaine courante » par mtime, pas par timestamp
**What goes wrong :** 886 fichiers ont un mtime ≤ 7 j mais contiennent des lignes bien plus anciennes (un fichier a 85 j d'écart mtime −
dernier message) ; 2 fichiers ont des timestamps non croissants.
**How to avoid :** le checkpoint « semaine courante disponible » signifie « tous les fichiers dont le mtime tombe dans la semaine ont été
lus » (c'est suffisant : un message de la semaine ne peut vivre que dans un fichier écrit depuis) ; ne jamais supposer l'ordre des
lignes dans un fichier (le slot vient du timestamp de chaque ligne, le max par id est indépendant de l'ordre).

### Pitfall 8 : `<synthetic>` et lignes à quatre zéros
**What goes wrong :** 54 lignes `message.model == "<synthetic>"` (usage à zéro) créent 34 tuples de bruit avec `n > 0` et zéro token.
**How to avoid :** une ligne `assistant` dont les quatre compteurs valent 0 n'est ni comptée ni indexée (« rien à jeter » : il n'y a aucun
token connu) — décision à écrire (D-33-xx) et à documenter au §8 ; `model` reste « tel que lu » pour tout le reste.

### Pitfall 9 : arbre partagé, scratchpad partagé
Les plans parallèles (33-01 ∥ 33-02, 33-03 ∥ 33-04) compilent dans le même arbre : instantané `git archive <SHA GREEN>` sous un nom **unique
par plan** (`snap-33-02`), fenêtres RED courtes, révocation de mutation par copie (sha256), jamais `git checkout -- fichier` (autocrlf).

## Code Examples

### Tranche et slot (Models/Historique/Tokens)
```csharp
// Entiers seulement : la garde TOK-05 rougit sur tout flottant dans ce namespace.
public sealed record TrancheTokens(DateTimeOffset Slot, string Model, bool Sub, long In, long Out, long CacheW, long CacheR, int N)
{
    public static DateTimeOffset SlotDe(DateTimeOffset t)   // début de tranche 15 min UTC
    {
        var u = t.UtcDateTime;
        return new DateTimeOffset(u.Year, u.Month, u.Day, u.Hour, u.Minute - u.Minute % 15, 0, TimeSpan.Zero);
    }
}
```

### Service hébergé avec thread dédié (Pattern 3)
```csharp
public sealed class ReconstructionTokens : BackgroundService, IEtatReconstruction
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var fin = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { Boucle(stoppingToken); }                 // synchrone : pas un seul await ici (Pitfall 1)
            catch (Exception ex) { Signaler(Phase.EnEchec, ex); }
            finally { fin.TrySetResult(); }
        }) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Chronos.AgregatsTokens" };
        thread.Start();
        return fin.Task;                                   // StartAsync rend la main immédiatement ; StopAsync attend ce Task après annulation
    }
}
```

### Delta d'un id re-rencontré (Pattern 2)
```csharp
// IndexMessages.Ajouter : rend le delta à appliquer à la tranche (0 partout si rien de neuf) ; N n'augmente qu'à la première vue.
if (_parId.TryGetValue(m.Id, out var connu))
{
    var fusion = DedupUsage.Fusionner((connu.In, connu.Out, connu.CacheW, connu.CacheR), (m.In, m.Out, m.CacheW, m.CacheR));
    var delta = (fusion.In - connu.In, fusion.Out - connu.Out, fusion.CacheW - connu.CacheW, fusion.CacheR - connu.CacheR);
    if (delta is (0, 0, 0, 0)) return Delta.Aucun;          // copie fork/resume, relecture après arrêt, fichier raccourci : idempotent
    _parId[m.Id] = connu with { In = fusion.In, Out = fusion.Out, CacheW = fusion.CacheW, CacheR = fusion.CacheR };
    _aEcrire.Add(_parId[m.Id]);                             // le shard reçoit la nouvelle ligne (le max gagnera à la relecture)
    return new Delta(connu.Slot, connu.Model, connu.Sub, delta, NouveauMessage: false);   // slot du PREMIER timestamp vu (D-32-02)
}
```

### Rendu local (Pattern 5) et classification de couverture (Pattern 6)
```csharp
public static IReadOnlyList<BarreHeure> ParHeure(IReadOnlyList<TrancheTokens> tranches, Plage plage, TimeZoneInfo tz) { /* boucle sur les débuts d'heure UTC de [Debut, Fin[ */ }
public enum EtatCouverture { HorsCouverture, TranscriptsAbsents, Couverte }
public static EtatCouverture Classer(DateTimeOffset slot, CouvertureTokens c)
    => c.PlusAncienneLigneVue is null || slot < c.PlusAncienneLigneVue ? EtatCouverture.HorsCouverture
     : c.Intervalles.Any(i => i.Debut <= slot && slot < i.Fin)         ? EtatCouverture.Couverte
     : EtatCouverture.TranscriptsAbsents;
```

### Câblage DI (33-05, App.xaml.cs, près du journal)
```csharp
services.AddSingleton(sp => new MagasinAgregats(sp.GetRequiredService<ChronosPaths>().HistoriqueDir, sp.GetRequiredService<IClock>()));
services.AddSingleton(sp => new ReconstructionTokens(sp.GetRequiredService<ChronosPaths>(), sp.GetRequiredService<MagasinAgregats>(), sp.GetRequiredService<IClock>()));
services.AddSingleton<IEtatReconstruction>(sp => sp.GetRequiredService<ReconstructionTokens>());
services.AddHostedService(sp => sp.GetRequiredService<ReconstructionTokens>());   // avant RefreshOrchestrator, comme le journal
// DiagnosticService : magasins: [LastExactStore, JournalReleves, MagasinAgregats] + etatReconstruction: IEtatReconstruction (paramètre optionnel, dernière position)
// MainViewModel : IEtatReconstruction? reconstruction = null → propriétés TexteReconstruction / AfficherReconstruction, MAJ au tick (motif MajTexteEtatJournal)
```

## Fixtures (réelles anonymisées, `tests/Chronos.Tests/TestData/transcripts/<cas>/`)

| Cas | Contenu | Prouve |
|---|---|---|
| `multi-blocs/` | réutiliser `transcript-multi-blocs.jsonl` (7 lignes 2.1.281, 77 004 + 50 + 2) placé comme `<session>.jsonl` | max par champ, `sub=false`, `n=3` (3 ids), tranche 11:15Z |
| `sous-agent/<session>/subagents/agent-<id>.jsonl` | 3 lignes réelles `isSidechain:true`, modèle `claude-sonnet-5` | `sub=true` par le dossier parent, modèle tel que lu |
| `tronque/` | fichier dont la dernière ligne s'arrête au milieu du JSON (sans `\n`) | curseur = fin de l'avant-dernière ligne ; 0 exception ; relecture après complétion |
| `reduit/` | même fichier en deux versions (longue puis courte) copiées par le test | « raccourci » → relu de zéro → agrégats identiques (delta 0) |
| `futur/` | une ligne datée `now + 3 s` (par `FakeClock`) puis `now + 3 j` | curseur bloqué avant la ligne ; ignorée et comptée au-delà de 24 h |
| `fork-copie/` | deux `<session>.jsonl` du même projet : A (3 messages), B (2 copies de A + 2 messages propres, `sessionId` = B) | 5 messages, pas 7 ; ordre mtime B puis A et A puis B → même résultat |
| `a-cheval-tranche/` | un message dont les blocs sont à 10:59:58Z et 11:00:07Z (gabarit réel 00:51:45/00:52:22) | slot = 10:45Z (premier timestamp), un seul `n` |
| `prefiltre-piege/` | une ligne `user` dont le texte contient `"type":"assistant"` + une ligne `summary` | le pré-filtre passe, l'autorité (`type` + `role`) refuse ; `LignesIgnorees` inchangé |
| `sans-message-id/` | 2 lignes de même `requestId` sans `message.id` + 1 sans aucun id (extrait de la fixture 32-01) | repli `requestId` ; sans id → compté tel quel, jamais indexé |
| `synthetic/` | ligne réelle `model:"<synthetic>"`, quatre zéros | ni comptée ni indexée |
| `tokens/dst-2026-10-25/`, `tokens/dst-2027-03-28/` | agrégats d'une tranche par quart d'heure sur `[22:00Z veille, 23:00Z[` / `[23:00Z veille, 22:00Z[` | 100 / 92 tranches ; 25 / 23 barres ; libellés « 02:xx » doublés / « 02:xx » absent |
| `tokens/tolerance/` | `v=2`, `slot` non aligné, compteur manquant, ligne vide, ligne tronquée | lecture tolérante, comptée |
| `tokens/a-cheval-mois/` | `tokens-2026-09.jsonl` + `tokens-2026-10.jsonl`, plage 30/09 22:00Z → 01/10 02:00Z | mois UTC chevauchants, tri |

Anonymisation : clés de premier niveau et de `usage` réelles, textes « … », `cwd`/`sessionId`/`uuid` en `<…>`, ids `msg_01EXEMPLE…`,
LF sans BOM, jamais un chemin réel (garde `Le_chemin_de_l_app_bureau…` : ne pas citer `Packages`/`Claude_` non plus).

## Découpage en plans (proposition : 5 plans, 3 vagues, fichiers disjoints)

| Plan | Vague | Exigences | Fichiers (créés / modifiés) | Tests (RED nommés) | Mutations à jouer |
|---|---|---|---|---|---|
| **33-01** magasin et schéma | 1 | TOK-01, TOK-05 | `Models/Historique/Tokens/{TrancheTokens, LectureAgregats}.cs`, `Services/Historique/Tokens/{LigneAgregat, MagasinAgregats}.cs`, `tests/{LigneAgregatTests, MagasinAgregatsTests, GardeTokensSansPourcentageTests}.cs`, `ServicesLayerPurityTests` (+1 cas), fixtures `tokens/tolerance` | ligne dans les deux sens (9 champs, ordre, « O » UTC, `in`/`out`) ; `v` inconnu / slot non aligné / compteur absent refusés et comptés ; réécriture atomique (temp absent après Move, contenu trié, octets identiques sur deux écritures du même état) ; `IEtatMagasin` (Nom, Chemin = dossier, `DerniereEcriture` = mtime) ; garde réflexive + textuelle + contrôle positif `DeltaConsommation` | (a) tri retiré → octets différents ; (b) `Move` remplacé par `WriteAllText` direct → test « aucun .tmp ni fichier partiel observable » via lecteur concurrent ; (c) un `double` ajouté à `TrancheTokens` → garde rouge |
| **33-02** lecteur, index, curseurs | 1 | TOK-02 (lecture), TOK-03 | `Services/Historique/Tokens/{LecteurTranscript, IndexMessages, Curseurs}.cs`, `Services/DedupUsage.cs` (+`Fusionner`, XML-doc), `tests/{LecteurTranscriptTests, IndexMessagesTests, CurseursTests}.cs`, `DedupUsageTests` (+1 : `Fusionner`), fixtures `transcripts/*` | offsets sur `tronque` puis complétion ; pré-filtre piège ; `sub` par dossier ; delta 0 sur `fork-copie` dans les deux ordres ; delta 248 sur bloc partiel ; `raccourci`/`nouveau`/`inchangé`/`disparu` ; ligne future bloque le curseur ; shard relu → max gagne ; `synthetic` ignoré ; `GardesDedupUsageTests` toujours vert (aucun littéral hors helper) | (a) `Fusionner` → « dernier gagne » (rougit `Le_max_est_independant_de_l_ordre` ET le test de bloc partiel relu en ordre inverse) ; (b) index vidé entre deux fichiers → `fork-copie` rouge ; (c) curseur avancé sur ligne future → `futur` rouge ; (d) `StreamReader` à la place du lecteur octet → `tronque` rouge |
| **33-03** reconstruction de fond | 2 | TOK-02, TOK-03 | `Services/Historique/Tokens/ReconstructionTokens.cs`, `Models/Historique/Tokens/EtatReconstruction.cs`, `tests/Fakes/FakeEtatReconstruction.cs`, `tests/ReconstructionTokensTests.cs` | ordre mtime décroissant et checkpoint semaine courante (callback de test « après fichier ») ; priorité du thread `BelowNormal` et `IsBackground` observées dans le callback ; annulation en < 200 ms au milieu d'un gros fichier synthétique ; **reprise idempotente** : annulation après k fichiers puis nouvelle instance sur le même dossier ⇒ `tokens-*.jsonl` octet pour octet identiques à une passe ininterrompue (k = 1, 3, tous) ; incrémental : fichier grandi relu depuis l'offset, inchangé jamais ouvert (compteur d'ouvertures), raccourci relu de zéro ; `StartAsync` rend la main avant la fin (< 50 ms) ; exception dans un fichier → `EnEchec` sans crash, les autres fichiers lus | (a) `await Task.Yield()` inséré → test de priorité rouge ; (b) flush des curseurs AVANT les agrégats → test « arrêt entre les deux » rouge (simulé par callback qui annule après l'écriture des curseurs) ; (c) tri mtime croissant → checkpoint semaine rouge |
| **33-04** lecture par plage, rendu local, couverture | 2 | TOK-04 | `Services/Historique/Tokens/{LecteurAgregats, RenduLocalTokens, CouvertureTokens}.cs`, `Models/Historique/Tokens/LectureAgregats.cs` (compléter), `tests/{LecteurAgregatsTests, RenduLocalTokensTests, CouvertureTokensTests}.cs`, fixtures `tokens/dst-*`, `tokens/a-cheval-mois` | 25 barres le 25/10/2026 (deux « 02:xx »), **23 le 28/03/2027**, 24 le 26/09/2026 ; quarts d'heure par modèle ; part sous-agents en entiers ; mois UTC chevauchants ; `HorsCouverture` avant `PlusAncienneLigneVue`, `TranscriptsAbsents` pour juillet (intervalle garanti commençant le 28/08), `Couverte` dedans ; « jamais zéro » : une tranche absente en zone non couverte n'est pas rendue 0 | (a) regroupement par heure locale → 24 barres le 25/10 rouge ; (b) `TimeSpan.FromDays(1)` au lieu de `BornesPlage.Jour` → 28/03 rouge ; (c) intervalle sans `− HorizonPurge` → juillet classé `Couverte` rouge |
| **33-05** câblage, diagnostic, docs, mesure | 3 | TOK-02, TOK-05 (+ intégration TOK-01..04) | `App.xaml.cs`, `Services/DiagnosticService.cs`, `ViewModels/MainViewModel.cs` (propriétés seules, aucun XAML), `tests/{CompositionRootTests (+2), DiagnosticServiceTests (l. 1354 modifiée + 2), MainViewModelTests (+2), ContratAgregatsDocumenteTests (nouveau)}`, `docs/data-sources.md` (§8, renvoi §7, ligne finale), `docs/publish.md` si la procédure change (non attendu) | hosted service avant l'orchestrateur ; mêmes instances (`IEtatReconstruction` == `ReconstructionTokens`, magasin dans la liste du diagnostic) ; section `[Agrégats de tokens]` (phase, N / M, semaine courante, périmètre) ; VM : texte « reconstruction … N / M fichiers · la semaine courante est déjà complète » puis « agrégats à jour il y a N min » ; doc : 9 champs nommés, `Perimetre` mot pour mot, HYP-4, ids-shards et rétentions écrits | (h1) un champ retiré du §8 → garde rouge ; (h2) `Perimetre` modifié dans le code seul → garde rouge ; (c1) hosted service inscrit après l'orchestrateur → `CompositionRootTests` rouge |

Mesure à consigner en 33-05 (SUMMARY + §8) : celles de cette recherche (2,4–2,6 s chaud, 1,7 s semaine courante, 2,7 s CPU, +90 Mo pic /
31 Mo résidents, 63 ms de contrôle incrémental, 524 Ko d'agrégats, ≈ 11 Mo/mois d'ids) — le prototype de recherche est la mesure sur la
vraie machine ; l'overlay n'est pas lancé par l'agent (la phase 35 constate).

## State of the Art

| Old Approach (dans le dépôt) | Current Approach (cette phase) | When Changed | Impact |
|---|---|---|---|
| `TranscriptActivityProvider` : passe complète bornée à 8 j, `StreamReader`, dédup globale en RAM, rien de persisté | lecteur octet + curseurs + index persistant, toute l'histoire, incrémental | phase 33 | la passe de la tête reste telle quelle (garde de position) ; deux lecteurs coexistent, un seul lecteur de `usage` |
| Estimation « 10–20 s CPU » (conseil, STATE.md) | **2,7 s CPU / 2,5 s mur chaud** mesurés, ≈ 7 s froid attendus (SATA) | 2026-09-27 | le throttling élaboré est inutile ; la priorité basse suffit |
| « 491 ids dans 2 à 3 fichiers » (8 j) | **1 377 ids dans 2 à 4 fichiers** (historique), copies ≤ 3,6 j, indiscernables structurellement | 2026-09-27 | index d'ids requis ; borne temporelle justifiée |
| « juillet purgé » | juillet **partiellement présent** (23 211 lignes) | 2026-09-27 | couverture par intervalles garantis, HYP-4 |

**Deprecated/outdated :** l'énoncé « 23 h le 29/03/2027 » (TOK-04, ROADMAP) — c'est le 28/03/2027.

## Open Questions

1. **Coquille de date TOK-04 / ROADMAP (29/03/2027 → 28/03/2027)**
   - What we know : vérifié sur le fuseau réel et déjà testé au 28 par `BornesPlage` (32-06).
   - What's unclear : qui corrige le texte (l'orchestrateur ; un exécuteur ne touche ni ROADMAP ni la formulation de REQUIREMENTS).
   - Recommendation : le planner écrit les tests au 28/03/2027 et mentionne la coquille dans le PLAN de 33-04 ; l'orchestrateur corrige.
2. **Format du `slot` : « O » (dialecte `LigneJournal`) ou court `…T00:00:00Z` (schéma §3 « ISO, `Z` »)**
   - Les deux sont ISO UTC, lisibles par `InstantDepuisIso` ; « O » coûte +13 o/ligne (≈ +10 %, 335 Ko/mois au lieu de 304 Ko).
   - Recommendation : « O » pour l'uniformité du dossier `historique\` (un seul `Instant()`), à écrire au §8.
3. **Rétention des shards d'ids (3 mois recommandés) et `HorizonIndex` (45 j)**
   - Fondé sur l'âge max des copies (3,6 j sur 3 mois d'historique) ; un usage plus intensif ferait grossir les shards (≈ 150 o × ids/mois).
   - Recommendation : constantes nommées dans `IndexMessages`, documentées comme bornes (« au-delà, le mois est gelé »).
4. **Horizon de purge `cleanupPeriodDays`**
   - Non défini dans `~/.claude/settings.json` (défaut 30 j) ; le mécanisme exact de purge n'est pas vérifié (38 fichiers de juin survivent).
   - Recommendation : constante `HorizonPurge = 30 j` + HYP-4 dans la doc ; ne PAS lire `settings.json` pour ça en v1.8 (couplage, et
     `ClaudeSettingsJson` ne connaît que hooks/statusLine).
5. **Exposition au VM sans UI** : propriétés `[ObservableProperty]` seules dans `MainViewModel` (texte + booléen), aucun XAML — la fenêtre
   Historique (34) consommera `IEtatReconstruction` directement ; à confirmer dans le plan 33-05 pour ne pas surcharger le VM du cadran.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK / runtime | build, tests | ✓ | SDK 10.0.201 ; Microsoft.NETCore.App 8.0.25 (et 8.0.21, 8.0.0, 10.0.x) | — |
| `System.IO.Pipelines` dans le framework partagé 8.0 | (option écartée) | ✗ (absent de `shared/Microsoft.NETCore.App/8.0.*`) | — | lecteur de lignes maison (Pattern 1) |
| xunit 2.9.2 + Xunit.StaFact 1.1.11 | tests | ✓ | csproj | — |
| Fuseau `Romance Standard Time` / `Europe/Paris` | tests DST | ✓ / ✓ | transitions 2026-10-25 et 2027-03-28 vérifiées | ids en dur (`FuseauParisPourTests`) |
| `~/.claude/projects` | reconstruction réelle (pas les tests) | ✓ | 1 603 fichiers, 2,08 Go, lecture seule | racine temp injectée par `ChronosPaths` en test |
| Disque `C:` | coût à froid | Samsung 870 QVO SATA SSD | — | — |
| CPU / RAM | prototype | 24 threads ; mémoire non contrainte | — | — |
| Python 3.14 | mesures de recherche seulement | ✓ | — | — |
| Droits admin (purge du cache pour mesurer à froid) | — | ✗ | — | borne calculée (≈ 7 s) |

**Missing dependencies with no fallback :** aucune.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 + Xunit.StaFact 1.1.11 (Microsoft.NET.Test.Sdk 17.11.1), net8.0-windows |
| Config file | `tests/Chronos.Tests/Chronos.Tests.csproj` (`CheminSourcesChronos`, `CheminDocsChronos` injectés par MSBuild) |
| Quick run command | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q --filter "FullyQualifiedName~<Classe>"` |
| Full suite command | `dotnet test Chronos.sln -c Debug --nologo -v q` (**1313 / 0 en ≈ 10 s** à l'entrée, HEAD `3fb1cad`) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command (filtre) | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TOK-01 | ligne `{v, slot, model, sub, in, out, cache_w, cache_r, n}` dans les deux sens ; tolérance ; réécriture atomique triée ; `IEtatMagasin` | unit (pur + E/S temp) | `~LigneAgregatTests`, `~MagasinAgregatsTests` | ❌ Wave 0 (33-01) |
| TOK-01 | pas de somme, pas de message, pas de texte dans le fichier | unit (contenu) | `~MagasinAgregatsTests` (aucune clé hors les 9 ; aucun `msg_` ; aucun champ texte libre) | ❌ (33-01) |
| TOK-02 | lecteur en flux : offsets, pré-filtre non-autorité, `sub`, `LireUsage` | unit (fixtures réelles) | `~LecteurTranscriptTests` | ❌ (33-02) |
| TOK-02 | thread `IsBackground`/`BelowNormal`, mtime décroissant, semaine courante d'abord, annulable, `StartAsync` non bloquant, progression N / M | unit (racine temp, callbacks) | `~ReconstructionTokensTests` | ❌ (33-03) |
| TOK-02 | coût mesuré et consigné | mesure (prototype sur la vraie machine, lecture seule) | — (chiffres de cette recherche, repris dans le SUMMARY 33-05) | n/a |
| TOK-03 | curseurs : inchangé non ouvert, grandi depuis l'offset, raccourci/renommé de zéro, disparu retiré ; ligne future | unit | `~CurseursTests`, `~LecteurTranscriptTests` | ❌ (33-02) |
| TOK-03 | dédup sous curseurs : copies fork dans les deux ordres, bloc partiel, shard relu (max gagne) | unit | `~IndexMessagesTests` | ❌ (33-02) |
| TOK-03 | reprise idempotente : arrêt après k fichiers puis reprise ⇒ octets identiques | integration (temp) | `~ReconstructionTokensTests` | ❌ (33-03) |
| TOK-04 | 25 barres le 25/10/2026, 23 le 28/03/2027 ; quarts d'heure par modèle ; part sous-agents | unit (pur, tz injecté) | `~RenduLocalTokensTests` | ❌ (33-04) |
| TOK-04 | lecture par plage : mois UTC chevauchants, tri ; couverture (3 états), jamais « zéro » hors couverture | unit | `~LecteurAgregatsTests`, `~CouvertureTokensTests` | ❌ (33-04) |
| TOK-05 | aucun flottant ni membre « quota » dans `*.Historique.Tokens` ; contrôle positif ; garde textuelle ; pureté voit le sous-namespace | garde | `~GardeTokensSansPourcentageTests`, `~ServicesLayerPurityTests` | ❌ (33-01) / ✅ à étendre |
| TOK-05 | périmètre partiel dans le schéma (`Perimetre`) et la doc (§8) ; 9 champs nommés ; HYP-4 | garde documentaire | `~ContratAgregatsDocumenteTests` (+ `~ContratJournalDocumenteTests` inchangée et verte) | ❌ (33-05) / ✅ |
| intégration | hosted service avant l'orchestrateur ; mêmes instances ; diagnostic ; VM | garde DI + unit | `~CompositionRootTests`, `~DiagnosticServiceTests`, `~MainViewModelTests` | ✅ classes, ❌ cas (33-05) |
| non-régression | `GardesDedupUsageTests`, `GardesDoctrineTests`, `NormalisationUniqueTests`, `LectureSeuleAppBureauTests`, `GardesPerimetreTests` restent verts sans exemption | gardes existantes | suite complète | ✅ |

### Sampling Rate
- **Per task commit :** filtre de la classe du plan (< 5 s).
- **Per wave merge :** `dotnet test Chronos.sln -c Debug --nologo -v q` (≈ 10–12 s), deux fois de suite.
- **Phase gate :** suite complète verte deux fois, 0 avertissement (porte DoD), avant `/gsd:verify-work` ; 1313 + nouveaux (≈ +90 attendus).

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/TestData/transcripts/<cas>/…` — 10 cas réels anonymisés (§Fixtures) — 33-02
- [ ] `tests/Chronos.Tests/TestData/tokens/{tolerance, dst-2026-10-25, dst-2027-03-28, a-cheval-mois}/` — 33-01 / 33-04
- [ ] `tests/Chronos.Tests/Fakes/FakeEtatReconstruction.cs` — 33-03 (consommé par 33-05)
- [ ] Nouvelles classes : `LigneAgregatTests`, `MagasinAgregatsTests`, `GardeTokensSansPourcentageTests`, `LecteurTranscriptTests`,
      `IndexMessagesTests`, `CurseursTests`, `ReconstructionTokensTests`, `LecteurAgregatsTests`, `RenduLocalTokensTests`,
      `CouvertureTokensTests`, `ContratAgregatsDocumenteTests`
- [ ] `ServicesLayerPurityTests` : + cas « voit `…Historique.Tokens` » ; `DiagnosticServiceTests` l. 1354 : assertion à remplacer (33-05)
- Framework : rien à installer.

## Sources

### Primary (HIGH confidence)
- Code réel lu ce jour : `Services/DedupUsage.cs`, `Services/Historique/{LigneJournal, JournalReleves, LecteurJournal, BornesPlage}.cs`,
  `Models/Historique/LectureJournal.cs`, `Services/{IEtatMagasin, ChronosPaths, LastExactStore, TranscriptActivityProvider,
  TranscriptSessionSource, RefreshOrchestrator, DiagnosticService}.cs`, `App.xaml.cs` (l. 55–92, 225–240, 405–490),
  `ViewModels/MainViewModel.cs` (l. 452–484), `tests/{GardesDedupUsageTests, GardesDoctrineTests, ServicesLayerPurityTests,
  ContratJournalDocumenteTests, CompositionRootTests (l. 486–520), DiagnosticServiceTests (l. 1340–1360, 1460), LectureSeuleAppBureauTests}.cs`,
  `docs/data-sources.md` §2 et §7, `src/Chronos/Chronos.csproj`, `tests/Chronos.Tests/Chronos.Tests.csproj`.
- Mesures lecture seule sur `~/.claude/projects` (scratchpad `mesure33.py`, `mesure33b.py`, `mesure33d.py`, 2026-09-27 11:20–11:45) —
  tous les chiffres du §Mesures.
- Prototype `proto33` (C#, net8.0 Release, scratchpad) — coût de la passe, mémoire, tailles des fichiers produits (sous le scratchpad).
- PowerShell `[TimeZoneInfo]` « Romance Standard Time » — transitions 2026-10-25, 2027-03-28 ; 2027-03-29 = lundi, +02:00.
- `Get-PhysicalDisk` — `C:` = Samsung 870 QVO (SATA SSD) ; `dotnet --list-sdks` 10.0.201 ; `shared/Microsoft.NETCore.App/` sans `System.IO.Pipelines.dll`.
- Context7 `/dotnet/docs` — « SynchronizationContext and console apps » : sans contexte, les continuations d'`await Task.Yield()` s'exécutent
  sur le pool (Pitfall 1).
- SUMMARY 32-01, 32-02, 32-04, 32-06, 32-07 et 32-RESEARCH (§CPT-01) — livrables et décisions D-32-01…D-32-35.

### Secondary (MEDIUM confidence)
- `JsonDocument.Parse(ReadOnlyMemory<byte>)` ne copie pas l'entrée et exige qu'elle reste stable pendant la vie du document —
  connaissance de la doc officielle, confirmée empiriquement par le prototype (0 ligne ignorée, totaux identiques à Python), non
  retrouvée via Context7 (requête sans résultat).
- `cleanupPeriodDays` par défaut 30 j (doc Claude Code, connaissance d'entraînement) ; absent de `~/.claude/settings.json` (vérifié).

### Tertiary (LOW confidence)
- Coût « à froid » ≈ 7 s : calcul (2,08 Go / ~500 Mo/s SATA + CPU), non mesuré.
- Mécanisme exact de purge de Claude Code (par mtime ? par session ?) : non vérifié ; d'où la couverture par intervalles + HYP-4.

## Metadata

**Confidence breakdown :**
- Mesures (données, prototype, DST) : HIGH — reproduites deux fois, scripts conservés dans le scratchpad de session.
- Standard stack : HIGH — rien à installer, tout est dans le dépôt ou la BCL.
- Architecture (index d'ids + projection, thread dédié, couverture par intervalles) : MEDIUM-HIGH — cohérente avec toutes les
  exigences et mesures ; forme laissée à discrétion (nommage, constantes).
- Pitfalls : HIGH — chacun vérifié (doc officielle, code réel, ou fuseau réel).

**Research date :** 2026-09-27
**Valid until :** 2026-10-27 (stable : BCL .NET 8 ; à revalider si le schéma des transcripts Claude Code change — `version` 2.1.281 observée)
