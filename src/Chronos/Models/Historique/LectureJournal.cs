namespace Chronos.Models.Historique;

/// <summary>
/// JRN-05 — ce que la lecture par plage du journal REND aux trois vues de la phase 34 (Semaine de forfait,
/// Jour, 4 semaines), dans le vocabulaire du plan de design (§4) : un <b>relevé</b> est un fait exact du
/// serveur à un instant ; un <b>trou</b> est une absence de relevés de plus de deux cadences, qui porte sa
/// <b>cause</b> ; un <b>reset 5 h</b> / <b>reset hebdo</b> est OBSERVÉ (le <c>resets_at</c> a changé entre
/// deux relevés), jamais projeté ; un <b>Δ</b> est la différence de deux relevés consécutifs de MÊME
/// <c>resets_at</c> ; le saut de part et d'autre d'un trou est « non localisé » (<b>répartition inconnue</b>) ;
/// « <b>journal ouvert le</b> … » date la première ligne du journal — avant, rien.
///
/// <para><b>Doctrine</b> : aucun trou n'est interpolé ; aucune projection ; aucun pourcentage dérivé de
/// tokens ici (les tokens sont la phase 33, sur leur propre axe). Ces records ne disent que ce que les
/// relevés disent. Types NEUTRES (aucun WPF), sous la garde de <c>ServicesLayerPurityTests</c>.</para>
/// </summary>
/// <param name="Debut">Borne incluse.</param>
/// <param name="Fin">Borne EXCLUE : la plage est <c>[Debut, Fin[</c>.</param>
public sealed record Plage(DateTimeOffset Debut, DateTimeOffset Fin)
{
    /// <summary>Durée de la plage — 169 h ou 167 h pour une semaine de forfait qui contient un changement d'heure.</summary>
    public TimeSpan Duree => Fin - Debut;

    /// <summary>Un instant appartient-il à <c>[Debut, Fin[</c> ?</summary>
    public bool Contient(DateTimeOffset t) => Debut <= t && t < Fin;
}

/// <summary>
/// Le résultat BRUT d'une lecture par plage : relevés et événements triés par <c>t</c> et filtrés sur la plage,
/// le nombre de lignes sautées (exposé, jamais tu), et la date d'ouverture du journal (première ligne valide du
/// plus ancien fichier du dossier — indépendante de la plage demandée).
/// </summary>
/// <param name="Releves">Relevés de TOUTES les sources dans <c>[Debut, Fin[</c>, triés par <c>T</c> (tri stable).</param>
/// <param name="Evenements">Événements dans <c>[Debut, Fin[</c>, triés par <c>T</c>.</param>
/// <param name="LignesIgnorees">Lignes refusées par le lecteur tolérant, sur l'ensemble des fichiers ouverts.</param>
/// <param name="JournalOuvertLe"><c>t</c> de la première ligne valide du plus ancien fichier ; <c>null</c> = journal vide ou absent.</param>
/// <param name="Plage">La plage demandée.</param>
public sealed record LectureJournal(
    IReadOnlyList<ReleveJournal> Releves,
    IReadOnlyList<EvenementJournal> Evenements,
    int LignesIgnorees,
    DateTimeOffset? JournalOuvertLe,
    Plage Plage);

/// <summary>Pourquoi le journal s'est tu (D-32-26). <see cref="Inconnue"/> est une cause, pas un silence.</summary>
public enum CauseTrou
{
    /// <summary>Un <c>arret</c> propre a été écrit, ou un <c>demarrage</c> tombe dans le trou (kill, veille : pas d'<c>arret</c>).</summary>
    ChronosArrete,

    /// <summary>Un <c>jeton_invalide</c> a été écrit : le serveur a refusé les identifiants.</summary>
    JetonInvalide,

    /// <summary>Un <c>sonde_refusee</c> a été écrit : saturation ou refus serveur.</summary>
    SondeRefusee,

    /// <summary>Aucun événement n'explique l'absence. Affichée telle quelle : « cause inconnue ».</summary>
    Inconnue,
}

/// <summary>Les mots du plan de design (§2.2, §4) pour chaque cause — les mêmes partout : fenêtre, diagnostic, docs.</summary>
public static class CauseTrouTexte
{
    public static string Libelle(CauseTrou cause) => cause switch
    {
        CauseTrou.ChronosArrete => "Chronos arrêté",
        CauseTrou.JetonInvalide => "jeton invalide",
        CauseTrou.SondeRefusee => "sonde refusée",
        _ => "cause inconnue",
    };
}

/// <summary>
/// Une absence de relevés de plus de deux cadences. La série s'INTERROMPT ici : rien n'est interpolé.
/// </summary>
/// <param name="Debut"><c>t</c> du dernier relevé avant l'absence.</param>
/// <param name="Fin"><c>t</c> du premier relevé après l'absence ; <c>null</c> = trou encore OUVERT à <c>now</c>.</param>
/// <param name="Cause">Ce que les événements du journal disent de l'absence.</param>
public sealed record Trou(DateTimeOffset Debut, DateTimeOffset? Fin, CauseTrou Cause);

/// <summary>
/// Un reset OBSERVÉ : le <c>resets_at</c> d'une fenêtre a changé entre deux relevés consécutifs (D-32-27).
/// </summary>
/// <param name="Fenetre">5 h ou hebdo.</param>
/// <param name="Instant">L'ANCIENNE borne (<c>r[i−1]</c>) : c'est là que la vue Jour écrit « reset 5 h HH:MM ».</param>
/// <param name="ObserveA"><c>t</c> du relevé qui porte la NOUVELLE borne — le premier relevé après le reset.</param>
public sealed record ResetObserve(WindowKind Fenetre, DateTimeOffset Instant, DateTimeOffset ObserveA);

/// <summary>
/// Δ de consommation entre deux relevés CONSÉCUTIFS de MÊME <c>resets_at</c>, sans trou entre eux (D-32-28).
/// Jamais à travers un reset, jamais à travers un trou : la piste Rythme ne montre jamais une barre inventée.
/// </summary>
/// <param name="Fenetre">5 h ou hebdo.</param>
/// <param name="De"><c>t</c> du premier relevé.</param>
/// <param name="A"><c>t</c> du second relevé.</param>
/// <param name="Delta"><c>u[i] − u[i−1]</c>, fraction brute (peut être négatif : voir <paramref name="Anormal"/>).</param>
/// <param name="ResetsAt">Le <c>resets_at</c> commun aux deux relevés.</param>
/// <param name="Anormal">Δ négatif SANS changement de <c>resets_at</c> : conservé, marqué — c'est l'hypothèse
/// « Δ = consommation, pas de recalcul rétroactif » qui se teste avec le journal.</param>
public sealed record DeltaConsommation(WindowKind Fenetre, DateTimeOffset De, DateTimeOffset A, double Delta, DateTimeOffset ResetsAt, bool Anormal);

/// <summary>
/// Le saut de part et d'autre d'un trou FERMÉ : ce qui a été consommé pendant l'absence, sans qu'on sache quand
/// (« +N % pendant l'absence (répartition inconnue) »). Jamais une barre au réveil.
/// </summary>
/// <param name="Fenetre">5 h ou hebdo.</param>
/// <param name="Trou">Le trou concerné.</param>
/// <param name="Avant"><c>u</c> du dernier relevé avant l'absence.</param>
/// <param name="Apres"><c>u</c> du premier relevé après l'absence.</param>
/// <param name="Delta"><c>Apres − Avant</c> si le <c>resets_at</c> est le MÊME des deux côtés ; <c>null</c> = au moins
/// un reset dans l'absence (ou une des deux valeurs manque) : indéterminable, et dit comme tel.</param>
public sealed record SautNonLocalise(WindowKind Fenetre, Trou Trou, double? Avant, double? Apres, double? Delta);

/// <summary>
/// Le résultat de l'analyse PURE d'une lecture : tout ce que les trois vues dessinent, et rien d'autre.
/// </summary>
/// <param name="Source">La source retenue (D-32-25 : une vue lit UNE source) ; <c>null</c> si aucun relevé.</param>
/// <param name="Serie">Les relevés de cette source, triés par <c>T</c>.</param>
/// <param name="Trous">Les absences de plus de deux cadences, dans l'ordre, le dernier éventuellement ouvert.</param>
/// <param name="Resets5h">Resets 5 h observés.</param>
/// <param name="ResetsHebdo">Resets hebdo observés.</param>
/// <param name="Deltas5h">Δ 5 h entre relevés consécutifs de même <c>r5</c>.</param>
/// <param name="DeltasHebdo">Δ hebdo entre relevés consécutifs de même <c>r7</c>.</param>
/// <param name="Sauts">Sauts non localisés de part et d'autre de chaque trou fermé (une entrée par fenêtre renseignée).</param>
/// <param name="JournalOuvertLe">Recopié de la lecture : « journal ouvert le … ».</param>
/// <param name="Plage">Recopiée de la lecture.</param>
public sealed record AnalyseJournal(
    SourceUsage? Source,
    IReadOnlyList<ReleveJournal> Serie,
    IReadOnlyList<Trou> Trous,
    IReadOnlyList<ResetObserve> Resets5h,
    IReadOnlyList<ResetObserve> ResetsHebdo,
    IReadOnlyList<DeltaConsommation> Deltas5h,
    IReadOnlyList<DeltaConsommation> DeltasHebdo,
    IReadOnlyList<SautNonLocalise> Sauts,
    DateTimeOffset? JournalOuvertLe,
    Plage Plage);
