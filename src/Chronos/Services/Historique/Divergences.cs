using Chronos.Models.Historique;
using Chronos.Models.Historique.Tokens;

namespace Chronos.Services.Historique;

/// <summary>
/// D-34-07 — la divergence « consommé ailleurs (Cowork, claude.ai) » est une ANALYSE, pas une géométrie : une marche du % hebdo
/// sur une case d'une heure SANS aucune tranche de tokens Claude Code, alors que les transcripts de cette heure sont COUVERTS.
///
/// <para><b>POURQUOI le seuil vaut 0,01</b> (<see cref="SeuilDelta"/>) : c'est la granularité CONSTATÉE des en-têtes de rate-limit
/// (STATE « Contexte technique v1.8 » : les <c>utilization</c> bougent par centièmes) — une marche plus petite n'est pas
/// observable, une marche d'un centième l'est. Constante NOMMÉE, recalable après le constat de la phase 35 ; jamais un
/// nombre en ligne.</para>
///
/// <para><b>POURQUOI la condition de couverture</b> : une heure « transcripts absents » (Claude Code a pu purger) ou « hors
/// couverture » (avant le plus vieux transcript vu) n'a AUCUNE preuve d'absence d'activité Code — elle ne peut pas accuser
/// Cowork. Seule une heure <see cref="EtatCouverture.Couverte"/> sans message (<c>Principal.N + SousAgents.N == 0</c>) dit
/// vraiment « rien côté Code ». Seuls les Δ POSITIFS comptent (un Δ anormal négatif n'est jamais soustrait). Les heures
/// adjacentes retenues fusionnent en une seule divergence qui porte la somme. Classe PURE, type NEUTRE.</para>
/// </summary>
public static class Divergences
{
    /// <summary>Granularité constatée des en-têtes (un centième) : la plus petite marche observable du % hebdo.</summary>
    public const double SeuilDelta = 0.01;

    // Tolérance d'arrondi binaire sur la somme (0,005 + 0,005 doit atteindre 0,01) ; sans effet sur le seuil lui-même.
    private const double ToleranceArrondi = 1e-9;

    private static readonly TimeSpan Heure = TimeSpan.FromHours(1);

    /// <summary>
    /// Les divergences d'une plage : pour chaque barre d'une heure <c>[DebutUtc, DebutUtc + 1 h[</c>, la somme des Δ hebdo
    /// positifs dont <c>A</c> tombe dans la case ; retenue si somme ≥ <see cref="SeuilDelta"/>, aucun message de tokens et
    /// couverture prouvée ; cases adjacentes retenues fusionnées.
    /// </summary>
    public static IReadOnlyList<Divergence> Detecter(IReadOnlyList<DeltaConsommation> deltasHebdo, IReadOnlyList<BarreHeure> barres)
    {
        ArgumentNullException.ThrowIfNull(deltasHebdo);
        ArgumentNullException.ThrowIfNull(barres);

        var retenues = new List<Divergence>();
        foreach (var b in barres)
        {
            var debut = b.DebutUtc;
            var fin = debut + Heure;
            var somme = 0.0;
            foreach (var d in deltasHebdo)
            {
                if (d.Delta > 0 && debut <= d.A && d.A < fin) somme += d.Delta;
            }

            var sansTokens = b.Principal.N + b.SousAgents.N == 0;
            if (somme + ToleranceArrondi >= SeuilDelta && sansTokens && b.Etat == EtatCouverture.Couverte)
            {
                if (retenues.Count > 0 && retenues[^1].Fin == debut)
                    retenues[^1] = new Divergence(retenues[^1].Debut, fin, retenues[^1].Delta + somme);   // adjacente : fusion
                else
                    retenues.Add(new Divergence(debut, fin, somme));
            }
        }
        return retenues;
    }
}
