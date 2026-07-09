# Phase 8: Inférence des fenêtres + estimation depuis JSONL - Context

**Gathered:** 2026-07-09
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

En repli JSONL, les arcs retrouvent une longueur (reset 5 h inféré de l'activité) et — si un plafond
est défini — une couleur (utilization estimée), le tout badgé « estimée ». Contrat providers nettoyé.

Requirements couverts : EST-01..05, NET-01.
</domain>

<decisions>
## Implementation Decisions

### Motivation (contexte utilisateur — verrouillé)
L'utilisateur travaille EXCLUSIVEMENT dans l'app bureau Claude (Code desktop + Cowork) : la statusline
ne se rend jamais → usage.json restera à null chez lui. Les JSONL (~/.claude/projects) couvrent en
revanche tout son usage réel (vérifié empiriquement : les sessions bureau y écrivent). v1.1 rend le
repli utile SANS trahir l'honnêteté.

### Inférence de la fenêtre 5 h (EST-01, EST-02 — verrouillé)
- La fenêtre 5 h glissante démarre au premier message suivant un trou d'inactivité ≥ 5 h.
- Algorithme : parcourir les timestamps des entrées JSONL (déjà lus par JsonlEstimationProvider),
  trouver le début de la fenêtre courante = le plus ancien message M tel qu'il n'existe AUCUN trou ≥ 5 h
  entre M et maintenant, en remontant depuis le message le plus récent. resets_at estimé = début + 5 h.
- Si resets_at estimé < maintenant (fenêtre expirée) ou aucune activité < 5 h : fenêtre inactive →
  fraction de temps = 1 (arc plein, rien d'entamé), tokens de fenêtre = 0, utilization = 0 si plafond
  défini sinon null. JAMAIS un arc vide par défaut.
- La somme de tokens 5 h ne compte QUE les messages de la fenêtre courante (pas 5 h glissantes brutes).

### Utilization estimée par plafonds (EST-03, EST-04 — verrouillé)
- settings.json : FiveHourTokenBudget (long?), WeeklyTokenBudget (long?) — null par défaut.
- utilization estimée = tokens fenêtre / plafond ; clampée ≥ 0, PAS clampée à 1 (≥ 1 = gris épuisé, déjà géré).
- Sans plafond : utilization = null (couleur neutre, comportement v1.0). Aucune UI de calibration dans
  cette phase (Phase 9) — mais la lecture des settings et la math sont en place et testées.
- Fenêtre hebdo : bornée par WeeklyAnchor si défini (fenêtre [ancre ; ancre+7j] roulante), sinon 7 jours
  glissants pour la somme de tokens ; resets_at hebdo estimé = mécanique WeeklyRecalibration existante (EST-05).

### Nettoyage contrat (NET-01 — verrouillé)
- Retirer l'événement SnapshotChanged de IUsageProvider et des 3 providers (jamais abonné — l'orchestrateur
  expose le sien). Retirer UsageSnapshot.Age (inutilisé, IsStale dérivé de SourceCapturedAt).
- Mettre à jour les tests touchés ; la suite complète doit rester verte (107 attendus, moins ceux retirés/adaptés).

### Honnêteté (transverse — verrouillé)
- Tout ce qui sort de l'inférence reste Reliability=Estimated → badge « estimée » par fenêtre (v1.0).
- Ne jamais présenter un reset inféré comme exact ; ne jamais colorer sans plafond.

### Claude's Discretion
Structure interne de l'algorithme d'inférence (fonction pure recommandée, testable avec FakeClock),
seuil exact du trou (≥ 5 h strict), gestion des timestamps non monotones entre fichiers.
</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- JsonlEstimationProvider (Phase 3) : scanne déjà récursivement les *.jsonl (subagents inclus), lit
  message.usage et les timestamps ISO 8601 — à ENRICHIR, pas réécrire.
- WeeklyRecalibration + ChronosSettings.WeeklyAnchor (Phase 6) : mécanique hebdo existante.
- SettingsService/ChronosSettings : ajouter les deux champs de plafond (record with-friendly).
- IClock/FakeClock : pour tester l'inférence sans horloge réelle.
- WindowState.FractionRemaining(resetsAt, now, len) : consomme un resets_at — l'inférence n'a qu'à le fournir.
- 107 tests verts — ne rien casser.

### Established Patterns
- TDD sur la logique pure ; fixtures TestData/*.jsonl existantes (étendre avec des scénarios de trous d'activité).
- Services neutres sans WPF (garde de pureté).

### Integration Points
- JsonlEstimationProvider → CompositeUsageProvider (rien à changer côté composite).
- ChronosSettings → lecture des plafonds dans le provider (injection du SettingsService ou des valeurs).
</code_context>

<specifics>
## Specific Ideas

- Perf : l'inférence ne doit pas relire tous les JSONL de l'histoire — borner la lecture aux fichiers
  modifiés récemment (mtime < 8 jours) comme le fait déjà le provider pour la somme hebdo.
- Le provider reçoit les settings à CHAQUE GetAsync (Load frais ou injection) pour que la calibration
  Phase 9 s'applique sans redémarrage.
</specifics>

<deferred>
## Deferred Ideas

- UI de calibration et calibration auto (Phase 9 : CAL-01..03, NET-02).
</deferred>
