---
phase: 35-4-semaines-acc-s-release-3-3-0
plan: 04
subsystem: historique — vue 4 semaines (XAML pur), segment actif, reprises visuelles de 34 (infobulle bornée, annotations Jour sur deux rangées)
tags: [HIS-05, PisteQuatreSemaines, VueQuatreSemainesView, DesignTokens, BorneInfobulleConverter, VueJourView, VueSemaineView, TDD, mutations]

sha_entree_de_plan: a00aafd
one_liner: "La vue « 4 semaines » existe et s'ouvre depuis l'en-tête : une piste dédiée superpose S-3 / S-2 / S-1 en HistoGris aux opacités 0,25 / 0,45 / 0,8 des tokens (PushOpacity, grille dessinée une fois) et S par bandes de la rampe du thème, plateau épuisé en gris. Les étiquettes sont empilées, entières, dans une colonne de 140. La couverture a quatre rangées de 10 au pas de 16. Les semaines d'avant le journal sont hachurées et disent « avant le journal — aucun relevé ». Le marqueur « journal ouvert le 14 sept. 2026 » est posé sur S-1, l'annotation « épuisée jeu. 20:00 → bloquée jusqu'au reset » est reportée sur l'axe de S, et le pied dit « Rien n'est inventé avant l'ouverture du journal. ». Tokens 38 → 45. Les quatre infobulles du réticule restent dans leur piste, et en Jour les resets et les trous ont chacun leur rangée. Revue DAEDALUS non rendue : rien n'a été inventé. Suite 1648 / 0 deux fois, 0 warning Debug et Release."

requires:
  - phase: 35-01
    provides: "DonneesQuatreSemaines, EtiquetteSemaine, RangeeCouverture, EchelleTemps.Reporter, VM (IsVueQuatreSemaines, LibellesJoursCourts, EtiquettesSemaines, AnnotationsEpuiseeSemaines, RangeesCouverture, PiedQuatreSemaines, Theme), FixturesQuatreSemaines.SemaineUnEpuisee"
  - phase: 34-04 / 34-05 / 34-08
    provides: "PisteBase (Rendu, Plume, GeometrieEscalier, DessinerGrille, trace), PisteCouverture, convertisseurs InstantVersX / LargeurIntervalle, HistoriqueWindow et injection des pinceaux du thème dans les vues, gardes tokens / textes"
provides:
  - "Controls/Historique/PisteQuatreSemaines.cs — piste NIVEAU des quatre semaines (DP Semaines, Rampe, Gris, EpaisseurEscalier, OpaciteS1/S2/S3, NbBandes ; trace « semaine k n opacite umax gris|rampe », « bande … », « palier … gris|rampe »)"
  - "Views/Historique/VueQuatreSemainesView.xaml(.cs) — vue §2.4 en XAML pur, code-behind = InitializeComponent()"
  - "DesignTokens.xaml — 7 sys:Double : HistoOpaciteSemaine1/2/3 (0.8 / 0.45 / 0.25), HistoHauteurNiveauQuatreSemaines 250, HistoLargeurEtiquettesSemaines 140, HistoHauteurCouvertureSemaine 10, HistoPasCouvertureSemaines 16"
  - "Converters/HistoriqueConverters.cs — BorneInfobulleConverter (D-35-17)"
  - "HistoriqueWindow — segment « 4 semaines » actif (style SegmentQuatreSemaines), vue hébergée VueQuatreSemaines + pinceaux du thème injectés ; TextesHistorique.InfobulleBientot supprimée"
  - "VueJourView — RangeeAnnotationsHaut (resets + épuisée) / RangeeAnnotationsTrous (causes des trous)"
  - "tests/Chronos.Tests/Fakes/FixturesQuatreSemaines.SemaineCouranteEpuisee(d, tz) ; BancQuatreSemaines (banc de montage de la vue)"
affects:
  - "35-05 / docs : écarts assumés à écrire — pas de réticule en 4 semaines (D-35-16), semaine épuisée absente de la galerie (fixture de test seulement)"
  - "revue DAEDALUS (itération DESIGN-REVIEW) : points à regarder listés plus bas"

tech-stack:
  added: []
  patterns:
    - "Opacité poussée DANS la piste (DrawingContext.PushOpacity) plutôt que UIElement.Opacity : la grille et la courante restent pleines, la brosse reste celle du token"
    - "Trace de rendu qui nomme la brosse qui a PEINT (ReferenceEquals(brosse, Gris) → gris | rampe) : une mutation de brosse rougit sans lire un pixel"
    - "Bandes empilées en grilles Auto/*/Auto indépendantes (largeurs par cellules de tokens) : même colonne * partout sans colonne partagée, donc aucune contribution d'un élément à cheval sur les colonnes Auto"
    - "Infobulle bornée par IMultiValueConverter [x, ActualWidth Self, ActualWidth Canvas] : la seconde passe de mise en page corrige la première (largeur encore nulle)"

key-files:
  created:
    - src/Chronos/Controls/Historique/PisteQuatreSemaines.cs
    - src/Chronos/Views/Historique/VueQuatreSemainesView.xaml
    - src/Chronos/Views/Historique/VueQuatreSemainesView.xaml.cs
    - tests/Chronos.Tests/VueQuatreSemainesBindingTests.cs
    - tests/Chronos.Tests/HonneteteQuatreSemainesTests.cs
  modified:
    - src/Chronos/Resources/DesignTokens.xaml
    - src/Chronos/Views/Historique/HistoriqueWindow.xaml
    - src/Chronos/Views/Historique/HistoriqueWindow.xaml.cs
    - src/Chronos/Views/Historique/VueJourView.xaml
    - src/Chronos/Views/Historique/VueSemaineView.xaml
    - src/Chronos/Converters/HistoriqueConverters.cs
    - src/Chronos/Text/TextesHistorique.cs
    - tests/Chronos.Tests/Fakes/FixturesQuatreSemaines.cs
    - tests/Chronos.Tests/GardeTokensHistoriqueTests.cs
    - tests/Chronos.Tests/PistesHistoriqueTests.cs
    - tests/Chronos.Tests/HistoriqueBindingTests.cs
    - tests/Chronos.Tests/TextesHistoriqueTests.cs
    - tests/Chronos.Tests/HistoriqueConvertersTests.cs
    - tests/Chronos.Tests/VueJourBindingTests.cs
    - tests/Chronos.Tests/VueSemaineBindingTests.cs

key-decisions:
  - "D-35-13 : piste dédiée PisteQuatreSemaines : PisteBase. Grille une fois, puis S-3 → S-1 en HistoGris (PushOpacity des DP liées aux tokens), puis S par bandes de rampe comme l'escalier hebdo de PisteNiveau. Un refus serveur compte 1,0 (plateau épuisé gris). Une semaine sans relevé ne dessine et ne trace rien. PisteNiveau n'est pas touchée."
  - "D-35-14 : sept tokens, 38 → 45 (table TaillesHisto mise à jour dans le commit RED de la même tâche). Lecture de « espacées de 16 px » = pas haut-à-haut de 16 (4 × 10 + 3 × 6 = 58) ; la lecture « écart de 16 entre rangées » est laissée à la revue."
  - "D-35-15 : étiquettes empilées S, S-1, S-2, S-3 dans la colonne de 140. Retour à la ligne, jamais d'ellipse. Trait repère HistoGris à l'opacité de sa semaine, masqué (Hidden) pour S, qui est en couleur. La courante est en Ink semi-gras."
  - "D-35-16 : pas de réticule ni d'infobulle en 4 semaines (§2.4 n'en prévoit pas). Pas de piste tokens."
  - "D-35-17 : BorneInfobulleConverter [x, largeurInfobulle, largeurCanvas] → clamp(x, 0, max(0, W − w)) sur les QUATRE infobulles. x invalide → 0. Largeurs pas encore mesurées → x borné à gauche (précision au plan : la passe suivante corrige, jamais un saut à 0)."
  - "D-35-18 (nouvelle) : chaque bande de la vue 4 semaines est sa propre grille Auto/*/Auto (StackPanel de bandes), au lieu d'un Corps à colonnes partagées. Le gabarit des rangées de couverture est à cheval sur les trois colonnes et son texte « avant le journal — aucun relevé » aurait pu élargir les colonnes Auto. Alignement prouvé par test (largeur et abscisse des 4 PisteCouverture = celles de la piste NIVEAU)."

requirements-completed: [HIS-05]

metrics:
  duration: 35min
  completed: 2026-09-27
  tasks: 3
  files: 20
  tests_added: 19
---

# Phase 35 Plan 04 : vue 4 semaines, segment actif, infobulle bornée, annotations Jour sur deux rangées — Summary

**« 4 semaines » s'ouvre : quatre courbes sur un axe, quatre rangées de couverture, et chaque semaine d'avant le journal le dit au lieu
de se taire. Plus aucune infobulle ne sort de sa piste, et en Jour un reset ne recouvre plus un trou. HIS-05 est clos.**

## Performance

- **Durée :** ~35 min (≈ 17:03Z → 17:38Z)
- **Tâches :** 3 (TDD, 6 commits)
- **Fichiers :** 5 créés, 15 modifiés
- **Tests :** +19 (4 piste, 5 mise en page, 4 honnêteté, 1 segment, 2 convertisseur, 2 Jour, 1 Semaine) ; suite complète **1648 / 0 deux fois de suite** sur l'arbre réel (avec les commits de 35-05) ; `dotnet build -c Release` et `-c Debug --no-incremental` : **0 avertissement**
- **Lignes XAML :** `VueQuatreSemainesView.xaml` 283, `VueJourView.xaml` 264 → 278, `VueSemaineView.xaml` 595 → 618, `HistoriqueWindow.xaml` 296 → 304 ; `PisteQuatreSemaines.cs` 107

## Commits (SHA d'entrée `a00aafd`)

| Tâche | RED | GREEN |
|------|-----|-------|
| 1. Sept tokens + PisteQuatreSemaines | `57301cf` : RED 5 (CS0246 `PisteQuatreSemaines` + table 45 lignes) | `4855223` |
| 2. VueQuatreSemainesView + segment actif | `c882c81` : RED 11 (CS0246 `VueQuatreSemainesView`) | `f4d485b` |
| 3. Infobulle bornée + annotations Jour | `b26ee59` : RED 5 (CS0246 `BorneInfobulleConverter`) ; puis, convertisseur seul, 3 rouges NOMMÉS : `Les_resets_et_les_trous_ont_chacun_leur_rangee` (rangée absente), `L_infobulle_reste_dans_la_piste_au_bord_droit` (749,4 + 221,6 > 752), `Les_infobulles_des_trois_styles_restent_dans_la_piste` (600,9 + 214,0 > 736) | `c41e45e` |

Tous en `--no-verify`, stage explicite, aucun `--amend`. 35-05 tournait en parallèle : deux collisions de build (CS2001 sur `obj/`),
chacune réglée par une attente puis un nouvel essai. Il n'y a pas eu de RED du voisin, donc pas d'instantané (`snap-35-04` non créé).

## Ce que la vue montre (§2.4, mot pour mot)

- Axe fixe : « sam. » … « ven. » aux minuits locaux de S (« sam. » à x = 0 de la piste, « ven. » à 6/7), puis « reset hebdo → » dans la colonne des étiquettes. La largeur de l'axe est liée à celle de la piste, donc il reste aligné quand la barre de défilement apparaît.
- NIVEAU 250 : S-3 / S-2 (avant le journal) ne dessinent rien. S-1 est en gris à 0,8 et S en rampe du thème. Repères « 100 % » / « 0 ».
- Étiquettes : « S · 19 sept. · N % » (Ink semi-gras), « S-1 · 12 sept. · N % », « S-2 · 5 sept. · pas de relevés (avant le journal) », « S-3 · 29 août · pas de relevés (avant le journal) ».
- COUVERTURE PAR SEMAINE : S (présent Ok, trous gris / ambre), S-1 (hachure du 12 sept. 00:00 au 14 sept. 12:00, puis « journal ouvert le 14 sept. 2026 », puis présent), S-2 et S-3 (hachure pleine largeur + « avant le journal — aucun relevé »).
- Pied fixe : « Rien n'est inventé avant l'ouverture du journal. » Pas de piste tokens, pas de réticule.

## Revue DAEDALUS — tâche réservée

**Revue DAEDALUS non rendue au 2026-09-27 17:38Z.** `.zeus/reports/design-review-1.md` n'existe pas (`.zeus/reports/` ne contient que
les deux fichiers llm-council du 26/09). `.zeus/state.json` indique : `phase: DESIGN-REVIEW`, `phase_status: en_attente_galerie_utilisateur`,
`iterations.design_review: 0`. Aucun écart n'a été deviné ni anticipé. Les écarts entreront par une itération DESIGN-REVIEW → GSD
(3 au plus, pipeline ZEUS).

Rappel : la revue se fait sur `dotnet run --project src/Chronos -- --historique`, lancé par l'utilisateur (l'agent n'a rien lancé),
sur les vues Semaine × 3, Jour et 4 semaines. La semaine épuisée n'existe qu'en fixture de test (`FixturesQuatreSemaines`) : c'est un
écart de galerie à signaler à la revue, parce que le critère §8.2 (« vue 4 semaines avec semaine épuisée ») n'est pas visible dans la galerie.

**Points à regarder dans la galerie, vue 4 semaines :**
1. Lisibilité des trois gris (0,8 / 0,45 / 0,25) sur Panel. Dans le scénario, S-2 et S-3 sont avant le journal, donc seul le gris 0,8 (S-1) est visible dans la galerie. Les deux autres ne sont prouvés que par la trace.
2. Pas des rangées de couverture : 16 haut-à-haut (D-35-14). Si la maquette voulait 16 d'écart entre deux rangées, il faut changer un token (`HistoPasCouvertureSemaines` 16 → 26).
3. Textes des rangées posés SUR la bande : « avant le journal — aucun relevé » sur la hachure, et « journal ouvert le 14 sept. 2026 » sur le début de la bande « présent » de S-1. Le contraste est à juger.
4. Étiquettes empilées (D-35-15), et non posées à la hauteur de leur valeur finale.
5. Axe « sam. … ven. » : les libellés sont posés au début de leur colonne (minuit), comme en Semaine.
6. Annotation « épuisée … » tronquée à son plateau, avec une infobulle (même règle que D-34-33). Elle n'est visible qu'avec la fixture.
7. Jour : les deux rangées d'annotations (resets, puis trous) ajoutent 18 px au-dessus de NIVEAU.

## Écarts connus, à écrire dans les docs (35-05 ou la suite)

- **Pas de réticule en 4 semaines** (D-35-16) : `SurcoucheReticule` n'accepte qu'une série, et §2.4 n'en prévoit pas.
- **Semaine épuisée absente de la galerie** : elle est prouvée par des fixtures dérivées (`SemaineUnEpuisee`, `SemaineCouranteEpuisee`) et non par le scénario.
- L'annotation « épuisée … » est tronquée à la largeur de son plateau, avec une infobulle (règle Jour D-34-33 reprise).

## Mutations (par copie, révoquées par re-copie, sha256 identiques avant / après)

| # | Mutation | Rouge nommé | sha256 (avant = après) |
|---|----------|-------------|------------------------|
| p1 | fantômes peints par `Rampe.ArcBrush(umax)` | `PistesHistoriqueTests.Quatre_semaines_fantomes_gris_aux_opacites_des_tokens`, `…La_semaine_un_epuisee_monte_a_cent_pour_cent_en_gris` | `PisteQuatreSemaines.cs` 67604f56111e (1) |
| p2 | bande ≥ 1 de S peinte par `Rampe.ArcBrush(1.0)` | `PistesHistoriqueTests.La_semaine_courante_epuisee_est_un_plateau_gris` | idem |
| p3 | `HistoOpaciteSemaine1` 0.8 → 0.7 | `GardeTokensHistoriqueTests.Les_tokens_de_taille_sont_des_doubles_nommes` | `DesignTokens.xaml` f3eae91cf14f |
| q1 | `OpaciteS1="{StaticResource HistoOpaciteSemaine3}"` | `VueQuatreSemainesBindingTests.La_piste_recoit_les_opacites_des_tokens` | `VueQuatreSemainesView.xaml` 95ab701ed7b3 |
| q2 | témoin `_mut.xaml` avec `OpaciteS1="0.8"` (`dotnet test --no-build`) | `GardeTokensHistoriqueTests.Aucune_couleur_ni_taille_en_dur_dans_les_vues_et_les_pistes_de_l_historique` | témoin supprimé (`ls _mut.xaml` échoue), jamais commité |
| q3 | zone hachurée figée `Collapsed` | `HonneteteQuatreSemainesTests.Les_semaines_anterieures_au_journal_sont_vides_et_dites`, `…Le_marqueur_journal_ouvert_est_pose_sur_la_semaine_d_ouverture` | `VueQuatreSemainesView.xaml` 95ab701ed7b3 |
| q4 | `TextTrimming=CharacterEllipsis` sur le style `Etiquette` | `VueQuatreSemainesBindingTests.Les_etiquettes_sont_a_droite_entieres_et_dans_l_ordre` | idem |
| r1 | infobulle Jour posée par `XReticule` seul | `VueJourBindingTests.L_infobulle_reste_dans_la_piste_au_bord_droit` | `VueJourView.xaml` cc9405b60fb5 |

(1) Les mutations p1 et p2 ont été jouées sur la version 67604f56111e. Après leur révocation, seule une phrase de la XML-doc a changé
(« Pourquoi une opacité poussée… », pour que `grep -c PushOpacity` = 1). Le fichier commité est 1b45b6c71ecc.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug de test] Hauteurs à 125 % DPI : DP exacte, ActualHeight ± 0,5**
- **Trouvé pendant :** tâche 2 (GREEN). `PisteQuatreSemaines.ActualHeight` valait 249,6 au lieu de 250 : `UseLayoutRounding` à 125 % arrondit 312,5 px physiques à 312.
- **Correction :** le test vérifie la DP `Height` exactement (250, 10) et l'`ActualHeight` à ± 0,5, comme `VueJourBindingTests` (190 → 190,4). Le pas entre deux rangées est vérifié à 16 ± 0,5.
- **Fichiers :** `tests/Chronos.Tests/VueQuatreSemainesBindingTests.cs` (commité avec le GREEN `f4d485b`).

**2. [Rule 1 - Bug de test] Infobulle au bord droit : « recule » n'est vrai que si elle déborde**
- **Trouvé pendant :** tâche 3 (RED nommé). Aujourd'hui, le dernier relevé est à 17:12 et l'infobulle tient dans la piste (≈ 539 + 214 ≤ 752). L'assertion « gauche < XReticule » était donc fausse sans que le code soit en cause.
- **Correction :** l'assertion devient `Canvas.Left == max(0, min(XReticule, W − w))` ± 0,5. Un volet « la veille » (dernier relevé 23:55) exige que le cas éprouve le bord droit, et le test Semaine l'exige sur les trois styles. Le test est plus strict qu'avant : il vérifie que l'infobulle recule JUSTE de ce qui dépasse.
- **Fichiers :** `VueJourBindingTests.cs`, `VueSemaineBindingTests.cs` (commit `c41e45e`).

**3. [Rule 3 - Structure] Bandes en grilles indépendantes (D-35-18) au lieu d'un Corps à colonnes partagées**
- Le plan décrivait des rangées dans une grille à colonnes Auto partagées, avec un gabarit de couverture à cheval sur les trois colonnes. Or un élément à cheval sur des colonnes Auto peut les élargir de la largeur de son texte. Chaque bande est donc sa propre grille Auto/*/Auto, avec des cellules de tokens. L'alignement est prouvé par le test des hauteurs : largeur et abscisse des 4 `PisteCouverture` égales à celles de la piste NIVEAU, à ± 1.

**4. [Précision] `BorneInfobulleConverter` : largeurs pas encore mesurées → x borné à gauche (et non 0)**
- À la première passe, l'infobulle repliée a une largeur nulle. Rendre 0 la ferait sauter au bord gauche pendant une passe. Seul un x invalide rend 0, comme au plan. Le test `BorneInfobulle_est_tolerante_et_ne_revient_pas` épingle les deux cas.

**5. [Ajout] Trace « semaine … gris|rampe »**
- Le plan le laissait ouvert (« si nécessaire »). C'est nécessaire : sans le mot de la brosse, la mutation p1 (fantôme en rampe) ne rougissait aucune assertion.

### Tests existants adaptés

- `HistoriqueBindingTests.L_en_tete_commun_est_complet` : retrait des trois assertions sur le segment désactivé et son infobulle « bientôt ». Elles sont remplacées par `Le_segment_4_semaines_est_actif_et_ouvre_la_vue`.
- `TextesHistoriqueTests` : les deux assertions sur `InfobulleBientot` sont retirées. L'une devient `Assert.Null(typeof(TextesHistorique).GetField("InfobulleBientot"))`.
- Vue Jour : aucun test existant ne cherchait les trous dans `RangeeAnnotationsHaut` (grep vide), donc rien à adapter. Les tests qui trouvent « jeton invalide » par texte restent verts.

## Known Stubs

Aucun. Toutes les données de la vue viennent du VM (35-01). Le seul écart de contenu est la semaine épuisée absente du scénario de
galerie. C'est voulu (décision 1 de 35-01 : `ScenariosHistorique` n'est pas modifié), et c'est signalé plus haut pour la revue.

## Self-Check: PASSED

- Fichiers créés présents : `PisteQuatreSemaines.cs`, `VueQuatreSemainesView.xaml`, `VueQuatreSemainesView.xaml.cs`, `VueQuatreSemainesBindingTests.cs`, `HonneteteQuatreSemainesTests.cs`.
- Commits présents : `57301cf`, `4855223`, `c882c81`, `f4d485b`, `b26ee59`, `c41e45e`.
- Greps d'acceptation : 45 `sys:Double` et 7 nouveaux ; `PushOpacity` = 1, `Escalier.ParBandes(` = 1, 0 hexadécimal dans la piste. Dans la vue : `<hc:PisteQuatreSemaines` = 1, `<hc:PisteCouverture` = 1, `HistoOpaciteSemaine[123]` = 7, tokens de taille = 9, `PisteTokens|SurcoucheReticule` = 0, valeurs en dur = 0, `PiedQuatreSemaines|CouvertureParSemaine` = 2. `InfobulleBientot|bientôt` = 0 (fenêtre + textes), `VueHistorique.QuatreSemaines` = 1, `VueQuatreSemaines.Resources` = 1, code-behind 10 lignes. `BorneInfobulle` : 3 (Jour), 5 (Semaine) ; `RangeeAnnotationsTrous` = 1.
- `HistoriqueWindow.xaml.cs` : CRLF conservé. ROADMAP.md / STATE.md : non modifiés. HIS-05 cochée dans `REQUIREMENTS.md` (seules les deux lignes HIS-05 changent).
