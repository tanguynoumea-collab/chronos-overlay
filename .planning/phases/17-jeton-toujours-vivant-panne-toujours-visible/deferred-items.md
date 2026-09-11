# Éléments différés — Phase 17

Découvertes HORS PÉRIMÈTRE des plans de la phase. Aucune n'a été corrigée ici (règle de portée :
n'auto-corriger que ce que la tâche courante a causé).

## Découvert au plan 17-03

### 1. La barre de progression de `STATE.md` reste figée à 0 %

- **Constat :** `gsd-tools state update-progress` rend `{ percent: 83, bar: "[████████░░] 83%" }` (10/12
  plans) mais n'écrit ni le champ `percent:` du frontmatter (resté à `0`) ni la ligne
  `Progress: [░░░░░░░░░░] 0% (0/6 phases)`. Les deux métriques ne parlent d'ailleurs pas de la même
  chose : l'outil compte des **plans**, la ligne de texte annonce des **phases**.
- **Pré-existant :** oui — `percent: 0` figurait déjà dans `STATE.md` avec `completed_plans: 9` au terme
  du plan 17-02, et la ligne `0% (0/6 phases)` est inexacte depuis la clôture des phases 15 et 16.
- **Impact :** purement cosmétique. Aucun effet sur l'exécution, les tests ou le produit.
- **Pourquoi non corrigé ici :** éditer `STATE.md` à la main contournerait l'outil qui en est
  propriétaire, et le décalage réapparaîtrait au plan suivant. La correction appartient à l'outillage
  GSD, pas au code de Chronos.

### 2. `DiagnosticService.cs:220` remonte encore sur le `grep` « aucun jeton en clair »

- **Constat :** la ligne écrit le **nom** du champ (`claudeAiOauth.accessToken`) suivi de
  « PRÉSENT »/« absent », **jamais la valeur**. Le critère de vérification de chaque plan de la phase la
  signale donc systématiquement.
- **Pré-existant :** oui — déjà relevé aux plans 17-01 et 17-02, jamais touché.
- **Impact :** aucun risque de fuite. Le critère `grep` est simplement plus large que son intention.
- **Pourquoi non corrigé ici :** `DiagnosticService.cs` est hors du périmètre de 17-03 ; le plan 17-04
  le modifie (« diagnostic qui nomme l'état réel ») et pourra reformuler cette ligne à cette occasion.

## Découvert au plan 17-05

### 3. La XML-doc de `RefreshOrchestrator.TryTrigger` (ligne 105) affirme le contraire de la réalité

- **Constat :** elle annonce « Retourne false si un rafraîchissement est déjà en file (DropWrite) ».
  C'est **faux** : avec `BoundedChannelFullMode.DropWrite`, `TryWrite` **abandonne l'élément entrant et
  renvoie `true`**. Seul le mode `Wait` renvoie `false`. Découvert en écrivant une garde de test qui
  s'appuyait sur cette doc : elle était **muette** (verte quoi qu'il arrive), et il a fallu la remplacer
  par une preuve de bout en bout (orchestrateur démarré, comptage des `GetAsync`).
- **Pré-existant :** oui — la doc date du plan 04-01, jamais relue depuis.
- **Impact :** aucun effet fonctionnel (le comportement de coalescence est correct et testé par
  `RefreshOrchestratorTests`). Le risque est de faire écrire, à quiconque s'y fie, une garde de test
  qui ne garde rien — c'est exactement ce qui s'est produit ici.
- **Pourquoi non corrigé ici :** `RefreshOrchestrator.cs` n'est pas dans le périmètre du plan 17-05
  (règle de portée) et le corriger ferait apparaître un fichier hors plan dans le diff. Correction
  d'une ligne, à saisir au prochain plan qui touche ce fichier — ou en `/gsd:quick`.

### 4. Les bindings ne s'évaluent pas sur une fenêtre WPF jamais affichée

- **Constat :** une `Window` construite sans `Show()` n'a pas de template appliqué, donc son `Content`
  n'a **aucun parent visuel** : le `DataContext` ne se propage pas et **aucun** binding ne s'évalue
  (`Command` reste `null`, `Visibility` reste à son défaut `Visible`). Tout test d'affichage qui
  asserte une valeur bindée sur une telle fenêtre est vert pour de mauvaises raisons.
- **Pré-existant :** oui — les smoke tests XAML existants (`CadranBindingTests`, `ThemingTests`)
  n'assertent que l'état du ViewModel et l'absence d'exception, donc aucun n'est faux. Mais rien ne
  documentait le piège.
- **Contournement retenu en 17-05 :** poser le `DataContext` sur la grille racine (ses enfants SONT des
  enfants visuels), **purger la file du Dispatcher** (`Invoke(..., ApplicationIdle)` — une réévaluation
  déclenchée par un changement de `DataContext` est une opération différée), puis `Measure`/`Arrange`.
  Consigné dans la XML-doc de `CadranBindingTests.MonterPastille`.
- **Pourquoi non généralisé ici :** rétrofitter ce montage sur les smoke tests existants les ferait
  passer d'« aucune exception » à « valeurs bindées vérifiées » — un gain réel, mais hors périmètre du
  plan 17-05. Candidat naturel pour la phase 20 (rendu visible).
