using System.Collections.Generic;

namespace Chronos.Services;

/// <summary>D'OÙ vient un signal de session. L'ORDRE DE DÉCLARATION EST SIGNIFIANT : il sert de
/// départage à ÂGE ÉGAL, et seulement là (voir <see cref="ArbitrageSessions"/>).</summary>
public enum SourceSession
{
    /// <summary>Fichier d'état écrit par un hook. SPÉCIFIQUE : lui seul sait dire « une permission est
    /// demandée ». C'est ce qui le départage d'un transcript de MÊME âge — jamais ce qui le hisse
    /// au-dessus d'un signal plus récent.</summary>
    Hook,

    /// <summary>Transcript JSONL. Universel et continu, mais muet sur la permission.</summary>
    Transcript,
}

/// <summary>Ce qu'UNE source dit d'UNE session, à un instant qu'elle porte elle-même
/// (<see cref="SessionSnapshot.UpdatedAt"/>). Un signal n'est pas un verdict : c'est une déposition.</summary>
public sealed record SignalSession(SourceSession Source, SessionSnapshot Session);

/// <summary>
/// FUS-02 — deux sources ont parlé de la même session et ne disent PAS la même chose. Ce n'est ni une
/// erreur ni un masquage : la session est bel et bien affichée, c'est l'un de ses signaux qui a perdu.
///
/// <para>Le relevé du 2026-09-12 est l'exemple type : un fichier de hook figé depuis 7 h annonçait
/// « à toi » pendant qu'un transcript de 10 s prouvait le contraire. Le désaccord existait, il gagnait,
/// et rien ne le disait. <see cref="EcartAge"/> est la grandeur qui rend une source FIGÉE
/// diagnosticable par l'utilisateur seul.</para>
/// </summary>
public sealed record DesaccordSources(
    string SessionId,
    SourceSession SourceRetenue, SessionActivity EtatRetenu,
    SourceSession SourceEcartee, SessionActivity EtatEcarte,
    System.TimeSpan EcartAge);

/// <summary>Ce que l'arbitrage retient, et ce qu'il a écarté en le DISANT.</summary>
public sealed record ResultatArbitrage(
    IReadOnlyList<SessionSnapshot> Retenus,
    IReadOnlyList<DesaccordSources> Desaccords);

/// <summary>
/// FUS-01 — quand deux sources parlent de la même session, c'est la plus RÉCENTE qui gagne.
///
/// <para>SQUELETTE (étape ROUGE) : le contrat est posé, la décision ne l'est pas encore.</para>
/// </summary>
public static class ArbitrageSessions
{
    /// <summary>Tranche entre les signaux, session par session. Rend les retenus ET les désaccords.</summary>
    public static ResultatArbitrage Trancher(IEnumerable<SignalSession> signaux)
        => throw new System.NotImplementedException();
}
