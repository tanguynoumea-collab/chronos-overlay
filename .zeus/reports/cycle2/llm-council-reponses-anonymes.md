# Réponses anonymisées — LLM-COUNCIL cycle 2

Question : voir `.zeus/reports/cycle2/llm-council-brief.md` (8 décisions pour implémenter R1-R7 de Chronos).

---

## Response A

Hypothèses clés : « +20 % » s'entend de la taille **effectivement affichée** aujourd'hui (Fusible ≈ 158 × 77, Volets ≈ 158 × 54), pas de la largeur XAML (210/234), sinon on obtient ≈ +60 % à l'écran. Une fenêtre layered est hit-testée en deux temps : l'OS ne route le clic que si le pixel a alpha > 0, puis WPF teste sa géométrie. `Fill="Transparent"` passe le test WPF mais pas celui de l'OS.

1. Taille → supprimer le Viewbox pour Fusible/Volets/Marée ; fenêtre dimensionnée par le contenu. Retirer les trois Viewbox ; tailles nominales en tokens à l'échelle 1, déjà ×1,2 du rendu actuel. Fenêtre en `SizeToContent="WidthAndHeight"`. Arcs et Braises gardent 170 × 170. Piège Win32 : quand la taille change, la fenêtre grandit vers la droite/le bas ; ancrée en bas à droite elle déborde. Sur `SizeChanged`, recaler sur le coin d'ancrage courant, pas le plus proche recalculé. Le Viewbox à 0,675/0,752 produit aussi des tailles de police fractionnaires sous rendu logiciel (AllowsTransparency) → texte flou.

2. Orientation → propriété `Orientation` sur les contrôles, deux mises en page dans les vues, réglage par cadran. Les contrôles sont des FrameworkElement à OnRender : une DP `Orientation` qui alimente une fonction de géométrie pure testable. Les vues ont besoin de deux gabarits par orientation. Ne pas partager avec `VerticalLayout`. Une valeur par style (`OrientationFusible`, `OrientationMaree`, `OrientationVolets`) ; une valeur globale ferait basculer Marée.

3. Hit-testing → couche de zones déclarée dans chaque vue, pinceau token `#01000000`, testée par `VisualTreeHelper.HitTest`. Sortir `CentreHit` de la racine ; chaque vue porte : une silhouette d'attrape (anneau/disque pour Arcs/Braises, rectangle arrondi pour les barres) en `{StaticResource ZoneAttrape}`=`#01000000` → drag ; une zone de bascule posée sur les valeurs, Handled ; double-clic via `ArbitreClicCentre`. Pour les barres, définir une « zone des valeurs ». Clic droit à la racine marche dès que la silhouette existe. Tests : matrice de points par cadran × orientation, vue réelle en STA, `HitTest`, élément attendu ; garde d'arbre visuel : tout élément à gestionnaire de souris a un Fill/Background d'opacité ≥ 1, `Transparent` interdit (remplace le test OS impossible en unitaire). Corriger les commentaires « Transparent suffit ».

4. Braises → divisions horaires internes, 20 pastilles, repère de reset distinct. Grille 24 h = erreur de catégorie. 20 pastilles (15 min), séparateur toutes les 4 → 5 heures à 72°. Point de reset (midi) marqué distinctement. Heure absolue du reset (serveur, exacte) au basculement.

5. Plein écran → hauteurs proportionnelles + dictionnaire « plein écran » ; Échap à deux niveaux. `EstPleinEcran` : rangées en `*`, ScrollViewer coupés, dictionnaire fusionné redéfinissant les `Histo*` (polices, épaisseurs, pointillé) en DynamicResource. Pas de Viewbox global. Piège Win32 : `WindowState=Maximized` sur `WindowStyle=None` couvre la barre des tâches et déborde ; poser Left/Top/Width/Height sur les bornes du moniteur courant (DIP), mémoriser la restauration. Échap quitte puis ferme ; bouton visible + F11. Tests 150/72 restent ceux du mode normal.

6. Purge → migration d'abord, relais `--statusline` conservé, garde sur arguments inconnus. `--statusline` traité avant le verrou (`App.xaml.cs:26` vs `:84`) ; le supprimer ⇒ à chaque rendu de barre un MessageBox « Chronos tourne déjà ». (a) Tout `--xxx` inconnu sort silencieusement avant le verrou. (b) 3.5.0 garde `--statusline` en relais pur (exécute `InnerStatusLineCommand`, aucune écriture `usage.json`). (c) Le réconciliateur migre une fois (`TransformForUninstall` existe) avec sauvegarde. (d) Retirer `InnerStatusLineCommand` seulement après. Reste dans l'ordre de l'inventaire, liste validée ; garde 3 → 1 composite. Les hooks pointent sur un exe versionné ; garder le repointage.

7. Thèmes → Pâle = désaturé sombre ; le clair est un autre axe, reporté ; rampes corrigées. Pâle/Classique/Vive = saturation ; luminance du fond = autre axe. Le clair demande variante de `From()`, `TickReset`/`HistoGris`/converter dans le thème, fenêtres secondaires, et un cadran sur fond d'écran clair inconnu. Brume/Lin au cycle suivant. Néon/Aurore violent la loi d'encodage : rampe monotone en luminance, rouge terminal. Gardes chiffrées : rouge ≥ 3:1 ; écart épuisé/piste ; deutéranopie.

8. Phases : P0 Socle sans UI (SettingsService tolérant valeur par valeur ; garde d'arguments), P1 R5 après validation de la liste, P2 R4 (supprimer la propriété), P3 R6 (palettes validées), P4 DAEDALUS unique R1/R2/R3/R7 validé, P5 R2 + fenêtre puis R1, P6 R3, P7 R7 + matrice, revue visuelle, release 3.5.0.

Plus forte objection : la fenêtre qui suit son contenu fait du recalage d'ancrage un point fragile (DPI mixte, multi-écrans, recalage pendant drag). Une enveloppe fixe par style éviterait ce risque au prix de plus de clics perdus. Maintenu, car R7 deviendrait plus difficile.

---

## Response B

Hypothèses : seul compte ce que l'utilisateur voit ; sur une fenêtre layered la zone cliquable = pixels peints alpha non nul ; l'exe est appelé par des outils externes (hooks, statusLine). Vérifié : MainWindow 170×170, 4 Viewbox Uniform, CentreHit 66 px Transparent, `--statusline` avant le mutex.

1. Taille dictée par le contenu. Supprimer 170×170 et les Viewbox ; `SizeToContent=WidthAndHeight` ; tokens `CadranTaille*` ; Arcs et Braises 170. Au changement de style/orientation, garder le coin d'accroche et relancer `SnapToNearestCorner`. « +20 % » par rapport à l'affiché : cible ≈ 190 px de large (natif 210/234 donnerait +33 %/+48 %, faux). Normal/Étendu ne correspond à rien pour ces cadrans : le dire, l'abandonner.

2. Propriété `Orientation` sur FuseBar/TideColumn/FlapRow, géométrie par fonction pure qui échange les axes ; deux petits gabarits par déclencheur dans chaque vue ; pas de vues dédiées. Séparé de `VerticalLayout`. Mémorisé par style. Stockage tolérant (chaîne ou dictionnaire, pas un enum brut) à cause de `Load()`.

3. On peint les zones. Couche de fond `#01000000` qui suit la silhouette (disque Arcs/Braises, rectangle arrondi englobant barres+étiquettes) → attrape + clic droit. Éléments de bascule avec propriété attachée `Geste.Role="Bascule"` ; MainWindow remonte l'arbre et route ; arbitre inchangé. Suppression du CentreHit partagé. Tests par style × orientation × taille : vérité layered par `RenderTargetBitmap` (alpha > 0 dans la silhouette, = 0 dehors) + routage par `HitTest`. `GardeGestesCadranTests` passe à cette matrice.

4. Marquer le reset, pas la grille 24 h. Le reset est toujours à midi quand la dernière pastille s'éteint. Seuil marqué à midi (encoche propre à Braises, heure serveur en option) ; 20 pastilles (15 min) en 5 groupes de 4 séparés par un vide plus large — la délimitation est le vide. L'hebdo à 12 pastilles a le même défaut, signalé hors périmètre.

5. Rangées proportionnelles (`150*`, `72*`, `72*`, `12*`), défilement coupé ; un seul dictionnaire « PleinEcran » en DynamicResource avec valeurs fixes ≈ ×1,5 (pas de facteur continu). Pas de Viewbox global. Échap à deux niveaux, bouton visible. Test des trous aux deux tailles.

6. (1) Garde d'arguments : tout `--xxx` inconnu → exit 0, jamais l'overlay ; `--statusline` reste en relais pur en 3.5.0. (2) Migration par le réconciliateur : sauvegarde, remplacement par `InnerStatusLineCommand` ou retrait de la clé, idempotent, journalisé, testé sur fichiers témoins. (3) Suppression dans l'ordre de l'inventaire, liste validée avant.

7. Pâle = fonds clairs — seule catégorie qui apporte une capacité nouvelle (overlay sur fond d'écran clair). Les sombres désaturés vont en Classique ; Pâle = Brume, Lin + une troisième. Pas de variante de `From()` : remplacer « × 0,66 » et l'assombrissement par une interpolation vers le fond `Lerp(texte, disque, 0,34)`, valable dans les deux sens. `TickReset`, `HistoGris` dans les pinceaux. Rampe : invariants testés (ordre perceptif croissant, dernière couleur rouge-magenta, épuisé ≥ 3:1 contre la piste). Corriger Néon et Aurore. Test « 9 thèmes » → test d'invariants.

8. P1 réglages tolérants ; P2 purge hors statusLine + docs (liste validée avant) ; P3 garde d'arguments, migration, suppression du pont ; P4 DAEDALUS (R1/R2 mesures écran, R3, plein écran, R6 + palettes) validé ; P5 géométrie (tokens, Viewbox, R2, R1) ; P6 R7 ; P7 R3 ; P8 R4 ; P9 R6 ; release 3.5.0. P7-P9 parallélisables après P4.

Objection : supprimer le pont statusLine jette une source exacte, gratuite, poussée par Claude Code ; elle ne gagne jamais seulement à cause d'un bug de chemin (virtualisation MSIX), déjà résolu pour les hooks via `Packages\Claude_*\LocalCache`. Maintenu (la sonde fournit déjà les chiffres + statut serveur ; le pont impose une intervention fragile ; R5 demande de simplifier), mais à soumettre à l'utilisateur.

---

## Response C

Hypothèses : « +20 % » sur la taille vue aujourd'hui (Fusible ≈ 158 × 77, Volets ≈ 158 × 54) ; doctrine « exact ou rien » ; un seul cycle → milestone v1.9, exe 3.5.0.

1. Retirer le Viewbox des styles alternatifs, taille de fenêtre par style et orientation, en tokens ; VM expose `LargeurEmpreinte`/`HauteurEmpreinte` ; Arcs 170. Cadrans à l'échelle 1 (sinon R7 calcule dans un repère qui ment). Coin accroché fixe ; aucun recalage aujourd'hui au changement de style. Cible ≈ 1,2 × rendu actuel (≈ 190 px), pas 1,2 × 210/234 (252/281, trop grand). « Normal et Étendu » ne correspond à rien hors Arcs : le signaler. Objection : on perd la simplicité de l'empreinte constante, mais l'accroche lit déjà le rectangle réel.

2. Propriété `Orientation` sur les 3 contrôles, une seule vue par cadran qui change la disposition de son panneau ; pas de vues dédiées. Réglage propre au cadran, mémorisé par style, défauts = orientations actuelles. Pas avec `VerticalLayout`. Lecture tolérante d'enum + test de non-régression.

3. Chaque vue déclare ses zones, nommées ou par propriété attachée : `ZoneBascule` (clic simple + double, arbitre inchangé), `ZoneAttrape` = silhouette `#01000000` (drag + clic droit), reste transparent (clic traverse, voulu). Suppression du CentreHit partagé. Tests : garde des pinceaux (alpha ≥ 1 ; `HitTest` considère `Transparent` comme touchable ⇒ test seul faussement vert) + points d'échantillonnage STA par style × orientation. Objection : `#01` rend toute la silhouette opaque aux clics, interstices compris — à valider en conception.

4. Divisions horaires internes ; 20 pastilles (15 min), repère toutes les 4 pastilles (interstice plus large ou point extérieur). On lit « environ 2 h restantes » ; la bascule donne l'heure exacte. Objection : l'utilisateur veut peut-être *quand* (« à 14 h 20 ») ; alors ajouter l'heure du reset au centre en mode heure — à valider au design.

5. Rangées proportionnelles, ScrollViewer sans défilement ; un seul dictionnaire fusionné « plein écran » (polices, épaisseurs) en DynamicResource sur ces clés seulement. Pas de Viewbox. Échap à deux niveaux, bouton. D'abord supprimer la propriété `HistoriqueStyleSemaine`. Objection : `150*/72*` vide sur 21:9 → hauteur max par piste.

6. Méthodologie d'abord (spécification cible), liste validée, suppression dans l'ordre de l'inventaire, pont statusLine en dernier en deux temps. Faits : `--statusline` court-circuite avant le verrou ; le réconciliateur repointe à chaque lancement. 3.5.0 : le réconciliateur désinstalle (remet `InnerStatusLineCommand` ou retire la clé, sauvegarde), `--statusline` = relais pur ; 3.6 : retirer le relais. Tester que le journal reste lisible avec `SourceUsage` inconnues. Objection : tant que 3.5.0 n'a pas été lancé une fois, settings.json pointe sur 3.4.0 ; le dire dans les notes de version.

7. Pâle = sombre désaturé/pastel ; aucun thème clair ce cycle sauf demande explicite (variante de `From()`, `TickReset`, fenêtres secondaires sombres, `HistoGris`, overlay clair sur fond aléatoire). Poser la question ; si oui `FromClair()` dédiée. Néon/Aurore restent Vive, rampe corrigée. Gardes : rouge en teinte chaude ; épuisé ≥ 3:1 contre le fond. `Categorie` sur `ThemeChoice`. Nouvelles : Sauge, Graphite, Marine, Café, Synthwave, Tropique, Lave ; Brume/Lin en réserve.

8. (1) DAEDALUS ÉVOLUTION unique R1/R2/R3/R6/R7 avec palettes, validé avant XAML, en parallèle de (2) R5 (liste validée) et (3) R4 ; (4) R6 ; (5) fondation géométrique R2+R1 ; (6) R3 ; (7) R7 dernier, contrôle humain sur la vraie machine ; (8) livraison 3.5.0, vérifier désinstallation statusLine + relais. Objection : 8 phases c'est lourd ; fusionner 5 et 6 si le design fige le rayon de Braises.

---

## Response D

Prémisse contestée : trois demandes reposent sur un constat inexact ou incomplet.

1. Supprimer le Viewbox ; taille naturelle par tokens, `SizeToContent=WidthAndHeight` ; recaler en gardant le coin d'accroche fixe. Prémisse fausse sur « +20 % » : 210 → 252 = +60 % à l'écran ; référence = rendu actuel × 1,2 (Fusible ≈ 190 × 92, Volets ≈ 190 × 65) — confirmer en une question. « Normal et Étendu » pour Fusible/Volets ne correspond à rien : retirer le critère. Objection : fenêtre variable → centre visuel déplacé, chevauchement du widget ; tester deux écrans DPI mixte.

2. Propriété `Orientation` dans les 3 contrôles, OnRender paramétré par l'axe ; deux mises en page par vue par trigger. Réglage propre à chaque style, défaut = actuel ; pas avec `VerticalLayout`. Faille de la demande : Marée horizontale ≈ Fusible ; Fusible vertical ≈ Marée ; volets verticaux contredisent l'afficheur à volets. R1 risque de rendre les styles interchangeables ; DAEDALUS doit montrer en quoi chaque variante reste reconnaissable, sinon renoncer à cette variante.

3. Zones déclarées dans chaque vue : attrape (rectangle d'emprise `#01000000`), bascule (valeurs/centre `#01000000`), clic droit sur la racine. CentreHit au niveau fenêtre seulement pour Arcs. Double-clic via l'arbitre. Piège : `Transparent` est touchable pour WPF → `HitTest` = faux positif garanti. Test honnête : `RenderTargetBitmap` par style × orientation × taille, alpha > 0 aux points témoins, puis `HitTest` sur la bonne zone. Ordre contesté : clic droit et bascule cassés aujourd'hui ; correctif minimal (fond `#01` sur la racine de chaque vue) dès la première phase, R7 complet ensuite.

4. Question mal posée : le reset tombe toujours à midi sur l'anneau ; ce qui manque = « combien d'heures restent » et « où est l'arrivée ». Repère de reset distinct à midi ; 4 séparateurs internes → 5 segments d'une heure ; 15 pastilles (20 min) pour garder la place des espaces à R66. Défaut latent : hebdo 12 pastilles pour 7 jours → 14 ou 7 séparateurs. Objection : l'heure absolue reste illisible sans clic ; demander à l'utilisateur lequel des deux il attend.

5. Rangées proportionnelles, défilement coupé ; un facteur d'échelle unique pour le texte en DynamicResource (base × k), pas de second dictionnaire ; pas de Viewbox ; `WindowState=Maximized` sur l'écran courant ; Échap à deux niveaux ; normal inchangé. Objection : Échap à double sens → bouton obligatoire.

6. Prémisse incomplète : `StatusLineBridge.Run` écrit aussi `MinimalStatusLine`, la barre que l'utilisateur voit dans Claude Code ; la supprimer retire un élément visible → accord explicite. Vérifié : `--statusline` sort avant le mutex ; le réconciliateur repointe à chaque lancement ⇒ une 3.5.0 sans le mode lancerait l'overlay (~60 Mo) à chaque rendu. Séquence : (1) `Load` tolérant ; (2) diag + docs ; (3) B4/B5/B1 ; (4) A1-A4/A8/A9, garde 3 → 1 ; (5) B3 (WeeklyAnchor lecture seule) ; (6) 3.5.0 : `--statusline` relais pur sortant la barre minimale sans `usage.json` ; retrait de `statusLine` seulement si l'utilisateur accepte de perdre la barre ; (7) suppression du mode en 3.6. `InnerStatusLineCommand` vaut null : rien à restaurer. Objection : `usage.json` peut être mort à cause de la vue virtualisée, pas de la donnée ; le stdin de la statusLine contient de vrais `rate_limits` ; confirmer avant de supprimer que Claude Code n'appelle pas du tout la barre.

7. Pâle = sombres désaturés ce cycle, sans nouvelle infrastructure ; clair = phase séparée si demandé (anneaux sur fond d'écran clair illisibles). Néon/Aurore : rampe sémantique, corrigée dans la famille de teintes, pas d'exception. Défaut oublié : épuisé à 1,7-2,1 → gardes ≥ 3:1 contre piste et fond ; palettes validées avant intégration.

8. Deux versions : 3.5.0 = P0 (`Load` tolérant, tests de caractérisation des 4 cadrans alternatifs, correctif minimal des fonds de clic), P1 R5 (liste + accord sur la barre), P2 R4 ; 3.6.0 = P3 R6, P4 DAEDALUS R1/R2/R3/R7, P5 infra taille + R2, P6 R1, P7 R3, P8 R7 avec tests alpha. Objection : deux versions pour un utilisateur, doctrine « un cycle = une version » ; si imposé, garder l'ordre et publier un exe intermédiaire après P2.

---

## Response E

1. Taille par cadran, sans `SizeToContent`, Viewbox retirés. Fonction pure `EmpreinteCadran(style, orientation) → Size` (tokens), Width/Height liés au VM ; Arcs 170 ; vues à 1:1 ; testable sans fenêtre. Recaler sur le coin d'accroche au changement de taille. L'aperçu 144 × 144 garde un Viewbox (seul cas justifié). +20 % par rapport à l'affiché : ≈ 190 × 92 Fusible, 190 × 65 Volets, validé sur capture.

2. DP `Orientation` sur les contrôles OnRender ; la vue bascule son StackPanel et le placement des étiquettes par trigger ; vue séparée seulement si illisible. Réglage propre aux cadrans, pas `VerticalLayout`, stocké par style avec orientation native par défaut. Modèle `ToggleCadranMode`, booléen `EstStyleOrientable`.

3. Propriété attachée `Geste.Role = Attrape | Bascule` déclarée par chaque vue ; un répartiteur unique dans MainWindow garde `ArbitreClicCentre`. Attrape = forme `#01000000` sur l'empreinte visuelle (clic droit aussi). Bascule = valeurs et étiquettes ; remplace CentreHit 66 × 66 faux pour 4 styles sur 5. `HitTest` considère `Transparent` touché : test STA paramétré style × orientation, points choisis, rôle trouvé ET brosse alpha ≥ 1 sur l'élément ou un ancêtre ; garde statique (aucun élément `Geste.Role` à Fill/Background nul ou transparent). Recette manuelle au clic.

4. Divisions horaires internes, à rebours depuis le reset ; pas de grille 24 h. 20 pastilles en 5 groupes de 4 séparés par un écart plus large (1 groupe = 1 h, 1 pastille = 15 min). Repère de reset (encoche ou étincelle) à la fin de la partie allumée ; heure de reset en texte via `resets_at` exact. Exposer `ResetsAt` dans `WindowGaugeViewModel`.

5. `EstPleinEcran` : rangées proportionnelles, défilement coupé, dictionnaire « plein écran » pour les polices ; pas de Viewbox global. Échap : petite fonction pure testée ; bouton de sortie. D'abord retirer Simplifié/Tuiles ; supprimer la propriété, pas réduire l'enum.

6. Vérifié : `--statusline` sort avant le mutex. Le réconciliateur repointe vers l'exe courant → inverser. (1) Liste validée ; (2) suppressions sans risque dans l'ordre ; (3) 3.5.0 : réconciliateur restaure `statusLine = InnerStatusLineCommand` ou retire la clé, sauvegarde ; hooks toujours repointés ; `--statusline` relais pur sans `usage.json` ; watchers A5/A7 supprimés ; (4) garder `InnerStatusLineCommand` en 3.5.0 ; B2 et relais en 3.6.

7. Pâle = sombres désaturés (Nord, Forêt, Moka, Roseraie, + Sauge). Clairs = chantier, proposés en option à part. Néon/Aurore gardent leur décor, rampe vert/ambre/rouge néon. Gardes : teinte rouge [340°, 20°], vert [80°, 170°], épuisé ≥ 3:1 contre le disque. `Categorie` sur `ChronosTheme` ; test « 9 » → compte par catégorie. ≤ 2 nouvelles palettes par catégorie, galerie de captures avant intégration.

8. P0 Socle (lecture tolérante des enums par propriété, tests « Tuiles », style inconnu, clé de thème inconnue) ; P1 R5 + migration ⛔ liste ; P2 R4 ; P3 R6 ⛔ palettes ; DAEDALUS R1/R2/R3/R7 ⛔ ; P4 R1+R2 + recalage ⛔ taille sur capture ; P5 R3 ; P6 R7 + recette ; release 3.5.0. Séquentiel, sans worktree parallèle.

Hypothèses : +20 % sur l'affiché ; la sonde reste la source réelle ; pas de Normal/Étendu pour Fusible/Volets. Objection : fenêtre variable touche le point le plus fragile (placement, recalage, restauration, aperçu, tests figés à 170) ; alternative = fenêtre fixe agrandie 210 × 210 avec Viewbox recalibrés, moins risquée mais plus de zones transparentes et empreinte inadaptée — justement le défaut de R7. Second contre-argument : commencer par la purge retarde la valeur visible ; accepté.
