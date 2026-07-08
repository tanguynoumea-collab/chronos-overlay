---
phase: 2
slug: d-couverte-des-sources-bloquante
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-08
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Aucun test de code — phase documentaire. Validation par checklist grep sur docs/data-sources.md |
| **Config file** | none |
| **Quick run command** | `grep -c "##" docs/data-sources.md` (sections présentes) |
| **Full suite command** | checklist grep complète (voir Per-Task Verification Map) |
| **Estimated runtime** | ~5 secondes |

---

## Sampling Rate

- **After every task commit:** vérifier que docs/data-sources.md contient les sections requises
- **After every plan wave:** checklist complète
- **Before `/gsd:verify-work`:** toutes les sections + échantillons présents
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 2-01-01 | 01 | 1 | DAT-01 | grep | `grep "rate_limits" docs/data-sources.md` | ❌ W0 | ⬜ pending |
| 2-01-02 | 01 | 1 | DAT-01 | grep | `grep "used_percentage" docs/data-sources.md` | ❌ W0 | ⬜ pending |
| 2-01-03 | 01 | 1 | DAT-01 | grep | `grep "resets_at" docs/data-sources.md && grep -i "epoch" docs/data-sources.md` | ❌ W0 | ⬜ pending |
| 2-01-04 | 01 | 1 | DAT-01 | grep | `grep -i "statusline" docs/data-sources.md` | ❌ W0 | ⬜ pending |
| 2-01-05 | 01 | 1 | DAT-01 | grep | `grep "jsonl" -i docs/data-sources.md && grep "input_tokens" docs/data-sources.md` | ❌ W0 | ⬜ pending |
| 2-01-06 | 01 | 1 | DAT-01 | grep | `grep -i "fragilit" docs/data-sources.md` (hypothèses/points de fragilité) | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `docs/` — dossier créé à la racine du projet

---

## Human Verification Items

| Criterion | How to verify | Maps to |
|-----------|--------------|---------|
| Aucun secret/ID de compte dans les échantillons | Relecture du document | Anonymisation |
