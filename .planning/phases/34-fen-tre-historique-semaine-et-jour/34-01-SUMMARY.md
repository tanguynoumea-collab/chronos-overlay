---
phase: 34-fen-tre-historique-semaine-et-jour
plan: 01
subsystem: ui
tags: [wpf, xaml, design-tokens, resource-dictionary, tdd, garde-textuelle]

# Dependency graph
requires: []
provides:
  - "Palette du chrome (Panel/Panel2/Line/Ink/Ink2/Accent/Ok) dans Resources/DesignTokens.xaml, valeurs identiques"
  - "Tokens couleur Histo* (HistoTokens, HistoSousAgent, HistoModele1/2/3, HistoGris) + brosse HistoHachure"
  - "38 tokens de taille sys:Double + 1 Thickness (HistoBordRedimensionnement) nommés"
  - "SettingsWindow.xaml fusionne DesignTokens.xaml par pack URI au niveau fenêtre"
  - "GardeTokensHistoriqueTests : garde « aucune valeur en dur » sous Views/Historique et Controls/Historique"
affects: [34-04, 34-05, 34-06, 34-07, 34-08]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Tokens de taille en sys:Double dans un ResourceDictionary (xmlns:sys=clr-namespace:System;assembly=mscorlib), clés sans virgule, valeurs avec un point"
    - "Fusion pack URI au niveau fenêtre (motif MainWindow.xaml) pour que les StaticResource résolvent sans Application"
    - "Garde textuelle par XDocument/Regex sur les sources injectées par AssemblyMetadata(CheminSourcesChronos)"

key-files:
  created:
    - tests/Chronos.Tests/GardeTokensHistoriqueTests.cs
  modified:
    - src/Chronos/Resources/DesignTokens.xaml
    - src/Chronos/Views/SettingsWindow.xaml

key-decisions:
  - "D-34-01 — HistoGris = #5A5960 : le gris « épuisé » de UtilizationToBrushConverter.Epuise ; token FIXE, hors ChronosTheme.BrushTokens()"
  - "D-34-02 — tokens de taille en sys:Double (clés sans virgule, valeurs avec un point) ; seul non-double : HistoBordRedimensionnement (Thickness 6)"
  - "D-34-03 — SettingsWindow.xaml fusionne DesignTokens.xaml au niveau fenêtre par pack URI ; styles Section/Flat/Switch/ThemeCardBtn/StyleChip restent locaux"
  - "D-34-04 — la hachure « hors couverture » est un token de brosse (HistoHachure, DrawingBrush tuilé 4 × 4, Pen HistoGris 1 px)"

patterns-established:
  - "Un test de garde compte les littéraux hexadécimaux d'un XAML (12 dans SettingsWindow) : toute promotion/ajout de couleur est visible"
  - "Le RED d'une garde vide s'obtient par un fichier-témoin copié hors dépôt (jamais stagé)"

requirements-completed: []

# Metrics
duration: ~55 min
completed: 2026-09-27
---

# Phase 34 Plan 01: Tokens de la fenêtre Historique Summary

**Palette du chrome promue de `SettingsWindow.xaml` vers `DesignTokens.xaml` sans changer une valeur (7 brosses, test avant/après), tokens `Histo*` (6 couleurs + hachure) et 38 tailles `sys:Double` ajoutés, garde « aucune valeur en dur » posée sur `Views/Historique` et `Controls/Historique` — 1421 verts deux fois, 0 warning.**

SHA d'entrée : `47904ff` (1415 verts). HIS-07 est **contribué** ici et sera **clos par 34-04** (`requirements mark-complete` NON exécuté).

## Performance

- **Duration:** ~55 min
- **Started:** 2026-09-27
- **Completed:** 2026-09-27
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments

- `Resources/DesignTokens.xaml` : +7 brosses promues (valeurs identiques), +6 couleurs `Histo*`, +`HistoHachure`, +38 `sys:Double`, +1 `Thickness`, `xmlns:sys` ; les 13 brosses du cadran, `UtilBrush` et `BoolToVis` inchangés.
- `Views/SettingsWindow.xaml` : −7 brosses locales, −`BoolToVis` local (fourni par le dictionnaire, évite la double définition), `<Window.Resources>` devient un `ResourceDictionary` qui fusionne `pack://application:,,,/Chronos;component/Resources/DesignTokens.xaml` ; les styles locaux sont inchangés ; **12** littéraux hexadécimaux restants (19 − 7).
- `tests/Chronos.Tests/GardeTokensHistoriqueTests.cs` : 5 `[Fact]` + 1 `[WpfFact]` (classe séparée `GardeTokensHistoriqueXamlTests`, `[Collection("XAML WPF")]`).

## Task Commits

| Task | Commit | Message |
|---|---|---|
| 1 RED | `f0b97c0` | test(34-01): tokens de l'historique — palette promue…, RED 4 |
| 1 GREEN | `fe7eb07` | feat(34-01): palette du chrome promue dans DesignTokens.xaml sans changer une valeur, tokens Histo* et tailles sys:Double, SettingsWindow fusionne le dictionnaire (HIS-07) |
| 2 RED | `5856360` | test(34-01): garde aucune couleur ni taille en dur sous Views/Historique et Controls/Historique, chargement du dictionnaire par LoadComponent, RED |
| 2 GREEN | `bd2387c` | test(34-01): garde tokens de l'historique verte (commit vide : aucun fichier n'a changé depuis le RED, le témoin n'a jamais été stagé) |

## Tokens ajoutés (contrat pour 34-04 à 34-07)

### Couleurs promues (valeurs INCHANGÉES, `StaticResource`, hors `BrushTokens()`)

| Clé | Valeur | Clé | Valeur |
|---|---|---|---|
| `Panel` | `#151322` | `Ink2` | `#A9A6C4` |
| `Panel2` | `#1E1B30` | `Accent` | `#8B7BF0` |
| `Line` | `#2C2942` | `Ok` | `#4EC98A` |
| `Ink` | `#F2F0FB` | | |

### Neutres Histo (DESIGN_PLAN §2 / §2.0)

| Clé | Valeur | Usage |
|---|---|---|
| `HistoTokens` | `#8C89A8` | barres de tokens (principal) |
| `HistoSousAgent` | `#5A5776` | part sous-agents empilée |
| `HistoModele1` | `#9D9AB8` | vue Jour, 1er modèle |
| `HistoModele2` | `#6E6B8C` | vue Jour, 2e modèle |
| `HistoModele3` | `#4A4762` | vue Jour, 3e modèle |
| `HistoGris` | `#5A5960` | « épuisée », fantôme S-1, bordure d'un trou (D-34-01) |
| `HistoHachure` | `DrawingBrush` tuilé `0,0,4,4` Absolute, `LineGeometry 0,4→4,0`, `Pen {StaticResource HistoGris}` 1 px | zones hors couverture / transcripts absents (D-34-04) |

### 38 tailles `sys:Double` (D-34-02) + 1 `Thickness`

| Clé | Valeur | Clé | Valeur |
|---|---|---|---|
| `HistoCorpsTitre` | 16 | `HistoHauteurNiveauPistes` | 150 |
| `HistoCorpsGrand` | 14 | `HistoHauteurRythmePistes` | 72 |
| `HistoCorpsNormal` | 11.5 | `HistoHauteurTokensPistes` | 72 |
| `HistoCorpsMoyen` | 11 | `HistoHauteurCouverture` | 12 |
| `HistoCorpsPetit` | 10.5 | `HistoHauteurNiveauSimplifie` | 200 |
| `HistoCorpsMini` | 9.5 | `HistoHauteurTokensSimplifie` | 90 |
| `HistoCorpsLegende` | 9 | `HistoHauteurNiveauTuiles` | 120 |
| `HistoCorpsInfime` | 8.5 | `HistoHauteurFenetres5hTuiles` | 62 |
| `HistoEpaisseurEscalier` | 2.2 | `HistoHauteurRythmeTuiles` | 58 |
| `HistoEpaisseurPremierPlan` | 2.4 | `HistoHauteurTokensTuiles` | 58 |
| `HistoEpaisseurFin` | 1 | `HistoHauteurNiveauJour` | 190 |
| `HistoEpaisseurBordureActive` | 1.5 | `HistoHauteurRythmeJour` | 64 |
| `HistoLongueurTiretReset` | 8 | `HistoHauteurTokensJour` | 64 |
| `HistoOpaciteTrou` | 0.35 | `HistoLargeurMin` | 760 |
| `HistoOpaciteCouverture` | 0.55 | `HistoHauteurMin` | 480 |
| `HistoOpaciteMaintenant` | 0.6 | `HistoLargeurDefaut` | 920 |
| `HistoRayonCoins` | 16 | `HistoHauteurDefaut` | 610 |
| `HistoLargeurLibelles` | 96 | `HistoHauteurAnnotations` | 18 |
| `HistoLargeurLegendeDroite` | 72 | `HistoHauteurEnTete` | 92 |

`HistoBordRedimensionnement` = `Thickness` 6 (pour `WindowChrome.ResizeBorderThickness` en 34-05).

Le test `Les_tokens_de_taille_sont_des_doubles_nommes` exige **exactement 38** `sys:Double` : un plan qui ajoute un token de taille doit mettre à jour la table du test (volontaire : le contrat de taille est fermé).

## Les 12 littéraux restants de `SettingsWindow.xaml` (dette de forme, hors périmètre)

`#221C33 ×1` (disque du logo), `#241F3C ×2` (fond sélectionné ThemeCardBtn / StyleChip), `#26223A ×1` (survol Flat), `#2C2942 ×1` (piste du Switch — même valeur que `Line`, toléré exactement 1 fois par la garde), `#38C08A ×1` (arc vert du logo), `#3B3860 ×2` (bordure survol ThemeCardBtn / StyleChip), `#6B6890 ×1` (bouton du Switch), `#E8907F ×1` (« Quitter Chronos »), `#ECA23C ×1` (arc ambre du logo), `#F4F2EC ×1` (point central du logo). Non corrigés : la promotion ne change AUCUNE valeur ni AUCUN attribut hors des sept brosses.

## Garde « aucune valeur en dur »

- Périmètre : `Views/Historique/**/*.xaml` (motif `#[0-9A-Fa-f]{6,8}|(FontSize|Height|MinHeight|StrokeThickness|Thickness|Opacity)="[0-9]`) et `Controls/Historique/**/*.cs` (motif `Frozen\(0x|Color\.FromRgb\(|Color\.FromArgb\(|Colors\.[A-Z]|Brushes\.(?!Transparent\b)[A-Z]`). Message d'échec : `chemin relatif:ligne: extrait de la ligne`.
- En vague 1 les dossiers n'existent pas : liste vide, garde verte. Commentaire `// Anti-mutisme relevé en 34-08 (HIS-06) : ≥ 3 xaml et ≥ 7 cs` en place.
- **Variante de chargement retenue pour le `[WpfFact]`** : `Application.LoadComponent(new Uri("/Chronos;component/Resources/DesignTokens.xaml", UriKind.Relative))` fonctionne dans le hôte de test sans `Application` (aucun repli nécessaire). Le test vérifie `HistoGris` (`SolidColorBrush #5A5960`), `HistoHauteurNiveauPistes` (`double` 150), `HistoBordRedimensionnement` (`Thickness` 6), `HistoHachure` (`DrawingBrush`) et `Panel`.

## RED nommés et mutations (par copie, sha256)

| Étape | Résultat |
|---|---|
| RED Task 1 | 4/4 rouges (`La_palette…`, `SettingsWindow_garde_ses_douze…`, `Les_tokens_couleur…`, `Les_tokens_de_taille…`) |
| GREEN Task 1 | filtre `GardeTokensHistorique|ReglagesBinding|Theming|CadranBinding` : 44/44 |
| (t1) `Ink2` → `#A9A8B2` | rouge : `La_palette_des_reglages_est_promue_sans_changer_une_valeur` |
| (t2) retrait de la fusion pack URI | rouges : `La_palette…` + 7 `ReglagesBindingTests` + `ThemingTests.SettingsWindow_se_construit…` (XamlParseException sur les StaticResource) |
| (t3) `HistoHauteurNiveauPistes` → 148 | rouge : `Les_tokens_de_taille_sont_des_doubles_nommes` |
| RED Task 2 | témoin `_temoin.xaml` (`FontSize="12"`) : rouge nommé `Views\Historique\_temoin.xaml:1: …` |
| GREEN Task 2 | 46/46 sur le filtre |
| (m1) `MainWindow.xaml` copié en `Views/Historique/_mut.xaml` | rouge nommé `_mut.xaml:9/30/35/51/69` (`Height="170"`, `StrokeThickness="8"`, …) |

sha256 (16 premiers hex) avant = après révocation : `DesignTokens.xaml` `0c5812fed8ea8991`, `SettingsWindow.xaml` `2242d6773d079558`. Aucun témoin ni mutant n'a touché l'arbre réel (`ls src/Chronos/Views | grep Historique` = 0).

## Verification

- Suite complète `dotnet test Chronos.sln -c Debug --nologo -v q` : **1421 / 1421 verts, deux exécutions consécutives** (1415 d'entrée + 6 ajoutés).
- `dotnet build Chronos.sln -c Debug --nologo | grep -c " warning "` = **0**.
- Critères grep : `x:Key="Panel"` ×1 ; 7 promues dans DesignTokens, 0 dans SettingsWindow ; `#5A5960` ×1 ; `Histo(…)` ×7 ; `<sys:Double` ×38 ; `HistoBordRedimensionnement` ×1 ; `xmlns:sys=` ×1 ; 12 littéraux et 1 pack URI dans SettingsWindow ; `[Fact]` ×5, `[WpfFact]` ×1, `[Collection("XAML WPF")]` ×1, « Anti-mutisme relevé en 34-08 » ×1.

**Où la suite a tourné** : l'arbre réel ne compile pas le projet de tests pendant toute la durée du plan (RED des voisins : `EchellesHistoriqueTests`/`ReticuleTests` de 34-02 au début, puis `ServicesLayerPurityTests.cs(118,70)` de 34-03 en fin de plan). Conformément aux règles d'exécution, toute la boucle TDD et les deux exécutions complètes ont été jouées dans l'instantané `snap-34-01` (`git archive 47904ff` : `src tests docs Chronos.sln` + `README.md CLAUDE.md LICENSE scripts`) avec les trois fichiers du plan copiés depuis l'arbre réel. Une exécution sur l'arbre réel reste due dès que les voisins sont GREEN (34-08 la couvre de toute façon).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Regex de tolérance de `Brushes.Transparent`**
- **Found during:** Task 2
- **Issue:** le motif du plan `Brushes\.[A-Z](?!Transparent)` aurait signalé `Brushes.Transparent` (le `[A-Z]` consomme le `T`, le lookahead ne voit que `ransparent`).
- **Fix:** `Brushes\.(?!Transparent\b)[A-Z]` — même intention, lookahead avant la lettre.
- **Files modified:** tests/Chronos.Tests/GardeTokensHistoriqueTests.cs
- **Commit:** `5856360`

**2. [Rule 3 - Blocking] Mutation (m1) rejouée avec `--no-build`**
- **Found during:** Task 2
- **Issue:** `MainWindow.xaml` porte `x:Class="Chronos.Views.MainWindow"` ; copié sous `src/Chronos/Views/Historique/`, il devient un `Page` du projet et fait échouer la compilation de Chronos (classe dupliquée) avant tout test.
- **Fix:** projet de tests compilé sans le mutant, puis `dotnet test --no-build` avec le mutant en place — la garde lit le texte source à l'exécution, l'intention de la mutation est préservée (rouge nommant `_mut.xaml`).

**3. Commentaire du fichier de test sans le littéral `[Fact]`**
- Le critère `grep -c "\[Fact\]" = 5` compte aussi les commentaires ; la doc de classe a été reformulée pour ne pas contenir la séquence.

**4. Commit GREEN de la Task 2 vide (`--allow-empty`)**
- Prévu par le plan (« ce commit ne porte que le retrait du témoin dans le message ») : aucun fichier n'ayant changé depuis le RED, le commit est un jalon vide.

## Known Stubs

Aucun. Les tokens ne sont consommés par aucune vue en vague 1 (c'est le but : la source de vérité précède les vues).

## Issues Encountered

- Fichiers non suivis / modifiés des voisins visibles dans `git status` (`DivergencesTests.cs`, `ChronosSettings.cs`, `Rendering/Historique/*`…) : non touchés, non stagés.

## Next Phase Readiness

- 34-04 à 34-07 écrivent `{StaticResource HistoHauteurNiveauPistes}`, `{StaticResource HistoGris}`, `{StaticResource HistoHachure}` ; tout chiffre ou hexadécimal sous `Views/Historique` / `Controls/Historique` rougit `Aucune_couleur_ni_taille_en_dur…`.
- 34-08 relève l'anti-mutisme (`≥ 3 xaml`, `≥ 7 cs`) dans ce même test.
- `BoolToVis` n'est plus déclaré dans `SettingsWindow.xaml` : toute nouvelle fenêtre qui fusionne `DesignTokens.xaml` ne doit PAS le redéclarer (double clé = `XamlParseException`).

## Self-Check: PASSED

- FOUND: src/Chronos/Resources/DesignTokens.xaml, src/Chronos/Views/SettingsWindow.xaml, tests/Chronos.Tests/GardeTokensHistoriqueTests.cs
- FOUND commits: f0b97c0, fe7eb07, 5856360, bd2387c
