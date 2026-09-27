---
phase: 33-agr-gats-de-tokens
plan: 03
subsystem: reconstruction de fond des agrégats de tokens (BackgroundService + thread dédié, progression en entiers, reprise idempotente)
tags: [TOK-02, TOK-03, ReconstructionTokens, IEtatReconstruction, PhaseReconstruction, ProjectionAgregats, BackgroundService, thread-dedie, BelowNormal, mtime-decroissant, semaine-courante, flush-ids-agregats-curseurs, reprojection, idempotence-octets, cle-synthetique-sans-id]

# Dependency graph
requires:
  - phase: 33-01 (TOK-01)
    provides: MagasinAgregats (Appliquer charge un mois gelé avant delta, RemplacerMois, EcrireMoisSales rend false sous lecteur → mois toujours sale, Purger, CheminDuMois), CouvertureTokens (VoirLigne, GarantirPasse, Sauvegarder, Charger, NomFichier), TrancheTokens.SlotDe, DeltaTranche, LigneAgregat.Parser (tests), garde TOK-05 réflexive + textuelle
  - phase: 33-02 (TOK-03)
    provides: LecteurTranscript.Lire (offsets, annulation toutes les 4096 lignes, Annulee), IndexMessages (Charger, Ajouter → DeltaMessage?, Entrees(mois), MoisOuverts, Flush, Purger, IdsConnus, LignesIgnorees), Curseurs (Charger, CleRelative, Classer, Enregistrer, RetirerDisparus, Sauvegarder, NomFichier), 14 fixtures réelles anonymisées
  - phase: 33 (recherche)
    provides: Pattern 3 (thread dédié + TaskCompletionSource, boucle synchrone), Pattern 2 (ordre shard → agrégats → curseurs, reprojection), Pitfall 1 (continuation asynchrone = priorité perdue), Pitfall 7 (semaine courante par mtime), mesures 2,4–2,6 s / 1,7 s
  - phase: 4 / 32 (existant)
    provides: RefreshOrchestrator (motif BackgroundService neutre), JournalisationUsageProvider (hosted service inscrit AVANT l'orchestrateur), ChronosPaths.HistoriqueDir, IClock, FakeClock, FakeEtatJournal (moule du faux)
provides:
  - Models/Historique/Tokens/PhaseReconstruction.cs — enum { JamaisLancee, Reconstruction, Incremental, Arretee, EnEchec }, XML-doc par valeur
  - Services/Historique/Tokens/IEtatReconstruction.cs — contrat NEUTRE de progression (Phase, FichiersTraites, FichiersTotal, FichiersOuvertsDernierePasse, SemaineCouranteDisponible, DernierFichier, DerniereErreur, FichiersDisparus, LignesIgnorees, IdsConnus, DureeMurDernierePasse, DureeCpuProcessusDernierePasse, DerniereReconstructionTerminee, événement Changement) — entiers et états, jamais un rapport
  - Services/Historique/Tokens/ProjectionAgregats.cs — Projeter(entrées d'ids) → tranches (slot, modèle ordinal, origine) sommées, N = nombre d'ids ; pur
  - Services/Historique/Tokens/ReconstructionTokens.cs — BackgroundService + thread IsBackground / BelowNormal / « Chronos.AgregatsTokens », boucle synchrone, ExecuterUnePasse(ct) publique → BilanPasse, constantes CadenceIncrementale 60 s / SemaineCourante 7 j / FichiersParLot 100 / DelaiEntreLots 2 s / NomThread / PrefixeSansId, RaccourcisRelusDernierePasse
  - tests/Chronos.Tests/Fakes/FakeEtatReconstruction.cs — faux réglable (toutes propriétés { get; set; }, Declencher()) pour 33-05 (VM, diagnostic)
  - 16 tests (3 projection + 13 Fact/Theory = 15 cas reconstruction) dont la preuve d'idempotence sur les octets (k = 1, 3, 5) et deux pannes simulées entre étapes du flush
affects: [33-05 (DI : AddSingleton ReconstructionTokens + IEtatReconstruction + AddHostedService AVANT RefreshOrchestrator ; diagnostic : section « Agrégats de tokens » ; docs §8 : D-33-13…D-33-18 dont la clé synthétique des lignes sans id), 34 (bandeau F2 : « N / M fichiers · la semaine courante est déjà complète » depuis IEtatReconstruction), 35 (constat sur la vraie machine)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "BackgroundService qui POSSÈDE un thread : ExecuteAsync démarre new Thread { IsBackground, BelowNormal, Name } et rend un TaskCompletionSource.Task (RunContinuationsAsynchronously) ; StartAsync rend la main immédiatement, StopAsync annule puis attend ce Task ; la boucle est SYNCHRONE (aucun await), quantum cédé entre fichiers, attente annulable par ct.WaitHandle.WaitOne(cadence)"
    - "État de progression lisible depuis un autre thread sans verrou : champs int/bool/référence écrits par Volatile.Write et lus par Volatile.Read ; les durées et instants sont des ticks long (−1 = null) exposés en TimeSpan?/DateTimeOffset? — un struct de 16 octets n'est pas atomique"
    - "Passe testable sans thread : ExecuterUnePasse(ct) publique, ne lève jamais (exception → EnEchec + DerniereErreur + bilan non complet), appelée par la boucle du thread"
    - "Hooks de test injectés par le constructeur (apresFichier, apresEtapeFlush) : observer l'ordre des fichiers, capturer le thread courant, annuler après k fichiers, simuler une panne EN LEVANT depuis le hook entre deux étapes du flush"
    - "Preuve d'idempotence sur les OCTETS : référence = passe ininterrompue sur une copie A ; sur une copie B, instance annulée après k fichiers puis instance NEUVE (nouveaux magasin et index, même dossier) → sha256 identique, et 6 − k fichiers seulement rouverts"
    - "Reprojection sélective au démarrage : un mois ouvert est reprojeté depuis l'index s'il a au moins un id OU si son fichier existe déjà (remplacé, même vide) ; un mois sans id ni fichier ne fait naître aucun fichier vide"

key-files:
  created:
    - src/Chronos/Models/Historique/Tokens/PhaseReconstruction.cs
    - src/Chronos/Services/Historique/Tokens/IEtatReconstruction.cs
    - src/Chronos/Services/Historique/Tokens/ProjectionAgregats.cs
    - src/Chronos/Services/Historique/Tokens/ReconstructionTokens.cs
    - tests/Chronos.Tests/Fakes/FakeEtatReconstruction.cs
    - tests/Chronos.Tests/ProjectionAgregatsTests.cs
    - tests/Chronos.Tests/ReconstructionTokensTests.cs
  modified:
    - .planning/REQUIREMENTS.md (TOK-02 → Complete)

key-decisions:
  - "D-33-13 — thread dédié IsBackground + BelowNormal piloté par un BackgroundService ; boucle SYNCHRONE ; le quantum est cédé entre fichiers par Thread.Yield() et l'annulation vérifiée entre fichiers et toutes les 4 096 lignes (dans le lecteur). L'énoncé « Task.Yield entre fichiers » de TOK-02 est tenu dans son INTENTION (ne jamais monopoliser, rester interruptible), pas au pied de la lettre : une continuation asynchrone sur un new Thread repart sur le pool à priorité normale (Pitfall 1) — mutation (a) rouge"
  - "D-33-14 — ordre de flush ids (ajout) → agrégats (Move) → curseurs (Move), reprojection des mois ouverts au démarrage : un arrêt à n'importe quel point est rattrapé (mutations (b) et (b') rouges sur la panne « après ids »)"
  - "D-33-15 — checkpoint « semaine courante disponible » par MTIME (SemaineCourante = 7 j) au premier fichier plus vieux qu'une semaine (tri décroissant ⇒ tous ceux de la semaine sont lus et FLUSHÉS avant) ; flush par lot FichiersParLot = 100 ou DelaiEntreLots = 2 s ; CadenceIncrementale = 60 s ; la couverture n'est garantie (GarantirPasse + Sauvegarder) que par une passe COMPLÈTE — annulée ≠ complète"
  - "D-33-16 — le curseur enregistre la taille et le mtime relevés à l'INVENTAIRE : tout ce qui bouge après sera vu au cycle suivant (avec D-33-09, un curseur en retrait de la taille n'est jamais « inchangé »)"
  - "D-33-17 — une passe en échec ne tue pas le service : Phase = EnEchec, DerniereErreur = première erreur de flush préfixée par la brique (« index : », « agrégats : », « curseurs.json : », « couverture : ») ou exception capturée « Type : message » ; PAS de nouveau flush dans le catch (la panne peut être dans le flush) ; nouvelle passe au cycle suivant ; un flush réussi de bout en bout remet DerniereErreur à null"
  - "D-33-18 (exécution) — une ligne sans message.id ni requestId reçoit côté reconstruction une CLÉ SYNTHÉTIQUE déterministe « sans-id:{ticks UTC}:{modèle}:{0|1}:{in}:{out}:{cache_w}:{cache_r} » avant d'entrer dans l'index. Sans elle, l'invariant octet pour octet est FAUX dès k = 1 : la première instance écrit ids + agrégats + curseur de s1 (multi-blocs, une ligne sans id), la seconde reprojette depuis l'index (qui ignore cette ligne) et ne rouvre pas s1 (inchangé) → la tranche 11:15Z perd (1, 1, 0, 0, N 1). La brique IndexMessages garde sa règle D-33-11 (Id null → jamais indexé) : c'est l'orchestrateur qui fournit une clé. Clé indépendante du fichier (un renommage ou une copie ne recompte pas) ; deux lignes sans id strictement identiques (même instant, même modèle, mêmes quatre compteurs) sont indiscernables et comptent une fois — limite à documenter au §8 (33-05)"
  - "Exécution — reprojection SÉLECTIVE : un mois ouvert sans aucun id et sans fichier n'est pas reprojeté (sinon une racine absente ferait naître tokens-2026-08.jsonl et tokens-2026-09.jsonl vides) ; un fichier existant est toujours remplacé par la projection, même vide (c'est l'état vrai)"
  - "Exécution — SemaineCouranteDisponible n'est posé à vrai que si le flush du checkpoint a RÉUSSI (ou en fin de passe complète avec flush final sans erreur) : « disponible » veut dire « sur le disque »"
  - "Exécution — inventaire par EnumerationOptions { RecurseSubdirectories, IgnoreInaccessible, AttributesToSkip = 0 } matérialisé dans le try, chaque FileInfo sous son propre try (un fichier qui disparaît entre l'énumération et ses attributs est simplement absent de la passe)"

patterns-established:
  - "Runner de mutation en shell pur (fonction jouer/lancer) : sauvegarde par copie dans le scratchpad, mutation par sed ou réassemblage par numéros de ligne, filtre, restauration par copie, sha256 avant/après — réutilisable pour 33-05"

requirements-completed: [TOK-02]

# Metrics
duration: 22min
completed: 2026-09-27
---

# Phase 33 Plan 03 : Reconstruction de fond des agrégats de tokens — Summary

**Un `BackgroundService` qui possède un thread `IsBackground` `BelowNormal` nommé, dont `StartAsync` rend la main en moins de 50 ms et dont la boucle synchrone lit les transcripts du plus récent au plus ancien, écrit la semaine courante avant l'historique dans l'ordre ids → agrégats → curseurs, ne rouvre en incrémental que ce qui a bougé, dit ses échecs sans mourir, et dont la reprise après annulation (k = 1, 3, 5) ou après panne entre deux étapes du flush rend `tokens-2026-09.jsonl` identique octet pour octet à une passe ininterrompue — grâce à la reprojection au démarrage et à une clé synthétique pour les lignes sans id.**

## Performance

- **Duration:** ≈ 22 min (10:54Z → 11:16Z)
- **Started:** 2026-09-27T10:54Z — SHA d'entrée `af7f52a` (1373 verts, 0 warning)
- **Completed:** 2026-09-27T11:16Z
- **Tasks:** 3 / 3 (TDD : 3 RED + 2 GREEN ; la Task 3 n'a demandé aucun code de production)
- **Files:** 7 créés (4 sources, 3 tests) + REQUIREMENTS.md

## Accomplishments

- **TOK-02 tenue côté code** : reconstruction sur thread dédié à priorité basse, mtime décroissant, semaine courante disponible (et sur le disque) avant l'historique, lecture en flux avec pré-filtre (33-02), dédup par `IndexMessages` (CPT-01 via `DedupUsage.Fusionner`), interruptible (annulation honorée en < 200 ms, dernier flush compris), progression N / M et états exposés par `IEtatReconstruction` — en entiers, jamais un rapport.
- **Critère 3 de la phase prouvé de bout en bout** : arrêt après 1, 3 ou 5 fichiers, panne juste après l'écriture des ids, panne juste après l'écriture des agrégats → à la reprise par une instance neuve, mêmes octets qu'en une seule traite, `Curseurs.Count == 6`, `IdsConnus == 13`, et seuls les `6 − k` fichiers non persistés sont rouverts.
- **Incrémental** : fichier inchangé jamais ouvert (`FichiersOuvertsDernierePasse == 0`, sha256 inchangé), grandi relu depuis son offset (FORK06 → tranche 14:15Z = 15 / 150 / 1 500 / 15 000, N 3), raccourci relu de zéro sans rien recompter (sha256 identique, `RaccourcisRelusDernierePasse == 1`), disparu retiré des curseurs sans rien soustraire (la tranche 10:45Z survit — critère DoD « l'historique déjà journalisé survit à une purge »).
- **Robustesse** : dossier historique « poison » (un fichier porte son nom) → `EnEchec`, `DerniereErreur` = `IOException : …`, la passe suivante réussit ; racine absente → passe vide, `Incremental`, aucun fichier d'agrégats créé ; jeton déjà annulé → rien lu, rien compté, pas `EnEchec`.
- Suite complète sur l'arbre réel : **1404 verts, deux fois de suite, 0 warning** (1373 + 16 de ce plan + 15 de 33-04 commités dans le même arbre).

## Task Commits

1. **Task 1 : PhaseReconstruction, IEtatReconstruction, ProjectionAgregats, FakeEtatReconstruction** — RED `631f5c3` (test, 3 rouges par compilation CS0103) → GREEN `d507349` (feat) ; filtre projection + garde TOK-05 + pureté : 10 / 10
2. **Task 2 : ReconstructionTokens** — RED `a19a02f` (test, 7 rouges par compilation CS0246) → GREEN `8c5b4a8` (feat) ; filtre reconstruction + gardes (TOK-05, pureté, dédup, doctrine, normalisation) : 28 / 28
3. **Task 3 : preuves de fond** — `2bb6e83` (test, +6 dont une `[Theory]` × 3) : **verts dès le premier passage** (15 / 15) — aucun diff de production, conformément au plan (« normalement aucun code à changer si la Task 2 est fidèle ») ; ce sont les mutations (a), (b), (b') qui prouvent que ces tests mordent

**Plan metadata:** commit final `docs(33-03)` (SUMMARY + REQUIREMENTS.md, TOK-02 seul — l'arbre était propre à l'instant du stage, 33-04 ayant commité `daf4831` entre-temps).

## API réellement livrée (pour 33-05)

```csharp
namespace Chronos.Models.Historique.Tokens;
public enum PhaseReconstruction { JamaisLancee, Reconstruction, Incremental, Arretee, EnEchec }

namespace Chronos.Services.Historique.Tokens;
public interface IEtatReconstruction
{
    PhaseReconstruction Phase { get; }
    int FichiersTraites { get; }                      // « N » du bandeau F2 (fichiers passés en revue dans la passe en cours, ouverts ou non)
    int FichiersTotal { get; }                        // « M » (inventaire de la passe en cours)
    int FichiersOuvertsDernierePasse { get; }
    bool SemaineCouranteDisponible { get; }           // vrai = la semaine (par mtime) est SUR LE DISQUE
    string? DernierFichier { get; }                   // chemin RELATIF à ProjectsRoot, jamais absolu
    string? DerniereErreur { get; }                   // « brique : Type : message » ou « Type : message »
    int FichiersDisparus { get; }                     // cumul depuis le démarrage
    int LignesIgnorees { get; }                       // lecteur (cumul) + index
    int IdsConnus { get; }
    TimeSpan? DureeMurDernierePasse { get; }
    TimeSpan? DureeCpuProcessusDernierePasse { get; } // CPU du PROCESSUS (Process.TotalProcessorTime) pendant la passe
    DateTimeOffset? DerniereReconstructionTerminee { get; }   // horloge injectée, fin de la dernière passe COMPLÈTE
    event EventHandler? Changement;                   // levé SUR LE THREAD DE FOND : le consommateur marshalle (IUiDispatcher.Post)
}

public static class ProjectionAgregats
{
    public static IReadOnlyList<TrancheTokens> Projeter(IEnumerable<(DateTimeOffset Ts, string Model, bool Sub, long In, long Out, long CacheW, long CacheR)> entrees);
}

public sealed record BilanPasse(int FichiersTotal, int FichiersOuverts, int RaccourcisRelus, int Disparus, int LignesIgnorees, bool Complete, TimeSpan DureeMur);

public sealed class ReconstructionTokens : BackgroundService, IEtatReconstruction
{
    public static readonly TimeSpan CadenceIncrementale;   // 60 s
    public static readonly TimeSpan SemaineCourante;       // 7 j
    public const int FichiersParLot = 100;
    public static readonly TimeSpan DelaiEntreLots;        // 2 s
    public const string NomThread = "Chronos.AgregatsTokens";
    public const string PrefixeSansId = "sans-id:";
    public ReconstructionTokens(ChronosPaths paths, MagasinAgregats magasin, IndexMessages index, IClock clock,
                                TimeSpan? cadence = null, Action<string>? apresFichier = null, Action<string>? apresEtapeFlush = null);
    public int RaccourcisRelusDernierePasse { get; }       // hors interface (diagnostic)
    public BilanPasse ExecuterUnePasse(CancellationToken ct);   // synchrone, testable sans thread, ne lève jamais
    // ExecuteAsync : thread dédié + TaskCompletionSource ; StopAsync hérité (annule, attend le thread ≤ HostOptions.ShutdownTimeout)
}

// tests : Chronos.Tests.FakeEtatReconstruction : IEtatReconstruction — toutes propriétés { get; set; }, Declencher()
```

**Câblage attendu en 33-05 (App.xaml.cs, près du journal des relevés)** :
```csharp
services.AddSingleton(sp => new MagasinAgregats(sp.GetRequiredService<ChronosPaths>().HistoriqueDir, sp.GetRequiredService<IClock>()));
services.AddSingleton(sp => new IndexMessages(sp.GetRequiredService<ChronosPaths>().HistoriqueDir, sp.GetRequiredService<IClock>()));
services.AddSingleton(sp => new ReconstructionTokens(sp.GetRequiredService<ChronosPaths>(), sp.GetRequiredService<MagasinAgregats>(),
                                                     sp.GetRequiredService<IndexMessages>(), sp.GetRequiredService<IClock>()));
services.AddSingleton<IEtatReconstruction>(sp => sp.GetRequiredService<ReconstructionTokens>());
services.AddHostedService(sp => sp.GetRequiredService<ReconstructionTokens>());   // AVANT RefreshOrchestrator, comme le journal (arrêt en ordre inverse)
```
- `IndexMessages` n'a pas d'enregistrement DI aujourd'hui : 33-05 doit l'ajouter (le constructeur de `ReconstructionTokens` le prend explicitement, pas construit en interne, pour que les tests partagent le dossier).
- Rien n'est écrit tant que `StartAsync` n'a pas été appelé ; `ExecuterUnePasse` n'est appelée par personne d'autre que la boucle du thread (et les tests).
- `Changement` peut être levé ≈ 1 565 fois par passe complète (une fois par fichier) : le VM doit coalescer (Post + relecture au tick), pas re-rendre à chaque événement.

## Tests — RED nommés, GREEN, suite complète, durées

| Étape | Filtre | Résultat |
|---|---|---|
| Entrée (HEAD `af7f52a`) | suite complète | 1373 verts, 0 warning |
| Task 1 RED | `~ProjectionAgregatsTests` | 3 rouges par compilation (CS0103 `ProjectionAgregats`) |
| Task 1 GREEN | projection + `GardeTokensSansPourcentageTests` + `ServicesLayerPurityTests` | 10 / 10 |
| Task 2 RED | `~ReconstructionTokensTests` | 7 rouges par compilation (CS0246 `ReconstructionTokens`) |
| Task 2 GREEN | reconstruction + TOK-05 + pureté + dédup + doctrine + normalisation | 28 / 28 |
| Task 3 | `~ReconstructionTokensTests` (+6, 15 cas) | 15 / 15 dès le premier passage |
| **Fin de plan, run 1** | `dotnet test Chronos.sln -c Debug` | **1404 verts, 0 échec**, 11 s |
| **Fin de plan, run 2** | idem | **1404 verts, 0 échec**, 11 s |
| Build | `dotnet build Chronos.sln -c Debug` | **0 warning** |

Contribution de ce plan : **+16 tests** (3 + 13 `[Fact]`/`[Theory]`, soit 18 cas exécutés avec les 3 `InlineData`). 1404 = 1373 + 16 + 15 (33-04, même arbre).

**Durées mesurées par les tests** (`--logger console;verbosity=normal`, machine de développement, 33-04 en cours en parallèle) : passe complète sur la racine de test (6 fichiers, 13 ids) **24 ms** test compris ; ordre mtime + checkpoint 23 ms ; incrémental (3 passes + relecture depuis offset) 103 ms ; reprise k = 1 / 3 / 5 : 39 / 44 / 40 ms (deux passes de référence + deux passes de reprise chacune) ; panne après ids / après agrégats : 48 / 40 ms ; thread + `StartAsync` + `StopAsync` 20 ms ; **annulation sur 300 fichiers : 184 ms pour le test entier** (création des 300 copies incluse ; l'assertion `< 200 ms` ne mesure que `ExecuterUnePasse`, qui inclut l'initialisation, l'inventaire de 300 fichiers, 3 lectures et le dernier flush). La mesure sur la vraie machine (2,08 Go) reste celle de la recherche (2,4–2,6 s mur / 2,7 s CPU, semaine courante à 1,7 s), à reprendre en 33-05.

### Mutations (jouées dans l'arbre réel par copie, révoquées par copie, sha256 identique)

| Mutation | Rouges nommés | sha256 |
|---|---|---|
| (p1) `N = 1` au lieu de `g.Count()` dans `Projeter` | `Projeter_groupe_par_slot_modele_et_origine_et_compte_les_ids` (1/3) | `38114562c8a6b76e` |
| (r1) tri `OrderBy(f => f.Mtime)` (croissant) | `Les_fichiers_sont_lus_par_mtime_decroissant_et_la_semaine_courante_est_disponible_avant_l_historique` (1/7) | `e4d3d0a957f5b0f2` |
| (r2') `curseurs.Enregistrer(...)` retiré | `L_incremental_ne_rouvre_que_ce_qui_a_bouge…`, `Un_fichier_raccourci…`, `Un_fichier_disparu…`, `Une_passe_complete…` (4/7) | identique |
| (r2) « Raccourci traité comme Grandi » | **non jouable dans ce fichier** : la protection vit dans `Curseurs.Classer` (`taille < Offset` → `depuis = 0`, mutation k2 de 33-02) et dans `LecteurTranscript` (`depuis > Length` → 0) ; (r2') jouée à la place, comme prévu par le plan | — |
| (a) `thread.Start()` remplacé par `Task.Run(() => Boucle(ct))` (pool) | `Le_thread_de_fond_est_IsBackground_BelowNormal_nomme_et_StartAsync_rend_la_main_avant_la_fin` (priorité `Normal`, nom du pool) (1/15) | identique |
| (b) ordre de flush curseurs → ids → agrégats | `Une_panne_juste_apres_l_ecriture_des_ids…` (`etapeVue == "curseurs"`) ET `Une_panne_juste_apres_l_ecriture_des_agregats…` (2/15) | identique |
| (b') reprojection retirée (`RemplacerMois` non appelé) | `Une_panne_juste_apres_l_ecriture_des_ids…` seule (les ids sont connus → delta 0 → agrégats amputés après reprise ; les autres scénarios lisent le fichier écrit par la première instance et restent verts — c'est exactement ce que le plan prédisait) (1/15) | identique |
| (c) tri croissant | = (r1), déjà jouée en Task 2 | — |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug de conception] Les lignes sans aucun id cassaient l'invariant « reprise = passe ininterrompue » (D-33-18)**
- **Found during:** conception de la Task 2, en déroulant à la main le scénario k = 1 de la Task 3 sur la racine de test (s1 = multi-blocs, première par mtime, contient une ligne sans `message.id` ni `requestId`)
- **Issue:** avec D-33-11 au pied de la lettre (ligne sans id comptée telle quelle, jamais indexée), la première instance écrit ids + agrégats + curseur de s1 ; la seconde reprojette depuis l'index — qui ignore cette ligne — et ne rouvre pas s1 (inchangé) : la tranche 11:15Z perd (1, 1, 0, 0, N 1) → sha256 ≠ référence. Le plan exigeait à la fois D-33-11 et l'identité octet pour octet : contradictoires sur ce fichier.
- **Fix:** `ReconstructionTokens.Traiter` donne à toute ligne sans id une clé synthétique déterministe (`PrefixeSansId` + ticks UTC + modèle + origine + quatre compteurs) avant `IndexMessages.Ajouter` ; la brique 33-02 n'est pas touchée (sa règle « `Id null` → jamais indexé » reste vraie et testée). Effet de bord assumé : `IdsConnus == 13` sur la racine de test — le nombre même que le plan annonçait (il comptait 3 ids pour multi-blocs là où la fixture n'en porte que 2 + la ligne sans id).
- **Files modified:** `src/Chronos/Services/Historique/Tokens/ReconstructionTokens.cs`
- **Verification:** `Une_reprise_apres_annulation_donne_des_fichiers_identiques_octet_pour_octet` k = 1 / 3 / 5 verts ; sans la clé, k = 1 rougirait (déroulé à la main, non joué en mutation pour ne pas allonger la fenêtre).
- **Committed in:** `8c5b4a8`

**2. [Rule 1 - Bug] La reprojection systématique de tous les mois ouverts faisait naître des fichiers vides**
- **Found during:** Task 2, test `Une_racine_absente_donne_une_passe_vide_sans_lever` (attendu : `tokens-*.jsonl` absent)
- **Issue:** `MoisOuverts()` = [2026-08, 2026-09] ; `RemplacerMois(mois, projection vide)` salit les deux mois → `tokens-2026-08.jsonl` et `tokens-2026-09.jsonl` vides écrits par le flush final.
- **Fix:** reprojection seulement si le mois a ≥ 1 entrée dans l'index OU si son fichier existe déjà (alors remplacé, même vide : c'est l'état vrai).
- **Files modified:** `ReconstructionTokens.cs` — **Committed in:** `8c5b4a8`

**3. [Critères d'acceptation] Deux comptages à la lettre**
- `CouvertureTokens.NomFichier` apparaissait 2 fois (chemin calculé au démarrage et en fin de passe) → propriété privée `CheminCouverture`, 1 occurrence ; le libellé de brique `"curseurs"` dans `Prefixer(...)` faisait 4 lignes au `grep -n '"ids"\|"agregats"\|"curseurs"'` → libellé `"curseurs.json"`, 3 lignes dans l'ordre. Intégré à `8c5b4a8` avant commit.

### Écarts de forme assumés

- **Task 3 « RED 6 » n'a pas rougi** : les 6 tests étaient verts au premier passage (Task 2 fidèle). Commités tels quels en `test(33-03)` (`2bb6e83`) ; les mutations (a), (b), (b') tiennent lieu de preuve que ces tests discriminent.
- `IdsConnus == 13` : le plan disait « 3 + 1 + 3 + 5 + 1 ; la ligne sans id n'est pas indexée » — la fixture multi-blocs porte 2 ids + 1 ligne sans id ; avec D-33-18 le total est bien 13 (2 + 1 synthétique + 1 + 3 + 5 + 1).
- Le thread test attend `FichiersTraites == 1` pendant que le callback bloque (le plan ne le demandait pas : preuve supplémentaire que rien ne s'exécute inline).
- `SemaineCouranteDisponible` n'est posé qu'après un flush RÉUSSI (le plan le posait inconditionnellement après `Flush()`) : « disponible » signifie « sur le disque ».
- Aucune écriture hors dossiers temporaires ; `~/.claude/projects` réel jamais lu par la suite (la mesure machine reste celle de la recherche).

## Issues Encountered

- Exécution parallèle avec 33-04 sans incident : aucune fenêtre RED du voisin n'a cassé la compilation pendant mes runs (ses fichiers non suivis compilaient à chaque essai) ; l'instantané `snap-33-03` n'a pas été nécessaire. Jamais d'`--amend`, stage explicite, `--no-verify` partout.
- `.planning/REQUIREMENTS.md` portait le marquage TOK-04 non commité du voisin au début du plan ; au moment du commit final il était propre (33-04 a commité `daf4831`) : `requirements mark-complete TOK-02` puis stage direct, diff re-vérifié à l'instant (2 lignes, TOK-02 seul).
- Risque de fragilité connu : `L_annulation_entre_deux_fichiers…` (< 200 ms) et `Le_thread_de_fond…` (`StartAsync` < 50 ms) sont des tests de temps mural. Mesurés à 184 ms (test entier, création des 300 fichiers incluse) et 20 ms avec le voisin en parallèle ; sur une machine très chargée, le premier pourrait rougir sans bug. À surveiller ; si cela arrive, la marge à desserrer est celle du test, pas du service.

## Known Stubs

Aucun. Toutes les propriétés d'`IEtatReconstruction` sont alimentées par le service ; `FakeEtatReconstruction` est un faux de test (valeurs par défaut documentées), pas un stub de production. Aucune UI dans ce plan (câblage DI, VM et diagnostic en 33-05 ; bandeau F2 en 34).

## Deferred Issues

- **Limite D-33-18 à documenter au §8 (33-05)** : deux lignes sans id strictement identiques (même instant à la milliseconde, même modèle, même origine, mêmes quatre compteurs) sont indiscernables et comptent une fois ; le shard `ids-AAAA-MM.jsonl` contient désormais des clés `sans-id:…` à côté des `msg_…` / `req_…`.
- **Mois gelés et copies tardives** (déjà noté en 33-02, D-33-08) : un id vu la première fois dans un mois hors `MoisOuverts()` (> 45 j) n'est pas en mémoire ; sa copie fork relue aujourd'hui serait recomptée dans le mois gelé (chargé avant delta). Jamais observé (âge max des copies 3,6 j).
- `Changement` levé une fois par fichier (≈ 1 565 par passe complète) : le VM de 34 doit coalescer.

## Self-Check: PASSED

- 7 fichiers créés présents (`PhaseReconstruction.cs`, `IEtatReconstruction.cs`, `ProjectionAgregats.cs`, `ReconstructionTokens.cs`, `FakeEtatReconstruction.cs`, `ProjectionAgregatsTests.cs`, `ReconstructionTokensTests.cs`).
- Commits présents dans `git log` : `631f5c3`, `d507349`, `a19a02f`, `8c5b4a8`, `2bb6e83`.
- `git diff --stat af7f52a` VIDE sur `MagasinAgregats.cs`, `CouvertureTokens.cs`, `LecteurTranscript.cs`, `IndexMessages.cs`, `Curseurs.cs`, `App.xaml.cs`, `DiagnosticService.cs`, `TrancheTokens.cs`, `Couverture.cs`, `ROADMAP.md`, `STATE.md`.
