---
phase: 24
slug: l-arbitrage-par-fraicheur
status: validated
nyquist_compliant: true
wave_0_complete: n/a
created: 2026-09-12
validated: 2026-09-12
---

# Phase 24 — Validation Strategy

> Carte **posée à la planification**, **remplie et mesurée** par le plan 24-02 Task 2.
> Toute case encore en attente de mesure à la clôture est un défaut de la phase, pas une approximation.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`net8.0-windows`) — `tests/Chronos.Tests` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` |
| **Quick run (arbitrage)** | `… --filter "FullyQualifiedName~Arbitrage\|FullyQualifiedName~Inspection\|FullyQualifiedName~Sessions\|FullyQualifiedName~Diagnostic\|FullyQualifiedName~Affichage\|FullyQualifiedName~GardesPerimetre"` |
| **Baseline d'entrée de phase** | **763 tests / 0 échec / ~4 s** (fin de phase 23) |
| **Cible de fin de phase** | **0 échec, aucun test supprimé**, total **> 763** — environ **+25** attendus (≈ 9 en 24-01 T1, ≈ 9 en 24-01 T2 *(avec la permutation rendue probante)*, 2 en 24-01 T3, **6** en 24-02 — la `[Theory]` `L_ecart_d_age_se_dit_en_distance_pas_en_instant` compte **4** cas xUnit, plus 2 `[Fact]`) → total attendu **≈ 788**. Chiffre **INDICATIF** : le critère est « 0 échec + aucune suppression », pas un total. |
| **Total mesuré après 24-01** | **782 / 0 échec / 4 s** (+19 : 9 + 8 + 2) |
| **Total mesuré après 24-02** | **788 / 0 échec / 4 s** (+6 exactement : 4 cas de `[Theory]` + 2 `[Fact]`). La prédiction « ≈ 788 » est atteinte **au test près**, sans ajustement. |
| **Deux exécutions consécutives** | **788 / 788**, 0 échec les deux fois, 4 s les deux fois ✅ |
| **Renommages** | **1, exactement celui prévu** : `Monitor_fusionne_transcripts_et_hooks_hook_prioritaire` → `…_le_plus_recent_gagne` (24-01). **Aucun autre renommage** dans toute la phase. |

## Le critère de cette phase n'est PAS un chiffre de couverture

Cette phase **n'enlève aucun code** et ne supprime aucun test. Le critère opérationnel :

> **0 échec, total > 763, aucun test supprimé, et les quatre critères de succès du ROADMAP prouvés un à un.**

Un total inférieur à 763 doit être expliqué **nominativement**, jamais absorbé en ajustant le chiffre attendu.

**Mesuré : 788 > 763, 0 échec, 0 test supprimé, 0 test ignoré.** Les quatre critères sont ci-dessous.

## Les quatre critères du ROADMAP, un par un

| # | Critère | Preuve nommée | Résultat mesuré |
|---|---------|---------------|-----------------|
| 1 | **Le cas mesuré ne se reproduit plus** — un transcript de 10 s bat un hook de 7 h ; les trois inversions du relevé disparaissent (FUS-01) | `InspectionSessionsTests.Un_hook_Working_de_25_min_ne_rend_plus_un_transcript_frais_inconnu`, `…Un_hook_WaitingTurn_de_7_h_ne_bat_plus_un_transcript_de_10_s`, `…Un_hook_WaitingAttention_de_7_h_ne_bat_plus_un_transcript_de_10_s` | **VERT ×3**, exécutés un par un (`1 réussite / 0 échec` chacun). Les **trois** étaient **rouges** contre le code d'avant le correctif (mesure 24-01 Task 2 : 4 échecs / 10 réussites / 14 total sur le filtre `InspectionSessions`). |
| 2 | **Le résultat ne dépend plus de l'ordre** — permuter l'ordre d'enregistrement des sources ne change pas un seul état affiché (FUS-01) | **Le critère au sens strict est porté par** `ArbitrageSessionsTests.Permuter_l_ordre_des_signaux_ne_change_pas_un_seul_etat_ni_un_seul_desaccord` (**720** permutations, `Assert.Single(distincts)` ; le cas d'égalité d'âge casserait toute implémentation reposant sur un tri stable ou sur l'ordre de regroupement). `InspectionSessionsTests.Permuter_l_ordre_rendu_par_la_source_ne_change_pas_l_affichage` (**6** permutations) prouve, lui, l'ordre rendu **par une source** — corpus ex aequo imposé pour qu'il soit rouge avant le correctif. | **VERT ×2**, exécutés un par un. Gardes anti-muettes vérifiées : `Assert.Equal(720, vues)` → **1** occurrence, `Assert.Single(distincts)` → **1** dans chacun des deux fichiers. Le second test était **rouge avant le correctif** (4e rouge-avant, voir plus bas). |
| 3 | **Les désaccords sont lisibles** — le diagnostic nomme la source retenue, la source écartée et l'écart d'âge (FUS-02) | `DiagnosticServiceTests.Un_desaccord_nomme_la_source_retenue_la_source_ecartee_et_l_ecart_d_age` + `…Sans_contradiction_le_rapport_annonce_zero_desaccord_et_le_dit` | **VERT ×2**. Étape ROUGE mesurée avant le correctif : **2 échecs / 28 réussites / 30 total** sur le filtre `Diagnostic` (**2 / 786 / 788** sur la suite complète). Ligne réellement produite :<br>`· e465420e — retenu transcript (~/.claude/projects) « en cours » ; écarté fichier de hook (%APPDATA%\Chronos\sessions) « à toi », plus ancien de 6 h` |
| 4 | **La précision ne bat pas la fraîcheur** — un signal plus spécifique mais plus ancien n'écrase jamais un signal plus récent, et l'égalité d'âge est tranchée par une règle explicite et testée | `ArbitrageSessionsTests.Un_permission_prompt_plus_ancien_ne_bat_pas_un_signal_plus_recent` + `…A_age_egal_la_source_la_plus_specifique_tranche_et_l_ordre_inverse_donne_le_meme_resultat` | **VERT ×2**, exécutés un par un. Les deux faisaient partie des **9 rouges** de l'étape ROUGE de 24-01 Task 1 (9 échecs / 0 réussite / 9 total sur le filtre `ArbitrageSessions`). La règle d'égalité est testée **dans les deux sens d'entrée** par le second. |

## Les cinq lignes mesurées du 2026-09-12 — et leur qualification HONNÊTE

Vérité terrain : le modèle travaille, transcript écrit il y a 10 secondes.

| Ligne du relevé | État rendu AVANT | Attendu APRÈS | Test | Qualification **mesurée** |
|---|---|---|---|---|
| transcript seul | `Working` | `Working`, 0 désaccord | `Un_transcript_frais_seul_est_annonce_en_cours` | **témoin** — vert avant comme après (confirmé au run de ROUGE de 24-01 T2 : il figurait parmi les 10 verts) |
| + hook `Working` de 25 min | `Unknown` | `Working`, 1 désaccord | `Un_hook_Working_de_25_min_ne_rend_plus_un_transcript_frais_inconnu` | **rouge avant le correctif** ✅ (mesuré) |
| + hook `WaitingTurn` de 7 h | `WaitingTurn` | `Working`, 1 désaccord | `Un_hook_WaitingTurn_de_7_h_ne_bat_plus_un_transcript_de_10_s` | **rouge avant le correctif** ✅ (mesuré) |
| + hook `WaitingAttention` de 7 h | `WaitingAttention` | `Working`, 1 désaccord | `Un_hook_WaitingAttention_de_7_h_ne_bat_plus_un_transcript_de_10_s` | **rouge avant le correctif** ✅ (mesuré) |
| + hook `WaitingTurn` de 9 h (> `DropAfter`) | `Working` | `Working`, **0 désaccord**, 1 fichier écarté pour ancienneté | `Un_hook_de_9_h_n_est_plus_candidat_du_tout_et_ne_produit_aucun_desaccord` | **contrôle de non-régression** — vert avant comme après ; le résultat était déjà bon, pour une mauvaise raison, et seule l'assertion « 0 désaccord » est neuve |

**Trois inversions étaient rouges, pas cinq.** Écrire le contraire serait exactement le genre d'affirmation
non observée que ce milestone bannit — la carte s'applique à elle-même la doctrine qu'elle vérifie.
**La mesure confirme la prédiction : 3 rouges-avant, 1 témoin, 1 contrôle de non-régression.**

**Un quatrième test rouge-avant, hors tableau — CONFIRMÉ par la mesure :**
`InspectionSessionsTests.Permuter_l_ordre_rendu_par_la_source_ne_change_pas_l_affichage`. Son corpus impose
deux sessions **ex aequo** sur (urgence, horodatage) — `b-deux` et `a-un`, tous deux `Working` à −1 min :
avant le correctif, une entrée indexée par identifiant rend l'ordre d'insertion et le tri de `Ordonner` est
stable, donc les deux sessions sortaient dans l'ordre où la source les avait rendues — le test était bien
**rouge** (il est le 4e des 4 échecs mesurés au ROUGE de 24-01 Task 2). Avec un corpus aux couples tous
distincts il aurait été vert avant comme après : une garde muette. Le corpus n'est donc pas un détail,
c'est la preuve.

## Le cas hérité de la phase 23 : un fragment n'est pas un vieux signal

L'écriture directe livrée en 23-01 **tronque la cible avant de la réécrire** : un lecteur malchanceux lit un
fragment. Trois traitements possibles, deux sont des fautes :

| Traitement du fragment | Conséquence |
|---|---|
| **Signal daté au minimum** (« très vieux ») | il entre dans l'arbitrage et **perd contre n'importe quoi** — une déposition fabriquée là où il n'y a rien |
| **Signal retenu** (parsing partiel toléré) | un état inventé s'affiche |
| **ABSENCE de signal** ✅ | il n'est pas déposé ; la session est décrite par ses autres sources, et **rien** n'est compté |

Preuves : `InspectionSessionsTests.Un_fragment_illisible_est_une_ABSENCE_de_signal_jamais_un_vieux_signal`
(`FichiersEcartesParAnciennete == 0` **et** 0 désaccord) et
`…Un_hook_sans_updated_at_ne_gagne_jamais`.
**Résultat mesuré : les deux VERTS**, exécutés un par un (`1 réussite / 0 échec` chacun). Le fragment
`{"session_id":"session-mesuree","project":"P","activ` pour la même session qu'un transcript frais rend
`Working`, `FichiersEcartesParAnciennete == 0`, **0 désaccord** : il n'est ni un vieux signal qui perdrait,
ni un signal retenu — il n'entre pas dans l'arbitrage.

## Ce qui rendrait cette phase creuse

Deux livraisons qui produisent **le même écran** le jour J :

| Livraison | Ce qu'on lit le jour J | Ce qui se passe la semaine suivante |
|---|---|---|
| **Inverser la priorité** : « le transcript prime toujours » | les inversions mesurées disparaissent | une vraie demande de permission — que seul le hook sait exprimer — est écrasée par un transcript à peine plus récent. Le défaut a changé de sens, pas de nature. |
| **Arbitrer sur la FRAÎCHEUR**, égalité tranchée par une règle nommée | les inversions disparaissent | un signal ne perd que parce qu'il est plus vieux, et on peut lire **pourquoi** dans le rapport |

Et deux façons de « satisfaire » le critère n°3 :

| Livraison | Ce que voit l'utilisateur | Ce que ça dit |
|---|---|---|
| Le désaccord est rangé avec les **masquages** | une ligne de plus dans « Sessions MASQUÉES » | la session serait **cachée** — elle ne l'est pas, elle est à l'écran. La confusion « absente / écartée par tel filtre » que la phase 22 a fermée se rouvre. |
| Le désaccord a son **propre bloc**, la session reste comptée dans les affichées | une ligne sous « Désaccords entre sources » | un **signal** a perdu, et on sait lequel, d'où il vient et de combien il est plus vieux |

C'est la **seconde** qui a été livrée, et le test le prouve dans le même rapport :
`Sessions AFFICHÉES par le widget : 1` **et** `Sessions MASQUÉES par un filtre : 0`, alors même que le
désaccord est compté. Le vocabulaire de masquage n'a pas été élargi — son type est resté à **3** occurrences
dans `DiagnosticService.cs`, avant comme après.

Seules les preuves structurelles les distinguent :

```sh
grep -cF "byId["                       src/Chronos/Services/SessionMonitor.cs      # attendu : 0
grep -cF "ArbitrageSessions.Trancher(" src/Chronos/Services/SessionMonitor.cs      # attendu : 1
grep -cF "DateTimeOffset.UtcNow"       src/Chronos/Services/ArbitrageSessions.cs   # attendu : 0
grep -cF "AffichageSessions.Urgence("  src/Chronos/Services/ArbitrageSessions.cs   # attendu : 2
grep -cF "lecture.Desaccords"          src/Chronos/Services/DiagnosticService.cs   # attendu : 3
grep -cF "new SessionMonitor"          src/Chronos/Services/DiagnosticService.cs   # attendu : 0
grep -cF "?? new "                     src/Chronos/Services/DiagnosticService.cs   # attendu : 2
```

**Résultats mesurés — les sept, un par un :**

| Preuve | Attendu | Mesuré |
|---|---|---|
| `byId[` dans `SessionMonitor.cs` | 0 | **0** ✅ |
| `ArbitrageSessions.Trancher(` dans `SessionMonitor.cs` | 1 | **1** ✅ |
| `DateTimeOffset.UtcNow` dans `ArbitrageSessions.cs` | 0 | **0** ✅ |
| `AffichageSessions.Urgence(` dans `ArbitrageSessions.cs` | 2 | **2** ✅ |
| `lecture.Desaccords` dans `DiagnosticService.cs` | 3 | **3** ✅ (le compte, le `foreach`, le test de vacuité) |
| `new SessionMonitor` dans `DiagnosticService.cs` | 0 | **0** ✅ |
| `?? new ` dans `DiagnosticService.cs` | 2 | **2** ✅ |

S'ajoutent les preuves propres au plan 24-02 :

| Preuve | Attendu | Mesuré |
|---|---|---|
| `Désaccords entre sources` dans `DiagnosticService.cs` | 1 | **1** ✅ |
| `AffichageSessions.Ecart(` dans `DiagnosticService.cs` | 1 | **1** ✅ |
| `public static string Ecart` dans `AffichageSessions.cs` | 1 | **1** ✅ |
| Motifs d'époque interdits dans `AffichageSessions.cs` | 0 | **0** ✅ (le fichier n'est pas exempté de `NormalisationUnique`) |
| Le type de motif de masquage dans `DiagnosticService.cs` | 3, **inchangé** | **3** ✅ |
| `git diff -U0 -- src/Chronos/Services/DiagnosticService.cs \| grep -c "^-[^-]"` | 0 | **0** ✅ — le changement est une **insertion** (31 lignes), pas une réécriture |
| `git diff -U0 -- tests/Chronos.Tests/DiagnosticServiceTests.cs \| grep -c "^-[^-]"` | 0 | **0** ✅ |
| `git diff --stat` du plan 24-02 entier | 4 fichiers, **0 suppression** | **103 insertions(+), 0 suppression**, 4 fichiers ✅ |

## Mutations de falsification

| Mutation | Test attendu en échec | Résultat mesuré | Révocation prouvée |
|---|---|---|---|
| `ArbitrageSessions.Trancher(` → `ArbitrageSessions.TrancherMUTANT(` dans `SessionMonitor.cs` (via alias temporaire, pour que le dépôt reste compilable) | **DEUX** tests : `GardesPerimetreTests.Le_moniteur_n_arbitre_plus_par_ordre_d_insertion` (l'appel a changé de nom) **et** `GardesPerimetreTests.L_arbitrage_ne_connait_ni_horloge_ni_magasin_ni_chemin` (l'alias public statique ajoute une seconde porte d'entrée et casse son `Assert.Single(declarees)`). Double falsification attendue, pas une surprise. | **2 échecs / 8 réussites / 10 total** sur le filtre `GardesPerimetre` — **exactement les deux tests attendus**, ni plus ni moins. Les deux gardes sont donc bien falsifiables : elles ne sont pas muettes. | **PROUVÉE** ✅ — `git diff --stat -- src/Chronos/Services/` après révocation : **vide** ; `grep -rlF "MUTANT" --include=*.cs src/Chronos tests/Chronos.Tests \| wc -l` → **0** ; filtre `GardesPerimetre` après révocation → **10 réussites / 0 échec**. (Sans `--include=*.cs`, le balayage traverserait `bin/` et `obj/` où le nom muté survit dans les assemblies.) |

Rappel de méthode (précédents 23-01 et 23-02) : une mutation qui ne **compile pas** fait échouer *toute*
l'invocation `dotnet test` et ne dit **rien** de la garde. On la joue donc par alias, et on révoque alias
et mutation **ensemble**. C'est ce qui a été fait en 24-01 Task 3.

## Acquis des phases précédentes, revérifiés

| Acquis | Attendu | Mesuré |
|---|---|---|
| Phase 22 — `=> Inspecter(now).Visibles;` dans `SessionMonitor.cs` | 1 | **1** ✅ |
| Phase 22 — `new SessionMonitor` dans `DiagnosticService.cs` | 0 | **0** ✅ |
| Phase 22 — `_moniteurSessions.Inspecter(_clock.UtcNow)` | 1 | **1** ✅ (les désaccords passent par **cette** lecture, pas par une seconde) |
| Phase 20 — `?? new ` dans `DiagnosticService.cs` | 2 | **2** ✅ |
| Phase 25 non anticipée — `StaleWorking` = 20 min, `DropAfter` = 8 h | inchangés | **1 occurrence chacun, valeurs inchangées** ✅ |
| Phase 26 non anticipée — diff de `SessionTreatmentTracker.cs` / `TreatedStore.cs` / `ArchiveStore.cs` | vide | **vide** ✅ (sur toute la phase) |
| Phase 24-01 — diff de `SessionMonitor.cs` / `ArbitrageSessions.cs` / `LectureSessions.cs` pendant 24-02 | vide | **vide** ✅ |
| Gardes — `ServicesLayerPurity` / `NormalisationUnique` / `CompositionRoot` / `GardesDoctrine` / `GardesPerimetre` | 2 / 3 / 5 / 8 / **10** | **2 / 3 / 5 / 8 / 10**, **0 échec** partout ✅ |

## Invariants de sécurité — à vérifier avant et après chaque tâche

| Invariant | Attendu | Mesuré (avant → après) |
|---|---|---|
| `%APPDATA%\Chronos\sessions` | **66** entrées, INCHANGÉ | **66 → 66** ✅ |
| `%APPDATA%\Chronos\archived.json` | **84** octets | **84 → 84** ✅ |
| `%APPDATA%\Chronos\oauth.dat` (**taille seule, jamais le mtime**) | **518** octets | **518 → 518** ✅ (mtime jamais consulté) |
| Overlay `Chronos-v3.0.2.exe` (pid 119412) | vivant, ni lancé ni tué (`tasklist` seul) | **vivant → vivant** ✅ |
| `git diff -- '*.csproj'` | vide | **vide** ✅ |
| `git status --porcelain` | rien hors `src/`, `tests/`, `.planning/` | **vide (tout commité)** ✅ |
| Requêtes réseau réelles depuis un test | 0 | **0** ✅ — les tests de diagnostic montent `Token = null`, la sonde réseau est gardée par `if (token is not null)` |
| Dépendances NuGet ajoutées | 0 | **0** ✅ (3 `PackageReference`, inchangées) |
| Tests écrivant hors de `Path.GetTempPath()` | 0 | **0** ✅ — `TempDir22()` porte sa garde anti-accident `Assert.StartsWith(Path.GetTempPath(), d)` |

## À VÉRIFIER PAR L'UTILISATEUR

Les tests prouvent le **mécanisme** ; ils ne prouvent pas ce que l'écran de l'utilisateur montre
(leçon 21-04). La phase s'interdit de lancer ou de tuer l'overlay.

1. **Critère n°1, in vivo.** Une session où le modèle travaille doit s'afficher **« en cours »**, même si un
   vieux fichier de hook traîne dans `%APPDATA%\Chronos\sessions`. Le symptôme historique — « à toi » alors
   que ça travaille — ne doit plus apparaître du tout.
2. **Critère n°3, in vivo.** **Clic droit → Diagnostic…**, bloc **« Désaccords entre sources »**. Chaque
   ligne doit nommer la source retenue **avec son dossier**, la source écartée **avec son dossier**, et
   l'écart d'âge. Une source figée depuis des heures se lit là, et nulle part ailleurs. Forme attendue,
   telle que mesurée en test :
   `· e465420e — retenu transcript (~/.claude/projects) « en cours » ; écarté fichier de hook (%APPDATA%\Chronos\sessions) « à toi », plus ancien de 6 h`
   Et quand rien ne se contredit : `Désaccords entre sources : 0` suivi de
   `(aucun — aucune source n'en contredit une autre en ce moment)`.
3. **Critère n°2, in vivo.** Aucun état affiché ne doit changer d'un tick à l'autre sans qu'une donnée ait
   changé. Un clignotement entre deux états est un défaut à signaler.
4. **Critère n°4, in vivo.** Une demande de permission réelle doit bien basculer la session en « à toi » —
   la fraîcheur ne doit pas avoir **enterré** la spécificité, seulement cessé de lui céder le pas quand elle
   est périmée.
5. **RIEN DE TOUT CELA NE S'EXÉCUTE TANT QUE L'EXE N'EST PAS REPUBLIÉ.** Les hooks installés dans
   `~/.claude/settings.json` pointent l'exe **v3.0.2**, celui qui tourne actuellement, antérieur à tout le
   milestone v1.6. La republication (version **dans** l'exe **et** dans le nom du fichier publié) reste à
   faire en fin de milestone.
6. **Reliquats hérités**, à présenter **groupés** en fin de milestone : purge réelle d'`archived.json`
   (phase 21), comparaison ligne à ligne widget ↔ rapport (phase 22), résorption du magasin de 66 entrées
   (phase 23).

## Clôture

**FUS-01** (24-01) et **FUS-02** (24-02) sont couvertes et cochées dans `REQUIREMENTS.md`.
Suite : **788 tests / 0 échec / 0 ignoré**, deux exécutions consécutives au même total, aucun test supprimé
sur toute la phase. Les quatre critères du ROADMAP sont prouvés par des tests nommés, dont **quatre** étaient
**rouges** contre le code d'avant le correctif — trois inversions du relevé plus la permutation à corpus
ex aequo. Ce qui reste ouvert relève de l'observation in vivo, ci-dessus, et non du mécanisme.
