# Mainteneur (Tier 1) — dev-team-council, cycle ZEUS 2 (2026-10-03)

Boussole : « un dev qui débarque comprend-il ce module en 10 minutes, sans m'appeler ? »
Périmètre : `src/Chronos` (189 fichiers .cs, 21 779 lignes), en insistant sur le diff du cycle 2 (`v1.8..HEAD`, 167 fichiers)
et le diff du cycle 1 (`937e980..v1.8`, 207 fichiers). Lecture seule : aucun fichier source modifié, l'exe n'a jamais été lancé.

## Phase 1 — Vérité-terrain (outils)

### 1. Métriques Roslyn (CA1502 / CA1505 / CA1506)
- `dotnet build` par défaut : 0 avertissement. Les règles de métriques sont **désactivées par défaut** (aucun `.editorconfig`,
  aucun `.globalconfig`, aucun `AnalysisMode` dans le csproj).
- Relevé obtenu **sans toucher au dépôt** : ruleset temporaire posé dans le scratchpad, passé par
  `-p:CodeAnalysisRuleSet=<scratch>\metrics.ruleset`, sortie de build vers le scratchpad (`-p:OutDir`). Seuils par défaut.

| Règle | Membre | Valeur (seuil) |
|---|---|---|
| CA1502 | `Services/DiagnosticService.cs:140` `BuildReportAsync` | complexité cyclomatique **75** (25) |
| CA1502 | `Services/SessionMonitor.cs:126` `Inspecter` | complexité cyclomatique **32** (25) |
| CA1506 | `App.xaml.cs:16` `App` | 126 types, 27 namespaces (95) |
| CA1506 | `App.xaml.cs:238` `ConfigureServices` | 86 types (40) |
| CA1506 | `Services/DiagnosticService.cs:23` `DiagnosticService` | 120 types (95) |
| CA1506 | `Services/DiagnosticService.cs:140` `BuildReportAsync` | 75 types (40) |
| CA1506 | `Services/SessionMonitor.cs:126` `Inspecter` | 44 types (40) |
| CA1506 | `ViewModels/MainViewModel.cs:434` `.ctor` | 53 types (40) |

Aucune occurrence de CA1505 (indice de maintenabilité) : aucun membre ne passe sous le seuil.

Second passage avec `-p:AnalysisMode=All` (même méthode, rien d'écrit dans le dépôt) : 664 avertissements, chacun compté environ 4 fois
(passes wpftmp et principale). En tête : CA1031 (catch général), CA1062, CA2007, CA1305. Ce sont surtout des signaux de robustesse et de
culture. Je les laisse aux rôles concernés, sauf ce qui touche à la lisibilité (voir MAINT-14).

### 2. Cohérence de style : `dotnet format Chronos.sln --verify-no-changes`
- Code de sortie 2, **754 écarts, tous `WHITESPACE`**, dans 49 fichiers (dont 206 écarts dans `src`). Le reste est dans les tests :
  SessionStylesBindingTests 120, CadransThemeBindingTests 101, PistesHistoriqueTests 64. Côté `src` : App.xaml.cs 50,
  DiagnosticService.cs 29, EmberRingControl.cs 18.
- **Aucun `.editorconfig` dans le dépôt** : la comparaison se fait donc contre les valeurs par défaut de l'outil, pas contre une
  convention du projet. La plupart des écarts sont des alignements volontaires de lignes de continuation.

### 3. Méthodes et fichiers démesurés (heuristique awk, méthodes de 70 lignes ou plus)
| Lignes | Membre |
|---|---|
| 377 | `Services/DiagnosticService.cs:140` `BuildReportAsync` (fichier : 999 lignes) |
| 256 | `App.xaml.cs:238` `ConfigureServices` |
| 158 | `Services/RateLimitHeaderUsageProvider.cs:196` `GetAsync` |
| 133 | `Services/SessionMonitor.cs:126` `Inspecter` |
| 133 | `App.xaml.cs:21` `OnStartup` |
| 104 | `ViewModels/MainViewModel.cs:434` constructeur (fichier : 876 lignes) |
| 104 | `Services/LecteurAppBureau.cs:196` `Lire` |
| 101 | `Services/Historique/Tokens/ReconstructionTokens.cs:156` `ExecuterUnePasse` |

Densité de commentaires : 6 715 lignes de commentaire sur 21 779, soit 31 %. **939 lignes** citent un identifiant de planification
(`XXX-00`, `D-00-00`, `plan 00-00`, `phase NN`). Dans plusieurs fichiers, les commentaires dépassent 45 % du total
(DonneesHistorique 60 %, UsageNormalization 58 %, SessionTreatmentTracker 53 %, SessionMonitor 46 %).

## Phase 2 — Findings

```
ID            : MAINT-1
Sévérité      : Majeur
Rôle émetteur : Mainteneur (Tier 1)
Localisation  : src/Chronos/Services/DiagnosticService.cs:140-517
Preuve        : OUTIL:CA1502 « 'BuildReportAsync' a une complexité cyclomatique de '75' » + CA1506 « couplé avec 75 types » ; heuristique : 377 lignes
Constat       : Une seule méthode construit les huit sections du rapport : chaîne de données, transcripts, magasins, processus, reconstruction, affichage, widget de sessions, réglages Claude. Elle fait elle-même les E/S disque, le parsing JSON (une boucle à 4 niveaux d'imbrication, l. 335-351) et la mise en forme. Certaines sections sont déjà extraites (SectionJournalHistorique, DecrireSourceAppBureau), d'autres restent en ligne. La numérotation des sections saute le « 2) » (l. 168 → 242).
Impact        : Pour modifier une ligne du rapport, il faut dérouler 377 lignes et 28 identifiants d'exigence distincts. Le rapport est l'outil de support principal (« pourquoi pas de couleurs »). Le module est intestable par section, sauf via des assertions sur le texte complet.
Recommandation: Une méthode privée par section (`SectionChaine`, `SectionTranscripts`, `SectionMagasins`, `SectionWidgetSessions`, `SectionReglagesClaude`), toutes sur le même modèle que SectionJournalHistorique. BuildReportAsync se réduit alors à l'appel unique au composite suivi de la liste ordonnée des sections. Sortir le parsing des hooks (l. 328-358) vers ClaudeSettingsJson ou SessionHookInstaller.
Applicabilité : Desktop : rapport local écrit dans %APPDATA%, aucune contrainte web.
Statut challenge : non contesté
```

```
ID            : MAINT-2
Sévérité      : Majeur
Rôle émetteur : Mainteneur (Tier 1)
Localisation  : src/Chronos/Services/SessionMonitor.cs:126-259
Preuve        : OUTIL:CA1502 « 'Inspecter' a une complexité cyclomatique de '32' » ; CITATION : étapes dans l'ordre du code « 1 & 2) » l.128, « 2.c) » l.164, « 2.e) » l.180, « 2.b) » l.186, « 2.d) » l.209, « 3) » l.216 ; 17 identifiants distincts dans la méthode (APP-02 APP-03 APP-06 FUS-01 LIB-01 LUE-01..05 NET-01 NET-03 NET-04 OBS-01 SIL-01 SRC-01 SUB-01)
Constat       : C'est le pipeline le plus critique du widget de sessions (collecte, app bureau, sous-agents, arbitrage, détecteur, titre, filtres). Les étapes sont étiquetées DANS LE DÉSORDRE (2.c, 2.e, 2.b, 2.d), ce qui indique des insertions successives jamais renumérotées. Un seul bloc de commentaire (l. 216-227) explique l'ordre des filtres sur 12 lignes. LUE-03, cité dans le code, n'apparaît que dans `.planning/milestones/v1.7-REQUIREMENTS.md`, pas dans le REQUIREMENTS.md courant.
Impact        : Test des 10 minutes ÉCHOUÉ sur ce module. Un nouveau venu ne peut pas reconstituer l'ordre réel du pipeline à partir des étiquettes. Il doit aussi fouiller les archives de jalons pour décoder les identifiants.
Recommandation: Découper en étapes nommées appelées dans l'ordre : `CollecterSignaux`, `QualifierParAppBureau`, `Arbitrer`, `ObserverTraitement`, `EnrichirTitres`, `AppliquerFiltres`. Les noms de méthode remplacent les étiquettes numérotées. Ramener chaque commentaire au pourquoi local et renvoyer les identifiants vers un seul index de traçabilité.
Applicabilité : Desktop : lecture de fichiers locaux, aucun impact web.
Statut challenge : non contesté
```

```
ID            : MAINT-3
Sévérité      : Majeur
Rôle émetteur : Mainteneur (Tier 1)
Localisation  : systémique ; exemples src/Chronos/Services/DiagnosticService.cs:42-82, ViewModels/MainViewModel.cs:423-431, Services/DiagnosticService.cs:420-421
Preuve        : OUTIL:grep : 939 lignes citant un identifiant de planification, 31 % de lignes de commentaire, 13 anecdotes datées (« constaté/mesuré le 2026-… ») ; CITATION DiagnosticService.cs:43 « les 8 sites de construction existants (1 en production, 7 en tests) compilent sans retouche » ; DiagnosticService.cs:420 « NB : l'itérateur s'appelle « fic » (nom historique, conservé : aucun autre `e` n'est plus déclaré… »
Constat       : Les commentaires expliquent bien le pourquoi, mais sous forme de journal de bord : numéros de phase et de plan, dates d'incident, nombre de sites d'appel dans les tests, historique des versions abandonnées. La documentation XML du constructeur de DiagnosticService fait 40 lignes pour 12 paramètres. Certaines mentions sont déjà fausses ou caduques : « 8 sites… 7 en tests » contre « 10 sites préexistants… 9 en tests » trois lignes plus bas.
Impact        : Le code utile est noyé : un lecteur doit filtrer l'archéologie pour trouver la règle en vigueur. Les compteurs de sites et les numéros de phase se périment sans qu'aucun outil le signale. Coût de lecture élevé dans tout le projet.
Recommandation: Règle d'écriture : un commentaire énonce l'invariant ou la raison actuelle, sans identifiant de phase ou de plan ni date d'incident. L'historique va dans git et les .planning. Garder au plus un identifiant d'exigence par règle, et seulement s'il est résolu dans le REQUIREMENTS.md courant. Purger en priorité les docs de constructeur et les « NB » de nommage.
Applicabilité : Indépendant de la cible.
Statut challenge : non contesté
```

```
ID            : MAINT-4
Sévérité      : Mineur
Rôle émetteur : Mainteneur (Tier 1)
Localisation  : src/Chronos/Services/DiagnosticService.cs:83-90 ; ViewModels/MainViewModel.cs:432-444 ; Services/SessionMonitor.cs:56-63 ; App.xaml.cs:296 (`new SessionMonitor(null, null, …)`)
Preuve        : CITATION MainViewModel.cs:424-426 « est OPTIONNEL et en DERNIÈRE position, et ce n'est pas un détail de style : les sites de construction préexistants (2 en tests…) compilent sans une retouche » ; DiagnosticService : 12 paramètres dont 8 optionnels ; SessionMonitor : 8 paramètres tous optionnels, dont deux mutuellement exclusifs (exception l. 62-63) ; 52 `new SessionMonitor(` dans les tests
Constat       : Un « protocole » ajoute chaque nouvelle dépendance en paramètre optionnel final, dans le seul but de ne pas toucher aux tests. En production, la DI fournit pourtant tout. Le résultat : des constructeurs télescopiques dont la signature ne dit pas ce qui est vraiment requis, et des appels positionnels opaques (`null, null`).
Impact        : Un nouveau venu ne sait pas quelles dépendances sont essentielles et lesquelles sont facultatives. Un oubli de câblage passe sans erreur (le null dégrade silencieusement). La liste s'allonge à chaque phase.
Recommandation: Rendre obligatoires les dépendances de production. Faire passer les tests par des fabriques ou builders de test (`DiagnosticServiceBuilder`) qui fournissent des doublures par défaut. Pour SessionMonitor, remplacer le couple exclusif sessionsDir/dossiersEtat par un seul paramètre.
Applicabilité : Desktop / DI Microsoft.Extensions : aucune contrainte.
Statut challenge : non contesté
```

```
ID            : MAINT-5
Sévérité      : Mineur
Rôle émetteur : Mainteneur (Tier 1)
Localisation  : src/Chronos/Services/DiagnosticService.cs:337 ; Services/SessionHookInstaller.cs:122-124
Preuve        : CITATION SessionHookInstaller.cs:122-124 « Les noms des événements câblés. CONSERVÉ (le diagnostic et plusieurs tests le lisent) mais DÉRIVÉ du câblage : une seule source de vérité, jamais deux listes à tenir d'accord » ; DiagnosticService.cs:337 `foreach (var ev in new[] { "Notification", "Stop", "UserPromptSubmit", "SessionStart", "SessionEnd" })` ; OUTIL:grep `SessionHookInstaller.Events` : seuls les tests l'utilisent, jamais le diagnostic
Constat       : Le commentaire affirme que le diagnostic lit `SessionHookInstaller.Events`. C'est faux : le diagnostic tient sa propre liste en dur de 5 événements. Le câblage réel en compte 8 (PermissionRequest, PreToolUse et PostToolUse manquent).
Impact        : Le commentaire trompe le mainteneur, qui croit à une source unique. Effet de bord à confirmer par le rôle qualité : la ligne « Hooks --hook installés » du rapport passe sous silence 3 des 8 hooks câblés.
Recommandation: Itérer sur `SessionHookInstaller.Events` à la l. 337, ce qui rend le commentaire vrai, et ajouter un test qui épingle cette dérivation.
Applicabilité : Desktop : lecture de ~/.claude/settings.json.
Statut challenge : non contesté
```

```
ID            : MAINT-6
Sévérité      : Mineur
Rôle émetteur : Mainteneur (Tier 1)
Localisation  : src/Chronos/Services/DiagnosticService.cs:218, 244, 327, 375 ; App.xaml.cs:179-180 ; Services/ChronosOAuthStore.cs:22 ; Services/ClaudeSettingsReconciler.cs:85 ; Services/SessionHookInstaller.cs:131 ; Services/TranscriptSessionSource.cs:70
Preuve        : CITATION ChronosPaths.cs : `ProjectsRoot: Path.Combine(…UserProfile), ".claude", "projects")` ; DiagnosticService.cs:244 `var projects = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");` alors que `_paths.ProjectsRoot` est injecté ; `".claude", "settings.json"` recomposé dans 3 fichiers ; `"Chronos", "sessions"` dans App et RacinesEtat ; `oauth.dat` dans 2 fichiers
Constat       : ChronosPaths existe pour centraliser les chemins, mais une dizaine de sites les recomposent à la main. Le même fichier recompose même un chemin que son propre `_paths` fournit déjà.
Impact        : Pour savoir où Chronos lit et écrit, un nouveau venu doit chercher partout au lieu d'ouvrir un seul fichier. Un renommage de dossier oublie forcément un site.
Recommandation: Ajouter à ChronosPaths `OAuthFile`, `SessionsDir` et `ClaudeSettingsFile`, puis faire passer tous les sites par lui (l'injecter, ou utiliser `ChronosPaths.Default()` en mode --hook).
Applicabilité : Desktop : chemins %APPDATA% / %USERPROFILE% (contrainte CLAUDE.md).
Statut challenge : non contesté
```

```
ID            : MAINT-7
Sévérité      : Mineur
Rôle émetteur : Mainteneur (Tier 1)
Localisation  : ArchiveStore.cs:82 ; ChronosOAuthStore.cs:38 ; ClaudeSettingsReconciler.cs:305-308 ; CouvertureTokens.cs:173 ; Curseurs.cs:136 ; MagasinAgregats.cs:205 ; LastExactStore.cs:129 ; SessionHookInstaller.cs:328 ; SettingsService.cs:163 ; TreatedStore.cs:126 (tous sous src/Chronos/Services)
Preuve        : OUTIL:grep `var tmp = … ".tmp-" + Environment.ProcessId` : 10 occurrences, chacune suivie de `File.WriteAllText(tmp, …); File.Move(tmp, …, overwrite: true);`. Variantes de mise en forme : `ToString(CultureInfo.InvariantCulture)` dans 3 sites, interpolation `$".tmp-{…}"` dans 2, concaténation dans 5. Deux méthodes privées homonymes `WriteAtomic` (ClaudeSettingsReconciler, SessionHookInstaller).
Constat       : L'écriture atomique, qui est une politique de robustesse du projet, est réimplémentée 10 fois au lieu d'exister une seule fois.
Impact        : Si la politique change (ajouter un Flush ou un File.Replace, gérer les collisions), il faut modifier 10 sites sans en oublier. Un lecteur se demande si les variantes sont voulues.
Recommandation: Une aide unique `EcritureAtomique.Ecrire(chemin, contenu)` dans Services, appelée partout. Les tests existants des magasins couvrent la non-régression.
Applicabilité : Desktop : fichiers locaux sous %APPDATA%.
Statut challenge : non contesté
```

```
ID            : MAINT-8
Sévérité      : Mineur
Rôle émetteur : Mainteneur (Tier 1)
Localisation  : src/Chronos/Services/DiagnosticService.cs:246
Preuve        : CITATION l.246 `Directory.EnumerateFiles(projects, "*.jsonl", SearchOption.AllDirectories).Take(5000).Count(); } catch { }` puis l.247 `jsonl + " fichier(s) .jsonl"` ; à comparer avec l.25-26 « Une borne de LISIBILITÉ, pas une troncature muette : le reste est annoncé »
Constat       : Valeur magique 5000 sans constante nommée. Elle tronque le compte en silence : le rapport affiche « 5000 fichier(s) » comme un compte exact. Le même fichier pose pourtant en tête la règle « pas de troncature muette ». Le `catch { }` vide masque aussi les échecs d'énumération.
Impact        : Incohérence entre la règle déclarée et le code. Le lecteur ne sait pas pourquoi 5000. Le chiffre affiché contredit la doctrine « exact ou rien ».
Recommandation: `private const int MaxJsonlComptes = 5000;` et afficher « ≥ 5000 » quand la borne est atteinte. Écrire l'échec au lieu de le taire.
Applicabilité : Desktop : parcours de ~/.claude/projects.
Statut challenge : non contesté
```


```
ID            : MAINT-9
Sévérité      : Mineur
Rôle émetteur : Mainteneur (Tier 1)
Localisation  : src/Chronos/ViewModels/MainViewModel.cs:49-50, 380, 412, 544, 582 ; Services/SessionMonitor.cs:126, 297, 305 ; Services/SettingsService.cs:29 / LectureReglages.cs:21 / ReglagesViewModel.cs:25 ; MainViewModel.cs:481, 493
Preuve        : CITATION MainViewModel : `FiveHour`, `SevenDay`, `ToggleVerticalLayout`, `SelectTheme` voisinent avec `SurEtatAuthChange`, `AppliquerEtatAuth`, `OrientationFusible` ; SessionMonitor : `Inspecter` / `AppliquerSilence` (FR) à côté de `TryRead` (EN) ; même concept nommé `SettingsService`/`ChronosSettings` (EN) et `LectureReglages`/`ReglagesViewModel` (FR) ; enum `CadranStyle.Arcs` affiché « Anneaux » (l.481), `SessionStyle.Annonciateur` affiché « Voyants » (l.493)
Constat       : Le nommage mélange français et anglais au sein d'une même classe et pour un même concept (Settings/Réglages, Snapshot/Relevé, Treated/Traitée). Plusieurs valeurs d'énumération portent un autre nom que celui de l'interface.
Impact        : Pour chercher un concept, il faut deviner la langue (grep « Reglages » ne trouve pas SettingsService). Le vocabulaire de l'utilisateur (« Anneaux », « Voyants ») ne se retrouve pas dans le code.
Recommandation: Fixer et écrire une règle dans CLAUDE.md (section Conventions, vide aujourd'hui) : par exemple, domaine en français et types d'infrastructure .NET en anglais. Aligner les noms d'énumération sur les libellés affichés lors d'un refactoring mécanique (renommage IDE).
Applicabilité : Indépendant de la cible.
Statut challenge : non contesté
```

```
ID            : MAINT-10
Sévérité      : Mineur
Rôle émetteur : Mainteneur (Tier 1)
Localisation  : src/Chronos/App.xaml.cs:238-494 ; ViewModels/MainViewModel.cs:434-537
Preuve        : OUTIL:CA1506 `ConfigureServices` 86 types, `App` 126 types / 27 namespaces, `MainViewModel..ctor` 53 types ; heuristique : 256 et 104 lignes ; 105 lignes de commentaire dans ConfigureServices
Constat       : La racine de composition est une seule méthode linéaire de 256 lignes qui mélange tous les sous-systèmes (Historique, réglages, sessions, pipeline exact, tokens). Le constructeur de MainViewModel construit lui-même un ReglagesViewModel, peuple trois catalogues avec des libellés en dur et câble quatre canaux d'événements.
Impact        : Pour trouver le câblage d'un sous-système, il faut lire toute la méthode. L'ordre d'inscription, parfois significatif (« inscrits plus bas »), n'est pas visible d'un coup d'œil.
Recommandation: Des méthodes d'extension par sous-système (`AddPipelineExact`, `AddWidgetSessions`, `AddHistorique`, `AddReglages`), appelées dans l'ordre depuis ConfigureServices. Dans MainViewModel, sortir la construction des catalogues vers des fabriques statiques testables.
Applicabilité : Desktop / Generic Host WPF.
Statut challenge : non contesté
```

```
ID            : MAINT-11
Sévérité      : Mineur
Rôle émetteur : Mainteneur (Tier 1)
Localisation  : src/Chronos/Services/RateLimitHeaderUsageProvider.cs:233-241
Preuve        : CITATION l.233 `var rejoue = false;` l.238 `if ((int)resp.StatusCode == 401 && !rejoue)` l.241 `rejoue = true;`, et la variable n'est plus jamais lue
Constat       : Le drapeau `rejoue` est toujours faux au moment du test et n'est relu nulle part après son affectation : c'est du code mort, qui suggère une boucle de rejeu qui n'existe pas. Les codes HTTP sont en entiers bruts (401, 403, 429, 400, 404).
Impact        : Le lecteur cherche la boucle, puis doute de l'intention (un seul rejeu ou plusieurs ?), sur le chemin le plus sensible de la chaîne exacte.
Recommandation: Supprimer `rejoue` (un seul rejeu, garanti par la structure). Utiliser `HttpStatusCode.Unauthorized`, `HttpStatusCode.TooManyRequests`, etc.
Applicabilité : Desktop : requête HTTPS sortante unique.
Statut challenge : non contesté
```

```
ID            : MAINT-12
Sévérité      : Mineur
Rôle émetteur : Mainteneur (Tier 1)
Localisation  : racine du dépôt (absence de .editorconfig) ; 49 fichiers signalés
Preuve        : OUTIL:dotnet format --verify-no-changes → code de sortie 2, 754 × « error WHITESPACE » (App.xaml.cs 50, DiagnosticService.cs 29, SessionStylesBindingTests.cs 120…) ; `find -name .editorconfig` : aucun résultat
Constat       : Aucune convention de style n'est écrite ni outillée. Le style repose sur l'habitude de l'auteur, et `dotnet format` ne peut pas servir de garde-fou (il diverge d'emblée sur 754 points).
Impact        : Un contributeur avec un autre IDE reformatera des fichiers entiers, et les diffs de revue seront pollués. CLAUDE.md dit d'ailleurs « Conventions not yet established ».
Recommandation: Ajouter un `.editorconfig` qui accepte le style actuel (alignement des continuations). Y activer CA1502/CA1506 en `suggestion` pour suivre les métriques. Puis passer `dotnet format --verify-no-changes` en porte de CI ou en hook.
Applicabilité : Indépendant de la cible.
Statut challenge : non contesté
```

```
ID            : MAINT-13
Sévérité      : Info
Rôle émetteur : Mainteneur (Tier 1)
Localisation  : src/Chronos/Services/LecteurAppBureau.cs (24 occurrences), TranscriptSessionSource.cs (12), SessionMonitor.cs (11), LastExactStore.cs (9)
Preuve        : OUTIL:grep -c « System.(DateTimeOffset|IO.|Environment.|StringComparer|Array.) » ; ImplicitUsings=enable dans Chronos.csproj
Constat       : Certains fichiers qualifient entièrement des types déjà importés implicitement (`System.DateTimeOffset`, `System.IO.File`), d'autres non. C'est le même code écrit de deux façons.
Impact        : Bruit visuel, lignes rallongées, impression de deux bases de code.
Recommandation: Simplifier (IDE0001/IDE0002) lors du passage `.editorconfig` de MAINT-12.
Applicabilité : Indépendant de la cible.
Statut challenge : non contesté
```

```
ID            : MAINT-14
Sévérité      : Info
Rôle émetteur : Mainteneur (Tier 1)
Localisation  : 21 sites `catch { }`, ex. src/Chronos/App.xaml.cs:136, 150, 208 ; Services/DiagnosticService.cs:246, 417, 436
Preuve        : OUTIL:grep `catch\s*\{\s*\}` → 21 occurrences ; CA1031 (AnalysisMode=All) ≈ 116 sites distincts
Constat       : De nombreux `catch` vides ou généraux, rarement accompagnés d'une ligne qui nomme ce qui est toléré. Avec un commentaire (« best-effort », « jamais fatal »), l'intention est claire. Sans, le lecteur ne distingue pas une tolérance voulue d'un oubli.
Impact        : Un nouveau venu ne sait pas quelles exceptions sont attendues.
Recommandation: Pour la lisibilité seulement : chaque `catch` porte le type attendu ou un commentaire d'une ligne. Le traitement de fond (journaliser) relève du rôle robustesse.
Applicabilité : Desktop : la robustesse « aucune source ≠ crash » est voulue par CLAUDE.md, donc seule la lisibilité des catch est en cause.
Statut challenge : non contesté
```

## Test des 10 minutes

- **SessionMonitor.Inspecter (le plus critique côté widget) : ÉCHEC.** Étapes étiquetées dans le désordre, 17 identifiants à
  décoder, dont certains seulement dans les archives de jalons (MAINT-2).
- **DiagnosticService : ÉCHEC.** 377 lignes et 8 sections dans une seule méthode (MAINT-1).
- **Réussites à citer comme modèles** : `ViewModels/AutomateGeste.cs` (131 lignes, automate à 4 états, méthodes courtes, noms
  explicites), `ViewModels/Historique/HistoriqueViewModel.cs` (635 lignes mais une trentaine de méthodes courtes et bien nommées),
  `App.OnStartup` (long mais linéaire, avec un switch de mode lisible). Le geste unique du cycle 2 se reprend sans aide.

## Compte par sévérité

| Bloquant | Majeur | Mineur | Info |
|---|---|---|---|
| 0 | 3 | 9 | 2 |

## Non vérifié faute d'outil

- Aucun analyseur de métriques n'est activé dans le projet. Je les ai mesurés par un ruleset temporaire passé en ligne de commande,
  hors dépôt : c'est une preuve d'outil, mais ces métriques ne sont **pas** suivies en continu.
- La longueur des méthodes vient d'une heuristique awk (repérage par indentation), pas de Roslyn : quelques lignes d'écart possibles.
- Les XAML ne sont couverts ni par `dotnet format` ni par les analyseurs : leur lisibilité n'a pas été mesurée
  (ReglagesWindow.xaml fait 1 004 lignes, NON VÉRIFIÉ en Phase 1).
