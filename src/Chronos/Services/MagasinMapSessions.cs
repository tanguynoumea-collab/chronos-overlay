using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>Issue d'une lecture d'un magasin { session_id : horodatage(ms) }.</summary>
internal enum IssueLectureMap
{
    /// <summary>Fichier absent : ensemble vide, lecture FIABLE.</summary>
    Absent,
    /// <summary>Lu (les valeurs non numériques sont des entrées ignorées, pas un échec).</summary>
    Lu,
    /// <summary>Vide, syntaxe JSON cassée, racine non objet : le contenu ne se lira jamais — candidat à la quarantaine.</summary>
    Illisible,
    /// <summary>E/S persistante après reprises (fichier tenu, droits) : PASSAGER — ni écriture, ni quarantaine.</summary>
    Inaccessible,
}

internal readonly record struct LectureMap(IssueLectureMap Issue, Dictionary<string, long> Map, string? Cause)
{
    public bool EstFiable => Issue is IssueLectureMap.Absent or IssueLectureMap.Lu;
}

/// <summary>
/// MAT-3 / DATA-7 (phase 42.2) — mécanique commune des deux magasins du widget de sessions (<see cref="ArchiveStore"/>,
/// <see cref="TreatedStore"/>) : même format de fichier, même règle « aucune réécriture qui perd l'original ».
///
/// <list type="bullet">
///   <item>lecture TRI-ÉTAT : lisible / illisible / inaccessible (5 essais espacés de 10 ms sur E/S) ;</item>
///   <item>illisible → l'original est mis en quarantaine (<see cref="QuarantaineFichier"/>) AVANT toute réécriture ;
///   quarantaine impossible → rien n'est écrit, blocage signalé ;</item>
///   <item>inaccessible → rien n'est écrit, aucune quarantaine : le geste suivant réessaiera ;</item>
///   <item>écriture atomique (temporaire unique + Move, 5 essais) ; échec → temporaire retiré, erreur exposée ;</item>
///   <item>chaque erreur NOUVELLE est journalisée une fois dans chronos.log (<see cref="JournalIncidents"/>).</item>
/// </list>
/// L'appelant tient <see cref="Verrou"/> autour de lecture + écriture. Ne lève jamais. Aucun type WPF.
/// </summary>
internal sealed class MagasinMapSessions
{
    private const int EssaisLecture = 5;
    private const int EssaisMove = 5;

    private DateTimeOffset? _derniereEcriture;
    private string? _derniereSignalee;

    // FIAB-R5 (42.2-11) : le dernier ensemble lu (ou écrit) avec succès par ce processus — socle après une quarantaine.
    private Dictionary<string, long>? _dernierConnu;

    public MagasinMapSessions(string chemin, string nom)
    {
        Chemin = chemin;
        Nom = nom;
    }

    public object Verrou { get; } = new();
    public string Chemin { get; }
    public string Nom { get; }

    /// <summary>« Type : message » de la dernière écriture ratée (ou refusée) ; null après une écriture réussie.</summary>
    public string? DerniereErreur { get; private set; }

    /// <summary>Cause de la dernière LECTURE non aboutie ; ne s'efface que par <see cref="LectureReussie"/> (MAT-4).</summary>
    public string? DerniereErreurLecture { get; private set; }

    /// <summary>Chemin du dernier original illisible conservé par ce processus.</summary>
    public string? DerniereQuarantaine { get; private set; }

    /// <summary>UTC : dernière écriture de ce processus, sinon date du fichier sur le disque, sinon null.</summary>
    public DateTimeOffset? DerniereEcriture
    {
        get
        {
            if (_derniereEcriture is { } e) return e;
            try
            {
                return File.Exists(Chemin) ? new DateTimeOffset(File.GetLastWriteTimeUtc(Chemin), TimeSpan.Zero) : null;
            }
            catch (Exception) { return null; }
        }
    }

    private string? Dossier
    {
        get
        {
            try { return Path.GetDirectoryName(Chemin); }
            catch (Exception) { return null; }
        }
    }

    /// <summary>Lecture tri-état. N'écrit ni ne renomme RIEN. Une lecture non fiable pose <see cref="DerniereErreurLecture"/>
    /// (jamais effacée ici : seul un appel explicite à <see cref="LectureReussie"/> le fait).</summary>
    public LectureMap Lire()
    {
        var map = new Dictionary<string, long>();
        try
        {
            if (!File.Exists(Chemin)) return new(IssueLectureMap.Absent, map, null);

            string texte;
            for (var essai = 1; ; essai++)
            {
                try
                {
                    texte = File.ReadAllText(Chemin);
                    break;
                }
                catch (FileNotFoundException) { return new(IssueLectureMap.Absent, map, null); }
                catch (DirectoryNotFoundException) { return new(IssueLectureMap.Absent, map, null); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    if (essai >= EssaisLecture) return Echec(IssueLectureMap.Inaccessible, Decrire(ex));
                    Thread.Sleep(10);
                }
            }

            if (string.IsNullOrWhiteSpace(texte)) return Echec(IssueLectureMap.Illisible, "fichier vide");
            using var doc = JsonDocument.Parse(texte);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return Echec(IssueLectureMap.Illisible, "racine non objet");
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.Value.TryGetInt64(out var ts))   // valeur non numérique = entrée illisible, donc ignorée
                    map[p.Name] = ts;
            _dernierConnu = new Dictionary<string, long>(map);
            return new(IssueLectureMap.Lu, map, null);
        }
        catch (JsonException ex)
        {
            return Echec(IssueLectureMap.Illisible, Decrire(ex));
        }
        catch (Exception ex)
        {
            // Inattendu : prudence — traité comme passager (aucune quarantaine, aucune écriture).
            return Echec(IssueLectureMap.Inaccessible, Decrire(ex));
        }
    }

    /// <summary>Une lecture FIABLE vient d'aboutir : l'erreur de lecture est levée (MAT-4).</summary>
    public void LectureReussie() => DerniereErreurLecture = null;

    /// <summary>
    /// Décide si l'on peut écrire après la lecture <paramref name="lu"/>, et sur quelle base :
    /// fiable → la map lue ; illisible → quarantaine puis base = DERNIER ensemble lu ou écrit avec succès par ce processus
    /// (FIAB-R5 : les sessions déjà archivées ne réapparaissent pas), vide s'il n'y en a pas (l'original reste intact sous son nouveau nom) ;
    /// quarantaine impossible ou inaccessible → false, rien ne doit être écrit.
    /// </summary>
    public bool PreparerEcriture(LectureMap lu, out Dictionary<string, long> socle)
    {
        socle = lu.Map;
        switch (lu.Issue)
        {
            case IssueLectureMap.Absent:
            case IssueLectureMap.Lu:
                return true;
            case IssueLectureMap.Illisible:
                if (QuarantaineFichier.Mettre(Chemin, out var cible, out var cause))
                {
                    DerniereQuarantaine = cible;
                    JournalIncidents.Signaler(Dossier, Nom + " : " + Path.GetFileName(Chemin) + " illisible (" + lu.Cause
                                                       + ") mis en quarantaine sous " + Path.GetFileName(cible));
                    socle = _dernierConnu is null ? new Dictionary<string, long>() : new Dictionary<string, long>(_dernierConnu);
                    return true;
                }
                Refuser("quarantaine impossible — " + cause + " — " + Path.GetFileName(Chemin) + " n'est pas réécrit");
                return false;
            default:
                Refuser("écriture différée — fichier inaccessible : " + lu.Cause);
                return false;
        }
    }

    /// <summary>Écriture atomique (temporaire unique + Move avec reprises). Ne lève jamais.</summary>
    public bool Ecrire(Dictionary<string, long> map)
    {
        string? tmp = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Chemin)!);
            tmp = Chemin + ".tmp-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(tmp, JsonSerializer.Serialize(map));
            for (var essai = 1; ; essai++)
            {
                try
                {
                    File.Move(tmp, Chemin, overwrite: true);
                    break;
                }
                catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && essai < EssaisMove)
                {
                    Thread.Sleep(20);
                }
            }
            tmp = null;
            _dernierConnu = new Dictionary<string, long>(map);
            try { _derniereEcriture = new DateTimeOffset(File.GetLastWriteTimeUtc(Chemin), TimeSpan.Zero); }
            catch (Exception) { _derniereEcriture = DateTimeOffset.UtcNow; }
            DerniereErreur = null;
            return true;
        }
        catch (Exception ex)
        {
            if (tmp is not null)
            {
                try { File.Delete(tmp); } catch (Exception) { /* best-effort : le temporaire, jamais la cible */ }
            }
            Refuser("écriture : " + Decrire(ex));
            return false;
        }
    }

    private LectureMap Echec(IssueLectureMap issue, string cause)
    {
        var texte = (issue == IssueLectureMap.Illisible ? "illisible — " : "inaccessible — ") + cause;
        DerniereErreurLecture = texte;
        Signaler("lecture non aboutie — " + texte);
        return new(issue, new Dictionary<string, long>(), cause);
    }

    private void Refuser(string cause)
    {
        DerniereErreur = cause;
        Signaler(cause);
    }

    /// <summary>Une fois par cause consécutive : la même panne répétée à chaque cycle ne remplit pas chronos.log.</summary>
    private void Signaler(string message)
    {
        if (string.Equals(message, _derniereSignalee, StringComparison.Ordinal)) return;
        _derniereSignalee = message;
        JournalIncidents.Signaler(Dossier, Nom + " : " + message);
    }

    private static string Decrire(Exception ex) => ex.GetType().Name + " : " + ex.Message;
}
