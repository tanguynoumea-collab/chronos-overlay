---
gsd_state_version: 1.0
milestone: v1.5
milestone_name: — Exactitude permanente (suppression de l'estimation pure, correction par delta)
status: "Milestone v1.5 démarré le 2026-09-09. Définition des exigences."
stopped_at: Milestone v1.5 initialisé
last_updated: "2026-09-09"
last_activity: 2026-09-09 — Milestone v1.5 démarré (diagnostic complet du pipeline d'usage + analyse de claude-session-browser)
progress:
  total_phases: 0
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-09-09)

**Core value:** Voir instantanément, sans terminal ni `/usage`, combien de quota et de temps il reste sur les
deux fenêtres — sans jamais présenter une estimation comme un chiffre exact.
**Current focus:** v1.5 — éradiquer la bascule silencieuse en estimation pure.

## Current Position

Milestone: v1.5 — Exactitude permanente
Phase: Not started (defining requirements)
Plan: —
Status: Defining requirements
Last activity: 2026-09-09 — Milestone v1.5 started

Progress: [░░░░░░░░░░] 0%

## Performance Metrics

**Velocity:**

- Total plans completed (v1.5): 0
- Average duration: —
- Total execution time: 0 h

*Updated after each plan completion*

## Milestone v1.4 (clos)

v1.4 validé en UAT app-réelle le 2026-07-11 (phases 13-14, 5 plans, 327 tests verts). Livré hors GSD depuis :
refonte visuelle des deux overlays + 3 thèmes, cadran deux modes, exe v2.8.1.

## Accumulated Context

### Contexte technique v1.5 (diagnostic DÉJÀ ÉTABLI — ne pas re-enquêter)

Diagnostic mené le 2026-09-09 sur la machine réelle. Cause racine des pourcentages faux après le passage
Max x5 → Max x20 : **les trois sources exactes sont tombées en même temps et le composite dégrade en silence.**

- **Jeton OAuth Chronos expiré le 2026-07-12.** `GET https://api.anthropic.com/api/oauth/usage` → **HTTP 401**
  (vérifié). `ChronosOAuthUsageProvider` rafraîchit paresseusement, l'échec est totalement muet côté UI.
- **`%APPDATA%/Chronos/usage.json` figé au 2026-07-10**, contenu
  `{"five_hour":{"used_percentage":10,"resets_at":9}}` — `resets_at: 9` = epoch 1970. Le pont statusLine n'est
  visiblement jamais invoqué par l'app de bureau. `ClaudeUsageObjectProvider` n'applique **aucune limite d'âge**
  et renvoie ça marqué `Exact`.
- **`CompositeUsageProvider.Best()` classe uniquement par fiabilité** (`Exact > Estimated > Unavailable`) :
  une donnée « exacte » de deux mois bat donc une estimation fraîche.
- **`ChronosOAuthUsageProvider._cached` est un champ d'instance (RAM)** → à chaque démarrage de l'exe, aucun
  chiffre exact en mémoire, bascule immédiate sur l'estimation. Cas le plus fréquent, jamais identifié.
- **`MainViewModel.cs:269` calcule `IsStale`** (seuil 2 min) mais la propriété n'est **bindée nulle part**.
- **`settings.json`** : `FiveHourTokenBudget=230000000` en source `Manual` (donc `BudgetCalibration.ApplyAuto`
  refuse par conception de l'écraser → gelé à vie), `WeeklyTokenBudget=5817635413` calibré sous Max x5.
- **`BudgetAutoCalibrator` a un défaut logique** : il ne calibre que lorsqu'une source *exacte* est présente,
  c'est-à-dire précisément quand l'estimation ne sert pas.
- **`~/.claude/settings.json` contient 25 hooks Chronos au lieu de 5** (v2.5, v2.5.1, v2.6, v2.8.1 + le build
  Debug), aucun installateur ne retire les précédents. Le `statusLine` pointe un chemin `Downloads` en dur.
- **`.credentials.json` de Claude Code ne contient que `mcpOAuth`** sur cette machine (pas de
  `claudeAiOauth.accessToken`) : la lecture de jeton « à la claude-session-browser » n'y marcherait pas telle
  quelle ; il faut passer par le jeton propre de Chronos.

**Technique reprise de `github.com/juppeee/claude-session-browser`** (`clawdmeter.py:150`, `poll_usage_meta`) :

```
POST https://api.anthropic.com/v1/messages
  Authorization: Bearer <token>
  anthropic-beta: oauth-2025-04-20
  User-Agent: claude-code/<version>
  body {"model":"claude-haiku-4-5-...","max_tokens":1,"messages":[{"role":"user","content":"hi"}]}
→ en-têtes anthropic-ratelimit-unified-{5h,7d}-{utilization,reset}, -5h-status, et variante -overage-*
```

Ils récupèrent les en-têtes **même sur un 429** (`except HTTPError: hdrs = e.headers`) — c'est l'avantage
décisif sur `/api/oauth/usage`.

**PIÈGE D'UNITÉ — trois unités pour la même donnée, à normaliser en un seul point :**

| Source | Champ | Unité |
|---|---|---|
| En-têtes `anthropic-ratelimit-unified-*` | `utilization` | **0..1** |
| `GET /api/oauth/usage` | `utilization` | **0..100** |
| Pont statusLine (`rate_limits`) | `used_percentage` | **0..100** (= `utilization*100`, confirmé dans le binaire `claude-2.1.87`) |

`resets_at` : **epoch secondes** pour les en-têtes et le pont statusLine, **ISO 8601** pour `/api/oauth/usage`.

**Fondement de la décision « plus d'estimation absolue »** : les limites Anthropic pondèrent par modèle (une
heure d'Opus ne pèse pas comme une heure de Haiku), donc `tokens / plafond` restera faux même avec le bon
plafond. Les transcripts ne peuvent répondre qu'à deux questions bornées : *activité depuis T ?* et
*tokens depuis T ?* — d'où la correction par delta.


### Decisions

- [v1.4/roadmap]: **2 phases à dépendance forte** — la source UIA (Phase 13) avant l'hystérésis (Phase 14).
  La distinction Chat/Cowork/Code et l'acquittement par focus (NET-02) EXIGENT l'arbre UIA ; la branche
  « répondu » (NET-01) roule déjà sur les sources actuelles. On ne peut donc pas inverser l'ordre.

- [v1.4/roadmap]: Phase 13 = BUR-01..05 + ROB-06 + ROB-07 (7 req) ; Phase 14 = NET-01..04 (4 req).
- [v1.4/roadmap]: Les deux phases sont **UI hint: yes** (surface visuelle WPF du widget sessions).
- [Phase 13]: [13-02] WindowsUiaTreeProvider propage AutomationId (dont l'ancre RootWebArea) → foreground reconnu en PROD, pas seulement en test. Assemblies UIA fournies implicitement par UseWPF (aucun <Reference>, 0 warning). Poll de fond via Timer .NET hors thread UI (ROB-07).
- [Phase 13]: 13-03: source bureau fusionnée dans SessionMonitor.Read (ISessionSource? optionnel, 4e arg non cassant), après transcripts+hooks, avant archived, non bloquant (ROB-07)
- [Phase 13]: 13-03: garde DI réelle (CompositionRootTests) reproduisant la sous-chaîne bureau d'App.xaml.cs → attrape un service manquant/mal ordonné qui ne planterait qu'au démarrage
- [Phase 14]: [14-01] Réversibilité NET-03 portée par le tracker (purge sur nouvel épisode d'attente), PAS par une comparaison ts>=UpdatedAt dans le filtre (fausse pour le bureau: UpdatedAt==now à chaque poll). Horodatage d'épisode maintenu par le tracker.
- [Phase 14]: [14-02] Focus premier-plan OS réel (WindowsForegroundWatch, Win32 GetForegroundWindow, titre « Claude ») injecté 7e param de SessionMonitor => branche NET-02 VIVANTE en prod. Best-effort ne lève jamais ; couche neutre (aucun HWND public). NET-01..04 tous couverts, phase 14 complète, milestone v1.4 prêt pour audit.

### Contexte technique (déjà établi — ne pas re-rechercher)

- **Spike UIA PROUVÉ (2026-07-10)** sur la vraie machine : l'app Electron/Chromium expose son arbre
  d'accessibilité complet. Matcher par **ControlType + Name** (PAS par `AutomationId` volatils) ; Names
  **localisés fr**. Signaux vérifiés :

  - Text « Claude répond. » / Button « Arrêter » = **Working**.
  - Button « Ignorer les permissions » = **attend permission**.
  - Text « Mode chat » + placeholder « Tapez / pour les commandes » = **repos** (attend ton message).
  - Onglets Home/Code + panneaux Terminal/Diff/Aperçu/Actions de session/Contrôle à distance.
  - Sidebar : Button « En cours d'exécution <nom> » par session active.
  - Cowork-VM = état distant **non observable** → indéterminé.
- **MANQUE** : un snapshot en état **REPOS** (le spike a été pris pendant une génération). À capturer en
  tout début de Phase 13 pour figer la représentation exacte du « m'attend » premier plan.

- **Code existant** :
  - `src/Chronos/Services/SessionSnapshot.cs` — record neutre + enum `SessionActivity`
    (Working / WaitingAttention / WaitingTurn / Unknown).

  - `src/Chronos/Services/SessionMonitor.cs` — `Read(now)` fusionne transcripts + hooks, applique la
    staleness, filtre `archived` via `ArchiveStore`. **Le filtre « traitées » (Phase 14) se branche ici,
    au même endroit que le filtre archived.**

  - `src/Chronos/Services/TranscriptSessionSource.cs`, `ArchiveStore`.
  - `src/Chronos/ViewModels/SessionsViewModel.cs` — `Refresh`, timer 2 s.
  - `tests/Chronos.Tests/SessionsTests.cs`.
- **Nouveau (Phase 13)** : `DesktopUiaSessionSource` (nouvelle `ISessionSource`), lecture hors thread UI
  puis marshalling, élément racine mis en cache, cadence ~1-2 s.

- **Contraintes** : `System.Windows.Automation` (interop COM managé, aucune dépendance native de rendu,
  pas d'admin) ; `%USERPROFILE%`/`%APPDATA%` uniquement ; honnêteté (indéterminé, jamais une inférence
  présentée comme exacte) ; lecture tolérante (aucune source ≠ crash) ; UI/commentaires en français.

### Pending Todos

- Phase 13 (tout début) : capturer le snapshot UIA en état **repos** avant de coder le mapping d'états.

### Blockers/Concerns

- Le mapping d'états UIA dépend de Names **localisés** ; prévoir la table fr/en (ROB-06) dès la Phase 13
  pour ne pas coder en dur des libellés qui changent à une MAJ de l'app.

- Débounce du focus (NET-02, Phase 14) : caler ~2-3 s pour distinguer un vrai acquittement d'un survol.

## Session Continuity

Last session: 2026-07-10T13:11:52.267Z
Stopped at: Completed 14-02-PLAN.md
Resume file: None
