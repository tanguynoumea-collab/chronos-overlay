---
quick: 260927-reglages-v2
type: refonte d'interface
subsystem: fenêtre de réglages (Views/Reglages, ReglagesViewModel, ouvreur)
tags: [reglages, wpf, windowchrome, tokens, iso-fonctionnalite, diagnostic, release, 3.4.0]
key-files:
  created:
    - src/Chronos/Views/Reglages/ReglagesWindow.xaml
    - src/Chronos/Views/Reglages/ReglagesWindow.xaml.cs
    - src/Chronos/Views/Reglages/OuvreurReglages.cs
    - src/Chronos/Views/OuvreurFenetreUnique.cs
    - src/Chronos/Views/PressePapiersWpf.cs
    - src/Chronos/ViewModels/ReglagesViewModel.cs
    - src/Chronos/Services/IOuvreurReglages.cs
    - src/Chronos/Services/IPressePapiers.cs
    - tests/Chronos.Tests/ReglagesWindowTests.cs
    - tests/Chronos.Tests/ReglagesViewModelTests.cs
    - tests/Chronos.Tests/OuvreurReglagesTests.cs
    - tests/Chronos.Tests/GardeTokensReglagesTests.cs
    - tests/Chronos.Tests/MontageReglages.cs
  modified:
    - src/Chronos/Resources/DesignTokens.xaml
    - src/Chronos/Services/ChronosSettings.cs
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/ViewModels/SessionsPreviewViewModel.cs
    - src/Chronos/Views/MainWindow.xaml.cs
    - src/Chronos/Views/Historique/OuvreurHistorique.cs
    - src/Chronos/Interop/NativeMethods.cs
    - src/Chronos/App.xaml.cs
    - src/Chronos/Chronos.csproj
    - tests/Chronos.Tests/ReglagesBindingTests.cs
    - tests/Chronos.Tests/GardeTokensHistoriqueTests.cs
    - tests/Chronos.Tests/ThemingTests.cs
    - README.md
    - docs/publish.md
  deleted:
    - src/Chronos/Views/SettingsWindow.xaml
    - src/Chronos/Views/SettingsWindow.xaml.cs
commits:
  - 9e2c3fc test RED (navigation, ouvreur, tokens)
  - b003c65 feat GREEN (navigation, ouvreur, tokens)
  - e6eb7f2 test RED (fenêtre, mise en page, iso-fonctionnalité, diagnostic)
  - 64135c5 feat GREEN (fenêtre, six sections, diagnostic ; ancienne fenêtre supprimée)
  - fa05719 docs (README, publish.md)
  - 8bec859 release 3.4.0
completed: 2026-09-27
duration: environ 45 min (21:23 → 22:08)
---

# Quick 260927 : réglages v2, fenêtre classique redimensionnable (release 3.4.0)

**En bref :** le clic droit sur le cadran ouvre maintenant une vraie fenêtre (`Views/Reglages/ReglagesWindow`). Elle a le même
chrome que la fenêtre Historique, se redimensionne par les bords et la poignée, s'agrandit, apparaît dans la barre des tâches,
mémorise sa taille, sa position et la dernière section, et ne se ferme plus quand on clique ailleurs. Un rail fixe de six
sections occupe la gauche ; la colonne de droite s'adapte puis défile. Le diagnostic s'affiche dans la fenêtre. Chaque commande
de l'ancienne fenêtre est liée dans la nouvelle, ce que vérifie un test d'inventaire. `SettingsWindow` est supprimée. La 3.4.0 est
publiée (`Chronos-v3.4.0.exe`, md5 `ddbe79959e26f970f5dd29a604ae0941`) dans le commit de release **`8bec859`**.

## Livré, section par section

| Zone | Ce qui est livré |
|---|---|
| **Chrome** | `WindowStyle=None`, `AllowsTransparency=False`, `WindowChrome` (bords de 6 px, barre de titre native de 40 px : glisser, double-clic pour agrandir ou restaurer, ancrage Windows), coins DWM best-effort (helper de l'Historique), `ResizeMode=CanResizeWithGrip` avec une poignée ◢ sombre, `Topmost=False`, `ShowInTaskbar=True`, **aucun `Owner`**. Boutons ─ □ ✕ fonctionnels, avec le glyphe ❐ quand la fenêtre est agrandie. Échap ferme. Taille par défaut **860 × 580**, minimum **640 × 440** (tokens). L'agrandissement est borné à la zone de travail du moniteur (`WM_GETMINMAXINFO`), la barre des tâches reste visible. |
| **Persistance** | `ReglagesX/Y/Width/Height` et `ReglagesSection` dans `ChronosSettings` (enum sérialisé en texte ; un ancien `settings.json` donne une fenêtre centrée sur la section Données). La géométrie est relue à l'ouverture et bornée à l'écran virtuel et aux minima par `PlacementHistorique.Borner`. Elle n'est écrite qu'en état Normal. |
| **Ouvreur** | `IOuvreurReglages` / `OuvreurReglages`, un singleton : le premier clic droit ouvre la fenêtre, les suivants la ramènent au premier plan, et une fenêtre fermée est recréée. Le mécanisme est extrait dans `OuvreurFenetreUnique`, que partage désormais `OuvreurHistorique` sans changer son comportement. `MainWindow.OnRightClick` se limite à `_ouvreurReglages?.Ouvrir()`. |
| **Rail** | Largeur fixe de 208 px. C'est une `ListBox` : clic, ↑ / ↓ et focus clavier visible (bordure Accent). Ctrl+1…6 fonctionne avec la rangée de chiffres et avec le pavé numérique. L'entrée sélectionnée prend le fond `Panel2`, un trait Accent à gauche, un glyphe Accent et un libellé en demi-gras. En bas, après un filet, une aide « Ctrl+1…6 · ↑ / ↓ · Échap ferme » et **« ⏻ Quitter Chronos »** en brosse `Danger`. La section courante est un état du VM (`ReglagesViewModel.Section`), testé sans WPF. |
| **Données** | Connexion Claude : « Se connecter », « ● Connecté » ou « Connexion… » pendant le login. Sous-texte d'invitation quand l'utilisateur n'est pas connecté. Sonde d'en-têtes : interrupteur, coût écrit (≈ 288/jour), ligne d'état. **« Barre de statut de Claude Code »** (ex-« Source terminal ») : un interrupteur lié à `IsStatusLineSourceEnabled` / `ToggleStatusLineSourceCommand`, avec l'aide du plan mot pour mot. |
| **Historique** | La carte F1 de 35-02 est reprise telle quelle : Ouvrir, sous-texte, dernière écriture et pastille, puces du style de la vue Semaine, mention du double-clic. Mêmes `x:Name`, bordure Accent 1,5. |
| **Apparence** | **Aperçu vivant** : le rendu réel du cadran, peint par un `VisualBrush` sur son contenu et cadré sur son empreinte de 170 × 170. Il change dès qu'on clique un thème ou un style. Légende « Thème : … / Style : … ». Ensuite THÈME (9 vignettes en `WrapPanel` : 3 par ligne à 640, 6 à 860, jamais coupées), STYLE DU CADRAN (5 puces), « Mode étendu » visible pour Anneaux seulement. |
| **Sessions** | Interrupteur « Widget sessions Claude Code » en tête. Quand il est activé : aperçu vivant du widget (gabarits de `SessionStyles.xaml`, données de `SessionsPreviewViewModel`, pinceaux du thème actif), STYLE DU WIDGET (8 puces), « Disposition verticale » pour les styles en rangée. Quand il est désactivé : les styles sont grisés (opacité 0,45, désactivés) et une note dit « Active le widget pour choisir son style. ». |
| **Comportement** | Arrière-plan, Lancer au démarrage, « Recalibrer le reset hebdomadaire… » (bouton « Recalibrer », commande existante). |
| **Diagnostic** | Rapport en police mono (Cascadia Mono / Consolas), en lecture seule et sélectionnable. Il défile dans les deux sens et s'étire avec la fenêtre. Boutons « ↻ Actualiser » et « ⧉ Copier » (presse-papiers ; un message s'affiche si le presse-papiers est tenu par une autre application). Ligne « Généré à HH:MM · N lignes ». La génération est asynchrone et tourne sur le pool (`Task.Run` : `BuildReportAsync` commence par des lectures disque synchrones) ; pendant ce temps, la fenêtre affiche « Génération du rapport… ». En cas d'échec : « Le diagnostic a échoué : <cause> » et un bouton « Réessayer ». Le rapport est lancé automatiquement à la première entrée dans la section. Il n'y a plus de `MessageBox`. |
| **Tokens** | La brosse `Danger` (#E8907F) remplace l'ancien littéral. **30 tailles** `Reglages*` (`sys:Double`) couvrent la fenêtre, le rail, la colonne de lecture, la typographie 18 / 13 / 12 / 11 / 10,5 / 9,5, les gabarits de l'interrupteur, de la vignette, des aperçus, du logo, du trait et de la pastille, la barre de défilement et l'opacité inactive. Les épaisseurs et les rayons sont des ressources locales nommées (motif 34-05). La nouvelle fenêtre ne contient **aucun** littéral de couleur ni de taille. Les barres de défilement sont fines et sombres (la barre système claire est remplacée). |

## Écarts au plan et aux maquettes

1. **L'aperçu du cadran utilise un `VisualBrush` sur le vrai cadran, et non `CadranPreviewViewModel`.** Le style Anneaux n'a pas de
   vue réutilisable : il est écrit dans `MainWindow.xaml`, et la galerie `--cadrans` ne le montre pas. Il aurait donc fallu dupliquer
   ce XAML ou refondre le cadran. La maquette R1 dit par ailleurs « C'est ton cadran réel, avec tes chiffres du moment ». Le
   pinceau peint le contenu de `MainWindow` (fourni par la fabrique de l'App). C'est le vrai rendu, sans aucune duplication, et il
   suit à l'instant le thème, le style et les chiffres. Le cadrage se fait en `Viewbox` absolu sur l'empreinte de 170 × 170 : la
   boîte englobante par défaut décentrait le cadran et aurait bougé à chaque apparition de pastille (constaté sur un rendu PNG,
   corrigé, et verrouillé par un test). L'aperçu des **sessions** suit bien le plan (`SessionsPreviewViewModel` et les gabarits
   partagés). `RowOrientation` de ce VM est devenu réglable pour que l'aperçu suive « Disposition verticale ».
2. **Grille de 4 px.** Interrupteur 40 × 23 → 40 × 24, bouton 17 → 16 ; vignette 88 × 74 → 88 × 76 ; entrée du rail 34 → 36 (pas
   de 40 avec l'écart). Le bouton de l'interrupteur est `Ink2` quand il est coupé et `Ink` quand il est activé (la maquette le
   met toujours en `Ink`) : on garde ainsi le contraste coupé / activé de l'ancienne fenêtre.
3. **Fond du rail en `Panel`** et non en `#12101D` comme dans la maquette : cette teinte n'est pas un token, et le plan interdit
   toute nouvelle couleur. Le rail est séparé du contenu par un filet `Line`.
4. **Vignette de thème** : disque, trois pastilles dessous, nom centré (la maquette place le nom en bas à gauche, où il se serait
   collé au bord à 88 px).
5. **6 vignettes par ligne à 860 px.** La maquette R1 les aligne toutes les neuf, ce qui déborde de son cadre ; le plan annonce
   « 4 à 7 par ligne selon largeur ». À 860, la colonne mesure 594 px, soit 6 × 96. L'attente « 5 » écrite dans le test RED était
   un calcul faux, corrigé avant le GREEN et commenté dans le test.
6. **Le diagnostic occupe toute la largeur** de la colonne, sans le plafond de lecture de 640 px : les lignes du rapport sont
   longues.
7. **Barre de titre native** (`CaptionHeight` = 40) au lieu du `DragMove` de l'Historique. On obtient ainsi le double-clic pour
   agrandir, l'ancrage Windows et le menu système sans code, les boutons restant cliquables (`IsHitTestVisibleInChrome`). Le
   double-clic lui-même est natif et n'est pas testé unitairement. Le test vérifie la hauteur de légende et la cliquabilité des
   boutons.
8. **Textes d'aide non fixés par le plan**, rédigés en tenant compte de la garde de vocabulaire : Historique (« Ce que le journal
   de Chronos a relevé… »), Comportement, Arrière-plan, Lancer au démarrage, Recalibrer (« cale la date du reset hebdomadaire
   quand aucune source exacte ne la donne », sans le mot « estimation »). Le bouton de cette carte s'appelle « Recalibrer ».
9. **`AppVersion` passe à trois composantes** (« v3.4.0 ») dans la barre de titre, comme sur les maquettes.
10. **Commit intermédiaire fonctionnel** : `b003c65` inscrivait l'ouvreur sur l'ancienne fenêtre, pour que le clic droit continue
    de marcher entre deux commits. `64135c5` le fait pointer sur la nouvelle.
11. **Tests existants portés, sans perte d'assertion de fond.** Le test « ColumnSpan du bouton Diagnostic » (grille de trois
    boutons disparue) est remplacé par l'inventaire et par les tests de la barre de statut et de « Recalibrer ». « La carte vient
    juste après la sonde » devient « première carte de la section Historique, absente de Données ». « SettingsWindow garde ses
    douze littéraux » devient la garde « aucune couleur ni taille en dur » (zéro littéral).
12. Un `<StaticResource x:Key="{x:Static SystemColors.ControlBrushKey}"/>` fait échouer le chargement BAML différé
    (NullReferenceException) : le coin des barres de défilement est donc posé dans le code-behind.

## Iso-fonctionnalité vérifiée

Test d'inventaire `ReglagesWindowTests.Chaque_commande_de_l_ancienne_fenetre_est_liee_dans_la_nouvelle` : pour chaque section,
il collecte les `ICommandSource` **affichés** et exige la même instance de commande. D'autres tests ciblés s'y ajoutent.

| Fonction | Ancien emplacement | Nouvel emplacement | Commande liée | Test |
|---|---|---|---|---|
| Connexion Claude | Données | Données | `LoginClaudeCommand` | inventaire |
| Sonde d'en-têtes (interrupteur, coût, état) | Données | Données | `ToggleSondeEnTetesCommand` | inventaire, `ReglagesBindingTests` (4 tests HDR-06 / HDR-03) |
| Source terminal | Réglages (bouton) | Données, « Barre de statut de Claude Code » (interrupteur) | `ToggleStatusLineSourceCommand` | inventaire, `La_barre_de_statut_est_un_interrupteur_lie_a_sa_commande_et_a_son_etat` |
| Historique : Ouvrir | Données (carte F1) | Historique | `OuvrirHistoriqueCommand` | inventaire, `Le_bouton_Ouvrir_ouvre_la_fenetre` |
| Historique : style Semaine | Données (carte F1) | Historique | `Historique.ChoisirStyleCommand` | inventaire, `La_carte_Historique_pilote_le_meme_style_que_la_fenetre` |
| Historique : dernière écriture, alerte, sous-texte | Données (carte F1) | Historique | liaisons VM | `La_ligne_d_etat_du_journal…`, `La_pastille_du_journal…`, `Le_sous_texte…` |
| Thème (9) | Thème | Apparence | `SelectThemeCommand` | inventaire, `Les_vignettes_de_theme_passent_a_la_ligne_entieres` |
| Style du cadran (5) | Cadran | Apparence | `SelectCadranStyleCommand` | inventaire, `L_apercu_montre_le_vrai_cadran…` |
| Mode étendu (Anneaux) | Affichage | Apparence | `ToggleCadranModeCommand` | inventaire, `L_apercu_montre_le_vrai_cadran…` (masqué hors Anneaux) |
| Widget sessions | Affichage | Sessions (en tête) | `ToggleSessionsWidgetCommand` | inventaire, `Sessions_widget_desactive…` |
| Style du widget (8) | Sessions | Sessions | `SelectSessionStyleCommand` | inventaire, `Sessions_widget_desactive…` (gabarit de l'aperçu) |
| Disposition verticale | Sessions | Sessions | `ToggleVerticalLayoutCommand` | inventaire |
| Arrière-plan | Affichage | Comportement | `ToggleBackgroundCommand` | inventaire |
| Lancer au démarrage | Affichage | Comportement | `ToggleAutostartCommand` | inventaire |
| Recalibrer hebdo… | Réglages | Comportement, « Recalibrer le reset hebdomadaire… » | `RecalibrateCommand` | inventaire, `Recalibrer_est_dans_Comportement` |
| Diagnostic… | Réglages (MessageBox) | Diagnostic (panneau) | `Reglages.ActualiserDiagnosticCommand`, `Reglages.CopierDiagnosticCommand` | inventaire, `Le_diagnostic_s_affiche…`, `Generation_et_echec…`, garde sans MessageBox |
| Quitter Chronos | bas de liste | bas du rail, isolé, `Danger` | `QuitCommand` | inventaire (sur chaque section), `Quitter_est_isole_en_bas_du_rail_en_brosse_Danger` |
| Version | en-tête | barre de titre | `AppVersion` | rendu |
| Fermer | ✕ | ✕ et Échap | code-behind | `La_desactivation_ne_ferme_plus_la_fenetre_et_Echap_la_ferme`, `Les_boutons_de_la_barre_de_titre…` |

Le test `Les_libelles_du_plan_sont_la_et_Source_terminal_est_renomme` vérifie aussi que les libellés du plan sont présents mot
pour mot, que « Source terminal » a disparu et que l'étiquette « RÉGLAGES » n'existe plus.

## Tests

- **RED nommé d'abord pour chaque bloc.** `9e2c3fc` : 15 rouges (navigation, géométrie, diagnostic du VM, ouvreur, clic droit,
  tokens). `e6eb7f2` : 48 rouges (chrome, désactivation, Échap, boutons, géométrie, 18 cas de mise en page, vignettes, rail,
  Quitter, inventaire, libellés, barre de statut, Recalibrer, aperçus, diagnostic, gardes, tests portés).
- **Mise en page mesurée** : pour 3 tailles (640 × 440, 860 × 580, 1 400 × 900) et les 6 sections, aucun texte, bouton ou carte
  affiché n'est rogné (`LayoutInformation.GetLayoutClip` nul), aucun enfant de `StackPanel` / `WrapPanel` ne chevauche son
  voisin, et le rail garde ses six entrées et « Quitter » entiers dans sa hauteur. Ce test a trouvé un vrai défaut pendant le
  GREEN : à 640 px, la phrase de repli de l'aperçu élargissait la colonne et rognait « APERÇU EN DIRECT ». La zone d'aperçu est
  maintenant fixée à 144 × 144.
- **Contrôle visuel** : un test jetable, jamais commité, a rendu la fenêtre en PNG hors écran (`RenderTargetBitmap`, dans le
  scratchpad) pour toutes les sections à 640, 860 et 1 400 px, avec le vrai cadran et le widget désactivé. Les rendus ont été
  inspectés et sont conformes à R1 à R4. C'est cette inspection qui a révélé le décentrage de l'aperçu (écart 1).
- **Suite** : **1707 réussis, 0 échec** (1654 + 55 nouveaux − 2 retirés avec l'ancienne fenêtre). Deux exécutions avant le bump,
  deux après. `dotnet build -c Debug` et `-c Release` : **0 avertissement, 0 erreur**, avant et après le bump.

## Mutations (jouées par copie, révoquées, `git status` vide ensuite)

| Mutation | Test(s) rouge(s) |
|---|---|
| M1 : `Deactivated += (_, _) => Close();` remis dans le constructeur | `La_desactivation_ne_ferme_plus_la_fenetre_et_Echap_la_ferme` |
| M2 : `Command="{Binding RecalibrateCommand}"` supprimé | `Chaque_commande_de_l_ancienne_fenetre_est_liee_dans_la_nouvelle`, `Recalibrer_est_dans_Comportement` |
| M3 : `{ Owner = MainWindow }` ajouté dans la fabrique de l'App | `L_ancienne_fenetre_a_disparu_et_personne_ne_pose_d_Owner` |
| M4 : la vue refabrique la fenêtre, `new ReglagesWindow(_vm) { Owner = this }.Show()` | `L_ancienne_fenetre_a_disparu…`, `Le_clic_droit_du_cadran_passe_par_l_ouvreur` |
| M5 : la section n'est plus persistée | `Changer_de_section_persiste_et_notifie` et 9 tests de fenêtre qui montent sur une section |
| M6 : la zone d'aperçu n'est plus bornée à 144 × 144 | `Rien_n_est_coupe_ni_ne_chevauche(640, 440, Apparence)` |

## Release 3.4.0

| Contrôle | Résultat |
|---|---|
| Bump | `Version` 3.4.0, `FileVersion` 3.4.0.0, `AssemblyVersion` 3.4.0.0, `InformationalVersion` 3.4.0 (`VersionPublieeTests` au vert) |
| `dotnet publish src/Chronos/Chronos.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true` | réussi (20:07:24Z → 20:07:56Z) |
| Sortie `publish/` | `Chronos.exe` **78 142 335 o** et `Chronos.pdb` 321 736 o, **0 DLL** (3.3.1 : 78 111 106 o) |
| VersionInfo (sortie et copie) | **3.4.0.0 / 3.4.0 / Chronos** |
| md5 | **`ddbe79959e26f970f5dd29a604ae0941`**, identique pour la sortie et `Chronos-v3.4.0.exe`, différent de la 3.3.1 (`c9d4c0bb71ef9b4b617f865270995712`) |
| Copie | `Chronos-v3.4.0.exe` à la racine (absente avant), ignorée par `.gitignore:22:/Chronos-v*.exe`. Anciens exe 3.1.0 à 3.3.1 intacts |
| Smoke `./Chronos-v3.4.0.exe --hook SessionStart < /dev/null` | **code 0** (20:08:21Z → 20:08:24Z). `~/.claude/settings.json` : md5 `5ba5bc6f9959b4b8d3ce994be9ee9918`, 3 384 o, date inchangés avant et après. Listing de `%APPDATA%\Chronos` identique. **0 processus Chronos résident** ensuite |
| Commit de release | **`8bec859`**, csproj seul (4 lignes ajoutées, 4 supprimées), ni tag ni push |

Remarque : au moment de la release, `tasklist` ne montrait **aucun** processus Chronos, ni avant ni après le smoke. La 3.3.1 était
donc déjà fermée. L'agent n'a arrêté aucun processus.

Documentation : le README décrit la nouvelle fenêtre et ses six sections. La liste retire « Calibrer les plafonds… » et « Usage
exact (OAuth) », qui n'existaient plus. Les chemins Sessions, Historique et Diagnostic sont mis à jour. `docs/publish.md` reçoit
« Lancer au démarrage » sous Comportement et une section « Premier lancement de la 3.4.0 ». La mention « plus de 1 600 tests »
du README reste exacte, et aucune garde ne fixe ce compte.

## Stubs

Aucun. La phrase « le cadran s'affiche ici quand Chronos tourne » ne s'affiche que si la fenêtre est construite sans cadran
(tests). En production, la fabrique fournit toujours le contenu de `MainWindow`.

## Marche à suivre pour l'utilisateur

1. **Quitter la version en cours** : clic droit sur le cadran, puis « Quitter Chronos ». Au moment de la release, aucun
   processus Chronos ne tournait. Si une 3.3.1 est relancée entre-temps, son arrêt est borné et elle disparaît en moins d'une
   seconde.
2. **Lancer la 3.4.0 depuis l'Explorateur** : double-clic sur
   `C:\Users\Tanguy\Documents\PROGRAMMES\DEV\PROJET OVERLAY\Chronos-v3.4.0.exe`. Si « Lancer au démarrage » était actif, le
   réactiver depuis la 3.4.0 (réglages, Comportement) : le raccourci vise l'exe qui l'a créé.
3. **Clic droit sur le cadran**, puis vérifier ces points :
   - la fenêtre s'ouvre au centre de l'écran en 860 × 580, sur **Données**, et apparaît dans la barre des tâches ;
   - cliquer ailleurs **ne la ferme pas** ; un second clic droit la **ramène** au premier plan au lieu d'en ouvrir une autre ;
   - la redimensionner par un bord et par la poignée ◢ en bas à droite ; elle refuse de descendre sous 640 × 440 ; à cette
     taille, rien n'est coupé et le contenu défile ;
   - **double-clic sur la barre de titre** et bouton □ : elle s'agrandit **sans couvrir la barre des tâches** ; ─ la réduit,
     ✕ ou **Échap** la ferment ;
   - le rail : clic, **↑ / ↓** (focus visible), **Ctrl+1…6** ; « ⏻ Quitter Chronos » en bas, en rouge clair ;
   - **Apparence** : l'aperçu montre **ton cadran réel**, qui change dès qu'on clique un thème ou un style ; les vignettes passent
     à la ligne sans jamais être coupées ; « Mode étendu » n'apparaît que pour Anneaux ;
   - **Sessions** : widget coupé, les styles sont grisés avec l'invitation ; widget allumé, l'aperçu du widget suit le style
     choisi ;
   - **Données** : « Barre de statut de Claude Code » est un interrupteur qui reflète l'état réel du pont statusLine ;
   - **Diagnostic** : le rapport apparaît après « Génération du rapport… », défile, se sélectionne, « ⧉ Copier » le met dans
     le presse-papiers, « ↻ Actualiser » le refait ;
   - fermer la fenêtre, puis la rouvrir : **même taille, même position, même section**.

## Self-Check: PASSED

- FOUND : `src/Chronos/Views/Reglages/ReglagesWindow.xaml`, `ReglagesWindow.xaml.cs`, `OuvreurReglages.cs`,
  `src/Chronos/ViewModels/ReglagesViewModel.cs`, les quatre fichiers de tests, `Chronos-v3.4.0.exe` (md5 `ddbe7995…`)
- ABSENT (voulu) : `src/Chronos/Views/SettingsWindow.xaml(.cs)`
- FOUND : commits `9e2c3fc`, `b003c65`, `e6eb7f2`, `64135c5`, `fa05719`, `8bec859`
