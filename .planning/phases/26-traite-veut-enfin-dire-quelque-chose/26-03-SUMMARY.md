---
phase: 26-traite-veut-enfin-dire-quelque-chose
plan: 03
subsystem: sessions
tags: [geste-explicite, menu-contextuel, effet-de-masse, reversibilite, libelles, xunit, wpf]

sha_entree_de_phase: 07ee784
sha_entree_de_plan: 7660ef7

requires:
  - phase: 26-01
    provides: "RetentionMax 24 h (point b bis) — sans elle, le geste sur une session attendant depuis sept heures serait un no-op silencieux ; et NET-03, qui rend le libellé « revient si elle me redemande » exact"
  - phase: 26-02
    provides: "ArchiveStore sans aucune durée de vie — c'est ce qui rend vrai le libellé « Archiver définitivement (ne revient jamais) »"
  - phase: 21-la-source-unique
    provides: "Les huit DataTemplate et leurs huit ContextMenu, le chemin déjà éprouvé du clic droit"
  - phase: 25-le-contrat-d-evenements-refonde
    provides: "WaitingDeduced — l'attente déduite qui sature le widget et justifie le geste de masse"
provides:
  - "SessionItemVm.UpdatedAtMs — l'instant que le SIGNAL portait, ce que le geste inscrit dans le magasin"
  - "MarquerTraiteeCommand / MarquerToutTraiteCommand — deux gestes RÉVERSIBLES, jamais l'archivage"
  - "ToutTraiterLibelle — le libellé du geste de masse, avec le NOMBRE et l'annonce du retour"
  - "Huit menus contextuels à trois entrées : réversible en tête, définitif en queue derrière un Separator"
  - "Deux gardes complémentaires : le câblage lu dans le TEXTE du XAML, et les 72 combinaisons montées"
affects: [26-04-le-contrat-documente]

tech-stack:
  added: []
  patterns:
    - "Un geste de masse doit dire sur COMBIEN il porte avant d'être cliqué — et, encadré par un libellé réversible et un libellé définitif, dire aussi s'il revient"
    - "Le libellé du geste de masse vient du ViewModel, jamais du XAML : en dur dans le gabarit, il ne pourrait plus porter le nombre"
    - "Un ContextMenu n'est pas dans l'arbre visuel de la fenêtre : il est la VALEUR d'une propriété, donc vérifiable sans ouvrir aucun menu ni afficher aucune fenêtre"
    - "Deux gardes valent mieux qu'une : le TEXTE du XAML attrape une commande débranchée, l'arbre VISUEL attrape un gabarit oublié"
    - "Le geste ordinaire n'écrit QUE dans le magasin réversible : il ne peut pas devenir destructif par élargissement"

key-files:
  created:
    - tests/Chronos.Tests/GesteTraiteTests.cs
  modified:
    - src/Chronos/ViewModels/SessionsViewModel.cs
    - src/Chronos/ViewModels/SessionsPreviewViewModel.cs
    - src/Chronos/Views/SessionsController.cs
    - src/Chronos/Views/SessionsWindow.xaml.cs
    - src/Chronos/Resources/SessionStyles.xaml
    - src/Chronos/Services/DiagnosticService.cs
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/AffichageSessionsTests.cs
    - tests/Chronos.Tests/SessionStylesBindingTests.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs

key-decisions:
  - "La tâche 1 est UN seul commit et non un cycle rouge/vert : les cinq cas de GesteTraiteTests lisent des membres qui n'existaient pas (UpdatedAtMs, MarquerTraiteeCommand, ToutTraiterLibelle) — un commit « RED » y serait un commit qui ne compile pas, interdit sans exception. Le plan et 26-VALIDATION.md les annoncent d'ailleurs « verts dès leur écriture »."
  - "Le cas des sept heures a tout de même été MESURÉ rouge, HORS COMMIT : RetentionMax remise à FromHours(6), la suite GesteTraite tombe à 4/5 et le seul échec est nominativement Marquer_traitee_une_session_qui_attend_depuis_sept_heures_la_fait_disparaitre. Le fichier a été restauré par git checkout. Ce n'est PAS un rouge-avant dans l'ordre des vagues — (b bis) précède de deux plans — c'est la preuve opposable de cette décision-là, que 26-VALIDATION.md autorise à constater sans l'exiger."
  - "Le geste de masse écrit dans _treated.Set, jamais dans ArchiveStore : le verrou UI de ce projet (LoginClaudeCommand bascule, et un clic effacerait le coffre de jetons) interdit d'attacher un geste ordinaire à une commande ambiguë. Il n'a pas été élargi d'un pouce."
  - "Le libellé du geste de masse n'apparaît PAS en dur dans le XAML (mesuré : 0 occurrence) : il est bindé sur ToutTraiterLibelle, sans quoi le nombre de sessions ne serait plus affiché."
  - "Aucun renommage. Aucun test supprimé. Aucune dépendance NuGet ajoutée."

requirements-completed: [TRT-03, TRT-04]

metrics:
  duration: "~11 min"
  completed: 2026-09-13
---

# Phase 26 Plan 03 : le geste explicite, et des libellés qui disent la vérité — Summary

**L'utilisateur peut enfin dire lui-même « c'est traité » — d'un clic droit sur une session, ou d'un seul
geste sur toutes — et les trois entrées du menu annoncent chacune si la session revient : le réversible en
tête, le définitif en queue derrière un séparateur, et « Archiver » ne ment plus.**

## Performance

- **Duration :** ~11 min
- **Started :** 2026-09-12T22:42Z
- **Completed :** 2026-09-12T22:53Z
- **Tasks :** 3/3
- **Files created/modified :** 1 créé, 10 modifiés

## La chaîne de totaux, mesurée

| Étape | Commande | Total | Échecs | Durée |
|---|---|---|---|---|
| Entrée de plan (baseline) | `dotnet test Chronos.sln -c Debug --nologo -v q` | **879** | 0 | 5 s |
| Fin tâche 1 | idem | **884** (+5) | **0** | 5 s |
| Fin tâche 2 | idem | **884** (+0) | **0** | 5 s |
| Fin tâche 3 | idem | **886** (+2) | **0** | **9 s** (1re exécution, juste après reconstruction) |
| Vérification, 1re exécution | idem | **886** | 0 | **7 s** |
| Vérification, 2e exécution | idem | **886** | 0 | **7 s** |

Chaîne annoncée **879 → 884 (+5) → 884 (+0) → 886 (+2)** : **tenue exactement**, aucun écart à justifier.
`26-VALIDATION.md` annonçait **886** après 26-03 : **886 mesuré**.

**Le seuil de 8 s est franchi une fois, et c'est dit plutôt que corrigé en retirant des combinaisons** :
la première exécution suivant une reconstruction complète prend **9 s** (les 72 montages supplémentaires de
`Les_trois_gestes_sont_offerts_sur_les_8_styles_et_les_9_themes` coûtent ~0,9 s, et la reconstruction pèse
sur la première passe). Les deux exécutions consécutives de vérification, elles, tiennent en **7 s** —
sous le seuil. Aucune combinaison n'a été retirée.

## Les étapes ROUGE — la qualification honnête, cas par cas

Ce plan n'a **aucune étape rouge dans son propre ordre d'exécution**, et c'est annoncé par le plan
lui-même comme par `26-VALIDATION.md`. Voici la qualification de chacun des **sept** tests ajoutés :

| Test | Tâche | Qualification | Mesuré |
|---|---|---|---|
| `Marquer_traitee_fait_disparaitre_la_session_immediatement` | T1 | **vert dès l'écriture** (porte sur du code écrit dans la même tâche ; un commit qui ne compile pas est interdit) | vert |
| `Marquer_tout_traite_vide_le_widget_en_un_geste` | T1 | **vert dès l'écriture** | vert |
| `Le_libelle_du_geste_de_masse_porte_le_nombre_de_sessions` | T1 | **vert dès l'écriture** | vert |
| `Le_geste_est_reversible_et_le_libelle_ne_ment_pas` | T1 | **vert dès l'écriture** | vert |
| `Marquer_traitee_une_session_qui_attend_depuis_sept_heures_la_fait_disparaitre` | T1 | **non mesurable rouge dans l'ordre des vagues** — il est rouge sans le point (b bis) de 26-01, livré en vague 1, deux plans avant sa naissance | vert ; **et rouge mesuré hors commit, voir ci-dessous** |
| `Les_huit_menus_offrent_le_geste_explicite_et_disent_la_verite` | T3 | **vert dès l'écriture** — c'est une GARDE, pas un test de développement : la tâche 2 a déjà livré le câblage | vert |
| `Les_trois_gestes_sont_offerts_sur_les_8_styles_et_les_9_themes` | T3 | **vert dès l'écriture** — garde également | vert |

**Zéro rouge annoncé, zéro rouge mesuré.** Aucun test ajouté n'est vacueux : chacun porte une assertion
anti-muette (5 items, 8 templates, 8 styles couverts, au moins un menu par style).

### La preuve opposable du point (b bis), mesurée HORS COMMIT

`26-VALIDATION.md` autorise ce constat sans l'exiger. Il a été fait, puis annulé :

```
sed -i 's/FromHours(24)/FromHours(6)/' src/Chronos/Services/TreatedStore.cs
dotnet test --filter "FullyQualifiedName~GesteTraite"
  → Échoué!  - échec : 1, réussite : 4, ignorée(s) : 0, total : 5
  Réussi : Marquer_traitee_fait_disparaitre_la_session_immediatement
  Réussi : Le_geste_est_reversible_et_le_libelle_ne_ment_pas
  Réussi : Marquer_tout_traite_vide_le_widget_en_un_geste
  Réussi : Le_libelle_du_geste_de_masse_porte_le_nombre_de_sessions
  → le SEUL échec : Marquer_traitee_une_session_qui_attend_depuis_sept_heures_la_fait_disparaitre
git checkout -- src/Chronos/Services/TreatedStore.cs   → FromHours(24) restauré, git status vide
```

**Un sur cinq, exactement celui-là.** Les quatre autres, tous horodatés au frais, ne voyaient rien — c'est
la démonstration littérale de ce que le plan annonçait : sans (b bis), `Set(id, UpdatedAtMs)` inscrivait un
instant vieux de sept heures que `Load()` écartait aussitôt, et le clic était un **no-op silencieux** sur
exactement le genre de session dont ce milestone est parti.

## Les valeurs réelles de chaque `grep`

### Tâche 1 — `src/Chronos/ViewModels/SessionsViewModel.cs` et ses sites d'appel

| Mesure | Attendu | **Mesuré** | Verdict |
|---|---|---|---|
| `dotnet build Chronos.sln -c Debug --nologo` | 0 erreur | **0 erreur, 0 avertissement** | ✅ |
| `dotnet test … -v q` | 0 échec, **884** | **0 échec, 884** | ✅ |
| Les 5 cas de `GesteTraiteTests`, nommément | verts | **5/5 verts, les cinq nommés par le runner** | ✅ |
| `grep -c "MarquerTraitee\b" …/SessionsViewModel.cs` | ≥ 1 | **1** | ✅ |
| `grep -c "MarquerToutTraite" …/SessionsViewModel.cs` | ≥ 3 | **3** (commande de la ligne, méthode du VM, passage au constructeur) | ✅ |
| `grep -cF "_treated.Set(" …/SessionsViewModel.cs` | 2 | **2** (geste unitaire, geste de masse) | ✅ |
| `grep -rn "new SessionItemVm(" --include=*.cs src/Chronos \| wc -l` | 2 | **2** — `SessionsViewModel.cs:191`, `SessionsPreviewViewModel.cs:37` *(la ligne du VM est passée de 141 à 191 : le fichier s'est allongé en amont, aucun troisième site n'a été créé)* | ✅ |
| `grep -c "GetFolderPath\|APPDATA" tests/…/GesteTraiteTests.cs` | 0 | **0** | ✅ |
| `grep -c "elles reviennent si elles redemandent" …/SessionsViewModel.cs` | 2 | **2** (valeur par défaut du libellé, valeur posée par `Refresh`) | ✅ |
| `grep -c "MotifMasquage" …/DiagnosticService.cs` | 3 (inchangé) | **3** | ✅ |
| `grep -c "posée automatiquement" …/DiagnosticService.cs` | 0 | **1 → 0** | ✅ |
| `Le_diagnostic_ne_fabrique_aucun_moniteur_de_sessions` | vert | **vert** | ✅ |
| `git diff --stat 07ee784..HEAD -- src/Chronos/Resources` (fin T1) | vide | **vide** | ✅ |
| Suites de gardes (9 filtres) | 0 échec | **76/76 verts** | ✅ |

### Tâche 2 — `src/Chronos/Resources/SessionStyles.xaml`

| Mesure (`grep -cF`, donc des LIGNES) | Attendu | **Mesuré** | Verdict |
|---|---|---|---|
| `<MenuItem` | 24 | **24** (8 → 24) | ✅ |
| `<Separator/>` | 8 | **8** (0 → 8) | ✅ |
| `<ContextMenu ` | 8 | **8** (inchangé) | ✅ |
| `MarquerTraiteeCommand` | 8 | **8** | ✅ |
| `MarquerToutTraiteCommand` | 8 | **8** | ✅ |
| `ArchiveCommand` | 8 | **8** | ✅ |
| `ToutTraiterLibelle` | 8 | **8** | ✅ |
| `ne revient jamais` | 8 | **8** | ✅ |
| `retirer de l'overlay` | 0 | **2 → 0** | ✅ |
| `DataTemplate x:Key=` | 8 (inchangé) | **8** | ✅ |
| `elles reviennent si elles redemandent` | **0** | **0** — le libellé vient du ViewModel, via `{Binding ToutTraiterLibelle}` | ✅ |
| `dotnet build` | 0 erreur | **0 erreur, 0 avertissement** | ✅ |
| `dotnet test … -v q` | 0 échec, **884**, +0 test | **0 échec, 884** | ✅ |
| `Les_8_styles_et_les_9_themes_se_chargent_se_mesurent_et_se_disposent` | VERT | **VERT** (998 ms) — aucune des 72 combinaisons n'a de taille dégénérée : **la compacité tient** | ✅ |
| `Le_style_Pastilles_n_a_plus_qu_un_seul_separateur_visible_par_session` | VERT, compte **5** | **VERT** — le `<Separator/>` du menu n'est pas un `TextBlock`, le compte reste 5 | ✅ |
| `Aucun_style_de_session_ne_binde_plus_un_libelle_de_type` | VERT | **VERT** | ✅ |

**`git diff --numstat 07ee784..HEAD -- src/Chronos/Resources/SessionStyles.xaml`** (mesuré à la fin de la
tâche 2, et inchangé depuis — la tâche 3 ne touche pas au XAML) :

```
32	8	src/Chronos/Resources/SessionStyles.xaml
```

**32 insertions, 8 suppressions** : les 8 anciennes lignes `<MenuItem … ArchiveCommand>` remplacées par
8 × 4 = 32 lignes. Aucun `DataTemplate`, `DataTrigger`, `Storyboard`, taille, marge ou police n'a bougé.

### Tâche 3 — les deux gardes

| Mesure | Attendu | **Mesuré** | Verdict |
|---|---|---|---|
| `dotnet test … -v q` | 0 échec, **886** | **0 échec, 886** | ✅ |
| Les deux nouveaux tests | verts dès l'écriture | **verts** (garde de source 2 ms, matrice 883 ms) | ✅ |
| `grep -c "MenusContextuels" tests/…/SessionStylesBindingTests.cs` | 3 | **3** (déclaration, appel récursif, appel dans le test) | ✅ |
| `grep -c "\[Fact\]" tests/…/GardesPerimetreTests.cs` | 11 | **11** — valeur d'entrée **mesurée avant écriture : 10**, conforme | ✅ |
| `grep -c "\.Show()\|ShowDialog" tests/…/SessionStylesBindingTests.cs` | 0 | **0** — aucune fenêtre affichée, aucun menu ouvert | ✅ |
| Suite complète | ≤ 8 s | **7 s / 7 s** aux deux vérifications ; **9 s** à la première passe post-reconstruction | ⚠ dit, non corrigé |

**Le nombre de menus par style, MESURÉ et reporté comme le plan le demande :** **5** (cinq lignes de
session, cinq menus contextuels), relevé hors commit par une assertion temporaire
(`Assert.Equal(-1, menus.Count)` → `Actual: 5`), puis retirée par `git checkout`. **Aucune assertion du
test ne dépend de ce nombre** : celle qui compte est « chaque menu trouvé porte trois entrées ». Aucun
style n'a produit zéro menu — l'assertion anti-muette n'a jamais eu à se plaindre, et les 8 styles sont
tous couverts.

### Acquis des phases précédentes, revérifiés

| Acquis | Attendu | **Mesuré** |
|---|---|---|
| `grep -cF "?? new " src/Chronos/Services/DiagnosticService.cs` | 2 | **2** |
| `grep -rhF "?? new " src/ \| wc -l` (somme du dépôt) | 6 à l'entrée de phase | **8** — les deux `clock ?? new SystemClock()` de 26-01 et 26-02, hors `DiagnosticService.cs` ; le compte gardé reste **2** |
| `grep -c "MotifMasquage" …/DiagnosticService.cs` | 3 | **3** — énumération (`LectureSessions.cs`) à **2** valeurs, `Archivee` / `Traitee`, inchangée |
| `grep -c "new SessionMonitor" …/DiagnosticService.cs` | 0 | **0** |
| `grep -c "Inspecter(now).Visibles" …/SessionMonitor.cs` | 1 | **1** (unicité de `Read(now) => Inspecter(now).Visibles`) |
| `PurgerPrefixe` intact vs `07ee784` | inchangé | **corps identique, 1297 = 1297 octets** ; et `git diff --stat 7660ef7..HEAD -- …/ArchiveStore.cs` est **VIDE** : ce plan n'a pas touché le fichier |
| diff sur `SessionHookProcessor.cs`, `EcritureEtatSession.cs`, `CatalogueEvenementsHooks.cs` | vide | **vide** |
| `git diff --stat 07ee784..HEAD -- '*.csproj'` | vide | **vide** — aucune dépendance NuGet |
| `ServicesLayerPurityTests`, `NormalisationUniqueTests`, `CompositionRootTests`, `GardesDoctrineTests`, `ContratHooksDocumenteTests`, `ReglagesBindingTests`, `TreatedSessionsTests`, `ArchiveStorePurgeTests`, `InspectionSessionsTests` | verts | **tous verts** (886/886) |

**`git diff --stat 07ee784..HEAD -- src/Chronos/Services`** — la règle dure « aucune logique ne descend
dans la couche neutre par ce geste d'interface » :

```
 src/Chronos/Services/ArbitrageSessions.cs       |  15 +++-      (26-01)
 src/Chronos/Services/ArchiveStore.cs            |  45 ++++----   (26-02)
 src/Chronos/Services/DiagnosticService.cs       |   2 +-        (26-03 T1 — UNE seule chaîne)
 src/Chronos/Services/SessionMonitor.cs          |   2 +-        (26-01)
 src/Chronos/Services/SessionTreatmentTracker.cs | 109 +++-----   (26-01)
 src/Chronos/Services/TreatedStore.cs            |  72 ++++---    (26-01/26-02)
```

**Exactement les six fichiers autorisés, et pas un de plus.** `DiagnosticService.cs` pèse **2 lignes**
(`1 +`, `1 -`) : la seule chaîne de `LibelleMotif`. Aucun type WPF dans `Services/` ni `Models/` —
`ServicesLayerPurityTests` est vert.

## Task Commits

1. **Task 1 : deux commandes — traiter celle-ci, ou toutes** — `927050e` (feat)
2. **Task 2 : huit menus, trois entrées, des libellés qui disent la vérité** — `923d1d5` (feat)
3. **Task 3 : deux gardes — le câblage des huit menus et la matrice 8 × 9** — `103978e` (test)

## Files Created/Modified

- **`tests/Chronos.Tests/GesteTraiteTests.cs` (créé, 174 l.)** — les cinq cas, montés sur une
  `SourceMutable` substituée, des chemins `Path.GetTempPath()` et un `TreatedStore` **PARTAGÉ** entre le
  moniteur et le ViewModel (sans ce partage, le filtre du moniteur ne verrait pas ce que le geste écrit).
  Détecteur branché **uniquement** dans le cas de réversibilité ; les quatre autres l'isolent.
- **`src/Chronos/ViewModels/SessionsViewModel.cs`** — `SessionItemVm` porte `UpdatedAtMs` (l'instant du
  SIGNAL), `ToutTraiterLibelle` et les deux nouvelles commandes ; `SessionsViewModel` reçoit le
  `TreatedStore` en quatrième paramètre et expose `MarquerSessionTraitee` / `MarquerToutTraite`, tous deux
  écrivant dans le magasin **réversible**. `Refresh` pose le libellé avec le nombre de sessions ; l'ordre
  d'`AffichageSessions.Ordonner`, les drapeaux, le compteur d'attente et le résumé ne changent pas.
- **`src/Chronos/Resources/SessionStyles.xaml`** — les huit blocs `<ContextMenu>` portent le même
  triptyque, une entrée par ligne, séparateur compris. Rien d'autre n'a bougé.
- **`src/Chronos/Views/SessionsWindow.xaml.cs`** — XML-doc de tête seule (aucun code) : elle n'annonce
  plus « clic droit sur une pastille = Archiver » mais les trois gestes, dans l'ordre, avec ce que chacun
  promet.
- **`src/Chronos/Views/SessionsController.cs`** — champ `_treated`, paramètre ajouté **en dernier**, et
  `new SessionsViewModel(_monitor, _clock, _archive, _treated)`.
- **`src/Chronos/App.xaml.cs`** — une ligne : `sp.GetRequiredService<TreatedStore>()` en dernier argument
  de `new Views.SessionsController(...)`.
- **`src/Chronos/Services/DiagnosticService.cs`** — **une seule chaîne** : le motif « traité » dit
  désormais « hystérésis automatique OU geste explicite ; réversible ».
- **`src/Chronos/ViewModels/SessionsPreviewViewModel.cs`** — une ligne : les trois gestes sont sans effet
  en galerie, comme l'archivage l'était déjà.
- **`tests/Chronos.Tests/AffichageSessionsTests.cs`** (2 sites) et
  **`tests/Chronos.Tests/SessionStylesBindingTests.cs`** (1 site) — quatrième argument
  `new TreatedStore(<chemin temporaire>, new FakeClock(Maintenant))`. Aucun test ne peut viser le vrai
  magasin de l'utilisateur.
- **`tests/Chronos.Tests/GardesPerimetreTests.cs`** — la garde de câblage (+35 l.) ; **aucun test existant
  modifié**.
- **`tests/Chronos.Tests/SessionStylesBindingTests.cs`** — `MenusContextuels` + la garde de matrice, dans
  la classe existante (`[Collection("XAML WPF")]`, `DisableParallelization`) : **aucune seconde classe de
  test WPF n'a été créée**.

## Deviations from Plan

**Aucune.** Les trois tâches ont été exécutées telles que le plan les écrit ; tous les seuils annoncés sont
mesurés à leur valeur exacte. Deux points méritent d'être **signalés sans être des écarts** :

### 1. La tâche 1, marquée `tdd="true"`, est **un seul commit** — comme le plan l'annonce lui-même

Le bloc `<behavior>` le dit explicitement (« Aucun n'est annoncé rouge […] un test qui ne compile pas n'est
pas un test rouge ») et `26-VALIDATION.md` classe les cinq cas parmi les « verts dès leur écriture ». Un
commit RED aurait été un commit qui ne compile pas — interdit sans exception. **Même situation qu'en
26-01 T1**, et traitée de la même façon.

### 2. Le seuil de durée est franchi une fois sur trois mesures — **dit, pas corrigé**

9 s à la première exécution suivant une reconstruction, 7 s aux deux vérifications. Le plan prescrit
explicitement de le dire plutôt que de retirer des combinaisons. **Aucune des 72 combinaisons n'a été
retirée.**

### Renommages : **0**

Aucun renommage n'était prévu, aucun n'a été fait — ni de test, ni de membre, ni de fichier. Aucun test
supprimé. Aucune dépendance NuGet ajoutée.

### Aucun auth gate, aucun blocage

Trois tâches, aucune interruption, aucune règle 4 déclenchée. Aucune correction de règle 1-3 n'a été
nécessaire : le build est passé du premier coup à chaque tâche.

## Known Stubs

Aucun. Les trois commandes sont câblées à du code réel ; les seules no-op sont celles de la **galerie de
prévisualisation** (`SessionsPreviewViewModel`), volontaires et documentées sur place — c'était déjà le cas
de l'archivage avant ce plan, et la galerie n'a aucune source de sessions.

## Deferred Issues

Aucun nouveau. Le report du plan 26-02 reste ouvert et relève du plan **26-04** :
la XML-doc de `PurgerPrefixe` décrit encore un `Add` « qui applique au passage la purge des entrées
expirées ». Ce plan n'a pas touché `ArchiveStore.cs` (diff **vide** sur `7660ef7..HEAD`).

## Sécurité — vérifié

- Aucun test n'écrit hors de `Path.GetTempPath()` : `GesteTraiteTests.TempDir()` **assert**
  `StartsWith(Path.GetTempPath(), d)`, et `grep -c "GetFolderPath\|APPDATA"` sur ce fichier = **0**.
- `%APPDATA%\Chronos\archived.json` : **84 octets**, mtime **12 juillet 16:38** — **inchangé**.
- `%APPDATA%\Chronos\treated.json` : **2 octets**, mtime **12 septembre 21:21**, antérieur à cette
  session — **inchangé** (ni écrit, ni supprimé).
- `%APPDATA%\Chronos\sessions\` : **jamais écrit**. Listé en lecture seule pour le seul compte d'entrées
  (**65**). Il bouge tout seul — les hooks v3.0.2 de l'utilisateur sont vivants ; rien n'a été « réparé ».
- Le vrai `~/.claude/settings.json` n'a pas été écrit. **Aucune sonde.** `oauth.dat` n'a pas été approché,
  **son mtime n'a pas été vérifié**.
- L'overlay (pid 119412) n'a été **ni lancé ni tué**. **Aucun test n'affiche de fenêtre** (`.Show()` /
  `ShowDialog` = **0**) et **aucun menu n'est ouvert** : un `ContextMenu` est lu comme VALEUR de propriété.
- Aucune requête réseau. Aucune dépendance NuGet.

## Ce que la suite peut tenir pour acquis

- Les **trois** entrées de menu existent sur les **8** styles et les **9** thèmes, tenues par deux gardes
  complémentaires : `Les_huit_menus_offrent_le_geste_explicite_et_disent_la_verite` (TEXTE du XAML, attrape
  une commande débranchée) et `Les_trois_gestes_sont_offerts_sur_les_8_styles_et_les_9_themes` (ARBRE
  VISUEL, attrape un gabarit oublié). La compacité est prouvée par la non-régression de
  `Les_8_styles_et_les_9_themes_se_chargent_se_mesurent_et_se_disposent`.
- `SessionItemVm` a **cinq** paramètres de construction et porte `UpdatedAtMs` ; `SessionsViewModel` a
  **quatre** paramètres, `TreatedStore` en dernier. Les **deux** seuls sites de construction de
  `SessionItemVm` sont `SessionsViewModel.cs` et `SessionsPreviewViewModel.cs`.
- Le geste de masse écrit **exclusivement** dans `TreatedStore` : il est réversible **par construction** et
  ne peut pas devenir destructif.
- TRT-03 est **complet** ; TRT-04 l'est aussi, ses deux volets réunis (le code en 26-02, le libellé ici).
- Reste au plan **26-04** : le contrat **documenté** (`docs/hooks-contract.md` §3, et la phrase obsolète de
  `PurgerPrefixe` signalée en 26-02). Rien de ce milestone ne s'exécute chez l'utilisateur tant que l'exe
  n'est pas republié et `settings.json` réconcilié — hors périmètre de cette phase.

## Self-Check: PASSED

- Fichiers annoncés : tous présents — `26-03-SUMMARY.md`, `GesteTraiteTests.cs`, `SessionsViewModel.cs`,
  `SessionsPreviewViewModel.cs`, `SessionsController.cs`, `SessionsWindow.xaml.cs`, `SessionStyles.xaml`,
  `DiagnosticService.cs`, `App.xaml.cs`, `AffichageSessionsTests.cs`, `SessionStylesBindingTests.cs`,
  `GardesPerimetreTests.cs`.
- Commits annoncés : `927050e`, `923d1d5`, `103978e` — les trois retrouvés dans `git log`.
- Suite complète relancée **deux fois** après le dernier commit : **886 / 0 échec / 7 s** les deux fois.
- Arbre de travail **propre** après les deux mesures hors commit (`git status --short` vide).
