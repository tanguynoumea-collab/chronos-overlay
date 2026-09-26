using Chronos.Services;

namespace Chronos.Tests;

/// <summary>Sonde de premier plan RÉGLABLE entre deux cycles (LUE-02) : aucun appel Win32. Compte ses appels (OBS-01 : le
/// rapport ne la relit pas) et peut lever (LUE-04 : une sonde en panne ne casse rien).</summary>
internal sealed class FakePremierPlan : IPremierPlan
{
    /// <summary>L'état rendu au prochain appel.</summary>
    public EtatPremierPlan Etat { get; set; } = EtatPremierPlan.NonBranche;

    /// <summary>Vrai : l'appel lève (sonde en panne).</summary>
    public bool Leve { get; set; }

    /// <summary>Les instants reçus, un par appel.</summary>
    public List<DateTimeOffset> Appels { get; } = new();

    public EtatPremierPlan Lire(DateTimeOffset now)
    {
        Appels.Add(now);
        if (Leve) throw new InvalidOperationException("sonde de test en panne");
        return Etat;
    }

    /// <summary>Le premier plan est claude depuis <paramref name="depuis"/>.</summary>
    public static FakePremierPlan Claude(DateTimeOffset depuis) => new() { Etat = new(StatutPremierPlan.Claude, "claude", depuis) };

    /// <summary>Le premier plan est un autre processus, nommé.</summary>
    public static FakePremierPlan Autre(string processus) => new() { Etat = new(StatutPremierPlan.AutreProcessus, processus, null) };

    /// <summary>La sonde ne sait pas, et dit pourquoi.</summary>
    public static FakePremierPlan Indisponible(string raison) => new() { Etat = new(StatutPremierPlan.Indisponible, null, null, raison) };
}
