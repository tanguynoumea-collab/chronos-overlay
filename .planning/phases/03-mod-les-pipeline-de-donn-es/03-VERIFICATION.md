---
phase: 03-mod-les-pipeline-de-donn-es
verified: 2026-07-08T15:05:26Z
status: passed
score: 4/4 must-haves verified
---

# Phase 3 : Modèle du pipeline de données — Rapport de vérification

**Objectif de la phase :** Un pipeline de données neutre produit des `UsageSnapshot` immuables — fiables depuis l'objet primaire ou estimés depuis les JSONL — entièrement isolé du cadran, sans aucun type WPF.
**Vérifié le :** 2026-07-08T15:05:26Z
**Statut :** passed
**Re-vérification :** Non — vérification initiale

## Réalisation de l'objectif

### Vérités observables

| # | Vérité | Statut | Preuve |
|---|--------|--------|--------|
| 1 | Provider composite : primaire si lisible, sinon estimé depuis JSONL | ✓ VÉRIFIÉ | `CompositeUsageProvider.Best()` sélectionne `Exact` (primaire) sinon `Estimated` (repli) sinon `Unavailable`, PAR fenêtre. 5 tests verts (`CompositeUsageProviderTests.cs`), enregistré en DI (`App.xaml.cs:50-52`) |
| 2 | Snapshot avec provenance (Exact/Estimated), utilization, resets_at, fraction temps restante des deux fenêtres | ✓ VÉRIFIÉ | `WindowState` porte `Reliability`, `Utilization`, `ResetsAt`, `FractionTimeRemaining` nullable-safe ; `UsageSnapshot` porte `FiveHour`/`SevenDay`/`SourceCapturedAt`/`Age`. Testé par `WindowStateTests` (6 cas dont Theory clamp) |
| 3 | Parsing tolérant (lignes/champs invalides, dernière ligne partielle) sans échec ni valeur inventée | ✓ VÉRIFIÉ | `ClaudeUsageObjectProvider` : catch ciblé (`IOException`/`JsonException`/`FileNotFoundException`/`DirectoryNotFoundException`) → `UsageSnapshot.Empty`. `JsonlEstimationProvider` : `catch (JsonException)` par ligne, `IsAssistant()` structurel (jamais de faux positif prose). Tests verts : `Corrompu_renvoie_Empty_sans_exception`, `Absent_renvoie_Empty_sans_exception`, `Tolerant_ignore_corrompue_partielle_prose_et_user_sans_exception`, `Dossier_absent_renvoie_zero_sans_exception` |
| 4 | Couche Services sans aucun type WPF (garde de pureté réflexive) | ✓ VÉRIFIÉ | `ServicesLayerPurityTests.La_couche_Services_ne_reference_aucun_assembly_WPF` — réflexion sur `Chronos.Services`/`Chronos.Models`, allow-list nominative documentée pour les 2 adaptateurs WPF de Phase 1 (`WpfUiDispatcher`, `TopmostGuard`). Test vert |

**Score :** 4/4 vérités vérifiées

### Artefacts requis

| Artefact | Attendu | Statut | Détails |
|----------|---------|--------|---------|
| `src/Chronos/Models/WindowState.cs` | Record immuable + FractionRemaining + Exhausted + Unavailable | ✓ VÉRIFIÉ | Contient `Math.Clamp(ratio, 0.0, 1.0)`, `Exhausted => Utilization is >= 1.0` |
| `src/Chronos/Models/UsageSnapshot.cs` | Record immuable deux fenêtres + Empty + SourceCapturedAt/Age | ✓ VÉRIFIÉ | `static UsageSnapshot Empty` présent |
| `src/Chronos/Services/IUsageProvider.cs` | Contrat neutre GetAsync + SnapshotChanged | ✓ VÉRIFIÉ | `event EventHandler<UsageSnapshot>? SnapshotChanged` présent |
| `src/Chronos/Services/IClock.cs` / `SystemClock.cs` | Horloge injectable | ✓ VÉRIFIÉ | `DateTimeOffset UtcNow` |
| `src/Chronos/Services/ChronosPaths.cs` | Chemins injectables via Environment | ✓ VÉRIFIÉ | `SpecialFolder.ApplicationData` + `SpecialFolder.UserProfile`, jamais `Assembly.Location` |
| `src/Chronos/Services/ClaudeUsageObjectProvider.cs` | Provider primaire Exact, lecture tolérante | ✓ VÉRIFIÉ | `FromUnixTimeSeconds`, `FileShare.ReadWrite`, `return UsageSnapshot.Empty` |
| `src/Chronos/Services/JsonlEstimationProvider.cs` | Repli Estimated, somme streaming tolérante | ✓ VÉRIFIÉ | `FileShare.ReadWrite`, `SearchOption.AllDirectories` (inclut `subagents/`, aucun filtre d'exclusion), `SourceReliability.Estimated`, `Utilization = null` |
| `src/Chronos/Services/CompositeUsageProvider.cs` | Bascule primaire→repli par fenêtre | ✓ VÉRIFIÉ | `Reliability == SourceReliability.Exact`, `SnapshotChanged?.Invoke` |
| `src/Chronos/App.xaml.cs` | Enregistrement DI Singleton du pipeline complet | ✓ VÉRIFIÉ | `AddSingleton<IUsageProvider>(sp => new CompositeUsageProvider(...))`, 4 lignes Phase 1 préservées |
| `tests/Chronos.Tests/ServicesLayerPurityTests.cs` | Garde réflexive WPF | ✓ VÉRIFIÉ | `PresentationFramework`, `typeof(Chronos.Services.IUsageProvider).Assembly` |
| `scripts/chronos-statusline-bridge.js` | Pont wrapper non destructif | ✓ VÉRIFIÉ | `renameSync` (atomique, avant `spawnSync`), re-émission stdout/stderr intacte |
| `scripts/install-bridge.mjs` | Installeur idempotent + backup + uninstall | ✓ VÉRIFIÉ | `settings.json`, `.chronos.bak`, `--uninstall`, message « deja installe » (idempotence) |
| `scripts/README.md` | Procédure manuelle install/désinstall | ✓ VÉRIFIÉ | Section pont, installation auto/manuelle, désinstallation, sécurité |

### Vérification des liens clés

| De | Vers | Via | Statut | Détails |
|----|------|-----|--------|---------|
| `WindowState.FractionRemaining` | usage par providers | `Math.Clamp` | ✓ WIRED | Appelé dans `ClaudeUsageObjectProvider.ReadWindow` |
| `ServicesLayerPurityTests` | `typeof(IUsageProvider).Assembly` | réflexion | ✓ WIRED | Test vert, fait partie de la suite permanente |
| `scripts/chronos-statusline-bridge.js` | `%APPDATA%/Chronos/usage.json` | `renameSync` avant `spawnSync` | ✓ WIRED | Ordre vérifié dans le fichier (ligne 61 avant ligne 69) |
| `ClaudeUsageObjectProvider` | `ChronosPaths.UsageFile` | `FileShare.ReadWrite` | ✓ WIRED | `FileStream(_paths.UsageFile, ..., FileShare.ReadWrite)` |
| `App.xaml.cs` | `IUsageProvider` | `CompositeUsageProvider(primary, fallback)` | ✓ WIRED | DI résout `ClaudeUsageObjectProvider` (primary) + `JsonlEstimationProvider` (fallback) |
| `JsonlEstimationProvider` | `ChronosPaths.ProjectsRoot` | `EnumerateFiles` récursif | ✓ WIRED | `SearchOption.AllDirectories`, test `Subagents_inclus_dans_la_somme_recursive` vert |
| `CompositeUsageProvider` | `WindowState.Best` | sélection Exact>Estimated>Unavailable | ✓ WIRED | `Reliability == SourceReliability.Exact` |
| `scripts/install-bridge.mjs` | `~/.claude/settings.json` | backup + réécriture idempotente | ✓ WIRED | Installé réellement : `statusLine.command` pointe sur le pont, `.chronos.bak` présent (vérifié sur le système) |

### Trace de flux de données (Niveau 4)

| Artefact | Variable de données | Source | Données réelles | Statut |
|----------|---------------------|--------|------------------|--------|
| `ClaudeUsageObjectProvider.GetAsync` | `five`/`week` (WindowState) | Lecture `usage.json` réel via `ChronosPaths.Default()` | Fichier réel présent sur le système, `five_hour: null, seven_day: null, capturedAt: <epoch>` — champs `null` car le compte de test n'a pas encore reçu de `rate_limits` (comportement documenté et attendu, pas une valeur inventée) | ✓ FLOWING (honnête : null propagé, pas de valeur inventée) |
| `JsonlEstimationProvider.GetAsync` | `five`/`week` (long) | `Directory.EnumerateFiles` sur `%USERPROFILE%\.claude\projects` réel | Requête réelle sur système de fichiers, pas de retour statique | ✓ FLOWING |
| `CompositeUsageProvider.GetAsync` | `FiveHour`/`SevenDay` | Combine les deux providers ci-dessus | Pas de valeur statique/hardcodée | ✓ FLOWING |

Note : le fichier `usage.json` réel actuellement sur le poste contient `five_hour: null, seven_day: null` — c'est le comportement HONNÊTE attendu (pas de souscription Pro/Max active ou pas encore de réponse API dans la session courante), documenté explicitement dans `03-HUMAN-UAT.md` § Gaps. Ce n'est pas un défaut du pipeline : le pont écrit fidèlement ce qu'il reçoit, sans jamais inventer de valeur.

### Vérifications comportementales (build + test)

| Comportement | Commande | Résultat | Statut |
|--------------|----------|----------|--------|
| Build solution | `dotnet build Chronos.sln -c Debug` | 0 avertissement, 0 erreur | ✓ PASS |
| Suite de tests complète | `dotnet test Chronos.sln -c Debug` | 27/27 réussis, 0 échec, 0 ignoré | ✓ PASS |
| Pont installé réellement | inspection `~/.claude/settings.json` | `statusLine.command` pointe sur `chronos-statusline-bridge.js`, `.chronos.bak` présent | ✓ PASS |
| usage.json matérialisé | inspection `%APPDATA%\Chronos\usage.json` | Fichier présent, structure conforme (`five_hour`/`seven_day`/`capturedAt`), valeurs null car pas de `rate_limits` réel capturé pour l'instant (attendu, tracké en HUMAN-UAT) | ✓ PASS (conforme au contrat, honnêteté préservée) |

### Couverture des exigences

| Exigence | Plan source | Description | Statut | Preuve |
|----------|-------------|--------------|--------|--------|
| DAT-02 | 03-01 | IUsageProvider isole les sources du cadran, couche Services sans WPF | ✓ SATISFAIT | `IUsageProvider.cs` neutre + `ServicesLayerPurityTests` vert |
| DAT-03 | 03-01 | Modèles UsageSnapshot/WindowState immuables, neutres | ✓ SATISFAIT | `WindowState.cs`/`UsageSnapshot.cs` + `WindowStateTests` vert |
| DAT-04 | 03-02, 03-04 | ClaudeUsageObjectProvider lit l'objet d'usage localisé (primaire) | ✓ SATISFAIT | `ClaudeUsageObjectProvider.cs` + tests verts + pont installé réellement sur le système (settings.json vérifié) |
| DAT-05 | 03-03 | JsonlEstimationProvider estime par somme de tokens JSONL, streaming FileShare.ReadWrite | ✓ SATISFAIT | `JsonlEstimationProvider.cs` + 4 tests verts incluant subagents |
| DAT-06 | 03-03 | CompositeUsageProvider tente primaire puis bascule sur repli | ✓ SATISFAIT | `CompositeUsageProvider.cs` + 5 tests verts |
| DAT-07 | 03-01 | FractionTimeRemaining des deux fenêtres calculé depuis ResetsAt | ✓ SATISFAIT | `WindowState.FractionRemaining` + Theory clamp vert |
| ROB-02 | 03-02, 03-03 | Parsing tolérant : lignes/champs invalides ignorés, dernière ligne JSONL partielle ignorée | ✓ SATISFAIT | Tests `Corrompu_renvoie_Empty_sans_exception`, `Tolerant_ignore_corrompue_partielle_prose_et_user_sans_exception`, etc. |

Aucune exigence orpheline détectée : les 7 IDs de REQUIREMENTS.md (Phase 3) sont tous déclarés dans les frontmatters des plans 03-01 à 03-04, et tous marqués `[x]` / "Complete" dans REQUIREMENTS.md.

### Anti-patterns détectés

| Fichier | Ligne | Pattern | Sévérité | Impact |
|---------|-------|---------|----------|--------|
| — | — | Aucun TODO/FIXME/placeholder/stub trouvé dans `src/Chronos/Models`, `src/Chronos/Services`, `scripts/` | — | — |

Aucun blocker ni warning détecté par le scan (grep TODO/FIXME/PLACEHOLDER/coming soon/not yet implemented).

### Vérification humaine requise

Aucun nouvel item. Les 3 items de vérification humaine (statusLine intacte en session réelle, usage.json rempli avec données réelles, rafraîchissement continu) sont déjà trackés dans `03-HUMAN-UAT.md` (statut `partial`, 3 pending) et ne sont pas redéclenchés ici — le pont est effectivement installé (backup vérifié sur le système), la vérification programmatique est verte, et le comportement observé (`usage.json` avec fenêtres `null`) est documenté comme attendu tant qu'aucune session Pro/Max réelle n'a produit de `rate_limits`.

### Résumé

Les 4 critères de succès de la phase sont vérifiés dans le code réel, pas seulement dans les SUMMARYs :
1. `CompositeUsageProvider` sélectionne bien primaire (Exact) ou repli (Estimated) par fenêtre, câblé en DI dans `App.xaml.cs`.
2. `UsageSnapshot`/`WindowState` portent provenance, utilization, resets_at, et fraction de temps restante, nullable-safe, jamais de valeur inventée.
3. Le parsing (fichier `usage.json` et JSONL) est tolérant à tous les niveaux testés : fichier absent/corrompu, fenêtre absente, champ manquant, ligne JSONL corrompue/partielle/prose — jamais d'exception, jamais de valeur inventée.
4. La garde de pureté réflexive (`ServicesLayerPurityTests`) est en place et verte, avec une allow-list documentée et restrictive pour les deux seuls adaptateurs WPF hérités de la Phase 1.

`dotnet build` et `dotnet test` passent intégralement (27/27). Le pont statusLine est réellement installé sur le système (backup présent, `statusLine.command` pointant sur le wrapper). Les 7 exigences de la phase (DAT-02 à DAT-07, ROB-02) sont toutes satisfaites et tracées sans orphelines. Phase 3 atteint son objectif.

---

*Vérifié le : 2026-07-08T15:05:26Z*
*Vérificateur : Claude (gsd-verifier)*
