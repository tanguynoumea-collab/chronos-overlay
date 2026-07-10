# Phase 14 : Auto-disparition hystérésis des sessions traitées - Context

**Gathered:** 2026-07-10
**Status:** Ready for planning
**Mode:** Contexte verrouillé (règle d'hystérésis décidée avec l'utilisateur) — pas de grey areas ouverts.

<domain>
## Phase Boundary

Faire **disparaître automatiquement** de la liste les sessions « traitées », selon une règle d'hystérésis
**réversible**. Un magasin « traitées » auto-géré retire une session (a) dès qu'elle est **répondue**
(retour en Working) ou (b) **acquittée** (focus premier plan de l'app ≥ ~2-3 s), et la fait **réapparaître**
si un événement d'attente **plus récent** survient. L'archivage manuel clic-droit (`ArchiveStore`) reste
**distinct et permanent**. S'appuie sur la source UIA de la Phase 13 (clés `desktop:...`, focus premier plan).

**Hors périmètre :** toute nouvelle source de détection (faite en Phase 13) ; notifications Windows.
</domain>

<decisions>
## Implementation Decisions

### Magasin « traitées » (`TreatedStore`) — calqué sur `ArchiveStore`, mais auto-géré et RÉVERSIBLE
- Fichier `%APPDATA%\Chronos\treated.json` : map `SessionId → treatedWaitingTs` (ms) — l'horodatage de
  l'**événement d'attente** qui a été traité (= `UpdatedAt` du snapshot en attente au moment du traitement).
- Écriture atomique (tmp + move), lecture tolérante (absent/corrompu → vide), purge TTL (même ordre de
  grandeur qu'`ArchiveStore`, ~6 h — borne la croissance ; une entrée disparaît une fois la session inactive).
- **Distinct d'`ArchiveStore`** : archivé = permanent (ne réapparaît jamais, NET-04) ; traité = réversible.

### Détection du traitement (`SessionTreatmentTracker`) — STATEFUL, logique pure testable
Composant à état, invoqué à chaque cycle avec les snapshots BRUTS (avant filtre) + info de focus + horloge :
- Mémorise la dernière activité vue par `SessionId`.
- **NET-01 (répondu)** : si une session était en attente (`WaitingTurn`/`WaitingAttention`) au cycle
  précédent et n'est plus en attente maintenant (Working, ou disparue puis revenue non-attente) →
  enregistrer `treated[SessionId] = dernierWaitingTs`.
- **NET-02 (acquitté par focus)** : si une session bureau est **actuellement en attente** ET que la
  fenêtre Claude est au **premier plan de l'OS** (focus) montrant cette session, de façon continue depuis
  **≥ ~2,5 s** (debounce anti-survol) → enregistrer `treated[SessionId] = waitingTs courant`.
  Le focus premier-plan vient de la source UIA Phase 13 (session foreground) + état focus fenêtre.

### Filtrage (dans `SessionMonitor.Read`, au MÊME endroit que le filtre `archived`)
- Ordre par cycle : (1) fusion des snapshots bruts (transcripts + hooks + bureau) ; (2) `tracker.Observe(bruts, focus, now)`
  met à jour `TreatedStore` ; (3) filtre `archived` **puis** `treated` ; (4) retour.
- Règle de masquage : une session `s` est **cachée** si `treated` contient `s.SessionId` avec
  `treatedWaitingTs >= s.UpdatedAt` (l'événement d'attente courant a déjà été traité) OU si `s` n'est plus
  en attente (traitée, pas encore ré-attendue).
- **Réapparition (NET-03)** : si `s` est en attente avec `s.UpdatedAt > treatedWaitingTs` (événement
  d'attente PLUS RÉCENT) → la session réapparaît et l'entrée `treated` est purgée.

### Honnêteté / robustesse
- Le focus/premier-plan est une observation UIA best-effort ; si indisponible, la branche NET-02 ne
  déclenche simplement pas (pas d'erreur, pas de faux traitement). NET-01 fonctionne sans UIA (transcripts).
- Aucune source ≠ crash ; tout en try/catch tolérant, cohérent avec le reste du projet.
- Debounce ~2,5 s : distinguer un vrai acquittement d'un survol/passage rapide.
</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/Chronos/Services/ArchiveStore.cs` — **patron direct** du `TreatedStore` (map id→ts, TTL, tmp+move,
  tolérance totale). Copier la structure, ajouter la sémantique réversible.
- `src/Chronos/Services/SessionMonitor.cs` — `Read(now)` : le filtre `archived` (lignes ~69-71) est le
  **point d'insertion** du filtre `treated`. Le tracker s'invoque juste avant, sur les snapshots fusionnés.
- `src/Chronos/Services/SessionSnapshot.cs` — `Activity` (Working/WaitingAttention/WaitingTurn/Unknown),
  `UpdatedAt`, `Origin` (Cli/Desktop, Phase 13), `Kind` (Phase 13). « En attente » = WaitingTurn|WaitingAttention.
- **Phase 13** : `DesktopUiaSessionSource` (source bureau + cache), clés `desktop:<kind>:<nom>` /
  `desktop:foreground:<kind>`. Exposer depuis la source (ou son poll) l'info « quelle session bureau est au
  premier plan + la fenêtre Claude a-t-elle le focus OS » pour la branche NET-02.
- `src/Chronos/ViewModels/SessionsViewModel.cs` — `Refresh(now)` (timer 2 s) : reste le déclencheur ;
  aucun changement de contrat, la disparition est gérée en amont (SessionMonitor + tracker + store).

### Established Patterns
- Services NEUTRES, lecture tolérante, chemins `%APPDATA%` via `Environment.GetFolderPath`.
- DI dans `App.xaml.cs` : enregistrer `TreatedStore` + `SessionTreatmentTracker` (Singleton) et les injecter
  dans `SessionMonitor` (à côté d'`ArchiveStore`).
- Tests xUnit : la logique tracker+filtre se teste avec des séquences de snapshots synthétiques + faux focus
  + horloge injectable — aucune fenêtre Claude réelle requise.

### Integration Points
- `SessionMonitor` ctor (`App.xaml.cs`) : ajouter `TreatedStore` + `SessionTreatmentTracker`.
- Focus premier-plan : depuis la source UIA Phase 13 (session foreground) — l'exposer si pas déjà fait.
</code_context>

<specifics>
## Specific Ideas

- L'archivage manuel (`ArchiveStore`, clic droit) NE CHANGE PAS et reste prioritaire/permanent (NET-04).
- Le débounce (~2,5 s) et le TTL (~6 h) sont des constantes ajustables en un point (comme les seuils existants).
</specifics>

<deferred>
## Deferred Ideas

- Un réglage utilisateur pour choisir la règle (répondu-seul / focus-seul / hystérésis) — non demandé, la
  règle hystérésis est fixée. Différé.
- Notification Windows — milestone suivant.
</deferred>
