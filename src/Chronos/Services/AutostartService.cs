using System.IO;

namespace Chronos.Services;

/// <summary>
/// Autostart via un raccourci .lnk dans shell:startup (DEP-02) — aucun droit admin, aucune
/// dépendance native. Le dossier startup est injectable pour permettre des tests sans polluer
/// le vrai Startup. Type NEUTRE (aucun type WPF) → garde de pureté verte.
/// </summary>
public sealed class AutostartService : IAutostartService
{
    private readonly string _startupFolder;
    private readonly string _linkName;

    /// <param name="startupFolder">Dossier startup ; par défaut le vrai shell:startup (per-user, sans admin).</param>
    /// <param name="linkName">Nom du raccourci créé.</param>
    public AutostartService(string? startupFolder = null, string linkName = "Chronos.lnk")
    {
        _startupFolder = startupFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        _linkName = linkName;
    }

    private string LinkPath => Path.Combine(_startupFolder, _linkName);

    public bool IsEnabled() => File.Exists(LinkPath);

    public void Disable()
    {
        if (File.Exists(LinkPath)) File.Delete(LinkPath);
    }

    public void Enable()
    {
        Directory.CreateDirectory(_startupFolder);

        // Cible = exe courant. Environment.ProcessPath est single-file-safe (PAS Assembly.Location, vide en mono-fichier).
        var exe = Environment.ProcessPath!;

        // COM late-bound via WScript.Shell : aucun NuGet, aucune interop IWshRuntimeLibrary à référencer.
        var t = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(t)!;
        var lnk = shell.CreateShortcut(LinkPath);
        lnk.TargetPath = exe;
        lnk.WorkingDirectory = Path.GetDirectoryName(exe);
        lnk.Description = "Chronos — overlay de quotas Claude";
        lnk.Save();
    }
}
