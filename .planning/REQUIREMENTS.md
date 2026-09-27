# Requirements : Chronos — milestone v1.8 « Historique d'utilisation »

**Defined:** 2026-09-27
**Core Value:** Voir instantanément, sans terminal ni `/usage`, combien de quota et de temps il reste — et ne jamais présenter
une estimation comme un chiffre exact. v1.8 y ajoute : **comprendre sa façon d'utiliser Claude au cours du temps**, avec la même
honnêteté.

**Cadre :** conseil LLM du 2026-09-26 (`.zeus/reports/llm-council-2026-09-26.md`, 5 membres, 5 relecteurs à l'aveugle) ;
plan de design validé par l'utilisateur le 2026-09-27 (`.zeus/DESIGN_PLAN.md`, maquettes Figma
https://www.figma.com/design/O8WVDejfdPcJv6314a7h6k, frames A à F). Décisions de l'utilisateur : forme A retenue, B et C
codées aussi et sélectionnables ; vues Jour et 4 semaines conservées ; deux gestes d'ouverture ; **phase « compter juste +
journal » publiée seule en 3.2.2 avant toute interface**.

**Doctrine (inchangée) :** exact ou rien ; deux séries de nature différente (relevés exacts du compte / tokens Claude Code) ne se
fusionnent jamais et ne partagent ni axe ni palette ; un trou n'est jamais interpolé ; aucune projection ; XAML pur ; chemins sous
`%APPDATA%\Chronos` ; lecture seule stricte de `~/.claude` et `%APPDATA%\Claude` ; aucun appel réseau supplémentaire.

## v1 Requirements

### Compter juste (CPT) — prérequis de tout le milestone

- [x] **CPT-01**: Le parser de transcripts **déduplique par `message.id`** (repli `requestId`) avant toute somme de `usage` :
  une ligne `assistant` par bloc de contenu recopie le même `usage` (facteur 2 à 2,75 mesuré le 2026-09-26). Un test épingle
  une fixture RÉELLE multi-blocs (trois lignes identiques d'un même `msg_…`) et la correction par delta (`TokensDepuisReleve`)
  en hérite ; aucun nouveau lecteur ne partage l'ancien helper sans cette dédup.
- [x] **CPT-02**: La cause du **gel de `last-exact.json`** (non écrit depuis le 2026-09-13 12:44 malgré des relevés exacts frais,
  `Save` dans un `try/catch` muet) est établie, corrigée et couverte par un test ; **l'âge de la dernière écriture** de chaque
  magasin persistant (dernier exact, journal des relevés, agrégats de tokens) apparaît au diagnostic, et une écriture qui échoue
  n'est plus silencieuse (ligne d'événement + diagnostic).
- [x] **CPT-03**: **Une seule instance** : au démarrage, Chronos détecte une autre instance (mutex nommé) et refuse de tourner en
  double en le disant à l'utilisateur (message + diagnostic « N processus Chronos »), sans jamais tuer l'autre ; les hooks
  `--hook` et le mode CLI restent multi-instances par nature.

### Journal des relevés exacts (JRN)

- [x] **JRN-01**: Chaque relevé **exact distinct** (source `Exact`, `CapturedAt` strictement plus récent que le dernier écrit) est
  ajouté à `%APPDATA%\Chronos\historique\releves-AAAA-MM.jsonl` : `{v, t, u5, r5, u7, r7, statut5, statut7, overage, source}` ;
  un relevé rejoué par le cache (cadence 60 s contre sonde 300 s), un plancher ou une valeur de `LastExactStore` n'y entrent jamais.
- [x] **JRN-02**: Les **événements de couverture** sont journalisés dans le même fichier : `demarrage`, `arret` (propre),
  `jeton_invalide`, `sonde_refusee` (429 / statut `rejected`), `reprise` — pour qu'un trou porte sa cause au lieu d'être tu.
- [x] **JRN-03**: Écriture **append atomique et idempotente** (clé = `CapturedAt` + source ; deux processus n'écrivent pas deux
  fois le même relevé), lecture **tolérante** (ligne tronquée ou invalide ignorée, `v` inconnu sauté ligne par ligne), fichiers
  mensuels, rétention 24 mois, aucune compaction ; le tout en types neutres (pas de WPF sous `Services/`).
- [x] **JRN-04**: L'**âge de la dernière écriture du journal** est exposé au diagnostic et dans les réglages ; si Chronos tourne
  et qu'aucune écriture n'a eu lieu depuis plus de 15 min, une alerte visible le dit (pastille `Alerte` + texte).
- [x] **JRN-05**: Une **lecture par plage** (semaine de forfait, jour, 4 semaines) en classes pures et testées fournit : la série des
  relevés, les trous (> 2 cadences), les resets 5 h et hebdo observés, les Δ de consommation entre relevés consécutifs de même
  `resets_at` (jamais à travers un reset), et le saut « non localisé » de part et d'autre d'un trou.
- [x] **JRN-06**: **Release 3.2.2** publiée (exe mono-fichier, version embarquée aux quatre propriétés du csproj et dans le nom du
  fichier, réconciliation hooks/statusLine au premier lancement constatée dans `~/.claude/settings.json`) contenant CPT + JRN,
  **sans interface** ; `docs/data-sources.md` gagne une section « Journal d'historique » (schéma, dédup, événements, rétention,
  hypothèses à vérifier : granularité des en-têtes, Δ = consommation, reset hebdo à l'heure locale au changement d'heure).

### Agrégats de tokens (TOK)

- [ ] **TOK-01**: Les tokens des transcripts (principal ET `subagents/`) sont agrégés par **tranche de 15 min UTC × modèle ×
  principal/sous-agent** dans `historique\tokens-AAAA-MM.jsonl` : `{v, slot, model, sub, in, out, cache_w, cache_r, n}` — quatre
  compteurs séparés, jamais la somme, jamais le message individuel, jamais le contenu.
- [ ] **TOK-02**: La **reconstruction initiale** parcourt les transcripts existants en arrière-plan (thread `IsBackground`,
  priorité `BelowNormal`, fichiers par mtime décroissant, lecture en flux `FileShare.ReadWrite`, pré-filtre texte avant parsing
  JSON, dédup `message.id` par fichier, `Task.Yield` entre fichiers, annulable) ; la progression (N / M fichiers) est exposée au
  ViewModel ; l'UI ne bloque jamais ; la semaine courante est disponible avant l'historique.
- [x] **TOK-03**: La mise à jour est **incrémentale par curseurs** (`curseurs.json` : chemin → offset de la dernière ligne
  complète, taille, mtime) : seuls les fichiers dont (taille, mtime) ont changé sont relus depuis leur offset ; un fichier
  raccourci ou renommé est réingéré de zéro et ses tranches réécrites (pas ajoutées) ; la reprise après arrêt est idempotente.
- [ ] **TOK-04**: Les tranches UTC sont **rendues en heure locale** correctement aux changements d'heure (25 h le 25/10/2026,
  23 h le 28/03/2027) — testé ; les tranches antérieures au plus vieux transcript sont « hors couverture », un mois purgé par
  Claude Code est « transcripts absents », jamais « zéro token ».
- [ ] **TOK-05**: **Aucun pourcentage dérivé de tokens** : une garde structurelle de test interdit à tout type de la couche
  historique d'exposer un `double` de quota calculé à partir de tokens ; le périmètre partiel (Claude Code seul, hors Cowork et
  claude.ai) est écrit dans le schéma et les docs.

### Fenêtre Historique (HIS)

- [ ] **HIS-01**: Une fenêtre **`HistoriqueWindow`** séparée — `WindowStyle=None`, **`AllowsTransparency=False`**, `Topmost=False`,
  `ShowInTaskbar=True`, redimensionnable (min 760 × 480, défaut 920 × 610), coins 16, position et taille mémorisées dans
  `settings.json` — avec l'en-tête commun du plan de design : segment Jour / Semaine / 4 semaines, ‹ ›, « Cette semaine » /
  « Aujourd'hui », ligne de fraîcheur (« dernier relevé il y a N min · source · N relevés · N interruptions · journal ouvert le … »),
  Échap ferme.
- [ ] **HIS-02**: La **vue « Semaine de forfait »** (X = samedi 00:00 → samedi 00:00 heure locale, bornes issues de `resets_at`
  7 j du journal, repli `WeeklyAnchor`) en style **« Pistes »** (A) : piste NIVEAU (hebdo en escalier coloré par la rampe du
  thème, dents de scie 5 h en trait fin `TickReset`, tirets de reset 5 h observés, semaine précédente en fantôme gris pointillé),
  piste RYTHME (barres horaires du Δ 5 h, couleur = rampe au niveau atteint), piste TOKENS CLAUDE CODE (barres horaires
  `HistoTokens` + part sous-agents `HistoSousAgent`, axe propre), bande COUVERTURE ; grille et libellés des jours ; réticule
  vertical commun au survol avec infobulle (heure, valeur, source, âge).
- [ ] **HIS-03**: Les styles **« Simplifié »** (B : Niveau 200 px + Tokens 90 px + Couverture, sans Rythme) et **« Tuiles »**
  (C : hebdo seul en Niveau, piste FENÊTRES 5 H en tuiles bornées par les resets observés, hauteur = max % 5 h, grise si
  épuisée, puis Rythme, Tokens, Couverture) sont **sélectionnables** (réglage `HistoriqueStyleSemaine`, sélecteur dans la
  fenêtre et dans la carte des réglages), avec exactement les pistes et hauteurs du plan de design §2.2.
- [ ] **HIS-04**: La **vue « Jour »** : X = 0 h → 24 h locale, grain 5 min (un relevé = un point) ; % 5 h au premier plan (2,4 px,
  couleur = niveau, **gris à 100 %** avec libellé « épuisée à 100 % — le serveur refuse (statut rejected) »), % hebdo en trait
  fin, trait et libellé « reset 5 h HH:MM » à chaque reset observé ; RYTHME par heure ; TOKENS par **quart d'heure empilés par
  modèle** (`HistoModele1/2/3`, légende « opus · sonnet · haiku · sous-agents inclus ») ; COUVERTURE ; ligne « maintenant » si
  le jour est aujourd'hui ; ligne de fraîcheur « 288 relevés attendus · N présents · N interruption(s) (cause, HH:MM → HH:MM) ».
- [ ] **HIS-05**: La **vue « 4 semaines »** : quatre courbes hebdo superposées sur le même axe samedi → samedi, la courante en
  couleur, S-1 / S-2 / S-3 en gris aux opacités 0,8 / 0,45 / 0,25 avec étiquettes à droite (libellé + valeur finale, ou « pas de
  relevés (avant le journal) ») ; une semaine **épuisée** montre un plateau gris à 100 % annoté « épuisée <jour> HH:MM → bloquée
  jusqu'au reset » ; bande COUVERTURE PAR SEMAINE (4 rangées) ; les semaines antérieures au journal sont vides et dites telles.
- [ ] **HIS-06**: **Honnêteté testée** sur fixtures : un trou (> 2 cadences) interrompt la ligne et se dessine en rectangle `Line`
  35 % à bordure pointillée (grise « Chronos arrêté », ambre « jeton invalide ») ; le saut de part et d'autre d'un trou est un bloc
  plat gris annoté « +N % pendant l'absence (répartition inconnue) », jamais une barre au réveil ; une marche de % sans tokens Code
  est encadrée en pointillé `Accent` et le pied de page l'explique (« consommé ailleurs (Cowork, claude.ai) ») ; marqueur « journal
  ouvert le <date> » et zone antérieure vide ; libellé permanent des tokens (« par heure, comptés localement — hors Cowork et
  claude.ai · bruts, non pondérés · ce n'est PAS un % du forfait ») ; pied de page fixe « Aucun trou n'est interpolé … » ; aucune
  projection nulle part.
- [ ] **HIS-07**: **Rendu** : un `FrameworkElement` par piste avec `OnRender` et `StreamGeometry` gelée, réduction min/max par colonne
  de pixels au-delà de 4 000 points, redessin sur changement de données (5 min) ou de plage — jamais sur le tick 1 s ; toute la
  géométrie (temps → x, valeur → y, binning, trous, tuiles, segments par bande de rampe) en classes pures de `Rendering/`
  testées ; couleurs et tailles uniquement via `Resources/DesignTokens.xaml` (palette de `SettingsWindow` promue en tokens
  partagés sans changer les valeurs ; nouveaux tokens `HistoTokens`, `HistoSousAgent`, `HistoModele1/2/3`, `HistoGris`) ;
  `ServicesLayerPurityTests` reste vert.
- [ ] **HIS-08**: Tant que la reconstruction des tokens court, la fenêtre affiche le **bandeau F2** (« Reconstruction des tokens
  depuis vos transcripts Claude Code — N / M fichiers · la semaine courante est déjà complète », barre `Accent`, sous-texte sur
  le non-recalcul des pourcentages) ; il disparaît à la fin.

### Accès, diagnostic, livraison (ACC)

- [ ] **ACC-01**: La fenêtre de réglages gagne, dans la section DONNÉES, la carte **« Historique d'utilisation »** (F1) : bouton
  « Ouvrir », sous-texte « hebdo / 5 h / tokens · journal du <date> · dernière écriture il y a N min », sélecteur de style
  Pistes / Simplifié / Tuiles, mention du double-clic, et la carte d'état « Dernière écriture du journal » avec l'alerte > 15 min.
- [ ] **ACC-02**: Un **double-clic au centre du cadran** (`CentreHit`) ouvre ou ramène au premier plan la fenêtre Historique, sans
  déclencher deux fois la bascule % / temps du simple clic (temporisation ou annulation) ; le drag et le clic droit sont inchangés.
- [ ] **ACC-03**: Le diagnostic gagne une section **« Journal d'historique »** : chemin des fichiers, âge de la dernière écriture,
  relevés du jour, événements récents, état de la reconstruction des tokens (N / M), taille des fichiers, nombre d'instances.
- [ ] **ACC-04**: **Release 3.3.0** publiée (mêmes contrôles que JRN-06) avec HIS + ACC ; README et `docs/data-sources.md`
  décrivent la fenêtre, les trois styles, les vues et les règles d'honnêteté avec les mots du plan de design §4.

### Constat en production (VAL)

- [ ] **VAL-04**: Le **constat reporté de v1.7** est joué sur la 3.2.2, avec l'utilisateur, protocole de `31-CONSTAT.md` inchangé :
  point (a) une seule instance (3.1.0 et 3.2.0 quittées, 3.2.2 lancée par l'Explorateur, CPT-03 constaté), réconciliation dans
  le fichier, tableau des gestes (L1, L2, L2b, L3, L4, Q) et les 12 vérifications déférées — chacun avec un verdict écrit
  (`32-CONSTAT.md`), écarts compris ; plus : le journal s'écrit (âge < 6 min après 10 min d'overlay).
- [ ] **VAL-05**: Le **constat de la 3.3.0** : la fenêtre s'ouvre par les deux gestes ; la semaine courante affiche les relevés
  depuis l'ouverture du journal et les tokens reconstruits ; les trois styles se sélectionnent ; un trou réel (PC éteint la nuit)
  est hachuré et annoté ; l'utilisateur relit les libellés d'honnêteté et rend un verdict écrit (`35-CONSTAT.md`).

## v2 Requirements

Reportés, tracés pour la roadmap (RETOUR ROADMAP du cycle ZEUS) :

- **HIS-09**: Heatmap jour × heure des rythmes (tokens et Δ %) sur 4 semaines glissantes.
- **HIS-10**: Export CSV de la plage affichée.
- **JRN-07**: Compaction du journal des relevés au-delà de 8 semaines (un relevé par heure).
- **TOK-06**: Dimension projet (dossier) dans les agrégats de tokens, avec bascule modèle / projet.
- **HIS-11**: Projection conditionnelle « au rythme des 3 dernières heures » — uniquement pointillée, distincte de tout relevé.
- **CAD-XX**: `DayTimeline` sur les resets 5 h observés plutôt qu'une grille théorique de 5 h ; dérive de `WeeklyWindow` d'une
  heure au changement d'heure (test d'acceptation le 25/10/2026 avec le journal).

## Out of Scope

| Feature | Reason |
|---------|--------|
| SQLite (`Microsoft.Data.Sqlite`, `System.Data.SQLite`) | embarque `e_sqlite3.dll` : dépendance native interdite, casse le mono-fichier |
| Double axe Y (% et tokens sur le même repère) | invite à lire un rapport tokens/% qui n'existe pas ; deux natures, deux axes |
| Spirale / horloge comme vue d'historique | illisible pour comparer des valeurs et des semaines ; l'identité passe par les tokens de design |
| Toute projection « épuisé vers … » ou « à ce rythme » en v1.8 | une projection est une estimation présentée en chiffre (doctrine v1.5) |
| Reconstitution des pourcentages avant l'ouverture du journal | impossible sans relevés ; `last-exact.json` et `usage.json` ne sont pas des points de courbe |
| Tokens Cowork / claude.ai | aucune source locale ; le périmètre partiel est écrit, jamais comblé |
| Sparkline ou historique sur le cadran lui-même | fenêtre layered, coût de composition permanent ; le cadran reste un mode coup d'œil |
| Bibliothèque de graphiques tierce | XAML pur suffit pour des escaliers, des barres et des tuiles ; aucune dépendance |

## Traceability

Rempli par le roadmapper le 2026-09-27 (phases 32 à 35, numérotation continue après la 31 de v1.7) — voir `.planning/ROADMAP.md`.

| Requirement | Phase | Status |
|-------------|-------|--------|
| CPT-01 | Phase 32 | Complete |
| CPT-02 | Phase 32 | Complete |
| CPT-03 | Phase 32 | Complete |
| JRN-01 | Phase 32 | Complete |
| JRN-02 | Phase 32 | Complete |
| JRN-03 | Phase 32 | Complete |
| JRN-04 | Phase 32 | Complete |
| JRN-05 | Phase 32 | Complete |
| JRN-06 | Phase 32 | Complete |
| TOK-01 | Phase 33 | Pending |
| TOK-02 | Phase 33 | Pending |
| TOK-03 | Phase 33 | Complete |
| TOK-04 | Phase 33 | Pending |
| TOK-05 | Phase 33 | Pending |
| HIS-01 | Phase 34 | Pending |
| HIS-02 | Phase 34 | Pending |
| HIS-03 | Phase 34 | Pending |
| HIS-04 | Phase 34 | Pending |
| HIS-05 | Phase 35 | Pending |
| HIS-06 | Phase 34 | Pending |
| HIS-07 | Phase 34 | Pending |
| HIS-08 | Phase 34 | Pending |
| ACC-01 | Phase 35 | Pending |
| ACC-02 | Phase 35 | Pending |
| ACC-03 | Phase 35 | Pending |
| ACC-04 | Phase 35 | Pending |
| VAL-04 | Phase 32 | Pending |
| VAL-05 | Phase 35 | Pending |
