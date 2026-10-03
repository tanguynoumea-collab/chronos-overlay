# DESIGN-REVIEW cycle 2 — itération 1/3 (DAEDALUS Phase 2)

Date : 2026-10-03. Contrat : `.zeus/DESIGN_PLAN_CYCLE2.md` §1–§9 ; maquettes `.zeus/maquettes/cycle2-cadrans-themes.html` ;
checklist `heuristiques.md` §1–§7.

## Méthode

- Rendus **hors écran** (RenderTargetBitmap), sans lancer Chronos ni aucun clic réel. Harnais jetable : fichier de test
  temporaire dans `tests/Chronos.Tests`, filtré par trait, **supprimé** après production. Aucune modification de `src/` ni
  de `tests/`.
- Montages repris des tests existants : `MainWindow` réelle (contenu mis en page à l'empreinte `EmpreinteCadran.Pour`),
  thème posé par `SelectThemeCommand`, style par `SelectCadranStyleCommand` ; `ReglagesWindow` et `HistoriqueWindow`
  réelles ; plein écran par `EstPleinEcran = true` avec un moniteur injecté (`FournisseurBornesMoniteur`).
- Données : 5 h 62 %, reset dans 2 h 14 ; hebdo 38 %, reset dans 3 j 6 h. États : épuisé (5 h 100 %), en attente (resets
  inconnus), plancher (`≥`, relevé daté), indisponible.
- Cadrans composés sur un **bureau simulé** (dégradé gris-bleu #5A6B82 → #9AA8B8 → #3C485A), plus exigeant que celui de
  la maquette (#2B3B4F → #5B6B7C), pour juger la transparence. Cadre pointillé = empreinte (ajout du harnais).
- 100 % (96 DPI) et 150 % (144 DPI) ; gros plans ×3 (288 DPI) pour les défauts fins.
- Contrastes calculés (WCAG) pour les 15 thèmes à partir des formules de `ChronosTheme.From`.

## Captures (docs/ui-baseline/cycle2/, 61 PNG)

| Groupe | Fichiers |
|---|---|
| 8 variantes, nominal | `cadrans-{minuit,nord,synthwave}-{pct,temps}-{100,150}.png` (12) |
| Silhouettes de geste (magenta, recoloriées dans le harnais) | `silhouettes-minuit-pct-100.png`, `silhouettes-minuit-temps-100.png`, `silhouettes-minuit-pct-150.png` |
| États | `etat-epuise-{minuit,nord,synthwave}-{pct,temps}-100.png`, `etat-attente-minuit-{pct,temps}-100.png`, `etat-indisponible-minuit-100.png`, `etat-plancher-minuit-pct-100.png` |
| Braises ×3 | `braises-zoom3x-{nominal,epuise}-{pct,temps}.png` |
| Gros plans ×3 | `zoom3x-*.png` (15 : indisponible, plancher, épuisé, attente, nominal) |
| Réglages | `reglages-apparence-fusible-860x580{,-150}.png`, `reglages-apparence-{fusible,arcs,volets}-deplie.png`, `reglages-apparence-640x440.png`, `reglages-donnees-{860x580,deplie}.png`, `reglages-historique-860x580.png` |
| Historique | `historique-semaine-920x610{,-150}.png`, `historique-semaine-normal-1920x1080.png`, `historique-semaine-pleinecran-{1920x1080,1280x720}.png`, `historique-jour-pleinecran-1920x1080.png`, `historique-4semaines-pleinecran-1920x1080.png` |
| Galerie `--cadrans` | `galerie-cadrans-1240x900.png` |

## Contrastes mesurés (gris « épuisé »)

`Epuise` = plus petit mélange piste → graduation à ≥ 3:1 **contre le disque** (règle §5.3, respectée : 3,00 à 3,08).
Mais le gris ne se pose presque jamais sur le disque :

| Contre | Plage sur les 15 thèmes | Où |
|---|---|---|
| Piste (`Piste5h`) | 2,32 – 2,60 | arc 5 h d'Arcs |
| Cendre (`TickMajeur`) | 1,40 – 1,78 | braises consumées |
| Neutre (`Scale(graduation, 0,5)`) | **1,01 – 1,78** (minuit 1,02) | « aucune donnée » |
| Sillon (`CadranTuile`) | 2,67 – 2,86 | Fusible |
| Texte de plaque Volets sur épuisé | 3,18 – 4,27 (< 4,5 AA) | Volets |
| `TexteSecondaire` sur bureau moyen / sombre | 1,0 – 1,3 / 2,0 – 2,9 | libellés, « ↻ », « indisponible » |

## Écarts

### Bloquants (retour GSD)

| # | Écart | PNG | Référence violée | Cause probable | Correction proposée |
|---|---|---|---|---|---|
| B1 | La **pastille d'âge** (relevé daté, plancher) chevauche le contenu des cadrans rectangulaires : elle couvre le « % » de « ≥ 38 % » sur Fusible V et Marée V, masque le 5e volet de la rangée 7 J sur Volets H et le bout du cordon 7 J sur Fusible H. | `etat-plancher-minuit-pct-100.png`, `zoom3x-plancher-fusible-v.png`, `zoom3x-plancher-maree-v.png`, `zoom3x-plancher-volets-h.png` | Heuristique §1 et §5 (une information masquée), plan §7 (plancher dans les deux orientations) | `src/Chronos/Views/MainWindow.xaml:111-112` : rangée de pastilles ancrée en bas à droite avec une marge de 6, mesurée pour le disque 170 d'Arcs (commentaire « bord gauche à x = 96 »). Jamais remesurée pour les empreintes 190×66, 110×190, 132×160. | Réserver une case de pastille dans chaque gabarit rectangulaire, ou poser la rangée hors de la zone des valeurs (coin haut-droit des gabarits verticaux, marge droite des horizontaux). Ajouter un test de non-recouvrement pastille / valeur / volets par variante. |
| B2 | Le mot **« indisponible »**, centré, se pose sur la géométrie neutre et devient illisible : entre deux sillons gris (Fusible H), sur la bande (Marée H), sur la plaque (Volets H), sur les volets (Volets V). Gris sur gris, ≈ 1,1–1,5:1. Sur Arcs et Braises, il reste en bas à gauche, ce qui contredit la lettre du §7 (« reste centré dans l'empreinte »). | `etat-indisponible-minuit-100.png`, `zoom3x-indisponible-{fusible-h,maree-h,volets-h,volets-v}.png` | Plan §7, heuristique §5 (un état d'erreur doit se lire) et §6 (contraste) | `src/Chronos/Views/MainWindow.xaml:67-84` (TexteSecondaire 10,5, sans fond) ; `src/Chronos/ViewModels/MainViewModel.cs:289` (`MotIndisponibleAuCentre` vaut seulement pour les styles orientables) | Poser le mot sur une pastille `FondCadran` (rayon 6, marges 6/2) en `TextePrincipal`, ou estomper davantage la géométrie neutre (opacité 0,35) dans cet état. Trancher et écrire Arcs/Braises dans le plan (centre du disque libre, ou bas-gauche assumé). |
| B3 | **Gris « épuisé »** peu visible là où il se pose réellement. Sur Braises épuisé, les braises restantes (1,4–1,8:1 contre la cendre) ne disent presque plus combien de temps il reste avant le reset, alors que c'est la seule question utile quand on est bloqué. Sur Fusible et Marée, le cordon ou la bande grise se fond dans un bureau gris-bleu, encore plus avec Synthwave, Marine ou Néon, où le « gris » est un bleu ardoise. Épuisé et Neutre se confondent : 1,02:1 sur Minuit. Le texte de la plaque Volets sur gris tombe à 3,2:1. | `etat-epuise-{minuit,nord,synthwave}-*.png`, `braises-zoom3x-epuise-pct.png`, `zoom3x-epuise-{arcs-minuit,fusible-v-nord,volets-h-minuit}.png` | Plan §5.3 (gris épuisé visible) et critère §9.5 ; heuristique §6 (3:1 pour les marques porteuses de sens, 4,5:1 pour le texte) | `src/Chronos/Theming/ChronosTheme.cs:168-176` : `EpuiseLisible` ne mesure que contre le disque opaque. `ChronosTheme.cs:153` : `Neutre = Scale(tk, 0.5)` atterrit sur la même luminance. | Exiger aussi ≥ 3:1 contre la piste **et** la cendre, et ≥ 2:1 contre `Neutre` (sinon distinguer par la forme, par exemple un trait creux). Désaturer le gris (chroma ≈ 0) pour qu'il ne passe pas pour une teinte de la rampe. Prévoir un `PlaqueTexteEpuise` clair si le texte sur la plaque grise reste sous 4,5:1. Étendre `ThemingTests`. |
| B4 | **« ↻ HH:MM » de Braises**, nouveauté R3, quasi invisible sans fond sombre : `TexteSecondaire` (×0,66), corps 10,5, posé directement sur le bureau (Braises n'a pas de disque). ≈ 1,1:1 sur un bureau moyen, ≈ 2,8:1 même sur le bureau sombre de la maquette. Il est plus pâle que « 3 j 6 h » juste au-dessus (`TexteSecondaireClair`). | `braises-zoom3x-nominal-temps.png`, `cadrans-*-temps-*.png` | Critère §9.3 (« ↻ HH:MM » en mode temps), heuristique §6 ; §9.6 (le corps 10,5 est écrit en dur) | `src/Chronos/Views/Cadrans/CadranBraisesView.xaml:55-57` | Passer en `TexteSecondaireClair`, et l'envisager aussi pour tout le centre de Braises : un disque `FondCadran` discret de rayon 40 sous le texte, comme Arcs. Remplacer le corps 10,5 par un token `CadranCorpsHeureReset`. |

### Mineurs (roadmap, issues `design`)

| # | Écart | PNG | Cause probable |
|---|---|---|---|
| M1 | Libellés « 5 H / 7 J » de Fusible et Marée quasi invisibles sur un bureau moyen (`TexteSecondaire`, ≈ 1,1:1). Défaut antérieur au cycle (#A9A8B2 avant la phase 39). Volets les porte sur une tuile, Arcs sur le disque. | `cadrans-*-100.png`, `zoom3x-nominal-maree-h-temps.png` | `CadranFusibleView.xaml:24,39,62,77`, `CadranMareeView.xaml:30` et suivantes |
| M2 | Carte Orientation : la puce « Vertical » non choisie n'a pas de fond, contrairement aux puces de style. Le plan §8 annonce « mêmes puces ». | `reglages-apparence-*-deplie.png` | `ReglagesWindow.xaml:195` : le fond `Panel2` de la puce est celui de la carte (`:785`) |
| M3 | Section Apparence : les 15 vignettes de thème précèdent « Style du cadran » et Orientation. À 860×580, la carte Orientation est sous la ligne de flottaison (≈ 300 px à défiler). | `reglages-apparence-fusible-860x580.png` | Ordre des blocs dans `SectionApparence` |
| M4 | Titres de groupe « PÂLE / CLASSIQUE / VIVE » au même style que le titre « THÈME » : la hiérarchie est plate. C'est conforme au §5.1, mais contraire à l'heuristique §1. Le défaut Minuit est dans le 2e groupe. | idem | style `TitreSection` réutilisé |
| M5 | Historique : les annotations « +2 % pendant l'absence… » et « +0 % … » se chevauchent à 920×610 et se touchent en plein écran 1280×720. Défaut probablement antérieur. | `historique-semaine-920x610.png`, `historique-semaine-pleinecran-1280x720.png` | Annotations de rupture sans évitement de collision |
| M6 | 4 semaines en plein écran : ≈ 260 px vides sous la couverture (plafond Niveau 520 atteint) ; légende de droite de 96 px enroulée sur 3 lignes ; « journal ouvert le 14 sept. 2026 » imprimé sur le début de la barre S-1. | `historique-4semaines-pleinecran-1920x1080.png` | Plafond fixe ; colonne de légende étroite |
| M7 | Légende des tokens « ■ principal ■ / sous-agents » cassée, avec un carré orphelin en fin de ligne, à toutes les tailles. | `historique-semaine-*.png` | Légende en un seul bloc qui s'enroule |
| M8 | Jour en plein écran : le trait « maintenant » traverse l'annotation « au moins un reset pendant l'absence… ». | `historique-jour-pleinecran-1920x1080.png` | Ordre Z / placement de l'annotation |
| M9 | Volets H n'a aucune marge interne (contenu de 0 à 190 DIP) : le coin du 6e volet sort du congé r10 de la silhouette, et la galerie coupe ce 6e volet. | `zoom3x-plancher-volets-h.png`, `galerie-cadrans-1240x900.png` | `CadranVoletsView.xaml`, gabarit horizontal |
| M10 | Galerie `--cadrans` : « ~71 % » et la case « Estimé (repli JSONL → grain) » datent d'avant la phase 19 (la convention est désormais « ≥ »). | `galerie-cadrans-1240x900.png` | `CadranPreviewViewModel` / `CadranGalleryWindow.xaml` |
| M11 | Vocabulaire : « 5H / 7J » sur les tuiles Volets, « 5 H / 7 J » ailleurs. | `cadrans-*.png` | `CadranVoletsView.xaml` |
| M12 | Plaque Volets : le filet de charnière traverse les chiffres, ce qui fait un effet « barré » (antérieur au cycle). | `zoom3x-nominal-volets-v-temps.png` | `PlaqueFilet` au milieu de la plaque |
| M13 | En-tête des réglages : « v3.4.0 » alors que le cycle livre la 3.5 (à vérifier à la release). | `reglages-*.png` | Version de l'assembly |
| M14 | Critère §9.6 : des corps sont écrits en dur (20 / 16 / 12 / 11 / 10,5) dans `CadranBraisesView.xaml:42-56`, `CadranArcsView.xaml:140-152` et `MainWindow.xaml:68`. Sans effet visuel. Le cas « ↻ » se corrige avec B4. | — | Tokens absents |
| M15 | Données : le sous-texte de la sonde commence en minuscule (« chiffres exacts… »), celui de la connexion en majuscule. | `reglages-donnees-860x580.png` | Texte |

## Verdicts par écran

| Écran | Verdict | Ce que l'œil voit (1er / 2e / 3e) | Notes |
|---|---|---|---|
| Cadrans, nominal (8 variantes × 3 thèmes × % / temps × 100 / 150 %) | **ÉCARTS MINEURS** | valeur 5 h / couleur du quota / longueur du temps | Empreintes conformes au §1.1 (vérifiées sur la planche). Les sens de fonte et la ligne d'eau ondulée sont conformes au §1.2. Les quatre cadrans alternatifs suivent bien les trois thèmes. Rien n'est tronqué à 150 %. **Volets vertical : conservé**. Il est lisible, la plaque chiffrée en tête donne la clé et les 6 volets empilés se lisent comme une jauge. Aucune raison de le retirer au titre du §8. |
| Silhouettes de geste | **CONFORME** (nuance M9) | — | Disque r 74 sur Arcs et Braises ; rectangle arrondi à l'empreinte pour les 6 autres, qui couvre tout le visuel. Seule la flèche de Braises dépasse, ce qui est assumé. |
| Cadrans, états non nominaux | **ÉCARTS BLOQUANTS** | — | B1 (pastille), B2 (indisponible), B3 (épuisé). En attente : neutres repris dans les deux orientations, jamais de vide. Conforme. |
| Braises (temps 3 lignes, épuisé) | **ÉCARTS BLOQUANTS** | compte à rebours / anneau / flèche | Flèche à midi et 5 groupes de 4 lisibles (la délimitation par le vide fonctionne). Écarts B4 (« ↻ ») et B3 (épuisé contre cendre). |
| Réglages | **ÉCARTS MINEURS** | aperçu en direct / vignettes de thème / puces | La carte Orientation est visible pour Fusible et Volets, masquée pour Arcs (« Mode étendu » à sa place). 3 groupes, 15 thèmes. Données : plus de « Barre de statut » ni de « Recalibrer… ». Historique : pas de sélecteur de style, « Aussi : double-clic sur le cadran » présent. |
| Historique | **ÉCARTS MINEURS** | courbe Niveau / rythme / tokens | Le plein écran agrandit réellement (comparer `historique-semaine-normal-1920x1080.png` et `-pleinecran-1920x1080.png`) : pistes en 150:72:72, corps environ ×1,35, plus de défilement. Le bouton « ⛶ Plein écran » devient « ⤢ Quitter le plein écran · Échap ». Aucun sélecteur de style. |
| Galerie `--cadrans` | **ÉCARTS MINEURS** | — | 8 variantes à l'échelle 1. Écarts M9 et M10. |

## Non couvert par cette passe (exige l'écran réel)

- §9.1 : les quatre coins de l'écran, et le recalage d'ancrage après un changement de style ou d'orientation.
- §9.2 : les clics réels (déjà vérifiés par l'humain en phase 42, commit d0be1ae).
- §9.4 : Échap à deux niveaux et F11 en conditions réelles (couverts ici seulement par les tests de liaison). Les 150 %
  sont simulés à 144 DPI : les arrondis de mise en page d'une vraie mise à l'échelle Windows peuvent différer d'un pixel.

## Décision

**4 bloquants → retour boucle GSD** (itération 1/3). B1 et B2 relèvent de la couche commune de `MainWindow`, B3 de
`ChronosTheme` (règle de contraste à étendre et tests à enrichir), B4 de `CadranBraisesView`. Les 15 mineurs vont à la
roadmap avec l'étiquette `design`. À la prochaine itération, tout refaire à partir d'un build propre.
