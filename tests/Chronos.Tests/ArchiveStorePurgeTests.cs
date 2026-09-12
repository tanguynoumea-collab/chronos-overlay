using System.IO;
using System.Text.Json;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// SRC-02 — les entrées fantômes de l'ancienne source app-bureau sont RETIRÉES du fichier d'archives, pas
/// seulement écartées à la lecture.
///
/// Contenu RÉEL relevé le 2026-09-12 dans %APPDATA%\Chronos\archived.json (84 octets, mtime du 12 juillet) :
/// deux entrées « desktop:foreground:… » et rien d'autre. Elles prouvent que l'utilisateur avait dû
/// archiver ces fantômes À LA MAIN : leur horodatage étant rafraîchi à chaque poll, elles ne vieillissaient
/// jamais et ne pouvaient jamais expirer. Les ignorer à la lecture laisse son contournement gravé dans ses
/// données ; les retirer l'efface.
///
/// Tous les tests écrivent dans un fichier TEMPORAIRE : aucun ne touche le vrai %APPDATA%\Chronos.
/// </summary>
public class ArchiveStorePurgeTests
{
    private static string TempFichier()
    {
        var d = Path.Combine(Path.GetTempPath(), "chronos-arch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        var f = Path.Combine(d, "archived.json");
        Assert.StartsWith(Path.GetTempPath(), f);
        return f;
    }

    // Le contenu EXACT mesuré sur la machine de l'utilisateur le 2026-09-12.
    private const string ContenuReel =
        """{"desktop:foreground:unknown":1783867136065,"desktop:foreground:code":1783867137990}""";

    [Fact]
    public void Les_deux_fantomes_mesures_sont_retires_du_fichier()
    {
        var f = TempFichier();
        File.WriteAllText(f, ContenuReel);

        var retires = new ArchiveStore(f).PurgerPrefixe("desktop:");

        Assert.Equal(2, retires);
        using var doc = JsonDocument.Parse(File.ReadAllText(f));
        Assert.Empty(doc.RootElement.EnumerateObject());              // le fichier est vidé, pas supprimé
        Assert.Empty(new ArchiveStore(f).Load());
    }

    [Fact]
    public void Une_session_Claude_Code_archivee_survit_intacte()
    {
        var f = TempFichier();
        var recent = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        File.WriteAllText(f, $$"""{"e465420e-83e0-428f-97f1-f0174c0848fc":{{recent}},"desktop:foreground:code":1783867137990}""");

        Assert.Equal(1, new ArchiveStore(f).PurgerPrefixe("desktop:"));

        using var doc = JsonDocument.Parse(File.ReadAllText(f));
        Assert.Equal(recent, doc.RootElement.GetProperty("e465420e-83e0-428f-97f1-f0174c0848fc").GetInt64());
        Assert.False(doc.RootElement.TryGetProperty("desktop:foreground:code", out _));
        Assert.Contains("e465420e-83e0-428f-97f1-f0174c0848fc", new ArchiveStore(f).Load());
    }

    [Fact]
    public void Purger_n_est_pas_expirer_une_entree_vieille_reste_dans_le_fichier()
    {
        var f = TempFichier();
        var vieux = DateTimeOffset.UtcNow.AddHours(-7).ToUnixTimeMilliseconds();   // au-delà du TTL de 6 h
        File.WriteAllText(f, $$"""{"une-vieille-session":{{vieux}},"desktop:session:X":1783867137990}""");

        Assert.Equal(1, new ArchiveStore(f).PurgerPrefixe("desktop:"));

        using var doc = JsonDocument.Parse(File.ReadAllText(f));
        Assert.True(doc.RootElement.TryGetProperty("une-vieille-session", out var v));
        Assert.Equal(vieux, v.GetInt64());                              // la purge ne l'a PAS touchée…
        Assert.DoesNotContain("une-vieille-session", new ArchiveStore(f).Load());  // …et Load ne la rend pas
    }

    [Fact]
    public void Sans_fantome_le_fichier_de_l_utilisateur_n_est_pas_reecrit()
    {
        var f = TempFichier();
        File.WriteAllText(f, """{"une-session":1783867137990}""");
        var avant = new DateTime(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(f, avant);

        Assert.Equal(0, new ArchiveStore(f).PurgerPrefixe("desktop:"));

        Assert.Equal(avant, File.GetLastWriteTimeUtc(f));   // pas touché : rien à faire = ne rien faire
    }

    [Fact]
    public void Fichier_absent_ou_illisible_rend_zero_sans_exception()
    {
        var absent = TempFichier();                       // créé nulle part : seul le dossier existe
        Assert.Equal(0, new ArchiveStore(absent).PurgerPrefixe("desktop:"));

        var corrompu = TempFichier();
        File.WriteAllText(corrompu, "ceci n'est pas du JSON {{{");
        Assert.Equal(0, new ArchiveStore(corrompu).PurgerPrefixe("desktop:"));

        var tableau = TempFichier();
        File.WriteAllText(tableau, "[1,2,3]");            // JSON valide, mais pas un objet
        Assert.Equal(0, new ArchiveStore(tableau).PurgerPrefixe("desktop:"));
    }

    [Fact]
    public void Un_prefixe_vide_ne_purge_rien()
    {
        // Garde-fou : un préfixe vide correspondrait à TOUTES les entrées et viderait le fichier de
        // l'utilisateur. Ce n'est jamais une intention légitime.
        var f = TempFichier();
        File.WriteAllText(f, """{"une-session":1783867137990}""");

        Assert.Equal(0, new ArchiveStore(f).PurgerPrefixe(""));
        Assert.Contains("une-session", File.ReadAllText(f));
    }
}
