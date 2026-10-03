# Phase 37: Une chaîne de données claire - Context

**Gathered:** 2026-10-03 · **Status:** Ready for planning · **Source:** cycle ZEUS n°2, décisions verrouillées

<domain>
## Phase Boundary
Purge R5 selon la **liste validée par l'utilisateur** (`.zeus/reports/cycle2/liste-purge.md` — DAT-01 déjà satisfait, ne pas
rejouer ce point de contrôle), retrait de la barre statusLine, diagnostic et documentation alignés sur la chaîne réelle.
</domain>

<decisions>
## Implementation Decisions
- Chaîne cible : `LastExactUsageProvider( JournalisationUsageProvider( CompositeUsageProvider(RateLimitHeaderUsageProvider,
  ChronosOAuthUsageProvider) ) )` — un seul composite (la garde `GardesPerimetreTests:627` passe de 3 à 1).
- Liste et ordre EXACTS : ceux de `liste-purge.md` (8 éléments, 5 étapes) ; **un commit réversible par étape**, gardes de
  doctrine vertes après chaque commit. Rien hors de cette liste n'est supprimé (la liste « à ne pas purger » reste).
- Barre statusLine (décision utilisateur : RETIRÉE). Au premier lancement de la 3.5, la réconciliation (`ClaudeSettingsReconciler`)
  **sauvegarde** `~/.claude/settings.json` puis **retire la clé `statusLine` si et seulement si elle pointe sur un exe Chronos
  `--statusline`** ; une barre d'un tiers reste intacte ; idempotent, journalisé, testé sur fichiers témoins. Les hooks restent
  réconciliés (repointés vers l'exe courant). `InnerStatusLineCommand` vaut `null` chez l'utilisateur : rien à restaurer ; si
  une valeur non nulle existait, la restaurer à la place de la barre Chronos (cas testé). Le mode `--statusline`, le pont,
  l'installeur, la proposition au premier lancement, la carte des réglages et `usage.json` (+ surveillance) disparaissent ;
  `--statusline` tombe alors dans la garde d'arguments inconnus de la phase 36 (sortie silencieuse).
- Recalibrage : `WeeklyAnchor` reste **lu** en secours par `BornesPlage` / `HistoriqueViewModel` (aucune écriture nouvelle).
- Diagnostic : nouvelle section « Chaîne de données » (sonde d'en-têtes, secours OAuth Chronos, dernier exact persisté,
  journal) ; retrait des sections pont statusLine, endpoint OAuth (repli), réglage Usage exact (OAuth), Conseil.
- Docs : `docs/data-sources.md` réécrit autour de la chaîne (exact = sonde, secours OAuth, dernier exact frais ou encore
  valide ; non exact = seulement le plancher « ≥ ») ; README « D'où viennent les chiffres » ; `CLAUDE.md` (sections Contexte /
  Sources de données) ; commentaires « repli JSONL / estimation » corrigés. Gardes de documentation existantes vertes ou
  adaptées explicitement.
- Mémoire hors dépôt : ne PAS modifier `~/.claude/projects/.../memory` (l'orchestrateur s'en charge).

### Claude's Discretion
Découpage en plans par étape de la liste ; libellés de la nouvelle section du diagnostic.
</decisions>

<canonical_refs>
- `.zeus/reports/cycle2/liste-purge.md` (VALIDÉE), `.zeus/reports/cycle2/inventaire-purge-donnees.md` (chemin:ligne, tests touchés)
- `.zeus/reports/llm-council-2026-10-03.md` § 6 ; `.zeus/DESIGN_PLAN_CYCLE2.md` § 6
- `.planning/phases/36-socle/` (garde d'arguments — prérequis)
</canonical_refs>

<deferred>None</deferred>
