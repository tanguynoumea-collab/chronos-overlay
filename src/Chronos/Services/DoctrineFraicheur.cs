using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// SQUELETTE (étape RED). La doctrine réelle arrive au commit suivant. Compilable délibérément :
/// tests/Chronos.Tests référence Chronos, donc un commit non compilable rendrait « dotnet test »
/// non invocable dans son intégralité (précédent 18-01 et 19-01).
/// </summary>
public static class DoctrineFraicheur
{
    /// <summary>Âge maximal d'un relevé exact affiché SANS vérification d'activité (EXA-02).</summary>
    public static readonly TimeSpan LimiteAge =
        RateLimitHeaderUsageProvider.CadenceNominale + TimeSpan.FromSeconds(60);

    public static TimeSpan? Age(WindowState w, DateTimeOffset now)
        => throw new NotImplementedException();

    public static bool ABesoinDuJournal(WindowState vivante, WindowState? memorisee, DateTimeOffset now)
        => throw new NotImplementedException();

    public static WindowState Statuer(WindowState vivante, WindowState? memorisee,
                                      TranscriptActivityLog? journal, DateTimeOffset now)
        => throw new NotImplementedException();
}
