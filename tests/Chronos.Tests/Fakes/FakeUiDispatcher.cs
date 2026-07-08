using Chronos.Services;

namespace Chronos.Tests;

/// <summary>Fake IUiDispatcher : compte les Post et exécute l'action inline (assertion directe).
/// Partagé avec le plan 04-02 (tests du marshaling VM, RAF-04). Namespace Chronos.Tests pour
/// rester cohérent avec FakeClock (référencé sans using superflu).</summary>
internal sealed class FakeUiDispatcher : IUiDispatcher
{
    public int PostCount { get; private set; }
    public bool OnUiThread { get; set; }               // simule le thread courant
    public bool CheckAccess() => OnUiThread;
    public void Post(Action a) { PostCount++; a(); }   // exécute inline pour l'assertion
}
