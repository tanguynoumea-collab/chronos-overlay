---
phase: 32-compter-juste-puis-journaliser
plan: 07
subsystem: release / documentation du journal d'historique
tags: [JRN-06, release, 3.2.2, publish, mono-fichier, smoke, garde-documentaire, data-sources, HYP-1, HYP-2, HYP-3, xunit-analyzers, zero-warning]

sha_entree_de_plan: 09a5e15
sha_release: 769c9a0
one_liner: "Chronos 3.2.2 est publié sous son nom, vérifié sans être lancé, et commité (769c9a0) : 77 557 997 o, 0 DLL, VersionInfo 3.2.2.0 / 3.2.2, md5 51f4d95bb346b9a4ce33f3f63f3d434c ; smoke --hook SessionStart code 0, settings.json md5 52827d6a… identique avant/après, 0 processus résident, overlay jamais lancé. docs/data-sources.md gagne le §7 « Journal d'historique » (11 champs, 6 événements, clé (t, source), FileMode.Append écarté, 24 mois, deux vues d'AppData, HYP-1/2/3) sous la garde ContratJournalDocumenteTests qui compare le texte aux types réels par réflexion dans les deux sens ; publish.md §7 dit le verrou mono-instance. Porte « zéro warning » tenue d'abord (xUnit1031, xUnit2013). Tests : 1309 → 1313 (+4), 0 échec, 0 avertissement, deux exécutions avant et après le bump."

requires:
  - phase: 32-compter-juste-puis-journaliser
    provides: "32-01 DedupUsage (CPT-01) ; 32-02 IEtatMagasin, VueAppData (CPT-02) ; 32-03 VerrouInstanceUnique, InventaireProcessus (CPT-03) ; 32-04 JournalReleves, LigneJournal (ChampsReleve), EvenementJournal (NomsDeFil), JournalisationUsageProvider ; 32-05 câblage, âge et alerte ; 32-06 LecteurJournal, BornesPlage, AnalyseReleves — 1309 / 0 à l'entrée (09a5e15)"
  - phase: 31-crit-publi-constat
    provides: "31-02 : procédure de release (bump × 4, suite × 2, publish mono-fichier, contrôles, copie versionnée, smoke --hook, commit sans étiquette) et VersionPublieeTests"
provides:
  - "docs/data-sources.md §7 « Journal d'historique » : emplacement et nommage mensuel UTC, rétention 24 mois sans compaction, tableau des 11 champs du relevé (type, unité, provenance), tableau des 6 événements (quand, ce qu'ils portent), idempotence (t, source) et append exclusif, les deux vues d'AppData, HYP-1/2/3 datées, ce que le journal n'est pas ; note CPT-01 au §2"
  - "tests/Chronos.Tests/ContratJournalDocumenteTests.cs : garde de non-dérive doc ↔ types (ChampsReleve == JsonPropertyName du DTO d'écriture ; NomsDeFil == Nom() de l'enum, aller-retour Depuis ; chaque nom cité entre accents graves ; HYP-1/2/3 ; rétention, dédup, deux vues ; publish.md sans « aucun verrou »)"
  - "docs/publish.md §7 : une seule instance (mutex Local\\Chronos-overlay), exe antérieurs à quitter à la main ; §6 : le smoke --hook ne prouve ni le verrou ni le journal"
  - "src/Chronos/Chronos.csproj en 3.2.2 (Version, FileVersion, AssemblyVersion, InformationalVersion)"
  - "Chronos-v3.2.2.exe à la racine du dépôt (non suivi, ignoré par /Chronos-v*.exe), prêt pour le premier lancement par l'utilisateur"
  - "Suite de tests sans aucun avertissement d'analyseur (porte DoD)"
affects: [32-08-constat, 33 (tokens-AAAA-MM.jsonl : un champ ajouté sans sa ligne de doc rougit), 34-35 (lecture du journal)]

tech-stack:
  added: []
  patterns:
    - "Garde documentaire à deux sens : le document doit nommer chaque élément d'un tableau public du code, ET ce tableau public doit être ce que le code écrit vraiment (réflexion sur le DTO privé et l'enum) — sinon on comparerait le texte à une liste qui ne garde rien"
    - "Les hypothèses s'écrivent comme des hypothèses, avec attendu chiffré et échéance datée, et une garde tient leur présence"
    - "Release : même procédure que 31-02, la version se lit (VersionInfo, grep) et ne s'épingle pas dans un test"

key-files:
  created:
    - tests/Chronos.Tests/ContratJournalDocumenteTests.cs
  modified:
    - docs/data-sources.md
    - docs/publish.md
    - src/Chronos/Chronos.csproj
    - tests/Chronos.Tests/JournalRelevesTests.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs

key-decisions:
  - "D-32-31 — la doc du journal est gardée par les types : ChampsReleve et NomsDeFil sont comparés au texte du §7 ET aux JsonPropertyName / Nom() réels par réflexion ; un champ ajouté en phase 33 sans sa ligne rougit"
  - "D-32-32 — les trois hypothèses sont écrites comme des hypothèses (HYP-1 granularité 0,01 ; HYP-2 Δ = consommation ; HYP-3 reset hebdo local le 25/10/2026, attendu 2026-10-30T23:00Z contre 22:00Z pour NextReset), avec attendu et échéance ; le journal écrit le double brut"
  - "D-32-33 — même procédure de release que 31-02 ; ancien md5 de référence 3.2.1 = 3a1dc26f92f7547cb244538f0447779b (77 385 116 o) ; exe à la racine du dépôt principal ; .gitignore vérifié, pas modifié"
  - "D-32-34 — le smoke --hook ne prouve ni le mutex ni le journal (sortie avant le Host) : il prouve que l'exe sort proprement sans écrire ; CPT-03 et JRN-01 se constatent en 32-08"
  - "D-32-35 — porte « zéro warning » avant toute release : les deux avertissements d'analyseur xUnit hérités de la vague 1 sont corrigés à l'identique sémantique (await WhenAll ; Assert.Single) avant le bump"

requirements-completed: [JRN-06]

duration: 12min
completed: 2026-09-27
---

# Phase 32 Plan 07 : Chronos 3.2.2 publié seul, sans interface, et documenté — Summary

**La 3.2.2 attend l'utilisateur à la racine du dépôt, sous son nom, version 3.2.2 dans ses quatre propriétés, son
VersionInfo et son rapport ; elle a été publiée, contrôlée et soumise au smoke `--hook` sans jamais être lancée en
overlay. `docs/data-sources.md` dit désormais ce que le journal écrit (11 champs, 6 événements), ce qu'il n'écrit jamais,
d'où l'on regarde (deux vues d'AppData), et les trois questions qu'il posera au temps (HYP-1/2/3) — sous la garde des
types réels. La suite compile sans un avertissement. Le commit de release `769c9a0` le date.**

## Performance

- **Début :** 2026-09-27T02:12:42Z. **Fin :** 2026-09-27T02:25Z environ. **Durée :** environ 12 min (dont ≈ 4 min de
  compilation et tests, 31 s de publication).
- **SHA d'entrée :** `09a5e15`, à **1309 verts / 0 échec** (mesuré après la porte zéro warning, deux fois).
- **Exécution :** seul sur l'arbre réel (vague 3), commits normaux, stage explicite. Le dépôt principal est l'arbre
  courant (`git rev-parse --git-common-dir` → `<dépôt>/.git`).
- **Tâches :** 2 sur 2, soit 4 commits de tâche (porte zéro warning, RED, GREEN, release) + le commit de docs final.
- **Fichiers :** les 4 de `files_modified` + les 2 fichiers de tests de la porte zéro warning (autorisés par la consigne).
  `.planning/ROADMAP.md` et `.planning/STATE.md` ne sont pas touchés.

## Mesures (tests)

| Moment | Filtre `ContratJournalDocumenteTests|ContratHooksDocumenteTests` | Suite complète (`dotnet test Chronos.sln -c Debug`) | Avertissements |
|---|---|---|---|
| Entrée `09a5e15`, après la porte (`13c04d9`) | — | **1309 / 0**, deux exécutions (12 s, 12 s) | **0** (build Release : 0 avertissement, 0 erreur) |
| Tâche 1, RED (`9a53d50`) | **4 échecs** (les 4 nouveaux ; 18 hooks verts) | — | — |
| Tâche 1, GREEN (`38c36e4`) | **22 / 22** | — | — |
| Tâche 2, après le bump 3.2.2 | — | **1313 / 0**, deux exécutions (10 s, 10 s) = 1309 + 4 | **0** ; publish : 0 avertissement |

## Porte « zéro warning » (déviation Rule 2, faite en premier)

Deux avertissements d'analyseur xUnit hérités de la vague 1, corrigés à l'identique sémantique, commit `13c04d9` :

| Fichier | Avertissement | Avant | Après |
|---|---|---|---|
| `JournalRelevesTests.cs` (142) | xUnit1031 (attente bloquante) | `public void …() { … Task.WaitAll(ta, tb); }` | `public async Task …() { … await Task.WhenAll(ta, tb); }` — toujours deux écrivains distincts, un seul fichier |
| `GardesPerimetreTests.cs` (469) | xUnit2013 (`Assert.Equal` sur un compte) | `Assert.Equal(1, Regex.Matches(texte, Regex.Escape("Inspecter(")).Count)` | `Assert.Single(Regex.Matches(texte, Regex.Escape("Inspecter(")))` — même assertion : exactement une occurrence |

Après correction : `dotnet build Chronos.sln -c Release --nologo` → 0 avertissement, 0 erreur ; `dotnet test … -v q` → 0
ligne « warning », 1309 / 0, deux fois. Les deux fichiers restent LF sans BOM.

## Le rouge (TDD) et les mutations

**RED 4 / 4** (`9a53d50`) : les quatre tests de `ContratJournalDocumenteTests` rougissent sur « Section « ## 7. Journal
d'historique » introuvable dans le document » — la bonne raison. Les 18 tests de `ContratHooksDocumenteTests`, dont la
garde réutilise `SectionDe`, restent verts.

**GREEN 22 / 22** (`38c36e4`). Les assertions par réflexion sont vraies sur les types réels : `LigneJournal.ChampsReleve`
(11) est exactement la suite des `JsonPropertyName` du DTO privé `LigneReleveDto` triés par `JsonPropertyOrder` ;
`TypeEvenementTexte.NomsDeFil` (6) est exactement `Nom()` des valeurs de `TypeEvenement` hors `NonReconnu`, et chaque nom
fait l'aller-retour `Depuis` → `Nom`.

| Mutation (sur `docs/data-sources.md`, sha256 avant = après `da920deed44e70e3205c7ffd5881807f0161c95402d13ffb9e62c4230b6d7194`) | Rouge constaté (filtre journal, 4 tests) |
|---|---|
| **(h1)** ligne `| \`overage_statut\` | … |` retirée du tableau | **1 / 4** : `Le_document_nomme_chaque_champ_du_releve_et_chaque_evenement_reels` |
| **(h2)** `HYP-3` → `HYP-4` (sed global) | **1 / 4** : `Le_document_ecrit_les_trois_hypotheses_a_verifier_avec_le_journal` |

Le fichier a été copié dans le bloc-notes de session avant chaque mutation et restauré par copie ; sha256 identique après
chaque révocation ; aucune mutation commitée.

## Critères grep (Tâche 1)

| Critère | Attendu | Mesuré |
|---|---|---|
| `^## 7. Journal d'historique` (data-sources.md) | 1 | **1** |
| `HYP-1\|HYP-2\|HYP-3` | ≥ 3 | **3** |
| `2026-10-30T23:00Z` / `24 mois` / `` `ecriture_ratee` `` / `DedupUsage` | ≥ 1 chacun | **1 / 1 / 3 / 2** |
| `il n'existe aucun verrou mono-instance` (publish.md) | 0 | **0** |
| `une seule instance` / `Local\Chronos-overlay` (publish.md, `grep -F`) | ≥ 1 / ≥ 1 | **1 / 1** |
| `[Fact]` dans ContratJournalDocumenteTests.cs | 4 | **4** ; LF seul, sans BOM |
| CR dans data-sources.md / publish.md / Chronos.csproj | 0 | **0 / 0 / 0** |

## La release

| Contrôle | Attendu | Mesuré |
|---|---|---|
| Préconditions | arbre `src tests docs` propre ; `Chronos-v3.2.1.exe` présent ; `Chronos-v3.2.2.exe` absent | **vrai / présent / absent** |
| Bump du csproj | 4 lignes ; `3.2.2` × 4, `3.2.1` × 0, 0 CR | **4 lignes remplacées (`git diff` : 4 −, 4 +) ; 4 / 0 ; 0 CR** |
| `dotnet publish src/Chronos/Chronos.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true` | code 0 | **code 0** (02:20:27Z → 02:20:58Z, 31 s), **0 avertissement** |
| Sortie `publish/` | `Chronos.exe` (+ `.pdb`), 0 DLL | **`Chronos.exe` 77 557 997 o + `Chronos.pdb` 199 712 o ; 0 DLL** |
| Taille | < 120 000 000 o | **77 557 997 o** (77,6 Mo ; 74,0 Mio) ; la 3.2.1 faisait 77 385 116 o (+172 881 o : le journal) |
| VersionInfo de la sortie | 3.2.2.0 / 3.2.2 / Chronos | **FileVersion 3.2.2.0, ProductVersion 3.2.2, ProductName Chronos** |
| md5 de la sortie | ≠ `3a1dc26f92f7547cb244538f0447779b` (3.2.1) | **`51f4d95bb346b9a4ce33f3f63f3d434c`** |
| Copie `Chronos-v3.2.2.exe` | md5 égal à la sortie ; VersionInfo 3.2.2.0 / 3.2.2 | **`51f4d95bb346b9a4ce33f3f63f3d434c` ; 3.2.2.0 / 3.2.2** |
| `git check-ignore -v Chronos-v3.2.2.exe` | `/Chronos-v*.exe`, code 0 | **`.gitignore:22:/Chronos-v*.exe`**, code 0 ; `git status` ne le montre pas |
| Anciens exe | `Chronos-v3.1.0/3.2.0/3.2.1.exe` intacts | **présents** (77 218 013 / 77 377 466 / 77 385 116 o) ; 3.2.1 md5 `3a1dc26f…` inchangé |

**Où l'exe attend l'utilisateur :** `%USERPROFILE%\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.2.2.exe`.

### Smoke `--hook SessionStart`, entrée standard vide (D-32-34)

| Relevé | Valeur |
|---|---|
| `~/.claude/settings.json` avant publication (02:20:27Z) | md5 `52827d6a52cb148b08165f36f46f2401`, 3 384 o, modifié le 2026-09-26 à 18:53:11 (+02:00) |
| Processus Chronos avant | `Chronos-v3.1.0.exe` PID 40772, `Chronos-v3.2.0.exe` PID 126160, `Chronos-v3.2.1.exe` PID 121900 — **trois anciens overlays tournent** ; 0 × 3.2.2 |
| md5 juste avant le smoke (02:21:35Z) | `52827d6a52cb148b08165f36f46f2401` |
| `"…/Chronos-v3.2.2.exe" --hook SessionStart < /dev/null` | **code 0**, sortie à 02:21:39Z |
| md5 après | **`52827d6a52cb148b08165f36f46f2401`, identique** (taille et date inchangées) |
| Processus `Chronos-v3.2.2.exe` après | **0** |
| `%APPDATA%\Chronos\historique` vu de ce processus | **absent** (le mode `--hook` n'écrit rien) |
| Seule écriture de l'hôte mono-fichier | `%LOCALAPPDATA%\Temp\.net\Chronos-v3.2.2\` (extraction native, attendue, hors périmètre) |

Le smoke s'exécute dans le processus de l'agent (vue **virtualisée** d'AppData) et sort avant le Host : il prouve que
l'exe démarre et sort proprement sans écrire ; il ne prouve ni le mutex ni le journal, qui se constatent en 32-08.
**L'overlay n'a jamais été lancé par l'agent**, ni sans argument ni avec `--statusline`.

### Commit de release

`769c9a0` — `release: Chronos 3.2.2 - phase 32 << Compter juste, puis journaliser >>`. `git show --stat` : exactement
`src/Chronos/Chronos.csproj`. Le corps donne le périmètre (CPT-01..03, JRN-01..05), la taille, le md5, VersionInfo, le
smoke et les tests (1313 / 0, deux exécutions, 0 avertissement). `git tag --points-at HEAD` est vide ; rien n'a été
poussé.

## Task Commits

0. **Porte « zéro warning » (déviation Rule 2)** — `13c04d9` (test : xUnit1031, xUnit2013 ; 1309 / 0 × 2, 0 avertissement)
1. **Tâche 1 : data-sources §7, publish §7, garde documentaire**
   - `9a53d50` (test, RED 4/4)
   - `38c36e4` (docs, GREEN 22/22 ; mutations h1, h2 jouées et révoquées)
2. **Tâche 2 : Chronos 3.2.2 publié sous son nom, vérifié sans être lancé, et commité**
   - `769c9a0` (release, suite 1313 / 0 deux fois)

**Plan metadata :** commit `docs(32-07)` final (SUMMARY, REQUIREMENTS).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - DoD] Porte « zéro warning » avant la release**
- **Trouvé pendant :** entrée du plan (consigne de l'orchestrateur, DoD `.zeus/DOD.md`).
- **Problème :** deux avertissements d'analyseur xUnit hérités de la vague 1 (`xUnit1031` `Task.WaitAll` dans un test
  synchrone ; `xUnit2013` `Assert.Equal(1, ….Count)`).
- **Correctif :** `async Task` + `await Task.WhenAll(ta, tb)` ; `Assert.Single(Regex.Matches(…))`. Comportement testé
  inchangé (deux écrivains, un fichier ; exactement une occurrence de `Inspecter(`).
- **Fichiers :** `tests/Chronos.Tests/JournalRelevesTests.cs`, `tests/Chronos.Tests/GardesPerimetreTests.cs`.
- **Commit :** `13c04d9`.

### Le code fait foi sur le texte attendu

**2. `docs/data-sources.md` §7 — chiffres tirés du code, pas du plan.** Le plan écrivait « 60 reprises » et « ≈ 230 o » ;
le code dit `EssaisMax = 600` (≈ 0,6 s au pire, borne volontairement plus haute que celle des hooks) et D-32-20 mesure
216 o sans overage / 270 o avec. Le document porte les valeurs du code, plus la queue relue (16 Ko ≈ 70 lignes ≈ 6 h),
`SeuilReprise` = 10 min et `SeuilMuet` = 15 min, la règle de cause d'un trou (D-32-26) et la règle du Δ (D-32-28), tous
lus dans `JournalReleves`, `JournalisationUsageProvider` et les SUMMARY 32-04/32-06.

**3. Garde documentaire plus stricte que le plan (deux sens).** Outre « le document nomme chaque élément », le test
vérifie par réflexion que `ChampsReleve` == `JsonPropertyName` du DTO d'écriture (ordre du fil) et que `NomsDeFil` ==
`Nom()` de l'enum hors `NonReconnu`, avec aller-retour `Depuis`. Sans cela, la garde comparerait le texte à une liste
qui pourrait elle-même dériver du fil. Toujours 4 `[Fact]`.

**4. Séparateur `---` entre §6 et §7.** Le §7 est inséré après le `---` qui clôt le §6 (comme chaque section du
document) et suivi d'un `---` avant la ligne finale ; `SectionJournal` coupe au dernier `---`. Le §2 reçoit la note
CPT-01 datée, la ligne finale mentionne « complété le 2026-09-27 (§7, note CPT-01 du §2) ».

**Total :** 1 correctif automatique (Rule 2) et 3 formulations tirées du code ou du contexte. Aucune garde assouplie,
aucune assertion existante affaiblie (les deux corrections de la porte sont sémantiquement identiques).

## Known Stubs

Aucun. `TODO|FIXME|placeholder|coming soon` ne donne rien dans les fichiers créés ou modifiés.

## Exigences

- **JRN-06 : complète.** Release 3.2.2 sans UI, quatre propriétés du csproj cohérentes (`VersionPublieeTests` relit le
  csproj 3.2.2 et l'assembly recompilée), `docs/data-sources.md` §« Journal d'historique » avec les trois hypothèses sous
  garde documentaire, exe publié et vérifié sans être lancé. La réconciliation au premier lancement, le mutex et le
  journal se constatent dans les fichiers en 32-08 (VAL-04).

## Next Phase Readiness

**32-08 (constat par l'utilisateur) :**
- l'exe attend à `%USERPROFILE%\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.2.2.exe`, md5
  `51f4d95bb346b9a4ce33f3f63f3d434c`, 77 557 997 o ;
- **trois anciens overlays tournent** (3.1.0 PID 40772, 3.2.0 PID 126160, 3.2.1 PID 121900) : le verrou `Local\Chronos-overlay`
  ne les connaît pas ; les quitter TOUS (réglages → « Quitter Chronos ») avant de lancer la 3.2.2 par l'Explorateur ;
  ensuite, lancer la 3.2.2 une seconde fois doit afficher « Chronos tourne déjà… » (CPT-03) ;
- `~/.claude/settings.json` avait le md5 `52827d6a52cb148b08165f36f46f2401` à 02:21:39Z (3 384 o). Le re-relever
  juste avant le lancement : c'est la valeur que la sauvegarde neuve doit reproduire ;
- une fois lancé, « Diagnostic… » doit afficher `Version : 3.2.2`, « Vue AppData : réelle », et la ligne du journal ;
  `%APPDATA%\Chronos\historique\releves-2026-09.jsonl` (vue RÉELLE, par sonde hors arbre) doit porter un `demarrage`
  avec `"version":"3.2.2"` puis un relevé `SondeEnTetes` à la première sonde, un seul par `t` ;
- les constats se font par sonde hors de l'arbre de l'app (`docs/data-sources.md` §7, « Les deux vues d'AppData »).

## Self-Check: PASSED

- FOUND : `tests/Chronos.Tests/ContratJournalDocumenteTests.cs`, `docs/data-sources.md` (§7), `docs/publish.md` (§7),
  `src/Chronos/Chronos.csproj` (3.2.2 × 4), `Chronos-v3.2.2.exe` (racine, md5 `51f4d95b…`), `Chronos-v3.2.1.exe`
  (intact), ce SUMMARY
- FOUND : commits `13c04d9`, `9a53d50`, `38c36e4`, `769c9a0` (et la référence d'entrée `09a5e15`)
- Suite : 1313 / 0, deux exécutions consécutives (10 s, 10 s), 0 avertissement, sur le code final ; aucune étiquette,
  aucun push ; aucun overlay lancé.

---
*Phase : 32-compter-juste-puis-journaliser*
*Terminé : 2026-09-27*
