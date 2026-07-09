---
phase: 10
slug: lecture-du-token-client-endpoint
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-09
---

# Phase 10 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (tests/Chronos.Tests, 154 tests verts) + FakeHttpMessageHandler (à créer, Wave 0) |
| **Config file** | tests/Chronos.Tests/Chronos.Tests.csproj |
| **Quick run command** | `dotnet build Chronos.sln -c Debug` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug` |
| **Estimated runtime** | ~75 secondes |

## Sampling Rate

- **After every task commit:** `dotnet build Chronos.sln -c Debug`
- **After every plan wave:** `dotnet test Chronos.sln -c Debug`
- **Max feedback latency:** 90 seconds

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 10-xx | — | — | TOK-01 | unit | `dotnet test --filter TokenReader` (déchiffrement v10/DPAPI/GCM sur blob fixture connu → map → token claude_code) | ❌ W0 | ⬜ pending |
| 10-xx | — | — | TOK-02 | unit | `dotnet test --filter TokenReader` (fichier absent/clé absente/base64 invalide/GCM échoue → null, jamais d'exception) | ❌ W0 | ⬜ pending |
| 10-xx | — | — | TOK-03 | grep+unit | le lecteur n'a aucun chemin d'écriture (`grep -L "File.Write\|Log\|Console" ClaudeTokenReader.cs`) + test : rien persisté | ❌ W0 | ⬜ pending |
| 10-xx | — | — | API-01 | unit | `dotnet test --filter OAuthUsage` (réponse five_hour.utilization 65 → 0.65, resets_at ISO → ResetsAt, Exact) via FakeHttpMessageHandler | ❌ W0 | ⬜ pending |
| 10-xx | — | — | API-02 | unit | `dotnet test --filter OAuthUsage` (401/403/timeout/JSON malformé → Unavailable, aucune exception) | ❌ W0 | ⬜ pending |
| 10-xx | — | — | API-03 | unit | `dotnet test --filter OAuthUsage` (respecte CancellationToken, async, expiresAt<now → n'appelle pas) | ❌ W0 | ⬜ pending |
| 10-xx | — | — | pureté | unit | ServicesLayerPurityTests verte (provider neutre) | ✅ | ⬜ pending |

## Wave 0 Requirements

- [ ] `tests/Chronos.Tests/Fakes/FakeHttpMessageHandler.cs` (réponses HTTP scriptées)
- [ ] Fixtures : blob v10 chiffré déterministe (clé de test connue, PAS le vrai coffre) + réponses /usage (200 exact, 401, malformé)

## Human Verification Items

| Criterion | How to verify | Maps to |
|-----------|--------------|---------|
| Sur la vraie machine, le provider renvoie des % Exact plausibles | probe déjà validé E2E (65 %/92 %) ; re-confirmé en Phase 11 dans l'app | API-01 |
