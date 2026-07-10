# Phase 13 : Source UIA app bureau - Context

**Gathered:** 2026-07-10
**Status:** Ready for planning
**Mode:** Contexte verrouillé (conseil llm-council + spike UIA prouvé le 2026-07-10) — pas de grey areas ouverts.

<domain>
## Phase Boundary

Ajouter une **nouvelle source de sessions** `DesktopUiaSessionSource` qui lit l'arbre UI Automation de la
fenêtre de l'app de bureau Claude et produit des `SessionSnapshot` pour les sessions Chat / Code / Cowork
de l'app bureau, en plus des sessions Claude Code CLI existantes (transcripts + hooks). États honnêtes
(bosse / attend / attend-permission / indéterminé), type identifié, sessions actives de la sidebar
énumérées. Robuste aux MAJ de l'app, lecture non bloquante pour le thread UI.

**Dans le périmètre :** la source UIA, son intégration dans `SessionMonitor`, l'extension du modèle
`SessionSnapshot` (type + origine), l'affichage du type dans la liste, les tests unitaires via un faux
arbre. **Hors périmètre (Phase 14) :** l'auto-disparition « traitées ». **Hors périmètre (différé) :**
notifications Windows, usage par session.
</domain>

<decisions>
## Implementation Decisions

### Architecture de la source
- **Testabilité = contrainte n°1.** Le walk `System.Windows.Automation` réel est isolé derrière un seam
  injectable : une interface neutre (ex. `IUiaTreeProvider`) qui retourne un **arbre neutre** (DTO
  `UiaNode { string ControlType; string Name; string? AutomationId; bool Enabled; IReadOnlyList<UiaNode> Children }`).
  Toute la LOGIQUE de `DesktopUiaSessionSource` (mapping arbre→snapshots, matching fr/en, santé, dégradation)
  travaille sur ce DTO et se teste avec un **faux arbre** — aucune fenêtre Claude réelle requise en test/CI.
- L'implémentation réelle `WindowsUiaTreeProvider` (celle qui appelle `AutomationElement`/`TreeWalker`)
  n'est PAS unit-testée (dépend de l'OS) ; elle est mince et sans logique métier.
- Intégration : `SessionMonitor` reçoit une source bureau supplémentaire (paramètre optionnel nullable,
  cohérent avec le style existant `transcripts`/`sessionsDir`), fusionnée dans `Read(now)` **après**
  transcripts + hooks. Une `ISessionSource { IReadOnlyList<SessionSnapshot> Read(DateTimeOffset now); }`
  peut être introduite et implémentée par `TranscriptSessionSource` + la nouvelle source (non cassant) —
  au choix du planner, tant que la testabilité par faux arbre est respectée.

### Threading (ROB-07) — NON bloquant
- La lecture UIA peut être coûteuse → elle ne doit JAMAIS s'exécuter sur le thread UI.
- Modèle retenu : `DesktopUiaSessionSource` maintient un **cache** de son dernier résultat, alimenté par
  un **poll en arrière-plan** (~1-2 s). `SessionMonitor.Read(now)` retourne ce cache de façon **synchrone
  et non bloquante** (aucune I/O UIA dans le chemin appelé par `SessionsViewModel.Refresh`/timer 2 s).
- L'`AutomationElement` racine (fenêtre Claude) est mis en **cache** entre les polls (réacquis si invalide).

### Mapping des états (matcher par ControlType + Name, PAS AutomationId volatils ; table fr/en)
Signaux vérifiés au spike (2026-07-10). Une **table de libellés fr/en** centralisée mappe :
- `Text "Claude répond." / "Claude is responding"` **ou** `Button "Arrêter" / "Stop"` présent → **Working** (bosse).
- `Button "Ignorer les permissions"` (+ variantes autorisation) → **WaitingAttention** (attend permission).
- `Text "Mode chat" / "Chat mode"` + placeholder `"Tapez / pour les commandes" / "..."`, sans « Claude répond. »
  → **WaitingTurn** (repos, attend ton message).
- Rien d'exploitable / ancre absente → **Unknown** (jamais inventé).
- **Sidebar** : `Button "En cours d'exécution <nom>" / "Running <nom>"` → session agentique active (Working)
  nommée `<nom>` ; les autres entlistées sans ce préfixe ne sont PAS listées comme sessions actives.

### Type de session (BUR-03) — extension du modèle
- Étendre `SessionSnapshot` (record) avec un champ **`SessionKind Kind`** (enum `Unknown, Chat, Code, Cowork`)
  et **`SessionOrigin Origin`** (enum `Cli, Desktop`) — paramètres AJOUTÉS avec valeurs par défaut
  (`Unknown`/`Cli`) pour ne PAS casser les usages existants (`TranscriptSessionSource`, hooks, tests).
- Dérivation du type bureau : `Text "Mode chat"` → Chat ; onglet/vue `Code` + panneaux `Terminal/Diff/Aperçu/
  Actions de session` → Code ; `Contrôle à distance` (pont VM) ou panneau Cowork → Cowork.

### Cowork-VM (BUR-05) — honnêteté
- Une session Cowork dont l'exécution est en VM distante (`Contrôle à distance` visible) est marquée
  `Kind=Cowork, Activity=Unknown` — jamais un état d'exécution certain. Sa présence est signalée, son état non.

### Identité des sessions bureau
- Les sessions bureau n'ont pas de `session_id` JSONL. Clé synthétique STABLE : `desktop:<kind>:<nom-sidebar>`
  pour les sessions de la sidebar ; pour la conversation au premier plan sans nom fiable (titre fenêtre = « Claude »),
  clé fixe `desktop:foreground:<kind>`. Ces clés servent la fusion et le futur magasin « traitées » (Phase 14).

### Robustesse (ROB-06)
- **Test de santé au démarrage / à chaque poll** : vérifier que la fenêtre Claude + l'ancre `RootWebArea`
  existent et que les ancres attendues sont présentes ; sinon → résultat vide/`Unknown`, JAMAIS d'exception.
- Aucune source indisponible ne crashe (app Claude fermée = liste bureau vide, pas d'erreur).
- Matching **souple** : insensible à la casse, tolérant aux espaces, table fr/en extensible ; ne jamais
  coder en dur un `AutomationId` `base-ui-_r_XXX_`.
</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/Chronos/Services/SessionSnapshot.cs` — record neutre `{SessionId, Project, Activity, Reason, UpdatedAt}`
  + enum `SessionActivity {Working, WaitingAttention, WaitingTurn, Unknown}`. **À étendre** (Kind, Origin).
- `src/Chronos/Services/SessionMonitor.cs` — `Read(now)` fusionne `TranscriptSessionSource` (base) + fichiers
  hooks (surcharge par session_id) + filtre `archived` (`ArchiveStore`). **Point d'insertion de la source bureau.**
- `src/Chronos/Services/TranscriptSessionSource.cs` — `Read(now)` → `IReadOnlyList<SessionSnapshot>`,
  fenêtre d'activité 15 min. Modèle à imiter pour la nouvelle source.
- `src/Chronos/Services/ArchiveStore.cs` — magasin JSON `%APPDATA%\Chronos\archived.json` (map id→ts, TTL,
  écriture atomique tmp+move). **Patron à réutiliser pour le magasin « traitées » de la Phase 14.**
- `src/Chronos/ViewModels/SessionsViewModel.cs` — `Refresh(now)` (trie attente-d'abord, timer 2 s),
  `Describe(activity)`→(texte, brush, isWaiting). **À enrichir** pour afficher le type (Chat/Code/Cowork).
- `src/Chronos/Views/SessionsController.cs`, `SessionsWindow.xaml(.cs)` — panneau flottant (pastilles).

### Established Patterns
- Services NEUTRES (aucun type WPF), lecture TOLÉRANTE (try/catch → ignore, jamais d'exception qui remonte).
- Chemins via `Environment.GetFolderPath` (jamais `Assembly.Location`, mono-fichier).
- DI dans `src/Chronos/App.xaml.cs` `ConfigureServices` : `SessionMonitor`/`ArchiveStore`/`SessionsController`
  déjà enregistrés en Singleton — **y enregistrer** `IUiaTreeProvider` + `DesktopUiaSessionSource` et les
  injecter dans `SessionMonitor`.
- Tests : `tests/Chronos.Tests/SessionsTests.cs` (xUnit). Nouveaux tests via **faux arbre UIA**.

### Integration Points
- `SessionMonitor` ctor (App.xaml.cs:177) : ajouter la source bureau à la composition.
- `SessionsViewModel.Describe`/pastille : ajouter le libellé de type.
</code_context>

<specifics>
## Specific Ideas

- **Snapshot état REPOS manquant** : le spike a été pris pendant une génération. En tout début de phase,
  capturer un dump UIA de la fenêtre Claude à l'état repos (via un petit script PowerShell UIAutomation,
  cf. scratchpad `uia-spike.ps1`) pour confirmer la représentation exacte du bouton d'envoi / disparition
  de « Claude répond. ». Si l'app est occupée, s'appuyer sur le matching tolérant (table fr/en) + dégradation.
- Signaux/exemples réels consignés dans la mémoire `chronos-desktop-uia.md`.
</specifics>

<deferred>
## Deferred Ideas

- Notifications Windows (`UserNotificationListener`) comme signal « vient de finir » — milestone suivant.
- Décompte d'usage par session / ventilation Chat vs Code vs Cowork (lisible dans l'arbre : `Usage: …`).
- Auto-disparition des sessions traitées — Phase 14 (dépend de cette source pour la branche focus).
</deferred>
