using System.IO;

namespace Chronos.Services;

/// <summary>
/// Les deux vues d'AppData (docs/desktop-app-sessions.md §7 ; sondes des phases 29 et 31 ; recherche 32). Tout
/// processus lancé SOUS l'app bureau Claude (session, hook, dotnet test, agent) lit la copie copy-on-write du
/// paquet MSIX ; l'overlay lancé par l'Explorateur lit la vue réelle. Dans la vue réelle, %APPDATA%\Claude est
/// ABSENT : sa présence signe la vue virtualisée.
///
/// C'est ce malentendu qui a fait croire à un « gel » de last-exact.json : la copie du paquet était figée au
/// 2026-09-13 12:44:59 pendant que le fichier réel était réécrit chaque minute (sonde WMI hors arbre du
/// 2026-09-27 01:56). Le diagnostic nomme donc la vue depuis laquelle il lit — sans quoi le même constat
/// erroné se reproduira au prochain relevé fait depuis une session.
/// </summary>
public enum VueAppData
{
    /// <summary>Le vrai %APPDATA% du profil : ce que l'overlay lancé par l'Explorateur lit et écrit.</summary>
    Reelle,

    /// <summary>La copie copy-on-write du paquet de l'app bureau : ce que voit tout processus lancé sous elle.</summary>
    Virtualisee,
}

/// <summary>Détection PURE (D-32-07) : un chemin injecté, un test d'existence, aucun autre savoir sur le paquet.
/// Le nom du paquet reste dans le résolveur de racines (garde de périmètre) : ici on ne fait que nommer la vue.</summary>
public static class DetecteurVueAppData
{
    /// <param name="appData">Le dossier AppData\Roaming à examiner (en production : <c>Environment.GetFolderPath(ApplicationData)</c>).</param>
    public static VueAppData Detecter(string appData)
        => Directory.Exists(Path.Combine(appData, "Claude")) ? VueAppData.Virtualisee : VueAppData.Reelle;

    /// <summary>Libellé du diagnostic. L'avertissement n'accompagne que la vue virtualisée : dans la vue réelle, les
    /// fichiers lus sont bien ceux de l'overlay, et un avertissement permanent finirait par ne plus être lu.</summary>
    public static string Libelle(VueAppData vue) => vue == VueAppData.Reelle
        ? "réelle"
        : "virtualisée (paquet de l'app bureau) — les fichiers lus ici ne sont pas ceux de l'overlay lancé par l'Explorateur";
}
