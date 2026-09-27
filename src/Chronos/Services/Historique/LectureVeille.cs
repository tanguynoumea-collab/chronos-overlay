using Chronos.Models.Historique;

namespace Chronos.Services.Historique;

/// <summary>
/// 35-01 (D-35-04, point différé 1 de 34-08) — la VEILLE DE MINUIT de la vue Jour. Un trou qui chevauche minuit (Chronos arrêté
/// mardi 23:00, relancé mercredi 07:00) est invisible si l'on n'analyse que les relevés du jour : l'analyse commence à 07:00 et la
/// nuit ne porte ni trou ni cause. Ce helper PUR adjoint à la lecture du jour le DERNIER relevé antérieur à minuit (même source que la
/// source dominante du jour, au plus <see cref="Horizon"/> avant) et les événements qui peuvent expliquer l'absence, pour que
/// <see cref="AnalyseReleves"/> — INCHANGÉE, pure — voie le trou et le nomme avec sa cause.
///
/// <para><b>Vue Jour seulement</b> (décision 3 de l'orchestrateur) : la Semaine et les 4 semaines commencent un samedi 00:00 déjà
/// couvert par leur propre lecture. <b>Rien n'est inventé</b> : le relevé de la veille sert à FERMER le trou, puis
/// <see cref="RestreindreAuJour"/> rend à la vue une série limitée au jour — aucun palier, aucune bande « présent », aucun relevé
/// compté avant le premier relevé du jour. Type NEUTRE (aucun WPF), aucune E/S, aucune horloge.</para>
/// </summary>
public static class LectureVeille
{
    /// <summary>Au plus sept jours en arrière : au-delà, le « dernier relevé » n'éclaire plus la nuit du jour affiché.</summary>
    public static readonly TimeSpan Horizon = TimeSpan.FromDays(7);

    /// <summary>
    /// La lecture de <paramref name="jour"/> augmentée de la veille : relevés et événements du jour ; si le jour a AU MOINS un relevé,
    /// le dernier relevé de <c>[jour.Debut − Horizon, jour.Debut[</c> de la source dominante du jour, et les événements de
    /// <c>]T_veille − cadence, jour.Debut[</c> (la fenêtre d'attribution des causes d'<see cref="AnalyseReleves"/>). Sans relevé du jour
    /// ou sans relevé de veille : la lecture du jour seule. <paramref name="large"/> couvre <c>[jour.Debut − Horizon, jour.Fin[</c>.
    /// </summary>
    public static LectureJournal PourLeJour(LectureJournal large, Plage jour, TimeSpan cadence)
    {
        ArgumentNullException.ThrowIfNull(large);
        ArgumentNullException.ThrowIfNull(jour);

        var relevesDuJour = large.Releves.Where(r => jour.Contient(r.T)).ToList();
        var evenementsDuJour = large.Evenements.Where(e => jour.Contient(e.T)).ToList();
        var seul = new LectureJournal(relevesDuJour, evenementsDuJour, large.LignesIgnorees, large.JournalOuvertLe, jour);
        if (relevesDuJour.Count == 0) return seul;

        // Même règle que AnalyseReleves (la plus fréquente ; à égalité, la première de l'enum) : le relevé de veille est de LA source lue.
        var dominante = relevesDuJour.GroupBy(r => r.Source)
                                     .OrderByDescending(g => g.Count())
                                     .ThenBy(g => g.Key)
                                     .First().Key;
        var depuis = jour.Debut - Horizon;
        var veille = large.Releves.LastOrDefault(r => r.T < jour.Debut && r.T >= depuis && r.Source == dominante);
        if (veille is null) return seul;

        var releves = new List<ReleveJournal>(relevesDuJour.Count + 1) { veille };
        releves.AddRange(relevesDuJour);
        var evenements = large.Evenements.Where(e => e.T > veille.T - cadence && e.T < jour.Debut)
                                         .Concat(evenementsDuJour)
                                         .OrderBy(e => e.T)   // tri stable
                                         .ToList();
        return new LectureJournal(releves, evenements, large.LignesIgnorees, large.JournalOuvertLe, jour);
    }

    /// <summary>
    /// Ce que la vue Jour DESSINE : l'analyse faite avec la veille, dont la série est restreinte aux relevés du jour. Les trous, sauts,
    /// resets et Δ restent ceux de l'analyse (le trou de minuit garde sa cause ; un reset observé de part et d'autre de minuit reste un
    /// fait) ; les pistes les bornent à la plage. Le relevé de la veille ne peint ni palier, ni « présent », ni réticule avant le premier
    /// relevé du jour, et n'est jamais compté parmi les « présents ».
    /// </summary>
    public static AnalyseJournal RestreindreAuJour(AnalyseJournal analyse, Plage jour)
    {
        ArgumentNullException.ThrowIfNull(analyse);
        ArgumentNullException.ThrowIfNull(jour);
        return analyse with { Serie = analyse.Serie.Where(r => jour.Contient(r.T)).ToList() };
    }
}
