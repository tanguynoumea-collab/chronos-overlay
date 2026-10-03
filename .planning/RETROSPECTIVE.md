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

---

## Milestone : v1.8 — Historique d'utilisation

**Livré :** 2026-09-27 (exe 3.3.0 ; 3.2.2 intermédiaire ; puis 3.3.1 et 3.4.0 hors phases) — clos le 2026-10-03 avec écarts connus
(constats VAL-04 et VAL-05 non joués, reportés au milestone suivant).
**Phases :** 4 (32-35) | **Plans :** 26 / 28 | **Tests :** 1200 → 1648 (3.3.0) → 1707 (3.4.0) | **Commits :** 239 (2026-09-27 01:34 → 22:11)

### Ce qui a été construit
- Compter juste : dédup des `usage` par `message.id` (max par champ, dictionnaire global), magasins observables, vue AppData au
  diagnostic, verrou mono-instance ; journal des relevés exacts et sa lecture par plage pure ; exe 3.2.2 publié seul (phase 32).
- Agrégats de tokens 15 min UTC × modèle × sous-agent, reconstruction de fond (3,2 s à chaud) et curseurs, rendu DST juste (phase 33).
- Fenêtre Historique : Semaine en trois styles, Jour, pistes `OnRender`, honnêteté testée mot pour mot, galerie `--historique` (phase 34).
- Vue 4 semaines, deux gestes d'ouverture, section du diagnostic, docs sous garde de vocabulaire, exe 3.3.0 (phase 35).
- Hors phases : arrêt sans processus zombie (3.3.1), réglages refondus en fenêtre classique (3.4.0).

### Ce qui a marché
- **Compter juste AVANT de dessiner.** Le conseil LLM du 26/09 a placé la dédup et l'observabilité en tête ; aucune ligne
  d'historique n'a été écrite sur un compteur faux, et la 3.2.2 a commencé à journaliser des jours qui, sinon, étaient perdus.
- **La recherche de phase a requalifié un « bug »** : le gel de `last-exact.json` n'existait pas (vue virtualisée MSIX). La
  correction est devenue de l'observabilité au lieu d'un correctif sur une panne imaginaire.
- **Le plan de design validé comme contrat** (pas de phase UI-SPEC) : les hauteurs de pistes du plan ont été vérifiées par
  Measure/Arrange, et le vocabulaire d'honnêteté par une garde — le design ne s'est pas dilué à l'exécution.
- **Un rythme très élevé** : 4 phases, 26 plans, ≈ 500 tests en une journée de mur, sans régression (0 warning tenu).

### Ce qui a été inefficace
- **Deux constats avec l'utilisateur non joués, pour la deuxième fois de suite.** La dette VAL-03 de v1.7 est devenue VAL-04, puis
  a été absorbée par VAL-05, puis reportée encore. Les plans `autonomous: false` s'accumulent en fin de milestone pendant que
  l'exécution autonome continue (3.3.1, 3.4.0 sortis avant le constat de la 3.3.0).
- **Le cycle ZEUS n'a pas été mené au bout** : DESIGN-REVIEW de la galerie, DEV-COUNCIL et DEV-SENIOR absorbés par le cycle 2.
- **Trois styles codés pour en garder un** : Simplifié et Tuiles seront supprimés au milestone suivant (décision utilisateur) —
  le coût de « coder pour comparer » aurait pu se payer en maquettes seules.
- **Un interblocage d'arrêt** (Host/Dispatcher) découvert après la 3.3.0, qui laissait un processus zombie et le mutex tenu : le
  verrou mono-instance a rendu visible un défaut d'arrêt ancien.

### Patterns établis
- Toute persistance expose l'âge de sa dernière écriture et son erreur ; « journal muet depuis N min » au-delà de 15 min.
- Deux séries de nature différente : deux axes, deux palettes, deux vocabulaires, une garde structurelle contre le pourcentage dérivé.
- Un trou n'est jamais interpolé ; il porte sa cause journalisée (`demarrage`, `arret`, `jeton_invalide`, `sonde_refusee`, `reprise`).
- Toute géométrie de rendu en classes pures testées ; les pistes ne se redessinent jamais au tick.

### Leçons
1. **Un constat avec l'utilisateur doit être planifié AVANT la release suivante, pas après** : ne pas publier N+1 tant que le
   constat de N n'est pas joué, ou le déclarer explicitement comme non bloquant dès la roadmap.
2. **Ne jamais juger un fichier de l'overlay depuis une session Claude Code** : la vue AppData y est virtualisée.
3. **Comparer des variantes en maquette, pas en code**, sauf si l'utilisateur doit les vivre sur ses vraies données.
4. **Un verrou mono-instance exige un arrêt propre** : tester l'arrêt du Host réel sous Dispatcher dès qu'on introduit un mutex.

### Observations de coût
- Modèles : profil `quality` (opus pour planners, executors, vérificateurs). Sessions : 2 principales (27/09 nuit et journée) + clôture.
- Notable : 74 commits `test` pour 58 `feat` — la méthode RED/GREEN est systématique ; 97 commits `docs`.

## Tendances inter-milestones

### Évolution du processus

| Milestone | Phases | Plans | Changement clé |
|-----------|--------|-------|----------------|
| v1.5 | 6 | 28 | Diagnostic sur la vraie machine avant la roadmap ; doctrine « exact ou rien » |
| v1.6 | 6 | 19 | Même méthode appliquée au widget ; audit d'intégration inter-phases avant release |
| v1.7 | 6 | 20 | Relevé in vivo avant la règle ; constat en production comme phase (partiel) |
| v1.8 | 4 | 26 / 28 | Conseil LLM avant la roadmap ; plan de design validé comme contrat ; constats reportés |

### Qualité cumulée

| Milestone | Tests | Dépendances ajoutées |
|-----------|-------|----------------------|
| v1.5 | 328 → 752 | 0 |
| v1.6 | 752 → 889 | 0 |
| v1.7 | 896 → 1200 | 0 |
| v1.8 | 1200 → 1707 | 0 |

### Leçons confirmées sur plusieurs milestones
1. « Ne jamais présenter comme un fait ce qui n'a pas été observé » — validée sur le cadran (v1.5) puis sur le
   widget (v1.6).
2. L'enquête chiffrée précède la roadmap — deux milestones sans phase réécrite.
3. Le constat avec l'utilisateur est le maillon qui casse : partiel en v1.7, non joué en v1.8 — à traiter comme une porte de
   release, pas comme une fin de phase optionnelle.
