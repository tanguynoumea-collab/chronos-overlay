# Roadmap — version suivante (après 3.5.0)

Retour roadmap du cycle ZEUS n° 2, confirmé par l'utilisateur le 2026-10-06. **Consigné seulement : aucun code n'est
commencé.** Sources : `.zeus/state.json` (`pending.pour_roadmap`), rapports `.zeus/reports/dev-council-2026-10-03.md`,
`dev-senior-1.md` à `dev-senior-3.md`, `.zeus/reports/cycle2/design-review-*.md`, `43-CONSTAT.md` (§8 Écarts).
Les points réglés pendant le cycle 2 (R1-R7, anneau hebdo à 12 braises, gris épuisé, « trois composites ») sont retirés.

---

## 1. Urgent (échéance)
- **Migration .NET 8 → .NET 10 avant le 10/11/2026** (fin de support de .NET 8) — PKG-3.

## 2. Honnêteté des chiffres et sessions
- **Grille des resets 5 h sur les resets observés** : la timeline 24 h des Anneaux et l'anneau « journée » de Braises projettent
  la grille de 5 h en 5 h depuis le seul reset courant (DS-ARCH-05) → la caler sur les resets réellement observés (journal).
- **Braises « journée »** : vides plus étroits (18-22,5° au lieu de 25,5-27°) quand la première limite du jour tombe entre 00:30
  et 03:30 (43-09).
- **Reconnexion par la pastille avec un AUTRE compte** : l'ancien relevé peut rester « exact » jusqu'à ~5 min (DS3-01) — comparer
  l'identité du compte avant/après et oublier si elle change.
- **Coffre OAuth, trois cas rares** :
  - le login doit écrire `oauth.dat` sous le verrou de `ChronosTokenAuthority` (cas hérité, 42.4-01-SUMMARY) ;
  - signaux d'état (`InvaliderAccessToken`, `SignalerSucces`, `SignalerRefusServeur`) hors verrou et sans génération (DS3-03) ;
  - relecture `null` du coffre juste après écriture → effacement sauté (DS3-04).
- **Arcs gris après déconnexion** : gardent les resets de l'ancien compte ≤ 5 min ; vrai chiffre masqué ≤ 5 min après une
  reconnexion rapide (DS3-05).
- **`CLAUDE_CONFIG_DIR`** : suivre le dossier des transcripts quand Claude Code le déplace (DS3-02, aujourd'hui documenté).

## 3. Diagnostic et documentation
- « Dernière sonde : cadence bornée… » cache la dernière requête réussie (E43-5).
- « Connecté : OUI — les chiffres exacts arrivent au prochain rafraîchissement » contredit la ligne suivante (E43-6).
- Libellé périmé « menu clic droit → Se connecter à Claude » (le chemin est Réglages → Données → Se connecter).
- `docs/publish.md` annonce des tags `exe-vX.Y.Z` ; les releases utilisent `vX.Y.Z` (v3.5.0) → aligner la doc et sa garde.
- PKG-R2 : afficher la cible du raccourci d'autostart dans le diagnostic.

## 4. Historique — fonctionnalités reportées
- Heatmap jour × heure des rythmes.
- Export CSV de la plage affichée.
- Compaction du journal des relevés au-delà de 8 semaines.
- Dimension projet dans les agrégats de tokens.
- Projection conditionnelle « à ce rythme » (seulement si visuellement distincte).
- Fenêtre hebdo : dérive d'une heure au changement d'heure — test d'acceptation le 25/10.

## 5. Retouches visuelles (mineurs de design review)
- DR1 M4 : hiérarchie des titres de groupe de thèmes (même style que « THÈME », étiquette peut-être redondante).
- DR1 M5/M8 : collisions d'annotations dans l'Historique (absence / maintenant).
- DR1 M6 : 4 semaines en plein écran, vide sous la couverture et légende étroite.
- DR1 M7 : légende des tokens cassée (carré orphelin).
- DR1 M12 : filet de charnière de la plaque Volets qui barre les chiffres.
- DR1 M14 : corps de police encore écrits en dur dans Arcs, Braises et MainWindow.
- DR2 R2-M1 : libellés 5 H / 7 J et cordon/bande épuisés de Fusible et Marée faibles sur fond gris-bleu moyen (~1,6-1,7:1).
- DR2 R2-M2 : cadran rectangulaire accroché en bas qui flotte 14 px au-dessus du bord (bande de pastilles).
- DR2 R2-M3 : cadrage de l'aperçu des réglages qui suppose fenêtre = cadran + bande.
- 40-07 : `SnapToNearestCorner` / `RestorePlacement` peuvent ramener brièvement au premier plan une fenêtre en mode arrière-plan.

## 6. Qualité et architecture
- ARCH-1 (MainViewModel-dieu), ARCH-3/MAINT-4 (dépendances optionnelles télescopiques), MAINT-2 (`SessionMonitor.Inspecter`),
  MAINT-1, ARCH-2/TEST-2 et DS2-05 (composition testable depuis le conteneur réel, au lieu des gardes de câblage textuelles),
  ARCH-4..8, ARCH-10, MAINT-3, MAINT-6..10.
- Trois lecteurs de transcripts aux règles divergentes à réunir (DS-DUP-01..04) ; cycles d'espaces de noms
  (Services↔Text, Services→Theming→Rendering).
- Lecture des transcripts par position pour les fichiers chauds, libération du cache hors mode dégradé (P-07 étape 2, DS2-04).
- DS-PERF-02 : scan du widget de sessions sur le thread UI toutes les 2 s.
- Outillage : `.editorconfig` + `dotnet format` (ARCH-11/MAINT-12), analyseurs Roslyn (ARCH-13), trous de tests TEST-1/5/6/7,
  test du lien symbolique ignoré sans mode développeur.
- Fiabilité et données : FIAB-5, FIAB-7 (énumération récursive sur thread UI), FIAB-R4 résiduel (marqueur transactionnel pour un
  mois gelé), FIAB-R6 (`LectureIncomplete` pour les agrégats de tokens), DATA-6, DATA-8..10, DATA-12, SEC-5..9, MAT-6,
  PERT-6..14, ARCH-9, galeries hors exe livré (ARCH-14/PERT-9).

## 7. Distribution
- Signature de l'exe (PKG-5/SEC-1).
- Build épinglé par `global.json` (PKG-4) ; CI (DS-MAINT-06).
- Dossier d'installation hors du dépôt (PKG-6, MAT-2).
- Sauvegardes de `~/.claude/settings.json` en clair, même ACL (SEC-R4).
