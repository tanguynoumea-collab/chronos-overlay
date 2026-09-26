---
phase: 29-ce-que-l-app-bureau-sait-de-chaque-session
plan: 04
subsystem: widget-sessions
tags: [csharp, wpf, mvvm, xaml, app-bureau, titre, info-bulle, compacite, maxwidth, mutation, xunit, tdd]

sha_entree_de_plan: 3c5f395
one_liner: "Le widget montre le titre de l'app bureau à la place du dossier, et le dossier reste lisible dans l'info-bulle. Celle-ci dit « titre — dossier — mot » (ou « dossier — mot »), puis le motif d'une attente OBSERVÉE en mots sur une seconde ligne : « permission demandée », « question posée », « réponse demandée », ou le needs_action de l'app tel quel. Une attente déduite ne recopie jamais le motif de son dernier travail. Un seul producteur (AffichageSessions.Nom / Infobulle / MotifLisible) sert le widget et la galerie. Pastilles et Marge bornent le nom à 160 DIP : un titre de 51 caractères est coupé par l'ellipse sur les 8 styles et les 9 thèmes, sans élargir le widget au-delà de la latitude de la borne. Suite : 1039 → 1051 tests isolés, 1062 combinés avec 29-05, 0 échec, deux fois ; mutation MaxWidth jouée et révoquée."

requires:
  - phase: 29-03
    provides: "SessionSnapshot.Titre (posé après l'arbitrage), AffichageSessions.Nom(s)"
  - phase: 28-03
    provides: "Infobulle liée sur les 8 gabarits (« projet — mot »), trois mots d'un seul producteur, garde des bindings muets (Piège 7)"
provides:
  - "AffichageSessions.MotifLisible(s) : motif d'une attente OBSERVÉE en mots, ou null (déduite, tour fini, travail)"
  - "AffichageSessions.Infobulle(s) : « titre — dossier — mot » ou « dossier — mot », puis le motif en seconde ligne"
  - "SessionItemVm.Infobulle : [ObservableProperty] posée par le producteur (plus aucune composition dans un ViewModel)"
  - "SessionsViewModel.Refresh : Project = AffichageSessions.Nom(s), Infobulle = AffichageSessions.Infobulle(s)"
  - "SessionsPreviewViewModel.Add(SessionSnapshot, …) : galerie mise en forme par le même producteur ; titre de 51 caractères (api-migration) et permission (overlay) échantillonnés"
  - "SessionStyles.xaml : MaxWidth=\"160\" sur le nom de Pastilles et de Marge"
affects: [29-05-diagnostic-et-constat, 30-la-lecture-fait-disparaitre, 31-verification-visuelle]

tech-stack:
  added: []
  patterns:
    - "Info-bulle en deux lignes : le mot se lit sur la première, le motif sur la seconde, et les tests lisent la première ligne"
    - "Garde de traduction tenue contre le câblage réel : chaque motif que Chronos câble (matcher de Notification, PermissionRequest, AskUserQuestion) doit se lire en mots"
    - "Compacité sous ellipse : largeur(titre) ≤ largeur(dossier) + latitude de la borne + 1 DIP, et tout gabarit qui écrit le nom le borne"

key-files:
  created: []
  modified:
    - src/Chronos/Services/AffichageSessions.cs
    - src/Chronos/ViewModels/SessionsViewModel.cs
    - src/Chronos/ViewModels/SessionsPreviewViewModel.cs
    - src/Chronos/Resources/SessionStyles.xaml
    - tests/Chronos.Tests/AffichageSessionsTests.cs
    - tests/Chronos.Tests/LibellesSessionsTests.cs
    - tests/Chronos.Tests/SessionStylesBindingTests.cs

key-decisions:
  - "Info-bulle : ligne 1 = « titre — dossier — mot » (ou « dossier — mot », forme de la phase 28 inchangée), ligne 2 = motif d'une attente OBSERVÉE seulement ; une attente déduite ne recopie pas le motif de son dernier travail"
  - "Les trois demandes du bus câblées (agent_needs_input, elicitation_dialog, elicitation_url_dialog) se lisent « réponse demandée » : l'UI parle français, un code technique ne s'affiche pas (CLAUDE.md)"
  - "Le dossier de référence de la matrice est PROJET OLYMPE DATAMIND (161,3 DIP, le maximum de la recherche) et non PROJET ADVANCED SHEET (151,4 DIP, la médiane)"
  - "Le critère « +1 DIP » tient compte de la latitude de la borne : une ellipse ne coupe qu'entre deux caractères, deux textes coupés à 160 n'ont pas la même largeur au DIP près"
  - "MaxWidth=160 (décision verrouillée) conservé, bien que quatre dossiers réels dépassent 160 DIP (161,3 à 166,8) et soient désormais coupés d'un ou deux caractères : à juger à l'œil en phase 31"

requirements-completed: [APP-02]

duration: 14min
completed: 2026-09-25
---

# Phase 29 Plan 04 : Le titre à l'écran, le dossier en info-bulle, la compacité tenue — Summary

**Le widget montre le titre de l'app bureau à la place du dossier, et le dossier reste lisible dans l'info-bulle.
Celle-ci dit « titre — dossier — mot » (ou « dossier — mot » sans titre), puis, pour une attente observée, son
motif en mots sur une seconde ligne. Une attente déduite ne recopie jamais le motif de son dernier travail. Le
widget et la galerie lisent un seul producteur. Pastilles et Marge bornent le nom à 160 DIP : sur les 8 styles et
les 9 thèmes, un titre de 51 caractères est coupé par l'ellipse sans élargir le widget au-delà de ce que la borne
permet. La suite passe de 1039 à 1051 tests isolés (1062 combinés avec 29-05), 0 échec, deux fois de suite.**

## Performance

- **Début :** 2026-09-25T21:44:28Z. **Fin :** 2026-09-25T21:58:40Z. **Durée :** environ 14 min.
- **SHA d'entrée :** `3c5f395`, **1039 verts / 0 échec** mesurés à l'entrée (10 s).
- **Tâches :** 2 sur 2, en TDD, soit 4 commits de tâche.
- **Fichiers :** 7, exactement ceux du plan : 4 sources et 3 classes de tests modifiées.
- **Exécution parallèle :** 29-05 travaillait sur le même arbre, sur des fichiers disjoints. Ses commits
  (`fc2c834`, `7d21a11`, `dd974f1`) s'intercalent avec les miens.

## Mesures (tests)

| Moment | Filtre de la tâche | Suite complète |
|---|---|---|
| Entrée (`3c5f395`) | — | **1039 / 0** |
| Tâche 1, RED (`daffd83`) | **11 échecs / 56** | — |
| Tâche 1, GREEN (`62482cb`) | 56 / 56 | 1058, dont **9 échecs = le RED de 29-05** (`fc2c834`) ; **1049 / 0 pour mes tests** = 1039 + 10 |
| Tâche 2, RED (`d8ac222`) | **2 échecs** | — |
| Tâche 2, GREEN (`21a144a`) | 76 / 76 (avec `GardesPerimetreTests` et `ServicesLayerPurityTests`) | **1062 / 0**, deux exécutions consécutives (14 s et 12 s) |

- **Isolé (29-04 seul) :** 1039 + 10 + 2 = **1051**. Le plan annonçait 1050 ; l'écart d'un test est la garde de
  traduction ajoutée (écart n° 1).
- **Combiné avec 29-05 :** **1062 / 0** à `21a144a`, soit 1051 + 11 pour les commits de 29-05 présents à cet
  instant (`fc2c834`, `7d21a11`, `dd974f1`).
- **Fin de phase (`415cd7d`, les deux plans commités) :** **1062 / 0**, deux exécutions consécutives (10 s et 9 s).
  Le plan attendait 1060 = 1050 + 10. L'écart de +2 vient d'un test de plus dans chaque plan : la garde de traduction
  ici, et un test de plus côté 29-05.

## Les rouges (TDD)

**Tâche 1.** Squelettes `AffichageSessions.Infobulle` et `MotifLisible`, qui lèvent `NotImplementedException` ; le
ViewModel n'est pas encore touché. **11 rouges :**
- `L_infobulle_dit_titre_dossier_mot_puis_le_motif_d_une_attente` × 6 (`NotImplementedException`) ;
- `Une_attente_deduite_ne_recopie_pas_le_motif_du_dernier_travail` (`NotImplementedException`) ;
- `Chaque_motif_d_attente_que_Chronos_cable_se_lit_en_francais` (`NotImplementedException`) ;
- `Le_widget_affiche_le_titre_et_pose_l_infobulle_du_producteur` (« Strings differ » : le widget affichait le dossier) ;
- `La_galerie_pose_ses_infobulles_par_le_producteur_et_montre_un_titre_long` (aucun `Project` de 43 caractères ou plus) ;
- `SessionStylesBindingTests.Les_trois_mots_se_lisent_sur_les_8_styles_et_les_9_themes`, **adapté** : sa nouvelle
  assertion sur la bulle de s1 (deux lignes) échoue, « Collections differ ».

**Tâche 2.** Tests seuls, le XAML n'est pas encore touché. **2 rouges :**
- `Le_nom_est_borne_a_160_dans_Pastilles_et_Marge` (« Sub-string not found » : `MaxWidth="160"`) ;
- `Un_titre_long_est_coupe_sans_elargir_le_widget_sur_les_8_styles_et_les_9_themes` (« style Pastilles, thème
  minuit : le titre long élargit le widget (331,7 DIP contre 205,3 DIP avec le plus long dossier) »).

## Mutation `MaxWidth` (jouée, constatée, révoquée)

| Mutation | sha256 de `SessionStyles.xaml` (avant = après) | Rouges constatés |
|---|---|---|
| `MaxWidth="160"` retiré du nom de **Pastilles** seulement | `81a82802…1767` | `LibellesSessionsTests.Le_nom_est_borne_a_160_dans_Pastilles_et_Marge` (« Sub-string not found ») et `SessionStylesBindingTests.Un_titre_long_est_coupe_sans_elargir_le_widget_sur_les_8_styles_et_les_9_themes` (« style Pastilles, thème minuit : le nom est écrit sans borne (MaxWidth ∞) — un titre long élargirait la fenêtre ») |

La matrice s'arrête au premier échec, et Pastilles est le premier style du premier thème (minuit). La révocation
s'est faite par copie de sauvegarde ; le sha256 est identique (`sha256sum -c` : OK), et la mutation n'a jamais été
commitée.

## Task Commits

1. **Tâche 1 : le producteur dit le nom, l'info-bulle et le motif ; le widget et la galerie le lisent.**
   - `daffd83` (test, RED 11/56)
   - `62482cb` (feat, GREEN)
2. **Tâche 2 : Pastilles et Marge bornent le nom à 160 DIP ; le titre long sur 8 styles × 9 thèmes.**
   - `d8ac222` (test, RED 2)
   - `21a144a` (feat, GREEN, suite combinée 1062/0 deux fois)

**Plan metadata :** commit `docs(29-04)` final (SUMMARY, STATE, ROADMAP, REQUIREMENTS).

## Accomplishments

- **APP-02 à l'écran.** `Project` vaut `AffichageSessions.Nom(s)`, c'est-à-dire le titre de l'app s'il existe, sinon
  le dossier. Le nom de propriété reste `Project`, puisque huit gabarits le lient.
- **Un seul producteur pour l'info-bulle.** `SessionItemVm.Infobulle` n'est plus composée dans le ViewModel : c'est une
  `[ObservableProperty]` posée par `AffichageSessions.Infobulle(s)`, dans le widget comme dans la galerie. Les deux
  `NotifyPropertyChangedFor(nameof(Infobulle))` ont disparu. Sans titre, la forme « dossier — mot » de la phase 28
  est inchangée : `Le_drapeau_et_le_compteur_d_attente_viennent_du_predicat_unique` reste vert sans retouche.
- **Le motif, versant lisible d'APP-03.** Il n'apparaît que pour une attente OBSERVÉE (`WaitingAttention`) :
  - `PermissionRequest` et `permission_prompt` se lisent « permission demandée » ;
  - `AskUserQuestion` se lit « question posée » ;
  - les trois demandes du bus se lisent « réponse demandée » ;
  - le `needs_action` de l'app passe tel quel, sans les blancs de bord.

  Une attente déduite garde le `PreToolUse` de son dernier travail, et ce motif n'est pas recopié.
- **La galerie parle comme l'écran.** `Add` reçoit un `SessionSnapshot`, et la galerie passe par le même producteur
  (Piège 7). Ses cinq échantillons gardent leurs identifiants, dossiers, états et détails. `overlay` porte
  `PermissionRequest`, avec une info-bulle à deux lignes, et `api-migration` le titre « Migration de l'API de
  facturation vers la v2 du SDK » (51 caractères). Le sous-titre de la galerie (« 2 « En attente », 1 « En
  attente ? », 2 « Réflexion » ») reste vrai, et un test compte les trois mots.
- **La compacité est tenue.** `MaxWidth="160"` s'applique au nom de Pastilles et de Marge, et à rien d'autre (Jetons
  50 et Annonciateur 130 sont inchangés). Sur les 72 combinaisons, le titre long est coupé par l'ellipse (Pastilles
  et Marge : `CharacterEllipsis`, borne 160, 160 DIP occupés). Le widget ne s'élargit que de la latitude de la
  borne. Les quatre gabarits qui écrivent le nom le bornent, sur les 9 thèmes (36 noms écrits comptés).

## Critères grep

| Fichier | Critère | Attendu | Mesuré |
|---|---|---|---|
| `SessionsViewModel.cs` | `public string Infobulle =>` | 0 | 0 |
| `SessionsViewModel.cs` | `AffichageSessions.Infobulle(s)` / `AffichageSessions.Nom(s)` | 1 / 1 | 1 / 1 |
| `SessionsPreviewViewModel.cs` | `AffichageSessions.Infobulle(` / `AffichageSessions.Nom(` | 1 / 1 | 1 / 1 |
| `AffichageSessions.cs` | `public static string Infobulle(SessionSnapshot s)` / `public static string? MotifLisible(SessionSnapshot s)` | 1 / 1 | 1 / 1 |
| `SessionStyles.xaml` | `MaxWidth="160"` | 2 | 2 |
| `SessionStyles.xaml` | `ToolTip="{Binding Infobulle}"` / `ToolTip="{Binding Project}"` | 8 / 0 | 8 / 0 |
| `SessionStyles.xaml` | `DataTemplate x:Key=` | 8 | 8 |

**Vérification du plan :**
- Mes quatre commits ne touchent, sous `src/Chronos/Services` et `App.xaml.cs`, que `AffichageSessions.cs`
  (`git show --stat`). Le `DiagnosticService.cs` du diff global appartient à 29-05.
- Aucun type WPF dans `Services/` : `ServicesLayerPurityTests` est vert.
- Aucun test ne lit le vrai `%APPDATA%`, `%LOCALAPPDATA%` ou `~/.claude` : les magasins sont temporaires. L'overlay
  et la galerie n'ont pas été lancés.

**Gardes vertes, sans assouplissement :** `SessionStylesBindingTests` (72 combinaisons, menus, séparateurs,
Annonciateur), `LibellesSessionsTests` (7/7, dont `Les_huit_infobulles_disent_le_mot`,
`Tout_ce_que_les_gabarits_lient_existe_sur_les_deux_ViewModels` et `Aucun_ancien_libelle_ne_subsiste…`),
`GardesPerimetreTests` (dont `Le_titre_est_pose_apres_l_arbitrage_et_nulle_part_ailleurs`),
`ServicesLayerPurityTests` et `AffichageSessionsTests`.

**Test adapté, sans renommage :** `SessionStylesBindingTests.Les_trois_mots_se_lisent_sur_les_8_styles_et_les_9_themes`.
Chaque bulle est lue par sa première ligne (`Split('\n')[0]`, puis `LastIndexOf(" — ")` comme avant). Une assertion
est ajoutée : la bulle de s1 vaut exactement `["overlay — En attente", "permission demandée"]`. Aucune assertion
n'a été retirée.

## Decisions Made

Voir `key-decisions` en tête. En bref :
- l'info-bulle a deux lignes, et la première garde la forme de la phase 28 quand il n'y a pas de titre ;
- le motif n'appartient qu'aux attentes observées ;
- les codes du bus se lisent en français ;
- la matrice se mesure contre le vrai plus long dossier et tient compte de la latitude de l'ellipse ;
- la borne verrouillée de 160 est conservée, et ses effets sur quatre dossiers réels sont consignés.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - CLAUDE.md « UI en français »] Les trois demandes du bus se lisent « réponse demandée ».**
- **Constat :** le `MotifLisible` du plan ne traduisait que `PermissionRequest`, `permission_prompt` et
  `AskUserQuestion`. Les trois types de `Notification` que Chronos câble lui-même (`agent_needs_input`,
  `elicitation_dialog`, `elicitation_url_dialog`, §1 du contrat) produisent pourtant `WaitingAttention` avec leur
  code pour motif. L'info-bulle aurait affiché `elicitation_dialog`, alors que la XML-doc du plan dit « les codes
  techniques connus sont traduits ».
- **Correctif :** ces trois codes se lisent « réponse demandée ». Une garde, `Chaque_motif_d_attente_que_Chronos_cable_se_lit_en_francais`,
  lit le matcher réel de `SessionHookInstaller.Cablage`, plus `PermissionRequest` et `AskUserQuestion`. Un type
  ajouté au matcher sans traduction la fait rougir.
- **Coût :** +1 test, d'où 1051 isolés au lieu de 1050.
- **Commits :** `daffd83` (test), `62482cb` (code).

**2. [Rule 1 - Erreur de mesure du plan] Le dossier de référence de la matrice est PROJET OLYMPE DATAMIND.**
- **Constat :** le plan présentait « PROJET ADVANCED SHEET » comme « le plus long dossier réel, 161 DIP ». Mesuré
  en WPF (`FormattedText`, Segoe UI SemiBold 12,5, la police de `SessionsWindow`), il fait **151,4 DIP** : c'est la
  **médiane** de la recherche (85 / 151 / 161), pas son maximum. Garder ce dossier revenait à exiger une borne de
  152 DIP environ, contre la décision verrouillée de 160.
- **Correctif :** la matrice utilise « PROJET OLYMPE DATAMIND », 161,3 DIP, qui est le maximum de la recherche.
  Mesures des autres dossiers réels : TEXTURE MANAGER 161,7 ; OLYMPE SENTINELLE 165,0 ; SMART NEWSLETTER 166,8.
- **Commit :** `d8ac222`.

**3. [Rule 1 - Critère infaisable] L'assertion « +1 DIP » tient compte de la latitude de la borne.**
- **Constat, mesuré par une sonde temporaire jamais commitée :** une ellipse ne coupe qu'entre deux caractères.
  Deux textes coupés à la même borne ont donc des largeurs différentes de quelques DIP :
  - Pastilles : dossier coupé 152,9 DIP, titre coupé 155,4 DIP, soit un widget de 196,9 contre 199,4 ;
  - Annonciateur, déjà borné à 130 avant cette phase : 161,2 DIP avec le dossier contre 167,6 avec le titre, et
    aucun dossier réel ne dépasse 165,1.

  Aucun dossier de référence ne peut donc satisfaire « largeur(titre) ≤ largeur(dossier) + 1 » sur les 8 styles.
  Ce n'est pas un élargissement : c'est la granularité de l'ellipse.
- **Correctif :** l'invariant vrai est écrit en deux parties.
  1. Tout gabarit qui écrit le nom le borne à 160 DIP au plus. Les 4 × 9 noms écrits sont comptés.
  2. `largeur(titre) ≤ largeur(dossier) + latitude + 1 DIP`, où la latitude est ce que la borne laisse au nom du
     dossier (`MaxWidth − largeur désirée du nom`). Autrement dit, le widget n'est jamais plus large qu'avec un nom
     qui remplirait la borne.

  Les contrôles Pastilles et Marge du plan sont conservés tels quels : `CharacterEllipsis`, `MaxWidth == 160` et
  `ActualWidth ≤ 160,5`. La mutation rougit toujours le test, par le contrôle de borne.
- **Commit :** `21a144a`.

### Ajustements mineurs

- **Deux commentaires XAML** placés avant le `TextBlock` du nom, dans Pastilles et dans Marge, disent pourquoi la
  borne existe. Les balises elles-mêmes ne changent que par `MaxWidth="160"`.
- **Commentaire du style implicite `ToolTip`** réécrit selon le texte du plan (« titre — dossier — mot d'état, puis,
  pour une attente, son motif sur une seconde ligne… liées à Infobulle »). Il ne recopie pas le littéral du binding,
  et le compte reste 8.

---

**Total :** 1 ajout exigé par CLAUDE.md (+1 test), 2 corrections d'erreurs du plan (dossier de référence, critère
infaisable), 2 ajustements de commentaires. **Impact :** aucun élargissement de périmètre, aucune garde assouplie ;
la borne verrouillée de 160 n'a pas bougé.

## Issues Encountered

- **Heredoc Bash.** Comme en 29-03, un heredoc contenant un script d'édition échoue (« unexpected EOF »). Les
  scripts ont été écrits dans le bloc-notes de session, puis exécutés.
- **Fins de ligne.** Les fichiers sont en CRLF sur disque (`core.autocrlf=true`). L'utilitaire d'édition normalise
  l'ancien et le nouveau texte vers la fin de ligne du fichier.

## Known Limits (écrites, non corrigées ici — à juger en phase 31)

- **La borne de 160 coupe quatre dossiers réels d'un ou deux caractères** dans Pastilles et Marge : OLYMPE DATAMIND
  (161,3), TEXTURE MANAGER (161,7), OLYMPE SENTINELLE (165,0) et SMART NEWSLETTER (166,8 DIP). La recherche arrondissait
  le maximum à 161 sur l'échantillon « moins de 24 h ». La borne est une décision verrouillée ; si l'œil juge la
  coupe gênante en phase 31, la relever à 167 ne touche que deux attributs et la garde texte.
- **L'info-bulle n'a ni largeur maximale ni retour à la ligne.** Un `needs_action` long s'affiche sur une seule ligne
  large. Cela ne touche pas le widget (c'est un popup), et c'est à juger dans la galerie.
- **Un motif inconnu passe tel quel** (`_ => s.Reason.Trim()`), c'est le choix du plan. Les motifs que Chronos câble
  sont tenus par la garde de l'écart n° 1.

## Known Stubs

Aucun. Les squelettes RED ont été remplacés dans `62482cb`. `grep SQUELETTE|NotImplementedException|TODO|FIXME|placeholder`
ne donne rien sur les quatre sources modifiées.

## Vérification manuelle (non bloquante, portée par 29-VALIDATION.md, l. 133)

À faire en phase 31, dans la galerie `Chronos.exe --sessions`, sur les 8 tuiles et les 9 thèmes :
- le titre long d'`api-migration` est coupé proprement par l'ellipse ;
- la largeur des tuiles ne change pas ;
- l'info-bulle dit « titre — dossier — mot » ;
- celle d'`overlay` montre « permission demandée » en seconde ligne.

L'agent n'a lancé ni l'overlay ni la galerie. Les skills frontend-design et windows-wpf ne sont pas installées,
comme en phase 28 : la matrice WPF 8 × 9 compense pour les tailles et les textes, pas pour l'œil.

## Exigences

- **APP-02** : livré sur son versant automatisable, c'est-à-dire le critère 1 de la ROADMAP. Le titre remplace le
  dossier, le dossier reste en info-bulle, et un titre long ne casse pas la compacité sur les 8 styles et les 9
  thèmes. **Coché.** Le contrôle à l'œil reste une vérification manuelle écrite (phase 31).

## Next Phase Readiness

- **29-05** : le rapport lit `AffichageSessions.Nom` (inchangé). S'il veut citer le motif d'une ligne, `MotifLisible`
  est disponible.
- **30 (LUE-01)** : le widget ne compose plus rien. Une ligne « lue » pourra être signalée par le producteur, dans
  l'info-bulle, sans toucher les gabarits.
- **31** : vérification visuelle de la galerie ; décision éventuelle sur la borne de 160 (voir Known Limits).

## Self-Check: PASSED

- Fichiers vérifiés présents : les 7 fichiers modifiés et ce SUMMARY.
- Commits vérifiés dans `git log` : `daffd83`, `62482cb`, `d8ac222` et `21a144a`, ainsi que la référence d'entrée
  `3c5f395`.
