using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Helper PARTAGÉ des tests de la source app-bureau (plans 29-02, 29-03, 29-05) : les six fixtures RÉELLES de la
/// phase 27, une racine temporaire à la forme exacte de l'app (<c>&lt;org&gt;\&lt;user&gt;\local_*.json</c>), et la
/// dérivation TEXTUELLE d'une fixture.
///
/// <para><b>Aucune re-sérialisation.</b> Une fixture dérivée l'est par remplacement de texte, jamais en passant par
/// un objet puis en le réécrivant : ce qui est testé reste, octet pour octet, ce que l'app a écrit sur le disque —
/// champs inconnus, ordre, indentation et accents compris. Chaque remplacement exige d'abord que le texte visé
/// existe : une faute de frappe dans un test ne passe pas en silence.</para>
///
/// <para><b>Aucune écriture hors du dossier temporaire.</b> Toute racine est créée sous <c>Path.GetTempPath()</c>
/// (asserté) ; les fixtures du dépôt ne sont que LUES. Tout horodatage est ancré sur l'instant injecté par le test,
/// jamais sur l'horloge.</para>
/// </summary>
internal static class RacineAppBureau
{
    /// <summary>UTF-8 SANS marque d'ordre d'octets : la forme des vrais fichiers de l'app.</summary>
    private static readonly UTF8Encoding Utf8SansBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Les octets EXACTS d'une fixture réelle.</summary>
    internal static byte[] Fixture(string nom, [CallerFilePath] string ici = "")
    {
        var chemin = Path.Combine(Path.GetDirectoryName(ici)!, "TestData", "DesktopAppSessions", nom);
        Assert.True(File.Exists(chemin), $"Fixture introuvable : {chemin}");
        return File.ReadAllBytes(chemin);
    }

    /// <summary>Le texte d'une fixture réelle (UTF-8).</summary>
    internal static string Texte(string nom) => Encoding.UTF8.GetString(Fixture(nom));

    /// <summary>
    /// Une fixture réelle dérivée par remplacement ORDINAL de TOUTES les occurrences, dans l'ordre donné. Le texte
    /// visé doit exister avant chaque remplacement.
    /// </summary>
    internal static string Deriver(string nom, params (string Ancien, string Nouveau)[] remplacements)
    {
        var texte = Texte(nom);
        foreach (var (ancien, nouveau) in remplacements)
        {
            Assert.Contains(ancien, texte, StringComparison.Ordinal);
            texte = texte.Replace(ancien, nouveau, StringComparison.Ordinal);
        }
        return texte;
    }

    /// <summary>Une racine NEUVE et vide, sous le dossier temporaire (asserté).</summary>
    internal static string NouvelleRacine()
    {
        var racine = Path.Combine(Path.GetTempPath(), "chronos-appbureau-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(racine);
        Assert.StartsWith(Path.GetTempPath(), racine);   // aucun test n'écrit hors du dossier temporaire
        return racine;
    }

    /// <summary>Le dossier <c>&lt;org&gt;\&lt;user&gt;</c> de la racine, à la profondeur exacte où l'app écrit.</summary>
    internal static string DossierUtilisateur(string racine)
    {
        var dossier = Path.Combine(racine, "org-0001", "user-0001");
        Directory.CreateDirectory(dossier);
        return dossier;
    }

    /// <summary>Écrit un TEXTE dans le dossier utilisateur (UTF-8 sans BOM) et pose sa date d'écriture.</summary>
    internal static string Ecrire(string racine, string nomFichier, string contenu, DateTimeOffset dateEcriture)
    {
        var chemin = Path.Combine(DossierUtilisateur(racine), nomFichier);
        File.WriteAllText(chemin, contenu, Utf8SansBom);
        File.SetLastWriteTimeUtc(chemin, dateEcriture.UtcDateTime);
        return chemin;
    }

    /// <summary>Écrit des OCTETS dans le dossier utilisateur et pose sa date d'écriture.</summary>
    internal static string EcrireOctets(string racine, string nomFichier, byte[] octets, DateTimeOffset dateEcriture)
    {
        var chemin = Path.Combine(DossierUtilisateur(racine), nomFichier);
        File.WriteAllBytes(chemin, octets);
        File.SetLastWriteTimeUtc(chemin, dateEcriture.UtcDateTime);
        return chemin;
    }

    /// <summary>Le nom de fichier de l'app pour une fixture : <c>local_&lt;nom sans .json&gt;.json</c>.</summary>
    internal static string NomFichier(string fixture)
        => "local_" + Path.GetFileNameWithoutExtension(fixture) + ".json";

    /// <summary>
    /// Une racine neuve où chaque fixture est COPIÉE (octets exacts) sous son nom de fichier de l'app, écrite une
    /// minute avant l'instant injecté.
    /// </summary>
    internal static string Creer(DateTimeOffset maintenant, params string[] fixtures)
    {
        var racine = NouvelleRacine();
        DossierUtilisateur(racine);
        foreach (var f in fixtures)
            EcrireOctets(racine, NomFichier(f), Fixture(f), maintenant.AddMinutes(-1));
        return racine;
    }

    /// <summary>
    /// Le BRUIT réel d'un dossier de l'app (29-RESEARCH, Piège 3), aucun ne devant être ouvert : l'index des
    /// archives, un temporaire d'écriture atomique, un sous-dossier <c>backlog</c> (profondeur 3), une trace de
    /// suppression, les tâches planifiées — et des <c>local_*.json</c> posés trop haut (racine, dossier d'org) ou trop
    /// bas (<c>backlog</c>). Tous écrits une minute avant l'instant injecté : seule la PROFONDEUR et le NOM les
    /// écartent, jamais leur âge.
    /// </summary>
    internal static void AjouterBruit(string racine, DateTimeOffset maintenant)
    {
        var date = maintenant.AddMinutes(-1);
        var bloquee = Fixture("fin-de-tour-blocked.json");
        var utilisateur = DossierUtilisateur(racine);
        var backlog = Path.Combine(utilisateur, "backlog");
        Directory.CreateDirectory(backlog);

        void Poser(string chemin, byte[] octets)
        {
            File.WriteAllBytes(chemin, octets);
            File.SetLastWriteTimeUtc(chemin, date.UtcDateTime);
        }

        Poser(Path.Combine(utilisateur, "archived-sessions.idx"), Utf8SansBom.GetBytes("adac2711-86a4-4b4d-b71c-9a594c9d959e\n"));
        Poser(Path.Combine(utilisateur, ".local_x.json.1234.ab12cd.tmp"), bloquee);
        Poser(Path.Combine(backlog, "tasks.json"), Utf8SansBom.GetBytes("[]"));
        Poser(Path.Combine(backlog, "local_profond.json"), bloquee);
        Poser(Path.Combine(utilisateur, "deleted_0c9f2d10-0000-4000-8000-000000000000"), Array.Empty<byte>());
        Poser(Path.Combine(utilisateur, "scheduled-tasks.json"), Utf8SansBom.GetBytes("[]"));
        Poser(Path.Combine(racine, "local_racine.json"), bloquee);
        Poser(Path.Combine(racine, "org-0001", "local_org.json"), bloquee);
    }
}
