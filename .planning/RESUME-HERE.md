# Point de reprise — 2026-09-27

**Milestone v1.7 « Lue ou non lue » clos le 2026-09-27** (exe 3.2.1, 1200 tests) avec un écart connu : le constat en
production est PARTIEL (trois exécutables en marche, tableau des gestes non joué) → repris en tête de la phase 32.
Archives : `.planning/milestones/v1.7-ROADMAP.md`, `.planning/milestones/v1.7-REQUIREMENTS.md` ; `MILESTONES.md` › Known Gaps.

**Milestone v1.8 « Historique d'utilisation » démarré** — cycle ZEUS n°1 (`.zeus/state.json`, phase GSD).
- Conseil LLM : `.zeus/reports/llm-council-2026-09-26.md` (approche arrêtée ; deux défauts découverts : parser de tokens ×2 à 2,75,
  `last-exact.json` figé depuis le 13/09).
- Plan de design VALIDÉ : `.zeus/DESIGN_PLAN.md` — maquettes Figma https://www.figma.com/design/O8WVDejfdPcJv6314a7h6k.
- Exigences : `.planning/REQUIREMENTS.md` (28, CPT/JRN/TOK/HIS/ACC/VAL). Roadmap : phases 32 → 35.
- Décisions de l'utilisateur : forme A retenue, B et C codées et sélectionnables ; vues Jour et 4 semaines gardées ; deux gestes
  d'ouverture ; **phase 32 publiée seule en 3.2.2** avant toute UI.

## Reprendre exactement ici

```
/gsd:progress
```
puis `/gsd:plan-phase 32` (ou `/gsd:autonomous`). La phase 32 se termine par un point de contrôle HUMAIN (VAL-04) : quitter
`Chronos-v3.1.0.exe` et `Chronos-v3.2.0.exe`, lancer `Chronos-v3.2.2.exe` par l'Explorateur, jouer le tableau des gestes de
`31-CONSTAT.md`.

## Sécurité — contrainte qui prime sur tout

Ne JAMAIS appeler l'endpoint de refresh OAuth avec le refresh token réel de `%APPDATA%\Chronos\oauth.dat` (rotation → login
invalidé). Ne JAMAIS écrire dans `%APPDATA%\Claude\` ni dans `~/.claude/projects` : lecture seule. L'agent ne lance, n'arrête ni
ne clique jamais l'overlay ; observer avec une sonde hors de l'arbre de l'app (WMI), jamais depuis une session Claude Code.
