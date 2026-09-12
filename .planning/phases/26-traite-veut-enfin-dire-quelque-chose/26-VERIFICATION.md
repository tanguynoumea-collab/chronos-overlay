---
phase: 26-traite-veut-enfin-dire-quelque-chose
verified: 2026-09-13T00:00:00Z
status: passed
score: 5/5 critères vérifiés (en salle) — 0/5 observés in vivo, par construction
sha_verifie: 521a8a2
sha_entree_de_phase: 07ee784
suite:
  total: 888
  echecs: 0
  executions: 2
  duree: "6 s / 6 s"
  build: "0 erreur, 0 avertissement"
mutations_rejouees:
  - id: "ROUGE 26-01 T2 (e3aa051)"
    attendu: "6 rouges"
    mesure: "échec : 6, réussite : 9, total : 15 — les six NOMS annoncés, à l'identique"
  - id: "b bis — RetentionMax 24 h → 6 h"
    attendu: "le cas des sept heures tombe, et lui seul"
    mesure: "filtre ~GesteTraite : 1 échec / 5 (le cas nommé) — CONFORME. Suite COMPLÈTE : 2 échecs / 888 (le cas nommé + TreatedStore_set_load_remove_et_purge_TTL)"
  - id: "M2 — casse abaissée dans le document"
    attendu: "garde croisée rouge, grep « même source » 2 → 3"
    mesure: "1 échec / 9, unique FAIL = Le_document_dit_la_regle_de_traitement_reellement_cablee ; grep 2 → 3 — CONFORME"
  - id: "M3 — WaitingDeduced retiré d'EstAttente"
    attendu: "garde croisée rouge"
    mesure: "1 échec / 9 (garde croisée). Suite complète : 2 échecs (garde + Une_attente_deduite_ouvre_un_episode) — CONFORME"
  - id: "M4 (ajoutée par le vérificateur) — EstTravailObserve relâché à !EstAttente"
    mesure: "1 échec / 888 : Un_etat_inconnu_n_affirme_pas_qu_on_a_repondu — la sonde S2 est bien verrouillée"
  - id: "M5 (ajoutée) — retrait de prec.Source == v.Source"
    mesure: "3 échecs / 888, dont Le_scenario_mesure_des_478_minutes — la condition « même source » est EFFECTIVE"
  - id: "M6 (ajoutée) — InstantDuSignal ramené à nowMs"
    mesure: "2 échecs / 888, dont Le_traite_survit_au_redemarrage — la datation par le signal est EFFECTIVE"
  - id: "M7 (ajoutée) — geste DESTRUCTIF en tête, libellés muets sur 2 des 8 menus"
    mesure: "888 / 0 échec — AUCUNE garde ne voit la régression (voir avertissement 1)"
avertissements:
  - id: 1
    severite: warning
    titre: "L'ordre des menus et le texte des deux libellés statiques ne sont tenus par AUCUNE garde"
    fichiers:
      - "tests/Chronos.Tests/GardesPerimetreTests.cs:270 (Les_huit_menus_offrent_le_geste_explicite_et_disent_la_verite)"
      - "tests/Chronos.Tests/SessionStylesBindingTests.cs:161 (Les_trois_gestes_sont_offerts_sur_les_8_styles_et_les_9_themes)"
    mesure: "mutation M7 (destructif en tête du menu, avant le <Separator/>, libellés réduits à « Archiver » et « Marquer traitée ») → suite 888/888 VERTE"
    impact: "Le câblage LIVRÉ est correct (réversible en tête, destructif en queue derrière le séparateur, trois libellés qui annoncent le retour). La protection de NON-DÉRIVE, elle, est plus faible que ce que la XML-doc de la garde affirme (« l'ORDRE et les libellés sont la sécurité, pas la décoration ») : les assertions sont des COMPTES, indifférents à l'ordre et au texte."
    bloquant: false
  - id: 2
    severite: info
    titre: "Nuance sur le point (b) : la mutation 6 h fait tomber DEUX cas sur la suite complète"
    mesure: "1 échec sur le filtre ~GesteTraite (conforme à la revendication de 26-03, qui est explicitement bornée à ce filtre) ; 2 échecs sur les 888"
    impact: "Aucun. Le second cas (TreatedStore_set_load_remove_et_purge_TTL, l'assertion inverse des sept heures ajoutée en 26-01) est un verrou de PLUS, pas un verrou de moins."
    bloquant: false
dettes_reportees_verifiees:
  - "XML-doc obsolète d'ArchiveStore.PurgerPrefixe (l. 99 : « Add, qui applique au passage la purge des entrées expirées ») — CONSTATÉE présente dans le code, CONSIGNÉE en écart n° 5 de 26-VALIDATION.md, NON corrigée. Conforme."
  - "TRT-04 coché au plan 26-02, volet UI arrivé en 26-03 — CONSIGNÉ en écart n° 4. État final vérifié cohérent : 4 cases [x], 4 lignes Complete, 0 Pending."
human_verification:
  - test: "Republier l'exe, relancer l'overlay, rouvrir les sessions, attendre une VRAIE demande de permission, puis y répondre"
    expected: "La session quitte le widget au moment de la réponse, et pas avant ; aucune session en attente ne disparaît quand son fichier de hook franchit les huit heures"
    why_human: "Aucun des cinq critères n'est observable sans exécution réelle. La chaîne de préalables est bloquante et hors périmètre de la phase (déférée par le ROADMAP)."
  - test: "Fermer puis relancer l'overlay avec au moins une session marquée traitée"
    expected: "Les sessions traitées ne ressortent pas"
    why_human: "Exige de lancer/arrêter l'overlay — interdit pendant la vérification (pid 119412 en cours d'utilisation)"
  - test: "Clic droit sur une ligne, sur chacun des 8 styles"
    expected: "Trois entrées, réversible en tête, « Archiver définitivement (ne revient jamais) » en queue derrière un trait, aucune coupe de texte et aucune perte de compacité"
    why_human: "Le rendu réel d'un menu ouvert (largeur, troncature, lisibilité) n'est pas mesurable sans afficher de fenêtre"
---

# Phase 26 : « Traité » veut enfin dire quelque chose — Rapport de vérification

**Goal (ROADMAP) :** une session disparaît du widget parce qu'on l'a **réellement traitée** — transition
observée sur la même source, ou geste explicite — jamais parce qu'une source a expiré ; et ce qui a disparu
ne revient pas tout seul au prochain démarrage.

**SHA vérifié :** `521a8a2` — **SHA d'entrée de phase :** `07ee784`
**Statut :** **passed** (en salle) — **5/5 critères**, **0/5 observés in vivo** (par construction, voir plus bas)
**Vérification :** initiale (aucun `26-VERIFICATION.md` antérieur)

---

## 0. Ce qui a été mesuré, pas lu

Aucun chiffre de SUMMARY n'a été repris. Tout ce qui suit sort d'une exécution.

| Mesure | Commande | Résultat |
|---|---|---|
| Suite, 1re exécution | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj -v q --nologo` | **888 / 0 échec / 6 s** |
| Suite, 2e exécution | idem | **888 / 0 échec / 6 s** |
| Build | `dotnet build Chronos.sln -c Debug --nologo` | **0 erreur, 0 avertissement** |
| Arithmétique des 478 min | `1789210240523 − 1789181509266` | **28 731 257 ms = 478,854 min** → `(int)` = **478** ✅ |
| Le cycle 2 franchit-il le seuil ? | `478,854 + 3 min = 481,854 min` vs `DropAfter = 480 min` | **oui, de 1,85 min** ✅ |
| Étape ROUGE rejouée (`e3aa051`) | `--filter "FullyQualifiedName~TreatedSessions"` | **échec : 6, réussite : 9, total : 15** — **6 rouges annoncés, 6 rouges mesurés** ✅ |

Les six noms sortis du runner à `e3aa051` sont **exactement** les six annoncés en 26-01 T2 :
`Une_attente_qui_devient_deduite_n_est_pas_traitee`, `Le_scenario_mesure_des_478_minutes_laisse_la_session_visible`,
`Une_attente_deduite_ouvre_un_episode`, `Une_bascule_de_source_ne_marque_rien`,
`Un_etat_inconnu_n_affirme_pas_qu_on_a_repondu`, `Le_traite_survit_au_redemarrage_mais_pas_a_une_nouvelle_demande`.
Le septième cas (`Un_episode_reellement_plus_recent_purge_toujours`) est bien **vert dès e3aa051** :
9 réussites = 8 préexistants + lui.

---

## 1. Verdict par critère du ROADMAP

| # | Critère | Verdict | Ce qui le prouve — **mécaniquement** |
|---|---|---|---|
| 1 | **Répondre fait disparaître, expirer non** (TRT-01) | ✅ **VÉRIFIÉ en salle** | `SessionTreatmentTracker.cs:86` porte les TROIS conditions : `connue && prec.Source == v.Source && EstAttente(prec.Activite) && EstTravailObserve(etat)`. **Mutation M5** (retrait de la condition de source) → **3 échecs/888**. Cas verts : `Une_bascule_de_source_ne_marque_rien`, `NET01_disparition_seule_ne_traite_pas`, `Un_etat_inconnu_n_affirme_pas_qu_on_a_repondu`. |
| 2 | **Le masquage de 6 h ne se reproduit plus** (TRT-01) | ✅ **VÉRIFIÉ en salle** | `Le_scenario_mesure_des_478_minutes_laisse_la_session_visible` rejoue les chiffres RÉELS, avec une garde anti-dérive `Assert.Equal(478, …)`. **Rouge rejoué par le vérificateur** à `e3aa051`, vert à `521a8a2`, et **rouge à nouveau sous M5**. |
| 3 | **Le traité survit au redémarrage** (TRT-02) | ✅ **VÉRIFIÉ en salle** | `InstantDuSignal = min(UpdatedAt du signal, now)` (`:64-65`), épisode posé une seule fois par attente (`:96-100`). **Mutation M6** (`=> nowMs`) → **2 échecs/888**. Quatre **sondes ajoutées par le vérificateur** (cas pervers) : toutes vertes, voir §3. |
| 4 | **Un geste explicite existe, 8 styles × 9 thèmes** (TRT-03) | ✅ **VÉRIFIÉ en salle** — ⚠ 1 réserve de non-dérive | 8 `DataTemplate`, 8 `ContextMenu`, **24 `<MenuItem>`**, 8 `<Separator/>`. `Les_trois_gestes_sont_offerts_sur_les_8_styles_et_les_9_themes` monte les **72** combinaisons et exige 3 entrées par menu **sans ouvrir aucun menu ni afficher aucune fenêtre** (un `ContextMenu` est la VALEUR d'une propriété ; aucun `.Show()` dans le montage). **Réserve : avertissement n° 1.** |
| 5 | **« Archiver » fait ce qu'il annonce** (TRT-04) | ✅ **VÉRIFIÉ en salle** | `grep -cE 'TimeSpan\|Ttl\|FromHours\|FromDays' ArchiveStore.cs` = **0**. `Le_magasin_d_archives_ne_connait_plus_aucune_duree_de_vie` le tient **par RÉFLEXION** (aucun champ statique `TimeSpan`, `IClock` au constructeur) — insensible au texte des commentaires. Les 8 libellés disent « **ne revient jamais** ». |

**Score : 5/5.**

---

## 2. Points de vigilance, un par un

### (a) Le scénario mesuré des 478 min — **arithmétique et rouge confirmés**

L'écart des deux constantes vaut **478,854 min**, figé à 478 par un `Assert.Equal`. Au cycle 2
(`tEpisode + 3 min`) le fichier de hook a **481,85 min** > `DropAfter` **480 min** : il cesse d'être lu,
le transcript `Working` reprend seul la main. Le test exige alors **trois** choses : la session **visible**,
**aucun** masquage de motif `Traitee`, et le magasin **vide**.

**Rouge rejoué** dans un worktree jetable hors dépôt, détaché sur `e3aa051` : `échec : 6, réussite : 9,
total : 15`. **6 annoncés, 6 mesurés, mêmes noms.**

### (b) La correction (b bis) — **livrée, et je l'ai mesurée moi-même**

`TreatedStore.cs:44` : `private static readonly System.TimeSpan RetentionMax = System.TimeSpan.FromHours(24);`
— **24 h**, strictement supérieure à `SessionMonitor.DropAfter` (**8 h**, `SessionMonitor.cs:30`).
`FromHours(6)` n'existe plus nulle part dans `src/`.

Mutation **24 h → 6 h**, mesurée par le vérificateur :

| Portée | Résultat |
|---|---|
| `--filter "FullyQualifiedName~GesteTraite"` | `échec : 1, réussite : 4, total : 5` — unique FAIL : `Marquer_traitee_une_session_qui_attend_depuis_sept_heures_la_fait_disparaitre` |
| **suite complète (888)** | `échec : 2` — le cas nommé **+ `TreatedSessionsTests.TreatedStore_set_load_remove_et_purge_TTL`** |

La revendication de 26-03 (« la suite GesteTraite tombe à 4/5 et le seul échec est nominativement… ») est
**exacte dans son périmètre déclaré**. Sur la suite entière, la mutation est attrapée par **deux** verrous
indépendants — c'est mieux que revendiqué, et le constat est consigné en avertissement n° 2 pour que le
chiffre reste juste.

### (c) Le trou `WaitingDeduced` — **refermé, et doublement**

`EstAttente` (`:52-54`) couvre les **trois** attentes (`WaitingTurn`, `WaitingAttention`, `WaitingDeduced`).
`EstTravailObserve` (`:59`) n'accepte que `Working`.

Constat **non trivial** que la mutation a révélé : retirer `WaitingDeduced` d'`EstAttente` (**M3**) ne fait
**pas** tomber `Une_attente_qui_devient_deduite_n_est_pas_traitee` — c'est `EstTravailObserve` qui empêche de
conclure. Les deux prédicats se couvrent donc **l'un l'autre** :

- **M3** (`EstAttente` amputé) → 2 échecs : la garde croisée + `Une_attente_deduite_ouvre_un_episode` ;
- **M4** (`EstTravailObserve => !EstAttente`, la sémantique d'avant) → 1 échec : `Un_etat_inconnu_n_affirme_pas_qu_on_a_repondu`.

**Une session en attente dont la source se tait n'est plus marquée traitée**, et elle ne le serait pas non
plus si l'un des deux prédicats seul régressait.

### (d) TRT-01 — la contrainte « MÊME source » est **présente ET effective**

Présente : `SessionTreatmentTracker.cs:86`. Effective : **mutation M5** → **3 échecs / 888**
(`Le_scenario_mesure_des_478_minutes`, `Une_bascule_de_source_ne_marque_rien`, la garde croisée du document).
Une expiration seule ne marque rien non plus (`NET01_disparition_seule_ne_traite_pas`).

Le chemin de la source jusqu'au détecteur est réel : `ResultatArbitrage.Vainqueurs` (`ArbitrageSessions.cs:48`)
est construit **index pour index** avec `Retenus` (`:82-85`), et `SessionMonitor.cs:119` passe
`arbitrage.Vainqueurs` — pas `Retenus` — au détecteur.

### (e) TRT-02 — c'est bien le **DÉTECTEUR** qui survit, pas seulement le magasin

Quatre sondes jetables écrites et exécutées **par le vérificateur** dans le worktree (puis supprimées) :

| Sonde | Cas pervers | Résultat |
|---|---|---|
| P1 | **Signal daté DANS L'AVENIR** (+3 h) puis travail | épisode **borné à `now`** — rien d'inexpirable ✅ |
| P2 | **Épisode FIGÉ** : source muette, **1 000 cycles** après un geste explicite | la traitée **ne ressort jamais** ✅ |
| P3 | **Alternance** Hook(attente) → Transcript(attente) → Hook(travail) | **rien n'est marqué** (le cycle précédent n'était pas la même source) ✅ |
| P4 | **Redémarrage après geste explicite**, signal vieux de 7 h, détecteur NEUF | la traitée **reste** ✅ |

Un tracker neuf ne purge donc plus le magasin qu'il vient de lire, **et** la réversibilité est intacte
(`Un_episode_reellement_plus_recent_purge_toujours`, vert avant comme après).

### (f) TRT-03 — le geste est non ambigu **dans le code livré**, mais la garde ne le tient pas

Dans les **8** menus, dans cet ordre exact :

1. `Marquer traitée (revient si elle me redemande)` → `MarquerTraiteeCommand` — **réversible, en tête**
2. `{Binding ToutTraiterLibelle}` → `MarquerToutTraiteCommand` — libellé calculé :
   `Tout marquer traité (N) — elles reviennent si elles redemandent` (`SessionsViewModel.cs:195`)
3. `<Separator/>`
4. `Archiver définitivement (ne revient jamais)` → `ArchiveCommand` — **destructif, en queue**

Les **trois** annoncent si la session revient, geste de masse compris.

**Le geste de masse n'écrit QUE dans le magasin réversible** : `MarquerToutTraite` (`SessionsViewModel.cs:166-170`)
n'appelle que `_treated.Set(...)`. Aucune référence à `_archive` dans cette méthode ni dans le chemin de la
commande. ✅

**La matrice 8 × 9 voit bien les menus sans rien afficher** : `Monter(...)` construit la fenêtre, pose le
`DataContext` sur la grille racine et fait `Measure/Arrange/UpdateLayout` — **aucun `Show()`** ; les menus
sont récupérés comme **valeur de la propriété `ContextMenu`**, jamais ouverts.

**⚠ Réserve mesurée (avertissement n° 1).** J'ai placé le geste **destructif en TÊTE** du menu, devant le
séparateur, et remplacé les deux libellés statiques par « Archiver » et « Marquer traitée » — sur deux des
huit templates. **Résultat : 888 / 0 échec.** Les deux gardes ne font que **compter** (`Regex.Matches(...).Count`
sur `<MenuItem`, `<Separator/>`, les trois noms de commande) et compter le nombre d'items par menu : ni
l'ordre, ni le texte des deux libellés statiques ne sont tenus. La XML-doc de la garde affirme pourtant que
« l'ORDRE et les libellés sont la sécurité, pas la décoration ». **Le livré est correct ; c'est la
non-dérive qui est plus faible qu'annoncée.** Non bloquant pour le goal ; à refermer d'une assertion
d'ordre (index du `<Separator/>` < index d'`ArchiveCommand`) et de deux `Assert.Contains` littéraux.

### (g) TRT-04 — `ArchiveStore` n'a plus aucune durée de vie

| Contrôle | Résultat |
|---|---|
| `grep -cE 'TimeSpan\|Ttl\|FromHours\|FromDays\|FromMinutes' ArchiveStore.cs` | **0** ✅ |
| Garde par réflexion (`Le_magasin_d_archives_ne_connait_plus_aucune_duree_de_vie`) | verte — aucun champ statique `TimeSpan`, `IClock` au ctor ✅ |
| `Load()` écarte-t-il par le temps ? | non : seule une valeur non numérique est ignorée (`:50`) ✅ |
| Horloge injectable | `ArchiveStore(string?, IClock?)` ; `L_horodatage_d_une_archive_vient_de_l_horloge_injectee` le prouve ✅ |
| **`PurgerPrefixe` intact — comparaison d'OCTETS `07ee784` vs `HEAD`** | **1312 o / 1312 o, md5 `bceaf7d903bd5805057d42befe9e7d3b` identique, `diff` vide** ✅ |
| Les 8 libellés | « Archiver **définitivement (ne revient jamais)** » ×8 ✅ |

### (h) La garde de non-dérive de 26-04 — **falsifiable, rejouée**

| Mutation rejouée par le vérificateur | Mesuré |
|---|---|
| **M2** — « transition observée sur la **même** source » (casse abaissée) | `grep -c "même source"` : **2 → 3**, et pourtant `échec : 1, réussite : 8, total : 9`, unique FAIL = `Le_document_dit_la_regle_de_traitement_reellement_cablee` |
| **M3** — `WaitingDeduced` retiré du prédicat `EstAttente` (**côté CODE**) | `échec : 1, réussite : 8, total : 9`, même unique FAIL |

**Ce que M2 prouve est confirmé** : une garde adossée aux deux mots « même source » aurait été **verte avant
l'édition** (2 occurrences préexistantes au §5) **et resterait verte sous la mutation** — elle n'aurait rien
gardé. La garde livrée porte sur la formulation exacte avec capitales, et elle rougit pendant que le compte
naïf **augmente**. **M3 prouve la bidirectionnalité** : la garde lit le corps découpé du prédicat
(`EstAttente(SessionActivity` → premier `;`), donc un commentaire ne la trompe pas.

### (i) Les acquis — remesurés un par un

| Acquis | Attendu | **Mesuré** |
|---|---|---|
| `PurgerPrefixe` intact | octet pour octet | **identique (md5)** ✅ |
| `diff` sur `SessionHookProcessor.cs`, `EcritureEtatSession.cs`, `CatalogueEvenementsHooks.cs`, `SessionHookInstaller.cs` | vide | `git diff --stat 07ee784..HEAD` → **vide** ✅ |
| `Read(now) => Inspecter(now).Visibles` | unique | **1** ✅ |
| `new SessionMonitor` dans `DiagnosticService` | 0 | **0** ✅ |
| `grep -cF "?? new " DiagnosticService.cs` | 2 | **2** ✅ |
| `MotifMasquage` | 3 lignes / 2 valeurs | **3 lignes** dans `DiagnosticService.cs` (597, 599, 600) ; **2 valeurs** (`Archivee`, `Traitee`) ✅ |
| Pureté d'`ArbitrageSessions` | verte | `ArbitrageSessionsTests` verts ; aucune E/S, aucune horloge, aucun WPF dans le fichier ✅ |
| `WaitingDeduced` écrit dans un fichier d'état | jamais | `grep` sur `EcritureEtatSession.cs` + `SessionHookProcessor.cs` → **0** ✅ |
| Marqueurs `EVENEMENTS-CABLES` | présents | **2** (début + fin) ✅ |
| Garde de la phase 25 + `ContratHooksDocumenteTests` | vertes | **9/9** ✅ |
| `NormalisationUniqueTests`, `ServicesLayerPurityTests`, `GardesDoctrineTests`, `CompositionRootTests`, `GardesPerimetreTests` | vertes | incluses dans les **888/0** ✅ |
| Aucun type WPF dans `Services/` ni `Models/` | — | 3 fichiers portent `using System.Windows` : `OverlayController`, `TopmostGuard`, `WpfUiDispatcher` — **les trois adaptateurs de l'allow-list nominative, antérieurs à la phase 26**. **Aucun des 5 fichiers touchés par la phase 26 n'importe WPF** ✅ |

### (j) Les deux dettes reportées — **consignées, non corrigées**

1. **XML-doc obsolète d'`ArchiveStore.PurgerPrefixe`.** `ArchiveStore.cs:99-101` écrit encore :
   « *Distinct aussi d'`Add`, qui applique au passage la purge des entrées expirées* » — phrase devenue
   **fausse** (`Add` conserve les entrées « TELLE QUELLE : recharger n'est pas périmer », `:71`).
   **Vérifié présente dans le code**, **vérifiée consignée** en écart n° 5 de `26-VALIDATION.md` et dans
   « Ce qui reste ouvert ». Aucune garde ne peut la rougir : c'est un commentaire périmé, pas un câblage faux. ✅
2. **TRT-04 coché au plan 26-02, volet UI livré au plan 26-03.** Vérifié consigné en écart n° 4.
   **État final vérifié cohérent** : `REQUIREMENTS.md` porte 4 cases `[x]` TRT et 4 lignes `| TRT-0x | Phase 26 | Complete |`,
   et les deux volets sont livrés (code en `a1413c2`, libellés en `923d1d5`). ✅

### (k) La suite, exécutée deux fois par le vérificateur

**888 tests, 0 échec, 6 s** — puis **888 tests, 0 échec, 6 s**. Build **0 erreur, 0 avertissement**.
Aucun chiffre de SUMMARY n'a été accepté sans mesure.

---

## 3. Chaîne de données (niveau 4) — le geste atteint-il vraiment le filtre ?

Une commande peut être parfaite et n'agir sur rien si le magasin qu'elle écrit n'est pas celui que le
moniteur lit. Tracé dans `App.xaml.cs` :

```
AddSingleton ArchiveStore(null, IClock)                     :246
AddSingleton TreatedStore(null, IClock)                     :251
AddSingleton SessionTreatmentTracker(TreatedStore)          :252
AddSingleton SessionMonitor(…, ArchiveStore, TreatedStore, SessionTreatmentTracker)  :254-256
AddSingleton SessionsViewModel(SessionMonitor, IClock, ArchiveStore, TreatedStore)   :278-281
```

**Singletons partagés de bout en bout** : la commande écrit dans l'**instance même** que le filtre relit,
puis `Refresh(_clock.UtcNow)` recharge → disparition **immédiate**. `TreatedStore.Load()` relit le fichier
à chaque appel (aucun cache), ce qui est la condition de la survie au redémarrage.
`GesteTraiteTests.Monter` reproduit ce partage et le prouve (`Marquer_traitee_fait_disparaitre_la_session_immediatement`).

Le geste explicite écrit `item.UpdatedAtMs` — **l'épisode que l'utilisateur vient de voir**, pas l'instant du
clic (`SessionsViewModel.cs:159`). NET-03 exige `cur > tts` **strictement** : l'égalité ne purge pas, un
signal plus récent purge. Le libellé « revient si elle me redemande » **décrit le câblage**.

---

## 4. Ce qui est prouvé mécaniquement, et ce qui ne peut l'être qu'in vivo

**Prouvé mécaniquement (en salle), et rien d'autre :**
les cinq critères, sur des signaux synthétiques, une horloge **injectée** et des chemins temporaires —
aucun test ne lit ni n'écrit le vrai `%APPDATA%\Chronos\`.

**Aucun des cinq critères n'est observé in vivo**, et ce n'est pas un manquement de la phase : c'est la
conséquence d'une chaîne de préalables que le ROADMAP a explicitement déférée.

**La chaîne de quatre préalables, dans l'ordre :**

1. **Republier l'exe.** Rien du milestone v1.6 ne s'exécute chez l'utilisateur : l'overlay en cours est
   `Chronos-v3.0.2.exe` (pid 119412), antérieur à tout ce qui a été livré des phases 21 à 26.
2. **Relancer l'overlay pour réconcilier `settings.json`.** La configuration de l'utilisateur ne porte
   encore que les **5 hooks v3.0.2** — sans `PermissionRequest`, sans matcher sur `Notification`.
   Sans cette réconciliation, aucune attente **observée** ne sera produite, et le détecteur ne verra rien
   à transitionner.
3. **Rouvrir les sessions.** Les hooks ne s'installent que dans des sessions démarrées après la
   réconciliation.
4. **Attendre une vraie demande de permission**, puis y répondre — c'est le seul moment où le critère 1
   (« répondre fait disparaître, expirer non ») devient observable pour de bon.

**État relevé pendant la vérification (lecture seule) :** `archived.json` = **84 octets** (inchangé),
`treated.json` = **2 octets** (`{}`, aucune entrée), `sessions\` = **65 entrées** (le dossier bouge tout
seul, hooks v3.0.2 vivants — attendu, non réparé). Rien n'a été écrit dans `%APPDATA%\Chronos\`, ni dans
`~/.claude/settings.json`. Aucune sonde installée, aucune fenêtre affichée, aucun processus lancé ou tué,
aucune requête réseau. `oauth.dat` n'a pas été approché.

---

## 5. Couverture des exigences

| Exigence | Verdict | Preuve nommée |
|---|---|---|
| **TRT-01** — transition observée sur la même source | ✅ **SATISFAITE** | `Une_bascule_de_source_ne_marque_rien`, `Le_scenario_mesure_des_478_minutes_laisse_la_session_visible`, `Une_attente_qui_devient_deduite_n_est_pas_traitee`, `Un_etat_inconnu_n_affirme_pas_qu_on_a_repondu` — falsifiabilité mesurée (M3, M4, M5) |
| **TRT-02** — survit au redémarrage | ✅ **SATISFAITE** | `Le_traite_survit_au_redemarrage_mais_pas_a_une_nouvelle_demande`, `Un_episode_reellement_plus_recent_purge_toujours` — falsifiabilité mesurée (M6) + 4 sondes de cas pervers |
| **TRT-03** — geste explicite | ✅ **SATISFAITE** (réserve de non-dérive, avertissement n° 1) | `GesteTraiteTests` (5 cas), `Les_huit_menus_offrent_le_geste_explicite_et_disent_la_verite`, `Les_trois_gestes_sont_offerts_sur_les_8_styles_et_les_9_themes` (72 montages) |
| **TRT-04** — contrat d'archivage unique | ✅ **SATISFAITE** | `Une_archive_ne_s_evapore_jamais_meme_apres_huit_jours`, `Le_magasin_d_archives_ne_connait_plus_aucune_duree_de_vie`, `Purger_n_est_pas_expirer_et_une_entree_vieille_reste_lisible`, libellé ×8 |

Aucune exigence orpheline : `REQUIREMENTS.md` ne rattache que TRT-01..04 à la phase 26, et les quatre sont
revendiquées par les plans.

---

## 6. Hygiène de la vérification

Deux worktrees jetables, tous deux créés **hors du dépôt** (sous le dossier temporaire de session) et
**révoqués** :

```
git worktree list      →  1 seule entrée (le dépôt)
git status --porcelain →  vide
```

Toutes les mutations (6 h, M2, M3, M4, M5, M6, M7) et les quatre sondes ont vécu et sont mortes dans ces
worktrees. **Aucun fichier du dépôt n'a été modifié.** Le dépôt est resté sur `521a8a2`, propre, du début
à la fin.

---

## 7. Conclusion

**Le goal de la phase est atteint.** « Traité » ne se déduit plus que d'une transition **observée sur la
même source** — la condition est présente, effective, et son retrait fait rougir trois cas dont le scénario
réel des 478 minutes. Le traité **survit au redémarrage** sans perdre sa réversibilité, y compris dans les
quatre cas pervers que j'ai sondés. Le **geste explicite** existe, réversible en tête et destructif en queue
derrière un séparateur, sur les 8 styles et les 9 thèmes, vérifié sans ouvrir un seul menu. Et « Archiver »
**ne ment plus** : le magasin n'a plus aucune durée de vie, la garde le tient par réflexion, et
`PurgerPrefixe` est intact à l'octet près.

**Un avertissement, non bloquant :** l'ordre des entrées de menu et le texte des deux libellés statiques
ne sont tenus par **aucune** garde — mesuré, pas soupçonné (M7 laisse la suite à 888/888). Le livré est
correct ; c'est la protection contre la prochaine dérive qui manque, alors que la XML-doc de la garde
affirme le contraire. À refermer hors phase, d'une assertion d'ordre et de deux littéraux.

**Et le rappel qui vaut pour tout le milestone v1.6 :** rien de ce qui précède n'a encore été vu tourner
chez l'utilisateur. La preuve en salle est complète ; la preuve in vivo attend la republication de l'exe.

---

_Vérifié : 2026-09-13 — Vérificateur : Claude (gsd-verifier)_
_Suite exécutée deux fois par le vérificateur : 888 / 0 échec. Aucun chiffre repris d'un SUMMARY._
