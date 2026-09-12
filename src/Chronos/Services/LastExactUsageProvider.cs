using Chronos.Models;

namespace Chronos.Services;

/// <summary>
/// LA COUCHE DE DOCTRINE (EXA-01, EXA-02, EXA-05, DEL-03, DEL-04). Décorateur d'<see cref="IUsageProvider"/>
/// en TÊTE de chaîne, et écrivain UNIQUE du magasin persistant.
///
/// POURQUOI la doctrine vit ici et non dans <see cref="CompositeUsageProvider"/> :
/// <list type="number">
///   <item>Elle est la SEULE couche à détenir simultanément l'horloge, le magasin persistant et la source
///     d'activité. Le composite n'en connaît aucun des trois, et lui en injecter reviendrait à faire
///     descendre la persistance dans chaque maillon de la chaîne.</item>
///   <item>Elle est en tête : elle voit exactement le snapshot fusionné qui sera affiché, donc elle statue
///     UNE fois, sur le résultat final — là où le composite statuerait à chaque niveau imbriqué (trois en
///     production), avec autant de passes disque potentielles.</item>
///   <item>Elle est au-dessus de tout composite : il n'y a donc plus aucune ambiguïté de provenance à
///     trancher, ce qui rend la recomposition triviale (voir le « with » ci-dessous).</item>
/// </list>
///
/// Ce qu'elle fait, à chaque rafraîchissement :
/// <list type="number">
///   <item>délègue à l'inner ;</item>
///   <item>écrit dans le magasin les fenêtres <see cref="SourceReliability.Exact"/> que l'inner a produites,
///     et RIEN d'autre — le magasin ne contient par construction que de l'exact certifié ;</item>
///   <item>ne paie la passe de transcripts que si au moins une fenêtre en a besoin (paresse) ;</item>
///   <item>fait STATUER <see cref="DoctrineFraicheur"/> une fois par fenêtre, avec le MÊME journal ;</item>
///   <item>remonte le bit EXA-05 « un exact a-t-il déjà été obtenu ? » jusqu'au snapshot.</item>
/// </list>
///
/// Ce n'est plus le rebouchage de trou de la phase 16 : une fenêtre vivante peut désormais être démotée
/// (relevé trop vieux, incertifiable, ou hors de l'horizon des transcripts) et une fenêtre mémorisée peut
/// être servie qualifiée — mais jamais gonflée d'un delta (EXA-04).
///
/// Choix de conception : un DÉCORATEUR, pas un abonnement à un événement de l'orchestrateur — ce dernier
/// motif exigeait une résolution forcée du service avant le démarrage de l'hôte (invisible au compilateur).
///
/// Type NEUTRE (aucun type WPF) : la garde de pureté Services/Models reste verte.
/// </summary>
public sealed class LastExactUsageProvider : IUsageProvider
{
    private readonly IUsageProvider _inner;
    private readonly LastExactStore _store;
    private readonly IClock _clock;
    private readonly ITranscriptActivitySource _activite;

    /// <param name="activite">Source d'activité. OBLIGATOIRE, sans valeur par défaut : un site de
    /// construction qui l'omettrait ferait dégrader la doctrine en branche « indisponible » systématique,
    /// EN SILENCE. Le dépôt a déjà vécu cette forme de bug (« un service que le host ne démarre jamais ne
    /// fait rien », phase 17) — le compilateur est ici le seul garde-fou qui ne s'oublie pas.</param>
    public LastExactUsageProvider(IUsageProvider inner, LastExactStore store, IClock clock,
                                  ITranscriptActivitySource activite)
    {
        _inner = inner;
        _store = store;
        _clock = clock;
        _activite = activite;
    }

    public async Task<UsageSnapshot> GetAsync(CancellationToken ct = default)
    {
        var snap = await _inner.GetAsync(ct);
        var now = _clock.UtcNow;

        LastExactWindows? memorise = null;
        bool? dejaEuUnExact = null;   // null = non évalué : un magasin en panne n'AFFIRME rien
        try
        {
            // On n'écrit QUE ce que l'inner a produit d'exact, et AVANT de statuer : un plancher ne doit
            // jamais contaminer le magasin, dont le contenu est par construction de l'exact certifié.
            // Sans cette précaution, une seule fenêtre démotée gèlerait la dégradation pour toujours.
            if (snap.FiveHour.Reliability == SourceReliability.Exact
                || snap.SevenDay.Reliability == SourceReliability.Exact)
                _store.Save(snap);

            memorise = _store.Load(now);
            dejaEuUnExact = _store.UnExactADejaEteObtenu();
        }
        catch
        {
            // Un magasin en échec (disque plein, fichier verrouillé, chemin inutilisable) ne doit jamais
            // priver l'utilisateur de son affichage, ni lui faire croire qu'il n'a JAMAIS rien obtenu :
            // « je ne sais pas » (null) et « jamais » (false) n'appellent pas la même invite (EXA-05).
            memorise = null;
            dejaEuUnExact = null;
        }

        // PARESSE — la passe disque coûte 2,7 à 3,2 s et lit 536 Mo (mesuré le 2026-09-12 sur la machine
        // cible) ; au tick de 60 s, une lecture inconditionnelle serait une E/S permanente sur un overlay.
        // On ne la paie que si au moins une des deux fenêtres sort du laissez-passer d'âge. Deux tests
        // comptent les passes : ZÉRO en régime nominal, et UNE SEULE — jamais deux — quand les deux
        // fenêtres en ont besoin. Un seul journal sert les deux : il est pur et interrogeable N fois,
        // c'est exactement ce pour quoi la phase 16 a séparé l'E/S de l'interrogation.
        TranscriptActivityLog? journal = null;
        if (DoctrineFraicheur.ABesoinDuJournal(snap.FiveHour, memorise?.FiveHour, now)
            || DoctrineFraicheur.ABesoinDuJournal(snap.SevenDay, memorise?.SevenDay, now))
        {
            try { journal = await _activite.ReadAsync(ct); }
            catch { journal = null; }   // source absente ou illisible -> branche 4, jamais de crash
        }

        // « with » et non « new » : cette couche est AU-DESSUS de tout composite, il n'y a donc aucune
        // ambiguïté de provenance à trancher — hériter du snapshot vivant est la bonne réponse pour tout
        // champ qu'on ne redéfinit pas ici. (Dans un composite, à l'inverse, « with » ferait hériter
        // silencieusement du primaire : c'est pourquoi le composite garde son « new » explicite.)
        return snap with
        {
            FiveHour = DoctrineFraicheur.Statuer(snap.FiveHour, memorise?.FiveHour, journal, now),
            SevenDay = DoctrineFraicheur.Statuer(snap.SevenDay, memorise?.SevenDay, journal, now),
            UnExactADejaEteObtenu = dejaEuUnExact,
        };
    }
}
