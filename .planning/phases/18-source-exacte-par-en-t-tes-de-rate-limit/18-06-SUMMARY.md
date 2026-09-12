---
phase: 18-source-exacte-par-en-t-tes-de-rate-limit
plan: 06
subsystem: viewmodels-et-reglages
tags: [mvvm, observable-property, relay-command, gap-1, frontiere-de-thread, baml, wpffact, mutation-testing, honnetete, xunit]

# Dependency graph
requires:
  - phase: 17-jeton-toujours-vivant-panne-toujours-visible
    provides: "17-05 — motif du canal d'état neutre marshallé par IUiDispatcher.Post, et les trois pièges de test WPF vécus (DataContext sur la racine, purge du Dispatcher, x:Name obligatoire)"
  - phase: 18-source-exacte-par-en-t-tes-de-rate-limit
    provides: "18-02/18-04 — StatutServeur (4 valeurs), EtatDepassement, IEtatServeur, WindowState.StatutServeur"
  - phase: 18-source-exacte-par-en-t-tes-de-rate-limit
    provides: "18-03 — ChronosSettings.SondeEnTetesActivee (défaut true), relu par la sonde à chaque GetAsync"
  - phase: 18-source-exacte-par-en-t-tes-de-rate-limit
    provides: "18-05 — IEtatServeur résolu en DI comme alias de l'UNIQUE instance de sonde, et FakeEtatServeur"
provides:
  - "HDR-06 — interrupteur « Sonde d'en-têtes » dans les réglages, DISTINCT de la connexion Claude, persistant (GAP-1) et effectif au clic (RequestRefresh), avec son coût ÉCRIT : ≈ 288 micro-requêtes/jour"
  - "HDR-03 — WindowGaugeViewModel.TexteStatutServeur / HasStatutServeur : le vocabulaire FR UNIQUE du statut serveur de toute la couche présentation, posé et testé pour que la phase 20 n'ait qu'à le binder"
  - "HDR-03/HDR-04 — MainViewModel.TexteEtatSonde / AfficherEtatSonde : une ligne d'état serveur + dépassement dans les réglages, VIDE quand rien n'est rapporté"
  - "TROISIÈME frontière de thread du ViewModel, qui AMENDE la formule « seconde et dernière » du plan 17-05"
  - "Garde BAML sur la fenêtre de réglages (ReglagesBindingTests) : l'interrupteur ne peut pas être câblé sur une autre commande sans faire tomber un test"
affects: [19-doctrine-du-composite, 20-rendu-visible]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Un mapping de vocabulaire serveur → FR vit dans UN SEUL ViewModel ; les consommateurs relisent ses propriétés déjà calculées au lieu de refaire le mapping (grep d'acceptation LibelleStatut == 0 dans MainViewModel)"
    - "Propriétés d'état posées et testées dans un ViewModel SANS être bindées, pour qu'une phase ultérieure n'ait qu'à les binder : le contrat est gravé avant que la géométrie ne bouge"
    - "Deux interrupteurs aux profils de coût OPPOSÉS restent deux champs, et c'est un TEST qui le documente (Les_DEUX_interrupteurs_sont_INDEPENDANTS_sur_disque) plutôt qu'un commentaire"
    - "Falsifiabilité d'un garde-fou de visibilité : muter le MAPPING ne suffit pas (le garde est le drapeau), il faut muter le DRAPEAU — d'où une 4e mutation non prévue par le plan"

key-files:
  created:
    - tests/Chronos.Tests/ReglagesBindingTests.cs
  modified:
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/ViewModels/WindowGaugeViewModel.cs
    - src/Chronos/Views/SettingsWindow.xaml
    - tests/Chronos.Tests/MainViewModelTests.cs
    - tests/Chronos.Tests/WindowGaugeViewModelTests.cs

key-decisions:
  - "Le commentaire XML mandaté mot pour mot par le plan contenait « micro-requête », ce qui aurait porté le compte du fichier à 2 et violé le critère d'acceptation grep == 1 du même plan. Reformulé en « une vraie requête sur le compte […] : minuscule, mais réelle » : le fait annoncé est identique, le critère est honoré. Précédent : déviations des plans 18-01, 18-02, 18-04 et 18-05, où des noms interdits par grep ont été décrits sans être cités."
  - "La XML-doc de ReglagesBindingTests citait l'attribut de collection entre <c>…</c>, portant grep '[Collection(\"XAML WPF\")]' à 2 contre un critère == 1. Reformulée en « l'appartenance à la collection sérialisée » : le fait reste écrit, le garde-fou contre une seconde déclaration reste utile."
  - "Une QUATRIÈME mutation, non demandée par le plan, a été nécessaire pour prouver la falsifiabilité du test « aucun statut → rien d'affiché » de MainViewModel : muter LibelleStatut(null) ne le fait PAS tomber, parce que le garde-fou de ce test est HasStatutServeur et non le texte. Les deux mutations sont consignées — la seconde (HasStatutServeur = true) fait tomber les trois tests concernés."
  - "Le statut est lu via les propriétés DÉJÀ CALCULÉES des deux jauges (FiveHour.TexteStatutServeur / HasStatutServeur), et non re-mappé depuis _last. C'est la lettre du plan, et sa raison est vérifiable par grep : LibelleStatut == 0 dans MainViewModel.cs, donc un seul vocabulaire dans tout le projet."
  - "La tâche 3 (checkpoint humain) a été VÉRIFIÉE PAR SUBSTITUTION sur demande explicite de l'utilisateur : tout ce qui est automatisable a été vérifié, la suite a été exécutée deux fois, et le protocole manuel est consigné intégralement ci-dessous. HDR-02 n'est PAS marqué « prouvé en production »."

patterns-established:
  - "La géométrie du cadran peut rester rigoureusement intacte (git diff VIDE sur MainWindow.xaml) tout en livrant l'information qu'elle affichera plus tard : les propriétés du ViewModel sont le point de rendez-vous entre deux phases."

requirements-completed: [HDR-03, HDR-04, HDR-06]

# Metrics
duration: 18min
completed: 2026-09-12
---

# Phase 18 Plan 06: Réglages, état serveur visible et point de vérification humaine Summary

**L'utilisateur peut désormais couper en un clic la seule source du projet qui dépense du quota pour en
mesurer, et il lit ce qu'elle coûte avant de décider — « consomme une micro-requête sur ton compte toutes
les 5 min (≈ 288/jour) », écrit noir sur blanc et verrouillé par un test BAML. Le statut que le SERVEUR
déclare et le dépassement qu'il rapporte sont visibles dans les réglages, et l'absence d'information y
reste une absence : rien de rapporté n'affiche rien, jamais « autorisé » par défaut. Pas un pixel du
cadran n'a changé — la distinction visuelle frais / daté / indisponible reste EXA-03, donc la phase 20, et
les propriétés de `WindowGaugeViewModel` qu'elle bindera sont déjà posées et testées. Et le seul fait que
l'automatisation ne peut pas trancher — qu'un 429 RÉEL d'Anthropic porte bien la famille `unified` — est
consigné comme tel, avec son protocole : HDR-02 reste « testé sur faux transport, NON prouvé en
production ».**

## Performance

- **Duration:** 18 min
- **Started:** 2026-09-12T02:08:56Z
- **Completed:** 2026-09-12T02:27:01Z
- **Tasks:** 2 exécutées + 1 vérifiée par substitution
- **Files modified:** 6 (1 créé, 5 modifiés) — 604 insertions, 3 suppressions

## Task Commits

| Tâche | Nom | Commit | Type |
|---|---|---|---|
| 1 | ViewModels — interrupteur persistant, texte d'état serveur, statut par fenêtre | `03dccac` | feat |
| 2 | Réglages — l'interrupteur, le coût écrit, et le smoke test BAML | `325c7dc` | feat |
| 3 | Vérification humaine du 429 réel | — | vérifiée par **substitution** (voir ci-dessous) |

## Ce qui a été construit

### Tâche 1 — les ViewModels (commit `03dccac`)

**`WindowGaugeViewModel`** porte désormais `TexteStatutServeur` et `HasStatutServeur`, alimentés dans
`Apply` juste après `HasUtilizationText`, et un mapping `LibelleStatut` qui est **le vocabulaire FR unique
du statut serveur de tout le projet** :

| `StatutServeur` | Libellé rendu |
|---|---|
| `null` (en-tête absent) | `""` — **jamais** « autorisé » |
| `Autorise` | `AUTORISÉ` |
| `AutoriseAvertissement` | `AUTORISÉ (avertissement)` |
| `Rejete` | `REJETÉ` |
| `NonReconnu` | `statut non reconnu` |

Ces deux propriétés ne sont **bindées nulle part** : elles sont le point de rendez-vous avec la phase 20.
`git diff --stat -- src/Chronos/Views/MainWindow.xaml` est **VIDE**, et les 11 tests préexistants de
`WindowGaugeViewModelTests` sont restés verts **sans une modification** (`git diff | grep -c "^-[^-]"` =
**0** sur ce fichier).

**`MainViewModel`** reçoit un **13ᵉ paramètre, `IEtatServeur? etatServeur = null`, optionnel et en
dernière position**. Conséquence mesurée : **aucun** site de construction préexistant n'a été retouché —
`grep -c "new MainViewModel(" tests/Chronos.Tests/MainViewModelTests.cs` vaut **2**, exactement comme
avant le plan (le nouveau paramètre passe par le helper `Build`), et la seule suppression du diff de ce
fichier source est la ligne de signature elle-même.

S'y ajoutent :

- `IsSondeEnTetesActivee`, miroir de `ChronosSettings.SondeEnTetesActivee` dès le constructeur ;
- `ToggleSondeEnTetesCommand`, motif **exact** de `ToggleCadranMode` : relecture disque **fraîche** avant
  `Save` (GAP-1), puis `_orchestrator.RequestRefresh()` pour que couper ou rallumer prenne effet
  immédiatement et non au tick suivant ;
- `TexteEtatSonde` / `AfficherEtatSonde`, assemblés par `MajTexteEtatSonde()` — appelé **dès le
  constructeur** (si le canal est injecté) **et** en fin de `ApplySnapshot`.

### La formule « seconde et **dernière** frontière de thread » du plan 17-05 est AMENDÉE

Le plan 17-05 déclarait `_ui.Post` franchie « une seconde et **dernière** fois ». **C'est désormais faux,
et plutôt que de laisser la contradiction dans l'historique, ce plan l'amende explicitement** : il y en a
**trois**, et la troisième est de nature **identique** aux deux premières — un service NEUTRE
(`IEtatServeur`) émet sur un thread du pool, l'abonné marshalle lui-même.

```csharp
private void OnSnapshotChanged(object? sender, UsageSnapshot snap) => _ui.Post(() => ApplySnapshot(snap));
private void SurEtatAuthChange(object? s, EtatAuthentification e)  => _ui.Post(() => AppliquerEtatAuth(e));
private void SurDepassementChange(object? s, EtatDepassement? d)   => _ui.Post(MajTexteEtatSonde);
```

`grep -c "_ui.Post" src/Chronos/ViewModels/MainViewModel.cs` == **3** — le critère d'acceptation grave ce
nombre, donc une quatrième frontière ne pourra pas se glisser sans décision explicite. Le test
`MainViewModelTests` qui mesure `Assert.Equal(avant + 1, ui.PostCount)` mesure un **delta par événement**
et reste vrai, comme le plan l'avait anticipé.

### Tâche 2 — les réglages (commit `325c7dc`)

Un `Border` de même facture que « Connexion Claude » est inséré dans la section `DONNÉES`, **après** elle
et **avant** la section `THÈME`. Ajout pur : `git diff | grep -c "^-"` == **1**, c'est-à-dire la seule
ligne d'en-tête du diff lui-même.

**Le libellé de coût EXACT, tel qu'écrit dans les réglages** (`SettingsWindow.xaml:194`) :

> `chiffres exacts même en saturation — consomme une micro-requête sur ton compte toutes les 5 min (≈ 288/jour)`

Rien n'y est minimisé : le mot « consomme », le « ton compte », la cadence et le nombre quotidien y sont.
Le test n° 3 exige qu'un `TextBlock` de la fenêtre montée contienne **à la fois** `micro-requête` **et**
`288` : retirer le libellé, ou seulement son chiffre, fait tomber un test.

`x:Name="InterrupteurSonde"` et `x:Name="LigneEtatSonde"` sont obligatoires et non décoratifs :
l'arbre visuel d'une fenêtre jamais affichée est vide, `FindName` est le seul accès.

**Ce qui n'a PAS été touché**, conformément à la consigne de ne rien anticiper de la phase 20 :
`grep -c 'UniformGrid Columns="2"'` vaut toujours **1** — le trou visuel de la cellule bas-droite des 3
boutons reste entier.

## Décompte de tests — avant / après, DEUX exécutions

| Moment | Total | Échecs | Ignorés |
|---|---|---|---|
| Baseline à l'entrée du plan (plan 18-05) | **633** | 0 | 0 |
| Après tâche 1 (suite complète) | **648** | 0 | 0 |
| Après tâche 2 — **exécution 1** (2 min 31) | **652** | 0 | 0 |
| Après tâche 2 — **exécution 2** (2 min 35) | **652** | 0 | 0 |

**+19 tests** (10 `MainViewModelTests`, 5 `WindowGaugeViewModelTests`, 4 `ReglagesBindingTests`),
**0 test supprimé, 0 test modifié, 0 test ignoré.** Les deux exécutions consécutives étaient exigées parce
que la phase 16 a établi que la course du chargeur BAML de WPF n'est visible qu'en exécutions répétées :
ce plan ajoute des `[WpfFact]`, et une seule exécution verte n'aurait rien prouvé.

Sous-suites de garde, rejouées explicitement (31 tests, 0 échec) : `ServicesLayerPurityTests`,
`CompositionRootTests`, `CadranBindingTests`, `ThemingTests`, `OverlayWindowConfigTests`,
`NormalisationUniqueTests`.

## Falsifiabilité : SIX mutations mesurées PUIS révoquées

TDD strict reste impossible sur ce dépôt (documenté depuis 18-03 : `tests/Chronos.Tests` porte un
`ProjectReference` vers `Chronos`, donc une étape RED serait un commit où `dotnet test` n'est pas même
invocable). La falsifiabilité est obtenue par mutation.

| # | Mutation appliquée | Test(s) tombé(s) | Révoquée |
|---|---|---|---|
| 1 | `_ui.Post` retiré de `SurDepassementChange` | `Le_depassement_franchit_la_frontiere_de_thread_UNE_fois` **[FAIL]** | oui |
| 2 | `LibelleStatut(null)` rend `"AUTORISÉ"` | `Statut_ABSENT_rend_le_VIDE_et_JAMAIS_autorise`, `Fenetre_indisponible_ne_porte_AUCUN_statut_serveur` **[FAIL]** | oui |
| 2b | `HasStatutServeur = true` inconditionnel | les 2 ci-dessus **+** `Aucun_statut_rapporte_n_affiche_RIEN_et_JAMAIS_autorise` **[FAIL]** | oui |
| 3 | `_settingsService.Load()` retiré de la bascule (`_settings with`) | `ToggleSondeEnTetes_bascule_persiste_et_n_ecrase_AUCUN_autre_reglage` **[FAIL]** | oui |
| a | `IsChecked` bindé sur `IsOAuthUsageEnabled` | `L_interrupteur_reflete_l_etat_persiste` **[FAIL]** | oui |
| b | `≈ 288/jour` retiré du libellé | `Le_cout_de_la_sonde_est_ECRIT_dans_les_reglages` **[FAIL]** | oui |

**La mutation 2b n'était pas demandée par le plan, et elle était nécessaire.** Le plan prédisait que faire
rendre `"AUTORISÉ"` à `LibelleStatut` pour `null` ferait tomber le test « aucun statut → rien d'affiché »
de `MainViewModel`. Ce n'est **pas** ce qui s'est produit : ce test est gardé par `HasStatutServeur`, pas
par le texte, donc il reste vert sous la mutation 2. L'enseignement vaut d'être gravé : **pour falsifier un
garde-fou de visibilité, il faut muter le DRAPEAU, pas le libellé.** La mutation 2b le fait, et les trois
tests concernés tombent ensemble.

Après révocation des six : `git diff` conforme à l'intention sur les trois fichiers source
(82/1, 29/0, 24/0), et les quatre greps de contrôle (`_ui.Post(MajTexteEtatSonde)`,
`_settingsService.Load() with { SondeEnTetesActivee`, `_ => "",`,
`HasStatutServeur = s.StatutServeur is not null;`) valent tous **1**.

## Vérifications du plan

| # | Vérification | Résultat |
|---|---|---|
| 1 | `dotnet test Chronos.sln -v q --nologo`, deux exécutions | **652 / 652, 0 échec** ×2, 0 test supprimé |
| 2 | `dotnet build Chronos.sln` | **0 erreur** (aucun `MC3000`) ; 2 avertissements `xUnit2031` **préexistants** dans `DesktopUiaSessionSourceTests.cs`, hors périmètre |
| 3 | `git diff --name-only` sur `MainWindow.xaml`, `src/Chronos/Services/`, `src/Chronos/Models/` | **VIDE** |
| 4 | `git diff --name-only` sur `CompositeUsageProvider.cs`, `UsageSnapshot.cs` | **VIDE** |
| 5 | `ServicesLayerPurityTests` | vert — aucun type WPF n'a migré dans les services |
| 6 | `CompositionRootTests` | vert — le 13ᵉ paramètre optionnel ne casse pas la résolution DI |
| 7 | `CadranBindingTests` / `ThemingTests` / `OverlayWindowConfigTests` | verts — aucun site de construction n'a eu à changer |
| 8 | `grep -rn "SondeEnTetes" src/Chronos/Views/SettingsWindow.xaml` | 2 lignes (l'`IsChecked` et la `Command`) |
| — | fichiers SOURCE contenant `RefreshAsync` | **exactement 2** (`ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs`) |
| — | `%APPDATA%\Chronos\oauth.dat` | **518 o, mtime 1783863147** avant ET après — intact |

Critères `grep` de la tâche 1 : `IEtatServeur? etatServeur = null` = 1 (dernier paramètre) ·
`new MainViewModel(` dans `src/` = **0** · dans `MainViewModelTests.cs` = **2** (inchangé) ·
`_ui.Post` = **3** · `ToggleSondeEnTetes` = 1 · `_settingsService.Load() with` = 8 (≥ 2) ·
`IsSondeEnTetesActivee` = 4 · `* 100` = **0** dans les deux ViewModels ·
`LibelleStatut` = **2** dans `WindowGaugeViewModel.cs` et **0** dans `MainViewModel.cs` ·
suppressions dans `WindowGaugeViewModelTests.cs` = **0**.

Critères `grep` de la tâche 2 : `IsChecked="{Binding IsSondeEnTetesActivee, Mode=OneWay}"` = 1 ·
`Command="{Binding ToggleSondeEnTetesCommand}"` = 1 · `micro-requête` = **1** · `288` = **1** ·
`x:Name="InterrupteurSonde"` = 1 · `x:Name="LigneEtatSonde"` = 1 ·
commentaire XML à double tiret = **0** · `UniformGrid Columns="2"` = 1 (inchangé) ·
`[Collection("XAML WPF")]` = 1 · `[WpfFact]` = **4** · `Assert.Same` = 1 · `Assert.NotSame` = 2 ·
`Path.GetTempPath()` = 3 · fichier = 180 lignes (artefact `min_lines: 70` satisfait).

## Tâche 3 — vérifiée par SUBSTITUTION, et pourquoi

La tâche 3 est un `checkpoint:human-verify` dont le point décisif exige **un jeton OAuth valide** (celui de
cette machine est expiré depuis le **2026-07-12**) **et** un compte réellement saturé. Aucune
automatisation ne peut le trancher, et simuler son résultat reproduirait exactement la panne silencieuse
que tout le milestone v1.5 éradique. L'utilisateur a demandé une exécution autonome ; la substitution
appliquée est donc :

1. **tout ce qui est automatisable a été vérifié** — réglages écrits, liaisons (commande + propriété),
   interrupteur reflétant l'état persisté, libellé de coût présent, propriétés d'état serveur exposées et
   testées sur les cinq formes de statut ;
2. **la suite complète a été exécutée deux fois** (652/652 ×2), parce que ce plan ajoute des `[WpfFact]` ;
3. le protocole manuel est consigné **intégralement** ci-dessous ;
4. **HDR-02 n'est PAS marqué « prouvé en production »** — voir la mise au point juste après ;
5. HDR-03, HDR-04 et HDR-06 sont réellement satisfaits par ce plan et marqués complets.

### Statut HONNÊTE de HDR-02, à ne pas arrondir

`REQUIREMENTS.md` portait déjà HDR-02 coché depuis le plan 18-05, et ce SUMMARY **ne le déplace pas** —
mais le fichier n'a pas la granularité nécessaire, donc l'état exact est écrit ici :

> **HDR-02 — implémenté, et testé sur FAUX TRANSPORT uniquement. NON prouvé en production.**
> Le code lit les en-têtes **avant** tout aiguillage par code de statut, donc un 429 porteur de la famille
> `unified` serait exploité ; les tests de `RateLimitHeaderUsageProviderTests` le prouvent sur un
> `FakeHttpMessageHandler`. Ce qu'**aucun** test ne peut prouver : qu'un 429 **réel** d'Anthropic porte
> cette famille. Trois sources concordantes l'affirment, **aucune officielle** — la famille `unified` est
> absente de la documentation publique. La vérification empirique du 2026-09-12 a établi qu'un **401 n'en
> porte aucun** (6 sondes, 2 endpoints).

## À VÉRIFIER PAR L'UTILISATEUR

**Prérequis, non fait à ce jour :** le login réel de la phase 17 n'a jamais été effectué. Sans
reconnexion, ni `/api/oauth/usage` ni la sonde ne répondront, et les étapes 2 à 4 seront muettes.

> **⚠ À ne surtout pas faire, à aucune étape :** lancer à la main une requête de rafraîchissement contre
> l'endpoint de jeton avec le refresh token de `%APPDATA%\Chronos\oauth.dat`, depuis un terminal ou un
> script. L'endpoint fait **tourner** le refresh token ; un appel dont le résultat n'est pas ré-enregistré
> invaliderait **définitivement** le login. Seule l'application a le droit de rafraîchir.

### 1. Se reconnecter (~2 min)

Lancer `dotnet run --project src/Chronos/Chronos.csproj`. La pastille ambre doit apparaître en bas à
droite du cadran. Cliquer **une fois** dessus → le parcours de login s'ouvre. Le terminer.

### 2. Vérifier la sonde nominale (~1 min) — et relever DEUX listes de noms

Clic droit sur le cadran → « Diagnostic… ». Dans la section
`[Source exacte — sonde d'en-têtes de rate-limit]`, relever :

- **`Dernière sonde :`** doit dire **« 200 — en-têtes lus, chiffres exacts »**.
  - si elle dit **« 200 mais AUCUN en-tête unified reconnu »** → la famille a été **renommée** côté
    serveur. Remonter la ligne `en-têtes de limite présents` de la section
    `[Source exacte — endpoint OAuth (repli)]` : le plan de correction est une mise à jour des constantes
    de noms de la sonde ;
  - si elle dit **« modèle refusé par le serveur »** → l'alias `claude-haiku-4-5` a été retiré ; la
    constante `Modele` est à mettre à jour.
- **`En-têtes « unified » reconnus :`** → **recopier la liste des NOMS** (jamais les valeurs).
- Dans la section `[Source exacte — endpoint OAuth (repli)]`, la ligne
  **`→ en-têtes de limite présents :`** → **recopier la liste**. C'est la réponse à la question ouverte
  n° 3 : si `/api/oauth/usage` porte **aussi** la famille `unified`, la sonde pourra devenir **gratuite**
  en phase 19+ (≈ 288 micro-requêtes par jour économisées).

### 3. Vérifier l'interrupteur et son coût (~1 min)

Clic droit → Réglages. La ligne **« Sonde d'en-têtes »** doit être lisible, avec le texte
« chiffres exacts même en saturation — consomme une micro-requête sur ton compte toutes les 5 min
(≈ 288/jour) », et son interrupteur **actif**. Basculer sur **off**, attendre un tick (60 s), rouvrir le
diagnostic : `Dernière sonde` doit dire **« désactivée »** et aucune requête ne doit plus partir.
Rebasculer sur **on**.

### 4. LE POINT DÉCISIF — saturer la fenêtre 5 h (non automatisable)

Quand l'usage 5 h approche 100 % (ou lors d'une saturation naturelle), ouvrir le diagnostic et vérifier :

- `Dernière sonde` dit **« 429 — en-têtes lus quand même, les chiffres restent exacts »** ;
- le cadran **continue d'afficher des pourcentages exacts** pendant le refus, au lieu de basculer en
  « données indisponibles » ;
- **aucune pastille de déconnexion** n'apparaît pendant la saturation — un 429 n'est pas une déconnexion.

→ **Si c'est le cas : HDR-02 est prouvé en production.** Le consigner.
→ **Si le diagnostic dit « 429 SANS en-tête unified »** : l'hypothèse est **fausse** sur ce plan de compte,
et l'avantage structurel de la phase 18 n'existe pas. Le code se dégrade correctement (rien n'est
inventé), mais il faut le consigner honnêtement et **ne pas** cocher HDR-02 comme prouvé en production.
→ **Si la saturation ne peut pas être provoquée** : répondre « approuvé — étape 4 non réalisée ». La phase
peut se clore, HDR-02 restant marqué **non prouvé en production**.

### 5. Contrôle de sécurité, avant et après

`%APPDATA%\Chronos\oauth.dat` doit avoir changé **uniquement** du fait du login de l'étape 1 (la rotation
est persistée par l'application, c'est normal et attendu). Aucune autre modification, et **aucune** requête
de rafraîchissement lancée à la main.

*(Référence de contrôle à l'issue de ce plan, avant tout login : **518 octets, mtime 1783863147** —
inchangé.)*

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — critère d'acceptation contredit par son propre commentaire mandaté] Commentaire XAML reformulé**

- **Found during:** tâche 2, rédaction du `Border`.
- **Issue:** le commentaire imposé mot pour mot par l'action A contient « micro-requête », et le libellé
  visible de l'utilisateur le contient aussi. Le compte du fichier serait passé à 2, violant le critère
  d'acceptation `grep -c "micro-requête" == 1` **du même plan**.
- **Fix:** le commentaire dit « Chaque passage de la sonde consomme une vraie requête sur le compte
  (modèle le moins cher, un seul jeton de sortie) : minuscule, mais réelle. » Le fait annoncé est
  identique ; le critère est honoré et reste un garde-fou utile (un second libellé de coût qui
  divergerait serait détecté).
- **Files modified:** `src/Chronos/Views/SettingsWindow.xaml` — commit `325c7dc`.

**2. [Rule 1 — même classe de défaut] XML-doc de test reformulée pour honorer `[Collection("XAML WPF")]` == 1**

- **Found during:** tâche 2, vérification des critères `grep`.
- **Issue:** la XML-doc citait l'attribut entre `<c>…</c>`, portant le compte à 2 contre un critère `== 1`.
- **Fix:** reformulée en « L'appartenance à la collection sérialisée est OBLIGATOIRE ». Le fait et sa
  justification (la course du chargeur BAML) restent écrits en entier.
- **Files modified:** `tests/Chronos.Tests/ReglagesBindingTests.cs` — commit `325c7dc`.

**3. [Rule 2 — falsifiabilité réelle] Quatrième mutation ajoutée (2b)**

- **Found during:** tâche 1, mutations de falsifiabilité.
- **Issue:** le plan prédit que `LibelleStatut(null) → "AUTORISÉ"` fait tomber le test « aucun statut →
  rien d'affiché » de `MainViewModel`. Mesure : **il reste vert**, parce que ce test est gardé par
  `HasStatutServeur` et non par le texte. La falsifiabilité annoncée n'était donc pas démontrée.
- **Fix:** une mutation supplémentaire (`HasStatutServeur = true` inconditionnel) a été appliquée ; elle
  fait tomber les **trois** tests concernés, ce qui établit la falsifiabilité réellement visée. Les deux
  mutations sont consignées dans le tableau ci-dessus. Aucun code de production modifié pour satisfaire
  une prédiction de plan.
- **Files modified:** aucun (mutations révoquées).

### Écart de forme, consigné

**Fins de ligne.** Le dépôt stocke ses sources en **LF** et `core.autocrlf` vaut `true`, ce qui fait
apparaître l'avertissement « LF will be replaced by CRLF » à chaque `git diff`. Une première édition
scriptée de `WindowGaugeViewModel.cs` a converti le fichier en CRLF ; la conversion a été **annulée avant
tout commit** et tous les fichiers touchés sont en LF, sans BOM. `git diff --numstat` confirme des
additions pures (29/0, 24/0, 89/0, 180/0) et la seule suppression réelle du plan est la ligne de signature
du constructeur de `MainViewModel`. Précédent : déviation 4 du plan 18-05.

### Hors périmètre, non corrigé

Deux avertissements `xUnit2031` préexistants dans `tests/Chronos.Tests/DesktopUiaSessionSourceTests.cs`
(lignes 345 et 364) — fichier non touché par ce plan, non causé par lui. Non corrigés, conformément à la
règle de frontière de périmètre.

## Authentication Gates

Aucune rencontrée pendant l'exécution. Aucun test n'émet de requête réseau réelle : `ReglagesBindingTests`
monte un `RefreshOrchestrator` **jamais démarré** et un `FakeUsageProvider`, ses réglages et son
`usage.json` vivent sous `Path.GetTempPath()` (garde `Assert.StartsWith`), et son `FakeClaudeTokenReader`
ne lit aucun coffre. L'endpoint de renouvellement n'a jamais été appelé avec le jeton réel.

**Le coffre réel n'a pas été touché** : `%APPDATA%\Chronos\oauth.dat` = 518 octets, mtime 1783863147,
avant comme après. Aucun jeton logué ni écrit, aucun corps HTTP transporté ni journalisé. Aucune
dépendance NuGet ajoutée.

## Ce que ce plan N'A PAS fait (par construction)

- **Aucune géométrie de cadran** : `MainWindow.xaml` intact. La distinction visuelle frais / daté /
  indisponible est EXA-03, donc la **phase 20**. « Affiché », pour HDR-03/HDR-04, veut dire réglages +
  diagnostic.
- **Aucune liaison des propriétés de `WindowGaugeViewModel`** : elles sont posées et testées pour que la
  phase 20 n'ait plus qu'à les binder.
- **Aucun service touché** : `git diff` VIDE sur `src/Chronos/Services/` et `src/Chronos/Models/`, donc
  aussi sur `CompositeUsageProvider.cs` et `UsageSnapshot.cs` (refonte = phase 19).
- **Aucune correction du trou de la `UniformGrid Columns="2"`** des 3 boutons des réglages : phase 20.
- **Aucune preuve en production du 429 réel** : elle n'appartient pas à l'automatisation.

## Known Stubs

Aucun stub introduit. Une seule chose reste **volontairement non bindée** et c'est documenté par le plan
lui-même : `WindowGaugeViewModel.TexteStatutServeur` / `HasStatutServeur` n'alimentent aucune vue. Ce
n'est pas un stub mais un **contrat posé à l'avance** pour la phase 20 (EXA-03) — les deux propriétés sont
alimentées par du vrai code, couvertes par 5 tests, et leur non-liaison est un choix de périmètre
explicite, vérifiable par `git diff --stat -- src/Chronos/Views/MainWindow.xaml` (vide).

## Self-Check: PASSED

- `src/Chronos/ViewModels/MainViewModel.cs` — FOUND
- `src/Chronos/ViewModels/WindowGaugeViewModel.cs` — FOUND
- `src/Chronos/Views/SettingsWindow.xaml` — FOUND
- `tests/Chronos.Tests/ReglagesBindingTests.cs` — FOUND (180 lignes, `min_lines: 70` satisfait)
- `tests/Chronos.Tests/MainViewModelTests.cs` — FOUND
- `tests/Chronos.Tests/WindowGaugeViewModelTests.cs` — FOUND
- commit `03dccac` (tâche 1) — FOUND
- commit `325c7dc` (tâche 2) — FOUND
- suite complète : **652 / 652, 0 échec, sur deux exécutions consécutives**
- coffre `%APPDATA%\Chronos\oauth.dat` : 518 octets, mtime 1783863147 — intact
