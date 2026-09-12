using System.IO;

namespace Chronos.Services;

/// <summary>
/// Ce qu'une tentative d'écriture d'état a RÉELLEMENT produit. Un booléen seul dirait qu'il s'est passé
/// quelque chose sans dire quoi : la <see cref="Cause"/> est la seule chose qui distingue « rien à faire »
/// d'« impossible » (même motif qu'au plan 17-02, où le rafraîchissement de jeton a cessé de rendre un null
/// muet). Elle n'est jamais fabriquée : c'est le type et le message de l'exception réellement levée.
/// </summary>
public sealed record ResultatEcritureEtat(bool Reussi, string? Cause)
{
    public static ResultatEcritureEtat Reussie { get; } = new(true, null);
    public static ResultatEcritureEtat Echouee(string cause) => new(false, cause);
}

/// <summary>
/// CYC-02 — applique au disque l'ordre rendu par <see cref="SessionHookProcessor"/>, et rend le résultat
/// au lieu de l'avaler.
///
/// <para>Deux défauts distincts sont corrigés ici. Le premier est mécanique : l'écriture passait par un
/// fichier temporaire déplacé par-dessus la cible, et ce déplacement échoue en
/// <c>UnauthorizedAccessException</c> dès qu'un lecteur tient le fichier — or
/// <see cref="SessionMonitor"/> le lit toutes les deux secondes en partage lecture-écriture. Mesuré le
/// 2026-09-12 : 290 écritures perdues sur 500 ; élargir le partage côté lecteur ne corrige pas (200 sur
/// 500) ; l'écriture directe : 0 sur 500. Les fichiers temporaires abandonnés étaient indexés par
/// identifiant de processus, donc ils se recouvraient : les 12 orphelins relevés SOUS-ESTIMENT massivement
/// le nombre d'écritures perdues.</para>
///
/// <para>Le second est le silence : l'échec disparaissait dans un bloc de capture vide. Ici il est RENDU.</para>
///
/// <para>Contrepartie ASSUMÉE et mesurable : l'écriture directe tronque la cible avant de la réécrire, donc
/// un lecteur malchanceux peut lire un fragment. Le moniteur ignore un fichier illisible et le relira deux
/// secondes plus tard. Perdre un état pendant un cycle de lecture n'est pas du même ordre que le perdre
/// définitivement.</para>
///
/// <para>Ce code vit dans <c>Services/</c> et non dans <c>App.xaml.cs</c> pour une raison précise : le point
/// d'entrée WPF n'est pas instanciable sous test, et c'est là que ce défaut a vécu sans couverture.</para>
/// </summary>
public static class EcritureEtatSession
{
    public static ResultatEcritureEtat Appliquer(string dossier, SessionHookResult resultat)
    {
        // Rien à faire n'est pas un échec (précédent ArchiveStore.PurgerPrefixe).
        if (resultat.Ignore || string.IsNullOrEmpty(resultat.SessionId)) return ResultatEcritureEtat.Reussie;

        try
        {
            Directory.CreateDirectory(dossier);
            var fichier = Path.Combine(dossier, resultat.SessionId + ".json");

            if (resultat.Delete)
            {
                if (File.Exists(fichier)) File.Delete(fichier);   // déjà absent = déjà dans l'état voulu
                return ResultatEcritureEtat.Reussie;
            }
            if (resultat.StateJson is null) return ResultatEcritureEtat.Reussie;

            EcrireAvecReprise(fichier, new System.Text.UTF8Encoding(false).GetBytes(resultat.StateJson));
            return ResultatEcritureEtat.Reussie;
        }
        catch (System.Exception ex)
        {
            // Jamais de relance : un hook ne doit pas casser la session Claude Code. Mais plus jamais de
            // silence non plus — l'appelant reçoit de quoi le dire.
            return ResultatEcritureEtat.Echouee(ex.GetType().Name + " : " + ex.Message);
        }
    }

    /// <summary>Nombre d'essais AU TOTAL. Borné, et petit : ce code est sur le chemin critique d'un hook
    /// BLOQUANT dont le délai de grâce est de trois secondes. Un budget non borné transformerait une
    /// contention en gel de l'appel d'outil — exactement ce que le délai court cherche à éviter.</summary>
    private const int EssaisMax = 60;

    /// <summary>
    /// EVT-03 — LA PARADE AUX ÉCRIVAINS CONCURRENTS, et la raison pour laquelle elle existe.
    ///
    /// <para>Avant les battements de cœur, l'écrivain unique était GARANTI : les événements câblés étaient
    /// tous des signaux de cycle de vie, un à la fois. Il ne l'est plus. Claude Code lance jusqu'à cinq
    /// processus de hook en parallèle par événement, et les appels d'outil parallèles existent — donc
    /// plusieurs processus ouvrent le MÊME fichier d'état au même instant. Le partage retenu ci-dessous
    /// admet les lecteurs mais EXCLUT tout autre écrivain : le second arrivant se voit refuser l'accès.</para>
    ///
    /// <para><b>Ce qui a été mesuré, et non supposé</b> (huit écrivains, cinquante battements chacun, sur
    /// la même session) : sans parade, 288 écritures refusées sur 400. Avec une reprise UNIQUE et
    /// immédiate — la parade d'abord retenue — encore 209 puis 180 sur 400 : elle ne corrige pas, parce
    /// qu'elle retente pendant que le détenteur écrit toujours. Il faut donc CÉDER LA MAIN entre deux
    /// essais, et s'y reprendre plus d'une fois. Avec la reprise bornée ci-dessous : zéro sur 400.</para>
    ///
    /// <para><b>Pourquoi pas un partage élargi en écriture.</b> Deux écrivains entrelacés produiraient un
    /// FRAGMENT là où il y avait un état — et la phase 23 a posé que « un fragment = une absence ». On
    /// troquerait un refus visible contre une perte silencieuse. Le partage reste donc exclusif entre
    /// écrivains, et l'attente est ce qui les sérialise.</para>
    ///
    /// <para><b>Et surtout pas un fichier temporaire déplacé par-dessus la cible</b> : c'est la mécanique
    /// que la phase 23 a retirée après avoir mesuré 290 pertes sur 500. Elle ne revient pas.</para>
    /// </summary>
    private static void EcrireAvecReprise(string fichier, byte[] octets)
    {
        for (var essai = 1; ; essai++)
        {
            try
            {
                using var flux = new FileStream(fichier, FileMode.Create, FileAccess.Write, FileShare.Read);
                flux.Write(octets, 0, octets.Length);
                flux.Flush();
                return;
            }
            catch (IOException) when (essai < EssaisMax)
            {
                // Céder la main, sans jamais dormir longtemps : les premiers essais rendent simplement leur
                // tranche de temps (une écriture concurrente dure des microsecondes), les suivants
                // attendent réellement, le temps qu'un détenteur obstiné relâche la cible.
                if (essai <= 12) System.Threading.Thread.Yield();
                else System.Threading.Thread.Sleep(1);
            }
        }
    }
}
