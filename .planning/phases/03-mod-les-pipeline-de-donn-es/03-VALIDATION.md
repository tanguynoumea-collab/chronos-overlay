---
phase: 3
slug: mod-les-pipeline-de-donn-es
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-08
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (tests/Chronos.Tests, existant et vert) |
| **Config file** | tests/Chronos.Tests/Chronos.Tests.csproj |
| **Quick run command** | `dotnet build Chronos.sln -c Debug` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug` |
| **Estimated runtime** | ~45 secondes |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build Chronos.sln -c Debug`
- **After every plan wave:** Run `dotnet test Chronos.sln -c Debug`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 3-xx | — | — | DAT-03 | unit | `dotnet test --filter UsageSnapshot` (immutabilité, Exhausted, mapping) | ❌ W0 | ⬜ pending |
| 3-xx | — | — | DAT-02 | grep | couche Services sans WPF : `grep -L "System.Windows" src/Chronos/Services/*.cs` + compile | ❌ W0 | ⬜ pending |
| 3-xx | — | — | DAT-04 | unit | `dotnet test --filter ClaudeUsageObjectProvider` (fichier pont valide/absent/corrompu/stale) | ❌ W0 | ⬜ pending |
| 3-xx | — | — | DAT-05 | unit | `dotnet test --filter JsonlEstimationProvider` (somme tokens, Estimated, utilization null) | ❌ W0 | ⬜ pending |
| 3-xx | — | — | DAT-06 | unit | `dotnet test --filter CompositeUsageProvider` (bascule par fenêtre) | ❌ W0 | ⬜ pending |
| 3-xx | — | — | DAT-07 | unit | `dotnet test --filter FractionTimeRemaining` (clamp 0..1, null, hebdo dérivant) | ❌ W0 | ⬜ pending |
| 3-xx | — | — | ROB-02 | unit | `dotnet test --filter Tolerant` (ligne corrompue, partielle, champ manquant) | ❌ W0 | ⬜ pending |
| 3-xx | — | — | pont | node | `node scripts/chronos-statusline-bridge.js < fixture.json` écrit usage.json atomiquement | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Fixtures de test : JSON statusLine réaliste (avec/sans rate_limits), JSONL avec lignes corrompues/partielles
- [ ] Tests stubs par requirement dans tests/Chronos.Tests

---

## Human Verification Items

| Criterion | How to verify | Maps to |
|-----------|--------------|---------|
| Le pont n'a pas cassé la statusline existante | Ouvrir une session Claude Code, vérifier l'affichage statusline | Pont |
| usage.json se remplit pendant une session réelle | Consulter %APPDATA%\Chronos\usage.json après un échange | DAT-04 |
