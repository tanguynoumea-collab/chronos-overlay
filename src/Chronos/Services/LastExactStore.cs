using System.Text.Json;
using System.Text.Json.Serialization;
using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// Magasin persistant du DERNIER RELEVÉ EXACT, par fenêtre (EXA-01), dans
/// %APPDATA%\Chronos\last-exact.json (chemin INJECTÉ, jamais construit en dur — voir
/// <see cref="ChronosPaths.LastExactFile"/>).
///
/// Raison d'être : les providers exacts ne gardent leur dernier chiffre qu'en RAM ; à chaque
/// démarrage de l'exe, aucun relevé exact n'existe et Chronos bascule en silence sur une source
/// dégradée. Persister ce relevé, avec son horodatage de capture PAR FENÊTRE, fournit l'instant T
/// de référence sans lequel la correction par delta est impossible.
///
/// Honnêteté par construction — ce qui n'est PAS persisté :
/// <list type="bullet">
///   <item>la fiabilité : DÉRIVÉE. Seules des fenêtres exactes sont écrites, donc la relecture
///     force <see cref="SourceReliability.Exact"/>. La lire du fichier permettrait à un fichier
///     corrompu ou édité à la main d'injecter une fausse exactitude.</item>
///   <item>la fraction de temps restante : RECALCULÉE au chargement à partir de resets_at et de
///     l'instant présent. La persister ressusciterait au démarrage une géométrie périmée,
///     c'est-à-dire un chiffre inventé.</item>
///   <item>les tokens estimés : une fenêtre exacte n'en porte jamais.</item>
///   <item>le genre de fenêtre : implicite dans la clé five_hour / seven_day.</item>
/// </list>
///
/// Écriture ATOMIQUE (temp + renommage) et lecture TOLÉRANTE (fichier absent, JSON invalide,
/// version inconnue → null, jamais d'exception qui remonte). Type NEUTRE : aucun type WPF.
///
/// Observabilité (phase 32, CPT-02) : le magasin dit QUAND il a écrit pour la dernière fois
/// (<see cref="DerniereEcriture"/> = mtime du fichier, le chiffre même qu'une sonde hors arbre lit) et
/// POURQUOI il n'a pas pu (<see cref="DerniereErreur"/>, <see cref="EcritureRatee"/>). Le projet a payé
/// trois fois la panne silencieuse — jeton expiré, usage.json figé, puis un faux « gel » de ce fichier
/// qui a fondé une phase : la copie copy-on-write du paquet MSIX, lue depuis une session, alors que le
/// fichier réel était réécrit chaque minute. L'ÉCRITURE ne se tait plus ; la LECTURE reste tolérante.
/// </summary>
public sealed class LastExactStore : IEtatMagasin, IOubliDernierReleve
{
    /// <summary>
    /// Version du schéma persisté. Une version inconnue est refusée EN BLOC plutôt que devinée :
    /// les phases suivantes y ajouteront le statut serveur et le dépassement, et un parsing
    /// approximatif d'un schéma futur produirait des chiffres faux.
    /// </summary>
    public const int SchemaVersion = 1;

    // Longueurs nominales des deux fenêtres — servent uniquement au recalcul de la géométrie.
    private static readonly TimeSpan LongueurCinqHeures = TimeSpan.FromHours(5);
    private static readonly TimeSpan LongueurHebdo = TimeSpan.FromDays(7);

    // Dates en ISO 8601 avec offset (jamais d'epoch) ; lecture insensible à la casse et tolérante.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;

    public LastExactStore(string path)
    {
        _path = path;

        // Amorçage best-effort (D-32-05) : si le fichier existe déjà, l'âge de sa dernière écriture est celui
        // du disque — un processus qui redémarre ne doit pas afficher « jamais écrit » devant un fichier d'hier.
        try
        {
            DerniereEcriture = System.IO.File.Exists(path)
                ? new DateTimeOffset(System.IO.File.GetLastWriteTimeUtc(path), TimeSpan.Zero)
                : null;
        }
        catch { DerniereEcriture = null; }
    }

    // DS2-03 — provisoire (RED) : aucun comportement.
    public void OublierDernierReleve(DateTimeOffset instant) { }
    public bool AnterieurAOubli(WindowState w) => false;

    /// <summary>Fichier effectivement piloté (injecté, donc isolable en test).</summary>
    public string Path => _path;

    // --- IEtatMagasin (CPT-02) ---------------------------------------------------------------------

    /// <inheritdoc/>
    public string Nom => NomsMagasins.DernierExact;

    /// <inheritdoc/>
    public string Chemin => _path;

    /// <summary>UTC. Posée sur le mtime après le renommage (D-32-05), amorcée du disque au démarrage ; null =
    /// jamais écrit, ni par ce processus, ni trouvé sur le disque.</summary>
    public DateTimeOffset? DerniereEcriture { get; private set; }

    /// <summary>« Type : message » de la dernière écriture ratée ; effacée au premier succès. Une erreur n'est
    /// pas une cicatrice, mais tant qu'elle dure, elle se lit au diagnostic.</summary>
    public string? DerniereErreur { get; private set; }

    /// <summary>Levé à chaque écriture ratée avec la cause (= <see cref="DerniereErreur"/>), AVANT de relancer.
    /// Le journal des relevés (32-05) l'abonne pour écrire un événement « ecriture_ratee ».</summary>
    public event EventHandler<string>? EcritureRatee;

    /// <summary>
    /// Persiste les fenêtres exactes du snapshot. Une fenêtre non exacte laisse la valeur
    /// précédemment persistée INTACTE : on n'efface jamais un chiffre connu avec une absence de
    /// donnée — c'est précisément l'absence de donnée que ce magasin sert à traverser.
    /// </summary>
    public void Save(UsageSnapshot snapshot)
    {
        // Fusion par fenêtre : relecture BRUTE (sans la garde de reset) — la fusion ne doit pas
        // purger silencieusement une fenêtre que Load refuserait seulement à cet instant précis.
        var existant = LireBrut();

        var five = Convertir(snapshot.FiveHour) ?? existant?.FiveHour;
        var seven = Convertir(snapshot.SevenDay) ?? existant?.SevenDay;

        // Rien d'exact à mémoriser et rien à préserver : ne pas créer un fichier vide.
        if (five is null && seven is null) return;

        var payload = new Payload { Version = SchemaVersion, FiveHour = five, SevenDay = seven };
        var json = JsonSerializer.Serialize(payload, Options);

        var dir = System.IO.Path.GetDirectoryName(_path)!;
        try
        {
            System.IO.Directory.CreateDirectory(dir);

            // Temp unique par process, sur le même volume que la cible → renommage atomique :
            // un last-exact.json partiel n'est jamais observable, même sur arrêt brutal.
            var tmp = _path + $".tmp-{Environment.ProcessId}";
            System.IO.File.WriteAllText(tmp, json);
            System.IO.File.Move(tmp, _path, overwrite: true);

            // D-32-05 : le mtime, et non une horloge injectée — c'est exactement ce que lit une sonde hors arbre,
            // et c'est le chiffre que le diagnostic comparera aux faits disque.
            DerniereEcriture = new DateTimeOffset(System.IO.File.GetLastWriteTimeUtc(_path), TimeSpan.Zero);
            DerniereErreur = null;
        }
        catch (Exception ex)
        {
            // CPT-02 — consigner PUIS relancer (D-32-06) : la tête (LastExactUsageProvider) garde son « je ne
            // sais pas » (EXA-05 : null, jamais false), mais la panne laisse une trace lisible au diagnostic et
            // un événement que le journal (32-05) écrira en « ecriture_ratee ». Plus rien ne se passe en silence.
            DerniereErreur = ex.GetType().Name + " : " + ex.Message;
            EcritureRatee?.Invoke(this, DerniereErreur);
            throw;
        }
    }

    /// <summary>
    /// Relit les fenêtres encore valides à l'instant <paramref name="now"/>. Renvoie null quand
    /// rien n'est exploitable : fichier absent, illisible, corrompu, version inconnue, ou toutes
    /// les fenêtres déjà remises à zéro.
    /// </summary>
    public LastExactWindows? Load(DateTimeOffset now)
    {
        var brut = LireBrut();
        if (brut is null) return null;

        var five = Reconstruire(brut.FiveHour, WindowKind.FiveHour, LongueurCinqHeures, now);
        var seven = Reconstruire(brut.SevenDay, WindowKind.SevenDay, LongueurHebdo, now);

        return five is null && seven is null ? null : new LastExactWindows(five, seven);
    }

    /// <summary>
    /// EXA-05 — un relevé exact a-t-il DÉJÀ été obtenu et mémorisé, au moins une fois ? Distinct de
    /// <see cref="Load"/>, qui rend null aussi bien pour « jamais rien vu » que pour « quelque chose a
    /// été vu, mais sa fenêtre a roulé depuis ». Les deux cas ne se traitent PAS pareil : le premier
    /// appelle une invitation à se connecter, le second non — l'utilisateur EST connecté, sa fenêtre a
    /// simplement tourné. Lui crier « connecte-toi » serait une fausse alerte.
    ///
    /// Aucune évolution de schéma : <see cref="SchemaVersion"/> reste 1 (trois fixtures épinglent
    /// "version":1 et "version":999). C'est une LECTURE de plus, pas un champ de plus.
    /// Tolérant par construction : s'appuie sur LireBrut, qui ne lève jamais.
    /// </summary>
    public bool UnExactADejaEteObtenu()
        => LireBrut() is { } p && (p.FiveHour is not null || p.SevenDay is not null);

    /// <summary>
    /// Lecture brute du fichier, sans aucune garde de validité temporelle. Tolérante : toute
    /// défaillance (absence, E/S, JSON invalide, date illisible, version inconnue) rend null.
    /// </summary>
    private Payload? LireBrut()
    {
        try
        {
            if (!System.IO.File.Exists(_path)) return null;

            var payload = JsonSerializer.Deserialize<Payload>(System.IO.File.ReadAllText(_path), Options);
            return payload is null || payload.Version != SchemaVersion ? null : payload;
        }
        catch
        {
            // Dégradation tolérante en LECTURE : un magasin illisible n'est pas une panne de Chronos, c'est
            // simplement l'absence d'un relevé de secours (« je ne sais pas » vaut mieux qu'un chiffre douteux).
            // L'ÉCRITURE, elle, ne se tait plus (DerniereErreur, EcritureRatee) : c'est là que vivait le défaut
            // d'observabilité que la phase 32 corrige.
            return null;
        }
    }

    /// <summary>
    /// Extrait l'entrée persistable d'une fenêtre. Trois conditions, pas deux : exacte, porteuse d'un
    /// pourcentage, d'un instant de reset ET d'un instant de CAPTURE. Le troisième est l'ajout de la
    /// phase 19 : un relevé sans instant de capture est définitivement incertifiable — ni son âge ni la
    /// question « de l'activité depuis ? » ne peuvent lui être posés. Le persister reviendrait à graver
    /// sur disque un chiffre que la doctrine devra rejeter à chaque lecture.
    /// </summary>
    private static Entry? Convertir(WindowState w)
        => w.Reliability == SourceReliability.Exact && w.Utilization is { } u
           && w.ResetsAt is { } r && w.CapturedAt is { } c
            ? new Entry { Utilization = u, ResetsAt = r, CapturedAt = c }
            : null;

    /// <summary>
    /// Reconstruit une fenêtre à partir de son entrée persistée. Renvoie null si l'entrée est
    /// absente, incomplète, ou si son reset est déjà passé — dans ce dernier cas la fenêtre a été
    /// remise à zéro depuis la capture et le pourcentage mémorisé ne décrit plus rien.
    /// </summary>
    private static WindowState? Reconstruire(Entry? e, WindowKind kind, TimeSpan longueur, DateTimeOffset now)
    {
        if (e?.ResetsAt is not { } resets || resets <= now) return null;

        return new WindowState
        {
            Kind = kind,
            Utilization = e.Utilization,
            ResetsAt = resets,
            CapturedAt = e.CapturedAt,
            FractionTimeRemaining = WindowState.FractionRemaining(resets, now, longueur),
            Reliability = SourceReliability.Exact,   // dérivé : seul de l'exact est écrit ici
            // EXA-06 — le nom du producteur voyage PAR RÉFÉRENCE à travers Best(), et la doctrine le
            // fait ensuite hériter par « candidat with » : un plancher reste donc attribué au magasin.
            Source = SourceUsage.MagasinDernierExact,
        };
    }

    /// <summary>Enveloppe persistée, versionnée.</summary>
    private sealed record Payload
    {
        [JsonPropertyName("version")] public int Version { get; init; }
        [JsonPropertyName("five_hour")] public Entry? FiveHour { get; init; }
        [JsonPropertyName("seven_day")] public Entry? SevenDay { get; init; }
    }

    /// <summary>Relevé exact d'UNE fenêtre. Toute clé supplémentaire du fichier est ignorée.</summary>
    private sealed record Entry
    {
        [JsonPropertyName("utilization")] public double Utilization { get; init; }
        [JsonPropertyName("resets_at")] public DateTimeOffset? ResetsAt { get; init; }
        [JsonPropertyName("captured_at")] public DateTimeOffset? CapturedAt { get; init; }
    }
}

/// <summary>Fenêtres exactes rechargées. Chaque membre est null si absent ou invalidé.</summary>
public sealed record LastExactWindows(WindowState? FiveHour, WindowState? SevenDay);
