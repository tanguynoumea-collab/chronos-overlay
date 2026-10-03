# Phase 38 : Historique — Pistes seul et plein écran — Research

**Researched:** 2026-10-03
**Domain:** WPF (.NET 8, `net8.0-windows`) — suppression d'un réglage persisté et de deux grilles XAML ; plein écran manuel d'une
fenêtre `WindowStyle=None` + `WindowChrome` sur le moniteur courant ; bascule de tailles par échange de `ResourceDictionary`
(`DynamicResource`) ; lignes de `Grid` proportionnelles plafonnées.
**Confidence:** HIGH sur l'inventaire du code (tout lu dans le dépôt) et sur les mécanismes de `Grid` et de ressources (source
dotnet/wpf, docs officielles) ; MEDIUM sur la barre des tâches couverte (détection automatique du shell, à constater à la main) et
sur les coordonnées DIP en DPI mixte.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Tout l'inventaire de suppression de `.zeus/reports/cycle2/inventaire-themes-historique.md` § B1 s'applique (code, XAML,
  tokens, tests, README § « Trois styles »). Carte des réglages : garder « Aussi : double-clic au centre du cadran » tel quel
  (il sera reformulé en phase 42, quand le geste couvrira la silhouette).
- **Supprimer la propriété** `HistoriqueStyleSemaine` (pas réduire l'enum) ; test : un ancien `settings.json` avec `"Tuiles"`
  se lit sans perte (s'appuie sur la phase 36).
- Plein écran (plan § 4.2, chiffres contractuels) :
  - bouton « ⛶ Plein écran » à droite de la ligne de fraîcheur (à la place du sélecteur), raccourci F11 ;
  - bornes du **moniteur courant** posées à la main (pas `WindowState.Maximized`), en DIP, barre des tâches couverte,
    restauration exacte de la géométrie précédente ; la géométrie enregistrée reste celle du mode normal ;
  - rangées des pistes en hauteurs proportionnelles (Semaine 150*/72*/72*, Jour 190*/64*/64*, 4 semaines Niveau 250*),
    plafonds Niveau 520, Rythme/Tokens 240 ; Couverture et annotations fixes ; plus de défilement ;
  - dictionnaire `PleinEcran` en `DynamicResource` : corps 8,5→11,5, 9→12, 9,5→13, 10,5→14, 11→15, 11,5→15,5, 14→18, 16→21 ;
    libellés 96→128 ; légende de droite 72→96 ; escalier 2,2→3 ; premier plan 2,4→3,2 ; tiret de reset 8→11 ;
  - bouton « ⤢ Quitter le plein écran · Échap » toujours visible ; Échap quitte d'abord le plein écran puis ferme au second
    appui (fonction pure testée) ; F11 bascule.
- Honnêteté inchangée à toutes les tailles (trous, libellés, pied). Valeurs via `DesignTokens.xaml` ; les tests qui figent
  150/72 restent valides en mode normal ; tests dédiés au plein écran.

### Claude's Discretion
Mécanisme de bascule des hauteurs (styles / triggers / converter) ; emplacement de la fonction pure Échap.

### Deferred Ideas (OUT OF SCOPE)
None
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| HIS-09 | Un seul style, Pistes : sélecteurs (fenêtre + carte des réglages), grilles Simplifié/Tuiles, piste Fenêtres 5 h, tokens et textes retirés ; propriété `HistoriqueStyleSemaine` supprimée ; ancien réglage lu sans perte | § « Inventaire de suppression » (liste fichier:ligne vérifiée), § « Runtime State Inventory », Pitfall 1 et 7 |
| HIS-10 | Plein écran général (bouton + F11) sur l'écran courant ; pistes proportionnelles plafonnées ; dictionnaire `PleinEcran` ≈ ×1,35 ; plus de défilement ; honnêteté identique | Patterns 1 à 4 (échange de dictionnaire, lignes `*` + `MaxHeight`, `ScrollViewer` désactivé, bornes du moniteur), liste des clés à passer en `DynamicResource`, Pitfalls 2 à 6 |
| HIS-11 | Sortie par bouton toujours visible, F11, Échap à deux niveaux ; position et taille d'avant restaurées | Pattern 5 (état dans le VM, fonction pure `Echap`, `KeyBinding` F11/Échap), Pattern 4 (mémoire de géométrie), Pitfall 8 (`EnregistrerGeometrie` en plein écran) |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- C# / .NET 8 (`net8.0-windows`) / WPF / MVVM strict (CommunityToolkit.Mvvm : `[ObservableProperty]`, `[RelayCommand]`) / DI.
  Dossiers Models / Views / ViewModels / Services.
- Rendu XAML pur, **aucune dépendance native** (P/Invoke vers les DLL de l'OS déjà pratiqué : `user32`, `dwmapi`, `Shcore`).
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin.
- Robustesse : parsing tolérant ; une source absente n'est jamais un crash.
- Honnêteté des chiffres : ne jamais présenter une estimation comme exacte (ici : trous, annotations et pied identiques en plein écran).
- UI et commentaires **en français**.
- Pas de `Assembly.Location`, pas de `PublishTrimmed`.
- Workflow GSD : modifications du dépôt seulement dans une commande GSD (`/gsd:execute-phase`).
- Contraintes milestone (ROADMAP) : les tests xUnit restent verts, **0 warning Debug et Release** ; les tests des éléments supprimés
  partent avec eux ; gardes de doctrine, `ServicesLayerPurityTests`, `CompositionRootTests` intacts.
- Routage : projet desktop .NET → orchestrateur ZEUS (pas ATHENA). Le plan de design `.zeus/DESIGN_PLAN_CYCLE2.md` § 4 et § 7 est
  contractuel (pas d'UI-SPEC).

## Summary

La phase a deux moitiés indépendantes. **HIS-09** est une suppression mécanique bien bornée : l'inventaire § B1 est exact (vérifié
fichier par fichier, voir la table plus bas) ; le seul piège réel — `SettingsService.Load()` tout-ou-rien sur un enum inconnu — ne
s'applique **pas** à une propriété supprimée : `SettingsService` sérialise avec `JsonUnmappedMemberHandling.Skip` (commentaire
`ChronosSettings.cs:42`), et un membre inconnu est déjà ignoré (test existant `Valeur_d_enum_supprimee_invalide_est_ignoree`,
`SettingsServiceTests.cs:208-224`). La phase 36 reste un prérequis d'ordre (ROADMAP), mais la non-régression de HIS-09 tient
même sans elle ; le test témoin doit le prouver.

**HIS-10/11** se règlent entièrement côté vue avec trois mécanismes WPF standards, sans `Viewbox` ni code de mise en page maison :
(1) un dictionnaire `PleinEcran` **fusionné dans les ressources de la fenêtre ET de chacune des trois vues** (chaque vue fusionne
`DesignTokens.xaml` elle-même ; un dictionnaire posé seulement sur la fenêtre serait masqué — c'est exactement le piège déjà
rencontré pour les pinceaux du thème, `HistoriqueWindow.xaml.cs:37-46`) ; (2) des `RowDefinition` dont `Height` et `MaxHeight`
viennent de `DynamicResource` (`Auto`/∞ en normal, `150*`/520 en plein écran) — l'algorithme d'étoiles de `Grid` (par défaut
`ResolveStarMaxDiscrepancy`) plafonne une ligne à son `MaxHeight` et **redistribue** le reste aux autres lignes étoilées ;
(3) `ScrollViewer.VerticalScrollBarVisibility` en `DynamicResource` (`Auto` → `Disabled`) : désactivé, le `ScrollViewer` passe une
hauteur finie au contenu, sans quoi les étoiles se comporteraient en `Auto`.

La géométrie suit le motif déjà présent dans le dépôt (`MonitorFromWindow` + `GetMonitorInfo` dans `OverlayController` et
`ReglagesWindow`) : on lit `rcMonitor` (pas `rcWork`, la barre des tâches doit être couverte), on convertit en DIP par
`PresentationSource.CompositionTarget.TransformFromDevice`, on mémorise `Left/Top/Width/Height` d'avant, on pose les quatre
propriétés. L'état « plein écran » vit dans le VM (`EstPleinEcran`, commandes `BasculerPleinEcran` et `Echap`) ; la décision d'Échap
est une fonction pure ; la fenêtre observe le VM et applique géométrie + dictionnaire.

**Primary recommendation:** Tout piloter par l'échange d'un seul dictionnaire `PleinEcran` construit en C# à partir de clés
`*PleinEcran` de `DesignTokens.xaml` (les étoiles dérivées des hauteurs normales, donc proportions garanties par construction),
fusionné/défusionné dans 4 propriétaires (fenêtre + 3 vues) ; espacements entre pistes sortis dans des lignes fixes pour que les
proportions et les plafonds portent exactement sur les pistes.

## Standard Stack

Aucune bibliothèque nouvelle. Tout est dans le framework et dans le dépôt.

### Core
| Élément | Version | Rôle | Pourquoi |
|---------|---------|------|----------|
| WPF `Grid` (étoiles + `MaxHeight`) | net8.0-windows | Partage proportionnel plafonné | `ResolveStarMaxDiscrepancy` (défaut) : « Allocate to *-defs hitting their min or max constraints, before allocating to other *-defs » — plafonne puis redistribue |
| `ResourceDictionary.MergedDictionaries` + `DynamicResource` | net8.0-windows | Bascule des tailles en bloc | Ajouter/retirer un dictionnaire fusionné invalide les références dynamiques de l'arbre (mécanisme des changements de thème) |
| `PresentationSource.CompositionTarget.TransformFromDevice` | net8.0-windows | Pixels physiques → DIP | API officielle ; tient compte du DPI courant de la fenêtre (manifeste `PerMonitorV2`, `app.manifest:5`) |
| `MonitorFromWindow` / `GetMonitorInfo` | user32 (déjà déclarés, `Interop/NativeMethods.cs:45-51`) | Bornes du moniteur courant | Déjà utilisés par `OverlayController.cs:71-73` et `ReglagesWindow.xaml.cs:183-186` |
| CommunityToolkit.Mvvm | 8.4.2 (déjà référencé) | `[ObservableProperty] EstPleinEcran`, `[RelayCommand]` | Convention du projet |
| xUnit 2.9.2 + Xunit.StaFact 1.1.11 | déjà référencés | `[Fact]` purs, `[WpfFact]` Measure/Arrange | Motif des tests existants (`VueSemaineBindingTests.MettreEnPage`) |

### Alternatives Considered
| Au lieu de | On pourrait | Pourquoi non |
|------------|-------------|--------------|
| Échange de dictionnaire | `DataTrigger` sur `EstPleinEcran` dans chaque style/ligne | Les vues devraient connaître le flag ; les valeurs locales (`Height="…"` en attribut) battent les triggers de style (précédence) ; deux mécanismes au lieu d'un |
| Lignes `*` + `MaxHeight` | Converter qui calcule les hauteurs | Réimplémente l'algorithme de `Grid` ; la redistribution au plafond est déjà native |
| `Viewbox` global | — | Exclu par l'inventaire (« éviter Viewbox global ») : étire le texte en continu, contredit « par paliers » |
| `WindowState.Maximized` | — | Exclu par décision verrouillée (déborde sur une fenêtre sans bordure ; `WM_GETMINMAXINFO` de `ReglagesWindow` le borne à `rcWork`) |
| Bornes en DIP (`Left/Top/Width/Height`) | `SetWindowPos` en pixels physiques | Repli seulement si le constat en DPI mixte montre un décalage (voir Pitfall 4) |

**Installation :** aucune.

## Inventaire de suppression (HIS-09) — vérifié dans le dépôt

| Fichier | Lignes | Action |
|---------|--------|--------|
| `src/Chronos/Services/ChronosSettings.cs` | 24-27 (enum + doc), 119-121 (propriété) | Supprimer enum `HistoriqueStyleSemaine` et propriété |
| `src/Chronos/ViewModels/Historique/HistoriqueViewModel.cs` | 73 (`_style = lus.HistoriqueStyleSemaine`), 178-197 (`Style`, `IsStyle*`, `OnStyleChanged`, `ChoisirStyle`) | Supprimer |
| `src/Chronos/Text/TextesHistorique.cs` | 34 (`PisteFenetres5h`), 36-39 (`StylePrefixe`, `StylePistes`, `StyleSimplifie`, `StyleTuiles`), 70-76 (`LibelleStyle`) | Supprimer ; **ajouter** `BoutonPleinEcran = "⛶ Plein écran"`, `BoutonQuitterPleinEcran = "⤢ Quitter le plein écran · Échap"` |
| `src/Chronos/Rendering/Historique/Tuiles5h.cs` | fichier | Supprimer |
| `src/Chronos/Controls/Historique/PisteFenetres5h.cs` | fichier | Supprimer |
| `src/Chronos/Controls/Historique/PisteNiveau.cs` | 17 (membre `SemaineHebdoSeul`), 102 (`case`) | Supprimer la variante |
| `src/Chronos/Views/Historique/VueSemaineView.xaml` | 13-14 (commentaire D-34-27), 99-102 (style `GrilleStyle`), 258-266 (`Grid.Style` DataTrigger `IsStylePistes` de la grille A), 372-602 (grilles B et C) | Supprimer ; la grille `StylePistes` devient toujours visible (renommage libre, mais attention aux tests qui cherchent `Name.StartsWith("Style")`) |
| `src/Chronos/Views/Historique/HistoriqueWindow.xaml` | 119-169 (styles `Puce*`), 243 (commentaire), 257-267 (`SelecteurStyle`), xmlns `svc` (l.8) s'il n'a plus d'usage | Supprimer ; remplacer par le bouton plein écran |
| `src/Chronos/Views/Reglages/ReglagesWindow.xaml` | 218 (style `PuceStyleHistorique`), 712-729 (« Style de la vue Semaine : » + `UniformGrid`) | Supprimer ; **garder** l.730 « Aussi : double-clic au centre du cadran » et le `StackPanel` qui l'entoure (visibilité `AfficherOuvrirHistorique`) |
| `src/Chronos/Resources/DesignTokens.xaml` | 90-96 (6 tokens `HistoHauteur*Simplifie` / `*Tuiles`) | Supprimer |
| `README.md` | 123-137 (« ### Trois styles pour la Semaine », tableau, puce « Fenêtres 5 h », mentions « en Pistes et Simplifié ») | Réécrire en description d'un style unique + paragraphe « Plein écran (F11, Échap) » |
| Tests | `Tuiles5hTests.cs` (supprimé) ; `GardeTokensHistoriqueTests` (table `TaillesHisto`), `GardeDocumentationHistoriqueTests:70-71` (mots « Fenêtres 5 h », « Pistes », « Simplifié », « Tuiles »), `VueSemaineBindingTests` (l.39-43 `Monter(style)`, 137-190, 207, 241-252, 304), `HonneteteHistoriqueTests` (l.50-73, 261, 380-393, 460 « Style : »), `PistesHistoriqueTests` (`SemaineHebdoSeul`, `PisteFenetres5h`), `HistoriqueBindingTests` (l.116-129 Échap→Fermer, 156-171 puces), `HistoriqueViewModelTests` (l.140-152), `ReglagesBindingTests:271-296`, `ReglagesWindowTests:318` (`Historique.ChoisirStyle`), `TextesHistoriqueTests` (l.250, 268-274), `VueJourBindingTests`, `SettingsServiceTests` (l.230-268) | Adapter / supprimer ; ajouter le témoin « Tuiles » |

Non concernés malgré un `git grep` positif : `MainViewModel`, `SessionsViewModel`, `MainWindow.xaml`, `SessionsWindow.xaml`
(styles de cadran / de sessions, homonymes `IsStyle*`). `HistoriqueGalerie.cs` et `ScenariosHistorique.cs` ne référencent pas le
style (vérifié). `docs/` : aucune occurrence.

## Architecture Patterns

### Fichiers touchés / créés
```
src/Chronos/
├── Placement/PleinEcranHistorique.cs        # NOUVEAU, pur (aucun type WPF) : ActionEchap Echap(bool), BornesDip(...)
├── Views/Historique/DictionnairePleinEcran.cs # NOUVEAU (WPF) : construit le ResourceDictionary « PleinEcran » depuis les tokens
├── Views/Historique/HistoriqueWindow.xaml(.cs) # bouton, KeyBindings F11/Échap, application géométrie + dictionnaire
├── Views/Historique/Vue{Semaine,Jour,QuatreSemaines}View.xaml # StaticResource → DynamicResource, lignes étoilables
├── ViewModels/Historique/HistoriqueViewModel.cs # EstPleinEcran, BasculerPleinEcranCommand, EchapCommand
├── Resources/DesignTokens.xaml                # tokens *PleinEcran, GridLength de rangées, plafonds, défilement
├── Text/TextesHistorique.cs                   # deux libellés de bouton
└── Interop/NativeMethods.cs                   # + DWMWCP_DONOTROUND = 1
```

### Pattern 1 : dictionnaire `PleinEcran` construit depuis les tokens, fusionné dans 4 propriétaires

**Quoi :** `DesignTokens.xaml` porte les valeurs plein écran sous des clés suffixées (`HistoCorpsTitrePleinEcran` = 21, …). Une
petite classe construit un `ResourceDictionary` dont les **clés sont les clés normales** et les valeurs celles des clés suffixées ;
plus les valeurs non numériques dérivées (étoiles, `NaN`, `Disabled`). La fenêtre l'ajoute à la fin de
`Resources.MergedDictionaries` de la fenêtre et des trois vues, et le retire en sortie.

**Pourquoi 4 propriétaires :** la recherche d'une `DynamicResource` remonte l'arbre ; la vue (`UserControl.Resources`, avec son
`DesignTokens.xaml` fusionné) est consultée **avant** la fenêtre. Un dictionnaire posé sur la fenêtre seule serait masqué dans les
vues — même constat que `HistoriqueWindow.xaml.cs:37-39` pour `Alerte`. Dans un même propriétaire, les dictionnaires fusionnés
sont consultés en ordre inverse : le dernier ajouté gagne. Une instance par propriétaire (construction triviale) évite toute
question de partage multi-propriétaires.

**Clés à rendre `DynamicResource` (toutes les occurrences, attributs ET `Setter Value` des styles locaux) :**

| Clé (normal → plein écran) | Fenêtre | Semaine* | Jour | 4 sem. |
|---|---|---|---|---|
| `HistoCorpsInfime` 8,5 → 11,5 | 0 | 2 | 2 | 2 |
| `HistoCorpsLegende` 9 → 12 | 0 | 1 | 1 | 2 |
| `HistoCorpsMini` 9,5 → 13 | 1 | 1 | 1 | 1 |
| `HistoCorpsPetit` 10,5 → 14 | 5 | 1 | 1 | 0 |
| `HistoCorpsMoyen` 11 → 15 | 4 | 0 | 0 | 0 |
| `HistoCorpsNormal` 11,5 → 15,5 | 0 | 0 | 0 | 0 (entrée du dictionnaire quand même : contrat) |
| `HistoCorpsGrand` 14 → 18 | 2 | 0 | 0 | 0 |
| `HistoCorpsTitre` 16 → 21 | 1 | 0 | 0 | 0 |
| `HistoLargeurLibelles` 96 → 128 | 0 | 2 (styles) | 5 | 4 |
| `HistoLargeurLegendeDroite` 72 → 96 | 0 | 1 (style) | 2 | 0 |
| `HistoEpaisseurEscalier` 2,2 → 3 | 0 | 1 | 0 | 2 |
| `HistoEpaisseurPremierPlan` 2,4 → 3,2 | 0 | 1 | 1 | 0 |
| `HistoLongueurTiretReset` 8 → 11 | 0 | 1 | 0 | 1 |
| `HistoHauteurNiveauPistes` / `RythmePistes` / `TokensPistes` 150/72/72 → `NaN` | | 1 chacune | | |
| `HistoHauteurNiveauJour` / `RythmeJour` / `TokensJour` 190/64/64 → `NaN` | | | 1 chacune | |
| `HistoHauteurNiveauQuatreSemaines` 250 → `NaN` | | | | 1 |
| `HistoHauteurEnTete` 92 → 124 (voir Open Question 1) | 1 | | | |

\* comptes Semaine **après** suppression des grilles B/C. Les comptes ont été obtenus par `grep -o "StaticResource <clé>}"`.
Les pistes déclarent leurs DP en `AffectsRender` (`PisteBase.cs:55-60`) : un changement d'épaisseur via `DynamicResource`
redessine sans rien ajouter. Le pointillé `{3,2}` est en unités d'épaisseur de plume : il grandit avec le trait.

**Exemple (constructeur) :**
```csharp
// Views/Historique/DictionnairePleinEcran.cs — WPF autorisé ici (pas dans Services/).
internal static class DictionnairePleinEcran
{
    /// <summary>Clés dont la valeur plein écran est le token « clé + PleinEcran » de DesignTokens.xaml (contrat § 4.2).</summary>
    internal static readonly string[] ClesEchelonnees =
    {
        "HistoCorpsInfime", "HistoCorpsLegende", "HistoCorpsMini", "HistoCorpsPetit", "HistoCorpsMoyen", "HistoCorpsNormal",
        "HistoCorpsGrand", "HistoCorpsTitre", "HistoLargeurLibelles", "HistoLargeurLegendeDroite",
        "HistoEpaisseurEscalier", "HistoEpaisseurPremierPlan", "HistoLongueurTiretReset", "HistoHauteurEnTete",
        "HistoPlafondNiveau", "HistoPlafondPiste",
    };
    /// <summary>(clé de rangée, clé de hauteur normale) : l'étoile reprend la hauteur normale → proportions par construction.</summary>
    internal static readonly (string Rangee, string Hauteur)[] Rangees =
    {
        ("HistoRangeeNiveauSemaine", "HistoHauteurNiveauPistes"), ("HistoRangeeRythmeSemaine", "HistoHauteurRythmePistes"),
        ("HistoRangeeTokensSemaine", "HistoHauteurTokensPistes"), ("HistoRangeeNiveauJour", "HistoHauteurNiveauJour"),
        ("HistoRangeeRythmeJour", "HistoHauteurRythmeJour"), ("HistoRangeeTokensJour", "HistoHauteurTokensJour"),
        ("HistoRangeeNiveauQuatreSemaines", "HistoHauteurNiveauQuatreSemaines"),
    };

    internal static ResourceDictionary Construire(ResourceDictionary tokens)
    {
        var d = new ResourceDictionary();
        foreach (var cle in ClesEchelonnees) d[cle] = (double)tokens[cle + "PleinEcran"];
        foreach (var (rangee, hauteur) in Rangees)
        {
            d[rangee] = new GridLength((double)tokens[hauteur], GridUnitType.Star);
            d[hauteur] = double.NaN;                         // la piste s'étire dans sa rangée
        }
        d["HistoDefilementVertical"] = ScrollBarVisibility.Disabled;
        return d;
    }
}
```
`tokens` = une instance de `new ResourceDictionary { Source = new Uri("pack://application:,,,/Chronos;component/Resources/DesignTokens.xaml") }`
(chargement déjà prouvé seul par `GardeTokensHistoriqueTests.Le_dictionnaire_de_tokens_se_charge_seul…`).

**Tokens à ajouter dans `DesignTokens.xaml` :**
```xml
<!-- Mode normal : rangées en Auto (les pistes ont leur hauteur fixe), pas de plafond, défilement automatique. -->
<GridLength x:Key="HistoRangeeNiveauSemaine">Auto</GridLength>   <!-- … ×7 -->
<GridLength x:Key="HistoRangeeEspaceSemaine">6</GridLength>      <!-- espacement entre pistes (ex-HistoMargePiste) -->
<GridLength x:Key="HistoRangeeEspaceJour">8</GridLength>         <!-- ex-Margin="0,8,0,0" de la vue Jour -->
<sys:Double x:Key="HistoPlafondNiveau">Infinity</sys:Double>
<sys:Double x:Key="HistoPlafondPiste">Infinity</sys:Double>
<ScrollBarVisibility x:Key="HistoDefilementVertical">Auto</ScrollBarVisibility>
<!-- Plein écran (DESIGN_PLAN_CYCLE2 § 4.2) : ≈ ×1,35, par paliers. -->
<sys:Double x:Key="HistoCorpsInfimePleinEcran">11.5</sys:Double>   <!-- … une entrée par clé échelonnée -->
<sys:Double x:Key="HistoPlafondNiveauPleinEcran">520</sys:Double>
<sys:Double x:Key="HistoPlafondPistePleinEcran">240</sys:Double>
```
La garde `Les_tokens_de_taille_sont_des_doubles_nommes` compte **tous** les `sys:Double` de `DesignTokens.xaml` : la table
`TaillesHisto` doit perdre les 6 tokens supprimés et gagner les ~18 nouveaux (16 `*PleinEcran` + 2 plafonds normaux). Les
`GridLength` et l'enum ne sont pas comptés : ajouter un test qui les vérifie (type + valeur).

### Pattern 2 : lignes étoilables plafonnées, espacements sortis des lignes de pistes

**Quoi :** chaque rangée de piste a `Height="{DynamicResource HistoRangee…}"` et `MaxHeight="{DynamicResource HistoPlafond…}"` ;
la piste a `Height="{DynamicResource HistoHauteur…}"` (150 en normal, `NaN` = s'étire en plein écran). Les marges basses
`HistoMargePiste` (Semaine) et hautes `Margin="0,8,0,0"` (Jour) deviennent des **lignes d'espacement fixes**.

**Pourquoi sortir les marges :** une marge à l'intérieur d'une ligne étoilée fausse les deux contrats — les proportions porteraient
sur « piste + marge » (156:78:78 au lieu de 150:72:72, ≈ 2 % d'écart à 700 DIP) et le plafond de ligne 520 donnerait une piste de
514. Avec des lignes d'espacement, la ligne étoilée contient exactement la piste : rapport et plafond exacts, testables à 0,5 DIP.
En mode normal, l'`ActualHeight` des pistes ne change pas (hauteur fixe) : les tests 150/72/72/12 et 190/64/64/12 restent valides.

**Pourquoi c'est sûr pour la redistribution :** source dotnet/wpf `Grid.cs` — par défaut (`GridStarDefinitionsCanExceedAvailableSpace`
faux) `ResolveStarMaxDiscrepancy` « allocate[s] to *-defs hitting their min or max constraints, before allocating to other *-defs ».
Rythme/Tokens atteignent 240 avant que Niveau atteigne 520 (240 × 150/72 = 500) : la hauteur libérée retourne à Niveau jusqu'à 520,
puis le reste est laissé vide **en bas** de la grille (aligner la grille des pistes en haut).

**Exemple (Semaine, grille unique) :**
```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>                                                  <!-- 0 annotations : trous, journal ouvert -->
    <RowDefinition Height="{DynamicResource HistoRangeeNiveauSemaine}" MaxHeight="{DynamicResource HistoPlafondNiveau}"/> <!-- 1 NIVEAU -->
    <RowDefinition Height="{StaticResource HistoRangeeEspaceSemaine}"/>              <!-- 2 espacement (ex-marge) -->
    <RowDefinition Height="Auto"/>                                                  <!-- 3 annotations : sauts -->
    <RowDefinition Height="{DynamicResource HistoRangeeRythmeSemaine}" MaxHeight="{DynamicResource HistoPlafondPiste}"/>  <!-- 4 RYTHME -->
    <RowDefinition Height="{StaticResource HistoRangeeEspaceSemaine}"/>
    <RowDefinition Height="{DynamicResource HistoRangeeTokensSemaine}" MaxHeight="{DynamicResource HistoPlafondPiste}"/>  <!-- 6 TOKENS -->
    <RowDefinition Height="{StaticResource HistoRangeeEspaceSemaine}"/>
    <RowDefinition Height="Auto"/>                                                  <!-- 8 libellé permanent -->
    <RowDefinition Height="Auto"/>                                                  <!-- 9 COUVERTURE -->
</Grid.RowDefinitions>
…
<hc:PisteRythme Grid.Row="4" Grid.Column="1" Height="{DynamicResource HistoHauteurRythmePistes}" … />
```
Écrire **toutes** les hauteurs et plafonds par ressource : la garde `InterditXaml` refuse `Height="1…"`, et son motif
`Height="[0-9]` attrape aussi `MaxHeight="520"` (pas de borne de mot). Re-numéroter `Grid.Row` et **`Grid.RowSpan`** (lignes de
jours, `SurcoucheReticule`, `Canvas` d'infobulle).

**4 semaines :** le corps est un `StackPanel` (`VueQuatreSemainesView.xaml:137`) — un `StackPanel` donne une hauteur infinie à ses
enfants, les étoiles n'y ont aucun sens. Le convertir en `Grid` à lignes (annotations `Auto`, NIVEAU
`{DynamicResource HistoRangeeNiveauQuatreSemaines}` + `MaxHeight` `HistoPlafondNiveau`, couvertures `Auto`).

### Pattern 3 : défilement coupé par ressource

```xml
<ScrollViewer Grid.Row="1" x:Name="Defilement" VerticalScrollBarVisibility="{DynamicResource HistoDefilementVertical}"
              HorizontalScrollBarVisibility="Disabled" Focusable="False">
```
Avec `Disabled`, `ScrollContentPresenter` mesure le contenu à la hauteur du viewport (pas à l'infini) : les étoiles se résolvent,
`ScrollableHeight` vaut 0. On **garde** le `ScrollViewer` (les tests le cherchent : `VueSemaineBindingTests:383` `FindName("Defilement")`,
`VueJourBindingTests:209` `Assert.Single(Visibles<ScrollViewer>)`). Attention à la précédence : si on passait par un `DataTrigger`
de style, l'attribut local actuel `VerticalScrollBarVisibility="Auto"` l'emporterait — la ressource dynamique évite ce piège.

Ne pas rendre les lignes étoilées en mode normal « pour simplifier » : un `ScrollViewer` arrange son contenu à
max(désiré, viewport) ; une fenêtre normale haute étirerait alors les lignes en `*` et ouvrirait des trous entre pistes. D'où
`Auto` en normal.

### Pattern 4 : bornes du moniteur courant et mémoire de géométrie

```csharp
// HistoriqueWindow.xaml.cs
private (double X, double Y, double L, double H)? _avantPleinEcran;
private WindowState _etatAvant;

internal void EntrerPleinEcran(RectangleEcran? moniteurInjecte = null)   // seam de test
{
    if (_avantPleinEcran is not null) return;                            // idempotent
    if (WindowState != WindowState.Normal) { /* mémoriser RestoreBounds */ WindowState = WindowState.Normal; }
    _avantPleinEcran = (Left, Top, ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
    var m = moniteurInjecte ?? BornesMoniteurCourantDip();
    if (m is not { } b) { _avantPleinEcran = null; return; }             // pas de moniteur : on ne bascule pas (jamais de crash)
    ResizeMode = ResizeMode.NoResize;                                    // WindowChrome cesse d'offrir les bords de redimensionnement
    Left = b.Gauche; Top = b.Haut; Width = b.Largeur; Height = b.Hauteur; // position d'abord, taille ensuite
    CoinsDwm(NativeMethods.DWMWCP_DONOTROUND);                            // sinon Windows 11 arrondit les 4 coins sur le bureau
    AjouterDictionnaire();
}

internal void QuitterPleinEcran()
{
    if (_avantPleinEcran is not { } g) return;
    RetirerDictionnaire();
    Width = g.L; Height = g.H; Left = g.X; Top = g.Y;                     // taille d'abord, position ensuite
    ResizeMode = ResizeMode.CanResize;
    CoinsDwm(NativeMethods.DWMWCP_ROUND);
    _avantPleinEcran = null;
}

private RectangleEcran? BornesMoniteurCourantDip()
{
    var hwnd = new WindowInteropHelper(this).Handle;
    if (hwnd == IntPtr.Zero) return null;
    var mon = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
    var mi = new NativeMethods.MONITORINFOEX { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
    if (mon == IntPtr.Zero || !NativeMethods.GetMonitorInfo(mon, ref mi)) return null;
    var versDip = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
    return PleinEcranHistorique.BornesDip(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom,
                                         versDip.M11, versDip.M22);   // rcMonitor : barre des tâches COUVERTE (pas rcWork)
}
```
```csharp
// Placement/PleinEcranHistorique.cs — pur, testable en [Fact].
public enum ActionEchap { QuitterPleinEcran, Fermer }
public static class PleinEcranHistorique
{
    public static ActionEchap Echap(bool estPleinEcran) => estPleinEcran ? ActionEchap.QuitterPleinEcran : ActionEchap.Fermer;
    public static RectangleEcran BornesDip(int gauche, int haut, int droite, int bas, double m11, double m22)
        => new(gauche * m11, haut * m22, (droite - gauche) * m11, (bas - haut) * m22);
}
```
`RectangleEcran` existe déjà (`Placement/PlacementHistorique.cs:4`). `Placement/` est déjà le lieu des calculs purs de placement ;
ce n'est pas `Services/` (garde de pureté non concernée, mais rester sans type WPF par cohérence avec `PlacementHistorique`).

**Barre des tâches :** une fenêtre active dont le rectangle égale exactement `rcMonitor` est détectée « plein écran » par le shell,
qui passe la barre des tâches sous elle (doc `ITaskbarList2::MarkFullscreenWindow` : « the Shell depends on its automatic detection
facility »). Pas de `Topmost` (décision D-34-23 conservée). Repli si le constat manuel échoue : `ITaskbarList2.MarkFullscreenWindow(hwnd, true)`
par `[ComImport]` (COM de `shell32`, aucune DLL embarquée).

### Pattern 5 : état dans le VM, Échap à deux niveaux, F11

```csharp
// HistoriqueViewModel
[ObservableProperty] private bool _estPleinEcran;
[RelayCommand] private void BasculerPleinEcran() => EstPleinEcran = !EstPleinEcran;
[RelayCommand] private void Echap()
{
    if (PleinEcranHistorique.Echap(EstPleinEcran) == ActionEchap.QuitterPleinEcran) EstPleinEcran = false;
    else Fermer();
}
```
```xml
<Window.InputBindings>
    <KeyBinding Key="Escape" Command="{Binding EchapCommand}"/>
    <KeyBinding Key="F11" Command="{Binding BasculerPleinEcranCommand}"/>
</Window.InputBindings>
…
<!-- Ligne 3 : Grid à deux colonnes (*, Auto) — la fraîcheur s'enroule au lieu de passer sous le bouton (§ 7). -->
<Button x:Name="BoutonPleinEcran" Grid.Column="1" Command="{Binding BasculerPleinEcranCommand}">
    <Button.Style>
        <Style TargetType="Button" BasedOn="{StaticResource Plat}">
            <Setter Property="Content" Value="{x:Static txt:TextesHistorique.BoutonPleinEcran}"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding EstPleinEcran}" Value="True">
                    <Setter Property="Content" Value="{x:Static txt:TextesHistorique.BoutonQuitterPleinEcran}"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </Button.Style>
</Button>
```
F11 sans modificateur est un `KeyGesture` valide (seules les touches lettre/chiffre exigent un modificateur). La fenêtre s'abonne
à `PropertyChanged(EstPleinEcran)` → `EntrerPleinEcran()` / `QuitterPleinEcran()` ; handler **nommé**, retiré au `Closed`, et
`EstPleinEcran = false` au `Closed` (le VM est un singleton qui survit à la fenêtre — piège 4 de D-35-05). Le bouton est visible
dans **toutes** les vues (le sélecteur de style ne l'était qu'en Semaine : retirer le `Visibility="{Binding IsVueSemaine…}"`).
Le drag de l'en-tête (`EnTete_Drag`) doit être ignoré en plein écran.

### Anti-Patterns to Avoid
- **Fusionner `PleinEcran` sur la fenêtre seulement :** masqué dans les vues par leur propre `DesignTokens.xaml`.
- **Laisser des `StaticResource` sur les clés échelonnées :** résolues une fois au chargement, elles ne bougent jamais (aucune erreur, juste rien).
- **`DataTrigger` sur une propriété posée en attribut local :** la valeur locale bat le trigger (précédence des DP).
- **Lignes `*` dans un `StackPanel` ou sous un `ScrollViewer` en `Auto` :** hauteur infinie → étoiles = `Auto`, rien ne s'agrandit.
- **`rcWork` au lieu de `rcMonitor` :** la barre des tâches resterait visible (c'est la bonne valeur pour l'agrandissement de `ReglagesWindow`, pas ici).
- **Persister le plein écran :** non demandé ; la fenêtre rouvre toujours en mode normal.

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser | Pourquoi |
|---------|-------------------|----------|----------|
| Partage proportionnel avec plafonds | Calcul des hauteurs en code-behind / converter | `RowDefinition` `*` + `MaxHeight` | Redistribution au plafond native (`ResolveStarMaxDiscrepancy`), se recalcule à chaque redimensionnement |
| Agrandissement des textes | `ScaleTransform` / `Viewbox` | `DynamicResource` + dictionnaire | Paliers exacts du contrat, texte net, pistes `OnRender` sur leur vraie taille |
| Conversion px → DIP | Division par 96/DPI codée à la main depuis `GetDpiForMonitor` | `CompositionTarget.TransformFromDevice` | DPI réel de la fenêtre, déjà la convention WPF |
| Désactivation du défilement | Retrait/ajout du `ScrollViewer` dans l'arbre | `VerticalScrollBarVisibility=Disabled` | Les tests s'appuient sur sa présence ; zéro restructuration |
| Lecture tolérante d'un membre supprimé | Migration du `settings.json` | Rien : `JsonUnmappedMemberHandling.Skip` | Le membre disparaît au prochain `Save` |

**Key insight :** le plein écran n'est pas un mode de mise en page à part ; c'est la même mise en page avec d'autres valeurs. Tout
ce qui est « autre valeur » passe par le dictionnaire ; tout ce qui est « autre structure » est interdit.

## Runtime State Inventory

| Catégorie | Éléments trouvés | Action |
|-----------|------------------|--------|
| Données stockées | `%APPDATA%\Chronos\settings.json` des utilisateurs peut contenir `"HistoriqueStyleSemaine": "Simplifie"` / `"Tuiles"` / `"Pistes"`. (Rappel mémoire : sous MSIX, `%APPDATA%` peut être une vue virtualisée ; sans effet ici, le fichier est lu par l'exe lui-même.) | **Aucune migration** : membre non mappé ignoré à la lecture, absent au prochain `Save`. Test témoin obligatoire (code edit seulement) |
| Config de services en ligne | Aucune — vérifié : le style n'est connu d'aucun service externe | — |
| État enregistré dans l'OS | Aucun — pas de tâche planifiée ni de clé de registre liée au style ; l'autostart `shell:startup` ne porte pas d'argument de style | — |
| Secrets / variables d'environnement | Aucun | — |
| Artefacts de build | `src/Chronos/bin/**`, `obj/**` contiennent encore `Tuiles5h`, `PisteFenetres5h`, BAML des grilles B/C (vu par `grep` binaire) ; exes publiés `Chronos-v3.4.0.exe` à la racine | Rebuild suffit (`obj` régénéré) ; ne rien faire sur les exes historiques |

## Common Pitfalls

### Pitfall 1 : croire que supprimer la propriété suffit à prouver « sans perte »
**Ce qui casse :** un test qui ne vérifie que l'absence d'exception laisse passer une régression du type « tout remis aux défauts ».
**Éviter :** témoin JSON contenant `"HistoriqueStyleSemaine": "Tuiles"` + thème non défaut, coin non défaut, géométrie Historique,
`InnerStatusLineCommand` (si la propriété existe encore après la phase 37) → toutes relues à l'identique, puis `Save(Load())` →
fichier réécrit sans la clé et sans perte. Réutiliser `FixtureLegacy()` / `EcrireSettings()` de `SettingsServiceTests`.

### Pitfall 2 : dictionnaire masqué par les `DesignTokens.xaml` des vues
**Symptôme :** l'en-tête grossit, les pistes et libellés non. **Éviter :** fusionner dans fenêtre + `VueSemaine` + `VueJour` +
`VueQuatreSemaines` (noms déjà exposés, `HistoriqueWindow.xaml:298-300`). **Détection :** test `[WpfFact]` qui lit
`FontSize` d'un libellé de piste de chaque vue après bascule.

### Pitfall 3 : `StaticResource` oubliée
**Symptôme :** une taille reste à sa valeur normale, sans erreur. **Éviter :** garde textuelle : dans les 4 XAML de l'Historique, aucune
`StaticResource` sur une clé de `DictionnairePleinEcran.ClesEchelonnees`, des hauteurs de pistes ni des `HistoRangee*`
(regex sur `\{StaticResource (clé)\}`). Peu coûteuse et alignée sur les gardes existantes.

### Pitfall 4 : DPI mixte et coordonnées DIP
**Ce qui casse :** en `PerMonitorV2`, `Left/Top` sont des DIP au DPI de la fenêtre ; la mémoire du projet signale des
« `Window.Left/Top` cassés » pour l'overlay. Une fenêtre à cheval sur deux moniteurs de DPI différents peut se décaler d'un
pixel ou changer de DPI pendant les quatre affectations. **Éviter :** `MonitorFromWindow(NEAREST)` (moniteur de plus grande
intersection, celui dont la fenêtre a le DPI) ; ordre position → taille à l'entrée, taille → position à la sortie. **Repli :**
`SetWindowPos(hwnd, 0, rc.Left, rc.Top, w, h, SWP_NOZORDER|SWP_NOACTIVATE)` en pixels physiques (un seul appel atomique ; `SWP_NOZORDER`
= 0x0004 à ajouter). **Détection :** constat manuel sur la machine de l'utilisateur, moniteur secondaire.

### Pitfall 5 : coins arrondis DWM visibles en plein écran
**Symptôme :** quatre petits coins de bureau aux angles (Windows 11 arrondit toute fenêtre non agrandie dont la préférence est
`DWMWCP_ROUND`, posée par `ArrondirCoinsDwm`). **Éviter :** `DWMWCP_DONOTROUND` (1) en entrée, `DWMWCP_ROUND` (2) en sortie ;
best-effort comme l'existant (Windows 10 : échec silencieux).

### Pitfall 6 : en-tête fixe à 92 avec des corps ×1,35
**Symptôme :** à 92 / 3 = 30,7 DIP par ligne, le segment (corps 15 + padding 8 + bordure 3 ≈ 31) déborde, et la fraîcheur sur deux
lignes (§ 7, ≈ 37 DIP à corps 14) est impossible. **Éviter :** passer `HistoHauteurEnTete` en `DynamicResource` avec une valeur
plein écran 124 (≈ ×1,35) et `TextWrapping="Wrap"` sur la fraîcheur, ligne 3 en deux colonnes `*`/`Auto`. Voir Open Question 1.

### Pitfall 7 : tests qui cherchent les grilles par nom ou comptent les styles
`VueJourBindingTests:205` refuse tout élément visible dont le nom commence par `Style` ; `VueSemaineBindingTests.GrilleVisible`
et `HonneteteHistoriqueTests.TroisStyles` supposent trois grilles. Renommer la grille unique (ex. `GrillePistes`) et réécrire ces
aides **avant** de supprimer les grilles B/C, pour que l'échec éventuel pointe la bonne cause.

### Pitfall 8 : `EnregistrerGeometrie` écrase la géométrie normale par celle de l'écran
**Ce qui casse :** en plein écran, `WindowState` reste `Normal` → la garde actuelle (`HistoriqueWindow.xaml.cs:93`) laisse passer et
`Closing` persisterait les bornes du moniteur. **Éviter :** si `_avantPleinEcran` est non nul, persister **celle-ci**. Test : entrer
(moniteur injecté), appeler `EnregistrerGeometrie()`, vérifier `HistoriqueX/Y/Width/Height` = valeurs d'avant.

### Pitfall 9 : redimensionnement et déplacement possibles en plein écran
`WindowChrome.ResizeBorderThickness` (6) et le drag d'en-tête restent actifs. `ResizeMode=NoResize` pendant le plein écran (le
`WindowChrome` honore `ResizeMode`) et `EnTete_Drag` sans effet si `EstPleinEcran`. Changement de DPI ou d'affichage pendant le
plein écran : recalculer les bornes sur `DpiChanged` (optionnel, à noter si non traité).

### Pitfall 10 : petit écran (< 1 280 px)
Pas de `MinHeight` sur les lignes étoilées (sinon, sur un écran bas, la grille dépasserait le viewport et, défilement coupé, serait
tronquée). Les libellés de colonne s'enroulent (`TextWrapping=Wrap` déjà posé). Calcul à 1280 × 720 : ≈ 420 DIP pour les trois
pistes Semaine → 214 / 103 / 103, au-dessus des hauteurs normales. Les annotations « épuisée » de la vue 4 semaines sont tronquées
**par conception** (ellipse + infobulle, `VueQuatreSemainesView.xaml:151`) : ce n'est pas une régression.

## Code Examples

### Test des proportions et plafonds (Measure/Arrange, vue seule)
```csharp
[WpfFact]
public void En_plein_ecran_les_pistes_de_la_semaine_se_partagent_la_hauteur_dans_leurs_proportions()
{
    var (vue, _) = Monter();
    vue.Resources.MergedDictionaries.Add(DictionnairePleinEcran.Construire(Tokens()));
    MettreEnPage(vue, 1400, 700);
    var p = PistesVisibles(vue).ToList();                 // Niveau, Rythme, Tokens, Couverture
    Assert.Equal(150.0 / 72.0, p[0].ActualHeight / p[1].ActualHeight, 2);
    Assert.Equal(p[1].ActualHeight, p[2].ActualHeight, 1);
    Assert.Equal(12, p[3].ActualHeight);                  // couverture fixe
    Assert.Equal(0, ((ScrollViewer)vue.FindName("Defilement")).ScrollableHeight);

    MettreEnPage(vue, 2560, 1600);                        // grand écran : plafonds
    Assert.Equal(240, p[1].ActualHeight, 1);
    Assert.Equal(520, p[0].ActualHeight, 1);
}
```

### Test de la géométrie (fenêtre jamais montrée, moniteur injecté)
```csharp
[WpfFact]
public void Le_plein_ecran_couvre_le_moniteur_et_la_sortie_restaure_la_geometrie()
{
    var (f, vm, _, reglages) = Monter(reglages: new ReglagesHistoriqueMemoire(new ChronosSettings
        { HistoriqueX = 100, HistoriqueY = 50, HistoriqueWidth = 900, HistoriqueHeight = 600 }));
    f.EntrerPleinEcran(new RectangleEcran(0, 0, 1536, 864));
    Assert.Equal((0d, 0d, 1536d, 864d), (f.Left, f.Top, f.Width, f.Height));
    f.EnregistrerGeometrie();
    Assert.Equal(900, reglages.Courant.HistoriqueWidth);  // la géométrie persistée reste celle du mode normal
    f.QuitterPleinEcran();
    Assert.Equal((100d, 50d, 900d, 600d), (f.Left, f.Top, f.Width, f.Height));
}
```

### Témoin de lecture (HIS-09)
```csharp
[Fact]
public void Un_ancien_reglage_de_style_d_historique_se_lit_sans_perte()
{
    EcrireSettings(FixtureLegacy().Replace("{", "{ \"HistoriqueStyleSemaine\": \"Tuiles\", \"ThemeKey\": \"nord\", ", 1)); // forme à adapter
    var s = _service.Load();
    Assert.Equal("nord", s.ThemeKey);  /* + coin, géométries, … */
    _service.Save(s);
    Assert.DoesNotContain("HistoriqueStyleSemaine", File.ReadAllText(_paths.SettingsFile), StringComparison.Ordinal);
}
```

## State of the Art

| Ancienne approche | Approche actuelle | Changement | Impact |
|-------------------|-------------------|------------|--------|
| `ResolveStarLegacy` (étoiles pouvant dépasser l'espace) | `ResolveStarMaxDiscrepancy` | .NET Framework 4.7 / tout .NET Core ; switch `GridStarDefinitionsCanExceedAvailableSpace` | Plafond + redistribution fiables ; ne pas activer le switch |
| `SystemParameters.PrimaryScreen*` pour le plein écran | `MonitorFromWindow` + `GetMonitorInfo` | Multi-écrans | Écran **courant**, pas le primaire |

## Open Questions

1. **Hauteur de l'en-tête en plein écran (124 ?)**
   - Connu : le contrat liste les corps (dont titre 16 → 21) mais pas `HistoHauteurEnTete` ; à 92 l'en-tête déborde déjà d'≈ 0,3 DIP
     et ne peut pas porter la fraîcheur sur deux lignes (§ 7).
   - Flou : si l'utilisateur accepte un token non listé.
   - Recommandation : `HistoHauteurEnTetePleinEcran` = 124 (≈ ×1,35, dans l'esprit du contrat), signalé dans le SUMMARY et à la
     revue visuelle ; alternative : `MinHeight` 92 avec lignes `Auto`.
2. **Colonne des étiquettes de la vue 4 semaines (140)**
   - Connu : `HistoLargeurEtiquettesSemaines` n'est pas dans le contrat ; corps ×1,35 dedans → plus de retours à la ligne
     (`TextWrapping=Wrap`, pas de troncature).
   - Recommandation : la laisser à 140 (ne pas inventer de chiffre) et vérifier par test que les étiquettes s'enroulent sans être
     coupées (hauteur de l'`ItemsControl` ≤ hauteur de la piste à 1280 × 720).
3. **Barre des tâches couverte sans `Topmost`**
   - Connu : détection automatique du shell documentée ; non vérifiable en test unitaire.
   - Recommandation : constat manuel dans la vérification (moniteur primaire + secondaire) ; repli `MarkFullscreenWindow` prêt.
4. **Phase 36 pas encore exécutée** (STATE : « Ready to plan »). La suppression ne dépend pas techniquement d'elle (membre non mappé),
   mais l'ordre de ROADMAP l'impose ; le témoin de cette phase doit passer quel que soit l'état de 36.

## Environment Availability

| Dépendance | Requise par | Disponible | Version | Repli |
|------------|-------------|------------|---------|-------|
| .NET SDK | build/test | ✓ | 10.0.201 (cible net8.0-windows) | — |
| Windows 11 (DWM, shell) | coins, barre des tâches | ✓ | 10.0.26200 | Windows 10 : coins droits (best-effort existant) |
| Second moniteur / DPI mixte | constat Pitfall 4 | inconnu | — | constat sur moniteur unique + test de `BornesDip` à 1,25 / 1,5 |

Aucune dépendance bloquante.

## Validation Architecture

### Test Framework
| Propriété | Valeur |
|-----------|--------|
| Framework | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`) |
| Config | `tests/Chronos.Tests/Chronos.Tests.csproj` (chemins des sources/docs injectés par `AssemblyMetadata`) |
| Quick run | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q --filter "FullyQualifiedName~Historique\|FullyQualifiedName~VueSemaine\|FullyQualifiedName~VueJour\|FullyQualifiedName~QuatreSemaines\|FullyQualifiedName~PleinEcran\|FullyQualifiedName~SettingsService\|FullyQualifiedName~Reglages"` |
| Full suite | `dotnet test Chronos.sln -c Debug --nologo -v q` + `dotnet build Chronos.sln -c Release --nologo` (0 warning) |

### Phase Requirements → Test Map
| Req ID | Comportement | Type | Commande | Fichier |
|--------|--------------|------|----------|---------|
| HIS-09 | Plus aucun sélecteur (fenêtre, réglages), « Aussi : double-clic… » présent | `[WpfFact]` | `--filter "FullyQualifiedName~HistoriqueBindingTests\|FullyQualifiedName~ReglagesBindingTests"` | existants, à adapter |
| HIS-09 | Grille unique Pistes, hauteurs 150/72/72/12 en normal | `[WpfFact]` | `--filter FullyQualifiedName~VueSemaineBindingTests` | existant, à adapter |
| HIS-09 | Témoin `"HistoriqueStyleSemaine": "Tuiles"` lu sans perte, réécrit sans la clé | `[Fact]` | `--filter FullyQualifiedName~SettingsServiceTests` | ❌ W0 (nouveau test) |
| HIS-09 | Tokens supprimés absents, README sans « Trois styles » / « Simplifié » / « Tuiles » | garde textuelle | `--filter "FullyQualifiedName~GardeTokensHistoriqueTests\|FullyQualifiedName~GardeDocumentationHistoriqueTests"` | existants, à adapter |
| HIS-10 | Dictionnaire `PleinEcran` : 16 valeurs contractuelles, étoiles = hauteurs normales | `[WpfFact]` | `--filter FullyQualifiedName~PleinEcranHistoriqueTests` | ❌ W0 |
| HIS-10 | Proportions et plafonds (3 vues), couverture/annotations fixes, `ScrollableHeight` = 0, à 1280×720 et 2560×1600 | `[WpfFact]` Measure/Arrange | `--filter FullyQualifiedName~PleinEcranVuesTests` | ❌ W0 |
| HIS-10 | Polices/largeurs/traits basculés dans **chaque** vue (Pitfall 2) ; aucune `StaticResource` sur une clé échelonnée (Pitfall 3) | `[WpfFact]` + garde textuelle | idem | ❌ W0 |
| HIS-10 | Honnêteté identique : pied, annotations de trou, hachure, libellés présents en plein écran | `[WpfFact]` | `--filter FullyQualifiedName~HonneteteHistoriqueTests` | à étendre |
| HIS-10 | `BornesDip` à 1,0 / 1,25 / 1,5, origine négative | `[Fact]` pur | `--filter FullyQualifiedName~PleinEcranHistoriqueTests` | ❌ W0 |
| HIS-11 | `Echap(true)=QuitterPleinEcran`, `Echap(false)=Fermer` ; `EchapCommand` : 1er appui quitte, 2e ferme (`DemandesDeFermeture`) | `[Fact]` + `[WpfFact]` | idem + `HistoriqueBindingTests` | ❌ W0 |
| HIS-11 | `KeyBinding` Échap → `EchapCommand`, F11 → `BasculerPleinEcranCommand` ; bouton visible avec le bon libellé dans les deux états | `[WpfFact]` | `--filter FullyQualifiedName~HistoriqueBindingTests` | à adapter (l.116-129) |
| HIS-11 | Géométrie restaurée ; géométrie persistée pendant le plein écran = celle d'avant | `[WpfFact]` | idem | ❌ W0 |
| HIS-10/11 | Barre des tâches couverte, moniteur secondaire, coins carrés | manuel | — | constat (non automatisable : shell + DWM) |

### Sampling Rate
- **Par commit de tâche :** quick run ci-dessus.
- **Par vague :** suite complète Debug (deux passes, 0 échec — tests WPF sensibles au dispatcher) + build Release 0 warning.
- **Gate de phase :** suite complète verte + constat manuel plein écran avant `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/PleinEcranHistoriqueTests.cs` — fonction `Echap`, `BornesDip`, contenu du dictionnaire (HIS-10, HIS-11)
- [ ] `tests/Chronos.Tests/PleinEcranVuesTests.cs` — Measure/Arrange des 3 vues avec dictionnaire fusionné ; garde « pas de StaticResource sur clé échelonnée »
- [ ] `SettingsServiceTests` — témoin « Tuiles » (remplace l.230-268)
- [ ] `GardeTokensHistoriqueTests.TaillesHisto` — −6 tokens, + tokens `*PleinEcran` et plafonds ; test des `GridLength` / `ScrollBarVisibility`
- [ ] Aides de test : `VueSemaineBindingTests.Monter` sans paramètre de style ; aide partagée « monter une vue avec PleinEcran »

## Sources

### Primary (HIGH confidence)
- Code du dépôt (lu) : `HistoriqueWindow.xaml(.cs)`, `VueSemaineView.xaml`, `VueJourView.xaml`, `VueQuatreSemainesView.xaml`,
  `DesignTokens.xaml`, `NativeMethods.cs`, `OverlayController.cs`, `ReglagesWindow.xaml(.cs)`, `PlacementHistorique.cs`,
  `ChronosSettings.cs`, `SettingsService.cs`, `HistoriqueViewModel.cs`, `TextesHistorique.cs`, `PisteBase.cs`, tests cités.
- dotnet/wpf `Grid.cs` (main) — `ResolveStarMaxDiscrepancy` par défaut, commentaire sur les définitions qui atteignent min/max,
  switch `GridStarDefinitionsCanExceedAvailableSpace` : https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Grid.cs
- Microsoft Learn — `ITaskbarList2::MarkFullscreenWindow` (détection automatique du plein écran par le shell) :
  https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-itaskbarlist2-markfullscreenwindow
- Plan contractuel `.zeus/DESIGN_PLAN_CYCLE2.md` § 4, § 7 ; inventaire `.zeus/reports/cycle2/inventaire-themes-historique.md` § B.

### Secondary (MEDIUM confidence)
- Fenêtre sans bordure exactement à la taille de l'écran = couvre la barre des tâches (communauté, cohérent avec la doc shell) :
  https://www.abhisheksur.com/2010/09/taskbar-with-window-maximized-and.html ,
  https://www.codegenes.net/blog/properly-maximizing-wpf-window-with-windowstyle-none/
- Ordre de recherche des dictionnaires fusionnés (dernier ajouté prioritaire), invalidation des `DynamicResource` à l'ajout/retrait
  d'un dictionnaire fusionné, `ScrollViewer` `Disabled` = contrainte finie : comportement WPF connu, cohérent avec le motif des
  pinceaux de thème du dépôt — à confirmer par les tests Wave 0.

### Tertiary (LOW confidence)
- Coordonnées DIP exactes en DPI mixte (`PerMonitorV2`) avec `Left/Top` : non vérifié sur la machine ; repli `SetWindowPos` documenté.

## Metadata

**Confidence breakdown :**
- Stack / inventaire : HIGH — tout est dans le dépôt, vérifié par `git grep` et lecture.
- Architecture (dictionnaire, étoiles, défilement) : HIGH — mécanismes WPF standards, source dotnet/wpf pour la redistribution.
- Géométrie / barre des tâches / DPI mixte : MEDIUM — dépend du shell et du matériel, constat manuel prévu.
- Pitfalls : HIGH pour 1-3, 6-10 (dérivés du code) ; MEDIUM pour 4-5.

**Research date :** 2026-10-03
**Valid until :** 2026-11-02 (stack stable)
