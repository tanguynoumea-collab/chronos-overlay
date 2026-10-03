# Phase 41: Braises - Context

**Gathered:** 2026-10-03 · **Status:** Ready for planning · **Source:** plan de design VALIDÉ `.zeus/DESIGN_PLAN_CYCLE2.md` § 3

<decisions>
## Implementation Decisions
- L'anneau 5 h de Braises **est** la fenêtre de 5 h (pas de grille 24 h). Allumées = temps restant, départ midi, sens horaire
  (`EmberRingControl.cs:72-85`) : la dernière braise allumée recule vers midi ; le reset tombe toujours à midi.
- **20 braises de 15 min en 5 groupes de 4** : chaque groupe couvre 72°, pas de 13,2° entre braises d'un groupe (groupe centré
  dans son secteur), pastilles de rayon 4,0 à R66 ; le vide entre groupes est la délimitation (pas de tiret). Paramètres
  ajoutés à `EmberRingControl` (ex. `GroupSize`), l'anneau hebdo (12 braises, R44) reste inchangé.
- **Flèche fixe à midi**, extérieure : triangle 10 × 7 pointe vers l'anneau, filet de 12 px, couleur `TickReset` du thème (phase
  39), sans animation. Elle reste dans l'empreinte 170 × 170.
- Mode temps : 3e ligne au centre « ↻ HH:MM » (10,5, `TexteSecondaire`), heure LOCALE du reset 5 h issue de `resets_at`
  (exact) ; **absente si `resets_at` est inconnu** (exact ou rien). Mode pourcentages inchangé. Exposer l'heure du reset
  depuis `WindowGaugeViewModel` (aujourd'hui `ResetsAt` est privé) — texte formaté fr-FR, horloge injectée pour les tests.
- États : « en attente » garde 20 braises neutres ; « plancher » garde le contour pointillé.

### Claude's Discretion
Nom des propriétés ; formatage exact de l'heure (HH:mm).
</decisions>

<canonical_refs>
- `.zeus/DESIGN_PLAN_CYCLE2.md` § 3 ; maquette § 3 (rendu de référence) ; `.zeus/reports/cycle2/inventaire-cadrans-gestes.md` § 2
</canonical_refs>

<deferred>Anneau hebdo 12 braises / 7 jours ; « ↻ HH:MM » sur les autres cadrans.</deferred>
