# Phase 22 : Un instrument de mesure qui ne ment plus - Context

**Gathered:** 2026-09-12
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Ce que le diagnostic rapporte est **exactement** ce que le widget affiche — même moniteur, mêmes filtres,
même instant — et il liste les sessions qui comptent au lieu des huit premières par ordre alphabétique.

Exigences : **OBS-01** (le diagnostic partage le moniteur du widget), **OBS-02** (sessions pertinentes).

**Hors périmètre :** le balayage du magasin (phase 23), la fusion par fraîcheur (phase 24), le contrat
d'événements (phase 25), le « traité » et le contrat d'archivage unique (phase 26). Cette phase ne change
**aucun comportement du widget** : elle rend le diagnostic fidèle à ce qui existe.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
Choix d'implémentation à la discrétion de Claude — discussion désactivée (`workflow.skip_discuss=true`),
`workflow.ui_phase=false`.

### Pourquoi cette phase est ici et pas en fin de milestone
L'ordre naïf placerait OBS-01 en dernier, « pour ne pas documenter un état intermédiaire ». On fait
l'inverse, délibérément : **le diagnostic est l'instrument qui servira à vérifier les phases 23 à 26.**

La contradiction se dissout si OBS-01 est livré comme un **partage d'instance** et non comme une copie de
comportement : le diagnostic interroge le moniteur **du widget**, donc il suit automatiquement chaque
changement ultérieur, sans retouche et sans jamais figer un état intermédiaire. **C'est la condition de
placement — si l'implémentation dérive vers une copie, l'argument tombe.**

### Doctrine du milestone
**Ne jamais présenter comme un fait ce qui n'a pas été observé.** Ici elle s'applique au rapport lui-même :
un diagnostic qui décrit un système différent de celui qui tourne est un mensonge poli.

### Contraintes projet
- MVVM strict, DI, dossiers Models/Views/ViewModels/Services.
- Aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`).
- **Compilabilité à chaque commit atomique** : `tests/Chronos.Tests` a un `ProjectReference` vers `Chronos` ;
  une erreur de compilation où que ce soit fait échouer TOUTE l'invocation `dotnet test`. Une tâche qui change
  une signature adapte ses sites d'appel DANS LA MÊME TÂCHE. Aucun « échec de build attendu ».
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement. Aucune dépendance NuGet nouvelle.
- UI et commentaires en **français**.
- **Baseline à l'entrée de phase : 719 tests verts**, suite en ~5 s.

### SÉCURITÉ
- **NE PAS toucher** aux fichiers d'état réels de `%APPDATA%\Chronos\sessions\` (66 entrées) ni à
  `archived.json` (84 octets, ses deux fantômes attendent le prochain lancement de l'utilisateur).
- **NE PAS lancer ni tuer l'overlay** (`Chronos-v3.0.2.exe`, pid 119412, en cours d'utilisation).
- Aucune requête réseau réelle depuis un test ; aucun test n'écrit dans le vrai `%APPDATA%\Chronos\`.
- **Invariant de coffre : `oauth.dat` = 518 octets. NE PAS vérifier son mtime** — le rafraîchissement
  préventif de la phase 17 le fait légitimement bouger toutes les 60 s (correction établie au plan 21-01).

</decisions>

<code_context>
## Existing Code Insights

### Le défaut, établi par l'enquête (ne pas ré-enquêter)
`DiagnosticService` construit **son propre `SessionMonitor` nu** (`new SessionMonitor(...)`, ~l. 460-485) :
**sans le filtre « traité », sans les filtres du widget**. Le rapport ne décrit donc pas le système qui
tourne. Il liste ensuite `files.Take(8)` — les **huit premiers fichiers par ordre alphabétique**, tous vieux
de plusieurs semaines sur cette machine.

**C'est très probablement pourquoi le problème n'a jamais été élucidé** : l'utilisateur lisait « des sessions
mortes depuis des semaines » dans le **rapport**, pas dans le widget. Constat de l'enquête, en direct :

```
fichiers de hook sur disque : 54  →  retenus 2  |  écartés par DropAfter 52
LE WIDGET AFFICHE           : 1 ligne
MASQUÉ par treated.json     : e465420e — session VIVANTE en attente de permission depuis 10 h
```

`e465420e` (PROJET ADVANCED SHEET) était une session vivante que le widget ne montrait **nulle part** et que
le diagnostic ne signalait pas non plus. Rendre ce cas lisible en une lecture est le critère n°2 de la phase.

### Fichiers concernés
- `src/Chronos/Services/DiagnosticService.cs` — la section « Widget sessions Claude Code » (~l. 455-500).
  **Signature à traiter avec prudence** : elle a été gelée pendant les milestones v1.5 (10+ sites de
  construction) ; le protocole établi est le **paramètre optionnel en dernière position**, qui coûte 0 site.
- `src/Chronos/Services/SessionMonitor.cs` — celui du conteneur DI est l'instance à partager.
- `src/Chronos/App.xaml.cs` — composition root ; le `SessionMonitor` y est déjà un singleton consommé par
  `Views.SessionsController`.
- `src/Chronos/ViewModels/SessionsViewModel.cs` — ce que le widget affiche réellement.
- `src/Chronos/Services/TreatedStore.cs`, `ArchiveStore.cs` — les filtres dont il faut dire qu'ils masquent.
- `tests/Chronos.Tests/DiagnosticServiceTests.cs`, `CompositionRootTests.cs`.

### Acquis de la phase 21 à respecter
- La source app-bureau n'existe plus ; `SessionMonitor` prend sa source de base par `ISessionSource`.
- `GardesPerimetreTests.CheminSources()` est `internal static` — réutilisable pour une garde de source.
- `DiagnosticService` a déjà un paramètre optionnel `IInventaireMachine` (phase 20) qui rend ses deux
  sondages chers substituables : la suite tourne en 5 s au lieu de 2 min 12. **Ne pas casser cet acquis.**

</code_context>

<specifics>
## Specific Ideas

Le critère de non-retour (n°4) est le plus important à long terme : **le diagnostic ne doit plus pouvoir
reconstruire son propre moniteur.** Une garde doit le prouver mécaniquement, et être falsifiable.

</specifics>

<deferred>
## Deferred Ideas

- Balayage des fichiers d'état expirés et des `.tmp` orphelins → **phase 23** (CYC-01).
- Fusion par fraîcheur et traçabilité des désaccords → **phase 24** (FUS-01/02).
- Contrat d'événements → **phase 25**. « Traité » et contrat d'archivage unique → **phase 26**.
- Vérifications in vivo héritées de la phase 21 (purge réelle d'`archived.json`, 8 styles × 9 thèmes,
  absence de lignes `desktop:` après usage prolongé) → à présenter à l'utilisateur en fin de milestone.

</deferred>
