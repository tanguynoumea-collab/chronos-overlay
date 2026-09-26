# Phase 30 : La lecture fait disparaître — Recherche

**Researched:** 2026-09-25 (fin de soirée ; rédigé le 2026-09-26)
**Domain:** règle « lue » dans le détecteur de traitement (hystérésis réversible), détection Win32 du premier plan
sans UI Automation, sélection de session par `lastFocusedAt`, motif de masquage porté jusqu'au diagnostic (OBS-01)
**Confidence:** HIGH sur le code (lu ligne à ligne à `3c5f395`, fichiers de la vague 3 de la phase 29 cités par
leur contenu, jamais par leurs numéros de ligne) et sur les mesures Win32 (faites sur cette machine, dans l'arbre
de l'app ET hors de l'arbre par WMI) ; MEDIUM sur deux comportements de l'app bureau non relevés en phase 27 (accueil
ou Chat au premier plan, latence d'écriture de `lastFocusedAt`) — chacun est nommé.

> **Quatre faits à lire avant tout plan.**
>
> 1. **La session sélectionnée ne se calcule PAS avec ce que le lecteur rend aujourd'hui.** `LecteurAppBureau
>    .Interpreter` s'arrête dès que `cliSessionId` manque (`LecteurAppBureau.cs:393-394`) : un fichier sans
>    identifiant ne livre pas son `lastFocusedAt`, et `ParSession` ne garde qu'un fichier par session (le plus
>    récent par `lastActivityAt`, `:243-253`). Or « le `lastFocusedAt` le plus récent, TOUS fichiers confondus »
>    (verrou du CONTEXT) inclut ces fichiers : l'utilisateur qui ouvre une session neuve, pas encore dotée d'un
>    `cliSessionId`, ne regarde AUCUNE des sessions du widget. Calculer le max sur `ParSession` seul désignerait la
>    session précédente — et LUE-02 la déclarerait lue pendant qu'il regarde ailleurs. Le lecteur doit exposer la
>    sélection (Q2.d). `LecteurAppBureau.cs` n'est pas touché par la vague 3 de la phase 29.
> 2. **L'instant comparé au focus est l'épisode du détecteur, pas l'`UpdatedAt` brut.** Le détecteur date chaque
>    épisode par `_attenteDepuis[id]` (le maximum des instants de signal de l'épisode, borné par l'horloge —
>    `SessionTreatmentTracker.cs:98-102`), et c'est CETTE valeur que NET-01 écrit et que NET-03 compare
>    (`:90`, `:106-108`). Si LUE-01 comparait et écrivait l'`UpdatedAt` du vainqueur courant alors que l'épisode
>    mémorisé est plus récent (bascule vers une source plus ancienne), NET-03 purgerait au cycle suivant ce que
>    LUE-01 vient d'écrire, puis LUE-01 le réécrirait : une écriture de `treated.json` par cycle. En régime
>    permanent les deux valeurs sont égales ; au bord, seule l'épisode est cohérente (Q1.b).
> 3. **NET-03 et LUE-01 sont UNE décision, pas deux blocs successifs.** Le magasin est lu une fois en tête de cycle
>    (`:76`). Placé avant NET-03, un `Set` de LUE-01 serait immédiatement annulé par un `Remove` fondé sur la
>    lecture périmée ; placé après, un nouvel épisode déjà lu coûterait `Remove` puis `Set` (deux écritures). La
>    table à six cas de Q1.c remplace les lignes 104-108.
> 4. **Le magasin ne sait pas POURQUOI une session est traitée.** `treated.json` est une carte `id → ms`
>    (`TreatedStore.cs:9-10`) : « lue par focus », « lue au premier plan », « répondue » et « marquée à la main »
>    y ont la même forme. LUE-03 exige la cause : elle se mémorise dans le détecteur au moment où il la constate,
>    avec un résidu HONNÊTE (« traitée — marquée à la main, ou avant le démarrage de l'overlay ») pour ce qu'il
>    n'a pas vu (Q3.b).

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Phase Boundary (copié tel quel)

Une session **lue** quitte le widget d'elle-même. Cette phase consomme `lastFocusedAt` (phase 29) et la forme
de LUE-02 fixée par le relevé (phase 27), et s'applique à la population élargie par SIL-01 (phase 28). Elle
n'ajoute ni source ni libellé.

Ce que dit l'utilisateur, ligne 3 de son tableau : « session qui a fini de réfléchir et qui est lue :
n'apparaît pas ».

### Locked Decisions (copiées telles quelles)

#### La règle LUE-01 — VERROUILLÉE par le relevé
- Une session en attente (WaitingTurn, WaitingAttention, question posée, `blocked`) est **lue** si
  `instantAttente < dernierFocus`, où :
  - `instantAttente` = l'instant que le signal RETENU porte (`SessionSnapshot.UpdatedAt` après la phase 28 :
    `timestamp` du dernier message significatif du transcript, ou `updated_at` du hook, ou `lastActivityAt`
    pour `blocked`) ;
  - `dernierFocus` = `lastFocusedAt` de la session (phase 29), via `cliSessionId`.
- Effet : `TreatedStore.Set(id, instantAttente)` — le magasin réversible EXISTANT, épisode daté par le signal
  (TRT-02). NET-03 inchangé : un signal d'attente plus récent que l'épisode traité fait revenir la session.
- Pas d'écriture à chaque cycle : `Set` seulement si la session n'est pas déjà traitée pour cet épisode
  (comparer à la valeur mémorisée avant d'écrire).
- Où : dans `SessionTreatmentTracker.Observe` (le détecteur possède déjà l'ajout et la purge), qui reçoit
  désormais aussi le dernier focus par session (dictionnaire `id → DateTimeOffset?`) et l'état du premier plan.

#### La règle LUE-02 — CONFIRMÉE NÉCESSAIRE par le geste B, forme fixée
- La session **sélectionnée** = celle dont `lastFocusedAt` est le plus récent parmi TOUS les fichiers de l'app
  (pas seulement celles affichées).
- Elle compte comme lue tant que la fenêtre au premier plan appartient au processus **`claude`**, avec un
  délai de grâce de **2,5 s** après `instantAttente` (le regard a le temps de se poser).
- Détection Win32 : `GetForegroundWindow()` → `GetWindowThreadProcessId()` → nom du processus (`Process
  .GetProcessById(pid).ProcessName`, comparaison ordinale insensible à la casse avec `claude`). Aucune UI
  Automation. Implémentation derrière une interface neutre (`IPremierPlan` / `PremierPlanWin32`), horloge
  injectée, sondage à la cadence du widget (2 s), résultat mis en cache 1 s.
- Nommage : `GardesPerimetreTests` interdit tout type dont le nom contient `Uia` ou se termine par
  `ForegroundWatch` — choisir `PremierPlanWin32` / `IPremierPlan` ; ne pas assouplir la garde.
- Dès que le premier plan quitte `claude`, LUE-01 reprend seule : le retour ultérieur (alt-tab ou clic) met
  `lastFocusedAt` à jour (geste A) et la session disparaît par LUE-01.

#### La déduction (« En attente ? ») — décision écrite
- Une session en attente DÉDUITE (silence) ouverte ensuite par l'utilisateur est **lue** au même titre : le
  prédicat d'attente du détecteur (`EstAttente`) inclut déjà `WaitingDeduced` depuis la phase 26, et le
  relevé de phase 27 ne donne aucune raison de l'exclure. Si elle redevient une attente observée plus récente,
  elle revient (NET-03).

#### Le diagnostic (LUE-03)
- `MotifMasquage` gagne `LueParFocus` (« lue : focus à HH:MM > attente à HH:MM ») et `LueAuPremierPlan`
  (« sélectionnée au premier plan depuis N s ») ; « répondue » (NET-01) et les motifs v1.6 (archivée, marquée
  à la main) restent distincts. Le diagnostic imprime, par session masquée, le motif et les deux instants.

#### Pas de faux masquage (LUE-04)
- Sans `lastFocusedAt` pour une session (pas de fichier app, pas de `cliSessionId`, format changé) : LUE-01 et
  LUE-02 ne s'appliquent PAS à cette session ; comportement v1.6 strict. Test dédié avec la fixture
  `sans-cliSessionId.json`.
- Sans détection de premier plan (échec Win32) : LUE-02 inactive, LUE-01 seule, et le diagnostic le dit.

### Claude's Discretion (copiée telle quelle)
- Découpage en plans ; nom des types ; présentation exacte des lignes du diagnostic.

### Deferred Ideas — OUT OF SCOPE (copiées telles quelles)
- Marquer « lu » côté app (écriture dans `%APPDATA%\Claude`) — hors périmètre définitif.
- Exploiter `latestUserFrameAt` pour dater une réponse (NET-01 côté app) — après v1.7.

### Specific Ideas (rappel du contexte)
- Test « le relevé du 2026-09-25 à 16 h 08 ne se reproduit plus » (critère 1 de la roadmap) : deux sessions
  `end_turn` à 15:59:05 et 15:56:10, focus à 16:00:39 et 16:00:59, horloge à 16:08 ⇒ masquées « lue » ; une
  troisième `end_turn` à 16:05 avec focus à 16:00 ⇒ visible « En attente ».
- Test du geste B : attente à 20:34:04.328, focus 20:30:44.976, premier plan `claude` depuis 20:33:27 ⇒ lue
  (LUE-02) ; même chose avec premier plan `explorer` ⇒ visible.
- Test alt-tab (geste A) : focus passe de 16:23:51 à 20:30:44 après une attente à 20:06 ⇒ lue par LUE-01.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description (REQUIREMENTS.md) | Ce que la recherche apporte |
|----|-------------------------------|-----------------------------|
| **LUE-01** | Attente antérieure à `lastFocusedAt` ⇒ traitée automatiquement via `TreatedStore` (épisode daté par le signal), sans clic ; réversible (NET-03) ; instant = `timestamp` du dernier message du transcript, jamais le `mtime` | **Q1** : l'instant = l'épisode `_attenteDepuis` (même valeur que NET-01/NET-03), la table à six cas qui fusionne NET-03 et LUE-01 (une écriture par épisode au plus), la preuve « pas d'écriture à chaque cycle » par sentinelle de date d'écriture, le dictionnaire de focus construit sur la MÊME lecture de l'app (OBS-01), les interactions NET-01 / 478 min / déduite / geste |
| **LUE-02** | Session sélectionnée (max `lastFocusedAt`, tous fichiers) + fenêtre au premier plan du processus `claude` + grâce 2,5 s ; Win32 sans UIA | **Q2** : mesures sur la machine (ProcessName `claude` rendu dedans ET hors de l'arbre, 0,021 ms médian ; 17 processus `claude` dont 3 CLI, une seule fenêtre), l'ancienne implémentation reprise en partie (`git show ff783d4^`), `IPremierPlan.Lire(now)` avec sonde injectable, état « depuis », cache 1 s, la sélection à calculer DANS le lecteur (fichiers sans `cliSessionId` compris), le prédicat de grâce `now − max(attente, début du premier plan) ≥ 2,5 s` |
| **LUE-03** | Le diagnostic nomme le motif de chaque masquage (« lue : focus à … > attente à … », « sélectionnée au premier plan », « répondue »), jamais un masquage sans cause | **Q3** : la cause mémorisée par le détecteur (le magasin ne la porte pas), `SessionMasquee` + `CauseTraitement` optionnelle, trois valeurs ajoutées EN FIN de `MotifMasquage`, résidu honnête, format des lignes, section « Règle « lue » » (sélection, premier plan) lue dans la MÊME `LectureSessions` |
| **LUE-04** | Sans source app-bureau pour une session ⇒ règle inactive, comportement v1.6, aucun faux masquage | **Q4** : huit cas de dégradation et leur résultat attendu ; paramètres nuls par défaut (v1.6 exact par construction) sous gardes de câblage (Piège 9 de la phase 29) |

**Critères de succès de la ROADMAP** (ils gouvernent la vérification) : (1) le relevé de 16 h 08 rejoué en test ne
montre plus les deux sessions lues, une attente postérieure au focus reste « En attente » ; (2) réversible (NET-03),
magasin existant, pas de réécriture de `treated.json` à chaque rafraîchissement, « Marquer traitée » et
« Archiver » comme en v1.6 ; (3) le tour fini sous les yeux n'est pas annoncé « En attente » (Win32, horloge
injectée, gardes de la phase 21 inchangées) ; (4) aucun masquage sans cause ; (5) pas de faux masquage sans
métadonnées. **Question ouverte de la ROADMAP** (l'attente déduite) : TRANCHÉE par le CONTEXT (« lue au même
titre ») — le plan doit la recopier comme décision écrite (D-30-07 ci-dessous), pas la laisser en effet de bord.
</phase_requirements>

---

## Project Constraints (from CLAUDE.md)

| Directive | Conséquence pour cette phase |
|---|---|
| C# / .NET 8 (`net8.0-windows`) / WPF / MVVM strict / DI | `IPremierPlan` enregistré en singleton, passé au moniteur par argument NOMMÉ ; aucune logique dans les ViewModels (le widget affiche `Visibles`, rien de plus) |
| Aucun type WPF dans `Services/` (`ServicesLayerPurityTests:29-53`) | La garde interdit les assemblies `PresentationCore`, `PresentationFramework`, `WindowsBase` dans les signatures PUBLIQUES des types de `Chronos.Services`/`Chronos.Models`. Un P/Invoke `user32` privé qui ne manipule que `nint`/`uint` passe (précédent : `WindowsCredentialStore.cs:35-39`, P/Invoke `advapi32` privé dans `Services/`). `System.Diagnostics.Process` n'est pas un assembly WPF |
| **Aucune dépendance native** | `user32.dll` est une DLL du SYSTÈME, pas une bibliothèque embarquée : elle n'entre pas dans le mono-fichier (`IncludeNativeLibrariesForSelfExtract` concerne les natives livrées avec l'exe, comme `wpfgfx`). Le dépôt en appelle déjà quatre fonctions (`Interop/NativeMethods.cs`). La contrainte vise le RENDU (pas de SkiaSharp) : elle est respectée — à écrire tel quel dans le plan |
| Horloges injectées | `IPremierPlan.Lire(DateTimeOffset now)` reçoit l'instant du moniteur (lui-même `IClock`) ; aucun `DateTime.Now`/`DateTimeOffset.UtcNow` dans le détecteur ni la sonde |
| Chemins sous le profil, aucun droit admin | Aucun chemin nouveau. `OpenProcess` implicite de `Process.GetProcessById` sur un processus du MÊME utilisateur : aucun droit requis (mesuré hors de l'arbre, Q2.a) |
| Honnêteté / « observé, jamais déduit » | La règle LUE-02 est une INFÉRENCE (fenêtre au premier plan ≠ regard) acceptée par le verrou ; ses limites connues (accueil/Chat, console) sont écrites (Open Questions), jamais tues |
| Robustesse : aucune source ≠ crash | Tout le chemin premier plan est sous `try` → « indisponible » ; le moniteur garde son `try { _tracker?.Observe(...) } catch { }` (`SessionMonitor.cs:179`) |
| UI et commentaires en **français** | Noms proposés : `IPremierPlan`, `PremierPlanWin32`, `EtatPremierPlan`, `StatutPremierPlan`, `ContexteLecture`, `CauseTraitement`, `MotifMasquage.LueParFocus / LueAuPremierPlan / Repondue`, `HorizonsSessions.GraceLecture`, `LectureAppBureau.Selection` |
| `Assembly.Location` interdit | Les gardes de source lisent `GardesPerimetreTests.CheminSources()` (attribut MSBuild) |
| Gardes à garder vertes sans assouplir leurs listes | `GardesPerimetreTests` (noms : `Uia`, `…ForegroundWatch` — les noms proposés passent ; chemin de l'app : `Claude_` / `claude-code-sessions` interdits HORS `RacinesEtat.cs`, commentaires compris — Piège 6), `ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests` (`FromUnixTimeMilliseconds` interdit dans `Services/` hors 4 exemptions : le détecteur passe par `UsageNormalization.InstantDepuisEpochMillisecondes`), `HorizonsSessionsTests` (`HorizonsSessions.Silence` lu par le SEUL `SessionMonitor.cs` ; aucun `TimeSpan.From` dans les 4 consommateurs), `ContratHooksDocumenteTests` (lit le corps d'`EstAttente(SessionActivity`, `prec.Source == v.Source`, `InstantDuSignal` dans le détecteur) |
| Ne pas lancer l'overlay ; aucune écriture hors dépôt | Toutes les mesures de cette recherche : bloc-notes de session, lecture seule (sonde `sonde-premier-plan-30`, lancée dans l'arbre puis hors de l'arbre par `Win32_Process.Create`) |
| GSD : travailler dans un flux GSD | Ce document est écrit par `/gsd:plan-phase` (recherche) |

---

## Summary

La phase n'ajoute ni source ni libellé : elle ajoute une **troisième façon de quitter le widget**. Aujourd'hui une
session en attente n'en sort que par un geste (« Marquer traitée », « Archiver ») ou par NET-01 (une réponse
observée sur la même source). Le détecteur `SessionTreatmentTracker` (113 lignes) possède déjà l'ajout et la
purge du magasin réversible ; il reçoit en plus, par cycle, le dernier focus de chaque session, la session
sélectionnée et l'instant depuis lequel la fenêtre `claude` est au premier plan. Le moniteur les lui passe depuis la
lecture de l'app bureau qu'il fait DÉJÀ à chaque cycle (`SessionMonitor.cs:158-159`) — aucun second `Lire`.

**Ce qui a été mesuré.** `Process.GetProcessById(88220).ProcessName` rend `claude`, dans l'arbre de l'app ET depuis
un processus créé par WMI (parent `WmiPrvSE`, la situation de l'overlay lancé par `explorer`). Coût : 0,021 ms
médian, 0,025 ms au p90 (300 appels, 510 processus sur la machine, .NET 8.0.25) ; `GetForegroundWindow` +
`GetWindowThreadProcessId` : sous la microseconde. Le cache d'une seconde n'est donc pas une nécessité de
performance ; il sert à la cohérence entre le widget et le rapport. Machine verrouillée au moment de la mesure :
le premier plan est `LockApp` — le verrouillage éteint LUE-02 de lui-même. Sur la machine, **17 processus
s'appellent `claude`** : 13 de l'app bureau (une seule fenêtre principale, PID 88220, titre « Claude ») et
**3 CLI Claude Code** (`…\Claude\claude-code\2.1.281\claude.exe`, sans fenêtre). Le nom seul ne distingue pas l'app
de la CLI ; c'est sans conséquence tant que la CLI n'a pas de fenêtre à elle (Open Question 3).

**Ce qui change dans le code** — quatre fichiers de `Services/` et un de câblage :
1. `SessionTreatmentTracker` : `Observe(vainqueurs, now, ContexteLecture? lecture = null)` ; la décision
   NET-03/LUE en un bloc ; une mémoire de cause par session (`CauseDe(id, tts)`), alimentée par LUE-01, LUE-02 et
   NET-01.
2. `LecteurAppBureau` : `Interpreter` lit `lastFocusedAt` même sans `cliSessionId` ; `LectureAppBureau.Selection`
   (le max sur tous les fichiers de la fenêtre de lecture, doublons et fichiers sans identifiant compris).
3. `PremierPlanWin32 : IPremierPlan` (nouveau) : deux P/Invoke `user32`, `Process.GetProcessById`, état
   « depuis », cache 1 s, sonde injectable pour tester l'état sans fenêtre.
4. `SessionMonitor` : paramètre `premierPlan` (dernier, optionnel, nommé), construction du `ContexteLecture`,
   cause posée sur `SessionMasquee`, constat de la règle posé sur `LectureSessions` pour le rapport.
5. `App.xaml.cs` + gardes de câblage ; puis `DiagnosticService` (LUE-03, APRÈS la clôture de 29-05, déjà faite).

**Primary recommendation :** une seule décision par session et par cycle dans le détecteur — `lue` calculée sur
l'épisode `_attenteDepuis` (strictement antérieur au focus, ou session sélectionnée avec `claude` au premier plan
depuis au moins 2,5 s après l'attente), `Set` seulement si le magasin porte un épisode antérieur, `Remove` (NET-03)
seulement si la session n'est pas lue — et la cause mémorisée pour le rapport ; tout le reste (focus, sélection,
premier plan) arrive par un `ContexteLecture` nul par défaut, ce qui rend LUE-04 vrai par construction.

---

## Q1 — LUE-01 dans le détecteur

### 1.a Ce que fait le détecteur aujourd'hui (`SessionTreatmentTracker.cs`, 113 lignes)

| Lignes | Rôle |
|---|---|
| `:40` | `_dernier` : par session, (source, état) du dernier vainqueur vu — NET-01 en a besoin |
| `:43` | `_attenteDepuis` : par session, l'instant (ms) de l'épisode d'attente courant, stable pendant l'épisode |
| `:54-56` | `EstAttente` = `WaitingTurn`, `WaitingAttention`, `WaitingDeduced` (tenu égal à `AffichageSessions.EstUneAttente` par test ; son corps est lu par `ContratHooksDocumenteTests:346-357` — ne pas le déplacer) |
| `:66-67` | `InstantDuSignal` = `min(UpdatedAt, now)` en ms |
| `:76` | `var traitees = _store.Load();` — UNE lecture du magasin par cycle |
| `:88-92` | NET-01 : même source, attente → `Working` ⇒ `Set(id, _attenteDepuis ?? instant)` puis oubli de l'épisode |
| `:98-102` | épisode : posé à l'ouverture, n'avance que si le signal affirme plus récent |
| `:106-108` | NET-03 : attente ET épisode courant > valeur mémorisée ⇒ `Remove` |

`TreatedStore.Set` (`TreatedStore.cs:76-82`) relit le fichier et le **réécrit TOUJOURS** (tmp + `Move`), même
pour une valeur identique ; `Remove` (`:89-95`) ne réécrit que si l'entrée existait. D'où la règle du verrou
« comparer à la valeur mémorisée avant d'écrire » : la comparaison doit se faire dans le détecteur, contre
`traitees` (la lecture de tête de cycle).

Le détecteur est appelé par le moniteur sur les **vainqueurs non enrichis**, après l'arbitrage et avant les filtres
(`SessionMonitor.cs:173-179`) ; le moniteur est appelé par `SessionsViewModel.Refresh` (minuteur `DispatcherTimer`
de 2 s, thread UI, plus un appel après chaque geste) et par `DiagnosticService.BuildReportAsync`
(`_moniteurSessions.Inspecter(_clock.UtcNow)`, sur la continuation du contexte WPF). Les deux appelants sont sur le
thread UI (MEDIUM : déduit du code, `RunAsync`/`LogStartupAsync` n'utilisent pas `Task.Run`) ; le détecteur n'a pas
de verrou. Recommandation : garder cette hypothèse écrite, et poser un `lock` dans `PremierPlanWin32` (coût nul,
même motif que `LecteurAppBureau`).

### 1.b L'instant d'attente = l'épisode du détecteur

Le verrou dit « `instantAttente` = l'instant que le signal RETENU porte (`SessionSnapshot.UpdatedAt`) ». Dans le
détecteur, cet instant est déjà matérialisé : `_attenteDepuis[id]`, posé depuis `InstantDuSignal(v, nowMs)` =
`min(UpdatedAt, now)` et seulement avancé. Trois raisons d'utiliser CETTE valeur (et non l'`UpdatedAt` du vainqueur
du cycle) pour la comparaison ET pour l'écriture :

1. **Cohérence avec NET-03.** NET-03 compare `_attenteDepuis[id]` à la valeur mémorisée. Écrire autre chose
   ferait purger au cycle suivant ce qui vient d'être écrit (Fait n° 2 de l'en-tête).
2. **Égalité en régime permanent.** Tant que la même source parle, `_attenteDepuis[id] == UpdatedAt` (au plafond
   `now` près). Le verrou est donc respecté à la lettre dans tous les cas ordinaires.
3. **Prudence au bord.** Quand un vainqueur plus ancien succède à un plus récent pendant la même attente (le
   fichier de hook `Stop` supprimé à la frontière des tours, le transcript reprend — relevé de phase 27), l'épisode
   garde l'instant le plus récent : la comparaison au focus est plus exigeante, jamais plus laxiste.

À écrire au plan comme **D-30-01** : « l'instant d'attente comparé et mémorisé est l'épisode du détecteur
(`_attenteDepuis`), égal à `UpdatedAt` du signal retenu en régime permanent ».

Sources de cet instant, selon le vainqueur (rappel, rien à coder) : transcript = `timestamp` de la dernière ligne
`user`/`assistant` (D-28-01) ; hook = `updated_at` ; app bureau (`blocked`) = `InstantClassification`, figé par
épisode par le lecteur (`LecteurAppBureau.cs:273-293`) ; attente déduite = l'instant du dernier TRAVAIL, que
`AppliquerSilence` ne réécrit pas (`SessionMonitor.cs:249-252`).

### 1.c La décision unique : NET-03 et LUE-01 (et LUE-02) en un bloc

Remplace `SessionTreatmentTracker.cs:104-108`. Pour une session en attente d'épisode `cur` :

| # | Lue ce cycle ? | Magasin (`traitees`, lu en tête) | Action | Écritures |
|---|---|---|---|---|
| 1 | non | absente | rien | 0 |
| 2 | non | `tts ≥ cur` | rien (déjà traitée pour cet épisode ou au-delà) | 0 |
| 3 | non | `tts < cur` | `Remove` — **NET-03 inchangé** | 1 |
| 4 | oui | absente | `Set(id, cur)` | 1 |
| 5 | oui | `tts < cur` | `Set(id, cur)` — un nouvel épisode déjà lu : PAS de `Remove` préalable | 1 |
| 6 | oui | `tts ≥ cur` | rien (cause mémorisée seulement) | 0 |

Conséquence : au plus UNE écriture par session et par épisode ; au cycle suivant, `traitees[id] == cur` ⇒ cas 6,
zéro écriture. C'est la preuve du critère 2 (« sans réécrire `treated.json` à chaque rafraîchissement »).

NET-01 (`:88-92`) reste AVANT ce bloc et inchangé dans sa condition. Seul ajout : mémoriser la cause « répondue ».
(Optionnel, non exigé : sauter le `Set` de NET-01 si `traitees[id] ≥ ep` — il ne coûte qu'une écriture par
transition, pas par cycle.)

### 1.d « Pas d'écriture à chaque cycle » — comment le prouver

`TreatedStore` est `sealed`, sans compteur. Preuve par **sentinelle de date d'écriture** : après le cycle qui
masque, poser `File.SetLastWriteTimeUtc(chemin, 2000-01-01)` ; jouer cinq cycles ; la date doit être restée la
sentinelle (`WriteAtomic` remplace le fichier par `Move` : toute écriture la changerait). Mutation de
falsification : supprimer la comparaison `tts < cur` du cas 5/6 (toujours `Set` quand lue) ⇒ le test rougit.

### 1.e Interactions, une par une

| Cas | Ce qui se passe | Test |
|---|---|---|
| **NET-01 (répondue)** | Session lue (cas 4), l'utilisateur répond : même source, attente → `Working` ⇒ NET-01 réécrit la même valeur `cur` et la cause devient « répondue ». La session reste masquée pendant qu'elle travaille (comportement v1.6 : le filtre `treatedMap.ContainsKey` ne regarde pas l'état — `SessionMonitor.cs:206` ; voir Open Question 2) | `Repondue_remplace_la_cause_lue` |
| **NET-03 (réapparition)** | Nouveau tour fini APRÈS le focus : `cur > tts`, non lue ⇒ cas 3, `Remove`, la session revient « En attente ». Si l'utilisateur l'ouvre ensuite ⇒ cas 4, masquée pour le nouvel épisode | `Une_session_lue_revient_sur_un_nouveau_tour` |
| **478 min** (`TreatedSessionsTests.cs:340-375`) | Le moniteur de ce test n'a pas de lecteur : `ContexteLecture` nul ⇒ LUE inerte, test inchangé par construction. À AJOUTER : la variante avec lecteur (focus antérieur au hook, premier plan `explorer`) ⇒ toujours visible, magasin vide | `Le_scenario_des_478_minutes_reste_visible_avec_la_regle_lue` |
| **Déduite (D-30-07)** | Le prédicat `EstAttente` l'inclut : un `WaitingDeduced` dont l'instant (dernier travail) précède le focus est lu. Conséquence à écrire : une session qui s'est tue APRÈS que l'utilisateur l'a regardée travailler ne s'affiche jamais « En attente ? » si ce regard est postérieur à son dernier signal | `La_deduite_ouverte_ensuite_est_lue`, `La_deduite_redevenue_attente_observee_plus_recente_revient` |
| **Permission (`WaitingAttention`)** | Verrou : lue au même titre. Une demande de permission vue puis laissée sans réponse quitte le widget (la session reste bloquée). Conséquence à écrire au plan (D-30-08) et à constater en phase 31 | `Une_permission_vue_est_lue` |
| **Geste « Marquer traitée »** | Inchangé : `SessionsViewModel` écrit `item.UpdatedAtMs` directement dans `TreatedStore`, sans passer par le détecteur ⇒ cause inconnue du détecteur ⇒ motif résiduel `Traitee` (Q3.b). `MoniteurAppBureauTests:417-440` asserte `MotifMasquage.Traitee` après un geste : reste vert | existant |
| **Archivée** | Le détecteur observe AVANT les filtres : il peut écrire `treated.json` pour une session archivée ; l'archivage prime à l'affichage (`SessionMonitor.cs:205`) | existant (`Archivee_ET_traitee_…`) |
| **Indéterminée** | `Unknown` n'est pas une attente : aucune règle « lue » | — |

### 1.f Le dictionnaire du dernier focus

Construit par le moniteur à partir de `appBureau.ParSession` (la lecture de CE cycle, `SessionMonitor.cs:159`), clé
**insensible à la casse** comme `ParSession` (`LecteurAppBureau.cs:243`) — les dictionnaires internes du détecteur
sont ordinaux ; la recherche de focus et la comparaison à la session sélectionnée doivent être
`OrdinalIgnoreCase`. Coût : une vingtaine d'entrées par cycle. Aucun second `Lire` (OBS-01, et 29-05 interdit
`.Lire(` dans `DiagnosticService.cs`).

---

## Q2 — LUE-02 : le premier plan, la sélection, la grâce

### 2.a Mesuré sur cette machine (2026-09-25, 23:46-23:55, lecture seule)

Sonde .NET 8 jetable (`scratchpad/sonde-premier-plan-30`), jouée **dans l'arbre de l'app** puis **hors de l'arbre**
(`Invoke-CimMethod Win32_Process Create`, parent `WmiPrvSE`, même motif que `29-SONDE-HORS-ARBRE.txt`).

| Grandeur | Dans l'arbre | Hors de l'arbre (comme l'overlay) |
|---|---|---|
| Runtime | .NET 8.0.25 | .NET 8.0.25 |
| Processus sur la machine | 508 | 510 |
| Premier plan | `LockApp` (PID 88808) — session verrouillée | `LockApp` |
| `Process.GetProcessById(88220).ProcessName` | **`claude`** | **`claude`** |
| `Process.GetProcessById(0).ProcessName` | `Idle` | `Idle` |
| `GetForegroundWindow` + `GetWindowThreadProcessId` (2 000 appels) | médiane 0,0000 ms, max 0,0012 ms | max 0,0077 ms |
| `GetProcessById(pid).ProcessName`, objet neuf à chaque appel (300) | médiane **0,0225 ms**, p90 0,0292, max 0,0752 | médiane **0,0212 ms**, p90 0,0249, max 0,0509 |

`Get-Process -Name claude` (PowerShell 5.1) : 17 processus. PID 88220 est le seul avec une fenêtre principale
(`MainWindowHandle` 1187080, titre « Claude »), chemin `C:\Program Files\WindowsApps\…\app\claude.exe` ; 13 autres
processus Electron du même exécutable sans fenêtre ; **3 CLI** Claude Code 2.1.281 nommées `claude` aussi
(`…\AppData\Roaming\Claude\claude-code\2.1.281\claude.exe` vu de l'arbre, démarrées 20:30:25-33 : les sessions
relancées par l'app au geste A), sans fenêtre. Le relevé de phase 27 (20:33:27) avait vu le premier plan sur
PID 88220, processus `claude`. Rien d'écrit hors du bloc-notes.

### 2.b L'ancienne implémentation (retirée en phase 21, `git show ff783d4^:src/Chronos/Services/WindowsForegroundWatch.cs`)

| Ce qu'elle faisait | Reprendre ? |
|---|---|
| `[DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();` | **Oui** (en `nint`) |
| `GetWindowText` + « le titre contient Claude » | **Non** : un onglet de navigateur « Claude » ou `claude.ai` passerait ; le verrou fixe le NOM DU PROCESSUS |
| Seam `IForegroundWatch.IsClaudeForeground() : bool` | **Non tel quel** : le nom finit par `ForegroundWatch` (garde), et un booléen ne dit ni « depuis quand » ni « indisponible » (LUE-04 exige de le dire) |
| « Best-effort, ne lève jamais ; erreur ⇒ false » | **Oui**, mais en trois états (Claude / autre / indisponible), pas deux |
| « Non unit-testé, dépend d'une vraie fenêtre » | **Non** : isoler la sonde Win32 derrière un délégué pour tester l'état (depuis, cache, erreurs) sans fenêtre ; la sonde réelle n'a qu'un test de fumée |
| Appelée depuis le chemin synchrone `SessionMonitor.Read` (thread UI) | **Oui** : coût mesuré < 0,03 ms |

### 2.c Forme recommandée

```csharp
namespace Chronos.Services;

/// <summary>Ce que la sonde de premier plan a vu. Neutre : aucun HWND, aucun type WPF.</summary>
public enum StatutPremierPlan { NonBranche, Claude, AutreProcessus, AucuneFenetre, Indisponible }

/// <param name="Depuis">Premier échantillon d'une suite ININTERROMPUE où le premier plan était <c>claude</c> ;
/// nul sinon.</param>
public sealed record EtatPremierPlan(StatutPremierPlan Statut, string? Processus, System.DateTimeOffset? Depuis,
                                     string? Raison = null)
{
    public static readonly EtatPremierPlan NonBranche = new(StatutPremierPlan.NonBranche, null, null);
    public System.DateTimeOffset? ClaudeDepuis => Statut == StatutPremierPlan.Claude ? Depuis : null;
}

public interface IPremierPlan
{
    /// <summary>Best-effort, jamais d'exception. <paramref name="now"/> est l'instant du moniteur (horloge injectée).</summary>
    EtatPremierPlan Lire(System.DateTimeOffset now);
}
```

`PremierPlanWin32` : un constructeur public sans argument (production) et un constructeur `internal` qui reçoit la
sonde (`Func<(StatutPremierPlan Statut, string? Processus, string? Raison)>`) pour les tests (`InternalsVisibleTo`
existe déjà, `Chronos.csproj`). Machine d'état, sous `lock` :

- **Cache 1 s** : si le dernier échantillon date de moins d'une seconde (et que `now` n'a pas reculé), le rendre.
- **Depuis** : `Claude` succédant à `Claude` ⇒ `Depuis` conservé ; `Claude` après tout autre statut (ou premier
  échantillon) ⇒ `Depuis = now`. Tout statut autre que `Claude` ⇒ `Depuis = null`.
- **Trou d'échantillonnage** (recommandé, discrétion) : si deux échantillons sont séparés de plus de ~5 s (veille,
  minuteur suspendu), repartir `Depuis = now` — on ne sait pas ce qui s'est passé entre les deux.
- **Sonde réelle** : `GetForegroundWindow() == 0` ⇒ `AucuneFenetre` (bascule de fenêtre en cours : pas une panne) ;
  `GetWindowThreadProcessId` rend 0 ⇒ `Indisponible` ; `Process.GetProcessById` lève (processus terminé entre-temps)
  ⇒ `Indisponible` avec le type de l'exception ; nom égal à `claude` en `OrdinalIgnoreCase` ⇒ `Claude` ; sinon
  `AutreProcessus` avec le nom (le diagnostic l'imprime : « explorer », « LockApp », « Chronos »…).
- Comparaison **d'égalité**, jamais `StartsWith` : « claudette » n'est pas `claude`.

### 2.d La session sélectionnée — à calculer DANS le lecteur

État actuel : `Interpreter` rend `SansCliSessionId` avant de lire le moindre champ (`LecteurAppBureau.cs:393-394`) ;
le cache stocke `(Ecriture, Taille, Issue, Meta)` avec `Meta` nul pour ces fichiers (`:139-140`, `:234-236`) ;
`ParSession` ne garde que le fichier le plus récent par `lastActivityAt` pour un identifiant (`:243-253`). La
fixture `sans-cliSessionId.json` PORTE un `lastFocusedAt` (1781190743865) : le champ existe sur ces fichiers.

Recommandation (D-30-04) :
- `Interpreter` lit `lastFocusedAt` AVANT de tester `cliSessionId` et le rend par un `out` supplémentaire (garder la
  signature à deux arguments en surcharge : `LecteurAppBureauTests:44-45` l'appelle) ; l'entrée de cache gagne
  `DateTimeOffset? Focus` (aussi pour les doublons écartés).
- `LectureAppBureau` gagne un dernier paramètre optionnel `SessionSelectionnee? Selection = null`, record
  `(string? CliSessionId, DateTimeOffset DernierFocus)` : le max de `Focus` sur TOUS les fichiers servis ce cycle
  (valides, doublons, sans identifiant). `CliSessionId` nul quand le max appartient à un fichier sans identifiant
  ⇒ **aucune** session du widget n'est sélectionnée. Ex aequo : le premier chemin ordinal (même règle que les
  doublons, `:247-250`). `Vide(...)` rend `Selection = null`.
- Une seule construction de `LectureAppBureau` dans `src/` (`:262`) et aucune dans les tests : l'ajout est sans
  casse. Garde de lecture seule n° 1 (`LectureSeuleAppBureauTests`) : n'ajouter aucune API d'écriture.

Limite à écrire : un fichier de plus de 24 h n'est pas ouvert (`HorizonsSessions.LectureAppBureau`). La session
réellement sélectionnée pourrait avoir un fichier plus vieux que 24 h pendant qu'une autre, plus récemment active
mais focalisée plus anciennement, tient le max dans la fenêtre. Cela exige une session restée sélectionnée 24 h
sans une réécriture (sélectionner réécrit le fichier) pendant qu'une autre travaille seule en arrière-plan : LOW.

### 2.e Le prédicat LUE-02 et la grâce

Trois textes décrivent la grâce : CONTEXT « 2,5 s après `instantAttente` » ; REQUIREMENTS « 2,5 s après la fin du
tour » ; ROADMAP critère 3 « au premier plan depuis au moins 2,5 s » ; et le test du CONTEXT fournit « premier plan
`claude` depuis 20:33:27 ». Le prédicat qui les satisfait tous, à écrire comme **D-30-02** :

> Session `s` en attente d'épisode `cur` ; lue par LUE-02 si `s` a un focus connu, `s` est la session
> sélectionnée, le premier plan est `claude` depuis `d`, et `now − max(cur, d) ≥ HorizonsSessions.GraceLecture`
> (2,5 s, borne incluse).

Geste B : `cur` = 18:34:04.328Z, `d` = 18:33:27Z ⇒ max = `cur` ⇒ lue dès `now` = 18:34:06.828Z (visible à
18:34:06.827Z). Avec l'échantillonnage réel (2 s) et le cache (1 s), la ligne « En attente » apparaît donc de 2,5 à
~5,5 s avant de disparaître : c'est la grâce voulue, à écrire pour le constat de phase 31.

Le terme `d` n'a d'effet propre que dans deux cas : (i) l'utilisateur revient sur `claude` alors que l'attente est
ancienne — il faut alors 2,5 s de premier plan continu ; (ii) la **réserve** de la phase 27 (retour sans mise à jour
de `lastFocusedAt`, 16:23:51 → 19:51) — LUE-02 la couvre pour la session sélectionnée. Sinon, le retour alt-tab
met `lastFocusedAt` à jour (geste A) et LUE-01 conclut seule.

Constante : `HorizonsSessions.GraceLecture = 2,5 s` (le type unique des seuils du widget ; ajouter à
`La_chaine_des_horizons_tient` l'inégalité `GraceLecture < Silence` et la valeur). Ne PAS la déclarer dans
`SessionMonitor.cs` (garde « aucun `TimeSpan.From` »). Le cache d'une seconde reste une constante privée de
`PremierPlanWin32` (ce n'est pas un seuil d'affichage).

### 2.f Où vit l'appel

Le moniteur, juste avant `_tracker?.Observe(...)` (`SessionMonitor.cs:179`) : `_premierPlan?.Lire(now)` sous `try`
(exception ⇒ `Indisponible`), puis `ContexteLecture` si `appBureau is { DossierTrouve: true }`, sinon nul. Le premier
plan est lu même sans source app-bureau : le rapport doit pouvoir dire ce qu'il voit. Le détecteur ne reçoit que
`ClaudeDepuis` (un `DateTimeOffset?`) : il ne dépend pas du type d'état Win32, ce qui découple les plans (Q5.c).

Effet de bord connu : le rapport de diagnostic appelle `Inspecter` quand l'utilisateur l'a demandé depuis le menu
de Chronos — le premier plan est alors Chronos ou l'éditeur. Le rapport dira donc « premier plan : Chronos », ce
qui est vrai à cet instant ; les causes déjà mémorisées (Q3.b), elles, restent lisibles.

### 2.g Nommage, gardes, périmètre

- `IPremierPlan`, `PremierPlanWin32`, `EtatPremierPlan`, `StatutPremierPlan` : aucun ne contient `Uia` ni ne finit
  par `ForegroundWatch` (`GardesPerimetreTests.cs:26-44`, réflexion ordinale). Aucun `using System.Windows.Automation`.
- **Aucun littéral `Claude_` ni `claude-code-sessions` dans le nouveau fichier, commentaires compris** : la garde
  n° 2 d'APP-05 (29-05, `Le_chemin_de_l_app_bureau_n_existe_que_dans_le_resolveur_de_racines`) balaie tous les
  `*.cs` de `src/` récursivement et ne tolère que `RacinesEtat.cs`. Écrire « le processus de l'app bureau
  (`…\WindowsApps\…\app\claude.exe`) », jamais le nom du paquet.
- P/Invoke en `DllImport` (convention du dépôt : `WindowsCredentialStore.cs`, `Interop/NativeMethods.cs`).
  `LibraryImport` exigerait `AllowUnsafeBlocks`, absent de `Chronos.csproj` : ne pas l'introduire pour deux
  fonctions.

---

## Q3 — LUE-03 : le motif et les deux instants

### 3.a Aujourd'hui

- `MotifMasquage` (`LectureSessions.cs:15-26`) : `Archivee`, `Traitee`, `Indeterminee`. `SessionMasquee(Session,
  Motif)` (`:29`). Le filtre : archivée, puis `treatedMap.ContainsKey` ⇒ `Traitee`, puis indéterminée
  (`SessionMonitor.cs:199-210`).
- Le rapport (`DiagnosticService`, fichier livré par 29-05 — citer par contenu) : une ligne par masquée,
  « … — masquée par {LibelleMotif(m.Motif)} » ; `LibelleMotif(Traitee)` = « treated.json (« traité » — hystérésis
  automatique OU geste explicite ; réversible) » ; défaut « un filtre non nommé ».
- Tests qui assertent `Traitee` pour un magasin écrit à la main ou par geste, SANS détecteur : `InspectionSessionsTests`
  (`Une_session_traitee_est_masquee_et_le_motif_le_dit`, `Le_cas_e465420e_devient_lisible_en_une_lecture`),
  `MoniteurAppBureauTests` (`Une_question_marquee_traitee_ne_revient_pas_…`, geste). Ils doivent rester verts :
  le résidu s'appelle toujours `Traitee`.

### 3.b La cause se mémorise dans le détecteur

Le magasin ne porte que `id → ms`. Changer son format (`{id: {ts, motif}}`) casserait la lecture tolérante
existante (`TryGetInt64` sur un objet ⇒ entrée ignorée) et toute rétrogradation : **ne pas le faire** (D-30-05).
Le détecteur connaît la cause au moment où il agit :

| Constat du détecteur | Cause mémorisée | Libellé proposé au rapport |
|---|---|---|
| LUE-01 (cas 4, 5 ou 6) | `LueParFocus(attente, focus)` | « lue : focus à 16:00:39 > attente à 15:59:05 » |
| LUE-02 (cas 4, 5 ou 6) | `LueAuPremierPlan(attente, depuis, constat)` | « sélectionnée au premier plan depuis 37 s (claude) — attente à 20:34:04 » |
| NET-01 | `Repondue(attente)` | « répondue (même source : attente puis travail observé) » |
| aucun (geste, ou entrée antérieure au démarrage) | — | résidu `Traitee` : « treated.json (« traité » — marquée à la main, ou traitée avant le démarrage de l'overlay ; réversible) » |

Mémoire : `Dictionary<string, (long Episode, CauseTraitement Cause)>`, posée par les trois constats, retirée par
NET-03. `CauseDe(id, tts)` rend la cause seulement si `Episode == tts` (le magasin porte l'épisode dont on connaît
la cause) — sinon nul ⇒ résidu. Après un redémarrage, LUE-01 se recalcule au premier cycle (cas 6 : cause posée
sans écriture) ; seules les causes LUE-02 et NET-01 antérieures au démarrage retombent dans le résidu. Limite
écrite, honnête.

`N s` de « depuis N s » : `constat − depuis`, figé au moment de la décision (le rapport lu une heure plus tard ne
doit pas dire « depuis 3 600 s »).

### 3.c Les types (noms à la discrétion du plan)

```csharp
// LectureSessions.cs — AJOUTÉS EN FIN (même convention que SessionActivity.WaitingDeduced ; MotifMasquage n'est
// persisté nulle part, mais l'ordre de déclaration sert l'ordre du rapport).
public enum MotifMasquage { Archivee, Traitee, Indeterminee, LueParFocus, LueAuPremierPlan, Repondue }

public sealed record CauseTraitement(MotifMasquage Motif, System.DateTimeOffset Attente,
    System.DateTimeOffset? Focus = null, System.DateTimeOffset? PremierPlanDepuis = null,
    System.DateTimeOffset? Constat = null);

public sealed record SessionMasquee(SessionSnapshot Session, MotifMasquage Motif, CauseTraitement? Cause = null);
```

Le filtre du moniteur devient :

```csharp
if (treatedMap is not null && treatedMap.TryGetValue(s.SessionId, out var tts))
{
    var cause = _tracker?.CauseDe(s.SessionId, tts);
    masquees.Add(new SessionMasquee(s, cause?.Motif ?? MotifMasquage.Traitee, cause));
    continue;
}
```

`SessionMasquee` n'est construite qu'à trois endroits, tous dans `SessionMonitor.cs:205-207` : le paramètre
optionnel ne casse rien.

### 3.d Le rapport

- Ligne par masquée : garder le préfixe existant (id court, nom, état, âge), remplacer « masquée par … » par le
  libellé de la cause (tableau 3.b). Heures : `ToLocalTime().ToString("HH:mm:ss")` (convention du fichier :
  `ToLocalTime().ToString("… HH:mm")`). Le verrou écrit « HH:MM » : les secondes sont à la discrétion du plan et
  **recommandées** — le relevé travaille à la seconde, et « focus à 16:00 > attente à 16:00 » serait illisible.
- Nouvelle sous-section « Règle « lue » (LUE-01, LUE-02) », lue dans la MÊME `LectureSessions` (champ optionnel
  ajouté en fin, par exemple `ConstatLecture? Lue = null`) :
  - « Session sélectionnée dans l'app : `11456cab` (focus 20:30:44) » / « aucune — le dernier focus est une session
    sans cliSessionId » / « inconnue — source app-bureau absente : règle « lue » inactive (comportement v1.6) » ;
  - « Premier plan : claude depuis 12 s — LUE-02 active » / « explorer — LUE-02 inactive » / « indisponible
    (GetWindowThreadProcessId a échoué) — LUE-02 inactive, LUE-01 seule » / « NON BRANCHÉ ».
- Garde OBS-01 étendue : `DiagnosticService.cs` ne contient ni `IPremierPlan`, ni `PremierPlanWin32`, ni `.Lire(`
  (déjà interdit par 29-05) ; il ne lit que `lecture.Masquees[i].Cause` et `lecture.Lue`.
- « Jamais un masquage sans cause » : un test qui parcourt `Enum.GetValues<MotifMasquage>()` et exige un libellé
  différent du défaut « un filtre non nommé » pour chaque valeur (la méthode est `private static` : passer par le
  rapport, ou la rendre `internal` comme `LibelleSourceSession` en 29-03).

---

## Q4 — LUE-04 : pas de faux masquage

| # | Situation | Résultat attendu | Mécanisme |
|---|---|---|---|
| D1 | Moniteur sans lecteur (tests existants, terminal pur) | v1.6 exact | `ContexteLecture` nul |
| D2 | Dossier de l'app absent | v1.6 exact ; rapport « inactive » | `DossierTrouve == false` ⇒ contexte nul |
| D3 | `Lire` lève | v1.6 exact | `appBureau = null` (`SessionMonitor.cs:159`) |
| D4 | Session inconnue de l'app (pas de fichier) | règle inactive pour ELLE, les autres inchangées | clé absente du dictionnaire |
| D5 | Fichier `sans-cliSessionId.json` | aucune jointure ⇒ inactive | pas dans `ParSession` |
| D6 | `lastFocusedAt` absent ou hors plancher dans un fichier joint | inactive pour cette session ; ne peut pas être sélectionnée | focus nul |
| D7 | Fichier illisible à sa première lecture | inactive ; relecture ratée APRÈS une lecture valide ⇒ la dernière valeur reste servie (comportement du lecteur, pas de clignotement) | cache du lecteur |
| D8 | Win32 en échec / premier plan non branché | LUE-02 inactive, LUE-01 seule ; le rapport le dit | `ClaudeDepuis` nul |

Le paramètre `premierPlan` nul par défaut rend l'oubli de câblage SILENCIEUX (Piège 9 de la phase 29) : garde de
source `Le_moniteur_de_production_recoit_le_premier_plan` (fragment de l'enregistrement du moniteur, même motif que
`GardesPerimetreTests.cs:241-258`). L'ajout de `premierPlan: sp.GetRequiredService<IPremierPlan>()` en DERNIER
argument nommé ne crée aucun `"));"` intermédiaire : les gardes existantes qui découpent le fragment jusqu'au
premier `"));"` (`:219-230`, `:249-257`) restent justes.

---

## Q5 — Les cas nommés, avec les valeurs du relevé

### 5.a Instants (heure locale = UTC+2 le 2026-09-25 ; epoch ms pour dériver les fixtures)

| Instant | Local | UTC | Epoch ms |
|---|---|---|---|
| Fin JARVIS (`end_turn`) | 15:59:05 | 13:59:05Z | 1790344745000 |
| Fin ADVANCED SHEET | 15:56:10 | 13:56:10Z | 1790344570000 |
| Focus JARVIS | 16:00:39 | 14:00:39Z | 1790344839000 |
| Focus ADVANCED SHEET | 16:00:59 | 14:00:59Z | 1790344859000 |
| Relevé (horloge) | 16:08:00 | 14:08:00Z | 1790345280000 |
| 3e session : fin / focus | 16:05:00 / 16:00:00 | 14:05Z / 14:00Z | 1790345100000 / 1790344800000 |
| Geste B : fin du tour | 20:34:04.328 | 18:34:04.328Z | 1790361244328 |
| Geste B : focus (= fixture `session-courante-geste-b.json`) | 20:30:44.976 | 18:30:44.976Z | **1790361044976** |
| Geste B : premier plan `claude` depuis | 20:33:27 | 18:33:27Z | 1790361207000 |
| Geste A : focus avant / attente / focus après | 16:23:51 / 20:06:00 / 20:30:44.976 | 14:23:51Z / 18:06Z / 18:30:44.976Z | 1790346231000 / 1790359560000 / 1790361044976 |

Identifiants réels : JARVIS `c17a1b03-3c8d-4c05-a747-dd77bc702e1b` (fixture `fin-de-tour-review-ready.json`,
`review_ready` ne dépose rien) ; ADVANCED SHEET `88677186-8690-44dc-ac77-7a319a0aa5cb` (dériver
`sans-postTurnSummary.json` en remplaçant `dae57d29-d277-4e4c-8cad-b4d2b97701e5`) ; geste A/B `11456cab-…`
(`session-courante-geste-b.json`). Dérivation TEXTUELLE par `RacineAppBureau.Deriver(nom, (ancien, nouveau))` sur
la valeur de `"lastFocusedAt"` (jamais de re-sérialisation, précédent DEL-06). Les `TreatedStore` des tests doivent
avoir une `FakeClock` proche de ces instants (rétention 24 h mesurée à SON horloge — note de
`MoniteurAppBureauTests`).

### 5.b Le tableau des cas

**Détecteur, pur** (`LectureSessionsTests` ou extension de `TreatedSessionsTests`, avec `Sig(...)` et un
`ContexteLecture` construit à la main) :

| Cas | Entrée | Attendu |
|---|---|---|
| L01 `Une_attente_anterieure_au_focus_est_lue` | `WaitingTurn` @13:59:05Z, focus 14:00:39Z | magasin = 1790344745000, cause `LueParFocus` |
| L02 `Une_attente_posterieure_au_focus_reste_en_attente` | @14:05Z, focus 14:00Z | magasin vide |
| L03 `Un_focus_egal_a_l_attente_n_est_pas_une_lecture` | @14:00:39Z, focus 14:00:39Z | magasin vide (D-30-03 : `<` strict) |
| L04 `La_lecture_n_ecrit_qu_une_fois_par_episode` | L01 puis 5 cycles | sentinelle de date d'écriture intacte (1.d) |
| L05 `Un_nouvel_episode_deja_lu_s_ecrit_sans_purge` | traitée à E1, nouvel épisode E2 < focus | une seule écriture, magasin = E2 (cas 5) |
| L06 `Une_session_lue_revient_sur_un_nouveau_tour` | traitée (lue) à E1, E2 > focus | `Remove` (NET-03), cause oubliée |
| L07 `La_deduite_ouverte_ensuite_est_lue` | `WaitingDeduced` @T0, focus T0+5 min | masquée `LueParFocus` (D-30-07) |
| L08 `La_deduite_redevenue_attente_observee_plus_recente_revient` | L07 puis `WaitingTurn` @T0+30 min > focus | revient |
| L09 `Une_permission_vue_est_lue` | `WaitingAttention` antérieure au focus | masquée (D-30-08) |
| L10 `Geste_B_le_tour_fini_sous_les_yeux_est_lu_au_premier_plan` | attente 18:34:04.328Z, focus 18:30:44.976Z, sélectionnée, `claude` depuis 18:33:27Z, now 18:34:06.828Z | `LueAuPremierPlan` |
| L11 `Geste_B_la_grace_n_est_pas_ecoulee` | idem, now 18:34:06.827Z | magasin vide |
| L12 `Geste_B_avec_explorer_au_premier_plan_reste_en_attente` | `ClaudeDepuis` nul | magasin vide |
| L13 `Un_retour_recent_au_premier_plan_attend_sa_grace` | attente ancienne, `claude` depuis now−2 s | vide ; à now−2,5 s ⇒ lue |
| L14 `Une_session_non_selectionnee_n_est_pas_lue_au_premier_plan` | sélection = autre id | vide |
| L15 `Sans_focus_la_selection_ne_suffit_pas` | id sélectionné mais focus absent du dictionnaire | vide (LUE-04) |
| L16 `Repondue_remplace_la_cause_lue` | L01 puis `Working` même source | cause `Repondue` |
| L17 `La_cause_survit_au_depart_du_premier_plan` | L10 puis cycle avec `ClaudeDepuis` nul | toujours `LueAuPremierPlan` |
| L18 `Apres_redemarrage_la_lecture_par_focus_retrouve_sa_cause` | détecteur NEUF, magasin = E, focus > E | cause `LueParFocus`, 0 écriture |
| L19 `Apres_redemarrage_une_cause_non_revue_est_residuelle` | détecteur NEUF, magasin = E, focus < E | `CauseDe` nul ⇒ `Traitee` |
| L20 `Sans_contexte_le_detecteur_est_celui_de_la_v1_6` | les 18 appels à deux arguments existants | inchangés (paramètre optionnel) |

**Moniteur, classes réelles** (lecteur sur racine temporaire, `MutableSource`/`SourceModifiable` pour les
transcripts, faux `IPremierPlan`) :

| Cas | Attendu |
|---|---|
| M01 `Le_releve_de_16_h_08_ne_se_reproduit_plus` (critère 1) : JARVIS et ADVANCED SHEET masquées `LueParFocus` avec (focus, attente) exacts ; 3e session visible « En attente » ; une 4e `Working` @14:07:30Z (focus 14:00Z) visible « Réflexion » | 2 masquées, 2 visibles |
| M02 `Geste_A_le_retour_alt_tab_rend_la_session_lue` : cycle à 18:10Z (focus 14:23:51Z) ⇒ visible ; fixture réécrite (focus 18:30:44.976Z, nouvelle date d'écriture) ; cycle à 18:31:30Z, premier plan `explorer` ⇒ masquée `LueParFocus` | visible puis masquée |
| M03 `Geste_B_…` au moniteur, fixture réelle, `claude` depuis 18:33:27Z | masquée `LueAuPremierPlan` |
| M04 `Le_dernier_focus_sur_un_fichier_sans_cliSessionId_ne_selectionne_personne` : fixture `sans-cliSessionId.json` dérivée à un focus plus récent que tous | `Selection.CliSessionId` nul ; la session finie reste visible malgré `claude` |
| M05 `Sans_cliSessionId_la_regle_ne_s_applique_pas` (fixture `sans-cliSessionId.json`, exigée par le verrou) | v1.6 exact |
| M06 `Sans_dossier_de_l_app_la_lecture_est_celle_de_la_v1_6` : comparaison champ par champ avec un moniteur sans lecteur (motif de 29-03) + magasin identique | égalité |
| M07 `Premier_plan_indisponible_LUE_02_inactive_LUE_01_seule` | L10 visible ; L01 masquée ; `lecture.Lue.PremierPlan.Statut == Indisponible` |
| M08 `Le_scenario_des_478_minutes_reste_visible_avec_la_regle_lue` | visible, magasin vide |
| M09 `Marquer_traitee_et_archiver_restent_ceux_de_la_v1_6` | geste ⇒ `Traitee` ; archivée ET lue ⇒ `Archivee` |
| M10 `La_question_de_l_app_lue_est_masquee` : `blocked` dérivé, `InstantClassification` < focus | masquée `LueParFocus` |

**Lecteur** : S01 `La_selection_est_le_focus_max_de_tous_les_fichiers` (valides, doublon écarté, sans identifiant) ;
S02 `Une_racine_absente_n_a_pas_de_selection` ; S03 `Les_six_fixtures_se_lisent_toujours` (non-régression de
`Interpreter`).

**Premier plan** (`PremierPlanWin32Tests`, sonde injectée) : P01 cache 1 s (deux appels à 999 ms ⇒ une sonde ;
à 1 000 ms ⇒ deux) ; P02 `Depuis` stable tant que `claude`, remis à nul dès un autre statut, reposé au retour ;
P03 `Claude`, `CLAUDE` ⇒ `Claude` ; `claudette` ⇒ `AutreProcessus` ; P04 sonde qui lève ⇒ `Indisponible`, jamais
d'exception ; P05 fenêtre nulle ⇒ `AucuneFenetre` (pas `Indisponible`) ; P06 horloge qui recule ⇒ nouvel
échantillon, `Depuis` reposé ; P07 fumée Win32 réelle : ne lève pas, rend un statut défini (le premier plan de la
machine de test est quelconque — n'asserter AUCUN nom).

**Rapport** (`DiagnosticServiceTests`) : R01 « lue : focus à … > attente à … » avec les instants formatés par
`ToLocalTime()` dans le test (dépend du fuseau de la machine — calculer l'attendu, ne pas l'écrire en dur, Piège 10
de la phase 29) ; R02 « sélectionnée au premier plan depuis N s » ; R03 « répondue » ; R04 résidu ; R05 « Premier
plan : indisponible … LUE-01 seule » ; R06 « Session sélectionnée : … » / « aucune » / « inactive » ; R07 aucun
motif sans libellé.

**Gardes** : G01 câblage `premierPlan:` (source) ; G02 miroir DI (`CompositionRootTests.Le_graphe_DI_resout_la_chaine
_de_sessions` : `IPremierPlan` résolu, `Assert.Same` avec une propriété `internal PremierPlan` du moniteur, jamais
`Inspecter`) ; G03 `DiagnosticService.cs` sans `IPremierPlan`/`PremierPlanWin32` ; G04 `HorizonsSessions.GraceLecture`
dans la chaîne ; G05 (optionnelle) garde croisée document ↔ code sur le §3 du contrat (Open Question 6).

### 5.c Découpage suggéré (discrétion du plan)

| Plan | Vague | Fichiers | Exigences |
|---|---|---|---|
| 30-01 — le détecteur lit | 1 | `SessionTreatmentTracker.cs`, `LectureSessions.cs` (enum, `SessionMasquee`, `CauseTraitement`, `ContexteLecture`), `HorizonsSessions.cs`, tests L01-L20 | LUE-01, LUE-02 (règle), LUE-04 (détecteur) |
| 30-02 — ce que voit l'OS, ce que l'app a sélectionné | 1 (fichiers disjoints) | `PremierPlanWin32.cs` (nouveau, avec `IPremierPlan`, `EtatPremierPlan`), `LecteurAppBureau.cs`, tests P01-P07, S01-S03 | LUE-02 (détection), LUE-04 (sonde) |
| 30-03 — le moniteur branche, la production câble | 2 | `SessionMonitor.cs`, `App.xaml.cs`, `LectureSessions.cs` (champ `Lue`), gardes, `CompositionRootTests`, M01-M10 | LUE-01..04 |
| 30-04 — le rapport dit pourquoi | 3 (ou fusionné dans 30-03) | `DiagnosticService.cs`, `docs/hooks-contract.md` §3, R01-R07 | LUE-03 |

Le découplage « le détecteur ne reçoit que `ClaudeDepuis` » permet 30-01 ∥ 30-02 sans type partagé.

---

## Standard Stack

Aucune dépendance ajoutée.

| Brique | Version | Usage ici |
|---|---|---|
| .NET 8 (`net8.0-windows`) | runtime 8.0.25 installé | `System.Diagnostics.Process`, `System.Runtime.InteropServices.DllImport` |
| `user32.dll` (système) | Windows 11 26200 | `GetForegroundWindow`, `GetWindowThreadProcessId` |
| xUnit 2.9.2 + Xunit.StaFact 1.1.11 | existant | tous les tests ; aucun `[WpfFact]` nécessaire (aucune UI touchée) |

**Alternatives écartées :** UI Automation (hors périmètre, gardes) ; titre de fenêtre (« Claude », sans nom de
session, et un navigateur passerait) ; `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` (événementiel, plus précis sur
« depuis », mais exige une pompe de messages et un rappel natif vivant — le sondage à 2 s suffit au verrou et se
teste) ; `Process.MainModule` ou `QueryFullProcessImageName` pour distinguer l'app de la CLI (Open Question 3 : pas
exigé, le verrou fixe le nom).

## Architecture Patterns

### Fichiers (tous à plat dans `Services/` : `NormalisationUniqueTests` et `HorizonsSessionsTests` ne balaient que ce dossier)

```
src/Chronos/Services/
├── SessionTreatmentTracker.cs   # + ContexteLecture en entrée, décision NET-03/LUE, CauseDe
├── LectureSessions.cs           # + MotifMasquage (3 valeurs en fin), CauseTraitement, SessionMasquee.Cause, LectureSessions.Lue
├── PremierPlanWin32.cs          # NOUVEAU : IPremierPlan, EtatPremierPlan, StatutPremierPlan, PremierPlanWin32
├── LecteurAppBureau.cs          # + focus des fichiers sans identifiant, LectureAppBureau.Selection
├── HorizonsSessions.cs          # + GraceLecture (2,5 s)
├── SessionMonitor.cs            # + premierPlan (dernier paramètre nommé), ContexteLecture, cause au filtre
└── DiagnosticService.cs         # LUE-03 (après 29-05)
src/Chronos/App.xaml.cs          # AddSingleton<IPremierPlan>(_ => new PremierPlanWin32()) ; premierPlan: au moniteur
```

### Pattern 1 — La décision du détecteur

```csharp
// Source : SessionTreatmentTracker.cs:73-112 (existant) + Q1.c. Remplace les lignes 104-108.
if (estAttente && _attenteDepuis.TryGetValue(id, out var cur))
{
    var cause = Lue(id, cur, lecture, now);                  // null : non lue, ou règle inactive (LUE-04)
    var traitee = traitees.TryGetValue(id, out var tts);
    if (cause is not null)
    {
        if (!traitee || tts < cur) _store.Set(id, cur);      // cas 4 et 5 : une écriture par épisode
        _causes[id] = (cur, cause);                          // cas 6 compris : la cause, sans écriture
    }
    else if (traitee && cur > tts)                           // cas 3 : NET-03 inchangé
    {
        _store.Remove(id);
        _causes.Remove(id);
    }
}

private static CauseTraitement? Lue(string id, long cur, ContexteLecture? l, System.DateTimeOffset now)
{
    if (l is null || !l.DernierFocus.TryGetValue(id, out var f) || f is not { } focus) return null;   // LUE-04
    if (UsageNormalization.InstantDepuisEpochMillisecondes(cur) is not { } attente) return null;
    if (attente < focus) return new(MotifMasquage.LueParFocus, attente, Focus: focus);             // LUE-01
    if (l.ClaudeAuPremierPlanDepuis is { } depuis
        && string.Equals(l.Selectionnee, id, System.StringComparison.OrdinalIgnoreCase)
        && now - (attente > depuis ? attente : depuis) >= HorizonsSessions.GraceLecture)           // LUE-02
        return new(MotifMasquage.LueAuPremierPlan, attente, focus, depuis, now);
    return null;
}
```

`DernierFocus` est construit `OrdinalIgnoreCase` par le moniteur. `UsageNormalization.InstantDepuisEpochMillisecondes`
et non `FromUnixTimeMilliseconds` (garde `NormalisationUniqueTests` : le détecteur n'est pas exempté).

### Pattern 2 — La sonde Win32, mince

```csharp
// Source : git show ff783d4^:src/Chronos/Services/WindowsForegroundWatch.cs (GetForegroundWindow repris) + mesures Q2.a
[DllImport("user32.dll")] private static extern nint GetForegroundWindow();
[DllImport("user32.dll", SetLastError = true)] private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

internal static (StatutPremierPlan Statut, string? Processus, string? Raison) SonderWin32()
{
    try
    {
        var fenetre = GetForegroundWindow();
        if (fenetre == 0) return (StatutPremierPlan.AucuneFenetre, null, null);
        if (GetWindowThreadProcessId(fenetre, out var pid) == 0 || pid == 0)
            return (StatutPremierPlan.Indisponible, null, "GetWindowThreadProcessId a échoué");
        using var p = System.Diagnostics.Process.GetProcessById((int)pid);
        var nom = p.ProcessName;                              // sans extension : « claude »
        return (string.Equals(nom, "claude", System.StringComparison.OrdinalIgnoreCase)
                    ? StatutPremierPlan.Claude : StatutPremierPlan.AutreProcessus, nom, null);
    }
    catch (System.Exception e) { return (StatutPremierPlan.Indisponible, null, e.GetType().Name); }
}
```

### Pattern 3 — Le moniteur construit le contexte depuis SA lecture

```csharp
// Source : SessionMonitor.cs:158-179 (existant). Insérer avant l'appel au détecteur.
var premierPlan = EtatPremierPlan.NonBranche;
if (_premierPlan is not null)
{
    try { premierPlan = _premierPlan.Lire(now); }
    catch { premierPlan = new EtatPremierPlan(StatutPremierPlan.Indisponible, null, null, "exception"); }
}
ContexteLecture? contexte = appBureau is { DossierTrouve: true }
    ? new ContexteLecture(
        appBureau.ParSession.ToDictionary(kv => kv.Key, kv => kv.Value.DernierFocus, System.StringComparer.OrdinalIgnoreCase),
        appBureau.Selection?.CliSessionId,
        premierPlan.ClaudeDepuis)
    : null;
try { _tracker?.Observe(arbitrage.Vainqueurs, now, contexte); } catch { }
```

### Anti-patterns à éviter

- **Deux blocs successifs NET-03 puis LUE** (ou l'inverse) : double écriture ou purge de ce qui vient d'être écrit.
- **Comparer l'`UpdatedAt` du vainqueur et écrire l'épisode** (ou l'inverse) : churn `Set`/`Remove`.
- **Calculer la sélection sur `ParSession`** : faux « lu » quand l'utilisateur est sur une session neuve.
- **Mettre la règle « lue » dans le filtre du moniteur ou dans le ViewModel** : le verrou la place dans le
  détecteur (il possède l'ajout et la purge) ; un masquage calculé ailleurs ne serait ni réversible par NET-03 ni
  daté par épisode.
- **Un second `Lire` du lecteur ou un appel à `IPremierPlan` depuis le rapport** : OBS-01.
- **Mentionner `HorizonsSessions.Silence` hors de `SessionMonitor.cs`** (XML-doc comprise) : garde
  `La_regle_de_silence_vit_en_un_seul_point`.
- **Faire passer le geste « Marquer traitée » par le détecteur** pour nommer la cause : change le câblage de
  `SessionsViewModel`/`SessionsController` pour un gain de libellé ; le critère 2 exige le geste v1.6 inchangé.

## Don't Hand-Roll

| Problème | Ne pas construire | Utiliser | Pourquoi |
|---|---|---|---|
| Épisode d'attente et sa datation | un second horodatage dans la règle « lue » | `_attenteDepuis` + `InstantDuSignal` du détecteur | NET-03 compare cette valeur ; deux horloges divergent |
| Persistance du « lu » | un nouveau magasin `read.json` | `TreatedStore` (verrou) | réversibilité NET-03, rétention 24 h, écriture atomique déjà prouvées |
| Nom du processus | `OpenProcess` + `QueryFullProcessImageName` à la main | `Process.GetProcessById(pid).ProcessName` | 0,021 ms mesuré, gestion d'erreurs du framework |
| Epoch → instant | `DateTimeOffset.FromUnixTimeMilliseconds` | `UsageNormalization.InstantDepuisEpochMillisecondes` | garde de normalisation unique |
| Fixtures dérivées | re-sérialisation JSON | `RacineAppBureau.Deriver` (remplacement textuel asserté) | précédent DEL-06 |
| Seuil de grâce | littéral dans le détecteur ou le moniteur | `HorizonsSessions.GraceLecture` | un seul siège des seuils, gardé |

## Common Pitfalls

### Piège 1 — L'accueil ou le Chat de l'app au premier plan
**Ce qui se passe :** l'utilisateur quitte la vue d'une session de code pour l'accueil ou une conversation Chat ; le
processus au premier plan reste `claude`, `lastFocusedAt` de la dernière session de code reste le max. Si cette
session finit un tour, LUE-02 la déclare lue après 2,5 s alors qu'il ne la voit pas.
**Pourquoi :** le critère 2 de la phase 27 posait la question (« y compris quand il est sur l'accueil de l'app ou dans
une conversation Chat ») ; le relevé ne l'a vérifiée que pour une session de code. Le titre de fenêtre vaut
« Claude » ; sans UIA, rien ne distingue les vues.
**Parade :** écrire la limite (plan + rapport de phase), la constater en phase 31 ; ne pas inventer de parade.
**Signe :** une session disparaît « sélectionnée au premier plan » alors que l'utilisateur était dans Chat.

### Piège 2 — Écrire l'épisode, comparer autre chose (Fait n° 2)
**Signe :** la sentinelle de date d'écriture change au deuxième cycle (L04).

### Piège 3 — Le magasin lu en tête de cycle (Fait n° 3)
**Signe :** L05 compte deux écritures ou trouve le magasin vide.

### Piège 4 — La sélection sans les fichiers sans identifiant (Fait n° 1)
**Signe :** M04 rouge.

### Piège 5 — Le détecteur ne connaît pas la cause d'un geste ni d'un passé antérieur au démarrage
**Parade :** résidu `Traitee` au libellé honnête ; ne pas prétendre « répondue » ni « lue » sans l'avoir constaté.

### Piège 6 — Un commentaire qui cite le chemin du paquet
**Ce qui se passe :** `// C:\Program Files\WindowsApps\Claude_2.9939…\claude.exe` dans `PremierPlanWin32.cs` fait
rougir la garde n° 2 d'APP-05 (29-05), qui balaie les commentaires. **Parade :** ne jamais écrire `Claude_` ni
`claude-code-sessions` hors `RacinesEtat.cs`.

### Piège 7 — `HorizonsSessions.Silence` cité ailleurs que dans le moniteur
Une `<see cref="HorizonsSessions.Silence"/>` dans le détecteur (pour expliquer la déduite) fait rougir
`La_regle_de_silence_vit_en_un_seul_point`. Écrire « le seuil de silence » en toutes lettres.

### Piège 8 — Le corps d'`EstAttente` lu par une garde documentaire
`ContratHooksDocumenteTests` prend la PREMIÈRE occurrence de `EstAttente(SessionActivity` jusqu'au premier `;`.
Ne rien écrire de tel au-dessus de la définition (`SessionTreatmentTracker.cs:54`), ne pas en changer le corps.

### Piège 9 — Le paramètre nul qui rend l'oubli silencieux
`premierPlan` nul par défaut ⇒ la production resterait sans LUE-02, sans erreur. Garde de source G01 obligatoire.

### Piège 10 — Heures locales dans les tests du rapport
Les attendus « focus à 16:00:39 » dépendent du fuseau de la machine : les calculer par `ToLocalTime()` dans le test.

### Piège 11 — La latence d'écriture de `lastFocusedAt`
Un seul point du relevé : focus 20:30:44.976, `mtime` du fichier 20:31:09 (24 s plus tard) — ambigu (le fichier a pu
être réécrit ensuite pour `lastActivityAt` 20:31:05). Si l'app écrit le focus en différé, LUE-01 tarde d'autant après
un alt-tab, et la sélection vue par Chronos retarde sur un clic dans la barre latérale : pendant ce retard, LUE-02
peut désigner la session PRÉCÉDENTE. Fenêtre étroite ; à mesurer en phase 31 (focus relevé vs date d'écriture).

### Piège 12 — La grâce se voit
Le tour qui finit sous les yeux s'affiche « En attente » 2,5 à ~5,5 s avant de disparaître. Voulu (« le regard a le
temps de se poser ») : à dire à l'utilisateur au constat de phase 31, pas à « corriger ».

### Piège 13 — Tests existants exposés à LUE-01
Seuls les moniteurs construits avec un détecteur ET un lecteur sont exposés : `MoniteurAppBureauTests` (C10 et
`Une_question_marquee_traitee_…`, fixture `blocked` dont le focus 2026-09-24T08:54:06.504Z précède l'attente ⇒ non
lue, verts) et le miroir DI (jamais `Inspecter`). `DiagnosticServiceTests` (29-05) construit des moniteurs avec
lecteur mais SANS détecteur. Relancer la suite complète après 30-03 pour le confirmer.

## Code Examples

### Le relevé de 16 h 08, au moniteur (esquisse)

```csharp
// Instants : Q5.a. Fixtures dérivées textuellement ; transcripts substitués ; horloges fixes.
var maintenant = new DateTimeOffset(2026, 9, 25, 14, 8, 0, TimeSpan.Zero);
var racine = RacineAppBureau.NouvelleRacine();
RacineAppBureau.Ecrire(racine, "local_jarvis.json",
    RacineAppBureau.Deriver("fin-de-tour-review-ready.json", ("1790361032950", "1790344839000")), maintenant.AddMinutes(-7));
RacineAppBureau.Ecrire(racine, "local_adv.json",
    RacineAppBureau.Deriver("sans-postTurnSummary.json",
        ("dae57d29-d277-4e4c-8cad-b4d2b97701e5", "88677186-8690-44dc-ac77-7a319a0aa5cb"),
        ("1790361025541", "1790344859000")), maintenant.AddMinutes(-7));
RacineAppBureau.Ecrire(racine, "local_trois.json",
    RacineAppBureau.Deriver("fin-de-tour-completed.json", ("1790321046116", "1790344800000")), maintenant.AddMinutes(-3));

var transcripts = new SourceModifiable(
    new SessionSnapshot("c17a1b03-3c8d-4c05-a747-dd77bc702e1b", "JARVIS", SessionActivity.WaitingTurn, null, maintenant.AddSeconds(-535)),  // 13:59:05Z
    new SessionSnapshot("88677186-8690-44dc-ac77-7a319a0aa5cb", "ADVANCED SHEET", SessionActivity.WaitingTurn, null, maintenant.AddSeconds(-710)), // 13:56:10Z
    new SessionSnapshot("7bc776ff-8019-4f2e-a942-83d2ac6490e6", "Projet-C", SessionActivity.WaitingTurn, null, maintenant.AddMinutes(-3)));      // 14:05Z

var treated = new TreatedStore(Path.Combine(Dossier(), "treated.json"), new FakeClock(maintenant));
var moniteur = new SessionMonitor(Dossier(), transcripts, Archive(), treated, new SessionTreatmentTracker(treated),
                                  appBureau: new LecteurAppBureau(new[] { racine }));

var lecture = moniteur.Inspecter(maintenant);

Assert.Equal("7bc776ff-8019-4f2e-a942-83d2ac6490e6", Assert.Single(lecture.Visibles).SessionId);
Assert.All(lecture.Masquees, m => Assert.Equal(MotifMasquage.LueParFocus, m.Motif));
Assert.Equal(2, lecture.Masquees.Count);
```

## Ce qui change par rapport à v1.6

| Avant | Après |
|---|---|
| Une session quitte le widget par un geste ou par NET-01 | … ou parce qu'elle a été LUE (focus postérieur à l'attente, ou session sélectionnée avec `claude` au premier plan depuis 2,5 s) |
| « masquée par treated.json (hystérésis OU geste) » | « lue : focus à … > attente à … », « sélectionnée au premier plan depuis N s », « répondue », ou le résidu honnête |
| Le §3 de `docs/hooks-contract.md` dit « Une session quitte le widget pour deux raisons seulement » (ligne 211) | Faux après cette phase : à réécrire (trois raisons), sinon le document ment (Open Question 6) |
| Aucune détection de premier plan depuis la phase 21 | `PremierPlanWin32` : processus, pas titre ; trois états ; jamais d'UIA |

## Open Questions

1. **Accueil / Chat au premier plan** (Piège 1). Ce qu'on sait : le relevé n'a vérifié la sélection que sur une
   session de code. Recommandation : limite écrite dans le plan et dans `docs/desktop-app-sessions.md` (phase 31),
   constat en phase 31 ; aucune parade inventée.
2. **Une session répondue est masquée pendant qu'elle travaille.** Le filtre `treatedMap.ContainsKey`
   (`SessionMonitor.cs:206`) ne regarde pas l'état : après NET-01, une session qui travaille n'affiche PAS
   « Réflexion » (test existant `NET01_le_monitor_masque_apres_reponse`). C'est la v1.6, et LUE ne l'aggrave pas
   (lue puis répondue = masquée comme répondue). Mais la ligne 1 du tableau de l'utilisateur (« en train de
   réfléchir ⇒ Réflexion ») et le critère 4 de la phase 31 risquent d'échouer au constat sur une session qu'il vient
   de relancer. Hors LUE-01..04 : **décision utilisateur à remonter**, ne pas changer en phase 30 sans elle.
3. **Une console dont le processus client s'appelle `claude`.** La CLI Claude Code s'appelle aussi `claude.exe` ;
   lancée dans une console classique (hors Windows Terminal), sa fenêtre pourrait être attribuée au processus
   client par `GetWindowThreadProcessId` (LOW : non vérifié, la recherche web n'a rien confirmé). L'utilisateur
   n'utilise que l'app bureau (REQUIREMENTS, hors périmètre). Recommandation : limite écrite ; si besoin plus tard,
   exiger en plus une fenêtre principale du processus, ou le chemin de l'image.
4. **HH:MM ou HH:mm:ss.** Recommandation : secondes (discrétion du plan), mots du verrou inchangés.
5. **Latence d'écriture de `lastFocusedAt`** (Piège 11) : à mesurer en phase 31.
6. **Le §3 du contrat des hooks.** « deux raisons seulement » devient faux. Recommandation : le réécrire en phase 30
   (trois raisons, la règle « lue » en un paragraphe) avec une garde croisée sur la chaîne exacte ; le document de
   la source (VAL-02) reste en phase 31.

## Environment Availability

| Dépendance | Requise par | Disponible | Version | Repli |
|---|---|---|---|---|
| .NET SDK | build, tests | ✓ | 10.0.201 | — |
| Runtime .NET 8 | tests `net8.0-windows` | ✓ | 8.0.25 (et 8.0.0, 8.0.21) | — |
| `user32.dll` | `PremierPlanWin32` | ✓ | Windows 11 Pro 10.0.26200 | — |
| App bureau Claude (processus `claude`, PID 88220) | vérification du nom | ✓ | `Claude_2.9939.2.0` | fixtures + sonde injectée |
| Six fixtures réelles | tests moniteur/lecteur | ✓ | `tests/Chronos.Tests/TestData/DesktopAppSessions/` | — |
| Overlay vivant | — | à ne pas lancer | 3.1.0 | constat en phase 31 |

Aucune dépendance manquante. Le comportement Win32 réel ne se constate qu'avec une vraie fenêtre au premier plan :
test de fumée seulement, constat humain en phase 31.

## Validation Architecture

### Test Framework

| Propriété | Valeur |
|---|---|
| Framework | xUnit 2.9.2 + Xunit.StaFact 1.1.11, Microsoft.NET.Test.Sdk 17.11.1 |
| Config | `tests/Chronos.Tests/Chronos.Tests.csproj` (attributs `CheminSourcesChronos`, `CheminDocsChronos`) ; `Fakes/FakeClock.cs` ; helper `RacineAppBureau` |
| Commande rapide | voir ci-dessous (`--no-build` après une compilation) |
| Suite complète | `dotnet test Chronos.sln --nologo` — 1039 verts à `3c5f395`, **≈ 1060 attendus en fin de phase 29** (à mesurer en entrée de phase : c'est la baseline) ; ~8 s de tests |

```bash
dotnet test Chronos.sln --nologo --no-build --filter "FullyQualifiedName~TreatedSessionsTests|FullyQualifiedName~LectureSessionsTests|FullyQualifiedName~PremierPlanWin32Tests|FullyQualifiedName~LecteurAppBureauTests|FullyQualifiedName~MoniteurAppBureauTests|FullyQualifiedName~InspectionSessionsTests|FullyQualifiedName~DiagnosticServiceTests|FullyQualifiedName~GardesPerimetreTests|FullyQualifiedName~CompositionRootTests|FullyQualifiedName~HorizonsSessionsTests|FullyQualifiedName~NormalisationUniqueTests|FullyQualifiedName~ServicesLayerPurityTests|FullyQualifiedName~ContratHooksDocumenteTests|FullyQualifiedName~LectureSeuleAppBureauTests"
```

### Phase Requirements → Test Map

| Req | Comportement | Type | Commande (`--filter`) | Existe ? |
|---|---|---|---|---|
| LUE-01 | attente < focus ⇒ traitée, cause `LueParFocus` ; `=` et `>` ⇒ visible (L01-L03) | unit pur | `~LectureSessionsTests` (ou `~TreatedSessionsTests`) | ❌ Wave 0 |
| LUE-01 | une écriture par épisode, sentinelle intacte ; nouvel épisode lu sans purge (L04, L05) | unit + fichier temp | idem | ❌ |
| LUE-01 | NET-03 inchangé ; réapparition sur nouveau tour (L06, L08) | unit pur | idem + `~TreatedSessionsTests` (existants verts) | ❌ / ✅ |
| LUE-01 | déduite lue (D-30-07), permission lue (D-30-08) (L07, L09) | unit pur | idem | ❌ |
| LUE-01 | relevé de 16 h 08 (critère 1), geste A (M01, M02) | intégration moniteur + lecteur réel | `~MoniteurAppBureauTests` ou nouvelle classe | ❌ |
| LUE-01 | 478 min préservé, avec et sans lecteur (M08) | intégration | `~TreatedSessionsTests` | ✅ + ❌ variante |
| LUE-01 | geste et archive comme en v1.6 (M09) | intégration | `~InspectionSessionsTests`, `~GesteTraiteTests` | ✅ + ❌ |
| LUE-02 | geste B lu au premier plan ; grâce à 2 499/2 500 ms ; `explorer` ⇒ visible ; non sélectionnée ⇒ visible (L10-L14, M03) | unit + intégration | `~LectureSessionsTests`, `~MoniteurAppBureauTests` | ❌ |
| LUE-02 | sélection = max focus tous fichiers, sans identifiant ⇒ personne (S01, S02, M04) | intégration lecteur | `~LecteurAppBureauTests` | ❌ |
| LUE-02 | sonde : cache 1 s, depuis, casse, égalité stricte, erreurs, horloge qui recule (P01-P06) | unit (sonde injectée) | `~PremierPlanWin32Tests` | ❌ |
| LUE-02 | fumée Win32 réelle, ne lève pas (P07) | intégration OS | `~PremierPlanWin32Tests` | ❌ |
| LUE-02 | noms hors liste interdite ; aucun `Claude_` ; pureté | réflexion / texte | `~GardesPerimetreTests`, `~ServicesLayerPurityTests`, `~LectureSeuleAppBureauTests` | ✅ (inchangés) |
| LUE-02 | câblage `premierPlan:` + miroir DI (G01, G02) | texte + DI | `~GardesPerimetreTests`, `~CompositionRootTests` | ❌ / à étendre |
| LUE-03 | libellés lue/premier plan/répondue/résidu ; section « Règle « lue » » ; aucun motif sans libellé (R01-R07) | intégration rapport | `~DiagnosticServiceTests` | ❌ |
| LUE-03 | cause survit au départ du premier plan ; redémarrage (L17-L19) ; `Repondue` (L16) | unit pur | `~LectureSessionsTests` | ❌ |
| LUE-03 | OBS-01 : rapport sans `IPremierPlan` ni `.Lire(` (G03) | texte | `~GardesPerimetreTests` | ❌ |
| LUE-04 | D1-D8 : sans lecteur, dossier absent, `Lire` qui lève, session inconnue, `sans-cliSessionId.json`, focus absent, illisible, Win32 en échec (M05-M07, L15, L20) | intégration + unit | `~MoniteurAppBureauTests`, `~LectureSessionsTests` | ❌ |
| (seuils) | `GraceLecture` = 2,5 s < silence (G04) | unit | `~HorizonsSessionsTests` | à étendre |

### Sampling Rate

- **Par tâche :** la commande rapide ci-dessus.
- **Par vague :** `dotnet test Chronos.sln --nologo` (deux exécutions consécutives en fin de vague, comme en 29).
- **Porte de phase :** suite complète verte (0 échec), et quatre mutations jouées puis révoquées (sha256 identique) :
  (m1) `tts < cur` retiré (toujours `Set` quand lue) ⇒ L04 rouge ; (m2) LUE avant NET-03 en deux blocs ⇒ L05 rouge ;
  (m3) sélection calculée sur `ParSession` ⇒ M04 rouge ; (m4) grâce en `>` au lieu de `>=` ⇒ L10 rouge ;
  en plus, (m5) `premierPlan:` retiré d'`App.xaml.cs` ⇒ G01 rouge.

### Wave 0 Gaps

- [ ] `tests/Chronos.Tests/LectureSessionsTests.cs` (nom libre) — L01-L20, contexte construit à la main
- [ ] `tests/Chronos.Tests/PremierPlanWin32Tests.cs` — P01-P07, sonde injectée par le constructeur `internal`
- [ ] extension `LecteurAppBureauTests` — S01-S03
- [ ] extension `MoniteurAppBureauTests` (ou classe dédiée) — M01-M10, helper `Moniteur(...)` avec `premierPlan`
- [ ] faux `IPremierPlan` (`Fakes/`) rendant un `EtatPremierPlan` réglable entre deux cycles
- [ ] extension `TreatedSessionsTests` — variante 478 min avec lecteur
- [ ] extension `DiagnosticServiceTests` — R01-R07
- [ ] extension `GardesPerimetreTests` (G01, G03), `CompositionRootTests` (G02), `HorizonsSessionsTests` (G04)
- Aucun framework à installer.

### Vérifications manuelles (non automatisables sans lancer l'overlay — phase 31)

- Une session finie, ouverte dans l'app puis quittée : elle disparaît sans clic (LUE-01), dans un délai à noter
  (Piège 11).
- Un tour qui finit sous les yeux, fenêtre Claude au premier plan : « En attente » quelques secondes, puis rien
  (LUE-02, Piège 12) ; la même chose avec l'Explorateur au premier plan : reste « En attente ».
- Accueil ou Chat au premier plan pendant la fin d'un tour de la dernière session de code (Open Question 1) : noter
  le résultat, écarts compris.
- Le rapport « Diagnostic… » : motif et deux instants pour chaque masquée ; ligne « Premier plan ».

## Sources

### Primary (HIGH)

- Dépôt à `3c5f395` puis `415cd7d`, lu le 2026-09-25 : `SessionTreatmentTracker.cs`, `TreatedStore.cs`,
  `SessionMonitor.cs`, `LectureSessions.cs`, `LecteurAppBureau.cs`, `ArbitrageSessions.cs`, `HorizonsSessions.cs`,
  `SessionSnapshot.cs`, `IClock.cs`, `UsageNormalization.cs`, `App.xaml.cs` (`:244-300`, `:415-436`),
  `SessionsViewModel.cs` et `DiagnosticService.cs` (par contenu), `Chronos.csproj` ; tests `TreatedSessionsTests`,
  `GardesPerimetreTests`, `ServicesLayerPurityTests`, `CompositionRootTests`, `NormalisationUniqueTests`,
  `HorizonsSessionsTests`, `ContratHooksDocumenteTests`, `InspectionSessionsTests`, `MoniteurAppBureauTests`,
  `RacineAppBureau` ; les six fixtures ; plans et SUMMARY 29-01 à 29-05 ; `docs/hooks-contract.md:170-229`.
- `git show ff783d4^:src/Chronos/Services/WindowsForegroundWatch.cs` et `IForegroundWatch.cs` (implémentation
  retirée en phase 21).
- Mesures (bloc-notes, lecture seule) : `Get-Process -Name claude` ; sonde .NET 8 `sonde-premier-plan-30` dans l'arbre
  et hors de l'arbre (WMI, parent `WmiPrvSE`).
- `.planning/phases/27-le-relev-avant-la-r-gle/27-RELEVE.md` (gestes A et B, 20:33:27, PID 88220).
- [Microsoft Learn — GetWindowThreadProcessId](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid) — signature, valeur de retour 0 en échec.

### Secondary (MEDIUM)

- Threading des deux appelants du moniteur (thread UI) : déduit du code, non instrumenté.
- Comportement de l'app quand l'accueil ou Chat est au premier plan, et latence d'écriture de `lastFocusedAt` :
  non relevés.

### Tertiary (LOW)

- Attribution d'une fenêtre de console classique au processus client par `GetWindowThreadProcessId` : souvenir non
  confirmé par la recherche web (Open Question 3). Aucune recommandation n'en dépend.

## Metadata

**Confidence breakdown :**
- Détecteur, décision NET-03/LUE, écritures : HIGH — code lu ligne à ligne, cas dérivés du code existant.
- Détection Win32 : HIGH sur le nom et le coût (mesurés dedans et dehors) ; MEDIUM sur la sémantique « regard »
  (accueil/Chat).
- Sélection : HIGH sur le défaut du lecteur actuel (lignes citées, fixture qui porte le champ) ; LOW sur le cas
  « fichier sélectionné > 24 h ».
- Diagnostic : HIGH sur l'insertion (même `LectureSessions`), format à la discrétion du plan.
- Gardes : HIGH — chaque garde lue, chaque piège nommé avec sa garde.

**Research date :** 2026-09-25
**Valid until :** 2026-10-09 (l'app bureau change souvent de version ; refaire Q2.a si elle n'est plus 2.9939.2.0)
