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
/// <c>[double fraction 0..1, double largeur]</c> → <c>clamp(fraction) × largeur</c> : la barre de progression du bandeau F2
/// (<c>FractionBandeauF2</c>). <c>NaN</c>, négatif, <c>UnsetValue</c> ou largeur nulle → 0.
/// </summary>
public sealed class FractionVersLargeurConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not { Length: >= 2 } || values[0] is not double fraction || values[1] is not double largeur
            || double.IsNaN(fraction) || double.IsNaN(largeur) || largeur <= 0)
            return 0.0;
        return Math.Clamp(fraction, 0.0, 1.0) * largeur;
    }

    public object[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Une largeur ne redevient pas une fraction par binding.");
}
