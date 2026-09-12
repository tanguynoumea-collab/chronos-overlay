# Roadmap : Chronos

## Milestones

- ✅ **v1.0 — Overlay de quotas Claude complet** (7 phases, 18 plans, SHIPPED 2026-07-08) — [archive](.planning/milestones/v1.0-ROADMAP.md)
- ✅ **v1.1 — Estimation utile en mode app bureau** (2 phases, 5 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.1-ROADMAP.md)
- ✅ **v1.2 — Usage exact via l'endpoint OAuth** (2 phases, 4 plans, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.2-ROADMAP.md)
- ✅ **v1.3 — Refonte du cadran (3 anneaux, remplissage, compacité)** (1 phase, phase 12, SHIPPED 2026-07-09) — [archive](.planning/milestones/v1.3-ROADMAP.md)
- ✅ **v1.4 — Intégration des sessions de l'app bureau Claude (Chat / Cowork / Code)** (2 phases, phases 13-14, 5 plans, SHIPPED 2026-07-11) — [archive](.planning/milestones/v1.4-ROADMAP.md)
- ✅ **v1.5 — Exactitude permanente** (6 phases, phases 15-20, 28 plans, SHIPPED 2026-09-12) — [archive](.planning/milestones/v1.5-ROADMAP.md)
- 🚧 **v1.6 — Observer au lieu de déduire (widget de sessions)** (6 phases, phases 21-26, en cours)

## Prochain milestone

Après v1.6 : sous-fenêtres opus/sonnet/cowork, survol/tooltip, tray, taille réglable, préavis avant saturation
du quota et notification au reset. Piste d'économie à trancher : si `/api/oauth/usage` sert un jour la famille
`anthropic-ratelimit-unified-*`, les ≈ 288 micro-requêtes/jour de la sonde deviennent supprimables.

---

## Milestone v1.6 : Observer au lieu de déduire

### Overview

Le cadran est réglé ; le widget de sessions ne l'est pas. v1.5 a corrigé sur le cadran exactement le défaut
que le widget porte encore : **présenter comme un fait ce qui n'a pas été observé.** L'investigation du
2026-09-12 (`.planning/debug/widget-sessions-statuts.md`, close, prouvée contre les classes réelles et contre
la documentation officielle des hooks) établit trois causes racines distinctes et deux amplificateurs :

1. **« Réfléchit » est déduit par expiration.** Rien ne confirme jamais que le travail continue, et
   `SessionMonitor.Read` arbitre par **ordre d'insertion** : un fichier de hook de 7 h bat un transcript de 10 s.
2. **« Attend » repose sur une sémantique fausse à la source.** `Stop` ne se déclenche pas sur interruption
   utilisateur ; `Notification` est une alerte d'absence, pas un état ; le `PermissionRequest` dédié n'est pas
   utilisé.
3. **« Traité » ne peut structurellement pas fonctionner en terminal.** Le tracker exige `Origin == Desktop` ;
   la règle confond « l'utilisateur a répondu » avec « ma source a expiré » — preuve arithmétique : 478 min
   contre un seuil de 480 min ont masqué une session réellement en attente pendant 6 h.

**Amplificateurs :** le magasin disque ne décroît jamais (54 fichiers dont 48 de plus de 7 jours, + 12 `.tmp`
orphelins), et l'instrument de mesure lui-même ment — `DiagnosticService` construit son propre `SessionMonitor`
nu, sans les filtres du widget. C'est probablement pourquoi le problème n'a jamais été élucidé.

**Doctrine du milestone**, héritée de v1.5 : un état dont la source a expiré n'est pas « terminé », il est
**inconnu**. Le widget **observe** ses trois états, il ne les déduit plus.

Le milestone se lit en **six gestes**, ordonnés par dépendance technique réelle :

1. **Réduire le périmètre à Claude Code** (Phase 21). Le retrait de la source app-bureau supprime ~690 lignes
   de `src/` et ~530 de tests, fait tomber `WindowsForegroundWatch` / `IForegroundWatch`, simplifie
   `SessionSnapshot` et `SessionMonitor`. Toute autre phase travaillerait sinon sur un terrain qu'on va démolir.
2. **Réparer l'instrument de mesure** (Phase 22). **Choix argumenté, contraire à l'ordre naïf** : OBS-01
   décrirait un état intermédiaire s'il était écrit tôt *comme une copie* du comportement du widget. On le
   recâble donc en **partage d'instance** — le diagnostic interroge le moniteur du widget au lieu d'en
   reconstruire un. Formulé ainsi, il reste vrai à chaque commit ultérieur *par construction*, et les phases
   23 à 26 cessent d'être vérifiées avec un instrument faussé. Placé après la Phase 21 uniquement pour ne pas
   avoir à le recâbler deux fois.
3. **Assainir le magasin** (Phase 23). `SessionEnd` ne couvre ni terminal tué, ni crash, ni redémarrage
   machine : le magasin ne peut que croître. Et une écriture perdue en silence fait **mentir un horodatage** —
   donc c'est un prérequis de l'arbitrage par fraîcheur, pas un simple ménage.
4. **Arbitrer par fraîcheur** (Phase 24). Le cœur mécanique : un signal ne peut en écraser un autre que s'il
   est plus récent, et les désaccords deviennent traçables.
5. **Refonder le contrat d'événements** (Phase 25). `PermissionRequest` au lieu du proxy `Notification`, des
   battements de cœur pour que « réfléchit » soit **observé**, le cas de l'interruption couvert, le contrat
   documenté. EVT-03 n'a de valeur qu'après FUS-01 : sans arbitrage par fraîcheur, un battement de cœur frais
   serait écrasé par un hook ancien.
6. **Donner un sens à « traité »** (Phase 26). Une transition **observée sur la même source**, persistante au
   redémarrage, assortie d'un geste explicite et d'un contrat d'archivage unique. Exige la notion de « même
   source » et de fraîcheur, donc la Phase 24.

**Contraintes portées :** C# / .NET 8 (`net8.0-windows`) / WPF / MVVM (CommunityToolkit.Mvvm) +
Microsoft.Extensions.DependencyInjection + Hosting ; aucune dépendance native ; chemins sous `%USERPROFILE%` /
`%APPDATA%` uniquement, aucun droit admin ; UI et commentaires en français. Les **752 tests xUnit** restent
verts (suite ~4 s), y compris `ServicesLayerPurityTests` (aucun type WPF dans `Services/` ni `Models/`),
`CompositionRootTests`, `NormalisationUniqueTests`, `GardesDoctrineTests` et
`La_sonde_d_en_tetes_est_le_PRIMAIRE_de_la_chaine_exacte`. Toute modification visuelle doit rester cohérente
sur les **8 styles de session** (galerie `--sessions`) et les **9 thèmes**.

### Phases

**Numérotation des phases :**
- Phases entières (21→26) : travail de milestone planifié — continue après la Phase 20 (v1.5)
- Phases décimales (21.1, 21.2) : insertions urgentes (marquées INSERTED)

- [x] **Phase 21 : Périmètre — le widget ne parle que de Claude Code** - La source app-bureau par UI Automation disparaît avec ses entrées fantômes, et la source transcripts cesse de s'aveugler pendant les vagues de sous-agents (completed 2026-09-12)
- [x] **Phase 22 : Un instrument de mesure qui ne ment plus** - Le diagnostic partage le moniteur du widget au lieu d'en reconstruire un nu, et liste les sessions pertinentes
 (completed 2026-09-12)
- [x] **Phase 23 : Un magasin qui ne croît plus et n'oublie plus** - Les états expirés et les `.tmp` orphelins sont balayés, et une écriture de hook ne peut plus être perdue en silence
 (completed 2026-09-12)
- [x] **Phase 24 : L'arbitrage par fraîcheur** - Un signal n'en écrase un autre que s'il est plus récent, jamais par ordre d'insertion, et les désaccords deviennent traçables
 (completed 2026-09-12)
- [ ] **Phase 25 : Le contrat d'événements refondé** - `PermissionRequest` au lieu du proxy `Notification`, « réfléchit » observé par battements de cœur, interruption utilisateur couverte, contrat documenté
- [ ] **Phase 26 : « Traité » veut enfin dire quelque chose** - Déduit d'une transition observée sur la même source, persistant au redémarrage, assorti d'un geste explicite et d'un contrat d'archivage unique

### Phase Details

### Phase 21 : Périmètre — le widget ne parle que de Claude Code
**Goal**: Le widget ne montre plus que des sessions **Claude Code**, réellement vivantes : la source
app-bureau par UI Automation et tout ce qui n'existait que pour elle disparaissent du dépôt, les entrées
fantômes `desktop:foreground:*` s'évaporent y compris celles déjà archivées à la main, et une vague de
sous-agents parallèles ne peut plus faire disparaître la vraie session.
**Depends on**: Rien (première phase du milestone v1.6). Elle est **prérequis de toutes les autres** :
elle supprime ~690 lignes de `src/` et ~530 de tests, fait tomber `WindowsForegroundWatch` / `IForegroundWatch`,
retire `SessionKind` / `SessionOrigin` de `SessionSnapshot` et trois arguments de construction de
`SessionMonitor`. Toute phase antérieure travaillerait sur un terrain démoli juste après.
**Requirements**: SRC-01, SRC-02, SRC-03
**Success Criteria** (what must be TRUE):
  1. **Plus une seule session qui n'est pas une session Claude Code** : après plusieurs heures d'usage avec
     l'app bureau Claude ouverte au premier plan, aucune ligne `desktop:foreground:*` ni `desktop:session:*`
     n'apparaît dans le widget — et l'utilisateur n'a plus rien à archiver à la main (SRC-01).
  2. **Les fantômes déjà archivés disparaissent aussi** : les entrées `desktop:foreground:*` présentes dans
     `archived.json` sont retirées au premier lancement, sans que l'utilisateur ait à toucher un fichier —
     ce qui restait de son archivage manuel de contournement s'efface (SRC-02).
  3. **La vraie session survit aux vagues d'agents** : pendant une exécution où douze `agent-*.jsonl` sont
     chauds simultanément (cas mesuré : 94 % des 868 transcripts sont des sous-agents), la session réelle
     reste visible dans le widget au lieu d'en disparaître (SRC-03).
  4. **Aucun trou visuel dans le widget** : la disparition du libellé de type (« Chat » / « Code » / « Cowork »),
     aujourd'hui bindé dans les 8 styles, ne laisse ni case vide ni décalage — les 8 styles et les 9 thèmes
     restent cohérents.
  5. **Aucune régression de garde** : la suite xUnit reste verte, `ServicesLayerPurityTests` et
     `CompositionRootTests` compris. Critère opérationnel : **0 échec + justification nominative du recul de
     couverture** (les tests supprimés couvrent exclusivement du code supprimé), et NON « ≥ 752 ».
**Plans**: 4 plans, 4 vagues strictement sérielles (débrancher avant supprimer : le risque n°1 d'une phase
de démolition est la compilation, pas la logique — `tests/Chronos.Tests` référence `Chronos`)

Plans:
- [x] 21-01-PLAN.md — SRC-03 : la limite de douze porte sur les sessions retenues, plus sur les fichiers examinés
- [x] 21-02-PLAN.md — SRC-01 (1/2) : débrancher — moniteur, détecteur, DI et diagnostic quittent la source app-bureau
- [x] 21-03-PLAN.md — SRC-01 (2/2) : supprimer — 10 fichiers, la typologie bureau, le libellé de type, deux gardes de non-retour
- [x] 21-04-PLAN.md — SRC-02 : les fantômes archivés retirés du fichier, et le bilan nominatif du recul de couverture
**UI hint**: yes

### Phase 22 : Un instrument de mesure qui ne ment plus
**Goal**: Ce que le diagnostic rapporte est **exactement** ce que le widget affiche — même moniteur, mêmes
filtres, même instant — et il liste les sessions qui comptent au lieu des huit premières par ordre
alphabétique.
**Depends on**: Phase 21 (recâbler le diagnostic avant la démolition obligerait à le recâbler deux fois :
la source bureau et le filtre par focus font partie de ce qu'il devrait refléter).
**Rationale de placement** : l'ordre naïf placerait OBS-01 en dernier, pour ne pas documenter un état
intermédiaire. On fait l'inverse, délibérément, parce que **le diagnostic est l'instrument qui sert à vérifier
les phases 23 à 26**. La contradiction se dissout si OBS-01 est livré comme un **partage d'instance** et non
comme une copie de comportement : le diagnostic interroge le moniteur *du widget*, donc il suit
automatiquement chaque changement ultérieur, sans retouche et sans jamais figer un état intermédiaire.
**Requirements**: OBS-01, OBS-02
**Success Criteria** (what must be TRUE):
  1. **Diagnostic et widget disent la même chose** : ouverts au même instant, la liste des sessions, leurs
     états et leurs âges sont identiques des deux côtés — l'utilisateur peut comparer ligne à ligne sans
     constater le moindre écart (OBS-01).
  2. **Ce qui est masqué est dit, et pourquoi** : une session écartée parce qu'elle est archivée ou traitée
     apparaît dans le diagnostic comme **masquée par tel filtre**, au lieu d'être simplement absente —
     le cas réel `e465420e` (session vivante cachée par `treated.json`) devient lisible en une lecture (OBS-01).
  3. **Des sessions pertinentes, pas les huit premières de l'alphabet** : le diagnostic montre les sessions
     récentes et celles qui attendent, pas huit entrées vieilles de plusieurs semaines choisies par tri de
     nom de fichier (OBS-02).
  4. **Non-retour garanti** : le diagnostic ne peut plus reconstruire son propre moniteur — une modification
     du câblage du widget se reflète dans le diagnostic sans qu'une ligne du diagnostic ne change, et une
     garde le prouve.
**Plans**: 3 plans, 3 vagues strictement sérielles (22-02 consomme les contrats posés en 22-01 ; 22-02 et
22-03 se disputent `DiagnosticService.cs`, la propriété exclusive des fichiers impose donc l'ordre)

Plans:
- [x] 22-01-PLAN.md — OBS-01 (1/2) : le moniteur devient inspectable (ce qu'il masque, par quel filtre) et sa mise en forme devient partageable
- [x] 22-02-PLAN.md — OBS-01 (2/2) : partage d'instance — le rapport interroge le moniteur du conteneur, nomme les masquages, deux gardes de non-retour falsifiables
- [x] 22-03-PLAN.md — OBS-02 : des fichiers d'état choisis par pertinence au lieu de l'ordre alphabétique, et la carte de vérification de la phase

### Phase 23 : Un magasin qui ne croît plus et n'oublie plus
**Goal**: `%APPDATA%\Chronos\sessions` cesse d'être un dépotoir qui ne fait que grandir, et une écriture
d'état de hook cesse de pouvoir disparaître sans que personne ne le sache.
**Depends on**: Phase 21 (terrain stabilisé). **Prérequis de la Phase 24** : l'arbitrage par fraîcheur compare
des horodatages — une écriture perdue en silence fait paraître un état plus vieux qu'il n'est et empoisonne
l'arbitrage à sa racine. Réparer l'écriture avant d'y adosser une règle de décision n'est pas un ménage, c'est
une dépendance.
**Requirements**: CYC-01, CYC-02
**Success Criteria** (what must be TRUE):
  1. **Le magasin se résorbe tout seul** : sur la machine telle qu'elle est aujourd'hui (54 fichiers d'état
     dont 48 de plus de 7 jours, plus 12 `.tmp` orphelins), le premier lancement ramène le dossier aux seules
     sessions plausibles — et il n'y a plus rien à nettoyer à la main, jamais (CYC-01).
  2. **Un terminal tué ne laisse plus de trace éternelle** : fermer brutalement un terminal, planter, ou
     redémarrer la machine (trois cas qu'aucune valeur de `SessionEnd.reason` ne couvre) ne laisse plus une
     session figée à l'écran indéfiniment (CYC-01).
  3. **Une écriture qui échoue se voit** : un état de hook écrit pendant que le widget lit est bien relu
     ensuite ; s'il ne peut pas être écrit, l'échec est constatable au lieu d'être avalé par un `catch` vide
     (CYC-02). Mesure de référence : 200 échecs sur 500 aujourd'hui par `tmp` + `Move`, 0 sur 500 par
     écriture directe.
  4. **Le balayage ne ment pas** : une session dont l'état a été balayé disparaît du widget sans jamais être
     annoncée « terminée » ni « traitée » — expirer, c'est ne plus savoir, et une session vivante depuis
     plusieurs jours survit au nettoyage.
**Plans**: 2 plans, 2 vagues strictement sérielles (23-01 et 23-02 se disputent `App.xaml.cs` et
`GardesPerimetreTests.cs` ; et le balayage date les fichiers par leur `updated_at`, donc il a besoin que
23-01 ait d'abord rendu ces dates fiables)

Plans:
- [x] 23-01-PLAN.md — CYC-02 : l'écriture d'état quitte la couche WPF, devient directe, et son échec se voit
- [x] 23-02-PLAN.md — CYC-01 : balayage des états périmés et des débris temporaires, sur critère double, sans jamais conclure

### Phase 24 : L'arbitrage par fraîcheur
**Goal**: Quand deux sources parlent de la même session, c'est la **plus récente** qui gagne — jamais celle
qui se trouvait en premier dans un dictionnaire — et l'utilisateur peut constater le désaccord au lieu de le
subir en silence.
**Depends on**: Phase 23 (des horodatages fiables : une écriture perdue fausse l'âge comparé) et Phase 22
(sans instrument honnête, le nouvel arbitrage n'est pas vérifiable).
**Requirements**: FUS-01, FUS-02
**Success Criteria** (what must be TRUE):
  1. **Le cas mesuré ne se reproduit plus** : une session dont le transcript a bougé il y a 10 secondes est
     annoncée « en cours », même si un fichier de hook vieux de 7 heures dit « à toi » — les trois inversions
     relevées par l'enquête (hook Working de 25 min → « inconnu » ; hooks de 7 h battant un transcript de
     10 s) disparaissent (FUS-01).
  2. **Le résultat ne dépend plus de l'ordre** : pour des données d'entrée identiques, permuter l'ordre
     d'enregistrement des sources ne change pas un seul état affiché (FUS-01).
  3. **Les désaccords sont lisibles** : quand deux sources se contredisent sur une session, le diagnostic
     nomme la source retenue, la source écartée et l'écart d'âge entre les deux — l'utilisateur peut
     diagnostiquer seul une source figée (FUS-02).
  4. **La précision ne bat pas la fraîcheur** : un signal plus ancien mais plus « spécifique » (un
     `permission_prompt`, par exemple) n'écrase jamais un signal plus récent, et le cas d'égalité d'âge est
     tranché par une règle explicite et testée, pas par le hasard d'un parcours.
**Plans**: 2 plans, 2 vagues strictement sérielles (24-02 affiche les désaccords que 24-01 fait produire :
il lui faut le 4e champ de `LectureSessions`. Les fichiers des deux plans sont par ailleurs disjoints.)

Plans:
- [x] 24-01-PLAN.md — FUS-01 : l'arbitrage se fait sur la fraîcheur, l'égalité d'âge a une règle nommée, et l'ordre d'insertion ne revient pas
- [x] 24-02-PLAN.md — FUS-02 : le diagnostic nomme la source retenue, la source écartée et l'écart d'âge

### Phase 25 : Le contrat d'événements refondé
**Goal**: Les états du widget viennent d'événements qui veulent **vraiment** dire ce qu'on leur fait dire :
« attend » naît d'une demande de permission réelle, « réfléchit » est réaffirmé tant que le travail dure, et
l'interruption au clavier cesse d'être un angle mort — le tout écrit noir sur blanc dans `docs/`.
**Depends on**: Phase 24 (EVT-03 n'a de valeur qu'avec FUS-01 : sans arbitrage par fraîcheur, un battement de
cœur frais serait écrasé par un hook ancien) et Phase 23 (les battements de cœur multiplient le volume
d'écritures — un chemin d'écriture qui perd en silence les rendrait inutiles).
**Requirements**: EVT-01, EVT-02, EVT-03, EVT-04, EVT-05
**Success Criteria** (what must be TRUE):
  1. **« Attend » naît d'une vraie demande** : quand Claude Code demande une permission, la session bascule
     en « à toi » immédiatement ; et une simple alerte d'absence (l'utilisateur n'a rien tapé depuis 60 s) ne
     fabrique plus aucun état (EVT-01, EVT-02).
  2. **« Réfléchit » ne s'éteint plus tout seul** : une session qui travaille reste annoncée « en cours »
     aussi longtemps qu'elle travaille, y compris au-delà d'une heure — l'état est réaffirmé par des
     battements de cœur au lieu d'être deviné par un seuil d'expiration (EVT-03).
  3. **Échap est couvert** : interrompre une réponse en cours (aucun `Stop` n'est émis par Claude Code) laisse
     la session dans un état juste et visible — elle m'attend — au lieu de rester figée sur « en cours » ou
     de disparaître (EVT-04).
  4. **Le silence se dit « inconnu »** : une session dont plus aucun signal n'arrive est annoncée inconnue,
     jamais « terminée » ni « tour fini » — l'expiration cesse d'être présentée comme une observation.
  5. **Le contrat est écrit** : `docs/` décrit les événements câblés, les champs lus, les états produits et
     surtout **ce qui n'est pas garanti** (`Stop` muet sur interruption, `SessionEnd.reason` qui ne couvre ni
     terminal tué ni crash) — une future dérive du contrat externe redevient détectable (EVT-05).
**Plans**: 4 plans, 4 vagues strictement sérielles. Les quatre se disputent `SessionHookProcessor.cs`,
`SessionHookInstaller.cs`, `SessionMonitor.cs` et les mêmes fichiers de tests : la propriété exclusive des
fichiers impose l'ordre. L'ordre est aussi celui de la dépendance logique — le battement de cœur (25-02) doit
exister avant que son SILENCE puisse fonder une déduction (25-03), et le contrat écrit (25-04) ne peut décrire
que ce qui a réellement été livré.

Plans:
- [ ] 25-01-PLAN.md — EVT-01 + EVT-02 : la liste blanche des 33 noms, le câblage validé par matcher, et l'attente qui naît d'une vraie demande
- [ ] 25-02-PLAN.md — EVT-03 : deux battements de cœur, un veto sous-agent, et un seuil qui devient celui du silence
- [ ] 25-03-PLAN.md — EVT-04 : l'attente DÉDUITE, visible et dite comme telle, jamais confondue avec un « tour fini »
- [ ] 25-04-PLAN.md — EVT-05 : le contrat des hooks écrit dans `docs/`, avec ses trois trous documentaires, et la garde qui l'empêche de mentir
**UI hint**: yes

### Phase 26 : « Traité » veut enfin dire quelque chose
**Goal**: Une session disparaît du widget parce qu'on l'a **réellement traitée** — transition observée sur la
même source, ou geste explicite de l'utilisateur — jamais parce qu'une source a expiré ; et ce qui a disparu
ne revient pas tout seul au prochain démarrage.
**Depends on**: Phase 24 (« même source » et « fraîcheur » doivent exister pour qu'une transition observée
signifie quelque chose) et Phase 25 (les transitions observables sont produites par le nouveau contrat
d'événements).
**Requirements**: TRT-01, TRT-02, TRT-03, TRT-04
**Success Criteria** (what must be TRUE):
  1. **Répondre fait disparaître, expirer non** : une session qui m'attendait et à laquelle j'ai répondu
     quitte le widget parce que la transition attente → travail a été **observée sur la même source** ; une
     source qui expire, ou une bascule transcript ↔ hook, ne marque plus jamais rien comme traité (TRT-01).
  2. **Le masquage de 6 heures ne se reproduit plus** : le scénario mesuré (attente enregistrée 478 min avant
     un seuil de 480 min, puis bascule de source) laisse désormais la session réellement en attente visible
     (TRT-01).
  3. **Le traité survit au redémarrage** : après fermeture et relance de l'overlay, les sessions déjà traitées
     ne ressortent pas — aujourd'hui elles ressortent **toutes** (TRT-02).
  4. **Un geste explicite existe** : l'utilisateur peut marquer une session traitée d'un geste direct, et elle
     disparaît immédiatement — sur les 8 styles de session et les 9 thèmes, sans casser la compacité du
     widget (TRT-03).
  5. **« Archiver » fait ce qu'il annonce** : soit l'entrée ne revient jamais, soit la durée est annoncée —
     plus de menu qui promet « permanent » pendant qu'un TTL de 6 h la fait réapparaître (TRT-04).
**UI hint**: yes

### Progress

**Execution Order:**
Phase 21 (démolition, prérequis de tout) → Phase 22 (instrument de mesure honnête, recâblé tôt **par choix**)
→ Phase 23 (magasin fiable = horodatages fiables) → Phase 24 (arbitrage par fraîcheur) → Phase 25 (contrat
d'événements, exige 24 pour les battements de cœur) → Phase 26 (« traité » observé, exige 24 et 25).

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 21. Périmètre — le widget ne parle que de Claude Code | 4/4 | Complete   | 2026-09-12 |
| 22. Un instrument de mesure qui ne ment plus | 3/3 | Complete   | 2026-09-12 |
| 23. Un magasin qui ne croît plus et n'oublie plus | 2/2 | Complete   | 2026-09-12 |
| 24. L'arbitrage par fraîcheur | 2/2 | Complete   | 2026-09-12 |
| 25. Le contrat d'événements refondé | 0/4 | Planned     | - |
| 26. « Traité » veut enfin dire quelque chose | 0/? | Not started | - |

### Couverture des exigences

18 requirements v1.6, chacun mappé à exactement une phase, aucun orphelin, aucun doublon.

| Phase | Requirements | Nombre |
|-------|--------------|--------|
| 21 | SRC-01, SRC-02, SRC-03 | 3 |
| 22 | OBS-01, OBS-02 | 2 |
| 23 | CYC-01, CYC-02 | 2 |
| 24 | FUS-01, FUS-02 | 2 |
| 25 | EVT-01, EVT-02, EVT-03, EVT-04, EVT-05 | 5 |
| 26 | TRT-01, TRT-02, TRT-03, TRT-04 | 4 |
| **Total** | | **18 / 18** |
