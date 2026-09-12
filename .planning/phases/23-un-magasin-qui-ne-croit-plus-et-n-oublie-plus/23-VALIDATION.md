---
phase: 23
slug: un-magasin-qui-ne-croit-plus-et-n-oublie-plus
status: validated
nyquist_compliant: true
wave_0_complete: n/a
created: 2026-09-12
validated: 2026-09-12
---

# Phase 23 — Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (net8.0-windows) — `tests/Chronos.Tests` |
| **Full suite command** | `dotnet test Chronos.sln -c Debug --nologo -v q` |
| **Quick run (magasin de sessions)** | `… --filter "FullyQualifiedName~EcritureEtatSession\|FullyQualifiedName~BalayageMagasinSessions\|FullyQualifiedName~Sessions\|FullyQualifiedName~Inspection\|FullyQualifiedName~GardesPerimetre\|FullyQualifiedName~CompositionRoot\|FullyQualifiedName~Diagnostic"` |
| **Baseline d'entrée de phase** | **747 tests / 0 échec / ~4 s** (fin de phase 22) |
| **Cible de fin de phase** | **0 échec, aucun test supprimé**, total **> 747** — environ **+14** attendus (6 en 23-01, 8 en 23-02). Chiffre INDICATIF : le critère est « 0 échec + aucune suppression », pas un total. |
| **Total mesuré après 23-01** | **754 / 0 échec / ~4 s** (+7 : 6 en T1, 1 garde de câblage en T2) |
| **Total mesuré après 23-02** | **763 / 0 échec / ~4 s** (+9 : 8 en T1, 1 garde de câblage en T2) — soit **+16** sur la phase, au-dessus des +14 prévus |
| **Deux exécutions consécutives** | **763 / 763**, 0 échec les deux fois, 4 s chacune |

## Le critère de cette phase n'est PAS un chiffre de couverture

Cette phase **n'enlève aucun code** et ne supprime aucun test. Le critère opérationnel :

> **0 échec, total > 747, aucun test supprimé, et les quatre critères de succès du ROADMAP prouvés un à un.**

Un total inférieur à 747 doit être expliqué **nominativement**, jamais absorbé en ajustant le chiffre attendu.
La phase 21 a fait exception (recul assumé, justifié test par test) ; ce n'est pas le cas ici.

**Résultat : 763 > 747, aucun test supprimé, aucun renommage.** L'écart de +2 par rapport aux +14 annoncés
vient des **deux gardes de câblage** (une par plan), que le chiffre indicatif du plan de phase ne comptait pas.

## Ce qui rendrait cette phase creuse

Deux façons de « satisfaire » CYC-01 se ressemblent le jour de la livraison et n'ont rien à voir à l'usage :

| Livraison | Ce qu'on lit le jour J | Ce qui se passe au bout de trois jours |
|---|---|---|
| **Balayer par l'âge seul** | le dossier a fondu, tout va bien | une session réellement vivante depuis plusieurs jours a été **effacée** |
| **Balayer sur un critère double** — vieux **et** sans attestation de vie | le dossier a fondu | la session vivante est toujours là |

Et deux façons de « satisfaire » le critère n°4 :

| Livraison | Ce que voit l'utilisateur | Ce que ça dit |
|---|---|---|
| Le balayage marque la session comme traitée / archivée | elle disparaît | **un verdict a été rendu** sur une session dont on ne sait plus rien |
| Le balayage supprime le fichier, sans rien écrire ailleurs | elle disparaît | expirer, c'est ne plus savoir |

Les deux livraisons produisent le **même écran**. Seules les preuves structurelles les distinguent :

```sh
grep -cF "TreatedStore"  src/Chronos/Services/BalayageMagasinSessions.cs    # attendu : 0
grep -cF "SessionActivity" src/Chronos/Services/BalayageMagasinSessions.cs  # attendu : 0
grep -cF "DateTimeOffset.UtcNow" src/Chronos/Services/BalayageMagasinSessions.cs   # attendu : 0
```

**Mesuré : 0, 0, 0.** Plus `ArchiveStore` → **2**, et les deux lignes relues une à une (l. 9 et l. 24) :
ce sont les deux `<see cref="ArchiveStore.PurgerPrefixe"/>` du XML-doc qui citent le précédent — **0 usage
de code**. Plus la garde par réflexion `Le_balayage_ne_connait_aucun_magasin_de_verdict`, insensible au texte
des commentaires : **verte**.

Le versant comportemental, qui ne se déduit pas du versant structurel :
`Un_etat_balaye_disparait_sans_etre_declare_traite_ni_archive` — après balayage, `Inspecter` rend `Visibles`
**vide**, `Masquees` **vide**, `FichiersEcartesParAnciennete == 0`, et **aucun** des deux fichiers de magasin
de verdict n'a été créé.

## Le risque n°1 est la compilation, pas la logique

`tests/Chronos.Tests` porte un `ProjectReference` vers `Chronos`. Une seule erreur de compilation, où que ce
soit, fait échouer **toute** l'invocation `dotnet test`, pas le test concerné. Les étapes ROUGE se jouent donc
contre un **squelette compilable** (`NotImplementedException`), jamais contre un type absent — précédents
18-01 et 22-03 (CS0136 sur un itérateur nommé `e`).

**Appliqué deux fois, vérifié deux fois.** 23-01 : 6 échecs / 0 succès. 23-02 : **7 échecs / 1 succès**, build
**0 erreur / 0 avertissement** au commit `2859afc`. Le succès isolé est la garde par **réflexion** : elle est
structurelle et non comportementale, donc le squelette la satisfait déjà (il ne porte aucun champ ni paramètre
de type de verdict, et expose bien une `IClock`). Les **7** autres échouent sur le comportement, pas sur la
compilation — c'est ce que l'étape ROUGE doit établir.

## Pourquoi deux vagues strictement sérielles

23-01 et 23-02 se disputent `src/Chronos/App.xaml.cs` **et** `tests/Chronos.Tests/GardesPerimetreTests.cs` :
la propriété exclusive des fichiers impose l'ordre. Il y a en outre une dépendance de fond : le balayage date
les fichiers par leur `updated_at`, et 23-01 est ce qui rend ces dates fiables.

## Carte de vérification par tâche

| Plan | Tâche | Ce qu'elle doit rendre vrai | Statut |
|---|---|---|---|
| 23-01 | T1 | Écriture directe testable, résultat porteur de sa cause, 200 écritures sous lecteur concurrent sans perte | **✅ 0 échec sur 200 écritures**, `updated_at` final = dernier écrit |
| 23-01 | T2 | `--hook` consomme le service, signale sur le flux d'erreur, sort 1 (jamais 2), garde de câblage falsifiée | **✅** `return 2;` → 0 occurrence ; mutation jouée et révoquée |
| 23-02 | T1 | Deux gestes distincts, horloge injectée, critère double, balayer ne conclut rien | **✅ 8/8 verts**, horloge système → 0 occurrence dans le service ET ses tests |
| 23-02 | T2 | Balayage câblé au démarrage sur le dossier du moniteur, deux gardes, `DiagnosticService.cs` hors du diff | **✅** `DiagnosticService.cs` absent du diff de phase ; mutation jouée et révoquée |
| 23-02 | T3 | Cette carte, remplie et mesurée | **✅** (ce document) |

## Sampling Rate

Le seul mécanisme périodique de cette phase est le balayage, et il n'est **pas** périodique : une exécution
par lancement de l'overlay. Aucun échantillonnage à régler. La cadence de lecture du widget (2 s) est
inchangée, et c'est elle qui borne la fenêtre pendant laquelle une écriture directe pourrait être lue
fragmentaire.

## Critères de succès du ROADMAP — preuves et résultats

| # | Critère | Preuve nommée | Résultat |
|---|---|---|---|
| 1 | **Le magasin se résorbe tout seul** (54 états dont 48 > 7 j, + 12 débris) | `Un_etat_perime_et_sans_attestation_est_retire_un_etat_encore_affichable_reste`, `Les_debris_temporaires_partent_et_un_debris_tout_frais_est_epargne` + garde de câblage `Le_demarrage_balaie_le_magasin_de_sessions_sur_le_dossier_du_moniteur` — **in vivo : à vérifier par l'utilisateur** | **MÉCANISME PROUVÉ.** 4 états (40 j, 10 j, 10 h, 1 h) → `EtatsRetires == 2`, `EtatsConserves == 2` ; les 5 débris du cas mesuré (même `session_id`, 5 pid) partent tous les cinq, le débris de 2 min reste. Le câblage au démarrage est prouvé falsifiable. **Le dossier réel de l'utilisateur est INCHANGÉ (66 entrées)** : c'est le code livré qui le balaiera, au prochain lancement volontaire — voir la section de vérification in vivo, point 1 |
| 2 | **Un terminal tué ne laisse plus de trace éternelle** | même chaîne : aucun événement de fin n'est requis, seuls l'âge et l'absence d'attestation tranchent — **in vivo : à vérifier par l'utilisateur** | **MÉCANISME PROUVÉ, par construction ET par mesure.** `grep -c "SessionEnd" src/Chronos/Services/BalayageMagasinSessions.cs` → **0** : le balayage ne consulte aucun événement de fin. Les deux tests du critère n°1 suppriment des fichiers dont aucun `SessionEnd` n'a jamais été reçu. **Non observable ici in vivo** : cela demande de tuer un terminal puis d'attendre 72 h |
| 3 | **Une écriture qui échoue se voit** (200/500 aujourd'hui, 0/500 en direct) | `Sous_un_lecteur_concurrent_aucune_ecriture_n_est_perdue`, `Un_echec_d_ecriture_est_rendu_avec_sa_cause_et_ne_leve_jamais`, garde `Le_mode_hook_ecrit_par_le_service_teste_et_signale_son_echec` | **✅ (23-01)** 200 écritures sous lecteur tenant la cible en `FileShare.ReadWrite` → **0 échec**, contenu final = dernier `updated_at` écrit. La cause est la RÉELLE (`ex.GetType().Name + " : " + ex.Message`), publiée sur le flux d'erreur en UTF-8 strict, code de sortie **1 et jamais 2** |
| 4 | **Le balayage ne ment pas** : balayé ≠ terminé ≠ traité, et une session vivante depuis plusieurs jours survit | `Un_etat_balaye_disparait_sans_etre_declare_traite_ni_archive`, `Une_session_vivante_depuis_des_jours_survit_au_nettoyage`, `Le_balayage_ne_connait_aucun_magasin_de_verdict` | **✅ TENU PAR TROIS PREUVES DE NATURES DIFFÉRENTES.** *Comportement* : après balayage, `Visibles` vide **ET** `Masquees` vide, aucun fichier de verdict créé. *Critère double* : un état de **40 jours** dont la session est attestée vivante → `EtatsRetires == 0`, fichier intact. *Structure* : la garde par réflexion ne voit aucun champ ni paramètre de type de verdict, et exige une `IClock` au constructeur |

## Falsification des gardes — obligatoire, pas facultative

Une garde qui ne peut pas échouer ne garde rien (précédents 21-03, 21-04). Chaque garde de câblage est
**mutée, vue échouer, puis révoquée** avant d'être committée.

| Garde | Mutation | Échec constaté | Révocation vérifiée |
|---|---|---|---|
| `Le_mode_hook_ecrit_par_le_service_teste_et_signale_son_echec` | `EcritureEtatSession.Appliquer(` → `…AppliquerMUTANT(` | **`Chronos.Tests.GardesPerimetreTests.Le_mode_hook_ecrit_par_le_service_teste_et_signale_son_echec` [FAIL]** — 1 échec / 6 succès sur le filtre `GardesPerimetre` | **oui** : `git diff` du service vide, `grep -cF "EcritureEtatSession.Appliquer(" src/Chronos/App.xaml.cs` → **1** |
| `Le_demarrage_balaie_le_magasin_de_sessions_sur_le_dossier_du_moniteur` | `.Balayer()` → `.BalayerMUTANT()` | **`Chronos.Tests.GardesPerimetreTests.Le_demarrage_balaie_le_magasin_de_sessions_sur_le_dossier_du_moniteur` [FAIL]** — 1 échec / 7 succès sur le filtre `GardesPerimetre` | **oui** : `git diff --stat` du service **vide**, `grep -cF "GetRequiredService<BalayageMagasinSessions>().Balayer()" src/Chronos/App.xaml.cs` → **1**, `grep -c MUTANT` → **0** dans les deux fichiers |

**Précision de méthode, valable pour les deux mutations.** Telles quelles, elles ne compilent pas — et une
erreur de compilation ferait échouer *toute* l'invocation `dotnet test`, ce qui ne dirait **rien** de la
garde. Chaque mutation a donc été jouée avec un **alias temporaire** dans le service
(`public BilanBalayage BalayerMUTANT() => Balayer();`), de sorte que le dépôt reste compilable et que l'échec
observé soit bien celui de la garde. Alias et mutation révoqués ensemble.

## Gardes à ne pas casser

| Garde | Attendu | Résultat |
|---|---|---|
| `ServicesLayerPurityTests` | 0 échec | **0 échec / 2 tests** |
| `CompositionRootTests` | 0 échec | **0 échec / 5 tests** (le miroir DI a gagné le balayeur ET un dossier temporaire) |
| `NormalisationUniqueTests` (balaie `Services/`, commentaires compris) | 0 échec | **0 échec / 3 tests** — `BalayageMagasinSessions.cs` n'est **pas exempté** et n'en a pas eu besoin : conversion d'époque par `UsageNormalization.InstantDepuisEpochMillisecondes` (1 occurrence), `FromUnixTimeMilliseconds` (0) |
| `GardesDoctrineTests` | 0 échec | **0 échec / 8 tests** |
| `La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte` | verte | **verte** (1/1) |
| Gardes de périmètre phase 21 | 0 échec | **0 échec / 8 tests** (6 héritées + 1 de 23-01 + 1 de 23-02) |
| `grep -cF "new SessionMonitor" src/Chronos/Services/DiagnosticService.cs` (phase 22) | 0 | **0** |
| `grep -cF "=> Inspecter(now).Visibles;" src/Chronos/Services/SessionMonitor.cs` (phase 22) | 1 | **1** |
| `grep -cF "?? new " src/Chronos/Services/DiagnosticService.cs` (phase 20) | 2 | **2** |
| Durée de la suite | ~4 s (acquis phase 20) | **4 s** (763 tests) |

### Chiffres de `grep` réellement obtenus — et non ceux prédits

Leçon 22-03 : un écart se documente, il ne s'absorbe pas.

| Contrôle | Prédit par le plan | Mesuré |
|---|---|---|
| `public BalayageMagasinSessions(string dossier, ISessionSource attestationDeVie, IClock horloge)` | 1 | **1** |
| `_horloge.UtcNow` dans le service | 1 | **1** |
| `DateTimeOffset.UtcNow` dans le service | 0 | **0** |
| `DateTimeOffset.UtcNow` dans son fichier de tests | 0 | **0** |
| `new FakeClock(Maintenant)` dans les tests | 1 | **1** |
| `Assert.StartsWith(Path.GetTempPath()` dans les tests | ≥ 2 | **3** |
| `TreatedStore` / `SessionActivity` / `FromUnixTimeMilliseconds` dans le service | 0 / 0 / 0 | **0 / 0 / 0** |
| `ArchiveStore` dans le service | 2 (XML-doc seulement) | **2**, l. 9 et l. 24, relues : **0 usage de code** |
| `GetRequiredService<BalayageMagasinSessions>().Balayer()` dans `App.xaml.cs` | 1 | **1** |
| `GetRequiredService<SessionMonitor>().Directory` dans `App.xaml.cs` | 1 | **1** |
| `GetRequiredService<SessionMonitor>()` dans `App.xaml.cs` | 3 | **3** |
| `new BalayageMagasinSessions(` dans `App.xaml.cs` | 1 | **1** |
| `Balayer()` dans `CompositionRootTests.cs` | 0 | **0** — la garde DI ne balaie rien |
| `Path.GetTempPath()` dans `CompositionRootTests.cs` | augmente de ≥ 2 | **15 → 17** (+2 : dossier de sessions + assertion anti-accident) |

## Preuve que le diagnostic suit SANS retouche

C'est la vérification décisive du placement de la phase 22 : le rapport doit refléter le balayage **sans
qu'une ligne de `DiagnosticService.cs` ne change**.

```sh
git diff --name-only 025538f..HEAD -- src/Chronos/Services/DiagnosticService.cs   # attendu : vide
```

| Contrôle | Attendu | Résultat |
|---|---|---|
| `DiagnosticService.cs` dans le diff de la phase | **absent** | **absent** (sortie vide) |
| `SessionMonitor.cs` dans le diff de la phase | **absent** | **absent** (sortie vide) |

Diff **complet** de la phase sous `src/` et `tests/` (base `025538f`) — sept fichiers, aucun de plus :
`App.xaml.cs`, `Services/BalayageMagasinSessions.cs`, `Services/EcritureEtatSession.cs`,
`BalayageMagasinSessionsTests.cs`, `CompositionRootTests.cs`, `EcritureEtatSessionTests.cs`,
`GardesPerimetreTests.cs`.

## Ce que cette phase NE fait PAS — et qu'il ne faut pas vérifier ici

| Hors périmètre | Pourquoi | Preuve de non-anticipation |
|---|---|---|
| Fusion par fraîcheur | phase 24 (FUS-01/02) | `SessionMonitor.cs` **absent du diff** : l'arbitrage reste par ordre d'insertion |
| Contrat d'événements (`PermissionRequest`, battements de cœur, interruption) | phase 25 | `SessionHookProcessor.cs` et `SessionHookInstaller.cs` **absents du diff** : 5 hooks, `SessionEnd` compris, inchangés |
| « Traité » et TTL d'`ArchiveStore` | phase 26 | `TreatedStore.cs` et `ArchiveStore.cs` **absents du diff** |
| **`ArchiveStore.Add` garde son écriture par fichier temporaire** | CYC-02 porte sur « l'écriture d'un **état de hook** ». `Add` n'a **aucun canal** pour rendre un échec constatable (geste de menu, valeur de retour ignorée) : changer sa mécanique sans lui donner de canal produirait un correctif **invérifiable** — exactement le genre de geste que ce milestone combat. Relève de l'**optimisation fine des écritures concurrentes**, hors périmètre v1.6 (poids mesuré ~0,7 événement perdu sur 5 000). | `ArchiveStore.cs` **absent du diff** |

## Limites assumées — à écrire, pas à taire

1. **Contrepartie de l'écriture directe.** Elle tronque la cible avant de la réécrire : un lecteur
   malchanceux peut lire un fragment et ignorer le fichier pendant **un** cycle de 2 s. À comparer à ce
   qu'elle remplace : une perte **définitive**, dans 290 cas sur 500 sous lecteur serré. Ce n'est pas le même
   ordre de gravité — et la phase 24 ne pourrait pas se permettre l'ancien défaut, puisqu'elle arbitrera en
   comparant des horodatages : un état perdu paraît **plus vieux qu'il n'est**.
2. **Le balayage s'exécute une fois par lancement.** Sur une machine qui reste allumée des semaines, le
   magasin croît entre deux lancements ; la borne est reprise au lancement suivant. **Condition qui rendrait
   ce choix faux** : constater, au prochain lancement, un magasin contenant nettement plus que quelques jours
   d'états — c'est-à-dire une machine qui reste allumée plus longtemps que la fenêtre de 72 h sans jamais
   relancer l'overlay. Si cela se produit, le remède n'est pas d'abaisser le seuil (on effacerait des sessions
   affichables) mais de rendre le balayage périodique.
3. **L'attestation de vie vient des transcripts.** Une source en panne n'atteste rien, et le balayage retombe
   alors sur le seul critère d'âge (72 h). C'est le cas le moins favorable, et il est assumé : 72 h sans le
   moindre événement de hook **et** sans transcript lisible.
4. **Un fichier d'état illisible est daté par sa date d'écriture**, jamais par une date inventée — et si même
   cette date est inaccessible, le fichier n'est **jamais** balayé : ne pas savoir quand un fichier a été
   écrit n'autorise pas à le supprimer.

## Invariants de sécurité — à vérifier et à consigner

| Invariant | Attendu | Résultat |
|---|---|---|
| `ls "$APPDATA/Chronos/sessions" \| wc -l` | **66**, INCHANGÉ (c'est le code LIVRÉ qui les balaiera) | **66**, vérifié avant et après chaque tâche |
| `stat -c '%s' "$APPDATA/Chronos/archived.json"` | **84** octets | **84** |
| `stat -c '%s' "$APPDATA/Chronos/oauth.dat"` | **518** octets — **taille SEULE, jamais le mtime** (le rafraîchissement préventif de la phase 17 le fait bouger toutes les 60 s) | **518** (mtime jamais consulté) |
| Overlay `Chronos-v3.0.2.exe` (pid 119412) | vivant, **ni lancé ni tué** | **vivant** (`tasklist` seul, aucune action) |
| `git diff -- '*.csproj'` | vide (aucune dépendance NuGet) | **vide** |
| `git status --porcelain` | rien hors `src/`, `tests/`, `.planning/` | **conforme** |
| Requêtes réseau réelles depuis un test | 0 | **0** — les tests de balayage ne lisent que des dossiers temporaires |

**Garde anti-accident, à trois niveaux, parce que ce code SUPPRIME des fichiers :**
`Assert.StartsWith(Path.GetTempPath(), …)` sur le dossier créé, **puis** sur `balayeur.Dossier` **avant**
tout appel à `Balayer()`, **plus** la correction du miroir DI de `CompositionRootTests` — il enregistrait
`new SessionMonitor(null, …)`, dont le dossier est le **vrai** `%APPDATA%\Chronos\sessions` ; il reçoit
désormais un dossier temporaire, et `Balayer()` n'y est jamais appelé (**0** occurrence dans ce fichier).

## À VÉRIFIER PAR L'UTILISATEUR

Ce que seul un lancement volontaire peut établir — la phase s'interdit de lancer ou de tuer l'overlay, et
aucun test ne touche aux données réelles. Le test unitaire prouve le **mécanisme** ; il ne prouve pas que le
dossier de l'utilisateur a changé (leçon 21-04, `archived.json`).

1. **Critère n°1, in vivo — le magasin se résorbe.** Avant / après le prochain lancement de la version du
   dépôt :

   ```sh
   ls "$APPDATA/Chronos/sessions" | wc -l          # avant : 66
   ls "$APPDATA/Chronos/sessions"/*.json | wc -l   # avant : 54
   ls "$APPDATA/Chronos/sessions" | grep -c 'tmp-' # avant : 12
   ```

   Attendu après : **0 fichier temporaire**, et seuls subsistent les états de moins de 72 h ou attestés
   vivants (ordre de grandeur : quelques unités sur 54 — 51 des 54 ont plus de 24 h et 48 plus de 7 jours).
   *À faire en connaissance de cause* : lancer l'overlay déclenche aussi la réconciliation de la phase 15 et
   la purge d'`archived.json` de la phase 21 (84 octets → `{}`).

2. **Critère n°2, in vivo — un terminal tué.** Ouvrir une session Claude Code, fermer brutalement le
   terminal (aucune valeur de motif de fin ne couvre ce cas), vérifier que le fichier d'état reste, puis
   qu'il a disparu au lancement suivant une fois passées les 72 h. Le widget, lui, cesse de l'afficher au
   bout de 8 h — c'est un comportement antérieur, à ne pas confondre avec le balayage.

3. **Critère n°3, in vivo — une écriture qui échoue se voit.** Avec le widget affiché (donc un lecteur qui
   tourne toutes les 2 s), enchaîner des tours dans une session : chaque changement d'état doit apparaître.
   En cas d'échec, `claude --debug` doit montrer la ligne d'erreur du hook — et la session **continuer**.

4. **Critère n°4, à l'œil.** Une session balayée doit simplement **disparaître** du widget. Si elle réapparaît
   plus tard marquée « traitée », ou si elle est présentée comme terminée, c'est un défaut à signaler.

5. **Le non-retour de la phase 22, à l'échelle du milestone.** Rouvrir le diagnostic après cette phase : il
   doit décrire le nouveau magasin (comptes et échantillons) alors qu'**aucune ligne de
   `DiagnosticService.cs` n'a changé**.

### Reliquats hérités des phases 21 et 22 — toujours ouverts

- Purge réelle d'`archived.json` (84 octets → `{}`) au premier lancement volontaire.
- Cohérence des **8 styles × 9 thèmes** (`Chronos.exe --sessions`).
- Absence de lignes `desktop:` après un usage prolongé avec l'app bureau ouverte.
- Comparaison ligne à ligne widget / diagnostic, et le cas `e465420e` masqué par `treated.json`.
- **L'exe déployé est antérieur à tout ce milestone** : rien des phases 15 à 23 n'est visible tant qu'une
  release n'est pas republiée (version dans l'exe **et** dans le nom du fichier publié). Les hooks installés
  dans `~/.claude/settings.json` pointent l'exe v3.0.2 actuellement en cours d'exécution : tant qu'il n'est
  pas republié, ni l'écriture directe de 23-01 ni le balayage de 23-02 ne s'exécutent chez l'utilisateur.
