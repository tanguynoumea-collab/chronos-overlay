# Dev-team-council — Chronos, cycle ZEUS 2 (v1.9, avant release 3.5.0) — 2026-10-03

Audit complet : 8 rôles (Architecte, Mainteneur, Tests — Tier 1 ; Fiabilité, Sécurité, Données — Tier 2 ; Packaging,
Pertinence — Tier 3) ; agent Autodesk non activé (pas de RevitAPI). Puis sceptique, matrice de cross-challenge (4 paires),
arbitrage. Périmètre : `v1.8..HEAD` (cycle 2) + `937e980..v1.8` (cycle 1, jamais audité). Lecture seule, exe jamais lancé.
Détail par rôle : `.zeus/reports/cycle2/council/{architecte,mainteneur,tests,fiabilite,securite,donnees,packaging,pertinence,sceptique,matrice}.md`.

## 1. Verdict

**0 Bloquant · 8 Majeurs distincts (après challenge et dédoublonnage) · ≈ 45 Mineurs · ≈ 30 Info.**
Verdict : **livrable sous réserve** — aucun défaut rencontré en usage normal, mais une **cause racine de perte de données**
(MAT-3) et un **défaut de mise à jour** (PKG-1) méritent d'être corrigés avant la 3.5.0.

Phase 1 (outils) : build 0 avertissement ; 1 949 tests verts ; couverture 90,3 % lignes / 83,2 % branches
(Microsoft.CodeCoverage — Coverlet absent) ; 0 paquet NuGet vulnérable ; analyseurs Roslyn **non activés** (forcés : ≈ 63
avertissements CA, CC 75 sur `BuildReportAsync`, 32 sur `SessionMonitor.Inspecter`) ; `dotnet format` en échec (754
écarts d'espacement, pas de `.editorconfig`) ; aucun exe signé.

**Non vérifié (audit aveugle)** : Security Code Scan (absent) ; métriques de couplage continues (NDepend absent) ; latence du
mode `--hook` et comportement réel de l'exe (lancement interdit) ; probabilité réelle d'un verrou antivirus sur
`settings.json` ; réaction de Claude Code à une commande de hook introuvable.

## 2. Arbitrage (§6.3, mode dégradé par l'orchestrateur — contestable au triage)

| Débat | Positions | Décision | Raison |
|---|---|---|---|
| DATA-1 Bloquant ? | Données : Bloquant ; sceptique et matrice : Majeur | **Majeur** | Mécanisme réel, déclencheur hors usage normal (0/1 491 transcripts récents) mais déclenchable par une future évolution du format des curseurs. |
| ARCH-2/TEST-2 | Matrice : Majeur ; sceptique : Mineur | **Mineur**, traité avec ARCH-3 | Le mode de panne dominant est bruyant (service manquant → exception au 1er lancement) ; la part silencieuse est la racine d'ARCH-3. |
| ARCH-1 (MainViewModel-dieu) | Architecte : Majeur ; sceptique : Mineur par défaut | **Mineur → roadmap** | VM couvert à 95,8 % ; le refactor est un choix de cycle, pas un défaut de livraison. La façade de réglages qu'il propose est en revanche le correctif de MAT-3 (voir plan). |

## 3. Plan de remédiation priorisé (proposition — à trier par l'utilisateur, checkpoint 2)

### Lot A — « une lecture ratée n'écrase jamais rien » (cause racine, Tier 2) — proposé CRITIQUE
- **MAT-3 (Majeur, nouveau)** : lectures à trois issues (valeur / absent / **illisible**) ; « illisible » bloque toute
  réécriture du fichier. Fichiers : `SettingsService`, `LectureReglages`, `ArchiveStore`, `TreatedStore`,
  `MagasinAgregats`, `IndexMessages`, `ClaudeSettingsJson`. Absorbe **DATA-5** (réglages illisibles écrasés au démarrage,
  Majeur), **FIAB-3**, **DATA-7/TEST-3** (archives), **DATA-3**, **TEST-4/DATA-13** (Historique : « verrouillé » ≠ « vide »),
  **MAT-4** (trace de l'illisible effacée), **MAT-5** (widget désactivé à tort → hooks retirés).
- **FIAB-2 (Majeur)** : `SettingsService.Save` ne lève plus sur le thread UI (réessai borné, journalisé) ; position du widget
  persistée à la fin du déplacement et non à chaque `LocationChanged`. Absorbe DATA-11.
- **FIAB-1 (Majeur)** : filet global (`DispatcherUnhandledException`, `AppDomain.UnhandledException`,
  `TaskScheduler.UnobservedTaskException`) qui journalise dans `chronos.log` ; `OnStartup` protégé.
- **FIAB-4 (Mineur)** : `chronos.log` n'efface plus la ligne « arrêt dépassé » de la session précédente.

### Lot B — `~/.claude/settings.json` (touche les permissions de Claude Code) — proposé CRITIQUE
- **DATA-4 = FIAB-8 (Mineur, rehaussé en priorité)** + **MAT-1 (Mineur)** : un fichier vide ou illisible n'est jamais
  réécrit ; l'installateur de hooks sauvegarde comme le réconciliateur (une seule voie d'écriture) ; vérification que le
  fichier n'a pas changé entre lecture et écriture. Raison de priorité : une perte effacerait `permissions` (règles `deny`).

### Lot C — historique des tokens — proposé CRITIQUE
- **DATA-1 (Majeur)** : la relecture d'un transcript ne peut plus doubler un mois gelé (index chargé pour le mois du delta,
  ou delta refusé sur mois gelé sans id connu) ; corriger le commentaire `Curseurs.cs:73`. **DATA-2 (Mineur)** : ne pas
  avancer les curseurs si l'index n'a pas été écrit.
- **SEC-2 (Mineur)** : valider `session_id` (caractères simples, chemin final confiné) — il peut sinon supprimer
  `curseurs.json` et déclencher DATA-1.

### Lot D — mise à jour vers la 3.5.0 — proposé CRITIQUE
- **PKG-1 (Majeur)** : au démarrage, le raccourci d'autostart (s'il existe) est repointé vers l'exe courant ; `IsEnabled`
  vérifie la cible. Sinon la 3.4.0 reprendrait la main au redémarrage. (Autostart actuellement désactivé sur ta machine.)

### Lot E — petits défauts de comportement et reliquats — proposé CRITIQUE (peu coûteux)
- **MAINT-5 (Mineur)** : le diagnostic liste les 8 hooks câblés, pas 5.
- **FIAB-9 (Mineur)** : fermer le cadran (Alt+F4) termine bien le processus et libère le verrou.
- **PKG-7 = PERT-1 (Mineur)** : retirer `scripts/` du pont statusLine Node (encore présenté comme « source primaire »).
- **PERT-2, PERT-3, PERT-4, PERT-5, MAINT-11 (Mineur)** : code mort (PurgerPrefixe, RunAsync, convertisseurs, `rejoue`…).

### Reportés (proposés → roadmap / issues)
- Structure : **ARCH-1**, **ARCH-3/MAINT-4** (Majeur : dépendances optionnelles télescopiques — refactor transverse),
  **MAINT-2** (Majeur : `SessionMonitor.Inspecter`), MAINT-1, ARCH-2/TEST-2, ARCH-4..8, ARCH-10, MAINT-3, MAINT-6..10.
- Outillage : ARCH-11/MAINT-12 (`.editorconfig`), ARCH-13 (analyseurs), TEST-1/5/6/7 (trous de tests ciblés).
- Packaging (décision produit) : **PKG-2** (outil personnel ou distribué ?), **PKG-5/SEC-1** (signature), PKG-3 (fin de
  support .NET 8 le 10/11/2026 → .NET 10), PKG-4, PKG-6 (dossier d'installation = racine du dépôt), MAT-2.
- Divers : FIAB-5, FIAB-7, DATA-6, DATA-8..10, DATA-12, SEC-5..9, MAT-6, PERT-6..14, ARCH-9, ARCH-14/PERT-9.

## 4. Findings détaillés
Au format §7 dans chaque fichier de rôle (`.zeus/reports/cycle2/council/*.md`), avec statut de challenge dans
`sceptique.md` et `matrice.md`. IDs stables.
