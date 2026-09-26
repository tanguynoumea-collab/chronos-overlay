using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Chronos.Services;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// VAL-03 — la version publiée est UNE. Les quatre propriétés de version du csproj (<c>Version</c>, <c>FileVersion</c>,
/// <c>AssemblyVersion</c>, <c>InformationalVersion</c>) disent la même version, et l'assembly compilée porte exactement
/// ce que le csproj déclare — c'est elle que lit la ligne « Version : » du rapport de diagnostic.
///
/// Aucune valeur en dur ici — la valeur publiée est tenue par le plan de release (grep du csproj, VersionInfo de l'exe)
/// et par le nom du fichier (<c>Chronos-vX.Y.Z.exe</c>). Un test qui épinglerait la version serait à réécrire à chaque
/// release sans rien garder de plus.
/// </summary>
public sealed class VersionPublieeTests
{
    [Fact]
    public void Les_quatre_proprietes_de_version_du_csproj_sont_coherentes()
    {
        var version = P("Version");

        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
        Assert.Equal(version, P("InformationalVersion"));
        Assert.Equal(version + ".0", P("FileVersion"));
        Assert.Equal(version + ".0", P("AssemblyVersion"));
        // Sans elle, le SDK ajoute « +<sha> » à la version informative : le rapport et les propriétés du fichier
        // diraient autre chose que le csproj et que le nom du fichier publié.
        Assert.Equal("false", P("IncludeSourceRevisionInInformationalVersion"));
    }

    [Fact]
    public void L_assembly_compilee_porte_la_version_du_csproj()
    {
        var assembly = typeof(DiagnosticService).Assembly;

        Assert.Equal(P("InformationalVersion"), assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        Assert.Equal(P("FileVersion"), assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version);
        Assert.Equal(P("AssemblyVersion"), assembly.GetName().Version?.ToString());
    }

    // Une propriété du csproj, par son nom d'élément ; exactement une occurrence (Single rougit sinon).
    private static string P(string nom) => Csproj().Descendants(nom).Single().Value.Trim();

    // Le csproj est trouvé par le chemin INJECTÉ par MSBuild (CheminSourcesChronos), jamais deviné. Anti-muet : une garde
    // qui ne trouve plus son fichier ne garde rien, et elle ne le dit pas.
    private static XDocument Csproj()
    {
        var sources = GardesPerimetreTests.CheminSources();
        Assert.False(string.IsNullOrEmpty(sources), "attribut CheminSourcesChronos absent : la garde ne sait plus où lire le csproj");
        var fichier = Path.Combine(sources, "Chronos.csproj");
        Assert.True(File.Exists(fichier), "Chronos.csproj introuvable : " + fichier);
        return XDocument.Load(fichier);
    }
}
