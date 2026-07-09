# Phase 12: Refonte du cadran (3 anneaux, remplissage, compacité) - Context

**Gathered:** 2026-07-09
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped ; design tranché avec l'utilisateur)

<domain>
## Phase Boundary

Refonte purement visuelle du cadran : arcs qui se remplissent vers le reset, 3 anneaux réordonnés,
nouvel anneau timeline 24 h, % d'utilization au centre, overlay compact ~170 px. Sources inchangées.

Requirements couverts : VIS-01, VIS-02, VIS-05, JOUR-01, JOUR-02, JOUR-03, TAILLE-01.
</domain>

<decisions>
## Implementation Decisions

### Sens de remplissage (VIS-01 — verrouillé par l'utilisateur)
- Les arcs se REMPLISSENT à l'approche du reset : longueur = fraction ÉCOULÉE = 1 − FractionRemaining.
  Vide en début de fenêtre, plein au reset. Exposer une propriété `FractionElapsed` (0..1) sur
  WindowGaugeViewModel (dérivée de FractionRemaining) et binder le RingArc dessus. Ne PAS casser
  FractionRemaining (peut rester, utilisé par le calcul). Clamp [0..1].

### Ordre des anneaux (VIS-02 — verrouillé)
Du centre vers l'extérieur : **hebdomadaire** (rayon le plus petit) → **5 h courant** (milieu) →
**timeline 24 h** (rayon le plus grand). C'est une INVERSION de l'ordre actuel (aujourd'hui 5h extérieur,
hebdo intérieur) + ajout du 24h en plus externe. Ajuster les rayons/épaisseurs des RingArc en conséquence.

### Anneau timeline 24 h (JOUR-01/02/03 — verrouillé, design confirmé « Timeline du jour + resets à venir »)
- Cercle complet = 24 h. Se remplit selon l'heure LOCALE du jour : fraction = (minutes depuis minuit local)
  / 1440. À 18 h → 0.75. Angle de départ au sommet (12 h horaire = minuit, sens horaire), cohérent avec
  les autres anneaux.
- Graduations toutes les 5 h : positions des resets de la fenêtre 5 h projetés sur l'axe 24 h. À partir du
  resets_at 5 h courant, les resets sont resets_at + k×5h ; mapper l'heure-du-jour de chaque reset visible
  sur son angle (fraction = minutes_depuis_minuit(reset) / 1440). Un TickRing paramétré par une LISTE
  d'angles (pas des ticks réguliers) — nouveau paramètre ou nouveau contrôle léger.
- Couleur de l'anneau 24 h = utilization de la fenêtre 5 h courante (même UtilizationToBrushConverter).
  Fonction pure « heure locale → fraction 0..1 » et « resets_at 5h + maintenant → liste d'angles de ticks »,
  testables avec un now injecté (FakeClock).

### % d'utilization au centre (VIS-05 — verrouillé)
- À côté du countdown de chaque fenêtre : ex. « 50 min · 80 % » (5h), « 1 j 13 h · 93 % » (hebdo).
- Source exacte → « 80 % » ; source estimée → « ~80 % » (marquage honnête) ; utilization null (pas de
  plafond) → PAS de %, juste le countdown (comportement honnête inchangé). Exposer `UtilizationText`
  sur WindowGaugeViewModel (format fr, arrondi entier), et composer avec CountdownText dans le XAML.
- Le badge « estimée » séparé et le texte tokens (v1.1) restent, mais le « ~ » du % porte déjà l'info :
  garder cohérent, ne pas dupliquer lourdement (discrétion : simplifier si redondant).

### Compacité (TAILLE-01 — verrouillé ~170 px)
- Fenêtre 220→~170 px ; mettre à l'échelle proportionnellement l'Ellipse de fond, les rayons/épaisseurs
  des 3 anneaux, les tailles de police du centre. Vérifier qu'aucun chevauchement (3 anneaux + 2 lignes
  de texte avec %). Le placement/snap (pixels physiques) et le drag doivent continuer à fonctionner avec
  la nouvelle taille (la logique de snap est basée sur le Rect fenêtre réel → OK).

### Claude's Discretion
Rayons/épaisseurs exacts des 3 anneaux à 170 px, tailles de police, gap entre anneaux, style des
graduations 24h, format exact du séparateur « · », nom des nouvelles propriétés/contrôles.
</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- RingArc (Shape, DP Fraction 0..1, StartAngle, Radius, Thickness) — réutiliser pour les 3 anneaux ;
  le binder sur FractionElapsed au lieu de FractionRemaining.
- ArcGeometry (pur : PointAt, Build) — la géométrie ne change pas, seul le paramètre Fraction change.
- TickRing (GeometryGroup, ticks réguliers) — à ÉTENDRE ou dupliquer pour des ticks à angles ARBITRAIRES
  (les resets 5h ne tombent pas à intervalles réguliers sur les 24h... en fait si : tous les 5h = tous les
  75° sur 24h. Mais l'OFFSET dépend du resets_at courant → ticks réguliers avec un décalage de phase).
- RampColor / UtilizationToBrushConverter — réutilisés tels quels pour la couleur du 24h.
- WindowGaugeViewModel — ajouter FractionElapsed + UtilizationText ; MainViewModel — exposer les données
  du timeline 24h (une petite propriété/VM : fraction du jour + liste d'angles de ticks + couleur 5h).
- MainWindow.xaml / DesignTokens.xaml — recomposer le cadran (3 anneaux réordonnés + centre + resize).
- 188 tests verts — ne rien casser (le pipeline données est intact).

### Established Patterns
- TDD sur la logique pure (fraction jour, angles ticks, FractionElapsed, UtilizationText).
- Tests géométrie en [WpfFact] si besoin. Commentaires français. Tokens de design existants.

### Integration Points
- MainWindow.xaml (recomposition), WindowGaugeViewModel (props), MainViewModel (données timeline 24h),
  éventuellement un DayTimelineViewModel léger ou des propriétés calculées.
</code_context>

<specifics>
## Specific Ideas

- Le tick « toutes les 5h » sur 24h = un tick tous les 360°×(5/24) = 75°, avec un offset de phase = angle
  du prochain reset 5h. Donc TickRing peut rester « réguliers » mais avec un StartOffset paramétrable →
  extension minime plutôt qu'un nouveau contrôle. (À confirmer par le planner/recherche.)
- Vérifier la lisibilité à 170px : 2 lignes de texte (5h : countdown+%, hebdo : countdown+%) au centre,
  police réduite. Si trop chargé, la discrétion permet d'empiler proprement.
- Le sens horaire et l'angle de départ (sommet = 12h) doivent être cohérents entre les 3 anneaux et
  l'horloge 24h pour une lecture intuitive.
</specifics>

<deferred>
## Deferred Ideas

- Taille réglable (v2-06). Sous-fenêtres opus/sonnet (v1.3+). Survol/tooltip (V2).
</deferred>
