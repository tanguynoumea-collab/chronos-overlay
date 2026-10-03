---
phase: 38
slug: historique-pistes-seul-et-plein-cran
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-10-03
---

# Phase 38 — Validation Strategy

> Contrat de validation de la phase. Détail des critères → tests : `38-RESEARCH.md` § « Validation Architecture ».
> Les nouveaux fichiers de test sont créés en tête de leur tâche (RED → GREEN) : chaque tâche a une commande automatisée.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + Xunit.StaFact 1.1.11 (`[WpfFact]`), net8.0-windows |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` (chemins des sources / docs injectés par `AssemblyMetadata`) |
| **Quick run command** | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -c Debug --nologo -v q --filter "FullyQualifiedName~Historique\|FullyQualifiedName~VueSemaine\|FullyQualifiedName~VueJour\|FullyQualifiedName~QuatreSemaines\|FullyQualifiedName~PleinEcran\|FullyQualifiedName~SettingsService\|FullyQualifiedName~Reglages\|FullyQualifiedName~GardeTokens\|FullyQualifiedName~GardeDocumentation"` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` (deux passes) puis `dotnet build Chronos.sln -c Release --nologo` (0 avertissement) |
| **Estimated runtime** | rapide ≈ 40 s ; complète ≈ 2-3 min |

---

## Sampling Rate

- **After every task commit:** commande rapide filtrée (ou la commande `<automated>` de la tâche)
- **After every plan wave:** suite complète Debug, deux passes (tests WPF sensibles au dispatcher) + build Release 0 avertissement
- **Before `/gsd:verify-work`:** suite complète verte + build 0 avertissement Debug et Release ; constat manuel plein écran reporté en phase 43
- **Max feedback latency:** 60 s

Vagues strictement séquentielles (01 → 06) : un seul arbre de travail et un seul `obj/` ; des exécuteurs parallèles se casseraient
mutuellement la compilation, et les fichiers `VueSemaineView.xaml`, `DesignTokens.xaml`, `HistoriqueWindow.xaml`, `TextesHistorique.cs`
sont partagés entre plans.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 38-01-01 | 01 | 1 | HIS-09 | build + grep | `dotnet build src/Chronos/Chronos.csproj -c Debug` | ✅ | ⬜ pending |
| 38-01-02 | 01 | 1 | HIS-09 | `[WpfFact]` vue + garde tokens | `--filter "FullyQualifiedName~VueSemaine\|FullyQualifiedName~VueJour\|FullyQualifiedName~HonneteteHistorique\|FullyQualifiedName~PistesHistorique\|FullyQualifiedName~GardeTokens\|FullyQualifiedName~TextesHistorique"` | ✅ (à adapter) | ⬜ pending |
| 38-02-01 | 02 | 2 | HIS-09 | build + grep | `dotnet build src/Chronos/Chronos.csproj -c Debug` | ✅ | ⬜ pending |
| 38-02-02 | 02 | 2 | HIS-09 | `[Fact]` témoin « Tuiles » + `[WpfFact]` sélecteurs absents | `--filter "FullyQualifiedName~SettingsService\|FullyQualifiedName~HistoriqueBinding\|FullyQualifiedName~HistoriqueViewModel\|FullyQualifiedName~Reglages\|FullyQualifiedName~TextesHistorique"` | ❌ W0 (témoin nouveau) | ⬜ pending |
| 38-02-03 | 02 | 2 | HIS-09 | garde documentation | `--filter "FullyQualifiedName~GardeDocumentation"` | ✅ (à adapter) | ⬜ pending |
| 38-03-01 | 03 | 3 | HIS-10 | `[WpfFact]` contenu du dictionnaire + types des tokens | `--filter "FullyQualifiedName~PleinEcranVuesTests\|FullyQualifiedName~GardeTokens"` | ❌ W0 (`PleinEcranVuesTests.cs`) | ⬜ pending |
| 38-03-02 | 03 | 3 | HIS-10 | `[WpfFact]` Measure/Arrange Semaine + garde « pas de StaticResource échelonnée » | `--filter "FullyQualifiedName~PleinEcranVuesTests\|FullyQualifiedName~VueSemaine\|FullyQualifiedName~HonneteteHistorique"` | ❌ W0 | ⬜ pending |
| 38-04-01 | 04 | 4 | HIS-10 | `[WpfFact]` Measure/Arrange Jour | `--filter "FullyQualifiedName~PleinEcranVuesTests\|FullyQualifiedName~VueJour"` | ❌ W0 | ⬜ pending |
| 38-04-02 | 04 | 4 | HIS-10 | `[WpfFact]` Measure/Arrange 4 semaines + non-troncature des étiquettes (colonne 140) | `--filter "FullyQualifiedName~PleinEcranVuesTests\|FullyQualifiedName~QuatreSemaines"` | ❌ W0 | ⬜ pending |
| 38-05-01 | 05 | 5 | HIS-11 | `[Fact]` purs `Echap`, `BornesDip` (100 / 125 / 150 %, origine négative) | `--filter "FullyQualifiedName~PleinEcranHistoriqueTests"` | ❌ W0 (`PleinEcranHistoriqueTests.cs`) | ⬜ pending |
| 38-05-02 | 05 | 5 | HIS-11, HIS-10 | `[Fact]` VM (EstPleinEcran, Échap ×2, non persisté) + libellés | `--filter "FullyQualifiedName~HistoriqueViewModel\|FullyQualifiedName~TextesHistorique"` | ✅ (à étendre) | ⬜ pending |
| 38-06-01 | 06 | 6 | HIS-10, HIS-11 | build + grep | `dotnet build src/Chronos/Chronos.csproj -c Debug` | ✅ | ⬜ pending |
| 38-06-02 | 06 | 6 | HIS-11, HIS-10 | `[WpfFact]` liaisons F11 / Échap / bouton, géométrie injectée, propagation 4 propriétaires | `--filter "FullyQualifiedName~HistoriqueBinding\|FullyQualifiedName~PleinEcran"` | ✅ (à étendre) | ⬜ pending |
| 38-06-03 | 06 | 6 | HIS-10 | `[WpfFact]` honnêteté normal = plein écran (3 vues, galerie) + garde doc | `--filter "FullyQualifiedName~HonneteteHistorique\|FullyQualifiedName~GardeDocumentation"` | ✅ (à étendre) | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

Critères de succès du ROADMAP → tâches : C1 (un seul style, témoin « Tuiles ») → 38-01-*, 38-02-* ; C2 (écran courant, proportions,
plafonds, plus de défilement) → 38-03-02, 38-04-*, 38-06-01/02 ; C3 (paliers en tokens, petit écran sans troncature) → 38-03-01,
38-03-02, 38-04-* ; C4 (sortie, géométrie restaurée) → 38-05-*, 38-06-02 ; C5 (honnêteté à toutes les tailles) → 38-06-03.

---

## Wave 0 Requirements

- [ ] `tests/Chronos.Tests/PleinEcranVuesTests.cs` — dictionnaire PleinEcran, Measure/Arrange des 3 vues, garde « aucune StaticResource échelonnée » (créé en 38-03-01, étendu en 38-03-02, 38-04-*, 38-06-02)
- [ ] `tests/Chronos.Tests/PleinEcranHistoriqueTests.cs` — `Echap`, `BornesDip`, pureté (créé en 38-05-01)
- [ ] `SettingsServiceTests.Un_ancien_reglage_de_style_d_historique_se_lit_sans_perte` — témoin HIS-09 (38-02-02)
- [ ] `GardeTokensHistoriqueTests.TaillesHisto` — −6 tokens (38-01-02), +18 tokens et test des `GridLength` / `ScrollBarVisibility` (38-03-01)
- [ ] Aides de test sans paramètre de style (`VueSemaineBindingTests.Monter`, `HonneteteHistoriqueTests.Vm` / `MonterSemaine`) — 38-01-02

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Barre des tâches couverte sans `Topmost` | HIS-10 | détection automatique du shell Windows, non simulable en test | Phase 43 : `Chronos.exe --historique`, F11 → la barre des tâches passe sous la fenêtre ; si non, ouvrir le repli `ITaskbarList2.MarkFullscreenWindow` (non livré en phase 38) |
| Moniteur secondaire / DPI mixte | HIS-10, HIS-11 | dépend du matériel ; `BornesDip` est testé en pur à 1,0 / 1,25 / 1,5 | Phase 43 : déplacer la fenêtre sur le second écran, F11 → elle couvre CET écran ; Échap → retour exact à la position d'avant |
| Coins carrés en plein écran (Windows 11) | HIS-10 | rendu DWM | Phase 43 : aucun coin de bureau visible aux quatre angles en plein écran ; coins arrondis revenus après Échap |
| Lisibilité des paliers ×1,35 et de l'en-tête 124 DIP (écart au contrat) | HIS-10 | jugement visuel (revue § 9 critère 4) | Phase 43 : revue visuelle de l'en-tête et des trois vues en plein écran, sur 1280 × 720 et sur le grand écran de l'utilisateur |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
