using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// CPT-03 — « N processus Chronos » pour le diagnostic, en PUR : listes injectées, horloge fixe, aucun
/// <c>Process.GetProcesses()</c> ici (le relevé réel est <c>InventaireProcessus.Relever()</c>, constaté en 32-08).
///
/// <para>POURQUOI un préfixe : le nom réel d'un overlay est <c>Chronos-v3.2.2</c> (exe versionné), jamais
/// <c>Chronos</c> seul — un filtre par égalité ne verrait rien. POURQUOI dater : les hooks <c>--hook</c> apparaissent
/// ~100 ms sous le même nom ; on les signale par leur âge au lieu de les compter comme des overlays.</para>
/// </summary>
public sealed class InventaireProcessusTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

    private static ProcessusChronos P(string nom, int pid, TimeSpan? age)
        => new(nom, pid, age is { } a ? Now - a : null);

    [Fact]
    public void Le_filtre_par_prefixe_retient_les_exe_versionnes_et_ecarte_les_homonymes()
    {
        var tous = new[]
        {
            new ProcessusChronos("Chronos-v3.2.2", 10, null),
            new ProcessusChronos("Chronos-v3.1.0", 11, null),
            new ProcessusChronos("chronos-v3.2.2", 12, null),   // casse différente : retenu (OrdinalIgnoreCase)
            new ProcessusChronos("ChronosViewer",  13, null),   // homonyme par préfixe : retenu, le diagnostic le montrera tel quel
            new ProcessusChronos("claude",         14, null),
            new ProcessusChronos("Chrono",         15, null),   // trop court : pas le préfixe
        };

        var retenus = InventaireProcessus.Filtrer(tous);

        Assert.Equal(new[] { 10, 11, 12, 13 }, retenus.Select(p => p.Pid).ToArray());
    }

    [Fact]
    public void Les_lignes_comptent_les_autres_sans_le_pid_courant_et_datent_chacun()
    {
        var processus = new[]
        {
            P("Chronos-v3.1.0", 1,  TimeSpan.FromDays(4)),
            P("Chronos-v3.2.2", 2,  TimeSpan.FromMinutes(2)),
            P("Chronos-v3.2.2", 99, TimeSpan.FromSeconds(5)),   // ce processus
        };

        var lignes = InventaireProcessus.Lignes(processus, pidCourant: 99, Now, etatVerrou: "tenu par ce processus");

        Assert.Equal("Processus Chronos : 3 (dont ce processus) — 2 autre(s)", lignes[0]);
        Assert.Contains("Chronos-v3.1.0#1 — démarré il y a 4 j", lignes);
        Assert.Contains("Chronos-v3.2.2#2 — démarré il y a 2 min", lignes);
        Assert.Contains("Chronos-v3.2.2#99 — ce processus, démarré à l'instant", lignes);
        Assert.DoesNotContain(lignes, l => l.Contains("hooks", StringComparison.Ordinal));   // ce processus n'est pas un hook
    }

    [Fact]
    public void Les_processus_de_moins_de_dix_secondes_sont_signales_comme_hooks_probables()
    {
        var processus = new[]
        {
            P("Chronos-v3.2.2", 7,   TimeSpan.FromHours(1)),     // ce processus, l'overlay
            P("Chronos-v3.2.2", 301, TimeSpan.FromSeconds(3)),
            P("Chronos-v3.2.2", 302, TimeSpan.FromSeconds(8)),
        };

        var lignes = InventaireProcessus.Lignes(processus, pidCourant: 7, Now, etatVerrou: "tenu par ce processus");

        Assert.Equal("Processus Chronos : 3 (dont ce processus) — 2 autre(s)", lignes[0]);
        Assert.Contains("dont 2 de moins de 10 s : hooks --hook probables (éphémères)", lignes);
    }

    [Fact]
    public void Un_age_inconnu_est_dit_inconnu_et_l_etat_du_verrou_est_ecrit()
    {
        var processus = new[]
        {
            P("Chronos-v3.2.1", 5, null),   // StartTime refusé (autre utilisateur) : Demarrage = null
        };

        var lignes = InventaireProcessus.Lignes(processus, pidCourant: 42, Now, etatVerrou: "libre");

        Assert.Equal("Processus Chronos : 1 — 1 autre(s)", lignes[0]);   // pid courant absent de la liste : pas de « dont ce processus »
        Assert.Contains("Chronos-v3.2.1#5 — démarré : date inconnue", lignes);
        Assert.Equal(@"Verrou mono-instance (Local\Chronos-overlay) : libre", lignes[^1]);
    }
}
