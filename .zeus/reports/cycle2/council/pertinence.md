# Audit dev-team-council — Pertinence / Product (Tier 3) — Chronos cycle 2 (2026-10-03)

Rôle : avocat anti-scope-creep. Je PROPOSE des candidats au retrait ; le dernier mot revient à l'utilisateur.
Lecture seule (seul ce fichier est écrit). L'exe n'a jamais été lancé.

## Vérité-terrain (outils)

- `dotnet build` (obj/out isolés dans le scratchpad, `EnforceCodeStyleInBuild=true`, `AnalysisModeStyle=All`) :
  0 CS0169 / CS0414 ; **IDE0052 ×3**, **IDE0060 ×1** (voir PERT-5). Build par défaut : 0 avertissement.
- Projets : la solution a 2 projets (Chronos, Chronos.Tests), aucun orphelin.
- Recherche de références par nom (src hors obj/bin, commentaires retirés) : 14 membres publics ou internes ne sont
  appelés que par leur déclaration ou par les tests (PERT-2 à PERT-5, PERT-14).
- Reliquats de la liste de purge validée (`liste-purge.md`) : `StatusLineBridge`, `WeeklyRecalibration`, `ClaudeTokenReader`,
  `FiveHourWindowInference`, `OAuthUsageEnabled`, `EndpointOAuthClaude`, `PontStatusLine` → **0 occurrence dans src**
  (purge bien exécutée). **Deux reliquats subsistent** : `scripts/` (PERT-1) et `ArchiveStore.PurgerPrefixe` (PERT-2).
- Aucun `TODO` / `FIXME` / `HACK` dans src ; aucun gros bloc de code commenté.

---

### PERT-1 — Le pont statusLine en Node survit dans `scripts/`, et son README dit qu'il est la « source primaire »
- **Sévérité** : Mineur
- **Rôle** : Pertinence / Product
- **Localisation** : `scripts/README.md`, `scripts/chronos-statusline-bridge.js`, `scripts/install-bridge.mjs`,
  `scripts/fixtures/statusline-input.json` (suivis par git, inchangés depuis 1e8e0f3 / 1dbb829, juillet)
- **Preuve** : `scripts/README.md` l.1-4 : « Ce dossier contient la **source primaire** de Chronos : le pont qui materialise
  le bloc `rate_limits`… dans… `usage.json` ». `install-bridge.mjs` écrit une clé `statusLine` dans `~/.claude/settings.json`.
  `ChronosPaths` (l.7) : « usage.json n'est plus écrit depuis la 3.5 ». `liste-purge.md` §2 et §8 retirent le pont
  et réécrivent les docs.
- **Constat** : la purge de la phase 37 a retiré le pont C# (`--statusline`, `StatusLineBridge`, `StatusLineInstaller`)
  mais pas son ancêtre en Node, ni sa doc. Le dossier décrit une architecture abandonnée, présentée comme actuelle.
- **Impact** : doc fausse dans le dépôt (exactement ce que §8 voulait supprimer). Si quelqu'un lance
  `node scripts/install-bridge.mjs`, il réinstalle une barre qui alimente un fichier que personne ne lit.
  `ClaudeSettingsReconciler.RetirerBarreChronos` ne la retire pas : elle ne porte pas le marqueur `--statusline` + `Chronos*.exe`,
  donc elle est classée `Tierce` et laissée en place.
- **Recommandation** (question) : ce dossier `scripts/` sert-il encore à quelque chose ? Sinon, est-il à ajouter à la purge
  (4 fichiers, aucun appelant) ?
- **Applicabilité** : immédiate, sans risque pour l'exe (aucune référence depuis src ni tests).
- **Statut challenge** : non challengé

### PERT-2 — `ArchiveStore.PurgerPrefixe` : la migration a été retirée (§7), la méthode est restée
- **Sévérité** : Mineur
- **Rôle** : Pertinence / Product
- **Localisation** : `src/Chronos/Services/ArchiveStore.cs:110` (+ doc l.18-20) ; `tests/Chronos.Tests/ArchiveStorePurgeTests.cs`
- **Preuve** : `grep "PurgerPrefixe(" src` → seule la déclaration. Les autres mentions sont des commentaires qui la citent comme
  « précédent » (`BalayageMagasinSessions.cs:9,24`, `EcritureEtatSession.cs:61`). 5 tests l'appellent avec `"desktop:"`.
- **Constat** : `liste-purge.md` §7 a retiré l'appel ponctuel `PurgerPrefixe("desktop:")` (ancienne source UIA). La méthode
  (environ 35 lignes), sa doc de contrat et ses tests ne servent plus qu'à eux-mêmes.
- **Impact** : faible, mais c'est un reliquat d'une purge qui se voulait complète. Il fige aussi le vocabulaire « desktop: » dans les tests.
- **Recommandation** (question) : voyez-vous un usage futur d'une purge par préfixe ? Sinon, candidate au retrait avec ses tests
  (les commentaires « précédent PurgerPrefixe » seraient à reformuler).
- **Applicabilité** : immédiate.
- **Statut challenge** : non challengé

### PERT-3 — Deux chemins de diagnostic morts : `DiagnosticService.RunAsync` et `MainViewModel.BuildDiagnosticReportAsync`
- **Sévérité** : Mineur
- **Rôle** : Pertinence / Product
- **Localisation** : `src/Chronos/Services/DiagnosticService.cs:120-136` ; `src/Chronos/ViewModels/MainViewModel.cs:869-871`
- **Preuve** : 0 appelant dans src, 0 dans tests pour les deux. `RunAsync` écrit `diagnostic.txt` puis l'ouvre avec
  `Process.Start` : c'est le comportement remplacé par le pop-up en 4fec98c, puis par l'affichage dans la fenêtre de réglages en 3.4.
  Le seul chemin vivant est `ReglagesViewModel` → `Task.Run(() => _diagnostic.BuildReportAsync())` (MainViewModel.cs:464).
- **Constat** : deux façons de produire le rapport, héritées de versions successives. Aucune n'est atteinte.
- **Impact** : du code mort sur un service de 999 lignes. `RunAsync` garde un `Process.Start` (lancement de l'éditeur par défaut)
  qui n'est plus jamais exécuté.
- **Recommandation** (question) : l'export du diagnostic dans un fichier ouvert automatiquement est-il voulu ? Sinon, les deux méthodes sont candidates au retrait.
- **Applicabilité** : immédiate.
- **Statut challenge** : non challengé

### PERT-4 — Convertisseurs morts : `FractionVersLargeurConverter`, `UtilizationToBrushConverter`
- **Sévérité** : Mineur
- **Rôle** : Pertinence / Product
- **Localisation** : `src/Chronos/Converters/HistoriqueConverters.cs:61` ; `src/Chronos/Converters/UtilizationToBrushConverter.cs` ;
  `src/Chronos/Resources/DesignTokens.xaml:231` ; `src/Chronos/Rendering/RampColor.cs:18`
- **Preuve** : `FractionVersLargeurConverter` n'apparaît dans aucun XAML et n'est déclaré dans aucune ressource (1 occurrence dans src,
  1 test). `UtilizationToBrushConverter` est déclaré `x:Key="UtilBrush"`, mais la clé `UtilBrush` n'est consommée nulle part. Le code
  le reconnaît lui-même (l.18) : « ce convertisseur n'est utilisé par aucun XAML (le thème actif passe par ChronosTheme.ArcBrush) ».
  La surcharge `RampColor.Interpolate(double)` (couleurs codées en dur) n'a que ce convertisseur et des tests pour appelants.
- **Constat** : il en résulte deux rampes de couleur parallèles. La vivante dépend du thème (`ChronosTheme.ArcBrush`), la morte
  est figée sur le thème par défaut.
- **Impact** : faible. Une seconde rampe peut toutefois être reprise par erreur et ignorer le thème actif.
- **Recommandation** (question) : ces deux convertisseurs (et la surcharge à couleurs fixes) ont-ils un usage prévu ? Sinon,
  candidats au retrait avec leurs tests.
- **Applicabilité** : immédiate.
- **Statut challenge** : non challengé

### PERT-5 — Membres inutilisés signalés par l'analyseur, plus un utilitaire d'installation sans appelant
- **Sévérité** : Mineur
- **Rôle** : Pertinence / Product
- **Localisation** : `Services/SessionHookProcessor.cs:42` et `Services/SessionMonitor.cs:89` (`Tolerant`, IDE0052) ;
  `ViewModels/SessionsViewModel.cs:135` (`_theme`, IDE0052 : écrit dans `SetTheme`, jamais lu) ;
  `Services/TranscriptSessionSource.cs:165` (paramètre `now` de `Classify`, IDE0060) ;
  `Services/SessionHookInstaller.cs:143` (`IsInstalled`, 0 appelant en production, 1 test)
- **Preuve** : sortie de `dotnet build -p:EnforceCodeStyleInBuild=true -p:AnalysisModeStyle=All`, et `grep -w IsInstalled src`
  qui ne trouve que la déclaration. La fraîcheur des hooks est maintenant décidée par `ClaudeSettingsReconciler`.
- **Constat** : ce sont des résidus de refactorisations successives (options JSON dupliquées puis abandonnées, thème mémorisé
  « au cas où », question de fraîcheur reprise ailleurs).
- **Impact** : négligeable à l'exécution ; c'est du bruit à la lecture.
- **Recommandation** (question) : `IsInstalled` sert-il encore à un futur écran ou diagnostic ? Les quatre autres
  ne semblent avoir aucune raison d'être.
- **Applicabilité** : immédiate.
- **Statut challenge** : non challengé

### PERT-6 — `ChronosPaths.UsageFile` : le dossier de Chronos est encore déduit d'un fichier supprimé
- **Sévérité** : Info
- **Rôle** : Pertinence / Product
- **Localisation** : `src/Chronos/Services/ChronosPaths.cs:7-37` ; `DiagnosticService.cs:218,375` ; 4 fichiers de tests
- **Preuve** : `record ChronosPaths(string UsageFile, string ProjectsRoot)`. Tous les chemins utiles (`SettingsFile`, `LastExactFile`,
  `HistoriqueDir`, `oauth.dat`, `sessions`) sont calculés par `Path.GetDirectoryName(UsageFile)`. La remarque l.7 l'admet :
  « usage.json n'est plus écrit depuis la 3.5 ; la propriété ancre le dossier ».
- **Constat** : le nom du paramètre racine désigne un fichier mort (pont statusLine). Le contrat réel est « le dossier %APPDATA%\Chronos ».
- **Recommandation** (question) : faut-il renommer la racine en dossier (par exemple `Dossier`) maintenant que `usage.json`
  n'existe plus ? C'est un changement de forme, à faire seulement si le coût sur les tests est jugé acceptable.
- **Applicabilité** : différable.
- **Statut challenge** : non challengé

### PERT-7 — Le retrait de la barre statusLine est une migration ponctuelle exécutée à chaque lancement, sans fin prévue
- **Sévérité** : Info
- **Rôle** : Pertinence / Product
- **Localisation** : `src/Chronos/Services/ClaudeSettingsReconciler.cs:108-203` (`CleCommandeHeritee`, `LireCommandeInterneHeritee`,
  `RetirerBarreChronos`) ; `App.xaml.cs:103-134` ; section « [Réglages de Claude Code] » du diagnostic
- **Preuve** : la clé héritée `InnerStatusLineCommand` (réglages ≤ 3.4) est lue à chaque démarrage, et la barre Chronos est
  recherchée à chaque réconciliation. Application mono-utilisateur : une fois la 3.5 lancée sur la machine, cela ne fait plus rien.
- **Constat** : c'est le même cas que `PurgerPrefixe("desktop:")`, que la purge a retiré au motif « ne fait plus rien ».
- **Recommandation** (question) : faut-il fixer une échéance (par exemple « retirée en v4, une fois la barre disparue
  de `settings.json` ») ? Dans l'immédiat, garder : c'est la protection de la phase 37.
- **Applicabilité** : différable (pas avant d'avoir constaté la barre retirée sur la machine).
- **Statut challenge** : non challengé

### PERT-8 — `WeeklyAnchor` : un réglage que plus rien n'écrit, lu en repli par l'Historique
- **Sévérité** : Info
- **Rôle** : Pertinence / Product
- **Localisation** : `Services/ChronosSettings.cs` (`WeeklyAnchor`) ; `ViewModels/Historique/HistoriqueViewModel.cs:73,269` ;
  `Services/Historique/BornesPlage.cs:17`
- **Preuve** : aucune affectation de `WeeklyAnchor` dans src depuis le retrait du recalibrage (§3). Ordre de lecture :
  `resets_at` 7 j du journal, puis `WeeklyAnchor`, puis le calendrier.
- **Constat** : ce repli ne sert que si le journal ne contient aucun reset hebdomadaire ET qu'une ancre ancienne existe.
  Sur la machine, la sonde alimente le journal depuis septembre.
- **Recommandation** (question) : ce maillon intermédiaire mérite-t-il sa place, ou le repli calendrier suffit-il ? Si oui, le réglage
  et le maillon sont candidats au retrait (lecture tolérante : la clé disparaîtrait au prochain Save, comme `HistoriqueStyleSemaine`).
- **Applicabilité** : différable.
- **Statut challenge** : non challengé

### PERT-9 — Trois galeries de revue livrées dans l'exe de production, dont une recoupe l'aperçu des réglages
- **Sévérité** : Info
- **Rôle** : Pertinence / Product
- **Localisation** : `App.xaml.cs:40-75` (modes `--cadrans`, `--sessions`, `--historique`) ; `Views/CadranGalleryWindow.xaml(.cs)` (236 l.),
  `ViewModels/CadranPreviewViewModel.cs` (98), `Views/SessionsGalleryWindow.xaml(.cs)` (141), `ViewModels/SessionsPreviewViewModel.cs` (85),
  `Views/Historique/HistoriqueGalerie.cs`, `ViewModels/Historique/ScenariosHistorique.cs` (242), `SourceHistoriqueDemonstration.cs` (61)
- **Preuve** : ces modes sont dans la liste blanche `ArgumentsDemarrage.ModesConnus`. Ils servent activement la revue visuelle ZEUS
  (`design-review-1/2.md`, 59 rendus r2), et `ScenariosHistorique` / `SourceHistoriqueDemonstration` sont utilisés par 13 et 10 fichiers
  de tests. `ReglagesViewModel.ApercuSessions` réutilise `SessionsPreviewViewModel` : l'aperçu vivant des réglages montre déjà
  le style de sessions choisi avec des données d'échantillon.
- **Constat** : ce ne sont PAS des reliquats, puisqu'elles ont un usage actuel. En revanche, environ 860 lignes d'outillage de conception
  et de données de démonstration vivent dans l'assembly livré. La galerie `--sessions` recoupe en partie l'aperçu des réglages.
- **Recommandation** (question) : (a) les trois galeries doivent-elles rester accessibles à l'utilisateur final, ou ne servir
  qu'au développement ? (b) `--sessions` apporte-t-elle encore quelque chose que l'aperçu des réglages ne montre pas (les 8 styles
  côte à côte) ? À trancher après la fin des revues visuelles du cycle, pas avant.
- **Applicabilité** : différable.
- **Statut challenge** : non challengé

### PERT-10 — Combinatoire d'apparence : 8 styles de sessions et 5 cadrans × orientations × modes × 15 thèmes
- **Sévérité** : Info
- **Rôle** : Pertinence / Product
- **Localisation** : `Services/ChronosSettings.cs` (`CadranStyle`, `OrientationFusible/Maree/Volets`, `CadranMode`, `SessionStyle`,
  `VerticalLayout`) ; `Resources/SessionStyles.xaml` (418 l., 8 gabarits) ; `Controls/Cadrans/*`, `Views/Cadrans/*`
- **Preuve** : les 8 styles de sessions issus de l'idéation llm-council (juillet) ont tous été intégrés et proposés dans les réglages
  (`MainViewModel.cs:490-495`). `VerticalLayout` ne s'applique qu'à 3 d'entre eux. La revue visuelle du cycle 2 a dû produire 59 rendus pour
  couvrir l'espace.
- **Constat** : chaque variante multiplie le coût de chaque évolution transverse (nouveau thème, nouvel état de session, geste unique,
  échelle 1). La mémoire projet note une shortlist, mais pas de choix définitif parmi les styles de sessions.
- **Recommandation** (question) : utilisez-vous réellement plus d'un ou deux styles de sessions, et plus de deux ou trois cadrans ?
  Si non, les variantes jamais choisies sont candidates au retrait (ou au gel), ce qui allégerait chaque revue visuelle.
- **Applicabilité** : décision produit ; aucune action technique avant votre réponse.
- **Statut challenge** : non challengé

### PERT-11 — `RefreshIntervalSeconds` : réglage persisté sans interface, défaut 60 s
- **Sévérité** : Info
- **Rôle** : Pertinence / Product
- **Localisation** : `Services/ChronosSettings.cs` ; `App.xaml.cs:464`
- **Preuve** : lu une seule fois pour construire `RefreshOptions` ; aucune UI (« décision verrouillée », 06-03). La sonde d'en-têtes
  a sa propre cadence (1 requête / 5 min selon `liste-purge.md`).
- **Recommandation** (question) : quelqu'un modifie-t-il ce réglage à la main dans `settings.json` ? Sinon, une constante suffirait
  (une option de configuration en moins à documenter et à tester).
- **Applicabilité** : différable.
- **Statut challenge** : non challengé

### PERT-12 — Le nom `SourceReliability.Estimated` survit alors qu'il ne désigne plus que le plancher « ≥ X % »
- **Sévérité** : Info
- **Rôle** : Pertinence / Product
- **Localisation** : `src/Chronos/Models/SourceReliability.cs` (5 usages de `Estimated`)
- **Preuve** : le commentaire du type précise que `Estimated` = « plancher ≥ X %… (seul chiffre non exact) ». L'estimation absolue
  a été supprimée (doctrine v1.5, mémoire projet).
- **Constat** : le vocabulaire d'une capacité retirée (estimer) reste dans un type central. C'est un reliquat de nommage, qui va
  à rebours de « ne jamais présenter une estimation ».
- **Recommandation** (question) : faut-il renommer en `Plancher` pour aligner le code sur la doctrine ? C'est cosmétique, à faire seulement si la
  vague de purge rouvre ce fichier.
- **Applicabilité** : différable.
- **Statut challenge** : non challengé

### PERT-13 — Petits fossiles hors code exécutable
- **Sévérité** : Info
- **Rôle** : Pertinence / Product
- **Localisation** : `src/Chronos/{Controls,Converters,Interop,Models}/.gitkeep` (suivis, dossiers non vides) ; `src/Chronos/Chronos.csproj`
  (commentaire « Phase 21 (SRC-01) », qui documente l'absence d'une référence qui n'a jamais existé) ; 7 exe `Chronos-v3.1.0…v3.4.0.exe`
  à la racine (environ 540 Mo, non suivis) ; `src/Chronos/obj/Release/.../RecalibrationDialog.g.cs` (artefact périmé, non suivi)
- **Preuve** : `git ls-files` ; `ls -la *.exe` ; contenu du csproj.
- **Recommandation** (question) : ces éléments sont-ils gardés volontairement (archives des exe livrés par exemple) ? Sinon, ce sont
  des candidats au ménage, sans effet sur le produit.
- **Applicabilité** : immédiate, sans risque.
- **Statut challenge** : non challengé

### PERT-14 — API publiques qui n'ont que des tests pour appelants
- **Sévérité** : Info
- **Rôle** : Pertinence / Product
- **Localisation** : `MagasinAgregats.TranchesDuMois` (l.175), `MagasinAgregats.ChargerMois` (l.120, public ; l'interne passe par
  `ChargerMoisSousVerrou`), `LectureAgregats.EtatA` (l.66), `RenduLocalTokens.PartSousAgents` (l.85), `RacinesEtat.PremiereExistante` (l.91),
  `InventaireProcessus.Filtrer` (l.33)
- **Preuve** : comptage par nom dans src (hors commentaires) : déclaration seule ; 1 ou 2 fichiers de tests chacun.
- **Constat** : il peut s'agir de points d'entrée de test légitimes (fonctions pures extraites) ou de restes de fonctionnalités
  (par exemple la part sous-agents des tokens n'est affichée nulle part ?). Je ne peux pas trancher sans l'intention produit.
- **Recommandation** (question) : la part sous-agents des tokens (`PartSousAgents`) devait-elle apparaître dans l'Historique ?
  Si c'est abandonné, cette méthode et ses tests sont candidats au retrait. Les autres peuvent rester comme points d'entrée de test.
- **Applicabilité** : différable.
- **Statut challenge** : non challengé

---

## Compte par sévérité

| Sévérité | Nombre | IDs |
|---|---|---|
| Bloquant | 0 | — |
| Majeur | 0 | — |
| Mineur | 5 | PERT-1, PERT-2, PERT-3, PERT-4, PERT-5 |
| Info | 9 | PERT-6 à PERT-14 |

Verdict de pertinence : la purge de la phase 37 est très largement réussie (0 occurrence des 15 symboles retirés dans src).
Il reste deux reliquats directs (le pont Node dans `scripts/`, `PurgerPrefixe`), du code mort ponctuel issu de refactorisations
plus anciennes (diagnostic, convertisseurs, membres inutilisés), et des questions produit sur l'étendue des variantes d'apparence
et des galeries de revue.
