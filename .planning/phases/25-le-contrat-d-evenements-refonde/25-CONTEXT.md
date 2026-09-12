# Phase 25 : Le contrat d'événements refondé - Context

**Gathered:** 2026-09-12
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Les états du widget viennent d'événements qui veulent **vraiment** dire ce qu'on leur fait dire : « attend »
naît d'une demande de permission réelle, « réfléchit » est réaffirmé tant que le travail dure, l'interruption
au clavier cesse d'être un angle mort — et le contrat est écrit noir sur blanc dans `docs/`.

Exigences : **EVT-01** (`PermissionRequest` alimente « en attente »), **EVT-02** (`Notification` cesse d'être
traité comme un état), **EVT-03** (battements de cœur : « réfléchit » observé, plus déduit par expiration),
**EVT-04** (l'interruption utilisateur ne laisse plus la session invisible), **EVT-05** (le contrat des hooks
documenté dans `docs/`).

**Hors périmètre :** le « traité », la persistance du tracker, le geste explicite et le contrat d'archivage
unique → **phase 26**. Cette phase change **ce que les sources produisent** ; elle ne retouche ni
l'arbitrage (phase 24, acquis) ni la sémantique du traitement.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
Discussion désactivée (`workflow.skip_discuss=true`). `UI hint: yes` au ROADMAP, mais l'essentiel de la phase
est dans `Services/` ; si un libellé du widget doit changer (critère n°4, « le silence se dit inconnu »),
il reste à la discrétion de Claude, dans le vocabulaire déjà en place.

### LIRE D'ABORD : le contrat externe a ete VERIFIE INDEPENDAMMENT

**`.planning/phases/25-le-contrat-d-evenements-refonde/25-CONTRAT-EXTERNE.md`** — releve du 2026-09-12
contre la documentation officielle. Il **CONFIRME** `PermissionRequest` et le catalogue, mais **CORRIGE
TROIS POINTS** du rapport d'enquete et apporte deux faits decisifs. En cas de contradiction entre le rapport
d'enquete et ce document, **c'est ce document qui fait foi**.

En resume, ce qui change la conception :
1. **`Notification` n'est pas une alerte d'absence** : c'est un **bus** a 12 types, filtrable par
   `matcher`. Le defaut reel : un bus entier reduit a un seul etat, si bien que `auth_success` et une
   reprise de quota fabriquent une attente. Remede : **filtrer par `matcher`**, pas lire un champ.
2. **Aucun evenement ne couvre l'interruption utilisateur** (`StopFailure` = erreurs d'API uniquement).
   EVT-04 ne peut etre que **deduit** — et la doctrine interdit de presenter comme observe ce qui est deduit.
3. **`SessionEnd.reason` a CINQ valeurs** : `resume` manquait.
4. **Aucun battement de coeur periodique n'existe.** Candidats : `MessageDisplay` (le plus fin, volume
   eleve, handler trivial obligatoire), `PostToolBatch`, `PreToolUse`/`PostToolUse` (preuve de vie
   **imparfaite** : rien entre le Pre et le Post d'un build de 10 min).
5. **PIEGE MAJEUR — les hooks d'un sous-agent portent le MEME `session_id`** que la session parente, et ne
   s'en distinguent que par `agent_id`/`agent_type`. C'est SRC-03 qui reapparait cote hooks, sur une
   machine ou **94 % des transcripts sont des sous-agents**. Tout battement de coeur DOIT filtrer dessus.
6. **Le sort d'un nom d'evenement inconnu n'est PAS documente** — mode de defaillance le plus dangereux.
   Parade obligatoire : valider chaque cle contre une **liste blanche des 33 noms codee en dur AVANT
   d'ecrire** dans `settings.json`.
7. **Aucun nom de champ specifique a un evenement n'est confirmable** (doc tronquee) : passer par les
   **valeurs de `matcher`** partout ou c'est possible.

### La cause racine n°2, établie contre la documentation officielle (NE PAS ré-enquêter)

Relevé du 2026-09-12, `.planning/debug/widget-sessions-statuts.md` :

| Constat | Conséquence pour Chronos |
|---|---|
| **`Stop` NE se déclenche PAS sur interruption utilisateur** (Échap) | la session n'est **jamais** annoncée en attente dans le cas où elle attend le plus |
| **`Notification` est une alerte d'ABSENCE** (« tu sembles absent du terminal »), conditionnée à ≥ 60 s d'inactivité, et couvre permission **ET** inactivité **ET** fin de tâche | faux positifs si l'utilisateur est absent, faux négatifs s'il est présent — ce n'est **pas un état** |
| **Un hook DÉDIÉ `PermissionRequest` existe** et n'est pas utilisé | le signal exact et immédiat est disponible, Chronos lui préfère un proxy |
| `SessionEnd.reason ∈ {clear, logout, prompt_input_exit, other}` | ne couvre ni terminal tué, ni crash, ni redémarrage machine (déjà compensé en phase 23 par le balayage) |
| Le catalogue compte ~**30** événements ; Chronos en câble **5** | `StopFailure`, `SubagentStart`, `PermissionDenied`, `PreCompact`/`PostCompact`… sont inexploités |

**La sémantique de la source est FAUSSE, pas seulement en retard.**

### Le code qui porte cette sémantique
`src/Chronos/Services/SessionHookProcessor.cs` (104 lignes), cœur **pur** et testable. Sa table actuelle,
écrite dans sa propre XML-doc :

```
Notification (permission/idle/agent_needs_input…) → WaitingAttention
Stop                                              → WaitingTurn
UserPromptSubmit / SessionStart                   → Working
SessionEnd                                        → suppression du fichier
SubagentStop / inconnu                            → ignoré
```

Il lit déjà `notification_type` sur stdin et le transporte dans le champ `reason` du fichier d'état. Le
routage par `switch` sur le nom d'événement est le point d'entrée naturel de la refonte.

### Ce que la phase doit installer chez l'utilisateur — et par quel chemin
Ajouter des événements veut dire **écrire dans `~/.claude/settings.json`**. Le mécanisme existe déjà et a été
durci en phase 15 : `ClaudeSettingsReconciler` / `ClaudeSettingsJson` (installation **idempotente**, plus
d'écrasement destructif). **Toute nouvelle entrée de hook doit passer par lui**, jamais par une écriture
ad hoc. État mesuré de `~/.claude/settings.json` aujourd'hui : **5 hooks Chronos** (`SessionStart`,
`Notification`, `Stop`, `UserPromptSubmit`, `SessionEnd`) + un `--statusline`. Les entrées `PreToolUse` /
`PostToolUse` qui s'y trouvent appartiennent à un **autre outil** — ne pas les toucher, ne pas les compter
comme des nôtres, et vérifier que la réconciliation les préserve.

### EVT-03 — les battements de cœur, et leur coût
« Réfléchit » doit être **réaffirmé** tant que le travail dure. Le choix de l'événement porteur est à la
discrétion de Claude, mais deux contraintes le bornent :
- **Le volume d'écritures augmente** : c'est précisément pourquoi la phase 23 (écriture directe, 0 perte sur
  500) est une **dépendance** et non du ménage. Vérifier que le chemin d'écriture livré en 23-01 tient la
  cadence visée, et ne pas réintroduire tmp+Move.
- **Un battement de cœur est une observation, pas une intention** : il dit « ça travaillait à cet instant »,
  jamais « ça travaille encore maintenant ». La doctrine du milestone s'applique au libellé comme au code.

### EVT-04 — l'interruption au clavier
Aucun `Stop` n'est émis. La session ne doit ni rester figée sur « en cours », ni disparaître : elle
m'attend. Comment le détecter est à la discrétion de Claude — mais **rien ne doit être présenté comme
observé s'il n'a été que déduit**. Si l'interruption ne peut être qu'inférée, l'état juste est celui que
l'inférence autorise, et le rapport doit pouvoir le dire.

### Doctrine du milestone — critère n°4
**Ne jamais présenter comme un fait ce qui n'a pas été observé.** Une session dont plus aucun signal n'arrive
est annoncée **inconnue**, jamais « terminée » ni « tour fini ». L'expiration cesse d'être présentée comme
une observation. C'est le pendant exact du « exact ou rien » du cadran en v1.5.

### EVT-05 — le contrat écrit
`docs/` contient déjà `data-sources.md` (précédent de format) et `publish.md`. Le document des hooks doit
décrire les événements câblés, les champs lus, les états produits **et surtout ce qui n'est PAS garanti** :
`Stop` muet sur interruption, `SessionEnd.reason` qui ne couvre ni terminal tué ni crash, `Notification`
conditionnée à l'absence. **L'absence de ce document est ce qui a laissé la dérive du contrat externe passer
inaperçue** — c'est la raison d'être de l'exigence, pas une formalité.

### Contraintes projet
- MVVM strict, DI ; aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`).
- **`NormalisationUniqueTests`** balaie `Services/` et `Models/`, commentaires compris : aucun `/ 100`,
  `* 100`, `FromUnixTimeSeconds`/`Milliseconds`, `DateTimeOffset.TryParse`. Utiliser
  `UsageNormalization.InstantDepuisEpochMillisecondes`.
- **Horloge injectable** : `DateTimeOffset.UtcNow` = 0 dans tout nouveau service et ses tests.
- **Compilabilité à chaque commit atomique** ; aucun « échec de build attendu ». `tests/Chronos.Tests` a un
  `ProjectReference` vers `Chronos` : une tâche qui change une signature adapte ses sites d'appel DANS LA
  MÊME TÂCHE.
- **Consigne de rédaction** (établie en phase 24) : ne jamais écrire littéralement les chaînes que les gardes
  interdisent, **commentaires et XML-doc compris** — les gardes lisent le TEXTE.
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement. Aucune dépendance NuGet nouvelle.
- UI et commentaires en **français**.
- **Baseline à l'entrée de phase : 788 tests verts**, suite en ~4 s.

### SÉCURITÉ
- **NE PAS modifier `~/.claude/settings.json` à la main.** C'est la configuration vivante de l'utilisateur :
  seul le code **livré**, via `ClaudeSettingsReconciler`, doit y écrire, au prochain lancement, sous son
  contrôle. Les tests travaillent sur des copies en dossier temporaire.
- **NE PAS supprimer ni modifier** les fichiers d'état réels de `%APPDATA%\Chronos\sessions\` (66 entrées)
  ni `archived.json` (84 octets).
- **NE PAS lancer ni tuer l'overlay** (`Chronos-v3.0.2.exe`, pid 119412, en cours d'utilisation).
- Aucune requête réseau réelle ; aucun test n'écrit dans le vrai `%APPDATA%\Chronos\` ni dans le vrai
  `~/.claude/`.
- **`oauth.dat` = 518 octets — NE PAS vérifier son mtime** (le refresh préventif de la phase 17 le fait
  légitimement bouger toutes les 60 s).

</decisions>

<code_context>
## Existing Code Insights

### Fichiers concernés
- `src/Chronos/Services/SessionHookProcessor.cs` — **le cœur pur à refondre** (table de routage, 104 lignes).
- `src/Chronos/App.xaml.cs` — mode `--hook` (arguments acceptés), et le câblage au démarrage.
- `src/Chronos/Services/ClaudeSettingsReconciler.cs`, `ClaudeSettingsJson.cs` — installation idempotente des
  hooks (phase 15). **Seul chemin autorisé** vers `settings.json`.
- `src/Chronos/Services/EcritureEtatSession.cs` — écriture directe livrée en 23-01 (0 perte sur 500).
- `src/Chronos/Services/SessionMonitor.cs` — `Inspecter`, les seuils `StaleWorking` (20 min) et `DropAfter`
  (8 h). **Si les battements de cœur rendent `StaleWorking` obsolète, c'est un changement de sémantique à
  assumer et à tester, pas un réglage à retoucher au passage.**
- `src/Chronos/Services/ArbitrageSessions.cs`, `LectureSessions.cs` — livrés en phase 24. Les nouveaux
  signaux entrent dans l'arbitrage par fraîcheur **sans modification** : ne pas les retoucher.
- `docs/data-sources.md` — précédent de format pour le document EVT-05.
- Tests : `SessionHookProcessorTests.cs`, `SessionsTests.cs`, `InspectionSessionsTests.cs`,
  `ClaudeSettingsReconcilerTests.cs`, `GardesPerimetreTests.cs`.

### Acquis à ne pas casser
- **Phase 21** : périmètre Claude Code seul, gardes de non-retour, `Take(12)` après le filtre sous-agents.
- **Phase 22** : `Read(now) => Inspecter(now).Visibles` **unique** implémentation des filtres ; **zéro**
  `new SessionMonitor` dans `DiagnosticService` ; partage d'instance.
- **Phase 23** : `EcritureEtatSession` (écriture directe), `BalayageMagasinSessions` (critère double,
  horloge injectée). **Un fragment illisible est une ABSENCE de signal**, jamais un vieux signal.
- **Phase 24** : arbitrage par **fraîcheur** (`ArbitrageSessions.Trancher`), désaccords tracés
  (`LectureSessions.Desaccords`, bloc « Désaccords entre sources » du diagnostic). `MotifMasquage` reste
  à **3**. Un désaccord n'est pas un masquage.
- **Phase 20** : `?? new ` reste à **2**, suite en ~4 s.

### Observation héritée de la vérification de la phase 24, à prendre en compte ici
Dans `ArbitrageSessions.Departager`, le rang « source » précède le rang « urgence » : à horodatage identique
**à la milliseconde**, un hook ramené à `Unknown` battrait un transcript `Working`. Invraisemblable
aujourd'hui — **mais si cette phase ajoute une source dont les horodatages sont dérivés d'une autre,
l'égalité exacte devient possible.** À vérifier si le cas se présente ; ne pas modifier l'arbitrage sans
qu'un test le motive.

</code_context>

<specifics>
## Specific Ideas

Le critère n°5 (le contrat écrit) est le seul qui protège contre la **répétition** de la panne : les quatre
autres réparent la dérive d'aujourd'hui, celui-là rend la prochaine détectable. Le document doit donc porter
**ce qui n'est pas garanti** au moins autant que ce qui l'est — et nommer la date et la source du relevé,
pour qu'un écart futur se voie.

</specifics>

<deferred>
## Deferred Ideas

- « Traité » observé, persistance du tracker, geste explicite, contrat d'archivage unique → **phase 26**
  (TRT-01 à TRT-04).
- Exploitation des ~25 autres événements du catalogue (`StopFailure`, `PreCompact`, `TeammateIdle`…) →
  **hors périmètre v1.6** : cette phase câble ce que les cinq critères exigent, pas le catalogue.
- Vérifications in vivo héritées des phases 21 à 24 → à présenter en fin de milestone. Rappel : **rien de ce
  milestone ne s'exécute tant que l'exe n'est pas republié** — les hooks pointent `Chronos-v3.0.2.exe`,
  l'exe qui tourne, antérieur à tout v1.6. Et ceux de cette phase ne s'exécuteront qu'après une
  republication **suivie d'une réconciliation de `settings.json`** au lancement.

</deferred>
