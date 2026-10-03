# Council — rôle Tests / QA (Tier 1) — Chronos, cycle ZEUS 2 (2026-10-03)

## Phase 1 — Vérité-terrain (outils)

- **Projet de tests** : `tests/Chronos.Tests/Chronos.Tests.csproj` (xUnit 2.9.2 + Xunit.StaFact 1.1.11, net8.0-windows).
- **Exécution** : `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --artifacts-path <scratchpad> --collect:"Code Coverage;Format=cobertura"`
  → **Réussi : 1949 / 1949, 0 échec, 0 ignoré, 49 s** (build isolé dans le scratchpad, aucun artefact écrit dans le dépôt).
- **Coverlet** : **absent** (`coverlet.collector` n'est pas référencé par le projet de tests) → couverture Coverlet NON VÉRIFIÉE.
  Couverture **mesurée à la place avec Microsoft.CodeCoverage** (livré par `Microsoft.NET.Test.Sdk` 17.11.1, déjà référencé ;
  aucune modification du projet). Artefacts : scratchpad `results/<guid>/*.cobertura.xml`, `results/tests.trx`, `files_cov.txt`.
- **Couverture de l'assembly Chronos** :
  - lignes (dédoublonnées par fichier source, `src/Chronos`) : **8512 / 9424 = 90,3 %** ; ligne-rate brute du paquet `Chronos` : 86,6 % (inclut le code généré) ;
  - branches (paquet `Chronos`) : **83,2 %**.
- **Par dossier** : Text 100 % · Placement 100 % · Rendering 98,8 % · Theming 98,4 % · Controls 98,2 % · Models 98,2 %
  · ViewModels 95,8 % · Services 93,3 % · Converters 93,3 % · **Views 64,0 %** · **racine (App.xaml.cs) 0 %**.
- **Périmètre du brief** :
  - cycle 2 (`v1.8..HEAD`, 78 fichiers .cs) : **88,7 %** (3569 / 4022), **94,2 % sans App.xaml.cs** ;
  - cycle 1 (`937e980..v1.8`, 87 fichiers .cs) : **89,1 %** (4969 / 5578).
- **Fichiers critiques du cycle 2** : ArgumentsDemarrage 100 % · AutomateGeste 100 % · SettingsService 100 % · LectureReglages 100 %
  · ClaudeSettingsJson 100 % · ChronosSettings 100 % · ReglagesViewModel 100 % · ZoneGeste 95,7 % · ClaudeSettingsReconciler 92,0 %
  · VerrouInstanceUnique 91,5 % · JournalReleves 91,2 % · LecteurJournal 90,1 % · ArretHote 76,5 % · SourceHistoriqueDisque 61,5 %.
- **Spécificité Revit** : sans objet (aucune API Revit). L'équivalent ici, c'est WPF : `App.xaml.cs` et le code-behind des vues ne
  s'instancient pas sous `dotnet test`. La logique a été massivement sortie en classes pures (AutomateGeste, ZoneGeste, ArretHote,
  EcritureEtatSession, ArgumentsDemarrage, LectureReglages…), testées. C'est le bon signal.
- **Hygiène constatée** (grep) : aucun `Assert.True(true)`, aucun `Skip =`, chemins utilisateur isolés en temp (une exception, TEST-8),
  gardes « anti-muettes » (les gardes textuelles échouent si le chemin source injecté manque), tests de concurrence (2 écrivains × 200,
  500 battements sous lecteur concurrent), tests de cohérence après crash (panne entre deux étapes du flush, reprise octet pour octet).

## Phase 2 — Findings

```
ID            : TEST-1
Sévérité      : Mineur
Rôle émetteur : Tests (Tier 1)
Localisation  : src/Chronos/Services/ClaudeSettingsReconciler.cs:238-253, 280
Preuve        : OUTIL:Microsoft.CodeCoverage — lignes 241-243, 250-253, 280 à 0 hit ; branche l.240 50 %, l.224 25 %
                | CITATION: `if (sauvegarde is null) { DernierBilan = new BilanReconciliation(false, barre, null, "sauvegarde impossible — rien écrit"); return false; }`
Constat       : Le réconciliateur écrit dans le ~/.claude/settings.json de l'UTILISATEUR (fichier hors Chronos). Ses 28 tests couvrent bien la
                logique pure (retrait, restauration, barre tierce, fichier illisible, idempotence), mais le garde-fou central
                « sauvegarde impossible ⇒ on N'ÉCRIT PAS » n'est jamais exécuté. Le catch global et le renoncement après 100 collisions de nom non plus.
Impact        : Une régression sur ce garde-fou (inversion du test, écriture déplacée avant la sauvegarde) passerait la suite au vert et pourrait
                réécrire le settings.json de Claude Code sans copie de secours. C'est le seul chemin du cycle 2 qui modifie un fichier tiers.
Recommandation: Ajouter 2 tests, faciles car backupDir est déjà injectable : (a) backupDir = chemin d'un FICHIER existant
                (Directory.CreateDirectory lève) → Reconcile rend false, settings.json identique octet pour octet, Cause = « sauvegarde impossible — rien écrit » ;
                (b) settings.json ouvert en FileShare.None pendant l'appel → false, Cause = nom de l'exception, contenu intact.
Applicabilité : Desktop mono-utilisateur. Le fichier vise ~/.claude, partagé avec Claude Code, et 5 processus --hook peuvent y écrire.
Statut challenge : non contesté
```

```
ID            : TEST-2
Sévérité      : Mineur
Rôle émetteur : Tests (Tier 1)
Localisation  : src/Chronos/App.xaml.cs (495 lignes) ; tests/Chronos.Tests/CompositionRootTests.cs:34 ; tests/Chronos.Tests/GardesPerimetreTests.cs:731-751
Preuve        : OUTIL:Microsoft.CodeCoverage — App.xaml.cs 0 / 235 lignes, 0 / 222 branches
                | CITATION (CompositionRootTests.cs:34) : `// Reproduit ConfigureServices (App.xaml.cs) dans un conteneur de test.`
                | CITATION (GardesPerimetreTests.cs:737-738) : `texte.IndexOf("LireCommandeInterneHeritee", …)` / `texte.IndexOf("window.Show()", …)`
Constat       : La racine de composition (ConfigureServices, private static, ~50 inscriptions) et la séquence OnStartup/OnExit ne sont jamais
                exécutées. Le graphe DI est vérifié sur des MIROIRS recopiés « ligne pour ligne », et l'ordre du démarrage par des gardes
                textuelles (premier IndexOf). L'invariant réel DAT-03 est « clé héritée lue AVANT TOUT Save ». La garde vérifie seulement
                « avant window.Show() », alors que _host.StartAsync() (services hébergés) s'exécute entre les deux.
Impact        : Une inscription oubliée ou mal typée dans le vrai ConfigureServices compile et ne casse qu'au lancement de l'exe. Un miroir
                peut diverger du vrai graphe sans qu'aucun test échoue. Un Save introduit dans un service hébergé effacerait la barre
                d'origine de l'utilisateur, et la garde resterait verte.
Recommandation: Sortir ConfigureServices en `internal static class RacineComposition { static void Configurer(IServiceCollection, ChronosPaths) }`
                appelé par App ET par un test unique. Ce test résout chaque service inscrit avec des ChronosPaths temp, sous [WpfFact],
                et remplace les miroirs. Pour DAT-03 : un test de SettingsService qui prouve que la clé héritée survit jusqu'à
                LireCommandeInterneHeritee, ou une garde qui interdit tout Save atteignable depuis StartAsync.
Applicabilité : WPF : App n'est pas instanciable sous test, d'où la stratégie miroir. Elle se défend, mais extraire la composition supprime le risque de dérive.
Statut challenge : non contesté
```

```
ID            : TEST-3
Sévérité      : Mineur
Rôle émetteur : Tests (Tier 1)
Localisation  : src/Chronos/Services/ArchiveStore.cs:58-87 ; src/Chronos/ViewModels/SessionsViewModel.cs:160-164, 184-191
Preuve        : OUTIL:Microsoft.CodeCoverage — ArchiveStore l.66-72, 74 à 0 hit ; SessionsViewModel l.161-164 (ArchiveSession) et 186-191 (MarquerToutTraite) à 0 hit
                | CITATION : `try { if (File.Exists(_path)) { using var doc = JsonDocument.Parse(File.ReadAllText(_path)); … } } catch { }  map[sessionId] = now;` puis écriture de `map`
Constat       : Le chemin « archiver alors qu'une archive existe déjà », qui est le cas nominal dès la 2e archive, n'est jamais exécuté.
                Les gestes « Archiver » et « Tout marquer traité » du widget non plus. Le code montre un mode de défaillance destructeur :
                si la relecture échoue (JSON corrompu, fichier pris), `map` reste vide et le fichier est RÉÉCRIT avec la seule nouvelle session.
Impact        : Une archive corrompue ou momentanément illisible efface toutes les archives précédentes : des sessions masquées par l'utilisateur
                réapparaissent. Ce sont des données de préférence, donc perte limitée, mais silencieuse et non testée.
Recommandation: Tests : (a) Add(A) puis Add(B) → Load() contient A et B ; (b) archive au JSON tronqué puis Add(B) → décider et figer le
                comportement (ne PAS écrire si la relecture d'un fichier existant échoue, comme le fait le réconciliateur) ; (c) SessionsViewModel :
                commande Archiver → item retiré et fichier mis à jour, en réutilisant le montage d'AffichageSessionsTests.
Applicabilité : Desktop local ; fichier sous %APPDATA%\Chronos (vue MSIX possible).
Statut challenge : non contesté
```

```
ID            : TEST-4
Sévérité      : Mineur
Rôle émetteur : Tests (Tier 1)
Localisation  : src/Chronos/Services/Historique/SourceHistoriqueDisque.cs:33-35, 55-57, 77-79, 96, 102-134 ; src/Chronos/Services/Historique/LecteurJournal.cs:52-56, 127-128
Preuve        : OUTIL:Microsoft.CodeCoverage — SourceHistoriqueDisque 48 / 78 lignes (61,5 %) ; tous les `catch (Exception)` et les fabriques
                SemaineVide / JourVide / AnalyseVide à 0 hit ; LecteurJournal l.52-56 (fichier tenu → LectureFichier.Vide) à 0 hit
Constat       : La contrainte « aucune source ≠ crash → état données indisponibles » n'est pas vérifiée pour la fenêtre Historique : aucun test ne
                force une panne de lecture. Les fabriques de repli s'exécutent DANS le catch : si l'une d'elles levait, l'exception sortirait
                vers la vue. Un mois de journal verrouillé est lu comme vide, sans ligne ignorée comptée, donc indiscernable d'un mois sans relevé.
Impact        : Une régression sur le repli fait planter la fenêtre Historique au lieu de dégrader proprement. La lecture « verrouillé = vide »
                peut afficher « aucun relevé » là où il y en a, ce qui touche la doctrine « exact ou rien ». Aucun test ne fige ce choix.
Recommandation: Tests sur SourceHistoriqueDisque avec un dossier poison (fichier à la place du dossier, mois tenu en FileShare.None) :
                LireSemaine / LireJour / LireQuatreSemaines / RepereHebdo rendent une donnée vide non nulle, sans lever. Ajouter un test
                LecteurJournal « fichier tenu au-delà des reprises » qui fige le comportement attendu (vide vs signalé).
Applicabilité : Desktop ; les écrivains concurrents (overlay + reconstruction de fond) rendent le verrou transitoire réaliste sous Windows.
Statut challenge : non contesté
```

```
ID            : TEST-5
Sévérité      : Mineur
Rôle émetteur : Tests (Tier 1)
Localisation  : src/Chronos/Services/ChronosOAuthClient.cs:99-112 (ExchangeCodeAsync), 166-184 (PostTokenAsync)
Preuve        : OUTIL:Microsoft.CodeCoverage — l.100-112 et 169-184 à 0 hit ; ChronosOAuthClient 72,6 %
Constat       : L'échange du code de connexion contre des jetons n'est jamais exécuté. C'est l'entrée de la source exacte OAuth (code+state
                collé, POST, timeout 15 s, réponse non 2xx). La classe prend pourtant un HttpClient injecté, donc elle est testable :
                le rafraîchissement l'est déjà par ce moyen. SplitCodeState, lui, est couvert.
Impact        : Une régression sur le corps de requête (champ renommé, state mal découpé) ou sur la lecture de la réponse casse la connexion
                initiale sans qu'un test le voie. L'utilisateur ne voit que « code invalide/expiré ».
Recommandation: Avec un HttpMessageHandler factice : (a) « code#state » → corps JSON avec grant_type=authorization_code, code, state, code_verifier ;
                (b) 2xx valide → OAuthTokens avec expiration = horloge + expires_in ; (c) 400 / timeout / JSON sans refresh_token → null.
Applicabilité : Desktop ; appel réseau vers l'endpoint OAuth Anthropic, pas de serveur propre.
Statut challenge : non contesté
```

```
ID            : TEST-6
Sévérité      : Mineur
Rôle émetteur : Tests (Tier 1)
Localisation  : src/Chronos/Services/ArretHote.cs:51-63, 80 ; src/Chronos/App.xaml.cs:211-229 (OnExit) ; tests/Chronos.Tests/ArretHoteTests.cs:309
Preuve        : OUTIL:Microsoft.CodeCoverage — ArretHote 76,5 %, l.55-62 (catch AggregateException / Exception) à 0 hit, branche l.40 à 25 %
                | CITATION (ArretHoteTests.cs:309) : `Assert.Contains("ArretHote.Arreter(", app, StringComparison.Ordinal);`
Constat       : L'arrêt borné est bien testé pour le cas « service récalcitrant » (délai respecté, cause non vide). Le cas « StopAsync ou
                Dispose LÈVE » ne l'est pas. Le câblage d'OnExit (libération du mutex sur le thread UI, puis Environment.Exit si l'arrêt a
                dépassé son délai) n'est vérifié que par la présence d'une sous-chaîne.
Impact        : C'est la zone de l'incident « processus zombie qui garde le mutex » (27/09). Une régression dans l'ordre de sortie d'OnExit
                (Exit avant Liberer, ou Arreter rappelé sur le thread UI) ne serait pas détectée.
Recommandation: Test ArretHote avec un IHostedService dont StopAsync lève → false, cause « arrêt du Host en échec : … ». Extraire la séquence
                d'OnExit dans une méthode pure (arrêter → signaler → libérer → décider la sortie forcée), avec des délégués injectés, et tester l'ordre.
Applicabilité : Desktop ; mutex Local\ par session Windows, sortie forcée si le Host ne rend pas la main.
Statut challenge : non contesté
```

```
ID            : TEST-7
Sévérité      : Mineur
Rôle émetteur : Tests (Tier 1)
Localisation  : tests/Chronos.Tests (37 fichiers lisant le texte de src/ ou docs/ via AssemblyMetadata CheminSourcesChronos / CheminDocsChronos)
Preuve        : OUTIL:grep — 37 fichiers de tests référencent CheminSources / CheminDocs ; exemples GardesPerimetreTests.cs:731-751, ArretHoteTests.cs:282-310
                | CITATION : `var iShow = texte.IndexOf("window.Show()", StringComparison.Ordinal);`
Constat       : Une part notable des « gardes » vérifie la FORME du code source (regex, premier IndexOf, présence de sous-chaînes) et non un
                comportement. Elles sont bien protégées contre le mutisme (chemin absent ⇒ échec), mais elles sont couplées à la mise en
                forme : un commentaire placé plus haut qui contient le repère satisfait IndexOf, et un renommage ou un reformatage les casse sans régression réelle.
Impact        : Faux verts possibles (le repère est trouvé dans un commentaire) et faux rouges au refactor. Le coût de maintenance monte avec
                le nombre de gardes, et la confiance qu'on leur accorde dépasse ce qu'elles prouvent.
Recommandation: Réserver les gardes textuelles aux interdits globaux (« / 100 », Assembly.Location, GetResult). Pour les invariants
                d'ordre et de câblage, préférer un test comportemental après extraction (cf. TEST-2, TEST-6). Faute de mieux, retirer
                commentaires et chaînes du texte avant d'appliquer IndexOf.
Applicabilité : Contrainte WPF réelle (App non instanciable). Le choix est assumé et documenté dans les tests ; c'est sa portée qui pose problème.
Statut challenge : non contesté
```

```
ID            : TEST-8
Sévérité      : Info
Rôle émetteur : Tests (Tier 1)
Localisation  : tests/Chronos.Tests/OverlayWindowConfigTests.cs:22-28
Preuve        : CITATION : `var settings = new SettingsService(ChronosPaths.Default());` (×3), et le constructeur de MainViewModel appelle `settings.Load()` (MainViewModel.cs:459)
Constat       : Ce test lit le VRAI %APPDATA%\Chronos\settings.json du poste. C'est une lecture seule : aucun Save n'est atteint par les
                constructeurs. Le reste de la suite isole systématiquement ces chemins (« GARDE ANTI-ACCIDENT », CompositionRootTests.cs:43).
Impact        : Test non hermétique : son résultat dépend de l'état du poste du développeur. Aucune écriture constatée.
Recommandation: `ChronosPaths.Default() with { UsageFile = <temp> }`, comme dans CompositionRootTests.
Applicabilité : Poste de dev unique ; sans effet en CI propre.
Statut challenge : non contesté
```

```
ID            : TEST-9
Sévérité      : Info
Rôle émetteur : Tests (Tier 1)
Localisation  : tests/Chronos.Tests/ReconstructionTokensTests.cs:341, 375 ; ArretHoteTests.cs:137, 275 ; CompositionRootTests.cs:672
Preuve        : CITATION : `Assert.True(chrono.ElapsedMilliseconds < 50, …)` ; `… < 200` ; `Elapsed < TimeSpan.FromSeconds(2)`
Constat       : Cinq assertions portent sur un temps mesuré à l'horloge réelle. Elles sont passées lors de cette exécution, alors que
                d'autres builds tournaient en parallèle sur le poste.
Impact        : Risque de rouges intermittents sur une machine chargée (antivirus, CI partagée). Aucune instabilité observée.
Recommandation: Garder les bornes « arrêt borné » (c'est le comportement testé), mais élargir la marge sur le seuil de 50 ms, ou le
                remplacer par une preuve structurelle (la passe ne tourne pas sur le thread appelant : comparer ManagedThreadId).
Applicabilité : Desktop / CI Windows.
Statut challenge : non contesté
```

```
ID            : TEST-10
Sévérité      : Info
Rôle émetteur : Tests (Tier 1)
Localisation  : src/Chronos/Views/SessionsController.cs (0 / 71), src/Chronos/Views/OAuthLogin.cs (0 / 79), src/Chronos/Views/MainWindow.xaml.cs:66-76, 112-124
Preuve        : OUTIL:Microsoft.CodeCoverage — 0 hit sur ces plages ; AutomateGeste 100 %, ZoneGeste 95,7 %
Constat       : Ces vues servent de colle entre l'UI et la logique (« humble object ») : la décision du geste unique est sortie dans AutomateGeste
                (13 tests, dont la réentrance de DragMove et la perte de capture). Restent non testés : le recalage sur SizeChanged (garde _enDeplacement)
                et SessionsController.Enable, qui installe les hooks dans ~/.claude/settings.json AVANT de persister le drapeau, sans test d'ordre ni d'échec partiel.
Impact        : Faible. La logique coûteuse est testée ailleurs (SessionHookInstaller 99,1 %).
Recommandation: Si l'on veut fermer le dernier trou : interface ISessionsUi (MessageBox / fenêtre) injectée dans SessionsController, puis
                un test « Install lève → drapeau non persisté ».
Applicabilité : WPF.
Statut challenge : non contesté
```

```
ID            : TEST-11
Sévérité      : Info
Rôle émetteur : Tests (Tier 1)
Localisation  : tests/Chronos.Tests/AutostartServiceTests.cs:64-73 ; tests/Chronos.Tests/ArgumentsDemarrageTests.cs:15-41
Preuve        : CITATION : `public void Enable_cree_un_lnk_ciblant_ProcessPath()` n'asserte que `File.Exists(… "Chronos.lnk")`
Constat       : Le nom du test promet une vérification de la cible du raccourci, que le test ne fait pas. Dans ArgumentsDemarrage (29 cas,
                très complet), le cas `--hook --cadrans` n'est pas couvert : le code rend Hook avec l'événement « --cadrans ».
Impact        : Assertion plus faible que son intitulé. Cas limite d'arguments non figé.
Recommandation: Lire TargetPath du .lnk (WScript.Shell) et le comparer à Environment.ProcessPath. Ajouter l'InlineData `--hook --cadrans`
                avec la sémantique voulue.
Applicabilité : Desktop Windows (shell:startup).
Statut challenge : non contesté
```

## Synthèse

- **Couverture globale** : **vérifiée** avec Microsoft.CodeCoverage (Coverlet absent du projet, donc NON VÉRIFIÉE au sens strict de l'outil) :
  **90,3 % des lignes** de src/Chronos, **83,2 % des branches**. Tests : **1949 / 1949 verts**.
- **Répartition critique / non critique** :
  - critique, bien couvert : parsing tolérant (LigneJournal 100 %, LigneAgregat 98,6 %, LecteurTranscript 89 %), réglages
    (SettingsService / LectureReglages / ChronosSettings 100 %), ClaudeSettingsJson 100 %, tri des arguments 100 %, automate de geste 100 %,
    déduplication (DedupUsage 95,7 %), agrégats et reconstruction des tokens (92-96 %, avec tests de panne entre étapes et de reprise octet pour octet),
    journal des relevés (91 %, avec concurrence).
  - critique, trous ciblés : garde-fou de sauvegarde du réconciliateur (TEST-1), composition et séquence du démarrage et de l'arrêt (TEST-2, TEST-6),
    fusion de l'archive (TEST-3), replis de l'Historique (TEST-4), échange OAuth (TEST-5).
  - non critique, peu couvert : code-behind des vues (Views 64 %), DiagnosticService (86 %), galeries.
- **Testabilité** : bonne. La logique est séparée de WPF par des classes pures et des interfaces (IClock, IUiDispatcher, ISourceHistorique,
  IPressePapiers, HttpClient injecté). Le seul îlot non testable est App.xaml.cs.
- **Qualité des assertions** : élevée (comportements, cas limites, concurrence, crash-consistency, anti-mutisme). Bémol : la place
  prise par les gardes textuelles (TEST-7).
- **Compte par sévérité** : Bloquant 0 · Majeur 0 · Mineur 7 (TEST-1 à TEST-7) · Info 4 (TEST-8 à TEST-11).
