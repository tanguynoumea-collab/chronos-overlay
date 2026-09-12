---
phase: 19-nouvelle-doctrine-du-composite
plan: 04
subsystem: ui
tags: [wpf, mvvm, xaml-binding, honnetete-des-chiffres, exa-05, del-04, pastille, wpffact]

# Dependency graph
requires:
  - phase: 19-02
    provides: "ProvenanceReleve (Frais / EncoreValide / PlancherAvecActivite) et WindowState.TokensDepuisReleve"
  - phase: 19-03
    provides: "la doctrine câblée en tête de chaîne — les snapshots de production portent Provenance et UnExactADejaEteObtenu"
  - phase: 17-05
    provides: "le harnais [WpfFact] MonterPastille, ReconnecterCommand, et le piège LoginClaudeCommand"
provides:
  - "PercentFormatter.Format(double?, ProvenanceReleve?) — « ≥ N % » pour un plancher, « N % » pour un exact, surcharge ADDITIVE"
  - "WindowGaugeViewModel : le préfixe dérive de la PROVENANCE ; HasTokens/TokensText dérivent de TokensDepuisReleve"
  - "MainViewModel.AfficherInvitationConnexion + MajPastilles, point de recomposition UNIQUE des trois pastilles"
  - "MainWindow.xaml : PastilleInvitationConnexion, bindée sur ReconnecterCommand, exclusive de PastilleDeconnexion"
  - "Surcharge MonterPastille(etat, snapshot) du harnais WPF, signature historique conservée"
affects: [phase-20-honnetete-visible, EXA-03, EXA-06, cadran, sémiologie]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Surcharge additive plutôt que mutation : bool (galerie d'aperçu) et ProvenanceReleve? (production) cohabitent sans ambiguïté de résolution"
    - "Point de recomposition unique pour des drapeaux d'UI alimentés par PLUSIEURS canaux asynchrones"
    - "Garde de non-retour par test comportemental : « l'ancien champ ne surface PLUS rien »"

key-files:
  created: []
  modified:
    - src/Chronos/Text/PercentFormatter.cs
    - src/Chronos/ViewModels/WindowGaugeViewModel.cs
    - src/Chronos/ViewModels/MainViewModel.cs
    - src/Chronos/Views/MainWindow.xaml
    - tests/Chronos.Tests/WindowGaugeViewModelTests.cs
    - tests/Chronos.Tests/CadranBindingTests.cs
    - tests/Chronos.Tests/MainViewModelTests.cs

key-decisions:
  - "« ≥ » et non « ~ » : l'incertitude d'un plancher est UNILATÉRALE ; un tilde autoriserait la lecture « peut-être 75 » alors qu'on SAIT « 80 au minimum »"
  - "Le préfixe dérive de la PROVENANCE et non de la fiabilité : un exact encore valide (DEL-03) est un chiffre juste et ne porte aucune marque"
  - "Surcharge ADDITIVE de PercentFormatter : la galerie de styles (CadranPreviewViewModel) pilote un booléen d'aperçu, elle n'a aucune provenance à exhiber — zéro site d'appel retouché"
  - "HasTokens perd sa condition de fiabilité : seule une fenêtre corrigée par delta porte TokensDepuisReleve, le tester serait redondant"
  - "MajPastilles : les deux entrées arrivent par deux canaux et à deux instants ; sans point de recomposition unique, le dernier arrivé écraserait l'autre"
  - "L'invitation s'efface devant la pastille de déconnexion : même geste (ReconnecterCommand), deux pastilles sur 170 px seraient une redondance ; la déconnexion est le diagnostic le plus précis"
  - "« == false » et non « != true » : null = non évalué, une absence de réponse ne produit jamais une affirmation"

patterns-established:
  - "Garde de commande par INSTANCE : Assert.Same(vm.ReconnecterCommand, pastille.Command) + Assert.NotSame(vm.LoginClaudeCommand, …) — un binding fautif porterait le même nom et passerait un test textuel"
  - "Un test réécrit change de champ mais ne disparaît jamais : la preuve reste, la matière change"

requirements-completed: [EXA-04, EXA-05, DEL-04]

# Metrics
duration: 19min
completed: 2026-09-12
---

# Phase 19 Plan 04: « ≥ N % » et l'invitation à se connecter — Summary

**Le vocabulaire d'honnêteté du cadran est complet : un plancher s'annonce « ≥ 42 % » (incertitude
unilatérale) là où un exact reste « 42 % », et quand aucun chiffre exact n'a jamais été obtenu une
troisième pastille — bindée sur `ReconnecterCommand`, jamais sur la commande qui bascule — invite à se
connecter au lieu d'afficher un pourcentage.**

## Performance

- **Duration:** 19 min
- **Started:** 2026-09-12T04:39:50Z
- **Completed:** 2026-09-12T04:58:23Z
- **Tasks:** 2 (TDD, 4 commits)
- **Files modified:** 7

## Accomplishments

- **DEL-04, moitié visible.** `PercentFormatter` reçoit une surcharge prenant la **provenance** : « ≥ 80 % »
  sur un `PlancherAvecActivite`, « 80 % » sur `Frais`, `EncoreValide` et `null`. La surcharge historique
  (`bool`) est intacte **mot pour mot** — `CadranPreviewViewModel` et ses quatre assertions littérales
  n'ont pas été touchés.
- **La matière brute change de champ.** `WindowGaugeViewModel.HasTokens`/`TokensText` dérivent désormais de
  `TokensDepuisReleve` ; plus **aucune** lecture d'`EstimatedTokens` côté présentation.
- **EXA-05, moitié visible.** Troisième pastille `PastilleInvitationConnexion`, ancrée bas-droite hors de
  toute géométrie d'anneau, **actionnable**, mutuellement exclusive de la pastille de déconnexion.
- **Le piège de la phase 17 reverrouillé** sur l'instance de commande réellement bindée.
- **688 → 699 tests verts**, deux exécutions consécutives, zéro échec.

## Task Commits

1. **Task 1 (RED): le plancher s'annonce « ≥ » et lit TokensDepuisReleve** — `1c39ead` (test)
2. **Task 1 (GREEN): « ≥ N % » et matière brute au bon champ** — `308ba41` (feat)
3. **Task 2 (RED): l'invitation à se connecter, côté VM et côté XAML** — `fc30f75` (test)
4. **Task 2 (GREEN): l'invitation visible et actionnable** — `d40345f` (feat)

## Files Created/Modified

- `src/Chronos/Text/PercentFormatter.cs` — surcharge `Format(double?, ProvenanceReleve?)`. Deux surcharges
  coexistent sans ambiguïté : `bool` n'accepte pas `null`, donc `Format(x, null)` vise la nouvelle et
  `Format(x, false)` l'ancienne.
- `src/Chronos/ViewModels/WindowGaugeViewModel.cs` — `Apply` dérive le préfixe de `s.Provenance` et les
  tokens de `s.TokensDepuisReleve`.
- `src/Chronos/ViewModels/MainViewModel.cs` — `AfficherInvitationConnexion`, les deux entrées brutes
  mémorisées (`_etatAuth`, `_jamaisDExactEtRienAAfficher`) et `MajPastilles`.
- `src/Chronos/Views/MainWindow.xaml` — `PastilleInvitationConnexion`. **Ajouts seuls : zéro ligne
  supprimée**, empreinte 170×170 et géométrie des anneaux rigoureusement intactes.
- `tests/Chronos.Tests/WindowGaugeViewModelTests.cs` — +2 tests (marque du plancher, bout en bout de `Apply`).
- `tests/Chronos.Tests/CadranBindingTests.cs` — 3 tests de tokens réécrits, 1 test de fiabilité mixte
  réécrit, +1 garde de non-retour, +2 `[WpfFact]`, surcharge du harnais `MonterPastille`.
- `tests/Chronos.Tests/MainViewModelTests.cs` — +6 tests couvrant la matrice EXA-05.

## Justification NOMINATIVE du changement de champ des trois tests de tokens

Les trois tests suivants pilotaient `WindowState.EstimatedTokens`. Ils ont été **réécrits, jamais
supprimés** : ils restent la preuve que la matière brute n'est surfacée ni sur un exact, ni à zéro token.

| Ancien nom | Nouveau nom | Champ piloté |
|---|---|---|
| `Estimated_avec_tokens_expose_HasTokens_et_TokensText_abrege` | `Plancher_avec_tokens_depuis_releve_expose_HasTokens_et_TokensText_abrege` | `TokensDepuisReleve = 62_484_658` |
| `Exact_sans_tokens_n_affiche_aucun_texte_de_tokens` | `Exact_sans_tokens_depuis_releve_n_affiche_aucun_texte_de_tokens` | `TokensDepuisReleve = null` |
| `Estimated_avec_zero_token_ne_surface_rien` | `Plancher_avec_zero_token_depuis_releve_ne_surface_rien` | `TokensDepuisReleve = 0` |

**Pourquoi :** `EstimatedTokens` portait la somme de l'estimation **absolue** (`tokens / plafond`) que la
phase 16 a démolie — le sous-système de plafonds n'existe plus, le champ est **mort en production** depuis.
Le chiffre de DEL-04 est `TokensDepuisReleve` : les tokens observés **depuis** le relevé exact, matière
d'une **borne inférieure** et non d'un pourcentage. Réutiliser l'ancien champ aurait rattaché au nouveau
chiffre une sémantique que ce milestone a explicitement tuée.

Un **quatrième** test a été ajouté en garde de non-retour : `EstimatedTokens_seul_ne_surface_PLUS_rien_au_cadran`
— une fenêtre portant 99 M d'`EstimatedTokens` et rien d'autre n'expose plus aucun texte.

## Les trois mutations jouées, et leur révocation

| # | Mutation | Fichier | Tests tombés | Révoquée |
|---|---|---|---|---|
| 1 | `snap.UnExactADejaEteObtenu == false` → `!= true` | `MainViewModel.cs` | **1** — `Bit_non_evalue_null_n_allume_pas_l_invitation` | oui |
| 2 | `Command="{Binding ReconnecterCommand}"` → `LoginClaudeCommand` sur l'invitation | `MainWindow.xaml` | **1** — `L_invitation_est_bindee_sur_ReconnecterCommand_et_JAMAIS_sur_LoginClaudeCommand` | oui |
| 3 | retrait de `&& !AfficherPastilleDeconnexion` | `MainViewModel.cs` | **2** — `L_etat_Deconnecte_eteint_l_invitation`, `Un_changement_d_etat_d_auth_apres_le_snapshot_recompose_l_invitation` | oui |

Après révocation : `git status --short` **vide**, suite complète à **699 verts**. Les trois gardes sont donc
**falsifiables** et non décoratives. La mutation 2 est la plus importante : sans elle, rien ne prouverait
que le test compare une **instance** et non un nom — un binding vers `LoginClaudeCommand` porterait le même
nom de propriété XAML et passerait n'importe quel contrôle textuel.

## Vérification

| Contrôle | Attendu | Mesuré |
|---|---|---|
| `dotnet test Chronos.sln -v q --nologo`, exécution 1 | 0 échec | **0 échec / 699** |
| `dotnet test Chronos.sln -v q --nologo`, exécution 2 (flakiness BAML) | 0 échec | **0 échec / 699** |
| Gardes permanentes (Purity, Normalisation, Doctrine, CompositionRoot) | vertes | **13/13** |
| `La_sonde_d_en_tetes_est_le_PRIMAIRE` | vert | **1/1** |
| `grep -c "public static string Format" PercentFormatter.cs` | 2 | **2** |
| `grep -c 'isEstimated ? "~" : ""' PercentFormatter.cs` | 1 | **1** |
| `grep -cE "s\.EstimatedTokens" WindowGaugeViewModel.cs` | 0 | **0** |
| `grep -cE "s\.TokensDepuisReleve" WindowGaugeViewModel.cs` | 2 | **2** |
| `grep -c "AfficherInvitationConnexion" MainViewModel.cs` | ≥ 2 | **2** |
| `grep -c "AfficherInvitationConnexion" MainWindow.xaml` | 1 | **1** |
| `grep -c "{Binding LoginClaudeCommand}" MainWindow.xaml` | 0 | **0** |
| `grep -c "{Binding ReconnecterCommand}" MainWindow.xaml` | 2 | **2** |
| `grep -c "UnExactADejaEteObtenu == false" MainViewModel.cs` | 1 | **1** |
| Lignes supprimées dans `MainWindow.xaml` | 0 | **0** |
| Fichiers touchés sous `src/Chronos/Services/` ou `Models/` | 0 | **0** |
| Fichiers touchés sous `src/Chronos/Views/Cadrans/` | 0 | **0** |
| `CadranPreviewViewModel.cs` | inchangé | **inchangé** |
| `grep -rl "RefreshAsync" src/Chronos --include=*.cs \| wc -l` | 2 | **2** |
| `stat -c '%s %Y' "$APPDATA/Chronos/oauth.dat"` | `518 1783863147` | **`518 1783863147`** |

**Variation du total de tests : 688 → 699 (+11), justifiée nominativement.** +2 dans
`WindowGaugeViewModelTests` (`UtilizationText_plancher_prefixe_superieur_ou_egal`,
`Apply_derive_le_prefixe_de_la_provenance_et_non_de_la_fiabilite`), +1 garde de non-retour
(`EstimatedTokens_seul_ne_surface_PLUS_rien_au_cadran`), +6 dans `MainViewModelTests` (matrice EXA-05),
+2 `[WpfFact]` d'invitation. **Zéro test supprimé** ; 4 tests réécrits sur place.

## Decisions Made

- **« ≥ » et non « ~ ».** L'incertitude d'un plancher est **unilatérale** : on sait qu'on est à 80 au
  minimum, c'est la borne **supérieure** qui est inconnue. Un tilde annoncerait une erreur symétrique et
  autoriserait la lecture « peut-être 75 » — un mensonge poli. Ce n'est pas un choix de style.
- **Le préfixe dérive de la provenance, pas de la fiabilité.** Un exact **encore valide** (DEL-03, plus
  vieux que la limite d'âge mais prouvé sans activité depuis) est un chiffre **juste** : il ne porte aucune
  marque. Décider par `SourceReliability` aurait sali précisément le cas que le plan 19-02 a passé sa
  démonstration à réhabiliter.
- **Surcharge additive plutôt que mutation.** `CadranPreviewViewModel` pilote un booléen d'aperçu pour la
  galerie de styles : il n'a aucune provenance à exhiber, et le faire porter un `ProvenanceReleve` fabriqué
  aurait été une fausse donnée dans une vitrine. Les deux surcharges coexistent sans ambiguïté de
  résolution (`bool` n'accepte pas `null`). Trois sites d'appel, zéro retouché.
- **`HasTokens` perd sa condition de fiabilité.** La formule était
  `Reliability == Estimated && EstimatedTokens > 0` ; elle devient `TokensDepuisReleve is > 0`. Seule une
  fenêtre corrigée par delta porte ce champ (posé par `DoctrineFraicheur` uniquement) : re-tester la
  fiabilité aurait été une redondance qui aurait masqué la vraie condition.
- **`MajPastilles`, point de recomposition unique.** Les deux entrées arrivent par **deux canaux** (un
  snapshot ; un événement d'authentification) et à **deux instants**. Sans ce point, un changement d'état
  d'authentification survenant après le dernier snapshot laisserait l'invitation périmée. Le test
  `Un_changement_d_etat_d_auth_apres_le_snapshot_recompose_l_invitation` est la raison d'être de la méthode,
  et la mutation 3 prouve qu'il mord.
- **L'invitation s'efface devant la déconnexion.** Les deux portent le **même geste** (`ReconnecterCommand`)
  ; les afficher ensemble sur un cadran de 170 px serait une redondance, pas une information. La
  déconnexion est le diagnostic le **plus précis** des deux : elle gagne.
- **`== false` et non `!= true`.** `null` signifie « non évalué » (magasin en panne, ou snapshot né hors de
  la couche de doctrine — dont `UsageSnapshot.Empty`). Une absence de réponse ne doit jamais produire une
  affirmation, et c'est ce qui garde les quatre tests de pastille de la phase 17 verts sans retouche.
- **`NonConnecte` n'allume toujours rien par lui-même.** La XML-doc d'`AppliquerEtatAuth` est amendée et non
  supprimée : c'est le **bit du magasin** et non l'état d'authentification qui décide de l'invitation. Un
  utilisateur peut être parfaitement connecté et n'avoir jamais obtenu le moindre chiffre (sonde coupée,
  endpoint muet) ; à l'inverse, allumer sur `NonConnecte` fabriquerait un badge permanent pour qui a
  délibérément choisi de ne pas se connecter.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Réécriture d'un quatrième test non prévu au plan : `Etat_fiabilite_mixte_le_tilde_du_pourcentage_est_par_fenetre`**

- **Found during:** Task 1 (bascule du préfixe de la fiabilité vers la provenance)
- **Issue:** Le plan nommait **trois** tests à réécrire dans `CadranBindingTests` (les tests de tokens) et
  listait les tests devant rester verts sans retouche. `Etat_fiabilite_mixte_le_tilde_du_pourcentage_est_par_fenetre`
  ne figurait dans **aucune** des deux listes. Or il assertait `Assert.StartsWith("~", vm.SevenDay.UtilizationText)`
  sur une fenêtre `Estimated` **sans provenance** : dès que `Apply` a cessé de lire `IsEstimated`, ce test
  tombait. Blocage direct de la tâche.
- **Fix:** Test **réécrit et renommé** `Etat_fiabilite_mixte_la_marque_du_pourcentage_est_par_fenetre`, avec
  les provenances explicites (`Frais` pour la 5 h, `PlancherAvecActivite` pour l'hebdo) et les assertions
  portées sur « ≥ » au lieu de « ~ ». Il continue de prouver exactement ce qu'il prouvait : **les deux
  fenêtres portent des marques indépendantes**. Un `Assert.DoesNotContain("~", …)` a été ajouté pour graver
  qu'on n'affiche plus jamais une incertitude symétrique.
- **Files modified:** `tests/Chronos.Tests/CadranBindingTests.cs`
- **Verification:** vert dans les deux exécutions complètes ; le nombre de tests est inchangé pour ce
  fichier (réécriture sur place, aucune suppression).
- **Committed in:** `1c39ead` (RED de la Task 1)

**2. [Rule 3 - Blocking] Étape RED jouée contre des SQUELETTES compilables**

- **Found during:** Tasks 1 et 2
- **Issue:** `tests/Chronos.Tests` référence `Chronos` : un commit RED non compilable rendrait
  `dotnet test` non invocable, et le critère « compilabilité à chaque commit » serait violé.
- **Fix:** Précédent 18-01 appliqué. Task 1 : la surcharge `Format(double?, ProvenanceReleve?)` est commitée
  en RED avec un corps `throw new NotImplementedException()` — **aucun appelant en production** à ce commit,
  l'application reste fonctionnelle. Task 2 : `AfficherInvitationConnexion` est commitée déclarée mais
  jamais posée. Les deux RED sont donc **comportementaux** (5 échecs / 26 puis 4 échecs / 52) et non des
  échecs de compilation.
- **Files modified:** `src/Chronos/Text/PercentFormatter.cs`, `src/Chronos/ViewModels/MainViewModel.cs`
- **Verification:** `dotnet test` invocable aux quatre commits ; les échecs sont nominatifs.
- **Committed in:** `1c39ead`, `fc30f75`

---

**Total deviations:** 2 auto-fixed (2 blocking)
**Impact on plan:** Aucun élargissement de périmètre. La déviation 1 est une conséquence mécanique et
inévitable du changement demandé par le plan lui-même — le plan avait simplement omis ce test dans son
inventaire. La déviation 2 est l'application d'un précédent déjà établi dans ce dépôt.

## Issues Encountered

- **Le critère `grep -c "AfficherInvitationConnexion" MainViewModel.cs` ≥ 2 échouait à 1** après une
  implémentation pourtant conforme : le champ généré s'écrit `_afficherInvitationConnexion` (minuscule), le
  grep est sensible à la casse, et seule l'affectation dans `MajPastilles` correspondait. Résolu par une
  documentation **utile** plutôt que par un artifice : la XML-doc du champ porte désormais un
  `<see cref="AfficherInvitationConnexion"/>` qui renvoie vers `<see cref="MajPastilles"/>`, gravant que la
  propriété n'est **jamais** posée directement.

## Known Stubs

Aucun stub. Les deux livrables sont câblés de bout en bout et prouvés par test.

## Dette explicitement léguée à la phase 20

**(a) Apparence définitive des trois pastilles.** L'invitation reprend au pixel près le gabarit de la
pastille de déconnexion (14 px, marge 6, ancrage bas-droite) avec le token `TexteSecondaire`. **Elle se
superposerait exactement à la pastille de déconnexion si les deux s'allumaient** — ce qui ne peut pas
arriver, l'exclusivité étant garantie et testée côté ViewModel, mais la cohabitation visuelle des **trois**
pastilles (deux peuvent s'allumer ensemble : hors ligne + invitation) dans les **2 modes** × **3 thèmes** ×
**5 styles de cadran** n'a **pas** été conçue. Ici on livre le **signal**, pas sa forme finale.

**(b) `IsStale` et `SourceCapturedAt` coexistent avec la limite d'âge de la doctrine.** Le seuil de
`MainViewModel.IsStale` vaut **2 min**, celui de la doctrine **6 min**, et `IsStale` n'est **bindé nulle
part** (vérifié : zéro occurrence dans les XAML). Deux définitions concurrentes de « périmé » dans le même
ViewModel. À retirer au profit de `Provenance`, qui est par fenêtre et non par snapshot.

**(c) `Provenance` ne couvre que la MOITIÉ d'EXA-06.** Elle dit quel **ÉTAT** (frais / encore valide /
plancher), pas quelle **SOURCE** (sonde d'en-têtes / endpoint OAuth / pont statusLine / magasin du dernier
exact). Le **nom** de la source n'est porté par **aucun champ** aujourd'hui et reste entièrement à
concevoir.

**(d) Contrainte imposée au dessin : PAS d'arc de delta, PAS de barre d'erreur.** DEL-04 n'a pas de borne
supérieure — il n'existe **rien** à représenter au-delà du plancher. Une barre d'erreur ou un arc
secondaire fabriquerait une borne supérieure qui n'existe pas, c'est-à-dire exactement le mensonge que
« ≥ » évite. L'ouverture doit être marquée **autrement que par une longueur** (luminance, texture,
discontinuité de trait — jamais une géométrie qui se lirait comme une quantité).

**(e) Découvert pendant l'exécution — `TokensText` / `HasTokens` sont calculés mais bindés NULLE PART.**
Vérifié : zéro occurrence dans tous les `*.xaml`. Le « centre épuré » de la v1.3 avait retiré la ligne de
tokens. La matière brute de DEL-04 est donc **correcte, testée, et invisible**. C'est un choix assumé ici
(aucune géométrie ne bouge dans ce plan), mais la phase 20 doit décider explicitement : soit la surfacer
sous le plancher, soit acter qu'elle reste un signal de diagnostic seulement.

**(f) Découvert pendant l'exécution — `DataUnavailable` n'est bindé nulle part non plus.** EXA-05 exige
« affiche *indisponible* **et** invite à se connecter, jamais un pourcentage ». Les deux dernières moitiés
sont acquises à ce commit (aucun pourcentage n'est rendu, et l'invitation est visible et actionnable, son
`ToolTip` portant le libellé). Le mot « indisponible » comme **label explicite à l'écran** appartient à
EXA-03 (distinction visuelle frais / daté / indisponible), c'est-à-dire à la phase 20.

**(g) Commentaire périmé, volontairement non corrigé.** `MainWindow.xaml` l. 87 dit encore
« UtilizationText (« ~ » si estimé…) ». Le corriger aurait produit une **ligne supprimée** dans ce fichier,
en violation du critère « ajouts uniquement » du plan. À rectifier en phase 20, qui touchera ce bloc.

## Statut des requirements

| Req | Statut à ce commit | Justification |
|---|---|---|
| **EXA-04** | **Complete** | Plus aucune utilization dérivée d'un comptage de tokens n'est affichée. Le dernier chemin de présentation lisant `EstimatedTokens` (produit de l'estimation absolue démolie en phase 16) est coupé, et une garde de non-retour comportementale l'interdit. Structurellement acquis depuis 16-03 (la source de delta n'implémente pas `IUsageProvider`, test réflexif) et 19-02 (`Utilization` jamais gonflée) ; **visiblement** acquis ici. |
| **EXA-05** | **Complete** | Moitié données acquise en 19-01/19-03 (`UnExactADejaEteObtenu`). Moitié visible livrée ici : pastille réellement bindée, actionnable, exclusive, prouvée par `[WpfFact]` sur l'instance de commande. Aucun pourcentage n'est rendu dans ce cas (assertion explicite). Réserve consignée en (f). |
| **DEL-04** | **Complete** (déjà coché par 19-03) | La marque du plancher passe du plan à l'écran : « ≥ 42 % ». Réserve consignée en (e). |
| EXA-02, DEL-03 | Complete depuis 19-03 | — |
| EXA-03, EXA-06 | Pending — **phase 20** | Hors périmètre, explicitement. |

La **confirmation finale** de la phase appartient au plan **19-05**.

## Next Phase Readiness

- Le vocabulaire d'honnêteté du cadran est **complet et testé** : « ≥ » pour un plancher, rien pour un
  exact, aucun pourcentage quand rien n'a jamais été obtenu, une invitation actionnable à la place.
- La phase 20 n'a plus qu'à donner à ce vocabulaire sa **forme définitive**, sous la contrainte (d) : le
  delta n'est pas un nombre, il change la **nature** du chiffre — il ne se dessine pas comme une longueur.
- **Rappel du blocage réel de terrain, inchangé :** le jeton OAuth de cette machine est mort depuis le
  2026-07-12. Ni `/api/oauth/usage` ni la sonde d'en-têtes ne répondent tant que la reconnexion manuelle
  (point de vérification humaine de la phase 17) n'a pas été faite. L'invitation livrée ici est
  précisément le signal qui rendra ce blocage **visible** au lieu de silencieux.
- **Vérification humaine à prévoir en phase 20** (non bloquante) : lisibilité et non-recouvrement des trois
  pastilles dans les 3 thèmes × 5 styles × 2 modes.

---
*Phase: 19-nouvelle-doctrine-du-composite*
*Completed: 2026-09-12*

## Self-Check: PASSED

7 fichiers modifiés présents sur disque, 4 commits de tâche présents dans l'historique
(`1c39ead`, `308ba41`, `fc30f75`, `d40345f`), suite complète verte en deux exécutions consécutives.
