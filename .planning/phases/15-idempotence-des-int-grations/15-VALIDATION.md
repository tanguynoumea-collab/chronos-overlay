---
phase: 15
slug: idempotence-des-int-grations
status: draft
nyquist_compliant: false
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

*Rempli par le planner.*

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| — | — | — | — | — | — | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Chronos.Tests/ClaudeSettingsReconcilerTests.cs` — stubs pour PUR-01, PUR-02, PUR-03
- [ ] `tests/Chronos.Tests/TestData/claude-settings-pollue.json` — fixture figée reproduisant l'état réel
      (25 groupes Chronos sur 5 événements + groupes GSD non-Chronos avec leurs `matcher`/`timeout`
      + `agentPushNotifEnabled` + `statusLine` pointant un exe périmé)
- [ ] Garde anti-accident : aucun test ne doit pouvoir atteindre le vrai `~/.claude/settings.json`
      (injection systématique de `settingsPath`)

*L'infrastructure xUnit existe déjà — Wave 0 n'installe rien, elle pose les fixtures et les stubs.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Purge effective du vrai fichier pollué | PUR-03 | Dépend de l'état réel de la machine de l'utilisateur (25 hooks constatés) ; non reproductible en CI | Après build, lancer l'exe une fois, puis compter les entrées Chronos dans `~/.claude/settings.json` : attendu 5, et les hooks GSD non-Chronos intacts |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
