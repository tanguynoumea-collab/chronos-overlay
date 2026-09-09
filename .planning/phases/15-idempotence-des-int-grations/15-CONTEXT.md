# Phase 15: Idempotence des intégrations - Context

**Gathered:** 2026-09-09
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

L'utilisateur peut installer et réinstaller Chronos autant de fois qu'il veut sans que
`~/.claude/settings.json` n'accumule d'entrées : chaque installation **remplace** l'entrée Chronos
précédente et **purge** les entrées fantômes pointant sur des exes de versions révolues.

Exigences couvertes : PUR-01 (hooks non cumulatifs, repérage sur le marqueur `--hook`),
PUR-02 (statusLine non cumulatif, repérage sur `--statusline`), PUR-03 (purge des fantômes existants).

Hors périmètre : tout le pipeline d'usage (persistance, delta, en-têtes, jeton, doctrine du composite).

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
Tous les choix d'implémentation sont à la discrétion de Claude — la phase de discussion est
désactivée (`workflow.skip_discuss=true`). S'appuyer sur le goal de la ROADMAP, les critères de
succès et les conventions de la base de code.

### Contraintes non négociables (portées par l'utilisateur)
- **Sauvegarder `~/.claude/settings.json` avant toute modification.**
- **Ne jamais toucher aux entrées non-Chronos** (hooks GSD, autres outils) : elles survivent intactes.
- **Un fichier malformé ou illisible ne provoque aucun crash** au démarrage — dégradation silencieuse.
- Repérage par **marqueur d'argument** (`--hook`, `--statusline`), jamais par chemin d'exe : c'est
  précisément le chemin qui change à chaque version et qui a causé le cumul.
- Chemins sous `%USERPROFILE%` / `%APPDATA%` uniquement, aucun droit admin.
- UI et commentaires en français.

</decisions>

<code_context>
## Existing Code Insights

### Fichiers concernés
- `src/Chronos/Services/SessionHookInstaller.cs` — installe les hooks `--hook <Event>` dans
  `~/.claude/settings.json` (5 événements : Notification, Stop, UserPromptSubmit, SessionStart, SessionEnd).
- `src/Chronos/Services/StatusLineInstaller.cs` — installe la commande `statusLine` (`--statusline`),
  mémorise l'éventuelle commande préexistante dans `ChronosSettings.InnerStatusLineCommand`.
- `src/Chronos/Views/SessionsController.cs` et `src/Chronos/Views/StatusLineSetup.cs` — points d'appel.
- Tests existants : `tests/Chronos.Tests/StatusLineInstallerTests.cs`.

### Défaut constaté en production (2026-09-09)
`~/.claude/settings.json` contient **25 hooks Chronos au lieu de 5** — un jeu complet par chemin d'exe
historique (`Chronos-v2.5.exe`, `v2.5.1`, `v2.6`, `v2.8.1`, plus le build Debug du dépôt). Chaque
événement Claude Code lance donc 5 processus dont 4 obsolètes, qui écrivent des états de session
concurrents dans `%APPDATA%\Chronos\sessions`. La commande `statusLine` pointe un chemin `Downloads` en dur.

### Détail de structure
Dans `settings.json`, `hooks.<Event>` est un tableau de **groupes**, chaque groupe portant lui-même un
tableau `hooks` d'entrées `{ type: "command", command: "...", timeout: N }`. Le cumul s'est produit au
niveau des groupes. `SessionHookInstaller.cs:92` sait déjà retirer un événement devenu vide
(`if (kept.Length == 0) hooks.Remove(ev)`) — la logique de désinstallation existe, c'est l'installation
qui ne remplace pas.

</code_context>

<specifics>
## Specific Ideas

Aucune exigence particulière — phase d'infrastructure. Se référer à la description de la phase dans la
ROADMAP et à ses critères de succès.

</specifics>

<deferred>
## Deferred Ideas

Aucune — la phase de discussion a été sautée.

</deferred>
