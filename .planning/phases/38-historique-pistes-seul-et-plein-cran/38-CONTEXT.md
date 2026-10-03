# Phase 38: Historique — Pistes seul et plein écran - Context

**Gathered:** 2026-10-03 · **Status:** Ready for planning · **Source:** plan de design VALIDÉ `.zeus/DESIGN_PLAN_CYCLE2.md` § 4

<domain>
## Phase Boundary
Suppression des styles Simplifié et Tuiles (Pistes seul), puis plein écran général de la fenêtre Historique.
</domain>

<decisions>
## Implementation Decisions
- Tout l'inventaire de suppression de `.zeus/reports/cycle2/inventaire-themes-historique.md` § B1 s'applique (code, XAML,
  tokens, tests, README § « Trois styles »). Carte des réglages : garder « Aussi : double-clic au centre du cadran » tel quel
  (il sera reformulé en phase 42, quand le geste couvrira la silhouette).
- **Supprimer la propriété** `HistoriqueStyleSemaine` (pas réduire l'enum) ; test : un ancien `settings.json` avec `"Tuiles"`
  se lit sans perte (s'appuie sur la phase 36).
- Plein écran (plan § 4.2, chiffres contractuels) :
  - bouton « ⛶ Plein écran » à droite de la ligne de fraîcheur (à la place du sélecteur), raccourci F11 ;
  - bornes du **moniteur courant** posées à la main (pas `WindowState.Maximized`), en DIP, barre des tâches couverte,
    restauration exacte de la géométrie précédente ; la géométrie enregistrée reste celle du mode normal ;
  - rangées des pistes en hauteurs proportionnelles (Semaine 150*/72*/72*, Jour 190*/64*/64*, 4 semaines Niveau 250*),
    plafonds Niveau 520, Rythme/Tokens 240 ; Couverture et annotations fixes ; plus de défilement ;
  - dictionnaire `PleinEcran` en `DynamicResource` : corps 8,5→11,5, 9→12, 9,5→13, 10,5→14, 11→15, 11,5→15,5, 14→18, 16→21 ;
    libellés 96→128 ; légende de droite 72→96 ; escalier 2,2→3 ; premier plan 2,4→3,2 ; tiret de reset 8→11 ;
  - bouton « ⤢ Quitter le plein écran · Échap » toujours visible ; Échap quitte d'abord le plein écran puis ferme au second
    appui (fonction pure testée) ; F11 bascule.
- Honnêteté inchangée à toutes les tailles (trous, libellés, pied). Valeurs via `DesignTokens.xaml` ; les tests qui figent
  150/72 restent valides en mode normal ; tests dédiés au plein écran.

### Claude's Discretion
Mécanisme de bascule des hauteurs (styles / triggers / converter) ; emplacement de la fonction pure Échap.
</decisions>

<canonical_refs>
- `.zeus/DESIGN_PLAN_CYCLE2.md` § 4 ; `.zeus/DESIGN_PLAN.md` (contrat Historique existant) ; maquette `.zeus/maquettes/cycle2-cadrans-themes.html` § 4
- `.zeus/reports/cycle2/inventaire-themes-historique.md` § B ; `.planning/phases/36-socle/`
</canonical_refs>

<deferred>None</deferred>
