---
phase: 37
slug: une-cha-ne-de-donn-es-claire
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-10-03
---

# Phase 37 — Validation Strategy

> Contrat de validation de la phase. Détail des critères → tests : `37-RESEARCH.md` § « Validation Architecture ».
> Six plans SÉQUENTIELS (vagues 1 → 6) pour les cinq étapes de la liste de purge validée : l'étape 5 est scindée en 37-05
> (réconciliateur + production, AUCUN commit de code) et 37-06 (tests, gardes, docs, UNIQUE commit de l'étape 5), exécutés à la
> suite sans rien d'intercalé. **Un commit de code par étape** (cinq au total), suite complète verte après CHACUN (critère 1 du
> ROADMAP). Entre 37-05 et 37-06, seule la production est garantie compilable : la suite complète n'est exigée qu'au commit.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`), net8.0-windows |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` (chemins `CheminSourcesChronos` / `CheminDocsChronos` injectés) |
| **Quick run command** | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~GardesDoctrineTests\|FullyQualifiedName~CompositionRootTests\|FullyQualifiedName~ServicesLayerPurityTests"` |
| **Full suite command** | `dotnet build Chronos.sln -warnaserror` (0 avertissement) puis `dotnet test Chronos.sln` |
| **Estimated runtime** | rapide ≈ 20 s ; complète ≈ 2-3 min |

---

## Sampling Rate

- **After every task :** commande rapide + le filtre du fichier touché (cf. `<verify>` de chaque tâche).
- **After every commit d'étape (plans 01, 02, 03, 04, 06) :** build 0 avertissement + suite complète — OBLIGATOIRE avant le commit.
- **After plan 05 (pas de commit) :** `dotnet build src/Chronos/Chronos.csproj -warnaserror` + filtre `ClaudeSettingsReconcilerTests` (tâche 1, avant la casse des sites de test).
- **Before `/gsd:verify-work` :** suite complète verte ; réversibilité optionnelle à blanc d'une étape :
  `git revert --no-commit <commit d'étape> && dotnet build Chronos.sln && git revert --abort`.
- **Max feedback latency :** 20 s (filtre), 3 min (suite).

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 37-01-01 | 01 | 1 | DAT-04 | unit + garde textuelle | `--filter "FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~GardeDiagnosticHistoriqueTests\|FullyQualifiedName~NormalisationUniqueTests"` | ✅ à adapter + ❌ W0 (garde anti-coffres, ligne de durée, ordre de la chaîne) | ⬜ pending |
| 37-01-02 | 01 | 1 | DAT-05 | build | `dotnet build Chronos.sln -warnaserror` | ✅ | ⬜ pending |
| 37-01-03 | 01 | 1 | DAT-05 | garde doc | `--filter "FullyQualifiedName~GardeDocumentationChaineTests\|FullyQualifiedName~Contrat\|FullyQualifiedName~GardeDocumentation"` puis suite | ❌ W0 (`GardeDocumentationChaineTests.cs`) | ⬜ pending |
| 37-02-01 | 02 | 2 | DAT-02 | unit | `--filter "FullyQualifiedName~BornesPlageTests\|FullyQualifiedName~MainViewModelTests\|FullyQualifiedName~ReglagesBindingTests"` | ✅ à adapter | ⬜ pending |
| 37-02-02 | 02 | 2 | DAT-02 | garde réflexion | `--filter "FullyQualifiedName~GardesPerimetreTests"` puis suite | ❌ W0 (`Aucun_maillon_retire_de_la_chaine_ne_subsiste`) | ⬜ pending |
| 37-03-01 | 03 | 3 | DAT-02 | build prod | `dotnet build src/Chronos/Chronos.csproj -warnaserror` | ✅ | ⬜ pending |
| 37-03-02 | 03 | 3 | DAT-02 | unit + intégration DI | `--filter "FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~CompositionRootTests\|FullyQualifiedName~MainViewModelTests"` | ✅ à adapter | ⬜ pending |
| 37-03-03 | 03 | 3 | DAT-02 | garde + unit | `--filter "FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~LigneJournalTests\|FullyQualifiedName~SettingsServiceTests"` puis suite | ❌ W0 (ligne `EndpointOAuthClaude` ignorée ; garde composite 3 → 2) | ⬜ pending |
| 37-04-01 | 04 | 4 | DAT-02 | build prod | `dotnet build src/Chronos/Chronos.csproj -warnaserror` | ✅ | ⬜ pending |
| 37-04-02 | 04 | 4 | DAT-02 | unit + WPF | `--filter "FullyQualifiedName~MainViewModelTests\|FullyQualifiedName~ReglagesWindowTests\|FullyQualifiedName~HistoriqueViewModelTests\|FullyQualifiedName~BornesPlageTests"` puis suite | ❌ W0 (reset hebdo jamais synthétique) | ⬜ pending |
| 37-05-01 | 05 | 5 | DAT-03 | unit + E/S témoins (temp) | `--filter "FullyQualifiedName~ClaudeSettingsReconcilerTests"` | ✅ à étendre + ❌ W0 (retrait, restauration, bilan, `CommandeInterneHeritee`) | ⬜ pending |
| 37-05-02 | 05 | 5 | DAT-02, DAT-03 | build prod (tests réparés en 37-06) | `dotnet build src/Chronos/Chronos.csproj -warnaserror` | ✅ | ⬜ pending |
| 37-06-01 | 06 | 6 | DAT-02, DAT-03 | unit + WPF + E/S témoins (temp, 3 arguments) | `--filter "FullyQualifiedName~ArgumentsDemarrageTests\|FullyQualifiedName~DiagnosticServiceTests\|FullyQualifiedName~CompositionRootTests\|FullyQualifiedName~ReglagesWindowTests\|FullyQualifiedName~RefreshOrchestratorTests\|FullyQualifiedName~SettingsServiceTests\|FullyQualifiedName~LigneJournalTests"` | ✅ à adapter + ❌ W0 (bilan journalisé, ligne `PontStatusLine` ignorée) | ⬜ pending |
| 37-06-02 | 06 | 6 | DAT-02, DAT-03 | garde + doc | `--filter "FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~GardeDocumentation\|FullyQualifiedName~Contrat"` puis suite complète + commit unique de l'étape 5 | ❌ W0 (ordre du démarrage, réconciliateur sans barre, garde composite 2 → 1) | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Critères du ROADMAP → preuves

| Critère | Requirement | Preuve automatisée |
|---------|-------------|--------------------|
| 1 Sources mortes disparues, doctrine verte à chaque commit, journal relisible, ancre lue | DAT-02 | `Aucun_maillon_retire_de_la_chaine_ne_subsiste` (liste croissante 2→5) ; garde composite 3 → 2 → 1 ; `GardesDoctrineTests` + `CompositionRootTests` dans la suite de chaque commit ; `LigneJournalTests` (sources retirées ignorées) ; `HistoriqueViewModelTests` / `BornesPlageTests` |
| 2 La barre quitte Claude Code proprement | DAT-03 | `ClaudeSettingsReconcilerTests` (Chronos / tierce / sans barre / illisible / inner restauré / 2ᵉ passe / sauvegarde / hooks même écriture) ; `DiagnosticServiceTests.Le_rapport_journalise_le_bilan_du_retrait_de_la_barre` ; garde d'ordre du démarrage ; `ArgumentsDemarrageTests` (`--statusline` → ArgumentInconnu) ; `ReglagesWindowTests` (carte absente) |
| 3 Le diagnostic dit la chaîne réelle, vite | DAT-04 | `DiagnosticServiceTests` (section, ordre, sections mortes absentes, ligne de durée) ; garde structurelle anti-coffres / anti-HTTP |
| 4 Une seule méthodologie, écrite | DAT-05 | `GardeDocumentationChaineTests` (termes retirés interdits ; « plancher » + « ≥ » au §3 ; README nomme la sonde) ; gardes `Contrat*Documente*` et `GardeDocumentationHistoriqueTests` vertes |
| DAT-01 | DAT-01 | Point de contrôle humain franchi le 2026-10-03 (`.zeus/reports/cycle2/liste-purge.md`) — non rejoué |

---

## Wave 0 Requirements

- [ ] `tests/Chronos.Tests/GardeDocumentationChaineTests.cs` (plan 01 ; enrichie plans 03 et 06)
- [ ] Garde structurelle « diagnostic sans coffres ni HTTP » dans `DiagnosticServiceTests.cs` (plan 01)
- [ ] `GardesPerimetreTests.Aucun_maillon_retire_de_la_chaine_ne_subsiste` (plan 02 ; enrichie plans 03, 04, 06)
- [ ] `LigneJournalTests` : sources retirées ignorées (`EndpointOAuthClaude` plan 03, `PontStatusLine` plan 06)
- [ ] `MainViewModelTests.Une_fenetre_hebdo_sans_reset_reste_sans_reset_malgre_une_ancre` (plan 04)
- [ ] Cas témoins du retrait + `CommandeInterneHeritee` dans `ClaudeSettingsReconcilerTests` (plan 05, tâche 1, écrits RED d'abord ; réconciliateur TOUJOURS à trois arguments temporaires + `Assert.StartsWith(Path.GetTempPath(), …BackupDir)`)
- [ ] Gardes textuelles « lecture héritée avant Show, réconciliation avant LogStartupAsync » et « réconciliateur sans barre » (plan 06)
- [ ] `DiagnosticServiceTests.Le_rapport_journalise_le_bilan_du_retrait_de_la_barre` (plan 06, réconciliateur à trois arguments temporaires)

Chaque élément Wave 0 est créé DANS la tâche qui en a besoin (tests écrits avant le code de la tâche) : pas de plan Wave 0 séparé,
la phase étant strictement séquentielle.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| La barre Chronos disparaît réellement de Claude Code au premier lancement de l'exe 3.5 ; une sauvegarde `claude-settings-*.json` existe ; second lancement sans écriture | DAT-03 | Interdit de lancer l'exe et d'écrire dans le vrai `~/.claude/settings.json` pendant la phase | Constat de la phase 43 : relevé hors arbre de l'app (sonde WMI), lecture de `chronos.log` section « [Réglages de Claude Code] » |
| `Chronos-v3.5.0.exe --statusline` sort en silence (code 0, aucune fenêtre) | DAT-03 | Lancer l'exe ouvrirait l'overlay si la garde régressait | Constat de la phase 43 |
| Durée réelle du diagnostic (≈ 17 s avant → mesurée après, attendue < 1 s hors réseau) | DAT-04 | Mesure sur la vraie machine | Lire la ligne « Rapport construit en N ms » de `chronos.log` après le premier lancement 3.5 (phase 43) |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 180 s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
