# Inventaire cycle 2 — thèmes (R6) et Historique (R4)

Agent Explore, lecture seule, 2026-10-03. Chemins relatifs à `src/Chronos/`.

## A. Thèmes
- `Theming/ChronosTheme.cs` : `From(key, name, disc, track, tick, ink, green, amber, red)` (l.98-123) dérive tout le reste
  (Épuisé = Lerp(piste, graduation, 0,18) ; texte secondaire = texte × 0,66 ; pistes assombries). Catalogue l.144-166,
  défaut `minuit`, `ByKey` tolérant.
- `DesignTokens.xaml` : `TickReset #F4F2EC` **hors `BrushTokens()`** (ne suit pas le thème) ; palette des fenêtres
  secondaires (Réglages, Historique) **toujours sombre** ; `HistoGris #5A5960` statique (gris épuisé de l'Historique
  ne suit pas le thème) ; `UtilizationToBrushConverter.cs:17` gris figé.
- Application : cadran (`MainWindow.xaml.cs:37-38,93-95`), widget sessions, Historique (thème lu **une seule fois** à
  l'ouverture), Réglages (seulement les pinceaux du cadran).
- Section Thème : `ReglagesWindow.xaml:738-798`, `WrapPanel` **plat**, vignettes 88 × 76 ; `ThemeChoice` sans catégorie.
- Tests : `ThemingTests.cs:26-31` **compte exactement 9 thèmes** ; `Alerte` = ambre ≠ rouge (`:51`) ;
  `SessionStylesBindingTests` parcourt tous les thèmes.

### Les 9 thèmes (tous **sombres**)
| Clé | disque | piste | grad. | texte | vert | ambre | rouge | épuisé |
|---|---|---|---|---|---|---|---|---|
| minuit | #16151B | #2A2932 | #C9C8D2 | #F4F2EC | #7BB13C | #EFA23A | #D8503A | #47464F |
| ardoise | #1B2027 | #2C333D | #CBD3DE | #EEF2F6 | #5FB39A | #E0A94E | #E06B5A | #49505A |
| nord | #2E3440 | #3B4252 | #D8DEE9 | #ECEFF4 | #A3BE8C | #EBCB8B | #BF616A | #575E6D |
| neon | #0E0A1F | #241B3A | #7DF9FF | #E6E1FF | #38E8C6 | #B14BFF | #FF2E97 | #34435D |
| aurore | #0E1726 | #1D2B44 | #BFE3FF | #EAF2FF | #4FD1C5 | #6D8CF0 | #C86BE0 | #3A4C66 |
| ambre | #1E1712 | #33271C | #EAD9B8 | #F6ECD9 | #E4B24A | #E07E3C | #D24A3A | #544738 |
| moka | #1E1E2E | #313244 | #BAC2DE | #CDD6F4 | #A6E3A1 | #FAB387 | #F38BA8 | #4A4C60 |
| roseraie | #191724 | #26233A | #B3AECC | #E0DEF4 | #9CCFD8 | #F6C177 | #EB6F92 | #3F3C54 |
| foret | #2D353B | #3A454A | #A6B0A0 | #D3C6AA | #A7C080 | #DBBC7F | #E67E80 | #4D5859 |

- Néon et Aurore : rampe **pas vert → rouge** (Aurore sans aucun rouge).
- Gris épuisé : contraste 1,7-2,1 contre le fond (très faible) ; rouge de Nord 3,05.
- **Aucun thème clair** ; `From()` inadapté à un fond clair (secondaire assombri, pistes assombries, `TickReset` fixe).

### Classement proposé (Pâle = désaturé/pastel)
Pâle : Nord, Forêt, Moka, Roseraie · Classique : Minuit, Ardoise, Ambre chaud · Vive : Néon, Aurore.

### Palettes nouvelles proposées par l'inventaire
| Cat. | Nom | disque | piste | grad. | texte | vert | ambre | rouge |
|---|---|---|---|---|---|---|---|---|
| Pâle (clair) | Brume | #EEF1F4 | #D9DEE5 | #5B6573 | #1F2630 | #3F8F5C | #B97A1E | #B83C3F |
| Pâle (clair) | Lin | #F5F0E6 | #E4DCCD | #6E6252 | #2A241C | #5A8C2C | #B5751A | #B13A2A |
| Pâle | Sauge | #262B28 | #343B37 | #AEB8B0 | #DCE3DD | #8FB996 | #D9C27E | #D08A7E |
| Classique | Graphite | #121314 | #26282B | #C4C7CC | #F2F3F5 | #6DBE45 | #F0A830 | #E04B3C |
| Classique | Marine | #0F1A2A | #1E2D44 | #B9C9DE | #EAF0F7 | #5DBB7A | #F2B544 | #E25C4F |
| Classique | Café | #1C1714 | #2E2621 | #D8CBBE | #F3ECE4 | #8DBF5A | #E9A23B | #D9534A |
| Vive | Synthwave | #140B24 | #2A1745 | #9AE6FF | #F5EEFF | #2BFF88 | #FFD000 | #FF2D55 |
| Vive | Tropique | #06201F | #0E3A37 | #9FF5E6 | #E9FFFB | #00E08A | #FFC400 | #FF4D3D |
| Vive | Lave | #1A0E0A | #33190F | #FFC9A3 | #FFF1E6 | #7CFF4F | #FFB000 | #FF3B1F |
Brume et Lin exigent une variante claire de `From()` + `TickReset` dans les pinceaux du thème.

## B. Historique
### Suppression de Simplifié et Tuiles
- Enum + propriété `HistoriqueStyleSemaine` (`ChronosSettings.cs:24-27,119-121`), VM (`HistoriqueViewModel.cs:73,178-197`),
  textes (`TextesHistorique.cs:34,36-39,70-76`), `Rendering/Historique/Tuiles5h.cs`, `Controls/Historique/PisteFenetres5h.cs`,
  variante `SemaineHebdoSeul` de `PisteNiveau`, grilles Simplifié / Tuiles de `VueSemaineView.xaml:372-602`, sélecteurs
  (`HistoriqueWindow.xaml:119-169,243-267` ; `ReglagesWindow.xaml:218,712-732` — garder « Aussi : double-clic »),
  6 tokens `HistoHauteur*Simplifie/*Tuiles`, README §« Trois styles » (l.123-137).
- Tests : `Tuiles5hTests.cs` supprimé ; adaptations dans `GardeTokensHistoriqueTests`, `GardeDocumentationHistoriqueTests:70-71`,
  `VueSemaineBindingTests`, `HonneteteHistoriqueTests`, `PistesHistoriqueTests`, `HistoriqueBindingTests`,
  `HistoriqueViewModelTests`, `ReglagesBindingTests:271-296`, `TextesHistoriqueTests`, `VueJourBindingTests`, `SettingsServiceTests`.

### PIÈGE de lecture des réglages
`SettingsService.Load()` (l.38-53) renvoie **`new ChronosSettings()` entier** sur `JsonException`. Garder la propriété
avec un enum réduit à `{ Pistes }` ⇒ `"Tuiles"` lève ⇒ **tous les réglages remis à zéro puis réécrits** (perte de
`InnerStatusLineCommand`, thème, coin…). **Supprimer la propriété** (membre inconnu ignoré, déjà testé
`SettingsServiceTests.cs:208-224`) + test de non-régression. Même piège latent pour `CadranStyle`, `SessionStyle`,
`SectionReglages`.

### Plein écran — état et freins
- Fenêtre `WindowStyle=None`, `CanResize`, 920 × 610, min 760 × 480, **Échap = Fermer** (l.189), en-tête fixe 92.
- Pistes `OnRender` sur `ActualWidth/Height` : **la largeur s'étire déjà** ; aucun texte dans les pistes.
- Freins : hauteurs de piste fixes en `StaticResource` (Semaine 150/72/72/12, Jour 190/64/64/12, 4 sem. 250) dans des
  `ScrollViewer` ⇒ en agrandi ≈ 380 DIP utilisés, reste vide ; polices `HistoCorps*` 8,5-16 fixes ; colonnes 96/72/140 ;
  épaisseurs fixes, pointillé `{3,2}` en dur ; marges en chiffres non gardés ; tests qui figent 150/72.
- Piste suggérée : en plein écran, rangées en hauteurs proportionnelles (`150*`, `72*`…), défilement coupé ; polices et
  hauteurs en `DynamicResource` avec dictionnaire « plein écran » ; éviter Viewbox global ; Échap quitte d'abord le
  plein écran. Précédent d'agrandissement : `ReglagesWindow.xaml:521-532`.
