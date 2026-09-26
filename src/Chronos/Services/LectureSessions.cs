using System.Collections.Generic;

namespace Chronos.Services;

/// <summary>
/// POURQUOI une session pourtant DÉTECTÉE n'apparaît pas dans le widget. Un motif n'est pas une cause
/// d'absence : les sessions listées ici existent, le moniteur les a lues, et un filtre les écarte.
///
/// <para>Ce vocabulaire existe parce que son absence a coûté cher. Relevé le 2026-09-12 : la session
/// e465420e (PROJET ADVANCED SHEET), vivante et en attente de permission depuis 10 h, n'apparaissait
/// NULLE PART — ni dans le widget, ni dans le rapport de diagnostic. Elle n'était pas absente, elle
/// était masquée. « Absent » et « masqué par tel filtre » ne se disent pas de la même façon, et
/// confondre les deux rend un défaut inélucidable.</para>
/// </summary>
public enum MotifMasquage
{
    /// <summary>Écartée par <see cref="ArchiveStore"/> — geste explicite de l'utilisateur (clic droit).</summary>
    Archivee,

    /// <summary>Écartée par <see cref="TreatedStore"/> SANS cause connue du détecteur : marquée à la main (geste), ou
    /// traitée avant le démarrage de l'overlay et non relue depuis. Réversible.</summary>
    Traitee,

    /// <summary>État INDÉTERMINÉ (signal illisible) : le widget n'a pas de ligne pour ce qu'il n'a pas pu lire
    /// (LIB-01). Ce n'est pas un geste de l'utilisateur ; le rapport la liste pour que son absence reste explicable.</summary>
    Indeterminee,

    // Les trois valeurs suivantes sont AJOUTÉES EN FIN (phase 30, LUE-03) : rien ne se réordonne. Elles nomment la
    // cause d'un masquage par TreatedStore quand le détecteur l'a CONSTATÉE lui-même.

    /// <summary>Lue : le focus de la session dans l'app est postérieur à l'attente — LUE-01.</summary>
    LueParFocus,

    /// <summary>Lue : session sélectionnée, fenêtre claude au premier plan au-delà de la grâce — LUE-02.</summary>
    LueAuPremierPlan,

    /// <summary>Répondue : attente puis travail observé sur la MÊME source — NET-01.</summary>
    Repondue,
}

/// <summary>
/// POURQUOI le détecteur a inscrit (ou constaté) une session traitée, et les instants qui le prouvent. treated.json
/// ne porte que l'épisode : la cause vit en mémoire (D-30-05). <see cref="Constat"/> = l'instant du cycle qui a
/// décidé, figé.
/// </summary>
/// <param name="Motif">La règle qui a conclu : <see cref="MotifMasquage.LueParFocus"/>,
/// <see cref="MotifMasquage.LueAuPremierPlan"/> ou <see cref="MotifMasquage.Repondue"/>.</param>
/// <param name="Attente">L'épisode d'attente traité (l'instant que le signal porte).</param>
/// <param name="Focus">Le dernier focus de la session dans l'app, quand la lecture en dépend.</param>
/// <param name="PremierPlanDepuis">L'instant depuis lequel le processus claude était au premier plan (LUE-02).</param>
/// <param name="Constat">L'instant du cycle qui a décidé, figé au premier constat de l'épisode.</param>
public sealed record CauseTraitement(
    MotifMasquage Motif,
    System.DateTimeOffset Attente,
    System.DateTimeOffset? Focus = null,
    System.DateTimeOffset? PremierPlanDepuis = null,
    System.DateTimeOffset? Constat = null);

/// <summary>
/// Ce que le moniteur sait de la LECTURE à ce cycle : le dernier focus par session (clé insensible à la casse, bâti
/// sur la lecture de l'app de CE cycle), la session sélectionnée dans l'app (nulle si inconnue ou sans cliSessionId)
/// et l'instant depuis lequel le processus claude est au premier plan (nul sinon). Un contexte NUL rend la règle
/// « lue » inactive : comportement v1.6 exact (LUE-04).
/// </summary>
public sealed record ContexteLecture(
    IReadOnlyDictionary<string, System.DateTimeOffset?> DernierFocus,
    string? Selectionnee,
    System.DateTimeOffset? ClaudeAuPremierPlanDepuis);

/// <summary>Une session détectée que le widget n'affiche pas, le filtre qui l'a écartée et, quand le détecteur l'a
/// constatée, la cause de son traitement (nulle : cause inconnue — geste, ou traitement antérieur au démarrage).</summary>
public sealed record SessionMasquee(SessionSnapshot Session, MotifMasquage Motif, CauseTraitement? Cause = null);

/// <summary>
/// Ce que le moniteur a VU à un instant donné : ce qu'il retient, ce qu'il masque (et par quel filtre),
/// et combien de fichiers d'état de hook il a écartés parce qu'ils étaient trop anciens.
///
/// <para><see cref="Visibles"/> est, mot pour mot, ce que le widget affiche — c'est la MÊME liste, rendue
/// par le MÊME appel. Aucune reconstruction, aucune approximation.</para>
///
/// <para><see cref="FichiersEcartesParAnciennete"/> n'est PAS un motif de masquage : une session dont le
/// signal a expiré n'est pas cachée, elle est inconnue. Le compteur est là parce que sur la machine
/// mesurée il valait 52 sur 54 fichiers — un fait qui explique l'écart entre « 54 fichiers sur disque »
/// et « 1 ligne à l'écran », et que rien ne disait.</para>
///
/// <para><see cref="Desaccords"/> (FUS-02) n'est PAS un motif de masquage et ne doit jamais être compté
/// comme tel : la session concernée est AFFICHÉE. Ce qui a été écarté, c'est l'un de ses signaux. Le relevé
/// du 2026-09-12 est le cas d'école : un fichier de hook figé depuis 7 h annonçait « à toi » pendant qu'un
/// transcript de 10 s prouvait le contraire — le désaccord gagnait, et rien ne le disait.</para>
///
/// <para><see cref="AppBureau"/> (APP-01, phase 29) est la lecture de l'app bureau de CE cycle, la MÊME que celle
/// qui a qualifié les lignes (OBS-01) : le rapport de diagnostic la lit ici, jamais par un second appel. Nul = moniteur
/// sans lecteur (comportement v1.6, « NON BRANCHÉE » au rapport).</para>
/// </summary>
public sealed record LectureSessions(
    IReadOnlyList<SessionSnapshot> Visibles,
    IReadOnlyList<SessionMasquee> Masquees,
    int FichiersEcartesParAnciennete,
    IReadOnlyList<DesaccordSources> Desaccords,
    LectureAppBureau? AppBureau = null);
