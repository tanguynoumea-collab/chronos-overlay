using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Chronos.Services;
using Chronos.Services.Historique;

namespace Chronos.Services.Historique.Tokens;

/// <summary>Ce qu'il reste à AJOUTER aux agrégats pour un message re-rencontré ou nouveau : <see cref="Ts"/> est le PREMIER
/// timestamp vu de l'id (D-32-02, la tranche en découle), <see cref="Model"/> / <see cref="Sub"/> ceux de la première vue ;
/// les quatre compteurs sont « max − déjà compté » (tous égaux aux compteurs lus si <see cref="NouveauMessage"/>).</summary>
public sealed record DeltaMessage(DateTimeOffset Ts, string Model, bool Sub, long In, long Out, long CacheW, long CacheR, bool NouveauMessage);

/// <summary>
/// TOK-03 — la MÉMOIRE D'IDEMPOTENCE des agrégats (D-33-07).
///
/// POURQUOI un index d'ids et non une dédup par fichier : 1 377 ids vivent dans 2 à 4 fichiers du même projet (fork / resume),
/// copies indiscernables structurellement, âge max 3,6 j ; une dédup par fichier sur-compte de 2,2 % (mesuré le 2026-09-27).
/// Avec cet index, reprise après arrêt, fichier raccourci ou renommé, copie fork et bloc partiel qui grandit sont UN SEUL cas :
/// re-rencontrer un id applique max − déjà compté, rien s'il n'y a rien de neuf.
///
/// Shards <c>ids-AAAA-MM.jsonl</c> (mois UTC du PREMIER timestamp) en AJOUT SEUL — une ligne par delta non nul, ouverture
/// <c>OpenOrCreate</c> + <c>Seek(End)</c> sous verrou exclusif avec reprises (motif <c>JournalReleves.EcrireSousVerrou</c>,
/// jamais le mode d'ouverture « ajout ») ; à la relecture le max gagne (<see cref="DedupUsage.Fusionner"/>, la règle vit à un
/// seul endroit). En mémoire, seuls les mois qui chevauchent <c>[now − HorizonIndex, now]</c> (45 j ≥ 12 × l'âge max observé des
/// copies) ; au-delà de <see cref="RetentionIndexMois"/> le mois est GELÉ : une copie fork d'un message plus vieux serait
/// recomptée — jamais observé, limite écrite au §8 de la doc. Entiers seulement (garde TOK-05). Type NEUTRE, horloge injectée.
/// </summary>
public sealed class IndexMessages
{
    public const int SchemaVersion = 1;

    /// <summary>D-33-08 — mois chargés en mémoire = ceux qui chevauchent <c>[now − HorizonIndex, now]</c>.</summary>
    public static readonly TimeSpan HorizonIndex = TimeSpan.FromDays(45);

    /// <summary>D-33-08 — au-delà, les shards d'ids sont supprimés et le mois d'agrégats correspondant est gelé.</summary>
    public const int RetentionIndexMois = 3;

    /// <summary>Ordre des champs d'une ligne de shard — le schéma, cité par la doc et sa garde.</summary>
    public static readonly string[] Champs = { "v", "id", "ts", "model", "sub", "in", "out", "cache_w", "cache_r" };

    private const string ModeleAbsent = "<absent>";
    private const int EssaisMax = 600;   // motif JournalReleves : deux processus ne se font pas échouer
    private const int EssaisLecture = 20;  // motif LecteurJournal.LireFichier : un écrivain tient le shard < 1 ms

    private static readonly Regex NomShard = new(@"^ids-(\d{4})-(\d{2})\.jsonl$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly UTF8Encoding Utf8SansBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions OptionsEcriture = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // les ids et modèles sont de l'ASCII : pas d'échappement parasite
        WriteIndented = false,
    };

    private readonly record struct Entree(DateTimeOffset Ts, string Model, bool Sub, long In, long Out, long CacheW, long CacheR);

    private readonly Dictionary<string, Entree> _parId = new(StringComparer.Ordinal);
    private readonly List<(string Id, Entree E)> _aEcrire = new();
    private readonly HashSet<DateTimeOffset> _moisIllisibles = new();
    private readonly object _verrou = new();
    private readonly IClock _clock;

    public IndexMessages(string dossier, IClock clock)
    {
        Dossier = dossier;
        _clock = clock;
    }

    /// <summary>Dossier des shards (injecté : HistoriqueDir en production, dossier temporaire en test).</summary>
    public string Dossier { get; }

    /// <summary>Ids en mémoire (mois ouverts chargés + ajoutés depuis).</summary>
    public int IdsConnus { get { lock (_verrou) return _parId.Count; } }

    /// <summary>Lignes de shard illisibles au dernier <see cref="Charger"/> (tronquée, version inconnue, sans id, compteur non entier).</summary>
    public int LignesIgnorees { get; private set; }

    /// <summary>Deltas non encore écrits sur disque (vidés par <see cref="Flush"/>).</summary>
    public int LignesEnAttente { get { lock (_verrou) return _aEcrire.Count; } }

    /// <summary>« Type : message » de la dernière écriture ratée ; effacée au premier succès.</summary>
    public string? DerniereErreur { get; private set; }

    /// <summary>MAT-4 — « shard AAAA-MM : cause » de la dernière lecture non aboutie au <see cref="Charger"/> ; remise à null par
    /// un <see cref="Charger"/> où tous les shards ont été lus, jamais par une écriture.</summary>
    public string? DerniereErreurLecture { get; private set; }

    /// <summary>DATA-3 — mois ouverts dont le shard n'a pas pu être lu au dernier <see cref="Charger"/> (copie) : l'index est alors
    /// INCOMPLET pour ces mois, il ne doit ni être reprojeté ni servir à dédoublonner.</summary>
    public IReadOnlyCollection<DateTimeOffset> MoisIllisibles
    {
        get { lock (_verrou) return _moisIllisibles.OrderBy(m => m).ToList(); }
    }

    /// <summary>Nom du shard qui porte un message : mois UTC de son premier timestamp.</summary>
    public static string NomFichier(DateTimeOffset ts)
        => "ids-" + ts.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".jsonl";

    /// <summary>Reconnaît <c>ids-AAAA-MM.jsonl</c> et rend le 1er du mois UTC ; tout autre nom (agrégats, notes) est étranger.</summary>
    public static bool EstNomShard(string nomFichier, out DateTimeOffset mois)
    {
        mois = default;
        var m = NomShard.Match(nomFichier);
        if (!m.Success
            || !int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var annee)
            || !int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var mm)
            || mm is < 1 or > 12 || annee < 1)
            return false;
        mois = new DateTimeOffset(annee, mm, 1, 0, 0, 0, TimeSpan.Zero);
        return true;
    }

    /// <summary>1er du mois UTC, du plus ancien au plus récent, pour tous les mois qui chevauchent <c>[now − HorizonIndex, now]</c>.</summary>
    public IReadOnlyList<DateTimeOffset> MoisOuverts()
    {
        var now = _clock.UtcNow;
        var debut = TrancheMoisDe(now - HorizonIndex);
        var fin = TrancheMoisDe(now);
        var mois = new List<DateTimeOffset>();
        for (var m = debut; m <= fin; m = m.AddMonths(1)) mois.Add(m);
        return mois;
    }

    /// <summary>Relit les shards des mois ouverts (lecture tolérante ; id déjà vu → le max gagne, le timestamp le plus PETIT
    /// reste). Remplace l'état en mémoire. Rend le nombre d'ids connus. Ne lève jamais : un shard illisible (ouverture refusée
    /// après reprises, lecture interrompue, fichier non vide sans aucune ligne valide) entre dans <see cref="MoisIllisibles"/>
    /// et pose <see cref="DerniereErreurLecture"/> — c'est à l'appelant de ne pas s'en servir (DATA-3).</summary>
    public int Charger()
    {
        lock (_verrou)
        {
            _parId.Clear();
            _moisIllisibles.Clear();
            LignesIgnorees = 0;
            foreach (var mois in MoisOuverts())
            {
                var chemin = Path.Combine(Dossier, NomFichier(mois));
                if (!File.Exists(chemin)) continue;
                ChargerShard(chemin, mois);
            }
            if (_moisIllisibles.Count == 0) DerniereErreurLecture = null;
            return _parId.Count;
        }
    }

    /// <summary>Rend le delta à appliquer aux agrégats, ou null si rien de neuf. Id null → jamais indexé, delta = ses compteurs.</summary>
    public DeltaMessage? Ajouter(MessageLu m)
    {
        if (m.Id is null)
            return new DeltaMessage(m.Ts, m.Model, m.Sub, m.In, m.Out, m.CacheW, m.CacheR, NouveauMessage: true);   // D-33-11

        lock (_verrou)
        {
            if (_parId.TryGetValue(m.Id, out var connu))
            {
                var f = DedupUsage.Fusionner((connu.In, connu.Out, connu.CacheW, connu.CacheR), (m.In, m.Out, m.CacheW, m.CacheR));
                var delta = (In: f.In - connu.In, Out: f.Out - connu.Out, CacheW: f.CacheW - connu.CacheW, CacheR: f.CacheR - connu.CacheR);
                if (delta is (0, 0, 0, 0)) return null;   // copie fork / resume, relecture après arrêt, fichier raccourci : idempotent

                var maj = connu with { In = f.In, Out = f.Out, CacheW = f.CacheW, CacheR = f.CacheR };   // Ts, Model, Sub : ceux de la première vue
                _parId[m.Id] = maj;
                _aEcrire.Add((m.Id, maj));
                return new DeltaMessage(connu.Ts, connu.Model, connu.Sub, delta.In, delta.Out, delta.CacheW, delta.CacheR, NouveauMessage: false);
            }

            var e = new Entree(m.Ts, m.Model, m.Sub, m.In, m.Out, m.CacheW, m.CacheR);
            _parId[m.Id] = e;
            _aEcrire.Add((m.Id, e));
            return new DeltaMessage(m.Ts, m.Model, m.Sub, m.In, m.Out, m.CacheW, m.CacheR, NouveauMessage: true);
        }
    }

    /// <summary>Les entrées dont le premier timestamp tombe dans <paramref name="mois"/> (1er du mois UTC) — pour la projection (33-03).</summary>
    public IEnumerable<(DateTimeOffset Ts, string Model, bool Sub, long In, long Out, long CacheW, long CacheR)> Entrees(DateTimeOffset mois)
    {
        var cible = TrancheMoisDe(mois);
        lock (_verrou)
        {
            return _parId.Values
                .Where(e => TrancheMoisDe(e.Ts) == cible)
                .Select(e => (e.Ts, e.Model, e.Sub, e.In, e.Out, e.CacheW, e.CacheR))
                .ToList();
        }
    }

    /// <summary>Écrit les lignes en attente, un shard par mois, en ajout exclusif avec reprises. Rien en attente → true sans toucher
    /// le disque. Échec → false + <see cref="DerniereErreur"/>, les lignes RESTENT en attente (retentées au prochain flush).</summary>
    public bool Flush()
    {
        lock (_verrou)
        {
            if (_aEcrire.Count == 0) return true;

            // Le dossier se crée HORS de la boucle de reprises : un dossier qu'on ne peut pas créer n'est pas un verrou transitoire.
            try { Directory.CreateDirectory(Dossier); }
            catch (Exception ex) { DerniereErreur = Decrire(ex); return false; }

            var tout = true;
            foreach (var groupe in _aEcrire.GroupBy(x => NomFichier(x.E.Ts)).ToList())
            {
                var texte = new StringBuilder();
                foreach (var (id, e) in groupe) texte.Append(Serialiser(id, e)).Append('\n');

                if (EcrireSousVerrou(Path.Combine(Dossier, groupe.Key), Utf8SansBom.GetBytes(texte.ToString())))
                    _aEcrire.RemoveAll(x => NomFichier(x.E.Ts) == groupe.Key);
                else
                    tout = false;
            }
            if (tout) DerniereErreur = null;
            return tout;
        }
    }

    /// <summary>Supprime les shards <c>ids-*.jsonl</c> antérieurs à (mois courant − <see cref="RetentionIndexMois"/>) ; tout autre
    /// fichier du dossier (agrégats, notes) est ignoré et compté. Motif <c>JournalReleves.Purger</c>. Ne lève jamais.</summary>
    public BilanRetention Purger()
    {
        if (!Directory.Exists(Dossier)) return new BilanRetention(0, 0, 0);

        string[] fichiers;
        try { fichiers = Directory.GetFiles(Dossier); }
        catch { return new BilanRetention(0, 0, 0); }

        var maintenant = _clock.UtcNow;
        var limite = maintenant.Year * 12 + maintenant.Month - RetentionIndexMois;   // index de mois : STRICTEMENT avant → part

        int supprimes = 0, echecs = 0, ignores = 0;
        foreach (var fichier in fichiers)
        {
            if (!EstNomShard(Path.GetFileName(fichier), out var mois)) { ignores++; continue; }
            if (mois.Year * 12 + mois.Month >= limite) continue;   // dans la rétention : gardé, pas compté

            try { File.Delete(fichier); supprimes++; }
            catch { echecs++; }
        }
        return new BilanRetention(supprimes, echecs, ignores);
    }

    // --- Privé ---

    private static DateTimeOffset TrancheMoisDe(DateTimeOffset t)
    {
        var u = t.UtcDateTime;
        return new DateTimeOffset(u.Year, u.Month, 1, 0, 0, 0, TimeSpan.Zero);
    }

    // DTO privé dans l'ordre exact de Champs ; ts en « O » UTC (relu par le point unique HDR-05).
    private sealed class LigneIdDto
    {
        [JsonPropertyName("v")] public int V { get; set; }
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("ts")] public string Ts { get; set; } = "";
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("sub")] public bool Sub { get; set; }
        [JsonPropertyName("in")] public long In { get; set; }
        [JsonPropertyName("out")] public long Out { get; set; }
        [JsonPropertyName("cache_w")] public long CacheW { get; set; }
        [JsonPropertyName("cache_r")] public long CacheR { get; set; }
    }

    private static string Serialiser(string id, Entree e)
        => JsonSerializer.Serialize(new LigneIdDto
        {
            V = SchemaVersion,
            Id = id,
            Ts = e.Ts.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            Model = e.Model,
            Sub = e.Sub,
            In = e.In,
            Out = e.Out,
            CacheW = e.CacheW,
            CacheR = e.CacheR,
        }, OptionsEcriture);

    // Lecture tolérante d'un shard : v lu en premier, id chaîne non vide, ts par le point unique HDR-05, compteurs entiers
    // (sinon ligne ignorée et comptée), sub absent → false. Partage large : un autre processus peut être en train d'écrire.
    // DATA-3 (phase 42.2) — TRI-ÉTAT : absent ou 0 octet → vide légitime (OpenOrCreate puis écriture : un arrêt entre les deux
    // laisse un shard qui n'a jamais rien porté) ; ouverture refusée après reprises, lecture interrompue, ou fichier non vide
    // sans AUCUNE ligne valide → illisible (false), jamais « vide ». Les ids lus avant une interruption restent en mémoire,
    // mais le mois est marqué : l'appelant ne doit pas s'en servir.
    private bool ChargerShard(string chemin, DateTimeOffset mois)
    {
        try
        {
            using var fs = OuvrirEnLecture(chemin);
            if (fs is null) return true;   // disparu entre File.Exists et l'ouverture : absent

            var valides = 0;
            using var lecteur = new StreamReader(fs, Utf8SansBom);
            string? ligne;
            while ((ligne = lecteur.ReadLine()) is not null)
            {
                if (ligne.Length == 0) continue;
                if (!ParserLigne(ligne, out var id, out var e)) { LignesIgnorees++; continue; }
                valides++;

                if (_parId.TryGetValue(id, out var connu))
                {
                    var f = DedupUsage.Fusionner((connu.In, connu.Out, connu.CacheW, connu.CacheR), (e.In, e.Out, e.CacheW, e.CacheR));
                    _parId[id] = connu with { Ts = connu.Ts <= e.Ts ? connu.Ts : e.Ts, In = f.In, Out = f.Out, CacheW = f.CacheW, CacheR = f.CacheR };
                }
                else
                {
                    _parId[id] = e;
                }
            }

            if (valides == 0 && fs.Length > 0)
                return Illisible(mois, "fichier non vide sans aucune ligne valide (" + fs.Length.ToString(CultureInfo.InvariantCulture) + " octets)");
            return true;
        }
        catch (IOException ex) { return Illisible(mois, Decrire(ex)); }
        catch (UnauthorizedAccessException ex) { return Illisible(mois, Decrire(ex)); }
    }

    // null = disparu entre File.Exists et l'ouverture. IOException au-delà des reprises → levée vers ChargerShard.
    private static FileStream? OuvrirEnLecture(string chemin)
    {
        for (var essai = 1; ; essai++)
        {
            try
            {
                return new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (FileNotFoundException) { return null; }
            catch (IOException) when (essai < EssaisLecture)
            {
                if (essai <= 12) Thread.Yield(); else Thread.Sleep(1);
            }
        }
    }

    private bool Illisible(DateTimeOffset mois, string cause)
    {
        _moisIllisibles.Add(mois);
        DerniereErreurLecture = "shard " + mois.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture) + " : " + cause;
        return false;
    }

    private static bool ParserLigne(string ligne, out string id, out Entree e)
    {
        id = "";
        e = default;
        try
        {
            using var doc = JsonDocument.Parse(ligne);
            var o = doc.RootElement;
            if (o.ValueKind != JsonValueKind.Object) return false;
            if (!o.TryGetProperty("v", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var version) || version != SchemaVersion)
                return false;
            if (!o.TryGetProperty("id", out var pid) || pid.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(pid.GetString()))
                return false;
            id = pid.GetString()!;

            var ts = o.TryGetProperty("ts", out var pts) && pts.ValueKind == JsonValueKind.String
                ? UsageNormalization.InstantDepuisIso(pts.GetString())
                : null;
            if (ts is not { } instant) return false;

            if (!Entier(o, "in", out var entree) || !Entier(o, "out", out var sortie)
                || !Entier(o, "cache_w", out var cacheEcrit) || !Entier(o, "cache_r", out var cacheLu))
                return false;

            var model = o.TryGetProperty("model", out var pm) && pm.ValueKind == JsonValueKind.String ? pm.GetString() ?? ModeleAbsent : ModeleAbsent;
            var sub = o.TryGetProperty("sub", out var ps) && ps.ValueKind == JsonValueKind.True;

            e = new Entree(instant, model, sub, entree, sortie, cacheEcrit, cacheLu);
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static bool Entier(JsonElement o, string nom, out long valeur)
    {
        valeur = 0;
        return o.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out valeur);
    }

    // Ajout exclusif avec reprises : OpenOrCreate + Seek(End) + une seule écriture ; l'autre écrivain tient le fichier → céder la main.
    private bool EcrireSousVerrou(string chemin, byte[] octets)
    {
        for (var essai = 1; ; essai++)
        {
            try
            {
                using var fs = new FileStream(chemin, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                fs.Seek(0, SeekOrigin.End);
                fs.Write(octets, 0, octets.Length);
                fs.Flush(flushToDisk: false);
                return true;
            }
            catch (IOException) when (essai < EssaisMax)
            {
                if (essai <= 12) Thread.Yield(); else Thread.Sleep(1);
            }
            catch (Exception ex)
            {
                DerniereErreur = Decrire(ex);
                return false;
            }
        }
    }

    private static string Decrire(Exception ex) => ex.GetType().Name + " : " + ex.Message;
}
