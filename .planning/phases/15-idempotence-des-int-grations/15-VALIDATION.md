---
phase: 15
slug: idempotence-des-int-grations
status: planned
nyquist_compliant: true
wave_0_complete: false
created: 2026-09-09
---

# Phase 15 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (net8.0-windows) — `tests/Chronos.Tests` |
| **Config file** | `tests/Chronos.Tests/Chronos.Tests.csproj` |
| **Quick run command** | `dotnet test Chronos.sln -v q --nologo --filter "FullyQualifiedName~Reconciler\|FullyQualifiedName~Installer"` |
| **Full suite command** | `dotnet test Chronos.sln -v q --nologo` |
| **Estimated runtime** | ~70 s (suite complète) · baseline mesurée : **328 tests / 0 échec** |

---

## Sampling Rate

- **After every task commit:** Run the quick command
- **After every plan wave:** Run the full suite
- **Before `/gsd:verify-work`:** Full suite must be green (328 + nouveaux tests)
- **Max feedback latency:** ~70 s

---

## Per-Task Verification Map

*Rempli par le planner le 2026-09-09 (3 plans, 3 vagues, 10 tâches dont 1 checkpoint humain).*

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 01-1 Socle `ClaudeSettingsJson` | 15-01 | 1 | PUR-01/02/03 | build | `dotnet build src/Chronos/Chronos.csproj --nologo -v q` | ❌ créé par la tâche | ⬜ pending |
| 01-2 Tests du socle | 15-01 | 1 | PUR-01/02/03 | unit (pur) | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --nologo -v q --filter "FullyQualifiedName~ClaudeSettingsJsonTests"` | ❌ créé par la tâche | ⬜ pending |
| 01-3 Fixture état réel pollué | 15-01 | 1 | PUR-03 | fixture | `python -c "import json;d=json.load(open('tests/Chronos.Tests/TestData/claude-settings-pollue.json',encoding='utf-8'));..."` (comptage 25 hooks / 7 événements) | ❌ créé par la tâche | ⬜ pending |
| 02-1 `SessionHookInstaller` retirer-puis-ajouter | 15-02 | 2 | PUR-01 | unit (pur) | `dotnet test ... --filter "FullyQualifiedName~SessionsTests"` | ✅ `tests/Chronos.Tests/SessionsTests.cs` | ⬜ pending |
| 02-2 `StatusLineInstaller` mutation ciblée | 15-02 | 2 | PUR-02 | unit (pur) | `dotnet test ... --filter "FullyQualifiedName~StatusLineInstallerTests"` | ✅ `tests/Chronos.Tests/StatusLineInstallerTests.cs` | ⬜ pending |
| 02-3 Tests étendus (non-cumul, padding, tiers) | 15-02 | 2 | PUR-01, PUR-02 | unit (pur) | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --nologo -v q` | ✅ les deux fichiers | ⬜ pending |
| 03-1 `ClaudeSettingsReconciler` | 15-03 | 3 | PUR-03 | build | `dotnet build src/Chronos/Chronos.csproj --nologo -v q` | ❌ créé par la tâche | ⬜ pending |
| 03-2 Tests du réconciliateur (fixture + E/S temp) | 15-03 | 3 | PUR-03 + critère 4 | unit + integration (dossier temp) | `dotnet test ... --filter "FullyQualifiedName~ClaudeSettingsReconcilerTests"` | ❌ créé par la tâche | ⬜ pending |
| 03-3 Câblage `App.xaml.cs` + garde DI | 15-03 | 3 | PUR-03 | integration (DI) | `dotnet test ... --filter "FullyQualifiedName~CompositionRootTests"` | ✅ `tests/Chronos.Tests/CompositionRootTests.cs` | ⬜ pending |
| 03-4 Checkpoint humain (vrai settings.json) | 15-03 | 3 | PUR-03 | manuel | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --nologo -v q` (filet) + comptage manuel | n/a | ⬜ pending |

**Gardes permanentes à revérifier à chaque vague :**

| Garde | Commande | Attendu |
|-------|----------|---------|
| Pureté de la couche Services | `dotnet test ... --filter "FullyQualifiedName~ServicesLayerPurityTests"` | vert (les 2 nouveaux types neutres ne fuient aucun WPF) |
| Composition DI | `dotnet test ... --filter "FullyQualifiedName~CompositionRootTests"` | vert (le réconciliateur se résout) |
| Baseline | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --nologo -v q` | ≥ 328 réussis, 0 échec |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

La Wave 0 est **absorbée par le plan 15-01 (vague 1)** : l'infrastructure xUnit existe déjà, il ne
reste qu'à poser le socle testable et les fixtures avant que quiconque touche aux installateurs.

- [ ] `src/Chronos/Services/ClaudeSettingsJson.cs` + `tests/Chronos.Tests/ClaudeSettingsJsonTests.cs`
      — plan 15-01, tâches 1 et 2 (prédicat d'identité, analyse tolérante, sérialisation fidèle)
- [ ] `tests/Chronos.Tests/TestData/claude-settings-pollue.json` — plan 15-01, tâche 3 : fixture figée
      reproduisant l'état réel (25 groupes Chronos sur 5 chemins d'exe + 3 groupes GSD avec leurs
      `matcher`/`timeout`/`if` + `agentPushNotifEnabled` + `statusLine` périmée avec `padding`)
- [ ] `tests/Chronos.Tests/ClaudeSettingsReconcilerTests.cs` — plan 15-03, tâche 2 (PUR-03 + critère 4)
- [ ] Garde anti-accident : aucun test ne peut atteindre le vrai `~/.claude/settings.json`.
      Imposée dans les trois plans — tout `new ClaudeSettingsReconciler(...)`, `new SessionHookInstaller(...)`
      et `new StatusLineInstaller(...)` reçoit ses chemins depuis `Path.GetTempPath()`, et le plan 15-03
      ajoute un test explicite `Aucun_test_ne_cible_le_vrai_settings_du_profil`.
      Contrôle : `grep -rn "SpecialFolder.UserProfile" tests/Chronos.Tests/` → aucune occurrence phase 15.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Purge effective du vrai fichier pollué | PUR-03 | Dépend de l'état réel de la machine de l'utilisateur (25 hooks constatés) ; non reproductible en CI | Après build, lancer l'exe une fois, puis compter les entrées Chronos dans `~/.claude/settings.json` : attendu 5, et les hooks GSD non-Chronos intacts |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies — 10/10 tâches portent une commande automatisée
- [x] Sampling continuity: no 3 consecutive tasks without automated verify — chaque tâche est suivie d'un `dotnet build` ou `dotnet test`
- [x] Chaque exigence de phase est couverte : PUR-01 (15-01, 15-02), PUR-02 (15-01, 15-02), PUR-03 (15-01, 15-03)
- [x] Le critère de succès 4 de la ROADMAP (rien d'autre n'est touché, aucun crash sur fichier malformé) est porté par 15-01 tâche 2, 15-02 tâches 1-3 et 15-03 tâche 2
- [ ] Vérification manuelle sur le vrai `~/.claude/settings.json` (checkpoint 15-03 tâche 4)
