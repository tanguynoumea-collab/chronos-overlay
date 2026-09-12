---
phase: 24-l-arbitrage-par-fraicheur
verified: 2026-09-12T19:05:00Z
status: complete
gsd_status: passed
score: 4/4 critères du ROADMAP vérifiés mécaniquement
verifier: gsd-verifier (goal-backward)
suite_mesuree:
  execution_1: "788 tests / 0 échec / 0 ignoré / 6 s"
  execution_2: "788 tests / 0 échec / 0 ignoré / 4 s"
rouges_avant_reproduits_independamment:
  - commit: 8e2db74
    filtre: ArbitrageSessions
    mesure: "9 échecs / 0 réussite / 9 total"
  - commit: 2cecfdb
    filtre: InspectionSessions
    mesure: "4 échecs / 10 réussites / 14 total — exactement les 4 tests annoncés"
  - commit: 2234e65
    filtre: Diagnostic
    mesure: "2 échecs / 28 réussites / 30 total"
falsification_du_verificateur:
  - mutation: "Departager réduit à la FRAÎCHEUR seule (rangs 2 à 5 supprimés), en worktree jetable"
    resultat: "2 échecs — Permuter_l_ordre_des_signaux… et A_age_egal_la_source… ⇒ le corpus ex aequo est porteur, la garde n'est pas muette"
  - mutation: "LibelleSourceSession → LibelleSource, en worktree jetable"
    resultat: "3 erreurs CS0119 (lignes 729, 730, 733) ⇒ la collision invoquée par la déviation est RÉELLE"
gaps: []
human_verification:
  - test: "Critère n°1 in vivo — une session qui travaille s'affiche « en cours » malgré un vieux fichier de hook"
    expected: "Plus aucun « à toi » pendant que le modèle travaille"
    why_human: "Ne s'exécute pas avant republication de l'exe (les hooks pointent Chronos-v3.0.2.exe)"
  - test: "Critère n°3 in vivo — clic droit → Diagnostic…, bloc « Désaccords entre sources »"
    expected: "Une ligne nommant source retenue + dossier, source écartée + dossier, écart d'âge"
    why_human: "Rendu à l'écran + données réelles de la machine"
  - test: "Critère n°2 in vivo — aucun clignotement d'un tick à l'autre sans donnée nouvelle"
    expected: "États stables entre deux ticks à données constantes"
    why_human: "Comportement temps réel, non observable en test unitaire"
  - test: "Critère n°4 in vivo — une vraie demande de permission bascule bien en « à toi »"
    expected: "Le hook spécifique, quand il est frais, l'emporte"
    why_human: "Nécessite un événement Claude Code réel"
---

# Phase 24 — L'arbitrage par fraîcheur : rapport de vérification

**But de la phase (ROADMAP) :** quand deux sources parlent de la même session, c'est la **plus récente** qui
gagne — jamais celle qui se trouvait en premier dans un dictionnaire — et l'utilisateur peut constater le
désaccord au lieu de le subir en silence.

**Méthode :** vérification *goal-backward*. Rien n'a été admis sur la foi des SUMMARY. Toutes les mesures
ci-dessous ont été refaites par le vérificateur, y compris les étapes ROUGE, **rejouées dans un worktree git
jetable** (créé puis supprimé ; le dépôt principal n'a jamais été modifié et est resté propre sur `7168e71`).

**Statut : `complete` côté mécanisme.** Les quatre critères sont prouvés mécaniquement. Ce qui reste ouvert
est explicitement **in vivo** et ne peut PAS l'être avant republication de l'exe — ce n'est pas un défaut de
la phase, c'est une contrainte de terrain héritée de tout le milestone v1.6.

---

## 1. La suite, mesurée par le vérificateur (point h)

`dotnet test Chronos.sln -c Debug --nologo -v q`, deux fois de suite, sur le dépôt à `7168e71` :

| Exécution | échec | réussite | ignoré | total | durée |
|---|---|---|---|---|---|
| 1 | **0** | **788** | 0 | **788** | 6 s |
| 2 (consécutive) | **0** | **788** | 0 | **788** | 4 s |

**Conforme à l'attendu** (788 / 0 échec). La course du chargeur BAML ne s'est pas manifestée : même total,
même verdict aux deux exécutions. Le chiffre des SUMMARY n'a pas été repris, il a été **remesuré**.

Suites de gardes nommées, chacune exécutée séparément :

| Suite | Attendu | Mesuré |
|---|---|---|
| `GardesPerimetreTests` | 10 | **10 / 0 échec** ✅ |
| `NormalisationUniqueTests` | 3 | **3 / 0 échec** ✅ |
| `ServicesLayerPurityTests` | 2 | **2 / 0 échec** ✅ |
| `CompositionRootTests` | 5 | **5 / 0 échec** ✅ |
| `GardesDoctrineTests` | 8 | **8 / 0 échec** ✅ |

`ServicesLayerPurityTests` étant verte, la garde « aucun type WPF dans `Services/` ni `Models/` » tient —
y compris pour `ArbitrageSessions.cs`, qui est neuf dans cette couche.

---

## 2. Les quatre critères du ROADMAP, un par un — dans le CODE

### Critère 1 — les inversions mesurées disparaissent : ✓ VÉRIFIÉ

**Dans le code.** `SessionMonitor.Inspecter` (lignes 72-130) ne construit plus aucune entrée indexée : il
COLLECTE (`signaux.Add(new SignalSession(SourceSession.Transcript, t))` puis `…SourceSession.Hook, snap`) et
délègue à `ArbitrageSessions.Trancher(signaux)`. Vérifié par grep sur le fichier réel :
`byId[` → **0**, `Dictionary<string, SessionSnapshot>` → **0**, `ArbitrageSessions.Trancher(` → **1**.

**Dans le comparateur.** `Departager` place la fraîcheur au **rang 1** :
`b.Session.UpdatedAt.CompareTo(a.Session.UpdatedAt)` — un transcript de 10 s bat donc tout signal plus vieux,
quelle que soit sa source.

**Rouge-avant, re-mesuré par le vérificateur** (worktree détaché sur `2cecfdb`, filtre `InspectionSessions`) :

```
4 échecs / 10 réussites / 14 total
  Un_hook_Working_de_25_min_ne_rend_plus_un_transcript_frais_inconnu       [FAIL]
  Un_hook_WaitingTurn_de_7_h_ne_bat_plus_un_transcript_de_10_s            [FAIL]
  Un_hook_WaitingAttention_de_7_h_ne_bat_plus_un_transcript_de_10_s       [FAIL]
  Permuter_l_ordre_rendu_par_la_source_ne_change_pas_l_affichage          [FAIL]
```

**Trois inversions, pas cinq** — le témoin (`Un_transcript_frais_seul_est_annonce_en_cours`) et le contrôle
(`Un_hook_de_9_h_n_est_plus_candidat_du_tout…`) figuraient bien parmi les **10 verts dès le rouge**. La
qualification honnête des SUMMARY et de `24-VALIDATION.md` est **exacte**.

### Critère 2 — le résultat ne dépend plus de l'ordre : ✓ VÉRIFIÉ (deux couches distinctes)

Deux tests, et ils ne prouvent **pas la même chose** — c'est important :

| Test | Ce qu'il tient | Ce qui le rend probant |
|---|---|---|
| `ArbitrageSessionsTests.Permuter_l_ordre_des_signaux_…` (720 permutations) | l'ordre **des signaux d'une même session** — le vainqueur et les désaccords | le corpus contient `s2` : deux signaux **ex aequo** sur l'horodatage (`T-1 min`) et **divergents** sur l'état |
| `InspectionSessionsTests.Permuter_l_ordre_rendu_par_la_source_…` (6 permutations) | l'ordre **des sessions rendues** par une source, jusqu'à l'écran (`Visibles` ET `Ordonner`) | le corpus `Trois()` contient `b-deux`/`a-un` **ex aequo** sur (urgence, horodatage) |

**Falsification jouée par le vérificateur** (worktree jetable, jamais le dépôt) : `Departager` réduit à la
**fraîcheur seule** (rangs 2 à 5 supprimés) — une implémentation *plausible*, pas un squelette :

```
2 échecs / 21 réussites / 23 total
  Permuter_l_ordre_des_signaux_ne_change_pas_un_seul_etat_ni_un_seul_desaccord [FAIL]
  A_age_egal_la_source_la_plus_specifique_tranche_et_l_ordre_inverse…          [FAIL]
```

⇒ **la garde des 720 permutations n'est pas muette** : le cas ex aequo est porteur, et l'`OrderBy` stable de
LINQ suffirait à faire dépendre le résultat de l'ordre d'entrée si le rang 2 disparaissait. C'était
exactement le doute du plan-checker : il est levé par la mesure.

Le second test, lui, est prouvé probant par sa mesure rouge-avant (4e des 4 échecs ci-dessus).

**Chaîne d'affichage réellement couverte :** `SessionsViewModel.cs:135` fait
`AffichageSessions.Ordonner(_monitor.Read(now))` et `Read(now) => Inspecter(now).Visibles` (1 occurrence).
Le test canonise **exactement** ces deux couches — ce n'est pas une chaîne jumelle.

**Réserve d'honnêteté (pas un défaut) :** dans le test à 720 permutations, `attendu` est produit par la
fonction sous test elle-même, donc `Assert.Equal(attendu, distincts.Single())` est redondant avec
`Assert.Single(distincts)`. Ce qui **épingle réellement le contenu**, ce sont les deux
`Assert.Contains("s1=Working")` / `Assert.Contains("s2=WaitingAttention")` : la première prouve que la
fraîcheur a tranché, la seconde que la règle d'égalité a tranché dans le bon sens. La garde anti-comptage
`Assert.Equal(720, vues)` est présente (**1** occurrence), comme `Assert.Single(distincts)` (**1** dans
chacun des deux fichiers).

### Critère 3 — les désaccords sont lisibles : ✓ VÉRIFIÉ

`DiagnosticService.cs` lignes 558-564 : un bloc **à part**, après les affichées et les masquées.
`lecture.Desaccords` → **3** occurrences (le compte, le `foreach`, le test de vacuité).
Chaque ligne nomme **source retenue + dossier**, **source écartée + dossier**, **écart d'âge** via
`LibelleSourceSession` (`fichier de hook (%APPDATA%\Chronos\sessions)` / `transcript (~/.claude/projects)`)
et `AffichageSessions.Ecart` — une **distance** (« 6 h »), jamais un instant (« il y a 6 h »), avec le zéro
dit « 0 s » pour que l'égalité d'âge se voie.

**Rouge-avant re-mesuré** (worktree sur `2234e65`, filtre `Diagnostic`) : **2 échecs / 28 réussites / 30
total**, exactement les deux tests annoncés. Chiffre des SUMMARY confirmé au test près.

### Critère 4 — la précision ne bat pas la fraîcheur : ✓ VÉRIFIÉ

Ordre total, lisible dans `ArbitrageSessions.Departager` : **1.** fraîcheur ; **2.** à âge strictement égal,
rang de la source (`Hook` = 0 avant `Transcript` = 1) ; **3.** urgence via `AffichageSessions.Urgence`
(**2** appels, jamais recopiée) ; **4.** motif ; **5.** projet. La règle d'égalité est donc **nommée**, et
testée **dans les deux sens d'entrée** par `A_age_egal_la_source_la_plus_specifique_tranche_et_l_ordre_inverse_donne_le_meme_resultat`.
`Un_permission_prompt_plus_ancien_ne_bat_pas_un_signal_plus_recent` tient l'autre moitié : le signal le plus
spécifique du système ne vaut rien s'il date.

**Observation (ℹ️ info, pas un gap).** Le rang 2 précède le rang 3 : à horodatage **rigoureusement identique
à la milliseconde**, un hook ramené à `Unknown` par le seuil `StaleWorking` l'emporterait sur un transcript
`Working` du même instant. Le cas suppose une égalité exacte entre une date de hook (ms d'époque) et une date
de transcript — invraisemblable in vivo, et le critère du ROADMAP (« l'égalité d'âge est tranchée par une
règle explicite et testée ») est tenu quoi qu'il en soit. À garder en tête si la phase 25 ajoute une source
dont les horodatages sont **dérivés** d'une autre (là, l'égalité exacte deviendrait possible).

---

## 3. Les points de vigilance, un par un

| # | Point | Verdict | Preuve refaite par le vérificateur |
|---|---|---|---|
| a | Tests probants ou muets ? | ✓ **PROBANTS** | Corpus `Corpus()` ex aequo sur `s2` et `Trois()` ex aequo sur `b-deux`/`a-un`, **lus dans le code** ; mutation « fraîcheur seule » ⇒ 2 échecs ; permutation du moniteur rouge à `2cecfdb` |
| b | Compte des rouges-avant honnête ? | ✓ **HONNÊTE** | **4** échecs à `2cecfdb`, nommément les 3 inversions + la permutation ; témoin et contrôle parmi les 10 verts. `24-VALIDATION.md` dit exactement cela, sans sur-vendre |
| c | Collision `LibelleSource` réelle ? | ✓ **RÉELLE** | Renommage inverse joué en worktree ⇒ **3 × CS0119**. Additivité : `git diff -U0 f27c55d..HEAD -- DiagnosticService.cs \| grep -c "^-[^-]"` → **0** sur toute la phase |
| d | Fragment illisible = ABSENCE ? | ✓ **OUI** | `SessionMonitor.cs:102` `if (snap is not null) signaux.Add(…)` ; `perimee` distingue « vieux » d'« illisible » ; test vert : `Working`, `FichiersEcartesParAnciennete == 0`, **0** désaccord |
| e | Désaccord ≠ masquage ? | ✓ **OUI** | Bloc séparé ; le test assert dans le MÊME rapport `Sessions AFFICHÉES par le widget : 1` et `Sessions MASQUÉES par un filtre : 0` ; `MotifMasquage` → **3** occurrences, enum toujours à 2 membres |
| f | Acquis non cassés ? | ✓ **INTACTS** | voir tableau ci-dessous |
| g | Horloge injectable ? | ✓ **OUI** | `DateTimeOffset.UtcNow` → **0** dans `ArbitrageSessions.cs`, **0** dans `ArbitrageSessionsTests.cs`, **0** dans `AffichageSessions.cs` ; tests de diagnostic sur `FakeClock(T22)` ; **1** seule occurrence dans `InspectionSessionsTests.cs` — le helper `EcritMaintenant()` **préexistant** (présent dès `f27c55d`), qui existe précisément pour **éviter** un test à retardement face au TTL 6 h de `TreatedStore`. Aucun des 8 tests neufs ne l'appelle |
| h | Suite exécutée par le vérificateur | ✓ **788 / 0 / 788 / 0** | voir § 1 |

### Point f — détail des acquis

| Acquis | Attendu | Mesuré |
|---|---|---|
| `=> Inspecter(now).Visibles;` dans `SessionMonitor.cs` | 1 | **1** ✅ |
| Autres appels à `Inspecter(` dans `src/` | 1 (le diagnostic) | **1** ✅ — aucune ré-implémentation des filtres |
| `new SessionMonitor` dans `DiagnosticService.cs` | 0 | **0** ✅ |
| `?? new ` dans `DiagnosticService.cs` | 2 | **2** ✅ |
| `StaleWorking` 20 min / `DropAfter` 8 h | inchangés | **1 occurrence chacun, inchangés** ✅ |
| `git diff --stat f27c55d..HEAD` sur `SessionTreatmentTracker.cs` / `TreatedStore.cs` / `ArchiveStore.cs` | vide | **vide** ✅ |
| `System.IO` / `FileStream` / `Directory.` dans `ArbitrageSessions.cs` | 0 | **0** ✅ |
| Résidu `MUTANT` dans `src/` + `tests/` | 0 | **0** ✅ |
| `TODO`/`FIXME`/`NotImplementedException` dans les 5 fichiers de service touchés | 0 | **0** ✅ |

### Additivité et périmètre du diff, sur la phase ENTIÈRE (`f27c55d..HEAD`)

```
 src/Chronos/Services/AffichageSessions.cs      |  11 ++
 src/Chronos/Services/ArbitrageSessions.cs      | 125 +++++++
 src/Chronos/Services/DiagnosticService.cs      |  31 ++
 src/Chronos/Services/LectureSessions.cs        |   8 +-
 src/Chronos/Services/SessionMonitor.cs         |  39 +-
 tests/… (6 fichiers)                           | 523 +
 11 fichiers, 720 insertions(+), 17 deletions(-)
```

Les **17** suppressions sont intégralement localisées et justifiées, ligne à ligne :

- `LectureSessions.cs` : 1 ligne — ajout du 4e champ positionnel `Desaccords` (la ligne
  `int FichiersEcartesParAnciennete);` devient `…,` + une ligne).
- `SessionMonitor.cs` : la refonte d'`Inspecter` (collecte + délégation) et le retrait de
  `using System.Linq;` devenu inutile.
- `SessionsTests.cs` : le **seul** renommage de la phase
  (`…_hook_prioritaire` → `…_le_plus_recent_gagne`) + 2 commentaires réécrits en conséquence. Le test reste
  vert **pour la bonne raison** : dans son montage le hook est plus récent d'une minute.

`DiagnosticService.cs`, `DiagnosticServiceTests.cs`, `AffichageSessions.cs`, `AffichageSessionsTests.cs`,
`GardesPerimetreTests.cs`, `InspectionSessionsTests.cs` : **0 suppression chacun** sur toute la phase.
L'exigence d'additivité du plan 24-02 est tenue — et au-delà de la seule tâche : sur la phase entière.

---

## 4. Couverture des exigences

| Exigence | Plan | Description | Statut | Preuve |
|---|---|---|---|---|
| **FUS-01** | 24-01 | Un signal ne peut en écraser un autre que s'il est plus récent, jamais par ordre d'enregistrement | ✓ **SATISFAITE** | `ArbitrageSessions.Trancher` + 9 tests ; `byId[` → 0 ; 3 inversions rouges-avant re-mesurées |
| **FUS-02** | 24-02 | Les désaccords entre sources sont traçables dans le diagnostic | ✓ **SATISFAITE** | Bloc « Désaccords entre sources » + 2 tests ; rouge-avant 2/28/30 re-mesuré |

`REQUIREMENTS.md` : les deux cases sont `- [x]`, le tableau de traçabilité porte `Complete` pour les deux,
**aucun `Pending`** ne subsiste sur la phase 24. Aucune exigence **orpheline** : la phase 24 ne revendique
que FUS-01 et FUS-02, et la carte de traçabilité ne lui en attribue pas d'autre.

---

## 5. Anti-patterns

| Fichier | Motif cherché | Trouvé | Sévérité |
|---|---|---|---|
| Les 5 services touchés | `TODO`/`FIXME`/`XXX`/`HACK`/`PLACEHOLDER`/`NotImplementedException` | **0** | — |
| `src/` + `tests/` | résidu `MUTANT` de la falsification 24-01 | **0** | — |
| `src/` + `tests/` | résidu de dump (`D1>>`, `D2>>`, `DUMP`) | **0** | — |
| `ArbitrageSessions.cs` | E/S, horloge, chemin | **0** | — |

Aucun stub. `LectureSessions.Desaccords` est alimenté par de vraies données dès 24-01 et **affiché** depuis
24-02 — la chaîne va de la collecte jusqu'à une ligne du rapport, sans maillon creux
(trace de données de bout en bout : `ISessionSource` + fichiers `%APPDATA%\Chronos\sessions` →
`SignalSession` → `Trancher` → `LectureSessions.Desaccords` → `DiagnosticService` ligne 559).

---

## 6. Écarts relevés entre les SUMMARY et le code (aucun n'est bloquant)

1. **Numéros de ligne de la collision `LibelleSource`.** Le SUMMARY 24-02 écrit « lignes **724-728** » ;
   les occurrences réelles de `Chronos.Text.LibelleSource` dans `DiagnosticService.cs` sont aux lignes
   **729, 730 et 733**, et ce sont exactement ces trois-là que le compilateur signale (`CS0119`). Dérive
   cosmétique de citation ; le **fond** — 3 erreurs sur des lignes **préexistantes**, donc intouchables sous
   la règle d'additivité — est **exact et reproduit**.
2. **« comparent un résultat canonisé à une valeur attendue » (720 permutations).** La valeur attendue est
   **dérivée** de la fonction sous test, pas écrite en dur ; l'épinglage du contenu est assuré par deux
   `Assert.Contains` de sous-chaîne. C'est suffisant, et la mutation « fraîcheur seule » le confirme — mais
   la formulation « valeur attendue » mérite cette précision.
3. **Le test de permutation du moniteur n'épingle pas la séquence obtenue** (il n'assert que
   `Assert.Single(...)` sur les deux ensembles). Sa non-mutité est établie autrement : par sa mesure
   rouge-avant, que le vérificateur a reproduite. Garde valide, un cran moins forte que sa sœur.

---

## 7. Sécurité — invariants relevés AVANT et APRÈS la vérification

| Invariant | Attendu | Avant | Après |
|---|---|---|---|
| `%APPDATA%\Chronos\sessions` | 66 entrées, jamais touché | **66** | **66** ✅ |
| `%APPDATA%\Chronos\archived.json` | 84 octets | **84** | **84** ✅ |
| `%APPDATA%\Chronos\oauth.dat` (taille seule, **mtime jamais consulté**) | 518 octets | **518** | **518** ✅ |
| Overlay `Chronos-v3.0.2.exe` pid 119412 | vivant, **ni lancé ni tué** (`tasklist` seul) | **vivant** | **vivant** ✅ |
| Dépôt principal | propre, sur `7168e71` | propre | **propre** ✅ |
| `git worktree list` | 1 seul (le dépôt) | 1 | **1** ✅ — le worktree jetable a été supprimé et `git worktree prune` passé |
| Requêtes réseau réelles | 0 | — | **0** ✅ |

Le worktree de mesure a été créé **hors du dépôt**, dans le scratchpad de session ; toutes les mutations de
falsification y ont vécu et y sont mortes. **Aucun fichier du dépôt n'a été modifié par la vérification.**

---

## 8. Ce qui est prouvé mécaniquement vs ce qui ne le sera qu'in vivo

### Prouvé mécaniquement — ici, maintenant, par le vérificateur

- Les 4 critères du ROADMAP, chacun par un ou deux tests nommés, **dont 4 re-mesurés rouges** contre le code
  d'avant le correctif (3 inversions + la permutation du moniteur) et 2 autres contre le rapport d'avant le
  bloc de désaccords.
- L'indépendance à l'ordre, **falsifiée puis rétablie** : une implémentation « fraîcheur seule » fait tomber
  la garde des 720 permutations.
- La lisibilité du désaccord dans le texte **réellement produit** par `DiagnosticService`.
- L'additivité (0 suppression sur `DiagnosticService.cs` pour la phase entière), le non-débordement sur le
  périmètre des phases 25-26, la pureté de l'arbitrage, l'horloge non consultée.
- 788 tests, 0 échec, deux exécutions consécutives.

### Impossible avant republication de l'exe — **à ne pas confondre avec un défaut**

Les hooks de `~/.claude/settings.json` pointent `Chronos-v3.0.2.exe` — **l'exe actuellement en cours
d'exécution (pid 119412), antérieur à tout le milestone v1.6**. Rien de ce qui a été livré en phases 21 à 24
ne tourne aujourd'hui sur la machine de l'utilisateur. Les 4 observations in vivo listées dans le frontmatter
(`human_verification`) et dans `24-VALIDATION.md` § « À VÉRIFIER PAR L'UTILISATEUR » ne peuvent donc **pas**
être faites maintenant, et leur absence n'enlève rien au verdict mécanique.

Rappel des reliquats hérités à présenter **groupés** en fin de milestone : purge réelle d'`archived.json`
(21), comparaison ligne à ligne widget ↔ rapport (22), résorption du magasin de 66 entrées (23) —
plus, désormais, les 4 observations de la phase 24.

---

## Verdict

**`complete`** — les 4 critères du ROADMAP sont **vérifiés dans le code**, pas dans les SUMMARY. Les comptes
annoncés (788 tests, 4 rouges-avant à la tâche 2, 9 à la tâche 1, 2 en 24-02, 0 suppression sur
`DiagnosticService.cs`) ont tous été **remesurés indépendamment** et sont **exacts**. Les gardes ne sont pas
muettes : la plus contestée a été **falsifiée par mutation** et est tombée. Aucun gap. Aucun stub.

Reste, et seulement cela : **voir de ses yeux, après republication de l'exe**.

---

_Vérifié le 2026-09-12 — Claude (gsd-verifier), vérification goal-backward, mesures refaites intégralement._
