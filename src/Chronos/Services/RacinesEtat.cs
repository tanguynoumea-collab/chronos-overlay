using System.IO;

namespace Chronos.Services;

/// <summary>Les racines CANDIDATES, dans l'ordre où on les essaie (APP-06).</summary>
public sealed record RacinesCandidates(IReadOnlyList<string> EtatsHooks, IReadOnlyList<string> SessionsAppBureau);

/// <summary>Squelette (RED) — le comportement arrive au commit suivant.</summary>
public static class RacinesEtat
{
    public static RacinesCandidates Candidats(string localAppData, string appData)
        => throw new System.NotImplementedException();

    public static RacinesCandidates ParDefaut()
        => Candidats(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                     System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData));

    public static string? PremiereExistante(IReadOnlyList<string> candidats)
        => throw new System.NotImplementedException();
}
