using System.IO;
using System.Linq;
using System.Text;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Prouve la passerelle UNIQUE de lecture/écriture de <c>~/.claude/settings.json</c> (DATA-4, FIAB-8, MAT-1) :
/// une ignorance (fichier vide, illisible, verrouillé, modifié entre-temps, lien symbolique) ne devient jamais
/// une écriture dans la configuration de Claude Code.
///
/// <para><b>GARDE ANTI-ACCIDENT.</b> Chaque passerelle construite ici reçoit un fichier de réglages ET un dossier
/// de sauvegarde sous <c>Path.GetTempPath()</c>, et chaque test le vérifie par <c>Assert.StartsWith</c>. Le vrai
/// fichier de l'utilisateur est hors d'atteinte : <c>ParDefaut()</c> n'est jamais appelé dans les tests.</para>
/// </summary>
public class PasserelleReglagesClaudeTests
{
    private sealed class Montage : IDisposable
    {
        public string Dossier { get; } = Path.Combine(Path.GetTempPath(), "ChronosPasserelle_" + Guid.NewGuid().ToString("N"));
        public string Settings => Path.Combine(Dossier, "settings.json");
        public string Backups => Path.Combine(Dossier, "backups");
        public PasserelleReglagesClaude Passerelle { get; }

        public Montage()
        {
            Directory.CreateDirectory(Dossier);
            Passerelle = new PasserelleReglagesClaude(Settings, Backups);
            Assert.StartsWith(Path.GetTempPath(), Passerelle.SettingsPath);
            Assert.StartsWith(Path.GetTempPath(), Passerelle.BackupDir);
        }

        public string[] Sauvegardes()
            => Directory.Exists(Backups) ? Directory.GetFiles(Backups, "claude-settings-*.json") : Array.Empty<string>();

        public string[] Temporaires() => Directory.GetFiles(Dossier, "*.tmp-*");

        public void Dispose()
        {
            try { Directory.Delete(Dossier, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Fichier_absent_donne_Absent_sans_texte()
    {
        using var m = new Montage();

        var l = m.Passerelle.Lire();

        Assert.Equal(IssueLectureClaude.Absent, l.Issue);
        Assert.Null(l.Texte);
    }

    [Fact]
    public void Fichier_lisible_donne_Lu_avec_texte_exact_mtime_et_taille()
    {
        using var m = new Montage();
        File.WriteAllText(m.Settings, """{"a":1}""");

        var l = m.Passerelle.Lire();

        Assert.Equal(IssueLectureClaude.Lu, l.Issue);
        Assert.Equal("""{"a":1}""", l.Texte);
        Assert.Equal(File.GetLastWriteTimeUtc(m.Settings), l.MtimeUtc);
        Assert.Equal(new FileInfo(m.Settings).Length, l.Taille);
    }

    /// <summary>DATA-4 : un fichier EXISTANT mais vide ou blanc n'est pas un « objet vide ». C'est ce que voit un
    /// lecteur qui tombe pendant une réécriture tronquante de Claude Code : le réécrire effacerait permissions/deny.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("\n  \n")]
    [InlineData("{ pas du json")]
    [InlineData("[1,2,3]")]
    public void Contenu_vide_blanc_ou_invalide_donne_Inexploitable_et_rien_n_est_ecrit(string contenu)
    {
        using var m = new Montage();
        File.WriteAllText(m.Settings, contenu);
        var avant = File.ReadAllBytes(m.Settings);

        var l = m.Passerelle.Lire();
        Assert.Equal(IssueLectureClaude.Inexploitable, l.Issue);
        Assert.False(string.IsNullOrWhiteSpace(l.Cause));

        var e = m.Passerelle.Ecrire(l, "{}", creerSiAbsent: true);

        Assert.False(e.Ecrit);
        Assert.False(string.IsNullOrWhiteSpace(e.Cause));
        Assert.Equal(avant, File.ReadAllBytes(m.Settings));
        Assert.Empty(m.Sauvegardes());
    }

    [Fact]
    public void Fichier_tenu_en_exclusif_donne_Inexploitable_sans_lever()
    {
        using var m = new Montage();
        File.WriteAllText(m.Settings, """{"a":1}""");

        LectureReglagesClaude l;
        using (new FileStream(m.Settings, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            l = m.Passerelle.Lire();

        Assert.Equal(IssueLectureClaude.Inexploitable, l.Issue);
        Assert.Contains("Exception", l.Cause);
    }

    /// <summary>La sauvegarde est le TEXTE LU (pas une copie disque faite plus tard, qui pourrait déjà porter
    /// l'écriture d'un autre) ; écriture atomique sans temporaire résiduel.</summary>
    [Fact]
    public void Lu_puis_Ecrire_sauvegarde_le_texte_lu_et_ecrit_le_nouveau()
    {
        using var m = new Montage();
        const string original = "{\n  \"permissions\": { \"deny\": [\"Bash(rm:*)\"] }\n}";
        File.WriteAllText(m.Settings, original);

        var l = m.Passerelle.Lire();
        var e = m.Passerelle.Ecrire(l, """{"nouveau":true}""", creerSiAbsent: false);

        Assert.True(e.Ecrit, e.Cause);
        Assert.NotNull(e.Sauvegarde);
        Assert.StartsWith(m.Backups, e.Sauvegarde);
        Assert.Equal(original, File.ReadAllText(e.Sauvegarde!));
        Assert.Equal("""{"nouveau":true}""", File.ReadAllText(m.Settings));
        Assert.Empty(m.Temporaires());
    }

    [Fact]
    public void Fichier_modifie_entre_lecture_et_ecriture_n_est_pas_ecrase()
    {
        using var m = new Montage();
        File.WriteAllText(m.Settings, """{"a":1}""");

        var l = m.Passerelle.Lire();
        File.WriteAllText(m.Settings, """{"a":1,"ecrit_par_claude_code":"entre-temps"}""");   // autre écrivain, autre taille

        var e = m.Passerelle.Ecrire(l, """{"chronos":true}""", creerSiAbsent: false);

        Assert.False(e.Ecrit);
        Assert.Contains("modifié", e.Cause);
        Assert.Equal("""{"a":1,"ecrit_par_claude_code":"entre-temps"}""", File.ReadAllText(m.Settings));
        Assert.Empty(m.Temporaires());
    }

    [Fact]
    public void Absent_sans_creation_est_refuse()
    {
        using var m = new Montage();

        var e = m.Passerelle.Ecrire(m.Passerelle.Lire(), "{}", creerSiAbsent: false);

        Assert.False(e.Ecrit);
        Assert.Contains("fichier absent", e.Cause);
        Assert.False(File.Exists(m.Settings));
    }

    [Fact]
    public void Absent_avec_creation_cree_le_fichier_sans_sauvegarde()
    {
        using var m = new Montage();

        var e = m.Passerelle.Ecrire(m.Passerelle.Lire(), """{"hooks":{}}""", creerSiAbsent: true);

        Assert.True(e.Ecrit, e.Cause);
        Assert.Null(e.Sauvegarde);
        Assert.Equal("""{"hooks":{}}""", File.ReadAllText(m.Settings));
        Assert.Empty(m.Sauvegardes());
    }

    [Fact]
    public void Absent_puis_apparu_entre_temps_est_refuse()
    {
        using var m = new Montage();
        var l = m.Passerelle.Lire();
        File.WriteAllText(m.Settings, """{"cree_par_claude_code":true}""");

        var e = m.Passerelle.Ecrire(l, "{}", creerSiAbsent: true);

        Assert.False(e.Ecrit);
        Assert.Contains("modifié", e.Cause);
        Assert.Equal("""{"cree_par_claude_code":true}""", File.ReadAllText(m.Settings));
    }

    /// <summary>FIAB-8 c : File.Move remplacerait le lien par un fichier ordinaire et la cible ne verrait jamais
    /// l'écriture. La création d'un lien exige le mode développeur ou des droits élevés : sans eux, le test
    /// s'arrête proprement (retour anticipé documenté), il ne ment pas.</summary>
    [Fact]
    public void Lien_symbolique_refuse_et_cible_intacte()
    {
        using var m = new Montage();
        var cible = Path.Combine(m.Dossier, "vrai-settings.json");
        File.WriteAllText(cible, """{"a":1}""");
        try { File.CreateSymbolicLink(m.Settings, cible); }
        catch (Exception) { return; }   // lien non permis sur cette machine : rien à prouver ici

        var l = m.Passerelle.Lire();
        var e = m.Passerelle.Ecrire(l, """{"chronos":true}""", creerSiAbsent: false);

        Assert.False(e.Ecrit);
        Assert.Contains("lien symbolique", e.Cause);
        Assert.Equal("""{"a":1}""", File.ReadAllText(cible));
        Assert.True((File.GetAttributes(m.Settings) & FileAttributes.ReparsePoint) != 0);
    }

    [Fact]
    public void La_retention_ne_garde_que_cinq_sauvegardes()
    {
        using var m = new Montage();
        File.WriteAllText(m.Settings, """{"n":0}""");

        for (int i = 1; i <= 7; i++)
        {
            var e = m.Passerelle.Ecrire(m.Passerelle.Lire(), "{\"n\":" + i + "}", creerSiAbsent: false);
            Assert.True(e.Ecrit, e.Cause);
        }

        Assert.InRange(m.Sauvegardes().Length, 1, 5);
        Assert.Equal("{\"n\":7}", File.ReadAllText(m.Settings));
    }

    [Fact]
    public void Lire_puis_Ecrire_ne_levent_jamais_sur_un_dossier_de_sauvegarde_impossible()
    {
        using var m = new Montage();
        File.WriteAllText(m.Settings, """{"a":1}""");
        File.WriteAllText(m.Backups, "je suis un fichier, pas un dossier");   // Directory.CreateDirectory échouera
        var avant = File.ReadAllBytes(m.Settings);

        var e = m.Passerelle.Ecrire(m.Passerelle.Lire(), """{"b":2}""", creerSiAbsent: false);

        Assert.False(e.Ecrit);
        Assert.Contains("sauvegarde impossible", e.Cause);
        Assert.Equal(avant, File.ReadAllBytes(m.Settings));
    }
}
