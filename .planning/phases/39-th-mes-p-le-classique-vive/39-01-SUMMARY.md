---
phase: 39-th-mes-p-le-classique-vive
plan: 01
subsystem: theming
tags: [wpf, theming, wcag, contraste, catalogue, tokens]
requires: []
provides:
  - "ThemeCatalog.All à 15 thèmes, ChronosTheme.Categorie (Pale/Classique/Vive)"
  - "ContrasteWcag.Luminance / Ratio (pur)"
  - "Epuise calculé (plus petit Lerp(piste, graduation) >= 3:1 contre le disque opaque)"
  - "Tokens BrushTokens() : TickReset, Epuise, CadranTuile, CadranAttente, PlaqueTexte, PlaqueFilet, PlaqueHachure + replis statiques minuit"
affects: [39-02 (groupes des réglages), 39-03 (cadrans thémés), 39-04 (Historique)]
tech-stack:
  added: []
  patterns:
    - "Contraste WCAG mesuré contre le disque OPAQUE (Hex(disc)), jamais FondCadran (translucide E6)"
    - "Boucle à compteur entier pas = 18..100 (pas de t += 0.01) ; comparaison >= 3.0 sans arrondi"
    - "Pinceau de hachure construit et gelé en C# (DrawingBrush) plutôt qu'en ressource locale"
key-files:
  created:
    - src/Chronos/Theming/ContrasteWcag.cs
    - src/Chronos/Theming/CategorieTheme.cs
  modified:
    - src/Chronos/Theming/ChronosTheme.cs
    - src/Chronos/Resources/DesignTokens.xaml
    - tests/Chronos.Tests/ThemingTests.cs
    - tests/Chronos.Tests/SessionStylesBindingTests.cs
    - tests/Chronos.Tests/ReglagesWindowTests.cs
decisions:
  - "Catalogue rangé par famille (Classique avec minuit en tête, puis Pâle, puis Vive) ; le regroupement d'affichage suit l'ordre de l'enum"
  - "Neutre (utilization inconnue) volontairement inchangé : le nouvel épuisé en devient presque indiscernable (contraste 1,01 à 1,8), écart laissé à la revue visuelle"
  - "PlaqueTexte = disque × 0,5 (tronqué, opaque) ; PlaqueFilet / PlaqueHachure en dérivent (alpha 0x33 / 0x55)"
metrics:
  duration: "~5 min"
  completed: 2026-10-03
  tasks: 3
  files: 7
---

# Phase 39 Plan 01 : catalogue de 15 thèmes, gris épuisé à 3:1 et tokens de cadran

Le catalogue passe de 9 à 15 thèmes répartis en trois familles : Pâle (6), Classique (5) et Vive (4). Le gris « épuisé » est maintenant calculé dans `From()` : c'est le plus petit mélange piste → graduation qui atteint un contraste WCAG d'au moins 3:1 contre le disque opaque. Les rampes de Néon et d'Aurore sont corrigées. Sept pinceaux sont ajoutés à `BrushTokens()` pour les plans 02 à 04, avec leurs replis statiques.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | RED : invariants du catalogue, palettes exactes, contraste, teinte, minimalité, tokens, replis ; littéraux 9 / 72 / 4 × 9 remplacés | c8dd4be |
| 2 | GREEN : `ContrasteWcag`, `CategorieTheme`, `EpuiseLisible`, catalogue de 15 thèmes | 6f1bb83 |
| 3 | Tokens `TickReset`, `Epuise`, `CadranTuile`, `CadranAttente`, `PlaqueTexte`, `PlaqueFilet`, `PlaqueHachure` + replis dans `DesignTokens.xaml` | 8809fa1 |

## Vérification

- `dotnet build src/Chronos/Chronos.csproj -c Debug` : 0 avertissement, 0 erreur.
- `dotnet test` (suite complète) : 1743/1743 réussis, gardes de doctrine comprises.
- Ancrages confirmés par les tests : minuit épuisé `#63626C`, Lave `#7C5844` (3,0027:1, cas limite), Forêt `#758079` ; tokens de minuit `#201F26`, `#6EC9C8D2`, `#0B0A0D`, `#330B0A0D`.

## Écarts au plan

### Corrections automatiques

**1. [Règle 3 - Blocage] `using Chronos.Theming;` ajouté à `ReglagesWindowTests.cs`**
- **Constaté pendant :** tâche 1
- **Problème :** le remplacement de `9` par `ThemeCatalog.All.Count` ne compilait pas, car `ThemeCatalog` n'était pas importé dans ce fichier.
- **Correction :** ajout de la directive `using`.
- **Commit :** c8dd4be

### Remarques

- Critère `grep -c "ChronosTheme.From(" … == 15` : le grep renvoie 16, parce qu'il compte aussi la ligne de déclaration `public static ChronosTheme From(`. Il y a bien 15 appels dans le catalogue, et `From("minuit"` est le premier.
- Le constructeur de `MainViewModel` utilisé dans `ReglagesWindow_se_construit…` compilait sans changement (aucun alignement requis).

## Effet de bord à surveiller (Pitfall 1)

Le nouveau gris épuisé est maintenant **presque identique à `Neutre`** (utilization inconnue, `Scale(graduation, 0,5)`) sur la plupart des thèmes : le contraste entre les deux va de 1,01 à 1,8. L'orchestrateur a décidé de garder `Neutre` inchangé. L'écart est laissé à la revue visuelle du cycle.

## Suites pour les plans suivants

- `HistoGris` et le gris figé de `UtilizationToBrushConverter` ne sont pas encore reliés au token `Epuise` (plan 04).
- `TickReset` est maintenant thémé dans `BrushTokens()`. Les vues qui le lisent en `DynamicResource` le suivent automatiquement. Les cadrans alternatifs (plan 03) doivent encore abandonner leurs couleurs fixes.

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND: src/Chronos/Theming/ContrasteWcag.cs, src/Chronos/Theming/CategorieTheme.cs
- FOUND: c8dd4be, 6f1bb83, 8809fa1
