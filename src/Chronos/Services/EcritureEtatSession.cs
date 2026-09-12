namespace Chronos.Services;

/// <summary>SQUELETTE (étape ROUGE du plan 23-01) — le contrat existe, le comportement pas encore.</summary>
public sealed record ResultatEcritureEtat(bool Reussi, string? Cause)
{
    public static ResultatEcritureEtat Reussie { get; } = new(true, null);
    public static ResultatEcritureEtat Echouee(string cause) => new(false, cause);
}

/// <summary>SQUELETTE (étape ROUGE du plan 23-01). Voir le plan pour le comportement attendu.</summary>
public static class EcritureEtatSession
{
    public static ResultatEcritureEtat Appliquer(string dossier, SessionHookResult resultat)
        => throw new System.NotImplementedException();
}
