---
phase: 24-l-arbitrage-par-fraicheur
plan: 02
subsystem: sessions
tags: [diagnostic, desaccords, fusion-sources, source-figee, traçabilite, xunit]

requires:
  - phase: 24-l-arbitrage-par-fraicheur
    provides: "LectureSessions.Desaccords + DesaccordSources (livrés par 24-01)"
  - phase: 22-l-instrument-partage
    provides: "partage d'instance du moniteur : _moniteurSessions.Inspecter(_clock.UtcNow), lecture unique"
  - phase: 20-l-inventaire-machine
    provides: "?? new InventaireMachine() — les 2 occurrences à préserver"
provides:
  - "AffichageSessions.Ecart : un ÉCART d'âge (distance) et non une ancienneté (instant)"
  - "Bloc « Désaccords entre sources » du rapport de diagnostic — compte + une ligne par désaccord"
  - "LibelleSourceSession : chaque source nommée AVEC SON DOSSIER, pour diagnostiquer seul une source figée"
  - "24-VALIDATION.md rempli et mesuré (status: validated) — clôture de la phase 24"
affects: [25-contrat-d-evenements, 26-le-traite-observe]

tech-stack:
  added: []
  patterns:
    - "Un désaccord a son PROPRE bloc : la session reste comptée parmi les AFFICHÉES, jamais parmi les masquées"
    - "Changement purement ADDITIF d'un fichier partagé, prouvé par `git diff -U0 | grep -c '^-[^-]'` → 0"
    - "Capture de l'extrait RÉEL du rapport par dump d'assertion temporaire, révoqué avant commit"

key-files:
  created: []
  modified:
    - src/Chronos/Services/AffichageSessions.cs
    - src/Chronos/Services/DiagnosticService.cs
    - tests/Chronos.Tests/AffichageSessionsTests.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - .planning/phases/24-l-arbitrage-par-fraicheur/24-VALIDATION.md
    - .planning/REQUIREMENTS.md

key-decisions:
  - "Un écart d'âge se dit en DISTANCE (« 7 h ») et non en instant (« il y a 7 h ») ; le zéro se dit « 0 s » pour que l'égalité d'âge se voie."
  - "Le membre neuf s'appelle LibelleSourceSession et non LibelleSource : Chronos.Text.LibelleSource existe déjà et nomme la source d'un relevé d'USAGE — le nom du plan aurait masqué ce type dans la classe."
  - "Le vocabulaire de masquage n'est pas élargi : le type de motif reste à 3 occurrences, avant comme après."

patterns-established:
  - "Preuve d'additivité exigée sur un fichier partagé : 31 insertions, 0 suppression sur DiagnosticService.cs."

requirements-completed: [FUS-02]

duration: 10min
completed: 2026-09-12
---

# Phase 24 Plan 02 : Le diagnostic des désaccords Summary

**Le rapport de diagnostic nomme désormais, pour chaque désaccord entre sources, la source RETENUE avec son dossier, la source ÉCARTÉE avec son dossier et l'ÉCART D'ÂGE qui les sépare — et la session concernée reste comptée parmi les AFFICHÉES, jamais parmi les masquées.**

## Performance

- **Duration:** 10 min
- **Started:** 2026-09-12T18:13Z
- **Completed:** 2026-09-12T18:24Z
- **Tasks:** 2/2
- **Files modified:** 6 (0 créé, 6 modifiés)

## Accomplishments

- **FUS-02 livré.** Une source figée cesse d'être un symptôme inexplicable : le rapport dit laquelle a perdu, d'où elle vient, et de combien elle est plus vieille.
- **Le critère n°3 du ROADMAP est prouvé** par deux tests — le désaccord nommé, et le silence qui se dit.
- **Un désaccord n'est pas un masquage**, et le test l'assert dans le **même** rapport : `Sessions AFFICHÉES par le widget : 1` et `Sessions MASQUÉES par un filtre : 0`, alors que le désaccord est compté.
- **Le changement de `DiagnosticService.cs` est purement additif** : 31 insertions, **0 suppression**, mesuré.
- **`24-VALIDATION.md` est rempli et mesuré**, `status: validated`, **0** case en attente de mesure. La phase 24 est close côté mécanisme.

## Task Commits

1. **Task 1 (a) — le formateur** : `6438b1d` — `feat(24-02): un ecart d'age se dit en distance, pas en instant`
2. **Task 1 (b) — ROUGE** : `2234e65` — `test(24-02): les desaccords entre sources doivent etre lisibles dans le rapport`
3. **Task 1 (c) — VERT** : `68af388` — `feat(24-02): le rapport nomme la source retenue, l'ecartee et l'ecart d'age (FUS-02)`
4. **Task 2 — la carte** : `fa3c74d` — `docs(24-02): la carte de verification de la phase 24, remplie et mesuree`

Les quatre commits compilent (`0 erreur / 0 avertissement`) ; aucun « échec de build attendu » n'a été utilisé.
Sujets de commit en ASCII, conformément à la convention observée sur les six commits de 24-01.

## Comptes de tests — avant / après

| Moment | échecs | réussites | total | durée |
|---|---|---|---|---|
| **Baseline d'entrée de plan** (fin de 24-01) | 0 | **782** | **782** | 4 s |
| Après Task 1 (a) (`6438b1d`) | 0 | 786 | 786 | 4 s |
| **Après ROUGE Task 1 (b) (`2234e65`)** | **2** | **786** | **788** | 4 s |
| Après VERT Task 1 (c) (`68af388`) | 0 | 788 | 788 | 4 s |
| **Après Task 2 (`fa3c74d`) — exécution 1** | **0** | **788** | **788** | 4 s |
| **Après Task 2 — exécution 2 (consécutive)** | **0** | **788** | **788** | 4 s |

**+6 tests exactement** (4 cas de `[Theory]` + 2 `[Fact]` ; 782 + 6 = 788). **Aucun test supprimé**, aucun ignoré.
La cible de fin de phase prédite par `24-VALIDATION.md` — « ≈ 788 » — est atteinte **au test près**, sans ajustement de la prédiction.

## L'étape ROUGE, mesurée

### Task 1 (b) — filtre `FullyQualifiedName~Diagnostic`, contre le rapport d'avant le bloc

**2 échecs / 28 réussites / 30 total.** Build **0 erreur / 0 avertissement**. Les 2 tests tombés :

1. `Un_desaccord_nomme_la_source_retenue_la_source_ecartee_et_l_ecart_d_age`
2. `Sans_contradiction_le_rapport_annonce_zero_desaccord_et_le_dit`

Sur la suite complète au même commit : **2 échecs / 786 réussites / 788 total** — les **786** tests
préexistants étaient donc tous verts dès le rouge. Le rouge est localisé au comportement neuf, jamais à une
régression.

**Note honnête sur la troisième assertion neuve.** L'extension de
`Sans_moniteur_le_rapport_dit_qu_il_n_a_rien_observe` (`Assert.DoesNotContain("Désaccords entre sources")`)
était **verte dès le rouge**, et c'est normal : au commit ROUGE, la chaîne n'existait nulle part. C'est une
garde de non-régression, pas un test rouge-avant — elle ne prend son sens qu'au commit VERT, où elle prouve
que le bloc neuf reste bien **à l'intérieur** de la branche « moniteur injecté ». À ne pas compter comme une
falsification.

### Task 1 (a) — pas d'étape rouge, et c'était prévu

Le plan ordonnait explicitement « **(a)** `AffichageSessions.Ecart` + ses tests → **vert immédiat** ».
Le formateur et sa `[Theory]` ont été livrés dans le même commit : 786/786, 0 échec. Aucune étape rouge n'a
donc été mesurée pour (a), conformément au plan — ce n'est pas une omission.

## La valeur réelle rendue par `Ecart` pour `7 h − 10 s`

**`"6 h"`** — exactement la valeur prédite par le plan. L'écart vaut 6 h 59 min 50 s ; `Ecart` tronque à
l'heure inférieure via `(int)d.TotalHours`. **Aucun ajustement de test n'a été nécessaire**, et le chiffre
n'a pas eu à être « compris après coup » : l'assertion `Assert.Contains("plus ancien de 6 h", ligne)` du plan
est passée telle quelle au premier essai.

## L'extrait RÉEL du nouveau bloc de rapport

Capturé en faisant dumper à l'assertion la valeur réellement produite (patch temporaire du fichier de test,
**révoqué avant tout commit** — `git checkout` puis `grep` de résidu → **0**, suite revérifiée à 788/788).

**Cas du désaccord** (hook figé de 7 h contre transcript de 10 s, `Un_desaccord_nomme_…`) :

```
  Désaccords entre sources : 1
    · e465420e — retenu transcript (~/.claude/projects) « en cours » ; écarté fichier de hook (%APPDATA%\Chronos\sessions) « à toi », plus ancien de 6 h
```

**Cas sans contradiction** (`Sans_contradiction_le_rapport_annonce_zero_desaccord_et_le_dit`) :

```
  Désaccords entre sources : 0
    (aucun — aucune source n'en contredit une autre en ce moment)
```

Les deux sources sont bien nommées **avec leur dossier** — c'est précisément ce qui permet à l'utilisateur
d'aller ouvrir `%APPDATA%\Chronos\sessions` et de constater seul que ses hooks n'écrivent plus.

## Critères d'acceptation — valeurs de `grep` PRÉDITES vs RÉELLEMENT OBTENUES

### Task 1

| Mesure | Prédit | Obtenu |
|---|---|---|
| `grep -cF "public static string Ecart"` (`AffichageSessions.cs`) | 1 | **1** ✅ |
| `grep -cE "FromUnixTimeMilliseconds\|FromUnixTimeSeconds\|DateTimeOffset\.TryParse"` (`AffichageSessions.cs`) | 0 | **0** ✅ |
| `grep -cF "L_ecart_d_age_se_dit_en_distance_pas_en_instant"` (tests) | 1 | **1** ✅ |
| `grep -cF "Désaccords entre sources"` (`DiagnosticService.cs`) | 1 | **1** ✅ |
| `grep -cF "lecture.Desaccords"` (`DiagnosticService.cs`) | 3 | **3** ✅ |
| `grep -cF "AffichageSessions.Ecart("` (`DiagnosticService.cs`) | 1 | **1** ✅ |
| `grep -cF "private static string LibelleSource(SourceSession s)"` | 1 | **0** ⚠️ — **renommé**, voir déviation n°1 |
| `grep -cF "private static string LibelleSourceSession(SourceSession s)"` | — | **1** ✅ (le compte prédit, porté par le nom réel) |
| `grep -cF "LibelleSource("` (`DiagnosticService.cs`) | 3 | **0** ⚠️ — **renommé**, voir déviation n°1 |
| `grep -cF "LibelleSourceSession("` (`DiagnosticService.cs`) | — | **3** ✅ (la signature + les **deux lignes** physiques de l'`AppendLine`) |
| `grep -cF "MotifMasquage"` (`DiagnosticService.cs`) | 3, INCHANGÉ | **3** ✅ |
| `grep -cF "new SessionMonitor"` (`DiagnosticService.cs`) | 0 | **0** ✅ |
| `grep -cF "?? new "` (`DiagnosticService.cs`) | 2 | **2** ✅ |
| `grep -cF "_moniteurSessions.Inspecter(_clock.UtcNow)"` | 1 | **1** ✅ |
| `git diff -U0 -- tests/Chronos.Tests/DiagnosticServiceTests.cs \| grep -c "^-[^-]"` | 0 | **0** ✅ |
| `git diff -U0 -- src/Chronos/Services/DiagnosticService.cs \| grep -c "^-[^-]"` | 0 | **0** ✅ |
| `git diff --stat` de `SessionMonitor.cs` / `ArbitrageSessions.cs` / `LectureSessions.cs` | vide | **vide** ✅ |
| Filtres `Diagnostic` + `Affichage` | 0 échec | **47 réussites / 0 échec** ✅ |
| Suite complète | 0 échec, total > 782 | **788 / 0 échec** ✅ |
| `GardesPerimetre` / `NormalisationUnique` / `ServicesLayerPurity` / `CompositionRoot` / `GardesDoctrine` | 10 / 3 / 2 / 5 / 8 | **10 / 3 / 2 / 5 / 8**, 0 échec partout ✅ |

**Le seul écart prédit/obtenu est le renommage**, et il est documenté ci-dessous plutôt qu'absorbé
(leçon 22-03). Les **comptes** exigés par le plan — 1 signature, 3 lignes physiques — sont tenus à
l'identique sous le nom réel.

### Task 2

| Mesure | Prédit | Obtenu |
|---|---|---|
| `grep -c "(à mesurer)"` (`24-VALIDATION.md`) | 0 | **0** ✅ |
| `grep -c "status: validated"` | 1 | **1** ✅ |
| `grep -c "À VÉRIFIER PAR L'UTILISATEUR"` | 1 | **1** ✅ |
| `grep -c "contrôle de non-régression"` | ≥ 1 | **2** ✅ |
| `grep -c "^\| *[1-4] "` (les 4 critères du ROADMAP) | ≥ 4 | **4** ✅ |
| `grep -cF "- [x] **FUS-01**"` (`REQUIREMENTS.md`) | 1 | **1** ✅ (déjà cochée par 24-01) |
| `grep -cF "- [x] **FUS-02**"` | 1 | **1** ✅ |
| `grep -c "\| FUS-0[12] \| Phase 24 \| Pending \|"` | 0 | **0** ✅ |
| `git diff --stat` de la tâche | `24-VALIDATION.md` + `REQUIREMENTS.md` seuls | **exactement ces deux fichiers** ✅ |
| Suite après écriture des documents | 0 échec, même total | **788 / 0 échec** ✅ |

## La justification du changement de `DiagnosticService.cs`, et sa preuve d'additivité

La phase 22 garantit que le rapport décrit **le moniteur qui tourne** : tout changement de **câblage** du
widget s'y reflète sans qu'une ligne n'y change. Elle ne garantit pas — et ne peut pas garantir — qu'une
information **qui n'existait pas** s'imprime toute seule. Les désaccords sont une information neuve : il
fallait des lignes neuves.

L'alternative rejetée était de faire passer un désaccord pour un masquage. Ce serait un mensonge structurel
— la session concernée est **affichée**, ce n'est pas elle qui est écartée mais l'un de ses **signaux** — et
cela rouvrirait la confusion « absente » / « écartée par tel filtre » que la phase 22 a fermée.

**Preuve que le changement est une INSERTION et non une réécriture :**

| Preuve | Exigé | Mesuré |
|---|---|---|
| `git diff -U0 -- src/Chronos/Services/DiagnosticService.cs \| grep -c "^-[^-]"` | 0 | **0** ✅ |
| `git diff --stat` sur le fichier | insertions seules | **31 insertions(+), 0 suppression** ✅ |
| `git diff --stat` du plan entier (`HEAD~3..HEAD` du code) | 0 suppression | **103 insertions(+), 0 suppression**, 4 fichiers ✅ |
| Le type de motif de masquage | 3, inchangé | **3** ✅ — le vocabulaire de masquage n'a pas été élargi |

Aucun `using` déplacé, aucune réindentation, aucun reformatage.

## Deviations from Plan

### 1. `LibelleSource` → `LibelleSourceSession` : collision avec un type préexistant

- **Rule 3 (blocage)** — **Trouvé pendant :** Task 1, étape VERT, au premier build.
- **Problème :** le plan demandait `private static string LibelleSource(SourceSession s)`. Or
  **`Chronos.Text.LibelleSource` est une classe statique déjà utilisée dans ce même fichier** (lignes
  724-728 : `LibelleSource.Format(w.Source)`, `LibelleSource.Anciennete(…)`, `LibelleSource.Provenance(…)`)
  — elle nomme la source d'un **relevé d'usage**, pas d'un signal de session. Déclarer un membre du même nom
  masque le type dans la classe : **3 erreurs `CS0119`**, sur des lignes **préexistantes** que la règle
  d'additivité m'interdisait de toucher.
- **Correction :** le membre neuf est nommé `LibelleSourceSession`. Les lignes 724-728 n'ont pas été
  effleurées, et l'additivité est intacte (0 suppression). Un commentaire ajouté au-dessus du membre explique
  la distinction, pour que le suffixe ne passe pas pour du bruit.
- **Pourquoi c'est mieux que l'alternative :** renommer l'existant aurait violé l'additivité et touché une
  API partagée avec l'infobulle du cadran (20-04) ; qualifier en `Chronos.Text.LibelleSource.Format` aurait
  réécrit trois lignes existantes. Au passage, le suffixe supprime une ambiguïté réelle : « qui alimente un
  quota » et « qui dépose un signal de session » sont deux notions étrangères.
- **Conséquence sur les critères :** `grep -cF "LibelleSource("` → **0** au lieu de 3, et
  `grep -cF "LibelleSourceSession("` → **3**. Le compte exigé (1 signature + 2 lignes physiques) est tenu.
- **Fichier :** `src/Chronos/Services/DiagnosticService.cs` — **Commit :** `68af388`.

### 2. Sujets de commit en ASCII

- **Trouvé pendant :** Task 1 (a). Le plan écrit ses sujets accentués (« un écart d'âge se dit… »).
- **Fait :** sujets non accentués, comme les **six** commits de 24-01 (`test(24-01): l'arbitrage par
  fraicheur…`) et les commits antérieurs du dépôt. Le **contenu** du message est inchangé.
- **Pourquoi :** cohérence de l'historique sur un dépôt Windows. Aucun effet sur le code ni sur les tests.

### 3. Capture de l'extrait réel du rapport par dump temporaire

- **Trouvé pendant :** Task 1 (c), pour honorer l'exigence du plan « l'extrait **réel** du nouveau bloc de
  rapport » dans ce SUMMARY — le reconstruire depuis la chaîne de format aurait été une affirmation non
  observée, exactement ce que la doctrine du milestone interdit.
- **Fait :** deux assertions du fichier de test remplacées **temporairement** par un `Assert.True(false, …)`
  qui imprime la valeur réelle, mesure, puis `git checkout -- tests/Chronos.Tests/DiagnosticServiceTests.cs`.
- **Révocation prouvée :** `grep` de résidu (`D1>>`/`D2>>`/`DUMP`) → **0** ; `git status --porcelain` ne
  montrait plus que `DiagnosticService.cs` ; suite revérifiée **788 / 0 échec** avant le commit VERT. Aucun
  dump n'a jamais été commité.

### 4. FUS-01 était déjà cochée

- Le plan demandait de cocher **FUS-01 et FUS-02**. `FUS-01` était déjà `- [x]` et `Complete` (fait par
  24-01). Seule **FUS-02** a été modifiée, dans la case et dans le tableau de traçabilité. L'état final est
  celui que le plan exigeait.

**Aucune autre déviation.** Le corps d'`Ecart`, le bloc de rapport, la `[Theory]`, les deux `[Fact]` et
l'assertion d'extension ont été écrits **tels quels** depuis le plan. Aucun seuil de `grep`, aucun corpus de
test, aucune valeur attendue n'a été « améliorée ».

## Acquis des phases précédentes — revérifiés

| Acquis | Attendu | Mesuré |
|---|---|---|
| Phase 22 — `=> Inspecter(now).Visibles;` dans `SessionMonitor.cs` | 1 | **1** ✅ |
| Phase 22 — `new SessionMonitor` dans `DiagnosticService.cs` | 0 | **0** ✅ |
| Phase 22 — `_moniteurSessions.Inspecter(_clock.UtcNow)` | 1 | **1** ✅ |
| Phase 20 — `?? new ` dans `DiagnosticService.cs` | 2 | **2** ✅ |
| Phase 24-01 — `byId[` / `ArbitrageSessions.Trancher(` (`SessionMonitor.cs`) | 0 / 1 | **0 / 1** ✅ |
| Phase 24-01 — `DateTimeOffset.UtcNow` / `AffichageSessions.Urgence(` (`ArbitrageSessions.cs`) | 0 / 2 | **0 / 2** ✅ |
| Phase 25 non anticipée — `StaleWorking` 20 min / `DropAfter` 8 h | inchangés | **inchangés** ✅ |
| Phase 26 non anticipée — diff `SessionTreatmentTracker` / `TreatedStore` / `ArchiveStore` | vide | **vide** ✅ |
| `GardesPerimetre` | 10 | **10 / 0 échec** ✅ |
| `NormalisationUnique` | 3 | **3 / 0 échec** ✅ |
| `ServicesLayerPurity` | 2 | **2 / 0 échec** ✅ |
| `CompositionRoot` | 5 | **5 / 0 échec** ✅ |
| `GardesDoctrine` | 8 | **8 / 0 échec** ✅ |

## Invariants de sécurité — mesurés AVANT et APRÈS

| Invariant | Attendu | Avant | Après |
|---|---|---|---|
| `%APPDATA%\Chronos\sessions` | 66 entrées | **66** | **66** ✅ |
| `%APPDATA%\Chronos\archived.json` | 84 octets | **84** | **84** ✅ |
| `%APPDATA%\Chronos\oauth.dat` (taille seule, **jamais** le mtime) | 518 octets | **518** | **518** ✅ |
| Overlay `Chronos-v3.0.2.exe` pid 119412 | vivant, ni lancé ni tué | **vivant** (`tasklist` seul) | **vivant** ✅ |
| `git diff -- '*.csproj'` | vide | vide | **vide** ✅ |
| `git status --porcelain` | rien hors `src/`, `tests/`, `.planning/` | vide | **vide (tout commité)** ✅ |
| Dépendances NuGet ajoutées | 0 | — | **0** (3 `PackageReference`, inchangées) ✅ |
| Requêtes réseau réelles depuis un test | 0 | — | **0** (montages à `Token = null` ; la sonde est gardée par `if (token is not null)`) ✅ |
| Tests écrivant hors de `Path.GetTempPath()` | 0 | — | **0** (garde `Assert.StartsWith` de `TempDir22()` conservée) ✅ |
| Test à retardement sur horloge réelle | 0 | — | **0** — horloge figée (`FakeClock(T22)`), dossiers de magasin **vides** via `TempFichier22()`, donc aucun TTL 6 h en jeu ✅ |

## Known Stubs

Aucun. Le bloc « Désaccords entre sources » est alimenté par la **même** lecture partagée que les visibles et
les masquées (`lecture.Desaccords`, `_moniteurSessions.Inspecter(_clock.UtcNow)` → 1), et l'extrait réel du
rapport ci-dessus le prouve sur de vraies données.

## État de clôture de la phase 24

- **FUS-01** (24-01, arbitrage par fraîcheur) et **FUS-02** (24-02, désaccords traçables) sont **couvertes
  et cochées** dans `REQUIREMENTS.md`. Le tableau de traçabilité ne porte plus aucun `Pending` sur la phase 24.
- **`24-VALIDATION.md`** est `status: validated`, **0** case en attente de mesure : les 4 critères du ROADMAP
  avec leur test nommé et leur résultat, les 5 lignes du relevé avec leur qualification honnête (**3**
  rouges-avant, **1** témoin, **1** contrôle de non-régression) plus le **4e rouge-avant** (permutation à
  corpus ex aequo), les preuves structurelles, la mutation et sa révocation, les invariants de sécurité.
- **Suite : 788 tests, 0 échec, 0 ignoré**, deux exécutions consécutives au même total, **aucun test
  supprimé** sur toute la phase (763 → 788, +25).

### Ce qui reste à vérifier in vivo

Les tests prouvent le **mécanisme**, pas ce que l'écran montre (leçon 21-04). Restent à voir de ses yeux :
le critère n°1 (une session qui travaille s'affiche « en cours » malgré un vieux fichier de hook), le
critère n°3 (**clic droit → Diagnostic…**, lire le bloc « Désaccords entre sources » et y trouver la source
figée nommée avec son dossier), le critère n°2 (aucun clignotement d'un tick à l'autre sans donnée nouvelle)
et le critère n°4 (une vraie demande de permission bascule bien en « à toi »).

### Le rappel décisif

**RIEN DE CE MILESTONE NE S'EXÉCUTE TANT QUE L'EXE N'EST PAS REPUBLIÉ.** Les hooks installés dans
`~/.claude/settings.json` pointent `Chronos-v3.0.2.exe` — l'exe **actuellement en cours d'exécution**,
antérieur à tout le milestone v1.6. La republication (version **dans** l'exe **et** dans le nom du fichier
publié, `Chronos-vX.Y.exe`) reste à faire en fin de milestone, avant toute vérification in vivo. Les
reliquats hérités des phases 21, 22 et 23 seront présentés **groupés** à ce moment-là.

## Self-Check: PASSED

- `src/Chronos/Services/AffichageSessions.cs` — FOUND (`Ecart` présent, 1 occurrence)
- `src/Chronos/Services/DiagnosticService.cs` — FOUND (« Désaccords entre sources » présent, 1 occurrence)
- `.planning/phases/24-l-arbitrage-par-fraicheur/24-VALIDATION.md` — FOUND (`status: validated`, 1 occurrence)
- Commits `6438b1d`, `2234e65`, `68af388`, `fa3c74d` — tous FOUND dans `git log`
- Suite complète : 788/788, deux exécutions consécutives au même total, 0 échec
