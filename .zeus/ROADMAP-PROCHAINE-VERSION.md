# Roadmap — version suivante (après 3.4.0)

Demandes de l'utilisateur du 2026-10-01. **Consignées seulement : aucun code n'est commencé.**
Elles entreront dans le prochain cycle ZEUS (ROADMAP → … → GSD). L'ordre de la liste suit celui de la demande, pas
celui de l'exécution.

État relevé dans le code le 2026-10-01, pour cadrer :
- `CadranStyle { Arcs, Braises, Fusible, Maree, Volets }` (`Services/ChronosSettings.cs`).
- Fusible (`Views/Cadrans/CadranFusibleView.xaml`, largeur 210) et Volets (`CadranVoletsView.xaml`, largeur 234)
  n'existent qu'**à l'horizontale** ; Marée (`CadranMareeView.xaml`, colonnes 42 × 120) n'existe qu'**à la verticale**.
- `VerticalLayout` existe déjà, mais il ne concerne que le widget des sessions, pas les cadrans.
- Historique : `HistoriqueStyleSemaine { Pistes, Simplifie, Tuiles }`.

---

## R1 — Fusible, Marée et Volets : orientations horizontale ET verticale

- Ajouter l'orientation manquante à chacun de ces trois cadrans : Fusible vertical, Volets vertical, Marée horizontale.
- Rendre l'orientation sélectionnable dans les réglages (section Apparence), avec un aperçu vivant. Elle ne s'affiche
  que pour les cadrans qui la gèrent.
- La loi d'encodage reste la même dans les deux sens (temps = géométrie, quota = luminance/couleur ; voir
  [[chronos-refonte-visuelle-shortlist]]).
- À trancher au design : le réglage d'orientation est-il propre au cadran ou partagé avec `VerticalLayout` (widget de
  sessions) ?

## R2 — Fusible et Volets : +20 % de taille

- Agrandir de 20 % **toutes les variantes** de Fusible et de Volets (horizontales et verticales, modes Normal et
  Étendu).
- Passer par `Resources/DesignTokens.xaml` (aucune taille codée en dur). Vérifier les marges de l'overlay et
  l'accroche aux coins de l'écran après l'agrandissement.

## R3 — Braises : délimitations des tranches de 5 h (défaut de conception)

- Constat : contrairement au cadran Anneaux (Arcs), la section « tranches de 5 h » de Braises n'a aucune délimitation.
  En le regardant, on ne sait pas quand la fenêtre de 5 h se réinitialise.
- Ajouter des repères de délimitation équivalents aux sous-tirets des anneaux, alignés sur la grille des resets de
  5 h (voir [[chronos-cadran-deux-modes]]). Leur style doit être propre à Braises, pas une copie des tirets d'Arcs.

## R4 — Historique : Pistes devient le seul style, et ajout d'un plein écran

- **Ne garder que le style Pistes.** Retirer Simplifié et Tuiles : vues, rendu (`Rendering/Historique/Tuiles5h.cs`…),
  sélecteurs (fenêtre Historique et carte des réglages), énumération `HistoriqueStyleSemaine` et tests associés.
  Les réglages déjà enregistrés avec Simplifié ou Tuiles doivent se lire sans erreur et retomber sur Pistes.
- **Mode plein écran général** : un seul plein écran, qui affiche tous les graphiques. Les graphiques s'agrandissent
  pour qu'on lise mieux les données. Pas de plein écran graphique par graphique. Avec une sortie évidente (Échap et un
  bouton).
- Les règles d'honnêteté restent valables à toutes les tailles : les trous restent des trous, rien n'est projeté et
  les libellés sont conservés.

## R5 — Purge des reliquats de récupération des données + méthodologie claire

- Contexte : la méthode de récupération des données a beaucoup évolué au fil des versions (estimation JSONL, inférence
  de fenêtre, calibration, correction par delta, statusline, en-têtes de limite, endpoint OAuth, UIA, etc.). Elle
  fonctionne enfin.
- **Supprimer tout ce qui ne sert plus** : les fournisseurs, services, réglages, entrées de diagnostic, tests et
  sections de documentation qui viennent des méthodes abandonnées ou incomplètes.
- **Livrable documentaire** : une méthodologie unique et lisible (`docs/data-sources.md` réécrit, ou un document
  dédié) qui décrit la chaîne actuelle, de la source jusqu'au cadran : sources, ordre de priorité, repli, ce qui est
  exact et ce qui ne l'est pas.
- Objectif : que la chaîne interne de récupération reste compréhensible pour la diffusion publique et les futures
  mises à jour.
- Méthode suggérée : commencer par un inventaire en lecture seule (dev-senior ou rôle `pertinence` du dev-council),
  puis faire valider la liste des suppressions par l'utilisateur **avant** de supprimer quoi que ce soit. Les gardes
  de doctrine (exact ou rien, aucune estimation présentée comme exacte) doivent rester vertes.

## R6 — Thèmes rangés en trois catégories : Pâle / Classique / Vive

- Dans la section Thème des réglages, regrouper les thèmes existants sous trois catégories : **Pâle**, **Classique**
  et **Vive**.
- **Proposer de nouvelles combinaisons de couleurs** dans chacune des trois catégories. Elles seront présentées à
  l'utilisateur pour validation avant d'être intégrées.
- Vérifier la lisibilité des rampes de quota (vert → rouge, gris = épuisé) pour chaque thème, dans les deux modes du
  cadran.

## R7 — Zones de clic et d'attrape recalculées pour chaque cadran

- Constat : sur la plupart des cadrans, la zone du clic qui bascule entre pourcentage et heure, ainsi que la zone du
  clic droit qui ouvre le menu, ne correspond pas au visuel.
- Recalculer **pour chaque cadran** (Arcs, Braises, Fusible, Marée, Volets), **dans chaque orientation** (voir R1) et
  **à chaque taille** (voir R2) :
  - la zone de clic (bascule % / heure, double-clic au centre pour l'Historique, clic droit) ;
  - la zone d'attrape qui sert à déplacer le cadran.
- Rappel technique : une zone transparente n'est cliquable que si son `Background` n'est pas nul (`#01000000`).
  Le double-clic au centre ne doit pas déclencher la bascule du simple clic (ACC-02).
- À faire **après** R1 et R2, puisque la géométrie change.

---

**Dépendances** : R1 et R2 → R7. R3, R4, R5 et R6 sont indépendants entre eux.
**Impact UI** : oui (R1, R2, R3, R4, R6, R7). Un passage par DAEDALUS en mode ÉVOLUTION est donc nécessaire avant le
XAML.
