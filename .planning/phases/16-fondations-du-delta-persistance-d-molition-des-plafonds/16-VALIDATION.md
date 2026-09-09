---
phase: 16
slug: fondations-du-delta-persistance-d-molition-des-plafonds
status: draft
nyquist_compliant: false
wave_0_complete: false
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
| — | — | — | — | — | — | — | ⬜ pending |

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
      `System.Text.Json` ignore déjà les membres non mappés).
- [ ] Fixtures de transcripts JSONL pour le contrat de delta (activité / absence d'activité depuis T,
      ligne partielle, timestamp futur, prose contenant « five_hour »).

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Disposition de la fenêtre de réglages après retrait du bouton « Plafonds… » | DEL-05 | La `UniformGrid` passe de 4 à 3 boutons — rendu visuel non assertable | Lancer l'app, ouvrir les réglages, vérifier qu'aucun trou disgracieux ni bouton fantôme ne subsiste |

---

## Validation Sign-Off

- [ ] Toutes les tâches ont une commande automatisée ou une dépendance Wave 0
- [ ] Continuité d'échantillonnage : jamais 3 tâches consécutives sans vérification automatisée
- [ ] Chaque test supprimé est justifié par la disparition du code qu'il couvrait
