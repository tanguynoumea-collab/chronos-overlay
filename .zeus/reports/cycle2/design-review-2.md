# DESIGN-REVIEW cycle 2 — itération 2/3 (DAEDALUS Phase 2)

Date : 2026-10-03. Objet : vérifier les corrections de la phase 42.1 (amendement §11 de `.zeus/DESIGN_PLAN_CYCLE2.md`)
après `design-review-1.md`, puis chercher les régressions. Porte annoncée : 1949/1949 tests, 0 avertissement.

## Méthode

Même méthode qu'à l'itération 1 :
- rendus hors écran (RenderTargetBitmap), sans lancer Chronos ni faire de clic réel ;
- harnais de test temporaire, supprimé ensuite ;
- bureau simulé gris-bleu ;
- échelles 96 et 144 DPI, gros plans ×3.

La fenêtre du cadran est désormais mise en page à `EmpreinteCadran.Fenetre` (empreinte + bande de 14 px pour Fusible,
Marée et Volets). Les contrastes ont été recalculés **par le code de production** (`ThemeCatalog.All`, `ContrasteWcag`).

Piège du harnais, consigné ici : un cadran monté hors fenêtre et sans parent est réarrangé à sa `DesiredSize` par le
gestionnaire de mise en page. Il perd alors sa bande, et l'aperçu des réglages paraissait rogné de 14 px en bas. Ce n'est
pas un défaut du produit : dans l'application, la `MainWindow` arrange son contenu à empreinte + bande. Le harnais
réarrange donc le cadran réel à la taille de sa fenêtre avant de rendre les réglages, et l'aperçu est alors correct.
Point de fragilité à connaître : `Cadrer()` (`ReglagesWindow.xaml.cs:107-112`) suppose que `ActualHeight` du contenu
inclut la bande. Si le contenu était un jour dimensionné à l'empreinte, la bande serait soustraite deux fois.

## Captures r2 (59 PNG, suffixe `-r2`, dans docs/ui-baseline/cycle2/)

- Cadrans en état normal : `cadrans-{minuit,nord,synthwave}-{pct,temps}-{100,150}-r2.png` (12).
- Silhouettes : `silhouettes-minuit-pct-100-r2.png`, `silhouettes-plancher-minuit-100-r2.png`.
- États :
  - `etat-epuise-{minuit,nord,synthwave,marine,neon}-pct-100-r2.png`, `etat-epuise-minuit-temps-100-r2.png` ;
  - `etat-indisponible-{minuit,nord,synthwave,marine,neon}-100-r2.png` ;
  - `etat-attente-minuit-{pct,temps}-100-r2.png` ;
  - `etat-plancher-minuit-{pct-100,temps-100,pct-150}-r2.png`.
- Gros plans :
  - `braises-zoom3x-{nominal,epuise}-{pct,temps}-r2.png` ;
  - `zoom3x-{indisponible-{arcs,braises,fusible-h,volets-v},plancher-{fusible-v,maree-v,volets-h}}-r2.png` ;
  - `zoom3x-{epuise-{arcs-minuit,fusible-v-nord,volets-h-minuit,volets-v-synthwave},nominal-{maree-h-temps,volets-h}}-r2.png`.
- Réglages :
  - `reglages-apparence-{fusible,fusible-v,volets-h,maree-v}-860x580-r2.png`, `reglages-apparence-fusible-860x580-150-r2.png` ;
  - `reglages-apparence-{arcs,fusible}-deplie-r2.png`, `reglages-apparence-640x440-r2.png`, `reglages-donnees-860x580-r2.png`.
- Historique et galerie : `historique-semaine-920x610-r2.png`, `historique-semaine-pleinecran-1920x1080-r2.png`,
  `galerie-cadrans-1240x900-r2.png`.

## Contrastes recalculés (15 thèmes, code de production)

| Mesure | Plage | Seuil | Résultat |
|---|---|---|---|
| Épuisé / disque | 3,00 – 3,08 | ≥ 3 | OK |
| Épuisé / neutre | 2,67 – 2,82 | ≥ 2 | OK (1,01 – 1,78 en r1) |
| Épuisé / piste | 2,32 – 2,60 | — | inchangé, mais les braises éteintes sont désormais sur la piste, plus sombres |
| Neutre / piste | 1,08 – 1,15 | « plus sombre » | OK : « rien » se lit comme du vide |
| Texte de plaque épuisée / épuisé | 4,67 – 5,83 | ≥ 4,5 | OK (3,18 – 4,27 en r1) |
| `TexteSecondaireClair` / bureau moyen #8A99AA | 1,15 – 1,72 | — | résiduel, dépend du fond (voir R2-M1) |

Épuisé est maintenant un gris neutre sur tous les thèmes (#5F5F5F à #7D7D7D) : plus de teinte bleue sur Synthwave, Marine
ou Néon.

## Vérification des bloquants de l'itération 1

| # | Vérification | PNG | Statut |
|---|---|---|---|
| B1 | Rangée de pastilles dans la bande de 14 px sous les cadrans rectangulaires : plus aucun recouvrement de « ≥ 38 % » (Fusible V, Marée V), des volets (Volets H) ni du cordon (Fusible H). Arcs et Braises sont inchangés. La silhouette de geste reste celle de l'empreinte, la bande est hors silhouette sauf sur la pastille elle-même. | `etat-plancher-minuit-*-r2.png`, `zoom3x-plancher-*-r2.png`, `silhouettes-plancher-minuit-100-r2.png` | **Corrigé** |
| B2 | « indisponible » centré dans l'empreinte (et non dans la fenêtre) pour les 8 variantes, sur une plaque `FondCadran` : lisible sur Minuit, Nord, Synthwave, Marine et Néon, quel que soit ce qui est dessous. | `etat-indisponible-*-r2.png`, `zoom3x-indisponible-*-r2.png` | **Corrigé** |
| B3 | Épuisé en gris pur, nettement distinct du neutre. Sur Braises épuisé, les braises restantes (grises) se détachent bien des braises éteintes (sur la piste) : le temps avant reset se lit de nouveau. Plaque Volets épuisée en texte clair (4,7 à 5,8:1). | `etat-epuise-*-r2.png`, `braises-zoom3x-epuise-*-r2.png`, `zoom3x-epuise-*-r2.png` | **Corrigé** |
| B4 | « ↻ 14:14 » en `TexteSecondaireClair`, corps 11 : même poids que « 3 j 6 h », lisible sur le bureau simulé. | `braises-zoom3x-nominal-temps-r2.png`, `braises-zoom3x-epuise-temps-r2.png` | **Corrigé** |

## Vérification des mineurs corrigés

| # | Statut | Preuve |
|---|---|---|
| M1 | Amélioré : libellés « 5 H / 7 J » de Fusible et Marée en `TexteSecondaireClair`, nettement plus visibles. Il reste un résiduel sur un bureau moyen (R2-M1). | `cadrans-minuit-pct-100-r2.png` |
| M2 | Corrigé : la puce non choisie de la carte Orientation a un fond, comme les puces de style. | `reglages-apparence-maree-v-860x580-r2.png` |
| M3 | Corrigé : le style et l'orientation viennent avant les thèmes. La carte Orientation est visible sans défiler à 860×580. | `reglages-apparence-fusible-860x580-r2.png` |
| M9 | Corrigé : marge interne dans Volets H, et le 6e volet est entier dans la galerie. | `zoom3x-nominal-volets-h-r2.png`, `galerie-cadrans-1240x900-r2.png` |
| M10 | Corrigé : la galerie affiche « ≥ 71 % » et la case s'intitule « Plancher (≥, relevé vieilli → grain) ». | `galerie-cadrans-1240x900-r2.png` |
| M11 | Corrigé : les tuiles Volets portent « 5 H / 7 J ». | `cadrans-*-r2.png` |
| M15 | Corrigé : « Chiffres exacts… » commence par une majuscule. | `reglages-donnees-860x580-r2.png` |

## Recherche de régressions

- **Bande de pastilles et recalage** : la fenêtre vaut empreinte + 14, constante, sans saut quand une pastille apparaît.
  Je n'ai vu ni chevauchement ni troncature à 100 et 150 %. Le recalage au coin ne se vérifie pas hors écran (voir R2-M2).
- **Aperçu des réglages** : il est cadré sur l'empreinte. Fusible H et V, Marée V, Volets H plancher et Arcs sont
  entiers et centrés dans le cadre de 144. La pastille d'âge de Volets H reste visible sous le cadran dans l'aperçu.
- **Arcs indisponible** : les arcs neutres sont désormais plus sombres que la piste (1,1:1). Les anneaux se lisent comme
  vides, sans valeur suggérée, avec le mot sur sa plaque au centre. C'est conforme à l'intention.
- **Volets, plaque épuisée** : texte clair sur gris, lisible (Minuit, Synthwave).
- **Galerie** : 8 variantes, vocabulaire plancher, aucune troncature.
- **Historique** : inchangé par rapport à l'itération 1 (sans régression). Les mineurs reportés M5 et M7 sont toujours
  visibles : ce n'est pas une aggravation.

## Écarts restants

### Bloquants

Aucun.

### Mineurs (roadmap)

| # | Écart | PNG | Cause probable |
|---|---|---|---|
| R2-M1 | Sur un bureau gris-bleu moyen, les marques qui reposent directement sur le fond d'écran restent faibles : libellés « 5 H / 7 J » de Fusible et Marée (≈ 1,7:1), cordon ou bande épuisée de Fusible et Marée (≈ 1,6:1). Le chiffre « 100 % » et le bord de la marque portent l'information, donc ce n'est pas bloquant. | `zoom3x-epuise-fusible-v-nord-r2.png`, `cadrans-minuit-pct-100-r2.png` | Pas de fond sous les cadrans rectangulaires. Piste à étudier : une ombre portée légère sous le texte et les marques. |
| R2-M2 | Effet de la bande de 14 px : accroché à un coin **bas** de l'écran, un cadran rectangulaire flotte 14 px au-dessus du bord, alors qu'il est collé au bord en haut (asymétrie haut / bas, à juger sur l'écran réel). | `silhouettes-plancher-minuit-100-r2.png` | `EmpreinteCadran.Fenetre`, bande toujours sous l'empreinte. Variante possible : placer la bande du côté du centre de l'écran. |
| R2-M3 | Fragilité de `Cadrer()` : la soustraction de la bande suppose que le contenu fait empreinte + bande. Un test d'intégration avec la vraie `MainWindow` affichée l'assurerait mieux que le test actuel. | — | `ReglagesWindow.xaml.cs:107-112` |
| Reportés | M4 à M8 et M12 à M14 de l'itération 1, non réévalués : ils sont inchangés, sans aggravation constatée (M5 et M7 revus dans l'Historique). | — | — |

## Verdicts par écran

| Écran | Verdict |
|---|---|
| Cadrans, état normal (8 variantes × 3 thèmes × % / temps × 100 / 150 %) | **ÉCARTS MINEURS** (R2-M1) |
| Silhouettes de geste | **CONFORME** |
| Cadrans, états non nominaux (épuisé, indisponible, en attente, plancher) | **ÉCARTS MINEURS** (R2-M1, R2-M2) |
| Braises | **CONFORME** |
| Réglages | **ÉCARTS MINEURS** (reportés M4 et M13 ; R2-M3) |
| Historique | **ÉCARTS MINEURS** (reportés M5 à M8) |
| Galerie `--cadrans` | **CONFORME** |

## Décision

**Zéro bloquant : sortie de la boucle DESIGN-REVIEW à l'itération 2/3.** Les captures `-r2` deviennent la base de
non-régression du cycle 2. R2-M1 à R2-M3 rejoignent la roadmap (`design`) avec les mineurs déjà reportés. Il reste à
vérifier sur l'écran réel ce que le hors-écran ne montre pas : les quatre coins, le recalage avec la bande, Échap et F11,
et la mise à l'échelle Windows réelle.
