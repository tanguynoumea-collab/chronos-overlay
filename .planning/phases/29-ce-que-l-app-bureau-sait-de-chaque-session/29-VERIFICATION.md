---
phase: 29-ce-que-l-app-bureau-sait-de-chaque-session
verified: 2026-09-26T00:00:00Z
status: passed
score: 6/6 critères ROADMAP vérifiés — 6/6 exigences (APP-01..06) satisfaites
sha_verifie: 1a25fdf
sha_fin_de_phase_29: 6f26972
sha_entree_de_phase: 10a821f
suite:
  total: 1062
  echecs: 0
  executions: 3
  duree: "15 s / 13 s / 9 s"
mutations_rejouees:
  - id: "écriture ajoutée dans LecteurAppBureau.cs (méthode morte jamais appelée, File.WriteAllText)"
    attendu: "Le_lecteur_ne_contient_aucun_appel_d_ecriture_et_ouvre_en_partage_complet rouge (garde au texte, jeton « File.Write »)"
    mesure: "échec : 2, réussite : 4, total : 6 sur LectureSeuleAppBureauTests — exactement le test nommé. Révoquée : restauration à partir du blob HEAD (git show), sha256 f19cf4c328abaaac36d3bd9dea5d0fdca0d85ca91bd5df7dffa1aba39129b082 identique avant/après (confirmé aussi par `git hash-object` = blob de l'index, et `cmp` octet pour octet identique à `git show HEAD:...`), git diff --stat et git diff --exit-code vides"
  - id: "commentaire « claude-code-sessions » ajouté juste avant DecrireSourceAppBureau dans DiagnosticService.cs"
    attendu: "Le_chemin_de_l_app_bureau_n_existe_que_dans_le_resolveur_de_racines rouge (porteurs : Services/DiagnosticService.cs, Services/RacinesEtat.cs)"
    mesure: "échec : 2, réussite : 4, total : 6 sur LectureSeuleAppBureauTests (les deux mutations jouées ensemble, un seul run) — exactement le test nommé, identique au précédent de 29-05-SUMMARY (mutation (c), 1 rouge nommé). Révoquée : git checkout --, sha256 3b87c84ebcbf91de9f9a02d54e235c490615b3c967bfe15f4b43a6062f9e69d7 identique avant/après (identique à la valeur consignée par 29-VALIDATION.md pour la même mutation), git diff --stat vide"
avertissements:
  - "Note d'hygiène, sans conséquence sur le code : après restauration de LecteurAppBureau.cs par `git checkout --`, git avait ré-appliqué le filtre core.autocrlf (CRLF) alors que le fichier vivait en LF pur avant toute mutation (créé par l'outil d'écriture, jamais recyclé par un checkout) — sha256 différent bien que `git diff` fût déjà vide. Restauré depuis `git show HEAD:...` (bytes bruts, sans filtre) pour retrouver le sha256 exact d'avant mutation ; `cmp` confirme l'identité octet pour octet avec le blob HEAD. `git status --short` affiche encore un « M » résiduel sur ce seul fichier par prudence de core.autocrlf, mais `git diff`, `git diff --exit-code` (code 0) et `git hash-object` (identique au blob de l'index) prouvent qu'il n'y a AUCUNE différence de contenu — artefact de tooling de cette vérification, pas une mutation non révoquée."
human_verification:
  - test: "Galerie 8 styles × 9 thèmes : le titre long (`api-migration`) coupé par une ellipse (Pastilles, Marge, Jetons, Annonciateur), largeur des tuiles inchangée ; info-bulle « titre — dossier — mot », celle d'`overlay` avec « permission demandée » en seconde ligne ; quatre dossiers réels de 161,3 à 166,8 DIP (OLYMPE DATAMIND, TEXTURE MANAGER, OLYMPE SENTINELLE, SMART NEWSLETTER) désormais coupés par la borne de 160 ; largeur d'une info-bulle à needs_action long sans retour à la ligne"
    expected: "Rendu visuel correct, ellipse propre, alignement et lisibilité tenus sur les 8×9 combinaisons"
    why_human: "Jugement visuel ; l'agent ne lance pas la galerie (Chronos-v3.1.0.exe tourne déjà). Le test WPF 8×9 (SessionStylesBindingTests) couvre mécaniquement texte, troncature et largeur (+1 DIP), pas l'œil. Déjà reporté à la phase 31 par 29-VALIDATION.md — non bloquant pour cette phase."
  - test: "Section « Source app-bureau » du rapport de diagnostic, lue dans l'exe 3.2.0 publié et lancé normalement (explorer / démarrage, hors de l'arbre de l'app)"
    expected: "« Source app-bureau : trouvée — %LOCALAPPDATA%\\Packages\\Claude_…\\LocalCache\\Roaming\\Claude\\claude-code-sessions » et des lignes « Fichiers d'état (…) » pour la racine du paquet ET la vue réelle ; jointures et titres cohérents avec le widget"
    why_human: "Un constat fait depuis une session Claude Code voit la vue virtualisée MSIX, pas celle de l'overlay (29-RESEARCH Q0.c.3) ; l'agent ne lance ni n'arrête l'overlay. La sonde WMI hors-arbre (29-SONDE-APP06.txt, rejouée en lecture par ce rapport, §2 ci-dessous) en est l'équivalent automatisé au niveau de la phase ; la confirmation dans l'exe publié reste humaine. Déjà reporté à la phase 31 par 29-VALIDATION.md — non bloquant pour cette phase."
  - test: "Une VRAIE question classée blocked par l'app s'affiche « En attente », au rang des questions, motif en info-bulle ; repasse « Réflexion » dès la réponse"
    expected: "Comportement observé conforme à C1-C11 (testés en pur sur fixtures réelles) sur une session réellement en cours"
    why_human: "La classification de fin de tour est faite par l'app et ne se provoque pas à la demande. Déjà reporté à la phase 31 par 29-VALIDATION.md — non bloquant pour cette phase."
---

# Phase 29 : Ce que l'app bureau sait de chaque session — Rapport de vérification

**Goal (ROADMAP) :** Chronos lit, en **lecture seule**, ce que l'app bureau Claude écrit de chaque session — son
titre, le dernier instant où l'utilisateur l'a sélectionnée, la façon dont l'app a classé la fin du tour —, le
joint aux sessions du widget par `cliSessionId`, et **se tait proprement** quand ces fichiers manquent ou
changent de forme : le widget retombe alors exactement sur son comportement v1.6. Et (APP-06, ajouté le
2026-09-25) : les fichiers d'état des hooks sont lus dans les DEUX vues d'AppData, cache du paquet MSIX en
premier, constaté par une sonde lancée hors de l'arbre de l'app.

**SHA vérifié (arbre de travail) :** `1a25fdf` — **fin réelle de la phase 29 :** `6f26972` (les deux commits qui
suivent, `4006a1a` et `1a25fdf`, sont `docs(30): research` et `docs(30): …` — aucune touche au code ; confirmé
par `git diff --stat 6f26972..HEAD -- src/ tests/` vide) — **SHA d'entrée de phase :** `10a821f`
**Statut :** **passed** — **6/6 critères ROADMAP**, **6/6 exigences** (APP-01 à APP-06)
**Vérification :** initiale (aucun `29-VERIFICATION.md` antérieur)

---

## 0. Ce qui a été mesuré, pas lu

Aucun chiffre de SUMMARY ou de 29-VALIDATION.md n'a été repris tel quel sans être rejoué.

| Mesure | Commande | Résultat |
|---|---|---|
| Suite, 1re exécution | `dotnet test Chronos.sln --nologo -v q` | **1062 / 0 échec / 15 s** |
| Suite, 2e exécution | idem | **1062 / 0 échec / 13 s** |
| Suite, 3e exécution (après révocation des deux mutations) | idem | **1062 / 0 échec / 9 s** |
| Gardes ciblées (`GardesPerimetreTests`, `ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests`, `ContratHooksDocumenteTests`, `GardesDoctrineTests`) | `--filter` ciblé | **49 / 0 échec** |
| `GardesPerimetreTests` seule | `--filter "FullyQualifiedName~GardesPerimetreTests"` | **16 / 0** (29-VALIDATION.md annonçait 15 → 16 en fin de phase — confirmé) |
| `DiagnosticServiceTests` seule | idem | **39 / 0** (29-VALIDATION.md annonçait 31 → 39 — confirmé) |
| `LectureSeuleAppBureauTests` seule | idem | **6 / 0** (29-VALIDATION.md annonçait 4 → 6 — confirmé) |
| Mutation write dans `LecteurAppBureau.cs` + mutation chemin dans `DiagnosticService.cs` (jouées ensemble) | `--filter "FullyQualifiedName~LectureSeuleAppBureauTests"` | **échec : 2, réussite : 4, total : 6** — exactement les deux tests nommés en frontmatter |
| Révocation | `sha256sum` avant/après, `git diff --stat`, `git hash-object`, `cmp` contre le blob HEAD | **identiques** (voir avertissement ci-dessus pour le détail du sha256 CRLF/LF) |
| `claude-code-sessions` dans `src/` | `grep -rl` | **1 seul fichier : `Services/RacinesEtat.cs`** (confirmé indépendamment de la garde de test) |
| `grep File.Write\|File.Delete\|File.Move\|FileMode.Create` | `LecteurAppBureau.cs`, `RacinesEtat.cs` | **0 occurrence** dans les deux fichiers |
| `git status --porcelain` (dépôt entier, après vérification) | — | **`Chronos-v3.1.0.exe`** (préexistant, non suivi) + un « M » résiduel purement cosmétique sur `LecteurAppBureau.cs` (voir avertissement — contenu byte-identique au blob HEAD, `git diff --exit-code` = 0) |
| Fixtures réelles de la phase 27 | `ls tests/Chronos.Tests/TestData/DesktopAppSessions/` | **6 fichiers présents** (dont `sans-cliSessionId.json`, `fin-de-tour-blocked/completed/review-ready.json`, `session-courante-geste-b.json`) |

Le total mesuré (**1062**) est exactement celui que `29-VALIDATION.md` annonce en fin de phase (2 exécutions
consécutives, 1062/0). Aucun test n'a été supprimé ni renommé.

---

## 1. Verdict par critère du ROADMAP

| # | Critère | Verdict | Ce qui le prouve — **mécaniquement** |
|---|---|---|---|
| 1 | **Le titre remplace le dossier** (APP-01, APP-02) | ✅ **VÉRIFIÉ** | `SessionSnapshot.cs:37` : `Titre` dernier paramètre optionnel, aucun site de construction touché. `SessionMonitor.Enrichir` (`SessionMonitor.cs:233-234`) pose le titre sur les RETENUS **après** `ArbitrageSessions.Trancher` (`:173`) — jamais sur un signal, jamais avant l'arbitrage. `AffichageSessions.Nom` (`:89`) : titre si non blanc, sinon `Project`. `SessionsViewModel.cs:206-207` et `SessionsPreviewViewModel.cs:58,60` : `Project = AffichageSessions.Nom(s)`, `Infobulle = AffichageSessions.Infobulle(s)` — un seul producteur, deux consommateurs (widget + galerie). `SessionStyles.xaml` : `MaxWidth="160"` exactement 2 fois (Pastilles, Marge — les deux gabarits compacts visés par le plan), 8 `ToolTip="{Binding Infobulle}"`, **0** libellé en dur (`Réflexion`/`En attente` n'apparaissent que dans un commentaire de tête). |
| 2 | **Une question de l'app est une attente observée** (APP-03) | ✅ **VÉRIFIÉ** | `ArbitrageSessions.cs` : `SourceSession.AppBureau` déclaré **entre** `Hook` et `Transcript` (`:8-25`), rang lu par `Departager` via `(int)a.Source` (`:142`). `SessionMonitor.QuestionPosee` (`:222-229`) : `Bloquee` + motif non blanc + non archivée + instant ≤ `HorizonsSessions.Abandon` — toutes conditions nécessaires. **« L'app qualifie, elle ne crée pas »** : `SessionMonitor.Inspecter` (`:160-171`) ne dépose la question QUE pour un id déjà présent dans `projetConnu` (construit à partir des signaux Hook/Transcript de CE cycle) — `C7_sans_autre_source_la_question_de_l_app_ne_cree_aucune_ligne` (tests/Chronos.Tests/MoniteurAppBureauTests.cs:301) le tient. `completed`/`review_ready`/catégorie inconnue → `ClassificationFinDeTour.Terminee/PreteARevue/Inconnue`, aucun cas ne matche `QuestionPosee`. |
| 3 | **Dégradation v1.6, jamais de crash ni d'invention** (APP-01) | ✅ **VÉRIFIÉ** | `SessionMonitor` : `_appBureau` nullable, défaut `null` (`:40,65`) → `appBureau` reste `null` dans `Inspecter`, `raw = arbitrage.Retenus` inchangé (`:184-186`) — comportement v1.6 EXACT. `LecteurAppBureau.Lire` (`:176-269`) : dossier absent → `Vide(null)` ; énumération qui lève → dernière lecture valide ou `Vide` ; fichier illisible → dernière lecture valide gardée, jamais de titre qui clignote (`:220-231`). Fenêtre de 24 h : `HorizonsSessions.LectureAppBureau` (`HorizonsSessions.cs:40`). `FileShare.ReadWrite \| FileShare.Delete` : `LecteurAppBureau.cs:459`. Chemins construits par `Path.Combine` uniquement dans `RacinesEtat.Candidats` (`:72-75`), aucun séparateur mixte. |
| 4 | **Le diagnostic dit ce qu'il sait — et ce qu'il ne sait pas** (APP-04) | ✅ **VÉRIFIÉ** | `DiagnosticService.DecrireSourceAppBureau` (`:658-711`) : quatre états explicites — `NON BRANCHÉE` (`:665`), `lecture impossible à ce cycle` (`:664`), `absente (dossier introuvable) — cherché : …` (`:671`), `trouvée — <racine>` avec autres candidats, compteurs (énumérés/récents/valides/relus/illisibles/sans id/doublons), champs absents, puis une ligne PAR SESSION AFFICHÉE (titre, focus, classification) (`:675-710`). Appelée à `:589` avec `lecture` — **la MÊME** que celle rendue par l'unique appel `_moniteurSessions.Inspecter(_clock.UtcNow)` à `:544` (confirmé : un seul `Inspecter(` dans tout `DiagnosticService.cs`) — OBS-01 étendu. Racines des fichiers d'état : une ligne par racine AVANT la liste fusionnée, `absent (dossier introuvable)` jamais tu (`:474-488`). |
| 5 | **Lecture seule, prouvée** (APP-05) | ✅ **VÉRIFIÉ** | `LecteurAppBureau.Ouvrir` (`:458-459`) : `FileMode.Open` (jamais Create), `FileAccess.Read`, `FileShare.ReadWrite \| FileShare.Delete` — seule ouverture du fichier dans la classe. `grep -rl "claude-code-sessions" src/` = **1 seul fichier**, `RacinesEtat.cs`, qui n'appelle aucune API d'écriture (`grep -cE "File\.\|Directory\.Create\|Directory\.Delete\|FileMode\|FileAccess"` = 0). Six tests dans `LectureSeuleAppBureauTests.cs` (garde au texte du lecteur, écrivain en place, empreinte octet pour octet ×3 lectures, garde du chemin unique, garde du résolveur), **6/6 verts**, et **mutation rejouée par le vérificateur** (§2 ci-dessous). |
| 6 | **Les deux vues d'AppData sont lues** (APP-06) | ✅ **VÉRIFIÉ** | `RacinesEtat.Candidats` (`:48-81`) : énumère `Packages\Claude_*` (motif, suffixe non codé), `EtatsHooks` = paquets d'abord puis vue réelle, dédoublonné en gardant l'ordre. `SessionMonitor.Inspecter` (`:132-150`) et `BalayageMagasinSessions.Balayer` (`:97-129`) itèrent `foreach (var racine in _dossiers/dossier)` sur **toutes** les racines. `DiagnosticService` : une ligne « Fichiers d'état (racine) » par racine (`:470-488`). `App.xaml.cs` : câblage de production ligne par ligne — `RacinesEtat.ParDefaut()` (`:259`), `LecteurAppBureau(… .SessionsAppBureau)` (`:263`), `SessionMonitor(…, dossiersEtat: … .EtatsHooks, appBureau: …)` (`:265-269`), `BalayageMagasinSessions(… .Dossiers, …)` (`:278`). **Constat hors de l'arbre** : `29-SONDE-APP06.txt`, processus WMI (parent `WmiPrvSE`, PID 132152), code du commit `dd974f1` livré, `%APPDATA%\Claude existe : False`, racine du paquet trouvée avec **5** fichiers d'état, `PremiereExistante(SessionsAppBureau)` = le chemin du paquet, **18** sessions lues avec métadonnées, **4/4** sessions affichées jointes à un titre, dates d'écriture de `%APPDATA%\Chronos\{archived,treated,settings}.json` identiques avant/après dans les trois vues (aucune écriture pendant la sonde). |

**Score : 6/6.**

---

## 2. Points de vigilance, un par un

### (1) APP-06 : racines par candidats, paquet avant vue réelle, câblage de production, sonde hors arbre

`RacinesEtat.Candidats` (`RacinesEtat.cs:59-62`) énumère `Directory.EnumerateDirectories(dossierPaquets,
"Claude_*")`, filtré par `StartsWith("Claude_", OrdinalIgnoreCase)`, trié `StringComparer.Ordinal`, **matérialisé
dans le `try`** — jamais de suffixe éditeur codé en dur. `SessionMonitor.cs:132` et
`BalayageMagasinSessions.cs:97` bouclent sur **chaque** racine (`foreach (var racine in _dossiers)` /
`foreach (var dossier in _dossiers)`), et `DiagnosticService.cs:474` de même. `App.xaml.cs:256-280` câble
production : `RacinesEtat.ParDefaut()` singleton, `LecteurAppBureau` construit sur `.SessionsAppBureau`,
`SessionMonitor` reçoit `dossiersEtat: … .EtatsHooks` et `appBureau: …` par arguments **nommés**,
`BalayageMagasinSessions` reçoit `SessionMonitor.Dossiers` — jamais un chemin déduit dans son coin. La sonde
`29-SONDE-APP06.txt`, relue intégralement (§ ci-dessus du fichier), a tourné **hors de l'arbre de l'app**
(processus créé par `Win32_Process.Create`, parent `WmiPrvSE`, comme l'overlay lancé par `explorer.exe`) avec le
code du commit `dd974f1` (celui livré par 29-05), et constate : `%APPDATA%\Claude existe : False`,
`%APPDATA%\Chronos\sessions existe : False` (la vue réelle, vide en production), la racine du paquet
`…\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Chronos\sessions` **présente avec 5 fichiers**, et
`PremiereExistante(SessionsAppBureau)` pointant vers le chemin du paquet — exactement la thèse d'APP-06.

### (2) Lecture seule — garde au texte, gardes de câblage, mutations rejouées par le vérificateur

`LecteurAppBureau.Ouvrir` (`:458-459`) est l'unique ouverture de fichier de la classe :
`FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete`. `grep -rl "claude-code-sessions"
src/` rend **exactement un fichier**, `Services/RacinesEtat.cs`, qui n'appelle aucune API d'écriture.

**Deux mutations jouées par le vérificateur** (pas seulement relues au SUMMARY), l'une reproduisant le point de
vigilance « rejouer une mutation d'écriture », l'autre « une mutation de chemin » :

1. Ajout d'une méthode morte (jamais appelée) `MutationTestM1()` dans `LecteurAppBureau.cs` portant
   `System.IO.File.WriteAllText("x", "y")`.
2. Ajout d'un commentaire `// mutation test: claude-code-sessions` juste avant
   `DecrireSourceAppBureau` dans `DiagnosticService.cs`.

```
dotnet test --filter "FullyQualifiedName~LectureSeuleAppBureauTests"
échec : 2, réussite : 4, total : 6
  LectureSeuleAppBureauTests.Le_lecteur_ne_contient_aucun_appel_d_ecriture_et_ouvre_en_partage_complet [FAIL]
  LectureSeuleAppBureauTests.Le_chemin_de_l_app_bureau_n_existe_que_dans_le_resolveur_de_racines [FAIL]
```

Exactement les deux tests attendus (la seconde mutation reproduit littéralement la mutation (c) de
`29-VALIDATION.md`, qui rapportait le même nom de test rouge). **Révocation** : `LecteurAppBureau.cs` restauré
depuis `git show HEAD:...` (bytes bruts du blob, sans filtre de checkout) — `sha256sum` =
`f19cf4c328abaaac36d3bd9dea5d0fdca0d85ca91bd5df7dffa1aba39129b082`, **identique** à la valeur mesurée avant
mutation, et `cmp` confirme l'identité octet pour octet avec `git show HEAD:...`. `DiagnosticService.cs`
restauré par `git checkout --` — `sha256sum` = `3b87c84ebcbf91de9f9a02d54e235c490615b3c967bfe15f4b43a6062f9e69d7`,
**identique** à la valeur d'avant mutation et à celle consignée par `29-VALIDATION.md` pour la mutation (c).
`git diff --stat -- src/` et `git diff --exit-code -- src/Chronos/Services/LecteurAppBureau.cs` (code 0) sont
**vides** ; la suite rejouée après révocation est **1062 / 0**. (Un « M » cosmétique subsiste dans
`git status --short` sur `LecteurAppBureau.cs` — pure question de fin de ligne LF/CRLF liée à `core.autocrlf`,
sans le moindre octet de différence de contenu ; détaillé dans l'avertissement du frontmatter.)

### (3) « L'app qualifie une ligne, elle n'en crée pas »

`SessionMonitor.Inspecter` (`:158-171`) : `appBureau.ParSession` n'est consulté QUE pour les identifiants déjà
présents dans `projetConnu`, construit à partir des signaux Hook/Transcript déposés CE cycle (`:162-167`) — un
id absent de `projetConnu` ne peut jamais recevoir de question. `QuestionPosee` (`:222-229`) exige en plus : non
archivée, motif non blanc, instant ≤ `HorizonsSessions.Abandon` (8 h — même borne que les autres sources), daté
par `InstantClassification` (l'épisode figé, jamais `DerniereActivite` courante). `SourceSession.AppBureau`
inséré entre `Hook` et `Transcript` (`ArbitrageSessions.cs:8-25`) ; `RangArbitrage` (`:159-165`) reste
**inchangé** par cette phase — aucune ligne de `ArbitrageSessions.cs` ne référence `AppBureau` dans le calcul du
rang d'état, seul le rang de SOURCE (`(int)a.Source`) le fait, exactement comme prévu. Test
`C7_sans_autre_source_la_question_de_l_app_ne_cree_aucune_ligne`
(`tests/Chronos.Tests/MoniteurAppBureauTests.cs:301`) et `Une_question_marquee_traitee_ne_revient_pas_quand_l_activite_de_fond_avance`
(`:416`, TRT-02) verts dans la suite mesurée.

### (4) Dégradation v1.6 exacte

`SessionMonitor` : `_appBureau` par défaut `null` (`:40`), et `Inspecter` ne consulte `appBureau.ParSession` que
si `appBureau is { DossierTrouve: true }` (`:160,184`) — sans lecteur injecté OU sans dossier trouvé, `raw =
arbitrage.Retenus`, identique à la v1.6. `LecteurAppBureau.Lire` sur un dossier absent rend `Vide(null)`
(`DossierTrouve = false`). Un fichier sans `cliSessionId` → `IssueLecture.SansCliSessionId`, compté (`sansId++`,
`:235`), jamais dans `ParSession`. Aucune exception ne remonte au-delà du `try/catch` de `Inspecter` (`:159`) ni
de `Lire` (catch sur l'énumération, `:193-198`, et sur l'interprétation, `:437-442`).

### (5) Le titre : champ optionnel, posé après l'arbitrage, producteur unique, gabarits

`SessionSnapshot.Titre` (`:37`) dernier paramètre optionnel. `SessionMonitor.Enrichir` (`:233-234`) appelé
**après** `ArbitrageSessions.Trancher` (`:173`), sur les `arbitrage.Retenus` (`:185`) — jamais sur un `SignalSession`
brut, jamais consulté par `Departager`. `AffichageSessions.Nom/Infobulle/MotifLisible` (`:89,98,111`) : seul
producteur, `SessionsViewModel.cs` et `SessionsPreviewViewModel.cs` les consomment tous deux, **0** composition
d'info-bulle ailleurs (`grep` ciblé sur les deux ViewModels ne montre aucune concaténation manuelle de
`Project`/`Titre`). `SessionStyles.xaml` : `MaxWidth="160"` ×2 (Pastilles, Marge), 8 `ToolTip="{Binding
Infobulle}"`, aucun libellé `Réflexion`/`En attente` en dur (seule occurrence : un commentaire de tête).

### (6) Diagnostic : même lecture que le widget, source jamais tue

Un seul appel à `_moniteurSessions.Inspecter(_clock.UtcNow)` dans tout `DiagnosticService.cs` (`:544`), sa
variable `lecture` réutilisée pour la section OBS-01 (visibles/masquées, `:546-620` environ) ET pour
`DecrireSourceAppBureau(sb, lecture, …)` (`:589`) — jamais un second `Inspecter`, jamais un second `LecteurAppBureau`.
Quatre états couverts sans silence : `NON BRANCHÉE`, `lecture impossible à ce cycle`, `absente (dossier
introuvable) — cherché : …`, `trouvée — <racine>`. Les lignes AFFICHÉES et MASQUÉES du rapport utilisent
`AffichageSessions.Nom(d)` (`:552,565`), le même nom que le widget.

### (7) Gardes existantes non assouplies — vérifié par exécution, pas par lecture du SUMMARY

`GardesPerimetreTests`, `ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests`,
`ContratHooksDocumenteTests`, `GardesDoctrineTests` rejouées explicitement par le vérificateur : **49 / 0**.
Comptes de classe vérifiés un par un et conformes à 29-VALIDATION.md : `GardesPerimetreTests` **16/16** (15 → 16),
`DiagnosticServiceTests` **39/39** (31 → 39), `LectureSeuleAppBureauTests` **6/6** (4 → 6). `docs/hooks-contract.md`
§3 porte « Deux vues d'AppData », `` `RacinesEtat` `` et `LocalCache\Roaming\Chronos\sessions` (`:137-143`),
tenu par `ContratHooksDocumenteTests`.

---

## 3. Couverture des exigences

| Exigence | Requise par plan | Verdict | Preuve nommée |
|---|---|---|---|
| **APP-01** | 29-02, 29-03 | ✅ **SATISFAITE** | `LecteurAppBureau.Interpreter` sur les 6 fixtures réelles ; fenêtre 24 h (`HorizonsSessions.LectureAppBureau`) ; `FileShare.ReadWrite \| FileShare.Delete` ; dégradation v1.6 exacte sans dossier/fichier illisible/`cliSessionId` manquant (§1 pt 3, §2 pt 4) |
| **APP-02** | 29-03, 29-04 | ✅ **SATISFAITE** | `SessionSnapshot.Titre`, `SessionMonitor.Enrichir` après l'arbitrage, `AffichageSessions.Nom/Infobulle`, `MaxWidth="160"` (Pastilles, Marge), 8 `ToolTip` liés (§1 pt 1, §2 pt 5) |
| **APP-03** | 29-03 | ✅ **SATISFAITE** | `SourceSession.AppBureau`, `QuestionPosee`, règle « qualifie, ne crée pas » tenue par `C7_…`, datation par épisode tenue par `Une_question_marquee_traitee_ne_revient_pas_…` (§1 pt 2, §2 pt 3) |
| **APP-04** | 29-05 | ✅ **SATISFAITE** | `DecrireSourceAppBureau`, 4 états sans silence, même `lecture` que le widget, un seul `Inspecter` (§1 pt 4, §2 pt 6) |
| **APP-05** | 29-02, 29-05 | ✅ **SATISFAITE** | `LecteurAppBureau.Ouvrir` (partage complet, jamais Create), `claude-code-sessions` dans 1 seul fichier de `src/`, 6/6 `LectureSeuleAppBureauTests`, 2 mutations rejouées et révoquées par le vérificateur (§1 pt 5, §2 pt 2) |
| **APP-06** | 29-01, 29-05 | ✅ **SATISFAITE** | `RacinesEtat.Candidats` (paquet `Claude_*` d'abord), moniteur/balayage/diagnostic sur chaque racine, câblage de production, sonde `29-SONDE-APP06.txt` hors de l'arbre avec le code livré (§1 pt 6, §2 pt 1) |

`REQUIREMENTS.md` : les six lignes `APP-01` à `APP-06` sont cochées `[x]` et marquées `| Phase 29 | Complete |`.
Union des `requirements:` déclarés par les 5 plans (`29-01`: APP-06 ; `29-02`: APP-01, APP-05 ; `29-03`: APP-01,
APP-02, APP-03 ; `29-04`: APP-02 ; `29-05`: APP-04, APP-05, APP-06) = {APP-01…APP-06} exactement. **Aucune
exigence orpheline.**

---

## 4. Ce qui reste manuel — déjà reporté à la phase 31, non bloquant

`29-VALIDATION.md` (section « Manual-Only Verifications ») reporte explicitement trois vérifications à la phase 31 :

1. **La racine de PRODUCTION vue dans le diagnostic de l'exe publié** (APP-06, APP-04) — un constat fait depuis
   une session Claude Code voit la vue virtualisée MSIX, jamais celle de l'overlay ; la sonde hors-arbre de 29-05
   (relue au §1 pt 6 et §2 pt 1 ci-dessus) en est l'équivalent automatisé au niveau de la phase, mais la
   confirmation dans l'exe 3.2.0 publié reste humaine.
2. **La galerie 8 × 9, à l'œil** (APP-02) — ellipse du titre long, largeur des tuiles, info-bulle à deux lignes,
   les quatre dossiers réels de 161-167 DIP désormais coupés par la borne de 160, la largeur d'une info-bulle
   `needs_action` longue sans retour à la ligne. Le test WPF `SessionStylesBindingTests` couvre mécaniquement les
   72 combinaisons (texte, troncature, +1 DIP), pas le rendu visuel.
3. **Une vraie question `blocked` affichée en conditions réelles** (APP-03) — la classification de fin de tour
   est faite par l'app et ne se provoque pas à la demande ; C1-C11 la couvrent en test pur sur fixture réelle.

Ces trois items sont repris tels quels dans le frontmatter (`human_verification`). Ils ne bloquent pas le statut
de cette phase : ils étaient déjà hors du périmètre exécutable par un agent en lecture seule (29-CONTEXT.md,
29-VALIDATION.md), et le ROADMAP les confie explicitement à la phase 31 (« l'exe 3.2.0 est publié et réconcilié,
et l'utilisateur vérifie le tableau en trois lignes sur sa machine »).

---

## 5. Hygiène de la vérification

Aucune écriture hors dépôt (hormis les deux fichiers temporaires `/tmp/lecteur.cs` et `/tmp/diag.cs`, extraits en
lecture depuis `git show`, utilisés uniquement pour comparaison de bytes et jamais réinjectés tels quels côté
`DiagnosticService.cs`). Les deux mutations jouées (§2, point 2) ont été appliquées directement aux fichiers du
dépôt puis révoquées — `LecteurAppBureau.cs` restauré depuis le blob `git show HEAD:...` (bytes bruts,
sha256 identique à la valeur d'avant mutation et `cmp` confirme l'identité octet pour octet), `DiagnosticService.cs`
restauré par `git checkout --` (sha256 identique). `git status --porcelain` du dépôt entier, avant et après la
session de vérification, ne montre que `Chronos-v3.1.0.exe` (non suivi, préexistant) et un « M » cosmétique sur
`LecteurAppBureau.cs` dû à une différence de fin de ligne LF/CRLF sans le moindre octet de contenu changé
(`git diff --exit-code` rend 0, `git hash-object` égale le blob de l'index). Aucun overlay n'a été lancé ni
arrêté ; aucun fichier sous `%APPDATA%\Chronos`, `%APPDATA%\Claude` ou `~/.claude` n'a été approché ; toute
lecture hors dépôt (fixtures, sonde) a été faite en lecture pure sur des fichiers déjà versionnés.

---

## 6. Conclusion

**Le goal de la phase est atteint.** Le widget lit désormais, en lecture strictement seule, le titre, le dernier
focus et la classification de fin de tour que l'app bureau écrit par session, les joint par `cliSessionId`, et se
dégrade exactement vers le comportement v1.6 quand ces fichiers manquent — vérifié dans le code (`LecteurAppBureau`,
`SessionMonitor`, `SessionSnapshot`) et par exécution de la suite complète, trois fois, 1062/1062 à chaque fois.
La règle « l'app qualifie une ligne, elle n'en crée pas » est tenue par construction (`projetConnu`) et par test
nommé (`C7_…`). La lecture seule n'est pas qu'une déclaration : `claude-code-sessions` n'existe que dans un seul
fichier de `src/`, aucune API d'écriture n'apparaît dans les deux fichiers sensibles, et j'ai moi-même rejoué une
mutation d'écriture et une mutation de chemin — les deux ont rougi exactement les tests attendus, et j'ai revérifié
la révocation à l'octet près plutôt que de me fier à un `git diff` silencieux. Le fait le plus lourd de la phase —
la virtualisation AppData qui cachait tout fichier de hook de l'app bureau à l'overlay en production — est fermé
par `RacinesEtat` et confirmé par une sonde qui a réellement tourné hors de l'arbre de l'app, avec le code livré,
et vu les 5 fichiers d'état du paquet que l'overlay ne voyait jamais avant cette phase.

Trois vérifications visuelles/in vivo restent hors de portée d'un agent — elles étaient déjà reportées à la phase
31 par le plan lui-même, et ne remettent pas en cause ce qui est prouvé mécaniquement ici.

---

_Vérifié : 2026-09-26 — Vérificateur : Claude (gsd-verifier)_
_Suite exécutée trois fois par le vérificateur : 1062 / 0 échec à chaque fois. Deux mutations rejouées et
révoquées (sha256 identique, vérifié aussi par `cmp` octet pour octet contre le blob HEAD). Aucun chiffre de
SUMMARY ou de VALIDATION repris sans exécution._
