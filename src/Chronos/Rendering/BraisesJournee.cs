using System;
using System.Collections.Generic;

namespace Chronos.Rendering;

/// <summary>État d'une braise de l'anneau journée.</summary>
public enum EtatBraise
{
    Cendre,
    DemiLueur,
    Pleine,
}

/// <summary>Squelette (RED, plan 43-09).</summary>
public static class BraisesJournee
{
    public const int Nombre = 24;

    public static IReadOnlyList<int>? Tranches(DateTimeOffset localNow, DateTimeOffset? localReset5h) => throw new NotImplementedException();
    public static IReadOnlyList<int> TaillesGroupes(DateTimeOffset localNow, DateTimeOffset? localReset5h) => throw new NotImplementedException();
    public static IReadOnlyList<double> Angles(DateTimeOffset localNow, DateTimeOffset? localReset5h) => throw new NotImplementedException();
    public static IReadOnlyList<EtatBraise> Etats(DateTimeOffset localNow) => throw new NotImplementedException();
}
