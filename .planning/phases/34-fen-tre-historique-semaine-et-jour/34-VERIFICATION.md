---
phase: 34-fen-tre-historique-semaine-et-jour
verified: 2026-09-27T00:00:00Z
status: passed
score: 8/8 must-haves verified
---

# Phase 34: Fenêtre Historique : Semaine et Jour — Verification Report

**Phase Goal:** Une fenêtre « Historique » séparée — opaque, non topmost, redimensionnable, mémorisée — montre la
semaine de forfait dans les trois styles du plan de design (Pistes, Simplifié, Tuiles) et le jour au grain de
5 min, en consommant les deux journaux sur deux axes distincts ; un trou reste un trou, un saut pendant l'absence
reste « répartition inconnue », une marche sans tokens Code dit « consommé ailleurs » ; le rendu est un `OnRender`
par piste gouverné par `DesignTokens.xaml`, et le bandeau F2 dit où en est la reconstruction.

**Verified:** 2026-09-27
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (Success Criteria, ROADMAP §Phase 34)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Une fenêtre de consultation, pas un overlay (HIS-01) | ✓ VERIFIED | `HistoriqueWindow.xaml:10` — `WindowStyle="None" AllowsTransparency="False" Topmost="False" ShowInTaskbar="True" ResizeMode="CanResize"` ; `shell:WindowChrome` avec `CaptionHeight`/`ResizeBorderThickness` en tokens (l.176-179) ; `MinWidth`/`MinHeight` = `HistoLargeurMin`(760)/`HistoHauteurMin`(480) tokens ; défaut 920×610 (`HistoLargeurDefaut`/`HistoHauteurDefaut`) ; géométrie persistée par `PlacementHistorique.Borner` + `EnregistrerGeometrie` (VM, `WindowState.Normal` seulement) |
| 2 | Semaine de forfait en style Pistes (HIS-02) | ✓ VERIFIED | `EchelleTemps`/`EchelleValeur`/`Escalier`/`Binning`/`Reticule` (Rendering/Historique, purs, 26 tests) ; `PisteNiveau`/`PisteRythme`/`PisteTokens`/`PisteCouverture` liées dans `VueSemaineView.xaml` (`hc:PisteNiveau ... Analyse="{Binding DonneesSemaine.Analyse}"` etc.) ; réticule commun `SurcoucheReticule` |
| 3 | Trois styles, un réglage (HIS-03) | ✓ VERIFIED | Tokens hauteurs exacts en base : Pistes 150/72/72/12, Simplifié 200/90/12, Tuiles 120/62/58/58/12 (`DesignTokens.xaml:82-93`) ; trois `Grid` (`StylePistes`/`StyleSimplifie`/`StyleTuiles`) commutées par `IsStyleX` ; vérifiées par Measure/Arrange réel dans `VueSemaineBindingTests.cs` (ActualHeight exact par style) |
| 4 | Le jour au grain de 5 min (HIS-04) | ✓ VERIFIED | `DesignTokens.xaml:95-97` : `HistoHauteurNiveauJour`=190, `HistoHauteurRythmeJour`=64, `HistoHauteurTokensJour`=64, `HistoHauteurCouverture`=12 ; vérifiées par Measure/Arrange dans `VueJourBindingTests.cs` (`Assert.InRange` sur 190/64/64/12) ; `PisteTokens` bindé `Colonnes="{Binding DonneesJour.Colonnes}"` (empilement par modèle) |
| 5 | L'honnêteté est testée mot pour mot (HIS-06) | ✓ VERIFIED | `TextesHistorique.cs` contient mot pour mot : `RepartitionInconnue` (« pendant l'absence (répartition inconnue) »), `PiedDivergence` (« consommé ailleurs (Cowork, claude.ai) »), `Epuisee` (« épuisée à 100 % — le serveur refuse (statut rejected) »), `PiedDePage` (« Aucun trou n'est interpolé… ») ; `HonneteteHistoriqueTests.cs` (11 `[WpfFact]`) prouve mots + traits sur 2 vues × 3 styles à partir des MÊMES objets que la galerie (`SourceHistoriqueDemonstration`) ; 5 mutations h1–h5 rougies puis révoquées (sha256 documentés dans 34-08-SUMMARY.md) ; `GardeVocabulaireHistoriqueTests.cs` interdit `épuisé[e]? vers\|à ce rythme\|projection\|prévision\|estim…\|tendance\|dans N h` sur les fichiers de la fenêtre |
| 6 | Un rendu qui ne coûte rien au tick (HIS-07, HIS-08) | ✓ VERIFIED | Palette 7 brosses promues avec valeurs EXACTES (`#151322`/`#1E1B30`/`#2C2942`/`#F2F0FB`/`#A9A6C4`/`#8B7BF0`/`#4EC98A`) ; `SettingsWindow.xaml` fusionne `DesignTokens.xaml` par pack URI, ne redéclare plus ces 7 brosses (garde à 12 littéraux restants, testée) ; `PisteBase.OnRender` scellé, `StreamGeometry`/`Pen` gelés, compteur `RendusPourTests` immobile sur 60 changements de surcouche (`PistesHistoriqueTests.Le_compteur_de_rendus_ne_bouge_pas_quand_seule_la_surcouche_change`) ; `ServicesLayerPurityTests` étendu voit `Chronos.Services.Historique`/`Chronos.Models.Historique` et les considère neutres ; bandeau F2 (`HistoriqueWindow.xaml:264-282`) bindé `AfficherBandeauF2`/`TexteBandeauF2`/`FractionBandeauF2`/`SousTexteBandeauF2` |

**Score:** 6/6 truths verified (mapped from ROADMAP Success Criteria; HIS-05 correctly out of scope — Phase 35)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Chronos/Resources/DesignTokens.xaml` | Palette promue + tokens Histo* + 38 tailles | ✓ VERIFIED | 7 brosses exactes, `HistoTokens #8C89A8`, `HistoSousAgent #5A5776`, `HistoModele1/2/3 #9D9AB8/#6E6B8C/#4A4762`, `HistoGris #5A5960` tous présents avec valeurs contractuelles |
| `src/Chronos/Views/SettingsWindow.xaml` | Fusion DesignTokens.xaml, plus de brosses locales | ✓ VERIFIED | `MergedDictionaries` + `pack://application:,,,/Chronos;component/Resources/DesignTokens.xaml` ; 0 occurrence des 7 hex promus en local ; 12 littéraux restants (non-régression testée) |
| `src/Chronos/Rendering/Historique/*.cs` (6 fichiers) | Géométrie pure | ✓ VERIFIED | `EchelleTemps`, `EchelleValeur`, `Escalier`, `Binning`, `Tuiles5h`, `Reticule` tous présents, sans `System.Windows` |
| `src/Chronos/Controls/Historique/*.cs` (7 fichiers) | Pistes OnRender | ✓ VERIFIED | `PisteBase`, `PisteNiveau`, `PisteRythme`, `PisteTokens`, `PisteCouverture`, `PisteFenetres5h`, `SurcoucheReticule` |
| `src/Chronos/ViewModels/Historique/*.cs` | VM + scénarios | ✓ VERIFIED | `HistoriqueViewModel`, `ScenariosHistorique`, `SourceHistoriqueDemonstration` ; lecture par `Task.Run` + `IUiDispatcher.Post` confirmée (pas de stub) |
| `src/Chronos/Views/Historique/HistoriqueWindow.xaml(.cs)` | Fenêtre + chrome + F2 | ✓ VERIFIED | Chrome conforme, bandeau F2 bindé, mode `--historique` avant verrou |
| `src/Chronos/Views/Historique/VueSemaineView.xaml` | 3 styles assemblés | ✓ VERIFIED | 3 grilles, hauteurs exactes par Measure/Arrange |
| `src/Chronos/Views/Historique/VueJourView.xaml` | Vue Jour assemblée | ✓ VERIFIED | 4 pistes hauteurs exactes, annotations, légende modèles |
| `src/Chronos/Text/TextesHistorique.cs` | Vocabulaire mot pour mot | ✓ VERIFIED | Toutes les chaînes d'honnêteté du plan §4/§2.2/§2.3 présentes textuellement |
| `tests/Chronos.Tests/HonneteteHistoriqueTests.cs` | Honnêteté bout en bout | ✓ VERIFIED | 11 `[WpfFact]`, 2 vues × 3 styles |
| `tests/Chronos.Tests/GardeVocabulaireHistoriqueTests.cs` | Garde anti-projection | ✓ VERIFIED | Regex anti-projection sur les fichiers Historique |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `SettingsWindow.xaml` | `DesignTokens.xaml` | `MergedDictionaries` pack URI | ✓ WIRED | Une seule fusion, `StaticResource` résolvent |
| `VueSemaineView.xaml` | `DesignTokens.xaml` | `Height="{StaticResource HistoHauteur*}"` | ✓ WIRED | `HistoHauteurNiveauPistes` etc. utilisés en Height sur les pistes réelles |
| `VueSemaineView.xaml` | `PisteNiveau.cs` | DP `Analyse`/`Precedente`/`Divergences`/`Rampe` | ✓ WIRED | `hc:PisteNiveau Plage="{Binding DonneesSemaine.Plage}" Analyse="{Binding DonneesSemaine.Analyse}" ...` |
| `VueSemaineView.xaml` | `HistoriqueViewModel.cs` | `DonneesSemaine.*`, `IsStyle*` | ✓ WIRED | Bindings confirmés dans le XAML |
| `VueJourView.xaml` | `PisteTokens.cs` | DP `Colonnes` | ✓ WIRED | `Colonnes="{Binding DonneesJour.Colonnes}"` |
| `App.xaml.cs` | Mode `--historique` | avant `VerrouInstanceUnique.Acquerir` | ✓ WIRED | Ligne 67 (`--historique`) précède ligne 83 (`Acquerir`) |
| `HistoriqueViewModel.cs` | `ISourceHistorique` | `Task.Run` + `IUiDispatcher.Post` | ✓ WIRED | Lecture hors UI confirmée, pas de données statiques |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|---------------------|--------|
| `HistoriqueViewModel` | `DonneesSemaine`/`DonneesJour` | `_source.LireSemaine`/`_source.LireJour` (ISourceHistorique) via `Task.Run`, appliqué par `IUiDispatcher.Post` | Oui — implémentations réelles `SourceHistoriqueDisque` (disque) et `SourceHistoriqueDemonstration` (galerie/tests, scénario déterministe partagé) | ✓ FLOWING |
| `VueSemaineView`/`VueJourView` | Pistes | Bindings directs sur `DonneesSemaine`/`DonneesJour` du VM | Oui — aucune valeur hardcodée dans les vues | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Suite complète (attendu 1565/0, 0 warning) | `dotnet test Chronos.sln -c Debug --nologo -v q` | `1565 réussis, 0 échec, 0 ignoré` | ✓ PASS |
| Build Release 0 warning | `dotnet build Chronos.sln -c Release --nologo` | `0 Avertissement(s), 0 Erreur(s)` | ✓ PASS |
| « jamais au tick » niveau contrôle | `PistesHistoriqueTests.Le_compteur_de_rendus_ne_bouge_pas_quand_seule_la_surcouche_change` (dans la suite verte) | présent et vert | ✓ PASS |
| Palette SettingsWindow non-régression (12 littéraux) | `GardeTokensHistoriqueTests.SettingsWindow_garde_ses_douze_litteraux_et_pas_un_de_plus` (dans la suite verte) | présent et vert | ✓ PASS |
| Mutations h1–h5 rouges puis révoquées | Documentées dans `34-08-SUMMARY.md` avec sha256 par mutation | 5/5 documentées, `git status --short` vide après révocation | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|--------------|--------|----------|
| HIS-01 | 34-05 | `HistoriqueWindow` séparée, chrome, redimensionnable, mémorisée, en-tête commun | ✓ SATISFIED | Chrome + géométrie confirmés dans le XAML/code-behind |
| HIS-02 | 34-02, 34-06 | Vue Semaine style Pistes | ✓ SATISFIED | Géométrie pure + assemblage XAML + Measure/Arrange |
| HIS-03 | 34-06 | Styles Simplifié/Tuiles sélectionnables | ✓ SATISFIED | 3 grilles, hauteurs exactes testées |
| HIS-04 | 34-07 | Vue Jour grain 5 min | ✓ SATISFIED | 4 pistes, hauteurs exactes testées |
| HIS-06 | 34-08 (+34-02/03/06/07) | Honnêteté testée mot pour mot | ✓ SATISFIED | `HonneteteHistoriqueTests` + `GardeVocabulaireHistoriqueTests` + mutations h1-h5 |
| HIS-07 | 34-01, 34-04 | Rendu OnRender gouverné par tokens | ✓ SATISFIED | `PisteBase` scellé, tokens exacts, `ServicesLayerPurityTests` vert |
| HIS-08 | 34-03 | Bandeau F2 | ✓ SATISFIED | Bindings F2 dans `HistoriqueWindow.xaml`, textes exacts dans `TextesHistorique` |
| HIS-05 | (aucun plan de phase 34) | Vue 4 semaines | N/A — hors périmètre (Phase 35, confirmé par CONTEXT.md et REQUIREMENTS.md ligne 187 « Pending ») | Pas orphelin : jamais assigné à la phase 34 |

Aucune exigence orpheline détectée : les 7 IDs déclarés en frontmatter de plan (HIS-01, 02, 03, 04, 06, 07, 08) correspondent exactement à ceux de `.planning/REQUIREMENTS.md` mappés « Phase 34 ». HIS-05 est mappé « Phase 35 » dans REQUIREMENTS.md, cohérent avec le CONTEXT.md qui l'exclut explicitement du périmètre.

### Anti-Patterns Found

Aucun. Recherche `TODO|FIXME|XXX|HACK|PLACEHOLDER|coming soon|not yet implemented` sur `Views/Historique`, `Controls/Historique`, `Rendering/Historique`, `ViewModels/Historique`, `Text/TextesHistorique.cs` : 0 occurrence. Le segment « 4 semaines » désactivé porte une infobulle honnête (« bientôt (phase 35) »), pas un texte trompeur.

### Human Verification Required

Ces éléments sont hors de portée d'une vérification automatisée (rendu perçu, DPI, fidélité maquette) et attendent la revue DAEDALUS de l'utilisateur, comme prévu par `34-VALIDATION.md` (§ Manual-Only Verifications) et le contrat `DESIGN_PLAN.md §8` :

### 1. Revue visuelle des trois styles (Pistes / Simplifié / Tuiles)

**Test:** Lancer `dotnet run --project src/Chronos -- --historique` (ou `Chronos.exe --historique`), ouvrir la vue Semaine, basculer entre les trois styles via le sélecteur.
**Expected:** Les trois styles affichent exactement les pistes et hauteurs prévues, sans texte tronqué, avec les couleurs de la rampe du thème actif et les annotations d'honnêteté lisibles.
**Why human:** Lisibilité et fidélité visuelle aux maquettes Figma ne sont pas mesurables par un test automatisé.

### 2. Vue Jour avec reset, plateau épuisé et infobulle

**Test:** Dans la galerie, ouvrir la vue Jour sur jeudi 24 (« Aujourd'hui ») et mercredi 23 (‹), survoler pour l'infobulle.
**Expected:** Reset 5 h visible avec libellé, plateau épuisé grisé et annoté, infobulle à quatre lignes correcte au survol.
**Why human:** Rendu perçu et positionnement de l'infobulle (bord droit non borné, noté comme point à juger en revue DAEDALUS dans 34-06-SUMMARY.md).

### 3. Redimensionnement 760 → 1 400 px et échelles 100 % / 150 %

**Test:** Redimensionner la fenêtre de 760 à plus de 1400 px de large ; répéter à l'échelle Windows 100 % puis 150 %.
**Expected:** Aucun chevauchement, aucun texte tronqué, défilement vertical du corps si nécessaire, axe des jours/heures et pieds de page restent fixes hors défilement.
**Why human:** Mise à l'échelle DPI et absence de chevauchement visuel ne sont vérifiables qu'à l'œil.

### 4. Coins arrondis et ombre DWM

**Test:** Observer les coins de la fenêtre sur la machine de l'utilisateur (Windows 11 vs Windows 10).
**Expected:** Coins arrondis best-effort (~8 px) sur Windows 11, coins droits sur Windows 10 — écart assumé vs les 16 px de la maquette (noté en D-34-22).
**Why human:** API DWM non observable dans un test automatisé hors écran.

### Gaps Summary

Aucun écart bloquant trouvé. Les 8 must-haves des 8 plans (34-01 à 34-08) sont vérifiés dans le code réel, pas seulement dans les SUMMARY : tokens exacts, chrome de fenêtre conforme, hauteurs de pistes par style vérifiées par Measure/Arrange réel, textes d'honnêteté mot pour mot dans `TextesHistorique`, garde de vocabulaire anti-projection active, garde de pureté des couches étendue à `Services/Historique`/`Models/Historique`, mode `--historique` positionné avant le verrou mono-instance, test « jamais au tick » présent et vert au niveau contrôle. La suite complète est verte à 1565/1565 avec 0 avertissement Debug et Release, conforme à l'attendu du prompt. HIS-05 est correctement hors périmètre (Phase 35), sans exigence orpheline. Les seuls points restants relèvent explicitement de la revue visuelle humaine DAEDALUS (rendu perçu, DPI, fidélité Figma), déjà identifiés comme tels par `34-VALIDATION.md` et non exécutables par un agent.

---

_Verified: 2026-09-27_
_Verifier: Claude (gsd-verifier)_
