using System.Diagnostics;
using System.Globalization;
using System.IO;
using Chronos.Models.Historique.Tokens;
using Microsoft.Extensions.Hosting;

namespace Chronos.Services.Historique.Tokens;

/// <summary>Ce qu'une passe a fait : des entiers, un booléen, une durée. <c>Complete</c> est faux si l'annulation ou une exception l'a interrompue.</summary>
public sealed record BilanPasse(int FichiersTotal, int FichiersOuverts, int RaccourcisRelus, int Disparus, int LignesIgnorees, bool Complete, TimeSpan DureeMur);

/// <summary>
/// TOK-02 / TOK-03 — la reconstruction des agrégats de tokens : un <see cref="BackgroundService"/> qui POSSÈDE un thread dédié.
///
/// <para><b>POURQUOI un thread et pas le pool</b> : la priorité ne se règle que sur un thread à soi. <b>POURQUOI la boucle est
/// SYNCHRONE</b> : un <c>new Thread</c> n'a pas de contexte de synchronisation ; la continuation de toute attente asynchrone
/// repartirait sur le pool à priorité normale — la décision « priorité basse » serait vidée en silence au premier fichier
/// (Pitfall 1). L'intention « céder la main entre fichiers, rester interruptible » est tenue autrement : le quantum est cédé
/// entre deux fichiers, l'annulation est vérifiée entre fichiers et toutes les 4 096 lignes dans le lecteur (D-33-13). Mesuré
/// sur la vraie machine (2,08 Go, 1 565 fichiers) : passe complète 2,4–2,6 s mur / 2,7 s CPU à chaud, semaine courante à
/// 1,7 s — la priorité basse suffit, aucun throttling élaboré.</para>
///
/// <para><b>Ordre d'une passe</b> : inventaire (récursif, matérialisé sous try/catch : une racine absente est une passe vide,
/// pas une panne), tri par mtime DÉCROISSANT, classification par les curseurs, lecture des seuls fichiers nouveau / grandi /
/// raccourci depuis leur offset, checkpoint « semaine courante disponible » dès le premier fichier plus vieux que
/// <see cref="SemaineCourante"/> (par MTIME et non par timestamp, Pitfall 7 : un message de la semaine ne peut vivre que dans
/// un fichier écrit depuis), puis flush tous les <see cref="FichiersParLot"/> fichiers ou <see cref="DelaiEntreLots"/>, flush
/// final, couverture garantie SEULEMENT si la passe est complète (annulée ≠ complète, D-33-15).</para>
///
/// <para><b>Ordre de flush ids → agrégats → curseurs, reprojection des mois ouverts au démarrage</b> (D-33-14) : la chaîne
/// s'ARRÊTE à la première étape en échec (DATA-2) — un curseur ne dépasse jamais ce que l'index a persisté. Un arrêt à
/// n'importe quel point est rattrapé — les ids écrits sont reprojetés, les fichiers sans curseur sont relus depuis le dernier
/// curseur persisté et leurs ids connus rendent delta 0. Une ligne sans aucun id reçoit ici une CLÉ SYNTHÉTIQUE déterministe
/// (instant, modèle, origine, quatre compteurs) : sans elle, la reprojection ne pourrait pas restituer ce que l'index ignore,
/// et « annulé après un fichier puis repris » ne donnerait plus les mêmes octets qu'une passe ininterrompue (D-33-18).
/// Le curseur enregistre la taille et le mtime relevés à l'INVENTAIRE (D-33-16) : tout ce qui bouge après sera vu au cycle
/// suivant. Une passe en échec ne tue pas le service (D-33-17) : <c>EnEchec</c>, <c>DerniereErreur</c>, nouvelle passe au cycle
/// suivant ; <c>StopAsync</c> reste propre.</para>
///
/// <para><b>Lecture illisible d'une brique = passe interrompue SANS flush</b> (MAT-3 / DATA-3, phase 42.2) : un shard d'ids
/// illisible au démarrage (l'index serait incomplet : la reprojection réécrirait le mois ouvert amputé, et toute lecture
/// recompterait), ou un mois d'agrégats illisible au moment d'y appliquer un delta (le réécrire en entier effacerait ce qu'on
/// n'a pas lu), lève une <see cref="LectureIllisibleException"/> — qui traverse le lecteur de transcripts et atteint le
/// <c>catch</c> de <see cref="ExecuterUnePasse"/>, où rien n'est flushé : ni agrégats ni curseurs écrits par-dessus. La passe
/// suivante REJOUE l'initialisation depuis l'état disque (index relu, état mémoire des agrégats abandonné, curseurs relus) :
/// le delta n'est ni perdu ni compté deux fois. Un mois ouvert dont l'index ne sait rien (shard absent ou de 0 octet) alors
/// que son fichier d'agrégats porte des octets n'est pas reprojeté vide : il est relu tel quel.</para>
///
/// <para><b>Mois gelés</b> (DATA-1, phase 42.2) : l'index ne charge d'office que les mois ouverts. Avant d'indexer un message
/// d'un autre mois, <see cref="Traiter"/> demande son shard (<see cref="IndexMessages.AssurerMoisCharge"/>) : présent ⇒ chargé,
/// la relecture reste idempotente ; absent ET fichier d'agrégats du mois présent au démarrage ⇒ le mois est déjà compté, le
/// message est ignoré et compté (<see cref="MessagesIgnoresMoisGeles"/>) ; absent sans fichier ⇒ première reconstruction,
/// acceptée. Compromis assumé : une PREMIÈRE reconstruction d'un mois sorti de la rétention des shards, interrompue entre
/// l'écriture de ses agrégats et celle de tous ses curseurs, laisse au démarrage suivant un mois tenu pour déjà compté — les
/// messages non encore lus en sont ignorés. Sous-comptage possible plutôt que double comptage.</para>
///
/// <para>Lecture seule stricte de la racine des projets ; écriture uniquement sous <c>HistoriqueDir</c>, par les briques
/// (le service n'écrit rien lui-même). Type NEUTRE (aucun WPF). Progression en ENTIERS (garde TOK-05) : les champs d'état
/// sont écrits par le thread de fond et lus par le thread UI — valeurs atomiques (int, bool, référence, ticks en <c>long</c>),
/// aucune cohérence croisée promise entre deux compteurs : c'est de l'affichage.</para>
/// </summary>
public sealed class ReconstructionTokens : BackgroundService, IEtatReconstruction
{
    /// <summary>Attente entre deux passes une fois la première complète (annulable à tout instant).</summary>
    public static readonly TimeSpan CadenceIncrementale = TimeSpan.FromSeconds(60);

    /// <summary>Le checkpoint « semaine courante disponible » se pose au premier fichier de mtime plus vieux que ceci.</summary>
    public static readonly TimeSpan SemaineCourante = TimeSpan.FromDays(7);

    /// <summary>Flush par lot : tous les N fichiers passés en revue…</summary>
    public const int FichiersParLot = 100;

    /// <summary>… ou dès que ce délai s'est écoulé depuis le dernier flush.</summary>
    public static readonly TimeSpan DelaiEntreLots = TimeSpan.FromSeconds(2);

    /// <summary>Nom du thread de fond (visible au débogueur et dans les vidages).</summary>
    public const string NomThread = "Chronos.AgregatsTokens";

    /// <summary>Préfixe de la clé synthétique d'une ligne sans <c>message.id</c> ni <c>requestId</c> (D-33-18).</summary>
    public const string PrefixeSansId = "sans-id:";

    private readonly ChronosPaths _paths;
    private readonly MagasinAgregats _magasin;
    private readonly IndexMessages _index;
    private readonly IClock _clock;
    private readonly TimeSpan _cadence;
    private readonly Action<string>? _apresFichier;
    private readonly Action<string>? _apresEtapeFlush;

    // Briques à état de fichier, créées à la première passe (le dossier peut être « poison » : l'initialisation est rejouée tant qu'elle échoue).
    private Curseurs? _curseurs;
    private CouvertureTokens? _couverture;
    private bool _initialise;
    private readonly HashSet<DateTimeOffset> _moisAgregesAuDemarrage = new();   // DATA-1 : mois hors fenêtre déjà agrégés sur disque
    private readonly HashSet<DateTimeOffset> _moisGelesSansIndex = new();       // DATA-1 : … dont le shard est absent → messages ignorés

    // --- État exposé (IEtatReconstruction) : écrit par le thread de fond, lu par le thread UI. Écritures atomiques, Volatile pour l'ordre. ---
    private int _phase = (int)PhaseReconstruction.JamaisLancee;
    private int _fichiersTraites;
    private int _fichiersTotal;
    private int _fichiersOuvertsDernierePasse;
    private int _raccourcisRelusDernierePasse;
    private bool _semaineCouranteDisponible;
    private string? _dernierFichier;
    private string? _derniereErreur;
    private int _fichiersDisparus;
    private int _lignesIgnoreesLecteur;
    private long _dureeMurTicks = -1;          // −1 = jamais mesuré (un TimeSpan? n'est pas atomique : on garde des ticks)
    private long _dureeCpuTicks = -1;
    private long _termineeUtcTicks = -1;
    private int _messagesIgnoresMoisGeles;

    public ReconstructionTokens(ChronosPaths paths, MagasinAgregats magasin, IndexMessages index, IClock clock,
                                TimeSpan? cadence = null, Action<string>? apresFichier = null, Action<string>? apresEtapeFlush = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _magasin = magasin ?? throw new ArgumentNullException(nameof(magasin));
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _cadence = cadence ?? CadenceIncrementale;
        _apresFichier = apresFichier;
        _apresEtapeFlush = apresEtapeFlush;
    }

    // --- IEtatReconstruction ---

    public PhaseReconstruction Phase => (PhaseReconstruction)Volatile.Read(ref _phase);
    public int FichiersTraites => Volatile.Read(ref _fichiersTraites);
    public int FichiersTotal => Volatile.Read(ref _fichiersTotal);
    public int FichiersOuvertsDernierePasse => Volatile.Read(ref _fichiersOuvertsDernierePasse);
    public bool SemaineCouranteDisponible => Volatile.Read(ref _semaineCouranteDisponible);
    public string? DernierFichier => Volatile.Read(ref _dernierFichier);
    public string? DerniereErreur => Volatile.Read(ref _derniereErreur);
    public int FichiersDisparus => Volatile.Read(ref _fichiersDisparus);
    public int LignesIgnorees => Volatile.Read(ref _lignesIgnoreesLecteur) + _index.LignesIgnorees;
    public int IdsConnus => _index.IdsConnus;
    public TimeSpan? DureeMurDernierePasse => Duree(Volatile.Read(ref _dureeMurTicks));
    public TimeSpan? DureeCpuProcessusDernierePasse => Duree(Volatile.Read(ref _dureeCpuTicks));
    public DateTimeOffset? DerniereReconstructionTerminee
        => Volatile.Read(ref _termineeUtcTicks) is var t && t >= 0 ? new DateTimeOffset(t, TimeSpan.Zero) : null;

    /// <summary>DATA-1 — messages d'un mois gelé déjà agrégé au démarrage et dont l'index d'ids n'est plus sur disque : ignorés
    /// parce que déjà comptés (cumul depuis le démarrage, diagnostic).</summary>
    public int MessagesIgnoresMoisGeles => Volatile.Read(ref _messagesIgnoresMoisGeles);

    /// <summary>Fichiers relus de zéro parce que plus courts que leur curseur, lors de la dernière passe (diagnostic).</summary>
    public int RaccourcisRelusDernierePasse => Volatile.Read(ref _raccourcisRelusDernierePasse);

    public event EventHandler? Changement;

    // --- Cycle de vie hébergé ---

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var fin = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { Boucle(stoppingToken); }                                   // SYNCHRONE : rien d'asynchrone sur ce thread
            catch (Exception ex) { Signaler(PhaseReconstruction.EnEchec, Decrire(ex)); }
            finally { fin.TrySetResult(); }
        })
        { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = NomThread };
        thread.Start();
        return fin.Task;   // StartAsync rend la main immédiatement ; StopAsync attend ce Task après avoir annulé le jeton
    }

    private void Boucle(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { ExecuterUnePasse(ct); }
            catch (Exception ex) { Signaler(PhaseReconstruction.EnEchec, Decrire(ex)); }   // ceinture : ExecuterUnePasse capture déjà
            if (ct.IsCancellationRequested) break;
            ct.WaitHandle.WaitOne(_cadence);                                 // attente annulable, sans rien d'asynchrone
        }
        Signaler(PhaseReconstruction.Arretee, null);
    }

    /// <summary>Une passe complète ou incrémentale, synchrone, sur le thread appelant — publique pour être testée sans thread.
    /// Ne lève jamais : une exception devient <c>EnEchec</c> + <c>DerniereErreur</c> et un bilan non complet.</summary>
    public BilanPasse ExecuterUnePasse(CancellationToken ct)
    {
        var chrono = Stopwatch.StartNew();
        var cpuDebut = TempsCpuProcessus();
        int total = 0, ouverts = 0, raccourcis = 0, disparus = 0, lignesIgnorees = 0;
        var complete = false;

        try
        {
            Initialiser();
            var curseurs = _curseurs!;
            var couverture = _couverture!;
            var debutPasse = _clock.UtcNow;

            // 1. Inventaire, du plus récent au plus ancien ; les disparus sont retirés des curseurs (rien à soustraire : Pattern 2).
            var fichiers = Inventorier();
            total = fichiers.Count;
            Volatile.Write(ref _fichiersTotal, total);
            Volatile.Write(ref _fichiersTraites, 0);
            var presentes = new HashSet<string>(fichiers.Select(f => curseurs.CleRelative(f.Chemin)), StringComparer.OrdinalIgnoreCase);
            disparus = curseurs.RetirerDisparus(presentes);
            Interlocked.Add(ref _fichiersDisparus, disparus);

            // 2. Lecture.
            var semaineFaite = false;
            var depuisFlush = 0;
            var dernierFlush = chrono.Elapsed;
            var limiteSemaine = debutPasse - SemaineCourante;

            foreach (var f in fichiers)
            {
                if (ct.IsCancellationRequested) break;

                if (!semaineFaite && f.Mtime < limiteSemaine)
                {
                    // Tous les fichiers de la semaine (par mtime) sont lus : on les écrit AVANT d'ouvrir l'historique.
                    semaineFaite = true;
                    if (Flush()) Volatile.Write(ref _semaineCouranteDisponible, true);
                    depuisFlush = 0;
                    dernierFlush = chrono.Elapsed;
                }

                var etat = curseurs.Classer(f.Chemin, f.Taille, f.Mtime, out var depuis);
                if (etat != EtatFichier.Inchange)
                {
                    if (etat == EtatFichier.Raccourci) raccourcis++;
                    ouverts++;
                    var r = LecteurTranscript.Lire(f.Chemin, depuis, _clock.UtcNow, Traiter, ct);
                    lignesIgnorees += r.LignesIgnorees;
                    Interlocked.Add(ref _lignesIgnoreesLecteur, r.LignesIgnorees);
                    if (r.Annulee) break;   // curseur NON enregistré : ce fichier sera relu depuis « depuis », ses ids connus rendront delta 0
                    curseurs.Enregistrer(f.Chemin, r.OffsetDerniereLigneComplete, f.Taille, f.Mtime);   // D-33-16
                    if (r.PlusAncienTs is { } plusAncien) couverture.VoirLigne(plusAncien);
                }

                var cle = curseurs.CleRelative(f.Chemin);
                Interlocked.Increment(ref _fichiersTraites);
                Volatile.Write(ref _dernierFichier, cle);
                _apresFichier?.Invoke(cle);
                Changement?.Invoke(this, EventArgs.Empty);

                if (++depuisFlush >= FichiersParLot || chrono.Elapsed - dernierFlush >= DelaiEntreLots)
                {
                    Flush();
                    depuisFlush = 0;
                    dernierFlush = chrono.Elapsed;
                }

                Thread.Yield();   // céder le quantum entre deux fichiers, sans quitter ce thread
            }

            // 3. Dernier flush — aussi après une annulation : ce qui est lu est écrit.
            var flushFinal = Flush();
            complete = !ct.IsCancellationRequested;

            if (complete)
            {
                couverture.GarantirPasse(debutPasse, _clock.UtcNow);
                if (!couverture.Sauvegarder(CheminCouverture)) RetenirErreur("couverture", couverture.DerniereErreur);
                _apresEtapeFlush?.Invoke("couverture");
                if (flushFinal && DerniereErreur is null) Volatile.Write(ref _semaineCouranteDisponible, true);
                Volatile.Write(ref _termineeUtcTicks, _clock.UtcNow.UtcTicks);
            }

            Volatile.Write(ref _dureeMurTicks, chrono.Elapsed.Ticks);
            Volatile.Write(ref _dureeCpuTicks, (TempsCpuProcessus() - cpuDebut).Ticks);

            if (complete)
                Signaler(DerniereErreur is null ? PhaseReconstruction.Incremental : PhaseReconstruction.EnEchec, DerniereErreur);
            else
                Changement?.Invoke(this, EventArgs.Empty);   // annulée : la phase est posée par l'appelant (Arretee dans la boucle)
        }
        catch (LectureIllisibleException ex)
        {
            // MAT-3 : rien n'est flushé ; l'initialisation sera rejouée depuis l'état disque (l'index en mémoire connaît déjà
            // des ids dont le delta n'a pas été appliqué — les garder perdrait ces deltas à la relecture).
            _initialise = false;
            Signaler(PhaseReconstruction.EnEchec, ex.Message);
        }
        catch (Exception ex)
        {
            // D-33-17 : la passe se dit en échec ; pas de nouveau flush ici (la panne peut être dans le flush lui-même).
            Signaler(PhaseReconstruction.EnEchec, Decrire(ex));
        }

        Volatile.Write(ref _fichiersOuvertsDernierePasse, ouverts);
        Volatile.Write(ref _raccourcisRelusDernierePasse, raccourcis);
        return new BilanPasse(total, ouverts, raccourcis, disparus, lignesIgnorees, complete, chrono.Elapsed);
    }

    // --- Internes ---

    private string CheminCouverture => Path.Combine(_paths.HistoriqueDir, CouvertureTokens.NomFichier);

    // Une seule fois (rejouée tant qu'elle échoue) : rétentions, curseurs, couverture, index, REPROJECTION des mois ouverts.
    private void Initialiser()
    {
        if (_initialise) return;

        Directory.CreateDirectory(_paths.HistoriqueDir);
        _magasin.Purger();
        _index.Purger();
        _curseurs = Curseurs.Charger(Path.Combine(_paths.HistoriqueDir, Curseurs.NomFichier), _paths.ProjectsRoot);
        _couverture = CouvertureTokens.Charger(CheminCouverture);
        _magasin.AbandonnerEtatMemoire();   // rejeu après une passe interrompue : repartir de l'état disque, pas de la mémoire
        _index.Charger();

        // DATA-3 : un index incomplet ne doit ni être reprojeté (le mois ouvert serait réécrit amputé) ni servir à dédoublonner.
        if (_index.MoisIllisibles.Count > 0)
            throw new LectureIllisibleException("index d'ids : shard illisible ("
                + string.Join(", ", _index.MoisIllisibles.Select(m => m.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture)))
                + ") — reprojection et lecture reportées au cycle suivant");

        // DATA-1 : les mois GELÉS déjà agrégés (fichier présent au démarrage, hors mois ouverts). Relevé une fois par
        // initialisation : un mois créé pendant la session par une première reconstruction reste dédoublonné en mémoire.
        _moisAgregesAuDemarrage.Clear();
        _moisGelesSansIndex.Clear();
        var ouverts = _index.MoisOuverts().ToHashSet();
        foreach (var fichier in Directory.GetFiles(_paths.HistoriqueDir))
            if (MagasinAgregats.EstNomMensuel(Path.GetFileName(fichier), out var moisAgrege) && !ouverts.Contains(moisAgrege))
                _moisAgregesAuDemarrage.Add(moisAgrege);

        // Le fichier d'agrégats d'un mois ouvert n'est jamais la vérité : l'index l'est. Un mois sans aucun id et sans
        // fichier n'a rien à dire (pas de fichier vide créé) ; un fichier existant est remplacé par la projection, même vide.
        // Exception (DATA-3) : un index MUET (shard absent ou de 0 octet) face à un fichier qui porte des octets ne prouve
        // rien — le reprojeter le réécrirait vide. Le mois est relu tel quel ; illisible → passe interrompue.
        foreach (var mois in _index.MoisOuverts())
        {
            var entrees = _index.Entrees(mois).ToList();
            var chemin = _magasin.CheminDuMois(mois);
            if (entrees.Count == 0 && TailleNonNulle(chemin))
            {
                if (_magasin.ChargerMois(mois) < 0) throw MoisIllisible(mois);
                continue;
            }
            if (entrees.Count > 0 || File.Exists(chemin))
                _magasin.RemplacerMois(mois, ProjectionAgregats.Projeter(entrees));
        }

        _initialise = true;
        if (Phase == PhaseReconstruction.JamaisLancee) Signaler(PhaseReconstruction.Reconstruction, null);
    }

    // Inventaire MATÉRIALISÉ dans le try (phase 21 : une énumération paresseuse levait hors du catch) ; un fichier qui disparaît
    // entre l'énumération et la lecture de ses attributs est simplement absent de cette passe.
    private List<(string Chemin, long Taille, DateTimeOffset Mtime)> Inventorier()
    {
        var fichiers = new List<(string Chemin, long Taille, DateTimeOffset Mtime)>();
        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = 0,   // rien n'est passé sous silence (ni caché ni système) : la racine est celle de l'utilisateur
            };
            foreach (var f in new DirectoryInfo(_paths.ProjectsRoot).EnumerateFiles("*.jsonl", options))
            {
                try { fichiers.Add((f.FullName, f.Length, new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero))); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (Exception)
        {
            // Racine absente ou illisible : passe vide, pas une panne.
        }
        return fichiers.OrderByDescending(f => f.Mtime).ToList();
    }

    // Un message lu → (DATA-1) son mois doit être connu de l'index → l'index décide du delta → le magasin l'applique à la
    // tranche du PREMIER timestamp de l'id.
    private void Traiter(MessageLu m)
    {
        var cle = m.Id is null ? m with { Id = CleSansId(m) } : m;
        var mois = TrancheTokens.MoisDe(TrancheTokens.SlotDe(cle.Ts));
        if (!_moisGelesSansIndex.Contains(mois) && !_index.EstCharge(mois))
        {
            var etat = _index.AssurerMoisCharge(mois);
            if (etat == ChargementShard.Illisible)
                throw new LectureIllisibleException("index d'ids : shard "
                    + mois.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                    + " illisible au chargement à la demande — passe interrompue sans flush");
            if (etat == ChargementShard.Absent && _moisAgregesAuDemarrage.Contains(mois))
                _moisGelesSansIndex.Add(mois);   // décidé une fois : tous les messages de ce mois suivront
        }
        if (_moisGelesSansIndex.Contains(mois))
        {
            Interlocked.Increment(ref _messagesIgnoresMoisGeles);   // déjà compté dans le mois gelé : ni indexé ni appliqué
            return;
        }

        var d = _index.Ajouter(cle);
        if (d is null) return;
        var slot = TrancheTokens.SlotDe(d.Ts);
        if (!_magasin.Appliquer(new DeltaTranche(slot, d.Model, d.Sub, d.In, d.Out, d.CacheW, d.CacheR, d.NouveauMessage)))
            throw MoisIllisible(TrancheTokens.MoisDe(slot));   // DATA-3 : traverse LecteurTranscript (pas une IOException)
    }

    private static LectureIllisibleException MoisIllisible(DateTimeOffset mois)
        => new("agrégats : mois " + mois.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture)
               + " illisible — passe interrompue sans flush, rien n'est écrit par-dessus");

    // Dans le doute (attributs illisibles), « non nul » : on relit plutôt que de reprojeter par-dessus.
    private static bool TailleNonNulle(string chemin)
    {
        try { return new FileInfo(chemin) is { Exists: true, Length: > 0 }; }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }

    // D-33-18 : déterministe et indépendante du fichier (un renommage ou une copie ne recompte pas) — deux lignes sans id
    // strictement identiques (même instant, même modèle, mêmes quatre compteurs) sont indiscernables et comptent une fois.
    private static string CleSansId(MessageLu m)
        => string.Create(CultureInfo.InvariantCulture,
            $"{PrefixeSansId}{m.Ts.UtcTicks}:{m.Model}:{(m.Sub ? 1 : 0)}:{m.In}:{m.Out}:{m.CacheW}:{m.CacheR}");

    // D-33-14 — dans CET ordre : shards d'ids (ajout) → agrégats (Move) → curseurs (Move). La première erreur, retenue avec le
    // nom de la brique, ARRÊTE la chaîne (DATA-2) : un curseur ne dépasse jamais ce que l'index a persisté, des agrégats jamais
    // ce que l'index sait reprojeter. Les données restent sales en mémoire : le flush suivant les retente. Un flush réussi de
    // bout en bout efface l'erreur. Une exception levée par un hook de test se propage : c'est la « panne simulée » entre deux étapes.
    private bool Flush()
    {
        if (!_index.Flush())
        {
            Volatile.Write(ref _derniereErreur, Prefixer("index", _index.DerniereErreur));
            _apresEtapeFlush?.Invoke("ids");
            return false;
        }
        _apresEtapeFlush?.Invoke("ids");

        if (!_magasin.EcrireMoisSales())   // le mois reste sale : le lot suivant rattrape
        {
            Volatile.Write(ref _derniereErreur, Prefixer("agrégats", _magasin.DerniereErreur));
            _apresEtapeFlush?.Invoke("agregats");
            return false;
        }
        _apresEtapeFlush?.Invoke("agregats");

        if (!_curseurs!.Sauvegarder())
        {
            Volatile.Write(ref _derniereErreur, Prefixer("curseurs.json", _curseurs.DerniereErreur));
            _apresEtapeFlush?.Invoke("curseurs");
            return false;
        }
        _apresEtapeFlush?.Invoke("curseurs");

        Volatile.Write(ref _derniereErreur, null);
        return true;
    }

    private void RetenirErreur(string brique, string? detail)
    {
        if (DerniereErreur is null) Volatile.Write(ref _derniereErreur, Prefixer(brique, detail));
    }

    private static string Prefixer(string brique, string? detail) => brique + " : " + (detail ?? "erreur non décrite");

    private void Signaler(PhaseReconstruction phase, string? erreur)
    {
        Volatile.Write(ref _derniereErreur, erreur);
        Volatile.Write(ref _phase, (int)phase);
        Changement?.Invoke(this, EventArgs.Empty);
    }

    private static TimeSpan? Duree(long ticks) => ticks >= 0 ? TimeSpan.FromTicks(ticks) : null;

    private static TimeSpan TempsCpuProcessus()
    {
        using var p = Process.GetCurrentProcess();
        return p.TotalProcessorTime;
    }

    private static string Decrire(Exception ex) => ex.GetType().Name + " : " + ex.Message;
}
