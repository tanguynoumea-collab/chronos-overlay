# Quick 260927 — « Quitter Chronos » laisse un processus zombie (interblocage d'arrêt)

**Type :** correctif (FAST TRACK ZEUS) · **Demandé par l'utilisateur le 2026-09-27** · **Livrable :** code + tests + release 3.3.1

## Symptôme

Après « Quitter Chronos », les fenêtres se ferment mais le processus `Chronos-v*.exe` reste vivant indéfiniment, sans fenêtre.
Le mutex `Local\Chronos-overlay` reste tenu : une relance peut répondre « Chronos tourne déjà » sans cadran visible.
Très probablement la cause de l'écart E1-ter de 32-08 (trois anciens overlays « jamais quittés »).

## Cause (prouvée par reproduction jetable, scratchpad `repro-arret/`)

1. `App.OnStartup` fait `await _host.StartAsync()` sur le thread UI (contexte `DispatcherSynchronizationContext`).
2. `RefreshOrchestrator.ExecuteAsync` commence par `await Task.Yield()` : la reprise revient sur le Dispatcher, donc toute la
   boucle (`await foreach`, `Task.Delay`, `GetAsync`, `RunPeriodicAsync`) vit sur le thread UI.
3. `App.OnExit` bloque le thread UI : `_host.StopAsync().GetAwaiter().GetResult()`.
4. `RefreshOrchestrator.StopAsync` fait `await base.StopAsync(ct)` sans `ConfigureAwait(false)` : sa reprise attend le thread UI
   bloqué → interblocage, y compris après `HostOptions.ShutdownTimeout`. Repro : forme actuelle « BLOQUÉ > 10 s (délai 3 s) »,
   forme corrigée « arrêt terminé ».

## Correctif attendu (défense en profondeur, trois couches)

1. **Aucun service hébergé ne tourne sur le contexte UI.** `RefreshOrchestrator.ExecuteAsync` : remplacer `await Task.Yield()` par
   un départ explicite hors contexte (ex. `await Task.Run(() => BoucleAsync(stoppingToken), stoppingToken)` ou
   `await Task.Yield()` remplacé par `await Task.Delay(0).ConfigureAwait(false)` n'est PAS suffisant — utiliser `Task.Run`) ;
   `ConfigureAwait(false)` sur les `await` de la boucle et sur `await base.StopAsync(ct)`. Passer en revue TOUS les
   `IHostedService` / `BackgroundService` (RefreshOrchestrator, TokenRefreshService, JournalisationUsageProvider,
   ReconstructionTokens, et tout autre) : aucun `await` sans `ConfigureAwait(false)` dans leurs chemins Start/Execute/Stop.
   Le VM continue de recevoir `SnapshotChanged` hors UI et marshale déjà via `IUiDispatcher.Post` (BeginInvoke) : vérifier.
2. **L'arrêt ne peut plus bloquer indéfiniment.** Dans `App.OnExit` : arrêter le Host HORS du thread UI avec un délai borné
   (ex. `Task.Run(() => _host.StopAsync(cts.Token)).Wait(TimeSpan.FromSeconds(5))` avec `cts` annulé au même délai), puis
   `_host.Dispose()` protégé, puis `_verrou?.Liberer()` sur le thread UI (ReleaseMutex l'exige). Si le délai est dépassé :
   écrire une ligne dans le journal/diagnostic si possible, et garantir la terminaison (`Environment.Exit(0)` en dernier recours,
   APRÈS `Liberer()`).
3. **Test de non-régression qui aurait attrapé le bug** : démarrer le Host réel (ou un conteneur miroir avec
   `RefreshOrchestrator` et les autres services hébergés, providers factices) sous un `DispatcherSynchronizationContext` sur un
   thread STA (comme WPF), puis appeler le même chemin d'arrêt qu'`OnExit` et exiger la fin en < 5 s. Le test doit ROUGIR sur le
   code actuel (le vérifier avant le correctif — c'est la mutation de référence), puis verdir. Ajouter une garde textuelle :
   aucun `await Task.Yield()` seul en tête d'un `ExecuteAsync` et aucun `.GetAwaiter().GetResult()` sur `StopAsync` dans
   `App.xaml.cs`.

## Release 3.3.1 (procédure 32-07 / 35-06)

Bump 3.3.1 aux quatre propriétés du csproj, `dotnet publish` mono-fichier win-x64, contrôles (taille, 0 DLL, VersionInfo
3.3.1.0 / 3.3.1, md5), smoke `--hook SessionStart` stdin vide (code 0, `~/.claude/settings.json` md5 inchangé, aucun processus
résident), `Chronos-v3.3.1.exe` à la racine, commit de release du csproj seul, ni tag ni push. Mettre à jour le compte de tests
du README si une garde le fixe.

## Interdits

Ne jamais lancer un overlay ni `dotnet run` ; ne jamais tuer ni arrêter un processus Chronos (la 3.3.0 de l'utilisateur tourne) ;
aucune écriture sous `~/.claude`, `%APPDATA%\Claude`, `%APPDATA%\Chronos` ; aucun paquet NuGet nouveau ; zéro warning Debug et
Release ; suite complète deux fois ; commits en français avec `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
