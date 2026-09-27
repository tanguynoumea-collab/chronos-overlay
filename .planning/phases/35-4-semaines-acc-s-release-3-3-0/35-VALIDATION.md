---
phase: 35
slug: 4-semaines-acc-s-release-3-3-0
status: planned
nyquist_compliant: true
wave_0_complete: false
created: 2026-09-27
---

# Phase 35 — Validation Strategy

> Contrat de validation de la phase : ce qui est prouvé par un test automatique, à quel moment, et ce qui ne peut être que
> constaté avec l'utilisateur (VAL-05). 7 plans en 4 vagues (décision 6 de l'orchestrateur, 35-CONTEXT).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`, `[Collection("XAML WPF")]` pour tout test qui monte du XAML) |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` (AssemblyMetadata `CheminSourcesChronos` / `CheminDocsChronos`) |
| **Quick run command** | `dotnet test Chronos.sln -c Debug --nologo -v q --filter "FullyQualifiedName~<Classe1>\|FullyQualifiedName~<Classe2>"` (filtre donné par chaque tâche) |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` (1565 verts à l'entrée, `b77dd17`) ; porte : `dotnet build Chronos.sln -c Release --nologo` → 0 avertissement |
| **Estimated runtime** | ≈ 15 s la suite (≈ 25 s mur, compilation comprise) ; ≤ 30 s un filtre de tâche |

---

## Sampling Rate

- **After every task commit:** le filtre `<automated>` de la tâche (RED nommé puis GREEN).
- **After every plan:** suite complète DEUX fois (0 échec) + `dotnet build -c Release` 0 avertissement ; en vague parallèle, si le RED d'un voisin
  casse la compilation de l'arbre partagé, la boucle tourne dans un instantané `snap-35-0N` (`git archive <sha d'entrée>` + les fichiers du plan) puis
  la suite complète est rejouée deux fois sur l'arbre réel dès que le voisin est GREEN (précédent 34-04 / 34-05).
- **After every wave (orchestrateur):** suite complète deux fois sur l'arbre réel.
- **Before 35-06 (release):** suite complète verte deux fois, Release 0 avertissement.
- **Before `/gsd:verify-work`:** `35-CONSTAT.md` § « Verdict » rendu.
- **Max feedback latency:** 30 s.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 35-01-01 | 01 | 1 | HIS-05 | unit | `--filter "FullyQualifiedName~SourceHistoriqueDisqueTests\|FullyQualifiedName~EchellesHistoriqueTests\|FullyQualifiedName~TextesHistoriqueTests"` | ✅ (à étendre) | ⬜ pending |
| 35-01-02 | 01 | 1 | HIS-05 | unit | `--filter "FullyQualifiedName~HistoriqueViewModelTests\|FullyQualifiedName~ReouvertureHistoriqueTests"` | ✅ / ❌ W0 (`ReouvertureHistoriqueTests.cs`) | ⬜ pending |
| 35-01-03 | 01 | 1 | (34 différé : minuit) | unit + WpfFact | `--filter "FullyQualifiedName~LectureVeilleTests\|FullyQualifiedName~VueJourBindingTests\|FullyQualifiedName~HonneteteHistoriqueTests"` | ❌ W0 (`LectureVeilleTests.cs`) | ⬜ pending |
| 35-02-01 | 02 | 1 | ACC-02 | unit | `--filter "FullyQualifiedName~ArbitreClicCentreTests\|FullyQualifiedName~MainViewModelTests\|FullyQualifiedName~GardeGestesCadranTests"` | ❌ W0 | ⬜ pending |
| 35-02-02 | 02 | 1 | ACC-02 | WpfFact + unit | `--filter "FullyQualifiedName~OuvreurHistoriqueTests\|FullyQualifiedName~CompositionRootTests"` | ❌ W0 (`OuvreurHistoriqueTests.cs`) | ⬜ pending |
| 35-02-03 | 02 | 1 | ACC-01 | WpfFact + unit | `--filter "FullyQualifiedName~ReglagesBindingTests\|FullyQualifiedName~JournalRelevesTests\|FullyQualifiedName~GardeTokensHistoriqueTests"` | ✅ | ⬜ pending |
| 35-03-01 | 03 | 1 | ACC-03 | unit | `--filter "FullyQualifiedName~EtatJournalHistoriqueTests"` | ❌ W0 | ⬜ pending |
| 35-03-02 | 03 | 1 | ACC-03 | unit | `--filter "FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~GardeDiagnosticHistoriqueTests\|FullyQualifiedName~GardesPerimetreTests"` | ✅ / ❌ W0 | ⬜ pending |
| 35-04-01 | 04 | 2 | HIS-05 | WpfFact + unit | `--filter "FullyQualifiedName~PistesHistoriqueTests\|FullyQualifiedName~GardeTokensHistorique"` | ✅ (à étendre) | ⬜ pending |
| 35-04-02 | 04 | 2 | HIS-05 | WpfFact | `--filter "FullyQualifiedName~VueQuatreSemainesBindingTests\|FullyQualifiedName~HonneteteQuatreSemainesTests\|FullyQualifiedName~HistoriqueBindingTests\|FullyQualifiedName~TextesHistoriqueTests"` | ❌ W0 | ⬜ pending |
| 35-04-03 | 04 | 2 | HIS-05 (reprises 34 + DAEDALUS) | WpfFact | `--filter "FullyQualifiedName~HistoriqueConvertersTests\|FullyQualifiedName~VueJourBindingTests\|FullyQualifiedName~VueSemaineBindingTests"` | ✅ | ⬜ pending |
| 35-05-01 | 05 | 2 | ACC-04 | unit | `--filter "FullyQualifiedName~GardeDocumentationHistoriqueTests"` | ❌ W0 | ⬜ pending |
| 35-05-02 | 05 | 2 | ACC-04 | unit | `--filter "FullyQualifiedName~GardeDocumentationHistoriqueTests\|FullyQualifiedName~ContratAgregatsDocumenteTests\|FullyQualifiedName~ContratJournalDocumenteTests"` | ✅ | ⬜ pending |
| 35-05-03 | 05 | 2 | ACC-03 (fuseau injecté) | unit | `--filter "FullyQualifiedName~GardeDiagnosticHistoriqueTests\|FullyQualifiedName~CompositionRootTests"` | ✅ | ⬜ pending |
| 35-06-01 | 06 | 3 | ACC-04 | unit + scripté | `dotnet test Chronos.sln -c Debug --nologo -v q` ×2 ; `--filter FullyQualifiedName~VersionPublieeTests` | ✅ | ⬜ pending |
| 35-06-02 | 06 | 3 | ACC-04 | scripté (agent) | publish + contrôles (taille, 0 DLL, VersionInfo 3.3.0.0 / 3.3.0, md5 ≠ 3.2.2) + smoke `--hook SessionStart` (md5 `settings.json` inchangé) | — | ⬜ pending |
| 35-07-* | 07 | 4 | VAL-05 | manuel (utilisateur) + sondes WMI hors arbre | `python -c …` de chaque tâche sur `35-CONSTAT.md` | ❌ (créé par 35-07) | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Mutations prévues (par copie, révoquées par re-copie, sha256 comparés — jamais commitées)

| Plan | Mutation | Rouge attendu |
|------|----------|---------------|
| 35-01 | `LireQuatreSemaines` analyse au vrai `now` (sans `InstantDAnalyse`) | `SourceHistoriqueDisqueTests.Quatre_semaines_revolues_ne_finissent_pas_par_un_faux_trou_ouvert` |
| 35-01 | `EchelleTemps.Reporter` sur 168 h fixes | `EchellesHistoriqueTests.Reporter_absorbe_la_semaine_de_169_h` |
| 35-01 | épuisée sans `Statut7 == Rejete` | `HistoriqueViewModelTests.La_semaine_epuisee_est_annotee_et_reportee_sur_l_axe_de_S` |
| 35-01 | « avant le journal » pour une semaine postérieure sans relevé | `HistoriqueViewModelTests.Les_etiquettes_distinguent_avant_le_journal_et_pas_de_releves` |
| 35-01 | `LectureVeille` sans les événements de la veille | `LectureVeilleTests.Le_trou_qui_chevauche_minuit_garde_sa_cause` |
| 35-01 | `DemarrerHorloge` sans réabonnement | `ReouvertureHistoriqueTests.Le_bandeau_F2_revit_a_la_deuxieme_ouverture` |
| 35-02 | l'arbitre bascule au premier clic | `ArbitreClicCentreTests.Un_double_clic_ouvre_sans_aucune_bascule` |
| 35-02 | `ClickCount >= 2` n'annule pas l'attente | idem |
| 35-02 | l'ouvreur crée une fenêtre à chaque appel | `OuvreurHistoriqueTests.Deux_ouvertures_une_seule_fenetre` |
| 35-02 | puce de la carte liée à une autre commande | `ReglagesBindingTests.La_carte_Historique_pilote_le_meme_style_que_la_fenetre` |
| 35-03 | section sans `LigneFraicheurJour` (texte recopié) | `GardeDiagnosticHistoriqueTests.La_journee_se_lit_par_la_facade_de_la_fenetre` |
| 35-03 | `TakeLast(4)` | `EtatJournalHistoriqueTests.Les_cinq_derniers_evenements_dans_l_ordre` |
| 35-03 | seconde lecture de la table des processus | `GardeDiagnosticHistoriqueTests.La_table_des_processus_est_relevee_une_seule_fois` |
| 35-04 | fantôme peint par la rampe | `PistesHistoriqueTests.Quatre_semaines_fantomes_gris_aux_opacites_des_tokens` |
| 35-04 | opacité S-1 liée au token de S-3 | `VueQuatreSemainesBindingTests.La_piste_recoit_les_opacites_des_tokens` |
| 35-04 | bande épuisée de S par `ArcBrush(1.0)` | `HonneteteQuatreSemainesTests.La_semaine_epuisee_est_un_plateau_gris_annote` |
| 35-04 | `OpaciteS1="0.8"` littéral | `GardeTokensHistoriqueTests.Aucune_couleur_ni_taille_en_dur…` |
| 35-04 | infobulle non bornée | `VueJourBindingTests.L_infobulle_reste_dans_la_piste_au_bord_droit` |
| 35-05 | « estimation » ajouté à la section README | `GardeDocumentationHistoriqueTests.Aucune_projection_dans_la_section_Historique_du_README` |
| 35-05 | ligne finale de `data-sources.md` sans « §8 » | `ContratAgregatsDocumenteTests` (ligne finale) |

---

## Wave 0 Requirements

- [ ] `tests/Chronos.Tests/LectureVeilleTests.cs`, `ReouvertureHistoriqueTests.cs`, `Fakes/FixturesQuatreSemaines.cs` (35-01)
- [ ] `tests/Chronos.Tests/ArbitreClicCentreTests.cs`, `OuvreurHistoriqueTests.cs`, `GardeGestesCadranTests.cs`, `Fakes/FakeOuvreurHistorique.cs` (35-02)
- [ ] `tests/Chronos.Tests/EtatJournalHistoriqueTests.cs`, `GardeDiagnosticHistoriqueTests.cs` (35-03)
- [ ] `tests/Chronos.Tests/VueQuatreSemainesBindingTests.cs`, `HonneteteQuatreSemainesTests.cs` (35-04)
- [ ] `tests/Chronos.Tests/GardeDocumentationHistoriqueTests.cs` (35-05)
- Framework : aucun ajout, aucune dépendance NuGet nouvelle. Chaque fichier est créé par la tâche RED de son plan (TDD) : pas de vague 0 séparée.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Toutes les anciennes instances quittées, `Chronos-v3.3.0.exe` lancé par l'Explorateur, second lancement refusé (« Chronos tourne déjà »), un seul cadran | VAL-05 (et E1-ter de VAL-04) | l'agent ne lance, n'arrête ni ne clique jamais l'overlay ; le verrou ne se prouve que sur la vraie machine | 35-07 point (a) ; sonde WMI hors arbre : un seul processus `Chronos-v3.3.0.exe`, parent `explorer.exe`, 0 ancienne |
| Réconciliation au premier lancement dans `~/.claude/settings.json` (sauvegarde = état d'avant, 9 remplacements → `Chronos-v3.3.0.exe`, 8 hooks + statusLine, 3 `gsd-`) | VAL-05 / ACC-04 | écrite par l'overlay au lancement | 35-07 tâche 3, script Python (0, 9, 8, 3, True) |
| La fenêtre s'ouvre par les DEUX gestes (carte F1 « Ouvrir », double-clic au centre) ; ramenée au premier plan, une seule entrée dans la barre des tâches ; simple clic bascule toujours (après ≈ 0,5 s, coût annoncé) ; drag et clic droit inchangés | VAL-05 (ACC-01, ACC-02) | geste souris réel, délai système réel | 35-07 point (b) |
| Semaine courante sur les VRAIES données : relevés depuis le 27/09 08:07 (zone hachurée avant, « journal ouvert le 27 sept. 2026 »), tokens reconstruits ; trois styles sélectionnables | VAL-05 | données de la machine de l'utilisateur | 35-07 point (b) + sonde (fichiers `tokens-*`, `curseurs.json`, `couverture.json`) |
| Vue 4 semaines : S partielle avec marqueur, S-1…S-3 « pas de relevés (avant le journal) » | VAL-05 (HIS-05) | idem | 35-07 point (b) |
| Un trou réel après au moins une nuit : rectangle gris à bordure pointillée annoté de sa cause (« Chronos arrêté » si arrêt / redémarrage, « cause inconnue » si veille — les deux sont honnêtes) | VAL-05 | exige une nuit réelle | 35-07 point (d), le lendemain |
| L'utilisateur relit les libellés d'honnêteté et rend un verdict écrit, écarts compris | VAL-05 | jugement humain | 35-07 point (d) → `35-CONSTAT.md` § « 4. Verdict » |
| Tableau des gestes de 32-08 (L1, L2, L2b, L3, L4, Q) et V01…V12, s'ils n'ont toujours pas été joués | VAL-04 (phase 32) | idem | 35-07 point (c), même séance |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies (les points de contrôle humains de 35-07 ont un relevé automatique en tâche suivante)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (créés par les RED des plans)
- [x] No watch-mode flags
- [x] Feedback latency < 30 s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending — signée par 35-07 (tâche 8) après le verdict du constat.
