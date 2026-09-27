---
phase: 32-compter-juste-puis-journaliser
plan: 05
subsystem: composition racine (DI) / diagnostic / réglages — câblage du journal des relevés et âge de la dernière écriture
tags: [JRN-04, cablage-DI, decorateur, IHostedService, ordre-des-hosted-services, EcritureRatee, ecriture_ratee, IEtatMagasin, journal-muet, SeuilMuet, InventaireProcessus, VerrouInstanceUnique, MainViewModel, SettingsWindow, pastille-Alerte, garde-textuelle, mutation]

# Dependency graph
requires:
  - phase: 32-02 (CPT-02)
    provides: IEtatMagasin + NomsMagasins, LastExactStore.EcritureRatee, DiagnosticService(… magasins), section [Magasins persistants] + LigneMagasin, ChronosPaths.HistoriqueDir
  - phase: 32-03 (CPT-03)
    provides: InventaireProcessus.Relever/Lignes, VerrouInstanceUnique.NomOverlay/EtatPourDiagnostic, garde de placement dans App.xaml.cs
  - phase: 32-04 (JRN-01..03)
    provides: JournalReleves (SeuilMuet = 3 × CadenceNominale), JournalisationUsageProvider (IUsageProvider + IHostedService, SignalerEcritureRatee), IEtatJournal
  - phase: 18-19 (HDR-03/HDR-06, EXA-01)
    provides: motif « paramètre optionnel en dernière position » (IEtatServeur? etatServeur = null), carte « Sonde d'en-têtes » comme gabarit, jeton Alerte, LastExactUsageProvider en tête de chaîne
provides:
  - App.xaml.cs — graphe DI réel : JournalReleves(HistoriqueDir, IClock) + IEtatJournal (même instance) ; JournalisationUsageProvider ENTRE LastExactUsageProvider et le composite (inner brut) ; AddHostedService du journal AVANT RefreshOrchestrator ; store.EcritureRatee += SignalerEcritureRatee("last-exact", cause) ; magasins: [LastExactStore, JournalReleves] au DiagnosticService
  - JournalReleves : IEtatMagasin (Nom = NomsMagasins.JournalReleves, Chemin = Dossier)
  - DiagnosticService — paramètre optionnel demarrageProcessus (dernière position) ; « ALERTE — journal muet depuis N min » sous la ligne du journal ; lignes « Processus Chronos : … » et « Verrou mono-instance (Local\Chronos-overlay) : … » dans [Magasins persistants]
  - MainViewModel — IEtatJournal? journal = null (dernière position), TexteEtatJournal / AlerteJournal / AfficherEtatJournal, MajTexteEtatJournal() au ctor et à chaque ApplySnapshot, règle D-32-21
  - SettingsWindow — carte minimale « Journal des relevés » (section DONNÉES) : LigneEtatJournal + PastilleJournal (jeton Alerte, visible seulement en alerte)
  - Tests : CompositionRootTests +2 (ordre des hosted services, ecriture_ratee de bout en bout), GardesPerimetreTests +1 (les trois enveloppes), DiagnosticServiceTests +3, MainViewModelTests +4, ReglagesBindingTests +2, Fakes/FakeEtatJournal
affects: [32-07 (docs : le journal est en production dès la 3.2.2, vocabulaire « dernière écriture » / « journal muet depuis N min »), 32-08 (constat VAL : ligne « Processus Chronos », « Verrou … : tenu par ce processus », carte des réglages, fichier releves-AAAA-MM.jsonl qui s'écrit), 35 (ACC-01 : la carte complète remplace la ligne minimale — mêmes propriétés du VM)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Décorateur d'observation inséré ENTRE la tête et le composite par réécriture de l'inner (la chaîne exacte est déplacée telle quelle, 3 CompositeUsageProvider inchangés) ; prouvé par une garde textuelle d'ordre (IndexOf) ET un conteneur miroir"
    - "Ordre des IHostedService = ordre d'inscription (Start) et inverse (Stop) : le miroir DI l'épingle par GetServices<IHostedService>().IndexOf ; la garde textuelle épingle App.xaml.cs"
    - "Abonnement d'un événement de magasin à un décorateur dans la FABRIQUE du singleton de tête (une seule fois, mêmes instances que la chaîne)"
    - "Règle des 15 min : alerte = now − max(démarrage, dernière écriture ?? démarrage) > seuil dérivé ; le démarrage est capturé au ctor (clock.UtcNow) — testable par FakeClock, sans timer"
    - "Deux consommateurs, un seul vocabulaire (D-32-22) : diagnostic et réglages disent « dernière écriture {Anciennete} » et « journal muet depuis N min » avec LibelleSource.Anciennete"
    - "Boucle TDD isolée sous parallélisme : instantané au SHA d'entrée dans un dossier NEUF à chaque refresh (jamais rmtree sur un bin/obj verrouillé) + purge des fichiers absents du SHA d'entrée ; suite complète finale sur l'arbre réel"

key-files:
  created:
    - tests/Chronos.Tests/Fakes/FakeEtatJournal.cs
  modified:
    - src/Chronos/App.xaml.cs
    - src/Chronos/Services/Historique/JournalReleves.cs
    - src/Chronos/Services/DiagnosticService.cs
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/Views/SettingsWindow.xaml
    - tests/Chronos.Tests/CompositionRootTests.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - tests/Chronos.Tests/MainViewModelTests.cs
    - tests/Chronos.Tests/ReglagesBindingTests.cs

key-decisions:
  - "D-32-21 — alerte « journal muet » mesurée depuis max(démarrage du processus, dernière écriture ?? démarrage) > JournalReleves.SeuilMuet ; démarrage = clock.UtcNow au ctor du VM / du diagnostic ; recalcul au tick (60 s), aucun timer ni événement"
  - "D-32-22 — mêmes mots partout : « dernière écriture {Anciennete} », « journal muet depuis N min » ; le diagnostic préfixe « ALERTE — » sur une ligne dédiée sous celle du journal"
  - "D-32-23 — la pastille réutilise le jeton Alerte (#EFA23A) et le gabarit de la carte « Sonde d'en-têtes » ; SettingsWindow.xaml compte toujours 19 littéraux de couleur (aucune valeur nouvelle)"
  - "D-32-24 — le journal ne casse jamais le démarrage : rien n'est ajouté ; StartAsync du décorateur consigne dans DerniereErreur (32-04), et le diagnostic le montre"
  - "Le miroir DI (CompositionRootTests) reproduit les lignes d'App.xaml.cs mais ne LIT pas App.xaml.cs : c'est la garde textuelle qui tient l'ordre réel ; la mutation (c1) est donc jouée deux fois, sur le fichier de production (garde rouge) et sur le miroir (test DI rouge)"
  - "Scénario du test « écriture récente » : écriture 6 min APRÈS le démarrage, lecture 10 min après — le scénario littéral du plan (écriture à Now − 4 min pour un VM construit à Now) tombait dans la branche « écriture de la veille » de l'algorithme du plan lui-même"
  - "Le relevé de processus du diagnostic est sous try/catch : « Processus Chronos : relevé impossible (Type : message) », jamais une exception dans le rapport"

patterns-established:
  - "Mutation sur un fichier de PRODUCTION dont la preuve vit dans un miroir de test : jouer la mutation sur les deux (production → garde textuelle ; miroir → assertion DI) et le dire, plutôt que d'annoncer un rouge que le miroir ne peut pas produire"
  - "Comptage d'acceptation d'un libellé (grep -c = 1) : ne jamais reproduire le libellé dans un commentaire du même fichier (doctrine phase 19, rappelée par 32-04 §5)"

requirements-completed: [JRN-04]

# Metrics
duration: ≈ 26 min
completed: 2026-09-27
---

# Phase 32 Plan 05 : Le journal se branche, et l'âge de sa dernière écriture devient un chiffre de première classe — Summary

**Le décorateur de journalisation est inséré dans le graphe DI réel ENTRE `LastExactUsageProvider` et le composite (il voit l'inner brut), inscrit comme service hébergé AVANT `RefreshOrchestrator` (« demarrage » avant le premier relevé, « arret » après le dernier) ; une écriture ratée du dernier exact devient une ligne `ecriture_ratee` du journal ; le diagnostic et les réglages disent, avec les mêmes mots, « dernière écriture il y a N min » et crient « journal muet depuis N min » (pastille `Alerte`) au-delà de 15 min mesurées depuis max(démarrage, dernière écriture) — JRN-04 tenue, JRN-01/02/03 et CPT-02/03 câblés en production.**

## Performance

- **SHA d'entrée du plan :** `498055d` (HEAD au lancement : 1271 tests verts ; Release : 0 warning)
- **Duration :** ≈ 26 min (01:41:26Z → dernier commit de code `1eada40` à 02:02:50Z, puis suite complète ×4 et SUMMARY)
- **Started :** 2026-09-27T01:41:26Z
- **Completed :** 2026-09-27T02:08Z (approx.)
- **Tasks :** 3 / 3 (tâche 1 directe ; tâches 2 et 3 en TDD RED → GREEN ; mutations (c1), (g1), (m1) jouées et révoquées)
- **Files modified :** 11 (1 créé, 10 modifiés) — 589 insertions, 17 suppressions
- **Tests :** +12 pour ce plan. Instantané (498055d + mes fichiers) : **1283 / 1283** deux fois (= 1271 + 12). Arbre réel (HEAD `1eada40`, avec 32-06 vert) : **1309 / 1309** deux fois (10 s chacune), 0 échec.
- **Warnings Release** (`dotnet build src/Chronos/Chronos.csproj -c Release`) : 0 avant, **0 après** (mesuré sur l'instantané = mon delta exact).

## Accomplishments

- **Câblage (tâche 1)** — `App.xaml.cs` : `JournalReleves(HistoriqueDir, IClock)` + `IEtatJournal` (même instance) ; `JournalisationUsageProvider` enveloppe la chaîne exacte déplacée telle quelle (3 `CompositeUsageProvider`, inchangés) ; `AddHostedService` du journal AVANT celui de l'orchestrateur ; la tête `LastExactUsageProvider(inner: journalisation, …)` ; dans sa fabrique, `store.EcritureRatee += (_, cause) => journalisation.SignalerEcritureRatee("last-exact", cause)` ; `magasins: new IEtatMagasin[] { LastExactStore, JournalReleves }` au diagnostic. `JournalReleves : IEtatJournal, IEtatMagasin`. Prouvé par `CompositionRootTests` (+2) et une garde textuelle (+1).
- **Diagnostic (tâche 2)** — `[Magasins persistants]` : sous la ligne « journal des relevés : … », « ALERTE — journal muet depuis N min » si `now − max(_demarrage, DerniereEcriture ?? _demarrage) > JournalReleves.SeuilMuet` ; puis les lignes d'`InventaireProcessus.Lignes(Relever(), Environment.ProcessId, _clock.UtcNow, VerrouInstanceUnique.EtatPourDiagnostic(NomOverlay))` sous try/catch ; enfin la ligne des agrégats (phase 33). Paramètre optionnel `DateTimeOffset? demarrageProcessus = null` en dernière position (repli = instant de construction du diagnostic, singleton construit au lancement).
- **Réglages (tâche 3)** — `MainViewModel` : `IEtatJournal? journal = null` en 14e et dernière position, `_demarrage = clock.UtcNow`, `TexteEtatJournal` / `AlerteJournal` / `AfficherEtatJournal`, `MajTexteEtatJournal()` au ctor et dans `ApplySnapshot`. `SettingsWindow.xaml` : carte « Journal des relevés » dans DONNÉES (gabarit de la carte voisine, `Panel2`/`Ink`/`Ink2`), `LigneEtatJournal`, `PastilleJournal` (`DynamicResource Alerte`), carte masquée sans journal injecté.

## Texte exact produit par `MajTexteEtatJournal()` (D-32-22)

| Cas | `TexteEtatJournal` | `AlerteJournal` | `AfficherEtatJournal` |
|---|---|---|---|
| aucun journal injecté | `""` | false | false |
| écriture récente (démarrage à T, écriture T+6 min, lecture T+10 min, 3 relevés) | `dernière écriture il y a 4 min · 3 relevés depuis le démarrage` | false | true |
| 16 min sans écriture alors que Chronos tourne (écriture à T = démarrage, lecture T+16 min, 1 relevé) | `journal muet depuis 16 min · dernière écriture il y a 16 min · 1 relevé depuis le démarrage` | **true** | true |
| écriture de la veille, 5 min après le démarrage | `aucune écriture depuis le démarrage · dernière écriture il y a 1 j` | false | true |
| … puis 16 min après le démarrage, toujours rien | `journal muet depuis 16 min · aucune écriture depuis le démarrage · dernière écriture il y a 1 j` | **true** | true |
| jamais écrit depuis le démarrage (`DerniereEcriture` null, < 15 min) | `aucune écriture depuis le démarrage` | false | true |
| panne d'écriture (`DerniereErreur = "IOException : x"`) | `dernière écriture : ÉCHEC — IOException : x` (préfixé de « journal muet depuis N min · » si l'alerte est allumée) | selon l'âge | true |

Diagnostic (mêmes mots) : ligne du journal par `LigneMagasin` (« dernière écriture il y a N min », « aucune écriture (dossier absent) »…), puis `    ALERTE — journal muet depuis N min` sur la ligne suivante.

## Task Commits

| Tâche | Commit | Message |
|---|---|---|
| 1 | `96ebcf3` | feat(32-05): cablage DI - journal entre la tete et le composite, hosted service avant l'orchestrateur, ecriture ratee du dernier exact journalisee, magasins au diagnostic |
| 2 RED | `d3c22b4` | test(32-05): le diagnostic doit dire journal muet, compter les processus et l'etat du verrou, RED 3 |
| 2 GREEN | `53c9bb0` | feat(32-05): diagnostic - journal muet depuis N min, N processus Chronos, etat du verrou mono-instance (JRN-04, CPT-03) |
| 3 RED | `07c1cf9` | test(32-05): les reglages doivent dire la derniere ecriture du journal et alerter s'il se tait, RED 6 |
| 3 GREEN | `1eada40` | feat(32-05): carte « Journal des relevés » dans les reglages - derniere ecriture, alerte « journal muet depuis N min » (JRN-04) |

Tous en `--no-verify`, fichiers stagés explicitement. **Plan metadata :** commit `docs(32-05): complete …` (SUMMARY seul — voir ci-dessous pour REQUIREMENTS.md).

## RED nommés

- **Tâche 2** (RED de compilation, `CS1739`) : `Le_diagnostic_dit_journal_muet_quand_la_derniere_ecriture_a_plus_de_quinze_minutes`, `Le_diagnostic_ne_dit_pas_muet_juste_apres_le_demarrage`, `Le_diagnostic_compte_les_processus_Chronos_et_dit_l_etat_du_verrou` — le paramètre `demarrageProcessus` n'existait pas. GREEN : filtre `DiagnosticServiceTests|GardesPerimetreTests|LectureSeuleAppBureauTests` → **82 / 82**.
- **Tâche 3** (RED de compilation, `CS1729` ×2 + `CS1061` ×19, aucun autre symbole manquant) : `Sans_journal_injecte_la_carte_est_masquee`, `Une_ecriture_recente_donne_l_age_et_le_compte_sans_alerte`, `Seize_minutes_sans_ecriture_alors_que_Chronos_tourne_allument_l_alerte`, `L_alerte_ne_s_allume_pas_sur_l_ecriture_de_la_veille_juste_apres_le_demarrage`, `La_ligne_d_etat_du_journal_est_liee_au_ViewModel`, `La_pastille_du_journal_ne_se_voit_qu_en_alerte`. GREEN : filtre `MainViewModelTests|ReglagesBindingTests|CadranBindingTests|CompositionRootTests` → **89 / 89**.
- **Tâche 1** (pas de RED : câblage + tests écrits ensemble) : filtre `CompositionRootTests|GardesPerimetreTests|ServicesLayerPurityTests|JournalisationUsageProviderTests` → **43 / 43**.

## Mutations (jouées dans l'instantané, rouges nommés, révoquées par copie, sha256 identiques)

| Mutation | Fichier muté | Contenu | Rouge(s) nommé(s) | sha256 avant = après |
|---|---|---|---|---|
| (c1) | `App.xaml.cs` | les deux `AddHostedService` inversés (journal APRÈS orchestrateur) | `GardesPerimetreTests.Le_journal_enveloppe_le_composite_et_la_tete_enveloppe_le_journal` (1 échec / 28) | `4686e633a618131d…` |
| (c1-miroir) | `CompositionRootTests.cs` (conteneur miroir) | même inversion | `CompositionRootTests.Le_journal_est_branche_entre_la_tete_et_le_composite_et_demarre_avant_l_orchestrateur` (1 échec / 28) | `6f6fecc308b54167…` |
| (g1) | `DiagnosticService.cs` | `Max(_demarrage, j.DerniereEcriture ?? _demarrage)` → `j.DerniereEcriture ?? _demarrage` | `DiagnosticServiceTests.Le_diagnostic_ne_dit_pas_muet_juste_apres_le_demarrage` (1 échec / 55) | `393386f05ecd9301…` |
| (m1) | `MainViewModel.cs` | `age > JournalReleves.SeuilMuet` → `age > TimeSpan.Zero` | `MainViewModelTests.Une_ecriture_recente_donne_l_age_et_le_compte_sans_alerte` **et** `L_alerte_ne_s_allume_pas_sur_l_ecriture_de_la_veille_juste_apres_le_demarrage` (2 échecs / 58) | `e3f9d4ca953fe418…` |

Pourquoi (c1) deux fois : le plan attendait que l'inversion dans `App.xaml.cs` rougisse AUSSI le test DI. Or `CompositionRootTests` est un **miroir** (il reproduit les lignes, il ne lit pas `App.xaml.cs`) — comme toutes les gardes DI existantes du fichier. L'inversion en production ne peut rougir que la garde textuelle ; l'inversion du miroir prouve que l'assertion d'ordre du test DI est vivante. Les deux ont été jouées et les deux rougissent le test attendu. Révocation par copie octet-à-octet (jamais `git checkout --`).

## Câblage exact — pour 32-07 (docs) et 32-08 (constat)

```csharp
// App.xaml.cs, ConfigureServices (extrait, ordre réel)
services.AddSingleton(sp => new JournalReleves(sp.GetRequiredService<ChronosPaths>().HistoriqueDir, sp.GetRequiredService<IClock>()));
services.AddSingleton<IEtatJournal>(sp => sp.GetRequiredService<JournalReleves>());
services.AddSingleton(sp => new JournalisationUsageProvider(
    inner: new CompositeUsageProvider(/* sonde → OAuth Chronos → OAuth coffre (gated) → pont statusLine, inchangé */),
    journal: …JournalReleves, etatServeur: …IEtatServeur, authStatus: …IAuthStatus, clock: …IClock));   // version : lue de l'assembly
services.AddHostedService(sp => sp.GetRequiredService<JournalisationUsageProvider>());              // AVANT RefreshOrchestrator
services.AddSingleton<IUsageProvider>(sp =>
{
    var store = sp.GetRequiredService<LastExactStore>();
    var journalisation = sp.GetRequiredService<JournalisationUsageProvider>();
    store.EcritureRatee += (_, cause) => journalisation.SignalerEcritureRatee("last-exact", cause);   // → ligne ecriture_ratee
    return new LastExactUsageProvider(inner: journalisation, store: store, clock: …, activite: …);
});
… services.AddHostedService(sp => sp.GetRequiredService<RefreshOrchestrator>());
services.AddSingleton(sp => new DiagnosticService(…, moniteurSessions: …, magasins: new IEtatMagasin[] { LastExactStore, JournalReleves }));
```

- Ordre de démarrage du Host : `TokenRefreshService` → **`JournalisationUsageProvider`** (Purger + `demarrage`) → `RefreshOrchestrator` (premier relevé) ; arrêt en ordre inverse (`arret` après le dernier relevé).
- Fichier écrit : `%APPDATA%\Chronos\historique\releves-AAAA-MM.jsonl` (mois UTC), une ligne de relevé par (t, source) toutes les 5 min quand la sonde répond exact.
- Ce que le constat 32-08 doit lire dans le rapport « Diagnostic… » : `[Magasins persistants]` → « journal des relevés : … — dernière écriture il y a N min (… o) », pas de ligne « ALERTE » ; « Processus Chronos : 1 (dont ce processus) — 0 autre(s) » (après avoir quitté les anciennes versions) ; « Verrou mono-instance (Local\Chronos-overlay) : tenu par ce processus ». Dans les réglages : carte « Journal des relevés » → « dernière écriture il y a N min · N relevés depuis le démarrage », pastille absente.
- `DiagnosticService` n'a PAS reçu `demarrageProcessus` depuis `App.xaml.cs` (défaut = instant de construction du singleton ≈ démarrage) ; `MainViewModel` capture `_demarrage` à sa construction (idem).

## Files Created/Modified

- `src/Chronos/App.xaml.cs` (CRLF conservé) — `using Chronos.Services.Historique;` ; bloc journal + décorateur + hosted ; tête réécrite en lambda-bloc avec l'abonnement ; `magasins:` au diagnostic. Greps : `AddHostedService(…JournalisationUsageProvider…)` = 1 (l. 438) < `…RefreshOrchestrator…` (l. 468) ; `SignalerEcritureRatee("last-exact"` = 1 ; `HistoriqueDir` = 1 ; `new CompositeUsageProvider(` = 3 ; `magasins: new IEtatMagasin[]` = 1.
- `src/Chronos/Services/Historique/JournalReleves.cs` — `: IEtatJournal, IEtatMagasin`, `Nom`, `Chemin` (+ un paragraphe de XML-doc).
- `src/Chronos/Services/DiagnosticService.cs` (CRLF conservé) — `using Chronos.Services.Historique;`, champ `_demarrage`, paramètre `demarrageProcessus`, bloc « journal muet » + bloc processus/verrou, helper privé `Max`. Greps : `journal muet depuis` = 1, `JournalReleves.SeuilMuet` = 1, `InventaireProcessus.Lignes(` = 1, `VerrouInstanceUnique.EtatPourDiagnostic` = 1, `DateTimeOffset? demarrageProcessus = null` = 1, jetons interdits (`RacinesEtat|Claude_|claude-code-sessions|.Lire(|new SessionMonitor`) = 0, `Inspecter(` = 1.
- `src/Chronos/ViewModels/MainViewModel.cs` — 3 `[ObservableProperty]`, 2 champs, paramètre `journal`, `MajTexteEtatJournal()` (déclaration + ctor + `ApplySnapshot` = 3 occurrences). Greps : `IEtatJournal? journal = null` = 1, `journal muet depuis` = 1, `JournalReleves.SeuilMuet` = 1.
- `src/Chronos/Views/SettingsWindow.xaml` — carte entre « Sonde d'en-têtes » et `<!-- Thème -->`. Greps : `LigneEtatJournal` = 1, `PastilleJournal` = 1, `DynamicResource Alerte` = 1 (0 avant), littéraux de couleur = 19 (inchangé).
- `tests/Chronos.Tests/Fakes/FakeEtatJournal.cs` (nouveau) ; `CompositionRootTests.cs` (+2, helper `ConteneurAvecJournal`, 6 → 8 `[Fact]/[WpfFact]`) ; `GardesPerimetreTests.cs` (+1, 20 → 21) ; `DiagnosticServiceTests.cs` (+3, 52 → 55) ; `MainViewModelTests.cs` (+4, 48 → 52 ; `Build(…, journal:)` optionnel, aucun site existant modifié) ; `ReglagesBindingTests.cs` (+2, 5 → 7 ; `MonterReglages(…, journal:, clock:)` optionnels, aucun test existant modifié).

## Decisions Made

Voir `key-decisions` : D-32-21 à D-32-24 telles qu'écrites dans le plan, plus trois décisions d'exécution (mutation (c1) jouée sur les deux fichiers ; scénario du test « écriture récente » ; relevé de processus sous try/catch avec message typé).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Cohérence du test] Scénario de `Une_ecriture_recente_donne_l_age_et_le_compte_sans_alerte`**
- **Found during:** Task 3 (écriture du RED)
- **Issue:** le plan décrit « journal `DerniereEcriture = Now − 4 min` » pour un VM construit à `Now`, et attend « dernière écriture il y a 4 min · 3 relevés depuis le démarrage ». Or l'algorithme du plan lui-même range une écriture ANTÉRIEURE au démarrage dans la branche « aucune écriture depuis le démarrage · dernière écriture … » : le test aurait rougi pour de mauvaises raisons.
- **Fix:** démarrage à `Now`, écriture à `Now + 6 min`, horloge à `Now + 10 min` → même texte attendu, même âge de 4 min, branche nominale.
- **Files modified:** `tests/Chronos.Tests/MainViewModelTests.cs` — **Committed in:** `07c1cf9`

**2. [Rule 1 - Critère d'acceptation] Le commentaire de `DiagnosticService` reproduisait le jeton « journal muet depuis »**
- **Found during:** Task 2 (grep -c = 2 au lieu de 1) — doctrine phase 19, déjà rencontrée par 32-04 (§5).
- **Fix:** commentaire reformulé (« un seul libellé dans ce fichier »). **Committed in:** `53c9bb0`

**3. [Rule 3 - Blocage] `using System.Windows.Shapes` rend `Path` ambigu dans `ReglagesBindingTests`**
- **Found during:** Task 3 (RED : 5 × `CS0104` parasites en plus du RED attendu)
- **Fix:** pas de `using` ; `Assert.IsType<System.Windows.Shapes.Ellipse>(…)` qualifié. **Committed in:** `07c1cf9`

**4. [Rule 3 - Blocage] Boucle TDD dans un instantané isolé, régénéré dans un dossier neuf**
- **Found during:** Task 1 (mutation (c1)) : le RED commité de 32-06 (`LecteurJournal.Lire` absent, puis `AnalyseReleves`) cassait la compilation du projet de tests sur l'arbre partagé.
- **Fix:** `git archive 498055d` (SHA d'entrée) + mes 11 fichiers copiés par-dessus, dans le scratchpad (précédent 32-02). Un premier instantané s'est révélé pollué (un `rmtree` sur un `bin/obj` verrouillé par le serveur de build avait laissé des fichiers d'un archivage antérieur de HEAD) : le script crée désormais un dossier NEUF à chaque refresh et purge tout fichier absent du SHA d'entrée et hors de ma liste. Les filtres, les 4 mutations et la suite complète (1283 ×2) y ont été joués ; la suite complète finale a été rejouée sur l'ARBRE RÉEL (1309 ×2) une fois 32-06 vert.
- **Files modified:** aucun du dépôt.

**5. [Observation - pas un correctif] Mutation (c1) et le miroir DI**
- Voir le tableau des mutations : (c1) sur `App.xaml.cs` ne peut rougir que la garde textuelle ; la même inversion appliquée au miroir rougit le test DI. Les deux ont été jouées. Aucun code changé ; le plan attendait un double rouge depuis un seul fichier, ce que la structure existante des gardes DI (miroirs) ne permet pas.

**6. [Note d'exécution parallèle] `REQUIREMENTS.md`**
- `requirements mark-complete JRN-04` a été exécuté (JRN-04 coché, traçabilité « Complete »). Entre l'exécution et mon staging, l'agent 32-06 a commité `REQUIREMENTS.md` dans son commit de docs `dfc6e1d` (arbre partagé) : ce commit porte donc mes deux lignes JRN-04 en plus des siennes (JRN-05). `git diff` est vide pour ce fichier ; le commit de docs de ce plan ne contient que le SUMMARY. Même situation, en miroir, que 32-03 § Self-Check.

---

**Total deviations :** 4 auto-fixed (2 × Rule 1, 2 × Rule 3) + 2 notes. Aucune modification hors de `files_modified` (+ ce SUMMARY) ; `ROADMAP.md` et `STATE.md` non touchés ; aucun fichier de 32-06 touché.

## Issues Encountered

- **Exécution parallèle (2 agents, même arbre)** : HEAD a avancé de `498055d` à `dfc6e1d` pendant le plan (6 commits de 32-06 entrelacés avec mes 5). Aucun conflit de fichier : les périmètres étaient disjoints. Les fenêtres non compilables ont été absorbées par l'instantané, sans attente.
- **Encodage de la console** : les noms des tests rouges ne sont lisibles qu'en capturant la ligne `Chronos.Tests.<classe>.<test> [FAIL]` (préfixe `[xUnit.net …]`), pas la ligne de bilan (accents mangés en `-v q`).
- **`InventaireProcessus.Relever()` sous test** lit la vraie table des processus (lecture seule, aucun verrou acquis) : la ligne « Processus Chronos : N … » dépend de la machine — le test n'épingle que la forme, pas le nombre.

## Known Stubs

Aucun stub. Deux textes à connaître :
- « un relevé exact toutes les 5 min, écrit dans %APPDATA%\Chronos\historique — sans interface pour l'instant » (sous-titre de la carte) : ASSUMÉ, c'est l'état réel jusqu'à la phase 35 (ACC-01).
- La branche « écriture de la veille » de `MajTexteEtatJournal` (`derniere < _demarrage`) est **inatteignable avec le `JournalReleves` réel** (son `DerniereEcriture` est celle de CE processus, jamais amorcée du disque — contrat `IEtatJournal`) ; elle est gardée parce que le plan l'exige et qu'un futur `IEtatJournal` amorcé du disque (phase 35, « journal ouvert le … ») la rendra vivante ; testée via le faux.

## Next Phase Readiness

- **32-07 (docs)** : `docs/data-sources.md` peut décrire le journal comme EN PRODUCTION à partir de la 3.2.2 (câblage ci-dessus), avec le vocabulaire « dernière écriture », « journal muet depuis N min », `ecriture_ratee` (magasin `last-exact`).
- **32-08 (constat)** : gestes et lignes attendues listés dans « Câblage exact » ; l'alerte est vérifiable sans attendre 15 min en lisant le rapport tout de suite après le lancement (pas d'ALERTE) — le cas « muet » ne se constate qu'en coupant la sonde 15 min (interrupteur des réglages), ce qui est un geste explicite de VAL.
- Mises à jour d'état laissées à l'orchestrateur (règle de vague) : `state advance-plan`, `state update-progress`, `state record-metric`, `roadmap update-plan-progress` — non exécutées par cet agent.

## Self-Check: PASSED

- 11 fichiers du plan + SUMMARY : FOUND ×12.
- Commits `96ebcf3`, `d3c22b4`, `53c9bb0`, `07c1cf9`, `1eada40` : FOUND ×5 dans `git log --all`.
- `REQUIREMENTS.md` : JRN-04 coché et « Complete » dans HEAD (porté par `dfc6e1d`, voir Deviations §6) ; `git diff` vide.
- `ROADMAP.md`, `STATE.md` : non modifiés par ce plan ; aucun fichier de 32-06 touché (5 commits `(32-05)` vérifiés).
- Suite complète sur l'arbre réel : 1309 / 1309 puis 1309 / 1309 ; instantané : 1283 / 1283 ×2 ; Release 0 warning.

---
*Phase: 32-compter-juste-puis-journaliser*
*Completed: 2026-09-27*
