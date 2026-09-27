---
phase: 34-fen-tre-historique-semaine-et-jour
plan: 03
subsystem: historique-viewmodel
tags: [historique, viewmodel, facade, textes, scenarios, divergence, settings, HIS-08, HIS-01, HIS-06, HIS-07]
requires:
  - 32-06 (LecteurJournal, AnalyseReleves, BornesPlage, records LectureJournal/AnalyseJournal)
  - 33-04 (LecteurAgregats, RenduLocalTokens, CouvertureTokens, BarreHeure/ColonneQuartDHeure)
  - 32-05 / 33-03 (IEtatJournal, IEtatReconstruction, JournalReleves.SeuilMuet, CadenceNominale)
provides:
  - ChronosSettings.HistoriqueStyleSemaine + HistoriqueX/Y/Width/Height (settings.json, texte, legacy sans migration)
  - Models/Historique/DonneesHistorique.cs (VueHistorique, Divergence, GraduationLibellee, TypeAnnotation, AnnotationHistorique, DonneesSemaine, DonneesJour)
  - Services/Historique : ISourceHistorique + InstantsHistorique, SourceHistoriqueDisque, Divergences (SeuilDelta), GraduationsCalendrier, IReglagesHistorique (SurDisque / Memoire)
  - Text/TextesHistorique.cs (producteur unique des mots, fr-FR explicite)
  - ViewModels/Historique : HistoriqueViewModel, ScenariosHistorique, SourceHistoriqueDemonstration
  - Fakes : FakeSourceHistorique, FakeUiDispatcherDiffere
affects:
  - 34-04 (SurcoucheReticule : Fuseau, Infobulle), 34-05 (HistoriqueWindow, DI, galerie --historique), 34-06 / 34-07 (vues Semaine / Jour bindées sur le VM), 34-08 (tests d'honnêteté sur ScenariosHistorique)
tech-stack:
  added: []
  patterns:
    - façade de lecture neutre appelée hors UI (Task.Run) et appliquée par IUiDispatcher.Post numérotée (la lecture la plus récente gagne)
    - coalescence d'un événement de thread de fond par Interlocked.Exchange + un seul Post en vol
    - tick 60 s qui ne relit que si un magasin a bougé (DerniereEcriture / DerniereReconstructionTerminee capturés à la demande de lecture)
    - scénario de démonstration déterministe en mémoire partagé par la galerie et les tests
key-files:
  created:
    - src/Chronos/Models/Historique/DonneesHistorique.cs
    - src/Chronos/Services/Historique/ISourceHistorique.cs
    - src/Chronos/Services/Historique/SourceHistoriqueDisque.cs
    - src/Chronos/Services/Historique/Divergences.cs
    - src/Chronos/Services/Historique/GraduationsCalendrier.cs
    - src/Chronos/Services/Historique/ReglagesHistorique.cs
    - src/Chronos/Text/TextesHistorique.cs
    - src/Chronos/ViewModels/Historique/HistoriqueViewModel.cs
    - src/Chronos/ViewModels/Historique/ScenariosHistorique.cs
    - src/Chronos/ViewModels/Historique/SourceHistoriqueDemonstration.cs
    - tests/Chronos.Tests/Fakes/FakeSourceHistorique.cs
    - tests/Chronos.Tests/Fakes/FakeUiDispatcherDiffere.cs
    - tests/Chronos.Tests/DivergencesTests.cs
    - tests/Chronos.Tests/GraduationsCalendrierTests.cs
    - tests/Chronos.Tests/SourceHistoriqueDisqueTests.cs
    - tests/Chronos.Tests/TextesHistoriqueTests.cs
    - tests/Chronos.Tests/ScenariosHistoriqueTests.cs
    - tests/Chronos.Tests/HistoriqueViewModelTests.cs
  modified:
    - src/Chronos/Services/ChronosSettings.cs
    - tests/Chronos.Tests/SettingsServiceTests.cs (+2)
    - tests/Chronos.Tests/ServicesLayerPurityTests.cs (+1)
decisions:
  - D-34-05 un seul HistoriqueViewModel (un marshaling, une persistance, un abonnement à Changement)
  - D-34-06 la piste Tokens montre le compteur Out ; échelle « 0 – 1,2 M (sortie) »
  - D-34-07 la divergence est une analyse de Services/Historique (Divergences.Detecter, SeuilDelta = 0,01 nommé, couverture prouvée, fusion des heures adjacentes)
  - D-34-13 repère hebdo = R7 du dernier relevé des 7 derniers jours, repli WeeklyAnchor ; navigation ‹ › par la borne courante
  - D-34-14 le libellé permanent des tokens porte le grain (« par heure, … » / « par quart d'heure, … »)
  - D-34-15 IReglagesHistorique (Lire / Modifier = Save(mutation(Load()))) ; ReglagesHistoriqueMemoire pour la galerie et les tests
  - D-34-16 scénario de référence : now = jeu. 24 sept. 2026 17:12 Paris ; plateau épuisé mer. 20:00 → jeu. 00:00 ; grille 5 h ancrée sur le début de semaine
  - D-34-17 (nouveau) une plage est analysée à min(now, Plage.Fin) — InstantsHistorique.InstantDAnalyse — sinon toute plage révolue finit par un faux trou ouvert
metrics:
  duration: 34 min (2026-09-27T13:04:02Z → 2026-09-27T13:38:03Z)
  completed: 2026-09-27
  tasks: 3
  files: 21
  tests_added: 52 (15 + 20 + 17)
  suite: 1499 / 1499 verts, deux exécutions consécutives, 0 warning
requirements-completed: [HIS-08]
---

# Phase 34 Plan 03: Façade, textes, scénarios et ViewModel de la fenêtre Historique — Summary

**Un `HistoriqueViewModel` unique qui lit hors UI par une façade neutre (`ISourceHistorique`), applique la lecture la plus récente par `Post` numéroté, ne relit au tick de 60 s que si un magasin a bougé, coalesce les rafales de la reconstruction en un seul franchissement de thread, et ne prononce que les mots de `TextesHistorique` (fr-FR explicite) — le tout prouvé sur la semaine de référence des maquettes fabriquée en mémoire par `ScenariosHistorique`.**

SHA d'entrée : `47904ff` (1415 verts). HEAD de sortie de ce plan : voir « Commits ».

## Écarts d'API par rapport au bloc `<interfaces>` du plan (à lire en premier par 34-04 … 34-08)

1. **`InstantsHistorique.InstantDAnalyse(now, plage)`** (nouveau, dans `ISourceHistorique.cs`) : les deux façades analysent une plage à `min(now, plage.Fin)`. Sans cela, `AnalyseReleves.Analyser` ajoute un trou OUVERT « cause inconnue » après le dernier relevé de TOUTE plage révolue (semaine précédente, jour d'hier). Correction Rule 1 côté façade ; `AnalyseReleves` n'est pas modifié.
2. **Saut 5 h du trou « jeton invalide » = `null`** (au moins un reset), pas un Δ : la grille 5 h prescrite par le plan (ancrée sur sam. 19 sept. 00:00, pas de 5 h) passe par **jeu. 15:00 local (13:00Z)**, à l'intérieur de l'absence 14:00 → 16:00. Le plan disait « aucun reset entre 14 h et 16 h » : c'est arithmétiquement faux avec la grille qu'il prescrit lui-même ; les bornes du mercredi (14:00 / 19:00 / 00:00, D-34-16) sont conservées. Le saut hebdo de ce trou vaut bien 0.
3. **Couverture du scénario** : `GarantirPasse(now, now)` comme le plan le demande → les barres **à venir** (après 17:12 jeudi) sont `TranscriptsAbsents`, pas `Couverte` ; c'est exactement ce que la vraie reconstruction produit. Le test grave : toutes les barres passées `Couverte`, toutes les futures `TranscriptsAbsents`, aucune `HorsCouverture`. **À noter pour 34-06** : la piste ne doit pas hachurer le futur comme « transcripts absents » — le futur est simplement vide.
4. **`HistoriqueViewModel.AnnotationsDivergences`** (ajouté) : une annotation `TypeAnnotation.Divergence` par divergence (`Texte = ""`, le pied de page explique) — pratique pour la vue, en plus de `DonneesSemaine.Divergences`.
5. **`TextesHistorique`** : constantes supplémentaires `SemaineDeForfait` (« Semaine de forfait »), `HistoriqueIndisponible` (« historique indisponible »), `Tiret` (« — ») ; `Infobulle` sans valeur 5 h rend la ligne « 5 h : — » seule (règle tranchée dans le plan).
6. **`ReglagesHistoriqueMemoire(ChronosSettings initial)`** : constructeur supplémentaire (état de départ pour la galerie et les tests).
7. **`ISourceHistorique.RepereHebdo`** lit `[now − 7 j, now]` borne incluse (`now.AddTicks(1)` passé au lecteur, dont la borne haute est exclue).

## Surface publique réelle de `HistoriqueViewModel` (contrat des vues 34-05 / 34-06 / 34-07)

```csharp
namespace Chronos.ViewModels.Historique;
public sealed partial class HistoriqueViewModel : ObservableObject
{
    public HistoriqueViewModel(ISourceHistorique source, IUiDispatcher ui, IClock clock, TimeZoneInfo tz, IReglagesHistorique reglages,
                               IEtatJournal? journal = null, IEtatReconstruction? reconstruction = null);

    // Vue / période
    public VueHistorique VueActive { get; set; }            // défaut Semaine
    public bool IsVueJour { get; }  public bool IsVueSemaine { get; }
    public Plage? PlageCourante { get; }                    // null avant Ouvrir()
    public string LibellePeriode { get; }                   // « Semaine de forfait · sam. 19 sept. 00:00 → sam. 26 sept. 00:00 » / « Jour · jeudi 24 sept. 2026 · semaine de forfait du 19 sept. »
    public bool EstAuPresent { get; }
    public string TexteRetourPresent { get; }               // « Cette semaine » / « Aujourd'hui »
    public event EventHandler? FermetureDemandee;
    public IRelayCommand<VueHistorique> ChoisirVueCommand;  // QuatreSemaines → no-op (phase 35)
    public IRelayCommand PrecedentCommand, SuivantCommand /* CanExecute = !EstAuPresent */, RetourPresentCommand, FermerCommand;

    // Style (persisté par IReglagesHistorique)
    public HistoriqueStyleSemaine Style { get; }  public bool IsStylePistes, IsStyleSimplifie, IsStyleTuiles;
    public IRelayCommand<HistoriqueStyleSemaine> ChoisirStyleCommand;

    // Données (références immuables : changent SEULEMENT sur lecture appliquée)
    public DonneesSemaine? DonneesSemaine { get; }  public DonneesJour? DonneesJour { get; }

    // Dérivés (recalculés dans Appliquer*, jamais au tick)
    public IReadOnlyList<GraduationLibellee> LibellesJours { get; }     // Semaine : 7 minuits locaux « sam. 19 » … « ven. 25 »
    public IReadOnlyList<GraduationLibellee> LibellesHeures { get; }    // Jour : 8 heures locales « 0 h » … « 21 h »
    public IReadOnlyList<AnnotationHistorique> AnnotationsTrous { get; }        // Texte = cause, Cause renseignée, Fin = LueA si ouvert
    public IReadOnlyList<AnnotationHistorique> AnnotationsSauts { get; }        // Semaine : SevenDay seulement ; Jour : FiveHour seulement
    public IReadOnlyList<AnnotationHistorique> AnnotationsResets { get; }       // Jour : « reset 5 h HH:MM », Instant ∈ Plage, Fin null
    public IReadOnlyList<AnnotationHistorique> AnnotationsEpuisee { get; }      // Jour : plateaux U5 ≥ 1 ou Rejete, Fin = dernier + cadence bornée
    public IReadOnlyList<AnnotationHistorique> AnnotationsDivergences { get; }  // Semaine : cadres « consommé ailleurs », Texte ""
    public AnnotationHistorique? AnnotationJournalOuvert { get; }              // non null si JournalOuvertLe ∈ Plage
    public bool AfficherPiedDivergence { get; }  public string TextePiedDivergence { get; }
    public string LibellePermanentTokens { get; }   // « par heure, … » / « par quart d'heure, … »
    public string LegendeTokens { get; }            // « ▮ principal ▮ sous-agents »
    public string LegendeModeles { get; }           // Jour : « opus · sonnet · haiku · sous-agents inclus » ; Semaine : ""
    public string EchelleTokens { get; }            // « 0 – 1,2 M (sortie) » — dérivée de PlafondTokens
    public long PlafondTokens { get; }              // plafond « joli » du max des Out empilés (barres ou colonnes) : UNE source pour l'échelle et la DP Plafond
    public string EchelleRythme { get; }  public string PiedDePage { get; }

    // Fraîcheur (au tick)
    public string TexteFraicheur { get; }  public bool AlerteJournal { get; }  public string TexteAlerteJournal { get; }
    public DateTimeOffset Maintenant { get; }  public bool AfficherMaintenant { get; }   // IsVueJour && EstAuPresent

    // Bandeau F2
    public bool AfficherBandeauF2 { get; }  public string TexteBandeauF2 { get; }  public string SousTexteBandeauF2 { get; }  public double FractionBandeauF2 { get; }

    // Thème, fuseau
    public ChronosTheme Theme { get; }   // ThemeCatalog.ByKey(reglages.Lire().ThemeKey), mémorisé au ctor
    public TimeZoneInfo Fuseau { get; }

    // Cycle de vie
    public void Ouvrir();               // repère hebdo (synchrone), plage au présent, 1re lecture hors UI, bandeau
    public void DemarrerHorloge();      // DispatcherTimer 60 s → Tick ; JAMAIS dans le ctor ; idempotent
    public void ArreterHorloge();       // stop + désabonnement de Changement
    internal void Tick(DateTimeOffset now);
    internal Task AttendreLecture();    // WhenAll des lectures en vol (tests)

    // Géométrie de fenêtre
    public (double? X, double? Y, double? Largeur, double? Hauteur) GeometriePersistee();
    public void EnregistrerGeometrie(double x, double y, double largeur, double hauteur);
}
```

Règles de comportement prouvées : `Ouvrir` → 1 lecture ; 60 ticks → 0 lecture, même référence de `DonneesSemaine`, 0 notification sur `DonneesSemaine` / `DonneesJour` / `LibellesJours` / `AnnotationsTrous` ; une nouvelle `DerniereEcriture` ou `DerniereReconstructionTerminee` → exactement 1 lecture au tick suivant ; 50 `Changement` d'un thread de fond → `PostCount + 1` ; une lecture périmée n'écrase jamais la plus récente (numéro de requête) ; alerte « journal muet » depuis `max(démarrage, dernière écriture)` > `JournalReleves.SeuilMuet`.

## `TextesHistorique` (namespace `Chronos.Text`, fr-FR explicite)

Constantes : `Titre` « Historique » · `SegmentJour` « Jour » · `SegmentSemaine` « Semaine » · `SegmentQuatreSemaines` « 4 semaines » · `InfobulleBientot` « bientôt (phase 35) » · `CetteSemaine` · `Aujourdhui` · `SemaineDeForfait` · `PisteNiveau` « NIVEAU » · `PisteRythme` « RYTHME » · `PisteTokens` « TOKENS CLAUDE CODE » · `PisteCouverture` « COUVERTURE » · `PisteFenetres5h` « FENÊTRES 5 H » · `StylePrefixe` « Style : » · `StylePistes` / `StyleSimplifie` « Simplifié » / `StyleTuiles` · `ResetHebdo` « reset hebdo → » · `EchelleRythme` « 0 – 25 % » · `LegendeTokens` « ▮ principal ▮ sous-agents » · `Epuisee` « épuisée à 100 % — le serveur refuse (statut rejected) » · `PiedDePage` · `PiedDivergence` « cadre violet : marches de % sans tokens Code = consommé ailleurs (Cowork, claude.ai) » · `AucunReleveSemaine` · `AucunReleveJour` · `GrainHeure` / `GrainQuartDHeure` · `HistoriqueIndisponible` · `Tiret`.

Méthodes : `LibellePermanentTokens(grain)`, `LibelleStyle(style)`, `LibellePeriodeSemaine(plage, tz)`, `LibellePeriodeJour(jour, semaine, tz)`, `LibelleJour(minuit, tz)` « sam. 19 », `LibelleHeure(t, tz)` « 3 h », `HeureMinute(t, tz)` « 14:00 », `DateLongue(t, tz)` « 14 sept. 2026 », `Pourcent(double?)` « 43 % » / « — », `LigneFraicheurSemaine(a, now, tz)`, `LigneFraicheurJour(jour, a, cadence, tz)` (attendus = `Duree / cadence` : 288 / 300 / 276), `AlerteJournalMuet(age)`, `Saut(s)` (« +2 % … », « −1 % … » U+2212, « au moins un reset … »), `Trou(t)` (= `CauseTrouTexte.Libelle`), `JournalOuvertLe(t, tz)`, `Reset5h(instant, tz)`, `EchelleTokens(max)` (« 0 – 1,2 M (sortie) »), `LegendeModeles(modeles)`, `Infobulle(r, now, tz)` (4 lignes `\n`), `BandeauF2(traites, total, dispo)`, `SousTexteF2(journalOuvertLe, tz)`.

Garde interne : aucune constante ni sortie ne contient `épuisé vers | à ce rythme | projection | prévision | estim | tendance | dans N h` (test par réflexion) ; les libellés sont identiques sous `CultureInfo.InvariantCulture`.

## `ScenariosHistorique` / `SourceHistoriqueDemonstration` — mode d'emploi (galerie 34-05, tests 34-08)

```csharp
var tz = TimeZoneInfo.Local;                                   // galerie ; tests : BornesPlage.FuseauParisPourTests()
var vm = new HistoriqueViewModel(
    new SourceHistoriqueDemonstration(tz),                     // sert la semaine de référence pour TOUTE plage
    ui, ScenariosHistorique.HorlogeFigee(tz), tz,              // now = jeu. 24 sept. 2026 17:12 local
    new ReglagesHistoriqueMemoire(),                           // n'écrit jamais settings.json
    journal: null,                                             // ou un FakeEtatJournal
    reconstruction: ScenariosHistorique.ReconstructionEnCours()); // bandeau F2 : 886 / 1603, semaine courante complète
vm.Ouvrir();
```

Faits du scénario (heure de Paris) : semaine sam. 19 sept. 00:00 → sam. 26 sept. 00:00 (`ScenariosHistorique.Semaine(tz)`) ; journal ouvert lun. 14 sept. 12:00 ; relevés toutes les 5 min, dernier à 17:10 ; trou « Chronos arrêté » mar. 22 23:00 → mer. 23 07:00 (saut hebdo +2 %, 5 h « au moins un reset ») ; trou « jeton invalide » jeu. 24 14:00 → 16:00 (hebdo 0, 5 h « au moins un reset » : la grille passe par 15:00) ; grille 5 h ancrée sur sam. 19 00:00 (mercredi : resets 04:00 / 09:00 / 14:00 / 19:00 / 00:00) ; fenêtre épuisée mer. 20:00 → jeu. 00:00 (`u5 = 1,0`, `Rejete`) ; divergence unique mer. 21:00 → 23:00 (+0,04 sans tokens Code, 19:00Z → 21:00Z) ; tokens opus / sonnet / haiku + sous-agents opus par quart d'heure aux heures actives (nuit 00:00–06:59 vide), y compris pendant les deux trous, jamais pendant le plateau ; couverture : plus ancienne ligne le 23 juin, garantie jusqu'à `now` ; semaine précédente : `U7` final ≈ 0,55, aucun trou. Accès bruts : `Journal(plage, tz)`, `Agregats(plage, tz)`, `Couverture()`, `RepereHebdo`, `RepereHebdoPrecedent`, `JournalOuvertLe`, `Maintenant(tz)`.

## Tâches, RED nommés, mutations

| Tâche | RED (commit) | GREEN (commit) | Mutations (rouge attendu → constaté) | sha256 avant = après |
|---|---|---|---|---|
| 1 — réglages, records, `Divergences`, `GraduationsCalendrier`, façade disque, `IReglagesHistorique` | 15 tests, `e4b17a7` | `af09745` | d1 (sans `Etat == Couverte`) → `Une_heure_hors_couverture…` ; d2 (`SeuilDelta = 0,05`) → `Le_seuil…`, `Une_marche_hebdo…`, `Les_deltas_negatifs…` ; g1 (`Jours` par k × 24 h) → `La_semaine_de_169_heures…` ; f1 (`CreateDirectory`) → `Un_dossier_absent…` | `e936ac51…` (d1, d2), `1ca0a580…` (g1), `f5384831…` (f1) |
| 2 — `TextesHistorique`, `ScenariosHistorique`, `SourceHistoriqueDemonstration` | 20 tests, `af48881` | `02e139f` | x1 (culture implicite) → `Les_libelles_de_periode…` ; x2 (« fichiers traités ») → `Le_bandeau_F2…` ; x3 (`288` en dur) → `La_ligne_de_fraicheur_jour…` ; sc1 (tokens pendant le plateau) → `La_divergence…`, `Les_tokens_couvrent…` ; sc2 (`arret` retiré) → **vert** : le `demarrage` dans le trou porte la cause (D-32-26) — robustesse de la cause, pas faiblesse du test | `d7d2e202…` (x1–x3), `27666856…` (sc1, sc2) |
| 3 — `HistoriqueViewModel` | 17 tests, `314f807` | `1fef30f` | v1 (`Tick` relit toujours) → `Soixante_ticks…` (+ `Une_nouvelle_ecriture…`) ; v2 (sans coalescence) → `Cinquante_changements…` ; v3 (lecture périmée appliquée) → `Une_lecture_perimee…` **rouge 5 / 5** ; v4' (sans `max(démarrage, …)`) → `L_alerte_journal_muet…` | `0a11970a…` (v1–v4') |

Toutes les mutations ont été appliquées par copie et révoquées par re-copie ; les sha256 complets ont été comparés par le harnais (égalité constatée à chaque fois).

Suite complète (`dotnet test Chronos.sln -c Debug --nologo -v q`) : **1499 / 1499** verts, deux exécutions consécutives ; `dotnet build Chronos.sln -c Debug` : **0 warning**. (1415 d'entrée + 52 de ce plan + 32 des plans voisins de la vague.)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Faux trou ouvert en fin de toute plage révolue**
- **Found during:** Task 2 (test `La_semaine_precedente_commence_a_l_ouverture_du_journal` : `Trous.Count == 0` impossible au `now` réel)
- **Issue:** `AnalyseReleves.Analyser(lecture, now, cadence)` ouvre un trou dès que `now − Serie[^1].T > 2 × cadence` : la semaine précédente et tout jour passé finissaient par une « interruption (cause inconnue, HH:MM → en cours) ».
- **Fix:** `InstantsHistorique.InstantDAnalyse(now, plage) = min(now, plage.Fin)` appliqué dans `SourceHistoriqueDisque` et `SourceHistoriqueDemonstration` ; l'analyse pure n'est pas touchée.
- **Files modified:** `ISourceHistorique.cs`, `SourceHistoriqueDisque.cs`, `SourceHistoriqueDemonstration.cs`
- **Commit:** `02e139f`

**2. [Rule 1 - Contrat incohérent] Saut 5 h du trou « jeton invalide »**
- **Found during:** Task 2 (conception du scénario)
- **Issue:** le plan demandait à la fois une grille 5 h ancrée sur sam. 19 00:00 (D-34-16, resets du mercredi à 14:00 / 19:00 / 00:00) et « aucun reset entre 14 h et 16 h » jeudi ; or la même grille passe par jeu. 15:00.
- **Fix:** grille conservée (elle est la décision écrite) ; le saut 5 h du trou B est `null` (« au moins un reset pendant l'absence ») — gravé dans `Le_saut_de_la_nuit_est_hebdo_seulement` et dans le test VM des annotations en Jour.
- **Commit:** `af48881` / `02e139f`

**3. [Rule 2 - Critères d'acceptation] Comptages littéraux**
- Les greps du plan comptent aussi les commentaires : `[Fact]` cité dans les en-têtes, `<see cref="NomLong"/>` dans le VM, préfixe F2 cité dans la doc de `BandeauF2`, `CouvertureTokens.Charger(` appelé deux fois. Reformulé / factorisé sans changement de comportement (`52ddf16`, `cb22fc5`).

### Couverture des heures à venir (à connaître, pas une déviation)
`GarantirPasse(now, now)` — fidèle à la vraie reconstruction — laisse les heures postérieures à `now` en `TranscriptsAbsents`. Les vues (34-06) ne doivent pas représenter le futur comme des transcripts manquants.

## Known Stubs

- `HistoriqueViewModel.PlafondJoli` : copie locale minimale (mantisses 1 / 1,2 / 1,5 / 2 / 2,5 / 3 / 4 / 5 / 6 / 8 / 10). **À remplacer par `EchelleValeur.MaxArrondi` (34-02) en 34-06**, pour que l'échelle textuelle et la DP `Plafond` des pistes viennent de la même règle. Intentionnel : 34-02 n'est pas une dépendance de ce plan.
- `LegendeModeles == ""` en vue Semaine : intentionnel (la piste Semaine empile principal / sous-agents, pas les modèles — D-34-06).
- `LibellesHeures` n'est calculée qu'en Jour, `LibellesJours` qu'en Semaine (l'autre garde sa dernière valeur) : les vues ne bindent que la liste de leur vue.

## Commits

- `e4b17a7` test(34-03): RED 15 — réglages, façade, divergences, graduations, pureté
- `af09745` feat(34-03): HistoriqueStyleSemaine + géométrie, records neutres, Divergences, GraduationsCalendrier, ISourceHistorique + SourceHistoriqueDisque, IReglagesHistorique (HIS-01, HIS-06)
- `af48881` test(34-03): RED 20 — textes mot pour mot, scénario de référence
- `02e139f` feat(34-03): TextesHistorique ; ScenariosHistorique + SourceHistoriqueDemonstration ; InstantDAnalyse (HIS-06, HIS-08)
- `314f807` test(34-03): RED 17 — HistoriqueViewModel
- `1fef30f` feat(34-03): HistoriqueViewModel (HIS-08, HIS-01)
- `52ddf16` refactor(34-03): couverture chargée par un seul point ; bornes ISO en commentaire
- `cb22fc5` docs(34-03): commentaires alignés sur les critères d'acceptation

## Self-Check: PASSED

19 fichiers créés vérifiés présents, 8 commits vérifiés dans l'historique (`git cat-file -e`), suite 1499 / 1499 verte deux fois, 0 warning.
