using System;
using System.Globalization;
using System.Windows.Data;
using Chronos.Models.Historique;
using Chronos.Rendering.Historique;

namespace Chronos.Converters;

/// <summary>
/// HIS-02 / HIS-07 — <c>[DateTimeOffset instant, Plage plage, double largeur]</c> → l'abscisse de l'instant sur une piste de
/// cette largeur, BORNÉE à <c>[0, largeur]</c> : une annotation (trou, saut, reset, « journal ouvert le ») se pose en
/// <c>Canvas.Left</c> sans une ligne de C# dans la vue. Entrée invalide (<c>UnsetValue</c>, <c>null</c>, type inattendu,
/// largeur nulle) → 0.
/// </summary>
public sealed class InstantVersXConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not { Length: >= 3 } || values[0] is not DateTimeOffset instant || values[1] is not Plage plage
            || values[2] is not double largeur || double.IsNaN(largeur) || largeur <= 0)
            return 0.0;
        return Math.Clamp(EchelleTemps.X(instant, plage, largeur), 0.0, largeur);
    }

    public object[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Une abscisse ne redevient pas un instant par binding.");
}

/// <summary>
/// <c>[DateTimeOffset debut, DateTimeOffset? fin, Plage plage, double largeur, (optionnel) DateTimeOffset finSiNull]</c> →
/// la largeur en pixels de l'intervalle, ≥ 0, entre les abscisses BORNÉES à la piste (cohérent avec
/// <see cref="InstantVersXConverter"/>). Une fin absente prend la cinquième valeur (« maintenant » pour un trou ouvert) ;
/// sans elle, 0. Entrée invalide → 0.
/// </summary>
public sealed class LargeurIntervalleConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not { Length: >= 4 } || values[0] is not DateTimeOffset debut || values[2] is not Plage plage
            || values[3] is not double largeur || double.IsNaN(largeur) || largeur <= 0)
            return 0.0;

        DateTimeOffset fin;
        if (values[1] is DateTimeOffset finPosee) fin = finPosee;
        else if (values.Length >= 5 && values[4] is DateTimeOffset finSiNull) fin = finSiNull;
        else return 0.0;

        var x0 = Math.Clamp(EchelleTemps.X(debut, plage, largeur), 0.0, largeur);
        var x1 = Math.Clamp(EchelleTemps.X(fin, plage, largeur), 0.0, largeur);
        return Math.Max(0.0, x1 - x0);
    }

    public object[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Une largeur ne redevient pas un intervalle par binding.");
}

/// <summary>
/// 35-04 (D-35-17) — <c>[double x, double largeurInfobulle, double largeurCanvas]</c> → <c>clamp(x, 0, max(0, W − w))</c> :
/// l'infobulle du réticule reste posée sur le relevé survolé, sauf au bord droit où elle recule juste assez pour rester DANS sa
/// piste (jamais à gauche de la piste). Tolérant comme les autres : <c>x</c> invalide (<c>UnsetValue</c>, <c>null</c>, <c>NaN</c>) → 0 ;
/// largeurs pas encore mesurées (infobulle repliée, canvas non arrangé) → l'abscisse brute bornée à gauche, corrigée à la passe
/// de mise en page suivante.
/// </summary>
public sealed class BorneInfobulleConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not { Length: >= 3 } || values[0] is not double x || double.IsNaN(x))
            return 0.0;
        if (values[1] is not double w || values[2] is not double largeur || double.IsNaN(w) || double.IsNaN(largeur) || largeur <= 0)
            return Math.Max(0.0, x);
        return Math.Clamp(x, 0.0, Math.Max(0.0, largeur - w));
    }

    public object[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Une abscisse d'infobulle ne redevient pas un relevé par binding.");
}
