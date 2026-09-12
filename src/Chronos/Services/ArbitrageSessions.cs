using System.Collections.Generic;
using System.Linq;

namespace Chronos.Services;

/// <summary>D'OÙ vient un signal de session. L'ORDRE DE DÉCLARATION EST SIGNIFIANT : il sert de
/// départage à ÂGE ÉGAL, et seulement là (voir <see cref="ArbitrageSessions"/>).</summary>
public enum SourceSession
{
    /// <summary>Fichier d'état écrit par un hook. SPÉCIFIQUE : lui seul sait dire « une permission est
    /// demandée ». C'est ce qui le départage d'un transcript de MÊME âge — jamais ce qui le hisse
    /// au-dessus d'un signal plus récent.</summary>
    Hook,

    /// <summary>Transcript JSONL. Universel et continu, mais muet sur la permission.</summary>
    Transcript,
}

/// <summary>Ce qu'UNE source dit d'UNE session, à un instant qu'elle porte elle-même
/// (<see cref="SessionSnapshot.UpdatedAt"/>). Un signal n'est pas un verdict : c'est une déposition.</summary>
public sealed record SignalSession(SourceSession Source, SessionSnapshot Session);

/// <summary>
/// FUS-02 — deux sources ont parlé de la même session et ne disent PAS la même chose. Ce n'est ni une
/// erreur ni un masquage : la session est bel et bien affichée, c'est l'un de ses signaux qui a perdu.
///
/// <para>Le relevé du 2026-09-12 est l'exemple type : un fichier de hook figé depuis 7 h annonçait
/// « à toi » pendant qu'un transcript de 10 s prouvait le contraire. Le désaccord existait, il gagnait,
/// et rien ne le disait. <see cref="EcartAge"/> est la grandeur qui rend une source FIGÉE
/// diagnosticable par l'utilisateur seul.</para>
/// </summary>
public sealed record DesaccordSources(
    string SessionId,
    SourceSession SourceRetenue, SessionActivity EtatRetenu,
    SourceSession SourceEcartee, SessionActivity EtatEcarte,
    System.TimeSpan EcartAge);

/// <summary>Ce que l'arbitrage retient, et ce qu'il a écarté en le DISANT.
/// <para><see cref="Vainqueurs"/> est la MÊME séquence que <see cref="Retenus"/>, index pour index,
/// chaque élément portant en plus la source qui a gagné. Les deux ne sont pas redondants : les filtres
/// du moniteur ne consomment que des instantanés, tandis que le détecteur de traitement a besoin de
/// savoir QUI a parlé — sans cela il lit « la session est passée d'attente à travail » là où deux
/// sources se sont relayées. Un test tient l'égalité des deux séquences, pour qu'aucune dérive ne
/// puisse s'installer entre elles.</para></summary>
public sealed record ResultatArbitrage(
    IReadOnlyList<SessionSnapshot> Retenus,
    IReadOnlyList<DesaccordSources> Desaccords,
    IReadOnlyList<SignalSession> Vainqueurs);

/// <summary>
/// FUS-01 — quand deux sources parlent de la même session, c'est la plus RÉCENTE qui gagne.
///
/// <para>Ce que ce type remplace : une fusion qui réaffectait une entrée indexée par identifiant, source
/// après source. Le dernier écrivain l'emportait, donc l'ordre du code faisait loi. Mesuré : un signal de
/// 7 heures battait un signal de 10 secondes.</para>
///
/// <para>LA DOCTRINE : un état ancien n'est pas une vérité plus solide parce qu'il est plus détaillé. La
/// précision ne bat JAMAIS la fraîcheur. Elle ne sert qu'à départager deux signaux du MÊME âge — et ce
/// départage est une règle écrite, testée et nommée, pas le hasard d'un parcours de collection.</para>
///
/// <para>Le vainqueur est le maximum d'un ordre TOTAL sur le CONTENU des signaux :
/// <list type="number">
///   <item>l'instant du signal, décroissant — LA FRAÎCHEUR ;</item>
///   <item>à âge égal, le rang de la source (la plus spécifique d'abord) ;</item>
///   <item>puis le rang d'urgence de l'état, par <see cref="AffichageSessions.Urgence"/> — appelé, jamais recopié ;</item>
///   <item>puis le motif, puis le projet, en comparaison ordinale.</item>
/// </list>
/// Ces critères épuisent tous les champs de <see cref="SessionSnapshot"/> hors l'identifiant, qui est la
/// clé de regroupement. Deux signaux encore ex aequo sont donc identiques champ pour champ : le choix ne
/// PEUT plus dépendre de l'ordre d'entrée. C'est ce qui rend le test de permutation vrai par construction
/// et non par chance.</para>
///
/// PUR : aucune E/S, aucune horloge, aucun type WPF. Les instants comparés sont ceux que les signaux
/// portent — on ne demande jamais l'heure à qui que ce soit.
/// </summary>
public static class ArbitrageSessions
{
    /// <summary>Tranche entre les signaux, session par session. Rend les retenus ET les désaccords.</summary>
    public static ResultatArbitrage Trancher(IEnumerable<SignalSession> signaux)
    {
        var retenus = new List<SessionSnapshot>();
        var desaccords = new List<DesaccordSources>();
        var vainqueurs = new List<SignalSession>();

        // Le regroupement ET la sortie sont ordonnés par identifiant : la SÉQUENCE rendue est elle aussi
        // indépendante de l'ordre d'entrée, sinon « le résultat ne dépend plus de l'ordre » ne vaudrait
        // que pour les états, pas pour les lignes.
        foreach (var groupe in signaux.GroupBy(s => s.Session.SessionId, System.StringComparer.Ordinal)
                                      .OrderBy(g => g.Key, System.StringComparer.Ordinal))
        {
            var classes = groupe.OrderBy(s => s, Comparateur).ToList();
            var vainqueur = classes[0];
            retenus.Add(vainqueur.Session);
            vainqueurs.Add(vainqueur);

            foreach (var ecarte in classes.Skip(1))
            {
                // Un DOUBLON n'est pas un désaccord : deux sources d'accord ne contredisent personne.
                if (ecarte.Session.Activity == vainqueur.Session.Activity) continue;

                desaccords.Add(new DesaccordSources(
                    groupe.Key,
                    vainqueur.Source, vainqueur.Session.Activity,
                    ecarte.Source, ecarte.Session.Activity,
                    vainqueur.Session.UpdatedAt - ecarte.Session.UpdatedAt));
            }
        }

        return new ResultatArbitrage(retenus, desaccords, vainqueurs);
    }

    private static readonly IComparer<SignalSession> Comparateur =
        Comparer<SignalSession>.Create(Departager);

    // Ordre TOTAL sur le contenu. Le rang 1 est la phase entière ; les rangs 2 à 5 n'existent que pour
    // qu'aucun ex aequo ne soit laissé à la position dans la collection.
    private static int Departager(SignalSession a, SignalSession b)
    {
        var c = b.Session.UpdatedAt.CompareTo(a.Session.UpdatedAt);   // 1. LA FRAÎCHEUR
        if (c != 0) return c;

        c = ((int)a.Source).CompareTo((int)b.Source);                 // 2. à âge ÉGAL : la plus spécifique
        if (c != 0) return c;

        c = AffichageSessions.Urgence(a.Session.Activity)             // 3. ce qui réclame une intervention
                             .CompareTo(AffichageSessions.Urgence(b.Session.Activity));
        if (c != 0) return c;

        c = System.StringComparer.Ordinal.Compare(a.Session.Reason ?? "", b.Session.Reason ?? "");
        if (c != 0) return c;                                         // 4. le motif

        return System.StringComparer.Ordinal.Compare(a.Session.Project, b.Session.Project);  // 5. le projet
    }
}
