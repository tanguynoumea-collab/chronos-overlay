# Conseil LLM — Chronos v1.8 : trackeur d'utilisation du forfait

Date : 2026-09-26 · Brief : `llm-council-2026-09-26-brief.md` · 5 membres, 5 relecteurs à l'aveugle, synthèse du Président (ZEUS).

## Synthèse du Président — l'approche retenue

### Deux découvertes qui priment sur la forme

1. **Le parser de tokens existant compte 2 à 2,75 fois trop.** Claude Code écrit une ligne `assistant` par bloc de
   contenu d'un même message, chacune recopiant le même objet `usage` (vérifié par 5 relecteurs sur 5 : 3 248 lignes
   pour 1 183 `message.id` distincts sur le plus gros transcript, usage strictement identique entre doublons, 0 divergent).
   `TranscriptActivityProvider.SumUsageTokens` additionne ligne par ligne. Conséquence immédiate : la correction par
   delta de la doctrine v1.5 (`TokensDepuisReleve`) est **déjà fausse en production**. La déduplication par
   `message.id` (repli `requestId`) est le prérequis n° 1 du milestone et un correctif à publier, journal ou pas.
2. **La persistance du dernier relevé exact est en panne silencieuse depuis le 13/09 12:44.** `last-exact.json` n'a pas
   bougé depuis treize jours alors que le diagnostic affiche « EXACT, relevé il y a 4 min » et que le câblage DI appelle
   `Save` à chaque relevé exact, dans un `try/catch` qui avale tout. C'est exactement la plomberie que le journal
   généraliserait : **l'âge de la dernière écriture du journal devient un chiffre de première classe** (diagnostic +
   fenêtre), et la cause de la panne est à comprendre avant d'écrire le journal. Par ailleurs trois exécutables
   (3.1.0, 3.2.0, 3.2.1) tournent en même temps : le journal doit être idempotent face à plusieurs écrivains.

Autres prémisses du brief corrigées par le conseil : il n'existe **plus de menu contextuel** (clic droit = réglages) ;
la **lecture incrémentale par offsets n'existe pas** pour les transcripts (passe complète bornée à 8 jours, à
construire) ; `DayTimeline` suppose des resets toutes les 5 h (vrai seulement en enchaînement continu) ;
`WeeklyWindow` avance par 7 × 24 h depuis une ancre `+02:00` et dérivera d'une heure au changement d'heure du 25/10.

### 1. Ce qu'on journalise (deux journaux, jamais fusionnés)

| | Journal des relevés (magnitude) | Agrégats de tokens (attribution) |
|---|---|---|
| Source | Sonde d'en-têtes, 1 relevé exact / 5 min, compte entier | Transcripts Claude Code (principal + sous-agents) |
| Ligne | `{v, t, u5, r5, u7, r7, statut5, statut7, overage, source}` + événements `demarrage / arret / jeton_invalide / sonde_refusee` | `{v, slot (15 min, UTC), model, sub, in, out, cache_w, cache_r, n}` — quatre compteurs séparés, jamais la somme |
| Dédup | `CapturedAt` strictement croissant (l'orchestrateur ressert le même relevé 5 fois sur 6) ; clé idempotente multi-instances | `message.id` par fichier ; curseurs `{chemin → offset, taille, mtime}` |
| Fichiers | `%APPDATA%\Chronos\historique\releves-AAAA-MM.jsonl` | `historique\tokens-AAAA-MM.jsonl` + `curseurs.json` |
| Volume | ≈ 30–60 Ko/j, 10–23 Mo/an | ≈ 2 Mo/an |
| Rétention | Mensuel, 24 mois, aucune compaction en v1.8 | Illimitée |

Format **JSONL tolérant** (dialecte maison : ligne invalide ignorée, `v` inconnu sauté, append atomique). **Pas de
SQLite** : `e_sqlite3.dll` est une dépendance native, exclue par la contrainte du projet. Pas de CSV (schéma évolutif,
`null` ≠ 0). Buckets en **UTC** rendus en local (25 h le 25 octobre, 23 h le 29 mars).

### 2. Ce qu'on montre

**Fenêtre « Historique »** séparée, opaque (`AllowsTransparency=false`), non topmost, redimensionnable, taille
mémorisée. Ni panneau sous le cadran (fenêtre layered, coût de composition permanent), ni onglet des réglages (on y
configure, on n'y consulte pas). Point d'entrée : bouton dans les réglages ; second geste à arbitrer par DAEDALUS.

**Vue « Semaine de forfait » (défaut)** : X = samedi 00:00 → samedi 00:00 heure locale, borne prise dans `resets_at`
7 j. Pistes empilées partageant l'axe du temps, curseur vertical commun :

1. **Niveau** — % hebdo en escalier, coloré par la rampe du cadran (temps = géométrie, quota = luminance) ; en trait
   fin dessous, les dents de scie 5 h ; un tiret à chaque reset 5 h observé (grille reconstituée depuis le journal,
   pas une grille théorique). Les deux sont des fractions de leur propre plafond : la superposition demandée est
   légitime, à condition d'étiqueter. Courbe fantôme grise de la semaine N-1.
2. **Rythme** — barres par heure de la consommation exacte (Δ du compteur 5 h entre relevés, coupé aux resets), en
   « % de la fenêtre 5 h ». C'est la piste qui répond à « quand je brûle ». Sous réserve de la granularité réelle des
   en-têtes (0,01 constaté ; à confirmer en phase 32).
3. **Tokens Claude Code / heure** — barres gris neutre (jamais la rampe de quota), sous-agents hachurés, ventilation
   par modèle au survol. Axe propre. Libellé permanent : « tokens Claude Code seulement — hors Cowork et claude.ai ;
   bruts, non pondérés ; ce n'est pas un pourcentage du forfait ».
4. **Couverture** — bandeau toujours visible : relevé présent / Chronos arrêté / jeton invalide / sonde refusée.
   Le trackeur devient le chien de garde de la sonde.

**Vue « Jour »** : 24 h, mêmes pistes, grain 5 min, dents de scie 5 h lisibles. **Vue « 4 semaines »** : quatre
courbes hebdo superposées et fondues (courante pleine, précédentes dégradées). Navigation ‹ › par unité, « Aujourd'hui »,
réticule au survol (heure, valeur, source, âge du relevé).

**Écartés** : spirale/horloge (illisible pour comparer des semaines ; l'identité passe par les tokens de design),
double axe Y (invite à lire un rapport tokens/% qui n'existe pas), toute projection « épuisé vers … » (estimation).
**Différé v1.9** : heatmap jour × heure des rythmes, export CSV, compaction, dimension projet, ventilation statistique.

Rendu : un `FrameworkElement` par piste, `OnRender` + `StreamGeometry` gelée, réduction min/max par colonne de pixels
au-delà de 4 000 points, redessin sur changement de données ou de plage, jamais sur le tick 1 s. Toute la géométrie
(temps → x, valeur → y, bins, trous, tuiles) en classes pures de `Rendering/`, testées comme `ArcGeometry`.

### 3. Honnêteté (non négociable)

- Un trou (> 2 cadences) casse la ligne ; zone hachurée annotée par l'événement journalisé. Jamais d'interpolation.
- Δ% pendant un trou = exact mais **non localisé** → bloc plat « +X % pendant l'absence », jamais une barre au réveil.
- Deux séries, deux unités, deux axes, deux vocabulaires. Garde structurelle de test : aucun type de la couche
  historique n'expose un pourcentage dérivé de tokens.
- Les % commencent à la 3.3.0 : marqueur « journal ouvert le … », zone antérieure vide et dite telle. Les tokens
  antérieurs au plus vieux transcript = « hors couverture » ; juillet = « purgé par Claude Code », pas « zéro ».
- Statut `rejected` hachuré (refus serveur ≠ 100 % inventé). « Dernier relevé il y a N min » dans l'en-tête.

### 4. Reconstruire le passé

- % : impossible avant la 3.3.0, affiché comme tel. Ni `last-exact.json` ni `usage.json` ne servent de point.
- Tokens : `BackgroundService` sur thread `BelowNormal`, fichiers par mtime décroissant (semaine courante en 1–2 s),
  flux ligne à ligne (`FileShare.ReadWrite`, `SequentialScan`, 64 Ko), pré-filtre texte avant `JsonDocument`,
  dédup `message.id` par fichier, `Task.Yield` entre fichiers, curseurs persistés → reprise idempotente ; ensuite
  incrémental sur (taille, mtime) changés. Mesure : 2 Go ≈ 10–20 s CPU chaud, étalés. Progression dans la fenêtre.

### 5. Phases GSD — milestone v1.8, exe 3.3.0

| Phase | Contenu | Livraison |
|---|---|---|
| **32 — Compter juste, puis journaliser** | Dédup `message.id` dans le parser (test épinglant les lignes dupliquées) ; cause du gel de `last-exact.json` ; `JournalReleves` (dédup `CapturedAt`, événements, idempotence multi-instances) ; âge de dernière écriture au diagnostic. Aucune UI. | **3.2.2 immédiate** : chaque jour sans journal de % est perdu à jamais |
| **33 — Agrégats de tokens** | Curseurs, buckets 15 min UTC × modèle × sub, reconstruction de fond, tests DST / ligne tronquée / fichier réduit / reprise. | — |
| **34 — Fenêtre Historique : Semaine + Jour** | DAEDALUS (mode ÉVOLUTION) → pistes, trous, couverture, fantôme N-1, navigation, point d'entrée. | — |
| **35 — 4 semaines + finitions** | Vue 4 semaines, réticule, docs `data-sources.md`, release. | **3.3.0** |

## Notes du conseil

- **Accord unanime** (5/5 membres) : deux journaux de nature différente jamais fusionnés ; JSONL, pas SQLite ; fenêtre
  séparée opaque ; semaine de forfait samedi → samedi par défaut ; trous jamais interpolés ; pas de projection ; la
  phase « journal des relevés » se publie seule et d'abord.
- **Vrai désaccord** : superposer le % 5 h et le % hebdo sur un même axe Y (Rigoriste, Pragmatique : oui, fractions de
  leur propre plafond, c'est la demande) contre les remplacer par des dérivées ou des tuiles (Red-teamer, Premiers
  principes). **Tranché** : les deux — la superposition demandée reste la piste « Niveau », et la piste « Rythme » (Δ
  exact par heure) y est ajoutée, parce que l'information d'usage est dans les marches, pas dans le cumul.
- **Second désaccord** : rang des tokens (annotation pour le Red-teamer, série à part entière pour les autres).
  **Tranché** : série à part entière mais sur sa propre piste et son propre axe ; c'est la seule qui dit « quoi ».
- **Classement agrégé** (position moyenne sur 5 relecteurs) : A Rigoriste 1,0 · D Red-teamer 2,4 · C Pragmatique 2,6 ·
  E Premiers principes 4,0 · B Généraliste 5,0. Les cinq relecteurs ont vérifié sur la machine les deux découvertes
  décisives (doublons `message.id` ; `last-exact.json` figé) et les corrections de code du Pragmatique.
- **Outrepassé par le Président** : le grain 15 min vient du Généraliste (dernier classé) parce que les bornes des
  tranches 5 h tombent à une minute quelconque ; la courbe fantôme N-1 vient aussi de lui. Le Δ « rythme » est calculé
  sur le compteur 5 h (résolution plus fine) plutôt que sur l'hebdo comme le proposait Premiers principes.
- **Hypothèses à vérifier en phase 32, avec le journal lui-même** : granularité des en-têtes `utilization` ; Δ
  d'utilization = consommation (pas de recalcul rétroactif hors reset) ; le reset hebdo suit l'heure locale au
  changement d'heure du 25/10.

Table privée étiquette → membre : A = Rigoriste, B = Généraliste, C = Pragmatique, D = Red-teamer, E = Premiers principes.
