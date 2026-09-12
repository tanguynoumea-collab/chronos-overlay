namespace Chronos.Models;

// SQUELETTE D'ÉTAPE RED — la forme compile, la logique n'existe pas encore.
public sealed record EtatDepassement
{
    public double? Utilization { get; init; }
    public DateTimeOffset? ResetsAt { get; init; }
    public StatutServeur? Statut { get; init; }
    public bool EstRenseigne => false;
}
