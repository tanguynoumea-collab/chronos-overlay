using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Chronos.Services;

/// <summary>
/// Persistance de <see cref="ChronosSettings"/> dans %APPDATA%\Chronos\settings.json (FEN-07).
///
/// Lecture TOLÉRANTE VALEUR PAR VALEUR (SOC-01, phase 36) : une valeur inconnue (enum disparu ou futur, entier hors
/// enum) ou mal typée retombe sur le défaut de SA propriété (l'initialiseur du record), le reste est conservé. Un contenu
/// réellement inexploitable (syntaxe, racine non objet, fichier vide) donne les défauts ENTIERS
/// (<see cref="IssueLectureReglages.Illisible"/>), une E/S passagère après reprises bornées aussi
/// (<see cref="IssueLectureReglages.Inaccessible"/>) : jamais de réglages « à moitié lus », jamais d'exception (ROB-02).
/// La lecture n'écrit ni ne renomme RIEN : l'utilisateur peut encore réparer le fichier à la main.
///
/// RÈGLE D'ÉCRITURE (42.2-02, MAT-3) — aucune réécriture ne perd l'original :
/// <list type="bullet">
///   <item>illisible → l'original est mis en QUARANTAINE (renommé en <c>settings.illisible-AAAAMMJJ-HHMMSS.json</c> dans le même
///   dossier, jamais supprimé, incident journalisé, visible au diagnostic) puis l'écriture a lieu ; l'écriture porte le marqueur
///   <see cref="ChronosSettings.QuarantaineReglagesDepuis"/> ;</item>
///   <item>inaccessible (verrou, droits) → aucune écriture, aucune quarantaine, réessai au passage suivant ;</item>
///   <item>quarantaine impossible → aucune écriture, blocage signalé.</item>
/// </list>
/// Raison (DATA-5) : chaque site faisait <c>Save(Load() with …)</c> et <c>Load()</c> rendait les défauts sur toute erreur — une
/// virgule de trop, ou un verrou d'antivirus, effaçait toute la configuration sans trace, avant même que le diagnostic le voie.
/// Le lire-modifier-écrire passe donc par <see cref="Modifier"/>, sous verrou, et <see cref="Save"/> ne LÈVE JAMAIS (FIAB-2 :
/// appelé depuis des chemins UI chauds). Un contenu identique au disque n'est pas réécrit.
///
/// <see cref="SettingsService"/> ne normalise PAS <see cref="ChronosSettings.ThemeKey"/> : le catalogue des thèmes est WPF ;
/// une clé inconnue est conservée brute et <c>ThemeCatalog.ByKey</c> la fait retomber sur « minuit » à l'affichage.
///
/// Écriture ATOMIQUE : on écrit un fichier temp UNIQUE puis on le renomme par-dessus la cible
/// (<see cref="File.Move(string, string, bool)"/> sur le même volume, reprises bornées), de sorte qu'un settings.json partiel ne
/// soit jamais observable en cas d'arrêt brutal ; le temporaire est nettoyé en cas d'échec (DATA-11).
///
/// Type NEUTRE (aucun type WPF en signature) : la garde de pureté Services/Models reste verte.
/// </summary>
public sealed class SettingsService
{
    private readonly ChronosPaths _paths;
    private readonly object _verrou = new();

    public SettingsService(ChronosPaths paths) => _paths = paths;

    // SOC-01 — rapport de la dernière lecture (issue + valeurs retombées), lu par le diagnostic.
    private volatile LectureReglages _derniereLecture = LectureReglages.Aucune;
    private int _lecturesNonAbouties;
    private int _ecrituresRefusees;
    private int _quarantaines;

    /// <summary>Issue de la dernière lecture de settings.json (SOC-01).</summary>
    public LectureReglages DerniereLecture => _derniereLecture;

    /// <summary>Noms (tels qu'écrits dans le JSON) des valeurs retombées sur leur défaut à la dernière lecture.</summary>
    public IReadOnlyList<string> DernieresRetombees => _derniereLecture.Retombees;

    /// <summary>Issue de la lecture de DÉMARRAGE (<see cref="ChargerPourDemarrage"/>) ; null si elle n'a pas eu lieu (MAT-4 a).</summary>
    public LectureReglages? LectureDuDemarrage { get; private set; }

    /// <summary>Nombre de lectures non abouties (illisible ou inaccessible) depuis la construction du service.</summary>
    public int LecturesNonAbouties => Volatile.Read(ref _lecturesNonAbouties);

    /// <summary>Dernière lecture non aboutie (issue + cause) ; null si aucune.</summary>
    public LectureReglages? DerniereLectureNonAboutie { get; private set; }

    /// <summary>Nombre d'écritures refusées (lecture inaccessible, quarantaine impossible, échec d'écriture).</summary>
    public int EcrituresRefusees => Volatile.Read(ref _ecrituresRefusees);

    /// <summary>Cause du dernier refus d'écriture ; remise à null par la prochaine écriture réussie.</summary>
    public string? DerniereEcritureRefusee { get; private set; }

    /// <summary>Nombre de quarantaines réussies depuis la construction du service.</summary>
    public int Quarantaines => Volatile.Read(ref _quarantaines);

    /// <summary>Chemin complet du dernier fichier illisible conservé en quarantaine ; null si aucun.</summary>
    public string? DerniereQuarantaine { get; private set; }

    private string Dossier => Path.GetDirectoryName(_paths.SettingsFile)!;

    // OverlayCorner sérialisé en texte (lisible/robuste au réordonnancement de l'enum) ;
    // lecture insensible à la casse et tolérante aux virgules trainantes.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        // Résolveur EXPLICITE : en .NET 8, Options.GetTypeInfo(...) lève tant qu'aucun TypeInfoResolver n'est posé
        // (les options ne sont pas encore « verrouillées » par une première sérialisation). Il sert à énumérer les
        // propriétés telles que le sérialiseur les voit, pour la validation valeur par valeur de Load().
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    // Mêmes tolérances syntaxiques que la lecture finale. NE PAS rendre le JsonObject insensible à la casse (piège 5).
    private static readonly JsonDocumentOptions OptionsDocument = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private sealed record ProprieteReglage(Type Type, bool NullAccepte);

    // Propriétés telles que le SÉRIALISEUR les voit (source de vérité unique : ChronosSettings), calculées une fois.
    private static readonly Lazy<Dictionary<string, ProprieteReglage>> Proprietes = new(() =>
    {
        var contexte = new NullabilityInfoContext();
        return Options.GetTypeInfo(typeof(ChronosSettings)).Properties.ToDictionary(
            p => p.Name,
            p => new ProprieteReglage(p.PropertyType, NullAccepte(p, contexte)),
            StringComparer.OrdinalIgnoreCase);
    });

    private static bool NullAccepte(JsonPropertyInfo p, NullabilityInfoContext contexte)
    {
        if (p.PropertyType.IsValueType) return Nullable.GetUnderlyingType(p.PropertyType) is not null;
        // Référence : null refusé si la propriété est déclarée non-nullable (ThemeKey), accepté sinon (string?).
        return p.AttributeProvider is not PropertyInfo pi || contexte.Create(pi).ReadState != NullabilityState.NotNull;
    }

    private const int EssaisLecture = 5;
    private const int EssaisMove = 5;
    private const int EssaisNomQuarantaine = 100;

    /// <summary>
    /// Relit settings.json, tolérant VALEUR PAR VALEUR (SOC-01) : une valeur inconnue ou mal typée prend le défaut de
    /// CETTE propriété, le reste est conservé (<see cref="IssueLectureReglages.LuAvecRetombees"/>). Fichier absent →
    /// défauts (<see cref="IssueLectureReglages.Absent"/>) ; contenu vide, JSON illisible ou racine non objet → défauts ENTIERS
    /// (<see cref="IssueLectureReglages.Illisible"/>) ; E/S persistante après reprises → défauts
    /// (<see cref="IssueLectureReglages.Inaccessible"/>). Jamais d'exception, aucune écriture ni aucun renommage disque ;
    /// <see cref="DerniereLecture"/> est renseignée à chaque appel.
    /// </summary>
    public ChronosSettings Load()
    {
        lock (_verrou)
        {
            try
            {
                if (!File.Exists(_paths.SettingsFile))
                    return Rendre(new(IssueLectureReglages.Absent, Array.Empty<string>()));

                // E/S : reprises bornées (verrou d'antivirus, écriture concurrente) ; au-delà → Inaccessible, jamais Illisible.
                string texte;
                for (var essai = 1; ; essai++)
                {
                    try
                    {
                        texte = File.ReadAllText(_paths.SettingsFile);
                        break;
                    }
                    catch (FileNotFoundException) { return Rendre(new(IssueLectureReglages.Absent, Array.Empty<string>())); }
                    catch (DirectoryNotFoundException) { return Rendre(new(IssueLectureReglages.Absent, Array.Empty<string>())); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        if (essai >= EssaisLecture)
                            return Rendre(new(IssueLectureReglages.Inaccessible, Array.Empty<string>(), Decrire(ex)));
                        Thread.Sleep(10);
                    }
                }

                if (string.IsNullOrWhiteSpace(texte))
                    return Rendre(new(IssueLectureReglages.Illisible, Array.Empty<string>(), "fichier vide"));

                // 1) Document ENTIER parsé d'abord : une erreur de syntaxe n'importe où lève ICI, avant qu'une seule
                //    valeur ne soit exploitée → jamais de réglages « à moitié lus » (SOC-01, critère 2).
                if (JsonNode.Parse(texte, nodeOptions: null, documentOptions: OptionsDocument) is not JsonObject racine)
                    return Rendre(new(IssueLectureReglages.Illisible, Array.Empty<string>(), "racine non objet"));   // null, tableau, scalaire

                // 2) Valeur par valeur, avec les MÊMES options que la lecture finale ; les fautives sont retirées.
                var retombees = new List<string>();
                foreach (var (nom, valeur) in racine.ToList())   // copie : on retire pendant le parcours
                {
                    if (!Proprietes.Value.TryGetValue(nom, out var p)) continue;   // membre inconnu : STJ l'ignore déjà
                    if (!ValeurAcceptable(valeur, p)) { racine.Remove(nom); retombees.Add(nom); }
                }

                // 3) Lecture finale : chaque membre retiré prend l'initialiseur de SA propriété dans le record.
                var lu = racine.Deserialize<ChronosSettings>(Options) ?? new ChronosSettings();
                _derniereLecture = new(retombees.Count == 0 ? IssueLectureReglages.Lu : IssueLectureReglages.LuAvecRetombees, retombees);
                return lu;
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or NotSupportedException or InvalidOperationException)
            {
                // Contenu inexploitable (syntaxe, clés strictement dupliquées) → défauts ENTIERS (ROB-02 conservé).
                return Rendre(new(IssueLectureReglages.Illisible, Array.Empty<string>(), Decrire(ex)));
            }
            catch (Exception ex)
            {
                // Inattendu : prudence — traité comme passager (aucune quarantaine, aucune écriture).
                return Rendre(new(IssueLectureReglages.Inaccessible, Array.Empty<string>(), Decrire(ex)));
            }
        }
    }

    /// <summary>Pose la lecture (comptée si non aboutie) et rend les défauts.</summary>
    private ChronosSettings Rendre(LectureReglages lecture)
    {
        _derniereLecture = lecture;
        if (!lecture.EstFiable)
        {
            Interlocked.Increment(ref _lecturesNonAbouties);
            DerniereLectureNonAboutie = lecture;
        }
        return new ChronosSettings();
    }

    /// <summary>LA lecture de démarrage (MAT-4 a) : <see cref="Load"/>, puis son issue est retenue dans
    /// <see cref="LectureDuDemarrage"/> pour le diagnostic, même si une relecture ultérieure dit autre chose.</summary>
    public ChronosSettings ChargerPourDemarrage()
    {
        lock (_verrou)
        {
            var s = Load();
            LectureDuDemarrage = DerniereLecture;
            return s;
        }
    }

    /// <summary>
    /// Lire-modifier-écrire SOUS VERROU (MAT-3) : relit le disque, applique <paramref name="mutation"/>, et n'écrit que si la
    /// lecture du même passage est fiable (absent, lu) ou illisible ET mise en quarantaine avec succès. Après une quarantaine,
    /// la mutation reçoit des défauts PORTANT le marqueur <see cref="ChronosSettings.QuarantaineReglagesDepuis"/> : elle le
    /// conserve si elle n'y touche pas, l'efface si elle le remet explicitement à null. Rend la valeur produite par la mutation
    /// (écrite ou non). Ne lève jamais pour une cause disque.
    /// </summary>
    public ChronosSettings Modifier(Func<ChronosSettings, ChronosSettings> mutation)
    {
        lock (_verrou)
        {
            var lu = Load();
            var prete = PreparerEcriture(out var quarantaine);
            if (quarantaine) lu = AvecMarqueur(lu);
            var nouveau = mutation(lu);
            if (prete) Ecrire(nouveau);
            return nouveau;
        }
    }

    /// <summary>
    /// Écrit <paramref name="settings"/> si l'état courant du disque le permet (même règle que <see cref="Modifier"/>) ; rend
    /// true si le fichier reflète <paramref name="settings"/> après l'appel. NE LÈVE JAMAIS (FIAB-2).
    ///
    /// <para><b>Réservé aux tests</b> (amorçage d'un fichier entier) — d'où <c>internal</c>, visible de Chronos.Tests par
    /// InternalsVisibleTo. La production écrit UNIQUEMENT par <see cref="Modifier"/> (fusion sous verrou : relecture, mutation,
    /// écriture dans le même passage) ; un Save de production écraserait une modification concurrente. Garde :
    /// <c>GardesReliquatsTests</c> (phase 42.3, DS-MAINT-04).</para>
    /// </summary>
    internal bool Save(ChronosSettings settings)
    {
        lock (_verrou)
        {
            try
            {
                Load();   // sonde de l'état courant du fichier
                if (!PreparerEcriture(out var quarantaine)) return false;
                return Ecrire(quarantaine ? AvecMarqueur(settings) : settings);
            }
            catch (Exception ex)
            {
                Refuser("écriture : " + Decrire(ex));
                return false;
            }
        }
    }

    private static ChronosSettings AvecMarqueur(ChronosSettings s) =>
        s with { QuarantaineReglagesDepuis = s.QuarantaineReglagesDepuis ?? DateTimeOffset.UtcNow };

    /// <summary>Décide si l'écriture peut avoir lieu, d'après la lecture du MÊME passage (à appeler sous verrou, après Load).</summary>
    private bool PreparerEcriture(out bool quarantaine)
    {
        quarantaine = false;
        var lecture = _derniereLecture;
        if (lecture.EstFiable) return true;
        if (lecture.Issue == IssueLectureReglages.Inaccessible)
        {
            Refuser("lecture inaccessible (E/S) — " + lecture.Cause + " — réessai au prochain enregistrement");
            return false;
        }
        quarantaine = MettreEnQuarantaine();
        return quarantaine;
    }

    /// <summary>Renomme le settings.json illisible en <c>settings.illisible-AAAAMMJJ-HHMMSS[-n].json</c> (même dossier) :
    /// RENOMMAGE, jamais de suppression, jamais d'écrasement d'une quarantaine existante.</summary>
    private bool MettreEnQuarantaine()
    {
        var cause = _derniereLecture.Cause ?? "contenu inexploitable";
        try
        {
            var dossier = Dossier;
            var baseNom = "settings.illisible-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            for (var n = 0; n <= EssaisNomQuarantaine; n++)
            {
                var cible = Path.Combine(dossier, baseNom + (n == 0 ? "" : "-" + n) + ".json");
                if (File.Exists(cible)) continue;
                try
                {
                    File.Move(_paths.SettingsFile, cible, overwrite: false);
                }
                catch (IOException) when (File.Exists(cible) && File.Exists(_paths.SettingsFile))
                {
                    continue;   // nom pris entre-temps : suffixe suivant
                }
                Interlocked.Increment(ref _quarantaines);
                DerniereQuarantaine = cible;
                JournalIncidents.Signaler(dossier, "settings.json illisible (" + cause + ") mis en quarantaine sous "
                                                   + Path.GetFileName(cible) + " — réglages repartis des défauts");
                return true;
            }
            Refuser("quarantaine impossible — aucun nom libre pour " + baseNom + " — settings.json n'est pas réécrit");
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Refuser("quarantaine impossible — " + Decrire(ex) + " — settings.json n'est pas réécrit");
            return false;
        }
    }

    /// <summary>Vrai si <paramref name="valeur"/> se lit dans le type de la propriété avec les options réelles, et qu'un
    /// enum lu est une valeur DÉFINIE (STJ accepte un entier hors enum).</summary>
    private static bool ValeurAcceptable(JsonNode? valeur, ProprieteReglage p)
    {
        if (valeur is null) return p.NullAccepte;   // null sur bool/double/enum/ThemeKey → retombée (piège 4)
        try
        {
            var lu = valeur.Deserialize(p.Type, Options);
            var t = Nullable.GetUnderlyingType(p.Type) ?? p.Type;
            // STJ accepte un entier hors enum (« Corner »: 99) : on le refuse (piège 3). Aucun enum des réglages n'est [Flags].
            return !(t.IsEnum && lu is not null && !Enum.IsDefined(t, lu));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or ArgumentException or FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Écrit settings.json de façon atomique (temp unique + Move avec reprises) ; contenu identique au disque → rien n'est écrit.
    /// Échec → temp supprimé (best-effort), refus journalisé, false. Ne lève jamais.
    /// </summary>
    private bool Ecrire(ChronosSettings settings)
    {
        string? tmp = null;
        try
        {
            var json = JsonSerializer.Serialize(settings, Options);

            if (File.Exists(_paths.SettingsFile))
            {
                string? actuel = null;
                try { actuel = File.ReadAllText(_paths.SettingsFile); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* on tente l'écriture */ }
                if (actuel == json) { DerniereEcritureRefusee = null; return true; }
            }

            Directory.CreateDirectory(Dossier);
            // Temp UNIQUE par appel, sur le même volume que la cible → File.Move atomique.
            tmp = _paths.SettingsFile + ".tmp-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(tmp, json);
            for (var essai = 1; ; essai++)
            {
                try
                {
                    File.Move(tmp, _paths.SettingsFile, overwrite: true);
                    break;
                }
                catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && essai < EssaisMove)
                {
                    Thread.Sleep(20);
                }
            }
            tmp = null;
            DerniereEcritureRefusee = null;
            return true;
        }
        catch (Exception ex)
        {
            if (tmp is not null)
            {
                try { File.Delete(tmp); } catch { /* best-effort */ }
            }
            Refuser("écriture : " + Decrire(ex));
            return false;
        }
    }

    /// <summary>Compte le refus ; ne journalise qu'une fois par cause consécutive (pas de spam de chronos.log).</summary>
    private void Refuser(string cause)
    {
        Interlocked.Increment(ref _ecrituresRefusees);
        if (!string.Equals(cause, DerniereEcritureRefusee, StringComparison.Ordinal))
        {
            string? dossier = null;
            try { dossier = Dossier; } catch { /* chemin invalide : rien à journaliser */ }
            JournalIncidents.Signaler(dossier, "réglages non enregistrés : " + cause);
        }
        DerniereEcritureRefusee = cause;
    }

    private static string Decrire(Exception ex) => ex.GetType().Name + " : " + ex.Message;
}
