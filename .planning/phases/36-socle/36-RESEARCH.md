# Phase 36 : Socle — Recherche

**Recherché le :** 2026-10-03
**Domaine :** System.Text.Json (lecture tolérante valeur par valeur) + aiguillage des arguments de démarrage WPF
**Confiance :** HAUTE (comportements System.Text.Json vérifiés par prototype net8.0 exécuté sur la machine ; code et gardes existants lus)

<user_constraints>
## Contraintes utilisateur (depuis CONTEXT.md)

### Décisions verrouillées

#### SOC-01 — Réglages tolérants
- Constat (inventaire) : `SettingsService.Load()` (`src/Chronos/Services/SettingsService.cs:38-53`) attrape `JsonException`
  et renvoie `new ChronosSettings()` ENTIER ; `JsonStringEnumConverter` lève sur une valeur d'enum inconnue ⇒ un seul enum
  inconnu efface thème, coin, géométries, `InnerStatusLineCommand`, puis le prochain `Save(Load() with …)` réécrit ces
  défauts (perte définitive).
- Décision : une valeur d'énumération inconnue retombe sur **la valeur par défaut de CETTE propriété** ; toutes les autres
  propriétés sont conservées. Membres inconnus : ignorés (comportement System.Text.Json actuel, déjà testé
  `SettingsServiceTests.cs:208-224`). S'applique à tous les enums de `ChronosSettings` (`CadranStyle`, `CadranDisplayMode`,
  `SessionStyle`, `HistoriqueStyleSemaine` tant qu'il existe, `SectionReglages`, `OverlayCorner`, et tout enum futur comme
  l'orientation de la phase 40) — mécanisme générique (convertisseur tolérant ou équivalent), pas un cas par propriété.
- Un JSON réellement illisible (tronqué, non-JSON) reste toléré comme aujourd'hui (défauts, pas de crash) — mais ne doit pas
  produire de réglages « à moitié lus » ; le comportement retenu doit être explicite et testé.
- Le chargement tolérant doit être journalisé (une ligne de log/diagnostic quand une valeur est retombée sur son défaut), sans
  bruit à chaque lecture si possible.

#### SOC-02 — Garde d'arguments
- Constat : `App.xaml.cs:23-31` aiguille `--statusline` (avant le verrou posé vers `:79-84`) ; `--hook`, `--cadrans`,
  `--historique`, `--sessions`, le mode CLI existent. Une invocation inconnue lancerait aujourd'hui l'overlay complet et
  heurterait le verrou (MessageBox « Chronos tourne déjà »).
- Décision : une fonction **pure** de tri des arguments (testable sans WPF) classe l'invocation : mode connu → comportement
  actuel ; argument commençant par `--` inconnu (ou retiré) → sortie silencieuse code 0, AVANT verrou, AVANT réconciliation
  des hooks, sans fenêtre. Aucun argument → overlay normal.
- Après la phase 37, `--statusline` deviendra un argument retiré : il devra tomber dans ce cas (la garde doit donc reposer sur
  une liste blanche des modes connus, pas sur une liste noire).
- Garde textuelle (test) sur l'ordre dans `App.xaml.cs` : le tri des arguments précède l'acquisition du verrou.

### Latitude de Claude
- Forme exacte du convertisseur tolérant (JsonConverterFactory générique vs pré-passe JsonNode) ; nom des types ; emplacement
  (`Services/`, sans type WPF — `ServicesLayerPurityTests`).
- Forme du journal de retombée.

### Idées reportées (HORS PÉRIMÈTRE)
None — la suppression effective de `HistoriqueStyleSemaine` est en phase 38, celle de `--statusline` en phase 37.
</user_constraints>

<phase_requirements>
## Exigences de la phase

| ID | Description | Ce que la recherche apporte |
|----|-------------|-----------------------------|
| SOC-01 | Un `settings.json` contenant une valeur d'énumération inconnue ou un membre supprimé ne remet plus tous les réglages à zéro : seule la valeur fautive retombe sur son défaut, le reste est conservé et réécrit intact ; épinglé par tests (`"Tuiles"`, style de cadran inconnu, clé de thème inconnue). | Pré-passe `JsonNode` + validation par propriété avec les MÊMES `Options` (prototype validé) ; piège `default(T)` ≠ défaut de propriété (`Corner` = `TopRight` = index 1) ; piège `GetTypeInfo` sans resolver ; entiers hors enum acceptés par STJ ; clé de thème = chaîne, retombée à l'affichage par `ThemeCatalog.ByKey`. |
| SOC-02 | Tout `--xxx` inconnu ou retiré sort silencieusement (code 0) avant le verrou ; `--hook` et le mode CLI inchangés. | Classificateur pur en liste blanche dans `Chronos.Services` ; préséance actuelle à préserver ; **les gardes textuelles existantes cherchent les littéraux `"--statusline"`, `"--hook"`, `"--cadrans"`, `"--sessions"`, `"--historique"` dans App.xaml.cs** et doivent être réécrites dans le même plan. |
</phase_requirements>

## Contraintes projet (depuis CLAUDE.md)

- C# / .NET 8 (`net8.0-windows`), WPF, MVVM CommunityToolkit, DI Microsoft — aucune nouvelle dépendance NuGet n'est nécessaire (System.Text.Json est intégré).
- Types de `Chronos.Services` / `Chronos.Models` **neutres** : aucun type WPF en signature (`ServicesLayerPurityTests`). Le classificateur d'arguments et la lecture tolérante vivent dans `Services/`.
- Chemins sous profil utilisateur uniquement ; jamais `Assembly.Location` (vide en mono-fichier) — les gardes textuelles utilisent `GardesPerimetreTests.CheminSources()` (chemin injecté par MSBuild).
- Robustesse : aucune source ≠ crash ; parsing tolérant (champs invalides ignorés).
- Commentaires et textes en français.
- Workflow GSD : modifications via `/gsd:execute-phase`.
- Critère de phase : suite à 0 échec, 0 warning (pas de `TreatWarningsAsErrors` dans les csproj — le « 0 warning » se vérifie à la lecture de la sortie de `dotnet build`).

## Résumé

Deux corrections courtes, toutes deux en code neutre testable, avec un point d'attention majeur côté gardes existantes.

**SOC-01.** La bonne forme est une **pré-passe `JsonNode`** dans `SettingsService.Load()` : (1) parser tout le document (`JsonNode.Parse`) — une erreur de syntaxe n'importe où lève AVANT toute exploitation, ce qui garantit mécaniquement « jamais à moitié lu » ; (2) pour chaque membre JSON qui correspond à une propriété de `ChronosSettings`, tenter `JsonSerializer.Deserialize(valeur, typePropriété, Options)` avec les **mêmes options** que la lecture réelle, plus un contrôle `Enum.IsDefined` ; (3) retirer du `JsonObject` les membres fautifs ; (4) désérialiser l'objet nettoyé — l'initialiseur de propriété du record (`= OverlayCorner.TopRight`, etc.) fournit alors exactement « le défaut de CETTE propriété ». Un `JsonConverterFactory` enum tolérant est **à écarter** : un convertisseur ne connaît que le type, pas la propriété, et rendrait `default(T)` — faux pour `Corner` (`TopRight` est l'index 1, `default` = `TopLeft`). La pré-passe couvre en outre gratuitement les incohérences de type non-enum (`"X": "abc"`, `null` sur un `bool`), qui aujourd'hui effacent aussi tout le fichier.

**SOC-02.** Un classificateur pur `ArgumentsDemarrage.Trier(string[]) → InvocationDemarrage(Mode, EvenementHook)` en liste blanche, appelé en **première instruction** de `OnStartup`, avec un `switch` sur le mode. Le cas `Inconnu` fait `Environment.Exit(0)` (motif déjà utilisé par `--statusline`). Attention : `GardesPerimetreTests.Le_verrou_mono_instance_est_pose_apres_les_court_circuits_et_avant_le_Host` et `Le_mode_historique_precede_le_verrou_et_ne_resout_aucun_service` repèrent les modes par leurs **littéraux** dans App.xaml.cs ; si les littéraux migrent dans le classificateur, ces gardes échouent (« Un court-circuit CLI a disparu »). Elles doivent être réécrites dans le même plan, sans affaiblir ce qu'elles prouvent.

**Recommandation principale :** pré-passe JsonNode validée par les Options réelles + classificateur d'arguments en liste blanche dans `Chronos.Services`, gardes textuelles de placement réécrites sur le nouvel appel `ArgumentsDemarrage.Trier(e.Args)`.

## Pile standard

### Cœur
| Bibliothèque | Version | Rôle | Pourquoi |
|---|---|---|---|
| System.Text.Json (`JsonNode`, `JsonSerializer`, `JsonStringEnumConverter`) | intégré net8.0 | Pré-passe et lecture | Déjà utilisé ; `JsonNode` (System.Text.Json.Nodes) permet de retirer un membre puis de désérialiser l'objet avec `JsonSerializer.Deserialize<T>(JsonNode, options)`. Vérifié par prototype. |
| `System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver` | intégré net8.0 | Énumérer les propriétés telles que le sérialiseur les voit (`Options.GetTypeInfo(typeof(ChronosSettings)).Properties`) | Optionnel — voir piège 2 ; la réflexion simple (`typeof(ChronosSettings).GetProperties()`) convient aussi. |
| xUnit 2.9.2 (+ Xunit.StaFact) | existant | Tests | Infrastructure en place. |

Aucune installation.

### Alternatives écartées
| Au lieu de | On pourrait | Pourquoi non |
|---|---|---|
| Pré-passe JsonNode | `JsonConverterFactory` enum tolérant rendant `default(T)` | Ne connaît pas le défaut de la propriété : `Corner` inconnu → `TopLeft` au lieu de `TopRight`. Ne couvre pas les erreurs non-enum. |
| Pré-passe JsonNode | Modificateur `DefaultJsonTypeInfoResolver` posant par propriété un `CustomConverter` qui renvoie la valeur lue sur `new ChronosSettings()` | Faisable, mais plus subtil (convertisseur par propriété, cache), limité aux enums, et la journalisation des retombées doit traverser le convertisseur. La pré-passe est plus lisible et plus générale. |
| Pré-passe JsonNode | Désérialiser propriété par propriété avec `JsonSerializer.Deserialize(node, type)` puis reconstruire le record par réflexion | Réimplémente le sérialiseur ; inutile puisque retirer le membre fautif suffit. |

## Patrons d'architecture

### Structure recommandée
```
src/Chronos/Services/
├── SettingsService.cs          # Load() devient : parse → validation par membre → retrait → désérialisation
│                               # + propriété DernieresRetombees (rapport de la dernière lecture)
├── LectureReglagesTolerante.cs # (option) fonction pure statique : JsonObject → (JsonObject nettoyé, retombées)
└── ArgumentsDemarrage.cs       # ModeDemarrage (enum), InvocationDemarrage (record), Trier(string[]) PUR
src/Chronos/App.xaml.cs         # OnStartup : var invocation = ArgumentsDemarrage.Trier(e.Args); switch (invocation.Mode) …
tests/Chronos.Tests/
├── SettingsServiceTests.cs     # + tests SOC-01 (fixture témoin, illisible, réécriture intacte, journal)
├── TestData/settings-valeurs-inconnues.json  # fixture témoin (nouvelle)
├── ArgumentsDemarrageTests.cs  # nouveau : table de vérité du tri
└── GardesPerimetreTests.cs     # gardes de placement réécrites (tri AVANT verrou, Inconnu → Exit(0) avant verrou)
```

### Patron 1 : pré-passe JsonNode validée par les Options réelles (SOC-01)
**Quoi :** valider chaque valeur avec exactement la machinerie de la lecture finale, retirer les fautives, puis désérialiser.
**Exemple (prototype exécuté, adapté) :**
```csharp
// Source : prototype net8.0 exécuté le 2026-10-03 (scratchpad stjproto) — sorties reproduites en « Exemples de code ».
private static readonly JsonSerializerOptions Options = new()
{
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() },
    TypeInfoResolver = new DefaultJsonTypeInfoResolver(),   // OBLIGATOIRE si on appelle Options.GetTypeInfo (piège 2)
};

private static readonly JsonDocumentOptions OptionsDocument = new()
{
    AllowTrailingCommas = true,                       // mêmes tolérances que la lecture actuelle
    CommentHandling = JsonCommentHandling.Skip,
};

public ChronosSettings Load()
{
    try
    {
        if (!File.Exists(_paths.SettingsFile)) { DernieresRetombees = []; return new ChronosSettings(); }
        var texte = File.ReadAllText(_paths.SettingsFile);

        // 1) Document ENTIER parsé d'abord : une erreur de syntaxe n'importe où lève ici,
        //    avant qu'une seule valeur ne soit exploitée → jamais de réglages « à moitié lus ».
        if (JsonNode.Parse(texte, null, OptionsDocument) is not JsonObject racine)
        { DernieresRetombees = []; return new ChronosSettings(); }   // null, tableau, scalaire : comme aujourd'hui

        // 2) Valeur par valeur, avec les MÊMES options que la lecture finale.
        var proprietes = Options.GetTypeInfo(typeof(ChronosSettings)).Properties
                                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var retombees = new List<string>();
        foreach (var (nom, valeur) in racine.ToList())
        {
            if (!proprietes.TryGetValue(nom, out var p)) continue;          // membre inconnu : STJ l'ignore déjà
            if (!ValeurAcceptable(valeur, p.PropertyType)) { racine.Remove(nom); retombees.Add(nom); }
        }

        // 3) Lecture finale de l'objet nettoyé : les membres retirés prennent l'initialiseur du record.
        DernieresRetombees = retombees;
        return racine.Deserialize<ChronosSettings>(Options) ?? new ChronosSettings();
    }
    catch (Exception ex) when (ex is IOException or JsonException or ArgumentException
                                  or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
    {
        DernieresRetombees = [];                // ou un marqueur « fichier illisible » : voir journal
        return new ChronosSettings();           // illisible → défauts ENTIERS (comportement conservé, ROB-02)
    }
}

private static bool ValeurAcceptable(JsonNode? valeur, Type type)
{
    var sousJacent = Nullable.GetUnderlyingType(type);
    if (valeur is null) return sousJacent is not null || !type.IsValueType;   // null sur bool/double/enum non-nullable → refusé
    try
    {
        var lu = valeur.Deserialize(type, Options);
        var t = sousJacent ?? type;
        return !(t.IsEnum && lu is not null && !Enum.IsDefined(t, lu));       // 99 → refusé (piège 3)
    }
    catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
    {
        return false;
    }
}
```
Notes :
- `null` sur un `string?` (ex. `InnerStatusLineCommand`) est accepté ; sur un `string` non-nullable (`ThemeKey`) STJ l'accepte aussi (NRT non appliquées en .NET 8) — décider si `"ThemeKey": null` doit retomber sur `"minuit"` (recommandé : oui, ajouter `!type.IsValueType && type == typeof(string)` non-nullable → refusé ; l'information de nullabilité est accessible via `NullabilityInfoContext` ou plus simplement une règle « `ThemeKey` null refusé »). Aujourd'hui `ThemeKey = null` passe et `ByKey(null)` retombe sur le défaut à l'affichage : sans danger, donc LOW priorité.
- Ne pas rendre `Options` « read-only » par erreur avant le premier `Serialize` : `GetTypeInfo` avec resolver explicite ne le verrouille pas (vérifié : `IsReadOnly = False`), et `Save` continue de fonctionner.

### Patron 2 : journal de retombée sans bruit
**Recommandation :** `SettingsService` n'écrit RIEN sur disque en lecture (29 appels à `Load()` dans le code, motif `Save(Load() with …)`). Il expose `IReadOnlyList<string> DernieresRetombees` (noms des propriétés retombées lors de la dernière lecture, éventuellement avec la valeur brute tronquée). `DiagnosticService.BuildReportAsync` appelle déjà `_settings.Load()` (l. 149) et écrit `chronos.log` **une fois au démarrage** (`LogStartupAsync`, `File.WriteAllText`) : y ajouter une ligne, p. ex. dans `[Magasins persistants]` ou une ligne dédiée « Réglages : N valeur(s) retombée(s) sur leur défaut — CadranStyle, SessionStyle ». Une ligne par démarrage = « sans bruit ».
- Ne PAS ajouter la ligne dans la section `[Réglage]` : la phase 37 retire C1 « Usage exact (OAuth) » de cette section (inventaire § C1).
- Ne PAS faire un `File.AppendAllText` dans `Load()` : `LogStartupAsync` écrase `chronos.log` à chaque démarrage, et `Load()` est appelé à chaque changement de réglage (bruit + concurrence).
- Distinguer dans le rapport « fichier illisible → défauts entiers » de « N retombées » (un état/énum `IssueLecture { Absent, Lu, LuAvecRetombees, Illisible }` ou un booléen `DerniereLectureIllisible`) — utile pour le critère 2 (« comportement explicite et testé »).

### Patron 3 : classificateur d'arguments pur en liste blanche (SOC-02)
```csharp
namespace Chronos.Services;

/// <summary>Mode de démarrage décidé AVANT toute initialisation (verrou, Host, hooks).</summary>
public enum ModeDemarrage { Overlay, StatusLine, Hook, GalerieCadrans, GalerieSessions, GalerieHistorique, ArgumentInconnu }

public sealed record InvocationDemarrage(ModeDemarrage Mode, string? EvenementHook = null);

public static class ArgumentsDemarrage
{
    public const string StatusLine = "--statusline";   // retiré en phase 37 : il suffira de supprimer sa ligne
    public const string Hook = "--hook";
    public const string Cadrans = "--cadrans";
    public const string Sessions = "--sessions";
    public const string Historique = "--historique";

    public static InvocationDemarrage Trier(IReadOnlyList<string> args)
    {
        // Préséance IDENTIQUE à la 3.4.0 (Any() sur tous les arguments, insensible à la casse) :
        // --statusline > --hook > --cadrans > --sessions > --historique.
        if (Contient(args, StatusLine)) return new(ModeDemarrage.StatusLine);
        int i = Index(args, Hook);
        if (i >= 0) return new(ModeDemarrage.Hook, i + 1 < args.Count ? args[i + 1] : null);
        if (Contient(args, Cadrans)) return new(ModeDemarrage.GalerieCadrans);
        if (Contient(args, Sessions)) return new(ModeDemarrage.GalerieSessions);
        if (Contient(args, Historique)) return new(ModeDemarrage.GalerieHistorique);
        // LISTE BLANCHE : tout autre « --xxx » (inconnu ou retiré) → sortie silencieuse.
        if (args.Any(a => a.StartsWith("--", StringComparison.Ordinal))) return new(ModeDemarrage.ArgumentInconnu);
        return new(ModeDemarrage.Overlay);
    }
    // Contient / Index : string.Equals(a, x, StringComparison.OrdinalIgnoreCase)
}
```
Dans `App.OnStartup`, première instruction :
```csharp
var invocation = ArgumentsDemarrage.Trier(e.Args);
switch (invocation.Mode)
{
    case ModeDemarrage.ArgumentInconnu:
        Environment.Exit(0);   // silence : ni fenêtre, ni verrou, ni hooks, ni stderr
        return;
    case ModeDemarrage.StatusLine: RunStatusLineBridge(); Environment.Exit(0); return;
    case ModeDemarrage.Hook: Environment.Exit(RunSessionHook(invocation.EvenementHook)); return;
    case ModeDemarrage.GalerieCadrans: /* inchangé */ return;
    // …
}
// ModeDemarrage.Overlay : base.OnStartup(e); puis verrou, Host… inchangés
```
Les commentaires historiques de chaque branche (pourquoi avant le verrou, etc.) sont à conserver dans les `case`.

### Anti-patrons
- **Liste noire** (`if (arg == "--statusline-retire") exit`) : contredit la décision ; la phase 37 doit pouvoir retirer `--statusline` en supprimant une seule ligne de la liste blanche.
- **Tri après `base.OnStartup(e)` ou après le verrou** : la MessageBox « tourne déjà » apparaîtrait.
- **`Shutdown()` au lieu de `Environment.Exit(0)`** pour le cas inconnu : `Shutdown` passe par la boucle de messages et `OnExit` ; possible mais moins direct ; le motif existant (`--statusline`) est `Environment.Exit(0)`.
- **Écrire sur stderr pour un argument inconnu** : décision = silence (Claude Code affiche le stderr des hooks à l'utilisateur).
- **Normaliser `ThemeKey` inconnu dans `SettingsService`** : impossible proprement — `ChronosTheme` utilise `System.Windows.Media` (violation de `ServicesLayerPurityTests`). La clé inconnue est conservée telle quelle ; `ThemeCatalog.ByKey` retombe déjà sur « minuit » à l'affichage (`ThemingTests.ByKey_inconnu_retombe_sur_minuit_insensible_a_la_casse`).

## Ne pas réinventer

| Problème | Ne pas construire | Utiliser | Pourquoi |
|---|---|---|---|
| Savoir si une valeur JSON est lisible pour un type | Parseur d'enum maison (`Enum.TryParse`) | `JsonNode.Deserialize(type, Options)` avec les Options réelles | `Enum.TryParse` diffère de `JsonStringEnumConverter` (chaînes numériques, listes à virgules, casse). Seule la machinerie réelle garantit « ce qui passe la pré-passe passe la lecture finale ». |
| Détecter un JSON tronqué | Heuristiques (accolades, longueur) | `JsonNode.Parse` sur le document entier | Lève `JsonException` (sous-type interne `JsonReaderException`) sur toute erreur de syntaxe. |
| Défaut d'une propriété | Table des défauts dupliquée | Retirer le membre du `JsonObject` ; l'initialiseur du record s'applique | Une seule source de vérité (`ChronosSettings`), valable pour tout enum futur (orientation phase 40). |

## Pièges courants

### Piège 1 : `default(T)` n'est pas le défaut de la propriété
**Ce qui casse :** un convertisseur enum tolérant renvoyant `default(T)` remet `Corner` à `TopLeft` (index 0) alors que le défaut est `TopRight` (index 1, `OverlayCorner.cs`). Les autres enums ont aujourd'hui leur défaut à l'index 0 — le bug ne se verrait que sur `Corner` ou sur un futur enum.
**Prévention :** pré-passe (retrait du membre). Test : `"Corner": "Fantome"` → `OverlayCorner.TopRight`.

### Piège 2 : `Options.GetTypeInfo` lève sans resolver explicite (.NET 8)
**Constaté au prototype :** `NotSupportedException: JsonTypeInfo metadata for type 'S' was not provided by TypeInfoResolver of type '<null>'` sur des options construites par `new()`. Résolu en ajoutant `TypeInfoResolver = new DefaultJsonTypeInfoResolver()`. Alternative sans risque : `typeof(ChronosSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)` (pas de renommage JSON dans le record, les noms coïncident). `PublishTrimmed=false` : la réflexion est sûre en publication.

### Piège 3 : STJ accepte des valeurs d'enum non définies
**Constaté au prototype :** `"Corner": 99` → `Corner = 99` sans erreur ; `"Corner": "2"` (chaîne numérique) → `BottomLeft` ; `"TopLeft, TopRight"` → combinaison binaire (`TopRight`). Une section de réglages `99` ou un style `99` pourraient faire planter un `switch` ou un index plus loin.
**Prévention :** contrôle `Enum.IsDefined` dans la pré-passe (aucun enum de `ChronosSettings` n'est `[Flags]`). Le cas `"2"` reste accepté (valeur définie) — comportement actuel, sans risque.

### Piège 4 : `null` sur un enum ou un `bool` non-nullable
**Constaté :** `"Corner": null` → `JsonException` (efface aujourd'hui tout le fichier). La pré-passe le traite comme une retombée.

### Piège 5 : clés dupliquées
**Constaté :** `JsonNode.Parse` + accès à un objet avec deux clés EXACTEMENT identiques → `ArgumentException` (déjà attrapée par `Load` → défauts entiers ; cas pathologique, acceptable et cohérent avec « illisible »). Des clés différant par la casse (`Theme`/`theme`) sont acceptées par le `JsonObject` (sensible à la casse par défaut — NE PAS passer `JsonNodeOptions { PropertyNameCaseInsensitive = true }`, sinon ce cas devient une exception) ; la lecture finale garde la dernière, comme aujourd'hui.

### Piège 6 : les gardes textuelles existantes cherchent les littéraux d'arguments dans App.xaml.cs
**Ce qui casse :** `GardesPerimetreTests.Le_verrou_mono_instance_est_pose_apres_les_court_circuits_et_avant_le_Host` (l. ~515-546) fait `LastIndexOf("\"--statusline\"")`, `"\"--hook\""`, `"\"--cadrans\""`, `"\"--sessions\""` et échoue si l'un est absent (« Un court-circuit CLI a disparu ») ; `Le_mode_historique_precede_le_verrou_et_ne_resout_aucun_service` (l. ~632-653) cherche `"\"--historique\""` puis le premier `return;` et vérifie le bloc (`HistoriqueGalerie.Creer(`, pas de `GetRequiredService`/`Host`/`Reconcil`).
**Prévention :** dans le MÊME plan, réécrire ces gardes sur la nouvelle forme : repères `ArgumentsDemarrage.Trier(e.Args)`, `case ModeDemarrage.StatusLine`, `case ModeDemarrage.Hook`, `case ModeDemarrage.GalerieCadrans`, `GalerieSessions`, `GalerieHistorique`, `ArgumentInconnu` — chacun avant `VerrouInstanceUnique.Acquerir(VerrouInstanceUnique.NomOverlay)` ; le bloc `case ModeDemarrage.GalerieHistorique` jusqu'à son `return;` conserve les mêmes interdits. Conserver intactes les autres assertions (acquisition unique, avant `Host.CreateApplicationBuilder()`, « tourne déjà », `Shutdown();`, pas de `.Kill(`). Le critère 4 « tests existants verts » s'entend à preuve égale : la réécriture ne doit retirer aucune propriété prouvée. Prévoir que la phase 37 retirera encore `case ModeDemarrage.StatusLine` et la constante — la garde de placement ne doit donc pas exiger le cas StatusLine de façon que sa suppression soit ambiguë (le plus simple : la phase 37 met à jour la garde ; le noter dans le SUMMARY).
**Autres références textuelles à vérifier :** commentaires de App.xaml.cs mentionnant « lignes 20 et 30 » (l. 122, 140) — à corriger au passage.

### Piège 7 : sémantique « `Any()` sur tous les arguments »
La 3.4.0 reconnaît un mode s'il apparaît **n'importe où** dans les arguments, pas seulement en premier. `--hook Stop` : `Stop` ne commence pas par `--`, donc pas d'ambiguïté. Préserver cette sémantique (critère 4) : un mode connu présent l'emporte sur un `--xxx` inconnu qui l'accompagne. Les arguments sans `--` (ex. `Stop` seul, `/foo`, `-x`) → overlay, comme aujourd'hui (la décision ne vise que `--`).

### Piège 8 : test de non-régression sur JSON corrompu
`Load_json_corrompu_redonne_les_defauts_sans_exception` attend `Assert.Equal(new ChronosSettings(), s)` (égalité de record). Toujours vrai avec la pré-passe. Ajouter les cas tronqué (`{"ThemeKey":"nord","Corner":"BottomLeft"` sans `}`) : doit donner les défauts ENTIERS, et en particulier `ThemeKey == "minuit"` (preuve qu'aucune valeur partielle n'a fui).

## Exemples de code (sorties vérifiées du prototype net8.0)

```
lowercase  {"Corner":"bottomleft"}          → OK BottomLeft          (lecture insensible à la casse)
numstr     {"Corner":"2"}                   → OK BottomLeft          (chaîne numérique acceptée)
int99      {"Corner":99}                    → OK Corner=99           (NON défini, accepté → contrôler IsDefined)
unknown    {"Corner":"Nope","Theme":"nord"} → JsonException path=$.Corner  (comportement qui efface tout aujourd'hui)
null       {"Corner":null}                  → JsonException path=$.Corner
JsonNode.Parse("{ ceci n'est pas ]")        → JsonReaderException (dérive de JsonException)
JsonNode.Parse("[1,2]")                     → JsonArray  (→ défauts)
JsonNode.Parse("null")                      → null       (→ défauts)
Pré-passe sur { // c  "corner":"BottomLeft", "Style":"Spirale", "Theme":"nord", "X":"abc", "Gone":{…}, "Corner2":99, }
  → retombées : Style, X, Corner2 ; résultat : Corner=BottomLeft, Style=Arcs, Theme=nord, X=null, Corner2=BottomRight (défaut ≠ index 0)
```

Fixture témoin recommandée (`TestData/settings-valeurs-inconnues.json`, lue comme `settings-legacy-plafonds.json` via `TestDataPath`) :
```json
{
  "ThemeKey": "nord",
  "Corner": "BottomLeft",
  "MonitorDeviceName": "\\\\.\\DISPLAY2",
  "CadranStyle": "Spirale",
  "CadranMode": "Etendu",
  "HistoriqueStyleSemaine": "Mosaique",
  "SessionStyle": "Inconnu",
  "ReglagesSection": "Fantome",
  "InnerStatusLineCommand": "echo hi",
  "HistoriqueX": 100, "HistoriqueY": 120, "HistoriqueWidth": 1000, "HistoriqueHeight": 700,
  "ReglagesX": 200, "ReglagesY": 220, "ReglagesWidth": 900, "ReglagesHeight": 600,
  "SessionsX": 10, "SessionsY": 20,
  "OrientationCadran": "Verticale"
}
```
Note : `"Tuiles"` est AUJOURD'HUI une valeur valide de `HistoriqueStyleSemaine` ; pour prouver la retombée avant la phase 38, utiliser une valeur inexistante (`"Mosaique"`) ; ajouter un test explicitement nommé pour `"Tuiles"` qui deviendra significatif quand la phase 38 supprimera la propriété (membre inconnu ignoré) — ou laisser la phase 38 l'ajouter. `"OrientationCadran"` simule un membre futur/supprimé (ignoré). Attention : la propriété s'appelle `ReglagesSection` (pas `SectionReglages`, qui est le nom de l'enum) et `CadranMode` (enum `CadranDisplayMode`).

## État de l'art

| Ancien | Actuel | Impact |
|---|---|---|
| `catch (JsonException) → new ChronosSettings()` sur toute erreur | Pré-passe : erreur de syntaxe → défauts entiers ; erreur de valeur → retombée de cette seule valeur | Plus de perte définitive de thème/coin/géométries au prochain `Save` |
| Aiguillage par `if (e.Args.Any(...))` successifs dans `OnStartup` | Tri pur `ArgumentsDemarrage.Trier` + `switch` | Testable sans WPF ; liste blanche ; inconnu → `Exit(0)` avant verrou |

## Questions ouvertes

1. **`ThemeKey` inconnu : conserver ou normaliser ?**
   - Ce qu'on sait : `SettingsService` ne peut pas consulter `ThemeCatalog` (WPF). `ByKey` retombe déjà sur « minuit » à l'affichage.
   - Recommandation : conserver la chaîne brute (réécrite telle quelle au `Save`, inoffensive) ; tester que les autres valeurs sont intactes ET que `ThemeCatalog.ByKey(clé inconnue).Key == "minuit"`. La phase 39 (thèmes) pourra décider d'une normalisation au niveau VM si un thème est retiré. Cohérent avec le critère « la clé de thème inconnue retombe sur son défaut » au sens de l'effet visible.
2. **Faut-il retomber aussi sur les valeurs non-enum mal typées (`"X":"abc"`) ?**
   - La décision vise les enums ; la pré-passe générique les couvre sans coût. Recommandation : oui (strictement plus tolérant, même esprit ROB-02), avec un test.
3. **Arguments sans `--` (`-x`, `/x`) ?**
   - Hors décision. Recommandation : comportement actuel (overlay). À signaler au SUMMARY.

## Disponibilité de l'environnement

| Dépendance | Requise par | Disponible | Version | Repli |
|---|---|---|---|---|
| SDK .NET | build/tests | oui | 10.0.201 (cible net8.0-windows) | — |
| xUnit + runner | tests | oui | 2.9.2 / 2.8.2 | — |

Aucune dépendance manquante. Base mesurée : `SettingsServiceTests` + `GardesPerimetreTests` + `VerrouInstanceUniqueTests` = 38 tests verts en ~9 s (build incrémental compris).

## Architecture de validation (Nyquist)

### Cadre de test
| Propriété | Valeur |
|---|---|
| Framework | xUnit 2.9.2 (+ Xunit.StaFact 1.1.11), net8.0-windows |
| Fichier de config | `tests/Chronos.Tests/Chronos.Tests.csproj` |
| Commande rapide | `dotnet test tests/Chronos.Tests/Chronos.Tests.csproj --filter "FullyQualifiedName~SettingsServiceTests\|FullyQualifiedName~ArgumentsDemarrageTests\|FullyQualifiedName~GardesPerimetreTests"` |
| Suite complète | `dotnet test Chronos.sln` (puis vérifier « 0 Avertissement(s) » dans `dotnet build Chronos.sln`) |

### Critères → tests
| Critère / Req | Comportement | Type | Commande | Existe ? |
|---|---|---|---|---|
| C1 / SOC-01 | Fixture témoin : `ThemeKey=nord`, `Corner=BottomLeft`, `InnerStatusLineCommand`, 10 géométries, `MonitorDeviceName`, `CadranMode=Etendu` conservés ; `CadranStyle=Arcs`, `SessionStyle=Pastilles`, `ReglagesSection=Donnees`, `HistoriqueStyleSemaine=Pistes` retombés | unitaire | `--filter "FullyQualifiedName~SettingsServiceTests"` | ❌ Wave 0 (fixture + test) |
| C1 / SOC-01 | `Save(Load())` puis relecture du FICHIER : valeurs conservées présentes à l'identique, valeurs fautives réécrites avec leur défaut, membre inconnu disparu | unitaire | idem | ❌ |
| C1 / SOC-01 | `Corner` inconnu → `TopRight` (défaut ≠ index 0) ; `Corner: 99` → `TopRight` ; `Corner: null` → `TopRight` | unitaire (Theory) | idem | ❌ |
| C1 / SOC-01 | Générique : chaque propriété enum de `ChronosSettings` (énumérée par réflexion) avec `"Fantome"` retombe sur `new ChronosSettings()` pour CETTE propriété, les autres intactes — couvre tout enum futur sans test à écrire | unitaire (réflexion) | idem | ❌ |
| C1 / SOC-01 | Clé de thème inconnue : chargée sans effet sur le reste ; `ThemeCatalog.ByKey` → « minuit » | unitaire | `--filter "FullyQualifiedName~SettingsServiceTests\|FullyQualifiedName~ThemingTests"` | partiel (ByKey existe) |
| C1 / SOC-01 | Journal : `DernieresRetombees` liste exactement les propriétés fautives ; vide sur fichier sain ; ligne présente dans le rapport de diagnostic | unitaire | `--filter "FullyQualifiedName~SettingsServiceTests\|FullyQualifiedName~DiagnosticServiceTests"` | ❌ |
| C2 / SOC-01 | Non-JSON → `new ChronosSettings()` | unitaire | `--filter "FullyQualifiedName~SettingsServiceTests"` | ✅ `Load_json_corrompu_redonne_les_defauts_sans_exception` |
| C2 / SOC-01 | JSON tronqué contenant des valeurs valides → défauts ENTIERS (`ThemeKey == "minuit"`, `Corner == TopRight`) — jamais « à moitié lu » ; racine tableau / `null` → défauts | unitaire | idem | ❌ |
| C2 / SOC-01 | Non-régression legacy : `Reglages_avec_anciens_plafonds…`, `Champ_obsolete_de_type_incoherent_est_ignore`, `Valeur_d_enum_supprimee_invalide_est_ignoree` | unitaire | idem | ✅ |
| C3 / SOC-02 | Table de vérité `Trier` : `[]`→Overlay ; `--statusline`→StatusLine ; `--STATUSLINE`→StatusLine ; `--hook Stop`→Hook("Stop") ; `--hook`→Hook(null) ; `--cadrans`/`--sessions`/`--historique`→galeries ; `--zzz`→ArgumentInconnu ; `--hook Stop --zzz`→Hook ; `Stop`→Overlay ; préséance `--statusline --hook X`→StatusLine | unitaire (Theory) | `--filter "FullyQualifiedName~ArgumentsDemarrageTests"` | ❌ Wave 0 |
| C3 / SOC-02 | Liste blanche : simuler le retrait (la phase 37 supprimera la constante) — test qui vérifie que `Trier` ne contient aucune liste noire : tout `--` absent de la liste des modes connus (exposée, p. ex. `ArgumentsDemarrage.ModesConnus`) → ArgumentInconnu, pour un échantillon d'arguments générés | unitaire | idem | ❌ |
| C3 / SOC-02 | Garde textuelle : dans `App.xaml.cs`, `ArgumentsDemarrage.Trier(e.Args)` est la PREMIÈRE instruction de `OnStartup`, précède `base.OnStartup(e)` de l'overlay, `VerrouInstanceUnique.Acquerir`, `Host.CreateApplicationBuilder()` et `Reconcile(` ; le bloc `case ModeDemarrage.ArgumentInconnu` contient `Environment.Exit(0)` et ne contient ni `MessageBox`, ni `Show(`, ni `GetRequiredService`, ni `SignalerSurErreurStandard` | garde de source | `--filter "FullyQualifiedName~GardesPerimetreTests"` | ❌ (nouvelle) |
| C4 / SOC-02 | Gardes de placement existantes réécrites (verrou après tous les court-circuits, `--historique` sans service) ; tests `VerrouInstanceUniqueTests`, `SessionsTests`, `StatusLineBridgeTests`, `EcritureEtatSessionTests` verts | garde + unitaires | `--filter "FullyQualifiedName~GardesPerimetreTests\|FullyQualifiedName~VerrouInstanceUniqueTests\|FullyQualifiedName~StatusLineBridgeTests\|FullyQualifiedName~EcritureEtatSessionTests\|FullyQualifiedName~SessionsTests"` | ✅ (à adapter) |
| C4 | Pureté : `ArgumentsDemarrage` et la lecture tolérante dans `Chronos.Services` sans WPF | garde | `--filter "FullyQualifiedName~ServicesLayerPurityTests"` | ✅ |
| C4 | Suite complète 0 échec, build 0 warning | suite | `dotnet build Chronos.sln` puis `dotnet test Chronos.sln` | ✅ |
| C3 (manuel, optionnel) | Exe réel : `Start-Process -Wait -PassThru …\Chronos.exe --zzz` → `ExitCode 0`, aucune fenêtre, même avec l'overlay en cours | smoke manuel | PowerShell | manuel (lancer un exe WPF sous test risquerait d'ouvrir l'overlay si la garde régressait) |

### Fréquence d'échantillonnage
- **Par commit de tâche :** commande rapide filtrée (< 15 s).
- **Par vague :** `dotnet test Chronos.sln`.
- **Porte de phase :** build 0 warning + suite complète verte avant `/gsd:verify-work`.

### Manques Wave 0
- [ ] `tests/Chronos.Tests/TestData/settings-valeurs-inconnues.json` — aucune entrée csproj nécessaire : les fixtures sont lues depuis le dossier SOURCE via `TestDataPath(file, [CallerFilePath])` (`SettingsServiceTests.cs:117`)
- [ ] `tests/Chronos.Tests/ArgumentsDemarrageTests.cs`
- [ ] Réécriture des deux gardes de placement de `GardesPerimetreTests.cs` + nouvelle garde « tri avant verrou »

## Sources

### Primaires (HAUTE)
- Prototype net8.0 exécuté localement le 2026-10-03 (SDK 10.0.201) : comportements `JsonStringEnumConverter` (casse, chaîne numérique, entier hors enum, null), `JsonNode.Parse` (tronqué, tableau, null, doublons), `GetTypeInfo` sans resolver, pré-passe complète.
- Code lu : `SettingsService.cs`, `ChronosSettings.cs`, `OverlayCorner.cs`, `App.xaml.cs`, `DiagnosticService.cs` (l. 113-175), `ChronosTheme.cs:164`, `SettingsServiceTests.cs`, `GardesPerimetreTests.cs` (l. 505-653), `ServicesLayerPurityTests.cs`, `Chronos.Tests.csproj`.
- `.zeus/reports/cycle2/inventaire-themes-historique.md` § PIÈGE ; `inventaire-purge-donnees.md` § A6, § C ; `llm-council-2026-10-03.md` § 6, § 8 ; `.planning/STATE.md` (barre statusLine retirée en 3.5).

### Secondaires (MOYENNE)
- Connaissance documentée de System.Text.Json (.NET 8) : `JsonSerializer.Deserialize(JsonNode, Type, JsonSerializerOptions)`, `JsonObject.Remove`, `DefaultJsonTypeInfoResolver` — confirmée par l'exécution du prototype.

## Métadonnées

**Confiance :**
- Pile : HAUTE — aucune dépendance nouvelle, API vérifiées par exécution.
- Architecture : HAUTE — prototype de la pré-passe fonctionnel ; motif d'aiguillage déjà présent.
- Pièges : HAUTE — pièges 1-5 observés au prototype, piège 6 lu dans les gardes existantes.

**Date :** 2026-10-03
**Valide jusqu'à :** 2026-11-02 (stable ; à revoir si la phase 37 modifie App.xaml.cs avant l'exécution de la 36)
