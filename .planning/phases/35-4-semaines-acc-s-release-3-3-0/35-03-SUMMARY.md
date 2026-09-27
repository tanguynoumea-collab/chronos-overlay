---
phase: 35-4-semaines-acc-s-release-3-3-0
plan: 03
subsystem: diagnostic / historique — section « Journal d'historique » (ACC-03)
tags: [ACC-03, DiagnosticService, EtatJournalHistorique, SourceHistoriqueDisque, LireJour, LigneFraicheurJour, InventaireProcessus, fuseau-injecte, garde-de-lecture, CRLF]

sha_entree_de_plan: b3b08f7
one_liner: "Le rapport « Diagnostic… » gagne une section [Journal d'historique], placée juste après [Magasins persistants] : dossier, fuseau (ou « UTC (fuseau non injecté) »), fichiers par familles avec taille et âge, âge de la dernière écriture du journal sur disque, la journée lue par la façade de la fenêtre (SourceHistoriqueDisque.LireJour) et dite par les mots de la vue Jour (LigneFraicheurJour), « journal ouvert le … », les cinq derniers événements, la reconstruction des tokens en une ligne (en cours N / M, terminée le …, pas encore lancée, non câblée) et le nombre d'instances Chronos, tiré du même relevé des processus que [Magasins persistants]. Un helper neutre (EtatJournalHistorique) sert l'inventaire et les événements. Aucun .Lire( ni AnalyseReleves.Analyser( ni TimeZoneInfo.Local dans DiagnosticService.cs, trois gardes nouvelles. Suite 1565 → 1577, 0 warning."

requires:
  - phase: 32-02 / 32-05 (CPT-02, CPT-03, JRN-04)
    provides: "section [Magasins persistants], LigneMagasin, InventaireProcessus.Relever / Lignes, VerrouInstanceUnique.EtatPourDiagnostic"
  - phase: 33-05 (TOK-02)
    provides: "IEtatReconstruction dans le diagnostic, LibellePhase, FakeEtatReconstruction"
  - phase: 34 (HIS-01…)
    provides: "SourceHistoriqueDisque.LireJour, BornesPlage.Jour / FuseauParisPourTests, TextesHistorique.LigneFraicheurJour / JournalOuvertLe / DateLongue"
  - phase: 32-04 (JRN-05)
    provides: "LecteurJournal.Lire (tolérant, dossier absent = lecture vide), TypeEvenementTexte.Nom"
provides:
  - "src/Chronos/Services/Historique/EtatJournalHistorique.cs — FichierHistorique(Nom, Taille, ModifieLe) ; EtatJournalHistorique.Inventaire(dossier) (releves-, tokens-, ids-, curseurs.json, couverture.json ; nom croissant par famille) ; DerniersEvenements(dossier, now, n = 5) (LecteurJournal sur [now − 7 j, now], TakeLast(n) chronologique) ; ne lève jamais"
  - "DiagnosticService — paramètre optionnel TimeZoneInfo? fuseau = null en DERNIÈRE position ; SectionJournalHistorique(sb, nbProcessus) ; relevé des processus unique (D-35-11)"
  - "tests/Chronos.Tests/GardeDiagnosticHistoriqueTests.cs — 3 gardes textuelles sur DiagnosticService.cs"
affects:
  - "35-05 : câbler `fuseau: sp.GetRequiredService<TimeZoneInfo>()` dans l'enregistrement du DiagnosticService (App.xaml.cs appartient à 35-02 en vague 1) — tant que ce n'est pas fait, la section parle en UTC et le dit"
  - "35-05 : docs/data-sources.md §9 peut citer la section du diagnostic"
  - "35-07 (constat VAL-05) : le rapport collé dit à lui seul si le journal écrit, depuis quand, la journée et le nombre de Chronos"

tech-stack:
  added: []
  patterns:
    - "Même lecture que la fenêtre : une instance LOCALE de la façade sans état (new SourceHistoriqueDisque(_paths, tz)) plutôt qu'un second chemin de lecture ; `.LireJour(` ne heurte pas la garde `.Lire(`"
    - "Relevé coûteux fait UNE fois par rapport et partagé entre deux sections (liste détaillée ici, compte là-bas), prouvé par une garde de comptage d'occurrences"
    - "Fuseau optionnel en dernière position avec repli UTC ANNONCÉ dans la sortie, jamais le fuseau local deviné"

key-files:
  created:
    - src/Chronos/Services/Historique/EtatJournalHistorique.cs
    - tests/Chronos.Tests/EtatJournalHistoriqueTests.cs
    - tests/Chronos.Tests/GardeDiagnosticHistoriqueTests.cs
  modified:
    - src/Chronos/Services/DiagnosticService.cs
    - tests/Chronos.Tests/DiagnosticServiceTests.cs
    - .planning/REQUIREMENTS.md

key-decisions:
  - "D-35-10 — même lecture que la fenêtre : la journée se lit par new SourceHistoriqueDisque(_paths, tz).LireJour(BornesPlage.Jour(now, tz), now) et se dit par TextesHistorique.LigneFraicheurJour ; événements et inventaire par le helper neutre EtatJournalHistorique (Services/Historique, où LecteurJournal.Lire est permis)"
  - "D-35-11 — pas de duplication : InventaireProcessus.Relever() une fois dans BuildReportAsync, partagé entre [Magasins persistants] (liste) et la section (compte, « détail sous [Magasins persistants] ») ; la reconstruction tient en une ligne, le détail reste sous les magasins"
  - "D-35-12 — fuseau : TimeZoneInfo? fuseau = null en dernière position ; null → heures en UTC et ligne « Fuseau : UTC (fuseau non injecté) » ; jamais TimeZoneInfo.Local dans DiagnosticService.cs ; câblage production par 35-05"

requirements-completed: [ACC-03]

metrics:
  duration: "≈ 28 min (16:46Z → 17:14Z, dont ≈ 15 min d'attente de l'arbre réel)"
  completed: 2026-09-27
  tasks: 2
  files: 5
  tests: "1565 → 1577 (+4 EtatJournalHistoriqueTests, +5 DiagnosticServiceTests, +3 GardeDiagnosticHistoriqueTests)"
---

# Phase 35 Plan 03 : Section « Journal d'historique » du diagnostic — Summary

Le diagnostic dit maintenant tout ce que le journal sait de lui-même (fichiers, dernière écriture, journée, événements, reconstruction,
instances), par la même lecture et avec les mêmes mots que la fenêtre Historique. Le fuseau est injecté ; sans fuseau, la section
parle en UTC et le dit.

## Exemple de section rendue

Rendu par le montage des tests (Paris injecté, horloge 2026-09-27T12:00Z, reconstruction en cours 886 / 1603). Le profil est masqué
ici : dans le rapport, le chemin est écrit tel quel, comme le fait `LigneMagasin`. En production, le dossier est
`%APPDATA%\Chronos\historique`.

```text
[Journal d'historique]
  Dossier : %TEMP%\ChronosDiag_55d0…\historique
  Fuseau : Romance Standard Time
  Fichiers : 2
    releves-2026-09.jsonl — 15277 o — modifié il y a 2 min
    curseurs.json — 2 o — modifié il y a 1 h 00
  Dernière écriture du journal (disque) : il y a 2 min — releves-2026-09.jsonl
  Jour (27 sept. 2026) : 288 relevés attendus · 71 présents · 0 interruption
  journal ouvert le 27 sept. 2026
  Événements récents (5 derniers) :
    2026-09-27 08:07 demarrage (3.2.2)
  Reconstruction des tokens : en cours — 886 / 1603 fichiers (détail sous [Magasins persistants])
  Instances Chronos : 4 (détail sous [Magasins persistants])
```

Sans fuseau injecté (état de la production jusqu'à 35-05), la ligne devient `Fuseau : UTC (fuseau non injecté)` et l'événement
s'écrit `2026-09-27 06:07 demarrage (3.2.2)`. Le « 4 » des instances correspond aux processus Chronos réellement présents sur la
machine au moment du test : c'est le même relevé que la liste de `[Magasins persistants]`, hooks `--hook` compris.

## Tâches

| # | Tâche | Commits | Fichiers |
|---|-------|---------|----------|
| 1 | Helper neutre `EtatJournalHistorique` : inventaire par familles, cinq derniers événements | RED `39578c7`, GREEN `25e203f` | `Services/Historique/EtatJournalHistorique.cs`, `EtatJournalHistoriqueTests.cs` |
| 2 | Section `[Journal d'historique]` : même lecture que la fenêtre, un seul relevé des processus, fuseau injecté | RED `99cedea`, GREEN `fbb4bdd` | `DiagnosticService.cs` (CRLF conservé), `DiagnosticServiceTests.cs`, `GardeDiagnosticHistoriqueTests.cs` |

- RED 1 : 4 tests, CS0103 `EtatJournalHistorique` (compilé dans l'instantané `snap-35-03`, l'arbre réel portant alors le RED de 35-02).
- RED 2 : 8 tests (5 + 3 gardes), CS1739 « pas de paramètre nommé 'fuseau' ».

## Mutations (jouées dans l'instantané `snap-35-03`, révoquées par copie)

| Id | Mutation | Rouges nommés | sha256 (16 premiers) orig → muté → restauré |
|----|----------|---------------|------------------------------------------|
| e1 | `TakeLast(n)` → `TakeLast(4)` | `EtatJournalHistoriqueTests.Les_cinq_derniers_evenements_dans_l_ordre` | `203642c5703edae7` → `7885794839dd9a44` → `203642c5703edae7` |
| j1 | ligne du jour recopiée à la main (`"288 relevés attendus · " + …`) sans `LigneFraicheurJour(` | `GardeDiagnosticHistoriqueTests.La_journee_se_lit_par_la_facade_de_la_fenetre`, `DiagnosticServiceTests.La_section_dit_la_journee_avec_les_mots_de_la_vue_Jour` | `565bb2eaeb38a954` → `130925f0c795cde4` → `565bb2eaeb38a954` |
| j2 | second `InventaireProcessus.Relever()` passé à la section | `GardeDiagnosticHistoriqueTests.La_table_des_processus_est_relevee_une_seule_fois` | `565bb2eaeb38a954` → `8afa4738ed02f0e4` → `565bb2eaeb38a954` |
| j3 | `_fuseau ?? TimeZoneInfo.Local` | `GardeDiagnosticHistoriqueTests.Aucun_fuseau_local_dans_le_diagnostic`, `DiagnosticServiceTests.Sans_fuseau_la_section_parle_en_UTC_et_le_dit` | `565bb2eaeb38a954` → `909ba0dbbc4b6ea0` → `565bb2eaeb38a954` |

Le fichier `DiagnosticService.cs` commité a le sha256 `565bb2ea…`, celui de l'original des mutations j1 à j3.

## Critères d'acceptation (relevés)

- `EtatJournalHistorique.cs` : `public static class EtatJournalHistorique` = 1 ; `System.Windows` = 0 ; `TakeLast(n)` = 1 ; `TimeZoneInfo.Local` = 0.
- `DiagnosticService.cs` : `[Journal d'historique]` = 1 ; `.LireJour(` = 1 ; `LigneFraicheurJour(` = 1 ; `EtatJournalHistorique.` = 2 ;
  `InventaireProcessus.Relever()` = 1 ; `TimeZoneInfo.Local` = 0 ; `.Lire(` = 0 ; `AnalyseReleves.Analyser(` = 0 ; `LecteurJournal.` = 0 ;
  `TimeZoneInfo? fuseau = null` = 1 ; `file` : « with CRLF line terminators ».
- `[Fact]` : 4 dans `EtatJournalHistoriqueTests.cs`, 3 dans `GardeDiagnosticHistoriqueTests.cs`.
- `GardesPerimetreTests` inchangé et vert (`.Lire(` absent, `Inspecter(` unique, pas de `new SessionMonitor`).
- Release : 0 avertissement, 0 erreur (instantané, `--no-incremental`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Honnêteté] Reconstruction arrêtée, en échec ou incrémentale sans date de fin**
- **Found during:** Tâche 2
- **Issue:** le plan prévoyait quatre formes (« en cours », « terminée le », « pas encore lancée », « non câblée »). Une reconstruction
  `EnEchec` ou `Arretee` sans `DerniereReconstructionTerminee` serait tombée sur « pas encore lancée », ce qui est faux.
- **Fix:** « pas encore lancée » est réservé à `PhaseReconstruction.JamaisLancee`. Les autres phases sans date de fin s'écrivent
  `LibellePhase(phase) — N / M fichiers (détail sous [Magasins persistants])`, avec les mots existants (« EN ÉCHEC », « arrêtée »).
- **Files modified:** `src/Chronos/Services/DiagnosticService.cs`
- **Commit:** `fbb4bdd`

**2. [Précision] « profil masqué comme LigneMagasin »**
- `LigneMagasin` écrit le chemin tel quel, sans masquer le profil. La ligne « Dossier : » fait donc de même (même traitement que
  `LigneMagasin`, testé par `"Dossier : " + paths.HistoriqueDir`). Le profil est masqué dans l'exemple de ce SUMMARY seulement.

**3. [Rule 3 - Blocage] Arbre réel rouge à cause des voisins**
- Les RED de 35-02 (`IOuvreurHistorique` absent) puis de 35-01 (et des éditions non commitées de `JournalRelevesTests.cs` /
  `FakeEtatJournal.cs`, qui ne relèvent pas de ce plan) cassaient la compilation de l'arbre réel. RED, GREEN, mutations et suite
  complète ont été joués dans l'instantané `git archive b3b08f7` → `snap-35-03`, avec les fichiers de ce plan copiés à l'identique.

### Notes de mise en œuvre

- La relève des processus est faite avant `[Magasins persistants]`. Un échec est mémorisé puis redit par le même `catch`, avec
  exactement le même message « Processus Chronos : relevé impossible (…) ». La section dit alors « Instances Chronos : relevé impossible ».
- Le compte d'instances est `processus.Count`, le même nombre que « Processus Chronos : N » (hooks `--hook` éphémères compris,
  signalés dans la liste détaillée).
- Toute la section est sous `try/catch`. Une panne s'écrit « Journal d'historique : relevé impossible (<Type> : <message>) » et ne
  lève jamais d'exception.
- Aucun relevé la veille dans les fixtures. Le résultat est donc identique avant et après la lecture de la veille ajoutée à
  `LireJour` par 35-01. La suite verte sur l'arbre réel, qui porte cette modification, le confirme.

## Câblage restant (hors de ce plan)

Câblage `fuseau: sp.GetRequiredService<TimeZoneInfo>()` par 35-05 : App.xaml.cs appartient à 35-02 en vague 1. D'ici là, la section
écrit ses heures en UTC et l'annonce.

## Known Stubs

Aucun. La ligne « Fuseau : UTC (fuseau non injecté) » en production est un repli annoncé, pas un stub : elle disparaît quand 35-05
passe le fuseau.

## Vérification

- Instantané `snap-35-03` (b3b08f7 + fichiers de ce plan) : suite complète 1577 / 1577 (1565 + 12), 0 échec ; Release 0 avertissement.
- Arbre réel à `00364b3` (porte 35-01 complet, dont la lecture de la veille dans `LireJour`, et 35-02) : suite complète **deux passes
  consécutives 1622 / 1622, 0 échec** ; Release `--no-incremental` 0 avertissement, 0 erreur. Pendant l'attente, les passes
  intermédiaires n'ont montré que les RED commités des voisins (ReglagesBindingTests de 35-02, veille de minuit de 35-01), jamais
  un test de ce plan.
- `REQUIREMENTS.md` : ACC-03 cochée (case et tableau). Le diff ne porte que ces deux lignes. La case avait été remise à « [ ] »
  dans l'arbre par l'écriture d'un voisin, puis recochée avant le commit.

## Self-Check: PASSED

- FOUND : `src/Chronos/Services/Historique/EtatJournalHistorique.cs`, `tests/Chronos.Tests/EtatJournalHistoriqueTests.cs`,
  `tests/Chronos.Tests/GardeDiagnosticHistoriqueTests.cs`, ce SUMMARY.
- FOUND : commits `39578c7`, `25e203f`, `99cedea`, `fbb4bdd`.
- Aucun écart entre HEAD et l'arbre sur les fichiers de ce plan ; ROADMAP.md et STATE.md non touchés.
