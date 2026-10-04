using System.IO;
using System.Runtime.InteropServices;

namespace Chronos.Services;

/// <summary>
/// Autostart via un raccourci .lnk dans shell:startup (DEP-02) — aucun droit admin, aucune
/// dépendance native. Le dossier startup et l'exe sont injectables pour permettre des tests sans polluer
/// le vrai Startup. Type NEUTRE (aucun type WPF) → garde de pureté verte.
/// <para>
/// PKG-1 (42.2) : les exe publiés sont versionnés (<c>Chronos-v3.4.0.exe</c>, <c>Chronos-v3.5.0.exe</c>…) et coexistent
/// sur le disque. Un raccourci créé par une version précédente vise l'ANCIEN exe : au redémarrage suivant la mise à jour,
/// l'ancienne version reprendrait la main (verrou d'instance, hooks repointés vers elle). Donc « activé » = le raccourci
/// existe ET vise l'exe courant, et <see cref="ConvergerVersExeCourant"/> repointe au démarrage un raccourci existant.
/// </para>
/// </summary>
public sealed class AutostartService : IAutostartService
{
    private readonly string _startupFolder;
    private readonly string _linkName;
    private readonly string? _exePath;

    /// <param name="startupFolder">Dossier startup ; par défaut le vrai shell:startup (per-user, sans admin).</param>
    /// <param name="linkName">Nom du raccourci créé.</param>
    /// <param name="exePath">Exe visé (tests) ; par défaut <see cref="Environment.ProcessPath"/>.</param>
    public AutostartService(string? startupFolder = null, string linkName = "Chronos.lnk", string? exePath = null)
    {
        _startupFolder = startupFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        _linkName = linkName;
        _exePath = exePath;
    }

    private string LinkPath => Path.Combine(_startupFolder, _linkName);

    // Environment.ProcessPath est single-file-safe (PAS l'emplacement d'assembly, vide en mono-fichier).
    private string ExeCourant => _exePath ?? Environment.ProcessPath!;

    public bool IsEnabled() => CibleDuRaccourci() is { } c && MemeChemin(c, ExeCourant);

    /// <summary>Cible du raccourci, ou null s'il est absent ou illisible. Ne lève jamais.</summary>
    public string? CibleDuRaccourci()
    {
        if (!File.Exists(LinkPath)) return null;
        object? shell = null;
        object? lnk = null;
        try
        {
            shell = CreerShell();
            lnk = ((dynamic)shell).CreateShortcut(LinkPath);
            string? cible = ((dynamic)lnk).TargetPath;
            return string.IsNullOrWhiteSpace(cible) ? null : cible;
        }
        catch
        {
            return null;   // .lnk corrompu, COM indisponible : cible inconnue
        }
        finally
        {
            Liberer(lnk);
            Liberer(shell);
        }
    }

    public void Disable()
    {
        if (File.Exists(LinkPath)) File.Delete(LinkPath);
    }

    public void Enable()
    {
        Directory.CreateDirectory(_startupFolder);

        var exe = ExeCourant;

        // COM late-bound via WScript.Shell : aucun NuGet, aucune interop IWshRuntimeLibrary à référencer.
        object? shell = null;
        object? lnk = null;
        try
        {
            shell = CreerShell();
            lnk = ((dynamic)shell).CreateShortcut(LinkPath);
            dynamic l = lnk!;
            l.TargetPath = exe;
            l.WorkingDirectory = Path.GetDirectoryName(exe);
            l.Description = "Chronos — overlay de quotas Claude";
            l.Save();
        }
        finally
        {
            Liberer(lnk);
            Liberer(shell);
        }
    }

    /// <summary>
    /// PKG-1 : repointe un raccourci EXISTANT vers l'exe courant (même logique « converger vers l'exe courant » que le
    /// réconciliateur des réglages de Claude Code). Absent → rien créé ; déjà conforme → rien réécrit ; ne lève jamais.
    /// </summary>
    public BilanAutostart ConvergerVersExeCourant()
    {
        try
        {
            if (!File.Exists(LinkPath)) return BilanAutostart.Absent;
            if (CibleDuRaccourci() is { } c && MemeChemin(c, ExeCourant)) return BilanAutostart.Conforme;
            Enable();
            return BilanAutostart.Repointe;
        }
        catch
        {
            return BilanAutostart.Echec;
        }
    }

    private static object CreerShell()
        => Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;

    private static void Liberer(object? o)
    {
        if (o is not null && Marshal.IsComObject(o))
        {
            try { Marshal.FinalReleaseComObject(o); } catch { /* libération best-effort */ }
        }
    }

    private static bool MemeChemin(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;   // chemin malformé : jamais « conforme »
        }
    }
}
