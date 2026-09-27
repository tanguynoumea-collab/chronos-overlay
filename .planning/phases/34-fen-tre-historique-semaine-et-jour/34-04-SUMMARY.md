---
phase: 34-fen-tre-historique-semaine-et-jour
plan: 04
subsystem: ui
tags: [wpf, onrender, streamgeometry, dependency-property, pistes, historique, converters, tdd, mutations, trace-de-rendu]

# Dependency graph
requires:
  - phase: 34-01
    provides: tokens Histo* (HistoGris, HistoHachure, HistoEpaisseur*, HistoOpacite*), garde « aucune valeur en dur » sous Controls/Historique
  - phase: 34-02
    provides: EchelleTemps, EchelleValeur, Escalier (Segments / ParBandes / RectanglesTrous / BlocsSauts), Binning (DeltasParHeure / ReductionMinMax / SeuilReduction), Tuiles5h, Reticule
  - phase: 34-03
    provides: DonneesSemaine / DonneesJour, Divergence, TextesHistorique.Infobulle, ScenariosHistorique + SourceHistoriqueDemonstration, HistoriqueViewModel.PlafondTokens / Theme / Fuseau / Maintenant
  - phase: 33-04
    provides: BarreHeure, ColonneQuartDHeure, PartModele, TotauxTokens.Out, EtatCouverture, TrancheTokens.Tranche
provides:
  - "Controls/Historique/PisteBase.cs — FrameworkElement abstrait : DP Plage / Grille / EpaisseurFine, OnRender scellé (compteur, trace, clip, guides), Plume / PlumePointillee / GeometrieEscalier gelées, DessinerGrille"
  - "Controls/Historique/PisteNiveau.cs — variantes SemaineComplete / SemaineHebdoSeul / Jour : escalier par bandes de rampe, dents 5 h (réduction min/max > 4 000), tirets et traits de reset, fantôme S-1, trous, sauts, divergences, gris si épuisée ou refusée"
  - "Controls/Historique/PisteRythme.cs, PisteTokens.cs, PisteCouverture.cs, PisteFenetres5h.cs — une piste par ligne du plan de design"
  - "Controls/Historique/SurcoucheReticule.cs — réticule, infobulle (DP lecture seule), ligne « maintenant » ; la seule à bouger au survol et au tick"
  - "Converters/HistoriqueConverters.cs — InstantVersXConverter, LargeurIntervalleConverter, FractionVersLargeurConverter (IMultiValueConverter tolérants)"
  - "17 tests : 14 [WpfFact] PistesHistoriqueTests (trace de rendu) + 3 faits HistoriqueConvertersTests"
affects: [34-06 vue Semaine, 34-07 vue Jour, 34-08 honnêteté et anti-mutisme, 35 vue 4 semaines]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Piste = FrameworkElement à OnRender scellé dans une souche (PisteBase) ; toute DP déclarée par PisteBase.Rendu (AffectsRender + compteur d'invalidations) ; StreamGeometry / Pen gelés ; PushClip + GuidelineSet 0,5 px"
    - "Brosses par DP à défaut null (une primitive sans brosse est sautée, jamais la piste) ; tailles par DP double à 0 ; rampe = ChronosTheme.ArcBrush du thème actif par la DP Rampe ; épuisé = DP Gris, jamais ArcBrush(1.0)"
    - "Trace de rendu (mots + fractions de plage) lue par les tests au lieu des pixels : robuste au rendu logiciel, et elle dit la brosse qui a PEINT"
    - "Test « jamais au tick » niveau contrôle : compteur d'OnRender immobile pendant 60 changements de la surcouche (RenderTargetBitmap.Render ne ré-invoque pas OnRender d'un visuel non invalidé)"
    - "Convertisseurs multi-valeurs tolérants (UnsetValue / null / NaN → 0) pour poser les annotations en XAML pur"

key-files:
  created:
    - src/Chronos/Controls/Historique/PisteBase.cs
    - src/Chronos/Controls/Historique/PisteNiveau.cs
    - src/Chronos/Controls/Historique/PisteRythme.cs
    - src/Chronos/Controls/Historique/PisteTokens.cs
    - src/Chronos/Controls/Historique/PisteCouverture.cs
    - src/Chronos/Controls/Historique/PisteFenetres5h.cs
    - src/Chronos/Controls/Historique/SurcoucheReticule.cs
    - src/Chronos/Converters/HistoriqueConverters.cs
    - tests/Chronos.Tests/PistesHistoriqueTests.cs
    - tests/Chronos.Tests/HistoriqueConvertersTests.cs
  modified:
    - .planning/REQUIREMENTS.md (HIS-07 → Complete)

key-decisions:
  - "D-34-17 — PisteBase scelle OnRender : RendusPourTests++, trace vidée, garde w/h/Plage, PushClip, GuidelineSet 0,5 px gelé, puis Dessiner(dc, w, h) abstrait ; trace de rendu activée par TracerPourTests"
  - "D-34-18 — DP de brosse à défaut null, DP de taille à 0,0 : une piste non bindée est invisible, jamais fausse ; le pinceau transparent de la surcouche est le seul pinceau nommé"
  - "D-34-19 — Statut5 == Rejete dessiné comme U5 = 1,0 en Jour → bande « épuisé » peinte par la DP Gris, pas par la rampe"
  - "D-34-20 — la semaine précédente est projetée par fraction de SA plage (EchelleTemps.X(t, PlagePrecedente, w)), pointillé Gris, jamais colorée"
  - "D-34-21 — la piste Tokens montre le compteur de sortie ; plafond = DP Plafond (long) bindée au VM ; rang de modèle ≥ 2 → Modele3"
  - "Open Question 4 tranchée : RenderTargetBitmap.Render ne ré-invoque PAS OnRender d'un visuel non invalidé → compteur d'OnRender retenu ; InvalidationsPourTests (callback commun des DP) tenu en second témoin"
  - "La trace dit la brosse qui a peint (ReferenceEquals(brosse, Gris) → gris / grise), pas le niveau supposé : les mutations « rampe à 1,0 » rougissent"
  - "PisteTokens.InstantLecture (DateTimeOffset?, null = sans limite) : les heures postérieures à la lecture ne sont ni dessinées ni hachurées — le futur est vide, pas « transcripts absents » (note 34-03)"

patterns-established:
  - "Une souche par famille de contrôles OnRender (PisteBase.Rendu) : le compteur d'invalidations et la trace viennent gratuitement pour toute piste future (phase 35)"
  - "Mutations par copie dans l'instantané snap-34-04, révocation par re-copie depuis l'arbre réel, sha256 comparés — le fichier réel ne bouge jamais"

requirements-completed: [HIS-07]

# Metrics
duration: ~35 min (lecture 13:40Z → dernier commit de tâche 14:06Z ; SUMMARY 14:20Z)
completed: 2026-09-27
---

# Phase 34 Plan 04 : Pistes de la fenêtre Historique Summary

**Six `FrameworkElement` à `OnRender` scellé dans une souche commune (`PisteBase` : compteur de rendus, trace, clip, guides, plumes et escaliers gelés) — Niveau (Semaine complète / hebdo seul / Jour), Rythme, Tokens, Couverture, Fenêtres 5 h, plus la surcouche réticule / infobulle / « maintenant » — qui ne calculent rien (34-02), ne choisissent aucune couleur (brosses par DP à défaut `null`, rampe par `ChronosTheme.ArcBrush`, épuisé par la DP `Gris`), réduisent les dents 5 h par min/max au-delà de 4 000 points, et restent immobiles pendant 60 changements de la surcouche (compteur d'`OnRender` prouvé) ; trois convertisseurs XAML pour les annotations ; 17 tests, 9 mutations rougies puis révoquées, suite 1531 / 1531 deux fois sur l'arbre réel, 0 warning — HIS-07 clos.**

SHA d'entrée : `56772cf` (1499 verts). HIS-07 est **clos** ici : tokens (34-01) + géométrie pure (34-02) + VM sans redessin au tick (34-03) + pistes `OnRender` (ce plan) ; `requirements mark-complete HIS-07` exécuté.

## Performance

- **Duration:** ~35 min
- **Started:** 2026-09-27 ≈ 13:40Z (RED 1 commité 13:52Z)
- **Completed:** 2026-09-27 14:20Z
- **Tasks:** 2 (TDD : 4 commits)
- **Files modified:** 10 créés (948 lignes de source, 684 lignes de tests), 1 modifié (REQUIREMENTS.md)

## Accomplishments

- **`PisteBase`** (D-34-17, D-34-18) : DP `Plage` / `Grille` / `EpaisseurFine` ; `Rendu(nom, type, proprietaire, defaut)` déclare toute DP `AffectsRender` avec un callback commun qui compte `InvalidationsPourTests` ; `OnRender` scellé (`RendusPourTests++`, trace vidée, garde `w <= 0 || h <= 0 || Plage is null`, `PushClip`, `GuidelineSet` 0,5 px gelé, `Dessiner`, `Pop` × 2) ; `Plume` / `PlumePointillee` (tirets 3 / 2) gelées ; `GeometrieEscalier` (paliers + jonction verticale seulement si `T1 == T0 suivant`) gelée ; `DessinerGrille`.
- **`PisteNiveau`** : Semaine complète = grille 0 / 0,5 / 1 → fantôme S-1 (D-34-20) → trous (fond à `OpaciteTrou`, bordure pointillée `BordTrouArrete` si arrêté / inconnue, `BordTrouJeton` sinon) → sauts hebdo (bloc `Gris`, Δ nul sauté) → dents 5 h (`Segments(U5)`, `Binning.DoitReduire(Serie.Count)` → `ReductionMinMax` en traits verticaux, sinon escalier) → tirets de reset (`LongueurTiretReset`) → escalier hebdo `ParBandes(…, NbBandes)` (un pinceau gelé par bande, `Niveau ≥ 1 → Gris`) → cadres de divergence (pointillé `CadreDivergence`, marge = `EpaisseurEscalier + EpaisseurFine`). Hebdo seul = sans dents ni tirets. Jour = grille → trous → sauts 5 h → hebdo trait fin (`TraitFin`) → traits de reset pleine hauteur → 5 h premier plan (`Rejete → 1,0`, D-34-19, épaisseur `EpaisseurPremierPlan`).
- **`SurcoucheReticule`** : rectangle transparent (hit-test), `OnMouseMove → Survoler(x)`, `OnMouseLeave → Quitter()` ; `Survoler` = `EchelleTemps.Instant` → `Reticule.PlusProche` → DP lecture seule `InstantSurvole` / `XReticule` / `TexteInfobulle` (`TextesHistorique.Infobulle(r, Maintenant ?? InstantLecture, Fuseau)`) / `InfobulleVisible` ; ligne « maintenant » sous `OpaciteMaintenant` si `AfficherMaintenant && Plage.Contient(Maintenant)`.
- **`PisteRythme`** : `Binning.DeltasParHeure(Deltas, Serie, U5, Plage)` → un rectangle par barre, couleur `Rampe.ArcBrush(NiveauAtteint)`, hauteur bornée à `Max` (défaut `EchelleValeur.MaxRythme`), un souffle d'`EpaisseurFine / 2` entre barres.
- **`PisteTokens`** (D-34-21) : Semaine = principal puis sous-agents empilés ; Jour (`Colonnes` prioritaire) = quart d'heure (`TrancheTokens.Tranche`) empilé par modèle dans l'ordre des parts ; état ≠ `Couverte` → `Hachure` pleine hauteur, jamais un zéro muet ; couverte et vide → rien ; `Plafond ≤ 0 → 1` ; heures ≥ `InstantLecture` ignorées.
- **`PisteCouverture`** : bande `Present` à `OpacitePresent` du premier au dernier relevé ; un rectangle opaque par `RectangleTrou` (`Arrete` si arrêté / inconnue, `Jeton` si jeton / sonde) ; trou ouvert fermé à `InstantLecture`.
- **`PisteFenetres5h`** : `Tuiles5h.Depuis` → tuile `Rampe.ArcBrush(UMax)` ou `Gris` si épuisée (sans `Gris`, sautée), tiret `TraitReset` à `X(Fin)` si `ResetDansPlage`.
- **Convertisseurs** : `InstantVersXConverter` (x borné à `[0, largeur]`), `LargeurIntervalleConverter` (fin absente → 5ᵉ valeur, largeurs entre abscisses bornées), `FractionVersLargeurConverter` ; `ConvertBack → NotSupportedException`.

## Task Commits

| Tâche | Commit | Message |
|---|---|---|
| 1 RED | `e04ce27` | test(34-04): pistes - rien sans donnees ni brosses, compteur de rendus immobile…, RED 8 |
| 1 GREEN | `de39c38` | feat(34-04): PisteBase (OnRender scelle, compteur, trace de test, plumes gelees), PisteNiveau (…), SurcoucheReticule (…) (HIS-07, HIS-02) |
| 2 RED | `3abc5ca` | test(34-04): rythme colore au niveau atteint, tokens empiles et hachures…, convertisseurs d'annotations, RED 9 |
| 2 GREEN | `c58c3b6` | feat(34-04): PisteRythme, PisteTokens (…), PisteCouverture, PisteFenetres5h ; convertisseurs (HIS-07, HIS-02, HIS-03) |

Les deux RED sont des RED de compilation (types absents), nommés par `dotnet build` : `Chronos.Controls.Historique` / `PisteBase` / `PisteNiveau` / `VariantePisteNiveau` / `SurcoucheReticule` (Task 1) ; `PisteRythme` / `PisteTokens` / `PisteCouverture` / `PisteFenetres5h` / les trois convertisseurs (Task 2).

## Open Question 4 tranchée — variante retenue pour « jamais au tick »

Sonde : deux `Rendre(piste)` consécutifs (Measure / Arrange / `RenderTargetBitmap.Render`) sans changement laissent `RendusPourTests == 1`. **`RenderTargetBitmap.Render` ne ré-invoque pas `OnRender` d'un visuel non invalidé** : le compteur d'`OnRender` est donc la mesure retenue. Le test `Le_compteur_de_rendus_ne_bouge_pas_quand_seule_la_surcouche_change` fait bouger la surcouche 60 fois (`Maintenant`, `AfficherMaintenant`, `Survoler(i × 10)`, chaque fois rendue : son compteur monte) et vérifie que la piste Niveau complète garde `RendusPourTests == avant` ET `InvalidationsPourTests == avant` ; `Une_nouvelle_reference_de_donnees_redessine` vérifie `+1` sur les deux compteurs à la nouvelle référence d'`Analyse`. La mutation (p1') (`piste.InvalidateVisual()` × 60) rougit le test : il compte bien des rendus.

À 0 × 0, 1 × 1 et 840 × 0, WPF appelle `OnRender` (compteur à 1) et la garde de `PisteBase` rend sans rien dessiner — aucune exception, aucun `NaN` dans la trace.

## Table des DP par piste (contrat XAML de 34-06 / 34-07)

Toutes `AffectsRender` sauf mention ; « — » = défaut `null`. Les brosses de tokens sont indiquées à titre de binding attendu (DESIGN_PLAN §2.2 / §2.3).

**`PisteBase` (communes)** : `Plage` (`Plage?`, —) · `Grille` (`Brush?`, — ; `Line`) · `EpaisseurFine` (`double`, 0 ; `HistoEpaisseurFin`).

**`PisteNiveau`** : `Analyse` (`AnalyseJournal?`) · `Precedente` (`AnalyseJournal?`) · `PlagePrecedente` (`Plage?`) · `Divergences` (`IReadOnlyList<Divergence>?`) · `InstantLecture` (`DateTimeOffset`, défaut) · `Variante` (`VariantePisteNiveau`, `SemaineComplete`) · `Rampe` (`ChronosTheme?` ; `vm.Theme`) · `TraitReset` (`Brush?` ; `TickReset`) · `TraitFin` (`Brush?` ; `Ink2`) · `Gris` (`Brush?` ; `HistoGris`) · `FondTrou` (`Brush?` ; `Line`) · `BordTrouArrete` (`Brush?` ; `HistoGris`) · `BordTrouJeton` (`Brush?` ; `Alerte`) · `CadreDivergence` (`Brush?` ; `Accent`) · `EpaisseurEscalier` (`double`, 0 ; `HistoEpaisseurEscalier`) · `EpaisseurPremierPlan` (`double`, 0 ; `HistoEpaisseurPremierPlan`) · `LongueurTiretReset` (`double`, 0 ; `HistoLongueurTiretReset`) · `OpaciteTrou` (`double`, 0 ; `HistoOpaciteTrou`) · `NbBandes` (`int`, 12).

**`PisteRythme`** : `Deltas` (`IReadOnlyList<DeltaConsommation>?` ; `Analyse.Deltas5h`) · `Serie` (`IReadOnlyList<ReleveJournal>?`) · `Rampe` (`ChronosTheme?`) · `Max` (`double`, `EchelleValeur.MaxRythme` = 0,25).

**`PisteTokens`** : `Barres` (`IReadOnlyList<BarreHeure>?`) · `Colonnes` (`IReadOnlyList<ColonneQuartDHeure>?`, prioritaire) · `Plafond` (`long`, 1 ; `vm.PlafondTokens`) · `InstantLecture` (`DateTimeOffset?`, — = sans limite ; `LueA`) · `Principal` (`HistoTokens`) · `SousAgents` (`HistoSousAgent`) · `Modele1` / `Modele2` / `Modele3` (`HistoModele1/2/3`) · `Hachure` (`HistoHachure`).

**`PisteCouverture`** : `Serie` · `Trous` (`IReadOnlyList<Trou>?`) · `InstantLecture` (`DateTimeOffset`) · `Present` (`Ok`) · `OpacitePresent` (`double`, 0 ; `HistoOpaciteCouverture`) · `Arrete` (`Line`) · `Jeton` (`Alerte`).

**`PisteFenetres5h`** : `Serie` · `Rampe` · `Gris` (`HistoGris`) · `TraitReset` (`TickReset`).

**`SurcoucheReticule`** : `Serie` · `Maintenant` (`DateTimeOffset?`) · `AfficherMaintenant` (`bool`, false) · `InstantLecture` (`DateTimeOffset`) · `Fuseau` (`TimeZoneInfo?` ; `vm.Fuseau`) · `TraitReticule` (`Ink2`) · `TraitMaintenant` (`Ink`) · `OpaciteMaintenant` (`double`, 0 ; `HistoOpaciteMaintenant`). **Lecture seule** : `XReticule` (`double`, `AffectsRender`) · `TexteInfobulle` (`string`, "") · `InfobulleVisible` (`bool`) · `InstantSurvole` (`DateTimeOffset?`) — les trois dernières sans `AffectsRender` (elles nourrissent le `Border` d'infobulle de la vue). `internal Survoler(double x)` / `Quitter()`.

Le test `Les_brosses_des_pistes_sont_des_DP_sans_couleur_par_defaut` parcourt par réflexion TOUTES les classes concrètes dérivées de `PisteBase` (une piste ajoutée en phase 35 est couverte d'office).

## Vocabulaire de la trace de rendu (pour 34-08)

Fractions = `EchelleTemps.Fraction(t, Plage)` à trois décimales (invariant) ; `u`, `niveau`, `delta`, `umax` à trois décimales ; ordonnées et hauteurs en pixels à trois décimales.

| Ligne | Piste | Sens |
|---|---|---|
| `grille f` | toutes | ligne horizontale à la fraction `f` (0 = bas) |
| `fantome n` | Niveau | escalier S-1 de `n` paliers |
| `trou f0 f1 Cause` | Niveau | rectangle de trou, `Cause` = nom de l'enum `CauseTrou` |
| `saut f0 f1 bas haut` | Niveau | bloc plat gris du saut |
| `dents n` / `reduction n colonnes` | Niveau (Semaine complète) | dents 5 h en escalier (`n` paliers) ou réduites (`n` colonnes) |
| `tiret f` | Niveau (Semaine complète) | tiret de reset 5 h en bas de piste |
| `bande k niveau [premierplan ]rampe\|gris epaisseur` | Niveau | une bande dessinée, `k` = index, `epaisseur` = plume |
| `palier f0 f1 u [premierplan ]rampe\|gris` | Niveau | un palier de la bande (nature = brosse qui a peint) |
| `divergence f0 f1` | Niveau | cadre pointillé |
| `hebdo n` / `trait f` | Niveau (Jour) | hebdo trait fin ; trait de reset pleine hauteur |
| `reticule f` / `maintenant f` | Surcouche | ligne verticale à `f` |
| `barre f0 f1 delta niveau` + `hauteur px` | Rythme | une barre et sa hauteur dessinée (bornée) |
| `tokens f0 f1 principal sousagents` + `empile ySommet yPrincipal` | Tokens (Semaine) | barre empilée (entiers) et ses ordonnées |
| `modele rang=r brosse=ModeleN f0 f1 sortie` | Tokens (Jour) | une part de modèle |
| `hachure f0 f1` | Tokens | zone hors couverture / transcripts absents |
| `present f0 f1 opacite` / `trou f0 f1 arrete\|jeton` | Couverture | bande présente ; trou par cause |
| `tuile f0 f1 umax grise\|rampe hauteur` / `tiret f` | Fenêtres 5 h | tuile (nature = brosse qui a peint) ; tiret de reset |

## Mutations jouées (par copie dans `snap-34-04`, révoquées par re-copie, sha256 identiques)

| # | Mutation | Rouge(s) nommé(s) | sha256 (16 premiers) avant = après |
|---|---|---|---|
| p1' | `piste.InvalidateVisual()` × 60 dans le test du compteur | `Le_compteur_de_rendus_ne_bouge_pas_quand_seule_la_surcouche_change` | tests `0e66ce7f0c4100d2` |
| p2 | `Segments(…, Array.Empty<Trou>(), U7)` dans l'escalier hebdo | `L_escalier_de_niveau_ne_traverse_pas_les_trous_et_les_bandes_ont_leur_couleur` | `fcbb9ded344df1c5` |
| p3 | bande épuisée du Jour peinte par `Rampe.ArcBrush(1.0)` | `La_variante_Jour_met_le_5h_au_premier_plan_et_grise_l_epuisee` | `fcbb9ded344df1c5` |
| p4 | défaut `new SolidColorBrush(Color.FromRgb(0x5A, 0x59, 0x60))` sur `Gris` | `Les_brosses_des_pistes_sont_des_DP_sans_couleur_par_defaut` ET `GardeTokensHistoriqueTests.Aucune_couleur_ni_taille_en_dur…` | `fcbb9ded344df1c5` |
| q1 | barre `TranscriptsAbsents` dessinée comme un zéro (pas de hachure) | `Les_tokens_empilent_principal_et_sous_agents_et_hachurent_le_hors_couverture` | `c833a69b19490295` |
| q2 | empile `In + Out` (doctrine 33-04 : jamais une somme) | `Les_tokens_empilent…` (hauteur `empile`) | `c833a69b19490295` |
| q3 | tuile épuisée peinte par `Rampe.ArcBrush(1.0)` | `Les_tuiles_finissent_au_reset_et_grisent_l_epuisee` | `fda2d8e97dc36c58` |
| q4 | `SondeRefusee` peinte en `arrete` | `La_couverture_peint_present_arrete_et_jeton` | `6b866cfb1fd95cd7` |
| c1 | `InstantVersXConverter` non borné | `InstantVersX_borne_a_la_largeur` | `b75ba3b7284a223b` |

Aucune mutation n'a produit d'erreur de compilation ; chaque rouge est le test attendu, seul (p4 : les deux attendus).

## Verification

- **Arbre réel** (`dotnet test Chronos.sln -c Debug --nologo -v q`) : **1531 / 1531 verts, deux exécutions consécutives** (14:09:06Z et 14:09:48Z ; 1499 d'entrée + 17 de ce plan + 15 de 34-05 déjà fusionnés). `dotnet build Chronos.sln -c Debug` : **0 warning**.
- **Instantané `snap-34-04`** (`git archive 56772cf` + les 10 fichiers du plan, aucun fichier de 34-05) : **1516 / 1516 deux fois de suite**, 0 warning — c'est là qu'ont tourné la boucle TDD, les filtres et les 9 mutations pendant que le RED de 34-05 (`HistoriqueWindow.xaml` : `EnTete_Drag` absent) cassait la compilation du projet applicatif sur l'arbre réel.
- Greps d'acceptation : `internal int RendusPourTests` 1 · `protected sealed override void OnRender` 1 · `protected abstract void Dessiner(` 1 · `PushClip(` 1 · `GuidelineSet` 5 · `Escalier.ParBandes(` 2 · `Escalier.RectanglesTrous(` 1 (Niveau) + 1 (Couverture) · `Escalier.BlocsSauts(` 1 · `Binning.DoitReduire(` 1 · `Rampe.ArcBrush(` 2 · `StatutServeur.Rejete ? 1.0` 1 · `PlagePrecedente` 3 · `.Freeze()` 5 · `Reticule.PlusProche(` 1 (Surcouche) · `TextesHistorique.Infobulle(` 1 · `DependencyPropertyKey` 4 · `Brushes.Transparent` 1 · `Binning.DeltasParHeure(` 1 · `EchelleValeur.MaxRythme` 1 · `Tuiles5h.Depuis(` 1 · `EtatCouverture.Couverte` 2 · `.Out` 3 lignes · `\.In\b|CacheW|CacheR` 0 · `TrancheTokens.Tranche` 1 · 3 classes `IMultiValueConverter` · `EchelleTemps.` 3 · 7 fichiers sous `Controls/Historique` · couleurs en dur : aucune · horloge / dispatcher : aucun · `[WpfFact]` 14 · faits convertisseurs 3 · `[Collection` 0.
- Périmètre : `git diff --stat 56772cf HEAD` restreint à mes 10 fichiers = 1632 insertions ; aucun de mes 4 commits ne touche `Views/`, `App.xaml.cs`, `ViewModels/`, `Resources/`, `Rendering/`, `Services/`, `ROADMAP.md`, `STATE.md`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Correctness] DP `InstantLecture` (`DateTimeOffset?`) sur `PisteTokens`**
- **Found during:** Task 2 (test des tokens sur le scénario réel)
- **Issue:** le plan attend « aucune ligne `hachure` (tout couvert dans le scénario) », mais 34-03 documente que `GarantirPasse(now, now)` laisse les heures à VENIR en `TranscriptsAbsents` ; sans filtre, la piste aurait hachuré tout le futur comme des transcripts manquants (contraire à la note « le futur est simplement vide »).
- **Fix:** DP `InstantLecture` (`null` = sans limite) ; barres / colonnes dont le début est ≥ à l'instant de lecture ne sont ni dessinées ni hachurées. 34-06 / 34-07 la bindent à `LueA`.
- **Files modified:** `PisteTokens.cs`, `PistesHistoriqueTests.cs`
- **Commit:** `c58c3b6`

**2. [Rule 1 - Bug] La trace dit la brosse qui a peint**
- **Found during:** Task 1 (préparation de la mutation p3)
- **Issue:** une nature de trace déduite du niveau (`Niveau ≥ 1 → "gris"`) resterait « gris » même si la bande était peinte par la rampe : p3 et q3 n'auraient pas rougi.
- **Fix:** `ReferenceEquals(brosse, Gris)` dans `PisteNiveau` et `PisteFenetres5h`.
- **Commit:** `de39c38`, `c58c3b6`

**3. [Rule 1 - Test] Réflexion et arrondis**
- La réflexion sur les DP ramassait `Style`, `Width`… hérités de `FrameworkElement` (`FlattenHierarchy`) : filtre `typeof(PisteBase).IsAssignableFrom(dp.OwnerType)`. Les comparaisons de fractions passent en tolérance absolue 0,001 (la trace arrondit à trois décimales ; « 2 décimales » chevauchait le pas de 5 min).

**4. [Rule 3 - Blocking] Instantané `snap-34-04`**
- Le RED de compilation du voisin 34-05 (`HistoriqueWindow.xaml` référençant `EnTete_Drag` avant le code-behind) cassait le projet applicatif sur l'arbre réel ; prévu par les règles d'exécution : TDD, filtres et mutations dans l'instantané, puis suite complète deux fois sur l'arbre réel une fois le voisin GREEN.

### Précisions prises à l'exécution (pas des écarts de contrat)

- Les traces `bande` portent l'épaisseur de la plume (`2.200` / `2.400`) et les paliers du Jour la marque `premierplan` : c'est ainsi que le test vérifie `EpaisseurPremierPlan`.
- La marge du cadre de divergence est `EpaisseurEscalier + EpaisseurFine` (pas « 3 px » en dur : aucune taille dans une piste).
- Un bloc de saut de Δ nul (`Haut <= Bas`) n'est pas peint (rectangle de hauteur nulle) et n'est pas tracé.
- Le « souffle » entre barres du Rythme vaut `EpaisseurFine / 2` ; une barre plus étroite reçoit au moins `EpaisseurFine` de large.
- `LargeurIntervalleConverter` calcule la largeur entre abscisses BORNÉES à la piste (cohérence avec `InstantVersXConverter` pour une annotation qui déborde).
- Un test supplémentaire (`Au_dela_du_seuil_la_piste_reduit_les_dents_par_min_max`, 4 001 relevés construits) couvre la vérité « réduction min/max au-delà de 4 000 points » du must-have — 8 tests en Task 1 au lieu de 7.

## Known Stubs

Aucun. Les pistes ne portent aucune valeur factice ; sans binding elles ne dessinent rien (volontaire, D-34-18). Les bindings eux-mêmes (tokens → DP, VM → DP) sont l'objet de 34-06 / 34-07.

## Issues Encountered

- Fichiers de 34-05 (`Views/Historique/*`, `App.xaml.cs`, `PlacementHistorique`…) présents et parfois en RED sur l'arbre partagé : non touchés, non stagés ; l'arbre était propre (`git status --short` vide) au moment des deux passes réelles.
- Le critère `.Out ≥ 3` compte des lignes : la déclaration `long principal = …, sousAgents = …;` a été scindée en deux lignes (aucun changement de comportement).

## Next Phase Readiness

- 34-06 / 34-07 posent les pistes en XAML : `Plage="{Binding DonneesSemaine.Plage}"`, `Analyse="{Binding DonneesSemaine.Analyse}"`, `Rampe="{Binding Theme}"`, brosses `{StaticResource …}` / `{DynamicResource TickReset}` (Pitfall 5), tailles `{StaticResource HistoEpaisseur…}`, `Plafond="{Binding PlafondTokens}"`, `InstantLecture="{Binding DonneesSemaine.LueA}"` ; la surcouche en `Grid.RowSpan` avec `Fuseau="{Binding Fuseau}"`, `Maintenant="{Binding Maintenant}"`, `AfficherMaintenant="{Binding AfficherMaintenant}"` ; l'infobulle = `Border` bindé à `InfobulleVisible` / `TexteInfobulle` / `XReticule`.
- 34-08 : l'anti-mutisme de la garde peut passer à « ≥ 7 cs » (7 fichiers présents) ; les tests d'honnêteté de bout en bout peuvent lire la trace de rendu (vocabulaire ci-dessus) sur `ScenariosHistorique`.
- `HistoriqueViewModel.PlafondJoli` reste à remplacer par `EchelleValeur.MaxArrondi` (stub connu de 34-03) pour que la DP `Plafond` et l'échelle textuelle viennent de la même règle.

## Self-Check: PASSED

- 11 fichiers vérifiés présents (7 pistes, 1 convertisseurs, 2 tests, ce SUMMARY).
- 4 commits vérifiés dans l'historique (`git cat-file -e`) : `e04ce27`, `de39c38`, `3abc5ca`, `c58c3b6`.
- `requirements mark-complete HIS-07` exécuté par ce plan (`updated: true`) ; la ligne modifiée de `REQUIREMENTS.md` (HIS-07 → `[x]` / `Complete`) a été emportée par le commit de clôture du voisin `7d7f8e8` (docs 34-05), l'arbre partagé étant stagé par lui entre mon marquage et ce commit — HEAD porte bien HIS-07 complet.
- `ROADMAP.md` / `STATE.md` : non modifiés par ce plan (exécution parallèle : `state *` et `roadmap update-plan-progress` laissés à l'orchestrateur).
