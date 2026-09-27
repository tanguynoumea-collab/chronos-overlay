# DESIGN_PLAN — Chronos · fenêtre de réglages (REFONTE)

> Statut : ☐ Brouillon ☑ Auto-critiqué (Passe B) ☐ **VALIDÉ PAR L'UTILISATEUR** (obligatoire avant tout XAML)
> Version : 1.0 — 2026-09-27 · Mode DAEDALUS : **REFONTE** · Ampleur : **REFONTE TOTALE de cette fenêtre**, décidée par
> l'utilisateur (« redesigne-la entièrement, fenêtre rectangulaire classique, redimensionnable dynamiquement »).
> Maquettes : `.zeus/maquettes/reglages-v2.html` (R1 à R4, référence). Le quota Figma du forfait Starter a été atteint après le
> premier cadre (page « Réglages v2 » du fichier O8WVDejfdPcJv6314a7h6k) : ce cadre Figma est un brouillon non corrigé, l'HTML fait foi.

## 0. Audit de l'existant (résumé)

**Verdict :** un popover de 330 px de large qui a grossi phase après phase jusqu'à dépasser la hauteur de l'écran ; il n'a ni
défilement ni redimensionnement, et se ferme au premier clic ailleurs.

| Gravité | Défaut | Heuristique | Impact |
|---|---|---|---|
| CRITIQUE | `SizeToContent`, `ResizeMode="NoResize"`, `Width="330"`, aucun `ScrollViewer` : ≈ 1 400 px de haut (6 sections, 9 vignettes de thème) | §7 fenêtrage | le bas du panneau (Réglages, Quitter) est hors écran sur un 1080p |
| CRITIQUE | Se ferme sur `Deactivated` (comportement de menu) | §7, §4 | impossible d'en faire une vraie fenêtre ; une boîte de dialogue ou un clic de redimensionnement la ferait disparaître |
| MAJEUR | Diagnostic affiché dans un `MessageBox` | §5, §7 | rapport de plusieurs centaines de lignes, sans défilement ni copie |
| MAJEUR | Une seule colonne : 6 sections, ≈ 25 réglages au même niveau | §3 (7±2) | on cherche en faisant défiler, rien ne regroupe |
| MAJEUR | `Owner` = le cadran topmost | §7 | la fenêtre reste au-dessus de tout, on ne peut pas la mettre derrière |
| MINEUR | 12 littéraux de couleur (dont `#E8907F` de « Quitter ») et 30 tailles de police en dur | contrat de tokens | dérive visuelle, pas de thème |
| MINEUR | « Quitter Chronos » collé à « Diagnostic… » en bas de liste | §2 (Fitts, action destructive) | action destructive au milieu d'actions anodines |
| MINEUR | Libellés d'implémentation : « Source terminal », « RÉGLAGES » dans la fenêtre de réglages | §3 vocabulaire | on ne sait pas ce que fait « Source terminal » |

**À préserver :** la palette sombre et ses tokens (Panel, Panel2, Line, Ink, Ink2, Accent, Ok, Alerte) ; les vignettes de thème
avec leurs trois pastilles de rampe ; les interrupteurs ; la carte « Historique d'utilisation » (35-02) telle quelle ; le coût de
la sonde annoncé en clair ; l'ouverture au clic droit sur le cadran (habitude saine, inchangée).

## 1. Ancrage

- **Sujet :** régler un instrument de bord posé sur le bureau (Chronos) : d'où viennent ses chiffres, à quoi il ressemble, ce
  qu'il fait au démarrage.
- **Utilisateur :** l'utilisateur intensif de Claude qui ouvre les réglages rarement (quelques fois par semaine), pour UNE
  chose précise, puis referme.
- **Job de l'écran :** trouver un réglage en un coup d'œil, le changer, voir l'effet, refermer.
- **Densité :** aérée et guidée (usage occasionnel) — mais sans défilement inutile à la taille par défaut.

## 2. Tokens

Aucune couleur nouvelle : la palette de `DesignTokens.xaml` (promue en 34-01) est le contrat. Ajouts :

| Token | Valeur | Rôle |
|---|---|---|
| `Danger` (brosse) | `#E8907F` (valeur actuelle de « Quitter », inchangée) | action destructive uniquement |
| `ReglagesRailLargeur` | 208 | largeur fixe du rail de navigation |
| `ReglagesLargeurMin` / `ReglagesHauteurMin` | 640 / 440 | taille minimale |
| `ReglagesLargeurDefaut` / `ReglagesHauteurDefaut` | 860 / 580 | taille par défaut |
| `ReglagesContenuMax` | 640 | largeur maximale lisible de la colonne de contenu (centrée au-delà) |
| Typo | Titre de section 18 · Titre de carte 13 · Corps/aide 11 · Libellé du rail 12 | `sys:Double` |
| Espacement | 4 / 8 / 12 / 16 / 24 | grille de 4 px |
| Rayons | cartes 11, puces 9, fenêtre : coins DWM (comme Historique) | |

Fondation : WPF pur + tokens du dépôt (pas de bibliothèque tierce), police Segoe UI, chrome identique à `HistoriqueWindow`
(`WindowChrome`, coins DWM best-effort) : les deux fenêtres de consultation de Chronos se ressemblent.

## 3. Wireframes

### R1 — Taille par défaut (860 × 580), section « Apparence »

```
┌───────────────────────────────────────────────────────────────────────────── ─ □ ✕ ┐
│ ● Chronos  Réglages                                                    v3.3.1       │  ← barre de titre (drag, double-clic = agrandir)
├──────────────────┬──────────────────────────────────────────────────────────────────┤
│ ◉ Données        │  Apparence                                                       │  ← 1er : titre de section
│ ◷ Historique     │  ┌──────────── aperçu vivant ────────────┐                       │  ← 2e : SIGNATURE — le cadran réel
│ ◐ Apparence   ●  │  │        (cadran rendu avec le thème     │   Thème actif : Aurore│
│ ☰ Sessions       │  │         et le style choisis)           │   Style : Anneaux     │
│ ⚙ Comportement   │  └────────────────────────────────────────┘                       │
│ ▤ Diagnostic     │  THÈME                                                            │  ← 3e : les choix
│                  │  [Minuit][Ardoise][Nord][Néon][Aurore●][Ambre chaud][Moka][Roseraie][Forêt]   │  (vignettes en WrapPanel :
│                  │  STYLE DU CADRAN                                                  │   4 à 7 par ligne selon largeur)
│                  │  [Anneaux●][Braises][Fusible][Marée][Volets]                                     │
│                  │  ┌ Mode étendu — 3 anneaux (style Anneaux seulement)     [◯━] ┐  │
│                  │                                                         ▒ défile │
├──────────────────┤                                                                  │
│ ⏻ Quitter Chronos│                                                                  │  ← action destructive isolée, en bas du rail
└──────────────────┴──────────────────────────────────────────────────────────────────┘
                                                                                  ◢ poignée
```

### R2 — Taille minimale (640 × 440), section « Données »

```
┌─────────────────────────────────────────────── ─ □ ✕ ┐
│ ● Chronos  Réglages                         v3.3.1   │
├───────────────┬──────────────────────────────────────┤
│ ◉ Données  ●  │ Données                              │
│ ◷ Historique  │ ┌ Connexion Claude       [Connecter]┐│
│ ◐ Apparence   │ ┌ Sonde d'en-têtes            [━◯] ┐ ││
│ ☰ Sessions    │ │ coût annoncé · état de la sonde  │ ││
│ ⚙ Comportement│ ┌ Barre de statut de Claude Code    ┐ ▒│  ← ex-« Source terminal », renommé
│ ▤ Diagnostic  │ │ pont statusLine (terminal) [━◯]   │ ▒│
├───────────────┤                                    ▒ │
│ ⏻ Quitter     │                                      │
└───────────────┴──────────────────────────────────────┘
```
Le rail ne rétrécit jamais ; seule la colonne de contenu se réduit puis défile. Aucune vignette n'est coupée : le WrapPanel
passe à 3 vignettes par ligne.

### R3 — « Diagnostic » (remplace le MessageBox)

```
│ Diagnostic                                   [↻ Actualiser] [⧉ Copier] │
│ ┌──────────────────────────────────────────────────────────────────┐ │
│ │ === Chronos — Diagnostic ===  (police mono, lecture seule,       │ │  ← occupe toute la hauteur,
│ │ défile, sélectionnable)                                          │ │     s'étire avec la fenêtre
│ └──────────────────────────────────────────────────────────────────┘ │
│ Généré à 21:04 · 412 lignes                                          │
```

### R4 — Sections « Historique », « Sessions », « Comportement » (contenu, rail identique)

- **Historique** : la carte F1 de 35-02 inchangée (Ouvrir, dernière écriture + alerte, style de la vue Semaine, mention du
  double-clic).
- **Sessions** : interrupteur « Widget sessions Claude Code » en tête ; si activé : aperçu vivant du widget (signature, variante),
  vignettes de style, « Disposition verticale ». Si désactivé : le reste est grisé avec « Active le widget pour choisir son style ».
- **Comportement** : « Arrière-plan » (le cadran passe derrière les fenêtres), « Lancer au démarrage », « Recalibrer le reset
  hebdomadaire… ».

Hiérarchie de lecture (toutes sections) : 1er titre de section · 2e le premier réglage (le plus fréquent) · 3e l'aide grise.

## 4. Navigation et fenêtrage

```
clic droit sur le cadran ─▶ Réglages (singleton : ramené au premier plan s'il est ouvert ; dernière section rouverte)
Réglages ─ rail ─▶ 6 sections (clic, ↑/↓ dans le rail, Ctrl+1…6)      Échap ou ✕ ─▶ ferme
Historique ─ « Ouvrir » ─▶ HistoriqueWindow                         Diagnostic ─ « Copier » ─▶ presse-papiers
```

- Fenêtre **classique** : rectangulaire, redimensionnable par les bords et la poignée, agrandissable, **ne se ferme plus quand
  elle perd le focus**, **n'est plus possédée par le cadran** (peut passer derrière les autres fenêtres), visible dans la barre des
  tâches, taille, position et dernière section **mémorisées** dans `settings.json`, bornées à l'écran au rétablissement.
- Ouvreur singleton (`IOuvreurReglages`, même principe que `IOuvreurHistorique` de 35-02).

### Mapping d'iso-fonctionnalité (inventaire complet, rien ne disparaît)

| Fonction | Emplacement actuel | Nouvel emplacement |
|---|---|---|
| Connexion Claude (login OAuth) | Données | Données |
| Sonde d'en-têtes (interrupteur, coût, état) | Données | Données |
| Source terminal (`ToggleStatusLineSourceCommand`) | Réglages (bouton) | Données — renommé « Barre de statut de Claude Code », interrupteur avec l'aide « installe ou retire le pont statusLine dans les réglages de Claude Code (utile en terminal) » |
| Historique : Ouvrir, dernière écriture, alerte, style Semaine, mention | Données (carte F1) | Historique (carte inchangée) |
| Thème (9) | Thème | Apparence |
| Style du cadran (5 : Anneaux, Braises, Fusible, Marée, Volets) | Cadran | Apparence |
| Mode étendu (Anneaux) | Affichage | Apparence, sous le style (visible pour Anneaux seulement, comme aujourd'hui) |
| Arrière-plan | Affichage | Comportement |
| Lancer au démarrage | Affichage | Comportement |
| Widget sessions Claude Code | Affichage | Sessions (en tête) |
| Style du widget de sessions | Sessions | Sessions |
| Disposition verticale | Sessions | Sessions |
| Recalibrer hebdo… | Réglages | Comportement — « Recalibrer le reset hebdomadaire… » |
| Diagnostic… | Réglages (MessageBox) | Diagnostic (panneau, actualiser, copier) |
| Quitter Chronos | bas de liste | bas du rail, isolé, couleur `Danger` |
| Version | en-tête | barre de titre |
| Fermer | ✕ | ✕ + Échap |

## 5. États non-nominaux

| Section | Vide / désactivé | Chargement | Erreur |
|---|---|---|---|
| Données | non connecté : « Connecte Chronos à ton compte Claude pour des chiffres exacts » + bouton | connexion : bouton « Connexion… » désactivé | échec : phrase de cause (existant) + « Réessayer » |
| Sessions | widget désactivé : réglages grisés + phrase d'invitation | — | — |
| Diagnostic | — | « Génération du rapport… » (asynchrone, jamais sur le thread UI) | « Le diagnostic a échoué : <cause> » + « Réessayer » |
| Historique | journal pas encore ouvert : « aucun relevé pour l'instant » (existant) | — | alerte « journal muet » (existant) |

## 6. Élément signature

**L'aperçu vivant.** En tête de « Apparence », le cadran lui-même, rendu en grand avec le thème et le style sélectionnés,
change à l'instant du clic sur une vignette. Même principe en tête de « Sessions » pour le widget. On règle un instrument en le
regardant, pas en lisant une liste de noms. Les moteurs d'aperçu existent déjà (`CadranPreviewViewModel`,
`SessionsPreviewViewModel`, galeries) : la signature coûte peu et elle est propre à Chronos.

## 7. Motion

Aucune animation de transition entre sections (réglages ouverts rarement, on veut l'instantané). Le seul mouvement est celui de
l'aperçu vivant qui se redessine.

## 8. Auto-critique (Passe B)

- **Générique ?** Le rail + colonne est le modèle classique des réglages Windows 11 ; c'est voulu (« fenêtre rectangulaire
  classique » est la demande) et c'est ce qui le rend lisible sans apprentissage. Ce qui n'est pas générique : l'aperçu vivant du
  cadran et du widget, le coût de la sonde annoncé, le diagnostic lisible et copiable, la barre de titre partagée avec Historique.
- **Heuristiques :** 6 entrées de rail (≤ 7) ; action destructive isolée ; Échap ferme ; ↑/↓ et Ctrl+1…6 ; focus visible sur le
  rail et les vignettes ; taille minimale testée (640 × 440) ; état persistant ; tout texte en tokens. Point faible assumé : à
  640 px de large, la section Apparence défile (aperçu + 9 vignettes ne tiennent pas en 440 px de haut) — acceptable, c'est la
  taille minimale.
- **Accessoire retiré (Chanel) :** pas d'icône colorée par section, pas de recherche de réglages (25 réglages rangés en 6
  sections n'en ont pas besoin), pas d'animation de transition.

## 9. Journal des choix rejetés

| Proposition | Raison | Date |
|---|---|---|
| Garder le popover en ajoutant un simple `ScrollViewer` | ne répond pas à la demande (fenêtre classique redimensionnable) ; la fermeture au défocus reste | 2026-09-27 |
| Onglets horizontaux en haut | 6 onglets + titres longs ne tiennent pas à 640 px ; le rail vertical tient à toutes les tailles | 2026-09-27 |
| Barre de titre Windows native (claire) | casserait la cohérence avec la fenêtre Historique sombre | 2026-09-27 |
| Champ de recherche de réglages | inutile pour 25 réglages en 6 sections | 2026-09-27 |
