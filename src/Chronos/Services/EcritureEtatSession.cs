using System.IO;

namespace Chronos.Services;

/// <summary>
/// Ce qu'une tentative d'écriture d'état a RÉELLEMENT produit. Un booléen seul dirait qu'il s'est passé
/// quelque chose sans dire quoi : la <see cref="Cause"/> est la seule chose qui distingue « rien à faire »
/// d'« impossible » (même motif qu'au plan 17-02, où le rafraîchissement de jeton a cessé de rendre un null
/// muet). Elle n'est jamais fabriquée : c'est le type et le message de l'exception réellement levée.
///
/// <para><see cref="Ignoree"/> nomme un TROISIÈME sort, apparu avec la monotonie (R3, cran 1) : l'écriture
/// n'a pas eu lieu, et c'est très bien ainsi — un état PLUS RÉCENT occupait déjà la place. Du point de vue
/// de l'appelant, c'est un succès : <see cref="Reussi"/> reste vrai, donc aucun message d'erreur ne remonte
/// à l'utilisateur pour une écriture légitimement écartée. Confondre ce sort avec un échec afficherait un
/// avertissement à chaque lot d'appels d'outil.</para>
/// </summary>
public sealed record ResultatEcritureEtat(bool Reussi, string? Cause, bool Ignoree = false)
{
    public static ResultatEcritureEtat Reussie { get; } = new(true, null);
    public static ResultatEcritureEtat Echouee(string cause) => new(false, cause);

    /// <summary>Écriture écartée parce qu'un état plus récent est déjà en place. Succès, pas échec.</summary>
    public static ResultatEcritureEtat IgnoreeCarPerimee(string cause) => new(true, cause, Ignoree: true);
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

            var instant = InstantDeLEtat(resultat.StateJson);
            var deja = EcrireAvecReprise(fichier, new System.Text.UTF8Encoding(false).GetBytes(resultat.StateJson), instant);

            return deja is null
                ? ResultatEcritureEtat.Reussie
                : ResultatEcritureEtat.IgnoreeCarPerimee(
                      $"état antérieur à celui déjà présent ({instant} contre {deja}) : écriture écartée");
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

    /// <summary>Taille au-delà de laquelle on ne relit plus la cible avant d'écrire : un fichier d'état
    /// pèse quelques centaines d'octets, et rien ne justifie de charger davantage pour y chercher un
    /// champ. Au-delà, la cible est traitée comme illisible — donc comme une absence de signal.</summary>
    private const int TailleRelueMax = 65_536;

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
    ///
    /// <para><b>MON-01 — L'ÉCRITURE EST MONOTONE (R3, cran 1).</b> Sérialiser les écrivains ne suffit pas :
    /// il fallait encore décider LEQUEL gagne. L'instant d'un hook est capturé à l'ENTRÉE de son processus,
    /// et la reprise bornée ci-dessous peut différer l'écriture de jusqu'à soixante essais — un
    /// <c>PreToolUse</c> parti AVANT un <c>PermissionRequest</c> pouvait donc écrire APRÈS lui, avec un
    /// horodatage plus ancien. L'horodatage du fichier d'état RECULAIT, « à toi » redevenait « en cours »,
    /// et le détecteur de traitement lisait cet effacement comme une réponse : la session qui attendait
    /// réellement une permission devenait invisible. La doctrine est donc étendue d'un cran —
    /// <b>FUS-01 vaut aussi à l'INTÉRIEUR d'une source</b> : un signal n'en écrase un autre que s'il est
    /// plus récent. Une écriture STRICTEMENT antérieure à l'état présent est écartée ; une écriture du même
    /// instant passe, sans quoi deux hooks du même millième de seconde se bloqueraient l'un l'autre.</para>
    ///
    /// <para>La relecture se fait sur le MÊME descripteur, donc sous le MÊME verrou que l'écriture :
    /// relire puis rouvrir ajouterait la course qu'on cherche à retirer. Et ce qui n'est PAS couvert doit
    /// être dit : la <b>suppression</b> (<c>SessionEnd</c>) reste inconditionnelle — elle ne porte pas
    /// d'état à comparer.</para>
    /// </summary>
    /// <param name="instant">Horodatage porté par l'état à écrire, ou <c>null</c> s'il est illisible.</param>
    /// <returns><c>null</c> si l'écriture a eu lieu ; sinon l'horodatage de l'état déjà présent qui l'a
    /// emporté.</returns>
    private static long? EcrireAvecReprise(string fichier, byte[] octets, long? instant)
    {
        for (var essai = 1; ; essai++)
        {
            try
            {
                using var flux = new FileStream(fichier, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

                if (instant is long neuf && InstantDejaSurDisque(flux) is long present && neuf < present)
                    return present;

                flux.Position = 0;
                flux.Write(octets, 0, octets.Length);
                flux.SetLength(octets.Length);   // la cible pouvait être plus longue : pas de queue orpheline
                flux.Flush();
                return null;
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

    /// <summary>
    /// L'horodatage porté par un contenu d'état, ou <c>null</c> s'il n'y en a pas de lisible.
    ///
    /// <para><b>Le null est la pièce maîtresse de la doctrine de la phase 23</b>, tenue ici à l'écriture
    /// comme elle l'est à la lecture : un fichier illisible, un fragment, un objet sans ce champ sont une
    /// ABSENCE de signal — pas un signal très ancien, et surtout pas un signal très récent. Rendre autre
    /// chose que <c>null</c> pour une relecture ratée gèlerait la cible : plus aucune écriture ne
    /// passerait, et un fichier corrompu le resterait pour de bon.</para>
    /// </summary>
    private static long? InstantDeLEtat(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return Champ(doc.RootElement);
        }
        catch { return null; }
    }

    /// <summary>Même lecture, mais sur le descripteur DÉJÀ OUVERT en écriture — c'est ce qui met la
    /// comparaison et l'écriture sous un seul et même verrou.</summary>
    private static long? InstantDejaSurDisque(FileStream flux)
    {
        try
        {
            var taille = flux.Length;
            if (taille <= 0 || taille > TailleRelueMax) return null;   // vide ou aberrant = absence

            var tampon = new byte[(int)taille];
            flux.Position = 0;
            var lus = 0;
            while (lus < tampon.Length)
            {
                var n = flux.Read(tampon, lus, tampon.Length - lus);
                if (n <= 0) break;
                lus += n;
            }

            using var doc = System.Text.Json.JsonDocument.Parse(new System.ReadOnlyMemory<byte>(tampon, 0, lus));
            return Champ(doc.RootElement);
        }
        catch { return null; }   // fragment, contenu étranger, lecture refusée : absence de signal
    }

    /// <summary>Le champ d'horodatage du schéma d'état, lu tel quel : aucune conversion d'unité ici, on
    /// compare des entiers de même nature entre eux (HDR-05 vaut aussi pour ce qu'on NE fait pas).</summary>
    private static long? Champ(System.Text.Json.JsonElement racine)
        => racine.ValueKind == System.Text.Json.JsonValueKind.Object
           && racine.TryGetProperty("updated_at", out var v)
           && v.TryGetInt64(out var ms)
            ? ms
            : null;
}
