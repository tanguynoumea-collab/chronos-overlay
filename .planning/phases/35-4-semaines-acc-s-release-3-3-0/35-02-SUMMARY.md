---
phase: 35-4-semaines-acc-s-release-3-3-0
plan: 02
subsystem: ui
tags: [wpf, mvvm, gestes, double-clic, p-invoke, singleton, di, reglages, journal, tdd, ACC-01, ACC-02]

# Dependency graph
requires:
  - 34-03 (HistoriqueViewModel : ChoisirStyleCommand, IsStyle*, Fuseau ; TextesHistorique : StylePistes / StyleSimplifie / StyleTuiles, DateLongue)
  - 34-05 (HistoriqueWindow(HistoriqueViewModel), bloc DI Historique d'App.xaml.cs)
  - 32-05 (carte « Journal des relevés » : LigneEtatJournal / PastilleJournal, TexteEtatJournal / AlerteJournal, IEtatJournal)
provides:
  - "ViewModels/ArbitreClicCentre.cs : enum ActionClicCentre + arbitre PUR (Clic, Echeance, Restant, EnAttente) — horloge passée par les appels"
  - "Services/IOuvreurHistorique.cs : contrat neutre Ouvrir()"
  - "Views/Historique/OuvreurHistorique.cs : singleton de fenêtre, recréée après Closed, Minimized → Normal puis Activate"
  - "MainViewModel : ClicCentre(int), EcheanceClicCentre() (internal), DefinirDelaiDoubleClic(TimeSpan), OuvrirHistoriqueCommand, Historique, AfficherCarteHistorique, AfficherOuvrirHistorique, SousTexteHistorique ; ctor(…, IOuvreurHistorique? ouvreurHistorique = null, HistoriqueViewModel? historique = null)"
  - "Interop/NativeMethods.GetDoubleClickTime (user32)"
  - "IEtatJournal.JournalOuvertLe ; JournalReleves.AmorcerJournalOuvertLe() / RetenirJournalOuvertLe (internal) ; LecteurJournal.JournalOuvertLe(dossier)"
  - "SettingsWindow : carte CarteHistorique (F1) dans DONNÉES, style local PuceStyleHistorique"
  - "App.xaml.cs : AddSingleton<IOuvreurHistorique>(sp => new OuvreurHistorique(() => new HistoriqueWindow(sp.GetRequiredService<HistoriqueViewModel>())))"
affects: [35-04, 35-05, 35-06, 35-07]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Temporisation du simple clic par un arbitre pur (instant injecté) + DispatcherTimer one-shot créé au premier clic, réarmé pour le reste si la minuterie part un peu tôt"
    - "Ouvreur singleton Func<Window> : une Window fermée n'est jamais ré-affichée, elle est recréée"
    - "Synchronisation par construction : la carte des réglages binde le VM singleton de la fenêtre exposé par le MainViewModel (aucun état dupliqué)"
    - "Sélection d'une puce par Tag : DataTrigger {Binding Tag, RelativeSource={RelativeSource Self}} dans ControlTemplate.Triggers (TemplatedParent y est muet)"
    - "Valeur amorcée en fond + posée à la première écriture : le plus ANCIEN gagne, sous verrou dédié (jamais le verrou d'écriture, lu par le thread UI)"

key-files:
  created:
    - src/Chronos/ViewModels/ArbitreClicCentre.cs
    - src/Chronos/Services/IOuvreurHistorique.cs
    - src/Chronos/Views/Historique/OuvreurHistorique.cs
    - tests/Chronos.Tests/ArbitreClicCentreTests.cs
    - tests/Chronos.Tests/GardeGestesCadranTests.cs
    - tests/Chronos.Tests/OuvreurHistoriqueTests.cs
    - tests/Chronos.Tests/Fakes/FakeOuvreurHistorique.cs
  modified:
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/Views/MainWindow.xaml.cs
    - src/Chronos/Interop/NativeMethods.cs
    - src/Chronos/Views/SettingsWindow.xaml
    - src/Chronos/Services/Historique/IEtatJournal.cs
    - src/Chronos/Services/Historique/JournalReleves.cs
    - src/Chronos/Services/Historique/JournalisationUsageProvider.cs
    - src/Chronos/Services/Historique/LecteurJournal.cs
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/MainViewModelTests.cs
    - tests/Chronos.Tests/ReglagesBindingTests.cs
    - tests/Chronos.Tests/CompositionRootTests.cs
    - tests/Chronos.Tests/JournalRelevesTests.cs
    - tests/Chronos.Tests/Fakes/FakeEtatJournal.cs

key-decisions:
  - "D-35-06 — temporisation, pas annulation : le premier clic ARME la bascule, elle a lieu à l'échéance GetDoubleClickTime (≈ 0,5 s) ; ClickCount >= 2 désarme et ouvre l'Historique. Une bascule au plus, jamais deux (Pitfall 7)"
  - "D-35-07 — IOuvreurHistorique (contrat neutre sous Services/) + OuvreurHistorique WPF sous Views/Historique/ qui recrée la fenêtre après Closed et fait Activate sinon"
  - "D-35-08 — MainViewModel reçoit le HistoriqueViewModel singleton (paramètre optionnel en dernière position) et l'expose : les puces bindent Historique.ChoisirStyleCommand / Historique.IsStyle*"
  - "D-35-09 — la carte « Journal des relevés » devient la ligne d'état de la carte F1 ; LigneEtatJournal / PastilleJournal / TexteEtatJournal / AlerteJournal conservés ; « — sans interface pour l'instant » supprimé"
  - "Coût du simple clic à annoncer au constat VAL-05 : la bascule % / temps arrive après le délai de double-clic Windows de l'utilisateur (500 ms par défaut), comme le renommage de l'Explorateur"

patterns-established:
  - "Arbitre de geste pur + minuterie de réveil : la décision est une fonction de l'instant, la minuterie n'est qu'un réveil"
  - "Garde textuelle des gestes (GardeGestesCadranTests) : MouseButtonEventArgs.ClickCount n'est pas réglable en test"

requirements-completed: [ACC-01, ACC-02]

# Metrics
duration: 22min
completed: 2026-09-27
---

# Phase 35 Plan 02 : gestes d'ouverture de l'Historique et carte F1 — Summary

**Double-clic au centre du cadran arbitré par une fonction pure du temps (délai `user32!GetDoubleClickTime`), ouvreur singleton qui recrée la fenêtre après fermeture, et carte « Historique d'utilisation » dans DONNÉES. Cette carte absorbe la ligne d'état du journal et pilote le style de la vue Semaine par le même `HistoriqueViewModel` que la fenêtre. ACC-01 et ACC-02 sont clos.**

## Performance

- **Duration :** ≈ 22 min (18:43 → 19:05)
- **Started :** 2026-09-27T18:43
- **Completed :** 2026-09-27T19:05
- **Tasks :** 3 (TDD : 3 RED + 3 GREEN)
- **Files modified :** 21 (7 créés, 14 modifiés)

## Accomplishments

- **ACC-02, gestes.** Le code-behind se contente de transmettre `e.ClickCount` (`ClicCentre(e.ClickCount); e.Handled = true;`). `ArbitreClicCentre` décide : simple clic = bascule à l'échéance, double-clic = Historique sans aucune bascule. Le drag par les anneaux et le clic droit ne changent pas, et une garde textuelle le vérifie.
- **ACC-02, fenêtre singleton.** `OuvreurHistorique` donne une seule fenêtre pour deux ouvertures. Il la recrée après une fermeture et la remet en Normal si elle était minimisée. Il est enregistré dans `App.xaml.cs`, qui conserve ses fins de ligne CRLF.
- **ACC-01, carte F1.** La carte se place juste après la sonde, avec une bordure Accent de 1,5. Elle contient :
  - le bouton « Ouvrir » ;
  - le sous-texte « hebdo / 5 h / tokens · journal du <date> » ;
  - une seule ligne « dernière écriture » et la pastille Alerte au-delà de 15 min ;
  - le sélecteur « Style de la vue Semaine : Pistes · Simplifié · Tuiles », branché sur la même instance que la fenêtre ;
  - la mention « Aussi : double-clic au centre du cadran ».
- **« journal du <date> ».** `IEtatJournal.JournalOuvertLe` est amorcé par `Task.Run` depuis `JournalisationUsageProvider.StartAsync`, donc jamais sur le thread UI. Il est posé à la première écriture, et c'est la date la plus ancienne qui l'emporte. Quand la date est inconnue, le segment n'apparaît pas.

## Coût du simple clic, à annoncer au constat (VAL-05)

Le simple clic au centre fait maintenant sa bascule % / temps **après le délai de double-clic Windows de l'utilisateur** : 500 ms par défaut, lu par `GetDoubleClickTime`, avec un repli à 500 ms si la valeur est illisible. C'est le prix d'une interface sans double bascule, le même compromis que le renommage dans l'Explorateur. Cas limite (Pitfall 7) : si l'échéance est traitée avant l'arrivée du second clic, on obtient une bascule puis l'ouverture. Il y a au plus une bascule, jamais deux. Autre cas limite accepté : deux simples clics rapides hors du rectangle de double-clic ne donnent qu'une seule bascule, l'attente étant réarmée à la date du second.

## Task Commits

1. **Task 1 : arbitre du clic au centre, délai système, `ClicCentre`**
   - RED `bb85fdc` : 12 tests, échec de compilation
   - GREEN `6848016`
2. **Task 2 : ouvreur singleton et composition**
   - RED `9335d45` : 3 tests, échec de compilation
   - GREEN `38c0a6b`
3. **Task 3 : carte F1, `JournalOuvertLe`, synchronisation du style**
   - RED `dfd4499` : 8 tests, échec de compilation
   - GREEN `1457984`

**Ordre des Tasks 2 et 3 :** l'assertion `Assert.Same(MainViewModel.Historique, HistoriqueViewModel)` de `CompositionRootTests` est écrite en Task 3, là où la propriété `Historique` apparaît. Chaque commit GREEN compile donc seul. La Task 2 ne pose que le miroir de l'ouvreur et la garde textuelle d'`App.xaml.cs`.

## Mutations (jouées dans l'instantané `snap-35-02`, révoquées par copie, sha256 identiques avant et après)

| Mut. | Altération | Rouge nommé |
|------|-----------|-------------|
| g1 | `Clic` rend `Basculer` au premier clic | `Un_simple_clic_bascule_a_l_echeance_et_pas_avant`, `Un_double_clic_ouvre_sans_aucune_bascule` (et 2 autres de l'arbitre) |
| g2 | `n >= 2` sans `_enAttenteDepuis = null` | `Un_double_clic_ouvre_sans_aucune_bascule`, `Un_triple_clic_ouvre_une_fois_et_ne_bascule_pas` |
| g3 | le handler rappelle `ToggleCenterMode()` | `GardeGestesCadranTests.Le_centre_transmet_le_compte_de_clics_et_reste_Handled` |
| o1 | `Ouvrir()` crée toujours une fenêtre | `Deux_ouvertures_une_seule_fenetre`, `Une_fenetre_minimisee_revient_en_normal` |
| o2 | `Closed` sans oubli de la référence | `Apres_fermeture_une_nouvelle_fenetre_est_creee` |
| c1 | puce « Tuiles » liée à `SelectCadranStyleCommand` | `La_carte_Historique_pilote_le_meme_style_que_la_fenetre` (+ 7 autres de `ReglagesBindingTests` : `RelayCommand<CadranStyleChoice?>.CanExecute` lève sur un paramètre enum, ce qui fait échouer le montage de la fenêtre) |
| c2 | `RetenirJournalOuvertLe` : `= lu` sans Min | `Une_amorce_qui_revient_apres_l_ecriture_du_demarrage_garde_le_plus_ancien` |
| c3 | `BorderBrush="#8B7BF0"` en dur | `GardeTokensHistoriqueTests.SettingsWindow_garde_ses_douze_litteraux_et_pas_un_de_plus` |

sha256 après révocation, identiques à l'arbre réel :
- `ArbitreClicCentre.cs` 47c14391…a415f
- `MainWindow.xaml.cs` 4e3943f1…203fe
- `OuvreurHistorique.cs` c6d1a223…f7648
- `SettingsWindow.xaml` 7d2bfa64…53279
- `JournalReleves.cs` a1b1391a…47b1f

## Totaux

- **Tests du plan :** 22 nouveaux, répartis ainsi :
  - `ArbitreClicCentreTests` : 5
  - `GardeGestesCadranTests` : 3
  - `MainViewModelTests` : +4
  - `OuvreurHistoriqueTests` : 3
  - `ReglagesBindingTests` : +4
  - `JournalRelevesTests` : +3

  `CompositionRootTests` reçoit des assertions supplémentaires dans deux tests existants, sans test nouveau.
- **Suite complète :**
  - instantané de HEAD (`1457984`) : 1615 / 1615 verts, deux fois de suite ;
  - arbre réel (qui porte aussi le travail en cours de 35-01) : 1622 / 1622 verts, deux fois de suite.
- **Release :** 0 avertissement, 0 erreur (arbre réel et instantané HEAD).

## Tests existants ajustés

- `MainViewModelTests.Build` : paramètre `FakeOuvreurHistorique? ouvreur` ajouté et transmis par nom (`ouvreurHistorique:`). Aucun site de construction nouveau.
- `ReglagesBindingTests.MonterReglages` : paramètres `HistoriqueViewModel? historique` et `IOuvreurHistorique? ouvreur` ajoutés et transmis par nom. `La_ligne_d_etat_du_journal_est_liee_au_ViewModel` et `La_pastille_du_journal_ne_se_voit_qu_en_alerte` restent verts **sans modification d'assertion**.
- `CompositionRootTests.Le_conteneur_resout_la_fenetre_historique` : le miroir passe de quatre à cinq lignes, avec l'ouvreur et la garde textuelle. `Host_resout_et_dispose_les_singletons` reçoit les cinq lignes Historique et `Assert.Same`.
- Aucun test n'épinglait l'ancienne infobulle de la pastille ni le titre « Journal des relevés ». L'infobulle devient « alerte si > 15 min sans écriture alors que Chronos tourne » (DESIGN_PLAN §6).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Une minuterie un peu en avance aurait perdu un simple clic**
- **Found during :** Task 1
- **Issue :** `DispatcherTimer` suit la granularité du système. S'il se déclenche quelques millisecondes avant `t0 + délai` selon l'horloge injectée, `Echeance` rend `Rien`, et sans réarmement la bascule n'aurait jamais lieu.
- **Fix :** ajout de `ArbitreClicCentre.Restant(t)` (pur, testé dans `Un_simple_clic_bascule_a_l_echeance_et_pas_avant`). Au Tick, si une bascule est toujours en attente, la minuterie est réarmée pour le reste + 1 ms.
- **Files :** `ArbitreClicCentre.cs`, `MainViewModel.cs` — **Commit :** `6848016`

**2. [Rule 1 - Bug] `RelativeSource TemplatedParent` est sans effet dans `ControlTemplate.Triggers`**
- **Found during :** Task 3, GREEN
- **Issue :** le plan prescrivait `DataTrigger Binding="{Binding Tag, RelativeSource={RelativeSource TemplatedParent}}"`. Dans les triggers d'un gabarit, la liaison s'évalue depuis le bouton lui-même, dont le TemplatedParent est nul. La puce sélectionnée ne s'allumait donc jamais : `Tag` était juste, mais la bordure restait `Line`.
- **Fix :** `RelativeSource={RelativeSource Self}`. Le test vérifie la bordure `puce` résolue, pas seulement le `Tag`.
- **Files :** `SettingsWindow.xaml` — **Commit :** `1457984`

**3. [Rule 2 - Testabilité / course] `RetenirJournalOuvertLe` interne**
- **Issue :** sans interleaving déterministe, impossible de rendre la course « amorce lue avant l'écriture de `demarrage` » (mutation c2).
- **Fix :** l'amorce se découpe en deux étapes : lecture tolérante, puis `RetenirJournalOuvertLe(lu)` (interne, le plus ancien gagne). Un verrou `_verrouOuverture` est dédié à cette valeur, distinct du verrou d'écriture, pour que le thread UI n'attende jamais les reprises de fichier. Les écritures utilisent `??=`, comme prévu.
- **Commit :** `1457984`

**4. [Rule 3 - Blocage] RED des voisins et bac à sable partagé**
- **Issue :** l'arbre réel ne compilait plus pendant les RED de 35-01 (`TextesHistorique.*`, `LectureVeille`). Par ailleurs, le dossier scratchpad est **partagé** entre les trois agents : mon `sync.sh` a été écrasé par celui de 35-01.
- **Fix :** filtres et mutations exécutés dans l'instantané `snap-35-02` (`git archive b3b08f7` + copie de mes fichiers). Mes fichiers de travail ont été déplacés dans `scratchpad/p3502/`. La suite complète finale a été jouée deux fois sur l'arbre réel, une fois celui-ci revenu à l'état compilable.
- **Incident corrigé :** une commande a extrait par erreur `git archive b3b08f7` dans la racine du dossier temporaire (`/tmp` = `%LOCALAPPDATA%\Temp`). Les douze entrées extraites, datées de l'archive (18:43), ont été supprimées aussitôt. Rien n'a été écrit sous `~/.claude`, `%APPDATA%\Claude` ni `%APPDATA%\Chronos`.

**5. [Plan] `MainViewModelTests` +4 au lieu de +3** : le comportement listait quatre cas (simple clic, double-clic, sans ouvreur, commande Ouvrir), chacun a son test.

**Total deviations :** 2 bugs corrigés (dont une prescription XAML du plan qui ne pouvait pas fonctionner), 1 ajustement de testabilité, 1 contournement d'environnement parallèle. Aucun changement d'architecture.

## Known Stubs

Aucun. Le sous-texte vaut « hebdo / 5 h / tokens » tant que l'ouverture du journal n'est pas connue : c'est l'omission voulue (« inconnu, jamais inventé »), pas une valeur de remplacement.

## Issues Encountered

- `RelayCommand<T>.CanExecute` (CommunityToolkit) lève sur un paramètre du mauvais type. La mutation c1 fait donc tomber tout le montage de `SettingsWindow`, et pas seulement l'assertion ciblée. Cela reste utile : une puce branchée sur la mauvaise commande casse la fenêtre de réglages dès l'ouverture.
- Dans ce harnais, `\\n` passé en ligne de commande Bash est converti en saut de ligne réel. Les scripts d'édition longs passent désormais par un fichier (voir la mémoire « outillage de session »).

## Next Phase Readiness

- 35-04 (vue 4 semaines) et 35-05 (docs) peuvent citer les gestes : « double-clic au centre du cadran » et « Réglages → Historique d'utilisation → Ouvrir ».
- 35-05 : dans la ligne « Au centre » du README, écrire que le simple clic bascule après le délai de double-clic et que le double-clic ouvre l'Historique.
- VAL-05 : annoncer le coût du simple clic (≈ 0,5 s) et vérifier en réel le double-clic, la réouverture après fermeture et le rappel d'une fenêtre minimisée.

## Self-Check: PASSED

- 7 fichiers créés présents ; 6 commits du plan présents (bb85fdc, 6848016, 9335d45, 38c0a6b, dfd4499, 1457984).
- ACC-01 et ACC-02 cochés dans REQUIREMENTS.md (liste et traçabilité) par blob construit depuis HEAD.
