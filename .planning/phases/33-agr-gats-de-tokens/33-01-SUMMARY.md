---
phase: 33-agr-gats-de-tokens
plan: 01
subsystem: persistance / agrégats de tokens (schéma de ligne, magasin mensuel atomique, couverture persistée, garde TOK-05)
tags: [tokens, agregats, jsonl, TrancheTokens, DeltaTranche, LigneAgregat, MagasinAgregats, CouvertureTokens, IEtatMagasin, File.Move, System.Text.Json, garde-structurelle, TOK-05, sous-namespaces]

# Dependency graph
requires:
  - phase: 32 (CPT-02, 32-02)
    provides: IEtatMagasin + NomsMagasins.AgregatsTokens, règle D-32-05 (DerniereEcriture = mtime après le Move), motif LastExactStore.Save (temp + Move)
  - phase: 32 (JRN-01/03, 32-04)
    provides: dialecte JSONL maison (DTO privé [JsonPropertyName]/[JsonPropertyOrder], instants UTC « O », encodeur relâché, v lu en premier, lecture tolérante), JournalReleves.RetentionMois, BilanRetention, motif Purger, UsageNormalization.InstantDepuisIso (point unique HDR-05)
  - phase: 33 (recherche)
    provides: mesures (3 886 tuples = 524 Ko ; juillet non vide : 23 211 lignes dans des fichiers d'août/septembre), Pattern 2 (agrégats = projection), Pattern 4 (écriture atomique), Pattern 6 (couverture par intervalles), garde TOK-05, HYP-4 (cleanupPeriodDays = 30 j)
provides:
  - Models/Historique/Tokens/TrancheTokens.cs — record TrancheTokens(Slot, Model, Sub, In, Out, CacheW, CacheR, N) + Tranche (15 min) + SlotDe(t) / MoisDe(t) (UTC, offset zéro) ; record DeltaTranche(Slot, Model, Sub, In, Out, CacheW, CacheR, NouveauMessage) + EstNul
  - Models/Historique/Tokens/Couverture.cs — enum EtatCouverture { HorsCouverture, TranscriptsAbsents, Couverte } ; record IntervalleGaranti(Debut, Fin) = [Debut, Fin[
  - Services/Historique/Tokens/LigneAgregat.cs — Serialiser (9 champs {v, slot, model, sub, in, out, cache_w, cache_r, n}, slot « O » UTC, entiers invariants) ; Parser tolérant (v en premier, slot aligné 15 min sinon refusé, compteur absent/non entier/négatif refusé, model absent refusé, sub absent = false) ; Champs[9] ; SchemaVersion = 1 ; Perimetre (D-33-06)
  - Services/Historique/Tokens/MagasinAgregats.cs : IEtatMagasin — état mémoire par mois UTC ; Appliquer(DeltaTranche) (mois pas en mémoire chargé d'abord) ; RemplacerMois ; ChargerMois tolérant ; TranchesDuMois (copie triée) ; EcrireMoisSales (temp + File.Move par mois sale, tri slot/model ordinal/sub, UTF-8 sans BOM, LF) ; Purger (RetentionMois = JournalReleves.RetentionMois) ; NomFichier / CheminDuMois / EstNomMensuel ; MoisEcrits, LignesIgnorees, TranchesEnMemoire, MoisSales
  - Services/Historique/Tokens/CouvertureTokens.cs — HorizonPurge = 30 j (HYP-4), NomFichier = couverture.json, PlusAncienneLigneVue monotone (VoirLigne), GarantirPasse [debut − HorizonPurge, fin[ fusionné, Classer (3 états, fin exclue), Charger tolérant, Sauvegarder atomique, DerniereErreur
  - Garde TOK-05 (GardeTokensSansPourcentageTests) — réflexive (aucun double/float/decimal dans aucune signature des namespaces *.Historique.Tokens, y compris Nullable/tableau/générique/byref ; aucun membre nommé Utilization|Utilisation|Pourcent|Quota|Ratio|Fraction|Pct) + textuelle (Services/Historique/Tokens/**, Models/Historique/Tokens/**) + contrôle positif sur DeltaConsommation.Delta
  - ServicesLayerPurityTests voit Chronos.Services.Historique.Tokens et Chronos.Models.Historique.Tokens (insertion seule, 19 lignes, 0 suppression)
  - Fixture tests/Chronos.Tests/TestData/tokens/tolerance/tokens-2026-09.jsonl (8 lignes : 2 valides, 5 refusées, 1 vide ; 145 o + LF pour la ligne 1)
affects: [33-03 (reconstruction : Appliquer / RemplacerMois / EcrireMoisSales / VoirLigne / GarantirPasse / Sauvegarder), 33-04 (lecture par plage : LigneAgregat.Parser, TranchesDuMois ou lecture directe des fichiers, CouvertureTokens.Charger + Classer), 33-05 (DI : MagasinAgregats comme troisième IEtatMagasin ; diagnostic : Perimetre, LignesIgnorees, MoisEcrits ; docs §8 : Champs, Perimetre, HYP-4), 34 (vues Tokens Claude Code), 35 (constat)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Projection mensuelle réécrite entière et atomiquement (temp unique par processus + File.Move overwrite) depuis un état mémoire, ordre de tri déterministe → deux écritures du même état = mêmes octets (assertion d'idempotence réutilisable)"
    - "Mois gelé chargé AVANT tout delta : un mois pas encore en mémoire est relu depuis son fichier avant d'être touché — jamais écrasé par une tranche isolée"
    - "Lecture tolérante STRICTE pour un agrégat : un champ manquant REFUSE la ligne (une somme sans compteur n'existe pas), à la différence du journal des relevés où un champ absent vaut null"
    - "Couverture = état persisté et daté (intervalles garantis fusionnés + plus ancienne ligne vue), jamais déduite des mtimes ; trois états, jamais un zéro implicite"
    - "Garde structurelle par réflexion + garde textuelle sur un sous-arbre + contrôle positif hors périmètre (prouve que le filtre mord et que le périmètre est le bon)"
    - "Preuve d'atomicité par lecteur concurrent en FileShare.ReadWrite | Delete (le partage du lecteur tolérant) : sur cette machine, File.Move(overwrite) échoue sous un tel lecteur, une écriture directe réussit — c'est ce qui discrimine la mutation Move → écriture directe ; FileShare.None ne discrimine pas"

key-files:
  created:
    - src/Chronos/Models/Historique/Tokens/TrancheTokens.cs
    - src/Chronos/Models/Historique/Tokens/Couverture.cs
    - src/Chronos/Services/Historique/Tokens/LigneAgregat.cs
    - src/Chronos/Services/Historique/Tokens/MagasinAgregats.cs
    - src/Chronos/Services/Historique/Tokens/CouvertureTokens.cs
    - tests/Chronos.Tests/LigneAgregatTests.cs
    - tests/Chronos.Tests/MagasinAgregatsTests.cs
    - tests/Chronos.Tests/CouvertureTokensTests.cs
    - tests/Chronos.Tests/GardeTokensSansPourcentageTests.cs
    - tests/Chronos.Tests/TestData/tokens/tolerance/tokens-2026-09.jsonl
  modified:
    - tests/Chronos.Tests/ServicesLayerPurityTests.cs
    - .planning/REQUIREMENTS.md

key-decisions:
  - "D-33-01 — sous-dossiers Models/Historique/Tokens et Services/Historique/Tokens, namespaces Chronos.Models.Historique.Tokens / Chronos.Services.Historique.Tokens : la garde TOK-05 est une phrase (« aucun type de *.Historique.Tokens n'expose un flottant ») ; la garde de pureté voit le nouveau quartier (test d'insertion)"
  - "D-33-02 — slot au format « O » UTC (2026-09-01T00:00:00.0000000+00:00), comme t du journal : un seul dialecte d'instant dans historique\\ ; mesuré 145 o + LF par ligne pleine (recherche : ≈ 148)"
  - "D-33-03 — un agrégat sans compteur n'a pas de sens : in/out/cache_w/cache_r/n absents, non entiers ou négatifs → ligne REFUSÉE et comptée ; model absent → refusée ; slot non aligné sur 15 min → refusée ; sub absent → false ; sub présent mais non booléen → refusée (une valeur inattendue n'est pas « false »)"
  - "D-33-04 — ordre d'écriture déterministe (slot, model ordinal, sub false avant true) : octets identiques pour le même état, prouvé par sha256 dans le test et rougi par la mutation m1"
  - "D-33-05 — couverture par intervalles garantis [debutPasse − HorizonPurge, finPasse[, HorizonPurge = 30 j = cleanupPeriodDays par défaut de Claude Code (HYP-4, settings.json non lu en v1.8) ; borne de fin EXCLUE"
  - "D-33-06 — LigneAgregat.Perimetre = « Claude Code seulement — hors Cowork et claude.ai ; bruts, non pondérés » : constante du code, à comparer mot pour mot au §8 par la garde documentaire de 33-05"
  - "Exécution : EcrireMoisSales rend false (DerniereErreur, temp effacé, mois toujours sale) si un lecteur tient le fichier — même en ReadWrite | Delete : c'est le prix de « jamais un fichier partiel » ; le lot suivant rattrape (consigné dans la XML-doc du magasin)"
  - "Exécution : TranchesDuMois ne charge PAS un mois absent de la mémoire (rend vide) — la lecture ne mute pas l'état ; 33-04 lit les fichiers via LigneAgregat.Parser ou appelle ChargerMois d'abord"
  - "Exécution : RemplacerMois normalise les slots (SlotDe) et ignore une tranche d'un autre mois ; CouvertureTokens.Sauvegarder crée le dossier parent ; Charger avale toute exception (motif LastExactStore.LireBrut) et saute un intervalle mal formé (fin < début) en gardant les autres"

patterns-established:
  - "Boucle TDD sous parallélisme sans worktree : instantané `git archive fd3dac9 src tests Chronos.sln` + script de synchronisation des seuls fichiers du plan (snap-33-01) ; mutations jouées sur la COPIE de l'instantané, révocation par re-copie, sha256 comparé au fichier de l'arbre réel (jamais touché) ; suite complète finale sur l'arbre réel une fois le voisin GREEN"
  - "Marquage d'une exigence quand REQUIREMENTS.md porte des lignes non commitées d'un autre plan : blob construit depuis HEAD + les seules lignes de ce plan (git hash-object / update-index --cacheinfo), arbre de travail laissé avec les deux marquages"

requirements-completed: [TOK-01]

# Metrics
duration: 24min
completed: 2026-09-27
---

# Phase 33 Plan 01 : Schéma, magasin et couverture des agrégats de tokens — Summary

**Ligne d'agrégat à neuf champs entiers (`{v, slot, model, sub, in, out, cache_w, cache_r, n}`, slot 15 min UTC au format « O », parsing tolérant strict), magasin mensuel `tokens-AAAA-MM.jsonl` réécrit entier et atomiquement (temp + Move, tri déterministe → octets identiques, mois gelé chargé avant delta, `IEtatMagasin`, rétention du journal), couverture persistée par intervalles garantis (`HorizonPurge` 30 j, trois états, juillet 2026 = « transcripts absents »), et garde TOK-05 réflexive + textuelle qui rougit sur tout flottant dans le quartier `*.Historique.Tokens`.**

## Performance

- **Duration:** 24 min
- **Started:** 2026-09-27T10:31:22Z
- **Completed:** 2026-09-27T10:55:00Z
- **Tasks:** 3 (TDD : 3 RED + 3 GREEN + 1 renforcement + 1 docs)
- **Files modified:** 12 (10 créés, 2 modifiés)
- **SHA d'entrée :** `fd3dac9` (suite : 1313 verts, 0 warning)

## Accomplishments

- **TOK-01 tenue côté code** : une tranche de tokens a un nom (`TrancheTokens`), un fichier (`tokens-AAAA-MM.jsonl`), une écriture sûre (`EcrireMoisSales`) et une lecture qui saute une ligne cassée en la comptant (`LigneAgregat.Parser`, `LignesIgnorees`) ; quatre compteurs séparés, jamais leur somme, jamais un message, jamais du texte (test `Le_fichier_ne_porte_ni_somme_ni_message_ni_texte` : exactement les 9 clés de `Champs`).
- **Idempotence assertable pour 33-03** : `Deux_ecritures_du_meme_etat_donnent_des_octets_identiques` (sha256) ; `RemplacerMois` réécrit, n'ajoute pas ; un mois pas en mémoire est chargé avant tout delta (`Appliquer_sur_un_mois_pas_encore_en_memoire_charge_d_abord_son_fichier`).
- **Fondation TOK-04** : `CouvertureTokens` — juin avant le 23 = hors couverture, juillet = transcripts absents, septembre = couverte ; persisté dans `couverture.json`, jamais déduit des mtimes.
- **Fondation TOK-05** : ≥ 6 types vus par la garde réflexive (7 publics + DTO privés), contrôle positif sur `DeltaConsommation.Delta`, garde textuelle sur 5 fichiers, pureté élargie au quartier Tokens — mutation `public double Part => 0;` → deux gardes indépendantes rouges.
- Suite complète sur l'arbre réel : **1373 verts, deux fois de suite, 0 warning** (1313 + 32 de ce plan + 28 déjà commités par 33-02 à cet instant).

## Task Commits

1. **Task 1 : TrancheTokens, DeltaTranche, LigneAgregat** — RED `773465c` (test, 9) → GREEN `758af72` (feat) → `ea9b664` (docs : référence XML au point unique reformulée pour le critère « un seul `UsageNormalization.InstantDepuisIso` »)
2. **Task 2 : MagasinAgregats** — RED `90fdb08` (test, 12) → GREEN `44fa064` (feat) → `f4e570f` (test : preuve d'atomicité par lecteur concurrent, mutation m2 ; fait mesuré consigné dans la XML-doc)
3. **Task 3 : CouvertureTokens + garde TOK-05 + pureté** — RED `3f92166` (test, 6 + 3 + 1) → GREEN `133f92c` (feat)

**Plan metadata:** commit final `docs(33-01)` (SUMMARY + REQUIREMENTS.md, blob TOK-01 seul).

## API réellement livrée (pour 33-03 / 33-04 / 33-05)

```csharp
namespace Chronos.Models.Historique.Tokens;
public sealed record TrancheTokens(DateTimeOffset Slot, string Model, bool Sub, long In, long Out, long CacheW, long CacheR, int N)
{
    public static readonly TimeSpan Tranche;                       // 15 min
    public static DateTimeOffset SlotDe(DateTimeOffset t);          // début de tranche 15 min, UTC, offset zéro
    public static DateTimeOffset MoisDe(DateTimeOffset t);          // 1er du mois UTC 00:00, offset zéro
}
public sealed record DeltaTranche(DateTimeOffset Slot, string Model, bool Sub, long In, long Out, long CacheW, long CacheR, bool NouveauMessage)
{ public bool EstNul { get; } }                                     // 0 partout et pas de nouveau message
public enum EtatCouverture { HorsCouverture, TranscriptsAbsents, Couverte }
public sealed record IntervalleGaranti(DateTimeOffset Debut, DateTimeOffset Fin);   // [Debut, Fin[

namespace Chronos.Services.Historique.Tokens;
public static class LigneAgregat
{
    public const int SchemaVersion = 1;
    public static readonly string[] Champs;                         // { v, slot, model, sub, in, out, cache_w, cache_r, n }
    public const string Perimetre;                                  // « Claude Code seulement — hors Cowork et claude.ai ; bruts, non pondérés »
    public static string Serialiser(TrancheTokens t);               // sans « \n » final
    public static bool Parser(string ligne, out TrancheTokens? tranche);   // false = ligne à IGNORER ET COMPTER ; ne lève jamais
}
public sealed class MagasinAgregats : IEtatMagasin
{
    public MagasinAgregats(string dossier, IClock clock);
    public static readonly int RetentionMois;                       // = JournalReleves.RetentionMois (24)
    public string Dossier { get; }  public string Nom { get; }  public string Chemin { get; }       // Nom = NomsMagasins.AgregatsTokens ; Chemin = Dossier
    public DateTimeOffset? DerniereEcriture { get; }  public string? DerniereErreur { get; }       // mtime après le Move ; « Type : message »
    public int MoisEcrits { get; }  public int LignesIgnorees { get; }  public int TranchesEnMemoire { get; }
    public IReadOnlyCollection<DateTimeOffset> MoisSales { get; }   // copie triée
    public static string NomFichier(DateTimeOffset slotOuMois);     // tokens-AAAA-MM.jsonl (mois UTC)
    public string CheminDuMois(DateTimeOffset slotOuMois);
    public static bool EstNomMensuel(string nomFichier, out DateTimeOffset mois);
    public int ChargerMois(DateTimeOffset mois);                    // tolérant ; remplace l'état du mois SANS le salir ; absent → 0
    public void RemplacerMois(DateTimeOffset mois, IEnumerable<TrancheTokens> tranches);   // reprojection : remplace ET salit
    public void Appliquer(DeltaTranche d);                          // EstNul → rien ; mois pas en mémoire → ChargerMois d'abord ; salit
    public IReadOnlyList<TrancheTokens> TranchesDuMois(DateTimeOffset mois);   // copie triée ; VIDE si le mois n'est pas en mémoire (ne charge pas)
    public bool EcrireMoisSales();                                  // temp + Move par mois sale ; false = DerniereErreur, mois reste sale
    public BilanRetention Purger();                                 // (supprimés, échecs, ignorés)
}
public sealed class CouvertureTokens
{
    public const int SchemaVersion = 1;  public const string NomFichier = "couverture.json";
    public static readonly TimeSpan HorizonPurge;                   // 30 j (HYP-4)
    public DateTimeOffset? PlusAncienneLigneVue { get; }  public IReadOnlyList<IntervalleGaranti> Intervalles { get; }  public string? DerniereErreur { get; }
    public CouvertureTokens();
    public static CouvertureTokens Charger(string chemin);          // absent / corrompu / v ≠ 1 → vide ; ne lève jamais
    public void VoirLigne(DateTimeOffset ts);                       // min monotone
    public void GarantirPasse(DateTimeOffset debutPasse, DateTimeOffset finPasse);   // ajoute [debutPasse − 30 j, finPasse[ et fusionne
    public EtatCouverture Classer(DateTimeOffset slot);             // HorsCouverture / Couverte (début inclus, fin exclue) / TranscriptsAbsents
    public bool Sauvegarder(string chemin);                         // temp + Move ; crée le dossier parent ; false + DerniereErreur
}
```

**À savoir pour les consommateurs :**
- `Appliquer` normalise le slot par `SlotDe` ; la clé d'une tranche est `(Model, Sub, Slot)`.
- `EcrireMoisSales` **échoue proprement si un lecteur tient le fichier** (même en `FileShare.ReadWrite | Delete`, mesuré) : `false`, `DerniereErreur`, aucun temp, octets intacts, mois toujours sale → rappeler au lot suivant. Le lecteur de 33-04 doit tenir les fichiers le moins longtemps possible.
- `ChargerMois` en échec d'E/S pose `DerniereErreur` et laisse le mois « connu » mais vide ; un `EcrireMoisSales` ultérieur réécrirait ce mois avec les seules tranches appliquées depuis. Cas pratiquement inatteignable (un verrou qui bloque notre lecture bloque aussi le `Move`), à garder en tête en 33-03 : vérifier `DerniereErreur` après le chargement des mois gelés si l'on veut être ceinture et bretelles.
- Le fichier d'agrégats d'un mois **ouvert** n'est jamais la vérité : 33-03 reprojette par `RemplacerMois` depuis l'index d'ids au démarrage.
- `couverture.json` : `{"v":1,"plus_ancienne_ligne_vue":"…O…","intervalles":[{"debut":"…","fin":"…"}]}`, compact, instants « O » UTC.

## Fixture de tolérance (`TestData/tokens/tolerance/tokens-2026-09.jsonl`)

8 lignes physiques, LF, sans BOM, `\n` final, 837 octets ; ligne 1 = 145 o + LF = **146 o** (la recherche estimait ≈ 148 avec le format « O »). Attendu et vérifié : 2 tranches (lignes 1 et 7), 5 refusées (v = 2, slot 00:07, `out` absent, tronquée, `model` absent), ligne vide ni lue ni comptée. Réutilisée par `ChargerMois_relit_un_mois_gele_avec_tolerance` (`LignesIgnorees == 5`).

## Mutations (jouées dans l'instantané, révoquées par copie, sha256 identiques au fichier de l'arbre réel)

| Mutation | Rouges nommés |
|---|---|
| (l1) `Parser` sans la vérification `slot != SlotDe(slot)` | `Une_ligne_avec_v_inconnu_slot_non_aligne_ou_compteur_absent_est_refusee` (1/9) |
| (l2) `[JsonPropertyName("in")]` → `"input"` | `Une_tranche_se_serialise_avec_les_neuf_champs_du_contrat_dans_l_ordre`, `Serialiser_puis_Parser_rend_la_meme_tranche` (+ `Aucune_somme…`, `Les_entiers_partent_invariants…`) (4/9) |
| (m1) tri retiré dans `EcrireMoisSales` | `EcrireMoisSales_reecrit_le_mois_entier_trie_et_atomiquement`, `Deux_ecritures_du_meme_etat_donnent_des_octets_identiques` (2/12) |
| (m2) `File.Move` → écriture directe | **restée verte** sur les 12 tests initiaux (le mtime du fichier cible est le même dans les deux formes) → **test renforcé** `Un_lecteur_concurrent_fait_echouer_l_ecriture_proprement_sans_fichier_partiel` → rouge (1/13). **Forme retenue** : lecteur ouvert en `FileShare.ReadWrite \| Delete` (celui de `ChargerMois`), PAS `FileShare.None` — mesure du 2026-09-27 (.NET 8, Windows 11, projet console jetable) : face à `None`, `Move` ET écriture directe échouent (pas de discrimination) ; face à `ReadWrite \| Delete`, `Move(overwrite)` échoue (`UnauthorizedAccessException`, ancien contenu intact, temp survivant → effacé par le magasin) alors que l'écriture directe réussit et fait lire le nouveau contenu au lecteur déjà ouvert |
| (c1) `GarantirPasse` sans `− HorizonPurge` | `GarantirPasse_ajoute_l_intervalle_recule…`, `Classer_distingue_les_trois_etats…` (+ `Sauvegarder_puis_Charger…`) (3/6) |
| (c2) `public double Part => 0;` dans `TrancheTokens` | `Aucun_type_du_namespace_Tokens_n_expose_un_flottant…` ET `Aucun_fichier_de_Tokens_ne_contient_de_flottant…` (deux gardes indépendantes) |
| (c3) `EstTypeNeutre` remis à plat (`n is "Chronos.Services" or "Chronos.Models"`) | `La_garde_de_purete_voit_le_sous_namespace_Tokens` ET `La_garde_de_purete_voit_les_sous_namespaces_Historique` (32-04) |

## Totaux

- Entrée : 1313 verts (HEAD `fd3dac9`), 0 warning.
- Fin de plan, arbre réel, `dotnet test Chronos.sln -c Debug --nologo -v q` : **1373 / 1373** — exécution 1 et 2 (avant le commit docs), puis **1373 / 1373** — exécutions 3 et 4 (après). `dotnet build` : **0 warning**.
- Ce plan : **32 tests** (9 LigneAgregat + 13 MagasinAgregats + 6 CouvertureTokens + 3 garde TOK-05 + 1 pureté) ; 33-02 en avait commité 28 à cet instant.

## Files Created/Modified

- `src/Chronos/Models/Historique/Tokens/TrancheTokens.cs` — `TrancheTokens` (+ `Tranche`, `SlotDe`, `MoisDe`) et `DeltaTranche` (+ `EstNul`) ; XML-doc : entiers seulement, quatre compteurs séparés, périmètre partiel.
- `src/Chronos/Models/Historique/Tokens/Couverture.cs` — `EtatCouverture` (mots du §4 en XML-doc), `IntervalleGaranti`.
- `src/Chronos/Services/Historique/Tokens/LigneAgregat.cs` — DTO privé `LigneAgregatDto` (`Entree`/`Sortie` pour `in`/`out`), `Serialiser`, `Parser`, `Champs`, `Perimetre`, `SchemaVersion` ; POURQUOI « O » et POURQUOI un agrégat sans compteur est refusé.
- `src/Chronos/Services/Historique/Tokens/MagasinAgregats.cs` — état par mois, verrou unique, `EcrireMoisSales` temp + Move, `Purger` (motif `JournalReleves`), `IEtatMagasin` ; fait mesuré « Move sous lecteur » consigné.
- `src/Chronos/Services/Historique/Tokens/CouvertureTokens.cs` — intervalles fusionnés, `couverture.json`, HYP-4.
- `tests/Chronos.Tests/LigneAgregatTests.cs` (9), `MagasinAgregatsTests.cs` (13), `CouvertureTokensTests.cs` (6), `GardeTokensSansPourcentageTests.cs` (3), `ServicesLayerPurityTests.cs` (+1, insertion seule).
- `tests/Chronos.Tests/TestData/tokens/tolerance/tokens-2026-09.jsonl` — fixture de tolérance.
- `.planning/REQUIREMENTS.md` — TOK-01 coché + traçabilité « Complete » (blob depuis HEAD, sans les lignes TOK-03 du voisin).

## Decisions Made

Voir `key-decisions` : D-33-01 à D-33-06 telles qu'écrites au plan, plus les décisions d'exécution (échec propre sous lecteur, `TranchesDuMois` ne charge pas, `sub` non booléen refusé, `RemplacerMois` normalise et filtre par mois, `Sauvegarder` crée le dossier, `Charger` avale tout et saute les intervalles mal formés).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug de test] Mutation (m2) restée verte → test renforcé (contingence prévue par le plan)**
- **Found during:** Task 2 (mutation m2)
- **Issue:** remplacer le `Move` par une écriture directe ne change ni le mtime lu ni les octets écrits : les 12 tests initiaux restaient verts.
- **Fix:** test `Un_lecteur_concurrent_fait_echouer_l_ecriture_proprement_sans_fichier_partiel` (lecteur en `FileShare.ReadWrite | Delete`) après mesure empirique ; note dans la XML-doc du magasin.
- **Files modified:** tests/Chronos.Tests/MagasinAgregatsTests.cs, src/Chronos/Services/Historique/Tokens/MagasinAgregats.cs
- **Verification:** m2 rejouée → rouge nommé ; sha256 identique après révocation.
- **Committed in:** `f4e570f`

**2. [Rule 3 - Blocage] Compilation du projet de test cassée par le RED de 33-02 (`LecteurTranscriptTests` : `ResultatLecture`, `MessageLu` introuvables)**
- **Found during:** Task 1 (premier `dotnet test`)
- **Fix:** boucle TDD dans l'instantané `snap-33-01` (`git archive fd3dac9 src tests Chronos.sln` + script `sync-33-01.sh` copiant les seuls fichiers du plan) ; la suite complète finale a été jouée sur l'arbre réel une fois 33-02 GREEN (build 0 erreur / 0 warning).
- **Files modified:** aucun fichier du dépôt.

**3. [Critère d'acceptation] `UsageNormalization.InstantDepuisIso` apparaissait 2 fois dans `LigneAgregat.cs` (un `<see cref>` + l'appel) là où le plan compte 1**
- **Fix:** référence XML reformulée (`<c>InstantDepuisIso</c>` de `<see cref="UsageNormalization"/>`) ; suite rejouée deux fois (1373 / 1373), 0 warning.
- **Committed in:** `ea9b664`

---

**Total deviations:** 3 (1 renforcement de test prévu au plan, 1 blocage d'environnement, 1 conformité littérale de critère)
**Impact on plan:** aucun changement de périmètre ; aucun fichier hors `files_modified` touché (vérifié : `git log --grep='(33-01)' -- DedupUsage.cs App.xaml.cs DiagnosticService.cs docs/ ROADMAP.md STATE.md` vide).

**Écarts de comptage assumés :** le plan disait « +12 » tests Magasin dans `<behavior>` et « = 11 » `[Fact]` dans les critères — il y en a **13** (12 du comportement + le renforcé). `HYP-4` apparaît 2 fois dans `CouvertureTokens.cs` (critère ≥ 1). `DeltaConsommation` 4 fois dans la garde (critère ≥ 1).

## Issues Encountered

- `git config core.autocrlf = true` : tous les fichiers écrits en LF ; les avertissements « LF will be replaced by CRLF » sont attendus ; les révocations de mutation se font par copie (jamais `git checkout --`), sha256 comparés — 7/7 identiques.
- Windows PowerShell 5.1 ne connaît pas `File.Move(a, b, overwrite)` (`MethodException`) et `pwsh` est absent : l'expérience Move-vs-lecteur a été faite avec un projet console .NET 8 jetable dans le scratchpad (`exp-move/`), hors dépôt.
- `.planning/REQUIREMENTS.md` portait dans l'arbre de travail le marquage TOK-03 non commité de 33-02 : le commit final embarque un blob construit depuis HEAD + les seules lignes TOK-01 ; l'arbre de travail garde les deux marquages pour que le voisin ne perde rien.

## Known Stubs

Aucun. Aucune valeur vide codée en dur ne remonte vers une UI (il n'y a pas d'UI dans cette phase) ; `Array.Empty<TrancheTokens>()` rendu par `TranchesDuMois` pour un mois hors mémoire est un comportement documenté et testé, pas un stub.

## Next Phase Readiness

- **33-03 (reconstruction)** peut consommer tel quel : `Appliquer(DeltaTranche)`, `RemplacerMois`, `EcrireMoisSales` (à rappeler si `false`), `CouvertureTokens.VoirLigne / GarantirPasse / Sauvegarder`, `MagasinAgregats.Purger`.
- **33-04 (lecture par plage)** : `LigneAgregat.Parser` + `MagasinAgregats.EstNomMensuel` pour lire les fichiers, `CouvertureTokens.Charger` + `Classer` par tranche ; `TrancheTokens.SlotDe` pour aligner les bornes.
- **33-05 (câblage, diagnostic, docs §8)** : `MagasinAgregats` est un `IEtatMagasin` complet (`NomsMagasins.AgregatsTokens`) ; `LigneAgregat.Champs` et `Perimetre` sont exposés pour la garde documentaire ; `HYP-4` est nommé dans `CouvertureTokens`.
- Rien ne bloque ; aucune écriture hors dossiers temporaires pendant ce plan.

---
*Phase: 33-agr-gats-de-tokens*
*Completed: 2026-09-27*

## Self-Check: PASSED

10 fichiers présents (5 sources, 4 tests, 1 fixture) + SUMMARY ; 8 commits de tâche retrouvés dans `git log` (773465c, 758af72, ea9b664, 90fdb08, 44fa064, f4e570f, 3f92166, 133f92c).
