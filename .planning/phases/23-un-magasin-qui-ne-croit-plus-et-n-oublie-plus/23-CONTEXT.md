# Phase 23 : Un magasin qui ne croît plus et n'oublie plus - Context

**Gathered:** 2026-09-12
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

`%APPDATA%\Chronos\sessions` cesse d'être un dépotoir qui ne fait que grandir, et une écriture d'état de hook
cesse de pouvoir disparaître sans que personne ne le sache.

Exigences : **CYC-01** (balayage des états expirés et des `.tmp` orphelins), **CYC-02** (plus d'écriture
perdue en silence).

**Hors périmètre :** la fusion par fraîcheur (phase 24), le contrat d'événements (phase 25), le « traité » et
le contrat d'archivage unique (phase 26). Cette phase répare le **magasin**, pas la sémantique des états.

**Pourquoi elle précède la phase 24 :** l'arbitrage par fraîcheur compare des horodatages. Une écriture
perdue en silence fait paraître un état **plus vieux qu'il n'est** et empoisonne l'arbitrage à sa racine.
Réparer l'écriture avant d'y adosser une règle de décision n'est pas du ménage, c'est une **dépendance**.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
Discussion désactivée (`workflow.skip_discuss=true`), `workflow.ui_phase=false`.

### Doctrine du milestone — elle mord ici
**Ne jamais présenter comme un fait ce qui n'a pas été observé.** Corollaire direct du critère n°4 :
**expirer, c'est ne plus savoir.** Une session dont l'état a été balayé doit disparaître du widget **sans
jamais être annoncée « terminée » ni « traitée »**. Balayer ne doit pas devenir une façon détournée de
conclure.

### Contraintes projet
- MVVM strict, DI, dossiers Models/Views/ViewModels/Services.
- Aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`).
- **`NormalisationUniqueTests`** balaie le texte source de `Services/` et `Models/`, commentaires compris :
  aucun `/ 100`, `* 100`, `FromUnixTimeSeconds`/`Milliseconds`, `DateTimeOffset.TryParse`.
- **Compilabilité à chaque commit atomique** ; aucun « échec de build attendu ».
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement. Aucune dépendance NuGet nouvelle.
- UI et commentaires en **français**.
- **Baseline à l'entrée de phase : 747 tests verts**, suite en ~4 s.

### PIÈGE DE TEST À RETARDEMENT — trois plans de la phase 22 s'y sont heurtés
`TreatedStore` purge à **6 h sur l'horloge SYSTÈME**, sans horloge injectable. Écrire un horodatage littéral
figé dans un magasin rend des tests rouges à une heure donnée du soir **sans qu'aucun code ne bouge**.
Remède établi : un helper qui écrit « maintenant ». Le même piège guette tout nouveau magasin de cette
phase — **prévoir l'horloge injectable dès la conception** plutôt que de reproduire le défaut.

### SÉCURITÉ — contraintes absolues
- **NE PAS supprimer ni modifier les fichiers d'état réels** de `%APPDATA%\Chronos\sessions\` (66 entrées :
  54 `.json` + 12 `.tmp`). Ce sont les données de l'utilisateur ET la matière de l'enquête. C'est le code
  **livré** qui doit les balayer, au prochain lancement, sous son contrôle. Les tests travaillent sur des
  copies en dossier temporaire.
- **NE PAS toucher** à `archived.json` (84 octets — ses deux fantômes attendent le prochain lancement).
- **NE PAS lancer ni tuer l'overlay** (`Chronos-v3.0.2.exe`, pid 119412, en cours d'utilisation).
- Aucune requête réseau réelle depuis un test ; aucun test n'écrit dans le vrai `%APPDATA%\Chronos\`.
- **Invariant de coffre : `oauth.dat` = 518 octets. NE PAS vérifier son mtime** — le rafraîchissement
  préventif de la phase 17 le fait légitimement bouger toutes les 60 s.

</decisions>

<code_context>
## Existing Code Insights

### État réel du magasin, mesuré (ne pas ré-enquêter)
- **66 entrées** : **54 fichiers `.json`** + **12 `.tmp-<pid>` orphelins**.
- **51 fichiers sur 54 ont plus de 24 h. 48 ont plus de 7 jours.** Âge médian ≈ **37 jours**.
- **32 fichiers se déclarent « Working »** tout en ayant plus de 24 h.
- **Cinq des `.tmp` portent le MÊME `session_id`** avec cinq PID différents — débris du fan-out des 25 hooks
  concurrents (corrigé en phase 15).
- Schéma d'un fichier : `{"session_id","project","activity","updated_at"}` (snake_case, epoch millisecondes).

### CYC-01 — pourquoi rien n'est jamais supprimé
La **seule** suppression d'un fichier d'état dans tout `src/` est dans `App.xaml.cs` (mode `--hook`), sur
`SessionEnd`. Or `SessionEnd.reason ∈ {clear, logout, prompt_input_exit, other}` : **aucune valeur ne couvre
un terminal tué, un crash ou un redémarrage machine** — dans ces cas le hook ne peut simplement pas
s'exécuter. S'y ajoute que la config des hooks est lue au **démarrage** de la session : une session lancée
avant l'installation, ou pointant un exe depuis effacé, n'écrira ni ne supprimera jamais rien.
**Le magasin ne peut que croître.** Aucun balayage d'expiration n'existe nulle part.

### CYC-02 — le mécanisme de perte, mesuré
`File.Move(tmp, dst, overwrite: true)` échoue avec `UnauthorizedAccessException` (0x80070005) si un lecteur
tient le fichier — et `SessionMonitor.TryRead` ouvre en `FileShare.ReadWrite`. La mise à jour est alors
**perdue en silence** (`catch {}`), et le `.tmp` reste.

Mesures de référence :
- sous lecteur serré : **290 écritures perdues sur 500** ;
- ajouter `FileShare.Delete` **ne corrige pas** (200/500) ;
- **écriture directe `FileMode.Create` / `FileShare.Read` : 0 perte sur 500** ;
- variante tmp+Move avec 5 tentatives espacées de 15 ms **et** `FileShare.ReadWrite | Delete` côté lecteur :
  3/500.

**Poids réel en production : ~0,7 événement perdu sur 5 000** (une passe de lecture coûte 15 ms pour 54
fichiers à une cadence de 2 s). CYC-02 vise donc d'abord **le silence** — qu'un échec soit constatable —
plus que l'optimisation fine, explicitement hors périmètre du milestone.

### Fichiers concernés
- `src/Chronos/App.xaml.cs` — mode `--hook` (écriture/suppression), et le démarrage overlay où câbler le balayage.
- `src/Chronos/Services/SessionHookProcessor.cs` — produit l'ordre `Delete`.
- `src/Chronos/Services/SessionMonitor.cs` — `TryRead`, `Directory`, les filtres (`Inspecter` depuis la phase 22).
- `src/Chronos/Services/ArchiveStore.cs` — précédent de purge câblée au démarrage (`PurgerPrefixe`, phase 21).
- `src/Chronos/Services/DiagnosticService.cs` — doit refléter le balayage sans qu'une ligne ne change
  (partage d'instance, phase 22).
- Tests : `SessionsTests.cs`, `ArchiveStorePurgeTests.cs` (précédent de purge), `GardesPerimetreTests.cs`.

### Acquis à ne pas casser
- Phase 21 : source app-bureau supprimée, gardes de non-retour, `TranscriptSessionSource : ISessionSource`.
- Phase 22 : `Read(now) => Inspecter(now).Visibles` (unique implémentation des filtres), partage d'instance
  du moniteur avec le diagnostic, **zéro** `new SessionMonitor` dans `DiagnosticService`.
- Phase 20 : `machine ?? new InventaireMachine()` — `?? new ` reste à **2**, suite en 4 s au lieu de 2 min 12.

</code_context>

<specifics>
## Specific Ideas

Le précédent de `ArchiveStore.PurgerPrefixe` (phase 21) est le modèle : purger et expirer sont **deux gestes
distincts**, la purge ne dépend d'aucun TTL, le nombre rendu est une observation et non une intention, et
rien à retirer ⇒ le fichier n'est pas réécrit.

</specifics>

<deferred>
## Deferred Ideas

- Fusion par fraîcheur et traçabilité des désaccords → **phase 24** (FUS-01/02).
- Contrat d'événements (`PermissionRequest`, battements de cœur, interruption) → **phase 25**.
- « Traité » observé et contrat d'archivage unique → **phase 26**.
- Optimisation fine des écritures concurrentes → **hors périmètre v1.6** (poids mesuré ~0,7 / 5 000).
- Vérifications in vivo héritées des phases 21 et 22 → à présenter en fin de milestone.

</deferred>
