using System.Windows.Threading;
using Chronos.Models;
using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;
using Chronos.Services;
using Chronos.Services.Historique;
using Chronos.Services.Historique.Tokens;
using Chronos.Text;
using Chronos.Theming;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chronos.ViewModels.Historique;

/// <summary>
/// HIS-08 / HIS-01 / HIS-07 — le SEUL ViewModel de la fenêtre Historique (D-34-05) : un point de marshaling, une persistance, un
/// abonnement à la reconstruction. Les vues (34-06 / 34-07) sont des <c>UserControl</c> sans logique bindés sur
/// <c>DonneesSemaine</c> / <c>DonneesJour</c> (records immuables) et sur les dérivés calculés ici.
///
/// <para><b>Ce qu'il fait</b> : calcule la période (semaine de forfait depuis le repère hebdo du journal — D-34-13 —, repli
/// <c>WeeklyAnchor</c>, puis calendrier ; jour local), lit HORS du thread UI par <c>ISourceHistorique</c> (<c>Task.Run</c>),
/// applique par <c>IUiDispatcher.Post</c> la lecture la plus RÉCENTE seulement (numéro de requête), porte le style persisté,
/// la ligne de fraîcheur, l'alerte « journal muet » (D-32-21), le bandeau F2 coalescé (Pattern 6 bis), les annotations d'honnêteté
/// (mots de <c>TextesHistorique</c> uniquement) et la géométrie de fenêtre.</para>
///
/// <para><b>JAMAIS de tick 1 s ici</b> : la fenêtre ne reçoit rien du cadran ; 60 s suffisent à « il y a N min ». Le tick ne touche
/// que les textes de fraîcheur, « maintenant » et le bandeau ; les pistes ne voient une NOUVELLE référence de données que sur une
/// lecture appliquée (HIS-07), et une lecture n'est demandée au tick que si un magasin a bougé (dernière écriture du journal,
/// dernière reconstruction terminée). Le <c>DispatcherTimer</c> n'est créé que par <c>DemarrerHorloge</c>, jamais dans
/// le constructeur (les tests sont en <c>[Fact]</c> simple).</para>
/// </summary>
public sealed partial class HistoriqueViewModel : ObservableObject
{
    private readonly ISourceHistorique _source;
    private readonly IUiDispatcher _ui;
    private readonly IClock _clock;
    private readonly TimeZoneInfo _tz;
    private readonly IReglagesHistorique _reglages;
    private readonly IEtatJournal? _journal;
    private readonly IEtatReconstruction? _reconstruction;
    private readonly DateTimeOffset _demarrage;
    private readonly List<Task> _lecturesEnVol = new();
    private readonly object _verrouLectures = new();

    private DateTimeOffset? _repere;
    private DateTimeOffset? _ancre;
    private int _decalage;                          // 0 = au présent ; −n = n unités en arrière
    private int _numeroLecture;                      // seule la lecture qui porte le dernier numéro est appliquée
    private DateTimeOffset? _derniereEcritureVue;    // état des magasins au moment de la dernière demande de lecture
    private DateTimeOffset? _reconstructionVue;
    private DateTimeOffset? _journalOuvertLe;        // mémorisé à la première lecture (sous-texte F2)
    private int _f2EnAttente;                        // 0 = aucun Post F2 en vol
    private DispatcherTimer? _timer;

    public HistoriqueViewModel(ISourceHistorique source, IUiDispatcher ui, IClock clock, TimeZoneInfo tz, IReglagesHistorique reglages,
                               IEtatJournal? journal = null, IEtatReconstruction? reconstruction = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _tz = tz ?? throw new ArgumentNullException(nameof(tz));
        _reglages = reglages ?? throw new ArgumentNullException(nameof(reglages));
        _journal = journal;
        _reconstruction = reconstruction;
        _demarrage = clock.UtcNow;
        _maintenant = _demarrage;

        var lus = reglages.Lire();
        _ancre = lus.WeeklyAnchor;
        _style = lus.HistoriqueStyleSemaine;
        Theme = ThemeCatalog.ByKey(lus.ThemeKey);   // lu une fois à l'ouverture (rampe des pistes)

        if (_reconstruction is not null) _reconstruction.Changement += SurChangementReconstruction;
    }

    // ------------------------------------------------------------------ Vue / période

    [ObservableProperty] private VueHistorique _vueActive = VueHistorique.Semaine;
    public bool IsVueJour => VueActive == VueHistorique.Jour;
    public bool IsVueSemaine => VueActive == VueHistorique.Semaine;
    partial void OnVueActiveChanged(VueHistorique value)
    {
        OnPropertyChanged(nameof(IsVueJour));
        OnPropertyChanged(nameof(IsVueSemaine));
        OnPropertyChanged(nameof(TexteRetourPresent));
    }

    [ObservableProperty] private Plage? _plageCourante;
    [ObservableProperty] private string _libellePeriode = "";
    [ObservableProperty] private bool _estAuPresent = true;

    /// <summary>« Cette semaine » / « Aujourd'hui » selon la vue.</summary>
    public string TexteRetourPresent => IsVueJour ? TextesHistorique.Aujourdhui : TextesHistorique.CetteSemaine;

    /// <summary>Levé par <c>FermerCommand</c> : la fenêtre se ferme (et persiste sa géométrie) ; le VM ne connaît pas la fenêtre.</summary>
    public event EventHandler? FermetureDemandee;

    /// <summary>Segment Jour / Semaine ; « 4 semaines » est un no-op tant que la vue n'existe pas (phase 35, infobulle « bientôt »).</summary>
    [RelayCommand]
    private void ChoisirVue(VueHistorique vue)
    {
        if (vue == VueHistorique.QuatreSemaines || vue == VueActive) return;
        VueActive = vue;
        AllerAuPresent();
    }

    /// <summary>‹ : l'unité de la vue en arrière (D-34-13 : la borne courante sert de repère — une borne est un reset observé).</summary>
    [RelayCommand]
    private void Precedent()
    {
        if (PlageCourante is not { } p) return;
        PlageCourante = VueActive == VueHistorique.Jour
            ? BornesPlage.Jour(p.Debut.AddTicks(-1), _tz)
            : BornesPlage.SemaineDeForfait(p.Debut.AddTicks(-1), p.Debut, _ancre, _tz);
        _decalage--;
        EstAuPresent = false;
        ApresChangementDePlage();
    }

    private bool PeutAvancer() => !EstAuPresent;

    /// <summary>› : l'unité de la vue en avant, jamais au-delà du présent.</summary>
    [RelayCommand(CanExecute = nameof(PeutAvancer))]
    private void Suivant()
    {
        if (PlageCourante is not { } p || EstAuPresent) return;
        PlageCourante = VueActive == VueHistorique.Jour
            ? BornesPlage.Jour(p.Fin, _tz)
            : BornesPlage.SemaineDeForfait(p.Fin, p.Fin, _ancre, _tz);
        _decalage++;
        EstAuPresent = _decalage == 0;
        ApresChangementDePlage();
    }

    [RelayCommand]
    private void RetourPresent() => AllerAuPresent();

    [RelayCommand]
    private void Fermer() => FermetureDemandee?.Invoke(this, EventArgs.Empty);

    // ------------------------------------------------------------------ Style (persisté)

    [ObservableProperty] private HistoriqueStyleSemaine _style;
    public bool IsStylePistes => Style == HistoriqueStyleSemaine.Pistes;
    public bool IsStyleSimplifie => Style == HistoriqueStyleSemaine.Simplifie;
    public bool IsStyleTuiles => Style == HistoriqueStyleSemaine.Tuiles;
    partial void OnStyleChanged(HistoriqueStyleSemaine value)
    {
        OnPropertyChanged(nameof(IsStylePistes));
        OnPropertyChanged(nameof(IsStyleSimplifie));
        OnPropertyChanged(nameof(IsStyleTuiles));
    }

    /// <summary>Sélecteur « Style : Pistes · Simplifié · Tuiles » — persisté par <c>Save(mutation(Load()))</c> (D-34-15).</summary>
    [RelayCommand]
    private void ChoisirStyle(HistoriqueStyleSemaine style)
    {
        Style = style;
        _reglages.Modifier(x => x with { HistoriqueStyleSemaine = style });
    }

    // ------------------------------------------------------------------ Données (références immuables)

    [ObservableProperty] private DonneesSemaine? _donneesSemaine;
    [ObservableProperty] private DonneesJour? _donneesJour;

    // Dérivés des données : recalculés dans Appliquer*, JAMAIS au tick.
    [ObservableProperty] private IReadOnlyList<GraduationLibellee> _libellesJours = Array.Empty<GraduationLibellee>();
    [ObservableProperty] private IReadOnlyList<GraduationLibellee> _libellesHeures = Array.Empty<GraduationLibellee>();
    [ObservableProperty] private IReadOnlyList<AnnotationHistorique> _annotationsTrous = Array.Empty<AnnotationHistorique>();
    [ObservableProperty] private IReadOnlyList<AnnotationHistorique> _annotationsSauts = Array.Empty<AnnotationHistorique>();
    [ObservableProperty] private IReadOnlyList<AnnotationHistorique> _annotationsResets = Array.Empty<AnnotationHistorique>();
    [ObservableProperty] private IReadOnlyList<AnnotationHistorique> _annotationsEpuisee = Array.Empty<AnnotationHistorique>();
    [ObservableProperty] private IReadOnlyList<AnnotationHistorique> _annotationsDivergences = Array.Empty<AnnotationHistorique>();
    [ObservableProperty] private AnnotationHistorique? _annotationJournalOuvert;
    [ObservableProperty] private bool _afficherPiedDivergence;
    [ObservableProperty] private string _libellePermanentTokens = TextesHistorique.LibellePermanentTokens(TextesHistorique.GrainHeure);
    [ObservableProperty] private string _legendeModeles = "";
    [ObservableProperty] private string _echelleTokens = TextesHistorique.EchelleTokens(0);
    [ObservableProperty] private long _plafondTokens;

    public string TextePiedDivergence => TextesHistorique.PiedDivergence;
    public string LegendeTokens => TextesHistorique.LegendeTokens;
    public string EchelleRythme => TextesHistorique.EchelleRythme;
    public string PiedDePage => TextesHistorique.PiedDePage;

    // ------------------------------------------------------------------ Fraîcheur (au tick)

    [ObservableProperty] private string _texteFraicheur = "";
    [ObservableProperty] private bool _alerteJournal;
    [ObservableProperty] private string _texteAlerteJournal = "";
    [ObservableProperty] private DateTimeOffset _maintenant;
    [ObservableProperty] private bool _afficherMaintenant;

    // ------------------------------------------------------------------ Bandeau F2

    [ObservableProperty] private bool _afficherBandeauF2;
    [ObservableProperty] private string _texteBandeauF2 = "";
    [ObservableProperty] private string _sousTexteBandeauF2 = "";
    [ObservableProperty] private double _fractionBandeauF2;

    // ------------------------------------------------------------------ Thème, fuseau

    /// <summary>Le thème actif à l'ouverture (rampe des pistes : même loi que le cadran).</summary>
    public ChronosTheme Theme { get; }

    /// <summary>Le fuseau injecté (infobulle et graduations en heure locale).</summary>
    public TimeZoneInfo Fuseau => _tz;

    // ------------------------------------------------------------------ Cycle de vie

    /// <summary>
    /// À l'ouverture de la fenêtre : repère hebdo (lu SYNCHRONEMENT — sept jours de journal ≈ 435 Ko, quelques dizaines de ms —,
    /// nécessaire avant de connaître la plage), ancre, plage au présent, première lecture hors UI, bandeau.
    /// </summary>
    public void Ouvrir()
    {
        var now = _clock.UtcNow;
        _repere = _source.RepereHebdo(now);
        _ancre = _reglages.Lire().WeeklyAnchor;
        AllerAuPresent();
        MajBandeauF2();
    }

    /// <summary>Crée le timer de 60 s (côté UI uniquement, jamais dans le ctor). Idempotent.</summary>
    public void DemarrerHorloge()
    {
        if (_timer is not null) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += (_, _) => Tick(_clock.UtcNow);
        _timer.Start();
    }

    /// <summary>Arrête le timer et se désabonne de la reconstruction (fermeture de la fenêtre).</summary>
    public void ArreterHorloge()
    {
        _timer?.Stop();
        _timer = null;
        if (_reconstruction is not null) _reconstruction.Changement -= SurChangementReconstruction;
    }

    /// <summary>
    /// Le tick de 60 s : « maintenant », fraîcheur, alerte, bandeau — et UNE demande de lecture si, et seulement si, un magasin a
    /// bougé depuis la dernière demande. Aucune nouvelle instance de données ici (HIS-07).
    /// </summary>
    internal void Tick(DateTimeOffset now)
    {
        Maintenant = now;
        MajMaintenant();
        MajFraicheur(now);
        MajBandeauF2();
        if (_journal?.DerniereEcriture != _derniereEcritureVue || _reconstruction?.DerniereReconstructionTerminee != _reconstructionVue)
            DemanderLecture();
    }

    /// <summary>Attend toutes les lectures en vol (tests).</summary>
    internal Task AttendreLecture()
    {
        Task[] copie;
        lock (_verrouLectures) copie = _lecturesEnVol.ToArray();
        return Task.WhenAll(copie);
    }

    // ------------------------------------------------------------------ Géométrie de fenêtre

    /// <summary>La géométrie mémorisée (DIU) ; <c>null</c> = défaut (920 × 610 centré).</summary>
    public (double? X, double? Y, double? Largeur, double? Hauteur) GeometriePersistee()
    {
        var s = _reglages.Lire();
        return (s.HistoriqueX, s.HistoriqueY, s.HistoriqueWidth, s.HistoriqueHeight);
    }

    /// <summary>Mémorise la géométrie (à appeler en <c>WindowState.Normal</c> seulement).</summary>
    public void EnregistrerGeometrie(double x, double y, double largeur, double hauteur)
        => _reglages.Modifier(s => s with { HistoriqueX = x, HistoriqueY = y, HistoriqueWidth = largeur, HistoriqueHeight = hauteur });

    // ------------------------------------------------------------------ Navigation interne

    private void AllerAuPresent()
    {
        var now = _clock.UtcNow;
        _decalage = 0;
        PlageCourante = VueActive == VueHistorique.Jour
            ? BornesPlage.Jour(now, _tz)
            : BornesPlage.SemaineDeForfait(now, _repere, _ancre, _tz);
        EstAuPresent = true;
        ApresChangementDePlage();
    }

    private void ApresChangementDePlage()
    {
        if (PlageCourante is not { } p) return;
        LibellePeriode = VueActive == VueHistorique.Jour
            ? TextesHistorique.LibellePeriodeJour(p, SemaineDe(p.Debut), _tz)
            : TextesHistorique.LibellePeriodeSemaine(p, _tz);
        SuivantCommand.NotifyCanExecuteChanged();
        MajMaintenant();
        DemanderLecture();
    }

    // La semaine de forfait qui contient un instant, alignée sur le repère observé (ou l'ancre, ou le calendrier).
    private Plage SemaineDe(DateTimeOffset instant) => BornesPlage.SemaineDeForfait(instant, _repere, _ancre, _tz);

    private void MajMaintenant() => AfficherMaintenant = IsVueJour && EstAuPresent;

    // ------------------------------------------------------------------ Lecture hors UI, numérotée

    private void DemanderLecture()
    {
        if (PlageCourante is not { } plage) return;
        var numero = Interlocked.Increment(ref _numeroLecture);
        var vue = VueActive;
        var now = _clock.UtcNow;
        _derniereEcritureVue = _journal?.DerniereEcriture;
        _reconstructionVue = _reconstruction?.DerniereReconstructionTerminee;

        var tache = Task.Run(() =>
        {
            try
            {
                if (vue == VueHistorique.Jour)
                {
                    var d = _source.LireJour(plage, now);
                    _ui.Post(() => { if (numero == Volatile.Read(ref _numeroLecture)) AppliquerJour(d); });
                }
                else
                {
                    var precedente = BornesPlage.SemaineDeForfait(plage.Debut.AddTicks(-1), plage.Debut, _ancre, _tz);
                    var d = _source.LireSemaine(plage, precedente, now);
                    _ui.Post(() => { if (numero == Volatile.Read(ref _numeroLecture)) AppliquerSemaine(d); });
                }
            }
            catch (Exception ex)
            {
                // La façade ne lève jamais ; ceci n'attrape qu'une panne inattendue : le dire, ne pas planter la fenêtre.
                _ui.Post(() => { if (numero == Volatile.Read(ref _numeroLecture)) TexteFraicheur = TextesHistorique.HistoriqueIndisponible + " — " + ex.GetType().Name; });
            }
        });

        lock (_verrouLectures)
        {
            _lecturesEnVol.RemoveAll(t => t.IsCompleted);
            _lecturesEnVol.Add(tache);
        }
    }

    private void AppliquerSemaine(DonneesSemaine d)
    {
        DonneesSemaine = d;
        _journalOuvertLe = d.JournalOuvertLe;

        LibellesJours = GraduationsCalendrier.Jours(d.Plage, _tz)
            .Select(i => new GraduationLibellee(i, TextesHistorique.LibelleJour(i, _tz))).ToList();
        AnnotationsTrous = Trous(d.Analyse, d.LueA);
        AnnotationsSauts = Sauts(d.Analyse, WindowKind.SevenDay);
        AnnotationsResets = Array.Empty<AnnotationHistorique>();
        AnnotationsEpuisee = Array.Empty<AnnotationHistorique>();
        AnnotationsDivergences = d.Divergences
            .Select(x => new AnnotationHistorique(TypeAnnotation.Divergence, x.Debut, x.Fin, ""))
            .ToList();
        AnnotationJournalOuvert = JournalOuvert(d.JournalOuvertLe, d.Plage);
        AfficherPiedDivergence = d.Divergences.Count > 0;

        LibellePermanentTokens = TextesHistorique.LibellePermanentTokens(TextesHistorique.GrainHeure);
        LegendeModeles = "";   // en Semaine, la piste empile principal / sous-agents, pas les modèles
        PlafondTokens = PlafondJoli(d.Barres.Count == 0 ? 0 : d.Barres.Max(b => b.Principal.Out + b.SousAgents.Out));
        EchelleTokens = TextesHistorique.EchelleTokens(PlafondTokens);

        MajFraicheur(_clock.UtcNow);
        MajBandeauF2();
    }

    private void AppliquerJour(DonneesJour d)
    {
        DonneesJour = d;
        _journalOuvertLe = d.JournalOuvertLe;

        LibellesHeures = GraduationsCalendrier.Heures(d.Plage, _tz, 3)
            .Select(h => new GraduationLibellee(h, TextesHistorique.LibelleHeure(h, _tz))).ToList();
        AnnotationsTrous = Trous(d.Analyse, d.LueA);
        AnnotationsSauts = Sauts(d.Analyse, WindowKind.FiveHour);
        AnnotationsResets = d.Analyse.Resets5h
            .Where(r => d.Plage.Contient(r.Instant))
            .Select(r => new AnnotationHistorique(TypeAnnotation.Reset5h, r.Instant, null, TextesHistorique.Reset5h(r.Instant, _tz)))
            .ToList();
        AnnotationsEpuisee = Epuisees(d.Analyse.Serie, d.Plage);
        AnnotationsDivergences = Array.Empty<AnnotationHistorique>();
        AnnotationJournalOuvert = JournalOuvert(d.JournalOuvertLe, d.Plage);
        AfficherPiedDivergence = false;

        LibellePermanentTokens = TextesHistorique.LibellePermanentTokens(TextesHistorique.GrainQuartDHeure);
        var parModele = d.Colonnes.SelectMany(c => c.ParModele)
            .GroupBy(p => p.Model, StringComparer.Ordinal)
            .Select(g => (Model: g.Key, Out: g.Sum(p => p.Totaux.Out)))
            .OrderByDescending(x => x.Out).ThenBy(x => x.Model, StringComparer.Ordinal)
            .Select(x => x.Model)
            .ToList();
        LegendeModeles = TextesHistorique.LegendeModeles(parModele);
        PlafondTokens = PlafondJoli(d.Colonnes.Count == 0 ? 0 : d.Colonnes.Max(c => c.ParModele.Sum(p => p.Totaux.Out)));
        EchelleTokens = TextesHistorique.EchelleTokens(PlafondTokens);

        MajFraicheur(_clock.UtcNow);
        MajBandeauF2();
    }

    // ------------------------------------------------------------------ Annotations (mots de TextesHistorique)

    private IReadOnlyList<AnnotationHistorique> Trous(AnalyseJournal a, DateTimeOffset lueA)
        => a.Trous.Select(t => new AnnotationHistorique(TypeAnnotation.Trou, t.Debut, t.Fin ?? lueA, TextesHistorique.Trou(t), t.Cause)).ToList();

    private static IReadOnlyList<AnnotationHistorique> Sauts(AnalyseJournal a, WindowKind fenetre)
        => a.Sauts.Where(s => s.Fenetre == fenetre)
            .Select(s => new AnnotationHistorique(TypeAnnotation.Saut, s.Trou.Debut, s.Trou.Fin, TextesHistorique.Saut(s)))
            .ToList();

    private AnnotationHistorique? JournalOuvert(DateTimeOffset? journalOuvertLe, Plage plage)
        => journalOuvertLe is { } j && plage.Contient(j)
            ? new AnnotationHistorique(TypeAnnotation.JournalOuvert, j, null, TextesHistorique.JournalOuvertLe(j, _tz))
            : null;

    // Plateaux « épuisée » : suites de relevés consécutifs à 100 % ou refusés ; la fin est le dernier relevé + une cadence, bornée à la plage.
    private static IReadOnlyList<AnnotationHistorique> Epuisees(IReadOnlyList<ReleveJournal> serie, Plage plage)
    {
        var cadence = RateLimitHeaderUsageProvider.CadenceNominale;
        var plateaux = new List<AnnotationHistorique>();
        DateTimeOffset? debut = null;
        DateTimeOffset dernier = default;
        foreach (var r in serie)
        {
            var epuise = r.U5 >= 1.0 || r.Statut5 == StatutServeur.Rejete;
            if (epuise)
            {
                debut ??= r.T;
                dernier = r.T;
            }
            else if (debut is { } d)
            {
                plateaux.Add(Plateau(d, dernier, cadence, plage));
                debut = null;
            }
        }
        if (debut is { } reste) plateaux.Add(Plateau(reste, dernier, cadence, plage));
        return plateaux;
    }

    private static AnnotationHistorique Plateau(DateTimeOffset debut, DateTimeOffset dernier, TimeSpan cadence, Plage plage)
    {
        var fin = dernier + cadence;
        if (fin > plage.Fin) fin = plage.Fin;
        return new AnnotationHistorique(TypeAnnotation.Epuisee, debut, fin, TextesHistorique.Epuisee);
    }

    // Plafond « joli » de l'axe des tokens de sortie (mantisses 1 / 1,2 / 1,5 / 2 / 2,5 / 3 / 4 / 5 / 6 / 8 / 10 × 10^n). Copie locale
    // minimale : à remplacer par EchelleValeur.MaxArrondi (34-02) lors du branchement des vues (34-06).
    private static long PlafondJoli(long max)
    {
        if (max <= 0) return 0;
        var puissance = Math.Pow(10, Math.Floor(Math.Log10(max)));
        foreach (var m in new[] { 1.0, 1.2, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10 })
        {
            var candidat = (long)Math.Round(m * puissance);
            if (candidat >= max) return candidat;
        }
        return max;
    }

    // ------------------------------------------------------------------ Fraîcheur et alerte D-32-21

    private void MajFraicheur(DateTimeOffset now)
    {
        TexteFraicheur = IsVueJour
            ? (DonneesJour is { } j ? TextesHistorique.LigneFraicheurJour(j.Plage, j.Analyse, RateLimitHeaderUsageProvider.CadenceNominale, _tz) : "")
            : (DonneesSemaine is { } s ? TextesHistorique.LigneFraicheurSemaine(s.Analyse, now, _tz) : "");

        if (_journal is null)
        {
            AlerteJournal = false;
            TexteAlerteJournal = "";
            return;
        }

        // « muet » se mesure depuis max(démarrage, dernière écriture) : l'écriture de la veille n'allume pas l'alerte au lancement.
        var derniere = _journal.DerniereEcriture;
        var reference = derniere is { } d && d > _demarrage ? d : _demarrage;
        var age = now - reference;
        AlerteJournal = age > JournalReleves.SeuilMuet;
        TexteAlerteJournal = AlerteJournal ? TextesHistorique.AlerteJournalMuet(age) : "";
    }

    // ------------------------------------------------------------------ Bandeau F2 (Pattern 6 bis : coalescence)

    // Levé SUR LE THREAD DE FOND, ≈ une fois par fichier : un seul Post en vol à la fois, les rafales sont coalescées.
    private void SurChangementReconstruction(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _f2EnAttente, 1) == 1) return;
        _ui.Post(() =>
        {
            Volatile.Write(ref _f2EnAttente, 0);
            MajBandeauF2();
        });
    }

    private void MajBandeauF2()
    {
        if (_reconstruction is not { } r)
        {
            AfficherBandeauF2 = false;
            TexteBandeauF2 = "";
            SousTexteBandeauF2 = "";
            FractionBandeauF2 = 0;
            return;
        }
        AfficherBandeauF2 = r.Phase == PhaseReconstruction.Reconstruction;
        TexteBandeauF2 = TextesHistorique.BandeauF2(r.FichiersTraites, r.FichiersTotal, r.SemaineCouranteDisponible);
        FractionBandeauF2 = r.FichiersTotal > 0 ? (double)r.FichiersTraites / r.FichiersTotal : 0;
        SousTexteBandeauF2 = TextesHistorique.SousTexteF2(_journalOuvertLe, _tz);
    }
}
