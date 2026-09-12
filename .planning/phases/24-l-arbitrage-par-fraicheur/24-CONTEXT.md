# Phase 24 : L'arbitrage par fraîcheur - Context

**Gathered:** 2026-09-12
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Quand deux sources parlent de la même session, c'est la **plus récente** qui gagne — jamais celle qui se
trouvait en premier dans un dictionnaire — et l'utilisateur peut constater le désaccord au lieu de le subir
en silence.

Exigences : **FUS-01** (arbitrage par fraîcheur), **FUS-02** (désaccords traçables).

**Hors périmètre :** le contrat d'événements (phase 25) et le « traité » (phase 26). Cette phase ne change
pas **ce que les sources produisent** — elle change **comment on tranche entre elles**.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
Discussion désactivée (`workflow.skip_discuss=true`), `workflow.ui_phase=false`.

### Le défaut, mesuré contre les classes réelles (ne pas ré-enquêter)
`SessionMonitor.Read` arbitre par **ordre d'insertion dans un dictionnaire** : étape 1 les transcripts,
étape 2 les hooks — donc **les hooks gagnent toujours**, quel que soit leur âge. Vérité terrain : le modèle
travaille, transcript écrit il y a 10 secondes.

```
1. transcript seul                        -> Working            (correct)
3. + hook Working de 25 min (> 20 min)    -> Unknown            << le périmé écrase le frais et CORRECT
4. + hook WaitingTurn de 7 h              -> WaitingTurn        << « tour fini » pendant que ça travaille
5. + hook WaitingAttention de 7 h         -> WaitingAttention   << « à toi » sans aucune permission demandée
6. + hook WaitingTurn de 9 h (> DropAfter)-> Working            << le hook s'efface, le transcript reprend
```

**Un signal de 7 heures bat un signal de 10 secondes.** C'est la cause racine n°1 du milestone.

### Critère n°4 — la précision ne bat pas la fraîcheur
Un signal plus ancien mais plus « spécifique » (un `permission_prompt`, par exemple) ne doit **jamais**
écraser un signal plus récent. Et le cas d'**égalité d'âge** doit être tranché par une **règle explicite et
testée**, jamais par le hasard d'un parcours de collection.

### Doctrine du milestone
**Ne jamais présenter comme un fait ce qui n'a pas été observé.** Ici : un état ancien n'est pas une vérité
plus solide parce qu'il est plus détaillé. La fraîcheur prime, et ce qui est écarté doit être **dit**.

### Contraintes projet
- MVVM strict, DI ; aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`).
- **`NormalisationUniqueTests`** balaie `Services/` et `Models/`, commentaires compris : aucun `/ 100`,
  `* 100`, `FromUnixTimeSeconds`/`Milliseconds`, `DateTimeOffset.TryParse`. Utiliser
  `UsageNormalization.InstantDepuisEpochMillisecondes`.
- **Horloge injectable** : `DateTimeOffset.UtcNow` = 0 dans tout nouveau service et ses tests. Trois plans de
  la phase 22 ont dû corriger des tests à retardement causés par un horodatage figé.
- **Compilabilité à chaque commit atomique** ; aucun « échec de build attendu ».
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement. Aucune dépendance NuGet nouvelle.
- UI et commentaires en **français**.
- **Baseline à l'entrée de phase : 763 tests verts**, suite en ~4 s.

### SÉCURITÉ
- **NE PAS supprimer ni modifier** les fichiers d'état réels de `%APPDATA%\Chronos\sessions\` (66 entrées)
  ni `archived.json` (84 octets). Les tests travaillent sur des copies temporaires.
- **NE PAS lancer ni tuer l'overlay** (`Chronos-v3.0.2.exe`, pid 119412, en cours d'utilisation).
- Aucune requête réseau réelle ; aucun test n'écrit dans le vrai `%APPDATA%\Chronos\`.
- **`oauth.dat` = 518 octets — NE PAS vérifier son mtime** (le refresh préventif de la phase 17 le fait
  légitimement bouger toutes les 60 s).

</decisions>

<code_context>
## Existing Code Insights

### Le point à refondre
`src/Chronos/Services/SessionMonitor.cs` — `Inspecter(now)` (livré en phase 22 ; `Read` n'en est qu'une
projection). C'est là que les sources sont fusionnées, aujourd'hui par ordre d'insertion.

### Ce que la fusion doit désormais comparer
Chaque signal porte un horodatage : les transcripts par le `mtime` / la dernière ligne, les hooks par
`updated_at` (epoch millisecondes, snake_case). La comparaison doit se faire sur ces instants, pas sur
l'ordre du code.

### FUS-02 — où loger la traçabilité
La phase 22 a livré le **partage d'instance** : le diagnostic interroge le `SessionMonitor` du conteneur, et
`Inspecter` rend déjà les **masquages** avec leur motif. Le désaccord entre sources doit suivre le même
chemin — rendu par `Inspecter`, affiché par le diagnostic — **sans qu'une ligne de `DiagnosticService.cs`
n'ait à changer** si possible. Si un changement y est nécessaire, il doit rester minimal et être justifié.

### Acquis à ne pas casser
- **Phase 21** : source app-bureau supprimée, gardes de non-retour, `TranscriptSessionSource : ISessionSource`.
- **Phase 22** : `Read(now) => Inspecter(now).Visibles` reste l'**unique** implémentation des filtres ;
  **zéro** `new SessionMonitor` dans `DiagnosticService` ; les deux gardes de non-retour.
- **Phase 23** : `EcritureEtatSession` (écriture directe, échec constatable) et `BalayageMagasinSessions`
  (critère double : > 72 h **ET** aucune source n'atteste la vie de la session ; aucun magasin de verdict).
  **Contrepartie assumée** de l'écriture directe : elle tronque la cible avant de la réécrire, donc un lecteur
  malchanceux lit un fragment et relira 2 s plus tard. **À prendre en compte ici** : un fragment illisible ne
  doit pas être interprété comme un signal ancien — c'est l'absence de signal.
- **Phase 20** : `machine ?? new InventaireMachine()` — `?? new ` reste à 2, suite en ~4 s.

### Fichiers concernés
`src/Chronos/Services/SessionMonitor.cs`, `LectureSessions.cs`, `TranscriptSessionSource.cs`,
`SessionSnapshot.cs`, `DiagnosticService.cs` (si nécessaire),
`tests/Chronos.Tests/SessionsTests.cs`, `InspectionSessionsTests.cs`, `TreatedSessionsTests.cs`,
`DiagnosticServiceTests.cs`.

</code_context>

<specifics>
## Specific Ideas

Le critère n°2 est le plus facile à rendre mécaniquement vrai et le plus révélateur : **pour des données
d'entrée identiques, permuter l'ordre d'enregistrement des sources ne doit pas changer un seul état
affiché.** Un test de permutation vaut mieux qu'une assertion ponctuelle.

</specifics>

<deferred>
## Deferred Ideas

- Contrat d'événements (`PermissionRequest`, battements de cœur, interruption utilisateur) → **phase 25**.
- « Traité » observé, persistance du tracker, geste explicite, contrat d'archivage unique → **phase 26**.
- Vérifications in vivo héritées des phases 21, 22 et 23 → à présenter en fin de milestone. Rappel : **rien
  de ce milestone ne s'exécute tant que l'exe n'est pas republié** — les hooks de l'utilisateur pointent
  l'exe v3.0.2 en cours d'exécution.

</deferred>
