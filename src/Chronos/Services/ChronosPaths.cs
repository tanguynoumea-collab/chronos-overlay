using System.IO;

namespace Chronos.Services;

/// <summary>Chemins des sources, injectables pour tester sans toucher le vrai profil utilisateur.</summary>
/// <remarks>
/// <c>UsageFile</c> : usage.json n'est plus écrit depuis la 3.5 ; la propriété ancre le dossier %APPDATA%\Chronos
/// (settings, last-exact, historique, oauth.dat, sessions). Un usage.json résiduel n'est ni lu ni supprimé.
/// </remarks>
public sealed record ChronosPaths(string UsageFile, string ProjectsRoot)
{
    /// <summary>Chemins réels : dossier %APPDATA%\Chronos (ancré par usage.json) et %USERPROFILE%\.claude\projects.</summary>
    public static ChronosPaths Default() => new(
        UsageFile: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chronos", "usage.json"),
        ProjectsRoot: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects"));

    /// <summary>
    /// settings.json dans le dossier ancré par UsageFile (même dossier %APPDATA%\Chronos). Propriété
    /// calculée : le ctor positionnel <c>(UsageFile, ProjectsRoot)</c> reste inchangé, donc les
    /// tests qui construisent un répertoire temp via <c>new ChronosPaths(usage, projects)</c>
    /// obtiennent aussi un SettingsFile isolé.
    /// </summary>
    public string SettingsFile => Path.Combine(Path.GetDirectoryName(UsageFile)!, "settings.json");

    /// <summary>
    /// last-exact.json dans le dossier ancré par UsageFile (même dossier %APPDATA%\Chronos). Propriété
    /// calculée : le ctor positionnel <c>(UsageFile, ProjectsRoot)</c> reste inchangé, donc les
    /// tests qui construisent un répertoire temp obtiennent aussi un last-exact.json isolé.
    /// </summary>
    public string LastExactFile => Path.Combine(Path.GetDirectoryName(UsageFile)!, "last-exact.json");

    /// <summary>
    /// JRN-01 — dossier du journal des relevés (releves-AAAA-MM.jsonl), dans le dossier ancré par UsageFile et dérivé
    /// comme les autres chemins, jamais construit en dur : les tests obtiennent un journal isolé sans retouche
    /// du ctor positionnel. Contrat de chemin que 32-05 câble.
    /// </summary>
    public string HistoriqueDir => Path.Combine(Path.GetDirectoryName(UsageFile)!, "historique");
}
