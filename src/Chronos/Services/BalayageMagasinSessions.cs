using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Chronos.Services;

/// <summary>
/// Ce qu'un balayage a RÉELLEMENT retiré, et ce qu'il a laissé.
/// </summary>
public sealed record BilanBalayage(int EtatsRetires, int TemporairesRetires, int EtatsConserves);

/// <summary>
/// SQUELETTE (étape ROUGE). Signatures réelles, comportement absent : la compilabilité à chaque commit
/// est non négociable, et un type absent ferait échouer TOUTE l'invocation de la suite au lieu des seuls
/// tests concernés.
/// </summary>
public sealed class BalayageMagasinSessions
{
    /// <summary>Au-delà, un état sans attestation de vie est balayé.</summary>
    public static readonly System.TimeSpan ExpirationEtat = System.TimeSpan.FromHours(72);

    /// <summary>En deçà, un fichier temporaire est épargné.</summary>
    public static readonly System.TimeSpan AgeMinimalTemporaire = System.TimeSpan.FromHours(1);

    private readonly string _dossier;
    private readonly ISessionSource _attestationDeVie;
    private readonly IClock _horloge;

    public BalayageMagasinSessions(string dossier, ISessionSource attestationDeVie, IClock horloge)
    {
        _dossier = dossier;
        _attestationDeVie = attestationDeVie;
        _horloge = horloge;
    }

    /// <summary>Le dossier balayé.</summary>
    public string Dossier => _dossier;

    public BilanBalayage Balayer()
    {
        _ = _attestationDeVie;
        _ = _horloge;
        throw new System.NotImplementedException();
    }
}
