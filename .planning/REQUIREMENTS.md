# Requirements : Chronos — milestone v1.9 « Lisible partout »

**Defined:** 2026-10-03
**Core Value:** Voir instantanément, sans terminal ni `/usage`, combien de quota et de temps il reste — et ne jamais présenter
une estimation comme un chiffre exact. v1.9 : **chaque cadran lisible et saisissable, dans toutes ses formes, sur une chaîne
de données réduite à ce qui marche**.

**Cadre :** roadmap utilisateur `.zeus/ROADMAP-PROCHAINE-VERSION.md` (R1-R7, 2026-10-01) ; inventaires lecture seule
`.zeus/reports/cycle2/` ; conseil LLM du 2026-10-03 (`.zeus/reports/llm-council-2026-10-03.md`, 5 membres, 5 relecteurs) ;
**plan de design validé par l'utilisateur le 2026-10-03** (`.zeus/DESIGN_PLAN_CYCLE2.md`, maquettes
`.zeus/maquettes/cycle2-cadrans-themes.html`) — contractuel, pas de phase UI-SPEC.
Décisions de l'utilisateur (2026-10-03) : barre statusLine **retirée** ; « Pâle » = sombres doux (pas de fonds clairs) ;
+20 % mesuré sur le **rendu actuel** ; Braises = repère de reset + séparations horaires + heure du reset ; liste de purge
**validée par l'utilisateur avant toute suppression**.

**Doctrine (inchangée) :** exact ou rien ; XAML pur, aucune dépendance native ; chemins sous `%APPDATA%\Chronos` ; lecture
seule de `~/.claude` hors réconciliation contrôlée de `~/.claude/settings.json` ; aucune valeur de couleur/taille hors
`DesignTokens.xaml` ou des pinceaux du thème ; UI et commentaires en français.

## v1 Requirements

### Socle (SOC) — prérequis

- [x] **SOC-01**: Un `settings.json` contenant une valeur d'énumération inconnue (style, orientation, section…) ou un membre
  supprimé ne remet plus **tous** les réglages à zéro : seule la valeur fautive retombe sur son défaut, le reste (thème, coin,
  géométries…) est conservé et réécrit intact ; épinglé par tests (`"Tuiles"`, style de cadran inconnu, clé de thème inconnue).
- [x] **SOC-02**: Tout argument `--xxx` inconnu ou retiré fait **sortir l'exe silencieusement (code 0) avant le verrou
  mono-instance** — jamais l'overlay, jamais la boîte « Chronos tourne déjà » ; les hooks `--hook` et le mode CLI existants
  sont inchangés.

### Données : une chaîne claire (DAT) — R5

- [x] **DAT-01**: La **liste de purge** (classes, réglages, cartes, sections de diagnostic, tests, docs) est présentée à
  l'utilisateur et **validée avant le premier commit de suppression** (point de contrôle humain). — Validée par l'utilisateur le 2026-10-03 (`.zeus/reports/cycle2/liste-purge.md`).
- [x] **DAT-02**: Les sources mortes disparaissent : jeton de l'app bureau (`ClaudeOAuthUsageProvider`, `GatedOAuthUsageProvider`,
  `ClaudeTokenReader`, `WindowsCredentialStore`, `InventaireMachine`), `ClaudeUsageObjectProvider` / `usage.json` et sa
  surveillance, inférence 5 h, `WeeklyWindow`, recalibrage hebdo (carte « Recalibrer… » comprise), réglages orphelins ; la
  chaîne devient `LastExact( Journal( Composite(sonde d'en-têtes, OAuth Chronos) ) )`, gardée par test (un seul composite).
- [x] **DAT-03**: La **barre statusLine est retirée** : au premier lancement de la 3.5, Chronos sauvegarde puis retire sa
  `statusLine` de `~/.claude/settings.json` (idempotent, journalisé, testé sur fichiers témoins) ; le mode `--statusline`, le
  pont, l'installeur et la carte des réglages disparaissent ; les hooks restent réconciliés.
- [x] **DAT-04**: Le diagnostic décrit **exactement** la chaîne réelle (section « Chaîne de données » : sonde d'en-têtes,
  secours OAuth Chronos, dernier exact persisté, journal) ; les sections « pont statusLine », « endpoint OAuth (repli) » et le
  « Conseil » trompeur disparaissent ; un rapport ne coûte plus la recherche des coffres (≈ 17 s).
- [x] **DAT-05**: Une **méthodologie unique** est écrite : `docs/data-sources.md` réécrit (source → cadran, ordre de priorité,
  repli, ce qui est exact et ce qui ne l'est pas : seul le plancher « ≥ » n'est pas exact), README « D'où viennent les
  chiffres » et `CLAUDE.md` alignés ; les commentaires « repli JSONL / estimation » périmés sont corrigés.

### Historique (HIS) — R4

- [x] **HIS-09**: L'Historique n'a plus qu'**un style, Pistes** : sélecteurs (fenêtre et carte des réglages), grilles
  Simplifié/Tuiles, piste Fenêtres 5 h, tokens et textes associés retirés ; la propriété `HistoriqueStyleSemaine` est
  supprimée et un ancien réglage se lit sans perte (SOC-01).
- [x] **HIS-10**: Un **plein écran général** (bouton « Plein écran » dans l'en-tête, F11) couvre l'écran courant ; les pistes
  des trois vues se partagent la hauteur dans leurs proportions (plafonds Niveau 520, Rythme/Tokens 240), les textes et traits
  passent au dictionnaire `PleinEcran` (≈ ×1,35), la vue ne défile plus ; les règles d'honnêteté sont identiques à toutes les
  tailles.
- [x] **HIS-11**: On **sort** du plein écran par un bouton toujours visible, par F11, ou par Échap — Échap quitte d'abord le
  plein écran, puis ferme la fenêtre au second appui ; la position et la taille d'avant sont restaurées.

### Thèmes (THM) — R6

- [x] **THM-01**: La section Thème des réglages range les thèmes en trois groupes titrés **Pâle · Classique · Vive** (§5.1 du
  plan) ; chaque thème porte sa catégorie.
- [x] **THM-02**: **Six nouvelles palettes** (Sauge, Lavande, Graphite, Marine, Synthwave, Lave) aux valeurs du plan §5.2 ;
  Néon et Aurore ont une rampe corrigée qui finit sur un vrai rouge (décors inchangés).
- [x] **THM-03**: Pour **chaque** thème, le gris « épuisé » atteint un contraste ≥ 3:1 contre le disque et le rouge de fin de
  rampe est dans la bande 335°–20° avec un contraste ≥ 3:1 — vérifié par test sur tout le catalogue.
- [x] **THM-04**: Braises, Fusible, Marée et Volets **suivent le thème** (textes, tuiles, fonds) et `TickReset` entre dans les
  pinceaux du thème ; plus aucune couleur fixe dans les quatre vues.

### Cadrans : taille et orientation (CAD) — R1, R2

- [ ] **CAD-01**: Les cadrans sont rendus **à l'échelle 1** (plus de Viewbox) ; la fenêtre prend l'empreinte du cadran choisi
  (plan §1.1 : Fusible 190 × 92, Volets 190 × 66, Arcs/Braises 170 × 170, Marée 132 × 160), soit **+20 %** à l'écran pour
  Fusible et Volets ; empreintes en tokens, fonction pure testée.
- [ ] **CAD-02**: Quand l'empreinte change (style, orientation), la fenêtre **reste collée à son coin d'accroche** et ne déborde
  jamais de l'écran (DPI mixte, multi-écrans) ; l'aperçu des réglages montre l'empreinte réelle.
- [ ] **CAD-03**: Fusible **vertical** (110 × 190, brûle de haut en bas), Marée **horizontale** (190 × 96, ligne d'eau
  ondulée), Volets **vertical** (128 × 190) existent, chacun reconnaissable par son signe propre (plan §1.2), avec les états
  « en attente », « plancher » et « indisponible ».
- [ ] **CAD-04**: Une carte **Orientation** (Horizontal · Vertical) dans Apparence, visible seulement pour Fusible, Marée et
  Volets ; orientation **mémorisée par cadran** (défaut = orientation actuelle), indépendante de « Disposition verticale » ;
  la galerie `--cadrans` montre les huit variantes.

### Braises (BRA) — R3

- [ ] **BRA-01**: L'anneau 5 h de Braises montre **20 braises de 15 min en 5 groupes d'une heure** séparés par un vide, et
  une **flèche fixe à midi** (couleur `TickReset`) qui marque la ligne d'arrivée du reset.
- [ ] **BRA-02**: En mode temps, le centre de Braises affiche **l'heure du reset** (« ↻ HH:MM », issue de `resets_at`) sous les
  deux comptes à rebours ; rien d'autre ne change en mode pourcentages.

### Gestes (GST) — R7

- [ ] **GST-01**: Sur **toute la silhouette** de chaque variante (disque r 74 pour Arcs/Braises, rectangle arrondi à 6 px de
  marge pour les autres), un clic sans déplacement bascule % ↔ temps, un double-clic ouvre l'Historique sans bascule, un
  appui-glisser au-delà du seuil Windows déplace puis accroche, un clic droit ouvre les réglages.
- [ ] **GST-02**: Hors silhouette, le clic **traverse** vers le bureau ; la silhouette est peinte en `ZoneSilhouette`
  (`#01000000`) ; aucun élément à geste n'a de pinceau nul ou `Transparent` (garde statique).
- [ ] **GST-03**: Les zones sont **prouvées par test** pour chaque cadran × orientation : rendu `RenderTargetBitmap` (alpha > 0
  dans la silhouette, = 0 hors) + `HitTest` de routage aux points témoins ; les commentaires « Transparent suffit » sont
  corrigés.

### Livraison et constat (VAL)

- [ ] **VAL-06**: `Chronos-v3.5.0.exe` est publié (version aux quatre propriétés du csproj et dans le nom), contrôles et smoke
  `--hook`, retrait de la statusLine constaté dans `~/.claude/settings.json` ; README et docs à jour.
- [ ] **VAL-07**: **Constat avec l'utilisateur** sur la 3.5.0 (verdict écrit) : huit variantes, zones en clics réels, Braises,
  plein écran, thèmes ; reprend les constats reportés de v1.8 (32-08 / VAL-04 et 35-07 / VAL-05).

## v2 Requirements (différé)

- Anneau hebdo de Braises : 12 braises pour 7 jours (14 demi-journées ou 7 séparateurs).
- Heure du reset (« ↻ HH:MM ») sur les autres cadrans en mode temps.
- Thèmes à fond clair (variante de `From()`, fenêtres secondaires claires).
- Palettes en réserve : Café (Classique), Tropique (Vive).
- Reports de v1.8 : heatmap jour × heure, export CSV, compaction du journal, dimension projet, projection conditionnelle,
  `DayTimeline` sur resets observés, dérive DST de `WeeklyWindow` (devenu sans objet si `WeeklyWindow` est supprimé — à
  revoir contre `BornesPlage`).

## Out of Scope

- Réparer le pont statusLine comme source (décision utilisateur : barre retirée).
- Fonds clairs dans « Pâle » (décision utilisateur).
- Plein écran par graphique (non demandé ; un plein écran général suffit).
- Grille 24 h des resets sur Braises (erreur de catégorie : l'anneau est la fenêtre de 5 h).
- Modes Normal/Étendu pour Fusible et Volets (n'existent pas ; clause de R2 sans objet).
- Réglage d'orientation partagé avec « Disposition verticale » du widget de sessions.

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| SOC-01 | Phase 36 | Complete |
| SOC-02 | Phase 36 | Complete |
| DAT-01 | Phase 37 | Complete |
| DAT-02 | Phase 37 | Complete |
| DAT-03 | Phase 37 | Complete |
| DAT-04 | Phase 37 | Complete |
| DAT-05 | Phase 37 | Complete |
| HIS-09 | Phase 38 | Complete |
| HIS-10 | Phase 38 | Complete |
| HIS-11 | Phase 38 | Complete |
| THM-01 | Phase 39 | Complete |
| THM-02 | Phase 39 | Complete |
| THM-03 | Phase 39 | Complete |
| THM-04 | Phase 39 | Complete |
| CAD-01 | Phase 40 | Pending |
| CAD-02 | Phase 40 | Pending |
| CAD-03 | Phase 40 | Pending |
| CAD-04 | Phase 40 | Pending |
| BRA-01 | Phase 41 | Pending |
| BRA-02 | Phase 41 | Pending |
| GST-01 | Phase 42 | Pending |
| GST-02 | Phase 42 | Pending |
| GST-03 | Phase 42 | Pending |
| VAL-06 | Phase 43 | Pending |
| VAL-07 | Phase 43 | Pending |

**Coverage:**
- v1 requirements: 25 total
- Mapped to phases: 25
- Unmapped: 0 ✓

---
*Traceability mise à jour le 2026-10-03 à la création de la roadmap v1.9 (phases 36-43).*
