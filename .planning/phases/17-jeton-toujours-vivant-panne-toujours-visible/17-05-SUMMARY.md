---
phase: 17-jeton-toujours-vivant-panne-toujours-visible
plan: 05
subsystem: ui
tags: [wpf, mvvm, xaml, communitytoolkit-mvvm, design-tokens, theming, oauth, overlay, xunit, wpffact]

# Dependency graph
requires:
  - phase: 17-jeton-toujours-vivant-panne-toujours-visible
    provides: "17-04 : autorité de jeton câblée dans le graphe DI, IAuthStatus aliasé sur la MÊME instance, SignalerSucces sur chaque 2xx, et ReinitialiserApresLogin SANS AUCUN APPELANT"
  - phase: 17-jeton-toujours-vivant-panne-toujours-visible
    provides: "17-03 : verrou d'état « Deconnecte » + recul dans l'autorité — c'est ce que ReinitialiserApresLogin relâche"
  - phase: 17-jeton-toujours-vivant-panne-toujours-visible
    provides: "17-02 : EtatAuthentification (4 valeurs) et IAuthStatus, contrats neutres sans type WPF"
  - phase: 16-fondations-du-delta
    provides: "collection xUnit « XAML WPF » (DisableParallelization) contre la course du chargeur BAML ; isolation des chemins sous Path.GetTempPath()"
provides:
  - "La panne d'authentification est VISIBLE : pastille ambre en bas-droite du cadran, dès le premier tick, pour les 5 styles et les 2 modes"
  - "La panne est RÉPARABLE EN UN CLIC sans risque : ReconnecterCommand dédiée, monodirectionnelle, qui n'appelle jamais Logout()"
  - "Premier appelant de ReinitialiserApresLogin : la pastille ne survit plus à sa propre réparation"
  - "Distinction visuelle « hors ligne » (gris, inerte) / « déconnecté » (ambre, cliquable) — Chronos ne crie jamais « reconnecte-toi » à qui a juste coupé le wifi"
  - "Token de design Alerte dans DesignTokens.xaml et dans les 9 thèmes"
  - "Seconde et DERNIÈRE frontière de thread du ViewModel (RAF-04) : grep « _ui.Post » → exactement 2"
  - "Garde XAML anti-piège : un [WpfFact] prouve que la pastille est bindée sur ReconnecterCommand et NON sur la commande bascule"
  - "Garde GÉOMÉTRIQUE automatisée : après Arrange réel, le centre de la pastille est à plus de 71,5 px du centre du cadran → elle ne peut masquer aucun anneau"
affects: [18-source-entetes, 19-doctrine-composite, 20-honnetete-visible]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Deux booléens de vue plutôt qu'un enum bindé + converter (cohérent avec IsStyleArcs / IsModeNormal)"
    - "Commande DÉDIÉE monodirectionnelle à côté d'une commande bascule préexistante, plutôt que réutilisation de la bascule"
    - "Élément d'UI transverse posé en DERNIER enfant du Grid racine : unique pour N styles et M modes, sans duplication"
    - "Token de design en DOUBLE déclaration : repli statique dans DesignTokens.xaml (autonomie hors Application) + entrée dans BrushTokens() (suivi des thèmes)"
    - "Paramètre optionnel injectable dans un helper de test pour rendre observable un effet de bord (l'orchestrateur), sans créer de site de construction supplémentaire"
    - "Montage de test d'une fenêtre non affichée : DataContext sur la grille racine + purge du Dispatcher + Measure/Arrange, faute de quoi AUCUN binding ne s'évalue"
    - "Mutation ciblée pour prouver la falsifiabilité quand le TDD RED strict est impossible (changement de forme de ctor)"
    - "Garde de test explicitement DÉCLARÉE non couvrante quand l'invariant est inobservable, plutôt qu'un test vert pour de mauvaises raisons"

key-files:
  created: []
  modified:
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/Views/MainWindow.xaml
    - src/Chronos/Resources/DesignTokens.xaml
    - src/Chronos/Theming/ChronosTheme.cs
    - tests/Chronos.Tests/MainViewModelTests.cs
    - tests/Chronos.Tests/CadranBindingTests.cs
    - tests/Chronos.Tests/ThemingTests.cs
    - tests/Chronos.Tests/Fakes/FakeOAuthLogin.cs
    - tests/Chronos.Tests/OverlayWindowConfigTests.cs

key-decisions:
  - "La pastille porte une commande DÉDIÉE et non LoginClaudeCommand : cette dernière BASCULE sur IOAuthLogin.IsLoggedIn, qui vaut _store.Exists (la seule présence du fichier) et donc « true » avec un jeton expiré. Un clic y aurait SUPPRIMÉ le coffre de jetons de l'utilisateur au lieu de le reconnecter. La bascule reste intacte, et un test la documente comme telle."
  - "Deux booléens de pastille et non un : « déconnecté » est actionnable, « hors ligne » est informatif. Les confondre ferait relancer un login qui échouerait à quelqu'un dont le wifi est coupé — et lui ferait perdre confiance dans le signal, qui est précisément l'objet de la phase."
  - "L'état d'authentification est appliqué DÈS le constructeur, sans attendre une transition : sur cette machine le jeton est mort depuis le 2026-07-12, donc l'autorité est déjà en échec au démarrage de l'exe et n'émettra aucune transition avant longtemps."
  - "Les pastilles sont les DERNIERS enfants du Grid racine, et non des éléments de chaque style : une seule déclaration couvre les 5 styles et les 2 modes, et les UserControl de Views/Cadrans restent intouchés."
  - "AMBRE et non rouge : le rouge signifie déjà « quota épuisé » dans la rampe d'usage ; un rouge d'authentification entrerait en collision sémantique. Cohérent avec la pastille session ORANGE des états d'attente."
  - "Le test d'ORDRE (réarmement avant rafraîchissement) a été ÉCRIT, prouvé non falsifiable par mutation, puis RETIRÉ : RequestRefresh ne fait qu'empiler un déclencheur dans un channel dont la consommation est asynchrone, donc inverser les deux lignes ne fait échouer aucun test. Un test vert pour de mauvaises raisons est pire que pas de test."
  - "La garde initialement fondée sur RefreshOrchestrator.TryTrigger a été abandonnée : sa XML-doc affirme qu'elle renvoie false quand le channel est plein, alors que BoundedChannelFullMode.DropWrite fait renvoyer true. Remplacée par une preuve de bout en bout (orchestrateur démarré, comptage des GetAsync)."

patterns-established:
  - "Avant de s'appuyer sur une XML-doc pour bâtir une garde de test, vérifier la garde par mutation : deux gardes muettes ont été attrapées ainsi dans ce seul plan"
  - "Un test de vue WPF qui asserte une valeur BINDÉE doit monter le DataContext sur la grille racine et purger le Dispatcher ; sinon il est vert par défaut de propriété"
  - "Une contrainte géométrique visuelle (« ne masque aucun anneau ») est automatisable : TransformToAncestor après Arrange + distance au centre comparée au rayon extrême"

requirements-completed: [TOK-02, TOK-03]

# Metrics
duration: 19min
completed: 2026-09-12
---

# Phase 17 Plan 05: La panne visible et réparable en un clic Summary

**Deux pastilles en bas-droite du cadran — ambre cliquable pour « le serveur a refusé les identifiants », grise et inerte pour « hors ligne » — pilotées par `IAuthStatus` à travers une seconde frontière de thread `IUiDispatcher.Post`, et une `ReconnecterCommand` dédiée qui réarme l'autorité de jeton puis demande un rafraîchissement immédiat sans jamais pouvoir déconnecter.**

## Performance

- **Duration:** 19 min
- **Started:** 2026-09-11T22:51:37Z
- **Completed:** 2026-09-11T23:11:00Z
- **Tasks:** 3 (2 automatiques + 1 point de vérification humaine, traité par substitution)
- **Files modified:** 9

## Accomplishments

- **Le 401 muet de deux mois est enfin DIT.** L'overlay affiche une pastille ambre en bas à droite du
  cadran dès que l'autorité déclare `Deconnecte` — et dès la construction du ViewModel, pas seulement à
  la prochaine transition. Sur cette machine (jeton expiré le 2026-07-12), c'est le cas au lancement.
- **La réparation tient en un clic, et ce clic ne peut pas détruire.** `ReconnecterCommand` est dédiée
  et monodirectionnelle. Le piège majeur de la phase — brancher la pastille sur `LoginClaudeCommand`,
  qui bascule sur `IsLoggedIn == _store.Exists` et **supprimerait le coffre** avec un jeton expiré — est
  verrouillé à **deux** niveaux : `LogoutCount == 0` côté ViewModel, et un `[WpfFact]` qui compare
  l'instance de commande réellement bindée dans le XAML (`Assert.NotSame(vm.LoginClaudeCommand, …)`).
- **`ReinitialiserApresLogin` a enfin un appelant.** Il n'en avait aucun depuis sa création au plan
  17-02. Sans lui, le verrou `Deconnecte` et le recul restaient posés : le jeton tout neuf n'aurait pas
  été utilisé et la pastille aurait survécu à sa propre réparation.
- **« Hors ligne » et « déconnecté » ne se confondent jamais**, ni dans le ViewModel (deux booléens,
  quatre cas testés) ni dans la vue (un `Button` ambre `Collapsed` vs une `Ellipse` grise `Visible`,
  vérifié sur les `Visibility` RÉSOLUES).
- **La moitié automatisable de la vérification visuelle l'est devenue** : après un `Arrange` réel dans
  l'empreinte 170×170, le centre de la pastille est mesuré à **101,8 px** du centre du cadran, contre un
  rayon extrême d'anneau de 71,5 px. Elle ne peut masquer aucun anneau, dans aucun style, dans aucun
  mode — et si quelqu'un change la marge, le test tombe (prouvé par mutation).
- **505 tests verts sur DEUX exécutions consécutives** (491 avant le plan, **+14 nets**), aucun test
  supprimé, `%APPDATA%\Chronos\oauth.dat` **inchangé** (518 o, mtime 1783863147) avant et après.

## Task Commits

1. **Task 1 : MainViewModel — abonnement à l'état d'auth, deux booléens, commande de reconnexion dédiée** — `c823839` (feat)
2. **Task 2 : La pastille — token Alerte, deux éléments en bas-droite du Grid racine, smoke tests** — `8cb7b2d` (feat)
3. **Task 3 : Vérification humaine, traitée par SUBSTITUTION — automatisation de tout le vérifiable** — `10fee2d` (test)

## Files Created/Modified

- `src/Chronos/ViewModels/MainViewModel.cs` — champ `_authStatus` + 12ᵉ paramètre de ctor ; abonnement à
  `IAuthStatus.EtatChange` et application de l'état initial ; `AfficherPastilleDeconnexion` /
  `AfficherPastilleHorsLigne` ; `SurEtatAuthChange` (2ᵉ et dernière frontière de thread) ;
  `AppliquerEtatAuth` ; `ReconnecterAsync`. `LoginClaude` **intacte** (`git diff` : 0 ligne supprimée la
  concernant).
- `src/Chronos/Views/MainWindow.xaml` — deux derniers enfants du `Grid` racine : `Button`
  `PastilleDeconnexion` (14 px, `Background="Transparent"`, `ControlTemplate` à `Ellipse`
  `{DynamicResource Alerte}`) et `Ellipse` `PastilleHorsLigne` (10 px, `TexteSecondaire`).
  **Uniquement des ajouts** (`git diff | grep "^-"` → 0), aucune propriété de `Window` touchée.
- `src/Chronos/Resources/DesignTokens.xaml` — token `Alerte` `#EFA23A` (repli statique = `RampAmber` du
  thème « minuit »), avec son commentaire de justification.
- `src/Chronos/Theming/ChronosTheme.cs` — `["Alerte"] = Frozen(RampAmber)` dans `BrushTokens()`.
  `SessionBrushTokens()` et les 9 palettes intouchées.
- `tests/Chronos.Tests/Fakes/FakeOAuthLogin.cs` — additif : `LoginDoitReussir`, `LoginCount`,
  `LogoutCount`. `LogoutCount` est la preuve décisive de TOK-03.
- `tests/Chronos.Tests/MainViewModelTests.cs` — helper `Build` étendu (`login`, `auth`, `orchestrator`
  optionnels) ; **9 tests ajoutés** (26 au total) : état initial, frontière de thread unique, les
  4 valeurs de `EtatAuthentification`, reconnexion sans déconnexion, rafraîchissement immédiat, échec de
  login, et la contre-épreuve `LoginClaudeCommand_DECONNECTE_bel_et_bien_quand_un_jeton_est_present`.
- `tests/Chronos.Tests/CadranBindingTests.cs` — helper `MonterPastille` + **4 `[WpfFact]`** : géométrie
  mesurée, binding de commande, hors ligne, état connecté.
- `tests/Chronos.Tests/ThemingTests.cs` — **1 test** : le token `Alerte` existe dans les 9 thèmes, vaut
  `RampAmber` et n'est jamais `RampRed`. (+ 1 ligne d'argument sur le site existant.)
- `tests/Chronos.Tests/OverlayWindowConfigTests.cs` — 1 ligne : argument `new FakeAuthStatus()`.

## À VÉRIFIER PAR L'UTILISATEUR

Le point de vérification humaine du plan (tâche 3) demandait (a) un **login navigateur réel** et (b) une
**inspection visuelle dans les 3 thèmes**. Il a été traité par **substitution** : tout le vérifiable l'a
été automatiquement (voir ci-dessous). **Deux points seulement restent réellement manuels.**

### 1. Le rendu de la pastille — 3 thèmes × 5 styles × 2 modes (~3 min)

```
dotnet run --project src/Chronos/Chronos.csproj
```

L'état réel de la machine étant un jeton expiré, la **pastille ambre** doit apparaître en bas à droite
du cadran dans les secondes qui suivent le lancement (premier tick immédiat), **pas** au bout d'une
minute. Clic droit → réglages : parcourir les thèmes, les 5 styles de cadran, et basculer Normal ↔
Étendu. À chaque combinaison, vérifier que la pastille reste **lisible** (contraste suffisant sur le
fond d'écran, à travers une fenêtre transparente) et que l'infobulle
« Connexion à Claude perdue — cliquer pour se reconnecter » s'affiche au survol.

*Ce qui est déjà garanti automatiquement :* la pastille ne **masque** aucun anneau (distance au centre
mesurée à 101,8 px contre 71,5 px de rayon extrême), elle ne déborde pas de l'empreinte 170×170, et son
pinceau existe dans les 9 thèmes. Ce qui ne l'est pas : le **contraste perçu** sur un vrai fond d'écran.

Vérifier aussi qu'un glisser-déposer de l'overlay **en saisissant la pastille** ne déplace PAS la
fenêtre (le `Button` doit capter le clic avant le `DragMove` de la fenêtre — garanti par `ButtonBase`
en théorie, jamais observé en pratique).

### 2. Le parcours de reconnexion de bout en bout (~2 min)

Cliquer **une fois** sur la pastille ambre → le parcours de login s'ouvre (navigateur + collage du
code). **Avant** de terminer le login, vérifier que `%APPDATA%\Chronos\oauth.dat` **existe toujours**.
Terminer le login : dans les secondes qui suivent, la pastille doit **disparaître** et des pourcentages
exacts apparaître. Puis clic droit → « Diagnostic… » : la ligne
« État d'authentification : CONNECTÉ (jeton valide, chiffres exacts en circulation) » doit être présente.

Enfin, couper le wifi ~1 min : la pastille doit devenir **grise** et ne pas être cliquable ; la
rétablir la fait disparaître au tick suivant.

> **⚠ À ne surtout pas faire :** lancer manuellement une requête de rafraîchissement contre l'endpoint
> avec le refresh token de `%APPDATA%\Chronos\oauth.dat` depuis un terminal ou un script. L'endpoint
> fait **tourner** le refresh token ; un appel dont le résultat n'est pas ré-enregistré invaliderait
> définitivement le login. Seule l'application a le droit de rafraîchir. (Aucun appel de ce genre n'a été
> émis pendant ce plan : le coffre est mesuré identique avant et après — 518 o, mtime 1783863147.)

## Decisions Made

Voir `key-decisions` en frontmatter. Les trois qui comptent pour la suite :

1. **La commande dédiée n'est pas un raffinement de style, c'est une protection de données.** Toute
   future surface d'UI qui voudra « relancer le login » doit utiliser `ReconnecterCommand` et jamais
   `LoginClaudeCommand`. Les deux tests qui gardent cet invariant se lisent comme une documentation :
   l'un prouve que la pastille ne déconnecte pas, l'autre prouve que la bascule **le fait**.
2. **L'invariant d'ORDRE (réarmer avant rafraîchir) est tenu par le code, pas par un test** — et c'est
   assumé, mesuré et documenté plutôt que masqué (détail en « Issues Encountered »).
3. **Rien n'a été anticipé sur les phases 18/19/20.** Pas de distinction visuelle frais/daté/indisponible
   du cadran, pas de touche à la `UniformGrid Columns="2"` des réglages, pas d'invite « jamais
   connecté » (`NonConnecte` n'allume rien — c'est EXA-05, phase 19, et un badge permanent pour qui a
   choisi de ne pas se connecter serait une nuisance).

## Deviations from Plan

### Auto-fixed Issues

**1. [Règle 3 - Blocage] `--cadrans` dans un commentaire XML casse la compilation XAML**

- **Found during:** Task 2 (ajout des pastilles)
- **Issue:** le commentaire de justification prescrit par le plan mentionnait la galerie `--cadrans`.
  Un commentaire XML **ne peut pas contenir `--`** → `error MC3000` sur `MainWindow.xaml`, build
  impossible, donc `dotnet test` impossible.
- **Fix:** « la galerie de cadrans reste intacte » (sens conservé, double tiret supprimé).
- **Files modified:** `src/Chronos/Views/MainWindow.xaml`
- **Verification:** `dotnet build Chronos.sln` → 0 erreur.
- **Committed in:** `8cb7b2d`

**2. [Règle 1 - Bug] La garde de test fondée sur `TryTrigger` était MUETTE**

- **Found during:** Task 1 (test « un rafraîchissement a été demandé »)
- **Issue:** la XML-doc de `RefreshOrchestrator.TryTrigger` annonce « Retourne false si un
  rafraîchissement est déjà en file (DropWrite) ». C'est faux : avec
  `BoundedChannelFullMode.DropWrite`, `TryWrite` abandonne l'élément entrant et renvoie **`true`**.
  La garde écrite sur cette base serait restée verte quoi qu'il arrive.
- **Fix:** remplacée par une preuve de bout en bout — orchestrateur réellement démarré, comptage des
  `GetAsync`, assertion qu'un appel SUPPLÉMENTAIRE survient après la commande. Le piège est consigné en
  commentaire dans le test pour que personne ne le réintroduise. Le défaut de doc est consigné dans
  `deferred-items.md` (hors périmètre : `RefreshOrchestrator.cs` n'est pas un fichier de ce plan).
- **Files modified:** `tests/Chronos.Tests/MainViewModelTests.cs`
- **Verification:** falsifiabilité prouvée par mutation — retrait de `RequestRefresh()` de
  `ReconnecterAsync` → le test tombe.
- **Committed in:** `c823839`, affiné en `10fee2d`

**3. [Règle 1 - Bug] Les tests de pastille étaient verts par défaut de propriété**

- **Found during:** Task 2 (smoke tests `[WpfFact]`)
- **Issue:** une `Window` jamais affichée n'a pas de template appliqué, donc son `Content` n'a **aucun
  parent visuel** : le `DataContext` ne se propage pas et **aucun** binding ne s'évalue. `Command`
  restait `null` et `Visibility` restait à son défaut `Visible` — les assertions d'affichage auraient
  été vertes ou rouges sans rapport avec le XAML livré. De plus, une réévaluation déclenchée par un
  changement de `DataContext` est une opération **différée** du `Dispatcher`.
- **Fix:** helper `MonterPastille` — `DataContext` posé sur la grille racine (ses enfants SONT des
  enfants visuels), purge de la file du `Dispatcher` (`Invoke(…, ApplicationIdle)`), puis
  `Measure`/`Arrange` dans l'empreinte réelle 170×170. Le POURQUOI est en XML-doc de l'helper.
- **Files modified:** `tests/Chronos.Tests/CadranBindingTests.cs`
- **Verification:** falsifiabilité prouvée par double mutation du XAML — binder
  `LoginClaudeCommand` **et** passer la marge à `0,0,60,60` → les deux tests concernés tombent.
- **Committed in:** `8cb7b2d`

### Écarts de forme assumés (documentés, non corrigés)

- **Paramètre `orchestrator` optionnel ajouté au helper `Build`**, en plus des `login`/`auth` prescrits.
  Le plan exige dans son `<behavior>` que « `orchestrator.RequestRefresh()` a été appelé » soit vérifié,
  mais l'orchestrateur construit à l'intérieur du helper n'était observable d'aucune façon. Le paramètre
  optionnel évite un **6ᵉ** site `new MainViewModel(` — le critère « → 5 » reste satisfait.
- **`x:Name` ajouté aux deux pastilles** (`PastilleDeconnexion`, `PastilleHorsLigne`). Non prévu au
  plan, mais nécessaire pour les atteindre en test : l'arbre visuel d'une fenêtre non affichée est vide,
  donc `FindName` est le seul accès — c'est d'ailleurs le motif déjà utilisé pour `ArcHebdo` /
  `ArcCinqHeures` dans le même fichier de tests. Aucun code-behind ajouté.
- **Quatre critères `grep` du plan lisent un chiffre différent de l'attendu, tous à cause du texte de
  commentaire que le plan prescrit lui-même mot pour mot.** Substance vérifiée séparément :

  | Critère du plan | Lu | Pourquoi | Vérification de substance |
  |---|---|---|---|
  | `ReconnecterCommand` dans le XAML → 1 | 2 | le binding + le commentaire « Command = ReconnecterCommand et SURTOUT PAS LoginClaudeCommand » | — |
  | `LoginClaudeCommand` dans le XAML → 0 | 1 | le même commentaire | `grep -c 'Command="{Binding LoginClaudeCommand}"'` → **0** (+ `Assert.NotSame` en `[WpfFact]`) |
  | `x:Null` dans le XAML → 0 | 1 | le commentaire « seul `{x:Null}` ne l'est pas » | `grep -cE '(Fill\|Background\|Stroke)="\{x:Null\}"'` → **0** |
  | `Storyboard\|DropShadow\|BlurEffect` → 0 | 1 | ligne 11, **préexistante** (« Aucune Storyboard/blur/shadow ») | `grep -cE '<Storyboard\|<DropShadowEffect\|<BlurEffect'` → **0** |

- **`grep -c "AfficherPastilleDeconnexion"` dans le ViewModel → 1 au lieu de 2** : le champ généré
  s'écrit `_afficherPastilleDeconnexion` (minuscule), donc le `grep` **sensible à la casse** n'en voit
  qu'une. `grep -ci` → **2**, comme le critère l'entendait.

---

**Total deviations:** 3 auto-corrigées (1 blocage, 2 bugs — dont **deux gardes de test muettes**) +
6 écarts de forme documentés.
**Impact on plan:** aucune dérive de périmètre. La correction n° 1 était indispensable à la
compilation ; les n° 2 et 3 ont transformé trois tests verts-pour-de-mauvaises-raisons en preuves
falsifiables, ce qui était l'enjeu même d'un plan dont le sujet est « ne jamais laisser une panne
silencieuse ».

## Issues Encountered

- **TDD strict (`tdd="true"`) impossible sur la tâche 1, comme au plan 17-04 :** le ctor de
  `MainViewModel` change de forme, donc écrire les tests d'abord aurait produit un état où le projet de
  test ne compile pas — et `tests/Chronos.Tests` référençant `Chronos`, **aucun** test de la solution
  n'aurait pu s'exécuter. Source et tests livrés dans un commit unique et compilable.
  **La falsifiabilité a été obtenue autrement, et mesurée :** trois mutations ciblées appliquées à
  `MainViewModel.cs` (retrait de `_ui.Post`, ajout d'un `Logout()` dans `ReconnecterAsync`, retrait de
  l'application de l'état initial) font tomber **5 des 9 nouveaux tests** ; deux mutations de
  `MainWindow.xaml` (binding de la bascule, marge à `0,0,60,60`) en font tomber 2 de plus. Toutes les
  mutations ont été révoquées et le `git diff` de restauration vérifié vide.
- **Un test d'ORDRE a été écrit, prouvé inutile, puis retiré.** Il journalisait « réarmement » et
  « lecture d'usage » pour prouver que `ReinitialiserApresLogin()` précède `RequestRefresh()`. La
  mutation (inverser les deux lignes) **ne l'a pas fait tomber** : `RequestRefresh` ne fait qu'empiler un
  déclencheur dans un channel dont la consommation est asynchrone, si bien que la lecture arrive de
  toute façon après. L'invariant reste **réel en production** (dans l'ordre inverse, la boucle
  consommatrice pourrait lire l'usage alors que l'autorité est encore verrouillée), mais il est
  **inobservable depuis un test**. Il est donc porté par le code et par la XML-doc de
  `ReconnecterAsync`, et l'absence de couverture est déclarée explicitement dans le test voisin — plutôt
  que dissimulée derrière une assertion complaisante. Les crochets ajoutés aux faux pour ce test ont été
  retirés (aucun code mort).
- **Compétences `frontend-design` et `windows-wpf` non disponibles** dans cet environnement (absentes de
  `~/.claude/skills` comme de `.claude/skills`). Les règles WPF applicables ont été appliquées
  directement : aucune `Storyboard` ni effet (le rendu est logiciel sous `AllowsTransparency`), `Button`
  plutôt qu'`Ellipse` nue pour neutraliser le `DragMove`, `Transparent` et non `{x:Null}` pour le
  hit-testing, pinceaux gelés, `DynamicResource` pour suivre le thème à chaud.
- **Aucun autre obstacle.** Deux avertissements `xUnit2031` subsistent dans
  `DesktopUiaSessionSourceTests.cs` : **préexistants et hors périmètre** (non touchés).

## User Setup Required

None — aucun service externe à configurer, aucune dépendance NuGet ajoutée
(`git diff` sur les deux `.csproj` → vide).

## Next Phase Readiness

**La phase 17 est complète : TOK-01, TOK-02 et TOK-03 sont livrés et prouvés.** Le jeton se renouvelle
seul (17-03/17-04), l'échec est classé par actionnabilité (17-02), et il est désormais **dit** et
**réparable** (17-05).

Prêt pour la **phase 18** (source exacte par en-têtes de rate-limit) :

- `ChronosTokenAuthority` est l'autorité **unique** de jeton et l'unique rafraîchisseur du dépôt
  (`grep -rln "RefreshAsync" src/Chronos` → exactement 2 fichiers). La sonde d'en-têtes de la phase 18
  n'aura qu'à lui demander son jeton et à lui signaler succès / refus serveur — elle héritera
  gratuitement du rejeu, du recul et de la pastille.
- `IAuthStatus` est consommé par exactement deux abonnés (`MainViewModel`, `DiagnosticService`) ; un
  troisième ne coûte rien.

**Points de vigilance :**

- Les deux vérifications manuelles ci-dessus (« À VÉRIFIER PAR L'UTILISATEUR ») **ne sont pas faites**.
  Elles ne bloquent aucune phase suivante, mais la réparation réelle du jeton conditionne l'intérêt
  pratique de la phase 18 : sans reconnexion, ni `/api/oauth/usage` ni la sonde d'en-têtes ne répondront.
- Deux découvertes hors périmètre sont consignées dans `deferred-items.md` (items 3 et 4) : la XML-doc
  trompeuse de `RefreshOrchestrator.TryTrigger`, et le fait que les smoke tests XAML existants
  n'évaluent aucun binding. Le second est un candidat naturel pour la **phase 20**.
- La distinction visuelle frais / daté / indisponible du cadran reste la **phase 20**, et le trou de la
  `UniformGrid Columns="2"` des réglages (3 boutons) reste ouvert — rien n'a été anticipé.

---
*Phase: 17-jeton-toujours-vivant-panne-toujours-visible*
*Completed: 2026-09-12*

## Self-Check: PASSED

- 9 fichiers modifiés + le SUMMARY : tous présents sur disque.
- Commits `c823839`, `8cb7b2d`, `10fee2d` : présents dans l'historique.
- 505 tests, 0 échec, **deux exécutions consécutives** (491 avant le plan, +14 nets, 0 supprimé).
- Aucun stub, aucun TODO/FIXME/placeholder dans les fichiers livrés.
- Aucune donnée bidon câblée à l'UI : les deux pastilles lisent `IAuthStatus`, service réel enregistré
  dans le graphe DI depuis le plan 17-04 (garde `CompositionRootTests` verte).
- `%APPDATA%\Chronos\oauth.dat` : 518 o, mtime 1783863147 — **identique** avant et après la suite.
  Aucun appel de rafraîchissement émis avec le refresh token réel.
