# Phase 26 : « Traité » veut enfin dire quelque chose - Context

**Gathered:** 2026-09-12
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)
**Dernière phase du milestone v1.6.**

<domain>
## Phase Boundary

« Traité » cesse d'être un accident de l'expiration. Il n'est déduit que d'une transition **observée sur la
même source**, il **survit à un redémarrage**, l'utilisateur dispose d'un **geste explicite**, et
« Archiver » fait enfin **ce qu'il annonce**.

Exigences : **TRT-01** (transition observée sur la même source), **TRT-02** (persistance au redémarrage),
**TRT-03** (geste explicite), **TRT-04** (contrat d'archivage unique).

**Hors périmètre :** tout le reste du milestone est livré (phases 21-25). Cette phase ne retouche ni
l'arbitrage (24), ni le contrat d'événements (25), ni le magasin d'états (23).

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
Discussion désactivée (`workflow.skip_discuss=true`). `UI hint: yes` — TRT-03 touche l'écran.

### ⚠ LE DÉFAUT LE PLUS URGENT EST NEUF : la phase 25 l'a créé, et il faut le refermer ici

`SessionTreatmentTracker.IsWaiting` (l. 38) reconnaît **`WaitingTurn` et `WaitingAttention`**, pas
`WaitingDeduced`. Or la phase 25 vient de livrer `WaitingDeduced` pour le silence après travail.
Conséquence mécanique de NET-01 (l. 56-60) :

```
une session en attente  →  sa source se tait  →  WaitingDeduced
        wasWaiting = true,  isWaiting = false   →  MARQUÉE TRAITÉE
```

**C'est exactement le masquage de 6 h que ce milestone existe pour supprimer, ressuscité par la porte
d'à côté.** Le rapport de vérification de la phase 25 l'a signalé et l'a **délibérément laissé** à cette
phase (`docs/hooks-contract.md` §3). **C'est le premier correctif à livrer, et il conditionne TRT-01.**

### TRT-01 — les deux confusions à éliminer, mesurées

1. **« Ma source a expiré » ≠ « l'utilisateur a répondu ».** Preuve in vivo du 2026-09-12 :
   `treatedWaitingTs = 1789210240523`, `updated_at` du hook `= 1789181509266`, écart **478 min** contre un
   `DropAfter` de **480 min**. L'épisode d'attente a été enregistré ~69 s **avant** le franchissement du
   seuil ; NET-01 a tiré à l'instant exact où le fichier de hook est tombé et où le transcript (`Working`)
   a repris la main. La session `e465420e` (PROJET ADVANCED SHEET), **réellement en attente de permission**,
   a été masquée **6 heures**.
2. **Une bascule de source n'est pas une transition.** Le passage « hook `WaitingAttention` » →
   « transcript `Working` » n'est pas l'utilisateur qui répond : c'est une source qui se tait pendant qu'une
   autre parle. **La transition doit être observée sur la MÊME source.**

**Obstacle structurel à traiter :** `SessionSnapshot` (`src/Chronos/Services/SessionSnapshot.cs`) porte
**5 champs** — `SessionId`, `Project`, `Activity`, `Reason`, `UpdatedAt` — et **aucune source**.
`SessionTreatmentTracker.Observe(raw, now)` ne peut donc pas savoir *qui* a parlé. Or la phase 24 a livré
`ArbitrageSessions` qui **connaît** la source retenue (`SourceRetenue`) et trace les désaccords.
**Faire parvenir la source au tracker est la condition de TRT-01** — par quel chemin exactement est à la
discrétion de Claude, mais **sans casser l'arbitrage ni l'unicité des filtres**.

### TRT-02 — pourquoi le traité ne survit à rien, mécaniquement

`SessionTreatmentTracker` porte **deux dictionnaires en mémoire** : `_lastActivity` et `_waitingSince`.
Au redémarrage de l'overlay, pour une session **encore en attente** :

```
tracker neuf → wasWaiting = false → _waitingSince[id] = nowMs      (daté MAINTENANT)
NET-03       → cur(now) > tts(mémorisé)  →  _store.Remove(id)
```

**Toutes les sessions traitées ressortent à chaque redémarrage.** Le magasin `treated.json` survit ; c'est
**l'état du détecteur** qui ne survit pas, et son amnésie purge le magasin. Corriger `TreatedStore` ne
suffirait donc pas — c'est l'épisode d'attente qu'il faut faire survivre, ou NET-03 qu'il faut rendre
insensible à l'amnésie.

**Piège adjacent, mesuré (S2)** : un **seul** cycle où la session paraît non-attente suffit à la marquer
traitée pour 6 h.

### TRT-03 — le geste explicite
Le focus ne peut pas fournir l'acquittement en terminal (NET-02 a été retirée en phase 21 : elle exigeait
une origine « app bureau » qu'une session Claude Code n'a jamais). Il faut donc un **geste direct**.
Précédent en place : `SessionStyles.xaml` porte **8 occurrences de `MenuItem`** — un menu contextuel
« Archiver » par style — liées à `SessionItemVm.ArchiveCommand`
(`src/Chronos/ViewModels/SessionsViewModel.cs`, l. 32-41). Le geste « traité » doit suivre **le même
chemin**, sur les **8 styles** et les **9 thèmes**, **sans casser la compacité** du widget.

**⚠ RAPPEL DE SÉCURITÉ UI, valable ici :** ne jamais lier une commande dont le clic serait destructif à un
geste ordinaire. Le verrou établi dans ce projet : `LoginClaudeCommand` **bascule** et un clic effacerait
le coffre de jetons — `ReconnecterCommand` est la commande dédiée. Appliquer la même prudence au nouveau
geste : il doit être **réversible ou explicitement annoncé**, jamais ambigu.

### TRT-04 — « Archiver » ment aujourd'hui
`ArchiveStore.cs` applique un **TTL de 6 h** (l. 17, 35, 55) alors que son contrat annoncé (NET-04) et le
libellé du menu — « Archiver (retirer de l'overlay) », « elle ne réapparaît plus » — promettent le
**permanent**. Le clic droit est en réalité une **mise en sourdine de 6 heures**.

`TreatedStore` applique le **même TTL de 6 h**, mais là c'est **cohérent** : sa XML-doc annonce
explicitement « RÉVERSIBLE ». Les deux magasins sont calqués l'un sur l'autre et se distinguent **par leur
sémantique**, pas par leur code. **Un contrat UNIQUE est exigé : soit l'entrée ne revient jamais, soit la
durée est annoncée.** Trancher, et faire que le libellé du menu dise la vérité.

**Trace du contournement :** `%APPDATA%\Chronos\archived.json` (**84 octets**) contient encore deux entrées
`desktop:foreground:*` de juillet que `Load()` écarte déjà — l'utilisateur avait dû archiver ces fantômes
**à la main**. Ne pas y toucher : la purge est livrée (phase 21) et s'exécutera à son prochain lancement.

### Doctrine du milestone — elle mord une dernière fois
**Ne jamais présenter comme un fait ce qui n'a pas été observé.** Ici : **expirer n'est pas répondre**, et
**changer de source n'est pas une transition**. « Traité » est une affirmation forte sur ce que
l'utilisateur a fait — elle doit reposer sur une observation, ou sur son geste.

### Contraintes projet
- MVVM strict, DI ; aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`).
- **`NormalisationUniqueTests`** balaie `Services/` et `Models/`, **commentaires compris**.
- **Horloge injectable** : `TreatedStore.Load()` (l. 36) et `ArchiveStore` utilisent l'horloge **système**
  sans injection — c'est le piège de test à retardement qui a mordu **trois plans de la phase 22**. Toute
  modification de ces magasins doit **introduire l'horloge injectable**, pas reproduire le défaut.
- **Consigne de rédaction** (phases 24-25) : ne jamais écrire littéralement les chaînes interdites dans un
  commentaire ou une XML-doc — **les gardes lisent le TEXTE**.
- **Compilabilité à chaque commit atomique** ; aucun « échec de build attendu ». `tests/Chronos.Tests` a un
  `ProjectReference` vers `Chronos` : une tâche qui change une signature adapte ses sites d'appel **dans la
  même tâche**.
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement. Aucune dépendance NuGet nouvelle.
- UI et commentaires en **français**.
- **Baseline à l'entrée de phase : 868 tests verts**, suite en ~4-5 s.

### SÉCURITÉ
- **NE PAS supprimer ni modifier** `%APPDATA%\Chronos\sessions\` ni `archived.json` (**84 octets**) ni
  `treated.json`. **Le dossier `sessions\` bouge tout seul** (65 entrées au dernier relevé, contre 66) :
  les hooks v3.0.2 de l'utilisateur sont **vivants** et écrivent pendant nos propres sessions Claude Code.
  **Ce n'est pas une violation — ne pas tenter de le « réparer ».** Tests sur copies temporaires.
- **NE PAS écrire dans le vrai `~/.claude/settings.json`** (lecture seule autorisée) ; **aucune sonde**.
- **NE PAS lancer ni tuer l'overlay** (`Chronos-v3.0.2.exe`, pid 119412, en cours d'utilisation).
- Aucune requête réseau réelle ; aucun test n'écrit dans le vrai `%APPDATA%\Chronos\`.
- **`oauth.dat` = 518 octets — NE PAS vérifier son mtime.**

</decisions>

<code_context>
## Existing Code Insights

### Fichiers concernés
- `src/Chronos/Services/SessionTreatmentTracker.cs` (74 l.) — **le cœur** : `IsWaiting` l. 38, NET-01
  l. 56-60, l'épisode d'attente l. 62-63, NET-03 l. 65-69.
- `src/Chronos/Services/TreatedStore.cs` (108 l.) — TTL 6 h l. 22, horloge **système** l. 36,
  écriture **tmp+move** (le motif que la phase 23 a écarté pour les états de hook).
- `src/Chronos/Services/ArchiveStore.cs` — TTL 6 h l. 17/35/55, `PurgerPrefixe` (phase 21).
- `src/Chronos/Services/SessionSnapshot.cs` — **5 champs, aucune source**.
- `src/Chronos/Services/ArbitrageSessions.cs`, `LectureSessions.cs` — **acquis de la phase 24** :
  `SourceRetenue`, `Desaccords`. C'est là que la source existe déjà.
- `src/Chronos/Services/SessionMonitor.cs` — `Inspecter` applique les filtres, `Read` en est la projection.
- `src/Chronos/ViewModels/SessionsViewModel.cs` — `SessionItemVm` l. 32-41 (`ArchiveCommand`).
- `src/Chronos/Resources/SessionStyles.xaml` — **8 `MenuItem`**, 8 styles.
- `docs/hooks-contract.md` §3 — porte la divergence transmise par la phase 25.
- Tests : `TreatedSessionsTests.cs`, `SessionsTests.cs`, `InspectionSessionsTests.cs`,
  `ArchiveStorePurgeTests.cs`, `SessionStylesBindingTests.cs`, `GardesPerimetreTests.cs`.

### Acquis à ne pas casser
- **Phase 21** : périmètre Claude Code seul ; `Take(12)` après le filtre sous-agents ; `PurgerPrefixe`.
- **Phase 22** : `Read(now) => Inspecter(now).Visibles` **unique** implémentation des filtres ; **zéro**
  `new SessionMonitor` dans `DiagnosticService` ; partage d'instance.
- **Phase 23** : `EcritureEtatSession` (écriture directe, reprise **bornée** — mesurée 0/400 contre 62-209
  pour une reprise unique) ; `BalayageMagasinSessions` ; **un fragment illisible est une ABSENCE**.
- **Phase 24** : arbitrage par **fraîcheur** ; `LectureSessions.Desaccords` ; bloc « Désaccords entre
  sources » ; `MotifMasquage` = **3** ; un désaccord **n'est pas** un masquage.
- **Phase 25** : liste blanche des 33 événements validée **avant** écriture ; purge large qui épargne les
  groupes tiers (index 0 préservé) ; veto sous-agent ; `WaitingDeduced` (rang d'urgence **3**, libellé
  « à toi ? déduit », **jamais écrit dans un fichier d'état**) ; `SilenceDesBattements` ;
  `docs/hooks-contract.md` et sa garde de non-dérive.
- **Phase 20** : `?? new ` reste à **2**, suite en ~4-5 s.

### L'effet de masse mesuré en 25-03, à garder en tête
Sur un corpus **vivant** de 66 états, **20 sessions sur 54** basculeraient ensemble en « à toi ? déduit »
(`WaitingCount` = 42). Sur le magasin **réel** d'aujourd'hui le nombre serait **zéro** — tous les états y
sont au-delà de huit heures. Si le geste explicite de TRT-03 devient le seul moyen de vider un widget
saturé de déductions, **son ergonomie compte** : un geste par session sur vingt sessions n'est pas un geste.

</code_context>

<specifics>
## Specific Ideas

Le critère le plus révélateur est le n°2 du ROADMAP : **rejouer le scénario mesuré** (attente enregistrée
478 min avant un seuil de 480 min, puis bascule de source) et vérifier que la session **reste visible**.
C'est un test de non-régression sur une panne **réellement survenue chez l'utilisateur**, avec ses chiffres
exacts — pas une hypothèse. Il doit être **rouge avant** le correctif.

</specifics>

<deferred>
## Deferred Ideas

- **Republication de l'exe** : rien de tout le milestone v1.6 ne s'exécute chez l'utilisateur tant que l'exe
  n'est pas republié **et** `settings.json` réconcilié. Mesuré : sa configuration ne porte encore que les
  **5 hooks v3.0.2**, sans `PermissionRequest`, sans matcher sur `Notification`.
- **Protocole de vérification in vivo consolidé** (phases 17-20 + 21-25, dont l'**effet de masse** A11) →
  à présenter en fin de milestone, après la republication.
- Exploitation des ~25 autres événements du catalogue → **hors périmètre v1.6**.

</deferred>
