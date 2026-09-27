using Chronos.Models;
using Chronos.Models.Historique;

namespace Chronos.Rendering.Historique;

/// <summary>
/// Une fenêtre 5 h telle que le journal l'a vue : du premier relevé qui porte ce <paramref name="ResetsAt"/> au reset
/// (borné à la plage), la hauteur du max % 5 h, grise si épuisée ; <paramref name="ResetDansPlage"/> dit si le tiret de
/// reset se dessine à droite de la tuile.
/// </summary>
public sealed record Tuile5h(DateTimeOffset Debut, DateTimeOffset Fin, DateTimeOffset ResetsAt, double UMax, bool Epuisee, bool ResetDansPlage);

/// <summary>
/// HIS-02 / HIS-03 (côté géométrie) — les TUILES 5 h du style « Tuiles » (DESIGN_PLAN §2.2, ligne FENÊTRES 5 H).
/// Géométrie PURE : aucun état, aucun I/O, aucun type WPF ; consomme la série d'<c>AnalyseJournal</c> et ne recalcule
/// rien. Une tuile est une fenêtre OBSERVÉE : elle commence au premier relevé qui porte un <c>resets_at</c> et finit à ce
/// <c>resets_at</c> — la même borne que <c>ResetObserve.Instant</c> quand le reset a été vu — jamais à une grille théorique
/// de 5 h. Une tuile est « épuisée » si le compteur a atteint 1 OU si le serveur a répondu <c>Rejete</c> sur la fenêtre 5 h :
/// le refus du serveur fait foi même quand le compteur dit moins (« épuisée à 100 % — le serveur refuse »).
/// </summary>
public static class Tuiles5h
{
    /// <summary>
    /// Une tuile par groupe de relevés CONSÉCUTIFS de même <c>R5</c> (relevés sans <c>R5</c> ou sans <c>U5</c> ignorés) :
    /// <c>Debut</c> = <c>T</c> du premier, <c>Fin</c> = <c>min(R5, Plage.Fin)</c>, <c>UMax</c> = max <c>U5</c>, <c>Epuisee</c> =
    /// <c>UMax ≥ 1</c> ou un statut 5 h refusé, <c>ResetDansPlage</c> = <c>R5 &lt; Plage.Fin</c>. Un groupe qui commence à ou
    /// après <c>Plage.Fin</c> est omis.
    /// </summary>
    public static IReadOnlyList<Tuile5h> Depuis(IReadOnlyList<ReleveJournal> serie, Plage plage)
    {
        var releves = serie.Where(r => r.R5 is not null && r.U5 is not null && !double.IsNaN(r.U5.Value)).ToList();
        var tuiles = new List<Tuile5h>();

        var i = 0;
        while (i < releves.Count)
        {
            var premier = releves[i];
            var resetsAt = premier.R5!.Value;
            var uMax = premier.U5!.Value;
            var refuse = Refuse(premier);

            var j = i + 1;
            while (j < releves.Count && releves[j].R5 == resetsAt)
            {
                uMax = Math.Max(uMax, releves[j].U5!.Value);
                refuse |= Refuse(releves[j]);
                j++;
            }
            i = j;

            if (premier.T >= plage.Fin) continue;
            var fin = resetsAt < plage.Fin ? resetsAt : plage.Fin;
            if (fin < premier.T) fin = premier.T;   // un resets_at antérieur au relevé (serveur incohérent) : tuile de largeur nulle, pas négative

            tuiles.Add(new Tuile5h(premier.T, fin, resetsAt, uMax, Epuisee: uMax >= 1.0 || refuse, ResetDansPlage: resetsAt < plage.Fin));
        }
        return tuiles;
    }

    // Le serveur a refusé la fenêtre 5 h sur ce relevé : la tuile est épuisée quoi qu'en dise le compteur.
    private static bool Refuse(ReleveJournal r) => r.Statut5 == StatutServeur.Rejete;
}
