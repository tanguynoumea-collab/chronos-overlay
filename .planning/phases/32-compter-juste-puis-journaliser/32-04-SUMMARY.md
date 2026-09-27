---
phase: 32-compter-juste-puis-journaliser
plan: 04
subsystem: persistance / journal des relevés exacts (JSONL mensuel, décorateur d'observation)
tags: [journal, jsonl, FileShare.None, idempotence, CapturedAt, dedup, IHostedService, decorateur, System.Text.Json, retention, tolerance, gardes-structurelles, sous-namespaces]

# Dependency graph
requires:
  - phase: 18-19 (HDR-01..06, EXA-02..05)
    provides: RateLimitHeaderUsageProvider.CadenceNominale (300 s), WindowState.StatutServeur / Depassement / Source / CapturedAt, IEtatServeur.DernierResultat, doctrine « exact ou rien »
  - phase: 17 (TOK-02)
    provides: IAuthStatus.EtatChange sur transition, EtatAuthentification.Deconnecte
  - phase: 23 / 25 (CYC-01, EVT-03)
    provides: motif EcritureEtatSession.EcrireAvecReprise (partage exclusif + reprises bornées + relecture sous le même descripteur), motif BalayageMagasinSessions (purge par âge, bilan = observation)
  - phase: 32 (recherche, §JRN-01..03, Pattern 1-3, Pitfall 3-4)
    provides: FileMode.Append non atomique sous Windows (mesuré), dédup t strictement croissant par source, piège des gardes sur les sous-dossiers
provides:
  - Models/Historique : ReleveJournal (T, Source, U5, R5, Statut5, U7, R7, Statut7, Overage, OverageStatut), TypeEvenement + EvenementJournal + TypeEvenementTexte (NomsDeFil / Nom / Depuis)
  - Services/Historique/LigneJournal : Serialiser (ordre du contrat §3, WhenWritingNull, enums par nom, double brut invariant, instants UTC « O ») et Parser tolérant (v lu en premier, ev inconnu conservé, relevé sans source refusé) ; ChampsReleve exposé pour la garde documentaire de 32-07
  - Services/Historique/LecteurJournal.LireFichier → LectureFichier(Releves, Evenements, LignesIgnorees), jamais d'exception
  - Services/Historique/JournalReleves : IEtatJournal ; append FileShare.None + reprises bornées + relecture de queue (16 Ko) sous le même verrou ; clé (t, source) ; fichiers releves-AAAA-MM.jsonl par mois UTC ; DernierT(source) ; Purger() 24 mois ; SeuilReprise = 2 × CadenceNominale, SeuilMuet = 3 × CadenceNominale
  - Services/Historique/JournalisationUsageProvider : décorateur IUsageProvider + IHostedService, n'écrit que l'exact de l'inner (jamais plancher, indisponible ni MagasinDernierExact), une ligne par couple (t, source), six événements (demarrage, arret, jeton_invalide, sonde_refusee, reprise, ecriture_ratee), SignalerEcritureRatee(magasin, cause)
  - Gardes élargies aux sous-dossiers/sous-namespaces : ServicesLayerPurityTests (EstTypeNeutre, StartsWith) + test « la garde voit Chronos.Services.Historique », GardesDoctrineTests ×2 et NormalisationUniqueTests en SearchOption.AllDirectories
  - Fixture tests/Chronos.Tests/TestData/journal/tolerance/releves-2026-09.jsonl (9 lignes : tronquée, v=2, sans t, vide, ev inconnu, t illisible)
affects: [32-05 (câblage DI : décorateur entre LastExactUsageProvider et le composite, AddHostedService AVANT RefreshOrchestrator, IEtatJournal au VM, SeuilMuet pour l'alerte), 32-06 (LecteurJournal.Lire(dossier, de, a) et AnalyseReleves), 32-07 (garde documentaire : ChampsReleve et NomsDeFil ↔ docs/data-sources.md §7), 32-08 (constat : le journal s'écrit), 33 (tranches UTC cohérentes avec les mois UTC), 34-35 (lecture par plage)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Append multi-processus sûr : FileStream(OpenOrCreate, ReadWrite, FileShare.None) + reprises bornées (Yield ×12 puis Sleep(1)) + relecture de la queue sous le MÊME descripteur ; jamais le mode « ajout » de FileStream (Seek à l'ouverture, ligne perdue mesurée), jamais temp + Move pour un fichier à ajout"
    - "Idempotence par clé (t, source) avec t STRICTEMENT croissant par source — la vérité est la relecture sous verrou ; la mémoire du décorateur n'est qu'une économie de verrou (prouvée par « un rejeu ne touche pas le disque »)"
    - "Ligne JSONL à DTO privés [JsonPropertyName]/[JsonPropertyOrder], STJ seul, encodeur relâché (l'encodeur par défaut échappe « + » de l'offset ISO), instants écrits en UTC au format « O »"
    - "Lecture tolérante : v lu EN PREMIER, champ absent ou du mauvais type → null (jamais 0), ligne refusée COMPTÉE, ligne vide ni lue ni comptée, ev inconnu conservé en NonReconnu avec son nom brut"
    - "Garde de pureté par préfixe de namespace (EstTypeNeutre) + test qui prouve que la garde VOIT le nouveau quartier ; gardes textuelles en AllDirectories"
    - "Décorateur d'observation : rend la MÊME instance, événements sur TRANSITION (mémoire du dernier état), une exception de journalisation devient un événement ecriture_ratee et jamais une panne de la chaîne"
    - "Seuils dérivés d'une constante nommée (2 × / 3 × CadenceNominale), jamais 600/900 s en dur (précédent DoctrineFraicheur.LimiteAge)"

key-files:
  created:
    - src/Chronos/Models/Historique/ReleveJournal.cs
    - src/Chronos/Models/Historique/EvenementJournal.cs
    - src/Chronos/Services/Historique/LigneJournal.cs
    - src/Chronos/Services/Historique/LecteurJournal.cs
    - src/Chronos/Services/Historique/IEtatJournal.cs
    - src/Chronos/Services/Historique/JournalReleves.cs
    - src/Chronos/Services/Historique/JournalisationUsageProvider.cs
    - tests/Chronos.Tests/LigneJournalTests.cs
    - tests/Chronos.Tests/JournalRelevesTests.cs
    - tests/Chronos.Tests/JournalisationUsageProviderTests.cs
    - tests/Chronos.Tests/TestData/journal/tolerance/releves-2026-09.jsonl
  modified:
    - tests/Chronos.Tests/ServicesLayerPurityTests.cs
    - tests/Chronos.Tests/GardesDoctrineTests.cs
    - tests/Chronos.Tests/NormalisationUniqueTests.cs
    - .planning/REQUIREMENTS.md

key-decisions:
  - "D-32-14 — sous-dossiers Services/Historique et Models/Historique avec sous-namespaces ; les quatre gardes passent en préfixe / AllDirectories, et un test rougit si le balayage redevient plat"
  - "D-32-15 — une ligne = un relevé d'UNE source à UN instant ; clé (t, source) ; snapshot mixte → une ligne par couple, fenêtres de l'autre couple à null, écrites dans l'ordre des instants"
  - "D-32-16 — fichier par mois UTC de t (culture invariante) ; 01:30+02:00 le 1er octobre tombe dans releves-2026-09"
  - "D-32-17 — SeuilReprise = 2 × CadenceNominale (10 min), SeuilMuet = 3 × CadenceNominale (15 min), jamais en dur"
  - "D-32-18 — u5/u7/overage écrits en double brut (STJ, invariant), sans arrondi : la granularité 0,01 se vérifie AVEC le journal"
  - "D-32-19 — le décorateur voit l'inner BRUT ; l'exclusion Source == MagasinDernierExact est écrite et testée bien qu'impossible sous la tête"
  - "D-32-20 — STJ seul, JsonStringEnumConverter, WhenWritingNull, UTF-8 sans BOM, LF seul ; 216 o pour un relevé complet sans overage, 270 o avec (≈ 75 Ko/jour, ≈ 2,3 Mo/mois)"
  - "EssaisMax = 600 (et non 60 comme les hooks) : deux écrivains du journal peuvent boucler serré (test à deux écrivains, deux overlays qui rattrapent) ; avec 60, un écrivain qui tient le fichier 95 % du temps ferait échouer l'autre une fois sur vingt"
  - "Création du dossier HORS de la boucle de reprises : un dossier qu'on ne peut pas créer (fichier « poison ») n'est pas un verrou transitoire — échec immédiat dans DerniereErreur"
  - "Purger() énumère TOUS les fichiers du dossier et compte les noms non conformes en Ignores (notes.txt, releves-abcd.jsonl) : un bilan qui ne voit que le motif attendu ne dit rien de ce qui traîne"
  - "Un refus SANS erreur de AjouterReleve (l'autre processus a déjà écrit) fait avancer la mémoire _dernierT du décorateur, sinon la même « reprise » serait réémise à chaque tick"
  - "Au plus UN sonde_refusee par tick (cause = nom du ResultatSonde, sinon « statut rejected ») ; le relevé porteur d'un 429 est écrit quand même (HDR-02)"
  - "t → instant par UsageNormalization.InstantDepuisIso (point unique HDR-05) : LigneJournal ne porte aucun DateTimeOffset.TryParse, la garde élargie reste verte sans exemption"

patterns-established:
  - "Mutation masquée par une double protection → renforcer le test NOMMÉ avec l'observable propre à la couche mutée (ici : supprimer le fichier, rejouer, il ne renaît pas) plutôt que de déclarer la mutation « inobservable »"
  - "Exécution parallèle sans worktree : les RED des voisins cassent la compilation du projet de tests — attendre 45 s et relancer, jamais conclure à un échec de code ; garder ses propres fenêtres RED courtes (implémentation prête dans le scratchpad avant de committer le RED)"
  - "Mutations jouées par script (sauvegarde, mutation, filtre, restauration, sha256) dans une boucle qui attend une fenêtre compilable"

requirements-completed: [JRN-01, JRN-02, JRN-03]

# Metrics
duration: 32min
completed: 2026-09-27
---

# Phase 32 Plan 04 : Le journal des relevés exacts, complet mais non branché — Summary

**Journal JSONL mensuel des relevés exacts (append `FileShare.None` idempotent sur (t, source), lecture tolérante, rétention 24 mois) + décorateur `IUsageProvider`/`IHostedService` qui n'écrit que l'exact de l'inner avec six événements de couverture, sous quatre gardes structurelles élargies aux sous-dossiers `Historique/` — prêt à être inséré dans le graphe DI par 32-05.**

## Performance

- **Duration:** ≈ 32 min (SHA d'entrée `c9d65cf` à 00:58:15Z → dernier commit de code `f23be00` à 01:21:53Z, puis vérifications finales)
- **Started:** 2026-09-27T00:58:15Z
- **Completed:** 2026-09-27T01:30:00Z (approx.)
- **Tasks:** 3 / 3 (TDD : RED → GREEN, mutations jouées et révoquées)
- **Files modified:** 15 (11 créés, 4 modifiés dont REQUIREMENTS.md)
- **Tests :** 1200 → **1268** (+68 sur l'arbre partagé : +33 de ce plan — 10 + 10 + 12 + 1 garde — les autres viennent des plans 32-01/02/03 exécutés en parallèle) ; suite complète **0 échec, deux exécutions consécutives** (1268 / 1268, 11 s et 9 s)

## Accomplishments

- **JRN-03 (types et ligne)** : `ReleveJournal`, `EvenementJournal`/`TypeEvenement`/`TypeEvenementTexte`, `LigneJournal` (sérialisation exacte au contrat `{v,t,u5,r5,u7,r7,statut5,statut7,overage,overage_statut,source}` / `{v,t,ev,magasin,cause,version}`, parsing tolérant), `LecteurJournal.LireFichier` — la fixture de tolérance rend 2 relevés, 2 événements (dont 1 `NonReconnu` « teleportation »), 4 lignes ignorées, la ligne vide ni lue ni comptée.
- **JRN-03 (gardes)** : `ServicesLayerPurityTests` filtre par préfixe (`EstTypeNeutre`) et prouve qu'il voit `Chronos.Services.Historique` / `Chronos.Models.Historique` ; `GardesDoctrineTests` ×2 et `NormalisationUniqueTests` balaient en `AllDirectories`. Aucun `System.Windows` sous `Historique/`.
- **JRN-01 / JRN-03 (écrivain)** : `JournalReleves` — deux instances sur deux threads écrivant chacune la même séquence de 100 relevés produisent **exactement 100 lignes, 0 ignorée, t distincts et croissants** ; mois UTC ; UTF-8 sans BOM ; `\n` seul ; `DerniereEcriture`/`DerniereErreur` ; `DernierT(source)` relit la queue ; `Purger()` = `{Supprimés 1, Échecs 0, Ignorés 2}` sur le jeu de fichiers du plan ; seuils dérivés de `CadenceNominale`.
- **JRN-01 / JRN-02 (décorateur)** : six `GetAsync` sur la même instance → 1 ligne, snapshot rendu `Assert.Same` ; plancher `Estimated`, `Unavailable`, `MagasinDernierExact` → 0 ligne (le dossier n'est même pas créé) ; deux sources → deux lignes à leur instant ; `overage`/`overage_statut`/`statut5` voyagent ; `demarrage` (version) / `arret` / `jeton_invalide` (transition) / `sonde_refusee` (transition, cause) / `reprise` (« trou de 25 min », datée du relevé qui met fin au trou, écrite avant lui) / `ecriture_ratee` (magasin, cause).

## Task Commits

SHA d'entrée du plan : `c9d65cf`.

1. **Task 1 — Types neutres, une ligne JSONL dans les deux sens, quatre gardes élargies (JRN-03)**
   - RED 11 : `5833b13` — `test(32-04): une ligne de journal dans les deux sens, lecture tolerante, la garde de purete voit Historique, RED 11` (CS0234/CS0246 : `Chronos.Models.Historique`, `Chronos.Services.Historique`, `ReleveJournal` introuvables → les 11 tests ne compilent pas)
   - GREEN : `030d65a` — `feat(32-04): types du journal, LigneJournal (serialisation/parsing tolerant), LecteurJournal.LireFichier ; gardes elargies aux sous-dossiers Historique (JRN-03)` — filtre : 24 verts
2. **Task 2 — JournalReleves : append exclusif idempotent, deux écrivains, mois UTC, rétention (JRN-01, JRN-03)**
   - RED 10 : `1d41f73` — `test(32-04): journal des releves - append idempotent, deux ecrivains, mois UTC, retention, RED 10` (CS0246 `JournalReleves`)
   - GREEN : `5375ef8` — `feat(32-04): JournalReleves - append exclusif idempotent (t, source), mois UTC, retention 24 mois, DerniereEcriture/DerniereErreur (JRN-01, JRN-03)` — filtre : 23 verts
   - Correctif documentaire : `f39da31` — `docs(32-04): JournalReleves - le commentaire ne reproduit plus le jeton du mode d'ouverture condamne` (critère `grep -c "FileMode.Append" = 0`, doctrine phase 19)
3. **Task 3 — JournalisationUsageProvider : n'écrit que l'exact de l'inner, événements de couverture (JRN-01, JRN-02)**
   - RED 12 : `4e7053c` — `test(32-04): le decorateur de journalisation - dedup (t, source), exclusions, evenements de couverture, RED 12` (CS0246 `JournalisationUsageProvider`)
   - GREEN : `9149de3` — `feat(32-04): JournalisationUsageProvider - n'ecrit que l'exact de l'inner, dedup (t, source), demarrage/arret/jeton_invalide/sonde_refusee/reprise/ecriture_ratee (JRN-01, JRN-02)` — filtre : 25 verts
   - Renforcement : `f23be00` — `test(32-04): Six_GetAsync prouve aussi qu'un rejeu ne touche pas le disque` (rend la mutation k1 observable, voir Deviations)

**Plan metadata :** commit `docs(32-04): complete …` (SUMMARY + REQUIREMENTS.md) — voir le dernier commit du plan.

## Mutations (jouées, rouges nommés, révoquées — sha256 identiques avant/après)

| Mutation | Contenu | Test(s) rougi(s) | Révocation |
|---|---|---|---|
| (l1) | `Parser` : `t` lu avant `v`, `v` absent accepté | `LigneJournalTests.Une_ligne_avec_v_inconnu_ou_sans_t_est_refusee` | `LigneJournal.cs` sha256 `e51e50e9…31c6a` = avant |
| (l2) | `EstTypeNeutre` ⇒ `t.Namespace is "Chronos.Services" or "Chronos.Models"` | `ServicesLayerPurityTests.La_garde_de_purete_voit_les_sous_namespaces_Historique` | `ServicesLayerPurityTests.cs` sha256 `7493f936…0a7f5` = avant |
| (j1) | relecture SANS verrou exclusif (`FileShare.ReadWrite`, lecture seule) puis ouverture en mode « ajout » partagé — le motif condamné par la recherche | `JournalRelevesTests.Deux_ecrivains_concurrents_200_ecritures_zero_doublon_zero_troncature` — **ROUGE dès la première passe, et à la recapture** (aucune relance nécessaire : (j1) n'est jamais resté vert) | `JournalReleves.cs` sha256 `e12d2afa…311b3` = avant |
| (j2) | relecture de queue neutralisée (`dejaPresent` toujours faux) | `Deux_ecrivains_concurrents…` **+** `Le_meme_instant_pour_la_meme_source_n_est_ecrit_qu_une_fois`, `Un_instant_plus_ancien_que_le_dernier_de_la_source_est_refuse`, `DernierT_relit_la_queue_du_fichier_du_mois` (4 rouges : la preuve déterministe) | idem |
| (k1) | `t <= d` → `t < d` | `JournalisationUsageProviderTests.Six_GetAsync_sur_la_meme_instance_de_snapshot_ecrivent_une_seule_ligne` | `JournalisationUsageProvider.cs` sha256 `e78e28ae…eb7f5` = avant |
| (k2) | exclusion `Source == MagasinDernierExact` retirée | `Une_fenetre_du_magasin_n_entre_jamais` | idem |
| (k3) | `sonde_refusee` sans mémoire de transition | `Une_saturation_ou_un_refus_ecrit_sonde_refusee_sur_transition` | idem |

Observation (j1), passe détaillée : **162 lignes physiques au lieu de 100** (`Assert.Equal(100, lignes.Length)` → `Actual: 162`), soit 62 doublons — les deux écrivains passent la relecture NON verrouillée avant que l'autre n'ait écrit, puis ajoutent chacun la ligne. Trois passes (j1) jouées au total (silencieuse, recapture, détaillée) : trois rouges, aucune verte — le motif condamné par la recherche est condamné par le test.

## Files Created/Modified

- `src/Chronos/Models/Historique/ReleveJournal.cs` — record neutre du relevé (10 champs, `null` = absence)
- `src/Chronos/Models/Historique/EvenementJournal.cs` — `TypeEvenement` (7 membres dont `NonReconnu`, jamais écrit), `EvenementJournal`, `TypeEvenementTexte` (`NomsDeFil`, `Nom`, `Depuis`)
- `src/Chronos/Services/Historique/LigneJournal.cs` — `SchemaVersion = 1`, `ChampsReleve`, `Serialiser` ×2, `Parser` ; STJ, `JsonStringEnumConverter`, `WhenWritingNull`, `UnsafeRelaxedJsonEscaping`
- `src/Chronos/Services/Historique/LecteurJournal.cs` — `LectureFichier` (+ `Vide`), `LireFichier` (partage `ReadWrite | Delete`, 20 reprises courtes à l'ouverture, jamais fatal)
- `src/Chronos/Services/Historique/IEtatJournal.cs` — `Dossier`, `DerniereEcriture`, `DerniereErreur`, `RelevesEcrits`
- `src/Chronos/Services/Historique/JournalReleves.cs` — `BilanRetention`, `JournalReleves` (`RetentionMois`, `SeuilReprise`, `SeuilMuet`, `NomFichier`, `CheminDuMois`, `AjouterReleve`, `AjouterEvenement`, `DernierT`, `Purger`, `EvenementsEcrits`)
- `src/Chronos/Services/Historique/JournalisationUsageProvider.cs` — décorateur + hosted service, `RelevesJournalises`, `SignalerEcritureRatee`
- `tests/Chronos.Tests/LigneJournalTests.cs` (10), `JournalRelevesTests.cs` (10), `JournalisationUsageProviderTests.cs` (12), fixture `TestData/journal/tolerance/releves-2026-09.jsonl` (9 lignes LF sans BOM)
- `tests/Chronos.Tests/ServicesLayerPurityTests.cs` (+1 test, `EstTypeNeutre`), `GardesDoctrineTests.cs` (2 × `AllDirectories`), `NormalisationUniqueTests.cs` (1 × `AllDirectories`)
- `.planning/REQUIREMENTS.md` — JRN-01, JRN-02, JRN-03 cochés (traçabilité « Complete »)

## Decisions Made

Voir `key-decisions` (D-32-14 à D-32-20 écrites dans le plan, plus six décisions d'exécution : `EssaisMax = 600`, dossier créé hors reprises, `Purger()` sur tous les fichiers, mémoire avancée sur refus sans erreur, un `sonde_refusee` par tick, `InstantDepuisIso` comme point unique).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug latent] `EssaisMax` porté de 60 à 600 dans `JournalReleves`**
- **Found during:** Task 2 (conception du test à deux écrivains)
- **Issue:** avec 60 essais (12 `Yield` + 48 × `Sleep(1)` ≈ 50 ms), deux écrivains en boucle serrée se font échouer mutuellement : le détenteur tient le fichier ~95 % du temps, P(60 échecs consécutifs) ≈ 5 % par écriture → le test aurait été instable et un second overlay en rattrapage aurait posé `DerniereErreur` à tort.
- **Fix:** `EssaisMax = 600` (≈ 0,6 s au pire), commentaire qui dit pourquoi la borne diffère de celle des hooks (écritures éparses).
- **Files modified:** `src/Chronos/Services/Historique/JournalReleves.cs`
- **Verification:** `Deux_ecrivains_concurrents…` vert (200 écritures, 100 lignes, 0 erreur) sur chaque exécution (filtre ×3, suite ×2).
- **Committed in:** `5375ef8`

**2. [Rule 1 - Bug latent] Création du dossier HORS de la boucle de reprises**
- **Found during:** Task 2 (test « dossier poison »)
- **Issue:** `Directory.CreateDirectory` sur un chemin occupé par un FICHIER lève une `IOException` — dans la boucle du plan, elle aurait été reprise 600 fois (0,6 s à chaque tick de 60 s, pour rien) avant d'être consignée.
- **Fix:** création du dossier dans son propre `try` avant la boucle ; toute exception → `DerniereErreur`, retour immédiat.
- **Files modified:** `JournalReleves.cs`
- **Verification:** `Une_ecriture_reussie_pose_DerniereEcriture_et_un_echec_pose_DerniereErreur_sans_lever` vert, instantané.
- **Committed in:** `5375ef8`

**3. [Rule 1 - Bug] Mémoire `_dernierT` avancée sur un refus SANS erreur**
- **Found during:** Task 3 (conception de la logique `reprise`)
- **Issue:** si un autre processus a déjà écrit le relevé, `AjouterReleve` rend `false` ; sans avancer la mémoire, le décorateur aurait réémis le même événement `reprise` à chaque tick tant que le relevé n'était pas remplacé.
- **Fix:** `if (ecrit || _journal.DerniereErreur is null) _dernierT[source] = t;` — seul un ÉCHEC d'écriture laisse la mémoire en place (le prochain tick réessaie).
- **Files modified:** `JournalisationUsageProvider.cs`
- **Verification:** `Un_ecart_de_plus_de_dix_minutes_ecrit_reprise_avant_le_releve` : exactement 1 `reprise` sur 3 relevés.
- **Committed in:** `9149de3`

**4. [Rule 2 - Test rendu probant] Mutation (k1) masquée par la double protection → assertion ajoutée au test nommé**
- **Found during:** Task 3 (analyse avant de jouer (k1))
- **Issue:** `t <= d` → `t < d` dans le décorateur est MASQUÉ par la relecture de queue de `JournalReleves` (le fichier refuse le doublon) : `Six_GetAsync…` serait resté vert et la mutation n'aurait rien prouvé.
- **Fix:** le test supprime le fichier après la première écriture et rejoue la même instance deux fois : le fichier ne doit PAS renaître (la dédup mémoire évite le verrou exclusif à chaque tick — sa raison d'être). Sous (k1), le fichier renaît → rouge.
- **Files modified:** `tests/Chronos.Tests/JournalisationUsageProviderTests.cs`
- **Verification:** filtre 12 verts sans mutation ; (k1) rougit exactement `Six_GetAsync_sur_la_meme_instance_de_snapshot_ecrivent_une_seule_ligne`.
- **Committed in:** `f23be00`

**5. [Rule 1 - Critère d'acceptation] Le commentaire de `JournalReleves` reproduisait le jeton `FileMode.Append`**
- **Found during:** Task 2 (greps d'acceptation : `grep -c "FileMode.Append"` = 1 au lieu de 0)
- **Issue:** le texte de doc suggéré par le plan contenait le jeton interdit par son propre critère ; la doctrine de la phase 19 (un commentaire qui reproduit l'expression fautive tue le critère de non-retour) s'applique.
- **Fix:** reformulé « le mode d'ouverture « ajout » de FileStream ».
- **Files modified:** `JournalReleves.cs`
- **Committed in:** `f39da31`

**6. [Rule 2 - Robustesse] `LecteurJournal.LireFichier` reprend 20 fois à l'ouverture**
- **Found during:** Task 1
- **Issue:** un écrivain `FileShare.None` tient le fichier quelques microsecondes ; une lecture qui rendrait « vide » au premier refus mentirait sur un fichier plein.
- **Fix:** 20 essais courts (`Yield` puis `Sleep(1)`), puis vide ; jamais bloquant, jamais fatal.
- **Files modified:** `LecteurJournal.cs`
- **Committed in:** `030d65a`

**7. [Rule 1 - Spécification] `Purger()` énumère tous les fichiers (pas seulement `releves-*.jsonl`)**
- **Found during:** Task 2
- **Issue:** le plan suggérait `EnumerateFiles(Dossier, "releves-*.jsonl")` ET attendait `Ignores = 2` pour `notes.txt` + `releves-abcd.jsonl` — incompatible.
- **Fix:** énumération complète, nom non conforme au `Regex` → `Ignores++`.
- **Committed in:** `5375ef8`

---

**Total deviations:** 7 auto-fixed (5 × Rule 1, 2 × Rule 2). Aucune modification hors de `files_modified` ; `App.xaml.cs`, `ChronosPaths.cs`, `DiagnosticService.cs`, `LastExactStore.cs`, `ROADMAP.md`, `STATE.md` non touchés par ce plan (vérifié sur les 8 commits `(32-04)`).
**Impact on plan:** toutes nécessaires à la stabilité du test à deux écrivains, à l'exactitude des événements ou à la valeur probante des mutations. Pas d'élargissement de périmètre.

## Issues Encountered

- **Exécution parallèle (4 agents, même arbre)** : à quatre reprises, `dotnet test` a échoué sur le RED d'un plan voisin (32-02 : `LastExactStoreTests`, `VueAppDataTests`, `DiagnosticServiceTests` ; 32-01 : `TranscriptActivityProvider.cs`). Traité comme prévu : attente 45 s et relance, par scripts qui appliquent la mutation, lancent le filtre, RESTAURENT le fichier et comparent le sha256 quelle que soit l'issue. Aucune fausse conclusion, aucun fichier laissé muté.
- **`DateTimeOffset` nu sous STJ** aurait écrit `2026-09-27T10:05:00+00:00` (fractions omises, offset d'origine) : les instants passent par une chaîne `ToUniversalTime().ToString("O")`, ce qui fixe le format et l'UTC.
- **Encodeur STJ par défaut** échappe `+` en `+` : `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (fichier privé, UTF-8, rien à protéger).
- **`NormalisationUniqueTests` élargie** aurait rougi sur un `DateTimeOffset.TryParse` dans `LigneJournal.cs` : `UsageNormalization.InstantDepuisIso` (déjà le point unique) le remplace — pas d'exemption ajoutée.
- **Mesure (D-32-20)** : relevé complet sans overage 216 o (LF compris), avec overage 270 o ; fixture 81,8 o/ligne en moyenne (événements courts) ; ≈ 75 Ko/jour à 288 relevés, ≈ 2,3 Mo/mois.
- **(j1) détail chiffré** : 162 lignes pour 100 relevés distincts (62 doublons), relevé à la 5e tentative de la passe détaillée — les quatre premières sont tombées sur l'édition en cours de `TranscriptActivityProvider.cs` par 32-01 (CS0103 `dedup`), attendues et relancées.

## Known Stubs

Aucun stub. Le journal est **volontairement NON BRANCHÉ** (objectif du plan) : aucune inscription DI, aucun appel depuis `App.xaml.cs` — c'est 32-05 qui l'insère. Rien dans ce plan ne rend une valeur vide à une UI.

## Pour 32-05 — câblage attendu (contrat de ce plan)

```csharp
// App.xaml.cs — l'inner de la tête devient le décorateur ; le composite reste inchangé.
services.AddSingleton(sp => new JournalReleves(sp.GetRequiredService<ChronosPaths>().HistoriqueDir, sp.GetRequiredService<IClock>()));
services.AddSingleton<IEtatJournal>(sp => sp.GetRequiredService<JournalReleves>());
services.AddSingleton(sp => new JournalisationUsageProvider(
    inner: /* CompositeUsageProvider actuel */,
    journal: sp.GetRequiredService<JournalReleves>(),
    etatServeur: sp.GetRequiredService<IEtatServeur>(),      // sonde_refusee (transition)
    authStatus: sp.GetRequiredService<IAuthStatus>(),        // jeton_invalide (transition vers Deconnecte)
    clock: sp.GetRequiredService<IClock>()));                // version : lue de l'assembly si null
services.AddHostedService(sp => sp.GetRequiredService<JournalisationUsageProvider>());   // AVANT RefreshOrchestrator
services.AddSingleton<IUsageProvider>(sp => new LastExactUsageProvider(inner: sp.GetRequiredService<JournalisationUsageProvider>(), …));
```

- `LastExactUsageProvider` (ou `LastExactStore.EcritureRatee` de 32-02) → `JournalisationUsageProvider.SignalerEcritureRatee("last-exact", cause)` pour l'événement `ecriture_ratee`.
- Alerte JRN-04 : `now − max(démarrage, IEtatJournal.DerniereEcriture ?? démarrage) > JournalReleves.SeuilMuet` → « journal muet depuis N min ».
- 32-02 a introduit `IEtatMagasin (Nom, Chemin, DerniereEcriture, DerniereErreur)` + `NomsMagasins` : `JournalReleves` peut l'implémenter en trois lignes (`Nom` = le nom du journal dans `NomsMagasins`, `Chemin` = `Dossier`) — non fait ici pour ne pas coupler deux plans en cours ; à faire dans 32-05 avec le câblage du diagnostic.
- `CompositionRootTests` (garde DI) : le journal est entre la tête et le composite ; hosted service inscrit avant l'orchestrateur.

## Next Phase Readiness

- 32-05 dispose du décorateur, de l'écrivain, d'`IEtatJournal` et des seuils ; rien à inventer.
- 32-06 étendra `LecteurJournal` avec `Lire(dossier, de, a)` dans le même fichier (mois UTC chevauchant la plage) et `AnalyseReleves`.
- 32-07 comparera `LigneJournal.ChampsReleve` et `TypeEvenementTexte.NomsDeFil` à `docs/data-sources.md` §7.
- Mises à jour d'état laissées à l'orchestrateur (règle d'exécution parallèle) : `state advance-plan`, `state update-progress`, `state record-metric`, `roadmap update-plan-progress` — non exécutées par cet agent ; `STATE.md` et `ROADMAP.md` non modifiés.

## Self-Check: PASSED

- Fichiers créés (11/11 FOUND) : `ReleveJournal.cs`, `EvenementJournal.cs`, `LigneJournal.cs`, `IEtatJournal.cs`, `JournalReleves.cs`, `LecteurJournal.cs`, `JournalisationUsageProvider.cs`, `LigneJournalTests.cs`, `JournalRelevesTests.cs`, `JournalisationUsageProviderTests.cs`, `TestData/journal/tolerance/releves-2026-09.jsonl`.
- Commits (8/8 FOUND) : `5833b13`, `030d65a`, `1d41f73`, `5375ef8`, `f39da31`, `4e7053c`, `9149de3`, `f23be00`.
- `[Fact]` : 10 + 10 + 12 = 32 (+1 dans `ServicesLayerPurityTests`) ; `git status` vide sur tous les fichiers du plan (tout committé).
- Fichiers interdits : aucun des 8 commits ne touche `App.xaml.cs`, `ChronosPaths.cs`, `DiagnosticService.cs`, `LastExactStore.cs`, `ROADMAP.md`, `STATE.md`.
- Suite complète : 1268 / 0 puis 1268 / 0.

---
*Phase: 32-compter-juste-puis-journaliser*
*Completed: 2026-09-27*
