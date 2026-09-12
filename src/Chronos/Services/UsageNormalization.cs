using System.Globalization;

namespace Chronos.Services;

/// <summary>
/// SQUELETTE (etape RED) — le POINT UNIQUE de conversion d'unite d'usage (HDR-05).
/// Implemente a l'etape GREEN.
/// </summary>
public static class UsageNormalization
{
    public static readonly DateTimeOffset PlancherEpoch = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static double? FractionDepuisFraction(double? f0a1) => throw new NotImplementedException();

    public static double? FractionDepuisPourcentage(double? p0a100) => throw new NotImplementedException();

    public static double? FractionDepuisTexteFraction(string? valeur) => throw new NotImplementedException();

    public static DateTimeOffset? InstantDepuisEpochSecondes(long? secondes) => throw new NotImplementedException();

    public static DateTimeOffset? InstantDepuisEpochMillisecondes(long? millisecondes) => throw new NotImplementedException();

    public static DateTimeOffset? InstantDepuisTexteEpoch(string? valeur) => throw new NotImplementedException();

    public static DateTimeOffset? InstantDepuisIso(string? valeur) => throw new NotImplementedException();

    public static string PourcentagePourAffichage(double? fraction0a1) => throw new NotImplementedException();
}
