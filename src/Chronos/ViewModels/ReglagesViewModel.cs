using System.Globalization;
using Chronos.Services;
using Chronos.Services.Historique;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Chronos.ViewModels;

/// <summary>État du panneau « Diagnostic » des réglages (DESIGN_PLAN_REGLAGES §5) : rien encore, génération, rapport prêt, échec.</summary>
public enum EtatDiagnostic { Vide, EnCours, Pret, Echec }

/// <summary>Une entrée du rail de navigation : la section, son glyphe, son libellé et son raccourci (Ctrl+<see cref="Numero"/>).</summary>
public sealed record EntreeRailReglages(SectionReglages Section, string Glyphe, string Libelle, int Numero);

/// <summary>
/// Quick 260927-reglages-v2 — ce que la fenêtre de réglages sait d'elle-même, testable sans WPF : la SECTION courante (rail,
/// ↑ / ↓, Ctrl+1…6, persistée dans <c>settings.json</c> et relue à l'ouverture), la géométrie mémorisée, et le panneau
/// « Diagnostic » (génération asynchrone, erreur lisible, copie). Les réglages eux-mêmes (thème, sonde, widget…) restent sur le
/// <see cref="MainViewModel"/> partagé avec le cadran : cette classe n'en duplique aucun.
///
/// <para>Persistance par <see cref="IReglagesHistorique"/> (motif GAP-1 : <c>Save(mutation(Load()))</c>) : l'enveloppe des
/// réglages de la fenêtre Historique sert telle quelle — la galerie et les tests passent une version en mémoire, rien n'écrit le
/// vrai <c>settings.json</c>.</para>
/// </summary>
public sealed partial class ReglagesViewModel : ObservableObject
{
    /// <summary>Texte affiché pendant la génération du rapport (§5, colonne « Chargement »).</summary>
    public const string TexteGeneration = "Génération du rapport…";

    /// <summary>Préfixe de l'erreur de génération (§5, colonne « Erreur ») : suivi de la cause.</summary>
    public const string PrefixeEchec = "Le diagnostic a échoué : ";

    /// <summary>Retour discret après « ⧉ Copier ».</summary>
    public const string TexteCopie = "copié dans le presse-papiers";

    /// <summary>Retour quand le presse-papiers est tenu par une autre application.</summary>
    public const string TexteCopieImpossible = "presse-papiers indisponible, réessaie";

    private readonly IReglagesHistorique _reglages;
    private readonly Func<Task<string>> _genererDiagnostic;
    private readonly IClock _clock;
    private readonly IPressePapiers? _pressePapiers;
    private readonly TimeZoneInfo _fuseau;
    private bool _relecture;   // vrai pendant Ouvrir() : relire la section ne la réécrit pas

    /// <summary>Les six sections du rail, dans l'ordre du plan (§3) : l'index + 1 est le chiffre de Ctrl+n.</summary>
    public IReadOnlyList<EntreeRailReglages> Entrees { get; } = new[]
    {
        new EntreeRailReglages(SectionReglages.Donnees, "◉", "Données", 1),
        new EntreeRailReglages(SectionReglages.Historique, "◷", "Historique", 2),
        new EntreeRailReglages(SectionReglages.Apparence, "◐", "Apparence", 3),
        new EntreeRailReglages(SectionReglages.Sessions, "☰", "Sessions", 4),
        new EntreeRailReglages(SectionReglages.Comportement, "◈", "Comportement", 5),
        new EntreeRailReglages(SectionReglages.Diagnostic, "▤", "Diagnostic", 6),
    };

    /// <summary>Aperçu vivant du widget de sessions (section « Sessions ») : données d'échantillon partagées avec la galerie.</summary>
    public SessionsPreviewViewModel ApercuSessions { get; } = new();

    [ObservableProperty] private SectionReglages _section;

    public bool IsDonnees => Section == SectionReglages.Donnees;
    public bool IsHistorique => Section == SectionReglages.Historique;
    public bool IsApparence => Section == SectionReglages.Apparence;
    public bool IsSessions => Section == SectionReglages.Sessions;
    public bool IsComportement => Section == SectionReglages.Comportement;
    public bool IsDiagnostic => Section == SectionReglages.Diagnostic;

    /// <summary>Les cinq sections « formulaire » partagent la colonne qui défile ; le diagnostic occupe toute la hauteur.</summary>
    public bool IsSectionDefilante => !IsDiagnostic;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiagnosticEnCours), nameof(DiagnosticPret), nameof(DiagnosticEnEchec))]
    [NotifyCanExecuteChangedFor(nameof(CopierDiagnosticCommand))]
    private EtatDiagnostic _etatDiagnostic;

    public bool DiagnosticEnCours => EtatDiagnostic == EtatDiagnostic.EnCours;
    public bool DiagnosticPret => EtatDiagnostic == EtatDiagnostic.Pret;
    public bool DiagnosticEnEchec => EtatDiagnostic == EtatDiagnostic.Echec;

    /// <summary>Le rapport complet (zone mono, lecture seule, sélectionnable). Vide tant qu'aucun rapport n'a abouti.</summary>
    [ObservableProperty] private string _texteDiagnostic = "";

    /// <summary>« Généré à HH:MM · N lignes » (fuseau local), sous le rapport.</summary>
    [ObservableProperty] private string _ligneDiagnostic = "";

    /// <summary>« Le diagnostic a échoué : &lt;cause&gt; » — jamais une boîte de message.</summary>
    [ObservableProperty] private string _erreurDiagnostic = "";

    /// <summary>Retour de « ⧉ Copier » (vide tant qu'on n'a rien copié depuis la dernière génération).</summary>
    [ObservableProperty] private string _retourCopie = "";

    public ReglagesViewModel(IReglagesHistorique reglages, Func<Task<string>> genererDiagnostic, IClock clock,
                             IPressePapiers? pressePapiers = null, TimeZoneInfo? fuseau = null)
    {
        _reglages = reglages ?? throw new ArgumentNullException(nameof(reglages));
        _genererDiagnostic = genererDiagnostic ?? throw new ArgumentNullException(nameof(genererDiagnostic));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _pressePapiers = pressePapiers;
        _fuseau = fuseau ?? TimeZoneInfo.Local;

    }

    /// <summary>À l'ouverture de la fenêtre : relit la dernière section persistée (sans la réécrire), et lance le diagnostic si
    /// la fenêtre rouvre directement sur lui sans rapport en main.</summary>
    public void Ouvrir()
    {
        // RED : squelette
    }

    private SectionReglages LireSection()
    {
        var s = _reglages.Lire().ReglagesSection;
        return Enum.IsDefined(s) ? s : SectionReglages.Donnees;
    }

    partial void OnSectionChanged(SectionReglages value)
    {
        OnPropertyChanged(nameof(IsDonnees));
        OnPropertyChanged(nameof(IsHistorique));
        OnPropertyChanged(nameof(IsApparence));
        OnPropertyChanged(nameof(IsSessions));
        OnPropertyChanged(nameof(IsComportement));
        OnPropertyChanged(nameof(IsDiagnostic));
        OnPropertyChanged(nameof(IsSectionDefilante));

    }

    /// <summary>Clic sur une entrée du rail.</summary>
    [RelayCommand]
    private void AllerA(SectionReglages section)
    {

    }

    /// <summary>Ctrl+<paramref name="numero"/> : la n-ième entrée du rail (1 à 6) ; tout autre chiffre est ignoré.</summary>
    [RelayCommand]
    private void AllerAuNumero(int numero)
    {

    }

    // ------------------------------------------------------------------ Géométrie (motif HistoriqueViewModel)

    /// <summary>Position et taille mémorisées (DIU) ; un champ absent → la fenêtre garde son défaut.</summary>
    public (double? X, double? Y, double? Largeur, double? Hauteur) GeometriePersistee()
    {
        return (null, null, null, null);
    }

    /// <summary>Mémorise la géométrie (à appeler en <c>WindowState.Normal</c> seulement).</summary>
    public void EnregistrerGeometrie(double x, double y, double largeur, double hauteur)
    {
    }

    // ------------------------------------------------------------------ Diagnostic (§5 : jamais sur le thread UI, jamais de MessageBox)

    private void LancerDiagnosticSiVide()
    {
        if (IsDiagnostic && EtatDiagnostic == EtatDiagnostic.Vide && ActualiserDiagnosticCommand.CanExecute(null))
            _ = ActualiserDiagnosticCommand.ExecuteAsync(null);
    }

    /// <summary>« ↻ Actualiser » / « Réessayer » : régénère le rapport. La génération elle-même est déléguée (le
    /// <see cref="MainViewModel"/> la pousse sur le pool) ; une erreur devient une phrase, jamais une exception qui remonte.</summary>
    [RelayCommand]
    private Task ActualiserDiagnostic() => Task.CompletedTask;

    private string Ligne(string rapport)
    {
        var lignes = rapport.Length == 0 ? 0 : rapport.TrimEnd('\r', '\n').Split('\n').Length;
        var heure = TimeZoneInfo.ConvertTime(_clock.UtcNow, _fuseau).ToString("HH:mm", CultureInfo.InvariantCulture);
        return $"Généré à {heure} · {lignes} {(lignes > 1 ? "lignes" : "ligne")}";
    }

    /// <summary>« ⧉ Copier » : le rapport entier dans le presse-papiers (disponible seulement quand un rapport est prêt).</summary>
    [RelayCommand(CanExecute = nameof(DiagnosticPret))]
    private void CopierDiagnostic()
    {

    }
}
