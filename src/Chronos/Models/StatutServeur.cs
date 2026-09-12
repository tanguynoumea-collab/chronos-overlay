namespace Chronos.Models;

// SQUELETTE D'ÉTAPE RED — la forme compile, la logique n'existe pas encore.
public enum StatutServeur { Autorise, AutoriseAvertissement, Rejete, NonReconnu }

public static class StatutServeurTexte
{
    public static StatutServeur? DepuisEnTete(string? valeur) => throw new NotImplementedException();
}
