# Brief pour le conseil — Chronos v1.8 : trackeur d'utilisation du forfait

Date : 2026-09-26. Rédigé par ZEUS à partir de faits vérifiés sur la machine de l'utilisateur.

## La demande de l'utilisateur (verbatim résumé)

« On ajoute un trackeur d'utilisation qui nous permet de consulter notre utilisation de forfait Claude au
cours de la journée. Garder une trace de l'utilisation des tokens et pouvoir la visualiser sous forme de
courbe ou de graphique. Me proposer la meilleure forme qui correspond à mon idée. Cela me permettra de mieux
comprendre ma façon d'utiliser Claude au cours du temps. Il faudra que je puisse visualiser l'utilisation de
mon forfait sur des durées comme la semaine qui correspond à la réinitialisation de mon forfait (samedi à
minuit), et je pourrai voir en parallèle mon utilisation hebdomadaire superposée à celle des tranches de 5 h. »

## Ce qu'est Chronos aujourd'hui (3.2.1)

- Overlay WPF always-on-top (.NET 8, MVVM CommunityToolkit, DI, rendu XAML pur, aucune dépendance native).
- Cadran : deux arcs (extérieur 5 h, intérieur hebdo) ; longueur = temps avant reset, couleur = % consommé.
- Widget de sessions Claude Code (« Réflexion » / « En attente »).
- Fenêtres existantes : `MainWindow` (cadran), `SessionsWindow`, `SettingsWindow`, galeries de styles,
  `RecalibrationDialog`. Menu contextuel sur l'overlay. Tokens de design dans `Resources/DesignTokens.xaml`.
- 1200 tests, exe mono-fichier `Chronos-vX.Y.Z.exe`, `%APPDATA%\Chronos` pour tout l'état.
- Doctrine non négociable : « exact ou rien » — jamais un pourcentage inventé, jamais une estimation
  présentée comme exacte. Lecture seule stricte de `~/.claude` et `%APPDATA%\Claude`.

## Les sources de données disponibles (vérifiées le 2026-09-26)

### A. Relevés exacts du forfait — la sonde d'en-têtes (`RateLimitHeaderUsageProvider`)
- Une micro-requête toutes les **5 min** (≈ 288/jour) lit `anthropic-ratelimit-unified-5h-utilization`,
  `-5h-reset`, `-7d-utilization`, `-7d-reset`, `-overage-status`, `-5h/7d-status`.
- Résultat : `utilization` 0..1 par fenêtre + `resets_at` exact + statut serveur (allowed / rejected).
- État actuel : fonctionne (12 % 5 h, 33 % hebdo, relevé il y a 4 min). Pool partagé compte : inclut
  Claude Code, Cowork, l'app bureau, claude.ai.
- Aujourd'hui **rien n'est journalisé** : seul le dernier relevé est persisté (`last-exact.json`).
- Ces relevés ne sont disponibles que quand l'overlay tourne et que le jeton OAuth est valide : le journal
  aura des **trous** (PC éteint, jeton expiré : panne silencieuse déjà vécue pendant deux mois).
- Autres sources exactes, marginales : pont statusLine (terminal seulement), endpoint `/api/oauth/usage`.

### B. Tokens par message — transcripts JSONL (`~/.claude/projects/**/*.jsonl`)
- Chaque ligne `assistant` porte `timestamp`, `message.model`, `message.usage` :
  `input_tokens`, `output_tokens`, `cache_creation_input_tokens`, `cache_read_input_tokens`.
- Sous-agents : `.../<session>/subagents/agent-*.jsonl` (43 dossiers) — même schéma ; ils consomment le
  forfait, donc à compter.
- Volume : 1 555 fichiers, 2,2 Go, du 2026-06-23 à aujourd'hui (juin 38 fichiers, juillet 1, août 224,
  septembre 1 292). **Rétention non garantie** : Claude Code purge (cleanupPeriodDays) ; le trou de juillet
  en témoigne. Un historique durable doit donc être persisté par Chronos, pas recalculé à la demande.
- Périmètre partiel par construction : **Claude Code seulement** (terminal + app bureau mode Code). Cowork
  (VM) et claude.ai n'y sont pas. Donc « tokens » ≠ « % du forfait » ; les plafonds sont pondérés par
  modèle et non publiés (l'estimation absolue tokens/plafond a été SUPPRIMÉE en v1.5, ne pas la ressusciter).
- Parser existant : `TranscriptActivityProvider` somme déjà les 4 champs par ligne (source de delta) ;
  lecture incrémentale par offsets déjà maîtrisée dans le projet.

### C. Repères temporels connus
- Reset hebdo : samedi 00:00 heure locale (Paris) — confirmé par `resets_at` 7 j (2026-09-18T22:00Z) et
  par `WeeklyAnchor` des réglages (2026-07-11T00:00+02:00). L'ancre glisse rarement ; `resets_at` fait foi.
- Fenêtres 5 h : glissantes, démarrées au premier message après un repos ; leurs bornes exactes sont
  connues par `resets_at` 5 h à chaque relevé → la « grille » des tranches 5 h peut être reconstituée
  depuis le journal des relevés (le cadran aligne déjà des sous-tirets sur cette grille).

## La question posée au conseil

**Quelle est la meilleure forme, pour Chronos, d'un trackeur d'utilisation du forfait qui permette à
l'utilisateur de comprendre sa façon d'utiliser Claude au cours du temps ?** Trancher notamment :

1. **Quoi journaliser, à quelle granularité, sous quel format et avec quelle rétention** (relevés exacts
   toutes les 5 min ; tokens par message agrégés par tranche ; JSONL / CSV / SQLite ; compaction).
2. **Quelle(s) représentation(s)** : courbe % hebdo (montée en escalier puis chute au reset du samedi)
   avec les dents de scie 5 h superposées ; barres de tokens par heure ; heatmap jour × heure des rythmes ;
   spirale/horloge fidèle à l'identité Chronos ; autre. Quelles échelles de temps (jour / semaine de forfait /
   4 semaines) et quelle navigation.
3. **Où ça vit** : nouvelle fenêtre ouverte depuis le menu contextuel ? Panneau dépliable sous le cadran ?
   Onglet des réglages ? Contraintes : overlay compact, fenêtre transparente coûteuse en composition.
4. **Comment rester honnête** : trous du journal, séries de nature différente (% de compte vs tokens Code
   seulement), lecture des tokens sans jamais suggérer un % de forfait, sous-agents.
5. **Comment reconstituer le passé** : reconstruction initiale depuis 2,2 Go de transcripts sans bloquer
   l'UI ni brûler le CPU ; pas de reconstruction possible pour les % (le journal commence à la 3.3.0).
6. **Découpage en phases GSD** raisonnable pour un milestone v1.8 (exe 3.3.0), avec ce qui est différé.

Contraintes : XAML pur (pas de bibliothèque de graphiques tierce sauf argument très fort — aucune dépendance
native), MVVM strict, français, tokens de design, tests unitaires sur toute la logique (agrégation,
fenêtres, reconstruction), aucun droit admin, aucun appel réseau supplémentaire.
