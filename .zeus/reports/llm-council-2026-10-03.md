# LLM-COUNCIL — cycle 2 (2026-10-03) : implémenter R1-R7

Brief : `.zeus/reports/cycle2/llm-council-brief.md`. Faits : `.zeus/reports/cycle2/inventaire-*.md`.
Réponses anonymisées : `.zeus/reports/cycle2/llm-council-reponses-anonymes.md`.

## Classement agrégé (5 relecteurs à l'aveugle, position moyenne)
| Rang | Réponse | Membre | Moyenne | Classements |
|---|---|---|---|---|
| 1 | A | Rigoriste du domaine | 1,4 | 1, 1, 2, 1, 2 |
| 2 | D | Red-teamer | 2,6 | 4, 2, 1, 5, 1 |
| 3 | C | Généraliste | 3,6 | 3, 4, 4, 4, 3 |
| 3 | E | Pragmatique | 3,6 | 2, 5, 3, 3, 5 |
| 5 | B | Premiers principes | 3,8 | 5, 3, 5, 2, 4 |

Faits vérifiés par les relecteurs dans le code : `--statusline` traité avant le verrou (`App.xaml.cs:26` / `:84`) ;
`StatusLineBridge.cs:140` écrit `MinimalStatusLine` ; **`InnerStatusLineCommand` vaut `null`** (vue réelle et vue MSIX)
⇒ le pont produit **la barre que l'utilisateur voit dans Claude Code** ; `TransformForUninstall` existe ; reset de
Braises toujours à midi (`EmberRingControl.cs:72-85`) ; `SettingsService.Load()` tout-ou-rien.

## Synthèse du Président (approche arrêtée, sous réserve des décisions utilisateur)

1. **Taille par style** — retirer les Viewbox des styles alternatifs ; empreinte par une fonction pure
   `EmpreinteCadran(style, orientation)` alimentée par des tokens `DesignTokens.xaml`, liée à Width/Height (testable sans
   fenêtre, pas de `SizeToContent`). Arcs et Braises restent 170 × 170. Recalage sur le **coin d'ancrage courant** à
   chaque changement de taille (absent aujourd'hui). **+20 % se mesure sur le rendu actuel** (Fusible ≈ 190 × 92,
   Volets ≈ 190 × 65 ; 1,2 × 210 ferait +60 % à l'écran). La clause « modes Normal et Étendu » de R2 est sans objet
   (ces modes n'existent que pour Arcs).
2. **Orientation** — DP `Orientation` sur `FuseBar`, `TideColumn`, `FlapRow` + géométrie pure par axe ; deux gabarits par
   vue (les étiquettes se placent différemment) ; réglage **par style**, défaut = orientation actuelle ; **pas** partagé
   avec `VerticalLayout`. Le plan DAEDALUS doit montrer que chaque variante reste reconnaissable (Marée horizontale ≠
   Fusible, Fusible vertical ≠ Marée, Volets vertical lisible) — sinon on renonce à la variante (Red-teamer).
3. **Zones R7** — chaque vue déclare ses zones : silhouette `#01000000` (token `ZoneAttrape`) pour le drag et le clic
   droit ; éléments `Geste.Role="Bascule"` (valeurs) ; un répartiteur unique dans `MainWindow` garde `ArbitreClicCentre`.
   Suppression du `CentreHit` 66 px partagé. Tests : `RenderTargetBitmap` (alpha > 0 dans la silhouette, = 0 dehors) +
   `HitTest` de routage + garde statique (aucun élément à geste avec un pinceau nul/Transparent). Corriger les
   commentaires « Transparent suffit ». Pas de test fondé sur `HitTest` seul (faux vert garanti).
4. **Braises** — pas de grille 24 h (erreur de catégorie). Repère de reset **fixe à midi**, propre au cadran, + 5 groupes
   d'une heure séparés par un vide (20 pastilles de 15 min ou 15 de 20 min : arbitrage de design selon la place à R66).
   Exposer `ResetsAt` dans `WindowGaugeViewModel` si l'heure absolue doit s'afficher. Défaut signalé hors périmètre :
   l'hebdo a 12 pastilles pour 7 jours.
5. **Plein écran Historique** — en plein écran, rangées proportionnelles (`150*`, `72*`…, hauteur max par piste),
   défilement coupé, dictionnaire `PleinEcran` (polices, épaisseurs, pointillé) en `DynamicResource` ; pas de Viewbox
   global ; bornes du moniteur courant posées à la main (pas `Maximized` sur `WindowStyle=None`) ; Échap à deux niveaux,
   bouton visible, F11. **Supprimer la propriété `HistoriqueStyleSemaine`** (pas réduire l'enum).
6. **Purge R5** — P0 : `SettingsService` tolérant valeur par valeur + garde générique (tout `--xxx` inconnu → sortie
   silencieuse avant le verrou). Puis, **liste validée par l'utilisateur**, ordre de l'inventaire : diag/docs →
   orphelins → sources mortes (garde 3 → 1 composite) → recalibrage (WeeklyAnchor en lecture seule) → pont.
   **Le pont statusLine affiche aujourd'hui la barre minimale visible dans Claude Code** : son sort est une décision
   utilisateur (garder un relais d'affichage pur sans aucune donnée, ou retirer la barre). Dans tous les cas
   `usage.json`, sa surveillance et le rôle de source disparaissent ; le réconciliateur cesse de repointer vers l'exe si
   la barre est retirée.
7. **Thèmes** — majorité (4/5) : **Pâle = sombre désaturé**, thèmes clairs reportés (variante de `From()`, `TickReset`,
   `HistoGris`, fenêtres secondaires, overlay sur fond d'écran clair). Néon et Aurore : rampe corrigée (alarme lisible).
   Gardes testées pour tous les thèmes : rouge en teinte chaude, contraste du gris épuisé ≥ 3:1 (aujourd'hui 1,7-2,1).
   `Categorie` sur `ChronosTheme`. Palettes validées avant intégration.
8. **Une seule release 3.5.0** (milestone v1.9), phases séquentielles : socle → purge → Historique → thèmes →
   géométrie (R2 + R1) → Braises → zones R7 → release + constat. Plan DAEDALUS unique (R1, R2, R3, R4 plein écran,
   R6 palettes, R7 zones) validé avant tout XAML.

## Notes du conseil
- Accord unanime : retrait des Viewbox, +20 % sur le rendu affiché, orientation par style hors `VerticalLayout`, zones
  peintes en `#01`, Braises sans grille 24 h, suppression de la propriété d'historique, relais `--statusline` le temps
  de la migration.
- Vrai désaccord n° 1 : Pâle = fonds clairs (Premiers principes) contre sombre désaturé (les 4 autres). Je suis la
  majorité, mais la question est posée à l'utilisateur.
- Vrai désaccord n° 2 : deux releases (Red-teamer) contre une. Je garde une release (cadre du cycle), en reprenant son
  ordre (socle et purge d'abord).
- Outrepassé : la « restauration de `InnerStatusLineCommand` » proposée par 4 membres — il vaut `null`, retirer la clé
  supprime la barre visible ; c'est le Red-teamer qui avait raison, et ce point devient une décision utilisateur.
- Objection retenue de Premiers principes : le pont est une source exacte gratuite, morte seulement par un chemin MSIX ;
  conservée comme argument pour la décision utilisateur, pas comme recommandation.
