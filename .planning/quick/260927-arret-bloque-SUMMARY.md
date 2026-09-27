---
quick: 260927-arret-bloque
type: correctif
subsystem: cycle de vie (App.OnExit, services hébergés)
tags: [arret, interblocage, dispatcher, zombie, release, 3.3.1]
key-files:
  created:
    - src/Chronos/Services/ArretHote.cs
    - tests/Chronos.Tests/ArretHoteTests.cs
  modified:
    - src/Chronos/App.xaml.cs
    - src/Chronos/Services/RefreshOrchestrator.cs
    - src/Chronos/Services/TokenRefreshService.cs
    - src/Chronos/Chronos.csproj
commits:
  - 191152e test RED
  - 00fcfd5 fix GREEN
  - 8723e2a release 3.3.1
completed: 2026-09-27
---

# Quick 260927 : « Quitter Chronos » ne laisse plus de processus zombie (3.3.1)

**En bref :** l'arrêt du Host tourne maintenant hors du thread UI, avec un délai borné. Aucun service hébergé ne tourne plus
sur le Dispatcher. Six tests de non-régression, rouges sur le code 3.3.0, passent au vert. La 3.3.1 est publiée
(`Chronos-v3.3.1.exe`, md5 `c9d4c0bb71ef9b4b617f865270995712`) dans le commit de release `8723e2a`.

## Cause

1. `App.OnStartup` lance `await _host.StartAsync()` sur le thread UI, sous `DispatcherSynchronizationContext`.
2. `RefreshOrchestrator.ExecuteAsync` commençait par `await Task.Yield()`. La reprise revenait sur le Dispatcher, donc
   toute la boucle (channel, `Task.Delay`, `GetAsync`, minuteur périodique) tournait sur le thread UI.
3. `App.OnExit` bloquait ce même thread avec `_host.StopAsync().GetAwaiter().GetResult()`.
4. Dans `RefreshOrchestrator.StopAsync`, `await base.StopAsync(ct)` n'avait pas de `ConfigureAwait(false)`. La boucle ne
   pouvait pas se terminer et la reprise de l'arrêt attendait le thread UI, qui était bloqué. C'était un interblocage
   sans fin : les fenêtres étaient fermées mais le processus restait vivant et gardait le mutex `Local\Chronos-overlay`.

## Correctif, couche par couche

| Couche | Changement |
|---|---|
| 1. Services | `RefreshOrchestrator.ExecuteAsync` = `Task.Run(() => BoucleAsync(ct), CancellationToken.None)`, sans contexte capturé ni `await`. `ConfigureAwait(false)` sur tous les `await` de la boucle (`await foreach … .ConfigureAwait(false)`, `Task.Delay`, `GetAsync`, `WaitForNextTickAsync`) et sur `await base.StopAsync(ct)`. `TokenRefreshService.TickAsync` passe aussi en `ConfigureAwait(false)`. `JournalisationUsageProvider` (Start et Stop synchrones) et `ReconstructionTokens` (thread dédié, `ExecuteAsync` sans `await`) ont été vérifiés et sont sains. Le VM continue de marshaller `SnapshotChanged` via `IUiDispatcher.Post` (vérifié, `MainViewModel.cs:450`). |
| 2. Arrêt | Nouveau `ArretHote.Arreter(host, delai, out cause)` : `StopAsync(cts.Token)` puis `Dispose()`, chacun lancé par `Task.Run` (hors thread appelant) et attendu au plus 5 s. Le jeton est annulé au même délai. La méthode ne lève jamais et renvoie la cause d'un dépassement. `OnExit` relit le dossier du log avant l'arrêt, puis appelle `ArretHote`. Si le délai est dépassé, il ajoute une ligne best-effort dans `chronos.log`. Ensuite `_verrou?.Liberer()` sur le thread UI, `base.OnExit`, et `Environment.Exit` en dernier recours, après la libération du mutex. |
| 3. Tests | `ArretHoteTests` (6 tests) : Host réel (`Host.CreateApplicationBuilder`, comme l'app) avec les quatre services hébergés de production, dans l'ordre d'App, providers factices, chemins temporaires. Il est démarré par `await` sur un thread STA qui fait tourner un Dispatcher sous `DispatcherSynchronizationContext`. Chaque scénario est limité à 10 s : un interblocage produit un échec, la suite ne gèle jamais. |

Les six tests :
- `Le_chemin_d_arret_d_OnExit_se_termine_sous_le_contexte_du_Dispatcher` : arrêt propre en moins de 2 s.
- `Les_services_heberges_s_arretent_meme_si_le_thread_UI_est_bloque_sur_StopAsync` : rejoue volontairement la forme
  3.3.0 d'OnExit.
- `L_arret_pendant_un_GetAsync_en_vol_ne_reprend_jamais_sur_le_thread_UI` : le provider termine sur le pool 200 ms
  après l'annulation. La reprise de l'arrêt est donc toujours une vraie reprise, quelle que soit la course.
- `SnapshotChanged_est_emis_hors_du_thread_UI`.
- `L_arret_rend_la_main_dans_le_delai_meme_si_un_service_ne_s_arrete_jamais` : un service ignore l'annulation. Le
  test exige `false`, une cause renseignée et moins de 3 s pour un délai de 0,5 s.
- `Garde_aucun_ExecuteAsync_ne_commence_par_Task_Yield_et_aucun_arret_bloquant_sur_le_contexte` : garde textuelle sur
  `src/Chronos`. Elle interdit `await Task.Yield()` en tête d'un `ExecuteAsync` et tout `StopAsync(…).GetAwaiter().GetResult()`.
  Elle exige `ConfigureAwait(false)` sur chaque `await` de `RefreshOrchestrator.cs` et `ArretHote.Arreter(` dans `App.xaml.cs`.

## Preuve RED puis GREEN

- **RED (`191152e`)** : `ArretHote` est d'abord extrait sans changer son comportement (même `GetAwaiter().GetResult()`).
  Résultat : 5 échecs sur 5. Le chemin OnExit est « BLOQUÉ au-delà de 10 s », la forme 3.3.0 est bloquée, l'arrêt est
  bloqué avant `SnapshotChanged`, l'arrêt n'est pas borné face au service récalcitrant, et la garde signale l'arrêt
  bloquant dans `ArretHote.cs`. Le 6e test (GetAsync en vol) a été ajouté pendant le GREEN. Rejoué sur les sources
  exactes du RED (copie des fichiers de `191152e`), il donne **6 échecs sur 6** en 50 s, chacun limité à 10 s.
- **GREEN (`00fcfd5`)** : 6 réussites sur 6 en environ 1 s.

## Mutations (jouées par copie, puis annulées)

| Mutation | Tests rouges |
|---|---|
| A : `ExecuteAsync` revient à `await Task.Yield(); await BoucleAsync(ct);` | **4 sur 6** : chemin OnExit (arrêt borné, mais non propre à 5 s), forme 3.3.0 bloquée, `SnapshotChanged` émis sur le thread UI, garde. Seul le test du service récalcitrant reste vert, comme attendu (il porte sur la couche 2). |
| B : `ConfigureAwait(false)` retiré de `await base.StopAsync(ct)` | **2 sur 6** : GetAsync en vol (interblocage à 10 s) et garde. La première version du test « forme 3.3.0 » ne détectait pas cette mutation : la boucle se termine parfois en ligne dans `Cancel()`, c'est une course. Le test « GetAsync en vol » a été ajouté pour la détecter de façon déterministe. |
| C : `ArretHote` revient à l'attente synchrone non bornée | **2 sur 6** : service récalcitrant (bloqué au-delà de 10 s) et garde. |

Après chaque mutation, les sources restaurées ont été vérifiées : `git diff` n'affiche plus que le correctif GREEN.

## Chiffres

- Suite : **1654 réussis, 0 échec** (1648 + 6). Deux exécutions consécutives avant le bump (après le GREEN), puis deux
  après le bump 3.3.1.
- `dotnet build Chronos.sln -c Release` : **0 avertissement, 0 erreur** avant et après le bump. Build Debug : 0 avertissement.
- Le README indique « plus de 1 600 tests » et reste exact, donc aucune modification.

## Release 3.3.1

| Contrôle | Résultat |
|---|---|
| Bump | `Version` 3.3.1, `FileVersion` 3.3.1.0, `AssemblyVersion` 3.3.1.0, `InformationalVersion` 3.3.1 (`VersionPublieeTests` au vert) |
| `dotnet publish src/Chronos/Chronos.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true` | code 0 (19:06:56Z → 19:07:34Z) |
| Sortie `publish/` | `Chronos.exe` **78 111 106 o** et `Chronos.pdb` 306 204 o, **0 DLL** (3.3.0 : 78 104 918 o) |
| VersionInfo (sortie et copie) | **3.3.1.0 / 3.3.1 / Chronos** |
| md5 | **`c9d4c0bb71ef9b4b617f865270995712`**, identique pour la sortie et `Chronos-v3.3.1.exe`, différent de la 3.3.0 (`7ec9fbcf95c5716e6b5bbce77ea9c0bc`) |
| Copie | `Chronos-v3.3.1.exe` à la racine (absente avant), ignorée par `.gitignore:22:/Chronos-v*.exe`. Anciens exe 3.1.0 à 3.3.0 intacts |
| Smoke `./Chronos-v3.3.1.exe --hook SessionStart < /dev/null` | **code 0** (19:07:54Z → 19:07:58Z). `~/.claude/settings.json` md5 `7ba368b23e494ab15d01100e61f34b10` identique avant et après (3 384 o, date inchangée). **0 processus Chronos résident** ensuite |
| Commit de release | **`8723e2a`**, csproj seul (4 lignes ajoutées, 4 supprimées), ni tag ni push |

Remarque : au moment de la release, `tasklist` ne montrait **aucun** processus Chronos, même pas le PID 97288 annoncé
pour la 3.3.0. L'utilisateur l'avait donc déjà fermé ou terminé. Aucun processus n'a été arrêté par l'agent.

## Écarts par rapport au plan

1. **[Règle 2 - test manquant] Test « GetAsync en vol » ajouté.** La mutation B (ConfigureAwait retiré) n'était pas
   détectée de façon fiable par le test de la forme 3.3.0, à cause d'une course de fin en ligne. Le nouveau test est
   déterministe et rouge sur le code 3.3.0.
2. **Extraction `ArretHote` dans le commit RED.** C'est une refactorisation sans changement de comportement, faite pour
   que le test passe par le même chemin qu'`OnExit` sans instancier `Application`. Le GREEN n'a modifié que son corps.
3. **Borne sur les deux étapes** (arrêt puis libération, 5 s chacune). Le plan ne bornait que l'arrêt, mais un `Dispose`
   bloqué aurait reproduit le zombie. Le pire cas est donc d'environ 10 s avant `Environment.Exit`.
4. Deux commentaires reformulés pour ne pas contenir le motif interdit par la garde textuelle.

## Stubs

Aucun.

## Marche à suivre pour l'utilisateur

1. **Fermer la 3.3.0** : clic droit sur le cadran, réglages, « Quitter Chronos ». La 3.3.0 a le défaut corrigé ici. Si
   son processus reste visible (`Chronos-v3.3.0.exe` dans le Gestionnaire des tâches, onglet Détails), faire
   **« Fin de tâche »** dessus. Sans cela, le mutex reste tenu et la 3.3.1 répondrait « Chronos tourne déjà ».
   (Au moment de la release, aucun processus Chronos ne tournait.)
2. **Lancer la 3.3.1 depuis l'Explorateur** : double-clic sur
   `C:\Users\Tanguy\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.3.1.exe`.
3. Plus tard, « Quitter Chronos » sur la 3.3.1 doit faire disparaître le processus en moins d'une seconde. Si un arrêt
   dépasse un jour son délai, la sortie est forcée au bout de 10 s au maximum, et une ligne « arrêt dépassé : … — sortie
   forcée » est ajoutée à `%APPDATA%\Chronos\chronos.log`.

## Self-Check: PASSED

- FOUND : `src/Chronos/Services/ArretHote.cs`, `tests/Chronos.Tests/ArretHoteTests.cs`, `Chronos-v3.3.1.exe` (md5 `c9d4c0bb…`)
- FOUND : commits `191152e` (RED), `00fcfd5` (GREEN), `8723e2a` (release)
