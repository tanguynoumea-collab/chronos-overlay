using System.Windows.Media;
using Chronos.Models;
using Chronos.Text;
using Chronos.Theming;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Chronos.ViewModels;

/// <summary>
/// Sous-VM d'UNE fenêtre d'usage (5 h ou hebdo). Mémorise le dernier <see cref="WindowState"/> immuable
/// et recalcule, à chaque interpolation, la fraction d'arc restante + le compte à rebours formaté FR.
/// L'interpolation est PURE (RAF-03) : elle ne lit que l'état mémorisé + l'instant fourni, jamais le disque.
/// Le XAML Phase 5 bindera un RingArc sur FractionRemaining/Utilization/Reliability.
/// </summary>
public sealed partial class WindowGaugeViewModel : ObservableObject
{
    private readonly TimeSpan _windowLength;
    private WindowState _state; // dernier snapshot de cette fenêtre (immuable)

    [ObservableProperty] private double _fractionRemaining;                    // 0..1 → longueur d'arc restante
    [ObservableProperty] private double _fractionElapsed;                       // 0..1 → longueur d'arc ÉCOULÉE (VIS-01)
    [ObservableProperty] private bool _hasTime;                                 // vrai SSI reset connu → les nouveaux styles
                                                                                // (temps = géométrie) ont de quoi dessiner ;
                                                                                // faux (chargement/pas de reset) → état « en attente »
    [ObservableProperty] private double? _utilization;                          // 0..1 ou null → couleur (Phase 5)
    [ObservableProperty] private string _utilizationText = "";                  // « 80 % » / « ≥ 80 % » / «» (VIS-05, DEL-04)
    [ObservableProperty] private bool _hasUtilizationText;                      // vrai SSI utilization connue (pilote le séparateur « · »)
    [ObservableProperty] private string _countdownText = "—";
    [ObservableProperty] private bool _exhausted;
    [ObservableProperty] private SourceReliability _reliability = SourceReliability.Unavailable;
    [ObservableProperty] private bool _estPlancher;                             // DEL-04 — le chiffre est une BORNE
                                                                                // INFÉRIEURE (« au moins X »), jamais une estimation
    /// <summary>EXA-03 — « ce chiffre a été relevé il y a un moment ». RAPPORTÉ par la doctrine, jamais
    /// recalculé : aucune comparaison d'horodatage ne vit dans ce ViewModel, et c'est la garantie
    /// STRUCTURELLE qu'un second seuil de « périmé » ne peut pas réapparaître à côté de LimiteAge.
    /// Frais → false. EncoreValide → true ET juste : la doctrine est ALLÉE VÉRIFIER qu'aucune réponse
    /// assistant n'est survenue depuis la capture. Provenance null (fenêtre née hors doctrine) → false :
    /// on ne salit pas un chiffre sur lequel la doctrine ne s'est pas prononcée.
    ///
    /// Indépendante de <see cref="EstPlancher"/> et COMPOSABLE avec elle : un plancher est aussi daté
    /// (la doctrine ne peut atteindre sa branche 3 qu'après avoir échoué le laissez-passer d'âge), mais un
    /// EncoreValide est daté SANS être un plancher — c'est un chiffre PROUVÉ juste.</summary>
    [ObservableProperty] private bool _estDate;

    [ObservableProperty] private string _tokensText = "";                       // « ≈ N M/k tokens » ; vide si masqué (NET-02)
    [ObservableProperty] private bool _hasTokens;                               // vrai SSI TokensDepuisReleve>0 (pilote la visibilité)

    /// <summary>EXA-06 — QUI alimente cette fenêtre, tel que nommé par le producteur lui-même ; <c>null</c>
    /// si personne ne l'alimente. Volontairement PAS observable : elle n'est bindée nulle part, elle est
    /// lue par <c>MainViewModel</c> pour composer l'infobulle du relevé.</summary>
    public SourceUsage? SourceDuReleve { get; private set; }

    /// <summary>EXA-03 — QUAND ce chiffre a été capturé ; <c>null</c> si la source ne le dit pas. Non
    /// observable pour la même raison que <see cref="SourceDuReleve"/>. Elle est TRANSPORTÉE telle quelle :
    /// la mettre en mots (« il y a 12 min ») appartient à <c>Chronos.Text.LibelleSource</c>, et la juger
    /// (« trop vieux ») appartient à la doctrine — jamais à ce ViewModel.</summary>
    public DateTimeOffset? InstantDuReleve { get; private set; }

    /// <summary>EXA-03 — ce que la doctrine a VÉRIFIÉ sur ce chiffre ; <c>null</c> si elle ne s'est pas
    /// prononcée. Non observable, même motif que les deux précédentes : sa mise en mots appartient à
    /// <c>Chronos.Text.LibelleSource</c>, et <see cref="EstDate"/> en est déjà le résumé bindable.</summary>
    public ProvenanceReleve? ProvenanceDuReleve { get; private set; }

    // HDR-03 — l'état déclaré par le SERVEUR pour CETTE fenêtre, en texte FR prêt à afficher.
    // Posées et testées ici pour que la phase 20 (EXA-03, distinction visuelle frais / daté /
    // indisponible) n'ait plus qu'à les binder : AUCUNE géométrie de cadran n'en dépend aujourd'hui.
    [ObservableProperty] private string _texteStatutServeur = "";   // HDR-03 — état RAPPORTÉ par le serveur
    [ObservableProperty] private bool _hasStatutServeur;            // pilote la visibilité (motif HasTokens)

    // Couleur de l'arc valeur calculée selon le THÈME courant (remplace le converter statique → switch live).
    [ObservableProperty] private Brush? _valueBrush;
    private ChronosTheme _theme = ThemeCatalog.Default;

    /// <summary>Applique un thème : recalcule la couleur de l'arc valeur pour l'utilization courante.</summary>
    public void SetTheme(ChronosTheme theme)
    {
        _theme = theme;
        ValueBrush = _theme.ArcBrush(Utilization);
    }

    // Recalcule l'arc à chaque changement d'utilization (rampe du thème courant).
    partial void OnUtilizationChanged(double? value) => ValueBrush = _theme.ArcBrush(value);

    public WindowGaugeViewModel(TimeSpan windowLength)
    {
        _windowLength = windowLength;
        _state = WindowState.Unavailable(default);
        ValueBrush = _theme.ArcBrush(null); // neutre au départ (aucune donnée)
    }

    /// <summary>Applique un nouvel état de fenêtre (thread UI). Met à jour provenance/utilization/épuisement.</summary>
    public void Apply(WindowState s)
    {
        _state = s;
        Utilization = s.Utilization;
        Exhausted = s.Exhausted;
        Reliability = s.Reliability;
        // DEL-04 — depuis la phase 19, SourceReliability.Estimated n'est plus produit qu'en UN SEUL
        // endroit de la production : la branche 3 de la doctrine, le plancher avec activité. Cette
        // propriété porte donc enfin le nom du fait qu'elle décrit — une BORNE INFÉRIEURE — là où
        // l'ancienne marque d'estimation désignait un concept que la phase 19 a supprimé.
        EstPlancher = s.Reliability == SourceReliability.Estimated;

        // EXA-03 — le fait est RECOPIÉ de la doctrine, pas redéduit : aucune soustraction d'horodatage,
        // aucune constante de durée. La seule limite d'âge du projet vit dans DoctrineFraicheur.LimiteAge.
        EstDate = s.Provenance is ProvenanceReleve.EncoreValide or ProvenanceReleve.PlancherAvecActivite;

        // EXA-06 — le COUPLE (qui, depuis quand) transporté tel quel jusqu'à l'infobulle.
        SourceDuReleve = s.Source;
        InstantDuReleve = s.CapturedAt;
        ProvenanceDuReleve = s.Provenance;

        // VIS-05 + DEL-04 : le préfixe est décidé par la PROVENANCE et non par la fiabilité — « ≥ » ne
        // doit apparaître que sur un plancher, jamais sur un exact encore valide (DEL-03), qui est un
        // chiffre juste et n'a rien à porter. «» si utilization null (jamais de plafond inventé).
        // HasUtilizationText pilote la visibilité du séparateur « · » côté XAML (même pattern que HasTokens).
        UtilizationText = PercentFormatter.Format(s.Utilization, s.Provenance);
        HasUtilizationText = s.Utilization is not null;

        // HDR-03 — l'état déclaré par le SERVEUR, pas un seuil déduit d'un pourcentage. Absent → rien
        // d'affiché (jamais « autorisé » par défaut : l'absence d'information n'est pas une bonne
        // nouvelle). La géométrie du cadran n'en dépend pas : la distinction visuelle est EXA-03, phase 20.
        TexteStatutServeur = LibelleStatut(s.StatutServeur);
        HasStatutServeur = s.StatutServeur is not null;

        // NET-02 + DEL-04 : la matière première brute est le compte de tokens observés DEPUIS le relevé,
        // et non la somme de l'estimation ABSOLUE supprimée en phase 16. Le champ qui la portait a
        // lui-même été supprimé en phase 20 : il rattachait au nouveau chiffre une sémantique que ce
        // milestone a tuée. Aucune conversion : ce compte est affiché tel quel, il ne devient JAMAIS un pourcentage
        // (les limites Anthropic pondèrent par modèle — tokens / plafond restera faux à jamais).
        // Seule une fenêtre corrigée par delta en porte un : aucun test de fiabilité n'est nécessaire.
        HasTokens = s.TokensDepuisReleve is > 0;
        TokensText = HasTokens ? TokenFormatter.Format(s.TokensDepuisReleve!.Value) : "";
    }

    /// <summary>
    /// Vocabulaire FR UNIQUE du statut serveur pour toute la couche présentation : <c>MainViewModel</c>
    /// réutilise ces textes déjà calculés plutôt que de refaire un second mapping — deux mappings
    /// divergeraient le jour où le vocabulaire du serveur bougera.
    ///
    /// <c>null</c> rend la chaîne VIDE, et surtout PAS « autorisé » : l'en-tête absent signifie que le
    /// serveur n'a rien dit, ce qui n'est pas une bonne nouvelle — seulement une absence de nouvelle.
    /// </summary>
    private static string LibelleStatut(StatutServeur? s) => s switch
    {
        StatutServeur.Autorise => "AUTORISÉ",
        StatutServeur.AutoriseAvertissement => "AUTORISÉ (avertissement)",
        StatutServeur.Rejete => "REJETÉ",
        StatutServeur.NonReconnu => "statut non reconnu",
        _ => "",
    };

    /// <summary>PUR, aucun I/O (RAF-03) : recalcule fraction d'arc + compte à rebours à l'instant <paramref name="now"/>.</summary>
    public void Interpolate(DateTimeOffset now)
    {
        var remaining = WindowState.FractionRemaining(_state.ResetsAt, now, _windowLength);
        FractionRemaining = remaining ?? 0.0;
        HasTime = _state.ResetsAt is not null;   // reset connu → géométrie fiable pour les nouveaux styles
        // VIS-01 : inversion du remplissage — l'arc est VIDE en début de fenêtre, PLEIN au reset.
        // Reset INCONNU (remaining null) → arc VIDE (0), jamais plein : on n'affiche pas un plein trompeur
        // quand on ne connaît pas le temps (countdown « — »).
        FractionElapsed = remaining is { } rem ? System.Math.Clamp(1.0 - rem, 0.0, 1.0) : 0.0;
        CountdownText = _state.ResetsAt is { } r
            ? CountdownFormatter.Format(r - now)
            : "—";
    }
}
