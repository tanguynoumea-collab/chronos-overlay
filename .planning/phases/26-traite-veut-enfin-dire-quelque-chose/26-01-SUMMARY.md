---
phase: 26-traite-veut-enfin-dire-quelque-chose
plan: 01
subsystem: sessions
tags: [hysteresis, arbitrage, source-du-signal, horloge-injectee, retention-de-fichier, tdd, xunit]

sha_entree_de_phase: 07ee784
sha_entree_de_plan: 030e7b8

requires:
  - phase: 24-l-arbitrage-par-fraicheur
    provides: "ArbitrageSessions.Trancher et SignalSession — la source existait déjà, elle n'atteignait simplement pas le détecteur"
  - phase: 25-le-contrat-d-evenements-refonde
    provides: "SessionActivity.WaitingDeduced et SilenceDesBattements — l'attente déduite que le prédicat du détecteur ignorait"
  - phase: 23-le-cycle-de-vie-du-magasin
    provides: "IClock / SystemClock et le FakeClock des tests — l'horloge injectable que TreatedStore n'avait pas"
provides:
  - "ResultatArbitrage.Vainqueurs — les retenus AVEC leur source, index pour index"
  - "SessionTreatmentTracker.Observe(IReadOnlyList<SignalSession>, now) — le détecteur sait QUI a parlé"
  - "NET-01 refondée : une transition observée sur la MÊME source, vers un travail OBSERVÉ, ou rien (TRT-01)"
  - "L'épisode d'attente daté par l'instant que le SIGNAL porte — le « traité » survit à un redémarrage (TRT-02)"
  - "TreatedStore à horloge INJECTÉE, et RetentionMax 24 h à la place d'une durée de vie de six heures (point b bis)"
  - "Le scénario mesuré des 478 minutes, rejoué bout en bout avec ses chiffres relevés"
affects: [26-02-le-contrat-d-archivage, 26-03-le-geste-explicite, 26-04-le-contrat-documente]

tech-stack:
  added: []
  patterns:
    - "Un détecteur de transition doit connaître la SOURCE de chaque signal : sans elle, « attente puis travail » ne distingue pas une réponse d'un relais entre deux sources"
    - "Un épisode se date par l'instant que le SIGNAL porte, borné par l'instant courant — jamais par l'horloge du guetteur : c'est ce qui le fait survivre à un redémarrage"
    - "Une borne de durée dans un magasin est une RÉTENTION DE FICHIER, jamais un délai d'affichage — et elle doit être strictement supérieure au seuil au-delà duquel la donnée n'est de toute façon plus lue"
    - "Deux séquences parallèles (Retenus / Vainqueurs) ne se tiennent que si un test exige leur égalité ET leur non-vacuité"
    - "Une garde qui comparerait deux listes vides est muette : Assert.NotEmpty avant Assert.Equal"

key-files:
  created: []
  modified:
    - src/Chronos/Services/ArbitrageSessions.cs
    - src/Chronos/Services/TreatedStore.cs
    - src/Chronos/Services/SessionTreatmentTracker.cs
    - src/Chronos/Services/SessionMonitor.cs
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/ArbitrageSessionsTests.cs
    - tests/Chronos.Tests/TreatedSessionsTests.cs

key-decisions:
  - "Le point (b bis) est livré : Ttl = FromHours(6) devient RetentionMax = FromHours(24). Depuis TRT-02 la valeur mémorisée est l'instant du SIGNAL ; une borne de six heures adossée à cet instant rendait le magasin aveugle à toute session attendant depuis plus de six heures — c'est-à-dire e465420e elle-même, et deux heures pleines où « marquer traitée » était un no-op silencieux."
  - "CompositionRootTests n'est PAS touché : le paramètre d'horloge est OPTIONNEL, le miroir garde la RÉSOLUTION du graphe et non la liste des arguments de chaque fabrique. Aucun test ajouté, conformément au point (f) du plan."
  - "Cli reste inchangé à côté du nouvel helper Sig : il alimente MutableSource.Snaps en SessionSnapshot, le supprimer aurait cassé huit sites."
  - "La tâche 1 est un seul commit et non un cycle rouge/vert : le seul test ajouté (Les_vainqueurs_sont_les_retenus_avec_leur_source) ne peut pas exister avant le membre qu'il lit — un commit rouge y serait un commit qui ne compile pas, ce que les règles dures interdisent. 26-VALIDATION.md l'annonce d'ailleurs « vert dès son écriture »."
  - "TreatedStore_set_load_remove_et_purge_TTL n'est PAS renommé, conformément au plan : un second renommage non annoncé serait une déviation de plus à justifier."

requirements-completed: [TRT-01, TRT-02]

metrics:
  duration: "~35 min"
  completed: 2026-09-13
---

# Phase 26 Plan 01 : « Traité » ne se déduit plus que d'une transition observée — Summary

**Le détecteur de traitement sait désormais QUI a parlé : NET-01 exige la même source aux deux cycles et un
travail OBSERVÉ, l'épisode d'attente est daté par l'instant que le signal porte (et survit donc à un
redémarrage), l'attente déduite est enfin une attente — et le masquage de six heures du 2026-09-12, rejoué
avec ses chiffres relevés, ne se produit plus.**

## Performance

- **Duration :** ~35 min
- **Started :** 2026-09-12T21:50Z
- **Completed :** 2026-09-12T22:25Z
- **Tasks :** 3/3
- **Files modified :** 7

## La chaîne de totaux, mesurée

| Étape | Commande | Total | Échecs | Durée |
|---|---|---|---|---|
| Entrée de plan (baseline) | `dotnet test Chronos.sln -c Debug --nologo -v q` | **868** | 0 | 5 s |
| Fin tâche 1 | idem | **869** (+1) | 0 | 5 s |
| Fin tâche 2 (ROUGE) | idem | **876** (+7) | **6** | 5 s |
| Fin tâche 3 (VERT) | idem | **876** (+0) | **0** | 5 s |
| Vérification, 1re exécution | idem | **876** | 0 | 6 s |
| Vérification, 2e exécution | idem | **876** | 0 | 6 s |

Chaîne annoncée 869 → 876 → 876 : **tenue exactement**, aucun écart à justifier.

## Les étapes ROUGE, mesurées et nommées

`dotnet test Chronos.sln -c Debug --nologo --filter "FullyQualifiedName~TreatedSessions"` à la fin de la
tâche 2 — **build 0 erreur, 0 avertissement** (une phase rouge est une phase de tests qui échouent, jamais
d'un build cassé) :

```
Échoué!  - échec : 6, réussite : 9, ignorée(s) : 0, total : 15
```

**15 = 8 préexistants + 7 ajoutés.** Liste EXACTE des six échecs, telle que sortie du runner :

| # | Test rouge en T2 | Ligne | Pourquoi il était rouge |
|---|---|---|---|
| 1 | `Une_bascule_de_source_ne_marque_rien` | 222 | `wasWaiting=true`, `isWaiting=false` → entrée posée alors que seule la source avait changé |
| 2 | `Une_attente_deduite_ouvre_un_episode` | 241 | `IsWaiting` ignorait `WaitingDeduced` → aucun épisode ouvert → rien posé |
| 3 | `Une_attente_qui_devient_deduite_n_est_pas_traitee` | 261 | déduite lue comme non-attente → session marquée traitée alors qu'elle attend |
| 4 | `Un_etat_inconnu_n_affirme_pas_qu_on_a_repondu` | 279 | `Unknown` traité comme une non-attente → conclusion tirée d'une absence de lecture |
| 5 | `Le_traite_survit_au_redemarrage_mais_pas_a_une_nouvelle_demande` | 300 | tracker neuf → épisode redaté à `now` → NET-03 purgeait le magasin qu'il venait de lire |
| 6 | `Le_scenario_mesure_des_478_minutes_laisse_la_session_visible` | 371 | le hook franchit `DropAfter` au cycle 2, le transcript reprend la main, NET-01 conclut « répondu » |

**Le septième cas, `Un_episode_reellement_plus_recent_purge_toujours`, est VERT dès son écriture** et
annoncé comme tel : c'est la non-régression de NET-03 (la réversibilité ne doit pas être sacrifiée à la
persistance). Il n'entre pas dans le compte des rouges. **6 rouges annoncés, 6 rouges mesurés.**

Après la tâche 3 : `--filter "FullyQualifiedName~TreatedSessions"` → **15/15 verts, 0 `[FAIL]`**. Les huit
préexistants — dont `NET01_repondu_marque_traitee`, `NET03_reapparition_purge_l_entree`,
`NET01_le_monitor_masque_apres_reponse` et `NET03_le_monitor_la_reaffiche_sur_nouvel_episode` — sont restés
verts de bout en bout.

## Le scénario mesuré, rejoué à l'identique

`Le_scenario_mesure_des_478_minutes_laisse_la_session_visible` rejoue la panne **réellement survenue**, avec
ses chiffres relevés, sans toucher aux données de l'utilisateur :

- `tHook = 1789181509266` — `updated_at` du fichier de hook (« une permission est demandée ») ;
- `tEpisode = 1789210240523` — `treatedWaitingTs` relevé dans `treated.json` ;
- écart **478 min**, tenu par une assertion d'anti-dérive : `Assert.Equal(478, (int)(tEpisode - tHook).TotalMinutes)` ;
- cycle 2 à `tEpisode + 3 min` → `481 min > DropAfter (480 min)` : le fichier de hook cesse d'être lu,
  le transcript (`Working`) reprend la main. **La bascule de source.**

**L'horloge injectée du magasin est avancée avec le cycle** (`horloge.UtcNow = apres`), parce que le magasin
doit voir le même temps que le moniteur : une horloge figée ferait mentir le filtre, et l'horloge système
rendrait le test dépendant de l'heure de la journée.

Trois assertions au cycle 2 : la session est dans `Visibles`, elle n'est PAS dans `Masquees` avec le motif
`Traitee`, et le magasin ne contient rien — « expirer n'est pas répondre ».

## La preuve que le point (b bis) est livré

C'est la correction la plus silencieuse du plan, et celle sans laquelle « marquer traité » serait un no-op
sur le cas même dont ce milestone est parti.

| Mesure | Attendu | **Mesuré** |
|---|---|---|
| `grep -c "FromHours(6)" src/Chronos/Services/TreatedStore.cs` | 0 | **0** |
| `grep -c "RetentionMax" src/Chronos/Services/TreatedStore.cs` | ≥ 3 | **4** (déclaration, filtre de `Load`, filtre de `LoadMutable`, mention de XML-doc) |

Et la preuve **par le comportement**, ajoutée dans `TreatedStore_set_load_remove_et_purge_TTL` — c'est
l'assertion qui accuse nommément l'ancienne borne :

```csharp
var septHeures = T.AddHours(-7).ToUnixTimeMilliseconds();
store.Set("sept-heures", septHeures);
Assert.True(store.Load().TryGetValue("sept-heures", out var ts7) && ts7 == septHeures);
```

Sous `Ttl = FromHours(6)`, cette entrée n'était **pas** rendue : une session attendant depuis sept heures —
encore pleinement lue par le moniteur, qui va jusqu'à huit — voyait son traitement s'évaporer en silence.
La borne est désormais une **rétention de fichier** (24 h), strictement supérieure à `DropAfter` (8 h). La
réversibilité reste portée par NET-03, **jamais par une horloge**.

## Les valeurs réelles de chaque `grep`

### Tâche 1

| Mesure | Attendu | **Mesuré** | Verdict |
|---|---|---|---|
| `grep -c "Vainqueurs" src/Chronos/Services/ArbitrageSessions.cs` | ≥ 2 | **2** | ✅ |
| `grep -c "Observe(arbitrage.Vainqueurs" src/Chronos/Services/SessionMonitor.cs` | 1 | **1** | ✅ |
| `grep -c "IClock" src/Chronos/Services/TreatedStore.cs` | 2 | **2** | ✅ |
| `grep -c "_horloge.UtcNow" src/Chronos/Services/TreatedStore.cs` | 2 | **2** | ✅ |
| `grep -c "DateTimeOffset.UtcNow" src/Chronos/Services/TreatedStore.cs` | 0 | **0** | ✅ |
| `grep -c "FromHours(6)" src/Chronos/Services/TreatedStore.cs` | 0 | **0** | ✅ |
| `grep -c "RetentionMax" src/Chronos/Services/TreatedStore.cs` | ≥ 3 | **4** | ✅ |
| `grep -c "tmp" src/Chronos/Services/TreatedStore.cs` | inchangé | **6 → 6** | ✅ (motif d'écriture non touché) |
| `grep -c "IsWaiting\|NET-01\|NET-03" src/Chronos/Services/SessionTreatmentTracker.cs` | ≥ 3 | **11** | ✅ (fin T1 : les règles sont encore là, intactes) |
| `git diff --stat 07ee784..HEAD -- src/Chronos/Views src/Chronos/Resources src/Chronos/ViewModels` | vide | **vide** | ✅ |

### Tâche 2

| Mesure | Attendu | **Mesuré** | Verdict |
|---|---|---|---|
| échecs sur `~TreatedSessions` | 6, nominatifs | **6, les six annoncés** | ✅ |
| `Un_episode_reellement_plus_recent_purge_toujours` | vert dès T2 | **vert** | ✅ |
| 8 tests préexistants de `TreatedSessionsTests` | verts | **verts** | ✅ |
| `grep -c "1789181509266\|1789210240523" tests/…/TreatedSessionsTests.cs` | 2 | **4** | ⚠ voir déviation 1 |
| `grep -c "DateTimeOffset.UtcNow" tests/…/TreatedSessionsTests.cs` | 0 | **0** | ✅ |
| `grep -c "GetFolderPath\|APPDATA" tests/…/TreatedSessionsTests.cs` | 0 | **0** | ✅ |
| `git diff --stat 07ee784..HEAD -- …/SessionTreatmentTracker.cs` | inchangé vs fin T1 | **3 insertions, 2 deletions — identique** | ✅ |

### Tâche 3

| Mesure | Attendu | **Mesuré** | Verdict |
|---|---|---|---|
| `grep -c "prec.Source == v.Source" …/SessionTreatmentTracker.cs` | 1 | **1** | ✅ |
| `grep -c "WaitingDeduced" …/SessionTreatmentTracker.cs` | ≥ 1 | **1** | ✅ |
| `grep -c "EstTravailObserve" …/SessionTreatmentTracker.cs` | 2 | **2** | ✅ |
| `grep -c "InstantDuSignal" …/SessionTreatmentTracker.cs` | 3 | **3** | ✅ |
| `grep -c "_waitingSince\|_lastActivity\|IsWaiting" …/SessionTreatmentTracker.cs` | 0 | **0** | ✅ |
| `git diff --stat 07ee784..HEAD -- …/ArbitrageSessions.cs` | le seul ajout de T1 | **15 (+12/-3), l'ajout de T1 et rien d'autre** | ✅ |
| Suites de gardes (9 filtres) | 0 échec | **78/78 verts** | ✅ |

### Acquis des phases précédentes, revérifiés

| Acquis | Attendu | **Mesuré** |
|---|---|---|
| `grep -cF "?? new " src/Chronos/Services/DiagnosticService.cs` | 2 | **2** |
| `grep -c "MotifMasquage" src/Chronos/Services/DiagnosticService.cs` | 3 | **3** |
| `grep -c "new SessionMonitor" src/Chronos/Services/DiagnosticService.cs` | 0 | **0** |
| `grep -rcF "?? new " src/` (somme) | 6 à l'entrée, 7 attendu après le `clock ?? new SystemClock()` | **7** — hors `DiagnosticService.cs`, le compte gardé reste 2 |
| `L_arbitrage_ne_connait_ni_horloge_ni_magasin_ni_chemin` | vert | **vert** |
| `NormalisationUniqueTests`, `ServicesLayerPurityTests`, `GardesPerimetreTests`, `CompositionRootTests`, `GardesDoctrineTests`, `ContratHooksDocumenteTests`, `InspectionSessionsTests`, `BalayageMagasinSessionsTests` | verts | **verts** |
| diff sur `SessionHookProcessor.cs`, `EcritureEtatSession.cs`, `CatalogueEvenementsHooks.cs` | vide | **vide** |

## Task Commits

1. **Task 1 : La source atteint le détecteur, et le magasin reçoit une horloge** — `2f61782` (feat)
2. **Task 2 : RED — le scénario mesuré des 478 minutes et les quatre invariants qui manquent** — `e3aa051` (test)
3. **Task 3 : GREEN — une transition, ou rien** — `a84745c` (feat)

## Files Created/Modified

- `src/Chronos/Services/ArbitrageSessions.cs` — `ResultatArbitrage` gagne `Vainqueurs`, la même séquence que
  `Retenus` index pour index, chaque élément portant sa source. Comparateur, détection de désaccord et
  signature de `Trancher` **inchangés**.
- `src/Chronos/Services/TreatedStore.cs` — horloge **injectée** (`IClock? clock = null`), les deux lectures
  d'horloge système remplacées, et la durée de vie de six heures devenue `RetentionMax` de 24 h (b bis).
  Motif d'écriture `tmp + move` **non touché**.
- `src/Chronos/Services/SessionTreatmentTracker.cs` — le cœur : `EstAttente` (trois attentes, `WaitingDeduced`
  comprise), `EstTravailObserve` (seul `Working`), `InstantDuSignal` (l'épisode daté par le signal, borné par
  l'instant courant), `_dernier` porte `(Source, Activite)`, NET-01 exige la même source. NET-03 inchangée.
- `src/Chronos/Services/SessionMonitor.cs` — une ligne : `_tracker?.Observe(arbitrage.Vainqueurs, now)`.
  `var raw = arbitrage.Retenus;` conservé : les filtres continuent de travailler sur des instantanés et
  l'unicité `Read(now) => Inspecter(now).Visibles` n'est pas touchée.
- `src/Chronos/App.xaml.cs` — une ligne : `new TreatedStore(null, sp.GetRequiredService<IClock>())`.
- `tests/Chronos.Tests/ArbitrageSessionsTests.cs` — `Les_vainqueurs_sont_les_retenus_avec_leur_source`.
- `tests/Chronos.Tests/TreatedSessionsTests.cs` — helper `Sig(...)`, les neuf instants dérivés de `T`, chaque
  `new TreatedStore(...)` avec son `FakeClock(T)`, et les sept cas de la tâche 2.

## Deviations from Plan

### 1. `grep -c "1789181509266\|1789210240523"` vaut **4** et non **2** — le critère se comptait lui-même de travers

- **Trouvé pendant :** vérification des critères d'acceptation de la tâche 2.
- **Constat :** le bloc `<action>` du plan écrit le test **intégralement**, et il y place chaque constante
  **deux fois** : une fois dans la XML-doc (l. 331 et 333) et une fois dans le code (l. 343 et 344).
  `grep` compte des LIGNES : le code que le plan prescrit donne donc mécaniquement **4**.
- **Décision :** le test est **conservé mot pour mot** tel que le plan l'écrit. Aucune ligne n'a été retirée
  pour faire dire 2 au compteur — supprimer les constantes de la XML-doc aurait retiré au test sa traçabilité
  vers le relevé, pour satisfaire un chiffre. La consigne d'exécution interdisait explicitement d'« améliorer »
  les seuils du plan ; elle interdit a fortiori de mutiler le code pour les atteindre.
- **Valeur réelle reportée :** **4** (2 en XML-doc, 2 en code). L'intention du critère — les constantes
  mesurées sont bien présentes et non arrondies — est tenue.
- **Fichiers :** `tests/Chronos.Tests/TreatedSessionsTests.cs` (aucune modification liée).

### 2. La tâche 1 est **un seul commit**, sans étape rouge préalable

- **Trouvé pendant :** tâche 1, marquée `tdd="true"`.
- **Constat :** le seul cas ajouté, `Les_vainqueurs_sont_les_retenus_avec_leur_source`, lit `r.Vainqueurs` —
  un membre qui n'existe pas avant le correctif. Un commit « RED » y serait un commit qui **ne compile pas**,
  ce que les règles dures du plan interdisent sans exception (« aucun échec de build attendu »).
- **Décision :** un commit `feat` unique. `26-VALIDATION.md` classe d'ailleurs ce test parmi les
  « verts dès leur écriture, et annoncés comme tels ». Les tâches 2 et 3 portent, elles, le cycle rouge/vert
  complet et mesuré.

### 3. `CompositionRootTests.cs` n'a pas été modifié

- **Trouvé pendant :** tâche 1, point (f).
- **Constat :** le point (f) laisse le choix, l'horloge étant un paramètre **optionnel**. Le miroir construit
  `TreatedStore` avec son seul chemin temporaire ; le graphe résout, `Le_graphe_DI_resout_la_chaine_de_sessions`
  reste vert sans une ligne touchée.
- **Décision :** ne rien changer. Ce fichier figure dans `files_modified` du frontmatter du plan mais le point
  (f) autorise explicitement de ne pas y toucher (« Aucun test ajouté ici »). **Zéro diff.**

### Aucun renommage supplémentaire

Le seul renommage prévu au milestone (26-02 T2) n'est pas de ce plan. `TreatedStore_set_load_remove_et_purge_TTL`
garde son nom, conformément à la consigne. Les renommages **internes au code** (`IsWaiting` → `EstAttente`,
`_lastActivity` → `_dernier`, `_waitingSince` → `_attenteDepuis`) sont ceux que le plan prescrit mot pour mot
dans son bloc `<action>` de la tâche 3, et `26-VALIDATION.md` les déclare explicitement « pas des renommages
de tests ». **Aucun autre renommage.**

### Aucun auth gate, aucun blocage

Trois tâches, aucune interruption, aucune règle 4 déclenchée.

## Sécurité — vérifié

- Aucun test n'écrit hors de `Path.GetTempPath()` : `grep -c "GetFolderPath\|APPDATA"` sur le fichier de tests
  touché = **0**.
- `%APPDATA%\Chronos\archived.json` : **84 octets**, mtime **12 juillet 16:38** — inchangé.
- `%APPDATA%\Chronos\treated.json` : **2 octets**, mtime **12 septembre 21:21**, antérieur à cette session —
  inchangé, ni lu en écriture ni supprimé.
- `%APPDATA%\Chronos\sessions\` : **non approché**. (Il bouge tout seul — les hooks v3.0.2 de l'utilisateur
  sont vivants ; ce n'est pas une violation et rien n'a été « réparé ».)
- Le vrai `~/.claude/settings.json` n'a pas été écrit. Aucune sonde. `oauth.dat` n'a pas été approché, son
  mtime n'a pas été vérifié.
- L'overlay (pid 119412) n'a été **ni lancé ni tué**. Aucune requête réseau. Aucune dépendance NuGet nouvelle.

## Ce que la suite peut tenir pour acquis

- `ResultatArbitrage.Vainqueurs` existe et est gardé égal à `Retenus`.
- `TreatedStore` prend une horloge en second paramètre **optionnel** — le plan 26-02 peut faire de même sur
  `ArchiveStore` sans toucher au miroir DI.
- La borne de `TreatedStore` est une **rétention de 24 h**, ce qui rend le geste explicite du plan 26-03
  opérant sur une session attendant depuis plus de six heures. Le test
  `Marquer_traitee_une_session_qui_attend_depuis_sept_heures_la_fait_disparaitre` (26-03 T1) naîtra vert
  **grâce à ce plan**, et non par hasard.
- `docs/hooks-contract.md` §3 porte encore la divergence que ce plan referme dans le code : le **document**
  est mis à jour au plan **26-04**, il n'a pas été touché ici.

## Self-Check: PASSED

- Fichiers annoncés : tous présents (`26-01-SUMMARY.md`, `ArbitrageSessions.cs`, `TreatedStore.cs`,
  `SessionTreatmentTracker.cs`, `TreatedSessionsTests.cs`, `ArbitrageSessionsTests.cs`).
- Commits annoncés : `2f61782`, `e3aa051`, `a84745c` — les trois retrouvés dans `git log`.
- Suite complète relancée deux fois après le dernier commit : **876 / 0 échec / 6 s**, les deux fois.
