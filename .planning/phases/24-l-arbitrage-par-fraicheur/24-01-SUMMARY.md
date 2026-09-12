---
phase: 24-l-arbitrage-par-fraicheur
plan: 01
subsystem: sessions
tags: [arbitrage, fraicheur, fusion-sources, permutation, gardes-de-non-retour, xunit]

requires:
  - phase: 21-la-source-unique
    provides: "TranscriptSessionSource : ISessionSource, gardes de périmètre"
  - phase: 22-l-instrument-partage
    provides: "Read(now) => Inspecter(now).Visibles, LectureSessions, partage d'instance du moniteur"
  - phase: 23-le-cycle-de-vie-du-magasin
    provides: "écriture directe (dont la contrepartie — le fragment tronqué — est traitée ici)"
provides:
  - "ArbitrageSessions : service PUR qui tranche entre signaux sur la FRAÎCHEUR, avec règle d'égalité nommée"
  - "SignalSession / SourceSession / DesaccordSources / ResultatArbitrage : le vocabulaire de l'arbitrage"
  - "LectureSessions.Desaccords : 4e champ, chemin de FUS-02 vers le diagnostic (plan 24-02)"
  - "SessionMonitor.Inspecter réduit à une COLLECTE de signaux + une délégation d'arbitrage"
  - "Deux gardes de non-retour : l'idiome d'écrasement ne revient pas, l'arbitrage reste pur"
affects: [24-02-diagnostic-des-desaccords, 25-contrat-d-evenements, 26-le-traite-observe]

tech-stack:
  added: []
  patterns:
    - "Arbitrage par ordre TOTAL sur le CONTENU : la permutation devient vraie par construction, pas par chance"
    - "Un fragment illisible = ABSENCE de signal (ni vieux signal daté au minimum, ni signal retenu)"
    - "Mutation de falsification par ALIAS temporaire : le dépôt reste compilable pendant la mesure"

key-files:
  created:
    - src/Chronos/Services/ArbitrageSessions.cs
    - tests/Chronos.Tests/ArbitrageSessionsTests.cs
  modified:
    - src/Chronos/Services/SessionMonitor.cs
    - src/Chronos/Services/LectureSessions.cs
    - tests/Chronos.Tests/InspectionSessionsTests.cs
    - tests/Chronos.Tests/SessionsTests.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs

key-decisions:
  - "La FRAÎCHEUR tranche en premier ; la spécificité de la source ne départage qu'à âge strictement égal."
  - "L'ordre total épuise tous les champs de SessionSnapshot hors l'identifiant : deux ex aequo résiduels sont identiques champ pour champ, donc l'ordre d'entrée ne peut plus décider."
  - "La séquence RENDUE est ordonnée par identifiant, pas seulement les états : sinon « le résultat ne dépend plus de l'ordre » ne vaudrait que pour la moitié du résultat."
  - "Un doublon (deux sources d'accord) n'est PAS un désaccord — sinon les vraies contradictions se noieraient dans le bruit."
  - "Un fragment tronqué n'est pas déposé dans l'arbitrage : le dater au minimum en ferait une déposition fabriquée."

patterns-established:
  - "Test de PERMUTATION exhaustive (720 ordres) avec canonisation en chaîne unique + deux assertions anti-garde-muette."
  - "Corpus de permutation choisi EX AEQUO pour être rouge avant le correctif : un corpus aux couples distincts aurait été une garde muette."

requirements-completed: [FUS-01]

duration: 41min
completed: 2026-09-12
---

# Phase 24 Plan 01 : L'arbitrage par fraîcheur Summary

**Un transcript de 10 secondes bat désormais un fichier de hook de 7 heures : `SessionMonitor.Inspecter` ne fusionne plus par réaffectation d'entrée indexée, il COLLECTE des signaux et délègue à `ArbitrageSessions.Trancher`, dont le résultat est identique pour les 720 permutations de son corpus.**

## Performance

- **Duration:** 41 min
- **Started:** 2026-09-12T17:29Z
- **Completed:** 2026-09-12T18:10Z
- **Tasks:** 3/3
- **Files modified:** 7 (2 créés, 5 modifiés)

## Accomplishments

- **FUS-01 livré.** L'arbitrage entre sources se fait sur l'instant que chaque signal porte, plus sur l'ordre d'enregistrement des sources dans le code. Les trois inversions mesurées du 2026-09-12 ont disparu.
- **Le critère n°2 de la phase est prouvé deux fois** : 720 permutations au niveau de l'arbitrage pur, 6 permutations sur un corpus EX AEQUO au niveau du moniteur — une seule sortie distincte dans les deux cas.
- **Le critère n°4 est prouvé** : un `permission_prompt` plus ancien perd contre un signal plus récent ; l'égalité d'âge est tranchée par une règle nommée, testée dans les deux sens d'entrée.
- **`LectureSessions` porte les désaccords**, alimentés par l'arbitrage — le plan 24-02 n'a plus qu'à les afficher.
- **Deux gardes de non-retour**, falsifiées par mutation réelle puis révoquées.

## Task Commits

1. **Task 1 (TDD) — ROUGE** : `8e2db74` — `test(24-01): l'arbitrage par fraicheur, ses permutations et sa regle d'egalite`
2. **Task 1 (TDD) — VERT** : `a4f04b0` — `feat(24-01): l'arbitrage se fait sur la FRAICHEUR, plus sur l'ordre d'insertion (FUS-01)`
3. **Task 2 (a) — le champ** : `cc509f3` — `feat(24-01): LectureSessions porte les desaccords de sources`
4. **Task 2 (b) — ROUGE** : `2cecfdb` — `test(24-01): les cinq lignes mesurees, le fragment et la permutation au niveau du moniteur`
5. **Task 2 (c) — VERT** : `18a3109` — `feat(24-01): Inspecter collecte des signaux et delegue l'arbitrage (FUS-01)`
6. **Task 3 — gardes** : `2cd8551` — `test(24-01): deux gardes de non-retour sur l'arbitrage par fraicheur`

Les six commits compilent (`0 erreur / 0 avertissement`) ; aucun « échec de build attendu » n'a été utilisé.

## Files Created/Modified

- `src/Chronos/Services/ArbitrageSessions.cs` **(créé, 125 lignes)** — `SourceSession`, `SignalSession`, `DesaccordSources`, `ResultatArbitrage`, `ArbitrageSessions.Trancher`. Service PUR.
- `tests/Chronos.Tests/ArbitrageSessionsTests.cs` **(créé, 207 lignes, 9 `[Fact]`)** — dont la permutation exhaustive.
- `src/Chronos/Services/SessionMonitor.cs` — `Inspecter` refondu (collecte + délégation) ; `using System.Linq;` retiré (plus aucun usage) ; `TryRead`, `StaleWorking`, `DropAfter`, `Read`, le constructeur et le bloc de filtres sont inchangés.
- `src/Chronos/Services/LectureSessions.cs` — 4e champ positionnel `Desaccords` + sa XML-doc (« ce n'est PAS un motif de masquage »).
- `tests/Chronos.Tests/InspectionSessionsTests.cs` — +8 `[Fact]` (6 → 14).
- `tests/Chronos.Tests/SessionsTests.cs` — un renommage, aucun autre changement.
- `tests/Chronos.Tests/GardesPerimetreTests.cs` — +2 `[Fact]` (8 → 10).

## Comptes de tests — avant / après

| Moment | échecs | réussites | total | durée |
|---|---|---|---|---|
| **Baseline d'entrée de phase** | 0 | **763** | **763** | 6 s |
| Après ROUGE Task 1 (`8e2db74`) | **9** | 763 | 772 | 5 s |
| Après VERT Task 1 (`a4f04b0`) | 0 | 772 | 772 | 5 s |
| Après Task 2 (a) (`cc509f3`) | 0 | 772 | 772 | 5 s |
| Après ROUGE Task 2 (`2cecfdb`) | **4** | 776 | 780 | 4 s |
| Après VERT Task 2 (`18a3109`) | 0 | 780 | 780 | 4 s |
| **Après Task 3 (`2cd8551`) — exécution 1** | **0** | **782** | **782** | 4 s |
| **Après Task 3 — exécution 2 (consécutive)** | **0** | **782** | **782** | 4 s |

**+19 tests exactement** (9 + 8 + 2 = 19 ; 763 + 19 = 782). **Aucun test supprimé**, aucun ignoré.

## L'étape ROUGE, mesurée

### Task 1 — filtre `FullyQualifiedName~ArbitrageSessions`, contre le squelette `NotImplementedException`

**9 échecs / 0 réussite / 9 total.** Build **0 erreur / 0 avertissement**. Les 9 tests tombés :

1. `Un_hook_de_7_h_perd_contre_un_transcript_de_10_secondes`
2. `Un_signal_perime_de_25_minutes_n_ecrase_plus_un_transcript_frais`
3. `Un_permission_prompt_plus_ancien_ne_bat_pas_un_signal_plus_recent`
4. `A_age_egal_la_source_la_plus_specifique_tranche_et_l_ordre_inverse_donne_le_meme_resultat`
5. `Permuter_l_ordre_des_signaux_ne_change_pas_un_seul_etat_ni_un_seul_desaccord`
6. `Un_desaccord_nomme_la_session_les_deux_sources_les_deux_etats_et_l_ecart_d_age`
7. `Deux_sources_d_accord_ne_produisent_aucun_desaccord`
8. `Aucun_ecart_d_age_n_est_negatif_sur_tout_le_corpus`
9. `L_entree_vide_rend_deux_listes_vides`

Sur la suite complète au même commit : **9 échecs / 763 réussites / 772 total** — c'est-à-dire que **les 763 tests préexistants étaient tous verts dès le rouge**. Le rouge est bien localisé au comportement neuf, jamais à une régression.

### Task 2 — filtre `FullyQualifiedName~InspectionSessions`, contre le code d'avant le correctif

**4 échecs / 10 réussites / 14 total.** Suite complète : **4 échecs / 776 réussites / 780 total**.

Les 4 tombés — exactement ceux prédits par `24-VALIDATION.md`, ni plus ni moins :

1. `Un_hook_Working_de_25_min_ne_rend_plus_un_transcript_frais_inconnu`
2. `Un_hook_WaitingTurn_de_7_h_ne_bat_plus_un_transcript_de_10_s`
3. `Un_hook_WaitingAttention_de_7_h_ne_bat_plus_un_transcript_de_10_s`
4. `Permuter_l_ordre_rendu_par_la_source_ne_change_pas_l_affichage` *(le 4e rouge-avant hors tableau)*

Les 10 verts dès le rouge : les 6 tests préexistants d'`InspectionSessionsTests`, plus
`Un_transcript_frais_seul_est_annonce_en_cours` (**témoin**),
`Un_hook_de_9_h_n_est_plus_candidat_du_tout_et_ne_produit_aucun_desaccord` (**contrôle**),
`Un_fragment_illisible_est_une_ABSENCE_de_signal_jamais_un_vieux_signal` et
`Un_hook_sans_updated_at_ne_gagne_jamais`.

## Les cinq lignes mesurées du 2026-09-12 — qualification HONNÊTE, vérifiée

| Ligne du relevé | Test | Qualification **mesurée** |
|---|---|---|
| transcript seul | `Un_transcript_frais_seul_est_annonce_en_cours` | **témoin** — vert avant ET après (confirmé au run de ROUGE) |
| + hook `Working` de 25 min | `Un_hook_Working_de_25_min_ne_rend_plus_un_transcript_frais_inconnu` | **rouge avant le correctif** ✅ |
| + hook `WaitingTurn` de 7 h | `Un_hook_WaitingTurn_de_7_h_ne_bat_plus_un_transcript_de_10_s` | **rouge avant le correctif** ✅ |
| + hook `WaitingAttention` de 7 h | `Un_hook_WaitingAttention_de_7_h_ne_bat_plus_un_transcript_de_10_s` | **rouge avant le correctif** ✅ |
| + hook `WaitingTurn` de 9 h (> `DropAfter`) | `Un_hook_de_9_h_n_est_plus_candidat_du_tout_et_ne_produit_aucun_desaccord` | **contrôle de non-régression** — vert avant ET après ; seule l'assertion « 0 désaccord » est neuve |

**Trois inversions étaient rouges, pas cinq** — la prédiction du plan est confirmée par la mesure, sans ajustement.

**Quatrième rouge-avant, hors tableau :** `Permuter_l_ordre_rendu_par_la_source_ne_change_pas_l_affichage`. Son corpus EX AEQUO (`b-deux` et `a-un`, tous deux `Working` à −1 min) le rendait bien rouge : l'entrée indexée par identifiant rendait l'ordre d'insertion et le tri d'affichage est stable. À inscrire à la ligne « Qualification » de `24-VALIDATION.md`.

## Le fragment — mesuré

`Un_fragment_illisible_est_une_ABSENCE_de_signal_jamais_un_vieux_signal` : fichier tronqué
`{"session_id":"session-mesuree","project":"P","activ` pour la MÊME session qu'un transcript frais →
état `Working`, `FichiersEcartesParAnciennete == 0`, **0 désaccord**. Le fragment n'est ni un vieux signal
qui perdrait, ni un signal retenu : il n'entre pas dans l'arbitrage.

`Un_hook_sans_updated_at_ne_gagne_jamais` : JSON valide sans `updated_at` → transcript retenu,
`FichiersEcartesParAnciennete == 1`, 0 désaccord (il tombe sous `DropAfter`, il n'est jamais candidat).

## Mutation de falsification — jouée, mesurée, révoquée

**Mutation** (par alias temporaire, pour que le dépôt reste compilable) :
`ArbitrageSessions.Trancher(` → `ArbitrageSessions.TrancherMUTANT(` dans `SessionMonitor.cs`,
plus `public static ResultatArbitrage TrancherMUTANT(IEnumerable<SignalSession> s) => Trancher(s);`
dans `ArbitrageSessions.cs`.

**Mesure, filtre `GardesPerimetre` : 2 échecs / 8 réussites / 10 total.** Les **deux** tests tombés sont
exactement les deux attendus :

1. `Le_moniteur_n_arbitre_plus_par_ordre_d_insertion` — l'appel a changé de nom.
2. `L_arbitrage_ne_connait_ni_horloge_ni_magasin_ni_chemin` — l'alias ajoute une seconde porte d'entrée publique statique et casse son `Assert.Single(declarees)`.

**Révocation prouvée :**

| Preuve | Attendu | Mesuré |
|---|---|---|
| `git diff --stat -- src/Chronos/Services/` après révocation | vide | **vide** ✅ |
| `grep -rlF "MUTANT" --include=*.cs src/Chronos tests/Chronos.Tests \| wc -l` | 0 | **0** ✅ |
| Filtre `GardesPerimetre` après révocation | 10 / 0 échec | **10 réussites / 0 échec** ✅ |

## Critères d'acceptation — valeurs de `grep` PRÉDITES vs RÉELLEMENT OBTENUES

### Task 1

| Mesure | Prédit | Obtenu |
|---|---|---|
| `wc -l src/Chronos/Services/ArbitrageSessions.cs` | ≥ 70 | **125** ✅ |
| `wc -l tests/Chronos.Tests/ArbitrageSessionsTests.cs` | ≥ 130 | **207** ✅ |
| `grep -c "\[Fact\]\|\[Theory\]"` (tests) | ≥ 9 | **9** ✅ |
| `grep -cF "public static ResultatArbitrage Trancher"` | 1 | **1** ✅ |
| `grep -cF "DateTimeOffset.UtcNow"` (service) | 0 | **0** ✅ |
| `grep -cF "DateTimeOffset.UtcNow"` (tests) | 0 | **0** ✅ |
| `grep -cE "FromUnixTimeMilliseconds\|FromUnixTimeSeconds\|DateTimeOffset\.TryParse"` (service) | 0 | **0** ✅ |
| `grep -cF "AffichageSessions.Urgence("` | 2 | **2** ✅ |
| `grep -cE "WaitingAttention\s*=>\s*0\|WaitingTurn\s*=>\s*1\|Working\s*=>\s*2"` | 0 | **0** ✅ |
| `grep -cE "System\.IO\|FileStream\|Directory\."` | 0 | **0** ✅ |
| `grep -cF "Assert.Equal(720, vues)"` | 1 | **1** ✅ |
| `grep -cF "Assert.Single(distincts)"` | 1 | **1** ✅ |

### Task 2

| Mesure | Prédit | Obtenu |
|---|---|---|
| `grep -cF "ArbitrageSessions.Trancher("` (`SessionMonitor.cs`) | 1 | **1** ✅ |
| `grep -cF "arbitrage.Desaccords"` | 1 | **1** ✅ |
| `grep -cF "byId["` | 0 | **0** ✅ |
| `grep -cF "Dictionary<string, SessionSnapshot>"` | 0 | **0** ✅ |
| `grep -cF "new SignalSession(SourceSession.Transcript"` | 1 | **1** ✅ |
| `grep -cF "new SignalSession(SourceSession.Hook"` | 1 | **1** ✅ |
| `grep -cF "=> Inspecter(now).Visibles;"` | 1 | **1** ✅ |
| `grep -cF "StaleWorking = System.TimeSpan.FromMinutes(20)"` | 1 | **1** ✅ |
| `grep -cF "DropAfter = System.TimeSpan.FromHours(8)"` | 1 | **1** ✅ |
| `grep -cF "IReadOnlyList<DesaccordSources> Desaccords"` (`LectureSessions.cs`) | 1 | **1** ✅ |
| `grep -cF "hook_prioritaire"` (`SessionsTests.cs`) | 0 | **0** ✅ |
| `grep -cF "Monitor_fusionne_transcripts_et_hooks_le_plus_recent_gagne"` | 1 | **1** ✅ |
| `grep -c "\[Fact\]"` (`InspectionSessionsTests.cs`) | ≥ 14 | **14** ✅ |
| `grep -cF "Assert.Single(distincts)"` (`InspectionSessionsTests.cs`) | 1 | **1** ✅ |
| `git diff --stat` de la tâche | 4 fichiers seulement | **`LectureSessions.cs`, `SessionMonitor.cs`, `InspectionSessionsTests.cs`, `SessionsTests.cs`** ✅ |
| `git diff --stat` de `SessionTreatmentTracker.cs` / `TreatedStore.cs` / `ArchiveStore.cs` / `DiagnosticService.cs` | vide | **vide** ✅ |

### Task 3

| Mesure | Prédit | Obtenu |
|---|---|---|
| `grep -cF "Le_moniteur_n_arbitre_plus_par_ordre_d_insertion"` | 1 | **1** ✅ |
| `grep -cF "L_arbitrage_ne_connait_ni_horloge_ni_magasin_ni_chemin"` | 1 | **1** ✅ |
| Filtre `GardesPerimetre` | 10 tests, 0 échec | **10 / 0** ✅ |
| `git diff --stat` de la tâche | `GardesPerimetreTests.cs` seul | **`GardesPerimetreTests.cs` seul (+62)** ✅ |

**Aucun écart entre valeur prédite et valeur obtenue.** Aucune valeur n'a été « absorbée ».

## Acquis des phases précédentes — revérifiés

| Acquis | Attendu | Mesuré |
|---|---|---|
| Phase 22 — `=> Inspecter(now).Visibles;` dans `SessionMonitor.cs` | 1 | **1** ✅ |
| Phase 22 — `new SessionMonitor` dans `DiagnosticService.cs` | 0 | **0** ✅ |
| Phase 22 — `_moniteurSessions.Inspecter(_clock.UtcNow)` | 1 | **1** ✅ |
| Phase 20 — `?? new ` dans `DiagnosticService.cs` | 2 | **2** ✅ |
| Phase 25 non anticipée — `StaleWorking` 20 min / `DropAfter` 8 h | inchangés | **inchangés** ✅ |
| Phase 26 non anticipée — diff `SessionTreatmentTracker` / `TreatedStore` / `ArchiveStore` | vide | **vide** ✅ |
| `NormalisationUnique` | 3 | **3 / 0 échec** ✅ |
| `ServicesLayerPurity` | 2 | **2 / 0 échec** ✅ |
| `CompositionRoot` | 5 | **5 / 0 échec** ✅ |
| `GardesDoctrine` | 8 | **8 / 0 échec** ✅ |
| `GardesPerimetre` | 10 | **10 / 0 échec** ✅ |

## Invariants de sécurité — mesurés AVANT et APRÈS

| Invariant | Attendu | Avant | Après |
|---|---|---|---|
| `%APPDATA%\Chronos\sessions` | 66 entrées | **66** | **66** ✅ |
| `%APPDATA%\Chronos\archived.json` | 84 octets | **84** | **84** ✅ |
| `%APPDATA%\Chronos\oauth.dat` (taille seule, jamais le mtime) | 518 octets | **518** | **518** ✅ |
| Overlay `Chronos-v3.0.2.exe` pid 119412 | vivant, ni lancé ni tué | **vivant** (`tasklist` seul) | **vivant** ✅ |
| `git diff -- '*.csproj'` | vide | — | **vide** ✅ |
| `git status --porcelain` | rien hors `src/`, `tests/`, `.planning/` | — | **vide (tout commité)** ✅ |
| Dépendances NuGet ajoutées | 0 | — | **0** ✅ |
| Requêtes réseau réelles depuis un test | 0 | — | **0** ✅ |
| Tests écrivant hors de `Path.GetTempPath()` | 0 | — | **0** (garde anti-accident `Assert.StartsWith` conservée) ✅ |

## Renommage — nommément

**Un seul renommage, celui prévu et autorisé :**

`tests/Chronos.Tests/SessionsTests.cs` :
`Monitor_fusionne_transcripts_et_hooks_hook_prioritaire` → **`Monitor_fusionne_transcripts_et_hooks_le_plus_recent_gagne`**.

Le test reste **vert** : dans son montage, le hook est plus RÉCENT d'une minute que le transcript — il gagne
donc, mais désormais à ce titre et à ce titre seul. Deux commentaires ont été réécrits en conséquence
(`// hook prioritaire` → `// le plus RÉCENT gagne — ici c'est le hook`, et le commentaire d'en-tête).
**Aucun autre test de ce fichier n'a été modifié ni supprimé. Aucun autre renommage nulle part.**

## Risque contrôlé : l'ordre brut rendu par `Inspecter`

`ArbitrageSessions.Trancher` rend ses retenus triés par identifiant, là où l'ancienne fusion rendait l'ordre
d'insertion. Le plan demandait d'inscrire nominativement tout test préexistant qui aurait échoué pour cette
raison : **aucun n'a échoué.** La suite complète est passée de 776/780 à 780/780 au commit de refonte, sans
qu'une seule ligne d'un test préexistant ait eu à être touchée. Le pari du plan — « le widget et le rapport
passent tous deux par `AffichageSessions.Ordonner` » — est donc confirmé par la mesure.

## Deviations from Plan

### 1. `direct.Reverse()` ne compile pas sur un tableau — corrigé en `Enumerable.Reverse(direct)`

- **Rule 3 (blocage)** — **Trouvé pendant :** Task 1, étape ROUGE.
- **Problème :** le plan écrivait `ArbitrageSessions.Trancher(direct.Reverse().ToArray())`. Sur un
  `SignalSession[]`, `Reverse()` résout vers `MemoryExtensions.Reverse<T>(this Span<T>)` (le tableau se
  convertit implicitement en `Span`), qui renvoie `void` → `error CS0023`.
- **Correction :** `ArbitrageSessions.Trancher(Enumerable.Reverse(direct).ToArray())` — l'intention du test
  (rejouer l'entrée dans l'ordre inverse) est strictement conservée.
- **Fichier :** `tests/Chronos.Tests/ArbitrageSessionsTests.cs` — **Commit :** `8e2db74`.

### 2. Une seule garde anti-muette de comptage pour les deux séquences canonisées (Task 2)

- **Trouvé pendant :** Task 2, écriture de `Permuter_l_ordre_rendu_par_la_source_ne_change_pas_l_affichage`.
- **Le plan disait :** « `Assert.Equal(6, vues)` et `Assert.Single(distincts)` sur chacune » (des deux
  séquences : `Visibles` et `Ordonner`).
- **Fait :** les deux séquences sont canonisées dans **la même** boucle, donc un **seul** compteur `vues` et
  un seul `Assert.Equal(6, vues)` ; en revanche `Assert.Single` est bien asserté **sur chacun** des deux
  ensembles (`distincts` pour `Visibles`, `distinctsAffiches` pour `Ordonner`).
- **Pourquoi :** deux compteurs incrémentés dans la même boucle seraient la même mesure écrite deux fois —
  une garde anti-muette ne gagne rien à être dupliquée. Par ailleurs le critère d'acceptation exige
  `grep -cF "Assert.Single(distincts)"` → **1** : un second ensemble nommé différemment était donc requis.
- **Fichier :** `tests/Chronos.Tests/InspectionSessionsTests.cs` — **Commit :** `2cecfdb`.

### 3. `using System.Linq;` retiré de `SessionMonitor.cs`

- **Prévu par le plan** (« retirer seulement si plus aucun usage ne subsiste »). Vérifié : après la refonte,
  plus aucun opérateur LINQ n'y subsiste (`.ToList()` sur `byId.Values` était le dernier). Build après
  retrait : **0 avertissement**.

**Aucune autre déviation.** Le corps d'`ArbitrageSessions.Trancher`, le comparateur, la XML-doc de doctrine,
le corps refondu d'`Inspecter` et les deux gardes ont été écrits **tels quels** depuis le plan. Aucun seuil,
aucun corpus de test n'a été « amélioré ».

## Known Stubs

Aucun. `LectureSessions.Desaccords` est alimenté par de vraies données dès ce plan (mesuré par
`Un_hook_WaitingAttention_de_7_h_ne_bat_plus_un_transcript_de_10_s`, qui assert l'écart d'âge réel).
Son **affichage** dans le rapport de diagnostic est le sujet explicite du plan 24-02, hors périmètre ici.

## À VÉRIFIER PAR L'UTILISATEUR

Les tests prouvent le mécanisme, pas ce que l'écran montre. **Rien de ce plan ne s'exécute tant que l'exe
n'est pas republié** : les hooks de `~/.claude/settings.json` pointent `Chronos-v3.0.2.exe`, antérieur à tout
le milestone v1.6. La vérification in vivo (critères 1 à 4 de `24-VALIDATION.md`) reste à faire en fin de
milestone, après republication.

## Self-Check: PASSED

- `src/Chronos/Services/ArbitrageSessions.cs` — FOUND
- `tests/Chronos.Tests/ArbitrageSessionsTests.cs` — FOUND
- Commits `8e2db74`, `a4f04b0`, `cc509f3`, `2cecfdb`, `18a3109`, `2cd8551` — tous FOUND dans `git log`
- Suite complète : 782/782, deux exécutions consécutives au même total
