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

    /// <summary>Écartée par <see cref="TreatedStore"/> — hystérésis « traité », posée par le détecteur.</summary>
    Traitee,
}

/// <summary>Une session détectée que le widget n'affiche pas, et le filtre qui l'a écartée.</summary>
public sealed record SessionMasquee(SessionSnapshot Session, MotifMasquage Motif);

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
/// </summary>
public sealed record LectureSessions(
    IReadOnlyList<SessionSnapshot> Visibles,
    IReadOnlyList<SessionMasquee> Masquees,
    int FichiersEcartesParAnciennete);
