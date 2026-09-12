using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Mémorise les sessions ARCHIVÉES dans %APPDATA%\Chronos\archived.json — une map
/// { session_id : archivedAt(ms) }. Le <see cref="SessionMonitor"/> les écarte, définitivement.
///
/// <para>CONTRAT UNIQUE (TRT-04) : ce qui est archivé NE REVIENT JAMAIS. Le magasin appliquait
/// auparavant une durée de vie de six heures pendant que son contrat annoncé et le libellé du menu
/// promettaient le définitif : le clic droit était une mise en sourdine, et l'utilisateur n'avait aucun
/// moyen de le savoir. Une durée de vie qui défait un geste explicite en silence n'est pas une
/// optimisation de taille, c'est une promesse non tenue.</para>
///
/// <para>Ce qui borne encore le fichier : rien d'automatique, et c'est assumé. Une entrée pèse une
/// ligne, et elle n'est écrite que sur un geste délibéré. Le seul retrait possible est
/// <see cref="PurgerPrefixe"/> — un geste, lui aussi, jamais une horloge. À NE PAS CONFONDRE avec
/// <see cref="TreatedStore"/>, dont le contrat est l'inverse : RÉVERSIBLE — une session traitée
/// revient dès qu'elle redemande quelque chose — et dont la seule borne temporelle est une RÉTENTION
/// de fichier, choisie supérieure au seuil au-delà duquel le moniteur cesse de lire la session.</para>
///
/// <para>L'horloge est INJECTÉE, jamais lue au système : elle ne sert plus qu'à horodater un geste.</para>
/// Tolérance totale : fichier absent/corrompu → ensemble vide, jamais d'exception. Aucun type WPF.
/// </summary>
public sealed class ArchiveStore
{
    private readonly string _path;
    private readonly IClock _horloge;

    public ArchiveStore(string? path = null, IClock? clock = null)
    {
        _path = path ?? Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Chronos", "archived.json");
        _horloge = clock ?? new SystemClock();
    }

    /// <summary>Identifiants archivés. TOUS : aucune entrée n'est écartée par le temps qui passe.</summary>
    public ISet<string> Load()
    {
        var set = new HashSet<string>();
        try
        {
            if (!File.Exists(_path)) return set;
            using var doc = JsonDocument.Parse(File.ReadAllText(_path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return set;
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.Value.TryGetInt64(out _))   // valeur non numérique = entrée illisible, donc ignorée
                    set.Add(p.Name);
        }
        catch { }
        return set;
    }

    /// <summary>Archive une session (idempotent). Les entrées déjà présentes sont conservées telles quelles.</summary>
    public void Add(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        var now = _horloge.UtcNow.ToUnixTimeMilliseconds();
        var map = new Dictionary<string, long>();
        try
        {
            if (File.Exists(_path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_path));
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    foreach (var p in doc.RootElement.EnumerateObject())
                        if (p.Value.TryGetInt64(out var ts))
                            map[p.Name] = ts;   // conservée TELLE QUELLE : recharger n'est pas périmer
            }
        }
        catch { }
        map[sessionId] = now;

        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(map);
            var tmp = _path + ".tmp-" + System.Environment.ProcessId;
            File.WriteAllText(tmp, json);
            File.Move(tmp, _path, overwrite: true);
        }
        catch { }
    }

    /// <summary>
    /// SRC-02 (phase 21) — retire DU FICHIER toute entrée dont l'identifiant commence par
    /// <paramref name="prefixe"/>, et rend le nombre d'entrées effectivement retirées ET écrites.
    ///
    /// <para>Distinct de <see cref="Load"/>, qui se contente d'ÉCARTER à la lecture. Écarter n'est pas
    /// retirer : les deux entrées de l'ancienne source app-bureau relevées le 2026-09-12 dans
    /// archived.json prouvent que l'utilisateur avait dû archiver ces fantômes à la main, faute qu'ils
    /// puissent vieillir. Tant qu'elles restent dans le fichier, son contournement reste gravé dans ses
    /// données.</para>
    ///
    /// <para>Distinct aussi d'<see cref="Add"/>, qui applique au passage la purge des entrées expirées :
    /// ici, AUCUN filtre de TTL. Purger un préfixe et expirer une entrée sont deux gestes différents ;
    /// les confondre ferait disparaître des archives que l'utilisateur n'a pas demandé de retirer.</para>
    ///
    /// <para>Rien à retirer → le fichier n'est PAS réécrit (ni date, ni contenu). Écriture DIRECTE et non
    /// par fichier temporaire : geste unique au démarrage, sans lecteur concurrent (l'écriture par
    /// tmp + Move a été mesurée à 200 échecs sur 500 contre un lecteur, la directe à 0 sur 500).</para>
    ///
    /// <para>Le nombre rendu n'est pas une intention mais une OBSERVATION : si l'écriture échoue, la
    /// méthode rend 0, parce que rien n'a été retiré.</para>
    /// </summary>
    public int PurgerPrefixe(string prefixe)
    {
        if (string.IsNullOrEmpty(prefixe)) return 0;   // un préfixe vide viderait tout : jamais légitime

        var restantes = new Dictionary<string, long>();
        var retirees = 0;
        var ecrit = false;
        try
        {
            if (!File.Exists(_path)) return 0;
            using (var doc = JsonDocument.Parse(File.ReadAllText(_path)))
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return 0;
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    if (!p.Value.TryGetInt64(out var ts)) continue;   // entrée illisible : ignorée, non conservée
                    if (p.Name.StartsWith(prefixe, System.StringComparison.Ordinal)) { retirees++; continue; }
                    restantes[p.Name] = ts;                           // conservée TELLE QUELLE, sans TTL
                }
            }
            if (retirees == 0) return 0;                              // rien à faire = ne rien écrire

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(restantes));
            ecrit = true;
        }
        catch { }
        return ecrit ? retirees : 0;
    }
}
