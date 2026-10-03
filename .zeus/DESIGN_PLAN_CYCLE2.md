# DESIGN_PLAN — Chronos 3.5 (cycle ZEUS n°2, R1 à R7)

Statut : **validé par l'utilisateur le 2026-10-03** (checkpoint humain 1, « Je valide »). Mode DAEDALUS : **ÉVOLUTION**. L'UI existante
est acceptée ; seuls les changements sont décrits ici. Contrats existants conservés : `.zeus/DESIGN_PLAN.md`
(Historique) et `.zeus/DESIGN_PLAN_REGLAGES.md` (réglages), sauf les amendements ci-dessous.
Maquettes à l'échelle réelle : `.zeus/maquettes/cycle2-cadrans-themes.html`.
Approche technique : `.zeus/reports/llm-council-2026-10-03.md`.

Décisions de l'utilisateur déjà prises (2026-10-03) : barre statusLine retirée ; **Pâle = sombres doux** ; **+20 %
mesuré sur le rendu actuel** ; Braises = repère de reset + séparations horaires + heure du reset.

Règle directrice : **cohérence avant nouveauté**. La loi d'encodage reste la même (temps = géométrie, quota =
couleur/luminance, gris = épuisé). L'orange reste réservé aux sessions qui attendent l'utilisateur. Toutes les tailles et
couleurs passent par `Resources/DesignTokens.xaml` ou par les pinceaux du thème.

---

## 1. Cadrans : tailles (R2) et orientations (R1)

### 1.1 Empreintes (DIP, échelle 1, plus de Viewbox)

| Cadran | Orientation | Empreinte | Avant | Remarque |
|---|---|---|---|---|
| Arcs | — | 170 × 170 | 170 × 170 | inchangé |
| Braises | — | 170 × 170 | 170 × 170 (réduit à 0,95) | rendu à l'échelle 1 (le Viewbox réduisait de 5 %) |
| Fusible | horizontal (défaut) | **190 × 92** | ≈ 158 × 77 | +20 % |
| Fusible | vertical | **110 × 190** | — | nouveau |
| Marée | vertical (défaut) | 132 × 160 | ≈ 131 × 158 | inchangé (hors R2) |
| Marée | horizontale | **190 × 96** | — | nouveau |
| Volets | horizontal (défaut) | **190 × 66** | ≈ 158 × 54 | +20 % |
| Volets | vertical | **128 × 190** | — | nouveau |

- Une fonction pure `EmpreinteCadran(style, orientation) → Size` lit ces valeurs dans des tokens (`CadranLargeur*`,
  `CadranHauteur*`). La fenêtre principale lie Width/Height à cette empreinte.
- **Ancrage** : quand l'empreinte change (style, orientation), la fenêtre reste collée à son **coin d'accroche
  courant** et grandit vers l'intérieur de l'écran. Ce recalage est absent aujourd'hui.
- Corps de texte : on ne les réduit pas. Libellés 11, valeurs 12, plaque Volets 13 en horizontal et 14 en vertical.
  Seule la géométrie suit l'échelle.
- La clause « modes Normal et Étendu » de R2 est sans objet : ces modes n'existent que pour Arcs.

### 1.2 Ce qui rend chaque variante reconnaissable (réponse à l'objection du conseil)

| Cadran | Signe propre, gardé dans les deux sens | Horizontal | Vertical |
|---|---|---|---|
| **Fusible** | mèche **fine** (cordon 10 / 8) dans un sillon de 5,4, **étincelle** au front | brûle de gauche à droite, il reste le cordon de droite | **brûle de haut en bas, comme une bougie** : il reste le cordon du bas |
| **Marée** | **large bande** (42 / 32 px) + **ligne d'eau** nette (pointillée si plancher) | la lumière part de la gauche, l'eau avance par la droite ; ligne d'eau légèrement **ondulée** | la lumière part du haut, l'ombre monte par le bas (existant) |
| **Volets** | **plaque chiffrée** colorée par le quota + rangée de **volets** | tuile / plaque / 6 volets en ligne (existant) | deux colonnes : tuile, plaque 56 × 40, puis 6 volets empilés |

Fusible vertical et Marée verticale se distinguent par l'épaisseur (mèche fine contre bande large), par l'étincelle
et par le sens de fonte (le cordon fond vers le bas, la lumière remonte vers le haut).

### 1.3 Réglage d'orientation

- Section **Apparence** des réglages, juste sous les styles de cadran : carte « **Orientation** » avec deux puces
  **Horizontal · Vertical**. Visible seulement pour Fusible, Marée et Volets, comme la carte « Mode étendu » qui
  n'apparaît que pour Arcs.
- Mémorisé **par cadran** : chacun garde son orientation, avec comme défaut son orientation actuelle. Passer de
  Fusible à Marée ne retourne donc pas Marée.
- **Indépendant** de « Disposition verticale » du widget de sessions, qui concerne une autre fenêtre.
- L'aperçu vivant des réglages montre l'empreinte réelle, réduite dans le cadre 144 × 144. C'est le seul endroit qui
  garde un Viewbox.
- La galerie `--cadrans` montre les huit variantes (Arcs compris).

## 2. Zones de clic et d'attrape (R7)

**Un seul geste, sur toute la silhouette du cadran.** Il remplace le disque de 66 px partagé par les cinq styles.

| Geste sur la silhouette | Effet |
|---|---|
| Clic sans bouger | bascule pourcentage ↔ temps |
| Double-clic | ouvre l'Historique, sans déclencher la bascule (`ArbitreClicCentre` inchangé) |
| Appuyer puis glisser au-delà du seuil Windows (`SystemParameters.MinimumHorizontal/VerticalDragDistance`) | déplace le cadran, puis l'accroche au coin le plus proche |
| Clic droit | ouvre les réglages |
| Hors silhouette | le clic traverse vers le bureau |

- **Silhouette** : un disque de rayon 74 pour Arcs et Braises ; pour Fusible, Marée et Volets, dans les deux
  orientations, un rectangle arrondi (rayon 10) qui englobe le cadran avec 6 px de marge.
- Elle est peinte avec un pinceau token `ZoneSilhouette = #01000000`. Sur une fenêtre transparente, c'est la seule façon
  pour qu'un pixel « vide » capte la souris.
- Chaque vue de cadran déclare sa silhouette. `MainWindow` porte un seul répartiteur de gestes.
- Les pastilles d'Arcs (âge du relevé, alertes) gardent leurs propres clics.
- Justification ergonomique (loi de Fitts) : la cible passe d'un disque de 66 px, mal placé pour 4 styles sur 5, à
  tout le cadran. On ne demande plus à l'utilisateur de deviner où est la zone.

## 3. Braises : où s'arrête la fenêtre de 5 h (R3)

- L'anneau extérieur **est** la fenêtre de 5 h. Pas de grille 24 h, qui serait une copie d'Arcs hors sujet.
- **20 braises de 15 min, en 5 groupes de 4.** Les groupes sont séparés par un vide plus large : chaque groupe dure
  72°, avec un pas de 13,2° entre braises d'un même groupe. Pastilles de rayon 4,0 (au lieu de 4,4) pour garder le vide
  lisible à R66. **La délimitation est le vide** : c'est la grammaire des braises, pas un tiret repris d'Arcs.
- **Flèche de braise fixe à midi**, à l'extérieur de l'anneau (triangle 10 × 7 et filet de 12 px), couleur
  `TickReset` du thème. C'est la ligne d'arrivée : le reset a lieu quand la dernière braise l'atteint.
- **Heure du reset** en mode temps : troisième ligne au centre « ↻ 14:20 » (10,5, `TexteSecondaire`), donnée par
  `resets_at`, qui vient du serveur et est donc exacte. En mode pourcentages, le centre ne change pas.
- L'anneau hebdo (12 braises pour 7 jours) ne change pas. Ce défaut est noté pour la roadmap.

## 4. Historique : Pistes seul et plein écran (R4)

### 4.1 Pistes seul
- Retirés : sélecteur « Style : Pistes · Simplifié · Tuiles » (fenêtre et carte des réglages), grilles Simplifié et
  Tuiles, piste Fenêtres 5 h, leurs tokens et leurs textes.
- Carte des réglages : « Aussi : double-clic sur le cadran » reste (le double-clic marche maintenant sur toute la
  silhouette, voir §2).
- La propriété `HistoriqueStyleSemaine` **est supprimée**. Un ancien `settings.json` qui la contient se lit sans
  perte : le membre inconnu est ignoré.

### 4.2 Plein écran
- Bouton **« ⛶ Plein écran »** à droite de la ligne de fraîcheur, à la place du sélecteur de style. Raccourci **F11**.
- La fenêtre couvre **l'écran où elle se trouve** : bornes du moniteur posées à la main, pas `Maximized`, qui déborde
  sur une fenêtre sans bordure. La barre des tâches est couverte. En sortie, la fenêtre retrouve sa position et sa
  taille d'avant.
- En plein écran, les rangées des pistes se partagent la hauteur dans les proportions du mode normal (Niveau 150 ·
  Rythme 72 · Tokens 72). Plafonds : Niveau 520, Rythme et Tokens 240. Couverture et annotations restent fixes, et la
  vue ne défile plus. Vues Jour (190 / 64 / 64) et 4 semaines (Niveau 250) : même règle.
- Dictionnaire `PleinEcran` en `DynamicResource`, environ ×1,35 : corps 8,5 → 11,5, 9 → 12, 9,5 → 13, 10,5 → 14,
  11 → 15, 11,5 → 15,5, 14 → 18, 16 → 21 ; colonne des libellés 96 → 128, légende de droite 72 → 96 ; traits
  escalier 2,2 → 3, premier plan 2,4 → 3,2, tiret de reset 8 → 11.
- **Sortie** : bouton « ⤢ Quitter le plein écran · Échap », toujours visible dans l'en-tête. **Échap** quitte d'abord
  le plein écran, puis un second Échap ferme la fenêtre. F11 bascule.
- Les règles d'honnêteté ne changent pas : trous hachurés, libellés et pied de page identiques à toutes les tailles.

## 5. Thèmes : Pâle · Classique · Vive (R6)

### 5.1 Rangement
La section Thème des réglages devient trois groupes titrés (même style que les titres de section : 10,5 semi-gras,
`Ink2`). Chaque groupe garde la grille de vignettes actuelle (88 × 76).

| Pâle (sombres doux) | Classique | Vive |
|---|---|---|
| Nord, Forêt, Moka, Roseraie, **Sauge**, **Lavande** | Minuit (défaut), Ardoise, Ambre chaud, **Graphite**, **Marine** | **Néon** (rampe corrigée), **Aurore** (rampe corrigée), **Synthwave**, **Lave** |

### 5.2 Nouvelles palettes (disque · piste · graduation · texte · vert · ambre · rouge)
- **Sauge** (Pâle) `#262B28 · #343B37 · #AEB8B0 · #DCE3DD · #8FB996 · #D9C27E · #D08A7E`
- **Lavande** (Pâle) `#22202C · #302D3D · #C3BCD9 · #E6E2F2 · #9FCFB0 · #E8C88E · #E08E9E`
- **Graphite** (Classique) `#121314 · #26282B · #C4C7CC · #F2F3F5 · #6DBE45 · #F0A830 · #E04B3C`
- **Marine** (Classique) `#0F1A2A · #1E2D44 · #B9C9DE · #EAF0F7 · #5DBB7A · #F2B544 · #E25C4F`
- **Synthwave** (Vive) `#140B24 · #2A1745 · #9AE6FF · #F5EEFF · #2BFF88 · #FFD000 · #FF2D55`
- **Lave** (Vive) `#1A0E0A · #33190F · #FFC9A3 · #FFF1E6 · #7CFF4F · #FFB000 · #FF3B1F`
- Corrections : **Néon** ambre `#B14BFF → #FFC23D`, rouge `#FF2E97 → #FF2E63` ; **Aurore** ambre
  `#6D8CF0 → #F0C36D`, rouge `#C86BE0 → #F2577A`. Les décors (disque, piste, graduation, texte) ne changent pas.
- En réserve : Café (Classique), Tropique (Vive).

### 5.3 Lisibilité, pour tous les thèmes
- **Gris « épuisé »** : plus petit mélange piste → graduation qui atteint un **contraste ≥ 3:1 contre le disque**
  (aujourd'hui 1,7 à 2,1 sur tous les thèmes). Calculé dans `From()` et vérifié par un test.
- Rouge de fin de rampe : teinte dans la bande rouge-magenta (335° à 20°) et contraste ≥ 3:1 contre le disque.
  Testé pour chaque thème.
- `TickReset` (tirets de reset, flèche de Braises) entre dans les pinceaux du thème : il est fixe aujourd'hui.
- Les textes et tuiles des cadrans Braises, Fusible, Marée et Volets passent sur les pinceaux du thème
  (`TextePrincipal`, `TexteSecondaire`, fonds de tuile). Aujourd'hui leurs couleurs sont fixes et ces quatre cadrans
  ne suivent pas le thème.
- Hors périmètre : fonds clairs (décision utilisateur) ; les fenêtres Réglages et Historique restent sombres.

## 6. Ce que R5 (purge) change à l'écran

- Réglages, section Données : la carte « Barre de statut de Claude Code » disparaît, ainsi que la carte
  « Recalibrer… » (recalibrage hebdomadaire, devenu inutile).
- Au premier lancement de la 3.5, Chronos **retire sa barre** de `~/.claude/settings.json`, avec une sauvegarde
  préalable. Claude Code n'affiche plus de barre de statut.
- Diagnostic : les sections « pont statusLine » et « endpoint OAuth (repli) » et le « Conseil » trompeur disparaissent.
  Une section « Chaîne de données » décrit les deux sources réelles : la sonde d'en-têtes et le secours OAuth Chronos.
- La liste exacte des suppressions sera soumise à l'utilisateur avant le premier commit de purge (checkpoint du
  cycle).

## 7. États non nominaux

- **En attente** (temps de reset inconnu au démarrage) : les états neutres actuels sont repris dans les deux
  orientations (braises, cordon, bande et volets translucides). Jamais de vide.
- **Plancher** (dernier relevé exact vieilli avec activité) : grain ou pointillé sur la marque du quota, dans les deux
  orientations, jamais sur le temps (inchangé).
- **Indisponible** : le mot « indisponible » reste centré dans l'empreinte du cadran courant.
- **Plein écran sur un petit écran** (< 1 280 px) : les plafonds de hauteur s'appliquent et rien n'est tronqué ; la
  ligne de fraîcheur passe sur deux lignes si besoin.

## 8. Passe B — auto-critique

- *Brief similaire, même plan ?* La règle « silhouette = zone » est générique. Le reste est propre à Chronos :
  délimitation de Braises par le vide plutôt que par un tiret, sens de fonte qui distingue Fusible de Marée, gris
  épuisé calculé par contraste. Révision faite : un premier jet copiait les tirets d'Arcs sur Braises. Je l'ai
  remplacé, parce qu'il contredisait le sens de l'anneau.
- *Fitts* : la cible de la bascule passe de 66 px à tout le cadran. Risque : un clic voulu comme début de glisser
  bascule l'affichage. Atténuation : la bascule n'a lieu qu'au relâchement sans déplacement au-delà du seuil Windows.
- *Charge cognitive* : la carte Orientation n'apparaît que pour les trois cadrans concernés. Les thèmes passent de 9 à
  15, mais sont rangés en 3 groupes de 4 à 6 (règle des 7 ± 2 respectée par groupe).
- *Cohérence* : mêmes puces que les styles de cadran ; même logique d'apparition que « Mode étendu » ; même vocabulaire
  (« Plein écran », « Quitter le plein écran »).
- *Point faible restant* : la silhouette rectangulaire en `#01` capte aussi les interstices entre les barres. C'est
  voulu (on n'attrape plus « dans le vide »), mais l'overlay masque quelques pixels de plus du bureau. À juger en revue
  visuelle.
- *Point faible restant* : Volets vertical est le moins naturel (un afficheur à volets est horizontal). Il est
  conservé parce que demandé, et rendu lisible par la plaque chiffrée en tête. Si la revue visuelle le juge confus, on
  le retire plutôt que de livrer une variante faible.
- *Chanel* : retirés du premier jet, un halo animé sur la flèche de Braises (le mouvement est réservé aux sessions) et
  un plein écran par graphique (non demandé).

## 9. Critères de revue visuelle (DESIGN-REVIEW du cycle 2)

1. Les huit variantes de cadran capturées à 100 % et 150 % d'échelle Windows, dans les quatre coins de l'écran, sans
   débordement après un changement de style ou d'orientation.
2. Zones : en clics réels sur la machine, la bascule, le double-clic, le glisser et le clic droit marchent sur toute la
   silhouette de chaque variante, et le clic traverse hors silhouette.
3. Braises : flèche à midi, 5 groupes lisibles, « ↻ HH:MM » en mode temps.
4. Historique : aucun sélecteur de style ; plein écran sur l'écran courant, pistes agrandies, Échap à deux niveaux,
   F11.
5. Thèmes : 3 groupes, 15 thèmes, gris épuisé visible sur chaque thème, et Braises, Fusible, Marée et Volets qui
   suivent le thème.
6. Aucune valeur de couleur ni de taille hors `DesignTokens.xaml` ou des pinceaux du thème.

## 10. Journal des choix tentés et rejetés

- Grille 24 h des resets sur Braises : rejetée, erreur de catégorie (l'anneau est la fenêtre de 5 h).
- Tirets copiés d'Arcs pour les heures de Braises : rejetés, la délimitation est le vide entre groupes.
- Fenêtre fixe agrandie (210 × 210) avec des Viewbox recalibrés : rejetée, trop de zones mortes et une empreinte qui ne
  correspond pas aux cadrans en barres.
- Réglage d'orientation partagé avec « Disposition verticale » : rejeté, autre fenêtre et autre sens.
- Thèmes à fond clair dans « Pâle » : rejetés par l'utilisateur pour ce cycle.
- Facteur d'échelle continu pour le plein écran : rejeté, une typographie continue devient illisible aux extrêmes.

## 11. Amendement après DESIGN-REVIEW 1 (DAEDALUS, 2026-10-03)

Rapport : `.zeus/reports/cycle2/design-review-1.md` (4 bloquants, 15 mineurs). Corrections retenues, dans l'esprit du
plan validé (lisibilité des états non nominaux) :

- **B1 — pastilles des cadrans rectangulaires** : Fusible, Marée et Volets (deux orientations) réservent une **bande de
  14 px sous l'empreinte** pour la rangée de pastilles (âge, hors ligne, alertes), alignée à droite. La fenêtre vaut
  empreinte + 14 en hauteur pour ces trois cadrans (constant, pas de saut à l'apparition d'une pastille). Arcs et Braises
  inchangés. La silhouette de geste reste celle du cadran.
- **B2 — « indisponible »** : centré dans l'empreinte pour les **8 variantes** (conforme au §7), posé sur une plaque
  `FondCadran` (coins 6, marge 6 × 2) qui garantit le contraste quel que soit ce qui est dessous.
- **B3 — épuisé ≠ aucune donnée** : `Neutre` (utilisation inconnue) devient **plus sombre que la piste**
  (`Lerp(disque, piste, 0,5)`) : « rien » se lit comme du vide. `Epuise` reste le plus petit mélange à ≥ 3:1 contre le
  disque, mais **désaturé** (gris de même luminance, plus de teinte bleue sur Synthwave / Marine / Néon) ; contraste
  épuisé / neutre ≥ 2:1 testé sur les 15 thèmes. Les braises éteintes passent de `TickMajeur` à `Piste5h` (plus sombres).
  Texte de plaque Volets sur épuisé ≥ 4,5:1.
- **B4 — « ↻ HH:MM »** : même pinceau que la ligne hebdo (`TexteSecondaireClair`), corps 11 via le token
  `CadranCorpsLibelle`.
- **Mineurs corrigés dans la foulée** : libellés « 5 H / 7 J » de Fusible et Marée en `TexteSecondaireClair` (M1) ;
  puce non choisie de la carte Orientation avec fond comme les puces de style (M2) ; dans Apparence, **style + orientation
  avant les thèmes** (M3) ; « 5H » → « 5 H » sur les tuiles Volets (M11) ; marge interne de 4 px dans Volets H (M9) ;
  vocabulaire périmé de la galerie (« ~ », « Estimé (repli JSONL) » → plancher « ≥ ») (M10) ; casse du sous-texte de la
  sonde (M15). Les autres mineurs (M4-M8, M12-M14) vont à la roadmap.
