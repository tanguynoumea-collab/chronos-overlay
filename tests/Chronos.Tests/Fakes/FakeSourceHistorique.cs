using Chronos.Models.Historique;
using Chronos.Services.Historique;
using Chronos.ViewModels.Historique;

namespace Chronos.Tests;

/// <summary>
/// Faux d'<see cref="ISourceHistorique"/> pour les tests du ViewModel : enveloppe la semaine de référence en mémoire
/// (<see cref="SourceHistoriqueDemonstration"/>), COMPTE les lectures (la preuve « jamais au tick » de HIS-07), laisse
/// régler le repère hebdo (repli sur l'ancre), transformer les données rendues (retirer les divergences) et ORDONNER deux
/// lectures par une barrière (la preuve « une lecture périmée n'écrase pas la plus récente »). Aucun fichier.
/// </summary>
internal sealed class FakeSourceHistorique : ISourceHistorique
{
    private readonly SourceHistoriqueDemonstration _interne;
    private int _lectures;

    public FakeSourceHistorique(TimeZoneInfo tz) => _interne = new SourceHistoriqueDemonstration(tz);

    /// <summary>Nombre d'appels à <see cref="LireSemaine"/> + <see cref="LireJour"/> (thread-safe).</summary>
    public int Lectures => Volatile.Read(ref _lectures);

    /// <summary>Le repère hebdo rendu ; <c>null</c> = aucun relevé récent (le VM se replie sur l'ancre puis le calendrier).</summary>
    public DateTimeOffset? Repere { get; set; } = ScenariosHistorique.RepereHebdo;

    /// <summary>Transformation appliquée aux données Semaine avant de les rendre (ex. : retirer les divergences).</summary>
    public Func<DonneesSemaine, DonneesSemaine>? TransformerSemaine { get; set; }

    /// <summary>Si non nulle, <see cref="LireSemaine"/> attend le signal AVANT de rendre : permet de faire courir deux lectures.</summary>
    public ManualResetEventSlim? Barriere { get; set; }

    public DateTimeOffset? RepereHebdo(DateTimeOffset now) => Repere;

    public DonneesSemaine LireSemaine(Plage semaine, Plage precedente, DateTimeOffset now)
    {
        Interlocked.Increment(ref _lectures);
        Barriere?.Wait();
        var d = _interne.LireSemaine(semaine, precedente, now);
        return TransformerSemaine is { } f ? f(d) : d;
    }

    public DonneesJour LireJour(Plage jour, DateTimeOffset now)
    {
        Interlocked.Increment(ref _lectures);
        Barriere?.Wait();
        return _interne.LireJour(jour, now);
    }
}
