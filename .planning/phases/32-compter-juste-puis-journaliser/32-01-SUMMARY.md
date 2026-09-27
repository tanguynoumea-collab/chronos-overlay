---
phase: 32-compter-juste-puis-journaliser
plan: 01
subsystem: transcripts / correction par delta (comptage des tokens)
tags: [CPT-01, DedupUsage, message.id, requestId, max-par-champ, TranscriptActivityProvider, TokensDepuisReleve, fixture-reelle, garde-structurelle]

# Dependency graph
requires:
  - phase: 16-19 (DEL-01..04, EXA-04)
    provides: TranscriptActivityProvider (passe disque unique, streaming tolérant), TranscriptActivityLog.Since (borne basse exclusive), DoctrineFraicheur.Statuer (plancher = activite.Tokens), LastExactUsageProvider (tête)
  - phase: 32 (recherche, §CPT-01)
    provides: la règle mesurée (max par champ = dernière ligne, 12 835 / 12 835 ids divergents), 491 ids dans 2 à 3 fichiers, gabarit de fixture réelle anonymisée
provides:
  - Services/DedupUsage.cs — helper UNIQUE et pur de somme des usages : max par champ par identifiant (message.id, repli requestId), premier timestamp, lignes sans identifiant comptées telles quelles ; LireUsage = seul lecteur de message.usage ; compteurs MessagesDistincts / LignesVues / LignesSansIdentifiant
  - TranscriptActivityProvider.ReadAsync dédoublonné — UN DedupUsage pour toute la passe (D-32-03), matérialisation puis tri global ; SumUsageTokens / Field supprimés
  - Fixture réelle anonymisée tests/Chronos.Tests/TestData/transcript-multi-blocs.jsonl (7 lignes, clés 2.1.281 réelles) : 77 056 attendus
  - Preuve d'héritage : TokensDepuisReleve == 77 056 par la tête + le VRAI provider, sans une ligne de doctrine changée (DedupHeritageDeltaTests)
  - Garde structurelle GardesDedupUsageTests : aucun littéral de usage hors du helper (Services/** + Models/**, récursif, ≥ 40 fichiers) ; SumUsageTokens absent ; new DedupUsage() unique et AVANT foreach
affects: [33 (agrégats de tokens, curseurs : tout lecteur de transcripts passe par DedupUsage ; scope du dictionnaire à décider en 33-02), 32-05 (diagnostic : les compteurs du helper sont disponibles), docs/data-sources.md §7]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Un seul lecteur d'un champ JSON piégeux, gardé textuellement (littéraux interdits hors du helper) ET réflexivement (l'ancienne méthode n'existe plus) : motif GardesDoctrineTests étendu au récursif"
    - "Dédup « max par champ, premier timestamp » : équivalent à la dernière ligne mais indépendant de l'ordre de lecture ; jamais jeter un token connu (sans identifiant → compté tel quel)"
    - "Fixture RÉELLE anonymisée (clés de premier niveau et de usage réelles, textes remplacés par « … », chemins par <cwd>/<sessionId>) plutôt qu'une fixture minimale : le doublon strict et les champs parasites font partie de la preuve"
    - "Révocation d'une mutation par COPIE d'une sauvegarde, jamais par git checkout (core.autocrlf=true réécrit LF → CRLF et casse le sha256)"

key-files:
  created:
    - src/Chronos/Services/DedupUsage.cs
    - tests/Chronos.Tests/DedupUsageTests.cs
    - tests/Chronos.Tests/DedupHeritageDeltaTests.cs
    - tests/Chronos.Tests/GardesDedupUsageTests.cs
    - tests/Chronos.Tests/TestData/transcript-multi-blocs.jsonl
  modified:
    - src/Chronos/Services/TranscriptActivityProvider.cs
    - tests/Chronos.Tests/TranscriptActivityProviderTests.cs (46 insertions, 0 suppression)

key-decisions:
  - "D-32-01 — max par champ par identifiant, pas « première ligne » (sous-compte la sortie) ni somme (×2,1) : équivalent « dernière ligne » mais robuste au tri global et au doublon strict relu en dernier"
  - "D-32-02 — le timestamp retenu est le PREMIER vu : le message est facturé une fois, à l'instant de la réponse ; Since(11:20:01) exclut tout le message daté 11:20:00 (52, pas 77 056)"
  - "D-32-03 — dictionnaire GLOBAL à la passe de ReadAsync (491 ids dans 2 à 3 fichiers) ; le helper s'instancie de l'extérieur pour que la phase 33 le scope autrement"
  - "D-32-04 — DedupUsage est le SEUL lecteur de message.usage : garde textuelle récursive + garde réflexive + garde de position (new DedupUsage() avant foreach)"
  - "Les helpers IsolatedRootWith / ProviderFor sont RECOPIÉS en privé dans DedupHeritageDeltaTests (option du plan) : TranscriptActivityProviderTests reste en insertions seules"

patterns-established:
  - "Mutation complémentaire quand la mutation prescrite ne rougit pas le test attendu : (m1) « première ligne gagne » ne peut pas rougir Le_max_est_independant_de_l_ordre (la première ligne y porte déjà le max) ; (m1') « dernière ligne gagne » le rougit — les deux tests épinglent le max par ses deux côtés"

requirements-completed: [CPT-01]

# Metrics
duration: 38min
completed: 2026-09-27
---

# Phase 32 Plan 01: Compter juste — dédup des usages par `message.id` Summary

**Un message assistant écrit sur trois lignes (blocs thinking / text / tool_use, `output_tokens` 8 / 8 / 256) compte désormais UNE fois — 77 004 tokens, max par champ — dans la passe disque, et `TokensDepuisReleve` en hérite sans code propre (77 056 sur la fixture réelle par la tête + le vrai provider) ; une garde structurelle interdit tout futur lecteur de `message.usage` hors du helper `DedupUsage`.**

## Performance

- **Duration:** ≈ 38 min (dont ≈ 15 min d'attente sur les compilations des plans parallèles 32-02/03/04 dans le même arbre)
- **Started:** 2026-09-27T01:00:04Z (SHA d'entrée `c9d65cf`, 1200 verts au relevé de planification)
- **Completed:** 2026-09-27T01:38Z
- **Tasks:** 3 / 3
- **Files modified:** 7 (5 créés, 2 modifiés)

## Accomplishments

- `DedupUsage` : helper pur, unique, testé (7 tests) — `Ajouter(messageId, requestId, ts, in, out, cacheW, cacheR)` accumule le **max de chaque champ** et le **plus petit timestamp** par clé `message.id ?? requestId` ; sans clé, la ligne est comptée telle quelle. `LireUsage` porte les CINQ littéraux JSON, qui n'existent plus nulle part ailleurs sous `Services/**` et `Models/**`.
- `TranscriptActivityProvider.ReadAsync` : un `DedupUsage` créé **avant** la boucle sur les fichiers (dédup globale à la passe), `Entrees()` matérialisées puis triées ; `SumUsageTokens` et `Field` supprimés. Les fixtures existantes sans identifiant (`sample-valid`, `sample-tolerant`, `sample-inactive`, `SubagentsRoot`) rendent exactement les mêmes totaux (1550 / 2150 / 700 / 800) : aucun test existant retouché.
- Fixture **réelle** anonymisée `transcript-multi-blocs.jsonl` (7 lignes, clés 2.1.281 réelles : `parentUuid … gitBranch`, `usage` avec `output_tokens_details`, `server_tool_use`, `cache_creation`, `inference_geo`, `iterations`, `speed`) : `Since(06:00).Tokens == 77 056` (77 004 + 50 + 2), `Since(11:20:01).Tokens == 52`, L4 identique octet pour octet à L1.
- Héritage prouvé : magasin avec un relevé 5 h exact à 30 % capturé à 11:00, inner muet, activité = **vrai** `TranscriptActivityProvider` → `Estimated` / `PlancherAvecActivite` / `Utilization 0.30` / **`TokensDepuisReleve == 77 056`** ; relevé à 11:29 → `Exact` / `EncoreValide` / 0.
- Garde `GardesDedupUsageTests` (3 tests) : textuelle récursive (93 fichiers vus ≥ 40, anti-mutisme sur le helper et ses cinq littéraux), réflexive (`SumUsageTokens` introuvable sous toute visibilité), de position (`new DedupUsage()` unique, index strictement inférieur à `foreach (var file`), de forme (public sealed, `Ajouter` à 7 paramètres, `LireUsage` statique, `Entrees`, ≥ 4 `Math.Max(`).

## Task Commits

Each task was committed atomically (`--no-verify`, fichiers stagés un par un, aucun fichier hors `files_modified`) :

1. **Task 1 (RED)** — `e4ef295` test(32-01): dedup des usages par message.id, max par champ, RED 7 — `tests/Chronos.Tests/DedupUsageTests.cs`
2. **Task 1 (GREEN)** — `d665529` feat(32-01): DedupUsage, helper unique de somme des usages — `src/Chronos/Services/DedupUsage.cs`
3. **Task 2 (RED)** — `d058f00` test(32-01): fixture reelle multi-blocs, RED 3 — fixture + `TranscriptActivityProviderTests.cs` (+3) + `DedupHeritageDeltaTests.cs` (+2)
4. **Task 2 (GREEN)** — `65c166c` feat(32-01): la passe disque dedoublonne par message.id ; TokensDepuisReleve en herite — `src/Chronos/Services/TranscriptActivityProvider.cs`
5. **Task 3** — `13dc228` test(32-01): garde CPT-01 — `tests/Chronos.Tests/GardesDedupUsageTests.cs`

**Plan metadata:** voir le commit `docs(32-01)` final (SUMMARY + REQUIREMENTS).

## Tests — avant / après, rouges nommés, mutations

| Étape | Filtre | Résultat |
|---|---|---|
| Relevé de planification (HEAD `c9d65cf`) | suite complète | 1200 verts |
| Task 1 RED | `~DedupUsageTests` | 7 rouges par compilation (CS0246/CS0103 : `DedupUsage` introuvable) |
| Task 1 GREEN | `~DedupUsageTests` | 7 / 7 verts |
| Task 2 RED | `~TranscriptActivityProviderTests\|~DedupHeritageDeltaTests` | **3 rouges nommés** : `Trois_lignes_d_un_meme_message_comptent_une_fois_sur_la_fixture_reelle`, `Le_premier_timestamp_d_un_message_multi_blocs_est_celui_qui_compte`, `TokensDepuisReleve_herite_de_la_dedup_sans_code_propre` ; 12 verts dont les 2 non-régressions (`Les_fixtures_sans_identifiant_gardent_leurs_totaux`, `Un_releve_pris_apres_le_dernier_message_reste_exact_sans_activite`) |
| Task 2 GREEN | filtre du plan (provider, héritage, log, tête, doctrine) | 49 / 49 verts |
| Task 3 | `~GardesDedupUsageTests` | 3 / 3 verts |
| **Fin de plan, run 1** | `dotnet test Chronos.sln -c Debug` | **1271 verts, 0 échec**, 11 s (15 s mur) |
| **Fin de plan, run 2** | idem | **1271 verts, 0 échec**, 11 s (15 s mur) |

Contribution de ce plan : **+15 tests** (7 + 3 + 2 + 3 ; le plan estimait ≈ 12). Le total 1271 inclut les tests des plans parallèles 32-02/03/04 exécutés dans le même arbre.

Somme ligne par ligne de la fixture (ancien code) : 3 × 76 756 + 77 004 + 30 + 50 + 2 = **307 354** par calcul, pour 77 056 attendus — le chiffre « 231 016 » annoncé par le plan était erroné, le RED a bien été rouge sur la valeur réelle.

### Mutations (jouées, rouges nommés, révoquées par copie, sha256 identique)

| Mutation | Fichier | Rouges observés | Révocation |
|---|---|---|---|
| (m1) `Math.Max(v.Out, output)` → `v.Out` (première ligne gagne) | DedupUsage.cs | `Trois_lignes_du_meme_message_id_comptent_une_fois_au_max_par_champ`, `Sans_message_id_le_requestId_sert_de_cle` (2 rouges / 7) | sha256 `56b3513aaa25c945` identique |
| (m1') complément : `Math.Max(v.Out, output)` → `output` (dernière ligne gagne) | DedupUsage.cs | `Le_max_est_independant_de_l_ordre`, `Trois_lignes_d_un_meme_message_comptent_une_fois_sur_la_fixture_reelle`, `TokensDepuisReleve_herite_de_la_dedup_sans_code_propre` (3 rouges / 22) | identique |
| (m2) `new DedupUsage()` déplacé DANS le `foreach` (dédup par fichier, entrées agrégées par `AddRange`) | TranscriptActivityProvider.cs | **aucun test comportemental** (17 / 17 verts : une seule fixture par dossier) — limite ATTENDUE, couverte par la garde : `GardesDedupUsageTests.TranscriptActivityProvider_ne_somme_plus_ligne_par_ligne` rougit (= g2) | identique |
| (m3) `dedup.Ajouter(messageId, requestId, …)` → `Ajouter(null, null, …)` | TranscriptActivityProvider.cs | les 3 tests RED de la Task 2 (3 rouges / 22) | identique |
| (g1) commentaire `// "output_tokens"` ajouté au provider | TranscriptActivityProvider.cs | `Aucun_fichier_de_Services_ou_Models_ne_lit_message_usage_hors_du_helper` (1 rouge / 3) | identique |
| (g2) = (m2) | TranscriptActivityProvider.cs | `TranscriptActivityProvider_ne_somme_plus_ligne_par_ligne` (1 rouge / 18) | identique |

Écart au plan sur (m1) : le plan attendait `Le_max_est_independant_de_l_ordre` en second rouge. Il ne PEUT pas rougir sous « première ligne gagne » : dans ce test la première ligne porte déjà 256. C'est `Sans_message_id_le_requestId_sert_de_cle` (20 puis 40 → 30 ≠ 50) qui a tué la mutation. La mutation complémentaire (m1') « dernière ligne gagne » rougit bien `Le_max_est_independant_de_l_ordre` — et aussi les tests sur fixture, parce que le **doublon strict L4 (out 8) est relu APRÈS L3 (out 256)** : « dernière ligne » n'est donc PAS équivalent au max dès qu'un doublon strict traîne en fin de fichier, ce qui justifie D-32-01 au-delà de l'argument d'ordre.

## Files Created/Modified

- `src/Chronos/Services/DedupUsage.cs` (115 l.) — helper unique : `Ajouter`, `Entrees`, `LireUsage` statique, compteurs `MessagesDistincts` / `LignesSansIdentifiant` / `LignesVues` ; XML-doc « POURQUOI le max ».
- `src/Chronos/Services/TranscriptActivityProvider.cs` (149 → 145 l.) — `var dedup = new DedupUsage()` avant la boucle ; `if (when > now) continue;` puis `LireUsage` + `Ajouter` ; `dedup.Entrees().ToList()` + tri ; `SumUsageTokens`/`Field` supprimés ; phrase CPT-01 dans le XML-doc de classe.
- `tests/Chronos.Tests/TestData/transcript-multi-blocs.jsonl` (7 l., LF, sans BOM) — 3 blocs de `msg_01EXEMPLEMULTIBLOCS0000000001` + doublon strict + 2 lignes `req_01EXEMPLESANSMESSAGEID00000002` + 1 ligne sans identifiant ; aucun chemin ni nom réel.
- `tests/Chronos.Tests/DedupUsageTests.cs` (7 tests) ; `tests/Chronos.Tests/TranscriptActivityProviderTests.cs` (+3, insertions seules) ; `tests/Chronos.Tests/DedupHeritageDeltaTests.cs` (2 tests, `IDisposable`, magasin et transcripts en dossiers temp uniques) ; `tests/Chronos.Tests/GardesDedupUsageTests.cs` (3 tests).

## Decisions Made

D-32-01 à D-32-04 telles qu'écrites au plan (voir frontmatter). Une décision locale : recopier `IsolatedRootWith` / `ProviderFor` en privé dans `DedupHeritageDeltaTests` plutôt que de les passer `internal static` — le plan offrait les deux options, celle-ci garantit un diff en insertions seules sur `TranscriptActivityProviderTests.cs` (46 / 0 vérifié).

Les noms relevés au plan (`SumUsageTokens`, `Field`, `IsAssistant`, `IsolatedRootWith`, `ProviderFor`, `GardesPerimetreTests.CheminSources()`, `FakeClock`, `FakeUsageProvider`, `WindowState.Unavailable`, `SourceUsage.SondeEnTetes`) correspondaient tous au code réel.

## Deviations from Plan

None - plan executed exactly as written. Deux compléments non prescrits, sans effet sur le code livré :
- mutation supplémentaire (m1') pour prouver que `Le_max_est_independant_de_l_ordre` mord bien (voir tableau) ;
- la première tentative de (m2) était mal écrite (le `dedup` hors de portée après la boucle → CS0103) : réécrite fidèlement (liste externe + `AddRange` par fichier) avant mesure.

## Issues Encountered

- **Arbre partagé, quatre agents** : la compilation du projet de tests a été bloquée par les RED des plans voisins (`VerrouInstanceUnique` de 32-03, `LastExactStore.DerniereErreur` / `VueAppData` de 32-02, `JournalReleves` de 32-04) et une fois par un verrou `CS2012`. Traité comme prévu : attente 40-45 s et relance (jusqu'à 10 essais), jamais conclu à un échec de code. Coût ≈ 15 min.
- **`core.autocrlf=true`** : révoquer une mutation par `git checkout -- fichier` réécrit le fichier en CRLF (sha256 différent, `git status` sale). Correction : sauvegarde du fichier commité dans le scratchpad et révocation par `cp` ; toutes les révocations du tableau ont été vérifiées identiques au HEAD. À retenir pour les plans suivants.
- Le compteur de CRLF par `grep -c $'\r'` dans une substitution `$( )` dégénère en `grep -c r` (115 « CRLF » fantômes) ; `file` fait foi : tous les fichiers livrés sont LF, UTF-8 sans BOM.

## Pour la phase 33 (à consigner)

- **491 ids dans 2 à 3 fichiers** sur 8 jours (reprise / fork de session) : la dédup est **globale à la passe** ici. Le scope du dictionnaire sous des curseurs par fichier (33-02) reste à décider — un `DedupUsage` par fichier recompterait ces 491 messages ; un dictionnaire persistant d'ids vus par fichier, ou une clé `(message.id)` conservée dans l'agrégat, sont les deux voies à arbitrer.
- Tout nouveau lecteur de transcripts DOIT passer par `DedupUsage.LireUsage` + `Ajouter` : la garde rougit sur le seul littéral `"usage"` sous `Services/**` ou `Models/**` (sous-dossiers inclus).
- `LignesVues / MessagesDistincts` donne le facteur de surcomptage réel (≈ 2,1 mesuré) : exploitable au diagnostic (32-05) pour vérifier en production que la dédup agit.

## Known Stubs

Aucun. Aucune valeur vide ou fictive câblée vers l'UI ; le plan ne touche aucune vue.

## Self-Check: PASSED

- FOUND: src/Chronos/Services/DedupUsage.cs
- FOUND: src/Chronos/Services/TranscriptActivityProvider.cs (modifié, `SumUsageTokens` = 0 occurrence)
- FOUND: tests/Chronos.Tests/TestData/transcript-multi-blocs.jsonl (7 lignes)
- FOUND: tests/Chronos.Tests/DedupUsageTests.cs, DedupHeritageDeltaTests.cs, GardesDedupUsageTests.cs
- FOUND: commits e4ef295, d665529, d058f00, 65c166c, 13dc228
- `git status --short -- src` vide après toutes les révocations ; ROADMAP.md et STATE.md non modifiés par ce plan.
