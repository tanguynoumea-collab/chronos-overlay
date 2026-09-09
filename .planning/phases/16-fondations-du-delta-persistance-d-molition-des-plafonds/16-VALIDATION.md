---
phase: 16
slug: fondations-du-delta-persistance-d-molition-des-plafonds
status: planned
nyquist_compliant: true
wave_0_complete: false   # fixture DEL-06 à créer en 16-04 T2 ; fixtures JSONL déjà en place
created: 2026-09-09
---

# Phase 16 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (net8.0-windows) — `tests/Chronos.Tests` |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` |
| **Quick run command** | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~Transcript\|FullyQualifiedName~LastExact\|FullyQualifiedName~SettingsService"` |
| **Full suite command** | `dotnet test Chronos.sln -v q --nologo` |
| **Estimated runtime** | ~35 s · baseline mesurée à l'entrée de phase : **405 tests / 0 échec** |

---

## Sampling Rate

- **After every task commit:** quick command
- **After every plan wave:** full suite
- **Before `/gsd:verify-work`:** full suite green
- **Max feedback latency:** ~35 s

---

## Critère de succès sur le compte de tests — À LIRE AVANT DE JUGER

Cette phase **supprime du code et ses tests**. Le bilan attendu est **−23 tests supprimés / ~+25
réécrits ou ajoutés**, soit une cible d'environ **405 ± 10**. Le critère n'est donc PAS « ≥ 405 » mais :

- **0 échec**, et
- **aucune perte de couverture nette** : chaque test supprimé l'est parce que le code qu'il couvrait
  n'existe plus (plafonds), pas parce qu'il est devenu gênant.

Un écart à la baisse du nombre total est légitime ici et ne doit pas être traité comme une régression.

---

## Per-Task Verification Map

*Rempli par le planner.*

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 16-01 T1 — Supprimer l'entrée « Plafonds… », la commande et le dialogue | 16-01 | 1 | DEL-05 | integration (build + suite) | `dotnet clean Chronos.sln --nologo && dotnet build Chronos.sln -v q --nologo && dotnet test Chronos.sln -v q --nologo` | ✅ existants (MainViewModelTests, CadranBindingTests, OverlayWindowConfigTests, ThemingTests, CompositionRootTests) | ⬜ pending |
| 16-01 T2 — Supprimer le calibrateur auto et la logique pure | 16-01 | 1 | DEL-05 | integration (DI) | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~CompositionRootTests"` | ✅ existant (CompositionRootTests) | ⬜ pending |
| 16-01 T3 — Reformuler les commentaires orphelins | 16-01 | 1 | DEL-05 | smoke (suite complète) | `dotnet test Chronos.sln -v q --nologo` | ✅ existants | ⬜ pending |
| 16-02 T1 — LastExactStore (schéma, atomicité, tolérance, rejet des fenêtres rollées) | 16-02 | 1 | EXA-01 | unit | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~LastExactStoreTests"` | ❌ Wave 0 — à créer (≥ 10 tests) | ⬜ pending |
| 16-02 T2 — LastExactUsageProvider (écriture + rebouchage étroit) | 16-02 | 1 | EXA-01 | unit | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~LastExactUsageProviderTests"` | ❌ Wave 0 — à créer (≥ 8 tests) | ⬜ pending |
| 16-03 T1 — Contrat de delta + conversion du provider | 16-03 | 2 | DEL-01, DEL-02 | build | `dotnet build Chronos.sln -v q --nologo` | n/a (compilation ; les tests arrivent en T2) | ⬜ pending |
| 16-03 T2 — Réécriture des tests JSONL en preuves de delta | 16-03 | 2 | DEL-01, DEL-02 | unit | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~TranscriptActivity"` | ❌ Wave 0 — TranscriptActivityProviderTests (10) + TranscriptActivityLogTests (5) | ⬜ pending |
| 16-03 T3 — Recâblage DI (sortie du JSONL, décorateur en tête) | 16-03 | 2 | EXA-01, DEL-01 | integration (DI) | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~CompositionRootTests"` | ✅ existant — à réécrire | ⬜ pending |
| 16-04 T1 — Retrait des 6 champs + BudgetSource + purge du diagnostic | 16-04 | 3 | DEL-05 | unit + smoke | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~SettingsServiceTests"` puis suite complète | ✅ existant — à adapter (5 tests) | ⬜ pending |
| 16-04 T2 — Fixture réelle + preuve DEL-06 | 16-04 | 3 | DEL-06 | unit | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~SettingsServiceTests"` | ❌ Wave 0 — fixture `TestData/settings-legacy-plafonds.json` + 4 tests | ⬜ pending |
| 16-04 T3 — Garde de non-retour DEL-05 (réflexion Budget*) | 16-04 | 3 | DEL-05 | unit (réflexion) | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~ServicesLayerPurityTests"` | ✅ existant — 1 `[Fact]` à ajouter | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Gardes permanentes à revérifier à chaque vague

| Garde | Commande | Attendu |
|-------|----------|---------|
| Pureté de la couche Services | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~ServicesLayerPurityTests"` | vert |
| Composition DI | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~CompositionRootTests"` | vert (le calibrateur retiré, le magasin résolu) |
| Suite complète | `dotnet test Chronos.sln -v q --nologo` | 0 échec |
| Artefacts XAML fantômes | `dotnet clean Chronos.sln` avant la 1re build suivant la suppression de `BudgetDialog.xaml` | 3 copies de `obj/**/Views/BudgetDialog.g.cs` éliminées |

---

## Wave 0 Requirements

L'infrastructure xUnit existe déjà. Wave 0 se limite aux fixtures :

- [ ] Fixture `settings.json` réelle portant les 6 champs de plafonds obsolètes + les 18 préférences à
      préserver — preuve de DEL-06 sans migrateur (la recherche a établi empiriquement que
      `System.Text.Json` ignore déjà les membres non mappés). → **plan 16-04, tâche 2**
      (`tests/Chronos.Tests/TestData/settings-legacy-plafonds.json`).
- [x] Fixtures de transcripts JSONL pour le contrat de delta — **déjà présentes et suffisantes** :
      `sample-valid.jsonl` (1550 @ 11:30, 600 @ 07-05, 9999 @ 06-01), `sample-inactive.jsonl` (600 @ 06:00),
      `sample-tolerant.jsonl` (700 valides + prose « five_hour » + ligne corrompue + ligne tronquée),
      `SubagentsRoot/` (500 + 300). Aucune nouvelle fixture disque à créer ; les cas de bornage sont
      couverts en PUR par `TranscriptActivityLogTests` (plan 16-03, tâche 2).

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Disposition de la fenêtre de réglages après retrait du bouton « Plafonds… » | DEL-05 | La `UniformGrid` passe de 4 à 3 boutons — rendu visuel non assertable | Lancer l'app, ouvrir les réglages, vérifier qu'aucun trou disgracieux ni bouton fantôme ne subsiste |

---

## Validation Sign-Off

- [x] Toutes les tâches ont une commande automatisée ou une dépendance Wave 0 (11/11 tâches ci-dessus)
- [x] Continuité d'échantillonnage : chaque tâche des 4 plans porte sa propre commande automatisée —
      jamais 2 tâches consécutives sans vérification
- [x] Chaque test supprimé est justifié par la disparition du code qu'il couvrait :

| Test(s) supprimé(s) | Plan | Code disparu qui le justifie |
|---|---|---|
| `BudgetCalibrationTests.cs` (15) | 16-01 T2 | `BudgetCalibration.Deduce` / `ApplyAuto` supprimés |
| `BudgetAutoCalibratorTests.cs` (3) | 16-01 T2 | `BudgetAutoCalibrator` supprimé |
| `MainViewModelTests` — les 3 tests `CalibrateBudgets_*` | 16-01 T1 | commande `CalibrateBudgets` supprimée |
| `Fakes/FakeBudgetPrompt.cs` | 16-01 T1 | interface `IBudgetPrompt` supprimée |
| `JsonlEstimationProviderTests.Utilization_5h_estimee_avec_plafond` | 16-03 T2 | l'utilization dérivée d'un comptage de tokens n'existe plus (EXA-04) |
| `JsonlEstimationProviderTests.Plafond_defini_laisse_la_fenetre_5h_Estimated_avec_utilization` | 16-03 T2 | idem — la relation plafond → couleur n'existe plus |

**Bilan attendu : −23 tests supprimés, +≈ 36 créés/réécrits → cible ~405 ± 10, critère 0 échec.**

### Disparitions visuelles ATTENDUES (à ne pas signaler comme régressions)

Établies par l'analyse chiffrée §Q1 de `16-RESEARCH.md` sur l'état réel de la machine :

| Élément | Après la phase | Pourquoi ce n'est pas une régression |
|---|---|---|
| Couleur de l'anneau hebdo | gris (`Utilization = null`) | elle venait de `tokens / 5 817 635 413`, plafond Max x5 faux d'un facteur ~4 |
| Texte « ≈ N tokens » | disparu | plus aucun producteur de `SourceReliability.Estimated` |
| Compte à rebours hebdo | **conservé** | il vient de `WeeklyRecalibration` + `WeeklyAnchor`, pas du plafond |
| Anneau 5 h | **inchangé** | toujours alimenté par `usage.json` marqué `Exact` |
| Bandeau « données indisponibles » | **non déclenché** | il exige les DEUX fenêtres `Unavailable` |
