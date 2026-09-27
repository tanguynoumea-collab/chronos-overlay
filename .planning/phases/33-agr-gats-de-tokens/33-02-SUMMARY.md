---
phase: 33-agr-gats-de-tokens
plan: 02
subsystem: transcripts / agrégats de tokens — lecture au niveau octet, index d'ids (idempotence), curseurs
tags: [TOK-03, TOK-02, LecteurTranscript, IndexMessages, Curseurs, DedupUsage.Fusionner, offsets, prefiltre, ligne-future, shards-ids, fixtures-reelles, garde-CPT-01]

# Dependency graph
requires:
  - phase: 32-01 (CPT-01)
    provides: DedupUsage (LireUsage = seul lecteur des quatre compteurs de usage ; garde GardesDedupUsageTests), fixture réelle transcript-multi-blocs.jsonl (gabarit 2.1.281)
  - phase: 32-04 (JRN)
    provides: motif EcrireSousVerrou (OpenOrCreate + Seek(End) + FileShare.None + reprises), Purger / BilanRetention, LastExactStore.Save (temp + Move)
  - phase: 33 (recherche)
    provides: Pattern 1 (lecteur octet), Pattern 2 (index d'ids = mémoire d'idempotence), Pattern 7 (raccord à DedupUsage), sémantique des curseurs, mesures (1 377 ids multi-fichiers, 2 lignes futures à +3 s, 788–800 Mo/s)
provides:
  - Services/Historique/Tokens/LecteurTranscript.cs — MessageLu, ResultatLecture, LecteurTranscript.Lire (octet, offsets de dernière ligne complète, pré-filtre u8 non-autorité, autorité type + role, LireUsage seul, Sub par dossier subagents, ligne future bloquante ≤ 24 h, annulation contrôlée avant la 1re ligne puis toutes les 4096)
  - Services/Historique/Tokens/IndexMessages.cs — DeltaMessage, IndexMessages (HorizonIndex 45 j, RetentionIndexMois 3, Champs, NomFichier, EstNomShard, MoisOuverts, Charger, Ajouter → delta ou null, Entrees(mois), Flush en ajout exclusif, Purger)
  - Services/Historique/Tokens/Curseurs.cs — EtatFichier, CurseurFichier, Curseurs (Charger tolérant, CleRelative, Classer, Enregistrer, RetirerDisparus, Sauvegarder atomique)
  - Services/DedupUsage.cs — + Fusionner (max par champ, pur, statique) utilisé par Ajouter ET IndexMessages ; garde CPT-01 intacte
  - 14 fixtures réelles anonymisées sous tests/Chronos.Tests/TestData/transcripts/<cas>/ (10 cas)
affects: [33-03 (reconstruction de fond : consomme Lire / Ajouter / Flush / Classer / Enregistrer / Sauvegarder tels quels), 33-05 (diagnostic : compteurs ResultatLecture, IdsConnus, LignesIgnorees, DerniereErreur ; docs §8 : D-33-10, D-33-11, mois gelés), 34 (Entrees(mois) pour la projection)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Lecteur de lignes au niveau OCTET (FileStream ReadWrite|Delete, SequentialScan, tampon 64 Ko, découpe sur \\n, MemoryStream pour les fragments à cheval) : l'offset rendu est toujours juste après le dernier \\n d'une ligne TRAITÉE — jamais un StreamReader pour poser un curseur"
    - "Pré-filtre u8 (\"type\":\"assistant\") = économie ; autorité structurée type == assistant ET message.role == assistant ; le seul littéral du marqueur vit dans un ReadOnlySpan<byte> u8"
    - "Ligne future proche = ARRÊT devant la ligne (curseur non avancé, BloqueSurLigneFuture) ; au-delà de ToleranceFutur = ignorée et comptée — un curseur en retrait de la fin n'est jamais « inchangé »"
    - "Index d'ids en shards mensuels append-only (le max gagne à la relecture), delta = max − déjà compté ; la règle du max vit à UN endroit (DedupUsage.Fusionner) et la garde compte les Math.Max( du fichier"
    - "Mutations jouées par script (python) et révoquées PAR COPIE (sha256 identique) ; jamais git checkout -- fichier (autocrlf), jamais --amend dans un arbre partagé"

key-files:
  created:
    - src/Chronos/Services/Historique/Tokens/LecteurTranscript.cs
    - src/Chronos/Services/Historique/Tokens/IndexMessages.cs
    - src/Chronos/Services/Historique/Tokens/Curseurs.cs
    - tests/Chronos.Tests/LecteurTranscriptTests.cs
    - tests/Chronos.Tests/IndexMessagesTests.cs
    - tests/Chronos.Tests/CurseursTests.cs
    - tests/Chronos.Tests/TestData/transcripts/{multi-blocs,sous-agent,tronque,reduit,futur,fork-copie,a-cheval-tranche,prefiltre-piege,sans-message-id,synthetic}/ (14 .jsonl)
  modified:
    - src/Chronos/Services/DedupUsage.cs (+17 −6 : Fusionner, Ajouter refactoré, XML-doc complété)
    - tests/Chronos.Tests/DedupUsageTests.cs (+1 test, insertion seule)
    - .planning/REQUIREMENTS.md (TOK-03 → Complete)

key-decisions:
  - "D-33-07 — l'index d'ids est la mémoire d'idempotence, les agrégats en sont la projection : shards ids-AAAA-MM.jsonl (mois UTC du premier timestamp) en ajout seul, le max par champ gagne à la relecture"
  - "D-33-08 — bornes nommées : HorizonIndex = 45 j (mois ouverts = ceux qui chevauchent [now − 45 j, now]), RetentionIndexMois = 3 (au-delà, mois d'agrégats gelé)"
  - "D-33-09 — ligne future ≤ ToleranceFutur (24 h) : la lecture s'arrête devant elle, le curseur reste avant, BloqueSurLigneFuture = true ; au-delà : ignorée et comptée (LignesFuturesIgnorees)"
  - "D-33-10 — une ligne assistant à quatre compteurs nuls n'est ni comptée ni indexée (LignesSansTokens)"
  - "D-33-11 — une ligne sans message.id ni requestId est comptée telle quelle et jamais indexée (Id null → delta = ses compteurs, NouveauMessage true)"
  - "D-33-12 — sub = le dossier parent immédiat s'appelle subagents (ordinal-insensible) ; LecteurTranscript.EstSousAgent recopie la convention sans référencer TranscriptSessionSource"
  - "Charger() REMPLACE l'état mémoire (Clear puis relecture) : un second appel n'additionne pas ; Entrees(mois) rend une copie sous verrou"
  - "Flush() sans ligne en attente ne touche pas le disque (ni dossier ni fichier) ; en cas d'échec d'un shard, seules ses lignes restent en attente"

patterns-established:
  - "Script de fixtures par plan dans le scratchpad (fixtures-33-02.py) : dérivation de la ligne 1 du gabarit réel par substitution de champs nommés, LF sans BOM, grep négatif (Packages / Claude_ / Users / nom d'utilisateur) avant commit"
  - "Runner de mutation (jouer-mutation.sh + mutations-33-02.py) : sauvegarde par copie, mutation nommée, filtre, restauration par copie, sha256 avant / après — réutilisable pour 33-03/04/05"

requirements-completed: [TOK-03]

# Metrics
duration: 22min
completed: 2026-09-27
---

# Phase 33 Plan 02: Lecteur octet, index d'ids, curseurs Summary

**N'importe quel transcript se lit depuis n'importe quel octet (offset = fin de la dernière ligne complète traitée, fragment et ligne du futur proche attendent leur tour), un message compte UNE fois quel que soit le nombre de fichiers où il vit et l'ordre de lecture (index d'ids persistant, delta = max − déjà compté, la règle du max n'existe qu'à un endroit : `DedupUsage.Fusionner`), et `curseurs.json` sait quel fichier rouvrir et d'où — sans qu'un curseur en retrait de la fin ne se croie jamais « inchangé ».**

## Performance

- **Duration:** ≈ 22 min (10:29:34Z → 10:51Z), dont ≈ 2 min d'attente sur le RED du plan parallèle 33-01
- **Started:** 2026-09-27T10:29:34Z — SHA d'entrée `fd3dac9`, 1313 verts au relevé de planification
- **Completed:** 2026-09-27T10:51Z
- **Tasks:** 3 / 3
- **Files:** 6 créés (3 sources, 3 tests) + 14 fixtures + 2 modifiés (`DedupUsage.cs`, `DedupUsageTests.cs`)

## Accomplishments

- **`LecteurTranscript.Lire(chemin, depuis, now, traiter, ct)`** : `FileStream(Open, Read, ReadWrite | Delete, bufferSize 1, SequentialScan)`, tampon de 64 Ko, découpe sur `\n`, `MemoryStream` pour les fragments à cheval, `\r` final retiré ; le fragment final sans `\n` n'est pas traité ; `depuis > Length` → repris de 0. Pré-filtre `"type":"assistant"u8` (le seul littéral du fichier) puis autorité `type == assistant && message.role == assistant` ; `timestamp` par `UsageNormalization.InstantDepuisIso` ; compteurs par `DedupUsage.LireUsage` (seul appel) ; `Sub` par `EstSousAgent` (dossier parent `subagents`, ordinal-insensible) ; quatre zéros → `LignesSansTokens` ; ligne future ≤ 24 h → arrêt devant elle sans avancer l'offset ; > 24 h → `LignesFuturesIgnorees`. Annulation contrôlée avant la première ligne puis toutes les 4096 : `Annulee`, offset de la dernière ligne traitée. `IOException` / `UnauthorizedAccessException` à l'ouverture → résultat vide à `depuis`, jamais d'exception.
- **`DedupUsage.Fusionner`** : `(long In, long Out, long CacheW, long CacheR) Fusionner(connu, lu)`, quatre `Math.Max(` sur quatre lignes ; `Ajouter` l'appelle (pas de surcharge, `Entrees()` et `LireUsage` intacts) ; XML-doc de classe nomme `IndexMessages`.
- **`IndexMessages(dossier, IClock)`** : `Dictionary<string, Entree>` (Ts = premier vu, Model / Sub de la première vue, max × 4) ; `Ajouter(MessageLu)` → `DeltaMessage` (id inconnu : compteurs + `NouveauMessage`) / delta `max − connu` avec `Ts = connu.Ts` / `null` si rien de neuf ; `Id null` → jamais indexé. Shards `ids-AAAA-MM.jsonl` : DTO `{v,id,ts,model,sub,in,out,cache_w,cache_r}` (ordre = `Champs`), `ts` en « O » UTC, UTF-8 sans BOM, `\n` seul, écriture en un `Write` par shard sous `OpenOrCreate + Seek(End) + FileShare.None` avec reprises (Yield × 12 puis Sleep(1), 600 essais), `Directory.CreateDirectory` hors boucle. `Charger()` relit les mois de `MoisOuverts()` (`[now − 45 j, now]`), tolérant (`v` lu en premier, `id` non vide, `ts` par `InstantDepuisIso`, compteurs `TryGetInt64`, `sub` absent → false), `Fusionner` si l'id est déjà là (le `Ts` le plus petit reste). `Purger()` : `ids-*.jsonl` d'index de mois `< courant − 3` supprimés, tout autre nom `Ignores`.
- **`Curseurs(chemin, racine)`** : clés `Path.GetRelativePath(Racine, …).Replace('\\','/')`, dictionnaire `OrdinalIgnoreCase` ; `Classer` : absent → `Nouveau` (0) ; `taille < c.Offset` → `Raccourci` (0) ; `taille == c.Taille && mtime == c.Mtime && c.Offset == c.Taille` → `Inchange` ; sinon `Grandi` (`c.Offset`). `Sauvegarder` : `{ "v":1, "fichiers": { clé: { offset, taille, mtime « O » } } }` compact, tmp `Chemin + ".tmp-" + pid` puis `File.Move(overwrite: true)`, tmp nettoyé sur échec, `DerniereErreur`. `Charger` : absent / `JsonException` / `v ≠ 1` → vide ; entrée sans `offset`, `taille` ou `mtime` lisible → sautée.
- **14 fixtures réelles anonymisées** (10 dossiers) dérivées de la ligne 1 du gabarit 2.1.281 par substitution de `message.model`, `message.id`, `requestId`, les quatre compteurs, `timestamp`, `isSidechain`, `sessionId`, `uuid`, `parentUuid` uniquement ; `multi-blocs/session-a.jsonl` = copie octet pour octet (`cmp` silencieux, 6 014 o) ; `tronque/session-c.jsonl` finit sur `,` (pas de `\n`) ; grep négatif `Packages|Claude_|Users|<nom>` vide ; le piège porte exactement 2 occurrences du marqueur.

## API livrée pour 33-03 (signatures réelles)

```csharp
namespace Chronos.Services.Historique.Tokens;

public sealed record MessageLu(string? Id, DateTimeOffset Ts, string Model, bool Sub, long In, long Out, long CacheW, long CacheR);
public sealed record ResultatLecture(long OffsetDerniereLigneComplete, int LignesLues, int LignesPrefiltrees, int LignesAssistant, int LignesIgnorees,
                                     int LignesSansTokens, int LignesFuturesIgnorees, bool BloqueSurLigneFuture, DateTimeOffset? PlusAncienTs, DateTimeOffset? PlusRecentTs, bool Annulee);
public static class LecteurTranscript {
    public const int TailleTampon = 65536; public const int LignesEntreControles = 4096; public static readonly TimeSpan ToleranceFutur = TimeSpan.FromHours(24);
    public static bool EstSousAgent(string chemin);
    public static ResultatLecture Lire(string chemin, long depuis, DateTimeOffset now, Action<MessageLu> traiter, CancellationToken ct); }

public sealed record DeltaMessage(DateTimeOffset Ts, string Model, bool Sub, long In, long Out, long CacheW, long CacheR, bool NouveauMessage);
public sealed class IndexMessages {
    public const int SchemaVersion = 1; public static readonly TimeSpan HorizonIndex = TimeSpan.FromDays(45); public const int RetentionIndexMois = 3;
    public static readonly string[] Champs;   // v, id, ts, model, sub, in, out, cache_w, cache_r
    public IndexMessages(string dossier, IClock clock);
    public string Dossier { get; } public int IdsConnus { get; } public int LignesIgnorees { get; } public int LignesEnAttente { get; } public string? DerniereErreur { get; }
    public static string NomFichier(DateTimeOffset ts); public static bool EstNomShard(string nomFichier, out DateTimeOffset mois);
    public IReadOnlyList<DateTimeOffset> MoisOuverts(); public int Charger(); public DeltaMessage? Ajouter(MessageLu m);
    public IEnumerable<(DateTimeOffset Ts, string Model, bool Sub, long In, long Out, long CacheW, long CacheR)> Entrees(DateTimeOffset mois);
    public bool Flush(); public BilanRetention Purger(); }

public enum EtatFichier { Nouveau, Inchange, Grandi, Raccourci }
public sealed record CurseurFichier(long Offset, long Taille, DateTimeOffset Mtime);
public sealed class Curseurs {
    public const int SchemaVersion = 1; public const string NomFichier = "curseurs.json";
    public Curseurs(string chemin, string racine); public string Chemin { get; } public string Racine { get; } public int Count { get; } public string? DerniereErreur { get; }
    public static Curseurs Charger(string chemin, string racine); public string CleRelative(string cheminAbsolu);
    public EtatFichier Classer(string cheminAbsolu, long taille, DateTimeOffset mtime, out long depuis);
    public void Enregistrer(string cheminAbsolu, long offset, long taille, DateTimeOffset mtime);
    public int RetirerDisparus(IReadOnlySet<string> clesPresentes); public bool Sauvegarder(); }

// Chronos.Services.DedupUsage
public static (long In, long Out, long CacheW, long CacheR) Fusionner((long In, long Out, long CacheW, long CacheR) connu, (long In, long Out, long CacheW, long CacheR) lu);
```

Ordre d'écriture attendu en 33-03 (Pattern 2) : `IndexMessages.Flush()` (shards, ajout) → agrégats (Move) → `Curseurs.Sauvegarder()` (Move). `Curseurs.Classer` puis `LecteurTranscript.Lire(chemin, depuis, now, m => { var d = index.Ajouter(m); if (d is not null) magasin.Appliquer(d); }, ct)` puis `Curseurs.Enregistrer(chemin, r.OffsetDerniereLigneComplete, taille, mtime)` — sauf si `r.Annulee` (ne pas enregistrer).

## Fixtures (`tests/Chronos.Tests/TestData/transcripts/<cas>/`) et attendus prouvés

| Cas | Fichier(s) | Attendu (prouvé par test) |
|---|---|---|
| multi-blocs | `session-a.jsonl` (= gabarit, 6 014 o) | 7 `MessageLu`, offset 6014, `msg_01EXEMPLEMULTIBLOCS0000000001` (2, 8→256, 35005, 41741), `req_01EXEMPLESANSMESSAGEID00000002`, L7 `Id null`, bornes 11:20:00Z / 11:28:00Z |
| sous-agent | `session-b.jsonl` ; `session-b/subagents/agent-x.jsonl` | `Sub false` (`msg_01EXEMPLEPRINCIPAL0000000001`) ; 3 × `Sub true`, `claude-sonnet-5`, (10,100,1000,5000) (20,200,0,6000) (30,300,2000,7000) |
| tronque | `session-c.jsonl` (2 lignes + 120 o de TRONQUE03, sans `\n`) ; `suite.jsonl` | 2 messages, offset = fin de L2, `LignesIgnorees 0` ; après ajout de `suite` : exactement TRONQUE03 (3,30,300,3000) et TRONQUE04, offset = taille |
| reduit | `long.jsonl` (4) ; `court.jsonl` (2 premières) | 4 / 2 messages ; taille de `court` < offset de `long` (cas « raccourci » de 33-03) |
| futur | `session-d.jsonl` : 10:00:00, 10:00:03, +3 j | `now` 10:00:01 → 1 message, bloqué, offset = fin de L1 ; `now` 10:00:10 → 2 messages, `LignesFuturesIgnorees 1`, offset = taille |
| fork-copie | `session-a.jsonl` (FORK01-03) ; `session-b.jsonl` (copies 01-02 + FORK04-05) | 3 + 4 lus ; index : 5 deltas non nuls, 2 null, même multiset A→B et B→A, `IdsConnus 5` |
| a-cheval-tranche | `session-e.jsonl` (CHEVAL01 à 10:59:58 / 11:00:07) | 2 `MessageLu` de même `Id` (slot du premier timestamp en 33-03) |
| prefiltre-piege | `session-f.jsonl` (user avec le marqueur, summary, PIEGE01) | `LignesLues 3`, `LignesPrefiltrees 2`, `LignesAssistant 1`, 1 message, `LignesIgnorees 0` |
| sans-message-id | `session-g.jsonl` (req × 2, aucun id × 1) | `Id == "req_01EXEMPLESANSID0000000001"` × 2, `null` × 1 ; index : (1,10,100,1000 nouveau), (0,30,0,0), (2,2,2,2 nouveau), `IdsConnus 1` |
| synthetic | `session-h.jsonl` (`<synthetic>`, 0,0,0,0) | 0 message, `LignesAssistant 1`, `LignesSansTokens 1`, `LignesIgnorees 0` |

Ids générés : `msg_01EXEMPLE<CAS>` complété de zéros à 32 caractères (`msg_01EXEMPLETRONQUE000000000001`, `msg_01EXEMPLEFORK000000000000001`, `msg_01EXEMPLEPIEGE00000000000001`, `msg_01EXEMPLEFUTUR00000000000001`, `msg_01EXEMPLECHEVAL0000000000001`, `msg_01EXEMPLEREDUIT0000000000001`, `msg_01EXEMPLESYNTH00000000000001`). Le plan annonçait « 13 fichiers dont suite.jsonl » : sa propre liste `files_modified` en compte **14**, c'est ce nombre qui est livré.

## Task Commits

Chaque tâche a été commitée atomiquement (`--no-verify`, fichiers stagés un par un) :

1. **Task 1 RED** — `03fd0c9` test(33-02): fixtures reelles anonymisees (12 cas) et lecteur de transcript au niveau octet, RED 9
2. **Task 1 GREEN** — `e37fd7b` feat(33-02): LecteurTranscript - lignes completes au niveau octet, offsets, prefiltre non-autorite, sub par dossier, ligne future bloquante (TOK-03) ; `d12f4ec` docs(33-02): reformulation d'un commentaire (critère `grep -c StreamReader = 0`)
3. **Task 2 RED** — `f61ca80` test(33-02): DedupUsage.Fusionner ; IndexMessages - delta par id, copies fork dans les deux ordres, shards relus (le max gagne), horizon 45 j, retention 3 mois, RED 11
4. **Task 2 GREEN** — `fd24685` feat(33-02): DedupUsage.Fusionner (regle du max partagee) ; IndexMessages - memoire d'idempotence, shards ids-AAAA-MM en ajout seul, horizon 45 j, retention 3 mois (TOK-03)
5. **Task 3 RED** — `c52c4de` test(33-02): curseurs - cles relatives, nouveau/inchange/grandi/raccourci/disparu, curseur bloque jamais inchange, atomique et tolerant, RED 8
6. **Task 3 GREEN** — `e4585f4` feat(33-02): Curseurs - curseurs.json atomique, cles relatives, classification nouveau/inchange/grandi/raccourci, disparus retires (TOK-03)

**Plan metadata:** `fd86db1` docs(33-02): complete … (SUMMARY + REQUIREMENTS.md) ; `1578f4f` docs(33-02): REQUIREMENTS.md — ne porter que TOK-03 (réparation, voir incidents) ; commit final de cette note.

## Tests — RED nommés, GREEN, suite complète

| Étape | Filtre | Résultat |
|---|---|---|
| Entrée (HEAD `fd3dac9`) | suite complète | 1313 verts |
| Task 1 RED | `~LecteurTranscriptTests` | 9 rouges par compilation (CS0234 / CS0246 : `LecteurTranscript`, `ResultatLecture`, `MessageLu`) |
| Task 1 GREEN | `~LecteurTranscriptTests\|~GardesDedupUsageTests` | 12 / 12 |
| Task 2 RED | `~IndexMessagesTests\|~DedupUsageTests` | 11 rouges par compilation (CS0246 `IndexMessages`, `Fusionner` absent) |
| Task 2 GREEN | filtre du plan (index, dedup, gardes, héritage, provider) | 36 / 36 |
| Task 3 RED | `~CurseursTests` | 8 rouges par compilation (CS0246 `Curseurs`) |
| Task 3 GREEN | `~CurseursTests` | 8 / 8 (après une attente de 45 s sur le RED `CouvertureTokensTests` de 33-01) |
| Gardes | dédup, doctrine, normalisation, lecture seule, pureté, TOK-05 (33-01) | 27 / 27, aucune exemption ajoutée |
| **Fin de plan, run 1** | `dotnet test Chronos.sln -c Debug` | **1373 verts, 0 échec**, 11 s |
| **Fin de plan, run 2** | idem | **1373 verts, 0 échec**, 11 s |
| Build | `dotnet build Chronos.sln -c Debug` | **0 warning** |

Contribution de ce plan : **+28 tests** (9 + 1 + 10 + 8 ; le plan estimait ≈ 30). 1373 = 1313 + 28 + 32 de 33-01 (fusionné dans le même arbre).

### Mutations (jouées par script, révoquées par copie, sha256 identique)

| Mutation | Fichier | Rouges observés | sha256 |
|---|---|---|---|
| (t1) lecteur octet → `StreamReader.ReadLine` + position du flux | LecteurTranscript.cs | `Une_ligne_tronquee_en_fin_de_fichier…`, `Reprendre_depuis_l_offset…`, **et** `Une_ligne_future_de_quelques_secondes…` (la position du flux n'est plus une fin de ligne) | `346debe9bd986951` |
| (t2) ligne future ≤ 24 h : ignorée et curseur avancé | LecteurTranscript.cs | `Une_ligne_future_de_quelques_secondes_bloque_le_curseur_devant_elle` | identique |
| (t3) autorité retirée (le pré-filtre décide) | LecteurTranscript.cs | `Le_prefiltre_est_une_economie_pas_l_autorite` | identique |
| (i1) `Fusionner` → « le lu gagne » | DedupUsage.cs | `Fusionner_rend_le_max…`, `Le_max_est_independant_de_l_ordre`, `Flush_ecrit_une_ligne_par_delta…`, `Un_bloc_partiel_qui_grandit…`, et la garde `Le_helper_de_dedup_existe…` (< 4 `Math.Max(`) | `a29ccf321ffe1dcf` |
| (i2) `Ajouter` ne consulte plus `_parId` | IndexMessages.cs | `Une_copie_fork_resume_rend_null…`, `Un_bloc_partiel_qui_grandit…`, `Flush_ecrit…`, `Charger_ne_lit_que…`, `Le_repli_requestId…` | `9a0cd72a6c92f1e5` |
| (i3) `MoisOuverts()` = mois courant seul | IndexMessages.cs | `Charger_ne_lit_que_les_mois_ouverts_de_l_horizon`, `Flush_ecrit…`, `Une_ligne_de_shard_corrompue…` | identique |
| (k1) `Inchange` sans `c.Offset == c.Taille` | Curseurs.cs | `Un_curseur_bloque_avant_la_fin_du_fichier_n_est_jamais_inchange`, `Sauvegarder_puis_Charger…` | `12961d80b180d9f5` |
| (k2) `Raccourci` → `depuis = c.Offset` | Curseurs.cs | `Un_fichier_raccourci_se_relit_de_zero` | identique |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Critère d'acceptation] Commentaires reformulés pour les comptages par ligne**
- **Found during:** Task 1 et Task 2 (relecture contre les critères `grep -c`)
- **Issue:** `grep -c "StreamReader"` = 1 (cité dans le XML-doc), `grep -c "FileShare.None"` = 2 et `grep -c "UsageNormalization.InstantDepuisIso"` = 2 dans `IndexMessages.cs` (une occurrence en commentaire chacune), `grep -c "Math.Max("` = 1 (quatre occurrences sur une seule ligne : la garde compte les occurrences et était verte, le critère du plan compte les lignes)
- **Fix:** commentaires reformulés sans les jetons ; `Fusionner` écrit sur quatre lignes
- **Files modified:** LecteurTranscript.cs, IndexMessages.cs, DedupUsage.cs
- **Commit:** `d12f4ec` (LecteurTranscript) ; intégré à `fd24685` (les deux autres, avant commit)

**2. [Rule 1 - Bug de mutation] (k1) et (i1) réécrites**
- (k1) en `//` avalait le corps de l'`if` sur la même ligne (CS0177) → commentaire bloc ; (i1) adaptée au `Fusionner` sur quatre lignes ; (i2) formulée `TryGetValue(...) && false` (définitivement assigné). Aucun impact sur les sources.

### Incidents d'exécution parallèle (réparés, à ne pas reproduire)

1. Un `git commit --amend` destiné à ma retouche de commentaire a réécrit le commit `758af72` de 33-01 arrivé entre mon contrôle de HEAD et l'amend (HEAD avait bougé). Réparé immédiatement par `git reset --soft 758af72` (commit du voisin intact, vérifié par `git show --stat`) puis commit séparé `d12f4ec`. **Règle retenue : jamais `--amend` dans un arbre partagé — toujours un commit de plus.**
2. Le commit `fd86db1` (SUMMARY + REQUIREMENTS.md) a emporté les lignes TOK-01 que 33-01 venait de marquer dans l'arbre entre mon `requirements mark-complete TOK-03` (diff 2/2 vérifié) et mon `git add` (diff devenu 4/4). Réparé par `1578f4f` : blob = version HEAD~1 + mes seules lignes TOK-03, commité par `git hash-object -w` + `git update-index --cacheinfo` **sans toucher l'arbre de travail** — les lignes TOK-01 y restent en diff non commité, pour le commit final de 33-01. **Règle retenue : `git add` de REQUIREMENTS.md seulement après avoir re-vérifié `git diff --stat` à l'instant du commit, sinon blob depuis HEAD.**

### Écarts de forme

- « 13 fichiers » du critère 1 → **14** (la liste `files_modified` du plan en compte 14 ; `suite.jsonl` inclus).
- `sous-agent/session-b.jsonl` et `synthetic/session-h.jsonl` : `file` les classe « JSON text data » (une seule ligne) et non « NDJSON » — LF sans BOM vérifié par `xxd`.

## Known Stubs

Aucun. Les trois types sont complets et purs (aucune E/S hors dossier injecté, aucune horloge système, aucun thread) ; c'est 33-03 qui les orchestre.

## Deferred Issues

- **autocrlf et fixtures binaires-sensibles** : comme `transcript-multi-blocs.jsonl` (32-01), les 14 fixtures sont LF dans l'index et la copie de travail, sans `.gitattributes` ; un clone frais avec `core.autocrlf=true` les convertirait en CRLF et décalerait les offsets (`6014`). Le lecteur tolère `\r` mais l'assertion `6014` et `cmp` rougiraient. Hors périmètre de ce plan (`.gitattributes` n'est pas dans `files_modified`) — à traiter par un `*.jsonl text eol=lf` (ou `-text`) dans un plan de docs/outillage.
- `IndexMessages.Charger()` ne borne pas la mémoire au-delà de `MoisOuverts()` : ≈ 87 k ids / 30 j mesurés (11–15 Mo) ; à surveiller au diagnostic (33-05).

## Self-Check: PASSED

- Fichiers créés présents : `LecteurTranscript.cs`, `IndexMessages.cs`, `Curseurs.cs`, les 3 fichiers de tests, les 14 fixtures (vérifiés par `ls` / `find` = 14).
- Commits présents dans `git log` : `03fd0c9`, `e37fd7b`, `d12f4ec`, `f61ca80`, `fd24685`, `c52c4de`, `e4585f4`.
- `git diff fd3dac9 -- NormalisationUniqueTests.cs TranscriptActivityProvider.cs TranscriptSessionSource.cs App.xaml.cs docs/` vide ; ROADMAP.md et STATE.md non modifiés par ce plan.
