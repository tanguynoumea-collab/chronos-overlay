# DESIGN_PLAN — Chronos v1.8 « Historique d'utilisation » (cycle ZEUS n°1)

Statut : **validé par l'utilisateur le 2026-09-27** (checkpoint humain 1), sur les maquettes Figma
https://www.figma.com/design/O8WVDejfdPcJv6314a7h6k (frames A à F). Mode DAEDALUS : ÉVOLUTION (UI acceptée,
delta seulement). Ce document est contractuel pour GSD : mêmes mots pour mêmes choses, tokens via ressources,
aucune valeur en dur.

Décisions de l'utilisateur : **A retenue, B et C codées aussi et sélectionnables** (style de la vue Semaine) ;
**vues Jour et 4 semaines conservées** dans le milestone ; **deux gestes d'ouverture** (bouton des réglages +
double-clic au centre du cadran) ; **phase 32 publiée seule en 3.2.2** avant toute interface.

## 1. Ce qui existe et ne change pas

- Cadran (`MainWindow`, 170 × 170, layered, topmost) : inchangé, sauf le geste double-clic au centre (`CentreHit`)
  qui ouvre la fenêtre Historique. Le simple clic centre (bascule % / temps) est conservé : le double-clic ne doit
  pas déclencher deux bascules (temporisation ou annulation de la première).
- Fenêtre de réglages (`SettingsWindow`, 330 px, chrome `Panel/Panel2/Line/Ink/Ink2/Accent`) : reçoit une carte
  dans la section DONNÉES (voir §6).
- Widget de sessions, diagnostic, doctrine « exact ou rien » : inchangés. Le diagnostic gagne une section
  « Journal d'historique » (âge de la dernière écriture, nombre de relevés du jour, état de la reconstruction).

## 2. Fenêtre « Historique »

| Propriété | Valeur | Pourquoi |
|---|---|---|
| Classe | `Views/HistoriqueWindow.xaml` + `ViewModels/HistoriqueViewModel` | famille des fenêtres secondaires |
| Chrome | `WindowStyle=None`, **`AllowsTransparency=False`**, `Topmost=False`, `ShowInTaskbar=True`, redimensionnable (grip), coins 16, ombre | fenêtre de consultation, pas d'overlay : pas de coût de composition layered |
| Taille par défaut / minimale | 920 × 610 / 760 × 480 | maquette A ; mémorisées dans `settings.json` (`HistoriqueX/Y/Width/Height`) |
| Palette | mêmes clés que `SettingsWindow` (`Panel #151322`, `Panel2 #1E1B30`, `Line #2C2942`, `Ink #F2F0FB`, `Ink2 #A9A6C4`, `Accent #8B7BF0`, `Ok #4EC98A`) **extraites dans `Resources/DesignTokens.xaml`** (aujourd'hui locales à SettingsWindow → à promouvoir, sans changer les valeurs) | une seule source de vérité |
| Rampe | `UtilizationToBrushConverter` du thème actif (Aurore : `#4FD1C5 → #6D8CF0 → #C86BE0`) | même loi que le cadran : temps = géométrie, quota = luminance |
| Neutres tokens | `HistoTokens #8C89A8`, `HistoSousAgent #5A5776`, `HistoModele1/2/3 #9D9AB8 / #6E6B8C / #4A4762` (nouveaux tokens) | les tokens ne portent JAMAIS la rampe de quota |
| Police | Segoe UI (comme les réglages) ; tailles 16 / 14 / 11,5 / 11 / 10,5 / 9,5 / 9 / 8,5 | maquettes en Inter, mêmes corps |
| Fermeture | ✕, Échap ; position/taille sauvegardées à la fermeture | |

### 2.0 Amendements techniques (orchestrateur, 2026-09-27, après la recherche de phase 34)
- **Coins et ombre** : `AllowsTransparency=False` (voulu : pas de fenêtre layered) interdit les coins 16 px dessinés et l'ombre WPF.
  Retenu : `System.Windows.Shell.WindowChrome` (bords de redimensionnement, ombre DWM) + coins arrondis DWM best-effort sur
  Windows 11 (`DWMWCP_ROUND`, rayon système ≈ 8 px) ; coins droits sur Windows 10. Pas de NuGet.
- **Rampe des pistes** : `ChronosTheme.ArcColor` du thème ACTIF (même loi que le cadran), pas `UtilizationToBrushConverter` (rampe
  Minuit fixe). `HistoGris = #5A5960` (le gris « épuisé » déjà utilisé par le cadran).
- **Segment « 4 semaines »** : présent mais désactivé avec l'infobulle « bientôt (phase 35) » tant que la vue n'existe pas — jamais
  un clic inerte.
- **Tokens de taille** : les corps de texte, hauteurs de pistes (§2.2) et épaisseurs deviennent des `sys:Double` de
  `DesignTokens.xaml` ; les hauteurs sont vérifiées par un test de mise en page réel (Measure/Arrange), pas dupliquées en C#.
- **Divergence « consommé ailleurs »** : Δ hebdo ≥ 0,01 sur une heure sans tranche de tokens couverte — constante nommée,
  recalable après le constat de la phase 35.

### 2.1 En-tête (identique sur toutes les vues)

```
[● Chronos]  Historique  [v3.3.0]                                                         ✕
[ Jour | Semaine | 4 semaines ]   ‹  <libellé de période>  ›                 [ Cette semaine / Aujourd'hui ]
Dernier relevé il y a N min · <source> · N relevés · N interruptions · journal ouvert le <date>
```

- Segment de vue (3 boutons, actif = bordure `Accent` 1,5 px sur `Panel`), navigation ‹ › par unité de la vue,
  bouton de retour au présent. Libellés de période :
  - Semaine : « Semaine de forfait · sam. 19 sept. 00:00 → sam. 26 sept. 00:00 » (bornes issues de `resets_at` 7 j
    du journal ; repli : `WeeklyAnchor`).
  - Jour : « Jour · jeudi 24 sept. 2026 · semaine de forfait du 19 sept. ».
  - 4 semaines : « 4 semaines de forfait · du sam. 29 août au sam. 26 sept. 2026 ».
- Ligne de fraîcheur : même règle de doctrine que le cadran (« il y a N min », source nommée). Si l'âge de la
  dernière **écriture du journal** dépasse 15 min alors que Chronos tourne : pastille `Alerte` + « journal muet
  depuis N min » (leçon du relevé figé).

### 2.2 Vue « Semaine de forfait » (défaut) — trois styles sélectionnables

Réglage `HistoriqueStyleSemaine` ∈ { `Pistes` (A, défaut), `Simplifie` (B), `Tuiles` (C) }, bascule dans la
fenêtre (petit sélecteur à droite de la ligne de fraîcheur : « Style : Pistes · Simplifié · Tuiles ») ET dans la
carte des réglages. Toutes les pistes partagent l'axe X (samedi 00:00 → samedi 00:00, grille verticale par jour,
libellés « sam. 19 … ven. 25 », « reset hebdo → » à droite) et un **réticule vertical commun** au survol.

| Piste | A Pistes | B Simplifié | C Tuiles | Contenu |
|---|---|---|---|---|
| NIVEAU | 150 px | 200 px | 120 px | % hebdo en **escalier** (2,2 px, couleur = niveau via la rampe, coupé aux trous) ; grille 0 / 50 / 100 % ; **semaine précédente** en escalier gris pointillé ; en A et B : **% 5 h en dents de scie** (1 px, `TickReset`, retour à 0 au reset) + **tirets de reset 5 h** (8 px, `TickReset`) en bas de piste ; en C : hebdo seul |
| FENÊTRES 5 H | — | — | 62 px | une **tuile** par fenêtre : x du premier relevé actif au `resets_at` observé, hauteur = max % 5 h, couleur = rampe, **gris `HistoGris` si 100 %** (« épuisée »), tiret de reset à droite |
| RYTHME | 72 px | — | 58 px | barres par heure, hauteur = Δ du compteur 5 h entre relevés consécutifs de même `resets_at` (jamais à travers un reset), couleur = rampe au niveau 5 h atteint ; échelle « 0 – 25 % » |
| TOKENS CLAUDE CODE | 72 px | 90 px | 58 px | barres par heure, `HistoTokens` + part sous-agents `HistoSousAgent` empilée ; axe propre (« 0 – 1,2 M ») ; libellé permanent : « par heure, comptés localement — hors Cowork et claude.ai · bruts, non pondérés · ce n'est PAS un % du forfait » ; légende « ▮ principal ▮ sous-agents » |
| COUVERTURE | 12 px | 12 px | 12 px | bande continue : `Ok` 55 % = relevé présent ; `Line` = Chronos arrêté ; `Alerte`/rampe ambre = jeton invalide ou sonde refusée ; toujours visible, jamais repliable |

Éléments d'honnêteté communs (obligatoires, testés) :
- **Trou** (> 2 cadences, soit 10 min, sans relevé) : la ligne s'interrompt ; rectangle `Line` 35 % + bordure
  pointillée (grise si « arrêté », ambre si « jeton ») + libellé (« Chronos arrêté », « jeton invalide »).
- **Saut non localisé** : entre le dernier relevé avant un trou et le premier après, bloc plat gris de la hauteur
  du Δ, annotation « +N % pendant l'absence (répartition inconnue) ». Jamais une barre au réveil.
- **Divergence** : une marche de % sans tokens Code est encadrée en pointillé `Accent` ; pied de page : « cadre
  violet : marches de % sans tokens Code = consommé ailleurs (Cowork, claude.ai) ».
- **Avant le journal** : zone vide, marqueur « journal ouvert le <date> ». Jamais de courbe reconstituée.
- Pied de page fixe : « Aucun trou n'est interpolé. Les pourcentages sont des relevés exacts du serveur ; les
  tokens sont un comptage local partiel, sur leur propre axe. »

### 2.3 Vue « Jour »

- X = 0 h → 24 h locale (grille toutes les 3 h), grain **5 min** (un relevé = un point).
- NIVEAU 190 px : **% 5 h au premier plan** (2,4 px, couleur = niveau, **gris à 100 %** avec libellé « épuisée à
  100 % — le serveur refuse (statut rejected) ») ; % hebdo en trait fin `Ink2` ; trait vertical `TickReset` à
  chaque reset observé avec « reset 5 h HH:MM » ; trous comme en Semaine.
- RYTHME 64 px : Δ 5 h par heure. TOKENS 64 px : par **quart d'heure**, empilés par **modèle** (`HistoModele1/2/3`,
  légende « opus · sonnet · haiku · sous-agents inclus »). COUVERTURE 12 px.
- Ligne de fraîcheur : « 288 relevés attendus · N présents · N interruption(s) (cause, HH:MM → HH:MM) ».
- Réticule : infobulle `Panel2` : « HH:MM · relevé exact / 5 h : N % — reset à HH:MM / hebdo : N % / source · âge ».
- Bouton « Aujourd'hui » ; ligne « maintenant » `Ink` 60 % si le jour affiché est aujourd'hui.

### 2.4 Vue « 4 semaines »

- X = samedi → samedi (7 colonnes « sam. … ven. »), quatre courbes hebdo superposées : **courante en couleur**
  (rampe), S-1 / S-2 / S-3 en gris `HistoGris` aux opacités 0,8 / 0,45 / 0,25 ; étiquettes à droite (libellé de
  semaine + valeur finale ou « pas de relevés (avant le journal) »).
- Semaine **épuisée** : plateau 100 % en gris avec annotation « épuisée <jour> HH:MM → bloquée jusqu'au reset ».
- COUVERTURE PAR SEMAINE : quatre bandes (S, S-1, S-2, S-3), mêmes couleurs ; « avant le journal — aucun relevé »
  pour les semaines antérieures ; marqueur « journal ouvert <date> » sur la semaine d'ouverture.
- Pas de piste tokens dans cette vue (v1.8). Pied : « Rien n'est inventé avant l'ouverture du journal. »

## 3. Données affichées (contrat avec la couche services)

- Relevés : `%APPDATA%\Chronos\historique\releves-AAAA-MM.jsonl`, une ligne par relevé exact **distinct**
  (dédup `CapturedAt` strictement croissant ; idempotent multi-instances) : `{v, t, u5, r5, u7, r7, statut5,
  statut7, overage, source}` + événements `{v, t, ev: demarrage|arret|jeton_invalide|sonde_refusee|reprise}`.
- Tokens : `historique\tokens-AAAA-MM.jsonl`, une ligne par (tranche 15 min **UTC**, modèle, sous-agent) :
  `{v, slot, model, sub, in, out, cache_w, cache_r, n}` ; `curseurs.json` `{chemin → offset, taille, mtime}` ;
  **dédup par `message.id` (repli `requestId`) par fichier**.
- Reconstruction : service de fond, thread `BelowNormal`, mtime décroissant, progression exposée au VM →
  bandeau F2 dans la fenêtre : « Reconstruction des tokens depuis vos transcripts Claude Code — N / M fichiers ·
  la semaine courante est déjà complète », barre `Accent`, sous-texte « en arrière-plan, priorité basse · du plus
  récent au plus ancien · les pourcentages du forfait ne se reconstruisent pas : ils commencent au <date> ».
- Toute la géométrie (temps → x, valeur → y, binning, détection des trous, tuiles, segments par bande de rampe,
  réduction min/max par colonne au-delà de 4 000 points) vit dans `Rendering/` en classes **pures**, testées
  comme `ArcGeometry`. Les pistes sont des `FrameworkElement` à `OnRender` + `StreamGeometry` gelée ;
  redessin sur changement de données ou de plage, jamais sur le tick 1 s.

## 4. Vocabulaire (mêmes mots partout : fenêtre, réglages, diagnostic, docs)

Historique · Semaine de forfait · Jour · 4 semaines · Niveau · Rythme · Tokens Claude Code · Couverture ·
Fenêtres 5 h · relevé (jamais « mesure », jamais « estimation ») · reset 5 h / reset hebdo · trou · « Chronos
arrêté » · « jeton invalide » · « sonde refusée » · « épuisée » · « répartition inconnue » · « consommé ailleurs » ·
« journal ouvert le … » · « dernière écriture » · style : Pistes / Simplifié / Tuiles.

## 5. Gestes et raccourcis

| Geste | Effet |
|---|---|
| Bouton « Ouvrir » (carte des réglages) | ouvre / ramène au premier plan la fenêtre Historique |
| Double-clic au centre du cadran | idem ; le simple clic centre garde sa bascule % / temps |
| ‹ › | période précédente / suivante (unité de la vue) |
| « Cette semaine » / « Aujourd'hui » | retour au présent |
| Survol | réticule + infobulle ; clic sur un jour en vue Semaine = zoom Jour ; Échap = retour puis fermeture |
| Molette sur le segment | change de vue (optionnel, non bloquant) |

## 6. Carte « Historique d'utilisation » dans les réglages (F1)

Section DONNÉES, après « Sonde d'en-têtes » : bordure `Accent` 1,5 px tant que la fonctionnalité est nouvelle
(première version), titre « Historique d'utilisation », sous-texte « hebdo / 5 h / tokens · journal du <date> ·
dernière écriture il y a N min », bouton « Ouvrir », ligne « Style de la vue Semaine : Pistes · Simplifié ·
Tuiles », ligne « Aussi : double-clic au centre du cadran ». Carte d'état : « Dernière écriture du journal : il y
a N min », pastille `Alerte` + « alerte si > 15 min alors que Chronos tourne ».

## 7. Auto-critique (DAEDALUS) et réponses

- *Quatre pistes, c'est dense.* → B « Simplifié » existe et se choisit en un clic ; A reste le défaut parce que la
  piste Rythme est la seule réponse à « quand je brûle ».
- *Deux % sur le même axe (5 h et hebdo) peuvent se lire comme comparables.* → légende explicite « % de sa
  propre fenêtre », dents de scie en 1 px neutre contre escalier 2,2 px coloré, et le style C les sépare.
- *Les tokens sous les % invitent à un ratio.* → axes, palettes et vocabulaires distincts ; libellé permanent ;
  garde structurelle de test : aucun type de la couche historique n'expose un % dérivé de tokens.
- *Le journal peut se taire sans bruit (précédent du relevé figé).* → âge de dernière écriture en première
  classe (fenêtre, réglages, diagnostic) + événements de couverture.
- *Fenêtre opaque non topmost : on peut la perdre derrière l'app Claude.* → `ShowInTaskbar=True`, rappel par
  les deux gestes qui la ramènent au premier plan.

## 8. Critères de revue visuelle (DESIGN-REVIEW, phase 6 ZEUS)

1. Chaque style de Semaine (A/B/C) capturé avec les mêmes données de fixture : trous, saut non localisé, fenêtre
   épuisée, divergence tokens, semaine précédente.
2. Vue Jour avec reset 5 h, plateau épuisé et infobulle ; vue 4 semaines avec semaine épuisée et semaines
   antérieures au journal.
3. Aucune valeur de couleur/taille hors `DesignTokens.xaml` ; aucun texte tronqué ; lisible à 100 % et 150 %
   d'échelle Windows ; redimensionnement 760 → 1 400 px sans chevauchement.
4. Les libellés d'honnêteté sont présents sur chaque vue, mot pour mot (§2.2, §4).
