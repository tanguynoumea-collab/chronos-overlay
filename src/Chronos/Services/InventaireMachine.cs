using System.IO;

namespace Chronos.Services;

/// <summary>
/// Implémentation RÉELLE de <see cref="IInventaireMachine"/> : le code des deux sondages chers a été
/// DÉPLACÉ TEL QUEL depuis <c>DiagnosticService</c> (phase 20, vague 0), pas réécrit. Les bornes du
/// balayage (profondeur 3, 5 résultats, liste noire de dossiers volumineux, fichiers &gt; 2 Mo ignorés)
/// sont le fruit d'un réglage, pas du hasard : les toucher ferait dériver le coût mesuré.
///
/// AUCUNE MÉMOÏSATION ici, à dessein : un diagnostic qui met en cache son inventaire d'environnement
/// est un diagnostic qui peut mentir. Le gain de temps de la suite vient de la SUBSTITUTION sous test,
/// jamais d'un cache en production.
/// </summary>
public sealed class InventaireMachine : IInventaireMachine
{
    // Cherche (profondeur bornée, dossiers volumineux ignorés) les fichiers config.json contenant
    // « oauth:tokenCache » → révèle où l'app bureau range son coffre, quel que soit son nom/emplacement.
    public IReadOnlyList<string> CoffresOAuth(string racine)
    {
        var results = new List<string>();
        void Scan(string dir, int depth)
        {
            if (depth > 3 || results.Count >= 5) return;
            string[] subdirs;
            try { subdirs = Directory.GetDirectories(dir); } catch { return; }
            try
            {
                foreach (var f in Directory.EnumerateFiles(dir, "config.json"))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        if (fi.Length > 2_000_000) continue;                 // pas un config.json d'app
                        if (File.ReadAllText(f).Contains("oauth:tokenCache")) { results.Add(f); if (results.Count >= 5) return; }
                    }
                    catch { }
                }
            }
            catch { }
            foreach (var sub in subdirs)
            {
                var n = Path.GetFileName(sub).ToLowerInvariant();
                if (n is "cache" or "gpucache" or "code cache" or "node_modules" or "blob_storage"
                      or "logs" or "crashpad" or "dawncache" or "service worker") continue; // bruit volumineux
                Scan(sub, depth + 1);
                if (results.Count >= 5) return;
            }
        }
        Scan(racine, 0);
        return results;
    }

    // Poll one-shot de la VRAIE source UIA, hors thread UI — vérité-terrain de ce que voit réellement
    // DesktopUiaSessionSource. Sessions et santé rendues ENSEMBLE : un second appel re-paierait le poll.
    public (IReadOnlyList<SessionSnapshot> Sessions, DesktopHealth Sante) SessionsBureau(DateTimeOffset now)
    {
        var desktop = new DesktopUiaSessionSource(new WindowsUiaTreeProvider());
        desktop.Poll(now);
        return (desktop.Read(now), desktop.Health);
    }
}
