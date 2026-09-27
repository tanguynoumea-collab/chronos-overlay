---
phase: 33-agr-gats-de-tokens
plan: 05
subsystem: composition racine (DI) / diagnostic / ViewModel / documentation — câblage des agrégats de tokens, garde documentaire du §8
tags: [TOK-05, TOK-02, cablage-DI, IHostedService, ordre-des-hosted-services, arret-propre, IEtatReconstruction, DiagnosticService, MagasinAgregats, NomsMagasins, Perimetre, MainViewModel, TexteReconstruction, data-sources-§8, ContratAgregatsDocumenteTests, garde-documentaire, HYP-4, mesures-reelles]

sha_entree_de_plan: 41c18b6
one_liner: "Chronos reconstruit et tient à jour ses agrégats de tokens dès son lancement (ReconstructionTokens hébergé AVANT RefreshOrchestrator, même instance qu'IEtatReconstruction, MagasinAgregats et IndexMessages singletons sur HistoriqueDir, arrêt propre en < 2 s prouvé), le dit au diagnostic (troisième magasin réel + sous-section [Agrégats de tokens] : phase, N / M fichiers, dernier fichier, disparus / ignorées / ids, ÉCHEC, et le PÉRIMÈTRE mot pour mot) et au ViewModel (TexteReconstruction / AfficherReconstruction relus au tick, sans XAML), et l'écrit dans docs/data-sources.md §8 (126 lignes) sous la nouvelle garde à deux sens ContratAgregatsDocumenteTests — champs ↔ DTO privés ↔ texte, bornes du code, HYP-4, renvoi du §7 — avec les mesures du service réel sur la vraie machine (3,2 s chaud / 15 s froid, 47 ms incrémental, 28,8 Mo d'ids). Suite 1404 → 1415, 0 échec deux fois, 0 warning."

# Dependency graph
requires:
  - phase: 33-01 (TOK-01)
    provides: MagasinAgregats (IEtatMagasin, NomFichier, RetentionMois), LigneAgregat.{Champs, Perimetre, LigneAgregatDto}, CouvertureTokens.{HorizonPurge, NomFichier}
  - phase: 33-02 (TOK-03)
    provides: IndexMessages.{Champs, HorizonIndex, RetentionIndexMois, LigneIdDto}, Curseurs.NomFichier, LecteurTranscript.ToleranceFutur
  - phase: 33-03 (TOK-02)
    provides: ReconstructionTokens (BackgroundService + thread BelowNormal, ExecuterUnePasse, CadenceIncrementale), IEtatReconstruction, PhaseReconstruction, FakeEtatReconstruction
  - phase: 33-04 (TOK-04)
    provides: LecteurAgregats / RenduLocalTokens (rien à câbler : statiques purs) ; états « hors couverture / transcripts absents / couverte » cités au §8
  - phase: 32-05 / 32-02 (JRN-04, CPT-02)
    provides: motif « hosted service AVANT l'orchestrateur », DiagnosticService(magasins), LigneMagasin, NomsMagasins.AgregatsTokens, MajTexteEtatJournal au tick (D-32-21)
  - phase: 32-07 (JRN-06)
    provides: ContratJournalDocumenteTests (moule de la garde documentaire à deux sens, D-32-31), §7 clos par « --- »
provides:
  - "App.xaml.cs — MagasinAgregats, IndexMessages, ReconstructionTokens singletons (dossier = HistoriqueDir, racine = ProjectsRoot en lecture seule) ; IEtatReconstruction = même instance ; AddHostedService(ReconstructionTokens) AVANT RefreshOrchestrator ; DiagnosticService avec les TROIS magasins réels + reconstruction: ; MainViewModel reçoit IEtatReconstruction par le conteneur"
  - "DiagnosticService — paramètre optionnel IEtatReconstruction? reconstruction (dernière position) ; ligne de magasin « agrégats de tokens » (fichier du mois UTC, motif LigneMagasin) ; sous-section Reconstruction / Fichiers disparus / ÉCHEC ; « Périmètre : » = LigneAgregat.Perimetre à chaque rapport ; LibellePhase"
  - "MainViewModel — IEtatReconstruction? reconstruction = null (dernière position) ; TexteReconstruction / AfficherReconstruction ; MajTexteReconstruction() au ctor et au tick ; aucun abonnement à Changement (la coalescence est le tick)"
  - "docs/data-sources.md §8 « Agrégats de tokens » (126 lignes) + renvoi du §7 + ligne finale ; tests/Chronos.Tests/ContratAgregatsDocumenteTests.cs (5 tests)"
  - "Mesures du service réel sur la vraie machine (harnais hors arbre, lecture seule) consignées au §8 et ici"
affects: [34 (bandeau F2 depuis IEtatReconstruction ; pistes Tokens depuis LecteurAgregats / RenduLocalTokens ; le VM du cadran n'a que deux propriétés), 35 (constat : quatre fichiers sous %APPDATA%\Chronos\historique\ au premier lancement ; NomsMagasins.AgregatsTokens dans la carte des réglages), release 3.3.0 (fin de milestone)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Le conteneur injecte les paramètres optionnels du VM (AddSingleton<MainViewModel>() sans fabrique) : IEtatJournal (32-05) puis IEtatReconstruction (33-05) — prouvé dans le conteneur miroir par un faux en phase Reconstruction qui fait apparaître le texte"
    - "Sous-section de diagnostic en ENTIERS sous une ligne de magasin (même moule que l'ALERTE du journal) ; la phrase de périmètre vit dans le code (LigneAgregat.Perimetre) et le rapport la cite — la doc la cite aussi, la garde compare les trois"
    - "Garde documentaire SŒUR : une classe par section, même moule (CheminDocs, LireDocument, SectionDe coupée au dernier « --- ») ; la nouvelle section s'insère après le « --- » de la précédente et la garde de la précédente ne bouge pas d'une ligne"
    - "Mesure de coût hors suite de tests : harnais console jetable sous le scratchpad référençant Chronos.csproj, ChronosPaths(<scratch>/usage.json, <ProjectsRoot réel>), ExecuterUnePasse × 2, Stopwatch + TotalProcessorTime + PeakWorkingSet64 ; HistoriqueDir sous le scratchpad, nettoyé après"

key-files:
  created:
    - tests/Chronos.Tests/ContratAgregatsDocumenteTests.cs
    - .planning/phases/33-agr-gats-de-tokens/33-05-SUMMARY.md
  modified:
    - src/Chronos/App.xaml.cs
    - src/Chronos/Services/DiagnosticService.cs
    - src/Chronos/ViewModels/MainViewModel.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - tests/Chronos.Tests/MainViewModelTests.cs
    - docs/data-sources.md
    - .planning/REQUIREMENTS.md

key-decisions:
  - "D-33-22 — l'exposition au VM du cadran se limite à deux propriétés observables (TexteReconstruction, AfficherReconstruction) relues au tick ; la fenêtre Historique (34) consommera IEtatReconstruction directement pour le bandeau F2 et sa barre ; aucun abonnement à Changement dans le VM du cadran (levé sur le thread de fond ≈ une fois par fichier : la coalescence est le tick, la frontière de thread reste unique)"
  - "D-33-23 — le diagnostic dit le périmètre à chaque rapport, avec ou sans état de reconstruction injecté : « Périmètre : Claude Code seulement — hors Cowork et claude.ai ; bruts, non pondérés » vit en UN endroit du code (LigneAgregat.Perimetre), cité par la doc et par le rapport"
  - "D-33-24 — garde documentaire à deux sens pour le §8 (motif D-32-31) : LigneAgregat.Champs et IndexMessages.Champs comparés aux JsonPropertyName des DTO privés LigneAgregatDto / LigneIdDto ET au texte ; HorizonIndex, RetentionIndexMois, HorizonPurge, RetentionMois, CadenceIncrementale comparés aux chiffres du texte ; les quatre fichiers nommés entre accents graves"
  - "D-33-25 — HYP-4 s'écrit comme une hypothèse : cleanupPeriodDays = 30 j par défaut (non défini dans ~/.claude/settings.json sur cette machine, non lu par Chronos en v1.8) ; si Claude Code purge plus tôt, des tranches « couvertes » pourraient manquer — à vérifier après un mois (plus vieux mtime vs début du dernier intervalle garanti)"
  - "Exécution — MainViewModel reste inscrit par AddSingleton<MainViewModel>() : le conteneur injecte IEtatReconstruction (paramètre optionnel, même mécanisme qu'IEtatJournal en 32-05) ; pas de fabrique à 15 arguments pour un argument nommé ; documenté dans App.xaml.cs et PROUVÉ dans le conteneur miroir (insertion seule dans Host_resout_et_dispose_les_singletons)"
  - "Exécution — dans le §8, les quatre compteurs de la ligne d'index sont décrits par renvoi à l'agrégat (« mêmes noms, mêmes unités ») sans accents graves : c'est ce qui rend la mutation h1 (ligne `cache_w` retirée du tableau des agrégats) discriminante"
  - "Exécution — le service réel coûte plus que le prototype de recherche (3,2 s contre 2,5 s à chaud ; 3,6 s CPU contre 2,7 s) : flush par lot, shards d'ids, reprojection — accepté, c'est le prix de la reprise idempotente ; l'estimation « 10–20 s CPU » reste périmée"

patterns-established:
  - "Mutation sur un fichier de PRODUCTION dont l'ordre n'est gardé que par un miroir de test : la mutation (d1) est jouée sur le miroir ConteneurAvecTokens (rouge) ; l'ordre réel des deux AddHostedService dans App.xaml.cs n'a PAS de garde textuelle propre (celle de 32-05 ne garde que le journal) — noté ci-dessous comme dette mineure"

requirements-completed: [TOK-05]

# Metrics
duration: ≈ 19 min (11:23:28Z → 11:42Z)
completed: 2026-09-27
---

# Phase 33 Plan 05 : Brancher, montrer, documenter, garder — Summary

**Tout ce que 33-01…33-04 ont construit en isolation entre dans le graphe DI réel avec son arrêt propre (`ReconstructionTokens`
hébergé AVANT `RefreshOrchestrator`, `IEtatReconstruction` = la même instance, `MagasinAgregats` et `IndexMessages` singletons sur
`HistoriqueDir`, `StopAsync` < 2 s prouvé sur racine vide) ; le diagnostic remplace « aucun (phase 33) » par un troisième magasin
réel et une sous-section en entiers — phase, N / M fichiers, semaine courante, dernier fichier, disparus / ignorées / ids, ÉCHEC — et
dit le PÉRIMÈTRE mot pour mot à chaque rapport ; le `MainViewModel` sait dire « reconstruction des tokens — 886 / 1603 fichiers · la
semaine courante est déjà complète » et se tait en incrémental, sans un pixel de XAML ; `docs/data-sources.md` gagne un §8 de 126
lignes sous la nouvelle garde `ContratAgregatsDocumenteTests` (champs ↔ DTO ↔ texte, bornes du code, HYP-4, renvoi du §7), la garde
du §7 intacte ; et le coût a été mesuré avec le SERVICE RÉEL sur la vraie machine, en lecture seule.**

## Performance

- **SHA d'entrée :** `41c18b6` — suite **1404 / 0**, 0 warning.
- **Début :** 2026-09-27T11:23:28Z. **Fin des commits de code :** 11:36Z (suite complète × 2 : 11:36:42Z, 11:36:57Z). **Durée :** ≈ 19 min.
- **Tâches :** 3 / 3 (TDD : 3 RED + 3 GREEN ; 7 mutations jouées et révoquées).
- **Fichiers :** 7 de `files_modified` (tous) + REQUIREMENTS.md + ce SUMMARY. `ROADMAP.md`, `STATE.md`, `ContratJournalDocumenteTests.cs`,
  `docs/publish.md` et les fichiers de 33-01…33-04 : **diff vide** contre `41c18b6`.
- **Seul sur l'arbre (vague 3)** : commits normaux, stage explicite, jamais `--amend`.

## Task Commits

| Tâche | RED | GREEN |
|---|---|---|
| 1 — Câblage DI + diagnostic `[Agrégats de tokens]` | `0f2c8ed` (test : +2 `CompositionRootTests`, +2 `DiagnosticServiceTests`, 1 assertion modifiée) — rouge par compilation CS1739 (`reconstruction` inconnu) | `4429077` (feat : `App.xaml.cs`, `DiagnosticService.cs`) — filtre 93 / 93 |
| 2 — `MainViewModel` sans XAML | `037d5d8` (test : +2 `MainViewModelTests`, `Build()` étendu par argument nommé) — rouge CS1739 / CS1061 | `7c1c5c2` (feat : `MainViewModel.cs`, commentaire `App.xaml.cs`, preuve d'injection dans le miroir) — filtre 69 / 69 |
| 3 — §8 + garde documentaire | `72ec927` (test : +5 `ContratAgregatsDocumenteTests`) — **5 / 5 rouges** « Section « ## 8. Agrégats de tokens » introuvable », 22 voisins verts | `c1ff731` (docs : `data-sources.md`) — filtre 27 / 27 |

**Plan metadata :** commit final `docs(33-05)` (SUMMARY + REQUIREMENTS.md).

## Câblage exact livré (pour la phase 34 et le constat 35)

```csharp
// App.xaml.cs — juste après AddHostedService(JournalisationUsageProvider), AVANT AddSingleton<RefreshOrchestrator>() (l. 445-451)
services.AddSingleton(sp => new MagasinAgregats(sp.GetRequiredService<ChronosPaths>().HistoriqueDir, sp.GetRequiredService<IClock>()));
services.AddSingleton(sp => new IndexMessages(sp.GetRequiredService<ChronosPaths>().HistoriqueDir, sp.GetRequiredService<IClock>()));
services.AddSingleton(sp => new ReconstructionTokens(sp.GetRequiredService<ChronosPaths>(), sp.GetRequiredService<MagasinAgregats>(),
                                                     sp.GetRequiredService<IndexMessages>(), sp.GetRequiredService<IClock>()));
services.AddSingleton<IEtatReconstruction>(sp => sp.GetRequiredService<ReconstructionTokens>());
services.AddHostedService(sp => sp.GetRequiredService<ReconstructionTokens>());          // l. 451 < l. 481 (RefreshOrchestrator)
// DiagnosticService : magasins: { LastExactStore, JournalReleves, MagasinAgregats }, reconstruction: sp.GetRequiredService<IEtatReconstruction>()
// MainViewModel : AddSingleton<MainViewModel>() — le conteneur injecte IEtatJournal et IEtatReconstruction (paramètres optionnels)
```

- **Phase 34, bandeau F2** : consommer `IEtatReconstruction` (singleton DI) directement — `Phase`, `FichiersTraites` / `FichiersTotal`
  (barre), `SemaineCouranteDisponible`, `DernierFichier` (relatif), `DerniereErreur`, `DerniereReconstructionTerminee` ; `Changement`
  est levé SUR LE THREAD DE FOND ≈ une fois par fichier : marshaller par `IUiDispatcher.Post` ET coalescer (ou relire au tick, comme le VM du cadran).
- **Phase 34, pistes Tokens** : `LecteurAgregats.Lire(magasin.Dossier, de, a)` + `RenduLocalTokens.{ParHeure, ParQuartDHeure, PartSousAgents}` (33-04) —
  le dossier est `sp.GetRequiredService<MagasinAgregats>().Dossier` (= `ChronosPaths.HistoriqueDir`) ; `CouvertureTokens.Charger(Path.Combine(dossier, CouvertureTokens.NomFichier))`.
- **Phase 35, carte des réglages** : `NomsMagasins.AgregatsTokens` (« agrégats de tokens ») ; le VM du cadran expose déjà `TexteReconstruction` / `AfficherReconstruction`.
- **Phase 35, constat par sonde hors arbre** — les quatre familles de fichiers que la 3.3.0 créera sous `%APPDATA%\Chronos\historique\` au premier lancement
  (mesuré ici sur un dossier scratchpad avec les vrais transcripts) : `tokens-2026-0{6,7,8,9}.jsonl` (≈ 10 / 68 / 161 / **331 Ko**),
  `ids-2026-0{6,7,8,9}.jsonl` (≈ 0,2 / 2,6 / 6,4 / **19,6 Mo**), `curseurs.json` (≈ 356 Ko), `couverture.json` (173 o) — à côté de `releves-2026-09.jsonl`.
  Rappel des deux vues d'AppData : un overlay lancé depuis un terminal de l'app écrirait la vue virtualisée.

## Ce que dit le diagnostic désormais (`[Magasins persistants]`)

```
  agrégats de tokens : C:\…\historique\tokens-2026-09.jsonl — dernière écriture il y a 0 min (331084 o)
    Reconstruction : à jour (incrémental) — 1611 / 1611 fichiers · semaine courante : complète · dernier fichier : proj/…/subagents/agent-x.jsonl
    Fichiers disparus : 0 · lignes ignorées : 0 · ids connus : 118641
    Périmètre : Claude Code seulement — hors Cowork et claude.ai ; bruts, non pondérés
```
Libellés de phase : « jamais lancée », « reconstruction en cours », « à jour (incrémental) », « arrêtée », « EN ÉCHEC » (+ ligne `ÉCHEC : cause`).
L'ordre « lignes de processus < ligne des agrégats » (test de 32-05) est conservé ; ni `%` ni « pour cent » dans la sous-section (assertion).

## Mesures consignées (TOK-02 « coût mesuré et consigné »)

| Mesure | Prototype de recherche (33-RESEARCH, cache chaud) | **Service réel `ReconstructionTokens`** (harnais hors arbre, 11:40Z, thread `BelowNormal`) |
|---|---|---|
| Transcripts | 1 603 fichiers / 2,08 Go / 238 857 `assistant` / 118 401 ids | 1 611 fichiers / 118 641 ids (8 fichiers de plus depuis la recherche) |
| Passe complète mur / CPU | 2,4–2,6 s / 2,7 s (788–800 Mo/s) | **3 222 et 3 252 ms / 3 562 et 3 578 ms** à chaud (runs 2 et 3) ; **15 007 ms / 3 750 ms** au run 1 (cache froid, SATA 870 QVO) |
| Semaine courante | 1,7 s (886 fichiers) | non chronométrée séparément (`SemaineCouranteDisponible == true` en fin de passe) |
| Cycle incrémental (rien n'a bougé) | 63 ms (+ 93 ms d'inventaire) | **47–48 ms**, 0 fichier ouvert, CPU 47 ms |
| Mémoire | pic +90 Mo, tas 31 Mo | working set 27 → 114–146 Mo, pic 120–176 Mo ; tas GC 40–51 Mo |
| Agrégats | 3 886 tuples = 524 Ko | **3 853 tuples** (3 886 − 34 `<synthetic>` exclus, D-33-10 ; ± sans-id) = 570 214 o |
| Ids | ≈ 11 Mo/mois estimés | 170 234 lignes = **28,8 Mo** (juin 215 Ko, juillet 2,6 Mo, août 6,4 Mo, **septembre 19,6 Mo**) — les blocs partiels écrivent plusieurs lignes par id |
| `curseurs.json` / `couverture.json` | 351 816 o | 356 290 o / 173 o |

Conditions : harnais `net8.0-windows` référençant `src/Chronos/Chronos.csproj`, `ChronosPaths(<scratch>/usage.json, %USERPROFILE%\.claude\projects)`,
`ExecuterUnePasse(None)` × 2 par run, 3 runs sur trois dossiers `appdata*` neufs sous le scratchpad ; **aucune écriture sous `~/.claude`,
`%APPDATA%\Claude` ni `%APPDATA%\Chronos`** (vérifié : aucun `tokens-*` / `ids-*` / `curseurs.json` sous `%APPDATA%\Chronos\historique`) ;
aucun overlay lancé ; dossiers de mesure et `bin/obj` du harnais supprimés (source conservée dans le scratchpad).
Lecture : le premier lancement à froid peut prendre ≈ 15 s sur ce disque — l'UI n'attend pas (thread `BelowNormal`, `StartAsync` immédiat) ;
ensuite ≈ 50 ms par minute. La croissance des shards d'ids (≈ 20 Mo pour un mois intensif, rétention 3 mois) est la dette à surveiller.

## Mutations (sauvegarde par copie, révocation par copie, sha256 identique)

| Mutation | Fichier | Rouges nommés | sha256 (avant = après) |
|---|---|---|---|
| (d1) `AddHostedService(ReconstructionTokens)` déplacé APRÈS celui de l'orchestrateur dans le miroir `ConteneurAvecTokens` | CompositionRootTests.cs | `La_reconstruction_des_tokens_est_un_service_heberge_avant_l_orchestrateur_et_la_meme_instance_que_son_etat` (1/9) | `dd68d3430bc2c4af` |
| (d2) ligne `Périmètre : ` retirée du diagnostic | DiagnosticService.cs | `La_section_des_agregats_dit_la_progression_le_dernier_fichier_et_le_perimetre` **et** `Sans_etat_de_reconstruction_la_ligne_des_agregats_dit_le_dossier_ou_le_fichier_du_mois` (2/57 — le périmètre se dit aussi sans état) | `f8a75f3f9cc882f4` |
| (v1) `case Incremental` traité comme `Reconstruction` | MainViewModel.cs | `Le_texte_de_reconstruction_suit_la_progression_au_tick_et_disparait_en_incremental` (1/53) | `1a81067c32378986` |
| (h1) ligne `| \`cache_w\` |` retirée du tableau des agrégats | data-sources.md | `Le_document_nomme_chaque_champ_reel_des_agregats_et_de_l_index` (1/5) | `747f5a5c67374745` |
| (h2) « hors Cowork » → « hors Cowork et Bedrock » dans le DOCUMENT | data-sources.md | `Le_document_ecrit_le_perimetre_mot_pour_mot_et_jamais_un_pourcentage` (1/5) | identique |
| (h3) `HYP-4` → `HYP-5` (2 occurrences) | data-sources.md | `Le_document_ecrit_HYP_4_et_le_paragraphe_7_renvoie_au_8` (1/5) | identique |
| (h4) CODE : `IndexMessages.HorizonIndex` 45 → 40 j | IndexMessages.cs | `Le_document_porte_les_bornes_du_code` **et** `IndexMessagesTests.Charger_ne_lit_que_les_mois_ouverts_de_l_horizon` (2/15) | `9a0cd72a6c92f1e5` (= le sha consigné par 33-02) |

## Totaux

| Étape | Résultat |
|---|---|
| Entrée `41c18b6` | 1404 / 0, 0 warning |
| Task 1 GREEN — `CompositionRootTests|DiagnosticServiceTests|GardesPerimetreTests|LectureSeuleAppBureauTests` | 93 / 93 |
| Task 2 GREEN — `MainViewModelTests|ReglagesBindingTests|CompositionRootTests` | 69 / 69 ; `GardesPerimetreTests` 30 / 30 après la preuve d'injection |
| Task 3 GREEN — `ContratAgregatsDocumenteTests|ContratJournalDocumenteTests|ContratHooksDocumenteTests` | 27 / 27 (5 + 4 + 18) |
| **Fin de plan, run 1** (`dotnet test Chronos.sln -c Debug --nologo -v q`, 11:36:42Z) | **1415 / 1415, 0 échec**, 10 s |
| **Fin de plan, run 2** (11:36:57Z) | **1415 / 1415, 0 échec**, 10 s |
| `dotnet build Chronos.sln -c Release --nologo` | **0 warning** ; Debug : 0 warning |

Ce plan : **+11 tests** (2 + 2 + 2 + 5) — le plan en annonçait ≈ 11. Total de phase : **1313 → 1415** (+102 ; le plan estimait ≈ +95).
Gardes vertes sans exemption nouvelle : `GardeTokensSansPourcentageTests`, `ServicesLayerPurityTests`, `GardesDedupUsageTests`, `GardesDoctrineTests`,
`NormalisationUniqueTests`, `LectureSeuleAppBureauTests`, `GardesPerimetreTests`, `ContratJournalDocumenteTests`, `ContratHooksDocumenteTests`, `ContratAgregatsDocumenteTests`.

## Critères grep (relevés)

| Critère | Attendu | Mesuré |
|---|---|---|
| `services.AddHostedService(sp => sp.GetRequiredService<ReconstructionTokens>())` / `AddSingleton<IEtatReconstruction>` (App.xaml.cs) | 1 / 1 | **1 / 1** ; ligne 451 < 481 (`RefreshOrchestrator`) |
| `GetRequiredService<MagasinAgregats>()` (App.xaml.cs) | ≥ 2 | **2** |
| `IEtatReconstruction? reconstruction = null` / `LigneAgregat.Perimetre` / `aucun (phase 33)` / `MagasinAgregats.NomFichier(` / `FichiersTraites` (DiagnosticService.cs) | 1 / 1 / 0 / 1 / ≥ 1 | **1 / 1 / 0 / 1 / 1** ; CRLF conservé |
| `aucun (phase 33)` (DiagnosticServiceTests.cs) ; `git diff --numstat` | 0 ; ≤ 1 suppression | **0 ; 91 + / 1 −** |
| `[Fact]` CompositionRootTests / MainViewModelTests / ContratAgregatsDocumenteTests | 7+2 / 52+2 / 5 | **9 / 54 / 5** |
| `IEtatReconstruction? reconstruction = null` / `MajTexteReconstruction()` / `reconstruction des tokens — ` / `la semaine courante est déjà complète` (MainViewModel.cs) | 1 / ≥ 3 / 1 / 1 | **1 / 3 / 1 / 1** |
| `\(double\)|\* 100|Percent` (MainViewModel.cs) | aucune occurrence nouvelle | **3 avant, 3 après** |
| `git diff --stat -- src/Chronos/Views src/Chronos/*.xaml src/Chronos/Resources` | vide | **vide** |
| §8 / §7 / « arrivent en phase 33 » / HYP-4 / cleanupPeriodDays / périmètre / champs entre accents graves / « 45 j » / « 28/03/2027 » / CR (data-sources.md) | 1 / 1 / 0 / ≥ 1 / ≥ 1 / ≥ 1 / ≥ 11 / ≥ 1 / ≥ 1 / 0 | **1 / 1 / 0 / 2 / 1 / 1 / 17 / 1 / 1 / 0** (LF, sans BOM) |
| `"LigneAgregatDto"` / `"LigneIdDto"` (garde) | 1 / 1 | **1 / 1** |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocage de compilation] Variable de motif `r` déjà utilisée dans `BuildReportAsync`**
- **Found during:** Task 1 GREEN (CS0136 aux l. 254 et 576 : `r` existe dans une portée englobante de cette très longue méthode)
- **Fix:** `_reconstruction is { } rec`, `DernierFichier is { } dernier`, `DerniereErreur is { } erreurRec` — même sémantique que le plan.
- **Files modified:** `src/Chronos/Services/DiagnosticService.cs` — **Commit:** `4429077`

**2. [Critère d'acceptation] « la semaine courante est déjà complète » apparaissait 2 fois dans `MainViewModel.cs`**
- Le XML-doc de `TexteReconstruction` reprenait le libellé ; reformulé (doctrine « jamais le libellé dans un commentaire du même fichier »). `grep -c` = 1. Intégré à `7c1c5c2`.

### Écarts assumés (nommés)

- **Pitfall 4 — 1 assertion existante modifiée** (`DiagnosticServiceTests.La_section_Magasins_persistants_dit_la_vue_et_les_trois_magasins`, l. 1354) :
  `Assert.Contains("agrégats de tokens : aucun (phase 33)", report)` → `Assert.Contains("aucune écriture (dossier absent)", Ligne(report, "agrégats de tokens : "))`.
  Une seule suppression dans le fichier ; le test d'ordre « processus < agrégats » (l. ~1460) est inchangé et vert.
- **`reconstruction: sp.GetRequiredService<IEtatReconstruction>()` sur le `MainViewModel` : NON écrit dans `App.xaml.cs`** (critère `grep -c` = 1 non tenu à la lettre).
  Le VM y est inscrit par `services.AddSingleton<MainViewModel>()` sans fabrique — c'était déjà le cas pour `IEtatJournal` en 32-05 : le conteneur injecte
  les paramètres optionnels enregistrés. Réécrire l'inscription en fabrique à 15 arguments pour un seul argument nommé aurait été un changement
  plus lourd que l'intention du plan. À la place : un commentaire à l'inscription du VM dans `App.xaml.cs`, et une **preuve** dans le conteneur miroir
  `Host_resout_et_dispose_les_singletons` (insertion seule : un `FakeEtatReconstruction` en phase Reconstruction enregistré comme `IEtatReconstruction`
  → `vm.AfficherReconstruction == true`, texte « reconstruction des tokens — 0 / 3 fichiers »). Le `reconstruction:` du diagnostic, lui, est écrit.
- **`Sans_etat_de_reconstruction_…` : « dernière écriture il y a 0 min » n'existe pas** — `LibelleSource.Anciennete` dit « à l'instant » sous une minute ;
  le test assert « dernière écriture à l'instant », le nom du fichier du mois et « o) », et couvre en plus le cas « dossier présent, fichier absent »
  (« aucune écriture (fichier du mois absent) »). Le troisième cas ne contient pas le libellé « fichier du mois » quand le fichier EXISTE : c'est le
  comportement du motif `LigneMagasin` (la nature n'est dite que pour un fichier absent), conservé tel quel.
- **(d2) rougit 2 tests, pas 1** : le périmètre se dit aussi sans état injecté (D-33-23), donc le test « sans état » le vérifie aussi.
- **(d1) jouée sur le miroir seulement** : l'ordre réel des deux `AddHostedService` dans `App.xaml.cs` n'a pas de garde textuelle propre (celle de 32-05,
  `Le_journal_enveloppe_le_composite…`, ne garde que le journal). Une mutation d'ordre dans `App.xaml.cs` seul resterait verte — **dette mineure**, notée
  ci-dessous ; le plan prescrivait explicitement de jouer d1 dans le helper.
- **Ligne finale du document réordonnée** : « §8 » devait être sur la DERNIÈRE ligne physique (assertion) ; la phrase finale sur deux lignes a été réagencée
  (« …(schéma = API privée de facto) ; / complété le 2026-09-27 (§7, note CPT-01 du §2 ; §8 agrégats de tokens).* »).
- **Mesure réelle faite** (option recommandée du plan) : chiffres du service réel consignés au §8 ET ici, à côté de ceux de la recherche ; le service réel
  est plus lent que le prototype (3,2 s vs 2,5 s à chaud) — dit tel quel, pas lissé.

### Mises à jour d'état

`STATE.md` et `ROADMAP.md` **non modifiés** (consigne de l'orchestrateur, vague 3) : `state advance-plan`, `state update-progress`, `state record-metric`,
`roadmap update-plan-progress` laissés à l'orchestrateur. `requirements mark-complete TOK-05` exécuté (case l. 69 + traçabilité l. 182, 2 lignes).

## Issues Encountered

- `git config core.autocrlf = true` : avertissements « LF will be replaced by CRLF » attendus sur les fichiers LF ; `App.xaml.cs`, `DiagnosticService.cs`,
  `CompositionRootTests.cs`, `DiagnosticServiceTests.cs` sont CRLF et ont été édités par script Python (`newline=""`) pour le rester ; révocations par copie, jamais `git checkout --`.
- `grep -c $'\r'` sous Git Bash compte TOUTES les lignes (motif vide) : le contrôle CR a été refait par `grep -cP '\r'` et Python (0 CR).
- Le premier run du harnais (15 s) est probablement à cache froid — les runs 2 et 3 concordent (3,2 s) ; rien ne prouve que le run 1 était strictement froid
  (pas de purge de cache sans droit admin) : consigné comme « premier run » plutôt que comme mesure à froid.

## Known Stubs

Aucun. `TexteReconstruction = ""` / `AfficherReconstruction = false` sans état injecté ou en incrémental est un comportement voulu et testé (« se tait quand
tout est à jour »), pas un stub ; aucune UI n'est branchée dans ce plan (bandeau F2 en phase 34).

## Deferred Issues

- **Garde textuelle de l'ordre des hosted services des agrégats dans `App.xaml.cs`** (voir d1) : une insertion dans `GardesPerimetreTests` du motif de 32-05
  (`hebergeReconstruction < hebergeOrchestrateur`, `Assert.Single`) fermerait le trou — hors `files_modified` de ce plan.
- **Croissance des shards d'ids** : 19,6 Mo pour septembre (mois intensif) contre ≈ 11 Mo estimés ; rétention 3 mois → ≤ 60 Mo disque, ≈ 118 k ids en mémoire
  (tas 40–50 Mo). À surveiller au diagnostic (« ids connus ») ; une compaction des shards (max par id) serait un plan de v1.9.
- **Premier lancement à froid ≈ 15 s** sur SATA : sans conséquence pour l'UI (thread de fond), mais la phase 35 devrait le constater et le dire dans le bandeau F2.

## Next Phase Readiness

- **Phase 34** : `IEtatReconstruction` (bandeau F2, barre N / M), `LecteurAgregats.Lire` + `RenduLocalTokens` (pistes Tokens), `MagasinAgregats.Dossier` (dossier des fichiers) —
  tout est résolu par DI ; aucune retouche de service attendue. Le VM du cadran n'expose que `TexteReconstruction` / `AfficherReconstruction` (D-33-22).
- **Phase 35** : constat par sonde hors arbre des quatre familles de fichiers sous `%APPDATA%\Chronos\historique\` ; le rapport de diagnostic dit la phase,
  N / M et le périmètre ; `NomsMagasins.AgregatsTokens` pour la carte des réglages.
- **Release 3.3.0** : `docs/publish.md` inchangé (procédure de 31-02 / 32-07) ; l'exe grossira du quartier `Tokens` ; à faire en fin de milestone, pas ici.

---
*Phase: 33-agr-gats-de-tokens*
*Completed: 2026-09-27*

## Self-Check: PASSED

- 9 fichiers présents (3 sources, 4 tests dont 1 créé, docs/data-sources.md, ce SUMMARY) ; 6 commits de tâche retrouvés dans `git log` (0f2c8ed, 4429077, 037d5d8, 7c1c5c2, 72ec927, c1ff731).
- `git diff --stat 41c18b6` VIDE sur `ROADMAP.md`, `STATE.md`, `ContratJournalDocumenteTests.cs`, `docs/publish.md` ; aucun fichier de 33-01…33-04 touché ; aucun overlay lancé ; rien écrit sous `~/.claude`, `%APPDATA%\Claude`, `%APPDATA%\Chronos`.
