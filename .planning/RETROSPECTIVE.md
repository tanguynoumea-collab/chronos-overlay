# Rétrospective — Chronos

*Document vivant, complété à chaque clôture de milestone. Les leçons alimentent la planification suivante.*

## Milestone : v1.6 — Observer au lieu de déduire (widget de sessions)

**Livré :** 2026-09-13 (exe 3.1.0) · **Clos :** 2026-09-25
**Phases :** 6 (21-26) | **Plans :** 19 | **Commits :** 110 | **Tests :** 752 → 889

### Ce qui a été construit
- Retrait de la source app-bureau par UI Automation (1 294 lignes) et recentrage du widget sur Claude Code.
- `ArbitrageSessions` : fusion des sources par fraîcheur, ordre total, désaccords tracés.
- Contrat d'événements refondé : `PermissionRequest`, matcher + veto sur `Notification`, battements de cœur,
  attente déduite du silence, `docs/hooks-contract.md` gardé par un test de non-dérive.
- « Traité » = transition observée sur la même source, persistant, geste explicite ; archivage permanent.
- Écriture d'état directe sans perte, balayage du magasin au démarrage.
- Diagnostic branché sur la même instance de moniteur que le widget.

### Ce qui a marché
- **L'enquête AVANT la roadmap.** Le rapport `.planning/debug/widget-sessions-statuts.md` (trois causes racines
  prouvées contre les classes réelles et contre la doc officielle) a rendu les 18 exigences évidentes et le
  découpage en 6 phases mécanique. Aucune phase n'a dû être réécrite.
- **Les gardes par mutation.** Chaque invariant important (retour de l'UIA, dérive document/code, « même
  source » de NET-01) a été prouvé falsifiable en cassant le code puis en révoquant : les tests verts ont
  cessé d'être des tests décoratifs.
- **Un seul chemin d'écriture, un seul producteur de mise en forme.** `EcritureEtatSession` et
  `AffichageSessions` ont supprimé les jumeaux qui dérivaient.
- **Audit d'intégration à la fin** : il a trouvé 8 régressions croisées, dont R3, avant la mise en production.

### Ce qui a été inefficace
- **Le milestone entier tient en 10 h 30 de mur**, mais la vérification in vivo n'a eu lieu que 10 jours plus
  tard (exe republié le 2026-09-23). Rien de v1.6 ne s'exécutait chez l'utilisateur pendant ce temps : la
  validation humaine devrait être planifiée comme une phase, pas comme un « à faire hors GSD ».
- **Le trou §9.1** (session sans fichier de hook qui disparaît à 15 min) était prévisible à la phase 25 et n'a
  été écrit qu'à l'audit.
- **La question produit n'a pas été posée.** Trois états « fiables » ont été livrés, mais l'utilisateur ne
  voulait qu'une chose : ne plus voir ce qu'il a déjà lu. Aucune phase ne traitait « lue / non lue ».

### Patterns établis
- Doctrine « observé, jamais déduit » : un état dont la source a expiré est **inconnu**, pas terminé.
- Une déduction porte un point d'interrogation dans son libellé (« à toi ? déduit »).
- Toute source externe non documentée : lecture tolérante, dégradation vers « indéterminé », jamais d'invention.
- Horloge injectée dans tout magasin à durée de vie ; aucune durée codée en dur dans une classe « permanente ».

### Leçons
1. Une enquête en lecture seule, chiffrée sur la vraie machine, vaut mieux que trois itérations de code.
2. La sémantique d'un contrat externe (hooks) dérive : un document + une garde croisée sont obligatoires.
3. Partir de la phrase de l'utilisateur (« quelle session m'attend ? ») et vérifier que le widget y répond
   AVANT de raffiner les états intermédiaires.
4. Une session de terminal et une session d'app bureau n'ont pas les mêmes signaux : ne jamais concevoir un
   acquittement qu'une des deux ne peut pas produire.

### Observations de coût
- Modèle : non mesuré (profil `quality`).
- Sessions Claude Code : 1 session principale du 2026-09-12 au 2026-09-13, 1 session d'audit, 1 de release.
- Notable : 48 commits `docs` pour 32 `feat` — la documentation de phase pèse autant que le code.

---

## Milestone : v1.7 — Lue ou non lue (widget de sessions)

**Livré :** 2026-09-26 (exe 3.2.1) — clos le 2026-09-27 avec un écart connu (constat en production partiel).
**Phases :** 6 (27, 28, 29, 30, 30.1 insérée, 31) | **Plans :** 20 | **Tests :** 896 → 1200 | **Commits :** 125 (2026-09-25 16:39 → 2026-09-26)

### Ce qui a été construit
- Le relevé in vivo AVANT la règle (phase 27) : deux gestes joués par l'utilisateur ont fixé la sémantique de `lastFocusedAt`.
- Deux mots à l'écran (« Réflexion », « En attente », « En attente ? »), la question `AskUserQuestion` classée attente, horizons uniques 20 min / 8 h (phase 28).
- La source app-bureau par fichiers, en lecture seule, avec la découverte de la virtualisation AppData de MSIX et la lecture des deux vues par candidats (phase 29).
- La règle « lue » : attente antérieure au dernier focus, ou session sélectionnée avec la fenêtre Claude au premier plan ⇒ masquée, cause écrite au diagnostic (phase 30).
- Un sous-agent qui écrit est un travail de sa session (phase insérée 30.1, née d'un écart du constat E2).
- Contrat de la source app-bureau sous garde, exe 3.2.0 puis 3.2.1 publiés et réconciliés (phase 31).

### Ce qui a marché
- **Le relevé avant la règle** : phase 27 a coûté un plan et a évité de coder une règle fausse (le retour alt-tab met bien `lastFocusedAt` à jour, le tour fini sous les yeux non).
- **La sonde hors de l'arbre de l'app** (WMI) : sans elle, la virtualisation MSIX serait restée invisible et l'overlay aurait continué à ne rien voir des hooks.
- **La phase insérée 30.1** déclenchée par un écart du constat : le constat a servi à quelque chose avant même d'être fini.

### Ce qui a été inefficace
- **Le constat en production n'a pas été mené au bout** : trois exécutables en marche simultanément (3.1.0, 3.2.0, 3.2.1) parce qu'aucun verrou mono-instance n'existe et que le geste « Quitter Chronos » n'a pas été fait ; les mesures du point (b) sont restées impossibles. Écart connu, reporté en tête de v1.8.
- **Une panne silencieuse de plus** découverte à la clôture : `last-exact.json` n'est plus écrit depuis le 2026-09-13 malgré des relevés exacts frais (Save dans un try/catch muet). Le projet a déjà payé deux fois ce motif (jeton expiré, usage.json figé).
- **Le parser de tokens compte 2 à 2,75 fois trop** (une ligne `assistant` par bloc de contenu, même `usage` recopié) — découvert par le conseil du 26/09, pas par les tests : aucune fixture réelle multi-blocs n'existait.

### Patterns établis
- Toute source de fichiers de l'app bureau se lit par candidats (cache du paquet MSIX puis vue réelle) et se constate hors de l'arbre de l'app.
- Un constat en production est une phase avec protocole écrit avant d'être joué ; « non observé » est une réponse valable ; un verdict PARTIEL est écrit, jamais tu.

### Leçons
- **Un verrou mono-instance** (ou au minimum une détection « une autre instance tourne ») est un prérequis de tout constat et de tout journal : à livrer en v1.8 phase 32.
- **Toute persistance doit exposer l'âge de sa dernière écriture** au diagnostic ; un try/catch muet autour d'un `Save` est une panne silencieuse en attente.
- **Les fixtures de transcripts doivent être réelles** (multi-blocs, sous-agents) : les fixtures synthétiques à une ligne par message ont caché un facteur 2.

### Observations de coût
- Modèles : profil `quality` (opus pour planners/executors). Sessions : 3 (25/09 soir, 26/09 journée, 26–27/09 nuit).
- Notable : le conseil LLM (10 agents, ≈ 1,2 M tokens) a trouvé en 20 min deux défauts que 1 200 tests verts n'avaient pas vus.

## Tendances inter-milestones

### Évolution du processus

| Milestone | Phases | Plans | Changement clé |
|-----------|--------|-------|----------------|
| v1.5 | 6 | 28 | Diagnostic sur la vraie machine avant la roadmap ; doctrine « exact ou rien » |
| v1.6 | 6 | 19 | Même méthode appliquée au widget ; audit d'intégration inter-phases avant release |

### Qualité cumulée

| Milestone | Tests | Dépendances ajoutées |
|-----------|-------|----------------------|
| v1.5 | 328 → 752 | 0 |
| v1.6 | 752 → 889 | 0 |

### Leçons confirmées sur plusieurs milestones
1. « Ne jamais présenter comme un fait ce qui n'a pas été observé » — validée sur le cadran (v1.5) puis sur le
   widget (v1.6).
2. L'enquête chiffrée précède la roadmap — deux milestones sans phase réécrite.
