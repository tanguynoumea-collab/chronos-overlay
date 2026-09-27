---
phase: 33-agr-gats-de-tokens
verified: 2026-09-27T00:00:00Z
status: passed
score: 5/5 must-haves verified
---

# Phase 33: Agrégats de tokens Verification Report

**Phase Goal:** La seconde série — les tokens de Claude Code, sur leur propre axe — existe : chaque transcript (principal
et `subagents/`) est agrégé par tranche de 15 min UTC × modèle × principal/sous-agent en quatre compteurs séparés,
reconstruit en arrière-plan depuis les 2,2 Go existants sans jamais bloquer l'UI, puis tenu à jour par curseurs ; les
tranches se rendent en heure locale juste au changement d'heure, et aucun type ne peut en faire un pourcentage.
**Verified:** 2026-09-27
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth (Success Criterion, ROADMAP.md Phase 33) | Status | Evidence |
|---|---|---|---|
| 1 | TOK-01 — Une ligne par (tranche, modèle, origine), quatre compteurs séparés, jamais leur somme, écriture atomique, lecture tolérante | ✓ VERIFIED | `LigneAgregat.Champs` = 9 champs exacts (`v,slot,model,sub,in,out,cache_w,cache_r,n`) ; `MagasinAgregats.EcrireMoisSales` réécrit par mois (temp + `File.Move`), triée ; pas de flottant/somme dans les types. Tests `LigneAgregatTests`, `MagasinAgregatsTests` verts. |
| 2 | TOK-02 — Reconstruction de fond sur thread `IsBackground`/`BelowNormal`, mtime décroissant, semaine courante disponible avant l'historique, progression exposée au ViewModel, jamais de blocage UI | ✓ VERIFIED | `ReconstructionTokens.cs` l.137 : `IsBackground = true, Priority = ThreadPriority.BelowNormal` ; l.224 `Thread.Yield()` (jamais `await Task.Yield()`) ; `IEtatReconstruction` exposé ; `MainViewModel.TexteReconstruction`/`AfficherReconstruction` mis à jour au tick (`MajTexteReconstruction`). Câblé en DI `AddHostedService` avant `RefreshOrchestrator` (App.xaml.cs l.453 < l.483). |
| 3 | TOK-03 — Curseurs `{offset, taille, mtime}`, fichier inchangé non rouvert, raccourci/renommé réingéré, reprise idempotente | ✓ VERIFIED | `Curseurs.Classer` (`Nouveau/Inchange/Grandi/Raccourci`) ; `ReconstructionTokensTests` prouve l'idempotence octet-pour-octet par sha256 après annulation à k=1,3,5 et pannes simulées entre étapes du flush. |
| 4 | TOK-04 — Rendu local correct aux changements d'heure (25 h / 23 h), couverture « hors couverture / transcripts absents / couverte », jamais « zéro » | ✓ VERIFIED | `RenduLocalTokensTests` : 25 barres le 25/10/2026 (deux « 02:00 »), 23 le 28/03/2027 (100 colonnes = 25 h × 4). `CouvertureTokens.Classer` rend les trois états ; aucun état "zéro" implicite. |
| 5 | TOK-05 — Aucun pourcentage dérivé de tokens, garde structurelle | ✓ VERIFIED | Aucun `double/float/decimal` dans `Services/Historique/Tokens/**` ni `Models/Historique/Tokens/**` (grep confirmé) ; `GardeTokensSansPourcentageTests` (réflexive + textuelle + contrôle positif) et `ServicesLayerPurityTests` verts. |

**Score:** 5/5 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|---|---|---|---|
| `src/Chronos/Models/Historique/Tokens/TrancheTokens.cs` | `TrancheTokens`, `DeltaTranche`, `SlotDe` | ✓ VERIFIED | Présent, conforme à l'API documentée dans 33-01-SUMMARY. |
| `src/Chronos/Models/Historique/Tokens/Couverture.cs` | `EtatCouverture`, `IntervalleGaranti` | ✓ VERIFIED | Présent. |
| `src/Chronos/Services/Historique/Tokens/LigneAgregat.cs` | Serialiser/Parser tolérant, `Champs[9]`, `Perimetre` | ✓ VERIFIED | Présent ; `Perimetre` cité mot pour mot au diagnostic et à `docs/data-sources.md` §8. |
| `src/Chronos/Services/Historique/Tokens/MagasinAgregats.cs` | `IEtatMagasin`, écriture atomique par mois | ✓ VERIFIED | Présent, `class MagasinAgregats : IEtatMagasin`. |
| `src/Chronos/Services/Historique/Tokens/CouvertureTokens.cs` | intervalles garantis, `HorizonPurge` 30j | ✓ VERIFIED | Présent. |
| `src/Chronos/Services/Historique/Tokens/LecteurTranscript.cs` | lecture octet, offsets, pré-filtre | ✓ VERIFIED | Présent, `EstSousAgent`, `Lire(...)`. |
| `src/Chronos/Services/Historique/Tokens/IndexMessages.cs` | shards `ids-AAAA-MM.jsonl`, delta par id | ✓ VERIFIED | Présent. |
| `src/Chronos/Services/Historique/Tokens/Curseurs.cs` | `curseurs.json` atomique | ✓ VERIFIED | Présent. |
| `src/Chronos/Services/Historique/Tokens/ReconstructionTokens.cs` | `BackgroundService` + thread dédié | ✓ VERIFIED | Présent, `Priority = ThreadPriority.BelowNormal`. |
| `src/Chronos/Services/Historique/Tokens/IEtatReconstruction.cs` | contrat neutre de progression | ✓ VERIFIED | Présent. |
| `src/Chronos/Models/Historique/Tokens/PhaseReconstruction.cs` | enum 5 valeurs | ✓ VERIFIED | Présent. |
| `src/Chronos/Services/Historique/Tokens/ProjectionAgregats.cs` | `Projeter(...)` pur | ✓ VERIFIED | Présent. |
| `src/Chronos/Models/Historique/Tokens/LectureAgregats.cs` | types de lecture par plage, entiers seulement | ✓ VERIFIED | Présent. |
| `src/Chronos/Services/Historique/Tokens/LecteurAgregats.cs` | `Lire(dossier, de, a)` | ✓ VERIFIED | Présent, WIRED via `LigneAgregat.Parser`. |
| `src/Chronos/Services/Historique/Tokens/RenduLocalTokens.cs` | `ParHeure` 25/23/24 barres | ✓ VERIFIED | Présent. |
| `src/Chronos/App.xaml.cs` | DI des agrégats, hosted service AVANT `RefreshOrchestrator` | ✓ VERIFIED / WIRED | l.448-453 enregistrements ; `AddHostedService(ReconstructionTokens)` l.453 < `AddHostedService(RefreshOrchestrator)` l.483. |
| `src/Chronos/Services/DiagnosticService.cs` | 3e magasin + section `[Agrégats de tokens]` + `Perimetre` | ✓ VERIFIED / WIRED | paramètre optionnel `IEtatReconstruction? reconstruction`, `LigneAgregat.Perimetre` cité. |
| `src/Chronos/ViewModels/MainViewModel.cs` | `TexteReconstruction`/`AfficherReconstruction` | ✓ VERIFIED / WIRED | `MajTexteReconstruction()` appelée au tick. |
| `docs/data-sources.md` §8 | ≥ 40 lignes, périmètre, HYP-4, renvoi du §7 | ✓ VERIFIED | §8 = 127 lignes (l.446-573), §7 renvoie explicitement au §8 (l.440-441). |
| `tests/Chronos.Tests/ContratAgregatsDocumenteTests.cs` | garde documentaire à deux sens | ✓ VERIFIED | Présent, 5 tests verts. |

### Key Link Verification

| From | To | Via | Status | Details |
|---|---|---|---|---|
| `LecteurAgregats.Lire` | `LigneAgregat.Parser` | lecture tolérante ligne par ligne | ✓ WIRED | Confirmé dans le code et par `LecteurAgregatsTests`. |
| `App.xaml.cs` (hosted service `ReconstructionTokens`) | `RefreshOrchestrator` (hosted service) | ordre d'inscription DI | ✓ WIRED | `services.AddHostedService(sp => sp.GetRequiredService<ReconstructionTokens>())` (l.453) précède `services.AddHostedService(sp => sp.GetRequiredService<RefreshOrchestrator>())` (l.483). |
| `DiagnosticService` | `IEtatReconstruction` + `MagasinAgregats` | injection DI optionnelle | ✓ WIRED | Paramètre optionnel résolu par le conteneur, testé par `CompositionRootTests`/`DiagnosticServiceTests`. |
| `MainViewModel` | `IEtatReconstruction` | injection DI optionnelle (même mécanisme que `IEtatJournal` en 32-05) | ✓ WIRED | Preuve dans le conteneur miroir `Host_resout_et_dispose_les_singletons` (33-05-SUMMARY) ; `AddSingleton<MainViewModel>()` sans fabrique explicite mais paramètre optionnel résolu par DI standard — comportement identique et déjà en production pour `IEtatJournal`. |
| `ReconstructionTokens` | `DedupUsage.Fusionner` (via `IndexMessages`/`LecteurTranscript`) | seul point d'entrée de somme des `usage` | ✓ WIRED | `GardesDedupUsageTests.Aucun_fichier_de_Services_ou_Models_ne_lit_message_usage_hors_du_helper` vert ; ≥ 4 `Math.Max(` dans `DedupUsage.cs`. |

### Data-Flow Trace (Level 4)

Non applicable au sens UI (aucune vue ne consomme ces agrégats avant la Phase 34) ; le contrôle porte donc sur la chaîne
service → fichier → lecture :

| Artifact | Data Variable | Source | Produces Real Data | Status |
|---|---|---|---|---|
| `MagasinAgregats.tokens-AAAA-MM.jsonl` | `TrancheTokens` (via `DeltaTranche`) | `ReconstructionTokens.ExecuterUnePasse` → `LecteurTranscript.Lire` sur les vrais transcripts (`ProjectsRoot`) | Oui — mesuré sur la vraie machine (1611 fichiers, 118 641 ids, 3 853 tuples d'agrégats, 570 214 o) par le SUMMARY 33-05, harnais hors arbre, aucune écriture sous `%APPDATA%\Chronos` par l'agent | ✓ FLOWING |
| `IEtatReconstruction` (VM/diagnostic) | `Phase, FichiersTraites, FichiersTotal, ...` | champs `Volatile.Write`/`Volatile.Read` mis à jour par le thread dédié pendant `ExecuterUnePasse` | Oui — entiers réels, pas de valeur statique (démontré par `FakeEtatReconstruction` réglable + tests d'intégration `ReconstructionTokensTests`) | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|---|---|---|---|
| Suite complète xUnit (1415 tests attendus) | `dotnet test Chronos.sln -c Debug --nologo -v q` | `Réussi! - échec : 0, réussite : 1415, ignorée(s) : 0, total : 1415, durée : 10 s` | ✓ PASS |
| Build sans warning | `dotnet build Chronos.sln -c Debug --nologo -v q` | `0 Avertissement(s), 0 Erreur(s)` | ✓ PASS |
| Ordre des hosted services agrégats/orchestrateur dans `App.xaml.cs` | `grep -n "AddHostedService" src/Chronos/App.xaml.cs` | `ReconstructionTokens` l.453 < `RefreshOrchestrator` l.483 | ✓ PASS |
| Aucun flottant dans le quartier Tokens | `grep -n "double\|float\|decimal" src/Chronos/{Services,Models}/Historique/Tokens/*.cs` | Aucune occurrence hors commentaires XML | ✓ PASS |
| Aucun type WPF sous Services/Models Tokens | `grep -rn "System.Windows" src/Chronos/{Services,Models}/Historique/Tokens/*.cs` | Aucune occurrence (seules des mentions en commentaire « aucun WPF ») | ✓ PASS |
| `.gitattributes` force LF sur les fixtures | `cat .gitattributes` | `*.jsonl text eol=lf` + `tests/Chronos.Tests/TestData/** text eol=lf` | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|---|---|---|---|---|
| TOK-01 | 33-01-PLAN.md | Agrégation 15 min UTC × modèle × origine, 4 compteurs, jamais la somme | ✓ SATISFIED | `LigneAgregat`, `MagasinAgregats`, tests verts, garde TOK-05 co-livrée. |
| TOK-02 | 33-03-PLAN.md | Reconstruction de fond non bloquante, thread `BelowNormal`, progression exposée | ✓ SATISFIED | `ReconstructionTokens` + `IEtatReconstruction` + câblage DI + `MainViewModel` (33-05). Mesures consignées (§8, SUMMARY 33-05). |
| TOK-03 | 33-02-PLAN.md | Incrémental par curseurs, reprise idempotente | ✓ SATISFIED | `Curseurs`, `IndexMessages`, idempotence octet-pour-octet prouvée dans `ReconstructionTokensTests` (33-03). |
| TOK-04 | 33-04-PLAN.md | Rendu local correct au changement d'heure, couverture jamais « zéro » | ✓ SATISFIED | `RenduLocalTokens`, `LecteurAgregats`, `CouvertureTokens` ; tests DST nommés et verts. |
| TOK-05 | 33-05-PLAN.md (garde initiale en 33-01) | Aucun pourcentage dérivé de tokens, garde structurelle | ✓ SATISFIED | `GardeTokensSansPourcentageTests`, `ServicesLayerPurityTests`, absence de flottant confirmée par grep direct. |

Aucun ID orphelin : les 5 IDs déclarés dans les frontmatters des plans (TOK-01..05) correspondent exactement aux 5 lignes
« Phase 33 | Complete » de `REQUIREMENTS.md` (l.178-182).

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|---|---|---|---|---|
| `src/Chronos/App.xaml.cs` | l.448-453 | Aucune garde textuelle propre sur l'ORDRE réel des deux `AddHostedService` (agrégats vs `RefreshOrchestrator`) dans le fichier de production — seule une mutation sur le conteneur MIROIR (`CompositionRootTests`) prouve le principe ; une régression d'ordre dans le vrai `App.xaml.cs` seul ne ferait rougir aucun test | ℹ️ Info | Dette mineure, explicitement documentée et assumée par le SUMMARY 33-05 (« Deferred Issues »). L'ordre RÉEL est actuellement correct (vérifié par grep direct dans cette vérification). Ne bloque pas le goal de la phase 33 ; à combler par une insertion dans `GardesPerimetreTests` (hors périmètre du plan 33-05). |

Aucun TODO/FIXME/placeholder trouvé dans le quartier `Historique/Tokens`. Aucune valeur vide codée en dur remontant vers une UI (pas d'UI dans cette phase).

### Human Verification Required

Aucune. Cette phase ne produit aucune interface utilisateur (les pistes Tokens et le bandeau F2 arrivent en Phase 34) ; tout
est vérifiable par tests automatisés et par grep direct sur le code réel, ce qui a été fait ci-dessus. Le constat en
production des quatre fichiers sous `%APPDATA%\Chronos\historique\` (présence réelle après premier lancement, coût CPU
observé par l'utilisateur) est explicitement différé au constat de la Phase 35 (VAL-05), conformément à `33-VALIDATION.md`
(« Manual-Only Verifications : — · All phase behaviors have automated verification »).

### Gaps Summary

Aucun gap bloquant. Les 5 vérités observables (TOK-01 à TOK-05) sont vérifiées avec preuve directe dans le code (pas
seulement via les SUMMARY) : schéma de ligne conforme, écriture atomique, reconstruction de fond sur thread `BelowNormal`
sans `await Task.Yield()`, câblage DI dans le bon ordre, curseurs et idempotence prouvés par sha256, rendu DST correct,
garde structurelle anti-pourcentage verte, deux gardes documentaires (§7/§8) vertes, `.gitattributes` LF en place, suite
complète 1415/1415 et 0 warning reproduits indépendamment de la revendication du SUMMARY. Le seul point noté est une dette
mineure déjà documentée par l'équipe d'exécution elle-même (absence de garde textuelle sur l'ordre réel des hosted
services dans `App.xaml.cs`, ordre actuellement correct) — classée Info, non bloquante.

---

*Verified: 2026-09-27*
*Verifier: Claude (gsd-verifier)*
