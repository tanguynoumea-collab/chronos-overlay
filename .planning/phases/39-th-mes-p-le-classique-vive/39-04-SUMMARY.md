---
phase: 39-th-mes-p-le-classique-vive
plan: 04
subsystem: theming
tags: [wpf, theming, historique, dynamicresource, gris-epuise]
requires:
  - "39-01 : token Epuise dans BrushTokens() + repli statique #63626C dans DesignTokens.xaml"
  - "38-06 : HistoriqueWindow recopie BrushTokens() dans la fenêtre et les trois vues"
provides:
  - "Propriété Gris de PisteNiveau (Jour, Semaine) et PisteQuatreSemaines liée à {DynamicResource Epuise}"
  - "UtilizationToBrushConverter : Epuise = ThemeCatalog.Default.Epuise"
  - "Tests Le_gris_epuise_de_l_historique_suit_le_theme / Le_gris_epuise_des_quatre_semaines_suit_le_theme (tokens de Lave)"
affects: [40, 43]
tech-stack:
  added: []
  patterns:
    - "Seule la propriété Gris des pistes passe sur Epuise ; HistoGris reste le token figé de la hachure, des bordures de trou et du trait repère"
key-files:
  created: []
  modified:
    - src/Chronos/Converters/UtilizationToBrushConverter.cs
    - tests/Chronos.Tests/UtilizationToBrushConverterTests.cs
    - src/Chronos/Views/Historique/VueJourView.xaml
    - src/Chronos/Views/Historique/VueSemaineView.xaml
    - src/Chronos/Views/Historique/VueQuatreSemainesView.xaml
    - tests/Chronos.Tests/VueJourBindingTests.cs
    - tests/Chronos.Tests/VueSemaineBindingTests.cs
    - tests/Chronos.Tests/VueQuatreSemainesBindingTests.cs
decisions:
  - "Voie retenue : Gris seul sur Epuise, HistoGris (#5A5960) conservé ; aucun repli vers HistoGris n'a été nécessaire"
  - "Le fantôme S-1, les blocs de saut et les fantômes S-1..S-3 de la vue 4 semaines prennent aussi le gris du thème (conséquence voulue de D-34-01)"
requirements-completed: [THM-03, THM-04]
metrics:
  duration: "~10 min"
  completed: 2026-10-03
  tasks: 2
  files: 8
---

# Phase 39 Plan 04 : le gris épuisé de l'Historique suit le thème

Dans l'Historique, la bande « épuisée » des pistes prend maintenant le gris épuisé lisible du thème actif. Elle était figée sur `#5A5960`. Pour minuit, ce gris vaut `#63626C`, et `#7C5844` pour Lave. Seule la propriété `Gris` de `PisteNiveau` (vues Jour et Semaine) et de `PisteQuatreSemaines` lit `{DynamicResource Epuise}`. `HistoGris` reste à `#5A5960` pour la hachure, les bordures de trou et le trait repère. Le convertisseur `UtilizationToBrushConverter`, que plus aucun XAML n'utilise, reprend `ThemeCatalog.Default.Epuise`.

## Tâches

| # | Tâche | Commit |
|---|-------|--------|
| 1 | Convertisseur : `Epuise = Frozen(ThemeCatalog.Default.Epuise)`, test aligné (RED vérifié : 2 échecs, puis vert) | 85e4393 |
| 2 | `Gris="{DynamicResource Epuise}"` dans les 3 vues, attentes `.Gris` sur `ThemeCatalog.Default.Epuise`, deux tests avec les tokens de Lave (RED vérifié : 5 échecs, puis vert) | bea88ac |

## Vérification

- `dotnet build src/Chronos/Chronos.csproj` en Debug et en Release : 0 avertissement, 0 erreur.
- Tests filtrés de la tâche 2 (vues, HistoriqueBindingTests, GardeTokensHistoriqueTests, HonneteteHistoriqueTests, PistesHistoriqueTests, PleinEcranVuesTests) : 91 sur 91.
- `dotnet test` (suite complète) : 1761 sur 1761 (1759 + les 2 nouveaux tests).
- `GardeTokensHistoriqueTests.cs` et `HonneteteHistoriqueTests.cs` n'ont pas été modifiés. `HistoGris` est toujours présent une seule fois dans `DesignTokens.xaml`, à `#5A5960`.
- `HistoriqueWindow.xaml.cs` recopie toujours `BrushTokens()` dans `VueSemaine`, `VueJour` et `VueQuatreSemaines` (grep = 1). Le fichier n'a pas été modifié.
- `TraitReset` reste `#F4F2EC` pour minuit (les assertions existantes passent).

## Écarts au plan

### Remarques

- **Critère « VueSemaineView.xaml >= 3 »** : la phase 38 a réécrit la vue Semaine, qui ne contient plus qu'**un seul** `hc:PisteNiveau`. `PisteFenetres5h` n'existe plus. On compte donc 1 `Gris="{DynamicResource Epuise}"` et 0 `Gris="{StaticResource HistoGris}"`, ce qui veut dire que toutes les pistes de la vue sont couvertes. `BordTrouArrete="{StaticResource HistoGris}"` n'a pas bougé (1 avant, 1 après).
- J'ai corrigé l'en-tête de commentaire de `VueQuatreSemainesView.xaml` (D-35-13), qui disait « S-3 / S-2 / S-1 en HistoGris ». Il dit maintenant « dans le gris épuisé du thème (Epuise, phase 39) ». Il n'y a aucun changement de code.
- Le plan prévoyait un seul commit par tâche. La phase RED a donc été vérifiée sans commit séparé.

Il n'y a eu aucune correction automatique et aucun repli vers `HistoGris`.

## Rendus à vérifier à la revue visuelle (§9.5)

- **Épuisé ≈ neutre** : le gris épuisé calculé est maintenant presque identique à `Neutre` (utilization inconnue), avec un contraste de 1,01 à 1,8 selon le thème (voir 39-01). L'orchestrateur a décidé de **garder le gris neutre inchangé**. Dans l'Historique aussi, une bande épuisée et une donnée inconnue risquent maintenant de se confondre visuellement. À trancher à la revue.
- **Fantôme S-1 et blocs de saut** (vue Semaine), **fantômes S-1..S-3** (vue 4 semaines) : ils sont peints avec la brosse `Gris` et prennent donc aussi le gris du thème. C'est voulu (D-34-01 : « HistoGris = gris épuisé du cadran »). Sur Lave, ils deviennent brun-gris `#7C5844`.
- La hachure, les bordures de trou « Chronos arrêté » et le trait repère des 4 semaines restent en `#5A5960` sur tous les thèmes. Les deux gris se côtoient donc sur les thèmes chauds.

## Known Stubs

Aucun.

## Self-Check: PASSED

- FOUND: src/Chronos/Converters/UtilizationToBrushConverter.cs, src/Chronos/Views/Historique/VueJourView.xaml, src/Chronos/Views/Historique/VueSemaineView.xaml, src/Chronos/Views/Historique/VueQuatreSemainesView.xaml
- FOUND: 85e4393, bea88ac
