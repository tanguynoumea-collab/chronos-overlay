---
phase: 39-th-mes-p-le-classique-vive
verified: 2026-10-03T00:00:00Z
status: passed
score: 4/4 must-haves verified
re_verification: non — vérification initiale
---

# Phase 39 : Thèmes Pâle / Classique / Vive — rapport de vérification

**Objectif de phase :** 15 thèmes rangés en Pâle / Classique / Vive, six palettes nouvelles aux hex exacts, Néon/Aurore corrigés, gris épuisé ≥ 3:1 et rouge de rampe valide sur tout le catalogue, Braises / Fusible / Marée / Volets et `TickReset` thémés.
**Vérifié le :** 2026-10-03
**Statut :** passed
**Re-vérification :** non

## Vérités observables

| # | Vérité (critère ROADMAP) | Statut | Preuve |
|---|---|---|---|
| 1 | Trois groupes titrés, quinze thèmes, `Categorie` par thème, choix existants conservés (THM-01) | VÉRIFIÉ | `Theming/ChronosTheme.cs` : 15 `From(...)` dans `ThemeCatalog.All`, chacun avec `CategorieTheme` (Pâle 6, Classique 5, Vive 4, composition identique au §5.1). `CategorieTheme.cs` fixe l'ordre d'affichage. `MainViewModel.GroupesThemes` construit les groupes par catégorie. `ReglagesWindow.xaml` l.705 : `ItemsControl` des groupes, titre en style `Etiquette` (10,5 semi-gras `Ink2`), vignettes en `WrapPanel`, tailles 88×76 (`DesignTokens.xaml`). Tests : `Catalogue_quinze_themes_trois_categories`, `Les_neuf_cles_existantes_restent_resolues`, `Les_groupes_de_themes_suivent_les_categories_et_partagent_les_choix`. |
| 2 | Six palettes exactes, Néon/Aurore corrigés (THM-02) | VÉRIFIÉ | Les sept couleurs de Sauge, Lavande, Graphite, Marine, Synthwave et Lave sont identiques au §5.2, relues ligne à ligne dans le catalogue. Néon : ambre `#FFC23D`, rouge `#FF2E63`. Aurore : ambre `#F0C36D`, rouge `#F2577A`. Disque, piste, graduation et texte inchangés. Test `Chaque_theme_a_exactement_sa_palette`. |
| 3 | Gris épuisé ≥ 3:1 et rouge de rampe valide, prouvés sur tout le catalogue (THM-03) | VÉRIFIÉ | `EpuiseLisible()` dans `From()` : plus petit mélange piste → graduation (pas de 0,01, t ≥ 0,18) atteignant 3:1 contre le disque opaque (`ContrasteWcag`). Tests `Le_gris_epuise_est_lisible_sur_tout_le_catalogue`, `Le_gris_epuise_est_le_plus_petit_melange_qui_suffit`, `Le_rouge_de_rampe_est_rouge_et_lisible_sur_tout_le_catalogue` (teinte 335°–20° et ≥ 3:1), avec boucle sur `ThemeCatalog.All`. Un thème futur fautif fait donc échouer le test. |
| 4 | Les quatre cadrans suivent le thème, `TickReset` dans les pinceaux (THM-04) | VÉRIFIÉ | `BrushTokens()` expose `TickReset` (= `TextePrincipal`), `Epuise`, `CadranTuile`, `CadranAttente`, `PlaqueTexte`, `PlaqueFilet` et `PlaqueHachure`. Aucune couleur littérale dans `Views/Cadrans/*.xaml` ni `Controls/Cadrans/*.cs` : le seul résultat du grep est un `Color.FromArgb` dérivé d'une couleur existante (alpha seulement). Garde `GardeCouleursCadransTests` (4 vues, 4 contrôles, anti-mutisme, interdit la fusion de dictionnaires) et `CadransThemeBindingTests` (`Les_quatre_cadrans_suivent_chaque_theme`, `Changer_de_theme_rafraichit_les_pinceaux`). |

**Score :** 4/4

## Artefacts

| Artefact | Statut | Détails |
|---|---|---|
| `src/Chronos/Theming/ChronosTheme.cs` | VÉRIFIÉ | Catalogue de 15 thèmes, dérivations, `BrushTokens` complet. |
| `src/Chronos/Theming/CategorieTheme.cs` | VÉRIFIÉ | Enum Pâle, Classique, Vive. |
| `src/Chronos/Theming/ContrasteWcag.cs` | VÉRIFIÉ | Utilisé par `From()` et par les tests. |
| `src/Chronos/ViewModels/GroupeThemes.cs` | VÉRIFIÉ | Record `Titre` + `Themes`, branché dans `MainViewModel`. |
| `src/Chronos/Views/Reglages/ReglagesWindow.xaml` | VÉRIFIÉ | Section Thème groupée. |
| `src/Chronos/Views/Cadrans/*`, `Controls/Cadrans/*` | VÉRIFIÉ | Thémés, sans couleur littérale. |
| `tests/Chronos.Tests/ThemingTests.cs`, `GardeCouleursCadransTests.cs`, `CadransThemeBindingTests.cs` | VÉRIFIÉ | Tests substantiels, non creux. |

## Liens clés

| De | Vers | Via | Statut |
|---|---|---|---|
| `ThemeCatalog.All` | `MainViewModel.GroupesThemes` | regroupement par `Categorie` | CÂBLÉ |
| `GroupesThemes` | `ReglagesWindow.xaml` | `ItemsControl` imbriqués, commande remontée à la Window | CÂBLÉ |
| `ChronosTheme.BrushTokens()` | vues de cadran | `DynamicResource` | CÂBLÉ |
| `Epuise` du thème | Historique et cadrans | token `Epuise` | CÂBLÉ |

## Couverture des exigences

| Exigence | Plans | Statut |
|---|---|---|
| THM-01 | 39-01, 39-02 | SATISFAITE |
| THM-02 | 39-01 | SATISFAITE |
| THM-03 | 39-01, 39-04 | SATISFAITE |
| THM-04 | 39-01, 39-03, 39-04 | SATISFAITE |

Les quatre ID du frontmatter des plans sont présents dans `REQUIREMENTS.md` (cochés, « Complete » dans la table de traçabilité). Aucune exigence orpheline pour la Phase 39.

## Anti-patterns

Aucun bloquant. Aucun TODO ni stub dans les fichiers de thème et de cadran examinés.

## Contrôles comportementaux

Je n'ai pas relancé la suite de tests. Je m'appuie sur la porte de phase annoncée : build `-warnaserror` à 0 avertissement, 1761/1761 tests. J'ai relu le code des tests concernés et vérifié qu'ils vérifient bien ce qu'ils annoncent. Je n'ai pas lancé l'exe.

## Écart décidé (pas un gap)

Le gris neutre (`Neutre`) reste inchangé, et la question « épuisé ≈ neutre » est renvoyée à la revue visuelle (DESIGN-REVIEW après la phase 42). Le §9.5 du plan en fait un critère de revue.

## Vérification humaine

Pas bloquante pour la phase. À traiter dans DESIGN-REVIEW : lisibilité visuelle du gris épuisé sur chacun des 15 thèmes, rendu des quatre cadrans sur chaque thème, et distinction épuisé / neutre.

Remarque mineure : les titres de groupe s'affichent en majuscules (« PÂLE », « CLASSIQUE », « VIVE »), comme l'étiquette « THÈME » voisine. Ils reprennent le style `Etiquette` (10,5 semi-gras `Ink2`) voulu par le plan. Ce n'est pas un écart.

## Synthèse

L'objectif est atteint. Le catalogue compte 15 thèmes aux valeurs exactes du plan, rangés en trois groupes. Le gris épuisé et le rouge de rampe sont garantis par des tests qui parcourent tout le catalogue. Les quatre cadrans alternatifs et `TickReset` suivent le thème, et une garde statique interdit les couleurs littérales.

_Vérifié : 2026-10-03_
_Vérificateur : Claude (gsd-verifier)_
