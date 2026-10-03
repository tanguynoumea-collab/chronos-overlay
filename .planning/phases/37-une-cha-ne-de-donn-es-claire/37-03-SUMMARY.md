---
phase: 37-une-cha-ne-de-donn-es-claire
plan: 03
subsystem: services, diagnostic, réglages, composition
tags: [purge-r5, jeton-app-bureau, gardes, diagnostic]
requires: ["37-02"]
provides:
  - "Chaîne à 2 composites : sonde → (OAuth Chronos → pont statusLine)"
  - "Signature DiagnosticService(paths, settings, composite, clock, …) sans lecteur de jeton ni inventaire machine"
  - "Garde de non-retour étendue (types de l'étape 3, OAuthUsageEnabled, EndpointOAuthClaude)"
affects: [37-04, 37-05, 37-06]
tech-stack:
  added: []
  patterns: ["tolérance des données héritées épinglée par test (journal : enum inconnu ignoré ; settings : membre inconnu ignoré)"]
key-files:
  created: []
  deleted:
    - src/Chronos/Services/ClaudeOAuthUsageProvider.cs
    - src/Chronos/Services/GatedOAuthUsageProvider.cs
    - src/Chronos/Services/ClaudeTokenReader.cs
    - src/Chronos/Services/IClaudeTokenReader.cs
    - src/Chronos/Services/WindowsCredentialStore.cs
    - src/Chronos/Services/InventaireMachine.cs
    - src/Chronos/Services/IInventaireMachine.cs
    - tests/Chronos.Tests/ClaudeOAuthUsageProviderTests.cs
    - tests/Chronos.Tests/GatedOAuthUsageProviderTests.cs
    - tests/Chronos.Tests/ClaudeTokenReaderTests.cs
    - tests/Chronos.Tests/Fakes/FakeClaudeTokenReader.cs
    - tests/Chronos.Tests/Fakes/FakeInventaireMachine.cs
    - tests/Chronos.Tests/Fakes/V10TestVault.cs
  modified:
    - src/Chronos/App.xaml.cs
    - src/Chronos/Services/DiagnosticService.cs
    - src/Chronos/Services/ChronosSettings.cs
    - src/Chronos/Models/SourceUsage.cs
    - src/Chronos/Text/LibelleSource.cs
    - src/Chronos/Services/RateLimitHeaderUsageProvider.cs
    - docs/data-sources.md
    - tests/Chronos.Tests/GardesPerimetreTests.cs
    - tests/Chronos.Tests/LigneJournalTests.cs
    - tests/Chronos.Tests/SettingsServiceTests.cs
    - tests/Chronos.Tests/LibelleSourceTests.cs
    - tests/Chronos.Tests/RateLimitHeaderUsageProviderTests.cs
    - tests/Chronos.Tests/NormalisationUniqueTests.cs
    - tests/Chronos.Tests/GardeDocumentationChaineTests.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs (+ 8 sites de construction dans d'autres fichiers de test)
decisions:
  - "Conflit C2 respecté : seule EndpointOAuthClaude quitte l'enum ; PontStatusLine reste jusqu'à l'étape 5"
  - "Rapport_sans_token_n_expose_jamais_un_jeton garde son assertion DoesNotContain(\"SECRET-TOKEN\") alors que le lecteur de jeton n'existe plus (le rapport n'a plus accès à aucun jeton)"
  - "LibelleSourceTests : seuil anti-mutisme ramené de 5 à 4 membres (3 après l'étape 5)"
  - "Test DEL-06 (fixture legacy, qui contient encore \"OAuthUsageEnabled\": true) : l'assertion devient un commentaire, et le test prouve maintenant aussi la tolérance au membre retiré"
metrics:
  duration: "≈ 15 min"
  completed: 2026-10-03
  tasks: 3
  files: 35
requirements: [DAT-02]
---

# Phase 37 Plan 03 : étape 3 de la purge, lecture du jeton de l'app bureau retirée — Summary

Chronos ne lit plus jamais le jeton de l'app bureau. Sont partis : les 7 types du jeton (fournisseur, portillon, lecteur,
coffre Windows, inventaire machine), le réglage `OAuthUsageEnabled` et la valeur `SourceUsage.EndpointOAuthClaude`. La
chaîne passe à 2 composites et `DiagnosticService` n'a plus de paramètre `tokenReader` ni `machine`. Le tout tient en un
commit réversible.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | Production : 7 fichiers supprimés, chaîne à 2 composites, nouvelle signature du diagnostic, réglage et valeur d'enum retirés | 7d48cfb |
| 2 | Tests : 6 fichiers supprimés, tous les `new DiagnosticService(` adaptés (y compris les 3 sites positionnels `null, etat, machine`) | 7d48cfb |
| 3 | Gardes (composite 3 → 2, non-retour étendue), tests journal et réglages hérités, docs §7, commit d'étape | 7d48cfb |

Un seul commit pour l'étape, comme le plan le prescrit.

## Ce qui a changé

- `App.xaml.cs` : les enregistrements du lecteur de jeton, du `ClaudeOAuthUsageProvider` et du portillon sont retirés.
  La chaîne devient `Composite(sonde, Composite(ChronosOAuth, ClaudeUsageObjectProvider))`, avec un commentaire mis à jour
  (« … → pont statusLine (retiré à l'étape suivante de la purge) »). Le diagnostic ne reçoit plus le lecteur de jeton.
  `moniteurSessions:`, `magasins:`, `reconstruction:` et `fuseau:` restent passés par nom.
- `DiagnosticService` : nouvelle signature conforme à `<interfaces>`. La XML-doc ne cite plus `tokenReader`, `machine`,
  les sondages d'environnement ni le « poll UIA ».
- `ChronosSettings` : `OAuthUsageEnabled` est retiré. La doc de `SondeEnTetesActivee` a été réécrite et ne s'y réfère plus.
- `SourceUsage` : « quatre membres », et la phrase « deux points de terminaison OAuth » est devenue « quelle source répond ».
  `LibelleSource` perd le bras correspondant.
- `RateLimitHeaderUsageProvider` (code et tests) : « motif GatedOAuthUsageProvider » est remplacé par « via SettingsService ».
- `ProtectedData` reste dans le .csproj, puisque `ChronosOAuthStore` l'utilise.
- `MainViewModel.cs` et `ReglagesBindingTests.cs` figuraient dans files_modified, mais le grep n'y a trouvé aucune mention
  résiduelle (elles avaient été nettoyées en 37-02). Ils n'ont donc pas été touchés.
- `V10TestVault` n'avait aucun autre consommateur : il est supprimé.
- `CompositionRootTests` : le miroir de la chaîne avait déjà 2 composites (sonde, (OAuth Chronos, faux)). Seul
  l'enregistrement de `IClaudeTokenReader` a été retiré.

## Vérification

- `dotnet build Chronos.sln -warnaserror --no-incremental` : 0 avertissement, 0 erreur.
- `dotnet test Chronos.sln` : 1717 / 1717 verts (contre 1751 avant). Il y a 3 fichiers de tests et 1 test d'indépendance
  OAuth en moins, et 2 tests en plus (journal, settings hérité).
- Greps d'acceptation :
  - `new CompositeUsageProvider(` = 2 dans App.xaml.cs, `PontStatusLine` = 1 dans SourceUsage.cs, `ProtectedData` ≥ 1.
  - Les types retirés n'apparaissent plus dans src.
  - Dans tests, il ne reste que trois choses : la garde de non-retour, la liste `TermesRetires`, et la liste « interdits »
    de `DiagnosticServiceTests:136` (une garde qui interdit `WindowsCredentialStore` dans la source du diagnostic,
    voulue).
  - `EndpointOAuthClaude` = 0 dans docs/data-sources.md.
- TDD, tâche 3 : `Une_source_retiree_EndpointOAuthClaude_est_ignoree_sans_exception` est vert dès sa première exécution,
  comme le plan l'annonçait : `Enum.TryParse` échoue déjà, la ligne est ignorée et comptée, et aucune modification de la
  production n'a été nécessaire. Le test épingle ce comportement.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Seuil anti-mutisme de `LibelleSourceTests` resté à 5 membres**
- **Found during:** Tâche 3 (suite complète)
- **Issue:** `Aucun_membre_de_l_enum_ne_retombe_sur_le_libelle_de_repli` exigeait `>= 5` membres. L'enum en a maintenant 4.
- **Fix:** seuil passé à `>= 4`, avec un commentaire « 3 après l'étape 5 ».
- **Files modified:** tests/Chronos.Tests/LibelleSourceTests.cs
- **Commit:** 7d48cfb

**2. [Rule 1 - Résidu] Deux commentaires de `RateLimitHeaderUsageProviderTests` citaient encore `GatedOAuthUsageProvider`**
- **Fix:** réécrits sans le nom, pour satisfaire la vérification du plan (seules les gardes peuvent citer les types retirés).
- **Commit:** 7d48cfb

Note : comme en 37-02, le script d'édition a écrit de vrais sauts de ligne à la place des `\n` dans le nouveau test du
journal (CS1010). Corrigé avant le commit.

Aucun stub.

## Self-Check: PASSED

- ABSENT (attendu) : les 7 fichiers source et les 6 fichiers de test supprimés
- FOUND : commit 7d48cfb
