# Phase 21 : Périmètre — le widget ne parle que de Claude Code - Context

**Gathered:** 2026-09-12
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

Le widget ne montre plus que des sessions **Claude Code**, réellement vivantes : la source app-bureau par
UI Automation et tout ce qui n'existait que pour elle disparaissent du dépôt, les entrées fantômes
`desktop:foreground:*` s'évaporent y compris celles déjà archivées à la main, et une vague de sous-agents
parallèles ne peut plus faire disparaître la vraie session.

Exigences : **SRC-01** (retrait de la source UIA), **SRC-02** (fantômes archivés purgés),
**SRC-03** (limite appliquée après le filtre sous-agents).

**Hors périmètre :** le contrat d'événements (phase 25), la fusion par fraîcheur (phase 24), le « traité »
(phase 26), le cycle de vie du magasin (phase 23), l'observabilité du diagnostic (phase 22). Cette phase
**démolit et nettoie** ; elle ne change aucune sémantique d'état.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
Tous les choix d'implémentation sont à la discrétion de Claude — la discussion est désactivée
(`workflow.skip_discuss=true`), `workflow.ui_phase=false`.

### Doctrine du milestone
**Ne jamais présenter comme un fait ce qui n'a pas été observé.** Un état dont la source a expiré n'est pas
« terminé » — il est **inconnu**. Cette phase n'applique pas encore la doctrine (c'est l'objet des phases
24-26), mais elle ne doit rien faire qui la contredise.

### Contrainte de comptage de tests — EXCEPTION DE CETTE PHASE
Le **recul** du nombre de tests est ici le **livrable**, pas une régression : ~529 lignes de tests couvrent
exclusivement du code supprimé. Critère opérationnel :
**0 échec + justification NOMINATIVE de chaque test supprimé**, et NON « ≥ 752 ».
Précédent exact : phase 16 du milestone v1.5.

### Contraintes projet
- MVVM strict, DI, dossiers Models/Views/ViewModels/Services.
- Aucun type WPF dans `Services/` ni `Models/` (`ServicesLayerPurityTests`).
- Solution **compilable à chaque commit atomique** : `tests/Chronos.Tests` a un `ProjectReference` vers
  `Chronos`, donc une erreur de compilation où que ce soit fait échouer TOUTE l'invocation `dotnet test`.
  Une tâche qui change une signature adapte ses sites d'appel DANS LA MÊME TÂCHE. Aucun « échec de build attendu ».
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement. Aucune dépendance NuGet nouvelle.
- UI et commentaires en **français**.

### SÉCURITÉ — contraintes absolues
- **NE PAS supprimer les fichiers d'état réels** de `%APPDATA%\Chronos\sessions\` (54 `.json` + 12 `.tmp`) :
  ce sont les données de l'utilisateur ET la matière de l'enquête. Seul le code **livré** doit les balayer,
  au prochain lancement, sous son contrôle. (Le balayage lui-même est la phase 23, pas celle-ci.)
- **NE PAS lancer ni tuer l'overlay** : une instance `Chronos-v3.0.2.exe` tourne (pid 119412) et
  l'utilisateur s'en sert. Tout checkpoint exigeant un lancement est substitué par une vérification non
  destructive et consigné sous « À VÉRIFIER PAR L'UTILISATEUR ».
- Aucune requête réseau réelle depuis un test ; jamais d'appel de refresh avec le refresh token RÉEL de
  `%APPDATA%\Chronos\oauth.dat` (518 octets, mtime 1783863147) ; aucun test n'écrit dans le vrai
  `%APPDATA%\Chronos\`.

</decisions>

<code_context>
## Existing Code Insights

### Inventaire MESURÉ le 2026-09-12 (pas estimé)

**Fichiers à supprimer intégralement — 765 lignes de `src/` :**

| Fichier | Lignes |
|---|---|
| `src/Chronos/Services/DesktopUiaSessionSource.cs` | 276 |
| `src/Chronos/Services/WindowsUiaTreeProvider.cs` | 198 |
| `src/Chronos/Services/UiaLabels.cs` | 93 |
| `src/Chronos/Services/DesktopUiaPollService.cs` | 86 |
| `src/Chronos/Services/WindowsForegroundWatch.cs` | 59 |
| `src/Chronos/Services/UiaNode.cs` | 24 |
| `src/Chronos/Services/IForegroundWatch.cs` | 16 |
| `src/Chronos/Services/IUiaTreeProvider.cs` | 13 |

`WindowsForegroundWatch` / `IForegroundWatch` tombent parce que l'hystérésis par focus (NET-02) **ne servait
que le bureau** : `SessionTreatmentTracker.IsForegroundDesktop` exige `Origin == Desktop` **et** un
identifiant `desktop:foreground:*`. Une session Claude Code a `Origin = Cli` et un UUID — le mécanisme ne
l'atteignait **jamais**. Le P/Invoke était consommé à chaque tick pour un résultat jamais lu.

**Tests à supprimer — 529 lignes :**
`DesktopUiaSessionSourceTests.cs` (423) et `DesktopUiaPollServiceTests.cs` (106).

**Sites d'accroche à adapter (hors fichiers supprimés) — vérifiés par grep :**
`src/Chronos/App.xaml.cs` (câblage DI + arguments de `SessionMonitor`),
`src/Chronos/Resources/SessionStyles.xaml` (les **8 styles** bindent le libellé de type),
`src/Chronos/Services/SessionMonitor.cs`, `SessionSnapshot.cs`, `SessionTreatmentTracker.cs`,
`DiagnosticService.cs`, `InventaireMachine.cs`,
`src/Chronos/ViewModels/SessionsViewModel.cs`, `SessionsPreviewViewModel.cs`,
et côté tests : `CompositionRootTests.cs`, `SessionsTests.cs`, `TreatedSessionsTests.cs`,
`TokenRefreshServiceTests.cs` *(à vérifier — l'occurrence y est peut-être fortuite)*.

### SRC-03 — l'étranglement de la source transcripts
`~/.claude/projects` contient **868 transcripts dont 817 `agent-*.jsonl` (94 %)**. Ces fichiers portent
`isSidechain: true` sur 100 % de leurs lignes et sont donc **correctement ignorés** — mais le `Take(12)` est
appliqué **AVANT** ce filtre. Chaque sous-agent chaud consomme un des 12 emplacements puis est jeté. Au
moment de la mesure, **4 des 5 fichiers chauds étaient des sous-agents**. Démontré : avec 12 sous-agents
chauds, la vraie session **disparaît complètement** de la source.

### SRC-02 — la trace du contournement de l'utilisateur
`%APPDATA%\Chronos\archived.json` contient encore deux entrées `desktop:foreground:*` (dont
`desktop:foreground:unknown` et `desktop:foreground:code`) datant de juillet, que `ArchiveStore.Load()`
écarte déjà. Elles prouvent que l'utilisateur avait dû **archiver ces fantômes à la main** : les entrées
`desktop:foreground:*` portaient `UpdatedAt == now` à chaque poll, donc elles ne vieillissaient **jamais** et
ne pouvaient **jamais** expirer.

### Conséquence de modèle
`SessionKind` et `SessionOrigin` perdent leur raison d'être → `SessionSnapshot` retrouve 5 champs,
`SessionsViewModel` perd son libellé de type. Impacte aussi `DiagnosticService` et `InventaireMachine`.

### Rapport source
`.planning/debug/widget-sessions-statuts.md`, section « Q7 — Volet périmètre ». **Ne pas ré-enquêter.**

</code_context>

<specifics>
## Specific Ideas

Le widget a **8 styles de session** (galerie `--sessions`) et **9 thèmes**. Le retrait du libellé de type ne
doit laisser ni case vide ni décalage dans aucune combinaison.

</specifics>

<deferred>
## Deferred Ideas

- Balayage des fichiers d'état expirés et des `.tmp` orphelins → **phase 23** (CYC-01).
- Diagnostic recâblé sur le moniteur du widget → **phase 22** (OBS-01).
- Contrat unique d'archivage (permanent vs TTL 6 h) → **phase 26** (TRT-04).

</deferred>
