using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// LA DOCTRINE (EXA-02, EXA-04, DEL-03, DEL-04). Classe PURE : aucune E/S, aucune horloge propre, aucun
/// type WPF. Elle reçoit tout ce dont elle a besoin et rend un <see cref="WindowState"/> — donc
/// entièrement testable en [Fact], et chaque branche est supprimable pour prouver qu'un test tombe.
///
/// Ordre des règles, et POURQUOI cet ordre :
///   1. âge sous la limite                        -> Exact / Frais                (laissez-passer, 0 E/S)
///   2. journal couvre T et AUCUNE activité       -> Exact / EncoreValide         (DEL-03, ré-habilitation)
///   3. journal couvre T et activité              -> Estimated / Plancher         (DEL-04, borne inférieure)
///   4. sinon                                     -> Unavailable                  (jamais de chiffre inventé)
///
/// La limite d'âge n'est PAS un couperet : c'est un laissez-passer. Sans consommation, l'utilisation ne
/// change pas ; « zéro réponse assistant depuis T » implique donc « l'utilisation à now égale celle de T ».
/// Ce n'est pas une approximation, c'est une déduction — et déclarer périmé un chiffre qu'on peut PROUVER
/// juste reviendrait à cacher une information vraie. Le plafond absolu existe déjà et il est gratuit :
/// l'horizon de huit jours des transcripts. C'est par Covers(T), et non par la limite d'âge, que meurt
/// le relevé figé depuis deux mois qui a motivé ce milestone.
/// </summary>
public static class DoctrineFraicheur
{
    /// <summary>
    /// Âge maximal d'un relevé exact affiché SANS vérification d'activité (EXA-02). DÉRIVÉE de la cadence
    /// de la sonde, jamais recopiée : sous cette limite, le cache légitime de la sonde n'est jamais démoté,
    /// donc le chemin nominal ne paie JAMAIS la passe de transcripts (mesurée à 2,7-3,2 s et 536 Mo sur la
    /// machine cible). La limite d'âge est donc AUSSI un régulateur de débit, pas seulement un jugement.
    ///
    /// NON RÉGLABLE, et c'est délibéré : EXA-02 est une propriété de sûreté, pas une préférence. Un réglage
    /// permettrait de la porter à deux mois, c'est-à-dire de recréer le défaut que ce milestone éradique —
    /// le précédent est documenté dans ce dépôt (une source de plafond passée en « Manual » a gelé à vie
    /// un chiffre faux).
    /// </summary>
    public static readonly TimeSpan LimiteAge =
        RateLimitHeaderUsageProvider.CadenceNominale + TimeSpan.FromSeconds(60);

    /// <summary>Âge d'une fenêtre, ou null si elle ne dit pas QUAND elle a été capturée. Un exact sans
    /// horodatage est INCERTIFIABLE : on ne peut ni mesurer son âge, ni poser la question « activité
    /// depuis T ? ». Rendre null, et non TimeSpan.Zero, est le cœur de la correction.</summary>
    public static TimeSpan? Age(WindowState w, DateTimeOffset now)
        => w.CapturedAt is { } t ? now - t : null;

    /// <summary>
    /// Le journal d'activité est-il NÉCESSAIRE pour statuer sur cette fenêtre ? Permet à l'appelant de
    /// n'engager la passe disque que lorsqu'elle change quelque chose. Contrat : quand ceci rend false,
    /// <see cref="Statuer"/> produit le MÊME résultat avec ou sans journal — un test l'impose.
    /// </summary>
    public static bool ABesoinDuJournal(WindowState vivante, WindowState? memorisee, DateTimeOffset now)
    {
        var candidat = Candidat(vivante, memorisee);
        return candidat is not null && Age(candidat, now) is { } age && age > LimiteAge;
    }

    /// <summary>
    /// Statue sur UNE fenêtre. <paramref name="vivante"/> est ce que la chaîne vient de produire,
    /// <paramref name="memorisee"/> ce que le magasin détient (déjà validé : son reset est futur), et
    /// <paramref name="journal"/> le journal d'activité (null = source indisponible ou non consultée).
    /// </summary>
    public static WindowState Statuer(WindowState vivante, WindowState? memorisee,
                                      TranscriptActivityLog? journal, DateTimeOffset now)
    {
        var candidat = Candidat(vivante, memorisee);
        if (candidat is null) return Indisponible(vivante);

        // EXA-02, première moitié : pas d'horodatage, pas de certification possible. C'est ici que tombe
        // un relevé marqué exact dont personne ne sait quand il a été pris.
        if (Age(candidat, now) is not { } age) return Indisponible(vivante);

        // 1. FRAIS — laissez-passer : aucune E/S, aucune question posée aux transcripts.
        if (age <= LimiteAge) return Qualifier(candidat, vivante, SourceReliability.Exact,
                                               ProvenanceReleve.Frais, tokens: null);

        var t = candidat.CapturedAt!.Value;

        // 4a. Sans journal, ou journal qui NE COUVRE PAS T : rien ne peut être prouvé. On ne borne pas un
        // delta qu'on ne sait pas borner (le filtre mtime de huit jours sous-évaluerait en silence) — on
        // se déclare indisponible plutôt que d'inventer.
        if (journal is null || !journal.Covers(t)) return Indisponible(vivante);

        var activite = journal.Since(t);

        // 2. ENCORE EXACT (DEL-03) — aucune réponse assistant depuis T : l'utilisation n'a PAS bougé.
        if (!activite.HasActivity) return Qualifier(candidat, vivante, SourceReliability.Exact,
                                                    ProvenanceReleve.EncoreValide, tokens: 0L);

        // 3. PLANCHER (DEL-04) — il y a eu de l'activité : le chiffre devient une BORNE INFÉRIEURE.
        // Utilization INCHANGÉE : on ne l'augmente d'aucun delta (EXA-04). C'est la QUALIFICATION du
        // chiffre qui change, jamais sa valeur.
        return Qualifier(candidat, vivante, SourceReliability.Estimated,
                         ProvenanceReleve.PlancherAvecActivite, activite.Tokens);
    }

    // Exact vivant CERTIFIABLE -> lui. Sinon le magasin. Sinon rien. Un exact vivant sans utilization ou
    // sans horodatage ne vaut pas mieux que le magasin : c'est là que le relevé figé perd sa préséance.
    private static WindowState? Candidat(WindowState vivante, WindowState? memorisee)
        => vivante.Reliability == SourceReliability.Exact
           && vivante.Utilization is not null && vivante.CapturedAt is not null
            ? vivante
            : memorisee;

    // Le statut serveur et le dépassement décrivent le TICK COURANT (ce que le serveur vient de déclarer),
    // pas la fraîcheur d'un pourcentage : ils survivent donc à une substitution par le magasin, qui ne les
    // persiste pas. Sans ce report, HDR-03/HDR-04 disparaîtraient dès que le magasin prend la main.
    private static WindowState Qualifier(WindowState candidat, WindowState vivante,
                                         SourceReliability fiabilite, ProvenanceReleve provenance,
                                         long? tokens)
        => candidat with
        {
            Reliability = fiabilite,
            Provenance = provenance,
            TokensDepuisReleve = tokens,
            StatutServeur = candidat.StatutServeur ?? vivante.StatutServeur,
            Depassement = candidat.Depassement ?? vivante.Depassement,
        };

    // Démotion : on EFFACE le chiffre. WindowGaugeViewModel.Apply affecte Utilization SANS consulter
    // Reliability : une fenêtre indisponible qui conserverait 0,10 peindrait encore l'arc à dix pour cent
    // et le thème lui donnerait sa couleur. On CONSERVE en revanche l'instance vivante — donc son reset,
    // sa géométrie temporelle, son statut serveur et son dépassement : un reset connu reste un fait,
    // indépendant de l'âge du pourcentage qui l'accompagnait.
    private static WindowState Indisponible(WindowState vivante)
        => vivante with
        {
            Reliability = SourceReliability.Unavailable,
            Utilization = null,
            EstimatedTokens = null,
            TokensDepuisReleve = null,
            Provenance = null,
        };
}
