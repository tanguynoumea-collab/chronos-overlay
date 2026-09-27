using Chronos.Models.Historique;

namespace Chronos.Models.Historique.Tokens;

/// <summary>
/// TOK-04 — ce que la lecture par plage des agrégats de tokens REND à la piste « Tokens Claude Code » de la phase 34
/// (barres par heure en Semaine, colonnes par quart d'heure et par modèle en Jour, part sous-agents), dans les mots du
/// plan de design (§4) : « <b>hors couverture</b> » = avant le plus vieux transcript jamais vu, rien ne peut être dit ;
/// « <b>transcripts absents</b> » = Claude Code a pu purger des fichiers avant que Chronos ne les lise (juillet 2026),
/// présence partielle possible ; « <b>couverte</b> » = tout a été lu, une absence de tranche est une vraie absence
/// d'activité.
///
/// <para><b>Doctrine</b> : les tokens de Claude Code restent sur leur PROPRE axe — jamais un pourcentage, jamais la somme
/// des quatre compteurs (les limites pondèrent par modèle et par nature ; ces tokens sont partiels : hors Cowork, hors
/// claude.ai). Un agrégat absent n'est un « zéro » QUE dans une sous-plage couverte : l'état de couverture voyage avec
/// chaque agrégat rendu (D-33-19), le consommateur ne peut pas obtenir « 0 » sans son état. ENTIERS SEULEMENT : la garde
/// TOK-05 (<c>GardeTokensSansPourcentageTests</c>) rougit sur tout flottant dans ce namespace ; la part sous-agents est un
/// couple de totaux (D-33-20), jamais un rapport. Types NEUTRES (aucun WPF), sous <c>ServicesLayerPurityTests</c>.</para>
/// </summary>
/// <param name="In">Tokens d'entrée, cumulés.</param>
/// <param name="Out">Tokens de sortie, cumulés.</param>
/// <param name="CacheW">Tokens d'écriture de cache, cumulés.</param>
/// <param name="CacheR">Tokens de lecture de cache, cumulés.</param>
/// <param name="N">Nombre de messages distincts, cumulé.</param>
public sealed record TotauxTokens(long In, long Out, long CacheW, long CacheR, int N)
{
    /// <summary>Rien : quatre compteurs à zéro, aucun message. C'est l'élément neutre de <see cref="Plus"/>.</summary>
    public static readonly TotauxTokens Vide = new(0, 0, 0, 0, 0);

    /// <summary>Ces totaux plus une tranche — les quatre compteurs SÉPARÉMENT, jamais additionnés entre eux.</summary>
    public TotauxTokens Plus(TrancheTokens t) => new(In + t.In, Out + t.Out, CacheW + t.CacheW, CacheR + t.CacheR, N + t.N);

    /// <summary>La somme de plusieurs tranches (vide → <see cref="Vide"/>).</summary>
    public static TotauxTokens Somme(IEnumerable<TrancheTokens> ts) => ts.Aggregate(Vide, (a, t) => a.Plus(t));
}

/// <summary>
/// Un segment <c>[Debut, Fin[</c> de la plage lue qui porte UN SEUL état de couverture. La plage entière est partagée en
/// sous-plages contiguës aux bornes connues de la couverture (plus ancienne ligne vue, débuts et fins d'intervalles garantis).
/// </summary>
/// <param name="Debut">Borne incluse.</param>
/// <param name="Fin">Borne EXCLUE.</param>
/// <param name="Etat">Ce que les agrégats garantissent sur ce segment.</param>
public sealed record SousPlageCouverture(DateTimeOffset Debut, DateTimeOffset Fin, EtatCouverture Etat);

/// <summary>
/// Le résultat BRUT d'une lecture par plage : les tranches filtrées sur <c>[Debut, Fin[</c> et triées (slot, modèle
/// ordinal, sub), le nombre de lignes refusées (exposé, jamais tu), la couverture de la plage en sous-plages, et la plus
/// ancienne ligne jamais vue (« hors couverture » avant elle). Comme <see cref="LectureJournal"/> porte
/// <c>JournalOuvertLe</c>, cette lecture porte sa <see cref="Couverture"/> : une plage vide dit POURQUOI elle est vide.
/// </summary>
/// <param name="Tranches">Tranches de 15 min dans <c>[Debut, Fin[</c>, triées (slot, modèle ordinal, sub false avant true).</param>
/// <param name="LignesIgnorees">Lignes refusées par le lecteur tolérant, sur l'ensemble des fichiers ouverts.</param>
/// <param name="Plage">La plage demandée.</param>
/// <param name="Couverture">Les sous-plages contiguës de <c>[Debut, Fin[</c> avec leur état ; vide si la plage est vide.</param>
/// <param name="PlusAncienneLigneVue">Le plus vieux timestamp jamais lu par la reconstruction ; <c>null</c> = rien lu encore (tout est hors couverture).</param>
public sealed record LectureAgregats(
    IReadOnlyList<TrancheTokens> Tranches,
    int LignesIgnorees,
    Plage Plage,
    IReadOnlyList<SousPlageCouverture> Couverture,
    DateTimeOffset? PlusAncienneLigneVue)
{
    /// <summary>L'état de couverture à un instant de la plage. Hors de toute sous-plage (instant hors de la plage lue,
    /// ou plage vide) → « hors couverture » : on ne dit jamais « couverte » de ce qu'on n'a pas lu.</summary>
    public EtatCouverture EtatA(DateTimeOffset instant)
        => Couverture.FirstOrDefault(s => s.Debut <= instant && instant < s.Fin)?.Etat ?? EtatCouverture.HorsCouverture;
}

/// <summary>
/// Une barre de la vue Semaine : UN début d'heure UTC de la plage, libellé en heure locale (D-33-18). Le 25/10/2026, deux
/// barres consécutives se libellent « 02:00 » (+02:00 puis +01:00) ; le 28/03/2027, aucune ne se libelle « 02:00 » — c'est
/// le calendrier, pas un défaut. Principal et sous-agents sont deux totaux séparés, empilés par la vue.
/// </summary>
/// <param name="DebutUtc">Début de l'heure, UTC.</param>
/// <param name="Libelle">« HH:mm » en heure locale du fuseau injecté.</param>
/// <param name="Principal">Totaux des tranches <c>sub == false</c>.</param>
/// <param name="SousAgents">Totaux des tranches <c>sub == true</c>.</param>
/// <param name="Etat">Couverture au début de l'heure : une barre vide n'est « zéro » que si <see cref="EtatCouverture.Couverte"/>.</param>
public sealed record BarreHeure(DateTimeOffset DebutUtc, string Libelle, TotauxTokens Principal, TotauxTokens SousAgents, EtatCouverture Etat);

/// <summary>La part d'UN modèle dans une colonne de la vue Jour (sous-agents INCLUS : légende « sous-agents inclus » du plan de design §2.3).</summary>
/// <param name="Model">Identifiant du modèle tel que lu (« claude-opus-5 »…).</param>
/// <param name="Totaux">Totaux du modèle sur la colonne, principal et sous-agents confondus.</param>
public sealed record PartModele(string Model, TotauxTokens Totaux);

/// <summary>
/// Une colonne de la vue Jour : UNE tranche de 15 min UTC, libellée en heure locale, empilée par modèle
/// (<see cref="ParModele"/> triées par sortie décroissante puis modèle ordinal).
/// </summary>
/// <param name="Slot">Début de la tranche, UTC.</param>
/// <param name="Libelle">« HH:mm » en heure locale du fuseau injecté.</param>
/// <param name="ParModele">Une part par modèle présent ; vide si aucune tranche.</param>
/// <param name="Etat">Couverture au début de la tranche.</param>
public sealed record ColonneQuartDHeure(DateTimeOffset Slot, string Libelle, IReadOnlyList<PartModele> ParModele, EtatCouverture Etat);
