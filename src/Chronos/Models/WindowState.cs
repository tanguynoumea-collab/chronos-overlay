namespace Chronos.Models;

/// <summary>État immuable d'UNE fenêtre (5 h ou hebdo). null = inconnu (jamais inventé).</summary>
public sealed record WindowState
{
    public required WindowKind Kind { get; init; }
    public double? Utilization { get; init; }            // 0..1 ; null si inconnu (repli sans plafond)
    public DateTimeOffset? ResetsAt { get; init; }        // null si inconnu (repli JSONL)
    public double? FractionTimeRemaining { get; init; }   // 0..1 clampé ; null si ResetsAt inconnu
    public long? EstimatedTokens { get; init; }           // somme brute (repli) ; info honnête
    public required SourceReliability Reliability { get; init; }

    /// <summary>Instant de capture de CETTE fenêtre par la source qui l'a produite. null = inconnu.
    /// Par fenêtre et non par snapshot : le composite choisit la meilleure source PAR FENÊTRE, les
    /// deux peuvent donc venir de sources différentes à des instants différents.</summary>
    public DateTimeOffset? CapturedAt { get; init; }

    /// <summary>HDR-03 — statut déclaré par le SERVEUR pour CETTE fenêtre. null = non rapporté (JAMAIS
    /// inventé, jamais déduit d'un pourcentage local). Porté par WindowState et NON par UsageSnapshot :
    /// CompositeUsageProvider.GetAsync reconstruit le snapshot par « new UsageSnapshot { … } » et non par
    /// « with », donc tout champ de snapshot est détruit au passage — et ce fichier est interdit d'édition
    /// jusqu'à la phase 19. Best() rend en revanche l'instance de WindowState PAR RÉFÉRENCE : ce champ
    /// voyage gratuitement à travers toute la chaîne de composites.</summary>
    public StatutServeur? StatutServeur { get; init; }

    /// <summary>HDR-04 — usage en DÉPASSEMENT rapporté avec cette fenêtre. null = aucun dépassement
    /// rapporté. Doublé par le canal latéral IEtatServeur : quand la forme overage est SEULE, les deux
    /// fenêtres sont Unavailable et Best() peut retenir l'instance d'une autre source Exact, ce qui
    /// écarterait ce champ. Un champ et un canal, parce qu'aucun des deux ne suffit seul.</summary>
    public EtatDepassement? Depassement { get; init; }

    /// <summary>Épuisé si utilization connue >= 1. Inconnu (null) != épuisé.</summary>
    public bool Exhausted => Utilization is >= 1.0;

    /// <summary>Fenêtre indisponible : conserve la WindowKind, tout le reste à l'inconnu.</summary>
    public static WindowState Unavailable(WindowKind k) =>
        new() { Kind = k, Reliability = SourceReliability.Unavailable };

    /// <summary>Fraction de temps restante clampée [0..1] ; null si reset inconnu ou fenêtre non positive.</summary>
    public static double? FractionRemaining(DateTimeOffset? resetsAt, DateTimeOffset now, TimeSpan windowLength)
    {
        if (resetsAt is null || windowLength <= TimeSpan.Zero) return null;
        var ratio = (resetsAt.Value - now) / windowLength; // TimeSpan / TimeSpan = double
        return Math.Clamp(ratio, 0.0, 1.0);
    }
}
