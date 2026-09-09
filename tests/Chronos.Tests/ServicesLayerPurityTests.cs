using Xunit;

namespace Chronos.Tests;

/// <summary>
/// Filet permanent (success criterion 4 / DAT-02) : prouve qu'AUCUN assembly WPF n'est
/// exposé dans les signatures publiques des types NEUTRES des namespaces Chronos.Services /
/// Chronos.Models (contrats + pipeline de données). La frontière neutre de la couche données
/// reste ainsi gardée automatiquement à chaque build : un `using System.Windows.*` qui se
/// glisserait dans un provider ou un modèle ferait échouer ce test.
///
/// Exception explicite et documentée : les ADAPTATEURS WPF de Phase 1 (WpfUiDispatcher,
/// TopmostGuard) vivent volontairement dans Chronos.Services — ce sont la frontière WPF assumée
/// (implémentations de IUiDispatcher / topmost overlay, ROB-04). Ils sont exclus par une
/// allow-list nominative : toute NOUVELLE fuite WPF (dans un modèle ou un provider de données)
/// reste détectée.
/// </summary>
public class ServicesLayerPurityTests
{
    // Adaptateurs WPF sanctionnés de Phase 1 (seule frontière WPF autorisée dans Chronos.Services).
    private static readonly string[] AdaptateursWpfAutorises =
    {
        "WpfUiDispatcher",   // implémentation WPF de IUiDispatcher (encapsule Dispatcher)
        "TopmostGuard",      // réaffirmation topmost overlay (Window/DispatcherTimer), ROB-04
        "OverlayController", // adaptateur WPF de placement physique + arrière-plan (Window/HwndSource), FEN-03/04/05
    };

    [Fact]
    public void La_couche_Services_ne_reference_aucun_assembly_WPF()
    {
        // Assembly qui contient les types Services/Models (le projet Chronos).
        var asm = typeof(Chronos.Services.IUsageProvider).Assembly;
        string[] interdits = { "PresentationCore", "PresentationFramework", "WindowsBase" };

        // Types NEUTRES du pipeline : Services/Models, hors adaptateurs WPF assumés (Phase 1).
        var typesData = asm.GetTypes()
            .Where(t => t.Namespace is "Chronos.Services" or "Chronos.Models")
            .Where(t => !AdaptateursWpfAutorises.Contains(t.Name));

        foreach (var t in typesData)
        {
            // Aucun type d'un assembly WPF ne doit apparaître dans les signatures publiques
            // (retours + paramètres de méthodes, types de propriétés) → contrat neutre garanti.
            var assembliesTouches = t.GetMethods()
                .SelectMany(m => new[] { m.ReturnType }.Concat(m.GetParameters().Select(p => p.ParameterType)))
                .Concat(t.GetProperties().Select(p => p.PropertyType))
                .Select(x => x.Assembly.GetName().Name)
                .Where(n => n is not null);

            Assert.DoesNotContain(assembliesTouches, n => interdits.Contains(n));
        }
    }

    /// <summary>
    /// GARDE DE NON-RETOUR (DEL-05, phase 16). Le sous-système de plafonds — cause racine des
    /// pourcentages faux après le passage Max x5 → Max x20 — a été entièrement supprimé. Les limites
    /// Anthropic pondèrent par modèle : « tokens / plafond » reste faux même avec le bon plafond, la
    /// calibration n'a donc aucun avenir. Ce test échoue si un type nommé Budget* réapparaît dans
    /// l'assembly, quelle qu'en soit la voie (rétablissement depuis l'historique, nouvelle
    /// implémentation, dialogue WPF). Le remplacement légitime est la correction par DELTA
    /// (phase 19, DEL-03/DEL-04), qui ne dérive jamais un pourcentage d'un comptage de tokens.
    /// </summary>
    [Fact]
    public void Aucun_type_de_plafond_ne_subsiste_dans_l_assembly()
    {
        var asm = typeof(Chronos.Services.IUsageProvider).Assembly;

        var revenants = asm.GetTypes()
            .Where(t => t.Name.StartsWith("Budget", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(revenants);
    }
}
