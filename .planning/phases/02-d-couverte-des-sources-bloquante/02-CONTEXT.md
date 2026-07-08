# Phase 2: Découverte des sources (bloquante) - Context

**Gathered:** 2026-07-08
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

La méthode d'obtention de l'objet d'usage Claude Code (five_hour/seven_day : utilization + resets_at)
est établie EMPIRIQUEMENT et documentée dans docs/data-sources.md — préalable strict à tout code de provider (Phase 3).

Requirement couvert : DAT-01.

Livrable : docs/data-sources.md avec (1) localisation de l'objet d'usage, (2) échantillon réel capturé,
(3) schéma des champs, (4) structure des transcripts JSONL (repli), (5) hypothèses et points de fragilité.
</domain>

<decisions>
## Implementation Decisions

### Cibles d'investigation (ordre de priorité)
- %USERPROFILE%\.claude\ : inventorier les fichiers (settings, state, caches, *.json) susceptibles de contenir five_hour/seven_day, utilization, resets_at.
- Chercher les chaînes "five_hour", "seven_day", "utilization", "resets_at", "usage" dans les fichiers texte/JSON sous ~/.claude (hors binaires), y compris ~/.claude.json s'il existe.
- Transcripts JSONL : %USERPROFILE%\.claude\projects\**\*.jsonl — la commande /usage étant alimentée par une API, il est possible que des réponses d'usage soient loggées dans les transcripts ou ailleurs.
- Statsig/telemetry caches éventuels sous ~/.claude (souvent porteurs d'état de compte).
- Si l'objet n'est trouvable dans AUCUN fichier local : documenter les alternatives (invocation CLI `claude /usage` scriptée ? endpoint API OAuth local ?) avec faisabilité, et déclarer le repli JSONL comme source primaire de fait — la Phase 3 s'adapte via CompositeUsageProvider sans changer l'UI.

### Contraintes d'honnêteté
- Chaque source documentée reçoit un niveau de fiabilité (SourceReliability : Fiable / Estimé).
- Documenter la fréquence de mise à jour observée de chaque source (à quel moment le fichier bouge).
- Documenter le format exact des timestamps (epoch ? ISO ? timezone ?) avec échantillon réel anonymisé.

### Structure JSONL (repli) à documenter précisément
- Champs de tokens par message (usage.input_tokens, output_tokens, cache_creation_input_tokens, cache_read_input_tokens, etc.).
- Identification des blocs Task (tool_use) pour la future bande sous-agents (V2-01).
- Taille typique des fichiers et implications perf (streaming, FileShare.ReadWrite).

### Claude's Discretion
Méthodologie d'investigation libre. Le livrable est de la DOCUMENTATION, pas du code —
aucun provider ne doit être codé dans cette phase.
</decisions>

<code_context>
## Existing Code Insights

Phase 1 livrée : solution Chronos buildable (src/Chronos, tests/Chronos.Tests), fenêtre overlay
fonctionnelle, IUiDispatcher posé. Aucun code de données encore — c'est voulu.
La machine hôte a Claude Code installé et utilisé activement : ~/.claude existe avec des projets
et transcripts réels à examiner (session en cours incluse).
</code_context>

<specifics>
## Specific Ideas

- L'objet d'usage recherché alimente la commande /usage de Claude Code : champs five_hour.utilization,
  five_hour.resets_at, seven_day.utilization, seven_day.resets_at.
- Le pool est partagé au niveau du compte (Code + chat + Cowork) : l'objet inclut déjà Cowork.
- Le reset hebdo dérive (~72 h, ancrage non documenté) : documenter resets_at tel que fourni.
- Ne lire QUE sous le profil utilisateur. Ne modifier AUCUN fichier de ~/.claude (lecture seule stricte).
- Anonymiser les échantillons capturés dans docs/data-sources.md (pas de contenu de conversation, pas d'identifiants de compte).
</specifics>

<deferred>
## Deferred Ideas

None — discuss phase skipped.
</deferred>
