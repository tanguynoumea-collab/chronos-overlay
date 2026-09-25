using System.IO;

namespace Chronos.Services;

/// <summary>Les racines CANDIDATES, dans l'ordre où on les essaie (APP-06) : le cache du paquet MSIX de l'app
/// bureau d'abord, la vue réelle d'AppData ensuite. <see cref="EtatsHooks"/> : fichiers d'état écrits par le mode
/// --hook (lus TOUS). <see cref="SessionsAppBureau"/> : métadonnées par session de l'app bureau (la PREMIÈRE
/// qui existe est lue — dans l'arbre de l'app les deux chemins montrent les mêmes fichiers).</summary>
public sealed record RacinesCandidates(IReadOnlyList<string> EtatsHooks, IReadOnlyList<string> SessionsAppBureau);

/// <summary>
/// OÙ CHERCHER, dit en un seul point (APP-06). Les racines ne se SUPPOSENT pas : elles se résolvent par candidats.
///
/// <para><b>Le fait, mesuré hors de l'arbre de l'app (29-SONDE-HORS-ARBRE.txt, 2026-09-25).</b> L'app bureau Claude
/// est un paquet MSIX. Les processus lancés SOUS elle — sessions Claude Code, leurs hooks, le mode --hook de Chronos —
/// voient une vue VIRTUALISÉE d'AppData : ce qu'ils écrivent dans « %APPDATA%\Chronos\sessions » atterrit
/// physiquement dans le cache privé du paquet, « %LOCALAPPDATA%\Packages\&lt;paquet&gt;\LocalCache\Roaming\… ». Ce
/// n'est pas une jonction (aucun point d'analyse) : c'est la virtualisation d'AppData de MSIX. L'overlay, lancé par
/// explorer (démarrage, raccourci), est HORS de cet arbre : pour lui « %APPDATA%\Claude » n'existe pas et
/// « %APPDATA%\Chronos\sessions » est le dossier RÉEL, vide des sessions de l'app. Le widget en production n'avait
/// donc jamais vu un fichier de hook d'une session de l'app bureau.</para>
///
/// <para><b>Pourquoi énumérer plutôt que coder le nom du paquet.</b> Le suffixe du nom de famille est un hachage de
/// l'éditeur, pas un identifiant d'utilisateur : il peut changer avec la signature, et rien n'oblige à ce qu'il n'y
/// ait qu'un paquet. On énumère donc les dossiers de paquets dont le nom commence par « Claude_ », dans l'ordre
/// ordinal (stable d'un lancement à l'autre). Un paquet au nom voisin (« ClaudeCode_… ») n'est pas candidat.</para>
///
/// <para><b>Quand.</b> Les candidats sont résolus UNE fois, au démarrage (singleton du conteneur) ; l'EXISTENCE de
/// chaque racine est testée à chaque lecture par ses consommateurs, de sorte qu'un dossier créé plus tard est lu dès
/// qu'il existe. Limite assumée : un paquet installé APRÈS le lancement de l'overlay n'est vu qu'au lancement
/// suivant.</para>
///
/// <para><b>Deux familles, deux usages.</b> Les états des hooks sont lus dans TOUTES les racines (union, fusion par
/// l'arbitrage ordinaire). Les métadonnées de l'app bureau sont lues dans la PREMIÈRE racine qui existe
/// (<see cref="PremiereExistante"/>) : dans l'arbre de l'app, les deux chemins montrent les mêmes fichiers, lire les
/// deux doublerait.</para>
///
/// <para><b>Lecture seule, et seul porteur du chemin.</b> Ce type ne crée, ne supprime ni n'ouvre rien : il nomme des
/// chemins et teste leur existence. Il est le SEUL de src/ à porter le chemin de l'app bureau : aucun code d'écriture
/// ne peut le viser sans passer par ici (APP-05, garde en 29-05) — et les racines d'état qu'il rend désignent
/// toujours « Chronos\sessions », jamais le dossier de l'app, ce qui tient le balayage à l'écart de ce dernier.
/// Aucun type WPF.</para>
/// </summary>
public static class RacinesEtat
{
    /// <summary>Les candidats des deux familles, paquets de l'app bureau d'abord (ordre ordinal), vue réelle ensuite.
    /// Ne lève jamais : un dossier des paquets absent ou illisible laisse la seule vue réelle.</summary>
    public static RacinesCandidates Candidats(string localAppData, string appData)
    {
        var paquets = new List<string>();
        try
        {
            if (!string.IsNullOrEmpty(localAppData))
            {
                var dossierPaquets = Path.Combine(localAppData, "Packages");
                if (Directory.Exists(dossierPaquets))
                {
                    // MATÉRIALISÉ dans le try : une énumération paresseuse lèverait plus loin, hors de sa garde.
                    paquets = Directory.EnumerateDirectories(dossierPaquets, "Claude_*")
                        .Where(p => (Path.GetFileName(p) ?? "").StartsWith("Claude_", System.StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => p, System.StringComparer.Ordinal)
                        .ToList();
                }
            }
        }
        catch
        {
            // Dossier des paquets illisible : la vue réelle reste.
            paquets = new List<string>();
        }

        var etats = paquets.Select(p => Path.Combine(p, "LocalCache", "Roaming", "Chronos", "sessions"))
            .Append(Path.Combine(appData, "Chronos", "sessions"));
        var appBureau = paquets.Select(p => Path.Combine(p, "LocalCache", "Roaming", "Claude", "claude-code-sessions"))
            .Append(Path.Combine(appData, "Claude", "claude-code-sessions"));

        // Distinct garde le premier vu : l'ordre « paquet d'abord » survit au dédoublonnage.
        return new RacinesCandidates(
            etats.Distinct(System.StringComparer.OrdinalIgnoreCase).ToArray(),
            appBureau.Distinct(System.StringComparer.OrdinalIgnoreCase).ToArray());
    }

    /// <summary>Les candidats de CETTE machine (dossiers de l'utilisateur, jamais d'emplacement système). Appelé une
    /// fois par la composition ; jamais par un test — ce processus-ci, lancé sous l'app bureau, voit la vue
    /// virtualisée et non celle de l'overlay.</summary>
    public static RacinesCandidates ParDefaut()
        => Candidats(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                     System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData));

    /// <summary>Le premier candidat qui existe, ou <c>null</c>. Un test d'existence qui lève compte comme « absent ».</summary>
    public static string? PremiereExistante(IReadOnlyList<string> candidats)
    {
        foreach (var c in candidats)
        {
            bool existe;
            try { existe = Directory.Exists(c); }
            catch { existe = false; }
            if (existe) return c;
        }
        return null;
    }
}
