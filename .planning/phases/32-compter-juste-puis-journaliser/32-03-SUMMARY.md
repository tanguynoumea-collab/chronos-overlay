---
phase: 32-compter-juste-puis-journaliser
plan: 03
subsystem: services
tags: [cpt-03, mutex, mono-instance, diagnostic, processus, app-startup, garde-textuelle]

# Dependency graph
requires: []
provides:
  - "VerrouInstanceUnique.Acquerir(nom) → ResultatVerrou(Obtenu, Mutex, Abandonne, Nom) ; Liberer() ; NomOverlay = Local\\Chronos-overlay ; EtatPourDiagnostic(nom)"
  - "InventaireProcessus.Relever() (Process.GetProcesses filtré par préfixe, ne lève jamais) ; Filtrer(...) et Lignes(...) purs pour le diagnostic"
  - "App.xaml.cs : mutex posé après les court-circuits CLI et avant le Host ; message « tourne déjà » + Shutdown() ; OnExit tolérant (Host absent) et libération du verrou"
affects: [32-05 (câblage des lignes « N processus Chronos » au diagnostic), 32-08 (constat VAL-04 : geste « double-clic une seconde fois »)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Mutex nommé Local\\ (session Windows, aucun droit) acquis par initiallyOwned puis WaitOne(0) ; AbandonedMutexException = acquis"
    - "Champ statique pour la référence au mutex (le GC ne le libère jamais) ; ReleaseMutex sur le thread UI dans OnExit"
    - "Relevé machine séparé du calcul pur (Relever vs Filtrer/Lignes) ; prédicat unique EstChronos par préfixe"
    - "Garde textuelle de PLACEMENT dans OnStartup (IndexOf/LastIndexOf) : ordre court-circuits < verrou < Host"

key-files:
  created:
    - src/Chronos/Services/VerrouInstanceUnique.cs
    - src/Chronos/Services/InventaireProcessus.cs
    - tests/Chronos.Tests/VerrouInstanceUniqueTests.cs
    - tests/Chronos.Tests/InventaireProcessusTests.cs
  modified:
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs

key-decisions:
  - "D-32-09 : Local\\ et pas Global\\ — session courante, aucun droit, un autre utilisateur de la machine a son Chronos"
  - "D-32-10 : AbandonedMutexException = acquis (Obtenu = true, Abandonne = true) — un crash de l'overlay n'interdit jamais le redémarrage"
  - "D-32-11 : MessageBox WPF depuis App.xaml.cs (fichier WPF, hors gardes de pureté), pas de fenêtre dédiée"
  - "D-32-13 : _verrou est un champ STATIQUE ; libération dans OnExit sur le thread qui a acquis"
  - "Relever() et Filtrer() partagent un prédicat privé unique EstChronos (préfixe, OrdinalIgnoreCase) : un seul endroit à faire dériver"
  - "EtatPourDiagnostic : la table Tenus (noms tenus par ce processus) fait foi ; l'ouverture de l'objet noyau n'est qu'un constat d'existence"

patterns-established:
  - "Mutation combinée en arbre partagé : trois mutations à cibles disjointes jouées en un passage, restauration par copie octet-à-octet (jamais git checkout : autocrlf réécrirait les fins de ligne et fausserait le sha256)"

requirements-completed: [CPT-03]

# Metrics
duration: ~30 min (premier commit RED 01:01Z → fin ≈ 01:30Z, hors attentes de compilation partagée)
completed: 2026-09-27
---

# Phase 32 Plan 03: Une seule instance — mutex `Local\Chronos-overlay` avant le Host Summary

**Mutex nommé `Local\Chronos-overlay` acquis entre les court-circuits CLI et `Host.CreateApplicationBuilder()` : la seconde instance le dit (MessageBox) et se retire par `Shutdown()` sans rien démarrer ni tuer l'autre ; un abandon est un acquis ; `OnExit` tolère un Host jamais construit ; « N processus Chronos » calculable en pur par préfixe de nom, hooks éphémères signalés.**

## Performance

- **Duration:** ~30 min de travail effectif (SHA d'entrée `c9d65cf`) ; les attentes de fenêtres compilables dans l'arbre partagé (4 agents, 3 RED simultanés) ne sont pas comptées.
- **Started:** 2026-09-27T01:01Z (premier commit RED)
- **Completed:** 2026-09-27
- **Tasks:** 3/3
- **Files modified:** 6 (4 créés, 2 modifiés)

## Accomplishments

- `VerrouInstanceUnique` (neutre, `Services/`) : `Acquerir(nom)` → `ResultatVerrou(Obtenu, Mutex, Abandonne, Nom)` ; `initiallyOwned: true` puis `WaitOne(0)` ; `AbandonedMutexException` → `Obtenu = true, Abandonne = true` (D-32-10) ; `Liberer()` idempotent et tolérant (`ApplicationException` / `ObjectDisposedException`) ; `EtatPourDiagnostic(nom)` sans jamais acquérir (« tenu par ce processus » | « tenu par un autre processus » | « libre »).
- `InventaireProcessus` : `ProcessusChronos(Nom, Pid, Demarrage?)` ; `Relever()` réel (`Process.GetProcesses`, `StartTime` sous try/catch, ne lève jamais) ; `Filtrer` et `Lignes` PURS : « Processus Chronos : 3 (dont ce processus) — 2 autre(s) », une ligne par processus datée par `LibelleSource.Anciennete`, « dont N de moins de 10 s : hooks --hook probables (éphémères) », dernière ligne « Verrou mono-instance (Local\Chronos-overlay) : {état} ».
- `App.xaml.cs` : verrou posé après `--statusline`, `--hook`, `--cadrans`, `--sessions` et avant le Host (l. 51 `"--sessions"` < l. 67 `Acquerir` < l. 78 `CreateApplicationBuilder`) ; message « Chronos tourne déjà (une seule instance à la fois). Cette copie se retire ; l'autre continue. … » puis `Shutdown()` ; `_verrou` statique ; `_host` devient `IHost?` ; `OnExit` : `if (_host is not null) { StopAsync ; Dispose }` puis `_verrou?.Liberer()`.
- `GardesPerimetreTests` +2 (insertions seules, 18 → 20 `[Fact]`) : placement du verrou (unicité de l'acquisition, ordre, « tourne déjà » et `Shutdown();` entre verrou et Host, aucun `.Kill(`) ; sortie tolérante (`_host is not null`, `_verrou?.Liberer()` dans `OnExit`).

## Task Commits

| Task | Commit | Message |
| ---- | ------- | ------- |
| 1 RED | `c5348e5` | test(32-03): verrou mono-instance par mutex nomme, deux threads, abandon, RED 5 |
| 1 GREEN | `1025cb2` | feat(32-03): VerrouInstanceUnique - mutex nomme Local\Chronos-overlay, abandon = acquis, etat pour le diagnostic (CPT-03) |
| 2 RED | `e28e2a6` | test(32-03): inventaire des processus Chronos en pur - prefixe, pid courant, hooks ephemeres, RED 4 |
| 2 GREEN | `fc0846c` | feat(32-03): InventaireProcessus - N processus Chronos par prefixe, pid courant exclu, hooks ephemeres, etat du verrou (CPT-03) |
| 3 | `2385123` | feat(32-03): une seule instance - mutex Local\Chronos-overlay avant le Host, message et retrait, OnExit tolerant ; garde textuelle (CPT-03) |

## RED nommés (observés avant implémentation)

- Task 1 — 5 tests, échec de compilation `CS0246 ResultatVerrou` / `CS0103 VerrouInstanceUnique` : `La_premiere_acquisition_reussit_et_n_est_pas_un_abandon`, `Une_seconde_acquisition_du_meme_nom_sur_un_autre_thread_echoue`, `Apres_liberation_la_seconde_acquisition_reussit`, `Un_proprietaire_mort_sans_liberer_rend_le_verrou_comme_abandonne_mais_obtenu`, `L_etat_pour_le_diagnostic_dit_tenu_par_ce_processus_puis_libre`.
- Task 2 — 4 tests, `CS0246 ProcessusChronos` : `Le_filtre_par_prefixe_retient_les_exe_versionnes_et_ecarte_les_homonymes`, `Les_lignes_comptent_les_autres_sans_le_pid_courant_et_datent_chacun`, `Les_processus_de_moins_de_dix_secondes_sont_signales_comme_hooks_probables`, `Un_age_inconnu_est_dit_inconnu_et_l_etat_du_verrou_est_ecrit`.
- GREEN : filtre `Verrou|Inventaire|ServicesLayerPurity|GardesPerimetre` → **32 / 32 verts** (5 + 4 + 3 + 20).

## Mutations (jouées en un passage, révoquées, sha256 identiques, `git diff` vide)

| Mutation | Fichier | Changement | Rouge nommé |
| -------- | ------- | ---------- | ----------- |
| (v1) | VerrouInstanceUnique.cs | `if (m.WaitOne(0))` → `if (true)` | `Une_seconde_acquisition_du_meme_nom_sur_un_autre_thread_echoue` **[FAIL]** (et, conséquence directe : `WaitOne` n'étant plus appelé, l'abandon devient indétectable → `Un_proprietaire_mort_sans_liberer_…` [FAIL] aussi) |
| (p1) | InventaireProcessus.cs | `StartsWith(Prefixe, …)` → `Equals(Prefixe, …)` | `Le_filtre_par_prefixe_retient_les_exe_versionnes_et_ecarte_les_homonymes` **[FAIL]** |
| (a1) | App.xaml.cs | bloc du verrou déplacé AVANT le court-circuit `--hook` (Acquerir l. 33 < `"--hook"` l. 46) | `Le_verrou_mono_instance_est_pose_apres_les_court_circuits_et_avant_le_Host` **[FAIL]** |

sha256 avant = après :
- `App.xaml.cs` `cc39456e20503f1075eb49768f4bfaa2ce40baa621567774bb65847feb3f0c63`
- `VerrouInstanceUnique.cs` `a76e42122a60e6bb150ee7b13b77d36494d81180b711dfc360ba6025a473579f`
- `InventaireProcessus.cs` `8985a3c1b54e440c2352c4d0ad7618a31ac7991499ac32ca492ea00ff51d98ca`

Pourquoi un seul passage : arbre partagé par 4 agents, chaque fenêtre de compilation dure quelques secondes ; une mutation laissée en place gêne les autres. Les trois cibles sont disjointes (un test nommé par mutation), l'attribution est sans ambiguïté. Restauration par copie octet-à-octet depuis le scratchpad, pas par `git checkout` (`autocrlf=true` réécrirait LF → CRLF et casserait le sha256).

## Suite complète (fin de plan)

`dotnet test Chronos.sln -c Debug --nologo -v q`, deux exécutions consécutives **entièrement vertes** :
- Exécution 1 : **1271 tests, 1271 verts, 0 échec, 0 warning CS**.
- Exécution 2 : **1271 tests, 1271 verts, 0 échec, 0 warning CS**.
- Total 1271 ≥ 1211 attendu (les autres plans de la vague ont ajouté leurs tests entre-temps).
- Historique honnête : deux runs antérieurs donnaient 1268 / 1265 verts avec 3 échecs, tous **RED en cours du plan 32-01** dans l'arbre partagé (`TranscriptActivityProviderTests.Trois_lignes_d_un_meme_message_…`, `…Le_premier_timestamp_d_un_message_multi_blocs_…`, `DedupHeritageDeltaTests.TokensDepuisReleve_herite_…`), `DedupUsage.cs` étant alors modifié non validé par cet agent ; aucun test de 32-03 ni aucune garde existante n'a jamais échoué. Les fenêtres compilables étaient rares (RED externes successifs : `LigneJournalTests`, `LastExactStoreTests`, `VueAppDataTests`, `DiagnosticServiceTests`, `JournalRelevesTests`, `JournalisationUsageProviderTests`) — d'où les boucles d'attente 45-75 s.

## Warnings

- `dotnet build src/Chronos/Chronos.csproj -c Debug` : **0 warning avant, 0 warning après** sur mes fichiers. Un `CS0067` transitoire (`LastExactStore.EcritureRatee` jamais utilisé) est apparu puis disparu pendant la vague : chantier 32-02.

## Critères d'acceptation (grep)

| Critère | Attendu | Obtenu |
| ------- | ------- | ------ |
| `NomOverlay = @"Local\Chronos-overlay"` (grep **-F**) | 1 | 1 (l. 59) — le grep BRE du plan avec `\\` rend 0 sous Git Bash : quirk d'échappement, pas du fichier |
| `AbandonedMutexException` | ≥ 1 | 2 |
| `TryOpenExisting` | 1 | 1 (XML-doc reformulée pour ne pas répéter le jeton) |
| `System.Windows` dans VerrouInstanceUnique.cs | 0 | 0 |
| `new Thread` / `[Fact]` dans VerrouInstanceUniqueTests.cs | ≥ 3 / 5 | 6 / 5 |
| `public static IReadOnlyList<string> Lignes(` | 1 | 1 |
| `GetProcessesByName` | 0 | 0 (XML-doc reformulée) |
| `StartsWith(Prefixe` | 1 | 1 (prédicat unique `EstChronos`, partagé par `Filtrer` et `Relever`) |
| `LibelleSource.Anciennete` | ≥ 1 | 2 |
| `[Fact]` InventaireProcessusTests.cs | 4 | 4 |
| `Acquerir(NomOverlay)` / `_verrou;` / `tourne déjà` / `_host is not null` / `_verrou?.Liberer()` dans App.xaml.cs | 1 chacun | 1 chacun |
| ordre `"--sessions"` < `Acquerir` < `CreateApplicationBuilder` | oui | l. 51 < 67 < 78 |
| `[Fact]` GardesPerimetreTests.cs | 18 + 2 | 20 ; `--numstat` 68 insertions / 0 suppression |
| `ConfigureServices` intact | oui | hunks à l. 13, 60, 213 ; `ConfigureServices` l. 241 hors de tout hunk |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocage] Comptages d'acceptation faussés par des mentions en XML-doc**
- **Found during:** Task 3 (vérification des critères)
- **Issue:** `TryOpenExisting` = 2 et `GetProcessesByName` = 1 à cause de mes propres commentaires ; `StartsWith(Prefixe` = 2 (dupliqué entre `Filtrer` et `Relever`).
- **Fix:** XML-doc reformulées ; prédicat privé unique `EstChronos` utilisé par les deux méthodes (meilleur design : un seul endroit à faire dériver).
- **Files modified:** VerrouInstanceUnique.cs, InventaireProcessus.cs (avant leurs commits GREEN)
- **Commit:** inclus dans `1025cb2` et `fc0846c`

**2. Fins de ligne** : le plan annonçait `App.xaml.cs` en LF ; l'arbre de travail est en CRLF (`autocrlf=true`, index LF). CRLF conservé à l'identique (462/462 lignes), nouveaux fichiers en LF ; git normalise à la validation. Aucun impact.

Aucune autre déviation : plan exécuté tel qu'écrit.

## Known Stubs

Aucun. `InventaireProcessus.Lignes` et `VerrouInstanceUnique.EtatPourDiagnostic` sont complets mais **pas encore appelés** par `DiagnosticService` — c'est le câblage prévu en 32-05 (voir ci-dessous), volontairement hors de ce plan (`DiagnosticService.cs` appartient à 32-02 dans cette vague).

## Rappel pour 32-05

Insérer au diagnostic :
```csharp
InventaireProcessus.Lignes(InventaireProcessus.Relever(), Environment.ProcessId, now,
                           VerrouInstanceUnique.EtatPourDiagnostic(VerrouInstanceUnique.NomOverlay))
```
(`now` = `_clock.UtcNow` ; les lignes sont rendues sans indentation, à préfixer selon la section.)

## Rappel pour 32-08 (constat, D-32-12)

Les trois exe déjà en marche (3.1.0, 3.2.0, 3.2.1) ne connaissent pas ce mutex : le constat commence par les quitter (geste utilisateur), puis double-clic sur `Chronos-v3.2.2.exe` une seconde fois → message « tourne déjà », l'autre continue ; « tenu par un autre processus » se constate à ce moment-là (non observable en un processus de test).

## Interdits respectés

- Aucun `Chronos*.exe` lancé ; aucune écriture sous `~/.claude`, `%APPDATA%\Claude`, `%APPDATA%\Chronos` ; aucun paquet NuGet ajouté.
- `DiagnosticService.cs`, `ConfigureServices`, `ROADMAP.md`, `STATE.md` non touchés. `roadmap update-plan-progress` sauté (consigne de vague).

## Next Phase Readiness

CPT-03 tenue côté code. Prêt pour 32-05 (câblage diagnostic) et 32-08 (constat en vrai).

## Self-Check: PASSED

- Fichiers : `VerrouInstanceUnique.cs`, `InventaireProcessus.cs`, `VerrouInstanceUniqueTests.cs`, `InventaireProcessusTests.cs`, `App.xaml.cs`, `GardesPerimetreTests.cs`, `32-03-SUMMARY.md` — tous FOUND.
- Commits : `c5348e5`, `1025cb2`, `e28e2a6`, `fc0846c`, `2385123` — tous FOUND dans `git log --all`.
- Arbre de travail : aucun des six fichiers du plan n'est modifié après les commits (`git status` vide sur ces chemins).
- `REQUIREMENTS.md` : CPT-03 coché (l. 30) et traçabilité « Complete » (l. 171) via `requirements mark-complete` ; le fichier portait déjà, non validées, les lignes JRN-01..03 « Complete » du plan parallèle 32-04 — embarquées telles quelles dans le commit de docs.
