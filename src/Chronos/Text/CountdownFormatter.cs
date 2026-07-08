namespace Chronos.Text;

/// <summary>
/// Formatage FR pur d'un compte à rebours (aucune dépendance WPF, aucun I/O, aucune CultureInfo).
/// Littéraux français fixes ; minutes sur 2 chiffres au-delà de l'heure. Le temps écoulé (≤ 0)
/// est rendu « 0 min » (jamais de valeur négative). Type neutre, hautement testable.
/// </summary>
public static class CountdownFormatter
{
    public static string Format(TimeSpan reste)
    {
        if (reste <= TimeSpan.Zero) return "0 min";
        if (reste.TotalDays >= 1) return $"{(int)reste.TotalDays} j {reste.Hours} h";
        if (reste.TotalHours >= 1) return $"{(int)reste.TotalHours} h {reste.Minutes:D2}";
        return $"{(int)reste.TotalMinutes} min";
    }
}
