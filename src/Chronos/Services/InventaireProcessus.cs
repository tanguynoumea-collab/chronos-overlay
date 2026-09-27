using System.Diagnostics;
using Chronos.Text;

namespace Chronos.Services;

/// <summary>Un processus Chronos relevé sur la machine. <see cref="Demarrage"/> est nul quand l'OS refuse
/// <c>StartTime</c> (processus d'un autre utilisateur, ou déjà terminé entre l'énumération et la lecture).</summary>
public sealed record ProcessusChronos(string Nom, int Pid, DateTimeOffset? Demarrage);

/// <summary>
/// CPT-03 — « N processus Chronos » pour le diagnostic (lignes câblées par 32-05, constatées en vrai par 32-08).
///
/// <para>POURQUOI un préfixe : le nom réel est <c>Chronos-v3.2.2</c> (exe versionné), une recherche par nom EXACT
/// (« Chronos ») ne verrait rien. POURQUOI dater : les hooks <c>--hook</c> apparaissent ~100 ms sous le même nom ; on les signale
/// par leur âge (« dont N de moins de 10 s ») plutôt que de les compter comme des overlays. Vérifié sans droit admin
/// le 2026-09-27 : trois overlays (3.1.0, 3.2.0, 3.2.1) + deux hooks éphémères.</para>
///
/// <para>Séparation volontaire : <see cref="Relever"/> touche la machine et ne lève jamais ; <see cref="Filtrer"/> et
/// <see cref="Lignes"/> sont PURES (listes injectées, horloge fournie) et c'est elles que les tests prouvent.</para>
/// </summary>
public static class InventaireProcessus
{
    public const string Prefixe = "Chronos";

    /// <summary>En dessous de cet âge, un processus Chronos est très probablement un hook en cours, pas un overlay.</summary>
    public static readonly TimeSpan SeuilEphemere = TimeSpan.FromSeconds(10);

    /// <summary>LE prédicat, unique : par PRÉFIXE de nom, insensible à la casse (jamais par égalité : Pitfall 6).
    /// <see cref="Filtrer"/> et <see cref="Relever"/> passent tous deux par lui — un seul endroit à faire dériver.</summary>
    private static bool EstChronos(string nom) => nom.StartsWith(Prefixe, StringComparison.OrdinalIgnoreCase);

    /// <summary>Filtre pur d'une liste injectée (testé) ; même prédicat que le relevé réel.</summary>
    public static IReadOnlyList<ProcessusChronos> Filtrer(IEnumerable<ProcessusChronos> tous)
        => tous.Where(p => EstChronos(p.Nom)).ToList();

    /// <summary>
    /// Relevé RÉEL : <c>Process.GetProcesses()</c> filtré par préfixe ; <c>StartTime</c> sous try/catch
    /// (<c>Win32Exception</c> pour un processus d'un autre utilisateur, <c>InvalidOperationException</c> pour un
    /// processus déjà terminé → <c>Demarrage = null</c>, dit « date inconnue »). Ne lève JAMAIS : un diagnostic qui
    /// planterait sur un inventaire serait pire qu'un diagnostic sans inventaire.
    /// </summary>
    public static IReadOnlyList<ProcessusChronos> Relever()
    {
        Process[] tous;
        try { tous = Process.GetProcesses(); }
        catch { return Array.Empty<ProcessusChronos>(); }

        var releves = new List<ProcessusChronos>();
        foreach (var p in tous)
        {
            try
            {
                string nom;
                try { nom = p.ProcessName; } catch { continue; }
                if (!EstChronos(nom)) continue;

                DateTimeOffset? demarrage;
                try { demarrage = new DateTimeOffset(p.StartTime); }   // heure locale → offset local, comparable à un now UTC
                catch { demarrage = null; }

                releves.Add(new ProcessusChronos(nom, p.Id, demarrage));
            }
            finally { p.Dispose(); }
        }
        return releves;
    }

    /// <summary>
    /// Lignes du diagnostic (sans indentation), PUR. <paramref name="pidCourant"/> est exclu du compte des « autres » ;
    /// les âges passent par <see cref="LibelleSource.Anciennete"/> ; <paramref name="etatVerrou"/> est fourni par
    /// l'appelant (<c>VerrouInstanceUnique.EtatPourDiagnostic(VerrouInstanceUnique.NomOverlay)</c>) pour que cette
    /// méthode reste sans effet de bord. Les processus de moins de <see cref="SeuilEphemere"/> — hors ce processus —
    /// sont annoncés comme hooks probables.
    /// </summary>
    public static IReadOnlyList<string> Lignes(IReadOnlyList<ProcessusChronos> processus, int pidCourant, DateTimeOffset now, string etatVerrou)
    {
        var lignes = new List<string>();

        bool contientCourant = processus.Any(p => p.Pid == pidCourant);
        int autres = processus.Count(p => p.Pid != pidCourant);
        lignes.Add(contientCourant
            ? $"Processus Chronos : {processus.Count} (dont ce processus) — {autres} autre(s)"
            : $"Processus Chronos : {processus.Count} — {autres} autre(s)");

        foreach (var p in processus)
        {
            var age = p.Demarrage is null ? ": date inconnue" : LibelleSource.Anciennete(p.Demarrage, now);
            lignes.Add(p.Pid == pidCourant
                ? $"{p.Nom}#{p.Pid} — ce processus, démarré {age}"
                : $"{p.Nom}#{p.Pid} — démarré {age}");
        }

        int ephemeres = processus.Count(p => p.Pid != pidCourant && p.Demarrage is { } d && now - d < SeuilEphemere);
        if (ephemeres > 0)
            lignes.Add($"dont {ephemeres} de moins de 10 s : hooks --hook probables (éphémères)");

        lignes.Add($"Verrou mono-instance ({VerrouInstanceUnique.NomOverlay}) : {etatVerrou}");
        return lignes;
    }
}
