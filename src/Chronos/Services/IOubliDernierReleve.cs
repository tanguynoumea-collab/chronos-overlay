namespace Chronos.Services;

/// <summary>
/// DS2-03 / D-03 — effacement du DERNIER RELEVÉ EXACT persisté (last-exact.json).
/// </summary>
public interface IOubliDernierReleve
{
    /// <summary>Oublie le dernier relevé exact à l'instant <paramref name="instant"/>.</summary>
    void OublierDernierReleve(DateTimeOffset instant);
}
