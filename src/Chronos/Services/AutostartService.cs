using System.Diagnostics;
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
/// <para>
/// PKG-R1 (42.2-11) : « dernier lancé gagne » abandonné. Un build F5 (<c>bin\Debug</c>), une copie lancée depuis le dossier
/// temporaire ou une version plus ancienne reprenait l'autostart en silence. On ne repointe plus que si la cible a DISPARU
/// du disque, ou si elle est d'une version STRICTEMENT inférieure à l'exe courant ; jamais depuis un emplacement jetable.
/// </para>
/// </summary>
public sealed class AutostartService : IAutostartService
{
    private readonly string _startupFolder;
    private readonly string _linkName;
    private readonly string? _exePath;
    private readonly Func<string, Version?> _lireVersion;
    private readonly string _dossierTemporaire;

    /// <param name="startupFolder">Dossier startup ; par défaut le vrai shell:startup (per-user, sans admin).</param>
    /// <param name="linkName">Nom du raccourci créé.</param>
    /// <param name="exePath">Exe visé (tests) ; par défaut <see cref="Environment.ProcessPath"/>.</param>
    /// <param name="lireVersion">Lecture de la version d'un exe (tests) ; par défaut la FileVersion du fichier, null si illisible.</param>
    /// <param name="dossierTemporaire">Dossier considéré comme jetable (tests) ; par défaut <see cref="Path.GetTempPath"/>.</param>
    public AutostartService(string? startupFolder = null, string linkName = "Chronos.lnk", string? exePath = null,
                            Func<string, Version?>? lireVersion = null, string? dossierTemporaire = null)
    {
        _startupFolder = startupFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        _linkName = linkName;
        _exePath = exePath;
        _lireVersion = lireVersion ?? VersionDuFichier;
        _dossierTemporaire = dossierTemporaire ?? Path.GetTempPath();
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
    /// PKG-1 : repointe un raccourci EXISTANT vers l'exe courant. Absent → rien créé ; déjà conforme → rien réécrit ; ne lève jamais.
    /// PKG-R1 (42.2-11) : le repointage n'a lieu que si (a) la cible du raccourci n'existe plus (ou est illisible), ou
    /// (b) la version de la cible est STRICTEMENT inférieure à celle de l'exe courant — et jamais si l'exe courant vit dans un
    /// emplacement jetable (<see cref="EmplacementJetable"/>). Sinon <see cref="BilanAutostart.Ignore"/> : le raccourci est conservé.
    /// Une version illisible d'un côté ou de l'autre conserve aussi le raccourci (prudence : on ne déloge pas sans preuve).
    /// </summary>
    public BilanAutostart ConvergerVersExeCourant()
    {
        try
        {
            if (!File.Exists(LinkPath)) return BilanAutostart.Absent;
            var cible = CibleDuRaccourci();
            if (cible is not null && MemeChemin(cible, ExeCourant)) return BilanAutostart.Conforme;
            if (EmplacementJetable(ExeCourant, _dossierTemporaire)) return BilanAutostart.Ignore;

            var cibleDisparue = cible is null || !File.Exists(cible);
            if (!cibleDisparue)
            {
                var vCible = _lireVersion(cible!);
                var vCourant = _lireVersion(ExeCourant);
                if (vCible is null || vCourant is null || vCourant <= vCible) return BilanAutostart.Ignore;
            }
            Enable();
            return BilanAutostart.Repointe;
        }
        catch
        {
            return BilanAutostart.Echec;
        }
    }

    /// <summary>
    /// PKG-R1 — vrai si l'exe est un build de développement (un segment de chemin <c>bin</c>, quelle que soit la casse) ou
    /// s'il vit sous <paramref name="dossierTemporaire"/>. Fonction pure ; un chemin malformé compte comme jetable (prudence).
    /// </summary>
    public static bool EmplacementJetable(string exe, string dossierTemporaire)
    {
        try
        {
            var plein = Path.GetFullPath(exe);
            var segments = Path.GetDirectoryName(plein)?.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? Array.Empty<string>();
            if (segments.Any(seg => string.Equals(seg, "bin", StringComparison.OrdinalIgnoreCase))) return true;

            var temp = Path.GetFullPath(dossierTemporaire);
            if (!temp.EndsWith(Path.DirectorySeparatorChar)) temp += Path.DirectorySeparatorChar;
            return plein.StartsWith(temp, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>FileVersion du fichier (ressource de version Win32), null si absente ou illisible. Ne lève jamais.</summary>
    private static Version? VersionDuFichier(string chemin)
    {
        try
        {
            var fvi = FileVersionInfo.GetVersionInfo(chemin);
            if (fvi.FileMajorPart == 0 && fvi.FileMinorPart == 0 && fvi.FileBuildPart == 0 && fvi.FilePrivatePart == 0) return null;
            return new Version(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart);
        }
        catch
        {
            return null;
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
