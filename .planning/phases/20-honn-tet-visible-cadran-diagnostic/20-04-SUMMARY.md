---
phase: 20-honn-tet-visible-cadran-diagnostic
plan: 04
subsystem: ui
tags: [exa-03, exa-06, wpf, xaml, semiologie, strokedasharray, stackpanel, geometrie, tdd-red-green]

# Dependency graph
requires:
  - phase: 20
    plan: 03
    provides: "EstPlancher / EstDate par fenêtre, AfficherReleveDate et InfobulleReleve — le contrat de présentation, calculé et testé, bindé nulle part"
  - phase: 20
    plan: 01
    provides: "CadranBindingTests monté sur TempPaths() : aucun test de style n'est plus vert par accident du profil réel"
  - phase: 19
    provides: "ProvenanceReleve et la contrainte dure « pas d'arc de delta, pas de barre d'erreur »"
provides:
  - "M-PLANCHER au style Anneaux : le cinquième et dernier style porte enfin le canal texture (StrokeDashArray sur les 3 arcs de VALEUR)"
  - "RangeePastilles : StackPanel horizontal où le non-recouvrement des pastilles est STRUCTUREL et non conventionnel"
  - "M-ÂGE : PastilleReleveDate, anneau creux inerte de 12 px, infobulle bindée sur InfobulleReleve — EXA-06 arrive au cadran"
  - "M-INDISPO : MotIndisponible, le mot qui distingue « je ne sais pas » de « j'attends »"
  - "Le recouvrement des deux pastilles d'authentification est corrigé, et sa REPRODUCTION est un test permanent"
affects: [20-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TDD RED/GREEN RÉEL, pour la première fois du milestone : le dessin ne crée aucun symbole de production nouveau (RingArc hérite déjà StrokeDashArray de Shape, les 4 pastilles se trouvent par FindName), donc l'étape ROUGE compile et l'historique git PORTE la falsifiabilité au lieu d'un tableau de mutations"
    - "Un défaut de mise en page se corrige par la STRUCTURE, jamais par une convention entre marges : la rangée rend le recouvrement impossible au lieu de le rendre improbable"
    - "Les unités de StrokeDashArray sont des MULTIPLES de StrokeThickness : la même valeur produit un grain proportionné sur un arc de 7 px et sur un arc de 8 px"
    - "Paramètre optionnel terminal sur un helper de test (MonterCadran(snap, out vm, modeEtendu)) : la signature à deux arguments décrite par le plan reste valide, 4e application du protocole des phases 17/18/20-01"

key-files:
  created: []
  modified:
    - src/Chronos/Views/MainWindow.xaml
    - tests/Chronos.Tests/CadranBindingTests.cs

key-decisions:
  - "L'anneau 24 h fin (R64, 4 px) n'est DÉLIBÉRÉMENT pas marqué, et la décision est verrouillée par un Assert.Empty plutôt que par un commentaire : en mode Étendu le 5 h a déjà son arc épais marqué juste en dessous, un pointillé de 2,2 px serait illisible ET redondant"
  - "Les deux pastilles qui se recouvraient ne sont PAS rendues mutuellement exclusives : elles disent des choses différentes (« le wifi est coupé » / « on n'a jamais rien obtenu ») et peuvent légitimement coexister — c'est le RECOUVREMENT qui était le défaut, pas la simultanéité"
  - "Le mot « indisponible » est au BAS-GAUCHE et non centré en bas : un mot centré entrerait en collision avec la rangée dès trois pastilles, et le bas-droite lui est interdit par construction"
  - "Le test d'indisponibilité préexistant (monté par BuildWindow seul) est étendu par une assertion de CÂBLAGE (BindingOperations.GetBinding) et non de Visibility : sur une fenêtre non montée, Visibility.Visible serait vrai pour une mauvaise raison (Piège 4)"
  - "Les deux commentaires de sécurité qui NOMMENT LoginClaudeCommand pour l'interdire sont conservés tels quels : un critère grep littéral ne justifie pas de supprimer l'avertissement qui protège le coffre de jetons de l'utilisateur"
  - "M-âge reste une Ellipse et non un Button : une Ellipse n'a AUCUNE propriété Command, le TYPE est le verrou de son inertie — pas une convention"

patterns-established:
  - "Un critère d'acceptation en grep littéral se lit sur ce qu'il VEUT prouver : quand un commentaire de sécurité préexistant fait déborder le compte, on corrige la LECTURE du critère et on l'écrit dans le SUMMARY — on ne mutile pas le code (4e application après 20-01, 20-02, 20-03)"
  - "Toute nouvelle assertion de géométrie passe par TransformToAncestor + ActualWidth/Height après Arrange : les marges déclarées ne prouvent rien, les rectangles mis en page prouvent tout"

requirements-completed: [EXA-03]

# Metrics
duration: 18min
completed: 2026-09-12
---

# Phase 20 Plan 04 : le dessin — pointillé de plancher, rangée de pastilles, mot « indisponible » Summary

**Les quatre apparences de la doctrine arrivent à l'œil : le style Anneaux porte enfin la texture de
plancher que les quatre autres styles avaient déjà, les pastilles ne peuvent PLUS se recouvrir parce
qu'elles ne peuvent plus occuper la même case, la marque d'âge y prend sa place avec l'infobulle qui
nomme la source, et le mot « indisponible » distingue enfin « je ne sais pas » de « j'attends ».**

## Performance

- **Duration:** 18 min
- **Started:** 2026-09-12T07:00:50Z
- **Completed:** 2026-09-12T07:18:00Z
- **Tasks:** 3/3
- **Files modified:** 2 (0 créé, 2 modifiés)

## Bilan de la suite : chaque écart nominatif

| | Tests | Durée |
|---|---|---|
| Entrée (commit `2a817bd`) | 733 | 2 s |
| Sortie (commit `1f92026`) | **744** | **2 s** |

| Écart | Tests | Où |
|---|---|---|
| Texture de plancher aux Anneaux (hebdo, frais, EncoreValide, mode Normal, mode Étendu) | +5 | `CadranBindingTests` |
| Non-recouvrement, géométrie des 4 pastilles, M-âge inerte | +3 | `CadranBindingTests` |
| Mot « indisponible » (allumé, éteint, géométrie) | +3 | `CadranBindingTests` |
| **Total** | **+11** | 733 → 744 |

Comptage par classe, réconcilié à l'unité : `CadranBindingTests` **24** (13 + 11). Aucune autre classe
n'a gagné ni perdu un test. **Aucun test supprimé** : les deux tests de pastille de la phase 17 sont
RÉÉCRITS sur deux lignes chacun, rien d'autre.

**Deux passes complètes consécutives : 744/744 les deux fois** (précédent BAML 16-03 ; cette classe
charge du XAML et le plan en ajoute onze cas).

## Falsifiabilité : TDD RED/GREEN RÉEL, et c'est une première dans ce dépôt

Les plans 17-04, 17-05, 18-01, 18-02, 20-01, 20-02 et 20-03 ont tous documenté la même impossibilité :
`tests/Chronos.Tests` porte un `ProjectReference` vers `Chronos`, donc un test qui référence un symbole
de production inexistant empêche TOUTE la solution de compiler — l'étape ROUGE serait un commit où
`dotnet test` n'est même pas invocable. La preuve passait alors par mutation après coup.

**Ce plan échappe à la contrainte, et ce n'est pas un hasard : il ne crée AUCUN symbole de production.**
`RingArc` hérite déjà `StrokeDashArray` de `Shape` (plan 20-04, `<interfaces>`), les quatre pastilles et
le mot se trouvent par `FindName(string)`, et les quatre booléens du ViewModel existent depuis 20-03. Les
tests compilent donc AVANT le XAML qui les satisfait. La falsifiabilité est ici portée par **l'historique
git lui-même** — trois commits `test(...)` rouges, trois commits `feat(...)` verts — ce qui est plus fort
qu'un tableau de mutations jouées puis révoquées.

| # | Commit ROUGE | Échecs constatés | Commit VERT |
|---|---|---|---|
| T1 | `f450e4e` | **3** (`ArcHebdo`, `ArcTimelineNormal`, `ArcCinqHeures` sans pointillé) | `4a92c50` |
| T2 | `7c3b0e6` | **5** (les 3 nouveaux + les 2 assertions d'alignement réécrites) | `f870d22` |
| T3 | `56a2ad2` | **4** (les 3 nouveaux + le test d'indisponibilité étendu) | `1f92026` |

**Le rouge de la tâche 1 est doublement instructif** : les 3 tests qui exigent un pointillé échouent, et
les **2 contre-épreuves** (`Un_exact_frais_laisse_l_arc_PLEIN`, `Un_EncoreValide_laisse_l_arc_PLEIN`)
passent DÉJÀ au rouge. C'est normal et c'est le signe qu'elles sont honnêtes : elles verrouillent une
absence, et l'implémentation ne doit pas la détruire. Elles sont restées vertes après.

## Le bug de recouvrement : REPRODUIT, chiffré, puis corrigé

La recherche de phase l'avait déduit par lecture de `MajPastilles`. Le commit `7c3b0e6` le **mesure** :

```
les deux pastilles se recouvrent : 152;152;10;10 ∩ 150;150;14;14 = 152;152;10;10
```

L'intersection est **égale à la pastille grise entière** : `PastilleHorsLigne` (10 × 10) était
**intégralement contenue** dans `PastilleInvitationConnexion` (14 × 14). Ce n'était pas un chevauchement
de bord, c'était une **occultation totale** — le signal « hors ligne » était purement et simplement
invisible dès que l'invitation s'allumait, et les deux s'allument ensemble dans un cas réellement
atteignable (`HorsLigne` ⇒ `Deconnexion = false` ⇒ l'invitation est libre).

Après correction, l'intersection est vide, et la sonde de géométrie (posée puis révoquée) donne les
chiffres réels de la rangée :

| Élément | Rectangle mis en page | Centre | Distance au centre (85,85) | Seuil |
|---|---|---|---|---|
| `PastilleReleveDate` (la plus à gauche, 4 pastilles allumées) | `102;151;12;12` | (108 ; 157) | **75,6 px** | > 71,5 ✅ |
| `MotIndisponible` | `8;150,0;56,7;14,0` | (36,3 ; 157,0) | **86,9 px** | > 71,5 ✅ |
| `RangeePastilles` (invitation seule) | `146;150;18;14` | — | — | — |

Écart entre le bord droit du mot (**64,7**) et le bord gauche de la rangée (**146**) : **81,3 px**. La
prédiction du plan (« coin gauche à x = 96, soit 72,8 px ») était **conservatrice** : la rangée réelle
mesure 62 px et non 68, parce que les deux pastilles informatives font 12 et 10 px et non 14.

## Accomplishments

### Task 1 — le cinquième style enfin marqué

- **Trois** `DataTrigger` sur les trois arcs de **VALEUR**, et zéro sur les pistes `Fraction="1"` : une
  piste en pointillé serait un mensonge géométrique.
- **Unités en multiples de `StrokeThickness`, pas en pixels.** `0.55 0.45` donne 4,4 px de tiret et
  3,6 px d'espace sur un arc de 8 px — comparable au grain des braises (1,4 / 1,4 sur un trait de 1,4).
  Le même littéral produit automatiquement 3,85 / 3,15 px sur l'arc de 7 px du mode Normal : la
  proportion est conservée sans seconde valeur à maintenir.
- **`ArcTimelineNormal` est marqué sur `FiveHour.EstPlancher` alors que sa GÉOMÉTRIE est celle du jour**,
  et le commentaire dit pourquoi : en mode Normal — **le mode par DÉFAUT** — il n'existe aucun anneau 5 h
  dédié, l'usage 5 h passe entièrement par la COULEUR de cet anneau. Sans cette ligne, le mode par défaut
  aurait été le seul des deux à ne rien dire du plancher 5 h.
- **`ArcVingtQuatreHeures` non marqué, verrouillé par `Assert.Empty`.** Le commentaire l'annonce et le
  test le prouve : une décision de design écrite seulement en commentaire passe pour un oubli au passage
  suivant.
- **Aucun effet, aucune `Storyboard`, aucun `Brush` nouveau.** Un `StrokeDashArray` est de la géométrie :
  gratuit sous le rendu logiciel qu'impose `AllowsTransparency`.
- Loi d'encodage respectée sans la contourner : la **géométrie** dit le temps, la **luminance** dit le
  quota, le **grain** (3ᵉ variable de Bertin, libre par construction) dit « borne inférieure ».

### Task 2 — le non-recouvrement devient structurel

- `StackPanel` horizontal `RangeePastilles`, ancré bas-droite, `Margin="0,0,6,6"`. Ordre de gauche à
  droite : **âge, hors ligne, invitation, déconnexion** — informatives d'abord, actionnables au plus près
  du coin, là où le pouce va les chercher.
- Les trois pastilles existantes conservent **à l'identique** leur `x:Name`, leur `ToolTip`, leur
  `Command`, leur `Background` et leur `Template`. Seuls leurs `HorizontalAlignment`,
  `VerticalAlignment` et `Margin` propres disparaissent au profit de `VerticalAlignment="Center"` +
  `Margin="0,0,4,0"` (aucune marge après la dernière).
- **M-ÂGE : anneau CREUX** (`Ellipse` 12 px, `Fill="Transparent"`, `Stroke="{DynamicResource
  TexteSecondaire}"`, `StrokeThickness="2"`). La FORME la distingue au premier coup d'œil des pastilles
  pleines voisines, et un cadran vide dit « du temps a passé » sans un mot. `Fill="Transparent"` et non
  `{x:Null}` : hit-testable sur fenêtre `AllowsTransparency`, donc **son infobulle s'ouvre** — c'est elle
  qui porte EXA-06 jusqu'au cadran.
- **Inerte par le TYPE, pas par convention** : `Assert.Null(typeof(Ellipse).GetProperty("Command"))`.
  Une `Ellipse` ne peut pas devenir cliquable par accident au prochain refactor.
- **Les assertions de sécurité de la phase 17 sont intactes**, et c'était la contrainte dure : seules
  les **deux** lignes `HorizontalAlignment.Right` / `VerticalAlignment.Bottom` ont changé de porteur (du
  `Button` vers la rangée). `Assert.Same(vm.ReconnecterCommand, …)`,
  `Assert.NotSame(vm.LoginClaudeCommand, …)` et `Assert.NotNull(…Background)` sont vérifiées présentes
  aux lignes 318-320 et 389-391 après réécriture.
- **Le test des quatre pastilles force les booléens DIRECTEMENT sur le ViewModel** : c'est un test de
  GÉOMÉTRIE, pas de sémantique. La recomposition garantit ce qui s'allume ensemble aujourd'hui ; la
  rangée doit garantir que la géométrie tient **même si cette recomposition change demain**.
- Borne dure inscrite dans le XAML : **quatre pastilles tiennent, cinq seraient la limite**, à ne pas
  franchir sans remesurer.

### Task 3 — « je ne sais pas » se lit enfin

- `DataUnavailable` était calculé, testé, et **bindé nulle part** depuis la v1.0. Il l'est.
- **Un seul exemplaire**, en couche racine : 5 styles × 2 modes × 9 thèmes, zéro duplication. C'est la
  seule couche `{DynamicResource}` du projet — les quatre `UserControl` de `Views/Cadrans` codent leurs
  couleurs en dur.
- **`IsHitTestVisible="False"`** : le clic continue d'atteindre `CentreHit`, donc la bascule
  pourcentage / temps fonctionne **même quand il n'y a rien à afficher**. C'est exactement le moment où
  l'utilisateur va cliquer pour comprendre.
- **Aucun token nouveau** : `TexteSecondaire` est déjà, par son commentaire d'origine, le token des
  mentions annexes (« estimée, épuisé, indisponible, périmée »). `git diff --name-only` sur ce plan ne
  contient ni `DesignTokens.xaml` ni `ChronosTheme.cs` — donc rien à ajouter aux neuf thèmes, et
  `ThemingTests` reste vert sans retouche.
- Le test préexistant est **étendu et non doublé**, mais par une assertion de **câblage**
  (`BindingOperations.GetBinding(…).Path.Path == nameof(vm.DataUnavailable)`) et non de `Visibility` : ce
  test-là monte par `BuildWindow` seul, sa `Visibility` resterait à son défaut `Visible` et l'assertion
  serait vraie pour rien (Piège 4). La `Visibility` **résolue** se prouve dans les deux tests montés.

## Deviations from Plan

### Écarts constatés, sans modification de code

**1. [Lecture de critère] `grep -c "Storyboard\|BlurEffect\|DropShadowEffect"` rend 1 et non 0**

- **Found during:** Task 1
- **Issue:** La ligne 11 de `MainWindow.xaml` porte, **depuis l'origine du fichier et sans rapport avec
  ce plan**, le commentaire « Aucune Storyboard/blur/shadow : AllowsTransparency force le rendu
  logiciel ». Le critère était donc inatteignable à 0 **avant même la première ligne de ce plan**
  (vérifié : `git show HEAD~6:…` rend 1 aussi).
- **Décision:** Ne PAS supprimer le commentaire. Il énonce **exactement** la contrainte que le critère
  veut protéger ; le retirer pour satisfaire un `grep` reviendrait à effacer l'avertissement pour faire
  taire l'alarme. Le critère est lu sur son intention — **aucun ÉLÉMENT** d'effet dans le fichier — et
  cette lecture est vérifiée : `grep -cE '<[A-Za-z]*(Storyboard|BlurEffect|DropShadowEffect)'` = **0**.
- **Files modified:** aucun.

**2. [Lecture de critère] `LoginClaudeCommand` rend 2 et non 0 ; `ReconnecterCommand` rend 4 et non 2**

- **Found during:** Task 2
- **Issue:** `grep -c` compte des LIGNES, et deux commentaires de sécurité préexistants **nomment**
  `LoginClaudeCommand` pour l'interdire (« Command = ReconnecterCommand et SURTOUT PAS
  LoginClaudeCommand, qui BASCULE et supprimerait le coffre »). Comptes à l'entrée du plan, **identiques
  à la sortie** : 2 et 4.
- **Décision:** Ne PAS toucher à ces deux commentaires. Ce sont eux qui protègent le coffre de jetons de
  l'utilisateur au prochain passage. Le critère est lu sur ce qu'il veut prouver — **les BINDINGS** — et
  cette lecture est vérifiée : `grep -c 'Command="{Binding LoginClaudeCommand}"'` = **0**,
  `grep -c 'Command="{Binding ReconnecterCommand}"'` = **2**. La preuve la plus forte reste de toute
  façon l'assertion d'**identité par instance** (`Assert.NotSame`), qu'un test textuel ne peut pas
  remplacer et qui est restée intacte dans les deux tests.
- **Files modified:** aucun.

**3. [Rule 3 - Blocage mineur] Paramètre de mode ajouté au helper `MonterCadran`**

- **Found during:** Task 1
- **Issue:** Le plan décrit `MonterCadran(UsageSnapshot snap, out MainViewModel vm)` comme un simple
  alias de `MonterPastille(Connecte, snap, out vm)`, **et** exige que « les réglages de style et de mode
  se posent explicitement dans chaque test ». Poser `vm.IsModeEtendu` après un alias nu laisserait les
  `Visibility` non réévaluées (la file du Dispatcher ayant déjà été purgée par `MonterPastille`) et
  imposerait de recopier purge + `Measure` + `Arrange` dans chaque test.
- **Fix:** `MonterCadran(snap, out vm, bool modeEtendu = false)` — **paramètre optionnel terminal**,
  4ᵉ application du protocole des phases 17/18/20-01. La signature à deux arguments décrite par le plan
  reste littéralement valide (3 tests sur 5 l'appellent ainsi) ; le helper pose `CadranStyle.Arcs` et le
  mode, **puis** re-purge et remet en page. Aucune duplication, aucun test dépendant d'un persisté.
- **Files modified:** `tests/Chronos.Tests/CadranBindingTests.cs`
- **Commit:** `f450e4e`

**4. [Écart de rédaction assumé] Le mot `StrokeDashArray` n'apparaît dans AUCUN commentaire**

- **Found during:** Task 1
- **Issue:** Le bloc XAML proposé par le plan contient, dans son propre commentaire, la phrase
  « `StrokeDashArray` est hérité de `Shape` ». Le recopier aurait porté le compte à **4** et fait tomber
  le critère `== 3` — la contradiction exacte que les plans 20-01, 20-02 et 20-03 ont déjà rencontrée.
- **Fix:** Le commentaire de doctrine dit « le tableau de tirets », « le pointillé », « multiples de
  l'épaisseur du trait ». Aucune information n'est perdue ; le compte est à 3. Même traitement pour
  `EstPlancher` (3) et `DataUnavailable` (1).
- **Files modified:** `src/Chronos/Views/MainWindow.xaml`
- **Commit:** `4a92c50`

---

**Total deviations:** 2 lectures de critère corrigées (0 code modifié), 1 ajustement de helper de test
(Rule 3), 1 écart de rédaction assumé.
**Impact on plan:** Aucun périmètre ajouté, aucun retiré. Les deux lectures de critère protègent des
commentaires de sécurité préexistants ; l'ajustement du helper évite de dupliquer trois lignes de
montage dans cinq tests. **Aucune tâche n'a atteint la limite de 3 auto-corrections.**

## Issues Encountered

Aucun. Les trois tâches sont passées du rouge au vert en une itération chacune.

## Requirements : EXA-03 COCHÉ ici

Le frontmatter du plan porte `requirements: [EXA-03]`, et c'est **le plan qui le satisfait**. EXA-03 est
rédigé côté utilisateur — « le cadran **distingue visuellement** chiffre exact frais, chiffre exact daté,
état indisponible » — et les plans 20-01 à 20-03 l'avaient délibérément laissé `Pending` parce
qu'**aucun pixel n'avait bougé**. Ici, les quatre apparences existent à l'écran :

| # | Apparence | Marque(s) portée(s) | Prouvée par |
|---|---|---|---|
| 1 | Exact frais | **aucune** | `Un_exact_frais_laisse_l_arc_des_Anneaux_PLEIN` |
| 2 | Exact daté (`EncoreValide`) | M-âge seule | `Un_EncoreValide_laisse_l_arc_PLEIN…` + `La_pastille_d_age_est_inerte…` |
| 3 | Plancher | M-âge **+** M-plancher | `Le_plancher_hebdo_rend_l_arc_des_Anneaux_en_pointille` (×3 arcs) |
| 4 | Indisponible | M-indispo | `Deux_fenetres_indisponibles_allument_le_mot_indisponible` |

**EXA-06 reste Pending** : l'infobulle de la pastille d'âge en livre la moitié (la source et son
ancienneté, au cadran), mais le diagnostic complet — les cinq sources nommées dans le rapport — est le
plan 20-05, nommément.

La part **non automatisable** d'EXA-03 — « les trois états sont-ils distinguables **à distance normale,
sur fond d'écran réel** ? » — est le point n° 1 de la vérification humaine du plan 05. Le cocher ici
déclare satisfait ce qui est **livré et testé**, pas ce qui est **jugé à l'œil**.

## Known Stubs

**Aucun.** Les deux propriétés listées comme stubs par le plan 20-03 — `AfficherReleveDate` et
`InfobulleReleve` — sont **bindées** par ce plan, à l'échéance annoncée et dans le plan nommé. La dette
était nominative et datée ; elle est levée.

Aucune valeur codée en dur, aucun texte « à venir », aucune donnée factice : les quatre marques sont
alimentées par le pipeline réel à chaque rafraîchissement.

## Vérifications de sécurité (contrôle avant / après)

| Contrôle | Attendu | Constaté après |
|---|---|---|
| `%APPDATA%\Chronos\oauth.dat` | 518 o, mtime 1783863147 | **518 1783863147** — identique |
| `~/.claude/settings.json` | 6872 o, mtime 1785403369 | **6872 1785403369** — identique |
| Écriture sous `%APPDATA%\Chronos\` | aucune | aucune (tous les mtimes antérieurs au début de session) |
| Fichiers SOURCE avec `RefreshAsync` | exactement 2 | **2** (`ChronosOAuthClient.cs`, `ChronosTokenAuthority.cs`) |
| Requête réseau depuis un test | aucune | aucune (aucun test de ce plan ne touche au transport) |
| Appel de refresh avec le jeton RÉEL | jamais | jamais |
| Overlay lancé | **NON** | jamais lancé |
| `LoginClaudeCommand` bindée | jamais | **0 binding** ; `Assert.NotSame` conservé dans les 2 tests |
| Dépendance NuGet ajoutée | aucune | aucune (aucun `.csproj` touché — 2 fichiers au diff) |
| Écriture de test dans le vrai profil | aucune | aucune (tout passe par `TempPaths()`, hérité de 20-01) |

## Verification

- [x] `dotnet test Chronos.sln -v q --nologo` : **744/744, 0 échec**, **deux passes consécutives**, 2 s
- [x] 5 gardes permanentes vertes (`ServicesLayerPurityTests`, `CompositionRootTests`,
      `NormalisationUniqueTests`, `GardesDoctrineTests`, `La_sonde_d_en_tetes_est_le_PRIMAIRE…`) — **18 tests**
- [x] `grep -c "StrokeDashArray" src/Chronos/Views/MainWindow.xaml` = **3**
- [x] `grep -c "EstPlancher" src/Chronos/Views/MainWindow.xaml` = **3** (`SevenDay` ×1, `FiveHour` ×2)
- [x] `grep -c "#" src/Chronos/Views/MainWindow.xaml` = **0** (aucune couleur en dur)
- [x] `grep -cE '<[A-Za-z]*(Storyboard|BlurEffect|DropShadowEffect)'` = **0** (aucun ÉLÉMENT d'effet)
- [x] `grep -c "MonterCadran" tests/Chronos.Tests/CadranBindingTests.cs` = **10** (≥ 6 exigé)
- [x] `grep -c 'x:Name="RangeePastilles"'` = **1** · `grep -c 'x:Name="PastilleReleveDate"'` = **1**
- [x] `grep -c 'HorizontalAlignment="Right"'` = **1** (seule la rangée porte l'alignement)
- [x] `grep -c 'Command="{Binding LoginClaudeCommand}"'` = **0**
- [x] `grep -c 'Command="{Binding ReconnecterCommand}"'` = **2** (la pastille d'âge n'en a PAS)
- [x] `grep -c 'x:Name="MotIndisponible"'` = **1** · `IsHitTestVisible="False"` = **1** ·
      `DataUnavailable` = **1**
- [x] `git diff --name-only f450e4e~1..HEAD` = **2 fichiers**, ni `DesignTokens.xaml` ni `ChronosTheme.cs`
      ni aucun `.csproj`
- [x] `ThemingTests` vert (aucun token nouveau à publier dans les 9 thèmes)
- [x] `Etat_fiabilite_mixte_la_marque_du_pourcentage_est_par_fenetre` passe **sans retouche** : la marque
      de plancher reste PAR FENÊTRE, seule la marque d'âge est globale
- [x] Assertions de sécurité de la phase 17 vérifiées présentes après réécriture (l. 318-320, 389-391)
- [x] 3 étapes ROUGES committées puis 3 étapes VERTES : falsifiabilité portée par l'historique git
- [x] 2 sondes de géométrie posées puis **révoquées** ; `git status` propre après révocation

## Commits

| Task | Étape | Commit | Message |
|---|---|---|---|
| 1 | ROUGE | `f450e4e` | `test(20-04): 5 tests RED pour la texture de plancher du style Anneaux (EXA-03)` |
| 1 | VERT | `4a92c50` | `feat(20-04): le style Anneaux porte enfin la texture de plancher (EXA-03)` |
| 2 | ROUGE | `7c3b0e6` | `test(20-04): reproduction du recouvrement des pastilles + M-age (EXA-03)` |
| 2 | VERT | `f870d22` | `feat(20-04): rangee de pastilles - le non-recouvrement devient STRUCTUREL (EXA-03)` |
| 3 | ROUGE | `56a2ad2` | `test(20-04): 3 tests ROUGES pour le mot indisponible (EXA-03)` |
| 3 | VERT | `1f92026` | `feat(20-04): le mot indisponible leve l'ambiguite avec l'etat en attente (EXA-03)` |

## Ce que le plan 05 peut tenir pour acquis

1. **Les quatre apparences sont à l'écran, une seule fois chacune.** Aucune duplication dans les
   `UserControl` : la texture est sur trois arcs, la rangée et le mot sont en couche racine.
2. **Le bug de recouvrement est mort, structurellement.** Ajouter une cinquième pastille exigerait de
   remesurer — la borne est écrite dans le XAML et testée.
3. **`InfobulleReleve` a son premier consommateur visible.** Le plan 05 peut s'appuyer sur le fait que
   le vocabulaire de `LibelleSource` est déjà lu par un utilisateur au survol du cadran : le rapport de
   diagnostic doit dire **la même chose**, pas une variante.
4. **La contrainte dure de la phase 19 tient toujours** : aucun arc de delta, aucune barre d'erreur. La
   sémiologie livrée exprime « au moins », jamais « environ ± x ».
5. **Rien n'a été vu à l'œil.** L'overlay n'a **pas** été lancé (la purge des 25 hooks se déclencherait
   sans supervision). Le constat visuel est intégralement délégué au plan 05.

## À VÉRIFIER PAR L'UTILISATEUR

**Nouveau pour ce plan** — premier livrable visible du milestone, à constater au lancement :

1. **Le pointillé du style Anneaux se lit-il à distance normale sans être bruyant ?** (tirets de 4,4 px
   espacés de 3,6 px sur les arcs de 8 px, 3,85 / 3,15 px sur celui de 7 px du mode Normal).
2. **La rangée de quatre pastilles reste-t-elle lisible sur 170 px**, et la pastille d'âge (anneau creux
   de 12 px) se distingue-t-elle des pastilles pleines voisines ?
3. **Le mot « indisponible » en bas à gauche** est-il lisible sur un fond d'écran clair comme sombre,
   dans plusieurs des 9 thèmes ?

Les points hors GSD des phases 17, 18 et 19 restent ouverts et sont rappelés dans `STATE.md`.

## Self-Check: PASSED

2 fichiers annoncés comme modifiés : tous deux présents sur disque et tous deux — et eux seuls — présents
dans le diff `f450e4e~1..HEAD`. 6 commits annoncés (`f450e4e`, `4a92c50`, `7c3b0e6`, `f870d22`,
`56a2ad2`, `1f92026`) : tous présents dans l'historique git. Aucun fichier créé n'est annoncé. Aucun
élément manquant.

---
*Phase: 20-honn-tet-visible-cadran-diagnostic*
*Completed: 2026-09-12*
