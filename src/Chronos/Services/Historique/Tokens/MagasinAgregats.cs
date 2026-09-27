using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Chronos.Models.Historique.Tokens;

namespace Chronos.Services.Historique.Tokens;

/// <summary>
/// TOK-01 — le magasin des agrégats de tokens : <c>tokens-AAAA-MM.jsonl</c>, un fichier par mois UTC du slot, un
/// état en mémoire par mois.
///
/// <para><b>POURQUOI réécrire le mois entier</b> : l'agrégat est minuscule (3 886 tuples = 524 Ko pour TOUT l'historique,
/// 304 Ko pour le mois le plus chargé, mesuré le 2026-09-27) ; une réécriture temp + <c>Move</c> coûte quelques
/// millisecondes, rend « tranches réécrites, pas ajoutées » (TOK-03) trivial, et jamais rien de partiel n'est
/// observable — motif <c>LastExactStore.Save</c>. Le mode d'ouverture « ajout » n'a pas sa place ici : ce fichier
/// est une PROJECTION, pas un journal.</para>
///
/// <para><b>POURQUOI le fichier n'est pas la vérité pour les mois ouverts</b> : l'index d'ids (<c>IndexMessages</c>, 33-02)
/// est la mémoire d'idempotence ; ce fichier en est la projection (<see cref="RemplacerMois"/>). Les mois plus anciens
/// que l'horizon de l'index sont GELÉS et rechargés tels quels (<see cref="ChargerMois"/>) : un delta qui tombe dans un
/// mois pas encore en mémoire commence par le charger, pour ne jamais écraser un mois gelé par une tranche isolée.</para>
///
/// <para><b>Ordre d'écriture déterministe</b> (D-33-04) : slot croissant, puis modèle (ordinal), puis principal avant
/// sous-agent — deux écritures du même état donnent des fichiers identiques octet pour octet ; c'est l'assertion
/// d'idempotence de 33-03. <b>Rétention</b> alignée sur le journal des relevés (<see cref="RetentionMois"/>, jamais un
/// nombre en dur).</para>
///
/// <para><see cref="IEtatMagasin"/> (CPT-02) : troisième magasin de la section « [Magasins persistants] » du diagnostic ;
/// <see cref="Chemin"/> est le DOSSIER ; <see cref="DerniereEcriture"/> = mtime du fichier après le <c>Move</c> (D-32-05),
/// le chiffre même qu'une sonde hors arbre lit. Un échec d'écriture pose <see cref="DerniereErreur"/> et rend false,
/// jamais d'exception vers l'appelant. Entiers seulement (garde TOK-05). Aucune E/S hors du dossier injecté.
/// Type NEUTRE (aucun WPF). Un seul verrou, sections courtes : <see cref="Appliquer"/> vient du thread de fond,
/// <see cref="TranchesDuMois"/> du lecteur.</para>
/// </summary>
public sealed class MagasinAgregats : IEtatMagasin
{
    /// <summary>Au-delà, un fichier mensuel est supprimé par <see cref="Purger"/>. Aligné sur le journal des relevés.</summary>
    public static readonly int RetentionMois = JournalReleves.RetentionMois;

    private static readonly Regex NomMensuel = new(@"^tokens-(\d{4})-(\d{2})\.jsonl$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly UTF8Encoding Utf8SansBom = new(encoderShouldEmitUTF8Identifier: false);

    // Clé d'une tranche dans son mois : (modèle, origine, slot). Le slot est TOUJOURS normalisé par SlotDe.
    private readonly Dictionary<DateTimeOffset, Dictionary<(string Model, bool Sub, DateTimeOffset Slot), TrancheTokens>> _parMois = new();
    private readonly HashSet<DateTimeOffset> _sales = new();
    private readonly object _verrou = new();
    private readonly IClock _clock;

    public MagasinAgregats(string dossier, IClock clock)
    {
        Dossier = dossier ?? throw new ArgumentNullException(nameof(dossier));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Dossier injecté (<c>ChronosPaths.HistoriqueDir</c> en production, 33-05).</summary>
    public string Dossier { get; }

    /// <inheritdoc/>
    public string Nom => NomsMagasins.AgregatsTokens;

    /// <inheritdoc/>
    public string Chemin => Dossier;

    /// <summary>UTC, mtime du dernier fichier mensuel écrit (après le <c>Move</c>). null = rien écrit par ce processus.</summary>
    public DateTimeOffset? DerniereEcriture { get; private set; }

    /// <summary>« Type : message » de la dernière écriture ratée ; null après un succès.</summary>
    public string? DerniereErreur { get; private set; }

    /// <summary>Nombre de fichiers mensuels écrits par ce processus (un mois réécrit deux fois compte deux).</summary>
    public int MoisEcrits { get; private set; }

    /// <summary>Lignes refusées par le lecteur tolérant au chargement des mois — exposé au diagnostic, jamais tu.</summary>
    public int LignesIgnorees { get; private set; }

    /// <summary>Tranches actuellement en mémoire, tous mois confondus.</summary>
    public int TranchesEnMemoire
    {
        get { lock (_verrou) return _parMois.Values.Sum(m => m.Count); }
    }

    /// <summary>Mois (1er du mois UTC) modifiés depuis leur dernière écriture — copie.</summary>
    public IReadOnlyCollection<DateTimeOffset> MoisSales
    {
        get { lock (_verrou) return _sales.OrderBy(m => m).ToList(); }
    }

    /// <summary>Nom du fichier mensuel : mois UTC du slot (ou de tout instant du mois), chiffres invariants.</summary>
    public static string NomFichier(DateTimeOffset slotOuMois)
        => "tokens-" + slotOuMois.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".jsonl";

    /// <summary>Chemin complet du fichier mensuel qui porte un slot.</summary>
    public string CheminDuMois(DateTimeOffset slotOuMois) => Path.Combine(Dossier, NomFichier(slotOuMois));

    /// <summary>Un nom de fichier est-il un mois d'agrégats (<c>tokens-AAAA-MM.jsonl</c>) ? Rend le 1er du mois UTC.</summary>
    public static bool EstNomMensuel(string nomFichier, out DateTimeOffset mois)
    {
        mois = default;
        var m = NomMensuel.Match(nomFichier);
        if (!m.Success
            || !int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var annee)
            || !int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var numero)
            || numero is < 1 or > 12 || annee < 1)
            return false;

        mois = new DateTimeOffset(annee, numero, 1, 0, 0, 0, TimeSpan.Zero);
        return true;
    }

    /// <summary>
    /// Charge un mois depuis son fichier (lecture tolérante ligne par ligne, partage large) et REMPLACE l'état mémoire de
    /// ce mois SANS le salir : charger ne réécrit jamais. Fichier absent → 0, rien n'est créé. Rend le nombre de tranches
    /// lues ; les lignes refusées s'ajoutent à <see cref="LignesIgnorees"/>. Ne lève jamais.
    /// </summary>
    public int ChargerMois(DateTimeOffset mois)
    {
        lock (_verrou) return ChargerMoisSousVerrou(TrancheTokens.MoisDe(mois));
    }

    /// <summary>
    /// Reprojection : remplace l'état mémoire du mois par <paramref name="tranches"/> et le marque sale. Les tranches
    /// d'un fichier réingéré sont ainsi RÉÉCRITES, pas ajoutées (TOK-03). Une tranche d'un autre mois est ignorée.
    /// </summary>
    public void RemplacerMois(DateTimeOffset mois, IEnumerable<TrancheTokens> tranches)
    {
        var cle = TrancheTokens.MoisDe(mois);
        var etat = new Dictionary<(string Model, bool Sub, DateTimeOffset Slot), TrancheTokens>();
        foreach (var t in tranches)
        {
            var slot = TrancheTokens.SlotDe(t.Slot);
            if (TrancheTokens.MoisDe(slot) != cle) continue;
            etat[(t.Model, t.Sub, slot)] = t with { Slot = slot };
        }

        lock (_verrou)
        {
            _parMois[cle] = etat;
            _sales.Add(cle);
        }
    }

    /// <summary>
    /// Ajoute un delta à sa tranche : créée (N = 1 si premier passage, sinon 0) ou augmentée (N += 1 seulement si
    /// premier passage). Un delta nul ne fait rien et ne salit pas. Un mois PAS ENCORE en mémoire est d'abord chargé
    /// depuis son fichier : un mois gelé n'est jamais écrasé par une tranche isolée.
    /// </summary>
    public void Appliquer(DeltaTranche d)
    {
        if (d.EstNul) return;

        var slot = TrancheTokens.SlotDe(d.Slot);
        var mois = TrancheTokens.MoisDe(slot);
        var n = d.NouveauMessage ? 1 : 0;

        lock (_verrou)
        {
            if (!_parMois.ContainsKey(mois)) ChargerMoisSousVerrou(mois);
            var etat = _parMois[mois];

            var cle = (d.Model, d.Sub, slot);
            etat[cle] = etat.TryGetValue(cle, out var t)
                ? t with { In = t.In + d.In, Out = t.Out + d.Out, CacheW = t.CacheW + d.CacheW, CacheR = t.CacheR + d.CacheR, N = t.N + n }
                : new TrancheTokens(slot, d.Model, d.Sub, d.In, d.Out, d.CacheW, d.CacheR, n);

            _sales.Add(mois);
        }
    }

    /// <summary>Copie triée (slot, modèle ordinal, sub) de l'état mémoire d'un mois ; vide si le mois n'est pas en mémoire.</summary>
    public IReadOnlyList<TrancheTokens> TranchesDuMois(DateTimeOffset mois)
    {
        lock (_verrou)
        {
            return _parMois.TryGetValue(TrancheTokens.MoisDe(mois), out var etat)
                ? Trier(etat.Values)
                : Array.Empty<TrancheTokens>();
        }
    }

    /// <summary>
    /// Réécrit ATOMIQUEMENT chaque mois sale : temp unique par processus sur le même volume, puis <c>Move</c> par-dessus
    /// l'ancien fichier — un fichier partiel n'est jamais observable. Un mois sans tranche s'écrit vide (état vrai).
    /// <c>false</c> = au moins un mois n'a pas pu être écrit (<see cref="DerniereErreur"/> posée, le mois reste sale,
    /// le temp est effacé). Ne lève jamais.
    /// </summary>
    public bool EcrireMoisSales()
    {
        lock (_verrou)
        {
            if (_sales.Count == 0) return true;

            // Le dossier se crée HORS de la boucle : un dossier « poison » (un FICHIER porte son nom, droits refusés) est un
            // échec immédiat, pas une reprise.
            try { Directory.CreateDirectory(Dossier); }
            catch (Exception ex) { DerniereErreur = Decrire(ex); return false; }

            foreach (var mois in _sales.OrderBy(m => m).ToList())
            {
                var chemin = CheminDuMois(mois);
                var tmp = chemin + ".tmp-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
                try
                {
                    using (var w = new StreamWriter(tmp, append: false, Utf8SansBom) { NewLine = "\n" })
                    {
                        foreach (var t in Trier(_parMois[mois].Values))
                            w.WriteLine(LigneAgregat.Serialiser(t));
                    }
                    File.Move(tmp, chemin, overwrite: true);

                    // D-32-05 : le mtime, et non l'horloge injectée — c'est ce qu'une sonde hors arbre lit.
                    DerniereEcriture = new DateTimeOffset(File.GetLastWriteTimeUtc(chemin), TimeSpan.Zero);
                    MoisEcrits++;
                    _sales.Remove(mois);
                }
                catch (Exception ex)
                {
                    DerniereErreur = Decrire(ex);
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* le temp orphelin n'est pas la panne à rapporter */ }
                    return false;
                }
            }

            DerniereErreur = null;
            return true;
        }
    }

    /// <summary>
    /// Rétention : supprime les fichiers mensuels dont le mois est antérieur à (mois courant − <see cref="RetentionMois"/>).
    /// Un nom non conforme (shard d'ids, notes) est ignoré et compté ; une suppression en échec est comptée, jamais
    /// relancée ; un dossier absent rend un bilan à zéro. Motif <c>JournalReleves.Purger</c>. Ne lève jamais.
    /// </summary>
    public BilanRetention Purger()
    {
        if (!Directory.Exists(Dossier)) return new BilanRetention(0, 0, 0);

        string[] fichiers;
        try { fichiers = Directory.GetFiles(Dossier); }
        catch { return new BilanRetention(0, 0, 0); }

        var maintenant = _clock.UtcNow;
        var limite = maintenant.Year * 12 + maintenant.Month - RetentionMois;   // index de mois : tout ce qui est STRICTEMENT avant part

        int supprimes = 0, echecs = 0, ignores = 0;
        foreach (var fichier in fichiers)
        {
            if (!EstNomMensuel(Path.GetFileName(fichier), out var mois))
            {
                ignores++;
                continue;
            }

            if (mois.Year * 12 + mois.Month >= limite) continue;   // dans la rétention : gardé, et pas compté

            try { File.Delete(fichier); supprimes++; }
            catch { echecs++; }   // pas supprimé = pas compté comme supprimé : le bilan reste une observation
        }

        return new BilanRetention(supprimes, echecs, ignores);
    }

    // --- Internes ---

    private static List<TrancheTokens> Trier(IEnumerable<TrancheTokens> tranches)
        => tranches.OrderBy(t => t.Slot).ThenBy(t => t.Model, StringComparer.Ordinal).ThenBy(t => t.Sub).ToList();

    private static string Decrire(Exception ex) => ex.GetType().Name + " : " + ex.Message;

    // Lecture tolérante d'un mois (partage large : un autre processus peut le tenir). L'état mémoire du mois est
    // remplacé par ce qui a été lu — même vide, même après une erreur d'E/S (consignée) : le mois est alors « connu ».
    private int ChargerMoisSousVerrou(DateTimeOffset mois)
    {
        var etat = new Dictionary<(string Model, bool Sub, DateTimeOffset Slot), TrancheTokens>();
        var chemin = CheminDuMois(mois);

        if (File.Exists(chemin))
        {
            try
            {
                using var fs = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var lecteur = new StreamReader(fs, Utf8SansBom, detectEncodingFromByteOrderMarks: true);
                while (lecteur.ReadLine() is { } brute)
                {
                    var ligne = brute.TrimEnd('\r');
                    if (ligne.Length == 0) continue;   // ligne vide : ni lue ni comptée
                    if (LigneAgregat.Parser(ligne, out var t) && t is not null)
                        etat[(t.Model, t.Sub, t.Slot)] = t;
                    else
                        LignesIgnorees++;
                }
            }
            catch (Exception ex)
            {
                DerniereErreur = Decrire(ex);   // une lecture ratée se lit au diagnostic ; le mois reste chargeable plus tard
            }
        }

        _parMois[mois] = etat;
        _sales.Remove(mois);
        return etat.Count;
    }
}
