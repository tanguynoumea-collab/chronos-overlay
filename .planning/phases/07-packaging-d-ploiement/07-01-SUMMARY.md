---
phase: 07-packaging-d-ploiement
plan: 01
subsystem: infra
tags: [dotnet-publish, single-file, self-contained, wpf, win-x64, packaging, autostart]

# Dependency graph
requires:
  - phase: 01-fondations-architecture-squelette-overlay
    provides: "PropertyGroup publish conditionné (6 props) dans Chronos.csproj + app.manifest PerMonitorV2"
  - phase: 06-comportements-overlay-placement-interaction
    provides: "AutostartService ciblant Environment.ProcessPath (single-file-safe)"
provides:
  - "Config publish complète (8 propriétés verrouillées) conditionnée au publish uniquement"
  - "Profil de publication reproductible win-x64.pubxml"
  - "Artefact prouvé : un unique Chronos.exe self-contained mono-fichier (~74 Mo) qui se lance sans crash"
  - "docs/publish.md : commande de publication exacte + rôle de chaque propriété + limite autostart"
affects: [distribution, release, milestone-v1.0]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Propriétés publish gatées sur Condition=\"'$(PublishSingleFile)' == 'true'\" → build/debug restent normaux"
    - "Profil .pubxml miroir des flags CLI verrouillés pour publication reproductible"
key-files:
  created:
    - src/Chronos/Properties/PublishProfiles/win-x64.pubxml
    - docs/publish.md
  modified:
    - src/Chronos/Chronos.csproj

key-decisions:
  - "PublishReadyToRun=true et InvariantGlobalization=false ajoutés DANS le PropertyGroup conditionné (jamais au build normal)"
  - "Commande CLI explicite reste canonique ; le profil win-x64.pubxml est un raccourci équivalent"
  - "PublishTrimmed=false non négociable (WPF non trim-safe) — compression via EnableCompressionInSingleFile"

patterns-established:
  - "Packaging: 8 props publish verrouillées conditionnées, mirrorées en .pubxml, jamais inconditionnelles"
  - "Validation packaging: smoke réel de l'exe PUBLIÉ (vivant 8 s, kill propre) + non-régression 106/106"

requirements-completed: [DEP-01]

# Metrics
duration: 3min
completed: 2026-07-08
---

# Phase 7 Plan 1: Packaging + déploiement Summary

**Chronos empaqueté en un unique Chronos.exe self-contained mono-fichier win-x64 (~74 Mo compressé), publication conditionnée au publish et prouvée par un smoke réel de l'exe publié, avec la commande exacte et la limite autostart documentées.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-07-08T20:32:02Z
- **Completed:** 2026-07-08T20:34:46Z
- **Tasks:** 3
- **Files modified:** 3 (2 créés, 1 modifié)

## Accomplishments

- Chronos.csproj finalisé : les 8 propriétés publish verrouillées (dont PublishReadyToRun et InvariantGlobalization ajoutées) sont toutes conditionnées au publish — `dotnet build -c Release` reste un build normal (pas de sous-dossier win-x64 self-contained).
- Profil de publication reproductible `win-x64.pubxml` créé (miroir des flags verrouillés).
- Publication réelle réussie : `publish/` ne contient que `Chronos.exe` (74 Mo, < 120 Mo) + `.pdb`, zéro DLL managée ou native à côté (natives embarquées).
- Smoke de l'exe PUBLIÉ : process vivant après 8 s (extraction native OK au 1er run), kill propre, aucun crash.
- Non-régression confirmée : 106/106 tests verts.
- `docs/publish.md` (112 lignes) : commande exacte + alternative par profil, tableau du POURQUOI de chaque propriété, distribution mono-fichier, et limite autostart (re-toggle après déplacement de l'exe).

## Task Commits

1. **Task 1: Finaliser Chronos.csproj + créer win-x64.pubxml** - `8ff1c22` (chore)
2. **Task 2: Publier + vérifier mono-fichier + smoke exe publié + non-régression** - (aucun commit source — tâche de vérification produisant l'artefact publié gitignoré ; build/test/smoke exécutés et validés)
3. **Task 3: Documenter la publication (docs/publish.md) + note autostart** - `117c437` (docs)

**Plan metadata:** (commit docs de clôture)

## Files Created/Modified

- `src/Chronos/Chronos.csproj` - Ajout de `<PublishReadyToRun>true</PublishReadyToRun>` et `<InvariantGlobalization>false</InvariantGlobalization>` dans le PropertyGroup conditionné (8 props publish au total).
- `src/Chronos/Properties/PublishProfiles/win-x64.pubxml` - Profil de publication reproductible, miroir des propriétés verrouillées.
- `docs/publish.md` - Documentation de publication : prérequis, commande canonique, tableau des propriétés + rationale, distribution, limite autostart, vérifications post-publication.

## Decisions Made

- Les deux propriétés manquantes ajoutées strictement à l'intérieur du PropertyGroup conditionné pour ne pas alourdir build/debug (piège documenté dans STACK.md « What NOT to Use »).
- La commande CLI explicite (`-p:PublishSingleFile=true --self-contained true`) reste la référence canonique ; le profil .pubxml est présenté comme raccourci équivalent, pas comme source de vérité.
- PublishTrimmed conservé à `false` (WPF non trim-safe) — non négociable.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- DEP-01 satisfait : artefact self-contained mono-fichier produit et prouvé (lancement réel, non-régression verte).
- Reste à valider par UAT humain (hors périmètre autonome, cf. 07-VALIDATION.md) : cadran visible au lancement de l'exe publié, machine réellement propre sans .NET, autostart après reboot Windows.
- Phase 07 étant l'unique et dernière phase du milestone v1.0, prêt pour la clôture du milestone après UAT.

---
*Phase: 07-packaging-d-ploiement*
*Completed: 2026-07-08*

## Self-Check: PASSED

- Files verified on disk: src/Chronos/Properties/PublishProfiles/win-x64.pubxml, docs/publish.md, 07-01-SUMMARY.md
- Commits verified: 8ff1c22 (Task 1), 117c437 (Task 3)
- csproj contains PublishReadyToRun (conditioned PropertyGroup)
