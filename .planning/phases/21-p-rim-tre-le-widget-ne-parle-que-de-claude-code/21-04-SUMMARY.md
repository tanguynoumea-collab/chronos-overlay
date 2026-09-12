---
phase: 21-p-rim-tre-le-widget-ne-parle-que-de-claude-code
plan: 04
subsystem: sessions
tags: [archivage, purge, demarrage, garde-de-cablage, bilan-de-phase, tdd]

requires:
  - phase: 21-01
    provides: "TranscriptSessionSource porte ISessionSource — SessionMonitor prend sa source par le contrat"
  - phase: 21-02
    provides: "SessionMonitor, SessionTreatmentTracker et le graphe DI debranches de la source app-bureau"
  - phase: 21-03
    provides: "GardesPerimetreTests et son aide CheminSources() — la garde de cablage s'y greffe"
provides:
  - "ArchiveStore.PurgerPrefixe : retire des entrees DU FICHIER par prefixe, sans TTL, sans reecriture inutile"
  - "Cablage de la purge dans App.OnStartup, mode overlay uniquement, best-effort"
  - "Garde de cablage sur la SOURCE : une methode publique jamais appelee ne purge rien"
  - "Bilan nominatif du recul de couverture de la phase 21 : 49 tests supprimes nommes un a un, 16 ajoutes"
  - "Section A VERIFIER PAR L'UTILISATEUR consolidee pour la phase 21"
affects: [phase-23-cycle-de-vie-des-etats, phase-26-contrat-de-traitement]

tech-stack:
  added: []
  patterns:
    - "Purger un prefixe et expirer une entree sont deux gestes DISTINCTS : la nouvelle methode n'applique aucun filtre de TTL"
    - "Le nombre rendu par une operation d'ecriture est une OBSERVATION, pas une intention : ecriture en echec => 0"
    - "Garde de CABLAGE sur le texte source quand le point d'appel n'est pas instanciable sous test"

key-files:
  created:
    - tests/Chronos.Tests/ArchiveStorePurgeTests.cs
  modified:
    - src/Chronos/Services/ArchiveStore.cs
    - src/Chronos/App.xaml.cs
    - tests/Chronos.Tests/GardesPerimetreTests.cs
    - .planning/phases/21-p-rim-tre-le-widget-ne-parle-que-de-claude-code/21-VALIDATION.md

key-decisions:
  - "Ecarter a la lecture n'est PAS retirer : Load() ecartait deja les deux fantomes, s'en contenter aurait laisse le contournement manuel de l'utilisateur grave dans ses donnees"
  - "PurgerPrefixe n'applique AUCUN filtre de TTL, contrairement a Add : confondre purger et expirer ferait disparaitre des archives que l'utilisateur n'a pas demande de retirer"
  - "Ecriture DIRECTE et non par tmp+Move : geste unique au demarrage sans lecteur concurrent (200 echecs/500 mesures pour tmp+Move, 0/500 pour la directe) — Add garde son ecriture actuelle, c'est CYC-02 phase 23"
  - "Rien a retirer => le fichier n'est PAS reecrit, date de derniere ecriture comprise : une purge idempotente ne doit pas laisser de trace au deuxieme passage"
  - "Un prefixe vide rend 0 : il correspondrait a TOUTES les entrees et viderait le fichier de l'utilisateur, ce n'est jamais une intention legitime"
  - "L'agent n'a PAS purge le vrai archived.json : c'est le code livre qui doit le faire, au prochain lancement, sous le controle de l'utilisateur"
  - "Le bilan compte des CAS de test et non des methodes : 28 methodes de DesktopUiaSessionSourceTests portaient 36 cas (3 [Theory], 11 [InlineData]) — les theories sont eclatees cas par cas pour que le tableau porte bien 49 lignes"

patterns-established:
  - "Etape ROUGE jouee contre un SQUELETTE compilable (NotImplementedException) : la compilabilite a chaque commit est non negociable (precedent 18-01)"
  - "Garde de cablage prouvee par MUTATION avant d'etre committee (precedent 21-03)"

requirements-completed: [SRC-02]

duration: 9min
completed: 2026-09-12
---

# Phase 21 Plan 04 : Purge des fantomes archives et bilan de phase — Summary

**`ArchiveStore.PurgerPrefixe` retire les deux entrees `desktop:foreground:*` de juillet DU FICHIER
`archived.json` au demarrage de l'overlay — pas seulement de la lecture — et le recul de couverture de la
phase (752 → 719) est justifie test par test, les 49 disparus nommes un a un.**

## Performance

- **Duration:** 9 min
- **Started:** 2026-09-12T15:04:19Z
- **Completed:** 2026-09-12T15:13:02Z
- **Tasks:** 3/3
- **Files modified:** 5 (1 cree, 4 modifies)

## Accomplishments

- **SRC-02 refermee sur son vrai critere.** La distinction qui fait toute l'exigence est tenue : `Load()`
  ecartait deja ces deux entrees par TTL, ce qui ne retirait rien. Le fichier de l'utilisateur porte encore
  aujourd'hui la trace de son contournement manuel — deux lignes qu'il avait du archiver a la main parce que
  leur horodatage, rafraichi a chaque poll, les empechait de vieillir et donc d'expirer. Le code livre les
  efface ; l'agent, lui, n'a pas touche au fichier.
- **La purge est cablee ET gardee.** Une methode publique parfaitement testee mais jamais appelee ne purge
  rien, et le defaut ne se verrait que chez l'utilisateur, sur ses propres donnees, en silence. La garde de
  source a ete prouvee falsifiable par mutation reelle avant d'etre committee.
- **Le livrable de verification de la phase est produit.** Le recul de 752 a 719 est le LIVRABLE de cette
  phase de demolition, pas une regression : 49 tests couvraient exclusivement du code qui n'existe plus.
  Chacun est nomme, avec le type ou la branche qu'il couvrait.
- **719 tests / 0 echec, deux executions consecutives.** Le chiffre predit par le `21-VALIDATION.md` est
  atteint a l'unite, apres chaque tache, sans jamais deriver d'un seul test.

## Task Commits

1. **Task 1 (ROUGE) : les 6 tests de purge par prefixe** — `79486ea` (test) — 6 echecs / 0 succes
2. **Task 1 (VERT) : `PurgerPrefixe` retire les fantomes du fichier** — `350db36` (feat) — 718 tests
3. **Task 2 : la purge s'execute au demarrage, gardee** — `57b90e4` (feat) — 719 tests
4. **Task 3 : bilan nominatif du recul de couverture** — `12c6fb5` (docs) — 719 tests

Aucune etape REFACTOR : l'implementation VERTE est celle qui reste, aucune dette de forme n'a ete creee.

## Files Created/Modified

- `tests/Chronos.Tests/ArchiveStorePurgeTests.cs` **(cree)** — 6 tests, dont le **contenu reel mesure** de
  `%APPDATA%\Chronos\archived.json` rejoue tel quel. Tous ecrivent sous `Path.GetTempPath()`, verrouille par
  une assertion : un chemin qui sortirait du dossier temporaire ferait echouer le test **avant** toute ecriture.
- `src/Chronos/Services/ArchiveStore.cs` — ajout de `PurgerPrefixe`. `Load` et `Add` **intacts** : leur TTL de
  6 h et l'ecriture par `tmp` + `Move` d'`Add` relevent des phases 26 et 23.
- `src/Chronos/App.xaml.cs` — un appel best-effort dans `OnStartup`, entre la reconciliation des reglages
  Claude et `OfferOnFirstRun()`. Mode overlay uniquement : les modes `--hook` et `--statusline` sortent bien
  plus haut et ne l'atteignent jamais.
- `tests/Chronos.Tests/GardesPerimetreTests.cs` — garde de cablage `Le_demarrage_purge_les_identifiants_
  fantomes_du_magasin_d_archives`.
- `.planning/…/21-VALIDATION.md` — bilan nominatif rempli, statuts de la carte par tache releves, invariants
  de securite mesures, section « A VERIFIER PAR L'UTILISATEUR » ouverte.

## Decisions Made

### 1. Ecarter n'est pas retirer — et c'est tout le contenu de SRC-02

`ArchiveStore.Load()` ecarte ces deux entrees depuis toujours, par TTL. On aurait donc pu declarer
l'exigence satisfaite sans ecrire une ligne. Ce raisonnement est faux : ce que SRC-02 demande n'est pas que
le widget cesse de les afficher (il ne les affiche deja plus), c'est que **les donnees de l'utilisateur
cessent de porter la marque du contournement qu'on lui a impose**. Les deux lignes de juillet sont une
piece a conviction : leur horodatage etant rafraichi a chaque poll, elles ne pouvaient **jamais** expirer,
et le menu contextuel etait le seul geste possible. Tant qu'elles restent dans le fichier, ce geste reste
grave.

### 2. Purger n'est pas expirer

`Add` purge les entrees expirees au passage. `PurgerPrefixe` n'applique **aucun** filtre de TTL, et un test
le verrouille : une entree de 7 h non prefixee **reste dans le fichier** apres la purge — et `Load()`
continue de ne pas la rendre. Ce sont deux questions distinctes. Les confondre aurait fait disparaitre,
a l'occasion d'un nettoyage de fantomes, des archives que l'utilisateur n'a jamais demande de retirer.

### 3. Le nombre rendu est une observation, pas une intention

`return ecrit ? retirees : 0` — si l'ecriture echoue, la methode rend **0**, parce que rien n'a ete retire.
Rendre le nombre d'entrees *reperees* aurait fait annoncer une purge qui n'a pas eu lieu, sur le seul canal
par lequel l'appelant peut savoir quelque chose.

### 4. Ecriture directe ici, `tmp` + `Move` conserve dans `Add`

La mesure de la phase 23 (200 echecs sur 500 pour `tmp` + `Move` contre un lecteur concurrent, 0 sur 500
pour l'ecriture directe) **n'est pas appliquee a `Add`** : ce serait anticiper CYC-02. Mais la methode
neuve, elle, n'a aucune raison d'heriter d'un defaut connu — un geste unique au demarrage, sans lecteur
concurrent, s'ecrit directement.

### 5. Le vrai `archived.json` n'a pas ete touche

Verifie en fin d'execution : **84 octets, mtime 1783867137, contenu identique**. C'est le code **livre** qui
doit le purger, au prochain lancement, sous le controle de l'utilisateur. Un agent qui aurait edite le
fichier aurait produit exactement le meme resultat visible et **aucune** preuve que le code fonctionne.

### 6. Le bilan compte des cas de test, pas des methodes

Precision **mesuree**, non prevue par le plan : `DesktopUiaSessionSourceTests` comptait **28 methodes**
(25 `[Fact]` + 3 `[Theory]`) pour **36 cas** — les theories portaient 4 + 4 + 3 = 11 `[InlineData]`, et
c'est le cas que xUnit compte. Le tableau eclate donc les theories cas par cas, chaque ligne nommant sa
methode **et** la donnee qui la distingue : 49 lignes pour 41 methodes distinctes. Un tableau de 41 lignes
aurait ete nominativement complet mais illisible face au chiffre « 49 ».

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Avertissement xUnit2013 introduit par le test du plan**

- **Found during:** Task 1 (etape ROUGE)
- **Issue:** Le code de test fourni par le plan contenait
  `Assert.Equal(0, doc.RootElement.EnumerateObject().Count())`, que l'analyseur xUnit signale
  (`xUnit2013 : Do not use Assert.Equal() to check for collection size`). Le depot construit a
  **0 avertissement** ; introduire le premier aurait entame cet invariant.
- **Fix:** `Assert.Empty(doc.RootElement.EnumerateObject())` — strictement equivalent, commentaire
  « le fichier est vide, pas supprime » conserve.
- **Files modified:** `tests/Chronos.Tests/ArchiveStorePurgeTests.cs`
- **Verification:** `dotnet build Chronos.sln -c Debug` → **0 avertissement, 0 erreur**. L'etape ROUGE
  rejouee apres correction : toujours 6 echecs / 0 succes.
- **Committed in:** `79486ea`

---

**Total deviations:** 1 auto-corrigee (Rule 1).
**Impact on plan:** nul sur le comportement. Aucun elargissement de perimetre.

### Criteres d'acceptation du plan corriges par la mesure

**`grep -c "Ttl" src/Chronos/Services/ArchiveStore.cs` rend 4, pas 3.** Le plan annoncait 3 (la constante +
ses deux usages) en oubliant le `<see cref="Ttl"/>` du commentaire de classe, present **avant** cette
execution. Mesure : **4 avant, 4 apres**. L'intention du critere — « `PurgerPrefixe` n'en fait AUCUN
usage » — est tenue et verifiee ligne a ligne (l. 11, 17, 35, 55 ; aucune dans la methode neuve). Le
chiffre du plan etait faux, pas le code.

Tous les autres criteres greps sont rendus exactement : signature = 1, `File.Move` = 1,
`ecrit ? retirees : 0` = 1, `desktop:foreground:unknown` = 1, `PurgerPrefixe("desktop:")` = 1,
`ShowIfEnabled` = 1, garde de cablage = 1.

## Issues Encountered

**Falsifiabilite de la garde de cablage : jouee, pas affirmee.** Conformement au precedent pose au plan
21-03, la garde a ete mutee avant d'etre committee — `PurgerPrefixe("desktop:")` remplace par
`PurgerPrefixe("desktopMUTANT:")` dans `App.xaml.cs`, garde relancee, **echec constate et nomme**, mutation
revoquee (`grep` de controle = 1). Une garde qui ne peut pas echouer ne garde rien.

## Bilan nominatif de la phase 21 — le livrable de verification

Le milestone v1.6 exige que les 752 tests restent verts. **Cette phase fait exception, explicitement**, et
le `21-VALIDATION.md` l'avait inscrit des la planification : 529 lignes de tests couvraient exclusivement du
code qui disparait. Le critere operationnel n'est pas « >= 752 » mais **0 echec + justification nominative
de chaque test supprime**. Precedent : la phase 16 du milestone v1.5 (demolition des plafonds).

### Reconciliation, etape par etape — aucun ecart

| Etape | Delta | Predit | **Mesure** |
|---|---|---|---|
| Baseline avant la phase 21 | — | 752 | **752** |
| Plan 21-01 — limite apres le filtre sous-agents (SRC-03) | +4 | 756 | **756** |
| Plan 21-02 — debranchement de la source app-bureau | −9 | 747 | **747** |
| Plan 21-03 T1 — 10 fichiers quittent le depot, +2 gardes | −40 / +2 | 709 | **709** |
| Plan 21-03 T2 — garde de source XAML | +1 | 710 | **710** |
| Plan 21-03 T3 — montage BAML 8 styles x 9 themes | +2 | 712 | **712** |
| Plan 21-04 T1 — `ArchiveStorePurgeTests` | +6 | 718 | **718** |
| Plan 21-04 T2 — garde de cablage | +1 | 719 | **719** |
| **Fin de phase, passe 1** | — | **719** | **719 / 0 echec / 6 s** |
| **Fin de phase, passe 2** | — | **719** | **719 / 0 echec / 6 s** |

**752 − 49 + 16 = 719. Mesure : 719.** Aucun ecart a expliquer.

### Les 49 supprimes — ventilation (detail nominatif complet dans `21-VALIDATION.md`)

| Origine | Methodes | Cas | Code disparu qu'ils couvraient |
|---|---|---|---|
| `DesktopUiaSessionSourceTests.cs` (fichier entier) | 28 | **36** | `DesktopUiaSessionSource`, `UiaLabels`, `UiaNode`, `DesktopHealth`, `SessionKind` |
| `DesktopUiaPollServiceTests.cs` (fichier entier) | 4 | **4** | `DesktopUiaPollService` |
| `SessionsTests.cs` | 5 | **5** | etape de repli app-bureau de `SessionMonitor.Read`, libelle de type du widget |
| `TreatedSessionsTests.cs` | 4 | **4** | branche d'acquittement par focus de `SessionTreatmentTracker` (NET-02) |
| **Total** | **41** | **49** | |

**Aucun de ces 49 tests ne couvrait une ligne survivante.** Un test a cheval sur du code supprime et du code
survivant n'est pas supprime mais **reduit** : c'est le cas de `Monitor_sans_source_bureau_ne_regresse_pas`,
conserve et renomme `Monitor_lit_une_session_du_transcript_sans_hook`. Il apparait des deux cotes du diff
de `9d0c8ca` — la seule raison pour laquelle ce commit montre 10 methodes retirees pour 9 suppressions nettes.

### Les 16 ajoutes

4 pour SRC-03 (`TranscriptSousAgentsTests`), 4 gardes de perimetre, 2 de montage BAML, 6 de purge et
1 de cablage. Les 16 noms figurent au `21-VALIDATION.md` et ont ete verifies **par extraction du code
source**, pas recopies du plan.

### Couverture des exigences de la phase

| Exigence | Statut | Ce qui la prouve |
|---|---|---|
| **SRC-01** — retrait de la source app-bureau | **Confirmee** (plans 02-03) | `Aucun_type_de_la_source_app_bureau_ne_subsiste_dans_l_assembly` (reflexion), `Aucun_style_de_session_ne_binde_plus_un_libelle_de_type` (source XAML), `CompositionRootTests` |
| **SRC-02** — fantomes archives purges | **Refermee ici** | 6 tests dont le contenu reel mesure + garde de cablage prouvee falsifiable |
| **SRC-03** — limite apres le filtre sous-agents | **Confirmee** (plan 01) | `TranscriptSousAgentsTests`, dont le scenario mesure des 12 sous-agents chauds |

### Gardes nommees de la phase — vertes

`ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests`, `GardesDoctrineTests`,
`La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte` — verifiees par filtre dedie : **18 cas, 0 echec**.

### Invariants de securite — releves en fin d'execution

| Invariant | Attendu | **Mesure** |
|---|---|---|
| `%APPDATA%\Chronos\sessions\` | 66 entrees | **66** |
| `oauth.dat` | 518 octets | **518** |
| `archived.json` | **inchange**, 84 octets | **84 octets, mtime 1783867137, contenu identique** |
| Overlay `Chronos-v3.0.2.exe` | ni lance ni tue | pid **119412** toujours vivant, jamais touche |
| Dependances NuGet | aucune nouvelle | `git diff -- '*.csproj'` **vide** |
| Ecrits hors perimetre | aucun | `git status --porcelain` **vide** |

Le mtime d'`oauth.dat` n'a **pas** ete relu : l'invariant annonce a la redaction du `21-VALIDATION.md`
(1783863147) avait ete demontre faux au plan 21-01 — c'etait celui d'`archived.json`, par transposition — et
l'overlay en cours fait tourner le refresh token toutes les 60 s. **Seule la taille est un invariant.**

## A VERIFIER PAR L'UTILISATEUR

Trois points ne sont observables qu'apres un redemarrage **volontaire** de l'overlay, que cette phase
s'interdit de provoquer : une instance `Chronos-v3.0.2.exe` (pid 119412) est en cours d'utilisation.
Protocole complet dans `21-VALIDATION.md`, section du meme nom.

1. **SRC-02 in vivo — non substituable.** Au prochain lancement de la version du depot,
   `%APPDATA%\Chronos\archived.json` doit passer de **84 octets** a `{}` (**2 octets**), sans qu'aucun
   fichier n'ait ete touche a la main.

   ```sh
   stat -c '%s' "$APPDATA/Chronos/archived.json"   # attendu APRES lancement : 2
   cat  "$APPDATA/Chronos/archived.json"           # attendu APRES lancement : {}
   ```

   Le test unitaire prouve que la methode retire les deux entrees mesurees ; la garde prouve que l'appel est
   cable. **Ni l'un ni l'autre ne prouve que le fichier de l'utilisateur a change.**

   *A faire en connaissance de cause* : lancer l'overlay declenche aussi la reconciliation de la phase 15,
   qui purge les groupes de hooks perimes de `~/.claude/settings.json` (avec sauvegarde horodatee).

2. **Critere 4, a l'oeil** — `Chronos.exe --sessions`, parcourir les **8 styles** sur les **9 themes** : une
   ligne du style Pastilles doit lire `etat · detail`, **un seul** point median, aucun separateur orphelin
   en fin de ligne. `SessionStylesBindingTests` monte le vrai BAML sur les 72 combinaisons et mesure — il
   attrape un binding casse ou une disposition a zero, pas un desequilibre visuel.

3. **Critere 1, apres quelques heures d'usage** — avec l'app bureau Claude ouverte au premier plan : plus
   aucune ligne prefixee `desktop:` dans le widget, et plus rien a archiver a la main.

### Points differes herites des plans 01 a 03

4. **Le `catch` de `TranscriptSessionSource.Read` n'est pas couvert** (plan 21-01). Le defaut reel —
   l'enumeration LINQ paresseuse levait **hors** du `try` — est corrige ; c'est sa **couverture** qui
   manque. Injecter une panne d'enumeration disque exigerait une abstraction de systeme de fichiers absente
   du depot.
5. **NET-02 (acquittement par focus) n'a plus aucun mecanisme vivant** (plan 21-02), documente par une
   epitaphe donnant la raison mecanique de sa mort plutot que supprime en silence. S'il redevient souhaite,
   c'est une exigence a re-poser, pas un code a ressusciter.
6. **L'exe deploye est anterieur a tout ce milestone.** Rien des phases 15 a 21 n'est visible tant qu'une
   release n'est pas republiee ; la version du `.csproj` doit etre montee a la prochaine release (version
   dans l'exe **et** dans le nom du fichier publie).

Les neuf constats humains en attente du milestone v1.5 (protocole dans `20-05-SUMMARY.md`) restent ouverts
et independants de cette phase.

## Hors perimetre — deliberement non anticipe

Verifie, non touche :

- `DiagnosticService` conserve son `new SessionMonitor()` nu et son `files.Take(8)` — **OBS-01/OBS-02,
  phase 22**.
- `ArchiveStore.Add` conserve son ecriture par fichier temporaire — **CYC-02, phase 23**.
- Le **TTL de 6 h** d'`ArchiveStore` reste, face a un contrat annonce « permanent » — **TRT-04, phase 26**.
  Ce plan purge des entrees obsoletes ; il ne change **pas** la politique de retention.
- Le balayage du magasin de sessions et des `.tmp` orphelins — **phase 23**.
- L'arbitrage par fraicheur — **phase 24**. Le contrat d'evenements — **phase 25**.

Cette phase **demolit et nettoie** ; elle ne change **aucune** semantique d'etat.

## Known Stubs

Aucun. `PurgerPrefixe` est implementee, cablee, gardee ; aucune valeur en dur, aucun placeholder, aucun
composant sans source de donnees n'a ete introduit par ce plan.

## Next Phase Readiness

Phase 21 **complete** : SRC-01, SRC-02 et SRC-03 couvertes, 719 tests verts en deux passes, recul de
couverture justifie nominativement. La phase 22 (OBS-01/OBS-02 — l'instrument de mesure) peut demarrer sur
un terrain demoli et stabilise : `DiagnosticService` est le seul consommateur de sessions qui ne partage pas
encore l'instance du widget, et c'est precisement son objet.

## Self-Check: PASSED

- 6 fichiers annonces (1 cree, 4 modifies, 1 SUMMARY) : tous presents sur disque.
- 4 commits de tache annonces (`79486ea`, `350db36`, `57b90e4`, `12c6fb5`) : tous presents dans `git log`.
- Suite complete rejouee apres le dernier commit de code : **719 / 0 echec**, deux executions consecutives.
- Invariants de securite reverifies apres le dernier commit : `sessions/` = 66, `oauth.dat` = 518 o,
  `archived.json` = 84 o inchange, overlay pid 119412 vivant et jamais touche.
