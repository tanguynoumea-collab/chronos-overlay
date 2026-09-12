---
phase: 18
slug: source-exacte-par-en-t-tes-de-rate-limit
status: planned
nyquist_compliant: true
wave_0_complete: planned
created: 2026-09-12
---

# Phase 18 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (net8.0-windows) — `tests/Chronos.Tests` |
| **Quick run command** | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~RateLimit\|FullyQualifiedName~Header\|FullyQualifiedName~Unite"` |
| **Full suite command** | `dotnet test Chronos.sln -v q --nologo` |
| **Estimated runtime** | ~55 s · baseline mesurée : **505 tests / 0 échec** |

## Sampling Rate

- Après chaque commit de tâche : commande rapide
- Après chaque vague : suite complète
- Avant vérification de phase : suite complète verte

## Contraintes de sécurité qui priment sur toute vérification

- **Aucune requête réseau réelle depuis un test.** Tout par `FakeHttpMessageHandler`.
- **Ne JAMAIS appeler l'endpoint de refresh avec le refresh token RÉEL** de `%APPDATA%\Chronos\oauth.dat`.
  Contrôle avant/après chaque plan : **518 octets, mtime 1783863147**.
- Le jeton ne vit qu'en variable locale. Jamais logué, écrit, mis en exception, ni concaténé dans une URL.
- Le corps de la réponse HTTP n'est jamais transporté ni journalisé.
- Aucun test n'écrit dans le vrai `%APPDATA%\Chronos\` — chemins depuis `Path.GetTempPath()`.

## Per-Task Verification Map

*Rempli par le planner le 2026-09-12. Toute tâche a une commande automatisée ; la seule vérification
manuelle est isolée en fin de phase (plan 18-06, tâche 3).*

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 18-01 T1 | 18-01 | 1 | HDR-05 | unit (TDD) | `--filter "FullyQualifiedName~UsageNormalizationTests"` | ❌ créé par la tâche | ⬜ pending |
| 18-01 T2 | 18-01 | 1 | HDR-05 | non-régression | suite complète (les 3 classes de providers vertes SANS modification) | ✅ existe | ⬜ pending |
| 18-01 T3 | 18-01 | 1 | HDR-05 | garde de source | `--filter "FullyQualifiedName~NormalisationUniqueTests"` | ❌ créé par la tâche | ⬜ pending |
| 18-02 T1 | 18-02 | 1 | HDR-03, HDR-04 | unit (TDD) | `--filter "FullyQualifiedName~StatutServeurTests"` et `~WindowStateTests` | ⚠ WindowStateTests à étendre | ⬜ pending |
| 18-02 T2 | 18-02 | 1 | HDR-03, HDR-04 | unit (TDD) | `--filter "FullyQualifiedName~CompositeUsageProviderTests"` | ⚠ à étendre | ⬜ pending |
| 18-02 T3 | 18-02 | 1 | HDR-02 (outillage) | Wave 0 de transport | `--filter "FullyQualifiedName~EnTetesDeReferenceTests"` | ❌ créé par la tâche | ⬜ pending |
| 18-03 T1 | 18-03 | 2 | HDR-01, HDR-06 | unit | `--filter "FullyQualifiedName~RateLimitHeaderUsageProviderTests"` | ❌ créé par la tâche | ⬜ pending |
| 18-03 T2 | 18-03 | 2 | HDR-01, HDR-02 | unit | `--filter "FullyQualifiedName~RateLimitHeaderUsageProviderTests"` | ⚠ étendu | ⬜ pending |
| 18-03 T3 | 18-03 | 2 | HDR-06 | unit | `--filter "FullyQualifiedName~RateLimitHeaderUsageProviderTests"` | ⚠ étendu | ⬜ pending |
| 18-04 T1 | 18-04 | 3 | HDR-03 | unit | `--filter "FullyQualifiedName~RateLimitHeaderUsageProviderTests"` | ⚠ étendu | ⬜ pending |
| 18-04 T2 | 18-04 | 3 | HDR-04 | unit | `--filter "FullyQualifiedName~RateLimitHeaderUsageProviderTests"` | ⚠ étendu | ⬜ pending |
| 18-05 T1 | 18-05 | 4 | HDR-01, HDR-02 | garde DI (comportement) | `--filter "FullyQualifiedName~CompositionRootTests"` | ⚠ à étendre | ⬜ pending |
| 18-05 T2 | 18-05 | 4 | HDR-03, HDR-04, HDR-06 | unit | `--filter "FullyQualifiedName~DiagnosticServiceTests"` | ⚠ à étendre | ⬜ pending |
| 18-06 T1 | 18-06 | 5 | HDR-03, HDR-04, HDR-06 | unit (TDD) | `--filter "FullyQualifiedName~MainViewModelTests"` et `~WindowGaugeViewModelTests` | ⚠ à étendre | ⬜ pending |
| 18-06 T2 | 18-06 | 5 | HDR-06 | BAML (`[WpfFact]`) | `--filter "FullyQualifiedName~ReglagesBindingTests"` | ❌ créé par la tâche | ⬜ pending |
| 18-06 T3 | 18-06 | 5 | HDR-02 (production) | **checkpoint humain** | suite complète (l'automatisation ne peut pas trancher le 429 réel) | — | ⬜ pending |

Toutes les commandes se préfixent de `dotnet test Chronos.sln -v q --nologo`.

### Couverture des requirements

| Req | Plans | Preuve automatisée principale |
|-----|-------|-------------------------------|
| HDR-01 | 18-03, 18-05 | `RateLimitHeaderUsageProviderTests.Un_200_porteur_d_en_tetes_rend_deux_fenetres_exactes` |
| HDR-02 | 18-03, 18-05 | `...Un_429_livre_quand_meme_les_chiffres` + `...Un_429_muet_n_invente_rien` (**production : checkpoint 18-06 T3**) |
| HDR-03 | 18-02, 18-04, 18-06 | `...Le_statut_serveur_voyage_avec_la_fenetre` + `CompositeUsageProviderTests` (survie à `Best()`) |
| HDR-04 | 18-02, 18-04, 18-06 | `...Le_depassement_seul_passe_par_le_canal_lateral` |
| HDR-05 | 18-01 | `UsageNormalizationTests.Un_en_tete_en_point_decimal_se_lit_meme_sous_une_culture_a_virgule` + `NormalisationUniqueTests` |
| HDR-06 | 18-03, 18-05, 18-06 | `...La_cadence_est_bornee_a_une_sonde_par_cadence_nominale` + `ReglagesBindingTests.Le_cout_de_la_sonde_est_ECRIT_dans_les_reglages` |

## Gardes permanentes

| Garde | Commande | Attendu |
|-------|----------|---------|
| Pureté Services | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~ServicesLayerPurityTests"` | vert |
| Composition DI | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~CompositionRootTests"` | vert (la sonde se résout, en `primary`) |
| `CompositeUsageProvider` intact | `git diff --name-only HEAD~N -- src/Chronos/Services/CompositeUsageProvider.cs` | vide (refonte = phase 19) |
| Un seul rafraîchisseur | `grep -rln "RefreshAsync" src/Chronos` | exactement 2 fichiers (client + autorité) |
| Coffre intact | `stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"` | `518 1783863147` |
| Suite complète | `dotnet test Chronos.sln -v q --nologo` | 0 échec |

## Wave 0 Requirements

L'infrastructure xUnit existe. Wave 0 pose les briques manquantes identifiées par la recherche :

- [x] *(plan 18-02, tâche 3)* Faux de transport capable de porter des **en-têtes de réponse sur un code
      d'erreur** (notamment 429). Constaté : `FakeHttpMessageHandler` ne le permet PAS aujourd'hui (ses
      deux fabriques `Json` / `Throws` ne posent aucun en-tête) → ajout **additif** de
      `AvecEnTetes(statut, enTetes, corps)` et `SequenceAvecEnTetes(...)`, avec
      `Headers.TryAddWithoutValidation` (obligatoire : `Add` rejette les noms non standard).
- [x] *(plan 18-02, tâche 3)* Jeux d'en-têtes de référence centralisés dans
      `tests/Chronos.Tests/EnTetesDeReference.cs` : `Nominal`, `Avertissement`, `Refus`, `StatutInconnu`,
      `DepassementSeul`, `Absents`, `Illisibles`, **plus** `NominalCasseMelangee` (la recherche insensible
      à la casse est vérifiée, pas supposée). Valeurs reprises d'un dump réel indépendant ; épochs
      postérieurs au plancher de sanité, sauf le jeu `Illisibles` qui porte volontairement `"9"` —
      l'epoch 1970 RÉEL du `usage.json` de cette machine.
- [x] *(plan 18-01, tâche 1 — écrit AVANT toute lecture d'en-tête ; rejoué de bout en bout au plan 18-03,
      tâche 2)* Garde de culture : le test force `CultureInfo.CurrentCulture = new CultureInfo("fr-FR")`
      (restaurée en `finally`), asserte d'abord le piège (`Assert.False(double.TryParse("0.63", out _))`)
      puis le comportement correct (`FractionDepuisTexteFraction("0.63") == 0.63`).
- [x] *(plan 18-01, tâches 2 et 3)* Garde d'unités : `NormalisationUniqueTests` balaie le **texte source**
      de `src/Chronos/Services/` et `src/Chronos/Models/` (la réflexion ne peut pas voir un `/ 100`), chemin
      **injecté par MSBuild** via `AssemblyMetadata("CheminSourcesChronos", ...)` — jamais deviné depuis
      `AppContext.BaseDirectory`, et `Assembly.Location` est interdit par CLAUDE.md. 5 motifs interdits,
      4 exemptions **nominatives et documentées** (`UsageNormalization.cs`, `ClaudeTokenReader.cs`,
      `SessionMonitor.cs`, `TranscriptActivityProvider.cs`). Le test **échoue** si le chemin injecté
      n'existe pas : une garde qui se met en sourdine ne garde rien.
      *Note de planification :* l'inventaire réel est de **12 sites** dans **4 fichiers** (et non 5), parce
      que rendre la garde étanche impose de rapatrier aussi les 3 conversions d'epoch en millisecondes de
      `DiagnosticService.cs` et `ClaudeUsageObjectProvider.cs`. Les 12 sites sont listés ligne par ligne
      dans le `<context>` du plan 18-01.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Un 429 **réel** porte bien les en-têtes | HDR-02 | Non vérifiable sans jeton valide ET compte saturé. Le jeton de l'utilisateur est expiré depuis le 2026-07-12 | Après reconnexion, saturer la fenêtre 5 h puis consulter le diagnostic : les chiffres doivent rester exacts pendant le refus |
| Noms d'en-têtes réellement présents | HDR-01 / HDR-05 | Les en-têtes `unified` sont absents de la doc publique Anthropic (source : code de `claude-session-browser` + un dump indépendant) | Le diagnostic doit lister les **noms** d'en-têtes de limite reçus (jamais les valeurs) — tranchera au premier rafraîchissement réussi |
| Coût réel de la sonde | HDR-06 | Dépend de la facturation observée sur le compte | Vérifier l'ordre de grandeur annoncé dans les réglages après quelques heures de fonctionnement |

**HDR-02 ne doit pas être coché « prouvé en production » avant la vérification manuelle ci-dessus.**

## Validation Sign-Off

- [x] Toutes les tâches ont une commande automatisée ou une dépendance Wave 0 — 16 tâches, 16 commandes.
- [x] Aucune requête réseau réelle dans aucun test — tout par `FakeHttpMessageHandler`. Les seuls appels
      réseau réels du dépôt restent ceux de `DiagnosticService` en production, jamais déclenchés par un
      test (les tests existants passent par `FakeClaudeTokenReader`).
- [x] Le cas « 429 sans en-têtes » est traité explicitement — branche `SaturationSansEnTetes` du tableau
      d'aiguillage (plan 18-03) et test `Un_429_muet_n_invente_rien`, avec ET sans `Retry-After`.
- [x] Un statut serveur inconnu n'est jamais rangé d'autorité dans « autorisé » — quatrième membre
      `NonReconnu` (plan 18-02), test `Un_statut_inconnu_ne_se_devine_pas`, et distinction explicite entre
      `null` (rien rapporté) et `NonReconnu` (rapporté mais hors de l'ensemble connu).
- [x] La vérification manuelle du 429 réel est isolée en tâche de checkpoint bloquante (plan 18-06,
      tâche 3), et **HDR-02 ne peut pas être coché « prouvé en production » sans elle**.
