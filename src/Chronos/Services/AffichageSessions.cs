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
    /// <summary>Les TROIS mots du widget (LIB-01), verrouillés par l'utilisateur. Rien d'autre ne s'affiche.</summary>
    public const string Reflexion = "Réflexion";
    public const string EnAttente = "En attente";
    public const string EnAttenteDeduite = "En attente ?";   // espace ORDINAIRE avant « ? » ; le « ? » est obligatoire

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

    /// <summary>LE prédicat « est une attente », unique et nommé : tour fini, permission ou question, attente
    /// déduite. C'est le point d'entrée de la règle « lue » (phase 30). Le ViewModel le lit (plan 28-03) ; le
    /// détecteur de traitement garde le sien — une garde documentaire en lit le texte — et un test tient leur
    /// égalité sur les cinq états.</summary>
    public static bool EstUneAttente(SessionActivity a)
        => a is SessionActivity.WaitingAttention or SessionActivity.WaitingTurn or SessionActivity.WaitingDeduced;

    /// <summary>Une session a-t-elle une LIGNE dans le widget ? L'état indéterminé n'en a plus (LIB-01) : il est
    /// masqué par le moniteur, avec son motif, et le rapport de diagnostic le liste.</summary>
    public static bool AUneLigne(SessionActivity a) => a is not SessionActivity.Unknown;

    /// <summary>Libellé d'état, tel qu'affiché par le widget ET par le rapport de diagnostic (LIB-03). Trois
    /// mots, là où la v1.6 en avait cinq : « Réflexion » dit le travail observé ; « En attente » couvre les
    /// trois attentes OBSERVÉES — tour fini, permission demandée, question posée — sans les distinguer par le
    /// mot : c'est l'ORDRE (<see cref="Urgence"/>) qui porte l'urgence, et la couleur (rampe ambre) ;
    /// « En attente ? » est la seule attente DÉDUITE, et son point d'interrogation est ce qui la distingue
    /// d'une observation — il n'est pas décoratif.
    /// <para>L'état indéterminé n'a pas de ligne dans le widget (<see cref="AUneLigne"/>) : son mot n'est lu
    /// que par le rapport de diagnostic, qui le liste parmi les masquées.</para>
    /// <para>Aucun de ces libellés ne dépasse seize caractères : huit gabarits les affichent sur une
    /// ligne, et la garde est tenue par un test, pas par cette phrase.</para></summary>
    public static string Etat(SessionActivity a) => a switch
    {
        SessionActivity.WaitingAttention or SessionActivity.WaitingTurn => EnAttente,
        SessionActivity.WaitingDeduced => EnAttenteDeduite,
        SessionActivity.Working => Reflexion,
        _ => "indéterminé",   // lu par le DIAGNOSTIC seulement : Unknown n'a pas de ligne dans le widget
    };

    /// <summary>Le seul texte de l'application qui explique le widget, lu au moment de l'activer (réserve R9 de
    /// l'audit v1.6). Construit à partir des trois constantes : il ne peut plus dériver des mots de l'écran.</summary>
    public static string TexteActivation()
        => "Widget de sessions activé.\n\n"
         + "Il affiche tes sessions Claude Code actives, observées par leurs hooks et par leurs transcripts, en trois mots :\n"
         + $"  « {Reflexion} » — la session travaille ;\n"
         + $"  « {EnAttente} » — elle a fini son tour, demande une permission ou te pose une question ;\n"
         + $"  « {EnAttenteDeduite} » — elle travaillait et plus rien n'arrive : Chronos le déduit, il ne l'a pas vu.\n\n"
         + "Clic droit sur une session : la marquer traitée (elle revient si elle te redemande quelque chose) ou l'archiver.";

    /// <summary>Le nom que le widget ET le rapport affichent (APP-02) : le titre de l'app s'il est connu, sinon le
    /// dossier — un producteur, deux consommateurs. Un titre vide ou blanc n'est pas un titre : la ligne garde son
    /// dossier, jamais un nom vide.</summary>
    public static string Nom(SessionSnapshot s) => string.IsNullOrWhiteSpace(s.Titre) ? s.Project : s.Titre!;

    // SQUELETTE (RED 29-04) : remplacé par le GREEN de la tâche 1.
    public static string? MotifLisible(SessionSnapshot s) => throw new System.NotImplementedException();
    public static string Infobulle(SessionSnapshot s) => throw new System.NotImplementedException();

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
