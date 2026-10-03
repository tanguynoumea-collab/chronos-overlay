---
phase: 37-une-cha-ne-de-donn-es-claire
plan: 05
subsystem: réconciliation settings Claude, composition, diagnostic
tags: [purge-r5, statusline, reconciliateur, gardes]
requires: ["37-04"]
provides:
  - "IssueBarreStatut { Absente, Tierce, Retiree, Restauree } et BilanReconciliation(Ecrit, Barre, Sauvegarde, Cause)"
  - "ClaudeSettingsReconciler.RetirerBarreChronos (internal), ReconcileJson à 5 arguments (out barre), Reconcile(hooksWanted, commandeHeritee = null), DernierBilan"
  - "ClaudeSettingsReconciler.CommandeInterneHeritee (pur) et LireCommandeInterneHeritee (fichier)"
  - "RefreshOrchestrator(IUsageProvider, RefreshOptions) : plus de ChronosPaths ni de surveillance de fichier"
  - "DiagnosticService : paramètre optionnel final reglagesClaude, section [Réglages de Claude Code]"
  - "Chaîne à 1 composite : Journal(Composite(sonde, OAuth Chronos))"
affects: [37-06]
tech-stack:
  added: []
  patterns: ["bilan immuable posé à chaque sortie de Reconcile", "lecture brute d'une clé héritée avant tout Save"]
key-files:
  created: []
  deleted:
    - src/Chronos/Services/ClaudeUsageObjectProvider.cs
    - src/Chronos/Services/StatusLineBridge.cs
    - src/Chronos/Services/StatusLineInstaller.cs
    - src/Chronos/Services/IStatusLineSetup.cs
    - src/Chronos/Views/StatusLineSetup.cs
  modified:
    - src/Chronos/Services/ClaudeSettingsReconciler.cs
    - src/Chronos/Services/ClaudeSettingsJson.cs
    - tests/Chronos.Tests/ClaudeSettingsReconcilerTests.cs
    - src/Chronos/Services/ArgumentsDemarrage.cs
    - src/Chronos/App.xaml.cs
    - src/Chronos/Services/RefreshOrchestrator.cs
    - src/Chronos/Services/ChronosSettings.cs
    - src/Chronos/Services/ChronosPaths.cs
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/Views/Reglages/ReglagesWindow.xaml
    - src/Chronos/Models/SourceUsage.cs
    - src/Chronos/Text/LibelleSource.cs
    - src/Chronos/Services/DiagnosticService.cs
    - src/Chronos/Services/CompositeUsageProvider.cs
    - src/Chronos/Services/RateLimitHeaderUsageProvider.cs
decisions:
  - "Le nom de la clé héritée est composé en deux morceaux (CleCommandeHeritee = \"Inner\" + \"StatusLineCommand\") : le critère d'acceptation exige 0 occurrence du nom du membre retiré dans src, alors que le réconciliateur doit encore lire la clé"
  - "La ligne « Barre de statut actuelle » du diagnostic relit le fichier du réconciliateur injecté (SettingsPath) quand il l'est, sinon le chemin du profil : le test de 37-06 sur réconciliateur témoin lira ainsi le fichier temporaire, pas le vrai"
  - "RetirerBarreChronos : statusLine présente mais non objet → Tierce, sans y toucher"
  - "Reconcile distingue « illisible » de « conforme » par l'issue rendue par le cœur (barre == null ⇒ ParseOrNull a échoué), sans second parsing"
metrics:
  duration: "≈ 25 min"
  completed: 2026-10-03
  tasks: 2
  files: 20
requirements: [DAT-02, DAT-03]
---

# Phase 37 Plan 05 : étape 5 de la purge, partie production (barre statusLine retirée, pont supprimé, un seul composite) — Summary

Au premier lancement de la 3.5, le réconciliateur sauvegarde `~/.claude/settings.json`, puis retire la barre de statut
Chronos, quels que soient son chemin et sa version. Si l'ancienne barre de l'utilisateur est connue, il la restaure. Une
barre tierce reste intacte. Les hooks sont repointés dans la même écriture, et un bilan immuable est exposé. Le pont, son
installeur, la proposition au premier lancement, la carte des réglages, `ClaudeUsageObjectProvider`, la surveillance de
`usage.json`, le mode `--statusline` et `SourceUsage.PontStatusLine` sont supprimés. La chaîne ne compte plus qu'un
composite : sonde → OAuth Chronos.

**AUCUN COMMIT DE CODE dans ce plan** (conformément au plan) : tout reste dans l'arbre de travail pour le commit unique de
l'étape 5, fait par 37-06.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | Réconciliateur : retrait/restauration, bilan, lecture héritée, tests sur fichiers témoins (RED puis GREEN) | non commité (37-06) |
| 2 | Production : 5 fichiers supprimés, chaîne à 1 composite, démarrage réordonné, section de bilan du diagnostic | non commité (37-06) |

## Fichiers laissés non commités pour 37-06 (liste exacte, `git status --short`)

Suppressions déjà indexées par `git rm` :
- `D  src/Chronos/Services/ClaudeUsageObjectProvider.cs`
- `D  src/Chronos/Services/IStatusLineSetup.cs`
- `D  src/Chronos/Services/StatusLineBridge.cs`
- `D  src/Chronos/Services/StatusLineInstaller.cs`
- `D  src/Chronos/Views/StatusLineSetup.cs`

Modifications non indexées :
- ` M src/Chronos/App.xaml.cs`
- ` M src/Chronos/Models/SourceUsage.cs`
- ` M src/Chronos/Services/ArgumentsDemarrage.cs`
- ` M src/Chronos/Services/ChronosPaths.cs`
- ` M src/Chronos/Services/ChronosSettings.cs`
- ` M src/Chronos/Services/ClaudeSettingsJson.cs`
- ` M src/Chronos/Services/ClaudeSettingsReconciler.cs`
- ` M src/Chronos/Services/CompositeUsageProvider.cs`
- ` M src/Chronos/Services/DiagnosticService.cs`
- ` M src/Chronos/Services/RateLimitHeaderUsageProvider.cs`
- ` M src/Chronos/Services/RefreshOrchestrator.cs`
- ` M src/Chronos/Text/LibelleSource.cs`
- ` M src/Chronos/ViewModels/MainViewModel.cs`
- ` M src/Chronos/Views/Reglages/ReglagesWindow.xaml`
- ` M tests/Chronos.Tests/ClaudeSettingsReconcilerTests.cs`

Au total, 20 fichiers, soit exactement le `files_modified` du plan. Aucun autre fichier n'est touché.

## Ce qui a changé

- **Réconciliateur** (`ClaudeSettingsReconciler.cs`) :
  - `ApplyStatusLine` est remplacé par `RetirerBarreChronos(root, commandeHeritee)`. La sémantique reprend celle de
    l'ancien `TransformForUninstall`, avec deux protections en plus : une commande héritée qui est elle-même une
    commande Chronos n'est jamais restaurée, et une `statusLine` non objet reste intacte.
  - `ReconcileJson` à 5 arguments (`out IssueBarreStatut? barre`, qui vaut `null` si le fichier est inexploitable). La
    surcharge à 3 arguments délègue.
  - `Sauvegarder` rend désormais le chemin de la sauvegarde, ou `null`.
  - `Reconcile(hooksWanted, commandeHeritee = null)` pose `DernierBilan` à chaque sortie : « fichier absent »,
    « illisible — rien écrit », conforme, « sauvegarde impossible — rien écrit », écrit, ou le nom du type d'exception.
  - `CommandeInterneHeritee` est pur : recherche de la clé insensible à la casse, seulement une chaîne non vide.
    `LireCommandeInterneHeritee` rend `null` sur toute exception.
  - Doc de classe réécrite : le réconciliateur ne pose ni ne repointe jamais de barre, et `OfferOnFirstRun` n'y figure
    plus.
- **`ClaudeSettingsJson`** : `StatusLineMarker` est conservé et documenté comme « marqueur des anciennes barres, à
  retirer ».
- **`ArgumentsDemarrage`** : la constante, la ligne de `ModesConnus` (il reste 4 modes), la ligne de `Trier` et
  `ModeDemarrage.StatusLine` sont supprimées. `--statusline` donne donc `ArgumentInconnu`. Combiné à `--hook X`, il donne
  `Hook("X")`.
- **`App.xaml.cs`** :
  - le `case` et `RunStatusLineBridge` sont supprimés, ainsi que le DI du pont et de l'installeur et
    `ClaudeUsageObjectProvider` ;
  - la chaîne est `Composite(sonde, ChronosOAuth)`, soit 1 `new CompositeUsageProvider(` ;
  - le diagnostic reçoit `reglagesClaude:` ;
  - l'ordre de démarrage est `Build` → `LireCommandeInterneHeritee` (l. 106) → `window.Show()` (l. 123) →
    `.Reconcile(…, commandeHeritee)` (l. 134) → `LogStartupAsync()` (l. 141) ;
  - le bloc `OfferOnFirstRun` est supprimé et les commentaires ne citent plus que `--hook`.
- **`ChronosSettings`** : `InnerStatusLineCommand` et `StatusLinePromptDismissed` sont retirés.
- **`ChronosPaths`** : `UsageFile` est conservé. Sa doc indique désormais qu'il n'est plus écrit et qu'il sert d'ancre
  au dossier.
- **`MainViewModel`** : le champ, le paramètre positionnel `IStatusLineSetup statusLineSetup` (qui était en 8e
  position), `_isStatusLineSourceEnabled` et `ToggleStatusLineSource` sont retirés.
- **`ReglagesWindow.xaml`** : la carte « Barre de statut de Claude Code » est retirée. La section se termine maintenant
  sur la carte « Sonde d'en-têtes ».
- **`SourceUsage`** : il reste trois membres. Le bras correspondant de `LibelleSource` est retiré.
- **`DiagnosticService`** : nouveau paramètre optionnel final `ClaudeSettingsReconciler? reglagesClaude = null`, et une
  section `[Réglages de Claude Code]` après la section widget. Elle contient deux lignes : « Ce lancement : … »
  (`LibelleBilan`) et « Barre de statut actuelle : absente | tierce (laissée intacte) | Chronos (retirée au prochain
  lancement) ».
- **Commentaires** : `CompositeUsageProvider` (imbrication) et `RateLimitHeaderUsageProvider` (« seule source qui
  interroge le serveur à cadence fixe »).

## Retrait du paramètre ChronosPaths de RefreshOrchestrator (côté production)

- Le constructeur devient `RefreshOrchestrator(IUsageProvider provider, RefreshOptions options)`.
- Sont retirés : `_paths`, `_watcher`, `CreateWatcher`, `Trigger`, `OnError`, `RecreateWatcher` (seam interne), l'appel
  `CreateWatcher()` au début de la boucle, le `Dispose` du watcher dans `StopAsync`, et `using System.IO`.
- La doc de classe ne parle plus de surveillance de fichier. Le commentaire du seam `TryTrigger` parle du minuteur.
- `RefreshOptions.Debounce` reste en place (coalescence).
- L'enregistrement DI `services.AddSingleton<RefreshOrchestrator>()` n'a pas changé : la DI résout seule le nouveau
  constructeur.
- Effet de bord à connaître : le watcher faisait un `Directory.CreateDirectory(%APPDATA%\Chronos)` au démarrage, qui
  n'existe plus. Les écrivains (settings, last-exact, journal, sessions) créent eux-mêmes leur dossier.
- **Pour 37-06** : les sites de test `new RefreshOrchestrator(…, paths, …)` ne compilent plus. Il faut aussi supprimer
  `Ecriture_usage_json_declenche_GetAsync` et `Error_du_watcher_entraine_recreation_sans_perdre_le_refresh` (qui
  utilisait `RecreateWatcher`).

## Vérification

- `dotnet test --filter ClaudeSettingsReconcilerTests` : 41 / 41 verts. La phase RED a été constatée avant
  l'implémentation : erreurs CS1739 et CS0103 sur `commandeHeritee` et `IssueBarreStatut`.
- `dotnet build src/Chronos/Chronos.csproj -warnaserror` : 0 avertissement, 0 erreur.
- Greps d'acceptation, tous conformes :
  - Tâche 1 :
    - `StatusLineInstaller|ApplyStatusLine` = 0 dans le réconciliateur ;
    - `enum IssueBarreStatut` = 1 ;
    - `DernierBilan` = 9 ;
    - `public static string? LireCommandeInterneHeritee` = 1 ;
    - `StatusLineMarker` = 1 dans `ClaudeSettingsJson` ;
    - `Retire_la_statusLine_Chronos_perimee` = 1 ;
    - `.claude` = 0 dans le fichier de tests (la seule mention, dans la doc de classe, a été reformulée) ;
    - constructeur à moins de trois arguments = 0 ;
    - `Assert.StartsWith(Path.GetTempPath(), reconciler.BackupDir)` = 5.
  - Tâche 2 :
    - les 13 noms retirés = 0 ligne dans src ;
    - `new CompositeUsageProvider(` = 1 ;
    - `"--statusline"` = 0 dans `ArgumentsDemarrage` ;
    - `FileSystemWatcher|ChronosPaths` = 0 dans `RefreshOrchestrator` ;
    - ordre des lignes dans App : 106 < 123 < 134 < 141 ;
    - `[Réglages de Claude Code]` = 1 et `pont statusLine` = 0 dans `DiagnosticService` ;
    - `UsageFile` ≥ 1 dans `ChronosPaths`.
- Sécurité : l'exe n'a pas été lancé. Chaque nouveau test d'E/S construit le réconciliateur avec les trois chemins
  temporaires, avec les deux `Assert.StartsWith` et un nettoyage en `finally`.
- Attendu à ce stade : le projet de tests ne compile plus (sites de construction de `RefreshOrchestrator` et de
  `MainViewModel`, tests des types supprimés). C'est le travail de 37-06.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Bloquant] Clé héritée lue sans écrire le nom du membre retiré**
- **Trouvé pendant :** tâche 2 (grep d'acceptation)
- **Problème :** le critère « 0 ligne `InnerStatusLineCommand` dans src » contredisait le contrat de
  `CommandeInterneHeritee`, qui doit lire cette clé.
- **Correctif :** `private const string CleCommandeHeritee = "Inner" + "StatusLineCommand";`. Le XML-doc justifie le
  découpage, sur le même principe que la doctrine de la phase 19 (un commentaire ne reproduit pas le nom compté par une
  garde). Le commentaire d'App dit « clé héritée des réglages ≤ 3.4 ».
- **Fichiers :** `ClaudeSettingsReconciler.cs`, `App.xaml.cs`

**2. [Rule 2 - Correction] La ligne « Barre de statut actuelle » lit le fichier du réconciliateur injecté**
- **Problème :** relire le chemin du profil codé en dur dans la section aurait fait lire le VRAI settings.json par le
  futur test de 37-06, même avec un réconciliateur témoin.
- **Correctif :** `BarreActuelle(_reglagesClaude?.SettingsPath ?? claudeSettings)`.
- **Fichier :** `DiagnosticService.cs`

**3. [Mise en forme] `[Réglages de Claude Code]` cité une seule fois**
- Le XML-doc du paramètre écrit le nom de la section sans crochets, pour que le grep compte exactement 1.

Tests en plus de la liste du plan, pour la même couverture :
- `Sans_statusLine_l_issue_est_absente` ;
- `LireCommandeInterneHeritee_fichier_absent_rend_null` ;
- des cas supplémentaires dans le `[Theory]`.

Les tests migrés s'appellent `Retrait_*` (et non `Uninstall_*`).

## Pour 37-06 — mentions restantes hors périmètre de ce plan

Ces commentaires ne sont pas dans la liste `files_modified` de 37-05. Je les ai laissés tels quels ; il faut décider s'ils
entrent dans le commit de l'étape 5 :
- `src/Chronos/Services/VerrouInstanceUnique.cs:49` : « APRÈS les court-circuits `--statusline`, `--hook`, … » (périmé) ;
- `src/Chronos/Services/UsageNormalization.cs` l. 13, 30, 55, 94 : « pont statusLine », « usage.json » (historique des
  unités) ;
- `src/Chronos/Services/SessionHookInstaller.cs:28` : « (comme pour statusLine) » ;
- `IEtatMagasin.cs:5` et `LastExactStore.cs:35` : « usage.json figé », un rappel historique légitime.

Aucune de ces mentions n'est couverte par les greps du plan, et aucune n'est sous `GardeDocumentationChaineTests` (qui
porte sur README, CLAUDE.md et data-sources).

## Known Stubs

Aucun.

## Self-Check: PASSED

- ABSENT (attendu) : les 5 fichiers source supprimés (`git rm`, indexés)
- FOUND : `ClaudeSettingsReconciler.cs` (`IssueBarreStatut`, `DernierBilan`, `LireCommandeInterneHeritee`)
- FOUND : `App.xaml.cs` (`LireCommandeInterneHeritee`, 1 composite) ; `DiagnosticService.cs` (`[Réglages de Claude Code]`)
- Aucun commit de code depuis `217f76b docs(37): avancement — 37-04 fait`
