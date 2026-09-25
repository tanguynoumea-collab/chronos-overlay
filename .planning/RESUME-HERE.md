# Point de reprise — 2026-09-25

**Milestone v1.6 « Observer au lieu de déduire » clos et archivé** (livré le 2026-09-13, exe 3.1.0, 889 tests).
Archives : `.planning/milestones/v1.6-ROADMAP.md`, `.planning/milestones/v1.6-REQUIREMENTS.md`,
`.planning/v1.6-MILESTONE-AUDIT.md`. Historique : `.planning/MILESTONES.md`, `.planning/RETROSPECTIVE.md`.

## Reprendre exactement ici

```
/gsd:new-milestone
```

Milestone **v1.7 — « Lue ou non lue »** : le widget ne montre que « Réflexion » / « En attente », une session
lue disparaît d'elle-même, le titre de session remplace le nom de dossier. Tout le relevé technique (source
app-bureau par fichiers, règle « lue », points à valider in vivo) est dans `.planning/STATE.md`, section
« Contexte technique v1.7 » — **ne pas re-enquêter**. Puis `/gsd:autonomous`.

## Sécurité — contrainte qui prime sur tout

Ne JAMAIS appeler l'endpoint de refresh OAuth avec le refresh token réel de `%APPDATA%\Chronos\oauth.dat`
(rotation → login invalidé). Ne JAMAIS écrire dans `%APPDATA%\Claude\` (données de l'app bureau) : lecture seule.
