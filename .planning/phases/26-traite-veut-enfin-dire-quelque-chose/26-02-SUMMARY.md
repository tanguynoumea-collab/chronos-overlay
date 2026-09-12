---
phase: 26-traite-veut-enfin-dire-quelque-chose
plan: 02
subsystem: sessions
tags: [archivage, contrat-unique, horloge-injectee, duree-de-vie-retiree, tdd, xunit]

sha_entree_de_phase: 07ee784
sha_entree_de_plan: ebd281c

requires:
  - phase: 23-le-cycle-de-vie-du-magasin
    provides: "IClock / SystemClock et le FakeClock des tests — l'horloge injectable que ArchiveStore n'avait pas"
  - phase: 26-01
    provides: "Le motif d'horloge injectable posé sur TreatedStore, recopié ici à l'identique ; et la rétention de 24 h qui rend le contraste des deux magasins écrivable"
  - phase: 21-la-source-unique
    provides: "PurgerPrefixe (SRC-02) — le seul retrait d'une archive, et il reste un geste"
provides:
  - "ArchiveStore sans AUCUNE durée de vie : ce qui est archivé ne revient jamais (TRT-04, volet code)"
  - "ArchiveStore(string? path = null, IClock? clock = null) — horloge injectée, plus aucun test à retardement"
  - "Load() qui ne lit plus d'horloge du tout ; Add() qui n'en garde une que pour HORODATER"
  - "Le contrat des deux magasins écrit là où il vit, et le contraste explicite entre eux"
affects: [26-03-le-geste-explicite, 26-04-le-contrat-documente]

tech-stack:
  added: []
  patterns:
    - "Une durée de vie qui défait un geste explicite de l'utilisateur en silence n'est pas une optimisation de taille : c'est une promesse non tenue"
    - "Un magasin dont le contrat est PERMANENT ne doit porter aucun champ de durée — et la garde qui le vérifie doit être par RÉFLEXION, insensible au texte des commentaires"
    - "Deux magasins jumeaux par leur code finissent par se faire prêter le même contrat : quand les contrats divergent, le code doit diverger avec eux, et chacun doit le dire"
    - "Prouver qu'une horloge injectée est RÉELLE et non décorative demande un test d'horodatage, distinct du test de durée"

key-files:
  created: []
  modified:
    - src/Chronos/Services/ArchiveStore.cs
    - src/Chronos/Services/TreatedStore.cs
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/ArchiveStorePurgeTests.cs

key-decisions:
  - "L'horloge est posée AVANT les tests, dans le même commit que l'étape ROUGE : les cas 2 et 3 référencent un constructeur à deux paramètres, les écrire d'abord aurait produit un commit qui ne compile pas — interdit sans exception par les règles dures. Ce qui reste rouge accuse la DURÉE DE VIE, et elle seule."
  - "La XML-doc de PurgerPrefixe a d'abord été corrigée (sa phrase « Distinct aussi d'Add, qui applique au passage la purge des entrées expirées » devenait fausse), puis RESTAURÉE octet pour octet : la règle dure « PurgerPrefixe reste INTACT » et le plan (« ne pas y toucher d'une ligne ») priment sur la correction opportuniste. La phrase devenue obsolète est signalée ci-dessous pour le plan 26-04."
  - "CompositionRootTests n'est PAS touché : le paramètre d'horloge est OPTIONNEL, son miroir construit ArchiveStore avec un chemin temporaire et sans horloge. Le graphe résout, Le_graphe_DI_resout_la_chaine_de_sessions reste vert sans une ligne modifiée — même règle qu'au plan 26-01."
  - "Le_magasin_d_archives_ne_connait_plus_aucune_duree_de_vie ne porte PAS d'Assert.NotEmpty(champs) : après le retrait de Ttl, ArchiveStore n'a plus aucun champ statique, une telle garde serait rouge à jamais. L'assertion anti-muette porte sur les paramètres de constructeur, exactement comme le plan la prescrit."

requirements-completed: [TRT-04]

metrics:
  duration: "~10 min"
  completed: 2026-09-13
---

# Phase 26 Plan 02 : « Archiver » cesse de mentir — Summary

**Le magasin d'archives n'applique plus aucune durée de vie : ce que l'utilisateur archive d'un clic droit ne
revient jamais, au lieu d'être mis en sourdine six heures sans qu'il puisse le savoir ; son horloge est
désormais injectée, et les deux magasins jumeaux ont chacun un contrat — écrit à l'endroit où un lecteur le
cherchera.**

## Performance

- **Duration :** ~10 min
- **Started :** 2026-09-12T22:29Z
- **Completed :** 2026-09-12T22:40Z
- **Tasks :** 2/2
- **Files modified :** 4

## La chaîne de totaux, mesurée

| Étape | Commande | Total | Échecs | Durée |
|---|---|---|---|---|
| Entrée de plan (baseline) | `dotnet test Chronos.sln -c Debug --nologo -v q` | **876** | 0 | 6 s |
| Fin tâche 1 (ROUGE) | idem | **879** (+3) | **2** | 5 s |
| Fin tâche 2 (VERT) | idem | **879** (+0) | **0** | 6 s |
| Vérification, 1re exécution | idem | **879** | 0 | 5 s |
| Vérification, 2e exécution | idem | **879** | 0 | 6 s |

Chaîne annoncée **876 → 879 (+3, dont 2 rouges) → 879 (+0)** : **tenue exactement**, aucun écart à justifier.
`26-VALIDATION.md` annonçait **879** après 26-02 : **879 mesuré**.

## L'étape ROUGE, mesurée et nommée

`dotnet test Chronos.sln -c Debug --nologo --filter "FullyQualifiedName~ArchiveStorePurge"` à la fin de la
tâche 1 — **build 0 erreur, 0 avertissement** (une phase rouge est une phase de tests qui échouent, jamais
d'un build cassé) :

```
Échoué!  - échec : 2, réussite : 7, ignorée(s) : 0, total : 9
```

**9 = 6 préexistants + 3 ajoutés.** Liste EXACTE des deux échecs, telle que sortie du runner :

| # | Test rouge en T1 | Pourquoi il était rouge |
|---|---|---|
| 1 | `Chronos.Tests.ArchiveStorePurgeTests.Une_archive_ne_s_evapore_jamais_meme_apres_huit_jours` | l'entrée `{"s": T-8 jours}` était écartée par `now - ts < Ttl.TotalMilliseconds` dans `Load()` |
| 2 | `Chronos.Tests.ArchiveStorePurgeTests.Le_magasin_d_archives_ne_connait_plus_aucune_duree_de_vie` | le champ statique `Ttl` de type `System.TimeSpan` siégeait encore dans un magasin qui promet le définitif |

**Le troisième cas, `L_horodatage_d_une_archive_vient_de_l_horloge_injectee`, est VERT dès la tâche 1** et
annoncé comme tel par le plan : sa raison d'être n'est pas d'être rouge mais de prouver que l'injection est
**RÉELLE et non décorative** — un paramètre peut être accepté puis ignoré. Il n'entre pas dans le compte des
rouges. **2 rouges annoncés, 2 rouges mesurés, nominativement les deux annoncés.**

Les **six tests préexistants** de `ArchiveStorePurgeTests` sont restés **VERTS** à la fin de la tâche 1 : la
durée de vie était encore là, donc `Purger_n_est_pas_expirer_…` n'avait pas encore à changer, exactement
comme le plan le prévoyait.

Après la tâche 2 : `--filter "FullyQualifiedName~ArchiveStorePurge"` → **9/9 verts, 0 `[FAIL]`**.

## La preuve que `PurgerPrefixe` est INTACT

C'est la règle dure la plus facile à enfreindre de bonne foi, et elle a failli l'être ici (voir déviation 2).
La preuve n'est pas un `grep` mais une **comparaison octet pour octet** du bloc complet — XML-doc **et**
corps — entre `07ee784` (SHA d'entrée de phase) et `HEAD` :

```
git show 07ee784:src/Chronos/Services/ArchiveStore.cs  →  bloc « /// <summary> SRC-02 … } »
longueurs : 2787  2787
IDENTIQUE
```

Purger un préfixe et expirer une entrée restaient deux gestes distincts ; il n'en reste plus qu'un dans le
code, et c'est **celui-là**, inchangé à l'octet près. Le nombre rendu, la non-réécriture quand il n'y a rien
à retirer, l'écriture directe, le garde-fou du préfixe vide : rien n'a bougé.

Trois tests le confirment par le comportement, tous verts :
`Les_deux_fantomes_mesures_sont_retires_du_fichier`, `Sans_fantome_le_fichier_de_l_utilisateur_n_est_pas_reecrit`,
`Un_prefixe_vide_ne_purge_rien` — plus la garde de démarrage
`Le_demarrage_purge_les_identifiants_fantomes_du_magasin_d_archives`, **verte**.

## Les valeurs réelles de chaque `grep`

### Tâche 1

| Mesure | Attendu | **Mesuré** | Verdict |
|---|---|---|---|
| `grep -c "IClock" src/Chronos/Services/ArchiveStore.cs` | 2 | **2** (le champ, le paramètre) | ✅ |
| `grep -c "_horloge.UtcNow" src/Chronos/Services/ArchiveStore.cs` | 2 | **2** (`Load`, `Add`) | ✅ |
| `grep -c "DateTimeOffset.UtcNow" src/Chronos/Services/ArchiveStore.cs` | 0 | **0** (2 avant) | ✅ |
| `grep -c "Ttl" src/Chronos/Services/ArchiveStore.cs` | 4 | **4** — *valeur mesurée AVANT modification : **4** également. La durée de vie est encore là, intacte.* | ✅ |
| `grep -c "new ArchiveStore(null, sp.GetRequiredService<IClock>())" src/Chronos/App.xaml.cs` | 1 | **1** (l. 246) | ✅ |
| `dotnet build Chronos.sln -c Debug --nologo` | 0 erreur | **0 erreur, 0 avertissement** | ✅ |
| échecs sur `~ArchiveStorePurge` | 2, nominatifs | **2, les deux annoncés** | ✅ |
| `L_horodatage_d_une_archive_vient_de_l_horloge_injectee` | vert dès T1 | **vert** | ✅ |
| 6 tests préexistants de `ArchiveStorePurgeTests` | verts | **verts** | ✅ |
| `CompositionRootTests` | compile et reste vert | **vert, zéro diff** | ✅ |
| Tests écrivant hors de `Path.GetTempPath()` | 0 | **0** — les 2 occurrences de `%APPDATA%` du fichier sont en XML-doc (l. 13 et 19), aucune écriture | ✅ |

### Tâche 2

| Mesure | Attendu | **Mesuré** | Verdict |
|---|---|---|---|
| `dotnet test Chronos.sln -c Debug --nologo -v q` | 0 échec, **879** | **0 échec, 879** | ✅ |
| `grep -c "Ttl" src/Chronos/Services/ArchiveStore.cs` | 0 | **0** | ✅ |
| `grep -c "TimeSpan" src/Chronos/Services/ArchiveStore.cs` | 0 | **0** | ✅ |
| `grep -c "PurgerPrefixe" src/Chronos/Services/ArchiveStore.cs` | inchangé vs entrée de phase | **1 → 2** | ⚠ voir déviation 1 |
| `grep -c "TRT-04" src/Chronos/Services/ArchiveStore.cs` | ≥ 1 | **1** | ✅ |
| `grep -c "TTL" src/Chronos/Services/TreatedStore.cs` | la phrase a disparu | **0** (déjà 0 à l'entrée de plan : 26-01 avait retiré le mot, pas la phrase) | ✅ |
| `grep -c "mêmes patterns" src/Chronos/Services/TreatedStore.cs` | 0 | **1 → 0** | ✅ |
| `grep -c "DateTimeOffset.UtcNow" tests/Chronos.Tests/ArchiveStorePurgeTests.cs` | 0 | **2 → 0** | ✅ |
| `git diff --stat 07ee784..HEAD -- src/Chronos/Views src/Chronos/Resources src/Chronos/ViewModels` | vide | **vide (0 ligne)** | ✅ |
| `git diff --stat 07ee784..HEAD -- '*.xaml'` | vide | **vide (0 ligne)** | ✅ |
| Suites de gardes (8 filtres) | 0 échec | **77/77 verts** | ✅ |
| `NET04_archivee_reste_masquee_meme_en_attente`, `Le_demarrage_purge_les_identifiants_fantomes_du_magasin_d_archives`, `Le_graphe_DI_resout_la_chaine_de_sessions` | verts | **3/3 verts** | ✅ |

**Note sur `_horloge.UtcNow` :** le compte passe de **2** (fin T1) à **1** (fin T2). C'est attendu et non une
régression : la tâche 2 prescrit que `Load()` n'ait **plus besoin d'horloge** du tout — il ne reste que
l'horodatage d'`Add()`. Aucun critère de la tâche 2 ne porte sur ce compteur.

### Acquis des phases précédentes, revérifiés

| Acquis | Attendu | **Mesuré** |
|---|---|---|
| `grep -cF "?? new " src/Chronos/Services/DiagnosticService.cs` | 2 | **2** |
| `grep -rhF "?? new " src/` (somme) | 6 à l'entrée de phase | **8** — les deux `clock ?? new SystemClock()` de 26-01 (`TreatedStore`) et 26-02 (`ArchiveStore`), hors `DiagnosticService.cs` ; le compte gardé reste **2** |
| `grep -c "MotifMasquage" src/Chronos/Services/DiagnosticService.cs` | 3 | **3** (énumération : **2** valeurs, `Archivee` / `Traitee`, inchangée) |
| `grep -c "new SessionMonitor" src/Chronos/Services/DiagnosticService.cs` | 0 | **0** |
| `grep -c "Inspecter(now).Visibles" src/Chronos/Services/SessionMonitor.cs` | 1 (unicité `Read(now) => Inspecter(now).Visibles`) | **1** (l. 69) |
| diff sur `SessionHookProcessor.cs`, `EcritureEtatSession.cs`, `CatalogueEvenementsHooks.cs` | vide | **vide** |
| Aucune dépendance NuGet ajoutée | — | **aucune** (`Chronos.Tests.csproj` et `Chronos.csproj` non touchés) |

## Task Commits

1. **Task 1 : RED — une archive de huit jours s'évapore encore** — `8a76322` (test)
2. **Task 2 : GREEN — ce qui est archivé ne revient jamais** — `a1413c2` (feat)

## Files Created/Modified

- `src/Chronos/Services/ArchiveStore.cs` — le champ `Ttl` et ses **deux** filtres de durée disparaissent.
  `Load()` ne lit plus aucune horloge et rend **toutes** les entrées lisibles (`TryGetInt64` conservé : une
  valeur non numérique reste illisible, donc ignorée). `Add()` garde `_horloge.UtcNow` pour **horodater** et
  recharge les entrées existantes **telles quelles**. Horloge injectée
  (`ArchiveStore(string? path = null, IClock? clock = null)`). XML-doc de tête réécrite : ce que le magasin
  promet, pourquoi il ne borne plus, ce qui borne encore, et le contraste explicite avec `TreatedStore`.
  **`PurgerPrefixe` : zéro octet modifié.**
- `src/Chronos/Services/TreatedStore.cs` — **documentation seule, aucun changement de code.** Le paragraphe
  « Calqué sur `ArchiveStore` (mêmes patterns…) » est remplacé : les deux magasins se distinguent désormais
  **par leur code autant que par leur sémantique** ; ils ne partagent plus que l'écriture atomique tmp+move,
  la lecture tolérante via `JsonDocument` et les chemins `%APPDATA%`. L'archive ne borne plus RIEN ; le
  « traité » est **RÉVERSIBLE via `Remove` (NET-03) : il dure tant que la session ne redemande rien**, et
  c'est la SEULE chose qui le défait. La rétention du fichier (24 h, supérieure à `DropAfter`) est nommée
  **borne de croissance**, pas délai d'affichage : elle ne peut pas, **par construction**, faire réapparaître
  une session encore lisible par le moniteur — c'est cela qui rend le libellé du menu exact (plan 26-03).
- `src/Chronos/App.xaml.cs` — une ligne (l. 246) :
  `services.AddSingleton(sp => new ArchiveStore(null, sp.GetRequiredService<IClock>()));`.
- `tests/Chronos.Tests/ArchiveStorePurgeTests.cs` — `using System.Reflection;`, l'instant de référence fixe
  `T = 2026-09-12 21:00:00 +00:00`, les trois cas de la tâche 1, le renommage annoncé avec son assertion
  inversée, et les deux tests jusqu'ici adossés à l'horloge système passés sur `FakeClock(T)`.

## Deviations from Plan

### 1. `grep -c "PurgerPrefixe" src/Chronos/Services/ArchiveStore.cs` vaut **2** et non **1** — c'est la XML-doc prescrite par le plan qui l'a fait monter

- **Trouvé pendant :** vérification des critères d'acceptation de la tâche 2.
- **Valeur mesurée à l'entrée de phase :** **1** (la seule déclaration de méthode).
- **Valeur mesurée à `HEAD` :** **2**.
- **Constat :** la ligne ajoutée est `/// <see cref="PurgerPrefixe"/> — un geste, lui aussi, jamais une
  horloge.`, qui appartient au bloc de XML-doc que le plan dicte **mot pour mot** au point (a) de la tâche 2.
  Le critère « inchangé » et le texte prescrit sont donc mécaniquement incompatibles : satisfaire le compteur
  aurait exigé de retirer une phrase que le plan ordonne d'écrire.
- **Décision :** la XML-doc est écrite **telle que le plan la dicte**, et le compteur est reporté à sa valeur
  réelle. L'intention du critère — « le geste de purge n'a pas été touché » — est tenue, et prouvée
  autrement, plus fortement : par la comparaison **octet pour octet** du bloc `PurgerPrefixe` contre
  `07ee784` (2787 = 2787, IDENTIQUE, section « La preuve que `PurgerPrefixe` est INTACT »).
- **Fichiers :** `src/Chronos/Services/ArchiveStore.cs` (aucune ligne de `PurgerPrefixe` modifiée).

### 2. La XML-doc de `PurgerPrefixe` a été corrigée, puis **restaurée octet pour octet** — et la phrase obsolète est signalée, pas réparée

- **Trouvé pendant :** tâche 2, point (a).
- **Constat :** la XML-doc de `PurgerPrefixe` contient ce paragraphe, inchangé depuis la phase 21 :

  > « Distinct aussi d'`Add`, qui applique au passage la purge des entrées expirées : ici, AUCUN filtre de
  > TTL. »

  Depuis ce plan, `Add` n'applique **plus** de purge d'entrées expirées : la phrase est devenue **fausse**.
  Elle a d'abord été réécrite (réflexe de la règle 1 : une documentation qui ment est un défaut).
- **Décision :** **restaurée à l'identique.** La règle dure de l'exécution (« `PurgerPrefixe` reste INTACT »)
  et le plan (« ne pas y toucher d'une ligne ») sont sans exception, et la portée de la règle n'est pas
  limitée au corps de la méthode. Un correctif opportuniste sur un bloc explicitement gelé est une déviation
  plus coûteuse qu'une phrase à rafraîchir. La vérification par comparaison d'octets ci-dessus confirme la
  restauration complète.
- **Impact réel, borné :** le paragraphe reste **exact sur ce qui compte** — `PurgerPrefixe` n'applique
  aucun filtre de durée, et purger n'est pas expirer. Seule sa description **d'`Add`** est périmée, et la
  XML-doc de tête du même fichier, elle, est à jour et dit le contraire sans ambiguïté (« Ce qui borne encore
  le fichier : rien d'automatique »). Aucun test ne dépend de ce texte.
- **Reporté à :** plan **26-04** (« le contrat documenté »), dont c'est le périmètre. Consigné ci-dessous.

### Renommages : **1 sur 1**, exactement celui qui était annoncé

`Purger_n_est_pas_expirer_une_entree_vieille_reste_dans_le_fichier` →
`Purger_n_est_pas_expirer_et_une_entree_vieille_reste_lisible`, dont la dernière assertion s'inverse
(`Assert.DoesNotContain` → `Assert.Contains`) parce que `Load()` rend désormais l'entrée de sept heures.
**Aucun autre renommage**, ni de test, ni de membre, ni de fichier. Les cinq autres tests préexistants ne
dépendaient pas de cette assertion — vérifié : ils sont restés verts de bout en bout, y compris pendant
l'étape rouge.

Deux tests ont vu leur **corps** ajusté sans être renommés, comme le plan le prescrit :
`Une_session_Claude_Code_archivee_survit_intacte` (son `recent` dérive de `T`) et le test renommé ci-dessus
(son `vieux` dérive de `T`) — tous deux construisent désormais leur magasin avec `new FakeClock(T)`.

### Aucun auth gate, aucun blocage

Deux tâches, aucune interruption, aucune règle 4 déclenchée. Aucune dépendance NuGet ajoutée.

## Known Stubs

Aucun. Aucun chemin de code n'est laissé vide, mock ou « à câbler plus tard ».

## Deferred Issues

- **`src/Chronos/Services/ArchiveStore.cs`, XML-doc de `PurgerPrefixe`** — le membre de phrase « `Add`, qui
  applique au passage la purge des entrées expirées » décrit un comportement que ce plan vient de retirer.
  Bloc **gelé** par une règle dure de ce plan, donc non corrigé ici. À rafraîchir au plan **26-04**.

## Sécurité — vérifié

- Aucun test n'écrit hors de `Path.GetTempPath()` : chaque cas passe par `TempFichier()`, qui **assert**
  `StartsWith(Path.GetTempPath(), f)`. Les 2 occurrences de `%APPDATA%` dans le fichier de tests sont en
  XML-doc (l. 13 et 19) ; aucune n'est un chemin d'écriture.
- `%APPDATA%\Chronos\archived.json` : **84 octets**, mtime **12 juillet 16:38:57** — **inchangé**. Ses deux
  fantômes attendent le prochain lancement de l'utilisateur ; ni lu en écriture, ni modifié, ni supprimé.
- `%APPDATA%\Chronos\treated.json` : **2 octets**, mtime **12 septembre 21:21:28**, antérieur à cette
  session — **inchangé**.
- `%APPDATA%\Chronos\sessions\` : **non approché**. (Il bouge tout seul — les hooks v3.0.2 de l'utilisateur
  sont vivants ; ce n'est pas une violation et rien n'a été « réparé ».)
- Le vrai `~/.claude/settings.json` n'a pas été écrit. **Aucune sonde.** `oauth.dat` n'a pas été approché,
  son mtime n'a **pas** été vérifié.
- L'overlay (pid 119412) n'a été **ni lancé ni tué**. Aucune requête réseau réelle.

## Ce que la suite peut tenir pour acquis

- `ArchiveStore` n'a **aucune** durée de vie et **aucun** champ `TimeSpan` — une garde par réflexion
  (`Le_magasin_d_archives_ne_connait_plus_aucune_duree_de_vie`) le tient, insensible au texte des
  commentaires.
- `ArchiveStore` prend une horloge en **second paramètre optionnel**, comme `TreatedStore` depuis 26-01 : le
  miroir DI de `CompositionRootTests` n'a pas eu à changer et n'aura pas à changer.
- `PurgerPrefixe` reste le **seul** retrait d'une archive, et c'est un geste — jamais une horloge.
- Le volet **UI** de TRT-04 reste entier pour le plan **26-03** : le libellé du menu promet toujours le
  définitif sans le dire précisément, et les fichiers `Views/`, `Resources/`, `ViewModels/` et tous les
  `*.xaml` ont **zéro diff** depuis l'entrée de phase. Le test
  `Marquer_traitee_une_session_qui_attend_depuis_sept_heures_la_fait_disparaitre` (26-03 T1) naîtra vert
  grâce à la rétention de 24 h posée en 26-01, pas grâce à ce plan.
- `docs/hooks-contract.md` n'a pas été touché — plan **26-04**, avec la phrase obsolète signalée ci-dessus.

## Self-Check: PASSED

- Fichiers annoncés : tous présents (`26-02-SUMMARY.md`, `ArchiveStore.cs`, `TreatedStore.cs`,
  `App.xaml.cs`, `ArchiveStorePurgeTests.cs`).
- Commits annoncés : `8a76322`, `a1413c2` — les deux retrouvés dans `git log`.
- Suite complète relancée **deux fois** après le dernier commit : **879 / 0 échec / 5 s puis 6 s**.
