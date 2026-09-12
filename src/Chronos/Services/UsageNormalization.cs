using System.Globalization;

namespace Chronos.Services;

/// <summary>
/// HDR-05 — LE POINT UNIQUE de conversion d'unité d'usage du projet.
///
/// Le projet fait circuler la même donnée sous TROIS unités et <c>resets_at</c> sous DEUX formats :
/// <list type="bullet">
///   <item>en-têtes <c>anthropic-ratelimit-unified-*</c> : <c>utilization</c> en 0..1, en TEXTE,
///         <c>reset</c> en epoch SECONDES (texte) ;</item>
///   <item><c>GET /api/oauth/usage</c> : <c>utilization</c> en 0..100, <c>resets_at</c> en ISO 8601 ;</item>
///   <item>pont statusLine : <c>used_percentage</c> en 0..100, <c>resets_at</c> en epoch SECONDES.</item>
/// </list>
/// Chaque conversion dispersée est un point de divergence futur : toutes vivent ici, et un test de
/// balayage du texte source (<c>NormalisationUniqueTests</c>) interdit d'en réintroduire une ailleurs.
///
/// Classe PURE : aucune entrée/sortie, aucune horloge, aucun type WPF, aucune sérialisation. Elle ne
/// connaît que des nombres, des textes et des instants.
///
/// Doctrine de sortie, invariante : <c>null</c> signifie INCONNU. Jamais <c>0</c> — « 0 % de quota
/// consommé » est une affirmation, l'absence de donnée n'en est pas une.
/// </summary>
public static class UsageNormalization
{
    /// <summary>
    /// Plancher de sanité des instants de reset. Antérieur à l'existence des fenêtres d'usage Claude,
    /// donc aucun reset légitime ne peut tomber avant.
    ///
    /// Motif RÉEL de son existence : le <c>usage.json</c> de production de cette machine contient
    /// <c>{"five_hour":{"used_percentage":10,"resets_at":9}}</c>. L'epoch 9 — janvier 1970 — ne décrit
    /// rien, mais il était servi comme un instant valide et produisait une géométrie fausse sur le
    /// cadran. On rend désormais l'inconnu plutôt qu'un dessin faux.
    /// </summary>
    public static readonly DateTimeOffset PlancherEpoch = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// PORTE DE VALIDATION UNIQUE d'une fraction d'usage 0..1. Toutes les autres conversions de
    /// fraction passent par ici.
    ///
    /// Ne clampe JAMAIS vers le haut : <c>WindowState.Exhausted</c> teste <c>&gt;= 1.0</c>, et un
    /// dépassement réel (overage) doit rester visible — le ramener à 1,0 effacerait l'information la
    /// plus importante du cadran.
    ///
    /// Ne clampe JAMAIS vers le bas non plus : une valeur négative est un symptôme de source
    /// incohérente, pas un zéro. <c>null != 0</c> est la doctrine du projet.
    /// </summary>
    public static double? FractionDepuisFraction(double? f0a1)
    {
        if (f0a1 is not { } f) return null;
        if (double.IsNaN(f) || double.IsInfinity(f)) return null;
        return f < 0.0 ? null : f;
    }

    /// <summary>Pourcentage 0..100 (<c>/api/oauth/usage</c>, pont statusLine) vers fraction 0..1.</summary>
    public static double? FractionDepuisPourcentage(double? p0a100)
        => FractionDepuisFraction(p0a100 is { } v ? v / 100.0 : null);

    /// <summary>
    /// Valeur TEXTUELLE d'en-tête HTTP (« 0.63 », déjà en 0..1) vers fraction 0..1.
    ///
    /// PIÈGE VÉRIFIÉ EMPIRIQUEMENT sur la machine cible, sous <c>fr-FR</c> (et
    /// <c>InvariantGlobalization=false</c> est verrouillé par CLAUDE.md) :
    /// <c>double.Parse("0.63")</c> LÈVE une <c>FormatException</c>, et
    /// <c>double.TryParse("0.63", out v)</c> rend <c>false</c> — une perte SILENCIEUSE. La source
    /// paraîtrait simplement muette sur toute machine française.
    ///
    /// Le point décimal des en-têtes HTTP n'est pas négociable ; la culture de la machine, si. D'où le
    /// style de nombre flottant explicite combiné à la culture invariante : seule forme correcte.
    /// </summary>
    public static double? FractionDepuisTexteFraction(string? valeur)
        => double.TryParse(valeur?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f)
            ? FractionDepuisFraction(f)
            : null;

    /// <summary>
    /// Epoch SECONDES vers instant, avec le plancher de sanité <see cref="PlancherEpoch"/>.
    /// Hors bornes représentables ou antérieur au plancher : inconnu, jamais d'exception.
    /// </summary>
    public static DateTimeOffset? InstantDepuisEpochSecondes(long? secondes)
    {
        if (secondes is not { } s) return null;
        try
        {
            var t = DateTimeOffset.FromUnixTimeSeconds(s);
            return t < PlancherEpoch ? null : t;
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>
    /// Epoch MILLISECONDES vers instant, MÊME plancher de sanité.
    ///
    /// Cette surcharge existe pour que <c>capturedAt</c> du pont statusLine et les horodatages du
    /// rapport de diagnostic passent AUSSI par le point unique : sans elle, la garde de non-retour
    /// devrait exempter <c>DiagnosticService.cs</c>, et une garde trouée ne garde rien.
    /// </summary>
    public static DateTimeOffset? InstantDepuisEpochMillisecondes(long? millisecondes)
    {
        if (millisecondes is not { } ms) return null;
        try
        {
            var t = DateTimeOffset.FromUnixTimeMilliseconds(ms);
            return t < PlancherEpoch ? null : t;
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>
    /// Valeur TEXTUELLE d'en-tête HTTP portant un epoch SECONDES (« 1783180800 ») vers instant.
    /// Même raison d'être <c>InvariantCulture</c> que <see cref="FractionDepuisTexteFraction"/> :
    /// le séparateur de groupes de la culture courante ne doit jamais entrer dans l'interprétation.
    /// </summary>
    public static DateTimeOffset? InstantDepuisTexteEpoch(string? valeur)
        => long.TryParse(valeur?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)
            ? InstantDepuisEpochSecondes(s)
            : null;

    /// <summary>
    /// Texte ISO 8601 (<c>/api/oauth/usage</c> : offset explicite, parfois microsecondes) vers
    /// instant, avec le même plancher de sanité. <c>RoundtripKind</c> préserve l'offset porté par la
    /// chaîne au lieu de le réinterpréter dans le fuseau local.
    /// </summary>
    public static DateTimeOffset? InstantDepuisIso(string? valeur)
        => DateTimeOffset.TryParse(valeur, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)
            ? (d < PlancherEpoch ? null : d)
            : null;

    /// <summary>
    /// Fraction 0..1 vers libellé d'affichage (« 63 % »). Inconnu vers chaîne vide : on n'affiche pas
    /// un chiffre là où il n'y a pas de donnée.
    ///
    /// L'espace avant le « % » est une espace ORDINAIRE, reproduisant à l'identique la forme déjà
    /// écrite dans le rapport de diagnostic — aucune assertion de test existante ne change.
    /// </summary>
    public static string PourcentagePourAffichage(double? fraction0a1)
        => fraction0a1 is { } f ? (f * 100).ToString("F0", CultureInfo.InvariantCulture) + " %" : "";
}
