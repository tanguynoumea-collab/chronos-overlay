# Point de reprise — 2026-09-27 (soir)

**Milestone v1.8 « Historique d'utilisation »** — cycle ZEUS n°1 (`.zeus/state.json`).

- Phases 32, 33, 34 : exécutées et vérifiées (32 : 7/8 plans, le constat 32-08 est absorbé par 35-07).
- Phase 35 : 6/7 plans exécutés. `Chronos-v3.3.0.exe` publié à la racine (78 104 918 o, md5 `7ec9fbcf…`, commit `9b74ed5`, ni tag
  ni push). Suite : 1648 tests verts, 0 warning Debug et Release.
- Reste :
  1. **35-07 — constat humain** (VAL-05) : protocole et temps 0 dans `.planning/phases/35-4-semaines-acc-s-release-3-3-0/35-CONSTAT.md`.
     Reprend l'écart E1-ter (quitter 3.1.0, 3.2.0, 3.2.1 ET 3.2.2, lancer la 3.3.0 par l'Explorateur, second lancement refusé)
     et le tableau L1…Q / V01…V12 de 32-08. Continuation de 35-07 à partir de la tâche qui suit le checkpoint rendu.
  2. **Revue DAEDALUS** de la galerie (`dotnet run --project src/Chronos -- --historique`) → `.zeus/reports/design-review-1.md` ;
     écarts bloquants → itération DESIGN-REVIEW → GSD (max 3).
  3. ZEUS : DEV-COUNCIL (audit multi-rôles) → triage humain → DEV-SENIOR → go publication (checkpoint humain 3) → PUBLICATION
     (tag v1.8, release GitHub, CHANGELOG) → RETOUR ROADMAP (checkpoint 4) → `/gsd:complete-milestone`.

## Sécurité — contrainte qui prime sur tout

Ne JAMAIS appeler l'endpoint de refresh OAuth avec le refresh token réel de `%APPDATA%\Chronos\oauth.dat`. Ne JAMAIS écrire dans
`%APPDATA%\Claude\` ni dans `~/.claude/projects`. L'agent ne lance, n'arrête ni ne clique jamais l'overlay ; observer avec une
sonde hors de l'arbre de l'app (WMI) — depuis une session, `%APPDATA%` est la vue virtualisée MSIX.
