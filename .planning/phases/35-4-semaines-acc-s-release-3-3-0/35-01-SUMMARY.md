---
phase: 35-4-semaines-acc-s-release-3-3-0
plan: 01
subsystem: historique — données / VM / mots de la vue 4 semaines, veille de minuit (vue Jour), réouverture du VM singleton
tags: [HIS-05, DonneesQuatreSemaines, LireQuatreSemaines, EchelleTemps.Reporter, TextesHistorique, HistoriqueViewModel, LectureVeille, ActualiserTheme, FermetureDemandee, F2, TDD, mutations]

sha_entree_de_plan: b3b08f7
one_liner: "La vue 4 semaines existe côté données et VM : une lecture du journal pour quatre analyses (chacune à InstantDAnalyse), report par fraction sur l'axe de S (169 h absorbées), étiquettes qui séparent « avant le journal » de « pas de relevés », « épuisée jeu. 20:00 → bloquée jusqu'au reset » reportée sur S (prouvée par une fixture dérivée du scénario), quatre rangées de couverture, pied « Rien n'est inventé avant l'ouverture du journal. ». La vue Jour lit la veille de minuit : le mercredi du scénario dit « 1 interruption (Chronos arrêté, mar. 23:00 → 07:00) » sans rien dessiner ni compter avant 07:00. Le VM singleton survit aux réouvertures : F2 réabonné, thème relu, aucune fenêtre fermée retenue. Suite 1565 → 1588 dans l'instantané (+23), 1622 sur l'arbre réel avec les voisins, deux passes vertes, 0 warning."

requires:
  - phase: 34 (HIS-01…HIS-08)
    provides: "HistoriqueViewModel, ISourceHistorique (disque + démonstration), InstantsHistorique, EchelleTemps, TextesHistorique, ScenariosHistorique, HistoriqueWindow, GraduationsCalendrier"
  - phase: 32-04 / 32-06 (JRN-05)
    provides: "LecteurJournal.Lire, AnalyseReleves (pure, non modifiée), BornesPlage.QuatreSemaines / SemaineDeForfait / Jour"
provides:
  - "Models/Historique/DonneesHistorique.cs — DonneesQuatreSemaines(Semaines, JournalOuvertLe, LueA) { Courante }, EtiquetteSemaine(Rang, Texte, AvantJournal), RangeeCouverture(Rang, Libelle, Plage, Serie, Trous, InstantLecture, ZoneAvantJournal, Texte, JournalOuvert)"
  - "ISourceHistorique.LireQuatreSemaines(semaines, now) — disque (une lecture, D-35-01) et démonstration"
  - "EchelleTemps.Reporter(t, source, cible) (D-35-02)"
  - "TextesHistorique — LibelleJourCourt, LibellePeriodeQuatreSemaines, RangSemaine, EtiquetteSemaine, EpuiseeSemaine, PiedQuatreSemaines, AvantJournalAucunReleve, AucunReleve, CouvertureParSemaine (InfobulleBientot GARDÉE)"
  - "HistoriqueViewModel — IsVueQuatreSemaines, DonneesQuatreSemaines, LibellesJoursCourts, EtiquettesSemaines, AnnotationsEpuiseeSemaines, RangeesCouverture, PiedQuatreSemaines, Theme observable + ActualiserTheme(), réabonnement F2 idempotent"
  - "Services/Historique/LectureVeille.cs — Horizon (7 j), PourLeJour(large, jour, cadence), RestreindreAuJour(analyse, jour)"
  - "tests/Chronos.Tests/Fakes/FixturesQuatreSemaines.cs — SemaineUnEpuisee(d, tz), DebutEpuisee (réutilisable par 35-04)"
affects:
  - "35-04 : pose le XAML de la vue 4 semaines sur le contrat ci-dessous, active le segment, retire InfobulleBientot (constante + HistoriqueBindingTests l. 144 + TextesHistoriqueTests)"
  - "35-03 (diagnostic) : LireJour appelé par le diagnostic lit désormais la veille — la journée du rapport nomme un trou qui chevauche minuit et « présents » ne compte que le jour ; signature inchangée"
  - "35-02 (ouvreur) : la fenêtre recréée sur le VM singleton relit le thème et ne laisse aucun abonnement derrière elle"

tech-stack:
  added: []
  patterns:
    - "Une lecture, N analyses : lire l'union des plages une fois et partitionner en mémoire (LecteurJournal relit le plus ancien fichier à chaque appel)"
    - "Report par fraction (cible.Debut + Fraction(t, source) × cible.Duree) pour superposer des semaines de durées différentes"
    - "Veille de minuit : augmenter la lecture d'UN relevé antérieur pour que l'analyse pure voie le trou, puis restreindre la série rendue — la vue ne dessine rien d'inventé"
    - "Singleton ré-ouvrable : abonnements rétablis au démarrage de l'horloge (drapeau idempotent), thème observable relu par la fenêtre, handlers nommés retirés au Closed"

key-files:
  created:
    - src/Chronos/Services/Historique/LectureVeille.cs
    - tests/Chronos.Tests/Fakes/FixturesQuatreSemaines.cs
    - tests/Chronos.Tests/ReouvertureHistoriqueTests.cs
    - tests/Chronos.Tests/LectureVeilleTests.cs
  modified:
    - src/Chronos/Models/Historique/DonneesHistorique.cs
    - src/Chronos/Services/Historique/ISourceHistorique.cs
    - src/Chronos/Services/Historique/SourceHistoriqueDisque.cs
    - src/Chronos/Rendering/Historique/EchelleTemps.cs
    - src/Chronos/Text/TextesHistorique.cs
    - src/Chronos/ViewModels/Historique/HistoriqueViewModel.cs
    - src/Chronos/ViewModels/Historique/SourceHistoriqueDemonstration.cs
    - src/Chronos/Views/Historique/HistoriqueWindow.xaml.cs
    - tests/Chronos.Tests/Fakes/FakeSourceHistorique.cs
    - tests/Chronos.Tests/SourceHistoriqueDisqueTests.cs
    - tests/Chronos.Tests/EchellesHistoriqueTests.cs
    - tests/Chronos.Tests/TextesHistoriqueTests.cs
    - tests/Chronos.Tests/HistoriqueViewModelTests.cs
    - tests/Chronos.Tests/VueJourBindingTests.cs
    - tests/Chronos.Tests/HonneteteHistoriqueTests.cs

key-decisions:
  - "D-35-01 : la façade 4 semaines lit le journal UNE fois sur [S-3.Debut, S.Fin[ et rend quatre analyses, chacune à InstantDAnalyse(now, sa plage) ; aucun agrégat de tokens lu"
  - "D-35-02 : EchelleTemps.Reporter = cible.Debut + Fraction(t, source) × cible.Duree ; les annotations « épuisée » de S-k sont reportées sur l'axe de S"
  - "D-35-03 : « avant le journal » (Plage.Fin <= JournalOuvertLe, ou journal absent) ≠ « pas de relevés » (semaine postérieure à l'ouverture sans relevé)"
  - "D-35-04 : veille de minuit en vue Jour seulement ; LectureVeille.PourLeJour adjoint le dernier relevé de la source dominante dans [Debut − 7 j, Debut[ et les événements de ]Tveille − cadence, Debut[ ; AnalyseReleves inchangée ; LigneFraicheurJour garde sa signature et compte les seuls relevés du jour"
  - "D-35-05 : réabonnement F2 idempotent dans DemarrerHorloge (drapeau _abonneReconstruction) ; Theme observable + ActualiserTheme() appelé par la fenêtre avant les pinceaux ; FermetureDemandee par handler nommé retiré au Closed"
  - "D-35-06 (nouvelle, écart au plan) : la série RENDUE en vue Jour est restreinte au jour (LectureVeille.RestreindreAuJour) — le relevé de la veille ferme le trou puis disparaît de la série, pour que la couverture ne peigne aucun « présent » avant 07:00 et que le réticule ne s'accroche pas à un relevé de la veille"

requirements-completed: []
requirements-advanced: [HIS-05]

metrics:
  duration: 22min
  completed: 2026-09-27
  tasks: 3
  files: 19
  tests_added: 23
---

# Phase 35 Plan 01 : données, VM et mots de la vue 4 semaines ; veille de minuit ; réouverture du singleton — Summary

**La vue 4 semaines est prête côté données et VM (une lecture, quatre analyses, report par fraction, étiquettes, épuisée, couverture par
semaine, pied). La vue Jour nomme le trou qui chevauche minuit. Le VM singleton survit aux réouvertures.**

HIS-05 : contribution seulement, la clôture revient à 35-04. `requirements mark-complete` n'a pas été lancé.

## Performance

- **Durée :** ~22 min (16:44Z → 17:06Z)
- **Tâches :** 3 (TDD, 6 commits)
- **Fichiers :** 4 créés, 15 modifiés
- **Tests :** +23 (1565 → 1588 dans l'instantané `snap-35-01` ; **1622** sur l'arbre réel avec 35-02 et 35-03, deux passes vertes consécutives) ; `dotnet build -c Release` : 0 avertissement

## Commits (SHA d'entrée `b3b08f7`)

| Tâche | RED | GREEN |
|------|-----|-------|
| 1. Données 4 semaines, report, mots | `0b08e05` : RED 5 (CS0246 `DonneesQuatreSemaines`) | `46f6055` |
| 2. VM 4 semaines + réouverture | `f684026` : RED 21 (CS1061 `IsVueQuatreSemaines`, `DonneesQuatreSemaines`, `ActualiserTheme`…) | `e2a9169` |
| 3. Veille de minuit (vue Jour) | `303c2c3` : RED 11 (CS0103 `LectureVeille`) | `760d449` |

Tous en `--no-verify`, fichiers indexés un par un, aucun `--amend`. Le RED de 35-02 cassait la compilation de l'arbre partagé : boucle TDD
et mutations jouées dans l'instantané `snap-35-01` (`git archive b3b08f7` + les fichiers de ce plan), puis suite complète deux fois sur
l'arbre réel une fois les voisins au vert.

## Contrat livré à 35-04 (noms exacts)

```csharp
// Models/Historique/DonneesHistorique.cs
public sealed record DonneesQuatreSemaines(IReadOnlyList<AnalyseJournal> Semaines, DateTimeOffset? JournalOuvertLe, DateTimeOffset LueA)
{ public AnalyseJournal Courante => Semaines[^1]; }                       // ordre CHRONOLOGIQUE : [0] = S-3 … [3] = S
public sealed record EtiquetteSemaine(int Rang, string Texte, bool AvantJournal); // Rang 0 = S
public sealed record RangeeCouverture(int Rang, string Libelle, Plage Plage, IReadOnlyList<ReleveJournal> Serie, IReadOnlyList<Trou> Trous,
    DateTimeOffset InstantLecture, Plage? ZoneAvantJournal, string Texte, AnnotationHistorique? JournalOuvert);
// ISourceHistorique
DonneesQuatreSemaines LireQuatreSemaines(IReadOnlyList<Plage> semaines, DateTimeOffset now);   // ArgumentException si Count != 4
// Rendering/Historique/EchelleTemps.cs
public static DateTimeOffset Reporter(DateTimeOffset t, Plage source, Plage cible);
```

**HistoriqueViewModel** (tout est recalculé dans `AppliquerQuatreSemaines`, jamais au tick) :

| Membre | Type | Contenu |
|--------|------|---------|
| `IsVueQuatreSemaines` | `bool` | notifié par `OnVueActiveChanged` |
| `DonneesQuatreSemaines` | `DonneesQuatreSemaines?` | référence immuable, nouvelle à chaque lecture appliquée |
| `LibellesJoursCourts` | `IReadOnlyList<GraduationLibellee>` | 7 minuits locaux de S → « sam. », « dim. », … « ven. » |
| `EtiquettesSemaines` | `IReadOnlyList<EtiquetteSemaine>` | 4 entrées, **rang 0 (S) d'abord** : « S · 19 sept. · 43 % », « S-2 · 5 sept. · pas de relevés (avant le journal) », « S-1 · 12 sept. · pas de relevés » |
| `AnnotationsEpuiseeSemaines` | `IReadOnlyList<AnnotationHistorique>` | `Type = Epuisee`, `Debut` = premier relevé `Statut7 == Rejete \|\| U7 >= 1` REPORTÉ sur l'axe de S, `Fin = S.Fin`, texte « épuisée jeu. 20:00 → bloquée jusqu'au reset » |
| `RangeesCouverture` | `IReadOnlyList<RangeeCouverture>` | 4 entrées, **S d'abord**, chacune sur SA plage ; `ZoneAvantJournal` = toute la plage (avant le journal) ou `[Debut, JournalOuvertLe[` (semaine d'ouverture) ou `null` ; `Texte` = « avant le journal — aucun relevé » / « aucun relevé » / « » ; `InstantLecture = min(LueA, Plage.Fin)` |
| `PiedQuatreSemaines` | `string` | « Rien n'est inventé avant l'ouverture du journal. » |
| `Theme` | `ChronosTheme` (observable) | + `ActualiserTheme()` |

Navigation : `PlageCourante` = union `[S-3.Debut, S.Fin[` ; `LibellePeriode` = « 4 semaines de forfait · du sam. 29 août au sam. 26 sept. 2026 » ;
‹ = bloc précédent (1er → 29 août : le libellé écrit « sam. 1 août », format `d` comme ailleurs) ; › = bloc suivant, jamais au-delà du présent ;
`TexteRetourPresent` = « Cette semaine » ; `AfficherMaintenant` = faux ; `TexteFraicheur` = `LigneFraicheurSemaine(Courante)`.
Mots pour la bande : `TextesHistorique.CouvertureParSemaine` (« COUVERTURE PAR SEMAINE »). Le segment reste désactivé dans
`HistoriqueWindow.xaml` (non modifié) ; `InfobulleBientot` est gardée (Pitfall 6).

Fixture de la semaine épuisée : `FixturesQuatreSemaines.SemaineUnEpuisee(d, tz)`. Elle relit `ScenariosHistorique.Journal(S-1)` et
pousse chaque relevé à partir de `DebutEpuisee` (jeu. 17 sept. 20:00 Paris) à `U7 = 1,0`, `Statut7 = Rejete`. Puis elle ré-analyse
à `S-1.Fin`. `ScenariosHistorique.cs` n'a pas changé (décision 1). À brancher par `FakeSourceHistorique.TransformerQuatreSemaines`.

## Assertions épinglées changées, et pourquoi

| Fichier | Avant | Après | Raison |
|---------|-------|-------|--------|
| `HonneteteHistoriqueTests.La_fraicheur_du_jour_…` | mercredi « … · 0 interruption » ; `n = Serie.Count` | « … · 1 interruption (Chronos arrêté, mar. 23:00 → 07:00) », `n`/`m` = relevés DANS la plage, `Trou(TrouADebut, TrouAFin, ChronosArrete)` présent. Nouveau : aucun palier NIVEAU avant 07:00 | la veille est lue, le trou de minuit est nommé ; rien n'est inventé avant 07:00 |
| `VueJourBindingTests.Les_resets_observes_…_mercredi` | 3 resets (09/14/19), `DoesNotContain("reset 5 h 04:00")`, `Empty(AnnotationsTrous)` | 4 resets (04/09/14/19) ; UNE annotation « Chronos arrêté » posée à x ∈ [−1, 1] ; couverture : « present » à 7/24 (inchangé), « trou 0 → 7/24 arrete » | le reset de 04:00 est désormais OBSERVÉ : le relevé de mar. 23:00 annonce 04:00, celui de mer. 07:00 annonce 09:00. D-32-27 compte un reset même à travers un trou. C'est un fait, pas une projection |
| `HistoriqueViewModelTests.La_vue_Jour_annonce_…` | mercredi `Empty(AnnotationsSauts)` | UN saut « au moins un reset pendant l'absence (répartition inconnue) » 21:00Z → 05:00Z | le trou de minuit se ferme dans l'analyse du jour ; son Δ 5 h traverse le reset de 04:00, donc il est indéterminable |
| `HistoriqueViewModelTests.La_vue_Jour_lit_…` | `ChoisirVue(QuatreSemaines)` → no-op | lignes retirées ; le test `La_vue_quatre_semaines_s_ouvre_sur_le_bloc_courant` les remplace | le segment n'est plus un no-op côté VM |
| `TextesHistoriqueTests.La_ligne_de_fraicheur_jour_…` | série de 262 relevés débordant sur la veille ; trou du mardi vu du jeudi « 23:00 → 07:00 » ; 25/10 et 28/03 sur une série hors du jour | série posée DANS le jeudi (262 conservé) ; « mar. 23:00 → mer. 07:00 » et « mar. 23:00 → en cours » ; 25/10 / 28/03 avec 3 relevés du jour (« 300 / 276 relevés attendus · 3 présents ») | « présents » compte le jour, et une borne antérieure au jour porte son jour |

Autre fait nouveau, testé par aucune assertion épinglée : le jeudi 24 affiche désormais « reset 5 h 00:00 ». Le relevé de mer. 23:55
annonce 00:00, celui de jeu. 00:00 annonce une nouvelle borne, donc le reset est observé à `Plage.Debut`. C'est honnête. Le plateau
épuisé du mercredi ne déborde pas sur le jeudi, parce que la série rendue est restreinte au jour.

## Mutations (par copie dans `snap-35-01`, révoquées par re-copie, sha256 identiques avant / après)

| # | Mutation | Rouge nommé | sha256 (avant = après) |
|---|----------|-------------|------------------------|
| d1 | `LireQuatreSemaines` du disque analyse au vrai `now` | `SourceHistoriqueDisqueTests.Quatre_semaines_revolues_ne_finissent_pas_par_un_faux_trou_ouvert` | `SourceHistoriqueDisque.cs` 460582467e83… |
| d2 | `Reporter = cible.Debut + (t − source.Debut)` | `EchellesHistoriqueTests.Reporter_absorbe_la_semaine_de_169_h` | `EchelleTemps.cs` 2b4b5068642d… |
| v1 | épuisée = `r.U7 > 1.0` sans `Rejete` | `HistoriqueViewModelTests.La_semaine_epuisee_est_annotee_et_reportee_sur_l_axe_de_S` | `HistoriqueViewModel.cs` d6d6c32263a5… |
| v2 | `avant = a.Serie.Count == 0` | `HistoriqueViewModelTests.Les_etiquettes_distinguent_avant_le_journal_et_pas_de_releves` | idem |
| v3 | réabonnement retiré de `DemarrerHorloge` | `ReouvertureHistoriqueTests.Le_bandeau_F2_revit_a_la_deuxieme_ouverture` | idem |
| v4 | `-= SurFermetureDemandee` retiré | `ReouvertureHistoriqueTests.Une_fenetre_fermee_ne_reste_pas_accrochee_au_VM` | `HistoriqueWindow.xaml.cs` 02abeebfae13… |
| m1 | `PourLeJour` sans les événements de la veille | `LectureVeilleTests.Le_trou_qui_chevauche_minuit_garde_sa_cause` (volet « jeton invalide » → cause inconnue) | `LectureVeille.cs` 2073bc7ea063… |
| m2 | `LigneFraicheurJour` compte `a.Serie.Count` | `TextesHistoriqueTests.La_fraicheur_du_jour_ne_compte_que_les_releves_du_jour_et_date_la_veille` | `TextesHistorique.cs` 6ea3393c2bbf… |
| m3 (ajoutée) | `RestreindreAuJour` rend l'analyse telle quelle | `LectureVeilleTests.La_serie_rendue_…`, `SourceHistoriqueDisqueTests.Le_jour_lit_la_veille_…`, `HonneteteHistoriqueTests.La_fraicheur_du_jour_…`, `VueJourBindingTests.Les_resets_observes_…_mercredi` | `LectureVeille.cs` 2073bc7ea063… |

m2 ne rougit PAS le test d'honnêteté mis à jour. La cause est D-35-06 : la façade rend déjà une série restreinte au jour, donc le compte
fait par la façade et celui fait par `LigneFraicheurJour` coïncident. m2 est attrapée par le test de textes, qui construit une analyse
contenant un relevé de la veille. m3 prouve la restriction de bout en bout.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] La série rendue en vue Jour est restreinte au jour (`LectureVeille.RestreindreAuJour`, D-35-06)**
- **Trouvé pendant :** tâche 3.
- **Problème :** le plan voulait garder le relevé de la veille dans `DonneesJour.Analyse.Serie` (`Serie[0].T < Plage.Debut`) et exigeait
  en même temps « aucune bande présent avant 07:00 » et « aucun palier entre 00:00 et 07:00 ». `PisteCouverture` peint « présent » de
  `Serie[0]` à `Serie[^1]`. Avec la veille dans la série, elle aurait peint une présence de 00:00 à 07:00 sous le trou, et le réticule
  aurait pu s'accrocher à un relevé de la veille daté « 23:00 » sans son jour. `PisteCouverture` et `VueJourView.xaml` n'appartiennent
  pas à ce plan.
- **Correction :** la veille sert à l'analyse (trou, cause, saut, reset observé), puis la série rendue est restreinte au jour. Trous,
  sauts, resets et Δ restent inchangés. Conséquence : `Serie[0].T == TrouAFin` le mercredi, comme avant. L'assertion du plan
  « `Serie[0].T < Plage.Debut` » est donc remplacée par « le trou est nommé et la série rendue commence à 07:00 ».
- **Fichiers :** `LectureVeille.cs`, `SourceHistoriqueDisque.cs`, `SourceHistoriqueDemonstration.cs`.
- **Commit :** `760d449` (tests `303c2c3`).

**2. [Rule 3 - Bloquant] Fixtures de `TextesHistoriqueTests.La_ligne_de_fraicheur_jour_…` recalées**
- La série de 262 relevés débordait sur la veille, et les cas 25/10 et 28/03 comptaient une série hors du jour. Avec « présents = relevés
  du jour », ces cas auraient dit 207 ou « aucun relevé ». Les séries sont maintenant posées dans leur jour, avec des assertions au moins
  aussi fortes (« 300 relevés attendus · 3 présents »).

**3. [Ajout] Volet « jeton invalide » dans `Le_trou_qui_chevauche_minuit_garde_sa_cause`**
- Avec la fixture du plan (`arret` puis `demarrage` à 05:00Z), le `demarrage` du jour suffit à `AnalyseReleves` pour dire « Chronos
  arrêté ». La mutation m1 ne pouvait donc pas rougir. Le même test ajoute un `jeton_invalide` écrit mardi 23:02 et une `reprise` à
  07:00 : seuls les événements de la veille donnent cette cause.

## Known Stubs

Aucun. La vue 4 semaines n'a pas encore de XAML (35-04). Le segment reste désactivé avec son infobulle « bientôt (phase 35) ». C'est
voulu (Pitfall 6).

## Self-Check: PASSED

- Fichiers créés présents : `LectureVeille.cs`, `FixturesQuatreSemaines.cs`, `ReouvertureHistoriqueTests.cs`, `LectureVeilleTests.cs`.
- Commits présents : `0b08e05`, `46f6055`, `f684026`, `e2a9169`, `303c2c3`, `760d449`.
- `git diff --stat b3b08f7 -- AnalyseReleves.cs ScenariosHistorique.cs HistoriqueWindow.xaml` : vide. `HistoriqueWindow.xaml.cs` : CRLF.
- ROADMAP.md / STATE.md : non modifiés par ce plan.
