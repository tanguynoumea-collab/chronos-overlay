# Architecte (Tier 1) — Chronos, cycle ZEUS 2

> Rapport rendu par l'agent (sans outil d'écriture) et reporté tel quel par l'orchestrateur, condensé sur la forme.

## Phase 1 — outils

| Contrôle | Résultat |
|---|---|
| `dotnet build Chronos.sln` | 0 avertissement, 0 erreur |
| Analyseurs Roslyn | **NON ACTIVÉS** dans le projet ; build forcé `-p:AnalysisLevel=latest-recommended` : ≈ 63 occurrences distinctes (CA1305 ×48, CA1725 ×26, CA1822 ×24, CA1859 ×16, CA2249 ×4, CA1861 ×4, CA2016 ×2, CA1001 ×2) |
| `dotnet format --verify-no-changes` | Échec (code 2) : 754 lignes WHITESPACE, 49 fichiers (21 sous src) ; pas de `.editorconfig` |
| Graphe ProjectReference | Chronos.Tests → Chronos seul ; pas de cycle |
| Étanchéité des couches (grep) | Models/ : 0 using WPF ; Services/ : WPF dans OverlayController, TopmostGuard, WpfUiDispatcher ; ViewModels/ : WPF dans 5 fichiers |
| `#if` / multi-cible | aucun / une seule cible |

## Findings

- **ARCH-1 — Majeur** — `ViewModels/MainViewModel.cs:434-446, 463, 312-418, 813-834`. CITATION : ctor à 16 paramètres (6 optionnels), 876 lignes, 31 `[ObservableProperty]`, 14 `[RelayCommand]`, 7 séquences `Load() with {…}; Save(...)`. VM-dieu (cadran + persistance de chaque réglage + arbitre de clic + thèmes/orientations + sessions + autostart + OAuth + journal) ; dépend de 3 types concrets. Impact : toute évolution touche ce fichier, testable seulement avec les vrais services. Reco : service de réglages derrière interface (`IReglagesChronos.Modifier(mutation)`), VM de réglages dédié, `ISourceSnapshots` au lieu de `RefreshOrchestrator`. Applicabilité : MVVM desktop.
- **ARCH-2 — Majeur** — `App.xaml.cs:238` ; `tests/CompositionRootTests.cs:34, 457`. CITATION : `private static void ConfigureServices` ; tests : « Reproduit ConfigureServices… copié LIGNE POUR LIGNE ». La racine de composition (~260 lignes) est privée ; les tests en maintiennent une copie. Impact : un enregistrement oublié/réordonné (ordre des IHostedService) passe les tests et casse au lancement. Reco : classe statique `CompositionChronos.Configurer(...)` hors WPF, appelée par App et par les tests.
- **ARCH-3 — Majeur** — `Services/SessionMonitor.cs:56-68`, `DiagnosticService.cs:83-90`, `App.xaml.cs:296`, `MainViewModel.cs:425-432`. CITATION : dépendances ajoutées en paramètres optionnels de fin de liste « pour que les sites préexistants compilent » ; défauts qui ouvrent des ressources réelles (`RacinesEtat.ParDefaut()`, `new TranscriptSessionSource()`). Impact : une dépendance oubliée compile et dégrade en silence (contre « exact ou rien »). Reco : dépendances de production obligatoires, fabriques de test, aucun défaut vers des ressources réelles.
- **ARCH-4 — Mineur** — `MainViewModel.cs:463`, `App.xaml.cs:254`. Le VM construit un second `IReglagesHistorique` et son `ReglagesViewModel` hors du conteneur. Reco : injecter.
- **ARCH-5 — Mineur** — `MainViewModel.cs:2,14,175,208,776` ; `HistoriqueViewModel.cs:57,284` ; `SessionsViewModel.cs:4-5,34,129,243` ; `WindowGaugeViewModel.cs:1`. `DispatcherTimer`, `Brush`, `Orientation` dans les VM malgré `IUiDispatcher`. Reco : `IMinuterie`, états énumérés + convertisseurs.
- **ARCH-6 — Mineur** — rangement : adaptateurs WPF dans Services (OverlayController, TopmostGuard) vs Views (SessionsController) ; modèles dans Services (ChronosSettings, SessionSnapshot) ; automates purs dans ViewModels ; Services/ plat (~70 fichiers). Reco : dossier Plateforme/Interop, modèles dans Models/, Services par domaine.
- **ARCH-7 — Mineur** — `Controls/Cadrans/{EmberRingControl,FlapRow,FuseBar,TideColumn}.cs` : namespace `Chronos.Controls` au lieu de `Chronos.Controls.Cadrans` (seuls écarts du projet).
- **ARCH-8 — Mineur** — `MainWindow.xaml.cs:152-156`, `HistoriqueWindow.xaml.cs:65-71,103-128`, `ReglagesWindow.xaml.cs:47-48,63,76-80` : recopie du thème dans chaque fenêtre/vue, géométrie et DWM dupliqués, `ReglagesWindow` appelle un helper statique de `HistoriqueWindow`. Reco : service de thème au niveau Application, helper `ChromeFenetre`.
- **ARCH-9 — Mineur** — `App.xaml:5-11`, `App.xaml.cs:35-39` : le mode `--hook` (jusqu'à 5 processus par événement) passe par l'initialisation de l'Application WPF. Latence NON VÉRIFIÉE. Reco : `Program.Main` explicite qui traite `--hook` avant d'instancier App.
- **ARCH-10 — Mineur** — `App.xaml.cs:109-114, 211-228` ; `JournalisationUsageProvider.cs:78-81` ; `RefreshOrchestrator.cs:27` : couplage temporel Host/Dispatcher (ordre de résolution du VM avant StartAsync, E/S synchrones dans StartAsync, arrêt borné). Reco : dernier snapshot rejoué à l'abonnement, pas d'E/S synchrones dans StartAsync.
- **ARCH-11 — Mineur** — solution : `dotnet format` en échec, pas de `.editorconfig`. Reco : `.editorconfig`, passage unique de `dotnet format`, vérification dans la porte de tests.
- **ARCH-12 — Info** — le cœur métier est isolé de WPF en pratique, mais seulement par convention. Reco : test de réflexion (aucun type de Models/Services hors adaptateurs ne référence PresentationFramework).
- **ARCH-13 — Info** — analyseurs non activés ; activés : CA1001 `SourceActiviteMemoisee._verrou` (SemaphoreSlim), CA2016 `JournalisationUsageProvider.cs:84`. Reco : `Directory.Build.props` `AnalysisLevel=latest-recommended`.
- **ARCH-14 — Info** — galeries et données de démonstration (`CadranGalleryWindow`, `SessionsGalleryWindow`, `HistoriqueGalerie`, `ScenariosHistorique`, `SourceHistoriqueDemonstration`) livrées dans l'exe de production. Reco : projet `Chronos.Galerie` séparé.

## Bilan
Bloquant 0 · Majeur 3 · Mineur 8 · Info 3. Non vérifié : coût de démarrage `--hook` (exe interdit), métriques de couplage (outil absent).
