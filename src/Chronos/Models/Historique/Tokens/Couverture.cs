namespace Chronos.Models.Historique.Tokens;

/// <summary>
/// TOK-04 — ce que les agrégats de tokens GARANTISSENT pour une tranche, dans les mots du plan de design (§4).
/// Jamais un « zéro » implicite : une tranche sans ligne n'est « zéro activité » que si elle est <see cref="Couverte"/>.
/// </summary>
public enum EtatCouverture
{
    /// <summary>« hors couverture » — avant le plus vieux transcript jamais vu par Chronos : rien ne peut être dit.</summary>
    HorsCouverture,

    /// <summary>« transcripts absents » — période où Claude Code a pu purger des fichiers (cleanupPeriodDays) AVANT que
    /// Chronos ne les lise ; présence PARTIELLE possible (juillet 2026 : 468 tuples survivants dans des fichiers d'août
    /// et septembre). Un arrêt de Chronos de plus de l'horizon de purge crée aussi une telle zone : c'est vrai, c'est dit.</summary>
    TranscriptsAbsents,

    /// <summary>« couverte » — tout transcript de la période existait encore et a été lu : l'absence de tranche est une
    /// vraie absence d'activité.</summary>
    Couverte,
}

/// <summary>Un intervalle <c>[Debut, Fin[</c> pendant lequel les agrégats sont garantis complets (borne de fin exclue).
/// Produit par <c>CouvertureTokens.GarantirPasse</c> : <c>[débutPasse − HorizonPurge, finPasse[</c>, fusionné aux voisins.</summary>
/// <param name="Debut">Borne incluse.</param>
/// <param name="Fin">Borne EXCLUE.</param>
public sealed record IntervalleGaranti(DateTimeOffset Debut, DateTimeOffset Fin);
