---
phase: 34-fen-tre-historique-semaine-et-jour
plan: 02
subsystem: géométrie pure des pistes de la fenêtre Historique (Rendering/Historique : échelles, escalier, binning, tuiles 5 h, réticule)
tags: [rendering, geometrie-pure, sans-wpf, escalier, trous, bandes-de-rampe, binning, min-max, tuiles-5h, reticule, fixtures-32-06, tdd, mutations]

# Dependency graph
requires:
  - phase: 32-06 (JRN-05)
    provides: Plage ([Debut, Fin[, Duree), ReleveJournal, Trou, ResetObserve, DeltaConsommation (Anormal), SautNonLocalise, AnalyseJournal ; LecteurJournal.Lire, AnalyseReleves.Analyser, BornesPlage (169 h / 25 h) ; fixtures trou-arrete, reset-5h-milieu-heure ; FabriqueJournal.JourneeNominale
  - phase: 18-19 (HDR)
    provides: RateLimitHeaderUsageProvider.CadenceNominale (300 s), StatutServeur.Rejete, WindowKind
provides:
  - Rendering/Historique/EchelleTemps.cs — Fraction (non bornée, durée réelle), X, Largeur (≥ 0), Instant (inverse borné à [Debut, Fin])
  - Rendering/Historique/EchelleValeur.cs — Y (axe inversé, borné, NaN/max ≤ 0 → plancher), MaxRythme = 0,25, MaxArrondi (D-34-12, entiers exacts)
  - Rendering/Historique/Escalier.cs — SegmentPalier, BandeRampe, RectangleTrou, BlocSaut ; Segments (D-34-08, coupés aux trous), ParBandes (D-34-09), RectanglesTrous, BlocsSauts
  - Rendering/Historique/Binning.cs — BarreDelta, PointReduit ; SeuilReduction = 4000, DoitReduire, DeltasParHeure (D-34-10), ReductionMinMax (D-34-11)
  - Rendering/Historique/Tuiles5h.cs — Tuile5h ; Depuis(serie, plage) (groupes consécutifs de même R5, bornées au reset observé, épuisées)
  - Rendering/Historique/Reticule.cs — PlusProche(serie, t) (recherche binaire, égalité → le plus ancien)
  - 26 tests [Fact] (EchellesHistoriqueTests 7, ReticuleTests 3, EscalierTests 7, BinningTests 5, Tuiles5hTests 4) sur fixtures 32-06 + séries en mémoire
affects: [34-04 pistes OnRender, 34-06 vue Semaine, 34-07 vue Jour, 34-08 honnêteté, 35 vue 4 semaines]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Géométrie pure sous Rendering/Historique : classes statiques, doubles nus et records neutres (aucun System.Windows), testées en [Fact] comme ArcGeometry/DayTimeline"
    - "Le fuseau n'existe pas dans la géométrie : la Plage vient de BornesPlage, les instants sont des DateTimeOffset ; l'échelle suit la DURÉE RÉELLE de la plage (169 h, 25 h)"
    - "Un trou est vide de géométrie par construction des données (palier de longueur nulle au Debut du trou), pas par une vérification au dessin"
    - "Mutations par copie dans l'instantané snap-34-02 (git archive 47904ff), révocation par re-copie depuis l'arbre réel, sha256 comparés"

key-files:
  created:
    - src/Chronos/Rendering/Historique/EchelleTemps.cs
    - src/Chronos/Rendering/Historique/EchelleValeur.cs
    - src/Chronos/Rendering/Historique/Escalier.cs
    - src/Chronos/Rendering/Historique/Binning.cs
    - src/Chronos/Rendering/Historique/Tuiles5h.cs
    - src/Chronos/Rendering/Historique/Reticule.cs
    - tests/Chronos.Tests/EchellesHistoriqueTests.cs
    - tests/Chronos.Tests/ReticuleTests.cs
    - tests/Chronos.Tests/EscalierTests.cs
    - tests/Chronos.Tests/BinningTests.cs
    - tests/Chronos.Tests/Tuiles5hTests.cs
  modified: []

key-decisions:
  - "D-34-08 — un palier va de T[i] à T[i+1], sauf si T[i] est le Debut d'un Trou (palier de longueur nulle : la ligne s'interrompt, rien n'est interpolé) ; le dernier relevé fait un point ; tout est borné à Plage.Fin ; un relevé à T ≥ Plage.Fin est omis"
  - "D-34-09 — bandes de rampe : nbBandes bandes égales sur [0, 1[ + une bande « épuisé » séparée pour U ≥ 1 ; Niveau = (k + 0,5)/nb ou 1,0 ; c'est ce Niveau que la piste passe à ChronosTheme.ArcColor (un Pen gelé par bande)"
  - "D-34-10 — le Rythme ne somme que les Δ POSITIFS non Anormal (une anomalie n'est pas une consommation) ; case = heure entière depuis Plage.Debut, rangée par l'instant d'ARRIVÉE A ; NiveauAtteint = max du sélecteur des relevés de la case"
  - "D-34-11 — réduction min/max : points triés par X, colonnes = largeur en pixels entiers, X de sortie = centre de colonne ; appliquée par la piste seulement si DoitReduire(n) (n > 4000)"
  - "D-34-12 — MaxArrondi : plafond « joli » parmi {1 ; 1,2 ; 1,5 ; 2 ; 2,5 ; 3 ; 4 ; 5 ; 6 ; 8} × 10^k, calculé en entiers (1 150 000 → 1 200 000 exact)"
  - "La longueur d'un palier suit la SÉRIE, pas le sélecteur : un relevé sans U7 ne fait pas de palier hebdo mais borne quand même le précédent"
  - "Tuiles5h : relevés sans R5 ou sans U5 filtrés AVANT le regroupement ; un resets_at antérieur au relevé donne une tuile de largeur nulle, jamais négative"

patterns-established:
  - "Un instantané par plan (snap-34-02) pour les filtres et les mutations pendant que les RED voisins cassent le projet de tests partagé ; suite complète sur l'arbre réel dès qu'il compile"

requirements-completed: []   # HIS-02 est clos par 34-06 (vue Semaine assemblée) — plan contributeur, pas de mark-complete ici

# Metrics
duration: 24min
completed: 2026-09-27
---

# Phase 34 Plan 02 : Géométrie pure de l'Historique Summary

**Six classes pures sous `Rendering/Historique` (échelles temps → x / valeur → y justes sur 169 h, escalier coupé aux trous et découpé par bande de rampe, rectangles de trous, blocs de sauts, Δ par heure sans les anormaux, réduction min/max > 4 000, tuiles 5 h bornées au reset observé, relevé le plus proche), 26 `[Fact]` sur les fixtures de 32-06, 11 mutations rougies puis révoquées — une piste de 34-04 n'aura plus une ligne d'arithmétique de temps ou de valeur.**

## Performance

- **Duration:** 24 min
- **Started:** 2026-09-27T13:01:04Z
- **Completed:** 2026-09-27T13:25:00Z
- **Tasks:** 3 (TDD : 6 commits)
- **Files modified:** 11 créés, 0 modifiés (442 lignes de source, 625 lignes de tests)
- **SHA d'entrée :** `47904ff` (1 415 tests verts)

## Accomplishments

- **Échelles** : `EchelleTemps.Fraction/X/Largeur/Instant` sur la durée RÉELLE de la `Plage` — le mardi minuit de la semaine du 25/10/2026 est à 73/169 de la largeur (test explicite « et PAS 3/7 ») ; `Instant` est l'inverse borné de `X`, `X`/`Fraction` ne sont pas bornés (semaine précédente, « maintenant ») ; plage vide → 0 sans exception. `EchelleValeur.Y` inverse l'axe WPF et borne ; `MaxRythme = 0,25` épinglé ; `MaxArrondi` en entiers exacts.
- **Escalier** (HIS-06 côté structure) : sur `trou-arrete`, 7 paliers pour 7 relevés, le palier de 10:10Z a une longueur nulle, le suivant repart à 11:03Z, aucun palier n'entre dans `]Debut, Fin[` d'un trou — ni en 5 h ni en hebdo. `ParBandes` sépare toujours la bande « épuisé » (0,99 → bande 11, 1,0 → bande épuisé). `RectanglesTrous` borne à la plage et ferme un trou ouvert à `now` ; `BlocsSauts` donne `(0,14 → 0,20)` et `(0,41 → 0,43)` sur la durée du trou, et RIEN pour un saut indéterminable.
- **Binning** : sur `reset-5h-milieu-heure`, somme des barres = 0,09 (le −0,01 `Anormal` exclu), `NiveauAtteint` 0,86 ; un Δ arrivé à 10:03 est dans la case 10:00 même s'il part de 09:58 ; 25 cases le 25/10/2026 (`BornesPlage.Jour`, 25 h) ; 4 001 points → ≤ 800 colonnes, le pic Y = 9 survit, min global conservé.
- **Tuiles 5 h** : la première tuile de `reset-5h-milieu-heure` finit EXACTEMENT à `ResetObserve.Instant` (13:37Z), la seconde commence à `ObserveA` (13:40Z) et est coupée à `Plage.Fin` ; épuisée par compteur (1,0) OU par statut `Rejete` (0,8 refusé → épuisée) ; la journée nominale de 288 relevés donne 5 tuiles contiguës (`t[i].Fin == t[i+1].Debut`), la dernière coupée à minuit avec `ResetDansPlage == false`.
- **Réticule** : distance minimale (au milieu d'un trou, le relevé qui SUIT gagne s'il est plus près), égalité → le plus ancien, vide → null.

## Task Commits

TDD par tâche (RED nommé → GREEN), `--no-verify`, stage explicite, jamais `--amend` :

1. **Task 1 : EchelleTemps, EchelleValeur, Reticule** — RED `e4b591a` (10 tests, compilation) → GREEN `c081c27`
2. **Task 2 : Escalier** — RED `a042993` (7 tests) → GREEN `a8797be`
3. **Task 3 : Binning et Tuiles5h** — RED `65e74ce` (9 tests) → GREEN `dd1cac8`

**Plan metadata :** commit `docs(34-02)` (SUMMARY seul — STATE.md / ROADMAP.md non touchés par ce plan, exécution parallèle).

## Mutations jouées (par copie, révoquées par re-copie, sha256 identiques)

Toutes jouées dans l'instantané `snap-34-02` (copies des fichiers de l'arbre réel), révoquées par re-copie depuis l'arbre réel ; le sha256 du fichier réel n'a jamais bougé.

| # | Mutation | Test(s) rougi(s) | sha256 (16 premiers) |
|---|----------|------------------|-----------------------|
| e1 | `Fraction` sur `TimeSpan.FromDays(7)` au lieu de `plage.Duree` | `Une_semaine_de_169_heures_place_le_mardi_minuit_a_sa_vraie_fraction` | `11809aa9a4bcc7f9` |
| e2 | `Instant` sans `Math.Clamp` | `Instant_est_borne_a_la_plage_et_X_ne_l_est_pas` | `11809aa9a4bcc7f9` |
| r1 | égalité → le plus récent (`<` au lieu de `<=`) | `A_egalite_le_plus_ancien_gagne` | `e64b725cbe790802` |
| s1 | `!debuts.Contains(releve.T)` retiré (un palier traverse le trou) | `Un_palier_va_jusqu_au_releve_suivant_et_s_arrete_au_trou` ET `Rien_n_est_interpole_a_travers_un_trou` | `bc3a7587b707e8dc` |
| s2 | `BlocsSauts` émet `Bas = Haut = Apres` pour `Delta == null` (barre au réveil) | `Les_blocs_de_sauts_ont_la_hauteur_du_delta_et_ignorent_l_indeterminable` | `bc3a7587b707e8dc` |
| s3 | bande épuisé fusionnée avec la bande `nb − 1` | `ParBandes_regroupe_par_niveau_et_isole_l_epuise` | `bc3a7587b707e8dc` |
| b1 | les Δ `Anormal` et négatifs sommés | `Les_deltas_5h_se_somment_par_heure_sans_les_anormaux` (0,08 au lieu de 0,09) | `0fa3c80771904580` |
| b2 | case par `De` au lieu de `A` | `Un_delta_est_range_dans_la_case_de_son_instant_d_arrivee` | `0fa3c80771904580` |
| b3 | `SeuilReduction = 400` | `Le_seuil_de_reduction_est_quatre_mille` | `0fa3c80771904580` |
| t1 | `Fin = R5` non borné | `Les_releves_sans_R5_ou_sans_U5_sont_ignores_et_une_tuile_hors_plage_est_omise` (+ `Une_tuile_par_resets_at…`, `La_journee_nominale…`) | `a210ea3ef1308f64` |
| t2 | `Epuisee` ignore `Statut5` | `Une_tuile_est_epuisee_si_le_serveur_refuse_ou_si_le_compteur_atteint_un` | `a210ea3ef1308f64` |

## Suite complète

- **Instantané `snap-34-02`** (arbre à `47904ff` + les 11 fichiers de ce plan) : **1 441 / 1 441** verts, deux fois de suite (1 415 + 26).
- **Arbre réel partagé** (avec les commits déjà fusionnés de 34-01 et 34-03) : **1 462 / 1 462** verts (essai 1, 13:15Z) ; l'essai 2 (13:17Z) est tombé sur le RED de compilation du voisin 34-03 (`TextesHistoriqueTests` avant `TextesHistorique`) — voir « Issues Encountered » pour l'exécution de confirmation.
- `dotnet build src/Chronos/Chronos.csproj -c Release` : **0 warning**.
- Greps d'acceptation : toutes les signatures présentes exactement une fois ; `grep -rc "System.Windows"`, `TimeZoneInfo.Local`, `DateTimeOffset.Now/UtcNow`, `FromDays(7)` sous `Rendering/Historique` : vides ; 7 / 3 / 7 / 5 / 4 `[Fact]` ; `StatutServeur.Rejete` une seule fois dans `Tuiles5h.cs` ; `JourneeNominale` une seule fois dans `Tuiles5hTests.cs` ; 6 fichiers sous `Rendering/Historique`.
- Périmètre : mes six commits ne touchent que `src/Chronos/Rendering/Historique/*` et les cinq fichiers de tests du plan — rien sous `Services/`, `Models/`, `Views/`, `Resources/`.

## Pour 34-04 : comment une piste consomme ces listes

Contrats figés (namespace `Chronos.Rendering.Historique`, aucun `using System.Windows` ; la piste transcrit en `StreamGeometry` gelées, elle ne calcule rien) :

```csharp
// Échelles — toute abscisse / ordonnée passe par là
double EchelleTemps.Fraction(DateTimeOffset t, Plage plage);                       // non bornée ; Duree ≤ 0 → 0
double EchelleTemps.X(DateTimeOffset t, Plage plage, double largeur);              // Fraction × largeur
double EchelleTemps.Largeur(DateTimeOffset debut, DateTimeOffset fin, Plage plage, double largeur);   // ≥ 0
DateTimeOffset EchelleTemps.Instant(double x, Plage plage, double largeur);        // inverse BORNÉ ; largeur ≤ 0 → Debut
const double EchelleValeur.MaxRythme = 0.25;
double EchelleValeur.Y(double valeur, double max, double hauteur);                 // hauteur − clamp(v/max) × hauteur
long   EchelleValeur.MaxArrondi(long max);                                         // 1 150 000 → 1 200 000

// Escalier
readonly record struct SegmentPalier(DateTimeOffset T0, DateTimeOffset T1, double U);
sealed record BandeRampe(double Niveau, IReadOnlyList<SegmentPalier> Segments);
readonly record struct RectangleTrou(DateTimeOffset Debut, DateTimeOffset Fin, Trou Trou);
readonly record struct BlocSaut(DateTimeOffset Debut, DateTimeOffset Fin, double Bas, double Haut, SautNonLocalise Saut);
IReadOnlyList<SegmentPalier> Escalier.Segments(IReadOnlyList<ReleveJournal> serie, IReadOnlyList<Trou> trous, Func<ReleveJournal, double?> u, Plage plage);
IReadOnlyList<BandeRampe>    Escalier.ParBandes(IReadOnlyList<SegmentPalier> segments, int nbBandes);   // nb ≤ 0 → une bande 0,5
IReadOnlyList<RectangleTrou> Escalier.RectanglesTrous(IReadOnlyList<Trou> trous, Plage plage, DateTimeOffset finSiOuvert);
IReadOnlyList<BlocSaut>      Escalier.BlocsSauts(IReadOnlyList<SautNonLocalise> sauts, Plage plage);

// Binning
readonly record struct BarreDelta(DateTimeOffset Debut, DateTimeOffset Fin, double Delta, double NiveauAtteint);
readonly record struct PointReduit(double X, double Min, double Max);
const int Binning.SeuilReduction = 4000;  bool Binning.DoitReduire(int nbPoints);   // n > 4000
IReadOnlyList<BarreDelta>  Binning.DeltasParHeure(IReadOnlyList<DeltaConsommation> deltas, IReadOnlyList<ReleveJournal> serie, Func<ReleveJournal, double?> niveau, Plage plage);
IReadOnlyList<PointReduit> Binning.ReductionMinMax(IReadOnlyList<(double X, double Y)> points, int colonnes);

// Tuiles 5 h et réticule
sealed record Tuile5h(DateTimeOffset Debut, DateTimeOffset Fin, DateTimeOffset ResetsAt, double UMax, bool Epuisee, bool ResetDansPlage);
IReadOnlyList<Tuile5h> Tuiles5h.Depuis(IReadOnlyList<ReleveJournal> serie, Plage plage);
ReleveJournal? Reticule.PlusProche(IReadOnlyList<ReleveJournal> serie, DateTimeOffset t);
```

Mode d'emploi par piste :

- **NIVEAU (escalier hebdo, rampe)** : `Segments(a.Serie, a.Trous, r => r.U7, plage)` → `ParBandes(segs, nbBandes)` → **un `StreamGeometry` gelé par bande** : pour chaque `SegmentPalier`, `MoveTo(X(T0), Y(U, 1, h))`, `LineTo(X(T1), Y(U))`, et un raccord vertical vers le palier suivant SEULEMENT si `segs[i].T1 == segs[i+1].T0` (les paliers de longueur nulle au bord d'un trou n'ont pas de raccord : c'est là que la ligne s'interrompt). Pinceau = `ChronosTheme.ArcColor(bande.Niveau)` du thème actif, épaisseur `HistoEpaisseurEscalier` (2,2). La bande `Niveau == 1.0` se dessine en `HistoGris` (épuisée). Le % 5 h en dents de scie (A et B) = `Segments(…, r => r.U5, …)` sans bandes, `TickReset` 1 px ; tirets de reset = `X(reset.Instant)` pour `a.Resets5h` dans la plage.
- **Semaine précédente (fantôme)** : mêmes `Segments` sur `analyseS1`, mais avec `X(t, analyseS1.Plage, largeur)` — fraction de SA propre plage (Pitfall 10 : S-1 peut faire 168 h quand S en fait 169) ; gris pointillé, jamais la rampe.
- **Trous** : `RectanglesTrous(a.Trous, plage, now)` → `DrawRectangle(Line à 35 %, Pen pointillé gris ou ambre selon rect.Trou.Cause, Rect(X(Debut), 0, Largeur(Debut, Fin), h))` + libellé `CauseTrouTexte.Libelle(rect.Trou.Cause)`.
- **Sauts** : `BlocsSauts(a.Sauts, plage)` → `DrawRectangle(HistoGris, Rect(X(Debut), Y(Haut), Largeur(Debut, Fin), Y(Bas) − Y(Haut)))` + « +N % pendant l'absence (répartition inconnue) » avec `N = (Haut − Bas) × 100` pour la fenêtre du saut. Un saut sans `Delta` n'apparaît pas dans la liste : la piste n'a pas à le filtrer.
- **RYTHME** : `DeltasParHeure(a.Deltas5h, a.Serie, r => r.U5, plage)` → une barre par `BarreDelta` : `Rect(X(Debut), Y(Delta, MaxRythme, h), Largeur(Debut, Fin), h − Y(…))`, couleur `ArcColor(NiveauAtteint)`. Échelle « 0 – 25 % » fixe.
- **TOKENS** : `MaxArrondi(max des barres)` pour l'axe (« 0 – 1,2 M » via `TokenFormatter`), puis `Y(n, maxArrondi, h)` — les tokens sont des `long`, l'axe est le leur, jamais celui des %.
- **FENÊTRES 5 H (style C)** : `Tuiles5h.Depuis(a.Serie, plage)` → `Rect(X(Debut), Y(UMax, 1, h), Largeur(Debut, Fin), h − Y(UMax))`, `HistoGris` si `Epuisee` sinon `ArcColor(UMax)` ; tiret de reset à `X(ResetsAt)` si `ResetDansPlage`.
- **Réduction min/max** : une semaine à la cadence de la sonde fait 2 016 points — SOUS le seuil : non réduite. La piste ne réduit que si `DoitReduire(points.Count)` (semaines multi-sources, cadence plus fine) : `ReductionMinMax(points triés par X, (int)largeur)` → un trait vertical `Min → Max` par `PointReduit.X`.
- **Réticule / infobulle** : `Instant(xSouris, plage, largeur)` → `PlusProche(a.Serie, instant)` → texte depuis `TextesHistorique` (34-03) avec l'heure DU RELEVÉ, jamais celle du pointeur.
- **Jour (34-07)** : mêmes appels avec `plage = BornesPlage.Jour(…)` (25 cases le 25/10) ; gris « épuisée » quand `Statut5 == Rejete` ou `U5 ≥ 1` (même règle que `Tuile5h.Epuisee`).

## Files Created/Modified

- `src/Chronos/Rendering/Historique/EchelleTemps.cs` — Fraction / X / Largeur / Instant (46 l.)
- `src/Chronos/Rendering/Historique/EchelleValeur.cs` — Y / MaxRythme / MaxArrondi (58 l.)
- `src/Chronos/Rendering/Historique/Escalier.cs` — 4 records + Segments / ParBandes / RectanglesTrous / BlocsSauts (121 l.)
- `src/Chronos/Rendering/Historique/Binning.cs` — 2 records + SeuilReduction / DoitReduire / DeltasParHeure / ReductionMinMax (113 l.)
- `src/Chronos/Rendering/Historique/Tuiles5h.cs` — Tuile5h + Depuis (62 l.)
- `src/Chronos/Rendering/Historique/Reticule.cs` — PlusProche (42 l.)
- `tests/Chronos.Tests/EchellesHistoriqueTests.cs` (7), `ReticuleTests.cs` (3), `EscalierTests.cs` (7), `BinningTests.cs` (5), `Tuiles5hTests.cs` (4)

## Decisions Made

D-34-08 à D-34-12 telles qu'écrites dans le plan, plus deux précisions prises à l'exécution :

- **La longueur d'un palier suit la série, pas le sélecteur** (test `Un_releve_sans_valeur_pour_le_selecteur_ne_fait_pas_de_palier`) : un relevé dont `U7` manque ne fait pas de palier hebdo mais borne le précédent à son `T` — sinon un palier « sauterait » un relevé existant et prétendrait à une durée qu'aucun relevé n'atteste.
- **Un relevé à `T ≥ Plage.Fin` n'a pas de palier** (la série d'analyse est déjà filtrée `[Debut, Fin[` ; le cas n'arrive que sur une série construite à la main) : sans cela, un palier `(T0 > Fin, T1 = Fin)` aurait une largeur négative.
- **`Tuiles5h`** filtre `R5`/`U5` null AVANT de regrouper (un relevé sans `U5` au milieu d'un groupe n'en casse pas la continuité) ; un `resets_at` antérieur au relevé (serveur incohérent) donne une largeur nulle, jamais négative.
- **`ReductionMinMax`** : tous les X égaux → une seule colonne ; colonnes vides omises (les `X` de sortie restent strictement croissants).

## Deviations from Plan

None — plan exécuté tel qu'écrit. Une adaptation d'outillage seulement : les filtres et les mutations ont tourné dans l'instantané `snap-34-02` (prévu par les règles d'exécution parallèle) parce que le projet de tests partagé ne compilait pas pendant les RED des voisins ; la suite complète a été rejouée sur l'arbre réel dès qu'il compilait.

## Issues Encountered

- **RED voisins sur l'arbre partagé** : `DivergencesTests.cs` (34-03) à 13:07Z, puis `TextesHistoriqueTests.cs` (34-03) à 13:17Z ont cassé la compilation de `Chronos.Tests` — attendu (Pitfall 13). Réponse : instantané `git archive 47904ff` → `snap-34-02`, synchronisation de mes 11 fichiers par copie, mutations là-bas (révocation par re-copie depuis l'arbre réel, sha256 comparés), suite complète 1 441 × 2 dans l'instantané, 1 462 sur l'arbre réel à 13:15Z ; l'exécution de confirmation sur l'arbre réel est notée ci-dessous.
- **Format de sortie des tests** : les échecs sortent en `[FAIL]` / `Échoué!` (xUnit fr-FR), pas `Échec` — le helper `run.sh` a été recalibré avant de rejouer e2 et r1 (e1 avait déjà été constatée en sortie brute).

## Confirmation finale sur l'arbre réel

| Essai | Heure (UTC) | Résultat |
|-------|-------------|----------|
| 1 | 13:15Z | **1 462 / 1 462 verts**, 0 warning (34-01 et 34-03 avaient fusionné leurs GREEN) |
| 2 | 13:17Z | compilation cassée par le RED voisin `TextesHistoriqueTests.cs` (34-03, `TextesHistorique` absent) |
| 3 | 13:18Z | idem |
| 4 | 13:20Z | compilation cassée par le RED voisin `ScenariosHistoriqueTests.cs` (34-03, namespace `ViewModels.Historique` absent) |
| 5 | 13:22Z | idem |

Le second passage vert consécutif sur l'arbre réel n'a pas pu être obtenu pendant la fenêtre de ce plan : le voisin 34-03
enchaîne ses cycles RED → GREEN sur le projet de tests partagé (la règle prévoit trois essais ; cinq ont été faits). La
preuve de non-régression de CE plan est double : arbre réel 1 462 verts (essai 1, avec les 26 tests du plan) et instantané
`snap-34-02` (base `47904ff` + les 11 fichiers du plan, aucun fichier voisin) **1 441 / 1 441 deux fois de suite**. Aucun des
échecs ci-dessus n'implique un fichier de ce plan (`git diff --stat 47904ff HEAD -- src/Chronos/Rendering/Historique tests/…`
= 11 fichiers, 1 067 insertions, tous les miens). L'orchestrateur rejouera la suite complète une fois 34-03 fusionné.

## Self-Check: PASSED

- 12 fichiers vérifiés présents (6 sources, 5 tests, ce SUMMARY).
- 6 commits vérifiés dans `git log` : `e4b591a`, `c081c27`, `a042993`, `a8797be`, `65e74ce`, `dd1cac8`.
- `ROADMAP.md` / `STATE.md` / `REQUIREMENTS.md` : non modifiés par ce plan.

## Known Stubs

Aucun — classes pures complètes, aucune valeur factice, aucun `TODO`.

## Next Phase Readiness

- 34-04 peut écrire les pistes `OnRender` en n'appelant que ces six classes ; les signatures ci-dessus sont figées.
- HIS-02 reste ouvert jusqu'à 34-06 (vue Semaine assemblée) ; ce plan n'a pas exécuté `requirements mark-complete`.
- Aucun `TimeZoneInfo` dans `Rendering/Historique` : la grille des jours (minuits LOCAUX) vient de `GraduationsCalendrier` (34-03), pas d'ici.
