using System.Collections.Generic;
using System.IO;

namespace Chronos.Services;

/// <summary>
/// Magasin AUTO-GÉRÉ et RÉVERSIBLE des sessions « traitées » (répondues ou acquittées par un geste) dans
/// %APPDATA%\Chronos\treated.json — une map { session_id : treatedWaitingTs(ms) }, où l'horodatage est
/// celui de l'ÉPISODE d'attente qui a été traité. Le <see cref="SessionTreatmentTracker"/> l'alimente
/// (ajout NET-01) et le purge (réapparition NET-03) ; le <see cref="SessionMonitor"/> masque les
/// sessions encore présentes.
///
/// Les deux magasins jumeaux se distinguent désormais PAR LEUR CODE autant que par leur sémantique : ils
/// ne partagent plus que la mécanique de fichier (<see cref="MagasinMapSessions"/> : lecture tri-état, quarantaine,
/// écriture atomique) et les chemins %APPDATA%. Depuis TRT-04, aucune durée de vie n'est commune :
///   • archivé (<see cref="ArchiveStore"/>) = PERMANENT. Ce magasin-là ne borne plus RIEN et ne
///     réapparaît jamais (NET-04) ; son seul retrait est un geste, jamais une horloge ;
///   • traité (ce magasin) = RÉVERSIBLE via <see cref="Remove"/> (NET-03) : il dure tant que la session
///     ne redemande rien — et c'est la SEULE chose qui le défait.
/// La rétention du fichier décrite ci-dessous n'est pas un délai d'affichage mais une borne de
/// croissance : choisie supérieure au seuil au-delà duquel le moniteur cesse de lire une session, elle
/// ne peut pas, PAR CONSTRUCTION, faire réapparaître une session encore lisible. C'est cela qui rend le
/// libellé du menu exact.
///
/// L'horloge est INJECTÉE, jamais lue au système : un filtre de durée adossé à l'heure de la machine
/// rend ses tests verts le jour où on les écrit et rouges six heures plus tard, sans qu'une ligne de
/// code ait bougé. Trois plans de la phase 22 sont tombés dans ce piège ; il ne se reproduit pas ici.
///
/// <para><see cref="HorizonsSessions.RetentionTraitees"/> est une RÉTENTION DE FICHIER, jamais un délai d'affichage. Depuis
/// TRT-02, la valeur mémorisée est l'instant que le SIGNAL porte, pas celui du guetteur : une borne de
/// six heures adossée à cet instant rendait le magasin AVEUGLE à toute session attendant depuis plus de
/// six heures — c'est-à-dire précisément celle dont ce milestone est parti, et deux heures pleines
/// pendant lesquelles « marquer traitée » était un no-op silencieux. La borne doit donc être
/// strictement supérieure au seuil au-delà duquel le moniteur cesse de lire une session (huit heures,
/// <see cref="HorizonsSessions.Abandon"/>), puisqu'au-delà la session n'est de toute façon plus affichée.
/// Les deux vivent dans <see cref="HorizonsSessions"/>, où une garde tient la chaîne. La réversibilité, elle, reste portée
/// par NET-03 — jamais par une horloge.</para>
///
/// <para>MAT-3 / DATA-7 (phase 42.2) — même règle que <see cref="ArchiveStore"/> (mécanique commune
/// <see cref="MagasinMapSessions"/>) : illisible → original mis en QUARANTAINE (renommé, jamais supprimé) puis écriture ;
/// quarantaine impossible ou E/S passagère → rien n'est écrit ; une lecture non aboutie rend le dernier ensemble lu.</para>
///
/// Ne lève jamais. Aucun type WPF (couche neutre).
/// </summary>
public sealed class TreatedStore : IEtatMagasin
{
    private readonly MagasinMapSessions _magasin;
    private readonly IClock _horloge;
    private Dictionary<string, long> _dernierLu = new();

    public TreatedStore(string? path = null, IClock? clock = null)
    {
        _magasin = new MagasinMapSessions(path ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Chronos", "treated.json"),
            NomsMagasins.SessionsTraitees);
        _horloge = clock ?? new SystemClock();
    }

    public string Nom => _magasin.Nom;
    public string Chemin => _magasin.Chemin;
    public System.DateTimeOffset? DerniereEcriture => _magasin.DerniereEcriture;
    public string? DerniereErreur => _magasin.DerniereErreur;
    public string? DerniereErreurLecture => _magasin.DerniereErreurLecture;
    public string? DerniereQuarantaine => _magasin.DerniereQuarantaine;

    /// <summary>Map { session_id : treatedWaitingTs(ms) } des entrées encore retenues dans le fichier. Lecture non aboutie →
    /// le dernier ensemble lu avec succès (toujours filtré par la rétention). N'écrit ni ne renomme JAMAIS rien.</summary>
    public IReadOnlyDictionary<string, long> Load()
    {
        lock (_magasin.Verrou)
        {
            var lu = _magasin.Lire();
            if (lu.EstFiable)
            {
                _magasin.LectureReussie();
                _dernierLu = lu.Map;
            }
            return Retenues(_dernierLu);
        }
    }

    /// <summary>
    /// Marque une session comme traitée pour l'épisode d'attente <paramref name="treatedWaitingTs"/>
    /// (idempotent) et laisse tomber les entrées hors rétention. Écriture atomique. Rend true si c'est écrit ; false si
    /// rien n'a pu l'être (cause dans <see cref="DerniereErreur"/>).
    /// </summary>
    public bool Set(string sessionId, long treatedWaitingTs)
    {
        if (string.IsNullOrEmpty(sessionId)) return false;
        lock (_magasin.Verrou)
        {
            var lu = _magasin.Lire();
            if (!_magasin.PreparerEcriture(lu, out var socle)) return false;
            var map = Retenues(socle);
            map[sessionId] = treatedWaitingTs;
            return Ecrire(map);
        }
    }

    /// <summary>
    /// RETIRE une session du magasin (point RÉVERSIBLE, NET-03, absent d'<see cref="ArchiveStore"/>) et
    /// laisse tomber les entrées hors rétention. Clé absente → true sans réécriture. Fichier illisible → quarantaine puis
    /// écriture d'une map vide (l'entrée n'existe plus de toute façon). Rend false si rien n'a pu être écrit.
    /// </summary>
    public bool Remove(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return false;
        lock (_magasin.Verrou)
        {
            var lu = _magasin.Lire();
            if (lu.EstFiable && !lu.Map.ContainsKey(sessionId)) return true;   // rien à faire → aucune réécriture
            if (!_magasin.PreparerEcriture(lu, out var socle)) return false;
            var map = Retenues(socle);
            map.Remove(sessionId);
            return Ecrire(map);
        }
    }

    private bool Ecrire(Dictionary<string, long> map)
    {
        if (!_magasin.Ecrire(map)) return false;
        _dernierLu = map;
        return true;
    }

    // Les entrées encore dans la rétention de fichier (HorizonsSessions.RetentionTraitees), copie neuve.
    private Dictionary<string, long> Retenues(Dictionary<string, long> map)
    {
        var now = _horloge.UtcNow.ToUnixTimeMilliseconds();
        var retenues = new Dictionary<string, long>();
        foreach (var (id, ts) in map)
            if (now - ts < HorizonsSessions.RetentionTraitees.TotalMilliseconds)
                retenues[id] = ts;
        return retenues;
    }
}
