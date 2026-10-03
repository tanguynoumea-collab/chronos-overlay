# Fiabilité runtime (Tier 2) — dev-team-council, cycle ZEUS 2 (2026-10-03)

Périmètre : `src/Chronos` à HEAD `244263f` (cycles 1 et 2), focalisé sur threading WPF/Dispatcher, arrêt du Host,
reconstruction de fond, écritures de fichiers (journal, settings, `~/.claude/settings.json`), répartiteur de gestes.
Lecture seule ; l'exe n'a jamais été lancé.

## Phase 1 — Vérité-terrain

**Build** : `dotnet build src/Chronos/Chronos.csproj -c Debug --no-incremental -p:AnalysisMode=All` (sortie vers le
scratchpad) → **0 erreur, 332 avertissements uniques** (664 lignes, doublées par la passe wpftmp).

| Règle | Nb | Lecture |
|---|---|---|
| CA1031 (catch général) | 116 | Le gros est du best-effort voulu et commenté. Les cas qui posent problème sont traités en Phase 2 (FIAB-3). |
| CA2007 (ConfigureAwait) | 36 | Peu pertinent en contexte UI ; le chemin critique (RefreshOrchestrator) le pose déjà. |
| CA1849 (appel sync dans async) | 5 | DiagnosticService 115/133/332/403, TokenRefreshService 80 (FIAB-10). |
| CA2000 (non disposé) | 4 | LecteurJournal:50, LecteurAgregats:76, LecteurTranscript:80, TranscriptActivityProvider:67 : **faux positifs**, le flux est pris dans un `using` juste après. |
| CA1001 | 1 | SourceActiviteMemoisee:17 (SemaphoreSlim d'un singleton) : sans effet. |
| CA2016 | 1 | JournalisationUsageProvider:84 (`Task.Run` sans jeton). |
| CA1508 | 1 | ReconstructionTokens:336 (code mort). |
| CS1998 / CS4014 / CA1063 / CA1816 | 0 | — |

**Motifs à risque (grep)** :
- `.Result` / `.GetAwaiter().GetResult()` : 0. `.Wait(` : 1, `ArretHote.cs:51`, sur une tâche du pool, borné à 5 s. Correct.
- `async void` : 1, `App.xaml.cs:21` (`OnStartup`), voir FIAB-1.
- `Dispatcher.Invoke` synchrone : 0. Tout passe par `WpfUiDispatcher.Post` (BeginInvoke, non bloquant).
- `DispatcherUnhandledException` / `AppDomain.UnhandledException` / `TaskScheduler.UnobservedTaskException` : **0 occurrence** dans src.
- `catch { }` vides : 31 lignes. Les cas qui posent problème sont dans FIAB-3.

**Ce qui tient (vérifié dans le code)** : l'arrêt du Host part hors du thread UI, chaque étape est bornée, `Environment.Exit` sert de dernier recours après libération du mutex (`ArretHote.cs:30-63`, `App.xaml.cs:211-229`). La boucle de données tourne sur le pool avec `ConfigureAwait(false)` partout (`RefreshOrchestrator.cs:41-61`). La reconstruction a un thread dédié, protégé par un catch à chaque niveau, annulable entre fichiers (`ReconstructionTokens.cs:128-257`). Le journal écrit en ajout exclusif avec reprises bornées et relecture sous le même verrou (`JournalReleves.cs:219-248`). La réconciliation de `~/.claude/settings.json` n'écrit qu'après une sauvegarde réussie et abandonne si le fichier est illisible (`ClaudeSettingsReconciler.cs:213-255`). Les appels HTTP ont un délai (8 s / 15 s), un recul exponentiel et une gestion des 429/401 sans fausse déconnexion. L'automate de geste est pur, réentrant face à DragMove et tolérant à la perte de capture (`AutomateGeste.cs`).

---

## Phase 2 — Findings

### FIAB-1 — Aucun filet global d'exception, `OnStartup` en `async void`
- **Sévérité** : Majeur
- **Rôle émetteur** : Fiabilité (Tier 2)
- **Localisation** : `src/Chronos/App.xaml.cs:21` (et absence globale)
- **Preuve** : OUTIL — `grep -rnE "DispatcherUnhandledException|UnhandledException|UnobservedTaskException" src` → 0 résultat. CITATION — `protected override async void OnStartup(StartupEventArgs e)`. Après `await _host.StartAsync();` (l.114), les appels `GetRequiredService<MainWindow>()`, `window.Show()` (et donc `SourceInitialized` → `RestorePlacement`) ainsi que `ShowIfEnabled()` (l.153) ne sont protégés par aucun try.
- **Constat** : toute exception sur le thread UI fait tomber le processus : handler d'événement, `DispatcherTimer.Tick`, WndProc hook, `Closing`, `RelayCommand`, suite de l'`async void`. Sous .NET 8, cela veut dire une terminaison sans boîte de dialogue ni ligne dans `chronos.log`. La seule trace est l'Observateur d'événements de Windows.
- **Impact** : le cadran disparaît sans explication. L'utilisateur ne peut pas distinguer un crash d'un arrêt voulu, et le diagnostic intégré n'en garde aucune trace. Cela amplifie FIAB-2 : chaque `Save` qui lève devient un crash.
- **Recommandation** : brancher `DispatcherUnhandledException` (écrire une ligne datée dans `chronos.log` en ajout, puis `Handled = true` pour les exceptions non fatales), `AppDomain.CurrentDomain.UnhandledException` (journaliser) et `TaskScheduler.UnobservedTaskException`. Entourer d'un try/catch journalisé la suite de `OnStartup` qui vient après `await`.
- **Applicabilité (desktop)** : pleine. Overlay résident lancé au démarrage de session, sans console.
- **Statut challenge** : non contesté

### FIAB-2 — `SettingsService.Save` lève et est appelé non protégé sur des chemins UI chauds (dont un par `LocationChanged`)
- **Sévérité** : Majeur
- **Rôle émetteur** : Fiabilité (Tier 2)
- **Localisation** : `src/Chronos/Services/SettingsService.cs:155-166` ; appelants : `Views/MainWindow.xaml.cs:123` (après DragMove) → `Services/OverlayController.cs:104` ; `OverlayController.cs:202` (WM_DISPLAYCHANGE, dans le hook WndProc) ; `MainWindow.xaml.cs:62` (DpiChanged) ; `OverlayController.cs:179/219` (RestorePlacement → SendToBackground, pendant `window.Show()`) ; `Views/SessionsController.cs:109,118,122` ; `Views/Historique/HistoriqueWindow.xaml.cs:85` et `Views/Reglages/ReglagesWindow.xaml.cs:67` (Closing) ; `ViewModels/MainViewModel.cs:323,342,357,384,418,821,834` (commandes).
- **Preuve** : CITATION — `File.WriteAllText(tmp, json); File.Move(tmp, _paths.SettingsFile, overwrite: true);` sans try. `_window.LocationChanged += (_, _) => PersistPosition();` → `_settings.Save(mutate(_settings.Load()))`. Un lecteur concurrent tourne sur le pool : `RateLimitHeaderUsageProvider.cs:205` `_settings.Load()` à chaque `GetAsync`, ainsi que `DiagnosticService.BuildReportAsync:145`.
- **Constat** : un `IOException` ou `UnauthorizedAccessException` au `File.Move` (violation de partage pendant qu'un autre lecteur tient `settings.json`, antivirus ou indexeur qui ouvre le fichier fraîchement écrit, disque plein, `%APPDATA%` redirigé hors ligne) remonte jusqu'au handler UI. Faute de filet global (FIAB-1), le processus tombe. Le widget de sessions relit et réécrit le fichier entier **à chaque** `LocationChanged`, soit des dizaines de cycles temp+Move par seconde pendant un glisser. C'est ce qui maximise la fenêtre de collision.
- **Impact** : crash sur le geste le plus courant (glisser le cadran ou le widget), sur un changement d'écran ou de DPI, à la fermeture des fenêtres Historique et Réglages, et même au lancement si `Background=true`.
- **Recommandation** : (1) rendre `Save` non levant, ou l'encapsuler dans une façade `EssayerEnregistrer` qui réessaie sur `IOException` (même motif que `JournalReleves`, borné), journalise l'échec et ne propage pas ; (2) pour le widget, ne persister la position qu'à la fin du glisser (`DragMove` qui rend la main, ou `LocationChanged` anti-rebondi d'environ 500 ms) ; (3) sérialiser les `Save` du processus derrière un verrou.
- **Applicabilité (desktop)** : pleine.
- **Statut challenge** : non contesté

### FIAB-3 — Motif « lecture ratée = vide, puis réécriture » : perte silencieuse de réglages et d'archives
- **Sévérité** : Majeur
- **Rôle émetteur** : Fiabilité (Tier 2)
- **Localisation** : `src/Chronos/Services/SettingsService.cs:124-130` combiné aux appelants `Save(Load() with …)` (FIAB-2) ; `src/Chronos/Services/ArchiveStore.cs:62-86` ; `src/Chronos/Services/TreatedStore.cs:96-130`
- **Preuve** : CITATION — `SettingsService.Load` : `catch (Exception ex) when (ex is IOException … or UnauthorizedAccessException …) { …Illisible…; return new ChronosSettings(); }`. La doc de classe assume que « le Save suivant réécrit simplement le fichier assaini ». `ArchiveStore.Add` : lecture dans `try { … } catch { }` → `map` vide → `map[sessionId] = now` → écriture de la map entière. L'échec d'écriture tombe ensuite dans `catch { }` (l.86).
- **Constat** : une erreur d'E/S **transitoire** à la lecture (fichier tenu par un autre lecteur, antivirus) est traitée comme un fichier corrompu. Le `Save` qui suit écrase alors **tous** les réglages de l'utilisateur avec les défauts plus une seule valeur modifiée. Pour les archives de sessions (geste permanent de l'utilisateur), une lecture ratée réduit `archived.json` à une seule entrée. Une écriture ratée est avalée sans trace, ni journal ni diagnostic, contrairement à `LastExactStore` qui publie `EcritureRatee`. Aucune sauvegarde n'est faite avant d'écraser un fichier jugé illisible, même réellement corrompu.
- **Impact** : réglages (thème, coin, styles, géométries, sonde) ou archives perdus d'un coup, sans qu'aucune trace ne permette de comprendre. Les sessions archivées réapparaissent.
- **Recommandation** : distinguer « illisible (syntaxe) » de « inaccessible (E/S) ». Sur une erreur d'E/S, réessayer brièvement, puis **refuser d'écrire** (lever vers la façade de FIAB-2 qui journalise) plutôt que repartir des défauts. Avant d'écraser un fichier réellement illisible, le copier en `.bak`. Dans `ArchiveStore` et `TreatedStore`, n'écrire que si la lecture a réussi ou si le fichier est absent, et exposer une `DerniereErreur` au diagnostic (motif `IEtatMagasin`).
- **Applicabilité (desktop)** : pleine.
- **Statut challenge** : non contesté

### FIAB-4 — `chronos.log` écrasé au démarrage : la trace « arrêt dépassé » du lancement précédent est détruite
- **Sévérité** : Mineur
- **Rôle émetteur** : Fiabilité (Tier 2)
- **Localisation** : `src/Chronos/Services/DiagnosticService.cs:115` ; `src/Chronos/Services/ArretHote.cs:77`
- **Preuve** : CITATION — `File.WriteAllText(Path.Combine(dir, "chronos.log"), "(log automatique au démarrage)\n" + report);` contre `File.AppendAllText(Path.Combine(dossier, "chronos.log"), "… arrêt dépassé : {cause} — sortie forcée…")`. OUTIL — `grep 'chronos.log"'` : ce sont les deux seuls écrivains.
- **Constat** : la ligne d'arrêt dépassé est ajoutée à la fermeture, mais le journal de démarrage suivant réécrit le fichier en entier. La seule observation du défaut de « processus zombie » (quick 260927) disparaît donc au relancement, c'est-à-dire au moment où l'utilisateur ou le développeur irait la chercher.
- **Impact** : la régression de l'arrêt ne peut pas être observée en production. L'instrumentation ajoutée exprès est neutralisée.
- **Recommandation** : écrire l'arrêt dépassé dans un fichier distinct (`arret.log`, en ajout avec rotation), ou faire relire et reporter par `LogStartupAsync` les lignes « arrêt dépassé » existantes avant de réécrire. Le crochet de FIAB-1 devrait suivre la même règle.
- **Applicabilité (desktop)** : pleine.
- **Statut challenge** : non contesté

### FIAB-5 — Le diagnostic interroge la chaîne en parallèle du consommateur unique
- **Sévérité** : Mineur
- **Rôle émetteur** : Fiabilité (Tier 2)
- **Localisation** : `src/Chronos/Services/DiagnosticService.cs:158` ; appelé par `App.xaml.cs:141` (fire-and-forget au démarrage) et `ViewModels/MainViewModel.cs:464` (réglages) ; contrat : `Services/RefreshOrchestrator.cs:10-12,52`
- **Preuve** : CITATION — `try { affiche = await _composite.GetAsync(ct); }` (`_composite` = l'`IUsageProvider` de tête). Doc de l'orchestrateur : « Une boucle consommateur UNIQUE … appelle IUsageProvider.GetAsync un à la fois (jamais de lecture concurrente) ». `RateLimitHeaderUsageProvider` et `ChronosOAuthUsageProvider` modifient `_prochainAppelAutorise`/`_nextAllowedCall`, `_cache`, `_cacheAt` et `_reculReseau` sans verrou. `LastExactStore.Save` utilise un temporaire `.tmp-{ProcessId}` partagé (l.129).
- **Constat** : au démarrage, la première passe de l'orchestrateur et celle du journal de démarrage partent quasi simultanément. Ce sont deux sondes et deux `LastExactStore.Save` concurrents. Ce dernier peut entrer en collision sur le même fichier temporaire, ce qui produit un faux `ecriture_ratee` au journal. Les `DateTimeOffset` (16 octets) lus et écrits sans barrière peuvent être déchirés : frein incohérent.
- **Impact** : une micro-requête de quota en double à chaque lancement et à chaque diagnostic. Des événements d'échec fictifs dans l'historique. Un frein anti-429 qui peut être faussé de façon rare et non reproductible.
- **Recommandation** : le diagnostic ne doit pas appeler la chaîne. Il peut lire le dernier snapshot émis (`SnapshotChanged`), ou demander un `RequestRefresh()` puis attendre l'émission suivante. À défaut, sérialiser `LastExactUsageProvider.GetAsync` derrière un `SemaphoreSlim`. Rendre le temporaire unique par appel (`Guid`).
- **Applicabilité (desktop)** : pleine.
- **Statut challenge** : non contesté

### FIAB-6 — La boucle de données meurt définitivement sur toute exception non prévue
- **Sévérité** : Mineur
- **Rôle émetteur** : Fiabilité (Tier 2)
- **Localisation** : `src/Chronos/Services/RefreshOrchestrator.cs:49-60` ; `Services/CompositeUsageProvider.cs:37-42` ; `Services/LastExactUsageProvider.cs:62` ; `Services/Historique/JournalisationUsageProvider.cs:65`
- **Preuve** : CITATION — `catch (OperationCanceledException) { /* arrêt normal */ }` est le seul catch autour de `await foreach … _provider.GetAsync … SnapshotChanged?.Invoke`. Le composite et les têtes de décorateur appellent leur inner hors de tout try. Les feuilles ne rattrapent qu'une liste fermée (`HttpRequestException`, `TaskCanceledException`, `OperationCanceledException`, `JsonException`, `IOException`). Aucun chemin levant concret n'a été prouvé atteignable.
- **Constat** : la moindre exception imprévue dans la chaîne ou chez un abonné de `SnapshotChanged` termine `ExecuteAsync`. Elle n'est pas retentée, et le comportement par défaut de .NET 8 (`BackgroundServiceExceptionBehavior.StopHost`) déclenche `StopApplication` alors que la fenêtre WPF reste affichée.
- **Impact** : le cadran se fige sur le dernier relevé sans signal clair. C'est une défaillance silencieuse sur le chemin critique.
- **Recommandation** : envelopper le corps de chaque itération dans `catch (Exception ex) when (ex is not OperationCanceledException)` : journaliser via `JournalReleves` ou le log, émettre un snapshot « indisponible », puis continuer. Poser `HostOptions.BackgroundServiceExceptionBehavior = Ignore` de façon consciente.
- **Applicabilité (desktop)** : pleine.
- **Statut challenge** : non contesté

### FIAB-7 — Rafraîchissement du widget de sessions : E/S disque synchrones sur le thread UI toutes les 2 s
- **Sévérité** : Mineur
- **Rôle émetteur** : Fiabilité (Tier 2)
- **Localisation** : `src/Chronos/ViewModels/SessionsViewModel.cs:185-197` → `Services/SessionMonitor.cs:126-239` → `Services/TranscriptSessionSource.cs:72-99`
- **Preuve** : CITATION — `DispatcherTimer { Interval = 2 s }` → `Refresh` → `_monitor.Read(now)`, qui enchaîne `EnumerateFiles("*.jsonl", SearchOption.AllDirectories)` sur `~/.claude/projects` (1 565 fichiers, 2,08 Go d'après `ReconstructionTokens.cs:20`), la lecture des transcripts récents (`Classify`, `DernierMessage`), `Directory.GetFiles` sur chaque racine d'état, la lecture du cache de l'app bureau et `ArchiveStore.Load` / `TreatedStore.Load`. Tout cela se fait sur le thread UI.
- **Constat** : le coût n'a pas été mesuré (exe non lancé). À chaud, il est probablement de quelques dizaines de ms. À froid (sortie de veille, cache disque vidé, antivirus actif, profil sur disque lent), l'énumération récursive peut bloquer le Dispatcher des centaines de ms, toutes les 2 s.
- **Impact** : saccades du glisser et de l'horloge 1 s, et retard des clics tant que le widget est activé.
- **Recommandation** : faire la lecture sur le pool (`Task.Run` + `IUiDispatcher.Post`, motif `HistoriqueViewModel.DemanderLecture`) avec une garde « une lecture en vol au plus ». Mesurer le temps passé et le publier au diagnostic.
- **Applicabilité (desktop)** : pleine.
- **Statut challenge** : non contesté

### FIAB-8 — Réconciliation de `~/.claude/settings.json` : lire-modifier-écrire sans coordination avec Claude Code
- **Sévérité** : Mineur
- **Rôle émetteur** : Fiabilité (Tier 2)
- **Localisation** : `src/Chronos/Services/ClaudeSettingsReconciler.cs:225-246,305-311` ; `src/Chronos/Services/ClaudeSettingsJson.cs:136`
- **Preuve** : CITATION — `var actuel = File.ReadAllText(_settingsPath); … WriteAtomic(reconcilie);` (aucun verrou, aucune vérification de mtime entre lecture et écriture). `if (string.IsNullOrWhiteSpace(json)) return new JsonObject();`. `File.Move(tmp, _settingsPath, overwrite: true)`.
- **Constat** : (a) si Claude Code réécrit son fichier entre la lecture et le `Move` (sessions ouvertes, `/config`, ajout de permission), l'une des deux écritures est perdue. (b) Un fichier vide ou blanc, par exemple capté pendant une réécriture tronquante, est traité comme `{}` : Chronos écrit alors un `settings.json` qui ne contient que ses hooks. (c) `File.Move` remplace un `settings.json` qui serait un lien symbolique (dépôt de dotfiles) par un fichier ordinaire.
- **Impact** : perte ponctuelle de réglages Claude Code, atténuée par la sauvegarde horodatée prise juste avant (5 conservées). La fenêtre de course est étroite : un passage par démarrage.
- **Recommandation** : relire le mtime et la taille juste avant le `Move` et abandonner s'ils ont changé. Traiter un fichier **existant** mais vide comme inexploitable (ne rien écrire). Détecter `FileAttributes.ReparsePoint` et écrire alors au travers du lien (écriture directe) ou s'abstenir.
- **Applicabilité (desktop)** : pleine.
- **Statut challenge** : non contesté

### FIAB-9 — Processus survivant invisible après fermeture du cadran (fenêtres cachées et ShutdownMode par défaut)
- **Sévérité** : Mineur
- **Rôle émetteur** : Fiabilité (Tier 2)
- **Localisation** : `src/Chronos/App.xaml` (pas de `ShutdownMode`) ; `src/Chronos/Views/SessionsController.cs:63` ; `src/Chronos/Views/MainWindow.xaml.cs` (aucun `Closing`)
- **Preuve** : CITATION — `_window?.Hide();` dans `Disable()`, jamais `Close()`. OUTIL — `grep "ShutdownMode|Closing"` : aucun traitement côté overlay. Le cadran est activable (aucun `WS_EX_NOACTIVATE`, et `DragMove` l'active).
- **Constat** : avec `ShutdownMode=OnLastWindowClose`, un Alt+F4 sur le cadran activé ferme `MainWindow`. La `SessionsWindow` cachée, ou visible, garde l'application en vie : Host actif, sonde réseau qui continue, mutex tenu, `OnExit` jamais appelé.
- **Impact** : le relancement affiche « Chronos tourne déjà ». C'est un zombie de même nature que celui corrigé par quick 260927, par une autre porte.
- **Recommandation** : `ShutdownMode="OnMainWindowClose"`, ou intercepter `MainWindow.Closing` pour router vers `Quit()` (ou annuler). Fermer réellement la fenêtre de sessions à la désactivation.
- **Applicabilité (desktop)** : pleine.
- **Statut challenge** : non contesté

### FIAB-10 — E/S synchrones sur le thread UI au démarrage
- **Sévérité** : Info
- **Rôle émetteur** : Fiabilité (Tier 2)
- **Localisation** : `src/Chronos/Services/Historique/JournalisationUsageProvider.cs:78-84` ; `src/Chronos/Services/DiagnosticService.cs:108-116,145,332,403`
- **Preuve** : OUTIL — CA1849 ×5, CA2016 ×1. CITATION — `StartAsync` exécute `_journal.Purger()` puis `AjouterEvenement` (jusqu'à 600 reprises, environ 0,6 s au pire) de façon synchrone dans `await _host.StartAsync()` sur le Dispatcher. `LogStartupAsync` est lancé depuis le thread UI sans `ConfigureAwait(false)`, ce qui fait revenir le `File.WriteAllText` final sur le Dispatcher.
- **Constat / Impact** : quelques dizaines à quelques centaines de ms de démarrage, sur un thread UI encore sans fenêtre. Pas de risque de deadlock.
- **Recommandation** : `Task.Run` pour `LogStartupAsync` et pour la purge et l'événement `demarrage`.
- **Applicabilité (desktop)** : pleine.
- **Statut challenge** : non contesté

### FIAB-11 — Hygiène des analyseurs (sans impact runtime)
- **Sévérité** : Info
- **Rôle émetteur** : Fiabilité (Tier 2)
- **Localisation** : voir le tableau de Phase 1
- **Preuve** : OUTIL — build AnalysisMode=All.
- **Constat** : les 4 CA2000 sont des faux positifs (`using (fs)` en aval). CA1001 SourceActiviteMemoisee concerne un singleton du processus, sans fuite réelle. CA1508 ReconstructionTokens:336 est du code mort. Aucune CS1998/CS4014.
- **Recommandation** : supprimer les faux positifs (`[SuppressMessage]` justifié) pour que les vraies alertes ressortent.
- **Applicabilité (desktop)** : pleine.
- **Statut challenge** : non contesté

---

## Compte par sévérité

| Bloquant | Majeur | Mineur | Info |
|---|---|---|---|
| 0 | 3 (FIAB-1, FIAB-2, FIAB-3) | 6 (FIAB-4 à FIAB-9) | 2 (FIAB-10, FIAB-11) |

## Points non vérifiés faute d'outil ou de droit
- Aucune exécution : l'exe Chronos ne doit pas être lancé. Les crashs de FIAB-2 et la survie de FIAB-9 sont déduits du code et de la sémantique WPF/.NET 8, pas reproduits.
- Coût réel sur le thread UI de FIAB-7 et FIAB-10 : non mesuré (pas de profilage).
- Suite xUnit non relancée (hors Phase 1 de ce rôle).
- Pas de fuzzing de fichiers corrompus ou tronqués. La tolérance de lecture (settings, journal, transcripts, agrégats) n'a été vérifiée que par lecture des catch.
- Ce que deviennent les journaux du Host (fournisseur EventLog par défaut de `Host.CreateApplicationBuilder`) quand un BackgroundService échoue : non observé.
