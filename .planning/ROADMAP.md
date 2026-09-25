# Roadmap : Chronos

## Milestones

- ✅ **v1.0 — Overlay de quotas Claude complet** (7 phases, 18 plans, SHIPPED 2026-07-08) — [archive](.planning/milestones/v1.0-ROADMAP.md)
- ✅ **v1.1 — Estimation utile en mode app bureau** (2 phases, 5 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.1-ROADMAP.md)
- ✅ **v1.2 — Usage exact via l'endpoint OAuth** (2 phases, 4 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.2-ROADMAP.md)
- ✅ **v1.3 — Refonte du cadran (3 anneaux, remplissage, compacité)** (1 phase, phase 12, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.3-ROADMAP.md)
- ✅ **v1.4 — Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code)** (2 phases, phases 13-14, 5 plans, SHIPPED 2026-07-11) — [archive](.planning/milestones/v1.4-ROADMAP.md)
- ✅ **v1.5 — Exactitude permanente** (6 phases, phases 15-20, 28 plans, SHIPPED 2026-09-12) — [archive](.planning/milestones/v1.5-ROADMAP.md)
- ✅ **v1.6 — Observer au lieu de déduire (widget de sessions)** (6 phases, phases 21-26, 19 plans, SHIPPED 2026-09-13, exe 3.1.0) — [archive](.planning/milestones/v1.6-ROADMAP.md)

## Prochain milestone

**v1.7 — « Lue ou non lue »** (à définir par `/gsd:new-milestone`). Constat du 2026-09-25 : le widget v1.6
observe correctement « réfléchit » et « tour fini », mais rien ne lui dit si l'utilisateur a **lu** une session
terminée — toute session finie reste orange jusqu'au prochain prompt, à un clic droit ou à 8 h. L'app bureau
Claude écrit un fichier par session (`%APPDATA%\Claude\claude-code-sessions\…\local_*.json`) qui porte
`cliSessionId`, `title`, `lastFocusedAt` et une classification de fin de tour : c'est la source du « lue ».
Cible : deux libellés seulement (« Réflexion », « En attente »), disparition à la lecture, titres de session.

Différé au-delà : sous-fenêtres opus/sonnet/cowork, survol/tooltip, tray, taille réglable, préavis avant
saturation du quota et notification au reset. Piste d'économie à trancher : si `/api/oauth/usage` sert un jour
la famille `anthropic-ratelimit-unified-*`, les ≈ 288 micro-requêtes/jour de la sonde deviennent supprimables.
