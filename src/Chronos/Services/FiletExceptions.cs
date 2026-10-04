using System.Text;

namespace Chronos.Services;

/// <summary>
/// FIAB-1 (phase 42.2) — la partie PURE du filet global d'exceptions installé par <c>App</c> : savoir si une exception est
/// fatale (on la laisse terminer le processus) et la décrire en UNE ligne pour <see cref="JournalIncidents"/>.
///
/// Pourquoi : sous .NET 8, une exception UI non gérée termine le processus sans boîte de dialogue ni ligne de journal — le
/// cadran disparaît sans trace. Le filet journalise tout, et ne marque « traitée » que ce qui n'a pas corrompu le processus.
///
/// Type NEUTRE : aucun type WPF ; testable sans monter l'application.
/// </summary>
public static class FiletExceptions
{
    private const int CausesMax = 3;
    private const int CadresMax = 6;

    /// <summary>
    /// Vrai pour les exceptions après lesquelles l'état du processus n'est plus fiable (mémoire épuisée, accès mémoire
    /// invalide, pile débordée, code invalide) : les avaler laisserait un overlay à moitié mort. Tout le reste est non fatal.
    /// </summary>
    public static bool EstFatale(Exception e) => e is OutOfMemoryException
                                                 or AccessViolationException
                                                 or StackOverflowException
                                                 or InvalidProgramException
                                                 or BadImageFormatException;

    /// <summary>
    /// « exception non gérée (origine) : Type: message ← Cause: message … | pile : cadre ; cadre … » — MONOLIGNE, au plus
    /// 3 causes internes et 6 cadres de pile. Ne lève jamais (une exception sans pile, ou dont le message lève, est acceptée).
    /// </summary>
    public static string Decrire(string origine, Exception e)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append("exception non gérée (").Append(origine).Append(") : ").Append(Resume(e));

            var cause = e.InnerException;
            for (var i = 0; cause is not null && i < CausesMax; i++, cause = cause.InnerException)
                sb.Append(" ← ").Append(Resume(cause));

            sb.Append(" | pile : ").Append(Pile(e));
            return Monoligne(sb.ToString());
        }
        catch
        {
            return $"exception non gérée ({origine}) : description impossible";
        }
    }

    private static string Resume(Exception e)
    {
        string message;
        try { message = e.Message; }
        catch { message = "(message illisible)"; }
        return e.GetType().Name + ": " + message;
    }

    private static string Pile(Exception e)
    {
        string? pile;
        try { pile = e.StackTrace; }
        catch { pile = null; }
        if (string.IsNullOrWhiteSpace(pile)) return "(indisponible)";

        var cadres = pile.Split('\n')
                         .Select(l => l.Trim())
                         .Where(l => l.Length > 0)
                         .Take(CadresMax);
        return string.Join(" ; ", cadres);
    }

    private static string Monoligne(string texte)
        => texte.Replace("\r\n", " ⏎ ").Replace("\n", " ⏎ ").Replace("\r", " ⏎ ");
}
