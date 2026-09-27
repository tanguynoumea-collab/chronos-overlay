using Chronos.Models;
using Chronos.Models.Historique;

namespace Chronos.Services.Historique;

/// <summary>
/// JRN-05 — ce que les relevés DISENT, et rien de plus. Classe PURE : aucune E/S, <c>now</c> et <c>cadence</c> en
/// paramètres (cadence par défaut au site d'appel = <c>RateLimitHeaderUsageProvider.CadenceNominale</c>, donc le seuil
/// de trou = <c>JournalReleves.SeuilReprise</c>).
///
/// <para><b>Une vue lit UNE source</b> (D-32-25) : la source demandée, sinon la plus fréquente de la plage (la sonde en
/// production) ; les autres relevés ne sont pas mélangés dans la série.</para>
/// <para><b>Un trou</b> (écart de plus de deux cadences) n'est jamais interpolé : la série s'interrompt et le trou porte
/// sa cause (D-32-26) — le dernier <c>arret</c> / <c>jeton_invalide</c> / <c>sonde_refusee</c> dans
/// <c>]debut − cadence, fin]</c>, sinon « Chronos arrêté » si un <c>demarrage</c> tombe dedans (kill ou veille : pas
/// d'<c>arret</c> écrit), sinon « cause inconnue » — jamais tu. Un trou encore ouvert à <c>now</c> a <c>Fin = null</c>.</para>
/// <para><b>Un reset observé</b> est daté de l'ANCIENNE borne (D-32-27).</para>
/// <para><b>Un Δ</b> n'est calculé qu'entre relevés consécutifs de MÊME <c>resets_at</c> et sans trou entre eux (D-32-28) :
/// la piste Rythme ne montrera jamais une barre inventée au réveil. Un Δ négatif sans changement de <c>resets_at</c> est
/// conservé et marqué <c>Anormal</c>.</para>
/// <para><b>Le saut</b> de part et d'autre d'un trou fermé est « non localisé » (répartition inconnue) ; si le
/// <c>resets_at</c> a changé pendant l'absence, il est indéterminable (<c>null</c>).</para>
/// <para>Ce que cette classe ne fait PAS : projeter, lisser, deviner une grille de resets (les resets sont OBSERVÉS,
/// contrairement à la grille théorique de 5 h du cadran), ou convertir des tokens en pourcentage.</para>
/// </summary>
public static class AnalyseReleves
{
    /// <summary>Analyse une lecture. <paramref name="source"/> null = la plus fréquente de la plage.</summary>
    public static AnalyseJournal Analyser(LectureJournal lecture, DateTimeOffset now, TimeSpan cadence, SourceUsage? source = null)
    {
        ArgumentNullException.ThrowIfNull(lecture);
        var seuilTrou = 2 * cadence;

        // 1. Source retenue et série (une seule source, triée par T — tri stable).
        var retenue = source ?? SourceLaPlusFrequente(lecture.Releves);
        var serie = retenue is { } s
            ? lecture.Releves.Where(r => r.Source == s).OrderBy(r => r.T).ToList()
            : new List<ReleveJournal>();
        var evenements = lecture.Evenements.OrderBy(e => e.T).ToList();

        // 2. Trous : trouAvant[i] = un trou sépare serie[i−1] de serie[i]. Le dernier trou peut être ouvert (Fin null).
        var trous = new List<Trou>();
        var indexFinDeTrou = new List<int>();   // pour les sauts : l'index i du relevé qui FERME chaque trou
        var trouAvant = new bool[serie.Count];
        for (var i = 1; i < serie.Count; i++)
        {
            if (serie[i].T - serie[i - 1].T <= seuilTrou) continue;
            trouAvant[i] = true;
            trous.Add(new Trou(serie[i - 1].T, serie[i].T, Cause(evenements, serie[i - 1].T, serie[i].T, cadence)));
            indexFinDeTrou.Add(i);
        }
        if (serie.Count > 0 && now - serie[^1].T > seuilTrou)
            trous.Add(new Trou(serie[^1].T, null, Cause(evenements, serie[^1].T, now, cadence)));

        // 3 + 4. Resets observés et Δ, par fenêtre.
        var (resets5h, deltas5h) = ResetsEtDeltas(serie, WindowKind.FiveHour, r => r.U5, r => r.R5, trouAvant);
        var (resetsHebdo, deltasHebdo) = ResetsEtDeltas(serie, WindowKind.SevenDay, r => r.U7, r => r.R7, trouAvant);

        // 5. Sauts non localisés : de part et d'autre de chaque trou FERMÉ, pour chaque fenêtre renseignée.
        var sauts = new List<SautNonLocalise>();
        for (var k = 0; k < indexFinDeTrou.Count; k++)
        {
            var avant = serie[indexFinDeTrou[k] - 1];
            var apres = serie[indexFinDeTrou[k]];
            AjouterSaut(sauts, trous[k], WindowKind.FiveHour, avant.U5, apres.U5, avant.R5, apres.R5);
            AjouterSaut(sauts, trous[k], WindowKind.SevenDay, avant.U7, apres.U7, avant.R7, apres.R7);
        }

        return new AnalyseJournal(retenue, serie, trous, resets5h, resetsHebdo, deltas5h, deltasHebdo, sauts,
                                  lecture.JournalOuvertLe, lecture.Plage);
    }

    // La source la plus fréquente ; à égalité, la première de l'enum (SondeEnTetes d'abord). Aucun relevé → null.
    private static SourceUsage? SourceLaPlusFrequente(IReadOnlyList<ReleveJournal> releves)
        => releves.Count == 0
            ? null
            : releves.GroupBy(r => r.Source)
                     .OrderByDescending(g => g.Count())
                     .ThenBy(g => (int)g.Key)
                     .First().Key;

    // D-32-26. La fenêtre d'attribution commence UNE cadence avant le dernier relevé : l'événement qui a interrompu la
    // sonde peut avoir été écrit juste avant que le relevé suivant ne manque. Reprise et EcritureRatee ne causent rien.
    private static CauseTrou Cause(IReadOnlyList<EvenementJournal> evenements, DateTimeOffset debut, DateTimeOffset fin, TimeSpan cadence)
    {
        CauseTrou? cause = null;
        var demarrageDedans = false;
        foreach (var e in evenements)
        {
            if (e.T <= debut - cadence || e.T > fin) continue;
            switch (e.Type)
            {
                case TypeEvenement.Arret: cause = CauseTrou.ChronosArrete; break;          // le DERNIER l'emporte
                case TypeEvenement.JetonInvalide: cause = CauseTrou.JetonInvalide; break;
                case TypeEvenement.SondeRefusee: cause = CauseTrou.SondeRefusee; break;
                case TypeEvenement.Demarrage when e.T > debut: demarrageDedans = true; break;
            }
        }
        return cause ?? (demarrageDedans ? CauseTrou.ChronosArrete : CauseTrou.Inconnue);
    }

    // D-32-27 / D-32-28 pour une fenêtre : un reset dès que r change (même à travers un trou : c'est un fait observé) ;
    // un Δ seulement si r est identique, non nul, u connus des deux côtés, et AUCUN trou entre les deux relevés.
    private static (IReadOnlyList<ResetObserve> Resets, IReadOnlyList<DeltaConsommation> Deltas) ResetsEtDeltas(
        IReadOnlyList<ReleveJournal> serie, WindowKind fenetre,
        Func<ReleveJournal, double?> u, Func<ReleveJournal, DateTimeOffset?> r, bool[] trouAvant)
    {
        var resets = new List<ResetObserve>();
        var deltas = new List<DeltaConsommation>();
        for (var i = 1; i < serie.Count; i++)
        {
            var rAvant = r(serie[i - 1]);
            var rApres = r(serie[i]);
            if (rAvant is null || rApres is null) continue;

            if (rApres != rAvant)
            {
                resets.Add(new ResetObserve(fenetre, rAvant.Value, serie[i].T));
                continue;   // jamais de Δ à travers un reset
            }
            if (trouAvant[i]) continue;   // jamais de Δ à travers un trou

            var uAvant = u(serie[i - 1]);
            var uApres = u(serie[i]);
            if (uAvant is null || uApres is null) continue;

            var delta = uApres.Value - uAvant.Value;
            deltas.Add(new DeltaConsommation(fenetre, serie[i - 1].T, serie[i].T, delta, rApres.Value, Anormal: delta < 0));
        }
        return (resets, deltas);
    }

    // Une fenêtre dont aucun des deux côtés n'a de valeur n'a rien à dire : pas de saut. Sinon le saut existe, et son
    // Delta n'est calculé que si r est le MÊME des deux côtés (au moins un reset dans l'absence → null, dit comme tel).
    private static void AjouterSaut(List<SautNonLocalise> sauts, Trou trou, WindowKind fenetre,
                                    double? uAvant, double? uApres, DateTimeOffset? rAvant, DateTimeOffset? rApres)
    {
        if (uAvant is null && uApres is null) return;
        var memeReset = rAvant is not null && rApres is not null && rAvant == rApres;
        double? delta = memeReset && uAvant is not null && uApres is not null ? uApres.Value - uAvant.Value : null;
        sauts.Add(new SautNonLocalise(fenetre, trou, uAvant, uApres, delta));
    }
}
