using Chronos.Models;
using Chronos.Services;

namespace Chronos.Tests;

/// <summary>Faux d'<see cref="IEtatServeur"/> : canal latéral de la sonde d'en-têtes, entièrement en
/// mémoire, avec déclencheur manuel de transition. Forme reprise de <see cref="FakeAuthStatus"/> —
/// propriétés réglables AVANT construction du consommateur, et aucune entrée/sortie.
///
/// Aucune requête, aucun en-tête réel : ce faux sert à prouver ce que le rapport de diagnostic ÉCRIT,
/// jamais ce que le serveur envoie.</summary>
public sealed class FakeEtatServeur : IEtatServeur
{
    public EtatDepassement? Depassement { get; set; }

    public ResultatSonde DernierResultat { get; set; } = ResultatSonde.JamaisSondee;

    public IReadOnlyList<string> NomsEnTetesRecus { get; set; } = Array.Empty<string>();

    public event EventHandler<EtatDepassement?>? DepassementChange;

    /// <summary>Provoque une transition observable (comme le ferait la sonde).</summary>
    public void Declencher(EtatDepassement? d)
    {
        Depassement = d;
        DepassementChange?.Invoke(this, d);
    }
}
