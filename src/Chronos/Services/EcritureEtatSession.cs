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

            var octets = new System.Text.UTF8Encoding(false).GetBytes(resultat.StateJson);
            using var flux = new FileStream(fichier, FileMode.Create, FileAccess.Write, FileShare.Read);
            flux.Write(octets, 0, octets.Length);
            flux.Flush();
            return ResultatEcritureEtat.Reussie;
        }
        catch (System.Exception ex)
        {
            // Jamais de relance : un hook ne doit pas casser la session Claude Code. Mais plus jamais de
            // silence non plus — l'appelant reçoit de quoi le dire.
            return ResultatEcritureEtat.Echouee(ex.GetType().Name + " : " + ex.Message);
        }
    }
}
