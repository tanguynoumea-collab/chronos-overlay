using System.Collections.Generic;
using System.Linq;

namespace Chronos.Services;

/// <summary>
/// La FORME que le widget de sessions donne à ce que le moniteur rend : l'ordre des lignes, le libellé
/// d'état, le libellé d'ancienneté. Couche NEUTRE (aucun type WPF) parce qu'il y a désormais DEUX
/// consommateurs : le widget et le rapport de diagnostic.
///
/// <para>OBS-01 demande que l'utilisateur puisse comparer LIGNE À LIGNE le rapport et le widget, sans
/// constater le moindre écart. Deux mises en forme jumelles satisferaient ce critère le jour de leur
/// écriture et dériveraient au premier changement — c'est exactement la faute que cette phase corrige,
/// une octave plus bas. Il n'y a donc qu'un producteur, et il est ici.</para>
///
/// <para>Ce qui reste dans le ViewModel : la COULEUR et les drapeaux d'état. Ce sont des types WPF, ils
/// ne peuvent pas descendre dans cette couche, et un rapport texte n'en a aucun besoin.</para>
///
/// <para>À NE PAS CONFONDRE avec le formateur d'ancienneté d'un RELEVÉ D'USAGE (« il y a 3 h 12 »,
/// « il y a 2 j ») : un quota et une session Claude Code ne se lisent pas au même grain, et les deux
/// échelles ont été réglées séparément.</para>
/// </summary>
public static class AffichageSessions
{
    /// <summary>Ordre du widget et du rapport : ce qui réclame une intervention d'abord, déduction comprise,
    /// puis le plus récent (LIB-04).</summary>
    public static IReadOnlyList<SessionSnapshot> Ordonner(IEnumerable<SessionSnapshot> sessions)
        => sessions.OrderBy(s => Urgence(s.Activity)).ThenByDescending(s => s.UpdatedAt).ToList();

    /// <summary>ORDRE D'ÉCRAN, et seulement lui (LIB-04) : widget et rapport de diagnostic le lisent. Une attente,
    /// même déduite, passe devant un travail — c'est ce qui la rend visible. L'arbitrage entre sources NE le lit
    /// PAS : il a son propre rang, figé (ArbitrageSessions.RangArbitrage, réserve R4 de l'audit v1.6).</summary>
    public static int Urgence(SessionActivity a) => a switch
    {
        SessionActivity.WaitingAttention => 0,   // permission, demande du bus, question posée
        SessionActivity.WaitingTurn => 1,
        SessionActivity.WaitingDeduced => 2,     // passe DEVANT le travail (LIB-04)
        SessionActivity.Working => 3,
        _ => 4,                                  // Unknown : aucune ligne à l'écran ; dernier au diagnostic
    };

    /// <summary>Libellé d'état, tel qu'affiché par le widget. « à toi » et « tour fini » sont les deux
    /// attentes OBSERVÉES, distinguées par les mots et non par deux oranges ; « à toi ? déduit » est la
    /// troisième, et elle n'a été observée par personne — le mot et l'interrogation sont là pour ça.
    /// <para>Aucun de ces libellés ne dépasse seize caractères : huit gabarits les affichent sur une
    /// ligne, et la garde est tenue par un test, pas par cette phrase.</para></summary>
    public static string Etat(SessionActivity a) => a switch
    {
        SessionActivity.WaitingAttention => "à toi",
        SessionActivity.WaitingTurn => "tour fini",
        SessionActivity.Working => "en cours",
        // L'interrogation et le mot ne sont pas décoratifs : ils sont ce qui distingue une déduction d'une
        // observation. « tour fini » et « à toi » restent réservés à ce qui a été réellement observé.
        SessionActivity.WaitingDeduced => "à toi ? déduit",
        _ => "inconnu",
    };

    /// <summary>Ancienneté d'une session, telle qu'affichée par le widget.</summary>
    public static string Age(System.TimeSpan d)
    {
        if (d < System.TimeSpan.FromSeconds(60)) return "à l'instant";
        if (d < System.TimeSpan.FromHours(1)) return $"il y a {(int)d.TotalMinutes} min";
        return $"il y a {(int)d.TotalHours} h";
    }

    /// <summary>Un ÉCART d'âge entre deux signaux, et non une ancienneté. « il y a 7 h » situe un instant,
    /// « 7 h » mesure une distance : confondre les deux rendrait un désaccord illisible, alors que c'est
    /// précisément l'écart qui dit à l'utilisateur qu'une de ses sources est FIGÉE. Zéro se dit « 0 s » et
    /// non « à l'instant » — c'est le cas d'égalité d'âge, il doit se voir.</summary>
    public static string Ecart(System.TimeSpan d)
    {
        if (d < System.TimeSpan.FromSeconds(60)) return $"{(int)d.TotalSeconds} s";
        if (d < System.TimeSpan.FromHours(1)) return $"{(int)d.TotalMinutes} min";
        return $"{(int)d.TotalHours} h";
    }
}
