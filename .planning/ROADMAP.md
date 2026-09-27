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
- 🚧 **v1.8 — Historique d'utilisation** (phases 32-35, en cours, exe cible 3.3.0 ; 3.2.2 intermédiaire après la phase 32) — [exigences](.planning/REQUIREMENTS.md)

## Milestone v1.8 : Historique d'utilisation

### Overview

Le cadran dit **maintenant** ; il ne dit pas **hier**. Un utilisateur intensif de Claude sait, d'un coup d'œil, qu'il lui
reste 40 % de sa semaine — il ne sait pas s'il les a brûlés lundi soir ou mercredi matin, ni si c'est Claude Code ou
Cowork qui les a pris. Ce milestone garde la trace de l'usage du forfait au fil du temps et la montre : **semaine de
forfait** (samedi 00:00 → samedi 00:00, heure locale), **jour**, **quatre semaines** — pour que l'utilisateur comprenne
quand et comment il consomme, sans qu'un seul chiffre soit inventé.

**Deux découvertes priment sur la forme.** Le conseil LLM du 2026-09-26 (`.zeus/reports/llm-council-2026-09-26.md`,
5 membres, 5 relecteurs qui ont vérifié sur la machine) a établi que **le parser de tokens compte 2 à 2,75 fois trop**
— Claude Code écrit une ligne `assistant` par bloc de contenu d'un même message, chacune recopiant le même `usage`
(3 248 lignes pour 1 183 `message.id` sur le plus gros transcript, 0 divergent) — et que **`last-exact.json` n'est plus
écrit depuis le 2026-09-13 12:44** malgré des relevés exacts frais toutes les 5 min (`Save` dans un `try/catch` muet).
La correction par delta de v1.5 est donc **déjà fausse en production**, et la plomberie que le journal généraliserait
est **déjà en panne silencieuse**. On compte juste et on cesse de se taire AVANT d'écrire une ligne d'historique. Le
relevé technique complet (sources, transcripts, persistance, UI existante) est dans `STATE.md`, section « Contexte
technique v1.8 » ; il ne se ré-enquête pas.

**Doctrine (inchangée depuis v1.5, étendue au temps) :** exact ou rien — un pourcentage affiché est un relevé exact du
serveur, jamais une estimation ; **deux séries de nature différente ne se fusionnent jamais** : les relevés exacts du
compte (magnitude, compte entier, Cowork et claude.ai compris) et les tokens des transcripts (attribution, Claude Code
seul) ne partagent ni axe, ni palette, ni vocabulaire, et aucun type de la couche historique n'expose un pourcentage
dérivé de tokens ; **un trou n'est jamais interpolé** — la ligne s'interrompt et le trou porte sa cause journalisée ;
**aucune projection** nulle part ; XAML pur, aucune dépendance native (pas de SQLite : `e_sqlite3.dll`) ; chemins sous
`%APPDATA%\Chronos` ; **lecture seule stricte** de `~/.claude` et de `%APPDATA%\Claude` ; **aucun appel réseau
supplémentaire** — le journal se nourrit de la sonde qui existe.

**Cadre contractuel :** le plan de design `.zeus/DESIGN_PLAN.md` est **validé par l'utilisateur le 2026-09-27**
(maquettes Figma frames A à F). Il fixe la fenêtre (opaque, non topmost, 920 × 610), les trois styles de la vue
Semaine (Pistes A par défaut, Simplifié B, Tuiles C), les pistes et leurs hauteurs (§2.2), le vocabulaire (§4), les
tokens de design et le rendu (`OnRender` + `StreamGeometry`). **Il n'y a donc pas de phase UI-SPEC** : les plans des
phases 34 et 35 le citent comme contrat. Décisions de l'utilisateur : A retenue, B et C codées aussi et sélectionnables ;
vues Jour et 4 semaines conservées ; deux gestes d'ouverture ; la phase « compter juste + journal » se publie **seule en
3.2.2** avant toute interface.

Le milestone se lit en **quatre gestes**, ordonnés par dépendance réelle — c'est le phasage suggéré par le conseil (§5),
et il tient :

1. **Compter juste, puis journaliser** (Phase 32). *Aucune UI.* La dédup par `message.id` d'abord, parce que tout ce qui
   sommera des tokens ensuite (Phase 33) en hérite ; la cause du gel de `last-exact.json` ensuite, parce que le journal
   utiliserait la même plomberie et se tairait de la même façon ; le verrou mono-instance, parce que trois exécutables
   tournaient ensemble le 2026-09-27 et qu'un journal à plusieurs écrivains doit être idempotent. Puis le journal des
   relevés exacts, ses événements de couverture, son âge de dernière écriture en première classe, et sa lecture par plage
   en classes pures. La phase se **publie seule en 3.2.2** : chaque jour sans journal de pourcentages est perdu à jamais
   (ils ne se reconstituent pas). Elle se **termine par le constat en production repris de v1.7** (VAL-04) : point (a)
   une seule instance, réconciliation, tableau des gestes, douze vérifications déférées, et le journal qui s'écrit.
2. **Agrégats de tokens** (Phase 33). La seconde série, sur son propre axe : tranches de 15 min UTC × modèle ×
   principal/sous-agent, quatre compteurs séparés, reconstruction de fond depuis 2,2 Go de transcripts puis incrémental
   par curseurs. Séparée de la 32 parce qu'elle ne bloque pas la 3.2.2 et parce que les curseurs n'existent pas encore
   (`TranscriptSessionSource` fait un `Seek` de queue, rien ne lit par offsets).
3. **Fenêtre Historique : Semaine et Jour** (Phase 34). Les deux vues qui consomment les DEUX journaux, dans les trois
   styles, avec les éléments d'honnêteté testés sur fixtures. Exige la 32 (lecture par plage) et la 33 (agrégats et
   progression de la reconstruction pour le bandeau F2).
4. **4 semaines, accès, release 3.3.0** (Phase 35). La troisième vue (relevés seuls, pas de tokens en v1.8), les deux
   gestes d'ouverture, la section du diagnostic, les docs avec les mots du plan §4, l'exe 3.3.0, et le **constat de la
   3.3.0 avec l'utilisateur** (VAL-05).

**Contraintes portées :** C# / .NET 8 (`net8.0-windows`) / WPF / MVVM (CommunityToolkit.Mvvm) +
Microsoft.Extensions.DependencyInjection + Hosting ; aucune dépendance native ; chemins sous `%USERPROFILE%` /
`%APPDATA%` uniquement, aucun droit admin ; UI et commentaires en français. Les **1200 tests xUnit** restent verts
(suite ≈ 10 s) et chaque phase de code ajoute les siens — horloges injectées, aucun type WPF dans `Services/` ni
`Models/` (`ServicesLayerPurityTests`), `CompositionRootTests`, `GardesPerimetreTests`, `GardesDoctrineTests`. Écriture
atomique (temp + renommage) sur le modèle de `LastExactStore` ; lecture tolérante (fichier absent, ligne invalide,
`v` inconnu → ignoré, jamais de crash). Aucune valeur de couleur ou de taille hors `Resources/DesignTokens.xaml`. Aucun
blocage du thread UI : transcripts et journal se lisent hors UI, marshaling unique. Definition of Done du cycle :
`.zeus/DOD.md`.

**Règles d'observation (constats) :** l'agent ne lance, n'arrête ni ne clique **jamais** l'overlay ; les relevés de
fichiers se font **hors de l'arbre de l'app** (sonde WMI, précédent `29-SONDE-HORS-ARBRE.txt`), **jamais depuis une
session Claude Code** — AppData est virtualisé par MSIX pour tout ce qui tourne sous l'app bureau, et une session
verrait `Packages\Claude_*\LocalCache\Roaming` au lieu de la vue réelle. Ne JAMAIS appeler l'endpoint de refresh OAuth
avec le jeton réel ; ne JAMAIS écrire dans `%APPDATA%\Claude` ni dans `~/.claude/projects`.

**Hors périmètre, rappelé :** SQLite ; double axe Y ; spirale ou horloge comme vue d'historique ; toute projection
« épuisé vers … » ou « à ce rythme » ; reconstitution des pourcentages avant l'ouverture du journal ; tokens Cowork /
claude.ai ; sparkline sur le cadran ; bibliothèque de graphiques tierce. Différé au-delà de v1.8 (v2 de
`REQUIREMENTS.md`) : heatmap jour × heure, export CSV, compaction au-delà de 8 semaines, dimension projet, projection
conditionnelle pointillée, `DayTimeline` sur les resets observés et dérive d'une heure de `WeeklyWindow` (test
d'acceptation le 25/10/2026 avec le journal).

### Phases

**Numérotation des phases :**
- Phases entières (32→35) : travail de milestone planifié — continue après la Phase 31 (v1.7)
- Phases décimales (32.1, 32.2) : insertions urgentes (marquées INSERTED)

- [ ] **Phase 32: Compter juste, puis journaliser** - Le parser dédoublonne par `message.id`, plus aucune écriture ne se tait, une seule instance tourne, un journal des relevés exacts s'écrit toutes les 5 min avec ses événements de couverture ; publié seul en 3.2.2, sans interface, et constaté en production avec l'utilisateur
- [x] **Phase 33: Agrégats de tokens** - Les tokens des transcripts sont agrégés par tranche de 15 min UTC × modèle × sous-agent, reconstruits en arrière-plan depuis 2,2 Go puis tenus à jour par curseurs, justes au changement d'heure, jamais convertis en pourcentage
 (completed 2026-09-27)
- [ ] **Phase 34: Fenêtre Historique : Semaine et Jour** - Une fenêtre opaque séparée montre la semaine de forfait dans trois styles et le jour au grain de 5 min, avec des trous qui restent des trous, sur un rendu `OnRender` gouverné par les tokens de design
- [ ] **Phase 35: 4 semaines, accès, release 3.3.0** - La vue 4 semaines, les deux gestes d'ouverture, la section du diagnostic, les docs, l'exe 3.3.0 publié et le constat avec l'utilisateur

### Phase Details

### Phase 32: Compter juste, puis journaliser
**Goal**: Avant toute ligne d'historique, Chronos **compte juste** (une ligne `assistant` par bloc de contenu ne compte
plus qu'une fois), **ne se tait plus** (la cause du gel de `last-exact.json` est comprise et corrigée, l'âge de chaque
écriture est un chiffre de première classe) et **ne tourne plus en double**. Puis un **journal des relevés exacts**
s'écrit toutes les 5 min avec ses événements de couverture, se lit par plage en classes pures, et se publie **seul en
3.2.2** — le constat en production reporté de v1.7 se joue sur cette version, avec l'utilisateur.
**Depends on**: Rien dans le code de v1.8 — première phase. Reprend la dette VAL-03 de v1.7 (constat partiel,
`31-CONSTAT.md` §4).
**Requirements**: CPT-01, CPT-02, CPT-03, JRN-01, JRN-02, JRN-03, JRN-04, JRN-05, JRN-06, VAL-04
**Success Criteria** (what must be TRUE):
  1. **Trois lignes identiques comptent une fois** : une fixture RÉELLE multi-blocs (trois lignes `assistant` d'un même
     `msg_…`, même `usage`) donne exactement le `usage` d'un message, pas trois ; `TokensDepuisReleve` (correction par
     delta v1.5) en hérite sans code propre ; le repli `requestId` est couvert ; une garde rougit si un lecteur de
     transcripts somme des `usage` sans passer par le helper dédoublonné (CPT-01).
  2. **Plus rien ne se passe en silence — ni écriture ratée, ni doublon de processus** : la cause du gel de
     `last-exact.json` (figé depuis le 2026-09-13 12:44) est écrite, datée, corrigée et épinglée par un test qui
     reproduit la panne ; une écriture qui échoue produit une ligne d'événement et une ligne de diagnostic ; l'âge de
     la dernière écriture des trois magasins (dernier exact, journal des relevés, agrégats de tokens — « aucun » tant
     qu'ils n'existent pas) apparaît au diagnostic. Un second Chronos lancé pendant qu'un premier tourne le dit à
     l'utilisateur (message) et se retire sans jamais tuer l'autre ; le diagnostic compte « N processus Chronos » ;
     les hooks `--hook` et le mode CLI restent multi-instances (CPT-02, CPT-03).
  3. **Le journal s'écrit, ne double pas, ne se tait pas** : après 1 h d'overlay, `%APPDATA%\Chronos\historique\
     releves-2026-09.jsonl` contient ≈ 12 relevés (1 / 5 min) `{v, t, u5, r5, u7, r7, statut5, statut7, overage, source}`
     alors que `RefreshOrchestrator` a resservi le même relevé 5 fois sur 6 (dédup `CapturedAt` strictement croissant) ;
     un plancher ou une valeur de `LastExactStore` n'y entre jamais ; `demarrage`, `arret`, `jeton_invalide`,
     `sonde_refusee`, `reprise` y sont journalisés ; deux écrivains concurrents (200 écritures) ne produisent aucun
     doublon (clé `CapturedAt` + source) ; une ligne tronquée ou un `v` inconnu est sauté ligne par ligne ; fichiers
     mensuels, rétention 24 mois, aucune compaction, aucun type WPF sous `Services/`. Si Chronos tourne et qu'aucune
     écriture n'a eu lieu depuis plus de **15 min**, le diagnostic et les réglages montrent une pastille `Alerte` et le
     texte « journal muet depuis N min » (JRN-01, JRN-02, JRN-03, JRN-04).
  4. **La lecture par plage est pure et testée** : pour une semaine de forfait (bornes issues de `resets_at` 7 j, repli
     `WeeklyAnchor`), un jour ou quatre semaines, des classes sans WPF rendent la série des relevés, les **trous
     (> 2 cadences, soit 10 min)** avec l'événement qui les cause, les resets 5 h et hebdo observés, les Δ de
     consommation entre relevés consécutifs de **même `resets_at`** (jamais à travers un reset) et le saut « non
     localisé » de part et d'autre d'un trou — sur fixtures de journal (trou « arrêté », trou « jeton », reset au milieu
     d'une heure, deux resets hebdo) (JRN-05).
  5. **`Chronos-v3.2.2.exe` est publié sans interface** : version 3.2.2 aux quatre propriétés du csproj et dans le nom
     du fichier ; contrôles et smoke `--hook` de la procédure 31-02 ; `docs/data-sources.md` gagne « Journal
     d'historique » (schéma, dédup, événements, rétention, et les trois hypothèses à vérifier avec le journal :
     granularité des en-têtes, Δ = consommation, reset hebdo à l'heure locale au changement d'heure) ; l'agent ne lance
     rien (JRN-06).
  6. **Constaté en production, avec l'utilisateur** — protocole de `31-CONSTAT.md` inchangé, verdicts écrits dans
     `32-CONSTAT.md`, écarts compris : point (a) 3.1.0 et 3.2.0 quittées, 3.2.2 lancée par l'Explorateur, une seule
     instance constatée (CPT-03 en vrai) ; réconciliation hooks/statusLine constatée dans `~/.claude/settings.json` ;
     tableau des gestes L1, L2, L2b, L3, L4, Q ; les 12 vérifications déférées ; et **le journal s'écrit** (âge < 6 min
     après 10 min d'overlay, relevé par sonde WMI hors de l'arbre, jamais depuis une session) (VAL-04).
**Plans**: 8 plans en 4 vagues (vague 1 : 32-01 ∥ 32-02 ∥ 32-03 ∥ 32-04 — fichiers disjoints ; vague 2 : 32-05 ∥ 32-06 ;
vague 3 : 32-07 ; vague 4 : 32-08, point de contrôle humain, `autonomous: false`). Planifié le 2026-09-27 : la recherche de phase a établi
que le « gel » de `last-exact.json` est un artefact de la vue virtualisée MSIX (CPT-02 = observabilité + « Vue AppData » au diagnostic),
que la dédup est « max par champ par `message.id` » (streaming `output_tokens`) et que `FileMode.Append` n'est pas atomique (append sous
`FileShare.None` + relecture de queue). Le décorateur de journalisation et ses événements sont construits NON BRANCHÉS en 32-04 ; 32-05 câble.

Plans:
- [x] 32-01-PLAN.md — CPT-01 : `DedupUsage` (max par champ par `message.id`, repli `requestId`, dictionnaire global à la passe), fixture réelle multi-blocs (8/8/256), `TokensDepuisReleve` en héritage, garde « aucun lecteur de `usage` hors du helper »
- [x] 32-02-PLAN.md — CPT-02 : le « gel » était la vue virtualisée MSIX (cause datée dans STATE.md et le SUMMARY) ; `IEtatMagasin`, `LastExactStore` observable (`DerniereEcriture`, `DerniereErreur`, `EcritureRatee`), `ChronosPaths.HistoriqueDir`, section `[Magasins persistants]` + « Vue AppData : réelle | virtualisée » au diagnostic, test qui reproduit la panne
- [x] 32-03-PLAN.md — CPT-03 : `VerrouInstanceUnique` (mutex `Local\Chronos-overlay`, abandon = acquis), `InventaireProcessus` (« N processus Chronos » par préfixe), mutex avant le Host dans `App.xaml.cs`, message et retrait, `OnExit` tolérant, garde textuelle ; `--hook` et CLI exemptés
- [x] 32-04-PLAN.md — JRN-01, JRN-02, JRN-03 : types neutres `Models/Historique`, `LigneJournal`, `JournalReleves` (append exclusif idempotent `(t, source)`, mois UTC, rétention 24 mois, test à deux écrivains), `LecteurJournal.LireFichier` tolérant, `JournalisationUsageProvider` (décorateur + hosted service : dédup `CapturedAt` strictement croissant, exclusions, `demarrage`/`arret`/`jeton_invalide`/`sonde_refusee`/`reprise`/`ecriture_ratee`) — NON BRANCHÉ ; quatre gardes élargies aux sous-dossiers
- [x] 32-05-PLAN.md — JRN-04 (+ câblage) : DI — décorateur entre `LastExactUsageProvider` et le composite, hosted service avant l'orchestrateur, `EcritureRatee` → `ecriture_ratee`, magasins au diagnostic ; « journal muet depuis N min » (15 min depuis max(démarrage, dernière écriture)), processus et verrou au diagnostic ; carte « Journal des relevés » + pastille `Alerte` dans les réglages
- [x] 32-06-PLAN.md — JRN-05 : `LecteurJournal.Lire` par plage (mois chevauchants, `JournalOuvertLe`), `BornesPlage` (semaine de forfait samedi 00:00 local via `resets_at` 7 j / `WeeklyAnchor`, jour, quatre semaines, DST 169 h / 167 h), `AnalyseReleves` pur (série, trous > 2 cadences avec cause, resets observés, Δ de même `resets_at`, saut non localisé) ; 7 fixtures de journal
- [x] 32-07-PLAN.md — JRN-06 : `docs/data-sources.md` §7 « Journal d'historique » (schéma, dédup, événements, rétention, deux vues, HYP-1/2/3) sous garde documentaire, `docs/publish.md` §7 verrou ; csproj 3.2.2 × 4, `dotnet publish`, `Chronos-v3.2.2.exe`, contrôles, smoke `--hook`, commit de release — s'arrête avant tout lancement
- [ ] 32-08-PLAN.md — VAL-04 : constat en production avec l'utilisateur (trois anciennes quittées, 3.2.2 lancée par l'Explorateur, second lancement refusé, réconciliation, tableau L1…Q, V01…V12, journal T+10 âge < 6 min et T+60 ≈ 12 relevés par sonde WMI hors arbre) → `32-CONSTAT.md`, `32-VALIDATION.md` signée — `autonomous: false`

### Phase 33: Agrégats de tokens
**Goal**: La seconde série — **les tokens de Claude Code, sur leur propre axe** — existe : chaque transcript (principal
et `subagents/`) est agrégé par tranche de 15 min UTC × modèle × principal/sous-agent en quatre compteurs séparés,
**reconstruit en arrière-plan** depuis les 2,2 Go existants sans jamais bloquer l'UI, puis **tenu à jour par
curseurs** ; les tranches se rendent en heure locale juste au changement d'heure, et aucun type ne peut en faire un
pourcentage.
**Depends on**: Phase 32 — le helper dédoublonné par `message.id` (CPT-01) est le seul point d'entrée autorisé pour
sommer des `usage` ; l'âge de dernière écriture des magasins (CPT-02) accueille celui des agrégats.
**Requirements**: TOK-01, TOK-02, TOK-03, TOK-04, TOK-05
**Success Criteria** (what must be TRUE):
  1. **Une ligne par (tranche, modèle, origine), quatre compteurs, jamais le contenu** :
     `%APPDATA%\Chronos\historique\tokens-AAAA-MM.jsonl` contient `{v, slot, model, sub, in, out, cache_w, cache_r, n}`
     — `slot` = début de tranche de 15 min en UTC, `sub` vrai pour un fichier `subagents/agent-*.jsonl` ; jamais la
     somme des quatre, jamais un message individuel, jamais un extrait de texte ; écriture atomique, lecture tolérante,
     réécriture (pas ajout) des tranches d'un fichier réingéré (TOK-01).
  2. **Le passé se reconstruit sans que rien ne bloque** : sur la vraie machine (1 565 fichiers, 2,2 Go, plus gros
     53 Mo), la reconstruction court sur un thread `IsBackground` en priorité `BelowNormal`, fichiers par mtime
     décroissant (la **semaine courante est disponible avant l'historique**), lecture en flux `FileShare.ReadWrite` avec
     pré-filtre texte avant tout `JsonDocument`, dédup `message.id` par fichier via le helper de CPT-01, `Task.Yield`
     entre fichiers, annulable à l'arrêt ; la progression « N / M fichiers » est exposée au ViewModel ; le coût CPU total
     est mesuré et consigné (estimation d'entrée : 10–20 s chaud, étalés) (TOK-02).
  3. **Seul ce qui a changé se relit** : `curseurs.json` `{chemin → offset de la dernière ligne complète, taille,
     mtime}` ; un fichier dont (taille, mtime) n'a pas bougé n'est pas ouvert ; un fichier qui a grandi est relu depuis
     son offset ; un fichier raccourci ou renommé est réingéré de zéro et ses tranches réécrites ; un arrêt au milieu
     de la reconstruction puis une reprise produisent **les mêmes agrégats** qu'une passe ininterrompue — tous testés
     (TOK-03).
  4. **L'heure locale est juste aux changements d'heure** : la journée du **25/10/2026 compte 25 h** et celle du
     **28/03/2027 en compte 23**, testé sur les tranches UTC rendues en `Europe/Paris` ; les tranches antérieures au plus
     vieux transcript sont « hors couverture », un mois purgé par Claude Code (juillet 2026) est « transcripts
     absents », et ni l'un ni l'autre n'est « zéro token » (TOK-04).
  5. **Aucun pourcentage ne peut naître des tokens** : une garde structurelle rougit si un type de la couche
     historique expose un `double` de quota calculé à partir de tokens ; le schéma et `docs/data-sources.md` écrivent le
     périmètre partiel (« Claude Code seulement — hors Cowork et claude.ai ; bruts, non pondérés ») ;
     `ServicesLayerPurityTests` reste vert (TOK-05).
**Plans**: 5 plans en 3 vagues (vague 1 : 33-01 ∥ 33-02 — magasin et lecteur, fichiers disjoints ; vague 2 : 33-03 ∥
33-04 ; vague 3 : 33-05). Point d'attention pour le plan : le coût de la reconstruction et de l'incrémental se
**mesure** sur la vraie machine (2,2 Go), il ne se suppose pas ; le magasin est écrit tranche par tranche à partir de
la première semaine terminée, pas en un seul bloc à la fin.

Plans:
- [x] 33-01-PLAN.md — TOK-01 : `TrancheTokens`/`DeltaTranche`, `LigneAgregat` (9 champs, `slot` « O » UTC, parsing tolérant), `MagasinAgregats` (réécriture atomique triée, `IEtatMagasin`, rétention alignée sur le journal), `CouvertureTokens` (intervalles garantis persistés, `HorizonPurge` 30 j), garde TOK-05 réflexive + textuelle avec contrôle positif
- [x] 33-02-PLAN.md — TOK-03 : 12 fixtures réelles anonymisées, `LecteurTranscript` (octet, offsets, pré-filtre, `sub` par dossier, ligne future = curseur bloqué), `DedupUsage.Fusionner`, `IndexMessages` (shards `ids-AAAA-MM.jsonl` append-only, delta par id, `HorizonIndex` 45 j), `Curseurs` (nouveau / inchangé / grandi / raccourci / disparu)
- [x] 33-03-PLAN.md — TOK-02 : `IEtatReconstruction`, `ProjectionAgregats`, `ReconstructionTokens` (`BackgroundService` + thread dédié `IsBackground` `BelowNormal`, `Thread.Yield()`, mtime décroissant, semaine courante d'abord, flush ids → agrégats → curseurs, incrémental 60 s) ; reprise idempotente octet pour octet, annulation < 200 ms
- [x] 33-04-PLAN.md — TOK-04 : `LecteurAgregats.Lire` par plage (mois UTC chevauchants, couverture en sous-plages), `RenduLocalTokens` (25 barres le 25/10/2026, 23 le 28/03/2027, colonnes par quart d'heure et par modèle, part sous-agents)
- [x] 33-05-PLAN.md — TOK-05 : câblage DI (hébergé avant `RefreshOrchestrator`, arrêt propre), diagnostic (troisième magasin + `[Agrégats de tokens]`), `MainViewModel` (propriétés de reconstruction, sans XAML), `docs/data-sources.md` §8 sous la nouvelle garde `ContratAgregatsDocumenteTests`, mesures consignées

### Phase 34: Fenêtre Historique : Semaine et Jour
**Goal**: Une **fenêtre « Historique »** séparée — opaque, non topmost, redimensionnable, mémorisée — montre la
**semaine de forfait** dans les trois styles du plan de design (Pistes, Simplifié, Tuiles) et le **jour** au grain de
5 min, en consommant les deux journaux sur deux axes distincts ; un trou reste un trou, un saut pendant l'absence
reste « répartition inconnue », une marche sans tokens Code dit « consommé ailleurs » ; le rendu est un `OnRender` par
piste gouverné par `DesignTokens.xaml`, et le bandeau F2 dit où en est la reconstruction.
**Depends on**: Phase 32 (lecture par plage du journal, JRN-05 ; âge de dernière écriture, JRN-04) et Phase 33
(lecture par plage des agrégats, TOK-04 ; progression N / M de la reconstruction, TOK-02). **Contrat :**
`.zeus/DESIGN_PLAN.md` validé le 2026-09-27 — pas de phase UI-SPEC ; chaque plan le cite (tokens §2, pistes et
hauteurs §2.2, Jour §2.3, vocabulaire §4).
**Requirements**: HIS-01, HIS-02, HIS-03, HIS-04, HIS-06, HIS-07, HIS-08
**Success Criteria** (what must be TRUE):
  1. **Une fenêtre de consultation, pas un overlay** : `HistoriqueWindow` en `WindowStyle=None`,
     `AllowsTransparency=False`, `Topmost=False`, `ShowInTaskbar=True`, coins 16, redimensionnable de 760 × 480 à plus
     de 1 400 px sans chevauchement, 920 × 610 par défaut ; position et taille relues depuis `settings.json` à la
     réouverture ; en-tête commun (segment Jour / Semaine / 4 semaines, ‹ ›, « Cette semaine » / « Aujourd'hui »,
     ligne de fraîcheur « dernier relevé il y a N min · source · N relevés · N interruptions · journal ouvert le … »,
     pastille `Alerte` « journal muet depuis N min » au-delà de 15 min) ; Échap ferme (HIS-01).
  2. **La semaine de forfait en style Pistes** : X = samedi 00:00 → samedi 00:00 heure locale, bornes prises dans le
     `resets_at` 7 j du journal (repli `WeeklyAnchor`), grille et libellés « sam. 19 … ven. 25 » ; NIVEAU 150 px
     (hebdo en escalier 2,2 px coloré par la rampe du thème, coupé aux trous ; dents de scie 5 h 1 px `TickReset` ;
     tirets de reset 5 h observés ; semaine précédente en escalier gris pointillé), RYTHME 72 px (barres horaires du
     Δ 5 h de même `resets_at`, couleur = rampe au niveau atteint, échelle 0 – 25 %), TOKENS CLAUDE CODE 72 px
     (`HistoTokens` + part `HistoSousAgent` empilée, axe propre), COUVERTURE 12 px (`Ok` 55 % présent, `Line` arrêté,
     ambre jeton ou sonde refusée) ; réticule vertical commun au survol avec infobulle (heure, valeur, source, âge)
     (HIS-02).
  3. **Trois styles, un réglage** : `HistoriqueStyleSemaine` ∈ { Pistes, Simplifie, Tuiles } persisté dans
     `settings.json`, sélecteur à droite de la ligne de fraîcheur ; **Simplifié** = NIVEAU 200 + TOKENS 90 + COUVERTURE
     12, sans Rythme ; **Tuiles** = NIVEAU 120 (hebdo seul) + FENÊTRES 5 H 62 (une tuile du premier relevé actif au
     `resets_at` observé, hauteur = max % 5 h, couleur = rampe, `HistoGris` si 100 % « épuisée », tiret à droite) +
     RYTHME 58 + TOKENS 58 + COUVERTURE 12 — exactement les pistes et hauteurs du plan §2.2, vérifiées par test (HIS-03).
  4. **Le jour au grain de 5 min** : X = 0 h → 24 h locale, grille toutes les 3 h ; NIVEAU 190 px avec le % 5 h au
     premier plan (2,4 px, couleur = niveau, **gris à 100 %** avec « épuisée à 100 % — le serveur refuse (statut
     rejected) »), % hebdo en trait fin `Ink2`, trait `TickReset` et « reset 5 h HH:MM » à chaque reset observé ;
     RYTHME 64 px par heure ; TOKENS 64 px par quart d'heure empilés par modèle (`HistoModele1/2/3`, légende « opus ·
     sonnet · haiku · sous-agents inclus ») ; COUVERTURE 12 px ; ligne « maintenant » `Ink` 60 % si le jour est
     aujourd'hui ; ligne de fraîcheur « 288 relevés attendus · N présents · N interruption(s) (cause, HH:MM → HH:MM) »
     (HIS-04).
  5. **L'honnêteté est testée, mot pour mot** : sur fixtures, un trou (> 2 cadences) interrompt la ligne et se dessine
     en rectangle `Line` 35 % à bordure pointillée — grise « Chronos arrêté », ambre « jeton invalide » ; le saut de part
     et d'autre d'un trou est un bloc plat gris « +N % pendant l'absence (répartition inconnue) », jamais une barre au
     réveil ; une marche de % sans tokens Code est encadrée en pointillé `Accent` et le pied de page dit « consommé
     ailleurs (Cowork, claude.ai) » ; la zone antérieure au journal est vide avec « journal ouvert le <date> » ; le
     libellé permanent des tokens (« par heure, comptés localement — hors Cowork et claude.ai · bruts, non pondérés · ce
     n'est PAS un % du forfait ») et le pied de page fixe (« Aucun trou n'est interpolé … ») sont présents sur chaque
     vue ; aucune projection nulle part, tenu par une garde sur le vocabulaire (HIS-06).
  6. **Un rendu qui ne coûte rien au tick** : un `FrameworkElement` par piste avec `OnRender` et `StreamGeometry`
     gelée ; réduction min/max par colonne de pixels au-delà de 4 000 points ; redessin sur changement de données
     (5 min) ou de plage, **jamais sur le tick 1 s** (test) ; toute la géométrie (temps → x, valeur → y, binning,
     trous, tuiles, segments par bande de rampe) en classes pures de `Rendering/` testées comme `ArcGeometry` ; la
     palette de `SettingsWindow` (`Panel`, `Panel2`, `Line`, `Ink`, `Ink2`, `Accent`, `Ok`) est promue dans
     `Resources/DesignTokens.xaml` **sans changer une valeur**, avec les nouveaux tokens `HistoTokens`, `HistoSousAgent`,
     `HistoModele1/2/3`, `HistoGris` ; aucune couleur ni taille en dur ; `ServicesLayerPurityTests` vert. Tant que la
     reconstruction court, le **bandeau F2** (« Reconstruction des tokens depuis vos transcripts Claude Code — N / M
     fichiers · la semaine courante est déjà complète », barre `Accent`, sous-texte sur le non-recalcul des
     pourcentages) est visible ; il disparaît à la fin (HIS-07, HIS-08).
**Plans**: 8 plans en 4 vagues (vague 1 : 34-01 ∥ 34-02 ∥ 34-03 — tokens, géométrie, ViewModel : fichiers disjoints ;
vague 2 : 34-04 ∥ 34-05 ; vague 3 : 34-06 ∥ 34-07 ; vague 4 : 34-08). Point d'attention pour le plan : la fenêtre n'a
pas encore de geste d'ouverture (Phase 35) — la **galerie `--historique`** sur fixtures (même mécanisme que `--sessions`,
sans réconciliation des hooks) sert à la revue visuelle par l'utilisateur (DESIGN_PLAN §8) ; l'agent ne la lance pas.

Plans:
- [x] 34-01-PLAN.md — HIS-07 (contribue) : palette `Panel/Panel2/Line/Ink/Ink2/Accent/Ok` promue dans `DesignTokens.xaml` sans changer une valeur (SettingsWindow fusionne par pack URI), tokens `Histo*` (+ `HistoGris #5A5960`, `HistoHachure`), 38 tokens de taille `sys:Double`, garde « aucune valeur en dur »
- [x] 34-02-PLAN.md — HIS-02 (contribue) : géométrie pure `Rendering/Historique` — `EchelleTemps`, `EchelleValeur`, `Escalier` (paliers coupés aux trous, bandes de rampe, rectangles de trous, blocs de sauts), `Binning` (Δ/h, min/max > 4 000), `Tuiles5h`, `Reticule`
- [x] 34-03-PLAN.md — HIS-08 : réglages (`HistoriqueStyleSemaine`, géométrie), `Divergences` (seuil 0,01 nommé), `GraduationsCalendrier`, `ISourceHistorique`/`SourceHistoriqueDisque`, `TextesHistorique` (mots §4), `ScenariosHistorique` (semaine des maquettes), `HistoriqueViewModel` (lecture hors UI, tick 60 s sans relecture, F2 coalescé)
- [ ] 34-04-PLAN.md — HIS-07 : six `FrameworkElement` à `OnRender` + `StreamGeometry` gelée (`PisteBase` avec compteur de rendus, Niveau, Rythme, Tokens, Couverture, Fenêtres 5 h, `SurcoucheReticule`), test « jamais au tick » niveau contrôle
- [ ] 34-05-PLAN.md — HIS-01 : `HistoriqueWindow` (WindowChrome, coins DWM best-effort, en-tête §2.1 avec « 4 semaines » désactivé, F2, Échap, géométrie bornée et persistée), stubs `VueSemaineView`/`VueJourView`, mode `--historique`, DI + miroir + garde
- [ ] 34-06-PLAN.md — HIS-02, HIS-03 : vue Semaine — trois grilles Pistes / Simplifié / Tuiles aux hauteurs 150/72/72/12 · 200/90/12 · 120/62/58/58/12 vérifiées par Measure/Arrange, annotations d'honnêteté, zone avant journal, surcouche + infobulle, 760 → 1 400
- [ ] 34-07-PLAN.md — HIS-04 : vue Jour — 190/64/64/12, % 5 h au premier plan (gris si épuisée), hebdo trait fin, resets observés, tokens par quart d'heure par modèle + légende, « maintenant » aujourd'hui seulement
- [ ] 34-08-PLAN.md — HIS-06 : honnêteté de bout en bout sur la galerie (2 vues × 3 styles, mots + trace de rendu), garde de vocabulaire « aucune projection », garde tokens durcie, mutations h1–h5, GATE TESTS (suite × 2, Release 0 warning)

### Phase 35: 4 semaines, accès, release 3.3.0
**Goal**: La **vue 4 semaines** compare quatre semaines de forfait sur le même axe et dit lesquelles sont antérieures au
journal ; la fenêtre s'ouvre par **deux gestes** (carte des réglages, double-clic au centre du cadran) sans casser le
simple clic ; le **diagnostic** a sa section « Journal d'historique » ; README et `docs/data-sources.md` parlent avec les
mots du plan §4 ; l'exe **3.3.0** est publié, et l'utilisateur **constate** sur sa machine ce que le milestone promettait.
**Depends on**: Phase 34 (fenêtre, pistes, ViewModel, tokens de design) ; Phases 32 et 33 pour les données du
diagnostic (âge de dernière écriture, événements, progression N / M). **Contrat :** `.zeus/DESIGN_PLAN.md` §2.4
(4 semaines), §5 (gestes), §6 (carte F1).
**Requirements**: HIS-05, ACC-01, ACC-02, ACC-03, ACC-04, VAL-05
**Success Criteria** (what must be TRUE):
  1. **Quatre semaines sur un même axe, rien d'inventé avant le journal** : X = samedi → samedi (sept colonnes) ;
     courbe hebdo de la semaine courante en couleur (rampe), S-1 / S-2 / S-3 en `HistoGris` aux opacités 0,8 / 0,45 /
     0,25, étiquettes à droite (libellé de semaine + valeur finale, ou « pas de relevés (avant le journal) ») ; une
     semaine épuisée montre un plateau gris à 100 % annoté « épuisée <jour> HH:MM → bloquée jusqu'au reset » ; bande
     COUVERTURE PAR SEMAINE à quatre rangées ; marqueur « journal ouvert <date> » sur la semaine d'ouverture ; pas de
     piste tokens ; pied « Rien n'est inventé avant l'ouverture du journal. » (HIS-05).
  2. **Deux gestes, aucun dégât collatéral** : dans la section DONNÉES des réglages, la carte « Historique
     d'utilisation » (bouton « Ouvrir », sous-texte « hebdo / 5 h / tokens · journal du <date> · dernière écriture il y a
     N min », sélecteur Pistes · Simplifié · Tuiles synchronisé avec celui de la fenêtre, mention du double-clic, carte
     d'état « Dernière écriture du journal » avec l'alerte > 15 min) ; un **double-clic au centre du cadran** ouvre ou
     ramène la fenêtre au premier plan **sans** déclencher la bascule % / temps du simple clic (test avec horloge
     injectée), et un simple clic la déclenche toujours ; le drag et le clic droit sont inchangés (ACC-01, ACC-02).
  3. **Le diagnostic dit tout ce que le journal sait de lui-même** : section « Journal d'historique » — chemins des
     fichiers, âge de la dernière écriture, relevés du jour (N / 288), événements récents, état de la reconstruction des
     tokens (N / M ou « terminée »), taille des fichiers, nombre d'instances Chronos (ACC-03).
  4. **`Chronos-v3.3.0.exe` est publié et documenté** : version 3.3.0 aux quatre propriétés du csproj et dans le nom du
     fichier, contrôles et smoke `--hook`, réconciliation au premier lancement constatée dans `~/.claude/settings.json` ;
     README et `docs/data-sources.md` décrivent la fenêtre, les trois styles, les trois vues et les règles d'honnêteté
     avec le vocabulaire du plan §4, sous la garde de vocabulaire de la Phase 34 (ACC-04).
  5. **Constaté avec l'utilisateur, verdict écrit** (`35-CONSTAT.md`) : la fenêtre s'ouvre par les deux gestes ; la
     semaine courante affiche les relevés depuis l'ouverture du journal (3.2.2, phase 32) et les tokens reconstruits ;
     les trois styles se sélectionnent ; un trou réel (PC éteint la nuit) est hachuré et annoté avec sa cause ;
     l'utilisateur relit les libellés d'honnêteté et rend un verdict, écarts compris (VAL-05).
**Plans**: 5 plans en 3 vagues (vague 1 : 35-01 ∥ 35-02 ∥ 35-03 — vue, gestes, diagnostic : fichiers disjoints ;
vague 2 : 35-04 ; vague 3 : 35-05, point de contrôle humain, `autonomous: false`).

Plans:
- [ ] 35-01-PLAN.md — HIS-05 : vue 4 semaines — quatre courbes superposées (opacités 0,8 / 0,45 / 0,25), étiquettes à droite, plateau gris « épuisée … », couverture par semaine à quatre rangées, semaines antérieures au journal vides et dites telles
- [ ] 35-02-PLAN.md — ACC-01, ACC-02 : carte F1 « Historique d'utilisation » dans DONNÉES (Ouvrir, sous-texte, sélecteur de style, mention, carte d'état > 15 min) ; double-clic `CentreHit` sans double bascule, drag et clic droit inchangés
- [ ] 35-03-PLAN.md — ACC-03 : section « Journal d'historique » du diagnostic (chemins, âge, relevés du jour, événements, reconstruction N / M, tailles, instances) — même lecture que la fenêtre
- [ ] 35-04-PLAN.md — ACC-04 : README et `docs/data-sources.md` avec les mots §4 ; csproj 3.3.0 × 4, `dotnet publish`, `Chronos-v3.3.0.exe`, contrôles, smoke `--hook`, commit de release ; s'arrête avant tout lancement
- [ ] 35-05-PLAN.md — VAL-05 : constat de la 3.3.0 avec l'utilisateur (deux gestes, semaine courante, trois styles, trou réel annoté, libellés relus) → `35-CONSTAT.md` — `autonomous: false`
**UI hint**: yes

### Progress

**Execution Order:**
Phase 32 (compter juste, journal, 3.2.2, constat repris de v1.7) → Phase 33 (agrégats de tokens, exige le helper
dédoublonné de la 32) → Phase 34 (fenêtre Semaine et Jour, exige les deux lectures par plage et la progression de la
reconstruction) → Phase 35 (4 semaines, accès, diagnostic, 3.3.0, constat).
Strictement séquentiel : chaque phase consomme la précédente. Seul degré de liberté : la vague 1 de la Phase 33 peut
commencer pendant que la Phase 32 attend son constat (32-08), le constat ne touchant aucun fichier de code.

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 32. Compter juste, puis journaliser | 7/8 | In Progress|  |
| 33. Agrégats de tokens | 5/5 | Complete    | 2026-09-27 |
| 34. Fenêtre Historique : Semaine et Jour | 3/8 | In Progress|  |
| 35. 4 semaines, accès, release 3.3.0 | 0/5 | Not started | - |

### Couverture des exigences

28 requirements v1.8, chacun mappé à exactement une phase, aucun orphelin, aucun doublon.

| Phase | Requirements | Nombre |
|-------|--------------|--------|
| 32 | CPT-01, CPT-02, CPT-03, JRN-01, JRN-02, JRN-03, JRN-04, JRN-05, JRN-06, VAL-04 | 10 |
| 33 | TOK-01, TOK-02, TOK-03, TOK-04, TOK-05 | 5 |
| 34 | HIS-01, HIS-02, HIS-03, HIS-04, HIS-06, HIS-07, HIS-08 | 7 |
| 35 | HIS-05, ACC-01, ACC-02, ACC-03, ACC-04, VAL-05 | 6 |
| **Total** | | **28 / 28** |
