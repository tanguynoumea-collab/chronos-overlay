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
