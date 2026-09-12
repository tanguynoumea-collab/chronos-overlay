---
phase: 25-le-contrat-d-evenements-refonde
verified: 2026-09-12
verifier: gsd-verifier (indépendant des SUMMARY)
sha_entree_de_phase: ce40e99cffe605e2baa93511e2555884aa6acdb8
sha_verifie: 1472dd9
status: human_needed
score: 5/5 critères prouvés mécaniquement — 4 gardent une part in vivo non mesurable à ce jour
suite: 862 tests / 0 échec, MESURÉS DEUX FOIS par le vérificateur (6 s / 6 s)
mutations_rejouees_par_le_verificateur: 3
gaps: []
reserves:
  - id: R1
    gravite: mineure
    sujet: "ApplyHooks — une clé CÂBLÉE dont la valeur n'est pas un JsonArray est ÉCRASÉE"
    fichier: "src/Chronos/Services/SessionHookInstaller.cs:210-214"
  - id: R2
    gravite: mineure
    sujet: "Aucun test ne fige les deux prudences de la purge (valeur non-tableau intouchée, clé déjà vide jamais retirée)"
  - id: R3
    gravite: cosmetique
    sujet: "CatalogueEvenementsHooksTests.Les_cinq_noms_cables_aujourd_hui_sont_tous_connus est resté à cinq noms en dur (le câblage en compte huit)"
  - id: R4
    gravite: cosmetique
    sujet: "25-VALIDATION.md nomme le prédicat du tracker `EstAttente` l. 39 ; le code dit `IsWaiting` l. 38. docs/hooks-contract.md §3, lui, est EXACT."
human_verification:
  - test: "Critère 1 in vivo — une demande de permission réelle bascule la session en « à toi »"
    why_human: "les trois événements neufs n'existent pas dans le settings.json vivant (vérifié ci-dessous)"
  - test: "Critère 2 in vivo — une session qui travaille plus d'une heure reste « en cours »"
    why_human: "idem : PreToolUse/PostToolUse Chronos absents de la configuration vivante"
  - test: "Critère 3 in vivo — Échap rend « à toi ? déduit », lisible, jamais « tour fini »"
    why_human: "idem"
  - test: "Critère 4 in vivo — aucune session annoncée « terminée » sans qu'un tour se soit terminé"
    why_human: "idem"
  - test: "A11 — effet de masse réel : combien de sessions passent ensemble en « à toi ? déduit », et WaitingCount"
    why_human: "exige republication + lancement + réconciliation + réouverture des sessions"
  - test: "Lisibilité de « à toi ? déduit » sur 8 gabarits × 9 thèmes (galerie --sessions)"
    why_human: "l'œil ; la matrice BAML mesure la disposition, pas la lisibilité"
  - test: "Sonde de capture des schémas stdin — à arbitrer par l'utilisateur"
    why_human: "toucher à sa configuration vivante ; la question est posée au §6 du contrat"
---

# Phase 25 — Le contrat d'événements refondé : rapport de vérification

**But de la phase :** les états du widget viennent d'événements qui veulent **vraiment** dire ce qu'on leur
fait dire.

**Méthode.** Vérification **goal-backward**, dans le CODE. Aucun chiffre n'est repris d'un SUMMARY sans
avoir été remesuré. Trois des cinq mutations de falsification ont été **rejouées par le vérificateur**, dans
un `git worktree` jetable **hors du dépôt** (sous le scratchpad de session), révoqué ensuite.

**Statut : `human_needed`.** Tout ce qu'une machine peut prouver est prouvé ; il ne reste **aucun gap
bloquant**. Ce qui subsiste est la part **in vivo**, structurellement inatteignable tant que l'exe n'est pas
republié ET que `settings.json` n'a pas été réconcilié — et ce point n'est pas une formule de prudence : il
est **mesuré** ci-dessous sur la configuration réelle.

---

## 0. La preuve que la part in vivo est réellement ouverte (lecture seule)

`~/.claude/settings.json`, **lu et non écrit** — md5 **`78eb517cc1a453b2ddce9a39af528900`**, identique à la
valeur relevée à l'entrée et à la sortie de la phase. Contenu des hooks, tel qu'il est **aujourd'hui** :

| Clé | Groupe [0] | Groupe [1] |
|---|---|---|
| `SessionStart` | `gsd-check-update.js` (tiers) | `Chronos-v3.0.2.exe --hook SessionStart` |
| `PreToolUse` | `gsd-prompt-guard.js`, matcher `Write\|Edit` | **— aucun Chronos —** |
| `PostToolUse` | `gsd-context-monitor.js`, matcher `Bash\|Edit\|Write\|MultiEdit\|Agent\|Task` | **— aucun Chronos —** |
| `Notification` | `Chronos-v3.0.2.exe --hook Notification`, **matcher `(aucun)`** | — |
| `Stop`, `UserPromptSubmit`, `SessionEnd` | Chronos v3.0.2 | — |

**Conclusions mécaniques, et elles comptent :**

1. **`PermissionRequest`, `PreToolUse` et `PostToolUse` Chronos sont ABSENTS** de la configuration vivante,
   et `Notification` y est encore **sans matcher**. Les critères 1 à 4 ne peuvent donc pas être observés
   in vivo aujourd'hui. Ce n'est pas une case oubliée : c'est un fait relevé.
2. **Aucune sonde de capture** n'est installée. La phase ne l'a pas fait, et le vérificateur non plus.
3. `PreToolUse` / `PostToolUse` hébergent **réellement** des groupes tiers en position 0 — c'est
   exactement la situation que le test `La_purge_large_epargne_les_hooks_d_un_autre_outil` reproduit.
4. Le magasin `%APPDATA%\Chronos\sessions` : **65 entrées = 53 `.json` + 12 reliquats `.tmp-*`** (compté,
   rien supprimé, rien modifié). `archived.json` = **84 o**. `oauth.dat` = **518 o** — **mtime jamais
   consulté**. Overlay `Chronos-v3.0.2.exe` **pid 119412 vivant** (`tasklist` seul).
5. Le passage de 66 à 65 est **confirmé, et il n'est pas une violation** : le fichier
   `74ac9f48-1fd6-4582-ad54-8c2ff43b557a.json` (21:21:27) porte **l'identifiant de la session courante** —
   les hooks v3.0.2 de l'utilisateur sont vivants et écrivent pendant nos propres sessions. Le dossier a
   d'ailleurs encore été touché à 22:46, après la clôture de la phase. Rien n'a été « réparé ».

---

## 1. Les cinq critères, un par un

| # | Critère | Verdict mécanique | Part in vivo |
|---|---|---|---|
| 1 | « Attend » naît d'une vraie demande ; une alerte d'absence ne fabrique plus d'état | ✅ **PROUVÉ** | ⏳ ouverte |
| 2 | « Réfléchit » ne s'éteint plus tout seul (battements) | ✅ **PROUVÉ** | ⏳ ouverte |
| 3 | Échap couvert — par une **déduction assumée** | ✅ **PROUVÉ** | ⏳ ouverte |
| 4 | Le silence n'affirme rien d'inobservé (critère **amendé** le 2026-09-12) | ✅ **PROUVÉ** | ⏳ ouverte (A11) |
| 5 | Le contrat est écrit, **y compris ce qui n'est pas garanti** | ✅ **PROUVÉ, entièrement livré** | — |

### Critère 1 — vérifié dans le code

`SessionHookInstaller.Cablage` (l. 83-94) porte `new("PermissionRequest", null, "… → à toi (EVT-01)")` et
`new("Notification", "agent_needs_input|elicitation_dialog|elicitation_url_dialog", …)`.
`SessionHookProcessor.Process` route `PermissionRequest → WaitingAttention` (l. 112) et inscrit comme motif
**le nom de l'événement**, jamais un champ non confirmable (l. 128-133).

Le **veto de second rideau** (l. 105-108) écarte les **neuf** types du bus qui ne sont pas des demandes —
`permission_prompt` compris, puisque `PermissionRequest` en a seul la charge. 9 vetoés + 3 laissés
passer = **12**, le bus complet. Sens exact du test, relu : il **n'ajoute jamais** d'état, il n'en retire
que — donc si le nom du champ change, on retombe sur le tri par matcher, **jamais** sur une attente
fabriquée. C'est la bonne doctrine, et elle est implémentée telle quelle.

### Critère 2 — vérifié dans le code

`PreToolUse` / `PostToolUse` câblés **sans matcher**, `timeout: 3`, routés vers `Working` (l. 117-118).
`Un_battement_frais_maintient_en_cours_bien_au_dela_d_une_heure` passe par le **pipeline réel**
(`EcritureEtatSession` → `SessionMonitor.Read`) et porte un **témoin** : un `SessionStart` de 2 h 10 rend
`WaitingDeduced` avant le battement, `Working` après. Non vacueux.

### Critère 3 — vérifié dans le code

`SessionMonitor.TryRead` l. 178-179 : `Working` + silence > 20 min → `WaitingDeduced`. `IsGhost == false`,
`IsTurn == true`, pinceau **ambre** identique à celui d'un `WaitingTurn` du même thème et **différent** du
gris de l'inconnu (`Une_session_deduite_est_visible_ambre_et_dit_sa_deduction`). État **juste ET visible**.

### Critère 4 — vérifié dans le code, y compris dans sa version amendée

- `WaitingDeduced` **ajouté en FIN** d'énumération (`SessionSnapshot.cs` l. 19) : aucun renumérotage.
- **Rang d'urgence 3**, derrière `Working` (2) — `AffichageSessions.Urgence`, cas explicite l. 38.
- Libellé **`"à toi ? déduit"` = 14 caractères** ≤ 16 ; garde `Aucun_libelle_d_etat_ne_depasse_seize_caracteres`.
- **Producteur unique** : `grep '"à toi\|"tour fini\|"en cours\|"inconnu'` sur `SessionsViewModel.cs` →
  **0 ligne**. Remesuré.
- **Jamais écrit dans un fichier d'état** : `WaitingDeduced_n_est_jamais_ecrit_dans_un_fichier_d_etat`
  parcourt le **câblage réel** (8 entrées), obtient 7 états écrits, et asserte l'absence de la chaîne dans
  chacun — plus deux gardes anti-muettes (`Assert.Equal(8, parcourus)`, `Assert.Equal(7, avecEtat)`).
  **C'est une garde, pas une XML-doc.** Point (d) : **tenu**.

### Critère 5 — vérifié dans le document

`docs/hooks-contract.md`, **344 lignes, 9 sections**. Détail au § 6 ci-dessous.

---

## 2. Points de vigilance — ce que le vérificateur a mesuré lui-même

### (a) Sécurité de la configuration vivante — **TENU**, avec une réserve mineure

| Exigence | Mesure |
|---|---|
| Liste blanche des **33 noms**, codée en dur | ✅ `CatalogueEvenementsHooks.Tous` — 3+4+6+2+5+6+5+2 = **33**, recomptés à la main |
| Consultée **AVANT** toute écriture | ✅ `ApplyHooks` l. 207-208 : `EstConnu` puis `AccepteUnMatcher`, `continue` avant toute mutation de la clé |
| `IsChronosGroup` exige `--hook` **ET** `Chronos*.exe` | ✅ `ClaudeSettingsJson.IsChronosCommand` l. 90-94 : marqueur **et** `StartsWith("Chronos") && EndsWith(".exe")`. Test : `La_purge_large_ne_prend_pas_pour_nous_le_hook_d_un_tiers_qui_porte_le_meme_argument` |
| Clé non-`JsonArray` ignorée **sans écriture** (purge) | ✅ `ApplyHooks` l. 192 `continue` — **voir réserve R1** |
| Clé **déjà vide à l'entrée** jamais retirée | ✅ l. 228-229 : seules les clés de l'ensemble `purgees` peuvent disparaître — **voir réserve R2** |
| Groupe tiers **à l'index 0** sur `PreToolUse`/`PostToolUse` | ✅ purge descendante puis `arr.Add(groupe)` → Chronos **en queue**. Test `La_purge_large_epargne_les_hooks_d_un_autre_outil` asserte `[0]` = matcher, commande **et** timeout tiers, sur les deux clés |
| Aucun test n'écrit dans le vrai `~/.claude/` ni le vrai `%APPDATA%\Chronos\` | ✅ **`GetFolderPath` dans `tests/` : 0 occurrence**. `new SessionMonitor()`, `new ArchiveStore()`, `new SessionHookInstaller()` **sans argument** : **0** chacun. Les 6 `new ClaudeSettingsReconciler(` prennent tous des chemins injectés |
| `Aucun_test_ne_cible_le_vrai_settings_du_profil` | ✅ **VERT** (dans les 862) ; il asserte `StartsWith(GetTempPath())` sur `SettingsPath` **et** `BackupDir` |
| Sonde de capture installée | ✅ **0** — vérifié directement sur le fichier vivant (§ 0) |

> **Réserve R1 (mineure, réelle).** La prudence « une clé dont la valeur n'est PAS un tableau est ignorée
> sans être écrite » ne vaut que pour la **purge**. Dans la boucle d'**installation** (l. 210-214), si l'une
> des **huit clés câblées** portait une valeur non-tableau (tiers mal formé), elle serait **écrasée** par un
> `JsonArray` neuf. Le cas est étroit — il faut qu'un tiers ait posé autre chose qu'un tableau sous
> `PreToolUse`, `Stop`, `Notification`… — mais le commentaire du code et le §1 du document présentent la
> prudence comme générale. À corriger d'un `continue`, ou à requalifier en une ligne.
>
> **Réserve R2 (mineure).** Aucun test ne fige les deux prudences : aucune fixture de test ne porte de
> valeur non-tableau sous `hooks`, ni de tableau vide (`grep ':\[\]'` → 0). Les deux comportements sont
> **corrects à la lecture**, mais **non falsifiables**. C'est le seul endroit de la phase où du code touchant
> à la configuration vivante de l'utilisateur n'a pas de garde.

### (b) La parade d'écriture concurrente (B6) — **LE correctif, VÉRIFIÉ PAR REMESURE**

Le mécanisme livré est bien celui décrit : `EcritureEtatSession.EcrireAvecReprise`, **reprise bornée** —
`EssaisMax = 60`, `Thread.Yield()` sur les 12 premiers essais puis `Thread.Sleep(1)`.

| Contrôle | Attendu | **Mesuré par le vérificateur** |
|---|---|---|
| `FileShare.Read` conservé | oui | ✅ l. 104, `FileMode.Create, FileAccess.Write, FileShare.Read` |
| Aucun retour à tmp + `Move` | `File.Move` absent | ✅ **0** occurrence dans `EcritureEtatSession.cs` **et** dans `SessionHookProcessor.cs` |
| Budget borné et cohérent avec `timeout: 3` | oui | ✅ pire cas ≈ 12 `Yield` + 47 `Sleep(1)` ≈ **50 ms**, soit ~1,7 % du délai de grâce d'un `PreToolUse` **bloquant** |
| Le test concurrent existe vraiment | 8 écrivains `Parallel.For` | ✅ `Huit_ecrivains_paralleles_sur_la_meme_session_n_echouent_jamais` — `Parallel.For(0, 8, …)` × 50 = **400**, et le fichier final est **désérialisé**, pas mesuré en taille |

**Remesure indépendante de la thèse « la reprise unique ne corrige pas »** (worktree jetable,
`EssaisMax = 2`, reprise immédiate sans céder la main) :

| Passe | Refus / 400, **reprise UNIQUE** | Refus / 400, **reprise BORNÉE livrée** |
|---|---|---|
| 1 | **62** | **0** |
| 2 | **69** | **0** |
| 3 | **52** | **0** |

**Verdict.** La **direction** rapportée par l'exécuteur est confirmée sans ambiguïté : une reprise unique
laisse des dizaines de refus, la reprise bornée en laisse **zéro**, trois fois sur trois. La **magnitude**
diffère de la sienne (62-69 chez moi contre 209/180 chez lui) — c'est attendu, la contention dépend de la
charge machine à l'instant de la mesure, et aucune des deux séries ne contredit l'autre : **toutes deux sont
strictement positives, et la parade livrée les ramène à zéro**. Le SUMMARY n'a pas maquillé le chiffre.

### (c) Le veto sous-agent — **ÉTANCHE, et REJOUÉ**

- **Placement** : l. 95-97, soit **après** la garde `session_id` (l. 81) et **avant** le court-circuit
  `SessionEnd` (l. 99). ✅
- **Un sous-agent peut-il encore supprimer le fichier d'état de son parent ?** **Non.** `SessionEnd` n'est
  pas dans `("PermissionRequest" or "Notification")` → `Ignored` avant d'atteindre la l. 99.
- **Les deux exceptions ne produisent-elles que `WaitingAttention` ?** ✅ `PermissionRequest` et
  `Notification` sont les seuls cas du `switch` qui leur soient atteignables, et tous deux rendent
  `WaitingAttention`. Un `Notification` de type vetoé rend `Ignored`. Aucun chemin vers `Working`,
  `WaitingTurn` ou `Delete`.
- **Mutation rejouée par le vérificateur** (`var estSousAgent = false && (…); // MUTANT`, worktree jetable) :

```
Échoué!  - échec : 5, réussite : 8, total : 13
  Un_SessionEnd_de_sous_agent_ne_supprime_pas_le_fichier_d_etat_du_parent   [FAIL]
  …_est_ignore(ev: "PostToolUse",      marqueur: "agent_type":"code-reviewer") [FAIL]
  …_est_ignore(ev: "PostToolUse",      marqueur: "agent_id":"a-1")             [FAIL]
  …_est_ignore(ev: "UserPromptSubmit", marqueur: "agent_id":"a-1")             [FAIL]
  …_est_ignore(ev: "Stop",             marqueur: "agent_id":"a-1")             [FAIL]
```

**Exactement 5 cas, dont celui du `SessionEnd` parent.** La mesure de fin de 25-02 est **reproduite**.

### (d) EVT-04 ne se présente jamais comme une observation — **TENU**

Voir critère 4 ci-dessus. Les quatre sous-points (fin d'énumération, rang 3, 14 caractères, garde
d'écriture) sont tenus, et le quatrième **par une garde exécutable**, pas par un commentaire.

### (e) La matrice de rendu VOIT le nouvel état — **TENU**

`SessionStylesBindingTests` construit **cinq** snapshots (l. 51-56), dont `WaitingDeduced` avec le libellé
le plus long, et asserte **5 aux deux endroits** : `Assert.Equal(5, vm.Items.Count)` (l. 61, garde
anti-fenêtre-vide) et `Assert.Equal(5, separateurs)` (l. 135). La matrice **8 × 9** est gardée
anti-muette (`Assert.Equal(8, styles.Length)`, `Assert.Equal(9, themes.Count)`).

XAML, depuis `ce40e99` : `git diff --numstat` → **`1 1 src/Chronos/Resources/SessionStyles.xaml`**, et ce
fichier seul. Diff complet relu : **une ligne de commentaire de tête**, rien d'autre. **Aucune** ligne
`DataTemplate`, `Style`, `Trigger` ni `x:Key`.

### (f) Le cas d'égalité à la milliseconde — **FIGÉ SANS ÊTRE MODIFIÉ**

`git diff --stat ce40e99..HEAD -- ArbitrageSessions.cs LectureSessions.cs` → **VIDE**.

Le test `Un_hook_deduit_et_un_transcript_travaillant_du_MEME_instant_sont_departages_par_la_source` **n'est
pas vacueux** : il asserte l'état retenu (`WaitingDeduced`), les deux faces du désaccord (`SourceRetenue` =
Hook, `SourceEcartee` = Transcript) **et** `EcartAge == TimeSpan.Zero` — c'est cette dernière assertion qui
prouve qu'on est bien dans l'égalité stricte et non dans un hasard d'ordonnancement.

### (g) La garde de non-dérive — **FALSIFIABLE, REJOUÉE PAR LE VÉRIFICATEUR**

Mutation rejouée (worktree jetable, **hors dépôt**) : altération de la **SEULE troisième cellule** de la
ligne `Stop` du §1 — `| \`Stop\` | (aucun) | MUTANT le tour se termine → tour fini | 10 |`. Nom, `matcher` et
`timeout` **intacts**.

```
Échoué!  - échec : 1, réussite : 6, total : 7
  Chronos.Tests.ContratHooksDocumenteTests.Chaque_ligne_documentee_porte_le_role_reellement_cable [FAIL]
```

**Exactement 1 échec sur 7, et c'est le bon.** `La_table_documentee_liste_EXACTEMENT_les_evenements_cables`
reste **verte** — ce qui est précisément le point : **sans cette garde, une description fausse passerait
entièrement inaperçue.** La mesure du SUMMARY 25-04 est **reproduite à l'identique**.

### (h) `docs/hooks-contract.md` — **CONFORME AU RÉELLEMENT CÂBLÉ**

| Exigence | Mesure |
|---|---|
| Les **trois trous** au **§5** | ✅ 5.1 Échap / 5.2 `SessionEnd` (`prompt_input_exit`) / 5.3 nom inconnu + liste blanche |
| Date `2026-09-12` **découpée dans la section §5** | ✅ **4 occurrences dans le §5 lui-même**, 5 dans le fichier. Le test découpe la section (`SectionNonGarantie`) : une date en tête ne daterait rien |
| La limite d'EVT-03 est une **limite**, pas un défaut | ✅ §5.4, en toutes lettres : « **C'est une LIMITE, pas un défaut à corriger** », et la conclusion est de rouvrir l'**événement porteur**, pas le seuil |
| §3 porte la divergence transmise à la phase 26 | ✅ « ⚠ Divergence connue — transmise à la phase 26, NON corrigée ici », avec la conséquence concrète explicitée |
| §6 porte la question de la sonde et le `timeout: 3` | ✅ « ⚠ Question OUVERTE, à poser à l'utilisateur » + « Pourquoi deux `timeout` et pas un » |
| Le document décrit le **réellement câblé**, dont la reprise **bornée** | ✅ §6 : « la parade livrée est une **reprise BORNÉE** avec cession de la main (**soixante essais au plus**) » — c'est bien `EssaisMax = 60`, **pas** la reprise unique du plan |

> **Réserve R4 (cosmétique).** Le §3 du document nomme le prédicat `IsWaiting`, **l. 38** — c'est
> **exact** (vérifié : `SessionTreatmentTracker.cs:38 private static bool IsWaiting`). C'est
> `25-VALIDATION.md` et le brief de vérification qui l'appellent `EstAttente` l. 39. **Le document livré a
> raison, l'artefact de planification a tort.** Rien à corriger dans `docs/`.

### (i) Acquis des phases 20 à 24 — **TOUS TENUS**

| Preuve | Attendu | **Remesuré** |
|---|---|---|
| diff `ArbitrageSessions.cs`, `LectureSessions.cs` depuis `ce40e99` | VIDE | **VIDE** |
| diff `SessionTreatmentTracker.cs`, `TreatedStore.cs`, `ArchiveStore.cs` | VIDE | **VIDE** |
| `?? new ` dans `DiagnosticService.cs` | 2 | **2** |
| `new SessionMonitor` dans `DiagnosticService.cs` | 0 | **0** |
| `MotifMasquage` dans `DiagnosticService.cs` | 3 | **3** |
| `=> Inspecter(now).Visibles;` dans `SessionMonitor.cs` | 1 | **1** |
| `byId[` dans `SessionMonitor.cs` | 0 | **0** |
| `ArbitrageSessions.Trancher(` | 1 | **1** |
| `StaleWorking` en `.cs` sous `src/` | 0 | **0** (les 5 correspondances sont des **binaires `bin/obj` Release périmés** — ce qui confirme au passage que **l'exe n'a pas été republié**) |
| `FromMinutes(20)` / `FromHours(8)` | 1 / 1 | **1 / 1** — la valeur du seuil **n'a pas bougé**, seul son **nom** a changé |
| `SilenceDesBattements` | ≥ 3 | **4** |
| `agent_id` / `agent_type` / `NotificationsSansEtat` dans le processeur | ≥1 / ≥1 / ≥2 | **2 / 2 / 2** |
| `WaitingDeduced` : Snapshot / Affichage / Monitor / StylesBinding | ≥1 / ≥2 / ≥1 / ≥1 | **1 / 2 / 2 / 1** |
| `NormalisationUnique` + `ServicesLayerPurity` + `GardesDoctrine` + `CompositionRoot` + `GardesPerimetre` | 3+2+8+5+10 = 28, 0 échec | **28 / 0 échec**, exécutés séparément |

### (j) La suite, exécutée par le vérificateur — **DEUX FOIS**

```
dotnet test Chronos.sln -c Debug --nologo -v q
passe 1 : Réussi!  - échec : 0, réussite : 862, ignorée(s) : 0, total : 862, durée : 6 s
passe 2 : Réussi!  - échec : 0, réussite : 862, ignorée(s) : 0, total : 862, durée : 6 s
```

**862 / 0 échec / 0 ignoré, deux fois.** Le chiffre du SUMMARY est **exact** et la course du chargeur BAML
ne se manifeste pas. Aucun `Skip`.

---

## 3. Couverture des exigences

| Exigence | Verdict | Preuve remesurée |
|---|---|---|
| **EVT-01** | ✅ SATISFAITE | `PermissionRequest` au câblage + routage vers `WaitingAttention` + motif = nom de l'événement ; `Le_cablage_installe_bien_le_groupe_PermissionRequest` |
| **EVT-02** | ✅ SATISFAITE | matcher à 3 types + veto sur 9 ; `permission_prompt` explicitement **écarté des deux côtés**, et la raison écrite au §6 |
| **EVT-03** | ✅ SATISFAITE | 2 battements `timeout: 3`, veto sous-agent **rejoué : 5 cas**, reprise bornée **rejouée : 0/400 ×3** |
| **EVT-04** | ✅ SATISFAITE | `WaitingDeduced` en fin d'énum, rang 3, 14 car., ambre, **jamais persisté** (garde exécutable) |
| **EVT-05** | ✅ SATISFAITE | `docs/hooks-contract.md` 344 l. / 9 §, garde **rejouée : 1 échec/7, le bon** |

Aucune exigence **orpheline** : `REQUIREMENTS.md` mappe EVT-01..05 à la phase 25, et les quatre plans les
réclament tous.

---

## 4. Ce qui est prouvé mécaniquement vs ce qui ne peut l'être qu'in vivo

### Prouvé mécaniquement, ici, par le vérificateur

- Le **routage** des huit événements, le **veto sous-agent** et le **veto de notification**.
- La **validation par liste blanche avant écriture**, et l'**innocuité envers les groupes tiers**.
- La **parade d'écriture concurrente**, remesurée dans les deux configurations.
- La **non-persistance** de `WaitingDeduced`, par une garde qui parcourt le câblage réel.
- La **non-dérive du document**, par mutation rejouée à la troisième cellule.
- L'**intégrité du périmètre** : 5 fichiers à diff vide, 1 XAML à 1/1 ligne de commentaire, 0 dépendance.
- **862 tests, 0 échec, deux fois.**

### Impossible à prouver avant republication de l'exe **ET** réconciliation de `settings.json`

La chaîne de préalables est **mesurée ouverte au § 0** : (a) republier l'exe, (b) le lancer — la
réconciliation n'a lieu qu'au **lancement en mode overlay**, (c) rouvrir les sessions Claude Code, la
configuration des hooks étant lue au **démarrage** d'une session.

1. **Critères 1 à 4 in vivo** (demande de permission réelle, session longue, Échap, absence de fausse
   annonce de fin).
2. **A11 — l'effet de masse réel.** Le succédané déterministe est **mesuré et honnête** :
   **20 sessions sur 54 visibles** basculent ensemble, `WaitingCount` = **42**, sur un corpus de 66 états —
   et le test dit lui-même que ce nombre est **une propriété du corpus autant que du seuil**, le magasin
   réel étant aujourd'hui un cimetière (ses 53 `.json` sont tous au-delà de huit heures, d'où **zéro**
   in vivo aujourd'hui). Le corpus place 12 `Working` juste sous le seuil et 20 juste au-dessus : il
   mesure le **pire cas** d'un magasin vivant. **Les deux lignes in vivo d'A11 restent à la charge de
   l'utilisateur.**
3. **Lisibilité** de « à toi ? déduit » sur 8 gabarits × 9 thèmes — la matrice BAML prouve que rien ne
   dégénère en disposition, pas que l'œil lit bien.
4. **La question de la sonde** de capture, à arbitrer par l'utilisateur (§6 du contrat).

---

## 5. Réserves — aucune ne bloque

| # | Gravité | Réserve | Où |
|---|---|---|---|
| **R1** | mineure | La prudence « clé non-tableau intouchée » ne vaut que pour la **purge** : à l'installation, une des 8 clés câblées portant une valeur non-tableau serait **écrasée**. Étroit, mais le commentaire et le §1 le présentent comme général | `SessionHookInstaller.cs:210-214` |
| **R2** | mineure | Les **deux prudences** de la purge (valeur non-tableau, clé déjà vide) sont **correctes à la lecture mais non falsifiables** — aucune fixture ne les exerce | `tests/…/SessionsTests.cs` |
| **R3** | cosmétique | `Les_cinq_noms_cables_aujourd_hui_sont_tous_connus` fige **cinq** noms en dur alors que le câblage en compte **huit**. Inoffensif — `Install_pose_un_groupe_par_evenement_cable` couvre les huit — mais le nom ment | `CatalogueEvenementsHooksTests.cs:100` |
| **R4** | cosmétique | `25-VALIDATION.md` nomme le prédicat `EstAttente` l. 39 ; le code dit `IsWaiting` l. 38. **`docs/hooks-contract.md` §3 est exact** | artefact de planification |

---

## 6. Verdict

**`human_needed`** — et c'est la formulation juste, pas un compromis.

Les **cinq critères** du ROADMAP sont **prouvés**, le cinquième intégralement. **Aucun gap bloquant**, aucun
stub, aucun câblage mort, aucun écart entre ce que les SUMMARY affirment et ce que le code contient — les
trois mutations rejouées indépendamment donnent **1/7**, **5/13** et **0 contre 52-69 / 400**, c'est-à-dire
exactement les comptes annoncés, à la magnitude du bruit de contention près.

Ce qui empêche `complete`, ce n'est pas un défaut de la phase : c'est que **la configuration vivante ne
porte pas encore le contrat livré** (§ 0, mesuré). Les quatre premiers critères gardent une moitié qui ne
s'observe qu'à l'écran, après republication et réconciliation. La phase s'est interdit ces gestes, et le
vérificateur aussi.

**Sécurité pendant la vérification :** `~/.claude/settings.json` **lu, jamais écrit** (md5 inchangé,
`78eb517…`) ; aucune sonde ; `%APPDATA%\Chronos\sessions` **compté, jamais modifié** ; `archived.json`
intact (84 o) ; `oauth.dat` **taille seule** (518 o), mtime jamais consulté ; overlay pid **119412** ni
lancé ni tué ; aucune requête réseau. Le worktree de mutation était **hors du dépôt**, il est **révoqué** :
`git worktree list` réduit au dépôt principal, `git status --porcelain` **vide**,
`grep -rlF "MUTANT" --include=*.cs` → **0**.

---

_Vérifié le 2026-09-12 — Claude (gsd-verifier). Aucun chiffre repris d'un SUMMARY sans remesure._
