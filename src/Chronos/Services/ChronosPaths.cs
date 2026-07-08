using System.IO;

namespace Chronos.Services;

/// <summary>Chemins des sources, injectables pour tester sans toucher le vrai profil utilisateur.</summary>
public sealed record ChronosPaths(string UsageFile, string ProjectsRoot)
{
    /// <summary>Chemins réels : %APPDATA%\Chronos\usage.json et %USERPROFILE%\.claude\projects.</summary>
    public static ChronosPaths Default() => new(
        UsageFile: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chronos", "usage.json"),
        ProjectsRoot: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects"));
}
