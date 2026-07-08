# Phase 5: Cadran (RingArc + converters) + câblage View - Context

**Gathered:** 2026-07-08
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss)

<domain>
## Phase Boundary

L'utilisateur voit le cadran complet à deux anneaux refléter en temps réel l'état des quotas,
branché sur le flux de données déjà éprouvé (MainViewModel Phase 4).

Requirements couverts : CAD-01..07, DAT-08, ROB-01.
</domain>

<decisions>
## Implementation Decisions

### Concept visuel (VERROUILLÉ — maquette validée, à respecter à l'identique)
- Cadran circulaire sombre, semi-transparent. Deux arcs concentriques :
  - Arc EXTÉRIEUR = fenêtre 5 h ; arc INTÉRIEUR = fenêtre hebdomadaire.
  - LONGUEUR d'arc = temps restant avant reset (plein en début de fenêtre, se vide à l'approche du reset).
  - COULEUR d'arc = utilization : dégradé progressif vert → ambre → rouge ; GRIS quand utilization ≥ 1 (épuisé).
- Compte à rebours texte au CENTRE (temps avant reset 5 h + temps avant reset hebdo).

### Tokens de design (VERROUILLÉS — valeurs exactes)
- Fond cadran #16151B, rim #2C2B34
- Ticks : mineurs #34333D, majeurs #46454F
- Arc 5 h (piste) #2A2932, arc hebdo (piste) #26252E
- Rampe utilization : vert #7BB13C (0 %) → ambre #EFA23A → rouge #D8503A (~100 %) → gris #5A5960 (épuisé)
- Texte principal #F4F2EC, secondaire #A9A8B2 / #C7C6D0

### Rendu (VERROUILLÉ — recherche + contraintes)
- XAML pur : Path/ArcSegment, AUCUNE dépendance native.
- RingArc : contrôle réutilisable dérivé de Shape (PAS UserControl), DefiningGeometry surchargée, DP avec FrameworkPropertyMetadataOptions.AffectsRender, arc = figure ouverte tracée (StrokeThickness pour l'épaisseur), StrokeStartLineCap/EndLineCap Round.
- Géométrie : repère WPF Y-inversé (point = centre + R·(sin θ, −cos θ)), IsLargeArc = |sweep| > 180°, cas 360° borné (~359.9°), SweepDirection cohérente.
- Pas d'animation continue / blur / shadow (AllowsTransparency = rendu logiciel). Mise à jour par binding sur changements de propriétés uniquement.

### Converters et honnêteté (VERROUILLÉ)
- UtilizationToBrushConverter : interpolation de la rampe (0 → vert, ~0.5-0.6 → ambre, ~1 → rouge), utilization ≥ 1 → gris #5A5960 + mention « quota épuisé » (CAD-05).
- utilization == null (repli JSONL sans plafond) : l'arc ne doit PAS mentir — couleur neutre (piste ou gris doux) + marquage « estimée » (DAT-08). Ne jamais inventer une couleur d'utilization sur une donnée absente.
- DAT-08 : badge/mention « estimée » visible quand SourceReliability == Estimated. Staleness : donnée périmée signalée (texte secondaire).
- ROB-01 : état « données indisponibles » (les deux fenêtres Unavailable) — cadran visible avec pistes vides + texte « données indisponibles », zéro crash.

### Binding (VERROUILLÉ)
- Arc extérieur lié à FiveHour.FractionTimeRemaining (interpolée Phase 4), couleur à FiveHour.Utilization.
- Arc intérieur idem pour SevenDay.
- Countdown central : textes formatés du MainViewModel (CountdownFormatter FR livré Phase 4).
- Aucune logique métier en code-behind ; converters + bindings.

### Claude's Discretion
Dimensions exactes (fenêtre 220×220 posée Phase 1 — ajuster si besoin), épaisseurs d'arcs, tailles de police,
position exacte du badge « estimée », arc de départ (12 h, sens horaire suggéré), détails des graduations.
</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- MainViewModel + WindowGaugeViewModel (Phase 4) : FiveHour/SevenDay avec Utilization (double?), FractionTimeRemaining interpolée, CountdownText FR, Reliability, staleness — TOUT le flux de données est prêt et testé (41 tests verts).
- MainWindow.xaml (Phase 1) : fenêtre 220×220 transparente avec placeholder à remplacer par le cadran.
- Structure Controls/ et Converters/ prévues (dossiers).

### Established Patterns
- TDD sur la logique pure (la géométrie de RingArc et le converter sont testables unitairement).
- Commentaires français, tokens de design en ressources XAML (ResourceDictionary ou Window.Resources).

### Integration Points
- MainWindow.xaml : remplacement du placeholder par le cadran complet.
- Bindings sur le DataContext existant (MainViewModel).
</code_context>

<specifics>
## Specific Ideas

- Skills à activer sur les tâches UI : frontend-design + windows-wpf (mentionné CLAUDE.md).
- Les couleurs interpolées de la rampe doivent passer par les 3 points exacts de la maquette (vert #7BB13C, ambre #EFA23A, rouge #D8503A).
- Countdown central : 5 h en texte principal (#F4F2EC), hebdo en secondaire (#A9A8B2/#C7C6D0).
</specifics>

<deferred>
## Deferred Ideas

- Révélation au survol / tooltip détaillé (V2-02, V2-03).
</deferred>
