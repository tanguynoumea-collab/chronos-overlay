using System;
using System.IO;
using Xunit;

namespace Chronos.Tests;

/// <summary>
/// SEC-R3 (42.2-11) — un <see cref="FactAttribute"/> qui SAUTE explicitement le test quand la machine ne permet pas de
/// créer un lien symbolique (ni mode développeur, ni droits élevés). Sans lui, le test faisait un <c>return</c> silencieux
/// et se comptait « réussi » sans rien vérifier ; désormais il apparaît « ignoré » dans le bilan de la suite.
/// La sonde crée puis supprime un lien dans un dossier temporaire (jamais le vrai profil).
/// </summary>
public sealed class LienSymboliqueFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> Permis = new(Sonder);

    public LienSymboliqueFactAttribute()
    {
        if (!Permis.Value)
            Skip = "Création de lien symbolique non permise sur cette machine (mode développeur ou droits élevés requis).";
    }

    private static bool Sonder()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "ChronosSondeLien_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dossier);
            var cible = Path.Combine(dossier, "cible.txt");
            File.WriteAllText(cible, "x");
            File.CreateSymbolicLink(Path.Combine(dossier, "lien.txt"), cible);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            try { Directory.Delete(dossier, recursive: true); } catch { /* nettoyage best-effort */ }
        }
    }
}
