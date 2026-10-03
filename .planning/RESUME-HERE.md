# Point de reprise — 2026-10-03

**Milestone v1.8 « Historique d'utilisation » — CLOS le 2026-10-03 avec écarts connus.**

- Phases 32 (7/8), 33 (5/5), 34 (8/8), 35 (6/7) ; exes publiés localement : 3.2.2, 3.3.0, 3.3.1, 3.4.0 (aucun tag de release, aucun
  push). Suite : 1707 tests verts, 0 warning (release 3.4.0).
- Archives : `.planning/milestones/v1.8-ROADMAP.md`, `.planning/milestones/v1.8-REQUIREMENTS.md` ; entrée et Known Gaps dans
  `.planning/MILESTONES.md` ; rétrospective dans `.planning/RETROSPECTIVE.md` ; tag git local `v1.8`.
- Écarts reportés dans la phase de constat du milestone suivant (phase 43 « Release Chronos-v3.5.0.exe + constat utilisateur ») :
  1. **32-08 — VAL-04** : constat en production (une seule instance, tableau L1…Q, V01…V12) — protocole `32-CONSTAT.md`.
  2. **35-07 — VAL-05** : constat de la fenêtre Historique — protocole et temps 0 dans `35-CONSTAT.md`.
- Étapes ZEUS du cycle n°1 (DESIGN-REVIEW de la galerie, DEV-COUNCIL, DEV-SENIOR) absorbées par le cycle n°2 (`.zeus/state.json`).
- Prochaine étape : définir le milestone suivant (`/gsd:new-milestone`, piloté par l'orchestrateur ZEUS, cycle n°2).

## Sécurité — contrainte qui prime sur tout

Ne JAMAIS appeler l'endpoint de refresh OAuth avec le refresh token réel de `%APPDATA%\Chronos\oauth.dat`. Ne JAMAIS écrire dans
`%APPDATA%\Claude\` ni dans `~/.claude/projects`. L'agent ne lance, n'arrête ni ne clique jamais l'overlay ; observer avec une
sonde hors de l'arbre de l'app (WMI) — depuis une session, `%APPDATA%` est la vue virtualisée MSIX.
