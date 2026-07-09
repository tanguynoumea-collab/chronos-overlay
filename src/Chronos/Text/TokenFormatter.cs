namespace Chronos.Text;

/// <summary>
/// Formatage FR pur et abrégé d'un compte de tokens (NET-02). Aucune dépendance WPF, aucun I/O.
/// Abréviation M/k à 1 décimale, virgule française, décimale « ,0 » supprimée. Déterministe
/// (indépendant de la culture machine, comme <see cref="CountdownFormatter"/>). Type neutre.
/// </summary>
public static class TokenFormatter
{
    public static string Format(long tokens)
        => "STUB"; // implémenté en phase GREEN
}
