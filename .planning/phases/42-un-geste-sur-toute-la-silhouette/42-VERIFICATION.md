---
phase: 42-un-geste-sur-toute-la-silhouette
verified: 2026-10-03T00:00:00Z
status: human_needed
score: 3/3 critères de succès vérifiés dans le code et les tests (clics réels : constat humain)
re_verification: false
human_verification:
  - test: "Clics réels sur chacune des huit variantes (Arcs, Braises, Fusible H/V, Marée V/H, Volets H/V)"
    expected: "Clic = bascule % / temps (≈ 0,5 s après relâchement) ; double-clic = Historique sans bascule ; appui-glisser au-delà du seuil = déplacement puis accroche au coin ; clic droit = Réglages ; hors silhouette le clic atteint le bureau"
    why_human: "Le clic réel sur une fenêtre layered (alpha 0 = clic traversant, DragMove modal) ne se prouve pas sans lancer l'exe ; constat prévu au DESIGN-REVIEW du cycle et en phase 43"
  - test: "Pastilles d'Arcs (âge du relevé, invitation, déconnexion) et infobulles"
    expected: "Chaque pastille garde son propre clic / son infobulle sans déclencher la bascule"
    why_human: "Comportement souris réel sur fenêtre layered"
---

# Phase 42 : Un geste sur toute la silhouette — rapport de vérification

**But de la phase :** un geste unique sur toute la silhouette de chaque variante, clic traversant hors silhouette, silhouette en `ZoneSilhouette`, preuve par rendu et HitTest de routage, garde statique, commentaires corrigés, mots des gestes alignés.
**Vérifié le :** 2026-10-03
**Statut :** human_needed (aucun écart bloquant ; reste le constat humain des clics réels)
**Re-vérification :** non, vérification initiale

## Atteinte du but

### Vérités observables (critères de succès de ROADMAP)

| # | Vérité | Statut | Preuve |
|---|--------|--------|--------|
| 1 | Toute la silhouette répond de la même façon (clic, double-clic, glisser, clic droit), `CentreHit` supprimé, pastilles gardent leurs clics, un seul répartiteur | VÉRIFIÉ | `MainWindow.xaml:23-25` : grille `Racine` porte `MouseLeftButtonDown/Move/Up`, `LostMouseCapture`, `MouseRightButtonUp="OnRightClick"`. `MainWindow.xaml.cs` : `AutomateGeste _geste` ; filtre `ZoneGeste.Contient` avant armement (l.100-101) ; `ClicCentre(action.ClickCount)` / `ClicCentre(1)` (arbitre inchangé) ; `SystemParameters.MinimumHorizontalDragDistance` puis `DragMove()` puis `SnapToNearestCorner()` au retour (l.114-123). `CentreHit` absent des sources (il ne reste que `obj/` périmé et la garde qui l'interdit). |
| 2 | Hors silhouette le bureau reçoit le clic ; silhouette en `ZoneSilhouette` (#01000000) ; garde statique contre pinceau nul / `Transparent` | VÉRIFIÉ | `DesignTokens.xaml:24` token `#01000000`. Les 8 variantes (Arcs, Braises : Ellipse 148 = rayon 74 ; Fusible, Marée, Volets : Rectangle RadiusX/Y 10, un par orientation) portent `ZoneGeste.Silhouette="True"` + `Fill={DynamicResource ZoneSilhouette}`. Aucun `Transparent` dans `Views/Cadrans/*.xaml` ; seul le `Background` de la balise `<Window>` reste `Transparent` (voulu, et compté à 1 par `GardeGestesCadranTests`). |
| 3 | Prouvé pour les huit variantes : `RenderTargetBitmap` (alpha > 0 dedans, = 0 dehors) + `HitTest` de routage ; commentaires corrigés ; `GardeGestesCadranTests` adapté | VÉRIFIÉ | `ZonesGesteRenduTests.cs` (669 lignes, `RenderTargetBitmap`, `EmpreinteCadran.Pour`) ; `GardeGestesCadranTests.cs` (232 lignes) réécrite pour le répartiteur (`Racine_MouseMove`), interdit `CentreHit`, `Transparent suffit`, `CentreHit le prouve`, `clic au centre`. Porte annoncée : build -warnaserror 0 avertissement, 1922/1922. |

**Score :** 3/3 critères vérifiés dans le code et les tests.

### Artefacts requis

| Artefact | Attendu | Statut | Détails |
|----------|---------|--------|---------|
| `src/Chronos/ViewModels/AutomateGeste.cs` | automate pur de décision clic / glisser | VÉRIFIÉ | présent, `public sealed class AutomateGeste` ; instancié et câblé dans `MainWindow` |
| `tests/Chronos.Tests/AutomateGesteTests.cs` | tests purs, 80 lignes min | VÉRIFIÉ | 195 lignes |
| `src/Chronos/Views/ZoneGeste.cs` | propriété attachée + `Trouver` / `Contient` | VÉRIFIÉ | présent, utilisé par `MainWindow` |
| `src/Chronos/Resources/DesignTokens.xaml` | token `ZoneSilhouette` | VÉRIFIÉ | l.24 |
| `tests/Chronos.Tests/ZonesGesteRenduTests.cs` | preuve par rendu | VÉRIFIÉ | 669 lignes |
| `MainWindow.xaml(.cs)` | répartiteur unique | VÉRIFIÉ | voir vérité 1 |
| `ReglagesWindow.xaml` | « Aussi : double-clic sur le cadran » | VÉRIFIÉ | l.671 |
| `README.md` | gestes à jour | VÉRIFIÉ | l.20 « Gestes, sur tout le cadran », l.117 |

### Liens clés

| De | Vers | Via | Statut |
|----|------|-----|--------|
| Vues de cadran | `ZoneSilhouette` | `Fill={DynamicResource ZoneSilhouette}` + marqueur | CÂBLÉ (8/8) |
| `Racine_MouseLeftButtonDown` | `ZoneGeste.Contient` | filtre géométrique | CÂBLÉ |
| `Racine_MouseMove` | `AutomateGeste.Deplacement` + `DragMove` + `SnapToNearestCorner` | seuils `SystemParameters` | CÂBLÉ |
| Appui / relâchement | `MainViewModel.ClicCentre` | `action.EstClic` | CÂBLÉ |
| Tests de rendu | empreinte de l'orientation | `EmpreinteCadran.Pour` | CÂBLÉ |

### Couverture des exigences

| Exigence | Plans | Statut | Preuve |
|----------|-------|--------|--------|
| GST-01 | 42-01, 42-03, 42-04 | SATISFAITE | automate pur + répartiteur unique + mots des gestes alignés |
| GST-02 | 42-02, 42-03 | SATISFAITE | silhouette `#01000000`, garde statique, clic traversant (alpha 0 hors silhouette) |
| GST-03 | 42-02, 42-03 | SATISFAITE | rendu `RenderTargetBitmap` + HitTest de routage, 8 variantes |

Les trois ID du ROADMAP et de REQUIREMENTS.md (cochés, « Phase 42 / Complete ») sont déclarés dans les plans ; aucune exigence orpheline.

### Anti-patterns relevés

| Fichier | Ligne | Motif | Gravité | Impact |
|---------|-------|-------|---------|--------|
| `tests/Chronos.Tests/CadranBindingTests.cs` | 321, 392, 702 | commentaires « hit-testable (Transparent suffit) » : le thème exact que GST-03 voulait corriger | Avertissement | commentaires seuls, assertions saines (`NotNull`) ; non rattrapé par la garde, qui ne lit que le XAML et `MainWindow.xaml.cs` |
| `docs/data-sources.md` | 454 | « Simple clic au centre du cadran » périmé | Avertissement | doc, aucun test ne rougit |
| `docs/publish.md` | 156 | « double-clic au centre du cadran » (section 3.4 antérieure, alors que l.186 dit le geste unique) | Info | vocabulaire incohérent dans le même fichier |

Aucun bloquant : pas de stub, pas de pinceau nul, pas de `CentreHit`.

### Vérifications comportementales

Non exécutées : l'exe ne se lance pas (consigne) ; la porte annoncée (build -warnaserror, 1922/1922) n'a pas été rejouée par ce vérificateur, elle est reprise de l'orchestrateur.

### Vérification humaine requise

1. **Clics réels sur les huit variantes** : clic, double-clic, appui-glisser avec accroche, clic droit sur toute la silhouette ; clic traversant à côté. Raison : fenêtre layered réelle ; constat prévu au DESIGN-REVIEW et en phase 43.
2. **Pastilles d'Arcs** : âge du relevé, invitation, déconnexion, infobulles gardent leur clic sans bascule.

### Synthèse

Le code, le XAML et les tests couvrent les trois critères et les trois exigences. Les seuls points restants sont des vestiges de vocabulaire (deux phrases de docs, trois commentaires de tests CadranBindingTests) sans effet fonctionnel, à nettoyer en phase 43 avec la documentation de release, et le constat humain des clics réels, qui n'est pas compté comme écart.

---

_Vérifié le : 2026-10-03_
_Vérificateur : Claude (gsd-verifier)_
