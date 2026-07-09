# Phase 8 : Inférence des fenêtres + estimation depuis JSONL — Research

**Researched:** 2026-07-09
**Domain:** Algorithmique temporelle pure (inférence de fenêtre glissante), enrichissement d'un provider .NET existant, nettoyage de contrat
**Confidence:** HIGH (tout le code concerné a été lu intégralement ; aucune dépendance externe nouvelle ; stack figée)

## Summary

Cette phase est **100 % interne au dépôt** : aucune librairie à ajouter, aucun outil externe, aucune
API réseau. Le travail est (1) une **fonction pure d'inférence** de la fenêtre 5 h à partir de
timestamps triés (miroir exact du pattern `WeeklyRecalibration` déjà en place), (2) l'**enrichissement
de `JsonlEstimationProvider`** pour collecter `(timestamp, tokens)` en une seule passe disque, inférer la
fenêtre courante, sommer les tokens de CETTE fenêtre, et calculer une utilization estimée si un plafond
est défini, et (3) le **retrait de `SnapshotChanged` + `UsageSnapshot.Age`** (dette morte, jamais abonnée
/ jamais consommée).

Le pipeline aval est déjà prêt : `WindowState` porte déjà `Utilization`, `ResetsAt`,
`FractionTimeRemaining`, `EstimatedTokens` ; `WindowGaugeViewModel` les bind déjà ; le badge « estimée »
découle de `Reliability=Estimated` (inchangé). **Aucun changement UI n'est requis** — remplir les champs
`WindowState` suffit à faire réapparaître longueur et couleur des arcs. `WeeklyRecalibration.Apply` (appelé
dans `MainViewModel.ApplySnapshot`) fournit déjà le reset hebdo (EST-05) : rien à recâbler côté reset hebdo.

**Primary recommendation:** Extraire une classe statique pure `FiveHourWindowInference` dans
`Chronos.Services` (testable avec `FakeClock`), injecter `SettingsService` dans `JsonlEstimationProvider`
et appeler `Load()` à chaque `GetAsync` (coût négligeable, calibration Phase 9 sans redémarrage),
refactorer la passe de lecture pour matérialiser `List<(DateTimeOffset ts, long tokens)>` puis calculer
en mémoire. Adopter la définition **verrouillée (A)** de la fenêtre (remontée depuis le message le plus
récent tant qu'aucun trou inter-messages ≥ 5 h) ; `reset = début + 5 h` ; `reset ≤ now` ⇒ fenêtre inactive
⇒ `FractionTimeRemaining = 1`.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Motivation (contexte utilisateur — verrouillé)**
L'utilisateur travaille EXCLUSIVEMENT dans l'app bureau Claude (Code desktop + Cowork) : la statusline
ne se rend jamais → usage.json restera à null chez lui. Les JSONL (~/.claude/projects) couvrent en
revanche tout son usage réel (vérifié empiriquement). v1.1 rend le repli utile SANS trahir l'honnêteté.

**Inférence de la fenêtre 5 h (EST-01, EST-02 — verrouillé)**
- La fenêtre 5 h glissante démarre au premier message suivant un trou d'inactivité ≥ 5 h.
- Algorithme : parcourir les timestamps des entrées JSONL (déjà lus par JsonlEstimationProvider),
  trouver le début de la fenêtre courante = le plus ancien message M tel qu'il n'existe AUCUN trou ≥ 5 h
  entre M et maintenant, en remontant depuis le message le plus récent. resets_at estimé = début + 5 h.
- Si resets_at estimé < maintenant (fenêtre expirée) ou aucune activité < 5 h : fenêtre inactive →
  fraction de temps = 1 (arc plein, rien d'entamé), tokens de fenêtre = 0, utilization = 0 si plafond
  défini sinon null. JAMAIS un arc vide par défaut.
- La somme de tokens 5 h ne compte QUE les messages de la fenêtre courante (pas 5 h glissantes brutes).

**Utilization estimée par plafonds (EST-03, EST-04 — verrouillé)**
- settings.json : FiveHourTokenBudget (long?), WeeklyTokenBudget (long?) — null par défaut.
- utilization estimée = tokens fenêtre / plafond ; clampée ≥ 0, PAS clampée à 1 (≥ 1 = gris épuisé, déjà géré).
- Sans plafond : utilization = null (couleur neutre, comportement v1.0). Aucune UI de calibration dans
  cette phase (Phase 9) — mais la lecture des settings et la math sont en place et testées.
- Fenêtre hebdo : bornée par WeeklyAnchor si défini (fenêtre [ancre ; ancre+7j] roulante), sinon 7 jours
  glissants pour la somme de tokens ; resets_at hebdo estimé = mécanique WeeklyRecalibration existante (EST-05).

**Nettoyage contrat (NET-01 — verrouillé)**
- Retirer l'événement SnapshotChanged de IUsageProvider et des 3 providers (jamais abonné — l'orchestrateur
  expose le sien). Retirer UsageSnapshot.Age (inutilisé, IsStale dérivé de SourceCapturedAt).
- Mettre à jour les tests touchés ; la suite complète doit rester verte (107 attendus, moins ceux retirés/adaptés).

**Honnêteté (transverse — verrouillé)**
- Tout ce qui sort de l'inférence reste Reliability=Estimated → badge « estimée » par fenêtre (v1.0).
- Ne jamais présenter un reset inféré comme exact ; ne jamais colorer sans plafond.

### Claude's Discretion
Structure interne de l'algorithme d'inférence (fonction pure recommandée, testable avec FakeClock),
seuil exact du trou (≥ 5 h strict), gestion des timestamps non monotones entre fichiers.

### Deferred Ideas (OUT OF SCOPE)
- UI de calibration et calibration auto (Phase 9 : CAL-01..03, NET-02).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| EST-01 | Reset 5 h inféré : début = 1er message après trou ≥ 5 h ; reset = début + 5 h ; l'arc extérieur retrouve une longueur | `FiveHourWindowInference.InferWindowStart` (§ Pattern 1) — algorithme (A) verrouillé, pseudocode fourni |
| EST-02 | Fenêtre inactive (trou ≥ 5 h) → arc plein (fraction = 1), pas d'utilization inventée | Branche `reset ≤ now` → `FractionTimeRemaining = 1`, tokens = 0 (§ Pattern 1, Code Example 2) |
| EST-03 | Utilization 5 h = tokens fenêtre courante / FiveHourTokenBudget ; null sans plafond | `EstimateUtilization` (§ Pattern 3) ; somme bornée à la fenêtre inférée (§ Q3) |
| EST-04 | Utilization hebdo = tokens hebdo / WeeklyTokenBudget ; fenêtre ancrée WeeklyAnchor sinon 7 j glissants | Math de la fenêtre roulante ancrée (§ Q6 / Code Example 3) |
| EST-05 | Reset hebdo via WeeklyAnchor (WeeklyRecalibration existant) ; sinon « — » | **Déjà satisfait** par `WeeklyRecalibration.Apply` dans `MainViewModel` — vérifier non-régression (§ Q « EST-05 ») |
| NET-01 | Retirer SnapshotChanged (contrat + 3 providers) + UsageSnapshot.Age | Inventaire exact fichiers/tests (§ Runtime State Inventory + § Q5) |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Stack imposée** : C# / .NET 8 (`net8.0-windows`) / WPF / MVVM (CommunityToolkit.Mvvm 8.4.2) / MS.Extensions.DI + Hosting. **Ne rien ajouter** — `System.Text.Json` intégré suffit.
- **Couche Services/Models NEUTRE** : aucun type WPF (`PresentationCore`/`PresentationFramework`/`WindowsBase`) dans les signatures publiques → gardé par `ServicesLayerPurityTests`. La nouvelle classe d'inférence DOIT être pure (math + `DateTimeOffset` uniquement).
- **Honnêteté des chiffres** : `utilization`/`resets_at` exacts priment ; ne JAMAIS présenter une estimation comme exacte ; ne jamais colorer sans plafond. Tout ce qui sort de l'inférence reste `Estimated`.
- **Chemins sous profil utilisateur uniquement**, lecture seule stricte sous `~/.claude` ; ne jamais lire `.credentials.json` ni le contenu des conversations — ne compter que tokens/métadonnées.
- **UI et commentaires en français.**
- **Parsing tolérant** (ROB-02) : ligne corrompue/partielle/non-assistant ignorée, jamais d'exception qui remonte — comportement existant à PRÉSERVER.
- **GSD workflow** : passer par une commande GSD avant toute édition (`/gsd:execute-phase`).

## Standard Stack

Aucune nouvelle dépendance. Tout est déjà présent et figé.

| Élément | Version | Rôle dans la phase | Statut |
|---------|---------|--------------------|--------|
| `System.Text.Json` | intégré net8.0 | Parsing JSONL (déjà utilisé), lecture settings.json | En place |
| xUnit | 2.9.2 | Tests `[Fact]` purs | En place |
| Xunit.StaFact | 1.1.11 | Tests nécessitant un thread STA (UI) — **non requis** pour l'inférence pure | En place |
| `FakeClock` (interne test) | — | Injecter un `now` déterministe dans l'inférence | En place (`tests/Chronos.Tests/Fakes/FakeClock.cs`) |

**Rien à installer.** `Verify-Version` : sans objet (pas de package modifié).

## Architecture Patterns

### Où vit chaque responsabilité (décision recommandée)

| Responsabilité | Emplacement | Raison |
|----------------|-------------|--------|
| Inférence du début de fenêtre 5 h (EST-01/02) | **Nouvelle classe statique pure** `Chronos.Services.FiveHourWindowInference` | Miroir de `WeeklyRecalibration` : pure, `now` en paramètre, testable en `[Fact]` sans I/O ni horloge réelle |
| Collecte `(ts, tokens)` + sommes de fenêtre + utilization | **`JsonlEstimationProvider` enrichi** | Seul endroit qui lit les timestamps/tokens ; une seule passe disque |
| Lecture des plafonds/ancre | **`SettingsService` injecté dans le provider**, `Load()` par `GetAsync` | Fraîcheur à chaque refresh → calibration Phase 9 sans redémarrage |
| Reset hebdo (EST-05) | **`WeeklyRecalibration.Apply` dans `MainViewModel` — INCHANGÉ** | Déjà en place et testé ; conserve `Estimated` |
| Affichage (longueur/couleur/badge) | **`WindowGaugeViewModel` — INCHANGÉ** | Bind déjà `Utilization`/`FractionTimeRemaining`/`ResetsAt`/`EstimatedTokens` |

### Pattern 1 : Fonction pure d'inférence (algorithme verrouillé « A »)

**What:** À partir des timestamps triés croissants et de `now`, renvoyer le début de la fenêtre 5 h
courante, ou `null` si la fenêtre est inactive/expirée.

**When to use:** Appelée une fois par `GetAsync` après tri des timestamps collectés.

**Définition verrouillée (CONTEXT `<decisions>`) :** début = le plus ancien message M tel qu'AUCUN trou
inter-messages ≥ 5 h n'existe entre M et le message le plus récent, en **remontant depuis le plus récent**.

```csharp
// Source : dérivé de la décision verrouillée CONTEXT.md ; pattern miroir de WeeklyRecalibration (pur).
// tsAsc : timestamps des messages assistant, TRIÉS CROISSANT, déjà filtrés (t <= now — cf. Pitfall 3).
public static class FiveHourWindowInference
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(5);

    /// <summary>Début de la fenêtre 5 h courante, ou null si inactive/expirée (reset <= now).</summary>
    public static DateTimeOffset? InferWindowStart(IReadOnlyList<DateTimeOffset> tsAsc, DateTimeOffset now)
    {
        if (tsAsc.Count == 0) return null;                    // aucune activité → inactive

        var start = tsAsc[^1];                                 // message le plus récent
        for (int i = tsAsc.Count - 2; i >= 0; i--)
        {
            if (start - tsAsc[i] >= Window) break;             // trou ≥ 5 h STRICT → borne atteinte
            start = tsAsc[i];                                  // pas de trou → on recule le début
        }

        var reset = start + Window;
        return reset > now ? start : null;                     // reset <= now → fenêtre expirée (EST-02)
    }
}
```

**Conséquences pour le provider :**
- `start != null` (fenêtre ACTIVE) : `ResetsAt = start + 5h` ; `FractionTimeRemaining =
  WindowState.FractionRemaining(ResetsAt, now, 5h)` ; `EstimatedTokens` = somme des tokens dont
  `start ≤ ts ≤ now`.
- `start == null` (fenêtre INACTIVE, EST-02) : `ResetsAt = null` ; `FractionTimeRemaining = 1.0` ;
  `EstimatedTokens = 0` ; utilization = `budget is null ? null : 0.0`.

### Pattern 2 : Passe de lecture unique → matérialisation en mémoire (Q2)

**What:** Aujourd'hui la boucle de lecture accumule directement `five`/`week` (deux `long`). Pour inférer,
il faut le détail par message. Refactorer pour collecter `List<(DateTimeOffset ts, long tokens)>` en une
seule passe, puis tout calculer en mémoire (tri + inférence + sommes). **Aucune seconde lecture disque.**

**When to use:** Systématiquement — c'est le cœur de l'enrichissement.

```csharp
// Dans GetAsync, remplacer l'accumulation inline par une collecte :
var entries = new List<(DateTimeOffset Ts, long Tokens)>();
// ... dans la boucle, à la place de `five += ...; week += ...;` :
entries.Add((when, tokens));
// Après la boucle :
entries.Sort((a, b) => a.Ts.CompareTo(b.Ts));            // tri global (fichiers non triés entre eux)
```

**Coût mémoire :** un tuple = 16 octets ; même quelques milliers de messages sur 8 jours = quelques dizaines
de Ko. Négligeable. Le tri `O(n log n)` sur `n` messages récents est instantané.

### Pattern 3 : Utilization estimée (EST-03/04)

```csharp
// Source : décision verrouillée — clampée ≥ 0, PAS clampée à 1 (≥ 1 = gris épuisé, géré par WindowState.Exhausted).
private static double? EstimateUtilization(long windowTokens, long? budget)
    => budget is > 0 ? Math.Max(0.0, (double)windowTokens / budget.Value) : null;
```
- `budget == null` → `null` (couleur neutre, comportement v1.0 — CLAUDE.md « ne jamais colorer sans plafond »).
- `budget <= 0` → `null` (garde anti-division ; un plafond nul/négatif est invalide).
- Pas de clamp haut : `WindowState.Exhausted => Utilization is >= 1.0` continue de déclencher le gris épuisé.

### Anti-Patterns to Avoid

- **Sommer 5 h glissantes brutes `[now-5h, now]`** : c'est l'ancien comportement ; la décision verrouillée
  impose la somme de la **fenêtre courante inférée** `[start, now]`. Retirer le `if (when >= now - 5h)`.
- **Faire l'inférence dans le ViewModel** : le VM n'a pas les timestamps (seul le provider lit les JSONL).
  Garder l'inférence côté provider ; ne PAS dupliquer la lecture disque.
- **Injecter le singleton `ChronosSettings`** (App.xaml.cs ligne 58, lu une seule fois au démarrage) : il
  gèlerait les plafonds → calibration Phase 9 sans effet jusqu'au redémarrage. Injecter `SettingsService`
  et `Load()` frais (§ Q4).
- **Mettre un type WPF dans la classe d'inférence** : casse `ServicesLayerPurityTests`.

## Don't Hand-Roll

| Problème | Ne pas construire | Réutiliser | Pourquoi |
|----------|-------------------|-----------|----------|
| Fraction de temps restante clampée | Un calcul ad hoc `(reset-now)/len` | `WindowState.FractionRemaining(resetsAt, now, len)` | Existe déjà, clampe [0..1], gère `null`/fenêtre non positive |
| Reset hebdo aligné sur l'ancre | Une nouvelle math d'ancre | `WeeklyRecalibration` (VM) + `NextReset` pour la borne de fenêtre | Déjà testé (5 tests) ; garantit `Estimated` conservé |
| Parsing ISO 8601 / tolérance de ligne | Un parseur maison | `DateTimeOffset.TryParse(..., RoundtripKind)` + `try/catch(JsonException)` existants | Déjà en place et couverts par les tests ROB-02 |
| Lecture tolérante de settings.json | Relire/désérialiser à la main | `SettingsService.Load()` | Tolérant (fichier absent/corrompu → défauts), atomique en écriture |
| Détection « épuisé » (gris) | Un seuil ≥ 1 dans le provider | `WindowState.Exhausted` (dérivé de `Utilization >= 1`) | Ne pas clamper l'utilization à 1 suffit ; le gris est déjà géré en aval |

**Key insight:** presque toute la « plomberie » (fraction, reset hebdo, tolérance, staleness) existe déjà.
La phase n'ajoute qu'**une fonction pure** et **deux sommes bornées** ; tout le reste est du câblage.

## Réponses aux points à résoudre (du prompt)

### Q1 — Algorithme d'inférence pur (signature, complexité, cas limites)

- **Signature recommandée :** `static DateTimeOffset? InferWindowStart(IReadOnlyList<DateTimeOffset> tsAsc, DateTimeOffset now)` (cf. Pattern 1). Renvoie le début de fenêtre ; `null` = inactive.
- **Complexité :** tri `O(n log n)` (une fois, dans le provider) + inférence `O(n)`. `n` = messages des fichiers récents (mtime < 8 j), typiquement quelques centaines à quelques milliers → instantané.
- **Timestamps dispersés sur N fichiers non triés entre eux :** collecter tout en mémoire puis **trier une fois** (Pattern 2). Ne pas tenter un tri partiel/borné : la simplicité prime, le volume est faible une fois borné par mtime.
- **Cas limites (à tester explicitement) :**
  - Aucun message → `null` (inactive, fraction = 1).
  - Message unique < 5 h → actif `[msg, msg+5h]` ; message unique ≥ 5 h → `null` (reset ≤ now).
  - Trou exactement 5 h → **≥ 5 h STRICT = borne** (discretion verrouillée « ≥ 5 h strict » → `>=`).
  - Activité continue > 5 h (start très ancien) → `reset = start+5h ≤ now` → **inactive/fraction = 1** (comportement verrouillé, assumé imparfait — cf. Open Question 1).
  - Timestamps futurs / horloge décalée → **filtrer `ts > now` avant inférence** (Pitfall 3) ; discretion « timestamps non monotones ».

### Q2 — Réutiliser la passe de lecture (éviter une 2ᵉ lecture disque)

Aujourd'hui (`JsonlEstimationProvider.GetAsync`, lignes 40-69) la boucle lit chaque ligne et fait
`five += tokens` / `week += tokens` directement. **Remplacer** par `entries.Add((when, tokens))` (Pattern 2).
Une seule ouverture/streaming par fichier, comme aujourd'hui ; le calcul des fenêtres se fait après, en
mémoire. Le `FileShare.ReadWrite` + tolérance ROB-02 restent identiques.

### Q3 — Frontière fenêtre 5 h vs somme

- **Actuellement :** le provider somme sur `[now-5h, now]` (glissant brut) pour `five`, et `[now-7d, now]` pour `week`.
- **Adapter :** `five` = somme des `tokens` où `start ≤ ts ≤ now` (fenêtre **inférée**), `start` venant de `InferWindowStart`. Si inactive → `five = 0`. Le `week` reste sur fenêtre 7 j **mais ancrée** si `WeeklyAnchor` (§ Q6), sinon glissant `[now-7d, now]` comme aujourd'hui.

### Q4 — Injection des plafonds (coût vs snapshot poussé)

**Recommandation : injecter `SettingsService` dans `JsonlEstimationProvider` et appeler `Load()` en tête de
`GetAsync`.** C'est la solution **la plus simple** qui satisfait le besoin verrouillé « le provider reçoit
les settings à CHAQUE GetAsync pour que la calibration Phase 9 s'applique sans redémarrage ».

- **Coût :** `GetAsync` est cadencé ~60 s (PeriodicTimer) + événements watcher débouncés (300 ms). `Load()` =
  un `File.ReadAllText` d'un petit JSON + désérialisation tolérante. Ordre de grandeur < 1 ms, largement
  dominé par le scan JSONL lui-même. **Acceptable, aucun cache nécessaire.**
- **Pourquoi pas le singleton `ChronosSettings`** (App.xaml.cs:58) : lu une seule fois au démarrage → gèle
  les plafonds. À proscrire ici (contredit la décision Phase 9-friendly).
- **Impact DI :** `JsonlEstimationProvider` est déjà `AddSingleton` (App.xaml.cs:72) et `SettingsService`
  aussi (ligne 57) → ajouter le paramètre au constructeur suffit, la DI résout automatiquement.
- **Impact tests :** `ProviderFor` (JsonlEstimationProviderTests.cs:38) construit `new JsonlEstimationProvider(paths, clock)`
  → ajouter un `new SettingsService(paths)`. Comme le `ProjectsRoot` temp isolé n'a pas de settings.json,
  `Load()` renvoie les défauts (plafonds `null`) → utilization `null` (cohérent avec les tests existants).

### Q5 — NET-01 : impact exact (grep effectué)

Voir § Runtime State Inventory pour la liste ligne à ligne. **Aucun abonné réel** : `MainViewModel`
s'abonne à `RefreshOrchestrator.SnapshotChanged` (un event SÉPARÉ, porté par l'orchestrateur qui
**n'implémente pas** `IUsageProvider`). Le `SnapshotChanged` de `IUsageProvider` est déclaré + invoqué mais
jamais souscrit → suppression sûre. `Age` est calculé par `ClaudeUsageObjectProvider`/`CompositeUsageProvider`
mais jamais lu : `MainViewModel.Interpolate` dérive `IsStale` de `CapturedAt` (= `SourceCapturedAt`), pas de `Age`.

### Q6 — EST-04 : math de la fenêtre hebdo ancrée

Fenêtre roulante `[ancre + k·7j ; ancre + (k+1)·7j)` **contenant now** :

```
k          = floor((now - ancre) / 7j)          // nombre de cycles entiers écoulés depuis l'ancre
windowStart = ancre + k·7j
windowEnd   = windowStart + 7j                    // = prochain reset ; cohérent avec WeeklyRecalibration.NextReset
somme hebdo = Σ tokens où windowStart ≤ ts ≤ now
```

- Sans `WeeklyAnchor` → fallback **7 j glissants** `[now-7d, now]` (comportement actuel conservé).
- **Cohérence avec EST-05 :** `windowEnd` = `WeeklyRecalibration.NextReset(ancre, now)` dans le cas général
  (ceil = floor+1 hors frontière exacte). La somme de tokens et le reset affiché reposent donc sur la même
  ancre → pas d'incohérence visuelle entre longueur (reset) et couleur (utilization) de l'arc hebdo.
- **Recommandation d'implémentation :** calculer `windowStart` directement via `floor` (robuste à la
  frontière exacte) plutôt que `NextReset - 7j`. Encapsuler dans une petite fonction pure réutilisable
  (ex. `WeeklyWindow.CurrentStart(anchor, now)`), testable comme `WeeklyRecalibration`.

### EST-05 — déjà satisfait (vérifier non-régression)

`MainViewModel.ApplySnapshot` appelle déjà `WeeklyRecalibration.Apply(snap.SevenDay, _settings.WeeklyAnchor,
now)` : sans ancre, `ResetsAt` reste `null` → countdown « — » (comportement v1.0) ; avec ancre, reset futur
synthétisé en `Estimated`. **Ne rien changer côté reset hebdo** ; se contenter de vérifier que l'ajout de
l'utilization hebdo (calculée dans le provider) n'entre pas en conflit avec le recalibrage du reset (le
provider laisse `SevenDay.ResetsAt = null`, le VM le remplit — ordre inchangé).

## Runtime State Inventory (NET-01 — nettoyage/refactor)

Phase à composante refactor : le grep runtime a été fait. Aucune persistance ni état externe n'est en jeu
(pas de renommage de clé stockée, pas de service live). Le « state » ici = points de code référençant les
symboles supprimés.

| Catégorie | Éléments trouvés | Action requise |
|-----------|------------------|----------------|
| Stored data | **Aucun** — vérifié : `Age`/`SnapshotChanged` ne sont ni sérialisés ni persistés (settings.json ne les contient pas ; `UsageSnapshot` n'est jamais écrit sur disque) | Aucune migration |
| Live service config | **Aucun** — vérifié : pas de service externe, pas d'abonnement réel à `IUsageProvider.SnapshotChanged` | Aucune |
| OS-registered state | **Aucun** — vérifié : rien dans l'autostart/registry ne référence ces symboles | Aucune |
| Secrets/env vars | **Aucun** — vérifié : sans objet | Aucune |
| Build artifacts | `tests/.../obj/**` régénérés au build ; aucune référence source résiduelle après édition | `dotnet build` régénère |

**`SnapshotChanged` — fichiers/lignes à éditer (retrait) :**
- `src/Chronos/Services/IUsageProvider.cs:12` — supprimer la déclaration de l'event du contrat.
- `src/Chronos/Services/ClaudeUsageObjectProvider.cs:29` (déclaration) + `:64` (`SnapshotChanged?.Invoke`).
- `src/Chronos/Services/JsonlEstimationProvider.cs:33` (déclaration) + `:78` (`Invoke`).
- `src/Chronos/Services/CompositeUsageProvider.cs:17` (déclaration) + `:39` (`Invoke`).
- `tests/Chronos.Tests/CompositeUsageProviderTests.cs` — **supprimer** le test `SnapshotChanged_emis_une_fois_avec_le_snapshot_final` (lignes ~112-133) ; retirer l'event du `FakeProvider` interne (lignes 20-22) + le `#pragma CS0067`.
- `tests/Chronos.Tests/Fakes/FakeUsageProvider.cs:19` + `:25` — retirer l'event et son `Invoke` (non souscrit par l'orchestrateur).
- ⚠️ **NE PAS toucher** `RefreshOrchestrator.SnapshotChanged` (:32) ni `MainViewModel` (:59,:63) : event DISTINCT, vivant, cœur du pipeline temps réel.

**`Age` — fichiers/lignes à éditer (retrait) :**
- `src/Chronos/Models/UsageSnapshot.cs:12-13` — supprimer la propriété `Age` + xmldoc.
- `src/Chronos/Services/ClaudeUsageObjectProvider.cs:62` — supprimer `Age = ...` (garder `SourceCapturedAt`).
- `src/Chronos/Services/CompositeUsageProvider.cs:37` — supprimer `Age = p.Age ?? f.Age`.
- `src/Chronos/Services/JsonlEstimationProvider.cs:76` — supprimer `Age = TimeSpan.Zero`.
- `tests/Chronos.Tests/ClaudeUsageObjectProviderTests.cs` — **adapter** le test `Valide_mappe_utilization_reset_et_age` : retirer l'assertion `Assert.Equal(TimeSpan.FromSeconds(30), snap.Age)` (ligne 56) ; conserver l'assertion `SourceCapturedAt` (ligne 57) qui reste la source de staleness. Ajuster le nom/commentaire du test si besoin (« _et_age » → « _et_capturedAt »).

## Common Pitfalls

### Pitfall 1 : Casser les assertions `null` des tests JSONL existants
**What goes wrong:** `JsonlEstimationProviderTests.Valide_somme_par_fenetre_et_marque_Estimated` assert
`Null(FiveHour.ResetsAt)` et `Null(FiveHour.FractionTimeRemaining)`. Après enrichissement, la fenêtre 5 h
ACTIVE porte désormais un `ResetsAt` (16:30) et un `FractionTimeRemaining` (~0.9).
**Why:** l'inférence remplit maintenant ces champs (c'est le but d'EST-01).
**How to avoid:** mettre à jour ces assertions dans le même plan que l'enrichissement — séparer les
attentes 5 h (désormais peuplées) des attentes 7 j (toujours `null` côté provider, rempli par le VM). La
somme 5 h reste 1550 (le message 11:30 est dans la fenêtre inférée) → le total ne change pas, seuls
`ResetsAt`/`Fraction` changent.
**Warning signs:** échecs `Assert.Null` sur `FiveHour.ResetsAt`/`FractionTimeRemaining`.

### Pitfall 2 : Compter au-delà de la fenêtre courante
**What goes wrong:** garder le vieux `if (when >= now - 5h) five += tokens` en plus de l'inférence →
double comptage ou fenêtre trop large.
**How to avoid:** remplacer entièrement la logique 5 h par « somme des entries dans `[start, now]` ».

### Pitfall 3 : Timestamps futurs / horloge décalée
**What goes wrong:** un fichier avec un timestamp `> now` (horloge machine, fuseau) devient « le plus
récent » → `reset` futur incohérent, ou tokens futurs gonflant la somme.
**How to avoid:** filtrer `entries` sur `ts <= now` avant tri/inférence (discretion verrouillée « timestamps
non monotones »). Simple et défensif.
**Warning signs:** `FractionTimeRemaining` collé à 1 en permanence, sommes surestimées.

### Pitfall 4 : Injecter le mauvais objet de settings
**What goes wrong:** injecter le singleton `ChronosSettings` (figé au démarrage) au lieu de `SettingsService`
→ les plafonds calibrés en Phase 9 n'ont aucun effet sans redémarrage.
**How to avoid:** injecter `SettingsService`, `Load()` par `GetAsync` (§ Q4).

### Pitfall 5 : Fenêtre inactive rendue comme arc vide
**What goes wrong:** laisser `FractionTimeRemaining = null` pour une fenêtre expirée → arc vide (mensonge :
suggère « épuisé »).
**How to avoid:** EST-02 impose `FractionTimeRemaining = 1` (arc plein, rien d'entamé), tokens = 0,
utilization = `budget is null ? null : 0`.

### Pitfall 6 : Régression de pureté WPF
**What goes wrong:** ranger la classe d'inférence au mauvais endroit ou lui faire toucher un type WPF.
**How to avoid:** classe statique pure dans `Chronos.Services`, uniquement `DateTimeOffset`/`TimeSpan`/
collections. `ServicesLayerPurityTests` la couvre automatiquement.

## Code Examples

### Exemple 1 — Cœur de `GetAsync` enrichi (structure recommandée)

```csharp
// Source : refactor de JsonlEstimationProvider.GetAsync (lu intégralement).
public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
{
    var now = _clock.UtcNow;
    var settings = _settings.Load();                          // Q4 : frais à chaque refresh
    var entries = new List<(DateTimeOffset Ts, long Tokens)>();

    foreach (var file in EnumerateJsonl(_paths.ProjectsRoot)) // + filtre mtime < 8 j recommandé (perf)
    {
        // ... lecture streaming tolérante identique ...
        // à la place de five/week += : entries.Add((when, tokens));  (ts <= now — Pitfall 3)
    }

    entries.Sort((a, b) => a.Ts.CompareTo(b.Ts));

    var tsAsc = entries.Select(e => e.Ts).ToList();
    var start = FiveHourWindowInference.InferWindowStart(tsAsc, now);

    var five = BuildFiveHour(entries, start, now, settings.FiveHourTokenBudget);
    var seven = BuildSevenDay(entries, settings.WeeklyAnchor, now, settings.WeeklyTokenBudget);

    return new UsageSnapshot
    {
        FiveHour = five,
        SevenDay = seven,
        SourceCapturedAt = now,        // Age SUPPRIMÉ (NET-01)
    };
}
```

### Exemple 2 — Construction de la fenêtre 5 h (active vs inactive, EST-01/02/03)

```csharp
private static WindowState BuildFiveHour(
    List<(DateTimeOffset Ts, long Tokens)> entries, DateTimeOffset? start, DateTimeOffset now, long? budget)
{
    if (start is null)                                        // EST-02 : inactive → arc plein
        return new WindowState
        {
            Kind = WindowKind.FiveHour,
            EstimatedTokens = 0,
            Utilization = budget is > 0 ? 0.0 : null,
            ResetsAt = null,
            FractionTimeRemaining = 1.0,                      // arc plein, rien d'entamé
            Reliability = SourceReliability.Estimated,
        };

    var reset = start.Value + FiveHourWindowInference.Window;
    long tokens = entries.Where(e => e.Ts >= start.Value && e.Ts <= now).Sum(e => e.Tokens);

    return new WindowState
    {
        Kind = WindowKind.FiveHour,
        EstimatedTokens = tokens,
        Utilization = budget is > 0 ? Math.Max(0.0, (double)tokens / budget.Value) : null,  // EST-03
        ResetsAt = reset,                                                                     // EST-01
        FractionTimeRemaining = WindowState.FractionRemaining(reset, now, FiveHourWindowInference.Window),
        Reliability = SourceReliability.Estimated,            // honnêteté : toujours estimée
    };
}
```

### Exemple 3 — Fenêtre hebdo ancrée (EST-04)

```csharp
// Source : math dérivée de la décision verrouillée (§ Q6). Fonction pure recommandée.
private static WindowState BuildSevenDay(
    List<(DateTimeOffset Ts, long Tokens)> entries, DateTimeOffset? anchor, DateTimeOffset now, long? budget)
{
    var week = TimeSpan.FromDays(7);
    DateTimeOffset start = anchor is { } a
        ? a + TimeSpan.FromTicks(week.Ticks * (long)Math.Floor((now - a) / week))  // [ancre + k·7j ; …]
        : now - week;                                                              // fallback glissant

    long tokens = entries.Where(e => e.Ts >= start && e.Ts <= now).Sum(e => e.Tokens);

    return new WindowState
    {
        Kind = WindowKind.SevenDay,
        EstimatedTokens = tokens,
        Utilization = budget is > 0 ? Math.Max(0.0, (double)tokens / budget.Value) : null,  // EST-04
        ResetsAt = null,                          // EST-05 : rempli par WeeklyRecalibration côté VM
        FractionTimeRemaining = null,             // idem : dérivé du reset hebdo côté VM
        Reliability = SourceReliability.Estimated,
    };
}
```

## State of the Art

| Ancienne approche (v1.0) | Nouvelle approche (v1.1 / Phase 8) | Impact |
|--------------------------|------------------------------------|--------|
| 5 h = somme glissante brute `[now-5h, now]`, `ResetsAt`/`Fraction`/`Utilization` = `null` | 5 h = fenêtre inférée `[start, now]`, `ResetsAt` = `start+5h`, `Fraction` calculé, `Utilization` si plafond | L'arc extérieur retrouve longueur ET couleur (si plafond), badgé « estimée » |
| 7 j = somme glissante `[now-7d, now]` | 7 j = fenêtre ancrée `WeeklyAnchor` si défini, sinon glissant ; `Utilization` si plafond | Arc hebdo colorable, cohérent avec le reset ancré |
| `SnapshotChanged` sur `IUsageProvider` (mort) + `Age` (inerte) | Retirés | Contrat épuré, `IsStale` reste dérivé de `SourceCapturedAt` |

**Déprécié/retiré :** `IUsageProvider.SnapshotChanged` (jamais souscrit), `UsageSnapshot.Age` (jamais lu).

## Open Questions

1. **Fenêtre 5 h « expirée » sur activité continue > 5 h.**
   - Ce qu'on sait : algorithme (A) verrouillé → `start` = plus ancien message contigu ; si l'activité est
     continue depuis > 5 h, `reset = start+5h ≤ now` → fenêtre marquée **inactive (fraction = 1)**.
   - Ce qui est flou : sémantiquement, l'utilisateur EST actif, mais l'arc s'affiche « plein » (comme au
     repos). Une définition « bloc de 5 h depuis le 1er message, nouveau bloc au-delà » (B) collerait mieux
     à la vraie mécanique de reset de Claude.
   - Recommandation : **respecter (A)** (verrouillé par CONTEXT `<decisions>`) pour cette phase ; consigner
     (B) comme raffinement potentiel v1.2 si l'affichage « plein pendant le travail » gêne à l'usage. À
     valider empiriquement (cf. Blocker STATE.md sur la fiabilité de l'inférence).

2. **Filtre mtime < 8 j : borne exacte.**
   - Ce qu'on sait : CONTEXT recommande de borner la lecture aux fichiers récents (le code actuel ne filtre
     PAS encore). 8 j couvre la fenêtre 7 j + marge.
   - Recommandation : filtrer `LastWriteTimeUtc >= now - 8j`. Un fichier plus vieux ne peut contenir de
     message < 7 j (append-only). Ajouter un test de non-régression sur la somme hebdo.

3. **`FiveHourTokenBudget`/`WeeklyTokenBudget` : `long?` sur `ChronosSettings`.**
   - À ajouter comme deux propriétés `long?` (défaut `null`) sur le record `ChronosSettings`. Aucune UI
     (Phase 9). Vérifier que `SettingsServiceTests` (round-trip) reste vert et couvrir la sérialisation des
     deux nouveaux champs (null par défaut, valeurs relues).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK (compile net8.0-windows) | build/test | ✓ (assumé — projet v1.0 vert) | SDK 10.x ciblant net8.0 | — |
| xUnit + StaFact | suite de tests | ✓ | 2.9.2 / 1.1.11 | — |
| `~/.claude/projects` (JSONL réels) | exécution runtime (pas les tests) | ✓ (vérifié empiriquement, CONTEXT) | — | Dossier absent → séquence vide (déjà géré ROB-02) |

Phase essentiellement code/config : **aucune dépendance externe nouvelle**. Les tests utilisent des
fixtures `TestData/*.jsonl` (pas le vrai profil). Aucune dépendance bloquante manquante.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 (+ Xunit.StaFact 1.1.11 pour l'UI ; **non requis** pour l'inférence pure) |
| Config file | `tests/Chronos.Tests/Chronos.Tests.csproj` |
| Quick run command | `dotnet test tests/Chronos.Tests --filter "FiveHourWindowInference|JsonlEstimationProvider"` |
| Full suite command | `dotnet test` (à la racine solution) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| EST-01 | Début = 1er message après trou ≥ 5 h ; reset = début+5h | unit `[Fact]` | `dotnet test --filter FiveHourWindowInference` | ❌ Wave 0 |
| EST-01 | Somme 5 h = fenêtre inférée ; `ResetsAt`/`Fraction` peuplés | unit `[Fact]` | `dotnet test --filter JsonlEstimationProvider` | ⚠️ adapter (assertions null → peuplées) |
| EST-02 | Trou ≥ 5 h / aucune activité → fraction = 1, tokens = 0, util = 0/null | unit `[Fact]` | `dotnet test --filter FiveHourWindowInference` | ❌ Wave 0 |
| EST-03 | util 5 h = tokens/budget ; null sans budget ; pas de clamp haut | unit `[Fact]` | `dotnet test --filter JsonlEstimationProvider` | ❌ Wave 0 |
| EST-04 | util hebdo = tokens/budget ; fenêtre ancrée vs glissante | unit `[Fact]` | `dotnet test --filter JsonlEstimationProvider` | ❌ Wave 0 |
| EST-05 | reset hebdo via WeeklyAnchor ; sinon « — » | unit `[Fact]` | `dotnet test --filter WeeklyRecalibration` | ✅ existant (non-régression) |
| NET-01 | `SnapshotChanged`/`Age` absents ; suite verte | unit + compile | `dotnet test` | ⚠️ retirer/adapter tests concernés |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Chronos.Tests --filter "FiveHourWindowInference|JsonlEstimationProvider"`
- **Per wave merge:** `dotnet test` (suite complète — garantit non-régression des 107, moins retirés/adaptés)
- **Phase gate:** suite complète verte avant `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `tests/Chronos.Tests/FiveHourWindowInferenceTests.cs` — NOUVEAU : couvre EST-01/EST-02 (vide, message unique <5h/≥5h, trou exactement 5h, activité continue >5h, timestamps futurs). Fonction pure, `FakeClock`/`now` en paramètre.
- [ ] Fixtures `TestData/` de trous d'activité — NOUVELLES : p. ex. `sample-gap.jsonl` (messages avant/après un trou ≥ 5 h), `sample-active-window.jsonl` (rafale contiguë < 5 h), `sample-inactive.jsonl` (dernier message > 5 h). now de référence des tests = `2026-07-08T12:00:00Z` (cohérent avec l'existant).
- [ ] `JsonlEstimationProviderTests` — ADAPTER : assertions 5 h (`ResetsAt`/`Fraction` désormais non-null en fenêtre active) ; ajouter cas util avec/ sans budget (settings.json de fixture ou `SettingsService` sur root temp).
- [ ] `ClaudeUsageObjectProviderTests` — ADAPTER : retirer l'assertion `Age`, garder `SourceCapturedAt`.
- [ ] `CompositeUsageProviderTests` — RETIRER le test `SnapshotChanged_emis_une_fois...` + l'event du fake.
- [ ] `FakeUsageProvider` — retirer l'event `SnapshotChanged`.
- [ ] `SettingsServiceTests` — ÉTENDRE : round-trip des deux nouveaux `long?` (défaut null + valeurs relues).
- Framework déjà installé : aucune installation requise.

## Sources

### Primary (HIGH confidence)
- Code source du dépôt (lu intégralement) : `JsonlEstimationProvider.cs`, `ClaudeUsageObjectProvider.cs`, `CompositeUsageProvider.cs`, `RefreshOrchestrator.cs`, `MainViewModel.cs`, `WeeklyRecalibration.cs`, `SettingsService.cs`, `ChronosSettings.cs`, `ChronosPaths.cs`, `IUsageProvider.cs`, `IClock.cs`, `UsageSnapshot.cs`, `WindowState.cs`, `App.xaml.cs`.
- Tests lus : `JsonlEstimationProviderTests.cs`, `CompositeUsageProviderTests.cs`, `ClaudeUsageObjectProviderTests.cs` (extraits), `WeeklyRecalibrationTests.cs`, `ServicesLayerPurityTests.cs`, `Fakes/FakeUsageProvider.cs`, `Fakes/FakeClock.cs`, `Chronos.Tests.csproj`.
- `docs/data-sources.md` — structure JSONL, `message.usage`, timestamps ISO 8601, layout subagents.
- Grep runtime : `SnapshotChanged` (aucun abonné réel sur `IUsageProvider`), `Age` (jamais lu ; `IsStale` dérive de `SourceCapturedAt`).
- `.planning/config.json` — `nyquist_validation: true` (section Validation Architecture requise).
- CONTEXT.md, REQUIREMENTS.md, STATE.md, CLAUDE.md.

### Secondary (MEDIUM confidence)
- Aucune — pas de recherche web nécessaire (phase interne, stack figée).

### Tertiary (LOW confidence)
- Aucune.

## Metadata

**Confidence breakdown:**
- Standard stack : HIGH — aucune nouvelle dépendance, tout lu dans le dépôt.
- Architecture / algorithme : HIGH — algorithme dérivé de la décision verrouillée, testé mentalement sur les fixtures existantes (somme 5 h = 1550 conservée).
- Impact NET-01 : HIGH — grep exhaustif, chaque ligne identifiée.
- Fiabilité réelle de l'inférence à l'usage : MEDIUM — cf. Open Question 1 (définition A vs B), à valider empiriquement (Blocker STATE.md).

**Research date:** 2026-07-09
**Valid until:** ~30 jours (code interne stable ; invalidé seulement par une refonte du pipeline de données ou un changement de format JSONL de Claude Code).
