# Roadmap : Chronos

## Milestones

- ✅ **v1.0 — Overlay de quotas Claude complet** (7 phases, 18 plans, SHIPPED 2026-07-08) — [archive](.planning/milestones/v1.0-ROADMAP.md)
- ✅ **v1.1 — Estimation utile en mode app bureau** (2 phases, 5 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.1-ROADMAP.md)
- ✅ **v1.2 — Usage exact via l'endpoint OAuth** (2 phases, 4 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.2-ROADMAP.md)
- ✅ **v1.3 — Refonte du cadran (3 anneaux, remplissage, compacité)** (1 phase, phase 12, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.3-ROADMAP.md)
- ✅ **v1.4 — Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code)** (2 phases, phases 13-14, 5 plans, SHIPPED 2026-07-11) — [archive](.planning/milestones/v1.4-ROADMAP.md)
- ✅ **v1.5 — Exactitude permanente** (6 phases, phases 15-20, 28 plans, SHIPPED 2026-09-12) — [archive](.planning/milestones/v1.5-ROADMAP.md)
- ✅ **v1.6 — Observer au lieu de déduire (widget de sessions)** (6 phases, phases 21-26, 19 plans, SHIPPED 2026-09-13, exe 3.1.0) — [archive](.planning/milestones/v1.6-ROADMAP.md)
- ✅ **v1.7 — Lue ou non lue (widget de sessions)** (6 phases, phases 27-31 + 30.1, 20 plans, SHIPPED 2026-09-26, exe 3.2.1, clos le 2026-09-27 avec un écart connu : constat en production partiel, repris en phase 32) — [archive](.planning/milestones/v1.7-ROADMAP.md)
- ✅ **v1.8 — Historique d'utilisation** (4 phases, phases 32-35, 26 / 28 plans, SHIPPED 2026-09-27, exe 3.3.0 — puis 3.3.1 et 3.4.0 hors phases —, clos le 2026-10-03 avec écarts connus : constats VAL-04 (32-08) et VAL-05 (35-07) non joués, reportés en v1.9 phase 43) — [archive](.planning/milestones/v1.8-ROADMAP.md)
- 🚧 **v1.9 — Lisible partout** (phases 36-43, en cours, exe cible 3.5.0) — [exigences](.planning/REQUIREMENTS.md)

## Phases

<details>
<summary>✅ v1.8 — Historique d'utilisation (Phases 32-35) — SHIPPED 2026-09-27, clos le 2026-10-03 avec écarts connus</summary>

- [ ] Phase 32 : Compter juste, puis journaliser (7/8 plans — 32-08, constat VAL-04, reporté en v1.9) — exe 3.2.2
- [x] Phase 33 : Agrégats de tokens (5/5 plans) — completed 2026-09-27
- [x] Phase 34 : Fenêtre Historique : Semaine et Jour (8/8 plans) — completed 2026-09-27
- [ ] Phase 35 : 4 semaines, accès, release 3.3.0 (6/7 plans — 35-07, constat VAL-05, reporté en v1.9) — exe 3.3.0

Détail complet : [v1.8-ROADMAP.md](.planning/milestones/v1.8-ROADMAP.md) · exigences : [v1.8-REQUIREMENTS.md](.planning/milestones/v1.8-REQUIREMENTS.md).

</details>

## Milestone v1.9 : Lisible partout

### Overview

Le cadran dit juste ; il ne se dit pas toujours bien. Fusible et Volets sont rendus à 75 % et 68 % de leur taille par un
`Viewbox` qui les écrase dans une fenêtre carrée de 170 × 170 ; Marée n'existe qu'à la verticale, Fusible et Volets qu'à
l'horizontale ; l'anneau 5 h de Braises ne dit pas où la fenêtre s'arrête ; le clic qui bascule % ↔ temps n'est un disque de
66 px bien placé que pour un style sur cinq, et le reste du cadran laisse passer les clics vers le bureau ; quatre cadrans
sur cinq ne suivent pas le thème ; le gris « épuisé » n'atteint que 1,7 à 2,1 de contraste sur tous les thèmes ; l'Historique
ne sait pas s'agrandir. Et derrière, la chaîne de données traîne trois sources qui n'ont **jamais** produit un relevé depuis
septembre (1 531 relevés de la sonde d'en-têtes, 7 du secours OAuth Chronos, zéro du jeton de l'app bureau, zéro du pont
statusLine) — dont le code le plus sensible du projet, qui déchiffre le jeton d'une autre application, et un diagnostic qui
recommande encore une source morte et coûte ≈ 17 s par rapport. Ce milestone rend **chaque cadran lisible et saisissable,
dans toutes ses formes, sur une chaîne de données réduite à ce qui marche**.

**Cadre contractuel :** roadmap utilisateur `.zeus/ROADMAP-PROCHAINE-VERSION.md` (R1-R7, 2026-10-01) ; inventaires en lecture
seule `.zeus/reports/cycle2/inventaire-*.md` (faits chemin:ligne) ; conseil LLM du 2026-10-03
(`.zeus/reports/llm-council-2026-10-03.md`, 5 membres, 5 relecteurs — approche et ordre des phases arrêtés) ; **plan de design
`.zeus/DESIGN_PLAN_CYCLE2.md` validé par l'utilisateur le 2026-10-03** (maquettes `.zeus/maquettes/cycle2-cadrans-themes.html`)
— il fixe les empreintes, les orientations et leurs signes propres, les zones de geste, Braises, le plein écran, les palettes
et les seuils de lisibilité. **Il n'y a donc pas de phase UI-SPEC** : les plans des phases 38 à 42 le citent comme contrat.
**Liste de purge `.zeus/reports/cycle2/liste-purge.md` validée par l'utilisateur le 2026-10-03** (DAT-01).

**Décisions de l'utilisateur (2026-10-03) :** la barre statusLine est **retirée** (pas de relais d'affichage) ; « Pâle » =
**sombres doux** (pas de fonds clairs) ; **+20 % mesuré sur le rendu actuel** (Fusible ≈ 190 × 92, Volets ≈ 190 × 66, pas
1,2 × 210) ; Braises = repère de reset + séparations horaires + heure du reset ; une seule release **3.5.0**.

**Doctrine (inchangée) :** exact ou rien — seul le plancher « ≥ X % » n'est pas exact, et il le dit ; XAML pur, aucune
dépendance native ; chemins sous `%APPDATA%\Chronos` ; lecture seule de `~/.claude` **hors réconciliation contrôlée de
`~/.claude/settings.json`** (hooks, et désormais retrait sauvegardé de la `statusLine`) ; aucune valeur de couleur ni de taille
hors `Resources/DesignTokens.xaml` ou des pinceaux du thème ; loi d'encodage conservée (temps = géométrie, quota =
couleur/luminance, gris = épuisé ; l'orange reste réservé aux sessions qui attendent) ; UI et commentaires en français.

Le milestone se lit en **huit gestes**, dans l'ordre du conseil (socle → purge → Historique → thèmes → géométrie → Braises →
zones → release), ordonnés par dépendance réelle :

1. **Socle** (Phase 36). *Aucune UI.* Deux pièges qui rendraient dangereuses toutes les suppressions suivantes :
   `SettingsService.Load()` remet **tous** les réglages à zéro dès qu'une valeur d'énumération est inconnue (supprimer
   `Tuiles` ou un style de cadran effacerait thème, coin et géométries) ; et un `--statusline` retiré, appelé par Claude Code
   à chaque rendu de barre, lancerait l'overlay et heurterait le verrou mono-instance. Les deux se règlent avant de toucher
   à quoi que ce soit.
2. **Une chaîne de données claire** (Phase 37). La purge R5, dans l'ordre de la liste validée, **un commit réversible par
   ligne** : diagnostic et docs → orphelins → jeton de l'app bureau → recalibrage → pont statusLine et retrait de la barre,
   en dernier, derrière la garde d'arguments de la 36.
3. **Historique : Pistes seul et plein écran** (Phase 38). Suppression de Simplifié et Tuiles (décision utilisateur de v1.8),
   qui n'est sûre que grâce à la lecture tolérante de la 36 ; puis un plein écran général lisible de loin.
4. **Thèmes Pâle · Classique · Vive** (Phase 39). Trois groupes, quinze thèmes, gardes de contraste testées sur tout le
   catalogue, et les quatre cadrans alternatifs qui suivent enfin le thème — **avant** d'être redessinés.
5. **Cadrans à l'échelle 1 et orientations** (Phase 40). Plus de `Viewbox`, des empreintes par cadran, un ancrage qui tient,
   trois variantes nouvelles reconnaissables.
6. **Braises** (Phase 41). Où s'arrête la fenêtre de 5 h, sur la géométrie à l'échelle 1.
7. **Un geste sur toute la silhouette** (Phase 42). Les zones se dessinent sur la géométrie **finale** des huit variantes, et
   se prouvent par rendu.
8. **Release 3.5.0 et constat** (Phase 43). L'exe, la barre retirée constatée, et le constat avec l'utilisateur — qui reprend
   enfin les constats reportés de v1.8.

**Contraintes portées :** C# / .NET 8 (`net8.0-windows`) / WPF / MVVM (CommunityToolkit.Mvvm) +
Microsoft.Extensions.DependencyInjection + Hosting ; aucune dépendance native ; chemins sous `%USERPROFILE%` / `%APPDATA%`
uniquement, aucun droit admin. Les **1707 tests xUnit** restent verts (0 warning Debug et Release) ; les tests des éléments
supprimés partent avec eux, les gardes de doctrine (« exact ou rien »), `ServicesLayerPurityTests`, `CompositionRootTests`,
`GardesPerimetreTests` et `GardesDoctrineTests` restent vertes **à chaque commit** ; chaque phase de code ajoute les siens.
Géométries en fonctions pures testées sans fenêtre (`EmpreinteCadran`, braises, silhouettes), sur le modèle d'`ArcGeometry`.
Definition of Done du cycle : `.zeus/DOD.md`.

**Règles d'observation (constats) :** l'agent ne lance, n'arrête ni ne clique **jamais** l'overlay ; les relevés de fichiers
se font **hors de l'arbre de l'app** (sonde WMI), **jamais depuis une session Claude Code** — AppData est virtualisé par MSIX
pour tout ce qui tourne sous l'app bureau. Le retrait de la `statusLine` se teste **sur fichiers témoins** ; le vrai
`~/.claude/settings.json` n'est modifié que par l'exe 3.5.0 lancé par l'utilisateur. Ne JAMAIS appeler l'endpoint de refresh
OAuth avec le jeton réel ; ne JAMAIS écrire dans `%APPDATA%\Claude` ni dans `~/.claude/projects`.

**Hors périmètre, rappelé :** réparer le pont statusLine comme source ; fonds clairs (Brume, Lin) ; plein écran par graphique ;
grille 24 h des resets sur Braises ; modes Normal / Étendu pour Fusible et Volets ; orientation partagée avec « Disposition
verticale » du widget de sessions ; halo animé sur la flèche de Braises. Différé au-delà de v1.9 (v2 de `REQUIREMENTS.md`) :
anneau hebdo de Braises (12 braises pour 7 jours), « ↻ HH:MM » sur les autres cadrans, thèmes à fond clair, Café et Tropique,
reports de v1.8 (heatmap, export CSV, compaction, dimension projet, projection conditionnelle, `DayTimeline` sur resets
observés).

### Phases

**Numérotation des phases :**
- Phases entières (36→43) : travail de milestone planifié — continue après la Phase 35 (v1.8)
- Phases décimales (36.1, 36.2) : insertions urgentes (marquées INSERTED)

- [x] **Phase 36: Socle** - Une valeur inconnue dans `settings.json` ne coûte plus que cette valeur, et tout argument `--xxx` inconnu ou retiré sort en silence avant le verrou mono-instance (completed 2026-10-03)
- [x] **Phase 37: Une chaîne de données claire** - Les sources mortes, le pont statusLine et le recalibrage disparaissent en commits réversibles ; la barre est retirée de Claude Code avec sauvegarde ; le diagnostic et les docs décrivent exactement la chaîne réelle (completed 2026-10-03)
- [x] **Phase 38: Historique : Pistes seul et plein écran** - L'Historique n'a plus qu'un style, et un plein écran sur l'écran courant agrandit pistes et textes sans défilement, avec une sortie toujours à portée (completed 2026-10-03)
- [x] **Phase 39: Thèmes Pâle / Classique / Vive** - Quinze thèmes rangés en trois groupes, un gris épuisé et un rouge lisibles partout (≥ 3:1), et les quatre cadrans alternatifs qui suivent le thème (completed 2026-10-03)
- [ ] **Phase 40: Cadrans à l'échelle 1 et orientations** - Chaque cadran a son empreinte réelle (+20 % pour Fusible et Volets), la fenêtre reste collée à son coin, et Fusible vertical, Marée horizontale, Volets vertical existent
- [ ] **Phase 41: Braises** - L'anneau 5 h montre 20 braises en 5 heures séparées par un vide, une flèche fixe à midi, et l'heure du reset en mode temps
- [ ] **Phase 42: Un geste sur toute la silhouette** - Bascule, double-clic, glisser et clic droit marchent sur toute la silhouette des huit variantes, le clic traverse en dehors, et c'est prouvé par rendu
- [ ] **Phase 43: Release 3.5.0 et constat** - `Chronos-v3.5.0.exe` publié, la barre retirée constatée, et le constat avec l'utilisateur qui reprend VAL-04 et VAL-05 de v1.8

### Phase Details

### Phase 36: Socle
**Goal**: Rendre sûres toutes les suppressions du milestone. Un `settings.json` qui contient une valeur que la 3.5 ne connaît
plus (style d'Historique `"Tuiles"`, style de cadran retiré, clé de thème inconnue, membre supprimé) ne perd **que cette
valeur** ; et un appel `--statusline` (ou tout autre `--xxx` inconnu) — que Claude Code continuera d'émettre tant que la barre
n'est pas retirée — sort en silence au lieu de lancer l'overlay ou la boîte « Chronos tourne déjà ».
**Depends on**: Rien dans le code de v1.9 — première phase. Corrige le piège relevé par l'inventaire
(`SettingsService.Load()` tout-ou-rien, `inventaire-themes-historique.md` § B « PIÈGE ») et le risque A6 de
`inventaire-purge-donnees.md` (`--statusline` traité avant le verrou, `App.xaml.cs:26` / `:84`).
**Requirements**: SOC-01, SOC-02
**Success Criteria** (what must be TRUE):
  1. **Une valeur fautive ne coûte qu'elle-même** : un `settings.json` témoin contenant `"HistoriqueStyleSemaine": "Tuiles"`
     (après suppression du membre), un `CadranStyle` inconnu et une clé de thème inconnue se charge avec **seulement** ces
     valeurs retombées sur leur défaut ; thème, coin d'accroche, géométries de fenêtres et `InnerStatusLineCommand` sont
     conservés et **réécrits intacts** à la sauvegarde suivante — épinglé par tests, y compris pour les pièges latents
     `SessionStyle` et `SectionReglages` (SOC-01).
  2. **Un JSON réellement illisible reste un cas à part** : un fichier tronqué ou non-JSON est toujours toléré sans crash
     (comportement actuel conservé, testé), et le chargement valeur par valeur ne masque jamais une erreur de syntaxe en
     réglages « à moitié lus » (SOC-01).
  3. **`--statusline` et tout `--xxx` inconnu sortent en silence** : l'exe lancé avec un argument inconnu ou retiré quitte
     avec le code 0 **avant** l'acquisition du verrou mono-instance — ni overlay, ni boîte « Chronos tourne déjà », ni
     réconciliation des hooks — vérifié par test sur la fonction de tri des arguments et par une garde textuelle sur l'ordre
     dans `App.xaml.cs` (SOC-02).
  4. **Rien d'autre ne bouge** : `--hook`, le mode CLI, `--cadrans`, `--historique` et `--sessions` se comportent comme en
     3.4.0 (tests existants verts), et la suite reste à 0 échec, 0 warning (SOC-02).
**Plans**: 2 plans
Plans:
- [x] 36-01-PLAN.md — Lecture des réglages tolérante valeur par valeur (pré-passe JsonNode) + ligne de diagnostic (SOC-01)
- [x] 36-02-PLAN.md — Tri pur des arguments en liste blanche, inconnu → sortie code 0 avant le verrou, gardes réécrites (SOC-02)

### Phase 37: Une chaîne de données claire
**Goal**: La chaîne de données se réduit à ce qui a réellement produit des relevés —
`LastExact( Journal( Composite(sonde d'en-têtes, OAuth Chronos) ) )` — par la **liste de purge validée** exécutée dans son
ordre, **un commit réversible par ligne** ; Chronos retire sa barre de `~/.claude/settings.json` au premier lancement de la
3.5, après sauvegarde ; le diagnostic, `docs/data-sources.md`, le README et `CLAUDE.md` décrivent **exactement** cette chaîne,
et disent que seul le plancher « ≥ » n'est pas exact.
**Depends on**: Phase 36 — la garde d'arguments (SOC-02) doit exister avant que `--statusline` disparaisse, et la lecture
tolérante (SOC-01) avant que `OAuthUsageEnabled`, `InnerStatusLineCommand` ou `StatusLinePromptDismissed` ne deviennent des
membres inconnus. **Contrat :** `.zeus/reports/cycle2/liste-purge.md` (validée), ordre d'exécution : 1. diagnostic et docs
(§5, §8) · 2. orphelins (§4, §7) · 3. jeton de l'app bureau (§1, §6) · 4. recalibrage (§3) · 5. pont statusLine + retrait de
la barre (§2).
**Requirements**: DAT-01 (déjà validé le 2026-10-03), DAT-02, DAT-03, DAT-04, DAT-05
**Success Criteria** (what must be TRUE):
  1. **Les sources mortes ont disparu, la doctrine tient à chaque commit** : `ClaudeOAuthUsageProvider`,
     `GatedOAuthUsageProvider`, `ClaudeTokenReader`, `WindowsCredentialStore`, `InventaireMachine`,
     `ClaudeUsageObjectProvider` et la surveillance de `usage.json`, `FiveHourWindowInference`, `WeeklyWindow`,
     `WeeklyRecalibration` (dialogue et carte « Recalibrer… » compris), `OAuthUsageEnabled` et sa commande, la migration
     `PurgerPrefixe("desktop:")` et les valeurs `EndpointOAuthClaude` / `PontStatusLine` n'existent plus ; la garde « 3
     composites » devient « **1 composite** » ; `GardesDoctrineTests` et `CompositionRootTests` sont verts après **chacun**
     des cinq commits de purge, chacun réversible seul ; un journal existant se relit toujours sans erreur et l'ancre
     `WeeklyAnchor` déjà enregistrée reste lue en secours par l'Historique (DAT-02).
  2. **La barre quitte Claude Code proprement** : au premier lancement de la 3.5, Chronos sauvegarde
     `~/.claude/settings.json` puis en retire **sa** `statusLine` — idempotent (un second lancement ne change rien), journalisé
     dans `chronos.log`, testé sur fichiers témoins (barre Chronos, barre d'un tiers laissée intacte, fichier sans barre,
     fichier illisible) ; les hooks restent réconciliés ; le mode `--statusline`, `StatusLineBridge`, `StatusLineInstaller`,
     la proposition au premier lancement et la carte « Barre de statut de Claude Code » ont disparu, et un `--statusline`
     résiduel sort en silence par la garde de la Phase 36 (DAT-03).
  3. **Le diagnostic dit la chaîne réelle, vite** : une section « Chaîne de données » décrit la sonde d'en-têtes, le secours
     OAuth Chronos, le dernier exact persisté et le journal ; les sections « pont statusLine », « endpoint OAuth (repli) »,
     « Réglage Usage exact (OAuth) » et le « Conseil » trompeur ont disparu ; un rapport ne lance plus la recherche des coffres
     (≈ 17 s → mesuré et consigné, attendu sous la seconde hors réseau) (DAT-04).
  4. **Une seule méthodologie, écrite** : `docs/data-sources.md` réécrit (source → cadran, ordre de priorité, repli, ce qui
     est exact et ce qui ne l'est pas : seul le plancher « ≥ » n'est pas exact), README « D'où viennent les chiffres » et
     `CLAUDE.md` alignés, commentaires « repli JSONL / estimation » corrigés (CompositeUsageProvider, SourceReliability,
     WindowState, EmberRingControl, FuseBar, TideColumn, MainViewModel, RateLimitHeaderUsageProvider) ; une garde
     documentaire rougit si un document cite encore une source retirée (DAT-05).
**Plans**: TBD — point d'attention pour le plan : l'ordre de la liste de purge est contractuel et chaque ligne est un commit
autonome, réversible, suite verte ; la ligne 5 (pont + retrait de la barre) passe en dernier. DAT-01 (point de contrôle humain
avant suppression) est **déjà satisfait** : aucun plan ne le rejoue.

### Phase 38: Historique : Pistes seul et plein écran
**Goal**: L'Historique n'a plus qu'**un style, Pistes**, sans sélecteur nulle part ; et un **plein écran général** sur l'écran
courant agrandit les pistes dans leurs proportions et les textes d'environ ×1,35, sans défilement, avec une sortie toujours
visible (bouton, F11, Échap à deux niveaux) et les mêmes règles d'honnêteté à toutes les tailles.
**Depends on**: Phase 36 — la propriété `HistoriqueStyleSemaine` est **supprimée** et un ancien réglage `"Tuiles"` doit se lire
sans perte (SOC-01). Séquentielle après la 37 (les deux touchent `ReglagesWindow.xaml`). **Contrat :**
`.zeus/DESIGN_PLAN_CYCLE2.md` §4 (Pistes seul, plein écran), §7 (petit écran).
**Requirements**: HIS-09, HIS-10, HIS-11
**Success Criteria** (what must be TRUE):
  1. **Un seul style, plus aucun sélecteur** : le sélecteur « Style : Pistes · Simplifié · Tuiles » a disparu de la fenêtre et
     de la carte des réglages (qui garde « Aussi : double-clic sur le cadran ») ; les grilles Simplifié et Tuiles, la piste
     Fenêtres 5 h (`Tuiles5h`, `PisteFenetres5h`, variante `SemaineHebdoSeul`), les 6 tokens `HistoHauteur*Simplifie/*Tuiles`,
     leurs textes et le README « Trois styles » sont retirés ; un `settings.json` témoin contenant
     `"HistoriqueStyleSemaine": "Tuiles"` se charge sans perdre un autre réglage (HIS-09).
  2. **Le plein écran couvre l'écran courant, pistes agrandies dans leurs proportions** : le bouton « ⛶ Plein écran » (à droite
     de la ligne de fraîcheur) ou F11 pose la fenêtre sur les **bornes du moniteur où elle se trouve** (barre des tâches
     couverte, pas `Maximized`) ; les rangées se partagent la hauteur dans les proportions du mode normal — Semaine 150 · 72 ·
     72, Jour 190 · 64 · 64, 4 semaines 250 — plafonnées à **Niveau 520, Rythme et Tokens 240** ; couverture et annotations
     restent fixes ; la vue ne défile plus ; vérifié par Measure/Arrange (HIS-10).
  3. **Les textes et traits grandissent par paliers, pas en continu** : le dictionnaire `PleinEcran` en `DynamicResource`
     applique ≈ ×1,35 — corps 8,5 → 11,5, 9 → 12, 9,5 → 13, 10,5 → 14, 11 → 15, 11,5 → 15,5, 14 → 18, 16 → 21 ; colonne des
     libellés 96 → 128, légende 72 → 96 ; escalier 2,2 → 3, premier plan 2,4 → 3,2, tiret de reset 8 → 11 — toutes ces
     valeurs en tokens ; sur un écran < 1 280 px les plafonds s'appliquent et rien n'est tronqué (HIS-10).
  4. **On sort toujours, et on retrouve sa fenêtre** : le bouton « ⤢ Quitter le plein écran · Échap » est visible dans
     l'en-tête en plein écran ; F11 bascule ; **Échap quitte d'abord le plein écran, puis un second Échap ferme la fenêtre** ;
     la position et la taille d'avant sont restaurées (test) (HIS-11).
  5. **L'honnêteté ne dépend pas de la taille** : trous hachurés, « +N % pendant l'absence (répartition inconnue) », libellé
     permanent des tokens et pied de page « Aucun trou n'est interpolé … » sont présents mot pour mot en mode normal et en
     plein écran, sur les trois vues (test sur la galerie `--historique`) (HIS-10).
**Plans**: TBD
**UI hint**: yes

### Phase 39: Thèmes Pâle / Classique / Vive
**Goal**: Les thèmes passent de 9 à **15**, rangés en trois groupes titrés **Pâle · Classique · Vive** ; sur **chaque** thème,
le gris « épuisé » et le rouge de fin de rampe se lisent (contraste ≥ 3:1, rouge dans la bande 335°–20°), vérifié par test sur
tout le catalogue ; et Braises, Fusible, Marée et Volets **suivent enfin le thème**, `TickReset` compris.
**Depends on**: Rien dans les phases 37-38 côté code (catalogue `Theming/ChronosTheme.cs`, section Thème des réglages, vues
des cadrans alternatifs). Placée **avant** la Phase 40 : les quatre vues alternatives sont thémées une fois, puis redessinées
à l'échelle 1 sur des pinceaux déjà propres. **Contrat :** `.zeus/DESIGN_PLAN_CYCLE2.md` §5.
**Requirements**: THM-01, THM-02, THM-03, THM-04
**Success Criteria** (what must be TRUE):
  1. **Trois groupes titrés, quinze thèmes** : la section Thème montre « Pâle » (Nord, Forêt, Moka, Roseraie, Sauge, Lavande),
     « Classique » (Minuit — défaut —, Ardoise, Ambre chaud, Graphite, Marine) et « Vive » (Néon, Aurore, Synthwave, Lave),
     titres en 10,5 semi-gras `Ink2`, vignettes 88 × 76 inchangées ; chaque thème porte sa `Categorie` ; `ThemingTests` compte
     15 thèmes ; un thème déjà choisi reste choisi après mise à jour (THM-01).
  2. **Six palettes aux valeurs du plan, deux rampes corrigées** : Sauge, Lavande, Graphite, Marine, Synthwave et Lave ont
     exactement les sept couleurs du plan §5.2 (test) ; Néon passe à l'ambre `#FFC23D` et au rouge `#FF2E63`, Aurore à l'ambre
     `#F0C36D` et au rouge `#F2577A`, leurs décors (disque, piste, graduation, texte) inchangés (THM-02).
  3. **Lisible sur tout le catalogue, prouvé** : pour chacun des 15 thèmes, le gris « épuisé » calculé dans `From()` (plus petit
     mélange piste → graduation qui y parvient) atteint un contraste **≥ 3:1 contre le disque** (aujourd'hui 1,7 à 2,1), et le
     rouge de fin de rampe a une teinte dans **335°–20°** et un contraste **≥ 3:1** ; un thème ajouté plus tard qui viole l'une
     de ces règles fait rougir le test (THM-03).
  4. **Les quatre cadrans alternatifs changent avec le thème** : en changeant de thème, les textes, tuiles et fonds de
     Braises, Fusible, Marée et Volets passent sur `TextePrincipal`, `TexteSecondaire` et les fonds de tuile du thème ;
     `TickReset` entre dans les pinceaux du thème ; une garde statique rougit sur toute couleur littérale dans les quatre vues
     et leurs contrôles (THM-04).
**Plans**: TBD — point d'attention : les vues `Views/Cadrans/*` et contrôles `Controls/Cadrans/*` touchés ici sont redessinés
en Phase 40 ; garder l'ordre strict 39 → 40 pour éviter les conflits de fichiers.
**UI hint**: yes

### Phase 40: Cadrans à l'échelle 1 et orientations
**Goal**: Chaque cadran est rendu **à l'échelle 1** dans **son empreinte réelle** — Fusible et Volets gagnent +20 % à l'écran —
et la fenêtre reste collée à son coin d'accroche quand l'empreinte change ; Fusible vertical, Marée horizontale et Volets
vertical existent, chacun reconnaissable par son signe propre, avec ses états non nominaux ; l'orientation se choisit par
cadran dans Apparence.
**Depends on**: Phase 39 — les vues alternatives sont thémées avant d'être redessinées (sinon conflit de fichiers dans
`Views/Cadrans/` et `Controls/Cadrans/` : si l'ordre strict ne pouvait pas être tenu, ce risque de conflit est à arbitrer au
plan). **Contrat :** `.zeus/DESIGN_PLAN_CYCLE2.md` §1 (empreintes, signes propres, réglage d'orientation), §7 (états).
**Requirements**: CAD-01, CAD-02, CAD-03, CAD-04
**Success Criteria** (what must be TRUE):
  1. **Chaque cadran à sa vraie taille** : plus de `Viewbox` autour des cadrans (seul l'aperçu 144 × 144 des réglages en garde
     un) ; la fonction pure `EmpreinteCadran(style, orientation)` rend, depuis des tokens `CadranLargeur*` / `CadranHauteur*`,
     Arcs et Braises 170 × 170, Fusible 190 × 92, Volets 190 × 66, Marée 132 × 160 (test sans fenêtre) ; la fenêtre principale
     prend cette empreinte ; corps de texte non réduits (libellés 11, valeurs 12, plaque Volets 13 / 14) (CAD-01).
  2. **La fenêtre tient son coin** : un changement de style ou d'orientation garde le coin d'accroche courant, la fenêtre
     grandit vers l'intérieur et ne déborde jamais de l'écran — testé pour les quatre coins, à 100 % et 150 % d'échelle, sur
     écrans multiples (fonction de recalage pure) ; l'aperçu des réglages montre l'empreinte réelle réduite (CAD-02).
  3. **Trois variantes nouvelles, reconnaissables** : Fusible vertical 110 × 190 (mèche fine, étincelle au front, brûle de
     haut en bas comme une bougie), Marée horizontale 190 × 96 (large bande, ligne d'eau légèrement ondulée, la lumière part de
     la gauche), Volets vertical 128 × 190 (tuile, plaque 56 × 40, 6 volets empilés) ; chacune rend les états « en attente »
     (marques translucides, jamais de vide), « plancher » (grain ou pointillé sur la marque du quota, jamais sur le temps) et
     « indisponible » (mot centré dans l'empreinte) (CAD-03).
  4. **L'orientation se choisit par cadran** : une carte « Orientation » (Horizontal · Vertical) apparaît sous les styles dans
     Apparence pour Fusible, Marée et Volets seulement ; chaque cadran mémorise la sienne (défaut = orientation actuelle ;
     passer de Fusible à Marée ne retourne pas Marée), sans lien avec « Disposition verticale » du widget de sessions ; une
     orientation inconnue dans `settings.json` retombe sur le défaut (SOC-01) ; la galerie `--cadrans` montre les **huit**
     variantes, Arcs compris (CAD-04).
**Plans**: TBD — si la revue visuelle juge Volets vertical confus, le plan de design prévoit de le retirer plutôt que de livrer
une variante faible (§8) ; ce serait une décision utilisateur.
**UI hint**: yes

### Phase 41: Braises
**Goal**: L'anneau 5 h de Braises dit **où s'arrête la fenêtre** : 20 braises de 15 min en 5 heures séparées par un vide, une
flèche fixe à midi qui marque la ligne d'arrivée du reset, et en mode temps l'heure exacte du reset, donnée par le serveur.
**Depends on**: Phase 40 — Braises est rendu à l'échelle 1 (le `Viewbox` le réduisait de 5 %) ; la géométrie se cale sur
l'empreinte finale 170 × 170. **Contrat :** `.zeus/DESIGN_PLAN_CYCLE2.md` §3.
**Requirements**: BRA-01, BRA-02
**Success Criteria** (what must be TRUE):
  1. **Cinq heures lisibles d'un coup d'œil** : l'anneau 5 h (R66) montre **20 braises de 15 min en 5 groupes de 4**, chaque
     groupe sur 72° avec un pas de 13,2°, pastilles de rayon 4,0, séparés par un vide plus large ; le nombre de braises
     allumées suit `FractionRemaining` ; géométrie pure testée (angles, groupes, vide) ; l'anneau hebdo (12 braises) ne change
     pas (BRA-01).
  2. **La ligne d'arrivée est fixe à midi** : une flèche à l'extérieur de l'anneau (triangle 10 × 7 et filet de 12 px), couleur
     `TickReset` **du thème**, sans animation ; le reset a lieu quand la dernière braise l'atteint (BRA-01).
  3. **L'heure du reset, et seulement si elle est connue** : en mode temps, une troisième ligne « ↻ HH:MM » (10,5,
     `TexteSecondaire`), issue de `resets_at` exposé par `WindowGaugeViewModel`, s'affiche sous les deux comptes à rebours ;
     si `resets_at` est inconnu, la ligne ne s'affiche pas (jamais d'heure inventée) ; en mode pourcentages le centre est
     inchangé (test) (BRA-02).
**Plans**: TBD
**UI hint**: yes

### Phase 42: Un geste sur toute la silhouette
**Goal**: **Un seul geste, sur toute la silhouette** de chacune des huit variantes : clic sans déplacement = bascule % ↔ temps,
double-clic = Historique sans bascule, appui-glisser = déplacer puis accrocher, clic droit = réglages ; hors silhouette, le
clic traverse vers le bureau ; et c'est **prouvé par rendu**, pas supposé.
**Depends on**: Phases 40 et 41 — les silhouettes se dessinent sur la géométrie finale (empreintes, orientations, Braises à
l'échelle 1). **Contrat :** `.zeus/DESIGN_PLAN_CYCLE2.md` §2.
**Requirements**: GST-01, GST-02, GST-03
**Success Criteria** (what must be TRUE):
  1. **Toute la silhouette répond, de la même façon** : sur un disque de rayon 74 (Arcs, Braises) ou un rectangle arrondi
     (rayon 10, 6 px de marge) qui englobe Fusible, Marée et Volets dans les deux orientations, un clic relâché sans
     déplacement au-delà du seuil Windows (`SystemParameters.MinimumHorizontal/VerticalDragDistance`) bascule % ↔ temps, un
     double-clic ouvre l'Historique sans bascule (`ArbitreClicCentre` inchangé), un appui-glisser déplace puis accroche au coin
     le plus proche, un clic droit ouvre les réglages ; le disque `CentreHit` de 66 px est supprimé ; les pastilles d'Arcs
     (âge du relevé, alertes) gardent leurs propres clics ; un seul répartiteur de gestes dans `MainWindow` (GST-01).
  2. **Hors silhouette, le bureau reçoit le clic** : la silhouette est peinte avec le token `ZoneSilhouette` (`#01000000`) et
     rien d'autre ne capte la souris ; une garde statique rougit si un élément à geste a un pinceau nul ou `Transparent`
     (GST-02).
  3. **Prouvé pour les huit variantes** : pour chaque cadran × orientation, un rendu `RenderTargetBitmap` donne alpha > 0 en
     tout point témoin de la silhouette et alpha = 0 en dehors, et un `HitTest` de routage aux mêmes points atteint le bon
     répartiteur ; aucun test ne repose sur `HitTest` seul ; les commentaires « Transparent suffit, CentreHit le prouve » sont
     corrigés ; `GardeGestesCadranTests` est adapté (GST-03).
**Plans**: TBD
**UI hint**: yes

### Phase 43: Release 3.5.0 et constat
**Goal**: `Chronos-v3.5.0.exe` est publié et documenté ; le retrait de la barre est constaté dans `~/.claude/settings.json`
après un lancement par l'utilisateur ; et l'utilisateur **constate** sur sa machine ce que le milestone promettait — huit
variantes, zones en clics réels, Braises, plein écran, thèmes — en reprenant enfin les constats reportés de v1.8 (32-08 /
VAL-04 et 35-07 / VAL-05).
**Depends on**: Phases 36 à 42 (tout le code du milestone). Reprend les dettes de v1.8 : protocoles
`.planning/phases/32-compter-juste-puis-journaliser/32-CONSTAT.md` et `.planning/phases/35-4-semaines-acc-s-release-3-3-0/35-CONSTAT.md`
(écart E1-ter : plusieurs exécutables en marche — le point (a) commence par les quitter tous, à la main).
**Requirements**: VAL-06, VAL-07
**Success Criteria** (what must be TRUE):
  1. **`Chronos-v3.5.0.exe` est publié** : version 3.5.0 aux quatre propriétés du csproj et dans le nom du fichier ;
     contrôles et smoke `--hook` de la procédure 32-07 ; suite verte, 0 warning Debug et Release ; commit de release sans tag
     ni push ; l'agent ne lance rien (VAL-06).
  2. **La documentation dit la 3.5** : README (cadrans et orientations, plein écran, thèmes en trois groupes, « D'où viennent
     les chiffres ») et `docs/data-sources.md` à jour, sous les gardes documentaires (VAL-06).
  3. **La barre a quitté Claude Code, constaté** : après lancement de la 3.5.0 par l'Explorateur, `~/.claude/settings.json` ne
     contient plus la `statusLine` de Chronos, la sauvegarde existe, les hooks pointent vers la 3.5.0 — relevé par sonde hors
     de l'arbre, jamais depuis une session (VAL-06).
  4. **Constaté avec l'utilisateur, verdict écrit** (`43-CONSTAT.md`) : les huit variantes dans les quatre coins à 100 % et
     150 % sans débordement ; bascule, double-clic, glisser et clic droit en clics réels sur toute la silhouette, clic qui
     traverse en dehors ; Braises (flèche à midi, 5 groupes, « ↻ HH:MM ») ; plein écran (écran courant, Échap à deux niveaux,
     F11) ; 15 thèmes en 3 groupes avec gris épuisé visible ; et les points repris de 32-08 (une seule instance, tableau des
     gestes L1…Q, V01…V12, journal qui s'écrit) et de 35-07 (Historique sur vraies données, trou réel annoté) — écarts compris
     (VAL-07).
**Plans**: TBD — le plan de constat est `autonomous: false` (point de contrôle humain) ; il sert aussi de revue visuelle selon
les critères §9 du plan de design.
**UI hint**: yes

### Progress

**Execution Order:**
Phase 36 (socle : lecture tolérante, garde d'arguments) → Phase 37 (purge R5, cinq commits réversibles, exige la garde
d'arguments) → Phase 38 (Historique, exige la lecture tolérante) → Phase 39 (thèmes) → Phase 40 (échelle 1 et orientations,
après les thèmes) → Phase 41 (Braises, sur la géométrie à l'échelle 1) → Phase 42 (zones, sur la géométrie finale) →
Phase 43 (release 3.5.0, constat).
Séquentiel par prudence : 37 et 38 touchent toutes deux `ReglagesWindow.xaml` ; 39 et 40 touchent les mêmes vues
`Views/Cadrans/*`. Seul degré de liberté réel : la Phase 39 ne dépend pas de 37-38 côté code et pourrait commencer en
parallèle de la 38 si les fichiers de réglages sont répartis sans recouvrement.

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 32. Compter juste, puis journaliser | v1.8 | 7/8 | Clos avec écart (32-08 reporté en v1.9) | 2026-09-27 |
| 33. Agrégats de tokens | v1.8 | 5/5 | Complete | 2026-09-27 |
| 34. Fenêtre Historique : Semaine et Jour | v1.8 | 8/8 | Complete | 2026-09-27 |
| 35. 4 semaines, accès, release 3.3.0 | v1.8 | 6/7 | Clos avec écart (35-07 reporté en v1.9) | 2026-09-27 |
| 36. Socle | v1.9 | 2/2 | Complete    | 2026-10-03 |
| 37. Une chaîne de données claire | v1.9 | 6/6 | Complete    | 2026-10-03 |
| 38. Historique : Pistes seul et plein écran | v1.9 | 6/6 | Complete    | 2026-10-03 |
| 39. Thèmes Pâle / Classique / Vive | v1.9 | 4/4 | Complete    | 2026-10-03 |
| 40. Cadrans à l'échelle 1 et orientations | v1.9 | 2/8 | In Progress|  |
| 41. Braises | v1.9 | 0/? | Not started | - |
| 42. Un geste sur toute la silhouette | v1.9 | 0/? | Not started | - |
| 43. Release 3.5.0 et constat | v1.9 | 0/? | Not started | - |

### Couverture des exigences

25 requirements v1.9, chacun mappé à exactement une phase, aucun orphelin, aucun doublon (DAT-01 déjà satisfait le
2026-10-03, rattaché à la Phase 37).

| Phase | Requirements | Nombre |
|-------|--------------|--------|
| 36 | SOC-01, SOC-02 | 2 |
| 37 | DAT-01 (Complete), DAT-02, DAT-03, DAT-04, DAT-05 | 5 |
| 38 | HIS-09, HIS-10, HIS-11 | 3 |
| 39 | THM-01, THM-02, THM-03, THM-04 | 4 |
| 40 | CAD-01, CAD-02, CAD-03, CAD-04 | 4 |
| 41 | BRA-01, BRA-02 | 2 |
| 42 | GST-01, GST-02, GST-03 | 3 |
| 43 | VAL-06, VAL-07 | 2 |
| **Total** | | **25 / 25** |
